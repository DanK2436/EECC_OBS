using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EeccStudio.Core.Pairing;

namespace EeccStudio.Core.Network;

public enum TallyState
{
    Standby,
    Preview,
    Program
}

public sealed class CameraClient
{
    public string StreamName { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
    public WebSocket Socket { get; set; } = null!;
    public string StudioLabel { get; set; } = "EECC Studio";
    internal SemaphoreSlim SendLock { get; set; } = null!;
    public TallyState CurrentTally { get; set; } = TallyState.Standby;
    public int SlotNumber { get; set; } = 1;
    public string RemoteIp { get; set; } = string.Empty;
    public int BatteryLevel { get; set; } = -1;
    public bool IsCharging { get; set; }
    public double AudioLevel { get; set; }
    public int Fps { get; set; }
    public int BitrateKbps { get; set; }
    public DateTimeOffset ConnectedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastTelemetryTime { get; set; } = DateTimeOffset.UnixEpoch;
    internal DateTimeOffset ReservedUntil { get; set; }
}

/// <summary>
/// Écoute LAN déjà utilisée par EECC Caméra : TCP + WebSocket, images JPEG binaires,
/// télémétrie, tally et commandes. Les téléphones sont limités à 15. Les caméras physiques
/// sont un catalogue séparé et ne prennent pas ces places.
/// </summary>
public sealed class CameraServer : IDisposable
{
    public const int MaxPhoneConnections = 15;
    public const int MaxCameraSlots = MaxPhoneConnections;
    private const string WebSocketPath = "/camera/control";

    private readonly object _gate = new();
    private readonly Dictionary<string, CameraClient> _connected = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CameraClient> _reserved = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PairingTicket> _tickets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _physical = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _cameraFrames = new(StringComparer.OrdinalIgnoreCase);

    private TcpListener? _tcpListener;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public event Action<CameraClient>? OnCameraConnected;
    public event Action<string>? OnCameraDisconnected;
    public event Action<CameraClient>? OnTelemetryReceived;
    public event Action<string, byte[]>? OnCameraFrameReceived;
    public event Action<bool>? OnStreamingStateChanged;
    public event Action<bool>? OnRecordingStateChanged;

    public byte[]? LatestCameraFrameBytes { get; private set; }
    public byte[]? ActiveProgramFrameBytes { get; set; }
    public int TotalFramesReceived { get; private set; }

    public int Port { get; }
    public string StudioName { get; set; } = "EECC Studio";
    public string ChurchName { get; set; } = "Église Évangélique les Cohéritiers du Christ (EECC)";
    public string ProgramTitle { get; set; } = "Grand Culte d'Adoration et d'Enseignement";
    public string Speaker { get; set; } = "Pasteur Principal";
    public string ActiveSceneName { get; set; } = "Culte Principal";
    public TimeSpan PairingTtl { get; set; } = TimeSpan.FromMinutes(3);
    public TimeSpan ReconnectGrace { get; set; } = TimeSpan.FromSeconds(45);
    public Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.UtcNow;

    public bool IsStreaming { get; private set; }
    public bool IsRecording { get; private set; }
    public bool IsRunning => _isRunning;
    public string? StartError { get; private set; }
    public DateTimeOffset? StreamingStartedAt { get; private set; }
    public DateTimeOffset? RecordingStartedAt { get; private set; }

    public TimeSpan StreamingDuration => IsStreaming && StreamingStartedAt.HasValue
        ? Clock() - StreamingStartedAt.Value
        : TimeSpan.Zero;

    public TimeSpan RecordingDuration => IsRecording && RecordingStartedAt.HasValue
        ? Clock() - RecordingStartedAt.Value
        : TimeSpan.Zero;

    public int ListeningPort =>
        _tcpListener?.LocalEndpoint is IPEndPoint endpoint ? endpoint.Port : 0;

    public CameraServer(int port = 8089)
    {
        Port = port;
    }

    public IReadOnlyDictionary<string, CameraClient> ConnectedCameras
    {
        get
        {
            lock (_gate)
                return new Dictionary<string, CameraClient>(_connected, StringComparer.OrdinalIgnoreCase);
        }
    }

    public int ConnectedPhoneCount
    {
        get
        {
            lock (_gate)
                return _connected.Count;
        }
    }

