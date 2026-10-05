using System;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Invokes actual command-owned routines on prospective snapshots. These are not accepted
    /// public commands: the production engine/save validators deliberately still reject rules1.
    /// </summary>
    public sealed class UnifiedSafetyCommandOwnerTests
    {
        [TestCase(UnifiedCommitments.PlayerPromise)] [TestCase(UnifiedCommitments.HoHPitch)]
        public void PromiseOwnerUsesOnlyCanonicalStorageAndRetainsTheAuthoredOrigin(string origin)
        {
            var s = State(); string id = "promise-" + s.nextSequence;
            Call("MakePromise", s, Other(s), PromiseKind.Safety, null, origin);
            Assert.That(s.promises, Is.Empty); Assert.That(s.deals, Is.Empty);
            Assert.That(s.unifiedCommitments, Has.Count.EqualTo(1));
            Assert.That(s.unifiedCommitments[0].id, Is.EqualTo(id));
            Assert.That(s.unifiedCommitments[0].origin, Is.EqualTo(origin));
            Assert.That(s.unifiedCommitments[0].expiresWeek, Is.EqualTo(s.week + 1));
            Assert.That(EpisodeValidation.TryValidate(s, out _), Is.False);
        }

        [TestCase(UnifiedCommitments.PlayerPromise)] [TestCase(UnifiedCommitments.HoHPitch)]
        public void IsolatedPromiseCreationRetainsEveryLegacyEffectAndSequence(string origin)
        {
            var next = State(); var legacy = next.Clone(); legacy.unifiedCommitmentRulesVersion = 0;
            Call("MakePromise", legacy, Other(legacy), PromiseKind.Safety, null, origin);
            Call("MakePromise", next, Other(next), PromiseKind.Safety, null, origin);
            Assert.That(Json(CommitmentReferences.Promises(next)), Is.EqualTo(Json(legacy.promises)));
            Assert.That(OutsideStorage(next), Is.EqualTo(OutsideStorage(legacy)));
        }

        [TestCase("duplicate")] [TestCase("capacity")] [TestCase("origin")]
        [TestCase("departed")] [TestCase("target")]
        public void PromiseRefusalPrecedesAnyHistoryEffectsOrSpending(string defect)
        {
            var s = State(); string origin = UnifiedCommitments.PlayerPromise; string target = null;
            if (defect == "duplicate") Call("MakePromise", s, Other(s), PromiseKind.Safety, null, origin);
            if (defect == "capacity") for (int i = 0; i < 200; i++) s.promises.Add(new PromiseState
                { id = "old-" + i, fromId = s.playerId, toId = Other(s), kind = PromiseKind.Vote,
                    status = PromiseStatus.Expired, week = 1, expiresWeek = 1 });
            if (defect == "origin") origin = UnifiedCommitments.PlayerDeal;
            if (defect == "departed") s.Find(Other(s)).status = ContestantStatus.Jury;
            if (defect == "target") target = s.contestants.Last().id;
            string before = Json(s);
            Assert.That(() => Call("MakePromise", s, Other(s), PromiseKind.Safety, target, origin), Throws.Exception);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void ARealLaterPromiseExtensionDoesNotRewriteTheOriginalDuty()
        {
            var s = State(); Call("MakePromise", s, Other(s), PromiseKind.Safety, null, null);
            string original = Json(s.unifiedCommitments[0]); s.week++;
            Call("MakePromise", s, Other(s), PromiseKind.Safety, null, null);
            Assert.That(s.unifiedCommitments, Has.Count.EqualTo(2));
            Assert.That(Json(s.unifiedCommitments[0]), Is.EqualTo(original));
            Assert.That(s.unifiedCommitments[1].expiresWeek, Is.EqualTo(s.week + 1));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)]
        public void ASourceProposalKeepsItsAnswerCostRngAndEffectsWithoutASafetyMirror(int seed)
        {
            var next = State(seed); var legacy = next.Clone(); legacy.unifiedCommitmentRulesVersion = 0;
            var command = new EpisodeCommand { kind = EpisodeCommandKind.ProposeDeal, text = DealKind.SafetyAgreement };
            Call("ProposeDeal", legacy, legacy.Find(Other(legacy)), command);
            Call("ProposeDeal", next, next.Find(Other(next)), command);
            Assert.That(next.deals, Is.Empty);
            Assert.That(Json(CommitmentReferences.Deals(next)), Is.EqualTo(Json(legacy.deals)));
            Assert.That(OutsideStorage(next), Is.EqualTo(OutsideStorage(legacy)));
        }

        [TestCase("duplicate")] [TestCase("capacity")] [TestCase("non-safety-capacity")]
        public void ProposalRefusalIsBeforeTheAcceptanceRollEvenOutsideEngineRollback(string defect)
        {
            var s = State(); string kind = defect == "non-safety-capacity" ? DealKind.FinalTwo : DealKind.SafetyAgreement;
            if (defect == "duplicate") AddOffer(s, UnifiedCommitments.PlayerDeal);
            else for (int i = 0; i < 40; i++) s.unifiedCommitments.Add(new UnifiedCommitmentState
                { id = "deal-story-" + i, kind = UnifiedCommitments.Safety, sourcePolicy = UnifiedCommitments.DealPolicy,
                    origin = UnifiedCommitments.StoryDeal, reciprocal = true, makerId = s.playerId, beneficiaryId = Other(s),
                    createdWeek = 1, expiresWeek = 1, status = DealStatus.Expired, trustImpact = DealTrust.Medium });
            string before = Json(s);
            Assert.That(() => Call("ProposeDeal", s, s.Find(Other(s)), new EpisodeCommand
                { kind = EpisodeCommandKind.ProposeDeal, text = kind }), Throws.Exception);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void ARealLaterDealTermCanBeProposedWithoutErasingThePreviousStoredDuty()
        {
            var s = State(); AddOffer(s, UnifiedCommitments.PlayerDeal);
            string previous = Json(s.unifiedCommitments[0]); s.week++;
            Assert.That(PlayerDeals.CanPropose(s, Other(s), DealKind.SafetyAgreement, null, out string why), Is.True, why);
            uint random = s.randomState;
            Call("ProposeDeal", s, s.Find(Other(s)), new EpisodeCommand
                { kind = EpisodeCommandKind.ProposeDeal, text = DealKind.SafetyAgreement });
            Assert.That(s.randomState, Is.Not.EqualTo(random), "A genuine new proposal reaches its normal acceptance roll.");
            Assert.That(Json(s.unifiedCommitments[0]), Is.EqualTo(previous));
            Assert.That(s.deals, Is.Empty);
        }

        [TestCase(true, false)] [TestCase(false, false)] [TestCase(true, true)] [TestCase(false, true)]
        public void ActualResponsePreservesSourceEffectsAndOriginalIdWhileWritingOnlyTheTrueOwner(bool accept, bool late)
        {
            var next = State(); var draft = AddOffer(next, UnifiedCommitments.NpcOffer);
            var legacy = State(); legacy.unifiedCommitmentRulesVersion = 0; legacy.deals.Add(draft.Clone());
            if (late) { next.week++; legacy.week++; }
            string id = draft.id; int created = draft.week;
            var command = new EpisodeCommand { targetId = id, text = accept ? EpisodeEngine.AcceptDeal : "decline" };
            Call("RespondToDeal", legacy, command); Call("RespondToDeal", next, command);
            Assert.That(next.deals, Is.Empty); Assert.That(Json(CommitmentReferences.Deals(next)), Is.EqualTo(Json(legacy.deals)));
            Assert.That(OutsideStorage(next), Is.EqualTo(OutsideStorage(legacy)));
            Assert.That(next.unifiedCommitments[0].id, Is.EqualTo(id));
            Assert.That(next.unifiedCommitments[0].createdWeek, Is.EqualTo(created));
            Assert.That(next.unifiedCommitments[0].expiresWeek, Is.EqualTo(accept ? next.week : created));
            string after = Json(next); Assert.That(() => Call("RespondToDeal", next, command), Throws.Exception);
            Assert.That(Json(next), Is.EqualTo(after));
        }

        [TestCase("unknown")] [TestCase("promise")] [TestCase("departed")]
        public void WrongOrStaleResponseHasNoMutationOrNullReferenceEscape(string defect)
        {
            var s = State(); string id;
            if (defect == "promise") { Call("MakePromise", s, Other(s), PromiseKind.Safety, null, null); id = s.unifiedCommitments[0].id; }
            else { id = AddOffer(s, UnifiedCommitments.NpcOffer).id; }
            if (defect == "unknown") id = "absent";
            if (defect == "departed") s.Find(Other(s)).status = ContestantStatus.Jury;
            string before = Json(s);
            var error = Assert.Catch(() => Call("RespondToDeal", s, new EpisodeCommand { targetId = id, text = "accept" }));
            Assert.That(error, Is.Not.TypeOf<NullReferenceException>()); Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(true)] [TestCase(false)]
        public void SafetyPreflightNeverPublishesAnOfferOrConsumesASequence(bool duplicate)
        {
            var s = State(); if (duplicate) AddOffer(s, UnifiedCommitments.PlayerDeal);
            var draft = PlayerDeals.Draft(s, Other(s), DealKind.SafetyAgreement, null, "deal-player-900");
            string before = Json(s);
            Assert.That(Store("CanAddDeal", s, draft, UnifiedCommitments.PlayerDeal, null), Is.EqualTo(!duplicate));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        private static EpisodeState State(int seed = 17)
        {
            var s = ContentCatalog.Create(checked((uint)seed)); s.week = 3; s.unifiedCommitmentRulesVersion = 1;
            s.commitmentRulesStartWeek = 1; s.strategyRulesStartWeek = 1; s.dealRulesStartWeek = 1; return s;
        }
        private static string Other(EpisodeState s) => s.contestants.First(p => p.id != s.playerId).id;
        private static DealState AddOffer(EpisodeState s, string origin)
        {
            var draft = PlayerDeals.Draft(s, Other(s), DealKind.SafetyAgreement, null,
                origin == UnifiedCommitments.NpcOffer ? NpcDeals.OfferPrefix + "700" : "deal-player-700");
            if (origin == UnifiedCommitments.NpcOffer) { draft.proposerId = Other(s); draft.recipientId = s.playerId; draft.status = DealStatus.Proposed; }
            Assert.That(Store("TryAddDeal", s, draft, origin, null), Is.True); return draft;
        }
        private static bool Store(string name, params object[] args) => (bool)Invoke(typeof(EpisodeState).Assembly
            .GetType("Gamesim.Simulation.UnifiedCommitmentStore", true), name, args);
        private static void Call(string name, params object[] args) => Invoke(typeof(EpisodeEngine), name, args);
        private static object Invoke(Type type, string name, object[] args)
        {
            var method = type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            try { return method.Invoke(null, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private static string Json(object value) => value is EpisodeState s ? JsonConvert.SerializeObject(s.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToDictionary(f => f.Name, f => f.GetValue(s), StringComparer.Ordinal)) : JsonConvert.SerializeObject(value);
        private static string OutsideStorage(EpisodeState s) => JsonConvert.SerializeObject(s.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => f.Name != "promises" && f.Name != "deals"
                && f.Name != "unifiedCommitments" && f.Name != "unifiedCommitmentRulesVersion")
            .OrderBy(f => f.Name, StringComparer.Ordinal).ToDictionary(f => f.Name, f => f.GetValue(s), StringComparer.Ordinal));
    }
}
