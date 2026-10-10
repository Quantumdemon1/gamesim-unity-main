using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The story reactions' clock (the art brief's R1): beats and turns that come a moment after the
    /// story moment that asked for them - a furious body given its heading back, a winner's second
    /// beat. A small queue on the unscaled clock, presentation only: never saved, and dropped
    /// whole when the house's world changes hands (a load, a fixture, the NPC world disposed), so a
    /// beat meant for one season never plays in the next.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        private struct PendingStoryBeat
        {
            public float at;
            public long generation;
            public Action act;
        }

        private readonly List<PendingStoryBeat> storyBeats = new List<PendingStoryBeat>();

        /// <summary>How many story beats are waiting on the clock; for the tests.</summary>
        public int QueuedStoryReactions => storyBeats.Count;

        /// <summary>Runs <paramref name="act"/> once, <paramref name="seconds"/> from now on the unscaled clock.</summary>
        private void AfterStoryDelay(float seconds, Action act)
        {
            if (act == null) return;
            storyBeats.Add(new PendingStoryBeat { at = Time.unscaledTime + Mathf.Max(0f, seconds), generation = loadGeneration, act = act });
        }

        // ------------------------------------------------------------------ the endgame's beats (R4)

        /// <summary>
        /// How long the winner's shock plays before the win: React_shocked's 2.6 s to its exit at
        /// 0.95 (decision D6).
        /// </summary>
        public const float WinnerShockSeconds = 2.6f * .95f;

        /// <summary>The winner waiting for the jury card to show its result, or null.</summary>
        private string winnerAtResult;
        private bool juryResultWasShown;
        private long winnerGeneration;

        /// <summary>
        /// The season's winner (MOCKUP-PASS-PLAN M36, decision D6). On the night the jury card
        /// reads the votes one by one, and a winner who celebrated as it started gave the count
        /// away: so the winner waits for the card's result, plays the shock of it, and then the
        /// win. Where the card is the generic one instead, or the body has no shock to play, the
        /// win is at once, as it always was. The replay on the finale's panel never reacts.
        /// </summary>
        private void ReactToWinner(EpisodeState state)
        {
            var visual = CeremonyVisual(state.winnerId);
            bool onTheNight = juryReveal != null && juryReveal.IsPlaying;
            if (onTheNight && visual != null && (visual.Supports(CharacterPresentation.Reaction.Shocked) || visual.IsPrimitiveBody))
            {
                winnerAtResult = state.winnerId;
                juryResultWasShown = juryReveal.ShowingResult;
                winnerGeneration = loadGeneration;
                return;
            }
            React(state.winnerId, CharacterPresentation.Reaction.Won);
            TurnHeads(state, state.winnerId, null);
        }

        /// <summary>Watched each frame while a winner waits: the rising edge of the card's result plays the shock, then the win.</summary>
        private void TickWinnerAtResult()
        {
            if (winnerAtResult == null) return;
            // A season replaced while the card waited takes its winner with it.
            if (winnerGeneration != loadGeneration) { winnerAtResult = null; return; }
            string winner = winnerAtResult;
            bool shown = juryReveal != null && juryReveal.ShowingResult;
            bool ended = juryReveal == null || !juryReveal.IsPlaying;
            if (shown && !juryResultWasShown)
            {
                winnerAtResult = null;
                if (projected != null) TurnHeads(projected, winner, null);
                // The player's body keeps its own beat, the win - at the result now, not before it.
                if (projected != null && winner == projected.playerId) { React(winner, CharacterPresentation.Reaction.Won); return; }
                React(winner, CharacterPresentation.Reaction.Shocked);
                AfterStoryDelay(WinnerShockSeconds, () => React(winner, CharacterPresentation.Reaction.Won));
            }
            else if (ended && !shown)
            {
                // The card went without a result on screen (skipped, or cut by a load): the win, now.
                winnerAtResult = null;
                React(winner, CharacterPresentation.Reaction.Won);
            }
            juryResultWasShown = shown;
        }

        /// <summary>
        /// The final eviction: the juror leaving for the jury plays their tears (the eviction's own
        /// beat where the body has none), and the finalist the last Head of Household chose - the
        /// one in the house who is not that Head of Household - plays the shock of being taken.
        /// </summary>
        private void ReactToFinalEviction(EpisodeState state, System.Collections.Generic.HashSet<string> wasActive)
        {
            string juror = EvictedThisCommit(state, wasActive);
            var visual = CeremonyVisual(juror);
            // The player's body keeps the eviction's own beat; another's weeps where it can.
            if (visual != null)
                React(juror, juror != state.playerId && (visual.Supports(CharacterPresentation.Reaction.Tearful) || visual.IsPrimitiveBody)
                    ? CharacterPresentation.Reaction.Tearful : CharacterPresentation.Reaction.Evicted);
            foreach (var actor in state.contestants)
            {
                // The player's body keeps only the ceremony beats it always had: this one is new.
                if (actor.status != Gamesim.Simulation.ContestantStatus.Active || actor.id == state.hohId || actor.isPlayer) continue;
                var finalist = CeremonyVisual(actor.id);
                if (finalist != null && (finalist.Supports(CharacterPresentation.Reaction.Shocked) || finalist.IsPrimitiveBody))
                    React(actor.id, CharacterPresentation.Reaction.Shocked);
            }
            TurnHeads(state, juror, null);
        }

        /// <summary>The body a ceremony beat is played on, the player's included, as React(id) finds it.</summary>
        private CharacterPresentation CeremonyVisual(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (housemates != null)
                foreach (var npc in housemates)
                    if (npc != null && npc.Id == id) return npc.GetComponent<CharacterPresentation>();
            if (player != null && projected != null && id == projected.playerId) return player.GetComponent<CharacterPresentation>();
            return null;
        }

        // ------------------------------------------------------------------ a scene's arrivals (R6, D5)

        /// <summary>The beat and the people in it who have already acted their arrival.</summary>
        private string arrivalsBeat;
        private readonly HashSet<string> arrivalsActed = new HashSet<string>();

        /// <summary>
        /// A staged scene's people act out their arrival where the table asks (decision D5): the
        /// veto meeting's nominee storms in as they reach the living room. The walk out is the
        /// scene's end (not built yet: the storm's own lease is listed as remaining).
        /// </summary>
        private void TickSceneArrivals()
        {
            if (npcMeetings == null || !npcMeetings.HasSceneStage || projected == null)
            {
                arrivalsBeat = null; arrivalsActed.Clear();
                return;
            }
            string key = npcMeetings.SceneStageKey;
            if (key != arrivalsBeat) { arrivalsBeat = key; arrivalsActed.Clear(); }
            var beat = projected.houseEvents.FirstOrDefault(item => item.id == key);
            if (beat == null || !beat.IsStory) return;
            foreach (var part in beat.cast)
            {
                if (string.IsNullOrEmpty(part.contestantId) || arrivalsActed.Contains(part.contestantId)
                    || !npcMeetings.SceneActorArrived(part.contestantId)) continue;
                var wanted = StoryReactions.For(StoryReactions.Moment.OnSceneArrival, beat.contentId, part.role);
                arrivalsActed.Add(part.contestantId);
                if (wanted != null) ReactStory(part.contestantId, wanted.Value, null);
            }
        }

        private void TickStoryReactions()
        {
            TickWinnerAtResult();
            TickSceneArrivals();
            TickEmbrace();
            if (storyBeats.Count == 0) return;
            float now = Time.unscaledTime;
            for (int i = 0; i < storyBeats.Count;)
            {
                var beat = storyBeats[i];
                if (beat.generation != loadGeneration) { storyBeats.RemoveAt(i); continue; }
                if (now < beat.at) { i++; continue; }
                storyBeats.RemoveAt(i);
                beat.act();
            }
        }
    }
}
