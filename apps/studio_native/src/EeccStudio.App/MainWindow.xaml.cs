using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EeccStudio.Core.Media;
using EeccStudio.Core.Production;

namespace EeccStudio
{
    public partial class MainWindow : Window
    {
        private readonly StudioLinkController _link = new();
        private readonly DispatcherTimer _measureTimer;
        private readonly DispatcherTimer _programTimer;
        private readonly string _scenePath;
        private ProgramDirector _director;
        private CameraFrame? _camera;
        private readonly List<byte> _pcm = new();
        private FfmpegProgramSink? _recording;
        private FfmpegProgramSink? _publishing;
        private string? _recordDirectory;
        private bool _loadingScenes;
        private bool _playbackMuted;

        public MainWindow()
        {
            InitializeComponent();
            _scenePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EECC Studio", "scenes.json");
            _director = new ProgramDirector(SceneStore.LoadOrCreate(_scenePath));
            _link.StatusChanged += OnLinkStatus;
            _link.VideoFrame += OnVideoFrame;
            _link.PcmDecoded += OnPcm;
            _measureTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _measureTimer.Tick += (_, _) => StatusMeasuredText.Text = _link.MeasuredSummary;
            _programTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            _programTimer.Tick += (_, _) => PaintProgram();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusServerText.Text = "Aucun téléphone appairé. Le QR se génère avec le bouton d'appairage.";
                StatusMeasuredText.Text = _link.MeasuredSummary;
                ReloadScenes();
                _measureTimer.Start();
                _programTimer.Start();
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

        private void MenuQuit_Click(object sender, RoutedEventArgs e) => Close();

        private void MenuHelp_Click(object sender, RoutedEventArgs e)
        {
            if (App.IsSmokeTest) return;
            MessageBox.Show(
                "EECC Studio affiche le QR, reçoit l'image et le son du téléphone, coupe vers le programme, enregistre un fichier local et peut encoder ce programme en direct vers une adresse RTMP.\n\n" +
                "Le mixage, le plein écran et les profils ne sont pas branchés. La diffusion n'est pas lancée toute seule.",
                "À propos d'EECC Studio",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

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
            _camera = new CameraFrame(frame.Bgr.ToArray(), frame.Width, frame.Height, stride);
        }

        private void OnPcm(short[] pcm)
        {
            var bytes = new byte[pcm.Length * 2];
            Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);
            lock (_pcm)
            {
                _pcm.AddRange(bytes);
                if (_pcm.Count > 16000) _pcm.RemoveRange(0, _pcm.Count - 16000);
            }
        }

        private void PaintProgram()
        {
            var preview = _director.Render(_director.Preview, _camera, FfmpegProgramSink.Width, FfmpegProgramSink.Height);
            var program = _director.RenderProgram(_camera, FfmpegProgramSink.Width, FfmpegProgramSink.Height);
            Show(PreviewImage, preview);
            Show(ProgramImage, program);
            PreviewPlaceholder.Visibility = Visibility.Collapsed;
            ProgramStatus.Visibility = Visibility.Collapsed;
            if (_recording == null && _publishing == null) return;
            var audio = new byte[FfmpegProgramSink.SamplesPerFrame * 2];
            lock (_pcm)
            {
                var count = Math.Min(audio.Length, _pcm.Count);
                if (count > 0)
                {
                    _pcm.CopyTo(0, audio, 0, count);
                    _pcm.RemoveRange(0, count);
                }
            }
            if (_recording != null)
            {
                try { _recording.Push(program, audio); }
                catch (Exception ex)
                {
                    StatusServerText.Text = "Enregistrement interrompu (" + ex.GetType().Name + ")";
                    _recording = null;
                }
            }
            if (_publishing != null)
            {
                try { _publishing.Push(program, audio); }
                catch (Exception ex)
                {
                    StatusServerText.Text = "Diffusion interrompue (" + ex.GetType().Name + ")";
                    _publishing = null;
                }
            }
        }

        private static void Show(Image target, byte[] bgr)
        {
            var bitmap = new WriteableBitmap(FfmpegProgramSink.Width, FfmpegProgramSink.Height, 96, 96, PixelFormats.Bgr24, null);
            bitmap.WritePixels(new Int32Rect(0, 0, FfmpegProgramSink.Width, FfmpegProgramSink.Height), bgr, FfmpegProgramSink.Width * 3, 0);
            target.Source = bitmap;
        }

        private void ReloadScenes()
        {
            _loadingScenes = true;
            SceneList.Items.Clear();
            foreach (var scene in _director.Document.Scenes)
            {
                SceneList.Items.Add(new ListBoxItem { Content = scene.Name + (scene.Source == SceneSourceKind.Camera ? " — caméra" : " — fond"), Tag = scene.Id });
            }
            _loadingScenes = false;
            SourceText.Text = _director.Preview?.Source == SceneSourceKind.Camera
                ? "Source réelle : image reçue de EECC Caméra. Sans image, le cadre reste vide."
                : "Source réelle : fond de couleur généré dans Studio.";
        }

        private void SceneList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingScenes || SceneList.SelectedItem is not ListBoxItem item || item.Tag is not string id) return;
            _director.SelectPreview(id);
            SceneStore.Save(_scenePath, _director.Document);
            ReloadScenes();
        }

