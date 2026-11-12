using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
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
    /// Serveur WebSocket Haute Performance pour la liaison en direct avec EECC Caméra.
    /// Gère les signaux Tally sub-frame et la télémétrie matérielle sans aucune simulation.
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

        public int Port { get; }

        public CameraServer(int port = 8089)
        {
            Port = port;
        }

        public void Start()
        {
            if (_isRunning) return;

            _cts = new CancellationTokenSource();
            _listener.Prefixes.Clear();
            _listener.Prefixes.Add($"http://*:{Port}/");

            try
            {
                _listener.Start();
                _isRunning = true;
                Task.Run(() => ListenLoopAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                // Fallback local si les permissions de binding global nécessitent élévation
                _listener.Prefixes.Clear();
                _listener.Prefixes.Add($"http://localhost:{Port}/");
                _listener.Start();
                _isRunning = true;
                Task.Run(() => ListenLoopAsync(_cts.Token));
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _cts?.Cancel();
            _listener.Stop();
            _isRunning = false;
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
                        // Endpoint HTTP pour fournir les informations de couplage QR code
                        context.Response.StatusCode = 200;
                        var responseBytes = Encoding.UTF8.GetBytes("{\"service\":\"EECC Studio Server\",\"status\":\"ready\"}");
                        context.Response.ContentType = "application/json";
                        context.Response.ContentLength64 = responseBytes.Length;
                        await context.Response.OutputStream.WriteAsync(responseBytes, token);
                        context.Response.Close();
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
                        var streamName = root.GetProperty("stream_name").GetString() ?? "Cam-1";
                        var pairingToken = root.GetProperty("token").GetString() ?? "";

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

        /// <summary>
        /// Émet un signal Tally instantané à une caméra mobile connectée
        /// </summary>
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
