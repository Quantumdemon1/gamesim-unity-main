using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Camera Phase 4 (MASTER-PLAN §3.E): a timed move eases over exactly its duration, and a
    /// houseguest's name tag is there at a conversation's distance and gone from across the house.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Camera_ATimedMoveLandsOnItsClock()
        {
            cameraRig.SetReducedMotion(false);
            cameraRig.ControlsEnabled = false;
            var start = cameraRig.HouseCenter;
            cameraRig.MoveTo(start, 20f);
            yield return SettleCamera();
            var from = cameraRig.transform.position;
            var to = start + new Vector3(8f, 0f, 6f);
            cameraRig.MoveTo(to, 12f, 1.0f);
            Assert.That(cameraRig.IsTravelling, Is.True);
            float began = Time.unscaledTime;
            while (Time.unscaledTime - began < 0.5f) yield return null;
            float along = Vector3.Distance(from, cameraRig.transform.position) / Vector3.Distance(from, to);
            Assert.That(along, Is.InRange(0.3f, 0.7f), "Halfway through its second the move is about halfway there.");
            Assert.That(cameraRig.IsTravelling, Is.True);
            while (Time.unscaledTime - began < 1.15f) yield return null;
            Assert.That(cameraRig.IsTravelling, Is.False, "and it is over on the clock");
            Assert.That(Vector3.Distance(cameraRig.transform.position, to), Is.LessThan(0.05f), "exactly where it was sent.");
            cameraRig.ControlsEnabled = true;
        }

        [UnityTest]
        public IEnumerator Camera_ACeremonyFramesItsRoomAndComesBack()
        {
            cameraRig.SetReducedMotion(false);
            cameraRig.ControlsEnabled = false;
            var start = cameraRig.HouseCenter + new Vector3(-6f, 0f, -4f);
            cameraRig.MoveTo(start, 20f);
            yield return null;
            var focusBefore = cameraRig.DesiredFocus;
            float distanceBefore = cameraRig.DesiredDistance;
            var room = SceneComponents<HouseRoomMarker>().First(m => m.RoomName == EpisodeDirector.CeremonyRoom("nomination"));

            director.FrameCeremony("nomination");
            Assert.That(director.IsFramingCeremony, Is.True);
            var focus = cameraRig.DesiredFocus;
            Assert.That(Vector2.Distance(new Vector2(focus.x, focus.z), new Vector2(room.transform.position.x, room.transform.position.z)),
                Is.LessThan(0.1f), "A nomination is framed in the nomination room.");
            Assert.That(cameraRig.DesiredDistance, Is.EqualTo(EpisodeDirector.CeremonyDistance).Within(0.01f), "at the room-wide distance");
            Assert.That(cameraRig.IsTravelling, Is.True, "on the clock");

            float deadline = Time.realtimeSinceStartup + EpisodeDirector.CeremonyHoldSeconds + 3f;
            while (director.IsFramingCeremony && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.IsFramingCeremony, Is.False, "With no card playing the framing holds its minimum and lets go,");
            Assert.That(Vector3.Distance(cameraRig.DesiredFocus, focusBefore), Is.LessThan(0.05f), "back to where the viewer was");
            Assert.That(cameraRig.DesiredDistance, Is.EqualTo(distanceBefore).Within(0.05f));
            Assert.That(EpisodeDirector.CeremonyRoom("eviction"), Is.EqualTo("Living"));
            Assert.That(EpisodeDirector.CeremonyRoom("veto"), Is.EqualTo("Games"));
            Assert.That(EpisodeDirector.CeremonyRoom("arrival"), Is.Null, "A kind without a set frames nothing.");
            cameraRig.ControlsEnabled = true;
        }

        [UnityTest]
        public IEnumerator NameTags_ShowAtAConversationsDistanceAndFadeAcrossTheHouse()
        {
            cameraRig.ControlsEnabled = false;
            var npc = SceneComponents<HouseNpc>().First(n => n.gameObject.activeInHierarchy);
            var label = npc.GetComponentInChildren<TextMesh>(true);
            Assume.That(label, Is.Not.Null, "The houseguest carries a name label.");
            cameraRig.SetReducedMotion(true);
            cameraRig.MoveTo(npc.transform.position, cameraRig.NearestDistance);
            yield return null; yield return null;
            Assert.That(npc.NameTagAlpha, Is.EqualTo(1f).Within(0.01f), "Close up, the name is fully shown.");
            cameraRig.MoveTo(npc.transform.position, cameraRig.FarthestDistance);
            yield return null; yield return null;
            Assert.That(npc.NameTagAlpha, Is.LessThan(0.05f), "From the far end of the zoom it is gone.");
            Assert.That(label.color.a, Is.EqualTo(npc.NameTagAlpha).Within(0.01f), "and the label's colour carries it.");
            cameraRig.SetReducedMotion(false);
            cameraRig.ControlsEnabled = true;
        }
    }
}
