using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class UnifiedSafetyWordTests
    {
        [TestCase(true, DealStatus.Active, "open")] [TestCase(true, DealStatus.Expired, "lapsed")]
        [TestCase(true, DealStatus.Broken, "broken")]
        [TestCase(false, DealStatus.Active, "open")] [TestCase(false, DealStatus.Proposed, "open")]
        [TestCase(false, DealStatus.Declined, "lapsed")] [TestCase(false, DealStatus.Expired, "lapsed")]
        [TestCase(false, DealStatus.Fulfilled, "kept")] [TestCase(false, DealStatus.Broken, "broken")]
        public void PageReadsCanonicalPolicyStatusTermAndAttribution(bool promise, string status, string outcome)
        {
            var s = State(); string other = Other(s, 0);
            var row = Row(s, "word", promise, s.playerId, other); s.unifiedCommitments.Add(row);
            if (status == DealStatus.Broken) Install(s, UnifiedCommitments.EvaluateNomination(s, "nomination", s.playerId, new[] { other }));
            else { row.status = status; if (status == DealStatus.Fulfilled) row.settledWeek = s.week; }
            string before = Json(s);
            var page = CommitmentsRead.Of(s); Assert.That(page.Count, Is.EqualTo(1));
            var word = page.Single();
            Assert.That((word.id, word.withId, word.yours, word.week, word.untilWeek, word.outcome),
                Is.EqualTo(("word", other, true, s.week, s.week + (promise ? 1 : 0), outcome)));
            Assert.That(word.kind, Is.EqualTo(promise ? CommitmentsRead.Kinds.Promise : CommitmentsRead.Kinds.Deal));
            Assert.That(word.binds, Is.EqualTo(promise ? "not to nominate them" : "not to nominate each other"));
            Assert.That(word.brokenById, Is.EqualTo(status == DealStatus.Broken ? s.playerId : null));
            Assert.That(word.settledWeek, Is.EqualTo(status == DealStatus.Broken || status == DealStatus.Fulfilled ? s.week : 0));
            Assert.That(word.term, Is.EqualTo(promise ? "until week 4" : "this week"));
            Assert.That(Json(s), Is.EqualTo(before));
            word.id = "ui-cannot-rewrite-authority";
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(UnifiedCommitments.PlayerPromise, true)] [TestCase(UnifiedCommitments.NpcPromise, false)]
        [TestCase(UnifiedCommitments.StoryPromise, true)] [TestCase(UnifiedCommitments.HoHPitch, true)]
        [TestCase(UnifiedCommitments.PlayerDeal, true)] [TestCase(UnifiedCommitments.NpcDeal, false)]
        [TestCase(UnifiedCommitments.NpcOffer, true)] [TestCase(UnifiedCommitments.Lobby, true)]
        [TestCase(UnifiedCommitments.StoryDeal, true)] [TestCase(UnifiedCommitments.CounterDeal, true)]
        [TestCase(UnifiedCommitments.CounterPrice, true)]
        public void EveryOriginRetainsItsIdAndOnlyOwnPartiesAreShown(string origin, bool visible)
        {
            var s = State(); bool promise = UnifiedCommitments.IsPromiseOrigin(origin);
            string maker = origin == UnifiedCommitments.NpcOffer || !visible ? Other(s, 0) : s.playerId;
            string beneficiary = !visible ? Other(s, 1) : maker == s.playerId ? Other(s, 0) : s.playerId;
            var row = Row(s, "original-source-id", promise, maker, beneficiary); row.origin = origin;
            if (origin == UnifiedCommitments.Lobby) row.expiresWeek++;
            if (origin == UnifiedCommitments.CounterDeal || origin == UnifiedCommitments.CounterPrice)
            {
                row.linkedCommitmentId = "legacy-consideration";
                s.deals.Add(new DealState { id = "legacy-consideration", proposerId = maker, recipientId = beneficiary,
                    type = DealKind.VetoUse, status = DealStatus.Active, linkedDealId = row.id });
            }
            s.unifiedCommitments.Add(row); string before = Json(s);
            Assert.That(UnifiedCommitments.ValidateRecords(s, out string error), Is.True, error);
            var read = CommitmentsRead.Of(s).Where(c => c.id == row.id).ToArray();
            Assert.That(read.Length, Is.EqualTo(visible ? 1 : 0));
            if (visible) Assert.That(read.Single().yours, Is.EqualTo(maker == s.playerId));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(true)] [TestCase(false)]
        public void OtherPartyBreachIsAttributedToThemNotToYou(bool promise)
        {
            var s = State(); string other = Other(s, 0);
            s.unifiedCommitments.Add(Row(s, "their-word", promise, other, s.playerId));
            s.hohId = other;
            Install(s, UnifiedCommitments.EvaluateNomination(s, "replacement", other, new[] { s.playerId }));
            var word = CommitmentsRead.Of(s).Single();
            Assert.That((word.yours, word.brokenById, word.status), Is.EqualTo((false, other, "broken by them")));
        }

        [TestCase(true)] [TestCase(false)]
        public void InitialNominationWarningUsesTheCanonicalVerdictAndKeepsAllAgreementEvidence(bool reverseDeal)
        {
            var s = State(); string first = Other(s, 0), second = Other(s, 1);
            s.unifiedCommitments.Add(Row(s, "promise", true, s.playerId, first));
            s.unifiedCommitments.Add(Row(s, "deal", false, reverseDeal ? first : s.playerId, reverseDeal ? s.playerId : first));
            // A promise given TO the player is not the player's duty to its maker.
            s.unifiedCommitments.Add(Row(s, "their-promise", true, second, s.playerId));
            string before = Json(s); var decision = CommitmentsRead.Decision.Nominate(first, second);
            var expected = UnifiedCommitments.EvaluateNomination(s, "nomination", s.playerId, new[] { first, second });
            Assert.That(CommitmentsRead.AtStake(s, CommitmentsRead.DecisionKinds.Nominate), Is.True);
            var warning = CommitmentsRead.ByTheRules(s, decision);
            Assert.That(warning.Select(b => b.id).OrderBy(id => id), Is.EqualTo(expected.Changes.Select(c => c.Record.id).OrderBy(id => id)));
            Assert.That(warning.Select(b => b.effectKey).Distinct(), Is.EqualTo(expected.Breaches.Select(i => i.EffectKey)));
            Assert.That(CommitmentsRead.CountOf(warning), Is.EqualTo(2), "Two agreements, but one incident, not two penalties.");
            Assert.That(warning.All(b => b.withId == first && b.causeId == first && b.act == CommitmentsRead.Acts.Nominate), Is.True);
            Assert.That(CommitmentsRead.WouldBreak(s, decision).Select(b => b.id), Is.EqualTo(warning.Select(b => b.id)),
                "The prospective engine gate falls back to the same pure judgement, without activation.");
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase("wrong-phase")] [TestCase("other-hoh")] [TestCase("duplicate")]
        [TestCase("hoh-target")] [TestCase("already-nominated")]
        public void IllegalInitialDecisionProducesNoWarningAndNoMutation(string reason)
        {
            var s = State(); string first = Other(s, 0), second = Other(s, 1);
            s.unifiedCommitments.Add(Row(s, "word", true, s.playerId, first));
            if (reason == "wrong-phase") s.phase = EpisodePhase.Social;
            if (reason == "other-hoh") s.hohId = Other(s, 2);
            if (reason == "duplicate") second = first;
            if (reason == "hoh-target") second = s.playerId;
            if (reason == "already-nominated") s.nominees.Add(Other(s, 2));
            string before = Json(s);
            Assert.That(CommitmentsRead.ByTheRules(s, CommitmentsRead.Decision.Nominate(first, second)), Is.Empty);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(true)] [TestCase(false)]
        public void ReplacementWarningOnlyNamesTheActualReplacement(bool promise)
        {
            var s = VetoState(); string saved = s.nominees[0], retained = s.nominees[1], replacement = Other(s, 2);
            foreach (string id in new[] { saved, retained, replacement }) s.unifiedCommitments.Add(Row(s, "word-" + id, promise, s.playerId, id));
            string before = Json(s); var decision = CommitmentsRead.Decision.Veto(true, saved, replacement);
            var warning = CommitmentsRead.ByTheRules(s, decision);
            Assert.That(warning.Count, Is.EqualTo(1));
            Assert.That((warning[0].id, warning[0].causeId, warning[0].act),
                Is.EqualTo(("word-" + replacement, replacement, CommitmentsRead.Acts.Replace)));
            Assert.That(warning[0].effectKey, Is.EqualTo(UnifiedCommitments.EvaluateNomination(s, "replacement", s.playerId, new[] { replacement }).Breaches.Single().EffectKey));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase("not-used")] [TestCase("other-hoh")] [TestCase("invalid-replacement")]
        [TestCase("already-resolved")] [TestCase("wrong-phase")]
        public void VetoWarningsDoNotInventThePlayersSafetyBreach(string reason)
        {
            var s = VetoState(); string saved = s.nominees[0], replacement = Other(s, 2);
            s.unifiedCommitments.Add(Row(s, "word", false, s.playerId, replacement));
            if (reason == "other-hoh") s.hohId = Other(s, 3);
            if (reason == "invalid-replacement") replacement = saved;
            if (reason == "already-resolved") s.vetoResolved = true;
            if (reason == "wrong-phase") s.phase = EpisodePhase.Social;
            string before = Json(s);
            Assert.That(CommitmentsRead.ByTheRules(s, CommitmentsRead.Decision.Veto(reason != "not-used", saved, replacement)), Is.Empty);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void SweepRetainsEachChoicesEffectAndDoesNotSettleTheSharedCopy()
        {
            var s = VetoState(); string first = Other(s, 2), second = Other(s, 3);
            foreach (string id in new[] { first, second }) s.unifiedCommitments.Add(Row(s, "word-" + id, false, s.playerId, id));
            string before = Json(s);
            var decisions = new[] { CommitmentsRead.Decision.Veto(true, s.nominees[0], first), CommitmentsRead.Decision.Veto(true, s.nominees[0], second) };
            var warning = CommitmentsRead.ByTheRules(s, decisions);
            Assert.That(warning.Select(b => b.causeId), Is.EqualTo(new[] { first, second }));
            Assert.That(warning.Select(b => b.effectKey).Distinct().Count(), Is.EqualTo(2));
            Assert.That(CommitmentsRead.CountOf(warning), Is.EqualTo(2));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void SettledReaderRequiresAnActualMatchingTransitionNotAHistoricalOrOtherDecisionBreach(bool replacement)
        {
            var s = replacement ? VetoState() : State(); string target = Other(s, 2);
            s.unifiedCommitments.Add(Row(s, "current", false, s.playerId, target));
            var d = replacement ? CommitmentsRead.Decision.Veto(true, s.nominees[0], target)
                : CommitmentsRead.Decision.Nominate(target, Other(s, 3));
            var after = s.Clone();
            Install(after, UnifiedCommitments.EvaluateNomination(after, replacement ? "replacement" : "nomination", s.playerId, new[] { target }));
            var read = Settled(s, after, d);
            Assert.That(read.Select(b => b.id), Is.EqualTo(new[] { "current" }));
            Assert.That(read.Single().effectKey, Is.EqualTo(after.unifiedCommitments.Single().settlementEffectKey));
            Assert.That(Settled(after, after.Clone(), d), Is.Empty, "Already broken before this action is not a new transition.");
            var otherDecision = after.Clone();
            otherDecision.unifiedCommitments.Clear(); otherDecision.unifiedCommitments.Add(Row(otherDecision, "current", false, s.playerId, target));
            Install(otherDecision, UnifiedCommitments.EvaluateNomination(otherDecision, replacement ? "nomination" : "replacement", s.playerId, new[] { target }));
            Assert.That(Settled(s, otherDecision, d), Is.Empty, "The settlement class must match this action.");
        }

        [Test]
        public void MixedOtherFamilyPageRowsKeepTheirLegacyPresentation()
        {
            var s = State(); s.deals.Add(new DealState { id = "final-two", proposerId = s.playerId, recipientId = Other(s, 1),
                type = DealKind.FinalTwo, status = DealStatus.Active, expiresWeek = 0 });
            s.promises.Add(new PromiseState { id = "vote", fromId = s.playerId, toId = Other(s, 0), targetId = Other(s, 1),
                kind = PromiseKind.Vote, status = PromiseStatus.Active });
            s.unifiedCommitmentRulesVersion = 0; string legacy = JsonConvert.SerializeObject(CommitmentsRead.Of(s));
            s.unifiedCommitmentRulesVersion = 1; s.unifiedCommitments.Add(Row(s, "safety", true, s.playerId, Other(s, 2)));
            Assert.That(JsonConvert.SerializeObject(CommitmentsRead.Of(s).Where(c => c.id != "safety")), Is.EqualTo(legacy));
            Assert.That(CommitmentsRead.AtStake(s, CommitmentsRead.DecisionKinds.Vote), Is.True);
            Assert.That(CommitmentsRead.AtStake(s, CommitmentsRead.DecisionKinds.FinalEviction), Is.True);
        }

        [Test]
        public void ACompetingLegacySafetyMirrorIsRefusedByPageAndWarnings()
        {
            var s = State(); string other = Other(s, 0);
            s.unifiedCommitments.Add(Row(s, "canonical", false, s.playerId, other));
            s.deals.Add(new DealState { id = "mirror", proposerId = s.playerId, recipientId = other,
                type = DealKind.SafetyAgreement, status = DealStatus.Active });
            string before = Json(s);
            Assert.Throws<ArgumentException>(() => CommitmentsRead.Of(s));
            Assert.Throws<ArgumentException>(() => CommitmentsRead.AtStake(s, CommitmentsRead.DecisionKinds.Nominate));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        private static EpisodeState State()
        {
            var s = ContentCatalog.Create(17); s.week = 3; s.phase = EpisodePhase.Nomination; s.hohId = s.playerId;
            s.strategyRulesStartWeek = s.commitmentRulesStartWeek = 1; s.unifiedCommitmentRulesVersion = 1;
            return s;
        }
        private static EpisodeState VetoState()
        {
            var s = State(); s.phase = EpisodePhase.VetoMeeting; s.vetoHolderId = s.playerId;
            s.nominees = new List<string> { Other(s, 0), Other(s, 1) }; return s;
        }
        private static string Other(EpisodeState s, int index) => s.contestants.Where(c => c.id != s.playerId).ElementAt(index).id;
        private static UnifiedCommitmentState Row(EpisodeState s, string id, bool promise, string maker, string beneficiary) => new UnifiedCommitmentState {
            id = id, kind = UnifiedCommitments.Safety, sourcePolicy = promise ? UnifiedCommitments.PromisePolicy : UnifiedCommitments.DealPolicy,
            origin = promise ? UnifiedCommitments.StoryPromise : UnifiedCommitments.StoryDeal, makerId = maker, beneficiaryId = beneficiary,
            reciprocal = !promise, createdWeek = s.week, expiresWeek = s.week + (promise ? 1 : 0), status = DealStatus.Active, trustImpact = DealTrust.Medium,
        };
        private static void Install(EpisodeState s, UnifiedCommitmentEvaluation evaluation)
        {
            foreach (var change in evaluation.Changes)
                s.unifiedCommitments[s.unifiedCommitments.FindIndex(row => row.id == change.Record.id)] = change.Record.Clone();
        }
        private static List<CommitmentsRead.Breach> Settled(EpisodeState before, EpisodeState after, CommitmentsRead.Decision d)
        {
            var method = typeof(CommitmentsRead).GetMethod("Settled", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            try { return (List<CommitmentsRead.Breach>)method.Invoke(null, new object[] { before, after, d }); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private static string Json(EpisodeState s) => JsonConvert.SerializeObject(s.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToDictionary(field => field.Name, field => field.GetValue(s), StringComparer.Ordinal));
    }
}
