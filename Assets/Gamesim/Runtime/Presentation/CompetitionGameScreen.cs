using System;
using System.Collections.Generic;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// A persistent game surface: no timed controls scroll and card positions never reflow.
    ///
    /// <para>Built once per attempt (<see cref="Show"/>) and laid out as often as the frame changes
    /// shape (<see cref="LayoutForFrame"/>): a relayout re-places what exists and never rebuilds it,
    /// so the selection, the controls' identities and the attempt survive a resize. Each game keeps
    /// its own Build / Place / Refresh in a partial of its own; the frame's cards, the overlay over
    /// the board, the legend and the finish plate are shared.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class CompetitionGameScreen : MonoBehaviour
    {
        private CanvasGroup group, controls;
        private Canvas surface;
        private RectTransform panel, playArea, scrim;
        private Button pause, cancel, finishAction;
        private MiniGameRun run;
        private Action cancelAction;
        private float countdownLeft;
        private int shownFrame;
        private bool playing;
        private bool previewStarted;
        // The arena's staging is holding the start (HoldReady): the faces a preview shows are hidden
        // and its clock stands still until the field is ready again.
        private bool held;
        private bool ranked;
        private Selectable lastGameFocus;
        private int dismissedFrame = -10;
        public bool IsShowing { get; private set; }
        public bool Paused { get; private set; }
        public bool IsPlaying => IsShowing && playing && !Paused;
        public float FontScale { get; set; } = 1;
        /// <summary>The player's reduced-motion setting: flips, pops and pulses stand still under it.</summary>
        public bool ReducedMotion { get; set; }
        public bool OwnsMenuInput => IsShowing || Time.frameCount <= dismissedFrame + 1;

        /// <summary>
        /// Whether the board is drawn over the house this frame: shown, lit, and its canvas enabled.
        /// What the house draws under it - the yard's sign, the station discs, the name plates -
        /// asks this before drawing (UI-UX-PASS-PLAN G0); a capture that switches the canvas off to
        /// photograph the yard gets the yard back.
        /// </summary>
        public bool IsDrawn => IsShowing && gameObject.activeInHierarchy
            && group != null && group.alpha > 0f && (surface == null || surface.enabled);

        /// <summary>Every board there is, so the house can ask whether any is drawn: the director's, or one a test stood up beside it.</summary>
        private static readonly List<CompetitionGameScreen> live = new List<CompetitionGameScreen>();

        /// <summary>Whether any competition board is drawn this frame.</summary>
        public static bool AnyDrawn
        {
            get
            {
                for (int i = live.Count - 1; i >= 0; i--)
                {
                    if (live[i] == null) { live.RemoveAt(i); continue; }
                    if (live[i].IsDrawn) return true;
                }
                return false;
            }
        }

        private void OnDestroy() { live.Remove(this); }

        /// <summary>
        /// Forgets every board at play's start. The list is static, and the editor can start play
        /// without reloading the domain: a board left in it from the last session must hold nothing
        /// in this one (a destroyed one already reads as gone, but a list that only grows is a leak).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetBoards() => live.Clear();

        /// <summary>
        /// A sound the screen's own beats ask for - the count, the start, the last seconds, a wave -
        /// played by whoever owns the house's audio. Only existing cues: every cue needs a recording.
        /// </summary>
        public event Action<HouseAudio.Cue> CueRequested;

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
            screen.group = root.GetComponent<CanvasGroup>(); screen.surface = canvas; screen.Hide();
            live.Add(screen);
            return screen;
        }

        /// <summary>
        /// Builds the frame for one attempt. A final Head of Household part also hands over its
        /// <paramref name="bracket"/> (MOCKUP-PASS-PLAN M13): the part tracker and the scoring line
        /// go in the challenge card and the gold band on the legend row. Every other competition
        /// passes none and is drawn as it always was.
        /// </summary>
        public void Show(MiniGameRun attempt, string title, string field, bool practice, Action<int> flip,
            Action tap, Action<MiniGameRun.Direction> direction, Action toggleGrip, Action onCancel, Action missedTarget = null,
            bool assemble = false, IList<CompetitionEntrant> entrants = null, FinalBracket bracket = null)
        {
            ClearAssembly();
            if (panel != null) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
            run = attempt; cancelAction = onCancel; playing = false; Paused = false; held = false;
            finalBracket = bracket;
            ranked = !practice; leaveArmed = false; holdKeyWasDown = false;
            previewStarted = false;
            lastGameFocus = null;
            ForgetGames();
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
                // Lighter than it was: the cards are denser glass now, so the stage can show.
                legibility.GetComponent<Image>().color=new Color(1f,1f,1f,.45f);
            }
            // The studio is a frame the cards hang in now, not a slab over the set: a transparent
            // container across the screen, still the one thing assembly hides and shows.
            panel = HudPrimitives.Fill("Competition studio", transform, new Color(0,0,0,0), 16);
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one; panel.pivot = new Vector2(.5f,.5f);
            panel.offsetMin = panel.offsetMax = Vector2.zero; panel.GetComponent<Image>().raycastTarget = false;

            // The whole frame is the competition's (the HUD stands down for the attempt): the
            // challenge and the clock across the top, the board filling the space under them with
            // the controls legend at its foot, and the field and the controls down the right.
            BuildChallenge(title, practice);
            BuildFinalPartChallenge();
            BuildTimer();

            // The board, on glass the stage shows through.
            playArea = HudPrimitives.Fill("Game surface", panel, new Color(UiTheme.GlassFill.r, UiTheme.GlassFill.g, UiTheme.GlassFill.b, BoardAlpha), 12);
            UiTheme.AddBorder(playArea, 12, UiTheme.Edge(UiTheme.Emphasis.Resting));
            controls = playArea.gameObject.AddComponent<CanvasGroup>(); controls.interactable = false;
            BuildBoardHeader();
            BuildLegend();
            BuildFinalBand();
            BuildField(field, entrants);
            BuildFooter();
            switch (run.Kind)
            {
                case CompetitionMiniGames.Kind.Memory: BuildMemory(flip); break;
                case CompetitionMiniGames.Kind.Reaction: BuildReaction(tap, direction, missedTarget); break;
                case CompetitionMiniGames.Kind.Endurance: BuildEndurance(toggleGrip); break;
                // The luck and social boards play their attempt themselves and ask for sounds by cue.
                case CompetitionMiniGames.Kind.Dice: BuildDice(); break;
                case CompetitionMiniGames.Kind.Words: BuildWords(); break;
            }
            BuildOverlay();
            BuildFinishPlate();

            screenFor = new Vector2Int(Screen.width, Screen.height);
            LayoutForFrame(CurrentFrame());
            WireFooter();
            Select(pause);
            ShowOverlay(OverlayState.Count, "");
            Canvas.ForceUpdateCanvases();
            Refresh();
            if (assemble) BeginAssembly(title);
        }

        /// <summary>Returns true only after layout and the complete ready countdown have finished.</summary>
        public bool AdvanceReady(float delta)
        {
            if (!IsShowing || IsAssembling || Paused || Time.frameCount <= shownFrame) return false;
            if (held)
            {
                // The field is ready again: the count, or the preview, picks up where it stood.
                held = false;
                if (!playing) ShowOverlay(previewStarted ? OverlayState.None : OverlayState.Count, "");
                Refresh();
            }
            if ((playing || previewStarted) && delta > .25f)
            {
                // A stalled frame must not silently play unseen targets or exhaust the player.
                TogglePause();
                SetOverlayDetail("Paused after a frame delay. Resume when ready; no attempt time was lost.");
                return false;
            }
            if (playing) return true;
            bool preview = run.Definition?.Pattern == CompetitionPattern.PreviewPairs;
            if (preview && !previewStarted)
            {
                previewStarted = true; countdownLeft = (float)run.Definition.PreviewSeconds;
                ShowOverlay(OverlayState.None, "");
                Refresh(); return false;
            }
            int before = Mathf.CeilToInt(countdownLeft);
            countdownLeft = Mathf.Max(0, countdownLeft - Mathf.Max(0,delta));
            if (countdownLeft > 0)
            {
                if (!preview) ShowOverlay(OverlayState.Count, "");
                if (Mathf.CeilToInt(countdownLeft) != before) Cue(HouseAudio.Cue.Hover);
                Refresh(); return false;
            }
            playing = true; controls.interactable = true;
            ShowOverlay(OverlayState.Go, "");
            FillLegend();
            Cue(HouseAudio.Cue.CompetitionStart);
            FocusGame(); WireFooter();
            Refresh();
            // Start on the following frame; the opening button/key never counts as gameplay.
            return false;
        }

        public void SetArenaStatus(string text) { if(arenaStatus!=null)arenaStatus.text=text??""; if(assemblyStatus!=null)assemblyStatus.text=text??""; }

        /// <summary>
        /// The arena is not ready: the count waits, a preview's faces go down and its clock stands
        /// still, and the board says what it is waiting for. Nothing once the game is being played
        /// or is over.
        /// </summary>
        public void HoldReady(string text)
        {
            if (!IsShowing || run == null || run.Finished || playing || Paused || FinishShowing) return;
            held = true;
            ShowOverlay(OverlayState.Ready, text);
            Refresh();
        }

        public void TogglePause()
        {
            if (!IsShowing || run == null || run.Finished) return;
            if (IsAssembling) { ToggleAssemblyPause(); return; }
            RememberGameFocus();
            Paused = !Paused; controls.interactable = playing && !Paused;
            pause.GetComponentInChildren<TMP_Text>().text = Paused ? "Resume" : "Pause";
            run.SetHolding(false); holdKeyWasDown = false;
            if (!Paused) leaveArmed = false;
            if (Paused)
            {
                ShowOverlay(OverlayState.Paused, "Clock stopped. " + LegendKey(LegendAction.Pause) + " resumes  ·  "
                    + LegendKey(LegendAction.Leave) + (LeavingAsks ? " asks before leaving." : " returns to the briefing."));
                Select(pause);
            }
            else if (playing) { ShowOverlay(OverlayState.None, ""); FocusGame(); }
            else if (held) ShowOverlay(OverlayState.Ready, overlayDetail != null ? overlayDetail.text : "");
            else ShowOverlay(previewStarted ? OverlayState.None : OverlayState.Count, "");
            WireFooter();
            Refresh();
        }

        private void Update()
        {
            if (!IsShowing) return;
            var keyboard = Keyboard.current; var pad = Gamepad.current;
            NoteInputDevice(keyboard, pad);
            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                || (pad != null && pad.buttonEast.wasPressedThisFrame))
            { LeaveRequested(); return; }
            // On the word board every letter spells while it is played, P among them.
            if ((keyboard != null && keyboard.pKey.wasPressedThisFrame && !(WordsBoard && IsPlaying))
                || (pad != null && pad.startButton.wasPressedThisFrame))
                TogglePause();
            else ReadWordKeys(keyboard, pad);
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
                MoveControlFocus(keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            else if (pad != null && pad.rightShoulder.wasPressedThisFrame) MoveControlFocus(false);
            else if (pad != null && pad.leftShoulder.wasPressedThisFrame) MoveControlFocus(true);
            RememberGameFocus();
            RescueFocus();
            // The board's own beats - a card turning, GO, a hit mark fading - run on the screen's
            // clock, not the attempt's: cosmetic, and still while paused.
            if (!Paused && !IsAssembling) AdvanceBeats(Time.unscaledDeltaTime);
        }

        // Esc / B once a ranked attempt is under way: the first press pauses and asks, the second
        // leaves. Leaving throws away the attempt in progress (the same board waits in the
        // briefing), which one stray key should not do. A practice, or a board not yet started,
        // leaves on the first press as it always has.
        private bool leaveArmed;

        /// <summary>Whether leaving asks first: a ranked attempt under way.</summary>
        private bool LeavingAsks => ranked && playing && run != null && !run.Finished;

        private void LeaveRequested()
        {
            // A ranked attempt that has just ended - its last pair found - is a result waiting for
            // its plate, not an attempt to leave.
            if (holdingFinish || (ranked && run != null && run.Finished && !FinishShowing)) return;
            if (LeavingAsks && !leaveArmed)
            {
                if (!Paused) TogglePause();
                leaveArmed = true;
                ShowOverlay(OverlayState.Paused, "Leave this ranked attempt? Press " + LegendKey(LegendAction.Leave)
                    + " again, or choose Back to briefing. The same board will be waiting.");
                Select(cancel);
                return;
            }
            cancelAction?.Invoke();
        }

        /// <summary>
        /// A stray click on the frame's glass deselects: with the HUD stood down for the attempt, its
        /// rescue does not run, so the game puts the keyboard and the pad back on a control of its own.
        /// </summary>
        private void RescueFocus()
        {
            var events = EventSystem.current;
            if (events == null || IsAssembling) return;
            var selected = events.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(transform) && selected.activeInHierarchy) return;
            if (finishAction != null && finishAction.gameObject.activeInHierarchy) Select(finishAction);
            else if (IsPlaying) FocusGame();
            else Select(pause.gameObject.activeInHierarchy ? pause : cancel);
        }

        /// <summary>Tab and shoulders visit the game, Pause and Back, preserving the selected card.</summary>
        public void MoveControlFocus(bool reverse)
        {
            if (!IsShowing || EventSystem.current == null) return;
            if (IsAssembling) { MoveAssemblyFocus(reverse); return; }
            RememberGameFocus();
            var selected = EventSystem.current.currentSelectedGameObject;
            bool finished = finishAction != null && finishAction.gameObject.activeSelf;
            if (finished)
            {
                if (!cancel.gameObject.activeInHierarchy) { Select(finishAction); return; }
                Select(selected == cancel.gameObject ? finishAction : cancel); return;
            }
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
            if (selectable != null && selectable != lastGameFocus && IsGameControl(selectable))
            { lastGameFocus = selectable; if (playing) WireFooter(); }
        }

        // The control the keyboard last had on the board - unless it can no longer take it, as a
        // letter tile already chosen cannot - and the game's own first control otherwise.
        private Selectable GameFocus => lastGameFocus != null && lastGameFocus.IsActive() && lastGameFocus.IsInteractable()
            ? lastGameFocus : DefaultGameFocus;

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
            RefreshTimer();
            switch(run.Kind)
            {
                case CompetitionMiniGames.Kind.Memory: RefreshMemory(); break;
                case CompetitionMiniGames.Kind.Reaction: RefreshReaction(); break;
                case CompetitionMiniGames.Kind.Endurance: RefreshEndurance(); break;
                case CompetitionMiniGames.Kind.Dice: RefreshDice(); break;
                case CompetitionMiniGames.Kind.Words: RefreshWords(); break;
            }
            RefreshBoardEdge();
        }

        public void Hide()
        {
            if (IsShowing) dismissedFrame = Time.frameCount;
            IsAssembling=false; if(assemblyPanel!=null)assemblyPanel.gameObject.SetActive(false);
            IsShowing=false; playing=false; Paused=false; held=false; holdingFinish=false;
            if(group!=null) { group.alpha=0;group.interactable=false;group.blocksRaycasts=false; }
            if(EventSystem.current!=null && EventSystem.current.currentSelectedGameObject!=null
                && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void Cue(HouseAudio.Cue cue) => CueRequested?.Invoke(cue);

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
        private static RectTransform Card(string name, Transform parent, bool right = false)
        {
            var card = HudPrimitives.Fill(name, parent, new Color(UiTheme.GlassFill.r, UiTheme.GlassFill.g, UiTheme.GlassFill.b, CardAlpha), UiTheme.GlassRadius);
            var corner = right ? new Vector2(1, 1) : new Vector2(0, 1);
            card.anchorMin = card.anchorMax = corner; card.pivot = corner;
            card.GetComponent<Image>().raycastTarget = false;
            UiTheme.AddBorder(card, UiTheme.GlassRadius, UiTheme.Edge(UiTheme.Emphasis.Resting));
            return card;
        }

        /// <summary>
        /// A board control that is never tinted. Assigning a target graphic runs one colour
        /// transition before the transition can be switched off, and on a board that is not yet live
        /// that left the graphic at the disabled half-alpha - the board's own glass among them - so
        /// the tint is cleared as well as stopped.
        /// </summary>
        private static void Untinted(Selectable control)
        {
            if (control == null) return;
            control.transition = Selectable.Transition.None;
            if (control.targetGraphic != null) control.targetGraphic.canvasRenderer.SetColor(Color.white);
        }

        /// <summary>
        /// A control whose focus is seen: an edge that steps up when the keyboard or the pad is on
        /// it. The default tint dims a selected button by four percent, which on the dark secondary
        /// sprite is nothing at all - a stray Up could land on Back to briefing unseen.
        /// </summary>
        private static void Focusable(Button button)
        {
            if (button == null) return;
            var rect = (RectTransform)button.transform;
            UiTheme.AddBorder(rect, 8, UiTheme.Edge(UiTheme.Emphasis.Interactive));
            HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
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

        /// <summary>Anchors <paramref name="rect"/> to fill its parent, less the given insets.</summary>
        private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>A plain, click-through image, sliced from a pack sprite when there is one.</summary>
        private static Image Picture(string name, Transform parent, Sprite sprite, Color tint, bool preserveAspect = true)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            image.sprite = sprite; image.color = tint; image.preserveAspect = preserveAspect; image.raycastTarget = false;
            return image;
        }
    }

    /// <summary>
    /// One houseguest in the field, as the competitors card shows them: a face and a name, and
    /// whether they are you. Nothing about how they are doing - their result does not exist until
    /// the commit, and a live figure for them would be invented.
    /// </summary>
    public sealed class CompetitionEntrant
    {
        public string Id { get; }
        public string Name { get; }
        public bool IsPlayer { get; }
        public Texture Portrait { get; }
        public ContestantState Character { get; }

        public CompetitionEntrant(string id, string name, bool isPlayer, Texture portrait, ContestantState character)
        { Id = id; Name = name; IsPlayer = isPlayer; Portrait = portrait; Character = character; }
    }
}
