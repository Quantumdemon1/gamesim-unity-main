// Frozen29 (SCH30): carried from Tools/SimulationTests/Frozen28/FrozenV28VoteHistoryTests.cs by name, then to schema 29
// where a case builds a whole schema-29 state or payload.
// Source-only tests for the fixed Vote read-only domain. Not executed by this stage.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Persistence.Frozen29Data;
using Gamesim.Persistence.Frozen29ReadOnly;
using NUnit.Framework;

namespace Gamesim.Tests.Frozen29ReadOnly
{
    [TestFixture]
    public sealed class FrozenV29VoteHistoryTests
    {
        private static UnifiedCommitmentState TogetherRow() => new UnifiedCommitmentState
        {
            id = "deal-vote-1", kind = UnifiedVoteTogether.Vote,
            sourcePolicy = UnifiedCommitments.DealPolicy, subtype = DealKind.VoteTogether,
            makerId = "maker", beneficiaryId = "beneficiary", reciprocal = true,
            status = DealStatus.Active
        };

        private static UnifiedVoteBallotState Ballot(string voter, string target) =>
            new UnifiedVoteBallotState { voterId = voter, targetId = target };

        [TestCase(true)]
        [TestCase(false)]
        public void TogetherComparesActualTargetsWithoutMutatingTheRow(bool equal)
        {
            var row = TogetherRow();
            var ballots = new[] { Ballot("maker", "n1"), Ballot("beneficiary", equal ? "n1" : "n2") };
            Assert.That(UnifiedVoteTogether.TryVerdict(row, ballots, out var verdict, out var error), Is.True);
            Assert.That(error, Is.Null);
            Assert.That(verdict, Is.EqualTo(equal ? DealStatus.Fulfilled : DealStatus.Broken));
            Assert.That(row.status, Is.EqualTo(DealStatus.Active));
            Assert.That(row.targetId, Is.Null);
            Assert.That(ballots[0].targetId, Is.EqualTo("n1"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MissingPartyBallotDecidesNothing(bool makerPresent)
        {
            var row = TogetherRow();
            var ballots = new[] { Ballot(makerPresent ? "maker" : "beneficiary", "n1") };
            Assert.That(UnifiedVoteTogether.TryVerdict(row, ballots, out var verdict, out var error), Is.True);
            Assert.That(verdict, Is.Null);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void InactiveTogetherStillChecksEveryBallotAndDuplicateIdentity()
        {
            var row = TogetherRow();
            row.status = DealStatus.Expired;
            var ballots = new[] { Ballot("maker", "n1"), Ballot("maker", "n2") };
            Assert.That(UnifiedVoteTogether.TryVerdict(row, ballots, out var verdict, out var error), Is.False);
            Assert.That(verdict, Is.Null);
            Assert.That(error, Is.EqualTo("Actual ballots must have unique voter identities."));
            Assert.That(row.status, Is.EqualTo(DealStatus.Expired));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PromiseKeepsNamedAndStoryNullTargetDistinction(bool storyNull)
        {
            var row = TogetherRow();
            row.sourcePolicy = UnifiedCommitments.PromisePolicy;
            row.subtype = null; row.reciprocal = false;
            row.targetId = storyNull ? null : "n1";
            row.origin = storyNull ? UnifiedCommitments.StoryPromise : null;
            var ballots = new[] { Ballot("maker", "n1") };
            Assert.That(UnifiedVoteObligations.TryVerdict(row, ballots, new[] { "n1", "n2" }, out var verdict, out var error), Is.True);
            Assert.That(error, Is.Null);
            Assert.That(verdict.Status, Is.EqualTo(storyNull ? DealStatus.Broken : DealStatus.Fulfilled));
            Assert.That(verdict.ActorId, Is.EqualTo("maker"));
            Assert.That(row.status, Is.EqualTo(DealStatus.Active));
        }

        [TestCase(true, true, false, "beneficiary", false)]
        [TestCase(true, false, false, null, false)]
        [TestCase(false, false, true, "beneficiary", false)]
        [TestCase(false, false, false, "maker", true)]
        public void TargetedDealUsesPartyOrderNotPrivateBoxOrder(bool evict, bool makerTargetsNamed,
            bool beneficiaryTargetsNamed, string actor, bool kept)
        {
            var row = TogetherRow(); row.subtype = evict ? DealKind.VoteEvict : DealKind.VoteSave; row.targetId = "n1";
            var ballots = new[] {
                Ballot("beneficiary", beneficiaryTargetsNamed ? "n1" : "n2"),
                Ballot("maker", makerTargetsNamed ? "n1" : "n2")
            };
            Assert.That(UnifiedVoteObligations.TryVerdict(row, ballots, new[] { "n1", "n2" }, out var verdict, out var error), Is.True);
            Assert.That(error, Is.Null);
            Assert.That(verdict.Status, Is.EqualTo(kept ? DealStatus.Fulfilled : DealStatus.Broken));
            Assert.That(verdict.ActorId, Is.EqualTo(actor));
            Assert.That(row.status, Is.EqualTo(DealStatus.Active));
        }

        [Test]
        public void OffBlockTargetDoesNotBypassMalformedEvidence()
        {
            var row = TogetherRow(); row.subtype = DealKind.VoteSave; row.targetId = "off-block";
            var ballots = new[] { Ballot("maker", "n1"), Ballot("n2", "n1") };
            Assert.That(UnifiedVoteObligations.TryVerdict(row, ballots, new[] { "n1", "n2" }, out var verdict, out var error), Is.False);
            Assert.That(verdict, Is.Null);
            Assert.That(error, Is.EqualTo("Actual Vote ballots must be unique eligible local identities targeting the final block."));
        }

        [Test]
        public void OffBlockTargetWithValidEvidenceDecidesNothing()
        {
            var row = TogetherRow(); row.subtype = DealKind.VoteSave; row.targetId = "off-block";
            Assert.That(UnifiedVoteObligations.TryVerdict(row, new[] { Ballot("maker", "n1") },
                new[] { "n1", "n2" }, out var verdict, out var error), Is.True);
            Assert.That(verdict, Is.Null); Assert.That(error, Is.Null);
        }

        [Test]
        public void DecisionAndIncidentValuesDetachRowsBallotsAndEvidence()
        {
            var row = TogetherRow();
            var frame = new UnifiedVoteRevealState { week = 3, ballots = new List<UnifiedVoteBallotState> { Ballot("maker", "n1") } };
            var decision = new UnifiedVoteDecision(row, frame, DealStatus.Broken, "maker");
            var evidence = new List<string> { "z", "a" };
            var incident = new UnifiedVoteIncident(3, "maker", "beneficiary", row.id, row.id, false, -4, evidence);
            row.id = "changed"; frame.ballots[0].targetId = "changed"; evidence[0] = "changed";
            Assert.That(decision.Record.id, Is.EqualTo("deal-vote-1"));
            Assert.That(decision.Reveal.ballots[0].targetId, Is.EqualTo("n1"));
            Assert.That(decision.SettledWeek, Is.EqualTo(3));
            Assert.That(incident.EvidenceIds, Is.EqualTo(new[] { "a", "z" }));
            Assert.That(incident.SourceConsequence, Is.EqualTo(-4));
        }

        [Test]
        public void BetrayalKeyRetainsLengthFramingAndOrdinalIdentity()
        {
            var row = TogetherRow(); row.settledWeek = 4;
            row.settlementEffectKey = UnifiedVoteHistory.Key(row, row.settledWeek);
            Assert.That(UnifiedVoteHistory.ExactKey(row), Is.True);
            row.settlementEffectKey = row.settlementEffectKey.ToUpperInvariant();
            Assert.That(UnifiedVoteHistory.ExactKey(row), Is.False);
            row.settlementEffectKey = UnifiedVoteHistory.Key(row, 4) + "\n";
            Assert.That(UnifiedVoteHistory.ExactKey(row), Is.False);
        }

        [Test]
        public void OccurrenceUsesStrictInvariantTailAndRetainsAllTieBreakFields()
        {
            var prior = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var normal = UnifiedVoteSettlement.Occurrence("deal-vote-12", true, 9);
                Assert.That(normal.sequence, Is.EqualTo(12L));
                Assert.That(normal.price, Is.EqualTo(0)); Assert.That(normal.canonical, Is.EqualTo(1)); Assert.That(normal.index, Is.EqualTo(9));
                Assert.That(UnifiedVoteSettlement.Occurrence(Negotiation.PricePrefix + "12", false, 2).price, Is.EqualTo(1));
                Assert.That(UnifiedVoteSettlement.Occurrence("deal-vote-+12", false, 2).sequence, Is.EqualTo(long.MaxValue));
                Assert.That(UnifiedVoteSettlement.Occurrence("deal-vote-9223372036854775808", false, 2).sequence, Is.EqualTo(long.MaxValue));
                Assert.That(UnifiedVoteSettlement.Occurrence(null, false, 2).sequence, Is.EqualTo(long.MaxValue));
            }
            finally { CultureInfo.CurrentCulture = prior; }
        }

        private static UnifiedVoteVerdict Candidate(string id, bool kept, double delta, UnifiedVoteAtomRole ledgerRole)
        {
            var row = TogetherRow(); row.id = id;
            return new UnifiedVoteVerdict(row, kept ? DealStatus.Fulfilled : DealStatus.Broken, "maker", delta,
                new[] { new UnifiedVoteAtom("beneficiary", "maker", delta, UnifiedVoteAtomRole.Primary) },
                new[] { new UnifiedVoteAtom("beneficiary", "maker", ledgerRole == UnifiedVoteAtomRole.Context ? 0 : delta, ledgerRole) });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RuleTwoSelectsByPolarityThenOrdinalIdAndDoesNotReorderInput(bool kept)
        {
            var weak = Candidate("c", kept, kept ? 2 : -2, UnifiedVoteAtomRole.Primary);
            var tieB = Candidate("b", kept, kept ? 5 : -5, UnifiedVoteAtomRole.Primary);
            var tieA = Candidate("a", kept, kept ? 5 : -5, UnifiedVoteAtomRole.Primary);
            var verdicts = new[] { weak, tieB, tieA };
            UnifiedVoteSettlement.Select(verdicts);
            Assert.That(weak.Owns, Is.False); Assert.That(tieB.Owns, Is.False); Assert.That(tieA.Owns, Is.True);
            Assert.That(verdicts[0], Is.SameAs(weak)); Assert.That(verdicts[1], Is.SameAs(tieB));
            var groups = UnifiedVoteSettlement.Groups(7, verdicts);
            Assert.That(groups.Count, Is.EqualTo(1));
            Assert.That(groups[0].OwnerId, Is.EqualTo("a"));
            Assert.That(groups[0].DealId, Is.EqualTo("a"));
            Assert.That(groups[0].EvidenceIds, Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(groups[0].Kept, Is.EqualTo(kept));
        }

        [Test]
        public void BrokenZeroContextFollowsOnlyItsOwningRow()
        {
            var own = Candidate("a", false, -5, UnifiedVoteAtomRole.Context);
            var other = Candidate("b", false, -2, UnifiedVoteAtomRole.Context);
            UnifiedVoteSettlement.Select(new[] { own, other });
            Assert.That(own.Ledger[0].Selected, Is.True);
            Assert.That(other.Ledger[0].Selected, Is.False);
        }

        [Test]
        public void IncidentQueriesAreEmptyOutsideModeTwoBeforeAnyStorageRead()
        {
            Assert.That(UnifiedVoteHistory.Incidents(null), Is.Empty);
            Assert.That(UnifiedVoteHistory.Fulfillments(null), Is.Empty);
            Assert.That(UnifiedVoteStore.On(null), Is.False);
            Assert.That(UnifiedVoteStore.On(new EpisodeState { unifiedCommitmentRulesVersion = 1 }), Is.False);
        }
    }
}
