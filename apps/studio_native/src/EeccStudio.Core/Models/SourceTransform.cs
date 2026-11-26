namespace EeccStudio.Core.Models;

/// <summary>
/// Transformations d'un élément de scène, calquées sur le comportement d'OBS :
/// la rotation s'additionne, le miroir inverse le signe de l'échelle, une source verrouillée ne bouge pas.
/// </summary>
public static class SourceTransform
{
    public static int Normalize(int degrees)
    {
        var wrapped = degrees % 360;
        return wrapped < 0 ? wrapped + 360 : wrapped;
    }

    public static bool Rotate(StudioSource source, int deltaDegrees)
    {
        if (source.IsLocked)
            return false;
        source.RotationAngle = Normalize(source.RotationAngle + deltaDegrees);
        return true;
    }

    public static bool FlipHorizontal(StudioSource source)
    {
        if (source.IsLocked)
            return false;
        source.FlipHorizontal = !source.FlipHorizontal;
        return true;
    }

    public static bool FlipVertical(StudioSource source)
    {
        if (source.IsLocked)
            return false;
        source.FlipVertical = !source.FlipVertical;
        return true;
    }

    public static bool Reset(StudioSource source)
    {
        if (source.IsLocked)
            return false;
        source.RotationAngle = 0;
        source.FlipHorizontal = false;
        source.FlipVertical = false;
        source.Opacity = 1.0;
        return true;
    }

    public readonly record struct Snapshot(int Rotation, bool FlipH, bool FlipV, double Opacity);

    public static Snapshot Capture(StudioSource source) =>
        new(source.RotationAngle, source.FlipHorizontal, source.FlipVertical, source.Opacity);

    public static void Restore(StudioSource source, Snapshot snapshot)
    {
        source.RotationAngle = Normalize(snapshot.Rotation);
        source.FlipHorizontal = snapshot.FlipH;
        source.FlipVertical = snapshot.FlipV;
        source.Opacity = snapshot.Opacity;
    }
}
