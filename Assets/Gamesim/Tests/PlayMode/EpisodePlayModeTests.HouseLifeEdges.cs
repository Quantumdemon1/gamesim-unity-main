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
    /// The edges of house life a review found: a refused place, a walk interrupted by a nap, the
    /// menu over a sleeper, and the lens across a warp.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// A place that turns out to be unusable moves nobody. The warp to it used to happen before
        /// the place was checked, so a refusal left the player across the house for nothing.
        /// </summary>
        [UnityTest]
        public IEnumerator HouseLife_ARefusedPlaceMovesNobody()
        {
            director.ClosePanels();
            yield return null;
            var pool = PlacesFor(HouseFurnitureActivity.Swim).Single();
            WarpPlayer(FarthestRoomFrom(pool.Approach));
            yield return null;
            // Somebody standing where the player would climb in.
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            obstacle.name = "Somebody on the steps";
            obstacle.transform.position = pool.Approach + Vector3.up;
            Physics.SyncTransforms();
            try
            {
                var before = player.transform.position;
                director.StartActivityInHouse(pool, HouseFurnitureActivity.Swim);
                Assert.That(director.PlayerActivity, Is.Null, "The place is refused.");
                Assert.That(Vector3.Distance(player.transform.position, before), Is.LessThan(.01f), "And nobody was moved for it.");
                Assert.That(director.IsTravelDipShowing, Is.False, "Nor did the screen blink.");
                Assert.That(director.StatusMessage, Does.Contain("obstructed"));
            }
            finally { Object.Destroy(obstacle); }
            yield return null;
        }

        /// <summary>
        /// Getting up does not set off after the walk the activity interrupted: that errand was
        /// given up when the player chose the stove.
        /// </summary>
        [UnityTest]
        public IEnumerator HouseLife_GettingUpDoesNotResumeAnOldWalk()
        {
            director.ClosePanels();
            yield return null;
            var stove = PlacesFor(HouseFurnitureActivity.Cook).Single();
            WarpPlayer(OnFootFrom(stove.Approach, 3f, 8f));
            yield return null;
            Assert.That(player.TryMoveTo(OnFootFrom(player.transform.position, 6f, 12f)), Is.True, "A walk is under way.");
            yield return null;
            player.GetComponent<Gamesim.Presentation.CharacterPresentation>().SetReducedMotion(false);
            player.Agent.speed = 20; player.Agent.acceleration = 100;
            director.StartActivityInHouse(stove, HouseFurnitureActivity.Cook);
            Assert.That(director.PlayerActivity, Is.EqualTo(HouseFurnitureActivity.Cook), director.StatusMessage);
            float deadline = Time.realtimeSinceStartup + 10f;
            var pose = player.GetComponent<HouseFurniturePose>();
            while (Time.realtimeSinceStartup < deadline && !(pose != null && pose.IsPerforming)) yield return null;
            director.FinishPlayerHouseActivity();
            deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline && director.IsPlayerHouseActivityActive) yield return null;
            yield return null;
            Assert.That(player.Agent.hasPath, Is.False, "The player stands where they got up, not off after the old walk.");
        }

        /// <summary>The House Activities menu, opened and closed over a sleeping player, leaves them asleep.</summary>
        [UnityTest]
        public IEnumerator HouseLife_TheMenuDoesNotWakeTheSleeper()
        {
            var bed = PlacesFor(HouseFurnitureActivity.Sleep).First(anchor => anchor.transform.parent.name == "bedSingle");
            yield return BeginInHouse(bed, HouseFurnitureActivity.Sleep);
            var seat = player.GetComponent<HouseSeatPresentation>();
            director.OpenHouseActivities();
            yield return null;
            Assert.That(seat.IsExiting, Is.False, "Opening the menu does not get them up.");
            director.ClosePanels();
            yield return null;
            Assert.That(seat.IsExiting, Is.False, "Nor does closing it: the menu did not put them to bed.");
            float later = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < later) yield return null;
            Assert.That(director.PlayerActivity, Is.EqualTo(HouseFurnitureActivity.Sleep));
            director.FinishPlayerHouseActivity();
            yield return null;
        }

        /// <summary>
        /// A new body arriving while the player climbs out - the change back out of the swimwear
        /// starts as they get up, and often finishes on the way - climbs on from where the old one
        /// had got to. The climb used to stop dead at the swap and the body jump to the deck.
        /// </summary>
        [UnityTest]
        public IEnumerator HouseLife_ANewBodyMidClimbClimbsOn()
        {
            var seats = PlacesFor(HouseFurnitureActivity.Soak);
            yield return BeginInHouse(seats[0], HouseFurnitureActivity.Soak);
            var presentation = player.GetComponent<Gamesim.Presentation.CharacterPresentation>();
            float deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline && presentation.IsChangingOutfit) yield return null;
            var seat = player.GetComponent<HouseSeatPresentation>();
            Assert.That(seat.Settled, Is.True, "In the tub.");

            director.FinishPlayerHouseActivity();
            Assert.That(seat.IsExiting, Is.True, "Climbing out.");
            yield return null;
            // The new body arrives in the middle of the climb: attached outright here, which swaps
            // the body in the same call, where a change of clothes swaps it whenever it is ready.
            var shown = presentation.VisualRoot;
            var state = director.Snapshot;
            Gamesim.Presentation.CharacterPresentation.Attach(player.gameObject,
                Gamesim.Presentation.CharacterOutfits.ForPhase(state.Find(state.playerId), state.phase), Color.white).SetReducedMotion(false);
            Assert.That(presentation.VisualRoot, Is.Not.SameAs(shown), "A new body, mid-climb.");
            yield return null;
            Assert.That(seat.IsExiting, Is.True, "The new body climbs on: the climb is not cut short.");
            Assert.That(director.IsPlayerHouseActivityActive, Is.True, "Still getting out.");
            var body = presentation.VisualRoot;
            Assert.That(Vector3.Distance(Flat(body.position), Flat(player.transform.position)), Is.GreaterThan(.1f),
                "From where the old body had got to, still over the water - not jumped to the deck.");
            deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline && director.IsPlayerHouseActivityActive) yield return null;
            Assert.That(director.IsPlayerHouseActivityActive, Is.False, "And the climb finishes.");
            Assert.That(Vector3.Distance(Flat(presentation.VisualRoot.position), Flat(player.transform.position)), Is.LessThan(.05f),
                "On the deck, where the player stands.");
        }

        private static Vector3 Flat(Vector3 point) => new Vector3(point.x, 0f, point.z);

        /// <summary>
        /// A warp from the map cuts the lens as well as the pivot: the overview's orthographic
        /// morph does not go on easing out on the far side of the cut.
        /// </summary>
        [UnityTest]
        public IEnumerator Overview_AWarpFromTheMapLeavesNoLensBehind()
        {
            director.ClosePanels();
            yield return null;
            Assert.That(director.ShowOverview(), Is.True);
            float deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline && cameraRig.LensOrthographic < .995f) yield return null;
            var far = SceneComponents<HouseRoomMarker>().Where(marker => marker.RoomName != "Private" && marker.RoomName != director.ScreenRoom)
                .OrderByDescending(marker => RouteMetres(player.transform.position, marker.transform.position)).First();
            director.PressTravelBeacon(far.RoomName);
            Assert.That(director.LastTravel, Is.EqualTo(EpisodeDirector.TravelKind.Warp), "The far room is a warp away.");
            yield return null;
            Assert.That(cameraRig.LensOrthographic, Is.Zero, "The lens is cut with the rest of the camera.");
        }
    }
}
