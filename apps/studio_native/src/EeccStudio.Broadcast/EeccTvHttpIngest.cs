using System.Net;
using System.Net.Sockets;
using System.Text;

namespace EeccStudio.Broadcast;

/// <summary>
/// Adaptateur local du contrat EECC TV quand on ne passe pas par le serveur caméra du Studio.
/// L'hôte et le port sont choisis par l'appelant. Aucun service cloud n'est contacté.
/// </summary>
public sealed class EeccTvHttpIngest : ITvOutput, IAsyncDisposable
{
    private readonly string _bindHost;
    private readonly int _requestedPort;
    private readonly string _title;
    private readonly string _speaker;
    private readonly string _scene;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;
    private int _clients;
    private volatile bool _isLive;
    private byte[] _frame;

    public EeccTvHttpIngest(
        string bindHost = "127.0.0.1",
        int port = 0,
        string title = "Culte EECC",
        string speaker = "",
        string scene = "Programme")
    {
        _bindHost = string.IsNullOrWhiteSpace(bindHost) ? "127.0.0.1" : bindHost.Trim();
        _requestedPort = port;
        _title = title;
        _speaker = speaker;
        _scene = scene;
        _frame = EeccTvFrames.Slate;
    }

    public int Port { get; private set; }

    public bool IsLive => _isLive;

    public string PlaybackUrl => $"http://127.0.0.1:{Port}{EeccTvContract.StreamPath}";

    public async Task<TvOutputResult> StartAsync(byte[]? programFrame, CancellationToken cancellationToken)
    {
        if (programFrame is { Length: > 3 })
            _frame = programFrame;

        if (_listener == null)
        {
            var address = _bindHost is "0.0.0.0" or "*"
                ? IPAddress.Any
                : IPAddress.Parse(_bindHost);
            _listener = new TcpListener(address, _requestedPort);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _cts = new CancellationTokenSource();
            _acceptLoop = Task.Run(() => AcceptAsync(_cts.Token));
        }

        _isLive = true;
        await Task.CompletedTask;
        return new TvOutputResult(true, PlaybackUrl, null);
    }

    public Task StopAsync()
    {
        _isLive = false;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _isLive = false;
        _cts?.Cancel();
        _listener?.Stop();
        if (_acceptLoop != null)
        {
            try { await _acceptLoop; }
            catch { }
        }
        _cts?.Dispose();
        _listener = null;
    }

    private async Task AcceptAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener != null)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch
            {
                break;
            }

            _ = Task.Run(() => HandleAsync(client, cancellationToken), cancellationToken);
        }
    }

    private async Task HandleAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var path = (await EeccTvContract.ReadRequestPathAsync(stream, cancellationToken)).ToLowerInvariant();
                if (path is "/live/status" or "/status" or "/api/status")
                {
                    var json = EeccTvContract.StatusJson(_isLive, PlaybackUrl, _title, _speaker, _scene, _clients);
                    await EeccTvContract.WriteJsonAsync(stream, json, cancellationToken);
                    return;
                }

                if (path is "/live/frame.jpg" or "/frame.jpg")
                {
                    if (!_isLive)
                    {
                        await EeccTvContract.WriteOfflineAsync(stream, cancellationToken);
                        return;
                    }

                    await EeccTvContract.WriteJpegAsync(stream, _frame, cancellationToken);
                    return;
                }

                if (path == "/live/stream")
                {
                    Interlocked.Increment(ref _clients);
                    try
                    {
                        await EeccTvContract.WriteMjpegAsync(
                            stream,
                            () => _isLive,
                            () => _frame,
                            () => true,
                            cancellationToken);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _clients);
                    }
                }
                else
                {
                    var body = Encoding.UTF8.GetBytes("{\"error\":\"introuvable\"}");
                    await EeccTvContract.WriteJsonAsync(stream, body, cancellationToken);
                }
            }
            catch
            {
            }
        }
    }
}
