using System;

namespace EeccStudio.Core.Graphics
{
    /// <summary>
    /// Type de courbe d'atténuation cinématique (Easing function) conforme à OBS Studio
    /// </summary>
    public enum EasingType
    {
        Linear,
        CubicInOut,
        QuadraticInOut,
        CubicIn,
        CubicOut,
        SmoothStep,
        SmootherStep
    }

    /// <summary>
    /// Moteur de courbes d'atténuation et interpolations cinématiques
    /// Implémentation fidèle des algorithmes d'OBS Studio (plugins/obs-transitions/easings.h et obs-transitions.c)
    /// </summary>
    public static class AnimationEasings
    {
        /// <summary>
        /// Calcule le coefficient d'interpolation [0.0 - 1.0] selon la courbe sélectionnée
        /// </summary>
        public static float Evaluate(EasingType type, float t)
        {
            t = Math.Clamp(t, 0.0f, 1.0f);

            return type switch
            {
                EasingType.Linear => t,
                EasingType.CubicInOut => CubicEaseInOut(t),
                EasingType.QuadraticInOut => QuadraticEaseInOut(t),
                EasingType.CubicIn => CubicEaseIn(t),
                EasingType.CubicOut => CubicEaseOut(t),
                EasingType.SmoothStep => SmoothStep(t),
                EasingType.SmootherStep => SmootherStep(t),
                _ => t
            };
        }

        /// <summary>
        /// Formule exacte de plugins/obs-transitions/easings.h :
        /// if (t < 0.5f) return 4.0f * t * t * t;
        /// else { float temp = (2.0f * t) - 2.0f; return (t - 1.0f) * temp * temp + 1.0f; }
        /// </summary>
        public static float CubicEaseInOut(float t)
        {
            t = Math.Clamp(t, 0.0f, 1.0f);
            if (t < 0.5f)
            {
                return 4.0f * t * t * t;
            }
            else
            {
                float temp = (2.0f * t) - 2.0f;
                return ((t - 1.0f) * temp * temp) + 1.0f;
            }
        }

        /// <summary>
        /// Quadratic Ease In-Out
        /// </summary>
        public static float QuadraticEaseInOut(float t)
        {
            t = Math.Clamp(t, 0.0f, 1.0f);
            if (t < 0.5f)
            {
                return 2.0f * t * t;
            }
            else
            {
                return (-2.0f * t * t) + (4.0f * t) - 1.0f;
            }
        }

        public static float CubicEaseIn(float t)
        {
            t = Math.Clamp(t, 0.0f, 1.0f);
            return t * t * t;
        }

        public static float CubicEaseOut(float t)
        {
            t = Math.Clamp(t, 0.0f, 1.0f);
            float f = t - 1.0f;
            return (f * f * f) + 1.0f;
        }

        /// <summary>
        /// SmoothStep standard GPU GLSL / HLSL : 3*t^2 - 2*t^3
        /// </summary>
        public static float SmoothStep(float t)
        {
            t = Math.Clamp(t, 0.0f, 1.0f);
            return t * t * (3.0f - (2.0f * t));
        }

        /// <summary>
        /// Perlin SmootherStep : 6*t^5 - 15*t^4 + 10*t^3
        /// </summary>
        public static float SmootherStep(float t)
        {
            t = Math.Clamp(t, 0.0f, 1.0f);
            return t * t * t * (t * ((t * 6.0f) - 15.0f) + 10.0f);
        }

        /// <summary>
        /// Interpolation linéaire simple de float
        /// </summary>
        public static float Lerp(float start, float end, float t)
        {
            return start + ((end - start) * t);
        }
    }

    /// <summary>
    /// Type de transition de scène OBS
    /// </summary>
    public enum TransitionKind
    {
        Cut,
        Fade,
        SlideLeft,
        SlideRight,
        SlideUp,
        SlideDown,
        LumaWipe,
        Stinger
    }

    /// <summary>
    /// Configuration d'une transition vidéo cinématique
    /// </summary>
    public class TransitionSettings
    {
        public TransitionKind Kind { get; set; } = TransitionKind.Fade;
        public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(300);
        public EasingType Easing { get; set; } = EasingType.CubicInOut;

