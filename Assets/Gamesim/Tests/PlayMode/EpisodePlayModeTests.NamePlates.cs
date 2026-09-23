using System.Collections;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// A houseguest's name is the mockups' plate (01, 12) - the pack's dark glass with a blue
        /// edge and the given name in the HUD's face - not the default font's 3D text, which read as
        /// a large grey word across a director's close shot. And the follow diamond stands over the
        /// plate, as mockup-01 stacks them: at head height it stood across the name and read as a
        /// white shape through it.
        /// </summary>
        [UnityTest]
        public IEnumerator NamePlates_AreThePlateAndTheFollowDiamondStandsOverIt()
        {
            yield return null;
            var npcs = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy).ToArray();
            Assume.That(npcs, Is.Not.Empty);
            foreach (var npc in npcs)
            {
                var text = npc.GetComponentInChildren<TextMesh>(true);
                if (text == null) continue;
                var renderer = text.GetComponent<MeshRenderer>();
                Assert.That(renderer == null || !renderer.enabled, Is.True, npc.Id + "'s 3D text no longer draws the name.");
                Assert.That(Plate(npc), Is.Not.Null, npc.Id + " carries a name plate.");
                string given = npc.DisplayName.Split(' ')[0];
                Assert.That(Plate(npc).GetComponentsInChildren<TMPro.TMP_Text>(true).Select(label => label.text), Does.Contain(given));
            }

            var chosen = npcs[0];
            director.FollowHouseguest(chosen.Id);
            yield return null; yield return null;
            foreach (var npc in npcs)
                Assert.That(npc.Spotlit, Is.EqualTo(npc == chosen),
                    npc == chosen ? "The followed houseguest's plate stays up." : npc.Id + " is not the one you are with.");

            var diamond = GameObject.Find(FollowRing.DiamondName);
            Assert.That(diamond, Is.Not.Null, "The followed houseguest wears the diamond.");
            var corners = new Vector3[4];
            Plate(chosen).GetWorldCorners(corners);
            float plateTop = corners.Max(corner => corner.y);
            float diamondFoot = diamond.transform.position.y - FollowRing.DiamondHalfHeight;
            Assert.That(diamondFoot, Is.GreaterThan(plateTop),
                "The diamond's point stays over the plate's top edge, not across the name: " + diamondFoot + " against " + plateTop + ".");

            if (Application.isBatchMode) yield return CaptureFraming("followed");
            director.FollowHouseguest(chosen.Id);
            yield return null;
        }

        private static RectTransform Plate(HouseNpc npc) =>
            npc.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(rect => rect.name == HouseNpc.PlateName);
    }
}
