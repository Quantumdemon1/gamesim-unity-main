using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Weekly rules ported from the reference build, asserted against its actual numbers.
    ///
    /// <para>Each of these was either absent or a literal that happened to be right for one house
    /// size. They are grouped here rather than spread across the existing suites because what they
    /// have in common is provenance: every value is the source's, and a test that does not say so
    /// invites someone to "fix" it later.</para>
    /// </summary>
    public sealed class WeeklyRuleParityTests
    {
        // ---------------------------------------------------------------- the action budget

        /// <summary>
        /// <c>Math.ceil(activeCount / 2)</c>. It read as a flat eighteen, which was not the rule for
        /// any house size — a six-person house should get three.
        /// </summary>
        // Twelve is the top because that is a roster's size; SeasonBuilder will not pad a regular
        // season with all-stars to reach sixteen, and a fabricated house would be testing the
        // fixture rather than the rule.
        [TestCase(12, 6)] [TestCase(11, 6)] [TestCase(9, 5)] [TestCase(8, 4)]
        [TestCase(7, 4)] [TestCase(6, 3)] [TestCase(5, 3)] [TestCase(4, 2)] [TestCase(3, 2)]
        public void TheBudgetIsHalfTheActiveHouseRoundedUp(int active, int expected)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 12 }, 1u);
            foreach (var evicted in state.contestants.Where(c => !c.isPlayer).Skip(active - 1))
                evicted.status = ContestantStatus.Evicted;

            Assert.That(state.Active.Count(), Is.EqualTo(active));
            Assert.That(EpisodeEngine.SocialActionBudget(state), Is.EqualTo(expected));
        }

        [Test]
        public void TheBudgetIsSpentDownAndRefusesTheActionAfterIt()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(4242));
            var state = engine.Snapshot;
            Assert.That(state.phase, Is.EqualTo(EpisodePhase.Social));

            int budget = EpisodeEngine.SocialActionBudget(state);
            Assert.That(budget, Is.EqualTo(3), "Six active houseguests give three actions.");

            var partner = state.contestants.First(c => !c.isPlayer).id;
            for (int spent = 0; spent < budget; spent++)
            {
                var accepted = engine.Apply(Talk(engine.Snapshot, partner));
                Assert.That(accepted.accepted, Is.True, "Action " + (spent + 1) + ": " + accepted.reason);
                Assert.That(EpisodeEngine.SocialActionsSpent(engine.Snapshot), Is.EqualTo(spent + 1));
            }

            var refused = engine.Apply(Talk(engine.Snapshot, partner));
            Assert.That(refused.accepted, Is.False, "The action after the budget must be refused.");
            Assert.That(refused.reason, Does.Contain("social window"));
        }

        /// <summary>
        /// Campaigning is not the social week. Those actions draw on the same budget but are counted
        /// apart, because the in-phase counter resets with the phase and this one has to survive it.
        /// </summary>
        [Test]
        public void CampaignActionsSpendTheSameBudgetOnASeparateCounter()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(4242));
            int guard = 0;
            while (engine.Snapshot.phase != EpisodePhase.Campaign && guard++ < 60)
            {
                var step = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(step.accepted, Is.True, step.reason);
            }
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Campaign), "The season should reach campaigning.");

            var state = engine.Snapshot;
            int before = state.socialActions;
            var partner = state.Active.First(c => !c.isPlayer && !state.nominees.Contains(c.id)).id;

            var result = engine.Apply(Talk(state, partner));
            Assert.That(result.accepted, Is.True, result.reason);

            var after = engine.Snapshot;
            Assert.That(after.socialActions, Is.EqualTo(before), "The in-phase counter must not move outside the social week.");
            Assert.That(after.outOfPhaseSocialActions, Is.EqualTo(1));
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(before + 1),
                "Both counters draw on one budget.");
        }

        [Test]
        public void BothCountersRefillWhenTheWeekTurns()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(4242));
            int guard = 0;
            while (engine.Snapshot.week == 1 && guard++ < 120)
            {
                var step = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(step.accepted, Is.True, step.reason);
            }

            var state = engine.Snapshot;
            Assert.That(state.week, Is.GreaterThan(1), "The season should have reached a second week.");
            Assert.That(state.socialActions, Is.Zero);
            Assert.That(state.outOfPhaseSocialActions, Is.Zero, "A new week refills the whole budget.");
        }

        // ---------------------------------------------------------------- the Final 4 veto

        /// <summary>
        /// A veto holder who is not on the block may not use it at four. The case that matters is
        /// the Head of Household holding it: the other safe player is blocked by arithmetic already,
        /// because there is no one left to name as a replacement.
        /// </summary>
        [Test]
        public void AtFinalFourAVetoHolderOffTheBlockCannotUseIt()
        {
            var state = FinalFour();
            var safe = state.Active.Single(c => c.id != state.hohId && !state.nominees.Contains(c.id));

            state.vetoHolderId = state.hohId;
            Assert.That(EpisodeEngine.VetoIsLockedAtFinalFour(state), Is.True,
                "The Head of Household is not on the block, so their veto is locked.");
            Assert.That(EpisodeEngine.NpcVetoSave(state), Is.Null,
                "A locked veto saves nobody, or Advance would commit a decision the rule rejects.");

            state.vetoHolderId = safe.id;
            Assert.That(EpisodeEngine.VetoIsLockedAtFinalFour(state), Is.True);

            state.vetoHolderId = state.nominees[0];
            Assert.That(EpisodeEngine.VetoIsLockedAtFinalFour(state), Is.False,
                "A nominee holding the veto may always use it on themselves.");
        }

        [Test]
        public void TheLockAppliesOnlyAtFour()
        {
            var state = FinalFour();
            state.vetoHolderId = state.hohId;
            Assert.That(EpisodeEngine.VetoIsLockedAtFinalFour(state), Is.True);

            // Put someone back in the house and the ordinary rule returns.
            state.contestants.First(c => c.status != ContestantStatus.Active).status = ContestantStatus.Active;
            Assert.That(state.Active.Count(), Is.EqualTo(5));
            Assert.That(EpisodeEngine.VetoIsLockedAtFinalFour(state), Is.False);
        }

        /// <summary>
        /// Four active leaves exactly one eligible voter, so the single vote decides the eviction.
        /// The rule already held by arithmetic; this pins it so a change to <c>Voters</c> cannot
        /// quietly reintroduce a second ballot.
        /// </summary>
        [Test]
        public void AtFinalFourExactlyOneHouseguestVotes()
        {
            var state = FinalFour();
            Assert.That(EpisodeEngine.Voters(state).Count(), Is.EqualTo(1));
        }

        // ---------------------------------------------------------------- fixtures

        /// <summary>A four-active house with a Head of Household and two nominees.</summary>
        private static EpisodeState FinalFour()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 5u);
            foreach (var evicted in state.contestants.Where(c => !c.isPlayer).Skip(3))
                evicted.status = ContestantStatus.Evicted;
            Assert.That(state.Active.Count(), Is.EqualTo(4));

            var active = state.Active.ToList();
            state.hohId = active[0].id;
            state.nominees = new List<string> { active[1].id, active[2].id };
            return state;
        }

        private static EpisodeCommand Talk(EpisodeState state, string targetId) => new EpisodeCommand
        {
            id = "parity-" + state.revision,
            kind = EpisodeCommandKind.Talk,
            actorId = state.playerId,
            targetId = targetId,
            expectedPhase = state.phase,
            expectedRevision = state.revision,
        };
    }
}
