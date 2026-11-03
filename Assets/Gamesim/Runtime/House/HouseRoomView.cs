using UnityEngine;

namespace Gamesim.House
{
    /// <summary>
    /// The room view's framing (ROOM-SCOPE-PLAN §3.2 g, D4, D5, D11, RS5): the free camera over one room.
    ///
    /// <para>The farthest zoom is fixed per room, its fit along its best heading (D4): 11.25 m for the four
    /// 14 x 10 m rooms of the north block (yaw 0), 10.5 m for the three 9.4 x 10 m rooms of the south wing
    /// (yaw 90) and 18.25 m for the 28 x 10 m Yard (yaw 0). It does not depend on the yaw, so orbiting never
    /// pulls the camera in; at diagonal yaws the room's corners crop. A room's best headings look along its
    /// short side, so its long side spans the frame's width: the heading and the one opposite it.</para>
    ///
    /// <para>The room clamp sits beside the house's (§3.2 g): it narrows with the zoom, so at the farthest
    /// zoom the focus is the room's centre and the whole room is in frame, and closer in the focus may
    /// follow the player out towards the walls, all the way to them at the nearest zoom (4 m, D11). The
    /// pitch curve keeps the rig's own metres: nothing here feeds it.</para>
    ///
    /// <para>The view changes rooms at most once per <see cref="RoomChangeSeconds"/> while it follows a
    /// walker (§3.2 a): a change inside that waits, with the subject still drawn.</para>
    ///
    /// <para>Plain C# over plan rectangles and room indices (the layers' order), so a fixture runs it under
    /// the editor's Mono.</para>
    /// </summary>
    public static class HouseRoomView
    {
        /// <summary>The nearest zoom (D11): four metres, the 2026-09-17 decision the scene lost a day later.</summary>
        public const float NearestDistance = 4f;

        /// <summary>The farthest zooms of D4, by the room's block.</summary>
        public const float NorthBlockFarthest = 11.25f, SouthWingFarthest = 10.5f, YardFarthest = 18.25f;

        /// <summary>How often, at most, a walk changes the room the view shows (§3.2 a).</summary>
        public const float RoomChangeSeconds = .6f;

        /// <summary>How far outside its room's box the camera's focus may stand, and its boom past the room's farthest zoom, and still be over the room.</summary>
        public const float OverRoomSlack = .5f;

        // By room index, in RoomLayers' order: Living, Kitchen, Bedroom, Private, Yard, HoH, Nomination, Games.
        private static readonly float[] farthest =
        {
            NorthBlockFarthest, NorthBlockFarthest, NorthBlockFarthest, NorthBlockFarthest, YardFarthest,
            SouthWingFarthest, SouthWingFarthest, SouthWingFarthest,
        };
        private static readonly float[] heading = { 0f, 0f, 0f, 0f, 0f, 90f, 90f, 90f };

        /// <summary>Whether <paramref name="room"/> is one of the eight rooms the view frames.</summary>
        public static bool Frames(int room) => room >= 0 && room < farthest.Length;

        /// <summary>The farthest the camera zooms out over room <paramref name="room"/> (D4); 0 for no room.</summary>
        public static float FarthestDistance(int room) => Frames(room) ? farthest[room] : 0f;

        /// <summary>The room's best heading (D4): the yaw that looks along its short side. The heading opposite it frames the room as well.</summary>
        public static float BestHeading(int room) => Frames(room) ? heading[room] : 0f;

        /// <summary>
        /// The room's best heading nearest <paramref name="yaw"/>: the heading or the one opposite it,
        /// whichever turns the camera least (§3.2 g, D4), in 0 to 360. The heading itself on a tie.
        /// </summary>
        public static float NearestBestHeading(int room, float yaw)
        {
            float best = BestHeading(room), opposite = Mathf.Repeat(best + 180f, 360f);
            return Mathf.Abs(Mathf.DeltaAngle(yaw, opposite)) < Mathf.Abs(Mathf.DeltaAngle(yaw, best)) ? opposite : best;
        }

        /// <summary>
        /// How much of a room's half-extents the focus may stray from its centre at a distance: none at
        /// <paramref name="farthestDistance"/> and beyond, all of it at <paramref name="nearestDistance"/>
        /// and closer, in a straight line between.
        /// </summary>
        public static float Reach(float distance, float nearestDistance, float farthestDistance)
        {
            if (farthestDistance <= nearestDistance) return 0f;
            return Mathf.Clamp01((farthestDistance - distance) / (farthestDistance - nearestDistance));
        }

        /// <summary>
        /// The room clamp (§3.2 g): <paramref name="focus"/> kept inside the room's box (x is world x, y is
        /// world z) narrowed about its centre by <see cref="Reach"/>. The height is the caller's.
        /// </summary>
        public static Vector3 Clamp(Rect box, Vector3 focus, float distance, float nearestDistance, float farthestDistance)
        {
            float reach = Reach(distance, nearestDistance, farthestDistance);
            var centre = box.center;
            float halfX = box.width * .5f * reach, halfZ = box.height * .5f * reach;
            focus.x = Mathf.Clamp(focus.x, centre.x - halfX, centre.x + halfX);
            focus.z = Mathf.Clamp(focus.z, centre.y - halfZ, centre.y + halfZ);
            return focus;
        }

        /// <summary>
        /// Whether a camera with its focus at <paramref name="focus"/> and its boom at
        /// <paramref name="distance"/> is over the room: the focus within <see cref="OverRoomSlack"/> of
        /// the room's box and the boom no farther than the room's farthest zoom and that slack. A view
        /// that comes on with the camera anywhere else cuts into the room (D5).
        /// </summary>
        public static bool Over(Rect box, Vector3 focus, float distance, float farthestDistance)
        {
            if (distance > farthestDistance + OverRoomSlack) return false;
            return focus.x >= box.xMin - OverRoomSlack && focus.x <= box.xMax + OverRoomSlack
                && focus.z >= box.yMin - OverRoomSlack && focus.z <= box.yMax + OverRoomSlack;
        }

        /// <summary>
        /// Whether a walk's change of room must wait (§3.2 a): the view last changed rooms less than
        /// <see cref="RoomChangeSeconds"/> ago. A clock that reads earlier than the change (a new play
        /// session's) never makes a change wait.
        /// </summary>
        public static bool Waits(float now, float changedAt) => now >= changedAt && now - changedAt < RoomChangeSeconds;
    }
}
