import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:eecc_camera/src/models/session_config.dart';
import 'package:eecc_camera/src/pairing/studio_link_phase.dart';
import 'package:eecc_camera/src/services/studio_connection_service.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test('WebRTC signaling messages are correctly sent and received over control WebSocket', () async {
    final server = await _MockStudioSignalingServer.start();
    
    String? receivedAnswer;
    String? receivedCandidate;
    String? receivedSdpMid;
    int? receivedMLineIndex;
    bool? receivedReadyState;

    final service = StudioConnectionService(
      config: StudioSessionConfig(
        studioIp: '127.0.0.1',
        port: server.port,
        token: 'test-token-valid-22ch',
        studioName: 'EECC Studio',
        displayName: 'Caméra Mobile 16K',
        expiresAt: DateTime.now().add(const Duration(minutes: 5)),
        fps: 300,
        targetResolution: '16K Ultra-HD',
      ),
      onTallyChanged: (_) {},
      onConnectionStatusChanged: (_) {},
      onWebRtcAnswerReceived: (sdp) => receivedAnswer = sdp,
      onIceCandidateReceived: (candidate, sdpMid, mline) {
        receivedCandidate = candidate;
        receivedSdpMid = sdpMid;
        receivedMLineIndex = mline;
      },
      onWebRtcReadyReceived: (ready) => receivedReadyState = ready,
    );

    await service.connect();
    await _until(() => service.phase == StudioLinkPhase.connected);
    expect(service.isConnected, isTrue);

    // 1. Envoi de l'offre SDP WebRTC par le client
    service.sendWebRtcOffer(
      'v=0\r\no=camera 1234 5678 IN IP4 127.0.0.1\r\ns=EECC 300FPS\r\nm=video 9 UDP/TLS/RTP/SAVPF 96',
      codec: 'H264',
      fps: 300,
      resolution: '16K UHD (15360x8640)',
    );

    final offer = await server.nextMessage();
    expect(offer['type'], 'sdp_offer');
    expect(offer['codec'], 'H264');
    expect(offer['fps'], 300);
    expect(offer['resolution'], '16K UHD (15360x8640)');
    expect(offer['sdp'], contains('EECC 300FPS'));

    // 2. Réception de la réponse SDP du serveur
    server.sendMessage({
      'type': 'sdp_answer',
      'sdp': 'v=0\r\no=studio 9876 5432 IN IP4 127.0.0.1\r\ns=EECC Studio Receiver',
      'status': 'accepted',
    });

    await _until(() => receivedAnswer != null);
    expect(receivedAnswer, contains('EECC Studio Receiver'));

    // 3. Envoi de candidat ICE client -> serveur
    service.sendIceCandidate(
      'candidate:1 1 UDP 2130706431 127.0.0.1 50000 typ host',
      sdpMid: 'video',
      sdpMLineIndex: 0,
    );

    final iceClient = await server.nextMessage();
    expect(iceClient['type'], 'ice_candidate');
    expect(iceClient['candidate'], contains('50000'));
    expect(iceClient['sdpMid'], 'video');
    expect(iceClient['sdpMLineIndex'], 0);

    // 4. Réception de candidat ICE serveur -> client
    server.sendMessage({
      'type': 'ice_candidate',
      'candidate': 'candidate:2 1 UDP 2130706431 127.0.0.1 50002 typ host',
      'sdpMid': 'video',
      'sdpMLineIndex': 0,
    });

    await _until(() => receivedCandidate != null);
    expect(receivedCandidate, contains('50002'));
    expect(receivedSdpMid, 'video');
    expect(receivedMLineIndex, 0);

    // 5. Annonce des capacités matérielles
    service.sendWebRtcCapabilities(
      codecs: ['H264', 'HEVC'],
      maxFps: 300,
      maxResolution: '16K UHD',
    );

    final cap = await server.nextMessage();
    expect(cap['type'], 'webrtc_capabilities');
    expect(cap['max_fps'], 300);
    expect(cap['max_resolution'], '16K UHD');
    expect(cap['codecs'], contains('H264'));

    // 6. Confirmation de canal prêt
    server.sendMessage({
      'type': 'webrtc_ready',
      'ready': true,
    });

    await _until(() => receivedReadyState != null);
    expect(receivedReadyState, isTrue);

    service.disconnect();
    await server.close();
  });
}

Future<void> _until(bool Function() condition, {Duration timeout = const Duration(seconds: 4)}) async {
  final end = DateTime.now().add(timeout);
  while (DateTime.now().isBefore(end)) {
    if (condition()) return;
    await Future.delayed(const Duration(milliseconds: 20));
  }
  throw TimeoutException('Condition non remplie à temps');
}

class _MockStudioSignalingServer {
  final HttpServer _server;
  final List<Map<String, dynamic>> _queue = [];
  final List<Completer<Map<String, dynamic>>> _waiters = [];
  WebSocket? _clientSocket;

  _MockStudioSignalingServer._(this._server);

  int get port => _server.port;

  static Future<_MockStudioSignalingServer> start() async {
    final server = await HttpServer.bind(InternetAddress.loopbackIPv4, 0);
    final mock = _MockStudioSignalingServer._(server);
    server.listen((HttpRequest request) async {
      if (WebSocketTransformer.isUpgradeRequest(request)) {
        final socket = await WebSocketTransformer.upgrade(request);
        mock._clientSocket = socket;
        socket.listen((data) {
          if (data is String) {
            try {
              final json = jsonDecode(data) as Map<String, dynamic>;
              if (json['type'] == 'handshake') {
                socket.add(jsonEncode({
                  'type': 'handshake_ack',
                  'session_id': 'session-test-webrtc',
                  'assigned_slot': 'Cam-1',
                  'slot_number': 1,
                }));
              } else {
                if (mock._waiters.isNotEmpty) {
                  mock._waiters.removeAt(0).complete(json);
                } else {
                  mock._queue.add(json);
                }
              }
            } catch (_) {}
          }
        });
      }
    });
    return mock;
  }

  void sendMessage(Map<String, dynamic> msg) {
    _clientSocket?.add(jsonEncode(msg));
  }

  Future<Map<String, dynamic>> nextMessage({Duration timeout = const Duration(seconds: 4)}) {
    if (_queue.isNotEmpty) {
      return Future.value(_queue.removeAt(0));
    }
    final completer = Completer<Map<String, dynamic>>();
    _waiters.add(completer);
    return completer.future.timeout(timeout);
  }

  Future<void> close() async {
    await _clientSocket?.close();
    for (final waiter in _waiters) {
      if (!waiter.isCompleted) {
        waiter.completeError(Exception('Server closed'));
      }
    }
    _waiters.clear();
    await _server.close(force: true);
  }
}
