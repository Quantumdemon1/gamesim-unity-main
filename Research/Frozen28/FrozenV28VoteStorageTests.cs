// Detached, manually specified storage fixtures; not whole saves or historical producer witnesses.
using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Persistence.Frozen28Data;
using Gamesim.Persistence.Frozen28Semantics;
using NUnit.Framework;

namespace Gamesim.Persistence.Frozen28Tests
{
    [TestFixture]
    public sealed class FrozenV28VoteStorageTests
    {
        private const string Mode = "Expected the explicit unified vote storage.";
        private const string Storage = "Invalid prospective commitment storage.";
        private const string Mirror = "The unified Safety and Vote authority cannot have writable raw mirrors.";
        private const string Shape = "A stored Vote row needs its family, policy, origin, subtype and an installed status.";
        private const string Trust = "A stored Vote row keeps its source's trust weight.";
        private const string Parties = "A stored Vote row's parties and target must be houseguests of this season.";
        private const string Target = "A stored Vote row names a target exactly where its source does.";
        private const string Link = "A stored Vote deal's link must resolve to another deal of this season.";
        private const string Collision = "Canonical identity collides with another commitment.";

        [TestCase(-1)] [TestCase(0)] [TestCase(1)] [TestCase(3)] [TestCase(int.MinValue)] [TestCase(int.MaxValue)]
        public void ExactModeTwoIsRequiredBeforeReadingStorage(int mode)
        {
            Check(new EpisodeState { unifiedCommitmentRulesVersion = mode }, false, Mode);
            Check(null, false, Mode);
        }

        [Test]
        public void LiteralSchemaMarkerDoesNotBecomeAnExtraStorageGuard()
        {
            Assert.That(FrozenV28VoteStorage.HistoricalSchema, Is.EqualTo(28));
            var state = State(); state.schemaVersion = -99;
            Check(state, true, null);
        }

        [Test]
        public void ValidEmptyAndMixedSafetyVoteStorage()
        {
            var state = State(); Check(state, true, null);
            state.unifiedCommitments.Add(Promise()); state.unifiedCommitments.Add(Deal());
            state.unifiedCommitments.Add(SafetyDeal()); Check(state, true, null);
        }

        [TestCase("vote_together", true)] [TestCase("vote_save", true)] [TestCase("vote_evict", true)]
        [TestCase("Vote_together", false)] [TestCase("vote", false)] [TestCase("safety_agreement", false)]
        [TestCase("target_agreement", false)] [TestCase("", false)] [TestCase(null, false)]
        public void ExactVoteSubtypeVocabulary(string subtype, bool expected)
        {
            Assert.That(FrozenV28VoteStorage.IsVoteType(subtype), Is.EqualTo(expected));
        }

        [TestCase("veto_use", "critical")] [TestCase("final_two", "critical")]
        [TestCase("target_agreement", "high")] [TestCase("safety_agreement", "high")]
        [TestCase("alliance_invite", "high")] [TestCase("final_three", "high")]
        [TestCase("information_sharing", "low")]
        [TestCase("vote_together", "medium")] [TestCase("vote_save", "medium")]
        [TestCase("vote_evict", "medium")] [TestCase("partnership", "medium")]
        [TestCase("unknown", "medium")] [TestCase("Veto_use", "medium")]
        [TestCase("", "medium")] [TestCase(null, "medium")]
        public void CompleteDefaultTrustIncludesEveryNonVoteArm(string subtype, string expected)
        {
            Assert.That(FrozenV28VoteStorage.DealKind.DefaultTrust(subtype), Is.EqualTo(expected));
        }

        [TestCase("player-promise", true)] [TestCase("npc-promise", true)] [TestCase("story-promise", true)]
        [TestCase("hoh-pitch", false)] [TestCase("player-deal", false)] [TestCase("npc-offer", false)]
        [TestCase("vote-lobby", false)] [TestCase("", false)] [TestCase(null, false)]
        public void PromiseOriginsUseTheirOwnThreeMemberVocabulary(string origin, bool accepted)
        {
            var state = State(); var row = Promise(); row.origin = origin;
            if (origin == "story-promise") row.targetId = null;
            state.unifiedCommitments.Add(row); Check(state, accepted, accepted ? null : Shape);
        }

