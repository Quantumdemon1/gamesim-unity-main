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
    /// UI-UX-PASS-PLAN T0, rows and readings: a decision row's trust reading stands clear of its
    /// chevron and says whose reading it is; a fixed button stands on the kit's ground; the diary's
    /// ballot fits its panel without a scroll with Confirm and its way back pinned; the week's recap
    /// clamps to the screen at the larger text with its title and its way on in view; the cast
    /// screen's house-size block reads at thirteen or more; and the veto's draw of sixteen holds on
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

        /// <summary>A fixed button stands on the kit's secondary ground: a drawn image with the kit's sprite under its words, and the kit's edge.</summary>
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
            AssertEveryLabelDraws(rect, where);
        }

        /// <summary>
        /// The diary's ballot as the player sees it: no scroll, the cards inside the window and
        /// drawn; while a choice waits, Confirm and the way back pinned under the scroll as one
        /// pair, inside the panel, without meeting, the scroll clear of them.
        /// </summary>
        private void AssertBallotFits(string where, bool reviewing)
        {
            Canvas.ForceUpdateCanvases();
            var panel = LastActive("Episode panel");
            var content = LastActive("Episode content");
            var viewport = (RectTransform)content.parent;
            Assert.That(content.rect.height, Is.LessThanOrEqualTo(viewport.rect.height + .5f),
                where + " scrolls: " + content.rect.height.ToString("0") + " in " + viewport.rect.height.ToString("0") + ".");
            var cards = LastActive(EpisodeHud.BallotRowName);
            Assert.That(cards, Is.Not.Null, where + " has its cards.");
            AssertInside(ScreenRect(viewport), cards, where + "'s cards");
            AssertEveryLabelDraws(cards, where + "'s cards");
            if (!reviewing) return;
            var pair = LastActive(EpisodeHud.PinnedPairName);
            Assert.That(pair, Is.Not.Null, where + ": Confirm and the way back are a pinned pair.");
            Assert.That(pair.parent, Is.SameAs(panel), where + ": the pair is pinned to the panel, not in its scroll.");
            var confirm = ButtonWithCaption(EpisodeHud.DiaryConfirmCaption);
            var back = ButtonWithCaption(EpisodeHud.DiaryCancelCaption);
            foreach (var button in new[] { confirm, back })
            {
                Assert.That(button.transform.parent, Is.SameAs(pair), "'" + button.name + "' is one of the pair.");
                AssertInside(ScreenRect(panel), (RectTransform)button.transform, "'" + button.name + "'");
                AssertEveryLabelDraws((RectTransform)button.transform, "'" + button.name + "'");
            }
            Assert.That(ScreenRect((RectTransform)confirm.transform).Overlaps(ScreenRect((RectTransform)back.transform)), Is.False,
                where + ": the pair share the row without meeting.");
            float pairTop = Mathf.Max(ScreenRect((RectTransform)confirm.transform).yMax, ScreenRect((RectTransform)back.transform).yMax);
            Assert.That(ScreenRect(viewport).yMin, Is.GreaterThanOrEqualTo(pairTop - .5f), where + ": the scroll runs under the pair.");
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
            var viewport = LastActive("Viewport");
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
        /// rows, the overview's way back to its briefing, and the picker's door to the backdoor
        /// plan each stand on the kit's ground with its edge.
        /// </summary>
        [UnityTest]
        public IEnumerator RowsAndReadings_FixedButtonsStandOnTheKitsGround()
        {
            director.OpenSettings();
            yield return null;
            ButtonWithCaption("Use compact HUD").onClick.Invoke();
            Assert.That(director.CompactHud, Is.True);
            director.ClosePanels();
            yield return null;
            Canvas.ForceUpdateCanvases();
            AssertKitGround(ButtonWithCaption("Go to episode screen"), "The compact objective's 'Go to episode screen'");
            AssertKitGround(ButtonWithCaption(EpisodeHud.DiaryTravelCaption), "The compact objective's '" + EpisodeHud.DiaryTravelCaption + "'");
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
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The eviction vote in the diary at both text sizes: the cards, the line and the voters'
        /// row of chips on one screen without a scroll; a chip a voter who is not the player, each
        /// the control it was; then, with a choice waiting, the cards still on one screen with
        /// Confirm and the way back pinned under them, and the bar saying what a draft is.
        /// </summary>
        [UnityTest]
        public IEnumerator RowsAndReadings_TheBallotFitsItsPanelWithoutAScrollAtBothTextSizes()
        {
            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.Eviction && !state.evictionResolved
                && state.evictionStage == EvictionStage.Voting
                && EpisodeEngine.Voters(state).Any(voter => voter.isPlayer)
                && !state.votes.Any(vote => vote.voterId == state.playerId), "eligible private eviction ballot");
            var before = director.Snapshot;
            string caption = "Vote to evict " + before.Find(before.nominees[0]).name;
            var voters = EpisodeEngine.Voters(before).Where(voter => !voter.isPlayer).ToList();
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                yield return OpenDiaryFixturePanel();
                string where = "The ballot" + (larger ? " at the larger text" : "");
                AssertBallotFits(where, false);
                var roster = LastActive(EpisodeDirector.VotersRowName);
                Assert.That(roster, Is.Not.Null, where + ": the voters are a row of chips.");
                Assert.That(roster.GetComponentsInChildren<Button>().Select(chip => chip.name),
                    Is.EquivalentTo(voters.Select(voter => "Thoughts · " + voter.name)), where + ": a chip a voter who is not the player.");
                AssertEveryLabelDraws(roster, where + "'s voters");

                ButtonWithCaption(caption).onClick.Invoke();
                yield return null; yield return null;
                Assert.That(director.HasDiaryDecisionDraft, Is.True, where + ": the choice waits.");
                AssertBallotFits(where + " with a choice waiting", true);
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

        /// <summary>
        /// The week's recap at both text sizes, on the batch canvas and on both capture frames: the
        /// card inside the screen, the title in a head over the scroll and the way on under it,
        /// both in view and drawn. At the larger text the whole card used to stand past the screen,
        /// sized to the frame before the scaler caught up (weekly-recap-overview-large).
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
        }

        /// <summary>
        /// The cast screen's house-size block reads: its heading, its range line, the count, the two
        /// chips' words and the footnote all set at thirteen or more in boxes 1.3 times their type,
        /// fitting; the chips tall enough to press; and every trait pill on the cards 1.3 times its
        /// word, at eleven or more.
        /// </summary>
        [UnityTest]
        public IEnumerator RowsAndReadings_TheHouseSizeBlockReadsAtThirteenOrMore()
        {
            yield return OpenCastScreen();
            Canvas.ForceUpdateCanvases();
            yield return null;
            var screen = CastScreen();
            Assert.That(screen != null && screen.IsShowing, Is.True, "The cast screen is up.");
            var panel = screen.GetComponentsInChildren<RectTransform>(true)
                .First(rect => rect.name == CastSelect.HouseSizePanelName && rect.gameObject.activeInHierarchy);
            var labels = panel.GetComponentsInChildren<TMP_Text>().ToList();
            foreach (var caption in new[] { "Fewer houseguests", "More houseguests" })
            {
                var chip = (RectTransform)CastButtons(caption)[0].transform;
                Assert.That(chip.rect.height, Is.GreaterThanOrEqualTo(CastSelect.HouseSizeChipHeight - .5f), "'" + caption + "' is tall enough to press.");
                labels.AddRange(chip.GetComponentsInChildren<TMP_Text>());
            }
            labels.AddRange(screen.GetComponentsInChildren<TMP_Text>()
                .Where(label => label.isActiveAndEnabled && (label.name == "House size" || label.text.StartsWith("A shorter season"))));
            Assert.That(labels.Count, Is.GreaterThanOrEqualTo(6), "The block's heading, its range, the count, two chips and the footnote.");
            foreach (var label in labels)
            {
                label.ForceMeshUpdate(true);
                string said = "'" + label.text + "' (" + label.name + ")";
                Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(CastSelect.FootnoteSize - .01f), said + " is set under thirteen: " + label.fontSize.ToString("0.#") + ".");
                Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.fontSize * 1.3f - .5f),
                    said + " has a box " + label.rectTransform.rect.height.ToString("0.0") + " high for a " + label.fontSize.ToString("0.#") + " font.");
                Assert.That(label.isTextOverflowing, Is.False, said + " runs out of its box.");
            }
            var pills = screen.GetComponentsInChildren<RectTransform>()
                .Where(rect => rect.name == CastSelect.TraitChipName && rect.gameObject.activeInHierarchy).ToList();
            Assert.That(pills, Is.Not.Empty, "The cards carry their trait pills.");
            foreach (var pill in pills)
            {
                var word = pill.GetComponentInChildren<TMP_Text>();
                word.ForceMeshUpdate(true);
                Assert.That(word.fontSize, Is.GreaterThanOrEqualTo(11f - .01f), "'" + word.text + "' is set under eleven.");
                Assert.That(pill.rect.height, Is.GreaterThanOrEqualTo(word.fontSize * 1.3f - .5f), "'" + word.text + "' pill is under 1.3 times its word.");
            }
            screen.Dismiss();
            yield return null;
        }
    }
}
