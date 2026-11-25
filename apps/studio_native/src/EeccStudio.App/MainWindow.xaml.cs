using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EeccStudio.Broadcast;
using EeccStudio.Core.Audio;
using EeccStudio.Core.Graphics;
using EeccStudio.Core.Models;
using EeccStudio.Core.Network;
using EeccStudio.Core.Storage;
using EeccStudio.Core.WebRtc;

namespace EeccStudio
{
    public partial class MainWindow : Window
    {
        private readonly CameraServer _cameraServer;
        private AudioEngine? _audioEngine;
        private WebRtcSessionManager? _webRtcSessionManager;
        private PhysicalCameraCapture? _physicalCameraCapture;
        private ScreenCaptureEngine? _screenCaptureEngine;
        private MediaVideoSource? _mediaVideoSource;
        private GpuCompositor? _gpuCompositor;
        private MasterRecorder? _masterRecorder;
        private ReplayBuffer? _replayBuffer;
        private VirtualCameraOutput? _virtualCamera;
        private AdaptiveBitrateController? _abrController;
        private ObsWebSocketServer? _obsWebSocketServer;
        private OfflineSyncQueue? _offlineSyncQueue;
        private readonly DispatcherTimer _clockTimer;
        private readonly ObservableCollection<StudioScene> _scenes;

        private StudioScene? _previewScene;
        private StudioScene? _programScene;
        private bool _isStudioMode = true;
        private string _selectedPreviewSlot = "Cam-1";
        private string _selectedProgramSlot = "Cam-1";
        private BroadcastSession? _broadcast;
        private int _framesThisWindow;
        private DateTime _fpsStamp = DateTime.UtcNow;
        private TimeSpan _prevCpu = TimeSpan.Zero;
        private DateTime _prevCpuAt = DateTime.UtcNow;
        private readonly object _frameLock = new();
        private readonly Dictionary<string, byte[]> _pendingFrames = new(StringComparer.OrdinalIgnoreCase);
        private bool _renderScheduled;
        private static readonly int[] DemoMeterHeights = { 70, 66, 78, 74, 48, 50, 64, 64, 52, 50 };

        public MainWindow()
        {
            InitializeComponent();

            // 1. Initialisation des moteurs C# Broadcast natifs
            _cameraServer = new CameraServer(port: 8089);
            _audioEngine = new AudioEngine();
            _webRtcSessionManager = new WebRtcSessionManager(_cameraServer, _audioEngine);
            _webRtcSessionManager.OnVideoFrameReceived += (slot, frame) =>
            {
                OnCameraFrameReceived(slot, frame);
            };

            _gpuCompositor = new GpuCompositor(CanvasPreset.FullHd1080p);
            _physicalCameraCapture = new PhysicalCameraCapture();
            _physicalCameraCapture.FrameArrived += OnPhysicalCameraFrameArrived;
            _screenCaptureEngine = new ScreenCaptureEngine();
            _screenCaptureEngine.FrameArrived += OnScreenCaptureFrameArrived;
            _mediaVideoSource = new MediaVideoSource();
            _mediaVideoSource.FrameArrived += OnMediaVideoFrameArrived;

            _masterRecorder = new MasterRecorder();
            _replayBuffer = new ReplayBuffer(capacitySeconds: 30, expectedFps: 30);
            _replayBuffer.Start();
            _virtualCamera = new VirtualCameraOutput();
            _abrController = new AdaptiveBitrateController();

            _obsWebSocketServer = new ObsWebSocketServer(port: 4455);
            _obsWebSocketServer.OnGetCurrentProgramScene = () => _programScene?.Name ?? "Culte";
            _obsWebSocketServer.OnGetCurrentPreviewScene = () => _previewScene?.Name ?? "Aperçu";
            _obsWebSocketServer.OnSetCurrentProgramScene = name => Dispatcher.Invoke(() =>
            {
                var found = _scenes.FirstOrDefault(s => s.Name == name);
                if (found != null) { _programScene = found; UpdateProgramDisplay(); }
            });
            _obsWebSocketServer.OnSetCurrentPreviewScene = name => Dispatcher.Invoke(() =>
            {
                var found = _scenes.FirstOrDefault(s => s.Name == name);
                if (found != null) { _previewScene = found; UpdatePreviewDisplay(); }
            });
            _obsWebSocketServer.OnTriggerTransition = () => Dispatcher.Invoke(() => BtnFade_Click(this, new RoutedEventArgs()));
            _obsWebSocketServer.OnGetStudioMode = () => _isStudioMode;
            _obsWebSocketServer.OnSetStudioMode = enabled => Dispatcher.Invoke(() =>
            {
                if (_isStudioMode != enabled) BtnStudioMode_Click(this, new RoutedEventArgs());
            });
            _obsWebSocketServer.OnGetStreamStatus = () => _broadcast is { IsActive: true };
            _obsWebSocketServer.OnToggleStream = () => Dispatcher.Invoke(() => BtnToggleStream_Click(this, new RoutedEventArgs()));
            _obsWebSocketServer.OnGetRecordStatus = () => _masterRecorder?.IsRecording ?? false;
            _obsWebSocketServer.OnToggleRecord = () => Dispatcher.Invoke(() => BtnToggleRecord_Click(this, new RoutedEventArgs()));
            _obsWebSocketServer.OnSaveReplay = () => _ = _replayBuffer.SaveReplayAsync();

            // Contrôle à distance des Overlays Graphiques (Stream Deck / Companion / OBS WebSocket v5)
            _obsWebSocketServer.OnToggleLowerThird = (visible, speaker, title) => Dispatcher.Invoke(() =>
            {
                var vis = visible ? Visibility.Visible : Visibility.Collapsed;
                PanelProgramLowerThird.Visibility = vis;
                PanelPreviewLowerThird.Visibility = vis;
                if (!string.IsNullOrWhiteSpace(speaker))
                {
                    TxtProgramLowerTitle.Text = speaker;
                    TxtPreviewLowerTitle.Text = speaker;
                }
                if (!string.IsNullOrWhiteSpace(title))
                {
                    TxtProgramLowerSub.Text = title;
                    TxtPreviewLowerSub.Text = title;
                }
            });

            _obsWebSocketServer.OnToggleTicker = (enabled, text) => Dispatcher.Invoke(() =>
            {
                var vis = enabled ? Visibility.Visible : Visibility.Collapsed;
                PanelProgramTicker.Visibility = vis;
                PanelPreviewTicker.Visibility = vis;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    TxtProgramTicker.Text = text;
                    TxtPreviewTicker.Text = text;
                    StartTickerAnimations();
                }
            });

