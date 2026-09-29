using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The jury house (ENDGAME-PLAN F4): the jurors as the player can know them, read by
    /// <see cref="JuryHouseRead"/>. A view over the station's own panel, as the comparison is: a
    /// free tile opens it in the window at three, and a row after the questioning's and the
    /// speech's own controls opens it at the Final 2, so neither the opening focus nor any
    /// pinned caption moves. It commits nothing, and "Leave the jury house" goes back.
    ///
    /// <para>Not built this round: the door on the jury strip. The strip is chrome, drawn only in
    /// free roam, and a screen that opens from anywhere needs a panel flag of its own, as the
    /// notebook has. The final case's preparation, the lock and the theme's effect wait for
    /// schema 21 (the plan's F4b).</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        public const string JuryHouseCaption = "The jury house";
        public const string LeaveJuryHouseCaption = "Leave the jury house";

        /// <summary>Whether the jury house is open over the station's panel. View state.</summary>
        private bool juryHouseOpen;

        /// <summary>Whether the jury house is open. A read for tests.</summary>
        public bool InJuryHouse => juryHouseOpen;

        /// <summary>Where the jury house can open: the window at three, or the Final 2's questioning and speeches, for a player who is a finalist.</summary>
        public static bool JuryHouseAvailable(EpisodeState state) =>
            state != null && state.Active.Any(actor => actor.id == state.playerId) && FinalistRead.Jurors(state).Count > 0
            && (Preparing(state) || state.phase == EpisodePhase.JuryQuestioning || state.phase == EpisodePhase.FinalSpeeches);

        /// <summary>Opens the jury house. Public for tests.</summary>
        public void OpenJuryHouse()
        {
            if (!JuryHouseAvailable(projected)) return;
            moveScreenId = null; comparingFinalists = false; finalCaseOpen = false;
            juryHouseOpen = true;
            Render();
        }

        /// <summary>Back to the panel the jury house was opened over.</summary>
        public void CloseJuryHouse()
        {
            juryHouseOpen = false;
            Render();
        }

        /// <summary>The free tile at three, beside the comparison.</summary>
        private static EpisodeHud.MoveTile JuryHouseTile(System.Action open) => new EpisodeHud.MoveTile
        {
            Caption = JuryHouseCaption,
            Description = "Where each juror stands with you, as far as you know it, and what they saw.",
            Corner = "Free", CornerTint = UiTheme.Allied, Glyph = "people", Foot = "Costs no action", Choose = open,
        };

        /// <summary>Draws the jury house when it is open and may be, and says whether it did; closes it when it may not be.</summary>
        private bool JuryHouseIfOpen(EpisodeState state)
        {
            if (!juryHouseOpen) return false;
            if (!JuryHouseAvailable(state)) { juryHouseOpen = false; return false; }
            JuryHouseScreen(state);
            return true;
        }

        /// <summary>The Final 2's door: a row after the panel's own controls, for a finalist player.</summary>
        private void JuryHouseDoor(EpisodeState state)
        {
            if (JuryHouseAvailable(state)) hud.Action(JuryHouseCaption, OpenJuryHouse);
        }

        /// <summary>"May be swayed by": the theme a juror values and the answers that land with them, whatever they ask.</summary>
        private static System.Collections.Generic.List<string> SwayedBy(ContestantState juror)
        {
            if (juror == null) return null;
            string theme = FinalArgument.ThemeOf(juror);
            return new System.Collections.Generic.List<string>
            {
                "An argument of " + FinalArgument.Label(theme),
                "Answers that " + string.Join(" or ", FinaleQuestions.Values(theme).Select(r => FinaleQuestions.Caption(r).ToLowerInvariant())),
            };
        }

        private void JuryHouseScreen(EpisodeState state)
        {
            var house = JuryHouseRead.Read(state);
            hud.ScreenHead("THE JURY HOUSE", house.jurors.Count + (house.jurors.Count == 1 ? " JUROR" : " JURORS"),
                "Where each juror stands with you, as far as you know it. Nothing here acts.");
            hud.Action(LeaveJuryHouseCaption, CloseJuryHouse);
            hud.Footnote(string.Join(" · ", JuryHouseRead.Bands)
                + ": from your last read of them, and what the house saw you do since. Never a count of votes.");
            hud.BeginSideCard(EpisodeHud.JuryMattersName, "WHAT MATTERS TO THIS JURY");
            foreach (var line in house.matters) hud.CardLine(line, 14, UiTheme.Paper);
            hud.CardLine("The trait each juror leads with is the one their questions come from.", 12, UiTheme.Muted);
            bool rules = EpisodeEngine.FinaleOn(state);
            if (rules)
            {
                // Under the finale rules, what the jury values: the theme each juror's lead trait reads as.
                var themes = FinalistRead.Jurors(state).Where(j => j.status != ContestantStatus.Expelled)
                    .GroupBy(FinalArgument.ThemeOf).OrderByDescending(g => g.Count()).ThenBy(g => System.Array.IndexOf(FinalArgument.Themes, g.Key));
                foreach (var theme in themes)
                    hud.CardLine(theme.Count() + " of " + house.jurors.Count + " value " + FinalArgument.Label(theme.Key) + ".", 13, UiTheme.Paper);
            }
            hud.EndSideCard();
            hud.JurorCards(house.jurors.Select(juror => new EpisodeHud.JurorCard { Actor = state.Find(juror.id), Read = juror, Swayed = rules ? SwayedBy(state.Find(juror.id)) : null })
                .Where(card => card.Actor != null).ToList());
        }
    }
}
