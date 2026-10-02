using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// Prepare your final case (ENDGAME-PLAN F4b, mockup 37; MOCKUP-PASS M15, mockup 57), under the
    /// finale rules: choose the narrative, read the season's résumé, choose three signature moments,
    /// and lock the argument (<see cref="FinalArgument"/>). A view over the station's panel, as the
    /// jury house is: a row after the questioning's and the speech's own controls opens it, so
    /// neither the opening focus nor any pinned caption moves. The theme and the moments are view
    /// state until the lock, which is the one command, carrying keys; after it the screen reads the
    /// argument back.
    ///
    /// <para>The screen is the station's own (EpisodeHud.FinalCase.cs): its band names it, and three
    /// columns - the narratives, the résumé, the moments as cards with the faces they are about -
    /// stand over a tray of the moments chosen, in the order they were chosen, and the gold lock.</para>
    ///
    /// <para>At three, a free tile beside the jury house's opens the same screen to read early: the
    /// same choices, as view state dropped on close, and in the lock's place the line that it opens
    /// at the Final 2. The locked argument fills the speech editor when the speeches open, and
    /// counts at the vote for the jurors whose theme it argues.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        public const string FinalCaseCaption = "Prepare your final case";
        public const string LeaveFinalCaseCaption = "Close your final case";
        public const string LockArgumentCaption = "Lock final argument";

        /// <summary>The line in the lock's place at three.</summary>
        public const string FinalCaseLockLaterLine = "The lock opens at the Final 2.";

        /// <summary>The brand's line over the case (decision 10): an existing one, unattributed.</summary>
        public const string FinalCaseBrandLine = "“Same house. Different stories.”";

        /// <summary>Whether the final case is open over the station's panel. View state.</summary>
        private bool finalCaseOpen;

        /// <summary>Whether the open case was opened at three, to read: it closes with the window rather than carrying on to the Final 2.</summary>
        private bool finalCaseEarly;

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

        /// <summary>Where the final case opens to read, with no lock: a finalist in the window at three under the finale rules.</summary>
        public static bool FinalCaseEarlyAvailable(EpisodeState state) => Preparing(state) && EpisodeEngine.FinaleOn(state);

        /// <summary>Opens the final case, or at three its early read. Public for tests.</summary>
        public void OpenFinalCase()
        {
            var state = projected;
            bool early = FinalCaseEarlyAvailable(state);
            if (!early && !FinalCaseAvailable(state)) return;
            moveScreenId = null; comparingFinalists = false; juryHouseOpen = false;
            finalCaseOpen = true;
            finalCaseEarly = early;
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

        /// <summary>
        /// Draws the final case when it is open and may be, and says whether it did; closes it when
        /// it may not be. An early read closes with the window at three, so it never reopens itself
        /// at the Final 2.
        /// </summary>
        private bool FinalCaseIfOpen(EpisodeState state)
        {
            if (!finalCaseOpen) return false;
            if (!(finalCaseEarly ? FinalCaseEarlyAvailable(state) : FinalCaseAvailable(state))) { finalCaseOpen = false; ForgetFinalCaseChoice(); return false; }
            FinalCaseScreen(state);
            return true;
        }

        /// <summary>The door: a row after the panel's own controls, before the jury house's.</summary>
        private void FinalCaseDoor(EpisodeState state)
        {
            if (FinalCaseAvailable(state)) hud.Action(FinalCaseCaption, OpenFinalCase);
        }

        /// <summary>The free tile at three, beside the jury house's: the case to read before it can be locked.</summary>
        private static EpisodeHud.MoveTile FinalCaseTile(System.Action open) => new EpisodeHud.MoveTile
        {
            Caption = FinalCaseCaption,
            Description = "Read your case before the lock opens at the Final 2: your story, résumé and moments.",
            Corner = "Free", CornerTint = UiTheme.Allied, Glyph = "crown", Foot = "Costs no action", Choose = open,
        };

        private void FinalCaseScreen(EpisodeState state)
        {
            var argument = state.finalArgument;
            bool early = !FinalCaseAvailable(state);
            var moments = FinalArgument.Moments(state);
            int required = FinalArgument.Required(state);
            // The station's panel is the screen: its band names it, and the columns take the frame.
            hud.StationScreen("PREPARE YOUR FINAL CASE", "Shape your story. Show the jury why you should win.", "crown", UiTheme.Gold);
            hud.FinalCaseHead(argument != null ? "YOUR ARGUMENT IS LOCKED" : "WHAT WILL THE JURY REMEMBER?",
                "The story of your game and three moments that prove it. Your speech opens with it, and a juror who values it weighs it.",
                early ? "FINAL 3 · Read early" : "FINAL 2 · Jury review", "gavel", FinalCaseBrandLine);
            // The way back, "Close your final case", is the screen's last row, under the columns and
            // the tray: it used to be a full-width button over the form, so the screen's first
            // control was the one that leaves it (UI-UX-PASS-PLAN E0).

            hud.BeginCaseColumns(.30f, .34f, .36f);
            hud.SectionHead("journal", "1 · YOUR NARRATIVE", argument != null ? "The story you locked." : "Choose how the jury remembers you.");
            if (argument != null)
                hud.ChosenTile(EpisodeHud.FinalCaseLockedName, new EpisodeHud.MoveTile
                {
                    Caption = FinalArgument.Label(argument.theme), Description = FinalArgument.Claim(argument.theme),
                    Corner = "Locked", CornerTint = UiTheme.Gold, Glyph = ThemeGlyph(argument.theme),
                });
            else
                hud.Tiles(EpisodeHud.FinalCaseThemesName, FinalArgument.Themes.Select(theme => new EpisodeHud.MoveTile
                {
                    Caption = FinalArgument.Label(theme), Description = FinalArgument.Claim(theme),
                    Corner = theme == chosenTheme ? EpisodeHud.ChosenWord : null, CornerTint = UiTheme.Allied, Glyph = ThemeGlyph(theme),
                    Selected = theme == chosenTheme, Choose = () => ChooseTheme(theme),
                }).ToList(), EpisodeHud.TileStyle.List);

            hud.NextCaseColumn();
            hud.SectionHead("trophy", "2 · YOUR SEASON RÉSUMÉ", "Your record, as the house saw it.");
            // The quote slot (decision 44): the chosen claim, unquoted; after the lock, the opening
            // of the speech it templates. Nobody else's words.
            string argued = argument != null ? argument.theme : chosenTheme;
            hud.FinalCaseResumeCard(new EpisodeHud.ResumeCard
            {
                Actor = state.Find(state.playerId), Standing = "Final " + state.Active.Count(),
                QuoteLabel = argument != null ? "YOUR SPEECH OPENS" : "YOUR CLAIM",
                Quote = argued == null ? null : argument != null ? FinalArgument.Opening(argued) : FinalArgument.Claim(argued),
                QuoteEmpty = "Choose a narrative, and your claim reads here.",
                Read = FinalCaseResume.Read(state),
            });

            hud.NextCaseColumn();
            if (argument != null)
            {
                hud.SectionHead("star", "3 · YOUR MOMENTS", "The moments you locked.");
                hud.MomentCards(EpisodeHud.FinalCaseMomentsName, (argument.momentRefs ?? new List<string>()).Select(reference =>
                {
                    var moment = moments.FirstOrDefault(m => m.reference == reference);
                    return new EpisodeHud.MomentCard
                    {
                        Caption = moment?.text ?? "A moment the record no longer holds.", SubjectId = FinalArgument.SubjectOf(state, reference),
                        Week = moment?.week ?? 0, Title = FinalArgument.Title(reference), Glyph = ThemeGlyph(FinalArgument.ThemeOfReference(reference)),
                        Backs = "Backs " + FinalArgument.Label(FinalArgument.ThemeOfReference(reference)), Selected = true,
                    };
                }).ToList());
                hud.EndCaseColumns();
                hud.Footnote("Your final speech opens with this argument. You can still change the words, or skip the speech.");
                hud.Action(LeaveFinalCaseCaption, CloseFinalCase);
                return;
            }
            hud.SectionHead("star", "3 · SIGNATURE MOMENTS", required == 0 ? "None on the record yet."
                : required == 1 ? "Choose the one that backs your story." : "Choose " + required + " that back your story.");
            if (moments.Count == 0) hud.CardLine("The record holds no moment of yours to choose. Your narrative stands on its own.", 15, UiTheme.Paper);
            else
            {
                hud.Footnote("A moment backs one narrative; a juror who values it weighs the ones that back it.");
                hud.MomentCards(EpisodeHud.FinalCaseMomentsName, moments.Select(moment => new EpisodeHud.MomentCard
                {
                    Caption = moment.text, SubjectId = FinalArgument.SubjectOf(state, moment.reference), Week = moment.week,
                    Title = FinalArgument.Title(moment.reference), Backs = "Backs " + FinalArgument.Label(moment.theme), Glyph = ThemeGlyph(moment.theme),
                    Selected = chosenMoments.Contains(moment.reference), Choose = () => ToggleMoment(moment.reference),
                }).ToList());
            }
            hud.EndCaseColumns();

            hud.SectionHead("key", "4 · LOCK YOUR CASE", early ? "Read it now; lock it at the Final 2." : "Your narrative and your moments, in the order you chose them.");
            if (required > 0)
            {
                var slots = new List<EpisodeHud.TraySlot>();
                for (int i = 0; i < required; i++)
                {
                    var moment = i < chosenMoments.Count ? moments.FirstOrDefault(m => m.reference == chosenMoments[i]) : null;
                    slots.Add(moment == null ? new EpisodeHud.TraySlot()
                        : new EpisodeHud.TraySlot { SubjectId = FinalArgument.SubjectOf(state, moment.reference), Week = moment.week, Title = FinalArgument.Title(moment.reference) });
                }
                hud.FinalCaseTray(required == 1 ? "YOUR FINAL MOMENT" : "YOUR FINAL " + required + " MOMENTS",
                    chosenMoments.Count + " / " + required + " selected", slots);
            }
            // At three the case is read, not locked: the lock's place says when it opens.
            if (early) hud.CardLine(FinalCaseLockLaterLine, 15, UiTheme.Gold);
            else
            {
                bool ready = ReadyToLock(state);
                hud.FinalCaseLock(LockArgumentCaption, LockFinalArgument, ready, "Next: deliver your final speech to the jury. Locking is final.",
                    ready ? null : "Choose a narrative and " + (required == 1 ? "one moment" : required + " moments") + " to lock your argument.");
            }
            hud.Action(LeaveFinalCaseCaption, CloseFinalCase);
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
