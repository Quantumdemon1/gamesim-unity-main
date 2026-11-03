using System.Collections.Generic;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Stepping beside a houseguest to talk to them, by the floors (ROOM-SCOPE-PLAN §3.2 k, RSW).
    ///
    /// <para>Twelve tests warp the player a step from a houseguest whose autonomy is still live and
    /// then need Talk at once: a conversation opened, a click within reach answered, the talk prompt
    /// up. Talk was the 2.8 m reach and a sight line at 1.15 m, which passes over the south wing's
    /// 1.1 m walls, so any point a step away would do. Since RSW it also needs the two on one floor, or
    /// both within one doorway's band with no wall between their feet. At the saved spawns every
    /// houseguest stands two metres or more inside its floor, so the tests' own points hold; a wander
    /// can leave one beside a wall or a doorway, and a step towards wherever the player stood can then
    /// land across it, where Talk is rightly refused.</para>
    ///
    /// <para>So each test names its own point, and <see cref="WarpToTalk"/> warps there first with
    /// <see cref="WarpPlayer"/> itself: a point with no floor within 0.75 m, or a warp the agent
    /// refuses, fails the test there as it always has, and wherever Talk is allowed from the point,
    /// that is all that happens. Only where Talk refuses it is the same step turned around the
    /// houseguest, nearest direction first, and the first where the two then stand on one floor taken;
    /// a point Talk allows across a doorway's band only where no direction puts them on one floor. The
    /// houseguest is never moved. Where every direction is refused the test fails here, naming where
    /// the houseguest stands, their floor and every point tried with why Talk refused it, so a
    /// regression in Talk still reads as one.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>How many directions the step is tried in around the houseguest, the test's own first: every 22.5°.</summary>
        private const int TalkBesideDirections = 16;

        /// <summary>
        /// Warps the player to <paramref name="point"/>, a step from <paramref name="npc"/>, with
        /// <see cref="WarpPlayer"/> itself, and stops there if Talk is allowed (Talk's own test,
        /// <see cref="RswCanTalk"/>: what TryOpenNpc, a click, Talk with intent and the prompt all ask).
        /// Where it is refused, the same step turned about the houseguest - so the same distance - is
        /// tried in the other directions, nearest first: the first where Talk is allowed with the two
        /// on one floor is taken, else the first where it is allowed across a doorway's band. A turned
        /// point with no floor within 0.75 m, or a warp the agent refuses, is passed over and noted.
        /// Fails where no direction will do, saying why at every point.
        ///
        /// <para>Talk is asked in the warp frame. A test that yields before it needs Talk can still be
        /// refused if the houseguest's wander takes them across a doorway in between; a TryOpenNpc that
        /// fails after a silent WarpToTalk means exactly that.</para>
        /// </summary>
        private void WarpToTalk(HouseNpc npc, Vector3 point)
        {
            var centre = npc.transform.position;
            var step = point - centre;
            // The test's own point, exactly as before.
            WarpPlayer(point);
            if (RswCanTalk(npc)) return;
            var tried = new List<string> { "the test's own point " + TalkPlan(player.transform.position) + ": " + WhyTalkRefuses(npc) };
            // The turned points: one floor first; a doorway's band is the easier for a wander to break.
            var acrossADoorway = new List<int>();
            for (int turn = 1; turn < TalkBesideDirections; turn++)
            {
                if (!TryTalkBeside(npc, centre, step, turn, out string where, out string refused))
                {
                    tried.Add(where + ": " + refused);
                    continue;
                }
                if (OnOneFloorWith(npc))
                {
                    TookTurnedPoint(npc, where, tried);
                    return;
                }
                acrossADoorway.Add(turn);
                tried.Add(where + ": allowed across a doorway's band, kept in case no direction is on their floor");
            }
            foreach (int turn in acrossADoorway)
            {
                if (TryTalkBeside(npc, centre, step, turn, out string where, out string refused))
                {
                    TookTurnedPoint(npc, where, tried);
                    return;
                }
                tried.Add(where + ", tried again: " + refused);
            }
            string floor = director.WalkerRoomOf(npc.transform);
            Assert.Fail("No point " + new Vector2(step.x, step.z).magnitude.ToString("0.00") + " m from " + npc.DisplayName
                + " lets the player talk to them. They stand at " + TalkPlan(centre)
                + (floor != null ? " on the " + floor + " floor" : ", on no floor the room scope knows")
                + ". Tried: " + string.Join("; ", tried) + ".");
        }

        /// <summary>
        /// Warps the player to the step from <paramref name="centre"/> turned by direction
        /// <paramref name="turn"/> (+22.5°, -22.5°, +45° ... 180°) and asks Talk there. False with the
        /// reason where there is no floor, the agent refuses the warp or Talk is refused.
        /// </summary>
        private bool TryTalkBeside(HouseNpc npc, Vector3 centre, Vector3 step, int turn, out string where, out string refused)
        {
            float degrees = (turn + 1) / 2 * (360f / TalkBesideDirections) * (turn % 2 == 1 ? 1f : -1f);
            var candidate = centre + Quaternion.Euler(0f, degrees, 0f) * step;
            string which = "turned " + degrees.ToString("+0.0;-0.0") + "° ";
            where = which + TalkPlan(candidate);
            refused = null;
            if (!NavMesh.SamplePosition(candidate, out var hit, 0.75f, player.Agent.areaMask))
            {
                refused = "no floor within 0.75 m";
                return false;
            }
            where = which + TalkPlan(hit.position);
            if (!player.Agent.Warp(hit.position))
            {
                refused = "the agent refused the warp";
                return false;
            }
            player.Agent.ResetPath();
            Physics.SyncTransforms();
            if (RswCanTalk(npc)) return true;
            refused = WhyTalkRefuses(npc);
            return false;
        }

        /// <summary>Notes in the test's output that the test's own point would not do, and what was taken.</summary>
        private static void TookTurnedPoint(HouseNpc npc, string where, List<string> tried)
            => TestContext.WriteLine("WarpToTalk: the test's own point beside " + npc.DisplayName + " would not do; taken "
                + where + ". Before it: " + string.Join("; ", tried) + ".");

        /// <summary>Whether the player and <paramref name="npc"/> were last tracked on one floor, or the floors cannot say.</summary>
        private bool OnOneFloorWith(HouseNpc npc)
        {
            var scope = director.RoomScope;
            if (scope == null) return true;
            int here = scope.LastRoom(player.transform), there = scope.LastRoom(npc.transform);
            return here < 0 || there < 0 || here == there;
        }

        /// <summary>
        /// Why Talk refuses <paramref name="npc"/> from where the player stands, by its own tests in its
        /// order: the reach, the floors (one floor, or both in one doorway's band with no wall between
        /// their feet), the sight line - naming what stands in the way.
        /// </summary>
        private string WhyTalkRefuses(HouseNpc npc)
        {
            var me = player.transform;
            var them = npc.transform;
            var mine = me.position;
            var theirs = them.position;
            // Talk measures between points 1.15 m over each pair of feet: the same offset.
            float gap = Vector3.Distance(mine, theirs);
            if (gap > 2.8f) return gap.ToString("0.00") + " m apart, past the 2.8 m reach";
            var scope = director.RoomScope;
            int here = scope != null ? scope.LastRoom(me) : -1;
            int there = scope != null ? scope.LastRoom(them) : -1;
            if (here < 0 || there < 0 || here == there)
                return (here < 0 || there < 0 ? "the floors cannot say" : "on one floor") + ", and " + SightBlockedBy(npc);
            string floors = "the player on the " + HouseRoomScope.RoomId(here) + " floor, they on the " + HouseRoomScope.RoomId(there) + ": ";
            bool inTheirBand = HouseWhoIsWhere.InDoorwayBand(scope, there, mine);
            bool inMyBand = HouseWhoIsWhere.InDoorwayBand(scope, here, theirs);
            string band = HouseWhoIsWhere.DoorwayBand.ToString("0.0") + " m";
            if (!inTheirBand || !inMyBand)
                return floors + "the player " + (inTheirBand ? "within " : "beyond ") + band + " of their floor, they "
                    + (inMyBand ? "within " : "beyond ") + band + " of the player's";
            // The director's own look for a wall, at half a metre over the feet, and a mirror of it to name the wall.
            float height = (mine.y + theirs.y) * .5f + HouseWhoIsWhere.BelowEveryWall;
            var low = new Vector3(mine.x, height, mine.z);
            var lowThere = new Vector3(theirs.x, height, theirs.z);
            string wall = FirstInTheWay(low, lowThere, me, them, true);
            var look = typeof(EpisodeDirector).GetMethod("WallBetween", BindingFlags.NonPublic | BindingFlags.Instance);
            bool walled = look != null ? (bool)look.Invoke(director, new object[] { mine, theirs, me, them }) : wall != null;
            if (walled)
                return floors + "both in the doorway band, with a wall between their feet (" + (wall ?? "one this look does not find") + ")";
            return floors + "both in the doorway band with no wall at " + HouseWhoIsWhere.BelowEveryWall.ToString("0.0") + " m, and " + SightBlockedBy(npc);
        }

        /// <summary>What blocks Talk's sight line, 1.15 m over each pair of feet, as Talk looks.</summary>
        private string SightBlockedBy(HouseNpc npc)
        {
            var me = player.transform;
            var them = npc.transform;
            string what = FirstInTheWay(me.position + Vector3.up * 1.15f, them.position + Vector3.up * 1.15f, me, them, false);
            return "the 1.15 m sight line between them is blocked by " + (what ?? "nothing this look finds");
        }

        /// <summary>
        /// The nearest thing across a look on the Sight layers that Talk would count, named: a houseguest
        /// by name, anything else by its collider. Skips what is under either of the two; the look for a
        /// wall (<paramref name="wallsOnly"/>) also skips floors and bodies, as the director's does. Null
        /// where nothing is in the way.
        /// </summary>
        private static string FirstInTheWay(Vector3 from, Vector3 to, Transform one, Transform two, bool wallsOnly)
        {
            var offset = to - from;
            float distance = offset.magnitude;
            if (distance < .001f) return null;
            var hits = Physics.RaycastAll(from, offset / distance, distance, HouseLayers.Sight, QueryTriggerInteraction.Ignore);
            // Talk's looks read into a 32-hit buffer and fail closed when it fills.
            if (hits.Length >= 32) return hits.Length + " things at once, past the 32 Talk's look reads";
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var collider = hit.collider;
                if (collider == null) continue;
                // As each look skips the two: Talk's by the hit's transform, the wall look by the collider's.
                var owner = wallsOnly ? collider.transform : hit.transform;
                if (owner.IsChildOf(one) || owner.IsChildOf(two)) continue;
                var body = collider.GetComponentInParent<HouseNpc>();
                if (wallsOnly && (collider.GetComponent<HouseWalkable>() != null || body != null
                    || collider.GetComponentInParent<HousePlayerController>() != null)) continue;
                return body != null ? body.DisplayName + ", standing in the way (" + collider.name + ")"
                    : "'" + collider.name + "' at " + TalkPlan(hit.point);
            }
            return null;
        }

        /// <summary>A point in plan, to the centimetre.</summary>
        private static string TalkPlan(Vector3 point) => "(" + point.x.ToString("0.00") + ", " + point.z.ToString("0.00") + ")";
    }
}
