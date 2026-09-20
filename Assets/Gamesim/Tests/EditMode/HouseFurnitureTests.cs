using System.Linq;
using Gamesim.House;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.Tests.EditMode
{
    public sealed class HouseFurnitureTests
    {
        [Test]
        public void LoungerAuthoringRepairsOnlyTheUnsafeLegacyOffsetWithoutMovingFurniture()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var prop=new GameObject("Authored lounger");SceneManager.MoveGameObjectToScene(prop,scene);
                prop.transform.position=new Vector3(9.408f,0,11);
                var anchor=HouseInteractionAnchor.Create(prop.transform,"yard-lounger-chat","Yard",0,prop.transform.position,0,true,Vector3.back*.55f);
                var position=anchor.Position;var contact=anchor.SeatContact;var camera=anchor.CameraPosition;
                Assert.That(HouseFurniture.AuthorLoungerApproaches(scene),Is.EqualTo(1));
                Assert.That(anchor.Approach.z,Is.EqualTo(10.60f).Within(.001f));
                Assert.That(anchor.Approach.z-.35f,Is.GreaterThan(10.125f),"The actual wall plus player radius must clear.");
                Assert.That(anchor.Approach.z,Is.GreaterThan(10f+.35f+.10f),"Floor interior uses a strict margin, not equality.");
                Assert.That(anchor.Position,Is.EqualTo(position));Assert.That(anchor.SeatContact,Is.EqualTo(contact));
                Assert.That(anchor.CameraPosition,Is.EqualTo(camera));Assert.That(anchor.VenueId,Is.EqualTo("yard-lounger-chat"));
                Assert.That(HouseFurniture.AuthorLoungerApproaches(scene),Is.Zero);
                anchor.Configure(anchor.VenueId,anchor.RoomId,anchor.Slot,true,Vector3.left*.7f);
                var custom=anchor.Approach;
                Assert.That(HouseFurniture.AuthorLoungerApproaches(scene),Is.Zero,"An authored custom approach must not be overwritten.");
                Assert.That(anchor.Approach,Is.EqualTo(custom));
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }

        [Test]
        public void KitchenAuthoringRequiresARealCounterAndRuntimeCatalogCannotInventFurniture()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                Assert.That(HouseFurniture.InScene(scene),Is.Empty);
                Assert.That(HouseFurniture.AuthorKitchenAnchor(scene),Does.Contain("no substitute"));
                Assert.That(HouseInteractionAnchors.InScene(scene),Is.Empty);
                var floor=new GameObject("Kitchen floor",typeof(BoxCollider));
                SceneManager.MoveGameObjectToScene(floor,scene);
                floor.GetComponent<BoxCollider>().size=new Vector3(14,.2f,10);
                var counter=GameObject.CreatePrimitive(PrimitiveType.Cube);
                counter.name="bb_set_kitchenrun";SceneManager.MoveGameObjectToScene(counter,scene);
                counter.transform.position=new Vector3(2,.55f,4);
                counter.transform.localScale=new Vector3(6,.9f,.7f);
                Assert.That(HouseFurniture.InScene(scene),Is.Empty,"Merely discovering a prop must not author it at runtime.");
                Assert.That(HouseFurniture.AuthorKitchenAnchor(scene),Is.Null);
                Assert.That(HouseInteractionAnchors.TryFind(scene,HouseFurniture.KitchenAnchor,0,out var anchor),Is.True);
                Assert.That(anchor.transform.parent,Is.EqualTo(counter.transform));
                Assert.That(anchor.Seated,Is.False);
                Assert.That(anchor.RoomId,Is.EqualTo("Kitchen"));
                Assert.That(anchor.Approach.y,Is.EqualTo(.1f).Within(.001f));
                Assert.That(anchor.Approach.z,Is.LessThan(counter.GetComponent<Renderer>().bounds.min.z));
                Assert.That(HouseFurniture.AtProp(scene,counter.transform),Is.SameAs(anchor));
                Assert.That(HouseFurniture.AtProp(scene,floor.transform),Is.Null);
                var before=anchor.Approach;
                counter.transform.position+=Vector3.right*3;
                Assert.That(anchor.Approach,Is.EqualTo(before+Vector3.right*3));
                Assert.That(HouseFurniture.AuthorKitchenAnchor(scene),Is.Null);
                Assert.That(HouseFurniture.InScene(scene).Count(),Is.EqualTo(1));
                Assert.That(HouseInteractionAnchors.TryFind(scene,HouseFurniture.KitchenAnchor,0,out var same),Is.True);
                Assert.That(same,Is.SameAs(anchor),"Reauthoring preserves the moved prop's existing identity.");
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
