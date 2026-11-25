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
    /// <summary>
    /// Serveur API WebSocket compatible OBS Studio (OBS WebSocket Protocol v5.x / Port 4455) :
    /// Permet le contrôle à distance intégral de la régie EECC Studio depuis un boîtier
    /// Elgato Stream Deck (avec plugin OBS Studio officiel), Bitfocus Companion ou applications mobiles.
    /// </summary>
    public sealed class ObsWebSocketServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
        private CancellationTokenSource? _cts;
        private Task? _listenerTask;

        public int Port { get; }
        public bool IsRunning => _listener.IsListening;

        // Événements pour piloter la régie Studio
        public Func<string>? OnGetCurrentProgramScene { get; set; }
        public Func<string>? OnGetCurrentPreviewScene { get; set; }
        public Action<string>? OnSetCurrentProgramScene { get; set; }
        public Action<string>? OnSetCurrentPreviewScene { get; set; }
        public Action? OnTriggerTransition { get; set; }
        public Func<bool>? OnGetStudioMode { get; set; }
        public Action<bool>? OnSetStudioMode { get; set; }
        public Func<bool>? OnGetStreamStatus { get; set; }
        public Action? OnToggleStream { get; set; }
        public Func<bool>? OnGetRecordStatus { get; set; }
        public Action? OnToggleRecord { get; set; }
        public Action? OnSaveReplay { get; set; }

        // Événements pour piloter les Overlays Graphiques (Textes, Titrages, Ticker, Diaporamas)
        public Action<bool, string?, string?>? OnToggleLowerThird { get; set; }
        public Action<bool, string?>? OnToggleTicker { get; set; }
        public Action? OnSlideShowNext { get; set; }
        public Action? OnSlideShowPrevious { get; set; }
        public Action<string, int>? OnSetTransition { get; set; }

        public ObsWebSocketServer(int port = 4455)
        {
            Port = port;
        }

        public void Start()
        {
            if (_listener.IsListening) return;

            try
            {
                _listener.Prefixes.Clear();
                _listener.Prefixes.Add($"http://*:{Port}/");
                _listener.Start();
            }
            catch
            {
                // Fallback localhost si écoute globale restreinte par l'OS
                _listener.Prefixes.Clear();
                _listener.Prefixes.Add($"http://localhost:{Port}/");
                _listener.Start();
            }

            _cts = new CancellationTokenSource();
            _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token));
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                    if (ctx.Request.IsWebSocketRequest)
                    {
                        _ = ProcessWebSocketConnectionAsync(ctx, token);
                    }
                    else
                    {
                        ctx.Response.StatusCode = 200;
                        using var writer = new StreamWriter(ctx.Response.OutputStream);
                        await writer.WriteAsync("EECC Studio OBS WebSocket Server v5.0 Active");
                        ctx.Response.Close();
                    }
                }
                catch (HttpListenerException) { break; }
                catch (Exception) { }
            }
        }

        private async Task ProcessWebSocketConnectionAsync(HttpListenerContext ctx, CancellationToken token)
        {
            WebSocketContext wsCtx;
            try
            {
                wsCtx = await ctx.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
            }
            catch { return; }

            var ws = wsCtx.WebSocket;
            var clientId = Guid.NewGuid().ToString("N");
            _clients[clientId] = ws;

            try
            {
                // Envoi de l'OpCode 0 : Hello (protocole OBS WebSocket v5)
                var helloMsg = new
                {
                    op = 0,
                    d = new
                    {
                        obsWebSocketVersion = "5.3.0",
                        rpcVersion = 1,
                        authentication = (object?)null // Pas d'auth requise en LAN par défaut
                    }
                };
                await SendJsonAsync(ws, helloMsg, token).ConfigureAwait(false);

                var buffer = new byte[8192];
                while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await HandleMessageAsync(ws, json, token).ConfigureAwait(false);
                    }
                }
            }
            catch { }
            finally
            {
                _clients.TryRemove(clientId, out _);
                try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None); } catch { }
                ws.Dispose();
            }
        }

        private async Task HandleMessageAsync(WebSocket ws, string json, CancellationToken token)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("op", out var opProp)) return;

                int op = opProp.GetInt32();

                // OpCode 1 : Identify
                if (op == 1)
                {
                    var identified = new
                    {
                        op = 2,
                        d = new { negotiatedRpcVersion = 1 }
                    };
                    await SendJsonAsync(ws, identified, token).ConfigureAwait(false);
                    return;
                }

                // OpCode 6 : Request
                if (op == 6)
                {
                    if (!root.TryGetProperty("d", out var dProp)) return;

                    string reqType = dProp.GetProperty("requestType").GetString() ?? "";
                    string reqId = dProp.GetProperty("requestId").GetString() ?? "";

                    object? responseData = null;

                    switch (reqType)
                    {
                        case "GetVersion":
                            responseData = new
                            {
                                obsVersion = "30.0.0 (EECC Studio Native)",
                                obsWebSocketVersion = "5.3.0",
                                rpcVersion = 1,
                                platform = "windows",
                                platformDescription = "Windows 11 / EECC OBS Ecosystem"
                            };
                            break;

                        case "GetStudioModeEnabled":
                            responseData = new { studioModeEnabled = OnGetStudioMode?.Invoke() ?? true };
                            break;

                        case "SetStudioModeEnabled":
                            if (dProp.TryGetProperty("requestData", out var reqData) && reqData.TryGetProperty("studioModeEnabled", out var smProp))
                            {
                                OnSetStudioMode?.Invoke(smProp.GetBoolean());
                            }
                            break;

                        case "GetCurrentProgramScene":
                            responseData = new { currentProgramSceneName = OnGetCurrentProgramScene?.Invoke() ?? "Culte Principal" };
                            break;

                        case "GetCurrentPreviewScene":
                            responseData = new { currentPreviewSceneName = OnGetCurrentPreviewScene?.Invoke() ?? "Prédication" };
                            break;

                        case "SetCurrentProgramScene":
                            if (dProp.TryGetProperty("requestData", out var sData) && sData.TryGetProperty("sceneName", out var scProp))
                            {
                                OnSetCurrentProgramScene?.Invoke(scProp.GetString() ?? "");
                            }
                            break;

                        case "TriggerStudioModeTransition":
                            OnTriggerTransition?.Invoke();
                            break;

                        case "GetStreamStatus":
                            responseData = new { outputActive = OnGetStreamStatus?.Invoke() ?? false };
                            break;

                        case "ToggleStream":
                            OnToggleStream?.Invoke();
                            responseData = new { outputActive = OnGetStreamStatus?.Invoke() ?? false };
                            break;

                        case "GetRecordStatus":
                            responseData = new { outputActive = OnGetRecordStatus?.Invoke() ?? false };
                            break;

                        case "ToggleRecord":
                            OnToggleRecord?.Invoke();
                            responseData = new { outputActive = OnGetRecordStatus?.Invoke() ?? false };
                            break;

                        case "SaveReplayBuffer":
                            OnSaveReplay?.Invoke();
                            break;

                        case "ToggleLowerThird":
                        case "EECC_ToggleLowerThird":
                            if (dProp.TryGetProperty("requestData", out var ltData))
                            {
                                bool visible = ltData.TryGetProperty("visible", out var vProp) && vProp.GetBoolean();
                                string? speaker = ltData.TryGetProperty("speaker", out var spProp) ? spProp.GetString() : null;
                                string? title = ltData.TryGetProperty("title", out var titProp) ? titProp.GetString() : null;
                                OnToggleLowerThird?.Invoke(visible, speaker, title);
                            }
                            else
                            {
                                OnToggleLowerThird?.Invoke(true, null, null);
                            }
                            break;

                        case "ToggleTicker":
                        case "EECC_ToggleTicker":
                            if (dProp.TryGetProperty("requestData", out var tkData))
                            {
                                bool enabled = tkData.TryGetProperty("enabled", out var enProp) && enProp.GetBoolean();
                                string? text = tkData.TryGetProperty("text", out var txProp) ? txProp.GetString() : null;
                                OnToggleTicker?.Invoke(enabled, text);
                            }
                            else
                            {
                                OnToggleTicker?.Invoke(true, null);
                            }
                            break;

                        case "SlideShowNext":
                        case "EECC_SlideShowNext":
                            OnSlideShowNext?.Invoke();
                            break;

                        case "SlideShowPrevious":
                        case "EECC_SlideShowPrevious":
                            OnSlideShowPrevious?.Invoke();
                            break;

                        case "SetCurrentSceneTransition":
                            if (dProp.TryGetProperty("requestData", out var trData) && trData.TryGetProperty("transitionName", out var tnProp))
                            {
                                string tName = tnProp.GetString() ?? "Fade";
                                int duration = trData.TryGetProperty("transitionDuration", out var tdProp) ? tdProp.GetInt32() : 300;
                                OnSetTransition?.Invoke(tName, duration);
                            }
                            break;
                    }

                    // Envoi de l'OpCode 7 : RequestResponse
                    var resp = new
                    {
                        op = 7,
                        d = new
                        {
                            requestType = reqType,
                            requestId = reqId,
                            requestStatus = new { result = true, code = 100 },
                            responseData = responseData
                        }
                    };
                    await SendJsonAsync(ws, resp, token).ConfigureAwait(false);
                }
            }
            catch { }
        }

        private static async Task SendJsonAsync(WebSocket ws, object obj, CancellationToken token)
        {
            if (ws.State != WebSocketState.Open) return;
            string json = JsonSerializer.Serialize(obj);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, token).ConfigureAwait(false);
        }

        public void Stop()
        {
            _cts?.Cancel();
            try { _listener.Stop(); } catch { }
            foreach (var ws in _clients.Values)
            {
                try { ws.Dispose(); } catch { }
            }
            _clients.Clear();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
