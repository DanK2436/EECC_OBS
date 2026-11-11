import 'package:flutter/material.dart';

class EeccCamColors {
  static const Color background = Color(0xFF0F172A); // Fond sombre immersif pour la prise de vue
  static const Color surface = Color(0xFF1E293B);
  static const Color surfaceLight = Color(0xFF334155);

  static const Color primary = Color(0xFF2563EB);
  static const Color primaryLight = Color(0xFF60A5FA);

  static const Color textMain = Colors.white;
  static const Color textSecondary = Color(0xFF94A3B8);

  // Feux Tally stricts (Section 5.4)
  static const Color tallyProgram = Color(0xFFDC2626); // Rouge : Caméra en direct à l'antenne
  static const Color tallyPreview = Color(0xFF16A34A); // Vert : Caméra en prévisualisation dans Studio
  static const Color tallyStandby = Color(0xFF64748B); // Gris : Caméra connectée mais en attente
}
