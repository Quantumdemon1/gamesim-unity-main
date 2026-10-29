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
        /// <summary>
        /// A timed move is where its clock says, eased, and over when its second is up.
        ///
        /// <para>The clock is the rig's: each LateUpdate from the one in the frame the move is sent
        /// in adds that frame's Time.unscaledDeltaTime, the sending frame's whole interval included.
        /// Read on Time.unscaledTime from the sending frame, a long frame before the move - a busy
        /// machine - put the move that far ahead of the test's half second: a 0.15 s frame there read
        /// 0.713 of the way, past the 0.7 allowed, and so did steady frames of 0.32 s (0.705); a long
        /// frame across the half second read the move a frame behind the wall clock (1.0 s there,
        /// none of the way). So the test keeps the rig's clock beside it, a frame behind as the
        /// position is (the coroutine resumes before LateUpdate), and asks for the eased position at
        /// that clock.</para>
        /// </summary>
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
            float clock = 0f;
            do { clock += Time.unscaledDeltaTime; yield return null; } while (clock < 0.5f);
            float along = Vector3.Distance(from, cameraRig.transform.position) / Vector3.Distance(from, to);
            Assert.That(along, Is.EqualTo(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(clock))).Within(0.05f),
                "Halfway through its second the move is about halfway there (" + along.ToString("0.###") + " of the way at "
                + clock.ToString("0.###") + " s of its clock).");
            Assert.That(cameraRig.IsTravelling, Is.EqualTo(clock < 1f), "It travels until its second is up (" + clock.ToString("0.###") + " s).");
            while (clock < 1f) { clock += Time.unscaledDeltaTime; yield return null; }
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
            // The veto meeting is held in the living room (PACK8-PASS-PLAN decision 5), staged or on the HUD frame.
            Assert.That(EpisodeDirector.CeremonyRoom("veto"), Is.EqualTo("Living"));
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
