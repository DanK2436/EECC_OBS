using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using EeccStudio.Core.Audio;

namespace EeccStudio;

public sealed class MixerStrip : INotifyPropertyChanged
{
    private readonly AudioEngine _engine;
    private double _fader;
    private double _meterLeft;
    private double _meterRight;
    private bool _muted;
    private bool _locked;
    private AudioMonitorMode _monitor;
    private Brush _meterBrush = Brushes.Transparent;

    public MixerStrip(AudioEngine engine, AudioChannel channel)
    {
        _engine = engine;
        Id = channel.Id;
        Name = channel.Name;
        _muted = channel.IsMuted;
        _locked = channel.Locked;
        _monitor = channel.Monitor;
        _fader = 100.0 * Math.Cbrt(Math.Clamp(channel.VolumeLinear, 0, 1));
        _meterBrush = Freeze(Color.FromRgb(0x2F, 0xE0, 0x6A));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }
    public string Name { get; }
    public bool CanMove => !_locked;
    public string DbText => FormatDb(Linear);
    public string MuteGlyph => _muted ? "M" : "🔊";
    public Brush MuteBrush => _muted ? Brushes.IndianRed : Brushes.White;
    public string MonitorGlyph => _monitor switch
    {
        AudioMonitorMode.MonitorOnly => "◐",
        AudioMonitorMode.Both => "◉",
        _ => "○",
    };
    public string MonitorTip => _monitor switch
    {
        AudioMonitorMode.MonitorOnly => "Écoute seule (hors programme)",
        AudioMonitorMode.Both => "Écoute et programme",
        _ => "Programme seul",
    };

    public double Fader
    {
        get => _fader;
        set
        {
            var next = Math.Clamp(value, 0, 100);
            if (Math.Abs(next - _fader) < 0.05)
                return;
            _fader = next;
            _engine.SetVolume(Id, (float)Linear);
            Notify(nameof(Fader));
            Notify(nameof(DbText));
        }
    }

    public double MeterLeft
    {
        get => _meterLeft;
        private set { _meterLeft = value; Notify(); }
    }

    public double MeterRight
    {
        get => _meterRight;
        private set { _meterRight = value; Notify(); }
    }

    public Brush MeterBrush
    {
        get => _meterBrush;
        private set { _meterBrush = value; Notify(); }
    }

    public bool IsMuted => _muted;

    public double Balance
    {
        get
        {
            var channel = _engine.Channels.FirstOrDefault(item => item.Id == Id);
            return channel?.Balance ?? 0;
        }
        set => _engine.SetBalance(Id, (float)value);
    }

    public void ApplyLevel(double db)
    {
        var height = Math.Clamp((db + 60.0) / 60.0 * 120.0, 0, 120);
        MeterLeft = height;
        MeterRight = Math.Max(0, height - 4);
        MeterBrush = db >= -9
            ? Freeze(Color.FromRgb(0xE0, 0x32, 0x32))
            : db >= -20
                ? Freeze(Color.FromRgb(0xF5, 0xA6, 0x23))
                : Freeze(Color.FromRgb(0x2F, 0xE0, 0x6A));
    }

    public void ToggleMute()
    {
        _muted = !_muted;
        _engine.SetMute(Id, _muted);
        Notify(nameof(MuteGlyph));
        Notify(nameof(MuteBrush));
        Notify(nameof(IsMuted));
    }

    public void CycleMonitor()
    {
        _monitor = _monitor switch
        {
            AudioMonitorMode.Off => AudioMonitorMode.Both,
            AudioMonitorMode.Both => AudioMonitorMode.MonitorOnly,
            _ => AudioMonitorMode.Off,
        };
        _engine.SetMonitor(Id, _monitor);
        Notify(nameof(MonitorGlyph));
        Notify(nameof(MonitorTip));
    }

    public void ToggleLock()
    {
        _locked = !_locked;
        var channel = _engine.Channels.First(item => item.Id == Id);
        channel.Locked = _locked;
        Notify(nameof(CanMove));
    }

    public void SetDelay(int milliseconds) => _engine.SetChannelDelay(Id, milliseconds);

    public DspChain Filters => _engine.GetOrCreateDsp(Id);

    private double Linear => Math.Pow(Math.Clamp(_fader, 0, 100) / 100.0, 3);

    private static string FormatDb(double linear) =>
        linear < 0.001 ? "-inf dB" : $"{20.0 * Math.Log10(linear):0.0} dB";

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
