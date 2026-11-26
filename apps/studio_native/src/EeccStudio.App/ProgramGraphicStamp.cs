using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using EeccStudio.Core.Models;

namespace EeccStudio;

public static class ProgramGraphicStamp
{
    private static GraphicCard[] _cards = [];

    public static void Use(IEnumerable<StudioSource> sources)
    {
        _cards = sources
            .Where(source => source.IsVisible && GraphicPlacement.IsGraphic(source))
            .Select(source =>
            {
                GraphicPlacement.Ensure(source);
                return GraphicCard.From(source);
            })
            .ToArray();
    }

    public static byte[]? Apply(byte[] jpeg)
    {
        var cards = _cards;
        if (cards.Length == 0 || jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
            return jpeg;

        using var input = new MemoryStream(jpeg);
        using var image = Image.FromStream(input);
        using var bitmap = new Bitmap(image.Width, image.Height);
        using (var canvas = Graphics.FromImage(bitmap))
        {
            canvas.SmoothingMode = SmoothingMode.AntiAlias;
            canvas.DrawImage(image, 0, 0, bitmap.Width, bitmap.Height);
            foreach (var card in cards)
                Draw(canvas, bitmap.Width, bitmap.Height, card);
        }

        using var output = new MemoryStream();
        bitmap.Save(output, ImageFormat.Jpeg);
        return output.ToArray();
    }

    private static void Draw(Graphics canvas, int width, int height, GraphicCard card)
    {
        var box = new RectangleF(
            width * card.X / 100f,
            height * card.Y / 100f,
            Math.Max(8, width * card.W / 100f),
            Math.Max(8, height * card.H / 100f));
        var seconds = (Environment.TickCount64 - card.Born) / 1000f;
        var alpha = MotionAlpha(card.Motion, seconds);
        if (card.Motion == GraphicMotion.Slide)
            box.X += (1f - Math.Min(1f, seconds / 0.5f)) * -box.Width * 0.4f;

        using var path = Shape(card.Shape, box);
        using var back = new SolidBrush(Color.FromArgb((int)(alpha * card.Background.A), card.Background));
        canvas.FillPath(back, path);
        if (card.ImagePath.Length > 0 && File.Exists(card.ImagePath))
        {
            canvas.SetClip(path);
            using var photo = Image.FromFile(card.ImagePath);
            canvas.DrawImage(photo, box);
            canvas.ResetClip();
            return;
        }

        var shown = ShownText(card, seconds);
        using var font = new Font(card.Verse ? "Georgia" : "Segoe UI", card.FontSize, card.Verse ? FontStyle.Italic : FontStyle.Bold);
        using var brush = new SolidBrush(Color.FromArgb((int)(alpha * 255), card.Foreground));
        var format = new StringFormat
        {
            Alignment = card.Verse ? StringAlignment.Center : StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisWord,
        };
        if (card.Motion == GraphicMotion.Scroll)
        {
            var size = canvas.MeasureString(shown, font);
            var travel = box.Width + size.Width;
            var shift = travel - (seconds * card.Speed % travel);
            canvas.SetClip(path);
            canvas.DrawString(shown, font, brush, box.Left + shift - size.Width, box.Top + (box.Height - size.Height) / 2);
            canvas.ResetClip();
            return;
        }

        canvas.DrawString(shown, font, brush, box, format);
        if (card.Secondary.Length > 0 && card.Motion != GraphicMotion.Type)
        {
            using var small = new Font(font.FontFamily, Math.Max(10, card.FontSize - 4), FontStyle.Regular);
            var second = new RectangleF(box.X + 8, box.Bottom - small.Height - 6, box.Width - 16, small.Height + 4);
            canvas.DrawString(card.Secondary, small, brush, second);
        }
    }

    private static string ShownText(GraphicCard card, float seconds)
    {
        if (card.Motion != GraphicMotion.Type || card.Text.Length == 0)
            return card.Text;
        var cycle = card.Text.Length + 28;
        var index = (int)(seconds / 0.042f) % cycle;
        return card.Text[..Math.Min(card.Text.Length, index)];
    }

    private static float MotionAlpha(GraphicMotion motion, float seconds) => motion switch
    {
        GraphicMotion.Fade => 0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(seconds * 2.2f)),
        GraphicMotion.Pulse => 0.4f + 0.6f * (0.5f + 0.5f * MathF.Sin(seconds * 4f)),
        GraphicMotion.Slide => Math.Min(1f, seconds / 0.4f),
        _ => 1f,
    };

    private static GraphicsPath Shape(GraphicShape shape, RectangleF box)
    {
        var path = new GraphicsPath();
        var radius = shape switch
        {
            GraphicShape.Rounded => 16f,
            GraphicShape.Circle => Math.Min(box.Width, box.Height) / 2f,
            GraphicShape.Pill => box.Height / 2f,
            _ => 0f,
        };
        if (radius <= 1)
        {
            path.AddRectangle(box);
            return path;
        }

        var diameter = radius * 2;
        path.AddArc(box.Left, box.Top, diameter, diameter, 180, 90);
        path.AddArc(box.Right - diameter, box.Top, diameter, diameter, 270, 90);
        path.AddArc(box.Right - diameter, box.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(box.Left, box.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed record GraphicCard(
        string Text,
        string Secondary,
        string ImagePath,
        GraphicTextStyle Style,
        GraphicMotion Motion,
        GraphicShape Shape,
        float X,
        float Y,
        float W,
        float H,
        float FontSize,
        float Speed,
        bool Verse,
        Color Foreground,
        Color Background,
        long Born)
    {
        public static GraphicCard From(StudioSource source)
        {
            var style = source.TextStyle;
            var verse = style == GraphicTextStyle.Verse;
            var foreground = style switch
            {
                GraphicTextStyle.Verse => Color.FromArgb(245, 213, 138),
                GraphicTextStyle.Glass => Color.FromArgb(17, 24, 39),
                _ => Color.White,
            };
            var background = source.Type == SourceType.Image
                ? Color.FromArgb(0, 0, 0, 0)
                : style switch
                {
                    GraphicTextStyle.LowerThird => Color.FromArgb(230, 22, 27, 34),
                    GraphicTextStyle.Verse => Color.FromArgb(210, 26, 20, 8),
                    GraphicTextStyle.Glass => Color.FromArgb(210, 244, 247, 251),
                    _ => Color.FromArgb(0, 0, 0, 0),
                };
            return new GraphicCard(
                string.IsNullOrWhiteSpace(source.TextContent) ? source.Name : source.TextContent,
                source.SecondaryText ?? "",
                source.ImagePath ?? "",
                style,
                source.Motion,
                source.Shape,
                (float)source.XPercent,
                (float)source.YPercent,
                (float)source.WidthPercent,
                (float)source.HeightPercent,
                source.FontSize > 0 ? (float)source.FontSize : style == GraphicTextStyle.Title ? 32 : 18,
                (float)Math.Max(20, source.ScrollSpeed),
                verse,
                foreground,
                background,
                Environment.TickCount64);
        }
    }
}
