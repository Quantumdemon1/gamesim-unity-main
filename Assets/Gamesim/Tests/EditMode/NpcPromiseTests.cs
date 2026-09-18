using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Houseguests giving each other their word.
    ///
    /// <para>The organising idea, and the thing these mostly check: promise type follows game
    /// position, not affection. A nominee begs for votes because they are on the block, not because
    /// they like whoever they are begging. The order the branches are checked in is the rule.</para>
    /// </summary>
    public sealed class NpcPromiseTests
    {
        // ---------------------------------------------------------------- position, not affection

        /// <summary>
        /// On the block, talking to somebody who is not: votes, whatever they think of them.
        /// Checked first in the source, and desperation outranking warmth is the point.
        /// </summary>
        [Test]
        public void ANomineeAsksForVotesEvenFromSomeoneTheyDislike()
        {
            var state = Nominated(out string nominee, out string safe);
            Set(state, nominee, safe, -50);

            Assert.That(NpcPromises.Offer(state, nominee, safe), Is.EqualTo(PromiseKind.Vote),
                "Being on the block outranks how they feel about the person they are asking.");
        }

        [Test]
        public void ANomineeDoesNotAskAFellowNominee()
        {
            var state = Nominated(out string nominee, out _);
            string other = state.nominees.Single(id => id != nominee);
            Assert.That(NpcPromises.Offer(state, nominee, other), Is.Not.EqualTo(PromiseKind.Vote));
        }

        /// <summary>Power outranks warmth: an unallied houseguest courts whoever holds the title.</summary>
        [Test]
        public void SomebodyUnalliedCourtsTheHeadOfHousehold()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            var cast = state.Active.Where(c => !c.isPlayer).ToList();
            state.hohId = cast[0].id;
            Set(state, cast[1].id, cast[0].id, 5);

            Assert.That(NpcPromises.Offer(state, cast[1].id, cast[0].id), Is.EqualTo(PromiseKind.Safety));
        }

        [Test]
        public void AnAllyDoesNotNeedToCourtTheHeadOfHousehold()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            var cast = state.Active.Where(c => !c.isPlayer).ToList();
            state.hohId = cast[0].id;
            Set(state, cast[1].id, cast[0].id, 5);
            state.alliances.Add(new AllianceState
            {
                id = "pact", name = "A Pact",
                members = new List<string> { cast[0].id, cast[1].id }, active = true,
            });

            Assert.That(NpcPromises.Offer(state, cast[1].id, cast[0].id), Is.Not.EqualTo(PromiseKind.Safety),
                "They already have a claim on them.");
        }

        [Test]
        public void WarmthWithoutAPactLaysGroundwork()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            var cast = state.Active.Where(c => !c.isPlayer).ToList();
            Set(state, cast[0].id, cast[1].id, NpcPromises.WarmthThreshold + 5);

            Assert.That(NpcPromises.Offer(state, cast[0].id, cast[1].id),
                Is.EqualTo(PromiseKind.AllianceLoyalty));
        }

        [Test]
        public void LukewarmHouseguestsPromiseNothing()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            var cast = state.Active.Where(c => !c.isPlayer).ToList();
            Set(state, cast[0].id, cast[1].id, NpcPromises.WarmthThreshold - 5);

            Assert.That(NpcPromises.Offer(state, cast[0].id, cast[1].id), Is.Null);
        }

        /// <summary>
        /// Final-two shopping needs both a close bond and a small house. Either alone is not enough,
        /// which is what stops the endgame promise appearing in week two.
        /// </summary>
        [Test]
        public void FinalTwoShoppingNeedsBothClosenessAndASmallHouse()
        {
            var big = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 7u);
            var bigCast = big.Active.Where(c => !c.isPlayer).ToList();
            big.alliances.Add(Pact(bigCast[0].id, bigCast[1].id));
            Set(big, bigCast[0].id, bigCast[1].id, NpcPromises.FinalTwoThreshold + 10);
            Assert.That(NpcPromises.Offer(big, bigCast[0].id, bigCast[1].id), Is.Null,
                "Ten houseguests is too early to be picking a final two.");

            var small = Shrink(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 7u),
                NpcPromises.EndgameSize);
            var smallCast = small.Active.Where(c => !c.isPlayer).ToList();
            small.alliances.Add(Pact(smallCast[0].id, smallCast[1].id));
            Set(small, smallCast[0].id, smallCast[1].id, NpcPromises.FinalTwoThreshold + 10);
            Assert.That(NpcPromises.Offer(small, smallCast[0].id, smallCast[1].id),
                Is.EqualTo(PromiseKind.FinalTwo));
        }

        [Test]
        public void NobodyShopsForASecondFinalTwo()
        {
            var state = Shrink(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 7u),
                NpcPromises.EndgameSize);
            var cast = state.Active.Where(c => !c.isPlayer).ToList();
            foreach (var other in cast.Skip(1)) Set(state, cast[0].id, other.id, NpcPromises.FinalTwoThreshold + 10);
            foreach (var other in cast.Skip(1)) state.alliances.Add(Pact(cast[0].id, other.id));

            NpcPromises.Settle(state);
            Assert.That(state.promises.Count(p => p.fromId == cast[0].id && p.kind == PromiseKind.FinalTwo),
                Is.LessThanOrEqualTo(1), "One final two at a time.");
        }

        // ---------------------------------------------------------------- the pass

        [Test]
        public void SettlingSpendsNoRandomness()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u), 50);
            uint before = state.randomState;
            NpcPromises.Settle(state);
            Assert.That(state.randomState, Is.EqualTo(before));
        }

        [Test]
        public void TheSameHouseMakesTheSameOffersEveryTime()
        {
            var first = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 11u), 50);
            var second = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 11u), 50);
            NpcPromises.Settle(first);
            NpcPromises.Settle(second);
            CollectionAssert.AreEqual(Given(first), Given(second));
        }

        [Test]
        public void NobodyGivesTheirWordTwiceInOneWeek()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 12 }, 9u), 50);
            NpcPromises.Settle(state);

            var givers = state.promises.Select(p => p.fromId).ToList();
            CollectionAssert.AreEqual(givers.Distinct().OrderBy(id => id).ToList(),
                givers.OrderBy(id => id).ToList());
        }

        [Test]
        public void ThePassStaysWithinWhatASeasonMayHold()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 12 }, 9u), 80);
            for (int week = 1; week <= 30; week++) { state.week = week; NpcPromises.Settle(state); }

            Assert.That(state.promises.Count, Is.LessThanOrEqualTo(NpcPromises.PromiseCeiling));
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
        }

        [Test]
        public void SettlingLeavesAValidSeason()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 3u), 50);
            NpcPromises.Settle(state);
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
        }

        /// <summary>Giving your word is remembered permanently, which is what trust reads.</summary>
        [Test]
        public void GivingYourWordIsRememberedPermanently()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 9u), 50);
            NpcPromises.Settle(state);
            var promise = state.promises.First();

            var entries = state.relationships
                .Single(r => r.fromId == promise.fromId && r.toId == promise.toId)
                .events.Where(e => e.type == "promise-made").ToList();
            Assert.That(entries, Is.Not.Empty);
            Assert.That(entries.All(e => !e.decayable), Is.True,
                "A word given does not fade, however many were given.");
        }

        // ---------------------------------------------------------------- fixtures

        private static EpisodeState Nominated(out string nominee, out string safe)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            var cast = state.Active.Where(c => !c.isPlayer).ToList();
            state.hohId = cast[0].id;
            state.nominees = new List<string> { cast[1].id, cast[2].id };
            nominee = cast[1].id;
            safe = cast[3].id;
            return state;
        }

        private static AllianceState Pact(string a, string b) => new AllianceState
        {
            id = "pact-" + a + "-" + b, name = "A Pact",
            members = new List<string> { a, b }, active = true,
        };

        /// <summary>The same season with people evicted until only this many remain.</summary>
        private static EpisodeState Shrink(EpisodeState state, int remaining)
        {
            foreach (var evicted in state.contestants.Where(c => !c.isPlayer).Skip(remaining - 1))
                evicted.status = ContestantStatus.Jury;
            return state;
        }

        private static EpisodeState Warm(EpisodeState state, double score)
        {
            foreach (var edge in state.relationships) edge.score = score;
            return state;
        }

        private static void Set(EpisodeState state, string from, string to, double score)
        {
            foreach (var edge in state.relationships.Where(r => r.fromId == from && r.toId == to))
                edge.score = score;
        }

        private static List<string> Given(EpisodeState state) => state.promises
            .Select(p => p.fromId + "->" + p.toId + ":" + p.kind)
            .OrderBy(entry => entry)
            .ToList();
    }
}
