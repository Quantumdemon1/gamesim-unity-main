using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Detached whole-state compatibility controls, NOT a mode2 factory, importer, public game,
    /// save or actual canonical producer. Public0/1 games supply the unmodified episode context
    /// and real regular private boxes. Each additional record/reference is explicitly synthetic.
    /// The split box is constructed (<see cref="Split"/>): its NPC ballots are pinned after the
    /// real batch, and the source counts, reveals and records it.
    /// </summary>
    public sealed class UnifiedVoteProspectiveWholeStateTests
    {
        [TestCase(false)] [TestCase(true)]
        public void CompleteCoreAcceptsSourceShapedImmediateAndReconciledOpportunitiesOnly(bool reconciled)
        {
            var s = Opening(); var row = OpeningDeal(s); s.unifiedCommitments.Add(row);
            var opportunity = Opportunity(s, row, reconciled); s.ledger.opportunities.Add(opportunity);
            Check(s, true);
            Assert.That(EpisodeValidation.TryValidate(s, out _), Is.False);
            Assert.Throws<ArgumentException>(() => new EpisodeEngine(s));
            Assert.That(UnifiedCommitments.RulesOn(s), Is.False);
            Assert.That(UnifiedCommitmentHearings.RulesOn(s), Is.False);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        public void TypedVoteOpportunityRejectsSingleChangedSourceScalar(int defect)
        {
            var s = Opening(); var row = OpeningDeal(s); s.unifiedCommitments.Add(row);
            var opportunity = Opportunity(s, row, true); s.ledger.opportunities.Add(opportunity); Check(s, true);
            switch (defect)
            {
                case 0: opportunity.kind = OpportunityKinds.Vote; break;
                case 1: opportunity.source = DealKind.SafetyAgreement; break;
                case 2: opportunity.note = "offered by " + row.beneficiaryId + ", " + DealStatus.Active; break;
                case 3: opportunity.note = "put to " + row.beneficiaryId + ", " + DealStatus.Proposed; break;
                case 4: opportunity.note = "put to " + row.beneficiaryId + ", " + DealStatus.Fulfilled; break;
                case 5: opportunity.anchor = "social"; break;
                case 6: opportunity.currency = PayoffCurrency.Trust; break;
                case 7: opportunity.payoff = 1; break;
                case 8: opportunity.steps.Add(new OpportunityStep()); break;
                case 9: opportunity.response = OpportunityResponse.Ignored; break;
                case 10: opportunity.outcome = OpportunityOutcome.Won; break;
                default: opportunity.note = null; break;
            }
            Assert.That(UnifiedVoteFamilyValidation.TryValidate(s, s.unifiedVoteReveals, out var error), Is.True, error);
            Check(s, false);
        }

        [TestCase(false)] [TestCase(true)]
        public void PrivateVoteTruthCannotBecomeASafetyBrokenWordOrHearing(bool hearing)
        {
            var s = Opening(); var row = OpeningDeal(s); s.unifiedCommitments.Add(row); Check(s, true);
            var fact = new HouseFactState { id = "prospective-vote-fact", kind = FactKinds.BrokenWord,
                actorId = row.makerId, subjectId = row.beneficiaryId, refId = row.id,
                week = s.week, visibility = FactVisibility.Private, knowers = new List<string> { s.playerId } };
            if (hearing) s.unifiedHearingEvidence.Add(new UnifiedHearingEvidenceState { incidentKey = "not-a-safety-incident", fact = fact });
            else s.story.facts.Add(fact);
            Assert.That(UnifiedVoteFamilyValidation.TryValidate(s, s.unifiedVoteReveals, out var error), Is.True, error);
            Check(s, false);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void CompleteCoreRefusesMalformedStoryContainersBeforeProspectiveReaders(int defect)
        {
            var s = Opening(); Check(s, true);
            switch (defect)
            {
                case 0: s.houseEvents = null; break;
                case 1: s.houseEvents.Add(null); break;
                case 2: s.storylines = null; break;
                case 3: s.storylines.Add(null); break;
                case 4: s.activeModifiers = null; break;
                default: s.activeModifiers.Add(null); break;
            }
            Assert.DoesNotThrow(() => Check(s, false));
        }

        [TestCase(DealKind.VoteSave)] [TestCase(DealKind.VoteEvict)]
        public void ActualPrivateArchiveDoesNotGrantPartnerKnowledgeOrHideOwnBreach(string subtype)
        {
            var witness = Split(); var s = Prospect(witness.State, witness.Frames); Check(s, true);
            var frame = s.unifiedVoteReveals.Single();
            var own = frame.ballots.Single(ballot => ballot.voterId == s.playerId);
            string otherTarget = s.ledger.power.Single(power => power.week == frame.week).nominees.Single(id => id != own.targetId);
            var row = TerminalDeal(s, witness.PartnerId, subtype,
                subtype == DealKind.VoteSave ? own.targetId : otherTarget);
            s.unifiedCommitments.Add(row); Check(s, true);
            var detached = CommitmentReferences.FindDeal(s, row.id);
            Assert.That(KnownBallots.DealSettledWeek(s, detached, witness.PartnerId), Is.EqualTo(frame.week));
            Assert.That(KnownBallots.Knows(s, frame.week, witness.PartnerId), Is.False);
            Assert.That(FinalistRead.DealBreaker(s, detached), Is.EqualTo(s.playerId));
            Assert.That(KnownBallots.DealOutcomeKnown(s, detached), Is.True, "The player's own breach needs no partner disclosure.");
            Assert.That(KnownBallots.Knows(s, frame.week, witness.PartnerId), Is.False);
        }

        [Test]
        public void TogetherDecisionHasNoSoleBreakerAndArchiveDoesNotRevealPartner()
        {
            var witness = Split(); var s = Prospect(witness.State, witness.Frames);
            var row = TerminalDeal(s, witness.PartnerId, DealKind.VoteTogether, null);
            s.unifiedCommitments.Add(row); Check(s, true);
            var decision = UnifiedVoteHistory.FindDecision(s, row.id);
            Assert.That(decision.ActorId, Is.Null);
            Assert.That(FinalistRead.DealBreaker(s, CommitmentReferences.FindDeal(s, row.id)), Is.Null);
            Assert.That(KnownBallots.DealOutcomeKnown(s, CommitmentReferences.FindDeal(s, row.id)), Is.False);
            Assert.That(KnownBallots.Knows(s, row.settledWeek, witness.PartnerId), Is.False);
        }

        private static EpisodeState Opening()
        {
            var source = Fresh(1); string before = Trace(source);
            var result = source.Clone(); result.unifiedCommitmentRulesVersion = UnifiedVoteFamilyValidation.Version;
            Assert.That(Trace(source), Is.EqualTo(before)); return result;
        }
        private static EpisodeState Fresh(uint seed) => PinnedVoteSeason.Fresh(seed);
        private static UnifiedCommitmentState OpeningDeal(EpisodeState s) => new UnifiedCommitmentState {
            id = FreeId(s), kind = UnifiedVoteTogether.Vote, sourcePolicy = UnifiedCommitments.DealPolicy,
            origin = UnifiedCommitments.PlayerDeal, makerId = s.playerId, beneficiaryId = s.contestants.First(c => !c.isPlayer).id,
            reciprocal = true, createdWeek = s.week, expiresWeek = s.week, voteBindingWeek = s.week,
            voteFirstRevealWeek = s.week, status = DealStatus.Active, trustImpact = DealTrust.Medium, subtype = DealKind.VoteTogether };
        private static OpportunityRow Opportunity(EpisodeState s, UnifiedCommitmentState row, bool reconciled) =>
            new OpportunityRow { id = row.id, kind = OpportunityKinds.Deal, week = row.createdWeek,
                source = reconciled ? row.subtype : null,
                note = reconciled ? "put to " + row.beneficiaryId + ", " + DealStatus.Active : null,
                response = OpportunityResponse.Taken, outcome = OpportunityOutcome.NotApplicable };
        private static UnifiedCommitmentState TerminalDeal(EpisodeState s, string partner, string subtype, string target)
        {
            var frame = s.unifiedVoteReveals.Single(); var row = OpeningDeal(s);
            row.origin = UnifiedCommitments.StoryDeal; row.id = FreeId(s, "deal-story-"); row.beneficiaryId = partner;
            row.createdWeek = row.expiresWeek = row.voteBindingWeek = row.voteFirstRevealWeek = frame.week;
            row.subtype = subtype; row.targetId = target;
            if (subtype == DealKind.VoteTogether)
            {
                Assert.That(UnifiedVoteTogether.TryVerdict(row, frame.ballots, out var status, out var error), Is.True, error);
                Assert.That(status, Is.Not.Null); row.status = status;
            }
            else
            {
                var power = s.ledger.power.Single(p => p.week == frame.week);
                Assert.That(UnifiedVoteObligations.TryVerdict(row, frame.ballots, power.nominees, out var verdict, out var error), Is.True, error);
                Assert.That(verdict, Is.Not.Null); row.status = verdict.Status;
                if (row.status == DealStatus.Broken) row.brokenById = verdict.ActorId;
            }
            row.settledWeek = frame.week;
            if (row.status == DealStatus.Broken) row.settlementEffectKey = UnifiedVoteHistory.Key(row, frame.week);
            return row;
        }
        private sealed class Witness { internal EpisodeState State; internal List<UnifiedVoteRevealState> Frames; internal string PartnerId; }
        private static Witness Cached;
        private static string SplitFailure;

        /// <summary>
        /// The season's first reveal, constructed: seeds 1..32 in order, the first success cached. The
        /// player is an ordinary voter at the season's first vote; after the real NPC batch the first
        /// ordinal NPC voter (the partner) is pinned against the nominee the player votes out and every
        /// other NPC voter with the player; then the player's real ballot and the real reveal. The
        /// partner is the box's lone dissent, which the count does not prove. Guards assert the first
        /// reveal, the designed count and the partner's unknown ballot.
        /// </summary>
        private static Witness Split()
        {
            if (Cached != null) return Cached;
            if (SplitFailure != null) { Assert.Fail(SplitFailure); return null; }
            var misses = new List<string>();
            for (uint seed = 1; seed <= 32; seed++)
            {
                var walk = new PinnedVoteSeason(seed, Fresh(seed));
                while (walk.Accepted < 512 && !PinnedVoteSeason.OpenVote(walk.State)) walk.Step(EpisodeEngineTests.NextCommand(walk.State));
                var s = walk.State;
                if (!PinnedVoteSeason.OpenVote(s) || !PinnedVoteSeason.PlayerVotes(s) || PinnedVoteSeason.NpcVoters(s).Count() < 2)
                { misses.Add("seed=" + seed + ": the player is not an ordinary voter among others at the first vote (week " + s.week + ")"); continue; }
                walk.RunNpcBatch();
                s = walk.State;
                string own = s.nominees[1], opposite = s.nominees[0];
                var npc = PinnedVoteSeason.NpcVoters(s).OrderBy(id => id, StringComparer.Ordinal).ToList();
                string partner = npc[0];
                walk.PinCast(npc.ToDictionary(id => id, id => id == partner ? opposite : own, StringComparer.Ordinal));
                walk.CastVote(own);
                var revealed = walk.Reveal();
                var power = revealed.ledger.power.Single(p => p.week == revealed.week);
                Assert.That(revealed.ledger.power.Count(p => p.tally.Count == 2), Is.EqualTo(1),
                    "Only a FIRST actual reveal can use this single-frame complete archive.");
                Assert.That(power.tally, Is.EqualTo(power.nominees.Select(id => id == opposite ? 1 : npc.Count).ToList()), "The designed split.");
                Assert.That(KnownBallots.Knows(revealed, revealed.week, partner), Is.False, "The partner's dissent is unknown to the player.");
                Assert.That(walk.Frames, Has.Count.EqualTo(1));
                var witness = new Witness { State = revealed.Clone(), PartnerId = partner,
                    Frames = walk.Frames.Select(frame => frame.Clone()).ToList() };
                if (!ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(Prospect(witness.State, witness.Frames), out var coreError))
                { misses.Add("seed=" + seed + ": whole core: " + coreError); continue; }
                TestContext.Out.WriteLine("Split witness: seed " + seed + ", week " + revealed.week + ", partner " + partner
                    + ", " + walk.Pins.Count + " pinned ballots.");
                Cached = witness; return Cached;
            }
            SplitFailure = "No constructed first split reveal within seeds 1..32:\n" + string.Join("\n", misses);
            Assert.Fail(SplitFailure); return null;
        }
        private static EpisodeState Prospect(EpisodeState source, List<UnifiedVoteRevealState> frames)
        {
            string before = Trace(source); var s = source.Clone(); s.unifiedCommitmentRulesVersion = UnifiedVoteFamilyValidation.Version;
            // Detached compatibility control, not conversion/backfill: unrelated raw Vote rows
            // are removed only in this test copy; original references/source remain unmodified.
            s.promises.RemoveAll(p => p.kind == PromiseKind.Vote); s.deals.RemoveAll(d => KnownBallots.IsVoteDeal(d.type));
            s.unifiedVoteReveals = frames.Select(f => f.Clone()).ToList();
            Assert.That(Trace(source), Is.EqualTo(before)); return s;
        }
        private static string FreeId(EpisodeState s, string prefix = "deal-player-")
        {
            var used = new HashSet<string>(s.promises.Select(p => p.id).Concat(s.deals.Select(d => d.id)).Concat(s.unifiedCommitments.Select(r => r.id)));
            for (long i = 1; i < s.nextSequence; i++) if (!used.Contains(prefix + i)) return prefix + i;
            Assert.Fail("No consumed source identity available for a detached control."); return null;
        }
        private static string Trace(EpisodeState s) => Newtonsoft.Json.JsonConvert.SerializeObject(s);
        private static void Check(EpisodeState s, bool expected)
        {
            string before = Trace(s); Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(s, out var error), Is.EqualTo(expected), error);
            if (!expected) Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(Trace(s), Is.EqualTo(before));
        }
    }
}
