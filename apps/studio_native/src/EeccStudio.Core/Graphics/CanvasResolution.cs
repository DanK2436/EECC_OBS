using System;

namespace EeccStudio.Core.Graphics
{
    public enum CanvasPreset
    {
        FullHd1080p,
        Qhd1440p,
        UltraHd4K,
        UltraHd8K,
        UltraHd16K
    }

    public sealed class GpuBudget
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public int TargetFps { get; init; }
        public long RawFrameSizeBytes { get; init; }
        public double RawBandwidthGbps { get; init; }
        public double EstimatedVramMb { get; init; }
        public string RecommendedEncodingBitrate { get; init; } = "";
        public string RecommendedHardware { get; init; } = "";
        public bool IsMultiGpuRecommended { get; init; }
        public string Description { get; init; } = "";
    }

    public static class CanvasResolution
    {
        public static (int width, int height, int defaultFps) GetDimensions(CanvasPreset preset)
        {
            return preset switch
            {
                CanvasPreset.FullHd1080p => (1920, 1080, 60),
                CanvasPreset.Qhd1440p => (2560, 1440, 60),
                CanvasPreset.UltraHd4K => (3840, 2160, 60),
                CanvasPreset.UltraHd8K => (7680, 4320, 30),
                CanvasPreset.UltraHd16K => (15360, 8640, 30),
                _ => (1920, 1080, 60)
            };
        }

        public static GpuBudget CalculateBudget(CanvasPreset preset, int? customFps = null)
        {
            var (w, h, defFps) = GetDimensions(preset);
            int fps = customFps ?? defFps;

            // 4 bytes per pixel (BGRA 32-bit)
            long frameSizeBytes = (long)w * h * 4;

            // Raw uncompressed bandwidth in Gbit/s: (Bytes * 8 * fps) / 1,000,000,000
            double rawGbps = (frameSizeBytes * 8.0 * fps) / 1_000_000_000.0;

            // VRAM budget for triple-buffered canvas + 4 composition layers (buffers of same res)
            // Buffer total = 7 full-res textures
            double vramMb = (frameSizeBytes * 7.0) / (1024.0 * 1024.0);

            string recBitrate;
            string recHardware;
            bool multiGpu = false;
            string desc;

            switch (preset)
            {
                case CanvasPreset.FullHd1080p:
                    recBitrate = "6 - 12 Mbps (H.264/HEVC)";
                    recHardware = "GPU intégré ou dédié standard (Intel QSV, GTX 1650+)";
                    desc = "1080p60 Full HD — Standard de production fluide et optimal";
                    break;
                case CanvasPreset.Qhd1440p:
                    recBitrate = "15 - 25 Mbps (HEVC/AV1)";
                    recHardware = "GPU dédié intermédiaire (RTX 3060, Radeon 6600+)";
                    desc = "1440p60 Quad HD — Très haute précision";
                    break;
                case CanvasPreset.UltraHd4K:
                    recBitrate = "35 - 60 Mbps (HEVC/AV1)";
                    recHardware = "GPU haute performance (RTX 3070 / RTX 4070+)";
                    desc = "4K60 Ultra-HD — Qualité broadcast cinéma";
                    break;
                case CanvasPreset.UltraHd8K:
                    recBitrate = "100 - 180 Mbps (HEVC/AV1 Dédié)";
                    recHardware = "GPU ultra haut de gamme (RTX 4080 / RTX 4090, 16+ Go VRAM)";
                    desc = "8K30 Ultra-HD — Clarté extrême pour grands écrans";
                    break;
                case CanvasPreset.UltraHd16K:
                    recBitrate = "400 - 800 Mbps (Découpage en tuiles / HEVC)";
                    recHardware = "Multi-GPU professionnel (NVIDIA RTX 6000 Ada / A100)";
                    multiGpu = true;
                    desc = "16K30 Master Studio — Résolution maximale cinéma";
                    break;
                default:
                    recBitrate = "8 Mbps";
                    recHardware = "Standard";
                    desc = "Résolution standard";
                    break;
            }

            return new GpuBudget
            {
                Width = w,
                Height = h,
                TargetFps = fps,
                RawFrameSizeBytes = frameSizeBytes,
                RawBandwidthGbps = Math.Round(rawGbps, 2),
                EstimatedVramMb = Math.Round(vramMb, 1),
                RecommendedEncodingBitrate = recBitrate,
                RecommendedHardware = recHardware,
                IsMultiGpuRecommended = multiGpu,
                Description = desc
            };
        }
    }
}
