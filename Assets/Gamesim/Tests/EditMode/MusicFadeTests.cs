using System;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The music's fades, as numbers: the reference build's straight-line ramps at a constant rate -
    /// the theme 1.5 s each way, the season's bed 2 s up and 1.5 s down - however the time arrives,
    /// a sixtieth of a second at a time or a headless frame's half-millisecond. The fade is pure
    /// arithmetic, so it is pinned here without an audio device or a clock; the PlayMode side
    /// (EpisodePlayModeTests.Music) pins what the house does with it.
    /// </summary>
    public sealed class MusicFadeTests
    {
        /// <summary>The frame lengths every ramp is run at: 60 and 30 fps, coarse steps, and a batchmode frame.</summary>
        private static readonly float[] Frames = { 1f / 60f, 1f / 30f, 0.1f, 0.25f, 0.00043f };

        /// <summary>
        /// <paramref name="level"/> run toward <paramref name="target"/> for <paramref name="total"/>
        /// seconds in steps of <paramref name="frame"/>, the last step whatever is left over.
        /// </summary>
        private static float Ramp(Func<float, float, float, float> voice, float level, float target, double total, float frame)
        {
            double elapsed = 0.0;
            while (elapsed < total - 1e-9)
            {
                float step = (float)Math.Min(frame, total - elapsed);
                level = voice(level, target, step);
                elapsed += step;
            }
            return level;
        }

        [Test]
        public void TheThemeTakesOneAndAHalfSecondsEachWayAtAnyFrameRate()
        {
            foreach (var frame in Frames)
            {
                Assert.That(Ramp(MusicFade.Theme, 0f, 1f, 1.5, frame), Is.EqualTo(1f), "Silence to full in 1.5 s, in steps of " + frame + " s.");
                Assert.That(Ramp(MusicFade.Theme, 0f, 1f, 1.49, frame), Is.EqualTo(1.49f / 1.5f).Within(1e-3f),
                    "and not before: a straight line, in steps of " + frame + " s.");
                Assert.That(Ramp(MusicFade.Theme, 1f, 0f, 1.5, frame), Is.EqualTo(0f), "Full to silence in 1.5 s, in steps of " + frame + " s.");
                Assert.That(Ramp(MusicFade.Theme, 1f, 0f, 1.49, frame), Is.EqualTo(0.01f / 1.5f).Within(1e-3f).And.GreaterThan(0f),
                    "and still sounding a hundredth before, in steps of " + frame + " s.");
            }
            Assert.That(MusicFade.Theme(0f, 1f, 0.75f), Is.EqualTo(0.5f).Within(1e-6f), "Half the time is half the way.");
        }

        [Test]
        public void TheSeasonBedRisesOverTwoSecondsAndFallsOverOneAndAHalf()
        {
            foreach (var frame in Frames)
            {
                Assert.That(Ramp(MusicFade.Season, 0f, 1f, 2.0, frame), Is.EqualTo(1f), "Silence to full in 2 s, in steps of " + frame + " s.");
                Assert.That(Ramp(MusicFade.Season, 0f, 1f, 1.99, frame), Is.EqualTo(1.99f / 2f).Within(1e-3f),
                    "and not before, in steps of " + frame + " s.");
                Assert.That(Ramp(MusicFade.Season, 1f, 0f, 1.5, frame), Is.EqualTo(0f), "Full to silence in 1.5 s, in steps of " + frame + " s.");
                Assert.That(Ramp(MusicFade.Season, 1f, 0f, 1.49, frame), Is.GreaterThan(0f), "and not before, in steps of " + frame + " s.");
            }
        }

        /// <summary>
        /// The rate is constant, so a fade from halfway takes half the time, and a fade turned round
        /// in the middle carries on from where it had got to: the reference build steps the volume it
        /// finds rather than restarting a curve.
        /// </summary>
        [Test]
        public void AFadeFromHalfwayTakesHalfTheTime()
        {
            foreach (var frame in Frames)
            {
                Assert.That(Ramp(MusicFade.Theme, 0.5f, 0f, 0.75, frame), Is.EqualTo(0f), "The theme from half to silence in 0.75 s, in steps of " + frame + " s.");
                Assert.That(Ramp(MusicFade.Theme, 0.5f, 0f, 0.74, frame), Is.GreaterThan(0f), "and not before, in steps of " + frame + " s.");
                Assert.That(Ramp(MusicFade.Season, 0.5f, 0f, 0.75, frame), Is.EqualTo(0f), "The bed from half to silence in 0.75 s, in steps of " + frame + " s.");
                Assert.That(Ramp(MusicFade.Season, 0.5f, 1f, 1.0, frame), Is.EqualTo(1f), "and from half back up to full in 1 s, in steps of " + frame + " s.");
                Assert.That(Ramp(MusicFade.Season, 0.5f, 1f, 0.99, frame), Is.LessThan(1f), "and not before, in steps of " + frame + " s.");
            }

            // A fade out called back in at 0.3: it rises from 0.3, not from silence and not from full.
            float turned = MusicFade.Season(1f, 0f, 1.05f);
            Assert.That(turned, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(MusicFade.Season(turned, 1f, 0.2f), Is.EqualTo(0.4f).Within(1e-5f), "It carries on from where it had got to.");
        }

        /// <summary>
        /// A fade out gives up within a thousandth of silence, as the reference build's do - and a
        /// fade in never does, or a batchmode frame's first step, smaller than the floor, would be
        /// thrown away every frame and the music would never start.
        /// </summary>
        [Test]
        public void OnlyAFadeOutSnapsToSilence()
        {
            Assert.That(MusicFade.Step(0.0015f, 0f, 1.5f, 0.0009f), Is.EqualTo(0f), "0.0009 on the way down is silence.");
            Assert.That(MusicFade.Theme(0f, 1f, 0.00075f), Is.EqualTo(0.0005f).Within(1e-7f), "0.0005 on the way up is the fade starting.");
            float level = 0f;
            for (int frame = 0; frame < 10; frame++) level = MusicFade.Theme(level, 1f, 0.00043f);
            Assert.That(level, Is.EqualTo(10 * 0.00043f / 1.5f).Within(1e-6f), "Ten headless frames move a fade in by ten frames' worth.");
        }

        [Test]
        public void TimeThatDidNotPassMovesNothing()
        {
            Assert.That(MusicFade.Theme(0.4f, 1f, 0f), Is.EqualTo(0.4f), "No time, no movement.");
            Assert.That(MusicFade.Theme(0.4f, 1f, -0.5f), Is.EqualTo(0.4f), "A clock that went backwards is ignored.");
            Assert.That(MusicFade.Theme(0.4f, 0f, float.NaN), Is.EqualTo(0.4f), "and so is one that is not a number.");
            Assert.That(MusicFade.Season(0.4f, 1f, float.NegativeInfinity), Is.EqualTo(0.4f));
            Assert.That(MusicFade.Step(0.4f, 1f, 0f, 0.01f), Is.EqualTo(1f), "A fade of no length is a cut.");
        }

        /// <summary>
        /// A frame is credited no more than a twentieth of a second, so a voice that starts after a
        /// frame stalled by a decode, or by the house being placed, rises from silence rather than
        /// entering partway up its fade. An ordinary frame, and a headless one, is credited in full.
        /// </summary>
        [Test]
        public void AFrameIsCreditedNoMoreThanATwentiethOfASecond()
        {
            Assert.That(MusicFade.LongestStep, Is.EqualTo(1f / 20f));
            Assert.That(MusicFade.FrameStep(1f / 60f), Is.EqualTo(1f / 60f), "A 60 fps frame counts in full,");
            Assert.That(MusicFade.FrameStep(1f / 30f), Is.EqualTo(1f / 30f), "a 30 fps frame too,");
            Assert.That(MusicFade.FrameStep(0.00043f), Is.EqualTo(0.00043f), "and a batchmode frame.");
            Assert.That(MusicFade.FrameStep(0.3f), Is.EqualTo(MusicFade.LongestStep), "A 300 ms stall counts a twentieth,");
            Assert.That(MusicFade.FrameStep(float.PositiveInfinity), Is.EqualTo(MusicFade.LongestStep), "and so does any longer.");
            Assert.That(MusicFade.Theme(0f, 1f, MusicFade.FrameStep(0.3f)), Is.EqualTo(0.05f / 1.5f).Within(1e-6f),
                "The theme's first step after one is a thirtieth of its rise, not a fifth.");
            Assert.That(MusicFade.Season(0f, 1f, MusicFade.FrameStep(0.3f)), Is.EqualTo(0.05f / 2f).Within(1e-6f),
                "The bed's is a fortieth.");
            Assert.That(MusicFade.Theme(0.4f, 1f, MusicFade.FrameStep(-0.5f)), Is.EqualTo(0.4f), "Time that did not pass still moves nothing");
            Assert.That(MusicFade.Theme(0.4f, 1f, MusicFade.FrameStep(float.NaN)), Is.EqualTo(0.4f), "however it arrives.");
        }

        /// <summary>
        /// The fades change how the music moves, not how loud it is: both voices peak at the one gain
        /// the port played its music at before (0.45 of the master volume). The reference build's
        /// 0.35 and 0.15 are a balance for somebody to choose by ear, not a side effect of a fade.
        /// </summary>
        [Test]
        public void BothVoicesKeepThePortsLoudness()
        {
            Assert.That(MusicFade.ThemePeak, Is.EqualTo(0.45f));
            Assert.That(MusicFade.SeasonPeak, Is.EqualTo(0.45f));
        }
    }
}
