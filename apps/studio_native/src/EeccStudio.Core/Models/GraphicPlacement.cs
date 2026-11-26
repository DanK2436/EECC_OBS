namespace EeccStudio.Core.Models;

public static class GraphicPlacement
{
    public static bool IsGraphic(StudioSource source) =>
        source.Type is SourceType.Text or SourceType.BibleVerse or SourceType.ScrollingTicker or SourceType.Image;

    public static void Ensure(StudioSource source)
    {
        if (source.GraphicPlaced || !IsGraphic(source))
            return;

        source.GraphicPlaced = true;
        switch (source.Type)
        {
            case SourceType.ScrollingTicker:
                source.TextStyle = GraphicTextStyle.LowerThird;
                source.Motion = GraphicMotion.Scroll;
                source.Shape = GraphicShape.Rectangle;
                source.XPercent = 0;
                source.YPercent = 88;
                source.WidthPercent = 100;
                source.HeightPercent = 12;
                source.FontSize = source.FontSize <= 0 ? 16 : source.FontSize;
                break;
            case SourceType.Image:
                source.Shape = GraphicShape.Rounded;
                source.Motion = GraphicMotion.Still;
                source.XPercent = 78;
                source.YPercent = 4;
                source.WidthPercent = 18;
                source.HeightPercent = 16;
                break;
            case SourceType.BibleVerse:
                source.TextStyle = GraphicTextStyle.Verse;
                source.Motion = GraphicMotion.Fade;
                source.Shape = GraphicShape.Rounded;
                source.XPercent = 12;
                source.YPercent = 28;
                source.WidthPercent = 76;
                source.HeightPercent = 36;
                source.FontSize = source.FontSize <= 0 ? 22 : source.FontSize;
                break;
            default:
                source.TextStyle = GraphicTextStyle.LowerThird;
                source.Motion = GraphicMotion.Slide;
                source.Shape = GraphicShape.Rounded;
                source.XPercent = 3;
                source.YPercent = 68;
                source.WidthPercent = 52;
                source.HeightPercent = 18;
                source.FontSize = source.FontSize <= 0 ? 16 : source.FontSize;
                break;
        }
    }
}
