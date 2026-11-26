using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using EeccStudio.Broadcast;
using EeccStudio.Core.Cms;
using EeccStudio.Core.Network;
using Microsoft.Win32;

namespace EeccStudio
{
    public partial class EeccTvCmsWindow : Window
    {
        private readonly EeccTvCmsService _cmsService;
        private readonly BatchPhotoUploader _batchUploader;
        private CancellationTokenSource? _batchCts;
        private readonly DispatcherTimer _captionTimer;
        private bool _captionReady;

        public CameraServer? StudioServer { get; set; }

        // ViewModels avec affichage formaté
        public class PodcastDisplay : PodcastItem
        {
            public string IsPublishedText => IsPublished ? "🟢 Visible sur EECC TV" : "🟡 Masqué (Brouillon)";
            public string DateFormatted => Date.ToString("dd/MM/yyyy");
            public string DurationFormatted => TimeSpan.FromSeconds(DurationSeconds).ToString(@"mm\:ss");
        }

        public class AudioSummaryDisplay : AudioSummaryItem
        {
            public string IsPublishedText => IsPublished ? "🟢 Visible sur EECC TV" : "🟡 Masqué (Brouillon)";
            public string DateFormatted => Date.ToString("dd/MM/yyyy");
            public string DurationFormatted => TimeSpan.FromSeconds(DurationSeconds).ToString(@"mm\:ss");
        }

        public class ShortVideoDisplay : ShortVideoItem
        {
            public string IsPublishedText => IsPublished ? "🟢 Visible sur EECC TV" : "🟡 Masqué (Brouillon)";
            public string DateFormatted => Date.ToString("dd/MM/yyyy");
            public string DurationFormatted => TimeSpan.FromSeconds(DurationSeconds).ToString(@"mm\:ss");
        }

        public class SermonTextDisplay : SermonTextItem
        {
            public string IsPublishedText => IsPublished ? "🟢 Visible sur EECC TV" : "🟡 Masqué (Brouillon)";
            public string DateFormatted => Date.ToString("dd/MM/yyyy");
        }

        public class BannerDisplay : BannerItem
        {
            public string IsPublishedText => IsPublished ? "🟢 Visible sur EECC TV" : "🟡 Masqué (Brouillon)";
        }

        public class PhotoAlbumDisplay : PhotoAlbumItem
        {
            public string IsPublishedText => IsPublished ? "🟢 Visible sur EECC TV" : "🟡 Masqué (Brouillon)";
            public string DateFormatted => Date.ToString("dd/MM/yyyy");
            public string PhotoCountFormatted => $"{PhotoCount} photo(s)";
        }

        private readonly ObservableCollection<PodcastDisplay> _podcasts = new();
        private readonly ObservableCollection<AudioSummaryDisplay> _audioSummaries = new();
        private readonly ObservableCollection<ShortVideoDisplay> _shortVideos = new();
        private readonly ObservableCollection<SermonTextDisplay> _sermonTexts = new();
        private readonly ObservableCollection<BannerDisplay> _banners = new();
        private readonly ObservableCollection<PhotoAlbumDisplay> _albums = new();

        public EeccTvCmsWindow(EeccTvCmsService? cmsService = null)
        {
            InitializeComponent();
            _cmsService = cmsService ?? EeccTvCmsService.ForStudio();
            _batchUploader = new BatchPhotoUploader(_cmsService, maxConcurrency: 4);

            GridPodcasts.ItemsSource = _podcasts;
            GridAudioSummaries.ItemsSource = _audioSummaries;
            GridShortVideos.ItemsSource = _shortVideos;
            GridSermonTexts.ItemsSource = _sermonTexts;
            GridBanners.ItemsSource = _banners;
            GridAlbums.ItemsSource = _albums;
            _captionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _captionTimer.Tick += async (_, _) =>
            {
                _captionTimer.Stop();
                var server = StudioServer;
                if (server == null) return;
                server.ProgramTitle = TxtLiveTitle.Text.Trim();
                server.Speaker = TxtLiveSpeaker.Text.Trim();
                try
                {
                    await EeccTvCatalog.UpdateCaptionAsync(server.ProgramTitle, server.Speaker, server.Theme);
                }
                catch
                {
                    // Le texte reste local. La frappe suivante réessaie.
                }
            };

            Loaded += async (_, _) =>
            {
                var server = StudioServer;
                TxtLiveTitle.Text = server?.ProgramTitle ?? "Culte EECC";
                TxtLiveSpeaker.Text = server?.Speaker ?? "";
                _captionReady = true;
                var host = server?.GetLocalIpAddress();
                if (string.IsNullOrWhiteSpace(host))
                    host = "127.0.0.1";
                var port = server?.ListeningPort ?? server?.Port ?? 8089;
                TxtLiveUrl.Text = $"http://{host}:{port}/live/stream";
                await LoadAllDataAsync();
            };
        }

