using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Bootstrap;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private const string EpisodeScene = "EpisodeHouse";
        private string temporaryDirectory;
        private EpisodeDirector director;
        private HousePlayerController player;
        private HouseCameraRig cameraRig;
        private Keyboard testKeyboard;
        private Mouse testMouse;

        [UnitySetUp]
        public IEnumerator LoadIsolatedEpisode()
        {
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "GamesimEpisodeTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            EpisodeDirector.SaveRootOverride = temporaryDirectory;
            if (GamesimBootstrap.Instance != null)
            {
                Object.Destroy(GamesimBootstrap.Instance.gameObject);
                yield return null;
            }

            Assert.That(Application.CanStreamedLevelBeLoaded(EpisodeScene), Is.True,
                "EpisodeHouse must be present in build settings.");
            yield return ReloadEpisode();
            Assert.That(Path.GetFullPath(director.SavePath), Does.StartWith(Path.GetFullPath(temporaryDirectory)));
        }

        [UnityTearDown]
        public IEnumerator CleanUpIsolatedEpisode()
        {
            if (testKeyboard != null && testKeyboard.added) InputSystem.RemoveDevice(testKeyboard);
            testKeyboard = null;
            if (testMouse != null && testMouse.added) InputSystem.RemoveDevice(testMouse);
            testMouse = null;
            if (testGamepad != null && testGamepad.added) InputSystem.RemoveDevice(testGamepad);
            testGamepad = null;
            if (director != null) director.ClosePanels();
            var episode = SceneManager.GetSceneByName(EpisodeScene);
            if (episode.IsValid() && episode.isLoaded)
            {
                var cleanupScene = SceneManager.CreateScene("Episode test cleanup " + Guid.NewGuid().ToString("N"));
                SceneManager.SetActiveScene(cleanupScene);
                yield return SceneManager.UnloadSceneAsync(episode);
            }
            EpisodeDirector.SaveRootOverride = null;
            if (GamesimBootstrap.Instance != null) Object.Destroy(GamesimBootstrap.Instance.gameObject);
            yield return null;
            if (!string.IsNullOrEmpty(temporaryDirectory))
            {
                var resolved = Path.GetFullPath(temporaryDirectory);
                Assert.That(resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase), Is.True);
                Assert.That(Path.GetFileName(resolved), Does.StartWith("GamesimEpisodeTests-"));
                if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
            }
        }

        [UnityTest]
        public IEnumerator EpisodeScene_StartsSixDistinctNativeCharactersAndUsableHud()
        {
            var snapshot = director.Snapshot;
            Assert.That(snapshot.contestants, Has.Count.EqualTo(6));
            Assert.That(snapshot.contestants.Count(actor => actor.isPlayer), Is.EqualTo(1));
            Assert.That(snapshot.phase, Is.EqualTo(EpisodePhase.Social));
            Assert.That(player.Agent.isOnNavMesh, Is.True);
            Assert.That(player.InputEnabled, Is.True);
            var visuals = SceneComponents<CharacterPresentation>();
            Assert.That(visuals, Has.Length.EqualTo(6));
            Assert.That(visuals.Select(visual => visual.CharacterId), Is.EquivalentTo(snapshot.contestants.Select(actor => actor.id)));
            foreach (var visual in visuals)
            {
                var body = visual.transform.Find("Gamesim Character Visual");
                Assert.That(body, Is.Not.Null, visual.CharacterId + " needs its native articulated visual.");
                // This used to require more than ten enabled renderers, which was really a count of
                // the primitive rig's parts. An authored rigged model is a single skinned renderer,
                // so that count started failing the moment the cast got real models. Both bodies are
                // correct, so assert the houseguest reads as a person rather than counting pieces.
                var parts = body.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled).ToArray();
                Assert.That(parts, Is.Not.Empty, visual.CharacterId + " needs a visible body.");
                var extent = parts[0].bounds;
                foreach (var part in parts) extent.Encapsulate(part.bounds);
                Assert.That(extent.size.y, Is.GreaterThan(1.2f).And.LessThan(2.6f),
                    visual.CharacterId + " should stand roughly human height, not a stray or collapsed body.");
            }
            Assert.That(director.GetComponentsInChildren<Canvas>(true).Any(canvas => canvas.isActiveAndEnabled), Is.True);
            Assert.That(director.GetComponentsInChildren<Button>(true).Count(button => button.IsActive() && button.IsInteractable()),
                Is.GreaterThanOrEqualTo(2));
            Assert.That(cameraRig.ViewCamera.isActiveAndEnabled, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NearbyNpcPanel_RealPromiseButtonCommitsAndUnlocksExploration()
        {
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            WarpPlayer(new Vector3(0f, 0f, 14f));
            Assert.That(director.TryOpenNpc(maya.Id), Is.False, "NPC conversation must respect world proximity.");
            yield return OpenNearbyNpc(maya);
            Assert.That(director.IsPanelOpen, Is.True);
            Assert.That(player.InputEnabled, Is.False);
            Assert.That(cameraRig.IsConversationFocused, Is.True);
            var before = director.Snapshot;
            var promise = ButtonWithCaption("Promise safety");
            Assert.That(promise.IsInteractable(), Is.True);
            promise.onClick.Invoke();
            yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1));
            Assert.That(after.promises.Any(item => item.fromId == after.playerId && item.toId == maya.Id
                && item.kind == PromiseKind.Safety && item.status == PromiseStatus.Active), Is.True);
            Assert.That(File.Exists(director.SavePath), Is.True, "A committed social action must reach local persistence.");
            director.ClosePanels();
            Assert.That(director.IsPanelOpen, Is.False);
            Assert.That(player.InputEnabled, Is.True);
            Assert.That(cameraRig.IsConversationFocused, Is.False);
        }

        [UnityTest]
        public IEnumerator EpisodeStations_GateDistanceAndButtonsAdvanceThenResolveCompetition()
        {
            WarpPlayer(new Vector3(0f, 0f, 14f));
            Assert.That(director.TryOpenPhasePanel(), Is.False);
            Assert.That(director.IsPanelOpen, Is.False);
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            Assert.That(player.InputEnabled, Is.False);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.HoH));
            Assert.That(director.IsPanelOpen, Is.False, "Changing room stations should close the old phase panel.");
            Assert.That(player.InputEnabled, Is.True);
            Assert.That(director.TryOpenPhasePanel(), Is.False,
                "The new competition station must be reached in the yard.");
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Accessible alternative: steady 1-point bonus").onClick.Invoke();
            yield return null;
            var result = director.Snapshot;
            Assert.That(result.phase, Is.EqualTo(EpisodePhase.HoH));
            Assert.That(result.competitionResolved, Is.True);
            Assert.That(result.competitionScores, Has.Count.EqualTo(6));
            Assert.That(result.competitionScores.All(score => !double.IsNaN(score.score) && !double.IsInfinity(score.score)), Is.True);
            Assert.That(result.hohId, Is.Not.Null.And.Not.Empty);

            yield return ContinueCompetitionResults(byKeyboard: false);
            ButtonWithCaption("Review competition results").onClick.Invoke();
            yield return null;
            var resultsCard = SceneComponents<CompetitionResult>().Single();
            Assert.That(resultsCard.IsPlaying, Is.True, "Saved standings must be available to review.");
            director.ClosePanels();
            yield return null;
            Assert.That(resultsCard.IsPlaying, Is.False, "Explicitly closing panels must retire the persistent results scrim.");
            Assert.That(director.IsPanelOpen, Is.False);
            Assert.That(player.InputEnabled, Is.True);

            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Review competition results").onClick.Invoke();
            yield return null;
            Assert.That(resultsCard.IsPlaying, Is.True);
            director.LoadNow();
            yield return null; yield return null;
            Assert.That(resultsCard.IsPlaying, Is.False, "Loading must discard presentation belonging to the replaced session.");
            Assert.That(director.IsPanelOpen, Is.False);
            Assert.That(player.InputEnabled, Is.True);
            Assert.That(director.Snapshot.revision, Is.EqualTo(result.revision));
            Assert.That(director.Snapshot.competitionResolved, Is.True, "Dismissing or loading results never replays the competition.");
        }

        [UnityTest]
        public IEnumerator SavedEpisode_ReloadsSameRunAndContinuesWithoutRepeatingCommands()
        {
            for (var index = 0; index < 4; index++)
            {
                var command = NextCommand(director.Snapshot);
                var result = director.Submit(command);
                Assert.That(result.accepted, Is.True, result.reason);
                yield return null;
            }
            director.SaveNow();
            var saved = director.Snapshot;
            var path = director.SavePath;
            Assert.That(new EpisodeSaveStore(path).TryLoad(out var disk, out var message), Is.True, message);
            AssertEquivalent(saved, disk);
            yield return ReloadEpisode();
            Assert.That(director.SavePath, Is.EqualTo(path));
            AssertEquivalent(saved, director.Snapshot);
            var next = NextCommand(director.Snapshot);
            var accepted = director.Submit(next);
            Assert.That(accepted.accepted, Is.True, accepted.reason);
            var committed = director.Snapshot;
            Assert.That(director.Submit(next).duplicate, Is.True);
            AssertEquivalent(committed, director.Snapshot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissingPrimary_WithGoodBackupRequiresExplicitRecovery()
        {
            director.SaveNow();
            var original = director.Snapshot;
            var command = NextCommand(original);
            Assert.That(director.Submit(command).accepted, Is.True);
            var primary = director.SavePath;
            Assert.That(File.Exists(primary + ".backup"), Is.True);
            Assert.That(Path.GetDirectoryName(Path.GetFullPath(primary)), Is.EqualTo(Path.GetFullPath(temporaryDirectory)));
            File.Delete(primary);
            yield return ReloadEpisode();
            Assert.That(director.StatusMessage, Does.Contain("Recover backup"));
            Assert.That(director.IsPanelOpen, Is.True);
            Assert.That(player.InputEnabled, Is.False);
            Assert.That(director.Submit(NextCommand(director.Snapshot)).accepted, Is.False);
            Assert.That(File.Exists(primary), Is.False, "An unresolved backup must not be replaced by an automatic new run.");
            director.RecoverBackup();
            AssertEquivalent(original, director.Snapshot);
            Assert.That(File.Exists(primary), Is.True);
            Assert.That(File.Exists(primary + ".backup"), Is.True);
            Assert.That(player.InputEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator NewSlotWriteFailure_PreservesCurrentRunAndSlot()
        {
            director.SaveNow();
            var before = director.Snapshot;
            var previousPath = director.SavePath;
            var beforeBytes = File.ReadAllBytes(previousPath);
            var blockedRoot = Path.Combine(temporaryDirectory, "cannot-be-a-directory");
            File.WriteAllText(blockedRoot, "test-owned obstruction");
            var rootField = typeof(EpisodeDirector).GetField("saveRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rootField, Is.Not.Null);
            rootField.SetValue(director, blockedRoot);
            try
            {
                Assert.DoesNotThrow(() => director.StartSeason(null));
                Assert.That(director.SavePath, Is.EqualTo(previousPath));
                AssertEquivalent(before, director.Snapshot);
                Assert.That(File.ReadAllBytes(previousPath), Is.EqualTo(beforeBytes));
                Assert.That(director.StatusMessage, Does.Not.Contain("New season started"));
            }
            finally
            {
                rootField.SetValue(director, temporaryDirectory);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ModalKeyboardFocus_IsRestoredAndScrollsToSelectedControls()
        {
            director.OpenSettings();
            yield return null;
            yield return null;
            ButtonWithCaption("Use larger text").onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(director.GetComponent<EpisodeHud>().FontScale, Is.EqualTo(1.2f).Within(0.001f));
            Canvas.ForceUpdateCanvases();
            var modal = director.GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.name == "Episode panel" && rect.gameObject.activeInHierarchy);
            Assert.That(modal.GetComponentsInChildren<TMPro.TMP_Text>().Any(text => text.fontSize == Mathf.RoundToInt(21 * 1.2f)), Is.True,
                "Scrollable panel text must use the selected larger font size.");
            var chrome = director.GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.name == "Navigation" && rect.gameObject.activeInHierarchy);
            Assert.That(chrome.GetComponentsInChildren<TMPro.TMP_Text>().All(text => text.enableAutoSizing), Is.True,
                "Fixed navigation labels need bounded fitting at the larger text setting.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject.transform.IsChildOf(modal), Is.True);
            var importButton = ButtonWithCaption("Archive and import this file");
            importButton.Select();
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var scroll = modal.GetComponentInChildren<ScrollRect>();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, importButton.transform);
            Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin - 1f));
            Assert.That(bounds.max.y, Is.LessThanOrEqualTo(scroll.viewport.rect.yMax + 1f));
            var saveButton = ButtonWithCaption("Save now  [F5]");
            saveButton.Select();
            saveButton.onClick.Invoke();
            yield return null;
            yield return null;
            var restored = ButtonWithCaption("Save now  [F5]");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(restored.gameObject),
                "Rebuilding a modal must restore its prior keyboard selection.");
            var world = ButtonWithCaption("Notebook [J]");
            Assert.That(world.navigation.mode, Is.EqualTo(Navigation.Mode.None));
            world.Select();
            yield return null;
            yield return null;
            var currentModal = director.GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.name == "Episode panel" && rect.gameObject.activeInHierarchy);
            Assert.That(EventSystem.current.currentSelectedGameObject.transform.IsChildOf(currentModal), Is.True,
                "World navigation must not capture keyboard focus while a modal is open.");
        }

        [UnityTest]
        public IEnumerator Director_CompletesEntireSeasonWithSavedFinaleAndValidatedProjection()
        {
            var seen = new HashSet<EpisodePhase>();
            var count = 0;
            while (director.Snapshot.phase != EpisodePhase.Finished && count++ < 150)
            {
                yield return ContinueCompetitionResults(byKeyboard: false);
                var before = director.Snapshot;
                seen.Add(before.phase);
                var command = NextCommand(before);
                var result = director.Submit(command);
                Assert.That(result.accepted, Is.True, before.phase + " / " + command.kind + ": " + result.reason);
                Assert.That(EpisodeValidation.TryValidate(result.state, out var reason), Is.True, reason);
                yield return null;
            }
            var finale = director.Snapshot;
            Assert.That(finale.phase, Is.EqualTo(EpisodePhase.Finished), "The director must reach a complete finale within the command bound.");
            Assert.That(finale.week, Is.EqualTo(4));
            Assert.That(finale.contestants.Count(actor => actor.status == ContestantStatus.Jury), Is.EqualTo(4));
            Assert.That(finale.contestants.Count(actor => actor.status == ContestantStatus.Winner), Is.EqualTo(1));
            Assert.That(finale.contestants.Count(actor => actor.status == ContestantStatus.RunnerUp), Is.EqualTo(1));
            Assert.That(finale.winnerId, Is.Not.EqualTo(finale.runnerUpId));
            foreach (var phase in new[] { EpisodePhase.Social, EpisodePhase.HoH, EpisodePhase.Nomination,
                EpisodePhase.VetoSelection, EpisodePhase.Veto, EpisodePhase.VetoMeeting, EpisodePhase.Campaign,
                EpisodePhase.Eviction, EpisodePhase.FinalHoHPart1, EpisodePhase.FinalHoHPart2, EpisodePhase.FinalHoHPart3,
                EpisodePhase.FinalEviction, EpisodePhase.JuryQuestioning, EpisodePhase.FinalSpeeches, EpisodePhase.Jury })
                Assert.That(seen, Does.Contain(phase), "The season skipped " + phase);
            Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var persisted, out var message), Is.True, message);
            AssertEquivalent(finale, persisted);
            Assert.That(File.Exists(director.SavePath + ".backup"), Is.True);
            Assert.That(director.TryOpenPhasePanel(), Is.True, "The completed season should expose its finale for review.");
            yield return null;
            Assert.That(director.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(text => text.gameObject.activeInHierarchy
                && text.text == "Winner: " + finale.Find(finale.winnerId).name
                    + ". Runner-up: " + finale.Find(finale.runnerUpId).name + "."), Is.True);
        }

        private IEnumerator ReloadEpisode()
        {
            // This exact informational startup line is expected on every successful load.
            // Keep strict teardown checks sensitive to every unrelated log and warning.
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(
                "^Gamesim episode ready: [0-9]+ contestants, validated simulation, local recovery and accessible HUD connected\\.$"));
