import 'dart:async';
import 'dart:convert';
import 'dart:math';

import 'package:battery_plus/battery_plus.dart';
import 'package:web_socket_channel/web_socket_channel.dart';

import '../models/device_telemetry.dart';
import '../models/session_config.dart';
import '../pairing/reconnect_policy.dart';

/// Liaison WebSocket réelle avec EECC Studio.
/// L'état connecté n'est posé qu'après un handshake_ack de la régie.
class StudioConnectionService {
  final StudioSessionConfig config;
  final void Function(TallyState tally) onTallyChanged;
  final void Function(bool isConnected) onConnectionStatusChanged;
  final void Function(String command, dynamic payload)? onRemoteCommandReceived;
  final void Function(String assignedSlot, int slotNumber)? onSlotAssigned;
  final void Function(int realFps, int realBitrateKbps)? onMetricsUpdated;
  final void Function(String code, String message)? onRejected;
  final void Function(String message)? onStatus;

  final Battery _battery = Battery();
  final String _deviceId = 'phone-${Random.secure().nextInt(1 << 32)}';
  WebSocketChannel? _wsChannel;
  StreamSubscription? _wsSubscription;
  Timer? _telemetryTimer;
  Timer? _heartbeatTimer;
  Timer? _handshakeTimeoutTimer;
  Timer? _retryTimer;

  bool _isConnected = false;
  bool _userLeft = false;
  int _retry = 0;
  int _generation = 0;
  String? _sessionId;
  String? _lastRejectCode;
  TallyState _currentTally = TallyState.standby;
  String _assignedSlot = '';
  int _slotNumber = 0;

  int _lastBatteryLevel = -1;
  bool _isCharging = false;
  double _currentAudioLevel = 0.0;
  int _framesSentThisSecond = 0;
  int _bytesSentThisSecond = 0;
  int _currentRealFps = 0;
  int _currentRealBitrateKbps = 0;
  DateTime _lastRateCalculation = DateTime.now();

  StudioConnectionService({
    required this.config,
    required this.onTallyChanged,
    required this.onConnectionStatusChanged,
    this.onRemoteCommandReceived,
    this.onSlotAssigned,
    this.onMetricsUpdated,
    this.onRejected,
    this.onStatus,
  });

  bool get isConnected => _isConnected;
  bool get canReconnect => _sessionId != null && !_userLeft;
  String? get sessionId => _sessionId;
  TallyState get currentTally => _currentTally;
  String get assignedSlot => _assignedSlot;
  int get slotNumber => _slotNumber;
  int get realFps => _currentRealFps;
  int get realBitrateKbps => _currentRealBitrateKbps;

  Future<void> connect({bool useSession = false}) async {
    _userLeft = false;
    if (!useSession) {
      _sessionId = null;
      _retry = 0;
    }
    await _open(useSession: useSession && _sessionId != null);
  }

  Future<void> reconnect() => connect(useSession: true);

  Future<void> _open({required bool useSession}) async {
    final generation = ++_generation;
    _retryTimer?.cancel();
    _handshakeTimeoutTimer?.cancel();
    _closeSocket();
    _isConnected = false;
    onConnectionStatusChanged(false);
    _lastRejectCode = null;

    final token = useSession ? _sessionId : config.token;
    if (token == null || token.isEmpty) {
      onRejected?.call('session_unknown', 'Session inconnue. Scannez un nouveau QR.');
      return;
    }

    try {
      final channel = WebSocketChannel.connect(Uri.parse(config.controlWebSocketUrl));
      _wsChannel = channel;
      _wsSubscription = channel.stream.listen(
        (data) {
          if (generation == _generation) _handleIncomingMessage(data);
        },
        onDone: () {
          if (generation == _generation) _onSocketClosed();
        },
        onError: (_) {
          if (generation == _generation) _onSocketClosed();
        },
        cancelOnError: true,
      );
      channel.sink.add(jsonEncode({
        'type': 'handshake',
        'role': 'camera_source',
        'token': token,
        'reconnect': useSession,
        'display_name': config.displayName,
        'device_model': 'EECC Caméra',
        'device_id': _deviceId,
        'protocol_version': 'eecc-lan-1',
      }));
      _handshakeTimeoutTimer?.cancel();
      _handshakeTimeoutTimer = Timer(const Duration(seconds: 8), () {
        if (_isConnected || _userLeft) return;
        _lastRejectCode = 'timeout';
        onRejected?.call(
          'timeout',
          'La régie n\'a pas répondu. Vérifiez l\'adresse, le pare-feu du PC et que le téléphone n\'est pas isolé du Wi-Fi.',
        );
        _closeSocket();
        onConnectionStatusChanged(false);
      });
    } catch (error) {
      onRejected?.call('network', 'Connexion impossible : $error');
      onConnectionStatusChanged(false);
    }
  }

