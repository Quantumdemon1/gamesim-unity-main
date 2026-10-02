using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
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
    /// UI-UX-PASS-PLAN T0, rows and readings: a decision row's trust reading stands clear of its
    /// chevron and says whose reading it is; a fixed button stands on the kit's ground and dims
    /// its edge with its glass; the diary's ballot keeps its decision in the window at scroll 0
    /// with the context following; the week's recap clamps to the screen at the larger text with
    /// its title and its way on in view; the cast screen's house-size block reads at thirteen or
    /// more and every trait pill stands inside its card; and the veto's draw of sixteen holds on
    /// the 16:9 frame as it does on the batch canvas.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// Every trust reading on the open panel: named as the player's own, in a box 1.3 times its
        /// type, drawn, inside its row, and clear of the row's chevron, its ALLY tag and its caption.
        /// With <paramref name="expectSome"/>, at least one row carries one.
        /// </summary>
        private void AssertReadingsClearOfChevrons(string where, bool expectSome)
        {
            Canvas.ForceUpdateCanvases();
            var panel = LastActive("Episode panel");
            Assert.That(panel, Is.Not.Null, where + ": the panel is up.");
            var readings = panel.GetComponentsInChildren<TMP_Text>().Where(label => label.name == EpisodeHud.TrustReadingName).ToList();
            if (expectSome) Assert.That(readings, Is.Not.Empty, where + ": no row carries a trust reading.");
            foreach (var reading in readings)
            {
                var row = (RectTransform)reading.transform.parent;
                string said = where + ": '" + reading.text + "' on '" + row.name + "'";
                Assert.That(reading.text, Does.StartWith("Your trust "), said + " says whose reading it is.");
                reading.ForceMeshUpdate(true);
                Assert.That(reading.isTextOverflowing, Is.False, said + " fits its box at " + reading.fontSize.ToString("0.#") + ".");
                Assert.That(reading.rectTransform.rect.height, Is.GreaterThanOrEqualTo(reading.fontSize * 1.3f - .5f),
                    said + " has a box " + reading.rectTransform.rect.height.ToString("0.0") + " high for a " + reading.fontSize.ToString("0.#") + " font.");
                Assert.That(reading.textInfo.characterInfo.Take(reading.textInfo.characterCount).Any(glyph => glyph.isVisible), Is.True, said + " draws nothing.");
                var box = OnTheHud(reading.rectTransform);
                AssertWithin(OnTheHud(row), box, said, "its row");
                var chevron = row.Find("Chevron") as RectTransform;
                if (chevron != null)
                    Assert.That(box.Overlaps(OnTheHud(chevron)), Is.False,
                        said + " is drawn over by the row's chevron: " + box + " against " + OnTheHud(chevron) + ".");
                foreach (var tag in row.GetComponentsInChildren<TMP_Text>().Where(label => label != reading && label.transform.parent == row && label.text == "ALLY"))
                    Assert.That(box.Overlaps(OnTheHud(tag.rectTransform)), Is.False, said + " meets its ALLY tag.");
                var caption = row.GetComponentsInChildren<TMP_Text>()
                    .FirstOrDefault(label => label != reading && label.transform.parent == row && label.text == Localisation.Text(row.name));
                if (caption != null) Assert.That(box.Overlaps(OnTheHud(caption.rectTransform)), Is.False, said + " runs under the caption.");
            }
        }

        /// <summary>
        /// A fixed button stands on the kit's secondary ground, as the strategy footer's secondary
        /// slot does: a drawn image with the kit's sprite under its words, the kit's edge, and no
        /// hover step of its own.
        /// </summary>
        private static void AssertKitGround(Button button, string where)
        {
            var rect = (RectTransform)button.transform;
            var ground = rect.GetComponent<Image>();
            Assert.That(ground != null && ground.sprite != null && ground.color.a > 0f, Is.True,
                where + " stands on a ground: an image with the kit's sprite, drawn.");
            Assert.That(ground.color, Is.Not.EqualTo(UiTheme.Surface), where + " is the Surface panel it was, which vanished on the stage.");
            var edge = rect.Find("Border");
            var edgeImage = edge != null ? edge.GetComponent<Image>() : null;
            Assert.That(edgeImage != null && edgeImage.color.a > 0f, Is.True, where + " has the kit's edge.");
            Assert.That(rect.GetComponent<HudEmphasis>(), Is.Null, where + " takes no hover step: it is the secondary slot's chrome.");
            AssertEveryLabelDraws(rect, where);
        }

        /// <summary>
        /// The diary's ballot by the rule the review settled (UI-UX-PASS-PLAN T0): the decision fits
        /// at scroll 0; the context follows. At the column's top - the panel opens there - the
        /// heading, the line and every nominee card stand whole in the scroll's window, each card at
        /// or above its floor and drawn. While a choice waits, Confirm and its way back are a pinned
        /// pair under the scroll, inside the panel, without meeting, and nothing scrolls. Before
        /// one, the voters' row follows the cards and the record and the reflections follow it, a
        /// scroll away. Measured in the HUD's own units, so it holds on either frame.
        /// </summary>
        private void AssertTheDecisionFitsAtScrollZero(string where, bool reviewing, bool larger)
        {
            Canvas.ForceUpdateCanvases();
            var panel = LastActive("Episode panel");
            var content = LastActive("Episode content");
            Assert.That(panel != null && content != null, Is.True, where + ": the vote's panel is up.");
            var viewport = (RectTransform)content.parent;
            float window = viewport.rect.height;
            Assert.That(content.anchoredPosition.y, Is.LessThanOrEqualTo(.5f), where + " opens at the top of its column.");
            // In the column's own units the top is 0, down is negative, and the window at scroll 0
            // shows 0 down to -window.
            void InTheWindow(RectTransform part, string what)
            {
                Assert.That(part, Is.Not.Null, where + " has " + what + ".");
                var at = LocalBounds(content, part);
                Assert.That(at.yMax <= .5f && at.yMin >= -window - .5f, Is.True,
                    where + ": " + what + " stands at " + at.yMax.ToString("0") + " to " + at.yMin.ToString("0")
                    + ", out of the window's 0 to " + (-window).ToString("0") + " at scroll 0.");
            }
            RectTransform Part(string name) => content.GetComponentsInChildren<RectTransform>().LastOrDefault(rect => rect.name == name);
            InTheWindow(Part(EpisodeHud.BallotHeadingName), "the heading");
            InTheWindow(Part(EpisodeHud.BallotLineName), "the line");
            var row = Part(EpisodeHud.BallotRowName);
            InTheWindow(row, "the cards' row");
            float floor = EpisodeHud.BallotCardHeight * EpisodeHud.BallotCardFloor * (larger ? 1.2f : 1f);
            var cards = row.GetComponentsInChildren<Button>().Where(button => button.transform.parent == row).ToList();
            Assert.That(cards.Count, Is.GreaterThanOrEqualTo(2), where + ": a card per nominee.");
            foreach (var card in cards)
            {
                var rect = (RectTransform)card.transform;
                InTheWindow(rect, "'" + card.name + "'");
                Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(floor - .5f),
                    where + ": '" + card.name + "' is " + rect.rect.height.ToString("0") + " tall, under its floor of " + floor.ToString("0") + ".");
                AssertEveryLabelDraws(rect, where + "'s '" + card.name + "'");
            }
            if (reviewing)
            {
                var pair = LastActive(EpisodeHud.PinnedPairName);
                Assert.That(pair, Is.Not.Null, where + ": Confirm and the way back are a pinned pair.");
                Assert.That(pair.parent, Is.SameAs(panel), where + ": the pair is pinned to the panel, not in its scroll.");
                var stage = OnTheHud(panel);
                var confirm = (RectTransform)FindButton(EpisodeHud.DiaryConfirmCaption).transform;
                var back = (RectTransform)FindButton(EpisodeHud.DiaryCancelCaption).transform;
                foreach (var button in new[] { confirm, back })
                {
                    Assert.That(button.parent, Is.SameAs(pair), "'" + button.name + "' is one of the pair.");
                    AssertWithin(stage, OnTheHud(button), where + "'s '" + button.name + "'", "the panel");
                    AssertEveryLabelDraws(button, where + "'s '" + button.name + "'");
                }
                Assert.That(OnTheHud(confirm).Overlaps(OnTheHud(back)), Is.False, where + ": the pair share the row without meeting.");
                float pairTop = Mathf.Max(OnTheHud(confirm).yMax, OnTheHud(back).yMax);
                Assert.That(OnTheHud(viewport).yMin, Is.GreaterThanOrEqualTo(pairTop - .5f), where + ": the scroll runs under the pair.");
                Assert.That(content.rect.height, Is.LessThanOrEqualTo(window + .5f),
                    where + " scrolls: " + content.rect.height.ToString("0") + " in " + window.ToString("0") + ".");
                return;
            }
            // The context follows: the voters' row under the cards, the record under the row, the
            // reflections under the record.
            var voters = Part(EpisodeDirector.VotersRowName);
            Assert.That(voters, Is.Not.Null, where + ": the voters are a row of chips.");
            Assert.That(LocalBounds(content, voters).yMax, Is.LessThanOrEqualTo(LocalBounds(content, row).yMin + .5f), where + ": the voters' row follows the cards.");
            TMP_Text Section(string words) => content.GetComponentsInChildren<TMP_Text>().LastOrDefault(label => label.text == words);
            var record = Section("YOUR DIARY RECORD");
            var reflections = Section("YOUR PRIVATE REFLECTIONS");
            Assert.That(record != null && reflections != null, Is.True, where + ": the record and the reflections stay under the vote.");
            Assert.That(LocalBounds(content, record.rectTransform).yMax, Is.LessThanOrEqualTo(LocalBounds(content, voters).yMin + .5f),
                where + ": the record follows the voters.");
            Assert.That(LocalBounds(content, reflections.rectTransform).yMax, Is.LessThanOrEqualTo(LocalBounds(content, record.rectTransform).yMin + .5f),
                where + ": the reflections follow the record.");
        }

        /// <summary>Eviction night in a house of <paramref name="houseguests"/>: the player one of its voters, the speeches given, nobody's ballot in.</summary>
        private IEnumerator InstallBallotHouse(int houseguests)
        {
            HoldTheHouseForTheFixture();
            var state = FullHouse(4401, houseguests);
            AtEviction(state);
            foreach (var speaker in state.nominees)
                state.evictionSpeeches.Add(new EvictionSpeechState
                {
                    speakerId = speaker, week = state.week, isPlayerAuthored = false, text = "I'd like to stay.",
                });
            state.evictionStage = EvictionStage.Voting;
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            yield return null;
        }

        /// <summary>
        /// The recap on its own screen: the card inside it, the title in the head over the scroll
        /// and drawn, the way on under the scroll and inside the screen, drawn.
        /// </summary>
        private void AssertRecapOnItsScreen(WeeklyRecapScreen screen, string where)
        {
            Canvas.ForceUpdateCanvases();
            var root = (RectTransform)screen.transform;
            var card = LastActive("Recap card");
            Assert.That(card, Is.Not.Null, where + " has its card.");
            AssertWithin(root.rect, LocalBounds(root, card), where + "'s card", "the screen");
            var head = LastActive(WeeklyRecapScreen.HeadName);
            Assert.That(head, Is.Not.Null, where + " has its head.");
            var title = head.GetComponentsInChildren<TMP_Text>().Single(label => label.name == WeeklyRecapScreen.TitleName);
            Assert.That(title.rectTransform.rect.height, Is.GreaterThanOrEqualTo(title.fontSize * 1.3f - .5f), where + "'s title has a box Inter draws in.");
            // The card's own window: the HUD's panel has a Viewport too.
            var viewport = card.Find("Viewport") as RectTransform;
            Assert.That(viewport, Is.Not.Null, where + "'s card has its scroll.");
            AssertWithin(root.rect, LocalBounds(root, title.rectTransform), where + "'s title", "the screen");
            Assert.That(LocalBounds(root, title.rectTransform).yMin, Is.GreaterThanOrEqualTo(LocalBounds(root, viewport).yMax - .5f),
                where + ": the title stands over the scroll, not in it.");
            AssertEveryLabelDraws(head, where + "'s head");
            var onward = (RectTransform)LiveButton(WeeklyRecapScreen.ContinueCaption).transform;
            AssertWithin(root.rect, LocalBounds(root, onward), where + "'s way on", "the screen");
            Assert.That(LocalBounds(root, onward).yMax, Is.LessThanOrEqualTo(LocalBounds(root, viewport).yMin + .5f),
                where + ": the way on stands under the scroll, not in it.");
            AssertEveryLabelDraws(onward, where + "'s way on");
        }

        /// <summary>
        /// A fixed button reads as a button wherever it stands: the compact objective's two travel
        /// rows, the overview's way back to its briefing, the picker's door to the backdoor plan and
        /// the panel's Close each stand on the kit's ground with its edge. One that cannot be pressed
        /// dims its edge with its glass, and puts it back when it can.
        /// </summary>
        [UnityTest]
        public IEnumerator RowsAndReadings_FixedButtonsStandOnTheKitsGround()
        {
            HoldTheHouseForTheFixture();
            director.OpenSettings();
            yield return null;
            ButtonWithCaption("Use compact HUD").onClick.Invoke();
            Assert.That(director.CompactHud, Is.True);
            director.ClosePanels();
            yield return null;
            Canvas.ForceUpdateCanvases();
            AssertKitGround(ButtonWithCaption("Go to episode screen"), "The compact objective's 'Go to episode screen'");
            var diary = ButtonWithCaption(EpisodeHud.DiaryTravelCaption);
            AssertKitGround(diary, "The compact objective's '" + EpisodeHud.DiaryTravelCaption + "'");
            var edge = diary.transform.Find("Border").GetComponent<Image>();
            var resting = edge.color;
            diary.interactable = false;
            yield return null;
            Assert.That(edge.color.a, Is.EqualTo(resting.a * diary.colors.disabledColor.a).Within(.01f),
                "A fixed button that cannot be pressed dims its edge with its glass.");
            diary.interactable = true;
            yield return null;
            Assert.That(edge.color, Is.EqualTo(resting), "and puts it back when it can.");
            director.OpenSettings();
            yield return null;
            ButtonWithCaption("Show full HUD").onClick.Invoke();
            Assert.That(director.CompactHud, Is.False);
            director.ClosePanels();
            yield return null;

            yield return SettleCast();
            Assert.That(director.ToggleOverview(), Is.True);
            yield return null; yield return null;
            ButtonWithCaption(EpisodeDirector.ShowMapCaption).onClick.Invoke();
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            AssertKitGround(ButtonWithCaption(EpisodeDirector.ShowBriefingCaption), "The overview's '" + EpisodeDirector.ShowBriefingCaption + "'");
            director.EndOverview();
            yield return null;

            yield return InstallNomination(8, true, NominationBlock.Open);
            yield return OpenStation();
            yield return null;
            Canvas.ForceUpdateCanvases();
            AssertKitGround(ButtonWithCaption(EpisodeHud.PlanBackdoorCaption), "The picker's '" + EpisodeHud.PlanBackdoorCaption + "'");
            AssertKitGround(ButtonWithCaption("Close  [Esc]"), "The panel's 'Close  [Esc]'");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The eviction vote in the diary, in a house of six and in the largest house, at both text
        /// sizes and on both frames: the decision fits at scroll 0 and the context follows
        /// (<see cref="AssertTheDecisionFitsAtScrollZero"/>); the voters are a row of chips, one a
        /// voter who is not the player, each the control it was; then, with a choice waiting, the
        /// cards on one screen with Confirm and the way back pinned under them, and the bar saying
        /// what a draft is. Nothing it does writes to the season.
        /// </summary>
        [UnityTest]
        public IEnumerator RowsAndReadings_TheBallotsDecisionFitsAtScrollZeroAndItsContextFollows()
        {
            foreach (int houseguests in new[] { 6, 16 })
            {
                if (houseguests == 16) yield return InstallBallotHouse(houseguests);
                else yield return InstallDiaryFixture(state => state.phase == EpisodePhase.Eviction && !state.evictionResolved
                    && state.evictionStage == EvictionStage.Voting
                    && EpisodeEngine.Voters(state).Any(voter => voter.isPlayer)
                    && !state.votes.Any(vote => vote.voterId == state.playerId), "eligible private eviction ballot");
                var before = director.Snapshot;
                // The house's size, not who is left in it: the six-house fixture votes in week two.
                Assert.That(before.contestants.Count, Is.EqualTo(houseguests), "A house of " + houseguests + ".");
                string caption = "Vote to evict " + before.Find(before.nominees[0]).name;
                var voters = EpisodeEngine.Voters(before).Where(voter => !voter.isPlayer).ToList();
                foreach (bool larger in new[] { false, true })
                {
                    yield return ApplyTextSize(larger);
                    yield return OpenDiaryFixturePanel();
                    string where = "The ballot in a house of " + houseguests + (larger ? " at the larger text" : "");
                    var roster = LastActive(EpisodeDirector.VotersRowName);
                    Assert.That(roster, Is.Not.Null, where + ": the voters are a row of chips.");
                    Assert.That(roster.GetComponentsInChildren<Button>().Select(chip => chip.name),
                        Is.EquivalentTo(voters.Select(voter => "Thoughts · " + voter.name)), where + ": a chip a voter who is not the player.");
                    AssertEveryLabelDraws(roster, where + "'s voters");
                    yield return AtBothFrames(frame => AssertTheDecisionFitsAtScrollZero(where + " on the " + frame + " frame", false, larger));

                    ButtonWithCaption(caption).onClick.Invoke();
                    yield return null; yield return null;
                    Assert.That(director.HasDiaryDecisionDraft, Is.True, where + ": the choice waits.");
                    yield return AtBothFrames(frame => AssertTheDecisionFitsAtScrollZero(where + " with a choice waiting on the " + frame + " frame", true, larger));
                    Assert.That(ActiveDiaryText(), Does.Contain(EpisodeDirector.DraftBarLine), where + ": the bar says what a draft is.");
                    AssertEquivalent(before, director.Snapshot);
                    ButtonWithCaption(EpisodeHud.DiaryCancelCaption).onClick.Invoke();
                    yield return null; yield return null;
                    Assert.That(director.HasDiaryDecisionDraft, Is.False, where + ": the way back discards the choice.");
                    AssertEquivalent(before, director.Snapshot);
                    director.ClosePanels();
                    yield return null;
                }
                yield return ApplyTextSize(false);
            }
        }

        /// <summary>
        /// The week's recap at both text sizes, on the batch canvas and on both capture frames: the
        /// card inside the screen, the title in a head over the scroll and the way on under it,
        /// both in view and drawn. At the larger text the whole card used to stand past the screen,
        /// sized to the frame before the scaler caught up (weekly-recap-overview-large) - so once
        /// more with the size set and the week shown in the same frame.
        /// </summary>
        [UnityTest]
        public IEnumerator RowsAndReadings_TheRecapClampsToTheScreenAtBothTextSizesAndFrames()
        {
            var screen = director.GetComponentInChildren<WeeklyRecapScreen>(true);
            var state = FirstWeekClosed();
            foreach (bool larger in new[] { false, true })
            {
                screen.FontScale = larger ? 1.2f : 1f;
                yield return null;
                screen.Show(state, () => { });
                yield return Frames(3);
                string where = "The recap" + (larger ? " at the larger text" : "");
                AssertRecapOnItsScreen(screen, where);
                yield return AtBothFrames(frame => AssertRecapOnItsScreen(screen, where + " on the " + frame + " frame"));
                yield return Frames(2);
                AssertRecapOnItsScreen(screen, where + " back on the batch canvas");
                screen.Hide();
                yield return null;
            }
            screen.FontScale = 1f;
            yield return Frames(2);
            // The same frame: the larger text asked for and the week shown before the canvas has
            // taken its new shape, as the settings and a week's close can land together.
            screen.FontScale = 1.2f;
            screen.Show(state, () => { });
            yield return Frames(5);
            AssertRecapOnItsScreen(screen, "The recap shown in the frame the larger text was asked for");
            screen.Hide();
            screen.FontScale = 1f;
            yield return null;
        }

        /// <summary>
        /// The cast screen's house-size block reads, on the 16:9 frame and the 4:3 at both text
        /// sizes: its heading, its range line, the count, the two chips' words and the footnote all
        /// set at thirteen or more in boxes 1.3 times their type, fitting; the chips tall enough to
        /// press; and every trait pill on the cards inside its card, its word whole on one line in a
        /// box 1.3 times its type - a pair of long words ran past Taylor Kim's card at 22 tall.
        /// </summary>
        [UnityTest]
        public IEnumerator RowsAndReadings_TheHouseSizeBlockReadsAtThirteenOrMore()
        {
            yield return OpenCastScreen();
            Canvas.ForceUpdateCanvases();
            yield return null;
            var screen = CastScreen();
            Assert.That(screen != null && screen.IsShowing, Is.True, "The cast screen is up.");
            var emma = CastTemplates.Find("emma-brown");
            CastButtons(emma.Name)[0].onClick.Invoke();
            yield return null;
            var canvas = screen.GetComponent<Canvas>();
            try
            {
                foreach (var frame in new[]
                {
                    (wide: 1600, high: 900, text: 1f, shape: "16:9"),
                    (wide: 1600, high: 900, text: 1.2f, shape: "16:9 at the larger text"),
                    (wide: 1024, high: 768, text: 1f, shape: "4:3"),
                    (wide: 1024, high: 768, text: 1.2f, shape: "4:3 at the larger text"),
                })
                {
                    yield return LayCastScreenOutAt(frame.wide, frame.high, frame.text);
                    AssertTheHouseSizeBlockReads(screen, frame.shape);
                    AssertEveryTraitPillInsideItsCard(screen, frame.shape);
                }
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            yield return null;
            screen.Dismiss();
            yield return null;
        }

        private void AssertTheHouseSizeBlockReads(CastSelect screen, string shape)
        {
            Canvas.ForceUpdateCanvases();
            var panel = screen.GetComponentsInChildren<RectTransform>(true)
                .Last(rect => rect.name == CastSelect.HouseSizePanelName && rect.gameObject.activeInHierarchy);
            var labels = panel.GetComponentsInChildren<TMP_Text>().ToList();
            foreach (var caption in new[] { "Fewer houseguests", "More houseguests" })
            {
                var chip = (RectTransform)CastButtons(caption).Last().transform;
                Assert.That(chip.rect.height, Is.GreaterThanOrEqualTo(CastSelect.HouseSizeChipHeight - .5f), "'" + caption + "' is tall enough to press on " + shape + ".");
                labels.AddRange(chip.GetComponentsInChildren<TMP_Text>());
            }
            labels.AddRange(screen.GetComponentsInChildren<TMP_Text>()
                .Where(label => label.isActiveAndEnabled && (label.name == "House size" || label.text.StartsWith("A shorter season"))));
            Assert.That(labels.Count, Is.GreaterThanOrEqualTo(6), "The block's heading, its range, the count, two chips and the footnote on " + shape + ".");
            foreach (var label in labels)
            {
                label.ForceMeshUpdate(true);
                string said = "'" + label.text + "' (" + label.name + ") on " + shape;
                Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(CastSelect.FootnoteSize - .01f), said + " is set under thirteen: " + label.fontSize.ToString("0.#") + ".");
                Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.fontSize * 1.3f - .5f),
                    said + " has a box " + label.rectTransform.rect.height.ToString("0.0") + " high for a " + label.fontSize.ToString("0.#") + " font.");
                Assert.That(label.isTextOverflowing, Is.False, said + " runs out of its box.");
            }
        }

        private void AssertEveryTraitPillInsideItsCard(CastSelect screen, string shape)
        {
            var pills = screen.GetComponentsInChildren<RectTransform>()
                .Where(rect => rect.name == CastSelect.TraitChipName && rect.gameObject.activeInHierarchy).ToList();
            Assert.That(pills, Is.Not.Empty, "The cards carry their trait pills on " + shape + ".");
            foreach (var pill in pills)
            {
                var card = (RectTransform)pill.parent;
                var word = pill.GetComponentInChildren<TMP_Text>();
                word.ForceMeshUpdate(true);
                string said = "'" + word.text + "' on " + card.name + "'s card on " + shape;
                var at = LocalBounds(card, pill);
                Assert.That(at.xMin >= card.rect.xMin - .5f && at.xMax <= card.rect.xMax + .5f, Is.True,
                    said + " runs past the card: " + at.xMin.ToString("0") + " to " + at.xMax.ToString("0") + " on " + card.rect.xMin.ToString("0") + " to " + card.rect.xMax.ToString("0") + ".");
                Assert.That(word.fontSize, Is.GreaterThanOrEqualTo(10f - .01f), said + " is set under ten.");
                Assert.That(pill.rect.height, Is.GreaterThanOrEqualTo(word.fontSize * 1.3f - .5f), said + ": the pill is under 1.3 times its word.");
                Assert.That(word.isTextTruncated || word.isTextOverflowing, Is.False, said + " is cut short.");
                Assert.That(word.textInfo.lineCount, Is.EqualTo(1), said + " stays on one line.");
            }
        }
    }
}
