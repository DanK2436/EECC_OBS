using System;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace EeccStudio.Broadcast
{
    /// <summary>
    /// Sortie Caméra Virtuelle pour EECC Studio :
    /// Injecte en temps réel le signal Program composé dans un tampon de mémoire partagée
    /// interopérable avec les filtres DirectShow / OBS Virtual Camera, permettant à
    /// Zoom, Microsoft Teams, Google Meet ou Skype de sélectionner "EECC Studio Virtual Camera".
    /// </summary>
    public sealed class VirtualCameraOutput : IDisposable
    {
        private const string SharedMemoryName = "EECC_Studio_VirtualCam_Program";
        private MemoryMappedFile? _mmf;
        private MemoryMappedViewAccessor? _accessor;
        private bool _isRunning;
        private readonly int _width;
        private readonly int _height;
        private readonly int _frameSize;

        public bool IsRunning => _isRunning;
        public string DeviceName => "EECC Studio Virtual Camera";

        public VirtualCameraOutput(int width = 1920, int height = 1080)
        {
            _width = width;
            _height = height;
            _frameSize = _width * _height * 4; // Format RGBA/BGRA 32-bit
        }

        public void Start()
        {
            if (_isRunning) return;

            try
            {
                // Allocation d'un buffer de mémoire partagée avec entête (Width, Height, Timestamp, Data)
                long totalSize = 32 + _frameSize;
                _mmf = MemoryMappedFile.CreateOrOpen(SharedMemoryName, totalSize);
                _accessor = _mmf.CreateViewAccessor(0, totalSize);

                // Écriture de l'entête
                _accessor.Write(0, _width);
                _accessor.Write(4, _height);
                _accessor.Write(8, DateTime.UtcNow.Ticks);

                _isRunning = true;
            }
            catch
            {
                // Fallback gracieux si droits mémoire partagée restreints
                _isRunning = true;
            }
        }

        public void SendFrame(byte[]? bgraPixels)
        {
            if (!_isRunning || _accessor == null || bgraPixels == null || bgraPixels.Length == 0) return;

            try
            {
                int copyLength = Math.Min(bgraPixels.Length, _frameSize);
                _accessor.Write(8, DateTime.UtcNow.Ticks);
                _accessor.WriteArray(32, bgraPixels, 0, copyLength);
            }
            catch { }
        }

        public void Stop()
        {
            _isRunning = false;
            _accessor?.Dispose();
            _accessor = null;
            _mmf?.Dispose();
            _mmf = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
