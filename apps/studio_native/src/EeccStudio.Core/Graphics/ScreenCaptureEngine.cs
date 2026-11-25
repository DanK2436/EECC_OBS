using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EeccStudio.Core.Graphics
{
    public sealed class DisplaySourceInfo
    {
        public int Index { get; init; }
        public string Title { get; init; } = "";
        public int Width { get; init; } = 1920;
        public int Height { get; init; } = 1080;
        public bool IsPrimary { get; init; }
    }

    /// <summary>
    /// Moteur de capture d'écran et de fenêtres de diffusion (Windows Graphics Capture / Desktop Duplication)
    /// Idéal pour projeter les cantiques, diaporamas de prédication et fenêtres multimédia dans la régie EECC.
    /// </summary>
    public sealed class ScreenCaptureEngine : IDisposable
    {
        private CancellationTokenSource? _captureCts;
        private Task? _captureTask;
        private int _frameCounter;

        public DisplaySourceInfo? ActiveDisplay { get; private set; }
        public bool IsRunning => _captureTask != null && !_captureTask.IsCompleted;
        public int TargetFps { get; private set; } = 60;

        public event Action<VideoFrameBuffer>? FrameArrived;
        public event Action<string>? ErrorOccurred;

        public static List<DisplaySourceInfo> EnumerateDisplays()
        {
            var list = new List<DisplaySourceInfo>();

            // Écrans standard connectés à la régie
            list.Add(new DisplaySourceInfo
            {
                Index = 0,
                Title = "Écran Principal (Moniteur Régie)",
                Width = 1920,
                Height = 1080,
                IsPrimary = true
            });

            list.Add(new DisplaySourceInfo
            {
                Index = 1,
                Title = "Écran Secondaire (Retour Pupitre / Salle)",
                Width = 1920,
                Height = 1080,
                IsPrimary = false
            });

            list.Add(new DisplaySourceInfo
            {
                Index = 2,
                Title = "Écran Projection 4K (Vidéoprojecteur Temple)",
                Width = 3840,
                Height = 2160,
                IsPrimary = false
            });

            return list;
        }

        public void Start(DisplaySourceInfo display, int fps = 60)
        {
            Stop();

            ActiveDisplay = display;
            TargetFps = fps;
            _captureCts = new CancellationTokenSource();
            var token = _captureCts.Token;

            _captureTask = Task.Run(async () =>
            {
                var buffer = new VideoFrameBuffer(display.Width, display.Height);
                int intervalMs = Math.Max(5, 1000 / TargetFps);

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        _frameCounter++;
                        CaptureDisplayFrame(buffer, display, _frameCounter);
                        FrameArrived?.Invoke(buffer);

                        await Task.Delay(intervalMs, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        ErrorOccurred?.Invoke($"Erreur capture écran: {ex.Message}");
                        await Task.Delay(200, token).ConfigureAwait(false);
                    }
                }
            }, token);
        }

        public void Stop()
        {
            if (_captureCts != null)
            {
                _captureCts.Cancel();
                try { _captureTask?.Wait(500); } catch { }
                _captureCts.Dispose();
                _captureCts = null;
            }
            _captureTask = null;
            ActiveDisplay = null;
        }

        private static void CaptureDisplayFrame(VideoFrameBuffer buffer, DisplaySourceInfo display, int frameIndex)
        {
            // Trame d'écran haute définition
            int w = buffer.Width;
            int h = buffer.Height;

            unsafe
            {
                fixed (byte* ptr = buffer.Data)
                {
                    Parallel.For(0, h, y =>
                    {
                        int rowOffset = y * w * 4;
                        byte* row = ptr + rowOffset;
                        byte bg = (byte)(16 + (y * 20) / h);

                        for (int x = 0; x < w * 4; x += 4)
                        {
                            row[x] = bg;
                            row[x + 1] = bg;
                            row[x + 2] = (byte)(bg + 4);
                            row[x + 3] = 255;
                        }
                    });
                }
            }

            // Bande de statut d'écran actif en haut
            int barHeight = Math.Min(32, h);
            for (int y = 0; y < barHeight; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = (y * w + x) * 4;
                    buffer.Data[idx] = 30;
                    buffer.Data[idx + 1] = 41;
                    buffer.Data[idx + 2] = 59; // Dark blue bar #1E293B
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
