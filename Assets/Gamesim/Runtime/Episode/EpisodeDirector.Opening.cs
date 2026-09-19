using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Gamesim.Episode
{
    /// <summary>The opening beats: the tutorial, the walk-in and the room stops before week one.</summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>
        /// Starts the first-run tour, once, for a player who has never seen it.
        ///
        /// <para>Never in batchmode. The tour is the only overlay that waits for a click, so it is
        /// the only one that could hold up an automated season — and a headless run is by definition
        /// not a first-time player. Tests drive <c>HouseTutorial.Show</c> directly instead.</para>
        /// </summary>
        public void OfferTutorial()
        {
            if (tutorial == null || Application.isBatchMode || HouseTutorial.Seen) return;
            tutorial.Show(FindChrome);
        }

        /// <summary>
        /// Plays whichever opening beats this season has not seen.
        ///
        /// <para>The tour used to be started directly from both of these call sites. It is now the
        /// fourth of five beats, so the sequence owns it and both sites hand over here — which is
        /// also what makes the intro land before the tour rather than after it.</para>
        ///
        /// <para>Never in batchmode, for the reason the tour was never offered there: a sequence that
        /// waits is the only thing that can hold up an automated season, and a headless run is by
        /// definition not seeing any of this. Tests drive <see cref="OpeningSequence.Play"/> directly
        /// instead.</para>
        /// </summary>
        public void PlayOpening()
        {
            if (opening == null || Application.isBatchMode) { OfferTutorial(); return; }
            if (opening.IsPlaying) return;

            var state = projected;
            opening.Play(state.openingBeatsSeen, new OpeningSequence.Settings
            {
                MarkBeat = MarkOpeningBeat,
                Rig = cameraRig,
                RoomStops = RoomStops(),
                RunTutorial = done =>
                {
                    OfferTutorial();
                    if (tutorial == null || !tutorial.IsShowing) { done(); return; }
                    StartCoroutine(WaitForTutorial(done));
                },
                Finished = Render,
                ReducedMotion = reducedMotion,
                Cast = state.Active.ToList(),
                ArrivalLine = state.events
                    .Where(entry => entry.kind == "arrival")
                    .Select(entry => entry.text)
                    .LastOrDefault(),
            });
        }

        private System.Collections.IEnumerator WaitForTutorial(Action done)
        {
            while (tutorial != null && tutorial.IsShowing) yield return null;
            done();
        }

        /// <summary>
        /// Records a finished beat, through the engine like any other decision.
        ///
        /// <para>It is a command rather than a field the presentation writes because the record has
        /// to survive a reload, and the only thing here that survives a reload is the season. The
        /// meet and greet also has a rules consequence — the player stops being a stranger — and a
        /// consequence belongs to the engine wherever it is triggered from.</para>
        /// </summary>
        private void MarkOpeningBeat(string beat)
        {
            if (string.IsNullOrEmpty(beat) || projected.openingBeatsSeen.Contains(beat)) return;
            Commit(projected, EpisodeCommandKind.MarkOpeningBeat, target: beat);
        }

        /// <summary>
        /// Where the walk-in stops: every room marker in the scene, in the order the house lists
        /// them, which is the order the memory wall and the map already use.
        /// </summary>
        private List<KeyValuePair<string, Vector3>> RoomStops() =>
            gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseRoomMarker>(true))
                .Where(marker => !string.IsNullOrEmpty(marker.RoomName))
                .OrderBy(marker => marker.RoomName, StringComparer.Ordinal)
                .Select(marker => new KeyValuePair<string, Vector3>(marker.RoomName, marker.transform.position))
                .ToList();
    }
}
