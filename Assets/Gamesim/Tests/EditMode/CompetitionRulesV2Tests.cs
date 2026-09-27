using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class CompetitionRulesV2Tests
    {
        [Test]
        public void RecoveryBeatsHoldingForeverAndNoEffortEarnsNothing()
        {
            var held=Run(CompetitionMiniGames.Kind.Endurance);
            held.SetHolding(true);held.Tick(30);
            Assert.That(held.Finished,Is.True);
            Assert.That(held.Elapsed,Is.LessThan(10));
            var balanced=Run(CompetitionMiniGames.Kind.Endurance);
            for(int i=0;i<4000&&!balanced.Finished;i++)
            {
                if(balanced.Meter<25)balanced.SetHolding(false);
                else if(balanced.Meter>80)balanced.SetHolding(true);
                balanced.Tick(.01);
            }
            Assert.That(balanced.Finished,Is.True);
            Assert.That(balanced.Score,Is.GreaterThan(held.Score+3));
            Assert.That(balanced.Score,Is.InRange(0,10));
            var idle=Run(CompetitionMiniGames.Kind.Endurance);idle.Tick(30);
            Assert.That(idle.Finished,Is.True);Assert.That(idle.Score,Is.Zero);
        }

        [Test]
        public void LongFramesCannotAwardTimeBeyondDeadlineOrSkipExhaustion()
        {
            var run=Run(CompetitionMiniGames.Kind.Endurance);run.SetHolding(true);run.Tick(1000);
            Assert.That(run.Held,Is.LessThan(10));Assert.That(run.Score,Is.LessThan(6));
            var idle=Run(CompetitionMiniGames.Kind.Memory);idle.Tick(1000);
            Assert.That(idle.Elapsed,Is.EqualTo(idle.TimeLimit).Within(.000001));
            Assert.That(idle.Finished,Is.True);
        }

        [Test]
        public void EveryExtraPairImprovesScoreEvenAtTheDeadline()
        {
            for(int mistakes=0;mistakes<50;mistakes++)
            for(int pairs=0;pairs<8;pairs++)
            {
                var before=CompetitionMiniGames.MemoryScore(pairs,8,mistakes,0,30,2);
                var after=CompetitionMiniGames.MemoryScore(pairs+1,8,mistakes,0,30,2);
                Assert.That(after,Is.GreaterThanOrEqualTo(before));
            }
            Assert.That(CompetitionMiniGames.MemoryScore(8,8,0,0,30,2),Is.EqualTo(9));
            Assert.That(CompetitionMiniGames.MemoryScore(7,8,0,0,30,2),Is.LessThan(9));
            Assert.That(CompetitionMiniGames.MemoryScore(8,8,0,0,30),Is.EqualTo(8),"Legacy crossover remains versioned.");
        }

        [Test]
        public void ReactionNeedsTheCorrectDirectionAndPenalizesEarlyInputOnce()
        {
            var run=Run(CompetitionMiniGames.Kind.Reaction);
            Assert.That(run.Tap(MiniGameRun.Direction.Up),Is.False);
            Assert.That(run.FalseStarts,Is.EqualTo(1));
            while(!run.TargetLive)run.Tick(.01);
            Assert.That(run.Tap(),Is.False,"Space cannot bypass aiming under the new rules.");
            var wrong=(MiniGameRun.Direction)(((int)run.TargetDirection+1)%4);
            Assert.That(run.Tap(wrong),Is.False);Assert.That(run.WrongDirections,Is.EqualTo(1));
            Assert.That(run.TargetLive,Is.False);Assert.That(run.Spawned,Is.EqualTo(1));
            while(!run.TargetLive)run.Tick(.01);
            Assert.That(run.Tap(run.TargetDirection),Is.True);
            run.Finish();
            Assert.That(run.Score,Is.EqualTo(3.33),"One hit / (two targets + one early input).");
        }

        [Test]
        public void AttemptSeedIsStableAndPracticeCannotChangeTheRankedBoard()
        {
            uint ranked=CompetitionMiniGames.AttemptSeed(123,2,(int)EpisodePhase.Veto,2,false);
            var first=new MiniGameRun(CompetitionMiniGames.Kind.Memory,ranked,2);
            var practice=new MiniGameRun(CompetitionMiniGames.Kind.Memory,
                CompetitionMiniGames.AttemptSeed(123,2,(int)EpisodePhase.Veto,2,true),2);
            practice.Flip(0);practice.Tick(3);
            var reloaded=new MiniGameRun(CompetitionMiniGames.Kind.Memory,
                CompetitionMiniGames.AttemptSeed(123,2,(int)EpisodePhase.Veto,2,false),2);
            CollectionAssert.AreEqual(first.Faces.ToArray(),reloaded.Faces.ToArray());
            Assert.That(practice.Faces.SequenceEqual(first.Faces),Is.False);
        }

        [Test]
        public void NewWeeklyVetoDiffersFromHohWhileLegacyAndFinalsStayTheSame()
        {
            for(int week=1;week<=12;week++)
            {
                Assert.That(EpisodeEngine.CompetitionCategory(EpisodePhase.Veto,week,2),
                    Is.Not.EqualTo(EpisodeEngine.CompetitionCategory(EpisodePhase.HoH,week,2)));
                Assert.That(EpisodeEngine.CompetitionCategory(EpisodePhase.Veto,week,1),
                    Is.EqualTo(EpisodeEngine.CompetitionCategory(EpisodePhase.HoH,week,1)));
            }
            Assert.That(EpisodeEngine.CompetitionCategory(EpisodePhase.FinalHoHPart1,1,2),Is.EqualTo("Endurance"));
            Assert.That(EpisodeEngine.CompetitionCategory(EpisodePhase.FinalHoHPart2,1,2),Is.EqualTo("Skill"));
            Assert.That(EpisodeEngine.CompetitionCategory(EpisodePhase.FinalHoHPart3,1,2),Is.EqualTo("Mental"));
        }

        private static MiniGameRun Run(CompetitionMiniGames.Kind kind)=>new MiniGameRun(kind,77,CompetitionMiniGames.ImprovedRules);
    }
}
