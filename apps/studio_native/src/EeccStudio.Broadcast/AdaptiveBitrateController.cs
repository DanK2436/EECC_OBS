using System;

namespace EeccStudio.Broadcast
{
    public sealed class BitrateProfile
    {
        public int BitrateKbps { get; init; }
        public int ResolutionHeight { get; init; }
        public string QualityLabel { get; init; } = "";
    }

    /// <summary>
    /// Contrôleur de Débit Adaptatif (Adaptive Bitrate / Dynamic Bonding) pour flux direct :
    /// - Surveille la santé de la connexion montante de l'église (perte de paquets, congestion RTMPS/SRT).
    /// - Ajuste automatiquement le débit du flux en direct (ex: 6000 kbps -> 4500 kbps -> 3000 kbps -> 1800 kbps)
    /// - Règle absolue de sécurité : NE TOUCHE JAMAIS à l'enregistrement maître local NVMe, qui reste à 50-80 Mbps.
    /// </summary>
    public sealed class AdaptiveBitrateController
    {
        private readonly int _maxBitrateKbps;
        private readonly int _minBitrateKbps;
        private int _currentBitrateKbps;
        private int _consecutiveCongestions;

        public int CurrentBitrateKbps => _currentBitrateKbps;
        public bool IsThrottled => _currentBitrateKbps < _maxBitrateKbps;

        public event Action<int, string>? BitrateChanged;

        public AdaptiveBitrateController(int targetBitrateKbps = 4500, int minBitrateKbps = 1500)
        {
            _maxBitrateKbps = targetBitrateKbps;
            _minBitrateKbps = minBitrateKbps;
            _currentBitrateKbps = targetBitrateKbps;
        }

        public void ReportNetworkStatus(double rttMs, double packetLossPercent, double uploadThroughputKbps)
        {
            // Détection de congestion réseau église (RTT > 500ms ou perte de paquets > 3%)
            bool isCongested = packetLossPercent > 3.0 || rttMs > 450.0;

            if (isCongested)
            {
                _consecutiveCongestions++;
                if (_consecutiveCongestions >= 2 && _currentBitrateKbps > _minBitrateKbps)
                {
                    // Réduction progressive de 20%
                    int newBitrate = Math.Max(_minBitrateKbps, (int)(_currentBitrateKbps * 0.80));
                    if (newBitrate != _currentBitrateKbps)
                    {
                        _currentBitrateKbps = newBitrate;
                        BitrateChanged?.Invoke(_currentBitrateKbps, $"Congestion détectée (Perte: {packetLossPercent:F1}%, Ping: {rttMs:F0}ms). Débit streaming direct réduit à {_currentBitrateKbps} kbps.");
                    }
                }
            }
            else
            {
                // Rétablissement progressif si le lien est stable
                if (_consecutiveCongestions > 0)
                    _consecutiveCongestions--;

                if (_consecutiveCongestions == 0 && _currentBitrateKbps < _maxBitrateKbps && rttMs < 120.0 && packetLossPercent < 0.5)
                {
                    int stepUp = Math.Min(_maxBitrateKbps, _currentBitrateKbps + 500);
                    if (stepUp != _currentBitrateKbps)
                    {
                        _currentBitrateKbps = stepUp;
                        BitrateChanged?.Invoke(_currentBitrateKbps, $"Connexion internet rétablie. Débit streaming augmenté à {_currentBitrateKbps} kbps.");
                    }
                }
            }
        }

        public void Reset()
        {
            _currentBitrateKbps = _maxBitrateKbps;
            _consecutiveCongestions = 0;
        }
    }
}
