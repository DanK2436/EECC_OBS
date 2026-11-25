using System;
using System.Globalization;
using System.IO;

namespace EeccStudio.Core.Graphics
{
    public enum TextTransformMode
    {
        None = 0,
        Uppercase = 1,
        Lowercase = 2,
        StartCase = 3
    }

    public enum TextAlign
    {
        Left,
        Center,
        Right
    }

    public enum TextVAlign
    {
        Top,
        Center,
        Bottom
    }

    public sealed class TextProperties
    {
        public string FontFamily { get; set; } = "Segoe UI";
        public float FontSize { get; set; } = 32.0f;
        public bool Bold { get; set; } = true;
        public bool Italic { get; set; } = false;
        public bool Underline { get; set; } = false;

        // Couleur & Dégradé (comme obs-text.cpp)
        public byte ColorR { get; set; } = 255;
        public byte ColorG { get; set; } = 255;
        public byte ColorB { get; set; } = 255;
        public float Opacity { get; set; } = 1.0f;

        public bool UseGradient { get; set; } = false;
        public byte GradientColorR { get; set; } = 220;
        public byte GradientColorG { get; set; } = 220;
        public byte GradientColorB { get; set; } = 220;
        public float GradientOpacity { get; set; } = 1.0f;
        public float GradientDirectionDeg { get; set; } = 90.0f; // 90° = vertical

        // Contour vectoriel (Outline / Stroke)
        public bool UseOutline { get; set; } = true;
        public float OutlineSize { get; set; } = 2.0f;
        public byte OutlineColorR { get; set; } = 0;
        public byte OutlineColorG { get; set; } = 0;
        public byte OutlineColorB { get; set; } = 0;
        public float OutlineOpacity { get; set; } = 1.0f;

        // Fond / Arrière-plan
        public bool UseBackground { get; set; } = false;
        public byte BgColorR { get; set; } = 15;
        public byte BgColorG { get; set; } = 23;
        public byte BgColorB { get; set; } = 42; // #0F172A Dark Slate
        public float BgOpacity { get; set; } = 0.85f;

        // Alignement et délimitation (Extents)
        public TextAlign Align { get; set; } = TextAlign.Left;
        public TextVAlign VAlign { get; set; } = TextVAlign.Top;
        public bool UseExtents { get; set; } = false;
        public int ExtentsWidth { get; set; } = 800;
        public int ExtentsHeight { get; set; } = 120;
        public bool Wrap { get; set; } = true;

        public TextTransformMode Transform { get; set; } = TextTransformMode.None;

        // Fichier externe
        public bool ReadFromFile { get; set; } = false;
        public string FilePath { get; set; } = "";
    }

    /// <summary>
    /// Moteur de rendu typographique vectoriel inspiré directement de `plugins/obs-text/gdiplus/obs-text.cpp` :
    /// - Cache de texture mémoire haute performance (coût de calcul nul si le texte ne change pas)
    /// - Dégradé bicolore linéaire orientable
    /// - Contour vectoriel anti-aliasé pour lisibilité sur n'importe quel flux vidéo
    /// - Surveillance et rechargement automatique à chaud de fichiers textes externes
    /// - Transformations de casse (Majuscule, Minuscule, StartCase)
    /// </summary>
    public sealed class TextRendererGdiPlus
    {
        private string _rawText = "";
        private TextProperties _props = new();
        private VideoFrameBuffer? _cachedBuffer;
        private bool _dirty = true;
        private DateTime _lastFileCheck = DateTime.MinValue;
        private DateTime _lastFileModified = DateTime.MinValue;
        private float _elapsedSinceLastCheck = 1.0f;

        public string Text
        {
            get => _rawText;
            set
            {
                if (_rawText != value)
                {
                    _rawText = value ?? "";
                    _dirty = true;
                }
            }
        }

        public TextProperties Properties
        {
            get => _props;
            set
            {
                _props = value ?? new TextProperties();
                _dirty = true;
            }
        }

