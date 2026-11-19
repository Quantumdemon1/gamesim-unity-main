using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The free talk's test fixtures (slice S1): a small table of the tests' own (the shipped tables arrive at integration and no
    /// test here reads them), fresh seasons at a talk level, and the command. Simulation only, so the Unity-free subset runs them.
    /// </summary>
    public static class FreeTalkFixture
    {
        /// <summary>Every slice-1 tone, so a test can answer in whichever tone lands the way it needs.</summary>
        public static TalkReply[] Replies(bool holdsBack = false) => TalkRules.Tones.Where(t => t != TalkRules.Flirt)
            .Select(t => new TalkReply
            {
                Tone = t, Line = "\"Say it " + t + ".\"", Lands = "\"Good one, {player}.\"", Even = "\"Fine.\"", Grates = "\"No.\"",
                HoldsBack = holdsBack,
            }).ToArray();

        public static TalkRow Row(string pool, string name, TalkKind kind, int layer, TalkGate when, bool usual = false, bool holdsBack = false) =>
            new TalkRow
            {
                Id = pool + ".test." + name, Pool = pool, Kind = kind, Layer = layer, When = when, Usual = usual,
                Reveals = kind == TalkKind.Layer, Open = "(at the counter) \"Talk to me.\"", Replies = Replies(holdsBack),
                Standing = kind == TalkKind.Standing
                    ? new TalkStandingLines
                    {
                        Honest = "\"{close} I trust. {worry} I watch. Me? {safe}.\"",
                        Neutral = "\"Someone I trust, someone I watch. Me? {safe}.\"",
                        Dishonest = "\"{close} I trust. {worry} I watch. Me? Fine.\"",
                    }
                    : null,
            };

        /// <summary>One talk of each slice-1 shape in each pool, the star pool's layer-3 frame included (it must never be served).</summary>
        public static List<TalkRow> Table(params string[] names)
        {
            var all = TalkPools.All.SelectMany(p => new[]
            {
                Row(p, "first", TalkKind.Layer, 1, new TalkGate { First = true }),
                Row(p, "l1", TalkKind.Layer, 1, new TalkGate { Layer = 1 }),
                Row(p, "l2", TalkKind.Layer, 2, new TalkGate { Layer = 2 }),
                Row(p, "l3", TalkKind.Layer, 3, new TalkGate { Layer = 3 }),
                Row(p, "st", TalkKind.Standing, 4, new TalkGate { Layer = 4 }),
                Row(p, "r1", TalkKind.Rhythm, 0, new TalkGate { Layer = 1 }),
                Row(p, "u1", TalkKind.Rhythm, 0, new TalkGate { Layer = 1 }, usual: true),
            }).ToList();
            return names.Length == 0 ? all : all.Where(t => names.Any(n => t.Id.EndsWith(".test." + n, StringComparison.Ordinal))).ToList();
        }

        /// <summary>A fresh shipped season of eight on move-in night, at a talk level.</summary>
        public static EpisodeState Fresh(int level = TalkRules.Talk, CastTemplates.Roster roster = CastTemplates.Roster.Regular, uint seed = 3111u)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8, Roster = roster }, seed);
            ShippedRules.ApplyFresh(s);
            s.story.talkRules = level;
            return s;
        }

        public static List<ContestantState> Npcs(EpisodeState s) => s.Active.Where(c => !c.isPlayer).ToList();

        public static EpisodeCommand Command(EpisodeState s, string npcId, string talkId, string tone) => new EpisodeCommand
        {
            id = "talk-" + s.revision + "-" + npcId, actorId = s.playerId, expectedRevision = s.revision, expectedPhase = s.phase,
            kind = EpisodeCommandKind.JustTalk, targetId = npcId, text = FreeTalk.CommandText(talkId, tone),
        };

        /// <summary>Answers the talk on offer with a housemate in a tone (a talk id of "none" when nothing is on offer).</summary>
        public static CommandResult Talk(EpisodeEngine engine, string npcId, string tone)
        {
            var s = engine.Snapshot;
            var offer = FreeTalk.Offer(s, npcId);
            return engine.Apply(Command(s, npcId, offer.Talk?.Id ?? "none", tone));
        }

        public static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var relation = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (relation == null) s.relationships.Add(relation = new RelationshipState { fromId = from, toId = to });
            relation.score = score;
        }

        /// <summary>The contact row with a housemate, made when missing.</summary>
        public static ContactState ContactOf(EpisodeState s, string npcId)
        {
            var contact = s.story.contacts.FirstOrDefault(c => c.npcId == npcId);
            if (contact == null) s.story.contacts.Add(contact = new ContactState { npcId = npcId });
            return contact;
        }

        /// <summary>A housemate (passing <paramref name="filter"/>) and a tone that lands at <paramref name="landing"/> on them, in cast and tone order.</summary>
        public static bool TryFind(EpisodeState s, int landing, Func<ContestantState, bool> filter, out string npcId, out string tone)
        {
            foreach (var npc in Npcs(s))
            {
                if (filter != null && !filter(npc)) continue;
                foreach (var t in TalkRules.Tones.Where(x => x != TalkRules.Flirt))
                    if (FreeTalk.Landing(s, npc, t) == landing) { npcId = npc.id; tone = t; return true; }
            }
            npcId = null; tone = null;
            return false;
        }

        /// <summary>Applies the house's next step until <paramref name="stop"/> holds; fails the test when it never does.</summary>
        public static void WalkUntil(EpisodeEngine engine, Func<EpisodeState, bool> stop, int most = 3000)
        {
            for (int i = 0; i < most; i++)
            {
                var s = engine.Snapshot;
                if (stop(s)) return;
                var result = engine.Apply(EpisodeEngineTests.NextCommand(s));
                Assert.That(result.accepted, Is.True, "The walk: " + result.reason);
            }
            Assert.Fail("The walk never reached its stop.");
        }

        /// <summary>A season walked from move-in night to the first window of week two with the player still in the house.</summary>
        public static EpisodeEngine WeekTwo(int level = TalkRules.Talk)
        {
            for (uint seed = 3201u; seed < 3241u; seed++)
            {
                var engine = new EpisodeEngine(Fresh(level, seed: seed));
                bool PlayerOut(EpisodeState s) => s.Find(s.playerId).status != ContestantStatus.Active;
                WalkUntil(engine, s => PlayerOut(s) || s.week >= 2 && FreeTalk.WindowKey(s) != 0 || s.phase == EpisodePhase.Finished);
                var at = engine.Snapshot;
                if (!PlayerOut(at) && at.week >= 2 && FreeTalk.WindowKey(at) != 0) return engine;
            }
            Assert.Fail("No seed kept the player in the house into week two.");
            return null;
        }
    }
}
