// Frozen29 (SCH30): carried from Tools/SimulationTests/Frozen28/FrozenV28ReferenceTests.cs by name, then to schema 29
// where a case builds a whole schema-29 state or payload.
// Unexecuted literal component specifications. Fixtures are not whole saves or genuine histories.
using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Persistence.Frozen29Data;
using Gamesim.Persistence.Frozen29Semantics;
using NUnit.Framework;

namespace Gamesim.Persistence.Frozen29Tests
{
    [TestFixture]
    public sealed class FrozenV29ReferenceTests
    {
        [Test]
        public void HistoricalFreshRowDefaultsAreExplicitAndNotHydrationRepair()
        {
            var promise = FrozenV29ReferenceRows.NewPromise();
            CollectionAssert.AreEqual(new object[] { null, null, null, null, PromiseKind.Safety, PromiseStatus.Active, 0, 0, "medium", null, 0 }, Values(promise));
            var deal = FrozenV29ReferenceRows.NewDeal();
            CollectionAssert.AreEqual(new object[] { null, "partnership", null, null, null, "proposed", 0, 0, "medium", null, 0, null }, Values(deal));
            Assert.That(new PromiseState().impact, Is.Null);
            Assert.That(new DealState().type, Is.Null);
            Assert.That(ReferenceEquals(promise, FrozenV29ReferenceRows.NewPromise()), Is.False);
        }

        [Test]
        public void PromiseCloneCopiesEveryScalarWithoutApplyingDefaults()
        {
            var row = RawPromise("clone-word"); row.fromId = "maker"; row.toId = "beneficiary"; row.targetId = "target";
            row.kind = (PromiseKind)(-17); row.status = (PromiseStatus)91; row.week = int.MinValue; row.expiresWeek = int.MaxValue;
            row.impact = null; row.brokenById = "breaker"; row.settledWeek = -2;
            var copy = FrozenV29ReferenceRows.Clone(row);
            Assert.That(ReferenceEquals(row, copy), Is.False); CollectionAssert.AreEqual(Values(row), Values(copy));
            Assert.That(ReferenceEquals(row.id, copy.id), Is.True);
            copy.fromId = "other"; Assert.That(row.fromId, Is.EqualTo("maker"));
        }

        [Test]
        public void DealCloneCopiesEveryScalarWithoutApplyingDefaults()
        {
            var row = RawDeal("clone-pact"); row.type = null; row.proposerId = "maker"; row.recipientId = "beneficiary";
            row.targetId = "target"; row.status = null; row.week = -3; row.expiresWeek = int.MaxValue;
            row.trustImpact = null; row.brokenById = "breaker"; row.settledWeek = -4; row.linkedDealId = "peer";
            var copy = FrozenV29ReferenceRows.Clone(row);
            Assert.That(ReferenceEquals(row, copy), Is.False); CollectionAssert.AreEqual(Values(row), Values(copy));
            copy.linkedDealId = "different"; Assert.That(row.linkedDealId, Is.EqualTo("peer"));
        }

        [Test]
        public void CanonicalCloneCopiesAllNineteenScalars()
        {
            var row = Canonical("canonical", false, false); row.origin = null; row.makerId = "maker"; row.beneficiaryId = "beneficiary";
            row.reciprocal = true; row.createdWeek = int.MinValue; row.expiresWeek = int.MaxValue; row.status = null;
            row.settledWeek = -6; row.brokenById = "breaker"; row.trustImpact = null; row.linkedCommitmentId = "link";
            row.settlementEffectKey = "effect"; row.targetId = "target"; row.subtype = "subtype"; row.voteBindingWeek = -7; row.voteFirstRevealWeek = 8;
            var copy = FrozenV29ReferenceRows.Clone(row);
            Assert.That(ReferenceEquals(row, copy), Is.False); CollectionAssert.AreEqual(Values(row), Values(copy));
            copy.targetId = "different"; Assert.That(row.targetId, Is.EqualTo("target"));
        }

