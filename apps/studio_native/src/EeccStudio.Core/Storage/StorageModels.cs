using System;

namespace EeccStudio.Core.Storage
{
    public enum StorageTargetType
    {
        SupabaseStorage,
        CloudflareR2,
        LocalNasArchive
    }

    public enum UploadPriority
    {
        High = 1,   // Podcast audio MP3 (rapide, priorité aux fidèles)
        Medium = 2, // Document résumé PDF / Notes pastorales
        Low = 3     // Gros master vidéo 1080p/4K (téléversement nocturne)
    }

    public enum UploadStatus
    {
        Pending,
        Uploading,
        Completed,
        Failed,
        Paused
    }

    public sealed class OfflineUploadItem
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string LocalFilePath { get; set; } = "";
        public StorageTargetType TargetType { get; set; } = StorageTargetType.SupabaseStorage;
        public string TargetBucket { get; set; } = "podcasts";
        public string RemoteKey { get; set; } = "";
        public UploadPriority Priority { get; set; } = UploadPriority.High;
        public long FileSizeBytes { get; set; }
        public long BytesUploaded { get; set; }
        public UploadStatus Status { get; set; } = UploadStatus.Pending;
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public double ProgressPercent => FileSizeBytes > 0 ? Math.Round((double)BytesUploaded / FileSizeBytes * 100.0, 1) : 0;
    }
}
