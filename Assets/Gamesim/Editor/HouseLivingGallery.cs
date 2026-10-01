using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// The living room as the eviction's set (MOCKUP-PASS-PLAN M21, CEREMONY-CUTSCENES-PLAN 8.2.3):
    /// a U of cream couches round a low table, facing two red wingbacks in front of the room's own
    /// screen on the south wall, so the whole house sits together for the vote and the two nominees
    /// sit apart in red. The pieces themselves are rows of <see cref="HouseSetPieces"/>' plan; this is
    /// what the placement pass does around them before the collision is fitted and the NavMesh is
    /// baked, so the bake sees the room as it will be played.
    ///
    /// <para><b>The U.</b> Two four-seat couches make its base at z -2.3, facing south; a three-seat
    /// couch on each arm faces in, its front flush with the base's first seat, spanning z -4.26 to
    /// -6.94. The corner between the base's front and an arm's end is 1.49 m, open floor after the
    /// bake's erosion, and the lane between the base and the table 1.36 m, so the U is walked into
    /// from its corners as well as past the red chairs, and every seat's approach stands clear of
    /// every piece's baked edge. With the arms tucked under the base's ends, as 8.2.3 first drew
    /// them, the base's outer seats would face the arms at 0.45 m and have no floor to be walked to.
    /// Fourteen seats; with the two red chairs, sixteen, the largest house.</para>
    ///
    /// <para><b>The red chairs</b> stand 1.1 m apart in front of the screen's stage, facing the U.
    /// The low table's baked edge meets the chairs' own in front of them, so they are approached from
    /// their outer sides.</para>
    ///
    /// <para><b>Around them:</b> the prototype's sofa, coffee table, rug and television, which stood
    /// where the U stands, are switched off (kept for the layout record, as the room finish keeps the
    /// rugs it covers); the living room's screen is named apart from the nomination room's, so each
    /// room finds its own; the room's marker, and the chat pair that stood inside the U, step out of
    /// it; and the three bodies the scene started where the U stands start on the U's own open floor.</para>
    /// </summary>
    internal static class HouseLivingGallery
    {
        public const string RoomFloor = "Living room floor";

        /// <summary>
        /// The room marker's new place: where it was, stepped 0.6 m south, clear of the east arm's
        /// baked edge. It must stay north of the screen's board (its face at z -8.32): ScreenSurface
        /// measures which way the board faces from the marker, and a marker behind it flips the screen.
        /// </summary>
        public static readonly Vector3 Marker = new Vector3(-5f, 0f, -7.6f);

        /// <summary>The living room's chat pair, out of the U into the east lane.</summary>
        public static readonly Vector3[] ChatPair = { new Vector3(-3.4f, 0f, -3.0f), new Vector3(-2.0f, 0f, -3.0f) };

        /// <summary>The prototype's pieces the U replaces, and their furnishing.</summary>
        private static readonly string[] Struck =
        {
            "Sofa seat", "Sofa seat (model)", "Sofa back", "Coffee table", "Coffee table (model)",
            "Television", "Television console", "Living rug", "Living rug (model)",
        };

        /// <summary>
        /// The broadcast dressing's living-room decor that belonged to the old layout: the books and
        /// vase that stood on the struck coffee table (they would float over the base's lane), the
        /// pendant over it (head height, and in the eviction's wide), and the frame on the south wall
        /// (under the screen's board now).
        /// </summary>
        private static readonly string[] StruckDecor = { "Books A", "Books B", "Vase", "Lamp - Living", "Frame - Living South" };

        /// <summary>The U and the chairs, grown by a stride: no body starts inside it (x, z).</summary>
        public static readonly Rect Footprint = Rect.MinMaxRect(-12.8f, -8.2f, -3.3f, -1.3f);

        /// <summary>
        /// The living room's exit door and its swing, at the south end of the west wall
        /// (MOCKUP-PASS-PLAN M23; the door set's <c>DoorLayout.Living</c>): from the west wall's face
        /// to a stride in front of the leaves, and from the south wall to the memory wall (x, z).
        /// </summary>
        public static readonly Rect Doorway = Rect.MinMaxRect(-13.9f, -9.9f, -11.0f, -7.3f);

        /// <summary>The prototype's pieces that stood in the exit door's doorway: the raised bed and the sphere on it.</summary>
        private static readonly string[] StruckFromTheDoorway = { "Planter", "Foliage" };

        /// <summary>
        /// Strikes the prototype's planter from the living room's exit door (MOCKUP-PASS-PLAN M23),
        /// with its foliage. A raised bed 0.6 m tall at (-12, -8), it stood in the right leaf's
        /// swing, and the bake took its top for floor: a route from the red chairs to the door
        /// climbed over it, and a body would have floated in the vestibule. Runs before the planting
        /// pass, which grows a bed of plants on every planter it finds and passes struck ones by;
        /// switched off rather than deleted, as the gallery keeps the pieces it replaces, so the bake
        /// sees no collider there. Idempotent. Returns how many pieces it struck.
        /// </summary>
        internal static int StrikeTheDoorway(Transform world)
        {
            int struck = 0;
            foreach (var node in world.GetComponentsInChildren<Transform>(true))
            {
                if (node == null || !node.gameObject.activeSelf) continue;
                if (!StruckFromTheDoorway.Contains(HouseSetPieces.Stem(node.name))) continue;
                if (!Doorway.Contains(new Vector2(node.position.x, node.position.z))) continue;
                node.gameObject.SetActive(false);
                struck++;
            }
            return struck;
        }

        /// <summary>
        /// Where the three bodies the scene started where the U stands start instead: inside the U,
        /// on its open floor, 1.35 m and more from every seat's approach and clear of the table - the
        /// house at home round its couches. Not the east lane beside the arm: a body standing there
        /// leaves 0.2 m between itself and the arm's baked edge, and every walker the path sends
        /// along the arm stops against it (measured 2026-09-29: the hot tub's companion, from the
        /// south, stood still at the pinch for the whole twenty seconds). Placed by name every run.
        /// Anyone else the scene starts inside the footprint takes the next spare.
        /// </summary>
        private static readonly Dictionary<string, Vector3> Moved = new Dictionary<string, Vector3>
        {
            { "Player", new Vector3(-8.0f, 0f, -4.2f) },
            { "Maya Hassan", new Vector3(-9.4f, 0f, -4.6f) },
            { "Casey Wilson", new Vector3(-6.6f, 0f, -4.6f) },
        };
        private static readonly Vector3[] Spare = { new Vector3(-1.8f, 0f, -7.4f), new Vector3(-1.8f, 0f, -4.4f) };

        /// <summary>
        /// Batch entry for the whole pass, in the order the placement pass asks for: the pieces with
        /// the collision, the back-off and the NavMesh bake; the room finish over them; then the
        /// lighting and its bake, so no new piece is left outside the lightmaps (9faf00d). Runs on a
        /// D: copy by -executeMethod; the scene, the NavMesh, the lighting and the new assets and
        /// their metas are copied back.
        /// </summary>
        public static void BuildFromCommandLine()
        {
            HouseSetPieces.Apply();
            HouseRoomFinish.Apply();
            HouseCinematicLighting.ApplyAndBakeFromCommandLine();
        }

        /// <summary>
        /// Dresses the room around the plan's pieces. Returns a line for the pass's report. Idempotent:
        /// struck pieces stay struck, a moved marker or body is already where it goes.
        /// </summary>
        internal static string Dress(Transform world, Transform setPieces)
        {
            var floor = world.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == RoomFloor && t.GetComponent<Renderer>() != null);
            if (floor == null) return "no living room floor; the gallery was not dressed";
            var bounds = floor.GetComponent<Renderer>().bounds;
            bool InRoom(Vector3 p) => p.x >= bounds.min.x && p.x <= bounds.max.x && p.z >= bounds.min.z && p.z <= bounds.max.z;

            int struck = 0;
            foreach (var node in world.GetComponentsInChildren<Transform>(true))
            {
                if (node == null || node.IsChildOf(setPieces) || !Struck.Contains(node.name) || !InRoom(node.position)) continue;
                // The room finish's own rug has the prototype's rug's name; it is the finish's to rebuild.
                if (node.GetComponentsInParent<Transform>(true).Any(p => p.name == HouseRoomFinish.RootName)) continue;
                if (!node.gameObject.activeSelf) continue;
                node.gameObject.SetActive(false);
                struck++;
            }

            // Outside the world root: the broadcast dressing's decor over the old layout, and the
            // fitted practicals that lit the television, which would bake a blue glow behind the U.
            var scene = world.gameObject.scene;
            var everything = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToList();
            foreach (var node in everything)
            {
                if (node == null || !node.gameObject.activeSelf || !InRoom(node.position)) continue;
                bool decor = StruckDecor.Contains(node.name) && node.GetComponentsInParent<Transform>(true).Any(p => p.name == "Decor");
                bool practical = node.name.StartsWith("Television", System.StringComparison.Ordinal)
                    && node.name.EndsWith("practical", System.StringComparison.Ordinal) && node.GetComponent<Light>() != null;
                if (!decor && !practical) continue;
                node.gameObject.SetActive(false);
                struck++;
            }

            // The room's screen, named apart: CeremonySets finds the living room's by this name and
            // the nomination room's by the plain one.
            int screens = 0;
            foreach (var screen in setPieces.GetComponentsInChildren<Transform>(true).Where(t => t.name == ScreenSurface.PropName && InRoom(t.position)).ToList())
            {
                screen.name = CeremonySets.LivingScreenName;
                screens++;
            }

            // The marker steps out of the east arm's baked edge. The chat pair hangs under it, so it
            // is placed after the marker moves.
            var marker = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HouseRoomMarker>(true))
                .FirstOrDefault(m => m.RoomName == CeremonySets.LivingRoom);
            bool markerMoved = false;
            if (marker != null && (marker.transform.position - Marker).sqrMagnitude > 0.0001f)
            {
                marker.transform.position = new Vector3(Marker.x, marker.transform.position.y, Marker.z);
                markerMoved = true;
            }
            int chats = 0;
            foreach (var anchor in HouseInteractionAnchors.InScene(scene).Where(a => a.VenueId == "living-east-chat"))
            {
                if (anchor.Slot < 0 || anchor.Slot >= ChatPair.Length) continue;
                var at = ChatPair[anchor.Slot];
                anchor.transform.position = new Vector3(at.x, anchor.transform.position.y, at.z);
                chats++;
            }

            // The bodies the scene starts inside the U.
            var moved = new List<string>();
            int spare = 0;
            var bodies = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(t => t.GetComponent<HouseNpc>() != null || t.GetComponent<HousePlayerController>() != null);
            foreach (var body in bodies)
            {
                var p = body.position;
                Vector3 to;
                if (Moved.TryGetValue(body.name, out to))
                {
                    if (new Vector2(p.x - to.x, p.z - to.z).sqrMagnitude < 0.0001f) continue;
                }
                else
                {
                    if (!Footprint.Contains(new Vector2(p.x, p.z))) continue;
                    if (spare >= Spare.Length) { moved.Add(body.name + " (no spare spot; left where it stood)"); continue; }
                    to = Spare[spare++];
                }
                body.position = new Vector3(to.x, p.y, to.z);
                moved.Add(body.name + " to (" + to.x.ToString("0.0") + ", " + to.z.ToString("0.0") + ")");
            }

            return "the gallery: " + struck + " prototype pieces struck, " + screens + " screen named for the room, marker "
                + (markerMoved ? "moved" : "in place") + ", " + chats + " chat anchors placed, bodies moved: "
                + (moved.Count == 0 ? "none" : string.Join("; ", moved));
        }
    }
}
