import 'package:eecc_camera/src/pairing/pairing_ticket.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  const fingerprint = '0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef';
  const token = '0123456789abcdef0123456789abcdef';

  String document(int exp) =>
      '{"v":1,"ip":"10.1.2.3","port":8091,"exp":$exp,"token":"$token","fp":"$fingerprint"}';

  test('accepts a current QR document', () {
    final exp = DateTime.utc(2030).millisecondsSinceEpoch ~/ 1000;
    final ticket = PairingTicket.parse(document(exp), now: DateTime.utc(2026));
    expect(ticket.ip, '10.1.2.3');
    expect(ticket.port, 8091);
    expect(ticket.token, token);
    expect(ticket.fingerprint, fingerprint);
  });

  test('rejects an expired QR', () {
    final exp = DateTime.utc(2020).millisecondsSinceEpoch ~/ 1000;
    expect(
      () => PairingTicket.parse(document(exp), now: DateTime.utc(2026)),
      throwsA(isA<PairingException>().having((error) => error.code, 'code', 'expired')),
    );
  });

  test('rejects a demo document, a short token and a bad version', () {
    expect(
      () => PairingTicket.parse('{"studio_ip":"192.168.1.50","port":8554,"token":"demo_token_123"}'),
      throwsA(isA<PairingException>().having((error) => error.code, 'code', 'invalid')),
    );
    final exp = DateTime.utc(2030).millisecondsSinceEpoch ~/ 1000;
    expect(
      () => PairingTicket.parse(document(exp).replaceAll(token, 'demo_token_123')),
      throwsA(isA<PairingException>()),
    );
    expect(
      () => PairingTicket.parse(document(exp).replaceAll('"v":1', '"v":9')),
      throwsA(isA<PairingException>().having((error) => error.code, 'code', 'version')),
    );
  });
}
