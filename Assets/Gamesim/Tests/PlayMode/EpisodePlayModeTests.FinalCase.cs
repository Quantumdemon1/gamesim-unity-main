using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Schema 21's finale rules on screen (ENDGAME-PLAN F4b/F5b): a season the director starts plays
    /// them; the final case locks an argument from its own screen and fills the speech; a history
    /// question offers its responses as tiles, says nothing of which lands, and shows the engine's
    /// note after the commit, through a reload.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Endgame_ASeasonTheDirectorStartsPlaysTheFinaleRules()
        {
            director.StartSeason(null);
            yield return SettleCast();
            Assert.That(director.Snapshot.finaleRulesStartWeek, Is.EqualTo(1), "The quick start.");
            director.StartSeason(new SeasonBuilder.Choice());
            yield return SettleCast();
            Assert.That(director.Snapshot.finaleRulesStartWeek, Is.EqualTo(1), "The cast screen's season.");
            Assert.That(EpisodeEngine.FinaleOn(director.Snapshot), Is.True);
            director.SaveNow();
            director.LoadNow();
            yield return SettleCast();
            Assert.That(EpisodeEngine.FinaleOn(director.Snapshot), Is.True, "and it survives a save.");
        }

        /// <summary>
        /// The final case: its door after the questioning's controls and before the jury house's;
        /// the narratives, the résumé, the moments, the lock waiting on a whole choice, at both text
        /// sizes; the lock the one commit, back to the panel; the argument read back; the speech
        /// editor filled with it when the speeches open.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_TheFinalCaseLocksAnArgumentAndFillsTheSpeech()
        {
            HoldTheHouseForTheFixture();
            yield return InstallFinaleFixture(true, finaleRules: true);
            yield return PutAwayTheCards();
            var state = director.Snapshot;
            Assert.That(EpisodeEngine.FinaleOn(state), Is.True);
            var moments = FinalArgument.Moments(state);
            int required = FinalArgument.Required(state);
            Assert.That(required, Is.GreaterThan(0), "A season played to the final has moments to choose.");

            yield return OpenFinalePanel();
            var door = ButtonWithCaption(EpisodeDirector.FinalCaseCaption);
            var skip = ButtonWithCaption(EpisodeHud.JurySkipCaption);
            var house = ButtonWithCaption(EpisodeDirector.JuryHouseCaption);
            Assert.That(door.transform.GetSiblingIndex(), Is.GreaterThan(skip.transform.GetSiblingIndex()), "After the panel's own controls,");
            Assert.That(door.transform.GetSiblingIndex(), Is.LessThan(house.transform.GetSiblingIndex()), "and before the jury house's.");
            Assert.That(LastActive(EpisodeHud.JuryWaysOnName).GetComponentsInChildren<Button>().Select(button => button.name),
                Is.EqualTo(new[] { EpisodeHud.JurySkipCaption, EpisodeDirector.FinalCaseCaption, EpisodeDirector.JuryHouseCaption }),
                "One thin row of ways on after the live layout, in that order (MOCKUP-PASS M11).");

            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                yield return OpenFinalePanel();
                ButtonWithCaption(EpisodeDirector.FinalCaseCaption).onClick.Invoke();
                yield return Frames(2);
                Canvas.ForceUpdateCanvases();
                Assert.That(director.InFinalCase, Is.True);
                var panel = LastActive("Episode panel");
                string words = Words(panel);
                Assert.That(words, Does.Contain("PREPARE YOUR FINAL CASE").And.Contain("YOUR SEASON RÉSUMÉ"));
                foreach (var theme in FinalArgument.Themes)
                    Assert.That(ButtonWithCaption(FinalArgument.Label(theme)).transform.IsChildOf(LastActive(EpisodeHud.FinalCaseThemesName)), Is.True, theme);
                foreach (var moment in moments)
                    Assert.That(ButtonWithCaption(moment.text).transform.IsChildOf(LastActive(EpisodeHud.FinalCaseMomentsName)), Is.True, moment.reference);
                Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.LockArgumentCaption).interactable, Is.False, "Nothing chosen, nothing to lock.");
                AssertDecisionCopyFits(LastActive(EpisodeHud.FinalCaseThemesName));
                AssertDecisionCopyFits(LastActive(EpisodeHud.FinalCaseResumeName));
                AssertTheFinalCaseScreen(state, panel, moments, required);
                string where = "The final case" + (larger ? " at the larger text" : "");
                AssertEveryLabelDraws(where, panel);
                AssertAStationScreen(where);
                AssertTheCloseUnderTheTray(early: false);
                if (Application.isBatchMode)
                    yield return CaptureFraming(larger ? "endgame-final-case-large" : "endgame-final-case", settle: false,
                        inspect: frame => LogTheFit(where + " on the 16:9 frame"));
                ButtonWithCaption(EpisodeDirector.LeaveFinalCaseCaption).onClick.Invoke();
                yield return Frames(1);
                Assert.That(director.InFinalCase, Is.False);
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);

            // Choose, and lock.
            yield return OpenFinalePanel();
            ButtonWithCaption(EpisodeDirector.FinalCaseCaption).onClick.Invoke();
            yield return Frames(1);
            ButtonWithCaption(FinalArgument.Label(FinalArgument.Cerebral)).onClick.Invoke();
            yield return Frames(1);
            // The chosen narrative says so three ways, one of them words; the rest keep an empty ring.
            var chosen = FindButton(FinalArgument.Label(FinalArgument.Cerebral));
            Assert.That(chosen.GetComponentsInChildren<Image>().Any(image => image.name == "Chosen mark"), Is.True, "Its ring is filled,");
            Assert.That(Words((RectTransform)chosen.transform), Does.Contain(EpisodeHud.ChosenWord), "and it says it is chosen.");
            foreach (var theme in FinalArgument.Themes.Where(t => t != FinalArgument.Cerebral))
            {
                var other = FindButton(FinalArgument.Label(theme));
                Assert.That(other.GetComponentsInChildren<Image>().Count(image => image.name == "Choice ring"), Is.EqualTo(1), theme + " is a choice,");
                Assert.That(other.GetComponentsInChildren<Image>().Any(image => image.name == "Chosen mark"), Is.False, theme + " not the one chosen.");
            }
            // The quote slot (decision 44): the claim, unquoted.
            string resume = Words(LastActive(EpisodeHud.FinalCaseResumeName));
            Assert.That(resume, Does.Contain(FinalArgument.Claim(FinalArgument.Cerebral)).And.Not.Contain("“"), "The chosen claim, in the player's own résumé.");
            foreach (var moment in moments.Take(required))
            {
                Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.LockArgumentCaption).interactable, Is.False, "Not until the choice is whole.");
                ButtonWithCaption(moment.text).onClick.Invoke();
                yield return Frames(1);
            }
            // The tray holds them in the order they were chosen, by title, never by their own words.
            var tray = LastActive(EpisodeHud.FinalCaseTrayName);
            var picked = moments.Take(required).ToList();
            for (int i = 0; i < picked.Count; i++)
            {
                string slot = Words((RectTransform)tray.Find(EpisodeHud.FinalCaseSlotPrefix + (i + 1)));
                Assert.That(slot, Does.Contain(FinalArgument.Title(picked[i].reference)), "Slot " + (i + 1) + " is the moment chosen " + (i + 1) + ".");
                Assert.That(slot, Does.Not.Contain(picked[i].text), "A slot is not a second copy of the card's caption.");
                Assert.That(FindButton(picked[i].text).GetComponentsInChildren<Image>().Any(image => image.name == "Chosen mark"), Is.True, "The card's ring is filled.");
            }
            Assert.That(Words(tray), Does.Contain(required + " / " + required + " selected"));
            long revision = director.Snapshot.revision;
            Assert.That(director.Snapshot.finalArgument, Is.Null, "Choosing commits nothing.");
            var lockButton = ButtonWithCaption(EpisodeDirector.LockArgumentCaption);
            Assert.That(lockButton.interactable, Is.True);
            lockButton.onClick.Invoke();
            yield return Frames(2);
            state = director.Snapshot;
            Assert.That(state.revision, Is.EqualTo(revision + 1), "The lock is the one commit.");
            Assert.That(state.finalArgument.theme, Is.EqualTo(FinalArgument.Cerebral));
            Assert.That(state.finalArgument.momentRefs, Is.EqualTo(moments.Take(required).Select(m => m.reference)));
            Assert.That(director.InFinalCase, Is.False, "Back to the panel.");
            Assert.That(ButtonWithCaption(EpisodeHud.JurySkipCaption), Is.Not.Null);

            // Read back.
            ButtonWithCaption(EpisodeDirector.FinalCaseCaption).onClick.Invoke();
            yield return Frames(1);
            Assert.That(Words(LastActive("Episode panel")), Does.Contain("YOUR ARGUMENT IS LOCKED").And.Contain(FinalArgument.Label(FinalArgument.Cerebral)));
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.LockArgumentCaption), Is.Null, "Locked once.");
            Assert.That(Words(LastActive(EpisodeHud.FinalCaseResumeName)), Does.Contain(FinalArgument.Opening(FinalArgument.Cerebral)),
                "Once locked, the quote slot is the speech's opening.");
            Assert.That(LastActive(EpisodeHud.FinalCaseTrayName), Is.Null, "The tray goes with the lock.");
            string locked = Words(LastActive(EpisodeHud.FinalCaseMomentsName));
            foreach (var moment in moments.Take(required)) Assert.That(locked, Does.Contain(moment.text), "The locked moments read back.");
            Assert.That(LastActive(EpisodeHud.FinalCaseMomentsName).GetComponentsInChildren<Button>(), Is.Empty, "Read back, not chosen again.");
            ButtonWithCaption(EpisodeDirector.LeaveFinalCaseCaption).onClick.Invoke();
            yield return Frames(1);

            // The speeches open with it.
            ButtonWithCaption(EpisodeHud.JurySkipCaption).onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.FinalSpeeches));
            var field = Object.FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None).Single(input => input.isActiveAndEnabled && input.name == "Final speech draft");
            Assert.That(field.text, Is.EqualTo(FinalArgument.Speech(director.Snapshot)), "The locked argument fills the editor.");
            field.text = "";
            director.ClosePanels();
            yield return OpenFinalePanel();
            yield return Frames(1);
            field = Object.FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None).Single(input => input.isActiveAndEnabled && input.name == "Final speech draft");
            Assert.That(field.text, Is.Empty, "A draft the player cleared stays clear.");
        }

        /// <summary>
        /// MOCKUP-PASS M15's frame, before anything is chosen: the station's band names the screen
        /// and its hint is gone; the narratives, the résumé and the moments are the columns' own; the
        /// tray has a slot for each moment to choose; each moment's card carries the face it is
        /// about; the lock's capitals are its style, not its words; and the cards and the tray fit.
        /// </summary>
        private void AssertTheFinalCaseScreen(EpisodeState state, RectTransform panel, List<FinalArgument.Moment> moments, int required)
        {
            Assert.That(Words(LastActive("Phase band")), Does.Contain("PREPARE YOUR FINAL CASE"), "The band names the screen.");
            Assert.That(panel.Find(EpisodeHud.PanelHintName).gameObject.activeSelf, Is.False, "The station's hint goes, as a house event's does.");
            var columns = LastActive(EpisodeHud.FinalCaseColumnsName);
            Assert.That(columns, Is.Not.Null);
            foreach (var part in new[] { EpisodeHud.FinalCaseThemesName, EpisodeHud.FinalCaseResumeName, EpisodeHud.FinalCaseMomentsName })
                Assert.That(LastActive(part).IsChildOf(columns), Is.True, part + " is one of the columns'.");
            Assert.That(Words(LastActive(EpisodeHud.FinalCaseResumeName)), Does.Contain("COMPETITION WINS").And.Contain("NOMINATIONS SURVIVED")
                .And.Contain("WEEKS IN THE HOUSE").And.Contain("KEY WEEKS"));
            var tray = LastActive(EpisodeHud.FinalCaseTrayName);
            Assert.That(tray, Is.Not.Null, "The tray waits for the moments.");
            for (int i = 1; i <= required; i++) Assert.That(tray.Find(EpisodeHud.FinalCaseSlotPrefix + i), Is.Not.Null, "Slot " + i);
            Assert.That(tray.GetComponentsInChildren<Button>(), Is.Empty, "Nothing on the tray is a control.");
            foreach (var moment in moments)
                Assert.That(FindButton(moment.text).GetComponentsInChildren<CharacterPortraitBinding>(true),
                    Has.Length.EqualTo(FinalArgument.SubjectOf(state, moment.reference) != null ? 1 : 0), moment.reference + "'s face, from the portrait studio.");
            var lockLabel = FindButton(EpisodeDirector.LockArgumentCaption).GetComponentsInChildren<TMP_Text>()
                .Single(text => text.text == EpisodeDirector.LockArgumentCaption);
            Assert.That(lockLabel.fontStyle & FontStyles.UpperCase, Is.EqualTo(FontStyles.UpperCase), "Capitals by style; the caption is unchanged.");
            AssertDecisionCopyFits(LastActive(EpisodeHud.FinalCaseMomentsName));
            AssertDecisionCopyFits(tray);
        }

        /// <summary>
        /// A station screen - the final case, the jury house - is a screen of its own (UI-UX-PASS-PLAN
        /// E0): on the strategy stage, with nothing pinned under it - at three the window's way on and
        /// its unused-actions note used to stand over the columns and cut them - so its scroll runs to
        /// the stage's foot and its content has the frame's room. Logs how far the column runs past
        /// its viewport, so a run says what the frame holds.
        /// </summary>
        private void AssertAStationScreen(string where)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(Hud.CurrentActivityLayout, Is.EqualTo(EpisodeHud.ActivityLayout.Strategy), where + " takes the strategy stage.");
            var panel = LastActive("Episode panel");
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.BeginNextCompetitionCaption), Is.Null, where + ": no way on is pinned under a station screen.");
            Assert.That(panel.Find(EpisodeHud.PinnedNoteName), Is.Null, where + ": and no note under it.");
            var pinned = panel.Cast<Transform>().Select(child => child.GetComponent<Button>())
                .Where(button => button != null && button.IsActive() && button.name != "Close  [Esc]").Select(button => button.name).ToArray();
            Assert.That(pinned, Is.Empty, where + " pins nothing in its footer: " + string.Join(", ", pinned));
            var content = LastActive("Episode content");
            var viewport = (RectTransform)content.parent;
            Assert.That(ScreenRect(viewport).yMin - ScreenRect(panel).yMin, Is.LessThan(40f), where + "'s scroll runs to the stage's foot.");
            LogTheFit(where);
        }

        /// <summary>How far the panel's column runs past its viewport, for the log: a scroll, or a screen that holds.</summary>
        private void LogTheFit(string where)
        {
            Canvas.ForceUpdateCanvases();
            var content = LastActive("Episode content");
            var viewport = (RectTransform)content.parent;
            float over = content.rect.height - viewport.rect.height;
            Debug.Log("[Gamesim] " + where + ": the column runs " + content.rect.height.ToString("0") + " in a viewport of "
                + viewport.rect.height.ToString("0") + (over > .5f ? ", " + over.ToString("0") + " past it." : ", and holds."));
        }

        /// <summary>
        /// "Close your final case" is the screen's last row: under the columns and the tray, after the
        /// lock at the Final 2 and the lock's line at three - never the full-width button over the
        /// form it was, which made the screen's first control the one that leaves it (UI-UX-PASS-PLAN E0).
        /// </summary>
        private void AssertTheCloseUnderTheTray(bool early)
        {
            var close = (RectTransform)FindButton(EpisodeDirector.LeaveFinalCaseCaption).transform;
            var columns = LastActive(EpisodeHud.FinalCaseColumnsName);
            Assert.That(close.parent, Is.SameAs(columns.parent), "The way back is a row of the column, as the columns are.");
            Assert.That(close.GetSiblingIndex(), Is.GreaterThan(columns.GetSiblingIndex()), "The way back comes under the columns,");
            var tray = LastActive(EpisodeHud.FinalCaseTrayName);
            if (tray != null) Assert.That(close.GetSiblingIndex(), Is.GreaterThan(tray.GetSiblingIndex()), "and under the tray.");
            if (!early)
                Assert.That(close.GetSiblingIndex(), Is.GreaterThan(LastActive(EpisodeHud.FinalCaseLockRowName).GetSiblingIndex()), "after the lock.");
            var rows = close.parent.Cast<Transform>().Where(row => row.gameObject.activeSelf).ToList();
            Assert.That(rows.Last(), Is.SameAs(close), "The way back is the screen's last row.");
        }

        /// <summary>The window at three under the finale rules: a season played there from its start, installed and reloaded.</summary>
        private IEnumerator InstallTheWindowAtThreeUnderTheFinaleRules()
        {
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 120 && fixture == null; seed++)
            {
                var initial = ContentCatalog.Create(seed);
                EpisodeEngine.EnableFinale(initial);
                var engine = new EpisodeEngine(initial);
                for (int guard = 0; guard < 150; guard++)
                {
                    var current = engine.Snapshot;
                    if (current.phase == EpisodePhase.Social && current.Active.Count() == 3 && current.Active.Any(actor => actor.isPlayer)
                        && current.pendingDiary == null) { fixture = current; break; }
                    if (current.phase == EpisodePhase.Finished || !current.Active.Any(actor => actor.isPlayer)) break;
                    var result = engine.Apply(NextCommand(current));
                    Assert.That(result.accepted, Is.True, result.reason);
                }
            }
            Assert.That(fixture, Is.Not.Null, "No bounded legal season reached the window at three with the player in it.");
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            AssertEquivalent(fixture, director.Snapshot);
        }

        /// <summary>
        /// MOCKUP-PASS M15's second part: at three a free tile beside the jury house's opens the
        /// final case to read early - the same columns and the same choices, at both text sizes -
        /// with the lock's place saying when it opens. Choosing commits nothing, and the choice goes
        /// with the screen.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_TheFinalCaseReadsEarlyAtThreeWithoutALock()
        {
            HoldTheHouseForTheFixture();
            yield return InstallTheWindowAtThreeUnderTheFinaleRules();
            yield return PutAwayTheCards();
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                yield return OpenFreeTime();
                var before = director.Snapshot;
                Assert.That(EpisodeDirector.FinalCaseEarlyAvailable(before), Is.True, "The window at three, under the finale rules.");
                Assert.That(EpisodeDirector.FinalCaseAvailable(before), Is.False, "Not yet the case that locks.");
                var moments = FinalArgument.Moments(before);
                var tile = ButtonWithCaption(EpisodeDirector.FinalCaseCaption);
                Assert.That(tile.transform.IsChildOf(LastActive(EpisodeHud.HouseMovesName)), Is.True, "A tile among the moves at three,");
                // A card says what it costs at its foot; at the larger text the moves are rows, which do not.
                if (!larger) Assert.That(Words((RectTransform)tile.transform), Does.Contain("Costs no action"), "and a free one,");
                if (EpisodeDirector.JuryHouseAvailable(before))
                    Assert.That(tile.transform.GetSiblingIndex(), Is.EqualTo(FindButton(EpisodeDirector.JuryHouseCaption).transform.GetSiblingIndex() + 1),
                        "beside the jury house's.");
                tile.onClick.Invoke();
                yield return Frames(2);
                Canvas.ForceUpdateCanvases();
                Assert.That(director.InFinalCase, Is.True);
                var panel = LastActive("Episode panel");
                Assert.That(Words(panel), Does.Contain("PREPARE YOUR FINAL CASE").And.Contain(EpisodeDirector.FinalCaseLockLaterLine));
                // A screen of its own, as at the Final 2: the final-four week's roles, still in state
                // until the window closes, do not head it.
                string status = EpisodeDirector.HouseStatus(before);
                Assert.That(status, Is.Not.Null, "The final-four week's roles are still in state at three,");
                Assert.That(Words(panel), Does.Not.Contain(status), "and the case does not open under them.");
                Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.LockArgumentCaption), Is.Null, "No lock before the Final 2.");
                foreach (var theme in FinalArgument.Themes)
                    Assert.That(ButtonWithCaption(FinalArgument.Label(theme)).transform.IsChildOf(LastActive(EpisodeHud.FinalCaseThemesName)), Is.True, theme);
                foreach (var moment in moments)
                    Assert.That(ButtonWithCaption(moment.text).transform.IsChildOf(LastActive(EpisodeHud.FinalCaseMomentsName)), Is.True, moment.reference);
                AssertDecisionCopyFits(LastActive(EpisodeHud.FinalCaseThemesName));
                AssertDecisionCopyFits(LastActive(EpisodeHud.FinalCaseResumeName));
                string where = "The final case at three" + (larger ? " at the larger text" : "");
                AssertEveryLabelDraws(where, panel);
                // A station screen at three (UI-UX-PASS-PLAN E0): the window's way on and its
                // unused-actions note, which cut the columns at the bar, are not pinned under it.
                AssertAStationScreen(where);
                AssertTheCloseUnderTheTray(early: true);
                if (Application.isBatchMode)
                    yield return CaptureFraming(larger ? "endgame-final-case-three-large" : "endgame-final-case-three", settle: false,
                        inspect: frame => LogTheFit(where + " on the 16:9 frame"));

                // Choosing is view state: nothing commits, and closing lets it go.
                ButtonWithCaption(FinalArgument.Label(FinalArgument.Social)).onClick.Invoke();
                yield return Frames(1);
                if (moments.Count > 0) { ButtonWithCaption(moments[0].text).onClick.Invoke(); yield return Frames(1); }
                Assert.That(FindButton(FinalArgument.Label(FinalArgument.Social)).GetComponentsInChildren<Image>().Any(image => image.name == "Chosen mark"), Is.True);
                AssertEquivalent(before, director.Snapshot);
                ButtonWithCaption(EpisodeDirector.LeaveFinalCaseCaption).onClick.Invoke();
                yield return Frames(2);
                Assert.That(director.InFinalCase, Is.False);
                Assert.That(Words(LastActive(EpisodeHud.ScreenHeadName)), Does.Contain(EpisodeDirector.EndgamePreparationTitle), "Back in the window.");
                Assert.That(Words(LastActive("Episode panel")), Does.Contain(status), "The window's own screen keeps its status line.");
                Assert.That(FindButton(EpisodeDirector.BeginNextCompetitionCaption).transform.parent, Is.SameAs(LastActive("Episode panel")),
                    "The window's way on is back, pinned, once the case is closed.");
                ButtonWithCaption(EpisodeDirector.FinalCaseCaption).onClick.Invoke();
                yield return Frames(2);
                Assert.That(LastActive(EpisodeHud.FinalCaseThemesName).GetComponentsInChildren<Image>().Any(image => image.name == "Chosen mark"), Is.False,
                    "The choice went with the screen.");
                AssertEquivalent(before, director.Snapshot);
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);
        }

        /// <summary>
        /// A history question: the responses it offers as tiles in the centre column, at both text
        /// sizes, with no A or B and nothing about which lands; the receipt it came from on the
        /// right; after the commit, the engine's note, still there after a reload.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_AHistoryQuestionOffersItsResponsesAndShowsTheEnginesNote()
        {
            HoldTheHouseForTheFixture();
            yield return InstallFinaleFixture(true, finaleRules: true);
            yield return PutAwayTheCards();
            var state = director.Snapshot;
            var exchange = state.juryExchanges[state.juryQuestionIndex];
            Assert.That(exchange.category, Is.Not.Null, "A question from the season's history.");
            var offered = FinaleQuestions.Offered(exchange.category, exchange.receiptKind);

            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                yield return OpenFinalePanel();
                yield return Frames(2);
                Canvas.ForceUpdateCanvases();
                var answers = LastActive(EpisodeHud.JuryAnswerColumnName);
                var grid = LastActive(EpisodeHud.JuryResponsesName);
                Assert.That(grid.IsChildOf(answers), Is.True, "The responses, centre.");
                foreach (var response in offered)
                    Assert.That(ButtonWithCaption(FinaleQuestions.Caption(response)).transform.IsChildOf(grid), Is.True, response);
                Assert.That(grid.GetComponentsInChildren<Button>().Length, Is.EqualTo(offered.Length));
                Assert.That(director.GetComponentsInChildren<Button>().Any(b => b.GetComponentsInChildren<TMP_Text>().Any(t => t.text.StartsWith("A · "))), Is.False,
                    "No catalogue answers under the rules.");
                string words = Words(LastActive("Episode panel"));
                Assert.That(words, Does.Not.Contain("took your answer").And.Not.Contain("not moved"), "Nothing says how a response will land.");
                if (exchange.receiptKind != null)
                    Assert.That(LastActive(EpisodeHud.JuryReceiptLineName)?.GetComponent<TMP_Text>().text, Is.EqualTo(FinaleQuestions.ReceiptLine(state, exchange)));
                AssertDecisionCopyFits(grid);
                // The mockup pass (MOCKUP-PASS M11): the responses under their eyebrow, in compact
                // rows shorter than the rows they replace; the receipt's kicker over the saved
                // question, which is untouched; the week's count beside the receipt line, never in it.
                Assert.That(ColumnWords(answers), Does.Contain("YOUR RESPONSE"));
                Assert.That(grid.GetComponent<GridLayoutGroup>().cellSize.y, Is.LessThan(78f * Hud.FontScale), "Compact rows.");
                Assert.That(LastActive("Jury question").GetComponent<TMP_Text>().text, Is.EqualTo(exchange.question), "The saved question, word for word.");
                string kicker = FinaleQuestions.Kicker(state, exchange);
                var over = LastActive(EpisodeHud.JuryKickerName);
                if (kicker == null) Assert.That(over, Is.Null, "No receipt, no kicker.");
                else
                {
                    Assert.That(over.GetComponent<TMP_Text>().text, Is.EqualTo(kicker));
                    Assert.That(over.IsChildOf(LastActive(EpisodeHud.JuryQuestionPanelName)), Is.True, "Over the question, in its panel.");
                }
                string tally = FinaleQuestions.ReceiptTally(state, exchange);
                var count = LastActive(EpisodeHud.JuryReceiptTallyName);
                if (tally == null) Assert.That(count, Is.Null, "No count for a receipt that is not about a vote.");
                else
                {
                    Assert.That(count.GetComponent<TMP_Text>().text, Is.EqualTo(tally));
                    Assert.That(count.parent, Is.SameAs(LastActive(EpisodeHud.JuryReceiptLineName).parent), "Beside the receipt line.");
                }
                // The week's recap headline only where it adds: under a receipt about that week's
                // vote whose line does not say who went (review correction 19).
                bool recapAdds = FinaleQuestions.RecapAdds(state, exchange)
                    && WeeklyRecap.Build(state, FinaleQuestions.ReceiptWeek(state, exchange).Value).evicted != null;
                Assert.That(LastActive(EpisodeHud.JuryRecapName) != null, Is.EqualTo(recapAdds), "The recap headline only where it adds.");
                AssertDecisionCopyFits(LastActive(EpisodeHud.JuryAskerColumnName));
                AssertDecisionCopyFits(LastActive(EpisodeHud.JuryReceiptColumnName));
                if (!larger && Application.isBatchMode) yield return CaptureFraming("endgame-history-question", settle: false);
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);

            yield return OpenFinalePanel();
            ButtonWithCaption(FinaleQuestions.Caption(FinaleQuestions.Own)).onClick.Invoke();
            yield return Frames(2);
            var after = director.Snapshot;
            Assert.That(after.juryExchanges[after.juryQuestionIndex].answerChoice, Is.EqualTo(FinaleQuestions.Own));
            string note = after.events.Last(entry => entry.kind == "jury-answer").text;
            Assert.That(LastActive(EpisodeHud.JuryReactionName)?.GetComponent<TMP_Text>().text, Is.EqualTo(note), "The engine's own note.");
            yield return ReloadEpisode();
            yield return OpenFinalePanel();
            yield return Frames(1);
            Assert.That(LastActive(EpisodeHud.JuryReactionName)?.GetComponent<TMP_Text>().text, Is.EqualTo(note), "It survives a reload.");
        }
    }
}
