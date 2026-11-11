import 'package:flutter/material.dart';

/// Charte graphique officielle EECC :
/// 70 % blanc/fond clair, 20 % bleu-gris, 10 % noir/accents sombres
class EeccColors {
  // Couleurs principales
  static const Color background = Color(0xFFF8FAFC);
  static const Color surface = Colors.white;
  static const Color surfaceElevated = Color(0xFFFFFFFF);

  // Bleu-gris institutionnel (20 %)
  static const Color primary = Color(0xFF1E3A8A); // Bleu profond
  static const Color primaryLight = Color(0xFF3B82F6);
  static const Color primaryDark = Color(0xFF172554);
  static const Color slate = Color(0xFF475569); // Bleu-gris texte/icônes
  static const Color slateLight = Color(0xFF94A3B8);

  // Noir et texte (10 %)
  static const Color textMain = Color(0xFF0F172A);
  static const Color textSecondary = Color(0xFF64748B);
  static const Color border = Color(0xFFE2E8F0);

  // Signaux fonctionnels stricts
  static const Color liveRed = Color(0xFFDC2626); // EN DIRECT
  static const Color warningOrange = Color(0xFFF59E0B);
  static const Color successGreen = Color(0xFF10B981);
}
