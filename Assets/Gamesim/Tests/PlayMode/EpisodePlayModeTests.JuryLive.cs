using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private static string ColumnWords(RectTransform column) =>
            string.Join("\n", column.GetComponentsInChildren<TMP_Text>().Where(text => text.gameObject.activeInHierarchy).Select(text => text.text));

        /// <summary>
        /// ENDGAME-PLAN F5's live questioning (F5a): the juror and the question left, the answers in
        /// the centre under the captions they have always had, the season's receipt right, the hint
        /// over them, and Skip after them - side by side at the resting text, one under another at
        /// the larger. Nothing says how an answer will land until it is committed; then the engine's
        /// own note is the reaction line, and it is still there after a reload. The player as a
        /// juror gets the finalist they ask on the left and what they know of them on the right.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_JuryQuestioningIsALiveEvent()
        {
            HoldTheHouseForTheFixture();
            yield return InstallFinaleFixture(true);
            yield return PutAwayTheCards();
            var state = director.Snapshot;
            Assert.That(state.phase, Is.EqualTo(EpisodePhase.JuryQuestioning));
            var exchange = state.juryExchanges[state.juryQuestionIndex];
            Assert.That(exchange.finalistId, Is.EqualTo(state.playerId), "The player answers.");
            var juror = state.Find(exchange.questionerId);

            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                yield return OpenFinalePanel();
                yield return Frames(2);
                Canvas.ForceUpdateCanvases();
                var live = LastActive(EpisodeHud.JuryLiveName);
                Assert.That(live, Is.Not.Null, "The live layout.");
                var asker = LastActive(EpisodeHud.JuryAskerColumnName);
                var answers = LastActive(EpisodeHud.JuryAnswerColumnName);
                var receipt = LastActive(EpisodeHud.JuryReceiptColumnName);
                Assert.That(ColumnWords(asker), Does.Contain(juror.name).And.Contain(exchange.question), "The juror and the question, left.");
                Assert.That(asker.GetComponentsInChildren<TMP_Text>().Count(text => text.name == "Jury question"), Is.EqualTo(1));
                Assert.That(ButtonWithCaption("A · " + exchange.optionA).transform.IsChildOf(answers), Is.True, "The answers, centre.");
                Assert.That(ButtonWithCaption("B · " + exchange.optionB).transform.IsChildOf(answers), Is.True);
                Assert.That(ColumnWords(receipt), Does.Contain("SEASON RECEIPT"), "The receipt, right.");
                Assert.That(receipt.GetComponentsInChildren<Button>(), Is.Empty, "The receipt is read, not pressed.");
                Assert.That(asker.GetComponentsInChildren<Button>(), Is.Empty);
                Assert.That(Words(LastActive("Episode panel")), Does.Contain(EpisodeHud.JuryHintWords));
                // The mockup pass (MOCKUP-PASS M11): Skip leads the thin row of ways on, and the row
                // stays in the panel's own column, after the live layout.
                var skip = ButtonWithCaption(EpisodeHud.JurySkipCaption);
                var ways = LastActive(EpisodeHud.JuryWaysOnName);
                Assert.That(skip.transform.parent, Is.SameAs(ways), "Skip is in the ways on,");
                Assert.That(ways.parent, Is.SameAs(live.parent), "which stay in the panel's own column,");
                Assert.That(ways.GetSiblingIndex(), Is.GreaterThan(live.GetSiblingIndex()), "after the live layout.");
                // The juror in a card of their own, with the heading and the line the panel opened
                // with, word for word; the hint in the framed footer, with the disclaimer; and until
                // the answer, a line under the receipt that the jury will react to it.
                var card = LastActive(EpisodeHud.JuryAskerCardName);
                Assert.That(card.IsChildOf(asker), Is.True, "The juror's card, left,");
                Assert.That(ColumnWords(card), Does.Contain(juror.name).And.Contain(juror.name + " asks you")
                    .And.Contain("Juror · Question " + (state.juryQuestionIndex + 1) + " of "), "with its heading and its line.");
                var footer = LastActive(EpisodeHud.JuryFooterName);
                Assert.That(LastActive(EpisodeHud.JuryHintName).IsChildOf(footer), Is.True, "The hint, framed.");
                Assert.That(ColumnWords(footer), Does.Contain("A response is not a guaranteed jury vote."));
                Assert.That(footer.GetSiblingIndex(), Is.GreaterThan(live.GetSiblingIndex()).And.LessThan(ways.GetSiblingIndex()));
                Assert.That(LastActive(EpisodeHud.JuryReactNoteName).IsChildOf(receipt), Is.True, "The jury will react, under the receipt.");
                string before = Words(LastActive("Episode panel"));
                Assert.That(before, Does.Not.Contain("impressed").And.Not.Contain("unconvinced"), "Nothing says how an answer will land.");
                AssertDecisionCopyFits(asker);
                AssertDecisionCopyFits(receipt);
                // The A and B answers are whole sentences in a centre column about a third of the
                // row: each row is as tall as its caption needs, so none loses a line.
                AssertDecisionCopyFits(answers);
                var a = ScreenRect(asker); var c = ScreenRect(answers); var r = ScreenRect(receipt);
                if (!larger)
                {
                    Assert.That(c.xMin, Is.GreaterThanOrEqualTo(a.xMax - 1f), "Side by side at the resting text.");
                    Assert.That(r.xMin, Is.GreaterThanOrEqualTo(c.xMax - 1f));
                    var question = LastActive(EpisodeHud.JuryQuestionPanelName);
                    Assert.That(question.IsChildOf(asker), Is.True);
                    Assert.That(ScreenRect(question).xMin, Is.GreaterThanOrEqualTo(ScreenRect(card).xMax - 1f), "The card and the question side by side.");
                    Assert.That(ScreenRect(ways.GetChild(ways.childCount - 1) as RectTransform).yMax, Is.GreaterThan(ScreenRect(skip.transform as RectTransform).yMin),
                        "The ways on, abreast.");
                    if (Application.isBatchMode) yield return CaptureFraming("endgame-jury-live", settle: false);
                }
                else Assert.That(c.yMax, Is.LessThanOrEqualTo(a.yMin + 1f), "One under another at the larger text.");
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);

            // The answer, and the engine's note as the reaction line.
            yield return OpenFinalePanel();
            ButtonWithCaption("A · " + exchange.optionA).onClick.Invoke();
            yield return Frames(2);
            var after = director.Snapshot;
            string note = after.events.Last(entry => entry.kind == "jury-answer").text;
            var reaction = LastActive(EpisodeHud.JuryReactionName);
            Assert.That(reaction, Is.Not.Null, "The reaction line after the commit.");
            Assert.That(reaction.GetComponent<TMP_Text>().text, Is.EqualTo(note), "The engine's own note, never a number.");
            Assert.That(reaction.IsChildOf(LastActive(EpisodeHud.JuryAnswerColumnName)), Is.True);
            Assert.That(LastActive(EpisodeHud.JuryReactNoteName), Is.Null, "The reaction takes the waiting line's place.");
            yield return ReloadEpisode();
            yield return OpenFinalePanel();
            yield return Frames(1);
            Assert.That(LastActive(EpisodeHud.JuryReactionName)?.GetComponent<TMP_Text>().text, Is.EqualTo(note), "It survives a reload.");

            // The player as a juror: the finalist they ask, and what they know of them.
            HoldTheHouseForTheFixture();
            yield return InstallFinaleFixture(false);
            yield return PutAwayTheCards();
            yield return OpenFinalePanel();
            yield return Frames(1);
            state = director.Snapshot;
            exchange = state.juryExchanges[state.juryQuestionIndex];
            Assert.That(exchange.questionerId, Is.EqualTo(state.playerId), "The player asks.");
            var finalist = state.Find(exchange.finalistId);
            Assert.That(ColumnWords(LastActive(EpisodeHud.JuryAskerColumnName)), Does.Contain(finalist.name));
            Assert.That(ColumnWords(LastActive(EpisodeHud.JuryAskerCardName)), Does.Contain("Finalist · Question "), "The finalist on the spot.");
            Assert.That(ColumnWords(LastActive(EpisodeHud.JuryReceiptColumnName)),
                Does.Contain("WHAT YOU KNOW OF " + FinalistRead.FirstName(finalist.name).ToUpperInvariant()));
            Assert.That(Words(LastActive("Episode panel")), Does.Contain("You ask " + finalist.name));
            var tone = WebJuryQuestioning.GetJurorQuestionOptions(state.juryQuestionIndex).First();
            Assert.That(ButtonWithCaption(tone.tone + " · " + tone.text).transform.IsChildOf(LastActive(EpisodeHud.JuryAnswerColumnName)), Is.True);
            Canvas.ForceUpdateCanvases();
            AssertDecisionCopyFits(LastActive(EpisodeHud.JuryAnswerColumnName));
        }
    }
}
