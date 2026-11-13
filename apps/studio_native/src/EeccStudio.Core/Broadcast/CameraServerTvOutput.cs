using EeccStudio.Broadcast;

namespace EeccStudio.Core.Broadcast;

/// <summary>
/// Relie la diffusion au serveur HTTP déjà lu par EECC TV.
/// Arrêter le direct ne ferme pas l'écoute des caméras.
/// </summary>
public sealed class CameraServerTvOutput : ITvOutput
{
    private readonly Network.CameraServer _server;

    public CameraServerTvOutput(Network.CameraServer server)
    {
        _server = server;
    }

    public Task<TvOutputResult> StartAsync(byte[]? programFrame, CancellationToken cancellationToken)
    {
        if (!_server.TryStart(out var detail))
        {
            var message = string.IsNullOrWhiteSpace(detail)
                ? "Le serveur local EECC TV n'écoute pas."
                : detail;
            return Task.FromResult(new TvOutputResult(false, null, message));
        }

        if (programFrame is { Length: > 3 } &&
            _server.ActiveProgramFrameBytes == null &&
            _server.LatestCameraFrameBytes == null)
        {
            _server.ActiveProgramFrameBytes = programFrame;
        }

        _server.StartStreaming();
        var playback = $"http://127.0.0.1:{_server.Port}{EeccTvContract.StreamPath}";
        return Task.FromResult(new TvOutputResult(true, playback, null));
    }

    public Task StopAsync()
    {
        _server.StopStreaming();
        return Task.CompletedTask;
    }
}
