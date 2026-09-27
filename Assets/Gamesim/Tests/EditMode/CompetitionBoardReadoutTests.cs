using System;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The numbers the redrawn boards read off a run - a target's window left, the grip's rate and
    /// time to empty, the effort that earns full marks - are the run's own, and reading them changes
    /// nothing about the run.
    /// </summary>
    public sealed class CompetitionBoardReadoutTests
    {
        [Test]
        public void TargetRemainingIsTheWindowLessTheTimeItHasBeenUp()
        {
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 17, 3, CompetitionDefinitions.SwitchbackSignals);
            Assert.That(run.TargetRemaining, Is.Zero, "No target, no window.");
            while (!run.TargetLive) run.Tick(.01);
            double window = run.TargetWindowSeconds, spawnedAt = run.Elapsed;
            run.Tick(.3);
            Assert.That(run.TargetRemaining, Is.EqualTo(window - (run.Elapsed - spawnedAt)).Within(1e-6));
            Assert.That(run.TargetRemaining, Is.GreaterThan(0).And.LessThan(window));
        }

        [Test]
        public void ReadingTheBoardNeverChangesWhereTargetsLand()
        {
            var read = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 23, 3, CompetitionDefinitions.SignalSprint);
            var blind = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 23, 3, CompetitionDefinitions.SignalSprint);
            while (!read.Finished)
            {
                _ = read.TargetRemaining; _ = read.GripRate; _ = read.SecondsToEmpty;
                read.Tick(.02); blind.Tick(.02);
                Assert.That(read.TargetX, Is.EqualTo(blind.TargetX));
                Assert.That(read.TargetY, Is.EqualTo(blind.TargetY));
                Assert.That(read.Spawned, Is.EqualTo(blind.Spawned));
            }
        }

        [Test]
        public void TheRateTheBoardShowsIsTheRateTheMeterMoves()
        {
            foreach (int rules in new[] { 1, 2, 3 })
            foreach (bool holding in new[] { true, false })
            foreach (double elapsed in new[] { 0.0, 7.5, 19.0, 29.0 })
            {
                const double limit = 30, meter = 50, step = 1e-3;
                double after = CompetitionMiniGames.MeterAfter(meter, elapsed, limit, step, holding, rules);
                double rate = CompetitionMiniGames.MeterRatePerSecond(elapsed, limit, holding, 1, rules);
                Assert.That((after - meter) / step, Is.EqualTo(rate).Within(1e-6),
                    "rules " + rules + ", holding " + holding + ", at " + elapsed + " s");
            }
            // Inside a pressure wave, the rate the run reports is the rate its meter really moves.
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Endurance, 3, 3, CompetitionDefinitions.PressureCooker);
            run.SetHolding(true); run.Tick(5);
            Assert.That(run.GripPressure, Is.GreaterThan(1), "Five seconds in is inside the first wave.");
            double reported = run.GripRate, before = run.Meter;
            run.Tick(1e-3);
            Assert.That((run.Meter - before) / 1e-3, Is.EqualTo(reported).Within(1e-2), "The wave's drain, as the run steps it.");
            Assert.That(CompetitionMiniGames.MeterRatePerSecond(5, 30, false, 1.7, 3), Is.EqualTo(28), "Recovery is not a wave's business.");
        }

        [Test]
        public void SecondsToEmptyIsTheGripOverItsFall()
        {
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Endurance, 5, 3, CompetitionDefinitions.HoldYourGround);
            Assert.That(double.IsPositiveInfinity(run.SecondsToEmpty), Is.True, "Recovering, the grip is not falling.");
            run.SetHolding(true); run.Tick(2);
            Assert.That(run.GripRate, Is.LessThan(0));
            Assert.That(run.SecondsToEmpty, Is.EqualTo(run.Meter / -run.GripRate).Within(1e-9));
        }

        [Test]
        public void FullMarksNeedSixtyFivePercentOfTheClockFromVersionTwo()
        {
            Assert.That(CompetitionMiniGames.EnduranceTarget(30, 3), Is.EqualTo(19.5).Within(1e-9));
            Assert.That(CompetitionMiniGames.EnduranceTarget(30, 2), Is.EqualTo(19.5).Within(1e-9));
            Assert.That(CompetitionMiniGames.EnduranceTarget(30, 1), Is.EqualTo(30));
            Assert.That(CompetitionMiniGames.EnduranceScore(19.5, 30, 3), Is.EqualTo(10));
            Assert.That(CompetitionMiniGames.EnduranceScore(9.75, 30, 3), Is.EqualTo(5));
        }

        [Test]
        public void TheAttemptLineSaysTheGamesOwnMeasure()
        {
            var memory = new MiniGameRun(CompetitionMiniGames.Kind.Memory, 9, 3);
            Assert.That(CompetitionGameScreen.AttemptLine(memory), Is.Null, "Nothing to say before the attempt ends.");
            memory.Finish();
            Assert.That(CompetitionGameScreen.AttemptLine(memory), Does.Contain("0 / 8 pairs").And.Contain("performance "));
            var endurance = new MiniGameRun(CompetitionMiniGames.Kind.Endurance, 9, 3);
            endurance.SetHolding(true); endurance.Tick(4); endurance.Finish();
            Assert.That(CompetitionGameScreen.AttemptLine(endurance), Does.Contain("held " + endurance.Held.ToString("0.0") + " s"));
        }
    }
}