        [TestCase("player-deal", true)] [TestCase("npc-deal", true)] [TestCase("npc-offer", true)]
        [TestCase("vote-lobby", true)] [TestCase("story-deal", true)] [TestCase("counter-deal", true)]
        [TestCase("counter-price", true)] [TestCase("veto-ask-price", true)] [TestCase("own-veto-price", true)]
        [TestCase("lobby", false)] [TestCase("player-promise", false)] [TestCase("", false)] [TestCase(null, false)]
        public void DealOriginsUseTheirOwnNineMemberVocabulary(string origin, bool accepted)
        {
            var state = State(); var row = Deal(); row.origin = origin;
            state.unifiedCommitments.Add(row); Check(state, accepted, accepted ? null : Shape);
        }

        [TestCase("proposed", false)] [TestCase("accepted", false)] [TestCase("active", true)]
        [TestCase("fulfilled", true)] [TestCase("broken", true)] [TestCase("declined", false)]
        [TestCase("expired", true)] [TestCase("ACTIVE", false)] [TestCase(null, false)]
        public void PromiseInstalledStatuses(string status, bool accepted)
        {
            var state = State(); var row = Promise(); row.status = status;
            state.unifiedCommitments.Add(row); Check(state, accepted, accepted ? null : Shape);
        }

        [TestCase("proposed", true)] [TestCase("accepted", false)] [TestCase("active", true)]
        [TestCase("fulfilled", true)] [TestCase("broken", true)] [TestCase("declined", true)]
        [TestCase("expired", true)] [TestCase("ACTIVE", false)] [TestCase(null, false)]
        public void DealInstalledStatuses(string status, bool accepted)
        {
            var state = State(); var row = Deal(); row.status = status;
            state.unifiedCommitments.Add(row); Check(state, accepted, accepted ? null : Shape);
        }

        [TestCase(false, "medium", true)] [TestCase(true, "medium", true)]
        [TestCase(false, "low", false)] [TestCase(true, "low", false)]
        [TestCase(false, "high", false)] [TestCase(true, "high", false)]
        [TestCase(false, "critical", false)] [TestCase(true, "critical", false)]
        [TestCase(false, null, false)] [TestCase(true, null, false)]
        public void SourceTrustIsExactForBothVotePolicies(bool deal, string trust, bool accepted)
        {
            var state = State(); var row = deal ? Deal() : Promise(); row.trustImpact = trust;
            state.unifiedCommitments.Add(row); Check(state, accepted, accepted ? null : Trust);
        }

        [TestCase("vote_together", null, true)] [TestCase("vote_together", "b", false)]
        [TestCase("vote_save", "b", true)] [TestCase("vote_save", null, false)]
        [TestCase("vote_evict", "b", true)] [TestCase("vote_evict", null, false)]
        public void DealTargetNullIffTogether(string subtype, string target, bool accepted)
        {
            var state = State(); var row = Deal(); row.subtype = subtype; row.targetId = target;
            state.unifiedCommitments.Add(row); Check(state, accepted, accepted ? null : Target);
        }

