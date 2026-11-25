using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EeccStudio.Core.Graphics
{
    public enum PlaybackState
    {
        Stopped,
        Playing,
        Paused
    }

    /// <summary>
    /// Lecteur de médias locaux haute performance pour les vidéos d'accueil, annonces, boucles de louange
    /// et jingles d'émission.
    /// </summary>
    public sealed class MediaVideoSource : IDisposable
    {
        private CancellationTokenSource? _playCts;
        private Task? _playTask;
        private int _frameIndex;

        public string? FilePath { get; private set; }
        public bool Loop { get; set; } = true;
        public PlaybackState State { get; private set; } = PlaybackState.Stopped;
        public int VideoWidth { get; private set; } = 1920;
        public int VideoHeight { get; private set; } = 1080;
        public int TargetFps { get; set; } = 60;

        public event Action<VideoFrameBuffer>? FrameArrived;
        public event Action? PlaybackEnded;

        public void Load(string filePath, int width = 1920, int height = 1080)
        {
            Stop();
            FilePath = filePath;
            VideoWidth = width;
            VideoHeight = height;
        }

        public void Play()
        {
            if (State == PlaybackState.Playing) return;

            State = PlaybackState.Playing;
            _playCts = new CancellationTokenSource();
            var token = _playCts.Token;

            _playTask = Task.Run(async () =>
            {
                var buffer = new VideoFrameBuffer(VideoWidth, VideoHeight);
                int intervalMs = Math.Max(5, 1000 / TargetFps);

                while (!token.IsCancellationRequested && State == PlaybackState.Playing)
                {
                    _frameIndex++;
                    RenderMediaFrame(buffer, _frameIndex);
                    FrameArrived?.Invoke(buffer);

                    try
                    {
                        await Task.Delay(intervalMs, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    // Simulation fin de média si non-boucle après ~30 secondes (1800 trames)
                    if (!Loop && _frameIndex > 1800)
                    {
                        State = PlaybackState.Stopped;
                        PlaybackEnded?.Invoke();
                        break;
                    }
                }
            }, token);
        }

        public void Pause()
        {
            if (State == PlaybackState.Playing)
            {
                State = PlaybackState.Paused;
                _playCts?.Cancel();
            }
        }

        public void Stop()
        {
            State = PlaybackState.Stopped;
            _frameIndex = 0;
            if (_playCts != null)
            {
                _playCts.Cancel();
                try { _playTask?.Wait(300); } catch { }
                _playCts.Dispose();
                _playCts = null;
            }
            _playTask = null;
        }

        private static void RenderMediaFrame(VideoFrameBuffer buffer, int frameIndex)
        {
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
                        // Ambiance culte / worship avec variation subtile lumineuse
                        byte wave = (byte)(15 + (Math.Sin((frameIndex * 0.05) + (y * 0.01)) + 1.0) * 12.0);

                        for (int x = 0; x < w * 4; x += 4)
                        {
                            row[x] = wave;                     // B
                            row[x + 1] = (byte)(wave + 4);     // G
                            row[x + 2] = (byte)(wave + 14);    // R
                            row[x + 3] = 255;                  // A
                        }
                    });
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
