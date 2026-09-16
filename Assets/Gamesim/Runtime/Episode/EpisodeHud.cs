using System;
using System.Linq;
using Gamesim.Presentation;
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
        // Palette lives in UiTheme so the HUD and the 3D set stay in step; these aliases keep
        // the existing call sites unchanged.
        private static readonly Color Ink = UiTheme.Ink;
        private static readonly Color Surface = UiTheme.Surface;
        private static readonly Color Accent = UiTheme.Accent;
        private static readonly Color Paper = UiTheme.Paper;
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
        /// <summary>Mirrors the director's accessibility preference; suppresses every HUD animation.</summary>
        public bool ReducedMotion { get; set; }
        // The canvas is rebuilt on every render, so motion is driven off genuine transitions
        // rather than off the rebuild itself.
        private bool modalWasOpen;
        private string lastStatusMessage;
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

        /// <summary>
        /// Where the panel column starts: clear of the cast rail's gutter. The ceremony card insets
        /// against this too, so it lives here rather than as a literal in two places.
        /// </summary>
        public const float LeftColumnX = 14f + CastRail.Width + 12f;

        public void Begin(EpisodeState state, string message, bool recovery, bool open)
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            preferredSelection = selected != null && modal != null && selected.transform.IsChildOf(modal)
                ? selected.name : null;
            foreach (Transform child in canvas.transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            challengeMeter = null; challengeCaption = null;
            modal = null; modalScroll = null; lastSelection = null; restoreSelection = true;
            // Brand and Objective used to be placed at hard-coded offsets, so Objective's -143
            // silently assumed Brand's exact height; growing either one overlapped them. Stacking
            // them in a column makes that impossible to get wrong.
            // The cast rail owns the far-left gutter, so the panel column starts to the right of it.
            // Six faces on screen at all times is what makes the rest of the HUD able to say "the
            // replacement nominee" and have that mean a person rather than a name.
            CastRail.Build(canvas.transform, state, FontScale, font, Portrait);

            var leftColumn = new GameObject("Left column",typeof(RectTransform),typeof(VerticalLayoutGroup),typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            leftColumn.SetParent(canvas.transform,false);
            leftColumn.anchorMin = new Vector2(0,1); leftColumn.anchorMax = new Vector2(0,1); leftColumn.pivot = new Vector2(0,1);
            leftColumn.anchoredPosition = new Vector2(LeftColumnX,-24);
            var columnLayout = leftColumn.GetComponent<VerticalLayoutGroup>();
            columnLayout.spacing = 16; columnLayout.childControlWidth = true; columnLayout.childControlHeight = true;
            columnLayout.childForceExpandWidth = false; columnLayout.childForceExpandHeight = false;
            var columnFitter = leftColumn.GetComponent<ContentSizeFitter>();
            columnFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            columnFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var brand = Chrome("Brand", leftColumn, Ink); Size(brand,330,103);
            FixedText(brand,"GAMESIM",32,Accent,new Vector2(18,-12),new Vector2(300,42));
            FixedText(brand,"THE HOUSE  /  A SIX-PERSON SEASON",14,Paper,new Vector2(19,-62),new Vector2(300,24));
            var controls = Chrome("Navigation",canvas.transform,Ink); Anchor(controls,new Vector2(1,1),new Vector2(1,1),new Vector2(-24,-24),new Vector2(465,64));
            FixedButton(controls,"Notebook [J]",new Vector2(10,-9),new Vector2(142,46),director.OpenJournal);
            FixedButton(controls,"Save [F5]",new Vector2(161,-9),new Vector2(122,46),director.SaveNow);
            FixedButton(controls,"Settings",new Vector2(292,-9),new Vector2(162,46),director.OpenSettings);
            var objective = Chrome("Objective",leftColumn,Ink); Size(objective,330,285);
            // Broadcast bug: the week reads as the headline and the phase as its strap, tied
            // together by an accent rule, the way a running TV graphic is built.
            var bug = Panel("Phase bug",objective,Accent,2); Anchor(bug,new Vector2(0,1),new Vector2(0,1),new Vector2(18,-14),new Vector2(4,52));
            bug.GetComponent<Image>().raycastTarget = false;
            FixedText(objective,"WEEK " + state.week,26,Accent,new Vector2(32,-12),new Vector2(160,30));
            FixedText(objective,EpisodeDirector.PhaseTitle(state.phase).ToUpperInvariant(),15,Paper,new Vector2(32,-42),new Vector2(262,22));
            FixedText(objective,state.pendingDiary != null ? "Next stop: private diary room" : EpisodeEngine.IsCompetition(state.phase)
                ? "Next stop: competition yard" : "Next stop: living-room screen",19,Paper,new Vector2(18,-83),new Vector2(294,47));
            FixedButton(objective,"Go to episode screen",new Vector2(18,-144),new Vector2(294,54),director.GoToStation);
            FixedButton(objective,DiaryTravelCaption,new Vector2(18,-210),new Vector2(294,54),director.GoToDiary).interactable =
                director.HasDiaryRoom && !recovery && state.Find(state.playerId)?.status == ContestantStatus.Active;
            var help = Chrome("Exploration controls",canvas.transform,Ink); Anchor(help,new Vector2(1,0),new Vector2(1,0),new Vector2(-24,100),new Vector2(285,115));
            FixedText(help,"Click floor: walk  ·  F: recenter\nWASD/arrows: camera pan\nRight-drag: orbit  ·  Wheel: zoom\nR: diary · E: interact · Esc: close",17,Paper,new Vector2(14,-12),new Vector2(258,97));
            // Spans the viewport with margins instead of assuming a 1200px width, so the caption
            // still fits when the window is narrower than the reference resolution.
            var status = Chrome("Status",canvas.transform,Ink);
            status.anchorMin = new Vector2(0,0); status.anchorMax = new Vector2(1,0); status.pivot = new Vector2(.5f,0);
            status.offsetMin = new Vector2(24,20); status.offsetMax = new Vector2(-24,84);
            if (message != lastStatusMessage) { HudReveal.Play(status,ReducedMotion,10f); lastStatusMessage = message; }
            // Lower third: a coloured rule leads the caption, and turns amber on recovery so the
            // state of the save is legible at a glance rather than only in the wording.
            var rule = Panel("Caption rule",status,recovery ? UiTheme.Warning : Accent,2);
            Anchor(rule,new Vector2(0,1),new Vector2(0,1),new Vector2(16,-12),new Vector2(5,40));
            rule.GetComponent<Image>().raycastTarget = false;
            var caption = FixedText(status,message,18,recovery ? UiTheme.Warning : Paper,new Vector2(32,-9),new Vector2(1150,48));
            Stretch(caption.rectTransform,32,9,24,7);
            var promptRoot = Chrome("Interaction prompt",canvas.transform,Ink); Anchor(promptRoot,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,107),new Vector2(425,52));
            prompt = FixedText(promptRoot,"",21,Accent,new Vector2(14,-7),new Vector2(397,39)); prompt.alignment = TextAlignmentOptions.Center;
            promptRoot.gameObject.SetActive(false);
            content = null;
            if (!open && !recovery) { modalWasOpen = false; return; }
            modal = Chrome("Episode panel",canvas.transform,Ink); Anchor(modal,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(95,-10),new Vector2(790,680));
            // Only on closed -> open. Re-renders of an already-open panel must not re-animate.
            if (!modalWasOpen) HudReveal.Play(modal,ReducedMotion);
            modalWasOpen = true;
            // The phase band. Fixed chrome rather than the first thing in the scroll, so the beat
            // the player is in stays on screen while they read past it — and coloured, because
            // "which part of the week is this" is the question every panel is answered against.
            // Built before the Close button so it sits behind it in the hierarchy.
            var band = PhaseBand(modal, state);
            FixedButton(modal,"Close  [Esc]",new Vector2(598,-15),new Vector2(174,45),director.ClosePanels);
            // LiberationSans SDF is a static atlas without U+2191/U+2193, so the arrow glyphs
            // would render as tofu. Words also read better to a screen reader.
            FixedText(modal,"Tab / Up / Down select · Enter confirm · Scroll for more",15,UiTheme.Muted,new Vector2(24,-72),new Vector2(550,26));
            var scrollRoot = new GameObject("Episode scroll",typeof(RectTransform),typeof(ScrollRect)); scrollRoot.transform.SetParent(modal,false);
            var scrollRect = (RectTransform)scrollRoot.transform; Stretch(scrollRect,20,104,20,22);
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

        /// <summary>
        /// A panel title fronted by the speaker's face.
        ///
        /// <para>The same information as <see cref="PanelTitle"/>, but a conversation is with a
        /// person and the cast rail has just taught the player which face that is. Falls back to the
        /// plain title when the persona has no authored art, so a missing portrait costs a picture
        /// rather than a header.</para>
        /// </summary>
        public void SpeakerTitle(string contestantId, string title, string subtitle)
        {
            var portrait = Portrait(contestantId);
            if (portrait == null) { PanelTitle(title, subtitle); return; }

            var row = new GameObject("Speaker",typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(content,false);
            float side = 64f * FontScale;
            row.gameObject.AddComponent<LayoutElement>().minHeight = side + 8f * FontScale;

            var frame = new GameObject("Speaker portrait",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();
            frame.SetParent(row,false);
            frame.anchorMin = new Vector2(0,1); frame.anchorMax = new Vector2(0,1); frame.pivot = new Vector2(0,1);
            frame.anchoredPosition = new Vector2(4f,0f);
            frame.sizeDelta = new Vector2(side,side);
            var disc = frame.GetComponent<Image>();
            disc.sprite = UiTheme.Circle(); disc.type = Image.Type.Simple; disc.raycastTarget = false;
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var face = new GameObject("Face",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
            face.rectTransform.SetParent(frame,false);
            face.rectTransform.anchorMin = Vector2.zero; face.rectTransform.anchorMax = Vector2.one;
            face.rectTransform.offsetMin = Vector2.zero; face.rectTransform.offsetMax = Vector2.zero;
            face.texture = portrait; face.raycastTarget = false;

            float text = side + 16f * FontScale;
            FixedText(row,title,26,Accent,new Vector2(text,-4f),new Vector2(420f * FontScale,32f * FontScale));
            FixedText(row,subtitle,21,Paper,new Vector2(text,-34f * FontScale),new Vector2(420f * FontScale,28f * FontScale));
        }

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
            var hint = NewText(rect,"What do you want the jury to remember about your game?",21,UiTheme.Muted);
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

        /// <summary>An action row fronted by a houseguest's portrait. Falls back to a plain row.</summary>
        public Button Action(string caption,Texture portrait,Action action)
        {
            if (portrait == null) return Action(caption,action);
            var rect = Panel(caption,content,Surface);
            rect.gameObject.AddComponent<LayoutElement>().minHeight = 68 * FontScale;
            var button = FinishButton(rect,caption,action,68f * FontScale);

            var frame = new GameObject("Portrait",typeof(RectTransform),typeof(RawImage));
            var frameRect = (RectTransform)frame.transform;
            frameRect.SetParent(rect,false);
            frameRect.anchorMin = new Vector2(0,.5f); frameRect.anchorMax = new Vector2(0,.5f); frameRect.pivot = new Vector2(0,.5f);
            float side = 52f * FontScale;
            frameRect.sizeDelta = new Vector2(side,side);
            frameRect.anchoredPosition = new Vector2(8f,0f);
            var raw = frame.GetComponent<RawImage>();
            raw.texture = portrait; raw.raycastTarget = false;
            return button;
        }

        public void ChoosePair(Option[] options,Action<string,string> commit,string commitCaption = "Commit nominations")
        {
            string first = null, second = null;
            // Same framing the notebook uses, so a trust number is never mistaken for fact.
            Paragraph("Trust readings are your own perspective; another housemate may feel differently.");
            var selection = FlowText("Choose two houseguests below.",21,Accent);
            foreach (var option in options)
            {
                var captured = option;
                var row = Action(option.Label,Portrait(option.Id),() =>
                {
                    if (first == captured.Id) first = null;
                    else if (second == captured.Id) second = null;
                    else if (first == null) first = captured.Id;
                    else second = captured.Id;
                    string Label(string id) => Array.Find(options,o=>o.Id==id).Label ?? "—";
                    selection.text = "Selected: " + Label(first) + " and " + Label(second);
                });
                Annotate(row, captured.Id);
            }
            Action(commitCaption,() => commit(first,second));
        }

        /// <summary>
        /// An action row about a specific houseguest, fronted by their portrait. Callers pass the
        /// contestant id rather than a texture so portrait resolution stays in one place.
        /// </summary>
        public Button ActionFor(string contestantId,string caption,Action action)
        {
            var button = Action(caption,Portrait(contestantId),action);
            Annotate(button,contestantId);
            return button;
        }

        /// <summary>
        /// Adds the player's own read of a houseguest to a decision row: their trust score and,
        /// when it applies, an alliance tag.
        ///
        /// Strictly bounded by what the character knows, matching the notebook: the score is
        /// <c>Score(player -> them)</c>, the player's own feeling, never theirs in return, and the
        /// alliance tag only covers alliances the player is actually in. Nothing here exposes
        /// NPC-to-NPC bonds, hidden blocs, or promises the player is not party to.
        /// </summary>
        private void Annotate(Button button,string contestantId)
        {
            var state = director != null ? director.Snapshot : null;
            if (state == null || string.IsNullOrEmpty(state.playerId)) return;
            if (contestantId == state.playerId || state.Find(contestantId) == null) return;

            double trust = state.Score(state.playerId, contestantId);
            bool allied = state.Allied(state.playerId, contestantId);
            var rect = (RectTransform)button.transform;
            float reserved = allied ? 150f : 96f;

            // Keep a long caption from running underneath the chips.
            var caption = button.GetComponentInChildren<TMP_Text>();
            if (caption != null)
                caption.rectTransform.offsetMax = new Vector2(-reserved, caption.rectTransform.offsetMax.y);

            if (allied)
            {
                var tag = NewText(rect,"ALLY",14,UiTheme.Gold);
                Anchor(tag.rectTransform,new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-96f,0f),new Vector2(48,22));
                tag.alignment = TextAlignmentOptions.Right;
            }

            var reading = NewText(rect,"Trust " + trust.ToString("0"),15,
                trust > 5 ? UiTheme.Accent : trust < -5 ? UiTheme.Danger : UiTheme.Muted);
            Anchor(reading.rectTransform,new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-16f,0f),new Vector2(74,22));
            reading.alignment = TextAlignmentOptions.Right;
        }

        /// <summary>
        /// Resolves a houseguest's portrait through the same persona mapping the in-world model
        /// uses. Returns null when the cast or the art is unavailable, and the row degrades to text.
        /// </summary>
        private Texture Portrait(string contestantId)
        {
            var state = director != null ? director.Snapshot : null;
            var contestant = state != null ? state.Find(contestantId) : null;
            if (contestant == null) return null;
            return CharacterPortraits.Get(
                CharacterPresentation.AppearanceId(contestant, ContentCatalog.CanonicalId(contestant.id)));
        }

        public void PathInput(string placeholder,Action<string> submit)
        {
            var rect = Panel("Import path",content,Surface); var element = rect.gameObject.AddComponent<LayoutElement>(); element.minHeight = 58;
            var input = rect.gameObject.AddComponent<TMP_InputField>(); var text = NewText(rect,"",19,Paper); Stretch(text.rectTransform,14,9,14,9);
            var hint = NewText(rect,placeholder,19,UiTheme.Muted); Stretch(hint.rectTransform,14,9,14,9);
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
        private Button FinishButton(RectTransform rect,string caption,Action action,float leftInset = 16f)
        {
            var button=rect.gameObject.AddComponent<Button>(); var colors=button.colors;
            colors.highlightedColor=new Color(1.2f,1.6f,1.45f); colors.selectedColor=colors.highlightedColor; colors.pressedColor=new Color(.65f,1.1f,.9f); button.colors=colors;
            var text=NewText(rect,caption,20,Paper); Stretch(text.rectTransform,leftInset,5,16,5); text.alignment=TextAlignmentOptions.Left;
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
        /// <summary>
        /// The colour a phase announces itself in. Deliberately the same vocabulary the ceremony
        /// takeover uses — gold for the veto, red for the block and the vote — so a player learns
        /// one palette rather than two.
        /// </summary>
        private static Color PhaseTint(EpisodePhase phase)
        {
            switch (phase)
            {
                case EpisodePhase.Nomination:
                case EpisodePhase.Eviction:
                case EpisodePhase.FinalEviction:
                    return UiTheme.Danger;
                case EpisodePhase.VetoSelection:
                case EpisodePhase.Veto:
                case EpisodePhase.VetoMeeting:
                    return UiTheme.Gold;
                case EpisodePhase.Finished:
                    return UiTheme.Gold;
                case EpisodePhase.HoH:
                case EpisodePhase.FinalHoHPart1:
                case EpisodePhase.FinalHoHPart2:
                case EpisodePhase.FinalHoHPart3:
                    return UiTheme.Accent;
                default:
                    return UiTheme.AccentDeep;
            }
        }

        /// <summary>
        /// The panel's fixed header: phase, week, and how many are left.
        ///
        /// <para>The foreground is chosen by luminance rather than fixed, because the band runs from
        /// a deep blue to gold and one hard-coded colour is unreadable at one end.</para>
        /// </summary>
        private RectTransform PhaseBand(RectTransform parent, EpisodeState state)
        {
            var tint = PhaseTint(state.phase);
            var ink = UiTheme.OnColor(tint);

            var rect = Panel("Phase band",parent,tint,UiTheme.PanelRadius);
            rect.anchorMin = new Vector2(0,1); rect.anchorMax = new Vector2(1,1); rect.pivot = new Vector2(.5f,1);
            rect.offsetMin = new Vector2(0,-62); rect.offsetMax = new Vector2(0,0);
            rect.GetComponent<Image>().raycastTarget = false;

            FixedText(rect,EpisodeDirector.PhaseTitle(state.phase).ToUpperInvariant(),21,ink,
                new Vector2(22,-9),new Vector2(540,27));
            FixedText(rect,"WEEK " + state.week + " · " + (state.phase == EpisodePhase.Finished
                    ? "Season complete"
                    : state.Active.Count() + " houseguests remain"),
                14,new Color(ink.r,ink.g,ink.b,.82f),new Vector2(22,-36),new Vector2(540,20));
            return rect;
        }

        private static RectTransform Panel(string name,Transform parent,Color color,int radius = UiTheme.ControlRadius)
        {
            var panel=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>(); panel.SetParent(parent,false);
            var image=panel.GetComponent<Image>();
            UiTheme.Style(image,color,radius); image.raycastTarget=true; return panel;
        }
        /// <summary>
        /// Major chrome: a larger corner radius plus a hairline border, so panel edges stay legible
        /// against the set's bloom instead of dissolving into it. The border never takes raycasts.
        /// </summary>
        private static RectTransform Chrome(string name,Transform parent,Color color)
        {
            var rect=Panel(name,parent,color,UiTheme.PanelRadius);
            UiTheme.AddBorder(rect,UiTheme.PanelRadius,UiTheme.Outline);
            return rect;
        }
        /// <summary>
        /// Fixes the size of a layout-group child. The group reads LayoutElement rather than
        /// sizeDelta, so both are set: the element drives layout, the sizeDelta keeps the panel's
        /// own absolutely-positioned contents correct before the first rebuild.
        /// </summary>
        private static void Size(RectTransform rect,float width,float height)
        {
            rect.sizeDelta = new Vector2(width,height);
            var element = rect.gameObject.GetComponent<LayoutElement>();
            if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width; element.preferredHeight = height;
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
