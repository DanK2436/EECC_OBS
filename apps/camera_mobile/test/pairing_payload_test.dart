import 'dart:convert';

import 'package:eecc_camera/src/pairing/local_network.dart';
import 'package:eecc_camera/src/pairing/pairing_payload.dart';
import 'package:eecc_camera/src/pairing/reconnect_policy.dart';
import 'package:eecc_camera/src/models/session_config.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  String qr({
    String token = 'abcdefghijklmnopqrstuv',
    String ip = '10.8.0.4',
    int exp = 2000000000,
    int version = 1,
    String transport = 'ws-jpeg',
  }) {
    return jsonEncode({
      'v': version,
      'studio_name': 'EECC Studio',
      'studio_ip': ip,
      'port': 8089,
      'token': token,
      'exp': exp,
      'path': '/camera/control',
      'codec': 'jpeg_stream',
      'transport': transport,
    });
  }

  test('accepts a real pairing payload and keeps the studio name', () {
    final config = StudioSessionConfig.parseQr(qr(), now: DateTime.utc(2026, 1, 1));
    expect(config.studioName, 'EECC Studio');
    expect(config.studioIp, '10.8.0.4');
    expect(config.token, 'abcdefghijklmnopqrstuv');
    expect(config.withDisplayName('  Pupitre  ').displayName, 'Pupitre');
  });

  test('rejects expired, invalid, demo and public addresses', () {
    expect(
      () => PairingPayload.parse(qr(exp: 100), now: DateTime.utc(2026, 1, 1)),
      throwsA(isA<FormatException>().having((error) => error.message, 'message', contains('expiré'))),
    );
    expect(
      () => PairingPayload.parse(qr(token: 'EECC-LIVE-4982'), now: DateTime.utc(2026, 1, 1)),
      throwsFormatException,
    );
    expect(
      () => PairingPayload.parse(qr(token: 'demo_token_123456789012'), now: DateTime.utc(2026, 1, 1)),
      throwsFormatException,
    );
    expect(
      () => PairingPayload.parse(qr(token: 'sess_1730000000123456'), now: DateTime.utc(2026, 1, 1)),
      throwsFormatException,
    );
    expect(
      () => PairingPayload.parse(qr(ip: '8.8.8.8'), now: DateTime.utc(2026, 1, 1)),
      throwsFormatException,
    );
    expect(
      () => PairingPayload.parse(qr(ip: '192.168.1.100'), now: DateTime.utc(2026, 1, 1)),
      returnsNormally,
    );
    expect(
      () => PairingPayload.parse('{"studio_ip":"192.168.1.100"}', now: DateTime.utc(2026, 1, 1)),
      throwsFormatException,
    );
  });

  test('reconnect policy retries only a confirmed session and stops on refusal', () {
    final lost = ReconnectPolicy.decide(userLeft: false, hasSession: true, rejectCode: null, attempts: 0);
    expect(lost.retry, isTrue);
    expect(lost.delay, const Duration(seconds: 1));

    final full = ReconnectPolicy.decide(userLeft: false, hasSession: true, rejectCode: 'capacity', attempts: 0);
    expect(full.retry, isFalse);

    final expired = ReconnectPolicy.decide(userLeft: false, hasSession: true, rejectCode: 'token_expired', attempts: 0);
    expect(expired.retry, isFalse);

    final exhausted = ReconnectPolicy.decide(userLeft: false, hasSession: true, rejectCode: null, attempts: 3);
    expect(exhausted.retry, isFalse);
    expect(exhausted.message, contains('Reconnecter'));

    final left = ReconnectPolicy.decide(userLeft: true, hasSession: true, rejectCode: null, attempts: 0);
    expect(left.retry, isFalse);
  });

  test('local address text does not claim the studio is reachable', () {
    final empty = LocalNetworkStatus.fromAddresses(['8.8.8.8', '127.0.0.1']);
    expect(empty.hasPrivateAddress, isFalse);
    expect(empty.message.toLowerCase(), isNot(contains('connecté')));
    expect(empty.message, contains('session'));

    final wifi = LocalNetworkStatus.fromAddresses(['192.168.4.20']);
    expect(wifi.hasPrivateAddress, isTrue);
    expect(wifi.message, contains('pas une session'));
  });
}
