using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Prospective saved-Safety reference leaf tests using actual source writers and completed
    /// nomination/veto routines. These are not accepted public commands, native persistence,
    /// whole-state validation, shipping-save provenance or production rule-1 activation.
    /// </summary>
    public sealed class UnifiedSafetySaveReferenceTests
    {
        [TestCase(UnifiedCommitments.PlayerPromise)] [TestCase(UnifiedCommitments.HoHPitch)]
        [TestCase(UnifiedCommitments.NpcPromise)] [TestCase(UnifiedCommitments.StoryPromise)]
        [TestCase(UnifiedCommitments.PlayerDeal)] [TestCase(UnifiedCommitments.NpcDeal)]
        [TestCase(UnifiedCommitments.NpcOffer)] [TestCase(UnifiedCommitments.Lobby)]
        [TestCase(UnifiedCommitments.StoryDeal)] [TestCase(UnifiedCommitments.CounterDeal)]
        [TestCase(UnifiedCommitments.CounterPrice)]
        public void EveryActualSourceWriterProducesAnInstalledIdentityAndItsOwnPolicy(string origin)
        {
            var s = Written(origin); var row = s.unifiedCommitments.Single(r => r.origin == origin);
            AcceptedUnchanged(s);
            long sequence = long.Parse(row.id.Substring(row.id.LastIndexOf('-') + 1), CultureInfo.InvariantCulture);
            Assert.That(sequence, Is.GreaterThan(0).And.LessThan(s.nextSequence));
            Assert.That(row.trustImpact, Is.EqualTo(row.sourcePolicy == UnifiedCommitments.PromisePolicy ? DealTrust.Medium : DealTrust.High));
            Assert.That(s.promises.Any(p => p.kind == PromiseKind.Safety), Is.False);
            Assert.That(s.deals.Any(d => d.type == DealKind.SafetyAgreement), Is.False);
        }

        [TestCase(UnifiedCommitments.PlayerPromise, 0)] [TestCase(UnifiedCommitments.PlayerPromise, 4)]
        [TestCase(UnifiedCommitments.HoHPitch, 0)] [TestCase(UnifiedCommitments.HoHPitch, 4)]
        [TestCase(UnifiedCommitments.NpcPromise, 0)] [TestCase(UnifiedCommitments.NpcPromise, 4)]
        [TestCase(UnifiedCommitments.StoryPromise, 0)] [TestCase(UnifiedCommitments.StoryPromise, 4)]
        [TestCase(UnifiedCommitments.PlayerDeal, 0)] [TestCase(UnifiedCommitments.PlayerDeal, 4)]
        [TestCase(UnifiedCommitments.NpcDeal, 0)] [TestCase(UnifiedCommitments.NpcDeal, 4)]
        [TestCase(UnifiedCommitments.NpcOffer, 0)] [TestCase(UnifiedCommitments.NpcOffer, 4)]
        [TestCase(UnifiedCommitments.Lobby, 0)] [TestCase(UnifiedCommitments.Lobby, 4)]
        [TestCase(UnifiedCommitments.StoryDeal, 0)] [TestCase(UnifiedCommitments.StoryDeal, 4)]
        public void OrdinaryActualWritersCanStoreUnsettledSafetyBeforeC0(string origin, int c0)
        {
            var s = Written(origin, c0); var row = s.unifiedCommitments.Single(r => r.origin == origin);
            Assert.That(EpisodeEngine.CommitmentRulesOn(s), Is.False);
            Assert.That(row.createdWeek, Is.EqualTo(3)); Assert.That(row.settledWeek, Is.Zero);
            Assert.That(row.status, Is.EqualTo(origin == UnifiedCommitments.NpcOffer ? DealStatus.Proposed : DealStatus.Active));
            AcceptedUnchanged(s);
        }

        [TestCase(UnifiedCommitments.PlayerPromise)] [TestCase(UnifiedCommitments.HoHPitch)]
        [TestCase(UnifiedCommitments.NpcPromise)] [TestCase(UnifiedCommitments.StoryPromise)]
        public void ARealPreC0PromiseCanBeBrokenByItsActualMakerAfterC0Starts(string origin)
        {
            var s = Written(origin, 4); var before = s.unifiedCommitments.Single();
            string id = before.id; int created = before.createdWeek;
            s.week = 4; s.phase = EpisodePhase.Nomination; s.hohId = before.makerId;
            string second = EpisodeEngine.NominationCandidates(s).First(c => c.id != before.beneficiaryId).id;
            Call(typeof(EpisodeEngine), "Nominate", s, before.beneficiaryId, second);
            var broken = s.unifiedCommitments.Single();
            Assert.That((broken.id, broken.createdWeek, broken.settledWeek, broken.brokenById, broken.status),
                Is.EqualTo((id, created, 4, before.makerId, DealStatus.Broken)));
            Assert.That(created, Is.LessThan(s.commitmentRulesStartWeek)); AcceptedUnchanged(s);
        }

        [TestCase(0)] [TestCase(4)]
        public void ActualSettlementCannotBeRelabelledAsHavingNoC0Authority(int c0)
        {
            var s = Broken(false); s.commitmentRulesStartWeek = c0; RefusedUnchanged(s);
        }

        [TestCase(false, "disabled")] [TestCase(true, "disabled")]
        [TestCase(false, "pending")] [TestCase(true, "pending")]
        [TestCase(false, "postdated")] [TestCase(true, "postdated")]
        public void ActualMixedCounterCannotPrecedeItsOwnC0Authority(bool safetyPrice, string defect)
        {
            var s = Counter(safetyPrice);
            if (defect == "disabled") s.commitmentRulesStartWeek = 0;
            else if (defect == "pending") s.commitmentRulesStartWeek = s.week + 1;
            else { s.week++; s.commitmentRulesStartWeek = s.week; }
            RefusedUnchanged(s);
        }

        [TestCase(true, false)] [TestCase(false, false)] [TestCase(true, true)] [TestCase(false, true)]
        public void ActualNpcAnswerKeepsOriginalIdentityAndLawfulLateTerm(bool accept, bool late)
        {
            var s = Written(UnifiedCommitments.NpcOffer); var original = s.unifiedCommitments.Single();
            string id = original.id; int created = original.createdWeek; long sequence = s.nextSequence;
            if (late) s.week++;
            Call(typeof(EpisodeEngine), "RespondToDeal", s, new EpisodeCommand { targetId = id, text = accept ? "accept" : "decline" });
            var answer = s.unifiedCommitments.Single();
            Assert.That((answer.id, answer.createdWeek, answer.status, answer.expiresWeek),
                Is.EqualTo((id, created, accept ? DealStatus.Active : DealStatus.Declined, accept ? s.week : created)));
            Assert.That(s.nextSequence, Is.GreaterThan(sequence)); AcceptedUnchanged(s);
        }

        [TestCase("wrong-prefix")] [TestCase("empty-suffix")] [TestCase("zero")]
        [TestCase("negative")] [TestCase("leading-zero")] [TestCase("future")]
        [TestCase("unconsumed")] [TestCase("overflow")] [TestCase("unicode-digit")]
        [TestCase("space")] [TestCase("control")] [TestCase("plus")]
        public void InvalidSavedSourceIdentityIsRefusedWithoutChangingAnyField(string defect)
        {
            var s = Written(UnifiedCommitments.PlayerPromise); var row = s.unifiedCommitments.Single();
            switch (defect)
            {
                case "wrong-prefix": row.id = "deal-player-1000"; break;
                case "empty-suffix": row.id = "promise-"; break;
                case "zero": row.id = "promise-0"; break;
                case "negative": row.id = "promise--1"; break;
                case "leading-zero": row.id = "promise-01000"; break;
                case "future": row.id = "promise-" + (s.nextSequence + 1); break;
                case "unconsumed": row.id = "promise-" + s.nextSequence; break;
                case "overflow": row.id = "promise-999999999999999999999"; break;
                case "unicode-digit": row.id = "promise-\u0661"; break;
                case "space": row.id = "promise- 1000"; break;
                case "control": row.id = "promise-1000\n"; break;
                case "plus": row.id = "promise-+1000"; break;
            }
            RefusedUnchanged(s);
        }

        [TestCase("promise-canonical")] [TestCase("deal-canonical")]
        [TestCase("raw-promise")] [TestCase("raw-deal")] [TestCase("raw-cross-family")]
        [TestCase("canonical")]
        public void FullCommitmentIdsAreUnambiguousAcrossAllTrueStorageOwners(string defect)
        {
            var s = Written(UnifiedCommitments.PlayerPromise); string id = s.unifiedCommitments.Single().id;
            switch (defect)
            {
                case "promise-canonical": s.promises.Add(PastPromise(s, id)); break;
                case "deal-canonical": s.deals.Add(PastDeal(s, id)); break;
                case "raw-promise": s.promises.Add(PastPromise(s, "old")); s.promises.Add(PastPromise(s, "old")); break;
                case "raw-deal": s.deals.Add(PastDeal(s, "old")); s.deals.Add(PastDeal(s, "old")); break;
                case "raw-cross-family": s.promises.Add(PastPromise(s, "old")); s.deals.Add(PastDeal(s, "old")); break;
                case "canonical": s.unifiedCommitments.Add(s.unifiedCommitments.Single().Clone()); break;
            }
            RefusedUnchanged(s);
        }

        [TestCase("accepted")] [TestCase("proposed")] [TestCase("declined")]
        [TestCase("trust")] [TestCase("reciprocal")] [TestCase("origin-party")]
        [TestCase("source-family")] [TestCase("unsupported-kind")]
        public void CanonicalDealMustKeepActualSourcePolicyAndConsent(string defect)
        {
            var s = Written(UnifiedCommitments.PlayerDeal); var row = s.unifiedCommitments.Single();
            switch (defect)
            {
                case "accepted": row.status = DealStatus.Accepted; break;
                case "proposed": row.status = DealStatus.Proposed; break;
                case "declined": row.status = DealStatus.Declined; break;
                case "trust": row.trustImpact = DealTrust.Medium; break;
                case "reciprocal": row.reciprocal = false; break;
                case "origin-party": row.makerId = B(s); break;
                case "source-family": row.sourcePolicy = UnifiedCommitments.PromisePolicy; break;
                case "unsupported-kind": row.kind = "final-two"; break;
            }
            RefusedUnchanged(s);
        }

        [TestCase("future-created")] [TestCase("future-settled")]
        [TestCase("before-created")] [TestCase("wrong-promise-term")]
        [TestCase("promise-after-expiry")] [TestCase("active-settlement")]
        [TestCase("promise-fulfilled")]
        public void SavedPromiseChronologyCannotInventAnImpossibleSettlement(string defect)
        {
            var s = Broken(false); var row = s.unifiedCommitments.Single();
            switch (defect)
            {
                case "future-created": row.createdWeek = s.week + 1; row.expiresWeek = row.createdWeek + 1; break;
                case "future-settled": row.settledWeek = s.week + 1; break;
                case "before-created": row.settledWeek = row.createdWeek - 1; break;
                case "wrong-promise-term": row.expiresWeek = row.createdWeek; break;
                case "promise-after-expiry": s.week += 2; row.settledWeek = s.week; row.settlementEffectKey = Key(s.week, "nomination", row.makerId, row.beneficiaryId); break;
                case "active-settlement": row.status = DealStatus.Active; break;
                case "promise-fulfilled": row.status = DealStatus.Fulfilled; row.brokenById = null; row.settlementEffectKey = null; break;
            }
            RefusedUnchanged(s);
        }

        [TestCase(true)] [TestCase(false)]
        public void DuplicateBindingDutyAndTermIsRefusedButDifferentPolicyOrDirectionIsNot(bool reciprocal)
        {
            var s = Written(reciprocal ? UnifiedCommitments.StoryDeal : UnifiedCommitments.StoryPromise);
            var original = s.unifiedCommitments.Single();
            var second = original.Clone(); second.id = (reciprocal ? "deal-story-" : "promise-") + s.nextSequence++;
            s.unifiedCommitments.Add(second); RefusedUnchanged(s);
            second.makerId = original.beneficiaryId; second.beneficiaryId = original.makerId;
            if (reciprocal) RefusedUnchanged(s); else AcceptedUnchanged(s);
            s.unifiedCommitments.Remove(second);
            Call(typeof(EpisodeEngine), reciprocal ? "StoryPromise" : "StoryDeal", reciprocal
                ? new object[] { s, s.Find(original.makerId), s.Find(original.beneficiaryId), "Safety" }
                : new object[] { s, s.Find(original.makerId), s.Find(original.beneficiaryId), null, DealKind.SafetyAgreement });
            Assert.That(s.unifiedCommitments, Has.Count.EqualTo(2)); AcceptedUnchanged(s);
        }

        [TestCase(true)] [TestCase(false)]
        public void RealLaterExtensionsAndRetainedEndedHistoryRemainDistinct(bool promise)
        {
            var s = Written(promise ? UnifiedCommitments.StoryPromise : UnifiedCommitments.StoryDeal);
            string original = Json(s.unifiedCommitments.Single()); s.week++;
            if (promise) Call(typeof(EpisodeEngine), "MakePromise", s, A(s), PromiseKind.Safety, null, null);
            else
            {
                var chance = PlayerDeals.AcceptanceChance(s, A(s), DealKind.SafetyAgreement, null);
                SeedFor(s, roll => roll * 100 < chance);
                Call(typeof(EpisodeEngine), "ProposeDeal", s, s.Find(A(s)), new EpisodeCommand { text = DealKind.SafetyAgreement });
            }
            Assert.That(s.unifiedCommitments, Has.Count.EqualTo(2));
            Assert.That(Json(s.unifiedCommitments[0]), Is.EqualTo(original)); AcceptedUnchanged(s);
            s.unifiedCommitments[0].status = DealStatus.Expired; AcceptedUnchanged(s);
        }

        [TestCase(true, 199, true)] [TestCase(true, 200, false)]
        [TestCase(false, 199, true)] [TestCase(false, 200, false)]
        public void SavedCapacityCountsEveryHistoricalSourceFamilyRow(bool promise, int history, bool allowed)
        {
            var s = Written(promise ? UnifiedCommitments.PlayerPromise : UnifiedCommitments.StoryDeal);
            for (int i = 0; i < history; i++)
                if (promise) s.promises.Add(PastPromise(s, "past-p-" + i));
                else s.deals.Add(PastDeal(s, "past-d-" + i));
            if (allowed) AcceptedUnchanged(s); else RefusedUnchanged(s);
        }

        [Test]
        public void BothFamiliesCanRetainTwoHundredRowsAndNpcHistoryCanExceedFortyDeals()
        {
            var s = Written(UnifiedCommitments.StoryDeal);
            for (int i = 0; i < 199; i++) s.deals.Add(PastDeal(s, "past-d-" + i));
            for (int i = 0; i < 200; i++) s.promises.Add(PastPromise(s, "past-p-" + i));
            Assert.That(CommitmentReferences.DealCount(s), Is.EqualTo(200));
            Assert.That(CommitmentReferences.PromiseCount(s), Is.EqualTo(200)); AcceptedUnchanged(s);
        }

        [TestCase(DealKind.FinalTwo)] [TestCase(DealKind.VoteSave)] [TestCase(DealKind.VoteTogether)]
        public void ActualSafetyCounterPreservesDistinctLawfulPriceTermsAndSharedSequence(string priceKind)
        {
            var s = Counter(false, priceKind); var bought = s.unifiedCommitments.Single(); var price = s.deals.Single();
            Assert.That(bought.origin, Is.EqualTo(UnifiedCommitments.CounterDeal));
            Assert.That(price.type, Is.EqualTo(priceKind));
            Assert.That(price.id.Substring(Negotiation.PricePrefix.Length), Is.EqualTo(bought.id.Substring(Negotiation.CounterDealPrefix.Length)));
            Assert.That(price.expiresWeek, Is.EqualTo(priceKind == DealKind.FinalTwo ? 0 : s.week));
            if (priceKind == DealKind.VoteSave) Assert.That(price.targetId, Is.EqualTo(A(s)));
            AcceptedUnchanged(s);
        }

        [Test]
        public void ActualCounterCanKeepItsBoughtLegacyTargetAndCanonicalSafetyPrice()
        {
            var s = Counter(true); Assert.That(s.deals.Single().type, Is.EqualTo(DealKind.TargetAgreement));
            Assert.That(s.deals.Single().targetId, Is.EqualTo(B(s)));
            Assert.That(s.unifiedCommitments.Single().origin, Is.EqualTo(UnifiedCommitments.CounterPrice)); AcceptedUnchanged(s);
        }

        [TestCase(false, "missing")] [TestCase(true, "missing")]
        [TestCase(false, "one-way")] [TestCase(true, "one-way")]
        [TestCase(false, "different-week")] [TestCase(true, "different-week")]
        [TestCase(false, "different-pair")] [TestCase(true, "different-pair")]
        [TestCase(false, "different-sequence")] [TestCase(true, "different-sequence")]
        [TestCase(false, "wrong-bought-prefix")] [TestCase(true, "wrong-bought-prefix")]
        [TestCase(false, "reversed-payer")] [TestCase(true, "reversed-payer")]
        [TestCase(false, "pending")] [TestCase(true, "pending")]
        [TestCase(false, "trust")] [TestCase(true, "trust")]
        [TestCase(false, "term")] [TestCase(true, "term")]
        [TestCase(false, "target")] [TestCase(true, "target")]
        [TestCase(false, "wrong-canonical-origin")] [TestCase(true, "wrong-canonical-origin")]
        public void CorruptMixedConsiderationCannotResolveThroughAnotherOrInventedOwner(bool safetyPrice, string defect)
        {
            var s = Counter(safetyPrice); var raw = s.deals.Single(); var canonical = s.unifiedCommitments.Single();
            switch (defect)
            {
                case "missing": s.deals.Clear(); break;
                case "one-way": raw.linkedDealId = null; break;
                case "different-week": raw.week--; break;
                case "different-pair": raw.recipientId = C(s); break;
                case "different-sequence": raw.id = (safetyPrice ? Negotiation.CounterDealPrefix : Negotiation.PricePrefix) + "900"; canonical.linkedCommitmentId = raw.id; break;
                case "wrong-bought-prefix":
                    if (safetyPrice) { raw.id = "deal-player-1000"; canonical.linkedCommitmentId = raw.id; }
                    else { canonical.id = "deal-player-1000"; raw.linkedDealId = canonical.id; }
                    break;
                case "reversed-payer": (raw.proposerId, raw.recipientId) = (raw.recipientId, raw.proposerId); break;
                case "pending": raw.status = DealStatus.Proposed; break;
                case "trust": raw.trustImpact = DealTrust.Low; break;
                case "term": raw.expiresWeek = 100; break;
                case "target": raw.targetId = safetyPrice ? null : B(s); break;
                case "wrong-canonical-origin": canonical.origin = UnifiedCommitments.PlayerDeal; break;
            }
            RefusedUnchanged(s);
        }

        [TestCase(8, DealKind.VoteSave)] [TestCase(6, DealKind.FinalTwo)]
        public void ActualUnrelatedVetoPriceRetainsItsOppositePayerAndOpenEndedRawTerm(int size, string priceKind)
        {
            var s = State(size); s.phase = EpisodePhase.VetoMeeting; s.hohId = C(s); s.vetoHolderId = s.playerId;
            s.nominees = new List<string> { A(s), B(s) };
            SeedFor(s, roll => roll * 100 < Negotiation.Chance(s, A(s), Negotiation.VetoForAPrice, false));
            Call(typeof(EpisodeEngine), "VetoForAPrice", s, s.Find(A(s)), priceKind);
            Assert.That(s.deals, Has.Count.EqualTo(2)); var price = s.deals.Single(Negotiation.IsPrice);
            Assert.That(price.proposerId, Is.EqualTo(A(s))); Assert.That(price.recipientId, Is.EqualTo(s.playerId));
            Assert.That(price.expiresWeek, Is.Zero); Assert.That(s.deals.Single(d => !Negotiation.IsPrice(d)).id, Does.StartWith("deal-player-"));
            AcceptedUnchanged(s);
        }

        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void CompletedInitialAndReplacementCommandsCarryActualCurrentOrHistoricalRoleEvidence(bool replacement, bool historical)
        {
            var s = Broken(replacement);
            if (historical) RecordHistoricalPower(s);
            Assert.That(UnifiedCommitmentHistory.Breaches(s), Has.Count.EqualTo(1)); AcceptedUnchanged(s);
        }

        [TestCase(false)] [TestCase(true)]
        public void RealFinalVetoSparedDealRequiresNoInventedFulfillmentActor(bool historical)
        {
            var s = Fulfilled(); var row = s.unifiedCommitments.Single();
            Assert.That(row.status, Is.EqualTo(DealStatus.Fulfilled));
            Assert.That(s.ledger.power.Single().hohId, Is.EqualTo(row.makerId));
            if (historical) RecordHistoricalPower(s);
            AcceptedUnchanged(s);
        }

        [TestCase("initial")] [TestCase("replacement")] [TestCase("spared")]
        public void APreviousWeekCannotUseOnlyAnUnfinishedVetoRowAsRevealEvidence(string kind)
        {
            var s = kind == "spared" ? Fulfilled() : Broken(kind == "replacement");
            RecordHistoricalPower(s); AcceptedUnchanged(s);
            var power = s.ledger.power.Single(); power.nominees.Clear(); power.evicteeId = null;
            RefusedUnchanged(s);
        }

        [TestCase("duplicate-unrelated-week")] [TestCase("null-power-row")] [TestCase("missing-power-container")]
        public void StoredPowerOwnershipCannotBeAmbiguousEvenWithoutAReferencedSettledAgreement(string defect)
        {
            var s = Written(UnifiedCommitments.StoryPromise); s.phase = EpisodePhase.Nomination; s.hohId = s.playerId;
            Call(typeof(EpisodeEngine), "Nominate", s, B(s), C(s)); RecordHistoricalPower(s); AcceptedUnchanged(s);
            Assert.That(s.unifiedCommitments.Single().status, Is.EqualTo(DealStatus.Active));
            if (defect == "duplicate-unrelated-week") s.ledger.power.Add(s.ledger.power.Single().Clone());
            else if (defect == "null-power-row") s.ledger.power.Add(null);
            else s.ledger.power = null;
            RefusedUnchanged(s);
        }

        [TestCase("missing-nomination")] [TestCase("wrong-current-hoh")]
        [TestCase("wrong-historical-hoh")] [TestCase("missing-historical-power")]
        [TestCase("duplicate-power")] [TestCase("wrong-replacement")]
        [TestCase("unused-veto")] [TestCase("unfinished-veto")]
        [TestCase("missing-holder")] [TestCase("initial-was-replacement")]
        public void SavedBreachCannotInventOrAmbiguouslyResolveTheActingRole(string defect)
        {
            bool replacement = defect == "wrong-replacement" || defect == "unused-veto" || defect == "unfinished-veto"
                || defect == "missing-holder" || defect == "initial-was-replacement";
            var s = Broken(replacement); var row = s.unifiedCommitments.Single();
            if (defect == "wrong-historical-hoh" || defect == "missing-historical-power") RecordHistoricalPower(s);
            else if (defect == "duplicate-power") { FinishVetoIfNeeded(s); AcceptedUnchanged(s); }
            switch (defect)
            {
                case "missing-nomination": s.Find(row.beneficiaryId).nominationWeeks.Clear(); break;
                case "wrong-current-hoh": s.hohId = B(s); break;
                case "wrong-historical-hoh": s.ledger.power.Single().hohId = C(s); break;
                case "missing-historical-power": s.ledger.power.Clear(); break;
                case "duplicate-power": s.ledger.power.Add(s.ledger.power.Single().Clone()); break;
                case "wrong-replacement": s.ledger.power.Single().replacementId = B(s); break;
                case "unused-veto": s.ledger.power.Single().vetoUsed = false; break;
                case "unfinished-veto": s.vetoResolved = false; break;
                case "missing-holder": s.ledger.power.Single().vetoHolderId = null; break;
                case "initial-was-replacement": row.settlementEffectKey = Key(s.week, "nomination", row.makerId, row.beneficiaryId); break;
            }
            RefusedUnchanged(s);
        }

        [TestCase("prefix-only")] [TestCase("wrong-week")] [TestCase("wrong-actor")]
        [TestCase("wrong-wronged")] [TestCase("extra-tail")] [TestCase("wrong-length")]
        [TestCase("unknown-decision")] [TestCase("nonbroken-key")]
        public void ExactBreachGrammarIsNotReplaceableByARecognizableSafetyPrefix(string defect)
        {
            var s = Broken(false); var row = s.unifiedCommitments.Single();
            switch (defect)
            {
                case "prefix-only": row.settlementEffectKey = "safety:"; break;
                case "wrong-week": row.settlementEffectKey = Key(s.week - 1, "nomination", row.makerId, row.beneficiaryId); break;
                case "wrong-actor": row.settlementEffectKey = Key(s.week, "nomination", row.beneficiaryId, row.makerId); break;
                case "wrong-wronged": row.settlementEffectKey = Key(s.week, "nomination", row.makerId, B(s)); break;
                case "extra-tail": row.settlementEffectKey += "junk"; break;
                case "wrong-length": row.settlementEffectKey = row.settlementEffectKey.Replace("10:nomination", "9:nomination"); break;
                case "unknown-decision": row.settlementEffectKey = Key(s.week, "final-veto", row.makerId, row.beneficiaryId); break;
                case "nonbroken-key": row.status = DealStatus.Expired; row.settledWeek = 0; row.brokenById = null; break;
            }
            RefusedUnchanged(s);
        }

        [TestCase("missing-power")] [TestCase("duplicate-power")] [TestCase("wrong-hoh")]
        [TestCase("nominated-partner")] [TestCase("still-on-block")]
        [TestCase("unfinished")] [TestCase("wrong-current-holder")]
        public void ASparedReceiptCannotBecomeARewardWithoutCompletedVetoEvidence(string defect)
        {
            var s = Fulfilled(); var row = s.unifiedCommitments.Single();
            switch (defect)
            {
                case "missing-power": s.ledger.power.Clear(); break;
                case "duplicate-power": s.ledger.power.Add(s.ledger.power.Single().Clone()); break;
                case "wrong-hoh": s.ledger.power.Single().hohId = B(s); break;
                case "nominated-partner": s.Find(row.beneficiaryId).nominationWeeks.Add(row.settledWeek); break;
                case "still-on-block": s.nominees[0] = row.beneficiaryId; break;
                case "unfinished": s.vetoResolved = false; break;
                case "wrong-current-holder": s.vetoHolderId = C(s); break;
            }
            RefusedUnchanged(s);
        }

        [TestCase(ContestantStatus.Jury)] [TestCase(ContestantStatus.Expelled)] [TestCase(ContestantStatus.Winner)]
        public void HistoricalPartiesNeedExistenceNotCurrentActiveStatus(ContestantStatus status)
        {
            var s = Broken(false); RecordHistoricalPower(s); s.Find(A(s)).status = status;
            AcceptedUnchanged(s);
        }

        [Test]
        public void GroupedPromiseAndDealEvidenceRemainsTwoAgreementsAndOneDurableIncidentAfterLogsRollOff()
        {
            var s = Written(UnifiedCommitments.StoryPromise);
            string promiseId = s.unifiedCommitments.Single().id;
            Call(typeof(EpisodeEngine), "StoryDeal", s, s.Find(s.playerId), s.Find(A(s)), null, DealKind.SafetyAgreement);
            double promiseImpact = WebRules.PromiseImpact(PromiseKind.Safety, PromiseStatus.Broken);
            double dealImpact = DealResolution.Impact(s, CommitmentReferences.Deals(s).Single(), DealStatus.Broken);
            // The source promise is -47, stronger than this ordinary High-trust deal's -30.
            // Trust labels are not the grouped incident's consequence ordering.
            Assert.That(promiseImpact, Is.EqualTo(-47)); Assert.That(dealImpact, Is.EqualTo(-30));
            Assert.That(promiseImpact, Is.LessThan(dealImpact));
            s.phase = EpisodePhase.Nomination; s.hohId = s.playerId;
            Call(typeof(EpisodeEngine), "Nominate", s, A(s), B(s)); RecordHistoricalPower(s);
            s.events.Clear(); foreach (var relation in s.relationships) relation.events.Clear();
            AcceptedUnchanged(s); var incident = UnifiedCommitmentHistory.Breaches(s).Single();
            Assert.That(s.unifiedCommitments, Has.Count.EqualTo(2)); Assert.That(incident.EvidenceIds, Has.Count.EqualTo(2));
            Assert.That(incident.EffectOwnerId, Is.EqualTo(promiseId));
            Assert.That(incident.SourceConsequence, Is.EqualTo(promiseImpact));
            Assert.That(s.unifiedCommitments.Single(r => r.id == incident.EffectOwnerId).sourcePolicy, Is.EqualTo(UnifiedCommitments.PromisePolicy));
            Assert.That(s.story.facts.Where(f => f.kind == FactKinds.BrokenWord && incident.EvidenceIds.Contains(f.refId)), Is.Empty,
                "The weaker deal alias must not invent the selected Promise owner's zero-fact emission.");
        }

        [TestCase("draft-sequence")] [TestCase("unfinished-spared")]
        public void IncompleteWriterOrSettlementDraftIsNotMislabelledAsACompletedSavedCommand(string draft)
        {
            var s = State();
            if (draft == "draft-sequence")
            {
                var row = PlayerDeals.Draft(s, A(s), DealKind.SafetyAgreement, null, "deal-player-" + s.nextSequence);
                Assert.That(Call(typeof(EpisodeState).Assembly.GetType("Gamesim.Simulation.UnifiedCommitmentStore", true),
                    "TryAddDeal", s, row, UnifiedCommitments.PlayerDeal, null), Is.EqualTo(true));
            }
            else
            {
                Call(typeof(EpisodeEngine), "StoryDeal", s, s.Find(s.playerId), s.Find(A(s)), null, DealKind.SafetyAgreement);
                s.hohId = s.playerId;
                Call(typeof(EpisodeEngine), "ResolveUnifiedSafetySpared", s, s.playerId, new List<string> { B(s), C(s) });
                Assert.That(s.unifiedCommitments.Single().status, Is.EqualTo(DealStatus.Fulfilled));
            }
            RefusedUnchanged(s);
        }

        [Test]
        public void AValidReferenceLeafDoesNotMakeAStoryOffDiagnosticPubliclyPlayable()
        {
            var s = Written(UnifiedCommitments.PlayerPromise); AcceptedUnchanged(s);
            Assert.That(UnifiedCommitments.RulesOn(s), Is.True);
            Assert.That(EpisodeEngine.CommitmentRulesOn(s), Is.True);
            Assert.That(EpisodeEngine.StoryOn(s), Is.False);
            string before = Json(s);
            Assert.That(EpisodeValidation.TryValidate(s, out string reason), Is.False);
            Assert.That(reason, Does.Contain("active commitment and story knowledge rules"));
            Assert.Throws<ArgumentException>(() => new EpisodeEngine(s)); Assert.That(Json(s), Is.EqualTo(before));
        }

        private static EpisodeState State(int size = 8)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, 171);
            EpisodeEngine.EnableWeek(s); EpisodeEngine.EnableEconomy(s); EpisodeEngine.EnableLevers(s);
            EpisodeEngine.EnableCommitments(s); EpisodeEngine.EnableRead(s); EpisodeEngine.EnableAgency(s);
            s.strategyRulesStartWeek = 1; s.dealRulesStartWeek = 1; s.week = 3; s.phase = EpisodePhase.Social;
            s.unifiedCommitmentRulesVersion = 1; s.nextSequence = 1000; s.alliances.Clear();
            return s;
        }
        private static string A(EpisodeState s) => s.contestants.First(c => !c.isPlayer).id;
        private static string B(EpisodeState s) => s.contestants.Where(c => !c.isPlayer).Skip(1).First().id;
        private static string C(EpisodeState s) => s.contestants.Where(c => !c.isPlayer).Skip(2).First().id;
        private static string D(EpisodeState s) => s.contestants.Where(c => !c.isPlayer).Skip(3).First().id;
        private static EpisodeState Written(string origin, int c0 = 1)
        {
            if (origin == UnifiedCommitments.CounterDeal) return Counter(false);
            if (origin == UnifiedCommitments.CounterPrice) return Counter(true);
            var s = State(); s.commitmentRulesStartWeek = c0;
            switch (origin)
            {
                case UnifiedCommitments.PlayerPromise: Call(typeof(EpisodeEngine), "MakePromise", s, A(s), PromiseKind.Safety, null, null); break;
                case UnifiedCommitments.HoHPitch:
                    s.phase = EpisodePhase.Nomination; s.hohId = s.playerId;
                    NpcSocialActions.Court(s, s.Find(A(s)), s.Find(s.playerId));
                    Assert.That(s.replyCards, Has.Count.EqualTo(1));
                    Call(typeof(EpisodeEngine), "AnswerHoHPitch", s, s.replyCards.Single(), "promise-safety"); break;
                case UnifiedCommitments.NpcPromise: Call(typeof(NpcPromises), "Give", s, A(s), B(s), PromiseKind.Safety); break;
                case UnifiedCommitments.StoryPromise: Call(typeof(EpisodeEngine), "StoryPromise", s, s.Find(s.playerId), s.Find(A(s)), "Safety"); break;
                case UnifiedCommitments.PlayerDeal:
                    SeedFor(s, roll => roll * 100 < PlayerDeals.AcceptanceChance(s, A(s), DealKind.SafetyAgreement, null));
                    Call(typeof(EpisodeEngine), "ProposeDeal", s, s.Find(A(s)), new EpisodeCommand { text = DealKind.SafetyAgreement }); break;
                case UnifiedCommitments.NpcDeal: Call(typeof(NpcDeals), "Strike", s, A(s), B(s), DealKind.SafetyAgreement); break;
                case UnifiedCommitments.NpcOffer:
                    foreach (var relation in s.relationships) relation.score = 0;
                    s.hohId = s.playerId; s.relationships.Single(r => r.fromId == A(s) && r.toId == s.playerId).score = 40;
                    NpcDeals.Propose(s); break;
                case UnifiedCommitments.Lobby:
                    s.phase = EpisodePhase.Nomination; s.hohId = A(s);
                    SeedFor(s, roll => roll * 100 < StrategyRules.Chance(s, A(s), LobbyAsk.Spare, s.playerId, LobbyApproach.Deal));
                    Call(typeof(EpisodeEngine), "Lobby", s, new EpisodeCommand { targetId = A(s), secondTargetId = s.playerId,
                        text = LobbyAsk.Encode(LobbyAsk.Spare, LobbyApproach.Deal) }); break;
                case UnifiedCommitments.StoryDeal: Call(typeof(EpisodeEngine), "StoryDeal", s, s.Find(s.playerId), s.Find(A(s)), null, DealKind.SafetyAgreement); break;
                default: Assert.Fail("Unknown actual source writer: " + origin); break;
            }
            Assert.That(s.unifiedCommitments.Count(r => r.origin == origin), Is.EqualTo(1), origin);
            AcceptedUnchanged(s);
            return s;
        }
        private static EpisodeState Counter(bool safetyPrice, string priceKind = DealKind.FinalTwo)
        {
            var s = State(safetyPrice || priceKind != DealKind.FinalTwo ? 8 : 6);
            string kind = safetyPrice ? DealKind.TargetAgreement : DealKind.SafetyAgreement;
            if (!safetyPrice && priceKind == DealKind.VoteSave)
            { s.phase = EpisodePhase.Campaign; s.hohId = C(s); s.nominees = new List<string> { A(s), B(s) }; }
            if (!safetyPrice && priceKind == DealKind.VoteTogether)
            { s.phase = EpisodePhase.Campaign; s.hohId = D(s); s.nominees = new List<string> { B(s), C(s) }; }
            var counter = Negotiation.CounterTo(s, A(s), kind, safetyPrice ? B(s) : null);
            Assert.That(counter, Is.Not.Null); Assert.That(counter.price.kind, Is.EqualTo(safetyPrice ? DealKind.SafetyAgreement : priceKind));
            Call(typeof(EpisodeEngine), "Log", s, Negotiation.CounterEventKind, Negotiation.CounterLine(s, counter), new[] { s.playerId, A(s) });
            Call(typeof(EpisodeEngine), "AnswerCounter", s, new EpisodeCommand { targetId = A(s), text = "accept" });
            Assert.That(s.unifiedCommitments, Has.Count.EqualTo(1)); Assert.That(s.deals, Has.Count.EqualTo(1));
            AcceptedUnchanged(s); return s;
        }
        private static EpisodeState Broken(bool replacement)
        {
            var s = Written(UnifiedCommitments.PlayerPromise); s.phase = EpisodePhase.Nomination; s.hohId = s.playerId;
            Call(typeof(EpisodeEngine), "Nominate", s, replacement ? B(s) : A(s), replacement ? C(s) : B(s));
            if (replacement)
            {
                s.phase = EpisodePhase.VetoMeeting; s.vetoHolderId = s.playerId;
                Call(typeof(EpisodeEngine), "ResolveVeto", s, true, B(s), A(s));
            }
            Assert.That(s.unifiedCommitments.Single().status, Is.EqualTo(DealStatus.Broken)); AcceptedUnchanged(s); return s;
        }
        private static EpisodeState Fulfilled()
        {
            var s = Written(UnifiedCommitments.StoryDeal); s.phase = EpisodePhase.Nomination; s.hohId = s.playerId;
            Call(typeof(EpisodeEngine), "Nominate", s, B(s), C(s));
            s.phase = EpisodePhase.VetoMeeting; s.vetoHolderId = s.playerId;
            Call(typeof(EpisodeEngine), "ResolveVeto", s, false, null, null);
            Assert.That(s.unifiedCommitments.Single().status, Is.EqualTo(DealStatus.Fulfilled)); AcceptedUnchanged(s); return s;
        }
        private static void FinishVetoIfNeeded(EpisodeState s)
        {
            if (s.vetoResolved) return;
            s.phase = EpisodePhase.VetoMeeting; s.vetoHolderId = s.playerId;
            Call(typeof(EpisodeEngine), "ResolveVeto", s, false, null, null);
        }
        private static void RecordHistoricalPower(EpisodeState s)
        {
            FinishVetoIfNeeded(s); AcceptedUnchanged(s);
            var finalBlock = s.nominees.ToArray(); Assert.That(finalBlock, Has.Length.EqualTo(2));
            int voters = EpisodeEngine.Voters(s).Count();
            // Use the actual durable reveal writer with a legal final pair, evictee and tally.
            // The later-week projection is a reference-leaf diagnostic, not a claimed public
            // command replay or complete whole-state save; root owns that separate proof.
            Call(typeof(EpisodeEngine), "RecordReveal", s, finalBlock[0], new List<int> { voters, 0 });
            var power = s.ledger.power.Single(p => p.week == s.week);
            Assert.That(power.nominees, Is.EqualTo(finalBlock)); Assert.That(power.evicteeId, Is.EqualTo(finalBlock[0]));
            s.week++; s.hohId = null; s.vetoHolderId = null; s.nominees.Clear(); s.vetoResolved = false;
            AcceptedUnchanged(s);
        }
        private static PromiseState PastPromise(EpisodeState s, string id) => new PromiseState { id = id, fromId = B(s), toId = C(s),
            kind = PromiseKind.Information, status = PromiseStatus.Expired, week = 1, expiresWeek = 1 };
        private static DealState PastDeal(EpisodeState s, string id) => new DealState { id = id, proposerId = B(s), recipientId = C(s),
            type = DealKind.InformationSharing, status = DealStatus.Expired, week = 1, expiresWeek = 0, trustImpact = DealTrust.Low };
        private static void SeedFor(EpisodeState s, Func<double, bool> predicate)
        {
            for (uint seed = 1; seed <= 10000; seed++)
                if (predicate(new SeededRandom(seed).NextDouble())) { s.randomState = seed; return; }
            Assert.Fail("No bounded source roll satisfies the fixture.");
        }
        private static void AcceptedUnchanged(EpisodeState s)
        {
            string before = Json(s); Assert.That(UnifiedSafetySaveReferences.TryValidate(s, out string error), Is.True, error);
            Assert.That(error, Is.Null); Assert.That(Json(s), Is.EqualTo(before));
        }
        private static void RefusedUnchanged(EpisodeState s)
        {
            string before = Json(s); Assert.That(UnifiedSafetySaveReferences.TryValidate(s, out string error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty); Assert.That(Json(s), Is.EqualTo(before));
        }
        private static object Call(Type owner, string name, params object[] args)
        {
            var method = owner.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, owner.Name + "." + name);
            try { return method.Invoke(null, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private static string Key(int week, string decision, string actor, string wronged) => "safety:"
            + week.ToString(CultureInfo.InvariantCulture) + ":" + Part(decision) + Part(actor) + Part(wronged);
        private static string Part(string value) => value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;
        private static string Json(object value) => JsonConvert.SerializeObject(value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(field => field.Name, StringComparer.Ordinal).ToDictionary(field => field.Name, field => field.GetValue(value), StringComparer.Ordinal));
    }
}
