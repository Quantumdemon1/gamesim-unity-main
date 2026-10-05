using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class UnifiedSafetyVotingTests
    {
        [TestCase(true, true, 30)] [TestCase(true, false, 10)]
        [TestCase(false, true, 35)] [TestCase(false, false, 35)]
        public void ActivePairUsesTheSourceWeightAndKeepsItsPrivateEvidence(bool promise, bool voterMade, double expected)
        {
            var s = State(); string voter = Other(s, 0), target = s.playerId;
            s.unifiedCommitments.Add(Row(s, "word", promise, voterMade ? voter : target, voterMade ? target : voter));
            string before = Json(s);
            var options = WebEvictionVoting.FromNative(s, voter);
            var term = options.safetyTerms.Single(t => t.nomineeId == target);
            Assert.That(term.obligation, Is.EqualTo(expected));
            Assert.That(term.evidenceIds, Is.EqualTo(new[] { "word" }));
            var factor = Factor(options, target, "deal");
            Assert.That(factor.value, Is.EqualTo(expected * (.1 / 1.1)).Within(1e-9));
            Assert.That(factor.visibility, Is.EqualTo("private"));
            Assert.That(factor.evidenceIds, Does.Contain("word"));
            Assert.That(Json(s), Is.EqualTo(before), "Reading and evaluation must not roll or write.");
            options.safetyTerms[0].evidenceIds.Clear(); options.safetyTerms[0].obligation = -999;
            Assert.That(Json(s), Is.EqualTo(before), "Options are detached, not mutable authority.");
        }

        [TestCase(1)] [TestCase(2)] [TestCase(5)]
        public void OverlappingActiveSafetyTakesOneMaximumAndRetainsEveryEvidenceId(int count)
        {
            var s = State(); string voter = Other(s, 0);
            for (int i = 0; i < count; i++) s.unifiedCommitments.Add(Row(s, "p" + i, true, voter, s.playerId));
            s.unifiedCommitments.Add(Row(s, "deal", false, s.playerId, voter));
            var options = WebEvictionVoting.FromNative(s, voter);
            Assert.That(options.safetyTerms.Single(t => t.nomineeId == s.playerId).obligation, Is.EqualTo(35));
            Assert.That(Factor(options, s.playerId, "deal").evidenceIds.Count, Is.EqualTo(count + 1));
            Assert.That(options.state.promises, Is.Empty);
            Assert.That(options.state.deals, Is.Empty, "No projected writable safety mirror.");
        }

        [TestCase(DealStatus.Proposed)] [TestCase(DealStatus.Declined)] [TestCase(DealStatus.Expired)]
        public void NonbindingDealProvidesNeitherProtectionNorBetrayal(string status)
        {
            var s = State(); string voter = Other(s, 0);
            var row = Row(s, "inactive", false, voter, s.playerId); row.status = status; s.unifiedCommitments.Add(row);
            var term = WebEvictionVoting.FromNative(s, voter).safetyTerms.Single(t => t.nomineeId == s.playerId);
            Assert.That((term.obligation, term.brokenIncidents), Is.EqualTo((0d, 0)));
            Assert.That(term.evidenceIds, Is.Empty);
        }

        [TestCase(1, 5)] [TestCase(4, 5)] [TestCase(8, 5)]
        public void KeptDealBonusIsOncePerActualSettlementWeek(int count, int expected)
        {
            var s = State(); string voter = Other(s, 0);
            for (int i = 0; i < count; i++)
            {
                var row = Row(s, "kept" + i, false, voter, s.playerId);
                row.status = DealStatus.Fulfilled; row.settledWeek = s.week; s.unifiedCommitments.Add(row);
            }
            var term = WebEvictionVoting.FromNative(s, voter).safetyTerms.Single(t => t.nomineeId == s.playerId);
            Assert.That(term.obligation, Is.EqualTo(expected));
            Assert.That(term.evidenceIds.Count, Is.EqualTo(count));
        }

        [Test]
        public void DistinctKeptWeeksRemainDistinctHistory()
        {
            var s = State(); string voter = Other(s, 0);
            for (int week = 1; week <= 3; week++)
            {
                var row = Row(s, "kept" + week, false, voter, s.playerId);
                row.createdWeek = row.expiresWeek = row.settledWeek = week; row.status = DealStatus.Fulfilled;
                s.unifiedCommitments.Add(row);
            }
            Assert.That(WebEvictionVoting.FromNative(s, voter).safetyTerms.Single(t => t.nomineeId == s.playerId).obligation, Is.EqualTo(15));
        }

        [TestCase(true, -25)] [TestCase(false, -35)]
        public void BrokenPairIsOneIncidentUsingItsSourceEffectOwner(bool includePromise, int expected)
        {
            var s = State(); string voter = Other(s, 0);
            s.unifiedCommitments.Add(Row(s, "deal-a", false, s.playerId, voter));
            s.unifiedCommitments.Add(Row(s, "deal-b", false, voter, s.playerId));
            if (includePromise) s.unifiedCommitments.Add(Row(s, "promise", true, s.playerId, voter));
            Break(s, "nomination", s.playerId, voter);
            string before = Json(s);
            var options = WebEvictionVoting.FromNative(s, voter);
            var term = options.safetyTerms.Single(t => t.nomineeId == s.playerId);
            Assert.That((term.obligation, term.brokenIncidents), Is.EqualTo(((double)expected, 1)));
            Assert.That(term.evidenceIds.Count, Is.EqualTo(includePromise ? 3 : 2));
            var without = s.Clone(); without.unifiedCommitments.Clear();
            Assert.That(Factor(options, s.playerId, "threat").value - Factor(WebEvictionVoting.FromNative(without, voter), s.playerId, "threat").value,
                Is.EqualTo(-3 * (.25 / 1.1)).Within(1e-9));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase("nomination")] [TestCase("replacement")]
        public void WrongedNomineeReceivesNoBreakerPenalty(string decision)
        {
            var s = State(); string voter = Other(s, 0);
            s.unifiedCommitments.Add(Row(s, "deal", false, voter, s.playerId));
            Break(s, decision, voter, s.playerId);
            var term = WebEvictionVoting.FromNative(s, voter).safetyTerms.Single(t => t.nomineeId == s.playerId);
            Assert.That((term.obligation, term.brokenIncidents), Is.EqualTo((0d, 0)));
            Assert.That(term.evidenceIds, Is.Empty);
        }

        [TestCase(1, 3)] [TestCase(2, 6)] [TestCase(3, 8)] [TestCase(4, 8)]
        public void SeparateBreachWeeksCountAndShareTheSourceThreatCap(int count, int threatPoints)
        {
            var s = State(); string voter = Other(s, 0);
            for (int week = 1; week <= count; week++)
            {
                s.week = week;
                s.unifiedCommitments.Add(Row(s, "deal" + week, false, s.playerId, voter));
                Break(s, "nomination", s.playerId, voter);
            }
            var options = WebEvictionVoting.FromNative(s, voter);
            Assert.That(options.safetyTerms.Single(t => t.nomineeId == s.playerId).brokenIncidents, Is.EqualTo(count));
            var without = s.Clone(); without.unifiedCommitments.Clear();
            Assert.That(Factor(options, s.playerId, "threat").value - Factor(WebEvictionVoting.FromNative(without, voter), s.playerId, "threat").value,
                Is.EqualTo(-threatPoints * (.25 / 1.1)).Within(1e-9));
        }

        [Test]
        public void OtherPairsAddNoPrivateObligationOrAgreementIdsToThisVoter()
        {
            var s = State(); string voter = Other(s, 0), stranger = Other(s, 2);
            s.unifiedCommitments.Add(Row(s, "private-other-pair", true, s.playerId, stranger));
            Break(s, "nomination", s.playerId, stranger);
            var options = WebEvictionVoting.FromNative(s, voter);
            var term = options.safetyTerms.Single(t => t.nomineeId == s.playerId);
            Assert.That((term.obligation, term.brokenIncidents), Is.EqualTo((0d, 1)));
            Assert.That(term.evidenceIds, Is.Empty);
            var evaluation = WebEvictionVoting.Evaluate(options, () => .25);
            Assert.That(evaluation.nomineeEvaluations.SelectMany(n => n.factors).SelectMany(f => f.evidenceIds),
                Does.Not.Contain("private-other-pair"));
            Assert.That(evaluation.publicReasonCodes, Does.Not.Contain("deal"));
        }

        [TestCase(0, 3)] [TestCase(1, 3)] [TestCase(2, 2)] [TestCase(3, 0)]
        public void LegacyOtherFamiliesAndCanonicalIncidentsUseOneSharedThreatCap(int legacyCount, int increase)
        {
            var s = State(); string voter = Other(s, 0);
            s.unifiedCommitments.Add(Row(s, "canonical", false, s.playerId, voter)); Break(s, "nomination", s.playerId, voter);
            for (int i = 0; i < legacyCount; i++) s.deals.Add(new DealState { id = "legacy" + i,
                proposerId = s.playerId, recipientId = voter, type = DealKind.VoteEvict, status = DealStatus.Broken,
                targetId = Other(s, 1), week = s.week, settledWeek = s.week, brokenById = s.playerId });
            var without = s.Clone(); without.unifiedCommitments.Clear();
            Assert.That(Factor(WebEvictionVoting.FromNative(s, voter), s.playerId, "threat").value
                - Factor(WebEvictionVoting.FromNative(without, voter), s.playerId, "threat").value,
                Is.EqualTo(-increase * (.25 / 1.1)).Within(1e-9));
        }

        [TestCase(false, -.5)] [TestCase(true, 5)]
        public void CanonicalAndOtherFamilyValuesAreCombinedBeforeOneFinalClamp(bool positive, double expectedFactor)
        {
            var s = State(); string voter = Other(s, 0);
            if (positive)
            {
                s.unifiedCommitments.Add(Row(s, "safety", false, voter, s.playerId));
                s.deals.Add(new DealState { id = "other", proposerId = voter, recipientId = s.playerId,
                    type = DealKind.FinalTwo, status = DealStatus.Active });
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    s.week = i + 1; s.unifiedCommitments.Add(Row(s, "safety" + i, false, s.playerId, voter));
                    Break(s, "nomination", s.playerId, voter);
                }
                // Three negative incidents (-105) plus two independent Final 2 deals (+100)
                // gives -5 BEFORE clamping, not +50 from an early clamp to -50.
                for (int i = 0; i < 2; i++) s.deals.Add(new DealState { id = "other" + i, proposerId = voter, recipientId = s.playerId,
                    type = DealKind.FinalTwo, status = DealStatus.Active });
            }
            Assert.That(Factor(WebEvictionVoting.FromNative(s, voter), s.playerId, "deal").value, Is.EqualTo(expectedFactor / 1.1).Within(1e-9));
        }

        [Test]
        public void LegacyAndImportedOptionsKeepTheirOriginalTenFactorEvaluation()
        {
            var s = State(); s.unifiedCommitmentRulesVersion = 0;
            var options = WebEvictionVoting.FromNative(s, Other(s, 0));
            Assert.That(options.safetyTerms, Is.Empty);
            string original = JsonConvert.SerializeObject(WebEvictionVoting.Evaluate(options, () => .25));
            options.safetyTerms = null;
            Assert.That(JsonConvert.SerializeObject(WebEvictionVoting.Evaluate(options, () => .25)), Is.EqualTo(original));
            Assert.That(WebEvictionVoting.Evaluate(options).nomineeEvaluations.All(n => n.factors.Count == 10), Is.True);
        }

        [TestCase(true)] [TestCase(false)]
        public void NominationHistoryPenalizesTheBreakerOnceNotTheWrongedCandidate(bool promise)
        {
            var s = State(); string hoh = Other(s, 0);
            s.unifiedCommitments.Add(Row(s, "first", promise, s.playerId, hoh));
            s.unifiedCommitments.Add(Row(s, "second", false, hoh, s.playerId));
            Break(s, "nomination", s.playerId, hoh);
            Assert.That(StrategyRules.NominationReluctance(s, hoh, s.playerId), Is.EqualTo(s.Score(hoh, s.playerId) - 35));
            Assert.That(StrategyRules.NominationReluctance(s, s.playerId, hoh), Is.EqualTo(s.Score(s.playerId, hoh)));
            Assert.That(StrategyRules.NominationReluctance(s, Other(s, 2), s.playerId), Is.EqualTo(s.Score(Other(s, 2), s.playerId)));
            s.strategyRulesStartWeek = 0;
            Assert.That(StrategyRules.NominationReluctance(s, hoh, s.playerId), Is.EqualTo(s.Score(hoh, s.playerId)));
        }

        [TestCase(true)] [TestCase(false)]
        public void StoryOddsShowsThePlayersActualBreachOnce(bool promise)
        {
            var s = State(); string subject = Other(s, 0);
            s.unifiedCommitments.Add(Row(s, "first", promise, s.playerId, subject));
            s.unifiedCommitments.Add(Row(s, "second", false, subject, s.playerId));
            Break(s, "nomination", s.playerId, subject);
            string before = Json(s);
            Assert.That(StoryOdds.PlayerBrokeTheirWord(s, subject), Is.True);
            Assert.That(StoryOdds.PlayerBrokeTheirWord(s, Other(s, 2)), Is.False);
            var terms = StoryOdds.Terms(s, null, new HouseEventChoice { checkBase = 50, subjectId = subject });
            Assert.That(terms.Where(t => t.label == "you broke your word").Select(t => t.value), Is.EqualTo(new[] { -30 }));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(true, 0)] [TestCase(false, 1)]
        public void StoryOddsOnlyCallsAReciprocalDealALiveDeal(bool promise, int expected)
        {
            var s = State(); string subject = Other(s, 0);
            s.unifiedCommitments.Add(Row(s, "word", promise, s.playerId, subject));
            var terms = StoryOdds.Terms(s, null, new HouseEventChoice { checkBase = 50, subjectId = subject });
            Assert.That(terms.Count(t => t.label == "a live deal" && t.value == 10), Is.EqualTo(expected));
        }

        [Test]
        public void StoryOddsDoesNotBlameThePlayerForTheirPartnersBreach()
        {
            var s = State(); string subject = Other(s, 0);
            s.unifiedCommitments.Add(Row(s, "word", false, subject, s.playerId)); Break(s, "nomination", subject, s.playerId);
            Assert.That(StoryOdds.PlayerBrokeTheirWord(s, subject), Is.False);
        }

        private static EpisodeState State()
        {
            var s = ContentCatalog.Create(17); s.week = 3; s.phase = EpisodePhase.Eviction;
            s.strategyRulesStartWeek = s.commitmentRulesStartWeek = 1; s.unifiedCommitmentRulesVersion = 1;
            foreach (var contestant in s.contestants) contestant.traits.Clear();
            s.nominees = new List<string> { s.playerId, Other(s, 1) };
            return s;
        }
        private static string Other(EpisodeState s, int index) => s.contestants.Where(c => c.id != s.playerId).ElementAt(index).id;
        private static UnifiedCommitmentState Row(EpisodeState s, string id, bool promise, string maker, string beneficiary) => new UnifiedCommitmentState {
            id = id, kind = UnifiedCommitments.Safety, sourcePolicy = promise ? UnifiedCommitments.PromisePolicy : UnifiedCommitments.DealPolicy,
            origin = promise ? UnifiedCommitments.StoryPromise : UnifiedCommitments.StoryDeal, makerId = maker, beneficiaryId = beneficiary,
            reciprocal = !promise, createdWeek = s.week, expiresWeek = s.week + (promise ? 1 : 0), status = DealStatus.Active, trustImpact = DealTrust.Medium,
        };
        private static void Break(EpisodeState s, string decision, string actor, string target)
        {
            string hohBefore = s.hohId; s.hohId = actor;
            foreach (var change in UnifiedCommitments.EvaluateNomination(s, decision, actor, new[] { target }).Changes)
                s.unifiedCommitments[s.unifiedCommitments.FindIndex(row => row.id == change.Record.id)] = change.Record.Clone();
            s.hohId = hohBefore;
        }
        private static WebVoteFactor Factor(WebVoteOptions o, string id, string code) =>
            WebEvictionVoting.Evaluate(o, () => .25).nomineeEvaluations.Single(n => n.nomineeId == id).factors.Single(f => f.code == code);
        private static string Json(EpisodeState s) => JsonConvert.SerializeObject(s.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToDictionary(field => field.Name, field => field.GetValue(s), StringComparer.Ordinal));
    }
}