        private void BtnCut_Click(object sender, RoutedEventArgs e)
        {
            _director.Cut();
            SceneStore.Save(_scenePath, _director.Document);
            StatusServerText.Text = "Programme coupé sur « " + (_director.Program?.Name ?? "aucune") + " ».";
        }

        private void BtnDissolve_Click(object sender, RoutedEventArgs e)
        {
            var current = _director.RenderProgram(_camera, FfmpegProgramSink.Width, FfmpegProgramSink.Height);
            _director.BeginDissolve(current, 10);
            SceneStore.Save(_scenePath, _director.Document);
            StatusServerText.Text = "Fondu vers « " + (_director.Program?.Name ?? "aucune") + " ».";
        }

        private void MenuNewCameraScene_Click(object sender, RoutedEventArgs e) => AddScene(SceneSourceKind.Camera, "Caméra");

        private void MenuNewColorScene_Click(object sender, RoutedEventArgs e) => AddScene(SceneSourceKind.Color, "Fond");

        private void AddScene(SceneSourceKind kind, string name)
        {
            var scene = new StudioScene { Name = name, Source = kind };
            _director.Document.Scenes.Add(scene);
            _director.SelectPreview(scene.Id);
            SceneStore.Save(_scenePath, _director.Document);
            ReloadScenes();
        }

        private void MenuDeleteScene_Click(object sender, RoutedEventArgs e)
        {
            var id = _director.Document.PreviewSceneId;
            if (id == null || _director.Document.Scenes.Count < 2) return;
            _director.Document.Scenes.RemoveAll(scene => scene.Id == id);
            _director.Document.PreviewSceneId = _director.Document.Scenes[0].Id;
            SceneStore.Save(_scenePath, _director.Document);
            ReloadScenes();
        }

