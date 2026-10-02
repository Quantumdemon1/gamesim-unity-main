using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The face of a ceremony screen in the house, as a place to draw a card on (CEREMONY-CUTSCENES-PLAN
    /// §3, "the cards on the screen"): where it is, which way it faces, how big it is, and the shot
    /// that fills the frame with it.
    ///
    /// <para>A card that plays on a screen is the same canvas the HUD draws, laid out at the screen's
    /// own shape (3:2) and hung on the face as a world-space canvas, so the camera can cut to the
    /// screen and see the keys and the faces on it the way the house does, and cut away to the
    /// people in their chairs. Nothing is rendered twice: the card is in the room, not over it.</para>
    ///
    /// <para>Measured from the prop's renderers rather than authored: the set's screen is a panel on
    /// a wall, and its thin axis is the way it faces. The living room's screen is the same prop the
    /// nomination room has, placed there at runtime by the ceremony seating.</para>
    /// </summary>
    public sealed class ScreenSurface
    {
        /// <summary>The set's ceremony screen, the episode station the player walks to.</summary>
        public const string PropName = "bb_set_ceremonyscreen";

        /// <summary>The card's frame on the screen, in canvas units: the screen's own shape.</summary>
        public const float ReferenceWidth = 1200f, ReferenceHeight = 800f;

        /// <summary>How much of the face the card takes; the rest is a bezel's worth round it.</summary>
        public const float FaceShare = 0.94f;

        /// <summary>How far in front of the face the card hangs, clear of the panel without floating.</summary>
        public const float Standoff = 0.012f;

        /// <summary>The lens the screen and the reaction shots share, so a cut never eases the lens.</summary>
        public const float FieldOfView = 40f;

        /// <summary>
        /// The idle graphic the room finish pass hangs on the face (ROOM-FINISH-PLAN.md §5.5), under
        /// the prop so the living room's clone carries it too. A card covers it while it shows.
        /// </summary>
        public const string IdleDisplayName = "Idle display";
        private static readonly Dictionary<Canvas, List<Renderer>> hiddenIdle = new Dictionary<Canvas, List<Renderer>>();

        public Transform Prop { get; }
        public string Room { get; }
        /// <summary>The middle of the face, in the world.</summary>
        public Vector3 Centre { get; }
        /// <summary>Out of the face, into the room.</summary>
        public Vector3 Normal { get; }
        /// <summary>The face's size in metres.</summary>
        public float Width { get; }
        public float Height { get; }

        /// <summary>Metres per canvas unit when the card's frame is fitted to the face.</summary>
        public float Scale => Mathf.Min(Width * FaceShare / ReferenceWidth, Height * FaceShare / ReferenceHeight);

        /// <summary>The way the camera looks to face the screen head-on, in degrees of yaw.</summary>
        public float LookYaw => Mathf.Atan2(-Normal.x, -Normal.z) * Mathf.Rad2Deg;

        private ScreenSurface(Transform prop, string room, Vector3 centre, Vector3 normal, float width, float height)
        {
            Prop = prop; Room = room; Centre = centre; Normal = normal; Width = width; Height = height;
        }

        /// <summary>
        /// Measures a screen prop: its renderers' bounds give the face's size, the thinner horizontal
        /// extent is the way it faces, and the side that faces <paramref name="roomCentre"/> is the
        /// front. Null when the prop has nothing to measure.
        /// </summary>
        public static ScreenSurface Measure(Transform prop, string room, Vector3 roomCentre)
        {
            if (prop == null) return null;
            // Not the idle graphic the room finish hangs on the face: it is drawn on the board, and
            // measuring it as the board would size the next one to it.
            var renderers = prop.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.GetComponent<Canvas>() == null
                && r.GetComponentInParent<Canvas>() == null && r.name != IdleDisplayName).ToList();
            if (renderers.Count == 0) return null;
            // The board, not the stage it stands on: the set's screen is a 2.6 × 1.5 board on a
            // 4 m stage (bb_set_ceremonyscreen.py), and the card belongs on the board. The authored
            // piece is one mesh, so the board is the sub-mesh the screen's glow material lights,
            // measured in the prop's own space and turned into the world; failing that, a part
            // named for it or lit by that material; failing that, the widest face off the floor;
            // failing that, the whole prop.
            Bounds bounds = default;
            bool found = false;
            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null) continue;
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length && i < mesh.subMeshCount; i++)
                {
                    if (slots[i] == null || !slots[i].name.StartsWith("bb_mat_glow_screen", System.StringComparison.Ordinal)) continue;
                    var local = mesh.GetSubMesh(i).bounds;
                    if (local.size.sqrMagnitude <= 0f) continue;
                    bounds = WorldBounds(local, renderer.transform);
                    found = true;
                    break;
                }
                if (found) break;
            }
            if (!found)
            {
                var board = renderers.FirstOrDefault(r => r.name.IndexOf("board", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || (r.sharedMaterial != null && r.sharedMaterial.name.IndexOf("screen", System.StringComparison.OrdinalIgnoreCase) >= 0));
                if (board == null)
                    board = renderers.Where(r => r.bounds.min.y > 0.5f).OrderByDescending(r => r.bounds.size.x * r.bounds.size.y).FirstOrDefault();
                if (board != null) bounds = board.bounds;
                else
                {
                    bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Count; i++) bounds.Encapsulate(renderers[i].bounds);
                }
            }
            bool facesZ = bounds.size.z <= bounds.size.x;
            var axis = facesZ ? Vector3.forward : Vector3.right;
            float thickness = facesZ ? bounds.size.z : bounds.size.x;
            float width = facesZ ? bounds.size.x : bounds.size.z;
            var toRoom = roomCentre - bounds.center;
            var normal = Vector3.Dot(toRoom, axis) >= 0f ? axis : -axis;
            var centre = bounds.center + normal * (thickness * 0.5f);
            return new ScreenSurface(prop, room, centre, normal, width, bounds.size.y);
        }

        /// <summary>A local-space box turned into the world's axis-aligned box round its eight corners.</summary>
        private static Bounds WorldBounds(Bounds local, Transform space)
        {
            var min = local.min; var max = local.max;
            var world = new Bounds(space.TransformPoint(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                world.Encapsulate(space.TransformPoint(new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z)));
            return world;
        }

        /// <summary>
        /// The ceremony screen in <paramref name="room"/>: the prop of that name whose place the house
        /// locates in the room, or the nearest to the room's marker. False when the room has none.
        /// </summary>
        public static bool TryFind(Scene scene, string room, out ScreenSurface surface)
        {
            surface = null;
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(room)) return false;
            var roots = scene.GetRootGameObjects();
            var marker = roots.SelectMany(r => r.GetComponentsInChildren<HouseRoomMarker>(true)).FirstOrDefault(m => m.RoomName == room);
            if (marker == null) return false;
            // The set's screen and its clones (the living room's is "bb_set_ceremonyscreen (Living)").
            var props = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name.StartsWith(PropName, System.StringComparison.Ordinal) && t.gameObject.activeInHierarchy).ToList();
            if (props.Count == 0) return false;
            Transform chosen = null;
            if (HouseRoomQuery.TryCreate(scene, out var rooms, out _))
                chosen = props.FirstOrDefault(p => rooms.TryLocate(p.position, 0.1f, out var at) && at == room);
            if (chosen == null)
            {
                // Screens hang on walls, which the room query may place just outside the room's
                // floor: the one nearest this room's marker, unless another room's is nearer to it.
                var markers = roots.SelectMany(r => r.GetComponentsInChildren<HouseRoomMarker>(true)).ToList();
                chosen = props.OrderBy(p => (p.position - marker.transform.position).sqrMagnitude)
                    .FirstOrDefault(p => markers.OrderBy(m => (m.transform.position - p.position).sqrMagnitude).First() == marker);
            }
            if (chosen == null) return false;
            surface = Measure(chosen, room, marker.transform.position);
            return surface != null;
        }

        /// <summary>
        /// Hangs a card's canvas on the face: world space, the card's frame fitted to the screen, a
        /// hair in front of it, facing the room. The card lays itself out in its frame as it would
        /// on the HUD; only where the frame is changes.
        /// </summary>
        public void Mount(Canvas canvas)
        {
            if (canvas == null) return;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = null;
            var root = (RectTransform)canvas.transform;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
            root.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);
            root.localScale = Vector3.one * Scale;
            root.SetPositionAndRotation(Centre + Normal * Standoff, Quaternion.LookRotation(-Normal, Vector3.up));
            // The idle graphic goes dark under the card and comes back when the card comes down.
            var idle = new List<Renderer>();
            if (Prop != null)
                foreach (var renderer in Prop.GetComponentsInChildren<Renderer>(true))
                    if (renderer.name == IdleDisplayName && renderer.enabled) { renderer.enabled = false; idle.Add(renderer); }
            if (idle.Count > 0) hiddenIdle[canvas] = idle;
        }

        /// <summary>Takes a canvas back off the screen: the HUD's own overlay, at the HUD's own size.</summary>
        public static void Unmount(Canvas canvas)
        {
            if (canvas == null) return;
            var root = (RectTransform)canvas.transform;
            root.localScale = Vector3.one;
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (hiddenIdle.TryGetValue(canvas, out var idle))
            {
                foreach (var renderer in idle) if (renderer != null) renderer.enabled = true;
                hiddenIdle.Remove(canvas);
            }
        }

        /// <summary>
        /// The screen shot: head-on, level, the face filling the frame's height with a little air
        /// round it - the "cut to the screen". A short move reads as a cut.
        /// </summary>
        public HouseCameraRig.Shot Shot(float seconds = 0.01f) => Framing(0f, 0f, seconds);

        /// <summary>The air round the face in the frame: the distance that fills the height, and a little more.</summary>
        private const float FrameAir = 1.08f;

        /// <summary>
        /// The screen shot raised by <paramref name="pitch"/> degrees and swung <paramref name="yaw"/>
        /// degrees round the face's centre, pulled back by what the near edge needs so the whole
        /// card still fits the frame's height: a raised lens brings the top edge nearer, a swung one
        /// a side. Zero and zero is <see cref="Shot(float)"/>.
        /// </summary>
        public HouseCameraRig.Shot Framing(float pitch, float yaw, float seconds = 0.01f)
        {
            float fit = (Height * 0.5f) / Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
            float near = Width * 0.5f * Mathf.Sin(Mathf.Abs(yaw) * Mathf.Deg2Rad) + Height * 0.5f * Mathf.Sin(Mathf.Abs(pitch) * Mathf.Deg2Rad);
            return new HouseCameraRig.Shot
            {
                Focus = Centre, Distance = Mathf.Max(0.6f, (fit + near) * FrameAir), Pitch = pitch, Yaw = LookYaw + yaw,
                FieldOfView = FieldOfView, Seconds = seconds, DepthOfFieldWeight = 0f,
            };
        }

        // ---------------------------------------------------------------- the lens kept clear of bodies (UI-UX-PASS-PLAN K0)

        /// <summary>A houseguest's height and half-width, as the shot reckons a body that stands.</summary>
        public const float StandingHeight = 1.8f, BodyRadius = 0.34f;

        /// <summary>From a rig's head bone, which sits about the ears, to the top of the head.</summary>
        public const float HeadCrown = 0.15f;

        /// <summary>
        /// A body as the screen shot sees it: an upright cylinder from its feet to the top of its
        /// head, <see cref="Radius"/> round. The seated and the standing alike - a seat moves the
        /// visual body and leaves the root at the chair's approach, so the caller says where the
        /// feet and the head are (<see cref="BodyOf"/> reads them from the scene).
        /// </summary>
        public readonly struct Body
        {
            public readonly Vector3 Feet, Head;
            public readonly float Radius;

            public Body(Vector3 feet, Vector3 head, float radius = BodyRadius)
            {
                Feet = feet; Head = head; Radius = radius;
            }

            /// <summary>A body standing at <paramref name="feet"/>, a houseguest tall.</summary>
            public static Body Standing(Vector3 feet) => new Body(feet, feet + Vector3.up * StandingHeight);
        }

        /// <summary>
        /// A houseguest's or the player's body as the shot sees it, from the scene: the top of the
        /// rig's head where the body has a head bone, over the visual body's feet where a seat has
        /// moved it off its root, else under the head - a body can stand away from its root, and the
        /// head bone is where its mesh is; without a head bone, the seat's focus, or a houseguest's
        /// height over the root.
        /// </summary>
        public static Body BodyOf(Component body)
        {
            var seat = body.GetComponent<HouseSeatPresentation>();
            bool seated = seat != null && seat.Active;
            var root = body.transform.position;
            var visual = body.GetComponent<CharacterPresentation>();
            var bone = visual != null ? visual.HeadBone : null;
            if (bone != null)
            {
                var head = bone.position + Vector3.up * HeadCrown;
                return new Body(seated ? seat.VisualFeet : new Vector3(head.x, root.y, head.z), head);
            }
            var feet = seated ? seat.VisualFeet : root;
            return new Body(feet, seated ? seat.VisualFocus + Vector3.up * HeadCrown : feet + Vector3.up * StandingHeight);
        }

        /// <summary>
        /// The framings a cut tries when a body stands between the lens and the card, nearest the
        /// head-on shot first: raised a little, swung a little to either side, then more of each.
        /// A lens raised 26 degrees at the screen's distance stands about a metre above the face's
        /// centre, under a room's ceiling; a swing of 40 keeps the card readable.
        /// </summary>
        private static readonly (float pitch, float yaw)[] ClearFramings =
        {
            (0f, 0f), (8f, 0f), (0f, 12f), (0f, -12f), (8f, 12f), (8f, -12f), (14f, 0f), (14f, 18f), (14f, -18f),
            (0f, 24f), (0f, -24f), (8f, 24f), (8f, -24f), (20f, 0f), (20f, 26f), (20f, -26f), (14f, 32f), (14f, -32f),
            (26f, 0f), (26f, 34f), (26f, -34f), (8f, 40f), (8f, -40f),
        };

        /// <summary>How many points along a body, its feet and the top of its head included, the shot tests: one every 0.3 m of a houseguest standing.</summary>
        private const int BodyPoints = 7;

        /// <summary>
        /// The screen shot with the lens kept clear of <paramref name="bodies"/>: the head-on shot
        /// when no body stands between it and the card, else the first of a few raised and swung
        /// framings that none does, and the one with the least of anyone in it when every framing
        /// has somebody. A framing <paramref name="usable"/> turns down - one whose boom a wall
        /// would shorten, cropping the card - is passed over. The staging's first line of defence
        /// is the marks beside the screen; this is the second, for a houseguest still walking to a
        /// chair or a seat the shot stands past. Allocates nothing: it runs at every cut.
        /// </summary>
        public HouseCameraRig.Shot ShotClearOf(IReadOnlyList<Body> bodies, float seconds = 0.01f, System.Func<HouseCameraRig.Shot, bool> usable = null)
        {
            var best = Shot(seconds);
            if (bodies == null || bodies.Count == 0) return best;
            int least = int.MaxValue;
            for (int f = 0; f < ClearFramings.Length; f++)
            {
                var shot = Framing(ClearFramings[f].pitch, ClearFramings[f].yaw, seconds);
                if (usable != null && !usable(shot)) continue;
                var lens = LensFor(shot);
                int inTheWay = 0;
                for (int i = 0; i < bodies.Count; i++) inTheWay += Intrusion(lens, bodies[i]);
                if (inTheWay == 0) return shot;
                if (inTheWay < least) { least = inTheWay; best = shot; }
            }
            return best;
        }

        /// <summary>Whether <paramref name="body"/> stands in the card from <paramref name="shot"/>: any part of it, between the lens and the face, in the card's frame.</summary>
        public bool InTheShot(in HouseCameraRig.Shot shot, in Body body) => Intrusion(LensFor(shot), body) > 0;

        /// <summary>Where the lens stands for <paramref name="shot"/>: the rig puts it the shot's distance behind the focus, turned by the shot's pitch and yaw.</summary>
        public static Vector3 Eye(in HouseCameraRig.Shot shot) =>
            shot.Focus + Quaternion.Euler(shot.Pitch, shot.Yaw, 0f) * Vector3.back * shot.Distance;

        /// <summary>The card's four world corners on the face, as <see cref="Mount"/> hangs it: bottom-left, top-left, top-right, bottom-right. For tests; the shot reads them without the array.</summary>
        public Vector3[] CardCorners() => new[] { CardCorner(0), CardCorner(1), CardCorner(2), CardCorner(3) };

        /// <summary>One of the card's corners, in <see cref="CardCorners"/>' order.</summary>
        private Vector3 CardCorner(int index)
        {
            var right = Vector3.Cross(Vector3.up, -Normal).normalized;
            float across = index < 2 ? -1f : 1f, up = index == 1 || index == 2 ? 1f : -1f;
            return Centre + right * (across * ReferenceWidth * Scale * 0.5f) + Vector3.up * (up * ReferenceHeight * Scale * 0.5f);
        }

        /// <summary>
        /// A framing as the body test reads it, worked out once a framing: where the lens stands, the
        /// turn into its own space, the card's box in its tangent plane, and how far it looks down -
        /// the share of a body's round top and foot a raised lens sees.
        /// </summary>
        private readonly struct Lens
        {
            public readonly Vector3 Eye;
            public readonly Quaternion ToView;
            public readonly float UMin, UMax, WMin, WMax, Tilt;

            public Lens(Vector3 eye, Quaternion toView, float uMin, float uMax, float wMin, float wMax, float tilt)
            {
                Eye = eye; ToView = toView; UMin = uMin; UMax = uMax; WMin = wMin; WMax = wMax; Tilt = tilt;
            }
        }

        private Lens LensFor(in HouseCameraRig.Shot shot)
        {
            var rotation = Quaternion.Euler(shot.Pitch, shot.Yaw, 0f);
            var eye = shot.Focus + rotation * Vector3.back * shot.Distance;
            var toView = Quaternion.Inverse(rotation);
            float uMin = float.MaxValue, uMax = float.MinValue, wMin = float.MaxValue, wMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var view = toView * (CardCorner(i) - eye);
                float depth = Mathf.Max(0.05f, view.z);
                float u = view.x / depth, w = view.y / depth;
                uMin = Mathf.Min(uMin, u); uMax = Mathf.Max(uMax, u);
                wMin = Mathf.Min(wMin, w); wMax = Mathf.Max(wMax, w);
            }
            return new Lens(eye, toView, uMin, uMax, wMin, wMax, Mathf.Abs(Mathf.Sin(shot.Pitch * Mathf.Deg2Rad)));
        }

        /// <summary>
        /// How much of <paramref name="body"/> the shot sees in the card: of the
        /// <see cref="BodyPoints"/> points along it, feet to the top of the head, how many project
        /// into the card's frame from in front of the face, each the body's round cross-section -
        /// its whole radius sideways, and up and down only what a raised lens sees of its top and
        /// foot - plus one for each pair of neighbours either side of the card's height, which the
        /// body crosses there however thin the card's slice at its distance. Zero when it is clear of
        /// the card, behind the lens, or on or behind the face (against the board, or through a
        /// cutaway). Measured in the lens's own tangent plane, so the frame's aspect is no part of
        /// it: the card's frame is the card's, whatever the screen's shape.
        /// </summary>
        public int Intrusion(in HouseCameraRig.Shot shot, in Body body) => Intrusion(LensFor(shot), body);

        private int Intrusion(in Lens lens, in Body body)
        {
            int points = 0, lastSide = 0;
            bool lastAcross = false;
            for (int i = 0; i < BodyPoints; i++)
            {
                var point = Vector3.Lerp(body.Feet, body.Head, i / (float)(BodyPoints - 1));
                var view = lens.ToView * (point - lens.Eye);
                // Behind the lens, or on or behind the face: not between them. Against the face's
                // plane, not the nearest corner's depth - a raised or swung lens sees the card's far
                // side deeper than its near corner, and a body in front of the face is in front of it.
                if (view.z <= 0f || Vector3.Dot(point - Centre, Normal) <= 0f) { lastAcross = false; continue; }
                // At the lens: in the way whatever the frame.
                if (view.z < 0.05f) { points++; lastAcross = false; continue; }
                float u = view.x / view.z, w = view.y / view.z;
                float across = body.Radius / view.z, along = across * lens.Tilt;
                float du = Mathf.Max(Mathf.Max(lens.UMin - u, 0f), u - lens.UMax);
                bool inAcross = du <= across;
                int side = w < lens.WMin - along ? -1 : w > lens.WMax + along ? 1 : 0;
                if (inAcross && side == 0)
                {
                    float dw = Mathf.Max(Mathf.Max(lens.WMin - w, 0f), w - lens.WMax);
                    float a = du / across, b = along > 1e-6f ? dw / along : 0f;
                    if (a * a + b * b <= 1f) points++;
                }
                else if (inAcross && lastAcross && side * lastSide < 0) points++;
                lastAcross = inAcross && side != 0;
                lastSide = side;
            }
            return points;
        }

        /// <summary>Every ceremony screen in the scene, by the room it stands in.</summary>
        public static IEnumerable<ScreenSurface> All(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            var rooms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HouseRoomMarker>(true))
                .Select(m => m.RoomName).Distinct().ToList();
            foreach (var room in rooms)
                if (TryFind(scene, room, out var surface)) yield return surface;
        }
    }
}
