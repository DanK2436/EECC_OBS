import '../pairing/pairing_payload.dart';

enum TallyState {
  standby,
  preview,
  program,
}

class StudioSessionConfig {
  final String studioIp;
  final int port;
  final String token;
  final String studioName;
  final String displayName;
  final DateTime expiresAt;
  final String path;
  final String codec;
  final int bitrateKbps;
  final int fps; // Cadence cible pouvant monter jusqu'à 300 FPS pour la haute vitesse
  final String targetResolution; // Jusqu'à 16K UHD (15360x8640)

  const StudioSessionConfig({
    required this.studioIp,
    required this.port,
    required this.token,
    required this.studioName,
    required this.displayName,
    required this.expiresAt,
    this.path = '/camera/control',
    this.codec = 'h264_webrtc_stream',
    this.bitrateKbps = 25000,
    this.fps = 300, // Cible sans bride jusqu'à 300 FPS
    this.targetResolution = '16K Ultra-HD',
  });

  String get controlWebSocketUrl => 'ws://$studioIp:$port$path';

  factory StudioSessionConfig.parseQr(String raw, {DateTime? now}) {
    final payload = PairingPayload.parse(raw, now: now);
    return StudioSessionConfig(
      studioIp: payload.studioIp,
      port: payload.port,
      token: payload.token,
      studioName: payload.studioName,
      displayName: 'Caméra Mobile HD',
      expiresAt: payload.expiresAt,
      path: payload.path,
    );
  }

  StudioSessionConfig withDisplayName(String name) {
    final trimmed = name.trim();
    final safe = trimmed.isEmpty ? 'Caméra Mobile HD' : (trimmed.length <= 40 ? trimmed : trimmed.substring(0, 40));
    return StudioSessionConfig(
      studioIp: studioIp,
      port: port,
      token: token,
      studioName: studioName,
      displayName: safe,
      expiresAt: expiresAt,
      path: path,
      codec: codec,
      bitrateKbps: bitrateKbps,
      fps: fps,
      targetResolution: targetResolution,
    );
  }

  StudioSessionConfig copyWith({
    String? studioIp,
    int? port,
    String? token,
    String? studioName,
    String? displayName,
    DateTime? expiresAt,
    String? path,
    String? codec,
    int? bitrateKbps,
    int? fps,
    String? targetResolution,
  }) {
    return StudioSessionConfig(
      studioIp: studioIp ?? this.studioIp,
      port: port ?? this.port,
      token: token ?? this.token,
      studioName: studioName ?? this.studioName,
      displayName: displayName ?? this.displayName,
      expiresAt: expiresAt ?? this.expiresAt,
      path: path ?? this.path,
      codec: codec ?? this.codec,
      bitrateKbps: bitrateKbps ?? this.bitrateKbps,
      fps: fps ?? this.fps,
      targetResolution: targetResolution ?? this.targetResolution,
    );
  }
}
