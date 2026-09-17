using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The house is no longer fixed at six.
    ///
    /// <para>Validation used to reject any cast that was not exactly six, which made this a demo of
    /// one scenario rather than the game the web version is — that one defaults to eight and lets
    /// the player choose. These pin the new boundary, and pin the one rule that had been hiding
    /// inside the old one: the veto competition seats six, which is indistinguishable from "everyone
    /// plays" in a six-person house and quite different in a larger one.</para>
    /// </summary>
    public sealed class CastSizeTests
    {
        private static EpisodeState House(int size)
        {
            var state = ContentCatalog.Create(4242);
            while (state.contestants.Count > size)
            {
                var last = state.contestants[state.contestants.Count - 1];
                state.contestants.Remove(last);
                state.relationships.RemoveAll(r => r.fromId == last.id || r.toId == last.id);
                state.memories.RemoveAll(m => m.ownerId == last.id || m.subjectId == last.id);
                foreach (var e in state.events) e.audienceIds.Remove(last.id);
            }
            while (state.contestants.Count < size)
            {
                int index = state.contestants.Count;
                state.contestants.Add(new ContestantState
                {
                    id = "extra-" + index,
                    name = "Extra " + index,
                    pronouns = "they/them",
                    homeRoom = "Living",
                    motive = "Added by a test to prove the house is not fixed at six.",
                    status = ContestantStatus.Active,
                    traits = new List<string> { "Social" },
                    stats = new ContestantStats(),
                });
            }
            return state;
        }

        [Test]
        public void AHouseOfEightValidates()
        {
            Assert.That(EpisodeValidation.TryValidate(House(8), out var error), Is.True,
                "Eight is the web game's default house size: " + error);
        }

        [Test]
        public void TheOriginalSixStillValidates()
        {
            Assert.That(EpisodeValidation.TryValidate(ContentCatalog.Create(4242), out var error), Is.True, error);
        }

        [Test]
        public void EveryAcceptedSizeValidates()
        {
            for (int size = EpisodeValidation.MinimumCast; size <= EpisodeValidation.MaximumCast; size++)
                Assert.That(EpisodeValidation.TryValidate(House(size), out var error), Is.True,
                    "A house of " + size + " was rejected: " + error);
        }

        [Test]
        public void AHouseSmallerThanTheFinalThreeIsRejected()
        {
            Assert.That(EpisodeValidation.TryValidate(House(EpisodeValidation.MinimumCast - 1), out _), Is.False,
                "A season cannot start already past its own Final Three.");
        }

        [Test]
        public void AnAbsurdlyLargeHouseIsRejected()
        {
            Assert.That(EpisodeValidation.TryValidate(House(EpisodeValidation.MaximumCast + 1), out _), Is.False);
        }

        /// <summary>
        /// The rule the six-person house was concealing: six seats, not "everyone".
        /// </summary>
        [Test]
        public void TheVetoSeatsSixOrTheWholeHouseWhenItIsSmaller()
        {
            Assert.That(EpisodeEngine.VetoPlayerCount(12), Is.EqualTo(6));
            Assert.That(EpisodeEngine.VetoPlayerCount(8), Is.EqualTo(6));
            Assert.That(EpisodeEngine.VetoPlayerCount(6), Is.EqualTo(6));
            Assert.That(EpisodeEngine.VetoPlayerCount(5), Is.EqualTo(5));
            Assert.That(EpisodeEngine.VetoPlayerCount(4), Is.EqualTo(4));
        }

        /// <summary>
        /// Why no existing save needed migrating: at six active or fewer the old rule and the new
        /// one pick the same lineup, so every season already on disk stays valid.
        /// </summary>
        [Test]
        public void TheNewVetoRuleAgreesWithTheOldOneAtSixOrFewer()
        {
            for (int active = 2; active <= 6; active++)
                Assert.That(EpisodeEngine.VetoPlayerCount(active), Is.EqualTo(active),
                    "At " + active + " active the whole house should still play, as it always did.");
        }

        [Test]
        public void TheCapsThatWereDerivedFromSixNowFollowTheCast()
        {
            var eight = House(8);
            // A directed graph over eight people holds 56 edges; the old cap was 36, which is 6x6.
            for (int i = 0; i < eight.contestants.Count; i++)
            for (int j = 0; j < eight.contestants.Count; j++)
            {
                if (i == j) continue;
                string from = eight.contestants[i].id, to = eight.contestants[j].id;
                if (!eight.relationships.Any(r => r.fromId == from && r.toId == to))
                    eight.relationships.Add(new RelationshipState { fromId = from, toId = to, score = 0 });
            }
            Assert.That(eight.relationships.Count, Is.EqualTo(8 * 7));
            Assert.That(EpisodeValidation.TryValidate(eight, out var error), Is.True,
                "A complete relationship graph for eight people was rejected: " + error);
        }
    }
}
