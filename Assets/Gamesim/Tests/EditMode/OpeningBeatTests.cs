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
        /// The one beat with a consequence: the player stops being a stranger to everybody.
        /// </summary>
        [Test]
        public void TheMeetAndGreetWarmsTheWholeHouseToYou()
        {
            var engine = new EpisodeEngine(Season());
            var before = engine.Snapshot;
            var strangers = before.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            Assert.That(strangers.All(id => before.Score(before.playerId, id) == 0), Is.True,
                "Nobody has met anybody yet.");

            Mark(engine, OpeningBeat.MeetAndGreet);

            var after = engine.Snapshot;
            foreach (var id in strangers)
            {
                Assert.That(after.Score(after.playerId, id),
                    Is.EqualTo(EpisodeEngine.FirstImpressionImpact).Within(0.001), id);
                Assert.That(after.Score(id, after.playerId),
                    Is.EqualTo(EpisodeEngine.FirstImpressionImpact).Within(0.001),
                    "Meeting is mutual, and symmetric because there is no roll here.");
            }
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

        [Test]
        public void MeetingTheHouseHappensOnceHoweverOftenItIsAsked()
        {
            var engine = new EpisodeEngine(Season());
            Mark(engine, OpeningBeat.MeetAndGreet);
            var once = engine.Snapshot;
            Mark(engine, OpeningBeat.MeetAndGreet);

            var twice = engine.Snapshot;
            foreach (var other in twice.Active.Where(c => !c.isPlayer))
                Assert.That(twice.Score(twice.playerId, other.id),
                    Is.EqualTo(once.Score(once.playerId, other.id)).Within(0.001));
        }

        [Test]
        public void MeetingSomeoneIsOnTheRecordAndFades()
        {
            var engine = new EpisodeEngine(Season());
            Mark(engine, OpeningBeat.MeetAndGreet);

            var state = engine.Snapshot;
            var other = state.Active.First(c => !c.isPlayer);
            var met = state.relationships
                .Single(r => r.fromId == state.playerId && r.toId == other.id)
                .events.Where(e => e.type == "met").ToList();

            Assert.That(met, Has.Count.EqualTo(1));
            Assert.That(met[0].decayable, Is.True,
                "Having been introduced is ordinary social traffic, not something anybody did to anybody.");
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
