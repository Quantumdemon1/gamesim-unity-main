using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Free talk slice 2 (talk rules 2, ruling T4): the lean of the player's tones (story.talkLean), the player's class it moves
    /// and the house's read of them. Simulation only.
    /// </summary>
    public sealed class FreeTalkLeanTests
    {
        [TestCase(TalkRules.Tease, 1)]
        [TestCase(TalkRules.Push, 1)]
        [TestCase(TalkRules.Careful, -1)]
        [TestCase(TalkRules.LetGo, -1)]
        [TestCase(TalkRules.Warm, 0)]
        [TestCase(TalkRules.Straight, 0)]
        [TestCase(TalkRules.CallOut, 0)]
        public void EachToneStepsTheLeanByItsMap(string tone, int step)
        {
            Assert.That(TalkLean.Step(tone), Is.EqualTo(step));
        }

        [Test]
        public void TheLeanIsClampedToSixEitherWay()
        {
            Assert.That(TalkLean.After(6, TalkRules.Push), Is.EqualTo(6));
            Assert.That(TalkLean.After(-6, TalkRules.LetGo), Is.EqualTo(-6));
            Assert.That(TalkLean.After(5, TalkRules.Tease), Is.EqualTo(6));
        }

        [TestCase(TalkPools.Guarded, 4, TalkPools.Steady)]
        [TestCase(TalkPools.Guarded, 6, TalkPools.Steady)]
        [TestCase(TalkPools.Steady, 4, TalkPools.Spark)]
        [TestCase(TalkPools.Steady, 3, TalkPools.Steady)]
        [TestCase(TalkPools.Spark, 6, TalkPools.Spark)]
        [TestCase(TalkPools.Spark, -4, TalkPools.Steady)]
        [TestCase(TalkPools.Spark, -6, TalkPools.Steady)]
        [TestCase(TalkPools.Steady, -4, TalkPools.Guarded)]
        [TestCase(TalkPools.Steady, -3, TalkPools.Steady)]
        [TestCase(TalkPools.Guarded, -6, TalkPools.Guarded)]
        public void TheLeanMovesThePlayerOneClassAtFourAndNeverMore(string start, int lean, string expected)
        {
            Assert.That(TalkLean.ClassAt(start, lean), Is.EqualTo(expected));
        }

        [Test]
        public void TheHouseReadsYouAsTheWordOfYourClass()
        {
            Assert.That(TalkLean.LineFor(TalkPools.Spark), Is.EqualTo("The house reads you as a flirt."));
            Assert.That(TalkLean.LineFor(TalkPools.Steady), Is.EqualTo("The house reads you as easy to talk to."));
            Assert.That(TalkLean.LineFor(TalkPools.Guarded), Is.EqualTo("The house reads you as hard to read."));
            var s = FreeTalkFixture.Fresh(TalkRules.Chemistry);
            s.Find(s.playerId).traits = new System.Collections.Generic.List<string> { "Loyal", "Competitive" };
            Assert.That(TalkNotes.HouseReadsYou(s), Is.EqualTo("The house reads you as easy to talk to."));
            s.story.talkLean = 4;
            Assert.That(TalkNotes.HouseReadsYou(s), Is.EqualTo("The house reads you as a flirt."));
            s.story.talkRules = TalkRules.Talk;
            Assert.That(TalkNotes.HouseReadsYou(s), Is.Null, "Talk rules 1 shows no read.");
            Assert.That(TalkLean.PlayerClass(s), Is.Null);
        }

        [Test]
        public void ACountedTalkMovesTheLeanAtTalkRulesTwoAndNeverAtOne()
        {
            foreach (int level in new[] { TalkRules.Talk, TalkRules.Chemistry })
                using (TalkBank.Use(FreeTalkFixture.Table()))
                {
                    var engine = new EpisodeEngine(FreeTalkFixture.Fresh(level));
                    var npcs = FreeTalkFixture.Npcs(engine.Snapshot);
                    Assert.That(FreeTalkFixture.Talk(engine, npcs[0].id, TalkRules.Tease).accepted, Is.True);
                    Assert.That(FreeTalkFixture.Talk(engine, npcs[1].id, TalkRules.Push).accepted, Is.True);
                    Assert.That(FreeTalkFixture.Talk(engine, npcs[2].id, TalkRules.Warm).accepted, Is.True);
                    Assert.That(engine.Snapshot.story.talkLean, Is.EqualTo(level == TalkRules.Chemistry ? 2 : 0), "Talk rules " + level);
                    // A refused talk (the cap) moves nothing.
                    Assert.That(FreeTalkFixture.Talk(engine, npcs[0].id, TalkRules.Tease).accepted, Is.False);
                    Assert.That(engine.Snapshot.story.talkLean, Is.EqualTo(level == TalkRules.Chemistry ? 2 : 0));
                    Assert.That(FreeTalkFixture.Talk(engine, npcs[3].id, TalkRules.LetGo).accepted, Is.True);
                    Assert.That(engine.Snapshot.story.talkLean, Is.EqualTo(level == TalkRules.Chemistry ? 1 : 0));
                }
        }
    }
}
