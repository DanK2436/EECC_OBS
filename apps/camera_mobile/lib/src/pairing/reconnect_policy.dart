class ReconnectDecision {
  final bool retry;
  final Duration delay;
  final String message;

  const ReconnectDecision(this.retry, this.delay, this.message);
}

class ReconnectPolicy {
  static const int maxAttempts = 3;
  static const fatalCodes = {
    'token_invalid',
    'token_expired',
    'token_used',
    'capacity',
    'session_expired',
    'session_unknown',
  };

  static ReconnectDecision decide({
    required bool userLeft,
    required bool hasSession,
    required String? rejectCode,
    required int attempts,
  }) {
    if (userLeft) {
      return const ReconnectDecision(false, Duration.zero, 'Déconnecté.');
    }
    if (rejectCode != null && fatalCodes.contains(rejectCode)) {
      return const ReconnectDecision(false, Duration.zero, '');
    }
    if (!hasSession) {
      return const ReconnectDecision(
        false,
        Duration.zero,
        'La régie n\'a pas confirmé la session. Scannez le QR à nouveau.',
      );
    }
    if (attempts >= maxAttempts) {
      return const ReconnectDecision(
        false,
        Duration.zero,
        'Connexion perdue. Appuyez sur Reconnecter.',
      );
    }
    return ReconnectDecision(
      true,
      Duration(seconds: 1 << attempts),
      'Connexion perdue — tentative ${attempts + 1}/$maxAttempts.',
    );
  }
}
