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
    public sealed partial class EpisodePlayModeTests
    {
        private static string ColumnWords(RectTransform column) =>
            string.Join("\n", column.GetComponentsInChildren<TMP_Text>().Where(text => text.gameObject.activeInHierarchy).Select(text => text.text));

        /// <summary>
        /// A real played vote receipt, in each saved mode, follows the resolved card width rather
        /// than the column's build-time preferred width. The existing four frames include the
        /// native 21:9 failure, and each is checked at all three unmodified text sizes. This adds
        /// no answer, ballot or knowledge: the complete durable graph remains unchanged.
        /// </summary>
        [UnityTest]
        public IEnumerator JuryReceipt_ThePlayedWordUsesItsResolvedWidthAndWrappedHeight([Values(2, 1)] int mode)
        {
            HoldTheHouseForTheFixture();
            var fixture = UvpJuryFixture(mode, out uint seed);
            var exchange = fixture.juryExchanges[fixture.juryQuestionIndex];
            string expected = FinaleQuestions.ReceiptLine(fixture, exchange);
            string expectedTally = FinaleQuestions.ReceiptTally(fixture, exchange);
            yield return UvpInstall(fixture);
            yield return PutAwayTheCards();
            var before = director.Snapshot;
            var widths = new HashSet<float>();
            int checkedFrames = 0;
            foreach (var textSize in TextSizes.All)
            {
                yield return ApplyTextSize(textSize);
                yield return OpenFinalePanel();
                yield return Frames(2);
                string where = "Mode " + mode + ", seed " + seed + ", " + TextSizes.Name(textSize) + " text";
                yield return OnFrames("jury-resolved-receipt-mode-" + mode + SizeSuffix(textSize), where, at =>
                {
                    var column = LastActive(EpisodeHud.JuryReceiptColumnName);
                    var card = LastActive(EpisodeHud.JuryReceiptCardName);
                    var receipt = LastActive(EpisodeHud.JuryReceiptLineName).GetComponent<TMP_Text>();
                    var tally = card.GetComponentsInChildren<TMP_Text>().SingleOrDefault(label => label.name == EpisodeHud.JuryReceiptTallyName);
                    var photoSlot = card.Find("Receipt photo slot") as RectTransform;
                    var layout = card.GetComponent<HorizontalLayoutGroup>();
                    var hud = director.GetComponentInChildren<EpisodeHud>(true);
                    Assert.That(layout, Is.Not.Null, at + ": the actual-width layout owns the card.");
                    Assert.That(receipt.text, Is.EqualTo(expected), at + ": the original record's words.");
                    Assert.That(receipt.fontSize, Is.EqualTo(Mathf.RoundToInt(14f * hud.FontScale)), at + ": the original selected font size.");
                    Assert.That(receipt.fontStyle, Is.EqualTo(FontStyles.Bold), at);
                    Assert.That(receipt.transform.parent, Is.SameAs(card), at);
                    Assert.That(photoSlot, Is.Not.Null, at + ": the actual juror's portrait remains.");
                    Assert.That(tally != null, Is.EqualTo(expectedTally != null), at + ": the record still owns whether a tally exists.");
                    if (tally != null)
                    {
                        Assert.That(tally.text, Is.EqualTo(expectedTally), at + ": the original tally words.");
                        Assert.That(tally.transform.parent, Is.SameAs(receipt.transform.parent), at + ": the tally remains beside the receipt.");
                    }
                    float reserved = (photoSlot != null ? photoSlot.rect.width : 0f) + (tally != null ? tally.rectTransform.rect.width : 0f);
                    float available = card.rect.width - reserved;
                    Assert.That(available, Is.GreaterThan(0f), at + ": real remaining width.");
                    Assert.That(receipt.rectTransform.rect.width, Is.EqualTo(available).Within(.1f), at + ": not the nominal preferred column width.");
                    float needed = receipt.GetPreferredValues(receipt.text, available, 0f).y;
                    Assert.That(receipt.rectTransform.rect.height, Is.GreaterThanOrEqualTo(needed - .1f), at + ": wrapped height at the resolved width.");
                    Assert.That(card.rect.height, Is.GreaterThanOrEqualTo(receipt.rectTransform.rect.height - .1f), at);

                    var note = LastActive(EpisodeHud.JuryReactNoteName);
                    Assert.That(note != null && note.IsChildOf(column), Is.True, at + ": the original waiting note remains in the receipt column.");
                    var noteText = note.GetComponentsInChildren<TMP_Text>().Single();
                    var noteGlyph = note.GetComponentsInChildren<Image>().SingleOrDefault(image => image.name == "Glyph");
                    var expectedGlyph = UiTheme.Pack(PackArt.KitIconJury);
                    Assert.That(noteGlyph != null, Is.EqualTo(expectedGlyph != null), at + ": the original icon or its original absent-icon fallback.");
                    Assert.That(noteText.text, Is.EqualTo(EpisodeHud.JuryReactWords), at + ": the waiting note's unchanged words.");
                    Assert.That(noteText.fontSize, Is.EqualTo(Mathf.RoundToInt(13f * hud.FontScale)), at + ": the original selected note font size.");
                    Assert.That(noteText.fontStyle, Is.EqualTo(FontStyles.Normal), at);
                    var regular = UiTheme.Font(UiTheme.Weight.Regular);
                    if (regular != null) Assert.That(noteText.font, Is.SameAs(regular), at + ": the original regular face.");
                    Assert.That(noteText.margin, Is.EqualTo(new Vector4(0f, 0f, 0f, 4f)), at + ": the original unscaled bottom allowance.");
                    float noteInset = noteGlyph != null ? 30f * hud.FontScale : 0f;
                    float noteWidth = note.rect.width - noteInset;
                    Assert.That(note.rect.width, Is.EqualTo(column.rect.width).Within(.1f), at + ": the actual column controls the note width.");
                    Assert.That(noteWidth, Is.GreaterThan(0f), at + ": real remaining note width.");
                    Assert.That(noteText.rectTransform.rect.width, Is.EqualTo(noteWidth).Within(.1f), at + ": not the preferred build-time width.");
                    float noteNeeded = noteText.GetPreferredValues(noteText.text, noteWidth, 0f).y;
                    Assert.That(noteText.rectTransform.rect.height, Is.GreaterThanOrEqualTo(noteNeeded - .1f), at + ": note height follows actual wrapping.");
                    Assert.That(note.rect.height, Is.GreaterThanOrEqualTo(noteText.rectTransform.rect.height + 12f * hud.FontScale - .1f), at);
                    var noteBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(note, noteText.transform);
                    Assert.That(noteBounds.min.x, Is.EqualTo(note.rect.xMin + noteInset).Within(.1f), at + ": the original fractional text inset.");
                    Assert.That(noteBounds.max.x, Is.LessThanOrEqualTo(note.rect.xMax + .1f), at + ": the unchanged horizontal guard.");
                    Assert.That(noteBounds.max.y, Is.EqualTo(note.rect.yMax - 6f * hud.FontScale).Within(.1f), at + ": the original fractional text top offset.");
                    if (noteGlyph != null)
                    {
                        Assert.That(noteGlyph.sprite, Is.SameAs(expectedGlyph), at + ": the original jury icon.");
                        var glyphBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(note, noteGlyph.transform);
                        Assert.That(glyphBounds.min.x, Is.EqualTo(note.rect.xMin).Within(.1f), at);
                        Assert.That(glyphBounds.max.y, Is.EqualTo(note.rect.yMax - 8f * hud.FontScale).Within(.1f), at + ": the original fractional icon top offset.");
                        Assert.That(noteGlyph.rectTransform.rect.size, Is.EqualTo(Vector2.one * (20f * hud.FontScale)), at);
                    }
                    AssertDecisionCopyFits(column); // Keep the original .1-unit horizontal bounds guard.
                    AssertEveryLabelDraws(column, at);
                    UvEquivalent(before, director.Snapshot);
                    var settledCard = card.rect.size;
                    var settledReceipt = receipt.rectTransform.rect.size;
                    var settledNote = note.rect.size;
                    var settledNoteCopy = noteText.rectTransform.rect.size;
                    Canvas.ForceUpdateCanvases();
                    Canvas.ForceUpdateCanvases();
                    Assert.That(card.rect.size, Is.EqualTo(settledCard), at + ": repeated layout is stable, without a callback rebuild loop.");
                    Assert.That(receipt.rectTransform.rect.size, Is.EqualTo(settledReceipt), at);
                    Assert.That(note.rect.size, Is.EqualTo(settledNote), at + ": note layout remains stable without a callback rebuild loop.");
                    Assert.That(noteText.rectTransform.rect.size, Is.EqualTo(settledNoteCopy), at);
                    widths.Add(card.rect.width);
                    checkedFrames++;
                });
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(TextSize.Standard);
            Assert.That(checkedFrames, Is.EqualTo(TextSizes.All.Length * (Application.isBatchMode ? ReviewFrames.Length : 1)),
                "Every declared size/frame was actually checked; no chosen-width shortcut.");
            if (Application.isBatchMode) Assert.That(widths.Count, Is.GreaterThan(1), "The real four-frame layout exercised different resolved widths.");
            UvEquivalent(before, director.Snapshot);
        }

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

            foreach (var (textSize, larger) in TextScales.Steps)
            {
                yield return ApplyTextSize(textSize);
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
                else Assert.That(c.yMax, Is.LessThanOrEqualTo(a.yMin + 1f), "One under another " + TextScales.At(textSize) + ".");
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
