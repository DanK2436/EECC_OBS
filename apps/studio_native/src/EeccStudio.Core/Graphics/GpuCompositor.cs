using System;
using System.Threading.Tasks;

namespace EeccStudio.Core.Graphics
{
    public enum TransitionType
    {
        Cut,
        Fade,
        SlideLeft,
        SlideRight,
        SlideUp,
        SlideDown,
        FadeToBlack
    }

    public sealed class LowerThirdConfig
    {
        public bool IsVisible { get; set; } = false;
        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public byte AccentR { get; set; } = 37;   // #2563EB EECC Blue
        public byte AccentG { get; set; } = 99;
        public byte AccentB { get; set; } = 235;
        public float Opacity { get; set; } = 0.95f;
    }

    public sealed class ScriptureOverlayConfig
    {
        public bool IsVisible { get; set; } = false;
        public string Reference { get; set; } = ""; // ex: "Jean 3:16"
        public string VerseText { get; set; } = "";
        public string Version { get; set; } = "LSG 1910";
        public float Opacity { get; set; } = 0.92f;
    }

    public sealed class TickerConfig
    {
        public bool IsVisible { get; set; } = false;
        public string Message { get; set; } = "";
        public float ScrollPosition { get; set; } = 0f;
    }

    public sealed class LogoOverlayConfig
    {
        public bool IsVisible { get; set; } = true;
        public float Opacity { get; set; } = 0.85f;
        public int MarginX { get; set; } = 40;
        public int MarginY { get; set; } = 40;
        public int Width { get; set; } = 120;
        public int Height { get; set; } = 120;
    }

    public sealed class VideoFrameBuffer
    {
        public int Width { get; }
        public int Height { get; }
        public int Stride => Width * 4;
        public byte[] Data { get; }

        public VideoFrameBuffer(int width, int height)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            Data = new byte[Width * Height * 4];
        }

