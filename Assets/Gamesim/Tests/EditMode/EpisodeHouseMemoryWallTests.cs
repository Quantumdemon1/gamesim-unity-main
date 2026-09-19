using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The memory wall as it ships: the authored sixteen-frame wall on the living room's west
    /// wall, each frame with the border the runtime tints and the portrait quad it paints, and
    /// nothing standing in front of it - a bookcase did, and nothing checked.
    /// </summary>
    public sealed class EpisodeHouseMemoryWallTests
    {
        private const string EpisodeScene = "Assets/Gamesim/Scenes/EpisodeHouse.unity";

        [Test]
        public void TheMemoryWallIsTheAuthoredSixteenAndNothingStandsInFrontOfIt()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                var wall = all.Single(t => t.GetComponent<MemoryWall>() != null);
                var frames = wall.Cast<Transform>()
                    .Where(t => t.name.StartsWith(MemoryWall.FramePrefix, System.StringComparison.Ordinal))
                    .OrderBy(t => t.name, System.StringComparer.Ordinal).ToArray();
                Assert.That(frames, Has.Length.EqualTo(16), "one frame per seat of the largest house");
                for (int i = 0; i < frames.Length; i++)
                {
                    var border = frames[i].Find(MemoryWall.BorderChild);
                    Assert.That(border, Is.Not.Null, frames[i].name + " has its border");
                    var mesh = border.GetComponent<MeshFilter>();
                    Assert.That(mesh != null && mesh.sharedMesh != null && mesh.sharedMesh.name == "bb_set_memorywall_f" + i.ToString("00"), Is.True,
                        frames[i].name + "'s border is the authored frame " + i);
                    Assert.That(frames[i].Find(MemoryWall.PortraitChild), Is.Not.Null, frames[i].name + " has its portrait");
                }

                // On the west wall's inner face, along the living room's half of it.
                var west = all.Single(t => t.name == "West wall").GetComponent<Renderer>().bounds;
                Assert.That(wall.position.x, Is.EqualTo(west.max.x + 0.06f).Within(0.02f), "just proud of the inner face");
                Assert.That(wall.position.z, Is.EqualTo((west.min.z + west.center.z) * 0.5f).Within(0.05f), "centred on the living room's half");
                var span = frames.Select(f => f.Find(MemoryWall.BorderChild).GetComponent<Renderer>().bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                Assert.That(span.max.y, Is.LessThan(west.max.y), "under the top of the cutaway wall");

                // Nothing tall stands in the band in front of it.
                var band = new Bounds(new Vector3(span.max.x + 0.4f, 0.75f, span.center.z), new Vector3(0.8f, 1.5f, span.size.z));
                // Props only: the shell, the walls and the floors span the house and meet every band.
                var standing = all.Where(t => t.GetComponent<Renderer>() != null && !t.IsChildOf(wall) && t.GetComponent<Renderer>().enabled)
                    .Select(t => t.GetComponent<Renderer>().bounds)
                    .Where(b => b.size.x < 6f && b.size.z < 6f)
                    .Where(b => b.size.y > 0.6f && b.max.y > 0.6f && b.Intersects(band))
                    .Select(b => b.ToString()).ToArray();
                Assert.That(standing, Is.Empty, "something over 0.6 m stands in front of the memory wall: " + string.Join(" | ", standing));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
