using System;
using System.Collections.Generic;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The overview's dashboard (the owner's review of the mockups, 2026-09-28): the strategic
    /// briefing, drawn over the house seen from above. The week's roles on a band; the house at a
    /// glance, each face with its role and where you stand; the plays, the threads and the phase's
    /// rules beside them; and the smart moves for where things stand, as tiles that say their value
    /// and their risk. Information, where the Free Time screen is the decision.
    ///
    /// <para>A chrome piece, not a panel: the overview is a camera mode, nothing pauses, and the
    /// room list the overview has always kept stands beside it. The first touch of the camera ends
    /// the overview and the dashboard with it.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        public const string DashboardName = "Overview dashboard", GlanceStripName = "Glance strip", RecommendedTilesName = "Recommended moves";
        public const string StrategicContextName = "Strategic context";

        /// <summary>What the dashboard says, as the director has worked it out from the committed state.</summary>
        public sealed class DashboardView
        {
            public string Title, Headline, Hint, PhaseRule, RecommendedHint;
            public List<string> GlanceIds = new List<string>();
            public Action<string> PickGlance;
            public List<(string title, string line)> Plays = new List<(string, string)>();
            public List<(string title, string line)> Threads = new List<(string, string)>();
            public List<MoveTile> Recommended = new List<MoveTile>();
        }

        private void OverviewDashboard(EpisodeState state, DashboardView view)
        {
            if (state == null || view == null || canvas == null) return;
            var root = (RectTransform)canvas.transform;
            var bounds = root.rect;
            float canvasWidth = bounds.width > 0 ? bounds.width : 1600f, canvasHeight = bounds.height > 0 ? bounds.height : 900f;
            float left = LeftColumnX, right = RightColumnInset + RightColumnWidth + RightColumnGap;
            float top = RightColumnTop, bottom = ModalLift;
            float width = Mathf.Max(420f, canvasWidth - left - right), height = Mathf.Max(300f, canvasHeight - top - bottom);
            var panel = Chrome(DashboardName, canvas.transform);
            Anchor(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(left, -top), new Vector2(width, height));
            var ground = panel.GetComponent<Image>();
            if (ground != null) { var c = ground.color; ground.color = new Color(c.r, c.g, c.b, .9f); }

            // The briefing scrolls: a big house's glance and its moves run past a short frame.
            var scrollRoot = new GameObject("Dashboard scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollRoot.transform.SetParent(panel, false);
            var scrollRect = (RectTransform)scrollRoot.transform;
            // Ten over and under, not fourteen, and twelve of padding at the foot: the briefing's
            // frame is short, and every unit of it is a unit of the recommended row.
            Stretch(scrollRect, 16f, 10f, 16f, 10f);
            var viewport = Panel("Viewport", scrollRect, new Color(0f, 0f, 0f, 0f));
            Stretch(viewport, 0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var column = new GameObject("Dashboard content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            column.SetParent(viewport, false);
            column.anchorMin = new Vector2(0f, 1f); column.anchorMax = Vector2.one; column.pivot = new Vector2(.5f, 1f); column.sizeDelta = Vector2.zero;
            var layout = column.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 6, 12); layout.spacing = 12;
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            column.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = scrollRoot.GetComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = column; scroll.horizontal = false; scroll.vertical = true;
            scroll.scrollSensitivity = 30f; scroll.movementType = ScrollRect.MovementType.Clamped;

            PushContent(column, width - 32f - 16f);
            ScreenHead(view.Title, view.Headline, view.Hint);
            RolesBanner(state);
            BeginColumns(320f);
            // The legend on the head's own line, and the glance's photos no taller than a chip's:
            // with the map's row the briefing's main column ran 115 units past the panel's foot at
            // the resting size on the 16:9 frame, and the recommended moves - the point of the
            // briefing - were the rows cut (the play sweep's row 12). The briefing still scrolls at
            // the larger text and for a house too wide for one row of faces.
            SectionHead("people", "HOUSEGUESTS AT A GLANCE", "Where you stand with everyone still in the house.");
            StandingLegendIn(content.childCount > 0 ? content.GetChild(content.childCount - 1) as RectTransform : null);
            var strip = content;
            HouseguestStrip(view.GlanceIds, null, view.PickGlance ?? (id => { }), photoCap: GlancePhotoHeight);
            if (strip.childCount > 0) strip.GetChild(strip.childCount - 1).name = GlanceStripName;
            SectionHead("bulb", "RECOMMENDED ACTIONS", view.RecommendedHint);
            Tiles(RecommendedTilesName, view.Recommended, TileStyle.Cards, RecommendedCardHeight);
            SideColumn();
            BeginSideCard(StrategicContextName, "STRATEGIC CONTEXT");
            ContextBlock("PLAYS", view.Plays, "No play in motion.");
            ContextBlock("THREADS", view.Threads, "No threads yet.");
            CardLine("PHASE RULES", 12, UiTheme.Heading, UiTheme.Weight.SemiBold).characterSpacing = 3f;
            CardLine(view.PhaseRule, 13, UiTheme.Muted);
            EndSideCard();
            EndColumns();
            PopContent();

            // The map is under the briefing, and the map is a way there: the panel's corner puts the
            // briefing away and leaves the labelled house with its chips and its floor to click.
            // In the corner, out of the flow and over it, as every other screen keeps its way out;
            // as a row of its own it was 69 units of the foot the recommended moves did not have.
            // In the button's own chrome: its ground and its edge are FixedButton's (the kit's
            // secondary chrome once T0 lands), never painted over here.
            var map = FixedButton(panel, EpisodeDirector.ShowMapCaption, Vector2.zero, new Vector2(MapButtonWidth, MapButtonHeight), director.HideBriefing);
            var mapRect = (RectTransform)map.transform;
            Anchor(mapRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -14f), new Vector2(MapButtonWidth, MapButtonHeight));
            var mapWords = map.GetComponentInChildren<TMPro.TMP_Text>();
            if (mapWords != null) { mapWords.alignment = TMPro.TextAlignmentOptions.Center; mapWords.color = Accent; }
        }

        /// <summary>The way back to the map, in the briefing's corner: wide enough for its words at the larger text, and a row's height.</summary>
        private const float MapButtonWidth = 200f, MapButtonHeight = 40f;

        /// <summary>
        /// The glance's photos, no taller than this at the resting size: the strip's cards are
        /// sized to the column, and six across a wide column the photo grew to 99 and the row to
        /// 151. The chip's face is 72, and a face is what the photo is for.
        /// </summary>
        private const float GlancePhotoHeight = 72f;

        /// <summary>
        /// The recommended cards' height at the resting size: the glyph and the caption, the two
        /// chips, two lines of the description and the foot, which is 136 of the cards' own 150.
        /// </summary>
        private const float RecommendedCardHeight = 136f;

        /// <summary>One block of the strategic context: its name, then a line each, or one word when it has none.</summary>
        private void ContextBlock(string heading, List<(string title, string line)> rows, string none)
        {
            CardLine(heading, 12, UiTheme.Heading, UiTheme.Weight.SemiBold).characterSpacing = 3f;
            if (rows == null || rows.Count == 0) { CardLine(none, 13, UiTheme.Muted); return; }
            foreach (var row in rows)
            {
                CardLine(row.title, 14, Paper, UiTheme.Weight.SemiBold);
                if (!string.IsNullOrEmpty(row.line)) CardLine(row.line, 12, UiTheme.Muted);
            }
        }
    }
}