        private void LiveCaption_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!_captionReady) return;
            _captionTimer.Stop();
            _captionTimer.Start();
        }

        private async void BtnPublishLive_Click(object sender, RoutedEventArgs e)
        {
            var url = TxtLiveUrl.Text.Trim();
            if (url.Length == 0)
            {
                TxtLiveResult.Text = "Indiquez l'adresse du flux.";
                return;
            }

            StudioServer?.StartStreaming();
            TxtLiveState.Text = "Publication…";
            TxtLiveState.Foreground = System.Windows.Media.Brushes.White;
            var message = await EeccTvCatalog.PublishLiveAsync(url, TxtLiveTitle.Text.Trim(), TxtLiveSpeaker.Text.Trim());
            TxtLiveResult.Text = message;
            TxtLiveState.Text = message.Contains("publié", StringComparison.OrdinalIgnoreCase) ? "En direct" : "À vérifier";
            TxtLiveState.Foreground = message.Contains("publié", StringComparison.OrdinalIgnoreCase)
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0x32, 0x32))
                : System.Windows.Media.Brushes.Goldenrod;
            TxtGlobalStatus.Text = message;
        }

        private async void BtnEndLive_Click(object sender, RoutedEventArgs e)
        {
            StudioServer?.StopStreaming();
            var message = await EeccTvCatalog.EndLiveAsync() ?? "Le direct est terminé.";
            TxtLiveResult.Text = message;
            TxtLiveState.Text = "Hors ligne";
            TxtLiveState.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8E, 0x92, 0x97));
            TxtGlobalStatus.Text = message;
        }

        private async Task LoadAllDataAsync()
        {
            try
            {
                TxtGlobalStatus.Text = "Chargement des contenus depuis Supabase...";

                // Podcasts
                var pods = await _cmsService.GetPodcastsAsync();
                _podcasts.Clear();
                foreach (var p in pods)
                {
                    _podcasts.Add(new PodcastDisplay
                    {
                        Id = p.Id,
                        Title = p.Title,
                        Speaker = p.Speaker,
                        Date = p.Date,
                        Theme = p.Theme,
                        ScriptureRefs = p.ScriptureRefs,
                        AudioUrl = p.AudioUrl,
                        DurationSeconds = p.DurationSeconds,
                        IsPublished = p.IsPublished
                    });
                }
                TxtPodcastCount.Text = $"{_podcasts.Count} épisode(s)";

                // Audio Summaries
                var audios = await _cmsService.GetAudioSummariesAsync();
                _audioSummaries.Clear();
                foreach (var a in audios)
                {
                    _audioSummaries.Add(new AudioSummaryDisplay
                    {
                        Id = a.Id,
                        Title = a.Title,
                        Speaker = a.Speaker,
                        Date = a.Date,
                        Description = a.Description,
                        AudioUrl = a.AudioUrl,
                        DurationSeconds = a.DurationSeconds,
                        IsPublished = a.IsPublished
                    });
                }
                TxtAudioSummaryCount.Text = $"{_audioSummaries.Count} capsule(s)";

                // Short Videos
                var shorts = await _cmsService.GetShortVideosAsync();
                _shortVideos.Clear();
                foreach (var v in shorts)
                {
                    _shortVideos.Add(new ShortVideoDisplay
                    {
                        Id = v.Id,
                        Title = v.Title,
                        Speaker = v.Speaker,
                        Date = v.Date,
                        Description = v.Description,
                        VideoUrl = v.VideoUrl,
                        DurationSeconds = v.DurationSeconds,
                        AspectRatio = v.AspectRatio,
                        IsPublished = v.IsPublished
                    });
                }
                TxtShortVideoCount.Text = $"{_shortVideos.Count} clip(s)";

                // Sermon Texts
                var texts = await _cmsService.GetSermonTextsAsync();
                _sermonTexts.Clear();
                foreach (var t in texts)
                {
                    _sermonTexts.Add(new SermonTextDisplay
                    {
                        Id = t.Id,
                        Title = t.Title,
                        Speaker = t.Speaker,
                        Date = t.Date,
                        Theme = t.Theme,
                        ScriptureRefs = t.ScriptureRefs,
                        Summary = t.Summary,
                        KeyPoints = t.KeyPoints,
                        IsPublished = t.IsPublished
                    });
                }
                TxtSermonTextCount.Text = $"{_sermonTexts.Count} résumé(s)";

                // Banners
                var banners = await _cmsService.GetBannersAsync();
                _banners.Clear();
                foreach (var b in banners)
                {
                    _banners.Add(new BannerDisplay
                    {
                        Id = b.Id,
                        Title = b.Title,
                        Subtitle = b.Subtitle,
                        ImageUrl = b.ImageUrl,
                        DisplayOrder = b.DisplayOrder,
                        IsPublished = b.IsPublished
                    });
                }
                TxtBannerCount.Text = $"{_banners.Count} affiche(s)";

                // Photo Albums
                var albums = await _cmsService.GetPhotoAlbumsAsync();
                _albums.Clear();
                foreach (var alb in albums)
                {
                    _albums.Add(new PhotoAlbumDisplay
                    {
                        Id = alb.Id,
                        Title = alb.Title,
                        Date = alb.Date,
                        Description = alb.Description,
                        PhotoCount = alb.PhotoCount,
                        IsPublished = alb.IsPublished
                    });
                }
                TxtAlbumCount.Text = $"{_albums.Count} album(s)";

                TxtGlobalStatus.Text = _cmsService.UsesCloud
                    ? "Contenus synchronisés avec Supabase."
                    : "Les publications de cette page sont écrites dans Supabase.";
            }
            catch (Exception ex)
            {
                TxtGlobalStatus.Text = $"Erreur de chargement: {ex.Message}";
            }
        }

        private async void BtnRefreshAll_Click(object sender, RoutedEventArgs e)
        {
            await LoadAllDataAsync();
        }

        #region Actions Podcasts
        private async void BtnNewPodcast_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Title = "Sélectionner l'enregistrement audio du Culte (MP3 / AAC)",
                Filter = "Fichiers Audio (*.mp3;*.m4a;*.wav)|*.mp3;*.m4a;*.wav"
            };

            if (ofd.ShowDialog() == true)
            {
                var newPodcast = new PodcastItem
                {
                    Title = $"Culte du {DateTime.Today:dd/MM/yyyy}",
                    Speaker = "Pasteur Principal",
                    Date = DateTime.Today,
                    AudioUrl = $"https://cdn.eecc.tv/podcasts/{Path.GetFileName(ofd.FileName)}",
                    DurationSeconds = 3600,
                    IsPublished = true
                };

                await _cmsService.SavePodcastAsync(newPodcast);
                await LoadAllDataAsync();
                MessageBox.Show($"Le podcast '{newPodcast.Title}' a été publié pour EECC TV !", "Podcast Publié", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void BtnTogglePodcast_Click(object sender, RoutedEventArgs e)
        {
            if (GridPodcasts.SelectedItem is PodcastDisplay selected)
            {
                bool newState = !selected.IsPublished;
                await _cmsService.TogglePodcastVisibilityAsync(selected.Id, newState);
                await LoadAllDataAsync();
            }
            else
            {
                MessageBox.Show("Veuillez sélectionner un podcast dans la liste.", "Sélection Requise", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void BtnDeletePodcast_Click(object sender, RoutedEventArgs e)
        {
            if (GridPodcasts.SelectedItem is PodcastDisplay selected)
            {
                if (MessageBox.Show($"Supprimer définitivement '{selected.Title}' ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    await _cmsService.DeletePodcastAsync(selected.Id);
                    await LoadAllDataAsync();
                }
            }
        }
        #endregion

        #region Actions Résumés Audios
        private async void BtnNewAudioSummary_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Title = "Sélectionner la capsule audio courte (1 à 5 min)",
                Filter = "Fichiers Audio (*.mp3;*.m4a;*.wav)|*.mp3;*.m4a;*.wav"
            };

            if (ofd.ShowDialog() == true)
            {
                var newAudio = new AudioSummaryItem
                {
                    Title = $"Extrait : Message du {DateTime.Today:dd/MM/yyyy}",
                    Speaker = "Pasteur Principal",
                    Date = DateTime.Today,
                    AudioUrl = $"https://cdn.eecc.tv/audio-summaries/{Path.GetFileName(ofd.FileName)}",
                    DurationSeconds = 180,
                    IsPublished = true
                };

                await _cmsService.SaveAudioSummaryAsync(newAudio);
                await LoadAllDataAsync();
                MessageBox.Show($"La capsule audio '{newAudio.Title}' est en ligne !", "Capsule Publiée", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void BtnToggleAudioSummary_Click(object sender, RoutedEventArgs e)
        {
            if (GridAudioSummaries.SelectedItem is AudioSummaryDisplay selected)
            {
                bool newState = !selected.IsPublished;
                await _cmsService.ToggleAudioSummaryVisibilityAsync(selected.Id, newState);
                await LoadAllDataAsync();
            }
        }

        private async void BtnDeleteAudioSummary_Click(object sender, RoutedEventArgs e)
        {
            if (GridAudioSummaries.SelectedItem is AudioSummaryDisplay selected)
            {
                if (MessageBox.Show($"Supprimer définitivement la capsule '{selected.Title}' ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    await _cmsService.DeleteAudioSummaryAsync(selected.Id);
                    await LoadAllDataAsync();
                }
            }
        }
        #endregion

        #region Actions Petites Vidéos (Shorts)
        private async void BtnNewShortVideo_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Title = "Sélectionner la courte vidéo (Short / Reel / Moment fort)",
                Filter = "Fichiers Vidéo (*.mp4;*.webm;*.mov)|*.mp4;*.webm;*.mov"
            };

            if (ofd.ShowDialog() == true)
            {
                var newVideo = new ShortVideoItem
                {
                    Title = $"Clip : Culte du {DateTime.Today:dd/MM/yyyy}",
                    Date = DateTime.Today,
                    VideoUrl = $"https://cdn.eecc.tv/short-videos/{Path.GetFileName(ofd.FileName)}",
                    DurationSeconds = 60,
                    AspectRatio = "9:16",
                    IsPublished = true
                };

                await _cmsService.SaveShortVideoAsync(newVideo);
                await LoadAllDataAsync();
                MessageBox.Show($"La vidéo courte '{newVideo.Title}' a été publiée sur EECC TV !", "Vidéo Publiée", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void BtnToggleShortVideo_Click(object sender, RoutedEventArgs e)
        {
            if (GridShortVideos.SelectedItem is ShortVideoDisplay selected)
            {
                bool newState = !selected.IsPublished;
                await _cmsService.ToggleShortVideoVisibilityAsync(selected.Id, newState);
                await LoadAllDataAsync();
            }
        }

        private async void BtnDeleteShortVideo_Click(object sender, RoutedEventArgs e)
        {
            if (GridShortVideos.SelectedItem is ShortVideoDisplay selected)
            {
                if (MessageBox.Show($"Supprimer définitivement le clip '{selected.Title}' ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    await _cmsService.DeleteShortVideoAsync(selected.Id);
                    await LoadAllDataAsync();
                }
            }
        }
        #endregion

        #region Actions Résumés Textes
        private async void BtnNewSermonText_Click(object sender, RoutedEventArgs e)
        {
            var newText = new SermonTextItem
            {
                Title = $"Résumé de la prédication du {DateTime.Today:dd/MM/yyyy}",
                Speaker = "Pasteur Principal",
                Date = DateTime.Today,
                Theme = "La foi qui triomphe",
                ScriptureRefs = "Hébreux 11:1-6",
                Summary = "Points clés du message dominical et applications spirituelles pour la semaine.",
                KeyPoints = new List<string> { "Comprendre l'épreuve", "Activer la foi", "Vivre dans la paix" },
                IsPublished = true
            };

            await _cmsService.SaveSermonTextAsync(newText);
            await LoadAllDataAsync();
            MessageBox.Show($"Le résumé textuel '{newText.Title}' est disponible sur EECC TV !", "Résumé Publié", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnToggleSermonText_Click(object sender, RoutedEventArgs e)
        {
            if (GridSermonTexts.SelectedItem is SermonTextDisplay selected)
            {
                bool newState = !selected.IsPublished;
                await _cmsService.ToggleSermonTextVisibilityAsync(selected.Id, newState);
                await LoadAllDataAsync();
            }
        }

        private async void BtnDeleteSermonText_Click(object sender, RoutedEventArgs e)
        {
            if (GridSermonTexts.SelectedItem is SermonTextDisplay selected)
            {
                if (MessageBox.Show($"Supprimer définitivement le résumé '{selected.Title}' ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    await _cmsService.DeleteSermonTextAsync(selected.Id);
                    await LoadAllDataAsync();
                }
            }
        }
        #endregion

        #region Actions Affiches & Flyers
        private async void BtnNewBanner_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Title = "Sélectionner l'affiche publicitaire ou le flyer d'événement",
                Filter = "Images (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp"
            };

            if (ofd.ShowDialog() == true)
            {
                var newBanner = new BannerItem
                {
                    Title = "Événement Spécial EECC",
                    Subtitle = $"Affichage du {DateTime.Today:dd/MM/yyyy}",
                    ImageUrl = $"https://cdn.eecc.tv/banners/{Path.GetFileName(ofd.FileName)}",
                    StartDate = DateTime.Today,
                    DisplayOrder = _banners.Count + 1,
                    IsPublished = true
                };

                await _cmsService.SaveBannerAsync(newBanner);
                await LoadAllDataAsync();
                MessageBox.Show($"L'affiche '{newBanner.Title}' a été publiée dans le carrousel d'accueil d'EECC TV !", "Affiche Publiée", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void BtnToggleBanner_Click(object sender, RoutedEventArgs e)
        {
            if (GridBanners.SelectedItem is BannerDisplay selected)
            {
                bool newState = !selected.IsPublished;
                await _cmsService.ToggleBannerVisibilityAsync(selected.Id, newState);
                await LoadAllDataAsync();
            }
        }

        private async void BtnDeleteBanner_Click(object sender, RoutedEventArgs e)
        {
            if (GridBanners.SelectedItem is BannerDisplay selected)
            {
                if (MessageBox.Show($"Supprimer définitivement l'affiche '{selected.Title}' ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    await _cmsService.DeleteBannerAsync(selected.Id);
                    await LoadAllDataAsync();
                }
            }
        }
        #endregion

        #region Actions Galeries Photos (Jusqu'à 200 images)
        private async void BtnNewAlbum_Click(object sender, RoutedEventArgs e)
        {
            var newAlbum = new PhotoAlbumItem
            {
                Title = $"Album Culte du {DateTime.Today:dd/MM/yyyy}",
                Date = DateTime.Today,
                Description = "Photos de la célébration dominicale et des moments de communion fraternelle.",
                PhotoCount = 0,
                IsPublished = true
            };

            await _cmsService.SavePhotoAlbumAsync(newAlbum);
            await LoadAllDataAsync();
            MessageBox.Show($"L'album '{newAlbum.Title}' a été créé. Vous pouvez maintenant y téléverser jusqu'à 200 photos !", "Album Créé", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnUploadBatchPhotos_Click(object sender, RoutedEventArgs e)
        {
            if (GridAlbums.SelectedItem is not PhotoAlbumDisplay selectedAlbum)
            {
                MessageBox.Show("Veuillez d'abord sélectionner un album dans la liste ci-dessus.", "Sélection Requise", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var ofd = new OpenFileDialog
            {
                Title = "Sélectionner jusqu'à 200 photos pour l'album",
                Filter = "Images (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp",
                Multiselect = true
            };

            if (ofd.ShowDialog() == true)
            {
                var files = ofd.FileNames;
                if (files.Length == 0) return;

                if (files.Length > 200)
                {
                    MessageBox.Show($"Vous avez sélectionné {files.Length} photos. Seules les 200 premières seront téléversées.", "Limite de 200 photos", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                PanelBatchProgress.Visibility = Visibility.Visible;
                ProgressBatch.Value = 0;
                TxtBatchStatus.Text = $"Téléversement de {Math.Min(files.Length, 200)} photos en cours...";

                _batchCts = new CancellationTokenSource();
                var progress = new Progress<BatchUploadProgress>(p =>
                {
                    ProgressBatch.Value = p.OverallPercent;
                    TxtBatchFileName.Text = $"{p.CompletedCount}/{p.TotalCount} ({p.CurrentFileName})";
                });

                try
                {
                    var uploaded = await _batchUploader.UploadAlbumPhotosAsync(selectedAlbum.Id, files, progress, _batchCts.Token);
                    selectedAlbum.PhotoCount += uploaded.Count;
                    await _cmsService.SavePhotoAlbumAsync(selectedAlbum);
                    await LoadAllDataAsync();

                    MessageBox.Show($"Succès ! {uploaded.Count} photos ont été téléversées et publiées dans l'album pour EECC TV.", "Lot Téléversé", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erreur lors de l'envoi du lot: {ex.Message}", "Erreur Lot", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    PanelBatchProgress.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void BtnToggleAlbum_Click(object sender, RoutedEventArgs e)
        {
            if (GridAlbums.SelectedItem is PhotoAlbumDisplay selected)
            {
                bool newState = !selected.IsPublished;
                await _cmsService.TogglePhotoAlbumVisibilityAsync(selected.Id, newState);
                await LoadAllDataAsync();
            }
        }

        private async void BtnDeleteAlbum_Click(object sender, RoutedEventArgs e)
        {
            if (GridAlbums.SelectedItem is PhotoAlbumDisplay selected)
            {
                if (MessageBox.Show($"Supprimer définitivement l'album '{selected.Title}' et toutes ses photos ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    await _cmsService.DeletePhotoAlbumAsync(selected.Id);
                    await LoadAllDataAsync();
                }
            }
        }
        #endregion

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
