using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EeccStudio.Core.Models
{
    public enum SourceType
    {
        PhysicalCamera,
        MobileCamera,
        PresentationSlide,
        BibleVerse,
        MediaVideo,
        AudioInput,
        ScreenCapture
    }

    public class StudioSource
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public SourceType Type { get; set; } = SourceType.PhysicalCamera;
        public bool IsVisible { get; set; } = true;
        public bool IsLocked { get; set; } = false;
        public string Details { get; set; } = string.Empty;

        public string IconSymbol => Type switch
        {
            SourceType.PhysicalCamera => "🎥",
            SourceType.MobileCamera => "📱",
            SourceType.PresentationSlide => "📊",
            SourceType.BibleVerse => "📖",
            SourceType.MediaVideo => "🎬",
            SourceType.AudioInput => "🎙️",
            SourceType.ScreenCapture => "🖥️",
            _ => "📹"
        };
    }

    public class StudioScene
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PreviewBadge { get; set; } = string.Empty;
        public ObservableCollection<StudioSource> Sources { get; set; } = new();

        public StudioScene(string name, string description = "")
        {
            Name = name;
            Description = description;
        }

        public static List<StudioScene> GetDefaultChurchScenes()
        {
            var scenes = new List<StudioScene>();

            // 1. Accueil & Décompte
            var scIntro = new StudioScene("🎬 Accueil & Décompte", "Ouverture de culte et compte à rebours");
            scIntro.Sources.Add(new StudioSource { Name = "Vidéo Intro Officielle EECC", Type = SourceType.MediaVideo, Details = "1080p60 Boucle" });
            scIntro.Sources.Add(new StudioSource { Name = "Musique d'accueil & Décompte", Type = SourceType.AudioInput, Details = "Stéréo 48kHz" });
            scIntro.Sources.Add(new StudioSource { Name = "Bannière Titre du Culte", Type = SourceType.PresentationSlide, Details = "Overlay 1920x200" });
            scenes.Add(scIntro);

            // 2. Orateur Principal
            var scSpeaker = new StudioScene("🎤 Orateur Principal (Pupitre)", "Plan serré pasteur et prédicateur");
            scSpeaker.Sources.Add(new StudioSource { Name = "Caméra 1 — Pupitre Pasteur (SDI)", Type = SourceType.PhysicalCamera, Details = "Sony FX3 / 1080p60" });
            scSpeaker.Sources.Add(new StudioSource { Name = "Micro Cravate Pasteur Principal", Type = SourceType.AudioInput, Details = "Sennheiser EW-D" });
            scSpeaker.Sources.Add(new StudioSource { Name = "Bandeau Inférieur (Nom Pasteur)", Type = SourceType.PresentationSlide, Details = "Tiers inférieur dynamique" });
            scenes.Add(scSpeaker);

            // 3. Chœur & Louange
            var scPraise = new StudioScene("🎵 Chœur & Louange", "Plan dynamique chorale et musiciens");
            scPraise.Sources.Add(new StudioSource { Name = "EECC Caméra (Smartphone Sans Fil)", Type = SourceType.MobileCamera, Details = "Flux SRT 1080p 5000kbps" });
            scPraise.Sources.Add(new StudioSource { Name = "Caméra 2 — Scène & Batterie", Type = SourceType.PhysicalCamera, Details = "Blackmagic Studio 4K" });
            scPraise.Sources.Add(new StudioSource { Name = "Micros Chœur & Instruments", Type = SourceType.AudioInput, Details = "Console Behringer X32" });
            scPraise.Sources.Add(new StudioSource { Name = "Paroles de Chants en Direct", Type = SourceType.PresentationSlide, Details = "Sous-titrage blanc sur fond noir" });
            scenes.Add(scPraise);

            // 4. Lecture Biblique
            var scBible = new StudioScene("📖 Lecture Biblique (Jean 3:16)", "Affichage des Écritures plein écran");
            scBible.Sources.Add(new StudioSource { Name = "Verset Biblique : Jean 3:16 (LSG)", Type = SourceType.BibleVerse, Details = "Bible Louis Segond 1910" });
            scBible.Sources.Add(new StudioSource { Name = "Fond Vidéo Méditatif Animé", Type = SourceType.MediaVideo, Details = "Motion Background Flou" });
            scBible.Sources.Add(new StudioSource { Name = "Micro Lecteur de la Parole", Type = SourceType.AudioInput, Details = "Micro Col de Cygne" });
            scenes.Add(scBible);

            // 5. Plan Large Assemblée
            var scWide = new StudioScene("🏛️ Plan Large Assemblée", "Vue d'ensemble du sanctuaire");
            scWide.Sources.Add(new StudioSource { Name = "Caméra Fond de Temple (Grand Angle)", Type = SourceType.PhysicalCamera, Details = "PTZOptics 30x NDI/SDI" });
            scWide.Sources.Add(new StudioSource { Name = "Micro d'Ambiance Sanctuaire", Type = SourceType.AudioInput, Details = "Ambiance assemblée stéréo" });
            scenes.Add(scWide);

            // 6. Annonces & Dîmes
            var scTreasury = new StudioScene("💰 Annonces & Dîmes (MaishaPay)", "Coordonnées de don et offrandes");
            scTreasury.Sources.Add(new StudioSource { Name = "QR Code & Numéros MaishaPay / M-Pesa", Type = SourceType.PresentationSlide, Details = "Overlay Dons & Dîmes" });
            scTreasury.Sources.Add(new StudioSource { Name = "Caméra Présentateur Annonces", Type = SourceType.PhysicalCamera, Details = "Caméra 1" });
            scTreasury.Sources.Add(new StudioSource { Name = "Fond Musical Doux", Type = SourceType.AudioInput, Details = "Piano méditation" });
            scenes.Add(scTreasury);

            // 7. Sainte-Cène & Bénédiction
            var scBlessing = new StudioScene("🕊️ Sainte-Cène & Bénédiction", "Communion et clôture fraternelle");
            scBlessing.Sources.Add(new StudioSource { Name = "Caméra Table de Communion", Type = SourceType.PhysicalCamera, Details = "Plan fixe haute définition" });
            scBlessing.Sources.Add(new StudioSource { Name = "Micro Bénédiction Pastorale", Type = SourceType.AudioInput, Details = "Pupitre" });
            scBlessing.Sources.Add(new StudioSource { Name = "Chant de Clôture", Type = SourceType.AudioInput, Details = "Chœur assemblée" });
            scenes.Add(scBlessing);

            return scenes;
        }
    }
}