    public int ReservedPhoneCount
    {
        get
        {
            lock (_gate)
            {
                PurgeExpiredReservations();
                return _reserved.Count;
            }
        }
    }

    public int PhoneCount
    {
        get
        {
            lock (_gate)
            {
                PurgeExpiredReservations();
                return _connected.Count + _reserved.Count;
            }
        }
    }

    public int PhysicalCameraCount
    {
        get
        {
            lock (_gate)
                return _physical.Count;
        }
    }

    public IReadOnlyList<string> ListLanAddresses() => LanAddresses.ListAssigned();

    public string GetLocalIpAddress()
    {
        var addresses = ListLanAddresses();
        return addresses.Count == 0 ? string.Empty : addresses[0];
    }

    public void RegisterPhysicalCamera(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Identifiant de caméra physique manquant.", nameof(id));
        lock (_gate)
            _physical[id.Trim()] = string.IsNullOrWhiteSpace(name) ? id.Trim() : name.Trim();
    }

    public PairingTicket IssuePairingTicket(string studioName, string lanIpv4)
    {
        var name = (studioName ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > 48)
            throw new ArgumentException("Le nom du Studio affiché dans le QR doit contenir entre 1 et 48 caractères.");
        if (!LanAddresses.IsPrivateLanIpv4(lanIpv4))
            throw new ArgumentException("Adresse LAN inutilisable. Aucune adresse de remplacement n'est inventée.");
        if (ListeningPort <= 0)
            throw new InvalidOperationException("Le serveur d'appairage n'écoute pas.");

        var token = EncodeToken(RandomNumberGenerator.GetBytes(PairingTicket.TokenByteLength));
        var ticket = new PairingTicket(token, name, lanIpv4.Trim(), ListeningPort, Clock().Add(PairingTtl));
        lock (_gate)
        {
            PurgeExpiredTickets();
            _tickets[token] = ticket;
        }

        return ticket;
    }

    public bool RevokePairingTicket(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;
        lock (_gate)
        {
            if (!_tickets.TryGetValue(token, out var ticket) || ticket.Consumed)
                return false;
            _tickets.Remove(token);
            return true;
        }
    }

    public byte[]? GetCameraFrame(string slotName)
    {
        lock (_gate)
        {
            foreach (var pair in _cameraFrames)
            {
                if (pair.Key.Equals(slotName, StringComparison.OrdinalIgnoreCase))
                    return pair.Value;
            }
        }

        return null;
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
            _tcpListener.Start();
            _isRunning = true;
            StartError = null;
            detail = string.Empty;
            Task.Run(() => AcceptLoopAsync(_cts.Token));
            return true;
        }
        catch (Exception ex)
        {
            detail = $"Le port {(Port == 0 ? "demandé" : Port.ToString())} n'a pas pu être ouvert ({ex.GetType().Name}: {ex.Message}). Un autre programme l'utilise peut-être, ou le pare-feu le bloque.";
            StartError = detail;
            _isRunning = false;
            try { _tcpListener?.Stop(); } catch { /* déjà fermé */ }
            _tcpListener = null;
            return false;
        }
    }

    public void Start() => TryStart(out _);

    public void Stop()
    {
        List<WebSocket> sockets;
        lock (_gate)
        {
            sockets = _connected.Values.Select(client => client.Socket).Where(socket => socket != null).ToList();
            _connected.Clear();
            _reserved.Clear();
            _tickets.Clear();
        }

        foreach (var socket in sockets)
        {
            try { socket.Abort(); } catch { /* fermeture déjà faite */ }
        }

        _isRunning = false;
        try { _cts?.Cancel(); } catch { /* déjà annulé */ }
        try { _tcpListener?.Stop(); } catch { /* déjà arrêté */ }
    }

    public void Dispose() => Stop();

    public void StartStreaming()
    {
        IsStreaming = true;
        StreamingStartedAt = Clock();
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
        RecordingStartedAt = Clock();
        OnRecordingStateChanged?.Invoke(true);
    }

    public void StopRecording()
    {
        IsRecording = false;
        RecordingStartedAt = null;
        OnRecordingStateChanged?.Invoke(false);
    }

