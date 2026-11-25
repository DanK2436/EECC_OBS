using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EeccStudio.Core.Storage;
using Xunit;

namespace EeccStudio.Core.Tests
{
    public sealed class OfflineSyncQueueTests
    {
        [Fact]
        public async Task OfflineSyncQueue_PrioritizesHighOverLow()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "Eecc_SyncQueueTest_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempDir);
                string podcastPath = Path.Combine(tempDir, "podcast_test.mp3");
                string masterPath = Path.Combine(tempDir, "master_test.mkv");

                File.WriteAllText(podcastPath, "MP3 audio dummy content");
                File.WriteAllText(masterPath, "MKV video dummy content");

                using var queue = new OfflineSyncQueue(tempDir);
                queue.MaxBandwidthKbps = 0; // Pas de délai artificiel pour le test

                // Enfiler d'abord le master volumineux en priorité basse
                var itemMaster = queue.Enqueue(
                    masterPath,
                    StorageTargetType.CloudflareR2,
                    "eecc-masters",
                    "masters/2026/master.mkv",
                    UploadPriority.Low);

                // Enfiler ensuite le podcast audio en priorité haute
                var itemPodcast = queue.Enqueue(
                    podcastPath,
                    StorageTargetType.SupabaseStorage,
                    "podcasts",
                    "podcasts/2026/predication.mp3",
                    UploadPriority.High);

                var items = queue.GetItems();
                Assert.Equal(2, items.Count);

                int completedCount = 0;
                string? firstCompletedKey = null;

                queue.ItemCompleted += item =>
                {
                    Interlocked.Increment(ref completedCount);
                    firstCompletedKey ??= item.RemoteKey;
                };

                queue.Start();

                // Attente du traitement
                int attempts = 0;
                while (completedCount < 2 && attempts++ < 20)
                {
                    await Task.Delay(50);
                }

                queue.Stop();

                Assert.True(completedCount >= 1);
                // Le podcast (High priority) a dû être traité en premier avant le master (Low priority)
                Assert.Contains("podcasts", firstCompletedKey);
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
        public void CloudflareR2Client_BuildsCorrectPublicUrls()
        {
            var config = new R2StorageConfig
            {
                AccountId = "acc123",
                CustomDomain = "https://cdn.eecc-media.org",
                BucketName = "masters"
            };

            var client = new CloudflareR2Client(config);
            var url = client.GetPublicMediaUrl("2026/culte_dimanche.mkv");

            Assert.Equal("https://cdn.eecc-media.org/2026/culte_dimanche.mkv", url);
        }

        [Fact]
        public async Task SupabaseStorageUploader_SimulatesUploadGracefully()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "Eecc_UploaderTest_" + Guid.NewGuid().ToString("N") + ".mp3");
            try
            {
                File.WriteAllText(tempFile, "Audio bits");
                var config = new SupabaseStorageConfig
                {
                    SupabaseUrl = "https://your-project.supabase.co",
                    ServiceRoleOrAnonKey = ""
                };

                var uploader = new SupabaseStorageUploader(config);
                var publicUrl = await uploader.UploadFileAsync("podcasts", "test.mp3", tempFile);

                Assert.NotNull(publicUrl);
                Assert.Contains("test.mp3", publicUrl);
                Assert.Contains("podcasts", publicUrl);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }
    }
}