        [Test]
        public void NullConditionalCloneHelpersDoNotConstructRows()
        {
            Assert.That(FrozenV29ReferenceRows.CloneOrNull((PromiseState)null), Is.Null);
            Assert.That(FrozenV29ReferenceRows.CloneOrNull((DealState)null), Is.Null);
            Assert.That(FrozenV29ReferenceRows.CloneOrNull((UnifiedCommitmentState)null), Is.Null);
            Assert.Throws<NullReferenceException>(() => FrozenV29ReferenceRows.Clone((PromiseState)null));
            Assert.Throws<NullReferenceException>(() => FrozenV29ReferenceRows.Clone((DealState)null));
            Assert.Throws<NullReferenceException>(() => FrozenV29ReferenceRows.Clone((UnifiedCommitmentState)null));
        }

        [Test]
        public void NullIdLookupOrderDiffersFromCanonicalLookup()
        {
            Assert.That(FrozenV29CommitmentReferences.FindPromise(null, null), Is.Null);
            Assert.That(FrozenV29CommitmentReferences.FindDeal(null, null), Is.Null);
            Assert.Throws<ArgumentNullException>(() => FrozenV29CommitmentReferences.FindPromise(null, "id"));
            Assert.Throws<ArgumentNullException>(() => FrozenV29CommitmentReferences.FindCanonical(null, null));
            Assert.Throws<ArgumentNullException>(() => FrozenV29CommitmentReferences.FindCanonicalSafety(null, null));
        }

        [TestCase(-1)] [TestCase(3)] [TestCase(int.MinValue)] [TestCase(int.MaxValue)]
        public void UnknownModeRawAliasesBypassButDetachedViewsRefuse(int mode)
        {
            var state = State(mode); state.promises.Add(RawPromise("raw")); state.deals.Add(RawDeal("raw-deal"));
            ReadUnchanged(state, () =>
            {
                Assert.That(ReferenceEquals(FrozenV29CommitmentReferences.RawPromises(state), state.promises), Is.True);
                Assert.That(ReferenceEquals(FrozenV29CommitmentReferences.RawDeals(state), state.deals), Is.True);
                Assert.That(FrozenV29CommitmentReferences.FindPromise(state, null), Is.Null);
                Marked(() => FrozenV29CommitmentReferences.Promises(state), "Invalid prospective commitment storage.");
                Marked(() => FrozenV29CommitmentReferences.Deals(state), "Invalid prospective commitment storage.");
                Marked(() => FrozenV29CommitmentReferences.FindCanonical(state, null), "Invalid prospective commitment storage.");
            });
        }

        [Test]
        public void LegacyZeroBypassesCanonicalAndCastChecksButCopiesRawNulls()
        {
            var state = new EpisodeState { unifiedCommitmentRulesVersion = 0,
                promises = new List<PromiseState> { RawPromise("a"), null, RawPromise("b") },
                deals = new List<DealState> { null, RawDeal("d") }, unifiedCommitments = new List<UnifiedCommitmentState> { null } };
            ReadUnchanged(state, () =>
            {
                var words = FrozenV29CommitmentReferences.Promises(state);
                CollectionAssert.AreEqual(new[] { "a", null, "b" }, words.Select(row => row?.id));
                Assert.That(ReferenceEquals(words[0], state.promises[0]), Is.False);
                Assert.That(FrozenV29CommitmentReferences.Deals(state)[0], Is.Null);
                Assert.That(FrozenV29CommitmentReferences.FindCanonical(state, "a"), Is.Null);
                Assert.That(FrozenV29CommitmentReferences.PromiseCount(state), Is.EqualTo(3));
                Assert.That(FrozenV29CommitmentReferences.DealCount(state), Is.EqualTo(2));
                Assert.That(FrozenV29CommitmentReferences.ReceiptWeek(state, "a", -88), Is.EqualTo(-88));
            });
        }

