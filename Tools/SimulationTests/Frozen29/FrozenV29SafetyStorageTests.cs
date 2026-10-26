// Frozen29 (SCH30): carried from Tools/SimulationTests/Frozen28/FrozenV28SafetyStorageTests.cs by name, then to schema 29
// where a case builds a whole schema-29 state or payload.
// Authored, unexecuted component specifications. Detached fixtures are not whole saves or genuine history.
using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Persistence.Frozen29Data;
using Gamesim.Persistence.Frozen29Semantics;
using NUnit.Framework;

namespace Gamesim.Persistence.Frozen29Tests
{
    [TestFixture]
    public sealed class FrozenV29SafetyStorageTests
    {
        private const string Missing = "Missing canonical commitment list.";
        private const string Disabled = "Disabled commitment authority must remain empty.";
        private const string Storage = "Invalid prospective commitment storage.";
        private const string Mirror = "Legacy safety rows must not mirror the prospective canonical authority.";
        private const string Collision = "Canonical identity collides with another commitment.";
        private const string Capacity = "Commitment family capacity exceeded.";
        private const string Identity = "Invalid canonical safety identity.";
        private const string Policy = "Invalid canonical safety source policy.";
        private const string Term = "Invalid canonical safety term.";
        private const string Unilateral = "A safety promise is unilateral, not a pending bargain or a spared-deal receipt.";
        private const string Parties = "The safety origin does not match its parties.";
        private const string Link = "Invalid safety consideration reference.";
        private const string Settlement = "Invalid canonical safety settlement week.";
        private const string Breach = "Invalid safety breach attribution.";
        private const string OnlyBreach = "Only a breach may carry a betrayal effect identity.";

        [TestCase(-1, false, false)]
        [TestCase(0, false, false)]
        [TestCase(1, true, true)]
        [TestCase(2, false, true)]
        [TestCase(3, false, false)]
        [TestCase(int.MinValue, false, false)]
        [TestCase(int.MaxValue, false, false)]
        public void ModePredicates(int mode, bool records, bool safety)
        {
            var state = State(mode);
            Assert.That(FrozenV29SafetyStorage.RulesOn(state), Is.EqualTo(records));
            Assert.That(FrozenV29SafetyStorage.SafetyAuthorityOn(state), Is.EqualTo(safety));
            Assert.That(FrozenV29SafetyStorage.RulesOn(null), Is.False);
            Assert.That(FrozenV29SafetyStorage.SafetyAuthorityOn(null), Is.False);
        }

        [Test]
        public void NullAndDisabledRootsKeepTheirDifferentContracts()
        {
            Check(null, false, Missing, false);
            Check(null, false, Storage, true);
            var state = new EpisodeState { unifiedCommitmentRulesVersion = 0, unifiedCommitments = new List<UnifiedCommitmentState>() };
            Check(state, true, null, false); // Missing cast/raw lists are deliberately not inspected in mode0.
            Check(state, false, Storage, true);
            state.unifiedCommitments.Add(null);
            Check(state, false, Disabled, false);
            state.unifiedCommitments = null;
            Check(state, false, Missing, false);
        }

        [TestCase(-1)] [TestCase(2)] [TestCase(3)] [TestCase(int.MaxValue)]
        public void GeneralRecordsRefuseModesOtherThanZeroAndOne(int mode)
        {
            var state = State(mode);
            state.unifiedCommitments.Add(Promise());
            Check(state, false, Storage, false);
        }

        [TestCase(1)] [TestCase(2)]
        public void EmptyStorageAndOrdinarySafetyRowsAreAcceptedOnlyByTheirLayer(int mode)
        {
            var state = State(mode);
            Check(state, true, null, mode == 2);
            state.unifiedCommitments.Add(Promise());
            state.unifiedCommitments.Add(Deal());
            Check(state, true, null, mode == 2);
        }

