using System.Linq;
using Gamesim.House;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.Tests.EditMode
{
    public sealed class HouseRestAnchorTests
    {
        [Test]
        public void AuthoredLoungersAndCouchesGainRestWithoutChangingTheSixteenCeremonyPlaces()
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/Gamesim/Scenes/EpisodeHouse.unity");
            try
            {
                CeremonySeating.Ensure(scene,16);
                var gallery=CeremonySeating.Anchors(scene,CeremonySeating.GallerySeat);
                var hot=CeremonySeating.Anchors(scene,CeremonySeating.HotSeat);
                Assert.That(gallery.Count+hot.Count,Is.EqualTo(16));
                var before=gallery.Concat(hot).Select(a=>(a.Position,a.Approach,a.Facing,a.SeatContact)).ToArray();
                var home=HouseInteractionAnchors.InScene(scene).Where(a=>a.VenueId==HouseFurniture.LoungerAnchor && a.Slot<2).OrderBy(a=>a.Slot).ToArray();
                Assert.That(home,Has.Length.EqualTo(2));
                var savedHome=home.Select(a=>(a.Position,a.Approach,a.Facing,a.SeatContact)).ToArray();
                HouseInteractionAnchors.EnsureDefaults(scene);
                HouseConversationSpots.Ensure(scene);
                var rest=HouseFurniture.InScene(scene).Where(a=>HouseFurniture.IndependentRest(a)).ToArray();
                Assert.That(rest.Count(a=>a.VenueId==HouseFurniture.LoungerAnchor),Is.EqualTo(3));
                Assert.That(rest.Count(a=>a.VenueId==HouseFurniture.LoungeAnchor),Is.EqualTo(14));
                Assert.That(rest.All(HouseConversationSpots.OnFurniture),Is.True,"Every cushion is inside an actual authored renderer.");
                var spare=rest.Single(a=>a.VenueId==HouseFurniture.LoungerAnchor && a.Slot==2);
                Assert.That(home.Select(a=>(a.Position,a.Approach,a.Facing,a.SeatContact)),Is.EqualTo(savedHome),
                    "Adding the spare never rewrites either saved Home place.");
                Assert.That(spare.Position.x,Is.LessThan(home[0].Position.x),"The spare is west of the saved pair, away from the east fence's dead end.");
                var yard=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Transform>(true))
                    .Single(t=>t.name=="Competition yard floor").GetComponent<BoxCollider>();
                var floorPoint=yard.transform.InverseTransformPoint(spare.Approach)-yard.center;
                Assert.That(Mathf.Abs(floorPoint.x),Is.LessThan(yard.size.x*.5f-.6f/yard.transform.lossyScale.x));
                Assert.That(Mathf.Abs(floorPoint.z),Is.LessThan(yard.size.z*.5f-.6f/yard.transform.lossyScale.z),
                    "A visually clear corner beyond the Yard is not a rest approach.");
                foreach(var renderer in spare.transform.parent.GetComponentsInChildren<Renderer>())
                    foreach(var saved in home)
                    {
                        var bounds=renderer.bounds;var point=new Vector3(saved.Approach.x,bounds.center.y,saved.Approach.z);
                        Assert.That(Vector3.Distance(bounds.ClosestPoint(point),point),Is.GreaterThan(.5f),
                            "The moved spare must not obstruct either unchanged Home approach.");
                    }
                foreach(var neighbour in rest.Where(a=>a.VenueId==HouseFurniture.LoungerAnchor && a!=spare))
                    foreach(var renderer in neighbour.transform.parent.GetComponentsInChildren<Renderer>())
                    {
                        var bounds=renderer.bounds;var point=new Vector3(spare.Approach.x,bounds.center.y,spare.Approach.z);
                        Assert.That(Vector3.Distance(bounds.ClosestPoint(point),point),Is.GreaterThan(.5f),
                            "The spare lounger must approach from the open side, not inside the neighbouring chair's agent clearance.");
                    }
                Assert.That(HouseConversationSpots.InScene(scene).Any(s=>s.Family==HouseFurniture.LoungerAnchor
                    && (s.First.transform.parent==rest.Single(a=>a.VenueId==HouseFurniture.LoungerAnchor && a.Slot==2).transform.parent
                        || s.Second.transform.parent==rest.Single(a=>a.VenueId==HouseFurniture.LoungerAnchor && a.Slot==2).transform.parent)),Is.True,
                    "The third lounger participates in a real reachable-distance chat pair.");
                foreach(var spot in HouseConversationSpots.InScene(scene).Where(s=>s.Family==HouseFurniture.LoungerAnchor))
                    foreach(var seat in new[]{spot.First,spot.Second})
                    {
                        var original=rest.Single(a=>a.VenueId==HouseFurniture.LoungerAnchor && a.transform.parent==seat.transform.parent);
                        Assert.That(Vector3.Distance(seat.Approach,original.Approach),Is.LessThan(.001f),
                            "Chat must use the same clear approach as the actual lounger, rather than restoring a shared old offset.");
                    }
                Assert.That(gallery.Concat(hot).Select(a=>(a.Position,a.Approach,a.Facing,a.SeatContact)),Is.EqualTo(before));
                int anchors=HouseInteractionAnchors.InScene(scene).Length;
                HouseInteractionAnchors.EnsureDefaults(scene);HouseConversationSpots.Ensure(scene);
                Assert.That(HouseInteractionAnchors.InScene(scene).Length,Is.EqualTo(anchors),"Repeated projection does not grow seats or chat pairs.");
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }

        [Test]
        public void SpareLoungerRequiresItsRealFloorAndRejectsStaticBlockedApproachesWithoutABake()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Gamesim/Art/Authored/SetPieces/bb_set_lounger.fbx");
                Assert.That(model,Is.Not.Null);
                var marker=new GameObject("Yard");SceneManager.MoveGameObjectToScene(marker,scene);
                marker.AddComponent<HouseRoomMarker>().Configure("Yard");
                foreach(float x in new[]{9.408f,10.808f,7.308f})
                {
                    var prop=(GameObject)PrefabUtility.InstantiatePrefab(model,scene);
                    prop.transform.position=new Vector3(x,0,11);
                }
                HouseInteractionAnchors.EnsureDefaults(scene);
                Assert.That(HouseInteractionAnchors.TryFind(scene,HouseFurniture.LoungerAnchor,2,out _),Is.False,
                    "A marker and visible furniture do not substitute for a real floor.");
                var home=HouseInteractionAnchors.InScene(scene).Where(a=>a.VenueId==HouseFurniture.LoungerAnchor).ToArray();
                Assert.That(home,Has.Length.EqualTo(2));
                var saved=home.Select(a=>(a.Position,a.Approach,a.Facing)).ToArray();
                var floor=new GameObject("Competition yard floor");SceneManager.MoveGameObjectToScene(floor,scene);
                floor.transform.position=new Vector3(0,-.15f,15);floor.AddComponent<BoxCollider>().size=new Vector3(28,.3f,10);
                var blocker=new GameObject("Static approach blocker");SceneManager.MoveGameObjectToScene(blocker,scene);
                blocker.transform.position=new Vector3(7.308f,1,11);blocker.AddComponent<BoxCollider>().size=new Vector3(8,2,5);
                HouseInteractionAnchors.EnsureDefaults(scene);
                Assert.That(HouseInteractionAnchors.TryFind(scene,HouseFurniture.LoungerAnchor,2,out _),Is.False,
                    "Real capsule blockers must reject every approach even when the imported mesh's corners look clear.");
                Object.DestroyImmediate(blocker);
                HouseInteractionAnchors.EnsureDefaults(scene);
                Assert.That(HouseInteractionAnchors.TryFind(scene,HouseFurniture.LoungerAnchor,2,out var spare),Is.True,
                    "Static authoring works in an isolated preview with no baked navigation data.");
                Assert.That(Vector3.Distance(spare.Approach,new Vector3(6.358f,0,11)),Is.LessThan(.001f));
                Assert.That(home.Select(a=>(a.Position,a.Approach,a.Facing)),Is.EqualTo(saved));
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }

        [Test]
        public void LoungerChatKeepsEachAuthoredApproachOnRotatedAndScaledFurniture()
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Gamesim/Art/Authored/SetPieces/bb_set_lounger.fbx");
            Assert.That(model,Is.Not.Null,"This regression uses the actual imported lounger.");
            foreach(var variant in new[]{(yaw:0f,scale:1f),(yaw:37f,scale:1.25f)})
            {
                var scene=EditorSceneManager.NewPreviewScene();
                try
                {
                    var originals=new HouseInteractionAnchor[2];
                    var yaw=Quaternion.Euler(0,variant.yaw,0);
                    for(int i=0;i<2;i++)
                    {
                        var prop=(GameObject)PrefabUtility.InstantiatePrefab(model,scene);
                        prop.transform.SetPositionAndRotation(new Vector3(10,0,10)+yaw*(Vector3.right*i*1.4f),yaw);
                        prop.transform.localScale=Vector3.one*variant.scale;
                        originals[i]=HouseInteractionAnchor.Create(prop.transform,HouseFurniture.LoungerAnchor,"Yard",i==0?0:2,
                            prop.transform.position,variant.yaw,true,Vector3.back*1.8f+Vector3.left*i*.4f);
                        originals[i].SetSeatHeight(.36f);
                    }
                    HouseConversationSpots.Ensure(scene);
                    var spot=HouseConversationSpots.InScene(scene).Single(s=>s.Family==HouseFurniture.LoungerAnchor);
                    foreach(var seat in new[]{spot.First,spot.Second})
                    {
                        var original=originals.Single(a=>a.transform.parent==seat.transform.parent);
                        Assert.That(Vector3.Distance(seat.Approach,original.Approach),Is.LessThan(.001f));
                        Assert.That(Vector3.Distance(seat.Position,original.Position),Is.LessThan(.001f));
                        Assert.That(Mathf.Abs(Mathf.DeltaAngle(seat.Facing,original.Facing)),Is.LessThan(.001f));
                    }
                    Assert.That(Vector3.Distance(spot.First.Approach,spot.Second.Approach),Is.EqualTo(1f).Within(.001f),
                        "The roots kept by the pair must be the roots whose spacing was validated.");
                }
                finally{EditorSceneManager.ClosePreviewScene(scene);}
            }
        }

        [Test]
        public void MissingCouchesAndLoungersDoNotBecomeInvisibleRestSeats()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                foreach(var room in new[]{"Living","Yard"})
                {
                    var marker=new GameObject(room);SceneManager.MoveGameObjectToScene(marker,scene);
                    marker.AddComponent<HouseRoomMarker>().Configure(room);
                }
                HouseInteractionAnchors.EnsureDefaults(scene);HouseConversationSpots.Ensure(scene);
                Assert.That(HouseFurniture.InScene(scene).Any(HouseFurniture.IndependentRest),Is.False);
                Assert.That(HouseConversationSpots.InScene(scene),Is.Empty);
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }

        [Test]
        public void HoHBenchSeatsUseImportedCushionBoundsAndStaySeparateOnTheSouthSide()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            var bench=BenchForFixture(scene,out var mesh,out var materials);
            try
            {
                bench.position=new Vector3(-10.4f,.1f,-14.4f);
                bench.rotation=Quaternion.Euler(0,35,0);bench.localScale=Vector3.one*1.25f;
                HouseInteractionAnchors.EnsureDefaults(scene);
                var seats=HouseFurniture.InScene(scene).Where(a=>a.VenueId==HouseFurniture.HoHBenchAnchor).OrderBy(a=>a.Slot).ToArray();
                Assert.That(seats,Has.Length.EqualTo(2));
                foreach(var seat in seats)
                {
                    var contact=bench.TransformPoint(new Vector3(.1f+(seat.Slot==0?-.355f:.355f),.555f,.05f));
                    Assert.That(Vector3.Distance(seat.SeatContact,contact),Is.LessThan(.001f),"Wood outside the cream submesh must not raise the seated contact.");
                    Assert.That(seat.Position.y,Is.EqualTo(.1f).Within(.001f));
                    Assert.That(Vector3.Dot(seat.transform.forward,-bench.forward),Is.GreaterThan(.999f));
                    Assert.That(Vector3.Dot(seat.Approach-seat.Position,-bench.forward),Is.GreaterThan(.9f));
                    Assert.That(seat.Approach.y,Is.EqualTo(.1f).Within(.001f));
                    Assert.That(HouseConversationSpots.OnFurniture(seat),Is.True);
                    Assert.That(HouseFurniture.IndependentRest(seat),Is.True);
                    Assert.That(HouseFurniture.Ambient(seat),Is.False,"The ambient cast does not acquire the private HoH bench.");
                    Assert.That(HouseFurniture.TryDescribe(seat,out var activity,out _),Is.True);
                    Assert.That(activity,Is.EqualTo(HouseFurnitureActivity.Rest));
                }
                Assert.That(Vector3.Distance(seats[0].Approach,seats[1].Approach),Is.GreaterThanOrEqualTo(HouseConversationSpots.RootsApart));
                var before=seats.Select(a=>(a.Position,a.Approach,a.SeatContact)).ToArray();
                HouseInteractionAnchors.EnsureDefaults(scene);
                Assert.That(HouseFurniture.InScene(scene).Count(a=>a.VenueId==HouseFurniture.HoHBenchAnchor),Is.EqualTo(2));
                Assert.That(seats.Select(a=>(a.Position,a.Approach,a.SeatContact)),Is.EqualTo(before));
                Assert.That(HouseInteractionAnchors.Meetings.Any(m=>m.Id==HouseFurniture.HoHBenchAnchor),Is.False,"Rest adds no saved conversation family.");
                var shift=new Vector3(2,0,3);bench.position+=shift;
                for(int i=0;i<seats.Length;i++)
                    Assert.That(Vector3.Distance(seats[i].SeatContact,before[i].SeatContact+shift),Is.LessThan(.001f),"The contact remains attached to the authored prop.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(mesh);foreach(var material in materials)Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void MissingCushionOrAnUndersizedHoHBenchCreatesNoImaginedSeats()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            var bench=BenchForFixture(scene,out var mesh,out var materials);
            try
            {
                materials[1].name="unrelated-upholstery";
                HouseInteractionAnchors.EnsureDefaults(scene);
                Assert.That(HouseFurniture.InScene(scene).Any(a=>a.VenueId==HouseFurniture.HoHBenchAnchor),Is.False);
                materials[1].name="bb_mat_amenity_cream";bench.localScale=Vector3.one*.5f;
                HouseInteractionAnchors.EnsureDefaults(scene);
                Assert.That(HouseFurniture.InScene(scene).Any(a=>a.VenueId==HouseFurniture.HoHBenchAnchor),Is.False,
                    "A struck or too small imported bench cannot fit two independent parked roots.");
                Object.DestroyImmediate(bench.gameObject);
                HouseInteractionAnchors.EnsureDefaults(scene);
                Assert.That(HouseFurniture.InScene(scene).Any(a=>a.VenueId==HouseFurniture.HoHBenchAnchor),Is.False);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(mesh);foreach(var material in materials)Object.DestroyImmediate(material);
            }
        }

        private static Transform BenchForFixture(Scene scene,out Mesh mesh,out Material[] materials)
        {
            var room=new GameObject("HoH");SceneManager.MoveGameObjectToScene(room,scene);
            room.AddComponent<HouseRoomMarker>().Configure("HoH");
            var floor=new GameObject("HoH floor");SceneManager.MoveGameObjectToScene(floor,scene);
            floor.AddComponent<BoxCollider>().size=new Vector3(20,.2f,40);
            var bench=new GameObject("bb_set_hohbench");SceneManager.MoveGameObjectToScene(bench,scene);
            var surface=new GameObject("Joined bench mesh");surface.transform.SetParent(bench.transform,false);
            surface.transform.localPosition=new Vector3(.1f,0,.05f);
            mesh=new Mesh{name="bench fixture with a taller non-cushion submesh"};
            mesh.vertices=new[]{new Vector3(-.71f,.555f,-.24f),new Vector3(.71f,.555f,-.24f),new Vector3(.71f,.555f,.24f),new Vector3(-.71f,.555f,.24f),
                new Vector3(-.8f,0,-.3f),new Vector3(.8f,1.2f,-.3f),new Vector3(.8f,1.2f,.3f),new Vector3(-.8f,1.2f,.3f)};
            mesh.subMeshCount=2;mesh.SetTriangles(new[]{4,5,6,4,6,7},0);mesh.SetTriangles(new[]{0,1,2,0,2,3},1);mesh.RecalculateBounds();
            surface.AddComponent<MeshFilter>().sharedMesh=mesh;
            materials=new[]{new Material(Shader.Find("Hidden/InternalErrorShader")){name="bb_mat_amenity_walnut"},
                new Material(Shader.Find("Hidden/InternalErrorShader")){name="bb_mat_amenity_cream"}};
            surface.AddComponent<MeshRenderer>().sharedMaterials=materials;
            return bench.transform;
        }
    }
}
