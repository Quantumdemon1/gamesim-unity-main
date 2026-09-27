using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The five beats before the first competition, as the season records them.
    ///
    /// <para>The field has existed since schema 8 with nothing that wrote to it. What matters here is
    /// the pair of properties that make a cinematic bearable: it never plays twice, and it cannot
    /// change who wins the first competition.</para>
    /// </summary>
    public sealed class OpeningBeatTests
    {
        [Test]
        public void ASeasonStartsHavingSeenNothing()
        {
            Assert.That(Season().openingBeatsSeen, Is.Empty);
        }

        [Test]
        public void TheBeatsAreTheFiveTheSourcePlaysInOrder()
        {
            CollectionAssert.AreEqual(
                new[] { "intro", "house-entry", "house-walk-in", "tutorial", "meet-and-greet" },
                OpeningBeat.InOrder);
            Assert.That(OpeningBeat.IsKnown("intro"), Is.True);
            Assert.That(OpeningBeat.IsKnown("credits"), Is.False);
            Assert.That(OpeningBeat.IsKnown(null), Is.False);
        }

        [Test]
        public void AFinishedBeatIsRecorded()
        {
            var engine = new EpisodeEngine(Season());
            Assert.That(Mark(engine, OpeningBeat.Intro).accepted, Is.True);
            CollectionAssert.AreEqual(new[] { OpeningBeat.Intro }, engine.Snapshot.openingBeatsSeen);
        }

        /// <summary>The whole reason the field exists: an intro that replays every load is worse
        /// than no intro at all.</summary>
        [Test]
        public void ABeatIsNeverRecordedTwice()
        {
            var engine = new EpisodeEngine(Season());
            Mark(engine, OpeningBeat.Intro);
            Mark(engine, OpeningBeat.Intro);
            Assert.That(engine.Snapshot.openingBeatsSeen.Count(beat => beat == OpeningBeat.Intro), Is.EqualTo(1));
        }

        [Test]
        public void SomethingThatIsNotABeatIsRefused()
        {
            var engine = new EpisodeEngine(Season());
            var result = Mark(engine, "credits");
            Assert.That(result.accepted, Is.False);
            Assert.That(engine.Snapshot.openingBeatsSeen, Is.Empty);
        }

        [Test]
        public void EveryBeatLeavesAValidSeason()
        {
            var engine = new EpisodeEngine(Season());
            foreach (var beat in OpeningBeat.InOrder)
                Assert.That(Mark(engine, beat).accepted, Is.True, beat);

            CollectionAssert.AreEqual(OpeningBeat.InOrder, engine.Snapshot.openingBeatsSeen);
            Assert.That(EpisodeValidation.TryValidate(engine.Snapshot, out var error), Is.True, error);
        }

        // ---------------------------------------------------------------- the meet and greet

        /// <summary>
        /// The meet-and-greet is bookkeeping like every other beat. The introductions move people,
        /// one chosen approach at a time (<see cref="IntroductionTests"/>); a mark that also warmed
        /// the whole house would pay everybody for an introduction, and pay twice for every one the
        /// player actually made.
        /// </summary>
        [Test]
        public void MarkingTheMeetAndGreetMovesNobody()
        {
            var engine = new EpisodeEngine(Season());
            var before = engine.Snapshot;
            var strangers = before.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            int eventsBefore = before.relationships.Sum(r => r.events.Count);

            Assert.That(Mark(engine, OpeningBeat.MeetAndGreet).accepted, Is.True);

            var after = engine.Snapshot;
            foreach (var id in strangers)
            {
                Assert.That(after.Score(after.playerId, id), Is.EqualTo(0), id);
                Assert.That(after.Score(id, after.playerId), Is.EqualTo(0), id);
            }
            Assert.That(after.relationships.Sum(r => r.events.Count), Is.EqualTo(eventsBefore),
                "Marking a beat puts nothing on anybody's record.");
            Assert.That(after.relationshipArcs, Is.Empty);
        }

        /// <summary>
        /// <b>The opening must not be able to re-roll a season.</b> One draw here would move every
        /// competition and every vote that follows it, which would make the difference between
        /// watching the intro and skipping it a difference in who wins.
        /// </summary>
        [Test]
        public void NoBeatSpendsTheSeasonsGenerator()
        {
            var engine = new EpisodeEngine(Season());
            uint before = engine.Snapshot.randomState;
            foreach (var beat in OpeningBeat.InOrder) Mark(engine, beat);
            Assert.That(engine.Snapshot.randomState, Is.EqualTo(before));
        }

        // ---------------------------------------------------------------- fixtures

        private static EpisodeState Season() =>
            SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 5u);

        private static CommandResult Mark(EpisodeEngine engine, string beat)
        {
            var state = engine.Snapshot;
            return engine.Apply(new EpisodeCommand
            {
                id = "beat-" + beat + "-" + state.revision,
                actorId = state.playerId,
                expectedRevision = state.revision,
                expectedPhase = state.phase,
                kind = EpisodeCommandKind.MarkOpeningBeat,
                targetId = beat,
            });
        }
    }
}
