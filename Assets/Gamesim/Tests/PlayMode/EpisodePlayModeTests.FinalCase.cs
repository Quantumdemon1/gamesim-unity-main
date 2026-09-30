using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
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
                AssertEveryLabelDraws("The final case" + (larger ? " at the larger text" : ""), panel);
                if (!larger && Application.isBatchMode) yield return CaptureFraming("endgame-final-case", settle: false);
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
