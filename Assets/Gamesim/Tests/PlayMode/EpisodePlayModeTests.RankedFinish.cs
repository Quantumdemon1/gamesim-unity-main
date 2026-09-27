using System.Collections;
using System.Linq;
using System.Reflection;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The player's reduced-motion setting reaches the competition screen.</summary>
        [UnityTest]
        public IEnumerator ReducedMotion_TheDirectorHandsItToTheCompetitionScreen()
        {
            typeof(Gamesim.Episode.EpisodeDirector).GetField("reducedMotion", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(director, true);
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Practice this competition").onClick.Invoke();
            yield return null;
            var screen = SceneComponents<CompetitionGameScreen>().Single();
            Assert.That(screen.IsShowing, Is.True);
            Assert.That(screen.ReducedMotion, Is.True);
        }

        /// <summary>
        /// A ranked attempt ends on its own plate before it commits: the player reads what they did
        /// before the standings replace it, nothing can leave the plate while it stands, and the
        /// commit happens exactly once - after which the standings say the player's attempt.
        /// </summary>
        [UnityTest]
        public IEnumerator RankedFinish_ThePlateHoldsBeforeTheCommitAndTheStandingsSayTheAttempt()
        {
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            var before = director.Snapshot;
            var kind = CompetitionMiniGames.For(EpisodeEngine.CompetitionCategory(before));
            ButtonWithCaption(CompetitionMiniGames.EnterCaption(kind)).onClick.Invoke();
            yield return null;
            var screen = SceneComponents<CompetitionGameScreen>().Single();
            if (screen.IsAssembling)
                screen.GetComponentsInChildren<Button>().First(button => button.name == "Continue to competition").onClick.Invoke();
            // Past the count: the arena may still be seating the audience, which only holds the start.
            for (int frame = 0; frame < 120 && !screen.IsPlaying; frame++) { screen.AdvanceReady(.2f); yield return null; }
            Assert.That(screen.IsPlaying, Is.True, "The ranked attempt is under way.");

            var run = (MiniGameRun)typeof(Gamesim.Episode.EpisodeDirector).GetField("challengeRun", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(director);
            run.Finish();
            yield return null; yield return null;
            float plateUp = Time.realtimeSinceStartup;
            Assert.That(screen.IsShowing, Is.True, "A ranked finish does not vanish on the frame it ends.");
            Assert.That(screen.FinishShowing, Is.True, "Its plate is up.");
            Assert.That(screen.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Finish performance").text, Does.StartWith("Performance "));
            var cancel = screen.GetComponentsInChildren<Button>().Single(button => button.name == "Cancel attempt");
            Assert.That(cancel.interactable, Is.False, "Nothing leaves a finished ranked board.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision), "and it has not committed yet.");
            yield return PressKey(Key.Escape);
            Assert.That(screen.IsShowing, Is.True, "Escape does not throw a finished ranked attempt away.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision));

            float deadline = Time.realtimeSinceStartup + 5f;
            while (director.Snapshot.revision == before.revision && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Time.realtimeSinceStartup - plateUp, Is.GreaterThan(.6f), "The plate stood long enough to be read.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1), "The result committed exactly once.");
            yield return null; yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1));
            Assert.That(screen.IsShowing, Is.False);
            var card = SceneComponents<CompetitionResult>().Single();
            Assert.That(card.IsPlaying, Is.True);
            Assert.That(card.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Player attempt").text,
                Does.StartWith("Your attempt").And.Contain("performance"), "The standings say the player's own attempt.");
        }
    }
}
