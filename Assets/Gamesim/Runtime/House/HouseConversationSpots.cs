using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.House
{
    /// <summary>
    /// Where the house goes to talk (PACK8-PASS-PLAN C3): pairs of seats on real furniture - the
    /// living room's couches and the long table's chairs - beside the six conversation venues the
    /// saves already know. The owner saw people sit to talk in the middle of a room and asked for
    /// talk to happen at specific places, sitting only on furniture next to somebody.
    ///
    /// <para>Built at runtime from the pieces the scene places, the way the ceremony sets are
    /// dressed, and nothing about it is saved. Each spot belongs to one of the six venues, its
    /// family, in that venue's room: a conversation held at a spot is saved under its family's
    /// name, and a load sends the pair back to a free place of the same family
    /// (<see cref="HouseMeetingCoordinator"/>). No piece, no spot: a house dressed without a
    /// gallery or a long table has its six venues and nothing more.</para>
    ///
    /// <para>Every pair's approaches - where the two navigation roots wait while the bodies sit -
    /// stand at least <see cref="RootsApart"/> apart, the coordinator's own two bodies and a little
    /// more, so two neighbouring cushions (0.76 m) are never a pair; and within
    /// <see cref="PlayerReach"/>, inside the 2.8 m a conversation with the player needs. The couches'
    /// seats and approaches are the gallery's own (<see cref="CeremonySets"/>), which the ceremonies
    /// proved on the baked edge. The spots' anchors carry venue ids of their own, so the ceremony
    /// sets never strike them, and nothing makes them clickable: <see cref="HouseFurniture.TryClick"/>
    /// knows none of them.</para>
    /// </summary>
    public static class HouseConversationSpots
    {
        /// <summary>What every spot's venue id starts with: the family's id and the spot's name follow it.</summary>
        public const string Prefix = "talk:";

        /// <summary>
        /// The least room between a pair's approaches: two bodies 0.35 m round and 0.2 m between
        /// them, the separation the coordinator measures every venue against. Parked roots closer
        /// than 0.7 m have deadlocked a table before.
        /// </summary>
        public const float RootsApart = 0.9f;

        /// <summary>The most room between a pair's approaches: inside the 2.8 m a conversation with the player needs, with room to spare.</summary>
        public const float PlayerReach = 2.6f;

        /// <summary>How much shorter a walk to a seat counts than the same walk to a standing pair: people sit to talk when they can.</summary>
        public const float SeatedBonus = 3f;

        /// <summary>How close two places may come before they are one place: under a cushion's width.</summary>
        public const float SeatsApart = 0.5f;

        /// <summary>The families with spots of their own: the living room's and the long table's.</summary>
        public const string LivingFamily = "living-east-chat", TableFamily = "kitchen-table-chat";

        /// <summary>The long table's chairs, by the names the house places them under.</summary>
        private static readonly string[] DiningChairs = { "bb_set_ph_diningchair", "bb_set_diningchair" };

        /// <summary>How far behind a dining chair a body waits before it sits: the table's own pair's distance.</summary>
        private const float TableApproach = 0.70f;

        /// <summary>The widest a pair of chairs at the table may be apart: one chair between them, not three.</summary>
        private const float TablePairMost = 1.6f;

        /// <summary>A pair of places to talk: two seats on one piece of furniture or two, or two standing marks.</summary>
        public readonly struct Spot
        {
            public readonly string Id, Family, Room;
            public readonly bool Seated;
            public readonly HouseInteractionAnchor First, Second;
            public Spot(string id, string family, string room, bool seated, HouseInteractionAnchor first, HouseInteractionAnchor second)
            { Id = id; Family = family; Room = room; Seated = seated; First = first; Second = second; }
        }

        public static string IdFor(string family, string name) => Prefix + family + ":" + name;

        public static bool IsSpot(string venueId) => venueId != null && venueId.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>The saved venue a spot belongs to, or null when the id is not a spot's; a venue's own id for one of the six.</summary>
        public static string FamilyOf(string venueId)
        {
            if (venueId == null) return null;
            if (!IsSpot(venueId)) return IsFamily(venueId) ? venueId : null;
            string rest = venueId.Substring(Prefix.Length);
            int colon = rest.IndexOf(':');
            string family = colon > 0 ? rest.Substring(0, colon) : null;
            return IsFamily(family) ? family : null;
        }

        /// <summary>Whether this is one of the six venues a save can name.</summary>
        public static bool IsFamily(string id) => id != null && HouseInteractionAnchors.Meetings.Any(m => m.Id == id);

        /// <summary>
        /// Dresses the spots the house's pieces allow, once: a spot already there is left alone, and
        /// a piece that is missing, struck or not a piece of furniture under its seat gets none.
        /// </summary>
        public static void Ensure(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var existing = HouseInteractionAnchors.InScene(scene);
            bool Has(string id) => existing.Any(anchor => anchor != null && anchor.VenueId == id);
            DressTheGallery(all, Has);
            DressTheTable(scene, all, existing, Has);
            DressTheLoungers(existing, Has);
        }

        /// <summary>Every complete spot in the scene, in id order: two active anchors, one room, both seats or both standing.</summary>
        public static List<Spot> InScene(Scene scene)
        {
            var spots = new List<Spot>();
            var ids = HouseInteractionAnchors.InScene(scene).Where(anchor => anchor != null && IsSpot(anchor.VenueId))
                .Select(anchor => anchor.VenueId).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToList();
            foreach (string id in ids)
            {
                string family = FamilyOf(id);
                if (family == null || !HouseInteractionAnchors.TryFind(scene, id, 0, out var first)
                    || !HouseInteractionAnchors.TryFind(scene, id, 1, out var second)) continue;
                if (first.RoomId != second.RoomId || first.Seated != second.Seated) continue;
                spots.Add(new Spot(id, family, first.RoomId, first.Seated, first, second));
            }
            return spots;
        }

        /// <summary>
        /// The places in a room where two people stand to talk: the standing venues' marks and any
        /// standing spot's, where the bodies stand when they get there. Offered to the story
        /// session as a feed for the places a scene puts its people (EpisodeDirector.ScenePlaces);
        /// nothing here uses it. Empty for a room with none.
        /// </summary>
        public static IReadOnlyList<Vector3> StandingPlaces(Scene scene, string room)
        {
            var places = new List<Vector3>();
            if (room == null || !scene.IsValid() || !scene.isLoaded) return places;
            foreach (var venue in HouseInteractionAnchors.Meetings)
            {
                if (venue.Seated || venue.Room != room) continue;
                for (int slot = 0; slot < 2; slot++)
                    if (HouseInteractionAnchors.TryFind(scene, venue.Id, slot, out var mark) && !mark.Posed && mark.RoomId == room)
                        places.Add(mark.Approach);
            }
            foreach (var spot in InScene(scene))
                if (!spot.Seated && spot.Room == room) { places.Add(spot.First.Approach); places.Add(spot.Second.Approach); }
            return places;
        }

        /// <summary>
        /// Whether a seat stands on furniture: the piece it hangs on is drawn, the seat's place is
        /// over it, and the cushion's height is inside it. A seat on an empty object, a struck piece
        /// or a piece somewhere else is not on furniture.
        /// </summary>
        public static bool OnFurniture(HouseInteractionAnchor seat) =>
            seat != null && seat.Posed && OnFurniture(seat.transform.parent, seat.Position, seat.SeatContact.y - seat.Position.y);

        private static bool OnFurniture(Transform prop, Vector3 at, float seatHeight)
        {
            if (prop == null || !prop.gameObject.activeInHierarchy) return false;
            var renderers = prop.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled).ToList();
            if (renderers.Count == 0) return false;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            const float Margin = 0.1f;
            float contact = at.y + seatHeight;
            return at.x >= bounds.min.x - Margin && at.x <= bounds.max.x + Margin
                && at.z >= bounds.min.z - Margin && at.z <= bounds.max.z + Margin
                && bounds.min.y <= contact && bounds.max.y >= contact - 0.1f;
        }

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>A seat the dressing may use: its piece, its place on the floor, the way it faces, and where its root waits.</summary>
        private readonly struct Seat
        {
            public readonly Transform Prop;
            public readonly Vector3 At, Approach;
            public readonly float Yaw;
            public Seat(Transform prop, Vector3 at, float yaw, Vector3 approach) { Prop = prop; At = at; Yaw = yaw; Approach = approach; }
        }

        /// <summary>Two seats as one spot, when they stand apart enough and near enough, and both are on furniture.</summary>
        private static void Pair(string id, string room, Seat first, Seat second, Vector3 approachOffset, float seatHeight, Func<string, bool> has)
        {
            if (has(id)) return;
            float apart = Flat(first.Approach, second.Approach);
            if (apart < RootsApart || apart > PlayerReach) return;
            if (!OnFurniture(first.Prop, first.At, seatHeight) || !OnFurniture(second.Prop, second.At, seatHeight)) return;
            HouseInteractionAnchor.Create(first.Prop, id, room, 0, first.At, first.Yaw, true, approachOffset).SetSeatHeight(seatHeight);
            HouseInteractionAnchor.Create(second.Prop, id, room, 1, second.At, second.Yaw, true, approachOffset).SetSeatHeight(seatHeight);
        }

        private static void DressTheLoungers(HouseInteractionAnchor[] existing,Func<string,bool> has)
        {
            var seats=existing.Where(a=>a.isActiveAndEnabled && a.VenueId==HouseFurniture.LoungerAnchor && a.RoomId=="Yard" && a.Seated)
                .OrderBy(a=>a.Slot).ToArray();
            foreach(var spare in seats.Where(a=>a.Slot>=2))
            {
                var neighbour=seats.Where(a=>a!=spare).OrderBy(a=>Flat(a.Approach,spare.Approach)).FirstOrDefault();
                if(neighbour==null)continue;
                var first=new Seat(neighbour.transform.parent,neighbour.Position,neighbour.Facing,neighbour.Approach);
                var second=new Seat(spare.transform.parent,spare.Position,spare.Facing,spare.Approach);
                Pair(IdFor(HouseFurniture.LoungerAnchor,"spare-"+spare.Slot),"Yard",first,second,Vector3.left*.95f,.36f,has);
            }
        }

        // ------------------------------------------------------------ the living room's couches

        /// <summary>
        /// The gallery's couches (MOCKUP-PASS-PLAN M21), read as CeremonySets reads them: the U's
        /// base faces the red chairs, the arms face across. Three spots: the base's middle - the
        /// seat each side of the U's middle line, one on each couch - and each corner, where the
        /// base's outer seat and the arm's seat nearest the base face round to each other. Each
        /// seat is a gallery seat, approached from in front, on the couch's baked edge.
        /// </summary>
        private static void DressTheGallery(Transform[] all, Func<string, bool> has)
        {
            var marker = all.Select(t => t.GetComponent<HouseRoomMarker>()).FirstOrDefault(m => m != null && m.RoomName == CeremonySets.LivingRoom);
            if (marker == null) return;
            var floor = all.FirstOrDefault(t => t.name == CeremonySets.LivingFloorName);
            var floorBox = floor != null ? floor.GetComponent<BoxCollider>() : null;
            var bounds = floorBox != null ? floorBox.bounds : new Bounds(marker.transform.position, new Vector3(10f, 0.3f, 10f));
            float floorY = floorBox != null ? bounds.max.y : marker.transform.position.y;
            bool InRoom(Transform t) => bounds.Contains(new Vector3(t.position.x, bounds.center.y, t.position.z));
            var chairs = all.Where(t => t.name == CeremonySets.WingbackName && t.gameObject.activeInHierarchy && InRoom(t)).OrderBy(t => t.position.x).ToList();
            var couches = all.Where(t => (t.name == CeremonySets.LoungeFourName || t.name == CeremonySets.LoungeThreeName)
                && t.gameObject.activeInHierarchy && InRoom(t)).ToList();
            if (chairs.Count < 2 || couches.Count == 0) return;

            var middle = (chairs[0].position + chairs[1].position) * 0.5f;
            var chairFacing = Quaternion.Euler(0f, chairs[0].eulerAngles.y + 180f, 0f);
            var toChairs = chairFacing * Vector3.back;   // from the U toward the chairs
            var across = chairFacing * Vector3.right;
            var onBase = new List<(Seat seat, float side, float depth)>();
            var onArms = new List<(Seat seat, float side, float depth)>();
            var approach = Vector3.forward * CeremonySets.GalleryApproach;
            foreach (var couch in couches)
            {
                float yaw = couch.eulerAngles.y + 180f;
                var facing = Quaternion.Euler(0f, yaw, 0f);
                bool isBase = Vector3.Dot(facing * Vector3.forward, toChairs) > 0.7f;
                foreach (float x in CeremonySets.SeatsAcross(couch.name))
                {
                    var at = couch.position + couch.rotation * new Vector3(x, 0f, -0.10f);
                    at.y = floorY;
                    var seat = new Seat(couch, at, yaw, at + facing * approach);
                    var row = (seat, Vector3.Dot(at - middle, across), Vector3.Dot(at - middle, toChairs));
                    (isBase ? onBase : onArms).Add(row);
                }
            }

            // The base's middle: on each side of the U's middle line, the seat nearest it.
            var west = onBase.Where(s => s.side < 0f).OrderByDescending(s => s.side).Select(s => (Seat?)s.seat).FirstOrDefault();
            var east = onBase.Where(s => s.side > 0f).OrderBy(s => s.side).Select(s => (Seat?)s.seat).FirstOrDefault();
            if (west.HasValue && east.HasValue && west.Value.Prop != east.Value.Prop)
                Pair(IdFor(LivingFamily, "base-middle"), CeremonySets.LivingRoom, west.Value, east.Value, approach, CeremonySets.GallerySeatHeight, has);

            // The corners: the base's outer seat on a side and that side's arm seat nearest the base.
            foreach (float sign in new[] { -1f, 1f })
            {
                var outer = onBase.Where(s => Mathf.Sign(s.side) == sign).OrderByDescending(s => Mathf.Abs(s.side)).Select(s => (Seat?)s.seat).FirstOrDefault();
                var arm = onArms.Where(s => Mathf.Sign(s.side) == sign).OrderBy(s => s.depth).Select(s => (Seat?)s.seat).FirstOrDefault();
                if (!outer.HasValue || !arm.HasValue) continue;
                Pair(IdFor(LivingFamily, sign < 0f ? "corner-west" : "corner-east"), CeremonySets.LivingRoom, outer.Value, arm.Value,
                    approach, CeremonySets.GallerySeatHeight, has);
            }
        }

        // ------------------------------------------------------------ the long table's chairs

        /// <summary>
        /// The long table's chairs beside the table's own pair: on each side of the table, the pair
        /// of chairs one chair apart nearest the table's own, of the chairs whose roots would wait
        /// clear of that pair's - so the two never hold the same patch of floor. Approached from
        /// behind, as the table's own pair are, at that pair's seat height.
        /// </summary>
        private static void DressTheTable(Scene scene, Transform[] all, HouseInteractionAnchor[] existing, Func<string, bool> has)
        {
            if (!HouseInteractionAnchors.TryFind(scene, TableFamily, 0, out var a) || !HouseInteractionAnchors.TryFind(scene, TableFamily, 1, out var b)
                || !a.Seated || !b.Seated) return;
            var home = new[] { a, b };
            var centre = (a.Position + b.Position) * 0.5f;
            float floorY = a.Position.y;
            float seatHeight = Mathf.Clamp(a.SeatContact.y - a.Position.y, 0.15f, 1.2f);
            var taken = new HashSet<Transform>(existing.Where(anchor => anchor != null && anchor.Seated && anchor.transform.parent != null)
                .Select(anchor => anchor.transform.parent));
            var chairs = all.Where(t => DiningChairs.Contains(t.name) && t.gameObject.activeInHierarchy && !taken.Contains(t)
                && Flat(t.position, centre) < 4f).ToList();
            var back = Vector3.back * TableApproach;
            Seat SeatOn(Transform chair)
            {
                var at = chair.position; at.y = floorY;
                float yaw = chair.eulerAngles.y;
                return new Seat(chair, at, yaw, at + Quaternion.Euler(0f, yaw, 0f) * back);
            }
            // The table's sides: the chairs that face the same way.
            foreach (var side in chairs.Select(SeatOn).GroupBy(seat => Mathf.RoundToInt(Mathf.Repeat(seat.Yaw, 360f)) % 360).OrderBy(group => group.Key))
            {
                var free = side.Where(seat => home.All(h => Flat(seat.Approach, h.Approach) >= RootsApart))
                    .OrderBy(seat => Flat(seat.At, centre)).ThenBy(seat => seat.At.x).ThenBy(seat => seat.At.z).ToList();
                bool paired = false;
                for (int i = 0; i < free.Count && !paired; i++)
                    for (int j = i + 1; j < free.Count && !paired; j++)
                    {
                        float apart = Flat(free[i].At, free[j].At);
                        if (apart < RootsApart || apart > TablePairMost) continue;
                        // Numbered along the side, so a pair reads the same however the search found it.
                        var along = Quaternion.Euler(0f, side.Key, 0f) * Vector3.right;
                        bool inOrder = Vector3.Dot(free[j].At - free[i].At, along) >= 0f;
                        Pair(IdFor(TableFamily, "side-" + side.Key), a.RoomId, inOrder ? free[i] : free[j], inOrder ? free[j] : free[i],
                            back, seatHeight, has);
                        paired = true;
                    }
            }
        }
    }
}
