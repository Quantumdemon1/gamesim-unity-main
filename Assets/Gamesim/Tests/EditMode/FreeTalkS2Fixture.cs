using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Free talk slice 2's fixtures (talk rules 2): the tests' own stage talks beside slice 1's small table, and the chemistry and
    /// stage a test needs, set on the state directly. Never the shipped tables. Simulation only, so the Unity-free subset runs it.
    /// </summary>
    public static class FreeTalkS2Fixture
    {
        public static readonly FriendStage[] TableStages = { FriendStage.Acquainted, FriendStage.Friendly, FriendStage.Friend, FriendStage.CloseFriend };

        /// <summary>The Close friend talk's two favour replies: Warm keeps it, Straight refuses it.</summary>
        public static TalkReply[] FavourReplies() => new[]
        {
            new TalkReply { Tone = TalkRules.Warm, Favour = TalkFavour.Keep, Line = "\"I've got you.\"", Lands = "\"I knew it.\"", Even = "\"Okay.\"", Grates = "\"We'll see.\"" },
            new TalkReply { Tone = TalkRules.Straight, Favour = TalkFavour.Refuse, Line = "\"Can't promise that.\"", Lands = "\"Fair.\"", Even = "\"Okay.\"", Grates = "\"Noted.\"" },
        };

        public static string StageId(string pool, FriendStage stage) => pool + ".test.stage" + (int)stage;

        public static TalkRow StageRow(string pool, FriendStage stage) => new TalkRow
        {
            Id = StageId(pool, stage), Pool = pool, Kind = TalkKind.Stage, Stage = stage, Open = "(on the couch) \"So. Us.\"",
            Replies = stage == FriendStage.CloseFriend ? FavourReplies() : FreeTalkFixture.Replies(),
        };

        /// <summary>A stage talk of each stage in each of <paramref name="pools"/> (every pool when none is named).</summary>
        public static List<TalkRow> Stages(params string[] pools) =>
            (pools.Length == 0 ? TalkPools.All : pools).SelectMany(p => TableStages.Select(st => StageRow(p, st))).ToList();

        /// <summary>Slice 1's small table with the stage talks of every pool.</summary>
        public static List<TalkRow> Table() => FreeTalkFixture.Table().Concat(Stages()).ToList();

        /// <summary>Kindred with a housemate: both Loyal (affinity 4, clamped 3), agreement 10, three counted talks.</summary>
        public static ContactState Kindred(EpisodeState s, string npcId)
        {
            s.Find(npcId).traits = new List<string> { "Loyal" };
            s.Find(s.playerId).traits = new List<string> { "Loyal" };
            var contact = FreeTalkFixture.ContactOf(s, npcId);
            contact.talks = Math.Max(contact.talks, TalkChemistry.ShownAfterTalks);
            contact.agreement = 10;
            return contact;
        }

        /// <summary>Grating with a housemate: a Loyal one and a Sneaky player (affinity -4, clamped -3), agreement 0, three counted talks.</summary>
        public static ContactState Grating(EpisodeState s, string npcId)
        {
            s.Find(npcId).traits = new List<string> { "Loyal" };
            s.Find(s.playerId).traits = new List<string> { "Sneaky" };
            var contact = FreeTalkFixture.ContactOf(s, npcId);
            contact.talks = Math.Max(contact.talks, TalkChemistry.ShownAfterTalks);
            contact.agreement = 0;
            return contact;
        }

        /// <summary>A housemate whose class is <paramref name="pool"/> by the traits spec 3.5 names (Steady: Loyal, Competitive; Guarded: Analytical, Strategic; Spark: Social, Strategic).</summary>
        public static void ClassAs(EpisodeState s, string npcId, string pool)
        {
            s.Find(npcId).traits = pool == TalkPools.Guarded ? new List<string> { "Analytical", "Strategic" }
                : pool == TalkPools.Spark ? new List<string> { "Social", "Strategic" } : new List<string> { "Loyal", "Competitive" };
            Assert.That(FreeTalk.ClassOf(s.Find(npcId)), Is.EqualTo(pool), "The control: the traits give the class.");
        }

        /// <summary>Their view of the player and the player's of them.</summary>
        public static void Views(EpisodeState s, string npcId, double theirs, double yours)
        {
            FreeTalkFixture.SetScore(s, npcId, s.playerId, theirs);
            FreeTalkFixture.SetScore(s, s.playerId, npcId, yours);
        }

        public static void AssertValid(EpisodeState s) =>
            Assert.That(EpisodeValidation.TryValidate(s, out string why), Is.True, "The fixture must be a valid season: " + why);

        /// <summary>A season walked to the first window of week <paramref name="week"/> with the player still in the house.</summary>
        public static EpisodeEngine AtWeek(int week, int level = TalkRules.Chemistry)
        {
            for (uint seed = 3201u; seed < 3241u; seed++)
            {
                var engine = new EpisodeEngine(FreeTalkFixture.Fresh(level, seed: seed));
                bool PlayerOut(EpisodeState s) => s.Find(s.playerId).status != ContestantStatus.Active;
                FreeTalkFixture.WalkUntil(engine, s => PlayerOut(s) || s.week >= week && FreeTalk.WindowKey(s) != 0 || s.phase == EpisodePhase.Finished);
                var at = engine.Snapshot;
                if (!PlayerOut(at) && at.week >= week && FreeTalk.WindowKey(at) != 0) return engine;
            }
            Assert.Fail("No seed kept the player in the house into week " + week + ".");
            return null;
        }
    }
}
