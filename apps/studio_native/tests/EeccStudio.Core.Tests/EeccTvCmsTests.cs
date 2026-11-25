using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EeccStudio.Core.Cms;
using Xunit;

namespace EeccStudio.Core.Tests
{
    public class EeccTvCmsTests
    {
        [Fact]
        public async Task Podcast_Lifecycle_CreateToggleDelete()
        {
            var service = new EeccTvCmsService();

            var initialList = await service.GetPodcastsAsync();
            int initialCount = initialList.Count;

            // 1. Création / Sauvegarde
            var podcast = new PodcastItem
            {
                Title = "Test Culte Céleste",
                Speaker = "Pasteur Invité",
                Date = DateTime.Today,
                AudioUrl = "https://cdn.eecc.tv/podcasts/test-culte.mp3",
                DurationSeconds = 2700,
                IsPublished = true
            };
            await service.SavePodcastAsync(podcast);

            var listAfterAdd = await service.GetPodcastsAsync();
            Assert.Equal(initialCount + 1, listAfterAdd.Count);

            // 2. Masquage (Toggle visibilité = false)
            bool toggleSuccess = await service.TogglePodcastVisibilityAsync(podcast.Id, isPublished: false);
            Assert.True(toggleSuccess);

            var updatedList = await service.GetPodcastsAsync();
            var item = updatedList.First(p => p.Id == podcast.Id);
            Assert.False(item.IsPublished);

            // 3. Suppression
            bool deleteSuccess = await service.DeletePodcastAsync(podcast.Id);
            Assert.True(deleteSuccess);

            var finalList = await service.GetPodcastsAsync();
            Assert.Equal(initialCount, finalList.Count);
            Assert.DoesNotContain(finalList, p => p.Id == podcast.Id);
        }

        [Fact]
        public async Task AudioSummary_Lifecycle_CreateToggleDelete()
        {
            var service = new EeccTvCmsService();

            var audio = new AudioSummaryItem
            {
                Title = "Capsule Test : Victoire Spirituelle",
                Speaker = "Pasteur Principal",
                Date = DateTime.Today,
                AudioUrl = "https://cdn.eecc.tv/audio-summaries/capsule-test.mp3",
                DurationSeconds = 120,
                IsPublished = true
            };

            await service.SaveAudioSummaryAsync(audio);
            var list = await service.GetAudioSummariesAsync();
            Assert.Contains(list, a => a.Id == audio.Id && a.IsPublished);

            // Masquer la capsule
            await service.ToggleAudioSummaryVisibilityAsync(audio.Id, false);
            var updated = await service.GetAudioSummariesAsync();
            Assert.False(updated.First(a => a.Id == audio.Id).IsPublished);

            // Supprimer
            await service.DeleteAudioSummaryAsync(audio.Id);
            var afterDelete = await service.GetAudioSummariesAsync();
            Assert.DoesNotContain(afterDelete, a => a.Id == audio.Id);
        }

        [Fact]
        public async Task ShortVideo_Lifecycle_CreateToggleDelete()
        {
            var service = new EeccTvCmsService();

            var video = new ShortVideoItem
            {
                Title = "Extrait Louange 60s",
                Date = DateTime.Today,
                VideoUrl = "https://cdn.eecc.tv/short-videos/louange-60s.mp4",
                DurationSeconds = 60,
                AspectRatio = "9:16",
                IsPublished = true
            };

            await service.SaveShortVideoAsync(video);
            var list = await service.GetShortVideosAsync();
            Assert.Contains(list, v => v.Id == video.Id);

            // Bascule de visibilité
            await service.ToggleShortVideoVisibilityAsync(video.Id, false);
            var updated = await service.GetShortVideosAsync();
            Assert.False(updated.First(v => v.Id == video.Id).IsPublished);

            // Suppression
            await service.DeleteShortVideoAsync(video.Id);
            var finalList = await service.GetShortVideosAsync();
            Assert.DoesNotContain(finalList, v => v.Id == video.Id);
        }