        [TestCase("player-promise", "promise", false, "p", "a", 4, null)]
        [TestCase("npc-promise", "promise", false, "a", "b", 4, null)]
        [TestCase("story-promise", "promise", false, "a", "p", 4, null)]
        [TestCase("hoh-pitch", "promise", false, "p", "a", 4, null)]
        [TestCase("player-deal", "deal", true, "p", "a", 3, null)]
        [TestCase("npc-deal", "deal", true, "a", "b", 3, null)]
        [TestCase("npc-offer", "deal", true, "a", "p", 3, null)]
        [TestCase("lobby", "deal", true, "p", "a", 4, null)]
        [TestCase("story-deal", "deal", true, "a", "p", 3, null)]
        [TestCase("counter-deal", "deal", true, "p", "a", 3, "peer-not-resolved-here")]
        [TestCase("counter-price", "deal", true, "p", "a", 3, "peer-not-resolved-here")]
        public void EveryLiteralOriginHasItsHistoricalDirectionAndTerm(string origin, string policy, bool reciprocal,
            string maker, string beneficiary, int expiry, string linked)
        {
            var state = State(1);
            var row = Promise();
            row.origin = origin; row.sourcePolicy = policy; row.reciprocal = reciprocal;
            row.makerId = maker; row.beneficiaryId = beneficiary; row.expiresWeek = expiry; row.linkedCommitmentId = linked;
            state.unifiedCommitments.Add(row);
            Check(state, true, null, false);
        }

        [TestCase("proposed", false)] [TestCase("accepted", false)] [TestCase("active", true)]
        [TestCase("fulfilled", false)] [TestCase("broken", true)] [TestCase("declined", false)] [TestCase("expired", true)]
        public void PromiseStatusPolicy(string status, bool accepted)
        {
            var state = State(1); var row = Promise(); row.status = status;
            SetSettlement(row, status); state.unifiedCommitments.Add(row);
            Check(state, accepted, accepted ? null : Unilateral, false);
        }

        [TestCase("proposed")] [TestCase("accepted")] [TestCase("active")]
        [TestCase("fulfilled")] [TestCase("broken")] [TestCase("declined")] [TestCase("expired")]
        public void AllSevenKnownDealStatuses(string status)
        {
            var state = State(1); var row = Deal(); row.status = status;
            SetSettlement(row, status); state.unifiedCommitments.Add(row);
            Check(state, true, null, false);
        }

        [TestCase("low", true)] [TestCase("medium", true)] [TestCase("high", true)] [TestCase("critical", true)]
        [TestCase("Medium", false)] [TestCase("", false)] [TestCase(null, false)]
        public void LiteralDealTrustVocabulary(string trust, bool accepted)
        {
            var state = State(1); var row = Deal(); row.trustImpact = trust; state.unifiedCommitments.Add(row);
            Check(state, accepted, accepted ? null : Policy, false);
        }

        [TestCase("low")] [TestCase("high")] [TestCase("critical")]
        public void KnownNonMediumPromiseTrustStillRefuses(string trust)
        {
            var state = State(1); var row = Promise(); row.trustImpact = trust; state.unifiedCommitments.Add(row);
            Check(state, false, Unilateral, false);
        }

        private static IEnumerable<TestCaseData> IdCases()
        {
            yield return new TestCaseData(null, false).SetName("RowIdNull");
            yield return new TestCaseData("", false).SetName("RowIdEmpty");
            yield return new TestCaseData(" \t ", false).SetName("RowIdWhitespace");
            yield return new TestCaseData("row\n1", false).SetName("RowIdControl");
            yield return new TestCaseData(new string('x', 160), true).SetName("RowId160");
            yield return new TestCaseData(new string('x', 161), false).SetName("RowId161");
            yield return new TestCaseData(" leading:and trailing ", true).SetName("RowIdUntrimmed");
            yield return new TestCaseData("row\u200b1", true).SetName("RowIdFormatCharacter");
        }

        [TestCaseSource(nameof(IdCases))]
        public void HistoricalTokenIsNotTrimmedOrGeneralUnicodeSanitized(string id, bool accepted)
        {
            var state = State(1); var row = Promise(); row.id = id; state.unifiedCommitments.Add(row);
            Check(state, accepted, accepted ? null : Identity, false);
        }

        [TestCase(1, 1, true)] [TestCase(100, 100, true)] [TestCase(0, 0, false)] [TestCase(101, 101, false)]
        public void WeekBounds(int week, int created, bool accepted)
        {
            var state = State(1); state.week = week; var row = Promise();
            row.createdWeek = created; row.expiresWeek = created + 1; state.unifiedCommitments.Add(row);
            Check(state, accepted, accepted ? null : Storage, false);
        }

