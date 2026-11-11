import 'package:flutter/material.dart';
import 'dart:math';
import '../constants/colors.dart';
import '../models/session_config.dart';
import '../services/studio_connection_service.dart';

class CameraActiveScreen extends StatefulWidget {
  final StudioSessionConfig config;

  const CameraActiveScreen({super.key, required this.config});

  @override
  State<CameraActiveScreen> createState() => _CameraActiveScreenState();
}

class _CameraActiveScreenState extends State<CameraActiveScreen> {
  late StudioConnectionService _service;
  TallyState _tally = TallyState.standby;
  bool _isConnected = false;

  // Contrôles locaux
  bool _isTorchOn = false;
  bool _isFrontCamera = false;
  bool _isMuted = false;
  bool _isLocked = false;
  double _audioLevel = 0.5;

  @override
  void initState() {
    super.initState();
    _service = StudioConnectionService(
      config: widget.config,
      onTallyChanged: (newTally) {
        if (mounted) setState(() => _tally = newTally);
      },
      onConnectionStatusChanged: (status) {
        if (mounted) setState(() => _isConnected = status);
      },
    );
    _service.connect();
  }

  @override
  void dispose() {
    _service.disconnect();
    super.dispose();
  }

  Color _getTallyColor() {
    switch (_tally) {
      case TallyState.program:
        return EeccCamColors.tallyProgram; // Rouge
      case TallyState.preview:
        return EeccCamColors.tallyPreview; // Vert
      case TallyState.standby:
        return EeccCamColors.tallyStandby; // Gris
    }
  }

  String _getTallyText() {
    switch (_tally) {
      case TallyState.program:
        return 'À L\'ANTENNE (DIRECT)';
      case TallyState.preview:
        return 'EN APERÇU (PROCHAIN PLAN)';
      case TallyState.standby:
        return 'CONNECTÉ (STANDBY)';
    }
  }

  @override
  Widget build(BuildContext context) {
    final tallyColor = _getTallyColor();

    return Scaffold(
      backgroundColor: Colors.black,
      body: Stack(
        children: [
          // 1. APERÇU VIDÉO CAMÉRA (Simulé ou flux natif CameraController)
          Container(
            width: double.infinity,
            height: double.infinity,
            color: const Color(0xFF111827),
            child: Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(
                    _isFrontCamera ? Icons.camera_front_rounded : Icons.videocam_rounded,
                    size: 80,
                    color: Colors.white24,
                  ),
                  const SizedBox(height: 12),
                  Text(
                    widget.config.streamName,
                    style: const TextStyle(color: Colors.white60, fontSize: 16, fontWeight: FontWeight.bold),
                  ),
                  Text(
                    'Flux vers ${widget.config.studioIp}:${widget.config.port}',
                    style: const TextStyle(color: Colors.white38, fontSize: 13),
                  ),
                ],
              ),
            ),
          ),

          // 2. CADRE TALLY LUMINEUX AUTOUR DE L'ÉCRAN
          Container(
            decoration: BoxDecoration(
              border: Border.all(
                color: tallyColor,
                width: _tally == TallyState.program ? 6 : 3,
              ),
            ),
          ),

          // 3. BARRE TALLY SUPÉRIEURE
          SafeArea(
            child: Align(
              alignment: Alignment.topCenter,
              child: Container(
                margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                decoration: BoxDecoration(
                  color: tallyColor,
                  borderRadius: BorderRadius.circular(24),
                  boxShadow: [
                    BoxShadow(
                      color: tallyColor.withOpacity(0.5),
                      blurRadius: 10,
                      spreadRadius: 2,
                    ),
                  ],
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
                      _getTallyText(),
                      style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.bold,
                        fontSize: 12,
                        letterSpacing: 0.5,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),

          // 4. OVERLAY TÉLÉMÉTRIE (HAUT GAUCHE / HAUT DROITE)
          SafeArea(
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 55),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  // Télémétrie Gauche : Batterie & Débit
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                    decoration: BoxDecoration(
                      color: Colors.black.withOpacity(0.6),
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: const Row(
                      children: [
                        Icon(Icons.battery_5_bar_rounded, size: 16, color: Colors.greenAccent),
                        SizedBox(width: 4),
                        Text('85%  •  4500 kbps  •  30 FPS', style: TextStyle(color: Colors.white, fontSize: 11)),
                      ],
                    ),
                  ),

                  // Résolution
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
                    decoration: BoxDecoration(
                      color: Colors.black.withOpacity(0.6),
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: const Text(
                      '1080p FHD',
                      style: TextStyle(color: Colors.white, fontSize: 11, fontWeight: FontWeight.bold),
                    ),
                  ),
                ],
              ),
            ),
          ),

          // 5. VU-MÈTRE AUDIO LATÉRAL (DROITE)
          Positioned(
            right: 16,
            top: 130,
            bottom: 120,
            child: Container(
              width: 14,
              padding: const EdgeInsets.all(2),
              decoration: BoxDecoration(
                color: Colors.black.withOpacity(0.6),
                borderRadius: BorderRadius.circular(7),
                border: Border.all(color: Colors.white24),
              ),
              child: Stack(
                alignment: Alignment.bottomCenter,
                children: [
                  Container(
                    width: 10,
                    height: 180 * _audioLevel,
                    decoration: BoxDecoration(
                      gradient: const LinearGradient(
                        colors: [Colors.green, Colors.yellow, Colors.red],
                        begin: Alignment.bottomCenter,
                        end: Alignment.topCenter,
                      ),
                      borderRadius: BorderRadius.circular(5),
                    ),
                  ),
                ],
              ),
            ),
          ),

          // 6. BARRE DE CONTRÔLES INFÉRIEURE
          Align(
            alignment: Alignment.bottomCenter,
            child: SafeArea(
              child: Container(
                margin: const EdgeInsets.all(16),
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                decoration: BoxDecoration(
                  color: EeccCamColors.surface.withOpacity(0.9),
                  borderRadius: BorderRadius.circular(20),
                  border: Border.all(color: Colors.white12),
                ),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceAround,
                  children: [
                    // Bascule caméra
                    IconButton(
                      icon: Icon(
                        Icons.flip_camera_ios_rounded,
                        color: _isFrontCamera ? EeccCamColors.primaryLight : Colors.white,
                      ),
                      onPressed: () => setState(() => _isFrontCamera = !_isFrontCamera),
                    ),

                    // Torche / Flash
                    IconButton(
                      icon: Icon(
                        _isTorchOn ? Icons.flash_on_rounded : Icons.flash_off_rounded,
                        color: _isTorchOn ? Colors.amber : Colors.white,
                      ),
                      onPressed: () => setState(() => _isTorchOn = !_isTorchOn),
                    ),

                    // Mute micro
                    IconButton(
                      icon: Icon(
                        _isMuted ? Icons.mic_off_rounded : Icons.mic_rounded,
                        color: _isMuted ? Colors.redAccent : Colors.white,
                      ),
                      onPressed: () => setState(() => _isMuted = !_isMuted),
                    ),

                    // Verrouillage Exposition/Focus
                    IconButton(
                      icon: Icon(
                        _isLocked ? Icons.lock_rounded : Icons.lock_open_rounded,
                        color: _isLocked ? Colors.amber : Colors.white,
                      ),
                      onPressed: () => setState(() => _isLocked = !_isLocked),
                    ),

                    // Déconnexion
                    IconButton(
                      icon: const Icon(Icons.power_settings_new_rounded, color: Colors.redAccent),
                      onPressed: () {
                        _service.disconnect();
                        Navigator.pop(context);
                      },
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
}
