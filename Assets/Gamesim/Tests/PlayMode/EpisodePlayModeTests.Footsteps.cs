using System.Collections;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>The player's footsteps (HouseFootsteps): heard as they walk the house, on the ground of the room under them.</summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The ground the player's root covered in the last <see cref="WalkMeasuring"/>, in metres.</summary>
        private float footstepsWalked;

        /// <summary>
        /// A walk across the kitchen lands a foot every stride the house's walk implies (HouseSteps:
        /// 0.87 m at 2.2 m/s), every one of them on the kitchen's tile and heard; under Reduce sound
        /// the walk back lands its feet and none is heard. The agent moves on scaled time, so the frame
        /// is pinned at a thirtieth of a second and the walk's length is the root's own, measured
        /// each frame; a frame either way at the walk's ends is a step at most.
        /// </summary>
        [UnityTest]
        public IEnumerator Footsteps_AWalkAcrossTheKitchenLandsOnTileEveryStrideAndReduceSoundSilencesIt()
        {
            director.ClosePanels();
            yield return null;
            var audio = director.GetComponent<HouseAudio>();
            audio.SetMuted(false);
            audio.SetVolume(.35f);
            audio.SetChannelVolumes(1f, 1f, 1f);
            audio.SetReducedAudio(false);
            var steps = director.GetComponent<HouseFootsteps>();
            Assert.That(steps, Is.Not.Null, "The house listens for the player's feet.");
            Assert.That(HouseRoomQuery.TryCreate(player.gameObject.scene, out var rooms, out var why), Is.True, why);
            var stove = PlacesFor(HouseFurnitureActivity.Cook).Single().Approach;
            var start = InsideTheKitchen(rooms, stove);
            WarpPlayer(start);
            cameraRig.FocusSubject(player.transform);
            // The house's own walk, reached at once.
            player.Agent.speed = 2.2f;
            player.Agent.acceleration = 100f;
            Time.captureDeltaTime = 1f / 30f;
            try
            {
                // A third of a second standing: the warp is behind the clock, and the walk starts afresh.
                for (int i = 0; i < 10; i++) yield return null;
                int taken = steps.StepsTaken, heard = steps.StepsHeard, onTile = steps.StepsOn(StepSurface.Tile);
                yield return WalkMeasuring(stove);
                int landed = steps.StepsTaken - taken;
                float strides = footstepsWalked / HouseSteps.Stride(2.2f, player.IsRunning);
                Assert.That(landed, Is.InRange(Mathf.FloorToInt(strides) - 1, Mathf.CeilToInt(strides) + 1),
                    "A foot lands every stride: " + footstepsWalked.ToString("0.00") + " m is " + strides.ToString("0.0") + " strides.");
                Assert.That(landed, Is.GreaterThanOrEqualTo(3), "It was a walk, not a shuffle.");
                Assert.That(steps.StepsHeard - heard, Is.EqualTo(landed), "Every step is heard where the camera looks,");
                Assert.That(steps.StepsOn(StepSurface.Tile) - onTile, Is.EqualTo(landed), "every one on the kitchen's tile,");
                Assert.That(steps.LastClip, Does.StartWith("Tile_"));
                Assert.That(steps.LastVolume, Is.GreaterThan(0f), "and played.");

                audio.SetReducedAudio(true);
                taken = steps.StepsTaken; heard = steps.StepsHeard;
                yield return WalkMeasuring(start);
                Assert.That(steps.StepsTaken - taken, Is.GreaterThanOrEqualTo(3), "Under Reduce sound the feet still land,");
                Assert.That(steps.StepsHeard - heard, Is.EqualTo(0), "and none is heard.");
            }
            finally
            {
                Time.captureDeltaTime = 0f;
                audio.SetReducedAudio(false);
            }
        }

        /// <summary>A place in the kitchen four to seven metres on foot from <paramref name="to"/>, with the whole walk between them in the kitchen.</summary>
        private Vector3 InsideTheKitchen(HouseRoomQuery rooms, Vector3 to)
        {
            Assert.That(rooms.TryLocate(to, player.Agent.radius, out var end) && end == "Kitchen", Is.True, "The stove stands in the kitchen.");
            var path = new NavMeshPath();
            for (float radius = 7f; radius >= 4f; radius -= .5f)
                for (int step = 0; step < 16; step++)
                {
                    var candidate = to + Quaternion.Euler(0f, step * 22.5f, 0f) * Vector3.forward * radius;
                    if (!NavMesh.SamplePosition(candidate, out var hit, .5f, player.Agent.areaMask)) continue;
                    if (!rooms.TryLocate(hit.position, player.Agent.radius, out var room) || room != "Kitchen") continue;
                    if (!NavMesh.CalculatePath(hit.position, to, player.Agent.areaMask, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    float route = 0f;
                    bool inside = true;
                    for (int i = 0; i < path.corners.Length; i++)
                    {
                        if (!rooms.TryLocate(path.corners[i], .05f, out var at) || at != "Kitchen") inside = false;
                        if (i > 0) route += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                    }
                    if (inside && route >= 4f) return hit.position;
                }
            Assert.Fail("Nowhere in the kitchen is four metres on foot from the stove with the walk inside it.");
            return to;
        }

        /// <summary>Walks the player to <paramref name="to"/> and measures the ground their root covers on the way, in <see cref="footstepsWalked"/>.</summary>
        private IEnumerator WalkMeasuring(Vector3 to)
        {
            footstepsWalked = 0f;
            Assert.That(player.TryMoveTo(to), Is.True, "The walk is under way.");
            var last = player.transform.position;
            float deadline = Time.realtimeSinceStartup + 30f;
            // The walk, and three frames past its end for its last movement and the stop.
            int after = 0;
            while (after < 3 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                var now = player.transform.position;
                footstepsWalked += new Vector2(now.x - last.x, now.z - last.z).magnitude;
                last = now;
                after = player.HasArrived ? after + 1 : 0;
            }
            Assert.That(player.HasArrived, Is.True, "The player got there.");
        }
    }
}
