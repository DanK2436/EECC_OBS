using System.Globalization;
using System.Text.Json;

namespace EeccStudio.Core.Production;

public enum SceneSourceKind
{
    Camera,
    Color,
}

public sealed class StudioScene
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public SceneSourceKind Source { get; set; }
    public string ColorHex { get; set; } = "#1E3A8A";
}

public sealed class SceneDocument
{
    public List<StudioScene> Scenes { get; set; } = new();
    public string? PreviewSceneId { get; set; }
    public string? ProgramSceneId { get; set; }
}

public static class SceneStore
{
    public static SceneDocument CreateDefault()
    {
        var camera = new StudioScene { Name = "Caméra EECC", Source = SceneSourceKind.Camera };
        var color = new StudioScene { Name = "Fond régie", Source = SceneSourceKind.Color, ColorHex = "#1E3A8A" };
        return new SceneDocument
        {
            Scenes = new List<StudioScene> { camera, color },
            PreviewSceneId = camera.Id,
            ProgramSceneId = color.Id,
        };
    }

    public static void Save(string path, SceneDocument document)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(document));
    }

    public static SceneDocument LoadOrCreate(string path)
    {
        if (!File.Exists(path))
        {
            var created = CreateDefault();
            Save(path, created);
            return created;
        }
        return JsonSerializer.Deserialize<SceneDocument>(File.ReadAllText(path)) ?? CreateDefault();
    }
}

public sealed class ProgramDirector
{
    public ProgramDirector(SceneDocument document)
    {
        Document = document;
    }

    public SceneDocument Document { get; }
    public int DissolveStepsRemaining { get; private set; }
    private byte[]? _dissolveFrom;
    private int _dissolveTotal;

    public StudioScene? Preview => Find(Document.PreviewSceneId);
    public StudioScene? Program => Find(Document.ProgramSceneId);

    public void SelectPreview(string sceneId)
    {
        if (Find(sceneId) == null) return;
        Document.PreviewSceneId = sceneId;
    }

    public void Cut()
    {
        if (Document.PreviewSceneId == null) return;
        Document.ProgramSceneId = Document.PreviewSceneId;
        DissolveStepsRemaining = 0;
        _dissolveFrom = null;
    }

    public void BeginDissolve(byte[] currentProgramFrame, int steps)
    {
        if (steps < 1 || Document.PreviewSceneId == null) return;
        _dissolveFrom = currentProgramFrame.ToArray();
        _dissolveTotal = steps;
        DissolveStepsRemaining = steps;
        Document.ProgramSceneId = Document.PreviewSceneId;
    }

    public byte[] Render(StudioScene? scene, CameraFrame? camera, int width, int height)
    {
        var frame = new byte[width * height * 3];
        if (scene == null)
        {
            return frame;
        }
        if (scene.Source == SceneSourceKind.Color)
        {
            Fill(frame, ParseHex(scene.ColorHex));
            return frame;
        }
        if (camera == null || camera.Bgr.Length < camera.Stride * camera.Height || camera.Width < 1 || camera.Height < 1)
        {
            Fill(frame, (0x11, 0x11, 0x11));
            return frame;
        }
        BlitNearest(camera, frame, width, height);
        return frame;
    }

    public byte[] RenderProgram(CameraFrame? camera, int width, int height)
    {
        var target = Render(Program, camera, width, height);
        if (DissolveStepsRemaining <= 0 || _dissolveFrom == null || _dissolveFrom.Length != target.Length)
        {
            return target;
        }
        var done = _dissolveTotal - DissolveStepsRemaining + 1;
        var mixed = Blend(_dissolveFrom, target, done / (double)_dissolveTotal);
        DissolveStepsRemaining--;
        if (DissolveStepsRemaining == 0) _dissolveFrom = null;
        return mixed;
    }

    public static byte[] Blend(byte[] from, byte[] to, double amount)
    {
        var t = Math.Clamp(amount, 0, 1);
        var mixed = new byte[to.Length];
        for (var i = 0; i < to.Length; i++)
        {
            mixed[i] = (byte)(from[i] + (to[i] - from[i]) * t);
        }
        return mixed;
    }

    private StudioScene? Find(string? id) => Document.Scenes.FirstOrDefault(scene => scene.Id == id);

    public static (byte B, byte G, byte R) ParseHex(string hex)
    {
        var text = hex.Trim().TrimStart('#');
        if (text.Length != 6 || !int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return (0x80, 0x3A, 0x1E);
        }
        return ((byte)(rgb & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)((rgb >> 16) & 0xFF));
    }

    private static void Fill(byte[] frame, (byte B, byte G, byte R) color)
    {
        for (var i = 0; i < frame.Length; i += 3)
        {
            frame[i] = color.B;
            frame[i + 1] = color.G;
            frame[i + 2] = color.R;
        }
    }

    private static void BlitNearest(CameraFrame camera, byte[] destination, int width, int height)
    {
        for (var y = 0; y < height; y++)
        {
            var sourceY = y * camera.Height / height;
            for (var x = 0; x < width; x++)
            {
                var sourceX = x * camera.Width / width;
                var source = sourceY * camera.Stride + sourceX * 3;
                var target = (y * width + x) * 3;
                destination[target] = camera.Bgr[source];
                destination[target + 1] = camera.Bgr[source + 1];
                destination[target + 2] = camera.Bgr[source + 2];
            }
        }
    }
}

public sealed record CameraFrame(byte[] Bgr, int Width, int Height, int Stride);