        /// <summary>
        /// Point de coupure (Cut Point) pour transition Stinger en millisecondes ou pourcentage
        /// Dans OBS Studio, c'est le moment précis où la vidéo A est commutée sur la vidéo B
        /// </summary>
        public TimeSpan StingerCutPoint { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Douceur de la transition Luma Wipe (0.0 = net, 1.0 = dégradé progressif très doux)
        /// Directement inspiré de obs-transitions/luma-wipe-transition.c
        /// </summary>
        public float LumaSoftness { get; set; } = 0.08f;
        public string? LumaWipeMapPath { get; set; }
    }

    /// <summary>
    /// Contrôleur d'exécution d'une transition vidéo temps-réel (Scene Transition Engine)
    /// Gère les coordonnées de mixage, translation et alpha pour le compositeur GPU
    /// </summary>
    public class SceneTransitionRunner
    {
        private readonly TransitionSettings _settings;
        private TimeSpan _elapsed = TimeSpan.Zero;
        private bool _isComplete = false;
        private bool _cutPointReached = false;

        public event Action? OnCutPointTriggered;
        public event Action? OnTransitionCompleted;

        public SceneTransitionRunner(TransitionSettings settings)
        {
            _settings = settings ?? new TransitionSettings();
            if (_settings.Duration <= TimeSpan.Zero)
            {
                _isComplete = true;
                _cutPointReached = true;
            }
        }

        public bool IsComplete => _isComplete;
        public bool CutPointReached => _cutPointReached;
        public float RawProgress => _settings.Duration > TimeSpan.Zero
            ? Math.Clamp((float)(_elapsed.TotalMilliseconds / _settings.Duration.TotalMilliseconds), 0f, 1f)
            : 1f;

        public float EasedProgress => AnimationEasings.Evaluate(_settings.Easing, RawProgress);

        /// <summary>
        /// Avance la transition dans le temps
        /// </summary>
        public void Tick(TimeSpan deltaTime)
        {
            if (_isComplete) return;

            _elapsed += deltaTime;

            // Détection du Cut Point pour Stinger
            if (!_cutPointReached && _elapsed >= _settings.StingerCutPoint)
            {
                _cutPointReached = true;
                OnCutPointTriggered?.Invoke();
            }

            if (_elapsed >= _settings.Duration)
            {
                _elapsed = _settings.Duration;
                _isComplete = true;
                if (!_cutPointReached)
                {
                    _cutPointReached = true;
                    OnCutPointTriggered?.Invoke();
                }
                OnTransitionCompleted?.Invoke();
            }
        }

        /// <summary>
        /// Calcule l'opacité alpha de la source A (sortante) et de la source B (entrante)
        /// </summary>
        public (float AlphaA, float AlphaB) GetFadedAlphas()
        {
            float t = EasedProgress;
            return _settings.Kind switch
            {
                TransitionKind.Cut => (_cutPointReached ? 0.0f : 1.0f, _cutPointReached ? 1.0f : 0.0f),
                TransitionKind.Fade => (1.0f - t, t),
                _ => (1.0f, 1.0f) // Pour Slide ou Stinger, l'alpha reste plein car masqué par translation ou stinger overlay
            };
        }

        /// <summary>
        /// Calcule le décalage géométrique en pixels pour les transitions de type Slide
        /// Inspiré du calcul matriciel de plugins/obs-transitions/transition-slide.c
        /// </summary>
        public (float OffsetXA, float OffsetYA, float OffsetXB, float OffsetYB) GetSlideOffsets(int canvasWidth, int canvasHeight)
        {
            float t = EasedProgress;

            return _settings.Kind switch
            {
                TransitionKind.SlideLeft => (
                    -t * canvasWidth, 0f,
                    (1.0f - t) * canvasWidth, 0f
                ),
                TransitionKind.SlideRight => (
                    t * canvasWidth, 0f,
                    -(1.0f - t) * canvasWidth, 0f
                ),
                TransitionKind.SlideUp => (
                    0f, -t * canvasHeight,
                    0f, (1.0f - t) * canvasHeight
                ),
                TransitionKind.SlideDown => (
                    0f, t * canvasHeight,
                    0f, -(1.0f - t) * canvasHeight
                ),
                _ => (0f, 0f, 0f, 0f)
            };
        }

        /// <summary>
        /// Calcule le seuil Luma Wipe (0.0 à 1.0) et la plage de fondu pour le shader
        /// Formule OBS : threshold = t * (1.0 + softness)
        /// </summary>
        public (float Threshold, float Softness) GetLumaWipeParameters()
        {
            float t = EasedProgress;
            float softness = Math.Max(0.001f, _settings.LumaSoftness);
            float threshold = t * (1.0f + softness);
            return (threshold, softness);
        }
    }
}
