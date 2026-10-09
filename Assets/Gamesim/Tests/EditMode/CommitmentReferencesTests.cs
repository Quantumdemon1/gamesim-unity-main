using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>Reader wiring only: prospective snapshots are still refused by engine/save validation.</summary>
    public sealed class CommitmentReferencesTests
    {
        [Test]
        public void LegacyViewsKeepEveryFieldAndOriginalOrderButNeverReturnMutableAuthority()
        {
            var s = ContentCatalog.Create(17);
            s.promises.AddRange(new[] {
                new PromiseState { id = "second", fromId = s.playerId, toId = A(s), targetId = B(s), kind = PromiseKind.Vote,
                    status = PromiseStatus.Broken, week = 1, expiresWeek = 2, impact = "high", brokenById = s.playerId, settledWeek = 2 },
                new PromiseState { id = "first", fromId = A(s), toId = s.playerId, kind = PromiseKind.Safety } });
            s.deals.AddRange(new[] {
                new DealState { id = "second", type = DealKind.VoteSave, proposerId = s.playerId, recipientId = A(s), targetId = B(s),
                    status = DealStatus.Fulfilled, week = 1, expiresWeek = 2, trustImpact = DealTrust.Critical, settledWeek = 2, linkedDealId = "first" },
                new DealState { id = "first", type = DealKind.SafetyAgreement, proposerId = A(s), recipientId = s.playerId } });
            string before = Json(s);
            var promises = CommitmentReferences.Promises(s); var deals = CommitmentReferences.Deals(s);
            Assert.That(Json(promises), Is.EqualTo(Json(s.promises)));
            Assert.That(Json(deals), Is.EqualTo(Json(s.deals)));
            promises[0].targetId = "changed"; deals[0].linkedDealId = "changed";
            Assert.Throws<NotSupportedException>(() => ((IList<PromiseState>)promises).Add(new PromiseState()));
            Assert.Throws<NotSupportedException>(() => ((IList<DealState>)deals).Clear());
            Assert.That(Json(s), Is.EqualTo(before));
            Assert.That(CommitmentReferences.PromiseCount(s), Is.EqualTo(2));
            Assert.That(CommitmentReferences.DealCount(s), Is.EqualTo(2));
        }

        [TestCase(DealStatus.Active, PromiseStatus.Active)]
        [TestCase(DealStatus.Broken, PromiseStatus.Broken)]
        [TestCase(DealStatus.Expired, PromiseStatus.Expired)]
        public void PromiseProjectionPreservesSourceFieldsAndSettlement(string status, PromiseStatus expected)
        {
            var s = State(); var row = Row(s, "promise", true); SetStatus(s, row, status);
            s.unifiedCommitments.Add(row); string before = Json(s);
            var p = CommitmentReferences.FindPromise(s, row.id);
            Assert.That(p.id, Is.EqualTo(row.id)); Assert.That(p.kind, Is.EqualTo(PromiseKind.Safety));
            Assert.That(p.fromId, Is.EqualTo(row.makerId)); Assert.That(p.toId, Is.EqualTo(row.beneficiaryId));
            Assert.That(p.targetId, Is.Null); Assert.That(p.status, Is.EqualTo(expected));
            Assert.That(p.week, Is.EqualTo(row.createdWeek)); Assert.That(p.expiresWeek, Is.EqualTo(row.expiresWeek));
            Assert.That(p.impact, Is.EqualTo(row.trustImpact)); Assert.That(p.brokenById, Is.EqualTo(row.brokenById));
            Assert.That(p.settledWeek, Is.EqualTo(row.settledWeek));
            p.status = PromiseStatus.Fulfilled;
            Assert.That(Json(s), Is.EqualTo(before)); Assert.That(s.promises, Is.Empty);
            Assert.That(CommitmentReferences.FindDeal(s, row.id), Is.Null);
        }

        [TestCase(DealStatus.Proposed)] [TestCase(DealStatus.Accepted)] [TestCase(DealStatus.Active)]
        [TestCase(DealStatus.Fulfilled)] [TestCase(DealStatus.Broken)] [TestCase(DealStatus.Declined)] [TestCase(DealStatus.Expired)]
        public void DealProjectionPreservesSourceFieldsAndSettlement(string status)
        {
            var s = State(); var row = Row(s, "deal", false); SetStatus(s, row, status);
            s.unifiedCommitments.Add(row); string before = Json(s);
            var d = CommitmentReferences.FindDeal(s, row.id);
            Assert.That(d.id, Is.EqualTo(row.id)); Assert.That(d.type, Is.EqualTo(DealKind.SafetyAgreement));
            Assert.That(d.proposerId, Is.EqualTo(row.makerId)); Assert.That(d.recipientId, Is.EqualTo(row.beneficiaryId));
            Assert.That(d.targetId, Is.Null); Assert.That(d.status, Is.EqualTo(status));
            Assert.That(d.week, Is.EqualTo(row.createdWeek)); Assert.That(d.expiresWeek, Is.EqualTo(row.expiresWeek));
            Assert.That(d.trustImpact, Is.EqualTo(row.trustImpact)); Assert.That(d.brokenById, Is.EqualTo(row.brokenById));
            Assert.That(d.settledWeek, Is.EqualTo(row.settledWeek)); Assert.That(d.linkedDealId, Is.Null);
            d.proposerId = "changed";
            Assert.That(Json(s), Is.EqualTo(before)); Assert.That(s.deals, Is.Empty);
            Assert.That(CommitmentReferences.FindPromise(s, row.id), Is.Null);
        }

        [Test]
        public void AStableMixedFamilyLinkResolvesBothSidesWithoutPuttingSafetyBackInLegacyStorage()
        {
            var s = State(); var safety = Row(s, Negotiation.CounterDealPrefix + "17", false);
            safety.origin = UnifiedCommitments.CounterDeal; safety.linkedCommitmentId = Negotiation.PricePrefix + "17";
            var price = PlayerDeals.Draft(s, A(s), DealKind.FinalTwo, null, safety.linkedCommitmentId);
            price.linkedDealId = safety.id; s.deals.Add(price); s.unifiedCommitments.Add(safety);
            string before = Json(s);
            var readBought = CommitmentReferences.FindDeal(s, safety.id);
            var readPrice = CommitmentReferences.FindDeal(s, readBought.linkedDealId);
            Assert.That(readPrice.id, Is.EqualTo(price.id)); Assert.That(readPrice.linkedDealId, Is.EqualTo(readBought.id));
            Assert.That(CommitmentReferences.FindCanonical(s, price.id), Is.Null);
            var canonical = CommitmentReferences.FindCanonical(s, safety.id);
            Assert.That(canonical.origin, Is.EqualTo(UnifiedCommitments.CounterDeal));
            canonical.linkedCommitmentId = null; readPrice.status = DealStatus.Expired;
            Assert.That(Json(s), Is.EqualTo(before));
            Assert.That(s.deals.Single().type, Is.EqualTo(DealKind.FinalTwo));
        }

        [Test]
        public void FullEvidenceIsNotSilentlyCollapsedIntoOneBetrayalRow()
        {
            var s = State(); var promise = Row(s, "promise", true); var deal = Row(s, "deal", false);
            s.unifiedCommitments.AddRange(new[] { promise, deal });
            var result = UnifiedCommitments.EvaluateNomination(s, "nomination", s.playerId, new[] { A(s) });
            Assert.That(result.Breaches.Count, Is.EqualTo(1));
            foreach (var change in result.Changes)
                s.unifiedCommitments[s.unifiedCommitments.FindIndex(row => row.id == change.Record.id)] = change.Record.Clone();
            string before = Json(s);
            Assert.That(CommitmentReferences.Promises(s).Single().status, Is.EqualTo(PromiseStatus.Broken));
            Assert.That(CommitmentReferences.Deals(s).Single().status, Is.EqualTo(DealStatus.Broken));
            var p = CommitmentReferences.FindCanonical(s, promise.id); var d = CommitmentReferences.FindCanonical(s, deal.id);
            Assert.That(p.settlementEffectKey, Is.EqualTo(d.settlementEffectKey));
            Assert.That(result.Breaches.Single().EvidenceIds, Is.EquivalentTo(new[] { promise.id, deal.id }));
            p.settlementEffectKey = "changed";
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(true)] [TestCase(false)]
        public void CapacityCountsAllHistoricalSourceFamiliesAndCanonicalStatuses(bool promise)
        {
            var s = State();
            if (promise) s.promises.Add(new PromiseState { id = "legacy", kind = PromiseKind.Vote, status = PromiseStatus.Expired });
            else s.deals.Add(new DealState { id = "legacy", type = DealKind.FinalTwo, status = DealStatus.Expired });
            var row = Row(s, "canonical", promise); row.status = DealStatus.Expired; s.unifiedCommitments.Add(row);
            Assert.That(promise ? CommitmentReferences.PromiseCount(s) : CommitmentReferences.DealCount(s), Is.EqualTo(2));
            Assert.That(promise ? CommitmentReferences.DealCount(s) : CommitmentReferences.PromiseCount(s), Is.Zero);
        }

        [TestCase("promise-mirror")] [TestCase("deal-mirror")] [TestCase("null-row")]
        [TestCase("collision")] [TestCase("unknown-version")]
        public void ProspectiveCorruptAuthorityIsRefusedRatherThanDisguisedByAReadView(string defect)
        {
            var s = State(); var row = Row(s, "canonical", true); s.unifiedCommitments.Add(row);
            switch (defect)
            {
                case "promise-mirror": s.promises.Add(new PromiseState { id = "mirror", kind = PromiseKind.Safety }); break;
                case "deal-mirror": s.deals.Add(new DealState { id = "mirror", type = DealKind.SafetyAgreement }); break;
                case "null-row": s.unifiedCommitments.Add(null); break;
                case "collision": s.deals.Add(new DealState { id = row.id, type = DealKind.FinalTwo }); break;
                // Version 2 is the prospective Vote mode, whose readers check its storage only (vote family V5a);
                // the next number is one no build knows.
                case "unknown-version": s.unifiedCommitmentRulesVersion = UnifiedVoteFamilyValidation.Version + 1; break;
            }
            Assert.Throws<ArgumentException>(() => CommitmentReferences.Promises(s));
            Assert.Throws<ArgumentException>(() => CommitmentReferences.Deals(s));
            Assert.Throws<ArgumentException>(() => CommitmentReferences.PromiseCount(s));
            Assert.Throws<ArgumentException>(() => CommitmentReferences.DealCount(s));
        }

        [Test]
        public void HoHPitchUsesCanonicalWordAndDoesNotReofferAnExhaustedPromiseRecord()
        {
            var s = State(); var row = Row(s, "pitch", true); row.origin = UnifiedCommitments.HoHPitch;
            s.unifiedCommitments.Add(row);
            for (int i = 0; i < 199; i++) s.promises.Add(new PromiseState { id = "old-" + i, kind = PromiseKind.Vote, status = PromiseStatus.Expired });
            Assert.That(HoHPitches.HasSafetyPromise(s, A(s)), Is.True);
            Assert.That(HoHPitches.CanPromiseSafety(s, A(s)), Is.True, "An existing promise can be reaffirmed without a new record.");
            Assert.That(HoHPitches.SafetyDescription(s, A(s)), Does.StartWith("Reaffirm"));
            Assert.That(HoHPitches.HasSafetyPromise(s, B(s)), Is.False);
            Assert.That(HoHPitches.CanPromiseSafety(s, B(s)), Is.False);
            Assert.That(HoHPitches.SafetyDescription(s, B(s)), Does.Contain("record is full"));
        }

        [TestCase(false)] [TestCase(true)]
        public void NotebookReadsCanonicalSafetyWithoutLeakingAnUnrelatedNpcPromise(bool broken)
        {
            var s = State(); var promise = Row(s, "your-word", true); var deal = Row(s, "your-pact", false);
            if (broken) { SetStatus(s, promise, DealStatus.Broken); SetStatus(s, deal, DealStatus.Broken); }
            var privateRow = Row(s, "npc-word", true); privateRow.origin = UnifiedCommitments.NpcPromise;
            privateRow.makerId = A(s); privateRow.beneficiaryId = B(s);
            s.unifiedCommitments.AddRange(new[] { promise, deal, privateRow }); string before = Json(s);
            var notes = HouseguestNotes.For(s, A(s));
            Assert.That(notes.Count(n => n.kind == HouseguestNotes.Kinds.Word), Is.EqualTo(1));
            Assert.That(notes.Count(n => n.kind == HouseguestNotes.Kinds.Offer), Is.EqualTo(1));
            Assert.That(notes.Single(n => n.kind == HouseguestNotes.Kinds.Word).text,
                Does.Contain(broken ? "broken" : "still standing"));
            Assert.That(notes.Single(n => n.kind == HouseguestNotes.Kinds.Offer).text,
                Does.Contain(broken ? "broken by you" : "agreed"));
            Assert.That(Json(s), Is.EqualTo(before)); Assert.That(s.promises, Is.Empty); Assert.That(s.deals, Is.Empty);
        }

        [Test]
        public void LegacyNotebookAttributionDoesNotChangeWithoutCanonicalAuthority()
        {
            var s = State(); s.unifiedCommitmentRulesVersion = 0;
            var deal = PlayerDeals.Draft(s, A(s), DealKind.SafetyAgreement, null, "deal-player-50");
            deal.status = DealStatus.Broken; deal.brokenById = s.playerId; deal.settledWeek = s.week;
            s.deals.Add(deal); string before = Json(s);
            Assert.That(HouseguestNotes.DealEnding(s, deal, false), Is.EqualTo("broken"),
                "Old-model notebooks retain their public-ledger interpretation, not a new inference.");
            Assert.That(CommitmentReferences.FindCanonical(s, deal.id), Is.Null);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void PublicNotebookHelperDoesNotExposeAnUnrelatedCanonicalNpcDealBreaker()
        {
            var s = State(); var row = Row(s, "npc-pact", false);
            row.makerId = A(s); row.beneficiaryId = B(s); SetStatus(s, row, DealStatus.Broken);
            row.brokenById = A(s); s.unifiedCommitments.Add(row); string before = Json(s);
            var deal = CommitmentReferences.FindDeal(s, row.id);
            Assert.That(HouseguestNotes.DealEnding(s, deal, true), Is.EqualTo("broken"));
            Assert.That(HouseguestNotes.For(s, A(s)).Any(n => n.kind == HouseguestNotes.Kinds.Offer), Is.False);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void MissingReferencesAndLegacyCanonicalLookupStayDetachedAndInert()
        {
            var s = ContentCatalog.Create(17);
            Assert.That(CommitmentReferences.FindPromise(s, null), Is.Null);
            Assert.That(CommitmentReferences.FindDeal(s, "missing"), Is.Null);
            Assert.That(CommitmentReferences.FindCanonical(s, "missing"), Is.Null);
            Assert.Throws<ArgumentNullException>(() => CommitmentReferences.Promises(null));
            Assert.That(s.unifiedCommitmentRulesVersion, Is.Zero); Assert.That(s.unifiedCommitments, Is.Empty);
        }

        private static EpisodeState State()
        {
            var s = ContentCatalog.Create(17); s.week = 3; s.hohId = s.playerId;
            s.unifiedCommitmentRulesVersion = UnifiedCommitments.ProspectiveVersion;
            s.commitmentRulesStartWeek = 1; return s;
        }
        private static UnifiedCommitmentState Row(EpisodeState s, string id, bool promise) => new UnifiedCommitmentState {
            id = id, kind = UnifiedCommitments.Safety,
            sourcePolicy = promise ? UnifiedCommitments.PromisePolicy : UnifiedCommitments.DealPolicy,
            origin = promise ? UnifiedCommitments.PlayerPromise : UnifiedCommitments.StoryDeal,
            makerId = s.playerId, beneficiaryId = A(s), reciprocal = !promise,
            createdWeek = s.week, expiresWeek = s.week + (promise ? 1 : 0), status = DealStatus.Active,
            trustImpact = promise ? DealTrust.Medium : DealTrust.High,
        };
        private static void SetStatus(EpisodeState s, UnifiedCommitmentState row, string status)
        {
            row.status = status;
            if (status == DealStatus.Broken || status == DealStatus.Fulfilled) row.settledWeek = s.week;
            if (status == DealStatus.Broken) { row.brokenById = s.playerId; row.settlementEffectKey = "safety:prospective-reference-test"; }
        }
        private static string A(EpisodeState s) => s.contestants.First(c => c.id != s.playerId).id;
        private static string B(EpisodeState s) => s.contestants.Where(c => c.id != s.playerId).Skip(1).First().id;
        private static string Json(object value) => JsonConvert.SerializeObject(value);
    }
}
