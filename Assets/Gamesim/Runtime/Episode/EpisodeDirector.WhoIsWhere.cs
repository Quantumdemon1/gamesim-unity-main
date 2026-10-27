using System;
using System.Linq;
using Gamesim.House;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// Who is where by floor (ROOM-SCOPE-PLAN §3.2 k, RSW; the owner's D14, "Follow the floor").
    ///
    /// <para>Who is where read the nearest room marker, and the markers stand off-centre: the
    /// living room's marker is 2.4 m from its south wall and the bedroom's 2 m from the wall the two
    /// share, so the living room north of z = -2.8 read "Bedroom"; with its corners by the kitchen and
    /// the HoH suite, 39.5% of its floor named another room on a half-metre grid, and 37.3% of the
    /// kitchen's. A room
    /// view would show the disagreement: the walk-in watch offered "Step in" on a pair standing in a
    /// room it would not draw. Now each body is in its walker room
    /// (<see cref="WalkerRoomOf"/>, RS1's rule): the floor its feet are on, kept through a doorway
    /// until they are three tenths of a metre inside the next. Every reader of
    /// <see cref="HouseOccupancy(EpisodeState)"/> follows - the notebook's Who is where, the social
    /// screen's and the free-time board's location and HERE tags, the overview's column, the room
    /// icons' counts, the live feed's caption, the room acts' audiences and the walk-in watch - and
    /// Talk needs the two on one floor or both in one doorway's band.</para>
    ///
    /// <para>Live play only: what changes is which walk-ins, HERE tags, room acts and talks the house
    /// offers. The rooms are still the markers' own ids, which are the floors' (<see cref="RoomWords.Rooms"/>),
    /// so a walk-in's command names a room from the same vocabulary, and no schema, saved field,
    /// command kind or rule moves.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>
        /// The room a body is in for who-is-where: its walker room by the floors, as the house's
        /// markers name it. The nearest marker only where the floors cannot say - a house the room
        /// scope refuses, or a floor no marker names - which is the rule as it was before them.
        /// </summary>
        private string OccupancyRoom(Transform body, HouseRoomMarker[] markers)
        {
            string floor = WalkerRoomOf(body);
            if (floor != null)
                foreach (var marker in markers)
                    if (string.Equals(marker.RoomName, floor, StringComparison.Ordinal)) return floor;
            var nearest = NearestMarker(markers, body.position);
            return nearest != null ? nearest.RoomName : null;
        }

        /// <summary>
        /// The marker of the room the live feed looks into: a body's walker room, or the floor under a
        /// point; the nearest marker where the floors cannot say. So the room the feed names is the
        /// room its caption counts.
        /// </summary>
        private HouseRoomMarker FloorMarker(HouseRoomMarker[] markers, Vector3 at, Transform body)
        {
            string floor = body != null ? WalkerRoomOf(body)
                : EnsureRoomScope() ? HouseRoomScope.RoomId(roomScope.RoomUnder(at)) : null;
            if (floor != null)
                foreach (var marker in markers)
                    if (marker != null && string.Equals(marker.RoomName, floor, StringComparison.Ordinal)) return marker;
            return NearestMarker(markers, at);
        }

        /// <summary>
        /// Whether the floors let the player talk to someone (§3.2 k): one walker room, or both in one
        /// doorway's band with no wall between them. The band reaches either side of a wall as well as
        /// of a doorway, and the south wing's walls are 1.1 m, under the 1.15 m sight line Talk looks
        /// across, so a wall is looked for at half a metre. Where the floors cannot say, yes: the
        /// range and the sight line decide, as they did.
        /// </summary>
        private bool FloorsLetTalk(Transform other)
        {
            if (player == null || other == null || !EnsureRoomScope()) return true;
            var mine = player.transform;
            int here = roomScope.Track(mine), there = roomScope.Track(other);
            if (here < 0 || there < 0 || here == there) return true;
            return HouseWhoIsWhere.FloorsLetThemTalk(roomScope, mine.position, here, other.position, there)
                && !WallBetween(mine.position, other.position, mine, other);
        }

        /// <summary>
        /// Whether a solid stands across the floor between two people's feet, looked for at
        /// <see cref="HouseWhoIsWhere.BelowEveryWall"/>: a wall, never a floor, the two themselves or
        /// anybody else standing there. Fails closed when the look overflows its buffer, as Talk does.
        /// </summary>
        private bool WallBetween(Vector3 from, Vector3 to, Transform one, Transform two)
        {
            float height = (from.y + to.y) * .5f + HouseWhoIsWhere.BelowEveryWall;
            var start = new Vector3(from.x, height, from.z);
            var offset = new Vector3(to.x, height, to.z) - start;
            float distance = offset.magnitude;
            if (distance < .001f) return false;
            int count = Physics.RaycastNonAlloc(start, offset / distance, sightHits, distance, HouseLayers.Sight, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return true;
            for (int i = 0; i < count; i++)
            {
                var hit = sightHits[i].collider;
                if (hit == null || hit.transform.IsChildOf(one) || hit.transform.IsChildOf(two)) continue;
                if (hit.GetComponent<HouseWalkable>() != null) continue;
                if (hit.GetComponentInParent<HouseNpc>() != null || hit.GetComponentInParent<HousePlayerController>() != null) continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Whether a spot to walk to lands the player on someone's floor: far enough inside their walker
        /// room that the player is in it on arrival. A spot across a doorway from them is in the next
        /// room, where Talk would not open and the walk would go on until it gave up. Where the floors
        /// cannot say, any spot will do.
        /// </summary>
        private bool ApproachOnTheirFloor(Vector3 spot, Transform other)
        {
            if (other == null || !EnsureRoomScope()) return true;
            int theirs = roomScope.Track(other);
            return theirs < 0 || HouseWhoIsWhere.SurelyIn(roomScope, theirs, spot);
        }

        /// <summary>
        /// What the walk-in watch has found, before the engine is asked about it: the player's room by
        /// the floors and, in it, the two houseguests who are together nearest the player
        /// (<see cref="TryWalkInPair"/>). Everyone it considers stands on the player's floor, so a pair
        /// across a wall - in a room a room view does not draw - is never offered.
        /// </summary>
        private bool TryWalkInHere(EpisodeState state, out string room, out string first, out string second)
        {
            room = first = second = null;
            var here = HouseOccupancy(state)
                .FirstOrDefault(each => each.Occupants != null && each.Occupants.Any(person => person.IsPlayer));
            if (string.IsNullOrEmpty(here.Name)) return false;
            var others = here.Occupants
                .Where(person => !person.IsPlayer && !string.IsNullOrEmpty(person.Id))
                .Select(person => person.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            if (!TryWalkInPair(others, out first, out second)) return false;
            room = here.Name;
            return true;
        }
    }
}
