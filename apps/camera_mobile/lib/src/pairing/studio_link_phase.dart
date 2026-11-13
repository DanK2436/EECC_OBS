import '../models/session_config.dart';

/// État de la liaison avec EECC Studio. L'aperçu caméra local n'en dépend pas.
enum StudioLinkPhase {
  disconnected,
  connecting,
  reconnecting,
  connected,
}

class StudioLinkLabels {
  static const disconnected = 'Déconnecté';
  static const connecting = 'Connexion en cours';
  static const reconnecting = 'Reconnexion en cours';
  static const connected = 'Connecté';

  static String badge(StudioLinkPhase phase) {
    switch (phase) {
      case StudioLinkPhase.disconnected:
        return disconnected;
      case StudioLinkPhase.connecting:
        return connecting;
      case StudioLinkPhase.reconnecting:
        return reconnecting;
      case StudioLinkPhase.connected:
        return connected;
    }
  }

  /// État vidéo, affiché seulement après une session confirmée.
  static String? video(StudioLinkPhase phase, TallyState tally) {
    if (phase != StudioLinkPhase.connected) return null;
    switch (tally) {
      case TallyState.program:
        return 'Vidéo à l\'antenne';
      case TallyState.preview:
        return 'Vidéo en aperçu';
      case TallyState.standby:
        return 'Vidéo en attente';
    }
  }
}
