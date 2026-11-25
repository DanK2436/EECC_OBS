using System;
using EeccStudio.Core.Graphics;
using Xunit;

namespace EeccStudio.Core.Tests
{
    public sealed class GpuScrollFilterTests
    {
        [Fact]
        public void GpuScrollFilter_TicksContinuouslyAndWraps()
        {
            var filter = new GpuScrollFilter(speedX: 100.0f, boundingWidth: 1000, boundingHeight: 50);
            Assert.Equal(0.0f, filter.CurrentOffsetX);

            // Avance de 1 seconde pour une texture de 1000px de large à 100px/s :
            // Delta offset = 1/1000 * 100 * 1.0 = 0.10f
            filter.Tick(1.0f, sourceWidth: 1000, sourceHeight: 50);
            Assert.InRange(filter.CurrentOffsetX, 0.09f, 0.11f);

            // Avance de 10 secondes supplémentaires : 0.10 + 1.00 = 1.10 -> wrap à 0.10f
            filter.Tick(10.0f, sourceWidth: 1000, sourceHeight: 50);
            Assert.InRange(filter.CurrentOffsetX, 0.09f, 0.11f);
        }

        [Fact]
        public void GpuScrollFilter_ApplyFilter_WrapsPixelsCorrectly()
        {
            var filter = new GpuScrollFilter(speedX: 50.0f, boundingWidth: 200, boundingHeight: 20);

            // Texture source de 200x20
            var source = new VideoFrameBuffer(200, 20);
            source.Clear(0, 0, 0, 255);

            // Pixel rouge à x = 10, y = 5
            int pixelIdx = (5 * 200 + 10) * 4;
            source.Data[pixelIdx] = 0;     // B
            source.Data[pixelIdx + 1] = 0; // G
            source.Data[pixelIdx + 2] = 255; // R
            source.Data[pixelIdx + 3] = 255; // A

            // Décalage de 50 pixels vers la gauche
            // offset = 50 / 200 = 0.25f
            filter.Tick(1.0f, sourceWidth: 200, sourceHeight: 20);

            var scrolled = filter.ApplyFilter(source);
            Assert.NotNull(scrolled);
            Assert.Equal(200, scrolled.Width);
            Assert.Equal(20, scrolled.Height);

            // Le pixel rouge à x = 10 doit s'être déplacé modulo 200 (10 - 50 = -40 -> 160)
            int targetX = (10 - 50 + 200) % 200;
            int scrolledIdx = (5 * 200 + targetX) * 4;
            Assert.Equal(255, scrolled.Data[scrolledIdx + 2]); // R
        }

        [Fact]
        public void GpuScrollFilter_DrawToCanvas_OverlaysCleanly()
        {
            var filter = new GpuScrollFilter(speedX: 60.0f, boundingWidth: 1920, boundingHeight: 40);
            var source = new VideoFrameBuffer(1920, 40);
            source.Clear(37, 99, 235, 200); // Bande bleue EECC semi-transparente

            var canvas = new VideoFrameBuffer(1920, 1080);
            canvas.Clear(0, 0, 0, 255);

            filter.DrawToCanvas(source, canvas, posX: 0, posY: 1040);

            // Vérification dans la zone du bandeau (Y = 1050)
            int idx = (1050 * 1920 + 500) * 4;
            Assert.True(canvas.Data[idx] > 0 || canvas.Data[idx + 1] > 0 || canvas.Data[idx + 2] > 0);
        }
    }
}
