import 'package:eecc_camera/src/models/session_config.dart';
import 'package:eecc_camera/src/services/studio_connection_service.dart';
import 'package:eecc_camera/src/services/webrtc_camera_stream_service.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  group('WebrtcCameraStreamService Tests', () {
    test('SDP Munging injects high bitrate constraint for 16K HD / 300 FPS broadcast', () {
      const rawSdp =
          'v=0\r\n'
          'o=- 12345 2 IN IP4 127.0.0.1\r\n'
          's=-\r\n'
          't=0 0\r\n'
          'm=video 9 UDP/TLS/RTP/SAVPF 96\r\n'
          'c=IN IP4 127.0.0.1\r\n'
          'a=rtpmap:96 H264/90000';

      final munged = WebrtcCameraStreamService.mungeSdpForHighQualityBroadcast(
        rawSdp,
        targetFps: 300,
        resolution: '16K Ultra-HD',
      );

      expect(munged, contains('m=video 9 UDP/TLS/RTP/SAVPF 96'));
      expect(munged, contains('b=AS:50000'));
      expect(munged, contains('a=rtpmap:96 H264/90000'));
    });

    test('Initial transport state is idle and metrics are zeroed', () {
      final config = StudioSessionConfig(
        studioIp: '127.0.0.1',
        port: 8089,
        token: 'test-token-valid-22ch',
        studioName: 'EECC Studio',
        displayName: 'Caméra Test',
        expiresAt: DateTime.now().add(const Duration(minutes: 5)),
        fps: 300,
        targetResolution: '16K Ultra-HD',
      );

      final signaling = StudioConnectionService(
        config: config,
        onTallyChanged: (_) {},
        onConnectionStatusChanged: (_) {},
      );

      final service = WebrtcCameraStreamService(
        config: config,
        signalingService: signaling,
      );

      expect(service.state, WebRtcTransportState.idle);
      expect(service.isStreaming, isFalse);
      expect(service.currentFps, 0);
      expect(service.currentBitrateKbps, 0);
    });
  });
}
