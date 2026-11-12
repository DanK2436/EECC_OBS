using System.Net;
using System.Text.Json;
using NAudio.Wave;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using Vpx.Net;

namespace EeccStudio.Core.Media;

public sealed record DecodedVideoFrame(byte[] Bgr, int Width, int Height, int Stride, string PixelFormat);

public sealed class LanMediaSession : IDisposable
{
    private readonly object _gate = new();
    private readonly List<(string Candidate, string? Mid, ushort Line)> _pendingIce = new();
    private readonly RTCPeerConnection _pc;
    private readonly Vp8NetVideoEncoderEndPoint _video = new();
    private readonly AudioEncoder _audioEncoder = new();
    private readonly List<(DateTime At, int Bytes)> _videoWindow = new();
    private WaveOutEvent? _waveOut;
    private BufferedWaveProvider? _provider;
    private bool _remoteSet;
    private bool _muted;
    private bool _disposed;
    private int _playbackFailed;

    public LanMediaSession(IPAddress bindAddress)
    {
        var config = new RTCConfiguration
        {
            iceServers = new List<RTCIceServer>(),
            X_BindAddress = bindAddress,
        };
        _pc = new RTCPeerConnection(config);
        _video.SetVideoSinkFormat(new VideoFormat(VideoCodecsEnum.VP8, 96));
        _pc.addTrack(new MediaStreamTrack(
            new List<VideoFormat> { new VideoFormat(VideoCodecsEnum.VP8, 96) },
            MediaStreamStatusEnum.RecvOnly));
        _pc.addTrack(new MediaStreamTrack(
            new List<AudioFormat> { new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMU) },
            MediaStreamStatusEnum.RecvOnly));

