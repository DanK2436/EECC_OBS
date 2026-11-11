import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import '../constants/colors.dart';
import '../models/session_config.dart';
import 'camera_active_screen.dart';

class QrScannerScreen extends StatefulWidget {
  const QrScannerScreen({super.key});

  @override
  State<QrScannerScreen> createState() => _QrScannerScreenState();
}

class _QrScannerScreenState extends State<QrScannerScreen> {
  final MobileScannerController _controller = MobileScannerController();
  bool _isScanned = false;

  void _onDetect(BarcodeCapture capture) {
    if (_isScanned) return;

    for (final barcode in capture.barcodes) {
      final rawValue = barcode.rawValue;
      if (rawValue != null && rawValue.isNotEmpty) {
        try {
          final config = StudioSessionConfig.fromJsonString(rawValue);
          _isScanned = true;
          _controller.stop();
          _navigateToCamera(config);
          break;
        } catch (_) {
          // Format QR code non reconnu
        }
      }
    }
  }

  void _navigateToCamera(StudioSessionConfig config) {
    Navigator.pushReplacement(
      context,
      MaterialPageRoute(builder: (context) => CameraActiveScreen(config: config)),
    );
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      appBar: AppBar(
        backgroundColor: Colors.transparent,
        elevation: 0,
        foregroundColor: Colors.white,
        title: const Text('Scanner le QR Code EECC Studio', style: TextStyle(fontSize: 16)),
        actions: [
          IconButton(
            icon: const Icon(Icons.flash_on_rounded),
            onPressed: () => _controller.toggleTorch(),
          ),
        ],
      ),
      body: Stack(
        alignment: Alignment.center,
        children: [
          MobileScanner(
            controller: _controller,
            onDetect: _onDetect,
          ),

          // Cadre de visée
          Container(
            width: 260,
            height: 260,
            decoration: BoxDecoration(
              border: Border.all(color: EeccCamColors.primaryLight, width: 3),
              borderRadius: BorderRadius.circular(16),
            ),
          ),

          Positioned(
            bottom: 60,
            child: Column(
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                  decoration: BoxDecoration(
                    color: Colors.black.withOpacity(0.7),
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: const Text(
                    'Pointez vers le QR code affiché dans EECC Studio',
                    style: TextStyle(color: Colors.white, fontSize: 13),
                  ),
                ),
                const SizedBox(height: 16),
                // Bouton de simulation directe (utile pour test sans caméra physique / émulateur)
                TextButton.icon(
                  style: TextButton.styleFrom(
                    backgroundColor: EeccCamColors.surface,
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                  ),
                  icon: const Icon(Icons.bug_report_rounded, size: 18),
                  label: const Text('Tester avec QR démo'),
                  onPressed: () {
                    final demoConfig = StudioSessionConfig(
                      studioIp: '192.168.1.50',
                      port: 8554,
                      token: 'demo_token_123',
                      streamName: 'Caméra Smartphone 1',
                    );
                    _navigateToCamera(demoConfig);
                  },
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
