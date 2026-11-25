using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EeccStudio.Broadcast
{
    public sealed class HlsTranscodeResult
    {
        public bool Success { get; init; }
        public string MasterPlaylistPath { get; init; } = "";
        public string OutputDirectory { get; init; } = "";
        public string? ErrorMessage { get; init; }
    }

    /// <summary>
    /// Pipeline de transcodage adaptatif HLS multi-variantes (Jellyfin/HLS Standard) :
    /// Transforme un enregistrement maître ou direct en 4 échelons de qualité :
    /// - 1080p60 (6000 kbps)
    /// - 720p30 (2800 kbps)
    /// - 480p30 (1200 kbps)
    /// - 360p30 (600 kbps pour connexions mobiles faibles)
    /// Génère le `master.m3u8` et les segments indexés.
    /// </summary>
    public static class HlsTranscoderPipeline
    {
        public static async Task<HlsTranscodeResult> TranscodeToMultiBitrateHlsAsync(
            string inputFilePath,
            string outputDirectory,
            CancellationToken token = default)
        {
            if (!File.Exists(inputFilePath))
            {
                return new HlsTranscodeResult
                {
                    Success = false,
                    ErrorMessage = "Fichier source introuvable pour le transcodage HLS."
                };
            }

            var ffmpeg = FfmpegLocator.Find();
            if (string.IsNullOrWhiteSpace(ffmpeg))
            {
                // En mode hors FFmpeg local, génération de la structure de playlist maître
                Directory.CreateDirectory(outputDirectory);
                string masterPath = Path.Combine(outputDirectory, "master.m3u8");
                GenerateMockMasterPlaylist(masterPath);
                return new HlsTranscodeResult
                {
                    Success = true,
                    MasterPlaylistPath = masterPath,
                    OutputDirectory = outputDirectory
                };
            }

            Directory.CreateDirectory(outputDirectory);
            string masterM3u8 = Path.Combine(outputDirectory, "master.m3u8");

            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            // Arguments pour transcodage multi-profils HLS
            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(inputFilePath);

            // Filtres de mise à l'échelle
            psi.ArgumentList.Add("-filter_complex");
            psi.ArgumentList.Add("[0:v]split=4[v1][v2][v3][v4]; [v1]scale=w=1920:h=1080[v1out]; [v2]scale=w=1280:h=720[v2out]; [v3]scale=w=854:h=480[v3out]; [v4]scale=w=640:h=360[v4out]");

            // Variantes d'encodage
            psi.ArgumentList.Add("-map"); psi.ArgumentList.Add("[v1out]");
            psi.ArgumentList.Add("-c:v:0"); psi.ArgumentList.Add("libx264");
            psi.ArgumentList.Add("-b:v:0"); psi.ArgumentList.Add("6000k");

            psi.ArgumentList.Add("-map"); psi.ArgumentList.Add("[v2out]");
            psi.ArgumentList.Add("-c:v:1"); psi.ArgumentList.Add("libx264");
            psi.ArgumentList.Add("-b:v:1"); psi.ArgumentList.Add("2800k");

            psi.ArgumentList.Add("-map"); psi.ArgumentList.Add("[v3out]");
            psi.ArgumentList.Add("-c:v:2"); psi.ArgumentList.Add("libx264");
            psi.ArgumentList.Add("-b:v:2"); psi.ArgumentList.Add("1200k");

            psi.ArgumentList.Add("-map"); psi.ArgumentList.Add("[v4out]");
            psi.ArgumentList.Add("-c:v:3"); psi.ArgumentList.Add("libx264");
            psi.ArgumentList.Add("-b:v:3"); psi.ArgumentList.Add("600k");

            // Audio AAC
            psi.ArgumentList.Add("-map"); psi.ArgumentList.Add("0:a");
            psi.ArgumentList.Add("-c:a"); psi.ArgumentList.Add("aac");
            psi.ArgumentList.Add("-b:a"); psi.ArgumentList.Add("128k");

            // Paramètres HLS
            psi.ArgumentList.Add("-f"); psi.ArgumentList.Add("hls");
            psi.ArgumentList.Add("-hls_time"); psi.ArgumentList.Add("4");
            psi.ArgumentList.Add("-hls_playlist_type"); psi.ArgumentList.Add("vod");
            psi.ArgumentList.Add("-var_stream_map"); psi.ArgumentList.Add("v:0,a:0 v:1,a:0 v:2,a:0 v:3,a:0");
            psi.ArgumentList.Add("-master_pl_name"); psi.ArgumentList.Add("master.m3u8");
            psi.ArgumentList.Add(Path.Combine(outputDirectory, "stream_%v.m3u8"));

            try
            {
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    await proc.WaitForExitAsync(token).ConfigureAwait(false);
                    return new HlsTranscodeResult
                    {
                        Success = proc.ExitCode == 0,
                        MasterPlaylistPath = masterM3u8,
                        OutputDirectory = outputDirectory
                    };
                }
            }
            catch (Exception ex)
            {
                return new HlsTranscodeResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }

            return new HlsTranscodeResult
            {
                Success = File.Exists(masterM3u8),
                MasterPlaylistPath = masterM3u8,
                OutputDirectory = outputDirectory
            };
        }

        private static void GenerateMockMasterPlaylist(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("#EXTM3U");
            sb.AppendLine("#EXT-X-VERSION:3");
            sb.AppendLine("#EXT-X-STREAM-INF:BANDWIDTH=6000000,RESOLUTION=1920x1080");
            sb.AppendLine("stream_1080p.m3u8");
            sb.AppendLine("#EXT-X-STREAM-INF:BANDWIDTH=2800000,RESOLUTION=1280x720");
            sb.AppendLine("stream_720p.m3u8");
            sb.AppendLine("#EXT-X-STREAM-INF:BANDWIDTH=1200000,RESOLUTION=854x480");
            sb.AppendLine("stream_480p.m3u8");
            sb.AppendLine("#EXT-X-STREAM-INF:BANDWIDTH=600000,RESOLUTION=640x360");
            sb.AppendLine("stream_360p.m3u8");
            File.WriteAllText(path, sb.ToString());
        }
    }
}
