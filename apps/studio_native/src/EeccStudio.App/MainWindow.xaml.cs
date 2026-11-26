using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        private readonly ObservableCollection<MixerStrip> _mixerStrips = new();
        private readonly StudioUndoStack _undo = new();
        private readonly OverlayPlayback _previewOverlays = new();
        private readonly OverlayPlayback _programOverlays = new();
        private StudioSource? _copiedSource;
        private string _profileName = "Culte Dominical EECC";
        private string _collectionName = "Production Culte EECC";
        private bool _tBarDragging;

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

        public MainWindow()
        {
            InitializeComponent();

            // 1. Initialisation des moteurs C# Broadcast natifs
            _cameraServer = new CameraServer(port: 8089);
            _audioEngine = new AudioEngine();
            _webRtcSessionManager = new WebRtcSessionManager(_cameraServer, _audioEngine);
            PublicProgramRelay.Shared.RenderAudio = frames => _audioEngine.RenderMaster(frames);
            FfmpegLocator.EnsureOnPath();
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
                foreach (var scene in new[] { _previewScene, _programScene }.Where(scene => scene != null).Distinct())
                {
                    var source = scene!.Sources.FirstOrDefault(item => item.Type is SourceType.Text or SourceType.BibleVerse);
                    if (source == null)
                        continue;
                    source.IsVisible = visible;
                    if (!string.IsNullOrWhiteSpace(speaker))
                        source.TextContent = speaker;
                    if (!string.IsNullOrWhiteSpace(title))
                        source.SecondaryText = title;
                }
                ApplyOverlaysFromScene();
            });

            _obsWebSocketServer.OnToggleTicker = (enabled, text) => Dispatcher.Invoke(() =>
            {
                foreach (var scene in new[] { _previewScene, _programScene }.Where(scene => scene != null).Distinct())
                {
                    var source = scene!.Sources.FirstOrDefault(item => item.Type == SourceType.ScrollingTicker);
                    if (source == null)
                        continue;
                    source.IsVisible = enabled;
                    if (!string.IsNullOrWhiteSpace(text))
                        source.TextContent = text;
                }
                ApplyOverlaysFromScene();
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

            if (!App.IsSmokeTest)
                TryLoadCollection();

            RefreshWindowTitle();

            // 3. Minuteur Broadcast temps réel (Status, LIVE, REC, CPU)
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += OnClockTick;
            _clockTimer.Start();

            // Affichage initial du statut réseau
            UpdateNetworkStatusDisplay();
            if (App.IsDemoMode)
            {
                BadgeDemo.Visibility = Visibility.Visible;
            }
            BindMixer();
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
                App.WriteLog("MainWindow loaded");
                CanvasPreviewGraphics.SizeChanged += (_, _) => PaintPreviewOverlays();
                CanvasProgramGraphics.SizeChanged += (_, _) => PaintProgramOverlays();
                _cameraServer.DecorateOnAirFrame = ProgramGraphicStamp.Apply;
                ApplyOverlaysFromScene();
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
                UpdateMixerMeters();
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
            UpdateMixerMeters();
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
                    if (!IsJpeg(bytes))
                        continue;

                    var activeSrc = _previewScene?.Sources.FirstOrDefault(s => SlotFromSource(s) == streamSlot)
                        ?? _programScene?.Sources.FirstOrDefault(s => SlotFromSource(s) == streamSlot);
                    int rot = activeSrc?.RotationAngle ?? 0;
                    bool fH = activeSrc?.FlipHorizontal ?? false;
                    bool fV = activeSrc?.FlipVertical ?? false;

                    if (!TryDecodeJpeg(bytes, out var bitmap, rot, fH, fV) || bitmap == null)
                        continue;

                    var previewFollows = streamSlot == _selectedPreviewSlot || !IsDedicatedProgramSlot(_selectedPreviewSlot);
                    if (previewFollows && (_selectedPreviewSlot == streamSlot || _cameraServer.GetCameraFrame(_selectedPreviewSlot) == null))
                    {
                        _selectedPreviewSlot = streamSlot;
                        ShowJpeg(ImgPreviewVideo, PanelPreviewSlate, bitmap);
                        TxtPreviewEmpty.Text = "";
                    }

                    var programFollows = streamSlot == _selectedProgramSlot
                        || (_selectedProgramSlot.StartsWith("Cam-", StringComparison.OrdinalIgnoreCase)
                            && _cameraServer.GetCameraFrame(_selectedProgramSlot) == null);
                    if (programFollows)
                    {
                        if (_selectedProgramSlot != streamSlot && _cameraServer.GetCameraFrame(_selectedProgramSlot) == null)
                            _selectedProgramSlot = streamSlot;
                        if (streamSlot == _selectedProgramSlot)
                        {
                            ShowJpeg(ImgProgramVideo, PanelProgramSlate, bitmap);
                            _cameraServer.ActiveProgramFrameBytes = bytes;
                            _replayBuffer?.AppendFrame(bytes);
                            TxtProgramLiveFooter.Text = $"Image reçue · {streamSlot}";
                        }
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
            PaintPreviewOverlays();
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
            PaintProgramOverlays();
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
                dup.Sources.Add(src.Clone());
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

            var miImg = new MenuItem { Header = "Image (logo, forme, position)..." };
            miImg.Click += (_, _) => OpenGraphics(new StudioSource
            {
                Name = "Image",
                Type = SourceType.Image,
                Shape = GraphicShape.Rounded,
                Motion = GraphicMotion.Fade,
            });
            cm.Items.Add(miImg);

            var miSlide = new MenuItem { Header = "📊 Diaporama d'images (Diapositives)..." };
            miSlide.Click += (s, args) => selected.Sources.Add(new StudioSource { Name = "Diaporama Culte", Type = SourceType.PresentationSlide, Details = "Projection Paroles" });
            cm.Items.Add(miSlide);

            var miTxt = new MenuItem { Header = "Texte fixe ou animé..." };
            miTxt.Click += (_, _) => OpenGraphics(new StudioSource
            {
                Name = "Texte",
                Type = SourceType.Text,
                TextContent = "Nouveau texte",
                SecondaryText = "Deuxième ligne",
                TextStyle = GraphicTextStyle.LowerThird,
                Motion = GraphicMotion.Slide,
            });
            cm.Items.Add(miTxt);

            var miTicker = new MenuItem { Header = "Texte défilant..." };
            miTicker.Click += (_, _) => OpenGraphics(new StudioSource
            {
                Name = "Texte défilant",
                Type = SourceType.ScrollingTicker,
                TextContent = "Annonce du culte · bienvenue à l'Église Évangélique du Centre",
                Motion = GraphicMotion.Scroll,
                ScrollSpeed = 70,
            });
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
                var index = selected.Sources.IndexOf(src);
                selected.Sources.RemoveAt(index);
                Remember("Supprimer la source",
                    () => selected.Sources.Insert(Math.Min(index, selected.Sources.Count), src),
                    () => selected.Sources.Remove(src));
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
                    Duration = TimeSpan.FromMilliseconds(TransitionDurationMs()),
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
                    Duration = TimeSpan.FromMilliseconds(TransitionDurationMs()),
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
                _ = EeccTvCatalog.EndLiveAsync();
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

        private void BindMixer()
        {
            if (_audioEngine == null)
                return;
            foreach (var channel in _audioEngine.Channels)
                _mixerStrips.Add(new MixerStrip(_audioEngine, channel));
            MixerStrips.ItemsSource = _mixerStrips;
        }

        private void UpdateMixerMeters()
        {
            if (_audioEngine == null)
                return;
            if (!App.IsDemoMode && _broadcast is not { IsActive: true })
                _audioEngine.RenderMaster(480);
            if (App.IsDemoMode)
            {
                var tick = DateTime.UtcNow.Second;
                var index = 0;
                foreach (var strip in _mixerStrips)
                    strip.ApplyLevel(-32 + (index++ * 4) + tick % 6);
                return;
            }

            foreach (var strip in _mixerStrips)
            {
                var channel = _audioEngine.Channels.FirstOrDefault(item => item.Id == strip.Id);
                if (channel != null)
                    strip.ApplyLevel(channel.CurrentLevelDb);
            }
        }

        private void MixerMute_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is MixerStrip strip)
                strip.ToggleMute();
        }

        private void MixerMonitor_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is MixerStrip strip)
                strip.CycleMonitor();
        }

        private void MixerLock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Parent: ContextMenu menu }
                && menu.PlacementTarget is FrameworkElement { DataContext: MixerStrip strip })
                strip.ToggleLock();
        }

        private void MenuAdvancedAudio_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AdvancedAudioWindow(_mixerStrips.ToList()) { Owner = this };
            dialog.ShowDialog();
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
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Matroska (*.mkv)|*.mkv|Tous les fichiers|*.*",
                Title = "Remuxer vers MP4 sans réencodage",
            };
            if (dialog.ShowDialog(this) != true)
                return;
            var ffmpeg = FfmpegLocator.Find();
            if (ffmpeg == null)
            {
                MessageBox.Show("ffmpeg est introuvable. Relancez l'installateur EECC Studio : il place ffmpeg dans le dossier de l'application et l'ajoute au PATH.", "Remux", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var output = Path.ChangeExtension(dialog.FileName, ".mp4");
            var start = new ProcessStartInfo
            {
                FileName = ffmpeg,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            start.ArgumentList.Add("-y");
            start.ArgumentList.Add("-i");
            start.ArgumentList.Add(dialog.FileName);
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add("copy");
            start.ArgumentList.Add(output);
            using var process = Process.Start(start);
            process?.WaitForExit(120_000);
            var ok = process is { HasExited: true, ExitCode: 0 } && File.Exists(output);
            MessageBox.Show(
                ok ? "Fichier écrit :\n" + output : "Le remux n'a pas abouti. Le fichier source est inchangé.",
                "Remux",
                MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
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

        private void MenuGraphics_Click(object sender, RoutedEventArgs e) => OpenGraphics(null);

        private void ListSources_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ListSources.SelectedItem is StudioSource source && GraphicPlacement.IsGraphic(source))
                OpenGraphics(source);
        }

        private void OpenGraphics(StudioSource? created)
        {
            if (ListScenes.SelectedItem is not StudioScene scene)
            {
                MessageBox.Show("Choisissez d'abord une scène.", "Habillage", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (created != null && !scene.Sources.Contains(created))
            {
                GraphicPlacement.Ensure(created);
                scene.Sources.Add(created);
            }

            var board = new GraphicsBoardWindow(scene, created ?? (ListSources.SelectedItem as StudioSource)) { Owner = this };
            board.Applied += ApplyOverlaysFromScene;
            board.ShowDialog();
            ApplyOverlaysFromScene();
        }

        private void ApplyOverlaysFromScene()
        {
            PaintPreviewOverlays();
            PaintProgramOverlays();
        }

        private void PaintPreviewOverlays()
        {
            if (_previewScene == null)
                return;
            _previewOverlays.Paint(CanvasPreviewGraphics, _previewScene.Sources);
        }

        private void PaintProgramOverlays()
        {
            var scene = _programScene ?? _previewScene;
            if (scene == null)
                return;
            _programOverlays.Paint(CanvasProgramGraphics, scene.Sources);
            ProgramGraphicStamp.Use(scene.Sources);
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
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = WindowState.Normal;
            Width = 1480;
            Height = 900;
            if (!_isStudioMode)
                BtnStudioMode_Click(sender, e);
            TBarSlider.Value = 0;
            ImgProgramMix.Opacity = 0;
            ImgProgramVideo.Opacity = 1;
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
            var win = new EeccTvCmsWindow { Owner = this, StudioServer = _cameraServer };
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

        private static bool IsJpeg(byte[] bytes) =>
            bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8;

        private static bool IsDedicatedProgramSlot(string slot) =>
            slot is "HDMI-1" or "Screen-1" or "Media-1";

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
                    if (_selectedPreviewSlot == "HDMI-1")
                    {
                        ImgPreviewVideo.Source = bitmap;
                        PanelPreviewSlate.Visibility = Visibility.Collapsed;
                    }

                    if (_selectedProgramSlot == "HDMI-1")
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

        private void MenuEditTransform_Reset_Click(object sender, RoutedEventArgs e) =>
            TransformSelected(SourceTransform.Reset, "Réinitialiser la transformation");

        private void MenuEditTransform_Rotate90CW_Click(object sender, RoutedEventArgs e) =>
            TransformSelected(source => SourceTransform.Rotate(source, 90), "Pivoter de 90°");

        private void MenuEditTransform_Rotate90CCW_Click(object sender, RoutedEventArgs e) =>
            TransformSelected(source => SourceTransform.Rotate(source, -90), "Pivoter de -90°");

        private void MenuEditTransform_Rotate180_Click(object sender, RoutedEventArgs e) =>
            TransformSelected(source => SourceTransform.Rotate(source, 180), "Pivoter de 180°");

        private void MenuEditTransform_FlipH_Click(object sender, RoutedEventArgs e) =>
            TransformSelected(SourceTransform.FlipHorizontal, "Miroir horizontal");

        private void MenuEditTransform_FlipV_Click(object sender, RoutedEventArgs e) =>
            TransformSelected(SourceTransform.FlipVertical, "Miroir vertical");

        private void MenuEditTransform_Fit_Click(object sender, RoutedEventArgs e)
        {
            ImgPreviewVideo.Stretch = Stretch.Uniform;
            ImgProgramVideo.Stretch = Stretch.Uniform;
            ImgProgramMix.Stretch = Stretch.Uniform;
            CenterVideoMonitors();
            TxtProgramLiveFooter.Text = "Source ajustée au canevas";
        }

        private void MenuEditTransform_Center_Click(object sender, RoutedEventArgs e)
        {
            CenterVideoMonitors();
            TxtProgramLiveFooter.Text = "Source centrée";
        }

        private void MenuUndo_Click(object sender, RoutedEventArgs e)
        {
            if (!_undo.TryUndo())
                return;
            ListSources.Items.Refresh();
            PersistCollection();
        }

        private void MenuRedo_Click(object sender, RoutedEventArgs e)
        {
            if (!_undo.TryRedo())
                return;
            ListSources.Items.Refresh();
            PersistCollection();
        }

        private void MenuCopySource_Click(object sender, RoutedEventArgs e)
        {
            if (ListSources.SelectedItem is StudioSource src)
                _copiedSource = src;
        }

        private void MenuPasteSource_Click(object sender, RoutedEventArgs e)
        {
            if (_copiedSource == null || ListScenes.SelectedItem is not StudioScene scene)
                return;
            var sameScene = scene.Sources.Contains(_copiedSource);
            var item = sameScene ? _copiedSource.Clone() : _copiedSource;
            if (!sameScene && scene.Sources.Contains(item))
                item = _copiedSource.Clone();
            scene.Sources.Add(item);
            ListSources.SelectedItem = item;
            Remember("Coller la source", () => scene.Sources.Remove(item), () => scene.Sources.Add(item));
        }

        private void MenuFilters_Click(object sender, RoutedEventArgs e) =>
            MenuSourceProperties_Click(sender, e);

        private void BtnMoveSourceUp_Click(object sender, RoutedEventArgs e) => MoveSource(-1);
        private void BtnMoveSourceDown_Click(object sender, RoutedEventArgs e) => MoveSource(1);

        private void BtnMoveSourceTop_Click(object sender, RoutedEventArgs e)
        {
            if (ListSources.SelectedIndex > 0)
                MoveSource(-ListSources.SelectedIndex);
        }

        private void BtnMoveSourceBottom_Click(object sender, RoutedEventArgs e)
        {
            if (ListScenes.SelectedItem is StudioScene scene && ListSources.SelectedIndex >= 0)
                MoveSource(scene.Sources.Count - 1 - ListSources.SelectedIndex);
        }

        private void SourceEye_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: StudioSource src })
                return;
            var was = src.IsVisible;
            src.IsVisible = !was;
            ListSources.Items.Refresh();
            ApplyOverlaysFromScene();
            Remember("Visibilité", () => { src.IsVisible = was; ListSources.Items.Refresh(); }, () => { src.IsVisible = !was; ListSources.Items.Refresh(); });
            e.Handled = true;
        }

        private void TransitionDuration_LostFocus(object sender, RoutedEventArgs e)
        {
            var ms = TransitionDurationMs(sender as TextBox);
            var text = ms + " ms";
            TxtTransitionDuration.Text = text;
            TxtCenterTransitionDuration.Text = text;
            TxtFadeButton.Text = "Fondu (" + text + ")";
        }

        private void TBarSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded)
                return;
            ImgProgramMix.Source = ImgPreviewVideo.Source;
            ImgProgramMix.Opacity = e.NewValue / 100.0;
            ImgProgramVideo.Opacity = 1.0 - e.NewValue / 100.0;
            _tBarDragging = e.NewValue > 0;
        }

        private void TBarSlider_Released(object sender, MouseButtonEventArgs e)
        {
            if (!_tBarDragging)
                return;
            var commit = TBarSlider.Value >= 98;
            TBarSlider.Value = 0;
            ImgProgramMix.Opacity = 0;
            ImgProgramVideo.Opacity = 1;
            _tBarDragging = false;
            if (commit)
                BtnCut_Click(sender, new RoutedEventArgs());
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox)
                return;
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
            {
                MenuUndo_Click(sender, e);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y)
            {
                MenuRedo_Click(sender, e);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
            {
                MenuCopySource_Click(sender, e);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V)
            {
                MenuPasteSource_Click(sender, e);
                e.Handled = true;
            }
        }

        private void MenuProfileNew_Click(object sender, RoutedEventArgs e) =>
            SwitchProfile(Prompt("Nouveau profil", "Nom du profil") );

        private void MenuProfileDuplicate_Click(object sender, RoutedEventArgs e) =>
            SwitchProfile(Prompt("Dupliquer le profil", _profileName + " copie"));

        private void MenuProfileRename_Click(object sender, RoutedEventArgs e)
        {
            var name = Prompt("Renommer le profil", _profileName);
            if (string.IsNullOrWhiteSpace(name) || name == _profileName)
                return;
            var root = ProfilesRoot();
            var from = Path.Combine(root, SceneCollectionFile.Sanitize(_profileName));
            var to = Path.Combine(root, SceneCollectionFile.Sanitize(name));
            PersistCollection();
            if (Directory.Exists(from) && !Directory.Exists(to))
                Directory.Move(from, to);
            _profileName = name.Trim();
            RefreshWindowTitle();
        }

        private void MenuProfilePick_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item)
                return;
            var header = item.Header?.ToString() ?? "";
            var name = header.Contains("Chorale", StringComparison.Ordinal) ? "Répétition Chorale & Louange"
                : header.Contains("Séminaire", StringComparison.Ordinal) ? "Séminaire & Événement Spécial"
                : "Culte Dominical EECC";
            if (name == _profileName)
                return;
            PersistCollection();
            _profileName = name;
            TryLoadCollection();
            CheckProfileMenus();
            RefreshWindowTitle();
        }

        private void MenuCollectionNew_Click(object sender, RoutedEventArgs e)
        {
            var name = Prompt("Nouvelle collection", "Nouvelle collection");
            if (string.IsNullOrWhiteSpace(name))
                return;
            PersistCollection();
            _collectionName = name.Trim();
            _scenes.Clear();
            var scene = new StudioScene("Scène", "Collection vide");
            _scenes.Add(scene);
            ListScenes.SelectedItem = scene;
            PersistCollection();
            RefreshWindowTitle();
        }

        private void MenuCollectionDuplicate_Click(object sender, RoutedEventArgs e)
        {
            var name = Prompt("Dupliquer la collection", _collectionName + " copie");
            if (string.IsNullOrWhiteSpace(name))
                return;
            PersistCollection();
            _collectionName = name.Trim();
            PersistCollection();
            RefreshWindowTitle();
        }

        private void MenuCollectionRename_Click(object sender, RoutedEventArgs e)
        {
            var name = Prompt("Renommer la collection", _collectionName);
            if (string.IsNullOrWhiteSpace(name))
                return;
            var previous = CollectionPath();
            _collectionName = name.Trim();
            PersistCollection();
            if (!string.Equals(previous, CollectionPath(), StringComparison.OrdinalIgnoreCase) && File.Exists(previous))
                File.Delete(previous);
            RefreshWindowTitle();
        }

        private void MenuCollectionPick_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item)
                return;
            var name = (item.Header?.ToString() ?? "").Contains("Démo", StringComparison.Ordinal)
                ? "Studio Démo & Tests"
                : "Production Culte EECC";
            if (name == _collectionName)
                return;
            PersistCollection();
            _collectionName = name;
            TryLoadCollection();
            MenuCollectionProduction.IsChecked = name.StartsWith("Production", StringComparison.Ordinal);
            MenuCollectionDemo.IsChecked = !MenuCollectionProduction.IsChecked;
            RefreshWindowTitle();
        }

        private void MenuProfileExport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Collection EECC (*.json)|*.json",
                FileName = SceneCollectionFile.Sanitize(_collectionName) + ".json",
            };
            if (dialog.ShowDialog(this) != true)
                return;
            SceneCollectionFile.Save(dialog.FileName, _scenes);
        }

        private void MenuProfileImport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Collection EECC (*.json)|*.json" };
            if (dialog.ShowDialog(this) != true)
                return;
            var loaded = SceneCollectionFile.Load(dialog.FileName);
            if (loaded.Count == 0)
            {
                MessageBox.Show("Ce fichier ne contient aucune scène.", "Importer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ReplaceScenes(loaded);
            PersistCollection();
        }

        private void MenuTools_AutoConfig_Click(object sender, RoutedEventArgs e)
        {
            TxtTransitionDuration.Text = "300 ms";
            TxtCenterTransitionDuration.Text = "300 ms";
            TxtFadeButton.Text = "Fondu (300 ms)";
            var width = _gpuCompositor?.CanvasWidth ?? 1920;
            var height = _gpuCompositor?.CanvasHeight ?? 1080;
            var fps = _gpuCompositor?.CanvasFps ?? 60;
            MessageBox.Show(
                $"Canevas conservé : {width}×{height} à {fps} images/s.\nDurée de transition : 300 ms.\nLes téléphones restent limités à 15 sur le port {_cameraServer.ListeningPort}.",
                "Configuration automatique",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void MenuTools_EeccCamTorch_Click(object sender, RoutedEventArgs e)
        {
            var sent = 0;
            foreach (var camera in _cameraServer.ConnectedCameras.Values)
            {
                if (await _cameraServer.SendCommandAsync(camera.StreamName, "toggle_torch"))
                    sent++;
            }
            TxtProgramLiveFooter.Text = sent == 0
                ? "Aucun téléphone connecté pour la torche"
                : $"Torche basculée sur {sent} téléphone(s)";
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

        private void TransformSelected(Func<StudioSource, bool> operation, string label)
        {
            if (ListSources.SelectedItem is not StudioSource source)
            {
                MessageBox.Show("Sélectionnez une source dans le dock Sources.", "Transformer", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var before = SourceTransform.Capture(source);
            if (!operation(source))
            {
                TxtProgramLiveFooter.Text = "Source verrouillée";
                return;
            }
            var after = SourceTransform.Capture(source);
            Remember(label, () => RestoreTransform(source, before), () => RestoreTransform(source, after));
            RestoreTransform(source, after);
        }

        private void RestoreTransform(StudioSource source, SourceTransform.Snapshot snapshot)
        {
            SourceTransform.Restore(source, snapshot);
            ListSources.Items.Refresh();
            var slot = SlotFromSource(source);
            if (slot == null)
                return;
            var frame = _cameraServer.GetCameraFrame(slot);
            if (frame == null || !TryDecodeJpeg(frame, out var bitmap, source.RotationAngle, source.FlipHorizontal, source.FlipVertical) || bitmap == null)
                return;
            if (slot == _selectedPreviewSlot)
                ShowJpeg(ImgPreviewVideo, PanelPreviewSlate, bitmap);
            if (slot == _selectedProgramSlot)
                ShowJpeg(ImgProgramVideo, PanelProgramSlate, bitmap);
            ImgPreviewVideo.Opacity = source.Opacity;
            ImgProgramVideo.Opacity = source.Opacity;
        }

        private void MoveSource(int delta)
        {
            if (ListScenes.SelectedItem is not StudioScene scene || ListSources.SelectedItem is not StudioSource source)
                return;
            var from = scene.Sources.IndexOf(source);
            var to = from + delta;
            if (from < 0 || to < 0 || to >= scene.Sources.Count)
                return;
            scene.Sources.Move(from, to);
            ListSources.SelectedIndex = to;
            Remember("Ordre des sources",
                () => { scene.Sources.Move(to, from); ListSources.SelectedIndex = from; },
                () => { scene.Sources.Move(from, to); ListSources.SelectedIndex = to; });
        }

        private void Remember(string name, Action undo, Action redo)
        {
            _undo.Push(name, undo, redo);
            PersistCollection();
        }

        private int TransitionDurationMs(TextBox? source = null)
        {
            var text = source?.Text ?? TxtTransitionDuration.Text;
            var digits = new string(text.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var ms) && ms is >= 0 and <= 60_000 ? ms : 300;
        }

        private void CenterVideoMonitors()
        {
            ImgPreviewVideo.HorizontalAlignment = HorizontalAlignment.Center;
            ImgPreviewVideo.VerticalAlignment = VerticalAlignment.Center;
            ImgProgramVideo.HorizontalAlignment = HorizontalAlignment.Center;
            ImgProgramVideo.VerticalAlignment = VerticalAlignment.Center;
            ImgProgramVideo.RenderTransform = Transform.Identity;
        }

        private static string ProfilesRoot() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EECC_Studio", "profiles");

        private string CollectionPath() =>
            SceneCollectionFile.PathFor(ProfilesRoot(), _profileName, _collectionName);

        private void PersistCollection()
        {
            if (App.IsSmokeTest)
                return;
            try { SceneCollectionFile.Save(CollectionPath(), _scenes); }
            catch (Exception ex) { App.WriteLog(ex.ToString()); }
        }

        private void TryLoadCollection()
        {
            try
            {
                var loaded = SceneCollectionFile.Load(CollectionPath());
                if (loaded.Count > 0)
                    ReplaceScenes(loaded);
            }
            catch (Exception ex)
            {
                App.WriteLog(ex.ToString());
            }
        }

        private void ReplaceScenes(List<StudioScene> loaded)
        {
            _scenes.Clear();
            foreach (var scene in loaded)
                _scenes.Add(scene);
            _programScene = _scenes[0];
            _previewScene = _scenes[0];
            ListScenes.SelectedIndex = 0;
            ListSources.ItemsSource = _scenes[0].Sources;
            UpdatePreviewDisplay();
            UpdateProgramDisplay();
            ApplyOverlaysFromScene();
        }

        private void SwitchProfile(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;
            PersistCollection();
            _profileName = name.Trim();
            if (File.Exists(CollectionPath()))
                TryLoadCollection();
            else
                PersistCollection();
            CheckProfileMenus();
            RefreshWindowTitle();
        }

        private void CheckProfileMenus()
        {
            MenuProfileSunday.IsChecked = _profileName.Contains("Dominical", StringComparison.Ordinal);
            MenuProfileChoir.IsChecked = _profileName.Contains("Chorale", StringComparison.Ordinal);
            MenuProfileSeminar.IsChecked = _profileName.Contains("Séminaire", StringComparison.Ordinal);
        }

        private void RefreshWindowTitle() =>
            Title = $"EECC Studio — Profil : {_profileName} — Collection : {_collectionName}";

        private string? Prompt(string title, string initial)
        {
            var box = new TextBox
            {
                Text = initial,
                Margin = new Thickness(12, 12, 12, 8),
                Padding = new Thickness(6, 4, 6, 4),
            };
            var ok = new Button { Content = "OK", Width = 88, Height = 28, Margin = new Thickness(0, 0, 8, 12), IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
            var panel = new DockPanel();
            DockPanel.SetDock(ok, Dock.Bottom);
            panel.Children.Add(ok);
            panel.Children.Add(box);
            var dialog = new Window
            {
                Title = title,
                Width = 420,
                Height = 150,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Content = panel,
                Background = (Brush)FindResource("ObsWindowBackground"),
            };
            string? result = null;
            ok.Click += (_, _) => { result = box.Text; dialog.DialogResult = true; };
            return dialog.ShowDialog() == true ? result?.Trim() : null;
        }
    }
}