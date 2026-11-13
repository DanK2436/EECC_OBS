namespace EeccStudio.Core.Audio;

/// <summary>
/// Bus master stéréo 16 bits. Les pistes soumises sont sommées selon leur volume et leur sourdine.
/// Chaque lecteur (une destination) reçoit une copie : une lecture ne retire pas le son des autres.
/// </summary>
public sealed class ProgramAudioMix
{
    public const int SampleRate = 44100;
    public const int Channels = 2;

    private readonly object _gate = new();
    private readonly Dictionary<string, Track> _tracks = new(StringComparer.Ordinal);
    private readonly short[] _ring = new short[SampleRate * Channels];
    private int _ringWrite;
    private long _samplesWritten;
    private readonly int _identity = Random.Shared.Next(1, int.MaxValue);

    [ThreadStatic]
    private static int _readerMix;

    [ThreadStatic]
    private static long _readerPos;

    public double MasterLevelDb { get; private set; } = -60;

    public void SetGain(string id, float volume)
    {
        lock (_gate)
            TrackFor(id).Volume = Math.Clamp(volume, 0f, 1f);
    }

    public void SetMute(string id, bool mute)
    {
        lock (_gate)
            TrackFor(id).Mute = mute;
    }

    public void Submit(string id, ReadOnlySpan<short> interleavedStereo)
    {
        if (interleavedStereo.Length == 0)
            return;
        lock (_gate)
        {
            var track = TrackFor(id);
            var queue = track.Pending;
            for (var i = 0; i < interleavedStereo.Length; i++)
                queue.Enqueue(interleavedStereo[i]);
            var cap = SampleRate * Channels * 2;
            while (queue.Count > cap)
                queue.Dequeue();
        }
    }

    public byte[] Render(int frames)
    {
        if (frames < 1)
            frames = 1;
        lock (_gate)
        {
            Commit(frames);
            return Read(frames);
        }
    }

    private void Commit(int frames)
    {
        var count = frames * Channels;
        var master = _tracks.TryGetValue("master", out var masterTrack) ? masterTrack : null;
        var masterVolume = master?.Volume ?? 1f;
        var masterMute = master?.Mute ?? false;
        double sumSq = 0;
        var produced = 0;
        for (var i = 0; i < count; i++)
        {
            var any = false;
            var mixed = 0;
            foreach (var pair in _tracks)
            {
                if (pair.Key == "master")
                    continue;
                var track = pair.Value;
                if (track.Pending.Count == 0)
                    continue;
                any = true;
                var sample = track.Pending.Dequeue();
                if (!track.Mute)
                    mixed += (int)(sample * track.Volume);
            }

            if (!any && produced == 0 && !HasPending())
                break;

            if (masterMute)
                mixed = 0;
            else
                mixed = (int)(mixed * masterVolume);
            if (mixed > short.MaxValue)
                mixed = short.MaxValue;
            if (mixed < short.MinValue)
                mixed = short.MinValue;
            _ring[_ringWrite] = (short)mixed;
            _ringWrite = (_ringWrite + 1) % _ring.Length;
            _samplesWritten++;
            sumSq += mixed * (double)mixed;
            produced++;
        }

        MasterLevelDb = produced == 0 || sumSq <= 0
            ? -60
            : Math.Max(-60, 20.0 * Math.Log10(Math.Sqrt(sumSq / produced) / 32768.0));
    }

    private bool HasPending()
    {
        foreach (var pair in _tracks)
        {
            if (pair.Key != "master" && pair.Value.Pending.Count > 0)
                return true;
        }

        return false;
    }

    private byte[] Read(int frames)
    {
        var need = frames * Channels;
        if (_readerMix != _identity || _readerPos > _samplesWritten)
        {
            _readerMix = _identity;
            _readerPos = Math.Max(0, _samplesWritten - need);
        }

        if (_samplesWritten - _readerPos > _ring.Length)
            _readerPos = _samplesWritten;

        var available = _samplesWritten - _readerPos;
        var take = (int)Math.Min(need, Math.Max(0, available));
        var bytes = new byte[need * 2];
        var index = (int)(_readerPos % _ring.Length);
        for (var i = 0; i < take; i++)
        {
            var sample = _ring[index];
            bytes[i * 2] = (byte)sample;
            bytes[i * 2 + 1] = (byte)(sample >> 8);
            index = (index + 1) % _ring.Length;
        }

        _readerPos += take;
        return bytes;
    }

    private Track TrackFor(string id)
    {
        if (!_tracks.TryGetValue(id, out var track))
        {
            track = new Track();
            _tracks[id] = track;
        }

        return track;
    }

    private sealed class Track
    {
        public float Volume = 1f;
        public bool Mute;
        public Queue<short> Pending { get; } = new();
    }
}
