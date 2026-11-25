using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EeccStudio.Broadcast
{
    public sealed record ReplayFrame(byte[] JpegData, DateTime Timestamp);

    /// <summary>
    /// Replay Buffer circulaire en RAM :
    /// Conserve en continu les 30 à 60 dernières secondes du signal Program.
    /// Permet d'exporter instantanément un clip de ralenti ou un temps fort du culte
    /// (ex: action de grâce, témoignage, solo de chant) sans interrompre l'enregistrement ni le streaming.
    /// </summary>
    public sealed class ReplayBuffer : IDisposable
    {
        private readonly ConcurrentQueue<ReplayFrame> _frames = new();
        private readonly int _maxFrames;
        private readonly string _exportDirectory;
        private bool _isActive;

        public bool IsActive => _isActive;
        public int CapacitySeconds { get; }
        public int CurrentFramesCount => _frames.Count;

        public event Action<string>? ReplaySaved;

        public ReplayBuffer(int capacitySeconds = 30, int expectedFps = 30, string? exportDir = null)
        {
            CapacitySeconds = Math.Clamp(capacitySeconds, 5, 120);
            _maxFrames = CapacitySeconds * expectedFps;
            _exportDirectory = exportDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "EECC_Replays");
        }

        public void Start()
        {
            _isActive = true;
            Directory.CreateDirectory(_exportDirectory);
        }

        public void AppendFrame(byte[]? jpegData)
        {
            if (!_isActive || jpegData == null || jpegData.Length == 0) return;

            _frames.Enqueue(new ReplayFrame(jpegData, DateTime.UtcNow));

            while (_frames.Count > _maxFrames && _frames.TryDequeue(out _))
            {
                // Libération FIFO des trames anciennes au-delà de la fenêtre glissante
            }
        }

        /// <summary>
        /// Sauvegarde instantanément les secondes en mémoire dans un fichier master replay daté.
        /// </summary>
        public async Task<string?> SaveReplayAsync()
        {
            if (_frames.IsEmpty) return null;

            var snapshot = _frames.ToArray();
            if (snapshot.Length == 0) return null;

            Directory.CreateDirectory(_exportDirectory);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var outPath = Path.Combine(_exportDirectory, $"Replay_{timestamp}_{snapshot.Length}frames.raw_clip");

            await Task.Run(() =>
            {
                using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var bw = new BinaryWriter(fs);

                bw.Write(snapshot.Length);
                foreach (var f in snapshot)
                {
                    bw.Write(f.JpegData.Length);
                    bw.Write(f.JpegData);
                }
            }).ConfigureAwait(false);

            ReplaySaved?.Invoke(outPath);
            return outPath;
        }

        public void Stop()
        {
            _isActive = false;
            while (_frames.TryDequeue(out _)) { }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
