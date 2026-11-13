import 'dart:convert';
import 'dart:io';

import 'package:eecc_camera/src/models/session_config.dart';
import 'package:eecc_camera/src/pairing/studio_link_phase.dart';
import 'package:eecc_camera/src/services/studio_connection_service.dart';
import 'package:eecc_camera/src/views/home_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  StudioSessionConfig configFor(int port) {
    return StudioSessionConfig(
      studioIp: '127.0.0.1',
      port: port,
      token: 'abcdefghijklmnopqrstuv',
      studioName: 'EECC Studio',
      displayName: 'Téléphone test',
      expiresAt: DateTime.utc(2099),
    );
  }

  test('cold service has no session and is not connected', () {
    final service = StudioConnectionService(
      config: configFor(9),
      onTallyChanged: (_) {},
      onConnectionStatusChanged: (_) {},
      handshakeTimeout: const Duration(milliseconds: 200),
    );
    expect(service.isConnected, isFalse);
    expect(service.phase, StudioLinkPhase.disconnected);
    expect(service.sessionId, isNull);
    expect(service.canReconnect, isFalse);
    expect(StudioLinkLabels.badge(service.phase), 'Déconnecté');
    expect(StudioLinkLabels.video(service.phase, TallyState.standby), isNull);
    service.disconnect();
  });

  testWidgets('home opens disconnected without a studio session', (tester) async {
    await tester.pumpWidget(const MaterialApp(home: CamHomeScreen()));
    expect(find.text('Déconnecté'), findsOneWidget);
    expect(find.text('Aucune session avec EECC Studio.'), findsOneWidget);
    expect(find.text('Connecté'), findsNothing);
    expect(find.byIcon(Icons.check_circle), findsNothing);
    expect(find.byIcon(Icons.check_circle_rounded), findsNothing);
  });

  test('handshake_ack with a session becomes connected', () async {
    final server = await _PairingServer.start(ack: true);
    final phases = <StudioLinkPhase>[];
    final service = _service(server.port, phases);
    await service.connect();
    await _until(() => service.phase == StudioLinkPhase.connected);
    expect(service.isConnected, isTrue);
    expect(service.sessionId, 'session-réelle');
    expect(service.assignedSlot, 'Cam-4');
    expect(StudioLinkLabels.video(service.phase, TallyState.standby), 'Vidéo en attente');
    expect(phases, contains(StudioLinkPhase.connecting));
    expect(phases.last, StudioLinkPhase.connected);
    service.disconnect();
    await server.close();
  });

  test('refusal, missing session and absent server stay disconnected', () async {
    final refused = await _PairingServer.start(ack: false);
    final refusedPhases = <StudioLinkPhase>[];
    final refusedService = _service(refused.port, refusedPhases);
    await refusedService.connect();
    await _until(() => refusedService.phase == StudioLinkPhase.disconnected && refusedPhases.isNotEmpty);
    expect(refusedService.isConnected, isFalse);
    expect(refusedService.sessionId, isNull);
    expect(refusedPhases, isNot(contains(StudioLinkPhase.connected)));
    refusedService.disconnect();
    await refused.close();

    final empty = await _PairingServer.start(ack: true, sessionId: '');
    final emptyService = _service(empty.port, []);
    await emptyService.connect();
    await _until(() => emptyService.phase == StudioLinkPhase.disconnected);
    expect(emptyService.isConnected, isFalse);
    expect(emptyService.sessionId, isNull);
    emptyService.disconnect();
    await empty.close();

    final absent = _service(1, []);
    await absent.connect();
    await _until(() => absent.phase == StudioLinkPhase.disconnected);
    expect(absent.isConnected, isFalse);
    absent.disconnect();
  });

  test('socket drop is reconnecting, then a new ack is connected again', () async {
    final server = await _PairingServer.start(ack: true, closeAfterAck: true);
    final phases = <StudioLinkPhase>[];
    final service = _service(server.port, phases);
    await service.connect();
    await _until(() => phases.contains(StudioLinkPhase.reconnecting));
    expect(service.isConnected, isFalse);
    expect(service.sessionId, isNotNull);
    server
      ..closeAfterAck = false
      ..sessionId = 'session-reprise';
    await _until(() => service.phase == StudioLinkPhase.connected && service.sessionId == 'session-reprise');
    expect(service.isConnected, isTrue);
    expect(phases.where((phase) => phase == StudioLinkPhase.connected).length, greaterThanOrEqualTo(1));
    expect(phases, contains(StudioLinkPhase.reconnecting));
    service.disconnect();
    expect(service.phase, StudioLinkPhase.disconnected);
    expect(service.isConnected, isFalse);
    expect(service.canReconnect, isFalse);
    await server.close();
  });
}

StudioConnectionService _service(int port, List<StudioLinkPhase> phases) {
  return StudioConnectionService(
    config: StudioSessionConfig(
      studioIp: '127.0.0.1',
      port: port,
      token: 'abcdefghijklmnopqrstuv',
      studioName: 'EECC Studio',
      displayName: 'Téléphone test',
      expiresAt: DateTime.utc(2099),
    ),
    onTallyChanged: (_) {},
    onConnectionStatusChanged: (_) {},
    onPhaseChanged: phases.add,
    handshakeTimeout: const Duration(milliseconds: 400),
  );
}

Future<void> _until(bool Function() ready) async {
  final deadline = DateTime.now().add(const Duration(seconds: 3));
  while (DateTime.now().isBefore(deadline)) {
    if (ready()) return;
    await Future<void>.delayed(const Duration(milliseconds: 20));
  }
  fail('état de liaison non atteint');
}

class _PairingServer {
  _PairingServer(this._server);

  final HttpServer _server;
  bool ack = true;
  bool closeAfterAck = false;
  String sessionId = 'session-réelle';
  int port = 0;

  static Future<_PairingServer> start({
    required bool ack,
    bool closeAfterAck = false,
    String sessionId = 'session-réelle',
  }) async {
    final http = await HttpServer.bind(InternetAddress.loopbackIPv4, 0);
    final server = _PairingServer(http)
      ..ack = ack
      ..closeAfterAck = closeAfterAck
      ..sessionId = sessionId
      ..port = http.port;
    http.listen((request) async {
      if (!WebSocketTransformer.isUpgradeRequest(request)) {
        request.response.statusCode = 404;
        await request.response.close();
        return;
      }
      final socket = await WebSocketTransformer.upgrade(request);
      socket.listen((raw) {
        final message = jsonDecode(raw as String) as Map<String, dynamic>;
        if (message['type'] != 'handshake') return;
        if (!server.ack) {
          socket.add(jsonEncode({
            'type': 'handshake_reject',
            'code': 'token_invalid',
            'message': 'Jeton refusé.',
          }));
          socket.close();
          return;
        }
        socket.add(jsonEncode({
          'type': 'handshake_ack',
          'session_id': server.sessionId,
          'assigned_slot': 'Cam-4',
          'slot_number': 4,
          'studio_name': 'EECC Studio',
        }));
        if (server.closeAfterAck) socket.close();
      });
    });
    return server;
  }

  Future<void> close() => _server.close(force: true);
}
