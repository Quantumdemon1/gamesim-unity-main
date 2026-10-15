using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class CompetitionSurfacePlayModeTests
    {
        private Button DiceKeepIncludingInactive() => screen.GetComponentsInChildren<Button>(true)
            .Single(button => button.name == "Keep roll");

        private void PaintDiceCueFinish(bool practice)
        {
            if (practice) screen.ShowFinished("Practice complete.", "Return to briefing", () => {}, hideCancel: true);
            else screen.ShowRankedFinish("Your result is ready.");
        }

        [UnityTest]
        public IEnumerator Dice_KeptOutcomeCueWaitsForVisibleFinishAndIsOnceOnly([Values(true, false)] bool practice)
        {
            CreateInput();
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Dice, 7, Widened);
            yield return ShowLive(run, practice);
            int heard = 0, cueFrame = -1; bool visibleAtCue = false;
            string headlineAtCue = null, measureAtCue = null;
            screen.CueRequested += cue =>
            {
                if (cue != HouseAudio.Cue.SocialUp) return;
                heard++; cueFrame = Time.frameCount;
                visibleAtCue = screen.IsDrawn && screen.FinishShowing;
                headlineAtCue = Text("Finish headline").text;
                measureAtCue = Text("Finish measure").text;
            };
            Control("Roll dice").onClick.Invoke(); Land(run);
            int total = run.RollTotal;
            var keep = Control("Keep roll"); keep.onClick.Invoke();
            Assert.That(run.Finished, Is.True);
            Assert.That(run.KeptTotal, Is.EqualTo(total));
            Assert.That(heard, Is.Zero, "An accepted Keep cannot sound before its result plate exists.");
            Assert.That(screen.FinishShowing, Is.False);
            double elapsed = run.Elapsed, performance = run.Performance;
            int rolls = run.RollsUsed, publishFrame = Time.frameCount;
            PaintDiceCueFinish(practice);
            Assert.That(heard, Is.EqualTo(1));
            Assert.That(cueFrame, Is.EqualTo(publishFrame), "The cue and the painted result share one frame.");
            Assert.That(visibleAtCue, Is.True);
            Assert.That(headlineAtCue, Is.EqualTo("KEPT " + total));
            Assert.That(measureAtCue, Is.EqualTo("Total " + total + " of 18  ·  roll " + rolls + " of " + MiniGameRun.MaxRolls));
            keep.onClick.Invoke(); screen.Refresh();
            screen.ShowFinished("Retry saving this result.", "Retry saving this result", () => {});
            screen.ShowRankedFinish("Repainted result.");
            yield return null;
            Assert.That(heard, Is.EqualTo(1), "Duplicate Keep, refresh and retry/repaint do not publish a second outcome.");
            Assert.That(run.Elapsed, Is.EqualTo(elapsed));
            Assert.That(run.Performance, Is.EqualTo(performance));
            Assert.That(run.RollsUsed, Is.EqualTo(rolls));
            Assert.That(run.KeptTotal, Is.EqualTo(total));
        }

        [UnityTest]
        public IEnumerator Dice_HiddenOrReplacedBoardDiscardsItsQueuedOutcome([Values(0, 1, 2)] int replacement)
        {
            CreateInput();
            var oldRun = new MiniGameRun(CompetitionMiniGames.Kind.Dice, 7, Widened);
            yield return ShowLive(oldRun);
            int heard = 0;
            screen.CueRequested += cue => { if (cue == HouseAudio.Cue.SocialUp) heard++; };
            Control("Roll dice").onClick.Invoke(); Land(oldRun); Control("Keep roll").onClick.Invoke();
            Assert.That(heard, Is.Zero);
            if (replacement == 0)
            {
                screen.Hide(); PaintDiceCueFinish(true);
                Assert.That(screen.IsDrawn, Is.False, "A hidden result does not grant a visible-outcome cue.");
            }
            else
            {
                var next = new MiniGameRun(replacement == 1 ? CompetitionMiniGames.Kind.Dice : CompetitionMiniGames.Kind.Memory, 17, Widened);
                yield return ShowLive(next);
                if (replacement == 1) { Control("Roll dice").onClick.Invoke(); Land(next); }
                next.Finish(); PaintDiceCueFinish(true);
                Assert.That(screen.IsDrawn && screen.FinishShowing, Is.True);
            }
            yield return null;
            Assert.That(heard, Is.Zero, "A prior board's accepted Keep cannot leak into a hidden or replacement result.");
        }

        [UnityTest]
        public IEnumerator Dice_RejectedKeepAndAutomaticFinishDoNotQueueKeptOutcome([Values(true, false)] bool thirdRoll)
        {
            CreateInput();
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Dice, 7, Widened);
            yield return ShowLive(run);
            int heard = 0;
            screen.CueRequested += cue => { if (cue == HouseAudio.Cue.SocialUp) heard++; };
            DiceKeepIncludingInactive().onClick.Invoke();
            Assert.That(run.Finished, Is.False, "A stale/inactive Keep has no roll to keep.");
            Control("Roll dice").onClick.Invoke(); DiceKeepIncludingInactive().onClick.Invoke();
            Assert.That(run.Finished, Is.False, "The tumbling roll cannot be kept.");
            Land(run); screen.TogglePause(); DiceKeepIncludingInactive().onClick.Invoke();
            Assert.That(run.Finished, Is.False, "Pause refuses the same command.");
            screen.TogglePause();
            if (thirdRoll)
            {
                Control("Roll dice").onClick.Invoke(); Land(run);
                Control("Roll dice").onClick.Invoke(); Land(run);
                Assert.That(run.RollsUsed, Is.EqualTo(MiniGameRun.MaxRolls));
            }
            else { run.Tick(run.Remaining + .01); screen.Refresh(); }
            Assert.That(run.Finished, Is.True);
            double elapsed = run.Elapsed, performance = run.Performance;
            PaintDiceCueFinish(true); screen.ShowRankedFinish("Repainted automatic finish.");
            yield return null;
            Assert.That(heard, Is.Zero, "Automatic third-roll/timeout results are not an accepted Keep command.");
            Assert.That(run.Elapsed, Is.EqualTo(elapsed));
            Assert.That(run.Performance, Is.EqualTo(performance));
        }

        [UnityTest]
        public IEnumerator Dice_OutcomeSubscriberCanLeaveBoardWithoutOldFinishClobberingIt([Values(true, false)] bool replace)
        {
            CreateInput();
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Dice, 7, Widened);
            yield return ShowLive(run);
            var next = new MiniGameRun(CompetitionMiniGames.Kind.Memory, 17, Widened);
            int heard = 0;
            screen.CueRequested += cue =>
            {
                if (cue != HouseAudio.Cue.SocialUp) return;
                heard++;
                if (replace) screen.Show(next, "Replacement memory board", "Player", true, i => next.Flip(i), () => {}, d => {}, () => {}, () => screen.Hide());
                else screen.Hide();
            };
            Control("Roll dice").onClick.Invoke(); Land(run); Control("Keep roll").onClick.Invoke();
            PaintDiceCueFinish(false);
            yield return null;
            Assert.That(heard, Is.EqualTo(1));
            Assert.That(screen.IsShowing, Is.EqualTo(replace));
            if (replace)
            {
                Assert.That(screen.FinishShowing, Is.False, "No old finish paint follows the synchronous outcome callback.");
                Assert.That(Control("Cancel attempt").interactable, Is.True, "The old ranked finish must not disable the replacement board.");
                Assert.That(screen.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("Memory card ")), Is.EqualTo(16));
                Assert.That(next.Finished, Is.False);
                Assert.That(next.Elapsed, Is.Zero);
            }
            else Assert.That(screen.IsDrawn, Is.False, "Nothing after the outcome callback may undo Hide.");
        }

        [UnityTest]
        public IEnumerator Dice_DisabledCanvasOrBoardAncestorDiscardsOutcome([Values(true, false)] bool canvas)
        {
            CreateInput();
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Dice, 7, Widened);
            yield return ShowLive(run);
            int heard = 0;
            screen.CueRequested += cue => { if (cue == HouseAudio.Cue.SocialUp) heard++; };
            Control("Roll dice").onClick.Invoke(); Land(run); Control("Keep roll").onClick.Invoke();
            var plateParent = screen.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Finish plate").parent.gameObject;
            if (canvas) screen.GetComponent<Canvas>().enabled = false;
            else plateParent.SetActive(false);
            PaintDiceCueFinish(true);
            Assert.That(heard, Is.Zero, "An undrawn outcome cannot sound.");
            if (canvas) screen.GetComponent<Canvas>().enabled = true;
            else plateParent.SetActive(true);
            PaintDiceCueFinish(true);
            yield return null;
            Assert.That(heard, Is.Zero, "A later repaint cannot recover a discarded cue.");
        }
    }
}
