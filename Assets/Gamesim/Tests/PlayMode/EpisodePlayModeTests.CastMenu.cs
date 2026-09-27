using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The cast strip's card (playtest, 2026-09-27): a chip only swung the camera onto its
        /// houseguest. It opens a card over the chip now, and the card takes the keyboard: walk over
        /// and talk, their profile, what is between you on the relationships page, or follow them.
        /// Choosing closes it, and so does the same chip again.
        /// </summary>
        [UnityTest]
        public IEnumerator CastMenu_AChipOpensItsHouseguestsWaysOn()
        {
            yield return null;
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            string first = maya.DisplayName.Split(' ')[0];
            Button Chip()
            {
                var rail = director.GetComponentsInChildren<RectTransform>().Last(rect => rect.name == CastRail.RootName);
                return rail.Cast<Transform>().Select(child => child.GetComponent<Button>()).Last(button => button != null && button.name == maya.DisplayName);
            }

            Chip().onClick.Invoke();
            yield return null;
            var menu = ActiveRect(EpisodeHud.CastMenuName);
            Assert.That(menu, Is.Not.Null, "A chip opens its houseguest's card.");
            foreach (var caption in new[] { EpisodeHud.CastTalkCaption(first), EpisodeHud.CastProfileCaption(first),
                EpisodeHud.CastDealsCaption(first), EpisodeHud.CastFollowCaption(first, false) })
                Assert.That(ButtonWithCaption(caption).transform.IsChildOf(menu), Is.True, "'" + caption + "' is on the card.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(EpisodeHud.CastTalkCaption(first)), "The card takes the keyboard.");

            ButtonWithCaption(EpisodeHud.CastFollowCaption(first, false)).onClick.Invoke();
            yield return null;
            Assert.That(cameraRig.FocusedSubject, Is.SameAs(maya.transform), "Follow is what the chip used to do.");
            Assert.That(ActiveRect(EpisodeHud.CastMenuName), Is.Null, "Choosing closes the card.");

            Chip().onClick.Invoke();
            yield return null;
            ButtonWithCaption(EpisodeHud.CastProfileCaption(first)).onClick.Invoke();
            yield return null;
            Assert.That(director.ProfileId, Is.EqualTo(maya.Id), "Their profile opens on the Houseguests page.");
            director.ClosePanels();
            yield return null;

            Chip().onClick.Invoke();
            yield return null;
            ButtonWithCaption(EpisodeHud.CastDealsCaption(first)).onClick.Invoke();
            yield return null;
            Assert.That(RelationshipWeb.Selected, Is.EqualTo(maya.Id), "What is between you: the relationships page, on them.");
            Assert.That(director.IsPanelOpen, Is.True);
            director.ClosePanels();
            yield return null;

            Chip().onClick.Invoke();
            yield return null;
            ButtonWithCaption(EpisodeHud.CastTalkCaption(first)).onClick.Invoke();
            yield return null;
            Assert.That(director.WalkingToId == maya.Id || director.TalkingToId == maya.Id, Is.True,
                "Talk walks over to them, or talks at once when they are close.");
            director.ClosePanels();
            yield return null;

            Chip().onClick.Invoke();
            yield return null;
            Assert.That(ActiveRect(EpisodeHud.CastMenuName), Is.Not.Null);
            Chip().onClick.Invoke();
            yield return null;
            Assert.That(ActiveRect(EpisodeHud.CastMenuName), Is.Null, "The same chip again closes it.");
        }
    }
}
