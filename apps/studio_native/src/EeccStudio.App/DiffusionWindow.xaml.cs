using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EeccStudio.Broadcast;
using EeccStudio.Core.Audio;
using EeccStudio.Core.Broadcast;
using EeccStudio.Core.Network;

namespace EeccStudio;

public partial class DiffusionWindow : Window
{
    private readonly CameraServer _server;
    private readonly AudioEngine _audio;
    private readonly List<ExternalRow> _rows = new();
    private readonly List<ExternalDestination>? _restored;
    private bool _starting;

    public DiffusionWindow(CameraServer server, AudioEngine audio)
    {
        InitializeComponent();
        _server = server;
        _audio = audio;
        var lan = _server.GetLocalIpAddress();
        TxtTvUrl.Text =
            "Toujours incluse, sans clé de flux.\n" +
            $"Cette machine : http://127.0.0.1:{_server.Port}/live/stream\n" +
            $"Adresse annoncée : http://{lan}:{_server.Port}/live/stream";

        BroadcastPlan? restored = null;
        try
        {
            restored = new BroadcastProfileStore(BroadcastProfileStore.DefaultDirectory()).Load();
        }
        catch
        {
            restored = null;
        }

        _restored = restored?.Externals.ToList();
        if (restored != null)
        {
            ChkPublic.IsChecked = restored.AllowPublicIngest;
            if (restored.Mode == BroadcastMode.TvPlusOne)
                RbMode2.IsChecked = true;
            else if (restored.Mode == BroadcastMode.TvPlusThreeOrMore)
                RbMode3.IsChecked = true;
        }

        var initial = Math.Max(3, _restored?.Count ?? 0);
        for (var index = 0; index < initial; index++)
            AddRow(_restored != null && index < _restored.Count ? _restored[index] : null);

        ApplyMode();
    }

    public BroadcastSession? StartedSession { get; private set; }

