using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EeccStudio.Broadcast
{
    public sealed class ChapterMarker
    {
        public string Title { get; init; } = "";
        public string Category { get; init; } = "culte";
        public TimeSpan StartTime { get; init; }
        public TimeSpan? EndTime { get; set; }
    }

    /// <summary>
    /// Gestionnaire de chapitrage en direct :
    /// Permet au technicien régisseur de poser des marqueurs en un clic pendant le culte
    /// (Début de louange, Prédication, Lecture biblique, Annonces, Prière de clôture).
    /// Génère le fichier WebVTT et exporte vers la table Supabase `service_chapters`.
    /// </summary>
    public sealed class LiveChapterMarker
    {
        private readonly List<ChapterMarker> _markers = new();
        private DateTime? _sessionStartTime;
        private readonly object _lock = new();

        public bool IsActive => _sessionStartTime.HasValue;
        public IReadOnlyList<ChapterMarker> Markers { get { lock (_lock) return _markers.ToArray(); } }

        public void StartSession()
        {
            lock (_lock)
            {
                _markers.Clear();
                _sessionStartTime = DateTime.UtcNow;
            }
        }

        public ChapterMarker MarkChapter(string title, string category = "culte")
        {
            lock (_lock)
            {
                var elapsed = _sessionStartTime.HasValue ? DateTime.UtcNow - _sessionStartTime.Value : TimeSpan.Zero;

                // Clôture du marqueur précédent si existant
                if (_markers.Count > 0 && !_markers[^1].EndTime.HasValue)
                {
                    _markers[^1].EndTime = elapsed;
                }

                var marker = new ChapterMarker
                {
                    Title = title,
                    Category = category,
                    StartTime = elapsed
                };
                _markers.Add(marker);
                return marker;
            }
        }

        public void StopSession()
        {
            lock (_lock)
            {
                if (_markers.Count > 0 && !_markers[^1].EndTime.HasValue)
                {
                    var elapsed = _sessionStartTime.HasValue ? DateTime.UtcNow - _sessionStartTime.Value : TimeSpan.Zero;
                    _markers[^1].EndTime = elapsed;
                }
                _sessionStartTime = null;
            }
        }

        /// <summary>
        /// Exporte la liste des chapitres au format standard WebVTT (conforme aux spécifications Jellyfin & HTML5).
        /// </summary>
        public string ExportToWebVtt()
        {
            var sb = new StringBuilder();
            sb.AppendLine("WEBVTT");
            sb.AppendLine();

            lock (_lock)
            {
                for (int i = 0; i < _markers.Count; i++)
                {
                    var m = _markers[i];
                    var start = FormatTime(m.StartTime);
                    var end = FormatTime(m.EndTime ?? m.StartTime.Add(TimeSpan.FromMinutes(5)));

                    sb.AppendLine($"Chapter {i + 1}");
                    sb.AppendLine($"{start} --> {end}");
                    sb.AppendLine(m.Title);
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private static string FormatTime(TimeSpan ts)
        {
            return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }
    }
}