        [TestCase("player-promise", "b", true)] [TestCase("player-promise", null, false)]
        [TestCase("npc-promise", "b", true)] [TestCase("npc-promise", null, false)]
        [TestCase("story-promise", null, true)] [TestCase("story-promise", "b", false)]
        public void PromiseTargetNullIffStory(string origin, string target, bool accepted)
        {
            var state = State(); var row = Promise(); row.origin = origin; row.targetId = target;
            state.unifiedCommitments.Add(row); Check(state, accepted, accepted ? null : Target);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void PartyAndTargetIdentityFailures(int variant)
        {
            var state = State(); var row = Promise();
            if (variant == 0) row.makerId = null;
            if (variant == 1) row.beneficiaryId = "bad\nname";
            if (variant == 2) row.beneficiaryId = row.makerId;
            if (variant == 3) row.makerId = "absent";
            if (variant == 4) row.targetId = "absent";
            if (variant == 5) row.targetId = "bad\0target";
            state.unifiedCommitments.Add(row); Check(state, false, Parties);
        }

        [TestCase("p")] [TestCase("a")]
        public void TargetCanEqualEitherPartyAtThisStorageLayer(string target)
        {
            var state = State(); var row = Promise(); row.targetId = target;
            state.unifiedCommitments.Add(row); Check(state, true, null);
        }

        [TestCase("player-promise")] [TestCase("npc-promise")]
        public void StorageDoesNotInventCommandSpecificPlayerDirection(string origin)
        {
            var state = State(); var row = Promise(); row.origin = origin; row.makerId = "a"; row.beneficiaryId = "p";
            state.contestants[1].status = ContestantStatus.Expelled;
            state.unifiedCommitments.Add(row); Check(state, true, null);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void UnknownFamilyPolicySubtypeAndPromiseSubtypeRefuse(int variant)
        {
            var state = State(); var row = Promise();
            if (variant == 0) row.kind = "unknown";
            if (variant == 1) row.sourcePolicy = "unknown";
            if (variant == 2) row.subtype = "vote_save";
            if (variant == 3) { row = Deal(); row.subtype = "safety_agreement"; }
            if (variant == 4) { row = Deal(); row.subtype = null; }
            state.unifiedCommitments.Add(row); Check(state, false, Shape);
        }

        [TestCase("vote_together")] [TestCase("vote_save")] [TestCase("vote_evict")]
        public void EveryRawVoteDealMirrorRefuses(string subtype)
        {
            var state = State(); state.deals.Add(new DealState { id = "raw", type = subtype });
            Check(state, false, Mirror);
        }

        [Test]
        public void RawVotePromiseMirrorRefusesBeforeBadCanonicalShape()
        {
            var state = State(); state.promises.Add(new PromiseState { id = "raw", kind = PromiseKind.Vote });
            var row = Promise(); row.origin = "bad"; state.unifiedCommitments.Add(row);
            Check(state, false, Mirror);
        }

        [TestCase("target_agreement")] [TestCase("partnership")] [TestCase("unknown")] [TestCase(null)]
        public void OtherRawDealTypesStayOutsideVotePolicy(string type)
        {
            var state = State(); state.deals.Add(new DealState { id = "raw", type = type }); Check(state, true, null);
        }

        [TestCase(PromiseKind.FinalTwo)] [TestCase(PromiseKind.AllianceLoyalty)] [TestCase(PromiseKind.Information)]
        public void OtherRawPromiseKindsStayOutsideVotePolicy(PromiseKind kind)
        {
            var state = State(); state.promises.Add(new PromiseState { id = "raw", kind = kind }); Check(state, true, null);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void SafetyAuthorityRunsBeforeEveryVoteCheck(int variant)
        {
            var state = State();
            state.promises.Add(new PromiseState { id = "raw", kind = PromiseKind.Vote });
            if (variant == 0) state.unifiedCommitments = null;
            if (variant == 1) state.contestants = null;
            if (variant == 2) state.unifiedCommitments.Add(null);
            if (variant == 3) state.deals = null;
            if (variant == 4) state.promises.Add(null);
            if (variant == 5) state.contestants[1].id = "p";
            Check(state, false, Storage);
        }

        [Test]
        public void SafetyPolicyErrorPropagatesUnchangedBeforeVoteMirror()
        {
            var state = State(); var row = SafetyDeal(); row.expiresWeek = 99;
            state.unifiedCommitments.Add(row); state.promises.Add(new PromiseState { id = "raw", kind = PromiseKind.Vote });
            Check(state, false, "Invalid canonical safety term.");
        }

        [TestCase(null)] [TestCase("bad\nraw")]
        public void GlobalIdsAreStrictBeforeRawVoteMirror(string id)
        {
            var state = State(); state.promises.Add(new PromiseState { id = id, kind = PromiseKind.Vote });
            Check(state, false, Collision);
        }

        [Test]
        public void GlobalIdCollisionPrecedesCanonicalVoteShape()
        {
            var state = State(); var row = Promise(); row.origin = "bad";
            state.deals.Add(new DealState { id = row.id, type = "partnership" }); state.unifiedCommitments.Add(row);
            Check(state, false, Collision);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void VoteGuardOrderPreservesFirstFailure(int variant)
        {
            var state = State(); var row = Promise();
            row.linkedCommitmentId = "missing";
            if (variant == 0) { row.status = "bad"; row.trustImpact = "high"; row.makerId = "absent"; row.targetId = null; }
            if (variant == 1) { row.trustImpact = "high"; row.makerId = "absent"; row.targetId = null; }
            if (variant == 2) { row.makerId = "absent"; row.targetId = null; }
            if (variant == 3) row.targetId = null;
            state.unifiedCommitments.Add(row);
            Check(state, false, variant == 0 ? Shape : variant == 1 ? Trust : variant == 2 ? Parties : Target);
        }

        [TestCase(null, true)] [TestCase("raw-peer", true)] [TestCase("missing", false)]
        [TestCase("vote-deal-1", false)] [TestCase("RAW-PEER", false)]
        public void LinkedDealMustResolveByExactOrdinalId(string linked, bool accepted)
        {
            var state = State(); var row = Deal(); row.linkedCommitmentId = linked;
            state.deals.Add(new DealState { id = "raw-peer", type = "partnership" });
            state.unifiedCommitments.Add(row); Check(state, accepted, accepted ? null : Link);
        }

        [TestCase("raw-peer")] [TestCase("canonical-peer")] [TestCase("safety-peer")]
        public void LinkedPromiseRefusesEvenWhenDealExists(string linked)
        {
            var state = State(); var row = Promise(); row.linkedCommitmentId = linked;
            var peer = Deal(); peer.id = "canonical-peer";
            state.deals.Add(new DealState { id = "raw-peer", type = "partnership" });
            state.unifiedCommitments.Add(SafetyDeal()); state.unifiedCommitments.Add(peer); state.unifiedCommitments.Add(row);
            Check(state, false, Link);
        }

        [TestCase(false)] [TestCase(true)]
        public void ForwardAndBackwardCanonicalDealLinksResolveWithoutReciprocity(bool reverse)
        {
            var state = State(); var row = Deal(); row.linkedCommitmentId = "canonical-peer";
            var peer = Deal(); peer.id = "canonical-peer";
            state.unifiedCommitments.Add(row); state.unifiedCommitments.Add(peer);
            if (reverse) state.unifiedCommitments.Reverse();
            Check(state, true, null);
        }

        [Test]
        public void CanonicalSafetyDealIsAnEligibleLinkButPromiseIsNot()
        {
            var state = State(); var row = Deal(); row.linkedCommitmentId = "safety-peer";
            state.unifiedCommitments.Add(row); state.unifiedCommitments.Add(SafetyDeal()); Check(state, true, null);
            state.unifiedCommitments.RemoveAt(1);
            var peer = Promise(); peer.id = "safety-peer"; state.unifiedCommitments.Add(peer); Check(state, false, Link);
        }

        [Test]
        public void LinkLookupDoesNotPruneLaterRowsOrSkipTheirValidation()
        {
            var state = State(); var row = Deal(); row.linkedCommitmentId = "canonical-peer";
            var peer = Deal(); peer.id = "canonical-peer"; peer.status = "accepted";
            state.unifiedCommitments.Add(row); state.unifiedCommitments.Add(peer); Check(state, false, Shape);
        }

        [Test]
        public void StoredOrderChoosesFirstVoteError()
        {
            var state = State(); var a = Promise(); a.trustImpact = "high";
            var b = Deal(); b.targetId = null;
            state.unifiedCommitments.Add(a); state.unifiedCommitments.Add(b); Check(state, false, Trust);
            state.unifiedCommitments.Reverse(); Check(state, false, Target);
        }

        [Test]
        public void VoteChronologyAndBreachFieldsAreNotInventedStorageGuards()
        {
            var state = State(); var row = Promise();
            row.createdWeek = int.MinValue; row.expiresWeek = int.MaxValue; row.settledWeek = -77; row.reciprocal = true;
            row.brokenById = "absent"; row.settlementEffectKey = "not-a-receipt";
            row.voteBindingWeek = -55; row.voteFirstRevealWeek = int.MaxValue;
            state.unifiedCommitments.Add(row); Check(state, true, null);
        }

        [TestCase(false, 200, true)] [TestCase(false, 201, false)]
        [TestCase(true, 200, true)] [TestCase(true, 201, false)]
        public void SourceFamilyCapacityIncludesCanonicalAndRaw(bool deal, int count, bool accepted)
        {
            var state = State(); state.unifiedCommitments.Add(deal ? Deal() : Promise());
            for (int i = 1; i < count; i++)
            {
                if (deal) state.deals.Add(new DealState { id = "raw-" + i, type = "partnership" });
                else state.promises.Add(new PromiseState { id = "raw-" + i, kind = PromiseKind.FinalTwo });
            }
            Check(state, accepted, accepted ? null : "Commitment family capacity exceeded.");
        }

        [Test]
        public void FullFourHundredRowsRemainVisibleAndBounded()
        {
            var state = State();
            for (int i = 0; i < 200; i++)
            {
                var promise = Promise(); promise.id = "word-" + i;
                var deal = Deal(); deal.id = "pact-" + i;
                state.unifiedCommitments.Add(promise); state.unifiedCommitments.Add(deal);
            }
            Check(state, true, null);
            state.unifiedCommitments.Add(new UnifiedCommitmentState { id = "extra" });
            Check(state, false, Storage);
        }

        private static IEnumerable<TestCaseData> PartyTokens()
        {
            yield return new TestCaseData("", false).SetName("VotePartyEmpty");
            yield return new TestCaseData(" \t ", false).SetName("VotePartyWhitespace");
            yield return new TestCaseData("person\n", false).SetName("VotePartyControl");
            yield return new TestCaseData(new string('x', 160), true).SetName("VoteParty160");
            yield return new TestCaseData(new string('x', 161), false).SetName("VoteParty161");
            yield return new TestCaseData(" leading:space ", true).SetName("VotePartyUntrimmed");
        }

        [TestCaseSource(nameof(PartyTokens))]
        public void PartyTokenMatchesHistoricalUtf16Rules(string id, bool accepted)
        {
            // Cast IDs and row IDs use the same rule. Invalid IDs fail at the preceding Safety context gate.
            var state = State(); var row = Promise(); state.contestants[1].id = id; row.beneficiaryId = id;
            state.unifiedCommitments.Add(row); Check(state, accepted, accepted ? null : Storage);
        }

        private static EpisodeState State() => new EpisodeState
        {
            week = 3, playerId = "p", unifiedCommitmentRulesVersion = 2,
            contestants = new List<ContestantState> { Person("p"), Person("a"), Person("b") },
            promises = new List<PromiseState>(), deals = new List<DealState>(), unifiedCommitments = new List<UnifiedCommitmentState>()
        };

        private static ContestantState Person(string id) => new ContestantState { id = id, nominationWeeks = new List<int>() };

        private static UnifiedCommitmentState Promise() => new UnifiedCommitmentState
        {
            id = "vote-word-1", kind = "vote", sourcePolicy = "promise", origin = "player-promise",
            makerId = "p", beneficiaryId = "a", reciprocal = false, createdWeek = 3, expiresWeek = 4,
            status = "active", settledWeek = 0, brokenById = null, trustImpact = "medium", linkedCommitmentId = null,
            settlementEffectKey = null, targetId = "b", subtype = null, voteBindingWeek = 0, voteFirstRevealWeek = 0
        };

        private static UnifiedCommitmentState Deal() => new UnifiedCommitmentState
        {
            id = "vote-deal-1", kind = "vote", sourcePolicy = "deal", origin = "player-deal",
            makerId = "p", beneficiaryId = "a", reciprocal = true, createdWeek = 3, expiresWeek = 3,
            status = "active", settledWeek = 0, brokenById = null, trustImpact = "medium", linkedCommitmentId = null,
            settlementEffectKey = null, targetId = "b", subtype = "vote_save", voteBindingWeek = 0, voteFirstRevealWeek = 0
        };

        private static UnifiedCommitmentState SafetyDeal() => new UnifiedCommitmentState
        {
            id = "safety-peer", kind = "safety", sourcePolicy = "deal", origin = "player-deal",
            makerId = "p", beneficiaryId = "a", reciprocal = true, createdWeek = 3, expiresWeek = 3,
            status = "active", settledWeek = 0, brokenById = null, trustImpact = "medium", linkedCommitmentId = null,
            settlementEffectKey = null, targetId = null, subtype = null, voteBindingWeek = 0, voteFirstRevealWeek = 0
        };

        private static void Check(EpisodeState state, bool expected, string reason)
        {
            object[] before = Surface(state);
            try
            {
                string error = "sentinel";
                Assert.That(FrozenV28VoteStorage.TryValidateStorage(state, out error), Is.EqualTo(expected));
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
                values.Add(person); if (person == null) continue;
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
