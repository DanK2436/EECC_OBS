import 'pairing_payload.dart';

class LocalNetworkStatus {
  final List<String> privateIpv4;
  final String message;

  const LocalNetworkStatus(this.privateIpv4, this.message);

  bool get hasPrivateAddress => privateIpv4.isNotEmpty;

  static LocalNetworkStatus fromAddresses(Iterable<String> addresses) {
    final private = addresses.where(PairingPayload.isPrivateIpv4).toList();
    if (private.isEmpty) {
      return const LocalNetworkStatus(
        [],
        'Aucune adresse privée détectée sur ce téléphone. Le Wi-Fi local est nécessaire. '
        'Le même nom de réseau ne prouve pas que EECC Studio est joignable : '
        'l\'isolation du point d\'accès, un réseau invité ou le pare-feu du PC peuvent bloquer la liaison.',
      );
    }
    return LocalNetworkStatus(
      private,
      'Adresse privée du téléphone : ${private.first}. '
      'Cela ne prouve pas que le Studio répond, ni que le pare-feu laisse passer le port.',
    );
  }
}
