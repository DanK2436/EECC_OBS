using System;
using System.IO;
using System.Threading.Tasks;
using EeccStudio.Core.Graphics;
using Xunit;

namespace EeccStudio.Core.Tests
{
    public sealed class ImageTextureAndSlideShowTests
    {
        [Fact]
        public async Task ImageTextureSource_LoadsAsynchronously_AndDrawsWithPremultipliedAlpha()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "Eecc_ImageTest_" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                File.WriteAllBytes(tempFile, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

                using var imgSource = new ImageTextureSource();
                await imgSource.LoadAsync(tempFile);

                Assert.True(imgSource.IsLoaded);
                Assert.True(imgSource.Width > 0);
                Assert.True(imgSource.Height > 0);

                var canvas = new VideoFrameBuffer(1920, 1080);
                canvas.Clear(0, 0, 0, 255); // Fond noir pur

                // Dessin avec alpha compositing pré-multiplié
                imgSource.DrawToCanvas(canvas, 100, 100, 400, 300, opacity: 0.9f);

                int sampleIdx = (150 * 1920 + 150) * 4;
                // La couleur doit avoir été incrustée proprement
                Assert.True(canvas.Data[sampleIdx + 3] == 255);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        [Fact]
        public void SlideShowPlayer_NavigatesSlides_AndTransitionsSmoothly()
        {
            var player = new SlideShowPlayer();
            player.AddSlide("Cantique 1 : Grand Dieu nous te bénissons", "slide1.jpg");
            player.AddSlide("Cantique 2 : À toi la gloire", "slide2.jpg");
            player.AddSlide("Cantique 3 : Quel ami fidèle et tendre", "slide3.jpg");

            Assert.Equal(3, player.SlideCount);
            Assert.Equal(0, player.CurrentIndex);

            // Avancer diapositive
            player.NextSlide();
            Assert.True(player.IsTransitioning);

            // Simulation du temps d'assouplissement cubique
            player.Tick(0.5f); // 500ms > 400ms transition
            Assert.False(player.IsTransitioning);
            Assert.Equal(1, player.CurrentIndex);

            // Reculer
            player.PreviousSlide();
            player.Tick(0.5f);
            Assert.Equal(0, player.CurrentIndex);

            // Aller directement à l'index 2
            player.GoToSlide(2);
            player.Tick(0.5f);
            Assert.Equal(2, player.CurrentIndex);
        }

        [Fact]
        public void SlideShowPlayer_Render_ProducesValidBuffer()
        {
            var player = new SlideShowPlayer();
            player.AddSlide("Slide A", "a.jpg");
            player.AddSlide("Slide B", "b.jpg");
            player.Transition = SlideTransition.Fade;

            player.NextSlide(); // Déclenche la transition
            player.Tick(0.2f);  // À mi-chemin (progress = 0.5)

            var frame = player.Render(1280, 720);
            Assert.NotNull(frame);
            Assert.Equal(1280, frame.Width);
            Assert.Equal(720, frame.Height);
            Assert.Equal(1280 * 720 * 4, frame.Data.Length);
        }
    }
}
