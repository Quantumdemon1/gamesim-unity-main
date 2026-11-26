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

        /// <summary>
        /// A duck falls from full to its level in 0.3 s and comes back up in 0.6 s, in a straight
        /// line, whatever its depth and at any frame rate: the game menu's third and a big moment's
        /// half alike.
        /// </summary>
        [Test]
        public void ADuckFallsInThreeTenthsOfASecondAndComesBackInSixTenths()
        {
            Assert.That(MusicFade.DuckIn, Is.EqualTo(0.3f));
            Assert.That(MusicFade.DuckOut, Is.EqualTo(0.6f));
            foreach (float depth in new[] { MusicFade.PausedMusicDuck, MusicFade.MomentMusicDuck })
            {
                Func<float, float, float, float> duck = (level, target, seconds) => MusicFade.Duck(level, target, depth, seconds);
                foreach (var frame in Frames)
                {
                    string at = " (" + depth + ", in steps of " + frame + " s)";
                    Assert.That(Ramp(duck, 1f, depth, 0.3, frame), Is.EqualTo(depth), "Full to the duck's level in 0.3 s" + at);
                    Assert.That(Ramp(duck, 1f, depth, 0.29, frame), Is.GreaterThan(depth), "and not before" + at);
                    Assert.That(Ramp(duck, depth, 1f, 0.6, frame), Is.EqualTo(1f), "Back up to full in 0.6 s" + at);
                    Assert.That(Ramp(duck, depth, 1f, 0.59, frame), Is.LessThan(1f), "and not before" + at);
                }
            }
            Assert.That(MusicFade.Duck(1f, 0.35f, 0.35f, 0.15f), Is.EqualTo(0.675f).Within(1e-5f), "Half the time is half the way down.");
            Assert.That(MusicFade.Duck(0.5f, 1f, 0.5f, 0.3f), Is.EqualTo(0.75f).Within(1e-5f), "and half the way back up.");
            Assert.That(MusicFade.Duck(0.6f, 0.35f, 0.35f, 0f), Is.EqualTo(0.6f), "No time, no movement,");
            Assert.That(MusicFade.Duck(0.6f, 0.35f, 0.35f, float.NaN), Is.EqualTo(0.6f), "and none for a clock that is not a number.");
            Assert.That(MusicFade.Duck(1f, 1f, 1f, 0.1f), Is.EqualTo(1f), "Nothing asked, nothing ducked.");
        }

        /// <summary>
        /// The duck the house keeps: it turns round where it is, its way back is measured from where
        /// it went or rested, and asking for the level it is heading to again changes nothing - the
        /// director asks every frame.
        /// </summary>
        [Test]
        public void TheDuckTurnsRoundWhereItIsAndComesBackFromWhereItRested()
        {
            var duck = new MusicDuck();
            Assert.That(duck.Level, Is.EqualTo(1f), "Nothing holds the music down to begin with.");
            duck.Ask(MusicFade.PausedMusicDuck);
            duck.Tick(0.15f);
            Assert.That(duck.Level, Is.EqualTo(0.675f).Within(1e-5f), "Halfway down the game menu's duck,");
            duck.Ask(1f);
            duck.Tick(0.15f);
            Assert.That(duck.Level, Is.EqualTo(0.675f + 0.65f * 0.15f / 0.6f).Within(1e-5f), "turned round there, it carries on up from where it got to");
            duck.Tick(0.15f);
            Assert.That(duck.Level, Is.EqualTo(1f), "and is back at full 0.3 s later: half the way, half the time.");

            duck.Ask(MusicFade.PausedMusicDuck);
            duck.Ask(MusicFade.PausedMusicDuck);
            duck.Tick(0.3f);
            Assert.That(duck.Level, Is.EqualTo(MusicFade.PausedMusicDuck), "Asked twice, the duck still takes 0.3 s.");
            duck.Ask(MusicFade.MomentMusicDuck);
            duck.Tick(0.2f);
            Assert.That(duck.Level, Is.EqualTo(MusicFade.MomentMusicDuck), "The menu closed on a big moment: the duck rises to the moment's half and rests,");
            duck.Ask(1f);
            duck.Tick(0.3f);
            Assert.That(duck.Level, Is.EqualTo(0.75f).Within(1e-5f), "and the moment over, the way back from half takes the whole 0.6 s.");
            duck.Tick(0.3f);
            Assert.That(duck.Level, Is.EqualTo(1f));

            duck.Ask(float.NaN);
            Assert.That(duck.Target, Is.EqualTo(1f), "A level that is not a number is no duck.");
            duck.Tick(-1f);
            Assert.That(duck.Level, Is.EqualTo(1f));
        }

        /// <summary>
        /// What ducks what: the game menu card takes the music to about a third and the rooms' beds to
        /// half; a big moment takes the music to half and leaves the beds; the menu is the deeper of
        /// the two; and with neither - the Settings frame included - nothing is ducked.
        /// </summary>
        [Test]
        public void TheGameMenuDucksDeeperThanABigMomentAndOnlyItDucksTheBeds()
        {
            Assert.That(MusicFade.MusicDuckFor(gameMenuOpen: false, bigMoment: false), Is.EqualTo(1f), "Nothing on, nothing ducked.");
            Assert.That(MusicFade.MusicDuckFor(gameMenuOpen: true, bigMoment: false), Is.EqualTo(0.35f), "The game menu: about a third.");
            Assert.That(MusicFade.MusicDuckFor(gameMenuOpen: false, bigMoment: true), Is.EqualTo(0.5f), "A big moment: half.");
            Assert.That(MusicFade.MusicDuckFor(gameMenuOpen: true, bigMoment: true), Is.EqualTo(0.35f), "Both: the menu's, the deeper.");
            Assert.That(MusicFade.BedDuckFor(gameMenuOpen: true), Is.EqualTo(0.5f), "The rooms' beds to half under the menu,");
            Assert.That(MusicFade.BedDuckFor(gameMenuOpen: false), Is.EqualTo(1f), "and not otherwise.");
        }

        /// <summary>
        /// A competition's loop comes up in a second, so it is there by the end of the count, and goes
        /// over 1.5 s as the card comes, at any frame rate; at full it is as loud as the season's bed it
        /// takes over from, so the recording alone sets how much harder it drives (CueLoudnessTests).
        /// </summary>
        [Test]
        public void TheCompetitionLoopRisesInASecondAndFallsInOneAndAHalf()
        {
            foreach (var frame in Frames)
            {
                Assert.That(Ramp(MusicFade.Competition, 0f, 1f, 1.0, frame), Is.EqualTo(1f), "Silence to full in 1 s, in steps of " + frame + " s.");
                Assert.That(Ramp(MusicFade.Competition, 0f, 1f, 0.99, frame), Is.EqualTo(0.99f).Within(1e-3f),
                    "and not before: a straight line, in steps of " + frame + " s.");
                Assert.That(Ramp(MusicFade.Competition, 1f, 0f, 1.5, frame), Is.EqualTo(0f), "Full to silence in 1.5 s, in steps of " + frame + " s.");
                Assert.That(Ramp(MusicFade.Competition, 1f, 0f, 1.49, frame), Is.GreaterThan(0f), "and not before, in steps of " + frame + " s.");
            }
            Assert.That(MusicFade.Competition(0.5f, 0f, 0.75f), Is.EqualTo(0f), "Half the way down is half the time.");
            Assert.That(MusicFade.CompetitionPeak, Is.EqualTo(MusicFade.SeasonPeak), "At full, at the season bed's gain.");
        }

        /// <summary>
        /// The main menu's title loop comes up gently, over 2 s, and goes over 1.5 s as the player
        /// leaves the menu - so by the time the intro's theme starts it is gone - at any frame rate;
        /// at full it plays at 60 percent of the theme's gain, so the menu is quieter than the show.
        /// </summary>
        [Test]
        public void TheTitleLoopRisesInTwoSecondsAndFallsInOneAndAHalfAtSixtyPercentOfTheTheme()
        {
            foreach (var frame in Frames)
            {
                Assert.That(Ramp(MusicFade.Title, 0f, 1f, 2.0, frame), Is.EqualTo(1f), "Silence to full in 2 s, in steps of " + frame + " s.");
                Assert.That(Ramp(MusicFade.Title, 0f, 1f, 1.98, frame), Is.EqualTo(0.99f).Within(1e-3f),
                    "and not before: a straight line, in steps of " + frame + " s.");
                Assert.That(Ramp(MusicFade.Title, 1f, 0f, 1.5, frame), Is.EqualTo(0f), "Full to silence in 1.5 s, in steps of " + frame + " s.");
                Assert.That(Ramp(MusicFade.Title, 1f, 0f, 1.49, frame), Is.GreaterThan(0f), "and not before, in steps of " + frame + " s.");
            }
            Assert.That(MusicFade.Title(0.5f, 1f, 1f), Is.EqualTo(1f), "Half the way up is half the time.");
            Assert.That(MusicFade.TitlePeak, Is.EqualTo(MusicFade.ThemePeak * 0.6f).Within(1e-6f), "At full, 60 percent of the theme's gain.");
            Assert.That(MusicFade.TitlePeak, Is.LessThan(MusicFade.ThemePeak));
        }
    }
}
