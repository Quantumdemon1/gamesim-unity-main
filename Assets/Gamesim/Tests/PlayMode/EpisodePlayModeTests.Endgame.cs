using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
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
            if (Application.isBatchMode) yield return CaptureFraming("endgame-final-two", settle: false);
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
        }
    }
}
