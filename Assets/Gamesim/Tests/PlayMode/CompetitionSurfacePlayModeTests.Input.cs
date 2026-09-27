using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class CompetitionSurfacePlayModeTests
    {
        private GameObject inputRoot;
        private EventSystem previousEvents;
        private Keyboard keyboard;
        private Gamepad pad;
        private Mouse pointer;

        private void CreateInput()
        {
            previousEvents = EventSystem.current;
            if (previousEvents != null) previousEvents.enabled = false;
            keyboard = InputSystem.AddDevice<Keyboard>(); pad = InputSystem.AddDevice<Gamepad>(); pointer = InputSystem.AddDevice<Mouse>();
            inputRoot = new GameObject("Competition input test", typeof(EventSystem), typeof(InputSystemUIInputModule));
            inputRoot.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            owner = new GameObject("Competition input owner"); screen = CompetitionGameScreen.Attach(owner);
        }

        private void CleanupInput()
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            if (pointer != null && pointer.added) InputSystem.RemoveDevice(pointer);
            if (inputRoot != null) { inputRoot.SetActive(false); Object.Destroy(inputRoot); }
            if (previousEvents != null) previousEvents.enabled = true;
            keyboard = null; pad = null; pointer = null; inputRoot = null; previousEvents = null;
        }

        [UnityTest]
        public IEnumerator ReactionAndEndurance_KeyboardAndControllerCanReachPauseResumeAndBack()
        {
            CreateInput();
            foreach (var kind in new[] { CompetitionMiniGames.Kind.Reaction, CompetitionMiniGames.Kind.Endurance })
            {
                var run = new MiniGameRun(kind, 77, 3); int cancelled = 0;
                screen.Show(run, "Competition", "Player", true, i => {}, () => {}, d => {}, () => run.SetHolding(!run.Holding),
                    () => { cancelled++; screen.Hide(); });
                yield return null; screen.AdvanceReady(4); yield return null;
                string game = kind == CompetitionMiniGames.Kind.Reaction ? "Game surface" : "Toggle grip";
                AssertSelected(game);
                yield return KeyPress(Key.Tab); AssertSelected("Pause competition");
                yield return KeyPress(Key.Enter); Assert.That(screen.Paused, Is.True); AssertSelected("Pause competition");
                yield return KeyPress(Key.Enter); Assert.That(screen.IsPlaying, Is.True); AssertSelected(game);
                yield return KeyPress(Key.Tab, true); AssertSelected("Cancel attempt");
                yield return KeyPress(Key.Enter); Assert.That(cancelled, Is.EqualTo(1)); Assert.That(screen.IsShowing, Is.False);

                screen.Show(run, "Competition", "Player", true, i => {}, () => {}, d => {}, () => run.SetHolding(!run.Holding),
                    () => { cancelled++; screen.Hide(); });
                yield return null; screen.AdvanceReady(4); yield return null; AssertSelected(game);
                yield return PadPress(GamepadButton.RightShoulder); AssertSelected("Pause competition");
                yield return PadPress(GamepadButton.South); Assert.That(screen.Paused, Is.True);
                yield return PadPress(GamepadButton.Start); Assert.That(screen.IsPlaying, Is.True); AssertSelected(game);
                yield return PadPress(GamepadButton.LeftShoulder); AssertSelected("Cancel attempt");
                yield return PadPress(GamepadButton.South); Assert.That(cancelled, Is.EqualTo(2)); Assert.That(screen.IsShowing, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator HeldDirectionCountsOnceAndPointerErrorsUseTheRealRaycastSurface()
        {
            CreateInput(); var run = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 77, 3);
            screen.Show(run, "Competition", "Player", true, i => {}, () => run.Tap(run.TargetDirection),
                direction => run.Tap(direction), () => {}, () => screen.Hide(), run.MissPointer);
            yield return null; screen.AdvanceReady(4); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow));
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(run.FalseStarts, Is.EqualTo(1), "UI navigation repeats must never count as fresh attempts.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            yield return KeyPress(Key.UpArrow); Assert.That(run.FalseStarts, Is.EqualTo(2));
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadLeft));
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(run.FalseStarts, Is.EqualTo(3), "A held D-pad direction also counts once.");
            InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
            var area = screen.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Game surface");
            Vector2 outsideTarget = RectTransformUtility.WorldToScreenPoint(null, area.TransformPoint(new Vector3(5, -5, 0)));
            yield return ClickAt(outsideTarget); Assert.That(run.FalseStarts, Is.EqualTo(4));
            run.Tick(.5); screen.Refresh(); yield return null;
            yield return ClickAt(outsideTarget);
            Assert.That(run.PointerMisses, Is.EqualTo(1)); Assert.That(run.Hits, Is.Zero);
            while (!run.TargetLive) run.Tick(.01);
            screen.Refresh(); yield return null;
            var target = screen.GetComponentsInChildren<Button>().Single(button => button.name == "Reaction target");
            var rect = (RectTransform)target.transform;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            yield return ClickAt(center);
            Assert.That(run.Hits, Is.EqualTo(1), "A target click must hit once without bubbling into a background miss.");
            Assert.That(run.PointerMisses, Is.EqualTo(1)); Assert.That(run.FalseStarts, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator MemoryPauseRestoresTheSelectedCardAndFinishedActionsStayReachable()
        {
            CreateInput(); var run = new MiniGameRun(CompetitionMiniGames.Kind.Memory, 77, 3); int continued = 0;
            screen.Show(run, "Competition", "Player", true, i => run.Flip(i), () => {}, d => {}, () => {}, () => screen.Hide());
            yield return null; screen.AdvanceReady(4); yield return null;
            yield return KeyPress(Key.RightArrow); yield return KeyPress(Key.DownArrow); AssertSelected("Memory card 6");
            yield return KeyPress(Key.Tab); yield return KeyPress(Key.Enter); Assert.That(screen.Paused, Is.True);
            yield return KeyPress(Key.Enter); AssertSelected("Memory card 6");
            run.Finish();
            screen.ShowFinished("The result could not be saved.", "Retry saving this result", () => continued++);
            yield return null; AssertSelected("Competition result action");
            yield return KeyPress(Key.Tab); AssertSelected("Cancel attempt");
            yield return KeyPress(Key.Tab, true); AssertSelected("Competition result action");
            yield return KeyPress(Key.Enter); Assert.That(continued, Is.EqualTo(1));
            yield return PadPress(GamepadButton.DpadDown); AssertSelected("Cancel attempt");
            yield return PadPress(GamepadButton.DpadUp); AssertSelected("Competition result action");
        }

        [UnityTest]
        public IEnumerator FrameStallPausesBeforeAnyAttemptTimeOrTargetIsLost()
        {
            CreateInput(); var run = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 77, 3);
            screen.Show(run, "Competition", "Player", true, i => {}, () => {}, d => {}, () => {}, () => screen.Hide());
            yield return null; screen.AdvanceReady(4); yield return null;
            run.Tick(.5); screen.Refresh();
            Assert.That(screen.AdvanceReady(1), Is.False); Assert.That(screen.Paused, Is.True);
            Assert.That(run.Elapsed, Is.EqualTo(.5)); Assert.That(run.TargetLive, Is.True); Assert.That(run.ExpiredTargets, Is.Zero);
            yield return KeyPress(Key.Enter); Assert.That(screen.IsPlaying, Is.True);
            Assert.That(screen.AdvanceReady(.01f), Is.True);
            Assert.That(run.Elapsed, Is.EqualTo(.5), "Only the director advances a resumed attempt.");
        }

        private static void AssertSelected(string name) => Assert.That(EventSystem.current.currentSelectedGameObject?.name, Is.EqualTo(name));
        private IEnumerator KeyPress(Key key, bool shift = false)
        {
            InputSystem.QueueStateEvent(keyboard, shift ? new KeyboardState(key, Key.LeftShift) : new KeyboardState(key));
            yield return null; InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }
        private IEnumerator PadPress(GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return null; InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
        }
        private IEnumerator ClickAt(Vector2 position)
        {
            InputSystem.QueueStateEvent(pointer, new MouseState { position = position }); yield return null;
            InputSystem.QueueStateEvent(pointer, new MouseState { position = position }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(pointer, new MouseState { position = position }); yield return null;
        }
    }
}
