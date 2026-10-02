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
            SuspendNpcAutonomyForDiagnostics();
            persistenceFrozenForDiagnostics = true;
        }

        private static bool EquivalentState(EpisodeState first, EpisodeState second) => first != null && second != null
            && JToken.DeepEquals(JObject.FromObject(first, SaveJson.Serializer()), JObject.FromObject(second, SaveJson.Serializer()));

        /// <summary>
        /// Persist before publishing. All callers are synchronous on the Unity thread;
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
                installed = candidate; npcSaveSuspended = false;
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
                    installed = candidate; npcSaveSuspended = false;
                    return true;
                }
                npcSaveSuspended = true;
                if (hasPrimary && (!readable || !EquivalentState(disk, before)))
                {
                    EnterRecoveryLock();
                    failure = "SAVE NEEDS ATTENTION: the primary is unreadable or differs from both the current session and prepared decision. Existing files were retained; reload or recover before continuing.";
                }
                else failure = "SAVE NEEDS ATTENTION: " + SaveJson.Explain(error) + " The decision was not committed."
                    + " Your current session is unchanged; retry Save or reload.";
                return false;
            }
            finally { durableCommitInProgress = false; }
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
