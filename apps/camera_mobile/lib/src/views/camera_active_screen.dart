import 'dart:io';
import 'package:flutter/material.dart';
import 'package:camera/camera.dart';
import '../constants/colors.dart';
import '../models/session_config.dart';
import '../services/studio_connection_service.dart';

/// Écran de captation vidéo 100% MATÉRIEL et FONCTIONNEL
/// Utilise le capteur physique du smartphone (CameraController), le Tally réel et la télémétrie en direct.
class CameraActiveScreen extends StatefulWidget {
  final StudioSessionConfig config;

  const CameraActiveScreen({super.key, required this.config});

  @override
  State<CameraActiveScreen> createState() => _CameraActiveScreenState();
}

class _CameraActiveScreenState extends State<CameraActiveScreen> with WidgetsBindingObserver {
  late StudioConnectionService _connectionService;
  TallyState _tally = TallyState.standby;
  bool _isConnected = false;
  bool _isStreamingFrames = false;
  bool _isCapturing = false;
  String _activeSlotName = '';
  int _activeSlotNumber = 0;
  int _measuredFps = 0;
  int _measuredBitrate = 0;
  String? _linkMessage;

  // Contrôleur matériel de la caméra
  CameraController? _cameraController;
  List<CameraDescription> _availableCameras = [];
  int _selectedCameraIndex = 0;
  bool _isCameraInitialized = false;
  String? _cameraErrorMessage;

