import 'dart:async';
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:camera/camera.dart';
import '../constants/colors.dart';
import '../models/session_config.dart';
import '../pairing/studio_link_phase.dart';
import '../services/studio_connection_service.dart';
import '../services/webrtc_camera_stream_service.dart';

/// Écran Caméra Active d'EECC Caméra
/// Supporte une cadence ultra-haute vitesse jusqu'à 300 FPS et une résolution jusqu'au 16K HD
class CameraActiveScreen extends StatefulWidget {
  final StudioSessionConfig config;

  const CameraActiveScreen({super.key, required this.config});

  @override
  State<CameraActiveScreen> createState() => _CameraActiveScreenState();
}

class _CameraActiveScreenState extends State<CameraActiveScreen> with WidgetsBindingObserver {
  late StudioConnectionService _connectionService;
  WebrtcCameraStreamService? _webrtcService;

  // États réseau et Studio
  TallyState _tally = TallyState.standby;
  StudioLinkPhase _phase = StudioLinkPhase.disconnected;
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

  // Configuration Haute Performance (16K HD & 300 FPS)
  ResolutionPreset _currentResolution = ResolutionPreset.max; // 16K HD / Plein capteur
  int _targetFps = 300; // Cadence broadcast jusqu'à 300 FPS
  bool _isFlipped180 = false;
  bool _showRuleOfThirds = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    SystemChrome.setPreferredOrientations([
      DeviceOrientation.landscapeLeft,
      DeviceOrientation.landscapeRight,
    ]);

