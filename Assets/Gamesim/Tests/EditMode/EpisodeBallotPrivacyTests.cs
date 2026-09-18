using System;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class EpisodeBallotPrivacyTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void PrivatePlayerBallot_DoesNotSettlePromisesOrInformRecipientsBeforeReveal(bool honorPromise)
        {
            var engine = CampaignWithPlayerVote();
            var campaign = engine.Snapshot;
            string recipient = EpisodeEngine.Voters(campaign).First(voter => !voter.isPlayer).id;
            Apply(engine,EpisodeCommandKind.PromiseVote,recipient,campaign.nominees[0]);
            EpisodeEngineTests.OpenTheVote(engine);
            var before = engine.Snapshot;
            var command = Command(before,EpisodeCommandKind.CastVote,before.nominees[honorPromise ? 0 : 1]);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True,result.reason);
            var pending = engine.Snapshot;

            Assert.That(pending.phase, Is.EqualTo(EpisodePhase.Eviction));
            Assert.That(pending.evictionResolved, Is.False);
            Assert.That(pending.votes.Count, Is.EqualTo(1));
            Assert.That(pending.votes[0].voterId, Is.EqualTo(pending.playerId));
            Assert.That(pending.promises.Single().status, Is.EqualTo(PromiseStatus.Active));
            AssertJsonEqual(before.promises,pending.promises);
            AssertJsonEqual(before.memories,pending.memories);
            AssertJsonEqual(before.relationships,pending.relationships);
            AssertJsonEqual(before.relationshipArcs,pending.relationshipArcs);
            AssertJsonEqual(before.loyaltyOaths,pending.loyaltyOaths);
            Assert.That(pending.randomState, Is.EqualTo(before.randomState), "Unrevealed ballots cannot consume witness-reaction randomness.");
            var addedEvents = pending.events.Where(entry => entry.sequence >= before.nextSequence).ToArray();
            Assert.That(addedEvents, Has.Length.EqualTo(1));
            Assert.That(addedEvents[0].kind, Is.EqualTo("private-vote"));
            Assert.That(addedEvents[0].audienceIds, Is.EquivalentTo(new[] { before.playerId }));
            Assert.That(engine.Apply(command).duplicate, Is.True);
            AssertJsonEqual(pending,engine.Snapshot);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EvictionReveal_UsesPreConsequenceNpcChoicesAndSettlesVotingPromiseExactlyOnce(bool honorPromise)
        {
            var engine = CampaignWithPlayerVote();
            var campaign = engine.Snapshot;
            string recipient = EpisodeEngine.Voters(campaign).First(voter => !voter.isPlayer).id;
            Apply(engine,EpisodeCommandKind.PromiseVote,recipient,campaign.nominees[0]);
            EpisodeEngineTests.OpenTheVote(engine);
            Apply(engine,EpisodeCommandKind.CastVote,engine.Snapshot.nominees[honorPromise ? 0 : 1]);
            var pending = engine.Snapshot;
            var expectedNpcVotes = EpisodeEngine.Voters(pending).Where(voter => !voter.isPlayer)
                .ToDictionary(voter => voter.id,voter => WebEvictionVoting.EvaluateNative(pending,voter.id).selectedNomineeId);
            Assert.That(expectedNpcVotes, Has.Count.EqualTo(2), "The legal six-active-cast fixture has three regular voters and cannot tie.");

            // Resuming an authoritative pending snapshot must not infer early consequences.
            engine = new EpisodeEngine(pending);
            var reveal = Command(pending,EpisodeCommandKind.Advance);
            var result = engine.Apply(reveal);
            Assert.That(result.accepted, Is.True,result.reason);
            var after = engine.Snapshot;
            Assert.That(after.evictionResolved, Is.True);
            Assert.That(after.revision, Is.EqualTo(pending.revision + 1));
            foreach (var expected in expectedNpcVotes)
                Assert.That(after.votes.Single(vote => vote.voterId == expected.Key).targetId, Is.EqualTo(expected.Value),
                    "Remaining housemates choose before this round's private-promise consequences become known.");
            Assert.That(after.promises.Single().status, Is.EqualTo(honorPromise ? PromiseStatus.Fulfilled : PromiseStatus.Broken));
            double expectedTrust = WebRules.ClampScore(pending.Score(recipient,pending.playerId)
                + WebRules.PromiseImpact("vote",honorPromise ? "fulfilled" : "broken"));
            Assert.That(after.Score(recipient,after.playerId), Is.EqualTo(expectedTrust));
            Assert.That(after.memories.Count, Is.GreaterThan(pending.memories.Count));
            var revealEvents = after.events.Where(entry => entry.sequence >= pending.nextSequence).ToArray();
            Assert.That(revealEvents.Count(entry => entry.kind == "promise-outcome"), Is.EqualTo(1));
            Assert.That(revealEvents.Count(entry => entry.kind == "eviction"), Is.EqualTo(1));
            Assert.That(revealEvents.Count(entry => entry.kind == "vote-reveal"), Is.EqualTo(3));
            Assert.That(engine.Apply(reveal).duplicate, Is.True);
            AssertJsonEqual(after,engine.Snapshot);
            Apply(engine,EpisodeCommandKind.Advance); // The reveal-to-social transition cannot replay effects.
            AssertJsonEqual(after.promises,engine.Snapshot.promises);
            AssertJsonEqual(after.memories,engine.Snapshot.memories);
            AssertJsonEqual(after.relationships,engine.Snapshot.relationships);
            Assert.That(engine.Snapshot.randomState, Is.EqualTo(after.randomState));
        }

        private static EpisodeEngine CampaignWithPlayerVote()
        {
            for (uint seed = 1; seed <= 30; seed++)
            {
                // These count promises and their settlement exactly, so the houseguests keep
                // their hands still: autonomy would add words nobody in the test gave.
                var opening = ContentCatalog.Create(seed);
                opening.npcSocial.rulesStartWeek = 2;
                var engine = new EpisodeEngine(opening);
                for (int guard = 0; guard < 20; guard++)
                {
                    var state = engine.Snapshot;
                    if (state.phase == EpisodePhase.Campaign)
                    {
                        if (state.Active.Count() == 6 && EpisodeEngine.Voters(state).Any(voter => voter.isPlayer)) return engine;
                        break;
                    }
                    var next = Command(state,EpisodeCommandKind.Advance);
                    if (EpisodeEngine.IsCompetition(state.phase) && !state.competitionResolved && EpisodeEngine.CompetitionPlayers(state).Any(actor => actor.isPlayer))
                    { next.kind = EpisodeCommandKind.Compete; next.performance = .5; }
                    else if (state.phase == EpisodePhase.Nomination && state.nominees.Count == 0 && state.hohId == state.playerId)
                    {
                        var candidates = EpisodeEngine.NominationCandidates(state).Take(2).ToArray();
                        next.kind = EpisodeCommandKind.Nominate; next.targetId = candidates[0].id; next.secondTargetId = candidates[1].id;
                    }
                    else if (state.phase == EpisodePhase.VetoMeeting && !state.vetoResolved
                        && (state.vetoHolderId == state.playerId || state.hohId == state.playerId && EpisodeEngine.NpcVetoSave(state) != null))
                    {
                        next.kind = EpisodeCommandKind.ResolveVeto;
                        var replacement = EpisodeEngine.ReplacementCandidates(state).FirstOrDefault();
                        next.useVeto = replacement != null;
                        next.targetId = next.useVeto ? state.vetoHolderId == state.playerId ? state.nominees[0] : EpisodeEngine.NpcVetoSave(state) : null;
                        next.secondTargetId = replacement?.id;
                    }
                    var result = engine.Apply(next);
                    Assert.That(result.accepted, Is.True,result.reason);
                }
            }
            throw new AssertionException("No bounded first-week player-voter fixture was found.");
        }

        private static EpisodeCommand Command(EpisodeState state,EpisodeCommandKind kind,string target = null,string second = null)
            => new EpisodeCommand { id = "ballot-privacy-" + state.revision, actorId = state.playerId,
                expectedPhase = state.phase, expectedRevision = state.revision, kind = kind, targetId = target, secondTargetId = second };

        private static void Apply(EpisodeEngine engine,EpisodeCommandKind kind,string target = null,string second = null)
        {
            var result = engine.Apply(Command(engine.Snapshot,kind,target,second));
            Assert.That(result.accepted, Is.True,result.reason);
        }

        private static void AssertJsonEqual(object expected,object actual)
            => Assert.That(JsonConvert.SerializeObject(actual), Is.EqualTo(JsonConvert.SerializeObject(expected)));
    }
}
