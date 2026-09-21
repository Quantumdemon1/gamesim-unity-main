using System;
using System.Collections.Generic;
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
    /// <remarks>
    /// The fixed chrome the mockups call a top bar and a right column lives in
    /// <c>EpisodeHud.Chrome.cs</c>, and the conversation dial in <c>EpisodeHud.Radial.cs</c>; both
    /// are partials of this class because they need its private drawing helpers and its localisation
    /// sink, not a second set of them.
    /// </remarks>
    public sealed partial class EpisodeHud : MonoBehaviour
    {
        public const string JuryContinueCaption = "Continue jury questioning";
        public const string JurySkipCaption = "Skip remaining questions";
        public const string SpeechSubmitCaption = "Submit final speech";
        public const string EvictionSpeechCaption = "Deliver your speech";
        public const string EvictionSpeechSkipCaption = "Say nothing";
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
        /// <summary>
        /// Throwing is a real strategic option in the reference build, sitting beside playing and
        /// simulating. It is a competition entered at the floor rather than a refusal to enter.
        /// </summary>
        public const string ThrowCompetitionCaption = "Throw this competition on purpose";
        /// <summary>
        /// The words on a deal control. Captions are a contract — tests and screen readers find a
        /// control by what it says — so the deal type is spelled out here once and the chance is
        /// drawn as a tag beside the button rather than appended to it.
        /// </summary>
        public const string DealAcceptCaption = "Accept this offer";
        public const string DealDeclineCaption = "Turn this offer down";
        public static string DealProposeCaption(string title) => "Propose a " + title;
        /// <summary>The words on a week-review control, one per week the notebook lists.</summary>
        public static string ReviewWeekCaption(int week) => "Read the week " + week + " recap";
        /// <summary>
        /// The five ways of having a conversation, and the two house-wide moves.
        ///
        /// <para>Each says what it is for rather than what it is called, because the difference
        /// between them is the whole point: small talk is safe and slight, a secret is the largest
        /// swing in the game in either direction.</para>
        /// </summary>
        public const string SmallTalkCaption = "Make small talk";
        public const string PersonalChatCaption = "Tell them something personal";
        public const string RelationshipBuildingCaption = "Spend real time with them";
        public const string StrategicDiscussionCaption = "Talk tactics";
        public const string DiscussGameCaption = "Talk game openly";
        public const string ShareSecretCaption = "Trust them with a secret";
        public static string WhisperCaption(string about) => "Whisper about " + about;
        public static string CalloutCaption(string about) => "Call " + about + " out publicly";
        public const string RallyHouseCaption = "Call a house meeting and rally the room";
        public const string AirLaundryCaption = "Call a house meeting and air everything";
        /// <summary>Buying a turn, which says what it costs before it is pressed.</summary>
        public const string BuyBurnOneCaption = "Buy an action by burning one bridge";
        public const string BuySpreadCaption = "Buy an action at the whole house's expense";

        /// <summary>
        /// The words on one way of answering a situation.
        ///
        /// <para>The option's own label, unchanged. It comes from the save, and a screen that
        /// decorated it would be showing something other than what the engine will match against.
        /// </para>
        /// </summary>
        public static string EventChoiceCaption(string label) => label;

        /// <summary>
        /// How far a choice could rebound, in a word.
        ///
        /// <para>Said rather than only coloured: a warning that exists only as a shade of red is a
        /// warning some players never receive.</para>
        /// </summary>
        public static string RiskTag(string risk) =>
            risk == HouseEventRisk.High ? "high risk"
            : risk == HouseEventRisk.Medium ? "some risk"
            : "low risk";

        /// <summary>The words on each competition minigame's controls.</summary>
        public const string HoldGripCaption = "Hold on  [hold Space]";
        public const string ReleaseGripCaption = "Let go  [release Space]";
        public const string TapTargetCaption = "Hit the target  [Space]";
        /// <summary>
        /// A memory card, named by what it shows once it is face up.
        ///
        /// <para>In words, not only in colour: this screen is the only place the board exists, so a
        /// player reading it aloud has no other way to know what they just turned over.</para>
        /// </summary>
        public static string CardCaption(int index, string face) =>
            "Card " + (index + 1) + (face == null ? "" : ": " + face);
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
            // Whatever had the keyboard keeps it by name, chrome as well as panel. The HUD rebuilds
            // itself for reasons that have nothing to do with the player - a houseguest's body
            // finishing its assembly is one - and a rebuild that moved the focus off the control
            // someone was about to press lost the press with it.
            preferredSelection = selected != null && canvas != null && selected.transform.IsChildOf(canvas.transform)
                ? selected.name : null;
            // A panel that is closing fades out for a few frames instead of vanishing. The old
            // modal goes to a ghost canvas that owns no controls (HudFade strips them), so the
            // rebuild below can throw the rest away as it always has.
            if (modal != null && !open && !recovery) HudFade.Ghost(modal, canvas, ReducedMotion);
            // UI foley: a panel arriving or leaving says so. A rebuild of an open panel is neither.
            if (open && modal == null) Foley(HouseAudio.Cue.PanelOpen);
            else if (!open && modal != null && !recovery) Foley(HouseAudio.Cue.PanelClose);
            foreach (Transform child in canvas.transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            challengeMeter = null; challengeCaption = null;
            modal = null; modalScroll = null; lastSelection = null; restoreSelection = true;
            activityLayout = ActivityLayout.Standard; relationshipRoot = null;
            // The dial belongs to the panel that was just thrown away; a stale one would seat the
            // next screen's petals on a destroyed rectangle.
            dialRoot = null; topicSeats = topicTaken = 0;
            // Brand and Objective used to be placed at hard-coded offsets, so Objective's -143
            // silently assumed Brand's exact height; growing either one overlapped them. Stacking
            // them in a column makes that impossible to get wrong.
            // The cast rail owns the far-left gutter, so the panel column starts to the right of it.
            // Six faces on screen at all times is what makes the rest of the HUD able to say "the
            // replacement nominee" and have that mean a person rather than a name.
            CastRail.Build(canvas.transform, state, FontScale, font, Portrait, director.FollowHouseguest);
            FollowChip(director.FollowedName);

            var leftColumn = new GameObject("Left column",typeof(RectTransform),typeof(VerticalLayoutGroup),typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            leftColumn.SetParent(canvas.transform,false);
            leftColumn.anchorMin = new Vector2(0,1); leftColumn.anchorMax = new Vector2(0,1); leftColumn.pivot = new Vector2(0,1);
            leftColumn.anchoredPosition = new Vector2(LeftColumnX,-TopBarTop);
            var columnLayout = leftColumn.GetComponent<VerticalLayoutGroup>();
            columnLayout.spacing = TopBarGap; columnLayout.childControlWidth = true; columnLayout.childControlHeight = true;
            columnLayout.childForceExpandWidth = false; columnLayout.childForceExpandHeight = false;
            var columnFitter = leftColumn.GetComponent<ContentSizeFitter>();
            columnFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            columnFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BrandCard(leftColumn, state);
            var controls = Chrome("Navigation",canvas.transform); Anchor(controls,new Vector2(1,1),new Vector2(1,1),new Vector2(-24,-TopBarTop),new Vector2(465,TopBarHeight));
            FixedButton(controls,"Notebook [J]",new Vector2(10,-9),new Vector2(142,46),director.OpenJournal);
            FixedButton(controls,"Save [F5]",new Vector2(161,-9),new Vector2(122,46),director.SaveNow);
            FixedButton(controls,"Settings",new Vector2(292,-9),new Vector2(162,46),director.OpenSettings);
            ObjectiveCard(leftColumn, state, recovery);
            if(!Compact)HouseVibeCard(leftColumn, state);
            HousePill(state);

            // The section rail. Four views that currently share one long scroll, and the overview,
            // which is a camera mode rather than a page: the director routes its section name.
            IconRail.Build(canvas.transform, new[]
            {
                new IconRail.Entry(IconRail.Mark.Network,  EpisodeDirector.NotebookSection.Network, "Relationships"),
                new IconRail.Entry(IconRail.Mark.Rooms,    EpisodeDirector.NotebookSection.Rooms,   "Who is where"),
                new IconRail.Entry(IconRail.Mark.Votes,    EpisodeDirector.NotebookSection.Votes,   "The vote"),
                new IconRail.Entry(IconRail.Mark.Story,    EpisodeDirector.NotebookSection.Story,   "The story so far"),
                new IconRail.Entry(IconRail.Mark.Overview, EpisodeDirector.OverviewSection,         "Overview"),
            }, FontScale, director.ShowNotebookSection);
            RightColumn(state);

            // Five lines, not four, because click-to-follow had to be added without lengthening a
            // line: this box is a fixed 258 wide and the accessibility suite fails any copy that
            // clips at either text size. Thirty-five characters is the proven ceiling — a forty
            // character line is what broke it — so the panel grew downward instead. It stays in the
            // lower right, well clear of the ceremony banner that must not overlap the chrome.
            if (open || Compact) helpExpanded = false;
            BuildExplorationHelp();
            // Spans the viewport with margins instead of assuming a 1200px width, so the caption
            // still fits when the window is narrower than the reference resolution.
            var status = Chrome("Status",canvas.transform);
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
            var promptRoot = Chrome("Interaction prompt",canvas.transform); Anchor(promptRoot,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,107),new Vector2(425,52));
            prompt = FixedText(promptRoot,"",21,Accent,new Vector2(14,-7),new Vector2(397,39)); prompt.alignment = TextAlignmentOptions.Center;
            promptRoot.gameObject.SetActive(false);
            content = null;
            if (!open && !recovery) { modalWasOpen = false; return; }
            // Low and wide, not centred (VISUAL-TARGET.md V2, mockup-04). The panel used to be a
            // 790x680 block in the middle of the screen, which covered 63% of the frame's height
            // and put the house - the thing every one of these decisions is about - behind it. The
            // mockups never do that: the set fills the frame and the decision is a wide, short card
            // low in it, above the cast strip. Docking it here costs nothing but a reflow and is
            // the single change that most affects how every screen reads.
            //
            // Every number here is in the canvas's own 1600x900 reference, not in pixels. A lift of
            // 170 clears the bottom chrome: the status band owns y 20..84 and the interaction prompt
            // y 107..159. 900 wide centres on x 350..1250, which stops short of the exploration
            // controls at x 1291 and is narrower on the right than the old centred panel was.
            modal = Chrome("Episode panel",canvas.transform); Anchor(modal,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,ModalLift),new Vector2(ModalWidth,ModalHeight));
            // Only on closed -> open. Re-renders of an already-open panel must not re-animate.
            if (!modalWasOpen) HudReveal.Play(modal,ReducedMotion);
            modalWasOpen = true;
            // The phase band. Fixed chrome rather than the first thing in the scroll, so the beat
            // the player is in stays on screen while they read past it — and coloured, because
            // "which part of the week is this" is the question every panel is answered against.
            // Built before the Close button so it sits behind it in the hierarchy.
            var band = PhaseBand(modal, state);
            // Measured off the panel's own width: the button is anchored to the panel's top-LEFT,
            // so a wider panel leaves it stranded in the middle unless it is moved with it.
            var close = FixedButton(modal,"Close  [Esc]",new Vector2(ModalWidth - 192f,-15),new Vector2(174,45),director.ClosePanels);
            Anchor((RectTransform)close.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-18,-15),new Vector2(174,45));
            // LiberationSans SDF is a static atlas without U+2191/U+2193, so the arrow glyphs
            // would render as tofu. Words also read better to a screen reader.
            var controlHint = FixedText(modal,"Tab / Up / Down select · Enter confirm · Scroll for more",15,UiTheme.Muted,new Vector2(24,-72),new Vector2(550,26));
            controlHint.name = "Panel control hint";
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

        /// <summary>
        /// The docked panel's shape. Wide and short, sitting above the bottom chrome, so the set
        /// stays visible over it - the mockups draw every decision this way and the panel is the
        /// one piece of chrome big enough to hide the house on its own.
        /// </summary>
        private const float ModalWidth = 900f;
        private const float ModalHeight = 300f;
        /// <summary>How far the panel's foot sits above the canvas floor, clear of the status band.</summary>
        private const float ModalLift = 170f;

        public void PanelTitle(string title, string subtitle) { Heading(title); Paragraph(subtitle); }
        /// <summary>The caption a spectating player sees above everything else.</summary>
        public const string SpectatorCaption = "WATCHING AS A SPECTATOR";

        /// <summary>
        /// Standing notice that the player is out of the game and the season is continuing without
        /// them.
        ///
        /// <para>The mechanism for spectating already worked — an evicted player's input is gated
        /// and the season plays on — but nothing said so, so the controls simply stopped answering.
        /// That reads as a bug rather than an ending, which is the difference this repairs.</para>
        /// </summary>
        public void SpectatorNote(string detail)
        {
            FlowText(SpectatorCaption,18,UiTheme.Warning).gameObject.name = "Spectator banner";
            FlowText(detail,19,UiTheme.Muted);
        }

        public void Heading(string value) { FlowText(value,26,Accent); }

        /// <summary>A heading in a given colour. The recap uses gold, as the reference build does.</summary>
        public void Heading(string value,Color colour) { FlowText(value,26,colour); }

        /// <summary>Small letterspaced copy above a section, the reference build's eyebrow.</summary>
        public void Eyebrow(string value,Color colour)
        {
            var text = FlowText(value,15,colour);
            text.characterSpacing = 10f;
        }
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
            SetActivityLayout(ActivityLayout.Conversation);
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

        /// <summary>
        /// The result of the exchange as a chip, the way the web build reports it.
        ///
        /// <para>A conversation that changes a number silently reads as flavour text. This is the
        /// committed difference in trust, not a predicted or nominal one — the engine scales every
        /// delta by the actor's social stat and rolls a separate reciprocal value, so the figure a
        /// design document would quote is routinely not the figure the save holds.</para>
        ///
        /// <para>Nothing is drawn when nothing moved. A chip reading "+0" would be worse than
        /// silence: it implies the action was wasted when it may have done something the notebook
        /// records elsewhere.</para>
        /// </summary>
        public void OutcomeChips(double trustDelta)
        {
            int rounded = Mathf.RoundToInt((float)trustDelta);
            if (rounded == 0) return;

            var row = new GameObject("Outcome",typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(content,false);
            row.gameObject.AddComponent<LayoutElement>().minHeight = 30f * FontScale;

            // The outcome of a social action is a relationship moving, so it takes the relationship
            // colours rather than navigation blue.
            var tint = rounded > 0 ? UiTheme.Allied : UiTheme.Conflict;
            var chip = Panel("Trust chip",row,new Color(tint.r,tint.g,tint.b,.18f));
            Anchor(chip,new Vector2(0,1),new Vector2(0,1),new Vector2(4f,-1f),new Vector2(214f * FontScale,28f * FontScale));
            chip.GetComponent<Image>().raycastTarget = false;

            FixedText(chip,
                (rounded > 0 ? "+" : "") + rounded + (rounded > 0 ? " trust gained" : " trust lost"),
                15,tint,new Vector2(10f,-4f),new Vector2(194f * FontScale,20f * FontScale));
        }

        /// <summary>
        /// Adds the relationship graph to the current panel.
        ///
        /// <para>Placed above the list it summarises rather than instead of it. The graph answers
        /// "where do I stand" at a glance; the list still carries the exact numbers, and a player
        /// deciding a nomination wants both.</para>
        /// </summary>
        public void SocialGraphPanel(EpisodeState state)
        {
            if (content == null || state == null) return;
            SetActivityLayout(ActivityLayout.Relationships);
            Canvas.ForceUpdateCanvases();
            relationshipRoot = RelationshipWeb.Build(content, state, FontScale, font, Portrait,
                id => director.ShowNotebookSection(EpisodeDirector.NotebookSection.Network), modalScroll.viewport.rect.height);
            // The graph is the notebook's entry view. Its explanatory heading belongs after the
            // complete graph instead of consuming the top of its only visible viewport.
            relationshipRoot.SetAsFirstSibling();
        }

        /// <summary>Adds the room-occupancy cards to the current panel.</summary>
        public void HouseMapPanel(System.Collections.Generic.IList<HouseMap.Room> rooms)
        {
            if (content == null) return;
            HouseMap.Build(content, rooms, FontScale, font);
        }

        /// <summary>
        /// A read-only row fronted by a houseguest's face: who they are, and what they did.
        ///
        /// <para>Distinct from <see cref="Action(string,Texture,Action)"/>, which looks similar and
        /// is a button. A vote that has already been cast is a fact to read, and making it clickable
        /// would invite a player to try to change it.</para>
        /// </summary>
        public void PortraitRow(string contestantId, string heading, string body)
        {
            var portrait = Portrait(contestantId);
            var rect = Chrome("Voter", content);
            rect.GetComponent<Image>().raycastTarget = false;
            float height = (portrait != null ? 66f : 52f) * FontScale;
            rect.gameObject.AddComponent<LayoutElement>().minHeight = height;

            float textLeft = 14f;
            if (portrait != null)
            {
                float side = 46f * FontScale;
                var frame = new GameObject("Face", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                frame.SetParent(rect, false);
                frame.anchorMin = new Vector2(0, .5f); frame.anchorMax = new Vector2(0, .5f); frame.pivot = new Vector2(0, .5f);
                frame.anchoredPosition = new Vector2(10f, 0f);
                frame.sizeDelta = new Vector2(side, side);
                var disc = frame.GetComponent<Image>();
                disc.sprite = UiTheme.Circle(); disc.type = Image.Type.Simple; disc.raycastTarget = false;
                frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;

                var face = new GameObject("Portrait", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                face.rectTransform.SetParent(frame, false);
                face.rectTransform.anchorMin = Vector2.zero; face.rectTransform.anchorMax = Vector2.one;
                face.rectTransform.offsetMin = Vector2.zero; face.rectTransform.offsetMax = Vector2.zero;
                face.texture = portrait; face.raycastTarget = false;
                textLeft = 10f + side + 12f;
            }

            FixedText(rect, heading, 19, Paper, new Vector2(textLeft, -8f * FontScale), new Vector2(560f * FontScale, 24f * FontScale));
            if (!string.IsNullOrEmpty(body))
                FixedText(rect, body, 15, UiTheme.Muted, new Vector2(textLeft, -32f * FontScale), new Vector2(560f * FontScale, 26f * FontScale));
        }

        private string pendingScroll;

        /// <summary>
        /// The live feed's card (V5): the second camera's picture and its caption, lower right
        /// above the exploration help, where the modal never reaches. Rebuilt with the rest of the
        /// chrome; the texture it shows is the director's and outlives every rebuild.
        /// </summary>
        private TMP_Text liveFeedCaption;
        private TMP_Text liveFeedHeading;
        private Image liveFeedDot;

        private float LiveFeedCard(Transform parent, float top)
        {
            const float height = 226f;
            var card = Chrome(EpisodeDirector.LiveFeedCardName, parent);
            Anchor(card, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-RightColumnInset, -top), new Vector2(RightColumnWidth, height));
            liveFeedHeading = CardHeading(card, "LIVE FEED", "camera");
            var dot = HudPrimitives.Disc("Live dot", card, UiTheme.Conflict);
            Anchor(dot, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -14), new Vector2(10, 10));
            liveFeedDot = dot.GetComponent<Image>();
            var picture = new GameObject("Picture", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            picture.transform.SetParent(card, false);
            picture.texture = director.LiveFeedTexture;
            picture.raycastTarget = false;
            Anchor(picture.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -36), new Vector2(RightColumnWidth - 24f, 145));
            UiTheme.AddBorder(picture.rectTransform, 6, UiTheme.EdgeActive);
            liveFeedCaption = FixedText(card, director.LiveFeedCaption, 13, Paper, new Vector2(16, -188), new Vector2(RightColumnWidth - 32f, 30));
            SetLiveFeedPaused(director.IsPanelOpen);
            return height;
        }

        /// <summary>The feed's caption changes with the house; the card is not rebuilt for it.</summary>
        public void SetLiveFeedCaption(string text)
        {
            if (liveFeedCaption != null) liveFeedCaption.text = text ?? "";
        }

        public void SetLiveFeedPaused(bool paused)
        {
            if (liveFeedHeading != null) liveFeedHeading.text = paused ? "FEED PAUSED" : "LIVE FEED";
            if (liveFeedDot != null) liveFeedDot.color = paused ? UiTheme.Muted : UiTheme.Conflict;
        }

        /// <summary>The overview's side column (V5): who is where, one row a room, beside the labelled house.</summary>
        public const string OverviewColumnName = "Overview column";

        private void OverviewColumn(Transform parent, float top)
        {
            var rooms = director.WhoIsWhere();
            const float RowHeight = 44f;
            var column = Chrome(OverviewColumnName, parent);
            Anchor(column, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-RightColumnInset, -top),
                new Vector2(RightColumnWidth, 40 + rooms.Count * RowHeight));
            CardHeading(column, "WHO IS WHERE", "house");
            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                float y = -(36 + i * RowHeight);
                FixedText(column, RoomLabels.Title(room.Name), 15, Paper, new Vector2(16, y), new Vector2(200, 20));
                var count = FixedText(column, room.Occupants.Count.ToString(), 15, Accent, new Vector2(218, y), new Vector2(52, 20));
                count.alignment = TextAlignmentOptions.Right;
                // Trimmed: a room holding the whole house is a line of eight names, and this row is
                // 254 px wide at a size that has nowhere left to shrink to.
                string who = room.Occupants.Count == 0 ? "empty"
                    : Excerpt(string.Join(", ", room.Occupants.Select(o => o.Name.Split(' ')[0])), 36);
                FixedText(column, who, 13, UiTheme.Muted, new Vector2(16, y - 19), new Vector2(RightColumnWidth - 32f, 18));
            }
        }

        /// <summary>
        /// Names the most recent thing added to the panel, so the icon rail can scroll back to it.
        /// </summary>
        public void Mark(string sectionName)
        {
            if (sectionName == EpisodeDirector.NotebookSection.Network && relationshipRoot != null)
            { relationshipRoot.name = sectionName; return; }
            if (content == null || content.childCount == 0) return;
            content.GetChild(content.childCount - 1).gameObject.name = sectionName;
        }

        /// <summary>
        /// Asks for the panel to be scrolled to a named section once it has been rebuilt.
        ///
        /// <para>Deferred rather than immediate because a rail click re-renders the whole panel:
        /// scrolling now would move a ScrollRect that is about to be destroyed.</para>
        /// </summary>
        public void RequestScrollTo(string sectionName) => pendingScroll = sectionName;

        /// <summary>Applies a deferred scroll. Called once the panel's content is complete.</summary>
        public void ApplyPendingScroll()
        {
            if (string.IsNullOrEmpty(pendingScroll) || modalScroll == null) return;
            var target = pendingScroll;
            pendingScroll = null;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            var section = content.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => rect.name == target);
            if (section == null) return;

            float travel = content.rect.height - modalScroll.viewport.rect.height;
            if (travel <= 1f) { modalScroll.verticalNormalizedPosition = 1f; return; }
            // Layout children do not all use a top pivot. Their anchored position may name the
            // centre of a tall graph, so measure the actual top in content coordinates instead.
            var sectionTop = content.InverseTransformPoint(section.TransformPoint(
                new Vector3(section.rect.center.x, section.rect.yMax, 0f)));
            float offset = content.rect.yMax - sectionTop.y;
            modalScroll.verticalNormalizedPosition =
                Mathf.Clamp01(1f - offset / travel);
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
            var input = SpeechDraft("Final speech draft","What do you want the jury to remember about your game?",
                "Final speech character count");
            Action(SpeechSubmitCaption,() => director.SubmitSpeech(input.text));
            Action(SpeechSkipCaption,() => director.SubmitSpeech(""));
        }
        /// <summary>
        /// The speech editor: a retained multi-line draft with a character count.
        ///
        /// <para>Shared by the finale and by a nominee's speech from the block. The two are
        /// different speeches with different captions and different stakes, but the widget is the
        /// same one, and it carries behaviour worth having in one place — Tab moves focus rather
        /// than inserting a tab, and the draft survives a HUD rebuild, so a repaint cannot silently
        /// erase what someone was part way through writing.</para>
        /// </summary>
        private EpisodeSpeechInputField SpeechDraft(string panelName,string hintText,string counterName)
        {
            var rect = Panel(panelName,content,Surface);
            rect.gameObject.AddComponent<LayoutElement>().minHeight = 210 * FontScale;
            var input = rect.gameObject.AddComponent<EpisodeSpeechInputField>();
            var text = NewText(rect,"",21,Paper); Stretch(text.rectTransform,14,12,14,12);
            text.alignment = TextAlignmentOptions.TopLeft;
            var hint = NewText(rect,hintText,21,UiTheme.Muted);
            Stretch(hint.rectTransform,14,12,14,12);
            input.textComponent = text; input.placeholder = hint;
            input.characterLimit = 2000; input.lineType = TMP_InputField.LineType.MultiLineNewline;
            input.onValidateInput = (value,index,character) => character == '\t' ? '\0' : character;
            input.customCaretColor = true; input.caretColor = Accent;
            input.selectionColor = new Color(Accent.r,Accent.g,Accent.b,.3f);
            input.text = retainedSpeech;
            var count = FlowText(retainedSpeech.Length + " / 2000 characters",17,Paper);
            count.gameObject.name = counterName;
            input.onValueChanged.AddListener(value => { retainedSpeech = value; count.text = value.Length + " / 2000 characters"; });
            return input;
        }

        /// <summary>A nominee's speech from the block, using the editor the finale uses.</summary>
        public void EvictionSpeech(Action<string> commit)
        {
            Paragraph("Up to 2,000 characters. Enter adds a line; Tab or Shift+Tab moves to another control.");
            var input = SpeechDraft("Block speech draft",
                "What do you want the house to have heard before it votes?","Block speech character count");
            Action(EvictionSpeechCaption,() => commit(input.text));
            Action(EvictionSpeechSkipCaption,() => commit(""));
        }

        private TMP_Text FlowText(string value,int size,Color color)
        {
            var text = NewText(content,value,size,color); var element = text.gameObject.AddComponent<LayoutElement>(); element.minHeight = size * FontScale + 8;
            return text;
        }

        /// <summary>
        /// Where a category pill sits: at the end of a row, or along the foot of a card. A petal on
        /// the conversation dial is a card, and a pill hung off its right-hand edge would sit over
        /// the caption rather than beside it.
        /// </summary>
        public enum TagSeat { RowEnd, CardFoot }

        /// <summary>
        /// Pins a small category pill to a control, the way the web build tags its action list.
        ///
        /// <para>It is drawn as a separate graphic rather than folded into the caption, because the
        /// caption is how tests and a screen reader identify the control — appending to it renamed
        /// every button and broke six tests that look one up by the words on it.</para>
        /// </summary>
        public void Tag(Button target,string text) => Tag(target,text,TagSeat.RowEnd);

        /// <inheritdoc cref="Tag(Button,string)"/>
        public void Tag(Button target,string text,TagSeat seat)
        {
            if (target == null || string.IsNullOrEmpty(text)) return;
            var chip = Panel("Tag",target.transform,new Color(Accent.r,Accent.g,Accent.b,.16f));
            var size = seat == TagSeat.CardFoot
                ? new Vector2(130f * FontScale,20f * FontScale)
                : new Vector2(104f * FontScale,22f * FontScale);
            if (seat == TagSeat.CardFoot)
                Anchor(chip,new Vector2(0,0),new Vector2(0,0),new Vector2(10f * FontScale,6f * FontScale),size);
            else
                Anchor(chip,new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-12f,0f),size);
            chip.GetComponent<Image>().raycastTarget = false;
            var label = FixedText(chip,text,12,Accent,Vector2.zero,size);
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero; label.rectTransform.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// One of the modal's option cards (mockup-04, mockup-11): the glass ground, the cyan
        /// hairline, and a chevron at the end saying the row leads somewhere.
        ///
        /// <para>The chevron costs the caption 26 px of width, which is why it is only on the rows
        /// that have the width to give: a row fronted by a portrait already spends its right-hand
        /// end on the trust reading.</para>
        /// </summary>
        public Button Action(string caption,Action action)
        {
            var rect = Chrome(caption,content); var element = rect.gameObject.AddComponent<LayoutElement>(); element.minHeight = 57 * FontScale;
            float mark = 18f * FontScale;
            var button = FinishButton(rect,caption,action,16f,16f + mark + 10f);
            HudPrimitives.Chevron(rect,UiTheme.Hairline,mark).anchoredPosition = new Vector2(-16f,0f);
            return button;
        }

        /// <summary>
        /// An action row fronted by a houseguest's portrait, as the nominee cards and the ballot
        /// draw one (mockup-08, mockup-10): the face in a ring, with the role badge the house has
        /// given them. Falls back to a plain row when the persona has no art.
        /// </summary>
        public Button Action(string caption,Texture portrait,Action action)
        {
            if (portrait == null) return Action(caption,action);
            var rect = Chrome(caption,content);
            rect.gameObject.AddComponent<LayoutElement>().minHeight = 68 * FontScale;
            var button = FinishButton(rect,caption,action,68f * FontScale);

            float side = 52f * FontScale;
            var rim = HudPrimitives.Portrait(rect,portrait,UiTheme.Outline,side,3f * FontScale,false);
            // Kept under the old name: this is the child every screen and test knows as the row's
            // face, and the change here is the frame around it, not what it is.
            rim.gameObject.name = "Portrait";
            rim.anchorMin = new Vector2(0,.5f); rim.anchorMax = new Vector2(0,.5f); rim.pivot = new Vector2(0,.5f);
            rim.anchoredPosition = new Vector2(8f,0f);
            return button;
        }

        public void ChoosePair(Option[] options,Action<string,string> commit,string commitCaption = "Commit nominations",
            Action<string,string> selectionChanged = null)
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
                    selectionChanged?.Invoke(first,second);
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

            // The card's face carries what the house has done to this person: the ring reads the
            // player's own standing with them, the badge reads the role the week has given them.
            // Both are decoration over facts the row already states in words - the trust chip below,
            // and the nomination or the veto in the panel's own copy.
            var rim = rect.Find("Portrait") as RectTransform;
            if (rim != null)
            {
                var ring = rim.GetComponent<Image>();
                if (ring != null)
                    // One fact, one set. This row states the player's standing with a houseguest
                    // three times - ring, ALLY tag, trust number - and the three used to disagree:
                    // the ring went cyan for trust while the number went blue and the tag went gold.
                    ring.color = allied || trust > 5 ? UiTheme.Allied
                        : trust < -5 ? UiTheme.Conflict : UiTheme.Muted;
                HudPrimitives.AddRoleMark(rim, RoleOf(state, contestantId), rim.sizeDelta.x * .82f);
            }

            // Keep a long caption from running underneath the chips.
            var caption = button.GetComponentInChildren<TMP_Text>();
            if (caption != null)
                caption.rectTransform.offsetMax = new Vector2(-reserved, caption.rectTransform.offsetMax.y);

            if (allied)
            {
                var tag = NewText(rect,"ALLY",14,UiTheme.Allied);
                Anchor(tag.rectTransform,new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-96f,0f),new Vector2(48,22));
                tag.alignment = TextAlignmentOptions.Right;
            }

            var reading = NewText(rect,"Trust " + trust.ToString("0"),15,
                // The same set as the portrait ring and the ALLY tag above.
                trust > 5 ? UiTheme.Allied : trust < -5 ? UiTheme.Conflict : UiTheme.Muted);
            Anchor(reading.rectTransform,new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-16f,0f),new Vector2(74,22));
            reading.alignment = TextAlignmentOptions.Right;
        }

        /// <summary>
        /// The badge a houseguest's face carries this week: the block, the crown, or the veto.
        /// Read from committed state, never from a projection.
        /// </summary>
        private static HudPrimitives.RoleMark RoleOf(EpisodeState state, string contestantId)
        {
            if (state == null || string.IsNullOrEmpty(contestantId)) return HudPrimitives.RoleMark.None;
            if (contestantId == state.hohId) return HudPrimitives.RoleMark.HeadOfHousehold;
            if (contestantId == state.vetoHolderId) return HudPrimitives.RoleMark.VetoHolder;
            if (state.nominees != null && state.nominees.Contains(contestantId)) return HudPrimitives.RoleMark.Nominee;
            return HudPrimitives.RoleMark.None;
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

            return CharacterPortraits.Get(contestant);
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

        /// <summary>
        /// A labelled progress bar: the caption on the left, the remaining count on the right, and
        /// a filled track under both.
        ///
        /// <para>The web build shows the action budget this way — a number you can see draining
        /// rather than a sentence you have to read and subtract. The count is stated in the caption
        /// as well as drawn, because a bar on its own is not something a screen reader can report.
        /// </para>
        /// </summary>
        public void Meter(string caption, int remaining, int total, Color tint)
        {
            int held = Mathf.Clamp(remaining, 0, Mathf.Max(1, total));
            var rect = Panel("Meter", content, Surface);
            rect.gameObject.AddComponent<LayoutElement>().minHeight = Mathf.RoundToInt(58 * FontScale);

            FixedText(rect, caption, 16, Paper, new Vector2(16, -10), new Vector2(360, 22));
            var countText = FixedText(rect, held + " of " + total, 16, tint, new Vector2(-16, -10), new Vector2(160, 22));
            countText.alignment = TextAlignmentOptions.Right;
            var countRect = countText.rectTransform;
            countRect.anchorMin = new Vector2(1, 1); countRect.anchorMax = new Vector2(1, 1); countRect.pivot = new Vector2(1, 1);

            var track = Panel("Track", rect, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .55f), 3);
            track.anchorMin = new Vector2(0, 0); track.anchorMax = new Vector2(1, 0); track.pivot = new Vector2(.5f, 0);
            track.offsetMin = new Vector2(16, 14); track.offsetMax = new Vector2(-16, 22);
            track.GetComponent<Image>().raycastTarget = false;

            // The fill travels from where this meter was last drawn to where it is now, so a
            // budget that just spent an action is seen draining. The last value survives the
            // rebuild by caption; a meter seen for the first time is drawn where it is.
            float target = total <= 0 ? 0f : (float)held / total;
            float shown = meterShown.TryGetValue(caption, out var previous) ? previous : target;
            meterShown[caption] = target;
            var fill = Panel("Fill", track, tint, 3);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(target, 1f);
            fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().raycastTarget = false;
            if (!ReducedMotion && Mathf.Abs(shown - target) > 0.001f) fill.gameObject.AddComponent<HudFill>().Play(shown, target);
        }
        private readonly Dictionary<string, float> meterShown = new Dictionary<string, float>();

        public const string FollowChipName = "Follow chip";

        private HouseAudio foley;
        /// <summary>The director's audio, for the HUD's own sounds; found once, absent in a bare test.</summary>
        private void Foley(HouseAudio.Cue cue)
        {
            if (foley == null && director != null) foley = director.GetComponent<HouseAudio>();
            if (foley != null) foley.PlayCue(cue);
        }

        /// <summary>
        /// The chip under the house pill naming who the camera is following, and how to stop.
        /// Rebuilt with the chrome, and redrawn on its own when the subject changes between
        /// renders - a click on a body changes the camera without changing the episode.
        /// </summary>
        public void ShowFollowing(string name)
        {
            if (canvas == null) return;
            // Every chip, not the first one found. Destroy is deferred to the end of the frame, so a
            // rebuild in the same frame leaves an old chip still parented and still named: taking one
            // of the two away left the other on screen naming a houseguest nobody was following.
            foreach (var child in canvas.transform.Cast<Transform>().Where(t => t.name == FollowChipName).ToArray())
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            FollowChip(name);
        }

        private void FollowChip(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            var chip = Chrome(FollowChipName, canvas.transform);
            Anchor(chip, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -78), new Vector2(360, 34));
            var text = FixedText(chip, "FOLLOWING · " + name.ToUpperInvariant() + "   F recenter · ] next", 13, UiTheme.Accent,
                new Vector2(12, -8), new Vector2(336, 20));
            text.alignment = TextAlignmentOptions.Center;
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
        public void SetPrompt(string value) { if(prompt==null)return; prompt.text=Localisation.Text(value); prompt.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(value)); }
        public void SetVisible(bool value) { if(canvas!=null) canvas.gameObject.SetActive(value); }
        private void OnDestroy() { if(canvas!=null) Destroy(canvas.gameObject); }

        // Full-screen screens that sit over the HUD: the weekly recap and the season report. While
        // one is up it is the keyboard's whole world - its controls get the ring and the HUD's get
        // none - so Enter cannot press a button behind a scrim. Before this, a recap opened with
        // the selection still on a HUD control underneath it.
        private readonly List<CanvasGroup> overlays = new List<CanvasGroup>();
        private RectTransform lastOverlay;
        private int overlayControls;

        public void RegisterOverlay(CanvasGroup group)
        {
            if (group != null && !overlays.Contains(group)) overlays.Add(group);
        }

        /// <summary>The topmost visible overlay, by canvas sorting order; null when the HUD is on top.</summary>
        private RectTransform ActiveOverlay()
        {
            RectTransform top = null; int order = int.MinValue;
            foreach (var group in overlays)
            {
                if (group == null || group.alpha <= 0f || !group.gameObject.activeInHierarchy) continue;
                var owner = group.GetComponent<Canvas>();
                int sorting = owner != null ? owner.sortingOrder : 0;
                if (sorting >= order) { order = sorting; top = group.transform as RectTransform; }
            }
            return top;
        }

        private void LateUpdate()
        {
            var events = EventSystem.current;
            if (canvas == null || !canvas.gameObject.activeInHierarchy || events == null) return;
            var overlay = ActiveOverlay();
            if (overlay != lastOverlay) { lastOverlay = overlay; restoreSelection = true; }
            // A screen that rebuilds its form on every press (the character creator) hands the ring
            // a new set of controls each time; rewire when the count moves.
            int controls = overlay != null ? overlay.GetComponentsInChildren<Selectable>().Length : 0;
            if (controls != overlayControls) { overlayControls = controls; if (overlay != null) restoreSelection = true; }
            var scope = overlay != null ? overlay : modal;
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
                // Scrollbars stay out of the ring: the panel scrolls to whatever is selected, and a
                // scrollbar the ScrollRect auto-hides after layout would sit in the ring inactive,
                // where Down and Tab both refuse to land - the keyboard stuck on the control before it.
                var all = canvas.GetComponentsInChildren<Selectable>()
                    .Concat(overlay != null ? overlay.GetComponentsInChildren<Selectable>() : Enumerable.Empty<Selectable>())
                    .Where(item => item.IsActive() && item.IsInteractable() && !(item is Scrollbar)).ToArray();
                var eligible = scope == null ? all : all.Where(item => item.transform.IsChildOf(scope)).ToArray();
                bool spatialOverlay = overlay != null && overlay.GetComponent<CompetitionGameScreen>() != null;
                foreach (var item in all)
                {
                    // A memory board supplies genuine two-dimensional navigation. Keep it, while
                    // still taking every control behind the competition out of the input scope.
                    if (spatialOverlay && item.transform.IsChildOf(scope)) continue;
                    var navigation = item.navigation;
                    navigation.mode = scope != null && !item.transform.IsChildOf(scope) ? Navigation.Mode.None : Navigation.Mode.Explicit;
                    navigation.selectOnLeft = null; navigation.selectOnRight = null;
                    navigation.selectOnUp = null; navigation.selectOnDown = null;
                    item.navigation = navigation;
                }
                for (var index = 0; index < eligible.Length; index++)
                {
                    if (spatialOverlay) break;
                    var navigation = eligible[index].navigation;
                    navigation.selectOnUp = eligible[(index + eligible.Length - 1) % eligible.Length];
                    navigation.selectOnDown = eligible[(index + 1) % eligible.Length];
                    eligible[index].navigation = navigation;
                }
                tabOrder = eligible;
                if (!spatialOverlay) WireConversationGrid();
                // Whatever is already selected and still eligible keeps the focus: a rewire is not a
                // reason to move the keyboard. The HUD's own rebuilds destroy the old selection, so
                // for them this is null and the named restore below takes over as before.
                var current = events.currentSelectedGameObject;
                var focus = eligible.FirstOrDefault(item => current != null && item.gameObject == current)
                    ?? eligible.FirstOrDefault(item => item.name == preferredSelection)
                    ?? eligible.FirstOrDefault(item => content != null && item.transform.IsChildOf(content))
                    ?? eligible.FirstOrDefault(item => item.name == "Go to episode screen")
                    ?? eligible.FirstOrDefault();
                events.SetSelectedGameObject(focus != null ? focus.gameObject : null);
                restoreSelection = false;
            }

            var selected = events.currentSelectedGameObject;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame && selected != null
                && (overlay == null || overlay.GetComponent<CompetitionGameScreen>() == null))
            {
                var current = selected.GetComponent<Selectable>();
                int index = System.Array.IndexOf(tabOrder, current);
                bool reverse = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                var next = index < 0 || tabOrder.Length == 0 ? null
                    : tabOrder[(index + (reverse ? tabOrder.Length - 1 : 1)) % tabOrder.Length];
                if (next != null && next.IsActive() && next.IsInteractable())
                {
                    selected.GetComponent<TMP_InputField>()?.DeactivateInputField();
                    events.SetSelectedGameObject(next.gameObject);
                    selected = next.gameObject;
                }
            }
            if (scope != null && (selected == null || !selected.transform.IsChildOf(scope)))
            {
                var focus = (overlay != null ? overlay : content).GetComponentsInChildren<Selectable>().FirstOrDefault(item => item.IsActive() && item.IsInteractable())
                    ?? scope.GetComponentsInChildren<Selectable>().FirstOrDefault(item => item.IsActive() && item.IsInteractable());
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
        private Button FinishButton(RectTransform rect,string caption,Action action,float leftInset = 16f,float rightInset = 16f)
        {
            var button = Pressable(rect,action);
            var text=NewText(rect,caption,20,Paper); Stretch(text.rectTransform,leftInset,5,rightInset,5); text.alignment=TextAlignmentOptions.Left;
            return button;
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
            // The one place the HUD turns a string into text on screen: the caption stays the
            // control's name and key, and the table decides the words (MASTER-PLAN §3.D).
            value = Localisation.Text(value);
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
                // The veto arc is three different colours in the web build, not one: the draw and the
                // competition are blue, and the meeting — the beat that resolves the block — is
                // green. Tinting all three gold made a week's most consequential turn look identical
                // to the draw that set it up.
                case EpisodePhase.VetoSelection:
                case EpisodePhase.Veto:
                    return UiTheme.AccentDeep;
                case EpisodePhase.VetoMeeting:
                    return UiTheme.PositiveDeep;
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
        /// <summary>
        /// A quiet, readable card. The edge says how much this panel is asking for: leave it out
        /// and it rests, pass <see cref="UiTheme.EdgeActive"/> for the one panel the player is
        /// meant to act on, or pass a semantic colour where the panel IS that thing - the
        /// conversation petals pass their action's own tint.
        ///
        /// <para>This used to take a Color it never read. Every one of the seventeen call sites
        /// passed Ink into the void and every panel came out wearing the same cyan hairline, so a
        /// resting frame carried eighteen identical lit rectangles and nothing could be emphasised
        /// by colour at all. The mockups do the opposite: in mockup-01 no persistent panel has a
        /// lit edge, and in mockup-04 exactly one element in the frame does.</para>
        ///
        /// <para>The default is null rather than default(Color), which is transparent black and
        /// would silently erase every edge instead.</para>
        /// </summary>
        private static RectTransform Chrome(string name,Transform parent,Color? edge=null)
        {
            var fill = UiTheme.GlassFill; fill.a = .94f;
            var rect=Panel(name,parent,fill,UiTheme.GlassRadius);
            UiTheme.AddBorder(rect,UiTheme.GlassRadius,edge ?? UiTheme.EdgeResting);
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
