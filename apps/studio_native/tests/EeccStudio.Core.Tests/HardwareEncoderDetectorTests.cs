using System;
using EeccStudio.Broadcast;
using Xunit;

namespace EeccStudio.Core.Tests
{
    public sealed class HardwareEncoderDetectorTests
    {
        [Fact]
        public void Detect_ReturnsValidCapabilities()
        {
            var caps = HardwareEncoderDetector.Detect();

            Assert.NotNull(caps);
            Assert.False(string.IsNullOrWhiteSpace(caps.H264EncoderName));
            Assert.False(string.IsNullOrWhiteSpace(caps.HevcEncoderName));
            Assert.False(string.IsNullOrWhiteSpace(caps.Description));

            // Si hardware détecté, l'encodeur ne doit pas être null
            if (caps.HasHardwareAcceleration)
            {
                Assert.NotEqual(GpuEncoderType.SoftwareCpu, caps.PreferredEncoder);
            }
            else
            {
                Assert.Equal("libx264", caps.H264EncoderName);
                Assert.Equal("libx265", caps.HevcEncoderName);
            }
        }
    }
}
