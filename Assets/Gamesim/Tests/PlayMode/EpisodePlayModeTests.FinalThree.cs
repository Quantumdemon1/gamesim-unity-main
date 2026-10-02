using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The last live copy of a HUD part by name: a render's rebuilt part, not the copy waiting to be destroyed.</summary>
        private RectTransform LastActive(string name) => director.GetComponentsInChildren<RectTransform>(true)
            .LastOrDefault(rect => rect.name == name && rect.gameObject.activeInHierarchy);

        /// <summary>The fact labels every finalist card carries, as the card prints them.</summary>
        private static readonly string[] FactLabels =
            { "COMPETITION RECORD", "YOUR RELATIONSHIP", "FINAL 2 AGREEMENT", "KNOWN ALLIANCES", "KNOWN JURY SUPPORT", "JURY BITTERNESS", "UNCERTAIN JURORS" };

        /// <summary>
        /// Whether a control's caption makes the final choice: "Take {first} · evict {first}"
        /// (UI-UX-PASS-PLAN Q0), or the paired choice's "Evict {name}" for a shape the engine never makes.
        /// </summary>
        private static bool IsFinalChoiceControl(string caption) =>
            caption != null && ((caption.StartsWith(FinalChoiceWords.TakeWord, System.StringComparison.Ordinal) && caption.Contains(FinalChoiceWords.EvictWord))
                || caption.StartsWith("Evict ", System.StringComparison.Ordinal));

        /// <summary>The colour a juror's chip wears for a lean, as the plan says it: close green, a grudge red, no read grey.</summary>
        private static Color ExpectedLeanColour(string lean) =>
            lean == FinalistRead.Support ? UiTheme.Allied : lean == FinalistRead.Bitter ? UiTheme.Danger : UiTheme.Muted;

        /// <summary>Whether two colours are one hue, their alpha aside.</summary>
        private static bool SameHue(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < .005f && Mathf.Abs(a.g - b.g) < .005f && Mathf.Abs(a.b - b.b) < .005f;

        /// <summary>
        /// The final Head of Household's page as the HUD has laid it for its frame (UI-UX-PASS-PLAN Q0):
        /// on the strategy stage, holding without a scroll, the band naming the screen and the line under
        /// it; the two columns side by side as equals with the VS between them; in each, the card with the
        /// finalist's name and photo, where the player stands with them, the three public tiles at the
        /// record's own numbers, a chip per juror in the colour of the lean the player can tell - read here
        /// from <see cref="FinalistRead.Lean"/> itself, never from the page - filled only for a lean the
        /// player saw or was part of, standing close, then a grudge, then no read, each group under its
        /// word, and every bullet of what taking them means; under the card the warning, what the choice
        /// does and the one control, "Take {first} · evict {first}", whole and inside its column, the two
        /// controls level; the either-way line where the player's word to a juror breaks whoever is
        /// taken, and a breach line over a control only where that choice alone breaks it; every label
        /// drawn, in a box at least 1.3 times its type. Measured in the canvas's own units, so it holds
        /// on the batch canvas, inside AtFrame and in a capture's frame. <paramref name="consequenceMayGo"/>
        /// lets the page's smallest tier drop the line of what the choice does (the worst case).
        /// </summary>
        private void AssertTheFinalChoicePage(EpisodeState state, string where, bool consequenceMayGo = false)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(Hud.CurrentActivityLayout, Is.EqualTo(EpisodeHud.ActivityLayout.Strategy), where + " takes the strategy stage.");
            var page = LastActive(EpisodeHud.FinalChoicePageName);
            Assert.That(page, Is.Not.Null, where + " is drawn as the page.");
            var content = LastActive("Episode content");
            var viewport = (RectTransform)content.parent;
            Assert.That(content.rect.height, Is.LessThanOrEqualTo(viewport.rect.height + .5f),
                where + " holds without a scroll: " + content.rect.height.ToString("0") + " in " + viewport.rect.height.ToString("0") + ".");
            Rect view = CanvasRect(viewport), whole = CanvasRect(page);
            Assert.That(whole.yMin >= view.yMin - .5f && whole.yMax <= view.yMax + .5f, Is.True, where + ": the page is inside the stage's view.");
            Assert.That(Words(LastActive("Phase band")), Does.Contain("CHOOSE YOUR FINAL TWO"), where + ": the band names the screen.");
            var head = LastActive(EpisodeHud.ScreenHeadName);
            Assert.That(head != null && head.IsChildOf(page), Is.True, where + " has its line under the band.");
            Assert.That(Words(head), Does.Contain(EpisodeDirector.FinalTwoHeadLine), where + ": nothing on it is a prediction.");
            string status = EpisodeDirector.HouseStatus(state);
            if (status != null) Assert.That(Words(LastActive("Episode panel")), Does.Not.Contain(status), where + ": the house's status line stands down.");
            Assert.That(Words(page), Does.Contain(FinalChoiceWords.Legend), where + ": what a chip's fill says.");
            // What breaks whichever finalist is taken is said once, over both cards.
            string eitherWay = FinalChoiceWords.EitherWay(FinalistRead.BrokenEitherWay(state));
            var either = page.GetComponentsInChildren<TMP_Text>().SingleOrDefault(label => label.name == EpisodeHud.EitherWayName);
            if (eitherWay == null) Assert.That(either, Is.Null, where + ": nothing breaks either way, and nothing says so.");
            else
            {
                Assert.That(either, Is.Not.Null, where + ": the either-way line.");
                Assert.That(either.text, Is.EqualTo(eitherWay), where + ": the either-way line's words.");
                Assert.That(CanvasRect(either.rectTransform).yMin, Is.GreaterThanOrEqualTo(CanvasRect(LastActive(EpisodeHud.FinalistColumnsName)).yMax - 1f),
                    where + ": over both cards.");
            }

            var others = FinalistRead.Others(state);
            Assert.That(others, Has.Count.EqualTo(2), where + ": two finalists to choose between.");
            var jurors = FinalistRead.Jurors(state);
            var chipNames = FinalChoiceWords.ChipNames(jurors.Select(juror => juror.name).ToList());
            var columns = new List<Rect>();
            var controls = new List<Rect>();
            var warnings = new List<Rect>();
            foreach (var take in others)
            {
                var cut = others.Single(other => other.id != take.id);
                string who = where + ", " + take.name + "'s column";
                var column = LastActive("Finalist column · " + take.name);
                var card = LastActive(EpisodeHud.FinalistCardPrefix + take.name);
                Assert.That(column != null && card != null && card.IsChildOf(column) && column.IsChildOf(page), Is.True, who + " has its card on the page.");
                Rect columnRect = CanvasRect(column), cardRect = CanvasRect(card);
                AssertWithin(whole, columnRect, who, "the page");
                AssertWithin(columnRect, cardRect, who + "'s card", "its column");
                columns.Add(columnRect);
                string words = Words(card);
                Assert.That(words, Does.Contain(take.name), who + ": the card names them.");
                Assert.That(words, Does.Not.Contain("FINAL HEAD OF HOUSEHOLD"), who + ": the crown is the player's tonight, not a finalist's.");
                Assert.That(card.GetComponentsInChildren<CharacterPortraitBinding>(true), Has.Length.EqualTo(1), who + ": a photo bound to them.");
                Assert.That(card.GetComponentsInChildren<TMP_Text>().Single(label => label.name == EpisodeHud.RelationshipWordName).text,
                    Is.EqualTo(FinalistRead.StandingWord(state, take.id)), who + ": where the player stands, in the web's word.");
                Assert.That(card.GetComponentsInChildren<Image>(true).Count(image => image.name == EpisodeHud.ChosenEdgeName), Is.EqualTo(1),
                    who + ": one gold edge, lit by the control.");

                // The three public numbers, at the record's own counts.
                var read = FinalistRead.Read(state, take.id);
                var stats = card.GetComponentsInChildren<RectTransform>().Last(rect => rect.name == EpisodeHud.FinalistStatsName);
                Assert.That(stats.GetComponentsInChildren<TMP_Text>().Where(label => label.name == "Stat label").Select(label => label.text),
                    Is.EqualTo(new[] { "Comp wins", "Votes survived", "Times on block" }), who + ": the three public tiles.");
                Assert.That(stats.GetComponentsInChildren<TMP_Text>().Where(label => label.name == "Stat value").Select(label => label.text),
                    Is.EqualTo(new[] { read.wins.ToString(), read.votesSurvived.ToString(), take.timesNominated.ToString() }), who + ": at the record's numbers.");
                AssertWithin(cardRect, CanvasRect(stats), who + "'s tiles", "the card");

                // A chip a juror, in the colour of the lean the player can tell, filled only where they saw it.
                Assert.That(words, Does.Contain(FinalChoiceWords.JuryEyebrow(jurors.Count)), who + ": the jury read, with the jury's size.");
                var chips = card.GetComponentsInChildren<RectTransform>().Where(rect => rect.name.StartsWith(EpisodeHud.JurorChipPrefix, System.StringComparison.Ordinal)).ToList();
                Assert.That(chips.Select(chip => chip.name.Substring(EpisodeHud.JurorChipPrefix.Length)), Is.EquivalentTo(jurors.Select(juror => juror.id)),
                    who + ": a chip a juror.");
                int lastRank = -1;
                foreach (var chip in chips.OrderByDescending(chip => Mathf.Round(CanvasRect(chip).yMax)).ThenBy(chip => CanvasRect(chip).xMin))
                {
                    string id = chip.name.Substring(EpisodeHud.JurorChipPrefix.Length);
                    var juror = state.Find(id);
                    var lean = FinalistRead.Lean(state, id, take.id);
                    var ground = chip.GetComponent<Image>();
                    Assert.That(SameHue(ground.color, ExpectedLeanColour(lean.lean)), Is.True,
                        who + ": " + juror.name + "'s chip is " + ground.color + " for a lean of '" + lean.lean + "' (" + lean.reason + ").");
                    bool filled = lean.lean != FinalistRead.Uncertain && lean.certainty == FinalistRead.Confirmed;
                    Assert.That(filled ? ground.color.a > .9f : ground.color.a < .3f, Is.True,
                        who + ": " + juror.name + "'s chip should be " + (filled ? "filled, seen" : "outlined, " + lean.certainty) + "; its ground is " + ground.color + ".");
                    Assert.That(chip.GetComponentsInChildren<TMP_Text>().Single(label => label.name == EpisodeHud.JurorChipWordName).text,
                        Is.EqualTo(chipNames[jurors.FindIndex(other => other.id == id)]), who + ": the chip carries the name the house calls them.");
                    int rank = FinalChoiceWords.LeanRank(lean.lean);
                    Assert.That(rank, Is.GreaterThanOrEqualTo(lastRank), who + ": the chips stand close, then a grudge, then no read.");
                    lastRank = rank;
                    AssertWithin(cardRect, CanvasRect(chip), who + ": " + juror.name + "'s chip", "the card");
                }
                // The word over each group says the lean as the colour does, so the colour is never the only telling.
                foreach (var lean in jurors.Select(juror => FinalistRead.Lean(state, juror.id, take.id).lean).Distinct())
                {
                    string word = FinalChoiceWords.GroupWord(lean);
                    var label = card.GetComponentsInChildren<TMP_Text>().SingleOrDefault(text => text.name == EpisodeHud.ChipGroupPrefix + word);
                    Assert.That(label, Is.Not.Null, who + ": the " + word + " chips stand under their word.");
                    Assert.That(SameHue(label.color, ExpectedLeanColour(lean)), Is.True, who + ": the word is in its chips' colour.");
                }

                // What taking them means: every bullet of the read's own.
                Assert.That(words, Does.Contain(FinalChoiceWords.BulletsEyebrow(take.name, cut.name)), who + ": what taking them means, headed.");
                Assert.That(card.GetComponentsInChildren<TMP_Text>().Where(label => label.name == EpisodeHud.FinalistBulletName).Select(label => label.text),
                    Is.EqualTo(FinalistRead.IfYouTake(state, take.id, cut.id).Select(line => "• " + line)), who + ": every bullet, in order.");

                // Under the card: any breach, the warning, what the choice does, and the one control, its caption both halves of the choice.
                var warning = column.GetComponentsInChildren<TMP_Text>().Single(label => label.name == EpisodeHud.FinalistWarningName);
                Assert.That(warning.text, Is.EqualTo(EpisodeDirector.FinalChoiceWarning), who + ": the warning.");
                var consequence = column.GetComponentsInChildren<TMP_Text>().SingleOrDefault(label => label.name == EpisodeHud.FinalistConsequenceName);
                if (consequence == null) Assert.That(consequenceMayGo, Is.True, who + ": what the choice does is said over the control.");
                else Assert.That(consequence.text, Is.EqualTo(EpisodeDirector.FinalChoiceConsequence(take, cut)), who + ": what the choice does.");
                string caption = FinalChoiceWords.Caption(take.name, cut.name);
                Assert.That(caption, Is.EqualTo(FinalChoiceWords.CaptionToEvict(state, cut.id)), who + ": the control that takes them evicts the other.");
                var buttons = column.GetComponentsInChildren<Button>().Where(button => button.IsActive() && button.IsInteractable()).ToList();
                Assert.That(buttons.Select(button => button.name), Is.EqualTo(new[] { caption }), who + ": its one control, captioned with the choice's two halves.");
                var control = (RectTransform)buttons[0].transform;
                var captionLabel = control.GetComponentsInChildren<TMP_Text>().First(text => text.text == caption);
                Assert.That(ShowsAllOf(captionLabel), Is.True, who + ": the caption '" + caption + "' is drawn to its end.");
                Rect controlRect = CanvasRect(control), warningRect = CanvasRect(warning.rectTransform);
                AssertWithin(columnRect, controlRect, who + "'s control", "its column");
                Assert.That(cardRect.yMin, Is.GreaterThanOrEqualTo(warningRect.yMax - 1f), who + ": the card, then the warning,");
                float overControl = warningRect.yMin;
                if (consequence != null)
                {
                    var consequenceRect = CanvasRect(consequence.rectTransform);
                    Assert.That(warningRect.yMin, Is.GreaterThanOrEqualTo(consequenceRect.yMax - 1f), who + ": then what the choice does,");
                    overControl = consequenceRect.yMin;
                }
                Assert.That(overControl, Is.GreaterThanOrEqualTo(controlRect.yMax - 1f), who + ": then the control.");
                // A breach line only where this choice alone breaks the player's word, between the card and the warning.
                var breach = column.GetComponentsInChildren<TMP_Text>().SingleOrDefault(label => label.name == EpisodeHud.BreachWarningName);
                if (breach != null)
                {
                    Assert.That(breach.text, Does.Contain(FinalistRead.FirstName(cut.name)).Or.Contain(cut.name), who + ": the breach names whom this choice lets down.");
                    Rect breachRect = CanvasRect(breach.rectTransform);
                    Assert.That(cardRect.yMin, Is.GreaterThanOrEqualTo(breachRect.yMax - 1f), who + ": the breach under the card,");
                    Assert.That(breachRect.yMin, Is.GreaterThanOrEqualTo(warningRect.yMax - 1f), who + ": over the warning.");
                }
                controls.Add(controlRect);
                warnings.Add(warningRect);
            }

            // Two people and one choice between them: side by side as equals, the VS in the gap.
            Assert.That(columns[1].xMin, Is.GreaterThanOrEqualTo(columns[0].xMax - .5f), where + ": the two side by side.");
            Assert.That(Mathf.Abs(columns[0].width - columns[1].width), Is.LessThan(1f), where + ": as equals.");
            Assert.That(Mathf.Abs(controls[0].yMin - controls[1].yMin), Is.LessThan(.5f), where + ": the two controls level.");
            Assert.That(Mathf.Abs(warnings[0].yMin - warnings[1].yMin), Is.LessThan(.5f), where + ": the two warnings level.");
            var versus = LastActive(EpisodeHud.VersusName);
            Assert.That(versus, Is.Not.Null, where + ": the two are set against each other.");
            float middle = CanvasRect(versus).center.x;
            Assert.That(middle >= columns[0].xMax - 1f && middle <= columns[1].xMin + 1f, Is.True,
                where + ": the VS is in the gap: " + middle + " between " + columns[0].xMax + " and " + columns[1].xMin + ".");

            // Every label draws, in a box Inter draws in.
            AssertEveryLabelDraws(page, where);
            foreach (var label in page.GetComponentsInChildren<TMP_Text>())
            {
                float size = label.enableAutoSizing ? label.fontSizeMax : label.fontSize;
                Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(size * 1.3f - .5f),
                    where + ": '" + label.text + "' (" + label.name + ") has a box Inter draws in: " + label.rectTransform.rect.height.ToString("0.#") + " for " + size + ".");
            }
        }

        /// <summary>
        /// UI-UX-PASS-PLAN Q0 (ENDGAME-PLAN F2, MOCKUP-PASS M8): the final Head of Household's choice is
        /// one page, a card per finalist - the portrait card, the three public tiles, the jurors' chips
        /// coloured by the lean the player can tell, the bullets of what taking them means - and under
        /// each card the warning, what the choice does, and its one control, "Take {first} · evict
        /// {first}". It holds without a scroll at both text sizes, on the batch canvas, on the 16:9
        /// frame and on the 4:3 (<see cref="AssertTheFinalChoicePage"/>). The selection lights its
        /// column; reading it commits nothing; one press commits that choice and nothing else.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_TheFinalHeadOfHouseholdChoosesBetweenTwoCards()
        {
            HoldTheHouseForTheFixture();
            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.FinalEviction && state.hohId == state.playerId,
                "the player as final Head of Household");
            yield return PutAwayTheCards();
            var state = director.Snapshot;
            Assert.That(state.hohId, Is.EqualTo(state.playerId), "The player decides.");
            var others = FinalistRead.Others(state);
            Assert.That(others, Has.Count.EqualTo(2), "Two finalists to choose between.");
            // The week chip's longest endgame line, "Week n · Final eviction", fits its slot.
            var chip = LastActive("Week chip");
            Assert.That(Words(chip), Does.Contain("FINAL 3"));
            foreach (var text in chip.GetComponentsInChildren<TMP_Text>())
                Assert.That(ShowsAllOf(text), Is.True, "The week chip loses the end of \"" + text.text + "\".");

            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                yield return OpenFinalePanel();
                yield return Frames(3);
                string where = "The final choice" + (larger ? " at the larger text" : "");
                AssertTheFinalChoicePage(state, where + " on the batch canvas");
                yield return AtBothFrames(frame => AssertTheFinalChoicePage(state, where + " on the " + frame + " frame"));
                if (Application.isBatchMode)
                    yield return CaptureFraming(larger ? "endgame-final-choice-large" : "endgame-final-choice", settle: false,
                        inspect: frame => AssertTheFinalChoicePage(state, where + " in the capture's 16:9 frame"));

                if (!larger)
                {
                    // The selection lights its own column: the card's gold edge and the ring's fill
                    // follow the control the player puts the keyboard on, and move with it. Neither is
                    // a control. Found again after each frame: a render in between rebuilds the panel
                    // and restores the selection by its caption.
                    RectTransform ControlFor(ContestantState taken) => (RectTransform)FindButton(
                        FinalChoiceWords.Caption(taken.name, others.Single(finalist => finalist.id != taken.id).name)).transform;
                    void AssertLit(ContestantState lit, string when)
                    {
                        foreach (var shown in others)
                        {
                            bool on = lit != null && shown.id == lit.id;
                            var edge = LastActive(EpisodeHud.FinalistCardPrefix + shown.name).Find(EpisodeHud.ChosenEdgeName);
                            Assert.That(edge.gameObject.activeSelf, Is.EqualTo(on), shown.name + "'s card is " + (on ? "" : "not ") + "lit " + when + ".");
                            Assert.That(ControlFor(shown).Find("Selection ring/Selection fill").gameObject.activeSelf, Is.EqualTo(on),
                                shown.name + "'s ring is " + (on ? "filled " : "empty ") + when + ".");
                        }
                    }
                    // The screen opens with the keyboard on Close, which commits nothing, and lights no
                    // card: the HUD never puts the keyboard on an irreversible choice of its own accord,
                    // and a card lit before the player has done anything would read as the game's pick.
                    var opened = EventSystem.current.currentSelectedGameObject;
                    Assert.That(opened, Is.Not.Null, "The screen opens on a control.");
                    Assert.That(IsFinalChoiceControl(opened.name), Is.False, "The screen opens on a control that commits nothing, not on '" + opened.name + "'.");
                    AssertLit(null, "as the screen opens");
                    // With the keyboard on no choice, the pointer's control lights its column - what a
                    // click there would do - and gives the light back as it leaves.
                    var pointer = new PointerEventData(EventSystem.current);
                    var previewed = ControlFor(others[0]).gameObject;
                    ExecuteEvents.Execute(previewed, pointer, ExecuteEvents.pointerEnterHandler);
                    AssertLit(others[0], "under the pointer, with the keyboard on no choice");
                    ExecuteEvents.Execute(previewed, pointer, ExecuteEvents.pointerExitHandler);
                    AssertLit(null, "once the pointer leaves");
                    foreach (var take in others)
                    {
                        // Through nothing, so that selecting even a control already selected is a move.
                        EventSystem.current.SetSelectedGameObject(null);
                        EventSystem.current.SetSelectedGameObject(ControlFor(take).gameObject);
                        yield return null;
                        AssertLit(take, "with " + take.name + "'s control selected");
                    }
                    // The keyboard's column keeps the light while the pointer passes over the other:
                    // Enter presses the selected control, so the lit card is always the one Enter takes.
                    // One card lit at a time, never both, and the pointer moves no selection.
                    var keyboardOn = others[1];
                    var pointed = ControlFor(others[0]).gameObject;
                    ExecuteEvents.Execute(pointed, pointer, ExecuteEvents.pointerEnterHandler);
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(ControlFor(keyboardOn).gameObject), "The keyboard stays where it was.");
                    AssertLit(keyboardOn, "with the pointer over " + others[0].name + "'s control and the keyboard on " + keyboardOn.name + "'s");
                    ExecuteEvents.Execute(pointed, pointer, ExecuteEvents.pointerExitHandler);
                    AssertLit(keyboardOn, "once the pointer leaves");
                }

                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);
            Assert.That(director.Snapshot.revision, Is.EqualTo(state.revision), "Reading the cards commits nothing.");
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.FinalEviction));

            // One press on a column's control commits that column's choice and nothing else: the
            // other finalist joins the jury, and the column's finalist sits beside the player.
            yield return OpenFinalePanel();
            yield return Frames(2);
            var kept = others[0];
            var gone = others[1];
            var press = ButtonWithCaption(FinalChoiceWords.Caption(kept.name, gone.name));
            Assert.That(press.transform.IsChildOf(LastActive("Finalist column · " + kept.name)), Is.True, "The control stands in the column of the finalist it takes.");
            press.onClick.Invoke();
            yield return Frames(2);
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(state.revision + 1), "One press, one command.");
            Assert.That(after.Find(gone.id).status, Is.EqualTo(ContestantStatus.Jury), gone.name + " joins the jury.");
            Assert.That(after.Find(kept.id).status, Is.EqualTo(ContestantStatus.Active), kept.name + " sits in the Final 2,");
            Assert.That(after.Find(after.playerId).status, Is.EqualTo(ContestantStatus.Active), "beside the player.");
            Assert.That(after.Active.Select(actor => actor.id), Is.EquivalentTo(new[] { after.playerId, kept.id }), "Nobody else moved.");
            Assert.That(after.ledger.power.Any(row => row.week == state.week && row.evicteeId == gone.id && row.hohId == state.playerId), Is.True,
                "The record says who the player sent to the jury.");
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.JuryQuestioning).Or.EqualTo(EpisodePhase.FinalSpeeches), "The finale opens.");
            yield return PutAwayTheCards();
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// UI-UX-PASS-PLAN Q0's review: an Enter the player did not aim commits nothing, and Enter only
        /// ever presses the lit control. The page opens with the keyboard on Close, never on a choice,
        /// so Enter as it opens commits nothing; Tab takes the keyboard to a choice, whose card lights,
        /// and Enter then commits that one and nothing else. Real key presses through the input system,
        /// as the keyboard walk makes them.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_EnterAtTheFinalChoiceOnlyEverPressesTheLitControl()
        {
            HoldTheHouseForTheFixture();
            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.FinalEviction && state.hohId == state.playerId,
                "the player as final Head of Household");
            yield return PutAwayTheCards();
            var state = director.Snapshot;
            var others = FinalistRead.Others(state);
            Assert.That(others, Has.Count.EqualTo(2), "Two finalists to choose between.");
            bool Lit(ContestantState finalist) => LastActive(EpisodeHud.FinalistCardPrefix + finalist.name).Find(EpisodeHud.ChosenEdgeName).gameObject.activeSelf;

            yield return OpenFinalePanel();
            yield return Frames(3);
            var opened = EventSystem.current.currentSelectedGameObject;
            Assert.That(opened, Is.Not.Null, "The page opens with the keyboard on a control.");
            Assert.That(IsFinalChoiceControl(opened.name), Is.False, "The page opens on a control that commits nothing, not on '" + opened.name + "'.");
            Assert.That(others.Any(Lit), Is.False, "No card is lit as the page opens.");
            yield return PressKey(Key.Enter);
            yield return Frames(2);
            Assert.That(director.Snapshot.revision, Is.EqualTo(state.revision), "Enter as the page opens commits nothing.");
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.FinalEviction), "The choice is still the player's to make.");

            // Tab to a choice: its card lights, and Enter commits that one.
            if (!director.IsPanelOpen) { yield return OpenFinalePanel(); yield return Frames(3); }
            GameObject selected = null;
            for (int step = 0; step < 6; step++)
            {
                yield return PressKey(Key.Tab);
                selected = EventSystem.current.currentSelectedGameObject;
                if (selected != null && IsFinalChoiceControl(selected.name)) break;
            }
            Assert.That(selected != null && IsFinalChoiceControl(selected.name), Is.True, "Tab reaches a choice.");
            string chosen = selected.name;
            var kept = others.Single(finalist => chosen == FinalChoiceWords.Caption(finalist.name, others.Single(other => other.id != finalist.id).name));
            var gone = others.Single(finalist => finalist.id != kept.id);
            Assert.That(Lit(kept), Is.True, "The card of the control the keyboard is on is lit,");
            Assert.That(Lit(gone), Is.False, "and only that one.");
            yield return PressKey(Key.Enter);
            yield return Frames(2);
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(state.revision + 1), "Enter on the lit choice commits it, once.");
            Assert.That(after.Find(gone.id).status, Is.EqualTo(ContestantStatus.Jury), "Enter took the lit card's choice: " + gone.name + " joins the jury,");
            Assert.That(after.Find(kept.id).status, Is.EqualTo(ContestantStatus.Active), "and " + kept.name + " sits in the Final 2.");
            yield return PutAwayTheCards();
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// A house of sixteen at its final eviction with the player the final Head of Household:
        /// thirteen on the jury and, toward the first finalist, a read of every kind the chips draw -
        /// two jurors the finalist nominated on the public record (a grudge the house saw: filled red),
        /// a juror whose warm standing the player was told (close, heard: outlined green), a juror in an
        /// alliance with the player and the finalist (close, part of it: filled green) - and nothing on
        /// the other nine (no read: outlined grey). Nothing of the jury model's is touched.
        /// </summary>
        private static EpisodeState FinalChoiceHouse()
        {
            var state = FullHouse(4402u, 16);
            var npcs = state.contestants.Where(actor => !actor.isPlayer).ToList();
            var first = npcs[0];
            foreach (var juror in npcs.Skip(2)) juror.status = ContestantStatus.Jury;
            state.week = 13;
            state.phase = EpisodePhase.FinalEviction;
            state.hohId = state.playerId;
            state.finalPart1WinnerId = state.playerId;
            state.finalPart2WinnerId = first.id;
            var jurors = npcs.Skip(2).ToList();
            state.ledger.power.Add(new PowerRow { week = 3, hohId = first.id, vetoHolderId = jurors[5].id,
                nominees = new List<string> { jurors[0].id, jurors[1].id } });
            state.ledger.standings.Add(new StandingRow { week = 4, fromId = jurors[2].id, toId = first.id, source = ClaimSource.Told, score = 40 });
            state.alliances.Add(new AllianceState { id = "final-choice-pact", name = "The Last Word", active = true,
                members = new List<string> { state.playerId, first.id, jurors[3].id } });
            return state;
        }

        /// <summary>
        /// The page with a jury of thirteen (UI-UX-PASS-PLAN Q0: houses of up to sixteen), at both text
        /// sizes on the batch canvas, the 16:9 frame and the 4:3: every assertion of
        /// <see cref="AssertTheFinalChoicePage"/>, the chips in rows inside their card, and on the first
        /// finalist's card a chip of every kind - filled red, outlined green, filled green, outlined grey -
        /// each as the player's read says; in a batch run photographed on the 16:9 frame at the larger
        /// text, the tightest the page is laid.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_TheFinalChoiceHoldsAJuryOfThirteenEveryChipThePlayersRead()
        {
            yield return InstallBuiltFinale(FinalChoiceHouse());
            yield return PutAwayTheCards();
            var state = director.Snapshot;
            Assert.That(FinalistRead.Jurors(state), Has.Count.EqualTo(13), "A jury of thirteen.");
            var first = FinalistRead.Others(state)[0];
            var leans = FinalistRead.Read(state, first.id).jurors;
            Assert.That(leans.Count(lean => lean.lean == FinalistRead.Bitter && lean.certainty == FinalistRead.Confirmed), Is.EqualTo(2), "Two grudges the house saw,");
            Assert.That(leans.Count(lean => lean.lean == FinalistRead.Support && lean.certainty == FinalistRead.Suspected), Is.EqualTo(1), "one closeness the player heard of,");
            Assert.That(leans.Count(lean => lean.lean == FinalistRead.Support && lean.certainty == FinalistRead.Confirmed), Is.EqualTo(1), "one they were part of,");
            Assert.That(leans.Count(lean => lean.lean == FinalistRead.Uncertain), Is.EqualTo(9), "and no read on the rest.");

            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                yield return OpenFinalePanel();
                yield return Frames(3);
                string where = "A jury of thirteen" + (larger ? " at the larger text" : "");
                AssertTheFinalChoicePage(state, where + " on the batch canvas");
                yield return AtBothFrames(frame => AssertTheFinalChoicePage(state, where + " on the " + frame + " frame"));
                // Every kind of chip on the first finalist's card, as the read says it.
                var card = LastActive(EpisodeHud.FinalistCardPrefix + first.name);
                var grounds = card.GetComponentsInChildren<RectTransform>()
                    .Where(rect => rect.name.StartsWith(EpisodeHud.JurorChipPrefix, System.StringComparison.Ordinal))
                    .Select(rect => rect.GetComponent<Image>().color).ToList();
                Assert.That(grounds.Count(colour => SameHue(colour, UiTheme.Danger) && colour.a > .9f), Is.EqualTo(2), where + ": two filled red,");
                Assert.That(grounds.Count(colour => SameHue(colour, UiTheme.Allied) && colour.a < .3f), Is.EqualTo(1), where + ": one outlined green,");
                Assert.That(grounds.Count(colour => SameHue(colour, UiTheme.Allied) && colour.a > .9f), Is.EqualTo(1), where + ": one filled green,");
                Assert.That(grounds.Count(colour => SameHue(colour, UiTheme.Muted) && colour.a < .3f), Is.EqualTo(9), where + ": nine outlined grey.");
                if (Application.isBatchMode && larger)
                    yield return CaptureFraming("endgame-final-choice-13", settle: false,
                        inspect: frame => AssertTheFinalChoicePage(state, where + " in the capture's 16:9 frame"));
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);
            Assert.That(director.Snapshot.revision, Is.EqualTo(state.revision), "Reading the page commits nothing.");
        }

        /// <summary>
        /// The worst the page is asked to hold (UI-UX-PASS-PLAN Q0's review): the house of sixteen with a
        /// Final 2 deal between the player and each finalist and one with a juror - five and six bullets
        /// in the columns, a breach over each control, and the either-way line over both cards.
        /// </summary>
        private static EpisodeState FinalChoiceHouseWithEveryDeal()
        {
            var state = FinalChoiceHouse();
            var finalists = state.contestants.Where(actor => !actor.isPlayer && actor.status == ContestantStatus.Active).ToList();
            var juror = state.contestants.First(actor => actor.status == ContestantStatus.Jury);
            state.deals.Add(WordDeal(state, "deal-final-first", DealKind.FinalTwo, finalists[0].id, state.playerId, DealStatus.Active, 0));
            state.deals.Add(WordDeal(state, "deal-final-second", DealKind.FinalTwo, state.playerId, finalists[1].id, DealStatus.Active, 0));
            state.deals.Add(WordDeal(state, "deal-final-juror", DealKind.FinalTwo, juror.id, state.playerId, DealStatus.Active, 0));
            return state;
        }

        /// <summary>
        /// The worst case at both text sizes on the batch canvas, the 16:9 frame and the 4:3: every
        /// assertion of <see cref="AssertTheFinalChoicePage"/> - no scroll among them - with the line of
        /// what the choice does free to go at the page's smallest tier; a breach over each control,
        /// since taking either finalist breaks the deal with the other; and the either-way line for the
        /// juror's deal, said once over both. In a batch run photographed at the larger text.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_TheFinalChoiceHoldsEveryDealBothBreachesAndAJuryOfThirteen()
        {
            yield return InstallBuiltFinale(FinalChoiceHouseWithEveryDeal());
            yield return PutAwayTheCards();
            var state = director.Snapshot;
            var others = FinalistRead.Others(state);
            Assert.That(FinalistRead.Jurors(state), Has.Count.EqualTo(13), "A jury of thirteen.");
            Assert.That(FinalistRead.BrokenEitherWay(state), Has.Count.EqualTo(1), "One agreement with a juror breaks either way.");
            foreach (var take in others)
                Assert.That(FinalistRead.IfYouTake(state, take.id, others.Single(other => other.id != take.id).id), Has.Count.GreaterThanOrEqualTo(5),
                    take.name + "'s column carries the deals' bullets beside the jury's.");

            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                yield return OpenFinalePanel();
                yield return Frames(3);
                string where = "Every deal and a jury of thirteen" + (larger ? " at the larger text" : "");
                AssertTheFinalChoicePage(state, where + " on the batch canvas", consequenceMayGo: true);
                yield return AtBothFrames(frame => AssertTheFinalChoicePage(state, where + " on the " + frame + " frame", consequenceMayGo: true));
                foreach (var take in others)
                    Assert.That(LastActive("Finalist column · " + take.name).GetComponentsInChildren<TMP_Text>().Count(label => label.name == EpisodeHud.BreachWarningName),
                        Is.EqualTo(1), where + ": " + take.name + "'s column warns of the deal its control alone breaks.");
                Assert.That(LastActive(EpisodeHud.FinalChoicePageName).GetComponentsInChildren<TMP_Text>().Count(label => label.name == EpisodeHud.EitherWayName),
                    Is.EqualTo(1), where + ": the either-way line, once, over both cards.");
                if (Application.isBatchMode && larger)
                    yield return CaptureFraming("endgame-final-choice-every-deal", settle: false,
                        inspect: frame => AssertTheFinalChoicePage(state, where + " in the capture's 16:9 frame", consequenceMayGo: true));
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);
            Assert.That(director.Snapshot.revision, Is.EqualTo(state.revision), "Reading the page commits nothing.");
        }

        /// <summary>
        /// The comparison over Endgame Preparation: a free tile opens it, the other two are cards
        /// with what the player knows and what taking each would mean if the player wins, nothing
        /// on it decides anything, nothing is committed, and the way back returns to the window.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_ComparingTheFinalistsCostsNothingAndComesBack()
        {
            HoldTheHouseForTheFixture();
            yield return InstallDiaryFixture(
                state => state.phase == EpisodePhase.Social && state.Active.Count() == 3
                    && state.Active.Any(actor => actor.isPlayer) && state.pendingDiary == null,
                "the player in the window at three");
            yield return PutAwayTheCards();
            yield return OpenFreeTime();
            var before = director.Snapshot;
            Assert.That(EpisodeDirector.Preparing(before), Is.True);

            var tile = ButtonWithCaption(EpisodeDirector.CompareFinalistsCaption);
            Assert.That(tile.transform.IsChildOf(LastActive(EpisodeHud.HouseMovesName)), Is.True, "A tile among the moves.");
            Assert.That(Words((RectTransform)tile.transform), Does.Contain("Costs no action"));
            tile.onClick.Invoke();
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();

            Assert.That(director.ComparingFinalists, Is.True);
            Assert.That(Words(LastActive(EpisodeHud.ScreenHeadName)), Does.Contain("COMPARE THE FINALISTS"));
            Assert.That(Words(LastActive("Episode panel")), Does.Contain(EpisodeHud.CertaintyLegendWords));
            foreach (var finalist in FinalistRead.Others(before))
            {
                var card = LastActive(EpisodeHud.FinalistCardPrefix + finalist.name);
                Assert.That(card, Is.Not.Null, finalist.name + " has a card.");
                string words = Words(card);
                foreach (var label in FactLabels) Assert.That(words, Does.Contain(label), finalist.name + "'s card lacks " + label);
                Assert.That(words, Does.Contain("IF YOU WIN AND TAKE " + FinalistRead.FirstName(finalist.name).ToUpperInvariant()));
            }
            Assert.That(director.GetComponentsInChildren<Button>().Any(button => button.IsActive() && IsFinalChoiceControl(button.name)), Is.False,
                "Nothing on the comparison decides anything.");
            AssertEquivalent(before, director.Snapshot);

            ButtonWithCaption(EpisodeDirector.BackToPreparationCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.ComparingFinalists, Is.False);
            Assert.That(Words(LastActive(EpisodeHud.ScreenHeadName)), Does.Contain(EpisodeDirector.EndgamePreparationTitle), "Back in the window.");
            AssertEquivalent(before, director.Snapshot);
        }
    }
}
