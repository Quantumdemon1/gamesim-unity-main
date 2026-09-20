using System.Linq;
using Gamesim.House;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The seated venues are code, the chairs are a scene: this is what keeps them the same thing.
    /// Every seated slot must stand on a placed authored seat and face the way that seat does, and
    /// its venue id must be one the simulation will accept in a save.
    /// </summary>
    public sealed class HouseSeatedVenueTests
    {
        private const string EpisodeScene = "Assets/Gamesim/Scenes/EpisodeHouse.unity";

        [Test]
        public void EverySeatedVenueSlotStandsOnAPlacedSeatFacingItsWay()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                // A placed prop is named for the id its plan row names, so the chairs answer to the
                // Poly Haven piece the row now asks for (V4); the authored twin's name is kept here
                // because a clone that rebuilds the house without the fetch places that one.
                var chairs = new[] { "bb_set_ph_diningchair", "bb_set_diningchair" };
                var seats = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Where(t => chairs.Contains(t.name) || t.name == "bb_set_lounger")
                    .ToArray();
                Assert.That(seats.Count(t => chairs.Contains(t.name)), Is.EqualTo(16), "Sixteen chairs at the long table.");
                Assert.That(seats.Count(t => t.name == "bb_set_lounger"), Is.EqualTo(3), "Three loungers by the pool.");

                var slots = HouseMeetingCoordinator.SeatedSlots.ToArray();
                Assert.That(slots, Has.Length.EqualTo(4), "Two seated venues, two seats each.");
                foreach (var slot in slots)
                {
                    var seat = seats.OrderBy(t => Flat(t.position, slot.Position)).First();
                    Assert.That(Flat(seat.position, slot.Position), Is.LessThan(0.1f),
                        slot.VenueId + " slot at " + slot.Position + " is not on a seat; the nearest is " + seat.name + " at " + seat.position);
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(seat.eulerAngles.y, slot.Yaw)), Is.LessThan(1f),
                        slot.VenueId + " must face the way its seat does (" + seat.eulerAngles.y + ").");
                    Assert.That(NpcSocialState.IsKnownRendezvous(slot.VenueId), Is.True,
                        "A save naming " + slot.VenueId + " must validate.");
                    Assert.That(HouseMeetingCoordinator.IsSeatedVenue(slot.VenueId), Is.True);
                }
                Assert.That(slots.Select(slot => slot.VenueId).Distinct().Count(), Is.EqualTo(2));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void StandingVenuesAreStillTheFourTheSavesKnow()
        {
            foreach (var id in new[] { "living-east-chat", "kitchen-west-chat", "bedroom-south-chat", "yard-south-chat" })
            {
                Assert.That(NpcSocialState.IsKnownRendezvous(id), Is.True, id);
                Assert.That(HouseMeetingCoordinator.IsSeatedVenue(id), Is.False, id + " is a standing chat.");
            }
            Assert.That(NpcSocialState.IsKnownRendezvous("private-room-chat"), Is.False, "The diary room is never a venue.");
        }

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
    }
}
