using System;

namespace EeccStudio.Core.Graphics
{
    /// <summary>
    /// Source d'affichage de versets bibliques à l'antenne :
    /// - Référence biblique (ex: "Jean 3:16") en couleur or/ambre (#D97706)
    /// - Texte biblique avec guillemets et retour à la ligne automatique
    /// - Mention de version (ex: "LSG 1910")
    /// </summary>
    public sealed class BibleVerseOverlaySource
    {
        private readonly TextRendererGdiPlus _referenceRenderer;
        private readonly TextRendererGdiPlus _textRenderer;
        private bool _isVisible = false;
        private float _animationProgress = 0.0f;

        public bool IsVisible
        {
            get => _isVisible;
            set => _isVisible = value;
        }

        public string Reference
        {
            get => _referenceRenderer.Text;
            set => _referenceRenderer.Text = value;
        }

        public string VerseText
        {
            get => _textRenderer.Text;
            set => _textRenderer.Text = value;
        }

        public BibleVerseOverlaySource(string reference = "Jean 3:16", string text = "Car Dieu a tant aimé le monde...", string version = "LSG 1910")
        {
            _referenceRenderer = new TextRendererGdiPlus($"{reference} ({version})", new TextProperties
            {
                FontSize = 24.0f,
                Bold = true,
                ColorR = 217,
                ColorG = 119,
                ColorB = 6, // Or / Ambre EECC
                UseOutline = true,
                OutlineSize = 1.5f,
                OutlineColorR = 0,
                OutlineColorG = 0,
                OutlineColorB = 0
            });

            _textRenderer = new TextRendererGdiPlus(text, new TextProperties
            {
                FontSize = 20.0f,
                Bold = false,
                Italic = true,
                ColorR = 250,
                ColorG = 250,
                ColorB = 250,
                UseOutline = true,
                OutlineSize = 1.0f,
                OutlineColorR = 0,
                OutlineColorG = 0,
                OutlineColorB = 0,
                UseExtents = true,
                ExtentsWidth = 1400,
                ExtentsHeight = 120,
                Wrap = true
            });
        }

        public void Tick(float deltaSeconds)
        {
            float speed = 3.0f;
            if (_isVisible && _animationProgress < 1.0f)
            {
                _animationProgress = Math.Min(1.0f, _animationProgress + deltaSeconds * speed);
            }
            else if (!_isVisible && _animationProgress > 0.0f)
            {
                _animationProgress = Math.Max(0.0f, _animationProgress - deltaSeconds * speed);
            }
        }

        public void ApplyToCanvas(VideoFrameBuffer targetCanvas)
        {
            if (_animationProgress <= 0.001f) return;

            int canvasW = targetCanvas.Width;
            int canvasH = targetCanvas.Height;

            int bannerW = (int)(canvasW * 0.82f);
            int bannerH = (int)(canvasH * 0.18f);
            int startX = (canvasW - bannerW) / 2;
            int startY = (int)(canvasH * 0.76f);

            byte alpha = (byte)(_animationProgress * 235);
            int invAlpha = 255 - alpha;

            // Fond du bandeau verset avec bordure or supérieure
            for (int y = startY; y < Math.Min(canvasH, startY + bannerH); y++)
            {
                for (int x = startX; x < Math.Min(canvasW, startX + bannerW); x++)
                {
                    int idx = (y * canvasW + x) * 4;
                    bool isTopBorder = (y - startY) < 3;

                    byte r = isTopBorder ? (byte)217 : (byte)17;
                    byte g = isTopBorder ? (byte)119 : (byte)24;
                    byte b = isTopBorder ? (byte)6 : (byte)39;

                    targetCanvas.Data[idx] = (byte)((targetCanvas.Data[idx] * invAlpha + b * alpha) >> 8);
                    targetCanvas.Data[idx + 1] = (byte)((targetCanvas.Data[idx + 1] * invAlpha + g * alpha) >> 8);
                    targetCanvas.Data[idx + 2] = (byte)((targetCanvas.Data[idx + 2] * invAlpha + r * alpha) >> 8);
                }
            }

            var refTex = _referenceRenderer.Render();
            var textTex = _textRenderer.Render();

            BlitTexture(refTex, targetCanvas, startX + 25, startY + 12);
            BlitTexture(textTex, targetCanvas, startX + 25, startY + 45);
        }

        private static void BlitTexture(VideoFrameBuffer src, VideoFrameBuffer dest, int startX, int startY)
        {
            int destW = dest.Width;
            int destH = dest.Height;

            for (int sy = 0; sy < src.Height; sy++)
            {
                int dy = startY + sy;
                if (dy >= destH) break;

                for (int sx = 0; sx < src.Width; sx++)
                {
                    int dx = startX + sx;
                    if (dx >= destW) break;

                    int sIdx = (sy * src.Width + sx) * 4;
                    byte sA = src.Data[sIdx + 3];
                    if (sA == 0) continue;

                    int dIdx = (dy * destW + dx) * 4;
                    int invA = 255 - sA;

                    dest.Data[dIdx] = (byte)((dest.Data[dIdx] * invA + src.Data[sIdx] * sA) >> 8);
                    dest.Data[dIdx + 1] = (byte)((dest.Data[dIdx + 1] * invA + src.Data[sIdx + 1] * sA) >> 8);
                    dest.Data[dIdx + 2] = (byte)((dest.Data[dIdx + 2] * invA + src.Data[sIdx + 2] * sA) >> 8);
                    dest.Data[dIdx + 3] = 255;
                }
            }
        }
    }
}