  void sendVideoFrame(List<int> jpegBytes) {
    if (!_isConnected || _wsChannel == null) return;
    try {
      _wsChannel?.sink.add(jpegBytes);
      _framesSentThisSecond++;
      _bytesSentThisSecond += jpegBytes.length;
      final elapsedMs = DateTime.now().difference(_lastRateCalculation).inMilliseconds;
      if (elapsedMs >= 1000) {
        _currentRealFps = ((_framesSentThisSecond * 1000) / elapsedMs).round();
        _currentRealBitrateKbps = ((_bytesSentThisSecond * 8) / elapsedMs).round();
        _framesSentThisSecond = 0;
        _bytesSentThisSecond = 0;
        _lastRateCalculation = DateTime.now();
        onMetricsUpdated?.call(_currentRealFps, _currentRealBitrateKbps);
      }
    } catch (_) {
      _onSocketClosed();
    }
  }

  void _handleIncomingMessage(dynamic rawData) {
    if (rawData is! String) return;
    try {
      final message = jsonDecode(rawData) as Map<String, dynamic>;
      final type = message['type'] as String?;
      if (type == 'handshake_ack') {
        _handshakeTimeoutTimer?.cancel();
        _retry = 0;
        _lastRejectCode = null;
        _sessionId = message['session_id'] as String? ?? _sessionId;
        _assignedSlot = message['assigned_slot'] as String? ?? '';
        _slotNumber = message['slot_number'] as int? ?? 0;
        if (_assignedSlot.isNotEmpty) {
          onSlotAssigned?.call(_assignedSlot, _slotNumber);
        }
        _isConnected = true;
        onConnectionStatusChanged(true);
        onStatus?.call('Connecté à ${message['studio_name'] ?? config.studioName} · $_assignedSlot');
        _initBatteryMonitoring();
        _startTelemetryLoop();
        _startHeartbeat();
      } else if (type == 'handshake_reject') {
        _handshakeTimeoutTimer?.cancel();
        _isConnected = false;
        final code = message['code'] as String? ?? 'rejected';
        final text = message['message'] as String? ?? 'La régie a refusé la connexion.';
        _lastRejectCode = code;
        if (code == 'session_unknown' || code == 'session_expired' || code == 'token_used') {
          _sessionId = null;
        }
        onRejected?.call(code, text);
        onConnectionStatusChanged(false);
        _closeSocket();
      } else if (type == 'tally') {
        final state = message['state'] as String? ?? 'standby';
        switch (state) {
          case 'program':
            _currentTally = TallyState.program;
            break;
          case 'preview':
            _currentTally = TallyState.preview;
            break;
          default:
            _currentTally = TallyState.standby;
        }
        onTallyChanged(_currentTally);
      } else if (type == 'command') {
        onRemoteCommandReceived?.call(message['command'] as String? ?? '', message['payload']);
      }
    } catch (_) {}
  }

