using System;
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
            DressHoHBench(all,markers,Has);
            var yard=markers.FirstOrDefault(m=>m.RoomName=="Yard");
            if(yard!=null)
            {
                // Slots zero and one remain the saved home venue. Only the spare actual lounger
                // receives another slot; missing furniture never becomes a room-marker seat.
                var used=existing.Where(a=>a.VenueId==HouseFurniture.LoungerAnchor).Select(a=>a.transform.parent).ToArray();
                var loungers=all.Where(t=>t.name=="bb_set_lounger" && t.gameObject.activeInHierarchy).ToArray();
                var yardFloor=all.FirstOrDefault(t=>t.name=="Competition yard floor")?.GetComponent<BoxCollider>();
                int slot=2;
                foreach(var lounger in loungers.Where(t=>!used.Contains(t))
                    .OrderBy(t=>t.position.x).ThenBy(t=>t.position.z))
                {
                    while(Has(HouseFurniture.LoungerAnchor,slot))slot++;
                    if(!TrySpareLoungerApproach(lounger,loungers,existing,yardFloor,out var approach))continue;
                    var at=lounger.position;at.y=yardFloor.bounds.max.y;
                    HouseInteractionAnchor.Create(lounger,HouseFurniture.LoungerAnchor,"Yard",slot++,at,lounger.eulerAngles.y,true,
                        approach)
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

        private static bool TrySpareLoungerApproach(Transform lounger,Transform[] loungers,HouseInteractionAnchor[] existing,BoxCollider floor,out Vector3 approach)
        {
            // Mesh clearance alone can choose a corner outside the Yard or inside the pool.
            // Author only on this floor's safe interior and clear of the actual static capsule
            // blockers. This works before a bake, too; native route validation remains required
            // by the coordinator. Existing saved Home anchors and explicit edits stay intact.
            approach=default;
            const float Radius=.5f,Height=2f; // The house's authored NavMesh build body.
            if(floor==null || !floor.enabled || floor.isTrigger || !floor.gameObject.activeInHierarchy
                || Vector3.Dot(floor.transform.up,Vector3.up)<.999f
                || floor.transform.lossyScale.x<=0 || floor.transform.lossyScale.z<=0)return false;
            Physics.SyncTransforms();
            var yaw=Quaternion.Euler(0,lounger.eulerAngles.y,0);
            var neighbours=existing.Where(a=>a.VenueId==HouseFurniture.LoungerAnchor && a.transform.parent!=lounger).ToArray();
            var candidates=new System.Collections.Generic.List<Vector3>{Vector3.left*.95f,Vector3.right*.95f};
            Bounds local=default;bool hasBounds=false;
            foreach(var renderer in lounger.GetComponentsInChildren<Renderer>())
            {
                if(!renderer.enabled)continue;
                var bounds=renderer.bounds;
                for(int corner=0;corner<8;corner++)
                {
                    var world=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                    var point=Quaternion.Inverse(yaw)*(world-lounger.position);
                    if(!hasBounds){local=new Bounds(point,Vector3.zero);hasBounds=true;}else local.Encapsulate(point);
                }
            }
            if(!hasBounds)return false;
            foreach(float x in new[]{local.min.x-.65f,local.max.x+.65f})
                foreach(float z in new[]{local.min.z-.65f,local.max.z+.65f})candidates.Add(new Vector3(x,0,z));
            Vector3 Feet(Vector3 offset)
            {
                var at=lounger.position+yaw*offset;at.y=floor.bounds.max.y;return at;
            }
            bool OnFloor(Vector3 offset)
            {
                var point=floor.transform.InverseTransformPoint(Feet(offset))-floor.center;
                var scale=floor.transform.lossyScale;
                return Mathf.Abs(point.x)<floor.size.x*.5f-(Radius+.1f)/scale.x
                    && Mathf.Abs(point.z)<floor.size.z*.5f-(Radius+.1f)/scale.z;
            }
            var hits=new Collider[128];
            bool ClearOfStatics(Vector3 offset)
            {
                var feet=Feet(offset);
                int count=lounger.gameObject.scene.GetPhysicsScene().OverlapCapsule(feet+Vector3.up*Radius,
                    feet+Vector3.up*(Height-Radius),Radius,hits,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                if(count==hits.Length)return false;
                for(int i=0;i<count;i++)
                {
                    var hit=hits[i];
                    if(hit==null)return false;
                    if(hit.gameObject.scene!=lounger.gameObject.scene || hit==floor)continue;
                    // Transient cast positions must not change the authored rest layout. The
                    // activity reservation separately checks live actor occupancy before moving.
                    if(hit.GetComponentInParent<HouseNpc>()!=null || hit.GetComponentInParent<HousePlayerController>()!=null)continue;
                    return false;
                }
                return true;
            }
            float Clearance(Vector3 offset)
            {
                var at=lounger.position+yaw*offset;
                float nearest=float.PositiveInfinity;
                foreach(var other in loungers)
                    foreach(var renderer in other.GetComponentsInChildren<Renderer>())
                    {
                        if(!renderer.enabled)continue;
                        var bounds=renderer.bounds;var point=new Vector3(at.x,bounds.center.y,at.z);
                        nearest=Mathf.Min(nearest,(bounds.ClosestPoint(point)-point).sqrMagnitude);
                    }
                return nearest;
            }
            bool SharesConversation(Vector3 offset)
            {
                if(neighbours.Length==0)return true;
                var at=lounger.position+yaw*offset;
                return neighbours.Any(a=>
                {
                    float apart=new Vector2(at.x-a.Approach.x,at.z-a.Approach.z).magnitude;
                    return apart>=HouseConversationSpots.RootsApart && apart<=HouseConversationSpots.PlayerReach;
                });
            }
            var safe=candidates.Where(offset=>OnFloor(offset) && Clearance(offset)>.55f*.55f && ClearOfStatics(offset)).ToArray();
            if(safe.Length==0)return false;
            var shared=safe.Where(SharesConversation).ToArray();
            approach=(shared.Length>0 ? shared : safe).OrderBy(offset=>offset.sqrMagnitude).First();
            return true;
        }

        private static void DressHoHBench(Transform[] all,HouseRoomMarker[] markers,Func<string,int,bool> has)
        {
            var room=markers.FirstOrDefault(m=>m.RoomName=="HoH");
            var bench=all.FirstOrDefault(t=>t.name=="bb_set_hohbench" && t.gameObject.activeInHierarchy);
            if(room==null || bench==null || !TryCushionBounds(bench,out var cushion))return;
            var floor=all.FirstOrDefault(t=>t.name=="HoH floor");
            var floorBox=floor!=null?floor.GetComponent<BoxCollider>():null;
            float floorY=floorBox!=null?floorBox.bounds.max.y:room.transform.position.y;
            var centre=bench.TransformPoint(cushion.center);
            var right=bench.right.normalized;var south=-bench.forward.normalized;
            float width=bench.TransformVector(Vector3.right*cushion.size.x).magnitude;
            float depth=bench.TransformVector(Vector3.forward*cushion.size.z).magnitude;
            if(width<HouseConversationSpots.RootsApart+.2f || depth<.2f)return; // Both parked roots must fit independently.
            float spread=Mathf.Min(width*.5f-.1f,HouseConversationSpots.RootsApart*.5f+.05f);
            for(int slot=0;slot<2;slot++)
            {
                if(has(HouseFurniture.HoHBenchAnchor,slot))continue;
                float side=slot==0?-1f:1f;
                var contact=bench.TransformPoint(new Vector3(cushion.center.x+side*cushion.size.x*.25f,cushion.max.y,cushion.center.z));
                var at=new Vector3(contact.x,floorY,contact.z);
                var approach=centre+right*(side*spread)+south*(depth*.5f+.7f);approach.y=floorY;
                var anchor=HouseInteractionAnchor.Create(bench,HouseFurniture.HoHBenchAnchor,"HoH",slot,at,
                    Quaternion.LookRotation(south).eulerAngles.y,true);
                anchor.Configure(anchor.VenueId,anchor.RoomId,slot,true,anchor.transform.InverseTransformPoint(approach));
                anchor.SetSeatHeight(contact.y-floorY);
            }
        }

        /// <summary>The imported cushion material's actual submesh, excluding wood and brass. Welt geometry lies below its top.</summary>
        private static bool TryCushionBounds(Transform bench,out Bounds bounds)
        {
            bounds=default;bool found=false;
            foreach(var renderer in bench.GetComponentsInChildren<MeshRenderer>())
            {
                if(!renderer.enabled)continue;
                var filter=renderer.GetComponent<MeshFilter>();var mesh=filter!=null?filter.sharedMesh:null;
                if(mesh==null)continue;
                var materials=renderer.sharedMaterials;
                for(int index=0;index<materials.Length && index<mesh.subMeshCount;index++)
                {
                    if(materials[index]==null || !materials[index].name.StartsWith("bb_mat_amenity_cream",StringComparison.Ordinal))continue;
                    var box=mesh.GetSubMesh(index).bounds;
                    for(int corner=0;corner<8;corner++)
                    {
                        var local=box.center+Vector3.Scale(box.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                        var point=bench.InverseTransformPoint(renderer.transform.TransformPoint(local));
                        if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);
                    }
                }
            }
            return found;
        }
    }
}
