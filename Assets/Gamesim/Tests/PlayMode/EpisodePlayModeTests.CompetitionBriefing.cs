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
        /// <summary>
        /// The competition's briefing is the whole screen: the house's chrome stands down, the
        /// style guide's modal grows into a full-height sheet down the left, and the camera stands
        /// the arena in the rest of the frame. On the sheet: the hero, the brief and four facts;
        /// practice as the one primary action, with the full rules behind the secondary beside it;
        /// and every way to compete still a control, all of them above the fold.
        /// </summary>
        [UnityTest]
        public IEnumerator CompetitionBriefing_IsTheWholeScreenWithOnePrimaryActionAndTheRulesOnRequest()
        {
            yield return SettleCast();
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var state = director.Snapshot;
            var hud = director.GetComponentInChildren<EpisodeHud>();
            Assert.That(hud.CurrentActivityLayout, Is.EqualTo(EpisodeHud.ActivityLayout.Competition));

            // The whole screen: a full-height sheet, and none of the house's chrome.
            var modal = ActiveRect("Episode panel");
            Assert.That(modal, Is.Not.Null);
            var sheet = ScreenRect(modal);
            Assert.That(sheet.height, Is.GreaterThan(Screen.height * .9f), "The sheet runs the frame's height: " + sheet + ".");
            Assert.That(sheet.width, Is.GreaterThan(Screen.width * .45f).And.LessThan(Screen.width * .62f),
                "It is most of the frame's width, leaving the arena the rest: " + sheet + ".");
            foreach (var name in new[] { IconRail.RootName, EpisodeHud.RecentEventsCardName, CastRail.RootName, "Week chip", "Rail ground" })
                Assert.That(ActiveRect(name), Is.Null, name + " stands down for the challenge.");
            // Except the status line: it is how a failed start or a rejected commit is said, so it
            // stays up, in the frame the sheet leaves.
            var status = ActiveRect("Status");
            Assert.That(status, Is.Not.Null, "The status line stays up.");
            Assert.That(ScreenRect(status).Overlaps(sheet), Is.False, "and stands clear of the sheet: status " + ScreenRect(status)
                + " (anchor " + status.anchorMin + ", pos " + status.anchoredPosition + ", size " + status.sizeDelta + ") against sheet " + sheet + ".");
            Assert.That(ScreenRect(status).xMax, Is.LessThanOrEqualTo(Screen.width + .5f), "and on the frame.");

            // Every way to compete is on the sheet without a scroll: measured before anything below
            // scrolls a control into view.
            var column = ActiveRect("Episode content");
            var fold = (RectTransform)column.parent;
            Assert.That(column.rect.height, Is.LessThanOrEqualTo(fold.rect.height + .5f),
                "The briefing's column fits its sheet: " + column.rect.height + " in " + fold.rect.height + ".");

            // The hero, and the facts four to a row.
            Assert.That(ActiveRect(EpisodeHud.BriefingHeroName), Is.Not.Null, "The briefing leads with its hero card.");
            var facts = ActiveRect(EpisodeHud.BriefingFactsName);
            Assert.That(facts, Is.Not.Null);
            Assert.That(facts.Cast<Transform>().Count(child => child.name == "Fact"), Is.EqualTo(4));
            foreach (var label in facts.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False, "A fact's words fit its cell: '" + label.text + "'.");
            }

            // One primary action: practice, in the action blue, and nothing else in it.
            var practice = ButtonWithCaption("Practice this competition");
            Assert.That(Same(practice.GetComponent<Image>().color, UiTheme.ActionBlue), Is.True, "Practice is the primary action.");
            var primaries = modal.GetComponentsInChildren<Button>()
                .Where(button => button.GetComponent<Image>() != null && Same(button.GetComponent<Image>().color, UiTheme.ActionBlue)).ToArray();
            Assert.That(primaries, Has.Length.EqualTo(1), "One primary action a modal: " + string.Join(", ", primaries.Select(button => button.name)));

            // The full rules on request, in place, and away again.
            var rules = modal.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == EpisodeHud.FullRulesName);
            Assert.That(rules.gameObject.activeInHierarchy, Is.False, "The fine print waits to be asked for.");
            ButtonWithCaption(EpisodeHud.ViewRulesCaption).onClick.Invoke();
            yield return null;
            Assert.That(rules.gameObject.activeInHierarchy, Is.True, "View full rules opens them.");
            Assert.That(string.Join("\n", rules.GetComponentsInChildren<TMP_Text>().Select(text => text.text)), Does.Contain("Competing: "));
            ButtonWithCaption(EpisodeHud.HideRulesCaption).onClick.Invoke();
            yield return null;
            Assert.That(rules.gameObject.activeInHierarchy, Is.False, "and Hide full rules puts them away.");
            Assert.That(ButtonWithCaption(EpisodeHud.ViewRulesCaption), Is.Not.Null);

            // Every way to compete is still a control on the card.
            var game = CompetitionMiniGames.For(EpisodeEngine.CompetitionCategory(state));
            foreach (var caption in new[] { CompetitionMiniGames.EnterCaption(game), "Accessible alternative: steady 1-point bonus",
                EpisodeHud.SimulateCompetitionCaption, EpisodeHud.ThrowCompetitionCaption })
            {
                var button = ButtonWithCaption(caption);
                Assert.That(button.transform.IsChildOf(modal) && button.IsInteractable(), Is.True, "'" + caption + "' is on the card.");
                // The card is as wide as the column its words were measured for: a stage's reading
                // cap leaking into the briefing laid the column out narrower than the cards were
                // built for, and ran their words past the card's edge while each still "fitted".
                Assert.That(((RectTransform)button.transform).rect.width, Is.EqualTo(hud.ColumnWidth).Within(6f),
                    "'" + caption + "' is laid out " + ((RectTransform)button.transform).rect.width + " wide in a column built " + hud.ColumnWidth + " wide.");
                // Its words stay on its card.
                var card = ScreenRect((RectTransform)button.transform);
                foreach (var words in button.GetComponentsInChildren<TMP_Text>())
                    Assert.That(ScreenRect(words.rectTransform).xMax, Is.LessThanOrEqualTo(card.xMax + 1f),
                        "'" + words.text + "' runs past the edge of '" + caption + "'.");
            }

            // And the camera stands the arena in the frame the sheet leaves.
            Assert.That(director.IsFramingBriefing, Is.True, "The briefing looks at the competition's set.");
            yield return Settle(() => !cameraRig.IsTravelling && cameraRig.HasArrived(0.05f), 4f);
            var arena = cameraRig.ViewCamera.WorldToScreenPoint(director.StationPosition + Vector3.up);
            Assert.That(arena.x, Is.GreaterThan(ScreenRect(modal).xMax).And.LessThan(Screen.width),
                "The arena stands right of the sheet, not behind it or off the frame: " + arena + ".");
            if (Application.isBatchMode) yield return CaptureFraming("competition-briefing");
            director.ClosePanels();
            yield return null;
            Assert.That(cameraRig.HasShot, Is.False, "Closing the briefing lets its shot go.");

            // Nothing on the sheet is cut off or runs past its card, at either text size.
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                WarpPlayer(director.StationPosition);
                Assert.That(director.TryOpenPhasePanel(), Is.True);
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var sheetNow = ActiveRect("Episode panel");
                foreach (var label in sheetNow.GetComponentsInChildren<TMP_Text>())
                {
                    label.ForceMeshUpdate();
                    Assert.That(label.isTextOverflowing, Is.False, "At " + (larger ? "larger" : "standard") + " text '" + label.text + "' is cut off.");
                }
                foreach (var control in sheetNow.GetComponentsInChildren<Button>())
                {
                    var edge = ScreenRect((RectTransform)control.transform).xMax;
                    foreach (var words in control.GetComponentsInChildren<TMP_Text>())
                        Assert.That(ScreenRect(words.rectTransform).xMax, Is.LessThanOrEqualTo(edge + 1f),
                            "At " + (larger ? "larger" : "standard") + " text '" + words.text + "' runs past its control.");
                }
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);
        }
    }
}
