using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
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
    /// <summary>
    /// The competition frame redrawn: what stands over the board when it is not being played, the
    /// cards around it, the keys, and a layout that follows the window.
    /// </summary>
    public sealed partial class CompetitionSurfacePlayModeTests
    {
        private TMP_Text Text(string name) => screen.GetComponentsInChildren<TMP_Text>().Single(label => label.name == name);
        private RectTransform Rect(string name) => screen.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == name);
        private int FacesShowing() => screen.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Memory card "))
            .Count(button => button.GetComponentInChildren<TMP_Text>().text.Contains(":"));

        /// <summary>
        /// First Impressions shows its faces for exactly its three seconds, however the preview is
        /// interrupted: a pause, or the arena slipping, puts every card down and stops the preview's
        /// clock; picking up again plays only what was left. A paused preview was a free look.
        /// </summary>
        [UnityTest]
        public IEnumerator Preview_APauseOrAHoldHidesTheFacesAndStopsThePreviewClock()
        {
            owner = new GameObject("Preview integrity test"); screen = CompetitionGameScreen.Attach(owner);
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Memory, 17, 3, CompetitionDefinitions.FirstImpressions);
            screen.Show(run, run.Definition.Title, "Player", true, i => run.Flip(i), () => {}, d => {}, () => {}, () => {});
            yield return null;
            screen.AdvanceReady(.01f);
            Assert.That(FacesShowing(), Is.EqualTo(16), "The preview shows every face.");
            float shown = 0f;

            screen.TogglePause();
            Assert.That(FacesShowing(), Is.Zero, "A paused preview shows no face.");
            Assert.That(Rect("Countdown plate").gameObject.activeInHierarchy, Is.True);
            Assert.That(Rect("Countdown plate").GetComponent<Image>().color.a, Is.GreaterThan(.9f), "and nothing reads through the pause.");
            screen.TogglePause();
            screen.AdvanceReady(.01f); shown += .01f;
            Assert.That(FacesShowing(), Is.EqualTo(16));

            for (int i = 0; i < 4; i++) { screen.AdvanceReady(.25f); shown += .25f; }
            screen.HoldReady("A houseguest stepped off their mark");
            Assert.That(FacesShowing(), Is.Zero, "The arena slipping puts the cards down too.");
            Assert.That(Text("Countdown detail").text, Does.Contain("stepped off their mark"));
            bool ticked = false;
            for (int i = 0; i < 40 && !screen.IsPlaying; i++)
            {
                bool before = FacesShowing() == 16;
                ticked |= screen.AdvanceReady(.1f);
                if (before || FacesShowing() == 16) shown += .1f;
            }
            Assert.That(screen.IsPlaying, Is.True);
            Assert.That(shown, Is.EqualTo(3f).Within(.15f), "The faces were up for the preview's three seconds, no more.");
            Assert.That(ticked, Is.False, "and the playing clock was never let run during it.");
        }

        [UnityTest]
        public IEnumerator Overlay_EachStateHasItsOwnSizeAndNoBoardControlTurnsHalfTransparent()
        {
            owner = new GameObject("Overlay test"); screen = CompetitionGameScreen.Attach(owner); screen.FontScale = 1.2f;
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Memory, 11, 3);
            screen.Show(run, "Veto", "Player", true, i => run.Flip(i), () => {}, d => {}, () => {}, () => {});
            yield return null;
            var headline = Text("Countdown");
            screen.HoldReady("Walking to the stations");
            Assert.That(headline.text, Is.EqualTo("GET READY"));
            Assert.That(headline.fontSize, Is.EqualTo(56f * 1.2f).Within(.01f));
            screen.AdvanceReady(.5f);
            Assert.That(headline.text, Is.EqualTo("3"), "The count picks up where it stood.");
            Assert.That(headline.fontSize, Is.EqualTo(96f * 1.2f).Within(.01f), "and at its own size - GET READY's never leaks into it.");
            yield return null; yield return null;
            foreach (var card in screen.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Memory card ")))
            {
                Assert.That(card.transition, Is.EqualTo(Selectable.Transition.None), card.name + " is not tinted while the board waits.");
                foreach (var graphic in card.GetComponentsInChildren<Graphic>())
                    Assert.That(graphic.canvasRenderer.GetColor().a, Is.GreaterThan(.99f), graphic.name + " on " + card.name);
            }
            screen.AdvanceReady(4f);
            Assert.That(screen.IsPlaying, Is.True);
            Assert.That(headline.text, Is.EqualTo("GO"));
            // The other boards' controls too: the reaction board's own glass, and the grip toggle.
            foreach (var kind in new[] { CompetitionMiniGames.Kind.Reaction, CompetitionMiniGames.Kind.Endurance })
            {
                var other = new MiniGameRun(kind, 11, 3);
                var probeOwner = new GameObject("Tint probe");
                var probe = CompetitionGameScreen.Attach(probeOwner);
                probe.Show(other, "Veto", "Player", true, i => {}, () => {}, d => {}, () => {}, () => {});
                yield return null; yield return null;
                string control = kind == CompetitionMiniGames.Kind.Reaction ? "Game surface" : "Toggle grip";
                var graphic = probe.GetComponentsInChildren<Image>().Single(image => image.name == control);
                Assert.That(graphic.canvasRenderer.GetColor().a, Is.GreaterThan(.99f), control + " is not half-tinted while the board waits.");
                Object.Destroy(probe.gameObject); Object.Destroy(probeOwner);
            }
            Assert.That(headline.gameObject.activeInHierarchy, Is.True);
            float until = Time.realtimeSinceStartup + .8f;
            while (Time.realtimeSinceStartup < until) yield return null;
            Assert.That(headline.gameObject.activeInHierarchy, Is.False, "GO gets out of the way.");
        }

        [UnityTest]
        public IEnumerator Finish_AHoldAfterTheEndChangesNothingAndPracticeLeavesOneWayOn()
        {
            CreateInput();
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Endurance, 3, 3, CompetitionDefinitions.HoldYourGround);
            int continued = 0;
            screen.Show(run, "Head of Household · Hold Your Ground", "Player", true, i => {}, () => {}, d => {}, () => run.SetHolding(!run.Holding), () => screen.Hide());
            yield return null; screen.AdvanceReady(4f); yield return null;
            run.SetHolding(true);
            while (!run.Finished) run.Tick(.1);
            screen.Refresh();
            screen.ShowFinished("Practice complete.", "Return to briefing", () => continued++, hideCancel: true);
            yield return null;
            var clock = Text("Competition clock").text;
            screen.HoldReady("late news from the arena");
            Assert.That(Text("Competition clock").text, Is.EqualTo(clock), "A finished attempt is not held.");
            Assert.That(screen.GetComponentsInChildren<TMP_Text>().Any(label => label.name == "Countdown"), Is.False,
                "No GET READY over a finished board.");
            Assert.That(Rect("Finish plate").gameObject.activeInHierarchy, Is.True);
            Assert.That(Text("Finish headline").text, Does.StartWith("GRIP GAVE OUT AT"), "It says the grip gave out, and when.");
            Assert.That(Text("Finish performance").text, Does.StartWith("Performance "));
            Assert.That(screen.GetComponentsInChildren<Button>().Any(button => button.name == "Toggle grip"), Is.False, "The toggle goes with the attempt.");
            Assert.That(screen.GetComponentsInChildren<Button>().Any(button => button.name == "Cancel attempt"), Is.False,
                "One way on where both would return to the briefing.");
            var action = (RectTransform)screen.GetComponentsInChildren<Button>().Single(button => button.name == "Competition result action").transform;
            Assert.That(action.anchoredPosition.y, Is.EqualTo(-14f).Within(.5f), "at the top of the controls card.");
            AssertSelected("Competition result action");
            yield return KeyPress(Key.Tab); AssertSelected("Competition result action");
            yield return KeyPress(Key.Enter); Assert.That(continued, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Keys_TheHoldKeyIsALevelAndSurvivesAPause()
        {
            CreateInput();
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Endurance, 3, 3, CompetitionDefinitions.HoldYourGround);
            screen.Show(run, "Veto · Hold Your Ground", "Player", true, i => {}, () => {}, d => {}, () => run.SetHolding(!run.Holding), () => screen.Hide());
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); yield return null;
            screen.SyncHoldKey();
            Assert.That(run.Holding, Is.False, "Not before play.");
            screen.AdvanceReady(4f);
            yield return null;
            Assert.That(screen.AdvanceReady(.01f), Is.True);
            screen.SyncHoldKey();
            Assert.That(run.Holding, Is.True, "Space already down when GO lands grips on the first live frame.");
            screen.TogglePause(); Assert.That(run.Holding, Is.False, "A pause lets go.");
            screen.TogglePause(); screen.SyncHoldKey();
            Assert.That(run.Holding, Is.True, "and a resume with Space still down grips again.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            screen.SyncHoldKey(); Assert.That(run.Holding, Is.False, "Letting go of Space lets go.");
            run.SetHolding(true); screen.SyncHoldKey();
            Assert.That(run.Holding, Is.True, "The key only speaks when it changes: the on-screen toggle's hold stands.");
        }

        [UnityTest]
        public IEnumerator Keys_EscapeInARankedAttemptAsksFirstAndAPracticeLeavesAtOnce()
        {
            CreateInput();
            int cancelled = 0;
            var ranked = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 3, 3, CompetitionDefinitions.SignalSprint);
            screen.Show(ranked, "Head of Household · Signal Sprint", "Player", false, i => {}, () => {}, d => {}, () => {}, () => { cancelled++; screen.Hide(); });
            yield return null; screen.AdvanceReady(4f); yield return null;
            yield return KeyPress(Key.Escape);
            Assert.That(cancelled, Is.Zero, "The first Escape in a ranked attempt does not throw it away.");
            Assert.That(screen.Paused, Is.True);
            AssertSelected("Cancel attempt");
            Assert.That(Text("Countdown detail").text, Does.Contain("again"));
            yield return KeyPress(Key.Escape);
            Assert.That(cancelled, Is.EqualTo(1), "The second leaves.");

            var button = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 3, 3, CompetitionDefinitions.SignalSprint);
            screen.Show(button, "Head of Household · Signal Sprint", "Player", false, i => {}, () => {}, d => {}, () => {}, () => { cancelled++; screen.Hide(); });
            yield return null; screen.AdvanceReady(4f); yield return null;
            screen.GetComponentsInChildren<Button>().Single(control => control.name == "Cancel attempt").onClick.Invoke();
            Assert.That(cancelled, Is.EqualTo(1), "Back to briefing asks first in a ranked attempt under way, as Escape does.");
            Assert.That(screen.Paused, Is.True);
            screen.GetComponentsInChildren<Button>().Single(control => control.name == "Cancel attempt").onClick.Invoke();
            Assert.That(cancelled, Is.EqualTo(2), "and the second press leaves.");

            var ended = new MiniGameRun(CompetitionMiniGames.Kind.Memory, 3, 3, CompetitionDefinitions.HouseMemory);
            screen.Show(ended, "Head of Household · House Memory", "Player", false, i => ended.Flip(i), () => {}, d => {}, () => {}, () => { cancelled++; screen.Hide(); });
            yield return null; screen.AdvanceReady(4f); yield return null;
            ended.Finish(); screen.Refresh();
            yield return KeyPress(Key.Escape);
            Assert.That(cancelled, Is.EqualTo(2), "A ranked board just finished is a result waiting for its plate, not an attempt to leave.");
            Assert.That(screen.IsShowing, Is.True);

            var practice = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 3, 3, CompetitionDefinitions.SignalSprint);
            screen.Show(practice, "Head of Household · Signal Sprint", "Player", true, i => {}, () => {}, d => {}, () => {}, () => { cancelled++; screen.Hide(); });
            yield return null; screen.AdvanceReady(4f); yield return null;
            yield return KeyPress(Key.Escape);
            Assert.That(cancelled, Is.EqualTo(3), "A practice leaves on the first press, as it always has.");
        }

        [UnityTest]
        public IEnumerator Keys_WasdAndTheStickAimLikeTheArrows()
        {
            CreateInput(); var run = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 77, 3);
            var pressed = new System.Collections.Generic.List<MiniGameRun.Direction>();
            screen.Show(run, "Competition", "Player", true, i => {}, () => {}, direction => { pressed.Add(direction); run.Tap(direction); }, () => {}, () => screen.Hide(), run.MissPointer);
            yield return null; screen.AdvanceReady(4f); yield return null;
            foreach (var letter in new[] { Key.W, Key.D, Key.S, Key.A }) yield return KeyPress(letter);
            foreach (var stick in new[] { new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(0f, -1f), new Vector2(-1f, 0f) })
            {
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = stick });
                yield return new WaitForSecondsRealtime(.4f);
                InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null; yield return null;
            }
            var turn = new[] { MiniGameRun.Direction.Up, MiniGameRun.Direction.Right, MiniGameRun.Direction.Down, MiniGameRun.Direction.Left };
            Assert.That(pressed, Is.EqualTo(turn.Concat(turn).ToArray()), "W D S A, then the stick up, right, down, left - each once.");
            while (!run.TargetLive) run.Tick(.01);
            screen.Refresh();
            var key = run.TargetDirection == MiniGameRun.Direction.Up ? Key.W : run.TargetDirection == MiniGameRun.Direction.Right ? Key.D
                : run.TargetDirection == MiniGameRun.Direction.Down ? Key.S : Key.A;
            yield return KeyPress(key);
            Assert.That(run.Hits, Is.EqualTo(1), "The target's own key hits it.");
        }

        /// <summary>
        /// The frame follows the window: switched to a 1600 by 900 camera (what a review capture
        /// does), every card and control is re-placed inside it and nothing is rebuilt - the same
        /// controls, the same one selected, the attempt not paused.
        /// </summary>
        [UnityTest]
        public IEnumerator Layout_FollowsTheFrameWithoutRebuilding()
        {
            CreateInput(); screen.FontScale = 1.2f;
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Memory, 77, 3, CompetitionDefinitions.HouseMemory);
            screen.Show(run, "Head of Household · House Memory", "Player\nMaya", true, i => run.Flip(i), () => {}, d => {}, () => {}, () => screen.Hide());
            yield return null; screen.AdvanceReady(4f); yield return null;
            yield return KeyPress(Key.RightArrow); yield return KeyPress(Key.DownArrow); AssertSelected("Memory card 6");
            var ids = screen.GetComponentsInChildren<Button>().Select(button => button.GetEntityId()).ToArray();

            var canvas = screen.GetComponent<Canvas>();
            var texture = new RenderTexture(1600, 900, 24);
            var viewer = new GameObject("Layout camera", typeof(Camera)).GetComponent<Camera>();
            viewer.targetTexture = texture;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = viewer; canvas.planeDistance = 1f;
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var root = (RectTransform)screen.transform;
                var frame = new Rect(0f, 0f, 1600f, 900f);
                foreach (var name in new[] { "Competition challenge", "Competition timer", "Game surface", "Competition field card",
                    "Competition controls", "Cancel attempt", "Control legend" })
                {
                    var corners = new Vector3[4]; Rect(name).GetWorldCorners(corners);
                    for (int i = 0; i < 4; i++)
                    {
                        var local = root.InverseTransformPoint(corners[i]);
                        var point = new Vector2(local.x - root.rect.xMin, local.y - root.rect.yMin);
                        Assert.That(point.x, Is.InRange(-1f, root.rect.width + 1f), name + " runs off the frame sideways.");
                        Assert.That(point.y, Is.InRange(-1f, root.rect.height + 1f), name + " runs off the frame: " + point + " in " + root.rect.size + ".");
                    }
                }
                Assert.That(root.rect.width, Is.EqualTo(1600f).Within(2f), "The screen laid out for the camera's frame.");
                foreach (var card in screen.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Memory card ")))
                {
                    var size = ((RectTransform)card.transform).rect.size;
                    // Card-shaped: 1.6 to 1, widened with the larger text so its caption keeps its size.
                    Assert.That(size.x / size.y, Is.LessThanOrEqualTo(1.6f * screen.FontScale + 1e-3f), card.name + " stays card-shaped: " + size + ".");
                }
                Assert.That(screen.GetComponentsInChildren<Button>().Select(button => button.GetEntityId()), Is.EqualTo(ids), "Re-placed, never rebuilt.");
                AssertSelected("Memory card 6");
                Assert.That(screen.Paused, Is.False, "A camera is not a window being resized.");
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Object.Destroy(viewer.gameObject); texture.Release(); Object.Destroy(texture);
            }
            yield return null;
            Assert.That(screen.Paused, Is.False);
            typeof(CompetitionGameScreen).GetField("screenFor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(screen, new Vector2Int(Screen.width + 7, Screen.height));
            yield return null;
            Assert.That(screen.Paused, Is.True, "A real resize mid-attempt pauses, noticed by the screen itself.");
            Assert.That(Text("Countdown detail").text, Does.Contain("The window changed size"));
        }

        [UnityTest]
        public IEnumerator Chrome_TheCardsSayTheGameItsRulesAndTheClock()
        {
            owner = new GameObject("Chrome test"); screen = CompetitionGameScreen.Attach(owner);
            foreach (float scale in new[] { 1f, 1.2f })
            foreach (var definition in CompetitionDefinitions.All)
            {
                screen.FontScale = scale;
                var run = new MiniGameRun(CompetitionMiniGames.For(definition.Category), 5, 3, definition);
                screen.Show(run, "Head of Household · " + definition.Title, "Player", true, i => {}, () => {}, d => {}, () => {}, () => {});
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(Text("Competition title").text, Is.EqualTo(definition.Title), "The game's own name, not the award.");
                Assert.That(Text("Challenge eyebrow").text, Is.EqualTo("HEAD OF HOUSEHOLD"));
                var rules = Text("Rules");
                Assert.That(rules.text, Does.Contain(definition.Summary));
                Assert.That(rules.text, Does.Contain(run.Kind == CompetitionMiniGames.Kind.Memory ? "0.15"
                    : run.Kind == CompetitionMiniGames.Kind.Endurance ? "empty grip ends the attempt" : "arrow keys"),
                    "The rules say the controls and the costs.");
                rules.ForceMeshUpdate();
                Assert.That(rules.fontSize, Is.GreaterThanOrEqualTo(12f), definition.Id + " at " + scale);
                Assert.That(rules.isTextOverflowing, Is.False, definition.Id + " at " + scale + " runs out of its card.");
                var card = Rect("Competition challenge").rect;
                var box = rules.rectTransform;
                Assert.That(-box.anchoredPosition.y + box.rect.height, Is.LessThanOrEqualTo(card.height + .5f), "Rules sit inside the card.");
            }
            var legacy = new MiniGameRun(CompetitionMiniGames.Kind.Endurance, 5, 2);
            screen.FontScale = 1f;
            screen.Show(legacy, "Veto", "Player", true, i => {}, () => {}, d => {}, () => {}, () => {});
            yield return null;
            Assert.That(Text("Competition title").text, Is.EqualTo("Hold On"), "Before titles, the game's own instruction.");
            Assert.That(Text("Challenge eyebrow").text, Is.EqualTo("VETO"));

            var timed = new MiniGameRun(CompetitionMiniGames.Kind.Endurance, 5, 3, CompetitionDefinitions.HoldYourGround);
            screen.Show(timed, "Veto · Hold Your Ground", "Player", true, i => {}, () => {}, d => {}, () => {}, () => {});
            yield return null; screen.AdvanceReady(4f);
            timed.Tick(30 - 4.9); screen.Refresh();
            var clock = Text("Competition clock");
            clock.ForceMeshUpdate();
            Assert.That(clock.GetParsedText(), Is.EqualTo("4.9"));
            Assert.That(clock.color, Is.EqualTo(UiTheme.Danger), "The last five seconds are red.");
            Assert.That(clock.textInfo.lineCount, Is.EqualTo(1));
            Assert.That(Text("Clock state").text, Is.EqualTo("FINAL SECONDS"));
            var ring = screen.GetComponentsInChildren<Image>().Single(image => image.name == "Clock ring");
            Assert.That(ring.fillAmount, Is.EqualTo(4.9f / 30f).Within(.01f), "The ring drains with the attempt.");
        }

        [UnityTest]
        public IEnumerator Chrome_FeedbackIsTheLiveMessageAndKeyHintsSitBesideCaptions()
        {
            CreateInput(); var run = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 77, 3, CompetitionDefinitions.SignalSprint);
            screen.Show(run, "Head of Household · Signal Sprint", "Player", true, i => {}, () => {}, direction => run.Tap(direction), () => {}, () => screen.Hide(), run.MissPointer);
            yield return null; screen.AdvanceReady(4f); yield return null;
            while (!run.TargetLive) run.Tick(.01);
            var wrong = (MiniGameRun.Direction)(((int)run.TargetDirection + 1) % 4);
            run.Tap(wrong); screen.Refresh();
            var feedback = Text("Attempt feedback");
            Assert.That(feedback.text, Is.EqualTo("Missed: wrong direction"), "Only what happened - the key hints are the legend's.");
            Assert.That(feedback.color, Is.EqualTo(UiTheme.Danger));
            var pause = screen.GetComponentsInChildren<Button>().Single(button => button.name == "Pause competition");
            Assert.That(pause.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Pause"), "The caption is the contract.");
            yield return KeyPress(Key.Tab);
            Assert.That(pause.transform.Find("Border").GetComponent<Image>().color, Is.EqualTo(UiTheme.Edge(UiTheme.Emphasis.Active)),
                "The control the keyboard is on shows it.");
            var back = screen.GetComponentsInChildren<Button>().Single(button => button.name == "Cancel attempt");
            Assert.That(back.transform.Find("Border").GetComponent<Image>().color, Is.EqualTo(UiTheme.Edge(UiTheme.Emphasis.Interactive)));
            yield return KeyPress(Key.Tab);
            Assert.That(back.transform.Find("Border").GetComponent<Image>().color, Is.EqualTo(UiTheme.Edge(UiTheme.Emphasis.Active)));
            Assert.That(pause.GetComponentsInChildren<TMP_Text>().Single(label => label.name == "Key hint").text, Is.EqualTo("P"));
            Assert.That(screen.GetComponentsInChildren<TMP_Text>().Where(label => label.name == "Key").Select(label => label.text),
                Does.Contain("Arrows / WASD").And.Contain("Tab"));
            yield return PadPress(GamepadButton.DpadUp);
            Assert.That(pause.GetComponentsInChildren<TMP_Text>().Single(label => label.name == "Key hint").text, Is.EqualTo("Start"),
                "A pad press turns the hints into the pad's buttons.");
        }
    }
}
