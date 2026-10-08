using System;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>Storage-only diagnostics; source writer preflight is not complete public saved-state validation.</summary>
    public sealed class UnifiedCommitmentStoreTests
    {
        // Mode 2 (vote family V3b) keeps canonical Safety too: UnifiedSafetyModeTwoTests holds its writes.
        [TestCase(0)] [TestCase(3)] [TestCase(-1)]
        public void EveryWriterRefusesOtherVersionsWithoutChangingStorageOrCounters(int version)
        {
            var state = State(); state.unifiedCommitmentRulesVersion = version;
            var promise = Promise(state); var deal = Deal(state); Pair(state, false, out var bought, out var price);
            string before = Json(state);
            Assert.That(AddPromise(state, promise, UnifiedCommitments.PlayerPromise), Is.False);
            Assert.That(AddDeal(state, deal, UnifiedCommitments.PlayerDeal), Is.False);
            Assert.That(AddPair(state, bought, price), Is.False); Assert.That(Respond(state, "unknown", true), Is.False);
            Assert.That(Json(state), Is.EqualTo(before));
        }

        [TestCase(UnifiedCommitments.PlayerPromise)] [TestCase(UnifiedCommitments.NpcPromise)]
        [TestCase(UnifiedCommitments.StoryPromise)] [TestCase(UnifiedCommitments.HoHPitch)]
        public void ANewPromiseHasOneCanonicalOwnerAndRetainsEverySourceScalar(string origin)
        {
            var state = State(); var draft = Promise(state);
            if (origin == UnifiedCommitments.NpcPromise)
            { draft.id = "promise-npc-50"; draft.fromId = A(state); draft.toId = B(state); }
            string untouched = OutsideStorage(state), original = Json(draft);
            Assert.That(AddPromise(state, draft, origin), Is.True);
            Assert.That(state.promises, Is.Empty); Assert.That(state.deals, Is.Empty); Assert.That(state.unifiedCommitments, Has.Count.EqualTo(1));
            Assert.That(Json(CommitmentReferences.FindPromise(state, draft.id)), Is.EqualTo(original));
            draft.toId = C(state); Assert.That(state.unifiedCommitments.Single().beneficiaryId, Is.Not.EqualTo(C(state)));
            Assert.That(OutsideStorage(state), Is.EqualTo(untouched));
            Assert.That(EpisodeEngine.StoryOn(state), Is.False);
            Assert.That(EpisodeValidation.TryValidate(state, out string publicError), Is.False, "Storage readiness does not supply missing story authority.");
            Assert.That(publicError, Does.Contain("active commitment and story knowledge rules"));
        }

        [TestCase(UnifiedCommitments.PlayerDeal)] [TestCase(UnifiedCommitments.NpcDeal)]
        [TestCase(UnifiedCommitments.NpcOffer)] [TestCase(UnifiedCommitments.Lobby)] [TestCase(UnifiedCommitments.StoryDeal)]
        public void NewDealsKeepSourceRolesAndTermsWithoutALegacySafetyMirror(string origin)
        {
            var state = State(); var draft = Deal(state, origin); string original = Json(draft), untouched = OutsideStorage(state);
            Assert.That(AddDeal(state, draft, origin), Is.True);
            Assert.That(state.deals, Is.Empty); Assert.That(state.unifiedCommitments.Single().origin, Is.EqualTo(origin));
            Assert.That(Json(CommitmentReferences.FindDeal(state, draft.id)), Is.EqualTo(original));
            draft.expiresWeek = 99; Assert.That(state.unifiedCommitments.Single().expiresWeek, Is.LessThan(99));
            Assert.That(OutsideStorage(state), Is.EqualTo(untouched));
        }

        [TestCase("kind")] [TestCase("target")] [TestCase("impact")] [TestCase("status")]
        [TestCase("actor")] [TestCase("settled")] [TestCase("origin")] [TestCase("id")]
        public void PromiseCreationNeverDropsUnexpectedSourceFields(string defect)
        {
            var state = State(); var draft = Promise(state); string origin = UnifiedCommitments.PlayerPromise;
            switch (defect)
            {
                case "kind": draft.kind = PromiseKind.Vote; break; case "target": draft.targetId = B(state); break;
                case "impact": draft.impact = DealTrust.High; break; case "status": draft.status = PromiseStatus.Broken; break;
                case "actor": draft.brokenById = state.playerId; break; case "settled": draft.settledWeek = state.week; break;
                case "origin": origin = UnifiedCommitments.PlayerDeal; break; case "id": draft.id = "deal-ask-50"; break;
            }
            string before = Json(state), original = Json(draft);
            Assert.That(AddPromise(state, draft, origin), Is.False);
            Assert.That(Json(state), Is.EqualTo(before)); Assert.That(Json(draft), Is.EqualTo(original));
        }

        [TestCase("kind")] [TestCase("target")] [TestCase("trust")] [TestCase("status")]
        [TestCase("actor")] [TestCase("settled")] [TestCase("link")] [TestCase("origin")]
        [TestCase("id")] [TestCase("week")] [TestCase("expiry")]
        public void DealCreationNeverDropsUnexpectedSourceFields(string defect)
        {
            var state = State(); var draft = Deal(state); string origin = UnifiedCommitments.PlayerDeal;
            switch (defect)
            {
                case "kind": draft.type = DealKind.Partnership; break; case "target": draft.targetId = B(state); break;
                case "trust": draft.trustImpact = DealTrust.Critical; break; case "status": draft.status = DealStatus.Accepted; break;
                case "actor": draft.brokenById = state.playerId; break; case "settled": draft.settledWeek = state.week; break;
                case "link": draft.linkedDealId = "elsewhere"; break; case "origin": origin = UnifiedCommitments.CounterDeal; break;
                case "id": draft.id = "deal-ask-50"; break; case "week": draft.week--; draft.expiresWeek--; break;
                case "expiry": draft.expiresWeek++; break;
            }
            string before = Json(state), original = Json(draft);
            Assert.That(AddDeal(state, draft, origin), Is.False);
            Assert.That(Json(state), Is.EqualTo(before)); Assert.That(Json(draft), Is.EqualTo(original));
        }

        [TestCase(true, 199, true)] [TestCase(true, 200, false)]
        [TestCase(false, 199, true)] [TestCase(false, 200, false)]
        public void EveryOtherFamilyHistoryRowConsumesTheSharedTwoHundredCapacity(bool promise, int count, bool allowed)
        {
            var state = State(); FillHistory(state, promise, count); string before = Json(state);
            bool added = promise ? AddPromise(state, Promise(state), UnifiedCommitments.PlayerPromise)
                : AddDeal(state, Deal(state, UnifiedCommitments.StoryDeal), UnifiedCommitments.StoryDeal);
            Assert.That(added, Is.EqualTo(allowed));
            if (!allowed) Assert.That(Json(state), Is.EqualTo(before));
            else Assert.That(promise ? CommitmentReferences.PromiseCount(state) : CommitmentReferences.DealCount(state), Is.EqualTo(200));
        }

        [TestCase(39, true)] [TestCase(40, false)]
        public void PlayerCreationCountsNpcAndSettledHistoryTowardForty(int count, bool allowed)
        {
            var state = State(); FillHistory(state, false, count); string before = Json(state);
            Assert.That(AddDeal(state, Deal(state), UnifiedCommitments.PlayerDeal), Is.EqualTo(allowed));
            if (!allowed) Assert.That(Json(state), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void AnAcceptedCounterIsAtomicAcrossLegacyAndCanonicalOwners(bool safetyPrice)
        {
            var state = State(); Pair(state, safetyPrice, out var bought, out var price);
            string untouched = OutsideStorage(state), boughtBefore = Json(bought), priceBefore = Json(price);
            Assert.That(AddPair(state, bought, price), Is.True);
            Assert.That(state.deals, Has.Count.EqualTo(1)); Assert.That(state.deals.Single().type, Is.Not.EqualTo(DealKind.SafetyAgreement));
            Assert.That(state.unifiedCommitments, Has.Count.EqualTo(1));
            Assert.That(state.unifiedCommitments.Single().origin, Is.EqualTo(safetyPrice ? UnifiedCommitments.CounterPrice : UnifiedCommitments.CounterDeal));
            Assert.That(Json(CommitmentReferences.FindDeal(state, bought.id)), Is.EqualTo(boughtBefore));
            Assert.That(Json(CommitmentReferences.FindDeal(state, price.id)), Is.EqualTo(priceBefore));
            bought.linkedDealId = "changed"; price.status = DealStatus.Broken;
            Assert.That(CommitmentReferences.FindDeal(state, "deal-counter-50").linkedDealId, Is.EqualTo("deal-price-50"));
            Assert.That(CommitmentReferences.FindDeal(state, "deal-price-50").status, Is.EqualTo(DealStatus.Active));
            Assert.That(OutsideStorage(state), Is.EqualTo(untouched));
        }

        [TestCase(38, true)] [TestCase(39, false)] [TestCase(199, false)]
        public void ACounterReservesBothRowsBeforeInstallingEither(int count, bool allowed)
        {
            var state = State(); FillHistory(state, false, count); Pair(state, true, out var bought, out var price); string before = Json(state);
            Assert.That(AddPair(state, bought, price), Is.EqualTo(allowed));
            if (!allowed) Assert.That(Json(state), Is.EqualTo(before));
            else Assert.That(CommitmentReferences.DealCount(state), Is.EqualTo(40));
        }

        [TestCase(37, true)] [TestCase(38, false)]
        public void TheCounterReservationAlsoCountsCanonicalHistory(int legacyCount, bool allowed)
        {
            var state = State(); FillHistory(state, false, legacyCount);
            var prior = Deal(state, UnifiedCommitments.StoryDeal); prior.id = "deal-story-49"; prior.recipientId = B(state);
            Assert.That(AddDeal(state, prior, UnifiedCommitments.StoryDeal), Is.True);
            state.unifiedCommitments.Single().status = DealStatus.Expired;
            Pair(state, true, out var bought, out var price); string before = Json(state);
            Assert.That(AddPair(state, bought, price), Is.EqualTo(allowed));
            if (!allowed) Assert.That(Json(state), Is.EqualTo(before));
            else Assert.That(CommitmentReferences.DealCount(state), Is.EqualTo(40));
        }

        [TestCase("price-kind")] [TestCase("bought-kind")] [TestCase("same-kind")]
        [TestCase("link")] [TestCase("self")] [TestCase("party")] [TestCase("reverse-price")]
        [TestCase("week")] [TestCase("status")] [TestCase("trust")] [TestCase("expiry")]
        [TestCase("target")] [TestCase("suffix")] [TestCase("missing-id")] [TestCase("second-collision")]
        public void InvalidMixedCounterCannotInstallItsOtherwiseValidFirstRow(string defect)
        {
            var state = State(); Pair(state, true, out var bought, out var price);
            switch (defect)
            {
                case "price-kind": price.type = DealKind.InformationSharing; break; case "bought-kind": bought.type = "future"; break;
                case "same-kind": bought.type = price.type; break; case "link": price.linkedDealId = "missing"; break;
                case "self": price.linkedDealId = price.id; break; case "party": price.recipientId = B(state); break;
                case "reverse-price": price.proposerId = A(state); price.recipientId = state.playerId; break;
                case "week": price.week--; break; case "status": price.status = DealStatus.Proposed; break;
                case "trust": bought.trustImpact = DealTrust.Critical; break; case "expiry": bought.expiresWeek = state.week; break;
                case "target": bought.targetId = B(state); break; case "suffix": price.id = "deal-price-51"; bought.linkedDealId = price.id; break;
                case "missing-id": price.id = null; break;
                case "second-collision": state.promises.Add(new PromiseState { id = price.id, kind = PromiseKind.Vote }); break;
            }
            string before = Json(state), originalBought = Json(bought), originalPrice = Json(price);
            Assert.That(AddPair(state, bought, price), Is.False);
            Assert.That(Json(state), Is.EqualTo(before)); Assert.That(Json(bought), Is.EqualTo(originalBought)); Assert.That(Json(price), Is.EqualTo(originalPrice));
        }

        [TestCase(true)] [TestCase(false)]
        public void AnswerKeepsProposalIdentityAndUsesTheActualSourceLateAnswerTerm(bool accept)
        {
            var state = State(); var offer = Deal(state, UnifiedCommitments.NpcOffer);
            Assert.That(AddDeal(state, offer, UnifiedCommitments.NpcOffer), Is.True); state.week++;
            string untouched = OutsideStorage(state); var originalStored = state.unifiedCommitments.Single();
            Assert.That(Respond(state, offer.id, accept), Is.True);
            var answer = CommitmentReferences.FindDeal(state, offer.id);
            Assert.That(answer.week, Is.EqualTo(offer.week)); Assert.That(answer.id, Is.EqualTo(offer.id));
            Assert.That(answer.status, Is.EqualTo(accept ? DealStatus.Active : DealStatus.Declined));
            Assert.That(answer.expiresWeek, Is.EqualTo(accept ? state.week : offer.expiresWeek));
            Assert.That(originalStored.status, Is.EqualTo(DealStatus.Proposed), "An old read reference cannot become a write-through handle.");
            Assert.That(OutsideStorage(state), Is.EqualTo(untouched));
            string once = Json(state); Assert.That(Respond(state, offer.id, accept), Is.False); Assert.That(Json(state), Is.EqualTo(once));
            Assert.That(UnifiedCommitments.ValidateRecords(state, out string error), Is.True, error);
        }

        [TestCase(DealStatus.Proposed, false)] [TestCase(DealStatus.Declined, false)]
        [TestCase(DealStatus.Active, true)] [TestCase(DealStatus.Expired, true)]
        public void OnlyAnsweredNpcOfferHistoryAllowsTheExtendedCreationToAnswerInterval(string status, bool valid)
        {
            var state = State(); var offer = Deal(state, UnifiedCommitments.NpcOffer);
            Assert.That(AddDeal(state, offer, UnifiedCommitments.NpcOffer), Is.True); state.week++;
            var row = state.unifiedCommitments.Single(); row.status = status; row.expiresWeek = state.week;
            Assert.That(UnifiedCommitments.ValidateRecords(state, out _), Is.EqualTo(valid));
            row.expiresWeek = state.week + 1;
            Assert.That(UnifiedCommitments.ValidateRecords(state, out _), Is.False, "No safety offer can claim an acceptance week that has not occurred.");
        }

        [TestCase("other-origin")] [TestCase("departed-maker")] [TestCase("departed-player")] [TestCase("expired")]
        [TestCase("unknown")] [TestCase("covered-term")]
        public void ResponseRefusesAStaleOrAlreadyCoveredOfferWithoutPartialMutation(string defect)
        {
            var state = State(); var offer = Deal(state, UnifiedCommitments.NpcOffer);
            Assert.That(AddDeal(state, offer, UnifiedCommitments.NpcOffer), Is.True);
            string id = offer.id;
            switch (defect)
            {
                case "other-origin": state.unifiedCommitments.Single().origin = UnifiedCommitments.StoryDeal; break;
                case "departed-maker": state.Find(A(state)).status = ContestantStatus.Jury; break;
                case "departed-player": state.Find(state.playerId).status = ContestantStatus.Jury; break;
                case "expired": state.unifiedCommitments.Single().status = DealStatus.Expired; break;
                case "unknown": id = "unknown"; break;
                case "covered-term":
                    state.week++; var newer = Deal(state, UnifiedCommitments.StoryDeal); newer.id = "deal-story-51";
                    Assert.That(AddDeal(state, newer, UnifiedCommitments.StoryDeal), Is.True); break;
            }
            string before = Json(state); Assert.That(Respond(state, id, true), Is.False); Assert.That(Json(state), Is.EqualTo(before));
        }

        [TestCase(true)] [TestCase(false)]
        public void DuplicateCreationRefusesWithoutRenumberingOrUpdatingTheExistingEvidence(bool promise)
        {
            var state = State(); var p = Promise(state); var d = Deal(state);
            Assert.That(promise ? AddPromise(state, p, UnifiedCommitments.PlayerPromise) : AddDeal(state, d, UnifiedCommitments.PlayerDeal), Is.True);
            p.id = "promise-51"; d.id = "deal-player-51"; string before = Json(state);
            Assert.That(promise ? AddPromise(state, p, UnifiedCommitments.PlayerPromise) : AddDeal(state, d, UnifiedCommitments.PlayerDeal), Is.False);
            Assert.That(Json(state), Is.EqualTo(before));
        }

        [TestCase(true)] [TestCase(false)]
        public void LegacySafetyMirrorsAreNeverSilentlyConvertedOrErased(bool promise)
        {
            var state = State(); if (promise) state.promises.Add(Promise(state)); else state.deals.Add(Deal(state));
            string before = Json(state);
            Assert.That(AddPromise(state, Promise(state), UnifiedCommitments.PlayerPromise), Is.False);
            Assert.That(AddDeal(state, Deal(state), UnifiedCommitments.PlayerDeal), Is.False);
            Assert.That(Json(state), Is.EqualTo(before));
        }

        [TestCase(true)] [TestCase(false)]
        public void PromiseCreationAndOfferResponseLeaveLegacyListAndRecordHandlesUntouched(bool promise)
        {
            var state = State(); FillHistory(state, false, 1);
            var legacyList = state.deals; var legacyRecord = state.deals.Single(); string original = Json(legacyRecord);
            if (promise) Assert.That(AddPromise(state, Promise(state), UnifiedCommitments.PlayerPromise), Is.True);
            else
            {
                var offer = Deal(state, UnifiedCommitments.NpcOffer);
                Assert.That(AddDeal(state, offer, UnifiedCommitments.NpcOffer), Is.True);
                Assert.That(state.deals, Is.SameAs(legacyList));
                Assert.That(Respond(state, offer.id, true), Is.True);
            }
            Assert.That(state.deals, Is.SameAs(legacyList)); Assert.That(state.deals.Single(), Is.SameAs(legacyRecord));
            Assert.That(Json(legacyRecord), Is.EqualTo(original));
        }

        [TestCase(true)] [TestCase(false)]
        public void MixedAppendPreservesOldRecordHandlesWithoutMutatingThePreviouslyOwnedList(bool safetyPrice)
        {
            var state = State(); FillHistory(state, false, 1);
            var previousList = state.deals; var previousRecord = previousList.Single(); string old = Json(previousRecord);
            Pair(state, safetyPrice, out var bought, out var price);
            var incomingLegacy = safetyPrice ? bought : price;
            Assert.That(AddPair(state, bought, price), Is.True);
            Assert.That(state.deals, Is.Not.SameAs(previousList)); Assert.That(previousList, Has.Count.EqualTo(1));
            Assert.That(state.deals[0], Is.SameAs(previousRecord)); Assert.That(Json(previousRecord), Is.EqualTo(old));
            Assert.That(state.deals[1], Is.Not.SameAs(incomingLegacy));
            string stored = Json(state.deals[1]); incomingLegacy.status = DealStatus.Broken;
            Assert.That(Json(state.deals[1]), Is.EqualTo(stored));
        }

        private static EpisodeState State()
        {
            var state = ContentCatalog.Create(17); state.week = 3; state.unifiedCommitmentRulesVersion = 1;
            state.commitmentRulesStartWeek = 1; state.strategyRulesStartWeek = 1; return state;
        }
        private static string A(EpisodeState s) => s.contestants.First(p => p.id != s.playerId).id;
        private static string B(EpisodeState s) => s.contestants.Where(p => p.id != s.playerId).Skip(1).First().id;
        private static string C(EpisodeState s) => s.contestants.Where(p => p.id != s.playerId).Skip(2).First().id;
        private static PromiseState Promise(EpisodeState s) => new PromiseState
        { id = "promise-50", fromId = s.playerId, toId = A(s), kind = PromiseKind.Safety, status = PromiseStatus.Active, week = s.week, expiresWeek = s.week + 1 };
        private static DealState Deal(EpisodeState s, string origin = UnifiedCommitments.PlayerDeal)
        {
            string prefix = origin == UnifiedCommitments.NpcOffer ? NpcDeals.OfferPrefix : origin == UnifiedCommitments.NpcDeal ? "deal-npc-"
                : origin == UnifiedCommitments.StoryDeal ? "deal-story-" : origin == UnifiedCommitments.Lobby ? "deal-lobby-" : "deal-player-";
            var d = PlayerDeals.Draft(s, A(s), DealKind.SafetyAgreement, null, prefix + "50");
            if (origin == UnifiedCommitments.NpcOffer) { d.proposerId = A(s); d.recipientId = s.playerId; d.status = DealStatus.Proposed; }
            if (origin == UnifiedCommitments.NpcDeal) { d.proposerId = A(s); d.recipientId = B(s); }
            if (origin == UnifiedCommitments.Lobby) d.expiresWeek++; return d;
        }
        private static void Pair(EpisodeState s, bool safetyPrice, out DealState bought, out DealState price)
        {
            bought = PlayerDeals.Draft(s, A(s), safetyPrice ? DealKind.Partnership : DealKind.SafetyAgreement, null, "deal-counter-50");
            price = PlayerDeals.Draft(s, A(s), safetyPrice ? DealKind.SafetyAgreement : DealKind.FinalTwo, null, "deal-price-50");
            bought.linkedDealId = price.id; price.linkedDealId = bought.id;
        }
        private static void FillHistory(EpisodeState s, bool promise, int count)
        {
            for (int i = 0; i < count; i++)
                if (promise) s.promises.Add(new PromiseState { id = "old-" + i, kind = PromiseKind.Vote, status = PromiseStatus.Expired,
                    fromId = A(s), toId = B(s), week = 1, expiresWeek = 1 });
                else s.deals.Add(new DealState { id = "old-" + i, type = DealKind.InformationSharing, status = DealStatus.Expired,
                    proposerId = A(s), recipientId = B(s), week = 1, expiresWeek = 1 });
        }
        // Keep production methods assembly-internal without granting a friend assembly or exposing
        // a UI writer. This invokes the actual methods in both native Edit and the pure assembly.
        private static bool Invoke(string name, params object[] args)
        {
            var method = typeof(EpisodeState).Assembly.GetType("Gamesim.Simulation.UnifiedCommitmentStore", true)
                .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            try { return (bool)method.Invoke(null, args); }
            catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
        }
        private static bool AddPromise(EpisodeState s, PromiseState d, string origin) => Invoke("TryAddPromise", s, d, origin, null);
        private static bool AddDeal(EpisodeState s, DealState d, string origin) => Invoke("TryAddDeal", s, d, origin, null);
        private static bool AddPair(EpisodeState s, DealState bought, DealState price) => Invoke("TryAddLinkedDeals", s, bought,
            UnifiedCommitments.CounterDeal, price, UnifiedCommitments.CounterPrice, null);
        private static bool Respond(EpisodeState s, string id, bool accept) => Invoke("TryRespond", s, id, accept, null);
        private static string Json(object value) => JsonConvert.SerializeObject(value == null ? null : value.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToDictionary(field => field.Name, field => field.GetValue(value), StringComparer.Ordinal));
        private static string OutsideStorage(EpisodeState s) => JsonConvert.SerializeObject(s.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance).Where(field => field.Name != "promises" && field.Name != "deals" && field.Name != "unifiedCommitments")
            .OrderBy(field => field.Name, StringComparer.Ordinal).ToDictionary(field => field.Name, field => field.GetValue(s), StringComparer.Ordinal));
    }
}
