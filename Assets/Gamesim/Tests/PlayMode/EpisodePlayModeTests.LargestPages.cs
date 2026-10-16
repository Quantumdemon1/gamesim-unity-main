using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The largest text's pages as every screen's suite asserts them (the owner's contract of
    /// 2026-10-15; EpisodeHud.LargestPages.cs): one page, laid as it always was, at the standard and
    /// the larger text; at the largest, exactly the pages the suite names for its frame - a literal,
    /// never the renderer's own arithmetic - each of which fits without a scroll, with the pager at
    /// its foot saying where the player is, a way back on every page but the first and a way on on
    /// every page but the last, and turning commits nothing.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The largest text's pager row now, or null when the screen is one page.</summary>
        private RectTransform LargestPagerNow() => ActiveRect(EpisodeHud.LargestPagerName);

        /// <summary>"Page i of N" read off the pager's indicator; (1, 1) when there is no pager.</summary>
        private (int Page, int Pages) LargestPageIndicator()
        {
            var pager = LargestPagerNow();
            if (pager == null) return (1, 1);
            var count = pager.GetComponentsInChildren<TMP_Text>().SingleOrDefault(text => text.name == EpisodeHud.LargestPageCountName);
            Assert.That(count, Is.Not.Null, "The pager says where the player is.");
            var words = count.text.Split(' ');
            Assert.That(words.Length == 4 && words[0] == "Page" && words[2] == "of", Is.True, "The pager's indicator reads 'Page i of N': '" + count.text + "'.");
            return (int.Parse(words[1]), int.Parse(words[3]));
        }

        /// <summary>How many pages the screen has now: N from its indicator, one when it has no pager.</summary>
        private int LargestPagesShown() => LargestPageIndicator().Pages;

        /// <summary>The page shown now, from one.</summary>
        private int LargestPageNow() => LargestPageIndicator().Page;

        /// <summary>A live control with this caption, or null.</summary>
        private Button LivePageControl(string caption) => director.GetComponentsInChildren<Button>()
            .Where(button => button.IsActive() && button.IsInteractable() && button.name == caption).LastOrDefault();

        /// <summary>
        /// The owner's contract, literally. At the standard and the larger text: one page - no pager,
        /// no way to turn one - and no scroll. At the largest: exactly <paramref name="expectedAtLargest"/>
        /// pages, walked from the first forward by "Next page" and back by "Previous page": on each, the indicator
        /// says which, the way back is there on every page but the first and the way on on every page
        /// but the last, the column holds without a scroll with the pager inside the stage's view,
        /// <paramref name="eachPage"/> runs, and the turn committed nothing. Ends on the first page.
        /// Synchronous, so a suite's AtEveryTextSize check can call it.
        /// </summary>
        private void AssertLargestPages(string where, int expectedAtLargest, System.Action<int> eachPage = null)
        {
            Canvas.ForceUpdateCanvases();
            bool largest = director.TextSize == TextSize.Largest;
            where += " " + TextScales.At(director.TextSize);
            void Fits(string what)
            {
                var content = ActiveRect("Episode content");
                var viewport = (RectTransform)content.parent;
                Assert.That(content.rect.height, Is.LessThanOrEqualTo(viewport.rect.height + .5f),
                    what + " holds without a scroll: " + content.rect.height.ToString("0") + " in " + viewport.rect.height.ToString("0") + ".");
            }
            if (!largest || expectedAtLargest <= 1)
            {
                Assert.That(LargestPagerNow(), Is.Null, where + " is one page: it has no pager.");
                Assert.That(LivePageControl(EpisodeHud.NextPageCaption), Is.Null, where + " has no '" + EpisodeHud.NextPageCaption + "'.");
                Assert.That(LivePageControl(EpisodeHud.PreviousPageCaption), Is.Null, where + " has no '" + EpisodeHud.PreviousPageCaption + "'.");
                Fits(where);
                eachPage?.Invoke(1);
                return;
            }
            ShowFirstPage();
            Assert.That(LargestPageIndicator(), Is.EqualTo((1, expectedAtLargest)),
                where + " has exactly " + expectedAtLargest + " pages.");
            for (int page = 1; ; page++)
            {
                string here = where + ", page " + page + " of " + expectedAtLargest;
                Assert.That(LargestPageIndicator(), Is.EqualTo((page, expectedAtLargest)), here + ": the pager says so.");
                Assert.That(LivePageControl(EpisodeHud.PreviousPageCaption) != null, Is.EqualTo(page > 1), here + ": a way back exactly when there is a page before.");
                Assert.That(LivePageControl(EpisodeHud.NextPageCaption) != null, Is.EqualTo(page < expectedAtLargest), here + ": a way on exactly when there is a page after.");
                Fits(here);
                var viewport = (RectTransform)ActiveRect("Episode content").parent;
                AssertInside(ScreenRect(viewport), LargestPagerNow(), here + "'s pager");
                eachPage?.Invoke(page);
                if (page == expectedAtLargest) break;
                int revision = director.Snapshot.revision;
                LivePageControl(EpisodeHud.NextPageCaption).onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                Assert.That(director.Snapshot.revision, Is.EqualTo(revision), here + ": turning the page commits nothing.");
            }
            for (int page = expectedAtLargest; page > 1; page--)
            {
                int revision = director.Snapshot.revision;
                LivePageControl(EpisodeHud.PreviousPageCaption).onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                Assert.That(director.Snapshot.revision, Is.EqualTo(revision), where + ": turning back commits nothing.");
                Assert.That(LargestPageIndicator(), Is.EqualTo((page - 1, expectedAtLargest)), where + ": back a page is page " + (page - 1) + ".");
            }
        }

        /// <summary>Turns the largest text's pages from the first until <paramref name="partName"/> is on screen.</summary>
        private void ShowPageWith(string partName)
        {
            Canvas.ForceUpdateCanvases();
            for (int turns = 0; ActiveRect(partName) == null; turns++)
            {
                var next = LivePageControl(EpisodeHud.NextPageCaption);
                Assert.That(next != null && turns < 8, Is.True, "No page shows '" + partName + "'.");
                next.onClick.Invoke();
                Canvas.ForceUpdateCanvases();
            }
        }

        /// <summary>Back to the largest text's first page, if the screen pages.</summary>
        private void ShowFirstPage()
        {
            Canvas.ForceUpdateCanvases();
            for (int turns = 0; turns < 8; turns++)
            {
                var back = LivePageControl(EpisodeHud.PreviousPageCaption);
                if (back == null) return;
                back.onClick.Invoke();
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