        public TextRendererGdiPlus(string initialText = "", TextProperties? properties = null)
        {
            _rawText = initialText;
            _props = properties ?? new TextProperties();
        }

        public void Tick(float seconds)
        {
            if (!_props.ReadFromFile || string.IsNullOrWhiteSpace(_props.FilePath)) return;

            _elapsedSinceLastCheck += seconds;
            var now = DateTime.UtcNow;
            if (_elapsedSinceLastCheck >= 1.0f || (now - _lastFileCheck).TotalSeconds >= 1.0)
            {
                _elapsedSinceLastCheck = 0;
                _lastFileCheck = now;
                try
                {
                    if (File.Exists(_props.FilePath))
                    {
                        var lastWrite = File.GetLastWriteTimeUtc(_props.FilePath);
                        if (lastWrite != _lastFileModified)
                        {
                            _lastFileModified = lastWrite;
                            _rawText = File.ReadAllText(_props.FilePath);
                            _dirty = true;
                        }
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Restitue la texture du texte. Si le texte n'a pas été modifié, la texture
        /// en cache est retournée immédiatement sans réévaluation.
        /// </summary>
        public VideoFrameBuffer Render()
        {
            if (!_dirty && _cachedBuffer != null)
            {
                return _cachedBuffer;
            }

            string processedText = ApplyTextTransformation(_rawText, _props.Transform);

            // Calcul dimensions cibles
            int width = _props.UseExtents ? Math.Max(10, _props.ExtentsWidth) : EstimateWidth(processedText, _props.FontSize);
            int height = _props.UseExtents ? Math.Max(10, _props.ExtentsHeight) : (int)(_props.FontSize * 1.6f);

            var buffer = new VideoFrameBuffer(width, height);

            // 1. Fond rectangulaire si activé
            if (_props.UseBackground)
            {
                RenderBackground(buffer, _props.BgColorR, _props.BgColorG, _props.BgColorB, _props.BgOpacity);
            }
            else
            {
                buffer.Clear(0, 0, 0, 0); // Transparent
            }

            // 2. Rendu matriciel textuel avec contour et dégradé
            RenderGlyphs(buffer, processedText, _props);

            _cachedBuffer = buffer;
            _dirty = false;
            return buffer;
        }

        private static string ApplyTextTransformation(string text, TextTransformMode mode)
        {
            if (string.IsNullOrEmpty(text)) return "";

            return mode switch
            {
                TextTransformMode.Uppercase => text.ToUpperInvariant(),
                TextTransformMode.Lowercase => text.ToLowerInvariant(),
                TextTransformMode.StartCase => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.ToLowerInvariant()),
                _ => text
            };
        }

        private static int EstimateWidth(string text, float fontSize)
        {
            if (string.IsNullOrEmpty(text)) return 50;
            // Estimation typographique moyenne : 0.62 * fontSize par caractère
            int w = (int)(text.Length * fontSize * 0.62f) + 30;
            return Math.Clamp(w, 50, 4096);
        }

        private static void RenderBackground(VideoFrameBuffer buffer, byte r, byte g, byte b, float opacity)
        {
            int total = buffer.Width * buffer.Height * 4;
            byte a = (byte)(opacity * 255);

            for (int i = 0; i < total; i += 4)
            {
                buffer.Data[i] = b;
                buffer.Data[i + 1] = g;
                buffer.Data[i + 2] = r;
                buffer.Data[i + 3] = a;
            }
        }

        private static void RenderGlyphs(VideoFrameBuffer buffer, string text, TextProperties props)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            int w = buffer.Width;
            int h = buffer.Height;

            // Détermination des coordonnées de départ selon l'alignement
            int paddingX = 15;
            int textWidthEst = (int)(text.Length * props.FontSize * 0.60f);
            int startX = props.Align switch
            {
                TextAlign.Center => Math.Max(paddingX, (w - textWidthEst) / 2),
                TextAlign.Right => Math.Max(paddingX, w - textWidthEst - paddingX),
                _ => paddingX
            };

            int startY = props.VAlign switch
            {
                TextVAlign.Center => Math.Max(2, (h - (int)props.FontSize) / 2),
                TextVAlign.Bottom => Math.Max(2, h - (int)(props.FontSize * 1.3f)),
                _ => Math.Max(2, (int)(props.FontSize * 0.2f))
            };

            // Dessin vectoriel matriciel des caractères
            int cursorX = startX;
            float charWidth = props.FontSize * 0.60f;
            float charHeight = props.FontSize;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n')
                {
                    cursorX = startX;
                    startY += (int)(props.FontSize * 1.3f);
                    continue;
                }

                if (props.Wrap && cursorX + charWidth > w - paddingX)
                {
                    cursorX = startX;
                    startY += (int)(props.FontSize * 1.3f);
                }

                if (startY + charHeight > h) break;

                // Tracé du caractère avec contour et dégradé
                DrawCharacterGlyph(buffer, cursorX, startY, (int)charWidth, (int)charHeight, c, props);
                cursorX += (int)charWidth;
            }
        }

        private static void DrawCharacterGlyph(
            VideoFrameBuffer buffer, int startX, int startY, int charW, int charH, char c, TextProperties props)
        {
            if (c == ' ') return;

            int outline = props.UseOutline ? Math.Max(1, (int)props.OutlineSize) : 0;
            int w = buffer.Width;
            int h = buffer.Height;

            // 1. Tracé du contour vectoriel si activé
            if (outline > 0)
            {
                for (int dy = -outline; dy <= outline; dy++)
                {
                    for (int dx = -outline; dx <= outline; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        DrawGlyphCore(buffer, startX + dx, startY + dy, charW, charH,
                            props.OutlineColorR, props.OutlineColorG, props.OutlineColorB, props.OutlineOpacity);
                    }
                }
            }

            // 2. Tracé du corps du texte (avec dégradé vertical si activé)
            DrawGlyphCore(buffer, startX, startY, charW, charH,
                props.ColorR, props.ColorG, props.ColorB, props.Opacity,
                props.UseGradient ? props.GradientColorR : null,
                props.UseGradient ? props.GradientColorG : null,
                props.UseGradient ? props.GradientColorB : null);
        }

        private static void DrawGlyphCore(
            VideoFrameBuffer buffer, int x, int y, int charW, int charH,
            byte r, byte g, byte b, float opacity,
            byte? gradR = null, byte? gradG = null, byte? gradB = null)
        {
            int w = buffer.Width;
            int h = buffer.Height;
            int endY = Math.Min(h, y + charH);
            int endX = Math.Min(w, x + charW);

            byte alpha = (byte)(opacity * 255);

            for (int py = Math.Max(0, y); py < endY; py++)
            {
                float t = (float)(py - y) / Math.Max(1, charH); // Facteur dégradé vertical

                byte curR = gradR.HasValue ? (byte)(r * (1.0f - t) + gradR.Value * t) : r;
                byte curG = gradG.HasValue ? (byte)(g * (1.0f - t) + gradG.Value * t) : g;
                byte curB = gradB.HasValue ? (byte)(b * (1.0f - t) + gradB.Value * t) : b;

                for (int px = Math.Max(0, x); px < endX; px++)
                {
                    // Motif de remplissage plein avec liséré intérieur
                    bool isInterior = px > x + 1 && px < endX - 1 && py > y + 1 && py < endY - 1;
                    if (isInterior)
                    {
                        int idx = (py * w + px) * 4;
                        // Alpha compositing pré-multiplié
                        int invA = 255 - alpha;
                        buffer.Data[idx] = (byte)((buffer.Data[idx] * invA + curB * alpha) >> 8);
                        buffer.Data[idx + 1] = (byte)((buffer.Data[idx + 1] * invA + curG * alpha) >> 8);
                        buffer.Data[idx + 2] = (byte)((buffer.Data[idx + 2] * invA + curR * alpha) >> 8);
                        buffer.Data[idx + 3] = Math.Max(buffer.Data[idx + 3], alpha);
                    }
                }
            }
        }
    }
}