    private BroadcastMode SelectedMode =>
        RbMode3.IsChecked == true ? BroadcastMode.TvPlusThreeOrMore :
        RbMode2.IsChecked == true ? BroadcastMode.TvPlusOne :
        BroadcastMode.TvOnly;

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (PanelExternals == null)
            return;
        ApplyMode();
    }

    private void ApplyMode()
    {
        var mode = SelectedMode;
        var visible = mode switch
        {
            BroadcastMode.TvOnly => 0,
            BroadcastMode.TvPlusOne => 1,
            _ => Math.Max(3, _rows.Count(row => row.IsActive))
        };

        if (mode == BroadcastMode.TvPlusThreeOrMore)
            visible = Math.Max(3, _rows.Count(row => row.IsActive));

        PanelExternalHeader.Visibility = visible == 0 ? Visibility.Collapsed : Visibility.Visible;
        BtnAdd.Visibility = mode == BroadcastMode.TvPlusThreeOrMore ? Visibility.Visible : Visibility.Collapsed;

        var shown = 0;
        foreach (var row in _rows)
        {
            var show = shown < visible;
            row.Root.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            row.IsActive = show;
            if (show)
                shown++;
        }
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMode != BroadcastMode.TvPlusThreeOrMore)
            return;
        AddRow(null);
        ApplyMode();
    }

    private void AddRow(ExternalDestination? restored)
    {
        var row = new ExternalRow(restored, RemoveRow);
        _rows.Add(row);
        PanelExternals.Children.Add(row.Root);
    }

    private void RemoveRow(ExternalRow row)
    {
        if (SelectedMode != BroadcastMode.TvPlusThreeOrMore)
        {
            TxtValidation.Text = "Ce mode a un nombre fixe de destinations externes.";
            return;
        }
        if (_rows.Count(item => item.IsActive) <= 3)
        {
            TxtValidation.Text = "Le mode 3 garde au moins trois destinations externes, en plus d'EECC TV.";
            return;
        }

        row.IsActive = false;
        _rows.Remove(row);
        PanelExternals.Children.Remove(row.Root);
        ApplyMode();
    }

    private BroadcastPlan BuildPlan()
    {
        var externals = new List<ExternalDestination>();
        if (SelectedMode != BroadcastMode.TvOnly)
        {
            foreach (var row in _rows.Where(item => item.IsActive))
                externals.Add(row.ToDestination());
        }

        return new BroadcastPlan
        {
            Mode = SelectedMode,
            AllowPublicIngest = ChkPublic.IsChecked == true,
            Tv = new TvDestination
            {
                Port = _server.Port,
                AnnouncedHost = _server.GetLocalIpAddress()
            },
            Externals = externals
        };
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (_starting || StartedSession != null)
            return;

        _starting = true;
        BtnStart.IsEnabled = false;
        try
        {
            var plan = BuildPlan();
            var validation = BroadcastPlanValidator.Validate(plan);
            TxtValidation.Text = validation.Summary;
            if (!validation.IsValid)
                return;

            var session = new BroadcastSession();
            var snapshot = await session.StartAsync(
                plan,
                new CameraServerTvOutput(_server),
                new FfmpegOutputFactory(),
                () => _server.ActiveProgramFrameBytes ?? _server.LatestCameraFrameBytes ?? EeccTvFrames.Slate,
                frames => _audio.RenderMaster(frames));

            TxtStatus.Text = Format(snapshot);
            TxtValidation.Text = snapshot.TvIsLive ? validation.Summary : snapshot.Summary;
            if (!snapshot.TvIsLive)
            {
                await session.DisposeAsync();
                return;
            }

            try
            {
                new BroadcastProfileStore(BroadcastProfileStore.DefaultDirectory()).Save(plan);
            }
            catch
            {
                TxtValidation.Text += " Le profil local n'a pas été enregistré.";
            }

            StartedSession = session;
            BtnStart.Visibility = Visibility.Collapsed;
            BtnCancel.Content = "Fermer";
        }
        catch (Exception)
        {
            TxtValidation.Text = "Le démarrage a échoué. Aucune clé n'est affichée.";
        }
        finally
        {
            _starting = false;
            if (StartedSession == null)
                BtnStart.IsEnabled = true;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = StartedSession != null;
        Close();
    }

    private static string Format(BroadcastSnapshot snapshot)
    {
        var lines = snapshot.Destinations.Select(status =>
        {
            var line = status.ToString();
            if (status.Kind == DestinationKind.EeccTv && !string.IsNullOrWhiteSpace(status.PlaybackUrl))
                line += " — " + status.PlaybackUrl;
            return line;
        });
        var text = snapshot.Summary + "\n" + string.Join("\n", lines);
        if (snapshot.Warnings.Count > 0)
            text += "\n" + string.Join("\n", snapshot.Warnings);
        return text;
    }

    private sealed class ExternalRow
    {
        private readonly TextBox _name;
        private readonly ComboBox _platform;
        private readonly TextBox _url;
        private readonly PasswordBox _key;
        private readonly TextBlock _note;
        private readonly string _id;

        public ExternalRow(ExternalDestination? restored, Action<ExternalRow> remove)
        {
            _id = restored?.Id ?? Guid.NewGuid().ToString("N");
            _name = Field(restored?.DisplayName ?? "Destination");
            _platform = new ComboBox { Margin = new Thickness(0, 6, 0, 0) };
            _platform.Items.Add(Item("YouTube", ExternalPlatform.YouTube));
            _platform.Items.Add(Item("Facebook", ExternalPlatform.Facebook));
            _platform.Items.Add(Item("TikTok", ExternalPlatform.TikTok));
            _platform.Items.Add(Item("Personnalisée", ExternalPlatform.Custom));
            _platform.SelectedIndex = restored == null ? 3 : PlatformIndex(restored.Platform);
            _platform.SelectionChanged += (_, _) => ApplySuggestion(restored == null);

            _url = Field(restored?.IngestUrl ?? string.Empty);
            _url.Margin = new Thickness(0, 6, 0, 0);
            _key = new PasswordBox
            {
                Margin = new Thickness(0, 6, 0, 0),
                Background = Brush("#0D1117"),
                Foreground = Brush("#FFFFFF"),
                BorderBrush = Brush("#30363D"),
                Padding = new Thickness(8, 6, 8, 6)
            };
            if (restored != null && !restored.StreamKey.IsEmpty)
                _key.Password = restored.StreamKey.Reveal();

            _note = new TextBlock
            {
                Foreground = Brush("#8B949E"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            };

            var removeButton = new Button
            {
                Content = "Retirer",
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = Brush("#21262D"),
                Foreground = Brush("#F0F6FC"),
                BorderBrush = Brush("#30363D"),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 6, 0, 0)
            };
            removeButton.Click += (_, _) => remove(this);

            var stack = new StackPanel();
            stack.Children.Add(Label("Nom"));
            stack.Children.Add(_name);
            stack.Children.Add(Label("Plateforme"));
            stack.Children.Add(_platform);
            stack.Children.Add(Label("Adresse d'ingestion, sans la clé"));
            stack.Children.Add(_url);
            stack.Children.Add(Label("Clé de flux"));
            stack.Children.Add(_key);
            stack.Children.Add(_note);
            stack.Children.Add(removeButton);

            Root = new Border
            {
                Background = Brush("#161B22"),
                BorderBrush = Brush("#30363D"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 8),
                Child = stack
            };
            ApplySuggestion(restored == null);
        }

        public Border Root { get; }

        public bool IsActive { get; set; } = true;

        public ExternalDestination ToDestination()
        {
            var platform = (_platform.SelectedItem as ComboBoxItem)?.Tag is ExternalPlatform value
                ? value
                : ExternalPlatform.Custom;
            return new ExternalDestination
            {
                Id = _id,
                DisplayName = _name.Text.Trim(),
                Platform = platform,
                IngestUrl = _url.Text.Trim(),
                StreamKey = new StreamKey(_key.Password)
            };
        }

        private void ApplySuggestion(bool fillEmptyUrl)
        {
            var platform = (_platform.SelectedItem as ComboBoxItem)?.Tag is ExternalPlatform value
                ? value
                : ExternalPlatform.Custom;
            _note.Text = BroadcastModeText.PlatformWarning(platform) ?? "Destination personnalisée. Seul un récepteur local est exercé par les tests.";
            if (!fillEmptyUrl || _url.Text.Length > 0)
                return;
            var suggestion = BroadcastModeText.SuggestedIngest(platform);
            if (!string.IsNullOrWhiteSpace(suggestion))
                _url.Text = suggestion;
        }

        private static int PlatformIndex(ExternalPlatform platform) => platform switch
        {
            ExternalPlatform.YouTube => 0,
            ExternalPlatform.Facebook => 1,
            ExternalPlatform.TikTok => 2,
            _ => 3
        };

        private static ComboBoxItem Item(string label, ExternalPlatform platform) =>
            new() { Content = label, Tag = platform };

        private static TextBox Field(string text) => new()
        {
            Text = text,
            Background = Brush("#0D1117"),
            Foreground = Brush("#FFFFFF"),
            BorderBrush = Brush("#30363D"),
            Padding = new Thickness(8, 6, 8, 6)
        };

        private static TextBlock Label(string text) => new()
        {
            Text = text,
            Foreground = Brush("#8B949E"),
            FontSize = 11,
            Margin = new Thickness(0, 6, 0, 2)
        };

        private static SolidColorBrush Brush(string hex) =>
            (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
    }
}
