using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// Every authored competition mid-play, over the arena, for review: the frames the game
        /// screen is judged by. A practice attempt carries the arena and the camera; each definition
        /// is then shown on a screen of the test's own with a run the test drives itself, so every
        /// frame is the state the test put there.
        /// </summary>
        [UnityTest]
        public IEnumerator MiniGames_CaptureEveryDefinitionMidPlayForReview()
        {
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Practice this competition").onClick.Invoke();
            yield return null;
            var screen = SceneComponents<CompetitionGameScreen>().Single();
            if (screen.IsAssembling)
                screen.GetComponentsInChildren<Button>().First(button => button.name == "Continue to competition").onClick.Invoke();
            // Let the director's own practice reach play first: until the arena is ready the director
            // holds whatever the screen shows, and the frames would be of a start that never comes.
            float ready = Time.realtimeSinceStartup + 35f;
            while (!screen.IsPlaying && Time.realtimeSinceStartup < ready)
            {
                if (screen.Paused) screen.TogglePause();
                yield return null;
            }
            Assert.That(screen.IsPlaying, Is.True, "The director's practice reached play.");
            string field = string.Join("\n", EpisodeEngine.CompetitionPlayers(director.Snapshot)
                .Select(actor => HudPrimitives.WithYou(actor.name, actor.isPlayer)));

            // The frames are taken on a screen of the test's own: the director drives its screen every
            // frame - holding it while the arena settles, pausing it after a slow frame, ticking its
            // own run - and a capture's frame is slow. Its practice stays up behind, holding the
            // arena and the camera; its own screen stands aside.
            screen.Hide();
            var probeOwner = new GameObject("Review capture screen");
            var probe = CompetitionGameScreen.Attach(probeOwner);
            try
            {
                MiniGameRun lastRun = null;
                foreach (var definition in CompetitionDefinitions.All)
                {
                    var kind = CompetitionMiniGames.For(definition.Category);
                    var run = new MiniGameRun(kind, 77, CompetitionMiniGames.CurrentRules, definition);
                    lastRun = run;
                    probe.Show(run, "Head of Household · " + definition.Title, field, true, i => run.Flip(i), () => run.Tap(), d => run.Tap(d),
                        () => run.SetHolding(!run.Holding), () => { });
                    yield return null;
                    if (definition == CompetitionDefinitions.All[0] && Application.isBatchMode) yield return CaptureFraming("minigame-count", false);
                    probe.AdvanceReady(4f);
                    if (definition.Pattern == CompetitionPattern.PreviewPairs)
                    {
                        probe.Refresh();
                        if (Application.isBatchMode) yield return CaptureFraming("minigame-" + definition.Id + "-preview", false);
                    }
                    // Small steps: a step over a quarter second reads as a stalled frame and pauses.
                    for (int step = 0; step < 100 && !probe.IsPlaying; step++) { probe.AdvanceReady(.2f); yield return null; }
                    Assert.That(probe.IsPlaying, Is.True, definition.Id + " is in play.");
                    switch (kind)
                    {
                        case CompetitionMiniGames.Kind.Reaction:
                            for (int hit = 0; hit < 4; hit++)
                            {
                                for (int step = 0; step < 100 && !run.TargetLive; step++) run.Tick(.05);
                                if (hit < 3) run.Tap(run.TargetDirection);
                            }
                            break;
                        case CompetitionMiniGames.Kind.Memory:
                            int first = 0, pair = Enumerable.Range(1, 15).First(i => run.Faces[i] == run.Faces[0]);
                            run.Flip(first); run.Flip(pair);
                            run.Tick(1.5);
                            int other = Enumerable.Range(1, 15).First(i => i != pair && run.Faces[i] != run.Faces[0]);
                            int wrong = Enumerable.Range(1, 15).First(i => i != pair && i != other && run.Faces[i] != run.Faces[other] && run.Faces[i] != run.Faces[0]);
                            run.Flip(other); run.Flip(wrong);
                            break;
                        case CompetitionMiniGames.Kind.Endurance:
                            run.SetHolding(true);
                            for (int step = 0; step < 50; step++) run.Tick(.1);
                            break;
                    }
                    probe.Refresh();
                    // Past GO, which stands over the board for its first half second.
                    float clear = Time.realtimeSinceStartup + .7f;
                    while (Time.realtimeSinceStartup < clear) yield return null;
                    probe.Refresh();
                    if (Application.isBatchMode) yield return CaptureFraming("minigame-" + definition.Id, false);
                }

                // The pause, and the end of the last board as a practice ends: the grip giving out.
                probe.TogglePause();
                if (Application.isBatchMode) yield return CaptureFraming("minigame-paused", false);
                probe.TogglePause();
                lastRun.SetHolding(true);
                while (!lastRun.Finished) lastRun.Tick(.1);
                probe.Refresh();
                probe.ShowFinished("Practice complete. No competition result or season state was changed.", "Return to briefing", () => { }, hideCancel: true);
                if (Application.isBatchMode) yield return CaptureFraming("minigame-finished", false);
            }
            finally { Object.Destroy(probe.gameObject); Object.Destroy(probeOwner); }
        }

        /// <summary>The standings card a committed competition opens on, for review.</summary>
        [UnityTest]
        public IEnumerator MiniGames_CaptureTheResultCardForReview()
        {
            Assert.That(director.Submit(NextCommand(director.Snapshot)).accepted, Is.True);
            Assert.That(director.Submit(NextCommand(director.Snapshot)).accepted, Is.True);
            Assert.That(SceneComponents<CompetitionResult>().Single().IsPlaying, Is.True);
            float settle = Time.realtimeSinceStartup + 2.5f;
            while (Time.realtimeSinceStartup < settle) yield return null;
            if (Application.isBatchMode) yield return CaptureFraming("competition-result");
        }
    }
}
