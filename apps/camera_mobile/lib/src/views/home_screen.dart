import 'dart:io';

import 'package:flutter/material.dart';

import '../constants/colors.dart';
import '../pairing/local_network.dart';
import 'camera_active_screen.dart';
import 'manual_connect_dialog.dart';
import 'qr_scanner_screen.dart';

class CamHomeScreen extends StatefulWidget {
  const CamHomeScreen({super.key});

  @override
  State<CamHomeScreen> createState() => _CamHomeScreenState();
}

class _CamHomeScreenState extends State<CamHomeScreen> {
  String _networkMessage = 'Lecture des adresses du téléphone…';

  @override
  void initState() {
    super.initState();
    _readNetwork();
  }

  Future<void> _readNetwork() async {
    try {
      final interfaces = await NetworkInterface.list(
        includeLoopback: false,
        type: InternetAddressType.IPv4,
      );
      final addresses = interfaces.expand((interface) => interface.addresses.map((address) => address.address));
      if (!mounted) return;
      setState(() => _networkMessage = LocalNetworkStatus.fromAddresses(addresses).message);
    } catch (error) {
      if (!mounted) return;
      setState(() => _networkMessage = 'Adresses locales illisibles : $error');
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: EeccCamColors.background,
      appBar: AppBar(
        backgroundColor: Colors.transparent,
        title: const Text('EECC Caméra', style: TextStyle(fontWeight: FontWeight.bold, color: Colors.white)),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: EeccCamColors.surface,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: Colors.white10),
              ),
              child: Row(
                children: [
                  const Icon(Icons.wifi_rounded, color: Colors.white70, size: 28),
                  const SizedBox(width: 14),
                  Expanded(child: Text(_networkMessage, style: const TextStyle(color: Colors.white, fontSize: 13))),
                ],
              ),
            ),
            const SizedBox(height: 30),
            SizedBox(
              width: double.infinity,
              height: 56,
              child: ElevatedButton.icon(
                style: ElevatedButton.styleFrom(backgroundColor: EeccCamColors.primary),
                icon: const Icon(Icons.qr_code_scanner_rounded, color: Colors.white),
                label: const Text('Scanner le QR Code EECC Studio', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                onPressed: () async {
                  final proceed = await showDialog<bool>(
                    context: context,
                    builder: (context) => AlertDialog(
                      backgroundColor: EeccCamColors.surface,
                      title: const Text('Caméra et réseau local', style: TextStyle(color: Colors.white)),
                      content: const Text(
                        'Le scan demande l\'autorisation caméra. La liaison utilise ensuite le Wi-Fi local. '
                        'Le même réseau ne garantit pas l\'accès si le point d\'accès isole les appareils ou si le pare-feu du PC bloque le port.',
                        style: TextStyle(color: Colors.white70),
                      ),
                      actions: [
                        TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Annuler')),
                        ElevatedButton(onPressed: () => Navigator.pop(context, true), child: const Text('Continuer')),
                      ],
                    ),
                  );
                  if (proceed == true && context.mounted) {
                    Navigator.push(context, MaterialPageRoute(builder: (context) => const QrScannerScreen()));
                  }
                },
              ),
            ),
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              height: 50,
              child: OutlinedButton.icon(
                icon: const Icon(Icons.paste_rounded, color: Colors.white70),
                label: const Text('Coller le code copié dans Studio', style: TextStyle(color: Colors.white70)),
                onPressed: () {
                  ManualConnectDialog.show(context, (config) {
                    Navigator.push(context, MaterialPageRoute(builder: (context) => CameraActiveScreen(config: config)));
                  });
                },
              ),
            ),
            const SizedBox(height: 28),
            const Text(
              '1. Ouvrez EECC Studio sur le PC.\n'
              '2. Affichez le QR « Connecter un téléphone ».\n'
              '3. Scannez-le et confirmez le nom du Studio.\n'
              '4. L\'image n\'est envoyée qu\'après l\'accusé de réception de la régie.',
              style: TextStyle(color: EeccCamColors.textSecondary, height: 1.4),
            ),
          ],
        ),
      ),
    );
  }
}