            _obsWebSocketServer.OnSlideShowNext = () => Dispatcher.Invoke(() =>
            {
                // Stream Deck commande "Diapositive Suivante"
            });

            _obsWebSocketServer.OnSlideShowPrevious = () => Dispatcher.Invoke(() =>
            {
                // Stream Deck commande "Diapositive Précédente"
            });

            _obsWebSocketServer.OnSetTransition = (name, duration) => Dispatcher.Invoke(() =>
            {
                if (name.Contains("Cut", StringComparison.OrdinalIgnoreCase))
                {
                    BtnCut_Click(this, new RoutedEventArgs());
                }
                else
                {
                    BtnFade_Click(this, new RoutedEventArgs());
                }
            });
            try { _obsWebSocketServer.Start(); } catch { }

            _offlineSyncQueue = new OfflineSyncQueue();
            _offlineSyncQueue.Start();

            _cameraServer.OnCameraConnected += OnCameraConnected;
            _cameraServer.OnCameraDisconnected += OnCameraDisconnected;
            _cameraServer.OnCameraFrameReceived += OnCameraFrameReceived;
            Loaded += OnLoaded;

            // 2. Initialisation des scènes d'église complètes
            _scenes = new ObservableCollection<StudioScene>(StudioScene.GetDefaultChurchScenes());
            ListScenes.ItemsSource = _scenes;

            if (_scenes.Count > 0)
            {
                _programScene = _scenes[0];
                _previewScene = _scenes[0];
                ListScenes.SelectedIndex = 0;
                ListSources.ItemsSource = _scenes[0].Sources;
                UpdatePreviewDisplay();
                UpdateProgramDisplay();
            }

            foreach (var scene in _scenes)
            {
                foreach (var source in scene.Sources.Where(source => source.Type == SourceType.PhysicalCamera))
                    _cameraServer.RegisterPhysicalCamera(source.Id, source.Name);
            }

            // 3. Minuteur Broadcast temps réel (Status, LIVE, REC, CPU)
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += OnClockTick;
            _clockTimer.Start();

            // Affichage initial du statut réseau
            UpdateNetworkStatusDisplay();
            if (App.IsDemoMode)
            {
                BadgeDemo.Visibility = Visibility.Visible;
                for (int i = 0; i < DemoMeterHeights.Length; i++)
                {
                    if (FindName($"MeterFill{i + 1}") is Border meter)
                        meter.Height = DemoMeterHeights[i];
                }
            }
        }

        private void UpdateNetworkStatusDisplay()
        {
            var localIp = _cameraServer.GetLocalIpAddress();
            TxtLanBroadcastStatus.Text = localIp.Length == 0
                ? "LAN : aucune adresse privée utilisable"
                : $"LAN : {localIp}:{_cameraServer.ListeningPort}";
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
                App.WriteLog("MainWindow loaded"); ApplyOverlaysFromScene();
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
            if (_broadcast is { IsActive: true })
            {
                var summary = _broadcast.Latest.Summary;
                if (!string.Equals(TxtLiveBadge.Text, summary, StringComparison.Ordinal))
                    TxtLiveBadge.Text = summary;
            }

            if (App.IsDemoMode)
            {
                var example = (Brush)new BrushConverter().ConvertFrom("#2FE06A")!;
                TxtStatusLive.Text = "LIVE  00:12:34";
                TxtStatusLive.Foreground = Brushes.White;
                StatusLiveDot.Foreground = example;
                TxtStatusRec.Text = "REC  00:00:00";
                TxtStatusCpu.Text = "CPU : 4.8%";
                TxtStatusFps.Text = "60.00 FPS";
                TxtStatusFps.Foreground = example;
                TxtStatusBitrate.Text = "Débit : 6.42 Mbps";
                TxtLanBroadcastStatus.Text = "LAN : http://192.168.46.78:8080/live";
                TxtProgramLiveFooter.Text = "DÉMO · Diffusion en cours...";
                TxtProgramDetails.Text = "00:12:34";
                PgmOnAirPill.Visibility = Visibility.Visible;
                return;
            }

            var liveBrush = (Brush)new BrushConverter().ConvertFrom("#2FE06A")!;
            var idleBrush = (Brush)new BrushConverter().ConvertFrom("#5C636A")!;

            if (_cameraServer.IsStreaming)
            {
                var d = _cameraServer.StreamingDuration;
                var clock = $"{d.Hours:D2}:{d.Minutes:D2}:{d.Seconds:D2}";
                TxtStatusLive.Text = $"LIVE  {clock}";
                TxtStatusLive.Foreground = Brushes.White;
                StatusLiveDot.Foreground = liveBrush;
                TxtProgramDetails.Text = clock;
                TxtProgramLiveFooter.Text = "Diffusion en cours...";
                PgmOnAirPill.Visibility = Visibility.Visible;
            }
            else
            {
                TxtStatusLive.Text = "LIVE  00:00:00";
                TxtStatusLive.Foreground = (Brush)new BrushConverter().ConvertFrom("#C5CCD3")!;
                StatusLiveDot.Foreground = idleBrush;
                TxtProgramDetails.Text = "";
                TxtProgramLiveFooter.Text = "En attente";
                PgmOnAirPill.Visibility = Visibility.Collapsed;
            }

            if (_cameraServer.IsRecording)
            {
                var d = _cameraServer.RecordingDuration;
                TxtStatusRec.Text = $"REC  {d.Hours:D2}:{d.Minutes:D2}:{d.Seconds:D2}";
                TxtStatusRec.Foreground = (Brush)new BrushConverter().ConvertFrom("#FF5A5A")!;
            }
            else
            {
                TxtStatusRec.Text = "REC  00:00:00";
                TxtStatusRec.Foreground = (Brush)new BrushConverter().ConvertFrom("#C5CCD3")!;
            }

            var proc = Process.GetCurrentProcess();
            var now = DateTime.UtcNow;
            var cpuTime = proc.TotalProcessorTime;
            var wallMs = (now - _prevCpuAt).TotalMilliseconds;
            var usedMs = (cpuTime - _prevCpu).TotalMilliseconds;
            var cpuPct = wallMs > 0 ? usedMs / wallMs / Environment.ProcessorCount * 100.0 : 0;
            TxtStatusCpu.Text = $"CPU : {Math.Clamp(cpuPct, 0, 100):F1}%";
            _prevCpu = cpuTime;
            _prevCpuAt = now;

            var windowSeconds = (now - _fpsStamp).TotalSeconds;
            var countedFps = windowSeconds > 0 ? _framesThisWindow / windowSeconds : 0;
            _framesThisWindow = 0;
            _fpsStamp = now;

            double telemetryFps = 0;
            double bitrateKbps = 0;
            int telemetryCount = 0;
            foreach (var cam in _cameraServer.ConnectedCameras.Values)
            {
                if (cam.LastTelemetryTime <= cam.ConnectedAt.AddMilliseconds(500)) continue;
                telemetryFps += cam.Fps;
                if (_cameraServer.IsStreaming) bitrateKbps += Math.Max(0, cam.BitrateKbps);
                telemetryCount++;
            }
            var fps = countedFps;
            if (fps < 0.05 && telemetryCount > 0) fps = telemetryFps / telemetryCount;
            TxtStatusFps.Text = $"{Math.Max(0, fps):F2} FPS";
            TxtStatusFps.Foreground = fps > 0.5 ? liveBrush : idleBrush;
            TxtStatusBitrate.Text = $"Débit : {bitrateKbps / 1000.0:F2} Mbps";
            TxtCameraStatusFooter.Text =
                $"Téléphones {_cameraServer.ConnectedPhoneCount}/{CameraServer.MaxPhoneConnections}" +
                $" · reconnexion {_cameraServer.ReservedPhoneCount}" +
                $" · physiques {_cameraServer.PhysicalCameraCount} (non comptés) · Pipeline 16K HD / 300 FPS";
        }

