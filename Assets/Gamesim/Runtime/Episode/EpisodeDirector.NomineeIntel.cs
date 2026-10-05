using Gamesim.Simulation;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeDirector
    {
        public const string NomineeIntelPickerCaption = "Ask what they think of…";
        public static string NomineeIntelCaption(string name) => "Ask what they think of " + name;

        // A render-local authority, not a saved choice. Even cancel/reopen at the same revision
        // cannot authorize a retained callback. Other conversation pickers keep their own policy.
        private object nomineeIntelView;

        private void NomineeIntelRows(EpisodeState state, ContestantState listener)
        {
            var targets = NomineeIntel.Targets(state, listener.id);
            nomineeIntelView = null;
            if (targets.Count == 0) return;
            object view = new object();
            nomineeIntelView = view;
            long generation = loadGeneration;
            bool Current() => this != null && isActiveAndEnabled && ReferenceEquals(nomineeIntelView, view)
                && generation == loadGeneration && IsCurrentDiaryRevision(state)
                && focusedNpc != null && focusedNpc.Id == listener.id;
            hud.BeginPersonPicker(NomineeIntelPickerCaption, LearnTag, PickerOpen(listener.id, NomineeIntelPickerCaption),
                () => { if (Current()) TogglePicker(listener.id, NomineeIntelPickerCaption); }, null);
            try
            {
                foreach (string id in targets)
                {
                    string chosen = id;
                    hud.ActionFor(chosen, NomineeIntelCaption(state.Find(chosen).name), () =>
                    {
                        if (!Current() || !PickerOpen(listener.id, NomineeIntelPickerCaption)
                            || !NomineeIntel.Targets(projected, listener.id).Contains(chosen)) return;
                        nomineeIntelView = null;
                        ClosePicker();
                        Commit(state, EpisodeCommandKind.AskForIntel, listener.id, chosen);
                    });
                }
            }
            finally { hud.EndPersonPicker(); }
        }
    }
}
