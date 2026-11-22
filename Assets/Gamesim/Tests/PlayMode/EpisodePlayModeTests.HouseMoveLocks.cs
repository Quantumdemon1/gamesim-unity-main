using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The whole-house moves on a houseguest's screen lock with the true reason (UI-UX-PLAN U4): with the
    /// window's actions spent, rallying the house, airing the laundry and listening in are drawn greyed and
    /// not pressable, each keeping its caption, with "No actions left" where the cost was; the talk tiles stay
    /// pressable; and the airing's "Read · high risk" is red, as on the free-time board. The rule itself
    /// is held against the engine's refusal at every step of a season by WholeHouseMoveLockTests.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator HouseMoveLocks_ASpentWindowGreysTheWholeHouseMovesWithTheReason()
        {
            HoldTheHouseForTheFixture();
            var state = FullHouse(4501, 6);
            state.strategyRulesStartWeek = 1;
            state.haveNotRulesStartWeek = 1;
            EpisodeEngine.EnableWeek(state, state.week);
            EpisodeEngine.EnableStory(state, state.week);
            state.economyRulesVersion = 1;
            state.houseEvents.RemoveAll(item => !item.resolved);
            state.replyCards.Clear();
            while (state.windowActions.Count < Windows.Count) state.windowActions.Add(0);
            int window = EpisodeEngine.Window(state);
            Assert.That(window, Is.Not.EqualTo(Windows.None), "The fixture opens in a window.");
            state.windowActions[window] = EpisodeEngine.SocialActionBudget(state);
            Assert.That(WholeHouseMoves.Lock(state, EpisodeCommandKind.HouseMeeting), Is.EqualTo(HouseMoveLock.NoActionsLeft), "The window is spent.");
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            yield return null;

            yield return OpenFreeTime();
            var who = director.Snapshot.Active.First(c => !c.isPlayer);
            director.OpenHouseguestScreen(who.id);
            yield return null; yield return null;
            yield return WaitOutThePointerHold();
            Canvas.ForceUpdateCanvases();
            Assert.That(director.HouseguestScreenFor, Is.EqualTo(who.id), "Their screen is open.");

            string Foot(Button tile) => tile.GetComponentsInChildren<TMP_Text>(true).Single(text => text.name == "Tile foot").text;
            foreach (var caption in new[] { EpisodeHud.RallyHouseCaption, EpisodeHud.AirLaundryCaption, "Listen in on a conversation" })
            {
                var tile = FindButton(caption);
                Assert.That(tile.transform.IsChildOf(ActiveRect(EpisodeHud.HouseMovesName)), Is.True, caption + " is a whole-house move,");
                Assert.That(tile.IsInteractable(), Is.False, caption + " is locked,");
                Assert.That(tile.name, Is.EqualTo(caption), "keeps its caption,");
                Assert.That(Foot(tile), Is.EqualTo(EpisodeDirector.TileNoActionsLeft), "and says why where its cost was.");
            }
            Assert.That(FindButton(EpisodeDirector.TalkPrivatelyCaption).IsInteractable(), Is.True, "A talk tile never locks here.");

            string risk = EpisodeDirector.AiringRiskLabel(director.Snapshot);
            Assert.That(risk, Is.EqualTo("Read · high risk"), "The airing reads under the economy rules.");
            var word = FindButton(EpisodeHud.AirLaundryCaption).GetComponentsInChildren<TMP_Text>(true).First(text => text.text == risk);
            Assert.That(new[] { word.color.r, word.color.g, word.color.b },
                Is.EqualTo(new[] { UiTheme.Danger.r, UiTheme.Danger.g, UiTheme.Danger.b }).Within(0.01f), "The airing's risk is red, as on the board.");
            director.ClosePanels();
            yield return null;
        }
    }
}
