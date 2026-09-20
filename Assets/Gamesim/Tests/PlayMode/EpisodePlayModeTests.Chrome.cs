using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The V2 chrome: the top bar, the right column's cards and the conversation dial.
    ///
    /// <para>What these hold to is not how the screen looks — that is judged from the look sheet
    /// against the mockups — but the two things a rebuild of the chrome can silently break: a
    /// caption that stopped being reachable, and a control that fell out of the keyboard's ring
    /// because it moved from a list into a ring of petals.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The seven seats of the dial, in the order the director fills them.</summary>
        private static readonly string[] PetalCaptions =
        {
            EpisodeHud.SmallTalkCaption,
            EpisodeHud.StrategicDiscussionCaption,
            EpisodeHud.PersonalChatCaption,
            EpisodeHud.MorePetalCaption,
            EpisodeHud.RelationshipBuildingCaption,
            EpisodeHud.ShareSecretCaption,
            "Spend time together",
        };

        /// <summary>Actions the dial does not seat. They stay ordinary rows, one level in.</summary>
        private static readonly string[] RowCaptions =
        {
            EpisodeHud.DiscussGameCaption,
            "Promise safety",
            "Share something I know",
            "Ask what they have heard",
        };

        [UnityTest]
        public IEnumerator Chrome_ConversationDialSeatsSevenPetalsAndKeepsEveryCaption()
        {
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            yield return OpenNearbyNpc(maya);

            var dial = ActiveRect(EpisodeHud.DialName);
            Assert.That(dial, Is.Not.Null, "A conversation must be drawn around the speaker as a dial.");

            var petals = dial.GetComponentsInChildren<Button>()
                .Where(button => button.IsActive()).ToArray();
            Assert.That(petals.Length, Is.EqualTo(PetalCaptions.Length),
                "The dial seats the mockups' seven petals; it seated " + petals.Length + ".");

            // Every caption still finds exactly one control - Single() is what the suite's own
            // lookup does, so a duplicated or reworded caption fails here rather than six tests later.
            foreach (var caption in PetalCaptions)
            {
                var button = ButtonWithCaption(caption);
                Assert.That(button.transform.IsChildOf(dial), Is.True,
                    "'" + caption + "' should be a petal on the dial.");
            }
            foreach (var caption in RowCaptions)
            {
                var button = ButtonWithCaption(caption);
                Assert.That(button.transform.IsChildOf(dial), Is.False,
                    "'" + caption + "' is one of the actions the dial does not seat, so it stays a row.");
            }

            // A ring of petals is only a ring if they do not sit on one another.
            Canvas.ForceUpdateCanvases();
            yield return null;
            for (int a = 0; a < petals.Length; a++)
            for (int b = a + 1; b < petals.Length; b++)
            {
                var first = ScreenRect((RectTransform)petals[a].transform);
                var second = ScreenRect((RectTransform)petals[b].transform);
                Assert.That(first.Overlaps(second), Is.False,
                    "Petal '" + petals[a].name + "' " + first + " overlaps '" + petals[b].name + "' " + second + ".");
            }

            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Chrome_MorePetalMovesTheKeyboardToTheRowsBeneathTheDial()
        {
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            yield return OpenNearbyNpc(maya);

            var dial = ActiveRect(EpisodeHud.DialName);
            Assert.That(dial, Is.Not.Null);

            var before = director.Snapshot;
            ButtonWithCaption(EpisodeHud.MorePetalCaption).onClick.Invoke();
            yield return null;

            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.That(selected, Is.Not.Null, "The More petal must hand the keyboard somewhere.");
            Assert.That(selected.transform.IsChildOf(dial), Is.False,
                "The More petal hands the keyboard to the first row beneath the dial, not back to a petal.");

            var panel = ActiveRect("Episode panel");
            Assert.That(selected.transform.IsChildOf(panel), Is.True,
                "It must stay inside the panel; focus outside it is focus the ring cannot walk.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision),
                "Pressing More is navigation. It commits nothing and spends no action.");
            Assert.That(director.Snapshot.socialActions, Is.EqualTo(before.socialActions));

            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Chrome_TopBarSharesOneBandAndTheRightColumnStacksItsCards()
        {
            director.ClosePanels();
            // Twice, with a frame between. The brand sits inside a layout group with a size fitter
            // and the chip is anchored to the canvas: measured in the frame the HUD was rebuilt, the
            // first has not been laid out yet and the second has, and they read eight pixels apart
            // for one frame while agreeing perfectly on every frame after it.
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            var brand = ActiveRect("Brand");
            var pill = ActiveRect("House pill");
            var navigation = ActiveRect("Navigation");
            Assert.That(brand, Is.Not.Null); Assert.That(pill, Is.Not.Null); Assert.That(navigation, Is.Not.Null);

            // One band: the mockups' top bar is a row, not three panels at three heights.
            float top = ScreenRect(brand).yMax;
            Assert.That(ScreenRect(pill).yMax, Is.EqualTo(top).Within(1f),
                "The week chip sits on the top bar's band with the brand.");
            Assert.That(ScreenRect(navigation).yMax, Is.EqualTo(top).Within(1f),
                "The navigation sits on the top bar's band with the brand.");

            var events = ActiveRect(EpisodeHud.RecentEventsCardName);
            Assert.That(events, Is.Not.Null, "The right column carries a recent-events card.");
            Assert.That(ScreenRect(events).yMax, Is.LessThan(ScreenRect(brand).yMin),
                "The right column starts below the top bar.");

            // The column is a stack. Whatever else is in the gutter, nothing may sit on the card.
            foreach (var name in new[] { "Brand", "Navigation", "Objective", "Exploration controls", "Status",
                "House pill", EpisodeDirector.LiveFeedCardName })
            {
                var panel = ActiveRect(name);
                if (panel == null) continue;
                Assert.That(ScreenRect(events).Overlaps(ScreenRect(panel)), Is.False,
                    "'" + EpisodeHud.RecentEventsCardName + "' " + ScreenRect(events) +
                    " overlaps '" + name + "' " + ScreenRect(panel) + ".");
            }
        }

        [UnityTest]
        public IEnumerator Chrome_RecentEventsShowsOnlyWhatThePlayerIsAllowedToHaveSeen()
        {
            director.ClosePanels();
            yield return null;

            var state = director.Snapshot;
            var card = ActiveRect(EpisodeHud.RecentEventsCardName);
            Assert.That(card, Is.Not.Null);

            var secrets = state.events
                .Where(entry => entry.audienceIds.Count > 0 && !entry.audienceIds.Contains(state.playerId))
                .Select(entry => entry.text)
                .Where(text => !string.IsNullOrEmpty(text) && text.Length >= 24)
                .ToArray();

            var shown = card.GetComponentsInChildren<TMP_Text>(true)
                .Where(label => !string.IsNullOrEmpty(label.text))
                .Select(label => label.text)
                .ToArray();

            foreach (var secret in secrets)
                Assert.That(shown.Any(line => line.StartsWith(secret.Substring(0, 24))), Is.False,
                    "The column reported an event the player's character was not party to: " + secret);
        }

        private RectTransform ActiveRect(string name) => director.GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault(rect => rect.name == name && rect.gameObject.activeInHierarchy);
    }
}
