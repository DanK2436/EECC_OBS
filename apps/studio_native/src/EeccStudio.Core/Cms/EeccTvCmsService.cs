using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EeccStudio.Core.Cms
{
    public class CmsServiceConfig
    {
        public string SupabaseUrl { get; set; } = "https://your-project.supabase.co";
        public string ApiKey { get; set; } = "";
    }

    /// <summary>
    /// Service d'intégration CMS pour EECC Studio :
    /// - Opérations CRUD complètes (Ajouter, Modifier, Effacer, Masquer)
    /// - Support des Podcasts, Résumés Audios, Petites Vidéos, Résumés Textes, Affiches et Galeries Photos
    /// - Mode résilient : cache mémoire et simulation offline si la régie n'a pas encore de clé renseignée
    /// </summary>
    public class EeccTvCmsService
    {
        private readonly CmsServiceConfig _config;
        private readonly HttpClient _http = new();
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        // Cache local pour fonctionnement hors-ligne / réactivité immédiate dans l'UI Studio
        private readonly ConcurrentDictionary<Guid, PodcastItem> _localPodcasts = new();
        private readonly ConcurrentDictionary<Guid, AudioSummaryItem> _localAudioSummaries = new();
        private readonly ConcurrentDictionary<Guid, ShortVideoItem> _localShortVideos = new();
        private readonly ConcurrentDictionary<Guid, SermonTextItem> _localSermonTexts = new();
        private readonly ConcurrentDictionary<Guid, BannerItem> _localBanners = new();
        private readonly ConcurrentDictionary<Guid, PhotoAlbumItem> _localAlbums = new();

        public EeccTvCmsService(CmsServiceConfig? config = null)
        {
            _config = config ?? new CmsServiceConfig();
            SeedSampleData();
        }

        private bool IsMockMode => string.IsNullOrWhiteSpace(_config.ApiKey) || _config.SupabaseUrl.Contains("your-project");

        #region Podcasts
        public async Task<List<PodcastItem>> GetPodcastsAsync(CancellationToken token = default)
        {
            if (IsMockMode) return _localPodcasts.Values.OrderByDescending(p => p.Date).ToList();

            try
            {
                var url = $"{_config.SupabaseUrl.TrimEnd('/')}/rest/v1/sermons?select=id,title,speaker,date,theme,scripture_refs,podcast_url,cover_image_url,is_published,created_at&podcast_url=not.is.null&order=date.desc";
                return await GetRestListAsync<PodcastItem>(url, token).ConfigureAwait(false);
            }
            catch { return _localPodcasts.Values.OrderByDescending(p => p.Date).ToList(); }
        }

        public async Task<PodcastItem> SavePodcastAsync(PodcastItem item, CancellationToken token = default)
        {
            item.UpdatedAt = DateTime.UtcNow;
            _localPodcasts[item.Id] = item;

            if (!IsMockMode)
            {
                var payload = new
                {
                    id = item.Id,
                    title = item.Title,
                    speaker = item.Speaker,
                    date = item.Date.ToString("yyyy-MM-dd"),
                    theme = item.Theme,
                    scripture_refs = item.ScriptureRefs,
                    podcast_url = item.AudioUrl,
                    cover_image_url = item.CoverImageUrl,
                    is_published = item.IsPublished,
                    updated_at = DateTime.UtcNow
                };
                await UpsertRestAsync("sermons", payload, token).ConfigureAwait(false);
            }
            return item;
        }

        public async Task<bool> DeletePodcastAsync(Guid id, CancellationToken token = default)
        {
            _localPodcasts.TryRemove(id, out var removed);
            if (!IsMockMode)
            {
                if (!string.IsNullOrWhiteSpace(removed?.StoragePath))
                    await DeleteStorageFileAsync("podcasts", removed.StoragePath, token).ConfigureAwait(false);
                await DeleteRestAsync("sermons", id, token).ConfigureAwait(false);
            }
            return true;
        }

        public async Task<bool> TogglePodcastVisibilityAsync(Guid id, bool isPublished, CancellationToken token = default)
        {
            if (_localPodcasts.TryGetValue(id, out var item))
            {
                item.IsPublished = isPublished;
                item.UpdatedAt = DateTime.UtcNow;
                if (!IsMockMode)
                    await PatchRestAsync("sermons", id, new { is_published = isPublished, updated_at = DateTime.UtcNow }, token).ConfigureAwait(false);
                return true;
            }
            return false;
        }
        #endregion

        #region Audio Summaries
        public async Task<List<AudioSummaryItem>> GetAudioSummariesAsync(CancellationToken token = default)
        {
            if (IsMockMode) return _localAudioSummaries.Values.OrderByDescending(a => a.Date).ToList();

            try
            {
                var url = $"{_config.SupabaseUrl.TrimEnd('/')}/rest/v1/audio_summaries?select=*&order=date.desc";
                return await GetRestListAsync<AudioSummaryItem>(url, token).ConfigureAwait(false);
            }
            catch { return _localAudioSummaries.Values.OrderByDescending(a => a.Date).ToList(); }
        }

        public async Task<AudioSummaryItem> SaveAudioSummaryAsync(AudioSummaryItem item, CancellationToken token = default)
        {
            item.UpdatedAt = DateTime.UtcNow;
            _localAudioSummaries[item.Id] = item;

            if (!IsMockMode)
            {
                await UpsertRestAsync("audio_summaries", item, token).ConfigureAwait(false);
            }
            return item;
        }

        public async Task<bool> DeleteAudioSummaryAsync(Guid id, CancellationToken token = default)
        {
            _localAudioSummaries.TryRemove(id, out var removed);
            if (!IsMockMode)
            {
                if (!string.IsNullOrWhiteSpace(removed?.StoragePath))
                    await DeleteStorageFileAsync("audio-summaries", removed.StoragePath, token).ConfigureAwait(false);
                await DeleteRestAsync("audio_summaries", id, token).ConfigureAwait(false);
            }
            return true;
        }

        public async Task<bool> ToggleAudioSummaryVisibilityAsync(Guid id, bool isPublished, CancellationToken token = default)
        {
            if (_localAudioSummaries.TryGetValue(id, out var item))
            {
                item.IsPublished = isPublished;
                item.UpdatedAt = DateTime.UtcNow;
                if (!IsMockMode)
                    await PatchRestAsync("audio_summaries", id, new { is_published = isPublished, updated_at = DateTime.UtcNow }, token).ConfigureAwait(false);
                return true;
            }
            return false;
        }
        #endregion

        #region Short Videos
        public async Task<List<ShortVideoItem>> GetShortVideosAsync(CancellationToken token = default)
        {
            if (IsMockMode) return _localShortVideos.Values.OrderByDescending(v => v.Date).ToList();

            try
            {
                var url = $"{_config.SupabaseUrl.TrimEnd('/')}/rest/v1/short_videos?select=*&order=date.desc";
                return await GetRestListAsync<ShortVideoItem>(url, token).ConfigureAwait(false);
            }
            catch { return _localShortVideos.Values.OrderByDescending(v => v.Date).ToList(); }
        }

        public async Task<ShortVideoItem> SaveShortVideoAsync(ShortVideoItem item, CancellationToken token = default)
        {
            item.UpdatedAt = DateTime.UtcNow;
            _localShortVideos[item.Id] = item;

            if (!IsMockMode)
            {
                await UpsertRestAsync("short_videos", item, token).ConfigureAwait(false);
            }
            return item;
        }

        public async Task<bool> DeleteShortVideoAsync(Guid id, CancellationToken token = default)
        {
            _localShortVideos.TryRemove(id, out var removed);
            if (!IsMockMode)
            {
                if (!string.IsNullOrWhiteSpace(removed?.StoragePath))
                    await DeleteStorageFileAsync("short-videos", removed.StoragePath, token).ConfigureAwait(false);
                await DeleteRestAsync("short_videos", id, token).ConfigureAwait(false);
            }
            return true;
        }

        public async Task<bool> ToggleShortVideoVisibilityAsync(Guid id, bool isPublished, CancellationToken token = default)
        {
            if (_localShortVideos.TryGetValue(id, out var item))
            {
                item.IsPublished = isPublished;
                item.UpdatedAt = DateTime.UtcNow;
                if (!IsMockMode)
                    await PatchRestAsync("short_videos", id, new { is_published = isPublished, updated_at = DateTime.UtcNow }, token).ConfigureAwait(false);
                return true;
            }
            return false;
        }
        #endregion

        #region Sermon Texts
        public async Task<List<SermonTextItem>> GetSermonTextsAsync(CancellationToken token = default)
        {
            if (IsMockMode) return _localSermonTexts.Values.OrderByDescending(s => s.Date).ToList();

            try
            {
                var url = $"{_config.SupabaseUrl.TrimEnd('/')}/rest/v1/sermons?select=id,title,speaker,date,theme,scripture_refs,summary,full_transcript,key_points,reading_time_minutes,is_published,created_at&order=date.desc";
                return await GetRestListAsync<SermonTextItem>(url, token).ConfigureAwait(false);
            }
            catch { return _localSermonTexts.Values.OrderByDescending(s => s.Date).ToList(); }
        }

        public async Task<SermonTextItem> SaveSermonTextAsync(SermonTextItem item, CancellationToken token = default)
        {
            item.UpdatedAt = DateTime.UtcNow;
            _localSermonTexts[item.Id] = item;

            if (!IsMockMode)
            {
                await UpsertRestAsync("sermons", item, token).ConfigureAwait(false);
            }
            return item;
        }

        public async Task<bool> DeleteSermonTextAsync(Guid id, CancellationToken token = default)
        {
            _localSermonTexts.TryRemove(id, out _);
            if (!IsMockMode)
            {
                await DeleteRestAsync("sermons", id, token).ConfigureAwait(false);
            }
            return true;
        }

        public async Task<bool> ToggleSermonTextVisibilityAsync(Guid id, bool isPublished, CancellationToken token = default)
        {
            if (_localSermonTexts.TryGetValue(id, out var item))
            {
                item.IsPublished = isPublished;
                item.UpdatedAt = DateTime.UtcNow;
                if (!IsMockMode)
                    await PatchRestAsync("sermons", id, new { is_published = isPublished, updated_at = DateTime.UtcNow }, token).ConfigureAwait(false);
                return true;
            }
            return false;
        }
        #endregion

        #region Banners
        public async Task<List<BannerItem>> GetBannersAsync(CancellationToken token = default)
        {
            if (IsMockMode) return _localBanners.Values.OrderBy(b => b.DisplayOrder).ToList();

            try
            {
                var url = $"{_config.SupabaseUrl.TrimEnd('/')}/rest/v1/event_banners?select=*&order=display_order.asc";
                return await GetRestListAsync<BannerItem>(url, token).ConfigureAwait(false);
            }
            catch { return _localBanners.Values.OrderBy(b => b.DisplayOrder).ToList(); }
        }

        public async Task<BannerItem> SaveBannerAsync(BannerItem item, CancellationToken token = default)
        {
            item.UpdatedAt = DateTime.UtcNow;
            _localBanners[item.Id] = item;

            if (!IsMockMode)
            {
                await UpsertRestAsync("event_banners", item, token).ConfigureAwait(false);
            }
            return item;
        }

        public async Task<bool> DeleteBannerAsync(Guid id, CancellationToken token = default)
        {
            _localBanners.TryRemove(id, out var removed);
            if (!IsMockMode)
            {
                if (!string.IsNullOrWhiteSpace(removed?.StoragePath))
                    await DeleteStorageFileAsync("banners", removed.StoragePath, token).ConfigureAwait(false);
                await DeleteRestAsync("event_banners", id, token).ConfigureAwait(false);
            }
            return true;
        }

        public async Task<bool> ToggleBannerVisibilityAsync(Guid id, bool isPublished, CancellationToken token = default)
        {
            if (_localBanners.TryGetValue(id, out var item))
            {
                item.IsPublished = isPublished;
                item.UpdatedAt = DateTime.UtcNow;
                if (!IsMockMode)
                    await PatchRestAsync("event_banners", id, new { is_published = isPublished, updated_at = DateTime.UtcNow }, token).ConfigureAwait(false);
                return true;
            }
            return false;
        }
        #endregion

        #region Photo Albums & Photos (Jusqu'à 200 photos)
        public async Task<List<PhotoAlbumItem>> GetPhotoAlbumsAsync(CancellationToken token = default)
        {
            if (IsMockMode) return _localAlbums.Values.OrderByDescending(a => a.Date).ToList();

            try
            {
                var url = $"{_config.SupabaseUrl.TrimEnd('/')}/rest/v1/photo_albums?select=*&order=date.desc";
                return await GetRestListAsync<PhotoAlbumItem>(url, token).ConfigureAwait(false);
            }
            catch { return _localAlbums.Values.OrderByDescending(a => a.Date).ToList(); }
        }

        public async Task<PhotoAlbumItem> SavePhotoAlbumAsync(PhotoAlbumItem item, CancellationToken token = default)
        {
            item.UpdatedAt = DateTime.UtcNow;
            _localAlbums[item.Id] = item;

            if (!IsMockMode)
            {
                var payload = new
                {
                    id = item.Id,
                    title = item.Title,
                    description = item.Description,
                    date = item.Date.ToString("yyyy-MM-dd"),
                    cover_image_url = item.CoverImageUrl,
                    photo_count = item.PhotoCount,
                    is_published = item.IsPublished,
                    updated_at = DateTime.UtcNow
                };
                await UpsertRestAsync("photo_albums", payload, token).ConfigureAwait(false);
            }
            return item;
        }

        public async Task<bool> DeletePhotoAlbumAsync(Guid id, CancellationToken token = default)
        {
            _localAlbums.TryRemove(id, out _);
            if (!IsMockMode)
            {
                await DeleteRestAsync("photo_albums", id, token).ConfigureAwait(false);
            }
            return true;
        }

        public async Task<bool> TogglePhotoAlbumVisibilityAsync(Guid id, bool isPublished, CancellationToken token = default)
        {
            if (_localAlbums.TryGetValue(id, out var item))
            {
                item.IsPublished = isPublished;
                item.UpdatedAt = DateTime.UtcNow;
                if (!IsMockMode)
                    await PatchRestAsync("photo_albums", id, new { is_published = isPublished, updated_at = DateTime.UtcNow }, token).ConfigureAwait(false);
                return true;
            }
            return false;
        }

        public async Task<AlbumPhotoItem> AddPhotoToAlbumAsync(AlbumPhotoItem photo, CancellationToken token = default)
        {
            if (_localAlbums.TryGetValue(photo.AlbumId, out var album))
            {
                lock (album.Photos)
                {
                    album.Photos.Add(photo);
                    album.PhotoCount = album.Photos.Count;
                }
            }

            if (!IsMockMode)
            {
                await UpsertRestAsync("album_photos", photo, token).ConfigureAwait(false);
            }
            return photo;
        }
        #endregion

        #region Storage File Operations
        public string GetStoragePublicUrl(string bucket, string remotePath)
        {
            return $"{_config.SupabaseUrl.TrimEnd('/')}/storage/v1/object/public/{bucket}/{remotePath.TrimStart('/')}";
        }

        public async Task<string> UploadMediaFileAsync(
            string bucket,
            string remotePath,
            string localFilePath,
            IProgress<double>? progress = null,
            CancellationToken token = default)
        {
            if (!File.Exists(localFilePath))
                throw new FileNotFoundException("Fichier média local introuvable", localFilePath);

            if (IsMockMode)
            {
                // Simulation progressive d'upload pour tests locaux
                for (int i = 1; i <= 5; i++)
                {
                    await Task.Delay(10, token).ConfigureAwait(false);
                    progress?.Report(i * 20.0);
                }
                return GetStoragePublicUrl(bucket, remotePath);
            }

            string url = $"{_config.SupabaseUrl.TrimEnd('/')}/storage/v1/object/{bucket}/{remotePath.TrimStart('/')}";
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("apikey", _config.ApiKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
            req.Headers.Add("x-upsert", "true");

            using var fs = File.OpenRead(localFilePath);
            using var content = new StreamContent(fs);
            content.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(localFilePath));
            req.Content = content;

            using var resp = await _http.SendAsync(req, token).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                throw new InvalidOperationException($"Erreur upload Supabase ({resp.StatusCode}): {err}");
            }

            progress?.Report(100.0);
            return GetStoragePublicUrl(bucket, remotePath);
        }

        public async Task<bool> DeleteStorageFileAsync(string bucket, string remotePath, CancellationToken token = default)
        {
            if (IsMockMode) return true;

            string url = $"{_config.SupabaseUrl.TrimEnd('/')}/storage/v1/object/{bucket}/{remotePath.TrimStart('/')}";
            using var req = new HttpRequestMessage(HttpMethod.Delete, url);
            req.Headers.Add("apikey", _config.ApiKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);

            using var resp = await _http.SendAsync(req, token).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }

        private static string GetMimeType(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".m4a" or ".aac" => "audio/aac",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }
        #endregion

        #region Helpers REST Supabase
        private async Task<List<T>> GetRestListAsync<T>(string url, CancellationToken token)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("apikey", _config.ApiKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);

            using var resp = await _http.SendAsync(req, token).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return new List<T>();

            var json = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<T>>(json, _jsonOptions) ?? new List<T>();
        }

        private async Task UpsertRestAsync(string table, object data, CancellationToken token)
        {
            string url = $"{_config.SupabaseUrl.TrimEnd('/')}/rest/v1/{table}";
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("apikey", _config.ApiKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
            req.Headers.Add("Prefer", "resolution=merge-duplicates");

            string json = JsonSerializer.Serialize(data);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req, token).ConfigureAwait(false);
        }

        private async Task PatchRestAsync(string table, Guid id, object data, CancellationToken token)
        {
            string url = $"{_config.SupabaseUrl.TrimEnd('/')}/rest/v1/{table}?id=eq.{id}";
            using var req = new HttpRequestMessage(HttpMethod.Patch, url);
            req.Headers.Add("apikey", _config.ApiKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);

            string json = JsonSerializer.Serialize(data);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req, token).ConfigureAwait(false);
        }

        private async Task DeleteRestAsync(string table, Guid id, CancellationToken token)
        {
            string url = $"{_config.SupabaseUrl.TrimEnd('/')}/rest/v1/{table}?id=eq.{id}";
            using var req = new HttpRequestMessage(HttpMethod.Delete, url);
            req.Headers.Add("apikey", _config.ApiKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);

            using var resp = await _http.SendAsync(req, token).ConfigureAwait(false);
        }

        private void SeedSampleData()
        {
            // Échantillons par défaut pour tests et affichage immédiat
            var p1 = new PodcastItem
            {
                Title = "La Puissance de la Résurrection",
                Speaker = "Pasteur Principal",
                Date = DateTime.Today.AddDays(-3),
                Theme = "Victoire et Espérance",
                ScriptureRefs = "Jean 11:25-26, Romains 8:11",
                AudioUrl = "https://cdn.eecc.tv/podcasts/culte-2026-11-22.mp3",
                DurationSeconds = 3420,
                IsPublished = true
            };
            _localPodcasts[p1.Id] = p1;

            var a1 = new AudioSummaryItem
            {
                Title = "Extrait : Marcher par la foi",
                Speaker = "Pasteur Principal",
                Date = DateTime.Today.AddDays(-3),
                Description = "Courte capsule sur la persévérance spirituelle au quotidien.",
                AudioUrl = "https://cdn.eecc.tv/audio-summaries/capsule-foi.mp3",
                DurationSeconds = 180,
                IsPublished = true
            };
            _localAudioSummaries[a1.Id] = a1;

            var v1 = new ShortVideoItem
            {
                Title = "Moment fort de Louange",
                Speaker = "Chorale EECC",
                Date = DateTime.Today.AddDays(-2),
                Description = "Temps d'adoration intense lors du culte d'action de grâce.",
                VideoUrl = "https://cdn.eecc.tv/short-videos/louange-action-grace.mp4",
                DurationSeconds = 65,
                AspectRatio = "9:16",
                IsPublished = true
            };
            _localShortVideos[v1.Id] = v1;

            var s1 = new SermonTextItem
            {
                Title = "Notes : La Puissance de la Résurrection",
                Speaker = "Pasteur Principal",
                Date = DateTime.Today.AddDays(-3),
                Theme = "Victoire et Espérance",
                ScriptureRefs = "Jean 11:25-26",
                Summary = "1. Le tombeau vide atteste la promesse divine.\n2. La vie nouvelle triomphe des découragements.",
                KeyPoints = new List<string> { "La promesse accomplie", "L'autorité du Christ", "La marche victorieuse" },
                ReadingTimeMinutes = 4,
                IsPublished = true
            };
            _localSermonTexts[s1.Id] = s1;

            var b1 = new BannerItem
            {
                Title = "Grande Nuit de Prière et d'Intercession",
                Subtitle = "Vendredi 28 Novembre dès 21h00 au Sanctuaire Principal",
                ImageUrl = "https://cdn.eecc.tv/banners/nuit-priere.jpg",
                StartDate = DateTime.Today,
                DisplayOrder = 1,
                IsPublished = true
            };
            _localBanners[b1.Id] = b1;

            var alb1 = new PhotoAlbumItem
            {
                Title = "Culte d'Action de Grâce & Baptêmes",
                Description = "Photos souvenirs de la célébration dominicale et des baptêmes d'eau.",
                Date = DateTime.Today.AddDays(-3),
                CoverImageUrl = "https://cdn.eecc.tv/photo-galleries/baptemes-cover.jpg",
                PhotoCount = 24,
                IsPublished = true
            };
            _localAlbums[alb1.Id] = alb1;
        }
        #endregion
    }
}
