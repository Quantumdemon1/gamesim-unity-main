using System.Collections.Generic;
using System.Linq;
using Gamesim.Editor;
using Gamesim.House;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Furniture has to answer two questions with different answers - <em>can I click this?</em> yes,
    /// and <em>does this block my view?</em> no - and one layer is what makes that possible. These
    /// are the rules that keep it true, written as behaviour rather than as arithmetic wherever the
    /// physics system can be asked directly.
    /// </summary>
    public sealed class HouseLayersTests
    {
        private const string EpisodeScene = "Assets/Gamesim/Scenes/EpisodeHouse.unity";

        private readonly List<GameObject> made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in made) if (go != null) Object.DestroyImmediate(go);
            made.Clear();
        }

        [Test]
        public void FurnitureHasItsOwnLayerAndTheLayerSaysWhatItCarries()
        {
            Assert.That(LayerMask.LayerToName(HouseLayers.Furniture), Is.EqualTo("Furniture"),
                "Layer " + HouseLayers.Furniture + " is what every furniture collider sits on. Renaming it in "
                + "TagManager without moving the constant would leave the masks pointing at a layer nobody "
                + "recognises, and nothing else in the project would notice.");
        }

        [Test]
        public void LookingIsBlindToFurnitureAndPointingIsNot()
        {
            int bit = 1 << HouseLayers.Furniture;
            Assert.That(HouseLayers.Pick & bit, Is.Not.Zero,
                "A click has to be able to hit the sofa it is aimed at.");
            Assert.That(HouseLayers.Sight & bit, Is.Zero,
                "A coffee table is not an answer to 'is anything solid between these two points'.");

            // Layer 2 is Unity's own Ignore Raycast, which the prototype's nav-only boxes live on.
            // Neither query may see it, or those boxes become dead click-spots all over the floor.
            Assert.That(HouseLayers.Pick & (1 << 2), Is.Zero);
            Assert.That(HouseLayers.Sight & (1 << 2), Is.Zero);
        }

        [Test]
        public void ARayStopsAtFurnitureForAPickAndCarriesOnForALook()
        {
            var sofa = GameObject.CreatePrimitive(PrimitiveType.Cube);
            made.Add(sofa);
            sofa.name = "a sofa";
            sofa.layer = HouseLayers.Furniture;
            sofa.transform.position = new Vector3(0f, 0f, 500f);

            var origin = new Vector3(0f, 0f, 495f);
            Assert.That(Physics.Raycast(origin, Vector3.forward, out var picked, 10f, HouseLayers.Pick,
                    QueryTriggerInteraction.Ignore), Is.True, "Pick must reach the sofa.");
            Assert.That(picked.transform, Is.SameAs(sofa.transform));
            Assert.That(Physics.Raycast(origin, Vector3.forward, out _, 10f, HouseLayers.Sight,
                    QueryTriggerInteraction.Ignore), Is.False, "Sight must pass straight through it.");
        }

        [Test]
        public void EveryFittedBoxInTheHouseIsOnTheFurnitureLayer()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                var proxies = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Where(HouseFurnitureCollision.IsProxy)
                    .ToArray();

                Assert.That(proxies, Is.Not.Empty,
                    "The house's furniture carries its collision on fitted proxies. None at all means the "
                    + "pass was never run on this scene, and every sofa in it is walk-through again.");

                foreach (var proxy in proxies)
                {
                    Assert.That(proxy.gameObject.layer, Is.EqualTo(HouseLayers.Furniture),
                        proxy.parent.name + "'s collision is on layer " + proxy.gameObject.layer
                        + ". On any other layer it either blocks sightlines or cannot be clicked.");
                    Assert.That(proxy.GetComponent<BoxCollider>(), Is.Not.Null,
                        proxy.parent.name + "'s proxy has no collider, so it is furniture-shaped nothing.");
                }

                // A proxy that was switched off has to say which room it was costing. An unexplained
                // inactive box is indistinguishable from one somebody disabled by accident.
                foreach (var proxy in proxies.Where(p => !p.gameObject.activeSelf))
                    Assert.That(proxy.name, Does.StartWith(HouseFurnitureCollision.OffPrefix),
                        proxy.parent.name + "'s collision is switched off with no reason recorded in its name.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void AFlatTileIsNotFurnitureHoweverLargeItsMeshIs()
        {
            // The trap this exists for: the box is measured in the prop's own space, where a unit
            // cube is 1.0 tall no matter what scale flattens it. Judged there, eighteen kitchen floor
            // tiles grew waist-high collision and the kitchen stopped being walkable.
            var tile = Prop("Tile", new Vector3(1.94f, 0.02f, 1.94f));
            Assert.That(HouseFurnitureCollision.Judge(tile.transform, out _),
                Is.EqualTo(HouseFurnitureCollision.Verdict.TooSmall),
                "A tile 2 cm thick is floor, not furniture.");
        }

        [Test]
        public void AThinPoleIsNotFurnitureEither()
        {
            var stanchion = Prop("Stanchion", new Vector3(0.09f, 1f, 0.09f));
            Assert.That(HouseFurnitureCollision.Judge(stanchion.transform, out _),
                Is.EqualTo(HouseFurnitureCollision.Verdict.TooSmall),
                "A 9 cm pole is something you walk past, not around.");
        }

        [Test]
        public void ABookcaseIsFurnitureAndItsBoxTurnsWithIt()
        {
            var bookcase = Prop("bookcaseOpen", new Vector3(0.8f, 1.45f, 0.32f));
            bookcase.transform.rotation = Quaternion.Euler(0f, 45f, 0f);

            Assert.That(HouseFurnitureCollision.Judge(bookcase.transform, out var local),
                Is.EqualTo(HouseFurnitureCollision.Verdict.Fit));

            // Measured in the prop's own frame, so a bookcase at 45 degrees is still 0.32 deep. An
            // axis-aligned box would read about 0.79 in both directions and block the wall it stands
            // against as thoroughly as the floor in front of it.
            var world = Vector3.Scale(local.size, bookcase.transform.lossyScale);
            Assert.That(world.z, Is.EqualTo(0.32f).Within(0.01f),
                "The box has to be the bookcase's depth, not its diagonal.");
        }

        [Test]
        public void SomethingStandingOnATableIsNotAnObstacle()
        {
            var mug = Prop("bb_set_mug", new Vector3(0.4f, 0.5f, 0.4f));
            mug.transform.position = new Vector3(0f, 0.92f, 500f);
            Assert.That(HouseFurnitureCollision.Judge(mug.transform, out _),
                Is.EqualTo(HouseFurnitureCollision.Verdict.OffFloor),
                "Nothing on a worktop should punch a hole in the floor plan beneath it.");
        }

        [Test]
        public void FurnitureStandingOnARoomMarkerKeepsTheFloorUnderIt()
        {
            var table = Prop("tableRound", new Vector3(1.5f, 0.78f, 1.5f));
            table.transform.position = new Vector3(0f, 0f, 500f);

            Assert.That(HouseFurnitureCollision.Judge(table.transform, out _),
                Is.EqualTo(HouseFurnitureCollision.Verdict.Fit),
                "With nothing routing to it, a round table is ordinary furniture.");

            var marker = new GameObject("Room - Somewhere");
            made.Add(marker);
            marker.transform.position = new Vector3(0f, 0f, 500f);
            marker.AddComponent<HouseRoomMarker>().Configure("Somewhere");

            Assert.That(HouseFurnitureCollision.Judge(table.transform, out _),
                Is.EqualTo(HouseFurnitureCollision.Verdict.OnADestination),
                "A prop sitting on the point every route into a room ends at must stay walkable, or "
                + "the room comes off the mesh entirely. This is how the nomination room was lost.");
        }

        /// <summary>A prop-shaped thing: a renderer at a size, with its collider stripped as the set does.</summary>
        private GameObject Prop(string name, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            made.Add(go);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale = size;
            // Far from anything else a test might have left lying around.
            go.transform.position = new Vector3(0f, size.y * 0.5f, 500f);
            return go;
        }
    }
}
