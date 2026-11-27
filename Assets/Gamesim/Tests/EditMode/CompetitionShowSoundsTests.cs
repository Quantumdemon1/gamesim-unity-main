using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// A staged competition's sounds (AU-13), read from its timeline: the horn starts a course and a
    /// table and calls time, a podium's consoles click in with the hands that press them, and each
    /// reveal sounds once - the player's own verdict while they are in it. Every one is the sound of
    /// the apparatus on screen, played as effects under the news. Unity-free.
    /// </summary>
    public sealed class CompetitionShowSoundsTests
    {
        private static readonly string[] Five = { "w", "b", "p", "d", "e" };

        private static List<string> Asked(CompetitionShowTimeline timeline, CompetitionShowBeatKind kind, string playerId = null)
            => timeline.Beats.Where(beat => beat.Kind == kind)
                .SelectMany(beat => CompetitionShowSounds.For(timeline, beat, playerId))
                .Select(ask => ask.Cue + "@" + ask.After.ToString("0.00", CultureInfo.InvariantCulture)).ToList();

        [Test]
        public void ACourseStartsOnTheHorn_ATableStartsOnItAndHasTimeCalledWithIt_AndTheRestOfTheirBeatsAreSilent()
        {
            var course = CompetitionShowTimeline.For(CompetitionShowFamily.Course, Five, false);
            Assert.That(Asked(course, CompetitionShowBeatKind.Go), Is.EqualTo(new[] { "ShowHorn@0.00" }), "One horn for the whole field's start.");
            foreach (var kind in new[] { CompetitionShowBeatKind.Depart, CompetitionShowBeatKind.Turn, CompetitionShowBeatKind.TurnToWinner, CompetitionShowBeatKind.Card })
                Assert.That(Asked(course, kind), Is.Empty, kind + " is silent.");
            foreach (var family in new[] { CompetitionShowFamily.Table, CompetitionShowFamily.Tower })
            {
                var table = CompetitionShowTimeline.For(family, Five, true);
                Assert.That(Asked(table, CompetitionShowBeatKind.Work), Is.EqualTo(new[] { "ShowHorn@0.00" }), family + ": work starts on the horn.");
                Assert.That(Asked(table, CompetitionShowBeatKind.TimeUp), Is.EqualTo(new[] { "ShowHorn@0.00" }), family + ": time is called on it.");
                foreach (var kind in new[] { CompetitionShowBeatKind.Finish, CompetitionShowBeatKind.Cheer, CompetitionShowBeatKind.TurnToWinner, CompetitionShowBeatKind.Card })
                    Assert.That(Asked(table, kind), Is.Empty, family + ": " + kind + " asks nothing of its own; a finisher's click comes as they finish.");
            }
            Assert.That(CompetitionShowSounds.Finished, Is.EqualTo("ShowLockIn"), "A finisher's last press clicks.");
        }

        [Test]
        public void APodiumsConsolesClickInOnceForEachMomentAHandComesDown_AsThePressesAreStaggered()
        {
            var six = new[] { "a", "b", "c", "d", "e", "f" };
            var podium = CompetitionShowTimeline.For(CompetitionShowFamily.Podium, six, false);
            var lockIns = podium.Beats.Where(beat => beat.Kind == CompetitionShowBeatKind.LockIn).ToList();
            Assert.That(CompetitionShowSounds.For(podium, lockIns.First(), null).Select(ask => ask.After).ToArray(),
                Is.EqualTo(new[] { 0f, .08f, .16f, .24f }).Within(1e-5f), "Six in: four moments, ranks four apart pressing together.");
            Assert.That(CompetitionShowSounds.For(podium, lockIns.Last(), null).Select(ask => ask.After).ToArray(),
                Is.EqualTo(new[] { 0f, .08f }).Within(1e-5f), "The final two: two clicks.");
            Assert.That(lockIns.SelectMany(beat => CompetitionShowSounds.For(podium, beat, null)).All(ask => ask.Cue == "ShowLockIn"), Is.True);
            for (int rank = 0; rank < 9; rank++)
                Assert.That(CompetitionShowSounds.LockInDelay(rank), Is.EqualTo(rank % 4 * .08f), "The press's own stagger, rank " + rank + ".");
            Assert.That(Asked(podium, CompetitionShowBeatKind.Question), Is.Empty, "A question is silent.");
            Assert.That(Asked(podium, CompetitionShowBeatKind.Cheer), Is.Empty, "The cheer leaves the fanfare to the card.");
        }

        [Test]
        public void AReveal_SoundsOnce_ThePlayersOwnVerdictWhileTheyAreIn_ThenRightOnlyForTheWinnersLast()
        {
            // Five in: four rounds, one out a round from the bottom - e, d, then p, then b.
            var podium = CompetitionShowTimeline.For(CompetitionShowFamily.Podium, Five, false);
            Assert.That(podium.Beats.Count(beat => beat.Kind == CompetitionShowBeatKind.Reveal), Is.EqualTo(4));
            Assert.That(Asked(podium, CompetitionShowBeatKind.Reveal, "p"),
                Is.EqualTo(new[] { "ShowRight@0.00", "ShowRight@0.00", "ShowWrong@0.00", "ShowRight@0.00" }),
                "The player comes through twice and goes out on the third; the last leaves the winner alone.");
            Assert.That(Asked(podium, CompetitionShowBeatKind.Reveal, "w"), Is.EqualTo(Enumerable.Repeat("ShowRight@0.00", 4).ToArray()),
                "The winner comes through every reveal.");
            Assert.That(Asked(podium, CompetitionShowBeatKind.Reveal),
                Is.EqualTo(new[] { "ShowWrong@0.00", "ShowWrong@0.00", "ShowWrong@0.00", "ShowRight@0.00" }),
                "Watched: wrong as each goes out, right as the winner is left alone.");
            Assert.That(Asked(podium, CompetitionShowBeatKind.Reveal, "nobody-in-it"), Is.EqualTo(Asked(podium, CompetitionShowBeatKind.Reveal)));
        }

        [Test]
        public void EveryShowSoundIsAMotionOnScreen_PlayedAsEffects_UnderTheNews()
        {
            Assert.That(CompetitionShowSounds.Cues, Is.EqualTo(new[] { "ShowHorn", "ShowLockIn", "ShowRight", "ShowWrong" }));
            foreach (var cue in CompetitionShowSounds.Cues)
            {
                var row = SoundPictures.Find(cue);
                Assert.That(row, Is.Not.Null, cue + " has its row.");
                Assert.That(row.Kind, Is.EqualTo(SoundKind.World), cue + " is the sound of the apparatus on screen, not news: the card carries that.");
                Assert.That(row.Channel, Is.EqualTo(SoundChannel.Effects), cue + " follows the Effects volume.");
                Assert.That(row.GainDb, Is.LessThan(0f), cue + " sits under the news.");
            }
        }
    }
}
