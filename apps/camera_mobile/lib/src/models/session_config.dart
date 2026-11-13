import 'dart:convert';

enum TallyState {
  standby, // Connecté mais pas utilisé à l'écran
  preview, // En Aperçu dans EECC Studio (prochaine scène)
  program, // À L'ANTENNE dans EECC Studio (diffusé en direct)
}

class StudioSessionConfig {
  final String studioIp;
  final int port;
  final String token;
  final String streamName;
  final String codec;
  final int bitrateKbps;
  final int fps;

  StudioSessionConfig({
    required this.studioIp,
    required this.port,
    required this.token,
    required this.streamName,
    this.codec = 'jpeg_stream',
    this.bitrateKbps = 4500,
    this.fps = 30,
  });

  String get controlWebSocketUrl => 'ws://$studioIp:$port/camera/control';
  String get streamUploadUrl => 'http://$studioIp:$port/camera/upload';

  factory StudioSessionConfig.fromJsonString(String rawInput) {
    final raw = rawInput.trim();
    try {
      if (raw.startsWith('{') && raw.endsWith('}')) {
        final Map<String, dynamic> data = jsonDecode(raw);
        return StudioSessionConfig(
          studioIp: data['studio_ip'] as String? ?? '192.168.1.100',
          port: data['port'] as int? ?? 8089,
          token: data['token'] as String? ?? 'EECC-LIVE-4982',
          streamName: data['requested_slot'] as String? ?? data['stream_name'] as String? ?? 'auto',
          codec: data['codec'] as String? ?? 'jpeg_stream',
          bitrateKbps: data['bitrate_kbps'] as int? ?? 4500,
          fps: data['fps'] as int? ?? 30,
        );
      } else if (raw.contains('://')) {
        final uri = Uri.parse(raw);
        return StudioSessionConfig(
          studioIp: uri.host,
          port: uri.port > 0 ? uri.port : 8089,
          token: 'EECC-LIVE-4982',
          streamName: 'auto',
        );
      } else if (raw.contains(':')) {
        final parts = raw.split(':');
        return StudioSessionConfig(
          studioIp: parts[0],
          port: int.tryParse(parts[1]) ?? 8089,
          token: 'EECC-LIVE-4982',
          streamName: 'auto',
        );
      }
    } catch (_) {}

    return StudioSessionConfig(
      studioIp: '192.168.1.100',
      port: 8089,
      token: 'EECC-LIVE-4982',
      streamName: 'auto',
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'studio_ip': studioIp,
      'port': port,
      'token': token,
      'stream_name': streamName,
      'codec': codec,
      'bitrate_kbps': bitrateKbps,
      'fps': fps,
    };
  }
}
