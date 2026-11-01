using System;
using System.IO;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeDirector
    {
        private bool durableCommitInProgress, npcSaveSuspended;
        // Explicit Editor diagnostics only (FreezeForReloadForDiagnostics): this director is about to
        // be replaced by one that loads what a test just wrote to its save path, so it writes nothing.
        private bool persistenceFrozenForDiagnostics;
        private long loadGeneration;
        // Explicit Editor diagnostics only; ordinary players always use the validated durable store.
        internal Action<EpisodeState> saveCandidateForDiagnostics = null;
        // Explicit Editor diagnostics only: run on each house operation after the store's own bounds, so a test can
        // reach the refusal of one (RefuseUnsaveableNpcOperation), which otherwise takes a save over 8 MiB.
        internal Action<EpisodeState> npcOperationCheckForDiagnostics = null;
        // Verification only (PortVerification sets it in a verifying player): how long each part of a house commit
        // takes, written beside the profile's frames at quit. Null in ordinary play and every Editor session, and
        // never serialized: a serialized instance would record in every scene that holds a director.
        [NonSerialized] internal NpcCommitTimings commitTimingForVerification;
        // The house's operations installed since its last save, and the state on disk while any are owed
        // (LIVE-SAVE-PLAN.md): they reach disk every NpcSaveCadence.Seconds of free time and at every way out.
        private readonly NpcSaveCadence npcSaveCadence = new NpcSaveCadence();
        // A new season or import refused once because the slot being left could not be written. Any successful
        // write and every install clear it, so a failure that persists refuses once and never traps the player.
        private bool npcSlotRefused;
        // The line a failed write of the unchanged session - the house's own, or Save's - left in the message. A retry
        // that lands at a way out replaces it (EndSessionSaveFailure); a decision's failure is never this line.
        private string sessionSaveFailure;

        /// <summary>
        /// Explicit Editor diagnostics for a test that has just written a fixture to <see cref="SavePath"/>
        /// and is about to reload the scene: from here on this director writes nothing to disk, and the
        /// house's clock stops (<see cref="SuspendNpcAutonomyForDiagnostics"/>). Both used to commit while
        /// the scene loaded. The opening plays on real time and records each finished beat as a commit,
        /// so the frame after a long fixture search could finish a beat and save the outgoing season over
        /// the fixture; the incoming director then came up with the default season instead.
        /// </summary>
        public void FreezeForReloadForDiagnostics()
        {
            if (!Application.isEditor) throw new InvalidOperationException("Reload diagnostics are Editor-only.");
            // Frozen before the house stops: stopping it writes what it has installed, and nothing may be written now.
            persistenceFrozenForDiagnostics = true;
            SuspendNpcAutonomyForDiagnostics();
        }

        private static bool EquivalentState(EpisodeState first, EpisodeState second) => first != null && second != null
            && JToken.DeepEquals(JObject.FromObject(first, SaveJson.Serializer()), JObject.FromObject(second, SaveJson.Serializer()));

        /// <summary>
        /// Persist a decision before publishing it; the house's own operations are published in memory and
        /// persisted together by <see cref="FlushUnsavedNpcTime"/>. All callers are synchronous on the Unity thread;
        /// slot replacement/load/player/NPC operations share this same reentrancy gate.
        /// This is not a cross-process filesystem compare-and-swap guarantee.
        /// </summary>
        private bool TryPersistCandidate(EpisodeState prepared, out EpisodeState installed, out string failure)
        {
            installed = null; failure = null;
            if (durableCommitInProgress || prepared == null || engine == null || saves == null)
            { failure = "A save transaction is already running or the episode is unavailable."; return false; }
            if (persistenceFrozenForDiagnostics)
            { failure = "This episode is being replaced, so nothing more is saved from it."; return false; }
            var authority = engine;
            var store = saves;
            long generation = loadGeneration;
            var before = authority.Snapshot;
            // What the disk should hold: the session itself, or, while the house owes operations, the state this
            // director last wrote or loaded. A failed write is judged against it, never against memory ahead of it.
            var known = npcSaveCadence.KnownOnDisk(before);
            var candidate = prepared.Clone();
            if (candidate.sessionId != before.sessionId || candidate.revision < before.revision
                || candidate.revision > before.revision + 1)
            { failure = "The prepared decision does not follow the current episode."; return false; }
            durableCommitInProgress = true;
            try
            {
                EpisodeSaveValidation.Validate(candidate);
                if (Application.isEditor && saveCandidateForDiagnostics != null)
                {
                    saveCandidateForDiagnostics(candidate.Clone());
                    if (!store.TryLoad(out var verified, out _) || !EquivalentState(verified, candidate))
                        throw new InvalidDataException("The diagnostic writer did not durably save the exact prepared decision.");
                }
                else store.Save(candidate);
                if (!ReferenceEquals(engine, authority) || !ReferenceEquals(saves, store) || generation != loadGeneration)
                {
                    EnterRecoveryLock();
                    failure = "The active slot changed during a save. Reload a validated slot before continuing.";
                    return false;
                }
                installed = candidate; npcSaveSuspended = false; npcSlotRefused = false; npcSaveCadence.Saved(candidate);
                return true;
            }
            catch (Exception error) when (SaveJson.IsExpected(error))
            {
                // An error reported after replacement must not redraw/replay an operation.
                // Reconcile only an exact validated candidate; never guess from a timestamp.
                bool hasPrimary = File.Exists(store.SavePath);
                bool readable = store.TryLoad(out var disk, out _);
                if (readable && EquivalentState(disk, candidate)
                    && ReferenceEquals(engine, authority) && ReferenceEquals(saves, store) && generation == loadGeneration)
                {
                    installed = candidate; npcSaveSuspended = false; npcSlotRefused = false; npcSaveCadence.Saved(candidate);
                    return true;
                }
                npcSaveSuspended = true;
                // A held primary is unreadable here too, so it locks: every way out of the lock goes back to the
                // last save and drops the house's owed seconds (LIVE-SAVE-PLAN.md 3.1, within NpcSaveCadence.Seconds).
                if (hasPrimary && (!readable || !EquivalentState(disk, known)))
                {
                    EnterRecoveryLock();
                    failure = "SAVE NEEDS ATTENTION: the primary is unreadable or differs from both the current session and prepared decision. Existing files were retained; reload or recover before continuing.";
                }
                // The session itself refused for what it holds, not where it goes: the house's own write, with seconds
                // owed, failed on content. No retry can save that state, so the way on is the last save.
                else if (candidate.revision == before.revision && !npcSaveCadence.Clean && !SaveJson.IsFilesystemError(error))
                    failure = "SAVE NEEDS ATTENTION: " + SaveJson.Explain(error) + " The house's latest seconds could not be saved."
                        + " Reload to go back to your last save; the houseguests will live those seconds again.";
                else failure = "SAVE NEEDS ATTENTION: " + SaveJson.Explain(error) + " The decision was not committed."
                    + " Your current session is unchanged; retry Save or reload.";
                return false;
            }
            finally { durableCommitInProgress = false; }
        }

        /// <summary><see cref="TryPersistCandidate"/>, with a row in the verification's timing log when it is on.</summary>
        private bool TryPersistTimed(EpisodeState prepared, string cause, out EpisodeState installed, out string failure)
        {
            var timing = commitTimingForVerification;
            long lap = timing != null ? NpcCommitTimings.Now() : 0;
            bool saved = TryPersistCandidate(prepared, out installed, out failure);
            timing?.Write(Time.frameCount, Time.realtimeSinceStartupAsDouble, cause, prepared != null ? prepared.revision : -1,
                NpcCommitTimings.Lap(ref lap), saved);
            return saved;
        }

        /// <summary>
        /// Writes the operations the house has installed since its last save - its clock seconds and the
        /// conversations its people started and cancelled - as one save of the unchanged session, the write Save
        /// makes without its status line. True unless it tried now and failed, which shows the failure and stops
        /// the house (only logged when <paramref name="quiet"/>, at quit).
        ///
        /// <para>Two kinds of caller. An automatic write - the period, the stop backstop, a hold - skips a house a
        /// failed save stopped: nothing on the house's own clock retries in a loop. A way out the player takes
        /// (<paramref name="leaving"/>: a panel, focus, pause, quit, a slot change) retries it once, as Save does,
        /// so what the menus show and what an orderly quit leaves reach disk whenever a write can; a retry that
        /// lands clears the failure's notice and its line (<see cref="EndSessionSaveFailure"/>). It draws nothing:
        /// a panel or a slot change draws after it, and switching away draws for itself
        /// (<see cref="FlushForSwitchingAway"/>).</para>
        /// </summary>
        private bool FlushUnsavedNpcTime(string cause, bool leaving = false, bool quiet = false)
        {
            // Nothing owed, or nothing this director may write now: not a failure.
            if (npcSaveCadence.Clean || blockedRecovery || durableCommitInProgress || persistenceFrozenForDiagnostics
                || engine == null || saves == null) return true;
            if (npcSaveSuspended && !leaving) return true;
            bool retry = npcSaveSuspended;
            if (TryPersistTimed(engine.Snapshot, cause, out _, out var failure))
            {
                if (retry) EndSessionSaveFailure();
                return true;
            }
            if (quiet) { Debug.LogWarning("The house's last seconds could not be saved: " + failure); return false; }
            message = sessionSaveFailure = failure; BeginPersistenceNotice(failure); SuspendNpcWorldWithoutSaving(); Render();
            return false;
        }

        private bool FlushForLeaving(string cause, bool quiet = false) => FlushUnsavedNpcTime(cause, true, quiet);

        /// <summary>
        /// A write of the unchanged session landed - Save, or a retry at a way out. Any notice goes, and so does the
        /// line a failed write of it left in the message, which Settings shows under YOUR SAVE: "Saved locally."
        /// replaces it. Any other line stays with "Saved locally." after it: a decision's failure above all, since
        /// that decision is still not committed, though it can read word for word as the house's.
        /// </summary>
        private void EndSessionSaveFailure()
        {
            bool failuresLine = sessionSaveFailure != null && message == sessionSaveFailure;
            message = failuresLine ? "Saved locally." : message + "  ·  Saved locally.";
            sessionSaveFailure = null;
            MarkSaveFeedback();
        }

        /// <summary>
        /// The check a house operation passes before it is installed: the store's own bounds, which every house
        /// write ran before the house saved in batches, so nothing they refuse is installed. Not the store's whole
        /// check: its read-back of the file it writes runs only at the write (LIVE-SAVE-PLAN.md 2.1), and running it
        /// here is the cost the batching removes. Null when the operation passes.
        /// </summary>
        private Exception RefusalOfNpcOperation(EpisodeState candidate)
        {
            try
            {
                EpisodeSaveValidation.Validate(candidate);
                if (Application.isEditor && npcOperationCheckForDiagnostics != null) npcOperationCheckForDiagnostics(candidate.Clone());
                return null;
            }
            catch (Exception error) when (SaveJson.IsExpected(error)) { return error; }
        }

        /// <summary>
        /// A house operation the store's bounds refuse is not installed. What is installed passed the same check,
        /// so it reaches disk before the house stops on the one refused, with the notice the refused write would
        /// have shown; if that write fails, its own notice stands instead. A refusal never leaves the house standing
        /// still on seconds only memory holds.
        /// </summary>
        private void RefuseUnsaveableNpcOperation(Exception error)
        {
            string failure = "SAVE NEEDS ATTENTION: " + SaveJson.Explain(error) + " The decision was not committed."
                + " Your current session is unchanged; retry Save or reload.";
            if (!npcSaveCadence.Clean && !blockedRecovery && !persistenceFrozenForDiagnostics
                && !TryPersistTimed(engine.Snapshot, "refuse", out _, out var unsaved)) failure = sessionSaveFailure = unsaved;
            npcSaveSuspended = true;
            message = failure; BeginPersistenceNotice(failure); SuspendNpcWorldWithoutSaving(); Render();
        }

        // Switching away is the last moment before a Task Manager kill or sleep: the player is built to advance
        // nothing while unfocused. Not in an Editor run under a save root, which is every test and Editor
        // verification: there focus moves with the Game view, the Inspector and other programs, at moments no
        // test controls. The owner's own Editor play has no save root and keeps both.
        private void OnApplicationFocus(bool focused)
        {
            if (Application.isEditor && SaveRootOverride != null) return;
            if (!focused && IsReady) FlushForSwitchingAway("focus");
        }

        private void OnApplicationPause(bool paused)
        {
            if (Application.isEditor && SaveRootOverride != null) return;
            if (paused && IsReady) FlushForSwitchingAway("pause");
        }

        /// <summary>
        /// The write as the player switches away. Nothing draws after it, as a panel or a slot change does, and the
        /// house moves again under whatever the HUD last said, so a retry that lands draws here: otherwise the
        /// failure's line stands over a house that has saved. A failed write has drawn its own notice.
        /// </summary>
        private void FlushForSwitchingAway(string cause)
        {
            bool stopped = npcSaveSuspended;
            if (FlushForLeaving(cause) && stopped && !npcSaveSuspended) Render();
        }

        /// <summary>
        /// An orderly quit - the menu's Quit, the window's close, Alt+F4, or the Editor leaving play - writes the
        /// house's installed operations while the scene is intact, retrying a house a failed save stopped. A failure
        /// only logs: blocking the quit would trap the player in a failing game. A kill or a power cut runs none of
        /// this; that is the most the owner's call lets the house lose (NpcSaveCadence.Seconds).
        /// </summary>
        private void OnApplicationQuit()
        {
            if (IsReady) FlushForLeaving("quit", true);
            WriteCommitTimings();
        }

        /// <summary>The verification's commit timings, beside its report in the save root; one warning if that fails.</summary>
        private void WriteCommitTimings()
        {
            var timing = commitTimingForVerification;
            if (timing == null || string.IsNullOrEmpty(saveRoot)) return;
            try { File.WriteAllText(Path.Combine(saveRoot, NpcCommitTimings.FileName), timing.ToCsv()); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { Debug.LogWarning("The house's commit timings could not be written: " + error.Message); }
        }

        private void EnterRecoveryLock()
        {
            blockedRecovery = true; npcSaveSuspended = true;
            ClosePanelsInternal(false);
            player?.SetInputEnabled(false);
            if (cameraRig != null) cameraRig.ControlsEnabled = false;
            SuspendNpcWorldWithoutSaving();
        }
    }
}
