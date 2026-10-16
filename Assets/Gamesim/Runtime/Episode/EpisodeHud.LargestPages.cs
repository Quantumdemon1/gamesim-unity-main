using System;
using System.Collections.Generic;
using Gamesim.Presentation;
using TMPro;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The largest text's pages: the owner's contract of 2026-10-15 for a screen whose parts cannot
    /// all fit its frame at the largest text size, and the one pager every screen family uses for it.
    ///
    /// <para>At the standard text (1.0) and the larger (1.2) every screen keeps its one-page layout,
    /// byte for byte: nothing here runs there, and a screen asks <see cref="LargestTextPages"/>
    /// before it lays out anything differently. At the largest (1.4) a screen that knows, before it
    /// builds, that its parts cannot fit the height it has lays them on pages instead of squeezing
    /// them or stepping its copy down: <see cref="LargestPages.Breaks"/> deals its whole parts (sections),
    /// in reading order, onto pages of that height less <see cref="LargestPagerHeight"/>; the screen
    /// builds only the shown page's sections; and <see cref="LargestPager"/> draws, at the page's
    /// foot, where the player is - "Page 1 of 2" - with a way back and a way on, each only where
    /// there is somewhere to go. Every page fits without a scroll.</para>
    ///
    /// <para>The pager's controls are ordinary HUD buttons named and captioned "Previous page" and
    /// "Next page": the mouse presses them, the keyboard's ring and the pad's d-pad walk to them and
    /// Enter or A presses them, and a turn keeps the keyboard on the pager - on the same control
    /// when the new page has it, else on the other - so Enter twice goes on and comes back. Which
    /// page is shown is view state kept here, by screen: a rebuild of the open panel keeps it (a
    /// houseguest's body finishing its assembly renders the HUD at any moment), a panel arriving
    /// starts on page one (<see cref="ForgetLargestPages"/> from Begin), and another screen paging
    /// starts on its own first page. Nothing is saved and a turn commits nothing.</para>
    ///
    /// <para>The pager pages a screen's own sections. A row of houseguests that pages within its
    /// section keeps its own pager and its own captions ("Previous houseguests", "Next
    /// houseguests"), as free time's cards and the campaign's tabs always have; on a screen that
    /// pages too, its count says which houseguests it shows (<see cref="LargestRowCount"/>), so
    /// "Page 1 of 2" is said once. Every pager draws the one row, <see cref="PagerRow"/>.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The pager's controls, by the captions a test, a screen reader and the ring find them by.</summary>
        public const string PreviousPageCaption = "Previous page", NextPageCaption = "Next page";

        /// <summary>The pager's row, and its indicator - "Page 1 of 2" - by the names a test finds them by.</summary>
        public const string LargestPagerName = "Page turner", LargestPageCountName = "Page count";

        /// <summary>The pager's row at the resting size: its pills' height, and the gap over it.</summary>
        private const float LargestPagerPill = 26f, LargestPagerGap = 10f;

        /// <summary>Whether this render's text is the largest: the one size at which a screen may page.</summary>
        public bool LargestTextPages => LargestPages.At(FontScale);

        /// <summary>What a page gives up for the pager at its foot: the row and the gap over it, at the text's scale.</summary>
        public float LargestPagerHeight => (LargestPagerPill + LargestPagerGap) * FontScale;

        /// <summary>The screen whose page is kept, the page, and whether the last render's pager was just turned.</summary>
        private string largestScreen;
        private int largestPage;
        private bool largestTurned;

        /// <summary>
        /// The page of <paramref name="screen"/> to show, of <paramref name="pages"/>: the one kept
        /// for it, within the pages it has now, or its first when another screen was paging.
        /// </summary>
        public int LargestPageOf(string screen, int pages)
        {
            if (screen != largestScreen) { largestScreen = screen; largestPage = 0; }
            largestPage = Mathf.Clamp(largestPage, 0, Mathf.Max(0, pages - 1));
            return largestPage;
        }

        /// <summary>Shows <paramref name="page"/> of <paramref name="screen"/> from its next render: a screen whose own focus is on another page turns to it.</summary>
        public void ShowLargestPage(string screen, int page)
        {
            largestScreen = screen;
            largestPage = Mathf.Max(0, page);
        }

        /// <summary>Every screen back to its first page: a panel arriving, or the screen's own view thrown away.</summary>
        public void ForgetLargestPages()
        {
            largestScreen = null;
            largestPage = 0;
            largestTurned = false;
        }

        /// <summary>
        /// The pager at a page's foot, across <paramref name="width"/> from (<paramref name="x"/>,
        /// <paramref name="y"/>) of <paramref name="parent"/>: the way back and the way on, each only
        /// where there is somewhere to go, laid from the right in reading order, and where the page is
        /// beside them. Pressing one shows that page and asks <paramref name="redraw"/> to render the
        /// screen again; nothing is committed. Null when there is one page. Its height is the pill's,
        /// <see cref="LargestPagerHeight"/> less the gap over it.
        /// </summary>
        public RectTransform LargestPager(RectTransform parent, float x, float y, float width, string screen, int page, int pages, Action redraw)
        {
            if (parent == null || pages <= 1) return null;
            float s = FontScale, height = LargestPagerPill * s;
            var row = EndScreenKit.Box(LargestPagerName, parent, x, y, width, height);
            void Turn(int to)
            {
                ShowLargestPage(screen, to);
                largestTurned = true;
                redraw?.Invoke();
            }
            // The one pager row (PagerRow), with where the page is in a box 1.3 times its size or more.
            PagerRow(row, width, height, 110f * s, PreviousPageCaption, NextPageCaption, LargestPageCountName,
                "Page " + (page + 1) + " of " + pages, page, pages, Turn);
            // A turn keeps the keyboard on the pager: the control pressed when this page has it, else the other.
            if (largestTurned)
            {
                largestTurned = false;
                string stay = preferredSelection == NextPageCaption && page < pages - 1 ? NextPageCaption
                    : preferredSelection == PreviousPageCaption && page > 0 ? PreviousPageCaption
                    : page < pages - 1 ? NextPageCaption : PreviousPageCaption;
                preferredSelection = stay;
                restoreSelection = true;
            }
            return row;
        }

        /// <summary>
        /// The one pager row the HUD draws, for every pager it has: the largest text's pages
        /// (<see cref="LargestPager"/>), free time's cards and the campaign's tabs. On
        /// <paramref name="parent"/>, at its top, across <paramref name="width"/>: pills
        /// <paramref name="height"/> high - the way back before the way on, each only where there is
        /// somewhere to go - laid from the right with a gap between, and left of them, right-aligned in
        /// a box <paramref name="where"/> wide and 17 high at the text's scale (1.3 times its 12 or
        /// more), where the player is: <paramref name="count"/>, named <paramref name="countName"/>.
        /// A press asks <paramref name="choose"/> for that page and commits nothing of its own. Each
        /// pager keeps its own captions, count name and box width, so sharing the row changes none of
        /// them. Returns the width it took, the gap left of the count included.
        /// </summary>
        private float PagerRow(RectTransform parent, float width, float height, float where, string previousCaption, string nextCaption,
            string countName, string count, int page, int pages, Action<int> choose)
        {
            float s = FontScale, gap = 8f * s, x = width;
            // Made in reading order, the way back before the way on, and laid from the right.
            var pills = new List<RectTransform>();
            if (page > 0)
            {
                int previous = page - 1;
                pills.Add((RectTransform)FilterPill(parent, previousCaption, false, () => choose?.Invoke(previous), height).transform);
            }
            if (page < pages - 1)
            {
                int next = page + 1;
                pills.Add((RectTransform)FilterPill(parent, nextCaption, false, () => choose?.Invoke(next), height).transform);
            }
            for (int i = pills.Count - 1; i >= 0; i--)
            {
                x -= pills[i].sizeDelta.x;
                Anchor(pills[i], new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, 0f), pills[i].sizeDelta);
                x -= gap;
            }
            x -= where;
            var at = FixedText(parent, count, 12, UiTheme.Muted, new Vector2(x, -(height - 17f * s) * .5f), new Vector2(where, 17f * s));
            at.name = countName;
            at.alignment = TextAlignmentOptions.Right;
            AutoSize(at, 9);
            return width - x + gap;
        }

        /// <summary>
        /// Where a row of houseguests is when it pages inside a screen that pages too: which of them
        /// it shows - "Houseguests 1–6 of 7", "Houseguest 7 of 7" - rather than a second "Page 1 of
        /// 2", so the screen's own pager is the one page count on it, for the eye and for a screen
        /// reader alike. Only the largest text pages a screen, so no other size ever shows it.
        /// </summary>
        private static string LargestRowCount(int first, int last, int total) =>
            last > first ? "Houseguests " + first + "–" + last + " of " + total : "Houseguest " + first + " of " + total;
    }
}
