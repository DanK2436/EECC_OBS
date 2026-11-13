using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EeccStudio.Core.Network
{
    public enum TallyState
    {
        Standby,
        Preview,
        Program
    }

    public class CameraClient
    {
        public string StreamName { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public WebSocket Socket { get; set; } = null!;
        public TallyState CurrentTally { get; set; } = TallyState.Standby;
        public int BatteryLevel { get; set; } = 100;
        public bool IsCharging { get; set; }
        public int Fps { get; set; } = 30;
        public int BitrateKbps { get; set; } = 5000;
        public DateTime LastTelemetryTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Serveur Broadcast & Caméra Haute Performance pour EECC Studio.
    /// Gère la réception des caméras mobiles, la diffusion locale LAN pour EECC TV,
    /// et les endpoints HTTP/WebSocket pour l'ensemble de l'écosystème.
    /// </summary>
    public class CameraServer
    {
        private readonly HttpListener _listener = new();
        private readonly ConcurrentDictionary<string, CameraClient> _connectedCameras = new();
        private CancellationTokenSource? _cts;
        private bool _isRunning;

        public event Action<CameraClient>? OnCameraConnected;
        public event Action<string>? OnCameraDisconnected;
        public event Action<CameraClient>? OnTelemetryReceived;
        public event Action<bool>? OnStreamingStateChanged;
        public event Action<bool>? OnRecordingStateChanged;

        public int Port { get; }
        public string PairingToken { get; set; } = "EECC-LIVE-4982";
        public string ChurchName { get; set; } = "Église Évangélique les Cohéritiers du Christ (EECC)";
        public string ProgramTitle { get; set; } = "Grand Culte d'Adoration et d'Enseignement";
        public string Speaker { get; set; } = "Pasteur Principal";
        public string ActiveSceneName { get; set; } = "Orateur Principal (Pupitre)";

        public bool IsStreaming { get; private set; }
        public bool IsRecording { get; private set; }
        public DateTime? StreamingStartedAt { get; private set; }
        public DateTime? RecordingStartedAt { get; private set; }
        public int ViewersCount { get; set; } = 1;

        public TimeSpan StreamingDuration => IsStreaming && StreamingStartedAt.HasValue
            ? DateTime.UtcNow - StreamingStartedAt.Value
            : TimeSpan.Zero;

        public TimeSpan RecordingDuration => IsRecording && RecordingStartedAt.HasValue
            ? DateTime.UtcNow - RecordingStartedAt.Value
            : TimeSpan.Zero;

        public CameraServer(int port = 8089)
        {
            Port = port;
        }

        public string GetLocalIpAddress()
        {
            try
            {
                foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (netInterface.OperationalStatus != OperationalStatus.Up ||
                        netInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    var ipProps = netInterface.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(addr.Address))
                        {
                            var ipStr = addr.Address.ToString();
                            if (ipStr.StartsWith("192.168.") || ipStr.StartsWith("10.") || ipStr.StartsWith("172."))
                            {
                                return ipStr;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback silencieux
            }
            return "192.168.1.100";
        }

        public void Start()
        {
            if (_isRunning) return;

            _cts = new CancellationTokenSource();
            _listener.Prefixes.Clear();

            try
            {
                _listener.Prefixes.Add($"http://*:{Port}/");
                _listener.Start();
                _isRunning = true;
                Task.Run(() => ListenLoopAsync(_cts.Token));
            }
            catch (Exception)
            {
                // Fallback si la réservation de préfixe global nécessite élévation administrateur
                _listener.Prefixes.Clear();
                _listener.Prefixes.Add($"http://localhost:{Port}/");
                _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
                try
                {
                    _listener.Start();
                    _isRunning = true;
                    Task.Run(() => ListenLoopAsync(_cts.Token));
                }
                catch
                {
                    // Listener ne peut pas être lié
                }
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _cts?.Cancel();
            try { _listener.Stop(); } catch { }
            _isRunning = false;
        }

        public void StartStreaming()
        {
            IsStreaming = true;
            StreamingStartedAt = DateTime.UtcNow;
            OnStreamingStateChanged?.Invoke(true);
        }

        public void StopStreaming()
        {
            IsStreaming = false;
            StreamingStartedAt = null;
            OnStreamingStateChanged?.Invoke(false);
        }

        public void StartRecording()
        {
            IsRecording = true;
            RecordingStartedAt = DateTime.UtcNow;
            OnRecordingStateChanged?.Invoke(true);
        }

        public void StopRecording()
        {
            IsRecording = false;
            RecordingStartedAt = null;
            OnRecordingStateChanged?.Invoke(false);
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        _ = ProcessWebSocketAsync(context, token);
                    }
                    else
                    {
                        _ = ProcessHttpRequestAsync(context, token);
                    }
                }
                catch when (token.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    // Erreur transitoire de socket
                }
            }
        }

        private async Task ProcessHttpRequestAsync(HttpListenerContext context, CancellationToken token)
        {
            var req = context.Request;
            var res = context.Response;
            var path = req.Url?.AbsolutePath.ToLowerInvariant() ?? "/";

            // En-têtes CORS universels pour l'application mobile EECC TV et les navigateurs
            res.Headers.Add("Access-Control-Allow-Origin", "*");
            res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            res.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 200;
                res.Close();
                return;
            }

            try
            {
                if (path == "/live/status" || path == "/status" || path == "/api/status")
                {
                    // Statut de diffusion en direct pour EECC TV
                    res.StatusCode = 200;
                    res.ContentType = "application/json; charset=utf-8";

                    var localIp = GetLocalIpAddress();
                    var statusData = new
                    {
                        service = "EECC Studio Broadcast Server",
                        church = ChurchName,
                        is_live = IsStreaming,
                        status = IsStreaming ? "live" : "standby",
                        title = ProgramTitle,
                        speaker = Speaker,
                        active_scene = ActiveSceneName,
                        live_stream_url = $"http://{localIp}:{Port}/live/stream",
                        resolution = "1920x1080",
                        fps = 60,
                        bitrate_kbps = 5850,
                        audio = "48kHz Stéréo",
                        viewers = ViewersCount,
                        uptime_seconds = (int)StreamingDuration.TotalSeconds,
                        cameras_connected = _connectedCameras.Count
                    };

                    var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(statusData, new JsonSerializerOptions { WriteIndented = true });
                    res.ContentLength64 = jsonBytes.Length;
                    await res.OutputStream.WriteAsync(jsonBytes, token);
                }
                else if (path == "/camera/pair" || path == "/camera/config" || path == "/api/pair")
                {
                    // Informations d'appairage QR Code pour EECC Caméra
                    res.StatusCode = 200;
                    res.ContentType = "application/json; charset=utf-8";

                    var localIp = GetLocalIpAddress();
                    var pairData = new
                    {
                        studio_ip = localIp,
                        port = Port,
                        token = PairingToken,
                        stream_name = "Caméra Mobile 1",
                        church = ChurchName,
                        codec = "h264",
                        bitrate_kbps = 4500,
                        fps = 30
                    };

                    var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(pairData);
                    res.ContentLength64 = jsonBytes.Length;
                    await res.OutputStream.WriteAsync(jsonBytes, token);
                }
                else if (path == "/live/stream" || path == "/stream")
                {
                    // Flux vidéo en direct (Stream continu MJPEG / Test Card Broadcast avec Tally)
                    res.StatusCode = 200;
                    res.ContentType = "multipart/x-mixed-replace; boundary=--frame";

                    var boundaryBytes = Encoding.ASCII.GetBytes("\r\n--frame\r\nContent-Type: image/jpeg\r\n\r\n");
                    var frameBmp = GenerateBroadcastTestFrame(ActiveSceneName, IsStreaming);

                    try
                    {
                        while (!token.IsCancellationRequested && res.OutputStream.CanWrite)
                        {
                            await res.OutputStream.WriteAsync(boundaryBytes, token);
                            await res.OutputStream.WriteAsync(frameBmp, token);
                            await res.OutputStream.FlushAsync(token);
                            await Task.Delay(100, token); // ~10 FPS pour le flux de monitoring
                        }
                    }
                    catch
                    {
                        // Client déconnecté
                    }
                }
                else
                {
                    // Page Web d'accueil du diffuseur local EECC Studio
                    res.StatusCode = 200;
                    res.ContentType = "text/html; charset=utf-8";

                    var localIp = GetLocalIpAddress();
                    var html = $@"<!DOCTYPE html>
<html lang=""fr"">
<head>
  <meta charset=""UTF-8"">
  <title>EECC Studio — Diffusion Directe</title>
  <style>
    body {{ background: #0A0D14; color: #F0F6FC; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 0; padding: 30px; text-align: center; }}
    .badge {{ display: inline-block; padding: 6px 14px; border-radius: 20px; font-weight: bold; font-size: 13px; margin-bottom: 15px; }}
    .live {{ background: #DC2626; color: white; animation: pulse 1.5s infinite; }}
    .standby {{ background: #30363D; color: #8B949E; }}
    .card {{ background: #161B22; border: 1px solid #30363D; border-radius: 16px; max-width: 680px; margin: 0 auto; padding: 24px; box-shadow: 0 10px 30px rgba(0,0,0,0.5); }}
    .monitor {{ width: 100%; height: 380px; background: #000; border-radius: 10px; margin-bottom: 20px; object-fit: contain; }}
    h1 {{ font-size: 24px; margin-bottom: 6px; color: #FFFFFF; }}
    p.sub {{ color: #8B949E; font-size: 14px; margin-bottom: 20px; }}
    @keyframes pulse {{ 0% {{ opacity: 1; }} 50% {{ opacity: 0.7; }} 100% {{ opacity: 1; }} }}
  </style>
</head>
<body>
  <div class=""card"">
    <div class=""badge {(IsStreaming ? "live" : "standby")}"">
      {(IsStreaming ? "● DIFFUSION EN DIRECT ACTIVER" : "○ STUDIO EN ATTENTE")}
    </div>
    <h1>{ChurchName}</h1>
    <p class=""sub"">{ProgramTitle} — {Speaker}</p>
    <img class=""monitor"" src=""/live/stream"" alt=""Moniteur Programme Direct"">
    <div style=""display:flex; justify-content:space-around; color:#8B949E; font-size:12px;"">
      <div>Scène active : <strong style=""color:#F0F6FC;"">{ActiveSceneName}</strong></div>
      <div>Résolution : <strong style=""color:#16A34A;"">1080p60</strong></div>
      <div>Serveur : <strong style=""color:#38BDF8;"">http://{localIp}:{Port}</strong></div>
    </div>
  </div>
</body>
</html>";
                    var htmlBytes = Encoding.UTF8.GetBytes(html);
                    res.ContentLength64 = htmlBytes.Length;
                    await res.OutputStream.WriteAsync(htmlBytes, token);
                }
            }
            catch
            {
                res.StatusCode = 500;
            }
            finally
            {
                try { res.Close(); } catch { }
            }
        }

        private byte[] GenerateBroadcastTestFrame(string sceneName, bool isLive)
        {
            // Génère une image JPEG de monitoring broadcast ultra-légère
            try
            {
                using var bmp = new System.Drawing.Bitmap(640, 360);
                using var g = System.Drawing.Graphics.FromImage(bmp);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Fond studio sombre
                g.Clear(System.Drawing.ColorTranslator.FromHtml("#0D1117"));

                // Mire broadcast
                using var gridPen = new System.Drawing.Pen(System.Drawing.ColorTranslator.FromHtml("#161B22"), 1);
                for (int x = 0; x < 640; x += 40) g.DrawLine(gridPen, x, 0, x, 360);
                for (int y = 0; y < 360; y += 40) g.DrawLine(gridPen, 0, y, 640, y);

                // Badge Live / Programme
                var badgeColor = isLive ? System.Drawing.ColorTranslator.FromHtml("#DC2626") : System.Drawing.ColorTranslator.FromHtml("#30363D");
                using var badgeBrush = new System.Drawing.SolidBrush(badgeColor);
                g.FillRectangle(badgeBrush, 24, 20, 160, 30);

                using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
                using var fontBold = new System.Drawing.Font("Segoe UI", 11, System.Drawing.FontStyle.Bold);
                using var fontScene = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold);
                using var fontSub = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Regular);

                g.DrawString(isLive ? "● EN DIRECT (PGM)" : "○ PROGRAMME (STANDBY)", fontBold, textBrush, 30, 25);

                // Titre Scène
                var sfCenter = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center, LineAlignment = System.Drawing.StringAlignment.Center };
                g.DrawString(sceneName, fontScene, textBrush, 320, 160, sfCenter);

                using var churchBrush = new System.Drawing.SolidBrush(System.Drawing.ColorTranslator.FromHtml("#9CA3AF"));
                g.DrawString("Église Évangélique les Cohéritiers du Christ (EECC)", fontSub, churchBrush, 320, 200, sfCenter);

                // Timecode
                using var timeBrush = new System.Drawing.SolidBrush(System.Drawing.ColorTranslator.FromHtml("#16A34A"));
                using var fontMono = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Bold);
                var timeStr = DateTime.Now.ToString("HH:mm:ss.ff");
                g.DrawString(timeStr, fontMono, timeBrush, 320, 240, sfCenter);

                using var ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                return ms.ToArray();
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        private async Task ProcessWebSocketAsync(HttpListenerContext context, CancellationToken token)
        {
            HttpListenerWebSocketContext wsContext;
            try
            {
                wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
            }
            catch
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
                return;
            }

            var socket = wsContext.WebSocket;
            var buffer = new byte[8192];
            CameraClient? client = null;

            try
            {
                while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var type = root.GetProperty("type").GetString();

                    if (type == "handshake")
                    {
                        var streamName = root.TryGetProperty("stream_name", out var sn) ? sn.GetString() ?? "Cam-1" : "Cam-1";
                        var pairingToken = root.TryGetProperty("token", out var tk) ? tk.GetString() ?? "" : "";

                        client = new CameraClient
                        {
                            StreamName = streamName,
                            Token = pairingToken,
                            Socket = socket,
                            CurrentTally = TallyState.Standby,
                        };

                        _connectedCameras[streamName] = client;
                        OnCameraConnected?.Invoke(client);
                    }
                    else if (type == "telemetry" && client != null)
                    {
                        if (root.TryGetProperty("payload", out var payload))
                        {
                            client.BatteryLevel = payload.GetProperty("batteryLevel").GetInt32();
                            client.IsCharging = payload.GetProperty("isCharging").GetBoolean();
                            client.Fps = payload.GetProperty("fps").GetInt32();
                            client.BitrateKbps = payload.GetProperty("currentBitrateKbps").GetInt32();
                            client.LastTelemetryTime = DateTime.UtcNow;

                            OnTelemetryReceived?.Invoke(client);
                        }
                    }
                }
            }
            catch
            {
                // Connexion interrompue
            }
            finally
            {
                if (client != null)
                {
                    _connectedCameras.TryRemove(client.StreamName, out _);
                    OnCameraDisconnected?.Invoke(client.StreamName);
                }
                socket.Dispose();
            }
        }

        public async Task SetTallyAsync(string streamName, TallyState tally)
        {
            if (_connectedCameras.TryGetValue(streamName, out var client) && client.Socket.State == WebSocketState.Open)
            {
                client.CurrentTally = tally;
                var tallyStr = tally switch
                {
                    TallyState.Program => "program",
                    TallyState.Preview => "preview",
                    _ => "standby"
                };

                var payload = $"{{\"type\":\"tally\",\"state\":\"{tallyStr}\"}}";
                var bytes = Encoding.UTF8.GetBytes(payload);
                await client.Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}
