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
    /// <para>The mockup pass (MOCKUP-PASS-PLAN M12, mockup 56) makes it a dashboard: the stage's
    /// whole width with the station band naming the screen, compact cards across the top, the
    /// jury as a 2D tableau of callouts in their own recorded words (decisions 40 C and 41 A),
    /// and beside it the season's highlights and what the jury values.</para>
    ///
    /// <para>Not built: the door on the objectives card's jury strip (decision 42). The strip is
    /// chrome, drawn only in free roam, and a screen that opens from anywhere needs a panel flag
    /// of its own, as the notebook has.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        public const string JuryHouseCaption = "The jury house";
        public const string LeaveJuryHouseCaption = "Leave the jury house";

        /// <summary>The screen's name: the head's title and, while it is open, the station band's.</summary>
        private const string JuryHouseTitle = "THE JURY HOUSE";

        /// <summary>How many highlights the side card shows before "and n more".</summary>
        private const int JuryHighlightsShown = 6;

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

        /// <summary>
        /// "May be swayed by", in one line: the theme a juror values and the answers that land with
        /// them, whatever they ask ("Loyal to the end · answers that stand by your people").
        /// </summary>
        private static System.Collections.Generic.List<string> SwayedBy(ContestantState juror)
        {
            if (juror == null) return null;
            string theme = FinalArgument.ThemeOf(juror);
            return new System.Collections.Generic.List<string>
            {
                FinalArgument.Label(theme) + " · answers that "
                    + string.Join(" or ", FinaleQuestions.Values(theme).Select(r => FinaleQuestions.Caption(r).ToLowerInvariant())),
            };
        }

        private static readonly string[] CountWords =
        {
            "No", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
            "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen",
        };

        /// <summary>The jury's size as the head says it: "SEVEN JURORS · ONE DECISION".</summary>
        private static string JurySizeHeadline(int count) =>
            (count >= 0 && count < CountWords.Length ? CountWords[count] : count.ToString()).ToUpperInvariant()
            + (count == 1 ? " JUROR" : " JURORS") + " · ONE DECISION";

        private void JuryHouseScreen(EpisodeState state)
        {
            var house = JuryHouseRead.Read(state);
            bool rules = EpisodeEngine.FinaleOn(state);
            hud.StageAsPlace(JuryHouseTitle, "people");
            hud.JuryHouseHead(JuryHouseTitle, JurySizeHeadline(house.jurors.Count),
                "Relationships still matter. Where each juror stands with you, as far as you know it.",
                FinalCaseAvailable(state) ? "You can't sway the jury from here. Your final case can." : null);
            hud.Action(LeaveJuryHouseCaption, CloseJuryHouse);
            hud.BandLegend(JuryHouseRead.Bands);
            hud.Footnote("From your last read of each juror and what the house saw you do since. Never a count of votes.");

            var jurors = house.jurors.Select(read => (read, actor: state.Find(read.id))).Where(juror => juror.actor != null).ToList();
            hud.JurorCards(jurors.Select(juror => new EpisodeHud.JurorCard
            {
                Actor = juror.actor, Read = juror.read, Name = JuryHouseRead.ShortName(state, juror.read.id),
                Swayed = rules ? SwayedBy(juror.actor) : null,
            }).ToList());

            hud.BeginColumns(320f);
            hud.JuryTableau(jurors.Select(juror => new EpisodeHud.JurorCallout
            {
                Actor = juror.actor, Name = JuryHouseRead.ShortName(state, juror.read.id), Band = juror.read.band, Line = juror.read.line,
            }).ToList());
            hud.SideColumn();

            // The season between the player and each juror, newest first, and tonight's questions on
            // top at the Final 2. Never what the jurors say to each other: nothing records it.
            hud.BeginSideCard(EpisodeHud.JuryHighlightsName, "JURY DISCUSSION HIGHLIGHTS");
            hud.CardLine("What the record holds between you and each juror, newest first.", 12, UiTheme.Muted);
            foreach (var line in house.highlights.Take(JuryHighlightsShown))
                hud.CardLine(line, 13, line.StartsWith("Tonight", System.StringComparison.Ordinal) ? UiTheme.Glow : UiTheme.Paper);
            if (house.highlights.Count > JuryHighlightsShown) hud.CardLine("and " + (house.highlights.Count - JuryHighlightsShown) + " more", 12, UiTheme.Muted);
            if (house.highlights.Count == 0) hud.CardLine("Nothing on the record between you and the jury yet.", 13, UiTheme.Muted);
            hud.EndSideCard();

            hud.BeginSideCard(EpisodeHud.JuryMattersName, "WHAT MATTERS TO THIS JURY");
            if (rules)
            {
                // Under the finale rules, what the jury values: the theme each juror's lead trait
                // reads as, one row a theme, the most-held first. A value nobody holds is not drawn.
                var voting = FinalistRead.Jurors(state).Where(j => j.status != ContestantStatus.Expelled).ToList();
                var themes = voting.GroupBy(FinalArgument.ThemeOf).Where(group => group.Key != null)
                    .OrderByDescending(group => group.Count()).ThenBy(group => System.Array.IndexOf(FinalArgument.Themes, group.Key));
                foreach (var theme in themes)
                    hud.MattersRow(ThemeGlyph(theme.Key), FinalArgument.Value(theme.Key), theme.Count() + " of " + house.jurors.Count);
            }
            foreach (var line in house.matters) hud.CardLine(line, rules ? 13 : 14, rules ? UiTheme.Muted : UiTheme.Paper);
            hud.CardLine("The trait each juror leads with is the one their questions come from.", 12, UiTheme.Muted);
            hud.EndSideCard();
            hud.EndColumns();
        }
    }
}
