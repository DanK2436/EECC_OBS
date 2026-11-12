import 'dart:convert';

class PairingException implements Exception {
  final String code;
  final String message;
  const PairingException(this.code, this.message);

  @override
  String toString() => message;
}

class PairingTicket {
  static const protocolVersion = 1;

  final String ip;
  final int port;
  final DateTime expiresAt;
  final String token;
  final String fingerprint;

  const PairingTicket({
    required this.ip,
    required this.port,
    required this.expiresAt,
    required this.token,
    required this.fingerprint,
  });

  static PairingTicket parse(String raw, {DateTime? now}) {
    Object? decoded;
    try {
      decoded = jsonDecode(raw);
    } catch (_) {
      throw const PairingException('invalid', 'QR refusé. Le contenu n\'est pas un appairage EECC.');
    }
    if (decoded is! Map) {
      throw const PairingException('invalid', 'QR refusé. Le contenu n\'est pas un appairage EECC.');
    }
    if (!decoded.containsKey('v')) {
      throw const PairingException('invalid', 'QR refusé. Le contenu n\'est pas un appairage EECC.');
    }
    if (decoded['v'] != protocolVersion) {
      throw const PairingException('version', 'QR d\'une version non reconnue.');
    }
    final ip = decoded['ip'];
    final port = decoded['port'];
    final exp = decoded['exp'];
    final token = decoded['token'];
    final fingerprint = decoded['fp'];
    if (ip is! String || !_ipv4(ip)) {
      throw const PairingException('invalid', 'QR refusé. Adresse locale absente.');
    }
    if (port is! int || port < 1 || port > 65535) {
      throw const PairingException('invalid', 'QR refusé. Port incohérent.');
    }
    if (exp is! int) {
      throw const PairingException('invalid', 'QR refusé. Expiration absente.');
    }
    if (token is! String || !RegExp(r'^[0-9a-f]{32}$').hasMatch(token)) {
      throw const PairingException('invalid', 'QR refusé. Jeton absent ou non aléatoire.');
    }
    if (fingerprint is! String || !RegExp(r'^[0-9a-f]{64}$').hasMatch(fingerprint)) {
      throw const PairingException('invalid', 'QR refusé. Empreinte du certificat absente.');
    }
    final expires = DateTime.fromMillisecondsSinceEpoch(exp * 1000, isUtc: true);
    if (!expires.isAfter(now ?? DateTime.now().toUtc())) {
      throw const PairingException('expired', 'QR expiré. Générez-en un nouveau dans Studio.');
    }
    return PairingTicket(
      ip: ip,
      port: port,
      expiresAt: expires,
      token: token,
      fingerprint: fingerprint,
    );
  }

  static bool _ipv4(String ip) {
    final parts = ip.split('.');
    if (parts.length != 4) return false;
    final bytes = <int>[];
    for (final part in parts) {
      final value = int.tryParse(part);
      if (value == null || value < 0 || value > 255 || part != value.toString()) return false;
      bytes.add(value);
    }
    if (bytes[0] == 127 || (bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0 && bytes[3] == 0)) return false;
    if (bytes[0] == 255 && bytes[1] == 255 && bytes[2] == 255 && bytes[3] == 255) return false;
    return true;
  }
}
