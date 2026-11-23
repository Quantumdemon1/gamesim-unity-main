using System.Collections;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The main menu's season card (change 10): above "Continue your season" it says which season
    /// that is, from the save's header; it is not a control, and Continue keeps its caption. The
    /// header read itself, a held save and byte-equality are pinned without a screen
    /// (SaveHeaderTests, MainMenuSeasonCardSaveTests).
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private RectTransform LiveSeasonCard() =>
            Menu().GetComponentsInChildren<RectTransform>(false).LastOrDefault(t => t.name == "Season card");

        [UnityTest]
        public IEnumerator MainMenu_SeasonCardSaysWhichSeasonContinueOpens_AndIsNotAControl()
        {
            new EpisodeSaveStore(director.SavePath).Save(director.Snapshot);
            director.OpenMainMenu();
            yield return null;
            yield return null;

            var state = director.Snapshot;
            int left = state.contestants.Count(actor => actor.status == ContestantStatus.Active);
            string expected = "Week " + state.week + " · " + (left == 1 ? "1 houseguest left" : left + " houseguests left") + " · saved ";
            Assert.That(Menu().SeasonFacts, Does.StartWith(expected), "The card says the saved season's week and who is left.");

            var card = LiveSeasonCard();
            Assert.That(card, Is.Not.Null, "With Continue offered, the card is drawn.");
            var words = card.GetComponentsInChildren<TMPro.TMP_Text>(false).Select(t => t.text).ToList();
            Assert.That(words, Does.Contain(SeasonCardWords.Heading));
            Assert.That(words, Does.Contain(Menu().SeasonFacts));
            Assert.That(card.GetComponentsInChildren<Selectable>(true), Is.Empty, "The card is not a control.");
            Assert.That(card.GetComponentsInChildren<Graphic>(true).Any(g => g.raycastTarget), Is.False, "Nothing on it takes a click.");

            var resume = CastButtons(MainMenu.ContinueCaption);
            Assert.That(resume, Has.Length.EqualTo(1), "Continue keeps its caption and stays the control.");
            var corners = new Vector3[4];
            card.GetWorldCorners(corners);
            float cardBottom = corners[0].y;
            ((RectTransform)resume[0].transform).GetWorldCorners(corners);
            Assert.That(cardBottom, Is.GreaterThanOrEqualTo(corners[1].y), "The card stands above Continue.");

            // Facts that could not be read (a held, missing or unreadable save) leave the heading alone.
            Menu().Show(true, null, () => { }, () => { }, () => { }, () => { });
            yield return null;
            yield return null;
            Assert.That(Menu().SeasonFacts, Is.Null);
            card = LiveSeasonCard();
            Assert.That(card, Is.Not.Null, "Without facts the card is still drawn,");
            Assert.That(card.GetComponentsInChildren<TMPro.TMP_Text>(false).Select(t => t.text).ToList(),
                Is.EqualTo(new[] { SeasonCardWords.Heading }), "with its heading alone.");
            Assert.That(CastButtons(MainMenu.ContinueCaption), Has.Length.EqualTo(1));

            director.CloseMainMenu();
            yield return null;
        }
    }
}
