using System.Linq;
using Gamesim.House;
using NUnit.Framework;
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
                HouseInteractionAnchors.EnsureDefaults(scene);
                HouseConversationSpots.Ensure(scene);
                var rest=HouseFurniture.InScene(scene).Where(a=>HouseFurniture.IndependentRest(a)).ToArray();
                Assert.That(rest.Count(a=>a.VenueId==HouseFurniture.LoungerAnchor),Is.EqualTo(3));
                Assert.That(rest.Count(a=>a.VenueId==HouseFurniture.LoungeAnchor),Is.EqualTo(14));
                Assert.That(rest.All(HouseConversationSpots.OnFurniture),Is.True,"Every cushion is inside an actual authored renderer.");
                Assert.That(HouseConversationSpots.InScene(scene).Any(s=>s.Family==HouseFurniture.LoungerAnchor
                    && (s.First.transform.parent==rest.Single(a=>a.VenueId==HouseFurniture.LoungerAnchor && a.Slot==2).transform.parent
                        || s.Second.transform.parent==rest.Single(a=>a.VenueId==HouseFurniture.LoungerAnchor && a.Slot==2).transform.parent)),Is.True,
                    "The third lounger participates in a real reachable-distance chat pair.");
                Assert.That(gallery.Concat(hot).Select(a=>(a.Position,a.Approach,a.Facing,a.SeatContact)),Is.EqualTo(before));
                int anchors=HouseInteractionAnchors.InScene(scene).Length;
                HouseInteractionAnchors.EnsureDefaults(scene);HouseConversationSpots.Ensure(scene);
                Assert.That(HouseInteractionAnchors.InScene(scene).Length,Is.EqualTo(anchors),"Repeated projection does not grow seats or chat pairs.");
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
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
