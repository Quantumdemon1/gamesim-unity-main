using System;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The competition's words to the player (UI-UX-PASS-PLAN C0, decision 12): the Score details
    /// page says how the player entered and what they brought, the briefing's cards say what each
    /// way in does for them, and none of it is a number of the engine's.
    /// </summary>
    public sealed class CompetitionWordsTests
    {
        [Test]
        public void TheDetailsSayHowThePlayerEnteredAndWhatTheyBroughtAndNeverANumber()
        {
            var state = Fixture(1708, EpisodePhase.HoH);
            string played = CompetitionWords.Explanation(Apply(state, EpisodeCommandKind.Compete, .371));
            Assert.That(played, Does.StartWith("You competed: your performance counted"));
            Assert.That(played, Does.Contain("Your preparation counted."));
            Assert.That(played, Does.Contain("The week's events gave you an edge."));
            Assert.That(played, Does.Contain("The story so far weighed on you."));
            Assert.That(played, Does.EndWith(CompetitionWords.Close));
            AssertNoNumber(played);

            string simulated = CompetitionWords.Explanation(Apply(state, EpisodeCommandKind.SimulateCompetition, 0));
            Assert.That(simulated, Does.StartWith("You let the day decide"));
            Assert.That(simulated, Does.Contain("Your preparation counted."));
            AssertNoNumber(simulated);

            string thrown = CompetitionWords.Explanation(Apply(state, EpisodeCommandKind.ThrowCompetition, 0));
            Assert.That(thrown, Does.StartWith("You threw it"));
            Assert.That(thrown, Does.Contain("the day can still hand you the win"));
            Assert.That(thrown, Does.Not.Contain("preparation"), "A throw gives every bonus up, and the words count none.");
            AssertNoNumber(thrown);
        }

        [Test]
        public void AnEarlierSeasonsPlayedCompetitionCountsOnlyTheStory()
        {
            // Through rules 2 a played competition reads the storyline and nothing banked; a
            // simulated one reads the preparation and the week's events and no storyline.
            var state = Fixture(1708, EpisodePhase.HoH);
            state.competitionRulesVersion = 2;
            string played = CompetitionWords.Explanation(Apply(state, EpisodeCommandKind.Compete, .5));
            Assert.That(played, Does.Not.Contain("preparation").And.Not.Contain("week's events"));
            Assert.That(played, Does.Contain("The story so far weighed on you."));
            string simulated = CompetitionWords.Explanation(Apply(state, EpisodeCommandKind.SimulateCompetition, 0));
            Assert.That(simulated, Does.Contain("Your preparation counted.").And.Contain("The week's events gave you an edge."));
            Assert.That(simulated, Does.Not.Contain("story so far"));
        }

        [Test]
        public void AVetoNomineeAndAHaveNotAreToldWhatTheyBrought()
        {
            var state = Fixture(1708, EpisodePhase.Veto);
            if (!state.nominees.Contains(state.playerId)) state.nominees[0] = state.playerId;
            // A season that plays Have-Nots, with the player one of this week's: the engine refuses a
            // Have-Not in a season without them.
            state.haveNotRulesStartWeek = 1;
            state.haveNots.Add(state.playerId);
            string words = CompetitionWords.Explanation(Apply(state, EpisodeCommandKind.Compete, .5));
            Assert.That(words, Does.Contain("On the block, you had something extra to fight for."));
            Assert.That(words, Does.Contain("A Have-Not competes tired, and you were one."));
            AssertNoNumber(words);

            var plain = Fixture(1708, EpisodePhase.HoH);
            Assert.That(CompetitionWords.Explanation(Apply(plain, EpisodeCommandKind.Compete, .5)),
                Does.Not.Contain("On the block").And.Not.Contain("Have-Not"), "Neither is said of a houseguest who is neither.");
        }

        [Test]
        public void AWatcherIsToldTheyWereNotInTheField()
        {
            var state = Fixture(1708, EpisodePhase.HoH);
            state.previousHohId = state.playerId;
            Assert.That(EpisodeEngine.CompetitionPlayers(state).Any(actor => actor.isPlayer), Is.False, "The outgoing Head of Household sits this one out.");
            var watched = Apply(state, EpisodeCommandKind.Advance, 0);
            Assert.That(watched.competitionResolved, Is.True);
            Assert.That(CompetitionWords.Explanation(watched), Is.EqualTo(CompetitionWords.Watched));
            Assert.That(CompetitionWords.Explanation(null), Is.EqualTo(CompetitionWords.DetailsFallback), "A card played without a season explains in the fallback's words.");
        }

        [Test]
        public void TheWaysInSayWhatTheyDoForThePlayer()
        {
            foreach (int rules in new[] { 2, 3, 4 })
            {
                Assert.That(CompetitionWords.SimulateDescription(rules), Does.StartWith("Let the day decide"));
                Assert.That(CompetitionWords.ThrowDescription(rules), Does.StartWith("You compete to lose"));
                foreach (string words in new[] { CompetitionWords.SimulateDescription(rules), CompetitionWords.ThrowDescription(rules),
                    CompetitionWords.PerformanceRule("0–3", false), CompetitionWords.PerformanceRule("0–3", true), CompetitionWords.EarnedRule(3) })
                    AssertNoJargon(words);
            }
            Assert.That(CompetitionWords.ThrowDescription(4), Does.Contain("about nine times in ten"), "The pin the play-mode suite reads the throw by.");
            Assert.That(CompetitionWords.ThrowDescription(3), Does.Contain("you may still win"));
            Assert.That(CompetitionWords.EarnedRule(3), Does.Contain("your preparation (3 of 5)"), "The player's banked preparation, as the diary counts it.");
            Assert.That(CompetitionWords.EarnedRule(0), Does.Not.Contain("of 5"));
            Assert.That(CompetitionWords.EarnedRule(3), Does.StartWith("Play it, let the day decide or take the accessible alternative:"),
                "The three ways in that keep what was earned; a throw gives it up, and its own card says so.");
            Assert.That(CompetitionWords.EarnedRule(3), Does.Not.Contain("However you enter"));
        }

        [Test]
        public void TheEnginesScaffoldingIsNeverAStoryLine()
        {
            // The phase markers and the competition's arithmetic are the record's, left out by every
            // reader that lists events for the player; everything that happened stays a line.
            foreach (string kind in new[] { "phase", "competition-standings", "competition-performance" })
                Assert.That(EpisodeEngine.IsScaffolding(kind), Is.True, kind);
            foreach (string kind in new[] { "conversation", "eviction", "competition", "competition-definition", EpisodeEngine.ThrowEventKind, "", null })
                Assert.That(EpisodeEngine.IsScaffolding(kind), Is.False, kind ?? "null");
        }

        [Test]
        public void TheCardsLinesAndTheFinalPartsRuleAreThePlayersWords()
        {
            foreach (string words in new[] { CompetitionWords.ResultFooter, CompetitionWords.StandingsHeading, CompetitionWords.DetailsHeading,
                CompetitionWords.DetailsFallback, CompetitionWords.PracticePolicy, CompetitionWords.Close, CompetitionWords.Watched, FinalBracket.ScoringLine })
            {
                AssertNoJargon(words);
                AssertNoNumber(words);
            }
            Assert.That(CompetitionWords.ResultFooter, Is.EqualTo("Decided on the day. Practice helps; a close race can go either way."));
            Assert.That(CompetitionWords.StandingsHeading, Is.EqualTo("STANDINGS"));
        }

        [Test]
        public void HasDecimalFindsACompositeScoreAndNotARankOrACount()
        {
            Assert.That(CompetitionWords.HasDecimal("6.42"), Is.True);
            Assert.That(CompetitionWords.HasDecimal("2.  Maya Hassan   6.42  ·  winner"), Is.True);
            Assert.That(CompetitionWords.HasDecimal("Your attempt  ·  held 12.4 s  ·  performance 64%"), Is.True,
                "The player's own measure may carry one; the sweeps exempt that label by its name.");
            Assert.That(CompetitionWords.HasDecimal("1"), Is.False);
            Assert.That(CompetitionWords.HasDecimal("Your attempt  ·  7 / 9 targets hit  ·  performance 78%"), Is.False);
            Assert.That(CompetitionWords.HasDecimal("WEEK 2  ·  HEAD OF HOUSEHOLD  ·  ENDURANCE"), Is.False);
            Assert.That(CompetitionWords.HasDecimal(null), Is.False);
        }

        private static void AssertNoNumber(string words) =>
            Assert.That(Regex.IsMatch(words, @"\d"), Is.False, "A number in the player's words: " + words);

        private static void AssertNoJargon(string words)
        {
            foreach (string jargon in new[] { "seeded", "Weighted", "weighted stat", "LEGACY RULES", "statistics", "/5", "composite" })
                Assert.That(words, Does.Not.Contain(jargon), "'" + jargon + "' in the player's words: " + words);
        }

        /// <summary>A rules-4 season at its first competition with something of everything banked, as CompetitionRulesV4Tests builds one.</summary>
        private static EpisodeState Fixture(uint seed, EpisodePhase phase)
        {
            var state = ContentCatalog.Create(seed);
            state.competitionRulesVersion = 4; state.phase = phase;
            state.playerStudyBonus = 3; state.phaseEventCompBonus = 1;
            state.activeModifiers.Add(new StoryModifierState { id = "focus", name = "Distracted", weeksLeft = 2, competitionBonus = -1 });
            if (phase == EpisodePhase.Veto)
            {
                state.hohId = state.Active.First(actor => !actor.isPlayer).id;
                state.nominees = state.Active.Where(actor => actor.id != state.hohId).Take(2).Select(actor => actor.id).ToList();
                state.vetoPlayers = state.Active.Select(actor => actor.id).ToList();
            }
            return state;
        }

        private static EpisodeState Apply(EpisodeState state, EpisodeCommandKind kind, double performance)
        {
            var result = new EpisodeEngine(state).Apply(new EpisodeCommand
            {
                id = Guid.NewGuid().ToString("N"), actorId = state.playerId, expectedPhase = state.phase,
                expectedRevision = state.revision, kind = kind, performance = performance,
            });
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }
    }
}