        [TestCase(2, false)] [TestCase(3, true)] [TestCase(16, true)] [TestCase(17, false)]
        public void CastBounds(int count, bool accepted)
        {
            var state = State(1);
            state.contestants = new List<ContestantState>();
            for (int i = 0; i < count; i++) state.contestants.Add(Person(i == 0 ? "p" : "guest" + i));
            Check(state, accepted, accepted ? null : Storage, false);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void EveryIdentityContextGuard(int variant)
        {
            var state = State(1);
            if (variant == 0) state.contestants = null;
            if (variant == 1) state.contestants[0] = null;
            if (variant == 2) state.contestants[1].id = "p";
            if (variant == 3) state.contestants[1].nominationWeeks = null;
            if (variant == 4) state.playerId = "missing";
            if (variant == 5) state.contestants[1].id = "bad\0id";
            Check(state, false, Storage, false);
        }

        [Test]
        public void CastIdentityDoesNotRequireActiveStatusOrNominationContents()
        {
            var state = State(1);
            state.contestants[0].status = ContestantStatus.Expelled;
            state.contestants[1].status = ContestantStatus.Winner;
            state.contestants[1].nominationWeeks.Add(-100);
            state.unifiedCommitments.Add(Promise());
            Check(state, true, null, false);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void CanonicalIdentityFieldsRefuse(int variant)
        {
            var state = State(1); var row = Promise();
            if (variant == 0) row = null;
            if (variant == 1) row.kind = "vote";
            if (variant == 2) row.targetId = "a";
            if (variant == 3) row.subtype = "safety";
            if (variant == 4) row.voteBindingWeek = 3;
            if (variant == 5) row.voteFirstRevealWeek = 3;
            if (variant == 6) row.beneficiaryId = "p";
            if (variant == 7) row.makerId = "missing";
            state.unifiedCommitments.Add(row); Check(state, false, Identity, false);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void PolicyGuardFieldsRefuse(int variant)
        {
            var state = State(1); var row = Promise();
            if (variant == 0) row.sourcePolicy = "unknown";
            if (variant == 1) row.origin = "player-deal";
            if (variant == 2) row.reciprocal = true;
            if (variant == 3) row.status = "ACTIVE";
            if (variant == 4) row.trustImpact = null;
            state.unifiedCommitments.Add(row); Check(state, false, Policy, false);
        }

        [TestCase(0, 1)] [TestCase(4, 5)] [TestCase(3, 3)] [TestCase(int.MaxValue, int.MinValue)]
        public void CreatedWeekAndTermRefuseWithoutRepair(int created, int expires)
        {
            var state = State(1); var row = Promise(); row.createdWeek = created; row.expiresWeek = expires;
            state.unifiedCommitments.Add(row); Check(state, false, Term, false);
        }

        [TestCase("proposed", 2, true)] [TestCase("declined", 2, true)]
        [TestCase("active", 2, true)] [TestCase("accepted", 3, true)]
        [TestCase("active", 1, false)] [TestCase("active", 4, false)]
        [TestCase("proposed", 3, false)] [TestCase("declined", 3, false)]
        public void AnsweredNpcOfferHasItsSeparateTermRule(string status, int expires, bool accepted)
        {
            var state = State(1); var row = Deal(); row.origin = "npc-offer";
            row.makerId = "a"; row.beneficiaryId = "p"; row.createdWeek = 2; row.expiresWeek = expires; row.status = status;
            state.unifiedCommitments.Add(row); Check(state, accepted, accepted ? null : Term, false);
        }

        [TestCase("player-promise", "promise", false)] [TestCase("hoh-pitch", "promise", false)]
        [TestCase("npc-promise", "promise", false)] [TestCase("player-deal", "deal", true)]
        [TestCase("npc-deal", "deal", true)] [TestCase("npc-offer", "deal", true)]
        [TestCase("lobby", "deal", true)] [TestCase("counter-deal", "deal", true)]
        [TestCase("counter-price", "deal", true)]
        public void OriginDirectionErrorsRefuse(string origin, string policy, bool reciprocal)
        {
            var state = State(1); var row = Promise(); row.origin = origin; row.sourcePolicy = policy; row.reciprocal = reciprocal;
            row.makerId = "b"; row.beneficiaryId = "a";
            if (origin == "npc-promise" || origin == "npc-deal") row.makerId = "p";
            row.expiresWeek = policy == "promise" || origin == "lobby" ? 4 : 3;
            if (origin == "counter-deal" || origin == "counter-price") row.linkedCommitmentId = "peer";
            state.unifiedCommitments.Add(row); Check(state, false, Parties, false);
        }

        [TestCase("player-deal", "peer", false)] [TestCase("counter-deal", null, false)]
        [TestCase("counter-price", "deal-1", false)] [TestCase("counter-deal", "bad\npeer", false)]
        [TestCase("counter-deal", "absent-peer", true)]
        public void LinkShapeDoesNotResolveTheOtherRecord(string origin, string linked, bool accepted)
        {
            var state = State(1); var row = Deal(); row.origin = origin; row.linkedCommitmentId = linked;
            state.unifiedCommitments.Add(row); Check(state, accepted, accepted ? null : Link, false);
        }

        [TestCase("active", 1)] [TestCase("expired", 3)] [TestCase("broken", 2)]
        [TestCase("broken", 4)] [TestCase("fulfilled", 0)]
        public void SettlementWeekIsCheckedBeforeBreachIdentity(string status, int settled)
        {
            var state = State(1); var row = Deal(); row.status = status; row.settledWeek = settled;
            row.brokenById = "wrong"; row.settlementEffectKey = "wrong";
            state.unifiedCommitments.Add(row); Check(state, false, Settlement, false);
        }

        [TestCase(false, "p", true)] [TestCase(false, "a", false)]
        [TestCase(true, "p", true)] [TestCase(true, "a", true)] [TestCase(true, "b", false)]
        public void BreachAttributionDependsOnFamily(bool deal, string breaker, bool accepted)
        {
            var state = State(1); var row = deal ? Deal() : Promise();
            row.status = "broken"; row.settledWeek = 3; row.brokenById = breaker; row.settlementEffectKey = "safety:";
            state.unifiedCommitments.Add(row); Check(state, accepted, accepted ? null : Breach, false);
        }

        private static IEnumerable<TestCaseData> EffectCases()
        {
            yield return new TestCaseData(null, false).SetName("EffectNull");
            yield return new TestCaseData("Safety:x", false).SetName("EffectWrongCase");
            yield return new TestCaseData("safety:", true).SetName("EffectPrefixOnly");
            yield return new TestCaseData("safety:" + new string('x', 633), true).SetName("Effect640");
            yield return new TestCaseData("safety:" + new string('x', 634), false).SetName("Effect641");
            yield return new TestCaseData("safety:bad\rkey", false).SetName("EffectControl");
        }

        [TestCaseSource(nameof(EffectCases))]
        public void EffectIdentityChecksOnlyHistoricalPrefixLengthAndControls(string effect, bool accepted)
        {
            var state = State(1); var row = Deal(); row.status = "broken"; row.settledWeek = 3;
            row.brokenById = "p"; row.settlementEffectKey = effect; state.unifiedCommitments.Add(row);
            Check(state, accepted, accepted ? null : Breach, false);
        }

        [TestCase(true)] [TestCase(false)]
        public void NonBrokenRowsCannotCarryEitherBreachField(bool breaker)
        {
            var state = State(1); var row = Deal();
            if (breaker) row.brokenById = "p"; else row.settlementEffectKey = "safety:x";
            state.unifiedCommitments.Add(row); Check(state, false, OnlyBreach, false);
        }

        [TestCase(1)] [TestCase(2)]
        public void RawSafetyMirrorsPrecedeCanonicalPolicy(int mode)
        {
            var state = State(mode); state.promises.Add(new PromiseState { id = "raw", kind = PromiseKind.Safety });
            state.unifiedCommitments.Add(Promise()); state.unifiedCommitments[0].origin = "bad";
            Check(state, false, Mirror, mode == 2);
            state.promises.Clear(); state.deals.Add(new DealState { id = "raw", type = "safety_agreement" });
            Check(state, false, Mirror, mode == 2);
        }

        [TestCase(1)] [TestCase(2)]
        public void NullRawRowsAndListsRefuseBeforeMirrorQueries(int mode)
        {
            var state = State(mode); state.promises.Add(null); Check(state, false, Storage, mode == 2);
            state.promises.Clear(); state.deals.Add(null); Check(state, false, Storage, mode == 2);
            state.deals = null; Check(state, false, Storage, mode == 2);
            state.deals = new List<DealState>(); state.promises = null; Check(state, false, Storage, mode == 2);
        }

        [TestCase(null)] [TestCase("bad\nraw")] [TestCase("duplicate")]
        public void GeneralModeRawIdsAreLaxButModeTwoIdsAreStrict(string id)
        {
            var state = State(1);
            state.promises.Add(new PromiseState { id = id, kind = PromiseKind.Vote });
            state.deals.Add(new DealState { id = id, type = "partnership" });
            Check(state, true, null, false);
            state.unifiedCommitmentRulesVersion = 2; Check(state, false, Collision, true);
        }

        [Test]
        public void ModeOneRowPolicyPrecedesCollisionButModeTwoAllIdsPrecedePolicy()
        {
            var state = State(1); var row = Promise(); row.status = "bad";
            state.promises.Add(new PromiseState { id = row.id, kind = PromiseKind.Vote });
            state.unifiedCommitments.Add(row); Check(state, false, Policy, false);
            state.unifiedCommitmentRulesVersion = 2; Check(state, false, Collision, true);
        }

        [Test]
        public void StoredCanonicalOrderDeterminesFirstPolicyRefusal()
        {
            var state = State(1); var first = Promise(); first.origin = "bad";
            var second = Deal(); second.expiresWeek = -1;
            state.unifiedCommitments.Add(first); state.unifiedCommitments.Add(second);
            Check(state, false, Policy, false);
            state.unifiedCommitments.Reverse(); Check(state, false, Term, false);
        }

        [Test]
        public void ModeTwoLeavesNonSafetyPolicyToItsOwnerButCountsItAsStored()
        {
            var state = State(2);
            state.unifiedCommitments.Add(new UnifiedCommitmentState { id = "other-1", kind = "unknown", sourcePolicy = "unknown" });
            Check(state, true, null, true);
            state.unifiedCommitmentRulesVersion = 1; Check(state, false, Identity, false);
        }

        [TestCase(1, 200, false, true)] [TestCase(1, 201, false, false)]
        [TestCase(2, 200, false, true)] [TestCase(2, 201, false, false)]
        [TestCase(1, 200, true, true)] [TestCase(1, 201, true, false)]
        [TestCase(2, 200, true, true)] [TestCase(2, 201, true, false)]
        public void PerFamilyCapacityCountsRawAndCanonical(int mode, int total, bool deal, bool accepted)
        {
            var state = State(mode); state.unifiedCommitments.Add(deal ? Deal() : Promise());
            for (int i = 1; i < total; i++)
            {
                if (deal) state.deals.Add(new DealState { id = "raw-" + i, type = "partnership" });
                else state.promises.Add(new PromiseState { id = "raw-" + i, kind = PromiseKind.Vote });
            }
            Check(state, accepted, accepted ? null : Capacity, mode == 2);
        }

        [TestCase(1)] [TestCase(2)]
        public void IdentifierEqualityIsOrdinalAcrossStores(int mode)
        {
            var state = State(mode); state.unifiedCommitments.Add(Promise());
            state.promises.Add(new PromiseState { id = "PROMISE-1", kind = PromiseKind.Vote });
            Check(state, true, null, mode == 2);
            state.promises[0].id = "promise-1";
            Check(state, false, Collision, mode == 2);
        }

        [Test]
        public void FourHundredCanonicalRowsCanSatisfyBothFamilyLimits()
        {
            var state = State(2);
            for (int i = 0; i < 200; i++)
            {
                var promise = Promise(); promise.id = "promise-" + i;
                var deal = Deal(); deal.id = "deal-" + i;
                state.unifiedCommitments.Add(promise); state.unifiedCommitments.Add(deal);
            }
            Check(state, true, null, true);
            state.unifiedCommitments.Add(new UnifiedCommitmentState { id = "other", kind = "unknown" });
            Check(state, false, Storage, true);
        }

        [TestCase(400, true)] [TestCase(401, false)]
        public void AggregateCountGuardPrecedesRowAndIdChecks(int count, bool atBound)
        {
            var state = State(1);
            for (int i = 0; i < count; i++) state.unifiedCommitments.Add(null);
            Check(state, false, atBound ? Identity : Storage, false);
        }

        [Test]
        public void ModeOneSafetyAuthorityDelegatesExactGeneralRefusal()
        {
            var state = State(1); state.unifiedCommitments.Add(null);
            Check(state, false, Identity, true);
            state.unifiedCommitments = null; Check(state, false, Missing, true);
        }

        [TestCase(0)] [TestCase(3)]
        public void RowEntryRefusesDisabledModesBeforeNullContext(int mode)
        {
            var state = new EpisodeState { unifiedCommitmentRulesVersion = mode };
            string error;
            Assert.That(FrozenV29SafetyStorage.ValidateRow(state, null, out error), Is.False);
            Assert.That(error, Is.EqualTo("Unified commitments are not enabled."));
        }

        private static EpisodeState State(int mode) => new EpisodeState
        {
            week = 3, playerId = "p", unifiedCommitmentRulesVersion = mode,
            contestants = new List<ContestantState> { Person("p"), Person("a"), Person("b") },
            promises = new List<PromiseState>(), deals = new List<DealState>(), unifiedCommitments = new List<UnifiedCommitmentState>()
        };

        private static ContestantState Person(string id) => new ContestantState { id = id, nominationWeeks = new List<int>() };

        private static UnifiedCommitmentState Promise() => new UnifiedCommitmentState
        {
            id = "promise-1", kind = "safety", sourcePolicy = "promise", origin = "player-promise",
            makerId = "p", beneficiaryId = "a", reciprocal = false, createdWeek = 3, expiresWeek = 4,
            status = "active", settledWeek = 0, brokenById = null, trustImpact = "medium", linkedCommitmentId = null,
            settlementEffectKey = null, targetId = null, subtype = null, voteBindingWeek = 0, voteFirstRevealWeek = 0
        };

        private static UnifiedCommitmentState Deal() => new UnifiedCommitmentState
        {
            id = "deal-1", kind = "safety", sourcePolicy = "deal", origin = "player-deal",
            makerId = "p", beneficiaryId = "a", reciprocal = true, createdWeek = 3, expiresWeek = 3,
            status = "active", settledWeek = 0, brokenById = null, trustImpact = "medium", linkedCommitmentId = null,
            settlementEffectKey = null, targetId = null, subtype = null, voteBindingWeek = 0, voteFirstRevealWeek = 0
        };

        private static void SetSettlement(UnifiedCommitmentState row, string status)
        {
            if (status == "fulfilled" || status == "broken") row.settledWeek = 3;
            if (status == "broken") { row.brokenById = "p"; row.settlementEffectKey = "safety:literal"; }
        }

        // Every root assertion captures the complete 32-field read surface, list identity and row order.
        // Values are literals; expected acceptance/error never comes from a live/current validator or factory.
        private static void Check(EpisodeState state, bool expected, string reason, bool safety)
        {
            object[] before = Surface(state);
            try
            {
                string error = "sentinel";
                bool actual = safety ? FrozenV29SafetyStorage.ValidateSafetyAuthority(state, out error)
                    : FrozenV29SafetyStorage.ValidateRecords(state, out error);
                Assert.That(actual, Is.EqualTo(expected));
                Assert.That(error, Is.EqualTo(reason));
            }
            finally
            {
                object[] after = Surface(state);
                Assert.That(after.Length, Is.EqualTo(before.Length));
                for (int i = 0; i < before.Length; i++)
                {
                    object value = before[i];
                    if (value != null && !(value is string) && !value.GetType().IsValueType)
                        Assert.That(ReferenceEquals(value, after[i]), Is.True, "Object/list identity changed at surface slot " + i);
                    else Assert.That(after[i], Is.EqualTo(value), "Scalar changed at surface slot " + i);
                }
            }
        }

        private static object[] Surface(EpisodeState state)
        {
            var values = new List<object> { state };
            if (state == null) return values.ToArray();
            values.AddRange(new object[] { state.week, state.playerId, state.unifiedCommitmentRulesVersion,
                state.contestants, state.promises, state.deals, state.unifiedCommitments });
            if (state.contestants != null) foreach (var person in state.contestants)
            {
                values.Add(person);
                if (person == null) continue;
                values.Add(person.id); values.Add(person.nominationWeeks);
                if (person.nominationWeeks != null) foreach (int week in person.nominationWeeks) values.Add(week);
            }
            if (state.promises != null) foreach (var row in state.promises)
            {
                values.Add(row); if (row != null) values.AddRange(new object[] { row.id, row.kind });
            }
            if (state.deals != null) foreach (var row in state.deals)
            {
                values.Add(row); if (row != null) values.AddRange(new object[] { row.id, row.type });
            }
            if (state.unifiedCommitments != null) foreach (var row in state.unifiedCommitments)
            {
                values.Add(row); if (row == null) continue;
                values.AddRange(new object[] { row.id, row.kind, row.sourcePolicy, row.origin, row.makerId, row.beneficiaryId,
                    row.reciprocal, row.createdWeek, row.expiresWeek, row.status, row.settledWeek, row.brokenById, row.trustImpact,
                    row.linkedCommitmentId, row.settlementEffectKey, row.targetId, row.subtype, row.voteBindingWeek, row.voteFirstRevealWeek });
            }
            return values.ToArray();
        }
    }
}
