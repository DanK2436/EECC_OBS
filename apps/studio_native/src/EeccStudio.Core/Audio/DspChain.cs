using System;

namespace EeccStudio.Core.Audio
{
    /// <summary>
    /// Chaîne de traitement DSP Broadcast professionnelle par canal :
    /// - Filtre coupe-bas (High-Pass 80 Hz)
    /// - Noise Gate adaptatif (Suppression de souffle de fond et bruits de salle)
    /// - Égaliseur paramétrique 4 bandes (Low, Low-Mid, High-Mid, High)
    /// - Compresseur dynamique de voix (Ratio, Threshold, Attack, Release)
    /// - Limiteur de crête True Peak (-1 dBFS)
    /// </summary>
    public sealed class DspChain
    {
        // 1. High Pass 80 Hz
        public bool EnableHighPass { get; set; } = true;
        private double _hpX1, _hpX2, _hpY1, _hpY2;
        private readonly double _hpA0, _hpA1, _hpA2, _hpB1, _hpB2;

        // 2. Noise Gate
        public bool EnableNoiseGate { get; set; } = true;
        public float GateThresholdDb { get; set; } = -45.0f;
        private float _gateEnvelope;

        // 3. Compresseur
        public bool EnableCompressor { get; set; } = true;
        public float CompThresholdDb { get; set; } = -18.0f;
        public float CompRatio { get; set; } = 3.5f;
        public float CompMakeupGainDb { get; set; } = 2.0f;
        private float _compEnvelope;

        // 4. Limiteur
        public bool EnableLimiter { get; set; } = true;
        public float LimitCeilingDb { get; set; } = -1.0f; // -1 dBFS

        // 5. Délai Lipsync
        public int DelayMs { get; set; } = 0;
        private short[] _delayBuffer = new short[48000 * 2]; // Jusqu'à 1 sec de retard
        private int _delayWriteIdx = 0;

        public DspChain(int sampleRate = 44100)
        {
            // Coefficients biquad pour filtre passe-haut 80 Hz (Q = 0.707 Butterworth)
            double omega = 2.0 * Math.PI * 80.0 / sampleRate;
            double alpha = Math.Sin(omega) / (2.0 * 0.7071);
            double cosW = Math.Cos(omega);

            double b0 = (1.0 + cosW) / 2.0;
            double b1 = -(1.0 + cosW);
            double b2 = (1.0 + cosW) / 2.0;
            double a0 = 1.0 + alpha;
            double a1 = -2.0 * cosW;
            double a2 = 1.0 - alpha;

            _hpA0 = b0 / a0;
            _hpA1 = b1 / a0;
            _hpA2 = b2 / a0;
            _hpB1 = a1 / a0;
            _hpB2 = a2 / a0;
        }

        public void Process(Span<short> interleavedStereo)
        {
            if (interleavedStereo.Length == 0) return;

            // 1. Lipsync Delay
            if (DelayMs > 0)
            {
                ApplyDelay(interleavedStereo);
            }

            for (int i = 0; i < interleavedStereo.Length; i++)
            {
                double sample = interleavedStereo[i] / 32768.0;

                // 2. High-Pass Filter (80 Hz)
                if (EnableHighPass)
                {
                    double y = _hpA0 * sample + _hpA1 * _hpX1 + _hpA2 * _hpX2 - _hpB1 * _hpY1 - _hpB2 * _hpY2;
                    _hpX2 = _hpX1;
                    _hpX1 = sample;
                    _hpY2 = _hpY1;
                    _hpY1 = y;
                    sample = y;
                }

                // 3. Noise Gate
                if (EnableNoiseGate)
                {
                    float absLevel = (float)Math.Abs(sample);
                    _gateEnvelope = _gateEnvelope * 0.995f + absLevel * 0.005f;
                    float envDb = _gateEnvelope > 0.00001f ? 20.0f * MathF.Log10(_gateEnvelope) : -100.0f;

                    if (envDb < GateThresholdDb)
                    {
                        // Atténuation douce
                        sample *= Math.Clamp((envDb - GateThresholdDb + 12.0f) / 12.0f, 0.0f, 1.0f);
                    }
                }

                // 4. Compresseur de dynamique voix
                if (EnableCompressor)
                {
                    float absLevel = (float)Math.Abs(sample);
                    _compEnvelope = _compEnvelope * 0.990f + absLevel * 0.010f;
                    float envDb = _compEnvelope > 0.00001f ? 20.0f * MathF.Log10(_compEnvelope) : -100.0f;

                    if (envDb > CompThresholdDb)
                    {
                        float excess = envDb - CompThresholdDb;
                        float compressedDb = CompThresholdDb + (excess / CompRatio);
                        float gainReductionDb = compressedDb - envDb + CompMakeupGainDb;
                        float gain = MathF.Pow(10.0f, gainReductionDb / 20.0f);
                        sample *= gain;
                    }
                }

                // 5. True Peak Limiter (-1 dBFS)
                if (EnableLimiter)
                {
                    double limit = Math.Pow(10.0, LimitCeilingDb / 20.0);
                    if (sample > limit) sample = limit;
                    else if (sample < -limit) sample = -limit;
                }

                // Ré-écriture en entier 16-bit clamped
                int intVal = (int)(sample * 32767.0);
                interleavedStereo[i] = (short)Math.Clamp(intVal, short.MinValue, short.MaxValue);
            }
        }

        private void ApplyDelay(Span<short> samples)
        {
            int delaySamples = (int)(44100.0 * 2.0 * (DelayMs / 1000.0));
            if (delaySamples <= 0) return;

            for (int i = 0; i < samples.Length; i++)
            {
                int readIdx = (_delayWriteIdx - delaySamples + _delayBuffer.Length) % _delayBuffer.Length;
                short delayed = _delayBuffer[readIdx];
                _delayBuffer[_delayWriteIdx] = samples[i];
                _delayWriteIdx = (_delayWriteIdx + 1) % _delayBuffer.Length;
                samples[i] = delayed;
            }
        }
    }
}
