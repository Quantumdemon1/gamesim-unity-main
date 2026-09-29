using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// Prepare your final case (ENDGAME-PLAN F4b, mockup 37), under the finale rules: choose the
    /// narrative, read the season's résumé, choose three signature moments, and lock the argument
    /// (<see cref="FinalArgument"/>). A view over the station's panel, as the jury house is: a row
    /// after the questioning's and the speech's own controls opens it, so neither the opening focus
    /// nor any pinned caption moves. The theme and the moments are view state until the lock, which
    /// is the one command, carrying keys; after it the screen reads the argument back.
    ///
    /// <para>The locked argument fills the speech editor when the speeches open, and counts at the
    /// vote for the jurors whose theme it argues. Not built this round: a tile in the window at
    /// three to read the screen early.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        public const string FinalCaseCaption = "Prepare your final case";
        public const string LeaveFinalCaseCaption = "Close your final case";
        public const string LockArgumentCaption = "Lock final argument";

        /// <summary>Whether the final case is open over the station's panel. View state.</summary>
        private bool finalCaseOpen;

        /// <summary>The theme and moments chosen and not yet locked. View state, gone with the panel.</summary>
        private string chosenTheme;
        private readonly List<string> chosenMoments = new List<string>();

        /// <summary>Whether the final case is open. A read for tests.</summary>
        public bool InFinalCase => finalCaseOpen;

        /// <summary>Where the final case opens: a player finalist under the finale rules, from the final eviction until their speech is in.</summary>
        public static bool FinalCaseAvailable(EpisodeState state) =>
            state != null && EpisodeEngine.FinaleOn(state) && state.Find(state.playerId)?.status == ContestantStatus.Active
            && (state.phase == EpisodePhase.JuryQuestioning || state.phase == EpisodePhase.FinalSpeeches)
            && !state.finalSpeeches.Any(speech => speech.speakerId == state.playerId);

        /// <summary>Opens the final case. Public for tests.</summary>
        public void OpenFinalCase()
        {
            if (!FinalCaseAvailable(projected)) return;
            moveScreenId = null; comparingFinalists = false; juryHouseOpen = false;
            finalCaseOpen = true;
            Render();
        }

        /// <summary>Back to the panel the final case was opened over; an unlocked choice goes with it.</summary>
        public void CloseFinalCase()
        {
            finalCaseOpen = false;
            ForgetFinalCaseChoice();
            Render();
        }

        private void ForgetFinalCaseChoice() { chosenTheme = null; chosenMoments.Clear(); }

        /// <summary>Chooses the narrative. Nothing commits until the lock.</summary>
        public void ChooseTheme(string theme)
        {
            if (!FinalArgument.Themes.Contains(theme)) return;
            chosenTheme = theme;
            Render();
        }

        /// <summary>Chooses or lets go of a moment; three at most.</summary>
        public void ToggleMoment(string reference)
        {
            if (!chosenMoments.Remove(reference) && chosenMoments.Count < FinalArgument.MomentCount) chosenMoments.Add(reference);
            Render();
        }

        /// <summary>Whether the choice is complete: a theme, and three moments or every one the season has.</summary>
        private bool ReadyToLock(EpisodeState state) =>
            chosenTheme != null && chosenMoments.Count == FinalArgument.Required(state);

        /// <summary>The lock: the one command, with the keys. Back to the panel, which is where the season goes on.</summary>
        public void LockFinalArgument()
        {
            var state = projected;
            if (!phaseOpen || !FinalCaseAvailable(state) || state.finalArgument != null || !ReadyToLock(state)) return;
            string theme = chosenTheme;
            string references = FinalArgument.JoinReferences(chosenMoments);
            finalCaseOpen = false;
            ForgetFinalCaseChoice();
            Commit(state, EpisodeCommandKind.LockFinalArgument, second: theme, text: references);
        }

        /// <summary>Draws the final case when it is open and may be, and says whether it did; closes it when it may not be.</summary>
        private bool FinalCaseIfOpen(EpisodeState state)
        {
            if (!finalCaseOpen) return false;
            if (!FinalCaseAvailable(state)) { finalCaseOpen = false; ForgetFinalCaseChoice(); return false; }
            FinalCaseScreen(state);
            return true;
        }

        /// <summary>The door: a row after the panel's own controls, before the jury house's.</summary>
        private void FinalCaseDoor(EpisodeState state)
        {
            if (FinalCaseAvailable(state)) hud.Action(FinalCaseCaption, OpenFinalCase);
        }

        private void FinalCaseScreen(EpisodeState state)
        {
            var argument = state.finalArgument;
            var moments = FinalArgument.Moments(state);
            hud.ScreenHead("PREPARE YOUR FINAL CASE", argument != null ? "YOUR ARGUMENT IS LOCKED" : "WHAT WILL THE JURY REMEMBER?",
                "The story of your game and three moments that prove it. Your speech opens with it, and a juror who values it weighs it.");
            hud.Action(LeaveFinalCaseCaption, CloseFinalCase);

            hud.BeginSideCard(EpisodeHud.FinalCaseResumeName, "YOUR SEASON RÉSUMÉ");
            foreach (var line in Resume(state)) hud.CardLine(line, 14, UiTheme.Paper);
            hud.EndSideCard();

            if (argument != null)
            {
                hud.Heading("1 · Your narrative");
                hud.Paragraph(FinalArgument.Label(argument.theme) + ". " + FinalArgument.Claim(argument.theme));
                hud.Heading("2 · Your signature moments");
                foreach (var reference in argument.momentRefs)
                    hud.Paragraph(moments.FirstOrDefault(m => m.reference == reference)?.text ?? "A moment the record no longer holds.");
                hud.Footnote("Your final speech opens with this argument. You can still change the words, or skip the speech.");
                return;
            }

            hud.Heading("1 · Choose your narrative");
            hud.Tiles(EpisodeHud.FinalCaseThemesName, FinalArgument.Themes.Select(theme => new EpisodeHud.MoveTile
            {
                Caption = FinalArgument.Label(theme), Description = FinalArgument.Claim(theme),
                Corner = theme == chosenTheme ? "Chosen" : null, CornerTint = UiTheme.Allied, Glyph = ThemeGlyph(theme),
                Choose = () => ChooseTheme(theme),
            }).ToList(), EpisodeHud.TileStyle.Rows);

            int required = FinalArgument.Required(state);
            hud.Heading("2 · Select signature moments");
            if (moments.Count == 0) hud.Paragraph("The record holds no moment of yours to choose. Your narrative stands on its own.");
            else
            {
                hud.Footnote(chosenMoments.Count + " of " + required + " chosen. A moment backs one narrative; a juror who values it weighs the ones that back it.");
                hud.Tiles(EpisodeHud.FinalCaseMomentsName, moments.Select(moment => new EpisodeHud.MoveTile
                {
                    Caption = moment.text, Description = "Backs " + FinalArgument.Label(moment.theme) + ".",
                    Corner = chosenMoments.Contains(moment.reference) ? "Chosen" : null, CornerTint = UiTheme.Allied,
                    Glyph = ThemeGlyph(moment.theme), Choose = () => ToggleMoment(moment.reference),
                }).ToList(), EpisodeHud.TileStyle.List);
            }

            hud.Heading("3 · Lock final argument");
            bool ready = ReadyToLock(state);
            var lockButton = hud.Action(LockArgumentCaption, LockFinalArgument);
            if (lockButton != null) lockButton.interactable = ready;
            hud.Footnote(ready ? "Locking is final. Your speech is still yours to write."
                : "Choose a narrative and " + (required == 1 ? "one moment" : required + " moments") + " to lock your argument.");
        }

        /// <summary>The season's résumé, from the record: wins, the block, the weeks, power, alliances, and broken word both ways.</summary>
        private static List<string> Resume(EpisodeState state)
        {
            var you = state.Find(state.playerId);
            var ledger = state.ledger ?? new SeasonLedger();
            var lines = new List<string>();
            int hoh = you?.hohWins ?? 0, veto = you?.vetoWins ?? 0;
            int parts = you == null ? 0 : FinalistRead.Wins(state, you) - hoh - veto;
            lines.Add("Wins: " + hoh + " HoH · " + veto + " veto" + (parts > 0 ? " · " + parts + " final HoH part" + (parts == 1 ? "" : "s") : ""));
            lines.Add("Nominations survived: " + (you?.timesNominated ?? 0));
            lines.Add("Weeks: " + state.week);
            int reign = ledger.power.Count(p => p.hohId == state.playerId);
            lines.Add("Weeks in power: " + reign);
            var allies = state.alliances.Where(a => a.members.Contains(state.playerId)).ToList();
            lines.Add(allies.Count == 0 ? "Key alliances: none" : "Key alliances: " + string.Join(", ", allies.Select(a => a.name)));
            int brokeTo = state.promises.Count(p => p.fromId == state.playerId && p.status == PromiseStatus.Broken)
                + state.deals.Count(d => FinalistRead.DealBreaker(state, d) == state.playerId);
            int brokeFrom = state.promises.Count(p => p.toId == state.playerId && p.status == PromiseStatus.Broken)
                + state.deals.Count(d => { string by = FinalistRead.DealBreaker(state, d); return by != null && by != state.playerId; });
            lines.Add("Betrayals: " + brokeTo + " by you · " + brokeFrom + " against you");
            return lines;
        }

        private static string ThemeGlyph(string theme)
        {
            switch (theme)
            {
                case FinalArgument.Cerebral: return "target";
                case FinalArgument.Social: return "people";
                case FinalArgument.Aggressive: return "trophy";
                case FinalArgument.Sneaky: return "eye";
                default: return "handshake";
            }
        }
    }
}
