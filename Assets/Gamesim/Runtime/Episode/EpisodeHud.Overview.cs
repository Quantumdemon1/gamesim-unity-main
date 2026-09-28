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
            Stretch(scrollRect, 16f, 14f, 16f, 14f);
            var viewport = Panel("Viewport", scrollRect, new Color(0f, 0f, 0f, 0f));
            Stretch(viewport, 0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var column = new GameObject("Dashboard content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            column.SetParent(viewport, false);
            column.anchorMin = new Vector2(0f, 1f); column.anchorMax = Vector2.one; column.pivot = new Vector2(.5f, 1f); column.sizeDelta = Vector2.zero;
            var layout = column.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 6, 18); layout.spacing = 12;
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            column.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = scrollRoot.GetComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = column; scroll.horizontal = false; scroll.vertical = true;
            scroll.scrollSensitivity = 30f; scroll.movementType = ScrollRect.MovementType.Clamped;

            PushContent(column, width - 32f - 16f);
            ScreenHead(view.Title, view.Headline, view.Hint);
            // The map is under the briefing, and the map is a way there: one row puts the briefing
            // away and leaves the labelled house with its chips and its floor to click.
            Action(EpisodeDirector.ShowMapCaption, director.HideBriefing);
            RolesBanner(state);
            BeginColumns(320f);
            SectionHead("people", "HOUSEGUESTS AT A GLANCE", "Where you stand with everyone still in the house.");
            StandingLegend();
            var strip = content;
            HouseguestStrip(view.GlanceIds, null, view.PickGlance ?? (id => { }));
            if (strip.childCount > 0) strip.GetChild(strip.childCount - 1).name = GlanceStripName;
            SectionHead("bulb", "RECOMMENDED ACTIONS", view.RecommendedHint);
            Tiles(RecommendedTilesName, view.Recommended, TileStyle.Cards);
            SideColumn();
            BeginSideCard(StrategicContextName, "STRATEGIC CONTEXT");
            ContextBlock("PLAYS", view.Plays, "No play in motion.");
            ContextBlock("THREADS", view.Threads, "No threads yet.");
            CardLine("PHASE RULES", 12, UiTheme.Heading, UiTheme.Weight.SemiBold).characterSpacing = 3f;
            CardLine(view.PhaseRule, 13, UiTheme.Muted);
            EndSideCard();
            EndColumns();
            PopContent();
        }

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
