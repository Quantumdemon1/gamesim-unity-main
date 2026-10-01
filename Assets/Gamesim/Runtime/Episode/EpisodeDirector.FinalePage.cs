using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The finale page (UI-UX-PASS-PLAN F0; decisions 7, 10, 11 and 13): what the finished panel
    /// says and offers, handed to the HUD as one spec (EpisodeHud.FinalePage.cs). The panel was a
    /// column of sentences - the winner, the Game Sense line, a line a ballot - and six rows.
    ///
    /// <para>Every word is the record's: the winner and the runner-up, the Game Sense report, the
    /// jury's ballots as the engine wrote them (public at the finale, decision 7, in cast order),
    /// and the counts the season report's tiles already count. The six ways on keep their captions
    /// word for word, each once. Which juror's reason is open and whether the jury's questions are
    /// open as the step are view state, forgotten with the panel; nothing here commits.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The status line after the finale's commit (decision 13); "Saved locally." and the career's note follow it.</summary>
        public const string SeasonCompleteToast = "Season complete! Thanks for playing.";

        /// <summary>The way back from the jury's questions, open as the step, to the page.</summary>
        public const string BackToFinaleCaption = "Back to the finale";

        /// <summary>The jury card's foot line for the player's own ballot, which carries no reason.</summary>
        public const string YourBallotLine = "Your ballot";

        /// <summary>The lines under the ways on: where each leads, as the mockup says it, true of what each opens.</summary>
        public const string SeasonReportLine = "Stats, moments and the season's timeline.";
        public const string WatchFinaleReplayLine = "Hear the jury's vote read again.";
        public const string MainMenuLine = "Start a new season or continue a save.";
        public const string NewSeasonLine = "Starting another season keeps this one's save.";
        public const string ReviewSeasonLine = "Your choices and votes are preserved in the notebook.";
        public const string JuryQuestionsLine = "What each juror asked, and the reason they gave.";

        /// <summary>The highlights' captions that are the page's own; the rest are the report's tile captions.</summary>
        public const string EvictionsTileCaption = "Evictions", EvictionTileCaption = "Eviction", JuryVoteTileCaption = "Jury vote",
            WinnerTileCaption = "Winner", WinnerTileValue = "One";

        /// <summary>The juror whose reason is the jury card's foot line, or null. View state.</summary>
        private string finalePressedJurorId;
        /// <summary>Whether the next render of the jury's questions puts the keyboard on the way back: once, as they open.</summary>
        private bool finaleFocusBack;

        /// <summary>The juror whose reason is open on the finale page, or null. A read for tests.</summary>
        public string FinalePressedJuror => finalePressedJurorId;

        /// <summary>A juror's face is a control captioned with their name; the player's says so.</summary>
        public static string JurorFaceCaption(ContestantState juror) =>
            juror == null ? string.Empty : HudPrimitives.WithYou(juror.name, juror.isPlayer);

        /// <summary>
        /// The line a press on a juror's face shows: the reason they gave with their vote, word for
        /// word; "Your ballot" for the player's own, which carries none; and for a ballot an older
        /// save kept without one, whom it was for.
        /// </summary>
        public static string JurorReasonLine(SeasonReport.JuryBallot ballot) =>
            ballot.IsPlayer ? YourBallotLine
            : !string.IsNullOrEmpty(ballot.Reason) ? ballot.Reason
            : "Voted for " + ballot.Finalist + ".";

        /// <summary>The jury vote as the highlights' tile says it, the winner's votes first: "5–2".</summary>
        public static string JuryVoteTileValue(int forWinner, int forRunnerUp) => forWinner + "–" + forRunnerUp;

        /// <summary>
        /// A finalist on a juror's chip: their first name, or "You" for the player, as every reader
        /// names the player where it names them (the record's "Voted for you").
        /// </summary>
        public static string FinalistChipWord(ContestantState finalist) =>
            finalist == null ? string.Empty : finalist.isPlayer ? "You" : FinalistRead.FirstName(finalist.name);

        /// <summary>
        /// SEASON HIGHLIGHTS: six real counts (decision 11) - the houseguests, the weeks, the
        /// evictions, the competitions held (every one has one winner, the final parts included),
        /// the jury's vote and the one winner. Never a count the ledger does not keep.
        /// </summary>
        public static List<(string Caption, string Value)> FinaleHighlights(EpisodeState state)
        {
            var highlights = new List<(string, string)>();
            if (state?.contestants == null) return highlights;
            int evictions = state.contestants.Count(c => c.status == ContestantStatus.Jury || c.status == ContestantStatus.Evicted);
            int competitions = state.contestants.Sum(c => FinalistRead.Wins(state, c));
            var ballots = SeasonReport.JuryBallots(state);
            int forWinner = ballots.Count(b => b.FinalistId == state.winnerId), forRunnerUp = ballots.Count(b => b.FinalistId == state.runnerUpId);
            highlights.Add((SeasonReport.HouseguestsTileCaption, state.contestants.Count.ToString()));
            highlights.Add((state.week == 1 ? "Week" : "Weeks", state.week.ToString()));
            highlights.Add((evictions == 1 ? EvictionTileCaption : EvictionsTileCaption, evictions.ToString()));
            highlights.Add((SeasonReport.CompetitionsTileCaption, competitions.ToString()));
            highlights.Add((JuryVoteTileCaption, JuryVoteTileValue(forWinner, forRunnerUp)));
            highlights.Add((WinnerTileCaption, state.Find(state.winnerId) != null ? WinnerTileValue : "0"));
            return highlights;
        }

        /// <summary>Whether the finished panel is the finale page: the season over, with its two finalists, and nothing of its own over it.</summary>
        private bool FinalePageBeat(EpisodeState s) => s != null && s.phase == EpisodePhase.Finished && !challengeActive && s.pendingDiary == null
            && s.Find(s.winnerId) != null && s.Find(s.runnerUpId) != null;

        /// <summary>What closing the panel throws away: the page opens again with no reason open.</summary>
        private void ForgetFinaleView()
        {
            finalePressedJurorId = null;
            finaleFocusBack = false;
        }

        /// <summary>
        /// The finished panel: the finale page, or - with the jury's questions open - the record as
        /// the step, the disclosure at its head to fold it again and the way back pinned in the
        /// footer, as the free-time board opens a beat. The keyboard goes to the way back as the
        /// step opens, so a second Enter on what opened it goes back.
        /// </summary>
        private void FinalePage(EpisodeState state)
        {
            var winner = state.Find(state.winnerId);
            var runnerUp = state.Find(state.runnerUpId);
            if (winner == null || runnerUp == null)
            {
                // A save without both finalists cannot load; the sentence and the four ways stay for one that somehow does.
                hud.Paragraph("The season is over.");
                hud.Action("Season report", ShowSeasonReport);
                hud.Action(SeasonReport.NewSeasonCaption, NewSeason);
                hud.Action(SeasonReport.ReviewCaption, OpenJournal);
                hud.Action(SeasonReport.MainMenuCaption, OpenMainMenu);
                return;
            }
            // A focus the last render asked for, which a render in between would otherwise drop.
            hud.KeepFocusAsked();
            if (juryQuestionsOpen)
            {
                hud.Disclosure(JuryQuestionsCaption, true, ToggleJuryQuestions);
                JuryQuestionsRecord(state);
                hud.PinnedSecondary(BackToFinaleCaption, CloseJuryQuestions);
                if (finaleFocusBack) hud.FocusWhenWired(BackToFinaleCaption);
                finaleFocusBack = false;
                return;
            }
            finaleFocusBack = false;
            hud.StrategyWholeWidth();
            hud.FinalePage(FinaleSpec(state, winner, runnerUp));
        }

        /// <summary>The jury's questions opened as the step, or folded again by their disclosure. View state.</summary>
        private void ToggleJuryQuestions()
        {
            juryQuestionsOpen = !juryQuestionsOpen;
            finaleFocusBack = juryQuestionsOpen;
            Render();
        }

        /// <summary>Back from the jury's questions to the page.</summary>
        private void CloseJuryQuestions()
        {
            juryQuestionsOpen = false;
            finaleFocusBack = false;
            Render();
        }

        /// <summary>A juror's face pressed: their reason is the jury card's foot line; pressed again, it folds. View state.</summary>
        private void PressJurorFace(string jurorId)
        {
            finalePressedJurorId = finalePressedJurorId == jurorId ? null : jurorId;
            Render();
        }

        // ------------------------------------------------------------ what the page says

        private EpisodeHud.FinalePageSpec FinaleSpec(EpisodeState state, ContestantState winner, ContestantState runnerUp)
        {
            var ballots = SeasonReport.JuryBallots(state);
            var sense = GameSense.Evaluate(state);
            var spec = new EpisodeHud.FinalePageSpec
            {
                WinnerId = winner.id, RunnerUpId = runnerUp.id,
                // The sentence the whole-season walk and the reveal's test read, word for word.
                WinnerLine = "Winner: " + winner.name + ". Runner-up: " + runnerUp.name + ".",
                GameSenseFirst = "Game Sense " + sense.score + " · Competitions " + sense.competitions,
                GameSenseSecond = "Strategy " + sense.strategy + " · Social " + sense.social,
                ForWinner = ballots.Count(b => b.FinalistId == winner.id),
                ForRunnerUp = ballots.Count(b => b.FinalistId == runnerUp.id),
                PressedJurorId = finalePressedJurorId,
                PressJuror = PressJurorFace,
                Jurors = new List<EpisodeHud.FinaleJuror>(),
                Highlights = new List<EpisodeHud.FinaleStat>(),
                Ways = new List<EpisodeHud.FinaleWay>(),
                SecondRow = new List<EpisodeHud.FinaleWay>(),
            };
            // The jury in cast order, a face a ballot.
            foreach (var juror in state.contestants)
            {
                int index = ballots.FindIndex(b => b.JurorId == juror.id);
                if (index < 0) continue;
                var ballot = ballots[index];
                spec.Jurors.Add(new EpisodeHud.FinaleJuror
                {
                    Id = juror.id, Caption = JurorFaceCaption(juror), FinalistId = ballot.FinalistId,
                    Reason = JurorReasonLine(ballot), IsPlayer = juror.isPlayer,
                });
            }
            // The highlights, each with the pack's mark; Kit 6's calendar for the weeks, the
            // generated glyph for the evictions, which no pack draws.
            var marks = new (string icon, string fallback, Color tint)[]
            {
                (PackArt.Pack9SeasonFinalePeopleIcon, "people", UiTheme.Accent),
                (PackArt.KitIconCalendar, "calendar", UiTheme.Heading),
                (null, "evicted", UiTheme.Danger),
                (PackArt.Pack9SeasonFinaleTrophyIcon, "trophy", UiTheme.Gold),
                (PackArt.Pack9SeasonFinaleJuryIcon, "gavel", UiTheme.Strategic),
                (PackArt.Pack9SeasonFinaleCrownIcon, "crown", UiTheme.Gold),
            };
            var counts = FinaleHighlights(state);
            for (int i = 0; i < counts.Count; i++)
            {
                var (icon, fallback, tint) = marks[Mathf.Min(i, marks.Length - 1)];
                spec.Highlights.Add(new EpisodeHud.FinaleStat { Caption = counts[i].Caption, Value = counts[i].Value, Icon = icon, Fallback = fallback, Tint = tint });
            }
            // The ways on: the three wide tiles, the replay only when there is a vote to read again
            // (EpisodeDirector.Finale.cs), then the thin row.
            spec.Ways.Add(new EpisodeHud.FinaleWay { Caption = "Season report", Line = SeasonReportLine,
                Icon = PackArt.Pack9SeasonFinaleReportIcon, Fallback = "journal", Choose = ShowSeasonReport });
            if (CanReplayJuryReveal(state))
                spec.Ways.Add(new EpisodeHud.FinaleWay { Caption = WatchFinaleReplayCaption, Line = WatchFinaleReplayLine,
                    Icon = PackArt.Pack9SeasonFinaleReplayIcon, Fallback = "camera", Choose = ReplayJuryReveal });
            spec.Ways.Add(new EpisodeHud.FinaleWay { Caption = SeasonReport.MainMenuCaption, Line = MainMenuLine,
                Icon = PackArt.Pack9SeasonFinaleHomeIcon, Fallback = "house", Choose = OpenMainMenu });
            spec.SecondRow.Add(new EpisodeHud.FinaleWay { Caption = SeasonReport.NewSeasonCaption, Line = NewSeasonLine,
                Icon = PackArt.Pack9SeasonFinaleStarIcon, Fallback = "star", Choose = NewSeason });
            spec.SecondRow.Add(new EpisodeHud.FinaleWay { Caption = SeasonReport.ReviewCaption, Line = ReviewSeasonLine,
                Icon = PackArt.KitIconBook, Fallback = "journal", Choose = OpenJournal });
            spec.SecondRow.Add(new EpisodeHud.FinaleWay { Caption = JuryQuestionsCaption, Line = JuryQuestionsLine,
                Icon = PackArt.Pack9SeasonFinaleJuryIcon, Fallback = "gavel", Choose = ToggleJuryQuestions });
            return spec;
        }
    }
}
