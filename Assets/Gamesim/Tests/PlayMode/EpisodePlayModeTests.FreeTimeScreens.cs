using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
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
    /// Free time as three screens (the owner's review of the mockups, 2026-09-28): the Free Time
    /// root leads with the actions left and keeps its context beside the decision; a houseguest's
    /// screen opens from their card with the three ways to spend an action on them and returns to
    /// the root; asking sends the conversation what the player came for; and the overview is the
    /// strategic dashboard over the labelled house. Every caption a control was found by before is
    /// still the caption it is found by.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private IEnumerator OpenFreeTime()
        {
            yield return SettleCast();
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
        }

        private static string Words(RectTransform root) =>
            string.Join("\n", root.GetComponentsInChildren<TMP_Text>().Where(t => t.gameObject.activeInHierarchy).Select(t => t.text));

        /// <summary>
        /// The board's root (ACTIONS-DEALS-ALLIANCES-PLAN F1, mockup 87): the budget card under the
        /// head's old name leads with FREE TIME and the actions left, with what moving on loses in it
        /// rather than under the way on; the play, the threads and where the player is stay in view
        /// beside the decision - the play in the hero, the threads on the story strip, the room in the
        /// footer - and the house's cards and the moves are rows of the board.
        /// </summary>
        [UnityTest]
        public IEnumerator FreeTime_LeadsWithTheActionsLeftAndKeepsItsContextBesideTheDecision()
        {
            yield return OpenFreeTime();
            var state = director.Snapshot;
            Assume.That(state.phase, Is.EqualTo(EpisodePhase.Social), "The fixture opens in free time.");
            Assume.That(state.houseEvents.Any(item => !item.resolved && !item.IsStory), Is.False,
                "The fixture's free time has no legacy house event, which keeps its band.");
            Assert.That(director.IsFreeTimeBoard, Is.True, "Free time is the board.");
            var panel = ActiveRect("Episode panel");
            var head = ActiveRect(EpisodeHud.ScreenHeadName);
            Assert.That(head, Is.Not.Null, "The screen has its budget card, under the head's name.");
            int left = Mathf.Max(0, EpisodeEngine.SocialActionBudget(state) - EpisodeEngine.SocialActionsSpent(state));
            var labels = head.GetComponentsInChildren<TMP_Text>().ToList();
            Assert.That(Words(head), Does.Contain("FREE TIME"), "The card says the screen's name,");
            Assert.That(labels.Single(text => text.name == EpisodeHud.ActionsLeftCountName).text, Is.EqualTo(left.ToString()), "how many actions are left, large,");
            Assert.That(labels.Single(text => text.name == EpisodeHud.ActionsLeftWordName).text, Is.EqualTo(left == 1 ? "ACTION LEFT" : "ACTIONS LEFT"));
            Assert.That(labels.Single(text => text.name == EpisodeHud.BudgetRuleName).text, Is.EqualTo(EpisodeDirector.BudgetRule(state)), "the week's rule,");
            Assert.That(labels.Single(text => text.name == EpisodeHud.BudgetCopyName).text, Is.EqualTo(EpisodeDirector.FreeTimeCostLine(state)),
                "and what costs an action, and what moving on loses.");
            // The board's rows: the hero, the story strip, the cards and the moves.
            var board = ActiveRect(EpisodeHud.FreeTimeBoardName);
            Assert.That(board, Is.Not.Null, "Free time is one board.");
            Assert.That(ActiveRect(EpisodeHud.HouseCardsName).IsChildOf(board), Is.True, "The house's cards are the decision, on the board.");
            Assert.That(ActiveRect(EpisodeHud.HouseMovesName).IsChildOf(board), Is.True, "The moves that name nobody are under them.");
            var hero = ActiveRect(EpisodeHud.FreeTimeHeroName);
            Assert.That(head.IsChildOf(board), Is.True, "The budget card is on the board,");
            Assert.That(Mathf.Abs(ScreenRect(head).yMax - ScreenRect(hero).yMax), Is.LessThan(1f), "beside the hero,");
            Assert.That(ScreenRect(head).xMin, Is.GreaterThanOrEqualTo(ScreenRect(hero).xMax - .5f), "on its right.");
            // The story session's ask: the play and the threads stay in view while actions are spent.
            foreach (var name in new[] { EpisodeHud.CurrentPlayCardName, EpisodeHud.ThreadsCardName })
            {
                var card = ActiveRect(name);
                Assert.That(card, Is.Not.Null, name + " is on the screen,");
                Assert.That(card.IsChildOf(board), Is.True, name + " on the board.");
            }
            Assert.That(ActiveRect(EpisodeHud.ThreadsCardName).IsChildOf(ActiveRect(EpisodeHud.StoryStripName)), Is.True, "The threads are on the story strip.");
            // Each card's name is its own control: it opens their screen. "Talk to X" stays the shortcut.
            var cards = ActiveRect(EpisodeHud.HouseCardsName);
            foreach (var actor in state.Active.Where(c => !c.isPlayer))
            {
                Assert.That(cards.GetComponentsInChildren<Button>(true).Any(b => b.name == actor.name), Is.True, actor.name + "'s name opens their screen.");
                Assert.That(ButtonWithCaption(EpisodeHud.CastTalkCaption(actor.name.Split(' ')[0])).transform.IsChildOf(cards), Is.True);
            }
            // The way on is pinned; what moving on costs is said in the budget card, not under it.
            var begin = ButtonWithCaption("Begin the next competition");
            Assert.That(begin.transform.parent, Is.SameAs(panel), "The way on is pinned.");
            Assert.That(panel.Find(EpisodeHud.PinnedNoteName), Is.Null, "Nothing is wedged under the way on.");
            var note = labels.SingleOrDefault(text => text.name == EpisodeHud.UnusedActionsNoteName);
            string lost = EpisodeDirector.UnusedActionsNote(state);
            if (lost != null)
            {
                Assert.That(left, Is.GreaterThan(0), "Only actions left can be lost.");
                Assert.That(note, Is.Not.Null, "The unused actions are said in the budget card.");
                Assert.That(note.text, Is.EqualTo(lost));
            }
            else Assert.That(note, Is.Null, "Nothing is said lost when moving on loses nothing.");
            // Where the player is, in the footer's strip, unless a storyline moving on lets pass outranks it.
            var strip = ActiveRect(EpisodeHud.StrategyStripName);
            Assert.That(strip, Is.Not.Null, "The footer has its strip.");
            var where = strip.GetComponentsInChildren<TMP_Text>().Single();
            Assert.That(new[] { EpisodeHud.LocationCardName, EpisodeHud.FreeTimeTipName, EpisodeDirector.AdvanceWarningName }, Does.Contain(where.name),
                "The strip says where the player is, a tip, or what moving on lets pass: '" + where.text + "'.");
            AssertClearOfTheChrome(panel);
            AssertEveryLabelDraws(board, "Free time's board");
            if (Application.isBatchMode) yield return CaptureFraming("free-time-screen");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FreeTime_AHouseguestsScreenOpensFromTheirCardWithThreeWaysToSpendTheActionAndReturns()
        {
            yield return OpenFreeTime();
            var state = director.Snapshot;
            Assume.That(state.phase, Is.EqualTo(EpisodePhase.Social));
            var who = state.Active.First(c => !c.isPlayer);
            var cards = ActiveRect(EpisodeHud.HouseCardsName);
            var open = cards.GetComponentsInChildren<Button>(true).First(b => b.name == who.name);
            open.onClick.Invoke();
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(director.HouseguestScreenFor, Is.EqualTo(who.id), "Their screen is open over free time.");
            var panel = ActiveRect("Episode panel");
            Assert.That(ActiveRect(EpisodeHud.HouseCardsName), Is.Null, "The root's cards stand down;");
            var strip = ActiveRect(EpisodeHud.HouseguestStripName);
            Assert.That(strip, Is.Not.Null, "the house is a strip instead,");
            Assert.That(strip.GetComponentsInChildren<Button>(true).Select(b => b.name),
                Is.EquivalentTo(state.Active.Where(c => !c.isPlayer).Select(c => c.name)), "everybody on it.");
            Assert.That(Words(ActiveRect(EpisodeHud.ScreenHeadName)), Does.Contain("SPEND"), "The head says the action is being spent.");
            // The three ways, as tiles, each with its cost at its foot.
            var tiles = ActiveRect("Interaction tiles");
            Assert.That(tiles, Is.Not.Null);
            foreach (var caption in new[] { EpisodeDirector.TalkPrivatelyCaption, EpisodeDirector.AskForInformationCaption, EpisodeDirector.PitchADealCaption })
            {
                var tile = ButtonWithCaption(caption);
                Assert.That(tile.transform.IsChildOf(tiles), Is.True, caption + " is one of the three.");
                Assert.That(tile.GetComponentsInChildren<TMP_Text>(true).Any(t => t.name == "Tile foot" && t.text.Contains("Cost")), Is.True, caption + " says its cost.");
            }
            Assert.That(ActiveRect(EpisodeHud.HouseMovesName), Is.Not.Null, "The whole house's moves are under them,");
            Assert.That(ButtonWithCaption(EpisodeHud.RallyHouseCaption).transform.IsChildOf(ActiveRect(EpisodeHud.HouseMovesName)), Is.True);
            var about = ActiveRect(EpisodeHud.AboutCardName);
            Assert.That(about, Is.Not.Null, "and what you have on them beside.");
            Assert.That(Words(about), Does.Contain(who.name.ToUpperInvariant()).And.Contain(RelationshipWeb.StandingWord(RelationshipWeb.KindOf(state, who.id))));
            Assert.That(ActiveRect(EpisodeHud.YourContextCardName), Is.Not.Null, "Your own context is beside too.");
            Assert.That(ButtonWithCaption("Begin the next competition").transform.parent, Is.SameAs(panel), "The way on stays pinned.");
            // Another face on the strip is another screen.
            var other = state.Active.First(c => !c.isPlayer && c.id != who.id);
            strip.GetComponentsInChildren<Button>(true).First(b => b.name == other.name).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.HouseguestScreenFor, Is.EqualTo(other.id), "Pressing another face makes the screen theirs.");
            if (Application.isBatchMode) yield return CaptureFraming("houseguest-screen");
            // And back.
            ButtonWithCaption(EpisodeDirector.BackToFreeTimeCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.HouseguestScreenFor, Is.Null, "Back to free time.");
            Assert.That(ActiveRect(EpisodeHud.HouseCardsName), Is.Not.Null, "The cards are back.");
            director.ClosePanels();
            yield return null;
            Assert.That(director.HouseguestScreenFor, Is.Null, "Closing the panel forgets the screen.");
        }

        /// <summary>
        /// Before anybody is Head of Household the context card says nothing is decided
        /// (UI-UX-PASS-PLAN D0, the play sweep's row 20): in free time - every week's, since the
        /// rollover clears the Head of Household - it read "You are safe this week" under a HUD
        /// saying "Awaiting HoH", because the safe line was everything that was not a role. The
        /// words are the plan's, the same ones U0's ContextRole says.
        /// </summary>
        [UnityTest]
        public IEnumerator FreeTime_YourContextSaysNothingIsDecidedBeforeAnyHeadOfHousehold()
        {
            yield return OpenFreeTime();
            var state = director.Snapshot;
            Assert.That(state.phase, Is.EqualTo(EpisodePhase.Social), "The fixture opens in free time,");
            Assert.That(string.IsNullOrEmpty(state.hohId), Is.True, "before the week's first Head of Household.");
            director.OpenHouseguestScreen(state.Active.First(c => !c.isPlayer).id);
            yield return null; yield return null;
            var context = LastActive(EpisodeHud.YourContextCardName);
            Assert.That(context, Is.Not.Null, "The houseguest screen has its context card.");
            Assert.That(Words(context), Does.Contain("Nothing decided yet").And.Contain("The week's roles come with the first competition.")
                .And.Not.Contain("You are safe this week").And.Not.Contain("You are HOH").And.Not.Contain("You are on the block"));
            director.CloseHouseguestScreen();
            yield return null;
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FreeTime_AskingForInformationOpensTheConversationOnWhatYouCameToAsk()
        {
            yield return SettleCast();
            var state = director.Snapshot;
            Assume.That(state.phase, Is.EqualTo(EpisodePhase.Social));
            var who = state.Active.First(c => !c.isPlayer);
            var npc = SceneComponents<HouseNpc>().First(n => n.Id == who.id && n.gameObject.activeInHierarchy);
            // Beside them, so the walk is no walk and the conversation opens at once.
            WarpPlayer(npc.transform.position + Vector3.right * 1.2f);
            director.TalkWithIntent(who.id, EpisodeDirector.IntentAsk);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(director.IsConversationOpen, Is.True, "The conversation is open.");
            Assert.That(director.ConversationIntent, Is.EqualTo(EpisodeDirector.IntentAsk), "and knows what you came for.");
            var content = ActiveRect("Episode content");
            var labels = content.GetComponentsInChildren<TMP_Text>().Where(t => t.gameObject.activeInHierarchy).Select(t => t.text).ToList();
            int heading = labels.IndexOf("WHAT YOU CAME TO ASK");
            Assert.That(heading, Is.GreaterThanOrEqualTo(0), "What you came for is named first.");
            int ask = labels.IndexOf("Ask what they have heard");
            Assert.That(ask, Is.GreaterThan(heading), "The asking rows follow it,");
            Assert.That(labels.Count(l => l == "Ask what they have heard"), Is.EqualTo(1), "and are not drawn twice.");
            // The dial's petals hang on their own hub, not in the scroll column; the first row the
            // dial leads to is the one to measure against.
            Assert.That(labels.IndexOf(EpisodeHud.DiscussGameCaption), Is.GreaterThan(ask), "before the rows the dial leads to.");
            director.ClosePanels();
            yield return null;
            Assert.That(director.ConversationIntent, Is.Null, "Closing the conversation forgets the intent.");
        }

        [UnityTest]
        public IEnumerator Overview_IsTheStrategicDashboardOverTheLabelledHouse()
        {
            yield return SettleCast();
            var state = director.Snapshot;
            // The rail's Overview row opens the overview with its briefing; the map alone is ShowOverview.
            Assert.That(director.ToggleOverview(), Is.True);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(director.IsOverview && director.IsBriefing, Is.True, "The rail's row is the briefing over the map.");
            var dashboard = ActiveRect(EpisodeHud.DashboardName);
            Assert.That(dashboard, Is.Not.Null, "The overview carries the dashboard.");
            Assert.That(ActiveRect(EpisodeHud.OverviewColumnName), Is.Not.Null, "and keeps its room list beside it.");
            Assert.That(ScreenRect(dashboard).xMax, Is.LessThanOrEqualTo(ScreenRect(ActiveRect(EpisodeHud.OverviewColumnName)).xMin + .5f),
                "The dashboard stands clear of the room list.");
            var banner = ActiveRect(EpisodeHud.RolesBannerName);
            Assert.That(banner, Is.Not.Null, "The week's roles are on a band.");
            if (state.Find(state.hohId) != null) Assert.That(Words(banner), Does.Contain(state.Find(state.hohId).name), "naming the Head of Household.");
            var glance = ActiveRect(EpisodeHud.GlanceStripName);
            Assert.That(glance, Is.Not.Null, "The house is there at a glance,");
            Assert.That(glance.GetComponentsInChildren<Button>(true).Select(b => b.name), Is.EquivalentTo(state.Active.Select(c => c.name)), "everyone still in it.");
            var context = ActiveRect(EpisodeHud.StrategicContextName);
            Assert.That(context, Is.Not.Null, "The strategic context is beside them,");
            Assert.That(Words(context), Does.Contain("PLAYS").And.Contain("THREADS").And.Contain("PHASE RULES").And.Contain(EpisodeDirector.PhaseRule(state)));
            var moves = ActiveRect(EpisodeHud.RecommendedTilesName);
            Assert.That(moves, Is.Not.Null, "and the smart moves under them.");
            Assert.That(ButtonWithCaption(EpisodeDirector.OverviewStationCaption).transform.IsChildOf(moves), Is.True, "The way on is always one of them.");
            if (state.phase == EpisodePhase.Social)
                Assert.That(ButtonWithCaption(EpisodeDirector.OverviewListenCaption).transform.IsChildOf(moves), Is.True, "Listening in is offered in free time.");
            if (Application.isBatchMode) yield return CaptureFraming("overview-dashboard");
            // The map is under the briefing and is a way there: putting the briefing away leaves the
            // overview, its room list and its chips, with nothing over the floor.
            ButtonWithCaption(EpisodeDirector.ShowMapCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.IsOverview, Is.True, "Showing the map keeps the overview.");
            Assert.That(director.IsBriefing, Is.False);
            Assert.That(ActiveRect(EpisodeHud.DashboardName), Is.Null, "The briefing is put away,");
            Assert.That(ActiveRect(EpisodeHud.OverviewColumnName), Is.Not.Null, "the room list stays,");
            Assert.That(ButtonWithCaption(EpisodeDirector.ShowBriefingCaption), Is.Not.Null, "and the briefing is a row away.");
            ButtonWithCaption(EpisodeDirector.ShowBriefingCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.IsBriefing, Is.True);
            Assert.That(ActiveRect(EpisodeHud.DashboardName), Is.Not.Null, "The briefing comes back.");
            director.EndOverview();
            yield return null;
            Assert.That(ActiveRect(EpisodeHud.DashboardName), Is.Null, "The dashboard goes with the overview.");
            Assert.That(director.IsBriefing, Is.False);
        }
    }
}
