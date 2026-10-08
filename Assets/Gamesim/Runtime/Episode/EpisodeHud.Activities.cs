using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeHud
    {
        /// <summary>
        /// Activities own their layout; they share controls, focus and save semantics. Strategy is
        /// the week's four strategy screens' taller stage (EpisodeHud.StrategyStage.cs).
        /// </summary>
        public enum ActivityLayout { Standard, Relationships, Conversation, ConversationNotice, Competition, Creation, Diary, Nominations, HouseEvent, Ballot, Settings, Stage, Strategy }

        private ActivityLayout activityLayout;
        private RectTransform relationshipRoot;
        // Closed until asked for. It opened itself on the first frame of every session, which put
        // a five-line reference card over the corner of the house that a player who has read it
        // once never wants again - and it is the one box the right column has no room to grow past.
        private bool helpExpanded = false;
        private RectTransform explorationHelp;

        /// <summary>What an activity leaves above itself: the top bar and a gap.</summary>
        private const float ActivityHeadroom = 80f;

        public ActivityLayout CurrentActivityLayout => activityLayout;
        public bool Compact { get; set; }
        public RectTransform ActivityContent => content;
        /// <summary>Reference-canvas bounds left clear for world captions by the visible chrome.</summary>
        public Rect WorldCaptionSafeBounds
        {
            get
            {
                if(canvas==null)return new Rect(24,194,1552,626);
                var root=(RectTransform)canvas.transform;var size=root.rect.size;
                // LeftColumnX says this once; this line used to say it again by hand. The top is
                // the 52-unit bar's foot and a gap: every chip on it is chrome a bubble must clear.
                float left=LeftColumnX,right=size.x-24f,bottom=100f,top=size.y-80f;
                foreach(var item in root.GetComponentsInChildren<RectTransform>())
                {
                    // The compact objective card is a left-column card; the full HUD's objective is
                    // a chip on the top bar, which the ceiling above already clears.
                    bool leftCard=(item.name=="Objective" && Compact) || item.name=="Rail ground";
                    // House vibe is a right-column card. It was counted as a LEFT card after it moved,
                    // which pushed the left edge past the right one whenever it was on screen.
                    bool rightCard=item.name==EpisodeDirector.LiveFeedCardName || item.name==RecentEventsCardName
                        || item.name==OverviewColumnName || item.name=="Exploration controls" || item.name==HouseVibeCardName || item.name==ObjectivesCardName
                        || item.name==RelationshipsCardName || item.name==NearbyCardName || item.name==PullCardName;
                    // The floor grew a band. The strip and the caption above it are both fixed
                    // chrome a world bubble must clear, and the caption used to be low enough that
                    // the default floor covered it.
                    bool bottomCard=item.name=="Interaction prompt" || item.name==CastRail.RootName
                        || item.name=="Status" || item.name==CastRail.StripName || item.name==SpeechBarName;
                    // The follow chip is anchored TOP-centre, under the house pill, and was being
                    // counted as a bottom card: it pushed `bottom` to 834 while `top` was 796, so
                    // MinMaxRect returned an inverted rect and every world bubble was pinned above
                    // the top bar for as long as the player was following anybody.
                    bool topCard=item.name==FollowChipName;
                    if(!leftCard && !rightCard && !bottomCard && !topCard)continue;
                    var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(root,item);
                    if(leftCard)left=Mathf.Max(left,bounds.max.x+size.x*.5f+12f);
                    if(rightCard)right=Mathf.Min(right,bounds.min.x+size.x*.5f-12f);
                    if(bottomCard)bottom=Mathf.Max(bottom,bounds.max.y+size.y*.5f+12f);
                    if(topCard)top=Mathf.Min(top,bounds.min.y+size.y*.5f-12f);
                }
                return Rect.MinMaxRect(left,bottom,Mathf.Max(left+1f,right),Mathf.Max(bottom+1f,top));
            }
        }

        public void SetActivityLayout(ActivityLayout layout)
        {
            // A layout starts from the full column: a stage's reading cap belongs to the stage, and
            // a later layout (the briefing after the episode screen's stage) sizes its own.
            UncapContent();
            ApplyActivityLayout(layout);
            // A dedicated layout sizes itself; only the Standard panel is fitted to its content.
            if (layout != ActivityLayout.Standard) fitToContent = false;
            // Every layout re-stretches the scroll; its foot stays clear of a pinned action.
            ApplyPinnedInset();
        }

        private void ApplyActivityLayout(ActivityLayout layout)
        {
            if (modal == null || canvas == null) return;
            activityLayout = layout;
            if (layout == ActivityLayout.Standard) return;

            var bounds = ((RectTransform)canvas.transform).rect;
            float canvasWidth = bounds.width > 0 ? bounds.width : 1600f;
            float canvasHeight = bounds.height > 0 ? bounds.height : 900f;
            if (layout == ActivityLayout.Conversation) { ConversationLayout(canvasWidth, canvasHeight); return; }
            if (layout == ActivityLayout.ConversationNotice) { ConversationNoticeLayout(canvasWidth, canvasHeight); return; }
            if (layout == ActivityLayout.Nominations) { NominationsLayout(canvasWidth, canvasHeight); return; }
            if (layout == ActivityLayout.HouseEvent) { HouseEventLayout(canvasWidth, canvasHeight); return; }
            if (layout == ActivityLayout.Competition) { CompetitionLayout(canvasWidth, canvasHeight); return; }
            if (layout == ActivityLayout.Diary) { DiaryLayout(canvasWidth, canvasHeight); return; }
            if (layout == ActivityLayout.Ballot) { BallotLayout(canvasWidth, canvasHeight); return; }
            if (layout == ActivityLayout.Settings) { SettingsLayout(canvasWidth, canvasHeight); return; }
            if (layout == ActivityLayout.Stage) { StageLayout(canvasWidth, canvasHeight); return; }
            if (layout == ActivityLayout.Strategy) { StrategyLayout(canvasWidth, canvasHeight); return; }
            float left = LeftColumnX;
            float right = 24f;
            float availableWidth = canvasWidth - left - right;
            float width = Mathf.Min(1180f, availableWidth);
            if (layout == ActivityLayout.Relationships) width = availableWidth;
            // An activity stands on the same floor the docked panel does. It used to stand at 100,
            // which was above the lower third while the lower third was on the canvas floor; the
            // caption sits a band higher now and Status is the one piece of chrome an activity does
            // NOT hide, so standing at 100 would draw the panel straight over the message channel.
            float height = layout == ActivityLayout.Conversation
                ? Mathf.Min(540f * FontScale, canvasHeight - ModalLift - ActivityHeadroom)
                : canvasHeight - ModalLift - ActivityHeadroom;
            Anchor(modal, new Vector2(1,0), new Vector2(1,0), new Vector2(-right,ModalLift), new Vector2(width,height));
            // The notebook is a page to read: its ground near opaque, as Refinement Kit 6 asks
            // (0.94 to 0.98, on the ground Image - never a CanvasGroup, which would fade the words
            // with it). The house's neon read through the .94 glass as lines across the text.
            if (layout == ActivityLayout.Relationships)
            {
                var ground = modal.GetComponent<Image>();
                if (ground != null) { var c = ground.color; ground.color = new Color(c.r, c.g, c.b, Mathf.Max(c.a, .97f)); }
            }

            // Context cards return when the player returns to exploration. They remain available
            // through the notebook and the persistent top navigation while an activity uses this space.
            // The objective is a chip on the top bar now and the travel buttons are rows of the
            // rail, both clear of every activity's frame; only the compact HUD's objective is a
            // left-column card an activity would sit on.
            if (Compact) SetChromeVisible("Objective", false);
            SetChromeVisible(HouseVibeCardName, false);
            SetChromeVisible(ObjectivesCardName, false);
            SetChromeVisible(EpisodeDirector.LiveFeedCardName, false);
            SetChromeVisible(RecentEventsCardName, false);
            SetChromeVisible(RelationshipsCardName, false);
            SetChromeVisible(NearbyCardName, false);
            SetChromeVisible(PullCardName, false);
            SetChromeVisible(OverviewColumnName, false);
            SetChromeVisible("Exploration controls", false);
            SetChromeVisible(FollowChipName, false);
            SetChromeVisible("Interaction prompt", false);
            // The strip goes with the rest of it. An activity claims the band from y 100 upward,
            // which is the strip's band, and a panel drawn over half the cast is worse than a panel
            // that has the screen to itself for as long as it is up.
            SetChromeVisible(CastRail.RootName, false);
            SetChromeVisible(CastRail.StripName, false);
            SetChromeVisible(CastRail.QuoteName, false);

            var hint = modal.Find("Panel control hint");
            if (hint != null) hint.gameObject.SetActive(false);
            if (modalScroll != null)
            {
                Stretch((RectTransform)modalScroll.transform, 20f, 76f, 20f, 16f);
                modalScroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>The conversation's column, so a test can find it the way it finds a named panel.</summary>
        public const string ConversationColumnName = "Conversation column";
        private RectTransform conversationColumn;

        /// <summary>The share of the stage the conversation's reading column takes, and the widest it runs at the resting text size.</summary>
        private const float ConversationColumnShare = .52f, ConversationColumnMax = 760f;
        /// <summary>How much of the house shows through a conversation: dimmed, not gone, so the pair stay behind the dial.</summary>
        private const float ConversationScrim = .72f;

        /// <summary>
        /// Where the conversation leaves the pair: the middle of the stage right of its column, in
        /// half-heights from the frame's centre, as <see cref="ArenaWindowOffset"/> is for the arena.
        /// The director hands it to the camera's two-shot.
        /// </summary>
        public float ConversationWindowOffset { get; private set; }

        /// <summary>
        /// A conversation takes the stage (playtest, 2026-09-27). It was mockup-12's transparent frame
        /// between the rail and the right column, with a glass column a fixed 340 wide down its left,
        /// and a row that carried a portrait, a trust reading and a tag left its caption 20 units, or
        /// less than nothing: the words stood one letter a line in a box 860 tall. Now the frame runs
        /// from the rail to the right edge over a dimmed house, the right column and the strip stand
        /// down as they do for every stage, and the column - the speaker, what they say and every way
        /// to talk that is not a petal - takes about half of it. The dial stands in the rest, under
        /// the pair, whom the camera stands there too.
        ///
        /// <para>The frame keeps its name and its raycast ground: the keyboard ring is scoped to
        /// 'Episode panel', and a click between the column and the dial must not fall through to
        /// the floor and walk the player away mid-sentence.</para>
        /// </summary>
        private void ConversationLayout(float canvasWidth, float canvasHeight)
        {
            var stage = StageRect(canvasWidth, canvasHeight);
            modal.anchorMin = modal.anchorMax = Vector2.zero;
            modal.pivot = Vector2.zero;
            modal.anchoredPosition = stage.position;
            modal.sizeDelta = stage.size;
            var frame = modal.GetComponent<Image>();
            if (frame != null) frame.color = new Color(UiTheme.Background.r, UiTheme.Background.g, UiTheme.Background.b, ConversationScrim);
            foreach (Transform child in modal)
                if (child.name == "Border" || child.name == "Glow" || child.name == "Phase band" || child.name == "Panel control hint")
                    child.gameObject.SetActive(false);
            HideStageChrome();

            float columnWidth = Mathf.Min(stage.width, Mathf.Clamp(stage.width * ConversationColumnShare, 420f, ConversationColumnMax * FontScale));
            float window = stage.x + columnWidth + 12f + (stage.width - columnWidth - 12f) * .5f;
            ConversationWindowOffset = Mathf.Max(0f, (window - canvasWidth * .5f) / (canvasHeight * .5f));

            if (conversationColumn != null) return;
            conversationColumn = Chrome(ConversationColumnName, modal);
            conversationColumn.anchorMin = new Vector2(0f, 0f); conversationColumn.anchorMax = new Vector2(0f, 1f);
            conversationColumn.pivot = new Vector2(0f, .5f);
            conversationColumn.anchoredPosition = Vector2.zero;
            conversationColumn.sizeDelta = new Vector2(columnWidth, 0f);
            if (modalScroll != null)
            {
                modalScroll.transform.SetParent(conversationColumn, false);
                Stretch((RectTransform)modalScroll.transform, 14f, 58f, 10f, 12f);
                modalScroll.verticalNormalizedPosition = 1f;
            }
            var close = modal.Find("Close  [Esc]");
            if (close != null)
            {
                close.SetParent(conversationColumn, false);
                Anchor((RectTransform)close, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12f, -10f), new Vector2(150f, 38f));
            }
        }

        /// <summary>
        /// The Head of Household's decision (mockup-09): a wide glass band low in the frame between
        /// the rail and the right column, holding the candidates as cards, with the house, the
        /// strip and the column all left up around it - the nominations are about those faces.
        /// </summary>
        private void NominationsLayout(float canvasWidth, float canvasHeight)
        {
            // The decision is the screen: the stage's whole frame, the candidates as many to a row
            // as it holds.
            var stage = StageRect(canvasWidth, canvasHeight);
            modal.anchorMin = modal.anchorMax = Vector2.zero;
            modal.pivot = Vector2.zero;
            modal.anchoredPosition = stage.position;
            modal.sizeDelta = stage.size;
            HideStageChrome();
            var hint = modal.Find("Panel control hint");
            if (hint != null) hint.gameObject.SetActive(false);
            if (modalScroll != null)
            {
                Stretch((RectTransform)modalScroll.transform, 20f, 72f, 20f, 14f);
                modalScroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>
        /// The competition's briefing (the style guide's modal): a card beside the rail, the
        /// height of the free area, with the house, the right column and the strip left up around
        /// it. It was the generic activity panel - the whole width, the strip and the column gone -
        /// which made a briefing a wall of paragraphs with every line the width of the screen.
        /// </summary>
        private void CompetitionLayout(float canvasWidth, float canvasHeight)
        {
            // A challenge is the whole screen: the house's chrome stands down, the briefing's sheet
            // runs down the left from the frame's foot to its head, and the arena fills the rest of
            // the frame, where the camera stands it (ArenaWindowOffset).
            float width = Mathf.Min(BriefingSheetWidth * FontScale, (canvasWidth - 2f * FullFrameEdge) * .58f);
            float height = canvasHeight - 2f * FullFrameEdge;
            modal.anchorMin = modal.anchorMax = Vector2.zero;
            modal.pivot = Vector2.zero;
            modal.anchoredPosition = new Vector2(FullFrameEdge, FullFrameEdge);
            modal.sizeDelta = new Vector2(width, height);
            var ground = modal.GetComponent<Image>();
            if (ground != null) { var c = ground.color; ground.color = new Color(c.r, c.g, c.b, Mathf.Max(c.a, .97f)); }
            CompactClose();
            HideChromeForFullFrame(canvasWidth, FullFrameEdge + width + FrameGapUnits);
            float window = FullFrameEdge + width + (canvasWidth - FullFrameEdge - (FullFrameEdge + width)) * .5f;
            ArenaWindowOffset = (window - canvasWidth * .5f) / (canvasHeight * .5f);
            var hint = modal.Find("Panel control hint");
            if (hint != null) hint.gameObject.SetActive(false);
            if (modalScroll != null)
            {
                Stretch((RectTransform)modalScroll.transform, 18f, 72f, 18f, 14f);
                modalScroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>
        /// The stage: every screen that is a place or a decision rather than a card - the episode
        /// screen, the nominations, the settings, the notebook - takes the frame from the rail to
        /// the right edge and from the status line to the top bar. The rail, the top bar and the
        /// status line stay; the right column and the strip stand down while it is up.
        /// </summary>
        private Rect StageRect(float canvasWidth, float canvasHeight)
        {
            float width = Mathf.Max(420f, canvasWidth - LeftColumnX - FullFrameEdge);
            float height = Mathf.Max(300f, canvasHeight - ModalLift - ActivityHeadroom);
            return new Rect(LeftColumnX, ModalLift, width, height);
        }

        /// <summary>The widest a stage's reading column runs, at the resting text size.</summary>
        private const float StageColumnWidth = 1040f;
        private const float SettingsColumnWidth = 900f;

        private void StageLayout(float canvasWidth, float canvasHeight)
        {
            var stage = StageRect(canvasWidth, canvasHeight);
            modal.anchorMin = modal.anchorMax = Vector2.zero;
            modal.pivot = Vector2.zero;
            modal.anchoredPosition = stage.position;
            modal.sizeDelta = stage.size;
            var ground = modal.GetComponent<Image>();
            if (ground != null) { var c = ground.color; ground.color = new Color(c.r, c.g, c.b, Mathf.Max(c.a, .97f)); }
            HideStageChrome();
            CapContent(StageColumnWidth * FontScale);
        }

        /// <summary>What a stage stands down: the right column, the strip, and the exploration furniture.</summary>
        private void HideStageChrome()
        {
            if (Compact) SetChromeVisible("Objective", false);
            SetChromeVisible(HouseVibeCardName, false);
            SetChromeVisible(ObjectivesCardName, false);
            SetChromeVisible(EpisodeDirector.LiveFeedCardName, false);
            SetChromeVisible(RecentEventsCardName, false);
            SetChromeVisible(RelationshipsCardName, false);
            SetChromeVisible(NearbyCardName, false);
            SetChromeVisible(PullCardName, false);
            SetChromeVisible(OverviewColumnName, false);
            SetChromeVisible("Exploration controls", false);
            SetChromeVisible(FollowChipName, false);
            SetChromeVisible("Interaction prompt", false);
            SetChromeVisible(CastRail.RootName, false);
            SetChromeVisible(CastRail.StripName, false);
            SetChromeVisible(CastRail.QuoteName, false);
        }

        /// <summary>
        /// Keeps a wide stage readable: the column its rows are laid in runs no wider than
        /// <paramref name="width"/>, centred in the frame, however wide the frame is.
        /// </summary>
        private void CapContent(float width)
        {
            if (content == null || modal == null) return;
            contentCap = width;
            int extra = Mathf.Max(0, Mathf.RoundToInt((modal.sizeDelta.x - 40f - 18f - 16f - width) * .5f));
            var layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout != null) layout.padding = new RectOffset(8 + extra, 8 + extra, layout.padding.top, layout.padding.bottom);
        }

        private void UncapContent()
        {
            contentCap = 0f;
            var layout = content != null ? content.GetComponent<VerticalLayoutGroup>() : null;
            if (layout != null) layout.padding = new RectOffset(8, 8, layout.padding.top, layout.padding.bottom);
        }

        /// <summary>The margin a full-frame screen keeps from the frame's edge.</summary>
        public const float FullFrameEdge = 24f;

        /// <summary>
        /// Where the briefing leaves the arena: the middle of the frame's free area, right of its
        /// sheet, in half-heights from the frame's centre - the unit a camera's vertical field of
        /// view projects into, so the director can stand the arena there at any aspect ratio.
        /// </summary>
        public float ArenaWindowOffset { get; private set; }

        private const float FrameGapUnits = 12f;

        /// <summary>
        /// A full-frame screen stands every piece of the house's chrome down: it is the screen. All
        /// but the status line, which is how a failed start, a cancelled walk to the stations or a
        /// rejected commit is said - it moves into the frame the screen leaves, at its foot.
        /// The last child of that name, not Find's first: Begin's rebuild leaves the last render's
        /// status line under the canvas, inactive, until the frame ends, ahead of the new one.
        /// </summary>
        private void HideChromeForFullFrame(float canvasWidth, float freeLeft)
        {
            RectTransform status = null;
            foreach (Transform child in canvas.transform)
            {
                if (child == modal) continue;
                child.gameObject.SetActive(false);
                if (child.name == "Status") status = (RectTransform)child;
            }
            if (status == null) return;
            // Unless a ceremony's card is saying what it would (UI-UX-PASS-PLAN V0).
            status.gameObject.SetActive(!StatusSaysTheCard);
            float room = canvasWidth - FullFrameEdge - freeLeft;
            float width = Mathf.Min(status.sizeDelta.x, Mathf.Max(200f, room));
            Anchor(status, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-FullFrameEdge, FullFrameEdge), new Vector2(width, status.sizeDelta.y));
        }

        /// <summary>The house event's card (mockup-04), at the resting text size.</summary>
        private const float HouseEventWidth = 760f;
        private const float HouseEventHeight = 400f;

        /// <summary>
        /// A house event (mockup-04): a card low in the middle of the frame, over the house rather
        /// than in place of it - what happened, the question and its answers, with the rest of the
        /// phase panel a scroll below them. It shared the nominations' band once, which is the
        /// whole width between the rail and the column and most of the height: the room the event
        /// happened in was behind it.
        /// </summary>
        private void HouseEventLayout(float canvasWidth, float canvasHeight)
        {
            // The stage's width; its top stays at about two-thirds of the frame, because the
            // camera stands the event's people above it and they are what the event is about.
            var stage = StageRect(canvasWidth, canvasHeight);
            float height = Mathf.Clamp(Mathf.Max(canvasHeight * .65f - ModalLift, HouseEventHeight * FontScale), 300f, stage.height);
            modal.anchorMin = modal.anchorMax = Vector2.zero;
            modal.pivot = Vector2.zero;
            modal.anchoredPosition = stage.position;
            modal.sizeDelta = new Vector2(stage.width, height);
            HideStageChrome();
            var hint = modal.Find("Panel control hint");
            if (hint != null) hint.gameObject.SetActive(false);
            if (modalScroll != null)
            {
                Stretch((RectTransform)modalScroll.transform, 20f, 72f, 20f, 14f);
                modalScroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>The diary's column (mockup-11), at the resting text size.</summary>
        private const float DiaryColumnWidth = 420f;
        /// <summary>The ballot's panel (mockup-08), at the resting text size.</summary>
        private const float BallotPanelWidth = 640f;
        private const float BallotPanelHeight = 520f;

        /// <summary>
        /// The diary's own page (mockup-11): a column down the right-hand side of the frame, where
        /// the right column's cards stand the rest of the time, with the player in the chair in the
        /// rest of it. The strip stays - the room is still in the house - and so does the rail.
        /// </summary>
        private void DiaryLayout(float canvasWidth, float canvasHeight)
        {
            // Under 35% of the frame at either text size: the diary is a column beside the chair.
            float column = diaryRoomColumn ? DiaryRoomColumnWidth : DiaryColumnWidth;
            float width = Mathf.Min(column * FontScale, canvasWidth * .345f, canvasWidth - LeftColumnX - RightColumnInset);
            float height = Mathf.Max(300f, canvasHeight - ModalLift - ActivityHeadroom);
            Anchor(modal, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-RightColumnInset, ModalLift), new Vector2(width, height));
            CompactClose();
            if (Compact) SetChromeVisible("Objective", false);
            SetChromeVisible(HouseVibeCardName, false);
            SetChromeVisible(ObjectivesCardName, false);
            SetChromeVisible(EpisodeDirector.LiveFeedCardName, false);
            SetChromeVisible(RecentEventsCardName, false);
            SetChromeVisible(RelationshipsCardName, false);
            SetChromeVisible(OverviewColumnName, false);
            SetChromeVisible("Exploration controls", false);
            SetChromeVisible(FollowChipName, false);
            SetChromeVisible("Interaction prompt", false);
            var hint = modal.Find("Panel control hint");
            if (hint != null) hint.gameObject.SetActive(false);
            if (modalScroll != null)
            {
                Stretch((RectTransform)modalScroll.transform, 14f, 76f, 12f, 14f);
                modalScroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>
        /// The eviction vote in the diary (mockup-08): a panel across the top of the room, centred
        /// between the rail and the right column, with the player in the chair below it. The right
        /// column stays up - the mockup keeps the house's feed beside the ballot - and so does the
        /// strip, the faces the vote is about.
        /// </summary>
        private void BallotLayout(float canvasWidth, float canvasHeight)
        {
            // The stage's width, from under the top bar down to the speech bar the vote is cast
            // over, in the strip's band.
            var stage = StageRect(canvasWidth, canvasHeight);
            // Never below the status line, which carries the diary's messages while the vote is up.
            float floor = Mathf.Max(CastRail.Bottom + (6f + CastRail.Height - 14f) * FontScale + 12f, ModalLift);
            float height = Mathf.Max(300f, canvasHeight - ActivityHeadroom - floor);
            modal.anchorMin = modal.anchorMax = new Vector2(0f, 1f);
            modal.pivot = new Vector2(0f, 1f);
            modal.anchoredPosition = new Vector2(stage.x, -ActivityHeadroom);
            modal.sizeDelta = new Vector2(stage.width, height);
            HideStageChrome();
            CapContent(StageColumnWidth * FontScale);
            // Near opaque: the diary's neon sign stands right behind the ballot, and through the
            // HUD's .94 glass it read as a second heading beside the first - six percent of a sign
            // that bright is still a word.
            var frame = modal.GetComponent<Image>();
            if (frame != null) { var ground = frame.color; frame.color = new Color(ground.r, ground.g, ground.b, Mathf.Max(ground.a, .99f)); }
            SetChromeVisible("Exploration controls", false);
            SetChromeVisible(FollowChipName, false);
            SetChromeVisible("Interaction prompt", false);
            var hint = modal.Find("Panel control hint");
            if (hint != null) hint.gameObject.SetActive(false);
            if (modalScroll != null)
            {
                Stretch((RectTransform)modalScroll.transform, 16f, 60f, 16f, 14f);
                modalScroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>
        /// The settings (a menu, not a beat of the week): a tall panel down the middle of the free
        /// area between the rail and the right column. In the docked panel's 300 units it showed
        /// four of its twenty rows, and the rest were a scroll nobody knew was there.
        /// </summary>
        private void SettingsLayout(float canvasWidth, float canvasHeight)
        {
            var stage = StageRect(canvasWidth, canvasHeight);
            modal.anchorMin = modal.anchorMax = new Vector2(0f, 0f);
            modal.pivot = new Vector2(0f, 0f);
            modal.anchoredPosition = stage.position;
            modal.sizeDelta = stage.size;
            CompactClose();
            HideStageChrome();
            CapContent(SettingsColumnWidth * FontScale);
            var hint = modal.Find("Panel control hint");
            if (hint != null) hint.gameObject.SetActive(false);
            if (modalScroll != null)
            {
                Stretch((RectTransform)modalScroll.transform, 16f, 76f, 16f, 14f);
                modalScroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>
        /// A competition is being played over the house: the docked panel has nothing to say and the
        /// right column's cards would sit under the game's own. The top bar, the rail, the strip and
        /// the caption stay - the frame the game is played in.
        /// </summary>
        public void StandAsideForPlay()
        {
            if (modal != null) modal.gameObject.SetActive(false);
            SetChromeVisible(EpisodeDirector.LiveFeedCardName, false);
            SetChromeVisible(RecentEventsCardName, false);
            SetChromeVisible(RelationshipsCardName, false);
            SetChromeVisible(HouseVibeCardName, false);
            SetChromeVisible(ObjectivesCardName, false);
            SetChromeVisible("Exploration controls", false);
            SetChromeVisible("Interaction prompt", false);
            SetChromeVisible(FollowChipName, false);
        }

        private void SetChromeVisible(string name, bool visible)
        {
            foreach (var rect in canvas.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name && rect != modal) rect.gameObject.SetActive(visible);
            MarkChromeChanged();
        }

        private void BuildExplorationHelp()
        {
            if (explorationHelp != null)
            {
                explorationHelp.gameObject.SetActive(false);
                Destroy(explorationHelp.gameObject);
            }
            // Collapsed, a pill the size of its words: the mockups have no controls box at all, and
            // a 285-wide panel spent a whole corner of the frame on a reference card most players
            // read once. Expanded, the card it always was, growing up from the same corner.
            float height = helpExpanded ? 180f : 40f;
            float width = helpExpanded ? HelpWidth : 196f;
            explorationHelp = Chrome("Exploration controls", canvas.transform);
            // Stays bottom right, and rises with the rest of the floor: the cast strip owns the band
            // under it now. Moving it to the empty bottom-LEFT band looks right with nothing open and
            // is wrong the moment anything is - the activity layout claims that band exactly, so it
            // would trade an overlap with the right column for an overlap with the modal. It cannot
            // rise any further either: the vibe card above it ends fifteen units from its expanded
            // top on the canvas the tests measure. The column is what grew - see RecentEventRows.
            Anchor(explorationHelp,new Vector2(1,0),new Vector2(1,0),new Vector2(-24,HelpBottom),new Vector2(width,height));
            var toggle = FixedButton(explorationHelp, helpExpanded ? "Hide controls" : "Help · controls",
                helpExpanded ? new Vector2(10,-8) : new Vector2(4,-3),
                helpExpanded ? new Vector2(265,38) : new Vector2(width - 8f,34), () =>
                {
                    helpExpanded = !helpExpanded;
                    BuildExplorationHelp();
                    preferredSelection = helpExpanded ? "Hide controls" : "Help · controls";
                    restoreSelection = true;
                });
            // The pill's words at the rail's size, with the bulb in front of them.
            var words = toggle.GetComponentInChildren<TMP_Text>();
            words.fontSize = Mathf.RoundToInt(15 * FontScale); words.fontSizeMax = words.fontSize;
            words.fontSizeMin = Mathf.Min(12f, words.fontSize);
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            if (medium != null) words.font = medium;
            if (!helpExpanded && HudPrimitives.Glyph("Help mark", toggle.transform, "bulb", UiTheme.Joke, new Vector2(10f, -8f), 18f) != null)
                Stretch(words.rectTransform, 34, 5, 10, 5);
            // The card's words follow the device last used (PLAN A, A4): the keyboard's as they
            // always read, or the pad's buttons, swapped in place when the device changes.
            helpText = helpExpanded
                ? FixedText(explorationHelp, InputGlossary.HelpCard(padHints), 16, Paper, new Vector2(14, -50), new Vector2(258, 122))
                : null;
        }
    }
}
