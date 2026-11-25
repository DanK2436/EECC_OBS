using System;
using System.Diagnostics;
using System.IO;

namespace EeccStudio.Broadcast
{
    public enum GpuEncoderType
    {
        NvidiaNvenc,
        IntelQuickSync,
        AmdAmf,
        SoftwareCpu
    }

    public sealed class GpuEncoderCapabilities
    {
        public GpuEncoderType PreferredEncoder { get; set; } = GpuEncoderType.SoftwareCpu;
        public string H264EncoderName { get; set; } = "libx264";
        public string HevcEncoderName { get; set; } = "libx265";
        public bool HasHardwareAcceleration => PreferredEncoder != GpuEncoderType.SoftwareCpu;
        public string Description { get; set; } = "CPU Logiciel (libx264)";
    }

    /// <summary>
    /// Détecte automatiquement la présence d'encodeurs vidéo matériels GPU sur la machine de régie
    /// (NVIDIA NVENC, Intel QuickSync, AMD AMF) pour soulager le processeur lors de la diffusion et de l'enregistrement.
    /// </summary>
    public static class HardwareEncoderDetector
    {
        private static GpuEncoderCapabilities? _cachedCapabilities;

        public static GpuEncoderCapabilities Detect(string? ffmpegPath = null)
        {
            if (_cachedCapabilities != null)
                return _cachedCapabilities;

            var result = new GpuEncoderCapabilities();

            try
            {
                // 1. Détection via bibliothèques matérielles Windows
                string gpuInfo = GetGpuDeviceNames();

                if (gpuInfo.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                    gpuInfo.Contains("GeForce", StringComparison.OrdinalIgnoreCase) ||
                    gpuInfo.Contains("Quadro", StringComparison.OrdinalIgnoreCase) ||
                    gpuInfo.Contains("RTX", StringComparison.OrdinalIgnoreCase))
                {
                    result.PreferredEncoder = GpuEncoderType.NvidiaNvenc;
                    result.H264EncoderName = "h264_nvenc";
                    result.HevcEncoderName = "hevc_nvenc";
                    result.Description = "NVIDIA NVENC (Accélération matérielle GPU active)";
                }
                else if (gpuInfo.Contains("Intel", StringComparison.OrdinalIgnoreCase) &&
                         (gpuInfo.Contains("UHD", StringComparison.OrdinalIgnoreCase) ||
                          gpuInfo.Contains("Iris", StringComparison.OrdinalIgnoreCase) ||
                          gpuInfo.Contains("Arc", StringComparison.OrdinalIgnoreCase)))
                {
                    result.PreferredEncoder = GpuEncoderType.IntelQuickSync;
                    result.H264EncoderName = "h264_qsv";
                    result.HevcEncoderName = "hevc_qsv";
                    result.Description = "Intel QuickSync Video (QSV)";
                }
                else if (gpuInfo.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                         gpuInfo.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
                {
                    result.PreferredEncoder = GpuEncoderType.AmdAmf;
                    result.H264EncoderName = "h264_amf";
                    result.HevcEncoderName = "hevc_amf";
                    result.Description = "AMD AMF Video Engine";
                }
            }
            catch
            {
                // Repli CPU libx264 standard
            }

            _cachedCapabilities = result;
            return result;
        }

        private static string GetGpuDeviceNames()
        {
            try
            {
                var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.System);
                if (File.Exists(Path.Combine(systemRoot, "nvencodeapi64.dll")) ||
                    File.Exists(Path.Combine(systemRoot, "nvapi64.dll")))
                {
                    return "NVIDIA GeForce / RTX NVENC";
                }

                if (File.Exists(Path.Combine(systemRoot, "amfrt64.dll")))
                {
                    return "AMD Radeon AMF";
                }
            }
            catch { }

            return "";
        }
    }
}
