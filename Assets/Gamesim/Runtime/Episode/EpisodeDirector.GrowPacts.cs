using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// Growing and managing the player's pacts in a conversation (ACTIONS-DEALS-ALLIANCES-PLAN C5): in
    /// BARGAIN, beside the pact's other rows, and only in a season that plays the commitment rules.
    ///
    /// <list type="bullet">
    /// <item><b>"Bring {name} into {pact}"</b>, in a conversation with anybody not in one of the player's
    /// pacts, a row for each such pact. It carries what it is for and the player's read of the one asked
    /// (<see cref="KnownOdds.Alliance"/>, the invitation's odds as the player reads them, under the note
    /// that says so), never the roll's own number and nothing of what the members think: their say is
    /// theirs, and only their answer tells it. A pact already holding <see cref="EpisodeEngine.LargestPact"/>
    /// in the house draws the row locked under the reason, as a fourth pact's proposal is drawn.</item>
    /// <item><b>"Leave {pact}"</b>: with one pact between the two of them it is "Leave our alliance", the
    /// caption it always had, now naming that pact; with more, one row a pact, "Leave The Riley Pact".
    /// A pact of three or more goes on without the player, and the pill says so. The week a member
    /// turned on it, the free exit's row (C2) is unchanged.</item>
    /// <item><b>"Rename {pact}…"</b>, in a conversation with a member of a pact the player founded: a
    /// row that opens the names on offer under it (<see cref="PactNames.For"/>), each "Rename {pact} to
    /// {name}", free. Once a week a pact; past that, the row is drawn locked under the reason.</item>
    /// </list>
    ///
    /// <para>Every caption is new and unique among the live controls, and no existing caption changes.
    /// A season without the rules draws what it always drew.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>"Bring Maya Hassan into The Riley Pact".</summary>
        public static string BringInCaption(string name, string pact) => "Bring " + name + " into " + pact;

        /// <summary>"Leave The Riley Pact": a row a pact, where the two of them share more than one.</summary>
        public static string LeavePactCaption(string pact) => "Leave " + pact;

        /// <summary>The row that opens a pact's names: "Rename The Riley Pact…". The ellipsis keeps it apart from the names' own rows.</summary>
        public static string RenamePickerCaption(string pact) => "Rename " + pact + "…";

        /// <summary>One name on offer: "Rename The Riley Pact to The Outsiders".</summary>
        public static string RenameCaption(string pact, string name) => "Rename " + pact + " to " + name;

        /// <summary>The pill on a leave from a pact of three or more: it costs what a leave costs, and the pact goes on without the player.</summary>
        public const string LeaveGoesOnTag = "costs warmth · it goes on";

        /// <summary>
        /// The leave rows under the commitment rules, outside the free exit's week: "Leave our alliance"
        /// for the one pact the two of them share, naming it; a row a pact where they share more.
        /// </summary>
        private void LeaveRows(EpisodeState state, ContestantState npc)
        {
            var shared = EpisodeEngine.SharedPacts(state, npc.id);
            if (shared.Count <= 1)
            {
                string only = shared.FirstOrDefault()?.id;
                hud.Tag(hud.Action("Leave our alliance", () => Commit(state, EpisodeCommandKind.LeaveAlliance, npc.id, only)),
                    LeaveTag(state, shared.FirstOrDefault()));
                return;
            }
            foreach (var pact in shared)
            {
                string pactId = pact.id;
                hud.Tag(hud.Action(LeavePactCaption(pact.name), () => Commit(state, EpisodeCommandKind.LeaveAlliance, npc.id, pactId)),
                    LeaveTag(state, pact));
            }
        }

        /// <summary>What a leave costs, and whether the pact goes on without the player: three or more of it in the house.</summary>
        private static string LeaveTag(EpisodeState state, AllianceState pact) =>
            pact != null && EpisodeEngine.InTheHouse(state, pact) > 2 ? LeaveGoesOnTag : Category(EpisodeCommandKind.LeaveAlliance);

        /// <summary>
        /// The rows that grow and name the player's pacts, under the commitment rules: a rename for each
        /// pact the player founded with this houseguest in it, then an asking for each of the player's
        /// pacts this houseguest is not in.
        /// </summary>
        private void PactRows(EpisodeState state, ContestantState npc)
        {
            if (!EpisodeEngine.CommitmentRulesOn(state)) return;
            foreach (var pact in EpisodeEngine.SharedPacts(state, npc.id).Where(pact => EpisodeEngine.PlayerFounded(state, pact)).ToList())
                RenameRows(state, npc, pact);
            foreach (var pact in state.alliances.Where(a => a.active && a.members.Contains(state.playerId) && !a.members.Contains(npc.id)).ToList())
                BringInRow(state, npc, pact);
        }

        /// <summary>
        /// "Bring {name} into {pact}": the player's read of the one asked beside what it is for, under
        /// the note that says whose read it is - once a render, above the first chance shown - or locked
        /// under the reason where the player can see it cannot be asked.
        /// </summary>
        private void BringInRow(EpisodeState state, ContestantState npc, AllianceState pact)
        {
            string caption = BringInCaption(npc.name, pact.name);
            string refusal = EpisodeEngine.BringInRefusal(state, npc.id, pact);
            if (refusal != null)
            {
                hud.Paragraph(refusal);
                hud.LockedAction(caption);
                return;
            }
            OddsAreYourRead(state, npc);
            string pactId = pact.id;
            hud.Tag(hud.Action(caption, () => Commit(state, EpisodeCommandKind.BringIntoAlliance, npc.id, pactId)),
                Category(EpisodeCommandKind.BringIntoAlliance) + " · " + KnownOdds.Alliance(state, npc.id).word);
        }

        /// <summary>
        /// "Rename {pact}…" and, while it is open, a row for each name on offer. It is a picker of this
        /// conversation's (<see cref="ConversationPicker"/>): one open at a time, shut by anything said,
        /// and stood at the top of the column when opened. Renamed already this week, the row is drawn
        /// locked under the reason.
        /// </summary>
        private void RenameRows(EpisodeState state, ContestantState npc, AllianceState pact)
        {
            string verb = RenamePickerCaption(pact.name);
            string refusal = EpisodeEngine.RenameRefusal(state, pact);
            if (refusal != null)
            {
                hud.Paragraph(refusal);
                hud.LockedAction(verb);
                return;
            }
            var names = PactNames.For(state, pact);
            if (names.Count == 0) return;
            string id = npc.id, pactId = pact.id;
            hud.Tag(hud.Action(verb, () => TogglePicker(id, verb)), Category(EpisodeCommandKind.RenameAlliance));
            if (!PickerOpen(id, verb)) return;
            foreach (string name in names)
            {
                string chosen = name;
                hud.Action(RenameCaption(pact.name, chosen), () =>
                {
                    ClosePicker();
                    Commit(state, EpisodeCommandKind.RenameAlliance, id, pactId, text: chosen);
                });
            }
        }
    }
}
