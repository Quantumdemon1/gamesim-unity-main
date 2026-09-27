using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The prompt that says what E does is a button that does it: "E · Get up" gets up, "E ·
        /// Open episode screen" opens the screen. A mouse player reaches everything the key does.
        /// </summary>
        [UnityTest]
        public IEnumerator Prompt_ClickingThePromptDoesWhatEWouldDo()
        {
            var bed = PlacesFor(HouseFurnitureActivity.Sleep).First(anchor => anchor.transform.parent.name == "bedSingle");
            yield return BeginInHouse(bed, HouseFurnitureActivity.Sleep);
            yield return null;
            var button = ButtonWithCaption(EpisodeHud.InteractCaption);
            Assert.That(button.GetComponentsInChildren<TMPro.TMP_Text>().Select(text => text.text), Does.Contain(HouseFurniture.StopPrompt(HouseFurnitureActivity.Sleep)),
                "The words beside the caption say what the click will do.");
            button.onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 3f;
            while (director.IsPlayerHouseActivityActive && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.IsPlayerHouseActivityActive, Is.False, "Clicking \"Get up\" gets up.");

            director.ClosePanels();
            WarpPlayer(director.StationPosition);
            yield return null;
            yield return null;
            // At the screen E opens it - unless a houseguest idling beside it outranks the screen,
            // when E talks to them. Either way the click does what the prompt said, and only that.
            var atScreen = ButtonWithCaption(EpisodeHud.InteractCaption);
            var said = atScreen.GetComponentsInChildren<TMPro.TMP_Text>().Select(text => text.text).ToArray();
            bool screen = said.Contains("E  ·  Open episode screen");
            Assert.That(screen || said.Any(text => text.StartsWith("E  ·  Talk to ")), Is.True,
                "At the screen the prompt offers the screen, or the houseguest beside it: " + string.Join(" | ", said));
            atScreen.onClick.Invoke();
            if (screen)
            {
                Assert.That(director.IsPhasePanelOpen, Is.True, "At the screen, clicking the prompt opens the screen.");
                Assert.That(director.IsConversationOpen, Is.False, "And nothing else.");
            }
            else
            {
                Debug.Log("[Gamesim] prompt: a houseguest by the screen outranked it");
                Assert.That(director.IsConversationOpen, Is.True, "The prompt offered a conversation, and the click opened it.");
                Assert.That(director.IsPhasePanelOpen, Is.False);
            }
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// Nothing to press under a ceremony card. The card takes the pointer by reading the mouse,
        /// not through a raycaster, so a prompt left up beneath it took the click that dismissed
        /// the card as a press of its own - and got the player out of bed.
        /// </summary>
        [UnityTest]
        public IEnumerator Prompt_NothingToPressUnderACeremonyCard()
        {
            var bed = PlacesFor(HouseFurnitureActivity.Sleep).First(anchor => anchor.transform.parent.name == "bedSingle");
            yield return BeginInHouse(bed, HouseFurnitureActivity.Sleep);
            yield return null;
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.InteractCaption), Is.Not.Null, "Asleep, the prompt offers getting up.");
            for (int frame = 0; frame < 3; frame++)
            {
                Gamesim.Presentation.CeremonyOverlays.Showing();
                yield return null;
            }
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.InteractCaption), Is.Null, "Under the card, the prompt stands down.");
            // The card gone, the prompt is back.
            for (int frame = 0; frame < 3; frame++) yield return null;
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.InteractCaption), Is.Not.Null, "And comes back when the card has gone.");
            Assert.That(director.IsPlayerHouseActivityActive, Is.True, "Still asleep.");
            director.FinishPlayerHouseActivity();
            yield return null;
        }
    }
}
