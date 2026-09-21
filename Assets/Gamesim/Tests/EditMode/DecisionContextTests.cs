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
            Assert.That(context.Promises,Does.Contain("Promised to you: a vote · fulfilled"));
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
    }
}
