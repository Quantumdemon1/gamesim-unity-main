using System.Collections;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// A storm-off walked (the art brief's R6): the one the house turned on plays the beat, and once
    /// the fallout's card has gone walks to a room away from the row - their own, or the Bedroom, or
    /// the Yard (decision D4) - stands with their back to it a while, and is the house's again. Asked
    /// with the house's world live, the clock pinned, and the scene's card left open, so the house's
    /// own conversations hold still and only the storm moves.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private string stormTarget, stormFrom;

        private HouseMeetingCoordinator StormCoordinator() =>
            (HouseMeetingCoordinator)typeof(EpisodeDirector).GetField("npcMeetings", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(director);

        /// <summary>The house turns on somebody, the player answers, and the fallout's card is cut short.</summary>
        private IEnumerator TurnTheHouse(bool storms)
        {
            Application.runInBackground = true;
            Time.captureDeltaTime = 1f / 30f;
            yield return InstallTheHouseTurnsFixture(suspend: false);
            director.StormOffsInBatchRuns = storms;
            director.StormSulkSeconds = 1.5f;
            director.BuildNpcWorldForDiagnostics();
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
            for (int frame = 0; frame < 300 && !director.NpcAutonomyReady; frame++) yield return null;
            Assert.That(director.NpcAutonomyReady, Is.True, "The house's world is up.");
            var beat = EpisodeEngine.OpenStoryBeats(director.Snapshot).Single(item => item.contentId == "the-house-turns:turns");
            stormTarget = beat.cast.Single(part => part.role == "TARGET").contestantId;
            stormFrom = EpisodeDirector.VenueRoom(beat.venue);
            Assert.That(director.OpenSceneCard(beat.id), Is.True, "The meeting opens where the player stands.");
            yield return null;
            var option = beat.choices.First(c => !c.locked && !c.pickPerson && !c.conduct && !c.costsAction);
            ButtonWithCaption(EpisodeHud.EventChoiceCaption(option.label)).onClick.Invoke();
            yield return Frames(2);
            Assert.That(StoryBody(stormTarget).LastReaction, Is.EqualTo(CharacterPresentation.Reaction.StormOff),
                "The one the house turned on storms off.");
            Assert.That(director.StormingId, Is.Null, "Nobody walks while the fallout's card is up.");
            foreach (var card in SceneComponents<CeremonyTakeover>()) card.Cancel();
        }

        /// <summary>
        /// What on the furniture layer (8) a body standing at <paramref name="feet"/> would overlap -
        /// its own capsule, five centimetres off the floor as the ceremony marks ask it, its own
        /// colliders aside - or null for nothing.
        /// </summary>
        private static string FurnitureAround(Vector3 feet, Transform body)
        {
            var capsule = body.GetComponent<CapsuleCollider>();
            Assert.That(capsule, Is.Not.Null, body.name + " has a capsule.");
            Physics.SyncTransforms();
            var hits = new Collider[32];
            float radius = capsule.radius;
            int count = body.gameObject.scene.GetPhysicsScene().OverlapCapsule(feet + Vector3.up * (radius + .05f),
                feet + Vector3.up * Mathf.Max(radius + .05f, capsule.height - radius), radius, hits,
                1 << HouseLayers.Furniture, QueryTriggerInteraction.Ignore);
            Assert.That(count, Is.LessThan(hits.Length), "The furniture query overflowed; occupancy is unknown.");
            var names = hits.Take(count).Where(hit => hit != null && !hit.transform.IsChildOf(body))
                .Select(hit => (hit.transform.parent != null ? hit.transform.parent.name + "/" : "") + hit.name).ToList();
            return names.Count == 0 ? null : string.Join(", ", names);
        }

        // The storm's beats wait on the unscaled clock, which the pinned frame time does not move: a
        // batchmode frame is a fraction of a millisecond, so these waits are in real seconds.
        private IEnumerator UntilTheStormWalks()
        {
            yield return WaitFor(() => director.StormNow == EpisodeDirector.StormStage.Walking, 5f, "The walk began once the card had gone.");
            Assert.That(director.StormingId, Is.EqualTo(stormTarget));
            Assert.That(director.StormLeaseHeld, Is.True, "The house holds the walk by a storm lease of its own.");
        }

        /// <summary>
        /// The whole storm: the beat, the walk under its own lease to the room decision D4 names, the
        /// hips arriving on that room's floor, the sulk, and the body the house's again.
        /// </summary>
        [UnityTest]
        public IEnumerator StormOff_TheOneTheHouseTurnedOnStormsToARoomOfTheirOwnAndSulks()
        {
            try
            {
                yield return TurnTheHouse(storms: true);
                yield return UntilTheStormWalks();
                string expected = EpisodeDirector.StormRoomFor(director.Snapshot.Find(stormTarget).homeRoom, stormFrom);
                Assert.That(director.StormRoom, Is.EqualTo(expected), "Their own room, or the Bedroom, or the Yard, away from the " + stormFrom + ".");
                Assert.That(expected, Is.Not.EqualTo(stormFrom), "Never the room the row was in.");
                yield return WaitFor(() => director.StormNow != EpisodeDirector.StormStage.Walking, 60f, "The walk ends.");
                Assert.That(director.StormNow, Is.EqualTo(EpisodeDirector.StormStage.Sulking), "They arrived and sulk.");
                var body = ActBody(stormTarget);
                Assert.That(RoomAt(body), Is.EqualTo(expected), "Their hips stand on the " + expected + "'s floor.");
                // The clearance the walk asks reads the sight layers, which leave the furniture out.
                Assert.That(FurnitureAround(body.position, body), Is.Null,
                    "Their arrived capsule overlaps nothing on the furniture layer, at " + body.position.ToString("F2") + ".");
                Assert.That(director.StormLeaseHeld, Is.True, "The house holds them through the sulk.");
                yield return WaitFor(() => director.StormNow == EpisodeDirector.StormStage.None, 6f, "The sulk ends,");
                Assert.That(director.AnyStormLease, Is.False, "and the body is the house's again.");
            }
            finally { Time.captureDeltaTime = 0f; }
        }

        /// <summary>A ceremony's summons has a better claim than a storm: it takes the body mid-walk, and the storm is over.</summary>
        [UnityTest]
        public IEnumerator StormOff_ACeremonysSummonsTakesTheBodyMidWalk()
        {
            try
            {
                yield return TurnTheHouse(storms: true);
                yield return UntilTheStormWalks();
                yield return Frames(10);
                var coordinator = StormCoordinator();
                Assert.That(coordinator.IsStorming(stormTarget), Is.True);
                var at = ActBody(stormTarget).position;
                // The nearest place a ceremony could summon them to that has a route: the first
                // summons takes them from the storm, whether or not its route is found.
                bool joined = false;
                string reason = null;
                foreach (var place in SceneComponents<HouseInteractionAnchor>()
                    .Where(anchor => anchor.isActiveAndEnabled && anchor.gameObject.scene == director.gameObject.scene)
                    .OrderBy(anchor => (anchor.transform.position - at).sqrMagnitude).Take(12))
                    if (coordinator.JoinCeremonyStage(stormTarget, place, out reason)) { joined = true; break; }
                Assert.That(joined, Is.True, "A ceremony summoned them: " + reason);
                Assert.That(coordinator.IsStorming(stormTarget), Is.False, "The summons took them from the storm.");
                yield return null;
                Assert.That(director.StormingId, Is.Null, "and the storm is over.");
                Assert.That(director.AnyStormLease, Is.False);
                coordinator.EndCeremonyStage();
                yield return Frames(30);
                Assert.That(director.StormingId, Is.Null, "It does not start again once the ceremony lets go.");
            }
            finally { Time.captureDeltaTime = 0f; }
        }

        /// <summary>
        /// A storm commits nothing: with the scene's card open the house's own conversations hold, and
        /// the revision does not move while the storm walks. A reload then drops it whole: no storm,
        /// no storm lease.
        /// </summary>
        [UnityTest]
        public IEnumerator StormOff_CommitsNothingAndAReloadDropsIt()
        {
            try
            {
                yield return TurnTheHouse(storms: true);
                yield return UntilTheStormWalks();
                int revision = director.Snapshot.revision;
                yield return Frames(60);
                Assert.That(director.StormNow, Is.Not.EqualTo(EpisodeDirector.StormStage.None), "Still storming.");
                Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "The storm commits nothing.");
                yield return ReloadEpisode();
                yield return Frames(3);
                Assert.That(director.StormingId, Is.Null, "A reload drops the storm.");
                Assert.That(director.AnyStormLease, Is.False, "and leaves no storm lease.");
            }
            finally { Time.captureDeltaTime = 0f; }
        }

        /// <summary>A batch run walks no storms unless it asks: the beat plays, and nobody leaves the room.</summary>
        [UnityTest]
        public IEnumerator StormOff_IsOffInBatchRunsByDefault()
        {
            try
            {
                Assert.That(director.StormOffsInBatchRuns, Is.False, "Off unless a test asks.");
                yield return TurnTheHouse(storms: false);
                // Past the beat's 0.9 s, when a storm would have set off.
                float until = Time.realtimeSinceStartup + 1.5f;
                while (Time.realtimeSinceStartup < until) { Assert.That(director.StormingId, Is.Null, "No storm begins."); yield return null; }
                Assert.That(director.StormingId, Is.Null, "No storm walked.");
                Assert.That(director.AnyStormLease, Is.False, "and no storm lease was taken.");
            }
            finally { Time.captureDeltaTime = 0f; }
        }

        /// <summary>
        /// A storm's spot is never in furniture - the clearance the walk asks reads the sight layers,
        /// which leave the furniture out: not in the house's own, in any room a storm can be sent to,
        /// for anybody; and not in a seat cloned at run time on the spot it picked, which the baked
        /// NavMesh has no hole under.
        /// </summary>
        [UnityTest]
        public IEnumerator StormOff_PicksNoSpotInsideFurnitureEvenFurnitureClonedAtRunTime()
        {
            yield return SettleCast();
            var state = director.Snapshot;
            var bodies = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy && npc.Id != state.playerId)
                .OrderBy(npc => npc.Id, System.StringComparer.Ordinal).ToList();
            Assert.That(bodies, Is.Not.Empty, "Houseguests to storm off.");
            // Every room a storm can be sent to: a home room, the Bedroom or the Yard (decision D4) -
            // never the Private or the Head of Household's room.
            var rooms = ActiveRoomMarkers().Select(marker => marker.RoomName).Where(room => room != "Private" && room != "HoH")
                .Distinct().OrderBy(room => room, System.StringComparer.Ordinal).ToList();
            Assert.That(rooms, Does.Contain("Bedroom").And.Contain("Yard"), "The rooms a storm goes to by decision D4.");
            int picked = 0;
            foreach (var npc in bodies)
                foreach (var room in rooms)
                {
                    if (!director.StormSpotForDiagnostics(npc.Id, room, out var spot)) continue;
                    picked++;
                    string inside = FurnitureAround(spot, npc.transform);
                    Assert.That(inside, Is.Null, npc.Id + "'s storm to the " + room + " is sent to " + spot.ToString("F2") + ", inside " + inside + ".");
                }
            Assert.That(picked, Is.GreaterThan(0), "Some storm had somewhere to go.");

            // A seat cloned at run time on the spot: furniture layer, and no hole in the NavMesh under it.
            var who = bodies[0];
            Assert.That(director.StormSpotForDiagnostics(who.Id, "Bedroom", out var first), Is.True, "A storm to the Bedroom has somewhere to go.");
            var seat = new GameObject("storm-off-test-seat");
            SceneManager.MoveGameObjectToScene(seat, director.gameObject.scene);
            seat.layer = HouseLayers.Furniture;
            seat.transform.position = first + Vector3.up * .5f;
            seat.AddComponent<BoxCollider>().size = new Vector3(.9f, 1f, .9f);
            try
            {
                yield return null;
                Assert.That(director.StormSpotForDiagnostics(who.Id, "Bedroom", out var next), Is.True, "The Bedroom still offers a spot.");
                string inside = FurnitureAround(next, who.transform);
                Assert.That(inside, Is.Null, "Nobody is sent into the seat cloned at " + first.ToString("F2") + ": sent to "
                    + next.ToString("F2") + ", inside " + inside + ".");
            }
            finally { Object.Destroy(seat); }
        }

        /// <summary>
        /// A storm with no route to its spot takes nobody from what they were doing: the houseguest
        /// out on a stroll is refused before the stroll is let go, and walks on it as before.
        /// </summary>
        [UnityTest]
        public IEnumerator StormOff_WithNoRouteThereTakesNobodyFromTheirStroll()
        {
            try
            {
                Application.runInBackground = true;
                Time.captureDeltaTime = 1f / 30f;
                director.BuildNpcWorldForDiagnostics();
                for (int frame = 0; frame < 300 && !director.NpcAutonomyReady; frame++) yield return null;
                Assert.That(director.NpcAutonomyReady, Is.True, "The house's world is up.");
                var coordinator = StormCoordinator();
                // Somebody out on a stroll: the house's own, or one sent a step away. All in one frame,
                // so nothing of the house's moves between the stroll and the storm.
                HouseNpc walker = null;
                foreach (var npc in SceneComponents<HouseNpc>().Where(each => each.gameObject.activeInHierarchy && each.Id != director.Snapshot.playerId)
                    .OrderBy(each => each.Id, System.StringComparer.Ordinal))
                {
                    if (coordinator.IsWandering(npc.Id)) { walker = npc; break; }
                    bool sent = false;
                    foreach (float step in new[] { 1.5f, 2.5f, 1f })
                        if (coordinator.TryWander(npc.Id, PointBeside(npc.transform.position, step))) { sent = true; break; }
                    if (sent) { walker = npc; break; }
                }
                Assert.That(walker, Is.Not.Null, "Somebody is out on a stroll.");
                var motion = walker.GetComponent<HouseNpcMotion>();
                string stroll = motion.LeaseId;
                var going = motion.ReservedDestination;
                Assert.That(stroll, Does.StartWith("wander:"), "Their route is the stroll's.");
                // Off every floor: nobody can walk there.
                var nowhere = new Vector3(500f, 0f, 500f);
                Assert.That(coordinator.TryStormOff(walker.Id, nowhere, EpisodeDirector.StormSpeed), Is.False, "No route there, no storm.");
                Assert.That(coordinator.StormingActorId, Is.Null, "Nobody is storming,");
                Assert.That(coordinator.IsWandering(walker.Id), Is.True, walker.Id + "'s stroll is still theirs,");
                Assert.That(motion.LeaseId, Is.EqualTo(stroll), "on the same route,");
                Assert.That(Flat(motion.ReservedDestination, going), Is.LessThan(.01f), "to the same place.");
            }
            finally { Time.captureDeltaTime = 0f; }
        }

        /// <summary>
        /// A walk that runs out of time short of its spot stops where it stands: the sulk is stood,
        /// not walked. The route they hold ends at their own feet, they stay put through the sulk,
        /// and the storm still holds them.
        /// </summary>
        [UnityTest]
        public IEnumerator StormOff_AWalkOutOfTimeStopsWhereItStandsToSulk()
        {
            try
            {
                yield return TurnTheHouse(storms: true);
                // Out of time on the walk's first tick: metres short of any spot in another room.
                director.StormWalkLimitSeconds = 0f;
                yield return UntilTheStormWalks();
                var body = ActBody(stormTarget);
                var motion = body.GetComponent<HouseNpcMotion>();
                yield return WaitFor(() => director.StormNow != EpisodeDirector.StormStage.Walking, 5f, "The walk runs out of time.");
                Assert.That(director.StormNow, Is.EqualTo(EpisodeDirector.StormStage.Sulking), "They sulk where the time ran out.");
                Assert.That(director.StormLeaseHeld, Is.True, "The storm still holds them,");
                var stoodAt = body.position;
                float onward = Flat(motion.ReservedDestination, stoodAt);
                Assert.That(onward, Is.LessThan(.3f), "and the route they hold ends at their own feet, not at the spot "
                    + onward.ToString("F2") + " m on.");
                // A game second at the pinned 1/30 s: the storm's 2.6 m/s would carry a walking body 2.6 m.
                yield return Frames(30);
                Assert.That(director.StormNow, Is.EqualTo(EpisodeDirector.StormStage.Sulking), "Still sulking,");
                float moved = Flat(body.position, stoodAt);
                Assert.That(moved, Is.LessThan(.5f), "they stand where they stopped rather than walk on: moved " + moved.ToString("F2") + " m.");
            }
            finally { Time.captureDeltaTime = 0f; }
        }
    }
}
