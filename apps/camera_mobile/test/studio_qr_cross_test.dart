import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:eecc_camera/src/models/session_config.dart';
import 'package:eecc_camera/src/pairing/pairing_payload.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('le QR émis par Studio est lu par Caméra puis validé par le serveur', () async {
    final project = Directory.current.path.endsWith('camera_mobile')
        ? '../studio_native/src/EeccStudio.QrEmit/EeccStudio.QrEmit.csproj'
        : 'apps/studio_native/src/EeccStudio.QrEmit/EeccStudio.QrEmit.csproj';
    final directory = await Directory.systemTemp.createTemp('eecc-qr');
    final process = await Process.start('dotnet', [
      'run',
      '--project',
      project,
      '-c',
      'Release',
      '-v',
      'q',
      '--',
      directory.path,
    ]);
    final lines = process.stdout.transform(utf8.decoder).transform(const LineSplitter());
    final output = <String>[];
    final done = lines.listen(output.add);
    String? failure;
    process.stderr.transform(utf8.decoder).listen((text) => failure = '${failure ?? ''}$text');

    Future<void> waitFor(bool Function() ready) async {
      final deadline = DateTime.now().add(const Duration(seconds: 40));
      while (DateTime.now().isBefore(deadline)) {
        if (ready()) return;
        await Future<void>.delayed(const Duration(milliseconds: 50));
      }
      fail('sortie Studio incomplète : $output ${failure ?? ''}');
    }

    await waitFor(() => output.where((line) => line.startsWith('{')).length >= 2 && output.any((line) => int.tryParse(line) != null));
    final payloadText = output.firstWhere((line) => line.startsWith('{'));
    final decoded = output.skip(output.indexOf(payloadText) + 1).firstWhere((line) => line.startsWith('{'));
    final port = int.parse(output.firstWhere((line) => int.tryParse(line) != null));
    expect(decoded, payloadText);

    final payload = PairingPayload.parse(payloadText);
    final config = StudioSessionConfig.parseQr(payloadText);
    expect(config.controlWebSocketUrl, 'ws://${payload.studioIp}:$port/camera/control');
    expect(payload.studioIp, '10.8.0.4');
    expect(payload.path, '/camera/control');
    expect(payload.transport, 'ws-jpeg');
    expect(payload.version, 1);
    expect(payload.token, isNot(contains('demo')));
    expect(payload.expiresAt.isAfter(DateTime.now().toUtc()), isTrue);

    final socket = await WebSocket.connect('ws://127.0.0.1:$port/camera/control');
    socket.add(jsonEncode({
      'type': 'handshake',
      'role': 'camera_source',
      'token': payload.token,
      'reconnect': false,
      'display_name': 'Téléphone croisé',
      'device_model': 'SYNTHETIC_TEST_CLIENT',
    }));
    final ackRaw = await socket.first;
    final ack = jsonDecode(ackRaw as String) as Map<String, dynamic>;
    expect(ack['type'], 'handshake_ack');
    expect((ack['session_id'] as String).isNotEmpty, isTrue);
    socket.add(Uint8List.fromList([0xFF, 0xD8, 0x01, 0x02]));

    await waitFor(() => output.contains('FRAME'));
    await socket.close();
    process.kill();
    await done.cancel();
    await directory.delete(recursive: true);
  });
}