        [Test]
        public void LegacyRawNullListReturnsNullWhileDetachedEnumerationThrows()
        {
            var state = new EpisodeState { unifiedCommitmentRulesVersion = 0 };
            Assert.That(FrozenV29CommitmentReferences.RawPromises(state), Is.Null);
            Assert.That(FrozenV29CommitmentReferences.RawDeals(state), Is.Null);
            ReadUnchanged(state, () =>
            {
                Assert.Throws<ArgumentNullException>(() => FrozenV29CommitmentReferences.Promises(state));
                Assert.Throws<ArgumentNullException>(() => FrozenV29CommitmentReferences.Deals(state));
                Assert.Throws<NullReferenceException>(() => FrozenV29CommitmentReferences.PromiseCount(state));
            });
        }

        [TestCase(0)] [TestCase(1)]
        public void RawAliasesRetainOriginalListAndRows(int mode)
        {
            var state = State(mode); var row = RawPromise("raw"); state.promises.Add(row);
            var deal = RawDeal("raw-deal"); state.deals.Add(deal);
            var result = FrozenV29CommitmentReferences.RawPromises(state);
            Assert.That(ReferenceEquals(result, state.promises), Is.True);
            Assert.That(ReferenceEquals(result[0], row), Is.True);
            var deals = FrozenV29CommitmentReferences.RawDeals(state);
            Assert.That(ReferenceEquals(deals, state.deals), Is.True);
            Assert.That(ReferenceEquals(deals[0], deal), Is.True);
        }

