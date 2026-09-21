using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    // Staged schema-5 tests. Navigation placement is accelerated on the baked surface;
    // the pact, competitions, veto, ballot, reveal, and reload use actual UI callbacks.
    public sealed partial class EpisodePlayModeTests
    {
        private const string BlocTaylorId = "taylor-kim", BlocCaseyId = "casey-wilson", BlocJamieId = "jamie-roberts";

        [UnityTest]
        public IEnumerator VotingBloc_ActualPactAndCeremoniesChangeMayaBallotWithoutExposingCoordination()
        {
            yield return ReachBlocWitnessThroughActualUi();
            var before = director.Snapshot;
            var witness = AssertBlocWitness(before);
            var expectedMaya = witness.evaluations.Single(vote => vote.voterId == ContentCatalog.MayaId);
            var diskBefore = File.ReadAllBytes(director.SavePath);

            yield return OpenDiaryFixturePanel();
            AssertBlocPrivateEvidenceAbsent(before, witness);
            ButtonWithCaption("Vote to evict Taylor Kim").onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.HasDiaryDecisionDraft, Is.True);
            Assert.That(ActiveDiaryText(), Does.Contain("Nothing has been committed yet"));
            AssertEquivalent(before, director.Snapshot);
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(diskBefore));
            ButtonWithCaption(EpisodeHud.DiaryCancelCaption).onClick.Invoke();
            yield return null; yield return null;
            AssertEquivalent(before, director.Snapshot);
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(diskBefore));

            ButtonWithCaption("Vote to evict Taylor Kim").onClick.Invoke();
            yield return null; yield return null;
            var confirm = ButtonWithCaption(EpisodeHud.DiaryConfirmCaption).onClick;
            confirm.Invoke(); confirm.Invoke();
            yield return null; yield return null;
            var pending = director.Snapshot;
            Assert.That(pending.revision, Is.EqualTo(before.revision + 1));
            Assert.That(pending.phase, Is.EqualTo(EpisodePhase.Eviction));
            Assert.That(pending.evictionResolved, Is.False);
            Assert.That(pending.votes, Has.Count.EqualTo(1));
            Assert.That(pending.votes[0].voterId, Is.EqualTo(pending.playerId));
            Assert.That(pending.votes[0].targetId, Is.EqualTo(BlocTaylorId),
                "An inferred pact directive must never override the human's actual ballot.");
            AssertBlocPrivateMechanicsUnchanged(before, pending);
            var ballotEvents = pending.events.Skip(before.events.Count).ToArray();
            Assert.That(ballotEvents, Has.Length.EqualTo(1));
            Assert.That(ballotEvents[0].kind, Is.EqualTo("private-vote"));
            Assert.That(ballotEvents[0].audienceIds, Is.EquivalentTo(new[] { pending.playerId }));
            Assert.That(DiaryHasButton("Continue episode"), Is.False);
            Assert.That(ActiveDiaryText(), Does.Contain("Your ballot has already been recorded."));
            AssertBlocPrivateEvidenceAbsent(pending, witness);
            AssertBlocPlanNotSerialized(pending);

            director.ClosePanels();
            ButtonWithCaption("Notebook [J]").onClick.Invoke();
            yield return null; yield return null;
            // An alliance you are in belongs to your own story.
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Story);
            yield return null; yield return null;
            Assert.That(ActiveDiaryText(), Does.Contain("The Maya Pact"),
                "The player's own pact membership is legitimately known, unlike its private coordination.");
            // Asked on BOTH pages a ballot could surface on - the story, which prints event text
            // verbatim, and the votes page, which prints the ballots themselves. The original asked
            // once of a notebook that rendered everything at once; asking each page separately is
            // strictly stronger, and asking only one of them would be weaker.
            Assert.That(ActiveDiaryText(), Does.Not.Contain("Maya Hassan voted to evict"));
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Votes);
            yield return null; yield return null;
            Assert.That(ActiveDiaryText(), Does.Not.Contain("Maya Hassan voted to evict"));
            AssertBlocPrivateEvidenceAbsent(pending, witness);
            AssertEquivalent(pending, director.Snapshot);
            yield return ReloadBlocThroughActualSettings(pending);
            yield return OpenDiaryFixturePanel();
            Assert.That(ActiveDiaryText(), Does.Contain("Your ballot has already been recorded."));
            Assert.That(DiaryHasButton("Vote to evict Taylor Kim"), Is.False);
            AssertBlocPrivateEvidenceAbsent(pending, witness);

            // Including the already-voted human in the full pact snapshot preserves the
            // source coordination draws; only missing NPC ballots are evaluated/committed.
            var afterReloadPlan = AssertBlocWitness(director.Snapshot);
            AssertBlocPlansEqual(witness, afterReloadPlan);
            yield return OpenBlocEpisodeStation();
            var reveal = ButtonWithCaption("Continue episode").onClick;
            reveal.Invoke();
            yield return null; yield return null;
            var revealed = director.Snapshot;
            Assert.That(revealed.revision, Is.EqualTo(pending.revision + 1));
            Assert.That(revealed.evictionResolved, Is.True);
            Assert.That(revealed.votes, Has.Count.EqualTo(3));
            Assert.That(revealed.randomState, Is.EqualTo(pending.randomState),
                "Pact planning uses its own named round stream, not the saved season stream.");
            var mayaBallot = revealed.votes.Single(vote => vote.voterId == ContentCatalog.MayaId);
            Assert.That(mayaBallot.targetId, Is.EqualTo(expectedMaya.selectedNomineeId));
            Assert.That(mayaBallot.targetId, Is.EqualTo(BlocCaseyId));
            var publicReason = WebEvictionVoting.ExplainNative(pending, expectedMaya);
            Assert.That(mayaBallot.reason, Is.EqualTo(publicReason));
            Assert.That(mayaBallot.reason, Is.Not.EqualTo(WebEvictionVoting.ExplainNative(pending, expectedMaya, true)));
            string publicMayaReveal = "Maya Hassan voted to evict Casey Wilson. " + publicReason;
            Assert.That(revealed.events.Single(entry => entry.kind == "vote-reveal"
                && entry.text.StartsWith("Maya Hassan voted to evict", StringComparison.Ordinal)).text,
                Is.EqualTo(publicMayaReveal));
            AssertBlocPrivateEvidenceAbsent(revealed, witness);
            var bytesAfterReveal = File.ReadAllBytes(director.SavePath);
            reveal.Invoke(); confirm.Invoke();
            yield return null; yield return null;
            AssertEquivalent(revealed, director.Snapshot);
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytesAfterReveal));

            director.ClosePanels();
            ButtonWithCaption("Notebook [J]").onClick.Invoke();
            yield return null; yield return null;
            // The reveal is an event, and events are the story page.
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Story);
            yield return null; yield return null;
            Assert.That(ActiveDiaryText(), Does.Contain(publicMayaReveal));
            AssertBlocPrivateEvidenceAbsent(revealed, witness);
            AssertEquivalent(revealed, director.Snapshot);
            yield return ReloadBlocThroughActualSettings(revealed);
            // Replay the actual persisted command receipt after reload, without an
            // invented successful command ID, to prove consequences cannot reapply.
            var duplicate = director.Submit(new EpisodeCommand
            {
                id = revealed.acceptedCommandIds.Last(), actorId = pending.playerId,
                expectedPhase = pending.phase, expectedRevision = pending.revision,
                kind = EpisodeCommandKind.Advance
            });
            Assert.That(duplicate.duplicate, Is.True);
            AssertEquivalent(revealed, director.Snapshot);
            director.ClosePanels();
            ButtonWithCaption("Notebook [J]").onClick.Invoke();
            yield return null; yield return null;
            // The reveal is an event, and events are the story page.
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Story);
            yield return null; yield return null;
            Assert.That(ActiveDiaryText(), Does.Contain(publicMayaReveal));
            AssertBlocPrivateEvidenceAbsent(revealed, witness);
            AssertBlocPlanNotSerialized(revealed);
            Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var persisted, out var message), Is.True, message);
            AssertEquivalent(revealed, persisted);
        }

        [UnityTest]
        public IEnumerator VotingBloc_PrivateBallotDraftCancelReplacementAndActualReloadCannotReuseOldConfirmation()
        {
            yield return InstallLegalBlocWitness();
            var before = director.Snapshot;
            var witness = AssertBlocWitness(before);
            var diskBefore = File.ReadAllBytes(director.SavePath);
            yield return OpenDiaryFixturePanel();
            ButtonWithCaption("Vote to evict Taylor Kim").onClick.Invoke();
            yield return null; yield return null;
            var oldConfirm = ButtonWithCaption(EpisodeHud.DiaryConfirmCaption).onClick;
            var oldCancel = ButtonWithCaption(EpisodeHud.DiaryCancelCaption).onClick;
            oldCancel.Invoke();
            yield return null; yield return null;
            ButtonWithCaption("Vote to evict Casey Wilson").onClick.Invoke();
            yield return null; yield return null;
            oldConfirm.Invoke(); oldCancel.Invoke();
            yield return null; yield return null;
            Assert.That(director.HasDiaryDecisionDraft, Is.True);
            Assert.That(ActiveDiaryText(), Does.Contain("Vote privately to evict Casey Wilson."));
            AssertEquivalent(before, director.Snapshot);
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(diskBefore));
            var preReloadConfirm = ButtonWithCaption(EpisodeHud.DiaryConfirmCaption).onClick;
            yield return ReloadBlocThroughActualSettings(before);
            Assert.That(director.HasDiaryDecisionDraft, Is.False);
            yield return OpenDiaryFixturePanel();
            ButtonWithCaption("Vote to evict Taylor Kim").onClick.Invoke();
            yield return null; yield return null;
            preReloadConfirm.Invoke(); oldConfirm.Invoke(); oldCancel.Invoke();
            yield return null; yield return null;
            Assert.That(director.HasDiaryDecisionDraft, Is.True);
            Assert.That(ActiveDiaryText(), Does.Contain("Vote privately to evict Taylor Kim."));
            AssertEquivalent(before, director.Snapshot);
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(diskBefore));
            AssertBlocPrivateEvidenceAbsent(before, witness);
            var confirm = ButtonWithCaption(EpisodeHud.DiaryConfirmCaption).onClick;
            confirm.Invoke(); confirm.Invoke(); preReloadConfirm.Invoke();
            yield return null; yield return null;
            var pending = director.Snapshot;
            Assert.That(pending.revision, Is.EqualTo(before.revision + 1));
            Assert.That(pending.votes, Has.Count.EqualTo(1));
            Assert.That(pending.votes.Single().targetId, Is.EqualTo(BlocTaylorId));
            Assert.That(pending.evictionResolved, Is.False);
            AssertBlocPrivateMechanicsUnchanged(before, pending);
            AssertBlocPrivateEvidenceAbsent(pending, witness);
            yield return ReloadBlocThroughActualSettings(pending);
            AssertEquivalent(pending, director.Snapshot);
        }

        private IEnumerator ReachBlocWitnessThroughActualUi()
        {
            // Install only an unmodified canonical initial seed. Every subsequent
            // gameplay state below is produced by a visible gameplay control.
            //
            // The one exception is the social-budget boundary. This walk was recorded when a week
            // allowed a flat eighteen actions and it spends four; the allowance is half the active
            // house now. Declaring the recording's own allowance is the same thing the EditMode
            // bloc witness does, and it is the only option that keeps the walk intact: every
            // conversation consumes the season's generator, so dropping one to fit the new budget
            // would re-roll the competitions and invalidate the caption sequence below.
            var initial = ContentCatalog.Create(4);
            initial.socialBudgetRulesStartWeek = 2;
            Assert.That(EpisodeValidation.TryValidate(initial, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(initial);
            yield return ReloadEpisode();
            AssertEquivalent(initial, director.Snapshot);
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            yield return OpenNearbyNpc(maya);
            for (int talk = 0; talk < 3; talk++)
                yield return ClickBlocCommand("Spend time together");
            yield return ClickBlocCommand("Propose an alliance");
            Assert.That(director.Snapshot.Allied(initial.playerId, ContentCatalog.MayaId), Is.True);
            director.ClosePanels();
            string[] captions =
            {
                "Begin the next competition",
                "Accessible alternative: steady 1-point bonus",
                "Continue to the next ceremony",
                "Continue episode", // NPC HoH nominates.
                "Continue episode", // Nomination to veto selection.
                "Continue episode", // Draw participants.
                "Accessible alternative: steady 1-point bonus",
                "Continue to the next ceremony",
                "Save You (HoH chooses replacement)",
                "Continue episode",
                "Close campaigning and open voting",
                "Continue episode" // Eviction night opens on the speeches; the house votes after them.
            };
            foreach (string caption in captions)
            {
                yield return OpenBlocEpisodeStation();
                yield return ClickBlocCommand(caption);
            }
            AssertBlocWitness(director.Snapshot);
        }

        private IEnumerator InstallLegalBlocWitness()
        {
            // Same source-confirmed seed-4 history as the actual-UI test; no mutable
            // role, relationship, nominee, alliance, memory or RNG fixture assignment.
            //
            // The social-budget boundary is declared for the same reason it is there: this history
            // spends four actions in week one, which was the whole allowance when it was recorded.
            // Trimming one to fit today's budget would consume a different number of generator
            // rolls and produce a different season, which is not the history this witness is.
            var initial = ContentCatalog.Create(4);
            initial.socialBudgetRulesStartWeek = 2;
            var engine = new EpisodeEngine(initial);
            for (int social = 0; social < 4; social++)
            {
                var state = engine.Snapshot;
                var command = NextCommand(state);
                command.kind = social < 3 ? EpisodeCommandKind.Talk : EpisodeCommandKind.FormAlliance;
                command.targetId = ContentCatalog.MayaId;
                var result = engine.Apply(command);
                Assert.That(result.accepted, Is.True, result.reason);
            }
            for (int guard = 0; !(engine.Snapshot.phase == EpisodePhase.Eviction
                && engine.Snapshot.evictionStage == EvictionStage.Voting) && guard < 30; guard++)
            {
                var state = engine.Snapshot;
                var command = NextCommand(state);
                if (state.phase == EpisodePhase.VetoMeeting && !state.vetoResolved)
                {
                    Assert.That(state.vetoHolderId, Is.EqualTo(state.playerId));
                    command.kind = EpisodeCommandKind.ResolveVeto;
                    command.useVeto = true;
                    command.targetId = state.playerId;
                    command.secondTargetId = null; // The actual UI lets the NPC HoH choose.
                }
                var result = engine.Apply(command);
                Assert.That(result.accepted, Is.True, result.reason);
            }
            var fixture = engine.Snapshot;
            AssertBlocWitness(fixture);
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            AssertEquivalent(fixture, director.Snapshot);
        }

        private IEnumerator OpenBlocEpisodeStation()
        {
            director.ClosePanels();
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null; yield return null;
        }

        private IEnumerator ClickBlocCommand(string caption)
        {
            int revision = director.Snapshot.revision;
            var button = ButtonWithCaption(caption);
            Assert.That(button.IsInteractable(), Is.True, caption);
            button.onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision + 1), caption + ": " + director.StatusMessage);
        }

        private IEnumerator ReloadBlocThroughActualSettings(EpisodeState expected)
        {
            director.ClosePanels();
            ButtonWithCaption("Settings").onClick.Invoke();
            yield return null; yield return null;
            ButtonWithCaption("Reload current slot").onClick.Invoke();
            yield return null; yield return null;
            AssertEquivalent(expected, director.Snapshot);
            Assert.That(director.IsPanelOpen, Is.False);
            Assert.That(player.InputEnabled, Is.True);
        }

        private static WebNativeEvictionRoundPlan AssertBlocWitness(EpisodeState state)
        {
            Assert.That(state.seed, Is.EqualTo(4));
            Assert.That(state.week, Is.EqualTo(1));
            Assert.That(state.phase, Is.EqualTo(EpisodePhase.Eviction));
            Assert.That(state.evictionResolved, Is.False);
            Assert.That(state.hohId, Is.EqualTo(BlocJamieId));
            Assert.That(state.vetoHolderId, Is.EqualTo(state.playerId));
            Assert.That(state.nominees, Is.EquivalentTo(new[] { BlocTaylorId, BlocCaseyId }));
            Assert.That(state.Allied(state.playerId, ContentCatalog.MayaId), Is.True);
            Assert.That(EpisodeEngine.Voters(state).Select(voter => voter.id), Does.Contain(state.playerId));
            Assert.That(EpisodeEngine.Voters(state).Select(voter => voter.id), Does.Contain(ContentCatalog.MayaId));
            var before = JsonUtility.ToJson(state);
            var missing = EpisodeEngine.Voters(state).Where(voter => !voter.isPlayer
                && !state.votes.Any(vote => vote.voterId == voter.id)).Select(voter => voter.id).ToArray();
            var plan = WebNativeEvictionRound.Evaluate(state, missing);
            var coordinated = plan.evaluations.Single(vote => vote.voterId == ContentCatalog.MayaId);
            var options = WebEvictionVoting.FromNative(state, ContentCatalog.MayaId);
            var nominees = options.nominees.ToDictionary(nominee => nominee.id);
            // Critical: compare the exact same cast-ordered nominee pair. Reversing
            // options can independently change seeded exact ties, falsely proving a pact.
            options.nominees = state.contestants.Where(actor => nominees.ContainsKey(actor.id))
                .Select(actor => nominees[actor.id]).ToList();
            options.blocDirective = null;
            var uncoordinated = WebEvictionVoting.Evaluate(options);
            Assert.That(uncoordinated.selectedNomineeId, Is.EqualTo(BlocTaylorId));
            Assert.That(coordinated.selectedNomineeId, Is.EqualTo(BlocCaseyId));
            Assert.That(coordinated.privateReasonCodes.First(), Is.EqualTo("blocPressure"));
            Assert.That(coordinated.publicReasonCodes, Does.Not.Contain("blocPressure"));
            Assert.That(coordinated.nomineeEvaluations.Single(item => item.nomineeId == BlocCaseyId)
                .factors.Single(factor => factor.code == "blocPressure").value, Is.EqualTo(-40));
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before), "Inspecting the detached plan cannot mutate the save or roll season RNG.");
            return plan;
        }

        private void AssertBlocPrivateEvidenceAbsent(EpisodeState state, WebNativeEvictionRoundPlan plan)
        {
            string text = ActiveDiaryText() + "\n" + director.StatusMessage + "\n"
                + string.Join("\n", state.events.Where(entry => entry.audienceIds.Count == 0
                    || entry.audienceIds.Contains(state.playerId)).Select(entry => entry.text));
            foreach (var result in plan.coordination.results)
                Assert.That(text, Does.Not.Contain(result.reasoning), "Private shot-caller/compliance narration must not enter any public/player recap.");
            var maya = plan.evaluations.Single(vote => vote.voterId == ContentCatalog.MayaId);
            Assert.That(text, Does.Not.Contain(WebEvictionVoting.ExplainNative(state, maya, true)));
            Assert.That(text, Does.Not.Contain("blocPressure"));
            Assert.That(text, Does.Not.Contain("bloc_vote"));
            Assert.That(text, Does.Not.Contain("Jamie checked in on me during arrival. I appreciated it."),
                "Maya's owned private memory is not the player's notebook knowledge.");
        }

        private static void AssertBlocPrivateMechanicsUnchanged(EpisodeState before, EpisodeState after)
        {
            Assert.That(after.randomState, Is.EqualTo(before.randomState));
            Assert.That(BlocSerializedRows(after.relationships), Is.EqualTo(BlocSerializedRows(before.relationships)));
            Assert.That(BlocSerializedRows(after.memories), Is.EqualTo(BlocSerializedRows(before.memories)));
            Assert.That(BlocSerializedRows(after.promises), Is.EqualTo(BlocSerializedRows(before.promises)));
            Assert.That(BlocSerializedRows(after.loyaltyOaths), Is.EqualTo(BlocSerializedRows(before.loyaltyOaths)));
            Assert.That(BlocSerializedRows(after.relationshipArcs), Is.EqualTo(BlocSerializedRows(before.relationshipArcs)));
            Assert.That(BlocSerializedRows(after.alliances), Is.EqualTo(BlocSerializedRows(before.alliances)));
            Assert.That(JsonUtility.ToJson(after.playerPersona), Is.EqualTo(JsonUtility.ToJson(before.playerPersona)));
            Assert.That(JsonUtility.ToJson(after.jurySentiment), Is.EqualTo(JsonUtility.ToJson(before.jurySentiment)));
        }

        private static string BlocSerializedRows<T>(IEnumerable<T> rows)
            => string.Join("\n", rows.Select(row => JsonUtility.ToJson(row)));

        private static void AssertBlocPlansEqual(WebNativeEvictionRoundPlan expected, WebNativeEvictionRoundPlan actual)
        {
            // These intentionally transient DTOs are not Unity-serializable. Compare
            // their relevant evidence explicitly rather than comparing two empty "{}".
            Assert.That(actual.coordination.seed, Is.EqualTo(expected.coordination.seed));
            Assert.That(actual.coordination.randomDraws, Is.EqualTo(expected.coordination.randomDraws));
            Assert.That(actual.coordination.results.Select(result => result.reasoning),
                Is.EqualTo(expected.coordination.results.Select(result => result.reasoning)));
            Assert.That(actual.coordination.directives.Select(item => item.voterId + ":" + item.directive + ":" + item.targetNomineeId + ":" + item.allianceId),
                Is.EqualTo(expected.coordination.directives.Select(item => item.voterId + ":" + item.directive + ":" + item.targetNomineeId + ":" + item.allianceId)));
            Assert.That(BlocSerializedRows(actual.evaluations), Is.EqualTo(BlocSerializedRows(expected.evaluations)));
        }

        private void AssertBlocPlanNotSerialized(EpisodeState state)
        {
            // Inspect the real Json.NET save envelope too: Unity's JsonUtility would
            // omit a mistakenly persisted non-[Serializable] coordination field.
            string json = File.ReadAllText(director.SavePath) + "\n" + JsonUtility.ToJson(state);
            foreach (string property in new[] { "coordination", "directives", "compliantVoters", "defectors", "privateReasonCodes", "blocDirective" })
                Assert.That(json, Does.Not.Contain("\"" + property + "\":"), "Detached private diagnostic fields must not become saved/UI state.");
        }
    }
}
