#if GAMESIM_ALLSTAR_CANDIDATE
using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class AlumniConversationTests
    {
        private static EpisodeState Fresh(bool histories = true, string playerTemplate = null, int size = 8, uint seed = 711)
        {
            var choice = new SeasonBuilder.Choice
            {
                Roster = CastTemplates.Roster.AllStars, HouseSize = size,
                PlayerTemplateId = playerTemplate, PlayerHistories = histories,
            };
            var state = SeasonBuilder.Create(choice, seed);
            ShippedRules.ApplyFresh(state, choice);
            Assert.That(EpisodeValidation.TryValidate(state, out string error), Is.True, error);
            return state;
        }

        private static string Person(EpisodeState state, string name) =>
            state.contestants.Single(person => person.name == name).id;

        private static EpisodeCommand Question(EpisodeState state, string npc, string subject, string approach = "curious")
        {
            var history = AlumniHistory.DiscussableFacts(state, npc)
                .Single(pair => AlumniHistory.SubjectId(state, pair, npc) == subject);
            return new EpisodeCommand
            {
                id = "history-conversation-" + state.revision + "-" + approach,
                actorId = state.playerId, expectedRevision = state.revision, expectedPhase = state.phase,
                kind = EpisodeCommandKind.AskForIntel, targetId = npc, secondTargetId = subject,
                text = "history:" + history.FactId + ":" + approach,
            };
        }

        private static List<EpisodeEvent> NewEvents(EpisodeState before, EpisodeState after) =>
            after.events.Where(item => item.sequence >= before.nextSequence).ToList();

        private static ConversationExchange Exchange(EpisodeState before, EpisodeState after, EpisodeCommand command) =>
            ConversationExchange.Create(before, after, command, NewEvents(before, after));

        private static EpisodeState Accepted(EpisodeState before, EpisodeCommand command)
        {
            var result = new EpisodeEngine(before).Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }

        [TestCase("curious", false, "How do you feel about them now?")]
        [TestCase("respectful", false, "I know you have history together.")]
        [TestCase("challenge", false, "Why should this time be any different?")]
        [TestCase("curious", true, "How do you feel about us playing again?")]
        [TestCase("respectful", true, "We both know how this game can go.")]
        [TestCase("challenge", true, "Why should this time be any different?")]
        public void AcceptedQuestionPublishesTheSelectedWordsAndExactlyOneAudibleReply(string approach, bool playJanelle, string selectedWords)
        {
            var before = Fresh(playerTemplate: playJanelle ? "janelle-pierzina" : null);
            string will = Person(before, "Dr. Will Kirby"), janelle = Person(before, "Janelle Pierzina");
            var command = Question(before, will, janelle, approach);
            var after = Accepted(before, command);
            var exchange = Exchange(before, after, command);
            Assert.That(exchange, Is.Not.Null);
            Assert.That(exchange.PlayerLine, Is.EqualTo(selectedWords), "The spoken player turn is the option selected in the UI.");
            Assert.That(exchange.NpcId, Is.EqualTo(will));
            Assert.That(exchange.NpcLine, Is.Not.Empty);
            Assert.That(AlumniHistory.TryReceipt(after, command, out string player, out string reply), Is.True);
            Assert.That(player, Is.EqualTo(exchange.PlayerLine));
            Assert.That(reply, Is.EqualTo(exchange.NpcLine));
            var receipt = NewEvents(before, after).Single(item => item.kind == "history-conversation");
            Assert.That(receipt.text, Is.EqualTo(player + "\n" + reply));
            Assert.That(receipt.audienceIds, Does.Contain(before.playerId).And.Contain(will));
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before) + 1));
            Assert.That(before.Find(will).name, Is.EqualTo("Dr. Will Kirby"), "A speech label never changes the saved identity.");
            Assert.That(exchange.NpcLine, Does.Not.Contain("they is").And.Not.Contain("They is"));
        }

        [TestCase("unknown-fact")]
        [TestCase("unknown-approach")]
        [TestCase("wrong-subject")]
        [TestCase("wrong-speaker")]
        [TestCase("truncated")]
        public void InvalidHistoryRequestRefusesBeforeCostOrRandomness(string fault)
        {
            var before = Fresh();
            string will = Person(before, "Dr. Will Kirby"), janelle = Person(before, "Janelle Pierzina");
            var command = Question(before, will, janelle);
            switch (fault)
            {
                case "unknown-fact": command.text = "history:missing-history:curious"; break;
                case "unknown-approach": command.text = command.text.Replace(":curious", ":invented"); break;
                case "wrong-subject": command.secondTargetId = Person(before, "Derrick Levasseur"); break;
                case "wrong-speaker": command.targetId = Person(before, "Dan Gheesling"); break;
                case "truncated": command.text = "history:"; break;
            }
            AssertRejectedUnchanged(before, command);
        }

        [Test]
        public void HistoriesOffDoesNotAcceptTheSameHistoryCommandAsGenericIntel()
        {
            var enabled = Fresh();
            var command = Question(enabled, Person(enabled, "Dr. Will Kirby"), Person(enabled, "Janelle Pierzina"));
            var disabled = Fresh(false);
            command.expectedRevision = disabled.revision;
            command.expectedPhase = disabled.phase;
            Assert.That(AlumniHistory.DiscussableFacts(disabled, command.targetId), Is.Empty);
            AssertRejectedUnchanged(disabled, command);
        }

        [Test]
        public void ExhaustedWindowRefusesWithoutPublishingOrChargingAHistoryTurn()
        {
            var engine = new EpisodeEngine(Fresh());
            for (int i = 0; i < 20 && EpisodeEngine.SocialActionsSpent(engine.Snapshot) < EpisodeEngine.SocialActionBudget(engine.Snapshot); i++)
            {
                var state = engine.Snapshot;
                var talk = EpisodeEngineTests.Command(state, EpisodeCommandKind.SmallTalk);
                talk.targetId = Person(state, "Dan Gheesling");
                Assert.That(engine.Apply(talk).accepted, Is.True);
            }
            var before = engine.Snapshot;
            Assert.That(EpisodeEngine.SocialActionsSpent(before), Is.GreaterThanOrEqualTo(EpisodeEngine.SocialActionBudget(before)));
            var command = Question(before, Person(before, "Dr. Will Kirby"), Person(before, "Janelle Pierzina"));
            AssertRejectedUnchanged(before, command);
        }

        private static void AssertRejectedUnchanged(EpisodeState before, EpisodeCommand command)
        {
            string snapshot = JsonConvert.SerializeObject(before);
            var result = new EpisodeEngine(before).Apply(command);
            Assert.That(result.accepted, Is.False);
            Assert.That(result.duplicate, Is.False);
            Assert.That(JsonConvert.SerializeObject(result.state), Is.EqualTo(snapshot), "No AP, RNG, receipt or state change survives a refusal.");
            Assert.That(JsonConvert.SerializeObject(before), Is.EqualTo(snapshot));
            Assert.That(Exchange(before, result.state, command), Is.Null);
        }

        [Test]
        public void DuplicateAndStaleCommandsCannotReplayAcceptedHistory()
        {
            var before = Fresh();
            var command = Question(before, Person(before, "Dr. Will Kirby"), Person(before, "Janelle Pierzina"));
            var engine = new EpisodeEngine(before);
            var first = engine.Apply(command);
            Assert.That(first.accepted, Is.True, first.reason);
            string committed = JsonConvert.SerializeObject(first.state);
            var duplicate = engine.Apply(command);
            Assert.That(duplicate.duplicate, Is.True);
            Assert.That(duplicate.accepted, Is.False);
            Assert.That(JsonConvert.SerializeObject(duplicate.state), Is.EqualTo(committed));
            Assert.That(Exchange(first.state, duplicate.state, command), Is.Null);
            command.id += "-stale";
            var stale = engine.Apply(command);
            Assert.That(stale.accepted, Is.False);
            Assert.That(stale.duplicate, Is.False);
            Assert.That(JsonConvert.SerializeObject(stale.state), Is.EqualTo(committed));
            Assert.That(Exchange(before, first.state, command), Is.Null);
        }

        [TestCase("approach")]
        [TestCase("subject")]
        [TestCase("speaker")]
        [TestCase("command-id")]
        [TestCase("session")]
        public void AnotherCommandOrSessionCannotBorrowAHistoryReceipt(string fault)
        {
            var before = Fresh();
            string will = Person(before, "Dr. Will Kirby"), janelle = Person(before, "Janelle Pierzina");
            var original = Question(before, will, janelle);
            var after = Accepted(before, original);
            var forged = Question(before, will, janelle);
            switch (fault)
            {
                case "approach": forged.text = forged.text.Replace(":curious", ":challenge"); break;
                case "subject": forged.secondTargetId = Person(before, "Derrick Levasseur"); break;
                case "speaker": forged.targetId = Person(before, "Dan Gheesling"); break;
                case "command-id": forged.id += "-not-accepted"; break;
                case "session": after.sessionId += "-different-slot"; break;
            }
            Assert.That(Exchange(before, after, forged), Is.Null);
        }

        [Test]
        public void OnlyThePublishedVisibleHistoryEventCanSupplySpeech()
        {
            var before = Fresh();
            var command = Question(before, Person(before, "Dr. Will Kirby"), Person(before, "Janelle Pierzina"));
            var after = Accepted(before, command);
            var events = NewEvents(before, after);
            var receipt = events.Single(item => item.kind == "history-conversation");
            var forged = receipt.Clone();
            forged.text += "\nA hidden agreement was exposed.";
            Assert.That(ConversationExchange.Create(before, after, command, new[] { forged }), Is.Null);
            Assert.That(ConversationExchange.Create(before, after, command, Array.Empty<EpisodeEvent>()), Is.Null);
            Assert.That(ConversationExchange.Create(before, after, command, before.events), Is.Null);
            var redacted = after.Clone();
            redacted.events.Single(item => item.sequence == receipt.sequence).audienceIds = new List<string> { command.targetId };
            Assert.That(Exchange(before, redacted, command), Is.Null);
        }

        [Test]
        public void PlaybackDoesNotReadHiddenCurrentAgreementsOrRewriteTheRecordedAnswer()
        {
            var before = Fresh();
            string derrick = Person(before, "Derrick Levasseur"), cody = Person(before, "Cody Calafiore");
            var command = Question(before, derrick, cody);
            var after = Accepted(before, command);
            var exchange = Exchange(before, after, command);
            Assert.That(exchange, Is.Not.Null);
            var changed = after.Clone();
            const string undisclosed = "An unspoken current-season plan";
            changed.Find(derrick).motive = undisclosed;
            changed.Find(derrick).mood = "Angry";
            foreach (var edge in changed.relationships) edge.score = -150;
            changed.deals.RemoveAll(deal => deal.proposerId == derrick || deal.recipientId == derrick);
            var same = Exchange(before, changed, command);
            Assert.That(same, Is.Not.Null);
            Assert.That((same.PlayerLine, same.NpcLine), Is.EqualTo((exchange.PlayerLine, exchange.NpcLine)));
            Assert.That(same.NpcLine, Does.Not.Contain(undisclosed));
            Assert.That(same.NpcLine.ToLowerInvariant(), Does.Not.Contain("we have a final two").And.Not.Contain("we are in a final two"),
                "A public historical pair does not disclose an unobserved current deal.");
        }

        [Test]
        public void RebuildingHistoryPlaybackLeavesBothSnapshotsAndRandomStreamsUntouched()
        {
            var before = Fresh();
            var command = Question(before, Person(before, "Dr. Will Kirby"), Person(before, "Janelle Pierzina"));
            var after = Accepted(before, command);
            string initial = JsonConvert.SerializeObject(before), saved = JsonConvert.SerializeObject(after);
            var first = Exchange(before, after, command);
            for (int i = 0; i < 20; i++)
            {
                var next = Exchange(before, after, command);
                Assert.That((next.PlayerLine, next.NpcLine, next.OutcomeLine), Is.EqualTo((first.PlayerLine, first.NpcLine, first.OutcomeLine)));
            }
            Assert.That(JsonConvert.SerializeObject(before), Is.EqualTo(initial));
            Assert.That(JsonConvert.SerializeObject(after), Is.EqualTo(saved));
        }

        [TestCase(8)]
        [TestCase(12)]
        [Category("AllStarSeasonSmoke")]
        public void PairedHistoriesOnAndOffSeasonsReachWinnersThroughPublicCommands(int houseSize)
        {
            int scheduled = 0, completed = 0, commands = 0;
            for (uint seed = 1; seed <= 4; seed++)
            {
                string[] cast = null;
                foreach (bool histories in new[] { false, true })
                {
                    scheduled++;
                    var start = Fresh(histories, size: houseSize, seed: seed);
                    var identities = start.contestants.Select(person => person.id + ":" + person.name).ToArray();
                    if (cast == null) cast = identities;
                    else Assert.That(identities, Is.EqualTo(cast), "The paired season has the same cast and seed.");
                    var engine = new EpisodeEngine(start);
                    int count = 0;
                    while (engine.Snapshot.phase != EpisodePhase.Finished && count < 3000)
                    {
                        var state = engine.Snapshot;
                        var command = EpisodeEngineTests.NextCommand(state);
                        // The shared walker predates the offered finale response vocabulary.
                        if (command.kind == EpisodeCommandKind.AnswerJury && EpisodeEngine.FinaleOn(state))
                        {
                            var exchange = state.juryExchanges[state.juryQuestionIndex];
                            if (exchange.finalistId == state.playerId)
                                command.secondTargetId = FinaleQuestions.Offered(exchange.category, exchange.receiptKind).First();
                        }
                        var result = engine.Apply(command);
                        Assert.That(result.accepted, Is.True,
                            "size=" + houseSize + " seed=" + seed + " histories=" + histories + " phase=" + state.phase
                            + " command=" + command.kind + ": " + result.reason);
                        count++;
                    }
                    var final = engine.Snapshot;
                    commands += count;
                    Assert.That(final.phase, Is.EqualTo(EpisodePhase.Finished), "The public walk must finish within its bounded commands.");
                    Assert.That(final.Find(final.winnerId), Is.Not.Null);
                    Assert.That(EpisodeValidation.TryValidate(final, out string error), Is.True, error);
                    completed++;
                    TestContext.WriteLine("All-Star smoke size=" + houseSize + " seed=" + seed + " histories=" + histories
                        + " commands=" + count + " winner=" + final.winnerId);
                }
            }
            Assert.That((scheduled, completed), Is.EqualTo((8, 8)));
            TestContext.WriteLine("All-Star paired smoke size=" + houseSize + ": completed=" + completed + "/" + scheduled
                + "; commands=" + commands + "; this is a completion check, not a win-rate estimate.");
        }
    }
}
#endif
