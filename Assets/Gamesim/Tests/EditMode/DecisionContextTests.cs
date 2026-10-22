using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    public sealed class DecisionContextTests
    {
        [Test]
        public void PrivateNpcInformationCannotChangeCandidateOrKnownEventCopy()
        {
            var state = ContentCatalog.Create(7);
            var guests = state.contestants.Where(c => c.id != state.playerId).Take(2).ToArray();
            var candidate = guests[0];
            state.promises.Add(new PromiseState { id="visible-safety",fromId=state.playerId,toId=candidate.id,
                kind=PromiseKind.Safety,status=PromiseStatus.Active,week=state.week });
            var before = DecisionContext.ForCandidate(state,candidate.id);
            string events = DecisionContext.KnownEventSummary(state);
            foreach (var edge in state.relationships.Where(edge => edge.fromId != state.playerId)) edge.score = -97;
            state.promises.Add(new PromiseState { id="private-final-two",fromId=candidate.id,toId=guests[1].id,
                kind=PromiseKind.FinalTwo,status=PromiseStatus.Active,week=state.week });
            state.events.Add(new EpisodeEvent { sequence=state.nextSequence++,week=state.week,kind="scheme",
                text="PRIVATE INTENTION SENTINEL",audienceIds=new List<string>{candidate.id} });
            string original = JsonUtility.ToJson(state);
            var after = DecisionContext.ForCandidate(state,candidate.id);
            Assert.That(after.Record,Is.EqualTo(before.Record));
            Assert.That(after.Relationship,Is.EqualTo(before.Relationship));
            Assert.That(after.Promises,Is.EqualTo(before.Promises));
            Assert.That(after.Promises,Does.Contain("You promised: safety").And.Not.Contain("final two"));
            Assert.That(DecisionContext.KnownEventSummary(state),Is.EqualTo(events));
            Assert.That(JsonUtility.ToJson(state),Is.EqualTo(original),"Building display facts must not mutate state or advance RNG.");
        }

        [Test]
        public void ComparisonReadsPublicRecordsOutgoingTrustAndDirectPromisesOnly()
        {
            var state=ContentCatalog.Create(9);
            var actor=state.contestants.First(c=>c.id!=state.playerId);
            actor.hohWins=2;actor.vetoWins=1;actor.timesNominated=3;
            state.relationships.Single(edge=>edge.fromId==state.playerId && edge.toId==actor.id).score=31;
            state.relationships.Single(edge=>edge.fromId==actor.id && edge.toId==state.playerId).score=-86;
            state.promises.Add(new PromiseState { id="direct-vote",fromId=actor.id,toId=state.playerId,
                kind=PromiseKind.Vote,status=PromiseStatus.Fulfilled,week=state.week });
            var context=DecisionContext.ForCandidate(state,actor.id);
            Assert.That(context.Record,Is.EqualTo("HoH 2 · Veto 1 · Nominated 3"));
            Assert.That(context.Relationship,Does.StartWith("Your trust: +31").And.Not.Contain("86"));
            // Since vote family V6's review (finding 2) a vote promise's ending reads as the houseguest's notes read it:
            // no ballot of theirs is on this record, so how it ended is not the player's to know (decision 4).
            Assert.That(context.Promises,Does.Contain("Promised to you: a vote · "+KnownBallots.Unresolved));
            Assert.That(context.Character,Is.Not.SameAs(actor));
            context.Character.name="Changed preview copy";
            Assert.That(actor.name,Is.Not.EqualTo("Changed preview copy"));
        }

        [Test]
        public void ParticipantIdentityComesOnlyFromThePresentedEvent()
        {
            var state=ContentCatalog.Create(7);
            var actor=state.contestants.First(c=>c.id!=state.playerId);
            var item=new HouseEventState { involvedIds=new List<string>{actor.id,actor.id,"missing"} };
            Assert.That(DecisionContext.Participants(state,item).Select(p=>p.Character.id),Is.EqualTo(new[]{actor.id}));
            Assert.That(DecisionContext.ForCandidate(state,"missing"),Is.Null);
            Assert.That(DecisionContext.Participants(state,null),Is.Empty);
        }

        /// <summary>
        /// The nominee comparison shows the deals between the player and a candidate beside the
        /// promises (ACTIONS-DEALS-ALLIANCES-PLAN V1), each with its term and where it stands, and
        /// never a deal between two houseguests. With none there is no line, so the comparison is
        /// no taller for a candidate with nothing between you.
        /// </summary>
        [Test]
        public void TheComparisonShowsTheDealsBetweenYouBesideThePromises()
        {
            var state = ContentCatalog.Create(11);
            var guests = state.contestants.Where(c => c.id != state.playerId).Take(3).ToArray();
            Assert.That(DecisionContext.ForCandidate(state, guests[0].id).Deals, Is.Null, "No deal, no line.");
            state.deals.Add(new DealState { id = "deal-safety", type = DealKind.SafetyAgreement, proposerId = state.playerId, recipientId = guests[0].id,
                status = DealStatus.Active, week = state.week, expiresWeek = state.week, trustImpact = DealKind.DefaultTrust(DealKind.SafetyAgreement) });
            state.deals.Add(new DealState { id = "deal-vote", type = DealKind.VoteEvict, proposerId = guests[0].id, recipientId = state.playerId, targetId = guests[2].id,
                status = DealStatus.Proposed, week = state.week, expiresWeek = state.week, trustImpact = DealKind.DefaultTrust(DealKind.VoteEvict) });
            state.deals.Add(new DealState { id = "deal-private", type = DealKind.FinalTwo, proposerId = guests[0].id, recipientId = guests[1].id,
                status = DealStatus.Active, week = state.week, trustImpact = DealKind.DefaultTrust(DealKind.FinalTwo) });
            var context = DecisionContext.ForCandidate(state, guests[0].id);
            Assert.That(context.Deals, Is.EqualTo("Deal: Vote-to-evict deal they offered (" + FinalistRead.FirstName(guests[2].name) + ") · this week · waiting on you\n"
                + "Deal: Safety deal you proposed · this week · agreed"), "Newest first, each with its term and where it stands.");
            Assert.That(context.Promises, Is.EqualTo("No promises recorded between you."), "The promises read as they always did.");
            Assert.That(DecisionContext.ForCandidate(state, guests[1].id).Deals, Is.Null, "A deal between two houseguests is theirs.");

            // A deal that names the player says "you", never the player's own name in the third person.
            state.deals.Add(new DealState { id = "deal-keep-you", type = DealKind.VoteSave, proposerId = guests[2].id, recipientId = state.playerId,
                targetId = state.playerId, status = DealStatus.Active, week = state.week, expiresWeek = state.week, trustImpact = DealKind.DefaultTrust(DealKind.VoteSave) });
            Assert.That(DecisionContext.ForCandidate(state, guests[2].id).Deals, Is.EqualTo("Deal: Vote-to-save deal they offered (you) · this week · agreed"));
        }

        /// <summary>
        /// The comparison tells a houseguest's vote promise's ending as their notes tell it (vote family V6's review, finding 2;
        /// <see cref="KnownBallots.PromiseOutcomeKnown"/>, decision 4): "unresolved" while the ballot that ended it is hidden from
        /// the player, the ending once they know that ballot. Before mode 2 (<see cref="LegacyVoteWord"/>) and in mode 2's flip
        /// pair (<see cref="ModeTwoReaderSweep.Flip"/>): the partner's word, broken by their one ballot, hidden in the blind pair
        /// and told and judged in the control.
        /// </summary>
        [Test]
        public void TheComparisonTellsAVotePromisesEndingOnlyWhereItsBallotIsKnown()
        {
            var legacy = LegacyVoteWord();
            Assert.That(DecisionContext.ForCandidate(legacy.blind, legacy.promiser).Promises, Is.EqualTo("Promised to you: a vote · " + KnownBallots.Unresolved));
            Assert.That(DecisionContext.ForCandidate(legacy.told, legacy.promiser).Promises, Is.EqualTo("Promised to you: a vote · broken"));

            var blind = ModeTwoReaderSweep.Flip(false, word: true);
            foreach (var s in new[] { blind.Kept, blind.Broken })
                Assert.That(DecisionContext.ForCandidate(s, blind.PartnerId).Promises,
                    Does.Contain("Promised to you: a vote · " + KnownBallots.Unresolved).And.Not.Contain("a vote · broken"), "Mode 2, the ballot hidden.");
            var told = ModeTwoReaderSweep.Flip(true, word: true);
            foreach (var s in new[] { told.Kept, told.Broken })
                Assert.That(DecisionContext.ForCandidate(s, told.PartnerId).Promises,
                    Does.Contain("Promised to you: a vote · broken").And.Not.Contain("a vote · " + KnownBallots.Unresolved), "Mode 2, the ballot told and judged.");
        }

        /// <summary>
        /// A season before mode 2 the week after a reveal: a houseguest's vote promise to the player, broken, with their ballot
        /// hidden (<c>blind</c>) and, in a copy, told and caught at the reveal (<c>told</c>) - BallotPrivacyTests' verdict fixture.
        /// </summary>
        internal static (EpisodeState blind, EpisodeState told, string promiser) LegacyVoteWord()
        {
            var s = ContentCatalog.Create(31);
            s.week = 2;
            var npcs = s.contestants.Where(c => !c.isPlayer).ToArray();
            s.ledger.power.Add(new PowerRow { week = 1, hohId = npcs[0].id, evicteeId = npcs[1].id, nominees = new List<string> { npcs[1].id, npcs[2].id }, tally = new List<int> { 2, 1 } });
            s.ledger.ballots.Add(new BallotRow { week = 1, voterId = s.playerId, targetId = npcs[1].id });
            s.promises.Add(new PromiseState { id = "legacy-vote-word", fromId = npcs[3].id, toId = s.playerId, targetId = npcs[1].id,
                kind = PromiseKind.Vote, status = PromiseStatus.Broken, week = 1, expiresWeek = 1 });
            var told = s.Clone();
            told.ledger.claims.Add(new ClaimRow { week = 1, voterId = npcs[3].id, targetId = npcs[1].id, source = ClaimSource.Told, status = ClaimStatus.Lied });
            Assert.That(KnownBallots.Knows(s, 1, npcs[3].id), Is.False, "Fixture: the ballot is hidden.");
            Assert.That(KnownBallots.Knows(told, 1, npcs[3].id), Is.True, "Fixture: the copy's ballot is known.");
            return (s, told, npcs[3].id);
        }
    }
}
