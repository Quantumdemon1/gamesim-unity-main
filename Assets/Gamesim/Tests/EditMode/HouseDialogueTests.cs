using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class HouseDialogueTests
    {
        private EpisodeState state;
        private const string Maya = ContentCatalog.MayaId;

        [SetUp]
        public void CreateScenario() => state = ContentCatalog.Create(412);

        [Test]
        public void CanonicalCast_HasFiveDistinctAuthoredVoices()
        {
            var npcs = state.contestants.Where(actor => !actor.isPlayer).ToArray();
            var lines = npcs.Select(actor => HouseDialogue.Greeting(state,actor.id)).ToArray();
            Assert.That(lines, Has.Length.EqualTo(5));
            Assert.That(lines.All(line => !string.IsNullOrWhiteSpace(line)), Is.True);
            Assert.That(lines.Distinct().Count(), Is.EqualTo(5));
            Assert.That(HouseDialogue.Greeting(state,Maya), Does.Contain("clear about what we're promising"));
            Assert.That(HouseDialogue.Greeting(state,"taylor-kim"), Does.Contain("fair shot in the yard"));
            Assert.That(HouseDialogue.Greeting(state,"jamie-roberts"), Does.Contain("quiet minute"));
            Assert.That(HouseDialogue.Greeting(state,"casey-wilson"), Does.Contain("who it's for"));
            Assert.That(HouseDialogue.Greeting(state,"riley-johnson"), Does.Contain("which parts are facts"));
        }

        [Test]
        public void VoiceIdentity_UsesSavedIdAndExplicitAliasNeverDisplayedName()
        {
            Assert.That(HouseDialogue.Greeting(state,"maya"), Is.EqualTo(HouseDialogue.Greeting(state,Maya)));
            state.Find(Maya).id = "imported-speaker";
            state.Find("imported-speaker").name = "Maya Hassan";
            var line = HouseDialogue.Greeting(state,"imported-speaker");
            Assert.That(line, Does.StartWith("Let's take a minute and talk about our own game."));
            Assert.That(line, Does.Not.Contain("clear about what we're promising"));
            Assert.That(HouseDialogue.Greeting(state,"maya"), Is.Empty);
        }

        [Test]
        public void TrustTone_UsesOnlyTheSpeakersOwnDirectedRelationship()
        {
            var baseline = HouseDialogue.Greeting(state,Maya);
            state.relationships.Single(edge => edge.fromId == state.playerId && edge.toId == Maya).score = 90;
            state.relationships.Single(edge => edge.fromId == Maya && edge.toId == "jamie-roberts").score = -90;
            Assert.That(HouseDialogue.Greeting(state,Maya), Is.EqualTo(baseline));
            var own = state.relationships.Single(edge => edge.fromId == Maya && edge.toId == state.playerId);
            own.score = 25;
            Assert.That(HouseDialogue.Greeting(state,Maya), Does.Contain("comfortable talking with you"));
            Assert.That(HouseDialogue.Greeting(state,Maya), Does.Not.Contain("25"));
            own.score = -15;
            Assert.That(HouseDialogue.Greeting(state,Maya), Does.Contain("cautious about relying on you"));
        }

        [Test]
        public void OtherActorsSecretsPromisesAndAlliances_DoNotChangeTheSpeakersLine()
        {
            var before = HouseDialogue.Greeting(state,Maya);
            state.promises.Add(Promise("jamie-roberts",PromiseStatus.Broken));
            state.alliances.Add(new AllianceState { members = new List<string> { state.playerId,"jamie-roberts" } });
            state.memories.Add(new MemoryState
            {
                ownerId = "jamie-roberts", subjectId = state.playerId, week = state.week, isPrivate = true,
                text = "We spent time talking in week 1. SECRET_OTHER_ACTOR"
            });
            state.events.Add(new EpisodeEvent
            {
                kind = "private-vote", text = "SECRET_BALLOT", audienceIds = new List<string> { "riley-johnson" }
            });
            Assert.That(HouseDialogue.Greeting(state,Maya), Is.EqualTo(before));
            Assert.That(HouseDialogue.Response(state,Maya,EpisodeCommandKind.Talk), Does.Not.Contain("SECRET"));
            Assert.That(HouseDialogue.Response(state,Maya,EpisodeCommandKind.Talk), Is.EqualTo(HouseDialogue.Response(state,Maya)));
        }

        [Test]
        public void DirectPromiseOutcomes_UseOnlyRecordedResolvedPromises()
        {
            var baseline = HouseDialogue.Greeting(state,Maya);
            var promise = Promise(Maya,PromiseStatus.Broken);
            promise.week = state.week + 1;
            state.promises.Add(promise);
            Assert.That(HouseDialogue.Greeting(state,Maya), Is.EqualTo(baseline));
            promise.week = state.week;
            Assert.That(HouseDialogue.Greeting(state,Maya), Does.Contain("You broke a promise to me"));
            promise.status = PromiseStatus.Fulfilled;
            Assert.That(HouseDialogue.Greeting(state,Maya), Does.Contain("You kept your promise to me"));
            promise.status = PromiseStatus.Active;
            Assert.That(HouseDialogue.Greeting(state,Maya), Is.EqualTo(baseline));
        }

        [Test]
        public void SafetyAcknowledgement_RequiresMatchingCurrentActiveDirectPromise()
        {
            var before = HouseDialogue.Response(state,Maya,EpisodeCommandKind.PromiseSafety);
            var promise = Promise(Maya,PromiseStatus.Active);
            promise.expiresWeek = state.week - 1;
            state.week = 3; promise.week = 1; promise.expiresWeek = 2;
            state.promises.Add(promise);
            Assert.That(HouseDialogue.Response(state,Maya,EpisodeCommandKind.PromiseSafety), Is.EqualTo(before));
            promise.expiresWeek = state.week;
            Assert.That(HouseDialogue.Response(state,Maya,EpisodeCommandKind.PromiseSafety), Does.StartWith("I heard your safety promise"));
            promise.kind = PromiseKind.FinalTwo;
            Assert.That(HouseDialogue.Response(state,Maya,EpisodeCommandKind.PromiseSafety), Is.EqualTo(before));
        }

        [Test]
        public void AllianceAcknowledgement_RequiresBothMembersAndActualActiveStatus()
        {
            var baseline = HouseDialogue.Response(state,Maya,EpisodeCommandKind.FormAlliance);
            var alliance = new AllianceState { active = true, members = new List<string> { state.playerId,"jamie-roberts" } };
            state.alliances.Add(alliance);
            Assert.That(HouseDialogue.Response(state,Maya,EpisodeCommandKind.FormAlliance), Is.EqualTo(baseline));
            alliance.members.Add(Maya);
            Assert.That(HouseDialogue.Response(state,Maya,EpisodeCommandKind.FormAlliance), Does.StartWith("We have an alliance"));
            alliance.active = false;
            Assert.That(HouseDialogue.Response(state,Maya,EpisodeCommandKind.FormAlliance), Is.EqualTo(baseline));
            Assert.That(HouseDialogue.Response(state,Maya,EpisodeCommandKind.LeaveAlliance), Does.StartWith("You've left our alliance"));
        }

        [Test]
        public void SharedInformationAcknowledgement_DoesNotQuoteOrCertifyPrivateMemory()
        {
            var baseline = HouseDialogue.Response(state,Maya,EpisodeCommandKind.ShareInformation);
            var memory = new MemoryState
            {
                ownerId = "jamie-roberts", subjectId = "riley-johnson", isPrivate = true, week = state.week,
                text = "Heard from you: PRIVATE_INFORMATION_MARKER"
            };
            state.memories.Add(memory);
            Assert.That(HouseDialogue.Response(state,Maya,EpisodeCommandKind.ShareInformation), Is.EqualTo(baseline));
            memory.ownerId = Maya;
            var acknowledged = HouseDialogue.Response(state,Maya,EpisodeCommandKind.ShareInformation);
            Assert.That(acknowledged, Does.Contain("separate from what I've witnessed"));
            Assert.That(acknowledged, Does.Not.Contain("PRIVATE_INFORMATION_MARKER"));
        }

        [Test]
        public void CampaignGreeting_UsesPublicNomineeStatusWithoutInventingAVote()
        {
            state.phase = EpisodePhase.Campaign;
            state.nominees.Add(Maya);
            var line = HouseDialogue.Greeting(state,Maya);
            Assert.That(line, Does.Contain("I'm on the block"));
            Assert.That(line, Does.Contain("won't pretend I know your vote"));
            state.phase = EpisodePhase.Veto;
            Assert.That(HouseDialogue.Greeting(state,Maya), Does.Contain("ceremony comes first"));
        }

        [Test]
        public void InvalidOrUnavailableSpeakers_DoNotInventDialogue()
        {
            Assert.That(HouseDialogue.Greeting(null,Maya), Is.Empty);
            Assert.That(HouseDialogue.Greeting(state,null), Is.Empty);
            Assert.That(HouseDialogue.Greeting(state,"unknown"), Is.Empty);
            Assert.That(HouseDialogue.Greeting(state,state.playerId), Is.Empty);
            state.Find(Maya).status = ContestantStatus.Jury;
            Assert.That(HouseDialogue.Response(state,Maya), Is.Empty);
            state.Find(Maya).status = ContestantStatus.Active;
            state.contestants.Remove(state.Find(state.playerId));
            Assert.That(HouseDialogue.Greeting(state,Maya), Is.Empty);
        }

        [Test]
        public void RepeatedDialogue_IsDeterministicAndDoesNotMutateAnySavedField()
        {
            state.promises.Add(Promise(Maya,PromiseStatus.Broken));
            var before = JsonConvert.SerializeObject(state);
            var expected = HouseDialogue.Greeting(state,Maya);
            for (int index = 0; index < 40; index++)
            {
                Assert.That(HouseDialogue.Greeting(state,Maya), Is.EqualTo(expected));
                foreach (var actor in state.contestants)
                {
                    HouseDialogue.Response(state,actor.id);
                    foreach (EpisodeCommandKind action in Enum.GetValues(typeof(EpisodeCommandKind)))
                        HouseDialogue.Response(state,actor.id,action);
                }
            }
            Assert.That(JsonConvert.SerializeObject(state), Is.EqualTo(before));
        }

        private PromiseState Promise(string recipient, PromiseStatus status) => new PromiseState
        {
            id = "dialogue-promise-" + state.promises.Count, fromId = state.playerId, toId = recipient,
            week = state.week, expiresWeek = state.week + 1, kind = PromiseKind.Safety, status = status
        };
    }
}
