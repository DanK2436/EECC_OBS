using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EeccStudio.Core.Audio;

namespace EeccStudio;

public partial class AdvancedAudioWindow : Window
{
    public AdvancedAudioWindow(IReadOnlyList<MixerStrip> strips)
    {
        InitializeComponent();
        Rows.Children.Add(Header());
        foreach (var strip in strips)
            Rows.Children.Add(BuildRow(strip));
    }

    private UIElement BuildRow(MixerStrip strip)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

        var name = new TextBlock
        {
            Text = strip.Name,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var balance = new Slider { Minimum = -1, Maximum = 1, Value = strip.Balance, TickFrequency = 1, IsSnapToTickEnabled = false, VerticalAlignment = VerticalAlignment.Center };
        balance.ValueChanged += (_, e) => strip.Balance = e.NewValue;
        var delay = new TextBox
        {
            Text = strip.Filters.DelayMs.ToString(),
            Width = 52,
            Background = new SolidColorBrush(Color.FromRgb(0x18, 0x19, 0x1C)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x31, 0x36)),
            Padding = new Thickness(4, 2, 4, 2),
        };
        delay.LostFocus += (_, _) =>
        {
            var digits = new string(delay.Text.Where(char.IsDigit).ToArray());
            strip.SetDelay(int.TryParse(digits, out var ms) ? ms : 0);
        };
        var monitor = new ComboBox();
        monitor.Items.Add("Programme");
        monitor.Items.Add("Écoute seule");
        monitor.Items.Add("Écoute et programme");
        monitor.SelectedIndex = (int)ReadMonitor(strip);
        monitor.SelectionChanged += (_, _) => ApplyMonitor(strip, monitor.SelectedIndex);
        var filters = new Button
        {
            Content = "Filtres",
            Background = new SolidColorBrush(Color.FromRgb(0x36, 0x39, 0x3F)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x31, 0x36)),
            Padding = new Thickness(8, 2, 8, 2),
        };
        filters.Click += (_, _) => ShowFilters(strip);

        Add(grid, name, 0);
        Add(grid, LabelOf(strip), 1);
        Add(grid, balance, 2);
        Add(grid, delay, 3);
        Add(grid, monitor, 4);
        Add(grid, filters, 5);
        return grid;
    }

    private static TextBlock LabelOf(MixerStrip strip) => new()
    {
        Text = strip.DbText,
        Foreground = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0)),
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(8, 0, 0, 0),
    };

    private static UIElement Header()
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        string[] titles = ["Source", "Volume", "Balance", "Décalage", "Écoute", ""];
        for (var i = 0; i < titles.Length; i++)
        {
            var text = new TextBlock { Text = titles[i], Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x92, 0x97)), FontSize = 11 };
            Add(grid, text, i);
        }
        return grid;
    }

    private static AudioMonitorMode ReadMonitor(MixerStrip strip) => strip.MonitorGlyph switch
    {
        "◐" => AudioMonitorMode.MonitorOnly,
        "◉" => AudioMonitorMode.Both,
        _ => AudioMonitorMode.Off,
    };

    private static void ApplyMonitor(MixerStrip strip, int index)
    {
        var target = index switch
        {
            1 => AudioMonitorMode.MonitorOnly,
            2 => AudioMonitorMode.Both,
            _ => AudioMonitorMode.Off,
        };
        while (ReadMonitor(strip) != target)
            strip.CycleMonitor();
    }

    private void ShowFilters(MixerStrip strip)
    {
        var dsp = strip.Filters;
        var high = Check("Coupe-bas 80 Hz", dsp.EnableHighPass);
        var gate = Check("Noise gate", dsp.EnableNoiseGate);
        var comp = Check("Compresseur", dsp.EnableCompressor);
        var limit = Check("Limiteur −1 dB", dsp.EnableLimiter);
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(high);
        panel.Children.Add(gate);
        panel.Children.Add(comp);
        panel.Children.Add(limit);
        var close = new Button { Content = "Fermer", Width = 90, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        panel.Children.Add(close);
        var dialog = new Window
        {
            Title = "Filtres — " + strip.Name,
            Content = panel,
            Width = 320,
            Height = 220,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(0x20, 0x22, 0x25)),
        };
        close.Click += (_, _) =>
        {
            dsp.EnableHighPass = high.IsChecked == true;
            dsp.EnableNoiseGate = gate.IsChecked == true;
            dsp.EnableCompressor = comp.IsChecked == true;
            dsp.EnableLimiter = limit.IsChecked == true;
            dialog.Close();
        };
        dialog.ShowDialog();
    }

    private static CheckBox Check(string label, bool value) => new()
    {
        Content = label,
        IsChecked = value,
        Foreground = Brushes.White,
        Margin = new Thickness(0, 4, 0, 0),
    };

    private static void Add(Grid grid, UIElement element, int column)
    {
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
