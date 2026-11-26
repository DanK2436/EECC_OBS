using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EeccStudio.Core.Models;

namespace EeccStudio;

public sealed class OverlayPlayback
{
    private readonly List<DispatcherTimer> _timers = [];

    public void Stop()
    {
        foreach (var timer in _timers)
            timer.Stop();
        _timers.Clear();
    }

    public void Paint(Canvas canvas, IEnumerable<StudioSource> sources)
    {
        Stop();
        canvas.Children.Clear();
        var width = canvas.ActualWidth;
        var height = canvas.ActualHeight;
        if (width < 8 || height < 8)
            return;

        foreach (var source in sources)
        {
            if (!source.IsVisible || !GraphicPlacement.IsGraphic(source))
                continue;
            GraphicPlacement.Ensure(source);
            var box = Build(source, width, height);
            canvas.Children.Add(box);
            Play(source, box);
        }
    }

    private static FrameworkElement Build(StudioSource source, double canvasWidth, double canvasHeight)
    {
        var boxWidth = Math.Max(24, canvasWidth * source.WidthPercent / 100);
        var boxHeight = Math.Max(18, canvasHeight * source.HeightPercent / 100);
        var host = new Border
        {
            Width = boxWidth,
            Height = boxHeight,
            Opacity = Math.Clamp(source.Opacity, 0.05, 1),
            ClipToBounds = source.Motion == GraphicMotion.Scroll,
            CornerRadius = Radius(source.Shape, boxWidth, boxHeight),
            Background = BrushFor(source),
            BorderBrush = BorderFor(source),
            BorderThickness = source.TextStyle == GraphicTextStyle.LowerThird && source.Type != SourceType.Image
                ? new Thickness(4, 0, 0, 0)
                : new Thickness(0),
            Padding = source.Type == SourceType.Image ? new Thickness(0) : new Thickness(10, 6, 10, 6),
            SnapsToDevicePixels = true,
        };
        Canvas.SetLeft(host, canvasWidth * source.XPercent / 100);
        Canvas.SetTop(host, canvasHeight * source.YPercent / 100);

        if (source.Type == SourceType.Image)
        {
            host.Child = ImageOf(source);
            if (source.Shape == GraphicShape.Circle)
                host.Clip = new EllipseGeometry(new Point(boxWidth / 2, boxHeight / 2), boxWidth / 2, boxHeight / 2);
            return host;
        }

        var title = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(source.TextContent) ? source.Name : source.TextContent,
            FontFamily = FontFor(source.TextStyle),
            FontSize = source.FontSize > 0 ? source.FontSize : DefaultSize(source.TextStyle),
            FontWeight = source.TextStyle == GraphicTextStyle.Verse ? FontWeights.Normal : FontWeights.SemiBold,
            FontStyle = source.TextStyle == GraphicTextStyle.Verse ? FontStyles.Italic : FontStyles.Normal,
            Foreground = ForegroundFor(source.TextStyle),
            TextWrapping = source.Motion == GraphicMotion.Scroll ? TextWrapping.NoWrap : TextWrapping.Wrap,
            TextAlignment = source.TextStyle == GraphicTextStyle.Verse ? TextAlignment.Center : TextAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (source.TextStyle is GraphicTextStyle.Outline or GraphicTextStyle.Title)
        {
            title.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = source.TextStyle == GraphicTextStyle.Outline ? 2 : 8,
                ShadowDepth = 0,
                Opacity = 0.9,
            };
        }

        if (source.Motion == GraphicMotion.Scroll)
        {
            var strip = new Canvas { Height = boxHeight, ClipToBounds = false };
            Canvas.SetTop(title, 4);
            strip.Children.Add(title);
            host.Child = strip;
            host.Tag = title;
            return host;
        }

