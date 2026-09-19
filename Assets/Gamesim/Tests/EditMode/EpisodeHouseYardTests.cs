using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The yard as it ships: the competition circle and podiums are the authored pieces standing in
    /// for the primitives (which stay, unlit, with the colliders the NavMesh was baked from), and
    /// the water no longer stands on a podium - the pool's first placement put its corner through
    /// podium 3, which nothing checked.
    /// </summary>
    public sealed class EpisodeHouseYardTests
    {
        private const string EpisodeScene = "Assets/Gamesim/Scenes/EpisodeHouse.unity";

        [Test]
        public void TheCompetitionCircleAndPodiumsAreTheAuthoredOnes()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();

                var circle = all.Where(t => t.name == "bb_set_compring").ToArray();
                Assert.That(circle, Has.Length.EqualTo(1), "one authored competition circle");
                Assert.That(circle[0].position.x, Is.EqualTo(0f).Within(0.05f), "on the yard's centre line");
                Assert.That(circle[0].position.z, Is.EqualTo(15f).Within(0.05f), "where the primitive rings were");
                var segments = all.Where(t => t.parent != null && t.parent.name.StartsWith("Ring ", System.StringComparison.Ordinal))
                    .Select(t => t.GetComponent<Renderer>()).Where(r => r != null).ToArray();
                Assert.That(segments, Has.Length.EqualTo(144), "three rings of 48 primitive segments remain in the scene");
                Assert.That(segments.All(r => !r.enabled), Is.True, "every primitive segment is unlit under the authored circle");

                var blocks = all.Where(t => t.name.StartsWith("Competition podium ", System.StringComparison.Ordinal) && !t.name.EndsWith("(set)"))
                    .ToArray();
                Assert.That(blocks, Has.Length.EqualTo(3), "the three authored podium blocks");
                foreach (var block in blocks)
                {
                    Assert.That(block.GetComponent<Collider>(), Is.Not.Null, block.name + " keeps the collider the NavMesh was baked from");
                    Assert.That(block.GetComponent<Renderer>().enabled, Is.False, block.name + " is unlit under its authored podium");
                    var set = all.Single(t => t.name == block.name + " (set)");
                    var mesh = set.GetComponentInChildren<MeshFilter>(true);
                    Assert.That(mesh != null && mesh.sharedMesh != null && mesh.sharedMesh.name == "bb_set_podium", Is.True,
                        block.name + " (set) is the authored podium, not the primitive composition");
                    var bounds = block.GetComponent<Renderer>().bounds;
                    Assert.That(Vector3.Distance(set.position, new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)), Is.LessThan(0.02f),
                        "the authored podium stands exactly on its block");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void TheWaterStandsClearOfThePodiumsAndOfItself()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                var pool = Footprint(all.Single(t => t.name == "bb_set_pool"));
                var tub = Footprint(all.Single(t => t.name == "bb_set_hottub"));
                var loungers = all.Where(t => t.name == "bb_set_lounger").Select(Footprint).ToArray();
                var podiums = all.Where(t => t.name.StartsWith("Competition podium ", System.StringComparison.Ordinal) && !t.name.EndsWith("(set)"))
                    .Select(t => t.GetComponent<Renderer>().bounds).ToArray();
                Assert.That(loungers, Has.Length.EqualTo(3));
                Assert.That(podiums, Has.Length.EqualTo(3));

                foreach (var podium in podiums)
                {
                    Assert.That(Overlaps(pool, podium), Is.False, "the pool must not stand on a podium");
                    Assert.That(Overlaps(tub, podium), Is.False, "the hot tub must not stand on a podium");
                    foreach (var lounger in loungers) Assert.That(Overlaps(lounger, podium), Is.False, "a lounger must not stand on a podium");
                }
                Assert.That(Overlaps(pool, tub), Is.False, "the hot tub is not in the pool");
                foreach (var lounger in loungers) Assert.That(Overlaps(pool, lounger), Is.False, "a lounger is beside the pool, not in it");
                var yard = all.Single(t => t.name == "Competition yard floor").GetComponent<Renderer>().bounds;
                foreach (var piece in new[] { pool, tub }.Concat(loungers))
                {
                    Assert.That(piece.min.x, Is.GreaterThanOrEqualTo(yard.min.x - 0.01f), "inside the yard's west wall");
                    Assert.That(piece.max.x, Is.LessThanOrEqualTo(yard.max.x + 0.01f), "inside the yard's east wall");
                    Assert.That(piece.min.z, Is.GreaterThanOrEqualTo(yard.min.z - 0.01f), "inside the yard's south wall");
                    Assert.That(piece.max.z, Is.LessThanOrEqualTo(yard.max.z + 0.01f), "inside the yard's north fence");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Bounds Footprint(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty, root.name + " renders nothing.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static bool Overlaps(Bounds a, Bounds b) =>
            a.min.x < b.max.x && a.max.x > b.min.x && a.min.z < b.max.z && a.max.z > b.min.z;
    }
}
