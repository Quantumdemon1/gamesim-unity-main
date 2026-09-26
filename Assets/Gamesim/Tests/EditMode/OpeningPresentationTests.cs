using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The two rules of the opening that are arithmetic rather than presentation: which music plays
    /// under which beat, and how long a front-door reveal takes for a house of a given size. Both
    /// are pure functions, so they are pinned here without a scene, a camera or a clock.
    /// </summary>
    public sealed class OpeningPresentationTests
    {
        /// <summary>
        /// The theme plays under the intro and nowhere else: the season's bed from the house entry
        /// on, and silence with the music off or a setup screen up. The reference build plays its
        /// theme under the intro only; played under all five beats, its sixteen-second fanfare
        /// looped for minutes under the introductions, which wait on the player.
        /// </summary>
        [Test]
        public void MusicPlaysTheThemeUnderTheIntroOnly()
        {
            Assert.That(EpisodeDirector.MusicFor(true, false, true, OpeningBeat.Intro), Is.EqualTo(HouseAudio.Music.Theme),
                "The theme plays under the intro.");

            foreach (var beat in new[] { OpeningBeat.HouseEntry, OpeningBeat.WalkIn, OpeningBeat.Tutorial, OpeningBeat.MeetAndGreet })
                Assert.That(EpisodeDirector.MusicFor(true, false, true, beat), Is.EqualTo(HouseAudio.Music.Season),
                    "The season's bed plays under " + beat + ".");
            Assert.That(EpisodeDirector.MusicFor(true, false, false, OpeningBeat.Intro), Is.EqualTo(HouseAudio.Music.Season),
                "A beat name left over from an opening that is no longer playing is not the intro.");
            Assert.That(EpisodeDirector.MusicFor(true, false, false, null), Is.EqualTo(HouseAudio.Music.Season),
                "The house plays the season's bed.");

            Assert.That(EpisodeDirector.MusicFor(false, false, true, OpeningBeat.Intro), Is.EqualTo(HouseAudio.Music.Silent),
                "Music off is silence, even under the intro.");
            Assert.That(EpisodeDirector.MusicFor(false, false, false, null), Is.EqualTo(HouseAudio.Music.Silent));
            Assert.That(EpisodeDirector.MusicFor(true, true, true, OpeningBeat.Intro), Is.EqualTo(HouseAudio.Music.Silent),
                "A setup screen is silent, even over the intro.");
            Assert.That(EpisodeDirector.MusicFor(true, true, false, null), Is.EqualTo(HouseAudio.Music.Silent));
        }

        /// <summary>
        /// A reveal waits the reference build's 2.8 s at the door and holds its houseguest on the
        /// mark for a second up to a house of eight, then shortens in a straight line to 2.0 s and
        /// 0.4 s at sixteen - so a big house does not spend a minute and a half at the front door,
        /// and a small one is not hurried.
        /// </summary>
        [Test]
        public void TheRevealShortensForALargeHouse()
        {
            var cases = new[]
            {
                new { house = 8, doorAt = 2.8f, markHold = 1.0f },
                new { house = 12, doorAt = 2.4f, markHold = 0.7f },
                new { house = 16, doorAt = 2.0f, markHold = 0.4f },
                new { house = 4, doorAt = 2.8f, markHold = 1.0f },
            };
            foreach (var expected in cases)
            {
                OpeningSequence.RevealTiming(expected.house, out float doorAt, out float markHold);
                Assert.That(doorAt, Is.EqualTo(expected.doorAt).Within(1e-4f), "The wait at the door for a house of " + expected.house + ".");
                Assert.That(markHold, Is.EqualTo(expected.markHold).Within(1e-4f), "The hold on the mark for a house of " + expected.house + ".");
            }
        }
    }
}
