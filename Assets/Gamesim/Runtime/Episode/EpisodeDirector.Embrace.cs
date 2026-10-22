using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// Made up (the art brief's R5): when the player brings two feuding houseguests round - a
    /// kitchen row calmed, a silent treatment ended with a truce, the arcs the story-reaction
    /// table acts at their "peace" ending - the two of them embrace once the scene's card closes.
    ///
    /// <para>The knowledge gate: the trigger is an ending the player made happen and saw, and who
    /// embraces is the two parts that beat's text names. Nothing reads a relationship or a feeling.
    /// Presentation only: it commits nothing and saves nothing, and a reload lets it go.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>How far apart the scene walks the two before they embrace, metres.</summary>
        public const float EmbraceApart = .9f;
        /// <summary>How long the two may take to come together before they embrace where they are, or not at all.</summary>
        public const float EmbraceGatherSeconds = 4f;

        private readonly HashSet<string> peaceSeen = new HashSet<string>();
        private long peaceGeneration = -1;
        private int peaceRevision = -1;
        private string peaceFirst, peaceSecond, peaceScene;
        private float peaceSince;
        private bool peaceGathering;
        private EmbracePair embrace;

        /// <summary>The embrace under way, or the last one while it lets go; for the tests.</summary>
        public EmbracePair CurrentEmbrace => embrace;

        /// <summary>The two a peace is waiting to embrace, or null; for the tests.</summary>
        public string PendingEmbraceFirst => peaceFirst;

        private void TickEmbrace()
        {
            var state = projected;
            if (state == null) return;
            if (peaceGeneration != loadGeneration)
            {
                // A season replaced, a fixture installed, the world handed over: whatever was made up
                // before is history, and an embrace under way lets go.
                peaceGeneration = loadGeneration;
                peaceSeen.Clear();
                foreach (var line in state.storylines ?? new List<StorylineState>())
                    if (line.endingId == StoryReactions.Peace) peaceSeen.Add(line.id);
                peaceRevision = state.revision;
                ClearPeace();
                if (embrace != null) embrace.Release("the house changed hands");
            }
            if (state.revision != peaceRevision)
            {
                peaceRevision = state.revision;
                NoteNewPeace(state);
            }
            if (peaceFirst != null) TickPeace(state);
            if (embrace != null)
            {
                string reason = EmbraceInterrupted(state);
                if (reason != null && embrace.Active) embrace.Release(reason);
                if (embrace == null || embrace.Released)
                {
                    if (npcMeetings != null && npcMeetings.SceneHeldForEmbrace)
                    {
                        npcMeetings.HoldSceneForEmbrace(false);
                        npcMeetings.EndSceneStage();
                    }
                    if (embrace == null || embrace.Stage == EmbracePair.Phase.Done) embrace = null;
                }
            }
        }

        /// <summary>A peace this commit made, on an arc the table acts at its ending: the two it names wait for the card to close.</summary>
        private void NoteNewPeace(EpisodeState state)
        {
            foreach (var line in state.storylines ?? new List<StorylineState>())
            {
                if (line.endingId != StoryReactions.Peace || !peaceSeen.Add(line.id)) continue;
                var beat = state.houseEvents.Where(item => item.IsStory && item.resolved && item.cycleId == line.id).LastOrDefault();
                if (beat == null || !StoryReactions.ActsEnding(beat.contentId, StoryReactions.Peace)) continue;
                string first = line.cast.FirstOrDefault(part => part.role == "HOTHEAD")?.contestantId;
                string second = line.cast.FirstOrDefault(part => part.role == "TARGET")?.contestantId;
                if (StoryReactions.For(StoryReactions.Moment.OnEnding, beat.contentId, "HOTHEAD", null, StoryReactions.Peace) != CharacterPresentation.Reaction.Embrace
                    || string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second) || first == second
                    || first == state.playerId || second == state.playerId
                    || state.Find(first)?.status != ContestantStatus.Active || state.Find(second)?.status != ContestantStatus.Active) continue;
                peaceFirst = first; peaceSecond = second; peaceScene = beat.id;
                peaceSince = Time.unscaledTime; peaceGathering = false;
                // The scene that staged them holds them through the embrace (TickSceneStage leaves a held scene up).
                if (npcMeetings != null && npcMeetings.SceneStageKey == beat.id) npcMeetings.HoldSceneForEmbrace(true);
            }
        }

        /// <summary>Once the scene's card has closed: the two come together, and then embrace.</summary>
        private void TickPeace(EpisodeState state)
        {
            if (IsSceneCardOpen || IsPanelOpen) { peaceSince = Time.unscaledTime; return; }
            if (EmbraceInterrupted(state) != null) { EndPeace(); return; }
            var first = BodyFor(peaceFirst);
            var second = BodyFor(peaceSecond);
            if (first == null || second == null) { EndPeace(); return; }
            var a = first.GetComponent<CharacterPresentation>();
            var b = second.GetComponent<CharacterPresentation>();
            if (a == null || b == null) { EndPeace(); return; }
            // The primitive rig plays nothing: it is only told the beat it would have played.
            if (a.IsPrimitiveBody || b.IsPrimitiveBody)
            {
                a.React(CharacterPresentation.Reaction.Embrace);
                b.React(CharacterPresentation.Reaction.Embrace);
                EndPeace();
                return;
            }
            bool held = npcMeetings != null && npcMeetings.SceneHeldForEmbrace && npcMeetings.SceneStageKey == peaceScene;
            if (held && !peaceGathering)
            {
                if (npcMeetings.IsPaused)
                {
                    // The frame the card closes the house is still paused - its world ticks after this -
                    // and a paused body takes no route: the gather waits for the house, but no longer
                    // than a gather may take. A house that stays paused (its world stopped, a save
                    // suspending it) walks nobody: the two embrace where they stand, or not at all.
                    if (Time.unscaledTime - peaceSince <= EmbraceGatherSeconds) return;
                }
                else
                {
                    var centre = (first.position + second.position) * .5f;
                    peaceGathering = npcMeetings.TryBringTogether(peaceScene, peaceFirst, peaceSecond, centre, EmbraceApart);
                    peaceSince = Time.unscaledTime;
                }
            }
            bool arrived = !peaceGathering || npcMeetings.SceneActorsArrived(peaceFirst, peaceSecond);
            bool late = Time.unscaledTime - peaceSince > EmbraceGatherSeconds;
            if (!arrived && !late) return;
            if (!a.IsStill || !b.IsStill)
            {
                if (!late) return;
            }
            embrace = EmbracePair.TryBegin(a, b, state.playerId);
            if (embrace == null && npcMeetings != null && npcMeetings.SceneHeldForEmbrace)
            {
                npcMeetings.HoldSceneForEmbrace(false);
                npcMeetings.EndSceneStage();
            }
            ClearPeace();
        }

        /// <summary>Why an embrace (or one about to begin) must not go on, or null.</summary>
        private string EmbraceInterrupted(EpisodeState state)
        {
            if (reducedMotion) return "motion is reduced";
            if (IsCeremonyStaged || (npcMeetings != null && (npcMeetings.HasCeremonyStage || npcMeetings.HasOpeningStage || npcMeetings.HasCompetitionStage)))
                return "the house is staged for something else";
            if (OpeningOwnsHouse) return "the opening has the house";
            if (!string.IsNullOrEmpty(departingId) || !string.IsNullOrEmpty(WalkingOutId)) return "somebody is leaving";
            if (state.phase != EpisodePhase.Social && state.phase != EpisodePhase.Campaign) return "free time is over";
            string first = embrace != null ? embrace.First?.CharacterId : peaceFirst;
            string second = embrace != null ? embrace.Second?.CharacterId : peaceSecond;
            foreach (var id in new[] { first, second })
                if (!string.IsNullOrEmpty(id) && state.Find(id)?.status != ContestantStatus.Active) return id + " has left the house";
            return null;
        }

        private void EndPeace()
        {
            if (npcMeetings != null && npcMeetings.SceneHeldForEmbrace && embrace == null)
            {
                npcMeetings.HoldSceneForEmbrace(false);
                npcMeetings.EndSceneStage();
            }
            ClearPeace();
        }

        private void ClearPeace() { peaceFirst = peaceSecond = peaceScene = null; peaceGathering = false; }
    }
}
