using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EeccStudio.Core.Cms
{
    /// <summary>
    /// Type de contenu multimédia géré par le CMS d'EECC Studio
    /// </summary>
    public enum CmsContentType
    {
        Podcast,
        AudioSummary,
        ShortVideo,
        SermonText,
        Banner,
        PhotoAlbum
    }

    /// <summary>
    /// Classe de base pour tous les éléments de contenu gérés par le CMS
    /// </summary>
    public abstract class CmsBaseItem
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("is_published")]
        public bool IsPublished { get; set; } = true;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Épisode de Podcast complet d'un culte (MP3 / AAC, 30 à 90 minutes)
    /// </summary>
    public class PodcastItem : CmsBaseItem
    {
        [JsonPropertyName("speaker")]
        public string Speaker { get; set; } = "";

        [JsonPropertyName("date")]
        public DateTime Date { get; set; } = DateTime.Today;

        [JsonPropertyName("theme")]
        public string? Theme { get; set; }

        [JsonPropertyName("scripture_refs")]
        public string? ScriptureRefs { get; set; }

        [JsonPropertyName("audio_url")]
        public string AudioUrl { get; set; } = "";

        [JsonPropertyName("storage_path")]
        public string? StoragePath { get; set; }

        [JsonPropertyName("duration_seconds")]
        public int DurationSeconds { get; set; }

        [JsonPropertyName("cover_image_url")]
        public string? CoverImageUrl { get; set; }

        [JsonPropertyName("listen_count")]
        public int ListenCount { get; set; }
    }

    /// <summary>
    /// Résumé Audio court / Capsule spirituelle (1 à 5 minutes)
    /// </summary>
    public class AudioSummaryItem : CmsBaseItem
    {
        [JsonPropertyName("sermon_id")]
        public Guid? SermonId { get; set; }

        [JsonPropertyName("speaker")]
        public string Speaker { get; set; } = "";

        [JsonPropertyName("date")]
        public DateTime Date { get; set; } = DateTime.Today;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("audio_url")]
        public string AudioUrl { get; set; } = "";

        [JsonPropertyName("storage_path")]
        public string? StoragePath { get; set; }

        [JsonPropertyName("duration_seconds")]
        public int DurationSeconds { get; set; }

        [JsonPropertyName("cover_image_url")]
        public string? CoverImageUrl { get; set; }

        [JsonPropertyName("listen_count")]
        public int ListenCount { get; set; }
    }

    /// <summary>
    /// Courte vidéo / Short / Reel de culte (30 secondes à 3 minutes)
    /// </summary>
    public class ShortVideoItem : CmsBaseItem
    {
        [JsonPropertyName("sermon_id")]
        public Guid? SermonId { get; set; }

        [JsonPropertyName("speaker")]
        public string? Speaker { get; set; }

        [JsonPropertyName("date")]
        public DateTime Date { get; set; } = DateTime.Today;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("video_url")]
        public string VideoUrl { get; set; } = "";

        [JsonPropertyName("storage_path")]
        public string? StoragePath { get; set; }

        [JsonPropertyName("thumbnail_url")]
        public string? ThumbnailUrl { get; set; }

        [JsonPropertyName("duration_seconds")]
        public int DurationSeconds { get; set; }

        [JsonPropertyName("aspect_ratio")]
        public string AspectRatio { get; set; } = "9:16";

        [JsonPropertyName("view_count")]
        public int ViewCount { get; set; }
    }

    /// <summary>
    /// Résumé texte complet d'une prédication (Notes, plan, versets et méditation)
    /// </summary>
    public class SermonTextItem : CmsBaseItem
    {
        [JsonPropertyName("speaker")]
        public string Speaker { get; set; } = "";

        [JsonPropertyName("date")]
        public DateTime Date { get; set; } = DateTime.Today;

        [JsonPropertyName("theme")]
        public string? Theme { get; set; }

        [JsonPropertyName("scripture_refs")]
        public string? ScriptureRefs { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = "";

        [JsonPropertyName("full_transcript")]
        public string? FullTranscript { get; set; }

        [JsonPropertyName("key_points")]
        public List<string> KeyPoints { get; set; } = new();

        [JsonPropertyName("reading_time_minutes")]
        public int ReadingTimeMinutes { get; set; } = 5;

        [JsonPropertyName("cover_image_url")]
        public string? CoverImageUrl { get; set; }
    }

    /// <summary>
    /// Affiche publicitaire unique ou flyer d'événement
    /// </summary>
    public class BannerItem : CmsBaseItem
    {
        [JsonPropertyName("subtitle")]
        public string? Subtitle { get; set; }

        [JsonPropertyName("image_url")]
        public string ImageUrl { get; set; } = "";

        [JsonPropertyName("storage_path")]
        public string? StoragePath { get; set; }

        [JsonPropertyName("link_url")]
        public string? LinkUrl { get; set; }

        [JsonPropertyName("start_date")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [JsonPropertyName("end_date")]
        public DateTime? EndDate { get; set; }

        [JsonPropertyName("display_order")]
        public int DisplayOrder { get; set; }

        [JsonPropertyName("click_count")]
        public int ClickCount { get; set; }
    }

    /// <summary>
    /// Album photo d'un culte (peut contenir jusqu'à 200 images)
    /// </summary>
    public class PhotoAlbumItem : CmsBaseItem
    {
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("date")]
        public DateTime Date { get; set; } = DateTime.Today;

        [JsonPropertyName("cover_image_url")]
        public string? CoverImageUrl { get; set; }

        [JsonPropertyName("photo_count")]
        public int PhotoCount { get; set; }

        public List<AlbumPhotoItem> Photos { get; set; } = new();
    }

    /// <summary>
    /// Photo individuelle au sein d'un album de culte
    /// </summary>
    public class AlbumPhotoItem
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [JsonPropertyName("album_id")]
        public Guid AlbumId { get; set; }

        [JsonPropertyName("photo_url")]
        public string PhotoUrl { get; set; } = "";

        [JsonPropertyName("thumbnail_url")]
        public string? ThumbnailUrl { get; set; }

        [JsonPropertyName("storage_path")]
        public string? StoragePath { get; set; }

        [JsonPropertyName("caption")]
        public string? Caption { get; set; }

        [JsonPropertyName("display_order")]
        public int DisplayOrder { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; } = 1920;

        [JsonPropertyName("height")]
        public int Height { get; set; } = 1080;

        [JsonPropertyName("file_size_bytes")]
        public long FileSizeBytes { get; set; }

        [JsonPropertyName("is_published")]
        public bool IsPublished { get; set; } = true;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
