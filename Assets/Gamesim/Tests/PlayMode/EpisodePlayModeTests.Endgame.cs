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
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The finale's opening card, and any other card a fixture's load starts, put away so the
        /// frame under it can be read.
        /// </summary>
        private IEnumerator PutAwayTheCards()
        {
            foreach (var takeover in SceneComponents<CeremonyTakeover>())
                if (takeover.IsPlaying) takeover.Cancel();
            foreach (var sting in SceneComponents<CeremonySting>()) sting.Cancel();
            yield return Frames(3);
            Canvas.ForceUpdateCanvases();
        }

        private RectTransform TheRail() => director.GetComponentsInChildren<RectTransform>(true)
            .Last(rect => rect.name == CastRail.RootName && rect.gameObject.activeInHierarchy);

        /// <summary>Whether this copy carries a crown to draw: the refinement kit's, or the HUD icon set's.</summary>
        private static bool CrownArt() => UiTheme.Pack(PackArt.KitIconCrown) != null || UiTheme.Icon("crown") != null;

        /// <summary>The objectives card's words, top to bottom, as its labels are laid down.</summary>
        private static System.Collections.Generic.List<string> CardLines(RectTransform card) =>
            card.GetComponentsInChildren<TMP_Text>().Select(label => label.text).ToList();

        /// <summary>
        /// ENDGAME-PLAN F1: at three the frame strips down to what is left. The week chip leads
        /// with FINAL 3 and keeps the week beside the phase; the objective says what the endgame
        /// is about over the unchanged next stop; the house pill's third cell is the jury's size
        /// while nobody holds the house; the objectives card takes the vibe card's place with its
        /// three marks and the jurors' faces, and still ends above the status band; the feed is
        /// two rows; the rail is the three who are left.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_TheFinalThreeStripTheFrameToWhatIsLeft()
        {
            HoldTheHouseForTheFixture();
            yield return InstallDiaryFixture(
                state => state.phase == EpisodePhase.FinalHoHPart1 && state.Active.Any(actor => actor.isPlayer),
                "the player at the Final 3");
            yield return PutAwayTheCards();
            var state = director.Snapshot;
            Assume.That(EpisodeHud.IsFinalThree(state), Is.True, "Three active in the first part of the final Head of Household.");
            Assume.That(state.hohId, Is.Null, "The final-four eviction clears the Head of Household.");
            int jurors = state.contestants.Count(EpisodeHud.IsJuror);
            Assume.That(jurors, Is.GreaterThan(0), "A jury is seated by the Final 3.");

            var week = ActiveRect("Week chip");
            Assert.That(week, Is.Not.Null, "The week chip is up under the finale's card.");
            Assert.That(Words(week), Does.Contain("FINAL 3").And.Contain("Week " + state.week),
                "The chip leads with FINAL 3 and keeps the week beside the phase.");
            var objective = ActiveRect("Objective");
            Assert.That(objective, Is.Not.Null);
            Assert.That(Words(objective), Does.Contain("Final Head of Household ahead").And.Contain("Next stop:"),
                "The objective says what the endgame is about, over the next stop it always had.");
            var pill = ActiveRect("House pill");
            Assert.That(pill, Is.Not.Null);
            Assert.That(Words(pill), Does.Contain("Jury").And.Contain(jurors.ToString()).And.Not.Contain("HoH"),
                "With nobody holding the house, the pill's third cell is the jury's size.");

            Assert.That(ActiveRect(EpisodeHud.HouseVibeCardName), Is.Null, "The objectives take the vibe card's place.");
            var card = ActiveRect(EpisodeHud.ObjectivesCardName);
            Assert.That(card, Is.Not.Null, "The right column carries the objectives card.");
            var words = Words(card);
            Assert.That(words, Does.Contain("FINAL 3 OBJECTIVES")
                .And.Contain("Win the Final HoH").And.Contain("Part 1 of 3")
                .And.Contain("Decide who to trust").And.Contain("Prepare your case"),
                "The outline's three objectives, each with where it stands.");
            Assert.That(words, Does.Contain("The jury (" + jurors + ")"), "The jury's size heads its faces.");
            Assert.That(card.GetComponentsInChildren<Image>().Count(image => image.name == "Objective mark"), Is.EqualTo(3),
                "A mark per row.");
            // MOCKUP-PASS M3: the mockups' tagline under the heading, for a finalist with the final
            // Head of Household ahead, and the crown in the objective chip's ring's place.
            var lines = CardLines(card);
            Assert.That(lines.IndexOf(EpisodeHud.ObjectivesTagline), Is.EqualTo(lines.IndexOf("FINAL 3 OBJECTIVES") + 1),
                "The tagline sits under the heading.");
            if (CrownArt())
            {
                Assert.That(objective.GetComponentsInChildren<Image>().Count(image => image.name == EpisodeHud.ObjectiveCrownName), Is.EqualTo(1),
                    "The objective chip wears the crown at the endgame,");
                Assert.That(objective.GetComponentsInChildren<Image>().Any(image => image.name == "Objective mark"), Is.False, "in place of the ring.");
                Assert.That(card.GetComponentsInChildren<Image>().Count(image => image.name == EpisodeHud.ObjectivesCrownName), Is.EqualTo(1),
                    "and the objectives card a crown before its heading.");
            }
            var strip = ActiveRect(EpisodeHud.JuryStripName);
            Assert.That(strip, Is.Not.Null);
            Assert.That(strip.GetComponentsInChildren<Image>().Count(image => image.name == "Juror"), Is.EqualTo(jurors), "One face per juror.");

            var events = ActiveRect(EpisodeHud.RecentEventsCardName);
            Assert.That(events, Is.Not.Null, "The feed is still up.");
            Assert.That(events.GetComponentsInChildren<Image>().Count(image => image.name == "Event mark"), Is.LessThanOrEqualTo(2),
                "A quieter feed at the endgame: two rows.");
            Assert.That(ScreenRect(card).yMax, Is.LessThanOrEqualTo(ScreenRect(events).yMin + 1f),
                "The objectives stack below the feed.");
            var band = ActiveRect("Status");
            Assert.That(band, Is.Not.Null);
            Assert.That(ScreenRect(card).yMin, Is.GreaterThan(ScreenRect(band).yMax),
                "The column still ends above the status band, as it does with the vibe card.");

            var rail = TheRail();
            Assert.That(rail.Cast<Transform>().Count(), Is.EqualTo(3), "The strip is the three who are left; the jury has its faces in the column.");
            foreach (var actor in state.Active)
                Assert.That(rail.Cast<Transform>().Any(entry => entry.name == actor.name), Is.True, actor.name + " keeps a chip.");

            // The chrome's small parts whole (UI-UX-PASS-PLAN T0): the strip's door inside the card
            // with its words drawn, and painting nothing over them while the column stands down;
            // every pin's count badge on the pin's shoulder, reading whole; and no name chip under
            // a pin, whose tail read as the pin's own - the yard's count of one over "Taylor".
            var door = FindButton(EpisodeDirector.JuryStripCaption);
            Assert.That(door, Is.Not.Null, "The strip is a door to the jury house.");
            AssertInside(ScreenRect(card), (RectTransform)door.transform, "The strip's door");
            var doorWords = door.GetComponentsInChildren<TMP_Text>().Single();
            doorWords.ForceMeshUpdate(true);
            Assert.That(doorWords.isTextOverflowing, Is.False, "'" + doorWords.text + "' fits its box at " + doorWords.fontSize.ToString("0.#") + ".");
            AssertEveryLabelDraws((RectTransform)door.transform, "The strip's door");
            Assert.That(door.colors.disabledColor.a, Is.EqualTo(0f).Within(.001f), "The door paints nothing over its words while the column stands down.");
            var beacons = director.GetComponentInChildren<EpisodeTravelBeacons>(true);
            if (beacons != null && beacons.IsShowing)
            {
                var icons = beacons.transform.Cast<Transform>().Select(child => (RectTransform)child)
                    .Where(rect => rect.gameObject.activeInHierarchy && rect.Find("Disc") != null).ToList();
                foreach (var icon in icons)
                {
                    var badge = icon.Find(EpisodeTravelBeacons.BadgeName) as RectTransform;
                    if (badge == null || !badge.gameObject.activeInHierarchy) continue;
                    var count = badge.GetComponentInChildren<TMP_Text>();
                    count.ForceMeshUpdate(true);
                    Assert.That(count.isTextTruncated || count.isTextOverflowing, Is.False, "'" + count.text + "' on '" + icon.name + "' reads whole.");
                    var shoulder = ScreenRect(icon);
                    shoulder.xMax += 12f; shoulder.yMax += 12f;
                    AssertInside(shoulder, badge, "'" + icon.name + "'s badge");
                }
                foreach (var chip in beacons.transform.Cast<Transform>().Select(child => (RectTransform)child)
                    .Where(rect => rect.gameObject.activeInHierarchy && rect.name.StartsWith(EpisodeTravelBeacons.NameChipPrefix)))
                    foreach (var icon in icons)
                        Assert.That(ScreenRect(chip).Overlaps(ScreenRect(icon)), Is.False, "'" + chip.name + "' stands under '" + icon.name + "'.");
            }
            // The look sheet: the frame at three, for the eye the measurements cannot replace.
            if (Application.isBatchMode) yield return CaptureFraming("endgame-final-three", settle: false);
        }

        /// <summary>
        /// The same frame at two: FINAL 2 on the chip, "The jury decides" over the next stop, the
        /// Final 2's three objectives, and a rail of two.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_TheFinalTwoFaceTheJuryInTheFrame()
        {
            HoldTheHouseForTheFixture();
            yield return InstallFinaleFixture(true);
            yield return PutAwayTheCards();
            var state = director.Snapshot;
            Assume.That(EpisodeHud.IsFinalTwo(state), Is.True, "Two active, the jury's questioning under way.");

            Assert.That(Words(ActiveRect("Week chip")), Does.Contain("FINAL 2").And.Contain("Week " + state.week));
            Assert.That(Words(ActiveRect("Objective")), Does.Contain("The jury decides").And.Contain("Next stop:"));
            var card = ActiveRect(EpisodeHud.ObjectivesCardName);
            Assert.That(card, Is.Not.Null, "The objectives card stays for the Final 2.");
            Assert.That(Words(card), Does.Contain("FINAL 2 OBJECTIVES")
                .And.Contain("Answer the jury's questions").And.Contain("Deliver your final speech").And.Contain("Await the jury vote"));
            int jurors = state.contestants.Count(EpisodeHud.IsJuror);
            Assert.That(ActiveRect(EpisodeHud.JuryStripName).GetComponentsInChildren<Image>().Count(image => image.name == "Juror"), Is.EqualTo(jurors));
            Assert.That(TheRail().Cast<Transform>().Count(), Is.EqualTo(2), "The strip is the two finalists.");

            // MOCKUP-PASS M3: the pill carries the jury all finale night, whoever holds the house, and
            // the Final 2 in the actions' place; the strap names the finale; the card keeps a mark a
            // row, and the rules-off rows above, with its tagline left to the Final 3.
            Assume.That(jurors, Is.GreaterThan(0), "A jury is seated by the Final 2.");
            var pill = ActiveRect("House pill");
            Assert.That(pill, Is.Not.Null);
            Assert.That(Words(pill), Does.Contain("Jury").And.Contain(jurors.ToString()).And.Not.Contain("HoH"),
                "Finale night: the pill's third cell is the jury's size, whoever holds the house.");
            Assert.That(Words(pill), Does.Contain("Finalists").And.Not.Contain("Actions left"),
                "The cell after the house's count is the two facing the jury, not actions nobody spends.");
            Assert.That(Words(ActiveRect("Brand")), Does.Contain(EpisodeHud.BrandStrap(state, director.SeasonNumber(state))).And.Contain("FINALE"),
                "The strap under the wordmark names the finale.");
            Assert.That(card.GetComponentsInChildren<Image>().Count(image => image.name == "Objective mark"), Is.EqualTo(3), "A mark per row.");
            Assert.That(Words(card), Does.Not.Contain(EpisodeHud.ObjectivesTagline), "The tagline is the Final 3's.");
            if (Application.isBatchMode) yield return CaptureFraming("endgame-final-two", settle: false);
        }

        /// <summary>
        /// The owner's decision 46 (MOCKUP-PASS M3, review correction 15): under the finale rules
        /// the final case leads the Final 2's objectives in the questions' place, open until the
        /// argument is locked and locked after, its mark gold. The pin above runs with the rules
        /// off and keeps the questions' row.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_UnderTheFinaleRulesTheFinalCaseLeadsTheFinalTwosObjectives()
        {
            HoldTheHouseForTheFixture();
            yield return InstallFinaleFixture(true, finaleRules: true);
            yield return PutAwayTheCards();
            var state = director.Snapshot;
            Assume.That(EpisodeHud.IsFinalTwo(state), Is.True, "Two active, the jury's questioning under way.");
            Assert.That(EpisodeEngine.FinaleOn(state), Is.True);
            Assert.That(state.finalArgument, Is.Null, "Nothing is locked yet.");

            var card = ActiveRect(EpisodeHud.ObjectivesCardName);
            Assert.That(card, Is.Not.Null, "The objectives card is up for the Final 2.");
            var lines = CardLines(card);
            int lead = lines.IndexOf(EpisodeHud.FinalCaseObjective);
            Assert.That(lead, Is.GreaterThanOrEqualTo(0), "The final case is an objective under the rules,");
            Assert.That(lines, Does.Not.Contain("Answer the jury's questions"), "in the questions' place,");
            Assert.That(lead, Is.EqualTo(lines.IndexOf("FINAL 2 OBJECTIVES") + 1), "and first under the heading.");
            Assert.That(lines[lead + 1], Is.EqualTo("Open"), "Open until the argument is locked.");
            Assert.That(lines, Does.Contain("Deliver your final speech").And.Contain("Await the jury vote"));
            Assert.That(card.GetComponentsInChildren<Image>().Count(image => image.name == "Objective mark"), Is.EqualTo(3), "Still a mark per row.");

            // The lock, through the final case's own calls; the row reads it from the commit.
            var moments = FinalArgument.Moments(state);
            int required = FinalArgument.Required(state);
            Assume.That(required, Is.GreaterThan(0), "A season played to the final has moments to choose.");
            yield return OpenFinalePanel();
            director.OpenFinalCase();
            yield return Frames(1);
            Assert.That(director.InFinalCase, Is.True);
            director.ChooseTheme(FinalArgument.Cerebral);
            foreach (var moment in moments.Take(required)) director.ToggleMoment(moment.reference);
            director.LockFinalArgument();
            yield return Frames(2);
            Assert.That(director.Snapshot.finalArgument, Is.Not.Null, "The argument is locked.");
            director.ClosePanels();
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();

            card = ActiveRect(EpisodeHud.ObjectivesCardName);
            Assert.That(card, Is.Not.Null);
            lines = CardLines(card);
            lead = lines.IndexOf(EpisodeHud.FinalCaseObjective);
            Assert.That(lead, Is.GreaterThanOrEqualTo(0));
            Assert.That(lines[lead + 1], Is.EqualTo("Locked"), "Locked once the argument is.");
            var marks = card.GetComponentsInChildren<Image>().Where(image => image.name == "Objective mark").ToList();
            Assert.That(marks, Has.Count.EqualTo(3));
            Assert.That(marks[0].color, Is.EqualTo(UiTheme.Gold), "The locked case's mark is the done gold.");
        }

        /// <summary>
        /// The window the final-four eviction opens is the Final 3's preparation: the frame already
        /// reads FINAL 3 with the final Head of Household ahead, and the free-time screen's head is
        /// ENDGAME PREPARATION over the same count of actions, the same cards and the same moves.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_TheWindowAtThreeIsEndgamePreparation()
        {
            HoldTheHouseForTheFixture();
            yield return InstallDiaryFixture(
                state => state.phase == EpisodePhase.Social && state.Active.Count() == 3
                    && state.Active.Any(actor => actor.isPlayer) && state.pendingDiary == null,
                "the player in the window at three");
            yield return PutAwayTheCards();
            var state = director.Snapshot;
            Assume.That(EpisodeHud.IsFinalThree(state), Is.True, "Free time with three active is the Final 3.");

            Assert.That(Words(ActiveRect("Week chip")), Does.Contain("FINAL 3"), "The chip leads with FINAL 3 from the window on.");
            Assert.That(Words(ActiveRect("Objective")), Does.Contain("Final Head of Household ahead"));
            var card = ActiveRect(EpisodeHud.ObjectivesCardName);
            Assert.That(card, Is.Not.Null, "The objectives card is up in the window.");
            Assert.That(Words(card), Does.Contain("Win the Final HoH").And.Contain("Ahead"),
                "No part has been played: the final Head of Household is ahead.");

            yield return OpenFreeTime();
            var head = ActiveRect(EpisodeHud.ScreenHeadName);
            Assert.That(head, Is.Not.Null, "The screen has a head.");
            int left = Mathf.Max(0, EpisodeEngine.SocialActionBudget(state) - EpisodeEngine.SocialActionsSpent(state));
            Assert.That(Words(head), Does.Contain(EpisodeDirector.EndgamePreparationTitle)
                    .And.Contain(EpisodeDirector.ActionsLeftHeadline(left)).And.Not.Contain("FREE TIME"),
                "The head names the window for what it is now, over the same count of actions.");
            Assert.That(ActiveRect(EpisodeHud.HouseCardsName), Is.Not.Null, "The house's cards are still the decision.");

            // A houseguest's screen: the context card speaks for the Final 3, not the final-four
            // week whose roles stay in state until the window closes.
            director.OpenHouseguestScreen(state.Active.First(actor => !actor.isPlayer).id);
            yield return null; yield return null;
            var context = LastActive(EpisodeHud.YourContextCardName);
            Assert.That(context, Is.Not.Null, "The houseguest screen has its context card.");
            Assert.That(Words(context), Does.Contain("You are in the Final 3").And.Contain("Final Head of Household ahead")
                .And.Not.Contain("You are HOH").And.Not.Contain("You are on the block").And.Not.Contain("Current Objective"));
            director.CloseHouseguestScreen();
            yield return null;
        }

        /// <summary>
        /// The words the frame is read by, from the committed state alone: an ordinary week is
        /// neither, three active anywhere else is not the Final 3, and the finished season is the
        /// endgame without being either.
        /// </summary>
        [Test]
        public void Endgame_TheFrameReadsTheEndgameFromTheState()
        {
            var state = director.Snapshot;
            Assume.That(state.Active.Count(), Is.GreaterThan(3));
            Assert.That(EpisodeHud.IsFinalThree(state), Is.False);
            Assert.That(EpisodeHud.IsFinalTwo(state), Is.False);
            Assert.That(EpisodeHud.IsEndgame(state), Is.False);
            Assert.That(EpisodeHud.EndgameLabel(state), Is.Null);
            Assert.That(EpisodeHud.ObjectiveTitle(state), Is.EqualTo("Current Objective"));

            var three = state.Clone();
            foreach (var actor in three.Active.Where(actor => !actor.isPlayer).Skip(2).ToList()) actor.status = ContestantStatus.Jury;
            Assume.That(three.Active.Count(), Is.EqualTo(3));
            three.phase = EpisodePhase.FinalHoHPart2;
            Assert.That(EpisodeHud.IsFinalThree(three), Is.True);
            Assert.That(EpisodeHud.EndgameLabel(three), Is.EqualTo("FINAL 3"));
            Assert.That(EpisodeHud.ObjectiveTitle(three), Is.EqualTo("Final Head of Household ahead"));
            three.phase = EpisodePhase.FinalEviction; three.hohId = three.playerId;
            Assert.That(EpisodeHud.ObjectiveTitle(three), Is.EqualTo("One decision remains"));
            three.hohId = three.Active.First(actor => !actor.isPlayer).id;
            Assert.That(EpisodeHud.ObjectiveTitle(three), Is.EqualTo("The final Head of Household decides"));
            three.phase = EpisodePhase.Social; three.hohId = null;
            Assert.That(EpisodeHud.IsFinalThree(three), Is.True, "The window the final-four eviction opens is the Final 3's.");
            Assert.That(EpisodeHud.ObjectiveTitle(three), Is.EqualTo("Final Head of Household ahead"));
            three.phase = EpisodePhase.Nomination;
            Assert.That(EpisodeHud.IsFinalThree(three), Is.False, "Three active in an ordinary phase is not the Final 3.");

            var finished = state.Clone();
            finished.phase = EpisodePhase.Finished;
            Assert.That(EpisodeHud.IsEndgame(finished), Is.True);
            Assert.That(EpisodeHud.EndgameLabel(finished), Is.EqualTo("FINALE"));
            Assert.That(EpisodeHud.ObjectiveTitle(finished), Is.EqualTo("Season complete"));

            // MOCKUP-PASS M3: the header's titles. One for the final Head of Household's three
            // parts, which the line under it tells apart; the finale's two jury beats; and the final
            // eviction the player's to choose only while they hold the house.
            foreach (var part in new[] { EpisodePhase.FinalHoHPart1, EpisodePhase.FinalHoHPart2, EpisodePhase.FinalHoHPart3 })
                Assert.That(EpisodeDirector.PhaseTitle(part), Is.EqualTo("FINAL HEAD OF HOUSEHOLD"), part.ToString());
            Assert.That(EpisodeDirector.PhaseTitle(EpisodePhase.JuryQuestioning), Is.EqualTo("FINALE · FACE THE JURY"));
            Assert.That(EpisodeDirector.PhaseTitle(EpisodePhase.Jury), Is.EqualTo("FINALE · JURY VOTE"));
            Assert.That(EpisodeDirector.PhaseTitle(finished), Is.EqualTo("SEASON FINALE"));
            three.phase = EpisodePhase.FinalEviction; three.hohId = three.playerId;
            Assert.That(EpisodeDirector.PhaseTitle(three), Is.EqualTo("CHOOSE YOUR FINAL TWO"));
            three.hohId = three.Active.First(actor => !actor.isPlayer).id;
            Assert.That(EpisodeDirector.PhaseTitle(three), Is.EqualTo("THE FINAL EVICTION"), "Somebody else decides.");

            // The objectives card's tagline, a finalist's only while the competition or the decision
            // it names is still theirs to come: Part 3 is the Part 1 and Part 2 winners', and the
            // decision is the Part 3 winner's.
            var parts = three.Clone();
            var rivals = parts.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            parts.hohId = null; parts.competitionResolved = false; parts.finalPart1WinnerId = null; parts.finalPart2WinnerId = null;
            parts.phase = EpisodePhase.FinalHoHPart1;
            Assert.That(EpisodeHud.ObjectivesTaglineShows(parts), Is.True, "Every finalist plays Part 1.");
            parts.phase = EpisodePhase.FinalHoHPart2; parts.finalPart1WinnerId = rivals[0];
            Assert.That(EpisodeHud.ObjectivesTaglineShows(parts), Is.True, "Beaten in Part 1, the player plays Part 2.");
            parts.competitionResolved = true; parts.finalPart2WinnerId = rivals[1];
            Assert.That(EpisodeHud.ObjectivesTaglineShows(parts), Is.False, "Beaten in Part 2, the player has played their last competition,");
            parts.phase = EpisodePhase.FinalHoHPart3; parts.competitionResolved = false;
            Assert.That(EpisodeHud.ObjectivesTaglineShows(parts), Is.False, "and sits Part 3 out.");
            parts.finalPart2WinnerId = parts.playerId;
            Assert.That(EpisodeHud.ObjectivesTaglineShows(parts), Is.True, "Part 2's winner has Part 3 ahead,");
            parts.competitionResolved = true; parts.hohId = rivals[0];
            Assert.That(EpisodeHud.ObjectivesTaglineShows(parts), Is.False, "and nothing left once Part 3 goes to somebody else.");
            parts.hohId = parts.playerId;
            Assert.That(EpisodeHud.ObjectivesTaglineShows(parts), Is.True, "Part 3's winner has the decision to come.");
            parts.phase = EpisodePhase.FinalHoHPart2; parts.hohId = null;
            parts.finalPart1WinnerId = parts.playerId; parts.finalPart2WinnerId = rivals[1];
            Assert.That(EpisodeHud.ObjectivesTaglineShows(parts), Is.True, "Part 1's winner sits Part 2 out with Part 3 still ahead.");
            parts.phase = EpisodePhase.FinalEviction; parts.competitionResolved = false; parts.hohId = parts.playerId;
            Assert.That(EpisodeHud.ObjectivesTaglineShows(parts), Is.True, "The final Head of Household makes the decision,");
            parts.hohId = rivals[0];
            Assert.That(EpisodeHud.ObjectivesTaglineShows(parts), Is.False, "and nobody else does.");

            // The strap: the season's size through the weeks, the finale all finale night, by its
            // number wherever the career record gives one (review correction 22).
            Assert.That(EpisodeHud.BrandStrap(state, 3), Is.EqualTo("THE HOUSE  |  " + state.contestants.Count + "-PERSON SEASON"));
            Assert.That(EpisodeHud.BrandStrap(finished, 3), Is.EqualTo("THE HOUSE  |  SEASON 3 FINALE"));
            Assert.That(EpisodeHud.BrandStrap(finished, null), Is.EqualTo("THE HOUSE  |  SEASON FINALE"), "No number known, none printed.");
            var questioning = finished.Clone();
            questioning.phase = EpisodePhase.JuryQuestioning;
            Assert.That(EpisodeHud.BrandStrap(questioning, 2), Is.EqualTo("THE HOUSE  |  SEASON 2 FINALE"));
        }
    }
}
