using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// Dialogue rules 4's veto question on the conversation's game page (DIALOGUE-PLAN §4.1, slice D4): while the player is
    /// nominated and the veto competition is still ahead, a veto player can be asked whether they would use the veto on the
    /// player if they win it. One action; the conversation that opens plays as every dialogue conversation does. Absent below
    /// rules 4 and wherever the engine's own entry plan refuses it.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The row's caption: the veto question's opening, word for word (<see cref="DialogueVetoQuestion.OpenLabel"/>).</summary>
        public const string VetoQuestionCaption = DialogueVetoQuestion.OpenLabel;

        /// <summary>The row's tag: what asking costs, and that the answer is theirs alone.</summary>
        public const string VetoQuestionTag = "1 action · their word only";

        private void VetoQuestionRow(EpisodeState state, ContestantState npc)
        {
            if (state == null || npc == null || !DialogueRules.CanAskVetoQuestion(state, npc.id)) return;
            string id = npc.id;
            hud.Tag(hud.Action(VetoQuestionCaption, () =>
            {
                if (DialogueRules.TryBuildEntryCommand(state, id, out var plan) && plan.text == DialogueVetoQuestion.TemplateId)
                    Commit(state, plan.kind, plan.targetId, plan.secondTargetId, text: plan.text);
            }), VetoQuestionTag);
        }
    }
}
