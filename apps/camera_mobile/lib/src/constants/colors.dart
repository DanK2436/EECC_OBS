import 'package:flutter/material.dart';

/// Charte graphique officielle EECC Caméra :
/// APLATS PURS DE COULEURS SOLIDES (0% dégradé, zéro gradient)
/// Définie pour l'obscurité en culte (dalles OLED) et la visibilité Tally à distance.
class EeccCamColors {
  // 1. Fond et surfaces solides (Zéro reflet parasite pour l'assemblée)
  static const Color background = Color(0xFF000000); // Noir OLED pur (pixels éteints)
  static const Color surface = Color(0xFF18181B); // Surface solide sombre pour boutons
  static const Color surfaceLight = Color(0xFF27272A);
  static const Color border = Color(0xFF3F3F46); // Bordure solide 1px

  // 2. Accents et sélections
  static const Color accent = Color(0xFF3B82F6); // Bleu de réglage solide
  static const Color telemetryGauge = Color(0xFFFACC15); // Jaune studio solide (Batterie, Débit, FPS)

  // 3. Typographie solide haute lisibilité
  static const Color textMain = Color(0xFFFFFFFF); // Blanc pur
  static const Color textSecondary = Color(0xFFA1A1AA); // Gris moyen lisible

  // 4. Feux Tally stricts (Aplats solides ultra-visibles)
  static const Color tallyProgram = Color(0xFFE11D48); // Rouge solide éclatant : À L'ANTENNE
  static const Color tallyPreview = Color(0xFF15803D); // Vert émeraude solide : EN APERÇU (CUE)
  static const Color tallyStandby = Color(0xFF475569); // Gris acier solide : EN ATTENTE
}
