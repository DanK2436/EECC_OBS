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

            // 1. Culte Principal
            var scMain = new StudioScene("Culte Principal", "Diffusion principale du culte");
            scMain.Sources.Add(new StudioSource { Name = "Caméra 1", Type = SourceType.PhysicalCamera, Details = "Plan Principal Orateur" });
            scMain.Sources.Add(new StudioSource { Name = "Caméra 2", Type = SourceType.MobileCamera, Details = "Caméra Mobile EECC" });
            scMain.Sources.Add(new StudioSource { Name = "QR Code", Type = SourceType.PresentationSlide, Details = "QR Code Appairage & Dons" });
            scMain.Sources.Add(new StudioSource { Name = "Média Pastor", Type = SourceType.MediaVideo, Details = "Supports visuels & Versets" });
            scMain.Sources.Add(new StudioSource { Name = "Fond Musical", Type = SourceType.AudioInput, Details = "Audio Synthé & Chœur" });
            scMain.Sources.Add(new StudioSource { Name = "Logo ECC", Type = SourceType.PresentationSlide, Details = "Logo Officiel EECC en filigrane" });
            scenes.Add(scMain);

            // 2. Louange
            var scPraise = new StudioScene("Louange", "Moment de chants et louanges");
            scPraise.Sources.Add(new StudioSource { Name = "Caméra 2", Type = SourceType.MobileCamera });
            scPraise.Sources.Add(new StudioSource { Name = "Caméra 1", Type = SourceType.PhysicalCamera });
            scPraise.Sources.Add(new StudioSource { Name = "Micros Chœur & Orchestre", Type = SourceType.AudioInput });
            scPraise.Sources.Add(new StudioSource { Name = "Logo ECC", Type = SourceType.PresentationSlide });
            scenes.Add(scPraise);

            // 3. Enseignement
            var scTeach = new StudioScene("Enseignement", "Prédication et enseignement biblique");
            scTeach.Sources.Add(new StudioSource { Name = "Caméra 1", Type = SourceType.PhysicalCamera });
            scTeach.Sources.Add(new StudioSource { Name = "Média Pastor", Type = SourceType.BibleVerse });
            scTeach.Sources.Add(new StudioSource { Name = "Micro Pasteur", Type = SourceType.AudioInput });
            scenes.Add(scTeach);

            // 4. Prière
            var scPrayer = new StudioScene("Prière", "Temps d'intercession et de prière");
            scPrayer.Sources.Add(new StudioSource { Name = "Caméra 1", Type = SourceType.PhysicalCamera });
            scPrayer.Sources.Add(new StudioSource { Name = "Fond Musical", Type = SourceType.AudioInput });
            scenes.Add(scPrayer);

            // 5. Fin
            var scEnd = new StudioScene("Fin", "Clôture du culte et bénédiction");
            scEnd.Sources.Add(new StudioSource { Name = "Média Pastor", Type = SourceType.MediaVideo });
            scEnd.Sources.Add(new StudioSource { Name = "QR Code", Type = SourceType.PresentationSlide });
            scEnd.Sources.Add(new StudioSource { Name = "Fond Musical", Type = SourceType.AudioInput });
            scenes.Add(scEnd);

            return scenes;
        }
    }
}
