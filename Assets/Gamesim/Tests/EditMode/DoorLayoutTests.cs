using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Where the door set stands (MOCKUP-PASS-PLAN M23): the yard's layout is the set's own
    /// constants, which the opening's tests measure, and the living room's is the same set moved to
    /// the south end of the west wall - the doorway and the vestibule the yard's, the facade its
    /// own - clear of the walls round it.
    /// </summary>
    public sealed class DoorLayoutTests
    {
        private const float Tolerance = 1e-4f;

        [TestCase(0f)]
        [TestCase(0.35f)]
        [TestCase(0.38f)]
        [TestCase(0.5f)]
        public void TheOpeningRouteDoesNotClearLeavesStillAcrossTheBody(float openness)
        {
            Assert.That(OpeningDoorSet.CanWalkThrough(DoorLayout.Yard, openness,
                new Vector3(-3.85f, 0f, 13.8f), new Vector3(-1.6f, 0f, 14.1f), 0.35f), Is.False,
                "The complete .7 m wide body corridor must clear both leaves before the walk begins.");
        }

        [Test]
        public void OpenLeavesClearTheOpeningRouteAndTheMovedLivingDoor()
        {
            var from = new Vector3(-3.85f, 0f, 13.8f);
            var to = new Vector3(-1.6f, 0f, 14.1f);
            Assert.That(OpeningDoorSet.CanWalkThrough(DoorLayout.Yard, 1f, from, to, 0.35f), Is.True);
            Assert.That(OpeningDoorSet.CanWalkThrough(DoorLayout.Living, 1f,
                from + DoorLayout.Living.Origin, to + DoorLayout.Living.Origin, 0.35f), Is.True,
                "Moving the set and route together preserves physical clearance.");
            Assert.That(OpeningDoorSet.CanWalkThrough(DoorLayout.Yard, 1f, from, to, 0.8f), Is.False,
                "Even fully open leaves cannot clear a body wider than this route allows.");
        }

        [Test]
        public void ABodyBetweenTheOpenLeafAndItsHingeStillCannotPass()
        {
            Assert.That(OpeningDoorSet.CanWalkThrough(DoorLayout.Yard, 1f,
                new Vector3(-3.85f, 0f, 12.8f), new Vector3(-1.6f, 0f, 12.8f), 0.35f), Is.False,
                "Open is insufficient when the actor's route runs through a leaf near the jamb.");
        }

        [Test]
        public void TheYardsLayoutIsTheSetsOwnGeometry()
        {
            var yard = DoorLayout.Yard;
            Assert.That(yard.Origin, Is.EqualTo(Vector3.zero), "The yard's set stands at the world origin, as the opening built it.");
            Assert.That(yard.DoorCentre, Is.EqualTo(OpeningDoorSet.DoorCentre));
            Assert.That(yard.FacadeFrontX, Is.EqualTo(OpeningDoorSet.FacadeFrontX));
            Assert.That(yard.VestibuleFarX, Is.EqualTo(OpeningDoorSet.VestibuleFarX));
            Assert.That(yard.ApertureMinZ, Is.EqualTo(OpeningDoorSet.ApertureMinZ));
            Assert.That(yard.ApertureMaxZ, Is.EqualTo(OpeningDoorSet.ApertureMaxZ));
            Assert.That(yard.FacadeMinZ, Is.EqualTo(OpeningDoorSet.FacadeMinZ));
            Assert.That(yard.FacadeMaxZ, Is.EqualTo(OpeningDoorSet.FacadeMaxZ));
            Assert.That(yard.FacadeHeight, Is.EqualTo(OpeningDoorSet.FacadeHeight));
            Assert.That(yard.Emblem, Is.EqualTo(DoorLayout.EyeEmblem), "The welcome keeps its eye.");
        }

        [Test]
        public void TheLivingRoomsDoorStandsOnTheWestWallClearOfItsWalls()
        {
            var living = DoorLayout.Living;
            Assert.That(living.DoorCentre.x, Is.EqualTo(-12.6f).Within(Tolerance), "The doorway's middle is on the west wall's south end,");
            Assert.That(living.DoorCentre.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(living.DoorCentre.z, Is.EqualTo(-8.5f).Within(Tolerance));
            Assert.That(living.ApertureMinZ, Is.EqualTo(-9.6f).Within(Tolerance), "its doorway runs from z -9.6");
            Assert.That(living.ApertureMaxZ, Is.EqualTo(-7.4f).Within(Tolerance), "to z -7.4,");
            Assert.That(living.FacadeFrontX, Is.EqualTo(-12.6f).Within(Tolerance));
            Assert.That(living.VestibuleFarX, Is.EqualTo(-13.8f).Within(Tolerance), "and its vestibule's back stands at x -13.8.");
            Assert.That(living.FacadeMinZ, Is.EqualTo(-9.85f).Within(Tolerance));
            Assert.That(living.FacadeMaxZ, Is.EqualTo(-7.33f).Within(Tolerance));
            Assert.That(living.FacadeHeight, Is.EqualTo(3.6f).Within(Tolerance));

            // The walls round it, from HousePrototypeSetup and the memory wall's frames.
            const float southWallFace = -9.875f, memoryWallFrames = -7.31f, westWallFace = -13.875f;
            Assert.That(living.FacadeMinZ, Is.GreaterThan(southWallFace), "The facade stops short of the south wall's face,");
            Assert.That(living.FacadeMaxZ, Is.LessThan(memoryWallFrames), "and of the memory wall's frames,");
            Assert.That(living.VestibuleFarX, Is.GreaterThan(westWallFace), "and the vestibule stands inside the west wall.");

            // The yard's set moved, not reshaped: its doorway, its vestibule and its leaves.
            Assert.That(living.ApertureMaxZ - living.ApertureMinZ, Is.EqualTo(OpeningDoorSet.ApertureMaxZ - OpeningDoorSet.ApertureMinZ).Within(Tolerance));
            Assert.That(living.FacadeFrontX - living.VestibuleFarX, Is.EqualTo(OpeningDoorSet.FacadeFrontX - OpeningDoorSet.VestibuleFarX).Within(Tolerance));
            Assert.That(living.SweepFrontX, Is.LessThan(-11.2f), "The open leaves stop short of the last look's mark at x -11.2.");
            Assert.That(living.Emblem, Is.EqualTo(DoorLayout.CrownEmblem), "The crown over the lintel: this door goes to the jury.");
        }
    }
}
