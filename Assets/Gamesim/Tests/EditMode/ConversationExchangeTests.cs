using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class ConversationExchangeTests
    {
        private static EpisodeCommand Command(EpisodeState state, EpisodeCommandKind kind,
            string npc = null, string about = null, string text = null) => new EpisodeCommand
        {
            id = "exchange-" + state.revision + "-" + kind, actorId = state.playerId,
            expectedPhase = state.phase, expectedRevision = state.revision, kind = kind,
            targetId = npc ?? state.Active.First(person => !person.isPlayer).id,
            secondTargetId = about, text = text,
        };

        private static EpisodeState Accepted(EpisodeState before, EpisodeCommand command)
        {
            var result = new EpisodeEngine(before).Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }

        private static List<EpisodeEvent> NewEvents(EpisodeState before, EpisodeState after) =>
            after.events.Where(item => item.sequence >= before.nextSequence).ToList();

        private static ConversationExchange Exchange(EpisodeState before, EpisodeState after, EpisodeCommand command) =>
            ConversationExchange.Create(before, after, command, NewEvents(before, after));

        private static EpisodeState Campaign()
        {
            var state = EconomyRulesTests.Fresh();
            state.phase = EpisodePhase.Campaign; state.hohId = state.playerId;
            state.nominees = state.Active.Where(person => !person.isPlayer).Reverse().Take(2).Select(person => person.id).ToList();
            state.vetoHolderId = state.playerId;
            state.vetoPlayers = state.Active.Take(EpisodeEngine.VetoPlayerCount(state.Active.Count())).Select(person => person.id).ToList();
            state.vetoResolved = true;
            return state;
        }

        [TestCase(EpisodeCommandKind.Talk)]
        [TestCase(EpisodeCommandKind.SmallTalk)]
        [TestCase(EpisodeCommandKind.PersonalChat)]
        [TestCase(EpisodeCommandKind.StrategicDiscussion)]
        [TestCase(EpisodeCommandKind.RelationshipBuilding)]
        [TestCase(EpisodeCommandKind.ShareSecret)]
        [TestCase(EpisodeCommandKind.DiscussGame)]
        public void AcceptedSocialChoicesHaveAuthoredSpeechAndKeepNarrationSeparate(EpisodeCommandKind kind)
        {
            var before = ContentCatalog.Create(412);
            var command = Command(before, kind);
            var after = Accepted(before, command);
            var exchange = Exchange(before, after, command);
            Assert.That(exchange, Is.Not.Null, kind.ToString());
            Assert.That(exchange.PlayerId, Is.EqualTo(before.playerId));
            Assert.That(exchange.NpcId, Is.EqualTo(command.targetId));
            Assert.That(exchange.PlayerLine, Is.Not.Empty);
            Assert.That(exchange.NpcLine, Is.Not.Empty);
            Assert.That(exchange.NpcLine, Does.Not.StartWith("You "));
            Assert.That(exchange.NpcLine, Is.Not.EqualTo(exchange.OutcomeLine));
            Assert.That(NewEvents(before, after).Any(item => item.text == exchange.OutcomeLine), Is.True,
                "The separate recap reports the command's recorded outcome.");
        }

        [TestCase(-40, "I don't trust ")]
        [TestCase(0, "I'm still figuring ")]
        [TestCase(40, "I'm solid with ")]
        public void AimedIntelVoicesOnlyTheOpinionActuallyDisclosed(double score, string expected)
        {
            var before = Campaign();
            string npc = before.Active.First(person => !person.isPlayer && !before.nominees.Contains(person.id)).id;
            string about = before.nominees[0];
            before.Find(npc).name = "Morgan. They Rivers";
            before.Find(about).name = "Ari O'Neil. Jr";
            var relationship = before.relationships.FirstOrDefault(edge => edge.fromId == npc && edge.toId == about);
            if (relationship == null)
            {
                relationship = new RelationshipState { fromId = npc, toId = about };
                before.relationships.Add(relationship);
            }
            relationship.score = score;
            var command = Command(before, EpisodeCommandKind.AskForIntel, npc, about);
            var after = Accepted(before, command);
            var exchange = Exchange(before, after, command);
            Assert.That(exchange, Is.Not.Null);
            Assert.That(exchange.PlayerLine, Does.Contain(before.Find(about).name));
            Assert.That(exchange.NpcLine, Does.StartWith(expected));
            Assert.That(exchange.NpcLine, Does.Contain(before.Find(about).name));
            Assert.That(exchange.OutcomeLine, Does.Contain("stated view"));
            Assert.That(exchange.NpcLine, Does.Not.Contain("vote"));
            Assert.That(exchange.NpcLine, Does.Not.Contain("They is"));

            // Once an answer is recorded, subsequent hidden facts cannot rewrite it.
            var changed = after.Clone();
            foreach (var edge in changed.relationships) edge.score = -score - 50;
            changed.Find(npc).motive = "A private plan that was never said";
            changed.Find(npc).mood = "Angry";
            changed.memories.Add(new MemoryState { ownerId = npc, subjectId = about, text = "Secret ballot plan", isPrivate = true, week = changed.week });
            var same = Exchange(before, changed, command);
            Assert.That(same.NpcLine, Is.EqualTo(exchange.NpcLine));
            Assert.That(same.OutcomeLine, Is.EqualTo(exchange.OutcomeLine));
            Assert.That(same.NpcLine, Does.Not.Contain("Secret ballot"));
        }

        [Test]
        public void LegacyIntelUsesTheRecordedRandomSubjectRatherThanAnIgnoredSecondTarget()
        {
            var before = ContentCatalog.Create(412);
            var command = Command(before, EpisodeCommandKind.AskForIntel, about: "ignored-by-legacy-rules");
            var after = Accepted(before, command);
            var exchange = Exchange(before, after, command);
            Assert.That(exchange, Is.Not.Null);
            Assert.That(exchange.PlayerLine, Is.EqualTo("What have you been hearing around the house?"));
            Assert.That(exchange.NpcLine, Does.Contain(after.Find(after.ledger.standings.Last().toId).name));
            Assert.That(exchange.NpcLine, Does.Not.Contain("ignored-by-legacy-rules"));
        }

        [Test]
        public void RejectedDuplicateWrongSessionAndStaleOriginsProduceNoAcceptedDialogue()
        {
            var before = ContentCatalog.Create(412);
            var command = Command(before, EpisodeCommandKind.SmallTalk);
            var after = Accepted(before, command);
            Assert.That(Exchange(before, before, command), Is.Null);
            Assert.That(Exchange(after, after, command), Is.Null);
            var wrongSlot = after.Clone(); wrongSlot.sessionId = "another-slot";
            Assert.That(Exchange(before, wrongSlot, command), Is.Null);
            var rejected = after.Clone(); rejected.acceptedCommandIds.Remove(command.id);
            Assert.That(Exchange(before, rejected, command), Is.Null);
            command.expectedRevision++;
            Assert.That(Exchange(before, after, command), Is.Null);
        }

        [Test]
        public void OnlyExactCurrentEntitledReceiptsCanSupplyAReply()
        {
            var before = Campaign();
            string npc = before.Active.First(person => !person.isPlayer && !before.nominees.Contains(person.id)).id;
            var command = Command(before, EpisodeCommandKind.AskForIntel, npc, before.nominees[0]);
            var after = Accepted(before, command);
            var events = NewEvents(before, after);
            Assert.That(ConversationExchange.Create(before, after, command, events.Select(item => item.Clone()).ToList()), Is.Not.Null,
                "Cloned projections of the same published events are valid.");
            var information = events.Single(item => item.kind == "information");
            var forged = information.Clone(); forged.text += " A private ballot was revealed.";
            Assert.That(ConversationExchange.Create(before, after, command, new[] { forged }), Is.Null);
            var privateAfter = after.Clone();
            privateAfter.events.Single(item => item.sequence == information.sequence).audienceIds = new List<string> { npc };
            Assert.That(Exchange(before, privateAfter, command), Is.Null);
            var redacted = information.Clone(); redacted.audienceIds = new List<string>();
            Assert.That(ConversationExchange.Create(before, after, command, new[] { redacted }), Is.Null,
                "An input cannot grant public access by changing the event audience.");
            Assert.That(ConversationExchange.Create(before, after, command, before.events), Is.Null);
            Assert.That(ConversationExchange.Create(before, after, command, Array.Empty<EpisodeEvent>()), Is.Null);
        }

        [Test]
        public void OldRecognizableLogsCannotReplayUnderAnUnrelatedNewCommand()
        {
            var original = ContentCatalog.Create(412);
            var first = Command(original, EpisodeCommandKind.SmallTalk);
            var before = Accepted(original, first);
            var next = Command(before, EpisodeCommandKind.PersonalChat);
            var after = Accepted(before, next);
            var old = before.events.Where(item => item.kind == "conversation").ToList();
            Assert.That(ConversationExchange.Create(before, after, next, old), Is.Null);
            var changedKind = Command(before, EpisodeCommandKind.SmallTalk);
            changedKind.id = next.id;
            Assert.That(Exchange(before, after, changedKind), Is.Null,
                "Even a new accepted ID cannot authorize a different action's recorded response.");
        }

        [Test]
        public void IdenticalNamesDoNotLetOneNpcTakeAnotherPersonsPrivateReply()
        {
            var before = ContentCatalog.Create(412);
            var people = before.Active.Where(person => !person.isPlayer).Take(2).ToArray();
            people[0].name = people[1].name = "Morgan Rivers";
            var command = Command(before, EpisodeCommandKind.SmallTalk, people[0].id);
            var after = Accepted(before, command);
            Assert.That(Exchange(before, after, command), Is.Not.Null);
            command.targetId = people[1].id;
            Assert.That(Exchange(before, after, command), Is.Null);
        }

        [Test]
        public void PlaybackConstructionIsDeterministicAndNeverChangesSavedStateOrRandomStreams()
        {
            var before = Campaign();
            string npc = before.Active.First(person => !person.isPlayer && !before.nominees.Contains(person.id)).id;
            var command = Command(before, EpisodeCommandKind.AskForIntel, npc, before.nominees[0]);
            var after = Accepted(before, command);
            string previous = JsonConvert.SerializeObject(before), saved = JsonConvert.SerializeObject(after);
            var first = Exchange(before, after, command);
            for (int i = 0; i < 20; i++)
            {
                var next = Exchange(before, after, command);
                Assert.That((next.PlayerLine, next.NpcLine, next.OutcomeLine), Is.EqualTo((first.PlayerLine, first.NpcLine, first.OutcomeLine)));
            }
            Assert.That(JsonConvert.SerializeObject(before), Is.EqualTo(previous));
            Assert.That(JsonConvert.SerializeObject(after), Is.EqualTo(saved));
        }

        [TestCase(true)] [TestCase(false)]
        public void VentRepliesFollowTheRecordedOutcomeAndNotLaterPrivateOpinions(bool landed)
        {
            EpisodeState before = null, after = null;
            EpisodeCommand command = null;
            for (uint seed = 1; seed < 128; seed++)
            {
                before = ContentCatalog.Create(seed);
                var people = before.Active.Where(person => !person.isPlayer).ToArray();
                command = Command(before, EpisodeCommandKind.VentAbout, people[0].id, people[1].id);
                after = Accepted(before, command);
                if (NewEvents(before, after).Single(item => item.kind == "vent").text.EndsWith("and it landed.", StringComparison.Ordinal) == landed) break;
            }
            var exchange = Exchange(before, after, command);
            Assert.That(exchange, Is.Not.Null);
            Assert.That(exchange.NpcLine, Is.EqualTo(landed ? "I get why that bothered you." : "I don't want to talk about them that way."));
            foreach (var edge in after.relationships) edge.score = 0;
            Assert.That(Exchange(before, after, command).NpcLine, Is.EqualTo(exchange.NpcLine));
        }

        [TestCase(EpisodeCommandKind.PromiseSafety)] [TestCase(EpisodeCommandKind.PromiseFinalTwo)]
        public void AcknowledgementsReuseOnlyRealNewDirectPromises(EpisodeCommandKind kind)
        {
            var before = ContentCatalog.Create(412);
            var command = Command(before, kind);
            var after = Accepted(before, command);
            var exchange = Exchange(before, after, command);
            Assert.That(exchange, Is.Not.Null);
            Assert.That(exchange.NpcLine, Is.EqualTo(HouseDialogue.Response(after, command.targetId, kind)));
            Assert.That(ConversationExchange.Create(before, after, command, before.events), Is.Null);
        }

        [Test]
        public void ProposalUsesTheSpokenReplyAlreadyInItsReceiptAfterHiddenFactsChange()
        {
            var before = ContentCatalog.Create(412); before.dealRulesStartWeek = 1;
            var command = Command(before, EpisodeCommandKind.ProposeDeal, text: DealKind.SafetyAgreement);
            var after = Accepted(before, command);
            var exchange = Exchange(before, after, command);
            Assert.That(exchange, Is.Not.Null);
            Assert.That(NewEvents(before, after).Single(item => item.kind == "deal").text, Does.Contain("“" + exchange.NpcLine + "”"));
            foreach (var edge in after.relationships) edge.score = 100;
            Assert.That(Exchange(before, after, command).NpcLine, Is.EqualTo(exchange.NpcLine));
        }

        [TestCase("accept")] [TestCase("decline")]
        public void IncomingDealsAddressTheirActualProposerAndOnlyTheRecordedAnswer(string answer)
        {
            var before = ContentCatalog.Create(412); before.dealRulesStartWeek = 1;
            string npc = before.Active.First(person => !person.isPlayer).id;
            before.deals.Add(new DealState { id = "exchange-offer", type = DealKind.SafetyAgreement,
                proposerId = npc, recipientId = before.playerId, week = before.week,
                expiresWeek = before.week, status = DealStatus.Proposed, trustImpact = DealTrust.Medium });
            var command = Command(before, EpisodeCommandKind.RespondToDeal, "exchange-offer", text: answer);
            var after = Accepted(before, command);
            var exchange = Exchange(before, after, command);
            Assert.That(exchange, Is.Not.Null);
            Assert.That(exchange.NpcId, Is.EqualTo(npc));
            Assert.That(exchange.PlayerLine, Does.StartWith(answer == "accept" ? "Yes." : "No."));
            Assert.That(exchange.OutcomeLine, Does.Contain(answer == "accept" ? "accepted" : "declined"));
        }

        [Test]
        public void UnsupportedCommandsAndMissingConfigurationHaveNoInventedSpeech()
        {
            var before = ContentCatalog.Create(412);
            var command = Command(before, EpisodeCommandKind.SmallTalk);
            var after = Accepted(before, command);
            command.kind = EpisodeCommandKind.SchemeAgainst;
            Assert.That(Exchange(before, after, command), Is.Null);
            Assert.That(ConversationExchange.Create(null, after, command, after.events), Is.Null);
            Assert.That(ConversationExchange.Create(before, after, null, after.events), Is.Null);
            Assert.That(ConversationExchange.Create(before, after, command, null), Is.Null);
        }
    }
}
