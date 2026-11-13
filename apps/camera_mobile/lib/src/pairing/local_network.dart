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
        'Aucune adresse privée locale. Cela ne crée pas de session avec EECC Studio.',
      );
    }
    return LocalNetworkStatus(
      private,
      'Adresse locale du téléphone : ${private.first}. Ce n\'est pas une session Studio.',
    );
  }
}
