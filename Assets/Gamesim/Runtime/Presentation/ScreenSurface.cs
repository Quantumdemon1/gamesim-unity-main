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
            var renderers = prop.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.GetComponent<Canvas>() == null
                && r.GetComponentInParent<Canvas>() == null).ToList();
            if (renderers.Count == 0) return null;
            // The board, not the stage it stands on: the set's screen is a 2.6 × 1.5 board on a
            // 4 m stage (bb_set_ceremonyscreen.py), and the card belongs on the board. Its part is
            // named for it and lit by the screen's material; failing both, the widest face off
            // the floor; failing that, the whole prop.
            var board = renderers.FirstOrDefault(r => r.name.IndexOf("board", System.StringComparison.OrdinalIgnoreCase) >= 0
                || (r.sharedMaterial != null && r.sharedMaterial.name.IndexOf("screen", System.StringComparison.OrdinalIgnoreCase) >= 0));
            if (board == null)
                board = renderers.Where(r => r.bounds.min.y > 0.5f).OrderByDescending(r => r.bounds.size.x * r.bounds.size.y).FirstOrDefault();
            Bounds bounds;
            if (board != null) bounds = board.bounds;
            else
            {
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Count; i++) bounds.Encapsulate(renderers[i].bounds);
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
        }

        /// <summary>Takes a canvas back off the screen: the HUD's own overlay, at the HUD's own size.</summary>
        public static void Unmount(Canvas canvas)
        {
            if (canvas == null) return;
            var root = (RectTransform)canvas.transform;
            root.localScale = Vector3.one;
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        /// <summary>
        /// The screen shot: head-on, level, the face filling the frame's height with a little air
        /// round it - the "cut to the screen". A short move reads as a cut.
        /// </summary>
        public HouseCameraRig.Shot Shot(float seconds = 0.01f)
        {
            float distance = (Height * 0.5f) / Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) * 1.08f;
            return new HouseCameraRig.Shot
            {
                Focus = Centre, Distance = Mathf.Max(0.6f, distance), Pitch = 0f, Yaw = LookYaw,
                FieldOfView = FieldOfView, Seconds = seconds, DepthOfFieldWeight = 0f,
            };
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
