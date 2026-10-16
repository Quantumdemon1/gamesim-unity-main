using System.Collections;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The story's scene stage (plan §5.1): the places it offers a scene's people are clear of
    /// furniture, even furniture cloned at run time, which the baked NavMesh has no hole under; and
    /// a beat's one try at staging waits for a house that could take it.
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

        /// <summary>What the house's world says about staging a scene now, for a failure's message.</summary>
        private string SceneStageDiagnostic()
        {
            var house = StormCoordinator();
            if (house == null) return "no house world";
            return "house ready=" + house.IsReady + ", paused=" + house.IsPaused + ", meetings=" + house.LeaseCount
                + ", could take a scene=" + house.CanBeginSceneStage + ", scene=" + (house.SceneStageKey ?? "none")
                + " (" + house.SceneStageCount + " held), panel open=" + director.IsPanelOpen;
        }

        /// <summary>
        /// Waits in real seconds - the pinned frame time does not move the clock a batchmode frame
        /// takes - for the house to stage the scene for <paramref name="key"/>, and says how many
        /// frames the house could have taken it if it never did.
        /// </summary>
        private IEnumerator WaitForSceneStage(string key, float seconds, string what)
        {
            int couldTake = 0;
            float until = Time.realtimeSinceStartup + seconds;
            while (StormCoordinator()?.SceneStageKey != key && Time.realtimeSinceStartup < until)
            {
                var house = StormCoordinator();
                if (house != null && house.CanBeginSceneStage && !director.IsPanelOpen) couldTake++;
                yield return null;
            }
            bool staged = StormCoordinator()?.SceneStageKey == key;
            Assert.That(staged, Is.True, staged ? null
                : what + ": never staged, though the house could have taken it on " + couldTake + " frames; " + SceneStageDiagnostic());
        }

        /// <summary>
        /// A story moment already open as a season loads is staged once the house could take it.
        /// The beat's one try waits for a house whose people are bound, which is not paused and
        /// holds no meeting: spent on the first frame the director was ready, while the house was
        /// still binding its people, it was refused, and the scene was never staged (measured
        /// 2026-10-15). It is still one try: a scene let go is not staged again while its moment
        /// stays open.
        /// </summary>
        [UnityTest]
        public IEnumerator SceneStage_AMomentOpenAsTheSeasonLoadsIsStagedOnceTheHouseCanTakeIt()
        {
            Application.runInBackground = true;
            Time.captureDeltaTime = 1f / 30f;
            try
            {
                yield return InstallPeaceFixture(suspend: false);
                director.BuildNpcWorldForDiagnostics();
                Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
                Assert.That(EpisodeEngine.OpenStoryBeats(director.Snapshot).Any(item => item.id == peaceEventId), Is.True,
                    "The moment is open as the season loads.");
                yield return WaitForSceneStage(peaceEventId, 30f, "The moment open as the season loaded is staged once the house can take it");
                var house = StormCoordinator();
                Assert.That(house.SceneHoldsActor(peaceFirstId) && house.SceneHoldsActor(peaceSecondId), Is.True,
                    "Its two people are the scene's: " + SceneStageDiagnostic());

                // Once per beat: let go, it is not staged again while the house could take it.
                house.EndSceneStage();
                int free = 0;
                float until = Time.realtimeSinceStartup + 20f;
                while (free < 10 && Time.realtimeSinceStartup < until)
                {
                    yield return null;
                    Assert.That(StormCoordinator().HasSceneStage, Is.False, "Staged again once let go: " + SceneStageDiagnostic());
                    if (StormCoordinator().CanBeginSceneStage && !director.IsPanelOpen) free++;
                }
                Assert.That(free, Is.EqualTo(10), "The house could have taken the scene again: " + SceneStageDiagnostic());
            }
            finally { Time.captureDeltaTime = 0f; }
        }

        /// <summary>
        /// A moment that opens while a panel is up is staged once the panel has gone and the house
        /// has resumed. The frame a panel closes the house is still paused - its world ticks after
        /// the stage does - and a try spent then was refused, so the scene was never staged.
        /// </summary>
        [UnityTest]
        public IEnumerator SceneStage_AMomentThatOpensUnderAPanelIsStagedOnceTheHouseResumes()
        {
            Application.runInBackground = true;
            Time.captureDeltaTime = 1f / 30f;
            try
            {
                yield return InstallPeaceFixture(suspend: false);
                director.BuildNpcWorldForDiagnostics();
                Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
                // The season's own try at the moment first (the load case's business, whatever came
                // of it), and whatever it staged let go, so nothing stages under the arrangement.
                var oneTry = typeof(EpisodeDirector).GetField("sceneStagedBeat", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(oneTry, Is.Not.Null, "The director keeps a beat's one try.");
                yield return WaitFor(() => (string)oneTry.GetValue(director) == peaceEventId, 30f, "The season's own try at the moment is made.");
                StormCoordinator().EndSceneStage();

                director.OpenJournal();
                yield return WaitFor(() => director.IsPanelOpen && StormCoordinator().IsPaused, 5f, "The house pauses under the notebook.");
                // The moment opens under the panel: its one try not yet spent, as a beat that has just
                // opened has not spent its own.
                oneTry.SetValue(director, null);
                for (int frame = 0; frame < 10; frame++)
                {
                    yield return null;
                    Assert.That(StormCoordinator().HasSceneStage, Is.False, "Nothing is staged under a panel: " + SceneStageDiagnostic());
                }

                director.ClosePanels();
                yield return WaitForSceneStage(peaceEventId, 30f, "Staged once the panel has gone and the house resumed");
                Assert.That(StormCoordinator().SceneHoldsActor(peaceFirstId) && StormCoordinator().SceneHoldsActor(peaceSecondId), Is.True,
                    "Its two people are the scene's: " + SceneStageDiagnostic());
            }
            finally { Time.captureDeltaTime = 0f; }
        }
    }
}
