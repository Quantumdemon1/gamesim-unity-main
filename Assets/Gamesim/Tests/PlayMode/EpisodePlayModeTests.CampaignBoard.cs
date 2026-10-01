using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The campaign as one board on the strategy stage (PACK8-PASS-PLAN B4, mockup 83): the owner's
    /// screenshots 77 to 80 were the campaign scrolling in the stage, or in a house event's band once
    /// a nominee came pleading. These hold that it fits the stage at both text sizes, a plea stays
    /// on it, a full house pages rather than scrolls, the tabs and pages are view state, and the
    /// keyboard walks it - with every pinned name and caption where it always was.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private const string CloseCampaigning = "Close campaigning and open voting";

        /// <summary>The campaign's column holds all of it: no scroll, and the hint says so.</summary>
        private void AssertCampaignFits(string where)
        {
            Canvas.ForceUpdateCanvases();
            var content = ActiveRect("Episode content");
            var viewport = (RectTransform)content.parent;
            Assert.That(content.rect.height, Is.LessThanOrEqualTo(viewport.rect.height + .5f),
                where + " fits the stage without a scroll: " + content.rect.height.ToString("0") + " in " + viewport.rect.height.ToString("0") + ".");
            var board = ActiveRect(EpisodeHud.CampaignBoardName);
            Assert.That(board, Is.Not.Null, where + " is drawn as the campaign's board.");
            AssertInside(ScreenRect(ActiveRect("Episode panel")), board, where + "'s board");
        }

        /// <summary>The names of the cards on the voters' grid now on screen.</summary>
        private string[] CampaignCards()
        {
            var grid = ActiveRect(EpisodeHud.CampaignVotersName);
            Assert.That(grid, Is.Not.Null, "The votes are a grid of cards.");
            return grid.Cast<Transform>().Select(card => card.name)
                .Where(name => name.StartsWith("Voter · ") || name.StartsWith("Head of Household · ")).ToArray();
        }

        [UnityTest]
        public IEnumerator CampaignBoard_TheCampaignIsOneScreenAtBothTextSizes()
        {
            yield return InstallCampaign(46);
            yield return AtBothTextSizes(larger =>
            {
                // The state the open panel was drawn from, read once it is open: opening it flushes
                // the house's ticks, and a houseguest's move toward the player moves the player's own
                // reading of them, which is what the heroes print.
                var state = director.Snapshot;
                var hoh = state.Find(state.hohId);
                string where = "The campaign" + (larger ? " at the larger text" : "");
                AssertOnTheStrategyStage(CloseCampaigning, where);
                AssertCampaignFits(where);
                Assert.That(ActiveRect(EpisodeHud.CeremonyTitleName).GetComponent<TMP_Text>().text, Is.EqualTo("Campaign"), "The title keeps its word,");
                Assert.That(ActiveRect(EpisodeHud.CampaignHeadlineName).GetComponent<TMP_Text>().text, Is.EqualTo(EpisodeDirector.CampaignSwingHeadline),
                    "and the mockup's headline is a label of its own.");
                Assert.That(PanelWords(ActiveRect("Episode panel")), Does.Not.Contain(EpisodeDirector.HouseStatus(state)),
                    "The week's roles are the situation card's rows, not a paragraph over the board.");

                // The block as heroes: the player's own reading of each, and the one way to talk to them.
                foreach (var id in state.nominees)
                {
                    var nominee = state.Find(id);
                    var hero = ActiveRect("Nominee · " + nominee.name);
                    Assert.That(hero, Is.Not.Null, nominee.name + " is a hero.");
                    var reading = hero.GetComponentsInChildren<TMP_Text>().Single(label => label.name == "Standing");
                    Assert.That(reading.text, Is.EqualTo(EpisodeHud.CampaignStandingWords(state, id)), "Your reading of them, said as yours.");
                    Assert.That(reading.text, Does.StartWith("You: "));
                    var talk = ButtonWithCaption(EpisodeHud.CastTalkCaption(nominee.name.Split(' ')[0]));
                    Assert.That(talk.transform.IsChildOf(hero), Is.True, "A nominee's talk button is on their hero, and only there.");
                }

                var situation = ActiveRect(EpisodeHud.CampaignSituationName);
                Assert.That(situation, Is.Not.Null, "This week's situation is a card.");
                var facts = situation.GetComponentsInChildren<TMP_Text>().Where(label => label.name == "Situation value").Select(label => label.text).ToArray();
                Assert.That(facts, Does.Contain(hoh.name), "It names the Head of Household,");
                Assert.That(facts, Does.Contain(string.Join(" and ", state.nominees.Select(id => state.Find(id).name))), "the block,");
                Assert.That(facts, Does.Contain(state.Active.Count() + " of " + state.contestants.Count + " remain"), "and how many are left.");

                // The votes: the voters' cards and the Head of Household's, who votes only on a tie.
                var cards = CampaignCards();
                Assert.That(cards, Does.Contain("Head of Household · " + hoh.name));
                var crown = ActiveRect(EpisodeHud.CampaignVotersName).Find("Head of Household · " + hoh.name);
                var crownLabels = crown.GetComponentsInChildren<TMP_Text>().Select(label => label.name).ToArray();
                Assert.That(crownLabels, Does.Contain("Tie line"), "The Head of Household votes only on a tie,");
                Assert.That(crownLabels, Does.Not.Contain(EpisodeHud.VoteReadLineName), "so their card has no read.");
                foreach (var voter in EpisodeEngine.Voters(state).Where(actor => !actor.isPlayer))
                    Assert.That(cards, Does.Contain("Voter · " + voter.name), voter.name + " is a card.");

                var chip = ActiveRect(EpisodeHud.CampaignActionsName);
                Assert.That(chip, Is.Not.Null, "The conversations left are a chip.");
                int budget = EpisodeEngine.SocialActionBudget(state);
                Assert.That(chip.GetComponentsInChildren<TMP_Text>().Select(label => label.text),
                    Is.EquivalentTo(new[] { "Interactions available", Mathf.Max(0, budget - EpisodeEngine.SocialActionsSpent(state)) + " of " + budget }),
                    "in the meter's own words.");

                bool footUp = ActiveRect(EpisodeHud.CampaignCardsName) != null;
                bool folded = ButtonWithCaptionOrNull(EpisodeHud.CampaignTabGoals) != null;
                Assert.That(footUp != folded, Is.True, "The goals, the intel and the tip are on the board or folded into a tab of their own - one or the other.");
                if (footUp)
                {
                    Assert.That(ActiveRect(EpisodeHud.CampaignGoalsName), Is.Not.Null);
                    Assert.That(ActiveRect(EpisodeHud.CampaignIntelName), Is.Not.Null);
                    Assert.That(PanelWords(ActiveRect(EpisodeHud.CampaignTipName)), Does.Contain(EpisodeDirector.CampaignTip), "The tip is the game's own rule copy.");
                }

                var strip = ActiveRect(EpisodeHud.StrategyStripName);
                Assert.That(strip, Is.Not.Null, "The footer says what comes next.");
                var words = strip.GetComponentsInChildren<TMP_Text>().Single();
                Assert.That(words.name == EpisodeHud.UpNextName ? words.text == EpisodeDirector.CampaignUpNext : words.name == EpisodeDirector.AdvanceWarningName,
                    Is.True, "Up next, unless a storyline moving on lets pass outranks it: '" + words.text + "'.");
                AssertEveryLabelDraws(ActiveRect(EpisodeHud.CampaignBoardName), where + "'s board");
            });
            if (Application.isBatchMode)
            {
                yield return OpenStation();
                yield return CaptureFraming("strategy-campaign-board");
                director.ClosePanels();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator CampaignBoard_APleaIsAnsweredOnTheStageAndTheNextFollows()
        {
            HoldTheHouseForTheFixture();
            yield return InstallCampaign(46, state =>
            {
                state.replyCards.Add(new ReplyCardState { id = "reply-8", week = state.week, kind = ReplyCards.Plea, fromId = state.nominees[0], aboutId = state.nominees[1] });
                state.replyCards.Add(new ReplyCardState { id = "reply-9", week = state.week, kind = ReplyCards.Plea, fromId = state.nominees[1], aboutId = state.nominees[0] });
            });
            yield return OpenStation();
            yield return null;
            var state = director.Snapshot;
            var oldest = state.replyCards[0];
            AssertOnTheStrategyStage(CloseCampaigning, "The campaign with two pleas waiting");
            AssertCampaignFits("The campaign with two pleas waiting");
            Assert.That(ShownText(), Does.Contain(EpisodeHud.ReplyCardEyebrow));
            Assert.That(ShownText(), Does.Contain(ReplyCards.Title(state, oldest)), "The oldest plea is the one up.");
            Assert.That(ShownText(), Does.Contain(ReplyCards.Message(state, oldest)));
            var plea = ActiveRect(EpisodeHud.CampaignPleaName);
            Assert.That(plea, Is.Not.Null, "The plea is a strip on the board, not a house event's band.");
            var choices = ActiveRect(EpisodeHud.EventChoicesName);
            Assert.That(choices.IsChildOf(plea), Is.True, "Its answers are on the strip, under the name they have always had.");
            foreach (var reply in ReplyCards.Replies(ReplyCards.Plea))
                Assert.That(ButtonWithCaption(EpisodeHud.ReplyCaption(reply.Label)).transform.IsChildOf(choices), Is.True, reply.Label);
            var count = plea.GetComponentsInChildren<TMP_Text>().Single(label => label.name == EpisodeHud.CampaignPleaCountName);
            Assert.That(count.text, Does.StartWith("1 of 2").And.Contain(state.Find(state.replyCards[1].fromId).name),
                "Two came to the player: this is the first, and it says who is next.");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.CampaignTabGoals), Is.Not.Null, "A plea folds the foot cards into a tab.");
            Assert.That(ActiveRect(EpisodeHud.CampaignCardsName), Is.Null);
            yield return null;
            // Found again after the frame: a render in it - a body finishing its assembly - rebuilds
            // the strip, and the grid found before it would be the copy on its way out.
            var answers = ActiveRect(EpisodeHud.EventChoicesName);
            var focus = EventSystem.current.currentSelectedGameObject;
            Assert.That(focus != null && answers != null && focus.transform.IsChildOf(answers), Is.True,
                "The panel opens on the plea's answers: " + (focus != null ? focus.name : "nothing") + ".");

            ButtonWithCaption(EpisodeHud.ReplyCaption("Stay noncommittal")).onClick.Invoke();
            yield return null; yield return null;
            if (!director.IsPhasePanelOpen) yield return OpenStation();
            var after = director.Snapshot;
            Assert.That(after.replyCards.Select(card => card.id), Is.EqualTo(new[] { "reply-9" }), "Answering settles the card it belongs to.");
            AssertOnTheStrategyStage(CloseCampaigning, "The campaign with one plea left");
            Assert.That(ShownText(), Does.Contain(ReplyCards.Title(after, after.replyCards[0])), "The next plea follows,");
            Assert.That(ActiveRect(EpisodeHud.CampaignPleaCountName), Is.Null, "alone, so with no count.");

            ButtonWithCaption(EpisodeHud.ReplyCaption("Promise support")).onClick.Invoke();
            yield return null; yield return null;
            if (!director.IsPhasePanelOpen) yield return OpenStation();
            var done = director.Snapshot;
            Assert.That(done.replyCards, Is.Empty);
            Assert.That(ActiveRect(EpisodeHud.CampaignPleaName), Is.Null, "Answered, the strip is gone.");
            Assert.That(ShownText(), Does.Not.Contain(EpisodeHud.ReplyCardEyebrow));
            AssertOnTheStrategyStage(CloseCampaigning, "The campaign with the pleas answered");
            AssertCampaignFits("The campaign with the pleas answered");
            var goals = CampaignBrief.Goals(done).Where(goal => goal.kind == CampaignBrief.GoalKinds.Plea).ToList();
            Assert.That(goals.Select(goal => goal.done), Is.EqualTo(new[] { true, true }), "Both pleas are answered goals now.");
            if (ActiveRect(EpisodeHud.CampaignGoalsName) != null)
                foreach (var goal in goals)
                    Assert.That(PanelWords(ActiveRect(EpisodeHud.CampaignGoalsName)), Does.Contain(goal.text), "The goals card says so.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator CampaignBoard_AFullHousePagesItsVotesAndTheTabsAreViewState()
        {
            HoldTheHouseForTheFixture();
            var full = FullHouse(46, EpisodeValidation.MaximumCast);
            AtVetoMeeting(full, false);
            full.phase = EpisodePhase.Campaign;
            full.vetoResolved = true;
            Assert.That(EpisodeValidation.TryValidate(full, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(full);
            yield return ReloadEpisode();
            var state = director.Snapshot;
            int expected = EpisodeEngine.Voters(state).Count(actor => !actor.isPlayer) + (state.Find(state.hohId).isPlayer ? 0 : 1);
            // Sixteen in the house still fit the stage at the larger text, before the pages are walked at the resting size.
            yield return AtBothTextSizes(larger => AssertCampaignFits("A full house's campaign" + (larger ? " at the larger text" : "")));
            yield return OpenStation();
            yield return null;
            AssertOnTheStrategyStage(CloseCampaigning, "A full house's campaign");
            AssertCampaignFits("A full house's campaign");

            // A tab or a page is view state: the press re-renders the panel within itself, so it is
            // measured across the press, not across frames in which the house's own ticks may commit.
            void Press(string caption)
            {
                int before = director.Snapshot.revision;
                ButtonWithCaption(caption).onClick.Invoke();
                Assert.That(director.Snapshot.revision, Is.EqualTo(before), "'" + caption + "' is view state: it commits nothing.");
            }

            // The votes page rather than scroll: every card once, across the pages.
            var seen = new List<string>();
            var first = CampaignCards();
            Assert.That(first.Length, Is.GreaterThan(0).And.LessThan(expected), "A full house runs past one row.");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.CampaignPreviousCaption), Is.Null, "The first page has no way back.");
            seen.AddRange(first);
            for (int page = 0; page < 8 && ButtonWithCaptionOrNull(EpisodeHud.CampaignNextCaption) != null; page++)
            {
                Press(EpisodeHud.CampaignNextCaption);
                yield return null;
                AssertCampaignFits("A full house's campaign, page " + (page + 2));
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.CampaignPreviousCaption), Is.Not.Null, "A later page has a way back.");
                seen.AddRange(CampaignCards());
            }
            Assert.That(seen, Is.Unique, "No card is on two pages.");
            Assert.That(seen, Has.Count.EqualTo(expected), "and every one is on a page.");

            // Each tab fits too, and none of them is a conversation's control.
            foreach (var caption in new[] { EpisodeHud.CampaignTabIntel, EpisodeHud.CampaignTabPoints, EpisodeHud.CampaignTabStories, EpisodeHud.CampaignTabOutlook })
            {
                Press(caption);
                yield return null;
                AssertCampaignFits("A full house's campaign on " + caption);
                Assert.That(ActiveRect(EpisodeHud.CampaignVotersName), Is.Null, caption + " is its own body.");
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.AskVoteCaption), Is.Null, caption + " offers no conversation's control.");
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ReadPersonCaption), Is.Null);
                AssertEveryLabelDraws(ActiveRect(EpisodeHud.CampaignBodyName), caption);
            }
            Assert.That(ActiveRect(EpisodeHud.CampaignOutlookName), Is.Not.Null, "The outlook reads the house as the notebook does,");
            Assert.That(ActiveRect(EpisodeHud.WhipCountName), Is.Null, "under a name of its own.");
            Press(EpisodeHud.CampaignTabTalk);
            yield return null;
            Assert.That(CampaignCards(), Is.EqualTo(first), "Back on the houseguests, from the first page.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator CampaignBoard_TheKeyboardWalksTheBoardFromItsFirstCard()
        {
            yield return InstallCampaign(46);
            yield return OpenStation();
            yield return null;
            yield return null;
            var opened = EventSystem.current.currentSelectedGameObject;
            var tabs = new[] { EpisodeHud.CampaignTabTalk, EpisodeHud.CampaignTabIntel, EpisodeHud.CampaignTabPoints,
                EpisodeHud.CampaignTabStories, EpisodeHud.CampaignTabOutlook, EpisodeHud.CampaignTabGoals };
            Assert.That(opened, Is.Not.Null, "The campaign opens with the keyboard somewhere.");
            Assert.That(tabs, Does.Not.Contain(opened.name), "It opens on a card, not on a tab.");
            Assert.That(opened.transform.IsChildOf(ActiveRect(EpisodeHud.CampaignBoardName)), Is.True, "and on the board: " + opened.name + ".");
            yield return AssertKeyboardRing("campaign", ModalRoot);
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The board keeps its tabs, or gives them way to the line that says where they went - one or
        /// the other - and "More ways to campaign" is on it either way.
        /// </summary>
        private void AssertTabsOrWaiting(string where)
        {
            var board = ActiveRect(EpisodeHud.CampaignBoardName);
            bool tabs = ActiveRect(EpisodeHud.CampaignTabsName) != null;
            var waiting = ActiveRect(EpisodeHud.CampaignWaitingName);
            Assert.That(tabs != (waiting != null), Is.True, where + " keeps its tabs or the line that says where they went, one or the other.");
            if (waiting != null)
                Assert.That(waiting.GetComponentsInChildren<TMP_Text>().Select(label => label.text), Does.Contain(EpisodeHud.CampaignWaitingLine),
                    where + " says where the tabs went.");
            Assert.That(ButtonWithCaption(EpisodeDirector.CampaignMoreCaption).transform.IsChildOf(board), Is.True,
                where + " keeps 'More ways to campaign' on the board.");
        }

        /// <summary>
        /// A plea and a Have-Not's line at once, at both text sizes - the most a board with nothing
        /// over it carries: it fits the stage with the plea's answers on it and the foot cards folded
        /// away, and where even a folded tab has no room the tabs give way to the line that says
        /// where they went.
        /// </summary>
        [UnityTest]
        public IEnumerator CampaignBoard_APleaAndAHaveNotsLineFitAtBothTextSizes()
        {
            HoldTheHouseForTheFixture();
            yield return InstallCampaign(46, state =>
            {
                state.haveNots.Add(state.playerId);
                state.replyCards.Add(new ReplyCardState { id = "reply-8", week = state.week, kind = ReplyCards.Plea, fromId = state.nominees[0], aboutId = state.nominees[1] });
            });
            yield return AtBothTextSizes(larger =>
            {
                string where = "The campaign with a plea and a Have-Not's line" + (larger ? " at the larger text" : "");
                AssertOnTheStrategyStage(CloseCampaigning, where);
                AssertCampaignFits(where);
                var plea = ActiveRect(EpisodeHud.CampaignPleaName);
                var answers = ActiveRect(EpisodeHud.EventChoicesName);
                Assert.That(plea != null && answers != null && answers.IsChildOf(plea), Is.True, where + ": the plea's answers are on its strip.");
                Assert.That(ActiveRect(EpisodeHud.CampaignHaveNotName), Is.Not.Null, where + " says what being a Have-Not costs.");
                Assert.That(ActiveRect(EpisodeHud.CampaignCardsName), Is.Null, where + ": the plea folds the foot cards away.");
                AssertTabsOrWaiting(where);
                AssertEveryLabelDraws(ActiveRect(EpisodeHud.CampaignBoardName), where + "'s board");
            });
        }

        /// <summary>
        /// A season played to the second week's campaign - the first week asks nothing of a story -
        /// with one story beat waiting on the player and nothing else: no plea and no legacy house
        /// event. The beat is the first arc that casts at the block's anchor with a houseguest's
        /// approach of at most four options, two rows of tiles, put to the player by the engine's own
        /// seam. The player competes to win, so the walk keeps them in the house to that week.
        /// </summary>
        private IEnumerator InstallCampaignWithABeat()
        {
            EpisodeState fixture = null;
            var arcs = StoryCatalog.All.Where(arc => arc.startAnchors.Contains(StoryAnchors.BlockSet)).Select(arc => arc.id).ToList();
            for (uint seed = 1; seed <= 20 && fixture == null; seed++)
            {
                var engine = new EpisodeEngine(ContentCatalog.Create(seed));
                for (int guard = 0; guard < 200; guard++)
                {
                    var current = engine.Snapshot;
                    if (current.phase == EpisodePhase.Finished || current.Find(current.playerId).status != ContestantStatus.Active) break;
                    if (current.phase == EpisodePhase.Campaign && current.week >= 2 && current.nominees.Count == 2 && current.pendingDiary == null)
                    {
                        foreach (var arc in arcs)
                        {
                            var trial = current.Clone();
                            // Only the beat waits: the walk answers no plea and no house event.
                            trial.replyCards.Clear();
                            trial.houseEvents.RemoveAll(e => !e.resolved && !e.IsStory);
                            EpisodeEngine.EnableStory(trial, trial.week);
                            if (!EpisodeEngine.StartStory(trial, arc, StoryAnchors.BlockSet)) continue;
                            var open = EpisodeEngine.OpenStoryBeats(trial);
                            if (open.Count == 1 && open[0].surface == StorySurfaces.Approach && open[0].choices.Count <= 4
                                && EpisodeValidation.TryValidate(trial, out _)) { fixture = trial; break; }
                        }
                        break;
                    }
                    var next = NextCommand(current);
                    if (next.kind == EpisodeCommandKind.Compete) next.performance = 1.0;
                    Assert.That(engine.Apply(next).accepted, Is.True);
                }
            }
            Assert.That(fixture, Is.Not.Null, "No bounded season reached a later campaign with a houseguest's approach to put to the player.");
            // The house's own clock must neither write over the fixture nor commit under the test.
            HoldTheHouseForTheFixture();
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            yield return null; yield return null;
        }

        /// <summary>
        /// A story beat waiting on the campaign is the step (PACK8-PASS-PLAN decision 7): it comes
        /// before the board, inside the stage, and the board makes room for it rather than pushing
        /// the column into a scroll - its foot cards folded away, and its tabs too where even a
        /// folded tab has no room - with the way on pinned, at both text sizes. Opening the screen
        /// answers nothing.
        /// </summary>
        [UnityTest]
        public IEnumerator CampaignBoard_AStoryBeatComesFirstAndTheBoardMakesRoomForIt()
        {
            yield return InstallCampaignWithABeat();
            Assert.That(EpisodeEngine.OpenStoryBeats(director.Snapshot), Has.Count.EqualTo(1), "The fixture has a beat waiting.");
            yield return AtBothTextSizes(larger =>
            {
                string where = "The campaign with a beat waiting" + (larger ? " at the larger text" : "");
                AssertOnTheStrategyStage(CloseCampaigning, where);
                AssertCampaignFits(where);
                var choices = ActiveRect(EpisodeHud.StoryChoicesName);
                Assert.That(choices, Is.Not.Null, where + ": the beat's choices are on the screen,");
                Assert.That(choices.parent, Is.SameAs(ActiveRect("Episode content")), "inside the stage's column,");
                var board = ActiveRect(EpisodeHud.CampaignBoardName);
                Assert.That(choices.GetSiblingIndex(), Is.LessThan(board.GetSiblingIndex()), "and before the board: the beat is the step.");
                Assert.That(ActiveRect(EpisodeHud.CampaignCardsName), Is.Null, where + ": a beat folds the foot cards away.");
                AssertTabsOrWaiting(where);
                AssertEveryLabelDraws(board, where + "'s board");
            });
            Assert.That(EpisodeEngine.OpenStoryBeats(director.Snapshot), Has.Count.EqualTo(1), "Opening the screen answers nothing.");
        }
    }
}
