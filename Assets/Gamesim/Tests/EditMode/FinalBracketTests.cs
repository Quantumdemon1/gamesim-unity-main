using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// MOCKUP-PASS-PLAN M13: the final Head of Household's part tracker reads the public record -
    /// who won each part played, who plays the part in hand, what is still locked - speaks to the
    /// player as "You", and never prints a score. Unity-free, so the dotnet subset runs it
    /// (Tools/SimulationTests).
    /// </summary>
    public sealed class FinalBracketTests
    {
        /// <summary>A Final 3 at the first part: the player and two others, three jurors.</summary>
        private static EpisodeState FinalThree(uint seed = 41)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 6 }, seed);
            s.week = 9;
            foreach (var juror in s.contestants.Where(c => !c.isPlayer).Take(3)) juror.status = ContestantStatus.Jury;
            s.phase = EpisodePhase.FinalHoHPart1;
            s.competitionResolved = false;
            s.hohId = null;
            return s;
        }

        private static ContestantState Other(EpisodeState s, int index) => s.Active.Where(c => !c.isPlayer).ElementAt(index);

        private static string First(ContestantState actor) => FinalistRead.FirstName(actor.name);

        [Test]
        public void OutsideTheFinalPartsThereIsNoBracket()
        {
            var s = FinalThree();
            foreach (var phase in new[] { EpisodePhase.HoH, EpisodePhase.Veto, EpisodePhase.FinalEviction, EpisodePhase.Finished })
            {
                s.phase = phase;
                Assert.That(FinalBracket.For(s), Is.Null, phase + " has no bracket.");
                Assert.That(FinalBracket.AdvanceLine(phase), Is.Null);
                Assert.That(FinalBracket.PartOf(phase), Is.Zero);
            }
            Assert.That(FinalBracket.For(null), Is.Null);
        }

        [Test]
        public void PartOneIsPlayedByAllThreeAndEverythingAfterItIsLocked()
        {
            var s = FinalThree();
            var bracket = FinalBracket.For(s);
            Assert.That(bracket.current, Is.EqualTo(1));
            Assert.That(bracket.parts.Select(p => p.number), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(bracket.advance, Is.EqualTo("Winner goes straight to Part 3"));

            var one = bracket.parts[0];
            Assert.That(one.standing, Is.EqualTo(FinalBracket.Standing.Playing));
            Assert.That(one.faces.Select(f => f.id), Is.EquivalentTo(s.Active.Select(c => c.id)), "All three play Part 1.");
            Assert.That(one.line.Split(new[] { " vs " }, System.StringSplitOptions.None), Has.Length.EqualTo(3));
            Assert.That(one.line, Does.Contain("You").And.Contain(First(Other(s, 0))).And.Contain(First(Other(s, 1))));

            Assert.That(bracket.parts[1].standing, Is.EqualTo(FinalBracket.Standing.Ahead));
            Assert.That(bracket.parts[1].line, Is.EqualTo("Locked"));
            Assert.That(bracket.parts[1].faces, Is.Empty, "Nobody knows who plays Part 2 until Part 1 is won.");
            Assert.That(bracket.parts[2].line, Is.EqualTo("Final round · Locked"));
            Assert.That(bracket.parts[2].faces, Is.Empty);
            Assert.That(bracket.parts[2].openSeat, Is.False);
        }

        [Test]
        public void PartTwoShowsPartOnesWinnerAndTheSeatStillOpenInPartThree()
        {
            var s = FinalThree();
            var winner = Other(s, 0);
            s.finalPart1WinnerId = winner.id;
            s.phase = EpisodePhase.FinalHoHPart2;
            var bracket = FinalBracket.For(s);
            Assert.That(bracket.current, Is.EqualTo(2));
            Assert.That(bracket.advance, Is.EqualTo("Winner advances to Part 3"));

            var one = bracket.parts[0];
            Assert.That(one.standing, Is.EqualTo(FinalBracket.Standing.Played));
            Assert.That(one.line, Is.EqualTo("Winner: " + First(winner)));
            Assert.That(one.faces.Single().id, Is.EqualTo(winner.id));

            var two = bracket.parts[1];
            Assert.That(two.standing, Is.EqualTo(FinalBracket.Standing.Playing));
            Assert.That(two.faces.Select(f => f.id), Is.EquivalentTo(new[] { s.playerId, Other(s, 1).id }), "The other two play Part 2.");
            Assert.That(two.line.Split(new[] { " vs " }, System.StringSplitOptions.None), Is.EquivalentTo(new[] { "You", First(Other(s, 1)) }));

            var three = bracket.parts[2];
            Assert.That(three.standing, Is.EqualTo(FinalBracket.Standing.Ahead));
            Assert.That(three.openSeat, Is.True);
            Assert.That(three.line, Is.EqualTo(First(winner) + " vs ?"));
            Assert.That(three.faces.Single().id, Is.EqualTo(winner.id));
        }

        [Test]
        public void ThePlayerIsYouWhereverTheyStandInIt()
        {
            var s = FinalThree();
            var other = s.Active.First(c => !c.isPlayer);
            other.name = "Dr. Will Kirby";
            s.finalPart1WinnerId = s.playerId;
            s.phase = EpisodePhase.FinalHoHPart2;
            var bracket = FinalBracket.For(s);
            Assert.That(bracket.parts[0].line, Is.EqualTo("Winner: You"));
            Assert.That(bracket.parts[2].line, Is.EqualTo("You vs ?"));
            Assert.That(bracket.parts[1].faces.Any(f => f.isPlayer), Is.False, "Part 1's winner sits Part 2 out.");
            Assert.That(bracket.parts[1].line, Does.Contain("Will").And.Not.Contain("Dr."), "A title is not a first name.");
        }

        [Test]
        public void PartThreeIsBothWinnersForTheFinalHeadOfHousehold()
        {
            var s = FinalThree();
            var first = Other(s, 0); var second = Other(s, 1);
            s.finalPart1WinnerId = first.id;
            s.finalPart2WinnerId = second.id;
            s.phase = EpisodePhase.FinalHoHPart3;
            var bracket = FinalBracket.For(s);
            Assert.That(bracket.current, Is.EqualTo(3));
            Assert.That(bracket.advance, Is.EqualTo("Winner becomes the final Head of Household"));
            Assert.That(bracket.parts[0].line, Is.EqualTo("Winner: " + First(first)));
            Assert.That(bracket.parts[1].line, Is.EqualTo("Winner: " + First(second)));
            Assert.That(bracket.parts[1].standing, Is.EqualTo(FinalBracket.Standing.Played));
            var three = bracket.parts[2];
            Assert.That(three.standing, Is.EqualTo(FinalBracket.Standing.Playing));
            Assert.That(three.faces.Select(f => f.id), Is.EquivalentTo(new[] { first.id, second.id }));
            Assert.That(three.faces.Any(f => f.isPlayer), Is.False, "The player lost Part 2 and watches.");

            // Resolved, Part 3 names the final Head of Household.
            s.competitionResolved = true;
            s.hohId = second.id;
            bracket = FinalBracket.For(s);
            Assert.That(bracket.parts[2].standing, Is.EqualTo(FinalBracket.Standing.Played));
            Assert.That(bracket.parts[2].line, Is.EqualTo("Winner: " + First(second)));
        }

        [Test]
        public void ItNeverPrintsAScoreAndChangesNothing()
        {
            var s = FinalThree();
            var first = Other(s, 0);
            s.finalPart1WinnerId = first.id;
            s.phase = EpisodePhase.FinalHoHPart2;
            // Numbers no line could print by accident: the last part's committed scores, planted.
            s.competitionScores.Add(new CompetitionScore { contestantId = first.id, score = 8.73 });
            s.competitionScores.Add(new CompetitionScore { contestantId = s.playerId, score = 6.41 });
            string before = JsonConvert.SerializeObject(s);
            uint random = s.randomState;
            var bracket = FinalBracket.For(s);
            string all = string.Join("\n", bracket.parts.Select(p => p.line)) + "\n" + bracket.advance;
            foreach (var number in new[] { "8.73", "6.41", "873", "641" })
                Assert.That(all, Does.Not.Contain(number), "A score in the bracket: " + number);
            Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(before), "Reading the bracket must not change the state.");
            Assert.That(s.randomState, Is.EqualTo(random), "or draw from its generator.");
            Assert.That(FinalBracket.ScoringLine, Is.EqualTo("Highest score wins · statistics and seeded rolls count"));
        }
    }
}