  void _startHeartbeat() {
    _heartbeatTimer?.cancel();
    _heartbeatTimer = Timer.periodic(const Duration(seconds: 2), (_) {
      if (!_isConnected || _wsChannel == null) return;
      try {
        _wsChannel?.sink.add(jsonEncode({
          'type': 'ping',
          'time': DateTime.now().millisecondsSinceEpoch,
        }));
      } catch (_) {
        _onSocketClosed();
      }
    });
  }

  Future<void> _initBatteryMonitoring() async {
    try {
      _lastBatteryLevel = await _battery.batteryLevel;
      final state = await _battery.batteryState;
      _isCharging = state == BatteryState.charging || state == BatteryState.full;
    } catch (_) {
      _lastBatteryLevel = -1;
      _isCharging = false;
    }
  }

  void _startTelemetryLoop() {
    _telemetryTimer?.cancel();
    _telemetryTimer = Timer.periodic(const Duration(seconds: 1), (_) async {
      if (!_isConnected || _wsChannel == null) return;
      try {
        _lastBatteryLevel = await _battery.batteryLevel;
        final state = await _battery.batteryState;
        _isCharging = state == BatteryState.charging || state == BatteryState.full;
      } catch (_) {}

      final telemetry = DeviceTelemetry(
        batteryLevel: _lastBatteryLevel < 0 ? 0 : _lastBatteryLevel,
        isCharging: _isCharging,
        audioLevelNormalized: _currentAudioLevel,
        currentBitrateKbps: _currentRealBitrateKbps,
        fps: _currentRealFps,
        droppedFrames: 0,
        resolution: 'capteur',
      );
      try {
        _wsChannel?.sink.add(jsonEncode({
          'type': 'telemetry',
          'stream_name': _assignedSlot,
          'payload': {
            ...telemetry.toJson(),
            if (_lastBatteryLevel < 0) 'battery_level': null,
          },
        }));
      } catch (_) {
        _onSocketClosed();
      }
    });
  }

  void updateAudioLevel(double level) {
    _currentAudioLevel = level.clamp(0.0, 1.0);
  }

  void _onSocketClosed() {
    final wasConnected = _isConnected;
    _isConnected = false;
    _currentTally = TallyState.standby;
    _handshakeTimeoutTimer?.cancel();
    _telemetryTimer?.cancel();
    _heartbeatTimer?.cancel();
    _closeSocket();
    onConnectionStatusChanged(false);
    onTallyChanged(TallyState.standby);
    if (!wasConnected && _lastRejectCode == null) {
      _lastRejectCode = 'network';
    }
    final decision = ReconnectPolicy.decide(
      userLeft: _userLeft,
      hasSession: _sessionId != null,
      rejectCode: _lastRejectCode,
      attempts: _retry,
    );
    if (!decision.retry) {
      if (decision.message.isNotEmpty) onStatus?.call(decision.message);
      return;
    }
    _retry += 1;
    onStatus?.call(decision.message);
    _retryTimer?.cancel();
    _retryTimer = Timer(decision.delay, () {
      if (!_userLeft) {
        _open(useSession: true);
      }
    });
  }

  void _closeSocket() {
    _wsSubscription?.cancel();
    _wsSubscription = null;
    try {
      _wsChannel?.sink.close();
    } catch (_) {}
    _wsChannel = null;
  }

  void disconnect({bool release = true}) {
    _userLeft = true;
    _retryTimer?.cancel();
    _handshakeTimeoutTimer?.cancel();
    _telemetryTimer?.cancel();
    _heartbeatTimer?.cancel();
    if (release && _isConnected && _wsChannel != null) {
      try {
        _wsChannel?.sink.add(jsonEncode({'type': 'bye'}));
      } catch (_) {}
    }
    if (release) _sessionId = null;
    _closeSocket();
    _isConnected = false;
    _currentTally = TallyState.standby;
    onConnectionStatusChanged(false);
    onTallyChanged(TallyState.standby);
  }
}