    public async Task<bool> SendCommandAsync(string slot, string command, CancellationToken cancellationToken = default)
    {
        CameraClient? client;
        lock (_gate)
            _connected.TryGetValue(slot, out client);
        if (client == null || client.Socket.State != WebSocketState.Open)
            return false;

        var json = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "command",
            ["command"] = command,
        });
        await SendTextAsync(client.Socket, client.SendLock, json, cancellationToken);
        return true;
    }

    public async Task SetTallyAsync(string streamName, TallyState tally)
    {
        CameraClient? client;
        lock (_gate)
            _connected.TryGetValue(streamName, out client);
        if (client == null || client.Socket.State != WebSocketState.Open)
            return;

        client.CurrentTally = tally;
        var tallyName = tally switch
        {
            TallyState.Program => "program",
            TallyState.Preview => "preview",
            _ => "standby"
        };
        var payload = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "tally",
            ["state"] = tallyName,
        });
        try
        {
            await SendTextAsync(client.Socket, client.SendLock, payload, CancellationToken.None);
        }
        catch
        {
            /* le téléphone a coupé pendant l'envoi */
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                var tcpClient = await _tcpListener!.AcceptTcpClientAsync(cancellationToken);
                _ = ProcessConnectionAsync(tcpClient, cancellationToken);
            }
            catch when (cancellationToken.IsCancellationRequested || !_isRunning)
            {
                break;
            }
            catch
            {
                /* une acceptation ratée ne doit pas arrêter l'écoute */
            }
        }
    }

    private async Task ProcessConnectionAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        using var client = tcpClient;
        client.NoDelay = true;
        using var stream = client.GetStream();
        var remote = client.Client.RemoteEndPoint?.ToString() ?? "inconnu";
        var headerBuffer = new byte[16384];
        var totalRead = 0;

        try
        {
            var headerEnd = -1;
            while (totalRead < headerBuffer.Length && headerEnd < 0)
            {
                var read = await stream.ReadAsync(headerBuffer.AsMemory(totalRead, headerBuffer.Length - totalRead), cancellationToken);
                if (read <= 0)
                    return;
                totalRead += read;
                headerEnd = IndexOfHeaderEnd(headerBuffer, totalRead);
            }

            if (headerEnd < 0)
                return;

            var headerLength = headerEnd + 4;
            var requestText = Encoding.ASCII.GetString(headerBuffer, 0, headerLength);
            var firstLine = requestText.Split("\r\n", StringSplitOptions.None)[0];
            var parts = firstLine.Split(' ');
            if (parts.Length < 2)
                return;

            var method = parts[0].ToUpperInvariant();
            var path = parts[1];
            Stream body = stream;
            if (totalRead > headerLength)
            {
                var extra = new byte[totalRead - headerLength];
                Buffer.BlockCopy(headerBuffer, headerLength, extra, 0, extra.Length);
                body = new PrefixedStream(extra, stream);
            }

            if (requestText.Contains("Upgrade: websocket", StringComparison.OrdinalIgnoreCase))
            {
                var cleanPath = path.Split('?')[0];
                if (!cleanPath.Equals(WebSocketPath, StringComparison.OrdinalIgnoreCase))
                {
                    await WriteAsciiAsync(body, "HTTP/1.1 404 Not Found\r\nConnection: close\r\nContent-Length: 0\r\n\r\n", cancellationToken);
                    return;
                }

                await AcceptWebSocketAsync(body, requestText, remote, cancellationToken);
                return;
            }

            await HandleHttpAsync(body, method, path, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            /* arrêt du serveur */
        }
        catch
        {
            /* connexion individuelle abandonnée */
        }
    }

    private async Task AcceptWebSocketAsync(Stream stream, string requestText, string remote, CancellationToken cancellationToken)
    {
        var key = string.Empty;
        foreach (var line in requestText.Split("\r\n"))
        {
            if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
            {
                key = line["Sec-WebSocket-Key:".Length..].Trim();
                break;
            }
        }

        if (key.Length == 0)
            return;

        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        var response =
            "HTTP/1.1 101 Switching Protocols\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            $"Sec-WebSocket-Accept: {accept}\r\n\r\n";
        await WriteAsciiAsync(stream, response, cancellationToken);
        await stream.FlushAsync(cancellationToken);

        var socket = WebSocket.CreateFromStream(stream, isServer: true, subProtocol: null, keepAliveInterval: TimeSpan.FromSeconds(20));
        await RunSocketAsync(socket, remote, cancellationToken);
    }

    private async Task RunSocketAsync(WebSocket socket, string remote, CancellationToken cancellationToken)
    {
        var buffer = new byte[65536];
        var message = new MemoryStream();
        var sendLock = new SemaphoreSlim(1, 1);
        CameraClient? client = null;
        var admitted = false;
        using var handshakeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        handshakeTimeout.CancelAfter(TimeSpan.FromSeconds(8));

        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var readToken = admitted ? cancellationToken : handshakeTimeout.Token;
                message.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, readToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return;
                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    if (!admitted || client == null)
                        continue;
                    var frame = message.ToArray();
                    if (frame.Length == 0)
                        continue;
                    lock (_gate)
                    {
                        _cameraFrames[client.StreamName] = frame;
                        LatestCameraFrameBytes = frame;
                        TotalFramesReceived++;
                    }

                    OnCameraFrameReceived?.Invoke(client.StreamName, frame);
                    continue;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                    continue;

                JsonDocument document;
                try
                {
                    document = JsonDocument.Parse(message.ToArray());
                }
                catch (JsonException)
                {
                    continue;
                }

                using (document)
                {
                    var root = document.RootElement;
                    var type = ReadString(root, "type");
                    if (!admitted)
                    {
                        if (type != "handshake")
                            continue;
                        var decision = Admit(root, remote, socket, sendLock);
                        if (decision.RejectCode != null)
                        {
                            await SendTextAsync(socket, sendLock, RejectJson(decision.RejectCode, decision.RejectMessage!), cancellationToken);
                            try
                            {
                                await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, decision.RejectCode, CancellationToken.None);
                            }
                            catch { /* le client a déjà fermé */ }
                            return;
                        }

                        client = decision.Client;
                        admitted = true;
                        await SendTextAsync(socket, sendLock, decision.AckJson!, cancellationToken);
                        OnCameraConnected?.Invoke(client!);
                        continue;
                    }

                    if (type == "ping")
                    {
                        var time = ReadInt64(root, "time");
                        var pong = JsonSerializer.Serialize(new Dictionary<string, object>
                        {
                            ["type"] = "pong",
                            ["time"] = time,
                            ["server_time"] = Clock().ToString("o"),
                        });
                        await SendTextAsync(socket, sendLock, pong, cancellationToken);
                    }
                    else if (type == "telemetry" && client != null)
                    {
                        ApplyTelemetry(client, root);
                        OnTelemetryReceived?.Invoke(client);
                    }
                    else if (type == "bye" && client != null)
                    {
                        ReleasePhone(client, socket, keepReservation: false);
                        client = null;
                        try
                        {
                            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                        }
                        catch { /* fermeture déjà en cours */ }
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (!admitted && socket.State == WebSocketState.Open)
            {
                try
                {
                    await SendTextAsync(socket, sendLock, RejectJson("timeout", "Aucun appairage reçu à temps. Scannez le QR affiché dans EECC Studio."), CancellationToken.None);
                }
                catch { /* plus rien à dire au client */ }
            }
        }
        catch
        {
            /* coupure réseau */
        }
        finally
        {
            if (client != null)
                ReleasePhone(client, socket, keepReservation: true);
            try { socket.Dispose(); } catch { /* déjà libéré */ }
            sendLock.Dispose();
        }
    }

    private Admission Admit(JsonElement root, string remote, WebSocket socket, SemaphoreSlim sendLock)
    {
        var token = ReadString(root, "token") ?? string.Empty;
        var reconnect = ReadBool(root, "reconnect");
        lock (_gate)
        {
            PurgeExpiredReservations();
            if (reconnect)
                return AdmitReconnect(token, remote, socket, sendLock, root);
            return AdmitPairing(token, remote, socket, sendLock, root);
        }
    }

    private Admission AdmitReconnect(string sessionId, string remote, WebSocket socket, SemaphoreSlim sendLock, JsonElement root)
    {
        if (!_reserved.TryGetValue(sessionId, out var client))
        {
            var known = _connected.Values.Any(phone => phone.SessionId == sessionId);
            return Admission.Reject(known ? "token_used" : "session_unknown", known
                ? "Cette session est déjà reliée."
                : "Session inconnue. Scannez un nouveau QR.");
        }

        if (client.ReservedUntil <= Clock())
        {
            _reserved.Remove(sessionId);
            _cameraFrames.Remove(client.StreamName);
            return Admission.Reject("session_expired", "La session a expiré. Scannez un nouveau QR.");
        }

        _reserved.Remove(sessionId);
        AttachSocket(client, remote, socket, sendLock, root);
        _connected[client.StreamName] = client;
        return Admission.Ok(client, AckJson(client));
    }

    private Admission AdmitPairing(string token, string remote, WebSocket socket, SemaphoreSlim sendLock, JsonElement root)
    {
        if (!_tickets.TryGetValue(token, out var ticket))
            return Admission.Reject("token_invalid", "Jeton refusé. Scannez le QR affiché maintenant dans EECC Studio.");
        if (ticket.ExpiresAt <= Clock())
            return Admission.Reject("token_expired", "Jeton expiré. Renouvelez le QR dans EECC Studio.");
        if (ticket.Consumed)
            return Admission.Reject("token_used", "Ce QR a déjà servi. Affichez un nouveau QR pour ce téléphone.");
        if (_connected.Count + _reserved.Count >= MaxPhoneConnections)
            return Admission.Reject("capacity", "15 téléphones sont déjà reliés. Déconnectez-en un avant d'en ajouter. Les caméras physiques ne libèrent pas ces places.");

        var slot = AllocateSlot(ReadString(root, "requested_slot") ?? ReadString(root, "stream_name"));
        if (slot == null)
            return Admission.Reject("capacity", "15 téléphones sont déjà reliés. Déconnectez-en un avant d'en ajouter. Les caméras physiques ne libèrent pas ces places.");

        ticket.Consumed = true;
        var client = new CameraClient
        {
            StreamName = slot,
            SessionId = EncodeToken(RandomNumberGenerator.GetBytes(16)),
            SlotNumber = int.Parse(slot["Cam-".Length..], System.Globalization.CultureInfo.InvariantCulture),
            StudioLabel = ticket.StudioName,
            ConnectedAt = Clock(),
        };
        AttachSocket(client, remote, socket, sendLock, root);
        _connected[slot] = client;
        return Admission.Ok(client, AckJson(client));
    }

    private void AttachSocket(CameraClient client, string remote, WebSocket socket, SemaphoreSlim sendLock, JsonElement root)
    {
        client.Socket = socket;
        client.SendLock = sendLock;
        client.RemoteIp = remote;
        client.DeviceModel = TrimLabel(ReadString(root, "device_model"), "non indiqué");
        client.DeviceId = TrimLabel(ReadString(root, "device_id"), string.Empty);
        client.DisplayName = TrimLabel(ReadString(root, "display_name"), $"Téléphone {client.SlotNumber}");
        client.ConnectedAt = Clock();
        client.ReservedUntil = DateTimeOffset.MinValue;
    }

    private static string AckJson(CameraClient client)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "handshake_ack",
            ["status"] = "connected",
            ["assigned_slot"] = client.StreamName,
            ["slot_number"] = client.SlotNumber,
            ["session_id"] = client.SessionId,
            ["display_name"] = client.DisplayName,
            ["max_phones"] = MaxPhoneConnections,
            ["studio_name"] = client.StudioLabel,
            ["tally"] = "standby",
        });
    }

    private static string RejectJson(string code, string message)
    {
        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "handshake_reject",
            ["status"] = "rejected",
            ["code"] = code,
            ["message"] = message,
        });
    }

    private string? AllocateSlot(string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var clean = requested.Trim();
            if (clean.StartsWith("Cam-", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(clean["Cam-".Length..], out var number)
                && number is >= 1 and <= MaxPhoneConnections
                && !_connected.ContainsKey($"Cam-{number}")
                && !_reserved.Values.Any(phone => phone.StreamName.Equals($"Cam-{number}", StringComparison.OrdinalIgnoreCase)))
            {
                return $"Cam-{number}";
            }
        }

        for (var number = 1; number <= MaxPhoneConnections; number++)
        {
            var slot = $"Cam-{number}";
            if (_connected.ContainsKey(slot))
                continue;
            if (_reserved.Values.Any(phone => phone.StreamName.Equals(slot, StringComparison.OrdinalIgnoreCase)))
                continue;
            return slot;
        }

        return null;
    }

    private void ReleasePhone(CameraClient client, WebSocket socket, bool keepReservation)
    {
        var removed = false;
        lock (_gate)
        {
            if (!ReferenceEquals(client.Socket, socket))
                return;
            if (_connected.TryGetValue(client.StreamName, out var current) && ReferenceEquals(current, client))
            {
                _connected.Remove(client.StreamName);
                removed = true;
                if (keepReservation)
                {
                    client.ReservedUntil = Clock().Add(ReconnectGrace);
                    _reserved[client.SessionId] = client;
                }
                else
                {
                    _reserved.Remove(client.SessionId);
                    _cameraFrames.Remove(client.StreamName);
                }
            }
        }

        if (removed)
            OnCameraDisconnected?.Invoke(client.StreamName);
    }

    private void PurgeExpiredReservations()
    {
        var now = Clock();
        var expired = _reserved.Where(pair => pair.Value.ReservedUntil <= now).Select(pair => pair.Key).ToList();
        foreach (var sessionId in expired)
        {
            if (_reserved.Remove(sessionId, out var client))
                _cameraFrames.Remove(client.StreamName);
        }
    }

    private void PurgeExpiredTickets()
    {
        var now = Clock();
        var expired = _tickets.Where(pair => !pair.Value.Consumed && pair.Value.ExpiresAt <= now).Select(pair => pair.Key).ToList();
        foreach (var token in expired)
            _tickets.Remove(token);
    }

    private void ApplyTelemetry(CameraClient client, JsonElement root)
    {
        if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
            return;
        var hasBattery = TryReadInt(payload, out var battery, "batteryLevel", "battery_level");
        var hasCharging = TryReadBool(payload, out var charging, "isCharging", "is_charging");
        var hasFps = TryReadInt(payload, out var fps, "fps");
        var hasBitrate = TryReadInt(payload, out var bitrate, "currentBitrateKbps", "bitrate_kbps");
        var hasAudio = TryReadDouble(payload, out var audio, "audioLevel", "audio_level", "audioLevelNormalized");
        lock (_gate)
        {
            if (hasBattery)
                client.BatteryLevel = battery;
            if (hasCharging)
                client.IsCharging = charging;
            if (hasFps)
                client.Fps = fps;
            if (hasBitrate)
                client.BitrateKbps = bitrate;
            if (hasAudio)
                client.AudioLevel = Math.Clamp(audio, 0, 1);
            client.LastTelemetryTime = Clock();
        }
    }

    private async Task HandleHttpAsync(Stream stream, string method, string path, CancellationToken cancellationToken)
    {
        var cleanPath = path.Split('?')[0].ToLowerInvariant();
        if (method == "OPTIONS")
        {
            await WriteAsciiAsync(stream, "HTTP/1.1 200 OK\r\nAccess-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, POST, OPTIONS\r\nAccess-Control-Allow-Headers: *\r\nContent-Length: 0\r\n\r\n", cancellationToken);
            return;
        }

        if (cleanPath is "/live/status" or "/status" or "/api/status")
        {
            var lan = GetLocalIpAddress();
            var phones = new List<object>();
            List<CameraClient> connected;
            int reserved;
            int physical;
            lock (_gate)
            {
                PurgeExpiredReservations();
                connected = _connected.Values.OrderBy(phone => phone.SlotNumber).ToList();
                reserved = _reserved.Count;
                physical = _physical.Count;
            }

            foreach (var phone in connected)
            {
                phones.Add(new Dictionary<string, object?>
                {
                    ["slot"] = phone.StreamName,
                    ["slot_number"] = phone.SlotNumber,
                    ["display_name"] = phone.DisplayName,
                    ["device_model"] = phone.DeviceModel,
                    ["remote"] = phone.RemoteIp,
                    ["battery"] = phone.BatteryLevel < 0 ? null : phone.BatteryLevel,
                    ["is_charging"] = phone.IsCharging,
                    ["audio_level"] = phone.AudioLevel,
                    ["fps"] = phone.Fps > 0 ? phone.Fps : null,
                    ["bitrate_kbps"] = phone.BitrateKbps > 0 ? phone.BitrateKbps : null,
                    ["tally"] = phone.CurrentTally.ToString().ToLowerInvariant(),
                    ["has_frame"] = GetCameraFrame(phone.StreamName) != null,
                });
            }

            var status = new Dictionary<string, object?>
            {
                ["service"] = "EECC Studio",
                ["church"] = ChurchName,
                ["is_live"] = IsStreaming,
                ["status"] = IsStreaming ? "live" : "standby",
                ["title"] = ProgramTitle,
                ["speaker"] = Speaker,
                ["active_scene"] = ActiveSceneName,
                ["live_stream_url"] = $"http://{(lan.Length == 0 ? "127.0.0.1" : lan)}:{ListeningPort}/live/stream",
                ["lan_addresses"] = ListLanAddresses(),
                ["lan_error"] = lan.Length == 0 ? "Aucune adresse IPv4 privée utilisable. Le QR n'est pas affiché." : null,
                ["phones_connected"] = connected.Count,
                ["phones_reconnecting"] = reserved,
                ["max_phones"] = MaxPhoneConnections,
                ["physical_cameras"] = physical,
                ["physical_cameras_count_toward_limit"] = false,
                ["viewers"] = null,
                ["resolution"] = null,
                ["fps"] = null,
                ["bitrate_kbps"] = null,
                ["phones"] = phones,
            };
            await WriteJsonAsync(stream, status, cancellationToken);
            return;
        }

        if (cleanPath is "/live/frame.jpg" or "/frame.jpg")
        {
            var frame = IsStreaming ? ActiveProgramFrameBytes : null;
            if (frame == null)
            {
                await WriteJsonAsync(stream, new Dictionary<string, string>
                {
                    ["error"] = "aucune_image",
                    ["message"] = "Aucune image de téléphone n'est à l'antenne.",
                }, cancellationToken, statusCode: 404);
                return;
            }

            await WriteBytesAsync(stream, "image/jpeg", frame, cancellationToken);
            return;
        }

        if (cleanPath.StartsWith("/camera/slot/", StringComparison.Ordinal) && cleanPath.EndsWith("/frame.jpg", StringComparison.Ordinal))
        {
            var segments = cleanPath.Split('/');
            var slot = segments.Length >= 4 ? segments[3] : string.Empty;
            var frame = GetCameraFrame(slot);
            if (frame == null)
            {
                await WriteJsonAsync(stream, new Dictionary<string, string>
                {
                    ["error"] = "aucune_image",
                    ["message"] = $"Aucune image reçue pour {slot}.",
                }, cancellationToken, statusCode: 404);
                return;
            }

            await WriteBytesAsync(stream, "image/jpeg", frame, cancellationToken);
            return;
        }

        if (cleanPath == "/live/stream")
        {
            if (!IsStreaming)
            {
                await WriteJsonAsync(stream, new Dictionary<string, string>
                {
                    ["error"] = "hors_antenne",
                    ["message"] = "EECC TV n'est pas en direct.",
                }, cancellationToken, statusCode: 404);
                return;
            }

            await WriteAsciiAsync(stream, "HTTP/1.1 200 OK\r\nContent-Type: multipart/x-mixed-replace; boundary=--eecc_boundary_frame\r\nAccess-Control-Allow-Origin: *\r\nCache-Control: no-cache, no-store\r\n\r\n", cancellationToken);
            while (!cancellationToken.IsCancellationRequested && _isRunning && IsStreaming)
            {
                var frame = ActiveProgramFrameBytes;
                if (frame != null)
                {
                    var part = $"--eecc_boundary_frame\r\nContent-Type: image/jpeg\r\nContent-Length: {frame.Length}\r\n\r\n";
                    await WriteAsciiAsync(stream, part, cancellationToken);
                    await stream.WriteAsync(frame, cancellationToken);
                    await WriteAsciiAsync(stream, "\r\n", cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }

                await Task.Delay(frame == null ? 200 : 66, cancellationToken);
            }

            return;
        }

        var lanAddress = GetLocalIpAddress();
        var html = "<!DOCTYPE html><html lang=\"fr\"><head><meta charset=\"utf-8\"><title>EECC Studio</title></head><body><h1>EECC Studio</h1><p>"
            + (lanAddress.Length == 0
                ? "Aucune adresse IPv4 privée utilisable n'a été détectée."
                : "Écoute LAN sur " + System.Net.WebUtility.HtmlEncode(lanAddress) + ":" + ListeningPort + ".")
            + " Le même réseau Wi-Fi ne garantit pas que les téléphones puissent joindre ce port.</p></body></html>";
        await WriteBytesAsync(stream, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(html), cancellationToken);
    }

    private static async Task SendTextAsync(WebSocket socket, SemaphoreSlim gate, string json, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (socket.State == WebSocketState.Open)
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task WriteJsonAsync(Stream stream, object payload, CancellationToken cancellationToken, int statusCode = 200)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(payload);
        var header = $"HTTP/1.1 {statusCode} {(statusCode == 200 ? "OK" : "Not Found")}\r\nContent-Type: application/json; charset=utf-8\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
        await WriteAsciiAsync(stream, header, cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
    }

    private static async Task WriteBytesAsync(Stream stream, string contentType, byte[] body, CancellationToken cancellationToken)
    {
        var header = $"HTTP/1.1 200 OK\r\nContent-Type: {contentType}\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
        await WriteAsciiAsync(stream, header, cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
    }

    private static Task WriteAsciiAsync(Stream stream, string text, CancellationToken cancellationToken) =>
        stream.WriteAsync(Encoding.ASCII.GetBytes(text), cancellationToken).AsTask();

    private static int IndexOfHeaderEnd(byte[] buffer, int length)
    {
        for (var i = 0; i <= length - 4; i++)
        {
            if (buffer[i] == 13 && buffer[i + 1] == 10 && buffer[i + 2] == 13 && buffer[i + 3] == 10)
                return i;
        }

        return -1;
    }

    private static string EncodeToken(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string TrimLabel(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        var cleaned = new string(value.Where(character => !char.IsControl(character)).ToArray()).Trim();
        if (cleaned.Length == 0)
            return fallback;
        return cleaned.Length <= 40 ? cleaned : cleaned[..40];
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
            return null;
        return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    }

    private static bool ReadBool(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
            return false;
        return property.ValueKind == JsonValueKind.True
            || (property.ValueKind == JsonValueKind.String && property.GetString() == "true");
    }

    private static long ReadInt64(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Number)
            return 0;
        return property.TryGetInt64(out var value) ? value : 0;
    }

    private static bool TryReadInt(JsonElement element, out int value, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Number)
                continue;
            if (property.TryGetInt32(out value))
                return true;
            if (property.TryGetDouble(out var number))
            {
                value = (int)number;
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool TryReadBool(JsonElement element, out bool value, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property))
                continue;
            if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                value = property.GetBoolean();
                return true;
            }
        }

        value = false;
        return false;
    }

    private static bool TryReadDouble(JsonElement element, out double value, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Number)
                continue;
            if (property.TryGetDouble(out value))
                return true;
        }

        value = 0;
        return false;
    }

    private readonly struct Admission
    {
        public CameraClient? Client { get; }
        public string? AckJson { get; }
        public string? RejectCode { get; }
        public string? RejectMessage { get; }

        private Admission(CameraClient? client, string? ackJson, string? rejectCode, string? rejectMessage)
        {
            Client = client;
            AckJson = ackJson;
            RejectCode = rejectCode;
            RejectMessage = rejectMessage;
        }

        public static Admission Ok(CameraClient client, string ackJson) => new(client, ackJson, null, null);
        public static Admission Reject(string code, string message) => new(null, null, code, message);
    }

    private sealed class PrefixedStream : Stream
    {
        private readonly byte[] _prefix;
        private int _offset;
        private readonly Stream _inner;

        public PrefixedStream(byte[] prefix, Stream inner)
        {
            _prefix = prefix;
            _inner = inner;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count)
        {
            var fromPrefix = ReadPrefix(buffer.AsSpan(offset, count));
            return fromPrefix > 0 ? fromPrefix : _inner.Read(buffer, offset, count);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var fromPrefix = ReadPrefix(buffer.Span);
            if (fromPrefix > 0)
                return fromPrefix;
            return await _inner.ReadAsync(buffer, cancellationToken);
        }

        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.WriteAsync(buffer, offset, count, cancellationToken);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.WriteAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        private int ReadPrefix(Span<byte> buffer)
        {
            var left = _prefix.Length - _offset;
            if (left <= 0 || buffer.Length == 0)
                return 0;
            var count = Math.Min(left, buffer.Length);
            _prefix.AsSpan(_offset, count).CopyTo(buffer);
            _offset += count;
            return count;
        }
    }
}
