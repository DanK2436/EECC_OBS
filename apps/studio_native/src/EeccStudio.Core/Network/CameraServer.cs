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
        public int SlotNumber { get; set; } = 1;
        public string DeviceModel { get; set; } = "Smartphone";
        public string RemoteIp { get; set; } = string.Empty;
        public int BatteryLevel { get; set; } = 100;
        public bool IsCharging { get; set; }
        public int Fps { get; set; } = 30;
        public int BitrateKbps { get; set; } = 5000;
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastTelemetryTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Serveur Broadcast & Matrice Multi-Caméra Haute Performance pour EECC Studio.
    /// Gère jusqu'à 15 smartphones simultanés (Cam-1 à Cam-15), la diffusion locale LAN pour EECC TV,
    /// et les endpoints HTTP/WebSocket pour l'ensemble de l'écosystème.
    /// </summary>
    public class CameraServer
    {
        public const int MaxCameraSlots = 15;
        private HttpListener? _listener;
        private readonly ConcurrentDictionary<string, CameraClient> _connectedCameras = new();
        private readonly ConcurrentDictionary<string, byte[]> _cameraFrames = new();
        private CancellationTokenSource? _cts;
        private bool _isRunning;

        // Image JPEG standard broadcast (640x360) pré-encodée sans dépendance GDI+/System.Drawing
        private static readonly byte[] StaticJpegFrame = new byte[] {
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x01, 0x00, 0x48,
            0x00, 0x48, 0x00, 0x00, 0xFF, 0xDB, 0x00, 0x43, 0x00, 0x08, 0x06, 0x06, 0x07, 0x06, 0x05, 0x08,
            0x07, 0x07, 0x07, 0x09, 0x09, 0x08, 0x0A, 0x0C, 0x14, 0x0D, 0x0C, 0x0B, 0x0B, 0x0C, 0x19, 0x12,
            0x13, 0x0F, 0x14, 0x1D, 0x1A, 0x1F, 0x1E, 0x1D, 0x1A, 0x1C, 0x1C, 0x20, 0x24, 0x2E, 0x27, 0x20,
            0x22, 0x2C, 0x23, 0x1C, 0x1C, 0x28, 0x37, 0x29, 0x2C, 0x30, 0x31, 0x34, 0x34, 0x34, 0x1F, 0x27,
            0x39, 0x3D, 0x38, 0x32, 0x3C, 0x2E, 0x33, 0x34, 0x32, 0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x01, 0x68,
            0x02, 0x80, 0x01, 0x01, 0x11, 0x00, 0xFF, 0xC4, 0x00, 0x1F, 0x00, 0x00, 0x01, 0x05, 0x01, 0x01,
            0x01, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04,
            0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F,
            0x00, 0x7F, 0x00, 0xFF, 0xD9
        };

        public event Action<CameraClient>? OnCameraConnected;
        public event Action<string>? OnCameraDisconnected;
        public event Action<CameraClient>? OnTelemetryReceived;
        public event Action<string, byte[]>? OnCameraFrameReceived;
        public event Action<bool>? OnStreamingStateChanged;
        public event Action<bool>? OnRecordingStateChanged;

        public byte[]? LatestCameraFrameBytes { get; set; }
        public byte[]? ActiveProgramFrameBytes { get; set; }
        public int TotalFramesReceived { get; private set; }

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

        public IReadOnlyDictionary<string, CameraClient> ConnectedCameras => _connectedCameras;
        public IReadOnlyDictionary<string, byte[]> CameraFrames => _cameraFrames;

        public byte[]? GetCameraFrame(string slotName)
        {
            return _cameraFrames.TryGetValue(slotName, out var frame) ? frame : null;
        }

        public string AllocateSlot(string? requestedSlot)
        {
            if (!string.IsNullOrWhiteSpace(requestedSlot))
            {
                var clean = requestedSlot.Trim();
                if (clean.StartsWith("Cam-", StringComparison.OrdinalIgnoreCase))
                {
                    if (!_connectedCameras.ContainsKey(clean)) return clean;
                }
            }

            for (int i = 1; i <= MaxCameraSlots; i++)
            {
                var slot = $"Cam-{i}";
                if (!_connectedCameras.ContainsKey(slot)) return slot;
            }
            return $"Cam-{MaxCameraSlots}";
        }

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

        public bool TryStart(out string detail)
        {
            if (_isRunning)
            {
                detail = string.Empty;
                return true;
            }

            if (TryListen($"http://+:{Port}/", out detail))
            {
                detail = string.Empty;
                return true;
            }

            var wideError = detail;
            if (TryListen($"http://127.0.0.1:{Port}/", out detail))
            {
                detail = "écoute locale uniquement (" + wideError + ")";
                return true;
            }

            detail = wideError + " | " + detail;
            return false;
        }

        public void Start()
        {
            TryStart(out _);
        }

        private bool TryListen(string prefix, out string error)
        {
            error = string.Empty;
            var listener = new HttpListener();
            try
            {
                listener.Prefixes.Add(prefix);
                listener.Start();
                _listener = listener;
                _cts = new CancellationTokenSource();
                _isRunning = true;
                Task.Run(() => ListenLoopAsync(_cts.Token));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                try { listener.Close(); } catch { }
                return false;
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _cts?.Cancel();
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch
            {
            }
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
            while (!token.IsCancellationRequested)
            {
                var listener = _listener;
                if (listener is not { IsListening: true }) break;

                try
                {
                    var context = await listener.GetContextAsync();
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
                }
            }
        }

        private async Task ProcessHttpRequestAsync(HttpListenerContext context, CancellationToken token)
        {
            var req = context.Request;
            var res = context.Response;
            var path = req.Url?.AbsolutePath.ToLowerInvariant() ?? "/";

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
                    res.StatusCode = 200;
                    res.ContentType = "application/json; charset=utf-8";

                    var localIp = GetLocalIpAddress();
                    var slotsList = new List<object>();
                    for (int i = 1; i <= MaxCameraSlots; i++)
                    {
                        var sName = $"Cam-{i}";
                        bool isConn = _connectedCameras.TryGetValue(sName, out var cam);
                        slotsList.Add(new
                        {
                            slot = sName,
                            slot_number = i,
                            is_connected = isConn,
                            battery = isConn ? cam!.BatteryLevel : 0,
                            is_charging = isConn && cam!.IsCharging,
                            fps = isConn ? cam!.Fps : 0,
                            bitrate_kbps = isConn ? cam!.BitrateKbps : 0,
                            tally = isConn ? cam!.CurrentTally.ToString().ToLowerInvariant() : "offline",
                            has_frame = _cameraFrames.ContainsKey(sName)
                        });
                    }

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
                        cameras_connected = _connectedCameras.Count,
                        max_cameras = MaxCameraSlots,
                        slots = slotsList
                    };

                    var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(statusData, new JsonSerializerOptions { WriteIndented = true });
                    res.ContentLength64 = jsonBytes.Length;
                    await res.OutputStream.WriteAsync(jsonBytes, token);
                }
                else if (path == "/camera/pair" || path == "/camera/config" || path == "/api/pair")
                {
                    res.StatusCode = 200;
                    res.ContentType = "application/json; charset=utf-8";

                    var localIp = GetLocalIpAddress();
                    var suggestedSlot = AllocateSlot("auto");
                    var pairData = new
                    {
                        studio_ip = localIp,
                        port = Port,
                        token = PairingToken,
                        suggested_slot = suggestedSlot,
                        max_slots = MaxCameraSlots,
                        stream_name = $"Caméra Mobile {suggestedSlot.Replace("Cam-", "")}",
                        church = ChurchName,
                        codec = "jpeg_stream",
                        bitrate_kbps = 4500,
                        fps = 30
                    };

                    var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(pairData);
                    res.ContentLength64 = jsonBytes.Length;
                    await res.OutputStream.WriteAsync(jsonBytes, token);
                }
                else if (path.StartsWith("/camera/slot/") && path.EndsWith("/frame.jpg"))
                {
                    // Ex: /camera/slot/cam-1/frame.jpg ou /camera/slot/2/frame.jpg
                    var rawSlot = path.Replace("/camera/slot/", "").Replace("/frame.jpg", "").Trim();
                    if (!rawSlot.StartsWith("cam-", StringComparison.OrdinalIgnoreCase))
                    {
                        rawSlot = "cam-" + rawSlot;
                    }
                    var matchSlot = rawSlot.ToUpperInvariant().Replace("CAM-", "Cam-");

                    res.StatusCode = 200;
                    res.ContentType = "image/jpeg";
                    res.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");

                    byte[] outBytes = _cameraFrames.TryGetValue(matchSlot, out var sf) ? sf : (LatestCameraFrameBytes ?? StaticJpegFrame);
                    res.ContentLength64 = outBytes.Length;
                    await res.OutputStream.WriteAsync(outBytes, token);
                }
                else if (path == "/camera/frame" || path == "/camera/upload")
                {
                    if (req.HttpMethod == "POST")
                    {
                        var qSlot = req.QueryString["slot"] ?? "Cam-1";
                        if (!qSlot.StartsWith("Cam-", StringComparison.OrdinalIgnoreCase)) qSlot = "Cam-" + qSlot;
                        var slotName = qSlot.ToUpperInvariant().Replace("CAM-", "Cam-");

                        using var memStream = new MemoryStream();
                        await req.InputStream.CopyToAsync(memStream, token);
                        var frameBytes = memStream.ToArray();
                        if (frameBytes.Length > 0)
                        {
                            _cameraFrames[slotName] = frameBytes;
                            LatestCameraFrameBytes = frameBytes;
                            TotalFramesReceived++;
                            OnCameraFrameReceived?.Invoke(slotName, frameBytes);
                        }
                        res.StatusCode = 200;
                        res.Close();
                        return;
                    }
                }
                else if (path == "/live/frame.jpg" || path == "/frame.jpg" || path == "/snapshot.jpg")
                {
                    res.StatusCode = 200;
                    res.ContentType = "image/jpeg";
                    res.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                    var currentBytes = ActiveProgramFrameBytes ?? LatestCameraFrameBytes ?? StaticJpegFrame;
                    res.ContentLength64 = currentBytes.Length;
                    await res.OutputStream.WriteAsync(currentBytes, token);
                }
                else if (path == "/live/stream" || path == "/stream")
                {
                    res.StatusCode = 200;
                    res.ContentType = "multipart/x-mixed-replace; boundary=--frame";

                    var boundaryBytes = Encoding.ASCII.GetBytes("\r\n--frame\r\nContent-Type: image/jpeg\r\n\r\n");

                    try
                    {
                        while (!token.IsCancellationRequested && res.OutputStream.CanWrite)
                        {
                            var frame = ActiveProgramFrameBytes ?? LatestCameraFrameBytes ?? StaticJpegFrame;
                            await res.OutputStream.WriteAsync(boundaryBytes, token);
                            await res.OutputStream.WriteAsync(frame, token);
                            await res.OutputStream.FlushAsync(token);
                            await Task.Delay(66, token);
                        }
                    }
                    catch
                    {
                    }
                }
                else
                {
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
    .live {{ background: #DC2626; color: white; }}
    .standby {{ background: #30363D; color: #8B949E; }}
    .card {{ background: #161B22; border: 1px solid #30363D; border-radius: 16px; max-width: 680px; margin: 0 auto; padding: 24px; box-shadow: 0 10px 30px rgba(0,0,0,0.5); }}
    h1 {{ font-size: 24px; margin-bottom: 6px; color: #FFFFFF; }}
    p.sub {{ color: #8B949E; font-size: 14px; margin-bottom: 20px; }}
  </style>
</head>
<body>
  <div class=""card"">
    <div class=""badge {(IsStreaming ? "live" : "standby")}"">
      {(IsStreaming ? "● DIFFUSION EN DIRECT ACTIVER" : "○ STUDIO EN ATTENTE")}
    </div>
    <h1>{ChurchName}</h1>
    <p class=""sub"">{ProgramTitle} — {Speaker}</p>
    <div style=""display:flex; justify-content:space-around; color:#8B949E; font-size:12px; margin-top:20px;"">
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
            var buffer = new byte[32768];
            CameraClient? client = null;
            using var msgStream = new MemoryStream();

            try
            {
                while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    msgStream.SetLength(0);
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                        if (result.MessageType == WebSocketMessageType.Close) break;
                        msgStream.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close) break;

                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        var frameBytes = msgStream.ToArray();
                        if (frameBytes.Length > 0)
                        {
                            var slot = client?.StreamName ?? "Cam-1";
                            _cameraFrames[slot] = frameBytes;
                            LatestCameraFrameBytes = frameBytes;
                            TotalFramesReceived++;
                            OnCameraFrameReceived?.Invoke(slot, frameBytes);
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var json = Encoding.UTF8.GetString(msgStream.GetBuffer(), 0, (int)msgStream.Length);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        var type = root.GetProperty("type").GetString();

                        if (type == "handshake")
                        {
                            var reqSlot = root.TryGetProperty("requested_slot", out var rs) ? rs.GetString() : root.TryGetProperty("stream_name", out var sn) ? sn.GetString() : "auto";
                            var assignedSlot = AllocateSlot(reqSlot);
                            var pairingToken = root.TryGetProperty("token", out var tk) ? tk.GetString() ?? "" : "";
                            var model = root.TryGetProperty("device_model", out var dm) ? dm.GetString() ?? "Smartphone" : "Smartphone";

                            client = new CameraClient
                            {
                                StreamName = assignedSlot,
                                Token = pairingToken,
                                Socket = socket,
                                DeviceModel = model,
                                SlotNumber = int.TryParse(assignedSlot.Replace("Cam-", ""), out var n) ? n : 1,
                                CurrentTally = TallyState.Standby,
                            };

                            _connectedCameras[assignedSlot] = client;
                            OnCameraConnected?.Invoke(client);

                            // Accusé de réception formel renvoyé au smartphone pour confirmer la connexion réelle
                            var ack = new
                            {
                                type = "handshake_ack",
                                status = "connected",
                                assigned_slot = assignedSlot,
                                slot_number = client.SlotNumber,
                                max_slots = MaxCameraSlots,
                                church = ChurchName,
                                tally = "standby",
                                server_time = DateTime.UtcNow.ToString("o")
                            };
                            var ackBytes = JsonSerializer.SerializeToUtf8Bytes(ack);
                            await socket.SendAsync(new ArraySegment<byte>(ackBytes), WebSocketMessageType.Text, true, token);
                        }
                        else if (type == "ping")
                        {
                            var time = root.TryGetProperty("time", out var tm) ? tm.GetInt64() : 0;
                            var pongJson = $"{{\"type\":\"pong\",\"time\":{time},\"server_time\":\"{DateTime.UtcNow:o}\"}}";
                            var pongBytes = Encoding.UTF8.GetBytes(pongJson);
                            await socket.SendAsync(new ArraySegment<byte>(pongBytes), WebSocketMessageType.Text, true, token);
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
            }
            catch
            {
            }
            finally
            {
                if (client != null)
                {
                    _connectedCameras.TryRemove(client.StreamName, out _);
                    _cameraFrames.TryRemove(client.StreamName, out _);
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