        private void MenuRecordStart_Click(object sender, RoutedEventArgs e)
        {
            var ffmpeg = FfmpegProgramSink.Locate(AppContext.BaseDirectory);
            if (ffmpeg == null)
            {
                StatusServerText.Text = "Enregistrement impossible : FFmpeg est introuvable.";
                return;
            }
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (!App.IsSmokeTest && dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var folder = App.IsSmokeTest ? Path.GetTempPath() : dialog.SelectedPath;
            try
            {
                _recordDirectory = Path.Combine(folder, "EECC-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                _recording = FfmpegProgramSink.BeginRecording(ffmpeg, _recordDirectory);
                StatusServerText.Text = "Enregistrement du programme en cours.";
            }
            catch (Exception ex)
            {
                StatusServerText.Text = "Enregistrement refusé (" + ex.GetType().Name + ")";
            }
        }

        private async void MenuRecordStop_Click(object sender, RoutedEventArgs e)
        {
            if (_recording == null) { StatusServerText.Text = "Aucun enregistrement en cours."; return; }
            var sink = _recording;
            _recording = null;
            try
            {
                var file = await sink.FinalizeRecordingAsync();
                StatusServerText.Text = file == null ? "Fichier non créé." : "Fichier finalisé.";
            }
            catch (Exception ex)
            {
                StatusServerText.Text = "Finalisation échouée (" + ex.GetType().Name + ")";
            }
        }

        private async void MenuStreamStart_Click(object sender, RoutedEventArgs e)
        {
            if (_publishing != null)
            {
                StatusServerText.Text = "Une diffusion est déjà en cours.";
                return;
            }
            var url = AskStreamUrl();
            if (string.IsNullOrWhiteSpace(url)) return;
            var ffmpeg = FfmpegProgramSink.Locate(AppContext.BaseDirectory);
            if (ffmpeg == null) { StatusServerText.Text = "Diffusion impossible : FFmpeg est introuvable."; return; }
            var encoder = await FfmpegProgramSink.ResolveH264EncoderAsync(ffmpeg);
            if (encoder == null)
            {
                StatusServerText.Text = "Diffusion impossible : aucun encodeur H.264 redistribuable (libx264 ou h264_mf).";
                return;
            }
            try
            {
                var directory = Path.Combine(Path.GetTempPath(), "eecc-live-" + Guid.NewGuid().ToString("N"));
                _publishing = await FfmpegProgramSink.BeginRtmpAsync(ffmpeg, directory, url, CancellationToken.None);
                StatusServerText.Text = "Diffusion en cours (" + encoder + "). Le programme est encodé au fur et à mesure.";
            }
            catch (Exception ex)
            {
                _publishing = null;
                StatusServerText.Text = "Diffusion non démarrée (" + ex.GetType().Name + ")";
            }
        }

        private async void MenuStreamStop_Click(object sender, RoutedEventArgs e)
        {
            var sink = _publishing;
            _publishing = null;
            if (sink == null)
            {
                StatusServerText.Text = "Aucune diffusion en cours.";
                return;
            }
            await sink.StopAsync();
            StatusServerText.Text = sink.Status == "failed" ? "Diffusion interrompue par l'encodeur." : "Diffusion arrêtée.";
        }

        private string? AskStreamUrl()
        {
            var stored = LoadStreamUrl();
            var box = new PasswordBox { Width = 360 };
            if (!string.IsNullOrEmpty(stored)) box.Password = stored;
            var window = new Window { Title = "Adresse RTMP", Width = 420, Height = 140, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, Content = box };
            box.KeyDown += (_, args) => { if (args.Key == System.Windows.Input.Key.Enter) window.DialogResult = true; };
            if (window.ShowDialog() != true) return null;
            SaveStreamUrl(box.Password);
            return box.Password;
        }

        private static string StreamFile() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EECC Studio", "stream-target.bin");

        private static void SaveStreamUrl(string url)
        {
            var file = StreamFile();
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllBytes(file, ProtectedData.Protect(Encoding.UTF8.GetBytes(url), null, DataProtectionScope.CurrentUser));
        }

        private static string? LoadStreamUrl()
        {
            var file = StreamFile();
            if (!File.Exists(file)) return null;
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(file), null, DataProtectionScope.CurrentUser)); }
            catch { return null; }
        }

        protected override async void OnClosed(EventArgs e)
        {
            _measureTimer.Stop();
            _programTimer.Stop();
            if (_publishing != null) await _publishing.DisposeAsync();
            _link.StatusChanged -= OnLinkStatus;
            _link.VideoFrame -= OnVideoFrame;
            _link.PcmDecoded -= OnPcm;
            await _link.DisposeAsync();
            base.OnClosed(e);
        }
    }
}
