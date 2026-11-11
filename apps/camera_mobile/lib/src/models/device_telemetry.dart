class DeviceTelemetry {
  final int batteryLevel;
  final bool isCharging;
  final double audioLevelNormalized; // 0.0 à 1.0 (pour le vu-mètre)
  final int currentBitrateKbps;
  final int fps;
  final int droppedFrames;
  final String resolution;

  DeviceTelemetry({
    required this.batteryLevel,
    required this.isCharging,
    required this.audioLevelNormalized,
    required this.currentBitrateKbps,
    required this.fps,
    required this.droppedFrames,
    required this.resolution,
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
      'timestamp': DateTime.now().millisecondsSinceEpoch,
    };
  }
}
