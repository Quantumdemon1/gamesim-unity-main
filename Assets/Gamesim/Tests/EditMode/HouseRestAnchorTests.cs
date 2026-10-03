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
    }
}