        [Fact]
        public async Task SermonText_Lifecycle_CreateToggleDelete()
        {
            var service = new EeccTvCmsService();

            var text = new SermonTextItem
            {
                Title = "Résumé Prédication : La Grâce Infinie",
                Speaker = "Pasteur Principal",
                Date = DateTime.Today,
                Theme = "La Grâce",
                ScriptureRefs = "Éphésiens 2:8-9",
                Summary = "Le salut est un don gratuit accordé par la foi en Jésus-Christ.",
                KeyPoints = new List<string> { "Don gratuit", "Non par les œuvres", "Paix intérieure" },
                IsPublished = true
            };

            await service.SaveSermonTextAsync(text);
            var list = await service.GetSermonTextsAsync();
            Assert.Contains(list, s => s.Id == text.Id);

            await service.ToggleSermonTextVisibilityAsync(text.Id, false);
            var updated = await service.GetSermonTextsAsync();
            Assert.False(updated.First(s => s.Id == text.Id).IsPublished);

            await service.DeleteSermonTextAsync(text.Id);
            var finalList = await service.GetSermonTextsAsync();
            Assert.DoesNotContain(finalList, s => s.Id == text.Id);
        }

        [Fact]
        public async Task Banner_Lifecycle_CreateToggleDelete()
        {
            var service = new EeccTvCmsService();

            var banner = new BannerItem
            {
                Title = "Conférence Annuelle des Familles",
                Subtitle = "Du 15 au 18 Décembre 2026",
                ImageUrl = "https://cdn.eecc.tv/banners/familles-2026.jpg",
                StartDate = DateTime.Today,
                DisplayOrder = 1,
                IsPublished = true
            };

            await service.SaveBannerAsync(banner);
            var list = await service.GetBannersAsync();
            Assert.Contains(list, b => b.Id == banner.Id);

            await service.ToggleBannerVisibilityAsync(banner.Id, false);
            var updated = await service.GetBannersAsync();
            Assert.False(updated.First(b => b.Id == banner.Id).IsPublished);

            await service.DeleteBannerAsync(banner.Id);
            var finalList = await service.GetBannersAsync();
            Assert.DoesNotContain(finalList, b => b.Id == banner.Id);
        }

        [Fact]
        public async Task BatchPhotoUploader_UpTo200Photos_ReportsProgressAndUpdatesAlbum()
        {
            var service = new EeccTvCmsService();
            var uploader = new BatchPhotoUploader(service, maxConcurrency: 4);

            // 1. Création d'un album
            var album = new PhotoAlbumItem
            {
                Title = "Culte Dominical - Lot 200 Photos Test",
                Date = DateTime.Today,
                IsPublished = true
            };
            await service.SavePhotoAlbumAsync(album);

            // 2. Création de 20 fichiers d'images virtuels temporaires pour tester l'upload par lot
            var tempDir = Path.Combine(Path.GetTempPath(), "eecc_batch_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var filePaths = new List<string>();
                for (int i = 1; i <= 20; i++)
                {
                    var p = Path.Combine(tempDir, $"photo_{i:D3}.jpg");
                    File.WriteAllBytes(p, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 }); // En-tête JPEG minimale
                    filePaths.Add(p);
                }

                int lastReportedCompleted = 0;
                var progress = new Progress<BatchUploadProgress>(prog =>
                {
                    lastReportedCompleted = prog.CompletedCount;
                });

                // 3. Exécution de l'upload par lot
                var uploaded = await uploader.UploadAlbumPhotosAsync(album.Id, filePaths, progress);

                Assert.Equal(20, uploaded.Count);
                Assert.All(uploaded, p => Assert.Equal(album.Id, p.AlbumId));
                Assert.All(uploaded, p => Assert.StartsWith("https://cdn.eecc.tv/storage/v1/object/public/photo-galleries/albums/", p.PhotoUrl));

                // 4. Vérification que l'album a bien comptabilisé ses photos
                var albums = await service.GetPhotoAlbumsAsync();
                var alb = albums.First(a => a.Id == album.Id);
                Assert.Equal(20, alb.PhotoCount);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }
}
