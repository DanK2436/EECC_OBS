using System;
using System.IO;
using EeccStudio.Core.Audio;
using Xunit;

namespace EeccStudio.Core.Tests
{
    public sealed class AudioDspAndIsoTests
    {
        [Fact]
        public void DspChain_HighPassAndLimiter_PreventsClipping()
        {
            var dsp = new DspChain(44100);
            dsp.EnableHighPass = true;
            dsp.EnableCompressor = true;
            dsp.EnableLimiter = true;
            dsp.LimitCeilingDb = -1.0f; // -1 dBFS = ~29205 en 16-bit

            // Échantillon saturé à pleine échelle (short.MaxValue = 32767)
            short[] samples = new short[1000];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = (short)(Math.Sin(i * 0.1) * 32767.0);
            }

            dsp.Process(samples);

            // Aucun échantillon ne doit dépasser le plafond du limiteur
            int maxExpected = (int)(Math.Pow(10.0, -1.0 / 20.0) * 32767.0) + 100;
            foreach (var s in samples)
            {
                Assert.InRange(s, -maxExpected, maxExpected);
            }
        }

        [Fact]
        public void DspChain_LipsyncDelay_DelaysSignal()
        {
            var dsp = new DspChain(44100);
            dsp.DelayMs = 50; // 50 ms de retard

            short[] pulse = new short[200];
            pulse[0] = 20000; // Impulsion

            dsp.Process(pulse);

            // À l'instant t=0 avec retard de 50ms, l'impulsion ne doit pas encore sortir
            Assert.Equal(0, pulse[0]);
        }

        [Fact]
        public void LufsMeter_ProcessSamples_UpdatesLufsReadings()
        {
            var meter = new LufsMeter();
            meter.TargetLufs = -16.0;

            short[] signal = new short[44100 * 2]; // 1 seconde de signal
            for (int i = 0; i < signal.Length; i++)
            {
                signal[i] = (short)(Math.Sin(i * 0.05) * 10000);
            }

            meter.ProcessSamples(signal);

            Assert.True(meter.MomentaryLufs > -70.0);
            Assert.True(meter.IntegratedLufs > -70.0);
        }

        [Fact]
        public void IsoAudioRecorder_CreatesValidWavFile()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "Eecc_IsoTest_" + Guid.NewGuid().ToString("N"));
            try
            {
                var recorder = new IsoAudioRecorder(tempDir);
                recorder.StartRecording();
                Assert.True(recorder.IsRecording);

                short[] samples = new short[4410 * 2]; // 100 ms
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = 1000;
                }

                recorder.SubmitIsoTrack("pastor_mic", samples);
                recorder.StopRecording();
                Assert.False(recorder.IsRecording);

                var files = Directory.GetFiles(tempDir, "*.wav");
                Assert.NotEmpty(files);

                // Vérification de l'entête WAV
                using var fs = File.OpenRead(files[0]);
                byte[] header = new byte[12];
                fs.Read(header, 0, 12);
                string riff = System.Text.Encoding.ASCII.GetString(header, 0, 4);
                string wave = System.Text.Encoding.ASCII.GetString(header, 8, 4);

                Assert.Equal("RIFF", riff);
                Assert.Equal("WAVE", wave);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }
    }
}
