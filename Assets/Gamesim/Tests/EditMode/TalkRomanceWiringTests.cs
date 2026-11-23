using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Free talk slice 3's wiring (talk rules 3): the shipped romance tables against what the engine reads of them, and the
    /// screen's Flirt button against the engine's own offer. The tables were written to the contract and the engine filled
    /// in after it, so these read each through the other: every shipped romance row is one the engine will say
    /// (<see cref="TalkRomance.WellFormed"/>), every class pool and every All-Stars table holds the kinds the engine needs
    /// (else that housemate plays nothing romantic), every All-Stars template has its table and its stance, and the screen
    /// never draws a Flirt the engine would refuse.
    /// </summary>
    public sealed class TalkRomanceWiringTests
    {
        private static IEnumerable<TalkRow> RomanceRows => TalkTables.All.Where(r => r != null && TalkRomance.IsRomanceKind(r.Kind));

        [Test]
        public void EveryShippedRomanceRowIsOneTheEngineSays()
        {
            var bad = RomanceRows.Where(r => !TalkRomance.WellFormed(r)).Select(r => r.Id).ToList();
            Assert.That(RomanceRows.Count(), Is.GreaterThan(0), "The control: the romance tables are registered.");
            Assert.That(bad, Is.Empty, "Romance rows the engine would never say.");
        }

        private static readonly (TalkKind kind, int least)[] PoolNeeds =
        {
            (TalkKind.FlirtLine, 3), (TalkKind.Opening, 2), (TalkKind.SoftNo, 2), (TalkKind.LineDrawn, 2), (TalkKind.Declaration, 1),
            (TalkKind.Accepts, 2), (TalkKind.Refuses, 2), (TalkKind.Couple, 2), (TalkKind.Ex, 2), (TalkKind.Jealousy, 2),
        };

        private static readonly (TalkKind kind, int least)[] StarNeeds =
        {
            (TalkKind.FlirtLine, 2), (TalkKind.HoldBack, 1), (TalkKind.Opening, 1), (TalkKind.Declaration, 1), (TalkKind.Accepts, 1),
            (TalkKind.Refuses, 1), (TalkKind.Couple, 1), (TalkKind.Ex, 1), (TalkKind.Jealousy, 1), (TalkKind.SoftNo, 1),
        };

        [Test]
        public void EveryClassPoolAndEveryAllStarsTableHoldsWhatTheEngineReads()
        {
            var missing = new List<string>();
            var rows = RomanceRows.Where(TalkRomance.WellFormed).ToList();
            foreach (string pool in new[] { TalkPools.Spark, TalkPools.Steady, TalkPools.Guarded })
            {
                var mine = rows.Where(r => r.Owner == null && r.Pool == pool).ToList();
                foreach (var (kind, least) in PoolNeeds)
                    if (mine.Count(r => r.Kind == kind) < least) missing.Add(pool + " " + kind);
                foreach (int reaction in new[] { 1, 2 })
                    if (mine.Count(r => r.Kind == TalkKind.Reaction && r.Reaction == reaction) < 2) missing.Add(pool + " reaction " + reaction);
                if (mine.Any(r => r.Kind == TalkKind.FlirtLine && (r.When == null || r.When.Spark == 0)) == false) missing.Add(pool + " a FlirtLine open at spark 0");
            }
            var templates = CastTemplates.In(CastTemplates.Roster.AllStars).Select(t => t.Id).ToList();
            Assert.That(templates.Count, Is.EqualTo(12), "The control: the twelve All-Stars templates.");
            foreach (string id in templates)
            {
                var mine = rows.Where(r => r.Owner == id).ToList();
                Assert.That(rows.Where(r => r.Owner == id).All(r => r.Pool == null), Is.True, id + ": an All-Star's rows belong to no pool.");
                foreach (var (kind, least) in StarNeeds)
                    if (mine.Count(r => r.Kind == kind) < least) missing.Add(id + " " + kind);
                foreach (int attempt in new[] { 1, 2, 3 })
                    if (!mine.Any(r => r.Kind == TalkKind.TurnAside && r.Attempt == attempt)) missing.Add(id + " turn-aside " + attempt);
                foreach (int reaction in new[] { 1, 2 })
                    if (!mine.Any(r => r.Kind == TalkKind.Reaction && r.Reaction == reaction)) missing.Add(id + " reaction " + reaction);
                if (!mine.Any(r => r.Kind == TalkKind.FlirtLine && (r.When == null || r.When.Spark == 0))) missing.Add(id + " a FlirtLine open at spark 0");
                Assert.That(TalkStanceTable.ByTemplate.ContainsKey(id), Is.True, id + ": a stance in the table.");
            }
            Assert.That(TalkStanceTable.ByTemplate.Keys, Is.EquivalentTo(templates), "The stance table holds the twelve templates and nothing else.");
            Assert.That(missing, Is.Empty, "Kinds the engine needs and the tables lack.");
        }

        [Test]
        public void TheFlirtButtonIsDrawnExactlyWhereTheEngineTakesAFlirt()
        {
            using (TalkBank.Use(FreeTalkS3Fixture.Table()))
            {
                var s = FreeTalkS3Fixture.WithInterested(TalkPools.Spark, TalkPools.Spark, out string npc);
                FreeTalkS3Fixture.Flirting(s, npc, spark: 5, rapport: 30);
                Assert.That(TalkRomance.FlirtOpen(s, npc), Is.True, "The control: Flirt is open with them.");
                var stage = new TalkRow
                {
                    Id = "spark.wire.stage", Pool = TalkPools.Spark, Kind = TalkKind.Stage, Stage = FriendStage.Friendly,
                    Open = "\"Friends, then.\"",
                    Replies = new[] { new TalkReply { Tone = TalkRules.Warm, Line = "\"Friends.\"", Lands = "\"Good.\"", Even = "\"Okay.\"", Grates = "\"Hm.\"" } },
                };
                var rhythm = new TalkRow
                {
                    Id = "spark.wire.rhythm", Pool = TalkPools.Spark, Kind = TalkKind.Rhythm, Open = "\"Long day.\"",
                    Replies = new[] { new TalkReply { Tone = TalkRules.Warm, Line = "\"It was.\"", Lands = "\"Good.\"", Even = "\"Okay.\"", Grates = "\"Hm.\"" } },
                };
                var declaration = FreeTalkS3Fixture.Romance().First(r => r.Kind == TalkKind.Declaration && r.Pool == TalkPools.Spark);
                foreach (var talk in new[] { stage, rhythm, declaration })
                    Assert.That(TalkScreenLines.FlirtShown(s, npc, talk, TalkScreenLines.Replies(s, npc, talk)),
                        Is.EqualTo(TalkRomance.FlirtOffered(s, npc, talk)), talk.Kind + ": the button is drawn exactly where the engine takes a Flirt.");
                Assert.That(TalkRomance.FlirtOffered(s, npc, stage), Is.True, "A stage talk carries the Flirt answer while Flirt is open.");
                Assert.That(TalkRomance.FlirtOffered(s, npc, declaration), Is.False, "A romance talk never carries the fourth reply.");
            }
        }
    }
}
