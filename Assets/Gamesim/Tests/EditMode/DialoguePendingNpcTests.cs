using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Actual public-command coverage of the paid choice's pending NPC Vote writer. Only the
    /// explicit pilot rule is selected on a fresh season; no cadence/history/RNG is fabricated.
    /// </summary>
    public sealed class DialoguePendingNpcTests
    {
        private const int Seeds = 64, WalkCommands = 96, NeutralAnswers = 24;

        [Test]
        public void ARealPaidChoiceCommitsAnNpcVotePromiseAndTheLaterChoicesPreserveEarlierWrites()
        {
            int eligibleCampaigns = 0, paidPaths = 0, observedPromiseActs = 0;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(fresh);
                Assert.That(fresh.dialogueRulesVersion, Is.Zero, "The test must not activate shipping defaults.");
                fresh.dialogueRulesVersion = 1;
                Valid(fresh);
                string freshBytes = Json(fresh);
                var engine = new EpisodeEngine(fresh);
                for (int step = 0; step < WalkCommands; step++)
                {
                    var current = engine.Snapshot;
                    if (current.week != 1 || current.phase == EpisodePhase.Finished) break;
                    if (current.phase != EpisodePhase.Campaign)
                    {
                        Accepted(engine, ModeTwoReaderSweep.Next(current));
                        continue;
                    }
                    if (!current.nominees.Contains(current.playerId)) break;
                    CloseOldAsks(engine);
                    var source = engine.Snapshot;
                    string voter = EpisodeEngine.Voters(source).Where(c => !c.isPlayer).Select(c => c.id)
                        .FirstOrDefault(id => DialogueRules.CanStart(source, id, out _));
                    if (voter == null) break;
                    eligibleCampaigns++;
                    string sourceBytes = Json(source);
                    Assert.That(source.unifiedCommitmentRulesVersion, Is.EqualTo(2));
                    Assert.That(source.allWeekRulesStartWeek, Is.EqualTo(1));
                    Assert.That(EpisodeEngine.AllWeekOn(source), Is.True);
                    Assert.That(source.npcSocial.beatWeek, Is.EqualTo(1));
                    Assert.That(source.npcSocial.beatWindow, Is.EqualTo(Windows.AfterVeto));
                    var candidate = new EpisodeEngine(source);
                    var start = Command(source, EpisodeCommandKind.StartDialogue);
                    start.targetId = voter; start.secondTargetId = "open-case"; start.text = DialogueCampaignV1.TemplateId;
                    var entered = Paid(candidate, start);
                    var written = NewNpcVotes(source, entered);
                    foreach (var row in written) AssertVoteLineage(source, entered, row);
                    observedPromiseActs += NewBeats(source, entered).Count(a => a.kind == NpcActKinds.Promise);
                    string episodeId = entered.dialogue.episodes.Single().id;

                    var middle = Paid(candidate, Choose(entered, episodeId, "case", "appeal-personally"));
                    Assert.That(NewNpcVotes(entered, middle), Is.Empty,
                        "This already-drained real path has no new tick at its free middle choice.");
                    var finished = Paid(candidate, Choose(middle, episodeId, "finish-personal", "make-plea"));
                    Assert.That(NewNpcVotes(middle, finished), Is.Empty,
                        "The free terminal preserves the entry's writes; it does not invent another due beat.");
                    paidPaths++;

                    Assert.That(entered.windowActions[Windows.AfterVeto], Is.EqualTo(source.windowActions[Windows.AfterVeto] + 1));
                    Assert.That(middle.windowActions, Is.EqualTo(entered.windowActions));
                    Assert.That(finished.windowActions, Is.EqualTo(entered.windowActions));
                    Assert.That(finished.outOfPhaseSocialActions, Is.EqualTo(source.outOfPhaseSocialActions + 1));
                    Assert.That(DialogueRules.Active(finished), Is.Null);
                    Assert.That(finished.storylines.Single(c => c.id == episodeId).endingId, Is.EqualTo("answered"));
                    Assert.That(finished.dialogue.episodes.Single().responses.Select(r => r.revision),
                        Is.EqualTo(new[] { entered.revision, middle.revision, finished.revision }));
                    Assert.That(finished.promises.Any(p => p.kind == PromiseKind.Vote), Is.False, "Mode2 has no raw Vote mirror.");
                    foreach (var row in written)
                        Assert.That(Json(finished.unifiedCommitments.Single(c => c.id == row.id)), Is.EqualTo(Json(row)),
                            "A later choice cannot lose or rewrite an earlier real NPC Vote promise.");
                    Assert.That(Json(source), Is.EqualTo(sourceBytes), "The candidate engine cannot mutate the retained campaign input.");
                    Assert.That(Json(fresh), Is.EqualTo(freshBytes), "The first-week walk cannot mutate its fresh constructor input.");
                    Valid(finished);
                    if (written.Count != 0)
                    {
                        TestContext.Out.WriteLine("Actual pending NPC Vote witness: seed " + seed + ", entry revision " + entered.revision
                            + ", rows " + string.Join(",", written.Select(r => r.id)) + "; entry/middle/terminal all committed.");
                        return;
                    }
                    // No Safety/no-effect substitution: a completed path without a new Vote row
                    // is merely another searched season, never a passing witness.
                    break;
                }
                Assert.That(Json(fresh), Is.EqualTo(freshBytes));
            }
            Assert.Fail("No actual paid-command NPC Vote promise in " + Seeds + " first weeks x " + WalkCommands
                + " walk commands, at most " + NeutralAnswers + " old-ask replies and three paid choices each. Eligible campaigns="
                + eligibleCampaigns + ", completed paid paths=" + paidPaths + ", entry Promise acts=" + observedPromiseActs
                + ". Safety and no-effect beats do not satisfy this gate.");
        }

        private static List<UnifiedCommitmentState> NewNpcVotes(EpisodeState before, EpisodeState after)
        {
            var old = new HashSet<string>(before.unifiedCommitments.Select(r => r.id), StringComparer.Ordinal);
            return after.unifiedCommitments.Where(r => !old.Contains(r.id) && r.kind == UnifiedVoteTogether.Vote
                && r.sourcePolicy == UnifiedCommitments.PromisePolicy && r.origin == UnifiedCommitments.NpcPromise)
                .Select(r => r.Clone()).ToList();
        }

        private static NpcActState[] NewBeats(EpisodeState before, EpisodeState after)
        {
            var old = new HashSet<string>(before.npcSocial.acts.Select(a => a.id), StringComparer.Ordinal);
            return after.npcSocial.acts.Where(a => !old.Contains(a.id) && EpisodeEngine.IsBeat(a)).ToArray();
        }

        private static void AssertVoteLineage(EpisodeState before, EpisodeState after, UnifiedCommitmentState row)
        {
            Assert.That(row.id, Does.StartWith("promise-npc-"));
            Assert.That(int.TryParse(row.id.Substring("promise-npc-".Length), NumberStyles.None,
                CultureInfo.InvariantCulture, out int sequence), Is.True);
            Assert.That(sequence, Is.GreaterThanOrEqualTo(before.nextSequence).And.LessThan(after.nextSequence));
            Assert.That((row.kind, row.sourcePolicy, row.origin, row.status, row.reciprocal, row.createdWeek, row.expiresWeek),
                Is.EqualTo((UnifiedVoteTogether.Vote, UnifiedCommitments.PromisePolicy, UnifiedCommitments.NpcPromise,
                    DealStatus.Active, false, before.week, before.week)));
            Assert.That(row.makerId, Is.Not.EqualTo(before.playerId));
            Assert.That(row.beneficiaryId, Is.Not.EqualTo(before.playerId));
            Assert.That(before.nominees, Does.Contain(row.makerId).And.Not.Contain(row.beneficiaryId));
            Assert.That(row.targetId, Is.EqualTo(before.nominees.First(id => id != row.makerId)));
            Assert.That((row.voteBindingWeek, row.voteFirstRevealWeek, row.trustImpact),
                Is.EqualTo((before.week, before.week, DealTrust.Medium)));
            Assert.That(row.subtype, Is.Null); Assert.That(row.conditionalVeto, Is.Null);
            Assert.That(NpcPromises.Offer(before, row.makerId, row.beneficiaryId, true), Is.EqualTo(PromiseKind.Vote));
            Assert.That(before.unifiedCommitments.Any(r => r.kind == UnifiedVoteTogether.Vote && r.sourcePolicy == UnifiedCommitments.PromisePolicy
                && r.makerId == row.makerId && r.beneficiaryId == row.beneficiaryId && r.status == DealStatus.Active), Is.False);
            var acts = NewBeats(before, after).Where(a => a.kind == NpcActKinds.Promise
                && a.actorId == row.makerId && a.partnerId == row.beneficiaryId).ToArray();
            Assert.That(acts, Has.Length.EqualTo(1), "The canonical row must belong to the actual new Promise beat.");
            int position = after.npcSocial.beatPlan.IndexOf(row.makerId);
            Assert.That(position, Is.GreaterThanOrEqualTo(before.npcSocial.beatsFired).And.LessThan(after.npcSocial.beatsFired));
            Assert.That(acts[0].id, Is.EqualTo(before.week + "-" + Windows.AfterVeto + "-" + position));
            Assert.That((acts[0].week, acts[0].window, acts[0].firedTick),
                Is.EqualTo((before.week, Windows.AfterVeto, after.windowActions[Windows.AfterVeto])));
            string description = after.Find(row.makerId).name + " gave " + after.Find(row.beneficiaryId).name + " their word";
            for (int reverse = 0; reverse < 2; reverse++)
            {
                string from = reverse == 0 ? row.makerId : row.beneficiaryId, to = reverse == 0 ? row.beneficiaryId : row.makerId;
                var receipt = after.relationships.Single(r => r.fromId == from && r.toId == to).events.Single(e => e.sequence == sequence + reverse);
                Assert.That((receipt.type, receipt.impactScore, receipt.description, receipt.week),
                    Is.EqualTo(("promise-made", 15d, description, before.week)));
            }
        }

        private static EpisodeState Paid(EpisodeEngine engine, EpisodeCommand command)
        {
            var before = engine.Snapshot;
            var after = Accepted(engine, command);
            Assert.That(Json(after.unifiedCommitments.Take(before.unifiedCommitments.Count)), Is.EqualTo(Json(before.unifiedCommitments)),
                "The sequential catch-up appends real canonical rows without rewriting prior commitments.");
            Assert.That(Json(after.npcSocial.acts.Take(before.npcSocial.acts.Count)), Is.EqualTo(Json(before.npcSocial.acts)),
                "Earlier real acts remain visible to later beats and later choices.");
            Assert.That(after.npcSocial.beatPlan, Is.EqualTo(before.npcSocial.beatPlan));
            Assert.That(after.npcSocial.beatsFired, Is.EqualTo(EpisodeEngine.Due(after.npcSocial.beatPlan.Count,
                after.npcSocial.beatSeats, after.windowActions[Windows.AfterVeto])));
            var positions = NewBeats(before, after).Select(a => int.Parse(a.id.Split('-')[2], CultureInfo.InvariantCulture)).ToArray();
            Assert.That(positions, Is.Ordered.Ascending.And.Unique, "The live source loop's new acts retain plan order.");
            return after;
        }

        private static void CloseOldAsks(EpisodeEngine engine)
        {
            for (int i = 0; i < NeutralAnswers; i++)
            {
                var s = engine.Snapshot;
                var item = s.houseEvents.FirstOrDefault(e => !e.resolved && HouseEventKind.Asks(e.kind));
                if (item == null) return;
                Assert.That(item.contentId?.StartsWith(DialogueCampaignV1.TemplateId + ":", StringComparison.Ordinal) == true, Is.False);
                var c = Command(s, item.IsStory ? EpisodeCommandKind.ProgressStoryline : EpisodeCommandKind.ResolveHouseEvent);
                c.targetId = item.id;
                if (item.IsStory)
                {
                    var leave = item.choices.Single(o => o.optionId == item.lapseOptionId);
                    Assert.That(leave.locked || leave.costsAction || leave.pickPerson, Is.False);
                    c.secondTargetId = leave.optionId;
                }
                else { Assert.That(item.choices, Is.Not.Empty); c.text = item.choices.First().label; }
                Accepted(engine, c);
            }
            Assert.Fail("Old asks did not close within " + NeutralAnswers + " real neutral replies; no history was cleared.");
        }

        private static EpisodeState Accepted(EpisodeEngine engine, EpisodeCommand command)
        {
            var before = engine.Snapshot; string original = Json(before), request = Json(command);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, "week " + before.week + " " + before.phase + " " + command.kind + ": " + result.reason);
            Assert.That(result.duplicate, Is.False);
            Assert.That(Json(before), Is.EqualTo(original)); Assert.That(Json(command), Is.EqualTo(request));
            Assert.That(result.state.revision, Is.EqualTo(before.revision + 1));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(Json(result.state)));
            Valid(result.state);
            return result.state;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = "dialogue-pending-npc-" + s.revision + "-" + kind, actorId = s.playerId, kind = kind,
            expectedRevision = s.revision, expectedPhase = s.phase
        };

        private static EpisodeCommand Choose(EpisodeState s, string id, string node, string option)
        {
            var c = Command(s, EpisodeCommandKind.ChooseDialogue);
            c.targetId = id; c.text = node; c.secondTargetId = option; return c;
        }

        private static string Json(object value) => JsonConvert.SerializeObject(value, Formatting.None);
        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
    }
}
