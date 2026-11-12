import 'package:flutter/material.dart';

/// Charte graphique officielle EECC TV :
/// APLATS PURS DE COULEURS SOLIDES (0% dégradé, zéro gradient)
/// Définie pour la lisibilité biblique, la sérénité et le direct.
class EeccColors {
  // 1. Fonds et surfaces solides
  static const Color background = Color(0xFFF8FAFC); // Fond blanc céleste reposant
  static const Color surface = Color(0xFFFFFFFF); // Surface pure blanche
  static const Color surfaceElevated = Color(0xFFFFFFFF);
  static const Color border = Color(0xFFE2E8F0); // Bordure solide nette de 1 px

  // 2. Couleur institutionnelle solide
  static const Color primary = Color(0xFF1E3A8A); // Bleu royal église profond
  static const Color primaryLight = Color(0xFF2563EB); // Bleu d'état actif
  static const Color primaryDark = Color(0xFF1E293B);

  // 3. Typographie solide contrastée
  static const Color textMain = Color(0xFF0F172A); // Noir ardoise profond (Contraste > 12:1)
  static const Color textSecondary = Color(0xFF475569); // Gris neutre descriptif
  static const Color textMuted = Color(0xFF94A3B8); // Gris légende
  static const Color slate = textSecondary;
  static const Color slateLight = textMuted;

  // 4. Signaux fonctionnels stricts (Aplats solides)
  static const Color liveRed = Color(0xFFB91C1C); // Badge solide « EN DIRECT »
  static const Color goldLiturgical = Color(0xFFD97706); // Or liturgique solide (Dons, favoris)
  static const Color warningOrange = Color(0xFFD97706);
  static const Color successGreen = Color(0xFF15803D);
}
