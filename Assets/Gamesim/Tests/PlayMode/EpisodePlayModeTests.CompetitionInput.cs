using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator CompetitionResultOpenedByDirectCommandOwnsWorldInputAndRestoresTheHouse()
        {
            Assert.That(director.IsPanelOpen, Is.False);
            Assert.That(director.Submit(NextCommand(director.Snapshot)).accepted, Is.True);
            var before = director.Snapshot;
            Assert.That(director.Submit(NextCommand(before)).accepted, Is.True);
            Assert.That(SceneComponents<CompetitionResult>().Single().IsPlaying, Is.True);
            Assert.That(player.InputEnabled, Is.False, "Result ownership must not depend on entering through a phase panel.");
            Assert.That(cameraRig.ControlsEnabled, Is.False);
            yield return ContinueCompetitionResults(byKeyboard: false);
            Assert.That(director.IsPanelOpen, Is.False);
            Assert.That(player.InputEnabled, Is.True);
            Assert.That(cameraRig.ControlsEnabled, Is.True);
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1));
        }

        [UnityTest]
        public IEnumerator CompetitionMenuKeysAffectOnlyTheAttemptAndPreserveTheBriefing()
        {
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            var before = director.Snapshot;
            ButtonWithCaption("Practice this competition").onClick.Invoke();
            yield return null;
            var screen = SceneComponents<CompetitionGameScreen>().Single();
            Assert.That(screen.IsShowing, Is.True);
            yield return PressKey(Key.Escape);
            Assert.That(screen.IsShowing, Is.False);
            Assert.That(director.IsPanelOpen, Is.True, "Escape returns to briefing without closing the phase panel underneath.");
            Assert.That(player.InputEnabled, Is.False);
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision));

            ButtonWithCaption("Practice this competition").onClick.Invoke();
            yield return null;
            var pad = TestGamepad();
            yield return PressPad(pad, GamepadButton.Start);
            Assert.That(screen.Paused, Is.True, "Start pauses the attempt instead of cancelling it.");
            Assert.That(screen.IsShowing, Is.True);
            yield return PressPad(pad, GamepadButton.East);
            Assert.That(screen.IsShowing, Is.False);
            Assert.That(director.IsPanelOpen, Is.True);
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision));
            Assert.That(director.Snapshot.randomState, Is.EqualTo(before.randomState));
        }
    }
}
