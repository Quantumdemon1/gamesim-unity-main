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

        /// <summary>
        /// A houseguest's screen opens from their card with the three ways to spend the action on
        /// them, the whole house's moves, your context and what you have on them, and returns. It is
        /// laid for the strategy stage as the board is (UI-UX-PASS-PLAN U0): the way on pinned with
        /// "Back to free time" in the footer's secondary slot, the room and what moving on loses in
        /// the footer's strip, and no row of the screen under the pinned bar - on the 16:9 frame at
        /// the resting text size the whole screen holds in the stage's height; at the larger text
        /// its scroll stands clear of the footer, and the log says how far the column runs. Before
        /// anybody is Head of Household the context says nothing is decided yet, never "safe".
        /// </summary>
        [UnityTest]
        public IEnumerator FreeTime_AHouseguestsScreenOpensFromTheirCardWithThreeWaysToSpendTheActionAndReturns()
        {
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                yield return OpenFreeTime();
                string where = "A houseguest's screen" + (larger ? " at the larger text" : "");
                var state = director.Snapshot;
                Assert.That(state.phase, Is.EqualTo(EpisodePhase.Social), "The fixture opens in free time.");
                var who = state.Active.First(c => !c.isPlayer);
                var cards = ActiveRect(EpisodeHud.HouseCardsName);
                var open = cards.GetComponentsInChildren<Button>(true).First(b => b.name == who.name);
                open.onClick.Invoke();
                Assert.That(Hud.PointerHeld, Is.True, "The screen replaces the board under the pointer, which is held off a moment.");
                yield return null; yield return null;
                // The next deliberate press waits that out, as a player's does - and as ButtonWithCaption's raycast needs.
                yield return WaitOutThePointerHold();
                Canvas.ForceUpdateCanvases();
                Assert.That(director.HouseguestScreenFor, Is.EqualTo(who.id), "Their screen is open over free time.");
                var panel = ActiveRect("Episode panel");
                Assert.That(ActiveRect(EpisodeHud.HouseCardsName), Is.Null, "The root's cards stand down;");
                var strip = ActiveRect(EpisodeHud.HouseguestStripName);
                Assert.That(strip, Is.Not.Null, "the house is a strip instead,");
                Assert.That(strip.GetComponentsInChildren<Button>(true).Select(b => b.name),
                    Is.EquivalentTo(state.Active.Where(c => !c.isPlayer).Select(c => c.name)), "everybody on it.");
                Assert.That(Words(ActiveRect(EpisodeHud.ScreenHeadName)), Does.Contain("SPEND"), "The head says the action is being spent.");
                // The three ways, as tiles, each with its cost at its foot - a card's foot; at the
                // larger text the tiles are rows, which carry no foot.
                var tiles = ActiveRect("Interaction tiles");
                Assert.That(tiles, Is.Not.Null);
                foreach (var caption in new[] { EpisodeDirector.TalkPrivatelyCaption, EpisodeDirector.AskForInformationCaption, EpisodeDirector.PitchADealCaption })
                {
                    var tile = ButtonWithCaption(caption);
                    Assert.That(tile.transform.IsChildOf(tiles), Is.True, caption + " is one of the three.");
                    if (!larger)
                        Assert.That(tile.GetComponentsInChildren<TMP_Text>(true).Any(t => t.name == "Tile foot" && t.text.Contains("Cost")), Is.True, caption + " says its cost.");
                }
                Assert.That(ActiveRect(EpisodeHud.HouseMovesName), Is.Not.Null, "The whole house's moves are under them,");
                Assert.That(ButtonWithCaption(EpisodeHud.RallyHouseCaption).transform.IsChildOf(ActiveRect(EpisodeHud.HouseMovesName)), Is.True);
                var about = ActiveRect(EpisodeHud.AboutCardName);
                Assert.That(about, Is.Not.Null, "and what you have on them beside.");
                Assert.That(Words(about), Does.Contain(who.name.ToUpperInvariant()).And.Contain(RelationshipWeb.StandingWord(RelationshipWeb.KindOf(state, who.id))));
                var context = ActiveRect(EpisodeHud.YourContextCardName);
                Assert.That(context, Is.Not.Null, "Your own context is beside too,");
                string role = EpisodeDirector.ContextRole(state, out string why);
                Assert.That(Words(context), Does.Contain(role).And.Contain(why), "with your role this week as the rule reads it.");
                if (state.hohId == null)
                    Assert.That(Words(context), Does.Contain(EpisodeDirector.NothingDecidedRole).And.Not.Contain("You are safe this week"),
                        "Before anybody is Head of Household nothing is decided, and nobody is 'safe'.");

                // The board's footer: the way on pinned under its headline, the way back in the
                // secondary slot, and in the strip where the player is over what moving on loses.
                AssertOnTheStrategyStage("Begin the next competition", where, EpisodeDirector.BackToFreeTimeCaption);
                Assert.That(panel.Find(EpisodeHud.PinnedNoteName), Is.Null, "Nothing is wedged under the way on.");
                var footer = ActiveRect(EpisodeHud.StrategyStripName);
                Assert.That(footer, Is.Not.Null, "The footer has its strip.");
                var said = footer.GetComponentsInChildren<TMP_Text>().Where(t => t.gameObject.activeInHierarchy).ToList();
                Assert.That(said.Select(t => t.name), Has.Some.Matches<string>(name =>
                        name == EpisodeHud.LocationCardName || name == EpisodeHud.FreeTimeTipName || name == EpisodeDirector.AdvanceWarningName),
                    "The strip says where the player is, a tip, or what moving on lets pass: " + string.Join(" | ", said.Select(t => t.name + " '" + t.text + "'")));
                string lost = EpisodeDirector.UnusedActionsNote(state);
                if (lost != null && said.All(t => t.name != EpisodeDirector.AdvanceWarningName))
                    Assert.That(said.Any(t => t.name == EpisodeHud.UnusedActionsNoteName && t.text == lost), Is.True,
                        "What moving on loses is the strip's second line: '" + lost + "'.");
                AssertNoRowUnderThePinnedBar(where + " on the batch canvas", assert: false);
                AssertEveryLabelDraws(panel, where);
                if (Application.isBatchMode)
                    yield return CaptureFraming(larger ? "houseguest-screen-large" : "houseguest-screen", settle: false,
                        inspect: frame => AssertNoRowUnderThePinnedBar(where + " on the 16:9 frame", assert: !larger));

                // Another face on the strip is another screen, its strip where it stood: no hold.
                var other = state.Active.First(c => !c.isPlayer && c.id != who.id);
                ActiveRect(EpisodeHud.HouseguestStripName).GetComponentsInChildren<Button>(true).First(b => b.name == other.name).onClick.Invoke();
                Assert.That(Hud.PointerHeld, Is.False, "One houseguest's screen giving way to another's holds nothing off.");
                yield return null; yield return null;
                Assert.That(director.HouseguestScreenFor, Is.EqualTo(other.id), "Pressing another face makes the screen theirs.");
                // And back, to the board, which replaces the screen under the pointer.
                ButtonWithCaption(EpisodeDirector.BackToFreeTimeCaption).onClick.Invoke();
                Assert.That(Hud.PointerHeld, Is.True, "The board replaces the screen under the pointer, which is held off a moment.");
                yield return null; yield return null;
                Assert.That(director.HouseguestScreenFor, Is.Null, "Back to free time.");
                Assert.That(ActiveRect(EpisodeHud.HouseCardsName), Is.Not.Null, "The cards are back,");
                Assert.That(director.IsFreeTimeBoard, Is.True, "on the board.");
                director.ClosePanels();
                yield return null;
                Assert.That(director.HouseguestScreenFor, Is.Null, "Closing the panel forgets the screen.");
            }
            yield return ApplyTextSize(false);
        }

        /// <summary>
        /// No row of the houseguest's screen reaches under the pinned way on, in the frame the HUD
        /// is laid for: the scroll's foot stands clear of the footer's row, and, when
        /// <paramref name="assert"/>, every named row of the screen stands wholly above it - which
        /// is to say the column holds without a scroll (UI-UX-PASS-PLAN U0: the Stage's column cut
        /// the strip's cards and YOUR CONTEXT at the bar). Measured in the canvas's own units, which
        /// hold inside a capture's frame as well as on the batch canvas. Logs how far the column
        /// runs past its viewport either way, and returns it.
        /// </summary>
        private float AssertNoRowUnderThePinnedBar(string where, bool assert)
        {
            Canvas.ForceUpdateCanvases();
            var begin = FindButton(EpisodeDirector.BeginNextCompetitionCaption);
            var bar = CanvasRect((RectTransform)begin.transform);
            var content = LastActive("Episode content");
            var viewport = (RectTransform)content.parent;
            float over = content.rect.height - viewport.rect.height;
            Debug.Log("[Gamesim] " + where + ": the column runs " + content.rect.height.ToString("0") + " in a viewport of "
                + viewport.rect.height.ToString("0") + (over > .5f ? ", " + over.ToString("0") + " past it." : ", and holds."));
            Assert.That(CanvasRect(viewport).yMin, Is.GreaterThanOrEqualTo(bar.yMax - .5f), where + ": the scroll runs under the pinned '" + begin.name + "'.");
            if (!assert) return over;
            foreach (var name in new[] { EpisodeHud.HouseguestStripName, "Interaction tiles", EpisodeHud.HouseMovesName,
                EpisodeHud.YourContextCardName, EpisodeHud.AboutCardName, EpisodeHud.ThreadsCardName })
            {
                var row = LastActive(name);
                if (row == null) continue;
                var at = CanvasRect(row);
                Assert.That(at.yMin, Is.GreaterThanOrEqualTo(bar.yMax - .5f),
                    where + ": '" + name + "' reaches under the pinned '" + begin.name + "': " + at + " against " + bar + ".");
            }
            Assert.That(over, Is.LessThanOrEqualTo(.5f), where + " holds without a scroll: " + content.rect.height.ToString("0")
                + " in " + viewport.rect.height.ToString("0") + ".");
            return over;
        }

        /// <summary>
        /// Before anybody is Head of Household the context card says nothing is decided
        /// (UI-UX-PASS-PLAN D0, the play sweep's row 20): on move-in night's free time - the only one
        /// with no Head of Household, since the rollover clears the roles only as free time closes and
        /// every later free time follows an eviction, where the card says the week is over - it read
        /// "You are safe this week" under a HUD saying "Awaiting HoH", because the safe line was
        /// everything that was not a role. The words are the plan's, the ones U0's ContextRole says.
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

        /// <summary>
        /// UI-UX-PASS-PLAN U0's review, M2: a houseguest's screen and the board stand in each other's
        /// place, so the second press of a double click on what swapped them would land on whatever
        /// is drawn there now - under a card's name, a strip face, a way to spend the action or a move
        /// that commits; under "Back to free time", the board's "Stay in the house", which closes the
        /// panel. For a moment the panel takes no pointer press either way: a double click on a
        /// card's name opens their screen and does nothing else, and one on "Back to free time" goes
        /// back and does nothing else - the panel stays open and nothing is committed. Once the moment
        /// has passed a deliberate press works.
        /// </summary>
        [UnityTest]
        public IEnumerator FreeTime_ADoubleClickBetweenTheBoardAndAHouseguestsScreenTakesNoSecondPress()
        {
            yield return OpenFreeTime();
            var state = director.Snapshot;
            Assert.That(director.IsFreeTimeBoard, Is.True, "Free time is the board.");
            var who = state.Active.First(c => !c.isPlayer);
            Button NameOnTheBoard() => ActiveRect(EpisodeHud.HouseCardsName).GetComponentsInChildren<Button>(true).First(b => b.name == who.name);

            // A double click on a card's name: their screen opens, and the second press takes nothing.
            var name = NameOnTheBoard();
            var namePoint = ScreenCentre(name);
            int revision = director.Snapshot.revision;
            Assert.That(PressAt(namePoint), Is.SameAs(name.gameObject), "The press lands on " + who.name + "'s name.");
            Assert.That(director.HouseguestScreenFor, Is.EqualTo(who.id), "Their screen opens,");
            Assert.That(Hud.PointerHeld, Is.True, "holding the pointer off as it replaces the board.");
            yield return null; yield return null;
            Assert.That(Hud.PointerHeld, Is.True, "The hold outlasts a frame or two.");
            var stray = PressAt(namePoint);
            Assert.That(stray == null || !stray.transform.IsChildOf(ActiveRect(ModalRoot)), Is.True,
                "The second press of a double click on the name lands on " + (stray != null ? stray.name : "nothing") + ".");
            Assert.That(director.IsPanelOpen, Is.True, "The panel stays open,");
            Assert.That(director.HouseguestScreenFor, Is.EqualTo(who.id), "on their screen,");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "and nothing is committed.");

            // A double click on "Back to free time": the board comes back, and "Stay in the house" under the point takes nothing.
            yield return WaitOutThePointerHold();
            var back = ButtonWithCaption(EpisodeDirector.BackToFreeTimeCaption);
            var backPoint = ScreenCentre(back);
            revision = director.Snapshot.revision;
            Assert.That(PressAt(backPoint), Is.SameAs(back.gameObject), "The press lands on 'Back to free time'.");
            Assert.That(director.HouseguestScreenFor, Is.Null, "Back on the board,");
            Assert.That(director.IsFreeTimeBoard, Is.True);
            Assert.That(Hud.PointerHeld, Is.True, "holding the pointer off as it replaces the screen.");
            yield return null; yield return null;
            Assert.That(Hud.PointerHeld, Is.True, "The hold outlasts a frame or two.");
            stray = PressAt(backPoint);
            Assert.That(stray == null || !stray.transform.IsChildOf(ActiveRect(ModalRoot)), Is.True,
                "The second press of a double click on 'Back to free time' lands on " + (stray != null ? stray.name : "nothing") + ".");
            Assert.That(director.IsPanelOpen && director.IsFreeTimeBoard, Is.True, "The panel stays open, on the board,");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "and nothing is committed.");

            // Once the moment has passed, a deliberate press works.
            yield return WaitOutThePointerHold();
            Assert.That(Hud.PointerHeld, Is.False, "The hold has lifted.");
            var again = NameOnTheBoard();
            Assert.That(PressAt(ScreenCentre(again)), Is.SameAs(again.gameObject), "A deliberate press on the name lands,");
            Assert.That(director.HouseguestScreenFor, Is.EqualTo(who.id), "and opens their screen.");
            director.ClosePanels();
            yield return null;
            Assert.That(Hud.PointerHeld, Is.False, "Closing the panel ends the hold.");
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
