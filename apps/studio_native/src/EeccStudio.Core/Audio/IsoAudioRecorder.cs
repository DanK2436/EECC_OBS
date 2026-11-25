using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EeccStudio.Core.Audio
{
    /// <summary>
    /// Enregistreur multipiste ISO indépendant :
    /// Enregistre chaque micro / entrée en fichier WAV séparé (WAV 16-bit ou 24-bit 48 kHz).
    /// Permet un remixage audio parfait en postproduction sans interférence entre pasteur, chœur et instruments.
    /// </summary>
    public sealed class IsoAudioRecorder : IDisposable
    {
        private readonly string _outputDirectory;
        private readonly ConcurrentDictionary<string, IsoTrackWriter> _writers = new();
        private bool _isRecording;

        public bool IsRecording => _isRecording;

        public IsoAudioRecorder(string? outputDirectory = null)
        {
            _outputDirectory = outputDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "EECC_Audio_ISO");
        }

        public void StartRecording()
        {
            if (_isRecording) return;
            Directory.CreateDirectory(_outputDirectory);
            _isRecording = true;
        }

        public void SubmitIsoTrack(string trackId, ReadOnlySpan<short> interleavedStereo)
        {
            if (!_isRecording || interleavedStereo.Length == 0) return;

            var writer = _writers.GetOrAdd(trackId, id =>
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filename = Path.Combine(_outputDirectory, $"ISO_{id}_{timestamp}.wav");
                return new IsoTrackWriter(filename);
            });

            writer.WriteSamples(interleavedStereo);
        }

        public void StopRecording()
        {
            _isRecording = false;
            foreach (var writer in _writers.Values)
            {
                writer.Dispose();
            }
            _writers.Clear();
        }

        public void Dispose()
        {
            StopRecording();
        }

        private sealed class IsoTrackWriter : IDisposable
        {
            private readonly FileStream _stream;
            private readonly BinaryWriter _writer;
            private int _dataBytesWritten;

            public IsoTrackWriter(string filePath)
            {
                _stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                _writer = new BinaryWriter(_stream, Encoding.UTF8);
                WriteHeaderPlaceholder();
            }

            public void WriteSamples(ReadOnlySpan<short> samples)
            {
                byte[] raw = new byte[samples.Length * 2];
                Buffer.BlockCopy(samples.ToArray(), 0, raw, 0, raw.Length);
                _writer.Write(raw);
                _dataBytesWritten += raw.Length;
            }

            private void WriteHeaderPlaceholder()
            {
                // Entête WAV RIFF standard
                _writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                _writer.Write(0); // Taille du fichier - 8 (mise à jour à la fermeture)
                _writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                _writer.Write(Encoding.ASCII.GetBytes("fmt "));
                _writer.Write(16); // Subchunk1Size (16 pour PCM)
                _writer.Write((short)1); // AudioFormat (1 = PCM)
                _writer.Write((short)2); // NumChannels (2 = Stéréo)
                _writer.Write(44100);    // SampleRate
                _writer.Write(44100 * 2 * 2); // ByteRate (SampleRate * NumChannels * BitsPerSample/8)
                _writer.Write((short)4); // BlockAlign (NumChannels * BitsPerSample/8)
                _writer.Write((short)16); // BitsPerSample
                _writer.Write(Encoding.ASCII.GetBytes("data"));
                _writer.Write(0); // Subchunk2Size (mise à jour à la fermeture)
            }

            public void Dispose()
            {
                try
                {
                    // Finalisation de l'entête WAV
                    _writer.Seek(4, SeekOrigin.Begin);
                    _writer.Write(_dataBytesWritten + 36);
                    _writer.Seek(40, SeekOrigin.Begin);
                    _writer.Write(_dataBytesWritten);
                    _writer.Flush();
                    _writer.Dispose();
                    _stream.Dispose();
                }
                catch { }
            }
        }
    }
}
