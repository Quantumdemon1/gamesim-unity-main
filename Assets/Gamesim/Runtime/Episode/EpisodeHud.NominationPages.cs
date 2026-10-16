using System;
using System.Collections.Generic;
using Gamesim.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The nomination's steps at the largest text (the owner's contract of 2026-10-15). Each step is
    /// a column of rows sized to the height the stage leaves it (EpisodeHud.NominationScreen.cs), and
    /// at the standard and the larger text every one holds on one page, laid as it always was:
    /// nothing here runs there. At the largest a step whose rows cannot all fit that height - sixteen
    /// faces at the ceremony, the Head of Household's sixteen candidates, the backdoor plan's fifteen
    /// aims, each grid at the least its cards read at - is dealt onto pages of it instead of growing
    /// a scroll: its rows, and a grid's lines one by one, whole and in reading order
    /// (<see cref="LargestColumnPages.Deal"/>), a heading held with the first line it heads, and the
    /// shared pager (EpisodeHud.LargestPages.cs) at each page's foot - "Page 1 of 2", "Previous
    /// page", "Next page" - where it stands on every page.
    ///
    /// <para>Nothing is built smaller for it and nothing is left out: a card keeps the size the
    /// one-page layout gave it (its least), every face and every candidate is on exactly one page,
    /// and the pinned footer - the way on, its secondary and the strip - is the same on every page.
    /// The pager is not where the panel opens: the step's first control does, else the way on
    /// (<see cref="IsLargestPagerControl"/>). Which page is shown is the pager's view state: a
    /// rebuild keeps it, another step starts on its first, and turning one commits nothing.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The row at a paged step's foot that holds the pager, by the name a test finds it by.</summary>
        public const string StepPagesRowName = "Step pages";

        /// <summary>One unit of a paged step: a row of the column, or one line of a grid row's cards, and its page.</summary>
        private sealed class StepUnit
        {
            public RectTransform Row;
            public int Line = -1;
            public int Page;
        }

        /// <summary>This render's paged step: its units, the page shown and the pager's row; null when it is one page.</summary>
        private List<StepUnit> stepUnits;
        private RectTransform stepPagesRow;
        private int stepPage;

        /// <summary>
        /// Deals the step just built onto the largest text's pages when it cannot fit the stage on
        /// one, shows the page kept for <paramref name="screen"/>, and stands the pager at its foot,
        /// turning through <paramref name="redraw"/>. False, with nothing touched, at the other text
        /// sizes and wherever the step fits. Call it once the step's rows are all built.
        /// </summary>
        public bool PageNominationStep(string screen, Action redraw)
        {
            stepUnits = null; stepPagesRow = null;
            if (!LargestTextPages || content == null || modal == null || string.IsNullOrEmpty(screen)) return false;
            var column = content.GetComponent<VerticalLayoutGroup>();
            float spacing = column != null ? column.spacing : 12f, width = ContentWidth();
            // The room NominationRoomLeft lays every step to, with the margin it keeps.
            float room = NominationStepRoom() - 2f, total = 0f;
            var units = new List<StepUnit>();
            var heights = new List<float>();
            var gaps = new List<float>();
            var keep = new List<bool>();
            foreach (Transform child in content)
            {
                if (!child.gameObject.activeSelf || !(child is RectTransform row)) continue;
                float height = ColumnRowHeight(row, width);
                total += (units.Count > 0 ? spacing : 0f) + height;
                var grid = row.GetComponent<GridLayoutGroup>();
                int cells = grid != null ? ActiveChildren(row) : 0;
                if (grid != null && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount && grid.constraintCount > 0
                    && cells > 0 && row.GetComponent<LayoutElement>() != null)
                {
                    // The row before a grid is what heads it: never the last thing on a page.
                    if (units.Count > 0 && units[units.Count - 1].Line < 0) keep[keep.Count - 1] = true;
                    int lines = Mathf.CeilToInt(cells / (float)grid.constraintCount);
                    for (int line = 0; line < lines; line++)
                    {
                        units.Add(new StepUnit { Row = row, Line = line });
                        heights.Add(grid.cellSize.y);
                        gaps.Add(line == 0 ? spacing : grid.spacing.y);
                        keep.Add(false);
                    }
                    continue;
                }
                units.Add(new StepUnit { Row = row });
                heights.Add(height);
                gaps.Add(spacing);
                keep.Add(false);
            }
            // One page, laid as it always was, wherever the step fits the stage.
            if (units.Count == 0 || total <= room + .5f) return false;
            float pager = LargestPagerPill * FontScale;
            var breaks = LargestColumnPages.Deal(heights, gaps, keep, room - spacing - pager);
            int pages = LargestPages.Count(breaks);
            // A single unit taller than the stage is the panel's scroll's, as it always was.
            if (pages <= 1) return false;
            for (int i = 0; i < units.Count; i++) units[i].Page = breaks[i];
            int page = LargestPageOf(screen, pages);
            stepUnits = units;
            stepPage = page;
            stepPagesRow = new GameObject(StepPagesRowName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            stepPagesRow.SetParent(content, false);
            ShowStepPage(false);
            // The pager at the page's foot - the row takes what the page leaves - so it stands at the
            // same place on every page and the press that turned one can turn it back.
            float used = 0f;
            bool first = true;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].Page != page) continue;
                used += (first ? 0f : gaps[i]) + heights[i];
                first = false;
            }
            float foot = Mathf.Max(pager, room - used - spacing);
            var element = stepPagesRow.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = foot;
            LargestPager(stepPagesRow, 0f, foot - pager, width, screen, page, pages, redraw);
            return true;
        }

        /// <summary>
        /// Shows the paged step's page: each row on it, and of a grid only the lines on it, sized to
        /// them. <paramref name="comparing"/> is the picker's comparison, which takes the grid's place
        /// in the step without a render: the rows that head the step stay, nothing past them, and no
        /// pager - the comparison is one view, not a page of the grid.
        /// </summary>
        private void ShowStepPage(bool comparing)
        {
            if (stepUnits == null) return;
            int firstGrid = stepUnits.FindIndex(unit => unit.Line >= 0);
            var lines = new Dictionary<RectTransform, List<int>>();
            for (int i = 0; i < stepUnits.Count; i++)
            {
                var unit = stepUnits[i];
                if (unit.Row == null) continue;
                if (unit.Line < 0)
                {
                    bool before = firstGrid < 0 || i < firstGrid;
                    unit.Row.gameObject.SetActive(comparing ? before : unit.Page == stepPage);
                    continue;
                }
                if (!lines.TryGetValue(unit.Row, out var shown)) lines[unit.Row] = shown = new List<int>();
                if (!comparing && unit.Page == stepPage) shown.Add(unit.Line);
            }
            foreach (var pair in lines)
            {
                var row = pair.Key;
                var grid = row.GetComponent<GridLayoutGroup>();
                int across = Mathf.Max(1, grid.constraintCount);
                for (int i = 0; i < row.childCount; i++) row.GetChild(i).gameObject.SetActive(pair.Value.Contains(i / across));
                int count = pair.Value.Count;
                var element = row.GetComponent<LayoutElement>();
                element.minHeight = element.preferredHeight = count > 0 ? count * grid.cellSize.y + (count - 1) * grid.spacing.y : 0f;
                row.gameObject.SetActive(count > 0);
            }
            if (stepPagesRow != null) stepPagesRow.gameObject.SetActive(!comparing);
        }

        /// <summary>
        /// The picker's comparison swapped in or out in place: on a paged step, the page is shown
        /// again around it. Nothing when the step is one page, or this render's step is gone.
        /// </summary>
        private void StepComparing(bool comparing)
        {
            if (stepUnits == null || stepPagesRow == null || stepPagesRow.parent != content) return;
            ShowStepPage(comparing);
        }

        /// <summary>Whether a control is one of the largest text's pager's: never the control a panel opens on.</summary>
        private static bool IsLargestPagerControl(Selectable item) =>
            item != null && item.transform.parent != null && item.transform.parent.name == LargestPagerName;

        private static int ActiveChildren(Transform row)
        {
            int count = 0;
            foreach (Transform child in row) if (child.gameObject.activeSelf) count++;
            return count;
        }
    }
}
