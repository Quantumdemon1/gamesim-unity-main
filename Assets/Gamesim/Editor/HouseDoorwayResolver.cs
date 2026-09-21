using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gamesim.House;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Gamesim.Editor
{
    /// <summary>
    /// Switches off the furniture collision that costs the house something it used to have.
    ///
    /// <para>Giving the props bodies connects almost everything and cuts off a few things. A prop
    /// standing in an opening is not distinguishable by size or by name from the same prop standing
    /// against a wall - the Head of Household door reads as a 3.16 m sideboard, the yard bookcase as
    /// any other bookcase - so this does not try to recognise them. It bakes with the collision off
    /// to learn what the house could do, bakes again with it on, and backs off the smallest set of
    /// boxes that gets the difference back.</para>
    ///
    /// <para>Three things are measured, because a bake can take any of them away, and each was
    /// learned by losing it.</para>
    ///
    /// <para><b>Rooms</b>: every room marker must still route to every other. This one is obvious
    /// once you look - the Head of Household door standing across its own opening.</para>
    ///
    /// <para><b>Approaches</b>: every interaction anchor's approach point must still be on the mesh
    /// <em>and</em> routable from a room. A bay walled in by its own furniture samples perfectly well
    /// and cannot be walked into, so "is it on the mesh" is not the question. The dining set is the
    /// case: sixteen chairs 0.64 m apart around a table make a solid six-metre block with both of its
    /// own seats sealed inside, in a room whose corridors measure 1.04 m to the north and 0.78 m to
    /// the south of them when an agent of radius 0.5 needs more than a metre.</para>
    ///
    /// <para><b>Actor bindings</b>: every houseguest and the player must be able to bind where they
    /// stand, which <c>HouseNpcMotion</c> does by sampling the mesh within 0.25 m of its own feet.
    /// Nothing was watching this and it cost nineteen PlayMode tests: the competition yard's crates
    /// took the floor out from under Taylor Kim, two metres away, and the only evidence was "NPC
    /// carving did not clear to a safe floor binding" - the same message obstacle carving produced,
    /// meaning the same thing, naming neither the actor nor the reason.</para>
    ///
    /// <para>This matters more than a hand-written exclusion list would, because a list goes stale
    /// silently. Move the bookcase out of the doorway next week and a list still exempts it forever;
    /// this re-derives the answer from the house as it actually stands, and says in the hierarchy
    /// which prop was backed off and what it was costing.</para>
    ///
    /// <para>A backed-off proxy is switched off, not deleted, and carries the reason in its name. It
    /// is still there to be found, and a re-fit gives it its collision back so it has to earn the
    /// exemption again.</para>
    /// </summary>
    public static class HouseDoorwayResolver
    {
        /// <summary>
        /// How far from a lost approach point a box has to be to be worth testing.
        ///
        /// <para>Wide enough to reach a whole row of dining chairs from the approach they enclose,
        /// because backing off the three nearest only opens a bay nothing can walk into. Narrow
        /// enough that this stays a handful rather than the hundred and nineteen.</para>
        /// </summary>
        private const float Reach = 3f;

        /// <summary>How far from a lost route a box has to be to be what closed it: the agent's own radius.</summary>
        private const float Corridor = 0.5f;

        /// <summary>The tolerance the meeting coordinator samples an approach point with.</summary>
        private const float ApproachTolerance = 0.25f;

        /// <summary>The tolerance <c>HouseNpcMotion</c> binds a body to the mesh with.</summary>
        private const float ActorTolerance = 0.25f;

        /// <summary>Stops, so a house that cannot be reconnected reports rather than grinds.</summary>
        private const int MaxOff = 56;
        private const int MaxRounds = 40;

        [MenuItem("Gamesim/Resolve furniture collision against the bake")]
        public static void Resolve()
        {
            var surface = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include)
                .FirstOrDefault();
            if (surface == null || surface.navMeshData == null)
            { Debug.LogWarning("[Gamesim] Collision - no baked NavMeshSurface in this scene."); return; }

            var committed = surface.navMeshData;
            var markers = UnityEngine.Object.FindObjectsByType<HouseRoomMarker>(FindObjectsInactive.Include)
                .OrderBy(m => m.name, StringComparer.Ordinal).ToList();
            var anchors = UnityEngine.Object.FindObjectsByType<HouseInteractionAnchor>(FindObjectsInactive.Include)
                .OrderBy(a => a.VenueId, StringComparer.Ordinal).ThenBy(a => a.Slot).ToList();
            var proxies = UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include)
                .Select(b => b.transform).Where(HouseFurnitureCollision.IsProxy).ToList();

            // Every houseguest owns a carving NavMeshObstacle, and carving is live in edit mode as
            // well as in play. Left alone it puts each of them in a 0.90 m hole of their own making,
            // which is indistinguishable from furniture having taken their floor - it reported five
            // of six actors unbindable before a single prop was solid. Suppressed for the duration
            // and restored in the finally, so what is measured is the house rather than the cast.
            var carvers = UnityEngine.Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include)
                .Where(o => o.GetComponentInParent<HouseNpc>() != null).ToList();
            var carving = carvers.Select(o => o.carving).ToList();
            foreach (var carver in carvers) carver.carving = false;

            var sb = new StringBuilder();
            try
            {
                // The house before any of this: every box off. Measured rather than read off the
                // committed asset, so running this twice gives the same answer the second time. The
                // routes are captured here too, while they still exist to be captured.
                foreach (var proxy in proxies) proxy.gameObject.SetActive(false);
                var target = Bake(surface, markers, anchors, out _);
                var routes = Routes(markers);
                sb.AppendLine("[Gamesim] Collision - with no furniture collision at all the house reaches "
                              + target + " of " + anchors.Count + ". That is what the collision may not cost.");

                foreach (var proxy in proxies) proxy.gameObject.SetActive(true);
                var reached = Bake(surface, markers, anchors, out var lost);
                if (reached.AtLeast(target))
                {
                    sb.Append("With all " + proxies.Count + " boxes on it still reaches " + reached + ". Nothing to back off.");
                    Debug.Log(sb.ToString());
                    return;
                }
                sb.AppendLine("With all " + proxies.Count + " boxes on it reaches only " + reached + ".");

                // One lost thing at a time. A pooled greedy fails here: with eighteen lost room pairs
                // and seven walled-in approaches competing for a single ordered list, it emptied the
                // neighbourhood of the nearest losses and fixed neither. Backing a box off never costs
                // the house anything, so fixing each loss in turn and keeping the union is sound; a
                // loss that its own neighbourhood cannot fix is left for the next one rather than
                // ending the search, because several of them share a cause.
                var off = new List<Transform>();
                var tried = new HashSet<string>(StringComparer.Ordinal);
                for (int round = 0; round < MaxRounds && !reached.AtLeast(target) && off.Count < MaxOff; round++)
                {
                    var key = lost.FirstOrDefault(k => !tried.Contains(k));
                    if (key == null) break;
                    tried.Add(key);

                    var before = reached;
                    foreach (var candidate in Nearest(key, markers, anchors, routes, proxies))
                    {
                        if (off.Count >= MaxOff) break;
                        candidate.gameObject.SetActive(false);
                        off.Add(candidate);
                        reached = Bake(surface, markers, anchors, out lost);
                        // Any gain at all is progress: stop emptying this neighbourhood and go and
                        // look at what is still missing.
                        if (reached.Rooms > before.Rooms || reached.Approaches > before.Approaches
                            || reached.Actors > before.Actors) break;
                    }
                }

                if (!reached.AtLeast(target))
                {
                    foreach (var proxy in off) proxy.gameObject.SetActive(true);
                    sb.Append("Backing off " + off.Count + " boxes still only reaches " + reached
                              + ". Something other than furniture is in the way - nothing was changed. Still missing: "
                              + string.Join(", ", lost.Take(8)));
                    Debug.LogWarning(sb.ToString());
                    return;
                }

                // Then give them back, keeping only what the house cannot do without. A whole kind at
                // a time first: far fewer bakes, and "the dining chairs" is a better answer for
                // somebody reading this later than eleven of the sixteen picked by search order. Only
                // a kind that cannot come back wholesale is then tried one at a time.
                foreach (var group in off.GroupBy(p => p.parent.name).ToList())
                {
                    foreach (var each in group) each.gameObject.SetActive(true);
                    if (Bake(surface, markers, anchors, out _).AtLeast(target))
                    { foreach (var each in group) off.Remove(each); continue; }

                    foreach (var each in group) each.gameObject.SetActive(false);
                    if (group.Count() == 1) continue;
                    foreach (var each in group.ToList())
                    {
                        each.gameObject.SetActive(true);
                        if (Bake(surface, markers, anchors, out _).AtLeast(target)) off.Remove(each);
                        else each.gameObject.SetActive(false);
                    }
                }

                sb.AppendLine("Backed off " + off.Count + " of " + proxies.Count + " boxes:");
                foreach (var group in off.GroupBy(p => p.parent.name).OrderBy(g => g.Key, StringComparer.Ordinal))
                {
                    // One bake each, with the kind put back, purely to name what it was costing.
                    foreach (var each in group) each.gameObject.SetActive(true);
                    Bake(surface, markers, anchors, out var costs);
                    foreach (var each in group) each.gameObject.SetActive(false);
                    string why = costs.Count == 0 ? "reachability" : costs[0];
                    foreach (var each in group)
                    {
                        Undo.RecordObject(each.gameObject, "Resolve collision");
                        each.name = HouseFurnitureCollision.OffPrefix + why + ")";
                    }
                    sb.AppendLine("   " + group.Key.PadRight(26) + group.Count().ToString().PadLeft(3) + "   costs " + why);
                }

                var final = Bake(surface, markers, anchors, out _);
                EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
                sb.Append("A bake now reaches " + final + ", matching " + target
                          + ". The committed mesh is still in place - run the rebake to write it.");
                Debug.Log(sb.ToString());
            }
            finally
            {
                for (int i = 0; i < carvers.Count; i++) carvers[i].carving = carving[i];
                surface.navMeshData = committed;
                Register(surface, committed);
            }
        }

        /// <summary>
        /// What the house can do: rooms that reach each other, approach points you can stand on, and
        /// actors that can bind to the mesh where they stand.
        /// </summary>
        private readonly struct Score
        {
            public readonly int Rooms, Approaches, Actors;
            public Score(int rooms, int approaches, int actors) { Rooms = rooms; Approaches = approaches; Actors = actors; }
            public bool AtLeast(Score other) =>
                Rooms >= other.Rooms && Approaches >= other.Approaches && Actors >= other.Actors;
            public override string ToString() =>
                Rooms + " room pairs, " + Approaches + " approaches and " + Actors + " actor bindings";
        }

        private static Score Bake(NavMeshSurface surface, List<HouseRoomMarker> markers,
            List<HouseInteractionAnchor> anchors, out List<string> lost)
        {
            surface.BuildNavMesh();
            Register(surface, surface.navMeshData);
            return Measure(markers, anchors, out lost);
        }

        private static void Register(NavMeshSurface surface, NavMeshData data)
        {
            NavMesh.RemoveAllNavMeshData();
            if (data != null)
                NavMesh.AddNavMeshData(data, surface.transform.position, surface.transform.rotation);
        }

        private static Score Measure(List<HouseRoomMarker> markers, List<HouseInteractionAnchor> anchors,
            out List<string> lost)
        {
            var actors = Actors();
            lost = new List<string>();
            var grounded = new List<KeyValuePair<string, Vector3>>();
            foreach (var marker in markers)
                if (NavMesh.SamplePosition(marker.transform.position, out var hit, 3f, NavMesh.AllAreas))
                    grounded.Add(new KeyValuePair<string, Vector3>(marker.name, hit.position));

            var path = new NavMeshPath();
            int rooms = 0;
            for (int i = 0; i < grounded.Count; i++)
                for (int j = i + 1; j < grounded.Count; j++)
                    if (NavMesh.CalculatePath(grounded[i].Value, grounded[j].Value, NavMesh.AllAreas, path)
                        && path.status == NavMeshPathStatus.PathComplete) rooms++;
                    else lost.Add(Key(grounded[i].Key, grounded[j].Key));

            // Standing on the mesh is not enough: a bay walled in by its own furniture samples fine
            // and cannot be walked into. Every approach has to be routable from somewhere real.
            int approaches = 0;
            var from = grounded.Count == 0 ? (Vector3?)null : grounded[0].Value;
            foreach (var anchor in anchors)
            {
                bool ok = from.HasValue
                    && NavMesh.SamplePosition(anchor.Approach, out var hit, ApproachTolerance, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(from.Value, hit.position, NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete;
                if (ok) approaches++;
                else lost.Add(Key(anchor));
            }

            // Where the cast stands is a destination too, and the one nothing was watching. A
            // houseguest binds by sampling the mesh within 0.25 m of its own feet; take that away
            // and HouseNpcMotion gives up with "NPC carving did not clear to a safe floor binding",
            // which is the same message obstacle carving produced and means the same thing. Taylor
            // Kim stands in the competition yard 2 m from the crates, and the crates took the floor.
            int bound = 0;
            foreach (var actor in actors)
            {
                if (NavMesh.SamplePosition(actor.Value, out _, ActorTolerance, NavMesh.AllAreas)) bound++;
                else lost.Add(Key(actor.Key));
            }
            return new Score(rooms, approaches, bound);
        }

        /// <summary>The authored standing position of every houseguest and the player.</summary>
        private static List<KeyValuePair<string, Vector3>> Actors()
        {
            var found = new List<KeyValuePair<string, Vector3>>();
            foreach (var npc in UnityEngine.Object.FindObjectsByType<HouseNpc>(FindObjectsInactive.Include)
                         .OrderBy(n => n.name, StringComparer.Ordinal))
                found.Add(new KeyValuePair<string, Vector3>(npc.name, npc.transform.position));
            foreach (var player in UnityEngine.Object.FindObjectsByType<HousePlayerController>(FindObjectsInactive.Include)
                         .OrderBy(p => p.name, StringComparer.Ordinal))
                found.Add(new KeyValuePair<string, Vector3>(player.name, player.transform.position));
            return found;
        }

        private static string Key(string a, string b) => a + " / " + b;
        private static string Key(string actor) => "actor " + actor;
        private static string Key(HouseInteractionAnchor a) => a.VenueId + " slot " + a.Slot;

        /// <summary>Every room-to-room route, as the house walks them with no furniture collision at all.</summary>
        private static Dictionary<string, Vector3[]> Routes(List<HouseRoomMarker> markers)
        {
            var routes = new Dictionary<string, Vector3[]>(StringComparer.Ordinal);
            var path = new NavMeshPath();
            for (int i = 0; i < markers.Count; i++)
                for (int j = i + 1; j < markers.Count; j++)
                {
                    if (!NavMesh.SamplePosition(markers[i].transform.position, out var a, 3f, NavMesh.AllAreas)) continue;
                    if (!NavMesh.SamplePosition(markers[j].transform.position, out var b, 3f, NavMesh.AllAreas)) continue;
                    if (NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path)
                        && path.status == NavMeshPathStatus.PathComplete)
                        routes[Key(markers[i].name, markers[j].name)] = path.corners.ToArray();
                }
            return routes;
        }

        /// <summary>
        /// The live boxes standing in the way of one lost thing, nearest first.
        ///
        /// <para>A lost room pair is looked at along the route it used to take, because a doorway is
        /// nowhere near either room's marker. A lost approach point has no route to walk, so it is
        /// simply everything within reach of the point.</para>
        /// </summary>
        private static List<Transform> Nearest(string key, List<HouseRoomMarker> markers,
            List<HouseInteractionAnchor> anchors, Dictionary<string, Vector3[]> routes, List<Transform> proxies)
        {
            var anchor = anchors.FirstOrDefault(a => Key(a) == key);
            if (anchor != null) return Around(anchor.Approach, Reach, proxies);

            foreach (var actor in Actors())
                if (Key(actor.Key) == key) return Around(actor.Value, Reach, proxies);

            if (!routes.TryGetValue(key, out var corners)) return new List<Transform>();
            var found = new List<Transform>();
            for (int i = 0; i + 1 < corners.Length; i++)
            {
                var a = corners[i] + Vector3.up * 0.9f;
                var b = corners[i + 1] + Vector3.up * 0.9f;
                foreach (var hit in Physics.OverlapCapsule(a, b, Corridor, 1 << HouseLayers.Furniture,
                             QueryTriggerInteraction.Ignore))
                    if (proxies.Contains(hit.transform) && hit.gameObject.activeSelf && !found.Contains(hit.transform))
                        found.Add(hit.transform);
            }
            return found;
        }

        private static List<Transform> Around(Vector3 at, float radius, List<Transform> proxies)
        {
            var eye = at + Vector3.up * 0.9f;
            return Physics.OverlapSphere(eye, radius, 1 << HouseLayers.Furniture, QueryTriggerInteraction.Ignore)
                .Where(c => proxies.Contains(c.transform) && c.gameObject.activeSelf)
                .OrderBy(c => Vector3.Distance(c.ClosestPoint(eye), eye))
                .ThenBy(c => c.transform.parent.name, StringComparer.Ordinal)
                .Select(c => c.transform)
                .ToList();
        }
    }
}
