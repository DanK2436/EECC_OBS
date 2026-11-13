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
  bool _handled = false;

  Future<void> _onDetect(BarcodeCapture capture) async {
    if (_handled) return;
    for (final barcode in capture.barcodes) {
      final rawValue = barcode.rawValue;
      if (rawValue == null || rawValue.isEmpty) continue;
      try {
        final config = StudioSessionConfig.parseQr(rawValue);
        _handled = true;
        await _controller.stop();
        if (!mounted) return;
        final accepted = await _confirm(config);
        if (!mounted) return;
        if (accepted == null) {
          _handled = false;
          await _controller.start();
          return;
        }
        Navigator.pushReplacement(
          context,
          MaterialPageRoute(builder: (context) => CameraActiveScreen(config: accepted)),
        );
        return;
      } on FormatException catch (error) {
        _handled = true;
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.message)));
        _handled = false;
        return;
      }
    }
  }

  Future<StudioSessionConfig?> _confirm(StudioSessionConfig config) {
    final name = TextEditingController();
    return showDialog<StudioSessionConfig>(
      context: context,
      builder: (context) => AlertDialog(
        backgroundColor: EeccCamColors.surface,
        title: const Text('Se connecter à ce Studio ?', style: TextStyle(color: Colors.white)),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Se connecter à ${config.studioName} (${config.studioIp}:${config.port}) ?',
              style: const TextStyle(color: Colors.white),
            ),
            const SizedBox(height: 8),
            const Text(
              'La vidéo JPEG part sur le réseau local, sans chiffrement supplémentaire. '
              'L\'autorisation caméra est demandée pour filmer.',
              style: TextStyle(color: EeccCamColors.textSecondary, fontSize: 12),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: name,
              style: const TextStyle(color: Colors.white),
              decoration: const InputDecoration(
                labelText: 'Nom de ce téléphone',
                labelStyle: TextStyle(color: EeccCamColors.textSecondary),
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Annuler'),
          ),
          ElevatedButton(
            onPressed: () => Navigator.pop(context, config.withDisplayName(name.text)),
            child: const Text('Connecter'),
          ),
        ],
      ),
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
        foregroundColor: Colors.white,
        title: const Text('Scanner le QR Code EECC Studio', style: TextStyle(fontSize: 16)),
      ),
      body: Stack(
        alignment: Alignment.center,
        children: [
          MobileScanner(
            controller: _controller,
            onDetect: _onDetect,
            errorBuilder: (context, error, child) {
              final denied = error.errorCode == MobileScannerErrorCode.permissionDenied;
              final message = denied
                  ? 'Autorisation caméra refusée. Ouvrez les réglages du téléphone pour scanner le QR.'
                  : 'Caméra de scan indisponible : ${error.errorCode.name}.';
              return ColoredBox(
                color: Colors.black,
                child: Center(
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Text(message, textAlign: TextAlign.center, style: const TextStyle(color: Colors.white)),
                  ),
                ),
              );
            },
          ),
          Container(
            width: 260,
            height: 260,
            decoration: BoxDecoration(
              border: Border.all(color: EeccCamColors.primaryLight, width: 3),
              borderRadius: BorderRadius.circular(16),
            ),
          ),
          Positioned(
            left: 24,
            right: 24,
            bottom: 48,
            child: Text(
              'Pointez vers le QR affiché dans EECC Studio. Aucun code de démonstration n\'est proposé.',
              textAlign: TextAlign.center,
              style: const TextStyle(color: Colors.white, fontSize: 13),
            ),
          ),
        ],
      ),
    );
  }
}
