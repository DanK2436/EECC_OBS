import 'dart:async';

import 'package:battery_plus/battery_plus.dart';
import 'package:flutter/material.dart';
import 'package:flutter_webrtc/flutter_webrtc.dart';

import '../constants/colors.dart';
import '../pairing/pairing_ticket.dart';
import '../services/lan_media_link.dart';

class CameraActiveScreen extends StatefulWidget {
  final PairingTicket ticket;

  const CameraActiveScreen({super.key, required this.ticket});

  @override
  State<CameraActiveScreen> createState() => _CameraActiveScreenState();
}

class _CameraActiveScreenState extends State<CameraActiveScreen> with WidgetsBindingObserver {
  late final LanMediaLink _link;
  final Battery _battery = Battery();
  bool _permissionGranted = false;
  bool _muted = false;
  bool _torch = false;
  bool _locked = false;
  String? _tally;
  int? _batteryLevel;
  String _status = 'Autorisation caméra et micro en attente.';
  double _zoom = 1;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _link = LanMediaLink(widget.ticket);
    _link.onStatus = (status) {
      if (mounted) setState(() => _status = status);
    };
    _link.onTally = (tally) {
      if (mounted) setState(() => _tally = tally);
    };
    _link.onBattery = (level) {
      if (mounted) setState(() => _batteryLevel = level);
    };
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    unawaited(_link.close());
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.paused && _permissionGranted) {
      setState(() => _status = 'Application en arrière-plan : l\'envoi est interrompu.');
    }
  }

  Future<void> _start() async {
    try {
      await _link.openCapture();
      if (!mounted) return;
      setState(() => _permissionGranted = true);
      await _link.connect(readBattery: () => _battery.batteryLevel);
    } catch (_) {
      if (mounted) setState(() => _permissionGranted = _link.localStream != null);
    }
  }

  Color _tallyColor() {
    switch (_tally) {
      case 'program':
        return EeccCamColors.tallyProgram;
      case 'preview':
        return EeccCamColors.tallyPreview;
      case 'standby':
        return EeccCamColors.tallyStandby;
      default:
        return EeccCamColors.border;
    }
  }

  String _tallyLabel() {
    switch (_tally) {
      case 'program':
        return 'TALLY PROGRAMME';
      case 'preview':
        return 'TALLY APERÇU';
      case 'standby':
        return 'TALLY ATTENTE';
      default:
        return 'TALLY NON REÇU';
    }
  }

  @override
  Widget build(BuildContext context) {
    if (!_permissionGranted) {
      return Scaffold(
        backgroundColor: EeccCamColors.background,
        appBar: AppBar(backgroundColor: Colors.transparent, foregroundColor: Colors.white, title: const Text('Caméra et micro')),
        body: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'EECC Caméra va demander l\'accès à la caméra et au micro pour envoyer l\'image et le son à EECC Studio sur le réseau local. Rien n\'est envoyé vers un service externe.',
                style: TextStyle(color: Colors.white, fontSize: 16),
              ),
              const SizedBox(height: 16),
              Text(
                'Studio : ${widget.ticket.ip}:${widget.ticket.port}',
                style: const TextStyle(color: EeccCamColors.textSecondary),
              ),
              const SizedBox(height: 12),
              Text(_status, style: const TextStyle(color: EeccCamColors.tallyProgram)),
              const Spacer(),
              SizedBox(
                width: double.infinity,
                height: 52,
                child: ElevatedButton(
                  style: ElevatedButton.styleFrom(backgroundColor: EeccCamColors.primary),
                  onPressed: _start,
                  child: const Text('Autoriser la caméra et le micro', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                ),
              ),
            ],
          ),
        ),
      );
    }

    return Scaffold(
      backgroundColor: Colors.black,
      body: Stack(
        children: [
          Positioned.fill(child: RTCVideoView(_link.localRenderer, objectFit: RTCVideoViewObjectFit.RTCVideoViewObjectFitCover)),
          Positioned.fill(
            child: IgnorePointer(
              child: Container(decoration: BoxDecoration(border: Border.all(color: _tallyColor(), width: _tally == 'program' ? 6 : 3))),
            ),
          ),
          SafeArea(
            child: Align(
              alignment: Alignment.topCenter,
              child: Container(
                margin: const EdgeInsets.all(12),
                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                decoration: BoxDecoration(color: _tallyColor(), borderRadius: BorderRadius.circular(20)),
                child: Text(_tallyLabel(), style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w900)),
              ),
            ),
          ),
          if (!_locked)
            Positioned(
              right: 12,
              top: 120,
              child: Column(
                children: [
                  _button(_torch ? Icons.flash_on : Icons.flash_off, 'Torche', () async {
                    final next = !_torch;
                    final ok = await _link.setTorch(next);
                    if (ok && mounted) setState(() => _torch = next);
                  }),
                  _button(Icons.flip_camera_ios, 'Bascule', _link.switchCamera),
                  _button(_muted ? Icons.mic_off : Icons.mic, _muted ? 'Coupé' : 'Micro', () async {
                    final next = !_muted;
                    await _link.setMicMuted(next);
                    if (mounted) setState(() => _muted = next);
                  }),
                  _button(Icons.refresh, 'Relancer', _link.reconnectMedia),
                  _button(Icons.lock_open, 'Verrou', () => setState(() => _locked = true)),
                ],
              ),
            ),
          Positioned(
            left: 0,
            right: 0,
            bottom: 0,
            child: Container(
              color: EeccCamColors.surface.withOpacity(0.94),
              padding: const EdgeInsets.fromLTRB(12, 8, 12, 8),
              child: SafeArea(
                top: false,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(_status, style: const TextStyle(color: Colors.white, fontSize: 12)),
                    const SizedBox(height: 4),
                    Text(
                      _batteryLevel == null ? 'Batterie non reçue' : 'Batterie $_batteryLevel %',
                      style: const TextStyle(color: EeccCamColors.textSecondary, fontSize: 12),
                    ),
                    Row(
                      children: [
                        const Text('Zoom', style: TextStyle(color: Colors.white, fontSize: 12)),
                        Expanded(
                          child: Slider(
                            value: _zoom,
                            min: 1,
                            max: 4,
                            onChanged: (value) {
                              setState(() => _zoom = value);
                              _link.setZoom(value);
                            },
                          ),
                        ),
                        IconButton(
                          onPressed: () => Navigator.of(context).pop(),
                          icon: const Icon(Icons.close, color: Colors.white),
                          tooltip: 'Fermer la liaison',
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

  Widget _button(IconData icon, String label, VoidCallback onTap) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: GestureDetector(
        onTap: onTap,
        child: Container(
          width: 56,
          height: 56,
          decoration: BoxDecoration(
            color: EeccCamColors.surface,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: EeccCamColors.border),
          ),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(icon, color: Colors.white, size: 18),
              Text(label, style: const TextStyle(color: Colors.white, fontSize: 9)),
            ],
          ),
        ),
      ),
    );
  }
}
