using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class HouseVibeTests
    {
        private static EpisodeState Fresh()
        { var state = ContentCatalog.Create(7); state.events.Clear(); return state; }
        private static void Log(EpisodeState state, string kind, int week, params string[] audience)
        {
            var entry = new EpisodeEvent { sequence = state.nextSequence++, week = week, phase = state.phase,
                kind = kind, text = kind + " in week " + week };
            entry.audienceIds.AddRange(audience); state.events.Add(entry);
        }

        [Test]
        public void EmptyReadoutClaimsNoKnownActivityRatherThanASettledHouse()
        {
            var reading = HouseVibe.Of(Fresh());
            Assert.That(reading.Total, Is.Zero);
            Assert.That(reading.Peak, Is.EqualTo(1));
            Assert.That(reading.Fraction(0), Is.Zero);
            Assert.That(HouseVibe.Tension(reading), Is.EqualTo("Known events this week: 0"));
        }

        [Test]
        public void CategoriesAreDisjointAndUseTheirKnownTotalAsTheBarDenominator()
        {
            var state = Fresh();
            foreach (var kind in new[] { "conversation", "house-event", "competition", "alliance", "promise", "nomination" })
                Log(state, kind, state.week);
            var reading = HouseVibe.Of(state);
            Assert.That(reading.Activity, Is.EqualTo(3));
            Assert.That(reading.Commitments, Is.EqualTo(2));
            Assert.That(reading.GameStakes, Is.EqualTo(1));
            Assert.That(reading.Total, Is.EqualTo(6));
            Assert.That(reading.Fraction(3), Is.EqualTo(.5f));
            Assert.That(reading.Fraction(reading.Total), Is.EqualTo(1f));
        }

        [Test]
        public void ConflictHouseEventsAreActivityWithoutClaimsOfFunOrHarmony()
        {
            var state = Fresh(); Log(state, "house-event", state.week); Log(state, "house-event-outcome", state.week);
            var reading = HouseVibe.Of(state);
            Assert.That(reading.Activity, Is.EqualTo(2));
            Assert.That(reading.Rows().Select(row => row.Word),
                Is.EqualTo(new[] { "Conversations", "Deals & alliances", "Game moves" }));
            Assert.That(HouseVibe.Tension(reading), Is.EqualTo("Known events this week: 2"));
        }

        [Test]
        public void GameStakesDoNotInventPairwiseTension()
        {
            var state = Fresh();
            foreach (var kind in new[] { "nomination", "rumour", "backdoor", "conversation" }) Log(state, kind, state.week);
            var reading = HouseVibe.Of(state);
            Assert.That(reading.GameStakes, Is.EqualTo(3));
            Assert.That(HouseVibe.Tension(reading), Is.EqualTo("Known events this week: 4"));
        }

        [Test]
        public void LastWeekIsNotThisWeek()
        {
            var state = Fresh(); state.week = 3;
            Log(state, "conversation", 1); Log(state, "nomination", 2); Log(state, "alliance", 3);
            var reading = HouseVibe.Of(state);
            Assert.That(reading.Activity, Is.Zero); Assert.That(reading.GameStakes, Is.Zero);
            Assert.That(reading.Commitments, Is.EqualTo(1));
        }

        [Test]
        public void PrivateNpcEventsCannotChangeTheReadout()
        {
            var state = Fresh(); var other = state.contestants.First(actor => actor.id != state.playerId).id;
            Log(state, "scheme", state.week, other);
            Log(state, "rumour", state.week, other, state.playerId);
            Log(state, "conversation", state.week);
            var reading = HouseVibe.Of(state);
            Assert.That(reading.GameStakes, Is.EqualTo(1)); Assert.That(reading.Activity, Is.EqualTo(1));
            Assert.That(reading.Total, Is.EqualTo(2));
        }

        [Test]
        public void NewPublicEventKindsStillCountAsKnownActivity()
        {
            var state=Fresh(); Log(state,"new-public-story-kind",state.week);
            var reading=HouseVibe.Of(state);
            Assert.That(reading.Activity,Is.EqualTo(1)); Assert.That(reading.Total,Is.EqualTo(1));
        }
    }
}
