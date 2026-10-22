using System;
using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    // STAGED ONLY: the existing fixture declaration must gain `partial` on adoption.
    // Its real scene setup, isolated slot, fault seam and teardown remain the owners.
    public sealed partial class EpisodeDurableTransactionTests
    {
        private EpisodeState durableDialogueCampaign;
        private string durableDialogueVoter;
        private string durableDialogueCampaignJson;

        private void FindDurableDialogueCampaignThroughActualCommands()
        {
            // One bounded first-week search for the entire fixture, not six season searches.
            // Every test uses a detached engine; this cached source is never installed directly.
            for (uint seed = 1; seed <= 24; seed++)
            {
                var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(fresh);
                Assert.That(fresh.dialogueRulesVersion, Is.Zero, "Shipping enrollment must stay off.");
                fresh.dialogueRulesVersion = 1; // Explicit unpublished fixture enrollment only.
                AssertDurableDialogueValid(fresh);
                var engine = new EpisodeEngine(fresh);
                for (int step = 0; step < 96; step++)
                {
                    var state = engine.Snapshot;
                    if (state.week != 1 || state.phase == EpisodePhase.Finished) break;
                    if (state.phase != EpisodePhase.Campaign)
                    {
                        ApplyDurableDialogueFixture(engine, NextDurableDialogueFirstWeek(state));
                        continue;
                    }
                    if (!state.nominees.Contains(state.playerId)) break;
                    CloseDurableDialogueOldAsks(engine);
                    for (int debit = 0; debit < 16 && DurableDialogueRemaining(engine.Snapshot) > 1; debit++)
                    {
                        state = engine.Snapshot;
                        var talk = DurableDialogueCommand(state, EpisodeCommandKind.Talk);
                        talk.targetId = state.nominees.First(id => id != state.playerId);
                        ApplyDurableDialogueFixture(engine, talk);
                        CloseDurableDialogueOldAsks(engine);
                    }
                    state = engine.Snapshot;
                    string voter = EpisodeEngine.Voters(state).Where(actor => !actor.isPlayer).Select(actor => actor.id)
                        .FirstOrDefault(id => DialogueRules.CanStart(state, id, out _));
                    if (DurableDialogueRemaining(state) != 1 || voter == null) break;
                    durableDialogueCampaign = state.Clone();
                    durableDialogueCampaignJson = JsonUtility.ToJson(durableDialogueCampaign);
                    durableDialogueVoter = voter;
                    Assert.That(state.unifiedCommitmentRulesVersion, Is.EqualTo(2));
                    Assert.That(state.unifiedHearingRulesVersion, Is.EqualTo(1));
                    Assert.That(state.economyRulesVersion, Is.EqualTo(1));
                    Assert.That(state.story.rulesVersion, Is.EqualTo(9));
                    Assert.That(state.allWeekRulesStartWeek, Is.EqualTo(1));
                    TestContext.Out.WriteLine("Durable dialogue witness: seed " + seed + ", week 1, revision " + state.revision + ".");
                    return;
                }
            }
            Assert.Fail("No legal nominated one-AP campaign within 24 seeds x 96 first-week commands; no state was fabricated.");
        }

        [UnityTest]
        public IEnumerator DialogueEntryBeforeWriteFailurePreservesSourceFilesAndBothRandomStreams()
        {
            yield return ExerciseDurableDialogueFault(0, false);
        }

        [UnityTest]
        public IEnumerator DialogueMiddleBeforeWriteFailurePreservesSourceFilesAndBothRandomStreams()
        {
            yield return ExerciseDurableDialogueFault(1, false);
        }

        [UnityTest]
        public IEnumerator DialogueTerminalBeforeWriteFailurePreservesSourceFilesAndBothRandomStreams()
        {
            yield return ExerciseDurableDialogueFault(2, false);
        }

        [UnityTest]
        public IEnumerator DialogueEntryAfterReplacementAcknowledgementFailureInstallsOnce()
        {
            yield return ExerciseDurableDialogueFault(0, true);
        }

        [UnityTest]
        public IEnumerator DialogueMiddleAfterReplacementAcknowledgementFailureInstallsOnce()
        {
            yield return ExerciseDurableDialogueFault(1, true);
        }

        [UnityTest]
        public IEnumerator DialogueTerminalAfterReplacementAcknowledgementFailureInstallsOnce()
        {
            yield return ExerciseDurableDialogueFault(2, true);
        }

        private IEnumerator ExerciseDurableDialogueFault(int acceptedSteps, bool replaceBeforeThrow)
        {
            // Lazy, fixture-local preparation runs only for these six cases, never as a
            // new setup prerequisite for the existing unrelated durable-transaction tests.
            if (durableDialogueCampaign == null) FindDurableDialogueCampaignThroughActualCommands();
            Assert.That(durableDialogueCampaign, Is.Not.Null);
            Assert.That(JsonUtility.ToJson(durableDialogueCampaign), Is.EqualTo(durableDialogueCampaignJson));
            director.SuspendNpcAutonomyForDiagnostics();
            var fixture = new EpisodeEngine(durableDialogueCampaign);
            for (int step = 0; step < acceptedSteps; step++)
                ApplyDurableDialogueFixture(fixture, NextDurableDialogueStep(fixture.Snapshot));
            var installedSource = fixture.Snapshot;
            AssertDurableDialogueValid(installedSource);
            string path = director.SavePath;
            new EpisodeSaveStore(path).Save(installedSource);
            director.LoadNow();
            director.SuspendNpcAutonomyForDiagnostics();
            Assert.That(director.SavePath, Is.EqualTo(path));
            AssertStateEquals(installedSource, director.Snapshot);
            AssertDiskEquals(installedSource);

            var before = director.Snapshot;
            var sourceCopy = before.Clone();
            var command = NextDurableDialogueStep(before);
            string commandJson = JsonUtility.ToJson(command);
            var expected = new EpisodeEngine(before).Apply(command);
            Assert.That(expected.accepted, Is.True, expected.reason);
            Assert.That(expected.duplicate, Is.False);
            AssertDurableDialogueValid(expected.state);
            AssertStateEquals(sourceCopy, before);
            Assert.That(JsonUtility.ToJson(command), Is.EqualTo(commandJson));
            AssertDurableDialogueStep(before, expected.state, acceptedSteps);
            var originalFiles = CaptureFiles();
            byte[] originalPrimary = File.ReadAllBytes(path);
            int writes = 0;
            EpisodeState attempted = null;
            SetSaveHook(candidate =>
            {
                writes++;
                attempted = candidate.Clone();
                if (replaceBeforeThrow) new EpisodeSaveStore(path).Save(candidate);
                throw new IOException(replaceBeforeThrow
                    ? "Injected dialogue acknowledgement failure after real replacement."
                    : "Injected dialogue failure before any store write.");
            });

            var result = director.Submit(command);

            Assert.That(writes, Is.EqualTo(1));
            Assert.That(attempted, Is.Not.Null, "The actual durable boundary must receive the prepared dialogue candidate.");
            AssertStateEquals(expected.state, attempted);
            Assert.That(JsonUtility.ToJson(command), Is.EqualTo(commandJson));
            AssertStateEquals(sourceCopy, before);
            if (!replaceBeforeThrow)
            {
                Assert.That(result.accepted, Is.False);
                Assert.That(result.duplicate, Is.False);
                AssertStateEquals(before, result.state);
                AssertStateEquals(before, director.Snapshot);
                AssertDiskEquals(before);
                AssertFilesEqual(originalFiles);
                Assert.That(director.StatusMessage, Does.Contain("not committed"));
                Assert.That(director.StatusMessage, Does.Not.Contain("Saved locally"));
                Assert.That(GetField<bool>("npcSaveSuspended"), Is.True);
                Assert.That(GetField<bool>("blockedRecovery"), Is.False);

                // A rejected attempt must leave neither a durable receipt nor a stale paid
                // capability. Explicit read-only reload then retries the unchanged command.
                SetSaveHook(null);
                director.LoadNow();
                director.SuspendNpcAutonomyForDiagnostics();
                AssertStateEquals(before, director.Snapshot);
                AssertFilesEqual(originalFiles);
                SetSaveHook(candidate => { writes++; new EpisodeSaveStore(path).Save(candidate); });
                result = director.Submit(command);
                Assert.That(writes, Is.EqualTo(2));
            }

            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.duplicate, Is.False);
            AssertStateEquals(expected.state, result.state);
            AssertStateEquals(expected.state, director.Snapshot);
            AssertDurableDialogueValid(director.Snapshot);
            AssertDiskEquals(expected.state);
            Assert.That(GetField<bool>("npcSaveSuspended"), Is.False);
            Assert.That(GetField<bool>("blockedRecovery"), Is.False);
            CollectionAssert.AreEqual(originalPrimary, File.ReadAllBytes(path + ".backup"),
                "Exactly one successful replacement rotates the original primary into backup.");
            var committedFiles = CaptureFiles();
            int committedWrites = writes;
            var duplicate = director.Submit(command);
            Assert.That(duplicate.accepted, Is.False);
            Assert.That(duplicate.duplicate, Is.True);
            Assert.That(writes, Is.EqualTo(committedWrites), "A proven receipt cannot replay a paid step or rotate backup again.");
            AssertStateEquals(expected.state, duplicate.state);
            AssertStateEquals(expected.state, director.Snapshot);
            AssertFilesEqual(committedFiles);
            SetSaveHook(null);
            director.LoadNow();
            director.SuspendNpcAutonomyForDiagnostics();
            AssertStateEquals(expected.state, director.Snapshot);
            AssertDiskEquals(expected.state);
            AssertFilesEqual(committedFiles);
            Assert.That(JsonUtility.ToJson(durableDialogueCampaign), Is.EqualTo(durableDialogueCampaignJson));
            yield break;
        }

        private void AssertDurableDialogueStep(EpisodeState before, EpisodeState after, int acceptedSteps)
        {
            Assert.That(after.revision, Is.EqualTo(before.revision + 1));
            Assert.That(DurableDialogueRemaining(before), Is.EqualTo(acceptedSteps == 0 ? 1 : 0));
            Assert.That(DurableDialogueRemaining(after), Is.Zero);
            Assert.That(after.windowActions[Windows.AfterVeto],
                Is.EqualTo(before.windowActions[Windows.AfterVeto] + (acceptedSteps == 0 ? 1 : 0)));
            Assert.That(after.outOfPhaseSocialActions,
                Is.EqualTo(before.outOfPhaseSocialActions + (acceptedSteps == 0 ? 1 : 0)));
            var episode = after.dialogue.episodes.Single();
            var cycle = after.storylines.Single(row => row.id == episode.id);
            Assert.That(cycle.path.Count, Is.EqualTo(acceptedSteps + 1));
            Assert.That(cycle.path.Last().optionId, Is.EqualTo(new[] { "open-case", "appeal-personally", "make-plea" }[acceptedSteps]));
            Assert.That(after.houseEvents.Count(row => row.cycleId == episode.id), Is.EqualTo(acceptedSteps == 0 ? 2 : 3));
            Assert.That(episode.entry.windowActionAfter, Is.EqualTo(episode.entry.windowActionBefore + 1));
            if (acceptedSteps < 2)
            {
                Assert.That(DialogueRules.Active(after)?.id, Is.EqualTo(episode.id));
                Assert.That(episode.time, Is.Null);
                Assert.That(cycle.beatId, Is.EqualTo(acceptedSteps == 0 ? "case" : "finish-personal"));
                Assert.That(after.lobbies.Any(row => row.week == after.week && row.deciderId == durableDialogueVoter), Is.False);
            }
            else
            {
                Assert.That(DialogueRules.Active(after), Is.Null);
                Assert.That(cycle.status, Is.EqualTo(StorylineStatus.Completed));
                Assert.That(cycle.endingId, Is.EqualTo("answered"));
                Assert.That(episode.time.reason, Is.EqualTo("answered"));
                Assert.That(episode.time.proposedDelta, Is.Zero);
                Assert.That(episode.time.appliedDelta, Is.Zero);
                var lobby = after.lobbies.Single(row => row.week == after.week && row.deciderId == durableDialogueVoter);
                Assert.That((lobby.ask, lobby.subjectId, lobby.approach),
                    Is.EqualTo((LobbyAsk.Vote, after.playerId, LobbyApproach.Emotional)));
                Assert.That(episode.responses.Last().responseKey, Is.EqualTo(DialogueCampaignV1.TemplateId + "/vote-" + lobby.response));
            }
        }

        private EpisodeCommand NextDurableDialogueStep(EpisodeState state)
        {
            var active = DialogueRules.Active(state);
            if (active == null)
            {
                Assert.That(state.dialogue.episodes, Is.Empty);
                var start = DurableDialogueCommand(state, EpisodeCommandKind.StartDialogue);
                start.targetId = durableDialogueVoter;
                start.secondTargetId = "open-case";
                start.text = DialogueCampaignV1.TemplateId;
                return start;
            }
            var cycle = state.storylines.Single(row => row.id == active.id);
            Assert.That(cycle.beatId, Is.EqualTo("case").Or.EqualTo("finish-personal"));
            var choice = DurableDialogueCommand(state, EpisodeCommandKind.ChooseDialogue);
            choice.targetId = active.id;
            choice.secondTargetId = cycle.beatId == "case" ? "appeal-personally" : "make-plea";
            choice.text = cycle.beatId;
            return choice;
        }

        private static EpisodeCommand NextDurableDialogueFirstWeek(EpisodeState state)
        {
            Assert.That(state.week, Is.EqualTo(1));
            Assert.That(state.phase, Is.Not.EqualTo(EpisodePhase.Campaign));
            var command = DurableDialogueCommand(state, EpisodeCommandKind.Advance);
            if (state.pendingDiary != null)
            {
                command.kind = EpisodeCommandKind.SkipDiary;
                command.targetId = state.pendingDiary.id;
            }
            else if (EpisodeEngine.IsCompetition(state.phase) && !state.competitionResolved
                && EpisodeEngine.CompetitionPlayers(state).Any(actor => actor.isPlayer))
            {
                command.kind = EpisodeCommandKind.Compete;
                command.performance = .5;
            }
            else if (state.phase == EpisodePhase.Nomination && state.nominees.Count == 0 && state.hohId == state.playerId)
            {
                command.kind = EpisodeCommandKind.Nominate;
                var nominees = EpisodeEngine.NominationCandidates(state).Take(2).ToArray();
                Assert.That(nominees.Length, Is.EqualTo(2));
                command.targetId = nominees[0].id;
                command.secondTargetId = nominees[1].id;
            }
            else if (state.phase == EpisodePhase.VetoMeeting && !state.vetoResolved
                && (state.vetoHolderId == state.playerId || state.hohId == state.playerId && EpisodeEngine.NpcVetoSave(state) != null))
            {
                command.kind = EpisodeCommandKind.ResolveVeto;
                var replacement = EpisodeEngine.ReplacementCandidates(state).FirstOrDefault();
                command.useVeto = replacement != null && !EpisodeEngine.VetoIsLockedAtFinalFour(state);
                command.targetId = command.useVeto
                    ? state.vetoHolderId == state.playerId ? state.nominees[0] : EpisodeEngine.NpcVetoSave(state)
                    : null;
                command.secondTargetId = replacement?.id;
            }
            return command;
        }

        private static void CloseDurableDialogueOldAsks(EpisodeEngine engine)
        {
            for (int reply = 0; reply < 24; reply++)
            {
                var state = engine.Snapshot;
                var item = state.houseEvents.FirstOrDefault(row => !row.resolved && HouseEventKind.Asks(row.kind));
                if (item == null) return;
                Assert.That(item.contentId == null || !item.contentId.StartsWith(DialogueCampaignV1.TemplateId + ":", StringComparison.Ordinal), Is.True);
                var command = DurableDialogueCommand(state, item.IsStory ? EpisodeCommandKind.ProgressStoryline : EpisodeCommandKind.ResolveHouseEvent);
                command.targetId = item.id;
                if (item.IsStory)
                {
                    var neutral = item.choices.Single(choice => choice.optionId == item.lapseOptionId);
                    Assert.That(neutral.locked || neutral.costsAction || neutral.pickPerson, Is.False);
                    command.secondTargetId = neutral.optionId;
                }
                else command.text = item.choices.First().label;
                ApplyDurableDialogueFixture(engine, command);
            }
            Assert.Fail("Old asks exceeded 24 actual neutral replies; no Story history was cleared.");
        }

        private static EpisodeCommand DurableDialogueCommand(EpisodeState state, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = "durable-dialogue-" + state.revision + "-" + kind, actorId = state.playerId,
            expectedRevision = state.revision, expectedPhase = state.phase, kind = kind
        };

        private static int DurableDialogueRemaining(EpisodeState state) => EpisodeEngine.SocialActionBudget(state) - EpisodeEngine.SocialActionsSpent(state);

        private static void ApplyDurableDialogueFixture(EpisodeEngine engine, EpisodeCommand command)
        {
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + ": " + result.reason);
            Assert.That(result.duplicate, Is.False);
            AssertDurableDialogueValid(result.state);
        }

        private static void AssertDurableDialogueValid(EpisodeState state) =>
            Assert.That(EpisodeValidation.TryValidate(state, out string error), Is.True, error);
    }
}
