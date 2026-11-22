using System.Linq;
using Gamesim.House;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The Notice line's room (plan 32 §I2 and §N, S2a): <see cref="NpcPairing.RoomOf"/> agrees with the house's authored
    /// meeting anchors - every rendezvous the saved world knows maps to a room the house builds, the very room its anchor is
    /// authored in. EditMode only: the anchors are the runtime's.
    /// </summary>
    public sealed class NpcPairingRoomTests
    {
        [Test]
        public void EveryKnownRendezvousIsInTheRoomItsMeetingAnchorIsAuthoredIn()
        {
            Assert.That(NpcPairing.KnownRendezvous, Is.EquivalentTo(HouseInteractionAnchors.Meetings.Select(m => m.Id)), "The same rendezvous.");
            foreach (var meeting in HouseInteractionAnchors.Meetings)
            {
                string room = NpcPairing.RoomOf(meeting.Id);
                Assert.That(RoomWords.IsRoom(room), Is.True, meeting.Id + " is in a room the house builds.");
                Assert.That(room, Is.EqualTo(meeting.Room), meeting.Id);
            }
            Assert.That(NpcPairing.RoomOf("nowhere-chat"), Is.Null);
            Assert.That(NpcPairing.RoomOf(null), Is.Null);
        }
    }
}