        public VideoFrameBuffer(int width, int height, byte[] existingData)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            Data = existingData ?? new byte[Width * Height * 4];
        }

        public void Clear(byte r = 0, byte g = 0, byte b = 0, byte a = 255)
        {
            int totalPixels = Width * Height;
            unsafe
            {
                fixed (byte* ptr = Data)
                {
                    uint color = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
                    uint* uPtr = (uint*)ptr;
                    for (int i = 0; i < totalPixels; i++)
                    {
                        uPtr[i] = color;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Moteur de composition graphique pour régie broadcast :
    /// - Canvas multi-résolutions (1080p, 1440p, 4K, 8K, 16K)
    /// - Multi-couches graphiques (Vidéo principale, PiP, Lower Thirds, Versets bibliques, Ticker, Logo)
    /// - Transitions vidéo broadcast (Cut, Cross-fade, Slide, FadeToBlack)
    /// </summary>
    public sealed class GpuCompositor
    {
        public CanvasPreset CurrentPreset { get; private set; } = CanvasPreset.FullHd1080p;
        public int CanvasWidth { get; private set; } = 1920;
        public int CanvasHeight { get; private set; } = 1080;
        public int CanvasFps { get; private set; } = 60;

        public LowerThirdConfig LowerThird { get; } = new();
        public ScriptureOverlayConfig Scripture { get; } = new();
        public TickerConfig Ticker { get; } = new();
        public LogoOverlayConfig Logo { get; } = new();

        public GpuCompositor(CanvasPreset preset = CanvasPreset.FullHd1080p)
        {
            SetResolution(preset);
        }

        public void SetResolution(CanvasPreset preset, int? customFps = null)
        {
            CurrentPreset = preset;
            var (w, h, defFps) = CanvasResolution.GetDimensions(preset);
            CanvasWidth = w;
            CanvasHeight = h;
            CanvasFps = customFps ?? defFps;
        }

        public GpuBudget GetBudget() => CanvasResolution.CalculateBudget(CurrentPreset, CanvasFps);

        /// <summary>
        /// Compose une transition entre deux trames de scènes (Scène A -> Scène B).
        /// </summary>
        public VideoFrameBuffer CompositeTransition(
            VideoFrameBuffer sourceA,
            VideoFrameBuffer sourceB,
            TransitionType transition,
            float progress)
        {
            progress = Math.Clamp(progress, 0.0f, 1.0f);
            var output = new VideoFrameBuffer(CanvasWidth, CanvasHeight);

            if (transition == TransitionType.Cut || progress <= 0.001f)
            {
                CopyOrScale(sourceA, output);
                ApplyOverlays(output);
                return output;
            }

            if (progress >= 0.999f && transition != TransitionType.FadeToBlack)
            {
                CopyOrScale(sourceB, output);
                ApplyOverlays(output);
                return output;
            }

            float eased = AnimationEasings.CubicEaseInOut(progress);

            switch (transition)
            {
                case TransitionType.Fade:
                    BlendCrossFade(sourceA, sourceB, output, eased);
                    break;

                case TransitionType.SlideLeft:
                    BlendSlideHorizontal(sourceA, sourceB, output, eased, toLeft: true);
                    break;

                case TransitionType.SlideRight:
                    BlendSlideHorizontal(sourceA, sourceB, output, eased, toLeft: false);
                    break;

                case TransitionType.SlideUp:
                    BlendSlideVertical(sourceA, sourceB, output, eased, toUp: true);
                    break;

                case TransitionType.SlideDown:
                    BlendSlideVertical(sourceA, sourceB, output, eased, toUp: false);
                    break;

                case TransitionType.FadeToBlack:
                    BlendFadeToBlack(sourceA, output, eased);
                    break;

                default:
                    CopyOrScale(sourceB, output);
                    break;
            }

            ApplyOverlays(output);
            return output;
        }

        /// <summary>
        /// Compose une scène unique avec l'ensemble de ses calques (Vidéo, PiP, Lower Third, Versets, Logo, Ticker).
        /// </summary>
        public VideoFrameBuffer CompositeScene(
            VideoFrameBuffer mainSource,
            VideoFrameBuffer? pipSource = null,
            float pipX = 0.72f,
            float pipY = 0.05f,
            float pipScale = 0.25f)
        {
            var output = new VideoFrameBuffer(CanvasWidth, CanvasHeight);
            CopyOrScale(mainSource, output);

            if (pipSource != null)
            {
                int pipW = (int)(CanvasWidth * pipScale);
                int pipH = (int)(CanvasHeight * pipScale);
                int startX = (int)(CanvasWidth * pipX);
                int startY = (int)(CanvasHeight * pipY);
                RenderPip(pipSource, output, startX, startY, pipW, pipH);
            }

            ApplyOverlays(output);
            return output;
        }

        private static void BlendCrossFade(VideoFrameBuffer a, VideoFrameBuffer b, VideoFrameBuffer dest, float t)
        {
            int w = dest.Width;
            int h = dest.Height;
            int totalPixels = w * h;
            int factorB = (int)(t * 256.0f);
            int factorA = 256 - factorB;

            unsafe
            {
                fixed (byte* pA = a.Data, pB = b.Data, pD = dest.Data)
                {
                    byte* rawA = pA;
                    byte* rawB = pB;
                    byte* rawD = pD;
                    Parallel.For(0, h, y =>
                    {
                        int rowOffset = y * w * 4;
                        byte* rowA = rawA + rowOffset;
                        byte* rowB = rawB + rowOffset;
                        byte* rowD = rawD + rowOffset;

                        for (int x = 0; x < w * 4; x++)
                        {
                            rowD[x] = (byte)((rowA[x] * factorA + rowB[x] * factorB) >> 8);
                        }
                    });
                }
            }
        }

        private static void BlendSlideHorizontal(VideoFrameBuffer a, VideoFrameBuffer b, VideoFrameBuffer dest, float t, bool toLeft)
        {
            int w = dest.Width;
            int h = dest.Height;
            int splitX = (int)(w * t);

            Parallel.For(0, h, y =>
            {
                int rowOffset = y * w * 4;
                if (toLeft)
                {
                    // A glisse vers la gauche, B entre par la droite
                    int remainingA = w - splitX;
                    if (remainingA > 0)
                    {
                        Buffer.BlockCopy(a.Data, rowOffset + splitX * 4, dest.Data, rowOffset, remainingA * 4);
                    }
                    if (splitX > 0)
                    {
                        Buffer.BlockCopy(b.Data, rowOffset, dest.Data, rowOffset + remainingA * 4, splitX * 4);
                    }
                }
                else
                {
                    // A glisse vers la droite, B entre par la gauche
                    if (splitX > 0)
                    {
                        Buffer.BlockCopy(b.Data, rowOffset + (w - splitX) * 4, dest.Data, rowOffset, splitX * 4);
                    }
                    int remainingA = w - splitX;
                    if (remainingA > 0)
                    {
                        Buffer.BlockCopy(a.Data, rowOffset, dest.Data, rowOffset + splitX * 4, remainingA * 4);
                    }
                }
            });
        }

        private static void BlendSlideVertical(VideoFrameBuffer a, VideoFrameBuffer b, VideoFrameBuffer dest, float t, bool toUp)
        {
            int w = dest.Width;
            int h = dest.Height;
            int splitY = (int)(h * t);
            int stride = w * 4;

            if (toUp)
            {
                int remainingA = h - splitY;
                if (remainingA > 0)
                {
                    Buffer.BlockCopy(a.Data, splitY * stride, dest.Data, 0, remainingA * stride);
                }
                if (splitY > 0)
                {
                    Buffer.BlockCopy(b.Data, 0, dest.Data, remainingA * stride, splitY * stride);
                }
            }
            else
            {
                if (splitY > 0)
                {
                    Buffer.BlockCopy(b.Data, (h - splitY) * stride, dest.Data, 0, splitY * stride);
                }
                int remainingA = h - splitY;
                if (remainingA > 0)
                {
                    Buffer.BlockCopy(a.Data, 0, dest.Data, splitY * stride, remainingA * stride);
                }
            }
        }

        private static void BlendFadeToBlack(VideoFrameBuffer a, VideoFrameBuffer dest, float t)
        {
            int w = dest.Width;
            int h = dest.Height;
            int factor = (int)((1.0f - t) * 256.0f);

            unsafe
            {
                fixed (byte* pA = a.Data, pD = dest.Data)
                {
                    byte* rawA = pA;
                    byte* rawD = pD;
                    Parallel.For(0, h, y =>
                    {
                        int rowOffset = y * w * 4;
                        byte* rowA = rawA + rowOffset;
                        byte* rowD = rawD + rowOffset;

                        for (int x = 0; x < w * 4; x += 4)
                        {
                            rowD[x] = (byte)((rowA[x] * factor) >> 8);         // B
                            rowD[x + 1] = (byte)((rowA[x + 1] * factor) >> 8); // G
                            rowD[x + 2] = (byte)((rowA[x + 2] * factor) >> 8); // R
                            rowD[x + 3] = 255;                                 // A
                        }
                    });
                }
            }
        }

        private static void RenderPip(VideoFrameBuffer pipSource, VideoFrameBuffer dest, int startX, int startY, int pipW, int pipH)
        {
            // Dessin bordure blanche PiP (2 px)
            int borderWidth = 2;
            int bStartX = Math.Max(0, startX - borderWidth);
            int bStartY = Math.Max(0, startY - borderWidth);
            int bEndX = Math.Min(dest.Width, startX + pipW + borderWidth);
            int bEndY = Math.Min(dest.Height, startY + pipH + borderWidth);

            for (int y = bStartY; y < bEndY; y++)
            {
                for (int x = bStartX; x < bEndX; x++)
                {
                    bool isBorder = x < startX || x >= startX + pipW || y < startY || y >= startY + pipH;
                    if (isBorder)
                    {
                        int idx = (y * dest.Width + x) * 4;
                        dest.Data[idx] = 240;     // B
                        dest.Data[idx + 1] = 240; // G
                        dest.Data[idx + 2] = 240; // R
                        dest.Data[idx + 3] = 255; // A
                    }
                }
            }

            // Échantillonnage de la source PiP
            Parallel.For(0, pipH, y =>
            {
                int destY = startY + y;
                if (destY >= dest.Height) return;

                int srcY = (y * pipSource.Height) / pipH;
                int srcRow = srcY * pipSource.Width * 4;
                int destRow = destY * dest.Width * 4;

                for (int x = 0; x < pipW; x++)
                {
                    int destX = startX + x;
                    if (destX >= dest.Width) break;

                    int srcX = (x * pipSource.Width) / pipW;
                    int srcIdx = srcRow + srcX * 4;
                    int destIdx = destRow + destX * 4;

                    dest.Data[destIdx] = pipSource.Data[srcIdx];
                    dest.Data[destIdx + 1] = pipSource.Data[srcIdx + 1];
                    dest.Data[destIdx + 2] = pipSource.Data[srcIdx + 2];
                    dest.Data[destIdx + 3] = 255;
                }
            });
        }

        private void ApplyOverlays(VideoFrameBuffer frame)
        {
            // 1. Lower Third (Bandeau de nom/titre en bas à gauche)
            if (LowerThird.IsVisible && !string.IsNullOrWhiteSpace(LowerThird.Title))
            {
                RenderLowerThirdBanner(frame);
            }

            // 2. Scripture Overlay (Verset biblique centré ou en tiers inférieur)
            if (Scripture.IsVisible && !string.IsNullOrWhiteSpace(Scripture.Reference))
            {
                RenderScriptureBanner(frame);
            }

            // 3. Ticker (Bande défilante d'annonces en bas d'écran)
            if (Ticker.IsVisible && !string.IsNullOrWhiteSpace(Ticker.Message))
            {
                RenderTickerBanner(frame);
            }

            // 4. Logo EECC (Watermark discret en haut à droite)
            if (Logo.IsVisible)
            {
                RenderLogoWatermark(frame);
            }
        }

        private void RenderLowerThirdBanner(VideoFrameBuffer frame)
        {
            int h = (int)(frame.Height * 0.12f); // Hauteur bandeau = 12% du canevas
            int w = (int)(frame.Width * 0.45f);  // Largeur bandeau = 45% du canevas
            int startX = (int)(frame.Width * 0.05f);
            int startY = (int)(frame.Height * 0.82f);

            int endX = Math.Min(frame.Width, startX + w);
            int endY = Math.Min(frame.Height, startY + h);

            int alpha = (int)(LowerThird.Opacity * 255);
            int invAlpha = 255 - alpha;

            Parallel.For(startY, endY, y =>
            {
                for (int x = startX; x < endX; x++)
                {
                    int idx = (y * frame.Width + x) * 4;
                    // Liseré vertical d'accent (6 px à gauche)
                    bool isAccentBar = (x - startX) < 8;

                    byte targetR = isAccentBar ? LowerThird.AccentR : (byte)15;
                    byte targetG = isAccentBar ? LowerThird.AccentG : (byte)23;
                    byte targetB = isAccentBar ? LowerThird.AccentB : (byte)42; // #0F172A Dark Slate

                    frame.Data[idx] = (byte)((frame.Data[idx] * invAlpha + targetB * alpha) >> 8);
                    frame.Data[idx + 1] = (byte)((frame.Data[idx + 1] * invAlpha + targetG * alpha) >> 8);
                    frame.Data[idx + 2] = (byte)((frame.Data[idx + 2] * invAlpha + targetR * alpha) >> 8);
                }
            });
        }

        private void RenderScriptureBanner(VideoFrameBuffer frame)
        {
            int h = (int)(frame.Height * 0.18f); // 18% hauteur
            int w = (int)(frame.Width * 0.80f);  // 80% largeur
            int startX = (frame.Width - w) / 2;
            int startY = (int)(frame.Height * 0.76f);

            int endX = startX + w;
            int endY = startY + h;

            int alpha = (int)(Scripture.Opacity * 255);
            int invAlpha = 255 - alpha;

            Parallel.For(startY, endY, y =>
            {
                for (int x = startX; x < endX; x++)
                {
                    int idx = (y * frame.Width + x) * 4;
                    // Bordure or/ambre en haut du bandeau (3 px)
                    bool isGoldBorder = (y - startY) < 4;

                    byte targetR = isGoldBorder ? (byte)217 : (byte)17;  // #D97706 or #111827
                    byte targetG = isGoldBorder ? (byte)119 : (byte)24;
                    byte targetB = isGoldBorder ? (byte)6 : (byte)39;

                    frame.Data[idx] = (byte)((frame.Data[idx] * invAlpha + targetB * alpha) >> 8);
                    frame.Data[idx + 1] = (byte)((frame.Data[idx + 1] * invAlpha + targetG * alpha) >> 8);
                    frame.Data[idx + 2] = (byte)((frame.Data[idx + 2] * invAlpha + targetR * alpha) >> 8);
                }
            });
        }

        private void RenderTickerBanner(VideoFrameBuffer frame)
        {
            int h = (int)(frame.Height * 0.05f); // 5% bas
            int startY = frame.Height - h;

            Parallel.For(startY, frame.Height, y =>
            {
                for (int x = 0; x < frame.Width; x++)
                {
                    int idx = (y * frame.Width + x) * 4;
                    // Fond noir translucide pour le bandeau d'alertes/annonces
                    frame.Data[idx] = (byte)(frame.Data[idx] * 0.2f);
                    frame.Data[idx + 1] = (byte)(frame.Data[idx + 1] * 0.2f);
                    frame.Data[idx + 2] = (byte)(frame.Data[idx + 2] * 0.2f);
                }
            });
        }

        private void RenderLogoWatermark(VideoFrameBuffer frame)
        {
            int logoW = Logo.Width;
            int logoH = Logo.Height;
            int startX = frame.Width - logoW - Logo.MarginX;
            int startY = Logo.MarginY;

            if (startX < 0 || startY + logoH >= frame.Height) return;

            int alpha = (int)(Logo.Opacity * 255);
            int invAlpha = 255 - alpha;

            // Pastille logo circulaire stylisée EECC
            int radius = logoW / 2;
            int centerX = startX + radius;
            int centerY = startY + radius;
            int radiusSq = radius * radius;

            Parallel.For(startY, startY + logoH, y =>
            {
                for (int x = startX; x < startX + logoW; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    if (dx * dx + dy * dy <= radiusSq)
                    {
                        int idx = (y * frame.Width + x) * 4;
                        byte logoR = 37;
                        byte logoG = 99;
                        byte logoB = 235; // EECC Royal Blue

                        frame.Data[idx] = (byte)((frame.Data[idx] * invAlpha + logoB * alpha) >> 8);
                        frame.Data[idx + 1] = (byte)((frame.Data[idx + 1] * invAlpha + logoG * alpha) >> 8);
                        frame.Data[idx + 2] = (byte)((frame.Data[idx + 2] * invAlpha + logoR * alpha) >> 8);
                    }
                }
            });
        }

        private static void CopyOrScale(VideoFrameBuffer src, VideoFrameBuffer dest)
        {
            if (src.Width == dest.Width && src.Height == dest.Height)
            {
                Buffer.BlockCopy(src.Data, 0, dest.Data, 0, Math.Min(src.Data.Length, dest.Data.Length));
                return;
            }

            // Bilinear ou Nearest Neighbor scaling haute vitesse
            Parallel.For(0, dest.Height, y =>
            {
                int srcY = (y * src.Height) / dest.Height;
                int srcRow = srcY * src.Width * 4;
                int destRow = y * dest.Width * 4;

                for (int x = 0; x < dest.Width; x++)
                {
                    int srcX = (x * src.Width) / dest.Width;
                    int srcIdx = srcRow + srcX * 4;
                    int destIdx = destRow + x * 4;

                    dest.Data[destIdx] = src.Data[srcIdx];
                    dest.Data[destIdx + 1] = src.Data[srcIdx + 1];
                    dest.Data[destIdx + 2] = src.Data[srcIdx + 2];
                    dest.Data[destIdx + 3] = src.Data[srcIdx + 3];
                }
            });
        }
    }
}
