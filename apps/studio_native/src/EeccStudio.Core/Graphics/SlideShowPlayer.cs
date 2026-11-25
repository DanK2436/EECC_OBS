using System;
using System.Collections.Generic;
using System.IO;

namespace EeccStudio.Core.Graphics
{
    public enum SlideMode
    {
        Auto,
        Manual
    }

    public enum SlideTransition
    {
        Cut,
        Fade,
        Slide,
        Swipe
    }

    public sealed class SlideItem
    {
        public string Title { get; init; } = "";
        public string FilePath { get; init; } = "";
        public VideoFrameBuffer? Buffer { get; set; }
    }

    /// <summary>
    /// Lecteur de diaporamas de cantiques et projections inspiré de `plugins/image-source/obs-slideshow.c` :
    /// - Gestion séquentielle des paroles de cantiques, annonces et versets
    /// - Modes Automatique (avec slide_time) ou Manuel (pilotable par touches ou Stream Deck)
    /// - Transitions animées (Cut, Fade, Slide, Swipe) avec courbe d'assouplissement cubique
    /// </summary>
    public sealed class SlideShowPlayer
    {
        private readonly List<SlideItem> _slides = new();
        private int _currentIndex = 0;
        private int _nextIndex = -1;
        private float _transitionProgress = 1.0f; // 1.0 = transition terminée
        private float _timeOnCurrentSlide = 0.0f;
        private bool _isPlaying = true;
        private readonly object _lock = new();

        public SlideMode Mode { get; set; } = SlideMode.Manual;
        public SlideTransition Transition { get; set; } = SlideTransition.Fade;
        public float SlideTimeSeconds { get; set; } = 8.0f;
        public float TransitionSpeedSeconds { get; set; } = 0.4f; // 400 ms
        public bool Loop { get; set; } = true;

        public int CurrentIndex => _currentIndex;
        public int SlideCount { get { lock (_lock) return _slides.Count; } }
        public bool IsTransitioning => _transitionProgress < 1.0f;

        public event Action<int>? SlideChanged;

        public void AddSlide(string title, string filePath)
        {
            lock (_lock)
            {
                var buf = new VideoFrameBuffer(1920, 1080);
                // Fond sombre diaporama avec titre
                buf.Clear(18, 24, 38, 255);
                _slides.Add(new SlideItem
                {
                    Title = title,
                    FilePath = filePath,
                    Buffer = buf
                });
            }
        }

        public void NextSlide()
        {
            lock (_lock)
            {
                if (_slides.Count <= 1) return;

                int target = _currentIndex + 1;
                if (target >= _slides.Count)
                {
                    if (!Loop) return;
                    target = 0;
                }

                StartTransitionTo(target);
            }
        }

        public void PreviousSlide()
        {
            lock (_lock)
            {
                if (_slides.Count <= 1) return;

                int target = _currentIndex - 1;
                if (target < 0)
                {
                    if (!Loop) return;
                    target = _slides.Count - 1;
                }

                StartTransitionTo(target);
            }
        }

        public void GoToSlide(int index)
        {
            lock (_lock)
            {
                if (index < 0 || index >= _slides.Count || index == _currentIndex) return;
                StartTransitionTo(index);
            }
        }

        private void StartTransitionTo(int targetIndex)
        {
            if (Transition == SlideTransition.Cut)
            {
                _currentIndex = targetIndex;
                _nextIndex = -1;
                _transitionProgress = 1.0f;
                _timeOnCurrentSlide = 0.0f;
                SlideChanged?.Invoke(_currentIndex);
                return;
            }

            _nextIndex = targetIndex;
            _transitionProgress = 0.0f;
            _timeOnCurrentSlide = 0.0f;
        }

        public void Tick(float deltaSeconds)
        {
            lock (_lock)
            {
                if (_slides.Count == 0) return;

                // 1. Avancement de la transition en cours
                if (_nextIndex >= 0 && _transitionProgress < 1.0f)
                {
                    float step = deltaSeconds / Math.Max(0.05f, TransitionSpeedSeconds);
                    _transitionProgress = Math.Min(1.0f, _transitionProgress + step);

                    if (_transitionProgress >= 1.0f)
                    {
                        _currentIndex = _nextIndex;
                        _nextIndex = -1;
                        SlideChanged?.Invoke(_currentIndex);
                    }
                    return;
                }

                // 2. Mode automatique
                if (Mode == SlideMode.Auto && _isPlaying)
                {
                    _timeOnCurrentSlide += deltaSeconds;
                    if (_timeOnCurrentSlide >= SlideTimeSeconds)
                    {
                        NextSlide();
                    }
                }
            }
        }

        /// <summary>
        /// Restitue la trame courante du diaporama avec interpolation de transition.
        /// </summary>
        public VideoFrameBuffer Render(int width = 1920, int height = 1080)
        {
            lock (_lock)
            {
                var output = new VideoFrameBuffer(width, height);
                if (_slides.Count == 0)
                {
                    output.Clear(0, 0, 0, 255);
                    return output;
                }

                var currentSlide = _slides[_currentIndex].Buffer ?? new VideoFrameBuffer(width, height);

                // Pas de transition active
                if (_nextIndex < 0 || _transitionProgress >= 0.999f)
                {
                    Buffer.BlockCopy(currentSlide.Data, 0, output.Data, 0, Math.Min(currentSlide.Data.Length, output.Data.Length));
                    return output;
                }

                var nextSlide = _slides[_nextIndex].Buffer ?? new VideoFrameBuffer(width, height);

                // Application de la courbe d'assouplissement cubique d'OBS Studio
                float t = CubicEaseInOut(_transitionProgress);

                switch (Transition)
                {
                    case SlideTransition.Fade:
                        BlendFade(currentSlide, nextSlide, output, t);
                        break;

                    case SlideTransition.Slide:
                    case SlideTransition.Swipe:
                        BlendSlideHorizontal(currentSlide, nextSlide, output, t);
                        break;

                    default:
                        Buffer.BlockCopy(nextSlide.Data, 0, output.Data, 0, Math.Min(nextSlide.Data.Length, output.Data.Length));
                        break;
                }

                return output;
            }
        }

        private static float CubicEaseInOut(float t)
        {
            // Formule exacte d'OBS Studio (easings.h)
            if (t < 0.5f)
            {
                return 4.0f * t * t * t;
            }
            else
            {
                float temp = (2.0f * t - 2.0f);
                return (t - 1.0f) * temp * temp + 1.0f;
            }
        }

        private static void BlendFade(VideoFrameBuffer a, VideoFrameBuffer b, VideoFrameBuffer dest, float t)
        {
            int factorB = (int)(t * 256.0f);
            int factorA = 256 - factorB;
            int total = dest.Width * dest.Height * 4;

            for (int i = 0; i < total; i++)
            {
                dest.Data[i] = (byte)((a.Data[i] * factorA + b.Data[i] * factorB) >> 8);
            }
        }

        private static void BlendSlideHorizontal(VideoFrameBuffer a, VideoFrameBuffer b, VideoFrameBuffer dest, float t)
        {
            int w = dest.Width;
            int h = dest.Height;
            int splitX = (int)(w * t);

            for (int y = 0; y < h; y++)
            {
                int rowOffset = y * w * 4;
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
        }
    }
}
