using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class EndgameCommitmentsTests
    {
        [TestCase(-80)] [TestCase(-10)] [TestCase(0)] [TestCase(25)] [TestCase(50)] [TestCase(80)]
        public void AnActiveFinalTwoKeepsItsStrengthAcrossColdZeroAndWarmViews(double view)
        {
            var state = Final();
            state.deals.Add(Deal(state, "final-two", DealKind.FinalTwo, state.playerId));
            SetView(state, view);
            var commitment = EndgameCommitments.Read(state, state.hohId, Finalists(state)).Single();
            Assert.That(commitment.Strength, Is.EqualTo(25));
            Assert.That(commitment.HasFinalTwo, Is.True);
            Assert.That(commitment.EvidenceIds, Is.EqualTo(new[] { "final-two" }));
        }

        [TestCase(DealKind.FinalTwo, DealStatus.Active, 25)]
        [TestCase(DealKind.FinalTwo, DealStatus.Fulfilled, 0)]
        [TestCase(DealKind.FinalTwo, DealStatus.Broken, 0)]
        [TestCase(DealKind.FinalTwo, DealStatus.Expired, 0)]
        [TestCase(DealKind.FinalTwo, DealStatus.Proposed, 0)]
        [TestCase(DealKind.FinalTwo, DealStatus.Accepted, 0)]
        [TestCase(DealKind.FinalTwo, DealStatus.Declined, 0)]
        [TestCase(DealKind.FinalThree, DealStatus.Fulfilled, 12.5)]
        [TestCase(DealKind.FinalThree, DealStatus.Active, 0)]
        [TestCase(DealKind.FinalThree, DealStatus.Broken, 0)]
        [TestCase(DealKind.FinalThree, DealStatus.Expired, 0)]
        [TestCase(DealKind.FinalThree, DealStatus.Proposed, 0)]
        [TestCase(DealKind.FinalThree, DealStatus.Accepted, 0)]
        [TestCase(DealKind.FinalThree, DealStatus.Declined, 0)]
        public void FormalDealStatusDeterminesApplicability(string kind, string status, double expected)
        {
            var state = Final(); SetView(state, -80);
            var deal = Deal(state, "status-probe", kind, state.playerId); deal.status = status;
            state.deals.Add(deal);
            Assert.That(EndgameCommitments.Read(state, state.hohId, Finalists(state)).Sum(value => value.Strength), Is.EqualTo(expected));
            SetView(state, 80);
            Assert.That(EndgameCommitments.Read(state, state.hohId, Finalists(state)).Sum(value => value.Strength), Is.EqualTo(expected), "Warmth cannot revive an inapplicable record or amplify a kept one.");
        }

        [TestCase(Negotiation.Remind, false, 25)]
        [TestCase(Negotiation.Demand, false, 37.5)]
        [TestCase(Negotiation.Threaten, false, 50)]
        [TestCase(Negotiation.Remind, true, 37.5)]
        [TestCase(Negotiation.Demand, true, 56.25)]
        [TestCase(Negotiation.Threaten, true, 75)]
        public void CalledPromisesKeepTheirExistingHoldAndLoyalWordMultipliers(string approach, bool loyal, double expected)
        {
            var state = Final(); SetView(state, -80);
            if (loyal) state.Find(state.hohId).traits.Add("Loyal");
            state.promises.Add(Promise(state, "called"));
            Hold(state, approach);
            Assert.That(EndgameCommitments.Read(state, state.hohId, Finalists(state)).Single().Strength, Is.EqualTo(expected));
            Assert.That(EpisodeEngine.FinalTwoTerms(state, state.hohId, Finalists(state)).Single().evidenceIds, Is.EqualTo(new[] { "promise:called" }));
        }

        [TestCase(false)] [TestCase(true)]
        public void SneakyRemovesObligationEvenWithALoyalTraitAndAStrongHeldPromise(bool alsoLoyal)
        {
            var state = Final();
            state.Find(state.hohId).traits.Add("Sneaky");
            if (alsoLoyal) state.Find(state.hohId).traits.Add("Loyal");
            state.deals.Add(Deal(state, "final-two", DealKind.FinalTwo, state.playerId));
            state.promises.Add(Promise(state, "called")); Hold(state, Negotiation.Threaten);
            Assert.That(EndgameCommitments.Read(state, state.hohId, Finalists(state)), Is.Empty);
        }

        [Test]
        public void DuplicateAndOverlappingCommitmentsAreCappedAndRetainAllSupportingEvidence()
        {
            var state = Final();
            state.deals.Add(Deal(state, "f2-b", DealKind.FinalTwo, state.playerId));
            state.deals.Add(Deal(state, "f2-a", DealKind.FinalTwo, state.playerId));
            state.deals.Add(Deal(state, "kept-f3", DealKind.FinalThree, state.playerId, DealStatus.Fulfilled));
            state.promises.Add(Promise(state, "called-b"));
            state.promises.Add(Promise(state, "called-a")); Hold(state, Negotiation.Threaten);
            var commitment = EndgameCommitments.Read(state, state.hohId, Finalists(state)).Single();
            Assert.That(commitment.Strength, Is.EqualTo(50), "The strongest held promise counts once, rather than adding every commitment.");
            Assert.That(commitment.EvidenceIds, Is.EqualTo(new[] { "f2-a", "f2-b", "kept-f3", "promise:called-a", "promise:called-b" }));
            state.deals.Reverse(); state.promises.Reverse();
            var reordered = EndgameCommitments.Read(state, state.hohId, Finalists(state)).Single();
            Assert.That(reordered.Strength, Is.EqualTo(commitment.Strength));
            Assert.That(reordered.EvidenceIds, Is.EqualTo(commitment.EvidenceIds), "Evidence order is independent of record order.");
            var combined = EpisodeEngine.FinalTwoTerms(state, state.hohId, Finalists(state))
                .Concat(EpisodeEngine.FinalChoiceTerms(state, state.hohId, Finalists(state))).Where(term => term.code == "obligation").ToList();
            Assert.That(combined, Has.Count.EqualTo(1), "The two existing helper paths emit the capped obligation only once.");
            Assert.That(combined.Single().value, Is.EqualTo(50));
            Assert.That(combined.Single().evidenceIds, Is.EqualTo(commitment.EvidenceIds));
        }

        [Test]
        public void OpposingCommitmentsAreCappedIndependentlyForEachFinalist()
        {
            var state = Final(); string other = Finalists(state).Single(id => id != state.playerId);
            state.deals.Add(Deal(state, "player-f2", DealKind.FinalTwo, state.playerId));
            state.deals.Add(Deal(state, "other-f2", DealKind.FinalTwo, other));
            state.deals.Add(Deal(state, "other-f3", DealKind.FinalThree, other, DealStatus.Fulfilled));
            var terms = EndgameCommitments.Read(state, state.hohId, Finalists(state));
            Assert.That(terms, Has.Count.EqualTo(2));
            Assert.That(terms.Single(term => term.FinalistId == state.playerId).Strength, Is.EqualTo(25));
            Assert.That(terms.Single(term => term.FinalistId == other).Strength, Is.EqualTo(25));
            Assert.That(terms.Single(term => term.FinalistId == other).EvidenceIds, Is.EquivalentTo(new[] { "other-f2", "other-f3" }));
            var evaluated = EpisodeEngine.FinalChoice(state);
            foreach (string id in Finalists(state))
                Assert.That(evaluated.nomineeEvaluations.Single(score => score.nomineeId == id).factors.Where(factor => factor.code == "obligation").Sum(factor => factor.value), Is.EqualTo(25));
        }

        [Test]
        public void AnInapplicableStrongerRecordDoesNotDisplaceTheKeptFinalThree()
        {
            var state = Final();
            state.deals.Add(Deal(state, "ended-f2", DealKind.FinalTwo, state.playerId, DealStatus.Expired));
            state.deals.Add(Deal(state, "kept-f3", DealKind.FinalThree, state.playerId, DealStatus.Fulfilled));
            state.promises.Add(Promise(state, "uncalled"));
            var commitment = EndgameCommitments.Read(state, state.hohId, Finalists(state)).Single();
            Assert.That(commitment.Strength, Is.EqualTo(12.5));
            Assert.That(commitment.HasFinalTwo, Is.False);
            Assert.That(commitment.EvidenceIds, Is.EqualTo(new[] { "kept-f3" }));
            Assert.That(EpisodeEngine.FinalTwoTerms(state, state.hohId, Finalists(state)), Is.Empty);
            Assert.That(EpisodeEngine.FinalChoiceTerms(state, state.hohId, Finalists(state)).Single(term => term.code == "obligation").value, Is.EqualTo(12.5));
        }

        [TestCase(PromiseStatus.Fulfilled)] [TestCase(PromiseStatus.Broken)] [TestCase(PromiseStatus.Expired)]
        public void ACalledPromiseWhoseFormalStatusEndedCountsForNothing(PromiseStatus status)
        {
            var state = Final(); var promise = Promise(state, "ended");
            promise.status = status; state.promises.Add(promise); Hold(state, Negotiation.Threaten);
            Assert.That(EndgameCommitments.Read(state, state.hohId, Finalists(state)), Is.Empty);
        }

        [TestCase(ContestantStatus.Jury)] [TestCase(ContestantStatus.Evicted)]
        public void ACommitmentToSomeoneWhoHasLeftIsNoFinalChoice(ContestantStatus status)
        {
            var state = Final(); state.deals.Add(Deal(state, "left-f2", DealKind.FinalTwo, state.playerId));
            state.Find(state.playerId).status = status;
            Assert.That(EndgameCommitments.Read(state, state.hohId, new[] { state.playerId, state.hohId, "not-in-cast" }), Is.Empty);
        }

        [Test]
        public void PromiseDirectionKindAndCallingAfterItsCreationRemainRequired()
        {
            var state = Final(); var promise = Promise(state, "direction"); state.promises.Add(promise);
            RelationshipLedger.RecordOneWay(state, state.hohId, state.playerId, Negotiation.HeldType(PromiseKind.FinalTwo, Negotiation.Threaten), 0, "Called in.");
            // The stored receipt is older than this promise, so it cannot have called this promise in.
            var edge = state.relationships.Single(relation => relation.fromId == state.hohId && relation.toId == state.playerId);
            edge.events.Last().week = promise.week - 1;
            Assert.That(EndgameCommitments.Read(state, state.hohId, Finalists(state)), Is.Empty);
            edge.events.Last().week = promise.week;
            Assert.That(EndgameCommitments.Read(state, state.hohId, Finalists(state)).Single().Strength, Is.EqualTo(50));
            promise.fromId = state.playerId; promise.toId = state.hohId;
            Assert.That(EndgameCommitments.Read(state, state.hohId, Finalists(state)), Is.Empty, "The finalist's promise does not bind the chooser.");
            promise.fromId = state.hohId; promise.toId = state.playerId; promise.kind = PromiseKind.Safety;
            Assert.That(EndgameCommitments.Read(state, state.hohId, Finalists(state)), Is.Empty);
        }

        [Test]
        public void ReadersAndChoiceArePureAndReturnedEvidenceDoesNotAliasState()
        {
            var state = Final(); state.deals.Add(Deal(state, "final-two", DealKind.FinalTwo, state.playerId));
            string before = JsonConvert.SerializeObject(state); uint random = state.randomState; int sequence = state.nextSequence;
            var read = EndgameCommitments.Read(state, state.hohId, Finalists(state)).Single();
            var projected = read.ToObligation(); projected.evidenceIds.Clear();
            Assert.That(read.EvidenceIds, Is.EqualTo(new[] { "final-two" }));
            EpisodeEngine.FinalTwoTerms(state, state.hohId, Finalists(state));
            EpisodeEngine.FinalChoiceTerms(state, state.hohId, Finalists(state)); EpisodeEngine.FinalChoice(state);
            Assert.That(JsonConvert.SerializeObject(state), Is.EqualTo(before));
            Assert.That(state.randomState, Is.EqualTo(random)); Assert.That(state.nextSequence, Is.EqualTo(sequence));
        }

        [Test]
        public void LegacyRulesAndThePlayersOwnChoiceReceiveNoAutomaticCommitment()
        {
            var state = Final(false); state.deals.Add(Deal(state, "final-two", DealKind.FinalTwo, state.playerId));
            Assert.That(EndgameCommitments.Read(state, state.hohId, Finalists(state)), Is.Empty);
            Assert.That(EpisodeEngine.FinalTwoTerms(state, state.hohId, Finalists(state)), Is.Empty);
            var own = Final(); own.hohId = own.playerId;
            Assert.That(EndgameCommitments.Read(own, own.playerId, Finalists(own)), Is.Empty);
        }

        private static EpisodeState Final(bool rules = true)
        {
            var state = ContentCatalog.Create(337); state.week = 4; state.phase = EpisodePhase.FinalEviction;
            foreach (var person in state.contestants.Skip(3)) person.status = ContestantStatus.Jury;
            state.hohId = state.contestants[1].id; state.finalPart1WinnerId = state.hohId; state.finalPart2WinnerId = state.contestants[2].id;
            state.Find(state.hohId).traits.Clear();
            if (rules) EpisodeEngine.EnableCommitments(state);
            return state;
        }
        private static string[] Finalists(EpisodeState state) => state.Active.Where(person => person.id != state.hohId).Select(person => person.id).ToArray();
        private static DealState Deal(EpisodeState state, string id, string kind, string partner, string status = DealStatus.Active) => new DealState
        { id = id, type = kind, proposerId = state.hohId, recipientId = partner, status = status, week = state.week, expiresWeek = 0,
            settledWeek = status == DealStatus.Fulfilled ? state.week : 0, trustImpact = DealKind.DefaultTrust(kind) };
        private static PromiseState Promise(EpisodeState state, string id) => new PromiseState
        { id = id, fromId = state.hohId, toId = state.playerId, kind = PromiseKind.FinalTwo, status = PromiseStatus.Active, week = state.week };
        private static void Hold(EpisodeState state, string approach) => RelationshipLedger.RecordOneWay(state, state.hohId, state.playerId,
            Negotiation.HeldType(PromiseKind.FinalTwo, approach), 0, "Called in.");
        private static void SetView(EpisodeState state, double view) => state.relationships.Single(edge => edge.fromId == state.hohId && edge.toId == state.playerId).score = view;
    }
}
