using System;

namespace EeccStudio.Core.Graphics
{
    /// <summary>
    /// Source de bandeau d'intervenant / pasteur (Lower Third) à 2 lignes :
    /// - Ligne 1 : Nom de l'orateur (en gras avec liséré accentué EECC)
    /// - Ligne 2 : Titre, fonction pastorale ou thème du message
    /// - Gestion de la visibilité et animation d'entrée/sortie
    /// </summary>
    public sealed class LowerThirdOverlaySource
    {
        private readonly TextRendererGdiPlus _titleRenderer;
        private readonly TextRendererGdiPlus _subtitleRenderer;
        private bool _isVisible = false;
        private float _animationProgress = 0.0f;

        public bool IsVisible
        {
            get => _isVisible;
            set => _isVisible = value;
        }

        public string SpeakerName
        {
            get => _titleRenderer.Text;
            set => _titleRenderer.Text = value;
        }

        public string SpeakerRole
        {
            get => _subtitleRenderer.Text;
            set => _subtitleRenderer.Text = value;
        }

        public LowerThirdOverlaySource(string name = "Pasteur Principal", string role = "Culte Dominical EECC")
        {
            _titleRenderer = new TextRendererGdiPlus(name, new TextProperties
            {
                FontSize = 26.0f,
                Bold = true,
                ColorR = 255,
                ColorG = 255,
                ColorB = 255,
                UseOutline = true,
                OutlineSize = 1.5f,
                OutlineColorR = 10,
                OutlineColorG = 10,
                OutlineColorB = 10
            });

            _subtitleRenderer = new TextRendererGdiPlus(role, new TextProperties
            {
                FontSize = 18.0f,
                Bold = false,
                ColorR = 210,
                ColorG = 225,
                ColorB = 250,
                UseOutline = true,
                OutlineSize = 1.0f,
                OutlineColorR = 10,
                OutlineColorG = 10,
                OutlineColorB = 10
            });
        }

        public void Tick(float deltaSeconds)
        {
            // Transition fluide d'entrée et de sortie
            float speed = 3.5f; // ~300 ms
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

            int bannerW = (int)(canvasW * 0.45f);
            int bannerH = (int)(canvasH * 0.12f);
            int startX = (int)(canvasW * 0.05f);
            // Glissement vertical élégant basé sur animationProgress
            int finalY = (int)(canvasH * 0.82f);
            int startY = finalY + (int)((1.0f - _animationProgress) * 50);

            // 1. Fond du bandeau
            byte alpha = (byte)(_animationProgress * 230);
            int invAlpha = 255 - alpha;

            for (int y = startY; y < Math.Min(canvasH, startY + bannerH); y++)
            {
                for (int x = startX; x < Math.Min(canvasW, startX + bannerW); x++)
                {
                    int idx = (y * canvasW + x) * 4;
                    bool isAccentBar = (x - startX) < 8;

                    byte r = isAccentBar ? (byte)37 : (byte)15;
                    byte g = isAccentBar ? (byte)99 : (byte)23;
                    byte b = isAccentBar ? (byte)235 : (byte)42;

                    targetCanvas.Data[idx] = (byte)((targetCanvas.Data[idx] * invAlpha + b * alpha) >> 8);
                    targetCanvas.Data[idx + 1] = (byte)((targetCanvas.Data[idx + 1] * invAlpha + g * alpha) >> 8);
                    targetCanvas.Data[idx + 2] = (byte)((targetCanvas.Data[idx + 2] * invAlpha + r * alpha) >> 8);
                }
            }

            // 2. Rendu des textes
            var titleTex = _titleRenderer.Render();
            var subTex = _subtitleRenderer.Render();

            BlitTexture(titleTex, targetCanvas, startX + 20, startY + 8);
            BlitTexture(subTex, targetCanvas, startX + 20, startY + (int)(bannerH * 0.55f));
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
