import 'package:flutter/material.dart';

import '../constants/colors.dart';
import '../models/session_config.dart';

class ManualConnectDialog extends StatefulWidget {
  final void Function(StudioSessionConfig config) onConnect;

  const ManualConnectDialog({super.key, required this.onConnect});

  static Future<void> show(BuildContext context, void Function(StudioSessionConfig config) onConnect) {
    return showDialog(
      context: context,
      builder: (context) => ManualConnectDialog(onConnect: onConnect),
    );
  }

  @override
  State<ManualConnectDialog> createState() => _ManualConnectDialogState();
}

class _ManualConnectDialogState extends State<ManualConnectDialog> {
  final TextEditingController _codeController = TextEditingController();
  final TextEditingController _nameController = TextEditingController();
  String? _error;

  @override
  void dispose() {
    _codeController.dispose();
    _nameController.dispose();
    super.dispose();
  }

  void _submit() {
    try {
      final config = StudioSessionConfig.parseQr(_codeController.text).withDisplayName(_nameController.text);
      Navigator.pop(context);
      widget.onConnect(config);
    } on FormatException catch (error) {
      setState(() => _error = error.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      backgroundColor: EeccCamColors.surface,
      title: const Text('Coller le code du Studio', style: TextStyle(color: Colors.white, fontSize: 18)),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Text(
              'Dans EECC Studio, utilisez « Copier le code ». Collez-le ici. Aucune adresse ni aucun jeton n\'est proposé par défaut.',
              style: TextStyle(color: EeccCamColors.textSecondary, fontSize: 12),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _codeController,
              minLines: 3,
              maxLines: 6,
              style: const TextStyle(color: Colors.white, fontSize: 12),
              decoration: const InputDecoration(
                labelText: 'Code JSON du QR',
                labelStyle: TextStyle(color: EeccCamColors.textSecondary),
              ),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _nameController,
              style: const TextStyle(color: Colors.white),
              decoration: const InputDecoration(
                labelText: 'Nom de ce téléphone',
                labelStyle: TextStyle(color: EeccCamColors.textSecondary),
              ),
            ),
            if (_error != null) ...[
              const SizedBox(height: 12),
              Text(_error!, style: const TextStyle(color: EeccCamColors.tallyProgram, fontSize: 12)),
            ],
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Annuler')),
        ElevatedButton(onPressed: _submit, child: const Text('Connecter')),
      ],
    );
  }
}