        _pc.onicecandidate += candidate =>
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.candidate)) return;
            LocalIceCandidateJson?.Invoke(JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["type"] = "ice",
                ["candidate"] = candidate.candidate,
                ["sdpMid"] = candidate.sdpMid,
                ["sdpMLineIndex"] = candidate.sdpMLineIndex,
            }));
        };
        _pc.oniceconnectionstatechange += state =>
        {
            IceState = state.ToString();
            StatusChanged?.Invoke("ICE " + IceState);
        };
        _video.OnVideoSinkDecodedSample += OnDecodedSample;
        _pc.OnVideoFrameReceived += OnEncodedVideo;
        _pc.OnAudioFrameReceived += OnEncodedAudio;
    }

    public string IceState { get; private set; } = "new";
    public long VideoFrames { get; private set; }
    public long AudioPackets { get; private set; }
    public int FrameWidth { get; private set; }
    public int FrameHeight { get; private set; }
    public bool PlaybackMuted => _muted;
    public event Action<string>? StatusChanged;
    public event Action<DecodedVideoFrame>? VideoFrameDecoded;
    public event Action<string>? LocalIceCandidateJson;

    public double? MeasuredFps
    {
        get
        {
            lock (_gate)
            {
                if (_videoWindow.Count < 2) return null;
                var span = (_videoWindow[^1].At - _videoWindow[0].At).TotalSeconds;
                if (span < 0.4) return null;
                return (_videoWindow.Count - 1) / span;
            }
        }
    }

    public double? MeasuredVideoKbps
    {
        get
        {
            lock (_gate)
            {
                if (_videoWindow.Count < 2) return null;
                var span = (_videoWindow[^1].At - _videoWindow[0].At).TotalSeconds;
                if (span < 0.4) return null;
                var bytes = _videoWindow.Sum(item => item.Bytes);
                return bytes * 8d / 1000d / span;
            }
        }
    }

    public void SetPlaybackMuted(bool muted)
    {
        _muted = muted;
        lock (_gate)
        {
            if (_waveOut != null) _waveOut.Volume = muted ? 0f : 1f;
        }
        StatusChanged?.Invoke(muted ? "Lecture locale coupée" : "Lecture locale non coupée");
    }

    public async Task<string> AcceptOfferAsync(string offerSdp)
    {
        await _video.StartVideoSink().ConfigureAwait(false);
        var result = _pc.setRemoteDescription(new RTCSessionDescriptionInit
        {
            type = RTCSdpType.offer,
            sdp = offerSdp,
        });
        if (result != SetDescriptionResultEnum.OK)
            throw new InvalidOperationException("Offre refusée (" + result + ")");

        List<(string Candidate, string? Mid, ushort Line)> pending;
        lock (_gate)
        {
            _remoteSet = true;
            pending = _pendingIce.ToList();
            _pendingIce.Clear();
        }
        foreach (var ice in pending) AddIce(ice.Candidate, ice.Mid, ice.Line);

        var answer = _pc.createAnswer(null);
        await _pc.setLocalDescription(answer).ConfigureAwait(false);
        return answer.sdp ?? throw new InvalidOperationException("Réponse SDP vide");
    }

    public void AddIce(string? candidate, string? mid, ushort line)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return;
        lock (_gate)
        {
            if (!_remoteSet)
            {
                _pendingIce.Add((candidate, mid, line));
                return;
            }
        }

        _pc.addIceCandidate(new RTCIceCandidateInit
        {
            candidate = candidate,
            sdpMid = mid,
            sdpMLineIndex = line,
        });
    }

    private void OnEncodedVideo(IPEndPoint endpoint, uint timestamp, byte[] frame, VideoFormat format)
    {
        try
        {
            lock (_gate)
            {
                var now = DateTime.UtcNow;
                _videoWindow.Add((now, frame?.Length ?? 0));
                _videoWindow.RemoveAll(item => (now - item.At).TotalSeconds > 2);
            }
            if (frame != null) _video.GotVideoFrame(endpoint, timestamp, frame, format);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Décodage vidéo en échec (" + ex.GetType().Name + ")");
        }
    }

    private void OnDecodedSample(byte[] sample, uint width, uint height, int stride, VideoPixelFormatsEnum pixelFormat)
    {
        if (sample == null || sample.Length == 0 || width == 0 || height == 0 || width > 4096 || height > 4096) return;
        var copy = new byte[sample.Length];
        Buffer.BlockCopy(sample, 0, copy, 0, sample.Length);
        VideoFrames++;
        FrameWidth = (int)width;
        FrameHeight = (int)height;
        VideoFrameDecoded?.Invoke(new DecodedVideoFrame(copy, (int)width, (int)height, stride, pixelFormat.ToString()));
    }

    private void OnEncodedAudio(EncodedAudioFrame frame)
    {
        try
        {
            if (frame.EncodedAudio == null || frame.EncodedAudio.Length == 0) return;
            var pcm = _audioEncoder.DecodeAudio(frame.EncodedAudio, frame.AudioFormat);
            if (pcm == null || pcm.Length == 0) return;
            AudioPackets++;
            if (_muted) return;
            Play(pcm, frame.AudioFormat);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Décodage audio en échec (" + ex.GetType().Name + ")");
        }
    }

    private void Play(short[] pcm, AudioFormat format)
    {
        var bytes = new byte[pcm.Length * 2];
        Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);
        lock (_gate)
        {
            if (_disposed || _muted) return;
            if (_provider == null)
            {
                try
                {
                    var rate = format.ClockRate > 0 ? format.ClockRate : 8000;
                    var channels = format.ChannelCount > 0 ? format.ChannelCount : 1;
                    _provider = new BufferedWaveProvider(new WaveFormat(rate, 16, channels))
                    {
                        DiscardOnBufferOverflow = true,
                        BufferDuration = TimeSpan.FromMilliseconds(400),
                    };
                    _waveOut = new WaveOutEvent { DesiredLatency = 120 };
                    _waveOut.Init(_provider);
                    _waveOut.Volume = _muted ? 0f : 1f;
                    _waveOut.Play();
                }
                catch (Exception ex)
                {
                    if (Interlocked.Exchange(ref _playbackFailed, 1) == 0)
                        StatusChanged?.Invoke("Lecture audio impossible (" + ex.GetType().Name + ")");
                    return;
                }
            }
            _provider.AddSamples(bytes, 0, bytes.Length);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pc.OnVideoFrameReceived -= OnEncodedVideo;
        _pc.OnAudioFrameReceived -= OnEncodedAudio;
        _video.OnVideoSinkDecodedSample -= OnDecodedSample;
        lock (_gate)
        {
            try { _waveOut?.Stop(); } catch { /* déjà arrêté */ }
            try { _waveOut?.Dispose(); } catch { /* périphérique déjà fermé */ }
            _waveOut = null;
        }
        try { _pc.close(); } catch { /* session déjà fermée */ }
        try { _video.CloseVideo().Wait(TimeSpan.FromSeconds(2)); } catch { /* fermeture déjà demandée */ }
    }
}
