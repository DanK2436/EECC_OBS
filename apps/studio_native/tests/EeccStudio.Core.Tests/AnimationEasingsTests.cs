using System;
using EeccStudio.Core.Graphics;
using Xunit;

namespace EeccStudio.Core.Tests
{
    public class AnimationEasingsTests
    {
        [Fact]
        public void CubicEaseInOut_BoundaryValues_ReturnsCorrectExtremes()
        {
            Assert.Equal(0.0f, AnimationEasings.CubicEaseInOut(0.0f), 4);
            Assert.Equal(0.5f, AnimationEasings.CubicEaseInOut(0.5f), 4);
            Assert.Equal(1.0f, AnimationEasings.CubicEaseInOut(1.0f), 4);
        }

        [Fact]
        public void CubicEaseInOut_Clamping_HandlesOutOfRangeValues()
        {
            Assert.Equal(0.0f, AnimationEasings.CubicEaseInOut(-0.5f), 4);
            Assert.Equal(1.0f, AnimationEasings.CubicEaseInOut(1.5f), 4);
        }

        [Fact]
        public void CubicEaseInOut_MonotonicIncrease_AlwaysIncreases()
        {
            float prev = 0.0f;
            for (float t = 0.05f; t <= 1.0f; t += 0.05f)
            {
                float current = AnimationEasings.CubicEaseInOut(t);
                Assert.True(current >= prev, $"At t={t}, current ({current}) was not >= prev ({prev})");
                prev = current;
            }
        }

        [Fact]
        public void SmoothStep_BoundaryValues_MatchStandardCurve()
        {
            Assert.Equal(0.0f, AnimationEasings.SmoothStep(0.0f), 4);
            Assert.Equal(0.5f, AnimationEasings.SmoothStep(0.5f), 4);
            Assert.Equal(1.0f, AnimationEasings.SmoothStep(1.0f), 4);
        }

        [Fact]
        public void SceneTransitionRunner_Fade_InterpolatesAlphasCorrectly()
        {
            var settings = new TransitionSettings
            {
                Kind = TransitionKind.Fade,
                Duration = TimeSpan.FromMilliseconds(1000),
                Easing = EasingType.Linear
            };

            var runner = new SceneTransitionRunner(settings);
            var (startA, startB) = runner.GetFadedAlphas();
            Assert.Equal(1.0f, startA, 3);
            Assert.Equal(0.0f, startB, 3);

            // Tick 500ms (50%)
            runner.Tick(TimeSpan.FromMilliseconds(500));
            var (midA, midB) = runner.GetFadedAlphas();
            Assert.Equal(0.5f, midA, 2);
            Assert.Equal(0.5f, midB, 2);

            // Tick remaining 500ms
            runner.Tick(TimeSpan.FromMilliseconds(500));
            var (endA, endB) = runner.GetFadedAlphas();
            Assert.Equal(0.0f, endA, 3);
            Assert.Equal(1.0f, endB, 3);
            Assert.True(runner.IsComplete);
        }

        [Fact]
        public void SceneTransitionRunner_SlideLeft_ComputesExactPixelOffsets()
        {
            var settings = new TransitionSettings
            {
                Kind = TransitionKind.SlideLeft,
                Duration = TimeSpan.FromMilliseconds(1000),
                Easing = EasingType.Linear
            };

            var runner = new SceneTransitionRunner(settings);

            // Tick halfway (500ms) on a 1920x1080 canvas
            runner.Tick(TimeSpan.FromMilliseconds(500));
            var (offXA, offYA, offXB, offYB) = runner.GetSlideOffsets(1920, 1080);

            Assert.Equal(-960f, offXA, 1);
            Assert.Equal(0f, offYA, 1);
            Assert.Equal(960f, offXB, 1);
            Assert.Equal(0f, offYB, 1);
        }

        [Fact]
        public void SceneTransitionRunner_StingerCutPoint_TriggersCutPointEvent()
        {
            var settings = new TransitionSettings
            {
                Kind = TransitionKind.Stinger,
                Duration = TimeSpan.FromMilliseconds(1000),
                StingerCutPoint = TimeSpan.FromMilliseconds(400)
            };

            var runner = new SceneTransitionRunner(settings);
            bool cutPointTriggered = false;
            bool completedTriggered = false;

            runner.OnCutPointTriggered += () => cutPointTriggered = true;
            runner.OnTransitionCompleted += () => completedTriggered = true;

            // Tick 200ms -> before cut point
            runner.Tick(TimeSpan.FromMilliseconds(200));
            Assert.False(cutPointTriggered);
            Assert.False(runner.CutPointReached);

            // Tick 250ms more (total 450ms) -> triggers cut point
            runner.Tick(TimeSpan.FromMilliseconds(250));
            Assert.True(cutPointTriggered);
            Assert.True(runner.CutPointReached);
            Assert.False(completedTriggered);

            // Tick remaining 600ms -> triggers complete
            runner.Tick(TimeSpan.FromMilliseconds(600));
            Assert.True(completedTriggered);
            Assert.True(runner.IsComplete);
        }

        [Fact]
        public void SceneTransitionRunner_LumaWipeParameters_CalculatesSoftnessThreshold()
        {
            var settings = new TransitionSettings
            {
                Kind = TransitionKind.LumaWipe,
                Duration = TimeSpan.FromMilliseconds(1000),
                Easing = EasingType.Linear,
                LumaSoftness = 0.10f
            };

            var runner = new SceneTransitionRunner(settings);
            runner.Tick(TimeSpan.FromMilliseconds(500)); // 0.5 progress

            var (threshold, softness) = runner.GetLumaWipeParameters();
            // threshold = t * (1.0 + softness) = 0.5 * 1.10 = 0.55
            Assert.Equal(0.55f, threshold, 2);
            Assert.Equal(0.10f, softness, 2);
        }
    }
}
