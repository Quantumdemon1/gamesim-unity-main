#if !UNITY_5_3_OR_NEWER
// D4's recorded-season evidence, reproducible on demand: Tools/SimulationTests only. Unity's batch
// runner executes [Explicit] tests, so the file is compiled out of the editor's assemblies.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Leaks and double-dealing over recorded seasons (WAVE-D-NPC-PACTS-PLAN §2.6): the 54 seasons
    /// <see cref="CommitmentRulesSeasonDigests"/> plays - three rule sets, houses of 6, 8 and 12, six seeds
    /// each - with the same busy scripted player.
    ///
    /// <para><b>Off.</b> The leak rules need the commitment rules, so the half that matters is the
    /// commitment rules' own: <see cref="RecordedUnderCommitments"/> holds those 54 seasons' digests as the
    /// build before D4 played them (2ea986df), and with the leak rules off every one is byte-identical.
    /// (The seasons without the commitment rules are <see cref="CommitmentRulesSeasonDigests"/>' first
    /// half, against its own goldens.)</para>
    ///
    /// <para><b>On.</b> The same seasons with the leak rules from week one, played twice - by the busy
    /// player, and by one who keeps three pacts - every command legal, and every step checked: each leak
    /// is recomputed from the eviction night's state and must be its coin; each double-dealing line has
    /// its receipt or its grudge and each receipt its line; no receipt is between two houseguests; each
    /// listen-in that taught the player a pact said so. Rates by house size are printed. The sixteen-person
    /// house is the combined roster's, another lane's: not measured here.</para>
    ///
    /// <para>Explicit: run with <c>dotnet test Tools/SimulationTests --filter "FullyQualifiedName~AllianceLeakSeasonDigests"</c>.</para>
    /// </summary>
    public sealed class AllianceLeakSeasonDigests
    {
        /// <summary>CommitmentRulesSeasonDigests.Run(true) at 2ea986df, before D4: the commitment rules on, the leak rules off.</summary>
        private static readonly string[] RecordedUnderCommitments =
        {
            "director n6 s1 898b5b200d4a3df9",
            "director n6 s2 096f334c6d8216da",
            "director n6 s3 0857ac1d72ad3cc5",
            "director n6 s4 94f5d2c67e988a29",
            "director n6 s5 7650f12a2770d630",
            "director n6 s6 b0530733d9656a36",
            "director n8 s1 1d254c0c125d5f13",
            "director n8 s2 b3131af609fb8f26",
            "director n8 s3 2f20d6bef43bc83e",
            "director n8 s4 3caf55434baeae87",
            "director n8 s5 9f07c3effe440228",
            "director n8 s6 2ba9f1b4de63f2d1",
            "director n12 s1 aec86623fc888dc4",
            "director n12 s2 7718ab85ebbe7df8",
            "director n12 s3 9a191dd79d89484c",
            "director n12 s4 41c7613bc9ace68a",
            "director n12 s5 c6c9a228179d4f6b",
            "director n12 s6 52ca890a7ac4ba1d",
            "legacy n6 s1 64f24a5b468b2885",
            "legacy n6 s2 55dc0f40ed5cdc42",
            "legacy n6 s3 b4d7f6c013c95765",
            "legacy n6 s4 e847b367f9ed9fd9",
            "legacy n6 s5 64476676a8bb7bc6",
            "legacy n6 s6 35bcc235b7acf771",
            "legacy n8 s1 ceba594228916acb",
            "legacy n8 s2 6a485be47dc2e69b",
            "legacy n8 s3 d98bb38d85a93857",
            "legacy n8 s4 b41beb533029b754",
            "legacy n8 s5 ced9a9d22e63ebb9",
            "legacy n8 s6 3da53f0a62835388",
            "legacy n12 s1 a150ee7cc958dba4",
            "legacy n12 s2 be056d29ce2dc5f1",
            "legacy n12 s3 e7ce0292916f7d0a",
            "legacy n12 s4 b487ab069ae80e98",
            "legacy n12 s5 95cee828ef343a18",
            "legacy n12 s6 6909159c43f4e119",
            "harness n6 s1 33094b4cdeddc6c4",
            "harness n6 s2 4e6cda838bd6ad61",
            "harness n6 s3 4cbe39765966c74f",
            "harness n6 s4 5736a948423a7d02",
            "harness n6 s5 6d5a0caa7f3ba54d",
            "harness n6 s6 9ef3876fcb40dd08",
            "harness n8 s1 264e3f0098e3b8f0",
            "harness n8 s2 7adddfeee8a92259",
            "harness n8 s3 b27eac80a7796ec0",
            "harness n8 s4 20b580d7316c2d03",
            "harness n8 s5 0f4cd0db117e3a92",
            "harness n8 s6 a376a9f6278a6b55",
            "harness n12 s1 b8759acae2a69ddc",
            "harness n12 s2 a6b119966d28256e",
            "harness n12 s3 a925b7fc4689adc6",
            "harness n12 s4 c3b832136bca38d7",
            "harness n12 s5 55da6cd996e653e7",
            "harness n12 s6 5f6750cd56f31fb7",
        };

        [Test, Explicit("Plays 54 seasons under the commitment rules to their ends: D4 off, every digest as it was.")]
        public void WithoutTheLeakRulesEverySeasonUnderTheCommitmentRulesPlaysAsItDidBeforeThem()
        {
            var played = CommitmentRulesSeasonDigests.Run(true, out int errors, out _);
            Assert.That(errors, Is.Zero, string.Join("\n", played.Where(line => line.Contains(" ERROR "))));
            Assert.That(played, Has.Count.EqualTo(RecordedUnderCommitments.Length), "Every season has its recorded digest.");
            var moved = played.Select(line => string.Join(" ", line.Split(' ').Take(4))).Where(key => !RecordedUnderCommitments.Contains(key)).ToList();
            Assert.That(moved, Is.Empty, "A season under the commitment rules without the leak rules moved:\n" + string.Join("\n", moved));
        }

        [Test, Explicit("Plays 54 seasons twice under the leak rules: every leak its coin, every reaction with its line, every command legal.")]
        public void UnderTheLeakRulesEveryLeakIsItsCoinAndEveryReactionHasItsLine()
        {
            var busy = Measure(false);
            var juggler = Measure(true);
            foreach (var (name, run) in new[] { ("busy player", busy), ("three-pact player", juggler) })
            {
                TestContext.Out.WriteLine(name + ":");
                foreach (int size in new[] { 6, 8, 12 })
                {
                    var c = run.bySize[size];
                    double n = run.seasonsBySize[size];
                    TestContext.Out.WriteLine("  n" + size + " (" + n + " seasons): " + string.Join(", ", Reported.Select(key =>
                        key + "=" + Get(c, key) + " (" + (Get(c, key) / n).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "/season)")));
                }
                var all = Total(run);
                TestContext.Out.WriteLine("  all: " + string.Join(", ", all.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key + "=" + k.Value)));
                Assert.That(run.errors, Is.Empty, name + ": every command a season under the leak rules was given is legal:\n" + string.Join("\n", run.errors));
                Assert.That(Get(all, "leak-coin-without-leak"), Is.Zero, name + ": every coin that landed leaked.");
                Assert.That(Get(all, "receipt-without-line"), Is.Zero, name + ": every receipt came with its line.");
                Assert.That(Get(all, "line-without-reaction"), Is.Zero, name + ": every line came with its receipt or grudge.");
                Assert.That(Get(all, "receipt-between-houseguests"), Is.Zero, name + ": nothing between two houseguests.");
                Assert.That(Get(all, "listen-in-without-knower"), Is.Zero, name + ": a listen-in's sentence always taught the player the pact.");
                Assert.That(Get(all, "double-dealing"), Is.EqualTo(Get(all, "receipt") + Get(all, "grudge")), name + ": one line a reaction.");
            }
            var both = Total(busy).Concat(Total(juggler)).GroupBy(k => k.Key).ToDictionary(g => g.Key, g => g.Sum(k => k.Value));
            Assert.That(Get(both, "leak"), Is.GreaterThan(0), "Pacts got out.");
            Assert.That(Get(both, "spread"), Is.GreaterThan(0), "The gossip carried them.");
            Assert.That(Get(both, "double-dealing"), Is.GreaterThan(0), "An ally found out about another of the player's pacts.");
        }

        // ------------------------------------------------------------ the run

        private static readonly string[] Reported =
            { "rolls", "leak", "spread", "spread-to-player", "double-dealing", "receipt", "grudge", "listen-in" };

        private sealed class RunCounts
        {
            public readonly Dictionary<int, Dictionary<string, int>> bySize = new Dictionary<int, Dictionary<string, int>>();
            public readonly Dictionary<int, int> seasonsBySize = new Dictionary<int, int>();
            public readonly List<string> errors = new List<string>();
        }

        private static int Get(IDictionary<string, int> counts, string key) => counts.TryGetValue(key, out int n) ? n : 0;

        private static void Count(Dictionary<string, int> counts, string key, int by = 1)
        {
            counts.TryGetValue(key, out int n); counts[key] = n + by;
        }

        private static Dictionary<string, int> Total(RunCounts run) =>
            run.bySize.Values.SelectMany(c => c).GroupBy(k => k.Key).ToDictionary(g => g.Key, g => g.Sum(k => k.Value));

        private static readonly MethodInfo SeasonOf = Private("Season", typeof(string), typeof(int), typeof(uint), typeof(bool));
        private static readonly MethodInfo BusyOf = Private("Busy", typeof(EpisodeState), typeof(uint), typeof(int), typeof(bool));
        private static readonly MethodInfo NextOf = Private("NextCommand", typeof(EpisodeState));

        /// <summary>The digests' own seasons and scripted player, by reflection: the same houses and the same moves.</summary>
        private static MethodInfo Private(string name, params Type[] parameters) =>
            typeof(CommitmentRulesSeasonDigests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static, null, parameters, null)
            ?? throw new InvalidOperationException("CommitmentRulesSeasonDigests." + name + " is the harness's player.");

        private static RunCounts Measure(bool juggler)
        {
            var run = new RunCounts();
            foreach (var config in new[] { "director", "legacy", "harness" })
            foreach (int size in new[] { 6, 8, 12 })
            for (uint seed = 1; seed <= 6; seed++)
            {
                if (!run.bySize.TryGetValue(size, out var counts)) run.bySize[size] = counts = new Dictionary<string, int>();
                run.seasonsBySize.TryGetValue(size, out int n); run.seasonsBySize[size] = n + 1;
                string error = Play(config, size, seed, juggler, counts);
                if (error != null) run.errors.Add(config + " n" + size + " s" + seed + ": " + error);
            }
            return run;
        }

        /// <summary>One season under the commitment rules and the leak rules from week one, every step checked.</summary>
        private static string Play(string config, int size, uint seed, bool juggler, Dictionary<string, int> counts)
        {
            var start = (EpisodeState)SeasonOf.Invoke(null, new object[] { config, size, seed, true });
            EpisodeEngine.EnableAllianceLeaks(start);
            var engine = new EpisodeEngine(start);
            for (int i = 0; i < 3000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var s = engine.Snapshot;
                CommandResult applied = null;
                var third = juggler ? ThirdPact(s, seed) : null;
                if (third != null)
                {
                    var tried = engine.Apply(third);
                    if (tried.accepted) applied = tried;
                }
                for (int attempt = 0; attempt < 6 && applied == null; attempt++)
                {
                    var own = (EpisodeCommand)BusyOf.Invoke(null, new object[] { s, seed, attempt, true });
                    if (own == null) break;
                    var tried = engine.Apply(own);
                    if (tried.accepted) applied = tried;
                }
                if (applied == null)
                {
                    var next = (EpisodeCommand)NextOf.Invoke(null, new object[] { s });
                    applied = engine.Apply(next);
                    if (!applied.accepted) return "week " + s.week + " " + s.phase + " " + next.kind + ": " + applied.reason;
                }
                Check(s, applied.state, counts);
            }
            return engine.Snapshot.phase == EpisodePhase.Finished ? null : "unfinished";
        }

        /// <summary>
        /// The three-pact player: in free time or the campaign, with an action left and fewer than three
        /// pacts, they propose one to the houseguest they like best who is not already a partner.
        /// </summary>
        private static EpisodeCommand ThirdPact(EpisodeState s, uint seed)
        {
            if (s.pendingDiary != null || s.Find(s.playerId)?.status != ContestantStatus.Active) return null;
            if (s.phase != EpisodePhase.Social && s.phase != EpisodePhase.Campaign) return null;
            if (EpisodeEngine.PlayerPactsHeld(s) >= EpisodeEngine.PlayerPactCap) return null;
            if (EpisodeEngine.SocialActionsSpent(s) >= EpisodeEngine.SocialActionBudget(s)) return null;
            var pick = s.Active.Where(c => !c.isPlayer && EpisodeEngine.AllianceRefusal(s, c.id) == null)
                .OrderByDescending(c => s.Score(s.playerId, c.id)).ThenBy(c => c.id, StringComparer.Ordinal).FirstOrDefault();
            if (pick == null) return null;
            return new EpisodeCommand
            {
                id = "three-pacts-" + seed + "-" + s.revision, actorId = s.playerId, kind = EpisodeCommandKind.FormAlliance,
                targetId = pick.id, expectedRevision = s.revision, expectedPhase = s.phase,
            };
        }

        // ------------------------------------------------------------ what one step did

        private static void Check(EpisodeState before, EpisodeState after, Dictionary<string, int> counts)
        {
            var said = after.events.Where(e => e.sequence >= before.nextSequence).ToList();

            // The weekly leak, recomputed from the eviction night's state: the step out of the eviction into free time.
            if (before.phase == EpisodePhase.Eviction && before.evictionResolved && after.phase == EpisodePhase.Social)
                foreach (var pact in before.alliances)
                {
                    if (AllianceLeaks.Rolls(before, pact) == null || !AllianceLeaks.On(before)) continue;
                    Count(counts, "rolls");
                    bool coin = AllianceLeaks.Leaks(before, pact);
                    var fact = Knowledge.Of(after, FactKinds.Alliance, pact.id);
                    bool out_ = fact != null && fact.visibility != FactVisibility.Private;
                    if (coin && out_) Count(counts, "leak");
                    else if (coin) Count(counts, "leak-coin-without-leak");
                    else if (out_) Count(counts, "widened-by-a-story");
                }

            // The gossip and the stories: who came to know of a pact they are not in.
            foreach (var fact in after.story.facts.Where(f => f.kind == FactKinds.Alliance))
            {
                var was = before.story.facts.FirstOrDefault(f => f.id == fact.id);
                var pact = after.alliances.FirstOrDefault(a => a.id == fact.refId);
                if (pact == null) continue;
                foreach (string knower in fact.knowers.Where(id => !pact.members.Contains(id) && (was == null || !was.knowers.Contains(id))))
                {
                    Count(counts, "spread");
                    if (knower == after.playerId) Count(counts, "spread-to-player");
                }
            }

            // The listen-ins that taught the player a pact, each by its sentence.
            foreach (var line in said.Where(e => e.kind == "eavesdrop" && e.text != null && e.text.Contains(" From the way they talked, ")))
            {
                Count(counts, "listen-in");
                bool taught = after.alliances.Any(pact => line.text.EndsWith(AllianceLeaks.ListenInSentence(after, pact), StringComparison.Ordinal)
                    && Knowledge.Knows(Knowledge.Of(after, FactKinds.Alliance, pact.id), after.playerId)
                    && !Knowledge.Knows(Knowledge.Of(before, FactKinds.Alliance, pact.id), before.playerId));
                if (!taught) Count(counts, "listen-in-without-knower");
            }

            // Double-dealing: each line with its receipt or its grudge, and each receipt with its line.
            var receipts = after.relationships.SelectMany(r => r.events.Where(e => e.type == StoryReceipts.DoubleDealt && e.sequence >= before.nextSequence)
                .Select(e => (r.fromId, r.toId))).ToList();
            foreach (var (from, to) in receipts)
            {
                Count(counts, "receipt");
                if (from == after.playerId || to != after.playerId) Count(counts, "receipt-between-houseguests");
            }
            var lines = said.Where(e => e.kind == WaveDEventKinds.DoubleDealing).ToList();
            Count(counts, "double-dealing", lines.Count);
            foreach (var line in lines.Where(e => e.audienceIds == null || e.audienceIds.Count != 2 || e.audienceIds[0] != after.playerId))
                Count(counts, "line-without-reaction");
            // One who found out about two of the player's pacts in one step reacts to each, so match by person.
            foreach (var group in lines.Where(e => e.audienceIds != null && e.audienceIds.Count == 2 && e.audienceIds[0] == after.playerId)
                         .GroupBy(e => e.audienceIds[1]))
            {
                int kept = receipts.Count(r => r.fromId == group.Key);
                int rest = group.Count() - kept;
                if (rest < 0) Count(counts, "receipt-without-line", -rest);
                else if (rest > 0 && Grudges.Severity(after, group.Key, after.playerId) > Grudges.Severity(before, group.Key, before.playerId))
                    Count(counts, "grudge", rest);
                else if (rest > 0) Count(counts, "line-without-reaction", rest);
            }
            foreach (var orphan in receipts.Where(r => !lines.Any(e => e.audienceIds != null && e.audienceIds.Count == 2 && e.audienceIds[1] == r.fromId)))
                Count(counts, "receipt-without-line");
        }
    }
}
#endif
