import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';
import 'package:http/http.dart' as http;
import 'package:http_parser/http_parser.dart';

import '../../constants/config.dart';
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
  final TextEditingController _donorNameController = TextEditingController();
  final TextEditingController _donorEmailController = TextEditingController();

  final List<double> _predefinedAmounts = [5.0, 10.0, 20.0, 50.0, 100.0];

  // État du don
  String? _donationReference;
  String? _donationStatus;
  bool _isProcessing = false;
  String? _errorMessage;

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
                      'Don sécurisé via Maishapay',
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
            const SizedBox(height: 15),
            TextField(
              controller: _donorNameController,
              decoration: InputDecoration(
                labelText: 'Nom complet (pour le reçu)',
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
              ),
            ),
            const SizedBox(height: 15),
            TextField(
              controller: _donorEmailController,
              decoration: InputDecoration(
                labelText: 'E-mail (pour le reçu électronique)',
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
              ),
            ),
            const SizedBox(height: 25),
            _isProcessing
                ? const Padding(
                    padding: EdgeInsets.all(16),
                    child: CircularProgressIndicator(
                      color: EeccColors.primary,
                    ),
                  )
                : SizedBox(
                    width: double.infinity,
                    height: 50,
                    child: ElevatedButton(
                      style: ElevatedButton.styleFrom(
                        backgroundColor: EeccColors.primary,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(12),
                        ),
                      ),
                      onPressed: () => _processDonation(context),
                      child: const Text(
                        'Faire un don de ${_selectedAmount.toStringAsFixed(0)} \$',
                        style: TextStyle(
                            fontSize: 16, fontWeight: FontWeight.bold, color: Colors.white),
                      ),
                    ),
                  ),
                  const SizedBox(height: 30),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _processDonation(BuildContext context) async {
    setState(() {
      _isProcessing = true;
      _errorMessage = null;
    });

    final amount = double.tryParse(_selectedAmount.toStringAsFixed(2)) ?? 0.0;
    final phone = _phoneController.text.trim();
    final donorName = _donorNameController.text.trim();
    final donorEmail = _donorEmailController.text.trim();
    final method = _selectedMethod;

    if (amount <= 0) {
      setState(() {
        _isProcessing = false;
        _errorMessage = 'Veuillez entrer un montant valide.';
      });
      return;
    }

    if (method == 'Mobile Money (Airtel, M-Pesa, Orange)' && phone.isEmpty) {
      setState(() {
        _isProcessing = false;
        _errorMessage = 'Veuillez entrer votre numéro de téléphone.';
      });
      return;
    }

    // 1. Appel au backend pour créer l'intention de don
    try {
      final uri = Uri.parse('${EeccConfig.supabaseUrl}/functions/v1/create-donation-intent');
      final response = await http.post(
        uri,
        headers: {
          'Content-Type': 'application/json',
          'apikey': EeccConfig.supabaseAnonKey,
          'Authorization': 'Bearer ${EeccConfig.supabaseAnonKey}',
        },
        body: jsonEncode({
          'amount': _selectedAmount,
          'currency': 'USD',
          'donor_name': donorName.isNotEmpty ? donorName : null,
          'donor_email': donorEmail.isNotEmpty ? donorEmail : null,
          'donor_phone': phone.isNotEmpty ? phone : null,
          'method': method,
        },
      );

      if (response.statusCode != 200) {
        final errorBody = response.body;
        throw Exception('Erreur backend: ${response.statusCode} - $errorBody');
      }

      final data = jsonDecode(response.body);

      if (!data['success']) {
        throw Exception(data['error'] ?? 'Erreur inconnue du backend');
      }

      final donation = data['donation'];
      _donationReference = donation['reference'];
      _donationStatus = donation['status'];

      // 2. Rediriger vers le checkout Maishapay
      final checkoutUrl = data['checkout_url'] as String?;
      if (checkoutUrl != null && checkoutUrl.isNotEmpty) {
        final paymentUri = Uri.parse(checkoutUrl);
        if (await canLaunchUrl(paymentUri)) {
          // On ouvre dans une vue externe; on passe aussi les infos utiles
          // Note: pour un usage réel, il faudrait passer par un navigateur WebView
          // ou stocker le reference côté client et laisser Maishapay y accéder.
          // Ici on redirige car c'est le pattern le plus simple.
          if (!mounted) return;
          Navigator.of(context).pop(); // fermer le modal
          if (!await launchUrl(paymentUri, mode: LaunchMode.externalApplication)) {
            if (!mounted) return;
            // fallback vers le site de l'église
            if (!mounted) return;
            // ignore: use_build_context_safely
            // ignore: avoid_print
            print('Impossible d\'ouvrir le lien de paiement, redirection vers le site');
          }
        }
      } else {
        throw Exception('URL de checkout introuvable');
      }
    } catch (e) {
      // 3. En cas d'erreur, fallback : construire manuellement l'URL Maishapay
      // (pour ne pas bloquer l'utilisateur si le backend ne répond pas)
      if (!mounted) return;
      final amountFmt = _selectedAmount.toStringAsFixed(0);
      final paymentUrl = Uri.parse(
        'https://maishapay.online/api/checkout?amount=$amountFmt&currency=USD&phone=${Uri.encodeComponent(_phoneController.text.trim())}&merchant_key=${Uri.encodeComponent(EeccConfig.maishapayPublicKey)}'
      );
      try {
        if (await launchUrl(paymentUrl, mode: LaunchMode.externalApplication)) {
          // fermer le modal
          if (!mounted) return;
          Navigator.of(context).pop();
        }
      } catch (_) {
        if (!mounted) return;
        setState(() {
          _isProcessing = false;
          _errorMessage =
              'Impossible d\'ouvrir le paiement. Veuillez réessayer ou contactez l\'administrateur.';
        });
      }
    } finally {
      if (mounted) {
        setState(() => _isProcessing = false);
      }
    }
  }
}