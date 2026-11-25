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
        ScreenCapture,
        Image,
        Text,
        ScrollingTicker
    }

    public class StudioSource
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public SourceType Type { get; set; } = SourceType.PhysicalCamera;
        public bool IsVisible { get; set; } = true;
        public bool IsLocked { get; set; } = false;
        public string Details { get; set; } = string.Empty;

        // Propriétés de superposition (Overlay OBS Studio)
        public string TextContent { get; set; } = string.Empty;
        public string SecondaryText { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public double ScrollSpeed { get; set; } = 80.0; // Vitesse de défilement (pixels / seconde)
        public double Opacity { get; set; } = 1.0;

        // Propriétés de transformation géométrique OBS Studio (Correction caméra inversée / miroir)
        public int RotationAngle { get; set; } = 0; // 0, 90, 180, 270 degrés
        public bool FlipHorizontal { get; set; } = false; // Effet miroir horizontal
        public bool FlipVertical { get; set; } = false; // Inversion verticale (tête en bas)

        public string IconSymbol => Type switch
        {
            SourceType.PhysicalCamera => "🎥",
            SourceType.MobileCamera => "📱",
            SourceType.PresentationSlide => "📊",
            SourceType.BibleVerse => "📖",
            SourceType.MediaVideo => "🎬",
            SourceType.AudioInput => "🎙️",
            SourceType.ScreenCapture => "🖥️",
            SourceType.Image => "🖼️",
            SourceType.Text => "📝",
            SourceType.ScrollingTicker => "📜",
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

            // 1. Culte Principal
            var scMain = new StudioScene("Culte Principal", "Diffusion principale du culte");
            scMain.Sources.Add(new StudioSource { Name = "Caméra 1", Type = SourceType.PhysicalCamera, Details = "Plan Principal Orateur" });
            scMain.Sources.Add(new StudioSource { Name = "Caméra 2", Type = SourceType.MobileCamera, Details = "Caméra Mobile EECC" });
            scMain.Sources.Add(new StudioSource
            {
                Name = "Logo EECC (Filigrane)",
                Type = SourceType.Image,
                Details = "Filigrane d'angle supérieur",
                ImagePath = "Assets/logo.png"
            });
            scMain.Sources.Add(new StudioSource
            {
                Name = "Bandeau Pasteur & Verset",
                Type = SourceType.Text,
                TextContent = "Pasteur Principal - Église Évangélique du Centre",
                SecondaryText = "Thème : La Puissance de la Résurrection (Philippiens 3:10)",
                Details = "Tiers inférieur (Lower Third)"
            });
            scMain.Sources.Add(new StudioSource
            {
                Name = "Bande Déroulante Annonces",
                Type = SourceType.ScrollingTicker,
                TextContent = "✦ Bienvenue au culte de l'Église Évangélique du Centre (EECC) ✦ Dimanche prochain : Culte de Sainte Cène à 9h00 ✦ Dons et Dîmes par Mobile Money au +243 000 000 000 ou en ligne sur notre application EECC TV ✦",
                ScrollSpeed = 70.0,
                Details = "Bandeau défilant inférieur continu"
            });
            scenes.Add(scMain);

            // 2. Louange
            var scPraise = new StudioScene("Louange", "Moment de chants et louanges");
            scPraise.Sources.Add(new StudioSource { Name = "Caméra 2", Type = SourceType.MobileCamera });
            scPraise.Sources.Add(new StudioSource { Name = "Caméra 1", Type = SourceType.PhysicalCamera });
            scPraise.Sources.Add(new StudioSource { Name = "Micros Chœur & Orchestre", Type = SourceType.AudioInput });
            scPraise.Sources.Add(new StudioSource { Name = "Logo EECC", Type = SourceType.Image, ImagePath = "Assets/logo.png" });
            scenes.Add(scPraise);

            // 3. Enseignement
            var scTeach = new StudioScene("Enseignement", "Prédication et enseignement biblique");
            scTeach.Sources.Add(new StudioSource { Name = "Caméra 1", Type = SourceType.PhysicalCamera });
            scTeach.Sources.Add(new StudioSource { Name = "Verset Biblique", Type = SourceType.BibleVerse, TextContent = "Jean 3:16", Details = "Car Dieu a tant aimé le monde..." });
            scTeach.Sources.Add(new StudioSource { Name = "Micro Pasteur", Type = SourceType.AudioInput });
            scenes.Add(scTeach);

            // 4. Prière
            var scPrayer = new StudioScene("Prière", "Temps d'intercession et de prière");
            scPrayer.Sources.Add(new StudioSource { Name = "Caméra 1", Type = SourceType.PhysicalCamera });
            scPrayer.Sources.Add(new StudioSource { Name = "Fond Musical", Type = SourceType.AudioInput });
            scenes.Add(scPrayer);

            // 5. Fin
            var scEnd = new StudioScene("Fin", "Clôture du culte et bénédiction");
            scEnd.Sources.Add(new StudioSource { Name = "QR Code & Dons", Type = SourceType.PresentationSlide });
            scEnd.Sources.Add(new StudioSource { Name = "Fond Musical", Type = SourceType.AudioInput });
            scenes.Add(scEnd);

            return scenes;
        }
    }
}
