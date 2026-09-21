using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeHud
    {
        /// <summary>Activities own their layout; they share controls, focus and save semantics.</summary>
        public enum ActivityLayout { Standard, Relationships, Conversation, Competition, Creation, Diary }

        private ActivityLayout activityLayout;
        private RectTransform relationshipRoot;
        private bool helpExpanded = true;
        private RectTransform explorationHelp;

        public ActivityLayout CurrentActivityLayout => activityLayout;
        public bool Compact { get; set; }
        public RectTransform ActivityContent => content;
        /// <summary>Reference-canvas bounds left clear for world captions by the visible chrome.</summary>
        public Rect WorldCaptionSafeBounds
        {
            get
            {
                if(canvas==null)return new Rect(24,100,1552,696);
                var root=(RectTransform)canvas.transform;var size=root.rect.size;
                // LeftColumnX says this once; this line used to say it again by hand.
                float left=LeftColumnX,right=size.x-24f,bottom=100f,top=size.y-104f;
                foreach(var item in root.GetComponentsInChildren<RectTransform>())
                {
                    bool leftCard=item.name=="Objective" || item.name==HouseVibeCardName;
                    bool rightCard=item.name==EpisodeDirector.LiveFeedCardName || item.name==RecentEventsCardName
                        || item.name==OverviewColumnName || item.name=="Exploration controls";
                    bool bottomCard=item.name=="Interaction prompt";
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
            float left = LeftColumnX;
            float right = 24f;
            float availableWidth = canvasWidth - left - right;
            float width = Mathf.Min(layout == ActivityLayout.Diary ? 600f : layout == ActivityLayout.Conversation ? 1040f : 1180f, availableWidth);
            if (layout == ActivityLayout.Relationships) width = availableWidth;
            float height = layout == ActivityLayout.Conversation
                ? Mathf.Min(540f * FontScale, canvasHeight - 196f)
                : canvasHeight - 196f;
            Anchor(modal, new Vector2(1,0), new Vector2(1,0), new Vector2(-right,100f), new Vector2(width,height));

            // Context cards return when the player returns to exploration. They remain available
            // through the notebook and the persistent top navigation while an activity uses this space.
            SetChromeVisible("Objective", false);
            SetChromeVisible(HouseVibeCardName, false);
            SetChromeVisible(EpisodeDirector.LiveFeedCardName, false);
            SetChromeVisible(RecentEventsCardName, false);
            SetChromeVisible(OverviewColumnName, false);
            SetChromeVisible("Exploration controls", false);
            SetChromeVisible(FollowChipName, false);
            SetChromeVisible("Interaction prompt", false);

            var hint = modal.Find("Panel control hint");
            if (hint != null) hint.gameObject.SetActive(false);
            if (modalScroll != null)
            {
                Stretch((RectTransform)modalScroll.transform, 20f, 76f, 20f, 16f);
                modalScroll.verticalNormalizedPosition = 1f;
            }
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
            float height = helpExpanded ? 180f : 54f;
            explorationHelp = Chrome("Exploration controls", canvas.transform);
            Anchor(explorationHelp,new Vector2(1,0),new Vector2(1,0),new Vector2(-24,100),new Vector2(285,height));
            FixedButton(explorationHelp, helpExpanded ? "Hide controls" : "Help · controls",
                new Vector2(10,-8),new Vector2(265,38), () =>
                {
                    helpExpanded = !helpExpanded;
                    BuildExplorationHelp();
                    preferredSelection = helpExpanded ? "Hide controls" : "Help · controls";
                    restoreSelection = true;
                });
            if (helpExpanded)
                FixedText(explorationHelp,
                    "Click a houseguest: follow\nClick floor: walk  ·  F: recenter\nWASD/arrows: pan  ·  Wheel: zoom\nRight-drag: orbit · Mid-drag: pan\nR: diary · E: interact · Esc: close",
                    16,Paper,new Vector2(14,-50),new Vector2(258,122));
        }
    }
}