#if !GAMESIM_UMA
            // The shipping scene retains its optional UMA-cast component. In the authored-only
            // configuration Unity reports exactly this missing-script warning on scene load;
            // account for it explicitly so strict teardown checks still catch every other log.
            LogAssert.Expect(LogType.Warning, "The referenced script (Unknown) on this Behaviour is missing!");
#endif
            yield return SceneManager.LoadSceneAsync(EpisodeScene, LoadSceneMode.Single);
            director = SceneComponents<EpisodeDirector>().Single();
            var deadline = Time.realtimeSinceStartup + 10f;
            while (!director.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.IsReady, Is.True, "Episode startup did not finish.");
            player = SceneComponents<HousePlayerController>().Single();
            cameraRig = SceneComponents<HouseCameraRig>().Single();
            yield return null;
        }

        private IEnumerator OpenNearbyNpc(HouseNpc npc)
        {
            // Test placement uses the actual baked surface and lets the runtime decide line of sight.
            for (var direction = 0; direction < 8; direction++)
            {
                var angle = direction * Mathf.PI / 4f;
                var candidate = npc.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.7f;
                if (!NavMesh.SamplePosition(candidate, out var hit, 0.5f, player.Agent.areaMask)) continue;
                if (!player.Agent.Warp(hit.position)) continue;
                player.Agent.ResetPath();
                Physics.SyncTransforms();
                if (director.TryOpenNpc(npc.Id))
                {
                    yield return null;
                    yield break;
                }
            }
            Assert.Fail("No reachable, visible nearby interaction point found for " + npc.DisplayName);
        }

        private void WarpPlayer(Vector3 target)
        {
            Assert.That(NavMesh.SamplePosition(target, out var hit, 0.75f, player.Agent.areaMask), Is.True);
            Assert.That(player.Agent.Warp(hit.position), Is.True);
            player.Agent.ResetPath();
            Physics.SyncTransforms();
        }

        private Button ButtonWithCaption(string caption)
        {
            return director.GetComponentsInChildren<Button>(true).Single(button => button.IsActive()
                && button.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(text => text.text == caption));
        }

        private static T[] SceneComponents<T>() where T : Component
            => SceneManager.GetSceneByName(EpisodeScene).GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static EpisodeCommand NextCommand(EpisodeState state)
        {
            var command = new EpisodeCommand
            {
                id = "play-mode-" + state.revision, actorId = state.playerId,
                expectedRevision = state.revision, expectedPhase = state.phase, kind = EpisodeCommandKind.Advance
            };
            if (state.pendingDiary != null)
            {
                command.kind = EpisodeCommandKind.SkipDiary;
                command.targetId = state.pendingDiary.id;
                return command;
            }
            if (EpisodeEngine.IsCompetition(state.phase) && !state.competitionResolved && EpisodeEngine.CompetitionPlayers(state).Any(actor => actor.isPlayer))
            {
                command.kind = EpisodeCommandKind.Compete;
                command.performance = 0.5;
            }
            else if (state.phase == EpisodePhase.Nomination && state.nominees.Count == 0 && state.hohId == state.playerId)
            {
                command.kind = EpisodeCommandKind.Nominate;
                var candidates = EpisodeEngine.NominationCandidates(state).ToArray();
                command.targetId = candidates[0].id;
                command.secondTargetId = candidates[1].id;
            }
            else if (state.phase == EpisodePhase.VetoMeeting && !state.vetoResolved
                && (state.vetoHolderId == state.playerId || state.hohId == state.playerId && EpisodeEngine.NpcVetoSave(state) != null))
            {
                command.kind = EpisodeCommandKind.ResolveVeto;
                var replacement = EpisodeEngine.ReplacementCandidates(state).FirstOrDefault();
                // At the final four a veto holder who is not on the block may not use it. The
                // EditMode driver has always checked this; this one did not, and only never hit it
                // because no season had reached that shape. Driving into a rule the engine enforces
                // tests the driver, not the game.
                command.useVeto = replacement != null && !EpisodeEngine.VetoIsLockedAtFinalFour(state);
                command.targetId = command.useVeto ? state.vetoHolderId == state.playerId ? state.nominees[0] : EpisodeEngine.NpcVetoSave(state) : null;
                command.secondTargetId = replacement?.id;
            }
            else if (state.phase == EpisodePhase.Eviction && state.evictionStage == EvictionStage.Speeches
                && state.nominees.Contains(state.playerId)
                && !state.evictionSpeeches.Any(speech => speech.speakerId == state.playerId))
            {
                command.kind = EpisodeCommandKind.SubmitEvictionSpeech;
                command.text = ""; // The complete-season route exercises the explicit "say nothing" path.
            }
            else if (state.phase == EpisodePhase.Eviction && !state.evictionResolved
                && (state.evictionStage == EvictionStage.Voting || state.evictionStage == EvictionStage.Tiebreaker)
                && !state.votes.Any(vote => vote.voterId == state.playerId)
                && (EpisodeEngine.Voters(state).Any(actor => actor.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(state)))
            {
                command.kind = EpisodeCommandKind.CastVote;
                command.targetId = state.nominees[0];
            }
            else if (state.phase == EpisodePhase.FinalEviction && state.hohId == state.playerId)
            {
                command.kind = EpisodeCommandKind.FinalEvict;
                command.targetId = state.Active.First(actor => !actor.isPlayer).id;
            }
            else if (state.phase == EpisodePhase.JuryQuestioning && !state.juryExchanges[state.juryQuestionIndex].completed)
            {
                var exchange = state.juryExchanges[state.juryQuestionIndex];
                command.kind = EpisodeCommandKind.AnswerJury;
                command.targetId = exchange.finalistId == state.playerId ? exchange.questionerId : exchange.finalistId;
                command.secondTargetId = exchange.finalistId == state.playerId ? "A" : "neutral";
            }
            else if (state.phase == EpisodePhase.FinalSpeeches && state.Active.Any(actor => actor.isPlayer)
                && !state.finalSpeeches.Any(speech => speech.speakerId == state.playerId))
            {
                command.kind = EpisodeCommandKind.SubmitSpeech;
                command.text = ""; // The complete-season route deliberately exercises explicit speech skip.
            }
            else if (state.phase == EpisodePhase.Jury && !state.Active.Any(actor => actor.isPlayer)
                && !state.votes.Any(vote => vote.voterId == state.playerId))
            {
                command.kind = EpisodeCommandKind.CastVote;
                command.targetId = state.Active.First().id;
            }
            return command;
        }

        [UnityTest]
        public IEnumerator EntireSeason_UsesReachableStationsAndActualCeremonyButtons()
        {
            // Accelerated navigation validates paths and UI wiring, not human pacing or mouse accuracy.
            player.Agent.speed = 25; player.Agent.acceleration = 100;
            int count = 0, walkedRoutes = 0;
            while (director.Snapshot.phase != EpisodePhase.Finished && count++ < 150)
            {
                yield return ContinueCompetitionResults(byKeyboard: false);
                var before = director.Snapshot;
                var next = NextCommand(before);
                if (!director.IsPanelOpen)
                {
                    ButtonWithCaption("Go to episode screen").onClick.Invoke();
                    if (before.Find(before.playerId).status == ContestantStatus.Active)
                    {
                        var deadline = Time.realtimeSinceStartup + 12;
                        while (!player.HasArrived && Time.realtimeSinceStartup < deadline) yield return null;
                        Assert.That(player.HasArrived, Is.True, "The phase station must be reachable by actual navigation.");
                        walkedRoutes++;
                    }
                    Assert.That(director.TryOpenPhasePanel(), Is.True, before.phase + " station could not be opened.");
                    yield return null;
                }
                string caption;
                switch (next.kind)
                {
                    case EpisodeCommandKind.SkipDiary: caption = EpisodeHud.DiarySkipReflectionCaption; break;
                    case EpisodeCommandKind.Compete: caption = "Accessible alternative: steady 1-point bonus"; break;
                    case EpisodeCommandKind.Nominate:
                        ButtonWithCaption(before.Find(next.targetId).name).onClick.Invoke();
                        ButtonWithCaption(before.Find(next.secondTargetId).name).onClick.Invoke();
                        caption = "Commit nominations"; break;
                    case EpisodeCommandKind.ResolveVeto:
                        caption = !next.useVeto ? "Do not use the veto" : before.hohId == before.playerId
                            ? before.Find(next.secondTargetId).name : "Save " + before.Find(next.targetId).name + " (HoH chooses replacement)";
                        break;
                    case EpisodeCommandKind.CastVote:
                        caption = before.phase == EpisodePhase.Jury ? "Vote for " + before.Find(next.targetId).name + " to win"
                            : "Vote to evict " + before.Find(next.targetId).name; break;
                    case EpisodeCommandKind.SubmitEvictionSpeech: caption = EpisodeHud.EvictionSpeechSkipCaption; break;
                    case EpisodeCommandKind.FinalEvict: caption = "Evict " + before.Find(next.targetId).name; break;
                    case EpisodeCommandKind.AnswerJury:
                        var exchange = before.juryExchanges[before.juryQuestionIndex];
                        caption = exchange.finalistId == before.playerId
                            ? next.secondTargetId + " · " + (next.secondTargetId == "A" ? exchange.optionA : exchange.optionB)
                            : next.secondTargetId + " · " + WebJuryQuestioning.GetJurorQuestionOptions(before.juryQuestionIndex)
                                .Single(option => option.tone == next.secondTargetId).text;
                        break;
                    case EpisodeCommandKind.SubmitSpeech: caption = EpisodeHud.SpeechSkipCaption; break;
                    default:
                        caption = EpisodeEngine.IsCompetition(before.phase)
                            ? before.competitionResolved ? "Continue to the next ceremony" : "Watch eligible housemates compete"
                            : before.phase == EpisodePhase.Social ? "Begin the next competition"
                            : before.phase == EpisodePhase.Campaign ? "Close campaigning and open voting"
                            : before.phase == EpisodePhase.JuryQuestioning ? EpisodeHud.JuryContinueCaption
                            : before.phase == EpisodePhase.FinalSpeeches ? EpisodeHud.SpeechContinueCaption : "Continue episode";
                        break;
                }
                // A player who is both HoH and veto holder has one replacement group per saved nominee;
                // the first group intentionally corresponds to NextCommand's first nominee.
                var button = director.GetComponentsInChildren<Button>(true).FirstOrDefault(item => item.IsActive()
                    && item.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(text => text.text == caption));
                // What the panel is actually showing, when it is not showing what was expected.
                // This walk has cost two runs to guesswork already; a missing control should say
                // which controls were there instead.
                Assert.That(button, Is.Not.Null, before.phase + " is missing action: " + caption
                    + "  ·  panel open: " + director.IsPanelOpen
                    + "  ·  visible: " + string.Join(" | ", director.GetComponentsInChildren<Button>(true)
                        .Where(item => item.IsActive())
                        .SelectMany(item => item.GetComponentsInChildren<TMPro.TMP_Text>(true))
                        .Select(text => text.text).Distinct().Take(20)));
                button.onClick.Invoke();
                yield return null; yield return null;
                Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1), before.phase + ": " + director.StatusMessage);
            }
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(walkedRoutes, Is.GreaterThanOrEqualTo(3));
            Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var saved, out var message), Is.True, message);
            Assert.That(saved.phase, Is.EqualTo(EpisodePhase.Finished));
        }

        [UnityTest]
        public IEnumerator ExpandedCast_LeavesAllFiveRoomDestinationsReachable()
        {
            player.Agent.speed = 25; player.Agent.acceleration = 100;
            yield return null; yield return null; // Let each active NPC carving obstacle register.
            var rooms = SceneComponents<HouseRoomMarker>().OrderBy(room => room.RoomName, StringComparer.Ordinal).ToArray();
            // The south wing brought this from five to eight. Kept as an exact count rather
            // than a minimum: this test walks to every room it finds, so a marker that goes
            // missing would otherwise make the suite quietly test less.
            Assert.That(rooms, Has.Length.EqualTo(8));
            foreach (var room in rooms)
            {
                Assert.That(player.TryMoveTo(room.transform.position), Is.True, room.RoomName + " route overlaps an obstacle or is disconnected.");
                var deadline = Time.realtimeSinceStartup + 12;
                while (!player.HasArrived && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(player.HasArrived, Is.True, "Could not walk into " + room.RoomName);
                Assert.That(Vector3.Distance(player.transform.position, room.transform.position), Is.LessThan(1));
            }
        }

        [UnityTest]
        public IEnumerator FinalistJuryAnswer_UsesActualButtonAndReloadPreservesQuestionAndAnswer()
        {
            yield return InstallFinaleFixture(true);
            var pending = director.Snapshot;
            yield return ReloadEpisode();
            AssertEquivalent(pending,director.Snapshot);
            yield return OpenFinalePanel();
            var question = pending.juryExchanges[pending.juryQuestionIndex];
            var questionLabel = director.GetComponentsInChildren<TMPro.TMP_Text>().Single(text => text.name == "Jury question");
            Assert.That(questionLabel.text, Is.EqualTo(question.question));
            ButtonWithCaption("A · " + question.optionA).onClick.Invoke();
            yield return null; yield return null;
            var answered = director.Snapshot;
            Assert.That(answered.revision, Is.EqualTo(pending.revision + 1));
            Assert.That(answered.juryQuestionIndex, Is.EqualTo(pending.juryQuestionIndex), "A committed answer waits for explicit Continue.");
            Assert.That(answered.juryExchanges[answered.juryQuestionIndex].completed, Is.True);
            Assert.That(answered.juryExchanges[answered.juryQuestionIndex].answerChoice, Is.EqualTo("A"));
            Assert.That(answered.juryExchanges[answered.juryQuestionIndex].answer, Is.EqualTo(question.optionA));
            Assert.That(director.GetComponentsInChildren<TMPro.TMP_Text>().Single(text => text.name == "Jury answer").text, Is.EqualTo(question.optionA));
            yield return ReloadEpisode();
            AssertEquivalent(answered,director.Snapshot);
            yield return OpenFinalePanel();
            Assert.That(director.GetComponentsInChildren<Button>().Any(button => button.IsActive() && button.name == "A · " + question.optionA), Is.False);
            ButtonWithCaption(EpisodeHud.JuryContinueCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.Snapshot.juryQuestionIndex, Is.EqualTo(answered.juryQuestionIndex + 1));
            Assert.That(director.Snapshot.juryExchanges.Last().completed, Is.False);
        }

        [UnityTest]
        public IEnumerator PlayerJuror_ToneButtonRecordsFallbackAnswerWithoutInventingTrustChanges()
        {
            yield return InstallFinaleFixture(false);
            yield return OpenFinalePanel();
            var before = director.Snapshot;
            var option = WebJuryQuestioning.GetJurorQuestionOptions(before.juryQuestionIndex).Single(item => item.tone == "neutral");
            ButtonWithCaption(option.tone + " · " + option.text).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            var exchange = after.juryExchanges[after.juryQuestionIndex];
            Assert.That(exchange.completed, Is.True);
            Assert.That(exchange.questionerId, Is.EqualTo(after.playerId));
            Assert.That(exchange.question, Is.EqualTo(option.text));
            Assert.That(exchange.tone, Is.EqualTo("neutral"));
            Assert.That(exchange.answer, Is.Not.Null.And.Not.Empty);
            Assert.That(after.relationships.Select(edge => edge.score), Is.EqualTo(before.relationships.Select(edge => edge.score)),
                "The source player-juror offline path has no relationship consequence.");
            yield return ReloadEpisode();
            AssertEquivalent(after,director.Snapshot);
            yield return OpenFinalePanel();
            Assert.That(director.GetComponentsInChildren<TMPro.TMP_Text>().Single(text => text.name == "Jury answer").text, Is.EqualTo(exchange.answer));
        }

        [UnityTest]
        public IEnumerator FinalSpeech_TypedDraftSurvivesRebuildAndKeyboardCloseThenCommittedTextReloads()
        {
            yield return InstallFinaleFixture(true);
            yield return OpenFinalePanel();
            ButtonWithCaption(EpisodeHud.JurySkipCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.FinalSpeeches));
            var beforeDraft = director.Snapshot;
            var input = SpeechInput();
            Assert.That(input.characterLimit, Is.EqualTo(2000));
            Assert.That(input.lineType, Is.EqualTo(TMPro.TMP_InputField.LineType.MultiLineNewline));
            input.Select(); input.ActivateInputField();
            yield return null; yield return null;
            const string draft = "I kept my promises.\nI made my own decisions.";
            // Exercise uGUI's actual key-edit path instead of assigning the completed speech.
            foreach (char character in draft)
                input.ProcessEvent(new Event { type = EventType.KeyDown, character = character,
                    keyCode = character == '\n' ? KeyCode.Return : KeyCode.None });
            input.ForceLabelUpdate();
            Assert.That(input.text, Is.EqualTo(draft));
            AssertEquivalent(beforeDraft,director.Snapshot);
            director.SaveNow(); // Rebuild the HUD without committing the unsent draft.
            yield return null; yield return null;
            Assert.That(SpeechInput().text, Is.EqualTo(draft));
            Assert.That(director.Snapshot.finalSpeeches.Any(speech => speech.speakerId == beforeDraft.playerId), Is.False);
            testKeyboard = InputSystem.AddDevice<Keyboard>();
            SpeechInput().Select(); SpeechInput().ActivateInputField();
            yield return null; yield return null;
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.Tab));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());
            yield return null; yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(ButtonWithCaption(EpisodeHud.SpeechSubmitCaption).gameObject));
            Assert.That(SpeechInput().text, Is.EqualTo(draft), "Tab must move focus without inserting a tab character.");
            SpeechInput().Select(); SpeechInput().ActivateInputField();
            yield return null; yield return null;
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());
            yield return null; yield return null;
            Assert.That(director.IsPanelOpen, Is.False, "Escape must close the panel even while editing.");
            AssertEquivalent(beforeDraft,director.Snapshot);
            yield return OpenFinalePanel();
            Assert.That(SpeechInput().text, Is.EqualTo(draft), "Closing the view must not silently erase the draft.");
            ButtonWithCaption(EpisodeHud.SpeechSubmitCaption).onClick.Invoke();
            yield return null; yield return null;
            var committed = director.Snapshot;
            var speech = committed.finalSpeeches.Single(item => item.speakerId == committed.playerId);
            Assert.That(speech.text, Is.EqualTo(draft));
            Assert.That(speech.isPlayerAuthored, Is.True);
            Assert.That(committed.revision, Is.EqualTo(beforeDraft.revision + 1));
            yield return ReloadEpisode();
            AssertEquivalent(committed,director.Snapshot);
            yield return OpenFinalePanel();
            Assert.That(director.GetComponentsInChildren<TMPro.TMP_InputField>().Any(field => field.name == "Final speech draft"), Is.False);
            Assert.That(ButtonWithCaption(EpisodeHud.SpeechContinueCaption), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator FinalSpeech_ExplicitSkipCommitsBlankRecordAndContinuesToVoting()
        {
            yield return InstallFinaleFixture(true);
            yield return OpenFinalePanel();
            ButtonWithCaption(EpisodeHud.JurySkipCaption).onClick.Invoke();
            yield return null; yield return null;
            var before = director.Snapshot;
            ButtonWithCaption(EpisodeHud.SpeechSkipCaption).onClick.Invoke();
            yield return null; yield return null;
            var skipped = director.Snapshot;
            Assert.That(skipped.phase, Is.EqualTo(EpisodePhase.FinalSpeeches));
            Assert.That(skipped.revision, Is.EqualTo(before.revision + 1));
            Assert.That(skipped.finalSpeeches.Single(speech => speech.speakerId == skipped.playerId).text, Is.Empty);
            ButtonWithCaption(EpisodeHud.SpeechContinueCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.Jury));
        }

        // The HUD's speech field is an EpisodeSpeechInputField, which derives from TMP_InputField.
        // This helper looked for the legacy uGUI InputField and was missed when the HUD migrated to
        // TextMeshPro, exactly as ActiveDiaryText was caught and updated at the time.
        private TMPro.TMP_InputField SpeechInput() => director.GetComponentsInChildren<TMPro.TMP_InputField>()
            .Single(input => input.gameObject.activeInHierarchy && input.name == "Final speech draft");

        private IEnumerator OpenFinalePanel()
        {
            if (director.Snapshot.Find(director.Snapshot.playerId).status == ContestantStatus.Active)
                WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null; yield return null;
        }

        private IEnumerator InstallFinaleFixture(bool playerFinalist)
        {
            // Generate a legal authoritative history, then install it in this test's isolated slot.
            // The full reachable-station test separately covers ordinary whole-season navigation.
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 60 && fixture == null; seed++)
            {
                var engine = new EpisodeEngine(ContentCatalog.Create(seed));
                int guard = 0;
                while (engine.Snapshot.phase != EpisodePhase.JuryQuestioning && guard++ < 150)
                {
                    var result = engine.Apply(NextCommand(engine.Snapshot));
                    Assert.That(result.accepted, Is.True, result.reason);
                }
                var candidate = engine.Snapshot;
                if (candidate.phase == EpisodePhase.JuryQuestioning && candidate.Active.Any(actor => actor.isPlayer) == playerFinalist)
                    fixture = candidate;
            }
            Assert.That(fixture, Is.Not.Null, "No bounded legal finale fixture found for the required player role.");
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            AssertEquivalent(fixture,director.Snapshot);
        }

        private static void AssertEquivalent(EpisodeState expected, EpisodeState actual)
        {
            Assert.That(actual.sessionId, Is.EqualTo(expected.sessionId));
            Assert.That(actual.revision, Is.EqualTo(expected.revision));
            Assert.That(actual.randomState, Is.EqualTo(expected.randomState));
            Assert.That(actual.phase, Is.EqualTo(expected.phase));
            Assert.That(actual.winnerId, Is.EqualTo(expected.winnerId));
            Assert.That(actual.runnerUpId, Is.EqualTo(expected.runnerUpId));
            Assert.That(actual.relationships.Select(edge => edge.score), Is.EqualTo(expected.relationships.Select(edge => edge.score)));
            Assert.That(actual.events.Select(item => item.text), Is.EqualTo(expected.events.Select(item => item.text)));
            Assert.That(actual.acceptedCommandIds, Is.EqualTo(expected.acceptedCommandIds));
            Assert.That(JsonUtility.ToJson(actual), Is.EqualTo(JsonUtility.ToJson(expected)), "Every serialized finale/history field must survive unchanged.");
        }
    }
}
