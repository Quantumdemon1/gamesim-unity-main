using System;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Public-command tests of an actual NPC-produced non-Vote incoming offer.
    /// A bounded Mono diagnostic established reachability for all four branches. Execution and
    /// acceptance are recorded separately. These tests do not cover confrontation/gossip.
    /// Only dialogue1 opt-in and ordinary command request nonces are authored. No offer/history
    /// ID, nominee, relationship, RNG, action counter, role or NPC choice is assigned.
    /// </summary>
    public sealed class DialogueCampaignFreeOfferTests
    {
        private const int Seeds = 64, WalkCommands = 96, TalkCommands = 16, NeutralAnswers = 24;
        private EpisodeState paused;
        private string pausedBytes, offerId;
        private int nonce;

        [OneTimeSetUp]
        public void FindARealIncomingNonVoteOfferAtAnActualPaidApZeroPause()
        {
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(fresh);
                Assert.That(fresh.dialogueRulesVersion, Is.Zero, "The pilot remains unpublished.");
                fresh.dialogueRulesVersion = 1;
                Valid(fresh);
                string freshBytes = Json(fresh);
                var engine = new EpisodeEngine(fresh);
                int neutralAnswers = 0;
                for (int step = 0; step < WalkCommands; step++)
                {
                    var s = engine.Snapshot;
                    if (s.week != 1 || s.phase == EpisodePhase.Finished) break;
                    if (s.phase != EpisodePhase.Campaign)
                    { Accepted(engine, ModeTwoReaderSweep.Next(s)); continue; }
                    if (!s.nominees.Contains(s.playerId)) break;
                    CloseOldAsks(engine, ref neutralAnswers);
                    for (int debit = 0; debit < TalkCommands && Remaining(engine.Snapshot) > 1; debit++)
                    {
                        s = engine.Snapshot;
                        var talk = Command(s, EpisodeCommandKind.Talk);
                        talk.targetId = s.nominees.First(id => id != s.playerId);
                        Accepted(engine, talk);
                        CloseOldAsks(engine, ref neutralAnswers);
                    }
                    s = engine.Snapshot;
                    string voter = EpisodeEngine.Voters(s).Where(p => !p.isPlayer).Select(p => p.id)
                        .FirstOrDefault(id => DialogueRules.CanStart(s, id, out _));
                    if (Remaining(s) != 1 || voter == null) break;
                    var start = Command(s, EpisodeCommandKind.StartDialogue);
                    start.targetId = voter; start.secondTargetId = "open-case"; start.text = DialogueCampaignV1.TemplateId;
                    var entered = Accepted(engine, start);
                    Assert.That(Remaining(entered), Is.Zero);
                    Assert.That(entered.unifiedCommitmentRulesVersion, Is.EqualTo(2));
                    Assert.That(entered.allWeekRulesStartWeek, Is.EqualTo(1));
                    Assert.That(DialogueRules.Cycle(entered, DialogueRules.Active(entered)).beatId, Is.EqualTo("case"));
                    foreach (var offer in NpcDeals.Pending(entered).Where(d => !KnownBallots.IsVoteDeal(d.type)))
                    {
                        Assert.That(offer.status, Is.EqualTo(DealStatus.Proposed));
                        Assert.That(offer.recipientId, Is.EqualTo(entered.playerId));
                        Assert.That(offer.id, Does.StartWith(NpcDeals.OfferPrefix), "Use the real weekly producer, not a synthetic row.");
                        var check = Answer(entered, offer.id, true);
                        string before = Json(entered), request = Json(check);
                        var trial = new EpisodeEngine(entered).Apply(check);
                        Assert.That(Json(entered), Is.EqualTo(before));
                        Assert.That(Json(check), Is.EqualTo(request));
                        if (!trial.accepted)
                        {
                            Assert.That(trial.duplicate, Is.False);
                            Assert.That(Json(trial.state), Is.EqualTo(before), "An unavailable acceptance cannot repair the source.");
                            continue;
                        }
                        Valid(trial.state);
                        Assert.That(CommitmentReferences.Deals(trial.state).Single(d => d.id == offer.id).status, Is.EqualTo(DealStatus.Active));
                        paused = entered.Clone(); pausedBytes = Json(paused); offerId = offer.id;
                        Assert.That(Json(fresh), Is.EqualTo(freshBytes));
                        TestContext.Out.WriteLine("Actual free-offer gameplay fixture: seed " + seed + ", revision "
                            + entered.revision + ", offer " + offer.id + ", kind " + offer.type + ", proposer " + offer.proposerId + ".");
                        return;
                    }
                    break;
                }
                Assert.That(Json(fresh), Is.EqualTo(freshBytes));
            }
            Assert.Fail("No actual legal non-Vote weekly offer at a paid AP0 pause in " + Seeds + " seeds x "
                + WalkCommands + " public first-week navigation commands, plus at most " + TalkCommands
                + " real Talk debits and " + NeutralAnswers + " total old-ask answers per seed. No state was forced.");
        }

        [TearDown]
        public void TheSharedPlayedFixtureRemainsUntouched()
        {
            if (paused != null) Assert.That(Json(paused), Is.EqualTo(pausedBytes));
        }

        [TestCase("case", false)]
        [TestCase("case", true)]
        [TestCase("finish-personal", false)]
        [TestCase("finish-personal", true)]
        public void AnActualIncomingNonVoteOfferCanBeAnsweredFreelyAndThePaidPathResumes(string node, bool accept)
        {
            Assert.That(Json(paused), Is.EqualTo(pausedBytes));
            var engine = new EpisodeEngine(paused);
            if (node == "finish-personal") Accepted(engine, Choice(engine.Snapshot, "appeal-personally"));
            var before = engine.Snapshot;
            var active = DialogueRules.Active(before);
            Assert.That(active, Is.Not.Null);
            string episodeId = active.id;
            var cycle = before.storylines.Single(row => row.id == episodeId);
            Assert.That(cycle.beatId, Is.EqualTo(node));
            Assert.That(Remaining(before), Is.Zero);
            var offered = NpcDeals.Pending(before).Single(d => d.id == offerId);
            Assert.That(KnownBallots.IsVoteDeal(offered.type), Is.False,
                "A nominated player cannot receive the NPC producer's VoteSave offer; do not manufacture one.");
            Assert.That(before.Find(offered.proposerId).status, Is.EqualTo(ContestantStatus.Active));
            string dialogueBytes = Json(before.dialogue), cycleBytes = Json(cycle), beforeBytes = Json(before);
            var command = Answer(before, offerId, accept);
            string requestBytes = Json(command);
            var expected = new EpisodeEngine(before).Apply(command);
            Assert.That(expected.accepted, Is.True, expected.reason);
            Assert.That(expected.duplicate, Is.False);
            Valid(expected.state);
            Assert.That(Json(before), Is.EqualTo(beforeBytes));
            Assert.That(Json(command), Is.EqualTo(requestBytes));

            var after = Accepted(engine, command);

            Assert.That(Json(after), Is.EqualTo(Json(expected.state)), "The exact public-command replay must retain every effect and RNG result.");
            Assert.That(after.randomState, Is.EqualTo(expected.state.randomState), "Free answers can legitimately consume the main RNG.");
            Assert.That(after.npcSocial.randomState, Is.EqualTo(expected.state.npcSocial.randomState));
            Assert.That(Json(after.dialogue), Is.EqualTo(dialogueBytes));
            Assert.That(Json(after.storylines.Single(row => row.id == episodeId)), Is.EqualTo(cycleBytes));
            Assert.That(DialogueRules.Active(after)?.id, Is.EqualTo(episodeId));
            AssertFreeCounters(before, after);
            var answered = CommitmentReferences.Deals(after).Single(d => d.id == offerId);
            Assert.That(answered.status, Is.EqualTo(accept ? DealStatus.Active : DealStatus.Declined));
            Assert.That((answered.id, answered.type, answered.proposerId, answered.recipientId, answered.targetId, answered.week),
                Is.EqualTo((offered.id, offered.type, offered.proposerId, offered.recipientId, offered.targetId, offered.week)));
            Assert.That(NpcDeals.Pending(after).Any(d => d.id == offerId), Is.False);
            string committed = Json(after);
            var duplicate = engine.Apply(command);
            Assert.That(duplicate.accepted, Is.False);
            Assert.That(duplicate.duplicate, Is.True);
            Assert.That(Json(duplicate.state), Is.EqualTo(committed));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committed));
            Assert.That(Json(command), Is.EqualTo(requestBytes));

            if (node == "case")
            {
                var middle = Accepted(engine, Choice(engine.Snapshot, "appeal-personally"));
                AssertFreeCounters(before, middle);
                Assert.That(middle.storylines.Single(row => row.id == episodeId).beatId, Is.EqualTo("finish-personal"));
            }
            var finished = Accepted(engine, Choice(engine.Snapshot, "make-plea"));
            AssertFreeCounters(before, finished);
            var closed = finished.dialogue.episodes.Single(e => e.id == episodeId);
            var closedCycle = finished.storylines.Single(row => row.id == episodeId);
            Assert.That(DialogueRules.Active(finished), Is.Null);
            Assert.That(closedCycle.status, Is.EqualTo(StorylineStatus.Completed));
            Assert.That(closedCycle.endingId, Is.EqualTo("answered"));
            Assert.That(closedCycle.path.Select(step => step.optionId), Is.EqualTo(new[] { "open-case", "appeal-personally", "make-plea" }));
            Assert.That(closed.time.reason, Is.EqualTo("answered"));
            Assert.That(new[] { closed.time.proposedDelta, closed.time.appliedDelta, closed.time.spentRevision,
                closed.time.spentSequence, closed.time.spentWindowAction }, Is.All.EqualTo(0));
            var lobby = finished.lobbies.Single(row => row.week == finished.week && row.phase == EpisodePhase.Campaign
                && row.deciderId == closed.npcId && row.ask == LobbyAsk.Vote);
            Assert.That(lobby.approach, Is.EqualTo(LobbyApproach.Emotional));
            Assert.That(closed.responses.Last().responseKey, Is.EqualTo(DialogueCampaignV1.TemplateId + "/vote-" + lobby.response));
            Assert.That(CommitmentReferences.Deals(finished).Single(d => d.id == offerId).status,
                Is.EqualTo(accept ? DealStatus.Active : DealStatus.Declined));
            Assert.That(Json(before), Is.EqualTo(beforeBytes));
            Assert.That(Json(paused), Is.EqualTo(pausedBytes));
            Valid(finished);
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

        private void CloseOldAsks(EpisodeEngine engine, ref int answers)
        {
            for (;;)
            {
                var s = engine.Snapshot;
                var item = s.houseEvents.FirstOrDefault(e => !e.resolved && HouseEventKind.Asks(e.kind));
                if (item == null) return;
                Assert.That(answers++, Is.LessThan(NeutralAnswers), "A single per-seed neutral-answer bound; never clear history.");
                Assert.That(item.contentId == null || !item.contentId.StartsWith(DialogueCampaignV1.TemplateId + ":", StringComparison.Ordinal), Is.True);
                var c = Command(s, item.IsStory ? EpisodeCommandKind.ProgressStoryline : EpisodeCommandKind.ResolveHouseEvent);
                c.targetId = item.id;
                if (item.IsStory)
                {
                    var neutral = item.choices.Single(choice => choice.optionId == item.lapseOptionId);
                    Assert.That(neutral.locked || neutral.costsAction || neutral.pickPerson, Is.False);
                    c.secondTargetId = neutral.optionId;
                }
                else c.text = item.choices.First().label;
                Accepted(engine, c);
            }
        }

        private EpisodeCommand Answer(EpisodeState s, string id, bool accept)
        {
            var c = Command(s, EpisodeCommandKind.RespondToDeal); c.targetId = id;
            c.text = accept ? EpisodeEngine.AcceptDeal : "decline"; return c;
        }

        private EpisodeCommand Choice(EpisodeState s, string option)
        {
            var active = DialogueRules.Active(s);
            Assert.That(active, Is.Not.Null);
            var c = Command(s, EpisodeCommandKind.ChooseDialogue);
            c.targetId = active.id; c.secondTargetId = option;
            c.text = s.storylines.Single(row => row.id == active.id).beatId; return c;
        }

        private EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = "dialogue-free-offer-" + (++nonce), actorId = s.playerId, expectedRevision = s.revision,
            expectedPhase = s.phase, kind = kind,
        };

        private static EpisodeState Accepted(EpisodeEngine engine, EpisodeCommand command)
        {
            var source = engine.Snapshot;
            string before = Json(source), request = Json(command);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + ": " + result.reason);
            Assert.That(result.duplicate, Is.False);
            Assert.That(result.state.revision, Is.EqualTo(source.revision + 1));
            Assert.That(Json(source), Is.EqualTo(before));
            Assert.That(Json(command), Is.EqualTo(request));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(Json(result.state)));
            Valid(result.state); return result.state;
        }

        private static int Remaining(EpisodeState s) => EpisodeEngine.SocialActionBudget(s) - EpisodeEngine.SocialActionsSpent(s);
        private static string Json(object value) => JsonConvert.SerializeObject(value, Formatting.None);
        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
    }
}
