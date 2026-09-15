using System;
using System.Linq;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>Native screen-space UI with scrollable content and keyboard-selectable controls.</summary>
    public sealed class EpisodeHud : MonoBehaviour
    {
        public const string JuryContinueCaption = "Continue jury questioning";
        public const string JurySkipCaption = "Skip remaining questions";
        public const string SpeechSubmitCaption = "Submit final speech";
        public const string SpeechSkipCaption = "Skip my final speech";
        public const string SpeechContinueCaption = "Continue to jury voting";
        public const string DiaryTravelCaption = "Go to diary room [R]";
        public const string DiaryReviewNominationsCaption = "Review nominations";
        public const string DiaryConfirmCaption = "Confirm diary decision";
        public const string DiaryCancelCaption = "Back to diary (discard choice)";
        public const string DiarySkipReflectionCaption = "Skip this reflection";
        public const string DiaryVisitReflectionCaption = "Visit diary room to answer";
        public const string DiaryConfirmReflectionCaption = "Confirm private reflection";
        public const string DiaryCancelReflectionCaption = "Back to reflection (discard answer)";
        public const string OathDeclareCaption = "Declare my loyalty";
        public const string OathDeclineCaption = "Pass on this loyalty declaration";
        public const string StudyMemorizeCaption = "Memorize the layout · review";
        public const string StudySneakCaption = "Sneak a peek at production notes · review";
        public const string StudyConfirmCaption = "Confirm study · use 1 social action";
        public const string StudyCancelCaption = "Back to diary (discard study)";
        public const string SimulateCompetitionCaption = "Simulate competition · weighted rules";
        private static readonly Color Ink = new Color(.035f,.055f,.085f,.98f);
        private static readonly Color Surface = new Color(.085f,.13f,.18f,.98f);
        private static readonly Color Accent = new Color(.5f,.93f,.78f,1);
        private static readonly Color Paper = new Color(.95f,.96f,.98f,1);
        private EpisodeDirector director;
        private Canvas canvas;
        private TMP_FontAsset font;
        private RectTransform content;
        private TMP_Text prompt, challengeCaption;
        private Slider challengeMeter;
        private RectTransform modal;
        private ScrollRect modalScroll;
        private string preferredSelection, retainedImportPath = "";
        private string retainedSpeech = "", speechSession, speechSpeaker;
        private bool restoreSelection;
        private GameObject lastSelection;
        public float FontScale { get; set; } = 1;
        public bool IsTyping => EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;

        public readonly struct Option
        {
            public readonly string Id, Label;
            public Option(string id, string label) { Id = id; Label = label; }
        }

        public void Initialize(EpisodeDirector owner)
        {
            director = owner;
            // TMP_Settings carries the imported default; the explicit load is the fallback if a
            // project ever ships without TMP Essential Resources.
            font = TMP_Settings.defaultFontAsset != null
                ? TMP_Settings.defaultFontAsset
                : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            var root = new GameObject("Gamesim Episode HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false); canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 70;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600,900); scaler.matchWidthOrHeight = .5f;
        }

        public void Begin(EpisodeState state, string message, bool recovery, bool open)
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            preferredSelection = selected != null && modal != null && selected.transform.IsChildOf(modal)
                ? selected.name : null;
            foreach (Transform child in canvas.transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            challengeMeter = null; challengeCaption = null;
            modal = null; modalScroll = null; lastSelection = null; restoreSelection = true;
            var brand = Panel("Brand", canvas.transform, Ink); Anchor(brand,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-24),new Vector2(330,103));
            FixedText(brand,"GAMESIM",32,Accent,new Vector2(18,-12),new Vector2(300,42));
            FixedText(brand,"THE HOUSE  /  A SIX-PERSON SEASON",14,Paper,new Vector2(19,-62),new Vector2(300,24));
            var controls = Panel("Navigation",canvas.transform,Ink); Anchor(controls,new Vector2(1,1),new Vector2(1,1),new Vector2(-24,-24),new Vector2(465,64));
            FixedButton(controls,"Notebook [J]",new Vector2(10,-9),new Vector2(142,46),director.OpenJournal);
            FixedButton(controls,"Save [F5]",new Vector2(161,-9),new Vector2(122,46),director.SaveNow);
            FixedButton(controls,"Settings",new Vector2(292,-9),new Vector2(162,46),director.OpenSettings);
            var objective = Panel("Objective",canvas.transform,Ink); Anchor(objective,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-143),new Vector2(330,285));
            FixedText(objective,"WEEK " + state.week + " · " + EpisodeDirector.PhaseTitle(state.phase),21,Accent,new Vector2(18,-14),new Vector2(294,65));
            FixedText(objective,state.pendingDiary != null ? "Next stop: private diary room" : EpisodeEngine.IsCompetition(state.phase)
                ? "Next stop: competition yard" : "Next stop: living-room screen",19,Paper,new Vector2(18,-83),new Vector2(294,47));
            FixedButton(objective,"Go to episode screen",new Vector2(18,-144),new Vector2(294,54),director.GoToStation);
            FixedButton(objective,DiaryTravelCaption,new Vector2(18,-210),new Vector2(294,54),director.GoToDiary).interactable =
                director.HasDiaryRoom && !recovery && state.Find(state.playerId)?.status == ContestantStatus.Active;
            var help = Panel("Exploration controls",canvas.transform,Ink); Anchor(help,new Vector2(1,0),new Vector2(1,0),new Vector2(-24,100),new Vector2(285,115));
            FixedText(help,"Click floor: walk  ·  F: recenter\nWASD/arrows: camera pan\nRight-drag: orbit  ·  Wheel: zoom\nR: diary · E: interact · Esc: close",17,Paper,new Vector2(14,-12),new Vector2(258,97));
            var status = Panel("Status",canvas.transform,Ink); Anchor(status,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,20),new Vector2(1200,64));
            FixedText(status,message,18,recovery ? new Color(1,.77f,.45f) : Paper,new Vector2(18,-9),new Vector2(1164,48));
            var promptRoot = Panel("Interaction prompt",canvas.transform,Ink); Anchor(promptRoot,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,107),new Vector2(425,52));
            prompt = FixedText(promptRoot,"",21,Accent,new Vector2(14,-7),new Vector2(397,39)); prompt.alignment = TextAlignmentOptions.Center;
            promptRoot.gameObject.SetActive(false);
            content = null;
            if (!open && !recovery) return;
            modal = Panel("Episode panel",canvas.transform,Ink); Anchor(modal,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(95,-10),new Vector2(790,680));
            FixedButton(modal,"Close  [Esc]",new Vector2(598,-15),new Vector2(174,45),director.ClosePanels);
            // LiberationSans SDF is a static atlas without U+2191/U+2193, so the arrow glyphs
            // would render as tofu. Words also read better to a screen reader.
            FixedText(modal,"Tab / Up / Down select · Enter confirm · Scroll for more",15,Paper,new Vector2(24,-28),new Vector2(550,30));
            var scrollRoot = new GameObject("Episode scroll",typeof(RectTransform),typeof(ScrollRect)); scrollRoot.transform.SetParent(modal,false);
            var scrollRect = (RectTransform)scrollRoot.transform; Stretch(scrollRect,20,75,20,22);
            var viewport = Panel("Viewport",scrollRect,new Color(0,0,0,0)); Stretch(viewport,0,0,18,0); viewport.gameObject.AddComponent<RectMask2D>();
            content = new GameObject("Episode content",typeof(RectTransform),typeof(VerticalLayoutGroup),typeof(ContentSizeFitter)).GetComponent<RectTransform>(); content.SetParent(viewport,false);
            content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f,1); content.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(8,8,6,18); layout.spacing = 12;
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = scrollRoot.GetComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
            scroll.scrollSensitivity = 30; scroll.movementType = ScrollRect.MovementType.Clamped;
            var track = Panel("Episode scrollbar",scrollRect,Surface);
            track.anchorMin = new Vector2(1,0); track.anchorMax = Vector2.one; track.pivot = new Vector2(1,.5f); track.sizeDelta = new Vector2(10,0);
            track.anchoredPosition = Vector2.zero;
            var handle = Panel("Scroll handle",track,Accent); Stretch(handle,0,0,0,0);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            modalScroll = scroll;
        }

        public void PanelTitle(string title, string subtitle) { Heading(title); Paragraph(subtitle); }
        public void Heading(string value) { FlowText(value,26,Accent); }
        public void Paragraph(string value) { FlowText(value,21,Paper); }

        public void NpcDialogue(EpisodeState state, string npcId, EpisodeCommandKind? acceptedAction = null)
        {
            var npc = state.Find(npcId) ?? (npcId == "maya" ? state.Find(ContentCatalog.MayaId) : null);
            if (npc == null) return;
            string line = acceptedAction.HasValue
                ? HouseDialogue.Response(state, npc.id, acceptedAction.Value)
                : HouseDialogue.Greeting(state, npc.id);
            if (!string.IsNullOrEmpty(line)) FlowText("\"" + line + "\"",21,Paper).gameObject.name = "NPC spoken dialogue";
        }

        public void JuryQuestioning(EpisodeState state)
        {
            Paragraph("Public questions and recorded answers. A response is not a guaranteed jury vote.");
            if (state.juryExchanges == null || state.juryQuestionIndex < 0 || state.juryQuestionIndex >= state.juryExchanges.Count)
            {
                Paragraph("The questions are complete. Continue to the final speeches.");
                Action(JuryContinueCaption,director.ContinueEpisode);
                return;
            }
            var exchange = state.juryExchanges[state.juryQuestionIndex];
            var questioner = state.Find(exchange.questionerId);
            var finalist = state.Find(exchange.finalistId);
            int questionCount = state.Active.Any(actor => actor.isPlayer)
                ? state.contestants.Count(actor => actor.status == ContestantStatus.Jury || actor.status == ContestantStatus.Evicted)
                : state.Active.Count();
            Heading("Question " + (state.juryQuestionIndex + 1) + " of " + questionCount);
            Paragraph((questioner?.name ?? "Juror") + " asks " + (finalist?.name ?? "Finalist"));
            if (!string.IsNullOrEmpty(exchange.question))
                FlowText(exchange.question,23,Accent).gameObject.name = "Jury question";
            if (exchange.completed)
            {
                if (!string.IsNullOrEmpty(exchange.answer))
                {
                    Heading(finalist?.name ?? "Finalist");
                    FlowText(exchange.answer,21,Paper).gameObject.name = "Jury answer";
                }
                if (!string.IsNullOrEmpty(exchange.opponentAnswer))
                {
                    var opponent = state.Active.FirstOrDefault(actor => actor.id != exchange.finalistId);
                    Heading(opponent?.name ?? "Other finalist");
                    FlowText(exchange.opponentAnswer,21,Paper).gameObject.name = "Other finalist answer";
                }
                Action(JuryContinueCaption,director.ContinueEpisode);
            }
            else if (exchange.questionerId == state.playerId)
            {
                Paragraph("Choose the question you want to put to this finalist.");
                foreach (var option in WebJuryQuestioning.GetJurorQuestionOptions(state.juryQuestionIndex))
                {
                    string tone = option.tone;
                    Action(tone + " · " + option.text,() => director.AnswerJury(tone));
                }
            }
            else if (exchange.finalistId == state.playerId)
            {
                Paragraph("Choose your answer. Only the committed response changes the record.");
                Action("A · " + exchange.optionA,() => director.AnswerJury("A"));
                Action("B · " + exchange.optionB,() => director.AnswerJury("B"));
            }
            else
            {
                Paragraph("The finalists are ready to answer this question.");
                Action(JuryContinueCaption,director.ContinueEpisode);
            }
            Action(JurySkipCaption,director.SkipQuestioning);
        }

        public void FinalSpeech(EpisodeState state)
        {
            Paragraph("The finalists make their final case before the jury votes.");
            if (speechSession != state.sessionId || speechSpeaker != state.playerId)
            {
                speechSession = state.sessionId; speechSpeaker = state.playerId; retainedSpeech = "";
            }
            foreach (var speech in state.finalSpeeches)
            {
                Heading(state.Find(speech.speakerId)?.name ?? "Finalist");
                Paragraph(string.IsNullOrWhiteSpace(speech.text) ? "No final speech was given." : speech.text);
            }
            bool playerFinalist = state.Find(state.playerId)?.status == ContestantStatus.Active;
            bool playerSpoke = state.finalSpeeches.Any(speech => speech.speakerId == state.playerId);
            if (!playerFinalist || playerSpoke)
            {
                retainedSpeech = "";
                Action(SpeechContinueCaption,director.ContinueEpisode);
                return;
            }
            Paragraph("Write your final speech, or skip it. Your speech becomes part of the saved record; no jury result is promised.");
            Paragraph("Up to 2,000 characters. Enter adds a line; Tab or Shift+Tab moves to another control.");
            var rect = Panel("Final speech draft",content,Surface);
            rect.gameObject.AddComponent<LayoutElement>().minHeight = 210 * FontScale;
            var input = rect.gameObject.AddComponent<EpisodeSpeechInputField>();
            var text = NewText(rect,"",21,Paper); Stretch(text.rectTransform,14,12,14,12);
            text.alignment = TextAlignmentOptions.TopLeft;
            var hint = NewText(rect,"What do you want the jury to remember about your game?",21,new Color(.6f,.7f,.75f));
            Stretch(hint.rectTransform,14,12,14,12);
            input.textComponent = text; input.placeholder = hint;
            input.characterLimit = 2000; input.lineType = TMP_InputField.LineType.MultiLineNewline;
            input.onValidateInput = (value,index,character) => character == '\t' ? '\0' : character;
            input.customCaretColor = true; input.caretColor = Accent;
            input.selectionColor = new Color(Accent.r,Accent.g,Accent.b,.3f);
            input.text = retainedSpeech;
            var count = FlowText(retainedSpeech.Length + " / 2000 characters",17,Paper);
            count.gameObject.name = "Final speech character count";
            input.onValueChanged.AddListener(value => { retainedSpeech = value; count.text = value.Length + " / 2000 characters"; });
            Action(SpeechSubmitCaption,() => director.SubmitSpeech(input.text));
            Action(SpeechSkipCaption,() => director.SubmitSpeech(""));
        }
        private TMP_Text FlowText(string value,int size,Color color)
        {
            var text = NewText(content,value,size,color); var element = text.gameObject.AddComponent<LayoutElement>(); element.minHeight = size * FontScale + 8;
            return text;
        }

        public Button Action(string caption,Action action)
        {
            var rect = Panel(caption,content,Surface); var element = rect.gameObject.AddComponent<LayoutElement>(); element.minHeight = 57 * FontScale;
            return FinishButton(rect,caption,action);
        }

        public void ChoosePair(Option[] options,Action<string,string> commit,string commitCaption = "Commit nominations")
        {
            string first = null, second = null;
            var selection = FlowText("Choose two houseguests below.",21,Accent);
            foreach (var option in options)
            {
                var captured = option;
                Action(option.Label,() =>
                {
                    if (first == captured.Id) first = null;
                    else if (second == captured.Id) second = null;
                    else if (first == null) first = captured.Id;
                    else second = captured.Id;
                    string Label(string id) => Array.Find(options,o=>o.Id==id).Label ?? "—";
                    selection.text = "Selected: " + Label(first) + " and " + Label(second);
                });
            }
            Action(commitCaption,() => commit(first,second));
        }

        public void PathInput(string placeholder,Action<string> submit)
        {
            var rect = Panel("Import path",content,Surface); var element = rect.gameObject.AddComponent<LayoutElement>(); element.minHeight = 58;
            var input = rect.gameObject.AddComponent<TMP_InputField>(); var text = NewText(rect,"",19,Paper); Stretch(text.rectTransform,14,9,14,9);
            var hint = NewText(rect,placeholder,19,new Color(.6f,.7f,.75f)); Stretch(hint.rectTransform,14,9,14,9);
            input.textComponent = text; input.placeholder = hint; input.characterLimit = 1024; input.lineType = TMP_InputField.LineType.SingleLine;
            input.text = retainedImportPath;
            input.onValueChanged.AddListener(value => retainedImportPath = value);
            Action("Archive and import this file",() => submit(input.text));
        }

        public void ChallengeMeter()
        {
            var rect = Panel("Precision meter",content,Surface); rect.gameObject.AddComponent<LayoutElement>().minHeight = 58;
            challengeMeter = rect.gameObject.AddComponent<Slider>(); challengeMeter.interactable = false; challengeMeter.minValue = 0; challengeMeter.maxValue = 1;
            var target = Panel("Center target",rect,new Color(.28f,.6f,.47f)); Anchor(target,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(90,48));
            var slideArea = new GameObject("Handle area",typeof(RectTransform)).GetComponent<RectTransform>(); slideArea.SetParent(rect,false); Stretch(slideArea,10,6,10,6);
            var handle = Panel("Marker",slideArea,Accent); handle.sizeDelta = new Vector2(12,0); challengeMeter.handleRect = handle;
            challengeCaption = FlowText("Attempt 1 of 3",21,Accent);
        }
        public void SetChallenge(float value,int hits)
        { if(challengeMeter!=null) challengeMeter.value=value; if(challengeCaption!=null) challengeCaption.text="Attempt " + (hits+1) + " of 3 · Aim for the center"; }
        public void SetPrompt(string value) { if(prompt==null)return; prompt.text=value; prompt.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(value)); }
        public void SetVisible(bool value) { if(canvas!=null) canvas.gameObject.SetActive(value); }
        private void OnDestroy() { if(canvas!=null) Destroy(canvas.gameObject); }

        private void LateUpdate()
        {
            var events = EventSystem.current;
            if (canvas == null || !canvas.gameObject.activeInHierarchy || events == null) return;
            if (restoreSelection)
            {
                Canvas.ForceUpdateCanvases();
                if (content != null)
                {
                    // Authored jury options may wrap to several lines, especially with large
                    // text. Grow action rows after their final width is known; never clip choices.
                    foreach (var button in content.GetComponentsInChildren<Button>())
                    {
                        var label = button.GetComponentInChildren<TMP_Text>();
                        var element = button.GetComponent<LayoutElement>();
                        if (label != null && element != null)
                            element.preferredHeight = Mathf.Max(element.minHeight,label.preferredHeight + 14f);
                    }
                    Canvas.ForceUpdateCanvases();
                }
                var all = canvas.GetComponentsInChildren<Selectable>().Where(item => item.IsActive() && item.IsInteractable()).ToArray();
                var eligible = modal == null ? all : all.Where(item => item.transform.IsChildOf(modal)).ToArray();
                foreach (var item in all)
                {
                    var navigation = item.navigation;
                    navigation.mode = modal != null && !item.transform.IsChildOf(modal) ? Navigation.Mode.None : Navigation.Mode.Explicit;
                    navigation.selectOnLeft = null; navigation.selectOnRight = null;
                    navigation.selectOnUp = null; navigation.selectOnDown = null;
                    item.navigation = navigation;
                }
                for (var index = 0; index < eligible.Length; index++)
                {
                    var navigation = eligible[index].navigation;
                    navigation.selectOnUp = eligible[(index + eligible.Length - 1) % eligible.Length];
                    navigation.selectOnDown = eligible[(index + 1) % eligible.Length];
                    eligible[index].navigation = navigation;
                }
                var focus = eligible.FirstOrDefault(item => item.name == preferredSelection)
                    ?? eligible.FirstOrDefault(item => content != null && item.transform.IsChildOf(content))
                    ?? eligible.FirstOrDefault(item => item.name == "Go to episode screen")
                    ?? eligible.FirstOrDefault();
                events.SetSelectedGameObject(focus != null ? focus.gameObject : null);
                restoreSelection = false;
            }

            var selected = events.currentSelectedGameObject;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame && selected != null)
            {
                var current = selected.GetComponent<Selectable>();
                var next = current == null ? null : keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed
                    ? current.navigation.selectOnUp : current.navigation.selectOnDown;
                if (next != null && next.IsActive() && next.IsInteractable())
                {
                    selected.GetComponent<TMP_InputField>()?.DeactivateInputField();
                    events.SetSelectedGameObject(next.gameObject);
                    selected = next.gameObject;
                }
            }
            if (modal != null && (selected == null || !selected.transform.IsChildOf(modal)))
            {
                var focus = content.GetComponentsInChildren<Selectable>().FirstOrDefault(item => item.IsActive() && item.IsInteractable())
                    ?? modal.GetComponentsInChildren<Selectable>().FirstOrDefault(item => item.IsActive() && item.IsInteractable());
                events.SetSelectedGameObject(focus != null ? focus.gameObject : null);
                selected = events.currentSelectedGameObject;
            }
            if (selected != lastSelection)
            {
                lastSelection = selected;
                if (selected != null && content != null && modalScroll != null && selected.transform.IsChildOf(content))
                    RevealSelection(selected.transform);
            }
        }

        private void RevealSelection(Transform selected)
        {
            Canvas.ForceUpdateCanvases();
            var viewport = modalScroll.viewport;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selected);
            var offset = bounds.min.y < viewport.rect.yMin ? viewport.rect.yMin - bounds.min.y
                : bounds.max.y > viewport.rect.yMax ? viewport.rect.yMax - bounds.max.y : 0f;
            if (Mathf.Abs(offset) < 0.01f) return;
            var position = content.anchoredPosition;
            position.y = Mathf.Clamp(position.y + offset, 0f, Mathf.Max(0f, content.rect.height - viewport.rect.height));
            modalScroll.StopMovement();
            content.anchoredPosition = position;
        }

        private Button FixedButton(RectTransform parent,string caption,Vector2 position,Vector2 size,Action action)
        {
            var rect=Panel(caption,parent,Surface); Anchor(rect,new Vector2(0,1),new Vector2(0,1),position,size);
            var button = FinishButton(rect,caption,action);
            var label = button.GetComponentInChildren<TMP_Text>();
            AutoSize(label, 18);
            return button;
        }
        private Button FinishButton(RectTransform rect,string caption,Action action)
        {
            var button=rect.gameObject.AddComponent<Button>(); var colors=button.colors;
            colors.highlightedColor=new Color(1.2f,1.6f,1.45f); colors.selectedColor=colors.highlightedColor; colors.pressedColor=new Color(.65f,1.1f,.9f); button.colors=colors;
            var text=NewText(rect,caption,20,Paper); Stretch(text.rectTransform,16,5,16,5); text.alignment=TextAlignmentOptions.Left;
            button.onClick.AddListener(()=>action()); return button;
        }
        private TMP_Text FixedText(RectTransform parent,string value,int size,Color color,Vector2 position,Vector2 dimensions)
        {
            var text=NewText(parent,value,size,color); Anchor(text.rectTransform,new Vector2(0,1),new Vector2(0,1),position,dimensions);
            // Fixed chrome fits its bounds; scrollable panel copy keeps the full requested font scale.
            AutoSize(text, size);
            return text;
        }
        /// <summary>TMP's auto-sizing replaces legacy best-fit; the floor keeps small chrome readable.</summary>
        private static void AutoSize(TMP_Text text,float floor)
        {
            if (text == null) return;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Min(floor, text.fontSize);
            text.fontSizeMax = text.fontSize;
        }
        private TMP_Text NewText(Transform parent,string value,int size,Color color)
        {
            var text=new GameObject("Text",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>(); text.transform.SetParent(parent,false);
            text.font=font;
            // Rounded so the scaled size stays an exact integer, which the HUD scaling tests assert.
            text.fontSize=Mathf.RoundToInt(size*FontScale); text.color=color; text.text=value; text.richText=false; text.raycastTarget=false;
            text.textWrappingMode=TextWrappingModes.Normal; text.overflowMode=TextOverflowModes.Truncate; return text;
        }
        private static RectTransform Panel(string name,Transform parent,Color color)
        {
            var panel=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>(); panel.SetParent(parent,false);
            panel.GetComponent<Image>().color=color; panel.GetComponent<Image>().raycastTarget=true; return panel;
        }
        private static void Anchor(RectTransform rect,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
        { rect.anchorMin=anchor; rect.anchorMax=anchor; rect.pivot=pivot; rect.anchoredPosition=position; rect.sizeDelta=size; }
        private static void Stretch(RectTransform rect,float left,float top,float right,float bottom)
        { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=new Vector2(left,bottom); rect.offsetMax=new Vector2(-right,-top); }
    }

    /// <summary>
    /// Runtime-created speech field. The director owns Escape and the HUD owns Tab; do not let
    /// uGUI's default Escape rollback discard the retained draft or Tab insert a literal tab.
    /// </summary>
    public sealed class EpisodeSpeechInputField : TMP_InputField
    {
        public override void OnUpdateSelected(BaseEventData eventData)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame))
            {
                eventData.Use();
                return;
            }
            base.OnUpdateSelected(eventData);
        }
    }
}
