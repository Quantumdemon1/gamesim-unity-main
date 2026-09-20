using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.House
{
    /// <summary>A prop-relative interaction slot. It supplies geometry, never simulation effects.</summary>
    [DisallowMultipleComponent]
    public sealed class HouseInteractionAnchor : MonoBehaviour
    {
        [SerializeField] private string venueId, roomId;
        [SerializeField] private int slot;
        [SerializeField] private bool seated;
        [SerializeField] private Vector3 approachOffset;
        [SerializeField] private Vector3 cameraOffset = new Vector3(0,1.45f,2.6f);
        [SerializeField, Min(0f)] private float seatHeight = .46f;
        public string VenueId => venueId;
        public string RoomId => roomId;
        public int Slot => slot;
        public bool Seated => seated;
        public Vector3 Position => transform.position;
        public float Facing => transform.eulerAngles.y;
        public Vector3 Approach => transform.TransformPoint(approachOffset);
        public Vector3 CameraPosition => transform.TransformPoint(cameraOffset);
        public Vector3 SeatContact => transform.TransformPoint(new Vector3(0,seatHeight,0));
        public void SetSeatHeight(float metres) => seatHeight = Mathf.Clamp(metres,.15f,1.2f);

        public void Configure(string venue, string room, int index, bool isSeated, Vector3 approach)
        {
            venueId = venue; roomId = room; slot = index; seated = isSeated; approachOffset = approach;
        }

        public static HouseInteractionAnchor Create(Transform prop, string venue, string room,
            int index, Vector3 position, float yaw, bool isSeated, Vector3 approach = default)
        {
            if (prop == null) throw new ArgumentNullException(nameof(prop));
            var root = new GameObject(venue + " · slot " + index);
            root.transform.SetParent(prop, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0,yaw,0));
            // These are metres in the anchor's own frame even when a fitted furniture model has
            // a non-unit scale. Moving or rotating the prop still moves every authored hint.
            var scale = prop.lossyScale;
            root.transform.localScale = new Vector3(SafeInverse(scale.x),SafeInverse(scale.y),SafeInverse(scale.z));
            var anchor = root.AddComponent<HouseInteractionAnchor>();
            anchor.Configure(venue,room,index,isSeated,approach);
            return anchor;
        }

        private static float SafeInverse(float value) => Mathf.Abs(value) > .0001f ? 1f/value : 1f;
    }

    /// <summary>Compatibility authoring for existing scenes; explicit authored anchors take priority.</summary>
    public static class HouseInteractionAnchors
    {
        public const string DiaryVenue = "private-diary";
        public const string DiaryProp = "bb_set_diarychair";
        public const string EpisodeDestination = "episode-screen";
        public const string CompetitionDestination = "competition-entry";

        public readonly struct Definition
        {
            public readonly string Id, Room;
            public readonly Vector3 First, Second;
            public readonly bool Seated;
            public readonly float FirstYaw, SecondYaw;
            public Definition(string id,string room,Vector3 a,Vector3 b,bool seated=false,float yawA=0,float yawB=0)
            { Id=id; Room=room; First=a; Second=b; Seated=seated; FirstYaw=yawA; SecondYaw=yawB; }
        }

        // Legacy scene coordinates are only the initial binding hints. Reservations read the live
        // anchor transforms, and saved rendezvous IDs remain unchanged.
        public static readonly Definition[] Meetings =
        {
            new Definition("living-east-chat","Living",new Vector3(-6.2f,0,-4),new Vector3(-4.8f,0,-4)),
            new Definition("kitchen-west-chat","Kitchen",new Vector3(2.3f,0,-3),new Vector3(3.7f,0,-3)),
            new Definition("bedroom-south-chat","Bedroom",new Vector3(-8.7f,0,3.5f),new Vector3(-7.3f,0,3.5f)),
            new Definition("yard-south-chat","Yard",new Vector3(3.3f,0,13.5f),new Vector3(4.7f,0,13.5f)),
            new Definition("kitchen-table-chat","Kitchen",new Vector3(7.2f,0,-7.22f),new Vector3(7.2f,0,-8.78f),true,180,0),
            new Definition("yard-lounger-chat","Yard",new Vector3(9.4f,0,11),new Vector3(10.8f,0,11),true,0,0),
        };

        public static HouseInteractionAnchor[] InScene(Scene scene) => !scene.IsValid() || !scene.isLoaded
            ? Array.Empty<HouseInteractionAnchor>()
            : scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HouseInteractionAnchor>(true)).ToArray();

        public static void EnsureDefaults(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            var existing = InScene(scene);
            var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var markers = all.Select(t => t.GetComponent<HouseRoomMarker>()).Where(m => m != null).ToArray();
            var usedSeats = new HashSet<Transform>(existing.Where(a=>a.Seated).Select(a=>a.transform.parent));
            foreach (var venue in Meetings)
            {
                var room = markers.FirstOrDefault(m => m.RoomName == venue.Room);
                if (room == null) continue;
                for (int slot=0;slot<2;slot++)
                {
                    if (existing.Any(a => a.VenueId==venue.Id && a.Slot==slot)) continue;
                    var at = slot==0 ? venue.First : venue.Second;
                    float yaw = slot==0 ? venue.FirstYaw : venue.SecondYaw;
                    Transform prop = room.transform;
                    if (venue.Seated)
                    {
                        var candidates = all.Where(t => !usedSeats.Contains(t) && (venue.Room=="Yard" ? t.name=="bb_set_lounger"
                            : t.name=="bb_set_ph_diningchair" || t.name=="bb_set_diningchair"));
                        var seat = candidates.OrderBy(t => (t.position-at).sqrMagnitude).FirstOrDefault();
                        // Old coordinates choose an initial seat only. There is no distance cutoff
                        // and no invisible room-marker substitute for missing furniture.
                        if (seat == null) continue;
                        usedSeats.Add(seat);
                        prop=seat; at=seat.position; at.y=room.transform.position.y; yaw=seat.eulerAngles.y;
                    }
                    var created=HouseInteractionAnchor.Create(prop,venue.Id,venue.Room,slot,at,yaw,venue.Seated,
                        venue.Seated ? Vector3.back*(venue.Id=="yard-lounger-chat" ? .40f : .55f) : Vector3.zero);
                    if(venue.Room=="Yard" && venue.Seated)created.SetSeatHeight(.36f);
                }
            }
            EnsureDestination(existing,all,markers,EpisodeDestination,"bb_set_ceremonyscreen",
                markers.Any(m=>m.RoomName=="Nomination") ? "Nomination" : "Living",-2.2f);
            var yard=all.FirstOrDefault(t=>t.name=="Competition yard floor");
            if(yard!=null && !existing.Any(a=>a.VenueId==CompetitionDestination))
            {
                var yardFloor=yard.GetComponent<Collider>();
                var at=yardFloor!=null ? yardFloor.bounds.center : yard.position;
                at.y=yardFloor!=null ? yardFloor.bounds.max.y : yard.position.y;
                HouseInteractionAnchor.Create(yard,CompetitionDestination,"Yard",0,at,0,false,Vector3.back);
            }
            if (existing.Any(a => a.VenueId==DiaryVenue)) return;
            var chair = all.FirstOrDefault(t => t.name==DiaryProp)
                ?? all.FirstOrDefault(t => t.name=="Confessional chair A");
            var privateRoom = markers.FirstOrDefault(m => m.RoomName=="Private");
            if (chair==null || privateRoom==null) return;
            var floor = chair.position; floor.y=privateRoom.transform.position.y;
            HouseInteractionAnchor.Create(chair,DiaryVenue,"Private",0,floor,chair.eulerAngles.y,true,Vector3.forward);
        }

        private static void EnsureDestination(HouseInteractionAnchor[] existing,Transform[] all,HouseRoomMarker[] markers,
            string id,string propName,string room,float approach)
        {
            if(existing.Any(a=>a.VenueId==id))return;
            var prop=all.FirstOrDefault(t=>t.name==propName);
            var marker=markers.FirstOrDefault(m=>m.RoomName==room);
            if(marker==null)return;
            if(prop==null)
            {
                // The prototype's episode station predates the ceremony set. A standing room
                // destination preserves that action; it does not pretend missing furniture seats exist.
                if(id==EpisodeDestination && room=="Living")
                    HouseInteractionAnchor.Create(marker.transform,id,room,0,marker.transform.position,0,false);
                return;
            }
            var at=prop.position;at.y=marker.transform.position.y;
            HouseInteractionAnchor.Create(prop,id,room,0,at,prop.eulerAngles.y,false,Vector3.forward*approach);
        }

        /// <summary>Authoring diagnostics. Missing props stay missing instead of creating phantom seats.</summary>
        public static List<string> Validate(Scene scene)
        {
            var errors=new List<string>();
            foreach(var venue in Meetings)
                for(int slot=0;slot<2;slot++)
                    if(!TryFind(scene,venue.Id,slot,out var anchor))errors.Add(venue.Id+" slot "+slot+" needs one active authored anchor.");
                    else if(anchor.RoomId!=venue.Room || anchor.Seated!=venue.Seated)errors.Add(venue.Id+" has incompatible room or seating metadata.");
            foreach(var id in new[]{DiaryVenue,EpisodeDestination,CompetitionDestination})
                if(!TryFind(scene,id,0,out _))errors.Add(id+" needs one active authored anchor.");
            return errors;
        }

        public static bool TryFind(Scene scene,string id,int slot,out HouseInteractionAnchor anchor)
        {
            var matches = InScene(scene).Where(a => a.VenueId==id && a.Slot==slot).ToArray();
            anchor = matches.Length==1 && matches[0].isActiveAndEnabled ? matches[0] : null;
            return anchor!=null;
        }
    }
}
