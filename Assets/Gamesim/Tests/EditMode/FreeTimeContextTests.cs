using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Free time's context card and where free time is drawn (UI-UX-PASS-PLAN U0). The role line:
    /// before anybody is Head of Household nothing is decided - it said "You are safe this week" on
    /// move-in night, under a top bar saying "Awaiting HoH" - and once the week's eviction is done
    /// the week is over, whose roles stay in the state until free time closes; with live roles the
    /// line is the week's. And which free times are the strategy stage's - the board and a
    /// houseguest's screen - and which keep the old column.
    /// </summary>
    public sealed class FreeTimeContextTests
    {
        /// <summary>A house of six on move-in night: free time, nobody holding a role, no eviction yet.</summary>
        private static EpisodeState House()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 6 }, 7);
            state.phase = EpisodePhase.Social;
            state.hohId = null;
            state.vetoHolderId = null;
            state.nominees = new List<string>();
            state.evictionResolved = false;
            state.pendingDiary = null;
            return state;
        }

        private static List<string> Others(EpisodeState state) => state.contestants.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();

        [Test]
        public void BeforeAnybodyIsHeadOfHouseholdNothingIsDecided()
        {
            var state = House();
            Assert.That(EpisodeDirector.ContextRole(state, out string why), Is.EqualTo(EpisodeDirector.NothingDecidedRole));
            Assert.That(why, Is.EqualTo(EpisodeDirector.NothingDecidedWhy));
            Assert.That(EpisodeDirector.ContextRole(state, out _), Does.Not.Contain("safe"), "Nobody is safe before the first competition.");
        }

        [Test]
        public void OnceTheRolesAreOutTheLineIsTheWeeks()
        {
            var state = House();
            var others = Others(state);
            state.hohId = others[0];
            Assert.That(EpisodeDirector.ContextRole(state, out _), Is.EqualTo("You are safe this week"), "Safe once somebody else is Head of Household.");
            state.nominees = new List<string> { state.playerId, others[1] };
            Assert.That(EpisodeDirector.ContextRole(state, out _), Is.EqualTo("You are on the block"));
            state.nominees = new List<string> { others[1], others[2] };
            state.vetoHolderId = state.playerId;
            Assert.That(EpisodeDirector.ContextRole(state, out _), Is.EqualTo("You hold the veto"));
            state.hohId = state.playerId;
            Assert.That(EpisodeDirector.ContextRole(state, out _), Is.EqualTo("You are HOH"), "The Head of Household's line outranks the veto's.");
        }

        /// <summary>
        /// The free time after an eviction: the ended week's roles are still in the state until free
        /// time closes, and a surviving nominee, the week's Head of Household and its veto holder all
        /// read that the week is over - never "You are on the block" a week after the vote.
        /// </summary>
        [Test]
        public void AfterTheEvictionTheWeekIsOverWhoeverHeldARole()
        {
            var state = House();
            var others = Others(state);
            state.evictionResolved = true;
            state.hohId = others[0];
            state.nominees = new List<string> { state.playerId, others[1] };
            state.vetoHolderId = others[2];
            Assert.That(EpisodeDirector.ContextRole(state, out string why), Is.EqualTo(EpisodeDirector.WeekOverRole), "A surviving nominee: the week is over.");
            Assert.That(why, Is.EqualTo(EpisodeDirector.WeekOverWhy));
            state.nominees = new List<string> { others[1], others[3] };
            state.hohId = state.playerId;
            Assert.That(EpisodeDirector.ContextRole(state, out _), Is.EqualTo(EpisodeDirector.WeekOverRole), "The ended week's Head of Household too.");
            state.hohId = others[0];
            state.vetoHolderId = state.playerId;
            Assert.That(EpisodeDirector.ContextRole(state, out _), Is.EqualTo(EpisodeDirector.WeekOverRole), "And its veto holder.");
        }

        /// <summary>At three the window is the Final 3's, eviction or no: the final Head of Household is next.</summary>
        [Test]
        public void AtThreeTheFinalHeadOfHouseholdIsNext()
        {
            var state = House();
            foreach (var id in Others(state).Skip(2)) state.Find(id).status = ContestantStatus.Jury;
            state.evictionResolved = true;
            Assert.That(state.Active.Count(), Is.EqualTo(3));
            Assert.That(EpisodeDirector.ContextRole(state, out _), Is.EqualTo("You are in the Final 3"));
        }

        /// <summary>
        /// Which free times are the strategy stage's (U0): move-in night and the window after an
        /// eviction, for a player in the house; never the Final 3's window (Endgame Preparation,
        /// decision 17), a watcher's, one with a legacy house event waiting (the house event's band)
        /// or one with a reflection waiting - those keep the old column, whose scroll stands clear of
        /// its pinned row - and never anything but free time.
        /// </summary>
        [Test]
        public void TheStageIsFreeTimesForAPlayerInTheHouseAndTheOldColumnTheRest()
        {
            var moveIn = House();
            Assert.That(EpisodeDirector.FreeTimeOnTheStage(moveIn), Is.True, "Move-in night is the board's.");

            var afterTheEviction = House();
            afterTheEviction.evictionResolved = true;
            afterTheEviction.hohId = Others(afterTheEviction)[0];
            Assert.That(EpisodeDirector.FreeTimeOnTheStage(afterTheEviction), Is.True, "So is the window after an eviction.");

            var three = House();
            foreach (var id in Others(three).Skip(2)) three.Find(id).status = ContestantStatus.Jury;
            Assert.That(EpisodeDirector.FreeTimeOnTheStage(three), Is.False, "At three the window is Endgame Preparation's column.");

            var watcher = House();
            watcher.Find(watcher.playerId).status = ContestantStatus.Jury;
            Assert.That(EpisodeDirector.FreeTimeOnTheStage(watcher), Is.False, "A player out of the house keeps the screen they watch from.");

            var legacy = House();
            legacy.houseEvents.Add(new HouseEventState { id = "legacy", kind = HouseEventKind.House, title = "A situation", resolved = false });
            Assert.That(EpisodeDirector.FreeTimeOnTheStage(legacy), Is.False, "A legacy house event keeps the house event's band.");
            legacy.houseEvents[0].resolved = true;
            Assert.That(EpisodeDirector.FreeTimeOnTheStage(legacy), Is.True, "Answered, it gives the stage back.");

            var reflecting = House();
            reflecting.pendingDiary = new DiaryPromptState { id = "reflection", week = 1 };
            Assert.That(EpisodeDirector.FreeTimeOnTheStage(reflecting), Is.False, "A reflection waiting keeps its card.");

            var nomination = House();
            nomination.phase = EpisodePhase.Nomination;
            Assert.That(EpisodeDirector.FreeTimeOnTheStage(nomination), Is.False, "Only free time.");
        }
    }
}
