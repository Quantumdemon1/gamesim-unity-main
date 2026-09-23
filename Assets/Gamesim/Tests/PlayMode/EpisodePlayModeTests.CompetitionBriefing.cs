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
        /// The competition's briefing as the style guide's modal: a card beside the rail with the
        /// house, the right column and the strip up around it and the arena in the free area; the
        /// hero, the brief and four facts; practice as the one primary action, with the full rules
        /// behind the secondary beside it; and every way to compete still a control. It was the
        /// generic activity panel: the whole width, every line the width of the screen, five
        /// actions of equal weight and the strip and the column gone.
        /// </summary>
        [UnityTest]
        public IEnumerator CompetitionBriefing_IsACardBesideTheRailWithOnePrimaryActionAndTheRulesOnRequest()
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

            // A card beside the rail, with the house's chrome still up around it.
            var modal = ActiveRect("Episode panel");
            var rail = ActiveRect(IconRail.RootName);
            Assert.That(modal, Is.Not.Null); Assert.That(rail, Is.Not.Null);
            Assert.That(ScreenRect(modal).xMin, Is.GreaterThanOrEqualTo(ScreenRect(rail).xMax),
                "The briefing stands beside the rail: " + ScreenRect(modal) + " against " + ScreenRect(rail) + ".");
            Assert.That(ScreenRect(modal).width, Is.LessThan(Screen.width * .5f), "It is a card, not the frame's width.");
            foreach (var name in new[] { EpisodeHud.RecentEventsCardName, CastRail.RootName })
            {
                var chrome = ActiveRect(name);
                Assert.That(chrome, Is.Not.Null, name + " stays up around the briefing.");
                Assert.That(ScreenRect(chrome).Overlaps(ScreenRect(modal)), Is.False, name + " is clear of the card.");
            }
            // The rail's groups carry their names.
            var labels = director.GetComponentsInChildren<TMP_Text>().Where(text => text.name == "Rail label").Select(text => text.text).ToArray();
            Assert.That(labels, Does.Contain("PLAY").And.Contain("NOTEBOOK & SETTINGS"));

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
            }

            // And the camera is on the arena, in the free area beside the card.
            Assert.That(director.IsFramingBriefing, Is.True, "The briefing looks at the competition's set.");
            yield return Settle(() => !cameraRig.IsTravelling && cameraRig.HasArrived(0.05f), 4f);
            var arena = cameraRig.ViewCamera.WorldToScreenPoint(director.StationPosition + Vector3.up);
            Assert.That(arena.x, Is.GreaterThan(ScreenRect(modal).xMax), "The arena stands right of the card, not behind it: " + arena + ".");
            if (Application.isBatchMode) yield return CaptureFraming("competition-briefing");
            director.ClosePanels();
            yield return null;
            Assert.That(cameraRig.HasShot, Is.False, "Closing the briefing lets its shot go.");
        }
    }
}
