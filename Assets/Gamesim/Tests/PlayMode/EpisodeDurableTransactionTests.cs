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
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Real scene/director/store regression tests. The Editor-only save seam injects
    /// faults at the persistence boundary, never changes the authoritative engine,
    /// and is cleared before teardown. Only generated, isolated test slots are touched.
    /// </summary>
    public sealed class EpisodeDurableTransactionTests
    {
        private const string EpisodeScene = "EpisodeHouse";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private string isolatedRoot, previousSaveRoot;
        private EpisodeDirector director;
        private HousePlayerController player;
        private HouseCameraRig cameraRig;

        [UnitySetUp]
        public IEnumerator LoadIsolatedDurableEpisode()
        {
            if (!Application.isEditor) Assert.Ignore("Fault injection is intentionally Editor-only.");
            previousSaveRoot = EpisodeDirector.SaveRootOverride;
            isolatedRoot = Path.Combine(Path.GetTempPath(), "GamesimDurableTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(isolatedRoot);
            EpisodeDirector.SaveRootOverride = isolatedRoot;
            if (GamesimBootstrap.Instance != null)
            {
                Object.Destroy(GamesimBootstrap.Instance.gameObject);
                yield return null;
            }
            Assert.That(Application.CanStreamedLevelBeLoaded(EpisodeScene), Is.True);
            yield return SceneManager.LoadSceneAsync(EpisodeScene, LoadSceneMode.Single);
            director = SceneObjects<EpisodeDirector>().Single();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!director.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.IsReady, Is.True, "The real episode scene did not initialize.");
            // Ordinary tests isolate the durable boundary from elapsed autonomous time.
            // The dedicated whole-tick cases explicitly restore the real runtime below.
            director.SuspendNpcAutonomyForDiagnostics();
            player = SceneObjects<HousePlayerController>().Single();
            cameraRig = SceneObjects<HouseCameraRig>().Single();
            Assert.That(player.Agent.isOnNavMesh, Is.True);
            Assert.That(Path.GetDirectoryName(Path.GetFullPath(director.SavePath)), Is.EqualTo(Path.GetFullPath(isolatedRoot)));
            director.SaveNow();
            director.SaveNow(); // A validated recovery copy exists before any deliberate corruption.
            Assert.That(File.Exists(director.SavePath), Is.True);
            Assert.That(File.Exists(director.SavePath + ".backup"), Is.True);
            AssertDiskEquals(director.Snapshot);
        }

        [UnityTearDown]
        public IEnumerator CleanUpDurableEpisode()
        {
            if (isolatedRoot == null) yield break; // An Editor-only ignored setup owns no scene, slot or preference.
            if (director != null)
            {
                SetSaveHook(null);
                director.SuspendNpcAutonomyForDiagnostics();
            }
            var episode = SceneManager.GetSceneByName(EpisodeScene);
            if (episode.IsValid() && episode.isLoaded)
            {
                var cleanup = SceneManager.CreateScene("Durable test cleanup " + Guid.NewGuid().ToString("N"));
                SceneManager.SetActiveScene(cleanup);
                yield return SceneManager.UnloadSceneAsync(episode);
            }
            EpisodeDirector.SaveRootOverride = previousSaveRoot;
            if (GamesimBootstrap.Instance != null) Object.Destroy(GamesimBootstrap.Instance.gameObject);
            yield return null;
            string resolved = Path.GetFullPath(isolatedRoot);
            string tempParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            Assert.That(resolved.StartsWith(tempParent, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(resolved), Does.StartWith("GamesimDurableTests-"));
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }

        [UnityTest]
        public IEnumerator FailedUiDecisionBeforeWrite_PreservesFullStateFilesAndBothRandomStreams()
        {
            yield return OpenNearbyMaya();
            var before = director.Snapshot;
            var files = CaptureFiles();
            int writes = 0;
            EpisodeState attempted = null;
            SetSaveHook(candidate =>
            {
                writes++;
                attempted = candidate.Clone();
                throw new IOException("Injected before-write failure.");
            });

            ButtonWithCaption("Spend time together").onClick.Invoke();

            Assert.That(writes, Is.EqualTo(1));
            Assert.That(attempted, Is.Not.Null);
            Assert.That(attempted.revision, Is.EqualTo(before.revision + 1));
            Assert.That(attempted.socialActions, Is.EqualTo(before.socialActions + 1));
            Assert.That(attempted.randomState, Is.Not.EqualTo(before.randomState), "The rejected candidate exercised a real random-consuming social operation.");
            AssertStateEquals(before, director.Snapshot);
            AssertFilesEqual(files);
            Assert.That(director.StatusMessage, Does.Contain("not committed"));
            Assert.That(director.StatusMessage, Does.Not.Contain("Saved locally"));
            Assert.That(GetField<bool>("npcSaveSuspended"), Is.True);
            AssertDiskEquals(before);
            yield break;
        }

        [UnityTest]
        public IEnumerator ExceptionAfterRealReplacement_InstallsExactCandidateAndDuplicateDoesNotSaveAgain()
        {
            var before = director.Snapshot;
            var command = Talk(before);
            var expected = new EpisodeEngine(before).Apply(command);
            Assert.That(expected.accepted, Is.True, expected.reason);
            int writes = 0;
            SetSaveHook(candidate =>
            {
                writes++;
                new EpisodeSaveStore(director.SavePath).Save(candidate);
                throw new IOException("Injected acknowledgement failure after replacement.");
            });

            var result = director.Submit(command);

            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(writes, Is.EqualTo(1));
            AssertStateEquals(expected.state, result.state);
            AssertStateEquals(expected.state, director.Snapshot);
            AssertDiskEquals(expected.state);
            var committedFiles = CaptureFiles();
            var duplicate = director.Submit(command);
            Assert.That(duplicate.accepted, Is.False);
            Assert.That(duplicate.duplicate, Is.True);
            Assert.That(writes, Is.EqualTo(1), "A durable receipt must not redraw, repeat effects, or rotate the backup twice.");
            AssertStateEquals(expected.state, director.Snapshot);
            AssertFilesEqual(committedFiles);
            SetSaveHook(null);
            director.LoadNow();
            AssertStateEquals(expected.state, director.Snapshot);
            Assert.That(GetField<bool>("npcSaveSuspended"), Is.False);
            yield break;
        }

        [UnityTest]
        public IEnumerator DiagnosticNoOpCannotPublishAnUnsavedCandidate()
        {
            var before = director.Snapshot;
            var files = CaptureFiles();
            int writes = 0;
            SetSaveHook(_ => writes++); // Deliberately returns normally without writing anything.

            var result = director.Submit(Talk(before));

            Assert.That(result.accepted, Is.False, "Returning from a diagnostic callback is not proof of durability.");
            Assert.That(writes, Is.EqualTo(1));
            AssertStateEquals(before, director.Snapshot);
            AssertFilesEqual(files);
            Assert.That(director.StatusMessage, Does.Not.Contain("Saved locally"));
            Assert.That(GetField<bool>("npcSaveSuspended"), Is.True);
            yield break;
        }

        [UnityTest]
        public IEnumerator DifferentValidatedPrimary_EntersRecoveryAndCannotOverwriteUntilExplicitLoad()
        {
            var before = director.Snapshot;
            var foreign = FreshOtherSession();
            Dictionary<string, byte[]> retained = null;
            SetSaveHook(_ =>
            {
                new EpisodeSaveStore(director.SavePath).Save(foreign);
                retained = CaptureFiles();
                throw new IOException("Injected uncertain replacement with a different valid state.");
            });

            var result = director.Submit(Talk(before));

            Assert.That(result.accepted, Is.False);
            AssertStateEquals(before, director.Snapshot);
            AssertRecoveryLocked();
            Assert.That(retained, Is.Not.Null);
            AssertFilesEqual(retained);
            Assert.That(director.Submit(Talk(before)).accepted, Is.False);
            director.SaveNow();
            AssertStateEquals(before, director.Snapshot);
            AssertFilesEqual(retained);
            SetSaveHook(null);
            director.LoadNow();
            AssertStateEquals(foreign, director.Snapshot);
            Assert.That(GetField<bool>("blockedRecovery"), Is.False);
            Assert.That(player.InputEnabled, Is.True);
            Assert.That(cameraRig.ControlsEnabled, Is.True);
            AssertFilesEqual(retained, "Explicit read-only load must not rewrite the validated foreign primary.");
            yield break;
        }

        [UnityTest]
        public IEnumerator UnreadablePrimary_EntersRecoveryAndPreservesDamagedBytesUntilExplicitBackupRecovery()
        {
            var before = director.Snapshot;
            byte[] damaged = { (byte)'{', (byte)'!' };
            byte[] originalBackup = File.ReadAllBytes(director.SavePath + ".backup");
            Dictionary<string, byte[]> retained = null;
            SetSaveHook(_ =>
            {
                File.WriteAllBytes(director.SavePath, damaged); // This generated slot already has a validated test-owned backup.
                retained = CaptureFiles();
                throw new IOException("Injected unreadable primary after an uncertain write.");
            });

            Assert.That(director.Submit(Talk(before)).accepted, Is.False);

            AssertStateEquals(before, director.Snapshot);
            AssertRecoveryLocked();
            AssertFilesEqual(retained);
            director.SaveNow();
            Assert.That(director.Submit(Talk(before)).accepted, Is.False);
            AssertFilesEqual(retained);
            CollectionAssert.AreEqual(originalBackup, File.ReadAllBytes(director.SavePath + ".backup"));
            SetSaveHook(null);
            director.RecoverBackup();
            AssertStateEquals(before, director.Snapshot);
            AssertDiskEquals(before);
            Assert.That(GetField<bool>("blockedRecovery"), Is.False);
            Assert.That(player.InputEnabled, Is.True);
            Assert.That(cameraRig.ControlsEnabled, Is.True);
            var retainedPrimary = Directory.GetFiles(isolatedRoot, Path.GetFileName(director.SavePath) + ".before-recovery-*.json").Single();
            CollectionAssert.AreEqual(damaged, File.ReadAllBytes(retainedPrimary));
            CollectionAssert.AreEqual(originalBackup, File.ReadAllBytes(director.SavePath + ".backup"));
            yield break;
        }

        [UnityTest]
        public IEnumerator ReentrantSubmitLoadRecoveryAndSlotChangesCannotRunInsideCommit()
        {
            var before = director.Snapshot;
            string originalPath = director.SavePath;
            long generation = GetField<long>("loadGeneration");
            var originalFiles = CaptureFiles();
            int writes = 0;
            SetSaveHook(candidate =>
            {
                writes++;
                var nested = director.Submit(Talk(before));
                Assert.That(nested.accepted, Is.False);
                Assert.That(nested.reason, Does.Contain("transaction"));
                director.LoadNow();
                director.RecoverBackup();
                director.NewSeason();
                director.ImportFile(Path.Combine(isolatedRoot, "not-an-import.json"));
                director.SaveNow();
                AssertStateEquals(before, director.Snapshot);
                Assert.That(director.SavePath, Is.EqualTo(originalPath));
                Assert.That(GetField<long>("loadGeneration"), Is.EqualTo(generation));
                AssertFilesEqual(originalFiles);
                new EpisodeSaveStore(originalPath).Save(candidate);
            });

            var result = director.Submit(Talk(before));

            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(writes, Is.EqualTo(1));
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1));
            Assert.That(director.SavePath, Is.EqualTo(originalPath));
            Assert.That(GetField<long>("loadGeneration"), Is.EqualTo(generation));
            AssertDiskEquals(director.Snapshot);
            yield break;
        }

        [UnityTest]
        public IEnumerator SaveNowFailedWholeNpcTick_DoesNotFallBackToWritingOldSnapshotOrResumeAutomatically()
        {
            yield return EnableWholeTickWithoutInventingPendingConversations();
            var before = director.Snapshot;
            var originalFiles = CaptureFiles();
            int writes = 0;
            SetSaveHook(candidate =>
            {
                writes++;
                if (writes == 1)
                {
                    Assert.That(candidate.npcSocial.clockTick, Is.EqualTo(before.npcSocial.clockTick + 1));
                    Assert.That(candidate.revision, Is.EqualTo(before.revision + 1));
                    throw new IOException("Injected whole-second pre-write failure.");
                }
                // Deliberately succeed if a buggy SaveNow attempts its fallback write.
                new EpisodeSaveStore(director.SavePath).Save(candidate);
            });

            director.SaveNow();

            Assert.That(writes, Is.EqualTo(1), "A failed whole-tick flush must abort this SaveNow invocation.");
            AssertStateEquals(before, director.Snapshot);
            AssertFilesEqual(originalFiles);
            Assert.That(GetField<bool>("npcSaveSuspended"), Is.True);
            Assert.That(director.StatusMessage, Does.Not.Contain("Saved locally"));
            yield break;
        }

        [UnityTest]
        public IEnumerator SaveNowAmbiguousWholeNpcTick_CannotBypassNewRecoveryLockWithSecondWrite()
        {
            yield return EnableWholeTickWithoutInventingPendingConversations();
            var before = director.Snapshot;
            var foreign = FreshOtherSession();
            Dictionary<string, byte[]> retained = null;
            int writes = 0;
            SetSaveHook(candidate =>
            {
                writes++;
                if (writes == 1)
                {
                    Assert.That(candidate.npcSocial.clockTick, Is.EqualTo(before.npcSocial.clockTick + 1));
                    new EpisodeSaveStore(director.SavePath).Save(foreign);
                    retained = CaptureFiles();
                    throw new IOException("Injected ambiguous whole-tick replacement.");
                }
                new EpisodeSaveStore(director.SavePath).Save(candidate);
            });

            director.SaveNow();

            Assert.That(writes, Is.EqualTo(1), "The recovery flag can change during Flush; SaveNow must check its result before another write.");
            AssertStateEquals(before, director.Snapshot);
            AssertRecoveryLocked();
            AssertFilesEqual(retained);
            Assert.That(director.StatusMessage, Does.Not.Contain("Saved locally"));
            yield break;
        }

        [UnityTest]
        public IEnumerator OldNpcButtonCannotCommitIntoAnotherSessionWithMatchingRevisionAndPhase()
        {
            yield return OpenNearbyMaya();
            var oldState = director.Snapshot;
            // Retain the actual event delegate across a synchronous panel rebuild/load.
            // There is no fabricated player command or altered authoritative engine here.
            var oldClick = ButtonWithCaption("Spend time together").onClick;
            var fresh = FreshOtherSession();
            Assert.That(fresh.revision, Is.EqualTo(oldState.revision));
            Assert.That(fresh.phase, Is.EqualTo(oldState.phase));
            Assert.That(fresh.playerId, Is.EqualTo(oldState.playerId));
            Assert.That(fresh.sessionId, Is.Not.EqualTo(oldState.sessionId));
            new EpisodeSaveStore(director.SavePath).Save(fresh);
            director.LoadNow();
            var loadedFiles = CaptureFiles();
            int writes = 0;
            SetSaveHook(candidate => { writes++; new EpisodeSaveStore(director.SavePath).Save(candidate); });

            oldClick.Invoke();

            Assert.That(writes, Is.Zero, "The captured origin session must be checked before ordinary non-diary callbacks submit.");
            AssertStateEquals(fresh, director.Snapshot);
            AssertFilesEqual(loadedFiles);
            yield break;
        }

        private IEnumerator EnableWholeTickWithoutInventingPendingConversations()
        {
            Assert.That(director.IsPanelOpen, Is.False);
            Assert.That(director.Snapshot.npcSocial.pending, Is.Empty);
            director.OpenSettings(); // Binding is permitted; the visible modal prevents all logical time and scheduling.
            var beforeBinding = director.Snapshot;
            SetField("npcDiagnosticsSuspended", false);
            InvokePrivate("EnsureNpcSocialWorld"); // Binds the real scene actors/coordinator; no synthetic arrival evidence.
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!director.NpcAutonomyReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.NpcAutonomyReady, Is.True, director.NpcAutonomyDiagnostic);
            Assert.That(GetField<HouseMeetingCoordinator>("npcMeetings"), Is.Not.Null);
            AssertStateEquals(beforeBinding, director.Snapshot);
            director.ClosePanels();
            InvokePrivate("SetNpcWorldPaused", false);
            // One elapsed whole second is the only clock seam. SaveNow exercises the
            // actual Flush -> PrepareNpcOperation -> validated durable install chain.
            SetField("npcFrameFraction", 1.25d);
        }

        private IEnumerator OpenNearbyMaya()
        {
            var maya = SceneObjects<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            for (int direction = 0; direction < 8; direction++)
            {
                float angle = direction * Mathf.PI / 4f;
                var point = maya.transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 1.7f;
                if (!NavMesh.SamplePosition(point, out var hit, .5f, player.Agent.areaMask) || !player.Agent.Warp(hit.position)) continue;
                player.Agent.ResetPath(); Physics.SyncTransforms();
                if (!director.TryOpenNpc(maya.Id)) continue;
                yield return null;
                yield break;
            }
            Assert.Fail("The existing scene must offer a real reachable, visible interaction point near Maya.");
        }

        private void AssertRecoveryLocked()
        {
            Assert.That(GetField<bool>("blockedRecovery"), Is.True);
            Assert.That(GetField<bool>("npcSaveSuspended"), Is.True);
            Assert.That(director.IsPanelOpen, Is.True);
            Assert.That(player.InputEnabled, Is.False, "A recovery modal must also lock world controls.");
            Assert.That(cameraRig.ControlsEnabled, Is.False);
            Assert.That(cameraRig.IsConversationFocused, Is.False);
            Assert.That(director.StatusMessage, Does.Contain("SAVE NEEDS ATTENTION"));
        }

        private static EpisodeState FreshOtherSession()
        {
            var state = ContentCatalog.Create(73519);
            state.sessionId = Guid.NewGuid().ToString("N");
            return state;
        }

        private static EpisodeCommand Talk(EpisodeState state) => new EpisodeCommand
        {
            id = Guid.NewGuid().ToString("N"), actorId = state.playerId,
            expectedRevision = state.revision, expectedPhase = state.phase,
            kind = EpisodeCommandKind.Talk, targetId = ContentCatalog.MayaId
        };

        private void AssertDiskEquals(EpisodeState expected)
        {
            Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var disk, out var message), Is.True, message);
            AssertStateEquals(expected, disk);
        }

        private static void AssertStateEquals(EpisodeState expected, EpisodeState actual)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.randomState, Is.EqualTo(expected.randomState), "The main random stream changed.");
            Assert.That(actual.npcSocial.randomState, Is.EqualTo(expected.npcSocial.randomState), "The independent NPC stream changed.");
            // All persisted state DTOs are Unity-serializable public-field graphs.
            // Compare the complete graph in addition to the explicit RNG assertions.
            Assert.That(JsonUtility.ToJson(actual), Is.EqualTo(JsonUtility.ToJson(expected)), "The full committed snapshot changed.");
        }

        private Dictionary<string, byte[]> CaptureFiles() => Directory.GetFiles(isolatedRoot, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path.Substring(isolatedRoot.Length + 1), File.ReadAllBytes, StringComparer.Ordinal);

        private void AssertFilesEqual(Dictionary<string, byte[]> expected, string message = "A rejected/read-only operation changed isolated slot bytes or created files.")
        {
            Assert.That(expected, Is.Not.Null);
            var actual = CaptureFiles();
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), message);
            foreach (var file in expected) CollectionAssert.AreEqual(file.Value, actual[file.Key], message + " " + file.Key);
        }

        private void SetSaveHook(Action<EpisodeState> hook) => SetField("saveCandidateForDiagnostics", hook);
        private static FieldInfo Field(string name)
        {
            var field = typeof(EpisodeDirector).GetField(name, PrivateInstance);
            Assert.That(field, Is.Not.Null, "The reviewed director test seam is missing: " + name);
            return field;
        }
        private void SetField(string name, object value) => Field(name).SetValue(director, value);
        private T GetField<T>(string name) => (T)Field(name).GetValue(director);
        private void InvokePrivate(string name, params object[] arguments)
        {
            var method = typeof(EpisodeDirector).GetMethod(name, PrivateInstance);
            Assert.That(method, Is.Not.Null, "The real runtime helper is missing: " + name);
            method.Invoke(director, arguments);
        }

        private Button ButtonWithCaption(string caption) => director.GetComponentsInChildren<Button>(true)
            .Single(button => button.IsActive() && button.GetComponentsInChildren<Text>(true).Any(text => text.text == caption));
        private static T[] SceneObjects<T>() where T : Component => SceneManager.GetSceneByName(EpisodeScene).GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
