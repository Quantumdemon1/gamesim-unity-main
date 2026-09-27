using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The rules of the opening that are arithmetic rather than presentation: which music plays
    /// under which beat, how long a front-door reveal takes for a house of a given size, and the
    /// reference build's timings - the loader's cap, the title's typewriter, the lower third's
    /// stagger, the door's flash and the walk-in's camera (D:/gamesim-web IntroSequence.tsx and
    /// HouseWalkInSequence.tsx). All pure, so they are pinned here without a scene, a camera or a
    /// clock; the PlayMode parity tests watch the same numbers play out.
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

        /// <summary>
        /// The loader gives the bodies the reference build's 20 s on a desktop
        /// (IntroSequence.tsx:297-303) before the show starts without them - not the 15 s of the
        /// Survivor conversion the first port was copied from.
        /// </summary>
        [Test]
        public void TheLoaderWaitsTheReferencesTwentySeconds()
        {
            Assert.That(OpeningSequence.LoadingCap, Is.EqualTo(20f));
        }

        /// <summary>
        /// The title's "Season N" types itself out as the reference build's does
        /// (IntroSequence.tsx:443-463): nothing before 1.8 s, then letter i at 1.8 + 0.06 i, never
        /// more letters than there are - and the subtitle rises in after it, at 2.4 s.
        /// </summary>
        [Test]
        public void TheSeasonTypesOutAtTheReferencesPace()
        {
            const int letters = 8; // "Season 3"
            Assert.That(OpeningSequence.SeasonLettersShown(0f, letters), Is.Zero);
            Assert.That(OpeningSequence.SeasonLettersShown(1.79f, letters), Is.Zero, "Nothing before 1.8 s.");
            Assert.That(OpeningSequence.SeasonLettersShown(1.8f, letters), Is.EqualTo(1), "The first letter at 1.8 s.");
            Assert.That(OpeningSequence.SeasonLettersShown(1.8f + 0.06f * 3f - 0.01f, letters), Is.EqualTo(3), "Three letters just before 1.98 s,");
            Assert.That(OpeningSequence.SeasonLettersShown(1.8f + 0.06f * 3f, letters), Is.EqualTo(4), "the fourth at 1.98 s.");
            Assert.That(OpeningSequence.SeasonLettersShown(1.8f + 0.06f * 7f, letters), Is.EqualTo(letters), "All eight by 2.22 s,");
            Assert.That(OpeningSequence.SeasonLettersShown(60f, letters), Is.EqualTo(letters), "and never more than there are.");
            Assert.That(OpeningSequence.SeasonLettersShown(5f, 0), Is.Zero);
            Assert.That(OpeningSequence.SubtitleAt, Is.EqualTo(2.4f), "The subtitle rises in once the season has typed.");
        }

        /// <summary>
        /// The lower third arrives piece by piece on the reference build's stagger
        /// (IntroSequence.tsx:561-623): the whole at 1.5 s, the name at 1.6 s, the face at 1.8 s,
        /// the details at 1.9 s and the count at 2.2 s.
        /// </summary>
        [Test]
        public void TheLowerThirdArrivesOnTheReferencesStagger()
        {
            CollectionAssert.AreEqual(new[] { 1.5f, 1.6f, 1.8f, 1.9f, 2.2f },
                new[] { OpeningSequence.LowerThirdAt, OpeningSequence.NameAt, OpeningSequence.PortraitAt, OpeningSequence.DetailsAt, OpeningSequence.CounterAt });
        }

        /// <summary>
        /// The white flash as a door gives is the reference build's (IntroSequence.tsx:546-558): 0.15 s
        /// long, clear to eight tenths white by 30% of the way through and clear again at the end -
        /// and never white once it is over.
        /// </summary>
        [Test]
        public void TheDoorFlashPeaksAtEightTenthsAndClears()
        {
            Assert.That(OpeningSequence.FlashSeconds, Is.EqualTo(0.15f).Within(1e-6f));
            Assert.That(OpeningSequence.FlashAlpha(0f), Is.EqualTo(0f).Within(1e-4f), "It starts clear,");
            Assert.That(OpeningSequence.FlashAlpha(0.15f), Is.EqualTo(0.4f).Within(1e-4f), "is halfway up at 15%,");
            Assert.That(OpeningSequence.FlashAlpha(0.3f), Is.EqualTo(0.8f).Within(1e-4f), "at eight tenths 30% of the way through,");
            Assert.That(OpeningSequence.FlashAlpha(0.65f), Is.EqualTo(0.4f).Within(1e-4f), "halfway down at 65%,");
            Assert.That(OpeningSequence.FlashAlpha(1f), Is.EqualTo(0f).Within(1e-4f), "and clear at the end.");
            Assert.That(OpeningSequence.FlashAlpha(2f), Is.EqualTo(0f).Within(1e-4f), "Past the end it stays clear.");
        }

        /// <summary>
        /// The walk-in's camera is the reference build's four keys on its schedule
        /// (HouseWalkInSequence.tsx:81-135): a second's push-in toward the doorway - from two metres
        /// further back and a fifth of a metre higher, ending on the doorway framing the walk-in
        /// always had - then the rise, done by 4 s, and the crane, done by 11 s.
        /// </summary>
        [Test]
        public void TheWalkInPushesInThenRisesThenCranes()
        {
            var keys = EpisodeDirector.WalkInKeys;
            Assert.That(keys, Has.Length.EqualTo(4));
            var from = keys[OpeningSequence.WalkInPushFrom];
            var doorway = keys[OpeningSequence.WalkInDoorway];
            Assert.That(doorway.Focus, Is.EqualTo(new Vector3(0f, 1.2f, 10f)), "The push-in ends on the doorway framing.");
            Assert.That(doorway.Distance, Is.EqualTo(5f));
            Assert.That(doorway.Pitch, Is.EqualTo(10f));
            Assert.That(doorway.Yaw, Is.EqualTo(15f));
            Assert.That(from.Focus, Is.EqualTo(doorway.Focus), "It keeps its eye on the doorway all the way in.");
            Assert.That(from.Yaw, Is.EqualTo(doorway.Yaw));

            float Back(HouseCameraRig.Shot shot) => shot.Distance * Mathf.Cos(shot.Pitch * Mathf.Deg2Rad);
            float Up(HouseCameraRig.Shot shot) => shot.Focus.y + shot.Distance * Mathf.Sin(shot.Pitch * Mathf.Deg2Rad);
            Assert.That(Back(from) - Back(doorway), Is.EqualTo(2f).Within(0.1f), "It starts two metres further back,");
            Assert.That(Up(from) - Up(doorway), Is.EqualTo(0.2f).Within(0.05f), "and a fifth of a metre higher.");
            Assert.That(doorway.Seconds, Is.EqualTo(1f), "The push-in takes a second.");

            OpeningSequence.WalkInSchedule(keys, out float riseAt, out float craneAt, out float settledAt);
            Assert.That(riseAt, Is.EqualTo(1f).Within(1e-4f), "The rise starts as the push-in lands,");
            Assert.That(craneAt, Is.EqualTo(4f).Within(1e-4f), "the crane at 4 s,");
            Assert.That(settledAt, Is.EqualTo(11f).Within(1e-4f), "and the camera is over the house at 11 s.");

            OpeningSequence.WalkInSchedule(new HouseCameraRig.Shot[3], out riseAt, out craneAt, out settledAt);
            Assert.That(new[] { riseAt, craneAt, settledAt }, Is.All.EqualTo(0f), "Too few keys to crane: nothing is scheduled.");
        }
    }
}
