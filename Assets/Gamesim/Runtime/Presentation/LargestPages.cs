using System;
using System.Collections.Generic;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The largest text's pages, as arithmetic (the owner's contract of 2026-10-15; the pager that
    /// draws them is EpisodeHud.LargestPages.cs): which text size may page, and how a screen's whole
    /// sections are dealt onto pages of a height known before anything is built. Unity-free and
    /// pure, so every screen family deals its sections the same way and a test pins it in seconds.
    /// </summary>
    public static class LargestPages
    {
        /// <summary>The text scale above which a screen may page: the largest text's 1.4, never the larger's 1.2.</summary>
        public const float Above = 1.3f;

        /// <summary>Whether text at <paramref name="scale"/> may page: only the largest.</summary>
        public static bool At(float scale) => scale > Above;

        /// <summary>
        /// Deals sections of <paramref name="heights"/>, in order and whole, onto pages of
        /// <paramref name="room"/>, with <paramref name="gap"/> between two on one page: each page
        /// takes the next section while it still fits, half a unit of rounding allowed, and a section
        /// is never split. A section taller than a page has a page of its own, which then needs the
        /// panel's scroll - a screen should not hand one in. Returns the page of each section, from
        /// zero, so the last is the page count less one.
        /// </summary>
        public static int[] Breaks(IList<float> heights, float gap, float room)
        {
            var pages = new int[heights?.Count ?? 0];
            int page = 0;
            float used = 0f;
            for (int i = 0; i < pages.Length; i++)
            {
                float height = Math.Max(0f, heights[i]);
                if (i > 0 && used + gap + height > room + .5f) { page++; used = height; }
                else used += (i > 0 ? gap : 0f) + height;
                pages[i] = page;
            }
            return pages;
        }

        /// <summary>How many pages <see cref="Breaks"/> dealt: one more than the last section's page, and one for none.</summary>
        public static int Count(int[] breaks) => breaks == null || breaks.Length == 0 ? 1 : breaks[breaks.Length - 1] + 1;
    }
}
