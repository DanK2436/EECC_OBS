import 'package:flutter/material.dart';

import '../constants/colors.dart';
import '../pairing/pairing_ticket.dart';
import 'camera_active_screen.dart';

class ManualConnectDialog extends StatefulWidget {
  const ManualConnectDialog({super.key});

  static Future<void> show(BuildContext context) {
    return showDialog(context: context, builder: (context) => const ManualConnectDialog());
  }

  @override
  State<ManualConnectDialog> createState() => _ManualConnectDialogState();
}

class _ManualConnectDialogState extends State<ManualConnectDialog> {
  final TextEditingController _payload = TextEditingController();
  String? _error;

  @override
  void dispose() {
    _payload.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      backgroundColor: EeccCamColors.surface,
      title: const Text('Coller le texte du QR', style: TextStyle(color: Colors.white, fontSize: 18)),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Text(
            'Collez le document affiché par Studio. Aucun jeton n\'est inventé ici.',
            style: TextStyle(color: EeccCamColors.textSecondary, fontSize: 13),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _payload,
            minLines: 4,
            maxLines: 8,
            style: const TextStyle(color: Colors.white, fontSize: 12),
            decoration: const InputDecoration(
              hintText: '{"v":1,"ip":"..."}',
              hintStyle: TextStyle(color: Colors.white24),
            ),
          ),
          if (_error != null) ...[
            const SizedBox(height: 8),
            Text(_error!, style: const TextStyle(color: EeccCamColors.tallyProgram)),
          ],
        ],
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Annuler')),
        ElevatedButton(
          style: ElevatedButton.styleFrom(backgroundColor: EeccCamColors.primary),
          onPressed: () {
            try {
              final ticket = PairingTicket.parse(_payload.text.trim());
              final navigator = Navigator.of(context);
              navigator.pop();
              navigator.push(
                MaterialPageRoute(builder: (context) => CameraActiveScreen(ticket: ticket)),
              );
            } on PairingException catch (error) {
              setState(() => _error = error.message);
            }
          },
          child: const Text('Valider', style: TextStyle(color: Colors.white)),
        ),
      ],
    );
  }
}
