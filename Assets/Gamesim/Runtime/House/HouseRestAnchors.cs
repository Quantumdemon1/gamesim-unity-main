using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.House
{
    /// <summary>Rest places on the authored seats, independent of a ceremony's gallery reservation.</summary>
    internal static class HouseRestAnchors
    {
        public static void Ensure(Scene scene)
        {
            var all=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Transform>(true)).ToArray();
            var existing=HouseInteractionAnchors.InScene(scene);
            var markers=all.Select(t=>t.GetComponent<HouseRoomMarker>()).Where(m=>m!=null).ToArray();
            bool Has(string id,int slot)=>existing.Any(a=>a.VenueId==id && a.Slot==slot);
            var yard=markers.FirstOrDefault(m=>m.RoomName=="Yard");
            if(yard!=null)
            {
                // Slots zero and one remain the saved home venue. Only the spare actual lounger
                // receives another slot; missing furniture never becomes a room-marker seat.
                var used=existing.Where(a=>a.VenueId==HouseFurniture.LoungerAnchor).Select(a=>a.transform.parent).ToArray();
                int slot=2;
                foreach(var lounger in all.Where(t=>t.name=="bb_set_lounger" && t.gameObject.activeInHierarchy && !used.Contains(t))
                    .OrderBy(t=>t.position.x).ThenBy(t=>t.position.z))
                {
                    while(Has(HouseFurniture.LoungerAnchor,slot))slot++;
                    var at=lounger.position;at.y=yard.transform.position.y;
                    HouseInteractionAnchor.Create(lounger,HouseFurniture.LoungerAnchor,"Yard",slot++,at,lounger.eulerAngles.y,true,Vector3.left*.95f)
                        .SetSeatHeight(.36f);
                }
            }
            var living=markers.FirstOrDefault(m=>m.RoomName==CeremonySets.LivingRoom);
            if(living==null)return;
            var floor=all.FirstOrDefault(t=>t.name==CeremonySets.LivingFloorName);
            var box=floor!=null?floor.GetComponent<BoxCollider>():null;
            var bounds=box!=null?box.bounds:new Bounds(living.transform.position,new Vector3(10f,.3f,10f));
            float floorY=box!=null?bounds.max.y:living.transform.position.y;
            int place=0;
            foreach(var couch in all.Where(t=>t.gameObject.activeInHierarchy
                && (t.name==CeremonySets.LoungeFourName || t.name==CeremonySets.LoungeThreeName)
                && bounds.Contains(new Vector3(t.position.x,bounds.center.y,t.position.z)))
                .OrderBy(t=>t.position.x).ThenBy(t=>t.position.z))
                foreach(float x in CeremonySets.SeatsAcross(couch.name))
                {
                    int slot=place++;
                    if(Has(HouseFurniture.LoungeAnchor,slot))continue;
                    var at=couch.position+couch.rotation*new Vector3(x,0,-.10f);at.y=floorY;
                    HouseInteractionAnchor.Create(couch,HouseFurniture.LoungeAnchor,CeremonySets.LivingRoom,slot,at,couch.eulerAngles.y+180f,
                        true,Vector3.forward*CeremonySets.GalleryApproach).SetSeatHeight(CeremonySets.GallerySeatHeight);
                }
        }
    }
}
