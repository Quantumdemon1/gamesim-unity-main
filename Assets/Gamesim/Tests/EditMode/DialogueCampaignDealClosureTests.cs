using System;
using System.Globalization;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Real public-command Vote Deal witnesses, not forced-roll creator fixtures. Each candidate
    /// starts from a retained first-week Campaign reached through public Apply. Only the fresh
    /// unpublished dialogue opt-in is assigned; no relationship, RNG, nominee, AP or history is
    /// fabricated. Accepted non-landed pleas are checked but never qualify the landed witness.
    /// </summary>
    public sealed class DialogueCampaignDealClosureTests
    {
        [TestCase("case", 2)]
        [TestCase("finish-personal", 3)]
        public void ActualSameVoterDealLapsesThePaidNodeBeforeWritingItsCanonicalVoteRow(string node, int steps)
        {
            int campaigns = 0, attempts = 0, nonLanded = 0, businessRefusals = 0, existingArrangements = 0;
            for (uint seed = 1; seed <= 64; seed++)
            {
                var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(fresh);
                Assert.That(fresh.dialogueRulesVersion, Is.Zero, "The pilot is not enabled for shipping seasons by this test.");
                fresh.dialogueRulesVersion = 1;
                Valid(fresh);
                string freshImage = Json(fresh);
                var walk = new EpisodeEngine(fresh);
                for (int step = 0; step < 96; step++)
                {
                    var state = walk.Snapshot;
                    if (state.week != 1 || state.phase == EpisodePhase.Finished) break;
                    if (state.phase != EpisodePhase.Campaign)
                    { Accepted(walk, ModeTwoReaderSweep.Next(state)); continue; }
                    if (!state.nominees.Contains(state.playerId)) break;
                    CloseOldAsks(walk);
                    var campaign = walk.Snapshot;
                    if (Remaining(campaign) < 2) break;
                    campaigns++;
                    Assert.That(campaign.unifiedCommitmentRulesVersion, Is.EqualTo(2));
                    Assert.That(campaign.allWeekRulesStartWeek, Is.EqualTo(1));
                    Assert.That(campaign.story.rulesVersion, Is.EqualTo(9));
                    string campaignImage = Json(campaign);
                    // Preserve actual cast/voter order. Each voter gets a new engine from the
                    // same source, not another attempt on a mutated or rewound RNG candidate.
                    var voters = EpisodeEngine.Voters(campaign).Where(c => !c.isPlayer)
                        .Select(c => c.id).Where(id => DialogueRules.CanStart(campaign, id, out _)).ToArray();
                    foreach (string voter in voters)
                    {
                        var engine = new EpisodeEngine(campaign);
                        var entered = Accepted(engine, Start(engine.Snapshot, voter));
                        AssertEntry(campaign, entered, voter);
                        if (node == "finish-personal")
                        {
                            var middle = Accepted(engine, Choose(entered, "case", "appeal-personally"));
                            Assert.That(middle.windowActions, Is.EqualTo(entered.windowActions));
                            Assert.That(middle.outOfPhaseSocialActions, Is.EqualTo(entered.outOfPhaseSocialActions));
                            entered = middle;
                        }
                        var before = engine.Snapshot;
                        Assert.That(before.storylines.Single(c => c.id == before.dialogue.episodes.Single().id).beatId, Is.EqualTo(node));
                        // An already-binding VoteSave selects the source's Safety fallback,
                        // which is not this test's new VoteLobby/DealPolicy witness.
                        if (NpcDeals.Between(before, before.playerId, voter).Any(d => d.type == DealKind.VoteSave)
                            || before.deals.Count + before.unifiedCommitments.Count(r => r.sourcePolicy == UnifiedCommitments.DealPolicy)
                                >= NpcDeals.DealCeiling)
                        { existingArrangements++; Assert.That(Json(campaign), Is.EqualTo(campaignImage)); continue; }
                        string businessReason = !StrategyRules.CanLobby(before, voter, LobbyAsk.Vote, before.playerId, out string why)
                            ? why : Remaining(before) <= 0 ? "You have no conversations left this week." : null;
                        var command = DealLobby(before, voter);
                        attempts++;
                        var result = Observe(engine, command);
                        if (!result.accepted)
                        {
                            // Never classify a reader/storage/candidate failure as an unlucky
                            // response. Only an exact pre-existing business gate can be searched past.
                            Assert.That(businessReason, Is.Not.Null, "Unexpected Deal refusal: " + result.reason);
                            Assert.That(result.reason, Is.EqualTo(businessReason));
                            businessRefusals++;
                            TestContext.Out.WriteLine("Deal business refusal: seed " + seed + ", voter " + voter + ": " + result.reason);
                            Assert.That(Json(campaign), Is.EqualTo(campaignImage));
                            continue;
                        }
                        Assert.That(businessReason, Is.Null, "A known business refusal must not be bypassed.");
                        var after = result.state;
                        AssertClosure(before, after, voter, node, steps);
                        var lobby = after.lobbies.Last();
                        var oldIds = before.unifiedCommitments.Select(r => r.id).ToArray();
                        var newRows = after.unifiedCommitments.Where(r => !oldIds.Contains(r.id)
                            && r.kind == UnifiedVoteTogether.Vote && r.sourcePolicy == UnifiedCommitments.DealPolicy
                            && r.origin == UnifiedVoteFamilyValidation.VoteLobby).ToArray();
                        bool landed = lobby.response == LobbyResponse.Receptive || lobby.response == LobbyResponse.Open;
                        if (!landed)
                        {
                            Assert.That(lobby.response, Is.EqualTo(LobbyResponse.Skeptical).Or.EqualTo(LobbyResponse.Hostile));
                            Assert.That(newRows, Is.Empty, "A non-landed response cannot supply the required canonical witness.");
                            nonLanded++;
                            Assert.That(Json(campaign), Is.EqualTo(campaignImage));
                            continue;
                        }
                        Assert.That(newRows, Has.Length.EqualTo(1), "A genuinely landed plea must actually write its canonical Vote deal.");
                        AssertCanonicalWriter(before, after, newRows.Single(), voter, result.beatsFromSequence);
                        AssertClosureCannotRunTwice(engine, command, voter, node);
                        Assert.That(Json(campaign), Is.EqualTo(campaignImage));
                        Assert.That(Json(walk.Snapshot), Is.EqualTo(campaignImage));
                        Assert.That(Json(fresh), Is.EqualTo(freshImage));
                        TestContext.Out.WriteLine("Actual paid/Deal witness: seed " + seed + ", node " + node + ", voter " + voter
                            + ", response " + lobby.response + ", canonical " + newRows.Single().id + "; attempts " + attempts
                            + ", non-landed " + nonLanded + ", business refusals " + businessRefusals + ".");
                        return;
                    }
                    Assert.That(Json(walk.Snapshot), Is.EqualTo(campaignImage));
                    break;
                }
                Assert.That(Json(fresh), Is.EqualTo(freshImage));
            }
            Assert.Fail("No genuine landed VoteLobby/DealPolicy witness at " + node + " within seeds 1..64 x96 public navigation commands,"
                + " <=24 old-ask answers per Campaign and all actual legal voters. Campaigns=" + campaigns + ", attempts=" + attempts
                + ", non-landed=" + nonLanded + ", business refusals=" + businessRefusals + ", existing-arrangement/capacity branches=" + existingArrangements + ".");
        }

        private static void AssertEntry(EpisodeState before, EpisodeState after, string voter)
        {
            Assert.That(before.dialogue.episodes, Is.Empty);
            var episode = after.dialogue.episodes.Single();
            Assert.That(episode.npcId, Is.EqualTo(voter));
            Assert.That(episode.entry.kind, Is.EqualTo("action"));
            Assert.That(episode.entry.grantId, Is.Null);
            Assert.That(episode.entry.revision, Is.EqualTo(before.revision + 1));
            Assert.That(episode.entry.windowActionBefore, Is.EqualTo(before.windowActions[Windows.AfterVeto]));
            Assert.That(episode.entry.windowActionAfter, Is.EqualTo(before.windowActions[Windows.AfterVeto] + 1));
            Assert.That(after.windowActions[Windows.AfterVeto], Is.EqualTo(before.windowActions[Windows.AfterVeto] + 1));
            Assert.That(after.outOfPhaseSocialActions, Is.EqualTo(before.outOfPhaseSocialActions + 1));
            Assert.That(Json(after.lobbies), Is.EqualTo(Json(before.lobbies)), "Entry is not already a terminal plea.");
            NoRawVote(after);
        }

        private static void AssertClosure(EpisodeState before, EpisodeState after, string voter, string node, int steps)
        {
            var original = before.dialogue.episodes.Single();
            var episode = after.dialogue.episodes.Single();
            var priorCycle = before.storylines.Single(c => c.id == original.id);
            var cycle = after.storylines.Single(c => c.id == original.id);
            Assert.That(original.time, Is.Null);
            Assert.That(episode.id, Is.EqualTo(original.id));
            Assert.That(DialogueRules.Active(after), Is.Null);
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(after.windowActions[Windows.AfterVeto], Is.EqualTo(before.windowActions[Windows.AfterVeto] + 1));
            Assert.That(after.outOfPhaseSocialActions, Is.EqualTo(before.outOfPhaseSocialActions + 1));
            foreach (int window in Enumerable.Range(0, before.windowActions.Count).Where(w => w != Windows.AfterVeto))
                Assert.That(after.windowActions[window], Is.EqualTo(before.windowActions[window]));
            Assert.That(Remaining(after), Is.EqualTo(Remaining(before) - 1));
            Assert.That(Json(episode.entry), Is.EqualTo(Json(original.entry)));
            Assert.That(cycle.path.Count, Is.EqualTo(steps));
            Assert.That(Json(cycle.path.Take(steps - 1)), Is.EqualTo(Json(priorCycle.path)));
            var step = cycle.path.Last();
            Assert.That((step.beatId, step.optionId, step.result), Is.EqualTo((node, "leave", StoryResults.Lapsed)));
            Assert.That((cycle.status, cycle.endingId, cycle.endedWeek), Is.EqualTo((StorylineStatus.Completed, "lapsed", before.week)));
            Assert.That(cycle.eventId, Is.EqualTo(priorCycle.eventId));
            Assert.That(after.houseEvents.Count(e => e.cycleId == episode.id), Is.EqualTo(steps));
            var item = after.houseEvents.Single(e => e.id == priorCycle.eventId);
            Assert.That(item.resolved, Is.True);
            Assert.That(item.choices[item.chosenIndex].optionId, Is.EqualTo("leave"));
            Assert.That(item.outcome, Is.EqualTo(DialogueCampaignV1.LapsedOutcome));
            Assert.That(episode.responses.Count, Is.EqualTo(original.responses.Count + 1));
            Assert.That(Json(episode.responses.Take(original.responses.Count)), Is.EqualTo(Json(original.responses)));
            var response = episode.responses.Last();
            Assert.That((response.eventId, response.speakerId, response.responseKey, response.pathIndex, response.revision),
                Is.EqualTo((item.id, voter, "dialogue-campaign-v1/occasion-closed", steps - 1, before.revision + 1)));
            Assert.That(episode.time, Is.Not.Null);
            Assert.That((episode.time.reason, episode.time.revision, episode.time.sequence, episode.time.window),
                Is.EqualTo(("occasion-closed", before.revision + 1, step.logSequence, Windows.AfterVeto)));
            Assert.That(episode.time.proposedDelta, Is.Zero); Assert.That(episode.time.appliedDelta, Is.Zero);
            Assert.That(episode.time.spentRevision, Is.Zero); Assert.That(episode.time.spentSequence, Is.Zero);
            Assert.That(episode.time.spentWindowAction, Is.Zero);
            Assert.That(after.lobbies.Count, Is.EqualTo(before.lobbies.Count + 1));
            Assert.That(Json(after.lobbies.Take(before.lobbies.Count)), Is.EqualTo(Json(before.lobbies)));
            var lobby = after.lobbies.Last();
            Assert.That((lobby.week, lobby.phase, lobby.deciderId, lobby.ask, lobby.subjectId, lobby.approach),
                Is.EqualTo((before.week, EpisodePhase.Campaign, voter, LobbyAsk.Vote, before.playerId, LobbyApproach.Deal)));
            Assert.That(after.lobbies.Count(l => l.week == before.week && l.phase == EpisodePhase.Campaign && l.deciderId == voter), Is.EqualTo(1));
            Assert.That(Json(after.unifiedCommitments.Take(before.unifiedCommitments.Count)), Is.EqualTo(Json(before.unifiedCommitments)),
                "The old canonical prefix remains visible and unchanged through this lobby and its later catch-up.");
            Valid(after); NoRawVote(after);
        }

        private static void AssertCanonicalWriter(EpisodeState before, EpisodeState after, UnifiedCommitmentState row, string voter, int catchUpStarts)
        {
            Assert.That(row.id, Does.StartWith("deal-lobby-"));
            Assert.That((row.kind, row.sourcePolicy, row.origin, row.subtype),
                Is.EqualTo((UnifiedVoteTogether.Vote, UnifiedCommitments.DealPolicy, UnifiedVoteFamilyValidation.VoteLobby, DealKind.VoteSave)));
            Assert.That((row.makerId, row.beneficiaryId, row.targetId), Is.EqualTo((voter, before.playerId, before.playerId)));
            Assert.That(row.reciprocal, Is.True);
            Assert.That((row.status, row.createdWeek, row.expiresWeek, row.voteBindingWeek, row.voteFirstRevealWeek),
                Is.EqualTo((DealStatus.Active, before.week, before.week, before.week, before.week)));
            Assert.That(row.settledWeek, Is.Zero); Assert.That(row.brokenById, Is.Null);
            Assert.That(row.settlementEffectKey, Is.Null); Assert.That(row.linkedCommitmentId, Is.Null);
            Assert.That(row.conditionalVeto, Is.Null);
            int dealSequence = int.Parse(row.id.Substring("deal-lobby-".Length), NumberStyles.None, CultureInfo.InvariantCulture);
            int closureSequence = after.dialogue.episodes.Single().time.sequence;
            Assert.That(closureSequence, Is.GreaterThanOrEqualTo(before.nextSequence));
            Assert.That(dealSequence, Is.GreaterThan(closureSequence), "The actual paid lapse precedes the old source writer's minted deal/log identity.");
            Assert.That(catchUpStarts, Is.GreaterThan(dealSequence), "This is the requested player's old writer, not a later NPC catch-up row.");
            Assert.That(after.events.Single(e => e.sequence == closureSequence).kind, Is.EqualTo(StoryLog.Outcome));
            Assert.That(after.events.Single(e => e.sequence == dealSequence).kind, Is.EqualTo("deal"));
            var pleaLog = after.events.Single(e => e.sequence > closureSequence && e.sequence < dealSequence && e.kind == "lobby");
            Assert.That(pleaLog.audienceIds, Is.EquivalentTo(new[] { before.playerId, voter }));
            Assert.That(after.unifiedCommitments.Count(r => r.id == row.id), Is.EqualTo(1));
            Assert.That(after.unifiedCommitments.FindIndex(r => r.id == row.id), Is.GreaterThanOrEqualTo(before.unifiedCommitments.Count));
            NoRawVote(after); Valid(after);
        }

        private static void AssertClosureCannotRunTwice(EpisodeEngine engine, EpisodeCommand accepted, string voter, string node)
        {
            string before = Json(engine.Snapshot), request = Json(accepted);
            var duplicate = engine.Apply(accepted);
            Assert.That(duplicate.accepted, Is.False); Assert.That(duplicate.duplicate, Is.True);
            Assert.That(duplicate.reason, Is.EqualTo("This command was already committed."));
            Assert.That(Json(duplicate.state), Is.EqualTo(before));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before)); Assert.That(Json(accepted), Is.EqualTo(request));
            var repeat = Observe(engine, DealLobby(engine.Snapshot, voter));
            Assert.That(repeat.accepted, Is.False);
            Assert.That(repeat.reason, Is.EqualTo(engine.Snapshot.Find(voter).name + " has already heard you out."));
            var staleChoice = Observe(engine, Choose(engine.Snapshot, node, "leave"));
            Assert.That(staleChoice.accepted, Is.False);
            Assert.That(staleChoice.reason, Is.EqualTo("That dialogue has already ended."));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before), "Neither replay nor another closure adds a row, receipt, cost, roll or log.");
        }

        private static CommandResult Observe(EpisodeEngine engine, EpisodeCommand command)
        {
            var source = engine.Snapshot; string before = Json(source), request = Json(command);
            var result = engine.Apply(command);
            Assert.That(result.duplicate, Is.False);
            Assert.That(Json(source), Is.EqualTo(before)); Assert.That(Json(command), Is.EqualTo(request));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(Json(result.state)));
            if (result.accepted) Assert.That(result.state.revision, Is.EqualTo(source.revision + 1));
            else
            {
                Assert.That(result.reason, Is.Not.Null.And.Not.Empty);
                Assert.That(Json(result.state), Is.EqualTo(before), "Refusal must preserve the whole source, including RNG, sequence, AP and history.");
            }
            Valid(result.state); return result;
        }

        private static EpisodeState Accepted(EpisodeEngine engine, EpisodeCommand command)
        {
            var result = Observe(engine, command);
            Assert.That(result.accepted, Is.True, command.kind + ": " + result.reason); return result.state;
        }

        private static void CloseOldAsks(EpisodeEngine engine)
        {
            for (int i = 0; i < 24; i++)
            {
                var s = engine.Snapshot;
                var item = s.houseEvents.FirstOrDefault(e => !e.resolved && HouseEventKind.Asks(e.kind));
                if (item == null) return;
                Assert.That(item.contentId == null || !item.contentId.StartsWith("dialogue-campaign-v1:", StringComparison.Ordinal), Is.True);
                var c = Command(s, item.IsStory ? EpisodeCommandKind.ProgressStoryline : EpisodeCommandKind.ResolveHouseEvent);
                c.targetId = item.id;
                if (item.IsStory)
                {
                    var lapse = item.choices.Single(o => o.optionId == item.lapseOptionId);
                    Assert.That(lapse.locked || lapse.costsAction || lapse.pickPerson, Is.False);
                    c.secondTargetId = lapse.optionId;
                }
                else c.text = item.choices.First().label;
                Accepted(engine, c);
            }
            Assert.Fail("Existing asks did not close in 24 actual neutral replies; history was not cleared.");
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
        { id = "dialogue-deal-proof-" + s.revision + "-" + kind, actorId = s.playerId, expectedRevision = s.revision, expectedPhase = s.phase, kind = kind };
        private static EpisodeCommand Start(EpisodeState s, string voter)
        {
            var c = Command(s, EpisodeCommandKind.StartDialogue); c.targetId = voter;
            c.secondTargetId = "open-case"; c.text = "dialogue-campaign-v1"; return c;
        }
        private static EpisodeCommand Choose(EpisodeState s, string node, string option)
        {
            var c = Command(s, EpisodeCommandKind.ChooseDialogue); c.targetId = s.dialogue.episodes.Single().id;
            c.secondTargetId = option; c.text = node; return c;
        }
        private static EpisodeCommand DealLobby(EpisodeState s, string voter)
        {
            var c = Command(s, EpisodeCommandKind.Lobby); c.targetId = voter; c.secondTargetId = s.playerId;
            c.text = LobbyAsk.Encode(LobbyAsk.Vote, LobbyApproach.Deal); return c;
        }
        private static int Remaining(EpisodeState s) => EpisodeEngine.SocialActionBudget(s) - EpisodeEngine.SocialActionsSpent(s);
        private static string Json(object value) => JsonConvert.SerializeObject(value, Formatting.None);
        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
        private static void NoRawVote(EpisodeState s)
        {
            Assert.That(s.promises.Any(p => p.kind == PromiseKind.Vote), Is.False);
            Assert.That(s.deals.Any(d => KnownBallots.IsVoteDeal(d.type)), Is.False);
        }
    }
}
