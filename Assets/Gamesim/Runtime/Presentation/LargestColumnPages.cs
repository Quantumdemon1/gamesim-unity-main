using System;
using System.Collections.Generic;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The largest text's pages for a screen that is a column of rows (the owner's contract of
    /// 2026-10-15; <see cref="LargestPages"/> says which size may page, and EpisodeHud.LargestPages.cs
    /// draws the pager). A column's units - a row, or one line of a grid of cards - are dealt whole
    /// and in reading order onto pages of a height known before a page is shown, as
    /// <see cref="LargestPages.Breaks"/> deals a screen's sections, with two things a column needs:
    /// the gap before each unit is its own (the column's spacing between two rows, a grid's own
    /// between two of its lines, and none before the first unit of a page), and a unit may be held
    /// with the one after it, so a heading never ends a page without the first line of what it
    /// heads. Unity-free and pure.
    /// </summary>
    public static class LargestColumnPages
    {
        /// <summary>
        /// Deals units of <paramref name="heights"/>, in order and whole, onto pages of
        /// <paramref name="room"/>: <paramref name="gapsBefore"/>[i] stands between unit i and the
        /// one before it on the same page, and half a unit of rounding is allowed. A unit marked in
        /// <paramref name="keepWithNext"/> starts the next page rather than end its own without the
        /// unit after it. A unit taller than a page has a page of its own, which then needs the
        /// panel's scroll - a screen should not hand one in. Returns the page of each unit, from
        /// zero; <see cref="LargestPages.Count"/> counts them.
        /// </summary>
        public static int[] Deal(IList<float> heights, IList<float> gapsBefore, IList<bool> keepWithNext, float room)
        {
            int count = heights?.Count ?? 0;
            var pages = new int[count];
            int page = 0;
            float used = 0f;
            bool empty = true;
            for (int i = 0; i < count; i++)
            {
                float height = Height(heights, i);
                float need = (empty ? 0f : Gap(gapsBefore, i)) + height;
                // Held with the next: this unit stays on the page only where the next one fits too.
                float held = need;
                if (Keeps(keepWithNext, i) && i + 1 < count) held += Gap(gapsBefore, i + 1) + Height(heights, i + 1);
                if (!empty && used + held > room + .5f) { page++; used = height; }
                else used += need;
                empty = false;
                pages[i] = page;
            }
            return pages;
        }

        private static float Height(IList<float> heights, int i) => Math.Max(0f, heights[i]);

        private static float Gap(IList<float> gaps, int i) => gaps != null && i < gaps.Count ? Math.Max(0f, gaps[i]) : 0f;

        private static bool Keeps(IList<bool> keep, int i) => keep != null && i < keep.Count && keep[i];
    }
}
