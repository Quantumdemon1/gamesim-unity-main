using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// Mockup-06's Nearby card: while two houseguests are talking where the player can see
        /// them, the right column offers to listen in - the house's own Eavesdrop, its cost and
        /// its odds - in the week card's place, and it goes when the conversation does.
        /// </summary>
        [UnityTest]
        public IEnumerator Nearby_AConversationWithinEarshotOffersToListenIn()
        {
            yield return null;
            Assert.That(director.CanListenIn, Is.True, "Free time with actions left should offer listening in.");
            Assert.That(ActiveRect(EpisodeHud.NearbyCardName), Is.Null, "Nothing is being witnessed yet.");

            var caption = SceneComponents<HouseConversationCaption>().First();
            caption.Show("Maya Hassan", "Riley Johnson", "strategy", 1f);
            yield return null; yield return null;
            var card = ActiveRect(EpisodeHud.NearbyCardName);
            Assert.That(card, Is.Not.Null, "A witnessed conversation brings up the Nearby card.");
            Assert.That(ActiveRect(EpisodeHud.HouseVibeCardName), Is.Null, "It takes the week card's place.");
            // And mockup-06's bar, in the status line's place while it holds.
            var bar = ActiveRect(EpisodeHud.SpeechBarName);
            Assert.That(bar, Is.Not.Null, "The bar comes up with the card.");
            Assert.That(ActiveRect("Status"), Is.Null, "It stands in the status line's place.");
            Assert.That(ScreenRect(bar).Overlaps(ScreenRect(ActiveRect(CastRail.RootName))), Is.False, "The bar covers the strip.");
            foreach (var name in new[] { EpisodeDirector.LiveFeedCardName, EpisodeHud.RecentEventsCardName })
            {
                var other = ActiveRect(name);
                if (other != null) Assert.That(ScreenRect(card).Overlaps(ScreenRect(other)), Is.False, "The card covers '" + name + "'.");
            }
            if (Application.isBatchMode) yield return CaptureFraming("nearby", settle: false);

            caption.Show("Maya Hassan", "Riley Johnson", "strategy", 1f);
            yield return null;
            var before = director.Snapshot;
            ButtonWithCaption(EpisodeHud.ListenInCaption).onClick.Invoke();
            // Read as the click returns: its commit is synchronous, and a frame later the house can
            // have committed something of its own. Run alone, this test once read revision +2: the
            // walk-in watch, on its four-second cadence, committed "You walk in on something" in
            // the frame after the click.
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1), "Listening in commits the house's Eavesdrop.");
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before) + 1),
                "It spends an action, as the card says.");
            yield return null;

            caption.Hide();
            yield return null;
            Assert.That(ActiveRect(EpisodeHud.NearbyCardName), Is.Null, "It goes when the conversation does.");
            Assert.That(ActiveRect(EpisodeHud.HouseVibeCardName), Is.Not.Null, "and the week card comes back.");
            Assert.That(ActiveRect(EpisodeHud.SpeechBarName), Is.Null, "and the bar goes with it,");
            Assert.That(ActiveRect("Status"), Is.Not.Null, "the status line back in its place.");
        }

        /// <summary>
        /// A render rebuilds the chrome, the Nearby card with it, and the card is built hidden. A
        /// render Update orders for a body finishing assembly comes after that frame's Nearby tick,
        /// so the card used to be gone for the rest of the frame and a press on Listen in landed on
        /// nothing. The test renders from its coroutine, which runs after Update, as that one does.
        /// </summary>
        [UnityTest]
        public IEnumerator Nearby_ARenderPutsItBackInTheSameFrame()
        {
            yield return null;
            Assert.That(director.CanListenIn, Is.True, "Free time with actions left should offer listening in.");
            SceneComponents<HouseConversationCaption>().First().Show("Maya Hassan", "Riley Johnson", "strategy", 1f);
            yield return null; yield return null;
            Assert.That(ActiveRect(EpisodeHud.NearbyCardName), Is.Not.Null, "A witnessed conversation brings up the Nearby card.");
            Assert.That(ControlCarrying(EpisodeHud.ListenInCaption), Is.Not.Null, "and its Listen in control.");
            // With nothing open, closing the panels is a render: the one the body-assembly check orders.
            director.ClosePanels();
            Assert.That(ActiveRect(EpisodeHud.NearbyCardName), Is.Not.Null,
                "The render that rebuilt the card puts it back before the frame ends.");
            Assert.That(ControlCarrying(EpisodeHud.ListenInCaption), Is.Not.Null,
                "and Listen in with it, so a press in this frame lands on it.");
        }
    }
}
