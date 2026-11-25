/// Télémétrie de capture transmise en continu à EECC Studio
/// Supporte les cadences jusqu'à 300 FPS et les résolutions ultra-haute fidélité jusqu'au 16K HD
class DeviceTelemetry {
  final int batteryLevel;
  final bool isCharging;
  final double audioLevelNormalized; // 0.0 à 1.0 (pour le vu-mètre temps réel)
  final int currentBitrateKbps;
  final int fps; // Cadence mesurée en temps réel (jusqu'à 300 FPS)
  final int droppedFrames;
  final String resolution; // Ex: "16K UHD (15360x8640)", "8K UHD", "4K UHD", "1080p FHD"
  final String codec;

  DeviceTelemetry({
    required this.batteryLevel,
    required this.isCharging,
    required this.audioLevelNormalized,
    required this.currentBitrateKbps,
    required this.fps,
    required this.droppedFrames,
    required this.resolution,
    this.codec = 'H.264 / WebRTC',
  });

  Map<String, dynamic> toJson() {
    return {
      'battery_level': batteryLevel,
      'is_charging': isCharging,
      'audio_level': audioLevelNormalized,
      'bitrate_kbps': currentBitrateKbps,
      'fps': fps,
      'dropped_frames': droppedFrames,
      'resolution': resolution,
      'codec': codec,
      'timestamp': DateTime.now().millisecondsSinceEpoch,
    };
  }
}
