using System.Collections.Generic;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The pick filter on real colliders (ROOM-SCOPE-PLAN §3.2 f, D13, RS2b): a piece of furniture's
    /// collision proxy is drawn as the prop it hangs under is, so a click or the hover passes over a prop
    /// standing on a room layer the camera leaves out and lands on the floor behind it; drawn, the prop
    /// takes it as it always has. Floors and walls keep their own colliders' answers and are always hit.
    /// Built far from anything else a test might leave lying around; edit mode runs no physics step, so
    /// every query follows a <c>Physics.SyncTransforms</c>. The rules alone are HouseRoomScopePickTests'.
    /// </summary>
    public sealed class HouseScopePickTests
    {
        private static readonly Vector3 Away = new Vector3(0f, 0f, -600f);
        private readonly List<GameObject> made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in made) if (go != null) Object.DestroyImmediate(go);
            made.Clear();
        }

        private static int ViewOf(string room)
        {
            int kept = RoomLayers.NothingKept;
            return RoomLayers.ScopeMask(-1, RoomSet.Of(room), ref kept);
        }

        /// <summary>
        /// A prop as the set builds one: its art collider-free on <paramref name="artLayer"/> (a room's, where
        /// RS2a places it), its collision on a child called Collision on the furniture layer
        /// (HouseFurnitureCollision). Two metres by one, 0.8 m high, standing on the floor at <see cref="Away"/>.
        /// </summary>
        private GameObject Prop(int artLayer, out BoxCollider proxy)
        {
            var prop = new GameObject("a sofa");
            made.Add(prop);
            prop.transform.position = Away;
            var art = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(art.GetComponent<Collider>());
            art.name = "sofa art";
            art.layer = artLayer;
            art.transform.SetParent(prop.transform, false);
            art.transform.localPosition = new Vector3(0f, .4f, 0f);
            art.transform.localScale = new Vector3(2f, .8f, 1f);
            var collision = new GameObject("Collision") { layer = HouseLayers.Furniture };
            collision.transform.SetParent(prop.transform, false);
            proxy = collision.AddComponent<BoxCollider>();
            proxy.center = new Vector3(0f, .4f, 0f);
            proxy.size = new Vector3(2f, .8f, 1f);
            return prop;
        }

        /// <summary>A floor as the house's are: a flat box carrying its own renderer, on layer 0, its top at <see cref="Away"/>.</summary>
        private GameObject Floor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            made.Add(floor);
            floor.name = "a floor";
            floor.transform.position = Away + Vector3.down * .05f;
            floor.transform.localScale = new Vector3(6f, .1f, 6f);
            return floor;
        }

        /// <summary>A camera that renders nothing, for its culling mask.</summary>
        private Camera Eye()
        {
            var host = new GameObject("an eye");
            made.Add(host);
            var eye = host.AddComponent<Camera>();
            eye.enabled = false;
            return eye;
        }

        [Test]
        public void AFurnitureProxyIsDrawnAsItsPropIs()
        {
            var prop = Prop(RoomLayers.Kitchen, out var proxy);
            Assert.That(HouseScopePick.OwnerOf(proxy), Is.SameAs(prop.transform), "A proxy's owner is the prop it hangs under.");
            Assert.That(HouseScopePick.Draws(ViewOf("Living"), prop.transform), Is.False, "Viewing the living room, the kitchen's sofa is not drawn.");
            Assert.That(HouseScopePick.Draws(ViewOf("Kitchen"), prop.transform), Is.True, "Viewing the kitchen, it is.");
            Assert.That(HouseScopePick.Draws(-1, prop.transform), Is.True, "The whole house draws it.");
            Assert.That(HouseScopePick.Draws(ViewOf("Living"), proxy.transform), Is.True,
                "Read as its own owner the proxy draws nothing, so it would count as drawn in every view: why its owner is its prop.");

            // A prop with one piece in the shell (layer 0) is drawn in every view; that piece switched off,
            // it is its kitchen art again.
            var band = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(band.GetComponent<Collider>());
            band.transform.SetParent(prop.transform, false);
            Assert.That(HouseScopePick.Draws(ViewOf("Living"), prop.transform), Is.True, "One piece in the shell: drawn in every view.");
            band.GetComponent<Renderer>().enabled = false;
            Assert.That(HouseScopePick.Draws(ViewOf("Living"), prop.transform), Is.False, "That piece off: the kitchen's again.");
        }

        [Test]
        public void AClickPassesOverHiddenFurnitureToTheFloorBehindIt()
        {
            var floor = Floor();
            var prop = Prop(RoomLayers.Kitchen, out var proxy);
            var eye = Eye();
            Physics.SyncTransforms();
            // From above and to one side, in through the sofa's top near its far edge, out through its far
            // side 0.64 m up, and down to the floor 1.1 m past it.
            var origin = Away + new Vector3(-3f, 3f, 0f);
            var ray = new Ray(origin, (Away + new Vector3(.9f, .7f, 0f) - origin).normalized);
            Assert.That(Physics.Raycast(ray, out var raw, 20f, HouseLayers.Pick, QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(raw.collider, Is.SameAs(proxy), "Physics meets the sofa first.");

            eye.cullingMask = ViewOf("Living");
            Assert.That(HouseScopePick.Draws(eye, proxy), Is.False, "Viewing the living room, the camera does not draw the sofa.");
            Assert.That(HouseScopePick.Raycast(eye, ray, 20f, HouseLayers.Pick, out var hit), Is.True, "Something stands behind it.");
            Assert.That(hit.collider.gameObject, Is.SameAs(floor), "The pick passes over the hidden sofa to the floor behind it (D13).");
            Assert.That(hit.point.x - Away.x, Is.GreaterThan(1f), "past the sofa's far side: " + (hit.point - Away) + ".");

            eye.cullingMask = ViewOf("Kitchen");
            Assert.That(HouseScopePick.Raycast(eye, ray, 20f, HouseLayers.Pick, out hit) && hit.collider == proxy, Is.True, "Viewing the kitchen, the sofa takes the click.");

            eye.cullingMask = -1;
            Assert.That(HouseScopePick.Raycast(eye, ray, 20f, HouseLayers.Pick, out hit), Is.True);
            Assert.That(hit.collider, Is.SameAs(proxy), "The whole house: Physics.Raycast's own hit, as it always was.");
            Assert.That(hit.distance, Is.EqualTo(raw.distance).Within(1e-4f));
        }

        [Test]
        public void FloorsAndWallsKeepTheirOwnAnswersAndAreAlwaysHit()
        {
            int living = ViewOf("Living");
            // A wall as the house keeps one: its collider on layer 0 under a group that also holds a room's
            // collider-free dressing, its renderer off (the authored shell mesh draws every wall).
            var group = new GameObject("House Architecture");
            made.Add(group);
            group.transform.position = Away;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "a wall";
            wall.transform.SetParent(group.transform, false);
            wall.transform.localPosition = new Vector3(0f, .75f, 0f);
            wall.transform.localScale = new Vector3(.2f, 1.5f, 4f);
            wall.GetComponent<Renderer>().enabled = false;
            var rug = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(rug.GetComponent<Collider>());
            rug.layer = RoomLayers.Kitchen;
            rug.transform.SetParent(group.transform, false);
            var wallCollider = wall.GetComponent<Collider>();
            Assert.That(HouseScopePick.OwnerOf(wallCollider), Is.SameAs(wall.transform), "A wall on layer 0 is its own owner, not its group's.");
            Assert.That(HouseScopePick.Draws(living, HouseScopePick.OwnerOf(wallCollider)), Is.True, "Nothing of it drawing, it counts as drawn: always hit.");

            var floor = Floor();
            var floorCollider = floor.GetComponent<Collider>();
            Assert.That(HouseScopePick.OwnerOf(floorCollider), Is.SameAs(floor.transform), "A floor is its own owner.");
            foreach (var room in RoomLayers.RoomIds)
                Assert.That(HouseScopePick.Draws(ViewOf(room), floor.transform), Is.True, "Its renderer on layer 0, the shell: drawn viewing the " + room + ".");

            // Off the furniture layer, a collider under a prop keeps its own answer: only proxies stand on 8.
            var prop = Prop(RoomLayers.Kitchen, out var proxy);
            proxy.gameObject.layer = 0;
            Assert.That(HouseScopePick.OwnerOf(proxy), Is.SameAs(proxy.transform), "On layer 0, the collider under the sofa is its own owner.");
            Assert.That(HouseScopePick.OwnerOf(proxy), Is.Not.SameAs(prop.transform));
        }
    }
}
