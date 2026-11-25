namespace EeccStudio.Core.Graphics
{
    // Alias vers EeccStudio.Broadcast pour compatibilité
    public static class HardwareEncoderDetector
    {
        public static EeccStudio.Broadcast.GpuEncoderCapabilities Detect(string? ffmpegPath = null)
            => EeccStudio.Broadcast.HardwareEncoderDetector.Detect(ffmpegPath);
    }
}
