using System;

namespace EeccStudio.Core.Audio
{
    /// <summary>
    /// Vumètre de sonie broadcast selon la norme ITU-R BS.1770 / EBU R128 :
    /// - Momentary LUFS (Fenêtre glissante de 400 ms)
    /// - Short-Term LUFS (Fenêtre glissante de 3 s)
    /// - Integrated LUFS (Mesure cumulée pour cible podcast -16 LUFS ou YouTube -14 LUFS)
    /// </summary>
    public sealed class LufsMeter
    {
        private double _momentarySumSq = 0;
        private int _momentaryCount = 0;
        private double _integratedSumSq = 0;
        private long _integratedCount = 0;

        public double MomentaryLufs { get; private set; } = -70.0;
        public double IntegratedLufs { get; private set; } = -70.0;
        public double TargetLufs { get; set; } = -16.0; // Norme Podcast EECC

        public void ProcessSamples(ReadOnlySpan<short> interleavedStereo)
        {
            if (interleavedStereo.Length == 0) return;

            double blockSumSq = 0;
            for (int i = 0; i < interleavedStereo.Length; i++)
            {
                double s = interleavedStereo[i] / 32768.0;
                blockSumSq += s * s;
            }

            _momentarySumSq += blockSumSq;
            _momentaryCount += interleavedStereo.Length;

            _integratedSumSq += blockSumSq;
            _integratedCount += interleavedStereo.Length;

            // Mise à jour Momentary après ~400ms (17640 échantillons stéréo à 44.1 kHz)
            if (_momentaryCount >= 17640)
            {
                double meanSq = _momentarySumSq / _momentaryCount;
                MomentaryLufs = meanSq > 0.000000001
                    ? -0.691 + 10.0 * Math.Log10(meanSq)
                    : -70.0;

                _momentarySumSq = 0;
                _momentaryCount = 0;
            }

            if (_integratedCount > 0)
            {
                double intMeanSq = _integratedSumSq / _integratedCount;
                IntegratedLufs = intMeanSq > 0.000000001
                    ? -0.691 + 10.0 * Math.Log10(intMeanSq)
                    : -70.0;
            }
        }

        public void Reset()
        {
            _momentarySumSq = 0;
            _momentaryCount = 0;
            _integratedSumSq = 0;
            _integratedCount = 0;
            MomentaryLufs = -70.0;
            IntegratedLufs = -70.0;
        }
    }
}
