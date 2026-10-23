using System;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Fixed witnesses observed by the bounded Task-only Mono diagnostic, not forced roll inputs.
    /// Every case plays a fresh mode2/all-week-from-start season through public commands. The only
    /// authored state override is the unpublished dialogue1 opt-in. Execution evidence is recorded
    /// separately; the isolated source-leaf parity suite is separate, and replay is not used here
    /// to claim effect parity. No source relationship, nominee, RNG, AP or history is set.
    /// </summary>
    public sealed class DialogueCampaignEmotionalResponseTests
    {
        private const int WalkCommands = 96, TalkCommands = 16, NeutralAnswers = 24;
        private int nonce;

        [TestCase(1, "alex-chen", LobbyResponse.Receptive, 4)]
        [TestCase(1, "emma-brown", LobbyResponse.Open, 3)]
        [TestCase(6, "jordan-taylor", LobbyResponse.Skeptical, 1)]
        [TestCase(6, "alex-chen", LobbyResponse.Hostile, 4)]
        public void ARealModeTwoPaidTerminalWritesItsActualEmotionalResponseAtZeroAp(
            int seed, string voter, string response, int terminalDraws)
        {
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, (uint)seed);
            ShippedRules.ApplyFresh(fresh);
            Assert.That(fresh.dialogueRulesVersion, Is.Zero, "This test must not publish the pilot.");
            fresh.dialogueRulesVersion = 1;
            string freshBytes = Json(fresh);
            var campaign = PlayFirstCampaign(fresh);
            Assert.That(campaign.unifiedCommitmentRulesVersion, Is.EqualTo(2));
            Assert.That(campaign.allWeekRulesStartWeek, Is.EqualTo(1));
            Assert.That(EpisodeEngine.Voters(campaign).Any(p => p.id == voter && !p.isPlayer), Is.True,
                "The retained actual witness must still have this legal voter; never substitute a role.");
            Assert.That(DialogueRules.CanStart(campaign, voter, out string refusal), Is.True, refusal);
            Assert.That(Remaining(campaign), Is.EqualTo(1));
            string campaignBytes = Json(campaign);
            var engine = new EpisodeEngine(campaign);

            var start = Command(campaign, EpisodeCommandKind.StartDialogue);
            start.targetId = voter; start.text = DialogueCampaignV1.TemplateId; start.secondTargetId = "open-case";
            var entered = Accepted(engine, start, retainCanonicalPrefix: true);
            var episode = DialogueRules.Active(entered);
            Assert.That(episode, Is.Not.Null);
            string episodeId = episode.id;
            Assert.That(episode.npcId, Is.EqualTo(voter));
            Assert.That(episode.entry.kind, Is.EqualTo(DialogueCampaignV1.EntryKind));
            Assert.That(episode.entry.grantId, Is.Null);
            Assert.That(episode.entry.revision, Is.EqualTo(campaign.revision + 1));
            Assert.That(episode.entry.windowActionBefore, Is.EqualTo(campaign.windowActions[Windows.AfterVeto]));
            Assert.That(episode.entry.windowActionAfter, Is.EqualTo(campaign.windowActions[Windows.AfterVeto] + 1));
            Assert.That(EpisodeEngine.SocialActionsSpent(entered), Is.EqualTo(EpisodeEngine.SocialActionsSpent(campaign) + 1));
            Assert.That(entered.outOfPhaseSocialActions, Is.EqualTo(campaign.outOfPhaseSocialActions + 1));
            Assert.That(entered.socialActions, Is.EqualTo(campaign.socialActions));
            for (int window = 0; window < Windows.Count; window++)
                Assert.That(entered.windowActions[window], Is.EqualTo(campaign.windowActions[window]
                    + (window == Windows.AfterVeto ? 1 : 0)));
            Assert.That(Remaining(entered), Is.Zero);

            var middle = Accepted(engine, Choice(entered, "appeal-personally"), retainCanonicalPrefix: true);
            AssertFreeCounters(entered, middle);
            Assert.That(middle.storylines.Single(c => c.id == episodeId).beatId, Is.EqualTo("finish-personal"));
            Assert.That(middle.dialogue.episodes.Single(e => e.id == episodeId).responses.Select(r => r.responseKey),
                Is.EqualTo(new[] { DialogueCampaignV1.TemplateId + "/hear-your-case", DialogueCampaignV1.TemplateId + "/hear-personal-case" }));

            // Read-only calculation of the documented response bands and influence. No current
            // state is assigned from these RNG objects; the old isolated leaf suite owns full
            // consequence parity, while this public all-week path owns actual seam integration.
            var sourceRolls = new SeededRandom(middle.randomState);
            double chance = StrategyRules.Chance(middle, voter, LobbyAsk.Vote, middle.playerId, LobbyApproach.Emotional);
            double roll = sourceRolls.NextDouble() * 100;
            string predicted = roll < chance * .6 ? LobbyResponse.Receptive : roll < chance ? LobbyResponse.Open
                : roll < chance + 20 ? LobbyResponse.Skeptical : LobbyResponse.Hostile;
            Assert.That(predicted, Is.EqualTo(response), "A changed real witness fails instead of forcing a roll.");
            double influence = response == LobbyResponse.Receptive ? 40 + 20 * sourceRolls.NextDouble()
                : response == LobbyResponse.Open ? 15 : response == LobbyResponse.Skeptical ? -5 : -(30 + 20 * sourceRolls.NextDouble());
            var counted = new SeededRandom(middle.randomState);
            for (int draw = 0; draw < terminalDraws; draw++) counted.NextDouble();
            var terminal = Choice(middle, "make-plea");
            string beforeTerminal = Json(middle), terminalRequest = Json(terminal);
            var finished = Accepted(engine, terminal, retainCanonicalPrefix: true);
            AssertFreeCounters(middle, finished);
            Assert.That(finished.randomState, Is.EqualTo(counted.State), "Actual terminal draws are 4/3/1/4, not a self-replay assertion.");
            Assert.That(finished.npcSocial.randomState, Is.EqualTo(middle.npcSocial.randomState),
                "The caught-up source has no new NPC tick on this zero-cost continuation.");
            Assert.That(Json(finished.unifiedCommitments), Is.EqualTo(Json(middle.unifiedCommitments)),
                "The Emotional terminal creates no canonical promise or deal.");
            Assert.That(Json(finished.deals), Is.EqualTo(Json(middle.deals)));
            Assert.That(Json(finished.promises), Is.EqualTo(Json(middle.promises)));
            Assert.That(finished.lobbies.Count, Is.EqualTo(middle.lobbies.Count + 1));
            Assert.That(Json(finished.lobbies.Take(middle.lobbies.Count).ToArray()), Is.EqualTo(Json(middle.lobbies.ToArray())));
            var actualLobby = finished.lobbies.Last();
            Assert.That((actualLobby.week, actualLobby.phase, actualLobby.deciderId, actualLobby.ask,
                actualLobby.subjectId, actualLobby.approach, actualLobby.response),
                Is.EqualTo((middle.week, EpisodePhase.Campaign, voter, LobbyAsk.Vote,
                    middle.playerId, LobbyApproach.Emotional, response)));
            Assert.That(actualLobby.influence, Is.EqualTo(Math.Max(-StrategyRules.MostInfluence,
                Math.Min(StrategyRules.MostInfluence, influence))));

            var closed = finished.dialogue.episodes.Single(e => e.id == episodeId);
            var cycle = finished.storylines.Single(c => c.id == episodeId);
            Assert.That(DialogueRules.Active(finished), Is.Null);
            Assert.That(cycle.status, Is.EqualTo(StorylineStatus.Completed));
            Assert.That(cycle.endingId, Is.EqualTo("answered"));
            Assert.That(cycle.path.Select(p => p.beatId), Is.EqualTo(new[] { "open", "case", "finish-personal" }));
            Assert.That(cycle.path.Select(p => p.optionId), Is.EqualTo(new[] { "open-case", "appeal-personally", "make-plea" }));
            Assert.That(cycle.path.Select(p => p.result), Is.All.EqualTo(StoryResults.Plain));
            Assert.That(finished.houseEvents.Count(e => e.cycleId == episodeId), Is.EqualTo(3));
            Assert.That(closed.responses.Count, Is.EqualTo(3));
            Assert.That((closed.responses.Last().responseKey, closed.responses.Last().speakerId,
                closed.responses.Last().pathIndex, closed.responses.Last().revision),
                Is.EqualTo((DialogueCampaignV1.TemplateId + "/vote-" + response, voter, 2, finished.revision)));
            Assert.That(closed.time.reason, Is.EqualTo("answered"));
            Assert.That(closed.time.revision, Is.EqualTo(finished.revision));
            Assert.That(closed.time.sequence, Is.EqualTo(cycle.path.Last().logSequence));
            Assert.That(new[] { closed.time.proposedDelta, closed.time.appliedDelta, closed.time.spentRevision,
                closed.time.spentSequence, closed.time.spentWindowAction }, Is.All.EqualTo(0));
            Assert.That(finished.dialogue.grants, Is.Empty);

            string committed = Json(finished);
            var duplicate = engine.Apply(terminal);
            Assert.That(duplicate.accepted, Is.False);
            Assert.That(duplicate.duplicate, Is.True);
            Assert.That(Json(duplicate.state), Is.EqualTo(committed));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committed));
            Assert.That(Json(middle), Is.EqualTo(beforeTerminal));
            Assert.That(Json(terminal), Is.EqualTo(terminalRequest));
            Assert.That(Json(campaign), Is.EqualTo(campaignBytes));
            Assert.That(Json(fresh), Is.EqualTo(freshBytes));
            Valid(finished);
            TestContext.Out.WriteLine("Actual Emotional terminal: seed " + seed + ", voter " + voter + ", response "
                + response + ", source revision " + campaign.revision + ", terminal revision " + finished.revision + ".");
        }

        private EpisodeState PlayFirstCampaign(EpisodeState fresh)
        {
            var engine = new EpisodeEngine(fresh);
            int neutralAnswers = 0;
            for (int step = 0; step < WalkCommands; step++)
            {
                var s = engine.Snapshot;
                Assert.That(s.week, Is.EqualTo(1), "The retained witness is first-week only.");
                if (s.phase == EpisodePhase.Campaign)
                {
                    Assert.That(s.nominees, Does.Contain(s.playerId));
                    CloseOldAsks(engine, ref neutralAnswers);
                    for (int debit = 0; debit < TalkCommands && Remaining(engine.Snapshot) > 1; debit++)
                    {
                        s = engine.Snapshot;
                        var talk = Command(s, EpisodeCommandKind.Talk);
                        talk.targetId = s.nominees.First(id => id != s.playerId);
                        Accepted(engine, talk);
                        CloseOldAsks(engine, ref neutralAnswers);
                    }
                    Assert.That(Remaining(engine.Snapshot), Is.EqualTo(1));
                    return engine.Snapshot;
                }
                Accepted(engine, ModeTwoReaderSweep.Next(s));
            }
            Assert.Fail("Actual first-week Campaign was not reached within 96 public commands; no state was repaired.");
            return null;
        }

        private void CloseOldAsks(EpisodeEngine engine, ref int answers)
        {
            for (;;)
            {
                var s = engine.Snapshot;
                var item = s.houseEvents.FirstOrDefault(e => !e.resolved && HouseEventKind.Asks(e.kind));
                if (item == null) return;
                Assert.That(answers++, Is.LessThan(NeutralAnswers));
                Assert.That(item.contentId == null || !item.contentId.StartsWith(DialogueCampaignV1.TemplateId + ":", StringComparison.Ordinal), Is.True);
                var command = Command(s, item.IsStory ? EpisodeCommandKind.ProgressStoryline : EpisodeCommandKind.ResolveHouseEvent);
                command.targetId = item.id;
                if (item.IsStory)
                {
                    var neutral = item.choices.Single(c => c.optionId == item.lapseOptionId);
                    Assert.That(neutral.locked || neutral.costsAction || neutral.pickPerson, Is.False);
                    command.secondTargetId = neutral.optionId;
                }
                else command.text = item.choices.First().label;
                Accepted(engine, command);
            }
        }

        private EpisodeCommand Choice(EpisodeState s, string option)
        {
            var active = DialogueRules.Active(s);
            Assert.That(active, Is.Not.Null);
            var command = Command(s, EpisodeCommandKind.ChooseDialogue);
            command.targetId = active.id; command.secondTargetId = option;
            command.text = s.storylines.Single(c => c.id == active.id).beatId;
            return command;
        }

        private EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = "dialogue-emotional-response-" + (++nonce), actorId = s.playerId,
            expectedRevision = s.revision, expectedPhase = s.phase, kind = kind,
        };

        private static EpisodeState Accepted(EpisodeEngine engine, EpisodeCommand command, bool retainCanonicalPrefix = false)
        {
            var source = engine.Snapshot;
            string before = Json(source), request = Json(command);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + ": " + result.reason);
            Assert.That(result.duplicate, Is.False);
            Assert.That(result.state.revision, Is.EqualTo(source.revision + 1));
            Assert.That(Json(source), Is.EqualTo(before));
            Assert.That(Json(command), Is.EqualTo(request));
            if (retainCanonicalPrefix)
            {
                Assert.That(result.state.unifiedCommitments.Count, Is.GreaterThanOrEqualTo(source.unifiedCommitments.Count));
                Assert.That(Json(result.state.unifiedCommitments.Take(source.unifiedCommitments.Count).ToArray()),
                    Is.EqualTo(Json(source.unifiedCommitments.ToArray())));
            }
            Assert.That(Json(engine.Snapshot), Is.EqualTo(Json(result.state)));
            Valid(result.state);
            return result.state;
        }

        private static void AssertFreeCounters(EpisodeState before, EpisodeState after)
        {
            Assert.That(Remaining(after), Is.Zero);
            Assert.That(after.windowActions, Is.EqualTo(before.windowActions));
            Assert.That(after.socialActions, Is.EqualTo(before.socialActions));
            Assert.That(after.outOfPhaseSocialActions, Is.EqualTo(before.outOfPhaseSocialActions));
            Assert.That(after.boughtActionPoints, Is.EqualTo(before.boughtActionPoints));
            Assert.That(after.moveInExtrasSpent, Is.EqualTo(before.moveInExtrasSpent));
        }

        private static int Remaining(EpisodeState s) => EpisodeEngine.SocialActionBudget(s) - EpisodeEngine.SocialActionsSpent(s);
        private static string Json(object value) => JsonConvert.SerializeObject(value, Formatting.None);
        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
    }
}

