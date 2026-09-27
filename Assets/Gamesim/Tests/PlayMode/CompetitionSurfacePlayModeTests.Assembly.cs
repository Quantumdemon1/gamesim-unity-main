using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class CompetitionSurfacePlayModeTests
    {
        [UnityTest]
        public IEnumerator AssemblyIsVisiblePauseableAndSkippableWithoutConsumingAttemptTime()
        {
            CreateInput();screen.FontScale=1.2f;
            var run=new MiniGameRun(CompetitionMiniGames.Kind.Reaction,77,3);int cancelled=0;
            screen.Show(run,"Head of Household","Player\nMaya",false,i=>{},()=>{},d=>{},()=>{},()=>{cancelled++;screen.Hide();},assemble:true);
            yield return null;
            Assert.That(screen.IsAssembling,Is.True);
            Assert.That(screen.GetComponentsInChildren<RectTransform>(true).Single(r=>r.name=="Competition studio").gameObject.activeSelf,Is.False);
            Assert.That(screen.GetComponentsInChildren<Button>().Length,Is.EqualTo(3));
            Assert.That(screen.GetComponent<CanvasGroup>().blocksRaycasts,Is.True,"Showing the house must never expose house input.");
            screen.AdvanceAssembly(100,false);Assert.That(screen.IsAssembling,Is.True,"Real arrivals gate the automatic transition.");
            Assert.That(screen.AdvanceReady(100),Is.False);Assert.That(run.Elapsed,Is.Zero);
            yield return KeyPress(Key.Tab);AssertSelected("Pause assembly");
            yield return KeyPress(Key.Enter);Assert.That(screen.Paused,Is.True);
            screen.AdvanceAssembly(100,true);Assert.That(screen.IsAssembling,Is.True,"A paused assembly cannot advance behind the pause button.");
            yield return PadPress(GamepadButton.Start);Assert.That(screen.Paused,Is.False);
            yield return PadPress(GamepadButton.LeftShoulder);AssertSelected("Continue to competition");
            yield return PadPress(GamepadButton.South);
            Assert.That(screen.IsAssembling,Is.False);Assert.That(screen.IsPlaying,Is.False);Assert.That(run.Elapsed,Is.Zero);
            AssertSelected("Pause competition");
            Assert.That(screen.GetComponentsInChildren<Button>().All(b=>b.name!="Pause assembly"),Is.True,"Hidden assembly controls leave the input scope.");
            screen.HoldReady("Your station is still being approached");Assert.That(run.Elapsed,Is.Zero);
            yield return KeyPress(Key.Escape);Assert.That(cancelled,Is.EqualTo(1));Assert.That(screen.IsShowing,Is.False);
        }

        [UnityTest]
        public IEnumerator AssemblyAutomaticArrivalAndReducedMotionHaveTheSameReadyCountdown()
        {
            CreateInput();var run=new MiniGameRun(CompetitionMiniGames.Kind.Memory,77,3);
            screen.Show(run,"Veto","Player",true,i=>{},()=>{},d=>{},()=>{},()=>screen.Hide(),assemble:true);
            yield return null;screen.AdvanceAssembly(.5f,true);Assert.That(screen.IsAssembling,Is.True);
            screen.AdvanceAssembly(.7f,true);Assert.That(screen.IsAssembling,Is.False);
            Assert.That(screen.AdvanceReady(4),Is.False);Assert.That(screen.IsPlaying,Is.False,"Transition-frame input cannot start the game.");
            yield return null;screen.AdvanceReady(2);Assert.That(screen.IsPlaying,Is.False);
            screen.AdvanceReady(1);Assert.That(screen.IsPlaying,Is.True);Assert.That(run.Elapsed,Is.Zero);
            screen.Hide();
            screen.Show(new MiniGameRun(CompetitionMiniGames.Kind.Memory,77,3),"Veto","Player",true,i=>{},()=>{},d=>{},()=>{},()=>screen.Hide(),assemble:false);
            Assert.That(screen.IsAssembling,Is.False);Assert.That(screen.IsPlaying,Is.False);
            yield return null;screen.AdvanceReady(2);Assert.That(screen.IsPlaying,Is.False);
            screen.AdvanceReady(1);Assert.That(screen.IsPlaying,Is.True);
        }

        [UnityTest]
        public IEnumerator ScoreDetailsUseRealKeyboardAndControllerInputWithoutDismissingTheResult()
        {
            CreateInput();screen.Hide();var result=CompetitionResult.Attach(owner);result.FontScale=1.2f;
            try
            {
                string explanation="Played: performance 62%. Your weighted stats: physical 1.2; mental 2.4; endurance 0.6; social 0; luck 0.9. "
                    +"Chance +0.27 (sampled multiplier 1.053); nominee +2.5; preparation +3; event +1; storyline -1; performance +1.24; rounding 0. "
                    +"Sum = 12.11. All displayed terms add to this committed score. Total player bonus +4.24. Zero performance can still win; full marks do not guarantee first place.";
                result.Play("Head of Household","Mental",1,new[]{new CompetitionResult.Standing("You",12.11,true,true,null)},true,explanation);
                yield return new WaitForSecondsRealtime(.3f);
                yield return KeyPress(Key.LeftArrow);AssertSelected("Review competition score details");
                yield return KeyPress(Key.Enter);
                Assert.That(result.IsPlaying,Is.True);Assert.That(result.OwnsInput,Is.True);
                var full=result.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="Full performance explanation");
                Assert.That(full.text,Is.EqualTo(explanation));Canvas.ForceUpdateCanvases();full.ForceMeshUpdate();
                Assert.That(full.isTextOverflowing,Is.False,"The full numerical explanation must fit at large text size.");
                yield return PadPress(GamepadButton.East);Assert.That(result.IsPlaying,Is.True);
                Assert.That(result.GetComponentsInChildren<TMP_Text>().Any(t=>t.name=="Full performance explanation"),Is.False);
                yield return PadPress(GamepadButton.South);Assert.That(result.IsPlaying,Is.True);
                Assert.That(result.GetComponentsInChildren<TMP_Text>().Any(t=>t.name=="Full performance explanation"),Is.True);
                yield return PadPress(GamepadButton.DpadRight);AssertSelected("Continue from competition results");
                yield return PadPress(GamepadButton.South);Assert.That(result.IsPlaying,Is.False);
            }
            finally{Object.Destroy(result.gameObject);}
        }
    }
}