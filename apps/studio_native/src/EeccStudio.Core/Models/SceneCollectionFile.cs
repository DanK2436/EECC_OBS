using System.Text.Json;
using System.Text.Json.Serialization;

namespace EeccStudio.Core.Models;

public static class SceneCollectionFile
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return cleaned.Length == 0 ? "sans-nom" : cleaned;
    }

    public static string PathFor(string root, string profile, string collection) =>
        System.IO.Path.Combine(root, Sanitize(profile), Sanitize(collection) + ".json");

    public static void Save(string path, IEnumerable<StudioScene> scenes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var payload = scenes.Select(SceneDto.From).ToList();
        File.WriteAllText(path, JsonSerializer.Serialize(payload, Json));
    }

    public static List<StudioScene> Load(string path)
    {
        if (!File.Exists(path))
            return [];
        var payload = JsonSerializer.Deserialize<List<SceneDto>>(File.ReadAllText(path), Json);
        return payload?.Select(dto => dto.ToScene()).ToList() ?? [];
    }

    private sealed class SceneDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string PreviewBadge { get; set; } = "";
        public List<SourceDto> Sources { get; set; } = [];

        public static SceneDto From(StudioScene scene) => new()
        {
            Id = scene.Id,
            Name = scene.Name,
            Description = scene.Description,
            PreviewBadge = scene.PreviewBadge,
            Sources = scene.Sources.Select(SourceDto.From).ToList(),
        };

        public StudioScene ToScene()
        {
            var scene = new StudioScene(Name, Description) { Id = Id, PreviewBadge = PreviewBadge };
            foreach (var source in Sources)
                scene.Sources.Add(source.ToSource());
            return scene;
        }
    }

    private sealed class SourceDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public SourceType Type { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsLocked { get; set; }
        public string Details { get; set; } = "";
        public string TextContent { get; set; } = "";
        public string SecondaryText { get; set; } = "";
        public string ImagePath { get; set; } = "";
        public double ScrollSpeed { get; set; } = 80;
        public double Opacity { get; set; } = 1;
        public bool GraphicPlaced { get; set; }
        public GraphicTextStyle TextStyle { get; set; }
        public GraphicMotion Motion { get; set; }
        public GraphicShape Shape { get; set; }
        public double XPercent { get; set; }
        public double YPercent { get; set; }
        public double WidthPercent { get; set; }
        public double HeightPercent { get; set; }
        public double FontSize { get; set; }
        public int RotationAngle { get; set; }
        public bool FlipHorizontal { get; set; }
        public bool FlipVertical { get; set; }

        public static SourceDto From(StudioSource source) => new()
        {
            Id = source.Id,
            Name = source.Name,
            Type = source.Type,
            IsVisible = source.IsVisible,
            IsLocked = source.IsLocked,
            Details = source.Details,
            TextContent = source.TextContent,
            SecondaryText = source.SecondaryText,
            ImagePath = source.ImagePath,
            ScrollSpeed = source.ScrollSpeed,
            Opacity = source.Opacity,
            GraphicPlaced = source.GraphicPlaced,
            TextStyle = source.TextStyle,
            Motion = source.Motion,
            Shape = source.Shape,
            XPercent = source.XPercent,
            YPercent = source.YPercent,
            WidthPercent = source.WidthPercent,
            HeightPercent = source.HeightPercent,
            FontSize = source.FontSize,
            RotationAngle = source.RotationAngle,
            FlipHorizontal = source.FlipHorizontal,
            FlipVertical = source.FlipVertical,
        };

        public StudioSource ToSource() => new()
        {
            Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id,
            Name = Name,
            Type = Type,
            IsVisible = IsVisible,
            IsLocked = IsLocked,
            Details = Details,
            TextContent = TextContent,
            SecondaryText = SecondaryText,
            ImagePath = ImagePath,
            ScrollSpeed = ScrollSpeed,
            Opacity = Opacity,
            GraphicPlaced = GraphicPlaced,
            TextStyle = TextStyle,
            Motion = Motion,
            Shape = Shape,
            XPercent = XPercent,
            YPercent = YPercent,
            WidthPercent = WidthPercent,
            HeightPercent = HeightPercent,
            FontSize = FontSize,
            RotationAngle = RotationAngle,
            FlipHorizontal = FlipHorizontal,
            FlipVertical = FlipVertical,
        };
    }
}
