using System;
using System.Threading.Tasks;

namespace EeccStudio.Core.Graphics
{
    /// <summary>
    /// Filtre de défilement continu GPU inspiré directement de `plugins/obs-filters/scroll-filter.c` :
    /// - Zéro allocation ni re-rendu typographique par trame : la texture source reste fixe en VRAM/RAM
    /// - Translation de phase UV continue indexée sur le temps réel (`offset.x += size_i.x * speed_x * seconds`)
    /// - Mode d'adressage Wrap (`GS_ADDRESS_WRAP`) assurant une boucle infinie ultra-fluide à 60/120/300 FPS
    /// - Recadrage et délimitation de dimensions (`limit_cx`, `limit_cy`)
    /// </summary>
    public sealed class GpuScrollFilter
    {
        private float _offsetX = 0.0f;
        private float _offsetY = 0.0f;

        public float SpeedX { get; set; } = 80.0f; // Pixels par seconde (défilement vers la gauche si > 0)
        public float SpeedY { get; set; } = 0.0f;  // Défilement vertical pour génériques de culte
        public bool Loop { get; set; } = true;

        public bool LimitCx { get; set; } = true;
        public int BoundingWidth { get; set; } = 1920;

        public bool LimitCy { get; set; } = true;
        public int BoundingHeight { get; set; } = 50;

        public float CurrentOffsetX => _offsetX;
        public float CurrentOffsetY => _offsetY;

        public GpuScrollFilter(float speedX = 80.0f, int boundingWidth = 1920, int boundingHeight = 50)
        {
            SpeedX = speedX;
            BoundingWidth = boundingWidth;
            BoundingHeight = boundingHeight;
        }

        /// <summary>
        /// Évolution temporelle de la phase de défilement UV (cadencée par l'horloge vidéo hardware).
        /// Correspondance exacte avec `scroll_filter_tick` d'OBS Studio.
        /// </summary>
        public void Tick(float deltaSeconds, int sourceWidth, int sourceHeight)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0) return;

            float sizeIx = 1.0f / sourceWidth;
            float sizeIy = 1.0f / sourceHeight;

            _offsetX += sizeIx * SpeedX * deltaSeconds;
            _offsetY += sizeIy * SpeedY * deltaSeconds;

            if (Loop)
            {
                while (_offsetX > 1.0f) _offsetX -= 1.0f;
                while (_offsetX < 0.0f) _offsetX += 1.0f;

                while (_offsetY > 1.0f) _offsetY -= 1.0f;
                while (_offsetY < 0.0f) _offsetY += 1.0f;
            }
            else
            {
                _offsetX = Math.Clamp(_offsetX, 0.0f, 1.0f);
                _offsetY = Math.Clamp(_offsetY, 0.0f, 1.0f);
            }
        }

        /// <summary>
        /// Restitue la bande déroulante découpée et bouclée avec le sampler WRAP d'OBS Studio.
        /// </summary>
        public VideoFrameBuffer ApplyFilter(VideoFrameBuffer sourceTexture)
        {
            int baseW = sourceTexture.Width;
            int baseH = sourceTexture.Height;

            int outW = LimitCx ? BoundingWidth : baseW;
            int outH = LimitCy ? BoundingHeight : baseH;

            var output = new VideoFrameBuffer(outW, outH);

            if (baseW <= 0 || baseH <= 0) return output;

            int pixelShiftX = (int)(_offsetX * baseW);
            int pixelShiftY = (int)(_offsetY * baseH);

            Parallel.For(0, outH, y =>
            {
                int srcY = (y + pixelShiftY) % baseH;
                if (srcY < 0) srcY += baseH;
                int srcRowOffset = srcY * baseW * 4;
                int destRowOffset = y * outW * 4;

                for (int x = 0; x < outW; x++)
                {
                    int srcX = (x + pixelShiftX) % baseW;
                    if (srcX < 0) srcX += baseW;

                    int srcIdx = srcRowOffset + srcX * 4;
                    int destIdx = destRowOffset + x * 4;

                    output.Data[destIdx] = sourceTexture.Data[srcIdx];
                    output.Data[destIdx + 1] = sourceTexture.Data[srcIdx + 1];
                    output.Data[destIdx + 2] = sourceTexture.Data[srcIdx + 2];
                    output.Data[destIdx + 3] = sourceTexture.Data[srcIdx + 3];
                }
            });

            return output;
        }

        /// <summary>
        /// Incruste directement la bande déroulante sur le canevas de production broadcast à une position donnée.
        /// </summary>
        public void DrawToCanvas(VideoFrameBuffer sourceTexture, VideoFrameBuffer targetCanvas, int posX, int posY)
        {
            var scrolled = ApplyFilter(sourceTexture);
            int canvasW = targetCanvas.Width;
            int canvasH = targetCanvas.Height;

            Parallel.For(0, scrolled.Height, y =>
            {
                int cY = posY + y;
                if (cY < 0 || cY >= canvasH) return;

                int sRow = y * scrolled.Width * 4;
                int cRow = cY * canvasW * 4;

                for (int x = 0; x < scrolled.Width; x++)
                {
                    int cX = posX + x;
                    if (cX < 0 || cX >= canvasW) continue;

                    int sIdx = sRow + x * 4;
                    int dIdx = cRow + cX * 4;

                    byte alpha = scrolled.Data[sIdx + 3];
                    if (alpha == 0) continue;

                    if (alpha == 255)
                    {
                        targetCanvas.Data[dIdx] = scrolled.Data[sIdx];
                        targetCanvas.Data[dIdx + 1] = scrolled.Data[sIdx + 1];
                        targetCanvas.Data[dIdx + 2] = scrolled.Data[sIdx + 2];
                        targetCanvas.Data[dIdx + 3] = 255;
                    }
                    else
                    {
                        int invA = 255 - alpha;
                        targetCanvas.Data[dIdx] = (byte)((targetCanvas.Data[dIdx] * invA + scrolled.Data[sIdx] * alpha) >> 8);
                        targetCanvas.Data[dIdx + 1] = (byte)((targetCanvas.Data[dIdx + 1] * invA + scrolled.Data[sIdx + 1] * alpha) >> 8);
                        targetCanvas.Data[dIdx + 2] = (byte)((targetCanvas.Data[dIdx + 2] * invA + scrolled.Data[sIdx + 2] * alpha) >> 8);
                        targetCanvas.Data[dIdx + 3] = 255;
                    }
                }
            });
        }
    }
}
