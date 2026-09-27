using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace Gamesim.Tests.EditMode
{
    public sealed class HouseInteractionAnchorTests
    {
        [Test]
        public void CeremonyDestinationUsesTheAuthoredScreenFrontInsideItsRoom()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var marker=new GameObject("Nomination marker");SceneManager.MoveGameObjectToScene(marker,scene);
                marker.transform.position=new Vector3(0,0,-15);marker.AddComponent<HouseRoomMarker>().Configure("Nomination");
                var screen=new GameObject("bb_set_ceremonyscreen");SceneManager.MoveGameObjectToScene(screen,scene);
                screen.transform.SetPositionAndRotation(new Vector3(0,0,-18.9f),Quaternion.Euler(0,180,0));
                HouseInteractionAnchors.EnsureDefaults(scene);
                Assert.That(HouseInteractionAnchors.TryFind(scene,HouseInteractionAnchors.EpisodeDestination,0,out var destination),Is.True);
                Assert.That(destination.RoomId,Is.EqualTo("Nomination"));
                Assert.That(Vector3.Distance(destination.Approach,new Vector3(0,0,-16.7f)),Is.LessThan(.001f),
                    "This prop's visible face is local -Z; +Z would place the player behind the exterior wall.");
                screen.transform.position+=Vector3.right;
                Assert.That(destination.Approach.x,Is.EqualTo(1).Within(.001f));
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }

        [Test]
        public void InitialBindingUsesMovedFurnitureAndNeverInventsMissingSeats()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var room=new GameObject("Kitchen marker");SceneManager.MoveGameObjectToScene(room,scene);
                room.AddComponent<HouseRoomMarker>().Configure("Kitchen");
                HouseInteractionAnchors.EnsureDefaults(scene);
                Assert.That(HouseInteractionAnchors.TryFind(scene,"kitchen-table-chat",0,out _),Is.False,
                    "An unfurnished room must not silently become a chair.");
                Assert.That(HouseInteractionAnchors.Validate(scene),Has.Some.Contains("kitchen-table-chat"));
                var chairs=new Transform[2];
                for(int i=0;i<2;i++)
                {
                    var chair=new GameObject("bb_set_ph_diningchair");SceneManager.MoveGameObjectToScene(chair,scene);
                    chair.transform.position=new Vector3(30+i*2,0,-6);chairs[i]=chair.transform;
                }
                HouseInteractionAnchors.EnsureDefaults(scene);
                Assert.That(HouseInteractionAnchors.TryFind(scene,"kitchen-table-chat",0,out var first),Is.True);
                Assert.That(HouseInteractionAnchors.TryFind(scene,"kitchen-table-chat",1,out var second),Is.True);
                Assert.That(first.transform.parent,Is.EqualTo(chairs[0]));
                Assert.That(second.transform.parent,Is.EqualTo(chairs[1]));
                Assert.That(Vector3.Distance(first.Position,chairs[0].position),Is.LessThan(.001f));
                Assert.That(Vector3.Distance(first.Approach,first.Position),Is.GreaterThan(.5f));
                int count=HouseInteractionAnchors.InScene(scene).Length;
                HouseInteractionAnchors.EnsureDefaults(scene);
                Assert.That(HouseInteractionAnchors.InScene(scene).Length,Is.EqualTo(count),"Authoring is idempotent.");
                var contact=first.SeatContact;
                chairs[0].position+=Vector3.right*3;
                Assert.That(first.SeatContact,Is.EqualTo(contact+Vector3.right*3));
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }

        [Test]
        public void PropTranslationAndRotationCarrySeatApproachAndCameraTogether()
        {
            var prop=new GameObject("Movable chair");
            try
            {
                prop.transform.position=new Vector3(4,0,7);
                var anchor=HouseInteractionAnchor.Create(prop.transform,"chair","Private",0,prop.transform.position,0,true,Vector3.forward);
                var oldPosition=anchor.Position;var oldApproach=anchor.Approach;var oldCamera=anchor.CameraPosition;
                prop.transform.position+=new Vector3(2,0,-1);
                Assert.That(anchor.Position,Is.EqualTo(oldPosition+new Vector3(2,0,-1)));
                Assert.That(anchor.Approach,Is.EqualTo(oldApproach+new Vector3(2,0,-1)));
                Assert.That(anchor.CameraPosition,Is.EqualTo(oldCamera+new Vector3(2,0,-1)));
                prop.transform.rotation=Quaternion.Euler(0,90,0);
                Assert.That(Vector3.Distance(anchor.Approach,anchor.Position+Vector3.right),Is.LessThan(.001f));
                Assert.That(Mathf.DeltaAngle(anchor.Facing,90),Is.EqualTo(0).Within(.001f));
                Assert.That(anchor.VenueId,Is.EqualTo("chair"));
            }
            finally{Object.DestroyImmediate(prop);}
        }

        [Test]
        public void FittedFurnitureScaleDoesNotScaleTheApproachDistance()
        {
            var prop=new GameObject("Scaled chair");
            try
            {
                prop.transform.localScale=new Vector3(2,3,4);
                var anchor=HouseInteractionAnchor.Create(prop.transform,"chair","Private",0,Vector3.zero,0,true,Vector3.forward);
                Assert.That(Vector3.Distance(anchor.Position,anchor.Approach),Is.EqualTo(1).Within(.001f));
            }
            finally{Object.DestroyImmediate(prop);}
        }
    }
}
