using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Gamesim.House
{
    /// <summary>
    /// The set dressing behind <see cref="CeremonySeating"/>, placed at runtime from the scene's own
    /// props (CEREMONY-CUTSCENES-PLAN §2.1, §2.4).
    ///
    /// <para><b>The nomination table.</b> The scene dresses six chairs round the round table at 60°.
    /// A house of N draws N−1 keys, so the ring is re-laid for N−1 chairs: the dressed chairs are
    /// moved onto the ring, more are cloned from them when the house is bigger than seven, and the
    /// extras are struck when it is smaller. The ring keeps one slot empty at the head, the side
    /// nearest the screen, where the Head of Household stands, and grows outward when N−1 chairs
    /// would sit closer together than a chair's width. Each chair gets a seated anchor
    /// (<see cref="CeremonySeating.NominationSeat"/>) approached from behind, as the dining chairs
    /// are.</para>
    ///
    /// <para><b>The living room.</b> The prototype's television stands in for the screen the format
    /// reads the vote on: the nomination room's ceremony screen is cloned into its place, facing
    /// the room, and the television goes. Two chairs cloned from the table's face it side by side
    /// - the hot seats - the sofa is turned to face it with its cushions, and gets three seats, and
    /// standing marks are laid behind the hot seats for the rest of the house, with one beside the
    /// screen for the Head of Household.</para>
    ///
    /// <para>Nothing here is saved: the scene is never re-saved for it, a reload dresses it again,
    /// and a house dressed differently - no table, no sofa - simply gets fewer seats, never
    /// phantom ones. Presentation only.</para>
    /// </summary>
    public static class CeremonySets
    {
        public const string RootName = "Ceremony sets";
        public const string NominationRoom = "Nomination";
        public const string LivingRoom = "Living";
        /// <summary>The dressed chairs' catalogue id (HouseSetPieces), which the placed instances are named by.</summary>
        public const string ChairName = "chairModernCushion";
        public const string TableName = "tableRound";
        public const string SofaName = "loungeDesignSofa";
        public const string LivingFloorName = "Living room floor";
        /// <summary>The prototype's television, which the living room's screen replaces.</summary>
        public const string TelevisionName = "Television";
        public const string TelevisionConsoleName = "Television console";
        /// <summary>The living room's screen: the set's screen prop, cloned, named so the episode station never mistakes it for its own.</summary>
        public const string LivingScreenName = ScreenSurface.PropName + " (Living)";

        /// <summary>The least room between chairs along the ring: a chair is about 0.57 wide.</summary>
        public const float ChairPitch = 0.75f;
        /// <summary>The dressed ring's radius; the ring never comes in closer than this.</summary>
        public const float RingRadius = 1.10f;
        /// <summary>How far behind a chair, or in front of a sofa seat, a body waits before it sits: the dining chairs' own distance.</summary>
        public const float SeatApproach = 0.70f;
        /// <summary>A seat's height as a share of the chair's: 0.46 of the authored dining chair's 1.06.</summary>
        public const float SeatShare = 0.434f;
        /// <summary>The authored sofa's seat: 0.42 of 0.80 tall.</summary>
        public const float SofaSeatShare = 0.525f;
        /// <summary>The hot seats' distance in front of the screen's stage, and how far apart they are.</summary>
        public const float HotSeatDistance = 2.0f, HotSeatGap = 1.1f;
        /// <summary>
        /// How far behind the hot seats the standing marks are laid, and how far apart across and
        /// between rows: 1.3 m, because a body bound for the back row passes between two standing
        /// in the front, and two roots a metre apart leave 0.4 m for a body 0.6 m wide (measured
        /// 2026-09-28: the last of sixteen crawled at 0.1 m/s behind the front row for the whole card).
        /// </summary>
        public const float MarkRowDistance = 1.7f, MarkPitch = 1.3f;
        /// <summary>The least room between a head mark and any chair's approach, or the other mark: two standing roots and a little more, since an arrival is refused while roots overlap.</summary>
        public const float HeadMarkClearance = 0.7f;

        /// <summary>What the dressing was made for, kept on the root so a house of the same size is left alone.</summary>
        private sealed class Dressing : MonoBehaviour
        {
            public int houseguests;
            public bool livingDressed;
        }

        /// <summary>Dresses the nomination room and the living room for a house of this size.</summary>
        public static void Ensure(Scene scene, int houseguests)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            var root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }
            var dressing = root.GetComponent<Dressing>();
            if (dressing == null) dressing = root.AddComponent<Dressing>();
            if (dressing.houseguests != houseguests)
            {
                DressTheTable(scene, root.transform, houseguests);
                dressing.houseguests = houseguests;
            }
            if (!dressing.livingDressed) dressing.livingDressed = DressTheLivingRoom(scene, root.transform);
        }

        /// <summary>Strikes the dressing: the clones go, the moved props stay where they are. For tests.</summary>
        public static void Strike(Scene scene)
        {
            var root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == RootName);
            if (root != null) Object.DestroyImmediate(root);
            foreach (var anchor in HouseInteractionAnchors.InScene(scene))
                if (anchor != null && IsCeremonyVenue(anchor.VenueId)) Object.DestroyImmediate(anchor.gameObject);
        }

        public static bool IsCeremonyVenue(string venue) =>
            venue == CeremonySeating.NominationSeat || venue == CeremonySeating.NominationHead
            || venue == CeremonySeating.HotSeat || venue == CeremonySeating.SofaSeat || venue == CeremonySeating.LivingMark;

        private static Transform[] All(Scene scene) =>
            scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToArray();

        private static HouseRoomMarker Marker(Scene scene, string room) =>
            scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<HouseRoomMarker>(true)).FirstOrDefault(m => m.RoomName == room);

        private static void StrikeVenue(Scene scene, string venue)
        {
            foreach (var anchor in HouseInteractionAnchors.InScene(scene))
                if (anchor != null && anchor.VenueId == venue) Object.DestroyImmediate(anchor.gameObject);
        }

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>
        /// A copy of a dressed prop for the set: the prop as placed, without the anchors that hang
        /// on it - the set's screen carries the episode station, and a second station would leave
        /// the house with none it could find.
        /// </summary>
        private static Transform Clone(Transform source, Transform root, string name)
        {
            var clone = Object.Instantiate(source.gameObject, root);
            clone.name = name;
            foreach (var anchor in clone.GetComponentsInChildren<HouseInteractionAnchor>(true))
                Object.DestroyImmediate(anchor.gameObject);
            return clone.transform;
        }

        private static readonly Collider[] overlaps = new Collider[8];

        /// <summary>Whether a body standing here would be inside furniture: the audit's own capsule against the furniture layer.</summary>
        private static bool InsideFurniture(Scene scene, Vector3 feet)
        {
            Physics.SyncTransforms();
            int count = scene.GetPhysicsScene().OverlapCapsule(feet + Vector3.up * 0.35f, feet + Vector3.up * 1.5f, 0.3f, overlaps,
                1 << HouseLayers.Furniture, QueryTriggerInteraction.Ignore);
            return count > 0;
        }

        private static float YawToward(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;

        private static float Height(Transform prop)
        {
            var renderers = prop.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToList();
            if (renderers.Count == 0) return 0f;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            return bounds.size.y;
        }

        // ------------------------------------------------------------ the nomination table

        /// <summary>The ring's spacing for this many chairs, with the head slot empty: the slots divide the circle by one more.</summary>
        public static float RingStep(int chairs) => 360f / (Mathf.Max(1, chairs) + 1);

        /// <summary>The ring's radius for this many chairs: the dressed radius, or wider when the chairs would touch.</summary>
        public static float RingRadiusFor(int chairs)
        {
            float step = RingStep(chairs) * Mathf.Deg2Rad;
            return Mathf.Max(RingRadius, ChairPitch / (2f * Mathf.Sin(step * 0.5f)));
        }

        /// <summary>Where chair <paramref name="slot"/> of <paramref name="chairs"/> sits: on the ring, counted round from the head.</summary>
        public static Vector3 RingPlace(Vector3 centre, float headAngle, int chairs, int slot)
        {
            float radius = RingRadiusFor(chairs);
            float angle = (headAngle + RingStep(chairs) * (slot + 1)) * Mathf.Deg2Rad;
            return new Vector3(centre.x + radius * Mathf.Cos(angle), centre.y, centre.z + radius * Mathf.Sin(angle));
        }

        private static void DressTheTable(Scene scene, Transform root, int houseguests)
        {
            var marker = Marker(scene, NominationRoom);
            if (marker == null) return;
            var all = All(scene);
            var table = all.FirstOrDefault(t => t.name == TableName && Flat(t.position, marker.transform.position) < 4f);
            var centre = table != null ? table.position : marker.transform.position;
            centre.y = marker.transform.position.y;
            var chairs = all.Where(t => t.name == ChairName && Flat(t.position, centre) < 4f).OrderBy(t => t.parent == root ? 1 : 0)
                .ThenBy(t => t.GetSiblingIndex()).ToList();
            if (chairs.Count == 0) return;
            StrikeVenue(scene, CeremonySeating.NominationSeat);
            StrikeVenue(scene, CeremonySeating.NominationHead);

            // The head of the table is the side nearest the screen; south, in the scene as dressed.
            float headAngle = 270f;
            if (ScreenSurface.TryFind(scene, NominationRoom, out var screen))
                headAngle = Mathf.Atan2(screen.Centre.z - centre.z, screen.Centre.x - centre.x) * Mathf.Rad2Deg;

            int wanted = Mathf.Max(1, houseguests - 1);
            var template = chairs[0];
            while (chairs.Count < wanted) chairs.Add(Clone(template, root, ChairName));
            float radius = RingRadiusFor(wanted);
            for (int i = 0; i < chairs.Count; i++)
            {
                var chair = chairs[i];
                if (i >= wanted) { chair.gameObject.SetActive(false); continue; }
                chair.gameObject.SetActive(true);
                var at = RingPlace(centre, headAngle, wanted, i);
                float yaw = YawToward(at, centre);
                chair.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
                var seat = HouseInteractionAnchor.Create(chair, CeremonySeating.NominationSeat, NominationRoom, i, at, yaw, true,
                    Vector3.back * SeatApproach);
                float height = Height(chair);
                if (height > 0.3f) seat.SetSeatHeight(height * SeatShare);
            }
            Physics.SyncTransforms();

            // The head marks: the Head of Household's on the head's own line, a step out from the
            // ring, and the veto holder's beside it, both facing the table. On the line because at
            // a full house the ring's first and last approaches come round to within a hand of a
            // mark laid a step out to the side, and whoever stood there blocked that seat for good
            // (measured 2026-09-28: the sixteenth never sat); two roots need 0.6 m between them.
            // The side mark is stepped further aside while it is too close, and never toward the
            // screen, where the floor runs out.
            var toHead = new Vector3(Mathf.Cos(headAngle * Mathf.Deg2Rad), 0f, Mathf.Sin(headAngle * Mathf.Deg2Rad));
            var aside = Vector3.Cross(Vector3.up, toHead).normalized;
            var approaches = CeremonySeating.Anchors(scene, CeremonySeating.NominationSeat).Select(a => a.Approach).ToList();
            // The Head of Household's mark, a shade off the line so the holder's fits beside it.
            var head = centre + toHead * (radius + 0.6f) + aside * 0.2f;
            HouseInteractionAnchor.Create(root, CeremonySeating.NominationHead, NominationRoom, 0, head, YawToward(head, centre), false);
            // The veto holder's: the dressed place a step aside, or the nearest of a few places
            // further in and further out that clear every approach and the head's own mark.
            float[] sides = { -0.9f, -0.7f, -0.5f, -0.35f };
            float[] steps = { 0.6f, 0.85f, 1.1f };
            Vector3 holder = centre + toHead * (radius + 0.6f) + aside * -0.9f;
            bool Clear(Vector3 at) => Flat(at, head) >= HeadMarkClearance && approaches.All(approach => Flat(approach, at) >= HeadMarkClearance);
            if (!Clear(holder))
                foreach (float step in steps)
                {
                    bool found = false;
                    foreach (float side in sides)
                    {
                        var at = centre + toHead * (radius + step) + aside * side;
                        if (!Clear(at)) continue;
                        holder = at; found = true; break;
                    }
                    if (found) break;
                }
            HouseInteractionAnchor.Create(root, CeremonySeating.NominationHead, NominationRoom, 1, holder, YawToward(holder, centre), false);
        }

        // ------------------------------------------------------------ the living room

        private static bool DressTheLivingRoom(Scene scene, Transform root)
        {
            var marker = Marker(scene, LivingRoom);
            if (marker == null) return false;
            var all = All(scene);
            var floor = all.FirstOrDefault(t => t.name == LivingFloorName);
            var floorBox = floor != null ? floor.GetComponent<BoxCollider>() : null;
            var bounds = floorBox != null ? floorBox.bounds
                : new Bounds(marker.transform.position, new Vector3(10f, 0.3f, 10f));
            float floorY = floorBox != null ? bounds.max.y : marker.transform.position.y;

            // The screen: the set's, cloned into the television's place on the room's far side, facing in.
            var screen = all.FirstOrDefault(t => t.name == LivingScreenName);
            if (screen == null)
            {
                var source = all.FirstOrDefault(t => t.name == ScreenSurface.PropName);
                if (source == null) return false;
                var television = all.FirstOrDefault(t => t.name == TelevisionName && bounds.Contains(new Vector3(t.position.x, bounds.center.y, t.position.z)));
                var console = all.FirstOrDefault(t => t.name == TelevisionConsoleName && bounds.Contains(new Vector3(t.position.x, bounds.center.y, t.position.z)));
                float x = television != null ? television.position.x : console != null ? console.position.x : bounds.center.x;
                screen = Clone(source, root, LivingScreenName);
                // The stage's depth clear of the room's edge, the board facing the room (-z).
                screen.SetPositionAndRotation(new Vector3(x, floorY, bounds.max.z - 1.25f), Quaternion.identity);
                if (television != null) television.gameObject.SetActive(false);
                if (console != null) console.gameObject.SetActive(false);
                Physics.SyncTransforms();
            }
            var face = ScreenSurface.Measure(screen, LivingRoom, marker.transform.position);
            if (face == null) return false;
            var inward = -face.Normal; inward.y = 0f; inward.Normalize();
            var outward = -inward;
            var aside = Vector3.Cross(Vector3.up, outward).normalized;
            var stage = new Vector3(screen.position.x, floorY, screen.position.z);

            StrikeVenue(scene, CeremonySeating.HotSeat);
            StrikeVenue(scene, CeremonySeating.SofaSeat);
            StrikeVenue(scene, CeremonySeating.LivingMark);

            // The hot seats: two of the table's chairs, side by side, facing the screen.
            var chairTemplate = all.FirstOrDefault(t => t.name == ChairName && t.gameObject.activeInHierarchy);
            var hotSeats = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == ChairName + " (hot seat)").ToList();
            float hotYaw = YawToward(stage + outward, stage);
            for (int slot = 0; slot < 2; slot++)
            {
                Transform chair = slot < hotSeats.Count ? hotSeats[slot] : null;
                if (chair == null)
                {
                    if (chairTemplate == null) break;
                    chair = Clone(chairTemplate, root, ChairName + " (hot seat)");
                }
                var at = stage + outward * HotSeatDistance + aside * (slot == 0 ? -HotSeatGap * 0.5f : HotSeatGap * 0.5f);
                chair.gameObject.SetActive(true);
                chair.SetPositionAndRotation(at, Quaternion.Euler(0f, hotYaw, 0f));
                var seat = HouseInteractionAnchor.Create(chair, CeremonySeating.HotSeat, LivingRoom, slot, at, hotYaw, true, Vector3.back * SeatApproach);
                float height = Height(chair);
                if (height > 0.3f) seat.SetSeatHeight(height * SeatShare);
            }

            // The sofa, turned to face the screen with whatever sits on it, and its three seats.
            var sofa = all.FirstOrDefault(t => t.name == SofaName && bounds.Contains(new Vector3(t.position.x, bounds.center.y, t.position.z)));
            if (sofa != null)
            {
                // Turned to the board itself, which is what the room looks at, not the stage it stands on.
                float facing = YawToward(sofa.position, new Vector3(face.Centre.x, floorY, face.Centre.z));
                float sofaYaw = facing + 180f;
                float turn = Mathf.DeltaAngle(sofa.eulerAngles.y, sofaYaw);
                if (Mathf.Abs(turn) > 0.5f)
                {
                    var pivot = sofa.position;
                    var spin = Quaternion.Euler(0f, turn, 0f);
                    foreach (var prop in all)
                    {
                        if (prop == null || prop == sofa || prop.IsChildOf(sofa) || prop.parent == root) continue;
                        if (prop.GetComponent<HouseRoomMarker>() != null || prop.GetComponent<HouseInteractionAnchor>() != null) continue;
                        if (prop.GetComponentInParent<HouseNpc>() != null || prop.GetComponentInParent<HousePlayerController>() != null) continue;
                        // Only the props on the sofa: cushions, not the chair beside it.
                        if (prop.position.y < floorY + 0.2f || Flat(prop.position, pivot) > 0.9f) continue;
                        if (prop.parent != null && prop.parent != sofa.parent) continue;
                        prop.SetPositionAndRotation(pivot + spin * (prop.position - pivot), spin * prop.rotation);
                    }
                    sofa.rotation = spin * sofa.rotation;
                    Physics.SyncTransforms();
                }
                float sofaHeight = Height(sofa);
                var forward = Quaternion.Euler(0f, facing, 0f) * Vector3.forward;
                var right = Quaternion.Euler(0f, facing, 0f) * Vector3.right;
                float[] offsets = { -0.52f, 0f, 0.52f };
                for (int slot = 0; slot < offsets.Length; slot++)
                {
                    var at = new Vector3(sofa.position.x, floorY, sofa.position.z) + right * offsets[slot] - forward * 0.05f;
                    var seat = HouseInteractionAnchor.Create(sofa, CeremonySeating.SofaSeat, LivingRoom, slot, at, facing, true,
                        Vector3.forward * SeatApproach);
                    if (sofaHeight > 0.3f) seat.SetSeatHeight(sofaHeight * SofaSeatShare);
                }
            }

            // Standing marks: one beside the screen for the Head of Household, facing the hot seats;
            // the rest in rows behind the hot seats, facing the screen. Each on the walkable floor.
            var marks = new List<(Vector3 at, float yaw)>();
            var hotCentre = stage + outward * HotSeatDistance;
            var headMark = stage + outward * 1.2f + aside * -2.4f;
            marks.Add((headMark, YawToward(headMark, hotCentre)));
            float markYaw = YawToward(stage + outward, stage);
            for (int row = 0; row < 2; row++)
            {
                float back = HotSeatDistance + MarkRowDistance + row * MarkPitch;
                for (int k = -3; k <= 3; k++)
                {
                    float across = k * MarkPitch + (row == 1 ? MarkPitch * 0.5f : 0f);
                    marks.Add((stage + outward * back + aside * across, markYaw));
                }
            }
            int placed = 0;
            var hotPlaces = CeremonySeating.Anchors(scene, CeremonySeating.HotSeat).Select(a => a.Position).ToList();
            foreach (var mark in marks)
            {
                var at = mark.at;
                // In the room, a stride from its edges, off the sofa and the hot seats, and on the floor the house walks.
                var inset = new Vector3(Mathf.Clamp(at.x, bounds.min.x + 0.5f, bounds.max.x - 0.5f), at.y,
                    Mathf.Clamp(at.z, bounds.min.z + 0.5f, bounds.max.z - 0.5f));
                if ((inset - at).sqrMagnitude > 0.0001f) continue;
                if (sofa != null && Flat(at, sofa.position) < 1.3f) continue;
                if (hotPlaces.Any(hot => Flat(at, hot) < 0.9f)) continue;
                if (NavMesh.SamplePosition(at, out var hit, 0.6f, NavMesh.AllAreas)) at = hit.position;
                else continue;
                if (InsideFurniture(scene, at)) continue;
                HouseInteractionAnchor.Create(root, CeremonySeating.LivingMark, LivingRoom, placed++, at, mark.yaw, false);
            }
            return true;
        }
    }
}