  // Contrôles optiques réels
  bool _isTorchOn = false;
  bool _isMuted = false;
  bool _isScreenLocked = false;
  double _currentZoom = 1.0;
  double _minZoom = 1.0;
  double _maxZoom = 4.0;
  double _baseScale = 1.0;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _initHardwareCamera();
    _initStudioConnection();
  }

  @override
  void dispose() {
    _stopContinuousStreaming();
    WidgetsBinding.instance.removeObserver(this);
    _cameraController?.dispose();
    _connectionService.disconnect();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    final CameraController? controller = _cameraController;
    if (controller == null || !controller.value.isInitialized) return;

    if (state == AppLifecycleState.inactive) {
      controller.dispose();
    } else if (state == AppLifecycleState.resumed) {
      _initHardwareCamera();
    }
  }

  /// Initialise le capteur optique réel de l'appareil
  Future<void> _initHardwareCamera() async {
    try {
      _availableCameras = await availableCameras();
      if (_availableCameras.isEmpty) {
        setState(() {
          _cameraErrorMessage = 'Aucun capteur caméra détecté sur cet appareil.';
        });
        return;
      }

      await _setupCameraController(_availableCameras[_selectedCameraIndex]);
    } catch (e) {
      setState(() {
        _cameraErrorMessage = 'Erreur d\'accès au capteur caméra : $e';
      });
    }
  }

  Future<void> _setupCameraController(CameraDescription description) async {
    await _cameraController?.dispose();

    final controller = CameraController(
      description,
      ResolutionPreset.high, // 1080p broadcast
      enableAudio: !_isMuted,
      imageFormatGroup: ImageFormatGroup.yuv420,
    );

    _cameraController = controller;

    try {
      await controller.initialize();
      _minZoom = await controller.getMinZoomLevel();
      _maxZoom = (await controller.getMaxZoomLevel()).clamp(1.0, 5.0);

      if (mounted) {
        setState(() {
          _isCameraInitialized = true;
          _cameraErrorMessage = null;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _cameraErrorMessage = 'Impossible d\'initialiser la caméra : $e';
        });
      }
    }
  }

  /// Initialise la liaison réseau de contrôle avec EECC Studio
  void _initStudioConnection() {
    _connectionService = StudioConnectionService(
      config: widget.config,
      onTallyChanged: (newTally) {
        if (mounted) setState(() => _tally = newTally);
      },
      onConnectionStatusChanged: (status) {
        if (mounted) {
          setState(() => _isConnected = status);
          if (status) {
            _startContinuousStreaming();
          } else {
            _stopContinuousStreaming();
          }
        }
      },
      onSlotAssigned: (slot, num) {
        if (mounted) {
          setState(() {
            _activeSlotName = slot;
            _activeSlotNumber = num;
          });
        }
      },
      onMetricsUpdated: (fps, kbps) {
        if (mounted) {
          setState(() {
            _measuredFps = fps;
            _measuredBitrate = kbps;
          });
        }
      },
      onRemoteCommandReceived: (command, payload) {
        if (command == 'toggle_torch') {
          _toggleTorch();
        }
      },
      onRejected: (code, message) {
        if (!mounted) return;
        setState(() => _linkMessage = message);
      },
      onStatus: (message) {
        if (!mounted) return;
        setState(() => _linkMessage = message);
      },
    );
    _connectionService.connect();
  }

  void _startContinuousStreaming() {
    _isStreamingFrames = true;
    _captureLoop();
  }

  void _stopContinuousStreaming() {
    _isStreamingFrames = false;
  }

  Future<void> _captureLoop() async {
    while (_isStreamingFrames && mounted) {
      if (_isConnected &&
          !_isCapturing &&
          _cameraController != null &&
          _cameraController!.value.isInitialized &&
          !_cameraController!.value.isTakingPicture) {
        _isCapturing = true;
        try {
          final xfile = await _cameraController!.takePicture();
          final bytes = await xfile.readAsBytes();
          _connectionService.sendVideoFrame(bytes);
          try {
            await File(xfile.path).delete();
          } catch (_) {}
        } catch (_) {
        } finally {
          _isCapturing = false;
        }
      }
      await Future.delayed(const Duration(milliseconds: 66)); // ~15 FPS
    }
  }

  /// Bascule entre la caméra arrière et la caméra avant (selfie)
  Future<void> _switchCamera() async {
    if (_availableCameras.length < 2) return;
    _selectedCameraIndex = (_selectedCameraIndex + 1) % _availableCameras.length;
    await _setupCameraController(_availableCameras[_selectedCameraIndex]);
  }

  /// Active ou désactive la torche physique du téléphone
  Future<void> _toggleTorch() async {
    if (_cameraController == null || !_cameraController!.value.isInitialized) return;
    try {
      if (_isTorchOn) {
        await _cameraController!.setFlashMode(FlashMode.off);
      } else {
        await _cameraController!.setFlashMode(FlashMode.torch);
      }
      setState(() => _isTorchOn = !_isTorchOn);
    } catch (_) {}
  }

  Color _getTallyColor() {
    if (!_isConnected) return EeccCamColors.tallyStandby;
    switch (_tally) {
      case TallyState.program:
        return EeccCamColors.tallyProgram; // Rouge solide
      case TallyState.preview:
        return EeccCamColors.tallyPreview; // Vert solide
      case TallyState.standby:
        return EeccCamColors.tallyStandby; // Gris solide
    }
  }

  String _getTallyLabel() {
    if (!_isConnected) {
      return _linkMessage ?? 'HORS LIGNE — la régie n\'a pas confirmé la session';
    }
    switch (_tally) {
      case TallyState.program:
        return '● À L\'ANTENNE : $_activeSlotName';
      case TallyState.preview:
        return '○ EN APERÇU : $_activeSlotName';
      case TallyState.standby:
        return 'CONNECTÉ : $_activeSlotName (STANDBY)';
    }
  }

  @override
  Widget build(BuildContext context) {
    final tallyColor = _getTallyColor();

    return Scaffold(
      backgroundColor: EeccCamColors.background, // Noir OLED pur
      body: Stack(
        children: [
          // 1. APERÇU VIDÉO CAMÉRA MATÉRIEL RÉEL
          if (_isCameraInitialized && _cameraController != null)
            GestureDetector(
              onScaleStart: (details) {
                _baseScale = _currentZoom;
              },
              onScaleUpdate: (details) async {
                final newZoom = (_baseScale * details.scale).clamp(_minZoom, _maxZoom);
                if (newZoom != _currentZoom) {
                  setState(() => _currentZoom = newZoom);
                  await _cameraController?.setZoomLevel(newZoom);
                }
              },
              child: SizedBox.expand(
                child: FittedBox(
                  fit: BoxFit.cover,
                  child: SizedBox(
                    width: _cameraController!.value.previewSize?.height ?? 1080,
                    height: _cameraController!.value.previewSize?.width ?? 1920,
                    child: CameraPreview(_cameraController!),
                  ),
                ),
              ),
            )
          else if (_cameraErrorMessage != null)
            Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Text(
                  _cameraErrorMessage!,
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: EeccCamColors.tallyProgram, fontSize: 16),
                ),
              ),
            )
          else
            const Center(
              child: CircularProgressIndicator(color: EeccCamColors.accent),
            ),

          // 2. CADRE TALLY SOLIDE EN BORDURE (0% dégradé, aplat solide pur)
          Positioned.fill(
            child: IgnorePointer(
              child: Container(
                decoration: BoxDecoration(
                  border: Border.all(
                    color: tallyColor,
                    width: _tally == TallyState.program ? 6 : 3,
                  ),
                ),
              ),
            ),
          ),

          // 3. BARRE TALLY SUPÉRIEURE SOLIDE
          SafeArea(
            child: Align(
              alignment: Alignment.topCenter,
              child: Container(
                margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                decoration: BoxDecoration(
                  color: tallyColor,
                  borderRadius: BorderRadius.circular(20),
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    CircleAvatar(
                      radius: 5,
                      backgroundColor: Colors.white,
                    ),
                    const SizedBox(width: 8),
                    Text(
                      _getTallyLabel(),
                      style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.w900,
                        fontSize: 13,
                        letterSpacing: 1,
                      ),
                    ),
                    const SizedBox(width: 12),
                    Text(
                      '${widget.config.displayName} · n°$_activeSlotNumber',
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 12,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),

          // 4. PANNEAU LATÉRAL DES CONTRÔLES TACTILES FLOTTANTS
          if (!_isScreenLocked)
            Positioned(
              right: 16,
              top: 100,
              bottom: 100,
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  _buildActionButton(
                    icon: _isTorchOn ? Icons.flash_on : Icons.flash_off,
                    label: 'Torche',
                    isActive: _isTorchOn,
                    onTap: _toggleTorch,
                  ),
                  const SizedBox(height: 16),
                  _buildActionButton(
                    icon: Icons.flip_camera_ios,
                    label: 'Bascule',
                    isActive: false,
                    onTap: _switchCamera,
                  ),
                  const SizedBox(height: 16),
                  _buildActionButton(
                    icon: _isMuted ? Icons.mic_off : Icons.mic,
                    label: _isMuted ? 'Coupé' : 'Micro',
                    isActive: !_isMuted,
                    onTap: () {
                      setState(() => _isMuted = !_isMuted);
                      if (_isMuted) _connectionService.updateAudioLevel(0);
                    },
                  ),
                  const SizedBox(height: 16),
                  _buildActionButton(
                    icon: Icons.lock_open,
                    label: 'Verrou',
                    isActive: false,
                    onTap: () {
                      setState(() => _isScreenLocked = true);
                    },
                  ),
                ],
              ),
            ),

          // 5. BANDEAU INFÉRIEUR DE TÉLÉMÉTRIE MATÉRIELLE SOLIDE
          Positioned(
            left: 0,
            right: 0,
            bottom: 0,
            child: Container(
              color: EeccCamColors.surface.withOpacity(0.92),
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
              child: SafeArea(
                top: false,
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Row(
                      children: [
                        const Icon(Icons.wifi, color: Colors.white, size: 18),
                        const SizedBox(width: 6),
                        Text(
                          _isConnected ? '$_activeSlotName (${widget.config.studioIp})' : (_linkMessage ?? 'Régie non confirmée'),
                          style: TextStyle(
                            color: _isConnected ? Colors.white : Colors.redAccent,
                            fontSize: 12,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        if (!_isConnected && _connectionService.canReconnect)
                          TextButton(
                            onPressed: () => _connectionService.reconnect(),
                            child: const Text('Reconnecter'),
                          ),
                      ],
                    ),
                    Row(
                      children: [
                        Text(
                          _measuredFps > 0 ? '$_measuredFps FPS' : 'FPS non mesuré',
                          style: const TextStyle(
                            color: EeccCamColors.telemetryGauge,
                            fontSize: 12,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        const SizedBox(width: 14),
                        Text(
                          _measuredBitrate > 0 ? '$_measuredBitrate kbps' : 'débit non mesuré',
                          style: const TextStyle(
                            color: EeccCamColors.telemetryGauge,
                            fontSize: 12,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        const SizedBox(width: 14),
                        IconButton(
                          icon: const Icon(Icons.close, color: Colors.white, size: 20),
                          onPressed: () => Navigator.of(context).pop(),
                          tooltip: 'Quitter la diffusion',
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildActionButton({
    required IconData icon,
    required String label,
    required bool isActive,
    required VoidCallback onTap,
  }) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        width: 52,
        height: 52,
        decoration: BoxDecoration(
          color: isActive ? EeccCamColors.accent : EeccCamColors.surface,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(
            color: isActive ? Colors.white : EeccCamColors.border,
            width: 1.5,
          ),
        ),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(icon, color: Colors.white, size: 20),
            const SizedBox(height: 2),
            Text(
              label,
              style: const TextStyle(color: Colors.white, fontSize: 9, fontWeight: FontWeight.bold),
            ),
          ],
        ),
      ),
    );
  }
}
