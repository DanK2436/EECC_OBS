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
  final String codec;
  final int bitrateKbps;
  final int fps;

  const StudioSessionConfig({
    required this.studioIp,
    required this.port,
    required this.token,
    required this.studioName,
    required this.displayName,
    required this.expiresAt,
    this.codec = 'jpeg_stream',
    this.bitrateKbps = 4500,
    this.fps = 15,
  });

  String get controlWebSocketUrl => 'ws://$studioIp:$port/camera/control';

  factory StudioSessionConfig.parseQr(String raw, {DateTime? now}) {
    final payload = PairingPayload.parse(raw, now: now);
    return StudioSessionConfig(
      studioIp: payload.studioIp,
      port: payload.port,
      token: payload.token,
      studioName: payload.studioName,
      displayName: 'Téléphone',
      expiresAt: payload.expiresAt,
    );
  }

  StudioSessionConfig withDisplayName(String name) {
    final trimmed = name.trim();
    final safe = trimmed.isEmpty ? 'Téléphone' : (trimmed.length <= 40 ? trimmed : trimmed.substring(0, 40));
    return StudioSessionConfig(
      studioIp: studioIp,
      port: port,
      token: token,
      studioName: studioName,
      displayName: safe,
      expiresAt: expiresAt,
      codec: codec,
      bitrateKbps: bitrateKbps,
      fps: fps,
    );
  }
}