        [Test]
        public void LegacyDuplicateIdFindUsesFirstStoredRowAndReturnsCopy()
        {
            var state = State(0); var first = RawPromise("duplicate"); first.week = 11;
            var second = RawPromise("duplicate"); second.week = 22;
            state.promises.Add(null); state.promises.Add(first); state.promises.Add(second);
            var firstDeal = RawDeal("duplicate-deal"); firstDeal.week = 31;
            var secondDeal = RawDeal("duplicate-deal"); secondDeal.week = 32;
            state.deals.Add(null); state.deals.Add(firstDeal); state.deals.Add(secondDeal);
            ReadUnchanged(state, () =>
            {
                var found = FrozenV29CommitmentReferences.FindPromise(state, "duplicate");
                Assert.That(found.week, Is.EqualTo(11)); Assert.That(ReferenceEquals(first, found), Is.False);
                Assert.That(FrozenV29CommitmentReferences.FindPromise(state, "DUPLICATE"), Is.Null);
                var foundDeal = FrozenV29CommitmentReferences.FindDeal(state, "duplicate-deal");
                Assert.That(foundDeal.week, Is.EqualTo(31)); Assert.That(ReferenceEquals(firstDeal, foundDeal), Is.False);
            });
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void DetachedViewsAndCountsKeepAllPoliciesInTheirMode(int mode)
        {
            var state = State(mode); state.promises.Add(RawPromise("raw-4")); state.deals.Add(RawDeal("raw-deal-4"));
            if (mode != 0)
            {
                state.unifiedCommitments.Add(Canonical("safety-word-1", false, false));
                state.unifiedCommitments.Add(Canonical("safety-deal-1", true, false));
            }
            if (mode == 2)
            {
                state.unifiedCommitments.Add(Canonical("vote-word-2", false, true));
                state.unifiedCommitments.Add(Canonical("vote-deal-2", true, true));
            }
            ReadUnchanged(state, () =>
            {
                var words = FrozenV29CommitmentReferences.Promises(state); var deals = FrozenV29CommitmentReferences.Deals(state);
                Assert.That(words.Count, Is.EqualTo(mode + 1)); Assert.That(deals.Count, Is.EqualTo(mode + 1));
                Assert.That(FrozenV29CommitmentReferences.PromiseCount(state), Is.EqualTo(mode + 1));
                Assert.That(FrozenV29CommitmentReferences.DealCount(state), Is.EqualTo(mode + 1));
                Assert.That(words.Any(row => ReferenceEquals(row, state.promises[0])), Is.False);
                Assert.That(deals.Any(row => ReferenceEquals(row, state.deals[0])), Is.False);
                Assert.Throws<NotSupportedException>(() => ((IList<PromiseState>)words).Add(RawPromise("cannot-add")));
            });
        }

        [Test]
        public void ModeTwoRawViewSharesRawRowsButProjectsCanonicalVoteAndOmitsSafety()
        {
            var state = State(2); var raw = RawPromise("raw-4"); state.promises.Add(raw);
            state.unifiedCommitments.Add(Canonical("vote-2", false, true));
            state.unifiedCommitments.Add(Canonical("safety-1", false, false));
            var rawDeal = RawDeal("raw-deal-4"); state.deals.Add(rawDeal);
            state.unifiedCommitments.Add(Canonical("vote-deal-2", true, true));
            state.unifiedCommitments.Add(Canonical("safety-deal-1", true, false));
            ReadUnchanged(state, () =>
            {
                var view = FrozenV29CommitmentReferences.RawPromises(state);
                CollectionAssert.AreEqual(new[] { "vote-2", "raw-4" }, view.Select(row => row.id));
                Assert.That(ReferenceEquals(view, state.promises), Is.False);
                Assert.That(ReferenceEquals(view[1], raw), Is.True);
                Assert.That(view[0].kind, Is.EqualTo(PromiseKind.Vote));
                var deals = FrozenV29CommitmentReferences.RawDeals(state);
                CollectionAssert.AreEqual(new[] { "vote-deal-2", "raw-deal-4" }, deals.Select(row => row.id));
                Assert.That(ReferenceEquals(deals, state.deals), Is.False);
                Assert.That(ReferenceEquals(deals[1], rawDeal), Is.True);
                Assert.That(deals[0].type, Is.EqualTo("vote_save"));
            });
        }

        [Test]
        public void SafetyRowsAppendAfterMergedRawAndVoteRegardlessOfNumericId()
        {
            var state = State(2); state.promises.Add(RawPromise("raw-4"));
            state.unifiedCommitments.Add(Canonical("vote-7", false, true));
            state.unifiedCommitments.Add(Canonical("safety-1", false, false));
            state.unifiedCommitments.Add(Canonical("vote-8", false, true));
            ReadUnchanged(state, () => CollectionAssert.AreEqual(new[] { "raw-4", "vote-7", "vote-8", "safety-1" },
                FrozenV29CommitmentReferences.Promises(state).Select(row => row.id)));
        }

        [Test]
        public void MergeKeepsUnsortedCanonicalFrontAndDoesNotGloballySort()
        {
            var state = State(2); state.promises.Add(RawPromise("raw-4"));
            state.unifiedCommitments.Add(Canonical("vote-99", false, true));
            state.unifiedCommitments.Add(Canonical("vote-1", false, true));
            ReadUnchanged(state, () => CollectionAssert.AreEqual(new[] { "raw-4", "vote-99", "vote-1" },
                FrozenV29CommitmentReferences.Promises(state).Select(row => row.id)));
        }

        [Test]
        public void EqualSequenceNonPriceCanonicalPrecedesRawPrice()
        {
            var state = State(2); state.deals.Add(RawDeal("deal-price-5"));
            state.unifiedCommitments.Add(Canonical("canonical-5", true, true));
            ReadUnchanged(state, () => CollectionAssert.AreEqual(new[] { "canonical-5", "deal-price-5" },
                FrozenV29CommitmentReferences.Deals(state).Select(row => row.id)));
        }

        [Test]
        public void EqualSequenceAndPriceRetainRawBeforeCanonical()
        {
            var state = State(2); state.deals.Add(RawDeal("raw-5"));
            state.unifiedCommitments.Add(Canonical("canonical-5", true, true));
            ReadUnchanged(state, () => CollectionAssert.AreEqual(new[] { "raw-5", "canonical-5" },
                FrozenV29CommitmentReferences.Deals(state).Select(row => row.id)));
        }

        [TestCase(null, long.MaxValue, 0)] [TestCase("plain", long.MaxValue, 0)]
        [TestCase("word-", long.MaxValue, 0)] [TestCase("word-0", 0L, 0)]
        [TestCase("word-0002", 2L, 0)] [TestCase("word--2", 2L, 0)]
        [TestCase("word-+2", long.MaxValue, 0)] [TestCase("word- 2", long.MaxValue, 0)]
        [TestCase("word-9223372036854775807", long.MaxValue, 0)] [TestCase("word-9223372036854775808", long.MaxValue, 0)]
        [TestCase("deal-price-7", 7L, 1)] [TestCase("Deal-price-7", 7L, 0)]
        public void OccurrenceParsingUsesLastDashUnsignedGrammarAndLiteralPricePrefix(string id, long sequence, int price)
        {
            var actual = FrozenV29Occurrence.Occurrence(id, true, 19);
            Assert.That(actual.sequence, Is.EqualTo(sequence)); Assert.That(actual.price, Is.EqualTo(price));
            Assert.That(actual.canonical, Is.EqualTo(1)); Assert.That(actual.index, Is.EqualTo(19));
        }

        [TestCase("active", PromiseStatus.Active)] [TestCase("broken", PromiseStatus.Broken)] [TestCase("expired", PromiseStatus.Expired)]
        public void SafetyProjectionStatusIsItsThreeMemberSwitch(string status, PromiseStatus expected)
        {
            var row = Canonical("id", false, false); row.status = status;
            var copy = FrozenV29CommitmentReferences.ProjectPromise(row);
            Assert.That(copy.status, Is.EqualTo(expected)); Assert.That(copy.kind, Is.EqualTo(PromiseKind.Safety));
        }

        [TestCase("fulfilled")] [TestCase("proposed")] [TestCase("accepted")] [TestCase("declined")] [TestCase(null)]
        public void SafetyProjectionRefusesOtherStatuses(string status)
        {
            Marked(() => FrozenV29CommitmentReferences.PromiseStatusFor(status), "Unsupported canonical safety-promise status.", null);
        }

        [TestCase("active", PromiseStatus.Active)] [TestCase("fulfilled", PromiseStatus.Fulfilled)]
        [TestCase("broken", PromiseStatus.Broken)] [TestCase("expired", PromiseStatus.Expired)]
        public void VoteProjectionStatusIncludesFulfilled(string status, PromiseStatus expected)
        {
            var row = Canonical("id", false, true); row.status = status;
            Assert.That(FrozenV29VoteReferences.ProjectPromise(row).status, Is.EqualTo(expected));
        }

        [TestCase("proposed")] [TestCase("accepted")] [TestCase("declined")] [TestCase(null)]
        public void VoteProjectionRefusesOtherStatuses(string status)
        {
            var row = Canonical("id", false, true); row.status = status;
            Marked(() => FrozenV29VoteReferences.ProjectPromise(row), "Only a validated promise status may be projected.", null);
        }

        [TestCase(false)] [TestCase(true)]
        public void PromiseProjectionWritesEveryFieldAndPreservesExplicitNull(bool vote)
        {
            var row = Canonical("source", false, vote); row.makerId = "maker"; row.beneficiaryId = "beneficiary";
            row.targetId = "target"; row.createdWeek = -8; row.expiresWeek = 123; row.trustImpact = null;
            row.brokenById = "breaker"; row.settledWeek = 33;
            var copy = vote ? FrozenV29VoteReferences.ProjectPromise(row) : FrozenV29CommitmentReferences.ProjectPromise(row);
            CollectionAssert.AreEqual(new object[] { "source", "maker", "beneficiary", vote ? "target" : null,
                vote ? PromiseKind.Vote : PromiseKind.Safety, PromiseStatus.Active, -8, 123, null, "breaker", 33 }, Values(copy));
            Assert.That(row.targetId, Is.EqualTo("target"));
        }

        [TestCase(false)] [TestCase(true)]
        public void DealProjectionWritesEveryFieldAndPreservesExplicitNull(bool vote)
        {
            var row = Canonical("source", true, vote); row.makerId = "maker"; row.beneficiaryId = "beneficiary";
            row.targetId = "target"; row.subtype = null; row.status = null; row.trustImpact = null;
            row.createdWeek = -9; row.expiresWeek = 124; row.brokenById = "breaker"; row.settledWeek = 34; row.linkedCommitmentId = "peer";
            var copy = vote ? FrozenV29VoteReferences.ProjectDeal(row) : FrozenV29CommitmentReferences.ProjectDeal(row);
            CollectionAssert.AreEqual(new object[] { "source", vote ? null : "safety_agreement", "maker", "beneficiary",
                vote ? "target" : null, null, -9, 124, null, "breaker", 34, "peer" }, Values(copy));
        }

        [Test]
        public void VoteProjectionCanAlsoProjectSafetyWithFullStatusMapping()
        {
            var word = Canonical("safety", false, false); word.status = "fulfilled";
            Assert.That(FrozenV29VoteReferences.ProjectPromise(word).kind, Is.EqualTo(PromiseKind.Safety));
            Assert.That(FrozenV29VoteReferences.ProjectPromise(word).status, Is.EqualTo(PromiseStatus.Fulfilled));
            Assert.That(FrozenV29VoteReferences.ProjectDeal(word).type, Is.EqualTo("safety_agreement"));
        }

        [Test]
        public void CanonicalFindCopiesAllFieldsAndReceiptOnlyUsesPositiveSafetySettlement()
        {
            var state = State(2); var safety = Canonical("safety", true, false);
            safety.status = "broken"; safety.settledWeek = 3; safety.brokenById = "p"; safety.settlementEffectKey = "safety:literal";
            var vote = Canonical("vote", true, true); vote.settledWeek = 99;
            state.unifiedCommitments.Add(safety); state.unifiedCommitments.Add(vote);
            ReadUnchanged(state, () =>
            {
                var found = FrozenV29CommitmentReferences.FindCanonical(state, "vote");
                CollectionAssert.AreEqual(Values(vote), Values(found)); Assert.That(ReferenceEquals(vote, found), Is.False);
                Assert.That(FrozenV29CommitmentReferences.FindCanonicalSafety(state, "vote"), Is.Null);
                Assert.That(FrozenV29CommitmentReferences.ReceiptWeek(state, "safety", -88), Is.EqualTo(3));
                Assert.That(FrozenV29CommitmentReferences.ReceiptWeek(state, "vote", -88), Is.EqualTo(-88));
                Assert.That(FrozenV29CommitmentReferences.FindCanonical(state, null), Is.Null);
            });
        }

        [Test]
        public void MarkerUsesExactExceptionTypeAndKeyPresenceNotBooleanValue()
        {
            var noParam = FrozenV29CommitmentReferences.StorageRefusal("reason");
            Assert.That(noParam.GetType(), Is.EqualTo(typeof(ArgumentException))); Assert.That(noParam.ParamName, Is.Null);
            var named = FrozenV29CommitmentReferences.StorageRefusal("reason", "state");
            Assert.That(named.ParamName, Is.EqualTo("state")); Assert.That(FrozenV29CommitmentReferences.IsStorageRefusal(named), Is.True);
            named.Data["Gamesim.Simulation.CommitmentStorageRefusal"] = false;
            Assert.That(FrozenV29CommitmentReferences.IsStorageRefusal(named), Is.True);
            var derived = new ArgumentNullException("state"); derived.Data["Gamesim.Simulation.CommitmentStorageRefusal"] = true;
            Assert.That(FrozenV29CommitmentReferences.IsStorageRefusal(derived), Is.False);
            Assert.That(FrozenV29CommitmentReferences.IsStorageRefusal(new ArgumentException("plain")), Is.False);
            Assert.That(FrozenV29CommitmentReferences.IsStorageRefusal(null), Is.False);
        }

        [Test]
        public void MarkedFailureDoesNotMutateListsOrRows()
        {
            var state = State(2); state.promises.Add(new PromiseState { id = "raw", kind = PromiseKind.Vote });
            ReadUnchanged(state, () => Marked(() => FrozenV29CommitmentReferences.Promises(state),
                "The unified Safety and Vote authority cannot have writable raw mirrors."));
        }

        private static void Marked(TestDelegate action, string message, string param = "state")
        {
            var error = Assert.Throws<ArgumentException>(action);
            Assert.That(error.GetType(), Is.EqualTo(typeof(ArgumentException)));
            Assert.That(error.Message.StartsWith(message, StringComparison.Ordinal), Is.True);
            Assert.That(error.ParamName, Is.EqualTo(param));
            Assert.That(FrozenV29CommitmentReferences.IsStorageRefusal(error), Is.True);
        }

        private static EpisodeState State(int mode) => new EpisodeState
        {
            week = 3, playerId = "p", unifiedCommitmentRulesVersion = mode,
            contestants = new List<ContestantState> { Person("p"), Person("a"), Person("b") },
            promises = new List<PromiseState>(), deals = new List<DealState>(), unifiedCommitments = new List<UnifiedCommitmentState>()
        };
        private static ContestantState Person(string id) => new ContestantState { id = id, nominationWeeks = new List<int>() };
        private static PromiseState RawPromise(string id) => new PromiseState { id = id, kind = PromiseKind.FinalTwo, status = PromiseStatus.Active, impact = "raw-value" };
        private static DealState RawDeal(string id) => new DealState { id = id, type = "partnership", status = "active", trustImpact = "raw-value" };
        private static UnifiedCommitmentState Canonical(string id, bool deal, bool vote) => new UnifiedCommitmentState
        {
            id = id, kind = vote ? "vote" : "safety", sourcePolicy = deal ? "deal" : "promise", origin = deal ? "player-deal" : "player-promise",
            makerId = "p", beneficiaryId = "a", reciprocal = deal, createdWeek = 3, expiresWeek = deal ? 3 : 4,
            status = "active", settledWeek = 0, brokenById = null, trustImpact = "medium", linkedCommitmentId = null,
            settlementEffectKey = null, targetId = vote ? "b" : null, subtype = vote && deal ? "vote_save" : null,
            voteBindingWeek = 0, voteFirstRevealWeek = 0
        };
        private static object[] Values(PromiseState row) => new object[] { row.id, row.fromId, row.toId, row.targetId, row.kind, row.status,
            row.week, row.expiresWeek, row.impact, row.brokenById, row.settledWeek };
        private static object[] Values(DealState row) => new object[] { row.id, row.type, row.proposerId, row.recipientId, row.targetId,
            row.status, row.week, row.expiresWeek, row.trustImpact, row.brokenById, row.settledWeek, row.linkedDealId };
        private static object[] Values(UnifiedCommitmentState row) => new object[] { row.id, row.kind, row.sourcePolicy, row.origin,
            row.makerId, row.beneficiaryId, row.reciprocal, row.createdWeek, row.expiresWeek, row.status, row.settledWeek,
            row.brokenById, row.trustImpact, row.linkedCommitmentId, row.settlementEffectKey, row.targetId, row.subtype,
            row.voteBindingWeek, row.voteFirstRevealWeek };

        private static void ReadUnchanged(EpisodeState state, Action action)
        {
            var before = Surface(state);
            try { action(); }
            finally
            {
                var after = Surface(state); Assert.That(after.Length, Is.EqualTo(before.Length));
                for (int i = 0; i < before.Length; i++)
                {
                    var value = before[i];
                    if (value != null && !(value is string) && !value.GetType().IsValueType)
                        Assert.That(ReferenceEquals(value, after[i]), Is.True, "Reference surface slot " + i);
                    else Assert.That(after[i], Is.EqualTo(value), "Scalar surface slot " + i);
                }
            }
        }
        private static object[] Surface(EpisodeState state)
        {
            var values = new List<object> { state, state.week, state.playerId, state.unifiedCommitmentRulesVersion,
                state.contestants, state.promises, state.deals, state.unifiedCommitments };
            if (state.contestants != null) foreach (var person in state.contestants)
            {
                values.Add(person); if (person == null) continue; values.Add(person.id); values.Add(person.nominationWeeks);
                if (person.nominationWeeks != null) foreach (int week in person.nominationWeeks) values.Add(week);
            }
            if (state.promises != null) foreach (var row in state.promises) { values.Add(row); if (row != null) values.AddRange(Values(row)); }
            if (state.deals != null) foreach (var row in state.deals) { values.Add(row); if (row != null) values.AddRange(Values(row)); }
            if (state.unifiedCommitments != null) foreach (var row in state.unifiedCommitments) { values.Add(row); if (row != null) values.AddRange(Values(row)); }
            return values.ToArray();
        }
    }
}
