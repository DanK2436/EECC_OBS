using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EeccStudio.Core.Cms
{
    public enum PhotoUploadStatus
    {
        Queued,
        Uploading,
        Completed,
        Failed,
        Cancelled
    }

    public class PhotoUploadTask
    {
        public string LocalFilePath { get; set; } = "";
        public string FileName => Path.GetFileName(LocalFilePath);
        public long FileSizeBytes { get; set; }
        public PhotoUploadStatus Status { get; set; } = PhotoUploadStatus.Queued;
        public double ProgressPercent { get; set; }
        public string? RemoteUrl { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class BatchUploadProgress
    {
        public int CompletedCount { get; set; }
        public int TotalCount { get; set; }
        public double OverallPercent => TotalCount > 0 ? (CompletedCount / (double)TotalCount) * 100.0 : 0.0;
        public string? CurrentFileName { get; set; }
    }

    /// <summary>
    /// Gestionnaire d'upload par lot haute performance pour les galeries de culte :
    /// - Supporte jusqu'à 200 images simultanées
    /// - Limitation de concurrence par sémaphore (défaut 4 uploads simultanés) pour préserver la bande passante du culte
    /// - Rapports de progression globale et individuelle
    /// - Tolérance aux pannes réseau avec réessais automatiques
    /// </summary>
    public class BatchPhotoUploader
    {
        private readonly EeccTvCmsService _cmsService;
        private readonly int _maxConcurrency;

        public BatchPhotoUploader(EeccTvCmsService cmsService, int maxConcurrency = 4)
        {
            _cmsService = cmsService ?? throw new ArgumentNullException(nameof(cmsService));
            _maxConcurrency = Math.Max(1, Math.Min(8, maxConcurrency));
        }

        public async Task<List<AlbumPhotoItem>> UploadAlbumPhotosAsync(
            Guid albumId,
            IReadOnlyList<string> localImagePaths,
            IProgress<BatchUploadProgress>? progressReporter = null,
            CancellationToken token = default)
        {
            if (localImagePaths == null || localImagePaths.Count == 0)
                return new List<AlbumPhotoItem>();

            // Limite à 200 photos maximum par album comme spécifié
            var pathsToUpload = localImagePaths.Take(200).ToList();
            var totalCount = pathsToUpload.Count;
            int completedCount = 0;

            var tasks = pathsToUpload.Select(path => new PhotoUploadTask
            {
                LocalFilePath = path,
                FileSizeBytes = File.Exists(path) ? new FileInfo(path).Length : 0
            }).ToList();

            using var semaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);
            var uploadedPhotos = new ConcurrentBag<AlbumPhotoItem>();

            var uploadTasks = tasks.Select(async (task, index) =>
            {
                await semaphore.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    token.ThrowIfCancellationRequested();
                    task.Status = PhotoUploadStatus.Uploading;

                    var uniqueFileName = $"{albumId:N}_{index + 1:D3}_{Path.GetFileName(task.LocalFilePath)}";
                    var remotePath = $"albums/{albumId:N}/{uniqueFileName}";

                    // Progression locale par photo
                    var photoProgress = new Progress<double>(p =>
                    {
                        task.ProgressPercent = p;
                    });

                    // Tentative d'upload avec 2 réessais en cas de fluctuation réseau
                    string? publicUrl = null;
                    int attempts = 0;
                    while (attempts < 3 && publicUrl == null)
                    {
                        try
                        {
                            attempts++;
                            publicUrl = await _cmsService.UploadMediaFileAsync(
                                bucket: "photo-galleries",
                                remotePath: remotePath,
                                localFilePath: task.LocalFilePath,
                                progress: photoProgress,
                                token: token
                            ).ConfigureAwait(false);
                        }
                        catch (Exception) when (attempts < 3)
                        {
                            await Task.Delay(500 * attempts, token).ConfigureAwait(false);
                        }
                    }

                    if (publicUrl != null)
                    {
                        task.Status = PhotoUploadStatus.Completed;
                        task.RemoteUrl = publicUrl;

                        var photoItem = new AlbumPhotoItem
                        {
                            AlbumId = albumId,
                            PhotoUrl = publicUrl,
                            ThumbnailUrl = publicUrl, // Dans un pipeline étendu, pointe vers la vignette 400px
                            StoragePath = remotePath,
                            DisplayOrder = index + 1,
                            FileSizeBytes = task.FileSizeBytes,
                            IsPublished = true
                        };

                        await _cmsService.AddPhotoToAlbumAsync(photoItem, token).ConfigureAwait(false);
                        uploadedPhotos.Add(photoItem);
                    }
                    else
                    {
                        task.Status = PhotoUploadStatus.Failed;
                        task.ErrorMessage = "Échec après 3 tentatives d'envoi.";
                    }
                }
                catch (OperationCanceledException)
                {
                    task.Status = PhotoUploadStatus.Cancelled;
                }
                catch (Exception ex)
                {
                    task.Status = PhotoUploadStatus.Failed;
                    task.ErrorMessage = ex.Message;
                }
                finally
                {
                    Interlocked.Increment(ref completedCount);
                    progressReporter?.Report(new BatchUploadProgress
                    {
                        CompletedCount = completedCount,
                        TotalCount = totalCount,
                        CurrentFileName = task.FileName
                    });
                    semaphore.Release();
                }
            });

            await Task.WhenAll(uploadTasks).ConfigureAwait(false);
            return uploadedPhotos.OrderBy(p => p.DisplayOrder).ToList();
        }
    }
}
