using System;
using System.Collections.Generic;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Pure local contract tests, not Unity/save/mode2 integration evidence. The party equality,
    /// missing partner and repeated-party controls follow the retained original DealSystem
    /// source04 boundary capture; the separate source-fixture consumer owns captured-byte replay.
    /// </summary>
    public sealed class UnifiedVoteTogetherTests
    {
        [TestCase(false, false, false)]
        [TestCase(false, false, true)]
        [TestCase(false, true, false)]
        [TestCase(false, true, true)]
        [TestCase(true, false, false)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        [TestCase(true, true, true)]
        public void ActualPartyTargetsDecideIndependentlyOfDirectionAndArrivalOrder(
            bool reverseParties, bool reverseBallots, bool differentTargets)
        {
            var row = Row();
            if (reverseParties)
            {
                row.makerId = "B";
                row.beneficiaryId = "A";
            }
            var ballots = Ballots(Ballot("A", "C"), Ballot("B", differentTargets ? "D" : "C"));
            if (reverseBallots) ballots.Reverse();
            Probe(row, ballots, true, differentTargets ? DealStatus.Broken : DealStatus.Fulfilled);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void MissingActualPartnerNeverBecomesAVerdict(int mask)
        {
            var ballots = Ballots();
            if ((mask & 1) != 0) ballots.Add(Ballot("A", "C"));
            if ((mask & 2) != 0) ballots.Add(Ballot("B", "C"));
            if ((mask & 4) != 0) ballots.Add(Ballot("outsider", "C"));
            Probe(Row(), ballots, true, null);
        }

        [TestCase(DealStatus.Proposed, false)]
        [TestCase(DealStatus.Proposed, true)]
        [TestCase(DealStatus.Accepted, false)]
        [TestCase(DealStatus.Accepted, true)]
        [TestCase(DealStatus.Declined, false)]
        [TestCase(DealStatus.Declined, true)]
        [TestCase(DealStatus.Expired, false)]
        [TestCase(DealStatus.Expired, true)]
        [TestCase(DealStatus.Fulfilled, false)]
        [TestCase(DealStatus.Fulfilled, true)]
        [TestCase(DealStatus.Broken, false)]
        [TestCase(DealStatus.Broken, true)]
        public void KnownInactiveRowsNeverSettleAgain(string status, bool differentTargets)
        {
            var row = Row();
            row.status = status;
            Probe(row, Ballots(Ballot("A", "C"), Ballot("B", differentTargets ? "D" : "C")), true, null);
        }

        [TestCase(DealStatus.Proposed)]
        [TestCase(DealStatus.Accepted)]
        [TestCase(DealStatus.Declined)]
        [TestCase(DealStatus.Expired)]
        [TestCase(DealStatus.Fulfilled)]
        [TestCase(DealStatus.Broken)]
        public void InactiveStatusDoesNotHideMalformedBallotEvidence(string status)
        {
            var row = Row();
            row.status = status;
            Probe(row, Ballots(Ballot("A", "C"), Ballot("B", "C"), Ballot("outsider", null)), false, null);
        }

        [TestCase("A", false)]
        [TestCase("A", true)]
        [TestCase("B", false)]
        [TestCase("B", true)]
        [TestCase("outsider", false)]
        [TestCase("outsider", true)]
        public void DuplicateVotersAreRefusedEvenWhenTheirTargetsAgree(string duplicateVoter, bool differentTargets)
        {
            var ballots = Ballots(Ballot("A", "C"), Ballot("B", "C"));
            if (duplicateVoter == "outsider") ballots.Add(Ballot("outsider", "C"));
            ballots.Add(Ballot(duplicateVoter, differentTargets ? "D" : "C"));
            Probe(Row(), ballots, false, null);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void CallerNormalizedLatestActualBallotIsUsedWithoutAnInternalTracker(
            bool reverseParties, bool latestMatchesPartner)
        {
            var row = Row();
            var repeatedId = reverseParties ? "B" : "A";
            var partnerId = reverseParties ? "A" : "B";
            // Source records C, then overwrites this party's still-pending ballot with D.
            var partnerTarget = latestMatchesPartner ? "D" : "C";
            Probe(row, Ballots(Ballot(repeatedId, "C"), Ballot(repeatedId, "D"),
                Ballot(partnerId, partnerTarget)), false, null);
            Probe(row, Ballots(Ballot(repeatedId, "D"), Ballot(partnerId, partnerTarget)), true,
                latestMatchesPartner ? DealStatus.Fulfilled : DealStatus.Broken);
        }

        [TestCase("null-row")]
        [TestCase("null-id")]
        [TestCase("empty-id")]
        [TestCase("blank-id")]
        [TestCase("long-id")]
        [TestCase("control-id")]
        [TestCase("null-maker")]
        [TestCase("empty-maker")]
        [TestCase("blank-maker")]
        [TestCase("long-maker")]
        [TestCase("control-maker")]
        [TestCase("null-beneficiary")]
        [TestCase("empty-beneficiary")]
        [TestCase("blank-beneficiary")]
        [TestCase("long-beneficiary")]
        [TestCase("control-beneficiary")]
        [TestCase("same-parties")]
        [TestCase("null-kind")]
        [TestCase("safety-kind")]
        [TestCase("uppercase-kind")]
        [TestCase("null-policy")]
        [TestCase("promise-policy")]
        [TestCase("uppercase-policy")]
        [TestCase("null-subtype")]
        [TestCase("save-subtype")]
        [TestCase("evict-subtype")]
        [TestCase("uppercase-subtype")]
        [TestCase("empty-target")]
        [TestCase("named-target")]
        [TestCase("directional")]
        [TestCase("null-status")]
        [TestCase("unknown-status")]
        [TestCase("uppercase-status")]
        public void InvalidLocalRowCannotProduceAVerdict(string defect)
        {
            var row = Row();
            switch (defect)
            {
                case "null-row": row = null; break;
                case "null-id": row.id = null; break;
                case "empty-id": row.id = ""; break;
                case "blank-id": row.id = "   "; break;
                case "long-id": row.id = new string('x', 161); break;
                case "control-id": row.id = "id\0"; break;
                case "null-maker": row.makerId = null; break;
                case "empty-maker": row.makerId = ""; break;
                case "blank-maker": row.makerId = "   "; break;
                case "long-maker": row.makerId = new string('x', 161); break;
                case "control-maker": row.makerId = "A\n"; break;
                case "null-beneficiary": row.beneficiaryId = null; break;
                case "empty-beneficiary": row.beneficiaryId = ""; break;
                case "blank-beneficiary": row.beneficiaryId = "   "; break;
                case "long-beneficiary": row.beneficiaryId = new string('x', 161); break;
                case "control-beneficiary": row.beneficiaryId = "B\t"; break;
                case "same-parties": row.beneficiaryId = row.makerId; break;
                case "null-kind": row.kind = null; break;
                case "safety-kind": row.kind = UnifiedCommitments.Safety; break;
                case "uppercase-kind": row.kind = "Vote"; break;
                case "null-policy": row.sourcePolicy = null; break;
                case "promise-policy": row.sourcePolicy = UnifiedCommitments.PromisePolicy; break;
                case "uppercase-policy": row.sourcePolicy = "Deal"; break;
                case "null-subtype": row.subtype = null; break;
                case "save-subtype": row.subtype = DealKind.VoteSave; break;
                case "evict-subtype": row.subtype = DealKind.VoteEvict; break;
                case "uppercase-subtype": row.subtype = "Vote_Together"; break;
                case "empty-target": row.targetId = ""; break;
                case "named-target": row.targetId = "C"; break;
                case "directional": row.reciprocal = false; break;
                case "null-status": row.status = null; break;
                case "unknown-status": row.status = "unknown"; break;
                case "uppercase-status": row.status = "Active"; break;
                default: throw new ArgumentException("Unknown row defect: " + defect);
            }
            Probe(row, Ballots(Ballot("A", "C"), Ballot("B", "C")), false, null);
        }

        [TestCase("null-container")]
        [TestCase("null-entry")]
        [TestCase("null-voter")]
        [TestCase("empty-voter")]
        [TestCase("blank-voter")]
        [TestCase("control-voter")]
        [TestCase("long-voter")]
        [TestCase("null-target")]
        [TestCase("empty-target")]
        [TestCase("blank-target")]
        [TestCase("control-target")]
        [TestCase("long-target")]
        [TestCase("duplicate-maker")]
        [TestCase("duplicate-beneficiary")]
        [TestCase("duplicate-outsider")]
        [TestCase("over-bound")]
        public void InvalidActualSnapshotIsRefusedWithoutChoosingAnInputByOrder(string defect)
        {
            var ballots = Ballots(Ballot("A", "C"), Ballot("B", "C"));
            var last = Ballot("outsider", "D");
            ballots.Add(last);
            switch (defect)
            {
                case "null-container": ballots = null; break;
                case "null-entry": ballots[2] = null; break;
                case "null-voter": last.voterId = null; break;
                case "empty-voter": last.voterId = ""; break;
                case "blank-voter": last.voterId = "   "; break;
                case "control-voter": last.voterId = "outsider\n"; break;
                case "long-voter": last.voterId = new string('x', 161); break;
                case "null-target": last.targetId = null; break;
                case "empty-target": last.targetId = ""; break;
                case "blank-target": last.targetId = "   "; break;
                case "control-target": last.targetId = "D\0"; break;
                case "long-target": last.targetId = new string('x', 161); break;
                case "duplicate-maker": last.voterId = "A"; break;
                case "duplicate-beneficiary": last.voterId = "B"; break;
                case "duplicate-outsider": ballots.Add(last.Clone()); break;
                case "over-bound":
                    while (ballots.Count <= UnifiedVoteTogether.MaximumBallots)
                        ballots.Add(Ballot("outsider-" + ballots.Count, "C"));
                    break;
                default: throw new ArgumentException("Unknown ballot defect: " + defect);
            }
            Probe(Row(), ballots, false, null);
        }

        [TestCase("id", false)]
        [TestCase("id", true)]
        [TestCase("maker", false)]
        [TestCase("maker", true)]
        [TestCase("beneficiary", false)]
        [TestCase("beneficiary", true)]
        [TestCase("voter", false)]
        [TestCase("voter", true)]
        [TestCase("target", false)]
        [TestCase("target", true)]
        public void ExactIdentityLengthBoundaryIsAcceptedWithoutChangingSpelling(string field, bool unicode)
        {
            var row = Row();
            var token = new string(unicode ? '\u03bb' : 'x', 160);
            var ballots = Ballots(Ballot("A", "C"), Ballot("B", "C"));
            switch (field)
            {
                case "id": row.id = token; break;
                case "maker": row.makerId = token; ballots[0].voterId = token; break;
                case "beneficiary": row.beneficiaryId = token; ballots[1].voterId = token; break;
                case "voter": ballots.Add(Ballot(token, "C")); break;
                case "target": ballots[0].targetId = token; ballots[1].targetId = token; break;
                default: throw new ArgumentException("Unknown identity field: " + field);
            }
            Probe(row, ballots, true, DealStatus.Fulfilled);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(15)]
        [TestCase(16)]
        public void ActualBallotCapacityIsInclusiveAndOutsidersDoNotChangeTheVerdict(int count)
        {
            Assert.AreEqual(16, UnifiedVoteTogether.MaximumBallots);
            var ballots = Ballots();
            if (count > 0) ballots.Add(Ballot("A", "C"));
            if (count > 1) ballots.Add(Ballot("B", "C"));
            while (ballots.Count < count) ballots.Add(Ballot("outsider-" + ballots.Count, "D"));
            Probe(Row(), ballots, true, count < 2 ? null : DealStatus.Fulfilled);
        }

        [TestCase("A", "a", "C", "C", DealStatus.Fulfilled)]
        [TestCase("A", "B", "C", "c", DealStatus.Broken)]
        [TestCase("A", "A ", "C", "C", DealStatus.Fulfilled)]
        [TestCase("A", "B", "C", "C ", DealStatus.Broken)]
        public void IdentityAndTargetComparisonsAreOrdinalNotTrimmedOrCaseFolded(
            string maker, string beneficiary, string makerTarget, string beneficiaryTarget, string expected)
        {
            var row = Row();
            row.makerId = maker;
            row.beneficiaryId = beneficiary;
            Probe(row, Ballots(Ballot(maker, makerTarget), Ballot(beneficiary, beneficiaryTarget)), true, expected);
        }

        [TestCase("pending")]
        [TestCase("fulfilled")]
        [TestCase("broken")]
        public void LocalComparisonDoesNotClaimToValidateUnrelatedStoredMetadata(string outcome)
        {
            var row = Row();
            row.origin = "not-a-validated-origin";
            row.createdWeek = -10;
            row.expiresWeek = -20;
            row.settledWeek = 1001;
            row.brokenById = "unvalidated-actor";
            row.trustImpact = "not-a-validated-impact";
            row.linkedCommitmentId = "unvalidated-link";
            row.settlementEffectKey = "unvalidated-key";
            var ballots = Ballots(Ballot("A", "C"));
            if (outcome != "pending") ballots.Add(Ballot("B", outcome == "fulfilled" ? "C" : "D"));
            Probe(row, ballots, true, outcome == "pending" ? null : outcome);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void VerdictIsDetachedFromLaterCallerMutations(bool differentTargets)
        {
            var row = Row();
            var ballots = Ballots(Ballot("A", "C"), Ballot("B", differentTargets ? "D" : "C"));
            var expected = differentTargets ? DealStatus.Broken : DealStatus.Fulfilled;
            var verdict = Probe(row, ballots, true, expected);
            row.status = DealStatus.Expired;
            row.makerId = "changed-maker";
            ballots[0].targetId = "changed-target";
            ballots.Clear();
            Assert.AreEqual(expected, verdict);
            Probe(Row(), Ballots(Ballot("A", "C"), Ballot("B", "C")), true, DealStatus.Fulfilled);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RepeatedReadDoesNotSettleTheCallerRowOrConsumeItsBallots(bool differentTargets)
        {
            var row = Row();
            var ballots = Ballots(Ballot("A", "C"), Ballot("B", differentTargets ? "D" : "C"));
            var expected = differentTargets ? DealStatus.Broken : DealStatus.Fulfilled;
            Assert.AreEqual(Probe(row, ballots, true, expected), Probe(row, ballots, true, expected));
            Assert.AreEqual(DealStatus.Active, row.status);
            Assert.AreEqual(0, row.settledWeek);
            Assert.IsNull(row.brokenById);
            Assert.IsNull(row.settlementEffectKey);
            Assert.IsNull(row.linkedCommitmentId);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClonedInputsStayDetachedFromTheirOriginals(bool differentTargets)
        {
            var original = Row();
            var originalBallots = Ballots(Ballot("A", "C"), Ballot("B", differentTargets ? "D" : "C"));
            var copy = original.Clone();
            var copyBallots = Ballots(originalBallots[0].Clone(), originalBallots[1].Clone());
            var expected = differentTargets ? DealStatus.Broken : DealStatus.Fulfilled;
            Probe(copy, copyBallots, true, expected);
            copy.id = "detached-copy";
            copyBallots[0].targetId = "detached-target";
            Assert.AreEqual("vote-deal-local", original.id);
            Assert.AreEqual("C", originalBallots[0].targetId);
            Assert.AreNotSame(original, copy);
            Assert.AreNotSame(originalBallots[0], copyBallots[0]);
            Probe(original, originalBallots, true, expected);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SeparateInvocationsNeverAccumulateAHiddenPartnerTracker(bool reverseOrder)
        {
            var firstParty = reverseOrder ? "B" : "A";
            var secondParty = reverseOrder ? "A" : "B";
            var row = Row();
            Probe(row, Ballots(Ballot(firstParty, "C")), true, null);
            Probe(row, Ballots(Ballot(secondParty, "C")), true, null);
            Probe(row, Ballots(), true, null);
        }

        [Test]
        public void CastAndNomineeEligibilityAreNotInventedByThisLocalPredicate()
        {
            // Identity-only comparison intentionally has no cast/block/phase input. A complete
            // save or command owner must independently refuse an ineligible or self-cast ballot.
            Probe(Row(), Ballots(Ballot("A", "A"), Ballot("B", "A")), true, DealStatus.Fulfilled);
        }

        private static UnifiedCommitmentState Row() => new UnifiedCommitmentState
        {
            id = "vote-deal-local",
            kind = UnifiedVoteTogether.Vote,
            sourcePolicy = UnifiedCommitments.DealPolicy,
            origin = UnifiedCommitments.PlayerDeal,
            makerId = "A",
            beneficiaryId = "B",
            reciprocal = true,
            createdWeek = 4,
            expiresWeek = 4,
            status = DealStatus.Active,
            trustImpact = DealTrust.Medium,
            subtype = DealKind.VoteTogether,
        };

        private static UnifiedVoteBallotState Ballot(string voter, string target) => new UnifiedVoteBallotState
        {
            voterId = voter,
            targetId = target,
        };

        private static List<UnifiedVoteBallotState> Ballots(params UnifiedVoteBallotState[] ballots)
            => new List<UnifiedVoteBallotState>(ballots);

        private static string Probe(UnifiedCommitmentState row, List<UnifiedVoteBallotState> ballots,
            bool valid, string expectedVerdict)
        {
            var rowBefore = row?.Clone();
            UnifiedVoteBallotState[] references = null, before = null;
            if (ballots != null)
            {
                references = ballots.ToArray();
                before = new UnifiedVoteBallotState[ballots.Count];
                for (var index = 0; index < ballots.Count; index++) before[index] = ballots[index]?.Clone();
            }

            string verdict = "stale-verdict", error = "stale-error";
            var accepted = UnifiedVoteTogether.TryVerdict(row, ballots, out verdict, out error);
            Assert.AreEqual(valid, accepted, error);
            Assert.AreEqual(expectedVerdict, verdict);
            if (valid) Assert.IsNull(error);
            else Assert.That(error, Is.Not.Null.And.Not.Empty);

            if (row != null) CollectionAssert.AreEqual(Fields(rowBefore), Fields(row));
            if (ballots != null)
            {
                Assert.AreEqual(references.Length, ballots.Count);
                for (var index = 0; index < ballots.Count; index++)
                {
                    Assert.AreSame(references[index], ballots[index]);
                    if (before[index] == null) Assert.IsNull(ballots[index]);
                    else
                    {
                        Assert.AreEqual(before[index].voterId, ballots[index].voterId);
                        Assert.AreEqual(before[index].targetId, ballots[index].targetId);
                    }
                }
            }
            return verdict;
        }

        private static object[] Fields(UnifiedCommitmentState row) => new object[]
        {
            row.id, row.kind, row.sourcePolicy, row.origin, row.makerId, row.beneficiaryId,
            row.reciprocal, row.createdWeek, row.expiresWeek, row.status, row.settledWeek,
            row.brokenById, row.trustImpact, row.linkedCommitmentId, row.settlementEffectKey,
            row.targetId, row.subtype,
        };
    }
}
