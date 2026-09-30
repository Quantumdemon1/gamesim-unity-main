using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
        /// ENDGAME-PLAN F2: the final Head of Household's choice is two cards, one per finalist, each
        /// with who they are, what the player knows and how sure, and what taking them means; under
        /// each card, "Take {name} to the Final 2" over the control that does it - the other
        /// finalist's pinned "Evict {name}" - in that card's own column. The warning and the legend
        /// are on the screen. Side by side at the resting text, one under the other at the larger,
        /// every line fitting its card at both. Reading it commits nothing.
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
                Canvas.ForceUpdateCanvases();
                var panel = LastActive("Episode panel");
                // As it opens, before anything is scrolled for the test: the screen goes to its first
                // control, and the warning is on screen with it.
                var viewport = panel.GetComponentsInChildren<ScrollRect>().First(scroll => scroll.gameObject.activeInHierarchy).viewport;
                var view = ScreenRect(viewport);
                Assert.That(panel.GetComponentsInChildren<TMP_Text>().Where(text => text.name == EpisodeHud.FinalistWarningName)
                        .Any(text => view.Contains(ScreenRect(text.rectTransform).center)), Is.True,
                    "The warning is in sight as the screen opens (" + (larger ? "larger" : "resting") + " text).");
                Assert.That(panel, Is.Not.Null);
                Assert.That(Words(panel), Does.Contain(EpisodeDirector.FinalChoiceWarning).And.Contain(EpisodeHud.CertaintyLegendWords),
                    "The warning and what the certainty words mean.");
                Assert.That(LastActive(EpisodeHud.FinalistColumnsName), Is.Not.Null, "The finalists as columns.");
                // MOCKUP-PASS M8 (mockup 59): the screen's own gold head, and no line of who holds
                // what over it - the house is the three, and the two it is between are on the cards.
                var head = LastActive(EpisodeHud.ScreenHeadName);
                Assert.That(head, Is.Not.Null, "The choice has a head.");
                Assert.That(Words(head), Does.Contain(EpisodeDirector.FinalTwoHeadTitle).And.Contain(EpisodeDirector.FinalTwoHeadLine));
                Assert.That(head.IsChildOf(panel) && ScreenRect(head).yMin >= ScreenRect(LastActive(EpisodeHud.FinalistColumnsName)).yMax - 1f, Is.True,
                    "The head is over the cards.");
                string status = EpisodeDirector.HouseStatus(state);
                if (status != null) Assert.That(Words(panel), Does.Not.Contain(status), "The house's status line stands down on this screen.");
                AssertEveryLabelDraws(panel, "The final choice (" + (larger ? "larger" : "resting") + " text)");

                // Both controls whole as the screen opens, still before anything is scrolled for the
                // test: each inside its own column with its caption drawn to the end, and neither cut
                // by the edge of the view. Side by side they are level, the headlines and warnings
                // over them too, so the control the screen opens on brings the other into sight.
                string size = larger ? "larger" : "resting";
                var controls = others.Select(take => (take, control: (RectTransform)FindButton(
                    "Evict " + others.Single(other => other.id != take.id).name).transform)).ToList();
                foreach (var (take, control) in controls)
                {
                    var rect = ScreenRect(control);
                    var own = ScreenRect(LastActive("Finalist column · " + take.name));
                    Assert.That(rect.yMin >= own.yMin - 1f && rect.yMax <= own.yMax + 1f, Is.True,
                        take.name + "'s control sits inside its column (" + size + " text).");
                    foreach (var caption in control.GetComponentsInChildren<TMP_Text>())
                        Assert.That(ShowsAllOf(caption), Is.True, "The control loses the end of \"" + caption.text + "\" (" + size + " text).");
                    if (rect.Overlaps(view))
                        Assert.That(rect.yMin >= view.yMin - 1f && rect.yMax <= view.yMax + 1f, Is.True,
                            take.name + "'s control is cut by the edge of the view as the screen opens (" + size + " text).");
                }
                Assert.That(controls.Any(entry => ScreenRect(entry.control).Overlaps(view)), Is.True,
                    "A control is in sight as the screen opens (" + size + " text).");
                if (!larger)
                {
                    var left = ScreenRect(controls[0].control);
                    var right = ScreenRect(controls[1].control);
                    Assert.That(Mathf.Abs(left.yMin - right.yMin), Is.LessThan(1f), "The two controls are level.");
                    Assert.That(left.Overlaps(view) && right.Overlaps(view), Is.True, "Level, both are in sight as the screen opens.");
                    foreach (var part in new[] { EpisodeHud.FinalistHeadlineName, EpisodeHud.FinalistWarningName })
                    {
                        var baselines = others.Select(take => ScreenRect(LastActive("Finalist column · " + take.name)
                            .GetComponentsInChildren<TMP_Text>().Single(text => text.name == part).rectTransform).yMin).ToList();
                        Assert.That(Mathf.Abs(baselines[0] - baselines[1]), Is.LessThan(1f), "The " + part + " lines share a baseline.");
                    }
                }

                foreach (var take in others)
                {
                    var cut = others.Single(other => other.id != take.id);
                    var card = LastActive(EpisodeHud.FinalistCardPrefix + take.name);
                    Assert.That(card, Is.Not.Null, take.name + " has a card.");
                    string words = Words(card);
                    Assert.That(words, Does.Contain(take.name).And.Contain("WHAT YOU KNOW")
                        .And.Contain("IF YOU TAKE " + FinalistRead.FirstName(take.name).ToUpperInvariant()), take.name + "'s card: " + words);
                    foreach (var label in FactLabels) Assert.That(words, Does.Contain(label), take.name + "'s card lacks " + label);
                    Assert.That(words, Does.Contain(FinalistRead.FirstName(cut.name) + " joins the jury"), "What taking them does to the other.");
                    Assert.That(card.GetComponentsInChildren<CharacterPortraitBinding>(true), Has.Length.EqualTo(1), "A photo bound to them.");
                    AssertDecisionCopyFits(card);

                    var column = LastActive("Finalist column · " + take.name);
                    Assert.That(column, Is.Not.Null);
                    var headline = column.GetComponentsInChildren<TMP_Text>().Single(text => text.name == EpisodeHud.FinalistHeadlineName);
                    Assert.That(headline.text, Is.EqualTo("Take " + take.name + " to the Final 2"));
                    var warning = column.GetComponentsInChildren<TMP_Text>().Single(text => text.name == EpisodeHud.FinalistWarningName);
                    Assert.That(warning.text, Is.EqualTo(EpisodeDirector.FinalChoiceWarning));
                    var button = ButtonWithCaption("Evict " + cut.name);
                    Assert.That(button.transform.IsChildOf(column), Is.True,
                        "Taking " + take.name + " evicts " + cut.name + ", in " + take.name + "'s column.");
                    Canvas.ForceUpdateCanvases();
                    Assert.That(ScreenRect(headline.rectTransform).yMin, Is.GreaterThanOrEqualTo(ScreenRect(warning.rectTransform).yMax - 1f),
                        "The headline, then the warning,");
                    Assert.That(ScreenRect(warning.rectTransform).yMin, Is.GreaterThanOrEqualTo(ScreenRect((RectTransform)button.transform).yMax - 1f),
                        "then the control they are about.");
                    Assert.That(column.GetComponentsInChildren<Button>().Count(b => b.interactable), Is.EqualTo(1),
                        "The caption's control is the only thing to press in a column.");

                    // MOCKUP-PASS M8: what the choice does, under the warning and over the control;
                    // the jury read as three counts that between them count every juror once; the
                    // record's counts; and a gold edge on the card waiting for its control's focus.
                    var consequence = column.GetComponentsInChildren<TMP_Text>().Single(text => text.name == EpisodeHud.FinalistConsequenceName);
                    Assert.That(consequence.text, Is.EqualTo(EpisodeDirector.FinalChoiceConsequence(take, cut)));
                    Assert.That(ScreenRect(warning.rectTransform).yMin, Is.GreaterThanOrEqualTo(ScreenRect(consequence.rectTransform).yMax - 1f), "The warning over what the choice does,");
                    Assert.That(ScreenRect(consequence.rectTransform).yMin, Is.GreaterThanOrEqualTo(ScreenRect((RectTransform)button.transform).yMax - 1f), "and that over the control.");
                    Assert.That(words, Does.Contain("JURY READ (" + FinalistRead.Jurors(state).Count), "The jury read, headed with the jury's size.");
                    var counts = card.GetComponentsInChildren<TMP_Text>().Where(text => text.name == "Jury count value").Select(text => int.Parse(text.text)).ToList();
                    Assert.That(counts, Has.Count.EqualTo(3), "Known support, bitterness and uncertain, a count each.");
                    Assert.That(counts.Sum(), Is.EqualTo(FinalistRead.Jurors(state).Count), "Every juror counted once.");
                    Assert.That(words, Does.Contain("HoH wins").And.Contain("Veto wins").And.Contain("Your standing"));
                    Assert.That(card.GetComponentsInChildren<Image>(true).Count(image => image.name == EpisodeHud.ChosenEdgeName), Is.EqualTo(1),
                        "One gold edge, lit by the control.");
                    Assert.That(words, Does.Not.Contain("FINAL HEAD OF HOUSEHOLD"), "The crown is the player's tonight, not a finalist's.");
                }

                var first = LastActive("Finalist column · " + others[0].name);
                var second = LastActive("Finalist column · " + others[1].name);
                var a = ScreenRect(first); var b = ScreenRect(second);
                if (!larger)
                {
                    Assert.That(b.xMin, Is.GreaterThanOrEqualTo(a.xMax - 1f), "Side by side at the resting text.");
                    Assert.That(Mathf.Abs(a.width - b.width), Is.LessThan(2f), "As equals.");
                    // The VS stands in the gap between them, over neither.
                    var versus = LastActive(EpisodeHud.VersusName);
                    Assert.That(versus, Is.Not.Null, "The two are set against each other.");
                    float middle = ScreenRect(versus).center.x;
                    Assert.That(middle >= a.xMax - 1f && middle <= b.xMin + 1f, Is.True, "The VS is in the gap: " + middle + " between " + a.xMax + " and " + b.xMin + ".");
                    if (Application.isBatchMode) yield return CaptureFraming("endgame-final-choice", settle: false);

                    // The selection lights its own column: the card's gold edge and the ring's fill
                    // follow the control the player puts the keyboard on, and move with it. Neither is
                    // a control. Found again after each frame: a render in between rebuilds the panel
                    // and restores the selection by its caption.
                    RectTransform ControlFor(ContestantState taken) =>
                        (RectTransform)FindButton("Evict " + others.Single(finalist => finalist.id != taken.id).name).transform;
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
                    // The screen opens with the keyboard on its first control, as every panel does, and
                    // lights no card for it: that control is the HUD's pick, not the player's, on a
                    // screen whose head says nothing on it is a prediction.
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null, "The screen opens on a control.");
                    AssertLit(null, "as the screen opens");
                    foreach (var take in others)
                    {
                        // Through nothing, so that selecting even the control the screen opened on is a move.
                        EventSystem.current.SetSelectedGameObject(null);
                        EventSystem.current.SetSelectedGameObject(ControlFor(take).gameObject);
                        yield return null;
                        AssertLit(take, "with " + take.name + "'s control selected");
                    }
                    // The pointer's column wins while the pointer is on its control, because a click
                    // lands there whatever the keyboard is on, and gives the light back as it leaves:
                    // one card lit at a time, never both. The pointer moves no selection.
                    var keyboardOn = others[1];
                    var pointed = ControlFor(others[0]).gameObject;
                    var pointer = new PointerEventData(EventSystem.current);
                    ExecuteEvents.Execute(pointed, pointer, ExecuteEvents.pointerEnterHandler);
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(ControlFor(keyboardOn).gameObject), "The keyboard stays where it was.");
                    AssertLit(others[0], "under the pointer, with " + keyboardOn.name + "'s control selected");
                    ExecuteEvents.Execute(pointed, pointer, ExecuteEvents.pointerExitHandler);
                    AssertLit(keyboardOn, "once the pointer leaves");

                    // The decision screen from its top: the head, the cards' faces and who they are.
                    if (Application.isBatchMode)
                    {
                        // Found again: the selection moves above re-render the HUD, and the panel read at
                        // the top of this pass is the destroyed copy by now.
                        LastActive("Episode panel").GetComponentsInChildren<ScrollRect>().First(scroll => scroll.gameObject.activeInHierarchy).verticalNormalizedPosition = 1f;
                        yield return null;
                        yield return CaptureFraming("endgame-final-choice-top", settle: false);
                    }
                }
                else
                {
                    Assert.That(b.yMax, Is.LessThanOrEqualTo(a.yMin + 1f), "One under the other at the larger text.");
                    Assert.That(LastActive(EpisodeHud.VersusName), Is.Null, "No VS between columns stacked one under the other.");
                }

                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);
            Assert.That(director.Snapshot.revision, Is.EqualTo(state.revision), "Reading the cards commits nothing.");
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.FinalEviction));
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
            Assert.That(director.GetComponentsInChildren<Button>().Any(button => button.IsActive() && button.name.StartsWith("Evict ")), Is.False,
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
