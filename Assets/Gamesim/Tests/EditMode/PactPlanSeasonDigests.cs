#if !UNITY_5_3_OR_NEWER
// D3's recorded-season evidence, reproducible on demand: Tools/SimulationTests only. Unity's batch
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
    /// The war rooms over recorded seasons (WAVE-D-NPC-PACTS-PLAN §3.6): the 54 seasons
    /// <see cref="CommitmentRulesSeasonDigests"/> plays - three rule sets, houses of 6, 8 and 12, six seeds
    /// each - and the director's own fresh season beside them, as <see cref="AllianceLeakSeasonDigests"/> does.
    ///
    /// <para><b>Off.</b> The war rooms need the commitment rules and the levers, so the half that matters is
    /// the commitment rules' own: <see cref="RecordedUnderCommitments"/> holds those 54 seasons' digests as the
    /// build before Wave D played them (2ea986df, the leak rules' own recording), and with the war rooms off
    /// every one is byte-identical. (The seasons without the commitment rules are
    /// <see cref="CommitmentRulesSeasonDigests"/>' first half, against its own goldens.)</para>
    ///
    /// <para><b>On.</b> The same seasons with the war rooms from week one, played twice - by the digests' busy
    /// player, answering every plan on a fixed policy, and by a player who starts the season in a constructed
    /// pact of three (trios are rare: C5 saw eight joins in 43 askings) and holds its war room every campaign
    /// it can - every command legal, and every step checked: each war room's says are lawful, each answer and
    /// each lapse draws nothing from the season's stream and says one line, the season's first war room draws
    /// exactly what the meeting draws without the rules, a plan the player backs is that week's call, and at
    /// each reveal who of those with a plan voted with it. Counted by house size: the campaign weeks a trio of
    /// the player's could meet (D3-S0's measure), war rooms held, majority targets and splits, answers,
    /// counters and those carried, members come round, those with a plan and those not (dissent), lapses and
    /// void plans, and follow-through. The legacy set has no levers, so it never plays the war rooms: rates are
    /// per season that played them. The sixteen-person house is the combined roster's: not measured here.</para>
    ///
    /// <para>Explicit: run with <c>dotnet test Tools/SimulationTests --filter "FullyQualifiedName~PactPlanSeasonDigests"</c>.</para>
    /// </summary>
    public sealed class PactPlanSeasonDigests
    {
        /// <summary>CommitmentRulesSeasonDigests.Run(true) at 2ea986df, before Wave D: the commitment rules on, Wave D's rules off.</summary>
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

        [Test, Explicit("Plays 54 seasons under the commitment rules to their ends: D3 off, every digest as it was.")]
        public void WithoutTheWarRoomsEverySeasonUnderTheCommitmentRulesPlaysAsItDidBeforeThem()
        {
            var played = CommitmentRulesSeasonDigests.Run(true, out int errors, out _);
            Assert.That(errors, Is.Zero, string.Join("\n", played.Where(line => line.Contains(" ERROR "))));
            Assert.That(played, Has.Count.EqualTo(RecordedUnderCommitments.Length), "Every season has its recorded digest.");
            var moved = played.Select(line => string.Join(" ", line.Split(' ').Take(4))).Where(key => !RecordedUnderCommitments.Contains(key)).ToList();
            Assert.That(moved, Is.Empty, "A season under the commitment rules without the war rooms moved:\n" + string.Join("\n", moved));
        }

        [Test, Explicit("Plays 72 seasons twice under the war rooms: every plan lawful, every answer and lapse drawing nothing, every command legal.")]
        public void UnderTheWarRoomsEveryPlanIsLawfulAndDrawsNothingFromTheSeason()
        {
            var busy = Measure(false);
            var trio = Measure(true);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var (name, run) in new[] { ("busy player", busy), ("trio player", trio) })
            {
                TestContext.Out.WriteLine(name + ":");
                foreach (int size in Sizes)
                {
                    var c = run.bySize[size];
                    double n = Get(run.playedBySize, size);
                    TestContext.Out.WriteLine("  n" + size + " (" + n + " of " + Get(run.seasonsBySize, size) + " seasons played the rules): "
                        + string.Join(", ", Reported.Select(key => key + "=" + Get(c, key) + " (" + (n > 0 ? Get(c, key) / n : 0).ToString("0.00", inv) + "/season)")));
                }
                foreach (var config in Configs)
                    TestContext.Out.WriteLine("  " + config + " (" + Get(run.playedByConfig, config) + " played the rules): "
                        + string.Join(", ", Reported.Select(key => key + "=" + Get(run.byConfig[config], key))));
                var all = Total(run);
                TestContext.Out.WriteLine("  all: " + string.Join(", ", all.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key + "=" + k.Value)));
                Assert.That(run.errors, Is.Empty, name + ": every command a season under the war rooms was given is legal:\n" + string.Join("\n", run.errors));
                Assert.That(Get(run.playedByConfig, "legacy"), Is.Zero, name + ": the legacy set has no levers, so it never plays the war rooms.");
                Assert.That(Get(run.playedByConfig, Fresh), Is.EqualTo(Sizes.Length * 6), name + ": every fresh season played them.");
                Assert.That(Get(all, "illegal-say"), Is.Zero, name + ": every say names somebody on the block, a nominee the other one.");
                Assert.That(Get(all, "war-room-drew"), Is.Zero, name + ": a season's first war room draws what the meeting draws without the rules.");
                Assert.That(Get(all, "answer-drew"), Is.Zero, name + ": no answer draws from the season's stream.");
                Assert.That(Get(all, "lapse-drew"), Is.Zero, name + ": no lapse draws from the season's stream.");
                Assert.That(Get(all, "line-missing"), Is.Zero, name + ": every plan settled says one line.");
                Assert.That(Get(all, "call-missing"), Is.Zero, name + ": a plan the player backs is that week's call.");
                Assert.That(Get(all, "open-after-campaign"), Is.Zero, name + ": no plan is open past its campaign.");
                Assert.That(Get(all, "agreed") + Get(all, "countered") + Get(all, "low") + Get(all, "lapsed") + Get(all, "void"),
                    Is.EqualTo(Get(all, "war-room")), name + ": every war room's plan settled, once.");
            }
            var both = Total(busy).Concat(Total(trio)).GroupBy(k => k.Key).ToDictionary(g => g.Key, g => g.Sum(k => k.Value));
            Assert.That(Get(Total(trio), "war-room"), Is.GreaterThan(0), "War rooms were held.");
            foreach (string key in new[] { "majority", "agreed", "countered", "low", "lapsed", "followed-through" })
                Assert.That(Get(both, key), Is.GreaterThan(0), "The seasons reach '" + key + "'.");
            Assert.That(Get(both, "came-round") + Get(both, "counter-carried"), Is.GreaterThan(0), "Counters moved somebody.");
            Assert.That(Get(both, "first-war-room-checked"), Is.GreaterThan(0), "The draw twin ran.");
        }

        // ------------------------------------------------------------ the run

        /// <summary>
        /// The director's own fresh season (EpisodeDirector.Season's StartSeason): the digests' director set,
        /// plus the economy, the unified Safety and hearing rules and the leak rules a fresh season takes.
        /// </summary>
        private const string Fresh = "fresh";

        private static readonly string[] Configs = { "director", "legacy", "harness", Fresh };
        private static readonly int[] Sizes = { 6, 8, 12 };

        private static readonly string[] Reported =
        {
            "trio-weeks", "war-room", "majority", "split", "quiet", "agreed", "countered", "counter-carried", "counter-reachable", "came-round",
            "low", "lapsed", "void", "with-plan", "not-with-plan", "player-call", "followed-through", "broke-ranks", "target-evicted",
        };

        private sealed class RunCounts
        {
            public readonly Dictionary<int, Dictionary<string, int>> bySize = new Dictionary<int, Dictionary<string, int>>();
            public readonly Dictionary<string, Dictionary<string, int>> byConfig = new Dictionary<string, Dictionary<string, int>>();
            public readonly Dictionary<int, int> seasonsBySize = new Dictionary<int, int>(), playedBySize = new Dictionary<int, int>();
            public readonly Dictionary<string, int> playedByConfig = new Dictionary<string, int>();
            public readonly List<string> errors = new List<string>();
        }

        private static int Get<TKey>(IDictionary<TKey, int> counts, TKey key) => counts.TryGetValue(key, out int n) ? n : 0;

        private static void Count<TKey>(Dictionary<TKey, int> counts, TKey key, int by = 1)
        {
            counts.TryGetValue(key, out int n); counts[key] = n + by;
        }

        private static void Merge<TKey>(Dictionary<TKey, Dictionary<string, int>> into, TKey key, Dictionary<string, int> counts)
        {
            if (!into.TryGetValue(key, out var total)) into[key] = total = new Dictionary<string, int>();
            foreach (var k in counts) Count(total, k.Key, k.Value);
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

        private static RunCounts Measure(bool trio)
        {
            var run = new RunCounts();
            foreach (var config in Configs)
            foreach (int size in Sizes)
            for (uint seed = 1; seed <= 6; seed++)
            {
                var counts = new Dictionary<string, int>();
                string error = Play(config, size, seed, trio, counts, out bool played);
                if (error != null) run.errors.Add(config + " n" + size + " s" + seed + ": " + error);
                Count(run.seasonsBySize, size);
                if (played) { Count(run.playedBySize, size); Count(run.playedByConfig, config); }
                Merge(run.bySize, size, counts);
                Merge(run.byConfig, config, counts);
            }
            return run;
        }

        /// <summary>
        /// One season under the commitment rules and the war rooms from week one, every step checked.
        /// <paramref name="played"/>: whether the war rooms were on at any step (the legacy set has no levers, so never).
        /// </summary>
        private static string Play(string config, int size, uint seed, bool trio, Dictionary<string, int> counts, out bool played)
        {
            played = false;
            var start = (EpisodeState)SeasonOf.Invoke(null, new object[] { config == Fresh ? "director" : config, size, seed, true });
            if (config == Fresh)
            {
                EpisodeEngine.EnableEconomy(start);
                EpisodeEngine.EnableAllianceLeaks(start);
                start.unifiedCommitmentRulesVersion = UnifiedCommitments.ProspectiveVersion;
                start.unifiedHearingRulesVersion = UnifiedCommitmentHearings.ProspectiveVersion;
            }
            EpisodeEngine.EnablePactPlans(start);
            if (trio) ATrioOfThePlayers(start);
            var engine = new EpisodeEngine(start);
            bool firstWarRoom = true;
            int campaignWeek = 0;
            for (int i = 0; i < 3000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var s = engine.Snapshot;
                if (EpisodeEngine.PactPlanRulesOn(s)) played = true;
                // D3-S0's measure: the campaign weeks a pact of three of the player's could meet.
                if (s.phase == EpisodePhase.Campaign && campaignWeek != s.week)
                {
                    campaignWeek = s.week;
                    Count(counts, "trio-weeks", s.alliances.Count(a => PactPlans.IsWarRoomPact(s, a)
                        && EpisodeEngine.Voters(s).Any(v => !v.isPlayer && a.members.Contains(v.id))));
                }
                EpisodeCommand applied = null;
                CommandResult result = null;
                var own = new List<EpisodeCommand>();
                var answer = Answer(s, seed);
                if (answer != null) own.Add(answer);
                if (trio) { var meeting = WarRoom(s); if (meeting != null) own.Add(meeting); }
                foreach (var command in own)
                {
                    var tried = engine.Apply(command);
                    if (tried.accepted) { applied = command; result = tried; break; }
                    if (command.kind == EpisodeCommandKind.AnswerPactPlan) return "week " + s.week + " " + s.phase + " AnswerPactPlan refused: " + tried.reason;
                }
                for (int attempt = 0; attempt < 6 && result == null; attempt++)
                {
                    var busy = (EpisodeCommand)BusyOf.Invoke(null, new object[] { s, seed, attempt, true });
                    if (busy == null) break;
                    var tried = engine.Apply(busy);
                    if (tried.accepted) { applied = busy; result = tried; }
                }
                if (result == null)
                {
                    applied = (EpisodeCommand)NextOf.Invoke(null, new object[] { s });
                    result = engine.Apply(applied);
                    if (!result.accepted) return "week " + s.week + " " + s.phase + " " + applied.kind + ": " + result.reason;
                }
                Check(s, applied, result.state, counts, ref firstWarRoom);
            }
            return engine.Snapshot.phase == EpisodePhase.Finished ? null : "unfinished";
        }

        /// <summary>
        /// The constructed trio (§3.6): the player and the two houseguests first in the cast's order, in a pact
        /// the player made in week one, warm on each other - a pact the player keeps or loses as any is kept.
        /// </summary>
        private static void ATrioOfThePlayers(EpisodeState s)
        {
            var npcs = s.Active.Where(c => !c.isPlayer).Take(2).Select(c => c.id).ToList();
            var pact = new AllianceState { id = "alliance-war-room-trio", name = "The War Room", active = true,
                members = new List<string> { s.playerId }.Concat(npcs).ToList() };
            s.alliances.Add(pact);
            s.ledger.alliances.Add(new AllianceRow { id = pact.id, startedWeek = s.week, why = "player" });
            EpisodeEngine.AllianceFormedUnderRead(s, pact);
            foreach (string from in pact.members)
                foreach (string to in pact.members.Where(id => id != from))
                {
                    var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
                    if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
                    edge.score = 30;
                }
        }

        private static EpisodeCommand Cmd(EpisodeState s, EpisodeCommandKind kind, string target, string second, string text) => new EpisodeCommand
        {
            id = "war-room-" + s.revision + "-" + kind + "-" + target + "-" + second, actorId = s.playerId, kind = kind,
            targetId = target, secondTargetId = second, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
        };

        /// <summary>The trio player's meeting: in the campaign, with an action left, the first pact of theirs that could convene, through its first member in the house.</summary>
        private static EpisodeCommand WarRoom(EpisodeState s)
        {
            if (s.pendingDiary != null || EpisodeEngine.SocialActionsSpent(s) >= EpisodeEngine.SocialActionBudget(s)) return null;
            var pact = s.alliances.FirstOrDefault(a => PactPlans.CouldConvene(s, a));
            string through = pact?.members.FirstOrDefault(id => id != s.playerId && Allegiance.InHouse(s, id));
            return through == null ? null : Cmd(s, EpisodeCommandKind.AllianceMeet, through, null, pact.id);
        }

        /// <summary>
        /// The fixed policy every plan is answered by, by the week and the seed: go with it (a split, the first
        /// nominee who is not the player), push for the other nominee, lie low, or leave it to lapse. The answer
        /// goes through the first member still here.
        /// </summary>
        private static EpisodeCommand Answer(EpisodeState s, uint seed)
        {
            if (s.phase != EpisodePhase.Campaign || !EpisodeEngine.PactPlanRulesOn(s) || !Allegiance.InHouse(s, s.playerId)) return null;
            foreach (var pact in s.alliances.Where(a => a.active && a.members.Contains(s.playerId)))
            {
                var row = PactPlans.OpenPlan(s, pact.id);
                if (row == null) continue;
                string through = PactPlans.StillHere(s, pact, row).FirstOrDefault();
                if (through == null) continue;
                var standing = PactPlans.StandingSays(s, pact, row);
                string plan = PactPlans.MembersPlan(standing);
                string other = plan == null ? null : s.nominees.FirstOrDefault(id => id != plan);
                switch ((int)((s.week + seed) % 4))
                {
                    case 0:
                        string agree = plan != null && plan != s.playerId ? plan : s.nominees.FirstOrDefault(id => id != s.playerId);
                        if (standing.Count > 0 && agree != null) return Cmd(s, EpisodeCommandKind.AnswerPactPlan, through, agree, pact.id);
                        return Cmd(s, EpisodeCommandKind.AnswerPactPlan, through, null, pact.id);
                    case 1:
                        if (other != null && other != s.playerId) return Cmd(s, EpisodeCommandKind.AnswerPactPlan, through, other, pact.id);
                        return Cmd(s, EpisodeCommandKind.AnswerPactPlan, through, null, pact.id);
                    case 2:
                        return Cmd(s, EpisodeCommandKind.AnswerPactPlan, through, null, pact.id);
                    default:
                        continue;
                }
            }
            return null;
        }

        // ------------------------------------------------------------ what one step did

        private static void Check(EpisodeState before, EpisodeCommand command, EpisodeState after, Dictionary<string, int> counts, ref bool firstWarRoom)
        {
            // War rooms held: their says lawful; the season's first, its draws against the same meeting without the rules.
            foreach (var row in after.ledger.plans.Where(p => !before.ledger.plans.Any(b => b.week == p.week && b.allianceId == p.allianceId)))
            {
                Count(counts, "war-room");
                Count(counts, PactPlans.MembersPlan(row.says) != null ? "majority" : "split");
                Count(counts, "quiet", row.present.Count(id => id != after.playerId) - row.says.Count);
                foreach (var say in row.says)
                    if (!before.nominees.Contains(say.targetId) || say.targetId == say.memberId
                        || (before.nominees.Contains(say.memberId) && say.targetId != before.nominees.First(id => id != say.memberId)))
                        Count(counts, "illegal-say");
                if (firstWarRoom && before.ledger.plans.Count == 0 && !before.events.Any(e => e.kind == WaveDEventKinds.PactPlan))
                {
                    var twin = before.Clone();
                    twin.pactPlanRulesStartWeek = 0;
                    var without = new EpisodeEngine(twin).Apply(command);
                    if (!without.accepted || without.state.randomState != after.randomState) Count(counts, "war-room-drew");
                    Count(counts, "first-war-room-checked");
                }
                firstWarRoom = false;
            }

            // Plans settled this step: one line each, the player's call where they back it, no season draw.
            int settled = 0;
            foreach (var row in after.ledger.plans)
            {
                var was = before.ledger.plans.FirstOrDefault(b => b.week == row.week && b.allianceId == row.allianceId);
                if (was == null || was.stance != PactPlanStance.Open || row.stance == PactPlanStance.Open) continue;
                settled++;
                Count(counts, row.stance);
                if (row.stance == PactPlanStance.Countered && row.targetId == row.counterId) Count(counts, "counter-carried");
                if (row.stance == PactPlanStance.Countered)
                {
                    // The counter's odds, re-measured (§6 Q8): every member it could reach - who said the plan,
                    // off the block - and their odds as the answer read them, in hundredths.
                    var pact = before.alliances.FirstOrDefault(a => a.id == row.allianceId);
                    var standing = PactPlans.StandingSays(before, pact, was);
                    string plan = PactPlans.MembersPlan(standing);
                    foreach (var say in standing.Where(x => x.targetId == plan && !before.nominees.Contains(x.memberId)))
                    {
                        Count(counts, "counter-reachable");
                        Count(counts, "counter-odds-hundredths", (int)Math.Round(PactPlans.ComeRoundOdds(before, say.memberId) * 100));
                    }
                }
                Count(counts, "came-round", row.cameRound.Count);
                Count(counts, "with-plan", row.followed.Count);
                Count(counts, "not-with-plan", PactPlans.NotFollowing(after, row).Count);
                if (row.callerId == after.playerId)
                {
                    Count(counts, "player-call");
                    if (after.ledger.calls.Count(k => k.week == row.week && k.allianceId == row.allianceId && k.callerId == after.playerId) != 1)
                        Count(counts, "call-missing");
                }
            }
            int lines = after.events.Count(e => e.sequence >= before.nextSequence && e.kind == WaveDEventKinds.PactPlan);
            if (lines != settled) Count(counts, "line-missing", Math.Abs(lines - settled));
            if (command.kind == EpisodeCommandKind.AnswerPactPlan && after.randomState != before.randomState) Count(counts, "answer-drew");
            if (before.phase == EpisodePhase.Campaign && after.phase == EpisodePhase.Eviction
                && before.ledger.plans.Any(p => p.stance == PactPlanStance.Open))
            {
                var twin = before.Clone();
                twin.ledger.plans.RemoveAll(p => p.stance == PactPlanStance.Open);
                var without = new EpisodeEngine(twin).Apply(command);
                if (!without.accepted || without.state.randomState != after.randomState) Count(counts, "lapse-drew");
            }
            if (after.phase != EpisodePhase.Campaign && after.ledger.plans.Any(p => p.stance == PactPlanStance.Open)) Count(counts, "open-after-campaign");

            // Follow-through at the reveal: who of those with a plan voted with it.
            if (!before.evictionResolved && after.evictionResolved && after.phase == EpisodePhase.Eviction)
                foreach (var row in after.ledger.plans.Where(p => p.week == after.week && !string.IsNullOrEmpty(p.targetId)))
                {
                    foreach (string id in row.followed)
                    {
                        var vote = after.votes.FirstOrDefault(v => v.voterId == id);
                        if (vote != null) Count(counts, vote.targetId == row.targetId ? "followed-through" : "broke-ranks");
                    }
                    if (after.ledger.power.LastOrDefault(p => p.week == after.week)?.evicteeId == row.targetId) Count(counts, "target-evicted");
                }
        }
    }
}
#endif
