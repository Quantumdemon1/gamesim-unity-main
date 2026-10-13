#if GAMESIM_ALLSTAR_CANDIDATE
using System;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeDirector
    {
        public const string AlumniHistoryPickerCaption = "Talk about past games";
        public const string AlumniHistoryFactsBackCaption = "Back to past connections";
        public const string AlumniHistoryLearnBackCaption = "Back to learn and share";
        private const string AlumniHistoryFactPrefix = AlumniHistoryPickerCaption + ":";

        private bool IsAlumniHistoryPicker => ConversationPicker == AlumniHistoryPickerCaption
            || (ConversationPicker?.StartsWith(AlumniHistoryFactPrefix, StringComparison.Ordinal) ?? false);

        // Uses the existing picker so category changes, accepted commands, new partners and
        // revision changes clear this view through the same navigation owner as other questions.
        private bool BackAlumniHistoryPicker()
        {
            if (!(ConversationPicker?.StartsWith(AlumniHistoryFactPrefix, StringComparison.Ordinal) ?? false)) return false;
            conversationPick = AlumniHistoryPickerCaption;
            return true;
        }

        private void AlumniHistoryEntry(EpisodeState state, ContestantState npc, object token)
        {
            if (ConversationPicker != null || AlumniHistory.DiscussableFacts(state, npc.id).Count == 0) return;
            hud.Action(AlumniHistoryPickerCaption, () =>
            {
                if (!ConversationNavigationCurrent(token, state, npc.id)) return;
                TogglePicker(npc.id, AlumniHistoryPickerCaption);
            });
        }

        private bool RenderAlumniHistoryPicker(EpisodeState state, ContestantState npc, object token)
        {
            if (!IsAlumniHistoryPicker) return false;
            var facts = AlumniHistory.DiscussableFacts(state, npc.id);
            if (facts.Count == 0) { conversationPick = null; return false; }
            string factId = ConversationPicker == AlumniHistoryPickerCaption
                ? null : ConversationPicker.Substring(AlumniHistoryFactPrefix.Length);
            var selected = factId == null ? null : facts.FirstOrDefault(history => history.FactId == factId);
            if (factId != null && selected == null)
            {
                conversationPick = AlumniHistoryPickerCaption;
                factId = null;
            }

            hud.Heading("PAST CONNECTIONS");
            hud.Action(selected == null ? AlumniHistoryLearnBackCaption : AlumniHistoryFactsBackCaption, () =>
            {
                if (ConversationNavigationCurrent(token, state, npc.id)) BackConversationNavigation();
            });
            if (selected == null)
            {
                foreach (var history in facts)
                {
                    string subjectId = AlumniHistory.SubjectId(state, history, npc.id);
                    var subject = state.Find(subjectId);
                    if (subject == null) continue;
                    string id = history.FactId;
                    string caption = subjectId == state.playerId ? "Our previous games" : "Your history with " + subject.name;
                    hud.Action(caption, () =>
                    {
                        if (!ConversationNavigationCurrent(token, state, npc.id)) return;
                        conversationPick = AlumniHistoryFactPrefix + id;
                        conversationPickFor = npc.id;
                        Render();
                    });
                }
                return true;
            }

            string otherId = AlumniHistory.SubjectId(state, selected, npc.id);
            if (otherId == null) { conversationPick = AlumniHistoryPickerCaption; return true; }
            hud.Paragraph(selected.Fact.Summary);
            bool ourselves = otherId == state.playerId;
            HistoryApproach(ourselves ? "How do you feel about us playing again?" : "How do you feel about them now?", "curious");
            HistoryApproach(ourselves ? "We both know how this game can go." : "I know you have history together.", "respectful");
            HistoryApproach("Why should this time be any different?", "challenge");
            return true;

            void HistoryApproach(string caption, string approach)
            {
                bool available = EpisodeEngine.SocialActionsSpent(state) < EpisodeEngine.SocialActionBudget(state);
                var button = available ? hud.Action(caption, () =>
                {
                    if (!ConversationNavigationCurrent(token, state, npc.id)) return;
                    Commit(state, EpisodeCommandKind.AskForIntel, npc.id, otherId,
                        text: "history:" + selected.FactId + ":" + approach);
                }) : hud.LockedAction(caption);
                hud.Tag(button, available ? "1 action" : "No actions left");
            }
        }
    }
}
#endif
