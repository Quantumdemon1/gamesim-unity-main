#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The balance lab's own tests (BALANCE plan B2-B4): the smoke tier, which plays one season per policy
    /// and size and proves every player stays legal; determinism; the statistics; the knowledge gate's
    /// structure; the seeds and the performance model. The headline tier is in
    /// <see cref="BalanceLabReports"/>, explicit. The Unity-free run only: Unity's batch runner would play
    /// these seasons under Mono several times slower, and the lab's files are compiled out of Unity.
    /// </summary>
    public sealed class BalanceLabTests
    {
        private static readonly int[] SmokeSizes = { 6, 8, 12 };

        // ---------------------------------------------------------------- the smoke tier

        [Test]
        public void Smoke_EveryPolicyPlaysALegalSeasonAtEverySize()
        {
            var clock = BalanceLab.Clock();
            var runs = BalanceLab.Run(BalanceLab.Grid(BalancePolicies.All, SmokeSizes), 1);
            double seconds = clock.Elapsed.TotalSeconds;
            TestContext.WriteLine("Smoke tier: " + runs.Length + " seasons in " + BalanceLab.Num(seconds, "0.0") + " s on " + Environment.ProcessorCount + " threads.");
            TestContext.WriteLine("| policy | size | outcome | weeks | commands | own | walker | free | refused | top refusal |");
            TestContext.WriteLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (var r in runs)
                TestContext.WriteLine("| " + r.cell.policy + " | " + r.cell.size + " | " + (r.error ?? r.autopsy.outcome + " (" + r.autopsy.placement + ")") + " | " + r.autopsy?.weeks
                    + " | " + r.commands + " | " + r.own + " | " + r.fallbacks + " | " + r.freeActions + " | " + r.refusals + " | "
                    + r.refusalsByKind.OrderByDescending(p => p.Value).Select(p => p.Key + " x" + p.Value).FirstOrDefault() + " |");
            TestContext.WriteLine("Rows: " + BalanceLab.Write("smoke", BalanceLab.Jsonl(runs)));

            foreach (var r in runs)
            {
                string at = r.cell.Key + " seed " + r.seed;
                Assert.That(r.error, Is.Null, at);
                Assert.That(r.autopsy.finished, Is.True, at);
                Assert.That(r.autopsy.houseSize, Is.EqualTo(r.cell.size), at);
                Assert.That(r.autopsy.npcTicks, Is.Zero, at + ": the lab does not drive the NPC world yet (B5).");
                Assert.That(r.pace.weeks, Is.EqualTo(r.autopsy.weeks), at + ": the story's pace was watched to the end.");
                if (r.cell.policy == BalancePolicies.Passive) Assert.That(r.own, Is.Zero, at + ": the passive player leaves every step to the walker.");
                else Assert.That(r.own, Is.GreaterThan(0), at + ": the player acted.");
                // Legal play: the house refuses a player now and then (a deal it will not take, a person
                // somebody else is with), never most of the time. The exploit hunter's refusals are its
                // measurement, so it answers only to the walker and the validator.
                if (r.cell.policy != BalancePolicies.Exploit)
                    Assert.That(r.refusals, Is.LessThanOrEqualTo(Math.Max(6, r.own / 4)), at + ": refusals " + string.Join("; ", r.refusalsByKind.Select(p => p.Key + " x" + p.Value)));
                // A veto holder is never evicted that week: on the block they save themselves, and off it they
                // cannot be named. (The final four's lock binds only a holder off the block; the final
                // eviction has no veto.)
                Assert.That(r.autopsy.weeksPlayed.Where(w => w.playerEvicted && w.playerVetoHolder && !w.finalEviction).Select(w => w.week), Is.Empty,
                    at + ": the player was evicted in a week they held the veto.");
            }
        }

        /// <summary>
        /// Whoever plays - the walker for the passive player, each gated policy, each oracle - a player who holds
        /// the veto at the meeting while on the block comes off it. The meeting - the player in the block's second
        /// chair, where the engine tests' walker saved the other nominee - is found in the passive player's seasons
        /// at eight and played on from there by every player through the lab's own step.
        /// </summary>
        [Test]
        public void APlayerWhoHoldsTheVetoOnTheBlockSavesThemselvesWhoeverPlays()
        {
            var finder = new BalanceLab.Cell { policy = BalancePolicies.Passive, size = 8 };
            EpisodeState meeting = null;
            uint seed = 0;
            for (int index = 0; index < 80 && meeting == null; index++)
            {
                seed = BalanceLab.Seed(finder, index);
                meeting = TheVetoMeetingWithThePlayerOnTheBlock(finder, index);
            }
            Assert.That(meeting, Is.Not.Null, "No season put a veto-holding player in the block's second chair.");
            Assert.That(meeting.nominees[0], Is.Not.EqualTo(meeting.playerId));
            foreach (string name in BalancePolicies.All)
            {
                var engine = new EpisodeEngine(meeting.Clone());
                var agent = BalancePolicies.Create(name);
                var run = new BalanceLab.SeasonRun { cell = new BalanceLab.Cell { policy = name, size = 8 }, seed = seed };
                for (int step = 0; step < 20 && !engine.Snapshot.vetoResolved; step++)
                {
                    var s = engine.Snapshot;
                    var result = BalanceLab.Step(engine, agent, s, run, out var used);
                    Assert.That(result.accepted, Is.True, name + ": " + used.kind + ": " + result.reason);
                }
                var after = engine.Snapshot;
                Assert.That(after.vetoResolved, Is.True, name + ": the meeting was decided.");
                Assert.That(after.nominees, Does.Not.Contain(after.playerId), name + ": the veto holder left themselves on the block.");
            }
        }

        /// <summary>The passive player's season to the first veto meeting at which they hold the veto on the block, or null.</summary>
        private static EpisodeState TheVetoMeetingWithThePlayerOnTheBlock(BalanceLab.Cell cell, int index)
        {
            var run = new BalanceLab.SeasonRun { cell = cell, index = index, seed = BalanceLab.Seed(cell, index) };
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = cell.size }, run.seed);
            ShippedRules.ApplyFresh(fresh);
            var engine = new EpisodeEngine(fresh);
            var agent = BalancePolicies.Create(cell.policy);
            for (int i = 0; i < BalanceLab.CommandCap; i++)
            {
                var s = engine.Snapshot;
                if (s.phase == EpisodePhase.Finished || s.Find(s.playerId).status != ContestantStatus.Active) return null;
                // The second chair: the engine tests' walker saves the first nominee, which there is somebody else.
                if (s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved && s.vetoHolderId == s.playerId && s.nominees.IndexOf(s.playerId) > 0) return s.Clone();
                Assert.That(BalanceLab.Step(engine, agent, s, run, out _).accepted, Is.True);
            }
            return null;
        }

        [Test]
        public void TwoRunsWriteTheSameRowsWhateverTheParallelism()
        {
            var cells = BalanceLab.Grid(new[] { BalancePolicies.Random, BalancePolicies.Schemer, BalancePolicies.Exploit, BalancePolicies.OracleSkilled }, new[] { 6 });
            string serial = BalanceLab.Jsonl(BalanceLab.Run(cells, 2, parallelism: 1));
            string parallel = BalanceLab.Jsonl(BalanceLab.Run(cells, 2));
            Assert.That(parallel, Is.EqualTo(serial));
            Assert.That(serial.Split('\n').Count(line => line.Length > 0), Is.EqualTo(8));
        }

        // ---------------------------------------------------------------- seeds and performance

        [Test]
        public void EveryPolicyPlaysTheSameSeasonsAndEachHouseItsOwn()
        {
            var a = new BalanceLab.Cell { policy = BalancePolicies.Passive, size = 8 };
            var b = new BalanceLab.Cell { policy = BalancePolicies.Schemer, size = 8, performance = PerformanceModel.Fixed(1) };
            var c = new BalanceLab.Cell { policy = BalancePolicies.Passive, size = 12 };
            var seeds = Enumerable.Range(0, 200).Select(i => BalanceLab.Seed(a, i)).ToList();
            Assert.That(Enumerable.Range(0, 200).Select(i => BalanceLab.Seed(b, i)), Is.EqualTo(seeds), "Policy and performance are not in the seed.");
            Assert.That(seeds.Distinct().Count(), Is.EqualTo(200));
            Assert.That(Enumerable.Range(0, 200).Select(i => BalanceLab.Seed(c, i)).Intersect(seeds), Is.Empty, "Each house size its own seasons.");
        }

        [Test]
        public void PerformanceIsKeyedToTheCompetitionAndStaysInRange()
        {
            foreach (double level in PerformanceModel.Levels)
                Assert.That(PerformanceModel.Draw(PerformanceModel.Fixed(level), BalancePolicies.Novice, 7u, 3, EpisodePhase.Veto), Is.EqualTo(level));
            double Mean(string policy) => Enumerable.Range(0, 4000)
                .Select(i => PerformanceModel.Draw(PerformanceModel.ByPolicy, policy, (uint)i, 1 + i % 9, i % 2 == 0 ? EpisodePhase.HoH : EpisodePhase.Veto)).Average();
            Assert.That(Mean(BalancePolicies.Beast), Is.EqualTo(0.79).Within(0.02));
            Assert.That(Mean(BalancePolicies.Novice), Is.EqualTo(0.37).Within(0.02));
            Assert.That(Mean(BalancePolicies.Reader), Is.EqualTo(0.5).Within(0.02));
            for (int i = 0; i < 500; i++)
            {
                double draw = PerformanceModel.Draw(PerformanceModel.ByPolicy, BalancePolicies.Random, (uint)i, i % 7 + 1, EpisodePhase.FinalHoHPart1);
                Assert.That(draw, Is.InRange(0.0, 1.0));
                Assert.That(PerformanceModel.Draw(PerformanceModel.ByPolicy, BalancePolicies.Random, (uint)i, i % 7 + 1, EpisodePhase.FinalHoHPart1), Is.EqualTo(draw), "The same competition, the same draw.");
            }
            // The same competition's luck for every average player: their draws are one draw.
            Assert.That(PerformanceModel.Draw(PerformanceModel.ByPolicy, BalancePolicies.Reader, 11u, 2, EpisodePhase.HoH),
                Is.EqualTo(PerformanceModel.Draw(PerformanceModel.ByPolicy, BalancePolicies.Social, 11u, 2, EpisodePhase.HoH)));
        }

        // ---------------------------------------------------------------- statistics

        [Test]
        public void TheWilsonIntervalMatchesItsFormula()
        {
            var ci = BalanceLab.Wilson(10, 80);
            Assert.That(ci.low, Is.EqualTo(0.0694).Within(0.0005));
            Assert.That(ci.high, Is.EqualTo(0.2153).Within(0.0005));
            var none = BalanceLab.Wilson(0, 10);
            Assert.That(none.low, Is.EqualTo(0).Within(1e-12));
            Assert.That(none.high, Is.EqualTo(0.2775).Within(0.0005));
            var all = BalanceLab.Wilson(800, 800);
            Assert.That(all.high, Is.EqualTo(1).Within(1e-12));
        }

        [Test]
        public void McNemarIsExactForFewDiscordantPairsAndChiSquareForMany()
        {
            bool[] Repeat(params (bool value, int times)[] parts) => parts.SelectMany(p => Enumerable.Repeat(p.value, p.times)).ToArray();
            // b = 10, c = 2: exact two-sided p = 2 * (1 + 12 + 66) / 4096.
            var first = Repeat((true, 10), (false, 2), (true, 5), (false, 20));
            var second = Repeat((false, 10), (true, 2), (true, 5), (false, 20));
            var exact = BalanceLab.McNemar(first, second);
            Assert.That((exact.b, exact.c), Is.EqualTo((10, 2)));
            Assert.That(exact.p, Is.EqualTo(158.0 / 4096).Within(1e-9));
            // b = 30, c = 15: chi-square (|30 - 15| - 1)^2 / 45 = 4.3556, p = 0.0369.
            var many = BalanceLab.McNemar(Repeat((true, 30), (false, 15), (false, 40)), Repeat((false, 30), (true, 15), (false, 40)));
            Assert.That(many.p, Is.EqualTo(0.0369).Within(0.0005));
            Assert.That(BalanceLab.McNemar(new[] { true, false }, new[] { true, false }).p, Is.EqualTo(1));
        }

        [Test]
        public void SpearmanRanksAndTheBootstrapIsSeeded()
        {
            Assert.That(BalanceLab.Spearman(new double[] { 1, 2, 3, 4 }, new double[] { 10, 20, 30, 40 }), Is.EqualTo(1).Within(1e-12));
            Assert.That(BalanceLab.Spearman(new double[] { 1, 2, 3, 4 }, new double[] { 4, 3, 2, 1 }), Is.EqualTo(-1).Within(1e-12));
            // Ties take their average rank: x ranks 1, 2.5, 2.5, 4 against y's 1..4.
            Assert.That(BalanceLab.Spearman(new double[] { 1, 2, 2, 3 }, new double[] { 1, 2, 3, 4 }), Is.EqualTo(0.9487).Within(0.0001));
            var values = Enumerable.Range(0, 200).Select(i => (double)(i % 10)).ToList();
            var ci = BalanceLab.Bootstrap(values);
            Assert.That(BalanceLab.Bootstrap(values), Is.EqualTo(ci), "Seeded: the same interval twice.");
            Assert.That(values.Average(), Is.InRange(ci.low, ci.high));
        }

        // ---------------------------------------------------------------- the knowledge gate

        /// <summary>
        /// The row types the player is handed by a reader because they are what the player was told: a
        /// houseguest's word about their vote, as heard or overheard (<see cref="VoteRead.VoterRead"/>'s claims).
        /// </summary>
        private static readonly Type[] PlayerKnown = { typeof(ClaimRow) };

        /// <summary>
        /// The types a policy must never be handed: the engine, and the season with every state type reachable
        /// from it - its public fields' and properties' types, through collections and generic arguments, enums,
        /// primitives and strings aside - less <see cref="PlayerKnown"/>. Built, not listed, so a state type
        /// added later is hidden without anybody remembering to add it.
        /// </summary>
        private static readonly HashSet<Type> Hidden = HiddenTypes();

        private static HashSet<Type> HiddenTypes()
        {
            var hidden = StateClosure();
            hidden.ExceptWith(PlayerKnown);
            return hidden;
        }

        /// <summary>The engine, and every Gamesim type reachable from the season's public members.</summary>
        private static HashSet<Type> StateClosure()
        {
            var seen = new HashSet<Type>();
            var queue = new Queue<Type>();
            void Visit(Type type)
            {
                if (type == null || type == typeof(void)) return;
                if (type.IsByRef || type.IsArray || type.IsPointer) { Visit(type.GetElementType()); return; }
                if (type.IsGenericType) foreach (var argument in type.GetGenericArguments()) Visit(argument);
                if (type.IsEnum || type.IsPrimitive || type == typeof(string) || type.Namespace == null || !type.Namespace.StartsWith("Gamesim", StringComparison.Ordinal)) return;
                if (seen.Add(type)) queue.Enqueue(type);
            }
            Visit(typeof(EpisodeState));
            while (queue.Count > 0)
            {
                var type = queue.Dequeue();
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public)) Visit(field.FieldType);
                foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)) Visit(property.PropertyType);
            }
            seen.Add(typeof(EpisodeEngine));
            seen.Add(typeof(CommandResult));
            return seen;
        }

        /// <summary>The closure finds what was once listed by hand: the season's own state objects.</summary>
        [Test]
        public void TheHiddenTypesAreTheSeasonsWholeState()
        {
            var listed = new[]
            {
                typeof(EpisodeState), typeof(EpisodeEngine), typeof(CommandResult), typeof(ContestantState), typeof(ContestantStats), typeof(RelationshipState),
                typeof(AllianceState), typeof(DealState), typeof(PromiseState), typeof(MemoryState), typeof(SeasonLedger), typeof(CompetitionRow),
                typeof(StoryWorldState), typeof(NpcSocialState), typeof(HouseEventState), typeof(ReplyCardState), typeof(StorylineState),
            };
            Assert.That(listed.Where(type => !Hidden.Contains(type)).Select(type => type.Name), Is.Empty);
            Assert.That(Hidden.Count, Is.GreaterThan(listed.Length), "The closure reaches past the hand-made list.");
            var closure = StateClosure();
            Assert.That(PlayerKnown.Where(type => !closure.Contains(type)).Select(type => type.Name), Is.Empty,
                "An allowed type is a piece of the state the player was told, or it needs no allowance.");
        }

        /// <summary>
        /// Gating is structural: every type reachable from what <see cref="PlayerView"/> hands out - its public
        /// members' types, and theirs, through fields, properties, collections and tuples - is a record of its own
        /// or a reader's, never the season or a piece of it (<see cref="Hidden"/>: the whole state, less what the
        /// player was told). And the policies take a view, never a state.
        /// </summary>
        [Test]
        public void ThePlayerViewHandsOutNothingOfTheSeasonItself()
        {
            var seen = new HashSet<Type>();
            var queue = new Queue<Type>();
            void Visit(Type type)
            {
                if (type == null || type == typeof(void)) return;
                if (type.IsByRef || type.IsArray || type.IsPointer) { Visit(type.GetElementType()); return; }
                if (type.IsGenericType) foreach (var argument in type.GetGenericArguments()) Visit(argument);
                if (!seen.Add(type)) return;
                queue.Enqueue(type);
            }
            foreach (var member in typeof(PlayerView).GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (member is PropertyInfo property) Visit(property.PropertyType);
                if (member is FieldInfo field) Visit(field.FieldType);
                if (member is MethodInfo method) { Visit(method.ReturnType); foreach (var p in method.GetParameters()) Visit(p.ParameterType); }
            }
            while (queue.Count > 0)
            {
                var type = queue.Dequeue();
                if (type.Namespace == null || !type.Namespace.StartsWith("Gamesim", StringComparison.Ordinal)) continue;
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public)) Visit(field.FieldType);
                foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)) Visit(property.PropertyType);
            }
            var leaks = seen.Where(type => Hidden.Contains(type)).Select(type => type.Name).ToList();
            Assert.That(seen.Count, Is.GreaterThan(10), "The view's records were walked.");
            Assert.That(leaks, Is.Empty, "PlayerView hands out a piece of the season.");
            Assert.That(typeof(PlayerView).GetFields(BindingFlags.Instance | BindingFlags.Public), Is.Empty, "No public field.");

            var policies = typeof(GatedPolicy).Assembly.GetTypes().Where(t => typeof(IBalancePolicy).IsAssignableFrom(t) && !t.IsInterface).ToList();
            Assert.That(policies.Count(t => !t.IsAbstract), Is.EqualTo(BalancePolicies.Gated.Length), "Every gated player was found.");
            foreach (var policy in policies)
                foreach (var member in policy.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    var types = new List<Type>();
                    if (member is FieldInfo field) types.Add(field.FieldType);
                    if (member is PropertyInfo property) types.Add(property.PropertyType);
                    if (member is MethodInfo method) { types.Add(method.ReturnType); types.AddRange(method.GetParameters().Select(p => p.ParameterType)); }
                    Assert.That(types.Where(t => Hidden.Contains(t)), Is.Empty, policy.Name + "." + member.Name + " takes or keeps a piece of the season.");
                }
            foreach (string name in BalancePolicies.All)
                Assert.That(BalancePolicies.Create(name) is IOraclePolicy, Is.EqualTo(BalancePolicies.IsOracle(name)), name + ": only the labelled oracles read the season.");
        }

        /// <summary>
        /// <see cref="KnownOdds"/>, which the view hands every policy, reads only what the player knows: across
        /// the seasons of a smoke run, a houseguest's hidden view of the player (where the player holds no read),
        /// the views between houseguests the player learned nothing of, and every statistic can all be changed
        /// without moving one estimate the player is shown.
        /// </summary>
        [Test]
        public void KnownOddsReadOnlyWhatThePlayerKnows()
        {
            int compared = 0;
            foreach (string policy in new[] { BalancePolicies.Reader, BalancePolicies.Schemer })
            {
                var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 9100u);
                ShippedRules.ApplyFresh(fresh);
                var engine = new EpisodeEngine(fresh);
                var agent = (IBalancePolicy)BalancePolicies.Create(policy);
                for (int i = 0; i < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
                {
                    var s = engine.Snapshot;
                    if (i % 7 == 0 && s.Find(s.playerId).status == ContestantStatus.Active) { Compare(s, i); compared++; }
                    var command = agent.Next(new PlayerView(s, 9100u));
                    var result = command == null ? null : engine.Apply(command);
                    if (result == null || !result.accepted) Assert.That(engine.Apply(BalanceLab.Walker(s)).accepted, Is.True);
                }
            }
            Assert.That(compared, Is.GreaterThan(20), "States across the season were compared.");
        }

        private static void Compare(EpisodeState s, int salt)
        {
            var hidden = s.Clone();
            var random = new SeededRandom((uint)(salt * 7919 + 13));
            string me = hidden.playerId;
            foreach (var r in hidden.relationships)
            {
                bool aboutMe = r.toId == me, mine = r.fromId == me;
                if (mine) continue;
                if (aboutMe ? KnownOdds.HasRead(s, r.fromId) : KnownOdds.Band(s, r.fromId, r.toId) != null) continue;
                r.score = Math.Round(random.NextDouble() * 200 - 100);
            }
            foreach (var c in hidden.contestants.Where(c => !c.isPlayer))
                c.stats = new ContestantStats { physical = random.NextDouble() * 10, mental = random.NextDouble() * 10, endurance = random.NextDouble() * 10,
                    social = random.NextDouble() * 10, luck = random.NextDouble() * 10, competition = random.NextDouble() * 10, strategic = random.NextDouble() * 10, loyalty = random.NextDouble() * 10 };
            string Odds(EpisodeState state)
            {
                var lines = new List<string>();
                var npcs = state.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
                foreach (var npc in npcs)
                {
                    lines.Add(npc + " alliance " + Show(KnownOdds.Alliance(state, npc)) + " presumed " + KnownOdds.PresumedView(state, npc));
                    foreach (var type in new[] { DealKind.InformationSharing, DealKind.FinalTwo, DealKind.SafetyAgreement, DealKind.VoteTogether, DealKind.Partnership })
                        lines.Add(npc + " " + type + " " + Show(KnownOdds.Deal(state, npc, type, null)));
                    foreach (var about in npcs.Where(x => x != npc))
                        foreach (var type in new[] { DealKind.TargetAgreement, DealKind.VoteEvict, DealKind.VoteSave })
                            lines.Add(npc + " " + type + " " + about + " " + Show(KnownOdds.Deal(state, npc, type, about)));
                    foreach (var ask in LobbyAsk.All)
                        foreach (var approach in LobbyApproach.All)
                            lines.Add(npc + " plea " + ask + "/" + approach + " " + Show(KnownOdds.Plea(state, npc, ask, npcs.FirstOrDefault(x => x != npc), approach)));
                }
                return string.Join("\n", lines);
            }
            Assert.That(Odds(hidden), Is.EqualTo(Odds(s)), "week " + s.week + " " + s.phase + ": an estimate moved with something the player cannot know.");
        }

        private static string Show(KnownOdds.Estimate e) =>
            e.chance.ToString("R", CultureInfo.InvariantCulture) + " " + e.word + " " + e.unknowns + " " + e.read + e.claim + e.history + e.aboutKnown + e.grudge;
    }
}
#endif
