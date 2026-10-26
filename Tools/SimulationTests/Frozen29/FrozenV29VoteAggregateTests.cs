// Frozen29 (SCH30): carried from Tools/SimulationTests/Frozen28/FrozenV28VoteAggregateTests.cs by name, then to schema 29
// where a case builds a whole schema-29 state or payload.
// Detached, explicitly constructed component fixtures; never historical producer witnesses.
using System;
using System.Collections.Generic;
using Gamesim.Persistence.Frozen29Data;
using Gamesim.Persistence.Frozen29ReadOnly;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Persistence.Frozen29Tests
{
    [TestFixture]
    public sealed class FrozenV29VoteAggregateTests
    {
        private static EpisodeState State() => new EpisodeState
        {
            schemaVersion = 29, week = 1, nextSequence = 10, playerId = "h",
            unifiedCommitmentRulesVersion = 2, commitmentRulesStartWeek = 1,
            phase = EpisodePhase.Social, evictionStage = EvictionStage.Interaction,
            contestants = new List<ContestantState>
            {
                Person("h", true), Person("a"), Person("b"), Person("v"), Person("x")
            },
            promises = new List<PromiseState>(), deals = new List<DealState>(),
            unifiedCommitments = new List<UnifiedCommitmentState>(),
            unifiedVoteReveals = new List<UnifiedVoteRevealState>(),
            nominees = new List<string>(), votes = new List<VoteState>(),
            ledger = new SeasonLedger { power = new List<PowerRow>() },
            story = new StoryWorldState
            {
                rulesStartWeek = 1, rulesVersion = 5, removals = new List<RemovalState>()
            }
        };

        private static ContestantState Person(string id, bool player = false) => new ContestantState
        {
            id = id, isPlayer = player, status = ContestantStatus.Active,
            nominationWeeks = new List<int>()
        };

        private static UnifiedCommitmentState Promise() => new UnifiedCommitmentState
        {
            id = "promise-npc-1", kind = "vote", sourcePolicy = "promise", origin = "npc-promise",
            makerId = "v", beneficiaryId = "x", targetId = "a", createdWeek = 1,
            expiresWeek = 1, voteBindingWeek = 1, voteFirstRevealWeek = 1,
            status = "active", trustImpact = "medium"
        };

        private static UnifiedVoteRevealState Complete(EpisodeState s)
        {
            s.phase = EpisodePhase.Eviction; s.evictionStage = EvictionStage.Results;
            s.evictionResolved = true; s.vetoResolved = true; s.hohId = "h"; s.vetoHolderId = "v";
            s.nominees = new List<string> { "a", "b" };
            s.contestants[1].status = ContestantStatus.Jury;
            s.ledger.power.Add(new PowerRow
            {
                week = 1, hohId = "h", vetoHolderId = "v", evicteeId = "a",
                nominees = new List<string> { "a", "b" }, tally = new List<int> { 2, 0 }
            });
            s.votes = new List<VoteState>
            {
                new VoteState { voterId = "v", targetId = "a" },
                new VoteState { voterId = "x", targetId = "a" }
            };
            var frame = new UnifiedVoteRevealState
            {
                week = 1, ballots = new List<UnifiedVoteBallotState>
                {
                    new UnifiedVoteBallotState { voterId = "v", targetId = "a" },
                    new UnifiedVoteBallotState { voterId = "x", targetId = "a" }
                }
            };
            s.unifiedVoteReveals.Add(frame);
            return frame;
        }

        private static void Aggregate(EpisodeState s, bool expected, string fragment = null)
        {
            string before = JsonConvert.SerializeObject(s);
            bool actual = UnifiedVoteFamilyValidation.TryValidate(s, s?.unifiedVoteReveals, out var error);
            Assert.That(actual, Is.EqualTo(expected), error);
            if (fragment != null) Assert.That(error, Does.Contain(fragment));
            Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(before), "reader changed its input");
        }

        [Test]
        public void EmptyBeginningAndActualCompletedFrameAreReadOnly()
        {
            Aggregate(State(), true);
            var state = State(); Complete(state); Aggregate(state, true);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(28)] [TestCase(30)] [TestCase(int.MaxValue)]
        public void AggregateRequiresLiteral29(int schema)
        {
            var state = State(); state.schemaVersion = schema; Aggregate(state, false, "live episode schema");
        }

        [TestCase(0)] [TestCase(1)] [TestCase(3)]
        public void AggregateRequiresExactMode2(int mode)
        {
            var state = State(); state.unifiedCommitmentRulesVersion = mode;
            Aggregate(state, false, "live episode schema");
        }

        [TestCase("promise-npc-01")] [TestCase("promise-npc-0")]
        [TestCase("promise-npc-10")] [TestCase("promise-npc-+1")]
        [TestCase("promise-npc-9223372036854775808")]
        public void ConsumedIdentityIsCanonicalAndBelowNextSequence(string id)
        {
            var state = State(); var row = Promise(); row.id = id; state.unifiedCommitments.Add(row);
            Aggregate(state, false, "consumed source sequence");
        }

        [TestCase("promise-npc-1", true)] [TestCase("promise-npc-9", true)]
        [TestCase("promise-npc-0", false)] [TestCase("promise-npc-01", false)]
        [TestCase("promise-npc-10", false)] [TestCase("Promise-npc-1", false)]
        [TestCase("promise-npc-١", false)] [TestCase("promise-npc-1\n", false)]
        public void InstalledIdentityRetainsOrdinalAsciiInt64Rules(string id, bool expected)
        {
            Assert.That(UnifiedVoteFamilyValidation.InstalledId(id, "promise-npc-", 10, out _), Is.EqualTo(expected));
        }

        [Test]
        public void RepeatedBindingDutyRefusesDespiteDistinctFullIds()
        {
            var state = State(); state.unifiedCommitments.Add(Promise());
            var second = Promise(); second.id = "promise-npc-2"; state.unifiedCommitments.Add(second);
            Aggregate(state, false, "duty and real term");
        }

        [Test]
        public void SameWeekRemovalStillRequiresItsOrdinaryBallot()
        {
            var state = State(); var frame = Complete(state);
            state.contestants[4].status = ContestantStatus.Expelled;
            state.story.removals.Add(new RemovalState { contestantId = "x", week = 1, reasonId = "conduct" });
            Aggregate(state, true);
            frame.ballots.RemoveAt(1); state.votes.RemoveAt(1);
            Aggregate(state, false, "every actual ordinary voter");
        }

        [Test]
        public void CurrentArchiveOrderMatchesTheActualPrivateBox()
        {
            var state = State(); var frame = Complete(state); frame.ballots.Reverse();
            Aggregate(state, false, "private-box order");
        }

        [Test]
        public void AHiddenMandatoryFirstDecisionRefusesAnActiveRow()
        {
            var state = State(); Complete(state); state.unifiedCommitments.Add(Promise());
            Aggregate(state, false, "earlier mandatory verdict");
        }

        [Test]
        public void AStoredTerminalRowMatchesItsFirstDecisionWithoutChangingInput()
        {
            var state = State(); Complete(state); var row = Promise();
            row.status = "fulfilled"; row.settledWeek = 1; state.unifiedCommitments.Add(row);
            Aggregate(state, true);
        }

        [Test]
        public void FirstDecisionUsesDetachedCloneAndFirstSuppliedEligibleFrame()
        {
            var state = State(); Complete(state); var row = Promise();
            string before = JsonConvert.SerializeObject(row);
            Assert.That(UnifiedVoteFamilyValidation.FirstDecision(state, state.unifiedVoteReveals, row,
                out var frame, out var status, out var actor, out var error), Is.True, error);
            Assert.That(frame, Is.SameAs(state.unifiedVoteReveals[0]));
            Assert.That(status, Is.EqualTo("fulfilled")); Assert.That(actor, Is.EqualTo("v"));
            Assert.That(JsonConvert.SerializeObject(row), Is.EqualTo(before));
        }

        [Test]
        public void UnboundOfferReturnsBeforeArchiveOrCloneAccess()
        {
            var row = new UnifiedCommitmentState { voteBindingWeek = 0 };
            Assert.That(UnifiedVoteFamilyValidation.FirstDecision(null, null, row,
                out var frame, out var status, out var actor, out var error), Is.True);
            Assert.That(frame, Is.Null); Assert.That(status, Is.Null);
            Assert.That(actor, Is.Null); Assert.That(error, Is.Null);
        }

        [Test]
        public void MissingRequiredPowerRetainsSourceFirstException()
        {
            var state = State(); var row = Promise();
            Assert.Throws<InvalidOperationException>(() =>
                UnifiedVoteFamilyValidation.FirstDecision(state,
                    new List<UnifiedVoteRevealState> { new UnifiedVoteRevealState { week = 1 } }, row,
                    out _, out _, out _, out _));
        }

        [TestCase("promise", null, true)] [TestCase("deal", "vote_together", true)]
        [TestCase("deal", "vote_save", false)] [TestCase("deal", "vote_evict", false)]
        public void OnlyTargetedDealsAcquireTheActualRevealWeekLeverGate(string policy, string subtype, bool expected)
        {
            var state = State(); var row = Promise(); row.sourcePolicy = policy; row.subtype = subtype;
            Assert.That(UnifiedVoteFamilyValidation.Eligible(state, row, 1, 1), Is.EqualTo(expected));
            state.leverRulesStartWeek = 1;
            Assert.That(UnifiedVoteFamilyValidation.Eligible(state, row, 1, 1), Is.True);
        }

        [TestCase(0, 1, false)] [TestCase(1, 1, true)] [TestCase(2, 1, false)]
        public void EligibilityKeepsFirstFloorTermAndCutoff(int week, int cutoff, bool expected)
        {
            Assert.That(UnifiedVoteFamilyValidation.Eligible(State(), Promise(), week, cutoff), Is.EqualTo(expected));
        }

        [Test]
        public void LateFloorNeedsPassedRegularFrameAndSurvivingParties()
        {
            var state = State(); Complete(state); var row = Promise();
            row.origin = "story-promise"; row.id = "promise-1"; row.targetId = null;
            row.voteFirstRevealWeek = 2; state.unifiedCommitments.Add(row);
            Aggregate(state, true);
            row.makerId = "a"; Aggregate(state, false, "late floor");
        }

        [TestCase("information_sharing", 2)] [TestCase("veto_use", 1)]
        public void PriceVoidCutoffPreservesInformationAfterRevealOrdering(string boughtType, int expected)
        {
            var state = State(); state.week = 3;
            var row = new UnifiedCommitmentState
            {
                id = "deal-price-1", sourcePolicy = "deal", makerId = "v", beneficiaryId = "h",
                linkedCommitmentId = "deal-counter-1", expiresWeek = 0
            };
            state.deals.Add(new DealState
            {
                id = "deal-counter-1", type = boughtType, status = "broken",
                brokenById = "h", settledWeek = 2
            });
            Assert.That(UnifiedVoteFamilyValidation.EndingCutoff(state, row), Is.EqualTo(expected));
        }

        [Test]
        public void CastDepartureOwnershipCannotBeMissingOrConflicting()
        {
            var state = State(); Complete(state);
            state.story.removals.Add(new RemovalState { contestantId = "a", week = 1, reasonId = "conduct" });
            Aggregate(state, false, "Conflicting durable departure");
        }

        [Test]
        public void ErasingPriorFrameAndItsOwnerDoesNotExcuseAHistoryHole()
        {
            var state = State(); state.week = 2;
            Aggregate(state, false, "missing a prior regular power owner");
        }

        [Test]
        public void ThreePersonFinalSelectionIsExcludedFromRegularArchive()
        {
            var state = State(); state.contestants.RemoveRange(3, 2);
            state.phase = EpisodePhase.JuryQuestioning; state.hohId = "h";
            state.contestants[1].status = ContestantStatus.Jury;
            state.ledger.power.Add(new PowerRow
            {
                week = 1, hohId = "h", evicteeId = "a",
                nominees = new List<string> { "a", "b" }, tally = new List<int>()
            });
            Aggregate(state, true);
            state.unifiedVoteReveals.Add(new UnifiedVoteRevealState { week = 1, ballots = new List<UnifiedVoteBallotState>() });
            Aggregate(state, false, "exactly cover");
        }

        private static EpisodeState Counter()
        {
            var state = State();
            state.deals.Add(new DealState
            {
                id = "deal-counter-1", type = "information_sharing", proposerId = "h", recipientId = "v",
                week = 1, status = "active", trustImpact = "low", linkedDealId = "deal-price-1"
            });
            state.unifiedCommitments.Add(new UnifiedCommitmentState
            {
                id = "deal-price-1", kind = "vote", sourcePolicy = "deal", origin = "counter-price",
                makerId = "h", beneficiaryId = "v", reciprocal = true, targetId = "v", subtype = "vote_save",
                createdWeek = 1, expiresWeek = 1, voteBindingWeek = 1, voteFirstRevealWeek = 1,
                status = "active", trustImpact = "medium", linkedCommitmentId = "deal-counter-1"
            });
            return state;
        }

        [Test]
        public void LinkedCounterKeepsItsDistinctSourcePolicies()
        {
            Aggregate(Counter(), true);
        }

        [TestCase("reciprocity")] [TestCase("sequence")] [TestCase("same-type")]
        [TestCase("term")] [TestCase("pair")]
        public void LinkedCounterRejectsIncompatibleOwnership(string defect)
        {
            var state = Counter(); var bought = state.deals[0]; var paid = state.unifiedCommitments[0];
            switch (defect)
            {
                case "reciprocity": bought.linkedDealId = null; break;
                case "sequence": bought.id = "deal-counter-2"; paid.linkedCommitmentId = bought.id; break;
                case "same-type": bought.type = "vote_save"; bought.targetId = "v"; bought.trustImpact = "medium"; break;
                case "term": bought.expiresWeek = 1; break;
                case "pair": bought.recipientId = "x"; break;
            }
            Aggregate(state, false);
        }

        [TestCase(true)] [TestCase(false)]
        public void VetoPricesKeepTheirSeparateAskAndOwnVetoProvenance(bool acceptedAsk)
        {
            var state = State(); string boughtId = acceptedAsk ? "deal-veto-2" : "deal-player-1";
            state.deals.Add(new DealState
            {
                id = boughtId, type = "veto_use", proposerId = acceptedAsk ? "v" : "h",
                recipientId = acceptedAsk ? "h" : "v", week = 1, expiresWeek = 1,
                status = "active", trustImpact = "critical", linkedDealId = "deal-price-1"
            });
            state.unifiedCommitments.Add(new UnifiedCommitmentState
            {
                id = "deal-price-1", kind = "vote", sourcePolicy = "deal",
                origin = acceptedAsk ? "veto-ask-price" : "own-veto-price",
                makerId = "v", beneficiaryId = "h", reciprocal = true, targetId = "h", subtype = "vote_save",
                createdWeek = 1, expiresWeek = 0, voteBindingWeek = 1, voteFirstRevealWeek = 1,
                status = "active", trustImpact = "medium", linkedCommitmentId = boughtId
            });
            Aggregate(state, true);
            state.deals[0].targetId = "a";
            Aggregate(state, false, "veto price");
        }

        [TestCase("null-row")] [TestCase("unknown-type")] [TestCase("unknown-trust")]
        [TestCase("same-party")] [TestCase("future-week")]
        public void RawScalarsFailBeforeReferenceLookups(string defect)
        {
            var state = Counter(); var raw = state.deals[0];
            switch (defect)
            {
                case "null-row": state.deals[0] = null; break;
                case "unknown-type": raw.type = "unknown"; break;
                case "unknown-trust": raw.trustImpact = "unknown"; break;
                case "same-party": raw.recipientId = raw.proposerId; break;
                case "future-week": raw.week = 2; break;
            }
            Aggregate(state, false, "raw deal scalar");
        }
    }
}
