using System.Collections;
using System.Linq;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The story's scene stage (plan §5.1): the places it offers a scene's people are clear of
    /// furniture, even furniture cloned at run time, which the baked NavMesh has no hole under.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator SceneStage_OffersNoPlaceInsideFurnitureClonedAtRunTime()
        {
            var before = director.ScenePlacesForDiagnostics("Living", 24);
            Assert.That(before.Count, Is.GreaterThan(1), "The living room offers a scene places to stand.");

            // A seat cloned at run time on the ring: furniture layer, and no hole in the NavMesh under it.
            var spot = before[0];
            var seat = new GameObject("scene-stage-test-seat");
            SceneManager.MoveGameObjectToScene(seat, director.gameObject.scene);
            seat.layer = HouseLayers.Furniture;
            seat.transform.position = spot + Vector3.up * 0.5f;
            seat.AddComponent<BoxCollider>().size = new Vector3(0.9f, 1f, 0.9f);
            try
            {
                yield return null;
                var after = director.ScenePlacesForDiagnostics("Living", 24);
                Assert.That(after.Any(p => (p - spot).sqrMagnitude < 0.01f), Is.False, "Nobody is offered the place inside the new seat.");
                Assert.That(after.Count, Is.GreaterThan(0), "The ring still offers the rest of the room.");
            }
            finally
            {
                Object.Destroy(seat);
            }
        }
    }
}
