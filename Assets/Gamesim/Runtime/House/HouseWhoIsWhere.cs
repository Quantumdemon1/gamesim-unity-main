using UnityEngine;

namespace Gamesim.House
{
    /// <summary>
    /// Who is where by floor (ROOM-SCOPE-PLAN §3.2 k, RSW; the owner's D14, "Follow the floor"): the
    /// plain rules the director's who-is-where and its Talk read, on the room scope's floor boxes.
    ///
    /// <para>A body is in its walker room (<see cref="HouseRoomScope.WalkerRoom"/>): the floor its feet
    /// are on, kept through a doorway until they are three tenths of a metre inside the next. Two people
    /// talk when they share that room, or when both stand in one doorway's band - each within
    /// <see cref="DoorwayBand"/> of the other's floor (§3.2 e), so both within it of the line their
    /// floors share. The band alone is either side of a wall as much as of a doorway, so the director
    /// completes it with a look across the floor at <see cref="BelowEveryWall"/>, where only a doorway
    /// is open. A walk over to talk to someone aims where it is sure to end on their floor
    /// (<see cref="WalkEndsIn"/>).</para>
    ///
    /// <para>Plain C# over plan rectangles, so a fixture runs it under the editor's Mono.</para>
    /// </summary>
    public static class HouseWhoIsWhere
    {
        /// <summary>How far past a floor's box the feet may stand and still be in its doorway band (§3.2 e).</summary>
        public const float DoorwayBand = .6f;

        /// <summary>
        /// The height the look for a wall crosses at, over the feet: above the floors, below every wall's
        /// top (the south wing's are 1.1 m, under the 1.15 m a conversation's own sight line looks across).
        /// </summary>
        public const float BelowEveryWall = .5f;

        /// <summary>How far outside a floor's box the feet stand, in plan: 0 on it, otherwise the distance to its nearest point.</summary>
        public static float OutsideBy(Rect box, Vector3 feet)
        {
            float dx = Mathf.Max(box.xMin - feet.x, Mathf.Max(0f, feet.x - box.xMax));
            float dz = Mathf.Max(box.yMin - feet.z, Mathf.Max(0f, feet.z - box.yMax));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>How far inside a floor's box the feet stand, to its nearest edge; negative outside it.</summary>
        public static float InsideBy(Rect box, Vector3 feet)
            => Mathf.Min(Mathf.Min(feet.x - box.xMin, box.xMax - feet.x), Mathf.Min(feet.z - box.yMin, box.yMax - feet.z));

        /// <summary>Whether the feet are in room <paramref name="room"/>'s doorway band: on its floor, or within <see cref="DoorwayBand"/> of it.</summary>
        public static bool InDoorwayBand(HouseRoomScope scope, int room, Vector3 feet)
            => scope != null && scope.Has(room) && OutsideBy(scope.Box(room), feet) <= DoorwayBand;

        /// <summary>
        /// Whether two people's floors let them talk: one walker room, or each in the other's doorway
        /// band. True where the floors cannot say - no scope, or a body with no room - which leaves the
        /// rule as it was before the floors. A wall between two people in the band is the director's to
        /// look for (<see cref="BelowEveryWall"/>).
        /// </summary>
        public static bool FloorsLetThemTalk(HouseRoomScope scope, Vector3 one, int oneRoom, Vector3 two, int twoRoom)
        {
            if (scope == null || oneRoom < 0 || twoRoom < 0 || oneRoom == twoRoom) return true;
            return InDoorwayBand(scope, twoRoom, one) && InDoorwayBand(scope, oneRoom, two);
        }

        /// <summary>
        /// How much further than its agent's stopping distance a walk may end from its spot and still
        /// have arrived: <see cref="HousePlayerController.HasArrived"/>'s own slack.
        /// </summary>
        public const float ArrivalSlack = .1f;

        /// <summary>
        /// Whether a spot is surely in room <paramref name="room"/> for a walker who arrives there: at
        /// least <see cref="HouseRoomScope.DoorwayDepth"/> inside its floor, where the walker rule names
        /// that room whatever room the walk began in.
        /// </summary>
        public static bool SurelyIn(HouseRoomScope scope, int room, Vector3 feet)
            => SurelyIn(scope, room, feet, HouseRoomScope.DoorwayDepth);

        /// <summary>Whether the feet are at least <paramref name="depth"/> inside room <paramref name="room"/>'s floor.</summary>
        public static bool SurelyIn(HouseRoomScope scope, int room, Vector3 feet, float depth)
            => scope != null && scope.Has(room) && InsideBy(scope.Box(room), feet) >= depth;

        /// <summary>
        /// Whether a walk to a spot ends in room <paramref name="room"/>. An agent stops up to its
        /// <paramref name="stoppingDistance"/> short of where it was sent, and has arrived
        /// <see cref="ArrivalSlack"/> further out still, so a spot only <see cref="SurelyIn(HouseRoomScope, int, Vector3)"/>
        /// can leave a walker who came through a doorway a step short of it, in the room they left. A
        /// spot that much deeper is <see cref="HouseRoomScope.DoorwayDepth"/> inside wherever the walk
        /// stops, from any side.
        /// </summary>
        public static bool WalkEndsIn(HouseRoomScope scope, int room, Vector3 spot, float stoppingDistance)
            => SurelyIn(scope, room, spot, HouseRoomScope.DoorwayDepth + Mathf.Max(0f, stoppingDistance) + ArrivalSlack);
    }
}
