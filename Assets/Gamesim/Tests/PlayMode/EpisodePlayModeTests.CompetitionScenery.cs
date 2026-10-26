using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Every competition station stands clear of the yard's drawn set pieces, measured on what the
    /// director actually stages: each instrument's bounds as fitted to its arrived contestant, and the
    /// contestant's own capsule, against the triangles of every mesh of the house that stands on the
    /// deck - the entrance arch, the stanchions and ropes, the course's gates, signs and backdrop, the
    /// display, the plants, the shell and the rest. Physics alone could not see most of them: the arch
    /// has no collider, and the player's station stood at x 2.4 on the back row with the arch's right
    /// leg through the rear of every family's instrument (the props batch's full-field captures).
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static readonly string[] SceneryFamilies={"Skill","Mental","Endurance","Luck","Social"};

        // Five whole fields in one case. A sixteen-person field takes about half a minute (the three-family
        // stored-maximum case ran 80-102 s), so five would pass the test framework's 180 s default.
        [UnityTest, Timeout(600000)]
        public IEnumerator Apparatus_InHouseEveryStationStandsClearOfTheYardsDrawnSetPieces([Values(6,16)] int houseSize)
        {
            foreach(string category in SceneryFamilies)
            {
                yield return EnterInstrumentAttempt(category,houseSize:houseSize);var before=director.Snapshot;
                // The fitted moment the full-field captures show: the grip held, the dice rolled.
                if(category=="Endurance")yield return HoldTheActualGrip();
                else if(category=="Luck")yield return RollTheActualDice();
                yield return WaitForFittedCompetitionField(houseSize,category);
                AssertStationsClearOfDrawnSetPieces(category,houseSize);
                yield return CancelInstrumentAttempt(before);
                var props=SceneComponents<Transform>().Single(transform=>transform.name=="Representative yard solid fixtures");Object.Destroy(props.gameObject);yield return null;
            }
        }

        /// <summary>
        /// Two houseguests standing at the south yard's chat places when a sixteen-person field begins
        /// still leave every entrant a clear station. While the cast is placed no body is a route owner,
        /// so each capsule refuses every place whose reservation it meets, and no station can be leased on
        /// top of one (the route asks for room to stand there). Once the arena cleared the yard's drawn
        /// scenery the 1.8 m grid had no place to spare, and one person chatting there cost an entrant
        /// their station; the second pass finds the gaps the grid steps over. These two stand-ins never
        /// move, so the field has to stand around them: the deepest family, with the suite's yard solids.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator Apparatus_InHouseStoredMaximumFieldStandsEveryEntrantAroundHouseguestsInTheSouthYard()
        {
            const string category="Endurance";int houseSize=Gamesim.Simulation.EpisodeValidation.MaximumCast;
            GameObject standers=null;
            yield return EnterInstrumentAttempt(category,houseSize:houseSize,beforeEntry:()=>standers=InstallSouthYardStandIns());
            var before=director.Snapshot;
            yield return HoldTheActualGrip();
            yield return WaitForFittedCompetitionField(houseSize,category);
            var floor=SceneComponents<BoxCollider>().Single(collider=>collider.name=="Competition yard floor");var deck=floor.bounds;
            var bodies=standers.GetComponentsInChildren<CapsuleCollider>();
            Assert.That(bodies,Has.Length.EqualTo(2));
            var instruments=SceneComponents<CompetitionApparatus>().Where(instrument=>instrument.gameObject.activeInHierarchy).ToArray();
            Assert.That(instruments,Has.Length.EqualTo(houseSize),"Every entrant has a fitted station beside the two houseguests. "+CompetitionFieldDiagnostic());
            // The stand-ins stand where the grid has places: each meets the whole reservation of a grid
            // place on a row the field uses, so the grid alone would have lost those places.
            var rowsUsed=instruments.Select(instrument=>instrument.transform.parent.position.z).Distinct().ToArray();
            var playerCapsule=player.GetComponent<CapsuleCollider>();
            float radius=Mathf.Max(player.Agent.radius,playerCapsule!=null?playerCapsule.radius:0);
            var family=PlayerInstrument().Instrument;
            foreach(var body in bodies)
            {
                bool takesAGridPlace=false;
                for(float x=deck.min.x+2f;x<deck.max.x-2f && !takesAGridPlace;x+=1.8f)
                    foreach(float z in rowsUsed)
                        if(CompetitionStageFootprint.Station(family,new Vector3(x,deck.max.y,z),0,radius,player.Agent.height).Meets(body.bounds))
                        {takesAGridPlace=true;break;}
                Assert.That(takesAGridPlace,Is.True,body.name+" stands in a grid place's reservation, or the case tests nothing.");
            }
            var scratch=new Collider[256];var owners=NpcRead<HashSet<Transform>>("competitionRouteOwners");
            Physics.SyncTransforms();
            foreach(var pair in director.CompetitionFootprints)
            {
                Assert.That(pair.Value.FitsOn(deck),Is.True,pair.Key+"'s reservation is on the deck.");
                // The stand-ins are no route owners, so this whole-reservation query counts their capsules.
                Assert.That(pair.Value.HasStaticClearance(director.gameObject.scene.GetPhysicsScene(),floor,scratch,owners),Is.True,
                    pair.Key+"'s reservation clears every collider, the two houseguests' bodies among them.");
                foreach(var body in bodies)Assert.That(pair.Value.Meets(body.bounds),Is.False,pair.Key+"'s reservation stays off "+body.name+".");
                foreach(var other in director.CompetitionFootprints.Where(other=>other.Key!=pair.Key))
                    Assert.That(pair.Value.Overlaps(other.Value),Is.False,pair.Key+" and "+other.Key+" keep their own reservations.");
            }
            foreach(var instrument in instruments)
                Assert.That(instrument.Fits(director.CompetitionFootprints[instrument.ActorId]),Is.True,instrument.ActorId+"'s fitted meshes stay inside its reservation.");
            AssertStationsClearOfDrawnSetPieces(category,houseSize);
            yield return CancelInstrumentAttempt(before);
            var props=SceneComponents<Transform>().Single(transform=>transform.name=="Representative yard solid fixtures");Object.Destroy(props.gameObject);
            Object.Destroy(standers);yield return null;
        }

        /// <summary>Two still bodies, each a houseguest's capsule as the cast wears it, on the yard-south-chat venue's two standing places.</summary>
        private GameObject InstallSouthYardStandIns()
        {
            var template=SceneComponents<Gamesim.House.HouseNpc>().Where(npc=>npc.gameObject.activeInHierarchy)
                .Select(npc=>npc.GetComponent<CapsuleCollider>()).First(capsule=>capsule!=null && capsule.enabled);
            var slots=SceneComponents<Gamesim.House.HouseInteractionAnchor>().Where(anchor=>anchor.VenueId=="yard-south-chat")
                .OrderBy(anchor=>anchor.Slot).ToArray();
            Assert.That(slots,Has.Length.EqualTo(2),"The south yard's chat venue keeps its two standing places.");
            var owner=new GameObject("South yard stand-in houseguests");SceneManager.MoveGameObjectToScene(owner,director.gameObject.scene);
            foreach(var slot in slots)
            {
                var body=new GameObject("Stand-in at "+slot.VenueId+" slot "+slot.Slot,typeof(CapsuleCollider));
                body.transform.SetParent(owner.transform,false);body.transform.position=slot.Position;
                var capsule=body.GetComponent<CapsuleCollider>();
                capsule.center=template.center;capsule.radius=template.radius;capsule.height=template.height;capsule.direction=template.direction;
            }
            Physics.SyncTransforms();return owner;
        }

        private void AssertStationsClearOfDrawnSetPieces(string category,int houseSize)
        {
            var floor=SceneComponents<BoxCollider>().Single(collider=>collider.name=="Competition yard floor");
            var deck=floor.bounds;
            var instruments=SceneComponents<CompetitionApparatus>().Where(instrument=>instrument.gameObject.activeInHierarchy).ToArray();
            Assert.That(instruments,Has.Length.EqualTo(houseSize),category+": every entrant's instrument is fitted. "+CompetitionFieldDiagnostic());
            var stage=instruments[0].transform.parent.parent;
            Assert.That(instruments.All(instrument=>instrument.transform.parent.parent==stage),Is.True,"Every station hangs under the one arena root.");
            // The house's drawings standing on the deck: everything under the floor's root but the arena's
            // own, effects and floor paint (lanes, start lines, neon strips), which an instrument's stance
            // rails lie on. Not limited to the deck's rectangle, so the shell's yard walls count as well.
            var pieces=floor.transform.root.GetComponentsInChildren<MeshRenderer>()
                .Where(renderer=>renderer.enabled && !renderer.transform.IsChildOf(stage) && renderer.transform!=floor.transform
                    && renderer.bounds.max.y>deck.max.y+CompetitionStageScenery.Rise)
                .Where(renderer=>{var filter=renderer.GetComponent<MeshFilter>();return filter!=null && filter.sharedMesh!=null;})
                .ToArray();
            Assert.That(pieces.Any(renderer=>renderer.name=="bb_set_entrancearch"),Is.True,"The entrance arch is among the drawn pieces measured.");
            Assert.That(pieces.Count(renderer=>DeckHolds(deck,renderer.bounds.center)),Is.GreaterThanOrEqualTo(20),"The dressed yard's set pieces are measured, not an empty set.");
            var triangles=new Dictionary<MeshRenderer,Vector3[]>();
            var failures=new List<string>();
            var stations=new List<string>();
            foreach(var instrument in instruments)
            {
                var anchor=instrument.transform.parent;
                stations.Add(instrument.ActorId+"@("+anchor.position.x.ToString("F2")+","+anchor.position.z.ToString("F2")+")");
                var body=instrument.ActorId==director.Snapshot.playerId?player.transform
                    :SceneComponents<Gamesim.House.HouseNpc>().Single(npc=>npc.Id==instrument.ActorId).transform;
                var capsule=body.GetComponent<CapsuleCollider>();
                Assert.That(capsule,Is.Not.Null,instrument.ActorId+" stands on a capsule.");
                var shapes=new List<(string What,Bounds Box)>{("instrument",FittedInstrumentBounds(instrument)),("body",capsule.bounds)};
                foreach(var (what,box) in shapes)
                {
                    var inside=new Bounds(box.center,Vector3.Max(Vector3.zero,box.size-Vector3.one*.002f));
                    foreach(var piece in pieces)
                    {
                        if(!piece.bounds.Intersects(inside))continue;
                        if(!triangles.TryGetValue(piece,out var world))triangles[piece]=world=WorldTriangles(piece);
                        int met=0;
                        for(int i=0;i+2<world.Length;i+=3)if(TriangleMeetsBox(world[i],world[i+1],world[i+2],inside))met++;
                        if(met>0)failures.Add(category+" at "+houseSize+": "+instrument.ActorId+"'s "+what+" at ("+anchor.position.x.ToString("F2")+", "
                            +anchor.position.z.ToString("F2")+"), bounds "+inside.min.ToString("F3")+".."+inside.max.ToString("F3")+", meets "
                            +ScenePath(piece.transform)+" ("+met+" triangles).");
                    }
                }
            }
            Debug.Log("[Gamesim stations] "+category+" at "+houseSize+": stations "+string.Join(" ",stations)+"; "+pieces.Length+" drawn pieces measured.");
            Assert.That(failures,Is.Empty,string.Join("\n",failures));
        }

        private static bool DeckHolds(Bounds deck,Vector3 point)
            =>point.x>=deck.min.x && point.x<=deck.max.x && point.z>=deck.min.z && point.z<=deck.max.z;

        /// <summary>The instrument's world bounds: every part's mesh box, as fitted, whether or not its overlay is drawn this frame.</summary>
        private static Bounds FittedInstrumentBounds(CompetitionApparatus instrument)
        {
            bool any=false;var all=new Bounds();
            foreach(var filter in instrument.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;
                if(mesh==null || mesh.vertexCount==0)continue;
                var local=mesh.bounds;var matrix=filter.transform.localToWorldMatrix;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {
                    var corner=matrix.MultiplyPoint3x4(local.center+Vector3.Scale(local.extents,new Vector3(x,y,z)));
                    if(!any){all=new Bounds(corner,Vector3.zero);any=true;}else all.Encapsulate(corner);
                }
            }
            Assert.That(any,Is.True,instrument.ActorId+"'s instrument has meshes.");
            return all;
        }

        /// <summary>A renderer's triangles in world space; an imported mesh keeps no CPU copy, so the editor reads its data.</summary>
        private static Vector3[] WorldTriangles(MeshRenderer renderer)
        {
            var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var matrix=renderer.localToWorldMatrix;
            var world=new List<Vector3>();
#if UNITY_EDITOR
            using(var array=UnityEditor.MeshUtility.AcquireReadOnlyMeshData(mesh))
#else
            using(var array=Mesh.AcquireReadOnlyMeshData(mesh))
#endif
            {
                var data=array[0];
                using(var positions=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp))
                {
                    data.GetVertices(positions);
                    for(int sub=0;sub<data.subMeshCount;sub++)
                    {
                        var descriptor=data.GetSubMesh(sub);
                        if(descriptor.topology!=MeshTopology.Triangles)continue;
                        using(var indices=new NativeArray<int>(descriptor.indexCount,Allocator.Temp))
                        {
                            data.GetIndices(indices,sub,true);
                            for(int i=0;i+2<indices.Length;i+=3)
                            {
                                world.Add(matrix.MultiplyPoint3x4(positions[indices[i]]));
                                world.Add(matrix.MultiplyPoint3x4(positions[indices[i+1]]));
                                world.Add(matrix.MultiplyPoint3x4(positions[indices[i+2]]));
                            }
                        }
                    }
                }
            }
            return world.ToArray();
        }

        /// <summary>The separating-axis test of a world-axis box against a triangle: the box's three faces, the triangle's face and the nine edge crossings.</summary>
        private static bool TriangleMeetsBox(Vector3 a,Vector3 b,Vector3 c,Bounds box)
        {
            Vector3 p=a-box.center,q=b-box.center,r=c-box.center,half=box.extents;
            if(Mathf.Max(p.x,Mathf.Max(q.x,r.x))<-half.x || Mathf.Min(p.x,Mathf.Min(q.x,r.x))>half.x)return false;
            if(Mathf.Max(p.y,Mathf.Max(q.y,r.y))<-half.y || Mathf.Min(p.y,Mathf.Min(q.y,r.y))>half.y)return false;
            if(Mathf.Max(p.z,Mathf.Max(q.z,r.z))<-half.z || Mathf.Min(p.z,Mathf.Min(q.z,r.z))>half.z)return false;
            Vector3 e0=q-p,e1=r-q,e2=p-r;
            if(SceneryApart(Vector3.Cross(e0,e1),p,q,r,half))return false;
            foreach(var axis in new[]{Vector3.right,Vector3.up,Vector3.forward})
                if(SceneryApart(Vector3.Cross(axis,e0),p,q,r,half) || SceneryApart(Vector3.Cross(axis,e1),p,q,r,half)
                    || SceneryApart(Vector3.Cross(axis,e2),p,q,r,half))return false;
            return true;
        }

        private static bool SceneryApart(Vector3 axis,Vector3 p,Vector3 q,Vector3 r,Vector3 half)
        {
            if(axis.sqrMagnitude<1e-14f)return false;
            float a=Vector3.Dot(p,axis),b=Vector3.Dot(q,axis),c=Vector3.Dot(r,axis);
            float reach=half.x*Mathf.Abs(axis.x)+half.y*Mathf.Abs(axis.y)+half.z*Mathf.Abs(axis.z);
            return Mathf.Min(a,Mathf.Min(b,c))>reach || Mathf.Max(a,Mathf.Max(b,c))<-reach;
        }

        private static string ScenePath(Transform node)
        {
            var parts=new List<string>();
            for(var at=node;at!=null;at=at.parent)parts.Add(at.name);
            parts.Reverse();
            return string.Join("/",parts);
        }
    }
}
