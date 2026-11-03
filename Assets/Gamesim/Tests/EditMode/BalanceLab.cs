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
            /// <summary>
            /// The card the player plays (ALL-STAR-HISTORIES-PLAN §9: the player on an alumni card), or null for the
            /// newcomer the lab has always seated.
            /// </summary>
            public string playerTemplate;
            /// <summary>
            /// The All-Star histories' paired control (§9): the season built without ApplyFresh's histories step, held at
            /// alumni rules 0 as a harness may hold one. Shipping never does.
            /// </summary>
            public bool alumniOff;
            /// <summary>
            /// The histories' second control (§9, "both together"): agency's first impressions left out as well. The agency
            /// boundary is set to week one before ApplyFresh, so EnableAgency switches agency on from week one without
            /// seeding them; every other rule is as shipped. With <see cref="alumniOff"/>, the season starts with neither.
            /// A harness's control only: shipping never does.
            /// </summary>
            public bool impressionsOff;
            /// <summary>
            /// The alumni rules value an enrolled season is held at (ALL-STAR-HISTORIES-PLAN 14.G, 14.H), or 0 for the shipped
            /// value: written after ApplyFresh, as a harness may hold one, valid while every value from it to
            /// <see cref="AlumniRules.Latest"/> writes the same opening. The seed is the shipped cell's, so arms pair.
            /// Shipping never does.
            /// </summary>
            public int alumniHeld;

            /// <summary>The house; a player card, the histories' controls and a held alumni value are named only when set, so every earlier cell keeps its name.</summary>
            public string House => roster + "/" + size.ToString(CultureInfo.InvariantCulture) + "/npc" + npcTicks.ToString(CultureInfo.InvariantCulture)
                + (playerTemplate == null ? "" : "/as-" + playerTemplate) + (alumniOff ? "/alumni0" : "") + (impressionsOff ? "/impressions0" : "")
                + (alumniHeld == 0 ? "" : "/alumni" + alumniHeld.ToString(CultureInfo.InvariantCulture));
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
            /// <summary>
            /// ALL-STAR-HISTORIES-PLAN 14.H and 14.L.H: the season's pacts and the cast's known pairs, as the pact watch saw
            /// them command by command (<see cref="PactWatch"/>). An analyst's record, like the diagnostics': never in the rows.
            /// </summary>
            public PactRecord pactWatch;
            /// <summary>The season's wall-clock milliseconds on its thread, for the report's pacing (timed: never in the rows).</summary>
            public double seasonMilliseconds;
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
                // NominationWeight's own gate since vote family V5b: canonical Safety in modes 1 and 2.
                protection = -(UnifiedCommitments.SafetyAuthorityOn(s) ? Math.Min(UnifiedCommitments.StrongestProtection(s, hohId, id).Strength, StoryConsumers.SafetyPreference(s, hohId, id)) : 0),
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

        /// <summary>
        /// One season of a cell. <paramref name="watch"/> false leaves the pact watch (<see cref="PactWatch"/>) off: it only
        /// reads, so a season plays the same with it and without it, which a test plays both ways to show.
        /// </summary>
        internal static SeasonRun Play(Cell cell, int index, bool watch = true)
        {
            var run = new SeasonRun { cell = cell, index = index, seed = Seed(cell, index) };
            var clock = Stopwatch.StartNew();
            try { PlayInto(run, watch); }
            catch (Exception error) { run.error = "exception: " + error.GetType().Name + ": " + error.Message; }
            run.seasonMilliseconds = clock.Elapsed.TotalMilliseconds;
            return run;
        }

        /// <summary>
        /// A cell's fresh season under the shipped rules. The histories' controls leave only their own parts out: the
        /// histories' step (ShippedRules.ApplyFresh's internal form) and, for "both together", agency's first impressions.
        /// A held alumni value is written over the shipped one when the season enrolled (14.H); a season that did not is left
        /// at 0.
        /// </summary>
        internal static EpisodeState Fresh(Cell cell, uint seed)
        {
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = cell.size, Roster = cell.roster, PlayerTemplateId = cell.playerTemplate }, seed);
            if (cell.impressionsOff) fresh.agencyRulesStartWeek = 1;
            ShippedRules.ApplyFresh(fresh, alumniHistories: !cell.alumniOff);
            if (cell.alumniHeld != 0 && AlumniRules.On(fresh)) fresh.alumniRulesVersion = cell.alumniHeld;
            return fresh;
        }

        private static void PlayInto(SeasonRun run, bool watch)
        {
            var cell = run.cell;
            var fresh = Fresh(cell, run.seed);
            var pacts = watch ? new PactWatch(fresh) : null;
            PactRecord watchFailed = null;
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
            var watched = s;
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
                if (pacts != null)
                {
                    // The NPC world's operations since the last command (B5b) are watched as a step of their own. A watch that
                    // throws ends for the season, its record kept with the error; the season plays on (PactWatch.Guard).
                    EpisodeState seen = watched, to = s, next = after;
                    var command = used;
                    if (to.revision != seen.revision) PactWatch.Guard(ref pacts, ref watchFailed, w => w.Watch(null, seen, to));
                    PactWatch.Guard(ref pacts, ref watchFailed, w => w.Watch(command, to, next));
                    watched = after;
                }
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
            PactRecord closed = null;
            var final = s;
            PactWatch.Guard(ref pacts, ref watchFailed, w => closed = w.Close(final));
            run.pactWatch = closed ?? watchFailed;
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

        // ---------------------------------------------------------------- the pact watch (ALL-STAR-HISTORIES-PLAN 14.H, 14.L.H)

        /// <summary>
        /// What 14.H's report reads of one season (ALL-STAR-HISTORIES-PLAN 14.H, 14.L.H), kept beside the pariah record and,
        /// like it, out of the rows the goldens hash; a part carries it (<see cref="BalanceLabParts.RoundTrip"/>). Counts,
        /// weeks and lists in a fixed order, so two runs write the same record.
        /// </summary>
        internal sealed class PactRecord
        {
            /// <summary>The lowest and the highest alumni rules value any state the watch saw held: a held arm plays at its value throughout.</summary>
            public int rulesLow = -1, rulesHigh = -1;
            /// <summary>The cast's known pairs: edition 1's seated shared alliances, "fact:first|second", read at every rules value (arm 0 included) so arms compare.</summary>
            public List<string> pairs = new List<string>();
            /// <summary>Their houseguests, by id, in order.</summary>
            public List<string> members = new List<string>();
            /// <summary>The Hitmen's two (<see cref="PactWatch.HitmenFact"/>), or null when the cast does not seat them.</summary>
            public string hitmenFirst, hitmenSecond;

            // 14.L.H's pact watch
            /// <summary>Houseguests' pacts born (an <c>alliance-npc-</c> id with no player), and members joined into one (a rise in its members, the player aside).</summary>
            public int births, joins;
            /// <summary>The week of the season's first join, or 0 for none.</summary>
            public int firstJoinWeek;
            /// <summary>A pact of houseguests born by week 3 with no player and no live-pair member in it.</summary>
            public bool outsiderPactByWeek3;
            /// <summary>The weeks with a known pair live (the bound above two), in order.</summary>
            public List<int> blocLiveWeeks = new List<int>();
            /// <summary>The most of one outsider pact (houseguests' own, no player, no live-pair member) in the house at once; 0 for none.</summary>
            public int largestOutsiderPact;
            /// <summary>Pact-weeks a houseguests' pact sat below the bound only because somebody outside it knew of it, by how it first came out.</summary>
            public SortedDictionary<string, int> heldBack = new SortedDictionary<string, int>(StringComparer.Ordinal);
            /// <summary>
            /// Joiners shed from a grown pact while it was private to its members, joiners out of one otherwise (a story's leave),
            /// and founders out of a grown pact that stood on: a member counts as a joiner only when the watch saw them join.
            /// </summary>
            public int shed, joinersOut, foundersOut;
            /// <summary>How every grown pact ended, or that it stood at the finale.</summary>
            public List<GrownEnd> grownEnds = new List<GrownEnd>();
            /// <summary>Each week, as the nominations are made, the share of the NPCs in the house who are in an NPC-only pact.</summary>
            public List<double> npcPactShares = new List<double>();
            /// <summary>Two grown pacts with three or more in the house at once; the player taken into a grown pact by an invitation.</summary>
            public bool twoGrownAtOnce, playerInvitedIntoGrown;
            /// <summary>The week the player first knew of a grown pact, or 0.</summary>
            public int playerLearnedGrownWeek;
            /// <summary>Evictions an outsider bloc of three or more carried: every one of them voted the evictee out, and without their votes the evictee would not have led.</summary>
            public int blocCarried;
            /// <summary>Every pile-on (the story's the-house-turns) and its target, as the pariah decomposition reads a pariah run.</summary>
            public List<PileOn> pileOns = new List<PileOn>();
            /// <summary>Pairs of NPCs who both rate each other at −10 or less at week 3 (NpcAgencyHarnessTests' cold pairs), or −1 unseen.</summary>
            public int coldPairsWeek3 = -1;
            /// <summary>Hunts with an NPC listener; bloc hunts (aimed at a live-pair member); the most hunts on one target in one week.</summary>
            public int hunts, blocHunts, mostHuntsOnOneTarget;
            /// <summary>The longest run of weeks one listener heard a bloc hunt.</summary>
            public int longestListenerRun;
            /// <summary>Hunts and target agreements aimed at a pact-mate of both, in any pact (at 4 the story trios' old flaw).</summary>
            public int huntsAtPactMate, agreementsAtPactMate;
            /// <summary>Power Shift stories cast on a grown pact.</summary>
            public int powerShiftOnGrown;

            // 14.H's rows about the known pairs
            /// <summary>Member-weeks: a live-pair member in the house, not the Head of Household, as an NPC Head of Household nominates; and those weeks they were put up.</summary>
            public int memberWeeks, memberPutUps;
            /// <summary>An active pact held both Hitmen; one of a known pair put the other up; one houseguest was put up three or more weeks running.</summary>
            public bool hitmenReformed, publicSplit, putUpThreeRunning;
            /// <summary>The Hitmen's opening final two at the season's end: its status, or "none" without one.</summary>
            public string hitmenFinalTwo;
            /// <summary>Rumours with a subject, and the longest run of weeks the house talked about the same subject.</summary>
            public int rumours, longestRumourRun;
            /// <summary>The mean share of a positive past-game receipt the holder still counts at the season's end; null without one.</summary>
            public double? goodwill;

            /// <summary>14.L.H's must-be-zero rows, and the watch's own consistency checks (<see cref="PactWatch.MustBeZero"/>), every one present.</summary>
            public SortedDictionary<string, int> mustBeZero = new SortedDictionary<string, int>(StringComparer.Ordinal);

            /// <summary>
            /// The final house's relationship scores, every edge in order, as a 16-hex SHA-256 (<see cref="PactWatch.ScoresHash"/>): two
            /// seasons whose rows are alike can still end with different scores, which the report counts beside its zeros.
            /// </summary>
            public string scoresHash;
            /// <summary>The watch's own failure when it threw (<see cref="PactWatch.Guard"/>): the season played on without it, and this record stops where it threw.</summary>
            public string watchError;
        }

        /// <summary>How a grown pact ended (the alliance row's why, "standing" at the finale), the weeks it lasted, its most in the house, and whether anyone outside it knew of it then.</summary>
        internal sealed class GrownEnd
        {
            public string why;
            public int weeks, largest;
            public bool known;
        }

        /// <summary>One pile-on: its week and target, how the target is classed (<see cref="PactWatch.PileOnKinds"/>), and the pariah decomposition's columns.</summary>
        internal sealed class PileOn
        {
            public int week, houseCount, holders, targetHohWins, npcHohWinsMax;
            public string target, kind;
            public List<string> causes = new List<string>();
        }

        /// <summary>
        /// 14.L.H's per-command pact watch, beside <see cref="PariahWatch"/>: it reads each accepted command's state before and
        /// after (and the NPC world's operations between two commands as a step of their own) and writes only its
        /// <see cref="PactRecord"/>. The cast's known pairs are read from edition 1 whatever the season's rules (the arm without
        /// the histories included), and are live while both are in the house and neither has put the other up, as
        /// <see cref="KnownBlocs.LivePairs"/> reads them: the watch checks that it agrees wherever the rules read them.
        ///
        /// <para><b>Between commands.</b> A command can carry a whole stretch of the week (a window's beats, the week's turn),
        /// so the watch's checks are made where they are sound from the states either side: a must-be-zero row counts only
        /// what must have held at the moment it happened (a bound read before a join can only have fallen since; a pact and an
        /// agreement holding in both states held in between). A violation that begins and ends inside one command is missed,
        /// never invented.</para>
        /// </summary>
        internal sealed class PactWatch
        {
            internal const string HitmenFact = "bb16-hitmen";

            /// <summary>How a pile-on's target is classed (14.L.H), in this order: a known-pair member, a member of a grown pact, a joiner a grown pact shed, anyone else.</summary>
            internal static readonly string[] PileOnKinds = { "known-pair member", "grown-pact member", "shed joiner", "other" };

            /// <summary>How a pact first came out, for the pact-weeks it was held back by being known (14.L.H).</summary>
            internal static readonly string[] Causes = { "whisper", "listen-in", "story: power-shift", "story: other", "a member left", "other" };

            /// <summary>14.L.H's must-be-zero rows, then the watch's own: every one is a bug when it is not zero.</summary>
            internal static readonly string[] MustBeZero =
            {
                "a join with no live pair", "a join above its bound", "more than 4 of a pact in the house", "a live-pair member in a grown pact",
                "a join into a pact not the houseguests' own", "a join into a pact known outside it", "a join in its fact's week",
                "a join into a pact with a member out of the house", "a join while a target agreement stands among the pact-to-be",
                "a hunt at a non-player pact-mate of both in a houseguests' pact", "a target agreement at a non-player pact-mate of both in a houseguests' pact",
                "a cap breach", "a houseguest in two pacts of their own",
                "a join below alumni rules 5", "more than one join a pact a week",
                "the watch's live pairs differ from the rules'", "the watch's bound differs from the rules'",
            };

            internal readonly PactRecord record = new PactRecord();
            private readonly List<(string fact, string a, string b)> pairs = new List<(string, string, string)>();
            private readonly Dictionary<string, string> idsByName = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly HashSet<string> grown = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> shedJoiners = new HashSet<string>(StringComparer.Ordinal);
            /// <summary>Every member the watch saw join a pact, as "pact|member": a joiner, whatever place in the roster a founder's leaving has moved them to.</summary>
            private readonly HashSet<string> joiners = new HashSet<string>(StringComparer.Ordinal);
            private readonly Dictionary<string, string> exposure = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> joinWeek = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> largest = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly HashSet<string> heldWeeks = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> countedDeals = new HashSet<string>(StringComparer.Ordinal);
            private readonly List<(int week, List<string> candidates)> nominations = new List<(int, List<string>)>();
            private readonly SortedDictionary<string, SortedSet<int>> listenerWeeks = new SortedDictionary<string, SortedSet<int>>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> huntsByTargetWeek = new Dictionary<string, int>(StringComparer.Ordinal);
            private int lastPacts;
            private bool week3Taken;

            internal PactWatch(EpisodeState fresh)
            {
                foreach (var pair in AlumniHistories.SeatedPairs(fresh, AlumniEditions.Histories1))
                    if (pair.Fact.Standing > 0) pairs.Add((pair.Fact.Id, pair.FirstId, pair.SecondId));
                record.pairs = pairs.Select(Key).ToList();
                record.members = pairs.SelectMany(p => new[] { p.a, p.b }).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToList();
                var hitmen = pairs.FirstOrDefault(p => p.fact == HitmenFact);
                record.hitmenFirst = hitmen.a;
                record.hitmenSecond = hitmen.b;
                foreach (string row in MustBeZero) record.mustBeZero[row] = 0;
                foreach (var c in fresh.contestants.Where(c => c != null && !c.isPlayer && c.name != null))
                    idsByName[c.name] = idsByName.ContainsKey(c.name) ? null : c.id;
                Rules(fresh);
            }

            private static string Key((string fact, string a, string b) p) => p.fact + ":" + p.a + "|" + p.b;

            /// <summary>The houseguests' bound as the rules read it from these live pairs (<see cref="KnownBlocs.CounterPactBound"/> at 5): 0 with none.</summary>
            internal static int Bound(int live) => live == 0 ? 0 : Math.Min(EpisodeEngine.LargestPact, live + 1 + KnownBlocs.CounterPactMargin);

            /// <summary>The cast's known pairs still live: both in the house, and neither has put the other up (this week's block included).</summary>
            internal List<(string fact, string a, string b)> Live(EpisodeState s) =>
                pairs.Where(p => s.Find(p.a)?.status == ContestantStatus.Active && s.Find(p.b)?.status == ContestantStatus.Active && !Split(s, p.a, p.b)).ToList();

            private static bool Split(EpisodeState s, string a, string b)
            {
                if (s.ledger?.power != null)
                    foreach (var row in s.ledger.power)
                        if (row?.nominees != null && !FinalEviction(row) && row.week <= s.week
                            && ((row.hohId == a && FinalistRead.PutUp(row, b)) || (row.hohId == b && FinalistRead.PutUp(row, a))))
                            return true;
                return s.nominees != null && ((s.hohId == a && s.nominees.Contains(b)) || (s.hohId == b && s.nominees.Contains(a)));
            }

            /// <summary>The last Head of Household's choice, a power row of its own: it nominates nobody (AlumniLoyalty's rule).</summary>
            private static bool FinalEviction(PowerRow row) => (row.tally == null || row.tally.Count == 0) && row.evicteeId != null && row.vetoHolderId == null;

            private void Bug(string row) => record.mustBeZero[row] = record.mustBeZero[row] + 1;

            private void Rules(EpisodeState s)
            {
                int v = s.alumniRulesVersion;
                record.rulesLow = record.rulesLow < 0 ? v : Math.Min(record.rulesLow, v);
                record.rulesHigh = Math.Max(record.rulesHigh, v);
            }

            private static int Here(EpisodeState s, AllianceState pact) => EpisodeEngine.InTheHouse(s, pact);

            /// <summary>NpcAlliances' own reading: the pact's fact is private and known only to its members (14.L.B rule 7).</summary>
            private static bool PrivateToMembers(EpisodeState s, AllianceState pact)
            {
                if (pact == null || !EpisodeEngine.StoryAt(s, StoryRules.Bonds)) return false;
                var fact = Knowledge.Of(s, FactKinds.Alliance, pact.id);
                return fact != null && fact.visibility == FactVisibility.Private && fact.knowers.All(pact.members.Contains);
            }

            /// <summary>A houseguests' own pact with no player and nobody in a live pair: the outsiders'.</summary>
            private static bool Outsider(EpisodeState s, AllianceState pact, HashSet<string> inLive) =>
                NpcAlliances.HouseguestsOwn(pact) && !pact.members.Contains(s.playerId) && !pact.members.Any(id => inLive.Contains(id) && s.Find(id)?.status == ContestantStatus.Active);

            /// <summary>One accepted command (or, with no command, the NPC world's operations): the states either side of it.</summary>
            internal void Watch(EpisodeCommand command, EpisodeState before, EpisodeState after)
            {
                Rules(before);
                Rules(after);
                var liveBefore = Live(before);
                var liveAfter = Live(after);
                if (AlumniRules.At(after, AlumniRules.Reputation)
                    && !KnownBlocs.LivePairs(after).Select(p => p.Fact.Id + ":" + p.FirstId + "|" + p.SecondId).SequenceEqual(liveAfter.Select(Key)))
                    Bug("the watch's live pairs differ from the rules'");
                if (AlumniRules.At(after, AlumniRules.GrowingPacts) && KnownBlocs.CounterPactBound(after) != Bound(liveAfter.Count))
                    Bug("the watch's bound differs from the rules'");
                var inLive = new HashSet<string>(liveAfter.SelectMany(p => new[] { p.a, p.b }), StringComparer.Ordinal);
                var wasLive = new HashSet<string>(liveBefore.SelectMany(p => new[] { p.a, p.b }), StringComparer.Ordinal);
                var (storyMoved, powerShiftMoved) = Stories(before, after);
                Pacts(command, before, after, Bound(liveBefore.Count), wasLive, inLive, storyMoved, powerShiftMoved);
                Hunts(before, after, wasLive, inLive);
                Agreements(after);
                Eviction(before, after, inLive);
                if (before.phase == EpisodePhase.Nomination && before.nominees.Count == 0 && after.nominees.Count == 2 && after.week == before.week)
                    Nominations(before, wasLive);
                if (command == null || command.kind == EpisodeCommandKind.Advance) Sample(after, liveAfter.Count, inLive);
            }

            private void Pacts(EpisodeCommand command, EpisodeState before, EpisodeState after, int boundBefore, HashSet<string> wasLive, HashSet<string> inLive,
                bool storyMoved, bool powerShiftMoved)
            {
                foreach (var pact in after.alliances)
                {
                    if (pact?.members == null) continue;
                    var was = before.alliances.FirstOrDefault(a => a?.id == pact.id);
                    bool own = NpcAlliances.HouseguestsOwn(pact), npcOnly = !pact.members.Contains(after.playerId);
                    if (was == null)
                    {
                        if (!own || !npcOnly) continue;
                        record.births++;
                        if (after.week <= 3 && Outsider(after, pact, inLive)) record.outsiderPactByWeek3 = true;
                        if (pact.members.Count > 2) { Bug("a join in its fact's week"); grown.Add(pact.id); }
                        continue;
                    }
                    var added = pact.members.Where(id => !was.members.Contains(id)).ToList();
                    var removed = was.members.Where(id => !pact.members.Contains(id)).ToList();
                    if (added.Contains(after.playerId) && grown.Contains(pact.id)) record.playerInvitedIntoGrown = true;
                    else if (added.Count > 0 && npcOnly) Joined(before, after, was, pact, added, boundBefore);
                    if (removed.Count > 0 && was.active && pact.active && grown.Contains(pact.id))
                        foreach (string id in removed)
                        {
                            if (!joiners.Contains(pact.id + "|" + id)) record.foundersOut++;
                            else if (was.members.IndexOf(id) >= 2 && PrivateToMembers(before, was) && Sour(before, after, was, id)) { record.shed++; shedJoiners.Add(id); }
                            else record.joinersOut++;
                        }
                    if (own && !exposure.ContainsKey(pact.id) && PrivateToMembers(before, was) && !PrivateToMembers(after, pact))
                        exposure[pact.id] = Cause(command, before, after, was, pact, storyMoved, powerShiftMoved);
                    if (was.active && !pact.active && grown.Contains(pact.id))
                    {
                        var row = after.ledger?.alliances?.FirstOrDefault(r => r.id == pact.id);
                        record.grownEnds.Add(new GrownEnd
                        {
                            why = row?.why ?? "?", weeks = row == null ? 0 : row.endedWeek - row.startedWeek, known = !PrivateToMembers(before, was),
                            largest = largest.TryGetValue(pact.id, out int most) ? most : 0,
                        });
                    }
                }
                int grownHere = 0;
                foreach (var pact in after.alliances.Where(a => a?.members != null && a.active && NpcAlliances.HouseguestsOwn(a) && !a.members.Contains(after.playerId)))
                {
                    int here = Here(after, pact);
                    if (here > EpisodeEngine.LargestPact) Bug("more than 4 of a pact in the house");
                    if (Outsider(after, pact, inLive)) record.largestOutsiderPact = Math.Max(record.largestOutsiderPact, here);
                    if (!grown.Contains(pact.id)) continue;
                    largest[pact.id] = Math.Max(largest.TryGetValue(pact.id, out int most) ? most : 0, here);
                    if (here >= 3) grownHere++;
                    if (pact.members.Any(id => inLive.Contains(id) && after.Find(id)?.status == ContestantStatus.Active)) Bug("a live-pair member in a grown pact");
                }
                if (grownHere >= 2) record.twoGrownAtOnce = true;
                if (record.playerLearnedGrownWeek == 0
                    && after.alliances.Any(a => a?.members != null && grown.Contains(a.id) && !a.members.Contains(after.playerId) && Knowledge.AllianceVisibleTo(after, a, after.playerId)))
                    record.playerLearnedGrownWeek = after.week;
                if (record.hitmenFirst != null && after.alliances.Any(a => a?.members != null && a.active && a.members.Contains(record.hitmenFirst) && a.members.Contains(record.hitmenSecond)))
                    record.hitmenReformed = true;
            }

            /// <summary>
            /// A rise in an NPC-only pact's members: a join (14.L.D), checked against every rule a join keeps, each read where it
            /// is sound from the states either side of the command.
            /// </summary>
            private void Joined(EpisodeState before, EpisodeState after, AllianceState was, AllianceState pact, List<string> added, int boundBefore)
            {
                record.joins += added.Count;
                if (record.firstJoinWeek == 0) record.firstJoinWeek = after.week;
                grown.Add(pact.id);
                foreach (string id in added) joiners.Add(pact.id + "|" + id);
                if (!AlumniRules.At(before, AlumniRules.GrowingPacts) || !AlumniRules.At(after, AlumniRules.GrowingPacts)) Bug("a join below alumni rules 5");
                // Live pairs only end, so the bound read before the command is the most it can have been at the join.
                if (boundBefore <= 2) Bug("a join with no live pair");
                if (Here(after, pact) > boundBefore) Bug("a join above its bound");
                if (added.Count > 1 || (joinWeek.TryGetValue(pact.id, out int week) && week == after.week)) Bug("more than one join a pact a week");
                joinWeek[pact.id] = after.week;
                if (!NpcAlliances.HouseguestsOwn(pact)) Bug("a join into a pact not the houseguests' own");
                // Knowers never shrink: known outside before, known outside at the join.
                if (!PrivateToMembers(before, was)) Bug("a join into a pact known outside it");
                var fact = Knowledge.Of(before, FactKinds.Alliance, pact.id);
                if (fact != null && fact.week >= after.week) Bug("a join in its fact's week");
                if (was.members.Any(id => before.Find(id)?.status != ContestantStatus.Active)) Bug("a join into a pact with a member out of the house");
                var group = new HashSet<string>(was.members.Where(id => before.Find(id)?.status == ContestantStatus.Active).Concat(added), StringComparer.Ordinal);
                if (before.deals.Any(d => TargetAmong(d, group) && after.deals.Any(e => e.id == d.id && TargetAmong(e, group))))
                    Bug("a join while a target agreement stands among the pact-to-be");
                // A live-pair member in it is counted with every grown pact's, below (live pairs only end, so after is sound).
            }

            private static bool TargetAmong(DealState d, HashSet<string> group) =>
                d != null && d.status == DealStatus.Active && d.type == DealKind.TargetAgreement && d.proposerId != null && d.recipientId != null && d.targetId != null
                && group.Contains(d.proposerId) && group.Contains(d.recipientId) && group.Contains(d.targetId);

            /// <summary>Whether a member taken out of a pact had soured with anybody left in it (ShedSourJoiners' reading), either side of the command.</summary>
            private static bool Sour(EpisodeState before, EpisodeState after, AllianceState was, string id)
            {
                foreach (var s in new[] { before, after })
                    foreach (string other in was.members.Where(o => o != id && s.Find(o)?.status == ContestantStatus.Active))
                        if (s.Score(id, other) < NpcAlliances.SourLine || s.Score(other, id) < NpcAlliances.SourLine) return true;
                return false;
            }

            /// <summary>How a pact first came out to somebody outside it (14.L.H's causes, <see cref="Causes"/>).</summary>
            private static string Cause(EpisodeCommand command, EpisodeState before, EpisodeState after, AllianceState was, AllianceState pact, bool storyMoved, bool powerShiftMoved)
            {
                var then = Knowledge.Of(before, FactKinds.Alliance, pact.id);
                var now = Knowledge.Of(after, FactKinds.Alliance, pact.id);
                if (now == null) return "other";
                var outside = now.knowers.Where(k => !pact.members.Contains(k)).ToList();
                if (outside.Any(was.members.Contains)) return "a member left";
                if (outside.Contains(after.playerId) && now.visibility == FactVisibility.Private && command != null
                    && (command.kind == EpisodeCommandKind.Eavesdrop || command.kind == EpisodeCommandKind.WitnessNpcAct || command.kind == EpisodeCommandKind.WitnessProximity))
                    return "listen-in";
                // The weekly leak widens the fact to a whisper and adds no knower.
                bool widenedAlone = then != null && now.visibility != FactVisibility.Private && now.knowers.Count == then.knowers.Count;
                if (widenedAlone && !storyMoved) return "whisper";
                if (powerShiftMoved) return "story: power-shift";
                if (storyMoved) return "story: other";
                return now.visibility != FactVisibility.Private ? "whisper" : "other";
            }

            /// <summary>The stories that started this command: a pile-on's target classed, a Power Shift cast on a grown pact. Returns whether any story began or took a step, and whether a Power Shift did.</summary>
            private (bool moved, bool powerShift) Stories(EpisodeState before, EpisodeState after)
            {
                bool moved = false, powerShift = false;
                foreach (var story in after.storylines)
                {
                    var was = before.storylines.FirstOrDefault(x => x.id == story.id);
                    if (was != null && was.path.Count == story.path.Count) continue;
                    moved = true;
                    if (story.templateId == "power-shift") powerShift = true;
                    if (was != null) continue;
                    string Role(string role) => story.cast.FirstOrDefault(c => c.role == role)?.contestantId;
                    if (story.templateId == "the-house-turns" && Role("TARGET") != null)
                    {
                        string target = Role("TARGET");
                        var holders = Grudges.HoldersAgainst(after, target, 40).Where(h => after.Find(h)?.status == ContestantStatus.Active).ToList();
                        var victim = after.Find(target);
                        record.pileOns.Add(new PileOn
                        {
                            week = after.week, target = target, kind = PileOnKind(after, target), houseCount = after.Active.Count(), holders = holders.Count,
                            targetHohWins = victim?.hohWins ?? 0, npcHohWinsMax = after.contestants.Where(c => !c.isPlayer).Select(c => c.hohWins).DefaultIfEmpty(0).Max(),
                            causes = after.story.grudges.Where(g => g.targetId == target && holders.Contains(g.holderId)).Select(g => g.cause ?? "").ToList(),
                        });
                    }
                    if (story.templateId == "power-shift" && Role("LEADER") != null && Role("MEMBER") != null
                        && after.alliances.Any(a => a?.members != null && a.active && grown.Contains(a.id) && a.members.Contains(Role("LEADER")) && a.members.Contains(Role("MEMBER"))))
                        record.powerShiftOnGrown++;
                }
                return (moved, powerShift);
            }

            private string PileOnKind(EpisodeState s, string target) =>
                record.members.Contains(target) ? PileOnKinds[0]
                : s.alliances.Any(a => a?.members != null && a.active && grown.Contains(a.id) && a.members.Contains(target)) ? PileOnKinds[1]
                : shedJoiners.Contains(target) ? PileOnKinds[2] : PileOnKinds[3];

            /// <summary>
            /// The command's hunts, from the ledger entries it wrote: a hunt with an NPC listener writes "What {listener} heard
            /// from {hunter} about {target}" on the listener's edge to the target (NpcSocialActions' pursuit), so the hunts the
            /// week's last window fires just before the week turns, and its acts are cleared, are counted too.
            /// </summary>
            private void Hunts(EpisodeState before, EpisodeState after, HashSet<string> wasLive, HashSet<string> inLive)
            {
                long from = before.nextSequence;
                foreach (var edge in after.relationships)
                {
                    if (edge?.events == null || edge.events.Count == 0) continue;
                    string listener = edge.fromId, listenerName = after.Find(listener)?.name;
                    if (listenerName == null) continue;
                    string prefix = "What " + listenerName + " heard from ";
                    for (int i = edge.events.Count - 1; i >= 0 && edge.events[i].sequence >= from; i--)
                    {
                        var e = edge.events[i];
                        if (e.type != "rumor" || e.description == null || !e.description.StartsWith(prefix, StringComparison.Ordinal)) continue;
                        int about = e.description.LastIndexOf(" about ", StringComparison.Ordinal);
                        if (about < prefix.Length) continue;
                        string hunterName = e.description.Substring(prefix.Length, about - prefix.Length);
                        string hunter = idsByName.TryGetValue(hunterName, out string id) ? id : null;
                        string target = edge.toId;
                        if (after.Find(listener)?.isPlayer == true) continue;
                        record.hunts++;
                        string week = e.week.ToString(CultureInfo.InvariantCulture) + "|" + target;
                        huntsByTargetWeek[week] = (huntsByTargetWeek.TryGetValue(week, out int n) ? n : 0) + 1;
                        record.mostHuntsOnOneTarget = Math.Max(record.mostHuntsOnOneTarget, huntsByTargetWeek[week]);
                        if (wasLive.Contains(target) && inLive.Contains(target))
                        {
                            record.blocHunts++;
                            if (!listenerWeeks.TryGetValue(listener, out var weeks)) listenerWeeks[listener] = weeks = new SortedSet<int>();
                            weeks.Add(e.week);
                        }
                        if (hunter == null) continue;
                        // A pact holding all three on both sides of the command held them at the hunt.
                        var mates = before.alliances.Where(a => PactOfAll(a, hunter, listener, target)
                            && after.alliances.Any(b => b.id == a.id && PactOfAll(b, hunter, listener, target))).ToList();
                        if (mates.Count == 0) continue;
                        record.huntsAtPactMate++;
                        if (AlumniRules.At(after, AlumniRules.GrowingPacts) && after.Find(target)?.isPlayer == false && mates.Any(NpcAlliances.HouseguestsOwn))
                            Bug("a hunt at a non-player pact-mate of both in a houseguests' pact");
                    }
                }
            }

            private static bool PactOfAll(AllianceState a, string x, string y, string z) =>
                a?.members != null && a.active && a.members.Contains(x) && a.members.Contains(y) && a.members.Contains(z);

            /// <summary>Every active target agreement once, as it stands: aimed at a pact-mate of both its parties.</summary>
            private void Agreements(EpisodeState after)
            {
                foreach (var d in after.deals)
                {
                    if (d == null || d.type != DealKind.TargetAgreement || d.status != DealStatus.Active || d.proposerId == null || d.recipientId == null || d.targetId == null) continue;
                    var mates = after.alliances.Where(a => PactOfAll(a, d.proposerId, d.recipientId, d.targetId)).ToList();
                    if (mates.Count == 0 || !countedDeals.Add(d.id ?? "")) continue;
                    record.agreementsAtPactMate++;
                    if (AlumniRules.At(after, AlumniRules.GrowingPacts) && mates.Any(NpcAlliances.HouseguestsOwn)
                        && new[] { d.proposerId, d.recipientId, d.targetId }.All(id => after.Find(id)?.isPlayer == false))
                        Bug("a target agreement at a non-player pact-mate of both in a houseguests' pact");
                }
            }

            /// <summary>An eviction the command revealed: carried by an outsider bloc of three or more, all voting the evictee out and decisive.</summary>
            private void Eviction(EpisodeState before, EpisodeState after, HashSet<string> inLive)
            {
                var row = after.ledger?.power?.LastOrDefault();
                if (row?.evicteeId == null || row.tally == null || row.tally.Count == 0) return;
                if (before.ledger?.power?.Any(r => r.week == row.week && r.evicteeId != null) == true) return;
                var votes = after.votes.Where(v => v?.voterId != null && v.targetId != null).ToList();
                if (votes.Count == 0) return;
                int forEvictee = votes.Count(v => v.targetId == row.evicteeId);
                int other = votes.Where(v => v.targetId != row.evicteeId).GroupBy(v => v.targetId).Select(g => g.Count()).DefaultIfEmpty(0).Max();
                foreach (var pact in before.alliances.Where(a => a?.members != null && a.active && Outsider(before, a, inLive)))
                {
                    var bloc = votes.Where(v => pact.members.Contains(v.voterId)).ToList();
                    if (bloc.Count < 3 || bloc.Any(v => v.targetId != row.evicteeId)) continue;
                    if (forEvictee - bloc.Count <= other) { record.blocCarried++; break; }
                }
            }

            /// <summary>As an NPC Head of Household's block is set: the live-pair members who could go up, and the house's share in NPC-only pacts.</summary>
            private void Nominations(EpisodeState before, HashSet<string> wasLive)
            {
                var npcs = before.Active.Where(c => !c.isPlayer).ToList();
                var pacts = NpcAlliances.NpcOnlyPacts(before);
                if (npcs.Count > 0) record.npcPactShares.Add(Math.Round(npcs.Count(c => pacts.Any(p => p.members.Contains(c.id))) / (double)npcs.Count, 6));
                if (before.hohId == null || before.hohId == before.playerId) return;
                nominations.Add((before.week, wasLive.Where(id => id != before.hohId && before.Find(id)?.status == ContestantStatus.Active)
                    .OrderBy(id => id, StringComparer.Ordinal).ToList()));
            }

            /// <summary>After an Advance, as NpcAgencyHarnessTests samples: the cap and one each, the bloc's weeks, the pacts the house's knowledge held back, week 3's cold pairs.</summary>
            private void Sample(EpisodeState after, int live, HashSet<string> inLive)
            {
                var pacts = after.alliances.Where(a => a?.members != null && a.active && NpcAlliances.HouseguestsOwn(a) && !a.members.Contains(after.playerId) && Here(after, a) >= 2).ToList();
                if (EpisodeEngine.AgencyOn(after) && pacts.Count > NpcAlliances.PactCap(after) && pacts.Count > lastPacts) Bug("a cap breach");
                lastPacts = pacts.Count;
                if (after.Active.Any(c => !c.isPlayer && pacts.Count(p => p.members.Contains(c.id)) > 1)) Bug("a houseguest in two pacts of their own");
                int bound = Bound(live);
                if (bound > 2 && !record.blocLiveWeeks.Contains(after.week)) record.blocLiveWeeks.Add(after.week);
                // Below the bound only because someone outside knows of it: every other pact-side rule of 14.L.B holds.
                foreach (var pact in pacts)
                {
                    if (Here(after, pact) >= bound || pact.members.Any(id => after.Find(id)?.status != ContestantStatus.Active) || pact.members.Any(inLive.Contains)) continue;
                    var fact = Knowledge.Of(after, FactKinds.Alliance, pact.id);
                    if (fact == null || fact.week >= after.week || !EpisodeEngine.StoryAt(after, StoryRules.Bonds) || PrivateToMembers(after, pact)) continue;
                    if (!heldWeeks.Add(pact.id + "|" + after.week.ToString(CultureInfo.InvariantCulture))) continue;
                    string cause = exposure.TryGetValue(pact.id, out string why) ? why : "other";
                    record.heldBack[cause] = (record.heldBack.TryGetValue(cause, out int n) ? n : 0) + 1;
                }
                if (!week3Taken && after.week == 3)
                {
                    week3Taken = true;
                    var npcs = after.Active.Where(c => !c.isPlayer).ToList();
                    record.coldPairsWeek3 = 0;
                    foreach (var a in npcs)
                        foreach (var b in npcs.Where(x => string.CompareOrdinal(a.id, x.id) < 0))
                            if (Math.Max(after.Score(a.id, b.id), after.Score(b.id, a.id)) <= -10) record.coldPairsWeek3++;
                }
            }

            /// <summary>The season's end: the rows read from the whole record of it.</summary>
            internal PactRecord Close(EpisodeState final)
            {
                Rules(final);
                var rows = (final.ledger?.power ?? new List<PowerRow>()).Where(r => r?.nominees != null && !FinalEviction(r)).ToList();
                foreach (var (week, candidates) in nominations)
                {
                    var row = rows.FirstOrDefault(r => r.week == week);
                    record.memberWeeks += candidates.Count;
                    if (row != null) record.memberPutUps += candidates.Count(id => FinalistRead.PutUp(row, id));
                }
                record.publicSplit = pairs.Any(p => rows.Any(r => (r.hohId == p.a && FinalistRead.PutUp(r, p.b)) || (r.hohId == p.b && FinalistRead.PutUp(r, p.a))));
                foreach (var who in final.contestants)
                {
                    int run = 0, best = 0, last = -1;
                    foreach (int week in rows.Where(r => FinalistRead.PutUp(r, who.id)).Select(r => r.week).Distinct().OrderBy(w => w))
                    {
                        run = week == last + 1 ? run + 1 : 1;
                        last = week;
                        best = Math.Max(best, run);
                    }
                    if (best >= 3) record.putUpThreeRunning = true;
                }
                var finalTwo = record.hitmenFirst == null ? null : final.deals.FirstOrDefault(d => d != null && d.type == DealKind.FinalTwo && d.week == 1
                    && ((d.proposerId == record.hitmenFirst && d.recipientId == record.hitmenSecond) || (d.proposerId == record.hitmenSecond && d.recipientId == record.hitmenFirst)));
                record.hitmenFinalTwo = finalTwo?.status ?? "none";
                record.goodwill = Goodwill(final);
                record.longestListenerRun = listenerWeeks.Values.Select(Longest).DefaultIfEmpty(0).Max();
                var subjects = new SortedDictionary<string, SortedSet<int>>(StringComparer.Ordinal);
                foreach (var edge in final.relationships)
                {
                    string name = final.Find(edge?.fromId)?.name;
                    if (name == null || edge.events == null) continue;
                    foreach (var e in edge.events)
                    {
                        if (e.type != "rumor" || e.description == null || !e.description.StartsWith("Something " + name + " heard about ", StringComparison.Ordinal)) continue;
                        record.rumours++;
                        if (!subjects.TryGetValue(edge.toId, out var weeks)) subjects[edge.toId] = weeks = new SortedSet<int>();
                        weeks.Add(e.week);
                    }
                }
                record.longestRumourRun = subjects.Values.Select(Longest).DefaultIfEmpty(0).Max();
                foreach (var pact in final.alliances.Where(a => a?.members != null && a.active && grown.Contains(a.id)))
                    record.grownEnds.Add(new GrownEnd { why = "standing", weeks = final.week - (final.ledger?.alliances?.FirstOrDefault(r => r.id == pact.id)?.startedWeek ?? final.week),
                        known = !PrivateToMembers(final, pact), largest = largest.TryGetValue(pact.id, out int most) ? most : 0 });
                record.scoresHash = ScoresHash(final);
                return record;
            }

            /// <summary>
            /// One of the watch's reads, guarded (14.L.H; S4's review): a throw ends the watch for the rest of the season
            /// (<paramref name="watch"/> set to null) and keeps its record, the error written on it, in <paramref name="failed"/>,
            /// so the season plays on and keeps its other rows, and the report counts "the watch threw" as a must-be-zero row.
            /// </summary>
            internal static void Guard(ref PactWatch watch, ref PactRecord failed, Action<PactWatch> read)
            {
                if (watch == null) return;
                try { read(watch); }
                catch (Exception error)
                {
                    watch.record.watchError = error.GetType().Name + ": " + error.Message;
                    failed = watch.record;
                    watch = null;
                }
            }

            /// <summary>The final house's relationship scores, every edge by its two ids in ordinal order, as a 16-hex SHA-256.</summary>
            internal static string ScoresHash(EpisodeState final)
            {
                var text = new StringBuilder();
                foreach (var edge in (final.relationships ?? new List<RelationshipState>()).Where(e => e != null)
                    .OrderBy(e => e.fromId, StringComparer.Ordinal).ThenBy(e => e.toId, StringComparer.Ordinal))
                    text.Append(edge.fromId).Append('>').Append(edge.toId).Append('=').Append(edge.score.ToString("R", CultureInfo.InvariantCulture)).Append(';');
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").Substring(0, 16).ToLowerInvariant();
            }

            private static int Longest(SortedSet<int> weeks)
            {
                int run = 0, best = 0, last = int.MinValue;
                foreach (int week in weeks)
                {
                    run = week == last + 1 ? run + 1 : 1;
                    last = week;
                    best = Math.Max(best, run);
                }
                return best;
            }

            /// <summary>
            /// The share of each positive past-game receipt its holder still counts at the end, averaged: from alumni rules 2
            /// <see cref="AlumniLoyalty.GoodwillShare"/>; at 1 the breach rule's all or nothing (§7.4); null with no receipt.
            /// </summary>
            private static double? Goodwill(EpisodeState final)
            {
                var shares = new List<double>();
                foreach (var edge in final.relationships)
                {
                    if (edge?.events == null || !edge.events.Any(e => AlumniHistories.IsReceipt(e) && e.impactScore > 0)) continue;
                    if (AlumniRules.At(final, AlumniRules.Loyalty)) shares.Add(AlumniLoyalty.GoodwillShare(final, edge.fromId, edge.toId));
                    else if (AlumniRules.On(final)) shares.Add(edge.events.Any(e => !e.decayable && e.impactScore < 0 && !AlumniHistories.IsReceipt(e)) ? 0 : 1);
                }
                return shares.Count == 0 ? (double?)null : Math.Round(shares.Average(), 6);
            }
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
