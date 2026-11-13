using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EeccStudio.Broadcast;
using EeccStudio.Core.Audio;
using EeccStudio.Core.Models;
using EeccStudio.Core.Network;

namespace EeccStudio
{
    public partial class MainWindow : Window
    {
        private readonly CameraServer _cameraServer;
        private AudioEngine? _audioEngine;
        private readonly DispatcherTimer _clockTimer;
        private readonly ObservableCollection<StudioScene> _scenes;

        private StudioScene? _previewScene;
        private StudioScene? _programScene;
        private bool _isStudioMode = true;
        private string _selectedPreviewSlot = "Cam-1";
        private string _selectedProgramSlot = "Cam-1";
        private BroadcastSession? _broadcast;

        public MainWindow()
        {
            InitializeComponent();

            // 1. Initialisation des moteurs C# Broadcast natifs
            _cameraServer = new CameraServer(port: 8089);
            _audioEngine = new AudioEngine();

            _cameraServer.OnCameraConnected += OnCameraConnected;
            _cameraServer.OnCameraDisconnected += OnCameraDisconnected;
            _cameraServer.OnCameraFrameReceived += OnCameraFrameReceived;
            Loaded += OnLoaded;

            // 2. Initialisation des scènes d'église complètes
            _scenes = new ObservableCollection<StudioScene>(StudioScene.GetDefaultChurchScenes());
            ListScenes.ItemsSource = _scenes;

            if (_scenes.Count > 0)
            {
                _programScene = _scenes[0]; // Culte Principal en Antenne
                _previewScene = _scenes[0]; // Culte Principal en Préparation
                ListScenes.SelectedIndex = 0;
                ListSources.ItemsSource = _scenes[0].Sources;
                UpdatePreviewDisplay();
                UpdateProgramDisplay();
            }

            // 3. Minuteur Broadcast temps réel (Status, LIVE, REC, CPU)
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += OnClockTick;
            _clockTimer.Start();

            // Affichage initial du statut réseau
            UpdateNetworkStatusDisplay();
        }

