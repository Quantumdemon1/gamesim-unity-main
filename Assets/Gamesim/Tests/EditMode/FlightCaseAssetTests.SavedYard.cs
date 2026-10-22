using System.Linq;
using Gamesim.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gamesim.Tests.EditMode
{
    // The saved scene's side of the flight case: the yard's three original roots, placed by
    // HouseRoomFinish's own bottom-centre formula. Native scene proof, not a render approval.
    public sealed partial class FlightCaseAssetTests
    {
        [Test]
        public void TheSavedYardKeepsThreeOriginalRootsBottomAnchorsYawAndOpenCentre()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                var world = scene.GetRootGameObjects().Single(go => go.name == "House Architecture").transform;
                var yard = world.Find(HouseRoomFinish.RootName + "/Competition yard");
                Assert.That(yard, Is.Not.Null);
                var cases = yard.Cast<Transform>().Where(t => t.name == "Flight case").ToArray();
                Assert.That(cases.Length, Is.EqualTo(3));
                var floor = world.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Competition yard floor").GetComponent<Renderer>().bounds;
                // The exact room anchor source is the existing floor envelope; top already includes its surface.
                for (int i = 0; i < 3; i++)
                {
                    var item = cases[i];
                    Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(item.gameObject)), Is.EqualTo(ModelPath));
                    Near(item.localScale, Vector3.one, 1e-6f, "No envelope stretch");
                    Assert.That(Quaternion.Angle(item.rotation, Quaternion.Euler(0, i * 6f, 0)), Is.LessThanOrEqualTo(1e-4f));
                    var renderers = item.GetComponentsInChildren<Renderer>(true);
                    Assert.That(renderers.Length, Is.EqualTo(2));
                    Assert.That(item.GetComponentsInChildren<LODGroup>(true).Single().GetLODs().Length, Is.EqualTo(2));
                    var bounds = Bounds(renderers.SelectMany(r => new[] { r.bounds.min, r.bounds.max }));
                    Near(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z),
                        new Vector3(floor.min.x + 1f + (i == 2 ? 1.1f : 0f), floor.max.y, floor.center.z - 2.4f + i * .6f),
                        .001f, "Existing bottom-centre formula");
                    Assert.That(bounds.center.x, Is.LessThan(floor.min.x + 3f));
                    Assert.That(Vector2.Distance(new Vector2(bounds.center.x, bounds.center.z), new Vector2(floor.center.x, floor.center.z)), Is.GreaterThan(4f));
                    Assert.That(item.GetComponentsInChildren<Collider>(true), Is.Empty);
                    foreach (var child in item.GetComponentsInChildren<Transform>(true))
                        Assert.That(GameObjectUtility.GetStaticEditorFlags(child.gameObject), Is.EqualTo((StaticEditorFlags)0));
                    foreach (var renderer in renderers)
                    {
                        Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off), "Existing PieceAt policy, not old cube shadow parity.");
                        Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial), Is.EqualTo(MaterialPath));
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
