using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>Actual internal settlement gateway, not permission to load or play rule 1.</summary>
    public sealed class UnifiedSafetyResolutionTests
    {
        [TestCase("nomination")] [TestCase("spared")] [TestCase("expiry")]
        public void DisabledGatewayIsByteExactEvenBeforeArgumentValidation(string call)
        {
            var s = State(); s.unifiedCommitmentRulesVersion = 0; string before = Json(s);
            if (call == "nomination") Nominate(s, "not-a-decision", null, null);
            else if (call == "spared") Spared(s, null, null);
            else Expire(s, (UnifiedCommitmentExpiry)99, "absent");
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(true, false)] [TestCase(false, false)] [TestCase(false, true)]
        public void OneSourceBreachUsesItsExactExistingEffectsWitnessesAndRandomStream(bool promise, bool npcOffer)
        {
            var s = State(); var row = Row(s, promise, 1);
            if (npcOffer) { row.origin = UnifiedCommitments.NpcOffer; row.id = "deal-ask-1";
                row.makerId = A(s); row.beneficiaryId = s.playerId; }
            s.unifiedCommitments.Add(row); var expected = s.Clone();
            if (promise) Invoke("SettlePromise", expected, CommitmentReferences.FindPromise(expected, row.id), PromiseStatus.Broken);
            else Invoke("SettleDeals", expected, new List<DealResolution.Verdict> { new DealResolution.Verdict {
                deal = CommitmentReferences.FindDeal(expected, row.id), status = DealStatus.Broken, actorId = s.playerId } });
            Nominate(s, "nomination", s.playerId, new[] { A(s) });
            Assert.That(Effects(s), Is.EqualTo(Effects(expected)));
            Assert.That(s.unifiedCommitments.Single().status, Is.EqualTo(DealStatus.Broken));
            Assert.That(s.unifiedCommitments.Single().brokenById, Is.EqualTo(s.playerId));
            Assert.That(s.unifiedCommitments.Single().settlementEffectKey, Does.StartWith("safety:"));
            Assert.That(s.promises, Is.Empty); Assert.That(s.deals, Is.Empty);
            string once = Json(s); Nominate(s, "nomination", s.playerId, new[] { A(s) });
            Assert.That(Json(s), Is.EqualTo(once), "Re-entry cannot draw witnesses or duplicate any effect.");
        }

        [TestCase(false)] [TestCase(true)]
        public void OverlappingSourcesKeepEveryEvidenceIdButRunOnlyTheStrongestProcedure(bool reverse)
        {
            var s = State(); var promise = Row(s, true, 1); var deal = Row(s, false, 2);
            s.unifiedCommitments.AddRange(reverse ? new[] { deal, promise } : new[] { promise, deal });
            var evaluation = UnifiedCommitments.EvaluateNomination(s, "nomination", s.playerId, new[] { A(s) });
            var incident = evaluation.Breaches.Single(); var expected = s.Clone();
            var owner = expected.unifiedCommitments.Single(row => row.id == incident.EffectOwnerId);
            if (owner.sourcePolicy == UnifiedCommitments.PromisePolicy)
                Invoke("SettlePromise", expected, CommitmentReferences.FindPromise(expected, owner.id), PromiseStatus.Broken);
            else Invoke("SettleDeals", expected, new List<DealResolution.Verdict> { new DealResolution.Verdict {
                deal = CommitmentReferences.FindDeal(expected, owner.id), status = DealStatus.Broken, actorId = s.playerId } });
            Nominate(s, "nomination", s.playerId, new[] { A(s) });
            Assert.That(Effects(s), Is.EqualTo(Effects(expected)));
            Assert.That(s.unifiedCommitments.All(row => row.status == DealStatus.Broken), Is.True);
            Assert.That(s.unifiedCommitments.Select(row => row.settlementEffectKey).Distinct().Single(), Is.EqualTo(incident.EffectKey));
            Assert.That(incident.EvidenceIds, Is.EquivalentTo(new[] { promise.id, deal.id }));
            Assert.That(CommitmentReferences.FindPromise(s, promise.id).status, Is.EqualTo(PromiseStatus.Broken));
            Assert.That(CommitmentReferences.FindDeal(s, deal.id).status, Is.EqualTo(DealStatus.Broken));
            Assert.That(s.events.Count(e => e.kind == "promise-outcome" || e.kind == "deal-outcome"), Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void StoryKnowledgeAndGrudgesStillUseTheSelectedSourceProcedure(bool promise)
        {
            var s = State(); EpisodeEngine.EnableStory(s); var row = Row(s, promise, 1);
            s.unifiedCommitments.Add(row); var expected = s.Clone();
            if (promise) Invoke("SettlePromise", expected, CommitmentReferences.FindPromise(expected, row.id), PromiseStatus.Broken);
            else Invoke("SettleDeals", expected, new List<DealResolution.Verdict> { new DealResolution.Verdict {
                deal = CommitmentReferences.FindDeal(expected, row.id), status = DealStatus.Broken, actorId = s.playerId } });
            Nominate(s, "nomination", s.playerId, new[] { A(s) });
            Assert.That(Effects(s), Is.EqualTo(Effects(expected)));
            Assert.That(s.story.grudges, Is.Not.Empty);
            Assert.That(s.story.reckonings, Is.Not.Empty);
            Assert.That(s.story.facts.Count(f => f.kind == FactKinds.BrokenWord), Is.EqualTo(promise ? 0 : 1));
            string once = Json(s); Nominate(s, "nomination", s.playerId, new[] { A(s) }); Assert.That(Json(s), Is.EqualTo(once));
        }

        [Test]
        public void TheSameSourceTieBreaksByStableIdRatherThanListOrder()
        {
            var first = State(); var a = Row(first, false, 2); var b = Row(first, false, 1);
            a.origin = UnifiedCommitments.Lobby; a.id = "deal-lobby-2"; a.expiresWeek++;
            first.unifiedCommitments.AddRange(new[] { a, b });
            var second = first.Clone(); second.unifiedCommitments.Reverse();
            var incident = UnifiedCommitments.EvaluateNomination(first, "nomination", first.playerId, new[] { A(first) }).Breaches.Single();
            Assert.That(incident.EffectOwnerId, Is.EqualTo("deal-lobby-2"));
            Nominate(first, "nomination", first.playerId, new[] { A(first) });
            Nominate(second, "nomination", second.playerId, new[] { A(second) });
            Assert.That(Effects(first), Is.EqualTo(Effects(second)));
        }

        [Test]
        public void NominationAndReplacementOnlyJudgeTheirOwnActualTargets()
        {
            var s = State(); var first = Row(s, true, 1); var other = Row(s, true, 2); other.beneficiaryId = B(s);
            s.unifiedCommitments.AddRange(new[] { first, other });
            Nominate(s, "nomination", s.playerId, new[] { A(s) });
            string firstKey = CommitmentReferences.FindCanonical(s, first.id).settlementEffectKey;
            Assert.That(CommitmentReferences.FindCanonical(s, other.id).status, Is.EqualTo(DealStatus.Active));
            var later = Row(s, true, 3); s.unifiedCommitments.Add(later);
            Nominate(s, "replacement", s.playerId, new[] { B(s) });
            Assert.That(CommitmentReferences.FindCanonical(s, later.id).status, Is.EqualTo(DealStatus.Active));
            Assert.That(CommitmentReferences.FindCanonical(s, first.id).settlementEffectKey, Is.EqualTo(firstKey));
            Assert.That(CommitmentReferences.FindCanonical(s, other.id).settlementEffectKey, Does.Contain("replacement"));
        }

        [TestCase("decision")] [TestCase("actor")] [TestCase("target")] [TestCase("duplicate")]
        [TestCase("link")] [TestCase("policy")] [TestCase("non-owner-policy")] [TestCase("base-rules")]
        public void InvalidPreflightCannotPublishRowsEffectsOrRandomDraws(string defect)
        {
            var s = State(); var row = Row(s, false, 1); s.unifiedCommitments.Add(row);
            string decision = "nomination", actor = s.playerId; var targets = new[] { A(s) };
            switch (defect)
            {
                case "decision": decision = "user-invented"; break;
                case "actor": actor = B(s); break;
                case "target": targets = new[] { "absent" }; break;
                case "duplicate": targets = new[] { A(s), A(s) }; break;
                case "link": row.origin = UnifiedCommitments.CounterDeal; row.id = "deal-counter-1"; row.linkedCommitmentId = "deal-price-1"; break;
                case "policy": row.origin = UnifiedCommitments.NpcOffer; row.makerId = A(s); row.beneficiaryId = s.playerId; break;
                case "non-owner-policy":
                    row.origin = UnifiedCommitments.NpcOffer; row.makerId = A(s); row.beneficiaryId = s.playerId;
                    s.unifiedCommitments.Add(Row(s, true, 2)); break;
                case "base-rules": s.commitmentRulesStartWeek = 0; break;
            }
            string before = Json(s); Assert.Throws<ArgumentException>(() => Nominate(s, decision, actor, targets));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void FinalVetoFulfillsOnlyNeverNominatedPartnersAndNeverSafetyPromises(bool everNominated)
        {
            var s = State(); s.unifiedCommitments.AddRange(new[] { Row(s, true, 1), Row(s, false, 2) });
            if (everNominated) s.Find(A(s)).nominationWeeks.Add(s.week);
            var expected = s.Clone();
            if (!everNominated) Invoke("SettleDeals", expected, new List<DealResolution.Verdict> { new DealResolution.Verdict {
                deal = CommitmentReferences.FindDeal(expected, "deal-player-2"), status = DealStatus.Fulfilled, actorId = s.playerId } });
            Spared(s, s.playerId, new[] { B(s), C(s) });
            Assert.That(Effects(s), Is.EqualTo(Effects(expected)));
            Assert.That(s.unifiedCommitments[0].status, Is.EqualTo(DealStatus.Active));
            Assert.That(s.unifiedCommitments[1].status, Is.EqualTo(everNominated ? DealStatus.Active : DealStatus.Fulfilled));
            Assert.That(s.unifiedCommitments.All(row => row.settlementEffectKey == null), Is.True);
            string once = Json(s); Spared(s, s.playerId, new[] { B(s), C(s) }); Assert.That(Json(s), Is.EqualTo(once));
        }

        [Test]
        public void OverlappingKeepsUseOneStrongestPositiveSourceEffectNotStackedRewards()
        {
            var s = State(); var first = Row(s, false, 1); var second = Row(s, false, 2);
            second.origin = UnifiedCommitments.Lobby; second.id = "deal-lobby-2"; second.expiresWeek++; second.trustImpact = DealTrust.Critical;
            s.unifiedCommitments.AddRange(new[] { first, second }); var expected = s.Clone();
            Invoke("SettleDeals", expected, new List<DealResolution.Verdict> { new DealResolution.Verdict {
                deal = CommitmentReferences.FindDeal(expected, second.id), status = DealStatus.Fulfilled, actorId = s.playerId } });
            Spared(s, s.playerId, new[] { B(s), C(s) });
            Assert.That(Effects(s), Is.EqualTo(Effects(expected)));
            Assert.That(s.unifiedCommitments.All(row => row.status == DealStatus.Fulfilled && row.settlementEffectKey == null), Is.True);
            Assert.That(s.events.Count(e => e.kind == "deal-outcome"), Is.EqualTo(1));
        }

        [TestCase(UnifiedCommitmentExpiry.PromiseWeekTurn)] [TestCase(UnifiedCommitmentExpiry.DealPass)]
        [TestCase(UnifiedCommitmentExpiry.Departure)] [TestCase(UnifiedCommitmentExpiry.Expulsion)]
        public void ExpiryBoundariesKeepTheirSeparateSourceLifetimesWithoutEffects(UnifiedCommitmentExpiry boundary)
        {
            var s = State(); s.unifiedCommitments.AddRange(new[] { Row(s, true, 1), Row(s, false, 2) });
            if (boundary == UnifiedCommitmentExpiry.PromiseWeekTurn) s.week += 2;
            if (boundary == UnifiedCommitmentExpiry.DealPass) s.week++;
            if (boundary == UnifiedCommitmentExpiry.Departure) s.Find(A(s)).status = ContestantStatus.Jury;
            if (boundary == UnifiedCommitmentExpiry.Expulsion) s.Find(A(s)).status = ContestantStatus.Expelled;
            string effects = Effects(s); Expire(s, boundary, A(s));
            Assert.That(s.unifiedCommitments[0].status, Is.EqualTo(boundary == UnifiedCommitmentExpiry.PromiseWeekTurn
                || boundary == UnifiedCommitmentExpiry.Expulsion ? DealStatus.Expired : DealStatus.Active));
            Assert.That(s.unifiedCommitments[1].status, Is.EqualTo(boundary == UnifiedCommitmentExpiry.PromiseWeekTurn ? DealStatus.Active : DealStatus.Expired));
            Assert.That(Effects(s), Is.EqualTo(effects));
            string once = Json(s); Expire(s, boundary, A(s)); Assert.That(Json(s), Is.EqualTo(once));
        }

        [TestCase(ContestantStatus.Active)] [TestCase(ContestantStatus.Jury)] [TestCase(ContestantStatus.Evicted)]
        public void ExpulsionCannotBeClaimedForAnOrdinaryDeparture(ContestantStatus status)
        {
            var s = State(); s.unifiedCommitments.Add(Row(s, true, 1)); s.Find(A(s)).status = status;
            string before = Json(s); Assert.Throws<ArgumentException>(() => Expire(s, UnifiedCommitmentExpiry.Expulsion, A(s)));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void ActualProductionRemovalRoutesOnlyEnabledSafetyThroughTheNewBoundary(bool enabled)
        {
            var s = State(); var promise = Row(s, true, 1); var deal = Row(s, false, 2);
            s.unifiedCommitments.AddRange(new[] { promise, deal });
            if (!enabled)
            {
                var legacyPromise = CommitmentReferences.FindPromise(s, promise.id);
                var legacyDeal = CommitmentReferences.FindDeal(s, deal.id);
                s.unifiedCommitments.Clear(); s.unifiedCommitmentRulesVersion = 0;
                s.promises.Add(legacyPromise); s.deals.Add(legacyDeal);
            }
            string random = Json(s.randomState); Invoke("Expel", s, A(s));
            Assert.That(s.Find(A(s)).status, Is.EqualTo(ContestantStatus.Expelled));
            Assert.That(CommitmentReferences.FindPromise(s, promise.id).status, Is.EqualTo(PromiseStatus.Expired));
            Assert.That(CommitmentReferences.FindDeal(s, deal.id).status, Is.EqualTo(DealStatus.Expired));
            Assert.That(Json(s.randomState), Is.EqualTo(random));
            Assert.That(s.events.Count(e => e.kind == "promise-outcome" || e.kind == "deal-outcome"), Is.Zero);
            Assert.That(s.unifiedCommitments.Count, Is.EqualTo(enabled ? 2 : 0));
            Assert.That(s.promises.Count, Is.EqualTo(enabled ? 0 : 1));
            Assert.That(s.deals.Count, Is.EqualTo(enabled ? 0 : 1));
        }

        [Test]
        public void AStrongerPromiseStillVoidsEveryBreachedSafetyBoughtPriceExactlyOnce()
        {
            var s = State(); s.hohId = A(s);
            var promise = Row(s, true, 1); promise.origin = UnifiedCommitments.StoryPromise;
            promise.makerId = A(s); promise.beneficiaryId = s.playerId;
            var bought = Row(s, false, 2); bought.id = "deal-counter-2"; bought.origin = UnifiedCommitments.CounterDeal;
            bought.linkedCommitmentId = "deal-price-2";
            var price = PlayerDeals.Draft(s, A(s), DealKind.FinalTwo, null, "deal-price-2"); price.linkedDealId = bought.id;
            s.deals.Add(price); s.unifiedCommitments.AddRange(new[] { promise, bought });
            var preview = UnifiedCommitments.EvaluateNomination(s, "nomination", A(s), new[] { s.playerId });
            Assert.That(preview.Breaches.Single().EffectOwnerId, Is.EqualTo(promise.id));
            Nominate(s, "nomination", A(s), new[] { s.playerId });
            Assert.That(s.deals.Single().status, Is.EqualTo(DealStatus.Expired));
            Assert.That(s.unifiedCommitments.All(row => row.status == DealStatus.Broken && row.brokenById == A(s)), Is.True);
            Assert.That(s.events.Count(e => e.kind == "promise-outcome"), Is.EqualTo(1));
            Assert.That(s.events.Count(e => e.kind == "deal-outcome"), Is.EqualTo(1), "Only the price-void line, not another breach effect.");
            string once = Json(s); Nominate(s, "nomination", A(s), new[] { s.playerId }); Assert.That(Json(s), Is.EqualTo(once));
        }

        private static EpisodeState State()
        {
            var s = ContentCatalog.Create(17); s.week = 3; s.hohId = s.playerId;
            s.unifiedCommitmentRulesVersion = 1; s.commitmentRulesStartWeek = 1; s.strategyRulesStartWeek = 1; return s;
        }
        private static UnifiedCommitmentState Row(EpisodeState s, bool promise, int id) => new UnifiedCommitmentState {
            id = (promise ? "promise-" : "deal-player-") + id, kind = UnifiedCommitments.Safety,
            sourcePolicy = promise ? UnifiedCommitments.PromisePolicy : UnifiedCommitments.DealPolicy,
            origin = promise ? UnifiedCommitments.PlayerPromise : UnifiedCommitments.PlayerDeal,
            makerId = s.playerId, beneficiaryId = A(s), reciprocal = !promise, createdWeek = s.week,
            expiresWeek = s.week + (promise ? 1 : 0), status = DealStatus.Active,
            trustImpact = promise ? DealTrust.Medium : DealKind.DefaultTrust(DealKind.SafetyAgreement),
        };
        private static string A(EpisodeState s) => s.contestants.First(c => c.id != s.playerId).id;
        private static string B(EpisodeState s) => s.contestants.Where(c => c.id != s.playerId).Skip(1).First().id;
        private static string C(EpisodeState s) => s.contestants.Where(c => c.id != s.playerId).Skip(2).First().id;
        private static void Nominate(EpisodeState s, string decision, string actor, string[] nominees) =>
            Invoke("ResolveUnifiedSafetyNomination", s, decision, actor, nominees);
        private static void Spared(EpisodeState s, string actor, string[] nominees) => Invoke("ResolveUnifiedSafetySpared", s, actor, nominees);
        private static void Expire(EpisodeState s, UnifiedCommitmentExpiry boundary, string id) => Invoke("ResolveUnifiedSafetyExpiry", s, boundary, id);
        private static void Invoke(string name, params object[] arguments)
        {
            var method = typeof(EpisodeEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, name);
            try { method.Invoke(null, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private static string Effects(EpisodeState s)
        {
            var image = JObject.FromObject(s); image.Remove("unifiedCommitments"); return image.ToString(Formatting.None);
        }
        private static string Json(object value) => JsonConvert.SerializeObject(value);
    }
}
