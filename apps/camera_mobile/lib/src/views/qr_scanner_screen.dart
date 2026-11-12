import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import '../constants/colors.dart';
import '../pairing/pairing_ticket.dart';
import 'camera_active_screen.dart';

class QrScannerScreen extends StatefulWidget {
  const QrScannerScreen({super.key});

  @override
  State<QrScannerScreen> createState() => _QrScannerScreenState();
}

class _QrScannerScreenState extends State<QrScannerScreen> {
  final MobileScannerController _controller = MobileScannerController();
  bool _accepted = false;
  String? _error;

  void _onDetect(BarcodeCapture capture) {
    if (_accepted) return;
    for (final barcode in capture.barcodes) {
      final rawValue = barcode.rawValue;
      if (rawValue == null || rawValue.isEmpty) continue;
      try {
        final ticket = PairingTicket.parse(rawValue);
        _accepted = true;
        _controller.stop();
        Navigator.pushReplacement(
          context,
          MaterialPageRoute(builder: (context) => CameraActiveScreen(ticket: ticket)),
        );
        return;
      } on PairingException catch (error) {
        setState(() => _error = error.message);
      }
    }
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
          MobileScanner(controller: _controller, onDetect: _onDetect),
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
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
              decoration: BoxDecoration(
                color: Colors.black.withOpacity(0.75),
                borderRadius: BorderRadius.circular(16),
              ),
              child: Text(
                _error ?? 'Pointez vers le QR affiché par EECC Studio sur le même réseau local.',
                textAlign: TextAlign.center,
                style: const TextStyle(color: Colors.white, fontSize: 13),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
