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
    /// <summary>Prospective pure policy only. Version 1 snapshots are deliberately NOT engine-valid yet.</summary>
    public sealed class UnifiedCommitmentsTests
    {
        [Test]
        public void ModelCloneAndOfferedDraftAreDetachedWithoutWritingAnyState()
        {
            var state = State(); var offer = Row(state, "draft"); string before = Json(state);
            Assert.That(UnifiedCommitments.Offer(state, offer, false, out string error), Is.Not.Null, error);
            var draft = UnifiedCommitments.Offer(state, offer, false, out error);
            Assert.That(Json(draft), Is.EqualTo(Json(offer)));
            draft.id = "elsewhere"; draft.status = DealStatus.Broken;
            Assert.That(offer.id, Is.EqualTo("draft")); Assert.That(offer.status, Is.EqualTo(DealStatus.Active));
            var clone = offer.Clone(); clone.beneficiaryId = B(state);
            Assert.That(offer.beneficiaryId, Is.EqualTo(A(state)));
            Assert.That(Json(state), Is.EqualTo(before));
        }

        [TestCase(UnifiedCommitments.PlayerPromise)] [TestCase(UnifiedCommitments.NpcPromise)]
        [TestCase(UnifiedCommitments.StoryPromise)] [TestCase(UnifiedCommitments.HoHPitch)]
        [TestCase(UnifiedCommitments.PlayerDeal)] [TestCase(UnifiedCommitments.NpcDeal)]
        [TestCase(UnifiedCommitments.NpcOffer)] [TestCase(UnifiedCommitments.Lobby)]
        [TestCase(UnifiedCommitments.StoryDeal)] [TestCase(UnifiedCommitments.CounterDeal)] [TestCase(UnifiedCommitments.CounterPrice)]
        public void EveryExistingSafetyOriginKeepsItsSourcePolicyAndNominalTerm(string origin)
        {
            var state = State(); var row = Row(state, "origin", origin); string before = Json(state);
            bool promise = UnifiedCommitments.IsPromiseOrigin(origin);
            var offered = UnifiedCommitments.Offer(state, row, origin == UnifiedCommitments.PlayerDeal, out string error);
            Assert.That(offered, Is.Not.Null, error);
            Assert.That(offered.reciprocal, Is.EqualTo(!promise));
            Assert.That(offered.sourcePolicy, Is.EqualTo(promise ? UnifiedCommitments.PromisePolicy : UnifiedCommitments.DealPolicy));
            Assert.That(offered.expiresWeek, Is.EqualTo(state.week + (promise || origin == UnifiedCommitments.Lobby ? 1 : 0)));
            Assert.That(Json(state), Is.EqualTo(before));
        }

        [TestCase(0)] [TestCase(2)] [TestCase(-1)]
        public void EveryProspectiveOperationIsInertWithoutExactlyVersionOne(int version)
        {
            var state = State(); state.unifiedCommitmentRulesVersion = version;
            var row = Row(state, "not-installed"); string before = Json(state);
            Assert.That(UnifiedCommitments.RulesOn(state), Is.False);
            Assert.That(UnifiedCommitments.Offer(state, row, false, out _), Is.Null);
            Assert.That(UnifiedCommitments.Find(state, row.id), Is.Null);
            Assert.That(UnifiedCommitments.Binding(state, state.playerId, A(state)), Is.Empty);
            Assert.That(UnifiedCommitments.StrongestProtection(state, state.playerId, A(state)).Strength, Is.Zero);
            Assert.That(UnifiedCommitments.EvaluateNomination(state, null, null, null).Changes, Is.Empty);
            Assert.That(UnifiedCommitments.EvaluateFinalVetoSpared(state, null, null).Changes, Is.Empty);
            Assert.That(UnifiedCommitments.Expire(state, (UnifiedCommitmentExpiry)99).Changes, Is.Empty);
            Assert.That(Json(state), Is.EqualTo(before));
        }

        [Test]
        public void CurrentFactoriesStayDisabledAndFullValidationRefusesTheProspectiveVersion()
        {
            var fresh = ContentCatalog.Create(17);
            Assert.That(fresh.unifiedCommitmentRulesVersion, Is.Zero); Assert.That(fresh.unifiedCommitments, Is.Empty);
            var state = State();
            Assert.That(EpisodeValidation.TryValidate(state, out string error), Is.False, "Pure tests do not authorize production opt-in.");
            Assert.That(error, Does.Contain("Unified commitments are not enabled"));
            state.unifiedCommitmentRulesVersion = 0;
            Assert.That(UnifiedCommitments.ValidateRecords(state, out _), Is.True);
            state.unifiedCommitments.Add(Row(state, "forged"));
            Assert.That(UnifiedCommitments.ValidateRecords(state, out _), Is.False);
        }

        [TestCase("null")] [TestCase("blank-id")] [TestCase("long-id")] [TestCase("control-id")]
        [TestCase("other-kind")] [TestCase("other-policy")] [TestCase("other-origin")] [TestCase("wrong-origin-policy")]
        [TestCase("self")] [TestCase("unknown-party")] [TestCase("wrong-role")] [TestCase("reciprocal-promise")]
        [TestCase("created-zero")] [TestCase("future-created")] [TestCase("wrong-expiry")] [TestCase("unknown-status")]
        [TestCase("fulfilled-promise")] [TestCase("pending-promise")] [TestCase("unknown-trust")] [TestCase("wrong-promise-trust")]
        [TestCase("unsettled-week")] [TestCase("unsettled-breaker")] [TestCase("unsettled-effect")]
        [TestCase("broken-no-week")] [TestCase("broken-wrong-actor")] [TestCase("broken-no-effect")]
        [TestCase("link-self")] [TestCase("unrelated-link")]
        public void StrictRowValidationRefusesMalformedOrUnsupportedSafetyWithoutMutation(string defect)
        {
            var state = State(); var row = Row(state, "strict");
            switch (defect)
            {
                case "null": row = null; break;
                case "blank-id": row.id = " "; break;
                case "long-id": row.id = new string('x', 161); break;
                case "control-id": row.id = "x\ny"; break;
                case "other-kind": row.kind = DealKind.FinalTwo; break;
                case "other-policy": row.sourcePolicy = "future"; break;
                case "other-origin": row.origin = "veto-price"; break;
                case "wrong-origin-policy": row.origin = UnifiedCommitments.PlayerDeal; break;
                case "self": row.beneficiaryId = row.makerId; break;
                case "unknown-party": row.beneficiaryId = "missing"; break;
                case "wrong-role": row.makerId = B(state); break;
                case "reciprocal-promise": row.reciprocal = true; break;
                case "created-zero": row.createdWeek = 0; break;
                case "future-created": row.createdWeek++; row.expiresWeek++; break;
                case "wrong-expiry": row.expiresWeek++; break;
                case "unknown-status": row.status = "future"; break;
                case "fulfilled-promise": row.status = DealStatus.Fulfilled; row.settledWeek = state.week; break;
                case "pending-promise": row.status = DealStatus.Proposed; break;
                case "unknown-trust": row.trustImpact = "future"; break;
                case "wrong-promise-trust": row.trustImpact = DealTrust.High; break;
                case "unsettled-week": row.settledWeek = state.week; break;
                case "unsettled-breaker": row.brokenById = row.makerId; break;
                case "unsettled-effect": row.settlementEffectKey = "safety:forged"; break;
                case "broken-no-week": row.status = DealStatus.Broken; row.brokenById = row.makerId; row.settlementEffectKey = "safety:group"; break;
                case "broken-wrong-actor": row.status = DealStatus.Broken; row.settledWeek = state.week; row.brokenById = row.beneficiaryId; row.settlementEffectKey = "safety:group"; break;
                case "broken-no-effect": row.status = DealStatus.Broken; row.settledWeek = state.week; row.brokenById = row.makerId; break;
                case "link-self": row.linkedCommitmentId = row.id; break;
                case "unrelated-link": row.linkedCommitmentId = "other"; break;
            }
            string before = Json(state), original = Json(row);
            Assert.That(UnifiedCommitments.ValidateRow(state, row, out string error), Is.False, defect);
            Assert.That(error, Is.Not.Empty); Assert.That(Json(row), Is.EqualTo(original)); Assert.That(Json(state), Is.EqualTo(before));
        }

        [TestCase("canonical")] [TestCase("promise")] [TestCase("deal")]
        public void IdCollisionAcrossEveryCommitmentStoreRefusesBeforeAnyWrite(string store)
        {
            var state = State(); var offer = Row(state, "collision");
            if (store == "canonical") state.unifiedCommitments.Add(Row(state, offer.id));
            else if (store == "promise") state.promises.Add(new PromiseState { id = offer.id, kind = PromiseKind.Vote });
            else state.deals.Add(new DealState { id = offer.id, type = DealKind.FinalTwo });
            string before = Json(state);
            Assert.That(UnifiedCommitments.Offer(state, offer, false, out string error), Is.Null);
            Assert.That(error, Does.Contain("identity")); Assert.That(Json(state), Is.EqualTo(before));
        }

        [TestCase(true, 198, 1, false, true)] [TestCase(true, 199, 1, false, false)]
        [TestCase(false, 198, 1, false, true)] [TestCase(false, 199, 1, false, false)]
        [TestCase(false, 38, 1, true, true)] [TestCase(false, 39, 1, true, false)]
        public void EveryHistoricalAndNpcRowStillCountsTowardSourceCapacity(bool promise, int legacy, int canonical, bool playerProposal, bool accepted)
        {
            var state = State();
            for (int i = 0; i < legacy; i++)
                if (promise) state.promises.Add(new PromiseState { id = "old-" + i, kind = PromiseKind.Vote, status = PromiseStatus.Expired });
                else state.deals.Add(new DealState { id = "old-" + i, type = DealKind.FinalTwo, status = DealStatus.Broken, proposerId = A(state), recipientId = B(state) });
            string origin = promise ? UnifiedCommitments.PlayerPromise : playerProposal ? UnifiedCommitments.PlayerDeal : UnifiedCommitments.StoryDeal;
            for (int i = 0; i < canonical; i++)
            { var old = Row(state, "canonical-" + i, origin); old.status = DealStatus.Expired; state.unifiedCommitments.Add(old); }
            var offer = Row(state, "new", origin); string before = Json(state);
            Assert.That(UnifiedCommitments.CanOffer(state, offer, playerProposal, out _), Is.EqualTo(accepted));
            Assert.That(Json(state), Is.EqualTo(before));
        }

        [Test]
        public void APlayerProposalCannotBypassFortyByClaimingTheNpcCapacity()
        {
            var state = State(); var row = Row(state, "player", UnifiedCommitments.PlayerDeal);
            for (int i = 0; i < 40; i++) state.deals.Add(new DealState { id = "old-" + i, type = DealKind.FinalTwo });
            Assert.That(UnifiedCommitments.CanOffer(state, row, false, out string error), Is.False);
            Assert.That(error, Does.Contain("player-proposal"));
        }

        [TestCase(false)] [TestCase(true)]
        public void APromiseOffersNewDurationPastALobbyDealsSparedSettlementEvenWithTheSameNominalExpiry(bool reversed)
        {
            var state = State(); var existing = Row(state, "mutual", UnifiedCommitments.Lobby);
            state.unifiedCommitments.Add(existing);
            var promise = Row(state, "promise", UnifiedCommitments.StoryPromise);
            if (reversed) { promise.makerId = existing.beneficiaryId; promise.beneficiaryId = existing.makerId; }
            string before = Json(state);
            Assert.That(promise.expiresWeek, Is.EqualTo(existing.expiresWeek));
            Assert.That(UnifiedCommitments.Offer(state, promise, false, out string reason), Is.Not.Null, reason);
            Assert.That(reason, Is.Null); Assert.That(Json(state), Is.EqualTo(before));
        }

        [Test]
        public void SameNominalExpiryDoesNotEraseProtectionPastADealsSparedSettlement()
        {
            var state = State();
            state.unifiedCommitments.AddRange(new[] { Row(state, "deal", UnifiedCommitments.Lobby), Row(state, "promise") });
            Assert.That(state.unifiedCommitments.Select(row => row.expiresWeek).Distinct().Count(), Is.EqualTo(1));
            var spared = UnifiedCommitments.EvaluateFinalVetoSpared(state, state.playerId, new[] { B(state), C(state) });
            Assert.That(spared.Changes.Single().Record.id, Is.EqualTo("deal"));
            InstallDetachedChanges(state, spared);
            Assert.That(UnifiedCommitments.Binding(state, state.playerId, A(state)).Select(row => row.id), Is.EqualTo(new[] { "promise" }));
            state.week++;
            Assert.That(UnifiedCommitments.Expire(state, UnifiedCommitmentExpiry.PromiseWeekTurn).Changes, Is.Empty);
            var nextWeek = UnifiedCommitments.EvaluateNomination(state, "next-week-nomination", state.playerId, new[] { A(state) });
            Assert.That(nextWeek.Changes.Single().Record.id, Is.EqualTo("promise"));
            Assert.That(nextWeek.Breaches.Single().EvidenceIds, Is.EqualTo(new[] { "promise" }));
            Assert.That(state.unifiedCommitments.Single(row => row.id == "deal").status, Is.EqualTo(DealStatus.Fulfilled));
        }

        [TestCase(true, false)] [TestCase(true, true)] [TestCase(false, false)] [TestCase(false, true)]
        public void ProspectiveAuthorityRefusesAllLegacySafetyMirrorsIncludingSettledHistory(bool promise, bool settled)
        {
            var state = State();
            if (promise) state.promises.Add(new PromiseState { id = "legacy", kind = PromiseKind.Safety,
                status = settled ? PromiseStatus.Expired : PromiseStatus.Active });
            else state.deals.Add(new DealState { id = "legacy", type = DealKind.SafetyAgreement,
                status = settled ? DealStatus.Broken : DealStatus.Active });
            string before = Json(state);
            Assert.That(UnifiedCommitments.ValidateRecords(state, out string error), Is.False);
            Assert.That(error, Does.Contain("Legacy safety"));
            Assert.That(UnifiedCommitments.Offer(state, Row(state, "new"), false, out _), Is.Null);
            Assert.That(Json(state), Is.EqualTo(before));
        }

        [TestCase(true)] [TestCase(false)]
        public void NullLegacyRowsAreRefusedBeforeInspectingTheirKindOrIdentity(bool promise)
        {
            var state = State();
            if (promise) state.promises.Add(null); else state.deals.Add(null);
            string before = Json(state);
            Assert.That(UnifiedCommitments.ValidateRecords(state, out string error), Is.False);
            Assert.That(error, Does.Contain("storage")); Assert.That(Json(state), Is.EqualTo(before));
        }

        [TestCase("null-state")] [TestCase("null-cast")] [TestCase("null-person")]
        [TestCase("duplicate-id")] [TestCase("null-id")] [TestCase("unknown-player")]
        [TestCase("too-many")] [TestCase("missing-nomination-history")] [TestCase("invalid-week")]
        public void PublicShapeValidatorsRefuseMalformedPartyContextWithoutThrowing(string defect)
        {
            var state = State(); var row = Row(state, "row");
            switch (defect)
            {
                case "null-state": state = null; break;
                case "null-cast": state.contestants = null; break;
                case "null-person": state.contestants[0] = null; break;
                case "duplicate-id": state.contestants[1].id = state.contestants[0].id; break;
                case "null-id": state.contestants[0].id = null; break;
                case "unknown-player": state.playerId = "unknown"; break;
                case "too-many": while (state.contestants.Count <= EpisodeValidation.MaximumCast) state.contestants.Add(state.contestants[0].Clone()); break;
                case "missing-nomination-history": state.contestants[0].nominationWeeks = null; break;
                case "invalid-week": state.week = 0; break;
            }
            string before = Json(state);
            Assert.That(UnifiedCommitments.ValidateRow(state, row, out _), Is.False);
            Assert.That(UnifiedCommitments.ValidateRecords(state, out _), Is.False);
            Assert.That(Json(state), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void DuplicateTermIsRejectedEvenWhenItsReciprocalPartiesAreReversed(bool reversed)
        {
            var state = State(); var first = Row(state, "first", UnifiedCommitments.StoryDeal);
            state.unifiedCommitments.Add(first); var second = first.Clone(); second.id = "second";
            if (reversed) { second.makerId = first.beneficiaryId; second.beneficiaryId = first.makerId; }
            Assert.That(UnifiedCommitments.CanOffer(state, second, false, out string reason), Is.False);
            Assert.That(reason, Does.Contain("same safety duty"));
        }

        [Test]
        public void ReversePromiseAndNextWeekExtensionAndReciprocalConsentAreRealNewDuties()
        {
            var state = State(); var first = Row(state, "first", UnifiedCommitments.StoryPromise);
            state.unifiedCommitments.Add(first);
            var reverse = first.Clone(); reverse.id = "reverse"; reverse.makerId = first.beneficiaryId; reverse.beneficiaryId = first.makerId;
            Assert.That(UnifiedCommitments.CanOffer(state, reverse, false, out _), Is.True);
            var reciprocal = Row(state, "mutual", UnifiedCommitments.Lobby);
            Assert.That(reciprocal.expiresWeek, Is.EqualTo(first.expiresWeek));
            Assert.That(UnifiedCommitments.CanOffer(state, reciprocal, false, out _), Is.True, "Accepting reverse protection is not silently pre-consented by the promise.");
            state.week++;
            var extension = Row(state, "extension", UnifiedCommitments.StoryPromise);
            Assert.That(UnifiedCommitments.CanOffer(state, extension, false, out _), Is.True);
            Assert.That(extension.expiresWeek, Is.GreaterThan(first.expiresWeek));
        }

        [TestCase(DealStatus.Proposed)] [TestCase(DealStatus.Accepted)] [TestCase(DealStatus.Fulfilled)]
        [TestCase(DealStatus.Broken)] [TestCase(DealStatus.Declined)] [TestCase(DealStatus.Expired)]
        public void OnlyActiveRecordsProtectAndResolve(string status)
        {
            var state = State(); var row = Row(state, "status", UnifiedCommitments.StoryDeal); row.status = status;
            if (status == DealStatus.Fulfilled || status == DealStatus.Broken) row.settledWeek = state.week;
            if (status == DealStatus.Broken) { row.brokenById = row.makerId; row.settlementEffectKey = "safety:old"; }
            state.unifiedCommitments.Add(row);
            Assert.That(UnifiedCommitments.Binding(state, row.makerId, row.beneficiaryId), Is.Empty);
            Assert.That(UnifiedCommitments.EvaluateNomination(state, "nom", row.makerId, new[] { row.beneficiaryId }).Changes, Is.Empty);
        }

        [Test]
        public void BindingAndLookupReturnClonesAndRespectPromiseDirection()
        {
            var state = State(); var promise = Row(state, "promise"); state.unifiedCommitments.Add(promise);
            Assert.That(UnifiedCommitments.Binding(state, promise.beneficiaryId, promise.makerId), Is.Empty);
            var found = UnifiedCommitments.Find(state, promise.id); found.status = DealStatus.Expired;
            var binding = UnifiedCommitments.Binding(state, promise.makerId, promise.beneficiaryId).Single(); binding.makerId = B(state);
            Assert.That(promise.status, Is.EqualTo(DealStatus.Active)); Assert.That(promise.makerId, Is.EqualTo(state.playerId));
            Assert.That(UnifiedCommitments.Find(state, "unknown"), Is.Null);
        }

        [TestCase(null, 35)] [TestCase(Negotiation.Remind, 35)]
        [TestCase(Negotiation.Demand, 52.5)] [TestCase(Negotiation.Threaten, 70)]
        public void ProtectionUsesExistingCalledPromiseHoldAndDealThirtyFiveExactlyOnce(string approach, double expected)
        {
            var state = State(); state.hohId = A(state);
            var promise = Row(state, "p", UnifiedCommitments.StoryPromise); promise.makerId = state.hohId; promise.beneficiaryId = state.playerId;
            var deal = Row(state, "d", UnifiedCommitments.NpcOffer); state.unifiedCommitments.AddRange(new[] { promise, deal });
            if (approach != null) RelationshipLedger.RecordOneWay(state, state.hohId, state.playerId, Negotiation.HeldType(PromiseKind.Safety, approach), 0, "Called in.");
            string before = Json(state);
            var protection = UnifiedCommitments.StrongestProtection(state, state.hohId, state.playerId);
            Assert.That(protection.Strength, Is.EqualTo(expected)); Assert.That(protection.EvidenceIds, Is.EqualTo(new[] { "d", "p" }));
            Assert.That(protection.StrongestId, Is.EqualTo(expected > 35 ? "p" : "d"));
            Assert.That(Json(state), Is.EqualTo(before));
            state.unifiedCommitments.Reverse();
            Assert.That(UnifiedCommitments.StrongestProtection(state, state.hohId, state.playerId).StrongestId, Is.EqualTo(protection.StrongestId));
        }

        [Test]
        public void AnUncalledPromiseGivesNoInventedNominationShield()
        {
            var state = State(); state.unifiedCommitments.Add(Row(state, "promise"));
            var protection = UnifiedCommitments.StrongestProtection(state, state.playerId, A(state));
            Assert.That(protection.Strength, Is.Zero); Assert.That(protection.StrongestId, Is.Null);
            Assert.That(protection.EvidenceIds, Is.EqualTo(new[] { "promise" }));
        }

        [TestCase(UnifiedCommitments.PlayerPromise, -47)] [TestCase(UnifiedCommitments.PlayerDeal, -30)]
        [TestCase(UnifiedCommitments.NpcOffer, -45)] [TestCase(UnifiedCommitments.CounterDeal, -45)]
        [TestCase(UnifiedCommitments.CounterPrice, -45)] [TestCase(UnifiedCommitments.Lobby, -30)]
        public void IsolatedBreachKeepsSourceConsequenceNotProtectionWeight(string origin, double expected)
        {
            var state = State(); var row = Row(state, "breach", origin); state.hohId = row.makerId; state.unifiedCommitments.Add(row);
            var result = UnifiedCommitments.EvaluateNomination(state, "initial", row.makerId, new[] { row.beneficiaryId });
            Assert.That(result.Breaches.Single().SourceConsequence, Is.EqualTo(expected));
            Assert.That(result.Changes.Single().Record.brokenById, Is.EqualTo(row.makerId));
            Assert.That(result.Changes.Single().Record.settledWeek, Is.EqualTo(state.week));
            Assert.That(row.status, Is.EqualTo(DealStatus.Active));
        }

        [Test]
        public void OneIncidentMarksAllOverlapsAndChoosesStrongestConsequenceThenOrdinalId()
        {
            var state = State(); state.unifiedCommitments.AddRange(new[]
            { Row(state, "p-z"), Row(state, "p-a", UnifiedCommitments.StoryPromise), Row(state, "d", UnifiedCommitments.PlayerDeal) });
            string before = Json(state);
            var result = UnifiedCommitments.EvaluateNomination(state, "one-nomination", state.playerId, new[] { A(state) });
            Assert.That(result.Changes, Has.Count.EqualTo(3)); Assert.That(result.Breaches, Has.Count.EqualTo(1));
            var incident = result.Breaches.Single();
            Assert.That(incident.EffectOwnerId, Is.EqualTo("p-a")); Assert.That(incident.SourceConsequence, Is.EqualTo(-47));
            Assert.That(incident.EvidenceIds, Is.EqualTo(new[] { "d", "p-a", "p-z" }));
            Assert.That(result.Changes.All(change => change.Record.settlementEffectKey == incident.EffectKey), Is.True);
            Assert.That(Json(state), Is.EqualTo(before));
            state.unifiedCommitments.Reverse();
            var reordered = UnifiedCommitments.EvaluateNomination(state, "one-nomination", state.playerId, new[] { A(state) });
            Assert.That(reordered.Breaches.Single().EffectKey, Is.EqualTo(incident.EffectKey));
            Assert.That(reordered.Breaches.Single().EffectOwnerId, Is.EqualTo(incident.EffectOwnerId));
            InstallDetachedChanges(state, reordered);
            Assert.That(UnifiedCommitments.EvaluateNomination(state, "one-nomination", state.playerId, new[] { A(state) }).Changes, Is.Empty);
            Assert.That(UnifiedCommitments.ValidateRecords(state, out string error), Is.True, error);
        }

        [Test]
        public void ReplacementUsesOnlyItsActualNomineeAndKeepsDifferentVictimsAndDecisionsDistinct()
        {
            var state = State(); var first = Row(state, "first"); var second = Row(state, "second"); second.beneficiaryId = B(state);
            state.unifiedCommitments.AddRange(new[] { first, second }); state.nominees.AddRange(new[] { A(state), B(state) });
            var replacement = UnifiedCommitments.EvaluateNomination(state, "replacement", state.playerId, new[] { B(state) });
            Assert.That(replacement.Changes.Select(change => change.Record.id), Is.EqualTo(new[] { "second" }));
            var initial = UnifiedCommitments.EvaluateNomination(state, "initial", state.playerId, new[] { A(state), B(state) });
            Assert.That(initial.Breaches.Select(item => item.EffectKey).Distinct().Count(), Is.EqualTo(2));
            Assert.That(initial.Breaches.Single(item => item.WrongedId == B(state)).EffectKey, Is.Not.EqualTo(replacement.Breaches.Single().EffectKey));
        }

        [TestCase("null-nominees")] [TestCase("empty")] [TestCase("duplicate")] [TestCase("too-many")]
        [TestCase("actor-nominee")] [TestCase("not-hoh")] [TestCase("unknown")] [TestCase("blank-decision")]
        public void InvalidDecisionContextCannotProduceASettlement(string defect)
        {
            var state = State(); state.unifiedCommitments.Add(Row(state, "standing"));
            string actor = state.playerId, decision = "nom"; IReadOnlyList<string> targets = new[] { A(state) };
            switch (defect)
            {
                case "null-nominees": targets = null; break; case "empty": targets = Array.Empty<string>(); break;
                case "duplicate": targets = new[] { A(state), A(state) }; break;
                case "too-many": targets = new[] { A(state), B(state), C(state) }; break;
                case "actor-nominee": targets = new[] { actor }; break; case "not-hoh": actor = B(state); break;
                case "unknown": targets = new[] { "unknown" }; break; case "blank-decision": decision = " "; break;
            }
            string before = Json(state);
            Assert.Throws<ArgumentException>(() => UnifiedCommitments.EvaluateNomination(state, decision, actor, targets));
            Assert.That(Json(state), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void ReciprocalDealsCanBreakEitherWayButUnilateralPromisesCannot(bool reverse)
        {
            var state = State(); var deal = Row(state, "deal", UnifiedCommitments.StoryDeal); var promise = Row(state, "promise");
            state.unifiedCommitments.AddRange(new[] { deal, promise });
            string actor = reverse ? A(state) : state.playerId, wronged = reverse ? state.playerId : A(state); state.hohId = actor;
            var result = UnifiedCommitments.EvaluateNomination(state, "nom", actor, new[] { wronged });
            Assert.That(result.Changes.Count, Is.EqualTo(reverse ? 1 : 2));
            Assert.That(result.Breaches.Single().ActorId, Is.EqualTo(actor)); Assert.That(result.Breaches.Single().WrongedId, Is.EqualTo(wronged));
        }

        [TestCase(false, false, true)] [TestCase(true, false, false)] [TestCase(false, true, false)]
        public void FinalVetoKeepsOnlyTheNeverNominatedSparedDeal(bool onFinalBlock, bool nominatedEarlier, bool kept)
        {
            var state = State(); state.unifiedCommitments.AddRange(new[] { Row(state, "promise"), Row(state, "deal", UnifiedCommitments.Lobby) });
            if (nominatedEarlier) state.Find(A(state)).nominationWeeks.Add(state.week);
            string before = Json(state);
            var result = UnifiedCommitments.EvaluateFinalVetoSpared(state, state.playerId, new[] { onFinalBlock ? A(state) : B(state), C(state) });
            Assert.That(result.Changes.Count, Is.EqualTo(kept ? 1 : 0)); Assert.That(result.Breaches, Is.Empty);
            if (kept)
            {
                var row = result.Changes.Single().Record; Assert.That(row.id, Is.EqualTo("deal")); Assert.That(row.status, Is.EqualTo(DealStatus.Fulfilled));
                Assert.That(row.brokenById, Is.Null); Assert.That(row.settlementEffectKey, Is.Null);
                Assert.That(UnifiedCommitments.ValidateRow(state, row, out string error), Is.True, error);
            }
            Assert.That(Json(state), Is.EqualTo(before));
        }

        [Test]
        public void ExpiryUsesSeparateSourceBoundariesWithoutInventingAnOutcome()
        {
            var state = State(); var promise = Row(state, "promise"); var deal = Row(state, "deal", UnifiedCommitments.StoryDeal);
            var pending = Row(state, "pending", UnifiedCommitments.NpcOffer); pending.status = DealStatus.Proposed;
            state.unifiedCommitments.AddRange(new[] { promise, deal, pending }); state.week++;
            Assert.That(UnifiedCommitments.Expire(state, UnifiedCommitmentExpiry.PromiseWeekTurn).Changes, Is.Empty, "The promised next week still binds.");
            var dealPass = UnifiedCommitments.Expire(state, UnifiedCommitmentExpiry.DealPass);
            Assert.That(dealPass.Changes.Select(change => change.Record.id), Is.EqualTo(new[] { "deal", "pending" }));
            Assert.That(dealPass.Breaches, Is.Empty); state.week++;
            var promiseTurn = UnifiedCommitments.Expire(state, UnifiedCommitmentExpiry.PromiseWeekTurn);
            Assert.That(promiseTurn.Changes.Single().Record.id, Is.EqualTo("promise"));
            Assert.That(promiseTurn.Changes.Single().Record.status, Is.EqualTo(DealStatus.Expired));
            Assert.That(promiseTurn.Changes.Single().Record.settledWeek, Is.Zero);
            Assert.That(promiseTurn.Changes.Single().Record.settlementEffectKey, Is.Null);
            Assert.That(state.unifiedCommitments.All(row => row.status != DealStatus.Expired), Is.True);
        }

        [Test]
        public void DepartureLapsesDealsButDoesNotRewriteSourceSafetyPromiseExpiry()
        {
            var state = State(); state.unifiedCommitments.AddRange(new[] { Row(state, "promise"), Row(state, "deal", UnifiedCommitments.StoryDeal) });
            Assert.Throws<ArgumentException>(() => UnifiedCommitments.Expire(state, UnifiedCommitmentExpiry.Departure, A(state)));
            state.Find(A(state)).status = ContestantStatus.Jury;
            var result = UnifiedCommitments.Expire(state, UnifiedCommitmentExpiry.Departure, A(state));
            Assert.That(result.Changes.Single().Record.id, Is.EqualTo("deal")); Assert.That(result.Breaches, Is.Empty);
            Assert.That(UnifiedCommitments.Binding(state, state.playerId, A(state)), Is.Empty);
        }

        [Test]
        public void BreachIdentityDoesNotDependOnCurrentCultureOrDelimiterConcatenation()
        {
            var state = State(); state.unifiedCommitments.Add(Row(state, "p")); var saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                string first = UnifiedCommitments.EvaluateNomination(state, "a:1:b", state.playerId, new[] { A(state) }).Breaches.Single().EffectKey;
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
                Assert.That(UnifiedCommitments.EvaluateNomination(state, "a:1:b", state.playerId, new[] { A(state) }).Breaches.Single().EffectKey, Is.EqualTo(first));
                Assert.That(UnifiedCommitments.EvaluateNomination(state, "a:1:b:", state.playerId, new[] { A(state) }).Breaches.Single().EffectKey, Is.Not.EqualTo(first));
            }
            finally { CultureInfo.CurrentCulture = saved; }
        }

        private static EpisodeState State()
        {
            var state = ContentCatalog.Create(17); state.week = 3; state.phase = EpisodePhase.Nomination; state.hohId = state.playerId;
            state.unifiedCommitmentRulesVersion = UnifiedCommitments.ProspectiveVersion;
            state.strategyRulesStartWeek = 1; state.commitmentRulesStartWeek = 1;
            return state;
        }
        private static string A(EpisodeState state) => state.contestants.First(person => person.id != state.playerId).id;
        private static string B(EpisodeState state) => state.contestants.Where(person => person.id != state.playerId).Skip(1).First().id;
        private static string C(EpisodeState state) => state.contestants.Where(person => person.id != state.playerId).Skip(2).First().id;
        private static UnifiedCommitmentState Row(EpisodeState state, string id, string origin = UnifiedCommitments.PlayerPromise)
        {
            bool promise = UnifiedCommitments.IsPromiseOrigin(origin);
            string maker = state.playerId, beneficiary = A(state);
            if (origin == UnifiedCommitments.NpcOffer) { maker = A(state); beneficiary = state.playerId; }
            else if (origin == UnifiedCommitments.NpcPromise || origin == UnifiedCommitments.NpcDeal) { maker = A(state); beneficiary = B(state); }
            return new UnifiedCommitmentState
            {
                id = id, kind = UnifiedCommitments.Safety, sourcePolicy = promise ? UnifiedCommitments.PromisePolicy : UnifiedCommitments.DealPolicy,
                origin = origin, makerId = maker, beneficiaryId = beneficiary, reciprocal = !promise, createdWeek = state.week,
                expiresWeek = state.week + (promise || origin == UnifiedCommitments.Lobby ? 1 : 0), status = DealStatus.Active,
                trustImpact = promise ? DealTrust.Medium : DealTrust.High,
                linkedCommitmentId = origin == UnifiedCommitments.CounterDeal || origin == UnifiedCommitments.CounterPrice ? "external-consideration" : null,
            };
        }
        private static void InstallDetachedChanges(EpisodeState state, UnifiedCommitmentEvaluation result)
        {
            foreach (var change in result.Changes)
            {
                int at = state.unifiedCommitments.FindIndex(row => row.id == change.Record.id);
                state.unifiedCommitments[at] = change.Record.Clone();
            }
        }
        // Snapshot public storage, not derived EpisodeState.Active/Player queries: malformed
        // cast inputs must reach the validator rather than throw while preparing the assertion.
        // Every mutable public field remains in the immutability comparison.
        private static string Json(object value) => JsonConvert.SerializeObject(value == null ? null : value.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToDictionary(field => field.Name, field => field.GetValue(value), StringComparer.Ordinal));
    }
}
