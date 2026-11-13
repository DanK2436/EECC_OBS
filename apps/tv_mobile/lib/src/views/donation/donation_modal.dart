import 'package:flutter/material.dart';
import '../../constants/colors.dart';

class DonationModal extends StatefulWidget {
  const DonationModal({super.key});

  static Future<void> show(BuildContext context) {
    return showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (context) => const DonationModal(),
    );
  }

  @override
  State<DonationModal> createState() => _DonationModalState();
}

class _DonationModalState extends State<DonationModal> {
  double _selectedAmount = 10.0;
  String _selectedMethod = 'Mobile Money (Airtel, M-Pesa, Orange)';
  final TextEditingController _customAmountController = TextEditingController();
  final TextEditingController _phoneController = TextEditingController();

  final List<double> _predefinedAmounts = [5.0, 10.0, 20.0, 50.0, 100.0];

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: EdgeInsets.only(
        top: 20,
        left: 20,
        right: 20,
        bottom: MediaQuery.of(context).viewInsets.bottom + 25,
      ),
      decoration: const BoxDecoration(
        color: EeccColors.surface,
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Center(
              child: Container(
                width: 40,
                height: 4,
                margin: const EdgeInsets.only(bottom: 20),
                decoration: BoxDecoration(
                  color: EeccColors.border,
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
            ),
            Row(
              children: [
                Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: EeccColors.primary.withOpacity(0.1),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: const Icon(Icons.favorite_rounded, color: EeccColors.primary),
                ),
                const SizedBox(width: 12),
                const Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Soutenir l\'œuvre EECC',
                      style: TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.bold,
                        color: EeccColors.textMain,
                      ),
                    ),
                    Text(
                      'Paiement MaishaPay non branché',
                      style: TextStyle(fontSize: 13, color: EeccColors.textSecondary),
                    ),
                  ],
                ),
              ],
            ),
            const SizedBox(height: 20),
            const Text(
              'Choisissez un montant (USD) :',
              style: TextStyle(fontWeight: FontWeight.w600, color: EeccColors.textMain),
            ),
            const SizedBox(height: 10),
            Wrap(
              spacing: 8,
              children: _predefinedAmounts.map((amount) {
                final isSelected = _selectedAmount == amount;
                return ChoiceChip(
                  label: Text('${amount.toInt()} \$'),
                  selected: isSelected,
                  selectedColor: EeccColors.primary,
                  labelStyle: TextStyle(
                    color: isSelected ? Colors.white : EeccColors.textMain,
                    fontWeight: FontWeight.bold,
                  ),
                  onSelected: (selected) {
                    if (selected) {
                      setState(() {
                        _selectedAmount = amount;
                        _customAmountController.clear();
                      });
                    }
                  },
                );
              }).toList(),
            ),
            const SizedBox(height: 15),
            TextField(
              controller: _customAmountController,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              decoration: InputDecoration(
                labelText: 'Ou montant personnalisé (\$ USD)',
                prefixIcon: const Icon(Icons.attach_money_rounded),
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
              ),
              onChanged: (val) {
                final parsed = double.tryParse(val);
                if (parsed != null && parsed > 0) {
                  setState(() => _selectedAmount = parsed);
                }
              },
            ),
            const SizedBox(height: 15),
            const Text(
              'Mode de paiement :',
              style: TextStyle(fontWeight: FontWeight.w600, color: EeccColors.textMain),
            ),
            const SizedBox(height: 8),
            DropdownButtonFormField<String>(
              value: _selectedMethod,
              decoration: InputDecoration(
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
              ),
              items: const [
                DropdownMenuItem(
                  value: 'Mobile Money (Airtel, M-Pesa, Orange)',
                  child: Text('Mobile Money (RDC)'),
                ),
                DropdownMenuItem(
                  value: 'Carte bancaire (Visa / Mastercard)',
                  child: Text('Carte bancaire (Internationale)'),
                ),
              ],
              onChanged: (val) {
                if (val != null) setState(() => _selectedMethod = val);
              },
            ),
            const SizedBox(height: 15),
            TextField(
              controller: _phoneController,
              keyboardType: TextInputType.phone,
              decoration: InputDecoration(
                labelText: 'Numéro de téléphone (+243...)',
                prefixIcon: const Icon(Icons.phone_iphone_rounded),
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
              ),
            ),
            const SizedBox(height: 25),
            SizedBox(
              width: double.infinity,
              height: 50,
              child: ElevatedButton(
                style: ElevatedButton.styleFrom(
                  backgroundColor: EeccColors.primary,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                ),
                onPressed: () {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(
                      content: Text('Aucun don n\'a été envoyé. MaishaPay n\'est pas configuré sur ce téléphone.'),
                    ),
                  );
                },
                child: Text(
                  'Don de ${_selectedAmount.toStringAsFixed(0)} \$ non disponible',
                  style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Colors.white),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
