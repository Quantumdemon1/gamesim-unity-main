using System;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>The actual nomination reader, not merely the prospective policy leaf. Rules remain disabled in production.</summary>
    public sealed class UnifiedSafetyProtectionTests
    {
        [TestCase(EpisodePhase.Nomination, null, 35d)]
        [TestCase(EpisodePhase.Nomination, Negotiation.Remind, 35d)]
        [TestCase(EpisodePhase.Nomination, Negotiation.Demand, 52.5d)]
        [TestCase(EpisodePhase.Nomination, Negotiation.Threaten, 70d)]
        [TestCase(EpisodePhase.VetoMeeting, null, 35d)]
        [TestCase(EpisodePhase.VetoMeeting, Negotiation.Remind, 35d)]
        [TestCase(EpisodePhase.VetoMeeting, Negotiation.Demand, 52.5d)]
        [TestCase(EpisodePhase.VetoMeeting, Negotiation.Threaten, 70d)]
        public void InitialAndReplacementNominationReadTheStrongestSafetyOnce(EpisodePhase phase, string approach, double expected)
        {
            var s = State(); s.phase = phase;
            var earlier = Promise(s, "promise-a"); earlier.createdWeek--; earlier.expiresWeek--;
            var extended = Deal(s, "deal-b"); extended.origin = UnifiedCommitments.Lobby;
            extended.makerId = s.playerId; extended.beneficiaryId = s.hohId; extended.expiresWeek++;
            s.unifiedCommitments.AddRange(new[] { earlier, Promise(s, "promise-b"), Deal(s, "deal-a"), extended });
            if (approach != null) Hold(s, approach);
            AssertRead(s, 12 + expected);
            s.unifiedCommitments.Reverse();
            AssertRead(s, 12 + expected);
        }

        [TestCase(false, 70d)] [TestCase(true, 0d)]
        public void ACalledPromiseProtectsOnlyItsBeneficiary(bool reversed, double expected)
        {
            var s = State(); var row = Promise(s, "promise");
            if (reversed) { row.makerId = s.playerId; row.beneficiaryId = s.hohId; }
            s.unifiedCommitments.Add(row); Hold(s, Negotiation.Threaten);
            AssertRead(s, 12 + expected);
        }

        [TestCase(false)] [TestCase(true)]
        public void AReciprocalDealProtectsEitherPartyButNoThirdPerson(bool reversed)
        {
            var s = State(); var row = Deal(s, "deal");
            if (reversed) { row.makerId = s.playerId; row.beneficiaryId = s.hohId; }
            s.unifiedCommitments.Add(row); AssertRead(s, 47);
            string before = Json(s), other = Other(s);
            Assert.That(StrategyRules.NominationReluctance(s, s.hohId, other), Is.EqualTo(s.Score(s.hohId, other)));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false, DealStatus.Proposed)] [TestCase(false, DealStatus.Declined)]
        [TestCase(false, DealStatus.Expired)] [TestCase(false, DealStatus.Fulfilled)]
        [TestCase(false, DealStatus.Broken)] [TestCase(true, DealStatus.Expired)]
        [TestCase(true, DealStatus.Broken)]
        public void NonactiveSafetyAddsNoProtectionAndDoesNotInventABreachHistoryPolicy(bool promise, string status)
        {
            var s = State(); var row = promise ? Promise(s, "promise") : Deal(s, "deal");
            row.status = status;
            if (status == DealStatus.Fulfilled || status == DealStatus.Broken) row.settledWeek = s.week;
            if (status == DealStatus.Broken)
            {
                row.brokenById = row.makerId; row.settlementEffectKey = "safety:reader-test";
            }
            s.unifiedCommitments.Add(row); Hold(s, Negotiation.Threaten);
            // Canonical incident/breach-history scoring is a separate prerequisite; do not count
            // each source-shaped evidence row as another penalty while routing protection.
            AssertRead(s, 12);
        }

        [Test]
        public void ACalledPromiseRetainsItsLongerPolicyAfterAnEqualExpiryLobbyDealWasKept()
        {
            var s = State(); var promise = Promise(s, "promise"); var lobby = Deal(s, "lobby");
            lobby.origin = UnifiedCommitments.Lobby; lobby.makerId = s.playerId; lobby.beneficiaryId = s.hohId; lobby.expiresWeek++;
            s.unifiedCommitments.AddRange(new[] { promise, lobby }); Hold(s, Negotiation.Demand);
            AssertRead(s, 64.5);
            lobby.status = DealStatus.Fulfilled; lobby.settledWeek = s.week; s.week++;
            AssertRead(s, 64.5);
            promise.status = DealStatus.Expired; AssertRead(s, 12);
        }

        [TestCase(EpisodePhase.Nomination)] [TestCase(EpisodePhase.VetoMeeting)]
        public void OtherFamiliesTargetsAndPleasKeepTheirIndependentEffects(EpisodePhase phase)
        {
            var s = State(); s.phase = phase; AddOtherTerms(s);
            s.unifiedCommitments.AddRange(new[] { Deal(s, "deal"), Promise(s, "promise") });
            Hold(s, Negotiation.Demand);
            // 12 warmth +50 final-two +20 partnership -35 broken information deal
            // -40 target agreement +17 spare plea -4 target plea +52.5 strongest safety.
            AssertRead(s, 72.5);
        }

        [TestCase(0)] [TestCase(4)]
        public void StrategyDisabledOrNotYetStartedStillReadsWarmthAlone(int starts)
        {
            var s = State(); s.strategyRulesStartWeek = starts;
            s.unifiedCommitments.Add(Deal(s, "deal")); Hold(s, Negotiation.Threaten); AddOtherTerms(s);
            AssertRead(s, 12);
        }

        [TestCase(false, 40d)] [TestCase(true, 180d)]
        public void LegacySeasonRetainsItsExactAdditiveSafetyAndCallInPolicy(bool commitments, double expected)
        {
            var s = State(); s.unifiedCommitmentRulesVersion = 0; s.commitmentRulesStartWeek = commitments ? 1 : 0;
            AddOtherTerms(s);
            // Replace the +50 final-two with two +35 safety deals, retaining the old sum.
            s.deals.RemoveAll(d => d.type == DealKind.FinalTwo);
            for (int i = 0; i < 2; i++)
            {
                s.deals.Add(new DealState { id = "legacy-deal-" + i, type = DealKind.SafetyAgreement,
                    proposerId = s.hohId, recipientId = s.playerId, status = DealStatus.Active, week = s.week, expiresWeek = s.week });
                s.promises.Add(new PromiseState { id = "legacy-promise-" + i, kind = PromiseKind.Safety,
                    fromId = s.hohId, toId = s.playerId, status = PromiseStatus.Active, week = s.week, expiresWeek = s.week + 1 });
            }
            Hold(s, Negotiation.Threaten); string before = Json(s);
            Assert.That(StrategyRules.NominationReluctance(s, s.hohId, s.playerId), Is.EqualTo(expected));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void SafetyDoesNotBecomeAnUnpromisedVetoCommitment()
        {
            var s = State(); s.phase = EpisodePhase.VetoMeeting; s.vetoHolderId = s.hohId;
            s.nominees.Add(s.playerId); s.nominees.Add(Other(s));
            double beforeWeight = StrategyRules.VetoWillingness(s, s.vetoHolderId, s.playerId);
            s.unifiedCommitments.Add(Deal(s, "deal")); Hold(s, Negotiation.Threaten); string before = Json(s);
            Assert.That(StrategyRules.VetoWillingness(s, s.vetoHolderId, s.playerId), Is.EqualTo(beforeWeight));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void ACompetingLegacySafetyMirrorIsRefusedRatherThanSilentlyStacked()
        {
            var s = State(); s.unifiedCommitments.Add(Deal(s, "canonical"));
            s.deals.Add(new DealState { id = "mirror", type = DealKind.SafetyAgreement,
                proposerId = s.hohId, recipientId = s.playerId, status = DealStatus.Active });
            string before = Json(s);
            Assert.Throws<ArgumentException>(() => StrategyRules.NominationReluctance(s, s.hohId, s.playerId));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void ReadingProspectiveProtectionDoesNotAuthorizeAProductionSeason()
        {
            var s = State(); s.unifiedCommitments.Add(Deal(s, "deal")); AssertRead(s, 47);
            Assert.That(EpisodeValidation.TryValidate(s, out _), Is.False);
        }

        private static EpisodeState State()
        {
            var s = ContentCatalog.Create(17); s.week = 3; s.phase = EpisodePhase.Nomination;
            s.hohId = s.contestants.First(c => c.id != s.playerId).id;
            s.strategyRulesStartWeek = 1; s.commitmentRulesStartWeek = 1; s.unifiedCommitmentRulesVersion = 1;
            s.relationships.Single(r => r.fromId == s.hohId && r.toId == s.playerId).score = 12;
            return s;
        }
        private static UnifiedCommitmentState Promise(EpisodeState s, string id) => new UnifiedCommitmentState {
            id = id, kind = UnifiedCommitments.Safety, sourcePolicy = UnifiedCommitments.PromisePolicy,
            origin = UnifiedCommitments.StoryPromise, makerId = s.hohId, beneficiaryId = s.playerId,
            createdWeek = s.week, expiresWeek = s.week + 1, status = DealStatus.Active, trustImpact = DealTrust.Medium,
        };
        private static UnifiedCommitmentState Deal(EpisodeState s, string id) => new UnifiedCommitmentState {
            id = id, kind = UnifiedCommitments.Safety, sourcePolicy = UnifiedCommitments.DealPolicy,
            origin = UnifiedCommitments.StoryDeal, makerId = s.hohId, beneficiaryId = s.playerId, reciprocal = true,
            createdWeek = s.week, expiresWeek = s.week, status = DealStatus.Active, trustImpact = DealTrust.Medium,
        };
        private static string Other(EpisodeState s) => s.contestants.First(c => c.id != s.playerId && c.id != s.hohId).id;
        private static void Hold(EpisodeState s, string approach) => RelationshipLedger.RecordOneWay(s, s.hohId, s.playerId,
            Negotiation.HeldType(PromiseKind.Safety, approach), 0, "Called in.");
        private static void AddOtherTerms(EpisodeState s)
        {
            s.deals.Add(new DealState { id = "final-two", type = DealKind.FinalTwo, proposerId = s.hohId, recipientId = s.playerId, status = DealStatus.Active });
            s.deals.Add(new DealState { id = "partnership", type = DealKind.Partnership, proposerId = s.hohId, recipientId = s.playerId, status = DealStatus.Active });
            s.deals.Add(new DealState { id = "broken-information", type = DealKind.InformationSharing, proposerId = s.hohId,
                recipientId = s.playerId, status = DealStatus.Broken, brokenById = s.playerId, settledWeek = s.week });
            s.deals.Add(new DealState { id = "target", type = DealKind.TargetAgreement, proposerId = s.hohId,
                recipientId = Other(s), targetId = s.playerId, status = DealStatus.Active });
            s.lobbies.Add(new LobbyState { week = s.week, phase = s.phase, deciderId = s.hohId, subjectId = s.playerId, ask = LobbyAsk.Spare, influence = 17 });
            s.lobbies.Add(new LobbyState { week = s.week, phase = s.phase, deciderId = s.hohId, subjectId = s.playerId, ask = LobbyAsk.Target, influence = 4 });
        }
        private static void AssertRead(EpisodeState s, double expected)
        {
            Assert.That(UnifiedCommitments.ValidateRecords(s, out string error), Is.True, error);
            string before = Json(s);
            Assert.That(StrategyRules.NominationReluctance(s, s.hohId, s.playerId), Is.EqualTo(expected).Within(1e-9));
            Assert.That(Json(s), Is.EqualTo(before), "Protection reads cannot change RNG, history, counters or either authority.");
        }
        private static string Json(EpisodeState s) => JsonConvert.SerializeObject(s.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToDictionary(field => field.Name, field => field.GetValue(s), StringComparer.Ordinal));
    }
}
