using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EeccStudio.Core.Graphics;
using Xunit;

namespace EeccStudio.Core.Tests
{
    public sealed class GpuCompositorTests
    {
        [Theory]
        [InlineData(CanvasPreset.FullHd1080p, 1920, 1080, 60)]
        [InlineData(CanvasPreset.Qhd1440p, 2560, 1440, 60)]
        [InlineData(CanvasPreset.UltraHd4K, 3840, 2160, 60)]
        [InlineData(CanvasPreset.UltraHd8K, 7680, 4320, 30)]
        [InlineData(CanvasPreset.UltraHd16K, 15360, 8640, 30)]
        public void CalculateBudget_ReturnsCorrectDimensionsAndValidBandwidth(
            CanvasPreset preset, int expectedW, int expectedH, int expectedFps)
        {
            var budget = CanvasResolution.CalculateBudget(preset);

            Assert.Equal(expectedW, budget.Width);
            Assert.Equal(expectedH, budget.Height);
            Assert.Equal(expectedFps, budget.TargetFps);
            Assert.True(budget.RawFrameSizeBytes > 0);
            Assert.True(budget.RawBandwidthGbps > 0);
            Assert.True(budget.EstimatedVramMb > 0);
            Assert.False(string.IsNullOrWhiteSpace(budget.RecommendedEncodingBitrate));
            Assert.False(string.IsNullOrWhiteSpace(budget.RecommendedHardware));

            if (preset == CanvasPreset.UltraHd16K)
            {
                Assert.True(budget.IsMultiGpuRecommended);
            }
        }

        [Fact]
        public void CompositeTransition_Fade_BlendsLinearly()
        {
            var compositor = new GpuCompositor(CanvasPreset.FullHd1080p);
            var frameA = new VideoFrameBuffer(1920, 1080);
            var frameB = new VideoFrameBuffer(1920, 1080);

            // Frame A = 100% Blanc (255, 255, 255)
            frameA.Clear(255, 255, 255);
            // Frame B = 100% Noir (0, 0, 0)
            frameB.Clear(0, 0, 0);

            // À progress = 0.5f, les pixels doivent être à mi-chemin (~127/128)
            var mid = compositor.CompositeTransition(frameA, frameB, TransitionType.Fade, 0.5f);

            int sampleIdx = (500 * 1920 + 500) * 4;
            byte bVal = mid.Data[sampleIdx];
            byte gVal = mid.Data[sampleIdx + 1];
            byte rVal = mid.Data[sampleIdx + 2];

            Assert.InRange(bVal, 120, 135);
            Assert.InRange(gVal, 120, 135);
            Assert.InRange(rVal, 120, 135);
        }

        [Fact]
        public void CompositeTransition_Cut_SwitchesImmediately()
        {
            var compositor = new GpuCompositor(CanvasPreset.FullHd1080p);
            var frameA = new VideoFrameBuffer(1920, 1080);
            var frameB = new VideoFrameBuffer(1920, 1080);

            frameA.Clear(255, 0, 0); // Rouge
            frameB.Clear(0, 255, 0); // Vert

            var cutAtZero = compositor.CompositeTransition(frameA, frameB, TransitionType.Cut, 0.0f);
            var cutAtMid = compositor.CompositeTransition(frameA, frameB, TransitionType.Cut, 0.5f);

            int idx = (500 * 1920 + 500) * 4;
            // À cut à zéro, on doit avoir la source A
            Assert.Equal(255, cutAtZero.Data[idx + 2]); // R
        }

        [Fact]
        public void CompositeScene_WithLowerThird_AppliesOverlay()
        {
            var compositor = new GpuCompositor(CanvasPreset.FullHd1080p);
            compositor.LowerThird.IsVisible = true;
            compositor.LowerThird.Title = "Pasteur Principal";
            compositor.LowerThird.Subtitle = "Culte Dominical";

            var baseFrame = new VideoFrameBuffer(1920, 1080);
            baseFrame.Clear(200, 200, 200);

            var result = compositor.CompositeScene(baseFrame);

            // Dans la zone du bandeau (Y = 85% de 1080 = 918, X = 10% de 1920 = 192)
            int lowerThirdY = (int)(1080 * 0.85f);
            int lowerThirdX = (int)(1920 * 0.10f);
            int idx = (lowerThirdY * 1920 + lowerThirdX) * 4;

            // La couleur doit avoir été assombrie par le bandeau slate #0F172A
            Assert.True(result.Data[idx] < 100);     // B
            Assert.True(result.Data[idx + 1] < 100); // G
            Assert.True(result.Data[idx + 2] < 100); // R
        }

        [Fact]
        public void PhysicalCameraCapture_EnumerateDevices_ReturnsAtLeastOneCaptureDevice()
        {
            var devices = PhysicalCameraCapture.EnumerateDevices();
            Assert.NotNull(devices);
            Assert.NotEmpty(devices);
            Assert.Contains(devices, d => d.IsCaptureCard || !string.IsNullOrWhiteSpace(d.Name));
        }

        [Fact]
        public void ScreenCaptureEngine_EnumerateDisplays_ReturnsPrimaryDisplay()
        {
            var displays = ScreenCaptureEngine.EnumerateDisplays();
            Assert.NotNull(displays);
            Assert.NotEmpty(displays);
            Assert.Contains(displays, d => d.IsPrimary);
        }

        [Fact]
        public async Task MediaVideoSource_PlayAndPause_OperatesCorrectly()
        {
            using var media = new MediaVideoSource();
            media.Load("test_intro.mp4", 1280, 720);
            media.TargetFps = 100; // Rapide pour le test

            int frameCount = 0;
            media.FrameArrived += _ => Interlocked.Increment(ref frameCount);

            media.Play();
            await Task.Delay(50);
            Assert.Equal(PlaybackState.Playing, media.State);
            Assert.True(frameCount > 0);

            media.Pause();
            Assert.Equal(PlaybackState.Paused, media.State);

            media.Stop();
            Assert.Equal(PlaybackState.Stopped, media.State);
        }
    }
}
