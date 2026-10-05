using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Actual writer hooks on explicitly unsupported prospective snapshots. Reflection reaches only
    /// named simulation owners; this is not evidence that version 1 can pass public command/save gates.
    /// Normalized comparisons preserve the legacy records, RNG, event ordering and source effects.
    /// </summary>
    public sealed class UnifiedSafetyWriterTests
    {
        [TestCase("npc-promise")] [TestCase("npc-deal")]
        [TestCase("story-promise")] [TestCase("story-deal")]
        public void EachSourceWriterMovesOnlySafetyStorageAndKeepsItsOriginalEffects(string writer)
        {
            var canonical = State(); var legacy = Legacy(canonical); uint random = canonical.randomState;
            Write(canonical, writer); Write(legacy, writer);
            Assert.That(canonical.unifiedCommitments, Has.Count.EqualTo(1));
            Assert.That(canonical.unifiedCommitments.Single().origin, Is.EqualTo(writer));
            Assert.That(canonical.promises, Is.Empty); Assert.That(canonical.deals, Is.Empty);
            Assert.That(canonical.randomState, Is.EqualTo(random));
            Assert.That(Normalized(canonical), Is.EqualTo(Json(legacy)));
            Assert.That(UnifiedCommitments.ValidateRecords(canonical, out string why), Is.True, why);
            Assert.That(EpisodeValidation.TryValidate(canonical, out _), Is.False,
                "Writer coverage must not switch on prospective production rules.");
        }

        [TestCase("npc-promise")] [TestCase("npc-deal")]
        [TestCase("story-promise")] [TestCase("story-deal")]
        public void RepeatingASafetyWriterCannotMintAnotherRecordLedgerEventOrSequence(string writer)
        {
            var s = State(); Write(s, writer); string before = Json(s);
            Write(s, writer); Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase("npc-promise", 199, true)] [TestCase("npc-promise", 200, false)]
        [TestCase("npc-deal", 199, true)] [TestCase("npc-deal", 200, false)]
        [TestCase("story-promise", 199, true)] [TestCase("story-promise", 200, false)]
        [TestCase("story-deal", 199, true)] [TestCase("story-deal", 200, false)]
        public void SourceWritersCountEveryRetainedNonSafetyHistoryRowBeforeEffects(string writer, int used, bool allowed)
        {
            var s = State(); Fill(s, writer.EndsWith("promise", StringComparison.Ordinal), used);
            string before = Json(s); Write(s, writer);
            Assert.That(s.unifiedCommitments.Count, Is.EqualTo(allowed ? 1 : 0));
            if (!allowed) Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase("npc-promise", "promise-npc-")] [TestCase("npc-deal", "deal-npc-")]
        [TestCase("story-promise", "promise-")] [TestCase("story-deal", "deal-story-")]
        public void ACollidingSourceIdentityCannotConsumeASequenceOrPublishAnySideEffect(string writer, string prefix)
        {
            var s = State(); Fill(s, false, 1); s.deals.Single().id = prefix + s.nextSequence;
            string before = Json(s); Write(s, writer); Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void AutonomousPromiseSelectionSeesItsCanonicalDuplicateAndNeverPromisesForThePlayer()
        {
            var s = State(); s.hohId = B(s); QuietRelationships(s);
            Assert.That(NpcPromises.TryGive(s, A(s)), Is.True);
            var row = s.unifiedCommitments.Single();
            Assert.That((row.makerId, row.beneficiaryId), Is.EqualTo((A(s), B(s))));
            string before = Json(s); Assert.That(NpcPromises.TryGive(s, A(s)), Is.False);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(true)] [TestCase(false)]
        public void AnActualNpcOfferNeedsConsentAndItsLateAnswerKeepsSourceEffectsAndCreationWeek(bool accept)
        {
            var s = OfferState(); var old = Legacy(s);
            NpcDeals.Propose(s); NpcDeals.Propose(old);
            Assert.That(s.unifiedCommitments.Single().status, Is.EqualTo(DealStatus.Proposed));
            Assert.That(NpcDeals.Between(s, s.playerId, A(s)), Is.Empty, "An unanswered proposal grants no protection.");
            Assert.That(Normalized(s), Is.EqualTo(Json(old)));
            string id = NpcDeals.Pending(s).Single().id;
            var detached = NpcDeals.Pending(s).Single(); detached.status = DealStatus.Active;
            Assert.That(s.unifiedCommitments.Single().status, Is.EqualTo(DealStatus.Proposed));
            string offered = Json(s); NpcDeals.Propose(s); Assert.That(Json(s), Is.EqualTo(offered));
            int created = s.week; s.week++; old.week++;
            var command = Command(EpisodeCommandKind.RespondToDeal, id, null, accept ? "accept" : "decline");
            Invoke(typeof(EpisodeEngine), "RespondToDeal", s, command);
            Invoke(typeof(EpisodeEngine), "RespondToDeal", old, command);
            var answer = s.unifiedCommitments.Single();
            Assert.That(answer.createdWeek, Is.EqualTo(created));
            Assert.That(answer.expiresWeek, Is.EqualTo(accept ? s.week : created));
            Assert.That(answer.status, Is.EqualTo(accept ? DealStatus.Active : DealStatus.Declined));
            Assert.That(Normalized(s), Is.EqualTo(Json(old)));
            string after = Json(s); Refused(() => Invoke(typeof(EpisodeEngine), "RespondToDeal", s, command));
            Assert.That(Json(s), Is.EqualTo(after));
        }

        [Test]
        public void NpcDealPassExpiresCanonicalOffersWithoutExpiringTheLongerPromiseOrInventingEffects()
        {
            var s = OfferState(); NpcDeals.Propose(s); Write(s, "story-promise");
            var old = Legacy(s); s.week++; old.week++;
            string outside = OutsideCommitments(s);
            Invoke(typeof(NpcDeals), "Expire", s); Invoke(typeof(NpcDeals), "Expire", old);
            Assert.That(s.unifiedCommitments.Single(r => r.origin == UnifiedCommitments.NpcOffer).status, Is.EqualTo(DealStatus.Expired));
            Assert.That(s.unifiedCommitments.Single(r => r.origin == UnifiedCommitments.StoryPromise).status, Is.EqualTo(DealStatus.Active));
            Assert.That(OutsideCommitments(s), Is.EqualTo(outside));
            Assert.That(Normalized(s), Is.EqualTo(Json(old)));
        }

        [TestCase(true)] [TestCase(false)]
        public void LobbyCreatesItsThroughNextWeekSafetyOnlyWhenTheActualSourceAnswerLands(bool lands)
        {
            var s = LobbyState(lands); var old = Legacy(s);
            var command = Command(EpisodeCommandKind.Lobby, s.hohId, s.playerId, LobbyAsk.Encode(LobbyAsk.Spare, LobbyApproach.Deal));
            Invoke(typeof(EpisodeEngine), "Lobby", s, command); Invoke(typeof(EpisodeEngine), "Lobby", old, command);
            Assert.That(StrategyRules.Landed(s.lobbies.Single().response), Is.EqualTo(lands));
            Assert.That(s.unifiedCommitments.Count, Is.EqualTo(lands ? 1 : 0));
            if (lands)
            {
                var row = s.unifiedCommitments.Single(); Assert.That(row.origin, Is.EqualTo(UnifiedCommitments.Lobby));
                Assert.That(row.expiresWeek, Is.EqualTo(s.week + 1));
            }
            Assert.That(Normalized(s), Is.EqualTo(Json(old)));
            string before = Json(s); Refused(() => Invoke(typeof(EpisodeEngine), "Lobby", s, command));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void AFullProspectiveSafetyLobbyRefusesBeforeDrawingOrSpendingOrWritingItsPlea()
        {
            var s = LobbyState(true); Fill(s, false, 200); string before = Json(s);
            Refused(() => Invoke(typeof(EpisodeEngine), "Lobby", s,
                Command(EpisodeCommandKind.Lobby, s.hohId, s.playerId, LobbyAsk.Encode(LobbyAsk.Spare, LobbyApproach.Deal))));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(true)] [TestCase(false)]
        public void AcceptingARealCounterStoresBothLinkedOwnersAtomicallyWithNoSeasonRoll(bool safetyPrice)
        {
            var s = CounterState(safetyPrice); var old = Legacy(s); uint random = s.randomState;
            Answer(s); Answer(old);
            Assert.That(s.deals, Has.Count.EqualTo(1)); Assert.That(s.unifiedCommitments, Has.Count.EqualTo(1));
            Assert.That(s.deals.Single().type, Is.Not.EqualTo(DealKind.SafetyAgreement));
            Assert.That(s.unifiedCommitments.Single().origin,
                Is.EqualTo(safetyPrice ? UnifiedCommitments.CounterPrice : UnifiedCommitments.CounterDeal));
            var bought = CommitmentReferences.Deals(s).Single(d => !Negotiation.IsPrice(d));
            var price = Negotiation.PriceOf(s, bought);
            Assert.That(price, Is.Not.Null); Assert.That(Negotiation.BoughtWith(s, price).id, Is.EqualTo(bought.id));
            Assert.That(s.randomState, Is.EqualTo(random)); Assert.That(Normalized(s), Is.EqualTo(Json(old)));
            string before = Json(s); price.status = DealStatus.Declined; bought.linkedDealId = "detached";
            Assert.That(Json(s), Is.EqualTo(before));
            Refused(() => Answer(s)); Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(true, 38, true)] [TestCase(true, 39, false)]
        [TestCase(false, 38, true)] [TestCase(false, 39, false)]
        public void CounterConsentReservesTwoSlotsIncludingCanonicalHistoryBeforeEitherWrite(bool safetyPrice, int used, bool accepted)
        {
            var s = CounterState(safetyPrice); Fill(s, false, used - 1);
            // A genuinely retained safety history row, not a slot freed by moving authorities.
            s.unifiedCommitments.Add(new UnifiedCommitmentState { id = "deal-story-9000", kind = UnifiedCommitments.Safety,
                sourcePolicy = UnifiedCommitments.DealPolicy, origin = UnifiedCommitments.StoryDeal, makerId = B(s), beneficiaryId = C(s),
                reciprocal = true, createdWeek = s.week, expiresWeek = s.week, status = DealStatus.Expired, trustImpact = DealTrust.High });
            string before = Json(s);
            if (accepted) { Answer(s); Assert.That(CommitmentReferences.DealCount(s), Is.EqualTo(40)); }
            else { Refused(() => Answer(s)); Assert.That(Json(s), Is.EqualTo(before)); }
        }

        [TestCase(true)] [TestCase(false)]
        public void BreakingTheBoughtDutyVoidsItsTruePriceOwnerExactlyOnce(bool safetyPrice)
        {
            var s = CounterState(safetyPrice); Answer(s);
            var bought = CommitmentReferences.Deals(s).Single(d => !Negotiation.IsPrice(d));
            var price = Negotiation.PriceOf(s, bought); string npc = A(s);
            s.hohId = npc; s.phase = EpisodePhase.Nomination;
            if (safetyPrice)
            {
                // Target agreement is public, unlike a ballot-settled partnership. The real source
                // setter chooses attribution and invokes the linked-price writer after its effects.
                var owned = s.deals.Single();
                Invoke(typeof(EpisodeEngine), "SettleDeals", s, new List<DealResolution.Verdict> {
                    new DealResolution.Verdict { deal = owned, status = DealStatus.Broken, actorId = npc } });
            }
            else Invoke(typeof(EpisodeEngine), "ResolveUnifiedSafetyNomination", s, "nomination", npc,
                new List<string> { s.playerId, C(s) });
            Assert.That(CommitmentReferences.FindDeal(s, bought.id).status, Is.EqualTo(DealStatus.Broken));
            Assert.That(CommitmentReferences.FindDeal(s, price.id).status, Is.EqualTo(DealStatus.Expired));
            Assert.That(s.events.Count(e => e.kind == "deal-outcome" && e.text.Contains("no longer owe")), Is.EqualTo(1));
            string before = Json(s); Invoke(typeof(EpisodeEngine), "VoidThePrice", s, bought, npc);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void CallingInAStoryPromiseUsesItsCanonicalStableIdentityAndKeepsTheOneCallGate()
        {
            var s = State(); Invoke(typeof(EpisodeEngine), "StoryPromise", s, s.Find(A(s)), s.Find(s.playerId), "Safety");
            string id = s.unifiedCommitments.Single().id;
            Assert.That(Negotiation.Owed(s, A(s)).Select(p => p.id), Is.EqualTo(new[] { id }));
            var p = Negotiation.Owed(s, A(s)).Single(); p.status = PromiseStatus.Expired;
            Assert.That(Negotiation.CallInRefusal(s, A(s), id, Negotiation.Demand), Is.Null);
            SeedFor(s, roll => roll * 100 < Negotiation.Chance(s, A(s), Negotiation.Demand, false));
            var old = Legacy(s);
            Invoke(typeof(EpisodeEngine), "CallInAPromise", s, s.Find(A(s)), id, Negotiation.Demand);
            Invoke(typeof(EpisodeEngine), "CallInAPromise", old, old.Find(A(old)), id, Negotiation.Demand);
            Assert.That(Negotiation.HeldTo(s, CommitmentReferences.FindPromise(s, id)), Is.EqualTo(1.5));
            Assert.That(Negotiation.SafetyHeld(s, A(s), s.playerId), Is.EqualTo(52.5));
            Assert.That(Normalized(s), Is.EqualTo(Json(old)));
            string before = Json(s); Refused(() => Invoke(typeof(EpisodeEngine), "CallInAPromise", s, s.Find(A(s)), id, Negotiation.Demand));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void OrdinaryNonSafetyProposalsCannotSpendSlotsAlreadyUsedByCanonicalSafety()
        {
            var s = State(); Write(s, "story-deal"); Fill(s, false, 39); string before = Json(s);
            Assert.That(PlayerDeals.CanPropose(s, B(s), DealKind.Partnership, null, out _), Is.False);
            Assert.That(Negotiation.CounterTo(s, B(s), DealKind.Partnership, null), Is.Null);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        private static EpisodeState State(int size = 8)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, 171);
            EpisodeEngine.EnableLevers(s); EpisodeEngine.EnableCommitments(s);
            s.strategyRulesStartWeek = 1; s.dealRulesStartWeek = 1; s.week = 3; s.phase = EpisodePhase.Social;
            s.unifiedCommitmentRulesVersion = 1; s.nextSequence = 1000;
            return s;
        }
        private static string A(EpisodeState s) => s.contestants.First(c => !c.isPlayer).id;
        private static string B(EpisodeState s) => s.contestants.Where(c => !c.isPlayer).Skip(1).First().id;
        private static string C(EpisodeState s) => s.contestants.Where(c => !c.isPlayer).Skip(2).First().id;
        private static void QuietRelationships(EpisodeState s)
        { s.alliances.Clear(); foreach (var r in s.relationships) r.score = 0; }
        private static EpisodeState OfferState()
        {
            var s = State(); QuietRelationships(s); s.hohId = s.playerId;
            s.relationships.Single(r => r.fromId == A(s) && r.toId == s.playerId).score = 40;
            return s;
        }
        private static EpisodeState LobbyState(bool lands)
        {
            var s = State(); s.phase = EpisodePhase.Nomination; s.hohId = A(s); s.nominees.Clear();
            double chance = StrategyRules.Chance(s, s.hohId, LobbyAsk.Spare, s.playerId, LobbyApproach.Deal);
            SeedFor(s, roll => lands ? roll * 100 < chance : roll * 100 >= chance);
            return s;
        }
        private static void SeedFor(EpisodeState s, Func<double, bool> predicate)
        {
            for (uint seed = 1; seed <= 10000; seed++)
                if (predicate(new SeededRandom(seed).NextDouble())) { s.randomState = seed; return; }
            Assert.Fail("Fixture could not find the requested bounded source roll.");
        }
        private static EpisodeState CounterState(bool safetyPrice)
        {
            var s = State(safetyPrice ? 8 : 6);
            string kind = safetyPrice ? DealKind.TargetAgreement : DealKind.SafetyAgreement;
            var counter = Negotiation.CounterTo(s, A(s), kind, safetyPrice ? B(s) : null);
            Assert.That(counter, Is.Not.Null);
            Assert.That(counter.price.kind, Is.EqualTo(safetyPrice ? DealKind.SafetyAgreement : DealKind.FinalTwo));
            Invoke(typeof(EpisodeEngine), "Log", s, Negotiation.CounterEventKind, Negotiation.CounterLine(s, counter),
                new[] { s.playerId, A(s) });
            return s;
        }
        private static void Answer(EpisodeState s) => Invoke(typeof(EpisodeEngine), "AnswerCounter", s,
            Command(EpisodeCommandKind.RespondToDeal, A(s), null, "accept"));
        private static EpisodeCommand Command(EpisodeCommandKind kind, string target, string second, string text) =>
            new EpisodeCommand { kind = kind, targetId = target, secondTargetId = second, text = text };
        private static void Write(EpisodeState s, string writer)
        {
            switch (writer)
            {
                case "npc-promise": Invoke(typeof(NpcPromises), "Give", s, A(s), B(s), PromiseKind.Safety); break;
                case "npc-deal": Invoke(typeof(NpcDeals), "Strike", s, A(s), B(s), DealKind.SafetyAgreement); break;
                case "story-promise": Invoke(typeof(EpisodeEngine), "StoryPromise", s, s.Find(s.playerId), s.Find(A(s)), "Safety"); break;
                case "story-deal": Invoke(typeof(EpisodeEngine), "StoryDeal", s, s.Find(s.playerId), s.Find(A(s)), null, DealKind.SafetyAgreement); break;
                default: Assert.Fail("Unknown source writer."); break;
            }
        }
        private static void Fill(EpisodeState s, bool promise, int count)
        {
            for (int i = 0; i < count; i++)
                if (promise) s.promises.Add(new PromiseState { id = "history-" + i, fromId = B(s), toId = C(s),
                    kind = PromiseKind.Vote, status = PromiseStatus.Expired, week = 1, expiresWeek = 1 });
                else s.deals.Add(new DealState { id = "history-" + i, proposerId = B(s), recipientId = C(s),
                    type = DealKind.InformationSharing, status = DealStatus.Expired, week = 1, expiresWeek = 1 });
        }
        private static EpisodeState Legacy(EpisodeState s)
        {
            var copy = s.Clone(); copy.promises = CommitmentReferences.Promises(s).Select(p => p.Clone()).ToList();
            copy.deals = CommitmentReferences.Deals(s).Select(d => d.Clone()).ToList();
            copy.unifiedCommitmentRulesVersion = 0; copy.unifiedCommitments.Clear(); return copy;
        }
        private static string Normalized(EpisodeState s)
        {
            var copy = Legacy(s);
            // The two authorities append independently; their provenance view expressly promises no
            // cross-store ordering. Compare every scalar in identity order, not an invented chronology.
            copy.promises = copy.promises.OrderBy(p => p.id, StringComparer.Ordinal).ToList();
            copy.deals = copy.deals.OrderBy(d => d.id, StringComparer.Ordinal).ToList(); return Json(copy);
        }
        private static string Json(object value) => JsonConvert.SerializeObject(value.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToDictionary(f => f.Name, f => f.GetValue(value), StringComparer.Ordinal));
        private static string OutsideCommitments(EpisodeState s) => JsonConvert.SerializeObject(s.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => f.Name != "promises" && f.Name != "deals" && f.Name != "unifiedCommitments")
            .OrderBy(f => f.Name, StringComparer.Ordinal).ToDictionary(f => f.Name, f => f.GetValue(s), StringComparer.Ordinal));
        private static void Refused(Action action)
        {
            var error = Assert.Throws<Exception>(() => { try { action(); } catch (Exception ex) { throw new Exception(ex.GetType().Name, ex); } });
            Assert.That(error.Message, Is.EqualTo("RuleException"));
        }
        private static object Invoke(Type owner, string method, params object[] arguments)
        {
            var hook = owner.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(hook, Is.Not.Null, owner.Name + "." + method);
            try { return hook.Invoke(null, arguments); }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
        }
    }
}