        if (string.IsNullOrWhiteSpace(source.SecondaryText))
        {
            host.Child = title;
            host.Tag = title;
            return host;
        }

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var sub = new TextBlock
        {
            Text = source.SecondaryText,
            FontFamily = title.FontFamily,
            FontSize = Math.Max(11, title.FontSize - 4),
            Foreground = source.TextStyle == GraphicTextStyle.Glass
                ? new SolidColorBrush(Color.FromRgb(0x37, 0x41, 0x51))
                : new SolidColorBrush(Color.FromRgb(0xC9, 0xD1, 0xD9)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0),
        };
        stack.Children.Add(title);
        stack.Children.Add(sub);
        host.Child = stack;
        host.Tag = title;
        return host;
    }

    private void Play(StudioSource source, FrameworkElement host)
    {
        switch (source.Motion)
        {
            case GraphicMotion.Scroll:
                Scroll(source, host);
                break;
            case GraphicMotion.Fade:
                AnimateOpacity(host, 0, 1, 1.4, autoReverse: true);
                break;
            case GraphicMotion.Slide:
                host.RenderTransform = new TranslateTransform(-Math.Min(80, host.Width * 0.45), 0);
                host.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, host.Opacity, TimeSpan.FromMilliseconds(420)));
                ((TranslateTransform)host.RenderTransform).BeginAnimation(
                    TranslateTransform.XProperty,
                    new DoubleAnimation(0, TimeSpan.FromMilliseconds(520)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                break;
            case GraphicMotion.Type:
                TypeOn(source, host);
                break;
            case GraphicMotion.Pulse:
                AnimateOpacity(host, 0.35, host.Opacity, 0.7, autoReverse: true);
                break;
        }
    }

    private static void Scroll(StudioSource source, FrameworkElement host)
    {
        if (host.Tag is not TextBlock title)
            return;
        var length = Math.Max(host.Width + 40, title.Text.Length * title.FontSize * 0.55);
        title.Width = length;
        var seconds = Math.Max(6, (length + host.Width) / Math.Max(20, source.ScrollSpeed));
        title.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation
        {
            From = host.Width,
            To = -length,
            Duration = TimeSpan.FromSeconds(seconds),
            RepeatBehavior = RepeatBehavior.Forever,
        });
    }

    private void TypeOn(StudioSource source, FrameworkElement host)
    {
        if (host.Tag is not TextBlock title)
            return;
        var full = title.Text;
        var index = 0;
        title.Text = "";
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(42) };
        timer.Tick += (_, _) =>
        {
            if (index < full.Length)
            {
                index++;
                title.Text = full[..index];
            }
            else if (index < full.Length + 28)
            {
                index++;
            }
            else
            {
                index = 0;
                title.Text = "";
            }
        };
        timer.Start();
        _timers.Add(timer);
    }

    private static void AnimateOpacity(FrameworkElement host, double from, double to, double seconds, bool autoReverse)
    {
        host.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromSeconds(seconds),
            AutoReverse = autoReverse,
            RepeatBehavior = RepeatBehavior.Forever,
        });
    }

    private static CornerRadius Radius(GraphicShape shape, double width, double height) => shape switch
    {
        GraphicShape.Rounded => new CornerRadius(12),
        GraphicShape.Circle => new CornerRadius(Math.Min(width, height) / 2),
        GraphicShape.Pill => new CornerRadius(height / 2),
        _ => new CornerRadius(0),
    };

    private static Brush BrushFor(StudioSource source)
    {
        if (source.Type == SourceType.Image)
            return Brushes.Transparent;
        return source.TextStyle switch
        {
            GraphicTextStyle.LowerThird => new SolidColorBrush(Color.FromArgb(0xE5, 0x16, 0x1B, 0x22)),
            GraphicTextStyle.Verse => new SolidColorBrush(Color.FromArgb(0xD0, 0x1A, 0x14, 0x08)),
            GraphicTextStyle.Glass => new SolidColorBrush(Color.FromArgb(0xCC, 0xF4, 0xF7, 0xFB)),
            _ => Brushes.Transparent,
        };
    }

    private static Brush BorderFor(StudioSource source)
    {
        if (source.Type == SourceType.Image)
            return Brushes.Transparent;
        return source.TextStyle == GraphicTextStyle.LowerThird
            ? new SolidColorBrush(Color.FromRgb(0x2C, 0x7B, 0xFE))
            : Brushes.Transparent;
    }

    private static Brush ForegroundFor(GraphicTextStyle style) => style switch
    {
        GraphicTextStyle.Verse => new SolidColorBrush(Color.FromRgb(0xF5, 0xD5, 0x8A)),
        GraphicTextStyle.Glass => new SolidColorBrush(Color.FromRgb(0x11, 0x18, 0x27)),
        _ => Brushes.White,
    };

    private static FontFamily FontFor(GraphicTextStyle style) =>
        style == GraphicTextStyle.Verse ? new FontFamily("Georgia") : new FontFamily("Segoe UI");

    private static double DefaultSize(GraphicTextStyle style) => style switch
    {
        GraphicTextStyle.Title => 32,
        GraphicTextStyle.Verse => 20,
        GraphicTextStyle.Outline => 26,
        _ => 16,
    };

    private static UIElement ImageOf(StudioSource source)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(source.ImagePath) && File.Exists(source.ImagePath))
            {
                var image = new Image
                {
                    Stretch = Stretch.Uniform,
                    Source = new BitmapImage(new Uri(Path.GetFullPath(source.ImagePath))),
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                return image;
            }
        }
        catch
        {
            // Le fichier illisible reste un cadre nommé.
        }

        return new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(source.Name) ? "Image" : source.Name,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };
    }
}
