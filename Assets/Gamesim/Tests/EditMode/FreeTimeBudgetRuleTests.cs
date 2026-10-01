using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The free-time budget card's rule line (ACTIONS-DEALS-ALLIANCES-PLAN X-a). Move-in night is
    /// the week's free-time window, but nobody has been evicted yet: it says what the night has and
    /// that it does not carry into the week, scaled to the night's real seats, rather than calling
    /// itself 'After the eviction'. Week one's second free time, after the first eviction, and every
    /// later one keep the window's own words, and a season before the week's rules keeps its pool's.
    /// </summary>
    public sealed class FreeTimeBudgetRuleTests
    {
        private static EpisodeState MoveIn(int houseguests)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = houseguests }, 5u);
            EpisodeEngine.EnableWeek(state);
            // Past the roster's largest, the house is filled the way the cast-size tests fill it.
            while (state.contestants.Count < houseguests)
            {
                int index = state.contestants.Count;
                state.contestants.Add(new ContestantState
                {
                    id = "extra-" + index, name = "Extra " + index, pronouns = "they/them", homeRoom = "Living",
                    motive = "Added by a test to fill the house.", status = ContestantStatus.Active,
                    traits = new List<string> { "Social" }, stats = new ContestantStats(),
                });
            }
            return state;
        }

        [Test]
        public void MoveInNightSaysItsOneActionDoesNotCarryIntoTheWeek()
        {
            var night = MoveIn(8);
            Assert.That(EpisodeEngine.IsFirstNight(night), Is.True, "The fixture is move-in night.");
            Assert.That(EpisodeDirector.BudgetRule(night), Is.EqualTo("1 action tonight; it does not carry into the week."));
            Assert.That(EpisodeDirector.BudgetRule(night), Does.Not.Contain(Windows.Names[Windows.AfterEviction]),
                "Nobody has been evicted on move-in night.");
        }

        [Test]
        public void MoveInNightsRuleIsScaledToTheNightsSeats()
        {
            var big = MoveIn(16);
            Assert.That(big.Active.Count(), Is.EqualTo(16));
            Assert.That(EpisodeDirector.BudgetRule(big), Is.EqualTo("5 actions tonight; they do not carry into the week."),
                "A house of sixteen has five seats on its first night.");
        }

        [Test]
        public void TheFreeTimeAfterAnEvictionKeepsTheWindowsOwnWords()
        {
            var after = MoveIn(8);
            // Week one's second free time: a Head of Household was crowned and the vote is in.
            after.hohId = after.Active.First(actor => !actor.isPlayer).id;
            after.evictionResolved = true;
            Assert.That(EpisodeEngine.IsFirstNight(after), Is.False);
            Assert.That(EpisodeDirector.BudgetRule(after),
                Is.EqualTo(Windows.Names[Windows.AfterEviction] + ". What you do not spend here does not carry to the next window."));
            var later = MoveIn(8);
            later.week = 2;
            Assert.That(EpisodeDirector.BudgetRule(later), Does.StartWith(Windows.Names[Windows.AfterEviction] + "."));
        }

        [Test]
        public void ASeasonBeforeTheWeeksRulesKeepsItsPoolsRule()
        {
            var pool = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 5u);
            Assert.That(EpisodeEngine.WeekRulesOn(pool), Is.False);
            Assert.That(EpisodeDirector.BudgetRule(pool),
                Is.EqualTo("The house gives you half its number in actions each week, so the budget tightens as people leave."));
        }

        /// <summary>
        /// Move-in night with an action bought (the coordinator's review): the line counts what ACTIONS
        /// LEFT counts, says the night's own seat does not carry into the week and the extra does -
        /// which it does, the week not turning tonight - and what moving on loses is the seat alone.
        /// </summary>
        [Test]
        public void MoveInNightWithAnActionBoughtSaysWhatTheCountSays()
        {
            var night = MoveIn(8);
            night.boughtActionPoints = 1;
            Assert.That(EpisodeEngine.SocialActionBudget(night) - EpisodeEngine.SocialActionsSpent(night), Is.EqualTo(2),
                "The night's one seat and the action bought: two left.");
            Assert.That(EpisodeDirector.BudgetRule(night), Is.EqualTo("2 actions tonight; 1 does not carry into the week, the extra 1 does."));
            Assert.That(EpisodeDirector.UnusedActionsNote(night), Is.EqualTo("1 unused action will be lost."), "Moving on loses the seat, not what was bought.");
            // The seat spent, what is left is the bought action, which the week's first window has whole again.
            night.windowActions[Windows.AfterEviction] = 1;
            Assert.That(EpisodeEngine.SocialActionBudget(night) - EpisodeEngine.SocialActionsSpent(night), Is.EqualTo(1));
            Assert.That(EpisodeDirector.UnusedActionsNote(night), Is.Null, "Moving on loses nothing it does not give back.");

            var big = MoveIn(16);
            big.boughtActionPoints = 2;
            Assert.That(EpisodeDirector.BudgetRule(big), Is.EqualTo("7 actions tonight; 5 do not carry into the week, the extra 2 do."));
            Assert.That(EpisodeDirector.UnusedActionsNote(big), Is.EqualTo("5 unused actions will be lost."));
        }

        /// <summary>
        /// The budget card's copy says what beginning the next competition loses as the engine counts
        /// it: on move-in night, which does not turn the week, what was bought carries into it; in a
        /// week that turns, under the levers, everything left goes, as the unused note counts it; and
        /// without the levers bought time is never reset, so it comes back every week.
        /// </summary>
        [Test]
        public void TheCostCopySaysWhatBeginningTheNextCompetitionLoses()
        {
            var night = MoveIn(8);
            EpisodeEngine.EnableLevers(night);
            Assert.That(EpisodeDirector.FreeTimeCostLine(night), Is.EqualTo(EpisodeDirector.FreeTimeCostCopy
                + " Unspent actions are lost when you begin the next competition, but actions you buy carry into the week."));

            var week = MoveIn(8);
            EpisodeEngine.EnableLevers(week);
            week.hohId = week.Active.First(actor => !actor.isPlayer).id;
            week.evictionResolved = true;
            Assert.That(EpisodeEngine.LeverRulesOn(week), Is.True);
            Assert.That(EpisodeDirector.FreeTimeCostLine(week),
                Is.EqualTo(EpisodeDirector.FreeTimeCostCopy + " Unspent actions are lost when you begin the next competition."));
            week.boughtActionPoints = 1;
            int left = EpisodeEngine.SocialActionBudget(week) - EpisodeEngine.SocialActionsSpent(week);
            Assert.That(left, Is.EqualTo(2), "The window's seat and the action bought.");
            Assert.That(EpisodeDirector.UnusedActionsNote(week), Is.EqualTo("2 unused actions will be lost."), "The week turning takes both.");

            var unreset = MoveIn(8);
            unreset.hohId = unreset.Active.First(actor => !actor.isPlayer).id;
            unreset.evictionResolved = true;
            Assert.That(EpisodeEngine.LeverRulesOn(unreset), Is.False);
            Assert.That(EpisodeDirector.FreeTimeCostLine(unreset),
                Is.EqualTo(EpisodeDirector.FreeTimeCostCopy + " Unspent actions are lost when you begin the next competition; actions you buy come back every week."));
        }
    }
}
