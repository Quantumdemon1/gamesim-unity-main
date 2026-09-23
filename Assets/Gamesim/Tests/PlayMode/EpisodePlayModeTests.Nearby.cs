using System.Collections;
using System.Linq;
using Gamesim.Episode;
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
            yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1), "Listening in commits the house's Eavesdrop.");
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before) + 1),
                "It spends an action, as the card says.");

            caption.Hide();
            yield return null;
            Assert.That(ActiveRect(EpisodeHud.NearbyCardName), Is.Null, "It goes when the conversation does.");
            Assert.That(ActiveRect(EpisodeHud.HouseVibeCardName), Is.Not.Null, "and the week card comes back.");
        }
    }
}
