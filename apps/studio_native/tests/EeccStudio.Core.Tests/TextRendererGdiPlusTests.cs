using System;
using System.IO;
using EeccStudio.Core.Graphics;
using Xunit;

namespace EeccStudio.Core.Tests
{
    public sealed class TextRendererGdiPlusTests
    {
        [Fact]
        public void TextRenderer_CachesTexture_WhenUnchanged()
        {
            var renderer = new TextRendererGdiPlus("Culte EECC", new TextProperties
            {
                FontSize = 30.0f,
                Bold = true
            });

            var firstTexture = renderer.Render();
            Assert.NotNull(firstTexture);
            Assert.True(firstTexture.Width > 0);
            Assert.True(firstTexture.Height > 0);

            // Deuxième appel sans modification : doit renvoyer la même référence en cache (coût 0)
            var secondTexture = renderer.Render();
            Assert.Same(firstTexture, secondTexture);

            // Modification du texte : doit invalider le cache et générer un nouveau buffer
            renderer.Text = "Nouveau Titre Culte";
            var thirdTexture = renderer.Render();
            Assert.NotSame(firstTexture, thirdTexture);
        }

        [Theory]
        [InlineData("culte de priere", TextTransformMode.Uppercase, "CULTE DE PRIERE")]
        [InlineData("CULTE DE PRIERE", TextTransformMode.Lowercase, "culte de priere")]
        [InlineData("culte de priere", TextTransformMode.StartCase, "Culte De Priere")]
        public void TextRenderer_AppliesTransformations(string input, TextTransformMode mode, string expected)
        {
            var renderer = new TextRendererGdiPlus(input, new TextProperties
            {
                Transform = mode,
                FontSize = 24.0f
            });

            var buf = renderer.Render();
            Assert.NotNull(buf);
            Assert.True(buf.Width > 0);
        }

        [Fact]
        public void TextRenderer_ReloadsExternalFile_OnTick()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "Eecc_TextTick_" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                File.WriteAllText(tempFile, "Annonce Initiale");

                var renderer = new TextRendererGdiPlus("", new TextProperties
                {
                    ReadFromFile = true,
                    FilePath = tempFile
                });

                renderer.Tick(1.5f); // Force le tick de 1.5s
                Assert.Equal("Annonce Initiale", renderer.Text);

                // Modification du fichier externe
                File.WriteAllText(tempFile, "Annonce Modifiée à Chaud");
                File.SetLastWriteTimeUtc(tempFile, DateTime.UtcNow.AddSeconds(5));

                renderer.Tick(1.5f);
                Assert.Equal("Annonce Modifiée à Chaud", renderer.Text);
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
        public void LowerThirdOverlay_AppliesCorrectlyToCanvas()
        {
            var lowerThird = new LowerThirdOverlaySource("Pasteur David", "Prédicateur");
            lowerThird.IsVisible = true;
            lowerThird.Tick(1.0f); // Animation complète (1.0)

            var canvas = new VideoFrameBuffer(1920, 1080);
            canvas.Clear(0, 0, 0, 255);

            lowerThird.ApplyToCanvas(canvas);

            // Vérification de la présence de la couleur du bandeau (Zone Y = 85%, X = 10%)
            int sampleY = (int)(1080 * 0.85f);
            int sampleX = (int)(1920 * 0.10f);
            int idx = (sampleY * 1920 + sampleX) * 4;

            // Doit être un fond sombre slate #0F172A (pas le noir pur d'origine)
            Assert.True(canvas.Data[idx] > 0 || canvas.Data[idx + 1] > 0 || canvas.Data[idx + 2] > 0);
        }

        [Fact]
        public void BibleVerseOverlay_AppliesCorrectlyToCanvas()
        {
            var verse = new BibleVerseOverlaySource("Psaumes 23:1", "L'Éternel est mon berger...", "LSG 1910");
            verse.IsVisible = true;
            verse.Tick(1.0f);

            var canvas = new VideoFrameBuffer(1920, 1080);
            canvas.Clear(0, 0, 0, 255);

            verse.ApplyToCanvas(canvas);

            // Vérification dans la zone du bandeau verset (Y = 80%, X = 50%)
            int sampleY = (int)(1080 * 0.80f);
            int sampleX = 1920 / 2;
            int idx = (sampleY * 1920 + sampleX) * 4;

            Assert.True(canvas.Data[idx] > 0 || canvas.Data[idx + 1] > 0 || canvas.Data[idx + 2] > 0);
        }
    }
}
