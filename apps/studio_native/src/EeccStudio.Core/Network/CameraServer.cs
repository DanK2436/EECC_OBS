using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
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
    /// Basé sur TcpListener brut (0.0.0.0) + WebSocket RFC 6455 natif pour garantir un fonctionnement à 100%
    /// sur le réseau Wi-Fi local sans dépendance aux permissions administrateur Windows (urlacl).
    /// </summary>
    public class CameraServer
    {
        public const int MaxCameraSlots = 15;
        private TcpListener? _tcpListener;
        private readonly ConcurrentDictionary<string, CameraClient> _connectedCameras = new();
        private readonly ConcurrentDictionary<string, byte[]> _cameraFrames = new();
        private CancellationTokenSource? _cts;
        private bool _isRunning;

        // Image JPEG de secours
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
        public string ActiveSceneName { get; set; } = "Culte Principal";

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

        public CameraServer(int port = 8089)
        {
            Port = port;
        }

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

        /// <summary>
        /// Détecte l'adresse IP locale Wi-Fi ou Ethernet du PC en éliminant les adaptateurs virtuels (WSL, Docker, VirtualBox).
        /// </summary>
        public string GetLocalIpAddress()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                                  nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                  !nic.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                                  !nic.Description.Contains("VMware", StringComparison.OrdinalIgnoreCase) &&
                                  !nic.Description.Contains("WSL", StringComparison.OrdinalIgnoreCase) &&
                                  !nic.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) &&
                                  !nic.Description.Contains("TAP", StringComparison.OrdinalIgnoreCase) &&
                                  !nic.Name.Contains("vEthernet", StringComparison.OrdinalIgnoreCase) &&
                                  !nic.Name.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase) &&
                                  !nic.Name.Contains("Docker", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 2 :
                                              nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 1 : 0);

                foreach (var netInterface in interfaces)
                {
                    var ipProps = netInterface.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr.Address))
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

            try
            {
                _cts = new CancellationTokenSource();
                _tcpListener = new TcpListener(IPAddress.Any, Port);
                _tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _tcpListener.Start();
                _isRunning = true;
                detail = string.Empty;

                Task.Run(() => AcceptTcpClientsLoopAsync(_cts.Token));
                return true;
            }
            catch (Exception ex)
            {
                detail = ex.GetType().Name + ": " + ex.Message;
                _isRunning = false;
                return false;
            }
        }

        public void Start()
        {
            TryStart(out _);
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _cts?.Cancel();
            try
            {
                _tcpListener?.Stop();
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

        private async Task AcceptTcpClientsLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _isRunning)
            {
                try
                {
                    var tcpClient = await _tcpListener!.AcceptTcpClientAsync(token);
                    _ = ProcessIncomingConnectionAsync(tcpClient, token);
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

        private async Task ProcessIncomingConnectionAsync(TcpClient tcpClient, CancellationToken token)
        {
            using var client = tcpClient;
            client.NoDelay = true;
            using var stream = client.GetStream();

            var remoteEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "inconnu";
            var headerBuffer = new byte[8192];
            int totalRead = 0;
            string requestText = string.Empty;

            try
            {
                // Lecture de l'en-tête HTTP
                while (totalRead < headerBuffer.Length)
                {
                    int read = await stream.ReadAsync(headerBuffer.AsMemory(totalRead, headerBuffer.Length - totalRead), token);
                    if (read <= 0) return;
                    totalRead += read;
                    requestText = Encoding.UTF8.GetString(headerBuffer, 0, totalRead);
                    if (requestText.Contains("\r\n\r\n")) break;
                }

                if (string.IsNullOrEmpty(requestText)) return;

                var firstLine = requestText.Split(new[] { "\r\n" }, StringSplitOptions.None)[0];
                var parts = firstLine.Split(' ');
                if (parts.Length < 2) return;

                var method = parts[0].ToUpperInvariant();
                var path = parts[1];

                // 1. DÉTECTION DU WEBSOCKET (EECC Caméra)
                if (requestText.IndexOf("Upgrade: websocket", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    await HandleWebSocketUpgradeAsync(stream, requestText, remoteEndpoint, token);
                    return;
                }

                // 2. GESTION DES REQUÊTES HTTP (EECC TV & Web)
                await HandleHttpRequestAsync(stream, method, path, requestText, totalRead, headerBuffer, token);
            }
            catch
            {
            }
        }

        private async Task HandleWebSocketUpgradeAsync(NetworkStream stream, string requestText, string remoteEndpoint, CancellationToken token)
        {
            string secWebSocketKey = string.Empty;
            foreach (var line in requestText.Split(new[] { "\r\n" }, StringSplitOptions.None))
            {
                if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                {
                    secWebSocketKey = line.Substring("Sec-WebSocket-Key:".Length).Trim();
                    break;
                }
            }

            if (string.IsNullOrEmpty(secWebSocketKey)) return;

            // Calcul du hash RFC 6455
            string acceptKey;
            using (var sha1 = SHA1.Create())
            {
                var combined = secWebSocketKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(combined));
                acceptKey = Convert.ToBase64String(hash);
            }

            var responseHeaders =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n";

            var responseBytes = Encoding.UTF8.GetBytes(responseHeaders);
            await stream.WriteAsync(responseBytes, token);
            await stream.FlushAsync(token);

            // Création de l'objet WebSocket .NET natif à partir du flux TCP
            var ws = WebSocket.CreateFromStream(stream, isServer: true, subProtocol: null, keepAliveInterval: TimeSpan.FromSeconds(30));
            await ProcessWebSocketSessionAsync(ws, remoteEndpoint, token);
        }

        private async Task ProcessWebSocketSessionAsync(WebSocket socket, string remoteEndpoint, CancellationToken token)
        {
            var buffer = new byte[65536];
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
                                RemoteIp = remoteEndpoint,
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
                try { socket.Dispose(); } catch { }
            }
        }

        private async Task HandleHttpRequestAsync(NetworkStream stream, string method, string path, string requestText, int totalHeaderRead, byte[] headerBuffer, CancellationToken token)
        {
            var cleanPath = path.Split('?')[0].ToLowerInvariant();

            if (method == "OPTIONS")
            {
                var optResponse = "HTTP/1.1 200 OK\r\nAccess-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, POST, OPTIONS\r\nAccess-Control-Allow-Headers: *\r\nContent-Length: 0\r\n\r\n";
                await stream.WriteAsync(Encoding.UTF8.GetBytes(optResponse), token);
                return;
            }

            if (cleanPath == "/live/status" || cleanPath == "/status" || cleanPath == "/api/status")
            {
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

                var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(statusData);
                var header = $"HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: {jsonBytes.Length}\r\n\r\n";
                await stream.WriteAsync(Encoding.UTF8.GetBytes(header), token);
                await stream.WriteAsync(jsonBytes, token);
                return;
            }

            if (cleanPath == "/live/frame.jpg" || cleanPath == "/frame.jpg")
            {
                var frame = ActiveProgramFrameBytes ?? LatestCameraFrameBytes ?? StaticJpegFrame;
                var header = $"HTTP/1.1 200 OK\r\nContent-Type: image/jpeg\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: {frame.Length}\r\n\r\n";
                await stream.WriteAsync(Encoding.UTF8.GetBytes(header), token);
                await stream.WriteAsync(frame, token);
                return;
            }

            if (cleanPath.StartsWith("/camera/slot/") && cleanPath.EndsWith("/frame.jpg"))
            {
                var segments = cleanPath.Split('/');
                var slot = segments.Length >= 4 ? segments[3] : "cam-1";
                var frame = GetCameraFrame(slot) ?? StaticJpegFrame;
                var header = $"HTTP/1.1 200 OK\r\nContent-Type: image/jpeg\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: {frame.Length}\r\n\r\n";
                await stream.WriteAsync(Encoding.UTF8.GetBytes(header), token);
                await stream.WriteAsync(frame, token);
                return;
            }

            if (cleanPath == "/live/stream")
            {
                var boundary = "eecc_boundary_frame";
                var mjpegHeader = $"HTTP/1.1 200 OK\r\nContent-Type: multipart/x-mixed-replace; boundary=--{boundary}\r\nAccess-Control-Allow-Origin: *\r\nCache-Control: no-cache, no-store, must-revalidate\r\n\r\n";
                await stream.WriteAsync(Encoding.UTF8.GetBytes(mjpegHeader), token);

                while (!token.IsCancellationRequested && _isRunning)
                {
                    var frame = ActiveProgramFrameBytes ?? LatestCameraFrameBytes ?? StaticJpegFrame;
                    var partHeader = $"--{boundary}\r\nContent-Type: image/jpeg\r\nContent-Length: {frame.Length}\r\n\r\n";
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(partHeader), token);
                    await stream.WriteAsync(frame, token);
                    await stream.WriteAsync(Encoding.UTF8.GetBytes("\r\n"), token);
                    await stream.FlushAsync(token);
                    await Task.Delay(66, token); // ~15 FPS
                }
                return;
            }

            // Page HTML par défaut
            var localIpDefault = GetLocalIpAddress();
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
      {(IsStreaming ? "● DIFFUSION EN DIRECT ACTIVÉE" : "○ STUDIO EN ATTENTE DU CULTE")}
    </div>
    <h1>{ChurchName}</h1>
    <p class=""sub"">{ProgramTitle} — {Speaker}</p>
    <div style=""display:flex; justify-content:space-around; color:#8B949E; font-size:12px; margin-top:20px;"">
      <div>Scène active : <strong style=""color:#F0F6FC;"">{ActiveSceneName}</strong></div>
      <div>Résolution : <strong style=""color:#16A34A;"">1080p60</strong></div>
      <div>Serveur : <strong style=""color:#38BDF8;"">http://{localIpDefault}:{Port}</strong></div>
    </div>
  </div>
</body>
</html>";
            var htmlBytes = Encoding.UTF8.GetBytes(html);
            var defaultResp = $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: {htmlBytes.Length}\r\n\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(defaultResp), token);
            await stream.WriteAsync(htmlBytes, token);
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
