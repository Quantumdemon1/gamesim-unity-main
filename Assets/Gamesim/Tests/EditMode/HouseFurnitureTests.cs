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
        /// <summary>
        /// Clicking a prop and idling at one are different questions, and the diary chair is the case
        /// that proves it. Nobody idles at the diary chair, so it is not in the activity catalogue -
        /// and it is the one prop in the house every player tries to click. Widening the activity
        /// catalogue to reach it would quietly change where cooling-down houseguests spend their idle
        /// time, so the click has a catalogue of its own.
        /// </summary>
        [Test]
        public void TheDiaryChairIsClickableWithoutBecomingSomewhereToIdle()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var prop = new GameObject("bb_set_diarychair");
                SceneManager.MoveGameObjectToScene(prop, scene);
                prop.transform.position = new Vector3(7f, 0f, 4f);
                var anchor = HouseInteractionAnchor.Create(prop.transform,
                    HouseInteractionAnchors.DiaryVenue, "Private", 0, prop.transform.position, 180f, true);

                Assert.That(HouseFurniture.TryDescribe(anchor, out _, out _), Is.False,
                    "The diary chair is not somewhere a houseguest is posed between conversations.");
                Assert.That(HouseFurniture.InScene(scene).ToArray(), Has.No.Member(anchor),
                    "Adding it to the activity catalogue would move the ambient routine.");

                Assert.That(HouseFurniture.TryClick(anchor, out var what, out var caption), Is.True,
                    "The diary chair has to be clickable; it is the first thing anybody tries.");
                Assert.That(what, Is.EqualTo(HousePropClick.Diary));
                Assert.That(caption, Is.Not.Null.And.Not.Empty);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        /// <summary>
        /// A click has to reach the prop through whatever the collision pass hung underneath it. The
        /// box lives on a child rather than on the prop root, and the anchor is a sibling of that
        /// child, so the lookup has to walk up to the prop rather than compare transforms.
        /// </summary>
        [Test]
        public void AClickOnAPropsCollisionProxyFindsThePropsAnchor()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var prop = new GameObject("bb_set_diarychair");
                SceneManager.MoveGameObjectToScene(prop, scene);
                var anchor = HouseInteractionAnchor.Create(prop.transform,
                    HouseInteractionAnchors.DiaryVenue, "Private", 0, prop.transform.position, 180f, true);

                var proxy = new GameObject("Collision");
                proxy.transform.SetParent(prop.transform, false);

                Assert.That(HouseFurniture.AtProp(scene, proxy.transform), Is.SameAs(anchor),
                    "A ray hits the proxy, never the anchor; the prop is what joins them.");
                Assert.That(HouseFurniture.AtProp(scene, prop.transform), Is.SameAs(anchor));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        /// <summary>
        /// A lounger is 1.90 m long and 0.65 m wide, so an approach along its length stands inside
        /// it. Both historical offsets did: the original .55 back, and the .40 back that replaced it,
        /// which measures 0.00 m of clearance from the lounger's own footprint in the shipped scene.
        /// A NavMesh bake erodes by the agent radius and would take the floor out from under whoever
        /// was walking there - which is the same failure that once took seventeen PlayMode tests
        /// down, arriving from the other direction.
        ///
        /// <para>So the repair moves sideways, which clears the 0.65 m width at 0.95 m and is also
        /// how a person gets onto a lounger. The wall behind still has to clear, but it is no longer
        /// the reason for the number.</para>
        /// </summary>
        [Test]
        public void LoungerAuthoringMovesBothLegacyApproachesClearOfTheLoungerItself()
        {
            foreach (var legacy in new[] { Vector3.back * .55f, Vector3.back * .40f })
            {
                var scene=EditorSceneManager.NewPreviewScene();
                try
                {
                    var prop=new GameObject("Authored lounger");SceneManager.MoveGameObjectToScene(prop,scene);
                    prop.transform.position=new Vector3(9.408f,0,11);
                    var anchor=HouseInteractionAnchor.Create(prop.transform,"yard-lounger-chat","Yard",0,prop.transform.position,0,true,legacy);
                    var position=anchor.Position;var contact=anchor.SeatContact;var camera=anchor.CameraPosition;

                    Assert.That(HouseFurniture.AuthorLoungerApproaches(scene),Is.EqualTo(1),
                        "The repair must recognise the " + legacy.z.ToString("0.00") + " offset.");

                    // Sideways, and far enough out that the lounger's own half-width (0.325) plus the
                    // 0.5 agent radius still leaves floor to stand on.
                    Assert.That(anchor.Approach.x,Is.EqualTo(9.408f-.95f).Within(.001f));
                    Assert.That(anchor.Approach.z,Is.EqualTo(11f).Within(.001f),
                        "and it no longer walks the length of the lounger to sit on it");
                    Assert.That(Mathf.Abs(anchor.Approach.x-prop.transform.position.x),Is.GreaterThan(.325f+.5f),
                        "The approach must clear the lounger's own footprint by more than the agent radius.");
                    Assert.That(anchor.Approach.z-.35f,Is.GreaterThan(10.125f),"The actual wall plus player radius must clear.");

                    Assert.That(anchor.Position,Is.EqualTo(position));Assert.That(anchor.SeatContact,Is.EqualTo(contact));
                    Assert.That(anchor.CameraPosition,Is.EqualTo(camera));Assert.That(anchor.VenueId,Is.EqualTo("yard-lounger-chat"));
                    Assert.That(HouseFurniture.AuthorLoungerApproaches(scene),Is.Zero,"and it is idempotent");

                    anchor.Configure(anchor.VenueId,anchor.RoomId,anchor.Slot,true,Vector3.left*.7f);
                    var custom=anchor.Approach;
                    Assert.That(HouseFurniture.AuthorLoungerApproaches(scene),Is.Zero,"An authored custom approach must not be overwritten.");
                    Assert.That(anchor.Approach,Is.EqualTo(custom));
                }
                finally{EditorSceneManager.ClosePreviewScene(scene);}
            }
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
