using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Uma.Tests
{
    /// <summary>
    /// Measures what a full house of UMA bodies costs against the same house on the authored
    /// prefabs, so the decision to put UMA in the shipping episode can be made on a number.
    ///
    /// Absolute milliseconds from a batchmode run mean little — there is no window to present to, so
    /// everything looks faster than it will in a player. The **ratio** between the two bodies
    /// measured back to back under identical conditions is the useful figure, which is why the
    /// acceptance matrix states C5 as a multiple rather than a millisecond budget.
    ///
    /// The assertion is a deliberately loose smoke guard. This is a measurement that reports, not a
    /// performance gate that fails the build on hardware variance.
    /// </summary>
    public sealed class UmaCastCostPlayModeTests
    {
        private const int HouseSize = 6;
        private const int SettleFrames = 120;
        private const int SampleFrames = 240;
        private const int BuildTimeoutFrames = 2400;

        private GameObject cast;
        private readonly List<GameObject> spawned = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var actor in spawned) if (actor != null) Object.Destroy(actor);
            spawned.Clear();
            if (cast != null) Object.Destroy(cast);
            yield return null;
            Assert.That(CharacterBodySource.Provider, Is.Null);
        }

        [UnityTest]
        public IEnumerator SixHouseguests_UmaFrameCostMeasuredAgainstTheFallback()
        {
            var house = ContentCatalog.Create(1).contestants.Take(HouseSize).ToArray();
            Assert.That(house, Has.Length.EqualTo(HouseSize));

            // Phase one: the bodies the episode ships with today. No provider is registered.
            Assert.That(CharacterBodySource.Provider, Is.Null);
            SpawnHouse(house);
            for (int i = 0; i < SettleFrames; i++) yield return null;
            double fallbackMs = 0;
            yield return SampleMedian(result => fallbackMs = result);
            DespawnHouse();
            yield return null;

            // Phase two: the same house, on UMA.
            cast = new GameObject("Gamesim UMA cast", typeof(GamesimUmaCast));
            yield return null;
            Assert.That(CharacterBodySource.Provider, Is.Not.Null);
            SpawnHouse(house);

            int built = 0;
            for (int frame = 0; frame < BuildTimeoutFrames && built < HouseSize; frame++)
            {
                yield return null;
                built = spawned.Count(actor => actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any());
            }
            Assert.That(built, Is.EqualTo(HouseSize), "Only " + built + " of " + HouseSize + " UMA bodies finished building.");

            for (int i = 0; i < SettleFrames; i++) yield return null;
            double umaMs = 0;
            yield return SampleMedian(result => umaMs = result);
            DespawnHouse();

            double ratio = fallbackMs > 0.0001 ? umaMs / fallbackMs : 0;
            Debug.Log(string.Format(
                "[Gamesim.Uma] C5 frame cost, {0} houseguests, batchmode (no present): " +
                "fallback median {1:F3} ms, UMA median {2:F3} ms, ratio {3:F2}x",
                HouseSize, fallbackMs, umaMs, ratio));

            Assert.That(umaMs, Is.LessThan(fallbackMs * 12.0 + 1.0),
                "A full UMA house costing more than an order of magnitude over the fallback would " +
                "change the shipping decision, not just the frame budget.");
        }

        private void SpawnHouse(IEnumerable<ContestantState> house)
        {
            int index = 0;
            foreach (var contestant in house)
            {
                var actor = new GameObject("Cost houseguest " + index);
                // Spread them out so nothing is trivially culled into a single draw.
                actor.transform.position = new Vector3(index * 1.5f, 0f, 0f);
                spawned.Add(actor);
                CharacterPresentation.Attach(actor, contestant, Color.HSVToRGB(index / (float)HouseSize, 0.6f, 0.9f));
                index++;
            }
        }

        private void DespawnHouse()
        {
            foreach (var actor in spawned) if (actor != null) Object.Destroy(actor);
            spawned.Clear();
        }

        /// <summary>Median rather than mean, so one hitch does not define the result.</summary>
        private static IEnumerator SampleMedian(System.Action<double> report)
        {
            var samples = new List<double>(SampleFrames);
            for (int i = 0; i < SampleFrames; i++)
            {
                yield return null;
                samples.Add(Time.unscaledDeltaTime * 1000.0);
            }
            samples.Sort();
            report(samples[samples.Count / 2]);
        }
    }
}
