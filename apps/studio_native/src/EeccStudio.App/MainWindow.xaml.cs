using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EeccStudio.Core.Media;

namespace EeccStudio
{
    public partial class MainWindow : Window
    {
        private readonly StudioLinkController _link = new();
        private readonly DispatcherTimer _measureTimer;
        private WriteableBitmap? _preview;
        private bool _playbackMuted;

        public MainWindow()
        {
            InitializeComponent();
            _link.StatusChanged += OnLinkStatus;
            _link.VideoFrame += OnVideoFrame;
            _measureTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _measureTimer.Tick += (_, _) => StatusMeasuredText.Text = _link.MeasuredSummary;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusServerText.Text = "Aucun téléphone appairé. Le QR se génère avec le bouton d'appairage.";
                StatusMeasuredText.Text = _link.MeasuredSummary;
                _measureTimer.Start();
                File.WriteAllText(
                    Path.Combine(AppContext.BaseDirectory, "startup.ready"),
                    DateTime.UtcNow.ToString("o"));
                App.WriteLog("MainWindow loaded");
            }
            catch (Exception ex)
            {
                App.WriteLog(ex.ToString());
                StatusServerText.Text = "Erreur au démarrage";
                if (!App.IsSmokeTest)
                {
                    MessageBox.Show(
                        ex.ToString(),
                        "EECC Studio — erreur au démarrage",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private async void BtnPairQr_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _link.EnsureStartedAsync();
            }
            catch (Exception ex)
            {
                StatusServerText.Text = "Signalisation indisponible (" + ex.GetType().Name + ")";
                App.WriteLog("signaling start " + ex.GetType().Name);
                if (!App.IsSmokeTest)
                {
                    MessageBox.Show(
                        "Le port " + StudioLinkController.SignalingPort + " n'a pas pu être ouvert.",
                        "EECC Studio — appairage",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                return;
            }

            var window = new PairingWindow(_link) { Owner = this };
            window.ShowDialog();
        }

        private void BtnMutePlayback_Click(object sender, RoutedEventArgs e)
        {
            _playbackMuted = !_playbackMuted;
            _link.SetPlaybackMuted(_playbackMuted);
            BtnMutePlayback.Content = _playbackMuted ? "Rétablir le son reçu" : "Couper le son reçu";
            StatusMeasuredText.Text = _link.MeasuredSummary;
        }

        private void BtnTallyProgram_Click(object sender, RoutedEventArgs e) => _link.TrySetTally("program");

        private void BtnTallyPreview_Click(object sender, RoutedEventArgs e) => _link.TrySetTally("preview");

        private void BtnTallyStandby_Click(object sender, RoutedEventArgs e) => _link.TrySetTally("standby");

        private void BtnUnwired_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string message)
                StatusServerText.Text = message;
        }

        private void OnLinkStatus(string status)
        {
            Dispatcher.BeginInvoke(() =>
            {
                StatusServerText.Text = status;
                StatusMeasuredText.Text = _link.MeasuredSummary;
            });
        }

        private void OnVideoFrame(DecodedVideoFrame frame)
        {
            var stride = frame.Stride > 0 ? frame.Stride : frame.Width * 3;
            if (frame.PixelFormat != "Bgr" || frame.Bgr.Length < stride * frame.Height) return;
            Dispatcher.BeginInvoke(() =>
            {
                if (_preview == null || _preview.PixelWidth != frame.Width || _preview.PixelHeight != frame.Height)
                    _preview = new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Bgr24, null);
                _preview.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), frame.Bgr, stride, 0);
                PreviewImage.Source = _preview;
                PreviewPlaceholder.Visibility = Visibility.Collapsed;
                StatusMeasuredText.Text = _link.MeasuredSummary;
            });
        }

        protected override async void OnClosed(EventArgs e)
        {
            _measureTimer.Stop();
            _link.StatusChanged -= OnLinkStatus;
            _link.VideoFrame -= OnVideoFrame;
            await _link.DisposeAsync();
            base.OnClosed(e);
        }
    }
}
