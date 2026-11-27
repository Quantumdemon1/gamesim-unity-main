using System;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// Dialogue rules 4's nominee grant on the plea panel (DIALOGUE-PLAN §4, slice D5a): while a nominated player's
    /// conversation would be on the house, the person's menu says so in one plain line (<see cref="DialogueRules.GrantLine"/>),
    /// the conversation's entry says it costs no action, and the desperate plea that opens it can be pressed with no action
    /// left; every other plea stays paid, and is locked when no action is left. Absent below rules 4 and wherever the engine
    /// would not pay for the conversation (EpisodeDirector.Strategy.cs's LobbyPanel reads <see cref="DialogueRules.OnTheHouseWith"/>).
    ///
    /// <para>Dialogue rules 5's HoH consultation on the person's menu (slice D5b-1): while the player is Head of Household and
    /// has not named anybody, any houseguest can be called in for their case (<see cref="HoHConsultCaption"/>), on the house
    /// while the HoH grant pays and otherwise for one action. Under the role moments a pitcher on the board can be called in
    /// from their card too, under a caption of its own (<see cref="HoHConsultPitchCaption"/>, EpisodeDirector.Strategy.cs).
    /// Absent below rules 5 and wherever the engine's own entry plan refuses it.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The entry's paragraph while the grant pays: the plain line, then what the conversation costs.</summary>
        public const string OnTheHouseEntryLine = DialogueRules.GrantLine
            + " A desperate plea starts it for no action, and its replies and final plea cost none either.";

        /// <summary>The desperate plea's tag while the grant pays. As short as <see cref="NoConversationTag"/>, so it keeps to one line.</summary>
        public const string OnTheHouseTag = "on the house";

        /// <summary>A paid plea's tag when no action is left and only the conversation on the house is open.</summary>
        public const string NoActionTag = "no actions left";

        /// <summary>The person's menu row (D5b-1): the HoH consultation's opening, word for word (<see cref="DialogueHoHConsult.OpenLabel"/>).</summary>
        public const string HoHConsultCaption = DialogueHoHConsult.OpenLabel;

        /// <summary>The pitch board's way into the same consultation (D5b-1, DIALOGUE-PLAN call 9): a caption of its own on each entry.</summary>
        public const string HoHConsultPitchCaption = "Talk it over first";

        /// <summary>The row's tag when the consultation costs the window's action; <see cref="OnTheHouseTag"/> while the HoH grant pays.</summary>
        public const string HoHConsultTag = "1 action";

        /// <summary>
        /// The HoH consultation's row on the conversation's game page (D5b-1): the plain line while it is on the house, and the
        /// row itself, tagged with what it costs. Absent below rules 5 and wherever the engine's own entry plan refuses it.
        /// </summary>
        private void HoHConsultRow(EpisodeState state, ContestantState npc)
        {
            if (state == null || npc == null || !DialogueRules.CanCallIn(state, npc.id)) return;
            string id = npc.id;
            bool free = DialogueRules.CallInOnTheHouse(state, id);
            if (free) hud.Paragraph(DialogueRules.GrantLine);
            hud.Tag(hud.Action(HoHConsultCaption, () => CallIn(state, id)), free ? OnTheHouseTag : HoHConsultTag);
        }

        /// <summary>Calls the houseguest in: the engine's own entry plan, committed as built. Nothing when it no longer builds.</summary>
        private void CallIn(EpisodeState state, string npcId)
        {
            if (DialogueRules.TryBuildEntryCommand(state, npcId, out var plan) && plan.text == DialogueHoHConsult.TemplateId)
                Commit(state, plan.kind, plan.targetId, plan.secondTargetId, text: plan.text);
        }

        /// <summary>The key the pitch board's consultation answer is registered under: never a reply card's own key.</summary>
        public const string HoHConsultPitchKey = "hoh-consult";

        /// <summary>
        /// The pitch board's consultation answer (D5b-1): guarded as the board's own answers are (<see cref="ReplyChoice"/>), it
        /// calls the pitcher in by the engine's own entry plan and puts the conversation on the nomination screen at once, as a
        /// story opened from the tracker is. The pitch itself stays on the board, unanswered.
        /// </summary>
        private Action CallInFromTheBoard(EpisodeState state, string cardId, string npcId, object view)
        {
            long generation = loadGeneration;
            return () =>
            {
                if (this == null || !isActiveAndEnabled || !IsPanelOpen || challengeActive
                    || !ReferenceEquals(replyCardView, view) || generation != loadGeneration
                    || !IsCurrentDiaryRevision(state) || !HoHPitches.Answerable(projected, cardId)
                    || !DialogueRules.TryBuildEntryCommand(state, npcId, out var plan) || plan.text != DialogueHoHConsult.TemplateId) return;
                // Consume before the durable command, as every board answer does.
                replyCardView = null;
                var result = Submit(new EpisodeCommand { id = Guid.NewGuid().ToString("N"), actorId = state.playerId,
                    expectedPhase = state.phase, expectedRevision = state.revision,
                    kind = plan.kind, targetId = plan.targetId, secondTargetId = plan.secondTargetId, text = plan.text });
                var open = result != null && result.accepted ? DialogueRules.Active(result.state) : null;
                if (open == null || open.occasionKind != DialogueHoHConsult.Kind) return;
                nominationOpenedBeat = DialogueRules.Cycle(result.state, open)?.eventId;
                nominationBackdoorView = false;
                ClearStoryStep();
                Render();
            };
        }

        /// <summary>
        /// The beat the nomination screen keeps open once the one it opened is answered (D5b-1): the same HoH consultation's next
        /// node, so a consultation called in from the board plays through on the screen; null for anything else, as before.
        /// </summary>
        private static string FollowTheConsultation(EpisodeState state, string answered)
        {
            var open = DialogueRules.HoHConsultOn(state) ? DialogueRules.Active(state) : null;
            if (open == null || open.occasionKind != DialogueHoHConsult.Kind || answered == null) return null;
            var cycle = DialogueRules.Cycle(state, open);
            return cycle != null && state.houseEvents.Any(e => e != null && e.id == answered && e.cycleId == open.id) ? cycle.eventId : null;
        }
    }
}
