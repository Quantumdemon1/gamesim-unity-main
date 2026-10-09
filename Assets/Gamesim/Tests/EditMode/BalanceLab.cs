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
    /// from <see cref="PerformanceModel"/>. The state is validated after every Advance, at every phase change
    /// and at the end.
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
            /// <summary>NPC-world ticks a week (B5b's budget, <see cref="BalanceLabNpcWorld"/>); 0 plays no NPC world.</summary>
            public int npcTicks;

            public string House => roster + "/" + size.ToString(CultureInfo.InvariantCulture) + "/npc" + npcTicks.ToString(CultureInfo.InvariantCulture);
            /// <summary>The house the season's seed is drawn for: the roster and size at budget nought, whatever the cell's budget, so budgets pair.</summary>
            public string SeedHouse => roster + "/" + size.ToString(CultureInfo.InvariantCulture) + "/npc0";
            public string Key => policy + "/" + House + "/" + performance;
            public override string ToString() => Key;
        }

        internal static List<Cell> Grid(IEnumerable<string> policies, IEnumerable<int> sizes, string performance = PerformanceModel.ByPolicy,
            CastTemplates.Roster roster = CastTemplates.Roster.Regular, int npcTicks = 0) =>
            (from size in sizes from policy in policies select new Cell { policy = policy, size = size, roster = roster, performance = performance, npcTicks = npcTicks }).ToList();

        /// <summary>
        /// A season's seed: a hash of its house and its index, the same for every policy, performance model and NPC
        /// budget - each budget plays the seasons budget nought plays (decision 2), and at nought the seed is the one it
        /// always was.
        /// </summary>
        public static uint Seed(Cell cell, int index) =>
            SeededRandom.HashSeed("balance-lab/v1/" + cell.SeedHouse + "/" + index.ToString(CultureInfo.InvariantCulture));

        // ---------------------------------------------------------------- one season

        internal sealed class SeasonRun
        {
            public Cell cell;
            public int index;
            public uint seed;
            public string error;
            public SeasonAutopsy.Report autopsy;
            /// <summary>The story's pace as the story pacing report counts it (<see cref="StoryPacingTests.Pace.Watch"/>).</summary>
            public StoryPacingTests.Pace pace;
            public int commands, own, fallbacks, refusals, freeActions;
            public readonly SortedDictionary<string, int> refusalsByKind = new SortedDictionary<string, int>(StringComparer.Ordinal);
            public readonly SortedDictionary<int, int> decisionsByWeek = new SortedDictionary<int, int>();
            public readonly SortedDictionary<int, double> ceremonyByWeek = new SortedDictionary<int, double>();
            public double finaleSeconds;
            /// <summary>Every member a counter of the player's could reach, at every plan the player answered (B6a).</summary>
            public readonly List<CounterMember> counterMembers = new List<CounterMember>();
            /// <summary>
            /// The NPC world (B5b, <see cref="BalanceLabNpcWorld"/>): operations installed, of them ticks and conversation
            /// starts; scans, the pairings they tried and held; operations the engine refused; and the milliseconds the
            /// engine spent preparing them (timed: never in the rows).
            /// </summary>
            public int npcOps, npcTicks, npcStarts, npcScans, npcTries, npcHeld, npcRejected;
            public double npcMilliseconds;
            /// <summary>The final house's pairs of houseguests (the player aside), and those whose mutual view is within ten of the bound (±200) and at it.</summary>
            public int pairs, pairsNearBound, pairsAtBound;
            // T0, the diagnostics' own records (BALANCE plan §4): never in the rows, which the goldens hash.
            /// <summary>Q2: Game Sense's notes at the season's end, their points summed by face and ledger row kind ("strategy/power").</summary>
            public readonly SortedDictionary<string, double> gameSenseRows = new SortedDictionary<string, double>(StringComparer.Ordinal);
            /// <summary>Q4: every NPC Head of Household's nominations while the player was in the house, each candidate's weight in its terms.</summary>
            public readonly List<NominationRecord> nominations = new List<NominationRecord>();
            /// <summary>Q5: the season's first pariah run, as the story pacing report counts one; null for none.</summary>
            public PariahRecord pariah;
            /// <summary>Q3: the player's competition preparation at the season's end (out of five).</summary>
            public int preparation;
            public bool Won => autopsy != null && autopsy.outcome == SeasonAutopsy.Outcomes.Winner;
            public bool FinalTwo => autopsy != null && (autopsy.outcome == SeasonAutopsy.Outcomes.Winner || autopsy.outcome == SeasonAutopsy.Outcomes.RunnerUp);
        }

        /// <summary>Q4: one candidate's <see cref="EpisodeEngine.NominationWeight"/> in its terms (lower is put up first).</summary>
        internal sealed class NominationTerms
        {
            /// <summary>
            /// As the weight sums them: the strategy windows' reluctance and the story's preference (each of which starts
            /// from the HoH's view), minus the HoH's view, minus the overlapping protection, minus the threat. The report
            /// counts the view once (<see cref="BalanceLabDiagnostics.NominationDecomposition"/>).
            /// </summary>
            public double reluctance, story, view, protection, threat, total;

            internal static NominationTerms Of(EpisodeState s, string hohId, string id) => new NominationTerms
            {
                reluctance = StrategyRules.NominationReluctance(s, hohId, id), story = StoryConsumers.NominationPreference(s, hohId, id), view = -s.Score(hohId, id),
                protection = -(UnifiedCommitments.RulesOn(s) ? Math.Min(UnifiedCommitments.StrongestProtection(s, hohId, id).Strength, StoryConsumers.SafetyPreference(s, hohId, id)) : 0),
                threat = -EpisodeEngine.ThreatTerm(s, hohId, id), total = EpisodeEngine.NominationWeight(s, hohId, id),
            };

            internal static NominationTerms Mean(IReadOnlyList<NominationTerms> terms) => terms.Count == 0 ? new NominationTerms() : new NominationTerms
            {
                reluctance = terms.Average(t => t.reluctance), story = terms.Average(t => t.story), view = terms.Average(t => t.view),
                protection = terms.Average(t => t.protection), threat = terms.Average(t => t.threat), total = terms.Average(t => t.total),
            };
        }

        /// <summary>Q4: an NPC Head of Household's nominations: the player's terms, the other candidates' mean, the player's rank (1 put up first) and whether they went up.</summary>
        internal sealed class NominationRecord
        {
            public int week, candidates, playerRank;
            public bool playerNominated;
            public NominationTerms player, others;
        }

        /// <summary>Q5: a pariah run - a houseguest three in the house hold forty against, two Advances running - and what made it.</summary>
        internal sealed class PariahRecord
        {
            /// <summary>The week, those in the house, the holders at forty or more, the target's competition wins, and the most HoH wins of any houseguest then.</summary>
            public int week, houseCount, holders, targetHohWins, targetVetoWins, npcHohWinsMax;
            /// <summary>Each holder's grudge against the target: its cause, as the grudge first came.</summary>
            public List<string> causes = new List<string>();
            public double meanSeverity;
            /// <summary>How many times the holders' grudges were stacked, summed.</summary>
            public int stacks;
        }

        /// <summary>
        /// One member a counter could reach (D3's re-measure as PactPlanSeasonDigests takes it): somebody who said the
        /// members' plan, off the block, with their odds as the answer read them - and what the odds were made of, their
        /// view of the player and the margin of their own lean as the player knows the season, and their traits - so the
        /// counter's constants can be replayed offline (BALANCE plan §4 Q1). Kept for every plan the player answered,
        /// with how they answered it: the members a counter would have faced where the player went with the plan or
        /// lay low. An analyst's record: no policy sees it.
        /// </summary>
        internal sealed class CounterMember
        {
            public int week;
            /// <summary>How the player answered the plan (<see cref="PactPlanStance"/>): only a countered plan's members were put to the coin.</summary>
            public string stance;
            public double odds, view, margin;
            public List<string> traits = new List<string>();
            /// <summary>Whether the player knew them to have turned: then they never come round, whatever the odds.</summary>
            public bool lapsed;
            public bool cameRound, carried;
        }

        /// <summary>The kinds the player does for nothing: no seat is spent on them.</summary>
        private static readonly HashSet<EpisodeCommandKind> Free = new HashSet<EpisodeCommandKind>
        {
            EpisodeCommandKind.AskVote, EpisodeCommandKind.ReadPerson, EpisodeCommandKind.WitnessProximity, EpisodeCommandKind.ReplyToHouseguest,
            EpisodeCommandKind.RespondToDeal, EpisodeCommandKind.SwearLoyalty, EpisodeCommandKind.DeclineLoyalty, EpisodeCommandKind.RenameAlliance,
            EpisodeCommandKind.Introduce, EpisodeCommandKind.BuyActionPoint, EpisodeCommandKind.ResolveHouseEvent, EpisodeCommandKind.AnswerPactPlan,
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
            // The NPC world (B5b): every operation installs a new engine, and every command goes to the world's.
            var world = cell.npcTicks > 0 ? new BalanceLabNpcWorld(engine, cell.npcTicks, run) : null;
            Func<EpisodeEngine> engineOf = world == null ? (Func<EpisodeEngine>)(() => engine) : world.Current;
            var agent = BalancePolicies.Create(cell.policy);
            var changes = new List<SeasonAutopsy.PhaseChange>();
            run.pace = new StoryPacingTests.Pace { removalWindow = fresh.contestants.Count >= 7 };
            string pariahTarget = null;
            int pariahRun = 0;
            var s = engine.Snapshot;
            while (s.phase != EpisodePhase.Finished && run.commands < CommandCap)
            {
                // A slice of the phase's NPC time before the decision (decision 3).
                if (world != null && world.SpendSlice()) s = world.Engine.Snapshot;
                var applied = Step(engineOf, world == null ? (Func<bool>)null : world.SpendRest, agent, s, run, out var used, out var at);
                s = at; // The state the command was applied to: the world may have spent the phase's rest before it.
                if (!applied.accepted)
                {
                    run.error = "week " + s.week + " " + s.phase + "/" + s.evictionStage + ": the walker's " + used.kind + " was refused: " + applied.reason;
                    break;
                }
                run.commands++;
                if (used.kind != EpisodeCommandKind.Advance) Count(run.decisionsByWeek, s.week);
                if (Free.Contains(used.kind)) run.freeActions++;
                var after = applied.state;
                if (used.kind == EpisodeCommandKind.AnswerPactPlan) Counters(s, after, run);
                // T0 Q4: an NPC Head of Household puts two up on an Advance, ranking the house as it stands.
                if (used.kind == EpisodeCommandKind.Advance && s.phase == EpisodePhase.Nomination && s.nominees.Count == 0 && after.nominees.Count == 2
                    && s.hohId != s.playerId && s.Find(s.playerId).status == ContestantStatus.Active)
                    run.nominations.Add(Nominations(s, after));
                if (used.kind == EpisodeCommandKind.Advance) PariahWatch(after, run, ref pariahTarget, ref pariahRun);
                run.pace.Watch(used, after);
                bool phaseChange = SeasonAutopsy.IsPhaseChange(s, after);
                if (phaseChange) changes.Add(new SeasonAutopsy.PhaseChange(s, after));
                if ((phaseChange || used.kind == EpisodeCommandKind.Advance) && !EpisodeValidation.TryValidate(after, out string invalid))
                {
                    run.error = "week " + after.week + " " + after.phase + " after " + used.kind + ": invalid: " + invalid;
                    break;
                }
                s = after;
            }
            if (run.error == null && s.phase != EpisodePhase.Finished) run.error = "did not finish in " + run.commands + " commands (week " + s.week + " " + s.phase + ")";
            if (run.error == null && !EpisodeValidation.TryValidate(s, out string finalError)) run.error = "the final state is invalid: " + finalError;
            run.autopsy = SeasonAutopsy.Of(changes, s);
            run.pace.Close(s);
            Ceremonies(run);
            Saturation(run, s);
            // T0 Q2 and Q3: Game Sense's notes by face and row kind; the player's preparation.
            foreach (var g in GameSense.Evaluate(s).notes.GroupBy(n => n.face + "/" + n.rowKind))
                run.gameSenseRows[g.Key] = Math.Round(g.Sum(n => n.points), 4);
            run.preparation = s.playerStudyBonus;
        }

        /// <summary>T0 Q4: the NPC Head of Household's ranking at the nominations, the player's terms against the other candidates' mean.</summary>
        private static NominationRecord Nominations(EpisodeState s, EpisodeState after)
        {
            var ranked = EpisodeEngine.NominationCandidates(s).Select(c => (c.id, terms: NominationTerms.Of(s, s.hohId, c.id)))
                .OrderBy(x => x.terms.total).ToList();
            var others = ranked.Where(x => x.id != s.playerId).Select(x => x.terms).ToList();
            return new NominationRecord
            {
                week = s.week, candidates = ranked.Count, playerRank = ranked.FindIndex(x => x.id == s.playerId) + 1,
                playerNominated = after.nominees.Contains(s.playerId),
                player = ranked.FirstOrDefault(x => x.id == s.playerId).terms ?? new NominationTerms(), others = NominationTerms.Mean(others),
            };
        }

        /// <summary>T0 Q5: the story pacing report's pariah rule, after every Advance (StoryPacingTests.Pace.Watch, copied), and the season's first run recorded.</summary>
        private static void PariahWatch(EpisodeState after, SeasonRun run, ref string pariahTarget, ref int pariahRun)
        {
            var target = after.Active.Where(c => !c.isPlayer && c.id != after.hohId)
                .Select(c => (c.id, holders: Grudges.HoldersAgainst(after, c.id, 40).Count(h => after.Find(h)?.status == ContestantStatus.Active)))
                .Where(x => x.holders >= 3).Select(x => x.id).OrderBy(id => id, StringComparer.Ordinal).FirstOrDefault();
            if (target != null && target == pariahTarget) { pariahRun++; }
            else { pariahTarget = target; pariahRun = target != null ? 1 : 0; }
            if (pariahRun < 2 || run.pariah != null) return;
            var holders = Grudges.HoldersAgainst(after, target, 40).Where(h => after.Find(h)?.status == ContestantStatus.Active).ToList();
            var grudges = after.story.grudges.Where(g => g.targetId == target && holders.Contains(g.holderId)).ToList();
            var victim = after.Find(target);
            run.pariah = new PariahRecord
            {
                week = after.week, houseCount = after.Active.Count(), holders = holders.Count, targetHohWins = victim.hohWins, targetVetoWins = victim.vetoWins,
                npcHohWinsMax = after.contestants.Where(c => !c.isPlayer).Max(c => c.hohWins),
                causes = grudges.Select(g => g.cause ?? "").ToList(), meanSeverity = grudges.Count == 0 ? 0 : grudges.Average(g => g.severity), stacks = grudges.Sum(g => g.count),
            };
        }

        /// <summary>The final house's pairs of houseguests, the player aside, and those at or near the bound of a mutual view (±200): whether a budget saturates the house (B5b, risk 8).</summary>
        private static void Saturation(SeasonRun run, EpisodeState final)
        {
            var npcs = final.contestants.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            for (int i = 0; i < npcs.Count; i++)
                for (int j = i + 1; j < npcs.Count; j++)
                {
                    double mutual = Math.Abs(final.Score(npcs[i], npcs[j]) + final.Score(npcs[j], npcs[i]));
                    run.pairs++;
                    if (mutual >= 190) run.pairsNearBound++;
                    if (mutual >= 199.5) run.pairsAtBound++;
                }
        }

        /// <summary>
        /// The counters an answer settled (PactPlanSeasonDigests' re-measure, copied): every member who said the
        /// members' plan and is off the block, with their odds as the answer read them.
        /// </summary>
        private static void Counters(EpisodeState before, EpisodeState after, SeasonRun run)
        {
            foreach (var row in after.ledger.plans)
            {
                var was = before.ledger.plans.FirstOrDefault(b => b.week == row.week && b.allianceId == row.allianceId);
                if (was == null || was.stance != PactPlanStance.Open || row.stance == PactPlanStance.Open || row.stance == PactPlanStance.Void) continue;
                var pact = before.alliances.FirstOrDefault(a => a.id == row.allianceId);
                var standing = PactPlans.StandingSays(before, pact, was);
                string plan = PactPlans.MembersPlan(standing);
                var known = Allegiance.AsThePlayerKnows(before);
                foreach (var say in standing.Where(x => x.targetId == plan && !before.nominees.Contains(x.memberId)))
                    run.counterMembers.Add(new CounterMember
                    {
                        week = row.week, stance = row.stance, odds = PactPlans.ComeRoundOdds(before, say.memberId),
                        view = known.Score(say.memberId, known.playerId), margin = WebEvictionVoting.EvaluateNative(known, say.memberId).margin,
                        traits = (known.Find(say.memberId)?.traits ?? new List<string>()).ToList(), lapsed = Allegiance.Lapsed(known, say.memberId),
                        cameRound = row.cameRound != null && row.cameRound.Contains(say.memberId),
                        carried = row.stance == PactPlanStance.Countered && row.targetId == row.counterId,
                    });
            }
        }

        /// <summary>
        /// One step of a season: the player proposes (a gated policy up to <see cref="Attempts"/> times, told each
        /// refusal; an oracle once, as it keeps no notes and a refused proposal would only come again); when it
        /// proposes nothing, or nothing it proposes is accepted, the walker takes the phase's own step. Returns
        /// the accepted result, or the walker's refusal (an error the caller reports).
        /// </summary>
        internal static CommandResult Step(EpisodeEngine engine, object agent, EpisodeState s, SeasonRun run, out EpisodeCommand used) =>
            Step(() => engine, null, agent, s, run, out used, out _);

        internal static CommandResult Step(Func<EpisodeEngine> engineOf, Func<bool> beforeAdvance, object agent, EpisodeState s, SeasonRun run, out EpisodeCommand used) =>
            Step(engineOf, beforeAdvance, agent, s, run, out used, out _);

        /// <summary>
        /// <see cref="Step(EpisodeEngine, object, EpisodeState, SeasonRun, out EpisodeCommand)"/> with an NPC world
        /// (B5b): <paramref name="engineOf"/> is the engine as the world last installed it, and
        /// <paramref name="beforeAdvance"/> spends the rest of the phase's ticks before the Advance that closes it - the
        /// walker's step, or an oracle's own Advance, which is then asked again of the state the world left - and says
        /// whether it spent any. The policies see the season's revision less the world's operations
        /// (<see cref="PlayerView"/>), so their coins fall as they fall without the world. <paramref name="at"/> is
        /// the state the accepted command was applied to: the world may have moved it since <paramref name="s"/>.
        /// </summary>
        internal static CommandResult Step(Func<EpisodeEngine> engineOf, Func<bool> beforeAdvance, object agent, EpisodeState s, SeasonRun run,
            out EpisodeCommand used, out EpisodeState at)
        {
            int attempts = agent is IBalancePolicy ? Attempts : 1;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                var command = agent is IBalancePolicy policy ? policy.Next(new PlayerView(s, run.seed, run.npcOps)) : ((IOraclePolicy)agent).Next(s, run.seed);
                if (command == null) break;
                if (command.kind == EpisodeCommandKind.Advance && beforeAdvance != null && beforeAdvance()) { s = engineOf().Snapshot; attempt--; continue; }
                Perform(command, s, run);
                var result = engineOf().Apply(command);
                if (result.accepted) { used = command; at = s; run.own++; return result; }
                run.refusals++;
                Count(run.refusalsByKind, command.kind + ": " + result.reason);
                (agent as IBalancePolicy)?.Refused(command, result.reason);
            }
            if (beforeAdvance != null && beforeAdvance()) s = engineOf().Snapshot;
            used = Walker(s);
            at = s;
            Perform(used, s, run);
            var walked = engineOf().Apply(used);
            if (walked.accepted) run.fallbacks++;
            return walked;
        }

        /// <summary>A competition's performance is the model's, whoever issued the command: skill is not a decision.</summary>
        private static void Perform(EpisodeCommand command, EpisodeState s, SeasonRun run)
        {
            if (command.kind == EpisodeCommandKind.Compete)
                command.performance = PerformanceModel.Draw(run.cell.performance, run.cell.policy, run.seed, s.week, s.phase);
        }

        /// <summary>
        /// The engine tests' walker, answering a finale question with an offered response, a production-removed
        /// player sitting the jury out, and a player who holds the veto on the block saving themselves
        /// (<see cref="SavesThePlayer"/>). The walker itself is left as it is: the recorded-season digests walk it.
        /// </summary>
        internal static EpisodeCommand Walker(EpisodeState s)
        {
            var command = SavesThePlayer(EpisodeEngineTests.NextCommand(s), s);
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

        /// <summary>
        /// A player who holds the veto while on the block uses it on themselves. The engine tests' walker uses it
        /// on the first nominee whoever that is, so a player who won the veto and sat in the second chair left
        /// themselves up - and every lab player who fell back on the walker at the meeting (the passive player,
        /// both oracles) was evicted in weeks they held the veto. Applied to the walker's step and to an oracle's
        /// own command (the story sweep's skilled player answers the meeting with the walker's). The final four's
        /// lock binds only a holder who is off the block, so it never applies here.
        /// </summary>
        internal static EpisodeCommand SavesThePlayer(EpisodeCommand command, EpisodeState s)
        {
            if (command != null && command.kind == EpisodeCommandKind.ResolveVeto && command.useVeto
                && s.vetoHolderId == s.playerId && s.nominees.Contains(s.playerId))
                command.targetId = s.playerId;
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
        internal static SeasonRun[] Run(IReadOnlyList<Cell> cells, int seasonsPerCell, int parallelism = 0) => RunPart(cells, 0, seasonsPerCell, parallelism);

        /// <summary>Every cell's seasons from..from+count-1, in parallel, into an array ordered by cell then index: one part of a tier.</summary>
        internal static SeasonRun[] RunPart(IReadOnlyList<Cell> cells, int from, int count, int parallelism = 0)
        {
            var runs = new SeasonRun[cells.Count * count];
            var options = new ParallelOptions { MaxDegreeOfParallelism = parallelism > 0 ? parallelism : Environment.ProcessorCount };
            Parallel.For(0, runs.Length, options, i => runs[i] = Play(cells[i / count], from + i % count));
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
                // The autopsy's story counts: "asks" is every story house event in the final state, and a pariah week an eviction night that closed with one.
                story = a == null ? null : new { a.story.storylines, a.story.completed, a.story.asks, a.story.weeksWithACard, a.story.npcRemovals, a.story.showmances, a.story.pileOns, a.story.pariahWeeks },
                // The story pacing report's counts (Pace): asks deduplicated per storyline and anchor, the first night, summons and plays apart.
                pace = r.pace == null ? null : new
                {
                    asks = r.pace.asks, budgeted = r.pace.budgetedAsks, summons = r.pace.summons, playOffers = r.pace.playOffers, weeksWithACard = r.pace.weeksWithACard,
                    stories = r.pace.storiesFinished, moments = r.pace.arcsFinished - r.pace.storiesFinished, pariah = r.pace.pariah,
                },
                // The war rooms (D3): the autopsy's plans, and every member a counter of the player's could reach.
                warRooms = a == null ? null : new
                {
                    a.warRooms.plans, stances = new SortedDictionary<string, int>(a.warRooms.stances, StringComparer.Ordinal), a.warRooms.counters,
                    carried = a.warRooms.countersCarried, a.warRooms.cameRound, a.warRooms.playerCalls, a.warRooms.targetEvicted,
                },
                counterMembers = r.counterMembers.Select(m => new
                {
                    w = m.week, m.stance, odds = Math.Round(m.odds, 4), view = Math.Round(m.view, 2), margin = Math.Round(m.margin, 4), traits = m.traits,
                    m.lapsed, came = m.cameRound, m.carried,
                }),
                commands = r.commands, own = r.own, fallbacks = r.fallbacks, freeActions = r.freeActions, refusals = r.refusals, refusalsBy = r.refusalsByKind,
                decisions = r.decisionsByWeek, ceremony = r.ceremonyByWeek, finaleSeconds = r.finaleSeconds,
                npcTicksUsed = a?.npcTicks ?? 0,
                // The NPC world (B5b): its operations, and the final house's pairs at or near the bound.
                npcWorld = new { ops = r.npcOps, ticks = r.npcTicks, starts = r.npcStarts, scans = r.npcScans, tries = r.npcTries, held = r.npcHeld, rejected = r.npcRejected },
                pairs = new { all = r.pairs, near = r.pairsNearBound, at = r.pairsAtBound },
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
