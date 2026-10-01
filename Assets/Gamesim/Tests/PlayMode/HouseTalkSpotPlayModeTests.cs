using System.Collections;
using System.Linq;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The talk spots in the meeting coordinator (PACK8-PASS-PLAN C3), on the preserved prototype,
    /// which has no gallery and no long table, so the one spot here is authored by the test on the
    /// living room's own floor. A new pair sits when it can; a saved one goes back to its venue's
    /// own pair first and to another of its places while that pair is gone; a venue holds one
    /// conversation whichever of its places it is at; and the player's talk sends one houseguest
    /// to a place that nobody else can take, through the house's pause, until it is let go.
    /// </summary>
    public sealed partial class HouseNpcMotionPlayModeTests
    {
        /// <summary>
        /// A seat pair for the living room's venue on the venue's own floor: the two seats where
        /// its two marks are, so the walks to both are the same and only the seats tell them apart.
        /// </summary>
        private (GameObject couch, string id) AuthorTestSpotOverTheLivingPair(HouseRoomQuery query)
        {
            Assert.That(HouseInteractionAnchors.TryFind(query.Scene, HouseConversationSpots.LivingFamily, 0, out var a), Is.True);
            Assert.That(HouseInteractionAnchors.TryFind(query.Scene, HouseConversationSpots.LivingFamily, 1, out var b), Is.True);
            var couch = new GameObject("Test conversation couch");
            SceneManager.MoveGameObjectToScene(couch, query.Scene);
            string id = HouseConversationSpots.IdFor(HouseConversationSpots.LivingFamily, "test");
            HouseInteractionAnchor.Create(couch.transform, id, a.RoomId, 0, a.Approach, a.Facing, true);
            HouseInteractionAnchor.Create(couch.transform, id, b.RoomId, 1, b.Approach, b.Facing, true);
            return (couch, id);
        }

        /// <summary>Every venue but the living room's out of the way, so the choice is between its own pair and the test's seats.</summary>
        private static void SetAsideEveryOtherVenue(HouseRoomQuery query)
        {
            foreach (var venue in HouseInteractionAnchors.Meetings.Where(venue => venue.Id != HouseConversationSpots.LivingFamily))
                for (int slot = 0; slot < 2; slot++)
                    if (HouseInteractionAnchors.TryFind(query.Scene, venue.Id, slot, out var anchor)) anchor.gameObject.SetActive(false);
        }

        [UnityTest]
        public IEnumerator Meeting_ANewPairSitsWhenItCanAndASavedOneGoesBackToItsOwnPairFirst()
        {
            var query = CreateNpcRoomQuery();
            var cast = CreateMeetingCast(4);
            var ids = cast.Select(npc => npc.Id).ToArray();
            var (couch, spotId) = AuthorTestSpotOverTheLivingPair(query);
            // The coordinator reads the house's places once, when it is made.
            var coordinator = CreateMeetingCoordinator(query);
            try
            {
                SetAsideEveryOtherVenue(query);
                Assert.That(coordinator.Reconcile("talk-spots:1", cast, ids, ids, out var reason), Is.True, reason);
                yield return WaitForMeetingBinding(coordinator, cast);

                Assert.That(coordinator.TryReservePair("fresh", ids[0], ids[1], out var fresh, out reason), Is.True, reason);
                Assert.That(fresh.SpotId, Is.EqualTo(spotId), "Two seats as near as two marks: the pair sits.");
                Assert.That(fresh.VenueId, Is.EqualTo(HouseConversationSpots.LivingFamily), "and the conversation is the living room's, as a save names it.");
                Assert.That(fresh.Seated, Is.True);
                Assert.That(coordinator.TryReserveAtVenue("other", ids[2], ids[3], HouseConversationSpots.LivingFamily, out _, out _), Is.False,
                    "A venue holds one conversation, whichever of its places it is at.");
                Assert.That(coordinator.TryGetSeat(ids[0], out var seat) && seat.VenueId == spotId, Is.True, "Its seats are the spot's.");
                Assert.That(coordinator.Release(fresh), Is.True);
                yield return null;

                Assert.That(coordinator.TryReserveAtVenue("saved", ids[0], ids[1], HouseConversationSpots.LivingFamily, out var saved, out reason), Is.True, reason);
                Assert.That(saved.SpotId, Is.EqualTo(HouseConversationSpots.LivingFamily),
                    "A saved conversation goes back to its venue's own pair first: which place it was at is not saved.");
                Assert.That(saved.Seated, Is.False);
                Assert.That(coordinator.Release(saved), Is.True);
                yield return null;

                Assert.That(HouseInteractionAnchors.TryFind(query.Scene, HouseConversationSpots.LivingFamily, 0, out var own), Is.True);
                own.gameObject.SetActive(false);
                Assert.That(coordinator.TryReserveAtVenue("saved-elsewhere", ids[0], ids[1], HouseConversationSpots.LivingFamily, out var moved, out reason), Is.True, reason);
                Assert.That(moved.SpotId, Is.EqualTo(spotId), "With its own pair gone, it is held at another of its venue's places,");
                Assert.That(moved.VenueId, Is.EqualTo(HouseConversationSpots.LivingFamily), "under the same saved name.");
            }
            finally { coordinator.Dispose(); Object.Destroy(couch); }
        }

        [UnityTest]
        public IEnumerator Talk_AHouseguestIsSentToAPlaceNobodyElseCanTakeUntilItIsLetGo()
        {
            var query = CreateNpcRoomQuery();
            var cast = CreateMeetingCast(2);
            var ids = cast.Select(npc => npc.Id).ToArray();
            var coordinator = CreateMeetingCoordinator(query);
            try
            {
                Assert.That(coordinator.Reconcile("talk:1", cast, ids, ids, out var reason), Is.True, reason);
                yield return WaitForMeetingBinding(coordinator, cast);
                WarpMeetingTestPlayer(query, new Vector3(-5.5f, 0, -6));

                Assert.That(coordinator.TryReserveTalkSpot(ids[0], player, out var spot, out reason), Is.True, reason);
                Assert.That(coordinator.Talk, Is.SameAs(spot));
                Assert.That(spot.NpcId, Is.EqualTo(ids[0]));
                Assert.That(HorizontalTestDistance(spot.NpcPlace.Approach, spot.PlayerPlace.Approach),
                    Is.InRange(HouseConversationSpots.RootsApart - .01f, HouseConversationSpots.PlayerReach + .01f),
                    "The two places stand apart and within talking reach.");
                Assert.That(HorizontalTestDistance(spot.PlayerStands, spot.PlayerPlace.Approach), Is.LessThan(.26f),
                    "The player's root waits at its place's approach.");
                var motion = cast[0].GetComponent<HouseNpcMotion>();
                Assert.That(motion.LeaseId, Does.StartWith("talk:"), "The houseguest is on their way on a token of the talk's own.");

                Assert.That(coordinator.TryReserveTalkSpot(ids[1], player, out _, out _), Is.False, "One talk at a time.");
                Assert.That(coordinator.TryReservePair("meeting", ids[0], ids[1], out _, out _), Is.False, "Nobody can pair them off on the way.");
                Assert.That(coordinator.Reconcile("talk:1", cast, ids, ids, out reason), Is.True,
                    "The house still owns them, through the talk: " + reason);
                Assert.That(motion.LeaseId, Is.EqualTo(spot.Token), "and keeps them on it.");

                // The house pauses for a panel; the person the player asked over still has to get there.
                coordinator.SetPaused(true);
                Assert.That(motion.State, Is.Not.EqualTo(HouseNpcMotionState.Paused), "The house's pause does not hold them.");
                float deadline = Time.realtimeSinceStartup + 25;
                while (!coordinator.TalkArrived(spot) && Time.realtimeSinceStartup < deadline)
                {
                    coordinator.SetPaused(true); coordinator.Tick();
                    yield return null;
                }
                Assert.That(coordinator.TalkArrived(spot), Is.True, "They reach their place: " + motion.ArrivalFailure);
                Assert.That(coordinator.TalkValid(spot), Is.True);

                coordinator.ReleaseTalk(spot);
                Assert.That(coordinator.Talk, Is.Null);
                Assert.That(spot.Released, Is.True);
                Assert.That(motion.LeaseId, Is.Null, "Let go, they are the house's again,");
                Assert.That(motion.State, Is.EqualTo(HouseNpcMotionState.Paused), "paused with it.");
                Assert.That(coordinator.TalkArrived(spot), Is.False);
            }
            finally { coordinator.Dispose(); }
        }
    }
}
