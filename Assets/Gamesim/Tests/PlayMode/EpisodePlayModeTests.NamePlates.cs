using System.Collections;
using System.Linq;
using Gamesim.House;
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
        /// A houseguest's name is the mockups' plate (01, 12) - the pack's dark glass with a blue
        /// edge and the given name in the HUD's face - not the default font's 3D text, which read as
        /// a large grey word across a director's close shot. And the follow diamond stands over the
        /// plate, as mockup-01 stacks them: at head height it stood across the name and read as a
        /// white shape through it.
        /// </summary>
        [UnityTest]
        public IEnumerator NamePlates_AreThePlateAndTheFollowSpotlightHangsOverIt()
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

            var spot = GameObject.Find(FollowRing.SpotlightName);
            Assert.That(spot, Is.Not.Null, "The followed houseguest stands in the spotlight.");
            var corners = new Vector3[4];
            Plate(chosen).GetWorldCorners(corners);
            float plateTop = corners.Max(corner => corner.y);
            Assert.That(spot.transform.position.y, Is.GreaterThan(plateTop + .5f),
                "The light hangs well over the plate, never in front of the name: " + spot.transform.position.y + " against " + plateTop + ".");

            if (Application.isBatchMode) yield return CaptureFraming("followed");
            director.FollowHouseguest(chosen.Id);
            yield return null;
        }

        /// <summary>
        /// Mockup-12 keeps the name of the one you are talking to over their head. The two-shot
        /// frames the pair's faces in the upper third, and a plate at its dollhouse height went up
        /// with them into the top bar's band, behind the chips; close up it comes down to the head.
        /// </summary>
        [UnityTest]
        public IEnumerator NamePlates_TheOneYouAreTalkingToWearsTheirNameUnderTheTopBar()
        {
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            yield return OpenNearbyNpc(maya);
            yield return Settle(() => !cameraRig.IsTravelling && cameraRig.HasArrived(0.05f), 4f);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            Plate(maya).GetWorldCorners(corners);
            var eye = cameraRig.ViewCamera;
            float top = corners.Max(corner => eye.WorldToScreenPoint(corner).y);
            float bottom = corners.Min(corner => eye.WorldToScreenPoint(corner).y);
            var bar = ActiveRect("House pill");
            Assume.That(bar, Is.Not.Null);
            Assert.That(bottom, Is.GreaterThan(0f), "The plate is in the frame.");
            var seat = maya.GetComponent<HouseSeatPresentation>();
            Assert.That(top, Is.LessThan(ScreenRect(bar).yMin),
                "Maya's plate stands under the top bar, over her head: its top at " + top + ", the bar's foot at " + ScreenRect(bar).yMin
                + " on a " + Screen.width + "x" + Screen.height + " screen; plate centre y " + Plate(maya).position.y.ToString("0.00")
                + ", feet y " + maya.transform.position.y.ToString("0.00") + ", seated " + (seat != null && seat.Active)
                + ", eye " + eye.transform.position.ToString("0.00") + " pitch " + cameraRig.Pitch.ToString("0.0")
                + " distance " + cameraRig.Distance.ToString("0.00") + ".");
            director.ClosePanels();
            yield return null;
        }

        private static RectTransform Plate(HouseNpc npc) =>
            npc.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(rect => rect.name == HouseNpc.PlateName);
    }
}
