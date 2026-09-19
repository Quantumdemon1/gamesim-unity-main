using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// What each competition's minigame is worth.
    ///
    /// <para>Every number below is the reference's, and they are pinned here rather than left to the
    /// screen because a scoring rule tested through a coroutine is a scoring rule nobody can check.
    /// </para>
    /// </summary>
    public sealed class CompetitionMiniGameTests
    {
        // ---------------------------------------------------------------- routing

        [Test]
        public void EveryCategoryTheEngineProducesHasItsOwnGame()
        {
            Assert.That(CompetitionMiniGames.For("Endurance"), Is.EqualTo(CompetitionMiniGames.Kind.Endurance));
            Assert.That(CompetitionMiniGames.For("Skill"), Is.EqualTo(CompetitionMiniGames.Kind.Reaction));
            Assert.That(CompetitionMiniGames.For("Mental"), Is.EqualTo(CompetitionMiniGames.Kind.Memory));
        }

        /// <summary>
        /// The two categories <c>WebRules</c> weights and the engine never asks for fall back to the
        /// timing bar rather than to a screen that does not exist.
        /// </summary>
        [Test]
        public void ACategoryWithNoGameOfItsOwnStillPlaysSomething()
        {
            Assert.That(CompetitionMiniGames.For("Physical"), Is.EqualTo(CompetitionMiniGames.Kind.Precision));
            Assert.That(CompetitionMiniGames.For("Crapshoot"), Is.EqualTo(CompetitionMiniGames.Kind.Precision));
            Assert.That(CompetitionMiniGames.For(null), Is.EqualTo(CompetitionMiniGames.Kind.Precision));
            Assert.That(CompetitionMiniGames.For("Nonsense"), Is.EqualTo(CompetitionMiniGames.Kind.Precision));
        }

        /// <summary>
        /// The routing is driven by what the engine actually produces, so this walks a season's
        /// worth of phases and weeks rather than trusting the three strings above.
        /// </summary>
        [Test]
        public void EveryCompetitionASeasonCanReachRoutesToAGameWithRules()
        {
            var phases = new[]
            {
                EpisodePhase.HoH, EpisodePhase.Veto,
                EpisodePhase.FinalHoHPart1, EpisodePhase.FinalHoHPart2, EpisodePhase.FinalHoHPart3,
            };
            foreach (var phase in phases)
                for (int week = 1; week <= 12; week++)
                {
                    string category = EpisodeEngine.CompetitionCategory(phase, week);
                    var kind = CompetitionMiniGames.For(category);
                    Assert.That(kind, Is.Not.EqualTo(CompetitionMiniGames.Kind.Precision),
                        phase + " week " + week + " is '" + category + "', which has no game of its own.");
                    Assert.That(CompetitionMiniGames.TimeLimit(kind), Is.GreaterThan(0), category);
                    Assert.That(CompetitionMiniGames.Brief(kind), Does.StartWith("HOUSE SIGNALS"));
                    Assert.That(CompetitionMiniGames.EnterCaption(kind), Is.Not.Empty);
                }
        }

        [Test]
        public void TheTimingBarHasNoClockBecauseItCountsAttempts()
        {
            Assert.That(CompetitionMiniGames.TimeLimit(CompetitionMiniGames.Kind.Precision), Is.Zero);
            Assert.That(CompetitionMiniGames.TimeLimit(CompetitionMiniGames.Kind.Reaction), Is.EqualTo(20));
            Assert.That(CompetitionMiniGames.TimeLimit(CompetitionMiniGames.Kind.Endurance), Is.EqualTo(30));
            Assert.That(CompetitionMiniGames.TimeLimit(CompetitionMiniGames.Kind.Memory), Is.EqualTo(30));
        }

        // ---------------------------------------------------------------- the command's scale

        [Test]
        public void AScoreOutOfTenBecomesThePerformanceTheCommandTakes()
        {
            Assert.That(CompetitionMiniGames.Performance(10), Is.EqualTo(1).Within(1e-9));
            Assert.That(CompetitionMiniGames.Performance(5), Is.EqualTo(0.5).Within(1e-9));
            Assert.That(CompetitionMiniGames.Performance(0), Is.Zero);
            // The engine rejects anything outside 0–1 outright, so the clamp is not decoration.
            Assert.That(CompetitionMiniGames.Performance(11), Is.EqualTo(1));
            Assert.That(CompetitionMiniGames.Performance(-3), Is.Zero);
        }

        // ---------------------------------------------------------------- endurance

        [Test]
        public void HoldingIsWorthTheShareOfTheClockYouSpentHolding()
        {
            Assert.That(CompetitionMiniGames.EnduranceScore(30, 30), Is.EqualTo(10));
            Assert.That(CompetitionMiniGames.EnduranceScore(15, 30), Is.EqualTo(5));
            Assert.That(CompetitionMiniGames.EnduranceScore(0, 30), Is.Zero);
            Assert.That(CompetitionMiniGames.EnduranceScore(45, 30), Is.EqualTo(10), "Never above ten.");
            Assert.That(CompetitionMiniGames.EnduranceScore(-1, 30), Is.Zero);
            Assert.That(CompetitionMiniGames.EnduranceScore(10, 0), Is.Zero, "No clock, no score.");
        }

        /// <summary>Letting go to save the grip costs score. That is the whole tension.</summary>
        [Test]
        public void SurvivingIsNotTheSameAsHolding()
        {
            Assert.That(CompetitionMiniGames.EnduranceScore(30, 30),
                Is.GreaterThan(CompetitionMiniGames.EnduranceScore(20, 30)));
        }

        [Test]
        public void TheGripRefillsWhileHeldAndDrainsWhenLetGo()
        {
            double held = CompetitionMiniGames.MeterAfter(50, 0, 30, 1, holding: true);
            double loose = CompetitionMiniGames.MeterAfter(50, 0, 30, 1, holding: false);
            Assert.That(held, Is.EqualTo(90).Within(1e-9), "Forty a second at the start.");
            Assert.That(loose, Is.EqualTo(20).Within(1e-9), "Thirty a second at the start.");
        }

        /// <summary>
        /// The reference's stamina curve: the fill decays from 40 to 15 and the drain climbs from 30
        /// to 40, so a grip that works early does not work late.
        /// </summary>
        [Test]
        public void HoldingOnGetsHarderAsTheClockRunsDown()
        {
            double early = CompetitionMiniGames.MeterAfter(0, 0, 30, 1, holding: true);
            double late = CompetitionMiniGames.MeterAfter(0, 30, 30, 1, holding: true);
            Assert.That(early, Is.EqualTo(40).Within(1e-9));
            Assert.That(late, Is.EqualTo(15).Within(1e-9));

            double drainEarly = 100 - CompetitionMiniGames.MeterAfter(100, 0, 30, 1, holding: false);
            double drainLate = 100 - CompetitionMiniGames.MeterAfter(100, 30, 30, 1, holding: false);
            Assert.That(drainEarly, Is.EqualTo(30).Within(1e-9));
            Assert.That(drainLate, Is.EqualTo(40).Within(1e-9));
        }

        [Test]
        public void TheGripNeverGoesAboveFullOrBelowEmpty()
        {
            Assert.That(CompetitionMiniGames.MeterAfter(99, 0, 30, 5, holding: true),
                Is.EqualTo(CompetitionMiniGames.MeterFull));
            Assert.That(CompetitionMiniGames.MeterAfter(1, 0, 30, 5, holding: false),
                Is.EqualTo(CompetitionMiniGames.MeterEmpty));
            Assert.That(CompetitionMiniGames.MeterAfter(50, 0, 30, 0, holding: true), Is.EqualTo(50),
                "A frame with no time in it changes nothing.");
            Assert.That(CompetitionMiniGames.MeterAfter(50, 0, 0, 1, holding: true), Is.EqualTo(50));
        }

        // ---------------------------------------------------------------- reaction

        [Test]
        public void TappingIsWorthTheShareOfTargetsYouHit()
        {
            Assert.That(CompetitionMiniGames.ReactionScore(10, 10), Is.EqualTo(10));
            Assert.That(CompetitionMiniGames.ReactionScore(5, 10), Is.EqualTo(5));
            Assert.That(CompetitionMiniGames.ReactionScore(0, 10), Is.Zero);
            Assert.That(CompetitionMiniGames.ReactionScore(0, 0), Is.Zero, "Nothing spawned, nothing scored.");
            Assert.That(CompetitionMiniGames.ReactionScore(12, 10), Is.EqualTo(10), "Never above ten.");
        }

        /// <summary>A target you let expire counts against you as much as one you miss.</summary>
        [Test]
        public void LettingATargetExpireCostsAsMuchAsMissingIt()
        {
            Assert.That(CompetitionMiniGames.ReactionScore(8, 10),
                Is.EqualTo(CompetitionMiniGames.ReactionScore(8, 10)));
            Assert.That(CompetitionMiniGames.ReactionScore(8, 12),
                Is.LessThan(CompetitionMiniGames.ReactionScore(8, 10)),
                "Two more targets appearing and going unhit has to cost something.");
        }

        // ---------------------------------------------------------------- memory

        [Test]
        public void AnUnfinishedBoardScoresWhatYouMatchedAndNeverBelowOne()
        {
            Assert.That(CompetitionMiniGames.MemoryScore(4, 8, 0, 0, 30), Is.EqualTo(5));
            Assert.That(CompetitionMiniGames.MemoryScore(0, 8, 0, 0, 30), Is.EqualTo(1),
                "A blank board is still worth one.");
            Assert.That(CompetitionMiniGames.MemoryScore(1, 8, 40, 0, 30), Is.EqualTo(1),
                "The floor holds even under the full wrong-flip penalty.");
        }

        /// <summary>
        /// Clearing the board quickly is the only route to full marks, and the time bonus is what
        /// makes finishing worth chasing.
        /// </summary>
        [Test]
        public void ClearingTheBoardFastIsTheOnlyRouteToTen()
        {
            Assert.That(CompetitionMiniGames.MemoryScore(8, 8, 0, 30, 30), Is.EqualTo(10),
                "The whole clock left is the full two-point bonus.");
            Assert.That(CompetitionMiniGames.MemoryScore(8, 8, 0, 15, 30), Is.EqualTo(9));
            Assert.That(CompetitionMiniGames.MemoryScore(8, 8, 0, 0, 30), Is.EqualTo(8),
                "A cleared board starts at eight and earns the rest from the clock.");

            Assert.That(CompetitionMiniGames.MemoryScore(8, 8, 0, 20, 30),
                Is.GreaterThan(CompetitionMiniGames.MemoryScore(7, 8, 0, 20, 30)),
                "Finishing with time in hand beats not finishing.");
        }

        /// <summary>
        /// The two formulas cross over, and that is the reference's own shape rather than a slip in
        /// porting it.
        ///
        /// <para>Seven of eight pairs scores 8.75 — the fraction out of ten. A board <i>cleared</i>
        /// on the buzzer scores 8, because a cleared board starts at eight and earns the rest from
        /// the clock. So matching almost everything quickly is worth marginally more than finishing
        /// at the last moment. It is pinned here because it looks like a bug the first time anybody
        /// sees it, and changing it would be a rules change rather than a port.</para>
        /// </summary>
        [Test]
        public void MatchingAlmostEverythingBeatsClearingItOnTheBuzzer()
        {
            Assert.That(CompetitionMiniGames.MemoryScore(7, 8, 0, 0, 30), Is.EqualTo(8.75));
            Assert.That(CompetitionMiniGames.MemoryScore(8, 8, 0, 0, 30), Is.EqualTo(8));
            Assert.That(CompetitionMiniGames.MemoryScore(7, 8, 0, 0, 30),
                Is.GreaterThan(CompetitionMiniGames.MemoryScore(8, 8, 0, 0, 30)));

            // The crossover closes as soon as there is any clock left to bank.
            Assert.That(CompetitionMiniGames.MemoryScore(8, 8, 0, 12, 30),
                Is.GreaterThan(CompetitionMiniGames.MemoryScore(7, 8, 0, 12, 30)));
        }

        [Test]
        public void AWrongFlipCostsALittleAndTheCostIsCapped()
        {
            Assert.That(CompetitionMiniGames.MemoryScore(8, 8, 1, 0, 30), Is.EqualTo(7.85).Within(1e-9));
            Assert.That(CompetitionMiniGames.MemoryScore(8, 8, 10, 0, 30), Is.EqualTo(6.5).Within(1e-9));
            // Capped at five, and a cleared board has its own floor of three under that.
            Assert.That(CompetitionMiniGames.MemoryScore(8, 8, 1000, 0, 30), Is.EqualTo(3));
            Assert.That(CompetitionMiniGames.MemoryScore(8, 8, -5, 0, 30), Is.EqualTo(8),
                "A negative flip count is not a bonus.");
        }

        [Test]
        public void AMemoryBoardWithNoPairsScoresNothing()
        {
            Assert.That(CompetitionMiniGames.MemoryScore(0, 0, 0, 30, 30), Is.Zero);
            Assert.That(CompetitionMiniGames.MemoryScore(20, 8, 0, 0, 30), Is.EqualTo(8),
                "More matches than pairs is still just a cleared board.");
        }

        // ---------------------------------------------------------------- the whole range

        /// <summary>
        /// Whatever a player does, the number handed to the engine is one the engine will take. The
        /// command rejects anything outside 0–1 outright, so this is the boundary that matters.
        /// </summary>
        [Test]
        public void NoGameCanProduceAPerformanceTheEngineWouldRefuse()
        {
            for (int held = -5; held <= 40; held += 5)
                Assert.That(CompetitionMiniGames.Performance(
                    CompetitionMiniGames.EnduranceScore(held, 30)), Is.InRange(0d, 1d));

            for (int hits = 0; hits <= 12; hits++)
                for (int spawned = 0; spawned <= 12; spawned++)
                    Assert.That(CompetitionMiniGames.Performance(
                        CompetitionMiniGames.ReactionScore(hits, spawned)), Is.InRange(0d, 1d));

            for (int matched = 0; matched <= 8; matched++)
                for (int wrong = 0; wrong <= 40; wrong += 8)
                    for (int left = 0; left <= 30; left += 10)
                        Assert.That(CompetitionMiniGames.Performance(
                            CompetitionMiniGames.MemoryScore(matched, 8, wrong, left, 30)),
                            Is.InRange(0d, 1d));
        }
    }
}
