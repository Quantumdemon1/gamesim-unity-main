#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gamesim.Presentation;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The balance lab (BALANCE plan B.4, slices B2-B4): whole seasons the game ships, played by scripted
    /// players who see only what a player sees, measured by <see cref="SeasonAutopsy"/>.
    ///
    /// <para><b>A season</b> is built as the director builds one (<see cref="SeasonBuilder.Create"/> and
    /// <see cref="ShippedRules.ApplyFresh"/>) and played through the public engine. Each step the policy
    /// proposes (up to three times, told each refusal); when it proposes nothing, or nothing it proposes is
    /// accepted, the engine tests' walker takes the phase's own step. Every competition's performance comes
    /// from <see cref="PerformanceModel"/>. The state is validated at every phase change and at the end.
    /// The NPC world is not driven (B5): no NPC-to-NPC conversation happens in these seasons.</para>
    ///
    /// <para><b>Seeds</b> are a hash of the house (roster, size, NPC budget) and the index, never of the policy
    /// or the performance model, so every player plays the same seasons and pairs compare (McNemar).</para>
    ///
    /// <para><b>Parallel and deterministic.</b> Seasons run in <see cref="Parallel.For(int, int, Action{int})"/>
    /// into an indexed array; nothing in a row depends on the run's timing or order, so two runs write the
    /// same JSONL byte for byte.</para>
    /// </summary>
    internal static class BalanceLab
    {
        /// <summary>The longest season the lab will walk before calling it stuck.</summary>
        public const int CommandCap = 6000;
        /// <summary>How many times a policy may propose at one moment before the walker steps in.</summary>
        public const int Attempts = 3;

        // ---------------------------------------------------------------- the grid

        internal sealed class Cell
        {
            public string policy;
            public int size;
            public CastTemplates.Roster roster = CastTemplates.Roster.Regular;
            public string performance = PerformanceModel.ByPolicy;
            /// <summary>NPC-world ticks a week; 0 until the B5 driver exists.</summary>
            public int npcTicks;

            public string House => roster + "/" + size.ToString(CultureInfo.InvariantCulture) + "/npc" + npcTicks.ToString(CultureInfo.InvariantCulture);
            public string Key => policy + "/" + House + "/" + performance;
            public override string ToString() => Key;
        }

        internal static List<Cell> Grid(IEnumerable<string> policies, IEnumerable<int> sizes, string performance = PerformanceModel.ByPolicy,
            CastTemplates.Roster roster = CastTemplates.Roster.Regular) =>
            (from size in sizes from policy in policies select new Cell { policy = policy, size = size, roster = roster, performance = performance }).ToList();

        /// <summary>A season's seed: a hash of its house and its index, the same for every policy and performance model.</summary>
        public static uint Seed(Cell cell, int index) =>
            SeededRandom.HashSeed("balance-lab/v1/" + cell.House + "/" + index.ToString(CultureInfo.InvariantCulture));

        // ---------------------------------------------------------------- one season

        internal sealed class SeasonRun
        {
            public Cell cell;
            public int index;
            public uint seed;
            public string error;
            public SeasonAutopsy.Report autopsy;
            public int commands, own, fallbacks, refusals, freeActions;
            public readonly SortedDictionary<string, int> refusalsByKind = new SortedDictionary<string, int>(StringComparer.Ordinal);
            public readonly SortedDictionary<int, int> decisionsByWeek = new SortedDictionary<int, int>();
            public readonly SortedDictionary<int, double> ceremonyByWeek = new SortedDictionary<int, double>();
            public double finaleSeconds;
            public bool Won => autopsy != null && autopsy.outcome == SeasonAutopsy.Outcomes.Winner;
            public bool FinalTwo => autopsy != null && (autopsy.outcome == SeasonAutopsy.Outcomes.Winner || autopsy.outcome == SeasonAutopsy.Outcomes.RunnerUp);
        }

        /// <summary>The kinds the player does for nothing: no seat is spent on them.</summary>
        private static readonly HashSet<EpisodeCommandKind> Free = new HashSet<EpisodeCommandKind>
        {
            EpisodeCommandKind.AskVote, EpisodeCommandKind.ReadPerson, EpisodeCommandKind.WitnessProximity, EpisodeCommandKind.ReplyToHouseguest,
            EpisodeCommandKind.RespondToDeal, EpisodeCommandKind.SwearLoyalty, EpisodeCommandKind.DeclineLoyalty, EpisodeCommandKind.RenameAlliance,
            EpisodeCommandKind.Introduce, EpisodeCommandKind.BuyActionPoint, EpisodeCommandKind.ResolveHouseEvent,
        };

        internal static SeasonRun Play(Cell cell, int index)
        {
            var run = new SeasonRun { cell = cell, index = index, seed = Seed(cell, index) };
            try { PlayInto(run); }
            catch (Exception error) { run.error = "exception: " + error.GetType().Name + ": " + error.Message; }
            return run;
        }

        private static void PlayInto(SeasonRun run)
        {
            var cell = run.cell;
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = cell.size, Roster = cell.roster }, run.seed);
            ShippedRules.ApplyFresh(fresh);
            var engine = new EpisodeEngine(fresh);
            var agent = BalancePolicies.Create(cell.policy);
            var changes = new List<SeasonAutopsy.PhaseChange>();
            var s = engine.Snapshot;
            while (s.phase != EpisodePhase.Finished && run.commands < CommandCap)
            {
                CommandResult applied = null;
                EpisodeCommand used = null;
                // An oracle proposes once: it keeps no notes, so a refused proposal would only come again.
                int attempts = agent is IBalancePolicy ? Attempts : 1;
                for (int attempt = 0; attempt < attempts && applied == null; attempt++)
                {
                    var command = agent is IBalancePolicy policy ? policy.Next(new PlayerView(s, run.seed)) : ((IOraclePolicy)agent).Next(s, run.seed);
                    if (command == null) break;
                    Perform(command, s, run);
                    var result = engine.Apply(command);
                    if (result.accepted) { applied = result; used = command; run.own++; break; }
                    run.refusals++;
                    Count(run.refusalsByKind, command.kind + ": " + result.reason);
                    (agent as IBalancePolicy)?.Refused(command, result.reason);
                }
                if (applied == null)
                {
                    used = Walker(s);
                    Perform(used, s, run);
                    applied = engine.Apply(used);
                    if (!applied.accepted)
                    {
                        run.error = "week " + s.week + " " + s.phase + "/" + s.evictionStage + ": the walker's " + used.kind + " was refused: " + applied.reason;
                        break;
                    }
                    run.fallbacks++;
                }
                run.commands++;
                if (used.kind != EpisodeCommandKind.Advance) Count(run.decisionsByWeek, s.week);
                if (Free.Contains(used.kind)) run.freeActions++;
                var after = applied.state;
                if (SeasonAutopsy.IsPhaseChange(s, after))
                {
                    changes.Add(new SeasonAutopsy.PhaseChange(s, after));
                    if (!EpisodeValidation.TryValidate(after, out string invalid)) { run.error = "week " + after.week + " " + after.phase + ": invalid: " + invalid; break; }
                }
                s = after;
            }
            if (run.error == null && s.phase != EpisodePhase.Finished) run.error = "did not finish in " + run.commands + " commands (week " + s.week + " " + s.phase + ")";
            if (run.error == null && !EpisodeValidation.TryValidate(s, out string finalError)) run.error = "the final state is invalid: " + finalError;
            run.autopsy = SeasonAutopsy.Of(changes, s);
            Ceremonies(run);
        }

        /// <summary>A competition's performance is the model's, whoever issued the command: skill is not a decision.</summary>
        private static void Perform(EpisodeCommand command, EpisodeState s, SeasonRun run)
        {
            if (command.kind == EpisodeCommandKind.Compete)
                command.performance = PerformanceModel.Draw(run.cell.performance, run.cell.policy, run.seed, s.week, s.phase);
        }

        /// <summary>The engine tests' walker, answering a finale question with an offered response, and a production-removed player sitting the jury out.</summary>
        internal static EpisodeCommand Walker(EpisodeState s)
        {
            var command = EpisodeEngineTests.NextCommand(s);
            command.id = "walk-" + s.revision.ToString(CultureInfo.InvariantCulture);
            if (command.kind == EpisodeCommandKind.AnswerJury && EpisodeEngine.FinaleOn(s))
            {
                var exchange = s.juryExchanges[s.juryQuestionIndex];
                if (exchange.finalistId == s.playerId) command.secondTargetId = FinaleQuestions.Offered(exchange.category, exchange.receiptKind).First();
            }
            if (command.kind == EpisodeCommandKind.CastVote && s.phase == EpisodePhase.Jury && s.Find(s.playerId).status == ContestantStatus.Expelled)
                command.kind = EpisodeCommandKind.Advance;
            return command;
        }

        private static void Count<T>(SortedDictionary<T, int> into, T key) => into[key] = (into.TryGetValue(key, out int n) ? n : 0) + 1;

        // ---------------------------------------------------------------- ceremony seconds

        /// <summary>
        /// The week's ceremonies at the suspenseful pace, fade to fade, without a skip: the key ceremony, the
        /// veto meeting and the live eviction (CeremonyPacing's own timings), from the autopsy's counts.
        /// The finale's jury reveal is counted apart.
        /// </summary>
        private static void Ceremonies(SeasonRun run)
        {
            const CeremonyPace pace = CeremonyPace.Suspenseful;
            foreach (var week in run.autopsy.weeksPlayed.Where(w => !w.finalEviction))
            {
                double keys = CeremonyPacing.FadeIn + CeremonyPacing.KeyIntro(pace) + week.keys * CeremonyPacing.PerKey(pace, week.keys)
                    + CeremonyPacing.LastKeyBeat(pace) + CeremonyPacing.BlockHold(pace) + CeremonyPacing.FadeOut;
                double veto = CeremonyPacing.VetoMeeting(pace, week.vetoUsed);
                double eviction = CeremonyPacing.FadeIn + CeremonyPacing.VoteIntro(pace) + week.votes * CeremonyPacing.PerVote(pace, week.votes)
                    + CeremonyPacing.LastVoteBeat(pace) + (week.tie ? CeremonyPacing.TieBeat(pace) : 0) + CeremonyPacing.ResultHold(pace) + CeremonyPacing.FadeOut;
                run.ceremonyByWeek[week.week] = Math.Round(keys + veto + eviction, 2);
            }
            int jurors = run.autopsy.jury.jurors;
            if (jurors > 0)
                run.finaleSeconds = Math.Round(CeremonyPacing.FadeIn + CeremonyPacing.JuryIntro(pace) + jurors * CeremonyPacing.PerJuror(pace, jurors)
                    + CeremonyPacing.WinnerHold(pace) + CeremonyPacing.FadeOut, 2);
        }

        // ---------------------------------------------------------------- many seasons

        /// <summary>Every cell's seasons 0..n-1, in parallel, into an array ordered by cell then index.</summary>
        internal static SeasonRun[] Run(IReadOnlyList<Cell> cells, int seasonsPerCell, int parallelism = 0)
        {
            var runs = new SeasonRun[cells.Count * seasonsPerCell];
            var options = new ParallelOptions { MaxDegreeOfParallelism = parallelism > 0 ? parallelism : Environment.ProcessorCount };
            Parallel.For(0, runs.Length, options, i => runs[i] = Play(cells[i / seasonsPerCell], i % seasonsPerCell));
            return runs;
        }

        // ---------------------------------------------------------------- JSONL

        /// <summary>One season as one line: what the reports read, in a fixed order, nothing timed.</summary>
        internal static string Row(SeasonRun r)
        {
            var a = r.autopsy;
            var you = a?.houseguests.FirstOrDefault(h => h.isPlayer);
            var npcs = a?.houseguests.Where(h => !h.isPlayer).ToList() ?? new List<SeasonAutopsy.Houseguest>();
            var mine = a?.competitions.Where(c => c.playerInField).ToList() ?? new List<SeasonAutopsy.CompetitionResult>();
            var row = new
            {
                policy = r.cell.policy, size = r.cell.size, roster = r.cell.roster.ToString(), performance = r.cell.performance, npcTicks = r.cell.npcTicks,
                index = r.index, seed = r.seed, error = r.error,
                outcome = a?.outcome, placement = a?.placement ?? 0, outWeek = a?.playerOutWeek ?? 0, weeks = a?.weeks ?? 0,
                won = r.Won, finalTwo = r.FinalTwo,
                hohWins = a?.playerHohWins ?? 0, vetoWins = a?.playerVetoWins ?? 0, nominated = a?.playerTimesNominated ?? 0,
                nominatedWeek1 = a?.weeksPlayed.Any(w => w.week == 1 && w.playerNominated) ?? false,
                evictedWeek1 = a?.weeksPlayed.Any(w => w.week == 1 && w.playerEvicted) ?? false,
                gameSense = a?.gameSense ?? 0, gsCompetitions = a?.gameSenseCompetitions ?? 0, gsStrategy = a?.gameSenseStrategy ?? 0, gsSocial = a?.gameSenseSocial ?? 0,
                competitions = mine.Select(c => new { w = c.week, p = c.phase, cat = c.category, f = c.field, won = c.winnerIsPlayer, place = c.playerPlacement, perf = Math.Round(c.playerPerformance, 4), entry = c.playerEntry }),
                npcs = npcs.Select(h => new { stats = h.statSum, wins = h.hohWins + h.vetoWins, noms = h.timesNominated, outWeek = h.outWeek, placement = h.placement }),
                weeklyComps = a?.competitions.Where(c => c.phase == "HoH" || c.phase == "Veto").Select(c => new { w = c.week, p = c.phase, f = c.field, rank = c.winnerStatRank, you = c.winnerIsPlayer }),
                evictions = a?.weeksPlayed.Where(w => w.evicteeId != null).Select(w => new { w = w.week, threat = w.evicteeThreatRank, comps = w.evicteeCompetitionWins, pact = w.evicteeInPact, you = w.playerEvicted, final = w.finalEviction }),
                pacts = a == null ? null : new { player = a.commitments.playerPacts, npc = a.commitments.npcPacts, story = a.commitments.storyPacts, playerEnded = a.commitments.playerPactsEnded, npcEnded = a.commitments.npcPactsEnded },
                deals = a == null ? null : new
                {
                    player = a.commitments.playerDeals, playerKept = a.commitments.playerDealsKept, playerBroken = a.commitments.playerDealsBroken,
                    npc = a.commitments.npcDeals, npcKept = a.commitments.npcDealsKept, npcBroken = a.commitments.npcDealsBroken,
                    promises = a.commitments.playerPromises, promisesKept = a.commitments.playerPromisesKept, promisesBroken = a.commitments.playerPromisesBroken,
                    ceilingWeeks = a.commitments.ceilingWeeks, oaths = a.commitments.oathsSworn,
                },
                economy = a == null ? null : new
                {
                    offered = a.economy.seatsOffered, spent = a.economy.seatsSpent, wasted = a.economy.seatsWasted, purchases = a.economy.purchases,
                    fromOne = a.economy.purchasesFromOne, fromEveryone = a.economy.purchasesFromEveryone, goodwill = a.economy.goodwillPaid, haveNotWeeks = a.economy.haveNotWeeks,
                },
                agency = a == null ? null : new { nominations = a.agency.npcNominations, topThreat = a.agency.topThreatNominated, agendas = new SortedDictionary<string, int>(a.agency.agendas, StringComparer.Ordinal) },
                warmest = a?.warmest?.mutual ?? 0, coldest = a?.coldest?.mutual ?? 0,
                jury = a == null ? null : new { jurors = a.jury.jurors, margin = a.jury.margin, bitter = a.jury.bitterJurors, withEvictor = a.jury.jurorsWithEvictorFinalist, playerVotes = a.jury.playerVotes },
                story = a == null ? null : new { a.story.storylines, a.story.completed, a.story.asks, a.story.weeksWithACard, a.story.npcRemovals, a.story.showmances, a.story.pileOns, a.story.pariahWeeks },
                commands = r.commands, own = r.own, fallbacks = r.fallbacks, freeActions = r.freeActions, refusals = r.refusals, refusalsBy = r.refusalsByKind,
                decisions = r.decisionsByWeek, ceremony = r.ceremonyByWeek, finaleSeconds = r.finaleSeconds,
                npcTicksUsed = a?.npcTicks ?? 0,
            };
            return JsonConvert.SerializeObject(row, Formatting.None, new JsonSerializerSettings { Culture = CultureInfo.InvariantCulture });
        }

        internal static string Jsonl(IEnumerable<SeasonRun> runs) => string.Join("\n", runs.Select(Row)) + "\n";

        /// <summary>Writes a tier's rows to the test's work directory, under balance/, and says where.</summary>
        internal static string Write(string name, string jsonl)
        {
            string folder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "balance");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, name + ".jsonl");
            File.WriteAllText(path, jsonl, new UTF8Encoding(false));
            return path;
        }

        // ---------------------------------------------------------------- statistics

        /// <summary>The Wilson score interval for a rate, at 95% by default.</summary>
        internal static (double low, double high) Wilson(int successes, int n, double z = 1.959963984540054)
        {
            if (n <= 0) return (0, 1);
            double p = (double)successes / n, z2 = z * z;
            double centre = (p + z2 / (2 * n)) / (1 + z2 / n);
            double half = z * Math.Sqrt(p * (1 - p) / n + z2 / (4.0 * n * n)) / (1 + z2 / n);
            return (Math.Max(0, centre - half), Math.Min(1, centre + half));
        }

        /// <summary>
        /// McNemar's test for two players on the same seasons: b where only the first succeeded, c where only
        /// the second did, and the two-sided p - exact (binomial) under 25 discordant pairs, the continuity-
        /// corrected chi-square above.
        /// </summary>
        internal static (int b, int c, double p) McNemar(IReadOnlyList<bool> first, IReadOnlyList<bool> second)
        {
            if (first.Count != second.Count) throw new ArgumentException("McNemar pairs the same seasons.");
            int b = 0, c = 0;
            for (int i = 0; i < first.Count; i++)
            {
                if (first[i] && !second[i]) b++;
                else if (!first[i] && second[i]) c++;
            }
            int n = b + c;
            if (n == 0) return (b, c, 1);
            if (n < 25)
            {
                double tail = 0;
                int k = Math.Min(b, c);
                for (int i = 0; i <= k; i++) tail += Binomial(n, i) * Math.Pow(0.5, n);
                return (b, c, Math.Min(1, 2 * tail));
            }
            double chi = Math.Pow(Math.Abs(b - c) - 1, 2) / n;
            return (b, c, ChiSquareOneTail(chi));
        }

        private static double Binomial(int n, int k)
        {
            double result = 1;
            for (int i = 1; i <= k; i++) result = result * (n - k + i) / i;
            return result;
        }

        /// <summary>P(X ≥ x) for chi-square with one degree of freedom: erfc(sqrt(x/2)).</summary>
        private static double ChiSquareOneTail(double x) => Erfc(Math.Sqrt(x / 2));

        /// <summary>The complementary error function (Numerical Recipes' erfc, relative error under 1.2e-7).</summary>
        private static double Erfc(double x)
        {
            double z = Math.Abs(x), t = 1 / (1 + 0.5 * z);
            double r = t * Math.Exp(-z * z - 1.26551223 + t * (1.00002368 + t * (0.37409196 + t * (0.09678418 + t * (-0.18628806
                + t * (0.27886807 + t * (-1.13520398 + t * (1.48851587 + t * (-0.82215223 + t * 0.17087277)))))))));
            return x >= 0 ? r : 2 - r;
        }

        /// <summary>A percentile bootstrap interval for a mean, from a seeded generator of its own.</summary>
        internal static (double low, double high) Bootstrap(IReadOnlyList<double> values, int resamples = 1000, uint seed = 20261008u)
        {
            if (values.Count == 0) return (0, 0);
            var random = new SeededRandom(seed);
            var means = new double[resamples];
            for (int i = 0; i < resamples; i++)
            {
                double sum = 0;
                for (int j = 0; j < values.Count; j++) sum += values[(int)(random.NextDouble() * values.Count) % values.Count];
                means[i] = sum / values.Count;
            }
            Array.Sort(means);
            return (means[(int)(0.025 * (resamples - 1))], means[(int)Math.Ceiling(0.975 * (resamples - 1))]);
        }

        /// <summary>Spearman's rank correlation, ties given their average rank.</summary>
        internal static double Spearman(IReadOnlyList<double> x, IReadOnlyList<double> y)
        {
            if (x.Count != y.Count || x.Count < 2) return double.NaN;
            var rx = Ranks(x); var ry = Ranks(y);
            double mx = rx.Average(), my = ry.Average(), num = 0, dx = 0, dy = 0;
            for (int i = 0; i < rx.Length; i++) { num += (rx[i] - mx) * (ry[i] - my); dx += (rx[i] - mx) * (rx[i] - mx); dy += (ry[i] - my) * (ry[i] - my); }
            return dx == 0 || dy == 0 ? double.NaN : num / Math.Sqrt(dx * dy);
        }

        private static double[] Ranks(IReadOnlyList<double> values)
        {
            var order = Enumerable.Range(0, values.Count).OrderBy(i => values[i]).ToArray();
            var ranks = new double[values.Count];
            for (int i = 0; i < order.Length;)
            {
                int j = i;
                while (j + 1 < order.Length && values[order[j + 1]] == values[order[i]]) j++;
                double rank = (i + j) / 2.0 + 1;
                for (int k = i; k <= j; k++) ranks[order[k]] = rank;
                i = j + 1;
            }
            return ranks;
        }

        // ---------------------------------------------------------------- formatting

        internal static string Pct(double rate) => (rate * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";
        internal static string Num(double value, string format = "0.00") => double.IsNaN(value) ? "n/a" : value.ToString(format, CultureInfo.InvariantCulture);
        internal static string Interval((double low, double high) ci) => "[" + Pct(ci.low) + ", " + Pct(ci.high) + "]";

        internal static Stopwatch Clock() => Stopwatch.StartNew();
    }
}
#endif