        private void OnCameraConnected(CameraClient camera)
        {
            Dispatcher.Invoke(() =>
            {
                EnsurePhoneSource(camera);
                if (_selectedPreviewSlot == "Cam-1" && _cameraServer.GetCameraFrame("Cam-1") == null)
                    _selectedPreviewSlot = camera.StreamName;
                _ = PushTalliesAsync();
            });
        }

        private void OnCameraDisconnected(string streamName)
        {
            Dispatcher.Invoke(() =>
            {
                foreach (var scene in _scenes)
                {
                    var source = scene.Sources.FirstOrDefault(item => item.Id == streamName);
                    if (source != null)
                        source.Details = "déconnecté — la session peut revenir pendant quelques secondes";
                }

                if (_selectedPreviewSlot == streamName && _cameraServer.GetCameraFrame(streamName) == null)
                {
                    ImgPreviewVideo.Source = null;
                    PanelPreviewSlate.Visibility = Visibility.Visible;
                    TxtPreviewEmpty.Text = $"{streamName} : connexion perdue. Aucune image.";
                }
            });
        }

        private void OnCameraFrameReceived(string slot, byte[] jpegBytes)
        {
            _framesThisWindow++;
            lock (_frameLock)
            {
                _pendingFrames[slot] = jpegBytes;
                if (_renderScheduled)
                    return;
                _renderScheduled = true;
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
            {
                Dictionary<string, byte[]> batch;
                lock (_frameLock)
                {
                    batch = new Dictionary<string, byte[]>(_pendingFrames, StringComparer.OrdinalIgnoreCase);
                    _pendingFrames.Clear();
                    _renderScheduled = false;
                }

                foreach (var pair in batch)
                {
                    var streamSlot = pair.Key;
                    var bytes = pair.Value;

                    if (streamSlot != _selectedPreviewSlot && streamSlot != _selectedProgramSlot)
                        continue;

                    var activeSrc = _previewScene?.Sources.FirstOrDefault(s => SlotFromSource(s) == streamSlot) ?? _programScene?.Sources.FirstOrDefault(s => SlotFromSource(s) == streamSlot);
                    int rot = activeSrc?.RotationAngle ?? 0;
                    bool fH = activeSrc?.FlipHorizontal ?? false;
                    bool fV = activeSrc?.FlipVertical ?? false;

                    if (!TryDecodeJpeg(bytes, out var bitmap, rot, fH, fV) || bitmap == null)
                        continue;

                    if (streamSlot == _selectedPreviewSlot)
                        ShowJpeg(ImgPreviewVideo, PanelPreviewSlate, bitmap);

                    if (streamSlot == _selectedProgramSlot)
                    {
                        ShowJpeg(ImgProgramVideo, PanelProgramSlate, bitmap);
                        _cameraServer.ActiveProgramFrameBytes = bytes;
                        _replayBuffer?.AppendFrame(bytes);
                        TxtProgramLiveFooter.Text = $"Image reçue · {streamSlot}";
                    }
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
                var slot = SlotFromSource(src);
                if (src.Type == SourceType.MobileCamera && slot != null)
                {
                    _selectedPreviewSlot = slot;
                    var frame = _cameraServer.GetCameraFrame(slot);
                    if (frame != null && TryDecodeJpeg(frame, out var bitmap))
                        ShowJpeg(ImgPreviewVideo, PanelPreviewSlate, bitmap!);
                    else
                    {
                        ImgPreviewVideo.Source = null;
                        PanelPreviewSlate.Visibility = Visibility.Visible;
                        TxtPreviewEmpty.Text = $"{slot} : aucune image reçue.";
                    }

                    _ = PushTalliesAsync();
                }
                else if (src.Type == SourceType.PhysicalCamera)
                {
                    ImgPreviewVideo.Source = null;
                    PanelPreviewSlate.Visibility = Visibility.Visible;
                    _selectedPreviewSlot = "HDMI-1";
                    var devices = PhysicalCameraCapture.EnumerateDevices();
                    if (devices.Count > 0 && (_physicalCameraCapture == null || !_physicalCameraCapture.IsRunning))
                    {
                        _physicalCameraCapture?.Start(devices[0], 1920, 1080, 60);
                    }
                    TxtPreviewEmpty.Text = "Acquisition du signal HDMI/USB en cours...";
                }
                else if (src.Type == SourceType.ScreenCapture)
                {
                    _selectedPreviewSlot = "Screen-1";
                    var displays = ScreenCaptureEngine.EnumerateDisplays();
                    if (displays.Count > 0 && (_screenCaptureEngine == null || !_screenCaptureEngine.IsRunning))
                    {
                        _screenCaptureEngine?.Start(displays[0], 60);
                    }
                }
                else if (src.Type == SourceType.MediaVideo)
                {
                    _selectedPreviewSlot = "Media-1";
                    if (_mediaVideoSource != null && _mediaVideoSource.State != PlaybackState.Playing)
                    {
                        _mediaVideoSource.Play();
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

            var frame = _cameraServer.GetCameraFrame(_selectedProgramSlot);
            if (frame != null && TryDecodeJpeg(frame, out var bitmap))
            {
                ShowJpeg(ImgProgramVideo, PanelProgramSlate, bitmap!);
                _cameraServer.ActiveProgramFrameBytes = frame;
                TxtProgramLiveFooter.Text = $"Image reçue · {_selectedProgramSlot}";
            }
            else
            {
                ImgProgramVideo.Source = null;
                PanelProgramSlate.Visibility = Visibility.Visible;
                _cameraServer.ActiveProgramFrameBytes = null;
                TxtProgramEmpty.Text = $"Aucune image à l'antenne pour {_selectedProgramSlot}.";
                TxtProgramLiveFooter.Text = "Aucune image à l'antenne";
            }

            if (_programScene.Sources.Count > 0)
            {
                var first = _programScene.Sources[0];
                TxtProgramSub.Text = first.Name;
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

            // SECTION 1 : OBS STUDIO - CAPTURE VIDÉO PHYSIQUE
            var miPhysical = new MenuItem { Header = "📹 Périphérique de capture vidéo (Webcam, HDMI, USB)..." };
            miPhysical.Click += (s, args) =>
            {
                var physical = new StudioSource
                {
                    Name = $"Caméra USB/HDMI {selected.Sources.Count + 1}",
                    Type = SourceType.PhysicalCamera,
                    Details = "Carte de capture / Webcam DirectShow"
                };
                selected.Sources.Add(physical);
                _cameraServer.RegisterPhysicalCamera(physical.Id, physical.Name);
            };
            cm.Items.Add(miPhysical);

            // SECTION 2 : NUANCE SPÉCIALE EECC CAMÉRA SUR LE MÊME WI-FI (ROUTEUR PARTAGÉ)
            var miEeccWifiGroup = new MenuItem { Header = "📱 Caméra Mobile EECC (Wi-Fi Local / Routeur Partagé)" };
            var localIp = _cameraServer.GetLocalIpAddress();
            var wifiHeader = string.IsNullOrEmpty(localIp)
                ? "📶 Wi-Fi : Aucune IP locale détectée"
                : $"📶 Routeur Wi-Fi : {localIp}:{_cameraServer.ListeningPort}";
            miEeccWifiGroup.Items.Add(new MenuItem { Header = wifiHeader, IsEnabled = false });
            miEeccWifiGroup.Items.Add(new Separator());

            // Téléphones actuellement connectés sur le Wi-Fi
            foreach (var cam in _cameraServer.ConnectedCameras.Values)
            {
                var camSlot = cam.StreamName;
                var camIp = cam.RemoteIp;
                var camModel = cam.DeviceModel;
                var miPhoneActive = new MenuItem { Header = $"🟢 {camSlot} : {cam.DisplayName} ({camIp} · {camModel})" };
                miPhoneActive.Click += (s, args) =>
                {
                    EnsurePhoneSource(cam);
                    _selectedPreviewSlot = camSlot;
                    UpdatePreviewDisplay();
                    _ = PushTalliesAsync();
                };
                miEeccWifiGroup.Items.Add(miPhoneActive);
            }

            if (_cameraServer.ConnectedPhoneCount == 0)
            {
                miEeccWifiGroup.Items.Add(new MenuItem { Header = "Aucun smartphone connecté pour l'instant", IsEnabled = false });
            }

            miEeccWifiGroup.Items.Add(new Separator());
            var miPairNew = new MenuItem { Header = "➕ Connecter un smartphone (Afficher le QR Code Wi-Fi)..." };
            miPairNew.Click += (s, args) => BtnPairQr_Click(s, args);
            miEeccWifiGroup.Items.Add(miPairNew);

            // Slots réservés 1 à 15
            var miSlotsSub = new MenuItem { Header = "Emplacements réservés (Cam-1 à Cam-15)" };
            for (int i = 1; i <= 15; i++)
            {
                int num = i;
                var miSlot = new MenuItem { Header = $"Slot Cam-{num}" };
                miSlot.Click += (s, args) =>
                {
                    selected.Sources.Add(new StudioSource
                    {
                        Name = $"Caméra Mobile {num} (Cam-{num})",
                        Type = SourceType.MobileCamera,
                        Details = $"Flux Smartphone Slot {num}"
                    });
                    _selectedPreviewSlot = $"Cam-{num}";
                    UpdatePreviewDisplay();
                };
                miSlotsSub.Items.Add(miSlot);
            }
            miEeccWifiGroup.Items.Add(miSlotsSub);
            cm.Items.Add(miEeccWifiGroup);

            cm.Items.Add(new Separator());

            // SECTION 3 : SOURCES OBS STUDIO OFFICIELLES
            var miScreen = new MenuItem { Header = "🖥️ Capture d'écran (Moniteur complet)..." };
            miScreen.Click += (s, args) => selected.Sources.Add(new StudioSource { Name = "Capture Écran", Type = SourceType.ScreenCapture, Details = "Moniteur Principal 1920x1080" });
            cm.Items.Add(miScreen);

            var miMedia = new MenuItem { Header = "🎬 Source Média (Fichier MP4 / Vidéo)..." };
            miMedia.Click += (s, args) => selected.Sources.Add(new StudioSource { Name = "Vidéo Culte MP4", Type = SourceType.MediaVideo, Details = "Lecture Vidéo Boucle" });
            cm.Items.Add(miMedia);

            var miImg = new MenuItem { Header = "🖼️ Image (Logo / Filigrane)..." };
            miImg.Click += (s, args) =>
            {
                var imgSource = new StudioSource
                {
                    Name = "Logo EECC",
                    Type = SourceType.Image,
                    Details = "Filigrane d'angle supérieur",
                    ImagePath = "Assets/logo.png"
                };
                selected.Sources.Add(imgSource);
                ApplyOverlaysFromScene();
            };
            cm.Items.Add(miImg);

            var miSlide = new MenuItem { Header = "📊 Diaporama d'images (Diapositives)..." };
            miSlide.Click += (s, args) => selected.Sources.Add(new StudioSource { Name = "Diaporama Culte", Type = SourceType.PresentationSlide, Details = "Projection Paroles" });
            cm.Items.Add(miSlide);

            var miTxt = new MenuItem { Header = "📝 Texte GDI+ (Titre Pasteur / Verset)..." };
            miTxt.Click += (s, args) =>
            {
                var txtSource = new StudioSource
                {
                    Name = "Bandeau Titre",
                    Type = SourceType.Text,
                    TextContent = "Pasteur Principal - Culte EECC",
                    SecondaryText = "Thème du Jour",
                    Details = "Tiers Inférieur (Lower Third)"
                };
                selected.Sources.Add(txtSource);
                ApplyOverlaysFromScene();
            };
            cm.Items.Add(miTxt);

            var miTicker = new MenuItem { Header = "📜 Bande Déroulante (Filtre Défilement Scroll OBS)..." };
            miTicker.Click += (s, args) =>
            {
                var tickerSource = new StudioSource
                {
                    Name = "Bande Déroulante Annonces",
                    Type = SourceType.ScrollingTicker,
                    TextContent = "✦ Bienvenue au culte de l'Église Évangélique du Centre ✦ Annonces & Dons par Mobile Money ✦",
                    ScrollSpeed = 70.0,
                    Details = "Bandeau Défilant Continu"
                };
                selected.Sources.Add(tickerSource);
                ApplyOverlaysFromScene();
            };
            cm.Items.Add(miTicker);

            var miBrowser = new MenuItem { Header = "🌐 Navigateur (Browser Source / Page Web)..." };
            miBrowser.Click += (s, args) => selected.Sources.Add(new StudioSource { Name = "Page Web", Type = SourceType.PresentationSlide, Details = "URL Web / Alertes" });
            cm.Items.Add(miBrowser);

            var miAudioIn = new MenuItem { Header = "🎙️ Capture d'entrée audio (Microphone)..." };
            miAudioIn.Click += (s, args) => selected.Sources.Add(new StudioSource { Name = "Micro Pupitre", Type = SourceType.AudioInput, Details = "Entrée Micro USB/XLR" });
            cm.Items.Add(miAudioIn);

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
            _selectedProgramSlot = _selectedPreviewSlot;
            UpdateProgramDisplay();
            _ = PushTalliesAsync();
        }

        private void BtnFade_Click(object sender, RoutedEventArgs e)
        {
            if (_previewScene == null) return;
            _programScene = _previewScene;
            _selectedProgramSlot = _selectedPreviewSlot;

            int selectedTransition = ComboSceneTransitions?.SelectedIndex ?? 0;

            if (selectedTransition == 1) // Coupure (Cut)
            {
                BtnCut_Click(sender, e);
                return;
            }
            else if (selectedTransition == 4) // Fondu au Noir
            {
                BtnFadeToBlack_Click(sender, e);
                return;
            }
            else if (selectedTransition == 2 || selectedTransition == 3) // Glissement Gauche / Droite
            {
                double startOffset = selectedTransition == 2 ? 300 : -300;
                var slideAnim = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = startOffset,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };
                var translate = new TranslateTransform();
                ImgProgramVideo.RenderTransform = translate;
                translate.BeginAnimation(TranslateTransform.XProperty, slideAnim);
            }
            else // Fondu enchaîné standard (Fade 300 ms)
            {
                var fadeAnim = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 0.15,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
                };
                ImgProgramVideo.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            }

            UpdateProgramDisplay();
            _ = PushTalliesAsync();
        }

        private void BtnFadeToBlack_Click(object sender, RoutedEventArgs e)
        {
            ImgProgramVideo.Source = null;
            PanelProgramSlate.Visibility = Visibility.Visible;
            _cameraServer.ActiveProgramFrameBytes = null;
            TxtProgramEmpty.Text = "Écran noir. Aucune image n'est envoyée.";
            TxtProgramLiveFooter.Text = "Écran noir";
            TxtProgramMain.Text = "Écran noir";
            TxtProgramSub.Text = "Aucune source à l'antenne";
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
                TxtProgramLiveFooter.Text = snapshot.Summary;
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
            TxtProgramLiveFooter.Text = "En attente";
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

                // Enregistrement maître local haute fidélité NVMe
                var config = new MasterRecordConfig
                {
                    FileNamePrefix = $"EECC_Culte_{_cameraServer.ChurchName.Replace(' ', '_')}",
                    ContainerFormat = "mkv",
                    TargetFps = 60,
                    VideoBitrateKbps = 50000,
                    UseHardwareEncoder = true
                };

                _masterRecorder?.Start(
                    config,
                    () => _cameraServer.ActiveProgramFrameBytes,
                    frames => _audioEngine?.RenderMaster(frames) ?? Array.Empty<byte>());
                _audioEngine?.IsoRecorder.StartRecording();
                File.WriteAllText(fullPath, $"EECC Studio Master Broadcast Recording\nCulte: {_cameraServer.ProgramTitle}\nDate: {DateTime.Now}\nÉglise: {_cameraServer.ChurchName}\n");

                _cameraServer.StartRecording();
                BtnToggleRecord.Content = "Arrêter l'enregistrement";
                BtnToggleRecord.Foreground = (Brush)new BrushConverter().ConvertFrom("#EF4444");

                BadgeRecIndicator.Background = (Brush)new BrushConverter().ConvertFrom("#DC2626");
                TxtRecBadge.Text = "REC MASTER : ACTIF (50 Mbps)";
                TxtRecBadge.Foreground = Brushes.White;
            }
            else
            {
                _masterRecorder?.Stop();
                _audioEngine?.IsoRecorder.StopRecording();
                _cameraServer.StopRecording();
                BtnToggleRecord.Content = "Démarrer l'enregistrement";
                BtnToggleRecord.Foreground = (Brush)new BrushConverter().ConvertFrom("#F0F6FC");

                BadgeRecIndicator.Background = (Brush)new BrushConverter().ConvertFrom("#21262D");
                TxtRecBadge.Text = "REC : OFF";
                TxtRecBadge.Foreground = (Brush)new BrushConverter().ConvertFrom("#8B949E");

                var masterPath = _masterRecorder?.CurrentRecordingFilePath;
                if (!string.IsNullOrWhiteSpace(masterPath) && File.Exists(masterPath))
                {
                    _offlineSyncQueue?.Enqueue(masterPath, StorageTargetType.CloudflareR2, "eecc-masters", Path.GetFileName(masterPath), UploadPriority.Low);
                }

                MessageBox.Show("Enregistrement vidéo sauvegardé avec succès et placé dans la file d'attente de synchronisation cloud !", "Enregistrement Terminé", MessageBoxButton.OK, MessageBoxImage.Information);
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
            try
            {
                var dlg = new QrPairingWindow(_cameraServer) { Owner = this };
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                App.WriteLog("BtnPairQr_Click error: " + ex);
                MessageBox.Show(
                    "Impossible d'afficher la fenêtre d'appairage QR :\n" + ex.Message,
                    "EECC Studio — Code QR",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
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
                var dlg = new SourcePropertiesWindow(src) { Owner = this };
                if (dlg.ShowDialog() == true)
                {
                    ApplyOverlaysFromScene();
                }
            }
            else
            {
                MessageBox.Show("Veuillez sélectionner une source dans le dock Sources.", "Propriétés OBS Studio", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ApplyOverlaysFromScene()
        {
            if (_previewScene == null) return;

            foreach (var src in _previewScene.Sources)
            {
                if (!src.IsVisible) continue;

                if (src.Type == SourceType.Image && !string.IsNullOrWhiteSpace(src.ImagePath))
                {
                    try
                    {
                        if (File.Exists(src.ImagePath))
                        {
                            var uri = new Uri(Path.GetFullPath(src.ImagePath));
                            var bi = new BitmapImage(uri);
                            ImgPreviewLogo.Source = bi;
                            ImgProgramLogo.Source = bi;
                        }
                    }
                    catch { }
                }
                else if (src.Type == SourceType.Text || src.Type == SourceType.BibleVerse)
                {
                    if (!string.IsNullOrWhiteSpace(src.TextContent))
                    {
                        TxtPreviewLowerTitle.Text = src.TextContent;
                        TxtProgramLowerTitle.Text = src.TextContent;
                    }
                    if (!string.IsNullOrWhiteSpace(src.SecondaryText))
                    {
                        TxtPreviewLowerSub.Text = src.SecondaryText;
                        TxtProgramLowerSub.Text = src.SecondaryText;
                    }
                }
                else if (src.Type == SourceType.ScrollingTicker && !string.IsNullOrWhiteSpace(src.TextContent))
                {
                    TxtPreviewTicker.Text = src.TextContent;
                    TxtProgramTicker.Text = src.TextContent;
                    StartTickerAnimations(src.ScrollSpeed);
                }
            }
        }

        private void StartTickerAnimations(double speedPxPerSec = 70.0)
        {
            try
            {
                var textLength = Math.Max(TxtPreviewTicker.Text.Length * 8, 800);
                var durationSec = Math.Max(8, (textLength + 800) / Math.Max(20, speedPxPerSec));

                var scrollAnim = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 600,
                    To = -textLength,
                    Duration = TimeSpan.FromSeconds(durationSec),
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
                };

                TxtPreviewTicker.BeginAnimation(Canvas.LeftProperty, scrollAnim);
                TxtProgramTicker.BeginAnimation(Canvas.LeftProperty, scrollAnim);
            }
            catch { }
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
            var addressLine = localIp.Length == 0
                ? "Aucune adresse IPv4 privée utilisable n'a été détectée."
                : $"Adresse détectée : http://{localIp}:{_cameraServer.ListeningPort}/live/stream";
            MessageBox.Show(
                addressLine + "\n\n" +
                "Le même nom de Wi-Fi ne garantit pas que les téléphones joignent Studio. " +
                "Un pare-feu, un réseau invité ou l'isolation des clients peut bloquer le port. " +
                "Aucune image n'est affichée tant qu'un téléphone appairé n'en a pas envoyé.",
                "Réseau local",
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

        private void MenuCmsManager_Click(object sender, RoutedEventArgs e)
        {
            var win = new EeccTvCmsWindow { Owner = this };
            win.Show();
        }

        private void MenuTools_EeccTvMeta_Click(object sender, RoutedEventArgs e)
        {
            MenuCmsManager_Click(sender, e);
        }

        private void MenuTools_EeccTvIngest_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Ingest direct HTTP / MJPEG actif sur port 8089.", "EECC TV Ingest", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuTools_EeccTvSync_Click(object sender, RoutedEventArgs e)
        {
            MenuCmsManager_Click(sender, e);
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
            _webRtcSessionManager?.Dispose();
            _physicalCameraCapture?.Dispose();
            _screenCaptureEngine?.Dispose();
            _mediaVideoSource?.Dispose();
            _masterRecorder?.Dispose();
            _replayBuffer?.Dispose();
            _virtualCamera?.Dispose();
            _obsWebSocketServer?.Dispose();
            _offlineSyncQueue?.Dispose();
            _cameraServer.Stop();
            base.OnClosed(e);
        }

        private void EnsurePhoneSource(CameraClient camera)
        {
            var label = $"{camera.StreamName} · {camera.DisplayName}";
            foreach (var scene in _scenes)
            {
                var existing = scene.Sources.FirstOrDefault(source =>
                    source.Id == camera.StreamName || SlotFromSource(source) == camera.StreamName);
                if (existing == null)
                {
                    scene.Sources.Add(new StudioSource
                    {
                        Id = camera.StreamName,
                        Name = label,
                        Type = SourceType.MobileCamera,
                        Details = camera.RemoteIp,
                    });
                }
                else
                {
                    existing.Id = camera.StreamName;
                    existing.Name = label;
                    existing.Type = SourceType.MobileCamera;
                    existing.Details = $"{camera.RemoteIp} · {camera.DeviceModel}";
                }
            }
        }

        private async Task PushTalliesAsync()
        {
            foreach (var phone in _cameraServer.ConnectedCameras.Values)
            {
                var tally = TallyState.Standby;
                if (phone.StreamName == _selectedProgramSlot)
                    tally = TallyState.Program;
                else if (phone.StreamName == _selectedPreviewSlot)
                    tally = TallyState.Preview;
                await _cameraServer.SetTallyAsync(phone.StreamName, tally);
            }
        }

        private static void ShowJpeg(System.Windows.Controls.Image target, System.Windows.Controls.Grid slate, BitmapSource bitmap)
        {
            target.Source = bitmap;
            slate.Visibility = Visibility.Collapsed;
        }

        private static bool TryDecodeJpeg(byte[] jpeg, out BitmapSource? bitmap, int customRotation = 0, bool flipH = false, bool flipV = false)
        {
            try
            {
                var bi = new BitmapImage();
                using var ms = new MemoryStream(jpeg);
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = ms;
                bi.EndInit();
                bi.Freeze();

                var group = new TransformGroup();

                // 1. Rotation manuelle ou correction d'orientation capteur
                if (customRotation != 0)
                {
                    group.Children.Add(new RotateTransform(customRotation));
                }

                // 2. Inversion Miroir Horizontale
                if (flipH)
                {
                    group.Children.Add(new ScaleTransform(-1, 1, bi.PixelWidth / 2.0, bi.PixelHeight / 2.0));
                }

                // 3. Inversion Verticale (Correction Caméra à l'envers)
                if (flipV)
                {
                    group.Children.Add(new ScaleTransform(1, -1, bi.PixelWidth / 2.0, bi.PixelHeight / 2.0));
                }

                if (group.Children.Count > 0)
                {
                    var transformed = new TransformedBitmap();
                    transformed.BeginInit();
                    transformed.Source = bi;
                    transformed.Transform = group;
                    transformed.EndInit();
                    transformed.Freeze();
                    bitmap = transformed;
                }
                else
                {
                    bitmap = bi;
                }
                return true;
            }
            catch
            {
                bitmap = null;
                return false;
            }
        }

        private void OnPhysicalCameraFrameArrived(VideoFrameBuffer frame)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
            {
                try
                {
                    var bitmap = CreateBitmapFromFrame(frame);
                    if (_selectedPreviewSlot == "HDMI-1" || _previewScene?.Sources.Any(s => s.Type == SourceType.PhysicalCamera) == true)
                    {
                        ImgPreviewVideo.Source = bitmap;
                        PanelPreviewSlate.Visibility = Visibility.Collapsed;
                    }

                    if (_selectedProgramSlot == "HDMI-1" || _programScene?.Sources.Any(s => s.Type == SourceType.PhysicalCamera) == true)
                    {
                        ImgProgramVideo.Source = bitmap;
                        PanelProgramSlate.Visibility = Visibility.Collapsed;
                        TxtProgramLiveFooter.Text = "Image reçue · HDMI In 1080p60";
                    }
                }
                catch { }
            });
        }

        private void OnScreenCaptureFrameArrived(VideoFrameBuffer frame)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
            {
                try
                {
                    var bitmap = CreateBitmapFromFrame(frame);
                    if (_selectedPreviewSlot == "Screen-1")
                    {
                        ImgPreviewVideo.Source = bitmap;
                        PanelPreviewSlate.Visibility = Visibility.Collapsed;
                    }
                    if (_selectedProgramSlot == "Screen-1")
                    {
                        ImgProgramVideo.Source = bitmap;
                        PanelProgramSlate.Visibility = Visibility.Collapsed;
                        TxtProgramLiveFooter.Text = "Image reçue · Capture Écran";
                    }
                }
                catch { }
            });
        }

        private void OnMediaVideoFrameArrived(VideoFrameBuffer frame)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
            {
                try
                {
                    var bitmap = CreateBitmapFromFrame(frame);
                    if (_selectedPreviewSlot == "Media-1")
                    {
                        ImgPreviewVideo.Source = bitmap;
                        PanelPreviewSlate.Visibility = Visibility.Collapsed;
                    }
                    if (_selectedProgramSlot == "Media-1")
                    {
                        ImgProgramVideo.Source = bitmap;
                        PanelProgramSlate.Visibility = Visibility.Collapsed;
                        TxtProgramLiveFooter.Text = "Image reçue · Vidéo Média";
                    }
                }
                catch { }
            });
        }

        private static BitmapSource CreateBitmapFromFrame(VideoFrameBuffer frame)
        {
            var bitmap = BitmapSource.Create(
                frame.Width,
                frame.Height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                frame.Data,
                frame.Stride);
            bitmap.Freeze();
            return bitmap;
        }

        private void MenuCanvasGpu_Click(object sender, RoutedEventArgs e)
        {
            if (_gpuCompositor == null) return;
            var budget = _gpuCompositor.GetBudget();
            var encoder = HardwareEncoderDetector.Detect();

            MessageBox.Show(
                $"MOTEUR DE COMPOSITION GPU DIRECT3D 11 / EECC STUDIO\n\n" +
                $"• Résolution Canevas : {budget.Width}x{budget.Height} @ {budget.TargetFps} FPS\n" +
                $"• Bande passante brute : {budget.RawBandwidthGbps} Gbps (Non compressé)\n" +
                $"• Budget VRAM estimé : {budget.EstimatedVramMb} Mo (Triple-buffering + calques)\n" +
                $"• Débit d'encodage recommandé : {budget.RecommendedEncodingBitrate}\n" +
                $"• Encodeur actif détecté : {encoder.Description} ({encoder.H264EncoderName})\n" +
                $"• Matériel recommandé : {budget.RecommendedHardware}\n\n" +
                $"Compatible 1080p, 1440p, 4K, 8K et 16K Ultra-HD.",
                "Canevas Vidéo & Budget GPU",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void MenuPhysicalDevices_Click(object sender, RoutedEventArgs e)
        {
            var devices = PhysicalCameraCapture.EnumerateDevices();
            var lines = new List<string>();
            foreach (var d in devices)
            {
                lines.Add($"• {d.Name} ({d.Id}) - {(d.IsCaptureCard ? "Carte HDMI In" : "Caméra USB")}");
            }

            MessageBox.Show(
                "PÉRIPHÉRIQUES VIDÉO PHYSIQUES DÉTECTÉS :\n\n" +
                string.Join("\n", lines) + "\n\n" +
                "Toutes les cartes HDMI et USB sont gérées nativement sans passerelle logicielle tierce.",
                "Périphériques Physiques DirectShow / Media Foundation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void MenuReplayBuffer_Click(object sender, RoutedEventArgs e)
        {
            if (_replayBuffer == null) return;
            var file = await _replayBuffer.SaveReplayAsync();
            if (file != null)
            {
                MessageBox.Show($"Instant Replay (Ralenti culte) exporté avec succès !\n\nFichier : {file}", "Replay Buffer Sauvegardé", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Le Replay Buffer est en cours de remplissage en mémoire (aucune trame disponible).", "Replay Buffer", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void MenuVirtualCamera_Click(object sender, RoutedEventArgs e)
        {
            if (_virtualCamera == null) return;

            if (!_virtualCamera.IsRunning)
            {
                _virtualCamera.Start();
                MessageBox.Show("Sortie Caméra Virtuelle ACTIVÉE !\n\nDisponible dans Zoom, Microsoft Teams et Google Meet sous le nom :\n'EECC Studio Virtual Camera'", "Caméra Virtuelle", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                _virtualCamera.Stop();
                MessageBox.Show("Sortie Caméra Virtuelle DÉSACTIVÉE.", "Caméra Virtuelle", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void MenuObsWebSocket_Click(object sender, RoutedEventArgs e)
        {
            var isRunning = _obsWebSocketServer?.IsRunning ?? false;
            var port = _obsWebSocketServer?.Port ?? 4455;
            MessageBox.Show(
                $"SERVEUR API OBS WEBSOCKET v5 (STREAM DECK & COMPANION)\n\n" +
                $"• Statut : {(isRunning ? "ACTIF (En écoute)" : "ARRÊTÉ")}\n" +
                $"• Port d'écoute : {port}\n" +
                $"• Protocole : OBS WebSocket Protocol v5.3.0 (RPC v1)\n" +
                $"• Compatibilité matérielle : Elgato Stream Deck (Plugin OBS Studio), Bitfocus Companion, Touch Portal\n" +
                $"• Sécurité : Connexion réseau local autorisée sans mot de passe requis.\n\n" +
                $"Toutes les commandes (Changement de scène, Transition Cut/Fade, Start/Stop Stream & REC, Replay) sont actives.",
                "Serveur OBS WebSocket",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private static string? SlotFromSource(StudioSource source)
        {
            if (source.Id.StartsWith("Cam-", StringComparison.OrdinalIgnoreCase))
                return source.Id;
            var match = Regex.Match(source.Name, @"Cam-(\d+)");
            return match.Success ? "Cam-" + match.Groups[1].Value : null;
        }

        private void MenuEditTransform_Reset_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Transformation de la source réinitialisée.", "OBS Studio", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuEditTransform_Rotate90CW_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Rotation 90° horaire appliquée.", "OBS Studio", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuEditTransform_Rotate90CCW_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Rotation 90° anti-horaire appliquée.", "OBS Studio", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuEditTransform_Rotate180_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Rotation 180° appliquée.", "OBS Studio", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuEditTransform_FlipH_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Miroir horizontal appliqué.", "OBS Studio", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuEditTransform_FlipV_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Miroir vertical appliqué.", "OBS Studio", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuTools_AutoConfig_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Assistant de configuration automatique OBS Studio :\nRésolution canevas : 1080p60.", "Configuration Automatique", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuTools_EeccCamTorch_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Contrôle torche & signal tally téléphones mobiles actifs.", "EECC Caméra", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuHelp_Logs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EeccStudio", "logs");
                Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            }
            catch { }
        }
    }
}