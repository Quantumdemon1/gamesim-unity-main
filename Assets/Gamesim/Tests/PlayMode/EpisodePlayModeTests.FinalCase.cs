using System.Collections;
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
            foreach (var moment in moments.Take(required))
            {
                Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.LockArgumentCaption).interactable, Is.False, "Not until the choice is whole.");
                ButtonWithCaption(moment.text).onClick.Invoke();
                yield return Frames(1);
            }
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
