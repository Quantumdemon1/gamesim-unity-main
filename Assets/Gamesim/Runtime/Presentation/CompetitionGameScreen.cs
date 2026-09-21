using System;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>A persistent game surface: no timed controls scroll and card positions never reflow.</summary>
    [DisallowMultipleComponent]
    public sealed partial class CompetitionGameScreen : MonoBehaviour
    {
        private static readonly string[] Faces = { "STAR", "KEY", "CROWN", "EYE", "FLAME", "ANCHOR", "CLOVER", "MOON" };
        private CanvasGroup group, controls;
        private RectTransform panel, playArea, target, scrim;
        private TMP_Text clock, status, feedback, countdown, targetLabel, gripLabel, actionLabel, arenaStatus;
        private Image gripFill;
        private Button pause, cancel, finishAction;
        private readonly Button[] cards = new Button[16];
        private readonly TMP_Text[] cardLabels = new TMP_Text[16];
        private MiniGameRun run;
        private Action cancelAction;
        private float countdownLeft;
        private int shownFrame;
        private bool playing;
        private bool previewStarted;
        private Selectable reactionFocus;
        private Selectable effortControl, lastGameFocus;
        private int dismissedFrame = -10;
        public bool IsShowing { get; private set; }
        public bool Paused { get; private set; }
        public bool IsPlaying => IsShowing && playing && !Paused;
        public float FontScale { get; set; } = 1;
        public bool OwnsMenuInput => IsShowing || Time.frameCount <= dismissedFrame + 1;

        public static CompetitionGameScreen Attach(GameObject owner)
        {
            var root = new GameObject("Gamesim Competition Surface", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(CanvasGroup), typeof(GraphicRaycaster));
            if (owner != null && owner.scene.IsValid()) SceneManager.MoveGameObjectToScene(root, owner.scene);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 104;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            var screen = root.AddComponent<CompetitionGameScreen>();
            screen.group = root.GetComponent<CanvasGroup>(); screen.Hide();
            return screen;
        }

        public void Show(MiniGameRun attempt, string title, string field, bool practice, Action<int> flip,
            Action tap, Action<MiniGameRun.Direction> direction, Action toggleGrip, Action onCancel, Action missedTarget = null, bool assemble = false)
        {
            ClearAssembly();
            if (panel != null) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
            run = attempt; cancelAction = onCancel; playing = false; Paused = false;
            previewStarted = false;
            lastGameFocus = null; reactionFocus = null; effortControl = null;
            countdownLeft = 3; shownFrame = Time.frameCount; IsShowing = true;
            group.alpha = 1; group.blocksRaycasts = true; group.interactable = true;
            if (scrim == null)
            {
                scrim = HudPrimitives.Fill("Competition input shield",transform,new Color(0,0,0,.55f),1);
                scrim.anchorMin=Vector2.zero;scrim.anchorMax=Vector2.one;scrim.offsetMin=scrim.offsetMax=Vector2.zero;
                scrim.GetComponent<Image>().raycastTarget=true;
            }
            panel = HudPrimitives.Fill("Competition studio", transform, new Color(.035f,.055f,.085f,.98f), 16);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f,.5f); panel.pivot = new Vector2(.5f,.5f);
            panel.sizeDelta = new Vector2(1420, 780); panel.GetComponent<Image>().raycastTarget = true;
            Label("Competition title", panel, title + (practice ? " · PRACTICE" : " · RANKED"), 30, 36, 22, 1348, 52, UiTheme.Paper);
            string rules = run.Definition?.Summary ?? CompetitionMiniGames.Brief(run.Kind, run.RulesVersion);
            if (run.Definition != null && run.Kind == CompetitionMiniGames.Kind.Reaction)
                rules += " Click the target or press its direction. Early input and incorrect aim cost accuracy.";
            Label("Rules", panel, rules, 18, 36, 82, 1348, 96, UiTheme.Muted);
            clock = Label("Competition clock", panel, "Ready in 3", 26, 36, 174, 450, 42, UiTheme.Gold);
            status = Label("Progress", panel, "", 22, 494, 174, 420, 42, UiTheme.Paper);
            playArea = HudPrimitives.Fill("Game surface", panel, UiTheme.Surface, 12);
            Place(playArea, 36, 230, 880, 430);
            controls = playArea.gameObject.AddComponent<CanvasGroup>(); controls.interactable = false;
            Label("Competition field", panel, "COMPETITORS\n" + field, 16, 956, 230, 425, 282, UiTheme.Paper);
            arenaStatus=Label("Arena status",panel,"",16,956,514,420,44,UiTheme.Muted);
            Label("Attempt policy", panel, practice
                ? "Practice never changes your season. Ranked play uses a separate, fixed board."
                : "Cancel or reload returns to this same ranked board. Scores commit once, after play ends.",
                17, 956, 562, 420, 98, UiTheme.Muted);
            feedback = Label("Attempt feedback", panel, "", 18, 36, 672, 1350, 36, UiTheme.Paper);
            pause = Button("Pause competition", panel, "Pause", 36, 718, 250, 46, TogglePause);
            cancel = Button("Cancel attempt", panel, "Back to briefing", 310, 718, 310, 46, () => cancelAction?.Invoke());
            finishAction = Button("Competition result action", panel, "Continue", 950, 718, 434, 46, () => { });
            finishAction.gameObject.SetActive(false);
            switch (run.Kind)
            {
                case CompetitionMiniGames.Kind.Memory: BuildMemory(flip); break;
                case CompetitionMiniGames.Kind.Reaction:
                    var input = playArea.gameObject.AddComponent<CompetitionDirectionControl>();
                    input.Pressed = direction; input.Missed = missedTarget;
                    input.targetGraphic = playArea.GetComponent<Image>(); reactionFocus = input;
                    input.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = pause, selectOnUp = cancel };
                    playArea.GetComponent<Image>().raycastTarget = true;
                    target = Button("Reaction target", playArea, "", 0, 0, 130, 92, () =>
                    { tap(); if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(input.gameObject); }).GetComponent<RectTransform>();
                    targetLabel = target.GetComponentInChildren<TMP_Text>();
                    target.GetComponent<Button>().navigation = new Navigation { mode = Navigation.Mode.None };
                    target.gameObject.SetActive(false);
                    break;
                case CompetitionMiniGames.Kind.Endurance:
                    Label("Endurance instruction", playArea, "EFFORT / RECOVERY", 29, 38, 40, 802, 64, UiTheme.Paper);
                    var track = HudPrimitives.Fill("Grip track", playArea, UiTheme.Ink, 8); Place(track,38,133,802,58);
                    var fill = HudPrimitives.Fill("Grip remaining",track,UiTheme.PositiveDeep,8);
                    fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
                    gripFill = fill.GetComponent<Image>();
                    gripLabel = Label("Grip value",playArea,"Grip 100%",24,38,205,802,45,UiTheme.Paper);
                    var hold = Button("Toggle grip",playArea,"Start holding",38,285,802,86,toggleGrip);
                    effortControl = hold;
                    actionLabel = hold.GetComponentInChildren<TMP_Text>();
                    hold.navigation = new Navigation { mode=Navigation.Mode.Explicit, selectOnDown=pause, selectOnUp=cancel };
                    break;
            }
            countdown = Label("Countdown", playArea, "3", 80, 0, 125, 880, 160, UiTheme.Gold);
            countdown.alignment = TextAlignmentOptions.Center;
            pause.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = cancel, selectOnLeft = cancel,
                selectOnUp = run.Kind == CompetitionMiniGames.Kind.Memory ? cards[12] : null };
            cancel.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = pause, selectOnRight = pause,
                 selectOnUp = run.Kind == CompetitionMiniGames.Kind.Memory ? cards[15] : null };
            WireFooter();
            Select(pause);
            Canvas.ForceUpdateCanvases();
            Refresh();
            if (assemble) BeginAssembly(title);
        }

        private void BuildMemory(Action<int> flip)
        {
            for (int i=0;i<16;i++)
            {
                int card = i;
                cards[i] = Button("Memory card " + (i+1),playArea,"CARD " + (i+1),
                    12 + i%4*217, 12 + i/4*103, 205, 91, () => flip(card));
                cardLabels[i] = cards[i].GetComponentInChildren<TMP_Text>();
                cardLabels[i].fontSize = 20 * FontScale;
            }
            for (int i=0;i<16;i++)
                cards[i].navigation = new Navigation { mode=Navigation.Mode.Explicit,
                    selectOnLeft=cards[i/4*4+(i+3)%4], selectOnRight=cards[i/4*4+(i+1)%4],
                    selectOnUp=i<4?cancel:cards[i-4], selectOnDown=i>=12?pause:cards[i+4] };
        }

        /// <summary>Returns true only after layout and the complete ready countdown have finished.</summary>
        public bool AdvanceReady(float delta)
        {
            if (!IsShowing || IsAssembling || Paused || Time.frameCount <= shownFrame) return false;
            if ((playing || previewStarted) && delta > .25f)
            {
                // A stalled frame must not silently play unseen targets or exhaust the player.
                TogglePause();
                feedback.text = "Paused after a frame delay. Resume when ready; no attempt time was lost.";
                return false;
            }
            if (playing) return true;
            bool preview = run.Definition?.Pattern == CompetitionPattern.PreviewPairs;
            if (preview && !previewStarted)
            {
                previewStarted = true; countdownLeft = (float)run.Definition.PreviewSeconds;
                countdown.gameObject.SetActive(false); clock.text = "Memorize · " + countdownLeft.ToString("0") + " seconds";
                Refresh(); return false;
            }
            countdownLeft = Mathf.Max(0, countdownLeft - Mathf.Max(0,delta));
            if (countdownLeft > 0)
            {
                countdown.text = Mathf.CeilToInt(countdownLeft).ToString();
                clock.text = (preview ? "Memorize · " : "Ready in ") + Mathf.CeilToInt(countdownLeft);
                countdown.gameObject.SetActive(!preview); return false;
            }
            playing = true; controls.interactable = true; countdown.gameObject.SetActive(false);
            FocusGame(); WireFooter();
            Refresh();
            // Start on the following frame; the opening button/key never counts as gameplay.
            return false;
        }

        public void SetArenaStatus(string text) { if(arenaStatus!=null)arenaStatus.text=text??""; if(assemblyStatus!=null)assemblyStatus.text=text??""; }
        public void HoldReady(string text)
        {
            if(!IsShowing||playing||Paused)return;
            clock.text="Preparing the arena";countdown.text="GET READY";countdown.fontSize=48*FontScale;
            feedback.text=text;
        }

        public void TogglePause()
        {
            if (!IsShowing || run == null || run.Finished) return;
            if (IsAssembling) { ToggleAssemblyPause(); return; }
            RememberGameFocus();
            Paused = !Paused; controls.interactable = playing && !Paused;
            pause.GetComponentInChildren<TMP_Text>().text = Paused ? "Resume" : "Pause";
            run.SetHolding(false);
            countdown.gameObject.SetActive(Paused || !playing);
            if (Paused) { countdown.text = "PAUSED"; Select(pause); }
            else if (playing) FocusGame();
            WireFooter();
            Refresh();
        }

        private void Update()
        {
            if (!IsShowing) return;
            var keyboard = Keyboard.current; var pad = Gamepad.current;
            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                || (pad != null && pad.buttonEast.wasPressedThisFrame))
            { cancelAction?.Invoke(); return; }
            if ((keyboard != null && keyboard.pKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame))
                TogglePause();
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
                MoveControlFocus(keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            else if (pad != null && pad.rightShoulder.wasPressedThisFrame) MoveControlFocus(false);
            else if (pad != null && pad.leftShoulder.wasPressedThisFrame) MoveControlFocus(true);
            RememberGameFocus();
        }

        /// <summary>Tab and shoulders visit the game, Pause and Back, preserving the selected card.</summary>
        public void MoveControlFocus(bool reverse)
        {
            if (!IsShowing || EventSystem.current == null) return;
            if (IsAssembling) { MoveAssemblyFocus(reverse); return; }
            RememberGameFocus();
            var selected = EventSystem.current.currentSelectedGameObject;
            bool finished = finishAction != null && finishAction.gameObject.activeSelf;
            if (finished) { Select(selected == cancel.gameObject ? finishAction : cancel); return; }
            if (!IsPlaying) { Select(selected == pause.gameObject ? cancel : pause); return; }
            if (selected == pause.gameObject) { if (reverse) FocusGame(); else Select(cancel); }
            else if (selected == cancel.gameObject) { if (reverse) Select(pause); else FocusGame(); }
            else Select(reverse ? cancel : pause);
        }

        private void RememberGameFocus()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null || playArea == null || !selected.transform.IsChildOf(playArea)) return;
            var selectable = selected.GetComponent<Selectable>();
            if (selectable != null && selectable != lastGameFocus && (selectable == reactionFocus || selectable == effortControl
                || run.Kind == CompetitionMiniGames.Kind.Memory))
            { lastGameFocus = selectable; if (playing) WireFooter(); }
        }

        private Selectable GameFocus => lastGameFocus != null ? lastGameFocus
            : run.Kind == CompetitionMiniGames.Kind.Memory ? cards[0]
            : run.Kind == CompetitionMiniGames.Kind.Reaction ? reactionFocus : effortControl;

        private void FocusGame() => Select(GameFocus);

        private static void Select(Selectable item)
        {
            if (item != null && item.IsActive() && item.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(item.gameObject);
        }

        private void WireFooter()
        {
            var game = IsPlaying ? GameFocus : null;
            pause.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = game != null ? game : cancel,
                selectOnDown = cancel, selectOnLeft = cancel, selectOnRight = cancel };
            cancel.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = pause,
                selectOnDown = game != null ? game : pause, selectOnLeft = pause, selectOnRight = pause };
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && IsShowing && !Paused && run != null && !run.Finished) TogglePause();
        }

        public void Refresh()
        {
            if (!IsShowing || run == null) return;
            if (playing) clock.text = Paused ? "Paused · clock stopped" : run.Remaining.ToString("0.0") + " seconds left";
            switch(run.Kind)
            {
                case CompetitionMiniGames.Kind.Memory:
                    bool preview = previewStarted && !playing && !run.Finished && countdownLeft > 0;
                    status.text = preview ? "MEMORIZE ALL 16 CARDS" : run.MatchedPairs + " / 8 pairs  ·  " + run.WrongFlips + " mistakes";
                    for(int i=0;i<16;i++)
                    {
                        bool faceUp = preview || run.Matched[i] || i == run.FirstFlip || i == run.SecondFlip;
                        cardLabels[i].text = "Card " + (i+1) + (faceUp ? ": " + Faces[run.Faces[i]] + (run.Matched[i] ? "\nMATCHED" : "") : "");
                        cards[i].GetComponent<Image>().color = run.Matched[i] ? UiTheme.PositiveDeep : faceUp ? UiTheme.AccentDeep : UiTheme.Ink;
                    }
                    feedback.text = "Arrows / D-pad: move  ·  Enter / A: flip  ·  Tab / shoulders: controls  ·  P / Start: pause";
                    break;
                case CompetitionMiniGames.Kind.Reaction:
                    status.text = run.Hits + " / " + run.Spawned + " hits  ·  " + run.FalseStarts + " early";
                    target.gameObject.SetActive(playing && !Paused && run.TargetLive);
                    if(run.TargetLive)
                    {
                        Place(target, 16+(float)run.TargetX*718, 16+(1-(float)run.TargetY)*306, 130, 92);
                        targetLabel.text = run.RulesVersion >= CompetitionMiniGames.ImprovedRules ? run.TargetDirection.ToString().ToUpperInvariant() : "HIT";
                        if (run.Definition != null) targetLabel.text += "\n" + run.TargetWindowSeconds.ToString("0.00") + " s";
                    }
                    feedback.text = run.Feedback + "  ·  Tab / shoulders: controls  ·  P / Start: pause  ·  Esc / B: briefing";
                    break;
                case CompetitionMiniGames.Kind.Endurance:
                    status.text = "Effort " + run.Held.ToString("0.0") + " seconds";
                    gripFill.rectTransform.anchorMax = new Vector2((float)run.Meter/100,1);
                    gripFill.color = run.Meter < 25 ? UiTheme.Warning : UiTheme.PositiveDeep;
                    gripLabel.text = "Grip " + run.Meter.ToString("0") + "% · " + (run.Holding ? "HOLDING" : "RECOVERING");
                    if (run.Definition?.Pattern == CompetitionPattern.PressureWaves)
                        gripLabel.text += run.GripPressure > 1 ? " · WAVE · " + run.PressureChangeIn.ToString("0.0") + "s"
                            : " · Wave in " + run.PressureChangeIn.ToString("0.0") + "s";
                    actionLabel.text = run.Holding ? "Release to recover" : "Hold to earn effort";
                    feedback.text = "Space / right trigger: hold  ·  Enter / A: toggle  ·  Tab / shoulders: controls  ·  P / Start: pause";
                    break;
            }
        }

        public void ShowFinished(string summary, string action, Action onContinue)
        {
            playing=false; controls.interactable=false; countdown.gameObject.SetActive(false);
            clock.text = "Attempt complete"; feedback.text = summary;
            status.text = "Performance " + (run.Performance*100).ToString("0") + "%";
            pause.gameObject.SetActive(false); finishAction.gameObject.SetActive(true);
            finishAction.GetComponentInChildren<TMP_Text>().text=action;
            finishAction.onClick.RemoveAllListeners(); finishAction.onClick.AddListener(() => onContinue?.Invoke());
            finishAction.navigation = new Navigation { mode=Navigation.Mode.Explicit,selectOnLeft=cancel,selectOnRight=cancel,
                selectOnUp=cancel,selectOnDown=cancel };
            cancel.navigation = new Navigation { mode=Navigation.Mode.Explicit,selectOnLeft=finishAction,selectOnRight=finishAction,
                selectOnUp=finishAction,selectOnDown=finishAction };
            if(EventSystem.current!=null) EventSystem.current.SetSelectedGameObject(finishAction.gameObject);
        }

        public void Hide()
        {
            if (IsShowing) dismissedFrame = Time.frameCount;
            IsAssembling=false; if(assemblyPanel!=null)assemblyPanel.gameObject.SetActive(false);
            IsShowing=false; playing=false; Paused=false;
            if(group!=null) { group.alpha=0;group.interactable=false;group.blocksRaycasts=false; }
            if(EventSystem.current!=null && EventSystem.current.currentSelectedGameObject!=null
                && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
                EventSystem.current.SetSelectedGameObject(null);
        }

        private TMP_Text Label(string name,Transform parent,string text,float size,float x,float y,float w,float h,Color colour)
        {
            var label=HudPrimitives.Label(name,parent,size*FontScale,colour,TextAlignmentOptions.Left);
            label.text=text; label.textWrappingMode=TextWrappingModes.Normal; Place(label.rectTransform,x,y,w,h); return label;
        }

        private Button Button(string name,Transform parent,string caption,float x,float y,float w,float h,Action action)
        {
            var rect=HudPrimitives.Fill(name,parent,UiTheme.AccentDeep,8);Place(rect,x,y,w,h);
            rect.GetComponent<Image>().raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();
            button.onClick.AddListener(() => action?.Invoke());
            var label=Label("Label",rect,caption,20,10,4,w-20,h-8,UiTheme.Paper);label.alignment=TextAlignmentOptions.Center;
            return button;
        }

        private static void Place(RectTransform rect,float x,float y,float w,float h)
        {
            rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);
        }
    }
}
