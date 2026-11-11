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
    this.codec = 'h264',
    this.bitrateKbps = 4500,
    this.fps = 30,
  });

  String get controlWebSocketUrl => 'ws://$studioIp:$port/control';
  String get streamTargetUrl => 'srt://$studioIp:${port + 1}?streamid=$streamName&token=$token';

  factory StudioSessionConfig.fromJsonString(String rawJson) {
    final Map<String, dynamic> data = jsonDecode(rawJson);
    return StudioSessionConfig(
      studioIp: data['studio_ip'] as String,
      port: data['port'] as int? ?? 8554,
      token: data['token'] as String? ?? 'anonymous_token',
      streamName: data['stream_name'] as String? ?? 'cam_phone_${DateTime.now().millisecondsSinceEpoch % 1000}',
      codec: data['codec'] as String? ?? 'h264',
      bitrateKbps: data['bitrate_kbps'] as int? ?? 4500,
      fps: data['fps'] as int? ?? 30,
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
