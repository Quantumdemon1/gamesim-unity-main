using System;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Public-command coverage of the unpublished ordinary-funded campaign pilot. The only rule
    /// override is the explicit dialogue opt-in on a fresh ShippedRules season. No nominee, action
    /// counter, RNG state, Story history or NPC decision is fabricated to produce the witness.
    /// </summary>
    public sealed class DialogueCampaignEngineTests
    {
        private const string Template = "dialogue-campaign-v1";
        private EpisodeState campaign;
        private string campaignBytes;
        private string voter;

        [OneTimeSetUp]
        public void FindAnActualFirstWeekCampaign()
        {
            // Search at most 24 first weeks, 96 accepted public commands each. Stop at the first
            // eligible campaign, not at a convenient response roll. Cache only a detached state;
            // every test constructs a new engine, so no test can spend another test's allowance.
            for (uint seed = 1; seed <= 24; seed++)
            {
                var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(fresh);
                Assert.That(fresh.dialogueRulesVersion, Is.Zero, "The fixture must not publish the pilot.");
                fresh.dialogueRulesVersion = 1;
                Valid(fresh);
                var engine = new EpisodeEngine(fresh);
                for (int step = 0; step < 96; step++)
                {
                    var s = engine.Snapshot;
                    if (s.week != 1 || s.phase == EpisodePhase.Finished) break;
                    if (s.phase == EpisodePhase.Campaign)
                    {
                        if (!s.nominees.Contains(s.playerId)) break;
                        CloseExistingAsks(engine);
                        s = engine.Snapshot;
                        var voters = EpisodeEngine.Voters(s).Where(c => !c.isPlayer).ToArray();
                        string eligible = voters.Select(c => c.id)
                            .FirstOrDefault(id => DialogueRules.CanStart(s, id, out _));
                        if (eligible == null || voters.Length < 2 || Remaining(s) < 2) break;
                        campaign = s.Clone();
                        campaignBytes = Json(campaign);
                        voter = eligible;
                        Assert.That(campaign.unifiedCommitmentRulesVersion, Is.EqualTo(2));
                        Assert.That(campaign.unifiedHearingRulesVersion, Is.EqualTo(1));
                        Assert.That(campaign.economyRulesVersion, Is.EqualTo(1));
                        Assert.That(campaign.story.rulesVersion, Is.EqualTo(9));
                        Assert.That(campaign.allWeekRulesStartWeek, Is.EqualTo(1));
                        TestContext.Out.WriteLine("Actual dialogue witness: seed " + seed
                            + ", week 1, revision " + s.revision + ", voter " + voter + ".");
                        return;
                    }
                    var next = ModeTwoReaderSweep.Next(s);
                    Accepted(engine, next);
                }
            }
            Assert.Fail("No actual nominated first-week campaign with two AP and two voters in 24 seeds x 96 commands.");
        }

        [Test]
        public void OnePaidEntryFundsThreeActualStepsIncludingTheFinalPleaAtZeroAp()
        {
            var engine = WithRemaining(1);
            var before = engine.Snapshot;
            int window = EpisodeEngine.Window(before), raw = before.windowActions[window];
            var entered = Accepted(engine, Start(before, voter));
            var episode = entered.dialogue.episodes.Single();
            Assert.That(episode.id, Is.EqualTo("story-" + episode.startedSequence));
            Assert.That(episode.startedRevision, Is.EqualTo(before.revision + 1));
            Assert.That(episode.entry.kind, Is.EqualTo("action"));
            Assert.That(episode.entry.grantId, Is.Null);
            Assert.That(episode.entry.windowActionBefore, Is.EqualTo(raw));
            Assert.That(episode.entry.windowActionAfter, Is.EqualTo(raw + 1));
            Assert.That(entered.windowActions[window], Is.EqualTo(raw + 1));
            Assert.That(entered.outOfPhaseSocialActions, Is.EqualTo(before.outOfPhaseSocialActions + 1));
            Assert.That(Remaining(entered), Is.Zero);
            Assert.That(Cycle(entered, episode.id).path.Select(p => p.optionId), Is.EqualTo(new[] { "open-case" }));
            Assert.That(entered.houseEvents.Count(e => e.cycleId == episode.id), Is.EqualTo(2));

            var middle = Accepted(engine, Choose(entered, episode.id, "case", "appeal-personally"));
            Assert.That(Remaining(middle), Is.Zero);
            Assert.That(middle.windowActions[window], Is.EqualTo(raw + 1));
            Assert.That(middle.outOfPhaseSocialActions, Is.EqualTo(entered.outOfPhaseSocialActions));
            Assert.That(middle.houseEvents.Count(e => e.cycleId == episode.id), Is.EqualTo(3));
            var finished = Accepted(engine, Choose(middle, episode.id, "finish-personal", "make-plea"));
            Assert.That(finished.windowActions[window], Is.EqualTo(raw + 1));
            Assert.That(finished.outOfPhaseSocialActions, Is.EqualTo(entered.outOfPhaseSocialActions));
            Assert.That(Remaining(finished), Is.Zero);
            var cycle = Cycle(finished, episode.id);
            Assert.That(cycle.path.Select(p => p.beatId), Is.EqualTo(new[] { "open", "case", "finish-personal" }));
            Assert.That(cycle.path.Select(p => p.optionId), Is.EqualTo(new[] { "open-case", "appeal-personally", "make-plea" }));
            Assert.That(cycle.path.Select(p => p.result), Is.All.EqualTo(StoryResults.Plain));
            Assert.That(finished.lobbies.Count(l => l.week == before.week && l.deciderId == voter
                && l.ask == LobbyAsk.Vote && l.approach == LobbyApproach.Emotional && l.subjectId == before.playerId), Is.EqualTo(1));
            AssertClosed(finished, episode.id, "answered", "answered", 3);
            Assert.That(finished.dialogue.episodes.Single().responses.Last().responseKey,
                Is.EqualTo(Template + "/vote-" + finished.lobbies.Single(l => l.week == before.week
                    && l.deciderId == voter && l.ask == LobbyAsk.Vote).response));
            Assert.That(Json(campaign), Is.EqualTo(campaignBytes), "The original constructor input remains detached after all three commands.");
        }

        [TestCase("case", "leave", "respectful-exit", 2)]
        [TestCase("finish-personal", "leave", "respectful-exit", 3)]
        [TestCase("finish-personal", "keep-vote-open", "no-promise-made", 3)]
        public void NeutralExitAtZeroApClosesWithoutARefundOrFourthEvent(string node, string option, string response, int steps)
        {
            var engine = OpenAt(node, 1, out string id);
            var before = engine.Snapshot;
            Assert.That(Remaining(before), Is.Zero);
            var after = Accepted(engine, Choose(before, id, node, option));
            Assert.That(after.windowActions, Is.EqualTo(before.windowActions));
            Assert.That(after.outOfPhaseSocialActions, Is.EqualTo(before.outOfPhaseSocialActions));
            Assert.That(Remaining(after), Is.Zero);
            Assert.That(Json(after.lobbies), Is.EqualTo(Json(before.lobbies)));
            Assert.That(Cycle(after, id).path.Last().result, Is.EqualTo(StoryResults.Plain));
            Assert.That(after.dialogue.episodes.Single().responses.Last().responseKey, Is.EqualTo(Template + "/" + response));
            AssertClosed(after, id, "neutral", "neutral", steps);
        }

        [TestCase("case", 2)]
        [TestCase("finish-personal", 3)]
        public void AdvanceCampaignAtZeroApLapsesExactlyTheCurrentPaidNode(string node, int steps)
        {
            var engine = OpenAt(node, 1, out string id);
            var before = engine.Snapshot;
            Assert.That(Remaining(before), Is.Zero);
            var after = Accepted(engine, Command(before, EpisodeCommandKind.Advance));
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.Eviction));
            Assert.That(after.windowActions, Is.EqualTo(before.windowActions));
            Assert.That(after.outOfPhaseSocialActions, Is.EqualTo(before.outOfPhaseSocialActions));
            Assert.That(Cycle(after, id).path.Last().optionId, Is.EqualTo("leave"));
            Assert.That(Cycle(after, id).path.Last().result, Is.EqualTo(StoryResults.Lapsed));
            Assert.That(after.dialogue.episodes.Single().responses.Last().responseKey, Is.EqualTo(Template + "/occasion-closed"));
            AssertClosed(after, id, "lapsed", "occasion-closed", steps);
            Refused(engine, Choose(after, id, node, "leave"));
        }

        [TestCase("case", 2)]
        [TestCase("finish-personal", 3)]
        public void AStandaloneEmotionalPleaToTheSameVoterLapsesThePaidOccasionOnce(string node, int steps)
        {
            var engine = OpenAt(node, 2, out string id);
            var before = engine.Snapshot;
            Assert.That(Remaining(before), Is.EqualTo(1));
            var after = Accepted(engine, Lobby(before, voter));
            Assert.That(after.windowActions[Windows.AfterVeto], Is.EqualTo(before.windowActions[Windows.AfterVeto] + 1));
            Assert.That(Remaining(after), Is.Zero);
            Assert.That(Cycle(after, id).path.Last().result, Is.EqualTo(StoryResults.Lapsed));
            AssertClosed(after, id, "lapsed", "occasion-closed", steps);
            string closed = Json(after.dialogue);
            Refused(engine, Lobby(after, voter));
            Refused(engine, Choose(after, id, node, "leave"));
            Assert.That(Json(engine.Snapshot.dialogue), Is.EqualTo(closed));
        }

        [Test]
        public void AStandalonePleaToAnotherVoterDoesNotCloseThePaidConversation()
        {
            var engine = OpenAt("case", 2, out string id);
            var before = engine.Snapshot;
            string other = EpisodeEngine.Voters(before).First(c => c.id != voter && !c.isPlayer).id;
            string dialogue = Json(before.dialogue), cycle = Json(Cycle(before, id));
            var after = Accepted(engine, Lobby(before, other));
            Assert.That(Json(after.dialogue), Is.EqualTo(dialogue));
            Assert.That(Json(Cycle(after, id)), Is.EqualTo(cycle));
            Assert.That(DialogueRules.Active(after)?.id, Is.EqualTo(id));
            Assert.That(Remaining(after), Is.Zero);
            var closed = Accepted(engine, Choose(after, id, "case", "leave"));
            AssertClosed(closed, id, "neutral", "neutral", 2);
        }

        [Test]
        public void RepeatedEntryNonceDoesNotDebitOrCreateAgainAndNewNonceCannotStartWhileActive()
        {
            var engine = WithRemaining(2);
            var start = Start(engine.Snapshot, voter);
            Accepted(engine, start);
            Duplicate(engine, start);
            Refused(engine, Start(engine.Snapshot, voter));
            string other = EpisodeEngine.Voters(engine.Snapshot).First(c => c.id != voter && !c.isPlayer).id;
            Refused(engine, Start(engine.Snapshot, other));
            Assert.That(engine.Snapshot.dialogue.episodes.Count, Is.EqualTo(1));
        }

        [Test]
        public void CompletedOccasionCannotReplayItsTerminalOrReopenWithANewNonce()
        {
            var engine = OpenAt("finish-personal", 1, out string id);
            var finish = Choose(engine.Snapshot, id, "finish-personal", "make-plea");
            Accepted(engine, finish);
            Duplicate(engine, finish);
            Refused(engine, Choose(engine.Snapshot, id, "finish-personal", "make-plea"));
            var restart = Refused(engine, Start(engine.Snapshot, voter));
            // The completed plea also makes this voter ineligible. Either admission guard may
            // explain the refusal; Refused already proves the entire saved state is unchanged.
            Assert.That(restart.reason, Is.Not.Null.And.Not.Empty);
            Assert.That(engine.Snapshot.dialogue.episodes.Count, Is.EqualTo(1));
        }

        [Test]
        public void ZeroApBeforeEntryRefusesWithoutMintingAStoryOrGrant()
        {
            var engine = WithRemaining(0);
            Assert.That(DialogueRules.CanStart(engine.Snapshot, voter, out _), Is.False);
            Refused(engine, Start(engine.Snapshot, voter));
            Assert.That(engine.Snapshot.dialogue.episodes, Is.Empty);
            Assert.That(engine.Snapshot.dialogue.grants, Is.Empty);
        }

        [Test]
        public void PresentationEntryPlanCapturesTheActualParticipantAndRevisionWithoutSpending()
        {
            var engine = WithRemaining(1);
            var s = engine.Snapshot;
            string before = Json(s);
            Assert.That(DialogueRules.TryBuildEntryCommand(s, voter, out var plan), Is.True);
            Assert.That(plan.kind, Is.EqualTo(EpisodeCommandKind.StartDialogue));
            Assert.That(plan.targetId, Is.EqualTo(voter));
            Assert.That(plan.secondTargetId, Is.EqualTo("open-case"));
            Assert.That(plan.text, Is.EqualTo(Template));
            Assert.That(plan.actorId, Is.EqualTo(s.playerId));
            Assert.That(plan.expectedPhase, Is.EqualTo(s.phase));
            Assert.That(plan.expectedRevision, Is.EqualTo(s.revision));
            Assert.That(plan.id, Is.Null, "Only a submitted durable command allocates its nonce.");
            Assert.That(Json(s), Is.EqualTo(before));
            Assert.That(DialogueRules.TryBuildEntryCommand(WithRemaining(0).Snapshot, voter, out _), Is.False);
        }

        [TestCase("case", "appeal-personally")]
        [TestCase("finish-personal", "make-plea")]
        public void PresentationChoicePlanUsesTheTypedCurrentEpisodeAtZeroAp(string node, string option)
        {
            var engine = OpenAt(node, 1, out string id);
            var s = engine.Snapshot;
            string before = Json(s);
            var item = s.houseEvents.Single(e => e.cycleId == id && !e.resolved);
            Assert.That(Remaining(s), Is.Zero);
            Assert.That(DialogueRules.TryBuildChoiceCommand(s, item.id, option, out var plan), Is.True);
            Assert.That(plan.kind, Is.EqualTo(EpisodeCommandKind.ChooseDialogue));
            Assert.That(plan.targetId, Is.EqualTo(id));
            Assert.That(plan.secondTargetId, Is.EqualTo(option));
            Assert.That(plan.text, Is.EqualTo(node));
            Assert.That(plan.expectedRevision, Is.EqualTo(s.revision));
            Assert.That(DialogueRules.TryBuildChoiceCommand(s, s.dialogue.episodes.Single().entryEventId, option, out _), Is.False,
                "An old resolved entry is not a current control even within the correct family.");
            Assert.That(DialogueRules.TryBuildChoiceCommand(s, item.id, "unknown-choice", out _), Is.False);
            Assert.That(DialogueRules.TryBuildChoiceCommand(s, "other-event", option, out _), Is.False);
            Assert.That(Json(s), Is.EqualTo(before));
            plan.id = "actual-view-choice-" + s.revision;
            Accepted(engine, plan);
            Refused(engine, Choose(engine.Snapshot, id, node, option));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisabledRulesRefuseBothClosedDialogueCommands(bool choose)
        {
            var off = campaign.Clone();
            off.dialogueRulesVersion = 0;
            Valid(off);
            var engine = new EpisodeEngine(off);
            Refused(engine, choose ? Choose(off, "story-missing", "case", "leave") : Start(off, voter));
        }

        [TestCase("template")]
        [TestCase("entry")]
        [TestCase("self")]
        [TestCase("nominee")]
        [TestCase("missing")]
        [TestCase("actor")]
        [TestCase("revision")]
        [TestCase("veto-input")]
        [TestCase("competition-input")]
        public void UnsupportedStartPayloadIsAnAtomicRefusal(string malformed)
        {
            var engine = WithRemaining(1);
            var s = engine.Snapshot;
            var c = Start(s, voter);
            switch (malformed)
            {
                case "template": c.text = "dialogue-conditional-veto-v1"; break;
                case "entry": c.secondTargetId = "make-plea"; break;
                case "self": c.targetId = s.playerId; break;
                case "nominee": c.targetId = s.nominees.First(id => id != s.playerId); break;
                case "missing": c.targetId = "missing-voter"; break;
                case "actor": c.actorId = voter; break;
                case "revision": c.expectedRevision--; break;
                case "veto-input": c.useVeto = true; break;
                case "competition-input": c.performance = 0.5; break;
            }
            Refused(engine, c);
        }

        [TestCase("episode")]
        [TestCase("node")]
        [TestCase("option")]
        [TestCase("category")]
        [TestCase("actor")]
        [TestCase("revision")]
        [TestCase("phase")]
        [TestCase("event-not-episode")]
        [TestCase("veto-input")]
        [TestCase("competition-input")]
        public void UnsupportedOrStaleChoiceCannotDrawSpendOrWrite(string malformed)
        {
            var engine = OpenAt("case", 1, out string id);
            var s = engine.Snapshot;
            var c = Choose(s, id, "case", "appeal-personally");
            switch (malformed)
            {
                case "episode": c.targetId = "story-missing"; break;
                case "node": c.text = "finish-personal"; break;
                case "option": c.secondTargetId = "make-plea"; break;
                case "category": c.text = "Campaign"; break;
                case "actor": c.actorId = voter; break;
                case "revision": c.expectedRevision--; break;
                case "phase": c.expectedPhase = EpisodePhase.Social; break;
                case "event-not-episode": c.targetId = Cycle(s, id).eventId; break;
                case "veto-input": c.useVeto = true; break;
                case "competition-input": c.performance = 0.5; break;
            }
            Refused(engine, c);
        }

        [TestCase(EpisodeCommandKind.ProgressStoryline)]
        [TestCase(EpisodeCommandKind.ResolveHouseEvent)]
        public void OldStoryCommandsCannotBypassThePaidDialogueOwner(EpisodeCommandKind kind)
        {
            var engine = OpenAt("case", 1, out string id);
            var s = engine.Snapshot;
            var c = Command(s, kind);
            c.targetId = Cycle(s, id).eventId;
            c.secondTargetId = "leave";
            c.text = s.houseEvents.Single(e => e.id == c.targetId).choices.Single(o => o.optionId == "leave").label;
            Refused(engine, c);
        }

        private EpisodeEngine WithRemaining(int desired)
        {
            Assert.That(Json(campaign), Is.EqualTo(campaignBytes));
            var engine = new EpisodeEngine(campaign);
            for (int i = 0; i < 16 && Remaining(engine.Snapshot) > desired; i++)
            {
                var s = engine.Snapshot;
                var talk = Command(s, EpisodeCommandKind.Talk);
                talk.targetId = s.nominees.First(id => id != s.playerId);
                Accepted(engine, talk);
                CloseExistingAsks(engine);
            }
            Assert.That(Remaining(engine.Snapshot), Is.EqualTo(desired), "Only actual public Talk commands spent the fixture's AP.");
            Assert.That(Json(campaign), Is.EqualTo(campaignBytes), "The engine may not mutate its constructor input.");
            return engine;
        }

        private EpisodeEngine OpenAt(string node, int entryAp, out string id)
        {
            var engine = WithRemaining(entryAp);
            var entered = Accepted(engine, Start(engine.Snapshot, voter));
            id = entered.dialogue.episodes.Single().id;
            if (node == "finish-personal") Accepted(engine, Choose(entered, id, "case", "appeal-personally"));
            Assert.That(Cycle(engine.Snapshot, id).beatId, Is.EqualTo(node));
            return engine;
        }

        private static void CloseExistingAsks(EpisodeEngine engine)
        {
            for (int i = 0; i < 24; i++)
            {
                var s = engine.Snapshot;
                var item = s.houseEvents.FirstOrDefault(e => !e.resolved && HouseEventKind.Asks(e.kind));
                if (item == null) return;
                Assert.That(item.contentId == null || !item.contentId.StartsWith(Template + ":", StringComparison.Ordinal), Is.True,
                    "Fixture cleanup may answer old asks, never the paid family under test.");
                var c = Command(s, item.IsStory ? EpisodeCommandKind.ProgressStoryline : EpisodeCommandKind.ResolveHouseEvent);
                c.targetId = item.id;
                if (item.IsStory)
                {
                    var lapse = item.choices.Single(o => o.optionId == item.lapseOptionId);
                    Assert.That(lapse.locked || lapse.costsAction || lapse.pickPerson, Is.False, "The authored neutral exit must be freely answerable.");
                    c.secondTargetId = lapse.optionId;
                }
                else c.text = item.choices.First().label;
                Accepted(engine, c);
            }
            Assert.Fail("Old house asks did not close within 24 public neutral answers; no history was cleared.");
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = "dialogue-engine-" + s.revision + "-" + kind,
            actorId = s.playerId, kind = kind, expectedRevision = s.revision, expectedPhase = s.phase,
        };

        private static EpisodeCommand Start(EpisodeState s, string npc)
        {
            var c = Command(s, EpisodeCommandKind.StartDialogue);
            c.targetId = npc; c.secondTargetId = "open-case"; c.text = Template;
            return c;
        }

        private static EpisodeCommand Choose(EpisodeState s, string id, string node, string option)
        {
            var c = Command(s, EpisodeCommandKind.ChooseDialogue);
            c.targetId = id; c.secondTargetId = option; c.text = node;
            return c;
        }

        private static EpisodeCommand Lobby(EpisodeState s, string npc)
        {
            var c = Command(s, EpisodeCommandKind.Lobby);
            c.targetId = npc; c.secondTargetId = s.playerId;
            c.text = LobbyAsk.Encode(LobbyAsk.Vote, LobbyApproach.Emotional);
            return c;
        }

        private static EpisodeState Accepted(EpisodeEngine engine, EpisodeCommand command)
        {
            var source = engine.Snapshot;
            string sourceBytes = Json(source), request = Json(command);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, "week " + source.week + " " + source.phase + " " + command.kind + ": " + result.reason);
            Assert.That(result.duplicate, Is.False);
            Assert.That(Json(source), Is.EqualTo(sourceBytes), "The detached source is not a candidate workspace.");
            Assert.That(Json(command), Is.EqualTo(request), "Command admission must not rewrite the caller's payload.");
            Assert.That(result.state.revision, Is.EqualTo(source.revision + 1));
            Valid(result.state);
            Assert.That(Json(engine.Snapshot), Is.EqualTo(Json(result.state)));
            return result.state;
        }

        private static CommandResult Refused(EpisodeEngine engine, EpisodeCommand command)
        {
            string before = Json(engine.Snapshot), request = Json(command);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.False);
            Assert.That(result.duplicate, Is.False);
            Assert.That(result.reason, Is.Not.Null.And.Not.Empty);
            Assert.That(Json(result.state), Is.EqualTo(before), "A refused payload may not change RNG, AP, IDs or history.");
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before));
            Assert.That(Json(command), Is.EqualTo(request));
            Valid(result.state);
            return result;
        }

        private static void Duplicate(EpisodeEngine engine, EpisodeCommand command)
        {
            string before = Json(engine.Snapshot), request = Json(command);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.False);
            Assert.That(result.duplicate, Is.True);
            Assert.That(Json(result.state), Is.EqualTo(before));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before));
            Assert.That(Json(command), Is.EqualTo(request));
            Valid(result.state);
        }

        private static void AssertClosed(EpisodeState s, string id, string ending, string reason, int steps)
        {
            var cycle = Cycle(s, id);
            var episode = s.dialogue.episodes.Single(e => e.id == id);
            Assert.That(cycle.status, Is.EqualTo(StorylineStatus.Completed));
            Assert.That(cycle.endingId, Is.EqualTo(ending));
            Assert.That(cycle.path.Count, Is.EqualTo(steps));
            Assert.That(s.houseEvents.Count(e => e.cycleId == id), Is.EqualTo(steps));
            Assert.That(s.houseEvents.Where(e => e.cycleId == id).All(e => e.resolved), Is.True);
            Assert.That(DialogueRules.Active(s), Is.Null);
            Assert.That(episode.time, Is.Not.Null);
            Assert.That(episode.time.reason, Is.EqualTo(reason));
            Assert.That(episode.time.revision, Is.EqualTo(s.revision));
            Assert.That(episode.time.sequence, Is.EqualTo(cycle.path.Last().logSequence));
            Assert.That(episode.time.window, Is.EqualTo(Windows.AfterVeto));
            Assert.That(new[] { episode.time.proposedDelta, episode.time.appliedDelta, episode.time.spentRevision,
                episode.time.spentSequence, episode.time.spentWindowAction }, Is.All.EqualTo(0));
            Assert.That(episode.statement, Is.Null);
            Assert.That(episode.evidence, Is.Empty);
            Assert.That(s.dialogue.grants, Is.Empty);
            Valid(s);
        }

        private static StorylineState Cycle(EpisodeState s, string id) => s.storylines.Single(c => c.id == id);
        private static int Remaining(EpisodeState s) => EpisodeEngine.SocialActionBudget(s) - EpisodeEngine.SocialActionsSpent(s);
        private static string Json(object value) => JsonConvert.SerializeObject(value, Formatting.None);
        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
    }
}
