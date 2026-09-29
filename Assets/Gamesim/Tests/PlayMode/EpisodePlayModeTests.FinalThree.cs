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
            {
                text.ForceMeshUpdate();
                Assert.That(text.isTextOverflowing, Is.False, "The week chip loses the end of \"" + text.text + "\".");
            }

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

                foreach (var take in others)
                {
                    var cut = others.Single(other => other.id != take.id);
                    var card = LastActive(EpisodeHud.FinalistCardPrefix + take.name);
                    Assert.That(card, Is.Not.Null, take.name + " has a card.");
                    string words = Words(card);
                    Assert.That(words, Does.Contain(take.name).And.Contain("WHAT YOU KNOW")
                        .And.Contain("IF YOU TAKE " + take.name.Split(' ')[0].ToUpperInvariant()), take.name + "'s card: " + words);
                    foreach (var label in FactLabels) Assert.That(words, Does.Contain(label), take.name + "'s card lacks " + label);
                    Assert.That(words, Does.Contain(cut.name.Split(' ')[0] + " joins the jury"), "What taking them does to the other.");
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
                }

                var first = LastActive("Finalist column · " + others[0].name);
                var second = LastActive("Finalist column · " + others[1].name);
                var a = ScreenRect(first); var b = ScreenRect(second);
                if (!larger)
                {
                    Assert.That(b.xMin, Is.GreaterThanOrEqualTo(a.xMax - 1f), "Side by side at the resting text.");
                    Assert.That(Mathf.Abs(a.width - b.width), Is.LessThan(2f), "As equals.");
                    if (Application.isBatchMode) yield return CaptureFraming("endgame-final-choice", settle: false);
                }
                else Assert.That(b.yMax, Is.LessThanOrEqualTo(a.yMin + 1f), "One under the other at the larger text.");

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
                Assert.That(words, Does.Contain("IF YOU WIN AND TAKE " + finalist.name.Split(' ')[0].ToUpperInvariant()));
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
