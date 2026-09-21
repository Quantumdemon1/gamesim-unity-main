using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class CompetitionSurfacePlayModeTests
    {
        [UnityTest]
        public IEnumerator PreviewStartsAfterStagingThenHidesCardsBeforeTimedInput()
        {
            owner = new GameObject("Memory preview test"); screen = CompetitionGameScreen.Attach(owner);
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Memory, 17, 3, CompetitionDefinitions.FirstImpressions);
            screen.Show(run, run.Definition.Title, "Player", true, i => run.Flip(i), () => {}, d => {}, () => {}, () => {});
            yield return null;
            screen.HoldReady("Walking to arena");
            var labels = screen.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Memory card "))
                .Select(button => button.GetComponentInChildren<TMP_Text>()).ToArray();
            Assert.That(labels.Count(label => label.text.Contains(":")), Is.Zero, "Staging cannot provide an unlimited preview.");
            screen.AdvanceReady(.01f);
            Assert.That(labels.Count(label => label.text.Contains(":")), Is.EqualTo(16));
            Assert.That(screen.IsPlaying, Is.False); Assert.That(run.Elapsed, Is.Zero);
            Assert.That(labels.All(label => !label.GetComponentInParent<Button>().IsInteractable()), Is.True);
            screen.TogglePause(); screen.AdvanceReady(5);
            Assert.That(run.Elapsed, Is.Zero); Assert.That(screen.IsPlaying, Is.False);
            screen.TogglePause();
            for (int frame = 0; frame < 16; frame++) screen.AdvanceReady(.2f);
            Assert.That(screen.IsPlaying, Is.True);
            Assert.That(labels.Count(label => label.text.Contains(":")), Is.Zero, "All unmatched cards hide before input begins.");
            Assert.That(run.Elapsed, Is.Zero, "Preview and countdown never spend the 24 second playing clock.");
            Assert.That(labels.All(label => label.GetComponentInParent<Button>().IsInteractable()), Is.True);
        }

        [UnityTest]
        public IEnumerator VariantRulesAndLiveCuesNameTheActualWindowAndPressure()
        {
            owner = new GameObject("Variant cue test"); screen = CompetitionGameScreen.Attach(owner);
            var reaction = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 17, 3, CompetitionDefinitions.SwitchbackSignals);
            screen.Show(reaction, reaction.Definition.Title, "Player", true, i => {}, () => {}, d => {}, () => {}, () => {});
            yield return null; screen.AdvanceReady(4); reaction.Tick(.5); screen.Refresh();
            Assert.That(screen.GetComponentsInChildren<TMP_Text>().Single(label => label.name == "Rules").text, Does.Contain("0.65 and 1.15"));
            Assert.That(screen.GetComponentsInChildren<Button>().Single(button => button.name == "Reaction target")
                .GetComponentInChildren<TMP_Text>().text, Does.Contain("0.65 s"));
            var endurance = new MiniGameRun(CompetitionMiniGames.Kind.Endurance, 17, 3, CompetitionDefinitions.PressureCooker);
            screen.Show(endurance, endurance.Definition.Title, "Player", true, i => {}, () => {}, d => {}, () => {}, () => {});
            yield return null; screen.AdvanceReady(4); endurance.Tick(4); screen.Refresh();
            var grip = screen.GetComponentsInChildren<TMP_Text>().Single(label => label.name == "Grip value");
            Assert.That(grip.text, Does.Contain("Wave in 0.5s"));
            endurance.Tick(1); screen.Refresh(); Assert.That(grip.text, Does.Contain("WAVE"));
        }
    }
}
