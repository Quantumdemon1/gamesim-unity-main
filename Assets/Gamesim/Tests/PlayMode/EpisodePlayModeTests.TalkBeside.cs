using System.Collections.Generic;
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
    /// <para>So each test names its own point, and <see cref="WarpToTalk"/> warps there first, as
    /// <see cref="WarpPlayer"/> does: wherever Talk is allowed from it, that is all that happens. Only
    /// where Talk refuses it is the same step turned around the houseguest, nearest direction first,
    /// and the first where Talk is allowed taken. The houseguest is never moved. Where every direction
    /// is refused the test fails here, naming where the houseguest stands, their floor and every point
    /// tried with why Talk refused it, so a regression in Talk still reads as one.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>How many directions the step is tried in around the houseguest, the test's own first: every 22.5°.</summary>
        private const int TalkBesideDirections = 16;

        /// <summary>
        /// Warps the player to <paramref name="point"/>, a step from <paramref name="npc"/>, exactly as
        /// <see cref="WarpPlayer"/> does, and stops there if Talk is allowed (Talk's own test,
        /// <see cref="RswCanTalk"/>: what TryOpenNpc, a click, Talk with intent and the prompt all ask).
        /// Where it is refused, the same step turned about the houseguest - so the same distance - is
        /// tried in the other directions, nearest first, and the first where Talk is allowed is taken.
        /// Fails where none is, saying why at every point.
        /// </summary>
        private void WarpToTalk(HouseNpc npc, Vector3 point)
        {
            var centre = npc.transform.position;
            var step = point - centre;
            var tried = new List<string>();
            for (int turn = 0; turn < TalkBesideDirections; turn++)
            {
                // 0, then +22.5, -22.5, +45, -45 ... +157.5, -157.5, 180 degrees.
                float degrees = (turn + 1) / 2 * (360f / TalkBesideDirections) * (turn % 2 == 1 ? 1f : -1f);
                var candidate = turn == 0 ? point : centre + Quaternion.Euler(0f, degrees, 0f) * step;
                string which = turn == 0 ? "the test's own point" : "turned " + degrees.ToString("+0.0;-0.0") + "°";
                if (!NavMesh.SamplePosition(candidate, out var hit, 0.75f, player.Agent.areaMask))
                {
                    tried.Add(which + " " + TalkPlan(candidate) + ": no floor within 0.75 m");
                    continue;
                }
                if (!player.Agent.Warp(hit.position))
                {
                    tried.Add(which + " " + TalkPlan(hit.position) + ": the agent refused the warp");
                    continue;
                }
                player.Agent.ResetPath();
                Physics.SyncTransforms();
                if (RswCanTalk(npc))
                {
                    if (turn > 0)
                        TestContext.WriteLine("WarpToTalk: the test's own point beside " + npc.DisplayName + " would not do; taken "
                            + which + " " + TalkPlan(hit.position) + ". Before it: " + string.Join("; ", tried) + ".");
                    return;
                }
                tried.Add(which + " " + TalkPlan(hit.position) + ": " + WhyTalkRefuses(npc));
            }
            string floor = director.WalkerRoomOf(npc.transform);
            Assert.Fail("No point " + new Vector2(step.x, step.z).magnitude.ToString("0.00") + " m from " + npc.DisplayName
                + " lets the player talk to them. They stand at " + TalkPlan(centre)
                + (floor != null ? " on the " + floor + " floor" : ", on no floor the room scope knows")
                + ". Tried: " + string.Join("; ", tried) + ".");
        }

        /// <summary>
        /// Why Talk refuses <paramref name="npc"/> from where the player stands, by its own tests in its
        /// order: the reach, the floors (one floor, or both in one doorway's band with no wall between
        /// their feet), the sight line.
        /// </summary>
        private string WhyTalkRefuses(HouseNpc npc)
        {
            var mine = player.transform.position;
            var theirs = npc.transform.position;
            // Talk measures between points 1.15 m over each pair of feet: the same offset.
            float gap = Vector3.Distance(mine, theirs);
            if (gap > 2.8f) return gap.ToString("0.00") + " m apart, past the 2.8 m reach";
            var scope = director.RoomScope;
            int here = scope != null ? scope.LastRoom(player.transform) : -1;
            int there = scope != null ? scope.LastRoom(npc.transform) : -1;
            if (here >= 0 && there >= 0 && here != there)
            {
                bool inTheirBand = HouseWhoIsWhere.InDoorwayBand(scope, there, mine);
                bool inMyBand = HouseWhoIsWhere.InDoorwayBand(scope, here, theirs);
                string band = HouseWhoIsWhere.DoorwayBand.ToString("0.0") + " m";
                return "the player on the " + HouseRoomScope.RoomId(here) + " floor, they on the " + HouseRoomScope.RoomId(there) + ": "
                    + (inTheirBand && inMyBand ? "both in the doorway band, with a wall between their feet"
                        : "the player " + (inTheirBand ? "within " : "beyond ") + band + " of their floor, they "
                          + (inMyBand ? "within " : "beyond ") + band + " of the player's");
            }
            return (here < 0 || there < 0 ? "the floors cannot say" : "on one floor") + ", and the sight line between them is blocked";
        }

        /// <summary>A point in plan, to the centimetre.</summary>
        private static string TalkPlan(Vector3 point) => "(" + point.x.ToString("0.00") + ", " + point.z.ToString("0.00") + ")";
    }
}
