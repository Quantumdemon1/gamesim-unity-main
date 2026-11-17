using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The menu review's quick wins on the in-season boards (part c), on the built board: the budget
    /// card's count labelled "Actions left" with the ways to buy an action behind one "Buy an action"
    /// (quick win 19), and the "Last move" line that keeps the player's last move and how it went
    /// (quick win 18).
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// On the free-time board, presses "Buy an action" where it stands, so the ways to buy are out,
        /// and waits out the pointer hold its press begins. Anywhere else - the old column's tiles show
        /// the ways themselves - it does nothing.
        /// </summary>
        private IEnumerator RevealBuyOptions()
        {
            if (LiveButtonsCarrying(EpisodeDirector.BuyAnActionCaption) == 0) yield break;
            ButtonWithCaption(EpisodeDirector.BuyAnActionCaption).onClick.Invoke();
            yield return null; yield return null;
            if (director.GetComponentInChildren<EpisodeHud>().PointerHeld) yield return WaitOutThePointerHold();
        }

        /// <summary>The buttons on the budget card's foot, by name, from the live copy of the strip.</summary>
        private string[] BuyStripButtons()
        {
            var strip = director.GetComponentsInChildren<RectTransform>(true)
                .LastOrDefault(rect => rect.name == EpisodeHud.BuyActionsName && rect.gameObject.activeInHierarchy);
            Assert.That(strip, Is.Not.Null, "The budget card has its row of ways to buy.");
            return strip.GetComponentsInChildren<Button>().Select(button => button.name).ToArray();
        }

        [UnityTest]
        public IEnumerator SeasonBoards_TheBudgetCardSaysActionsLeftLargeAndReadable()
        {
            yield return InstallFreeTime(8, FreeTimeWaiting.Nothing);
            yield return OpenStation();
            yield return null;
            var state = director.Snapshot;
            int left = EpisodeEngine.SocialActionBudget(state) - EpisodeEngine.SocialActionsSpent(state);
            var count = BudgetLabel(EpisodeHud.ActionsLeftCountName);
            var word = BudgetLabel(EpisodeHud.ActionsLeftWordName);
            Assert.That(count.text, Is.EqualTo(left.ToString()), "The count is the actions left,");
            Assert.That(word.text, Is.EqualTo("Actions left"), "under the budget's one name, whatever the count,");
            Assert.That(count.fontSize, Is.GreaterThan(word.fontSize), "and the count is the large one.");
            Canvas.ForceUpdateCanvases();
            Assert.That(word.fontSize, Is.GreaterThanOrEqualTo(14f), "The label is never under 14 px,");
            Assert.That(word.rectTransform.rect.height, Is.GreaterThanOrEqualTo(word.fontSize * 1.3f - .5f), "in a box 1.3 times as tall.");
        }

        [UnityTest]
        public IEnumerator SeasonBoards_BuyAnActionShowsTheTwoWaysAndEachBuysAsBefore()
        {
            yield return InstallFreeTime(8, FreeTimeWaiting.Nothing);
            yield return OpenStation();
            yield return null;
            Assert.That(BuyStripButtons(), Is.EqualTo(new[] { EpisodeDirector.BuyAnActionCaption }),
                "One button stands for the ways to buy an action.");
            Assert.That(LiveButtonsCarrying(EpisodeHud.BuyBurnOneCaption) + LiveButtonsCarrying(EpisodeHud.BuySpreadCaption), Is.EqualTo(0),
                "The two ways are behind it.");
            var before = director.Snapshot;

            // The keyboard reaches it, and Enter on it shows the ways and buys nothing.
            yield return KeyboardSubmit(EpisodeDirector.BuyAnActionCaption);
            yield return null; yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision), "Showing the ways commits nothing.");
            Assert.That(director.FreeTimeBuyOptionsShown, Is.True);
            yield return WaitOutThePointerHold();
            Assert.That(BuyStripButtons(), Is.EqualTo(new[] { EpisodeHud.BuyBurnOneCaption, EpisodeHud.BuySpreadCaption }),
                "The two ways stand where the button stood, in their old order.");
            Assert.That(EventSystem.current.currentSelectedGameObject?.name, Is.EqualTo(EpisodeHud.BuyBurnOneCaption),
                "The keyboard is on the first way, which only opens its picker.");

            // Each buys exactly as before: one commit, one more action, nothing spent.
            ButtonWithCaption(EpisodeHud.BuySpreadCaption).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1), "The whole house's way commits once,");
            Assert.That(after.boughtActionPoints, Is.EqualTo(before.boughtActionPoints + 1), "buys one action,");
            Assert.That(EpisodeEngine.SocialActionBudget(after), Is.EqualTo(EpisodeEngine.SocialActionBudget(before) + 1), "which the budget counts,");
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before)), "and spends none.");

            // Closing the panel puts them away again.
            director.ClosePanels();
            yield return null;
            yield return OpenStation();
            yield return null; yield return null;
            Assert.That(director.FreeTimeBuyOptionsShown, Is.False);
            Assert.That(BuyStripButtons(), Is.EqualTo(new[] { EpisodeDirector.BuyAnActionCaption }), "The board opens again with the one button.");
        }

        [UnityTest]
        public IEnumerator SeasonBoards_TheLastMoveStaysOnTheBoardAndNeverOutlivesALoad()
        {
            yield return InstallFreeTime(8, FreeTimeWaiting.Nothing);
            yield return OpenStation();
            yield return null;
            Assert.That(ActiveRect(EpisodeHud.LastMoveName), Is.Null, "Before the week's first move there is no Last move line.");

            var before = director.Snapshot;
            ButtonWithCaption(EpisodeDirector.OverviewListenCaption).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1), "Listening in commits once.");
            if (!director.IsPhasePanelOpen) yield return OpenStation();
            yield return null; yield return null;
            var line = director.GetComponentsInChildren<TMP_Text>()
                .LastOrDefault(text => text.name == EpisodeHud.LastMoveName && text.gameObject.activeInHierarchy);
            Assert.That(line, Is.Not.Null, "After the move the board has its Last move line,");
            Assert.That(line.text, Does.StartWith(EpisodeDirector.LastMovePrefix), "headed Last move,");
            var said = after.events.Where(e => e.sequence >= before.nextSequence).Select(e => EpisodeDirector.StatusLine(after, e)).ToList();
            Assert.That(said, Does.Contain(line.text.Substring(EpisodeDirector.LastMovePrefix.Length)),
                "in the words the move's own logged line says what happened and how it went.");
            Assert.That(line.fontSize, Is.GreaterThanOrEqualTo(14f), "At 14 px or more.");
            string kept = line.text;

            // It stays readable: closing the panel and opening it again does not lose it.
            director.ClosePanels();
            yield return null;
            yield return OpenStation();
            yield return null; yield return null;
            var again = director.GetComponentsInChildren<TMP_Text>()
                .LastOrDefault(text => text.name == EpisodeHud.LastMoveName && text.gameObject.activeInHierarchy);
            Assert.That(again != null ? again.text : null, Is.EqualTo(kept), "The line outlasts the panel.");

            // A load never brings back a line it cannot vouch for.
            new EpisodeSaveStore(director.SavePath).Save(director.Snapshot);
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            yield return null;
            yield return OpenStation();
            yield return null; yield return null;
            Assert.That(ActiveRect(EpisodeHud.LastMoveName), Is.Null, "After a load there is no stale Last move line.");
        }
    }
}
