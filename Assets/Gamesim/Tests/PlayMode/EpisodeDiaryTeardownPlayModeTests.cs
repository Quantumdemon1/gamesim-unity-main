using System.Collections;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator DiarySeat_DisableCleansUpThenLiveDirectorCancelsTheUnconfirmedVisit()
        {
            yield return OpenDiaryFixturePanel();
            ButtonWithCaption(EpisodeHud.StudyMemorizeCaption).onClick.Invoke();
            Assert.That(director.HasDiaryDecisionDraft, Is.True);
            var before = director.Snapshot;
            var seat = player.GetComponent<DiarySeatPose>();
            Assert.That(seat.Active, Is.True);

            seat.enabled = false;
            Assert.That(seat.Active, Is.False, "Disabling immediately restores the temporary seated presentation.");
            Assert.That(director.IsDiaryOpen, Is.True,
                "OnDisable must not synchronously call gameplay/UI code during possible scene teardown.");
            yield return null;
            Assert.That(director.IsDiaryOpen, Is.False, "A live director closes access on its next frame.");
            Assert.That(director.HasDiaryDecisionDraft, Is.False);
            Assert.That(player.InputEnabled, Is.True);
            Assert.That(cameraRig.ControlsEnabled, Is.True);
            AssertEquivalent(before, director.Snapshot);
            seat.enabled = true;
        }

        [UnityTest]
        public IEnumerator DiarySeat_ReloadWithAnOpenReviewDoesNotSpawnHudGhostsDuringTeardown()
        {
            yield return OpenDiaryFixturePanel();
            director.SaveNow();
            var saved = director.Snapshot;
            var hud = director.GetComponent<EpisodeHud>();
            hud.ReducedMotion = false; // Exercise the real fade path, never disable it to hide a leak.
            ButtonWithCaption(EpisodeHud.StudyMemorizeCaption).onClick.Invoke();
            Assert.That(director.HasDiaryDecisionDraft, Is.True);
            Assert.That(player.GetComponent<DiarySeatPose>().Active, Is.True);
            var oldDirector = director;

            // Intentionally do not close the panel or wait for a fade before unloading.
            yield return ReloadEpisode();
            Assert.That(oldDirector == null, Is.True);
            Assert.That(director.IsDiaryOpen, Is.False);
            Assert.That(director.HasDiaryDecisionDraft, Is.False);
            Assert.That(Object.FindObjectsByType<HudFade>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty,
                "No ghost created by the old diary can survive the scene it belonged to.");
            AssertEquivalent(saved, director.Snapshot);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
