using System;
using System.Collections;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The opening's front door, staged for real in the house: the cast placed behind a facade in
    /// the west yard, walked through the door one at a time, and put back where the season starts
    /// them when the show is skipped. Every wait is bounded in real seconds.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static readonly Vector3 RevealMarkForTests = new Vector3(-1.6f, 0f, 14.1f);
        private static readonly Vector3 DeckMarkForTests = new Vector3(-5.3f, 0f, 13.8f);
        private static readonly Vector3[] DoorEyes = { new Vector3(2.1f, 1.85f, 13.8f), new Vector3(1.1f, 1.85f, 13.8f) };

        private static IEnumerator WaitFor(Func<bool> condition, float seconds, string what)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(condition(), Is.True, what);
        }

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>
        /// Whether the line from an eye at the door shot to a point behind the facade is stopped by
        /// the set: by the facade itself, or - through the doorway - by the vestibule, whose far
        /// wall of light closes it. The set has no colliders, so this is geometry on its published
        /// measurements rather than a raycast.
        /// </summary>
        private static bool HiddenBehindTheFacade(Vector3 eye, Vector3 target)
        {
            if (target.x >= OpeningDoorSet.FacadeFrontX) return false;
            float t = (OpeningDoorSet.FacadeFrontX - eye.x) / (target.x - eye.x);
            var hit = eye + (target - eye) * t;
            bool onFacade = hit.z >= OpeningDoorSet.FacadeMinZ && hit.z <= OpeningDoorSet.FacadeMaxZ && hit.y >= 0f && hit.y <= OpeningDoorSet.FacadeHeight;
            bool inDoorway = hit.z > OpeningDoorSet.ApertureMinZ && hit.z < OpeningDoorSet.ApertureMaxZ && hit.y >= 0f && hit.y < OpeningDoorSet.ApertureHeight;
            if (onFacade && !inDoorway) return true;
            if (!inDoorway) return false;
            // Through the doorway, anything short of the far wall of light stands in the vestibule,
            // in view once the door opens. Anything beyond it is closed off: a line through the
            // doorway ends on the far wall, or leaves by a side flat or the ceiling before it.
            return target.x < OpeningDoorSet.VestibuleFarX;
        }

        /// <summary>
        /// The premiere through the front door: the house is placed behind the facade out of
        /// sight, the player comes through first, the door opens and they walk to the mark in front
        /// of the lens, and the first houseguest after them does the same and then walks off out of
        /// shot to where the house gathers. The frames are photographed for review.
        /// </summary>
        [UnityTest]
        public IEnumerator OpeningStage_TheFrontDoorRevealWalksAHouseguestIn()
        {
            var opening = director.Opening;
            var state = director.Snapshot;
            string playerId = state.playerId;
            string firstGuest = state.Active.First(person => !person.isPlayer).id;

            director.PlayOpeningForVerification(stage: true, holdHeadless: true);
            Assert.That(opening.IsPlaying, Is.True, "The opening plays.");
            yield return WaitFor(() => director.IsOpeningStaged, 40f, "The house is placed behind the front door once every body is built.");
            Assert.That(GameObject.Find(OpeningDoorSet.RootName), Is.Not.Null, "The front door is standing in the yard.");

            var waiting = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy).ToArray();
            Assert.That(waiting, Is.Not.Empty);
            foreach (var npc in waiting)
            {
                Assert.That(npc.transform.position.x, Is.LessThan(-5.5f), npc.DisplayName + " waits behind the facade.");
                foreach (var eye in DoorEyes)
                    foreach (float height in new[] { 0.1f, 1.7f })
                    {
                        var point = new Vector3(npc.transform.position.x, height, npc.transform.position.z);
                        Assert.That(HiddenBehindTheFacade(eye, point), Is.True,
                            npc.DisplayName + " cannot be seen waiting, from " + eye + " at " + height + " m.");
                    }
            }
            Assert.That(Flat(player.transform.position, DeckMarkForTests), Is.LessThan(0.6f), "The player waits on deck, first through the door.");

            yield return WaitFor(() => opening.CurrentGuestId == playerId, 30f, "The player is revealed first.");
            var set = Object.FindFirstObjectByType<OpeningDoorSet>();
            Assert.That(set, Is.Not.Null);
            if (Application.isBatchMode) yield return CaptureFraming("opening-reveal-closed", settle: false);
            yield return WaitFor(() => set.IsOpen, 12f, "The door opens for the player.");
            yield return WaitFor(() => set.Openness > 0.6f, 3f, "and swings wide.");
            if (Application.isBatchMode) yield return CaptureFraming("opening-reveal-open", settle: false);
            yield return WaitFor(() => Flat(player.transform.position, RevealMarkForTests) < 0.6f, 10f, "The player walks through the door to the mark.");
            if (Application.isBatchMode) yield return CaptureFraming("opening-reveal-mark", settle: false);

            yield return WaitFor(() => opening.CurrentGuestId == firstGuest, 15f, "The first houseguest follows the player.");
            var guest = SceneComponents<HouseNpc>().Single(npc => npc.Id == firstGuest);
            yield return WaitFor(() => Flat(guest.transform.position, RevealMarkForTests) < 0.6f, 15f, guest.DisplayName + " walks through the door to the mark.");
            float settle = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < settle) yield return null;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(guest.transform.eulerAngles.y, 90f)), Is.LessThan(35f), guest.DisplayName + " turns to the lens on the mark.");
            if (Application.isBatchMode) yield return CaptureFraming("opening-reveal-guest", settle: false);
            yield return WaitFor(() => player.transform.position.x > 3.0f, 20f, "The player walks off out of shot to where the house gathers, behind the camera.");

            opening.Skip();
            yield return null;
            yield return null;
            Assert.That(opening.IsPlaying, Is.False);
            Assert.That(GameObject.Find(OpeningDoorSet.RootName), Is.Null, "The front door comes down when the opening ends.");
            Assert.That(director.IsOpeningStaged, Is.False);
        }

        /// <summary>
        /// Skipping mid-reveal puts everybody back where the season starts them - behind black, by
        /// the load's own placement - and the introductions still play. The house is itself again
        /// once they are skipped too.
        /// </summary>
        [UnityTest]
        public IEnumerator OpeningStage_SkippingPutsEveryoneBackWhereTheSeasonStarts()
        {
            var opening = director.Opening;
            var home = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy)
                .ToDictionary(npc => npc.Id, npc => npc.transform.position);
            var playerHome = player.transform.position;
            string firstGuest = director.Snapshot.Active.First(person => !person.isPlayer).id;

            director.PlayOpeningForVerification(stage: true, holdHeadless: true);
            yield return WaitFor(() => director.IsOpeningStaged, 40f, "The house is placed behind the front door.");
            yield return WaitFor(() => opening.CurrentGuestId == firstGuest, 40f, "The reveals reach the first houseguest.");

            director.SkipOpening();
            yield return WaitFor(() => opening.IsMeeting, 10f, "Skipping the show stops at the introductions.");
            foreach (var npc in SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy))
                Assert.That(Flat(npc.transform.position, home[npc.Id]), Is.LessThan(0.6f), npc.DisplayName + " is back where the season started them.");
            Assert.That(Flat(player.transform.position, playerHome), Is.LessThan(0.6f), "and so is the player.");
            Assert.That(GameObject.Find(OpeningDoorSet.RootName), Is.Null, "The front door is gone.");
            CollectionAssert.AreEqual(OpeningBeat.InOrder.Take(4), director.Snapshot.openingBeatsSeen, "Every beat of the show is recorded; the introductions are not.");

            opening.SkipIntroductions();
            yield return WaitFor(() => !opening.IsPlaying, 5f, "Skipping the introductions ends the opening.");
            yield return WaitFor(() => director.NpcAutonomyReady, 5f, "The house binds its people again and carries on.");
        }
    }
}
