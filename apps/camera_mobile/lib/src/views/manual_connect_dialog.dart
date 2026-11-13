import 'package:flutter/material.dart';
import '../constants/colors.dart';
import '../models/session_config.dart';

class ManualConnectDialog extends StatefulWidget {
  final Function(StudioSessionConfig) onConnect;

  const ManualConnectDialog({super.key, required this.onConnect});

  static Future<void> show(BuildContext context, Function(StudioSessionConfig) onConnect) {
    return showDialog(
      context: context,
      builder: (context) => ManualConnectDialog(onConnect: onConnect),
    );
  }

  @override
  State<ManualConnectDialog> createState() => _ManualConnectDialogState();
}

class _ManualConnectDialogState extends State<ManualConnectDialog> {
  final TextEditingController _ipController = TextEditingController(text: '192.168.1.100');
  final TextEditingController _portController = TextEditingController(text: '8089');
  final TextEditingController _nameController = TextEditingController(text: 'Caméra Smartphone');

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      backgroundColor: EeccCamColors.surface,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
      title: const Row(
        children: [
          Icon(Icons.settings_ethernet_rounded, color: EeccCamColors.primaryLight),
          SizedBox(width: 10),
          Text('Connexion Manuelle', style: TextStyle(color: EeccCamColors.textMain, fontSize: 18)),
        ],
      ),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(
              controller: _ipController,
              keyboardType: TextInputType.datetime,
              style: const TextStyle(color: Colors.white),
              decoration: const InputDecoration(
                labelText: 'Adresse IP de EECC Studio',
                labelStyle: TextStyle(color: EeccCamColors.textSecondary),
                hintText: '192.168.1.xxx',
                hintStyle: TextStyle(color: Colors.white24),
                prefixIcon: Icon(Icons.computer_rounded, color: EeccCamColors.textSecondary),
              ),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _portController,
              keyboardType: TextInputType.number,
              style: const TextStyle(color: Colors.white),
              decoration: const InputDecoration(
                labelText: 'Port de contrôle',
                labelStyle: TextStyle(color: EeccCamColors.textSecondary),
                prefixIcon: Icon(Icons.numbers_rounded, color: EeccCamColors.textSecondary),
              ),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _nameController,
              style: const TextStyle(color: Colors.white),
              decoration: const InputDecoration(
                labelText: 'Nom de la caméra',
                labelStyle: TextStyle(color: EeccCamColors.textSecondary),
                prefixIcon: Icon(Icons.videocam_rounded, color: EeccCamColors.textSecondary),
              ),
            ),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: const Text('Annuler', style: TextStyle(color: EeccCamColors.textSecondary)),
        ),
        ElevatedButton(
          style: ElevatedButton.styleFrom(
            backgroundColor: EeccCamColors.primary,
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
          ),
          onPressed: () {
            final config = StudioSessionConfig(
              studioIp: _ipController.text.trim(),
              port: int.tryParse(_portController.text.trim()) ?? 8554,
              token: 'manual_${DateTime.now().millisecondsSinceEpoch}',
              streamName: _nameController.text.trim(),
            );
            Navigator.pop(context);
            widget.onConnect(config);
          },
          child: const Text('Connecter', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
        ),
      ],
    );
  }
}
