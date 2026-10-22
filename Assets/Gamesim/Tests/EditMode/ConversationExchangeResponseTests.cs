using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>Presentation of the exact words published by the existing plea and vote commands.</summary>
    public sealed class ConversationExchangeResponseTests
    {
        private static EpisodeState Nomination(uint seed = 412)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
            state.randomState = unchecked(seed * 2654435761u);
            EpisodeEngine.EnableWeek(state);
            EpisodeEngine.EnableLevers(state);
            state.phase = EpisodePhase.Nomination;
            state.hohId = state.Active.First(person => !person.isPlayer).id;
            state.nominees.Clear();
            return state;
        }

        private static EpisodeState Meeting(uint seed = 412)
        {
            var state = Nomination(seed);
            var npcs = state.Active.Where(person => !person.isPlayer).Select(person => person.id).ToList();
            state.phase = EpisodePhase.VetoMeeting;
            state.nominees = new List<string> { npcs[1], npcs[2] };
            state.vetoHolderId = npcs[3];
            state.vetoPlayers = new[] { state.hohId, npcs[1], npcs[2], state.vetoHolderId }
                .Concat(state.Active.Select(person => person.id)).Distinct()
                .Take(EpisodeEngine.VetoPlayerCount(state.Active.Count())).ToList();
            state.vetoResolved = false;
            return state;
        }

        private static EpisodeState Campaign(uint seed = 412, bool playerNominated = false)
        {
            var state = Meeting(seed);
            state.phase = EpisodePhase.Campaign;
            state.vetoResolved = true;
            if (playerNominated) state.nominees[0] = state.playerId;
            return state;
        }

        private static string Voter(EpisodeState state) =>
            EpisodeEngine.Voters(state).First(person => !person.isPlayer).id;

        private static EpisodeCommand Command(EpisodeState state, EpisodeCommandKind kind,
            string npc, string subject = null, string text = null) => new EpisodeCommand
        {
            id = "response-exchange-" + state.revision + "-" + kind,
            actorId = state.playerId, expectedRevision = state.revision, expectedPhase = state.phase,
            kind = kind, targetId = npc, secondTargetId = subject, text = text,
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

        [TestCase(LobbyResponse.Receptive, "You're not the one I'm looking at this week.")]
        [TestCase(LobbyResponse.Open, "I'm listening. Convince me it's worth it.")]
        [TestCase(LobbyResponse.Skeptical, "I don't know. I have to think about my own game.")]
        [TestCase(LobbyResponse.Hostile, "Don't push me. You're making this easy for me, and not the way you want.")]
        public void LobbyVoicesItsPublishedResponseWithoutReevaluatingPrivateConditions(string response, string expected)
        {
            EpisodeState before = null, after = null;
            EpisodeCommand command = null;
            for (uint seed = 1; seed <= 128; seed++)
            {
                before = Nomination(seed);
                before.Find(before.hohId).traits = new List<string>();
                command = Command(before, EpisodeCommandKind.Lobby, before.hohId, before.playerId,
                    LobbyAsk.Encode(LobbyAsk.Spare, LobbyApproach.Emotional));
                after = Accepted(before, command);
                if (after.lobbies.Last().response == response) break;
            }
            Assert.That(after.lobbies.Last().response, Is.EqualTo(response), "The bounded seed search must witness this result.");
            var exchange = Exchange(before, after, command);
            Assert.That(exchange, Is.Not.Null);
            Assert.That(exchange.PlayerLine, Is.EqualTo("Please don't put me up. I need this week."));
            Assert.That(exchange.NpcLine, Is.EqualTo(expected));
            Assert.That(NewEvents(before, after).Single(item => item.kind == "lobby").text,
                Does.Contain("“" + expected + "”"));

            // A published response outlives the hidden inputs and the private influence record.
            foreach (var edge in after.relationships) edge.score = 90;
            after.Find(command.targetId).traits = new List<string> { "Confrontational", "Stubborn" };
            after.Find(command.targetId).motive = "An unspoken private plan";
            after.lobbies.Last().response = response == LobbyResponse.Hostile ? LobbyResponse.Receptive : LobbyResponse.Hostile;
            after.lobbies.Last().influence = -90;
            Assert.That(Exchange(before, after, command).NpcLine, Is.EqualTo(expected));
        }

        [TestCase(LobbyApproach.Emotional, " is coming for both of us. Please, put them up.")]
        [TestCase(LobbyApproach.Strategic, " is the smart game move.")]
        [TestCase(LobbyApproach.Deal, " up and I'll protect you next week.")]
        [TestCase(LobbyApproach.Pressure, " has to go up.")]
        public void LobbyPlayerLineKeepsTheChosenPersonAndApproach(string approach, string ending)
        {
            var before = Nomination();
            var subject = before.Active.First(person => !person.isPlayer && person.id != before.hohId);
            subject.name = "Ari O'Neil. Jr";
            before.Find(before.hohId).name = "Morgan. They Rivers";
            var command = Command(before, EpisodeCommandKind.Lobby, before.hohId, subject.id,
                LobbyAsk.Encode(LobbyAsk.Target, approach));
            var after = Accepted(before, command);
            var exchange = Exchange(before, after, command);
            Assert.That(exchange, Is.Not.Null);
            Assert.That(exchange.PlayerLine, Does.Contain(subject.name));
            Assert.That(exchange.PlayerLine, Does.EndWith(ending));
            Assert.That(exchange.OutcomeLine, Does.Contain("“" + exchange.PlayerLine + "”"));
            Assert.That(exchange.OutcomeLine, Does.EndWith("“" + exchange.NpcLine + "”"));
            command.secondTargetId = before.playerId;
            Assert.That(Exchange(before, after, command), Is.Null, "Another person's plea cannot take this receipt.");
        }

        [TestCase("save-self")]
        [TestCase("save-other")]
        [TestCase("keep")]
        [TestCase("replacement-spare")]
        [TestCase("replacement-target")]
        [TestCase("vote")]
        public void VetoReplacementAndCampaignLobbyUseOnlyTheirRecordedTurns(string situation)
        {
            var before = situation == "vote" ? Campaign(playerNominated: true) : Meeting();
            string npc = situation == "vote" ? Voter(before)
                : situation.StartsWith("replacement", StringComparison.Ordinal) ? before.hohId : before.vetoHolderId;
            string ask = situation == "vote" ? LobbyAsk.Vote
                : situation == "keep" ? LobbyAsk.Keep
                : situation == "replacement-spare" ? LobbyAsk.Spare
                : situation == "replacement-target" ? LobbyAsk.Target : LobbyAsk.Save;
            if (situation == "save-self") before.nominees[0] = before.playerId;
            string subject = situation == "keep" ? null
                : situation == "save-self" || situation == "vote" || situation == "replacement-spare" ? before.playerId
                : situation == "save-other" ? before.nominees[0]
                : EpisodeEngine.ReplacementCandidates(before).First(person => !person.isPlayer).id;
            var command = Command(before, EpisodeCommandKind.Lobby, npc, subject,
                LobbyAsk.Encode(ask, LobbyApproach.Strategic));
            var after = Accepted(before, command);
            var exchange = Exchange(before, after, command);
            Assert.That(exchange, Is.Not.Null);
            var receipt = NewEvents(before, after).Single(item => item.kind == "lobby");
            Assert.That(exchange.OutcomeLine, Is.EqualTo(receipt.text));
            Assert.That(receipt.text, Does.Contain("“" + exchange.PlayerLine + "”"));
            Assert.That(receipt.text, Does.EndWith("“" + exchange.NpcLine + "”"));
            Assert.That(exchange.NpcId, Is.EqualTo(npc));
            Assert.That(exchange.IsDramatic, Is.True);
        }

        [Test]
        public void VoteQuestionUsesThePublishedClaimAsALeaningAndPreservesItsFreeCost()
        {
            var before = Campaign(playerNominated: true);
            string npc = Voter(before);
            before.Find(npc).traits = new List<string> { "Loyal" };
            var command = Command(before, EpisodeCommandKind.AskVote, npc);
            var after = Accepted(before, command);
            string stated = after.ledger.claims.Single(row => row.voterId == npc).targetId;
            var exchange = Exchange(before, after, command);
            Assert.That(exchange, Is.Not.Null);
            Assert.That(exchange.PlayerLine, Is.EqualTo("Where's your head at on this week's vote?"));
            Assert.That(exchange.NpcLine, Is.EqualTo("I'm leaning toward evicting "
                + (stated == before.playerId ? "you" : before.Find(stated).name) + "."));
            Assert.That(exchange.OutcomeLine, Is.EqualTo(NewEvents(before, after).Single(item => item.kind == "vote-read").text));
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before)));
            Assert.That(exchange.NpcLine, Does.Not.Contain("promise"));
        }

        [Test]
        public void AFalseVoteClaimDoesNotRevealTheActualBallotOrLaterPrivatePlan()
        {
            EpisodeState before = null, after = null;
            EpisodeCommand command = null;
            string stated = null, actual = null;
            for (uint seed = 1; seed <= 128; seed++)
            {
                before = Campaign(seed);
                string npc = Voter(before);
                before.Find(npc).traits = new List<string> { "Sneaky" };
                actual = EpisodeEngine.ProjectBallot(before, npc).selectedNomineeId;
                command = Command(before, EpisodeCommandKind.AskVote, npc);
                after = Accepted(before, command);
                stated = after.ledger.claims.Single(row => row.voterId == npc).targetId;
                if (stated != actual) break;
            }
            Assert.That(stated, Is.Not.EqualTo(actual), "The test must witness a real lying answer.");
            var exchange = Exchange(before, after, command);
            Assert.That(exchange, Is.Not.Null);
            Assert.That(exchange.NpcLine, Is.EqualTo("I'm leaning toward evicting " + before.Find(stated).name + "."));
            Assert.That(exchange.NpcLine, Does.Not.Contain(before.Find(actual).name));
            foreach (var edge in after.relationships) edge.score = -70;
            after.Find(command.targetId).motive = "Secretly vote for " + actual;
            after.ledger.claims.Single(row => row.voterId == command.targetId).targetId = actual;
            Assert.That(Exchange(before, after, command).NpcLine, Is.EqualTo(exchange.NpcLine),
                "The published stated answer, not a re-projection or mutable row, supplies speech.");
        }

        [Test]
        public void VoteDeflectionSaysOnlyWhatTheVoterActuallySaid()
        {
            var before = Campaign();
            string npc = Voter(before);
            before.Find(npc).traits = new List<string> { "Strategic" };
            before.relationships.Single(edge => edge.fromId == npc && edge.toId == before.playerId).score = 0;
            var command = Command(before, EpisodeCommandKind.AskVote, npc);
            var after = Accepted(before, command);
            var exchange = Exchange(before, after, command);
            Assert.That(exchange, Is.Not.Null);
            Assert.That(exchange.NpcLine, Is.EqualTo("Ask me after."));
            Assert.That(after.ledger.claims, Is.Empty);
            Assert.That(before.nominees.All(id => !exchange.NpcLine.Contains(before.Find(id).name)), Is.True);
            Assert.That(exchange.IsDramatic, Is.False);
        }

        [Test]
        public void DuplicateNomineeNamesLeaveAnAmbiguousClaimAsNarration()
        {
            var before = Campaign();
            foreach (string id in before.nominees) before.Find(id).name = "Same Name";
            string npc = Voter(before);
            before.Find(npc).traits = new List<string> { "Loyal" };
            var command = Command(before, EpisodeCommandKind.AskVote, npc);
            var after = Accepted(before, command);
            Assert.That(NewEvents(before, after).Any(item => item.kind == "vote-read"), Is.True);
            Assert.That(Exchange(before, after, command), Is.Null);
        }

        private static (EpisodeState before, EpisodeState after, EpisodeCommand command) AcceptedCase(EpisodeCommandKind kind)
        {
            var before = kind == EpisodeCommandKind.Lobby ? Nomination() : Campaign();
            string npc = kind == EpisodeCommandKind.Lobby ? before.hohId : Voter(before);
            if (kind == EpisodeCommandKind.AskVote) before.Find(npc).traits = new List<string> { "Loyal" };
            var command = Command(before, kind, npc, kind == EpisodeCommandKind.Lobby ? before.playerId : null,
                kind == EpisodeCommandKind.Lobby ? LobbyAsk.Encode(LobbyAsk.Spare, LobbyApproach.Emotional) : null);
            return (before, Accepted(before, command), command);
        }

        [TestCase(EpisodeCommandKind.Lobby)]
        [TestCase(EpisodeCommandKind.AskVote)]
        public void PrivateForgedOldMissingAndConflictingReceiptsCannotSupplySpeech(EpisodeCommandKind kind)
        {
            var (before, after, command) = AcceptedCase(kind);
            var published = NewEvents(before, after).Single(item => item.kind == (kind == EpisodeCommandKind.Lobby ? "lobby" : "vote-read"));
            Assert.That(ConversationExchange.Create(before, after, command, new[] { published.Clone() }), Is.Not.Null);
            var forged = published.Clone(); forged.text += " An unpublished private thought.";
            Assert.That(ConversationExchange.Create(before, after, command, new[] { forged }), Is.Null);
            var privateAfter = after.Clone();
            privateAfter.events.Single(item => item.sequence == published.sequence).audienceIds = new List<string> { command.targetId };
            Assert.That(Exchange(before, privateAfter, command), Is.Null);
            var wrongNpc = after.Clone();
            wrongNpc.events.Single(item => item.sequence == published.sequence).audienceIds = new List<string> { before.playerId };
            Assert.That(Exchange(before, wrongNpc, command), Is.Null);
            Assert.That(ConversationExchange.Create(before, after, command, before.events), Is.Null);
            Assert.That(ConversationExchange.Create(before, after, command, Array.Empty<EpisodeEvent>()), Is.Null);
            Assert.That(ConversationExchange.Create(before, after, command, new[] { published, published.Clone() }), Is.Null,
                "Ambiguous repeated input cannot silently choose one reply.");
        }

        [TestCase(EpisodeCommandKind.Lobby)]
        [TestCase(EpisodeCommandKind.AskVote)]
        public void RejectedDuplicateStaleAndChangedSessionCannotReplayAReply(EpisodeCommandKind kind)
        {
            var (before, after, command) = AcceptedCase(kind);
            Assert.That(Exchange(before, before, command), Is.Null);
            Assert.That(Exchange(after, after, command), Is.Null);
            var wrongSession = after.Clone(); wrongSession.sessionId += "-other";
            Assert.That(Exchange(before, wrongSession, command), Is.Null);
            var unpublished = after.Clone(); unpublished.acceptedCommandIds.Remove(command.id);
            Assert.That(Exchange(before, unpublished, command), Is.Null);
            var oldPhase = command.expectedPhase; command.expectedPhase = EpisodePhase.Finished;
            Assert.That(Exchange(before, after, command), Is.Null);
            command.expectedPhase = oldPhase; command.expectedRevision++;
            Assert.That(Exchange(before, after, command), Is.Null);
        }

        [TestCase(EpisodeCommandKind.Lobby)]
        [TestCase(EpisodeCommandKind.AskVote)]
        public void ResponseConstructionLeavesBothStatesCommandsEventsAndRandomStreamsUntouched(EpisodeCommandKind kind)
        {
            var (before, after, command) = AcceptedCase(kind);
            var events = NewEvents(before, after).Select(item => item.Clone()).ToList();
            string previous = JsonConvert.SerializeObject(before), saved = JsonConvert.SerializeObject(after);
            string commandJson = JsonConvert.SerializeObject(command), eventJson = JsonConvert.SerializeObject(events);
            var first = ConversationExchange.Create(before, after, command, events);
            Assert.That(first, Is.Not.Null);
            for (int i = 0; i < 12; i++)
            {
                var next = ConversationExchange.Create(before, after, command, events);
                Assert.That((next.PlayerLine, next.NpcLine, next.OutcomeLine),
                    Is.EqualTo((first.PlayerLine, first.NpcLine, first.OutcomeLine)));
            }
            Assert.That(JsonConvert.SerializeObject(before), Is.EqualTo(previous));
            Assert.That(JsonConvert.SerializeObject(after), Is.EqualTo(saved));
            Assert.That(JsonConvert.SerializeObject(command), Is.EqualTo(commandJson));
            Assert.That(JsonConvert.SerializeObject(events), Is.EqualTo(eventJson));
        }

        [Test]
        public void ReadingAPersonRemainsAnObservationAndNeverAnNpcAdmission()
        {
            var before = Campaign();
            var command = Command(before, EpisodeCommandKind.ReadPerson, Voter(before));
            var after = Accepted(before, command);
            Assert.That(Exchange(before, after, command), Is.Null);
            command.kind = EpisodeCommandKind.ProgressStoryline;
            Assert.That(Exchange(before, after, command), Is.Null, "Existing story presentation retains its own authority.");
        }
    }
}
