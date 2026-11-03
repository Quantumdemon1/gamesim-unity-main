using System.Collections;
using System.Linq;
using Gamesim.Episode;
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
                string given = CharacterNames.FirstName(npc.DisplayName);
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

            // Plates are for drawn houseguests only (RS4): on the followed one's room - the follow's own room
            // view from RS5, and the scope held - they keep theirs whatever the view, and anyone the view hides
            // has theirs down. One of the others stands in a room well away from them and from the player, so
            // somebody is hidden.
            var other = npcs.FirstOrDefault(npc => npc != chosen);
            Assert.That(other, Is.Not.Null, "A second houseguest to stand away.");
            var scope = ScopeOf(director);
            int away = Rs4FarRoom(scope, chosen.transform.position, player.transform.position);
            ScopePlace(other.transform, ScopeOpenFloor(scope, away), 2f);
            var owner = new object();
            director.HoldRoomScope(owner);
            try
            {
                yield return null;
                Assert.That(director.IsBodyDrawn(chosen.transform), Is.True, "The followed houseguest is drawn.");
                Assert.That(chosen.BodyHidden, Is.False, "and keeps their plate under the spotlight.");
                Assert.That(npcs.Any(npc => npc.BodyHidden), Is.True,
                    other.Id + ", in the " + RoomLayers.RoomIds[away] + ", away from the followed one's room: somebody's plate is held down.");
                foreach (var npc in npcs)
                {
                    bool hidden = !director.IsBodyDrawn(npc.transform);
                    Assert.That(npc.BodyHidden, Is.EqualTo(hidden), npc.Id + "'s plate goes with their body.");
                    if (hidden) Assert.That(npc.PlateAlpha, Is.Zero, npc.Id + " is hidden: no plate.");
                }
            }
            finally { director.Release(owner); }
            // Let go, the follow is still the room view (RS5: following is live, so with nothing held the view draws
            // the followed one's room): the plates go with the bodies, and whoever stands outside it keeps theirs
            // down. Only the whole house - the setting's view (D12), here a test's hold - brings every plate back.
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Following), "Let go, the view is the follow's own,");
            Assert.That(director.IsBodyDrawn(chosen.transform), Is.True, "the followed houseguest drawn,");
            Assert.That(npcs.Any(npc => npc.BodyHidden), Is.True,
                other.Id + ", in the " + RoomLayers.RoomIds[away] + ", is still left out by the follow's room view: a plate stays down with nothing held.");
            foreach (var npc in npcs)
                Assert.That(npc.BodyHidden, Is.EqualTo(!director.IsBodyDrawn(npc.transform)), npc.Id + "'s plate goes with their body, nothing held.");
            RoomViewHoldTheDollhouse();
            Assert.That(npcs.Any(npc => npc.BodyHidden), Is.False, "The whole house held, no plate is held down by the scope.");
            yield return RoomViewLetGoOfTheDollhouse();

            if (Application.isBatchMode) yield return CaptureFraming("followed");
            director.FollowHouseguest(chosen.Id);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NamePlates_UseGivenNameForTitledHouseguestAndPreserveIdentity()
        {
            yield return null;
            var npc = SceneComponents<HouseNpc>().Single(person => person.Id == ContentCatalog.MayaId);
            string originalId = npc.Id, originalName = npc.DisplayName;
            try
            {
                npc.Configure("dr-will-kirby", "Dr. Will Kirby");
                Assert.That(npc.Id, Is.EqualTo("dr-will-kirby"), "The displayed given name does not replace the character identity.");
                Assert.That(npc.DisplayName, Is.EqualTo("Dr. Will Kirby"), "The full name retains its title.");
                Assert.That(Plate(npc), Is.Not.Null);
                var labels = Plate(npc).GetComponentsInChildren<TMPro.TMP_Text>(true).Select(label => label.text).ToArray();
                Assert.That(labels, Does.Contain("Will").And.Not.Contain("Dr."), "The name over the houseguest is Will.");
                Assert.That(npc.GetComponentInChildren<TextMesh>(true).text, Is.EqualTo("Dr. Will Kirby"),
                    "The underlying full-name holder remains unchanged for existing readers.");
            }
            finally
            {
                npc.Configure(originalId, originalName);
            }
        }

        /// <summary>
        /// Mockup-12 keeps the name of the one you are talking to over their head. The two-shot
        /// frames the pair's faces in the upper third, and a plate at its dollhouse height went up
        /// with them into the top bar's band, behind the chips; close up it comes down to the head.
        /// And it is drawn: the conversation's stage is a scrim the pair are seen through, not a
        /// card, so the chrome's gate leaves the one you are talking to their name (UI-UX-PASS-PLAN
        /// H0), while anybody else whose plate falls under the conversation's column or its dial is down.
        /// </summary>
        [UnityTest]
        public IEnumerator NamePlates_TheOneYouAreTalkingToWearsTheirNameUnderTheTopBar()
        {
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            yield return OpenNearbyNpc(maya);
            Assert.That(LastActive(EpisodeHud.DialName), Is.Not.Null, "The conversation opens on its topic wheel.");
            Assert.That(LastActive(EpisodeHud.ConversationColumnName), Is.Null, "The root leaves the actors clear of an options column.");
            yield return EnterConversationCategory(EpisodeDirector.ConnectCategoryCaption);
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

            Assert.That(maya.PlateAlpha, Is.GreaterThan(.01f), "Maya's plate is drawn over the conversation, not taken down under its stage.");
            var column = LastActive(EpisodeHud.ConversationColumnName);
            var dial = LastActive(EpisodeHud.DialName);
            Assert.That(column, Is.Not.Null, "The conversation has its column.");
            foreach (var other in SceneComponents<HouseNpc>().Where(npc => npc != maya && npc.gameObject.activeInHierarchy))
            {
                if (!other.TryPlateScreenRect(eye, out var plate)) continue;
                // Well under, not grazing an edge: a plate at the column's edge is the frame before's call.
                var inner = Rect.MinMaxRect(plate.xMin + 4f, plate.yMin + 4f, plate.xMax - 4f, plate.yMax - 4f);
                bool under = ScreenRect(column).Overlaps(inner) || (dial != null && ScreenRect(dial).Overlaps(inner));
                if (under) Assert.That(other.PlateAlpha, Is.LessThan(.01f), other.DisplayName + "'s plate at " + plate + " is under the conversation's column or dial, and down.");
            }
            director.ClosePanels();
            yield return null;
        }

        private static RectTransform Plate(HouseNpc npc) =>
            npc.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(rect => rect.name == HouseNpc.PlateName);
    }
}