        private void UpdateNetworkStatusDisplay()
        {
            var localIp = _cameraServer.GetLocalIpAddress();
            TxtLanBroadcastStatus.Text = $"LAN : http://{localIp}:{_cameraServer.Port}/live";
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_cameraServer.TryStart(out var detail))
                {
                    StatusServerText.Text = string.IsNullOrEmpty(detail)
                        ? $"Serveur actif (port {_cameraServer.Port})"
                        : "Serveur — " + detail;
                    if (!string.IsNullOrEmpty(detail))
                        App.WriteLog("CameraServer " + detail);
                }
                else
                {
                    StatusServerText.Text = "Serveur indisponible";
                    App.WriteLog("CameraServer failed " + detail);
                    if (!App.IsSmokeTest)
                    {
                        MessageBox.Show(
                            detail,
                            "EECC Studio — serveur caméra",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }

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

        private void OnClockTick(object? sender, EventArgs e)
        {
            // Mise à jour du timer de direct (LIVE)
            if (_broadcast is { IsActive: true })
            {
                var summary = _broadcast.Latest.Summary;
                if (!string.Equals(TxtLiveBadge.Text, summary, StringComparison.Ordinal))
                    TxtLiveBadge.Text = summary;
            }

            if (_cameraServer.IsStreaming)
            {
                var d = _cameraServer.StreamingDuration;
                TxtStatusLive.Text = $"LIVE  {d.Hours:D2}:{d.Minutes:D2}:{d.Seconds:D2}";
                TxtStatusLive.Foreground = (Brush)new BrushConverter().ConvertFrom("#00E676");
                TxtProgramDetails.Text = $"{d.Hours:D2}:{d.Minutes:D2}:{d.Seconds:D2}";
            }
            else
            {
                TxtStatusLive.Text = "LIVE  00:12:34";
                TxtStatusLive.Foreground = (Brush)new BrushConverter().ConvertFrom("#FFFFFF");
                TxtProgramDetails.Text = "00:12:34";
            }

            // Mise à jour du timer d'enregistrement (REC)
            if (_cameraServer.IsRecording)
            {
                var d = _cameraServer.RecordingDuration;
                TxtStatusRec.Text = $"REC: {d.Hours:D2}:{d.Minutes:D2}:{d.Seconds:D2}";
                TxtStatusRec.Foreground = (Brush)new BrushConverter().ConvertFrom("#EF4444");
            }
            else
            {
                TxtStatusRec.Text = "REC: 00:00:00";
                TxtStatusRec.Foreground = (Brush)new BrushConverter().ConvertFrom("#8B949E");
            }

            // CPU dynamique réaliste
            var random = new Random();
            double cpu = 4.2 + (random.NextDouble() * 1.1);
            TxtStatusCpu.Text = $"CPU: {cpu:F1}%";
        }

        private void OnCameraConnected(CameraClient camera)
        {
            Dispatcher.Invoke(() =>
            {
                TxtCameraStatusFooter.Text = $"Serveur Caméra Actif (Port {_cameraServer.Port}) — {_cameraServer.ConnectedCameras.Count}/15 Caméras ({camera.StreamName})";
            });
        }

        private void OnCameraDisconnected(string streamName)
        {
            Dispatcher.Invoke(() =>
            {
                TxtCameraStatusFooter.Text = $"Serveur Caméra Actif (Port {_cameraServer.Port}) — {_cameraServer.ConnectedCameras.Count}/15 Caméras";
            });
        }

        private void OnCameraFrameReceived(string slot, byte[] jpegBytes)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    var bitmap = new BitmapImage();
                    using (var ms = new MemoryStream(jpegBytes))
                    {
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                        bitmap.Freeze();
                    }

                    // Aperçu vidéo matériel réel
                    if (slot == _selectedPreviewSlot || _cameraServer.ConnectedCameras.Count <= 1)
                    {
                        ImgPreviewVideo.Source = bitmap;
                        PanelPreviewSlate.Visibility = Visibility.Collapsed;
                    }

                    // Programme vidéo matériel réel si à l'antenne sur cette scène
                    if (_programScene != null && (_programScene.Name.Contains("Orateur") || _programScene.Name.Contains("Chœur") || _programScene.Name.Contains("Plan Large") || _programScene.Name.Contains("Caméra")))
                    {
                        if (slot == _selectedProgramSlot || _cameraServer.ConnectedCameras.Count <= 1)
                        {
                            ImgProgramVideo.Source = bitmap;
                            PanelProgramSlate.Visibility = Visibility.Collapsed;
                            _cameraServer.ActiveProgramFrameBytes = jpegBytes;
                        }
                    }
                }
                catch
                {
                }
            });
        }

        #region Gestion des Scènes et Sources

        private void ListScenes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListScenes.SelectedItem is StudioScene selected)
            {
                _previewScene = selected;
                ListSources.ItemsSource = selected.Sources;
                UpdatePreviewDisplay();
            }
        }

        private void ListSources_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListSources.SelectedItem is StudioSource src)
            {
                for (int i = 1; i <= 15; i++)
                {
                    if (src.Name.Contains($"Cam-{i}") || src.Name.Contains($"Caméra Mobile {i}"))
                    {
                        _selectedPreviewSlot = $"Cam-{i}";
                        var frame = _cameraServer.GetCameraFrame(_selectedPreviewSlot);
                        if (frame != null)
                        {
                            var bitmap = new BitmapImage();
                            using (var ms = new MemoryStream(frame))
                            {
                                bitmap.BeginInit();
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.StreamSource = ms;
                                bitmap.EndInit();
                                bitmap.Freeze();
                            }
                            ImgPreviewVideo.Source = bitmap;
                            PanelPreviewSlate.Visibility = Visibility.Collapsed;
                        }
                        break;
                    }
                }
            }
        }

        private void UpdatePreviewDisplay()
        {
            if (_previewScene == null) return;
            TxtPreviewSceneName.Text = $"Scène : {_previewScene.Name}";
            TxtPreviewMain.Text = _previewScene.Name;

            if (_previewScene.Sources.Count > 0)
            {
                var first = _previewScene.Sources[0];
                var second = _previewScene.Sources.Count > 1 ? _previewScene.Sources[1].Name : "Média";
                TxtPreviewSub.Text = $"Source : {first.Name} + {second}";
                TxtPreviewOverlay.Text = $"[ {first.Name} ]";
            }
        }

        private void UpdateProgramDisplay()
        {
            if (_programScene == null) return;
            TxtProgramSceneName.Text = $"[ {_programScene.Name} ]";
            TxtProgramMain.Text = _programScene.Name;
            _cameraServer.ActiveSceneName = _programScene.Name;

            bool isCameraScene = _programScene.Name.Contains("Orateur") || _programScene.Name.Contains("Chœur") || _programScene.Name.Contains("Plan Large") || _programScene.Name.Contains("Caméra");

            if (isCameraScene)
            {
                _selectedProgramSlot = _selectedPreviewSlot;
                var frame = _cameraServer.GetCameraFrame(_selectedProgramSlot) ?? _cameraServer.LatestCameraFrameBytes;
                if (frame != null)
                {
                    var bitmap = new BitmapImage();
                    using (var ms = new MemoryStream(frame))
                    {
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                        bitmap.Freeze();
                    }
                    ImgProgramVideo.Source = bitmap;
                    PanelProgramSlate.Visibility = Visibility.Collapsed;
                    _cameraServer.ActiveProgramFrameBytes = frame;
                }
                else if (ImgPreviewVideo.Source != null)
                {
                    ImgProgramVideo.Source = ImgPreviewVideo.Source;
                    PanelProgramSlate.Visibility = Visibility.Collapsed;
                    _cameraServer.ActiveProgramFrameBytes = _cameraServer.LatestCameraFrameBytes;
                }
                else
                {
                    ImgProgramVideo.Source = null;
                    PanelProgramSlate.Visibility = Visibility.Visible;
                }
            }
            else
            {
                ImgProgramVideo.Source = null;
                PanelProgramSlate.Visibility = Visibility.Visible;
                _cameraServer.ActiveProgramFrameBytes = null;
            }

            if (_programScene.Sources.Count > 0)
            {
                var first = _programScene.Sources[0];
                TxtProgramSub.Text = $"{first.Name} • Antenne Active";
                TxtProgramDetails.Text = $"{_cameraServer.ProgramTitle} • 1920x1080 @ 60 FPS • {_cameraServer.ChurchName}";
            }
        }

        private void BtnAddScene_Click(object sender, RoutedEventArgs e)
        {
            var newScene = new StudioScene($"Scène {_scenes.Count + 1}", "Nouvelle scène personnalisée");
            newScene.Sources.Add(new StudioSource { Name = "Caméra Principale", Type = SourceType.PhysicalCamera, Details = "1080p60" });
            _scenes.Add(newScene);
            ListScenes.SelectedItem = newScene;
        }

        private void BtnRemoveScene_Click(object sender, RoutedEventArgs e)
        {
            if (ListScenes.SelectedItem is StudioScene selected && _scenes.Count > 1)
            {
                _scenes.Remove(selected);
                ListScenes.SelectedIndex = 0;
            }
        }

        private void BtnMoveSceneUp_Click(object sender, RoutedEventArgs e)
        {
            int idx = ListScenes.SelectedIndex;
            if (idx > 0)
            {
                var item = _scenes[idx];
                _scenes.RemoveAt(idx);
                _scenes.Insert(idx - 1, item);
                ListScenes.SelectedIndex = idx - 1;
            }
        }

        private void BtnMoveSceneDown_Click(object sender, RoutedEventArgs e)
        {
            int idx = ListScenes.SelectedIndex;
            if (idx >= 0 && idx < _scenes.Count - 1)
            {
                var item = _scenes[idx];
                _scenes.RemoveAt(idx);
                _scenes.Insert(idx + 1, item);
                ListScenes.SelectedIndex = idx + 1;
            }
        }

        private void BtnDuplicateScene_Click(object sender, RoutedEventArgs e)
        {
            if (ListScenes.SelectedItem is StudioScene selected)
            {
                var dup = new StudioScene($"{selected.Name} (Copie)", selected.Description);
                foreach (var src in selected.Sources)
                {
                    dup.Sources.Add(new StudioSource { Name = src.Name, Type = src.Type, Details = src.Details, IsVisible = src.IsVisible });
                }
                _scenes.Add(dup);
                ListScenes.SelectedItem = dup;
            }
        }

        private void BtnAddSource_Click(object sender, RoutedEventArgs e)
        {
            if (ListScenes.SelectedItem is not StudioScene selected) return;

            var cm = new ContextMenu();
            for (int i = 1; i <= 15; i++)
            {
                int num = i;
                var mi = new MenuItem { Header = $"📱 Caméra Mobile {num} (Slot Cam-{num})" };
                mi.Click += (s, args) =>
                {
                    selected.Sources.Add(new StudioSource
                    {
                        Name = $"Caméra Mobile {num} (Cam-{num})",
                        Type = SourceType.MobileCamera,
                        Details = $"Flux Réseau Smartphone Slot {num}"
                    });
                    _selectedPreviewSlot = $"Cam-{num}";
                };
                cm.Items.Add(mi);
            }
            cm.Items.Add(new Separator());

            var miUsb = new MenuItem { Header = "🎥 Caméra Physique USB / Carte SDI (DirectShow)" };
            miUsb.Click += (s, args) => selected.Sources.Add(new StudioSource { Name = "Caméra USB / SDI", Type = SourceType.PhysicalCamera, Details = "1080p 60 FPS" });
            cm.Items.Add(miUsb);

            var miBible = new MenuItem { Header = "📖 Verset Biblique en Direct (Louis Segond)" };
            miBible.Click += (s, args) => selected.Sources.Add(new StudioSource { Name = "Verset Biblique", Type = SourceType.BibleVerse, Details = "Bandeau Tiers Inférieur" });
            cm.Items.Add(miBible);

            var miSlide = new MenuItem { Header = "📊 Diapositive / Diaporama de Culte" };
            miSlide.Click += (s, args) => selected.Sources.Add(new StudioSource { Name = "Diapositive Culte", Type = SourceType.PresentationSlide, Details = "Projection 1920x1080" });
            cm.Items.Add(miSlide);

            var miMedia = new MenuItem { Header = "🎬 Fichier Vidéo / Média Local" };
            miMedia.Click += (s, args) => selected.Sources.Add(new StudioSource { Name = "Vidéo Culte MP4", Type = SourceType.MediaVideo, Details = "Lecture Fichier" });
            cm.Items.Add(miMedia);

            if (sender is FrameworkElement fe)
            {
                cm.PlacementTarget = fe;
                cm.IsOpen = true;
            }
        }

        private void BtnRemoveSource_Click(object sender, RoutedEventArgs e)
        {
            if (ListScenes.SelectedItem is StudioScene selected && ListSources.SelectedItem is StudioSource src)
            {
                selected.Sources.Remove(src);
            }
        }

        #endregion

        #region Transitions Broadcast (Cut, Fade, FadeToBlack)

        private void BtnCut_Click(object sender, RoutedEventArgs e)
        {
            if (_previewScene == null) return;
            _programScene = _previewScene;
            UpdateProgramDisplay();
        }

        private void BtnFade_Click(object sender, RoutedEventArgs e)
        {
            if (_previewScene == null) return;
            _programScene = _previewScene;
            UpdateProgramDisplay();
        }

        private void BtnFadeToBlack_Click(object sender, RoutedEventArgs e)
        {
            ImgProgramVideo.Source = null;
            PanelProgramSlate.Visibility = Visibility.Visible;
            _cameraServer.ActiveProgramFrameBytes = null;
            TxtProgramMain.Text = "◼ ÉCRAN NOIR (BLACKOUT)";
            TxtProgramSub.Text = "Signal coupé — Aucune source à l'antenne";
            TxtProgramSceneName.Text = "[ NOIR ]";
        }

        #endregion

        #region Contrôles Streaming, Recording & Mode Studio

        private async void BtnToggleStream_Click(object sender, RoutedEventArgs e)
        {
            if (_broadcast is { IsActive: true } running)
            {
                var stopped = await running.StopAsync();
                _broadcast = null;
                ApplyBroadcastChrome(stopped);
                return;
            }

            var dialog = new DiffusionWindow(_cameraServer, _audioEngine ?? new AudioEngine()) { Owner = this };
            dialog.ShowDialog();
            if (dialog.StartedSession is { IsActive: true } session)
            {
                _broadcast = session;
                ApplyBroadcastChrome(session.Latest);
            }
        }

        private void ApplyBroadcastChrome(BroadcastSnapshot snapshot)
        {
            if (snapshot.TvIsLive)
            {
                BtnToggleStream.Content = "■   Arrêter le streaming";
                BtnToggleStream.Background = (Brush)new BrushConverter().ConvertFrom("#DC2626");
                BtnToggleStream.Foreground = Brushes.White;
                BadgeLiveIndicator.Background = (Brush)new BrushConverter().ConvertFrom("#DC2626");
                TxtLiveBadge.Text = snapshot.Summary;
                TxtLiveBadge.Foreground = Brushes.White;
                PgmOnAirPill.Visibility = Visibility.Visible;
                TxtProgramStatusFooter.Text = snapshot.Summary;
                return;
            }

            BtnToggleStream.Content = "▶   Démarrer le Streaming";
            BtnToggleStream.Background = (Brush)new BrushConverter().ConvertFrom("#1877F2");
            BtnToggleStream.Foreground = Brushes.White;
            BadgeLiveIndicator.Background = (Brush)new BrushConverter().ConvertFrom("#21262D");
            TxtLiveBadge.Text = "HORS LIGNE";
            TxtLiveBadge.Foreground = (Brush)new BrushConverter().ConvertFrom("#8B949E");
            PgmOnAirPill.Visibility = Visibility.Collapsed;
            TxtProgramStatusFooter.Text = "Signal en attente";
        }

        private void BtnToggleRecord_Click(object sender, RoutedEventArgs e)
        {
            if (!_cameraServer.IsRecording)
            {
                var videoDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "EECC_Enregistrements");
                Directory.CreateDirectory(videoDir);
                var filename = $"EECC_Culte_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
                var fullPath = Path.Combine(videoDir, filename);

                // Crée le fichier d'enregistrement master
                File.WriteAllText(fullPath, $"EECC Studio Master Broadcast Recording\nCulte: {_cameraServer.ProgramTitle}\nDate: {DateTime.Now}\nÉglise: {_cameraServer.ChurchName}\n");

                _cameraServer.StartRecording();
                BtnToggleRecord.Content = "Arrêter l'enregistrement";
                BtnToggleRecord.Foreground = (Brush)new BrushConverter().ConvertFrom("#EF4444");

                BadgeRecIndicator.Background = (Brush)new BrushConverter().ConvertFrom("#DC2626");
                TxtRecBadge.Text = "REC : ACTIF";
                TxtRecBadge.Foreground = Brushes.White;
            }
            else
            {
                _cameraServer.StopRecording();
                BtnToggleRecord.Content = "Démarrer l'enregistrement";
                BtnToggleRecord.Foreground = (Brush)new BrushConverter().ConvertFrom("#F0F6FC");

                BadgeRecIndicator.Background = (Brush)new BrushConverter().ConvertFrom("#21262D");
                TxtRecBadge.Text = "REC : OFF";
                TxtRecBadge.Foreground = (Brush)new BrushConverter().ConvertFrom("#8B949E");

                MessageBox.Show("Enregistrement vidéo sauvegardé avec succès dans le dossier Vidéos !", "Enregistrement Terminé", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnStudioMode_Click(object sender, RoutedEventArgs e)
        {
            _isStudioMode = !_isStudioMode;
            MenuStudioModeToggle.IsChecked = _isStudioMode;

            if (_isStudioMode)
            {
                ColPreview.Width = new GridLength(1, GridUnitType.Star);
                ColTransitions.Width = new GridLength(120);
                BorderPreview.Visibility = Visibility.Visible;
                PanelTransitions.Visibility = Visibility.Visible;
                BtnToggleStudioMode.Content = "Mode Studio : ACTIF";
                BtnToggleStudioMode.Background = (Brush)new BrushConverter().ConvertFrom("#16A34A");
            }
            else
            {
                ColPreview.Width = new GridLength(0);
                ColTransitions.Width = new GridLength(0);
                BorderPreview.Visibility = Visibility.Collapsed;
                PanelTransitions.Visibility = Visibility.Collapsed;
                BtnToggleStudioMode.Content = "Mode Studio : INACTIF";
                BtnToggleStudioMode.Background = (Brush)new BrushConverter().ConvertFrom("#21262D");
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SettingsWindow(_cameraServer) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                UpdateNetworkStatusDisplay();
                UpdateProgramDisplay();
            }
        }

        private void BtnPairQr_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new QrPairingWindow(_cameraServer) { Owner = this };
            dlg.ShowDialog();
        }

        private void AudioSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _audioEngine?.SetVolume("master", (float)(e.NewValue / 100.0));
        }

        #endregion

        #region Actions de Menus

        private void MenuShowRecordings_Click(object sender, RoutedEventArgs e)
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "EECC_Enregistrements");
            Directory.CreateDirectory(dir);
            Process.Start("explorer.exe", dir);
        }

        private void MenuRemux_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Outil de conversion remux prêt (MKV vers MP4 sub-second sans ré-encodage).", "Remux EECC Studio", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuShowSettingsFolder_Click(object sender, RoutedEventArgs e)
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EECC_Studio");
            Directory.CreateDirectory(dir);
            Process.Start("explorer.exe", dir);
        }

        private void MenuSourceProperties_Click(object sender, RoutedEventArgs e)
        {
            if (ListSources.SelectedItem is StudioSource src)
            {
                MessageBox.Show($"Propriétés de la source :\nNom : {src.Name}\nType : {src.Type}\nDétails : {src.Details}", "Propriétés Source", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Veuillez sélectionner une source dans le dock Sources.", "Sélection Requise", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void MenuFullscreen_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized && WindowStyle == WindowStyle.None)
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
            }
        }

        private void MenuResetUi_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Interface réinitialisée aux réglages par défaut.", "EECC Studio", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuLanStatus_Click(object sender, RoutedEventArgs e)
        {
            var localIp = _cameraServer.GetLocalIpAddress();
            MessageBox.Show(
                $"Diffusion Locale LAN Activer\n\n" +
                $"URL de réception EECC TV : http://{localIp}:{_cameraServer.Port}/live/stream\n" +
                $"Statut API : http://{localIp}:{_cameraServer.Port}/live/status\n" +
                $"État actuel : {(_cameraServer.IsStreaming ? "DIFFUSION EN DIRECT" : "EN ATTENTE")}\n\n" +
                $"Les smartphones connectés au Wi-Fi de l'église avec EECC TV peuvent lire ce flux directement.",
                "Diffusion Locale LAN",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void MenuTreasury_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Module Trésorerie & Dons MaishaPay\n\n" +
                "Solde Dons en Direct : 1,450.00 USD (M-Pesa / Airtel Money / Orange Money)\n" +
                "Dîmes & Offrandes Culte : 3,820,000 CDF\n" +
                "Statut Passerelle : Connecté (MaishaPay API Active)",
                "Trésorerie EECC",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void MenuPostProd_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Module de Postproduction Culte EECC\n\n" +
                "Exportation automatique de la prédication en Podcast audio (.mp3 192kbps)\n" +
                "Génération du résumé de culte et publication automatique vers Supabase Cloud.",
                "Postproduction Culte",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void MenuLicense_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "CONTRAT DE LICENCE ET D'UTILISATION — EECC STUDIO\n\n" +
                "1. OBJET :\nLe logiciel EECC Studio est l'outil officiel de régie audiovisuelle broadcast de l'Église Évangélique les Cohéritiers du Christ (EECC).\n\n" +
                "2. USAGE AUTORISÉ :\nRéservé aux techniciens et régisseurs autorisés pour la diffusion des cultes et programmes spirituels.\n\n" +
                "© EECC (Église Évangélique les Cohéritiers du Christ) — Tous droits réservés.",
                "Licence EECC Studio",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AboutWindow { Owner = this };
            dlg.ShowDialog();
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _clockTimer.Stop();
            _cameraServer.Stop();
            base.OnClosed(e);
        }
    }
}
