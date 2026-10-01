using System.Collections;
using System.Collections.Generic;
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
    /// Free time as one board on the strategy stage (ACTIONS-DEALS-ALLIANCES-PLAN F1, the owner's
    /// mockup 87). The owner's screenshots 84 to 86 were free time scrolling in a house event's band
    /// once a story beat was waiting, and in the plain stage without one, with the way on at the foot
    /// of the scroll. These hold that the board fits the stage without a scroll at both text sizes in
    /// houses of four, eight and sixteen - with nothing waiting, a beat waiting, somebody come to the
    /// player, and as a Have-Not - while a house of three's free time is still the Final 3's; that the
    /// cards page to everybody; that a beat opens as the step and goes back; that the moves cost what
    /// they say and lock when nothing is left; that the keyboard walks the board; and that the way on
    /// is one control.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>What is waiting on the player in a free-time fixture.</summary>
        private enum FreeTimeWaiting { Nothing, Beat, Reply, HaveNot }

        /// <summary>
        /// A story beat waiting in free time with three options and its lapse: a beat the engine
        /// answers by option id and lapses at the end of the day, drawn from its saved words because
        /// no catalogue arc is behind it.
        /// </summary>
        private static HouseEventState FreeTimeBeat(EpisodeState state, int index)
        {
            var beat = new HouseEventState
            {
                id = "free-time-beat-" + index, kind = HouseEventKind.Story, contentId = "free-time-board-test:beat-" + index,
                cycleId = "free-time-cycle-" + index, title = index == 0 ? "A Word After Dinner" : "A Second Word",
                narrative = "Somebody catches you on your way out of the kitchen. They want to talk about the week, and they want to "
                    + "do it now, before the house settles down for the night.",
                week = state.week, closesAnchor = StoryAnchors.SocialClose, surface = StorySurfaces.Conversation, lapseOptionId = "let-it-go",
            };
            string[] labels = { "Listen to them", "Change the subject", "Ask what they want" };
            string[] risks = { HouseEventRisk.Low, HouseEventRisk.Medium, HouseEventRisk.Low };
            for (int i = 0; i < labels.Length; i++)
                beat.choices.Add(new HouseEventChoice
                {
                    label = (index == 0 ? "" : "Again: ") + labels[i], optionId = "option-" + i, risk = risks[i],
                    description = "Say it plainly and see where it lands.",
                });
            beat.choices.Add(new HouseEventChoice { label = index == 0 ? "Let it go" : "Let that go too", optionId = "let-it-go", description = "Let the moment pass.", lapse = true });
            return beat;
        }

        /// <summary>
        /// A house of <paramref name="houseguests"/> on move-in night under the week's windows, the
        /// strategy rules and the story, as every season the director starts plays, with what is
        /// waiting on the player. The house's own clock is held, so nothing commits under the test.
        /// </summary>
        private IEnumerator InstallFreeTime(int houseguests, FreeTimeWaiting waiting, int beats = 1)
        {
            HoldTheHouseForTheFixture();
            var state = FullHouse(4501, houseguests);
            state.strategyRulesStartWeek = 1;
            state.haveNotRulesStartWeek = 1;
            EpisodeEngine.EnableWeek(state, state.week);
            EpisodeEngine.EnableStory(state, state.week);
            state.houseEvents.RemoveAll(item => !item.resolved);
            state.replyCards.Clear();
            var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            if (waiting == FreeTimeWaiting.Beat)
                for (int i = 0; i < beats; i++) state.houseEvents.Add(FreeTimeBeat(state, i));
            if (waiting == FreeTimeWaiting.Reply)
                state.replyCards.Add(new ReplyCardState { id = "reply-31", week = state.week, kind = ReplyCards.Confrontation, fromId = npcs[0] });
            if (waiting == FreeTimeWaiting.HaveNot) state.haveNots.Add(state.playerId);
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            yield return null;
        }

        /// <summary>Every live, pressable control showing exactly this caption.</summary>
        private int LiveButtonsCarrying(string caption) => director.GetComponentsInChildren<Button>(true)
            .Count(button => button.IsActive() && button.IsInteractable()
                && button.GetComponentsInChildren<TMP_Text>(true).Any(text => text.text == caption));

        /// <summary>The names of the cards on the house's row now on screen.</summary>
        private string[] FreeTimeCards()
        {
            var row = ActiveRect(EpisodeHud.HouseCardsName);
            Assert.That(row, Is.Not.Null, "The house is a row of cards.");
            return row.Cast<Transform>().Select(card => card.name).Where(name => name.StartsWith("Houseguest · ")).ToArray();
        }

        /// <summary>One of the budget card's labels, by its name.</summary>
        private TMP_Text BudgetLabel(string name)
        {
            var head = ActiveRect(EpisodeHud.ScreenHeadName);
            Assert.That(head, Is.Not.Null, "Free time has its budget card.");
            return head.GetComponentsInChildren<TMP_Text>().SingleOrDefault(text => text.name == name);
        }

        /// <summary>
        /// Free time's board open now: on the strategy stage with the way on and "Stay in the house"
        /// pinned and nothing else, one board that holds without a scroll, its rows in the mockup's
        /// order inside it and inside the panel, every label drawing some of its words, and "Begin
        /// the next competition" one live control.
        /// </summary>
        private void AssertFreeTimeBoard(string where)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(director.IsFreeTimeBoard, Is.True, where + " is free time's board.");
            AssertOnTheStrategyStage(EpisodeDirector.BeginNextCompetitionCaption, where, EpisodeDirector.StayInTheHouseCaption);
            var content = ActiveRect("Episode content");
            var viewport = (RectTransform)content.parent;
            Assert.That(content.rect.height, Is.LessThanOrEqualTo(viewport.rect.height + .5f),
                where + " fits the stage without a scroll: " + content.rect.height.ToString("0") + " in " + viewport.rect.height.ToString("0") + ".");
            var panel = ActiveRect("Episode panel");
            var board = ActiveRect(EpisodeHud.FreeTimeBoardName);
            Assert.That(board, Is.Not.Null, where + " is drawn as the board.");
            AssertInside(ScreenRect(panel), board, where + "'s board");
            var rows = new[] { EpisodeHud.FreeTimeHeroName, EpisodeHud.StoryStripName, EpisodeHud.HouseCardsName, EpisodeHud.HouseMovesName }
                .Select(name => ActiveRect(name)).ToArray();
            for (int i = 0; i < rows.Length; i++)
            {
                Assert.That(rows[i], Is.Not.Null, where + " has all four of its rows.");
                Assert.That(rows[i].IsChildOf(board), Is.True, where + "'s '" + rows[i].name + "' is on the board.");
                AssertInside(ScreenRect(board), rows[i], where + "'s '" + rows[i].name + "'");
                if (i > 0)
                    Assert.That(ScreenRect(rows[i]).yMax, Is.LessThanOrEqualTo(ScreenRect(rows[i - 1]).yMin + .5f),
                        where + ": '" + rows[i].name + "' stands under '" + rows[i - 1].name + "'.");
            }
            foreach (Transform card in rows[2]) AssertInside(ScreenRect(board), (RectTransform)card, where + "'s card '" + card.name + "'");
            foreach (Transform tile in rows[3]) AssertInside(ScreenRect(board), (RectTransform)tile, where + "'s tile '" + tile.name + "'");
            var head = ActiveRect(EpisodeHud.ScreenHeadName);
            Assert.That(head != null && head.IsChildOf(board), Is.True, where + " has its budget card.");
            AssertInside(ScreenRect(board), head, where + "'s budget card");
            Assert.That(Mathf.Abs(ScreenRect(head).yMax - ScreenRect(rows[0]).yMax) < 1f && ScreenRect(head).xMin >= ScreenRect(rows[0]).xMax - .5f, Is.True,
                where + ": the budget card stands beside the hero, on its right.");
            Assert.That(ActiveRect(EpisodeHud.ThreadsCardName).IsChildOf(rows[1]), Is.True, where + ": the threads are on the story strip.");
            Assert.That(ActiveRect(EpisodeHud.CurrentPlayCardName).IsChildOf(board), Is.True, where + ": the play is on the board, in the hero or on the strip.");
            AssertEveryLabelDraws(board, where + "'s board");
            Assert.That(LiveButtonsCarrying(EpisodeDirector.BeginNextCompetitionCaption), Is.EqualTo(1),
                where + ": 'Begin the next competition' is one live control.");
        }

        /// <summary>
        /// Nothing waiting, in a house of four, eight and sixteen at both text sizes: the board fits,
        /// the play in motion is the hero, and move-in night's rule says what the night has. A house
        /// of three's free time is the Final 3's window, which keeps Endgame Preparation (decision 17).
        /// </summary>
        [UnityTest]
        public IEnumerator FreeTimeBoard_NothingWaitingFitsInHousesOfFourEightAndSixteen()
        {
            foreach (int house in new[] { 4, 8, 16 })
            {
                yield return InstallFreeTime(house, FreeTimeWaiting.Nothing);
                yield return AtBothTextSizes(larger =>
                {
                    string where = "Free time in a house of " + house + (larger ? " at the larger text" : "");
                    AssertFreeTimeBoard(where);
                    var state = director.Snapshot;
                    Assert.That(ActiveRect(EpisodeHud.CurrentPlayCardName).IsChildOf(ActiveRect(EpisodeHud.FreeTimeHeroName)), Is.True,
                        where + ": with nothing waiting the play in motion is the hero.");
                    Assert.That(ActiveRect(EpisodeHud.StoryBannerName), Is.Null, where + " has no beat to show.");
                    Assert.That(BudgetLabel(EpisodeHud.BudgetRuleName).text, Is.EqualTo(EpisodeDirector.BudgetRule(state)), where + " says the week's rule,");
                    Assert.That(BudgetLabel(EpisodeHud.BudgetRuleName).text, Does.EndWith("tonight; " + (house <= 8 ? "it does" : "they do") + " not carry into the week."),
                        where + " in move-in night's own words.");
                    // Seven others fit one page at either size on either frame; fifteen page.
                    if (house <= 8) Assert.That(FreeTimeCards().Length, Is.EqualTo(house - 1), where + ": everybody else is a card on one page.");
                    else
                    {
                        Assert.That(FreeTimeCards().Length, Is.LessThan(house - 1), where + ": the house runs past one page,");
                        Assert.That(ButtonWithCaptionOrNull(EpisodeHud.CampaignNextCaption), Is.Not.Null, "and the pager says so.");
                    }
                });
            }

            yield return InstallFreeTime(3, FreeTimeWaiting.Nothing);
            // A house of three is the endgame's: any of its cards up over the house goes first.
            yield return PutAwayTheCards();
            yield return OpenStation();
            yield return null;
            Assert.That(director.IsFreeTimeBoard, Is.False, "A house of three's free time is the Final 3's window,");
            Assert.That(ActiveRect(EpisodeHud.FreeTimeBoardName), Is.Null, "which is not the board yet,");
            Assert.That(Words(LastActive(EpisodeHud.ScreenHeadName)), Does.Contain(EpisodeDirector.EndgamePreparationTitle), "but Endgame Preparation.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// A story beat waiting, in a house of eight and of sixteen at both text sizes: a banner in the
        /// hero's place with its eyebrow and name and the way in, its options behind it; a second beat
        /// a chip on the story strip; what moving on lets pass in the footer's strip; and the board
        /// fits. The batch run photographs it in a house of eight, and of sixteen at the larger text.
        /// </summary>
        [UnityTest]
        public IEnumerator FreeTimeBoard_ABeatWaitingIsABannerAndFitsInHousesOfEightAndSixteen()
        {
            foreach (int house in new[] { 8, 16 })
            {
                yield return InstallFreeTime(house, FreeTimeWaiting.Beat, 2);
                var beats = EpisodeEngine.OpenStoryBeats(director.Snapshot);
                Assert.That(beats, Has.Count.EqualTo(2), "The fixture has two beats waiting.");
                yield return AtBothTextSizes(larger =>
                {
                    string where = "Free time with two beats waiting in a house of " + house + (larger ? " at the larger text" : "");
                    AssertFreeTimeBoard(where);
                    var banner = ActiveRect(EpisodeHud.StoryBannerName);
                    Assert.That(banner, Is.Not.Null, where + ": the first beat is the hero's banner.");
                    Assert.That(Words(banner), Does.Contain(beats[0].title), where + ": the banner names it,");
                    Assert.That(ButtonWithCaption(EpisodeDirector.AnswerBeatCaption).transform.IsChildOf(banner), Is.True, "with the way in on it.");
                    Assert.That(ActiveRect(EpisodeHud.StoryChoicesName), Is.Null, where + ": its options wait behind Answer.");
                    Assert.That(ButtonWithCaptionOrNull(EpisodeHud.EventChoiceCaption(beats[0].choices[0].label)), Is.Null);
                    var chip = ButtonWithCaption(beats[1].title);
                    Assert.That(chip.transform.IsChildOf(ActiveRect(EpisodeHud.StoryStripName)), Is.True, where + ": the second beat is a chip on the strip.");
                    var warning = ActiveRect(EpisodeHud.StrategyStripName).GetComponentsInChildren<TMP_Text>().Single();
                    Assert.That(warning.name, Is.EqualTo(EpisodeDirector.AdvanceWarningName), where + ": what moving on lets pass outranks where the player is.");
                    Assert.That(warning.text, Does.StartWith("Moving on lets 2 storylines pass: "));
                });
                Assert.That(EpisodeEngine.OpenStoryBeats(director.Snapshot), Has.Count.EqualTo(2), "Opening the screen answers nothing.");
            }
            if (!Application.isBatchMode) yield break;
            yield return InstallFreeTime(8, FreeTimeWaiting.Beat);
            yield return OpenStation();
            yield return CaptureFraming("free-time-board-8");
            director.ClosePanels();
            yield return InstallFreeTime(16, FreeTimeWaiting.Beat);
            yield return ApplyTextSize(true);
            yield return OpenStation();
            yield return CaptureFraming("free-time-board-16");
            director.ClosePanels();
            yield return ApplyTextSize(false);
        }

        /// <summary>
        /// Somebody who came to the player, in a house of eight and of sixteen at both text sizes: the
        /// card in the hero's place with its eyebrow, its heading, what they said and its answers under
        /// the grid name they have always had, the play moved onto the strip, and the board fits.
        /// </summary>
        [UnityTest]
        public IEnumerator FreeTimeBoard_SomebodyWhoCameToYouLeadsAndFitsInHousesOfEightAndSixteen()
        {
            foreach (int house in new[] { 8, 16 })
            {
                yield return InstallFreeTime(house, FreeTimeWaiting.Reply);
                yield return AtBothTextSizes(larger =>
                {
                    string where = "Free time with somebody come to the player in a house of " + house + (larger ? " at the larger text" : "");
                    AssertFreeTimeBoard(where);
                    var state = director.Snapshot;
                    var card = state.replyCards.Single();
                    var reply = ActiveRect(EpisodeHud.FreeTimeReplyName);
                    Assert.That(reply != null && reply.IsChildOf(ActiveRect(EpisodeHud.FreeTimeHeroName)), Is.True, where + ": who came is the hero.");
                    string said = Words(reply);
                    Assert.That(said, Does.Contain(EpisodeHud.ReplyCardEyebrow).And.Contain(ReplyCards.Title(state, card)).And.Contain(ReplyCards.Message(state, card)),
                        where + ": its eyebrow, its heading and what they said.");
                    var answers = ActiveRect(EpisodeHud.EventChoicesName);
                    Assert.That(answers != null && answers.IsChildOf(reply), Is.True, where + ": its answers are on it,");
                    foreach (var answer in ReplyCards.Replies(card.kind))
                        Assert.That(ButtonWithCaption(EpisodeHud.ReplyCaption(answer.Label)).transform.IsChildOf(answers), Is.True, answer.Label);
                    Assert.That(ActiveRect(EpisodeHud.CurrentPlayCardName).IsChildOf(ActiveRect(EpisodeHud.StoryStripName)), Is.True,
                        where + ": the play moves onto the strip.");
                });
            }
        }

        /// <summary>
        /// A Have-Not, in a house of eight and of sixteen at both text sizes: the Have-Not line, word
        /// for word, under the story strip, and the board still fits with the line's row taken from it.
        /// </summary>
        [UnityTest]
        public IEnumerator FreeTimeBoard_AHaveNotsLineFitsInHousesOfEightAndSixteen()
        {
            foreach (int house in new[] { 8, 16 })
            {
                yield return InstallFreeTime(house, FreeTimeWaiting.HaveNot);
                yield return AtBothTextSizes(larger =>
                {
                    string where = "A Have-Not's free time in a house of " + house + (larger ? " at the larger text" : "");
                    AssertFreeTimeBoard(where);
                    var line = ActiveRect(EpisodeHud.FreeTimeHaveNotName);
                    Assert.That(line, Is.Not.Null, where + " says what being a Have-Not costs.");
                    Assert.That(line.GetComponent<TMP_Text>().text, Is.EqualTo(EpisodeDirector.HaveNotLine), "word for word,");
                    Assert.That(ScreenRect(line).yMax, Is.LessThanOrEqualTo(ScreenRect(ActiveRect(EpisodeHud.StoryStripName)).yMin + .5f), "under the story strip,");
                    Assert.That(ScreenRect(line).yMin, Is.GreaterThanOrEqualTo(ScreenRect(ActiveRect(EpisodeHud.HouseCardsName)).yMax - .5f), "over the cards.");
                });
            }
        }

        /// <summary>
        /// A house of sixteen pages its cards at both text sizes - ten to a page at the resting size,
        /// eight at the larger, fewer where the frame is narrower - and every houseguest is on exactly
        /// one page; paging commits nothing and every page fits.
        /// </summary>
        [UnityTest]
        public IEnumerator FreeTimeBoard_TheCardsPageToEverybodyInAHouseOfSixteen()
        {
            yield return InstallFreeTime(16, FreeTimeWaiting.Nothing);
            foreach (bool larger in new[] { false, true })
            {
                if (larger)
                {
                    director.ClosePanels();
                    yield return ApplyTextSize(true);
                }
                yield return OpenStation();
                yield return null;
                var state = director.Snapshot;
                string where = "A house of sixteen's free time" + (larger ? " at the larger text" : "");
                var everybody = state.Active.Where(actor => !actor.isPlayer).Select(actor => "Houseguest · " + actor.name).ToList();
                var first = FreeTimeCards();
                Assert.That(first.Length, Is.GreaterThan(0).And.LessThan(everybody.Count), where + " runs past one page.");
                Assert.That(first.Length, Is.LessThanOrEqualTo(larger ? 8 : 10), where + ": no more than " + (larger ? 8 : 10) + " to a page.");
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.CampaignPreviousCaption), Is.Null, where + ": the first page has no way back.");
                var seen = new List<string>(first);
                for (int page = 0; page < 8 && ButtonWithCaptionOrNull(EpisodeHud.CampaignNextCaption) != null; page++)
                {
                    int before = director.Snapshot.revision;
                    ButtonWithCaption(EpisodeHud.CampaignNextCaption).onClick.Invoke();
                    Assert.That(director.Snapshot.revision, Is.EqualTo(before), "A page is view state: it commits nothing.");
                    yield return null;
                    AssertFreeTimeBoard(where + ", page " + (page + 2));
                    Assert.That(ButtonWithCaptionOrNull(EpisodeHud.CampaignPreviousCaption), Is.Not.Null, where + ": a later page has a way back.");
                    seen.AddRange(FreeTimeCards());
                }
                Assert.That(seen, Is.Unique, where + ": no card is on two pages,");
                Assert.That(seen, Is.EquivalentTo(everybody), where + ": and everybody is on one.");
                ButtonWithCaption(EpisodeHud.CampaignPreviousCaption).onClick.Invoke();
                yield return null;
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.CampaignNextCaption), Is.Not.Null, where + ": back a page, there is a way on again.");
            }
            director.ClosePanels();
            yield return ApplyTextSize(false);
        }

        /// <summary>
        /// A beat opens as the step from the banner, and from the strip's chip: its options beside its
        /// words, "Back to free time" in the footer's secondary slot, the way on still pinned once,
        /// and no scroll. Back is back, with nothing committed; answering on the step commits once and
        /// hands the board back with the other beat as its banner.
        /// </summary>
        [UnityTest]
        public IEnumerator FreeTimeBoard_ABeatOpensAsTheStepAndGoesBack()
        {
            yield return InstallFreeTime(8, FreeTimeWaiting.Beat, 2);
            var beats = EpisodeEngine.OpenStoryBeats(director.Snapshot);
            yield return OpenStation();
            yield return null;
            // Read once the panel is open: opening it flushes the house's ticks.
            int revision = director.Snapshot.revision;

            ButtonWithCaption(EpisodeDirector.AnswerBeatCaption).onClick.Invoke();
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Opening the beat commits nothing.");
            yield return null;
            Assert.That(director.FreeTimeBeatOpen, Is.EqualTo(beats[0].id), "The banner's beat is the step.");
            AssertOnTheStrategyStage(EpisodeDirector.BeginNextCompetitionCaption, "The beat opened as the step", EpisodeDirector.BackToFreeTimeCaption);
            Assert.That(ActiveRect(EpisodeHud.FreeTimeBoardName), Is.Null, "The step has the stage to itself.");
            var choices = ActiveRect(EpisodeHud.StoryChoicesName);
            Assert.That(choices, Is.Not.Null, "Its options are on the screen,");
            foreach (var choice in beats[0].choices)
                Assert.That(ButtonWithCaption(EpisodeHud.EventChoiceCaption(choice.label)).transform.IsChildOf(choices), Is.True, choice.label);
            var content = ActiveRect("Episode content");
            Assert.That(content.rect.height, Is.LessThanOrEqualTo(((RectTransform)content.parent).rect.height + .5f), "and the step holds without a scroll.");
            Assert.That(LiveButtonsCarrying(EpisodeDirector.BeginNextCompetitionCaption), Is.EqualTo(1), "The way on is still one control.");
            AssertEveryLabelDraws(ActiveRect("Episode panel"), "The beat as the step");

            ButtonWithCaption(EpisodeDirector.BackToFreeTimeCaption).onClick.Invoke();
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Going back commits nothing.");
            yield return null;
            Assert.That(director.FreeTimeBeatOpen, Is.Null);
            AssertFreeTimeBoard("Back on the board");
            Assert.That(ActiveRect(EpisodeHud.StoryBannerName), Is.Not.Null, "The banner is back, its beat still waiting.");

            // The strip's chip opens the other beat the same way.
            ButtonWithCaption(beats[1].title).onClick.Invoke();
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Opening it from its chip commits nothing.");
            yield return null;
            Assert.That(director.FreeTimeBeatOpen, Is.EqualTo(beats[1].id), "The chip's beat is the step.");
            revision = director.Snapshot.revision;
            ButtonWithCaption(EpisodeHud.EventChoiceCaption(beats[1].choices[0].label)).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(revision + 1), "Answering on the step commits it once.");
            Assert.That(after.houseEvents.Single(item => item.id == beats[1].id).resolved, Is.True);
            Assert.That(after.houseEvents.Single(item => item.id == beats[0].id).resolved, Is.False, "and nothing else.");
            if (!director.IsPhasePanelOpen) yield return OpenStation();
            yield return null;
            AssertFreeTimeBoard("After the answer");
            Assert.That(director.FreeTimeBeatOpen, Is.Null, "The answered beat is no longer the step.");
            Assert.That(Words(ActiveRect(EpisodeHud.StoryBannerName)), Does.Contain(beats[0].title), "The one left is the banner.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// Move-in night, as every new season reaches it: the first night's one real conversation is
        /// the banner, said to be free, and the rule says the night has one action; answering it on
        /// the step - who with, then the confirm of the pick - spends nothing.
        /// </summary>
        [UnityTest]
        public IEnumerator FreeTimeBoard_MoveInNightsConversationIsFreeAndItsRuleSaysSo()
        {
            HoldTheHouseForTheFixture();
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 5u);
            EpisodeEngine.EnableStory(state);
            EpisodeEngine.EnableWeek(state);
            var engine = new EpisodeEngine(state);
            foreach (var beat in OpeningBeat.InOrder)
            {
                var now = engine.Snapshot;
                var result = engine.Apply(new EpisodeCommand
                {
                    id = "opening-" + beat, actorId = now.playerId, expectedRevision = now.revision, expectedPhase = now.phase,
                    kind = EpisodeCommandKind.MarkOpeningBeat, targetId = beat,
                });
                Assert.That(result.accepted, Is.True, beat + ": " + result.reason);
            }
            var fixture = engine.Snapshot;
            var night = EpisodeEngine.OpenStoryBeats(fixture).SingleOrDefault(item => StoryText.ArcOf(item)?.id == "first-night");
            Assert.That(night, Is.Not.Null, "The meet-and-greet over, the first night's conversation is waiting.");
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            yield return OpenStation();
            yield return null;

            AssertFreeTimeBoard("Move-in night");
            var banner = ActiveRect(EpisodeHud.StoryBannerName);
            Assert.That(banner, Is.Not.Null, "The first night's conversation is the banner,");
            Assert.That(Words(banner), Does.Contain(StoryText.Title(night)), "by its name,");
            Assert.That(banner.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "Free chip"), Is.True, "said to be free,");
            Assert.That(banner.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Banner note").text, Does.StartWith("Answering spends no action."));
            Assert.That(BudgetLabel(EpisodeHud.BudgetRuleName).text, Is.EqualTo("1 action tonight; it does not carry into the week."), "and the night has one action.");
            Assert.That(BudgetLabel(EpisodeHud.ActionsLeftCountName).text, Is.EqualTo("1"));

            ButtonWithCaption(EpisodeDirector.AnswerBeatCaption).onClick.Invoke();
            yield return null;
            var option = night.choices.First(choice => !choice.lapse && !choice.locked);
            ButtonWithCaption(EpisodeHud.EventChoiceCaption(option.label)).onClick.Invoke();
            yield return null;
            if (option.pickPerson)
            {
                // Who with, on the step's own grid of faces.
                var person = director.Snapshot.Find(option.eligibleIds.First());
                ButtonWithCaption(person.name).onClick.Invoke();
                yield return null;
            }
            yield return null;
            var after = director.Snapshot;
            Assert.That(after.houseEvents.Single(item => item.id == night.id).resolved, Is.True, "The conversation is had,");
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.Zero, "and it spent nothing.");
            if (!director.IsPhasePanelOpen) yield return OpenStation();
            yield return null;
            Assert.That(BudgetLabel(EpisodeHud.ActionsLeftCountName).text, Is.EqualTo("1"), "The night's one action is still there.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The moves cost what they say: walking the house and the activities are free, listening in
        /// and the meetings an action each. Spending move-in night's one action locks those three with
        /// the reason where their cost was, and leaves the free ones pressable.
        /// </summary>
        [UnityTest]
        public IEnumerator FreeTimeBoard_CostedMovesLockWhenNoActionIsLeft()
        {
            yield return InstallFreeTime(8, FreeTimeWaiting.Nothing);
            yield return OpenStation();
            yield return null;
            var costed = new[] { EpisodeDirector.OverviewListenCaption, EpisodeHud.RallyHouseCaption, EpisodeHud.AirLaundryCaption };
            var free = new[] { EpisodeDirector.ExploreTheHouseCaption, EpisodeDirector.DoAnActivityCaption };
            string Foot(Button tile) => tile.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Tile foot").text;
            foreach (var caption in costed) Assert.That(Foot(ButtonWithCaption(caption)), Is.EqualTo(EpisodeDirector.TileCostsAction), caption + " costs an action.");
            foreach (var caption in free) Assert.That(Foot(ButtonWithCaption(caption)), Is.EqualTo(EpisodeDirector.TileFree), caption + " is free.");
            var before = director.Snapshot;
            Assert.That(EpisodeEngine.SocialActionBudget(before) - EpisodeEngine.SocialActionsSpent(before), Is.EqualTo(1), "Move-in night has one action.");

            ButtonWithCaption(EpisodeDirector.OverviewListenCaption).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1), "Listening in commits once,");
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(1), "and spends the night's action.");
            if (!director.IsPhasePanelOpen) yield return OpenStation();
            yield return null;
            AssertFreeTimeBoard("Free time with no action left");
            Assert.That(BudgetLabel(EpisodeHud.ActionsLeftCountName).text, Is.EqualTo("0"), "Nothing is left,");
            Assert.That(BudgetLabel(EpisodeHud.UnusedActionsNoteName), Is.Null, "so nothing will be lost.");
            foreach (var caption in costed)
            {
                var tile = FindButton(caption);
                Assert.That(tile.IsInteractable(), Is.False, caption + " is locked,");
                Assert.That(Foot(tile), Is.EqualTo(EpisodeDirector.TileNoActionsLeft), "with the reason where its cost was.");
            }
            foreach (var caption in free) Assert.That(ButtonWithCaption(caption).IsInteractable(), Is.True, caption + " is still free to press.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The board's new controls do what they say and commit nothing: "Stay in the house" closes
        /// the screen as Escape does, "Explore the house" closes it onto the house's map, and "Do an
        /// activity" opens the house activities.
        /// </summary>
        [UnityTest]
        public IEnumerator FreeTimeBoard_ItsNewControlsCloseOntoTheHouseAndCommitNothing()
        {
            yield return InstallFreeTime(8, FreeTimeWaiting.Nothing);

            // Each press is measured across itself: opening a panel flushes the house's ticks.
            yield return OpenStation();
            yield return null;
            int revision = director.Snapshot.revision;
            ButtonWithCaption(EpisodeDirector.StayInTheHouseCaption).onClick.Invoke();
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Stay in the house commits nothing.");
            yield return null;
            Assert.That(director.IsPanelOpen, Is.False, "Stay in the house closes the screen,");
            Assert.That(player.InputEnabled, Is.True, "and the house is the player's again.");

            yield return OpenStation();
            yield return null;
            revision = director.Snapshot.revision;
            ButtonWithCaption(EpisodeDirector.ExploreTheHouseCaption).onClick.Invoke();
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Exploring commits nothing.");
            yield return null;
            Assert.That(director.IsPanelOpen, Is.False, "Explore the house closes the screen");
            Assert.That(director.IsOverview && !director.IsBriefing, Is.True, "onto the house's map, with no briefing over it.");
            director.EndOverview();
            yield return null;

            yield return OpenStation();
            yield return null;
            revision = director.Snapshot.revision;
            ButtonWithCaption(EpisodeDirector.DoAnActivityCaption).onClick.Invoke();
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Opening the activities commits nothing.");
            yield return null;
            Assert.That(director.IsHouseActivityOpen, Is.True, "Do an activity opens the house activities.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The keyboard walks the board: with somebody come to the player it opens on their answers,
        /// as the panel always has, and never on a way to buy time; Tab visits every control once and
        /// nothing outside the panel; and with nothing waiting it opens on a houseguest's card.
        /// </summary>
        [UnityTest]
        public IEnumerator FreeTimeBoard_TheKeyboardWalksTheBoard()
        {
            yield return InstallFreeTime(8, FreeTimeWaiting.Reply);
            yield return OpenStation();
            yield return null; yield return null;
            var opened = EventSystem.current.currentSelectedGameObject;
            Assert.That(opened, Is.Not.Null, "The board opens with the keyboard somewhere.");
            var answers = ActiveRect(EpisodeHud.EventChoicesName);
            Assert.That(answers != null && opened.transform.IsChildOf(answers), Is.True,
                "It opens on the answers to whoever came to the player: " + opened.name + ".");
            Assert.That(LiveButtonsCarrying(EpisodeDirector.BeginNextCompetitionCaption), Is.EqualTo(1), "The way on is one control.");
            yield return AssertKeyboardRing("free time's board", ModalRoot);

            ButtonWithCaption(EpisodeHud.ReplyCaption("Apologize")).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.Snapshot.replyCards, Is.Empty, "Answered, the card is gone.");
            director.ClosePanels();
            yield return null;
            yield return OpenStation();
            yield return null; yield return null;
            opened = EventSystem.current.currentSelectedGameObject;
            Assert.That(opened != null && opened.transform.IsChildOf(ActiveRect(EpisodeHud.HouseCardsName)), Is.True,
                "With nothing waiting it opens on a houseguest's card: " + (opened != null ? opened.name : "nothing") + ".");
            Assert.That(opened.transform.IsChildOf(ActiveRect(EpisodeHud.ScreenHeadName)), Is.False, "never on a way to buy time.");
            director.ClosePanels();
            yield return null;
        }
    }
}
