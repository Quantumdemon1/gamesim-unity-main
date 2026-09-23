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
                // Still the shield: it takes every click meant for the house while a competition
                // runs. It no longer paints the frame black - the lit stage the competition is
                // played on is the scene (mockup-05) - so only a vignette darkens the edges.
                scrim = HudPrimitives.Fill("Competition input shield",transform,new Color(0,0,0,0),1);
                scrim.anchorMin=Vector2.zero;scrim.anchorMax=Vector2.one;scrim.offsetMin=scrim.offsetMax=Vector2.zero;
                scrim.GetComponent<Image>().raycastTarget=true;
                var legibility=HudPrimitives.Vignette(scrim);
                legibility.name="Competition legibility";
                legibility.GetComponent<Image>().color=new Color(1f,1f,1f,.65f);
            }
            // The studio is a frame the cards hang in now, not a slab over the set: a transparent
            // container across the screen, still the one thing assembly hides and shows.
            panel = HudPrimitives.Fill("Competition studio", transform, new Color(0,0,0,0), 16);
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one; panel.pivot = new Vector2(.5f,.5f);
            panel.offsetMin = panel.offsetMax = Vector2.zero; panel.GetComponent<Image>().raycastTarget = false;

            float left = Episode.EpisodeHud.LeftColumnX;
            // The challenge: what this is and how it is played.
            var challenge = Card("Competition challenge", panel, new Vector2(left, -84f), new Vector2(470, 176));
            var crest = HudPrimitives.Glyph("Challenge mark", challenge, title != null && title.ToUpperInvariant().Contains("VETO") ? "veto-token" : "crown",
                UiTheme.Gold, new Vector2(16, -14), 20);
            var eyebrow = Label("Challenge eyebrow", challenge, "CURRENT CHALLENGE", 12, crest != null ? 44 : 16, 14, 300, 20, UiTheme.Gold);
            eyebrow.characterSpacing = 6f;
            var mode = HudPrimitives.Chip("Attempt mode", challenge, practice ? "PRACTICE" : "RANKED", practice ? UiTheme.Muted : UiTheme.Accent, 86, 20);
            mode.anchorMin = mode.anchorMax = new Vector2(1, 1); mode.pivot = new Vector2(1, 1); mode.anchoredPosition = new Vector2(-14, -12);
            mode.GetComponent<Image>().raycastTarget = false;
            var heading = Label("Competition title", challenge, title, 18, 16, 40, 438, 26, UiTheme.Paper);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold); if (semibold != null) heading.font = semibold;
            Fit(heading, 12);
            string rules = run.Definition?.Summary ?? CompetitionMiniGames.Brief(run.Kind, run.RulesVersion);
            if (run.Definition != null && run.Kind == CompetitionMiniGames.Kind.Reaction)
                rules += " Click the target or press its direction. Early input and incorrect aim cost accuracy.";
            Fit(Label("Rules", challenge, rules, 13, 16, 68, 438, 60, UiTheme.Muted), 10);
            Fit(Label("Attempt policy", challenge, practice
                ? "Practice never changes your season. Ranked play uses a separate, fixed board."
                : "Cancel or reload returns to this same ranked board. Scores commit once, after play ends.",
                12, 16, 132, 438, 34, UiTheme.Muted), 9);

            // The clock, as the mockup's timer card: time is the one number that is always moving.
            var timer = Card("Competition timer", panel, new Vector2(left + 482f, -84f), new Vector2(210, 176));
            UiTheme.AddGlow(timer, UiTheme.GlassRadius);
            var stopwatch = Label("Timer eyebrow", timer, "TIME REMAINING", 12, 16, 14, 180, 20, UiTheme.Accent);
            stopwatch.characterSpacing = 6f;
            clock = Label("Competition clock", timer, "Ready in 3", 24, 16, 42, 180, 64, UiTheme.Paper);
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) clock.font = bold;
            Fit(clock, 14);
            status = Label("Progress", timer, "", 14, 16, 110, 180, 54, UiTheme.Glow);
            Fit(status, 10);

            // The board: fixed geometry, on glass the stage shows through at its gutters.
            playArea = HudPrimitives.Fill("Game surface", panel, new Color(UiTheme.GlassFill.r, UiTheme.GlassFill.g, UiTheme.GlassFill.b, .72f), 12);
            Place(playArea, left, 272, 880, 430);
            UiTheme.AddBorder(playArea, 12, UiTheme.Edge(UiTheme.Emphasis.Resting));
            controls = playArea.gameObject.AddComponent<CanvasGroup>(); controls.interactable = false;
            feedback = Label("Attempt feedback", panel, "", 14, left, 710, 880, 26, UiTheme.Paper);
            Fit(feedback, 10);

            // Who is competing, down the right-hand side where the column of cards always is.
            var fieldCard = Card("Competition field card", panel, new Vector2(-24f, -84f), new Vector2(248, 356), true);
            var fieldHeading = Label("Field heading", fieldCard, "COMPETITORS", 12, 16, 14, 216, 20, UiTheme.Accent);
            fieldHeading.characterSpacing = 6f;
            Fit(Label("Competition field", fieldCard, field, 14, 16, 40, 216, 250, UiTheme.Paper), 10);
            arenaStatus = Label("Arena status", fieldCard, "", 12, 16, 296, 216, 50, UiTheme.Muted);
            Fit(arenaStatus, 9);

            // The controls, in their own card under it: nothing in the frame is a big blue slab.
            var footer = Card("Competition controls", panel, new Vector2(-24f, -452f), new Vector2(248, 172), true);
            pause = Button("Pause competition", footer, "Pause", 12, 14, 224, 44, TogglePause);
            cancel = Button("Cancel attempt", footer, "Back to briefing", 12, 66, 224, 44, () => cancelAction?.Invoke());
            finishAction = Button("Competition result action", footer, "Continue", 12, 118, 224, 44, () => { });
            finishAction.gameObject.SetActive(false);
            Secondary(pause); Secondary(cancel);
            UiTheme.PackSliced(finishAction.GetComponent<Image>(), PackArt.ButtonPrimary, 16f);
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
            countdown = Label("Countdown", playArea, "3", 90, 0, 125, 880, 160, UiTheme.Gold);
            countdown.alignment = TextAlignmentOptions.Center;
            var display = UiTheme.Font(UiTheme.Weight.Bold); if (display != null) countdown.font = display;
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
            // The mockups' action blue with white words: Paper on AccentDeep is 3.89:1, under the
            // 4.5 a label this size needs.
            var rect=HudPrimitives.Fill(name,parent,UiTheme.ActionBlue,8);Place(rect,x,y,w,h);
            rect.GetComponent<Image>().raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();
            button.onClick.AddListener(() => action?.Invoke());
            var label=Label("Label",rect,caption,20,10,4,w-20,h-8,Color.white);label.alignment=TextAlignmentOptions.Center;
            var medium=UiTheme.Font(UiTheme.Weight.SemiBold);if(medium!=null)label.font=medium;
            Fit(label,11);
            return button;
        }

        /// <summary>
        /// One of the screen's glass cards, anchored to a top corner of the frame: top-left when
        /// <paramref name="right"/> is false, top-right when it is. Nothing on it takes a click.
        /// </summary>
        private static RectTransform Card(string name, Transform parent, Vector2 position, Vector2 size, bool right = false)
        {
            var card = HudPrimitives.Fill(name, parent, new Color(UiTheme.GlassFill.r, UiTheme.GlassFill.g, UiTheme.GlassFill.b, .9f), UiTheme.GlassRadius);
            var corner = right ? new Vector2(1, 1) : new Vector2(0, 1);
            card.anchorMin = card.anchorMax = corner; card.pivot = corner;
            card.anchoredPosition = position; card.sizeDelta = size;
            card.GetComponent<Image>().raycastTarget = false;
            UiTheme.AddBorder(card, UiTheme.GlassRadius, UiTheme.Edge(UiTheme.Emphasis.Resting));
            return card;
        }

        /// <summary>A control that is not the one to press next: the pack's secondary button.</summary>
        private static void Secondary(Button button)
        {
            if (button == null) return;
            UiTheme.PackSliced(button.GetComponent<Image>(), PackArt.ButtonSecondary, 10f);
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) { label.color = UiTheme.Paper; var medium = UiTheme.Font(UiTheme.Weight.Medium); if (medium != null) label.font = medium; }
        }

        private static void Fit(TMP_Text label, float floor)
        {
            label.enableAutoSizing = true; label.fontSizeMax = label.fontSize; label.fontSizeMin = Mathf.Min(floor, label.fontSize);
        }

        private static void Place(RectTransform rect,float x,float y,float w,float h)
        {
            rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);
        }
    }
}
