using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeHud
    {
        /// <summary>Activities own their layout; they share controls, focus and save semantics.</summary>
        public enum ActivityLayout { Standard, Relationships, Conversation, Competition, Creation, Diary, Nominations, HouseEvent, Ballot }

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
                        || item.name==OverviewColumnName || item.name=="Exploration controls" || item.name==HouseVibeCardName
                        || item.name==RelationshipsCardName;
                    // The floor grew a band. The strip and the caption above it are both fixed
                    // chrome a world bubble must clear, and the caption used to be low enough that
                    // the default floor covered it.
                    bool bottomCard=item.name=="Interaction prompt" || item.name==CastRail.RootName
                        || item.name=="Status" || item.name==CastRail.StripName;
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
            if (modal == null || canvas == null) return;
            activityLayout = layout;
            if (layout == ActivityLayout.Standard) return;

            var bounds = ((RectTransform)canvas.transform).rect;
            float canvasWidth = bounds.width > 0 ? bounds.width : 1600f;
            float canvasHeight = bounds.height > 0 ? bounds.height : 900f;
            if (layout == ActivityLayout.Conversation) { ConversationLayout(canvasWidth, canvasHeight); return; }
            // Both are decisions about the people in the house, and both take the same band.
            if (layout == ActivityLayout.Nominations || layout == ActivityLayout.HouseEvent) { NominationsLayout(canvasWidth, canvasHeight); return; }
            if (layout == ActivityLayout.Diary) { DiaryLayout(canvasWidth, canvasHeight); return; }
            if (layout == ActivityLayout.Ballot) { BallotLayout(canvasWidth, canvasHeight); return; }
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

            // Context cards return when the player returns to exploration. They remain available
            // through the notebook and the persistent top navigation while an activity uses this space.
            // The objective is a chip on the top bar now and the travel buttons are rows of the
            // rail, both clear of every activity's frame; only the compact HUD's objective is a
            // left-column card an activity would sit on.
            if (Compact) SetChromeVisible("Objective", false);
            SetChromeVisible(HouseVibeCardName, false);
            SetChromeVisible(EpisodeDirector.LiveFeedCardName, false);
            SetChromeVisible(RecentEventsCardName, false);
            SetChromeVisible(RelationshipsCardName, false);
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

        /// <summary>
        /// A conversation (mockup-12) keeps the house around it: the top bar, the rail, the right
        /// column - where the relationships card lifts the person being talked to - and the cast
        /// strip all stay. The panel becomes a transparent frame between the rail and the column,
        /// holding a glass column on its left for the speaker, what they say and every way to talk
        /// that is not a petal, and the dial in the rest of it, below the pair.
        ///
        /// <para>The frame keeps its name and its raycast ground: the keyboard ring is scoped to
        /// 'Episode panel', and a click between the column and the dial must not fall through to
        /// the floor and walk the player away mid-sentence.</para>
        /// </summary>
        private void ConversationLayout(float canvasWidth, float canvasHeight)
        {
            float width = Mathf.Max(420f, canvasWidth - LeftColumnX - RightColumnInset - RightColumnWidth - 12f);
            float height = Mathf.Max(300f, canvasHeight - ModalLift - ActivityHeadroom - 12f);
            modal.anchorMin = modal.anchorMax = Vector2.zero;
            modal.pivot = Vector2.zero;
            modal.anchoredPosition = new Vector2(LeftColumnX, ModalLift);
            modal.sizeDelta = new Vector2(width, height);
            var frame = modal.GetComponent<Image>();
            if (frame != null) frame.color = new Color(0f, 0f, 0f, 0f);
            foreach (Transform child in modal)
                if (child.name == "Border" || child.name == "Glow" || child.name == "Phase band" || child.name == "Panel control hint")
                    child.gameObject.SetActive(false);

            SetChromeVisible(EpisodeDirector.LiveFeedCardName, false);
            SetChromeVisible(RecentEventsCardName, false);
            SetChromeVisible(OverviewColumnName, false);
            SetChromeVisible(FollowChipName, false);
            SetChromeVisible("Interaction prompt", false);

            if (conversationColumn != null) return;
            conversationColumn = Chrome(ConversationColumnName, modal);
            conversationColumn.anchorMin = new Vector2(0f, 0f); conversationColumn.anchorMax = new Vector2(0f, 1f);
            conversationColumn.pivot = new Vector2(0f, .5f);
            conversationColumn.anchoredPosition = Vector2.zero;
            conversationColumn.sizeDelta = new Vector2(Mathf.Min(340f, width * .40f), 0f);
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
            float width = Mathf.Max(420f, canvasWidth - LeftColumnX - RightColumnInset - RightColumnWidth - 12f);
            float height = Mathf.Min(470f * FontScale, Mathf.Max(300f, canvasHeight - ModalLift - ActivityHeadroom - 12f));
            modal.anchorMin = modal.anchorMax = Vector2.zero;
            modal.pivot = Vector2.zero;
            modal.anchoredPosition = new Vector2(LeftColumnX, ModalLift);
            modal.sizeDelta = new Vector2(width, height);
            SetChromeVisible(FollowChipName, false);
            SetChromeVisible("Interaction prompt", false);
            var hint = modal.Find("Panel control hint");
            if (hint != null) hint.gameObject.SetActive(false);
            if (modalScroll != null)
            {
                Stretch((RectTransform)modalScroll.transform, 20f, 72f, 20f, 14f);
                modalScroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>The diary's column (mockup-11), at the resting text size.</summary>
        private const float DiaryColumnWidth = 384f;
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
            float width = Mathf.Min(DiaryColumnWidth * FontScale, canvasWidth - LeftColumnX - RightColumnInset);
            float height = Mathf.Max(300f, canvasHeight - ModalLift - ActivityHeadroom);
            Anchor(modal, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-RightColumnInset, ModalLift), new Vector2(width, height));
            CompactClose();
            if (Compact) SetChromeVisible("Objective", false);
            SetChromeVisible(HouseVibeCardName, false);
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
            float right = RightColumnInset + RightColumnWidth + 16f;
            float free = Mathf.Max(420f, canvasWidth - LeftColumnX - right);
            float width = Mathf.Min(BallotPanelWidth * FontScale, free);
            float height = Mathf.Min(BallotPanelHeight * FontScale, Mathf.Max(300f, canvasHeight - ModalLift - ActivityHeadroom));
            modal.anchorMin = modal.anchorMax = new Vector2(0f, 1f);
            modal.pivot = new Vector2(.5f, 1f);
            modal.anchoredPosition = new Vector2(LeftColumnX + free * .5f, -ActivityHeadroom);
            modal.sizeDelta = new Vector2(width, height);
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
            SetChromeVisible("Exploration controls", false);
            SetChromeVisible("Interaction prompt", false);
            SetChromeVisible(FollowChipName, false);
        }

        private void SetChromeVisible(string name, bool visible)
        {
            foreach (var rect in canvas.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name && rect != modal) rect.gameObject.SetActive(visible);
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
            if (helpExpanded)
                FixedText(explorationHelp,
                    "Click a houseguest: talk\nClick floor: walk  ·  F: recenter\nWASD/arrows: pan  ·  Wheel: zoom\nRight-drag: orbit · Mid-drag: pan\nR: diary · E: interact · Esc: close",
                    16,Paper,new Vector2(14,-50),new Vector2(258,122));
        }
    }
}
