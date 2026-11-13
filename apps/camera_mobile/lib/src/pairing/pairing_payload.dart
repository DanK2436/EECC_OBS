import 'dart:convert';

class PairingPayload {
  final int version;
  final String studioName;
  final String studioIp;
  final int port;
  final String token;
  final DateTime expiresAt;
  final String transport;
  final String path;

  const PairingPayload({
    required this.version,
    required this.studioName,
    required this.studioIp,
    required this.port,
    required this.token,
    required this.expiresAt,
    required this.transport,
    required this.path,
  });

  static const rejectedTokens = {
    'EECC-LIVE-4982',
    'demo_token_123',
  };

  static PairingPayload parse(String raw, {DateTime? now}) {
    final text = raw.trim();
    if (!text.startsWith('{') || !text.endsWith('}')) {
      throw const FormatException('Ce QR n\'est pas un code d\'appairage EECC Studio.');
    }

    final Object? decoded;
    try {
      decoded = jsonDecode(text);
    } on FormatException {
      throw const FormatException('Ce QR n\'est pas un JSON d\'appairage lisible.');
    }
    if (decoded is! Map) {
      throw const FormatException('Ce QR n\'est pas un code d\'appairage EECC Studio.');
    }
    final data = decoded.cast<String, dynamic>();

    final version = data['v'];
    if (version != 1) {
      throw const FormatException('Version de QR non prise en charge.');
    }
    if (data['transport'] != 'ws-jpeg') {
      throw const FormatException('Ce QR n\'utilise pas le transport JPEG du Studio.');
    }

    final studioName = data['studio_name'];
    if (studioName is! String || studioName.trim().isEmpty) {
      throw const FormatException('Le nom du Studio manque dans le QR.');
    }

    final studioIp = data['studio_ip'];
    if (studioIp is! String || !isPrivateIpv4(studioIp)) {
      throw const FormatException(
        'L\'adresse du QR n\'est pas une IPv4 privée. Aucune adresse de remplacement n\'est utilisée.',
      );
    }

    final port = data['port'];
    if (port is! int || port < 1 || port > 65535) {
      throw const FormatException('Le port du QR est invalide.');
    }

    final token = data['token'];
    if (token is! String || !isAcceptableToken(token)) {
      throw const FormatException('Jeton refusé. Scannez le QR affiché maintenant dans EECC Studio.');
    }

    final path = data['path'];
    if (path is! String || path != '/camera/control') {
      throw const FormatException('Le chemin du QR n\'est pas /camera/control.');
    }

    final exp = data['exp'];
    if (exp is! int) {
      throw const FormatException('La date d\'expiration du QR manque.');
    }
    final expiresAt = DateTime.fromMillisecondsSinceEpoch(exp * 1000, isUtc: true);
    final clock = (now ?? DateTime.now()).toUtc();
    if (!expiresAt.isAfter(clock)) {
      throw const FormatException('Ce QR a expiré. Demandez un nouveau code dans EECC Studio.');
    }

    return PairingPayload(
      version: version,
      studioName: studioName.trim(),
      studioIp: studioIp,
      port: port,
      token: token,
      expiresAt: expiresAt,
      transport: 'ws-jpeg',
      path: path,
    );
  }

  static bool isAcceptableToken(String token) {
    if (rejectedTokens.contains(token)) return false;
    // Ancien bouton de démonstration et ancien jeton sess_<heure>, pas un jeton aléatoire.
    if (token.startsWith('demo_token') || RegExp(r'^sess_\d+$').hasMatch(token)) return false;
    return RegExp(r'^[A-Za-z0-9_-]{22,128}$').hasMatch(token);
  }

  static bool isPrivateIpv4(String value) {
    final parts = value.split('.');
    if (parts.length != 4) return false;
    final numbers = <int>[];
    for (final part in parts) {
      final number = int.tryParse(part);
      if (number == null || number < 0 || number > 255 || part != '$number') return false;
      numbers.add(number);
    }
    if (numbers[0] == 10) return true;
    if (numbers[0] == 192 && numbers[1] == 168) return true;
    if (numbers[0] == 172 && numbers[1] >= 16 && numbers[1] <= 31) return true;
    return false;
  }
}
