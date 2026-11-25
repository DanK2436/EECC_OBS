using System;
using System.IO;
using System.Threading.Tasks;
using EeccStudio.Broadcast;
using Xunit;

namespace EeccStudio.Core.Tests
{
    public sealed class JellyfinMediaAndChaptersTests
    {
        [Fact]
        public void LiveChapterMarker_MarksAndExportsWebVtt()
        {
            var chapterManager = new LiveChapterMarker();
            chapterManager.StartSession();
            Assert.True(chapterManager.IsActive);

            var c1 = chapterManager.MarkChapter("Louange & Adoration", "louange");
            var c2 = chapterManager.MarkChapter("Prédication : La Foi Victorieuse", "predication");
            var c3 = chapterManager.MarkChapter("Bénédiction finale", "benediction");

            chapterManager.StopSession();
            Assert.False(chapterManager.IsActive);
            Assert.Equal(3, chapterManager.Markers.Count);

            string vtt = chapterManager.ExportToWebVtt();
            Assert.StartsWith("WEBVTT", vtt);
            Assert.Contains("Louange & Adoration", vtt);
            Assert.Contains("Prédication : La Foi Victorieuse", vtt);
            Assert.Contains("-->", vtt);
        }

        [Fact]
        public async Task HlsTranscoderPipeline_GeneratesMultiBitratePlaylist()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "Eecc_HlsTest_" + Guid.NewGuid().ToString("N"));
            string dummyVideo = Path.Combine(tempDir, "source.mp4");
            try
            {
                Directory.CreateDirectory(tempDir);
                File.WriteAllText(dummyVideo, "video payload");

                string hlsOut = Path.Combine(tempDir, "hls");
                var result = await HlsTranscoderPipeline.TranscodeToMultiBitrateHlsAsync(dummyVideo, hlsOut);

                Assert.True(result.Success);
                Assert.True(File.Exists(result.MasterPlaylistPath));

                string masterContent = File.ReadAllText(result.MasterPlaylistPath);
                Assert.Contains("#EXTM3U", masterContent);
                Assert.Contains("1920x1080", masterContent);
                Assert.Contains("1280x720", masterContent);
                Assert.Contains("854x480", masterContent);
                Assert.Contains("640x360", masterContent);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public async Task TrickplayGenerator_GeneratesValidVttAndSprite()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "Eecc_TrickplayTest_" + Guid.NewGuid().ToString("N"));
            try
            {
                var result = await TrickplayGenerator.GenerateTrickplayAsync("dummy.mp4", tempDir, intervalSeconds: 10);

                Assert.True(result.Success);
                Assert.True(File.Exists(result.VttFilePath));
                Assert.True(File.Exists(result.SpritesheetPath));

                string vtt = File.ReadAllText(result.VttFilePath);
                Assert.StartsWith("WEBVTT", vtt);
                Assert.Contains("#xywh=", vtt);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }
    }
}
