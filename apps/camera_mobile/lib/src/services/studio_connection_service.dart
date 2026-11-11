import 'dart:async';
import 'dart:convert';
import '../models/session_config.dart';
import '../models/device_telemetry.dart';

class StudioConnectionService {
  final StudioSessionConfig config;
  final Function(TallyState) onTallyChanged;
  final Function(bool isConnected) onConnectionStatusChanged;

  Timer? _telemetryTimer;
  bool _isConnected = false;
  TallyState _currentTally = TallyState.standby;

  StudioConnectionService({
    required this.config,
    required this.onTallyChanged,
    required this.onConnectionStatusChanged,
  });

  bool get isConnected => _isConnected;
  TallyState get currentTally => _currentTally;

  /// Démarre la connexion de contrôle avec EECC Studio
  Future<void> connect() async {
    // Tentative de connexion WebSocket locale
    try {
      // Simulation / Établissement du canal de contrôle
      await Future.delayed(const Duration(milliseconds: 600));
      _isConnected = true;
      onConnectionStatusChanged(true);

      // Démarrage de la boucle d'émission de télémétrie vers la régie
      _startTelemetryLoop();

      // Simulation de cycle Tally pour démonstration (Standby -> Preview -> Program)
      _startTallySimulator();
    } catch (e) {
      _isConnected = false;
      onConnectionStatusChanged(false);
    }
  }

  void _startTelemetryLoop() {
    _telemetryTimer?.cancel();
    _telemetryTimer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (!_isConnected) return;

      final telemetry = DeviceTelemetry(
        batteryLevel: 85,
        isCharging: false,
        audioLevelNormalized: 0.45,
        currentBitrateKbps: config.bitrateKbps,
        fps: config.fps,
        droppedFrames: 0,
        resolution: '1080p',
      );

      // Envoi du paquet de contrôle JSON vers EECC Studio
      final packet = {
        'type': 'telemetry',
        'stream_name': config.streamName,
        'payload': telemetry.toJson(),
      };
      // wsChannel.sink.add(jsonEncode(packet));
    });
  }

  void _startTallySimulator() {
    // Fait alterner le Tally pour valider les états visuels de la caméra
    Timer(const Duration(seconds: 4), () {
      if (!_isConnected) return;
      _currentTally = TallyState.preview;
      onTallyChanged(_currentTally);
    });

    Timer(const Duration(seconds: 8), () {
      if (!_isConnected) return;
      _currentTally = TallyState.program;
      onTallyChanged(_currentTally);
    });
  }

  void sendAudioLevel(double level) {
    // Envoi en temps réel pour le mixeur audio de Studio
  }

  void disconnect() {
    _telemetryTimer?.cancel();
    _isConnected = false;
    _currentTally = TallyState.standby;
    onConnectionStatusChanged(false);
    onTallyChanged(TallyState.standby);
  }
}
