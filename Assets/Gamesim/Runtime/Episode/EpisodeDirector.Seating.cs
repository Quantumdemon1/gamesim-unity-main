using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Gamesim.Episode
{
    /// <summary>
    /// Where the season starts its people, and the one place the house got that wrong.
    ///
    /// <para>Every season seats scene body slot 1 on the anchor the setup pass authored at
    /// (0, 0, 15), and three authoring passes met there: <c>EpisodeProjectSetup</c> stood a
    /// houseguest on it, <c>EpisodeHouseDressing</c> carried the prototype's competition pendant to
    /// hang at head height over it, and <c>HouseSetPieces</c> put the centre lane's stacking prop half
    /// a metre behind it. Whoever took slot 1 began the season with their head in a lamp shade and
    /// their heels in a plinth, and no angle at the introductions could frame a face through that.</para>
    ///
    /// <para>The fix is a list of one, not a measurement. A runtime search that moved any spoiled
    /// slot to the first clear spot nearby would re-time free roam, with no diff to review, whenever
    /// the dressing changed; one explicit override is exact, moves nobody else, and a PlayMode audit
    /// (<c>EpisodePlayModeTests.StartSpots</c>) fails loudly, naming the prop, when any start spot is
    /// spoiled again. It is matched on the authored position rather than on a houseguest's id,
    /// because the problem belongs to the slot: a different cast still stands somebody there.</para>
    ///
    /// <para>The saved scene still carries the old anchor, and it is never re-saved from here, so the
    /// override lives at runtime; the editor's spawn table says the same for any scene the setup pass
    /// regenerates. Positions are presentation: nothing here reads or writes the season, draws from
    /// its generator or adds a saved field.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The anchor the setup pass authored on the competition course's centre lane.</summary>
        private static readonly Vector3 CourseAnchor = new Vector3(0f, 0f, 15f);

        /// <summary>
        /// Where that slot stands instead: open yard beside the lane, 1.3 m from the stack's centre,
        /// 1.25 m from the crate's and clear of the plinth by about 0.3 m. The height is the floor's,
        /// sampled when it is used.
        /// </summary>
        private static readonly Vector3 CourseAnchorMovedTo = new Vector3(1.1f, 0f, 14.8f);

        /// <summary>How near an authored spot, or a lamp's parts, has to be to count as the one meant.</summary>
        private const float AnchorMatch = 0.3f;

        private const string StartYard = "Yard";

        /// <summary>
        /// The course pieces that stand in the yard with no collider. The floor and clearance checks
        /// cannot see them, so the moved spot keeps a metre from each, as the opening stage's marks do.
        /// Measured by their origins, which their authoring scripts put on the floor under the centre.
        /// </summary>
        private static readonly string[] CourseStandingProps = { "bb_set_comp_stack", "bb_set_comp_crate" };
        private const float CourseKeepAway = 1.0f;

        /// <summary>The prototype's pendant over the centre lane: a shade and a cord, and no collider.</summary>
        private const string CompetitionLampName = "Lamp - Competition";

        /// <summary>The anchors as the scene authored them, by body slot, before the override. A read for tests.</summary>
        private Vector3[] authoredNpcPositions;

        /// <summary>Where the season starts each houseguest, by body slot: what a load, a skipped opening and the introductions all read. A copy.</summary>
        public IReadOnlyList<Vector3> StartSpots => initialNpcPositions != null ? (Vector3[])initialNpcPositions.Clone() : Array.Empty<Vector3>();

        /// <summary>The same spots as the scene authored them, before the override moved any. A copy, for tests.</summary>
        public IReadOnlyList<Vector3> AuthoredStartSpots => authoredNpcPositions != null ? (Vector3[])authoredNpcPositions.Clone() : Array.Empty<Vector3>();

        /// <summary>Where the season starts the player.</summary>
        public Vector3 PlayerStartSpot => initialPlayerPosition;

        /// <summary>
        /// Takes down the prototype's competition pendant before anybody is seated. It hung its shade
        /// at 1.54 to 1.90 m over the centre lane, so even with nobody starting under it, everyone
        /// who walked the lane put their head through it; the product owner's call was to remove it.
        ///
        /// <para>Found by its name and by where its parts hang, so a lamp of the same name anywhere
        /// else - or this one, moved - is never taken. The lamp's own transform sits at the scene
        /// origin and its shade and cord carry the placement, so it is their positions that are
        /// matched. Switched off as well as destroyed, because a destroy waits for the end of the frame
        /// and anything that counts props before then must not count it.</para>
        ///
        /// <para>The shade was lightmap-static (Contribute GI) and emissive, so the baked lighting
        /// may keep a faint trace of it under the lane until the house is next baked.</para>
        /// </summary>
        private void StrikeCompetitionLamp()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var node in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!IsCompetitionLamp(node)) continue;
                    node.gameObject.SetActive(false);
                    Destroy(node.gameObject);
                }
        }

        private static bool IsCompetitionLamp(Transform node)
        {
            if (node == null || node.name != CompetitionLampName) return false;
            var parts = node.GetComponentsInChildren<Renderer>(true);
            return parts.Length > 0 && parts.All(part => FlatDistance(part.transform.position, CourseAnchor) <= AnchorMatch);
        }

        private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>
        /// Keeps the anchors as the scene authored them, then moves the one on the competition course
        /// - and its body, so the season starts it there - to the clear yard spot beside it, keeping
        /// the way it was authored to face. Called by <see cref="SeatCast"/> whenever it rebuilds the
        /// anchors; an anchor already moved no longer matches, so running it twice moves nobody twice.
        ///
        /// <para>The body is moved the way a load places people, before anything is bound to it: at
        /// the start, before the house's world is first reconciled, and on an install, after the load
        /// has reset it. If the spot fails a check the authored anchor stays and a warning says why -
        /// and the audit test fails on it, so a broken override is never only a log line.</para>
        /// </summary>
        private void SeatOffTheCourse(Vector3[] positions, Quaternion[] rotations)
        {
            var authored = new Vector3[positions.Length];
            for (int i = 0; i < positions.Length; i++)
                authored[i] = authoredNpcPositions != null && i < authoredNpcPositions.Length ? authoredNpcPositions[i] : positions[i];
            authoredNpcPositions = authored;

            bool moved = false;
            for (int i = 0; i < positions.Length && i < housemates.Length; i++)
            {
                if (FlatDistance(positions[i], CourseAnchor) > AnchorMatch) continue;
                var body = housemates[i];
                if (!TryMoveOffTheCourse(body, positions[i], out var home, out var reason))
                {
                    Debug.LogWarning("Gamesim start spots: body slot " + i + (body != null ? " (" + body.name + ")" : "")
                        + " keeps its authored spot on the competition course at " + positions[i].ToString("F2") + ": " + reason + ".");
                    continue;
                }
                positions[i] = home;
                body.transform.SetPositionAndRotation(home, rotations[i]);
                moved = true;
            }
            if (moved) Physics.SyncTransforms();
        }

        /// <summary>
        /// The moved spot, checked the way binding and the opening stage's marks are checked: in the
        /// yard's safe interior, on the floor routing uses, clear of every body and obstacle with this
        /// body left out, and a metre from the course's standing props.
        /// </summary>
        private bool TryMoveOffTheCourse(HouseNpc body, Vector3 authored, out Vector3 home, out string reason)
        {
            home = authored;
            reason = null;
            if (body == null || !body.gameObject.activeInHierarchy) { reason = "no active body stands on it"; return false; }
            if (!HouseRoomQuery.TryCreate(gameObject.scene, out var rooms, out var failure)) { reason = failure; return false; }
            var capsule = body.GetComponent<CapsuleCollider>();
            float radius = capsule != null ? capsule.radius : 0.35f, height = capsule != null ? capsule.height : 1.9f;
            var agent = player != null ? player.Agent : null;
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = agent != null ? agent.agentTypeID : 0,
                areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas,
            };
            // At the authored anchor's own height, so the floor checks compare like with like.
            var wanted = new Vector3(CourseAnchorMovedTo.x, authored.y, CourseAnchorMovedTo.z);
            string where = wanted.ToString("F2");
            if (!rooms.TryLocate(wanted, radius, out var room) || room != StartYard)
                reason = where + " is not in the yard (" + (room ?? rooms.LastFailure) + ")";
            else if (!rooms.TrySampleFloor(wanted, radius, filter, .25f, out var sampled, out var sampledRoom) || sampledRoom != StartYard)
                reason = where + " is off the walkable floor (" + (sampledRoom ?? rooms.LastFailure) + ")";
            else if (!rooms.HasCapsuleClearance(sampled, radius, height, body.transform))
                reason = where + " is not clear: " + rooms.LastFailure;
            else
            {
                var prop = NearestCourseProp(sampled, out float distance);
                if (prop != null && distance < CourseKeepAway)
                    reason = where + " is " + distance.ToString("F2") + " m from the course's " + prop.name;
                else home = sampled;
            }
            return reason == null;
        }

        /// <summary>The course's nearest standing prop to a spot, by its origin, and how far it is across the floor.</summary>
        private Transform NearestCourseProp(Vector3 feet, out float distance)
        {
            Transform nearest = null;
            distance = float.PositiveInfinity;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var node in root.GetComponentsInChildren<Transform>())
                {
                    if (Array.IndexOf(CourseStandingProps, node.name) < 0) continue;
                    float flat = FlatDistance(node.position, feet);
                    if (flat < distance) { distance = flat; nearest = node; }
                }
            return nearest;
        }
    }

    /// <summary>
    /// What spoils a place for somebody to stand: a small prop the face would be inside or the body
    /// would stand in, or a piece of furniture the body would overlap.
    ///
    /// <para>The props are counted exactly as the introductions' framing counts them - small, near,
    /// taller than a table, and not a person or a canvas - so the audit and the camera agree on what
    /// spoils a face. The filter is a copy of <c>IntroductionYaw</c>'s in
    /// <c>EpisodeDirector.Opening.cs</c>, kept apart so this file can change without touching the
    /// framing; if one changes, change the other. Furniture is asked for separately, because the
    /// house's clearance query looks through its layer on purpose.</para>
    ///
    /// <para>A read for the audit test. Nothing in the running game moves anybody on its word.</para>
    /// </summary>
    public static class StartSpotAudit
    {
        /// <summary>Where a face is sampled, in metres over the feet.</summary>
        public static readonly float[] HeadHeights = { 1.35f, 1.5f, 1.7f };

        /// <summary>How far a prop's box is grown before it counts, as the framing grows it.</summary>
        public const float PropMargin = 0.12f;

        /// <summary>How far from the head a prop's centre can be and still be counted, as the framing counts it.</summary>
        private const float Reach = 6f;

        /// <summary>
        /// Everything that spoils a start spot, in words that name the prop: a face sample inside a
        /// small prop's box, the body's column crossing one, or its capsule overlapping furniture on
        /// <see cref="HouseLayers.Furniture"/>. Empty when the spot is clean.
        /// </summary>
        public static List<string> Spoilers(Scene scene, Vector3 feet, float radius, float height)
        {
            var found = new List<string>();
            var column = new Bounds(feet + Vector3.up * (height * 0.5f), new Vector3(radius * 2f, height, radius * 2f));
            foreach (var renderer in SmallProps(scene, feet + Vector3.up * 1.5f))
            {
                var box = renderer.bounds;
                box.Expand(PropMargin);
                float head = -1f;
                foreach (float at in HeadHeights)
                    if (box.Contains(feet + Vector3.up * at)) { head = at; break; }
                if (head > 0f) found.Add("head at " + head.ToString("F2") + " m inside " + PathOf(renderer.transform));
                else if (box.Intersects(column)) found.Add("body inside " + PathOf(renderer.transform));
            }

            Physics.SyncTransforms();
            var hits = new Collider[32];
            int count = scene.GetPhysicsScene().OverlapCapsule(feet + Vector3.up * radius, feet + Vector3.up * (height - radius),
                radius, hits, 1 << HouseLayers.Furniture, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (hits[i] != null) found.Add("capsule inside furniture " + PathOf(hits[i].transform));
            return found;
        }

        /// <summary>
        /// The props near a head that can spoil a face: lamps, poles, plants, stacks - not the floors,
        /// walls and furniture runs a room is made of, nor the houseguests, nor anything on a canvas.
        /// </summary>
        public static IEnumerable<Renderer> SmallProps(Scene scene, Vector3 head)
        {
            if (!scene.IsValid() || !scene.isLoaded) return Enumerable.Empty<Renderer>();
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Renderer>())
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy
                    && (renderer.bounds.center - head).sqrMagnitude < Reach * Reach
                    && Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.z) < 1.6f
                    && renderer.bounds.max.y > 0.9f
                    && renderer.GetComponentInParent<HouseNpc>() == null && renderer.GetComponentInParent<HousePlayerController>() == null
                    && renderer.GetComponentInParent<Canvas>() == null)
                .ToList();
        }

        /// <summary>A prop's name with up to two of its parents, so a message says which lamp, not just "Shade".</summary>
        public static string PathOf(Transform node)
        {
            var parts = new List<string>();
            for (var at = node; at != null && parts.Count < 3; at = at.parent) parts.Insert(0, at.name);
            return string.Join("/", parts);
        }
    }
}
