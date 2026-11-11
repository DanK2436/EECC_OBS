import 'package:flutter/material.dart';
import '../constants/colors.dart';
import '../models/session_config.dart';
import 'qr_scanner_screen.dart';
import 'manual_connect_dialog.dart';
import 'camera_active_screen.dart';

class CamHomeScreen extends StatelessWidget {
  const CamHomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: EeccCamColors.background,
      appBar: AppBar(
        backgroundColor: Colors.transparent,
        elevation: 0,
        title: Row(
          children: [
            ClipRRect(
              borderRadius: BorderRadius.circular(6),
              child: Image.asset(
                'assets/images/logo.png',
                width: 26,
                height: 26,
                fit: BoxFit.contain,
              ),
            ),
            const SizedBox(width: 10),
            const Text('EECC Caméra', style: TextStyle(fontWeight: FontWeight.bold, color: Colors.white)),
          ],
        ),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Carte Réseau Local
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: EeccCamColors.surface,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: Colors.white10),
              ),
              child: const Row(
                children: [
                  Icon(Icons.wifi_rounded, color: Colors.greenAccent, size: 28),
                  SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Réseau Local Actif', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                        Text('Connecté au Wi-Fi de la régie', style: TextStyle(color: EeccCamColors.textSecondary, fontSize: 12)),
                      ],
                    ),
                  ),
                  Icon(Icons.check_circle_rounded, color: Colors.greenAccent, size: 20),
                ],
              ),
            ),
            const SizedBox(height: 30),

            // Bouton Principal : Scanner QR Code
            SizedBox(
              width: double.infinity,
              height: 56,
              child: ElevatedButton.icon(
                style: ElevatedButton.styleFrom(
                  backgroundColor: EeccCamColors.primary,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                  elevation: 4,
                ),
                icon: const Icon(Icons.qr_code_scanner_rounded, size: 24, color: Colors.white),
                label: const Text(
                  'Scanner le QR Code EECC Studio',
                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Colors.white),
                ),
                onPressed: () {
                  Navigator.push(
                    context,
                    MaterialPageRoute(builder: (context) => const QrScannerScreen()),
                  );
                },
              ),
            ),
            const SizedBox(height: 12),

            // Bouton Secondaire : Connexion manuelle
            SizedBox(
              width: double.infinity,
              height: 50,
              child: OutlinedButton.icon(
                style: OutlinedButton.styleFrom(
                  side: const BorderSide(color: Colors.white24),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                ),
                icon: const Icon(Icons.tune_rounded, color: Colors.white70),
                label: const Text('Connexion manuelle (IP & Port)', style: TextStyle(color: Colors.white70)),
                onPressed: () {
                  ManualConnectDialog.show(context, (config) {
                    Navigator.push(
                      context,
                      MaterialPageRoute(builder: (context) => CameraActiveScreen(config: config)),
                    );
                  });
                },
              ),
            ),

            const SizedBox(height: 35),

            // Guide d'appairage rapide
            Container(
              padding: const EdgeInsets.all(18),
              decoration: BoxDecoration(
                color: EeccCamColors.surface,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: Colors.white10),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Row(
                    children: [
                      Icon(Icons.help_outline_rounded, color: EeccCamColors.primaryLight, size: 20),
                      SizedBox(width: 8),
                      Text('Comment appairer votre caméra ?', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                    ],
                  ),
                  const SizedBox(height: 14),
                  _buildStep('1', 'Lancez EECC Studio sur le PC de la régie.'),
                  _buildStep('2', 'Assurez-vous que le PC et ce téléphone sont sur le même réseau Wi-Fi.'),
                  _buildStep('3', 'Dans Studio, ajoutez la source "EECC Caméra" et cliquez sur "Appairer".'),
                  _buildStep('4', 'Scannez le QR code affiché à l\'écran du PC.'),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildStep(String number, String text) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          CircleAvatar(
            radius: 10,
            backgroundColor: EeccCamColors.primaryLight.withOpacity(0.2),
            child: Text(number, style: const TextStyle(fontSize: 11, fontWeight: FontWeight.bold, color: EeccCamColors.primaryLight)),
          ),
          const SizedBox(width: 10),
          Expanded(child: Text(text, style: const TextStyle(color: EeccCamColors.textSecondary, fontSize: 13))),
        ],
      ),
    );
  }
}
