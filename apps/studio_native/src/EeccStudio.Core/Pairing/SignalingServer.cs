using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace EeccStudio.Core.Pairing;

public sealed class SignalingServer : IAsyncDisposable
{
    private readonly Func<string, string?> _authenticate;
    private readonly Func<JsonElement, Task> _onMessage;
    private readonly CancellationTokenSource _cts = new();
    private TcpListener? _listener;
    private Task? _acceptLoop;
    private Stream? _stream;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private int _authenticated;

    public SignalingServer(Func<string, string?> authenticate, Func<JsonElement, Task> onMessage)
    {
        _authenticate = authenticate;
        _onMessage = onMessage;
    }

    public int Port { get; private set; }
    public bool HasAuthenticatedClient => Volatile.Read(ref _authenticated) == 1;
    public event Action<string>? StatusChanged;

    public async Task StartAsync(X509Certificate2 certificate, int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = Task.Run(() => AcceptLoop(certificate));
        await Task.CompletedTask;
    }

    public async Task<bool> SendAsync(string json)
    {
        var stream = _stream;
        if (stream == null || Volatile.Read(ref _authenticated) == 0) return false;
        var payload = Encoding.UTF8.GetBytes(json);
        await _sendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await WriteFrameAsync(stream, 0x1, payload, _cts.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            StatusChanged?.Invoke("Envoi signalisation impossible (" + ex.GetType().Name + ")");
            return false;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task AcceptLoop(X509Certificate2 certificate)
    {
        while (!_cts.IsCancellationRequested && _listener != null)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or OperationCanceledException or SocketException)
            {
                break;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleClientAsync(client, certificate).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or AuthenticationException or InvalidDataException or OperationCanceledException)
                {
                    StatusChanged?.Invoke("Session signalisation fermée (" + ex.GetType().Name + ")");
                }
                finally
                {
                    client.Dispose();
                }
            });
        }
    }

    private async Task HandleClientAsync(TcpClient client, X509Certificate2 certificate)
    {
        client.NoDelay = true;
        await using var ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
        await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = certificate,
            ClientCertificateRequired = false,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
        }, _cts.Token).ConfigureAwait(false);

        if (!await ReadHandshakeAsync(ssl).ConfigureAwait(false))
        {
            StatusChanged?.Invoke("Requête signalisation refusée");
            return;
        }

        var authed = false;
        while (!_cts.IsCancellationRequested)
        {
            var frame = await ReadFrameAsync(ssl, _cts.Token).ConfigureAwait(false);
            if (frame == null) break;
            if (frame.Value.Opcode == 0x8) break;
            if (frame.Value.Opcode == 0x9)
            {
                await _sendLock.WaitAsync(_cts.Token).ConfigureAwait(false);
                try { await WriteFrameAsync(ssl, 0xA, frame.Value.Payload, _cts.Token).ConfigureAwait(false); }
                finally { _sendLock.Release(); }
                continue;
            }
            if (frame.Value.Opcode != 0x1) continue;

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(frame.Value.Payload);
            }
            catch (JsonException)
            {
                StatusChanged?.Invoke("Message illisible");
                continue;
            }

            using (doc)
            {
                var root = doc.RootElement;
                var type = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
                if (!authed)
                {
                    if (type != "hello")
                    {
                        await ReplyAsync(ssl, "{\"type\":\"hello-rejected\",\"reason\":\"invalid\"}").ConfigureAwait(false);
                        break;
                    }
                    var token = root.TryGetProperty("token", out var tokenEl) && tokenEl.ValueKind == JsonValueKind.String
                        ? tokenEl.GetString()
                        : null;
                    var versionOk = root.TryGetProperty("v", out var versionEl) && versionEl.ValueKind == JsonValueKind.Number && versionEl.GetInt32() == PairingTicket.ProtocolVersion;
                    var reason = versionOk ? _authenticate(token ?? "") : "version";
                    if (reason != null)
                    {
                        await ReplyAsync(ssl, "{\"type\":\"hello-rejected\",\"reason\":\"" + reason + "\"}").ConfigureAwait(false);
                        StatusChanged?.Invoke(ReasonText(reason));
                        break;
                    }

                    authed = true;
                    _stream = ssl;
                    Volatile.Write(ref _authenticated, 1);
                    await ReplyAsync(ssl, "{\"type\":\"hello-ok\"}").ConfigureAwait(false);
                    StatusChanged?.Invoke("Téléphone accepté");
                    continue;
                }

                if (type == "bye")
                {
                    StatusChanged?.Invoke("Téléphone fermé");
                    break;
                }

                await _onMessage(root.Clone()).ConfigureAwait(false);
            }
        }

        if (ReferenceEquals(_stream, ssl))
        {
            _stream = null;
            Volatile.Write(ref _authenticated, 0);
            StatusChanged?.Invoke("Téléphone déconnecté");
        }
    }

    private static string ReasonText(string reason) => reason switch
    {
        "expired" => "Jeton expiré",
        "used" => "Jeton déjà utilisé",
        "version" => "Version de QR non reconnue",
        _ => "Jeton refusé",
    };

    private async Task ReplyAsync(Stream stream, string json)
    {
        await _sendLock.WaitAsync(_cts.Token).ConfigureAwait(false);
        try
        {
            await WriteFrameAsync(stream, 0x1, Encoding.UTF8.GetBytes(json), _cts.Token).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private static async Task<bool> ReadHandshakeAsync(Stream stream)
    {
        var header = new MemoryStream();
        var one = new byte[1];
        while (header.Length < 8192)
        {
            var read = await stream.ReadAsync(one).ConfigureAwait(false);
            if (read == 0) return false;
            header.WriteByte(one[0]);
            if (header.Length >= 4)
            {
                var buf = header.GetBuffer();
                var n = (int)header.Length;
                if (buf[n - 4] == '\r' && buf[n - 3] == '\n' && buf[n - 2] == '\r' && buf[n - 1] == '\n') break;
            }
        }

        var text = Encoding.ASCII.GetString(header.GetBuffer(), 0, (int)header.Length);
        if (!text.StartsWith("GET /pair ", StringComparison.Ordinal)) return false;
        var key = Header(text, "Sec-WebSocket-Key");
        if (string.IsNullOrEmpty(key)) return false;
        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        var response = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n";
        var bytes = Encoding.ASCII.GetBytes(response);
        await stream.WriteAsync(bytes).ConfigureAwait(false);
        return true;
    }

    private static string? Header(string text, string name)
    {
        foreach (var line in text.Split("\r\n"))
        {
            var split = line.IndexOf(':');
            if (split <= 0) continue;
            if (!line[..split].Trim().Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            return line[(split + 1)..].Trim();
        }
        return null;
    }

    private static async Task<(int Opcode, byte[] Payload)?> ReadFrameAsync(Stream stream, CancellationToken ct)
    {
        using var payload = new MemoryStream();
        var opcode = 0;
        while (true)
        {
            var header = await ReadExactAsync(stream, 2, ct).ConfigureAwait(false);
            if (header == null) return null;
            var fin = (header[0] & 0x80) != 0;
            var currentOpcode = header[0] & 0x0F;
            if (currentOpcode != 0) opcode = currentOpcode;
            var masked = (header[1] & 0x80) != 0;
            ulong length = (ulong)(header[1] & 0x7F);
            if (length == 126)
            {
                var ext = await ReadExactAsync(stream, 2, ct).ConfigureAwait(false);
                if (ext == null) return null;
                length = BinaryPrimitives.ReadUInt16BigEndian(ext);
            }
            else if (length == 127)
            {
                var ext = await ReadExactAsync(stream, 8, ct).ConfigureAwait(false);
                if (ext == null) return null;
                length = BinaryPrimitives.ReadUInt64BigEndian(ext);
            }
            if (length > 1_000_000) throw new InvalidDataException("frame");
            var mask = masked ? await ReadExactAsync(stream, 4, ct).ConfigureAwait(false) : null;
            if (masked && mask == null) return null;
            var data = await ReadExactAsync(stream, (int)length, ct).ConfigureAwait(false);
            if (data == null) return null;
            if (mask != null)
            {
                for (var i = 0; i < data.Length; i++) data[i] ^= mask[i % 4];
            }
            payload.Write(data);
            if (fin) return (opcode, payload.ToArray());
        }
    }

    private static async Task WriteFrameAsync(Stream stream, int opcode, byte[] payload, CancellationToken ct)
    {
        var header = new byte[payload.Length < 126 ? 2 : payload.Length <= ushort.MaxValue ? 4 : 10];
        header[0] = (byte)(0x80 | opcode);
        if (payload.Length < 126)
        {
            header[1] = (byte)payload.Length;
        }
        else if (payload.Length <= ushort.MaxValue)
        {
            header[1] = 126;
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(2), (ushort)payload.Length);
        }
        else
        {
            header[1] = 127;
            BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(2), (ulong)payload.Length);
        }
        await stream.WriteAsync(header, ct).ConfigureAwait(false);
        await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task<byte[]?> ReadExactAsync(Stream stream, int count, CancellationToken ct)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), ct).ConfigureAwait(false);
            if (read == 0) return null;
            offset += read;
        }
        return buffer;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener?.Stop();
        try
        {
            if (_acceptLoop != null) await _acceptLoop.ConfigureAwait(false);
        }
        catch
        {
            // L'arrêt du listener interrompt la boucle d'acceptation.
        }
        _sendLock.Dispose();
        _cts.Dispose();
    }
}
