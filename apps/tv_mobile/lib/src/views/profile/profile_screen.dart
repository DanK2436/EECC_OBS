import 'package:flutter/material.dart';
import '../../constants/colors.dart';
import '../../constants/config.dart';
import '../donation/donation_modal.dart';

class ProfileScreen extends StatelessWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: EeccColors.background,
      appBar: AppBar(
        backgroundColor: EeccColors.surface,
        elevation: 0,
        title: const Text(
          'Mon Profil & Église',
          style: TextStyle(fontWeight: FontWeight.bold, color: EeccColors.textMain),
        ),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          children: [
            // Carte Utilisateur
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: EeccColors.surface,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: EeccColors.border),
              ),
              child: Row(
                children: [
                  CircleAvatar(
                    radius: 30,
                    backgroundColor: EeccColors.primary.withOpacity(0.1),
                    child: const Icon(Icons.person_rounded, size: 36, color: EeccColors.primary),
                  ),
                  const SizedBox(width: 16),
                  const Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Fidèle EECC',
                          style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: EeccColors.textMain),
                        ),
                        SizedBox(height: 4),
                        Text(
                          'fidele@eecc-eglise.org',
                          style: TextStyle(fontSize: 13, color: EeccColors.textSecondary),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // Raccourcis Dons et Reçus
            _buildSectionTitle('Générosité'),
            _buildMenuTile(
              icon: Icons.volunteer_activism_rounded,
              title: 'Faire un don ou payer sa dîme',
              subtitle: 'Soutien sécurisé par Maishapay',
              onTap: () => DonationModal.show(context),
            ),
            _buildMenuTile(
              icon: Icons.receipt_long_rounded,
              title: 'Mes reçus de dons',
              subtitle: 'Historique des contributions',
              onTap: () {},
            ),

            const SizedBox(height: 20),
            _buildSectionTitle('Paramètres & Notifications'),
            _buildMenuTile(
              icon: Icons.notifications_active_outlined,
              title: 'Notifications de culte',
              subtitle: 'Alertes début de direct et nouveaux sermons',
              trailing: Switch(value: true, activeColor: EeccColors.primary, onChanged: (v) {}),
              onTap: () {},
            ),
            _buildMenuTile(
              icon: Icons.language_rounded,
              title: 'Langue de l\'application',
              subtitle: 'Français (par défaut)',
              onTap: () {},
            ),

            const SizedBox(height: 20),
            _buildSectionTitle('À propos de l\'EECC'),
            _buildMenuTile(
              icon: Icons.info_outline_rounded,
              title: 'Vision et Responsables de l\'église',
              subtitle: 'Découvrir la mission EECC',
              onTap: () {},
            ),
            _buildMenuTile(
              icon: Icons.location_on_outlined,
              title: 'Nos églises & Implantations',
              subtitle: 'Horaires et adresses des cultes',
              onTap: () {},
            ),

            const SizedBox(height: 30),
            Text(
              '${EeccConfig.appName} v${EeccConfig.appVersion}',
              style: const TextStyle(fontSize: 12, color: EeccColors.slateLight),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildSectionTitle(String title) {
    return Align(
      alignment: Alignment.centerLeft,
      child: Padding(
        padding: const EdgeInsets.only(left: 4, bottom: 8),
        child: Text(
          title,
          style: const TextStyle(fontSize: 13, fontWeight: FontWeight.bold, color: EeccColors.slate),
        ),
      ),
    );
  }

  Widget _buildMenuTile({
    required IconData icon,
    required String title,
    required String subtitle,
    Widget? trailing,
    required VoidCallback onTap,
  }) {
    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      decoration: BoxDecoration(
        color: EeccColors.surface,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: EeccColors.border),
      ),
      child: ListTile(
        leading: Icon(icon, color: EeccColors.primary),
        title: Text(title, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 14)),
        subtitle: Text(subtitle, style: const TextStyle(fontSize: 12, color: EeccColors.textSecondary)),
        trailing: trailing ?? const Icon(Icons.chevron_right_rounded, color: EeccColors.slateLight),
        onTap: onTap,
      ),
    );
  }
}