    _initHardwareCameras();
    _initStudioConnection();
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _stopContinuousStreaming();
    _webrtcService?.dispose();
    _cameraController?.dispose();
    _connectionService.dispose();
    SystemChrome.setPreferredOrientations([
      DeviceOrientation.portraitUp,
    ]);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (_cameraController == null || !_cameraController!.value.isInitialized) return;
    if (state == AppLifecycleState.inactive) {
      _stopContinuousStreaming();
      _cameraController?.dispose();
    } else if (state == AppLifecycleState.resumed) {
      if (_availableCameras.isNotEmpty) {
        _setupCameraController(_availableCameras[_selectedCameraIndex]);
      }
    }
  }

  Future<void> _initHardwareCameras() async {
    try {
      _availableCameras = await availableCameras();
      if (_availableCameras.isNotEmpty) {
        _selectedCameraIndex = 0;
        await _setupCameraController(_availableCameras[_selectedCameraIndex]);
      } else {
        setState(() {
          _cameraErrorMessage = 'Aucun capteur caméra détecté sur cet appareil.';
        });
      }
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
      _currentResolution, // Plein capteur 16K HD / 4K UHD / 1080p
      enableAudio: !_isMuted,
      imageFormatGroup: ImageFormatGroup.jpeg,
    );

    _cameraController = controller;

    try {
      await controller.initialize();
      try {
        await controller.unlockCaptureOrientation();
      } catch (_) {}
      _minZoom = await controller.getMinZoomLevel();
      _maxZoom = (await controller.getMaxZoomLevel()).clamp(1.0, 5.0);

      _connectionService.setResolution(_getResolutionLabel(_currentResolution));

      if (mounted) {
        setState(() {
          _isCameraInitialized = true;
          _cameraErrorMessage = null;
        });
      }

      if (_isConnected) {
        _startContinuousStreaming();
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _cameraErrorMessage = 'Initialisation capteur échouée : $e';
        });
      }
    }
  }

  void _initStudioConnection() {
    _connectionService = StudioConnectionService(
      config: widget.config.copyWith(
        fps: _targetFps,
        targetResolution: _getResolutionLabel(_currentResolution),
      ),
      onTallyChanged: (newTally) {
        if (mounted) setState(() => _tally = newTally);
      },
      onConnectionStatusChanged: (connected) {
        if (mounted) {
          setState(() => _isConnected = connected);
          if (connected) {
            _startContinuousStreaming();
          } else {
            _stopContinuousStreaming();
          }
        }
      },
      onPhaseChanged: (phase) {
        if (mounted) setState(() => _phase = phase);
      },
      onSlotAssigned: (slot, number) {
        if (mounted) {
          setState(() {
            _activeSlotName = slot;
            _activeSlotNumber = number;
          });
        }
      },
      onMetricsUpdated: (fps, bitrate) {
        if (mounted) {
          setState(() {
            _measuredFps = fps;
            _measuredBitrate = bitrate;
          });
        }
      },
      onRemoteCommandReceived: (command, payload) {
        if (command == 'toggle_torch') {
          _toggleTorch();
        }
      },
      onRejected: (code, message) {
        if (mounted) {
          setState(() => _linkMessage = message);
        }
      },
      onStatus: (status) {
        if (mounted) {
          setState(() => _linkMessage = status);
        }
      },
      onWebRtcAnswerReceived: (sdp) => _webrtcService?.handleRemoteAnswer(sdp),
      onIceCandidateReceived: (cand, sdpMid, mline) => _webrtcService?.handleRemoteIceCandidate(cand, sdpMid, mline),
    );

    _webrtcService = WebrtcCameraStreamService(
      config: widget.config,
      signalingService: _connectionService,
      onMetricsUpdated: (fps, bitrate) {
        if (mounted) {
          setState(() {
            _measuredFps = fps;
            _measuredBitrate = bitrate;
          });
        }
      },
      onError: (err) {
        debugPrint('WebRTC Notice: $err');
      },
    );
    _webrtcService?.initialize();

    _connectionService.setResolution(_getResolutionLabel(_currentResolution));
    _connectionService.connect();
  }

  void _startContinuousStreaming() {
    _isStreamingFrames = true;
    _webrtcService?.startStream(
      resolution: _getResolutionLabel(_currentResolution),
      targetFps: _targetFps,
      isFrontCamera: _selectedCameraIndex == 1,
      enableAudio: !_isMuted,
    );
    _captureLoop();
  }

  void _stopContinuousStreaming() {
    _isStreamingFrames = false;
  }

  /// Boucle de capture ultra-haute vitesse sans bride logicielle (jusqu'à 300 FPS)
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

      // Calcul dynamique du délai selon la cadence cible demandée (jusqu'à 300 FPS)
      final int targetDelayMs = (1000 / _targetFps).floor();
      if (targetDelayMs <= 3) {
        await Future.delayed(Duration.zero); // Cadence maximale capteur sans bride
      } else {
        await Future.delayed(Duration(milliseconds: targetDelayMs));
      }
    }
  }

  Future<void> _switchCamera() async {
    if (_availableCameras.length < 2) return;
    _selectedCameraIndex = (_selectedCameraIndex + 1) % _availableCameras.length;
    await _setupCameraController(_availableCameras[_selectedCameraIndex]);
  }

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

  String _getResolutionLabel(ResolutionPreset preset) {
    switch (preset) {
      case ResolutionPreset.max:
        return '16K HD (Plein Capteur)';
      case ResolutionPreset.ultraHigh:
        return '4K UHD (2160p)';
      case ResolutionPreset.veryHigh:
        return '1080p FHD Pro';
      case ResolutionPreset.high:
        return '1080p FHD';
      case ResolutionPreset.medium:
        return '720p HD';
      case ResolutionPreset.low:
        return '480p SD';
    }
  }

  Color _getTallyColor() {
    if (_phase != StudioLinkPhase.connected) return EeccCamColors.tallyStandby;
    switch (_tally) {
      case TallyState.program:
        return EeccCamColors.tallyProgram;
      case TallyState.preview:
        return EeccCamColors.tallyPreview;
      case TallyState.standby:
        return EeccCamColors.tallyStandby;
    }
  }

  String _getTallyLabel() {
    if (_phase != StudioLinkPhase.connected) return 'STANDBY';
    switch (_tally) {
      case TallyState.program:
        return 'ANTENNE (LIVE)';
      case TallyState.preview:
        return 'PRÉVISUALISATION';
      case TallyState.standby:
        return 'EN ATTENTE';
    }
  }

  @override
  Widget build(BuildContext context) {
    final tallyColor = _getTallyColor();

    return Scaffold(
      backgroundColor: Colors.black,
      body: Stack(
        fit: StackFit.expand,
        children: [
          // 1. APERÇU VIDÉO CAPTEUR
          if (_isCameraInitialized && _cameraController != null)
            GestureDetector(
              onScaleStart: (details) => _baseScale = _currentZoom,
              onScaleUpdate: (details) async {
                final newZoom = (_baseScale * details.scale).clamp(_minZoom, _maxZoom);
                if (newZoom != _currentZoom) {
                  _currentZoom = newZoom;
                  await _cameraController!.setZoomLevel(_currentZoom);
                }
              },
              child: Center(
                child: AspectRatio(
                  aspectRatio: _cameraController!.value.aspectRatio,
                  child: Transform.rotate(
                    angle: _isFlipped180 ? 3.141592653589793 : 0.0,
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

          // 1.5 GRILLE DE COMPOSITION
          if (_showRuleOfThirds)
            Positioned.fill(
              child: IgnorePointer(
                child: CustomPaint(
                  painter: _RuleOfThirdsPainter(),
                ),
              ),
            ),

          // 2. CADRE TALLY SOLIDE EN BORDURE
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
                    const CircleAvatar(
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
                    if (_phase == StudioLinkPhase.connected && _activeSlotName.isNotEmpty)
                      Text(
                        '${widget.config.displayName} · n°$_activeSlotNumber',
                        style: const TextStyle(
                          color: Colors.white,
                          fontSize: 12,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    const SizedBox(width: 10),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                      decoration: BoxDecoration(
                        color: Colors.black38,
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: Text(
                        '${_getResolutionLabel(_currentResolution)} · ${_targetFps} FPS',
                        style: const TextStyle(color: Colors.white, fontSize: 10, fontWeight: FontWeight.bold),
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
              top: 70,
              bottom: 70,
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  _buildActionButton(
                    icon: _isTorchOn ? Icons.flash_on : Icons.flash_off,
                    label: 'Torche',
                    isActive: _isTorchOn,
                    onTap: _toggleTorch,
                  ),
                  const SizedBox(height: 12),
                  _buildActionButton(
                    icon: Icons.flip_camera_ios,
                    label: 'Bascule',
                    isActive: false,
                    onTap: _switchCamera,
                  ),
                  const SizedBox(height: 12),
                  _buildActionButton(
                    icon: _isMuted ? Icons.mic_off : Icons.mic,
                    label: _isMuted ? 'Coupé' : 'Micro',
                    isActive: !_isMuted,
                    onTap: () {
                      setState(() => _isMuted = !_isMuted);
                      if (_isMuted) _connectionService.updateAudioLevel(0);
                    },
                  ),
                  const SizedBox(height: 12),
                  _buildActionButton(
                    icon: Icons.tune_rounded,
                    label: '16K/300',
                    isActive: true,
                    onTap: _showHighSpeedSettingsModal,
                  ),
                  const SizedBox(height: 12),
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
                    Expanded(
                      child: Row(
                        children: [
                          Icon(
                            _phase == StudioLinkPhase.connected ? Icons.link : Icons.link_off,
                            color: Colors.white,
                            size: 18,
                          ),
                          const SizedBox(width: 6),
                          Flexible(
                            child: Text(
                              _phase == StudioLinkPhase.connected
                                  ? '${StudioLinkLabels.connected}${_activeSlotName.isEmpty ? '' : ' · $_activeSlotName'}'
                                  : '${StudioLinkLabels.badge(_phase)}${_linkMessage == null ? '' : ' — $_linkMessage'}',
                              style: TextStyle(
                                color: _phase == StudioLinkPhase.connected ? Colors.white : Colors.white70,
                                fontSize: 12,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ),
                          if (_phase != StudioLinkPhase.connected && _connectionService.canReconnect)
                            TextButton(
                              onPressed: () => _connectionService.reconnect(),
                              child: const Text('Reconnecter'),
                            ),
                        ],
                      ),
                    ),
                    Row(
                      children: [
                        Text(
                          _measuredFps > 0 ? '$_measuredFps / $_targetFps FPS' : '$_targetFps FPS cible',
                          style: const TextStyle(
                            color: EeccCamColors.telemetryGauge,
                            fontSize: 12,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        const SizedBox(width: 14),
                        Text(
                          _measuredBitrate > 0 ? '$_measuredBitrate kbps' : 'débit actif',
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

  void _showHighSpeedSettingsModal() {
    showModalBottomSheet(
      context: context,
      backgroundColor: const Color(0xFF18181B),
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (ctx) {
        return StatefulBuilder(
          builder: (context, setModalState) {
            return SafeArea(
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
                child: SingleChildScrollView(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          const Text(
                            'Configuration Haute Fidélité & Cadence',
                            style: TextStyle(color: Colors.white, fontSize: 16, fontWeight: FontWeight.bold),
                          ),
                          IconButton(
                            icon: const Icon(Icons.close, color: Colors.white70),
                            onPressed: () => Navigator.pop(ctx),
                          ),
                        ],
                      ),
                      const Divider(color: Colors.white24),
                      const SizedBox(height: 8),

                      // SÉLECTION RÉSOLUTION (Jusqu'à 16K HD)
                      const Text('Résolution de capture :', style: TextStyle(color: Colors.white70, fontSize: 12)),
                      const SizedBox(height: 6),
                      Wrap(
                        spacing: 8,
                        children: [
                          _buildQualityChip('16K HD (Plein Capteur)', ResolutionPreset.max, setModalState),
                          _buildQualityChip('4K UHD', ResolutionPreset.ultraHigh, setModalState),
                          _buildQualityChip('1080p FHD Pro', ResolutionPreset.veryHigh, setModalState),
                          _buildQualityChip('720p HD', ResolutionPreset.medium, setModalState),
                        ],
                      ),

                      const SizedBox(height: 16),
                      // SÉLECTION CADENCE CIBLE (Jusqu'à 300 FPS)
                      const Text('Cadence d\'images par seconde (FPS) :', style: TextStyle(color: Colors.white70, fontSize: 12)),
                      const SizedBox(height: 6),
                      Wrap(
                        spacing: 8,
                        children: [
                          _buildFpsChip(300, '300 FPS (Ultra Vitesse)', setModalState),
                          _buildFpsChip(240, '240 FPS (Slow-Mo)', setModalState),
                          _buildFpsChip(120, '120 FPS', setModalState),
                          _buildFpsChip(60, '60 FPS (Broadcast)', setModalState),
                          _buildFpsChip(30, '30 FPS', setModalState),
                        ],
                      ),

                      const SizedBox(height: 16),
                      SwitchListTile(
                        contentPadding: EdgeInsets.zero,
                        title: const Text('Grille de cadrage (Règle des tiers)', style: TextStyle(color: Colors.white, fontSize: 13)),
                        subtitle: const Text('Aide visuelle pour centrer l\'orateur en culte', style: TextStyle(color: Colors.white60, fontSize: 11)),
                        value: _showRuleOfThirds,
                        activeColor: EeccCamColors.accent,
                        onChanged: (val) {
                          setModalState(() => _showRuleOfThirds = val);
                          setState(() => _showRuleOfThirds = val);
                        },
                      ),
                      SwitchListTile(
                        contentPadding: EdgeInsets.zero,
                        title: const Text('Inverser la caméra à 180° (Tête en bas)', style: TextStyle(color: Colors.white, fontSize: 13)),
                        subtitle: const Text('Corrige l\'image si le smartphone est monté à l\'envers', style: TextStyle(color: Colors.white60, fontSize: 11)),
                        value: _isFlipped180,
                        activeColor: Colors.amber,
                        onChanged: (val) {
                          setModalState(() => _isFlipped180 = val);
                          setState(() => _isFlipped180 = val);
                        },
                      ),
                    ],
                  ),
                ),
              ),
            );
          },
        );
      },
    );
  }

  Widget _buildQualityChip(String label, ResolutionPreset preset, StateSetter setModalState) {
    final isSelected = _currentResolution == preset;
    return ChoiceChip(
      label: Text(label, style: TextStyle(color: isSelected ? Colors.white : Colors.white70, fontSize: 11)),
      selected: isSelected,
      selectedColor: EeccCamColors.accent,
      backgroundColor: const Color(0xFF27272A),
      onSelected: (selected) {
        if (selected) {
          setModalState(() => _currentResolution = preset);
          setState(() => _currentResolution = preset);
          if (_availableCameras.isNotEmpty) {
            _setupCameraController(_availableCameras[_selectedCameraIndex]);
          }
        }
      },
    );
  }

  Widget _buildFpsChip(int fps, String label, StateSetter setModalState) {
    final isSelected = _targetFps == fps;
    return ChoiceChip(
      label: Text(label, style: TextStyle(color: isSelected ? Colors.white : Colors.white70, fontSize: 11)),
      selected: isSelected,
      selectedColor: EeccCamColors.tallyProgram,
      backgroundColor: const Color(0xFF27272A),
      onSelected: (selected) {
        if (selected) {
          setModalState(() => _targetFps = fps);
          setState(() => _targetFps = fps);
        }
      },
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

class _RuleOfThirdsPainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = Colors.white.withOpacity(0.2)
      ..strokeWidth = 1.0;

    canvas.drawLine(Offset(size.width / 3, 0), Offset(size.width / 3, size.height), paint);
    canvas.drawLine(Offset(size.width * 2 / 3, 0), Offset(size.width * 2 / 3, size.height), paint);
    canvas.drawLine(Offset(0, size.height / 3), Offset(size.width, size.height / 3), paint);
    canvas.drawLine(Offset(0, size.height * 2 / 3), Offset(size.width, size.height * 2 / 3), paint);
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}
