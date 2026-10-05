using System;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Actual pure dialogue readers. Canonical breaches are produced by the real
    /// prospective evaluator; these tests do not activate rule 1 or establish native acceptance.
    /// </summary>
    public sealed class UnifiedSafetyPresentationReaderTests
    {
        [Test]
        public void CanonicalActiveDirectSafetyPromiseAcknowledgesItsActualSourceWithoutAMirror()
        {
            var s = State(); string who = Other(s);
            string baseline = HouseDialogue.Response(s, who, EpisodeCommandKind.PromiseSafety);
            s.unifiedCommitments.Add(Promise(s, "direct-safety", s.playerId, who));
            string before = Json(s);
            var legacy = LegacyImage(s);
            Assert.That(HouseDialogue.Response(s, who, EpisodeCommandKind.PromiseSafety),
                Is.EqualTo(HouseDialogue.Response(legacy, who, EpisodeCommandKind.PromiseSafety)));
            Assert.That(HouseDialogue.Response(s, who, EpisodeCommandKind.PromiseSafety), Is.Not.EqualTo(baseline));
            Assert.That(s.promises, Is.Empty);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase("incoming-promise")]
        [TestCase("other-pair-promise")]
        [TestCase("outgoing-deal")]
        [TestCase("incoming-deal")]
        public void SafetyAcknowledgementDoesNotInventAPlayerPromiseFromOtherConsent(string source)
        {
            var s = State(); string who = Other(s);
            string baseline = HouseDialogue.Response(s, who, EpisodeCommandKind.PromiseSafety);
            bool deal = source.EndsWith("deal", StringComparison.Ordinal);
            string from = source == "outgoing-deal" ? s.playerId : who;
            string to = source == "other-pair-promise" ? Other(s, 1) : source == "outgoing-deal" ? who : s.playerId;
            s.unifiedCommitments.Add(deal ? Deal(s, source, from, to) : Promise(s, source, from, to));
            string before = Json(s);
            Assert.That(HouseDialogue.Response(s, who, EpisodeCommandKind.PromiseSafety), Is.EqualTo(baseline));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(DealStatus.Active, 1, false)]
        [TestCase(DealStatus.Active, 2, true)]
        [TestCase(DealStatus.Expired, 2, false)]
        public void CanonicalSafetyAcknowledgementRequiresAnActiveUnelapsedSourceTerm(string status, int created, bool acknowledged)
        {
            var s = State(); string who = Other(s);
            string baseline = HouseDialogue.Response(s, who, EpisodeCommandKind.PromiseSafety);
            var row = Promise(s, "term", s.playerId, who, created); row.status = status;
            s.unifiedCommitments.Add(row); string before = Json(s);
            string actual = HouseDialogue.Response(s, who, EpisodeCommandKind.PromiseSafety);
            Assert.That(actual == baseline, Is.EqualTo(!acknowledged));
            Assert.That(actual, Is.EqualTo(HouseDialogue.Response(LegacyImage(s), who, EpisodeCommandKind.PromiseSafety)));
            Assert.That(Json(s), Is.EqualTo(before), "A reader must not opportunistically expire a source row.");
        }

        [TestCase(EpisodeCommandKind.PromiseFinalTwo)]
        [TestCase(EpisodeCommandKind.PromiseVote)]
        [TestCase(EpisodeCommandKind.FormAlliance)]
        public void SafetyPromiseDoesNotAcknowledgeAnotherAcceptedAction(EpisodeCommandKind action)
        {
            var s = State(); string who = Other(s); string baseline = HouseDialogue.Response(s, who, action);
            s.unifiedCommitments.Add(Promise(s, "safety-only", s.playerId, who));
            string before = Json(s);
            Assert.That(HouseDialogue.Response(s, who, action), Is.EqualTo(baseline));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void EvaluatedDirectBreachRemainsAcknowledgedAfterTransientOutcomeRecordsDisappear()
        {
            var s = State(); string who = Other(s);
            s.unifiedCommitments.Add(Promise(s, "durable-breach", s.playerId, who, 2));
            Break(s, s.playerId, who);
            Assert.That(s.unifiedCommitments.Single().settlementEffectKey, Does.Contain("nomination"));
            string line = HouseDialogue.Response(s, who);
            Assert.That(Outcome(s, who).id, Is.EqualTo("durable-breach"));
            Assert.That(Outcome(s, who).status, Is.EqualTo(PromiseStatus.Broken));
            Assert.That(line, Is.EqualTo(HouseDialogue.Response(LegacyImage(s), who)));
            s.events.Clear(); s.memories.Clear(); s.ledger.power.Clear();
            foreach (var edge in s.relationships) edge.events.Clear();
            string before = Json(s);
            Assert.That(HouseDialogue.Response(s, who), Is.EqualTo(line));
            var detached = Outcome(s, who); detached.status = PromiseStatus.Active;
            Assert.That(Outcome(s, who).status, Is.EqualTo(PromiseStatus.Broken));
            Assert.That(s.promises, Is.Empty);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase("incoming-promise")]
        [TestCase("other-pair-promise")]
        [TestCase("outgoing-deal")]
        [TestCase("incoming-deal")]
        public void BrokenWordFromAnotherDirectionOrSourceDoesNotFalselyBlameThePlayer(string source)
        {
            var s = State(); string who = Other(s); string baseline = HouseDialogue.Response(s, who);
            bool deal = source.EndsWith("deal", StringComparison.Ordinal);
            string from = source == "outgoing-deal" ? s.playerId : who;
            string to = source == "other-pair-promise" ? Other(s, 1) : source == "outgoing-deal" ? who : s.playerId;
            s.unifiedCommitments.Add(deal ? Deal(s, source, from, to) : Promise(s, source, from, to));
            Break(s, deal ? from : who, to);
            string before = Json(s);
            Assert.That(Outcome(s, who), Is.Null);
            Assert.That(HouseDialogue.Response(s, who), Is.EqualTo(baseline));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyDialogueRetainsExactSavedReverseListBehaviorRatherThanNewTimestampOrdering(bool reverse)
        {
            var s = State(); s.unifiedCommitmentRulesVersion = 0; string who = Other(s);
            var broken = Legacy(s, "a-older-list", who, PromiseKind.Safety, PromiseStatus.Broken, 1, 3);
            var kept = Legacy(s, "z-last-list", who, PromiseKind.FinalTwo, PromiseStatus.Fulfilled, 2, 2);
            s.promises.AddRange(reverse ? new[] { kept, broken } : new[] { broken, kept });
            var selected = reverse ? broken : kept; string before = Json(s);
            Assert.That(Outcome(s, who).id, Is.EqualTo(selected.id));
            var control = s.Clone(); control.promises.Clear(); control.promises.Add(selected.Clone());
            Assert.That(HouseDialogue.Response(s, who), Is.EqualTo(HouseDialogue.Response(control, who)));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EnabledDialogueUsesActualSettlementChronologyNotListOrProposalOrder(bool legacyIsLater)
        {
            var s = State(); string who = Other(s);
            s.unifiedCommitments.Add(Promise(s, "canonical-broken", s.playerId, who, 2)); Break(s, s.playerId, who);
            s.week = 4;
            var kept = Legacy(s, "legacy-kept", who, PromiseKind.FinalTwo, PromiseStatus.Fulfilled,
                legacyIsLater ? 1 : 2, legacyIsLater ? 4 : 2);
            s.promises.Add(kept); string before = Json(s);
            string expected = legacyIsLater ? kept.id : "canonical-broken";
            Assert.That(Outcome(s, who).id, Is.EqualTo(expected));
            var control = s.Clone(); var chosen = Outcome(s, who); control.unifiedCommitments.Clear();
            control.unifiedCommitmentRulesVersion = 0; control.promises.Clear(); control.promises.Add(chosen);
            Assert.That(HouseDialogue.Response(s, who), Is.EqualTo(HouseDialogue.Response(control, who)));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(2, "canonical-broken")]
        [TestCase(4, "legacy-without-date")]
        public void UnknownLegacySettlementTimestampFallsBackToItsOwnCreationWeek(int created, string expected)
        {
            var s = State(); string who = Other(s);
            s.unifiedCommitments.Add(Promise(s, "canonical-broken", s.playerId, who, 2)); Break(s, s.playerId, who);
            s.week = 4;
            s.promises.Add(Legacy(s, "legacy-without-date", who, PromiseKind.Vote, PromiseStatus.Fulfilled, created, 0));
            string before = Json(s);
            Assert.That(Outcome(s, who).id, Is.EqualTo(expected));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EqualSettlementDatesChooseOrdinalStableIdAcrossBothSourceLists(bool canonicalWins)
        {
            var s = State(); string who = Other(s);
            string canonical = canonicalWins ? "a-canonical" : "z-canonical";
            s.unifiedCommitments.Add(Promise(s, canonical, s.playerId, who, 2)); Break(s, s.playerId, who);
            s.promises.Add(Legacy(s, canonicalWins ? "z-legacy" : "a-legacy", who,
                PromiseKind.FinalTwo, PromiseStatus.Fulfilled, 1, 3));
            s.promises.Add(Legacy(s, "zz-additional", who, PromiseKind.Information, PromiseStatus.Broken, 1, 3));
            string before = Json(s); string expected = canonicalWins ? canonical : "a-legacy";
            Assert.That(Outcome(s, who).id, Is.EqualTo(expected));
            var reordered = s.Clone(); reordered.promises.Reverse(); reordered.unifiedCommitments.Reverse();
            Assert.That(Outcome(reordered, who).id, Is.EqualTo(expected));
            Assert.That(HouseDialogue.Response(reordered, who), Is.EqualTo(HouseDialogue.Response(s, who)));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase("future-creation")]
        [TestCase("future-settlement")]
        [TestCase("incoming-legacy")]
        [TestCase("unrelated-legacy")]
        public void LaterIneligibleLegacyRowsCannotEraseADirectCanonicalOutcome(string control)
        {
            var s = State(); string who = Other(s);
            s.unifiedCommitments.Add(Promise(s, "direct", s.playerId, who, 2)); Break(s, s.playerId, who);
            var row = Legacy(s, "a-ineligible", who, PromiseKind.FinalTwo, PromiseStatus.Fulfilled, 1, 3);
            if (control == "future-creation") row.week = 4;
            if (control == "future-settlement") row.settledWeek = 4;
            if (control == "incoming-legacy") { row.fromId = who; row.toId = s.playerId; }
            if (control == "unrelated-legacy") row.toId = Other(s, 1);
            s.promises.Add(row); string before = Json(s);
            Assert.That(Outcome(s, who).id, Is.EqualTo("direct"));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void CanonicalActivePromiseCannotEraseAnEarlierResolvedDirectOutcome()
        {
            var s = State(); string who = Other(s);
            s.promises.Add(Legacy(s, "kept-vote", who, PromiseKind.Vote, PromiseStatus.Fulfilled, 1, 2));
            s.unifiedCommitments.Add(Promise(s, "new-safety", s.playerId, who));
            string before = Json(s);
            Assert.That(Outcome(s, who).id, Is.EqualTo("kept-vote"));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        private static EpisodeState State()
        {
            var s = ContentCatalog.Create(17); s.week = 3; s.hohId = s.playerId;
            s.unifiedCommitmentRulesVersion = 1; s.commitmentRulesStartWeek = 1; s.strategyRulesStartWeek = 1;
            return s;
        }

        private static string Other(EpisodeState s, int index = 0) => s.contestants.Where(c => c.id != s.playerId).Skip(index).First().id;

        private static UnifiedCommitmentState Promise(EpisodeState s, string id, string from, string to, int created = 3) => new UnifiedCommitmentState {
            id = id, kind = UnifiedCommitments.Safety, sourcePolicy = UnifiedCommitments.PromisePolicy, origin = UnifiedCommitments.StoryPromise,
            makerId = from, beneficiaryId = to, createdWeek = created, expiresWeek = created + 1,
            status = DealStatus.Active, trustImpact = DealTrust.Medium,
        };

        private static UnifiedCommitmentState Deal(EpisodeState s, string id, string from, string to) => new UnifiedCommitmentState {
            id = id, kind = UnifiedCommitments.Safety, sourcePolicy = UnifiedCommitments.DealPolicy, origin = UnifiedCommitments.StoryDeal,
            makerId = from, beneficiaryId = to, createdWeek = s.week, expiresWeek = s.week,
            reciprocal = true, status = DealStatus.Active, trustImpact = DealTrust.Medium,
        };

        private static PromiseState Legacy(EpisodeState s, string id, string who, PromiseKind kind, PromiseStatus status, int created, int settled)
            => new PromiseState { id = id, fromId = s.playerId, toId = who, kind = kind, status = status,
                week = created, settledWeek = settled, expiresWeek = 0 };

        private static void Break(EpisodeState s, string actor, string wronged)
        {
            s.hohId = actor;
            var evaluation = UnifiedCommitments.EvaluateNomination(s, "nomination", actor, new[] { wronged });
            Assert.That(evaluation.Changes, Is.Not.Empty);
            foreach (var change in evaluation.Changes)
                s.unifiedCommitments[s.unifiedCommitments.FindIndex(row => row.id == change.Record.id)] = change.Record.Clone();
        }

        private static EpisodeState LegacyImage(EpisodeState s)
        {
            var image = s.Clone(); image.promises = CommitmentReferences.Promises(s).Select(row => row.Clone()).ToList();
            image.deals = CommitmentReferences.Deals(s).Select(row => row.Clone()).ToList();
            image.unifiedCommitments.Clear(); image.unifiedCommitmentRulesVersion = 0; return image;
        }

        private static PromiseState Outcome(EpisodeState s, string who)
        {
            var method = typeof(HouseDialogue).GetMethod("LatestDirectOutcome", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Exercise the actual selector, not a parallel test-only ordering algorithm.");
            try { return (PromiseState)method.Invoke(null, new object[] { s, who }); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        private static string Json(object value) => JsonConvert.SerializeObject(value);
    }
}
