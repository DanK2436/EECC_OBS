using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EeccStudio.Broadcast
{
    public sealed class MasterRecordConfig
    {
        public string OutputDirectory { get; set; } = "";
        public string FileNamePrefix { get; set; } = "EECC_Master";
        public string ContainerFormat { get; set; } = "mkv"; // MKV fragmenté résistant aux crashs
        public int TargetWidth { get; set; } = 1920;
        public int TargetHeight { get; set; } = 1080;
        public int TargetFps { get; set; } = 60;
        public int VideoBitrateKbps { get; set; } = 50000; // 50 Mbps Master
        public bool UseHardwareEncoder { get; set; } = true;
    }

    /// <summary>
    /// Enregistreur maître haute fidélité sur stockage local NVMe :
    /// - Indépendant du flux direct (ne subit aucune coupure due à l'internet)
    /// - Conteneur MKV fragmenté résistant aux pannes de courant
    /// - Encodage matériel GPU NVENC / QuickSync / AMF à haut débit (50 à 150 Mbps)
    /// </summary>
    public sealed class MasterRecorder : IDisposable
    {
        private Process? _process;
        private ProgramTube? _videoTube;
        private ProgramTube? _audioTube;
        private CancellationTokenSource? _cts;
        private Task? _videoPumpTask;
        private Task? _audioPumpTask;
        private volatile bool _isRecording;

        public bool IsRecording => _isRecording;
        public string? CurrentRecordingFilePath { get; private set; }
        public DateTime? RecordingStartedAt { get; private set; }
        public TimeSpan Duration => RecordingStartedAt.HasValue ? DateTime.UtcNow - RecordingStartedAt.Value : TimeSpan.Zero;

        public event Action<string>? RecordingStarted;
        public event Action<string, TimeSpan>? RecordingStopped;
        public event Action<string>? ErrorOccurred;

        public bool Start(
            MasterRecordConfig config,
            Func<byte[]?> currentFrame,
            Func<int, byte[]> renderAudio)
        {
            if (_isRecording) return true;

            var ffmpeg = FfmpegLocator.Find();
            if (string.IsNullOrWhiteSpace(ffmpeg))
            {
                ErrorOccurred?.Invoke("FFmpeg introuvable pour l'enregistrement maître.");
                return false;
            }

            var dir = string.IsNullOrWhiteSpace(config.OutputDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "EECC_Master_Recordings")
                : config.OutputDirectory;

            Directory.CreateDirectory(dir);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"{config.FileNamePrefix}_{timestamp}.{config.ContainerFormat}";
            var outputFilePath = Path.Combine(dir, filename);
            CurrentRecordingFilePath = outputFilePath;

            try
            {
                _videoTube = ProgramTube.Create("master_rec_video");
                _audioTube = ProgramTube.Create("master_rec_audio");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Impossible d'initialiser les tubes d'enregistrement: {ex.Message}");
                Cleanup();
                return false;
            }

            var encoderCaps = config.UseHardwareEncoder
                ? HardwareEncoderDetector.Detect()
                : new GpuEncoderCapabilities { PreferredEncoder = GpuEncoderType.SoftwareCpu, H264EncoderName = "libx264" };

            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            // Arguments FFmpeg pour enregistrement maître ultra-qualité
            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-loglevel");
            psi.ArgumentList.Add("warning");

            // Entrée audio PCM 16-bit 44.1 kHz stéréo
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("s16le");
            psi.ArgumentList.Add("-ar");
            psi.ArgumentList.Add("44100");
            psi.ArgumentList.Add("-ac");
            psi.ArgumentList.Add("2");
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(_audioTube.FfmpegPath);

            // Entrée vidéo MJPEG/Raw pipe
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("image2pipe");
            psi.ArgumentList.Add("-vcodec");
            psi.ArgumentList.Add("mjpeg");
            psi.ArgumentList.Add("-framerate");
            psi.ArgumentList.Add(config.TargetFps.ToString());
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(_videoTube.FfmpegPath);

            // Mapping
            psi.ArgumentList.Add("-map");
            psi.ArgumentList.Add("1:v:0");
            psi.ArgumentList.Add("-map");
            psi.ArgumentList.Add("0:a:0");

            // Encodage vidéo master haut débit
            psi.ArgumentList.Add("-c:v");
            psi.ArgumentList.Add(encoderCaps.H264EncoderName);

            if (encoderCaps.PreferredEncoder == GpuEncoderType.NvidiaNvenc)
            {
                psi.ArgumentList.Add("-preset");
                psi.ArgumentList.Add("p4"); // Haute qualité
                psi.ArgumentList.Add("-b:v");
                psi.ArgumentList.Add($"{config.VideoBitrateKbps}k");
                psi.ArgumentList.Add("-maxrate");
                psi.ArgumentList.Add($"{config.VideoBitrateKbps + 15000}k");
                psi.ArgumentList.Add("-bufsize");
                psi.ArgumentList.Add($"{config.VideoBitrateKbps * 2}k");
            }
            else if (encoderCaps.PreferredEncoder == GpuEncoderType.IntelQuickSync)
            {
                psi.ArgumentList.Add("-preset");
                psi.ArgumentList.Add("quality");
                psi.ArgumentList.Add("-b:v");
                psi.ArgumentList.Add($"{config.VideoBitrateKbps}k");
            }
            else if (encoderCaps.PreferredEncoder == GpuEncoderType.AmdAmf)
            {
                psi.ArgumentList.Add("-quality");
                psi.ArgumentList.Add("quality");
                psi.ArgumentList.Add("-b:v");
                psi.ArgumentList.Add($"{config.VideoBitrateKbps}k");
            }
            else
            {
                psi.ArgumentList.Add("-preset");
                psi.ArgumentList.Add("fast");
                psi.ArgumentList.Add("-crf");
                psi.ArgumentList.Add("18"); // Pratiquement sans perte visuelle
            }

            psi.ArgumentList.Add("-pix_fmt");
            psi.ArgumentList.Add("yuv420p");

            // Encodage audio Master PCM non compressé ou AAC 320k haute fidélité
            if (config.ContainerFormat.Equals("mkv", StringComparison.OrdinalIgnoreCase))
            {
                psi.ArgumentList.Add("-c:a");
                psi.ArgumentList.Add("pcm_s16le"); // Audio transparent
            }
            else
            {
                psi.ArgumentList.Add("-c:a");
                psi.ArgumentList.Add("aac");
                psi.ArgumentList.Add("-b:a");
                psi.ArgumentList.Add("320k");
            }

            // Flag de protection contre les coupures (MKV / MP4 fragmenté)
            if (config.ContainerFormat.Equals("mp4", StringComparison.OrdinalIgnoreCase))
            {
                psi.ArgumentList.Add("-movflags");
                psi.ArgumentList.Add("+frag_keyframe+empty_moov+default_base_moof");
            }

            psi.ArgumentList.Add("-y");
            psi.ArgumentList.Add(outputFilePath);

            try
            {
                _process = Process.Start(psi) ?? throw new InvalidOperationException("Échec de démarrage du processus FFmpeg Master.");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Impossible de démarrer l'enregistrement : {ex.Message}");
                Cleanup();
                return false;
            }

            _isRecording = true;
            RecordingStartedAt = DateTime.UtcNow;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _videoPumpTask = Task.Run(() => PumpVideoLoop(_process, _videoTube, currentFrame, config.TargetFps, token));
            _audioPumpTask = Task.Run(() => PumpAudioLoop(_process, _audioTube, renderAudio, token));

            RecordingStarted?.Invoke(outputFilePath);
            return true;
        }

        public void Stop()
        {
            if (!_isRecording) return;

            _isRecording = false;
            var duration = Duration;
            var filePath = CurrentRecordingFilePath ?? "";

            _cts?.Cancel();
            try { _process?.WaitForExit(3000); } catch { }

            Cleanup();
            RecordingStopped?.Invoke(filePath, duration);
        }

        private void PumpVideoLoop(Process proc, ProgramTube tube, Func<byte[]?> currentFrame, int fps, CancellationToken token)
        {
            try
            {
                tube.WaitConnected(proc, TimeSpan.FromSeconds(5));
                int intervalMs = Math.Max(5, 1000 / fps);

                while (!token.IsCancellationRequested && !proc.HasExited)
                {
                    var frame = currentFrame() ?? EeccTvFrames.Slate;
                    if (frame.Length >= 4 && frame[0] == 0xFF && frame[1] == 0xD8)
                    {
                        tube.Write(frame);
                    }
                    else
                    {
                        tube.Write(EeccTvFrames.Slate);
                    }
                    Thread.Sleep(intervalMs);
                }
            }
            catch { }
        }

        private void PumpAudioLoop(Process proc, ProgramTube tube, Func<int, byte[]> renderAudio, CancellationToken token)
        {
            try
            {
                tube.WaitConnected(proc, TimeSpan.FromSeconds(5));
                const int framesPerTick = 4410; // 100 ms

                while (!token.IsCancellationRequested && !proc.HasExited)
                {
                    var pcm = renderAudio(framesPerTick) ?? Array.Empty<byte>();
                    if (pcm.Length > 0)
                    {
                        tube.Write(pcm);
                    }
                    Thread.Sleep(90);
                }
            }
            catch { }
        }

        private void Cleanup()
        {
            _videoTube?.Dispose();
            _videoTube = null;
            _audioTube?.Dispose();
            _audioTube = null;

            if (_process != null)
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill(entireProcessTree: true);
                    }
                }
                catch { }
                _process.Dispose();
                _process = null;
            }

            _cts?.Dispose();
            _cts = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
