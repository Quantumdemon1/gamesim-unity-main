using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// What one season did, measured (BALANCE plan B1, the metrics of B.4): who went out when and why
    /// it might have been them, who won each competition against how strong they were on paper, the
    /// pacts, deals and promises by party and kind, the player's window seats spent and wasted and the
    /// time they bought, the NPC agendas and whether a Head of Household went after the top threat, the
    /// warmest and coldest pair, the jury's margin and its bitter jurors, the story's pace and Game Sense.
    ///
    /// <para><b>From what.</b> The final state and the season's phase changes: for every command that
    /// moved the phase or the week, the state just before it (the phase as it closed) and just after it
    /// (the next as it opened). The final state's own records - the ledger's week rows and competition
    /// rows, the commitments, the storylines - carry most of it; the phase changes carry what the state
    /// forgets as the season moves on: each competition's field and scores, each window's spend, who
    /// was in a pact or a Have-Not when, and the log lines that roll off the 256-line event log.</para>
    ///
    /// <para><b>An analyst's view.</b> Unlike the player's readers it reads hidden state (threat,
    /// statistics, NPC-only pacts and deals): it is for harnesses and reports, never for a screen.</para>
    ///
    /// <para><b>Pure.</b> It reads the states it is given and nothing else: it never mutates them, never
    /// draws from a generator, and the same input gives the same report.</para>
    /// </summary>
    public static class SeasonAutopsy
    {
        /// <summary>One command that moved the phase or the week: the state before it and after it.</summary>
        public sealed class PhaseChange
        {
            public EpisodeState before, after;
            public PhaseChange() { }
            public PhaseChange(EpisodeState before, EpisodeState after) { this.before = before; this.after = after; }
        }

        /// <summary>Whether a command took the season from one state to the next across a phase or a week: what a caller keeps for <see cref="Of"/>.</summary>
        public static bool IsPhaseChange(EpisodeState before, EpisodeState after) =>
            before != null && after != null && (before.phase != after.phase || before.week != after.week);

        public sealed class Report
        {
            public uint seed;
            public int houseSize, weeks;
            public bool finished;
            /// <summary>The player's end: <see cref="Outcomes"/>.</summary>
            public string outcome;
            /// <summary>1 won, 2 runner-up, the house's size for the first out; 0 when the season did not finish and the player is still in.</summary>
            public int placement;
            /// <summary>The week the player left the house, or 0.</summary>
            public int playerOutWeek;
            public int playerHohWins, playerVetoWins, playerTimesNominated;
            public List<WeekRow> weeksPlayed = new List<WeekRow>();
            public List<CompetitionResult> competitions = new List<CompetitionResult>();
            public List<Houseguest> houseguests = new List<Houseguest>();
            public Commitments commitments = new Commitments();
            public Economy economy = new Economy();
            public Agency agency = new Agency();
            public Pair warmest, coldest;
            public Jury jury = new Jury();
            public Story story = new Story();
            public int gameSense, gameSenseCompetitions, gameSenseStrategy, gameSenseSocial;
            public long npcTicks;
            /// <summary>The rule boundaries the season played under (<see cref="ShippedRules.Fields"/>), "name=value".</summary>
            public List<string> rules = new List<string>();
        }

        public static class Outcomes
        {
            public const string Winner = "winner", RunnerUp = "runner-up", Evicted = "evicted", Expelled = "expelled", Unfinished = "unfinished";
        }

        /// <summary>One week's power and its eviction.</summary>
        public sealed class WeekRow
        {
            public int week;
            public string hohId, vetoHolderId, evicteeId;
            public bool vetoUsed, finalEviction;
            public List<string> nominees = new List<string>();
            public List<int> tally = new List<int>();
            /// <summary>Where the evictee stood in the Head of Household's threat ranking as the campaign closed (1 the top threat), or 0 unknown.</summary>
            public int evicteeThreatRank;
            /// <summary>The evictee's competition wins, Head of Household and veto, as they left; -1 unknown.</summary>
            public int evicteeCompetitionWins = -1;
            /// <summary>Whether the evictee stood in a pact as the campaign closed.</summary>
            public bool evicteeInPact;
            public bool playerHoh, playerVetoHolder, playerNominated, playerEvicted, playerHaveNot;
            /// <summary>An NPC Head of Household's block held their top threat outside their own pacts: 1 yes, 0 no, -1 not an NPC nomination or unseen.</summary>
            public int topThreatNominated = -1;
            /// <summary>Houseguests handed a key at the nomination ceremony: the house less the Head of Household and the block.</summary>
            public int keys;
            public int votes;
            public bool tie;
        }

        /// <summary>One competition: the field, the winner and how strong they were on paper, and the player's part.</summary>
        public sealed class CompetitionResult
        {
            public int week;
            public string phase, category, winnerId;
            public int field;
            /// <summary>The winner's rank in the field by strength on paper (<see cref="EpisodeEngine.ExpectedWin"/>), 1 the strongest.</summary>
            public int winnerStatRank;
            public bool winnerIsPlayer, playerInField;
            public int playerPlacement;
            /// <summary>The player's performance input, or -1 when they were not in it.</summary>
            public double playerPerformance = -1;
            public double playerExpectedWin;
            public string playerEntry;
        }

        /// <summary>A houseguest's season: statistics as they stand, wins and where they finished.</summary>
        public sealed class Houseguest
        {
            public string id;
            public bool isPlayer;
            public double statSum;
            public int hohWins, vetoWins, timesNominated, placement;
            /// <summary>The week they left the house, or 0 for a finalist.</summary>
            public int outWeek;
            public string status;
        }

        /// <summary>Pacts, deals and promises: by party (the player in it, or houseguests only) and kind.</summary>
        public sealed class Commitments
        {
            public int playerPacts, npcPacts, storyPacts, playerPactsEnded, npcPactsEnded;
            /// <summary>Why pacts ended, "party:why" counted.</summary>
            public Dictionary<string, int> pactEndings = new Dictionary<string, int>(StringComparer.Ordinal);
            /// <summary>"party:kind:status" counted, over every deal the season holds.</summary>
            public Dictionary<string, int> deals = new Dictionary<string, int>(StringComparer.Ordinal);
            /// <summary>"party:kind:status" counted, over every promise the season holds.</summary>
            public Dictionary<string, int> promises = new Dictionary<string, int>(StringComparer.Ordinal);
            public int playerDeals, npcDeals, playerDealsKept, playerDealsBroken, npcDealsKept, npcDealsBroken;
            public int playerPromises, playerPromisesKept, playerPromisesBroken;
            /// <summary>Weeks in which the deal table refused the player for their ceiling of arrangements.</summary>
            public int ceilingWeeks;
            public int oathsSworn;
        }

        /// <summary>The player's window seats and bought time.</summary>
        public sealed class Economy
        {
            public List<WindowRow> windows = new List<WindowRow>();
            public int seatsOffered, seatsSpent, seatsWasted;
            public int purchases, purchasesFromOne, purchasesFromEveryone;
            /// <summary>The relationship points the purchases cost, summed over everyone they were taken from.</summary>
            public double goodwillPaid;
            public int haveNotWeeks;
        }

        public sealed class WindowRow
        {
            public int week, window, budget, spent;
        }

        public sealed class Agency
        {
            /// <summary>NPC agendas as each social week closed, by kind.</summary>
            public Dictionary<string, int> agendas = new Dictionary<string, int>(StringComparer.Ordinal);
            public int npcNominations, topThreatNominated;
        }

        public sealed class Pair
        {
            public string a, b;
            public double mutual;
        }

        public sealed class Jury
        {
            public int jurors, winnerVotes, runnerUpVotes, margin;
            /// <summary>Jurors whose evictor (the Head of Household the week they went) is a finalist, and those of them who voted against that finalist.</summary>
            public int jurorsWithEvictorFinalist, bitterJurors;
            public bool playerFinalist;
            public int playerVotes;
        }

        public sealed class Story
        {
            public int storylines, completed, asks, weeksWithACard, npcRemovals, showmances, pileOns, pariahWeeks;
        }

        // ---------------------------------------------------------------- the autopsy

        /// <summary>The season measured, from its phase changes (in order) and its final state.</summary>
        public static Report Of(IReadOnlyList<PhaseChange> changes, EpisodeState final)
        {
            if (final == null) throw new ArgumentNullException(nameof(final));
            changes = changes ?? new List<PhaseChange>();
            var r = new Report
            {
                seed = final.seed,
                houseSize = final.contestants.Count,
                weeks = final.week,
                finished = final.phase == EpisodePhase.Finished,
                npcTicks = final.npcSocial?.clockTick ?? 0,
                rules = ShippedRules.Fields(final).Select(f => f.Key + "=" + f.Value.ToString(CultureInfo.InvariantCulture)).ToList(),
            };
            var you = final.Find(final.playerId);
            r.playerHohWins = you?.hohWins ?? 0;
            r.playerVetoWins = you?.vetoWins ?? 0;
            r.playerTimesNominated = you?.timesNominated ?? 0;

            var closings = changes.Where(c => c?.before != null).Select(c => c.before).ToList();
            // The player's pacts are every pact they were in at a state the autopsy saw - each phase
            // change's before and after, and the end - not only those they are in at the end: a player
            // who leaves a pact of three leaves it standing, and the end alone read it as the house's.
            // A membership begun and ended between two phase changes is not seen.
            var playerHeld = new HashSet<string>(changes.SelectMany(c => new[] { c?.before, c?.after }).Concat(new[] { final })
                .Where(s => s?.alliances != null)
                .SelectMany(s => s.alliances.Where(a => a?.members != null && a.members.Contains(s.playerId)).Select(a => a.id)),
                StringComparer.Ordinal);

            Placements(r, final);
            Weeks(r, changes, final);
            Competitions(r, changes, final);
            CommitmentsOf(r, closings, playerHeld, final);
            EconomyOf(r, closings, final);
            AgencyOf(r, changes);
            Pairs(r, final);
            JuryOf(r, final);
            StoryOf(r, closings, final);
            var sense = GameSense.Evaluate(final);
            r.gameSense = sense.score; r.gameSenseCompetitions = sense.competitions; r.gameSenseStrategy = sense.strategy; r.gameSenseSocial = sense.social;
            return r;
        }

        // ---------------------------------------------------------------- who went out

        private static void Placements(Report r, EpisodeState final)
        {
            int n = final.contestants.Count;
            // Departures in order: each week's production removals, then its eviction; the ledger's
            // power rows are the season's record of evictions and outlive the log.
            var departures = new List<string>();
            var removals = final.story?.removals ?? new List<RemovalState>();
            var evictions = final.ledger.power.Where(p => !string.IsNullOrEmpty(p.evicteeId)).OrderBy(p => p.week).ToList();
            int lastWeek = Math.Max(final.week, evictions.Select(p => p.week).DefaultIfEmpty(0).Max());
            for (int week = 0; week <= lastWeek; week++)
            {
                foreach (var removal in removals.Where(x => x.week == week && !string.IsNullOrEmpty(x.contestantId)))
                    if (!departures.Contains(removal.contestantId)) departures.Add(removal.contestantId);
                foreach (var row in evictions.Where(p => p.week == week))
                    if (!departures.Contains(row.evicteeId)) departures.Add(row.evicteeId);
            }
            // Anybody gone whom neither record names (an older season's), in cast order, before the finalists.
            foreach (var c in final.contestants.Where(c => (c.status == ContestantStatus.Evicted || c.status == ContestantStatus.Jury
                         || c.status == ContestantStatus.Expelled) && !departures.Contains(c.id)))
                departures.Add(c.id);
            int Placement(ContestantState c)
            {
                if (c.status == ContestantStatus.Winner) return 1;
                if (c.status == ContestantStatus.RunnerUp) return 2;
                int index = departures.IndexOf(c.id);
                return index < 0 ? 0 : n - index;
            }
            // The week each houseguest left: a production removal's, else their eviction's; 0 for the final two.
            int OutWeek(ContestantState c) =>
                c.status == ContestantStatus.Expelled ? removals.Where(x => x.contestantId == c.id).Select(x => x.week).DefaultIfEmpty(0).First()
                : c.status == ContestantStatus.Evicted || c.status == ContestantStatus.Jury ? evictions.Where(p => p.evicteeId == c.id).Select(p => p.week).DefaultIfEmpty(0).First()
                : 0;
            foreach (var c in final.contestants)
                r.houseguests.Add(new Houseguest
                {
                    id = c.id, isPlayer = c.isPlayer, hohWins = c.hohWins, vetoWins = c.vetoWins, timesNominated = c.timesNominated,
                    statSum = StatSum(c.stats), placement = Placement(c), outWeek = OutWeek(c), status = c.status.ToString(),
                });
            var you = final.Find(final.playerId);
            if (you == null) { r.outcome = Outcomes.Unfinished; return; }
            r.placement = Placement(you);
            r.outcome = you.status == ContestantStatus.Winner ? Outcomes.Winner
                : you.status == ContestantStatus.RunnerUp ? Outcomes.RunnerUp
                : you.status == ContestantStatus.Expelled ? Outcomes.Expelled
                : you.status == ContestantStatus.Evicted || you.status == ContestantStatus.Jury ? Outcomes.Evicted
                : Outcomes.Unfinished;
            r.playerOutWeek = OutWeek(you);
        }

        /// <summary>The statistics a competition reads: physical, mental, endurance, social, luck and competition.</summary>
        public static double StatSum(ContestantStats stats) =>
            stats == null ? 0 : stats.physical + stats.mental + stats.endurance + stats.social + stats.luck + stats.competition;

        // ---------------------------------------------------------------- each week

        private static void Weeks(Report r, IReadOnlyList<PhaseChange> changes, EpisodeState final)
        {
            var you = final.Find(final.playerId);
            foreach (var power in final.ledger.power.OrderBy(p => p.week))
            {
                var row = new WeekRow
                {
                    week = power.week, hohId = power.hohId, vetoHolderId = power.vetoHolderId, vetoUsed = power.vetoUsed,
                    evicteeId = power.evicteeId, nominees = new List<string>(power.nominees ?? new List<string>()),
                    tally = new List<int>(power.tally ?? new List<int>()),
                };
                row.finalEviction = row.tally.Count == 0 && !string.IsNullOrEmpty(row.evicteeId);
                row.votes = row.tally.Sum();
                row.tie = row.tally.Count == 2 && row.tally[0] == row.tally[1] && row.votes > 0;
                row.playerHoh = power.hohId == final.playerId;
                row.playerVetoHolder = power.vetoHolderId == final.playerId;
                row.playerEvicted = power.evicteeId == final.playerId;
                row.playerNominated = you != null && you.nominationWeeks != null && you.nominationWeeks.Contains(power.week);

                // The campaign as it closed: the evictee still in the house, their threat and their pacts.
                var campaign = changes.LastOrDefault(c => c?.before != null && c.before.week == power.week && c.before.phase == EpisodePhase.Campaign)?.before;
                var evictee = campaign?.Find(power.evicteeId);
                if (evictee != null)
                {
                    string ranker = !string.IsNullOrEmpty(campaign.hohId) && campaign.Find(campaign.hohId) != null ? campaign.hohId : null;
                    if (ranker != null && ranker != evictee.id)
                    {
                        int index = ThreatAssessment.RankedTargets(campaign, ranker).IndexOf(evictee.id);
                        row.evicteeThreatRank = index < 0 ? 0 : index + 1;
                    }
                    row.evicteeCompetitionWins = evictee.hohWins + evictee.vetoWins;
                    row.evicteeInPact = campaign.alliances.Any(a => a.active && a.members.Contains(evictee.id));
                }
                else if (power.evicteeId != null && final.Find(power.evicteeId) != null && row.finalEviction)
                {
                    var finalEvictee = final.Find(power.evicteeId);
                    row.evicteeCompetitionWins = finalEvictee.hohWins + finalEvictee.vetoWins;
                }

                // The block as the veto's selection opened: the Head of Household's nominations, before any veto.
                var block = changes.FirstOrDefault(c => c?.after != null && c.after.week == power.week && c.after.phase == EpisodePhase.VetoSelection)?.after;
                if (block != null && block.nominees.Count == 2)
                {
                    row.keys = Math.Max(0, block.Active.Count() - block.nominees.Count - 1);
                    if (!string.IsNullOrEmpty(block.hohId) && block.hohId != block.playerId)
                    {
                        string top = ThreatAssessment.RankedTargets(block, block.hohId).FirstOrDefault(id => !block.Allied(block.hohId, id) && id != block.hohId);
                        row.topThreatNominated = top != null && block.nominees.Contains(top) ? 1 : 0;
                    }
                }
                row.playerHaveNot = changes.Any(c => c?.before != null && c.before.week == power.week && c.before.haveNots != null && c.before.haveNots.Contains(final.playerId));
                r.weeksPlayed.Add(row);
            }
        }

        // ---------------------------------------------------------------- competitions

        private static readonly EpisodePhase[] CompetitionPhases =
            { EpisodePhase.HoH, EpisodePhase.Veto, EpisodePhase.FinalHoHPart1, EpisodePhase.FinalHoHPart2, EpisodePhase.FinalHoHPart3 };

        private static void Competitions(Report r, IReadOnlyList<PhaseChange> changes, EpisodeState final)
        {
            // A competition resolves inside its phase, and its scores stand until the next one starts or
            // the week turns: the state just after its phase closes holds them, however it was resolved.
            foreach (var change in changes)
            {
                if (change?.before == null || change.after == null) continue;
                if (Array.IndexOf(CompetitionPhases, change.before.phase) < 0 || change.after.phase == change.before.phase) continue;
                if (change.after.week != change.before.week) continue;
                var at = change.after;
                if (at.competitionScores.Count == 0) continue;
                var field = at.competitionScores.Select(x => at.Find(x.contestantId)).Where(c => c != null).ToList();
                if (field.Count == 0) continue;
                // The category as it was played: the closing phase's, under the season's own rules.
                string category = EpisodeEngine.CompetitionCategory(change.before);
                string winner = at.competitionScores.OrderByDescending(x => x.score).First().contestantId;
                var strength = field.Select(c => (id: c.id, chance: EpisodeEngine.ExpectedWin(change.before, field, category, c.id)))
                    .OrderByDescending(x => x.chance).ThenBy(x => x.id, StringComparer.Ordinal).Select(x => x.id).ToList();
                var result = new CompetitionResult
                {
                    week = change.before.week, phase = change.before.phase.ToString(), category = category, field = field.Count,
                    winnerId = winner, winnerIsPlayer = winner == final.playerId, winnerStatRank = strength.IndexOf(winner) + 1,
                    playerInField = field.Any(c => c.id == final.playerId),
                };
                if (result.playerInField)
                {
                    var order = at.competitionScores.OrderByDescending(x => x.score).Select(x => x.contestantId).ToList();
                    result.playerPlacement = order.IndexOf(final.playerId) + 1;
                    var row = final.ledger.competitions.LastOrDefault(x => x.week == result.week && x.kind == result.phase);
                    if (row != null)
                    {
                        result.playerPerformance = row.performance;
                        result.playerExpectedWin = row.expectedWin;
                        result.playerEntry = row.entry;
                    }
                }
                r.competitions.Add(result);
            }
        }

        // ---------------------------------------------------------------- pacts, deals, promises

        private static string Party(EpisodeState s, IEnumerable<string> ids) => ids.Contains(s.playerId) ? "player" : "npc";

        private static void Count(Dictionary<string, int> into, string key) => into[key] = (into.TryGetValue(key, out int n) ? n : 0) + 1;

        private static void CommitmentsOf(Report r, IReadOnlyList<EpisodeState> closings, ISet<string> playerHeld, EpisodeState final)
        {
            var c = r.commitments;
            foreach (var row in final.ledger.alliances)
            {
                string party = playerHeld.Contains(row.id) ? "player" : "npc";
                string origin = (row.why ?? "").Split('/')[0];
                if (party == "player") c.playerPacts++;
                else if (origin == "story") c.storyPacts++;
                else c.npcPacts++;
                if (row.endedWeek > 0)
                {
                    if (party == "player") c.playerPactsEnded++; else c.npcPactsEnded++;
                    string why = (row.why ?? "").Contains("/") ? row.why.Substring(row.why.IndexOf('/') + 1) : "ended";
                    Count(c.pactEndings, party + ":" + why);
                }
            }
            var deals = UsesReferences(final) ? CommitmentReferences.Deals(final) : (IReadOnlyList<DealState>)final.deals;
            foreach (var d in deals.Where(d => d != null))
            {
                string party = Party(final, new[] { d.proposerId, d.recipientId });
                Count(c.deals, party + ":" + d.type + ":" + d.status);
                bool kept = d.status == DealStatus.Fulfilled, broken = d.status == DealStatus.Broken;
                if (party == "player") { c.playerDeals++; if (kept) c.playerDealsKept++; if (broken) c.playerDealsBroken++; }
                else { c.npcDeals++; if (kept) c.npcDealsKept++; if (broken) c.npcDealsBroken++; }
            }
            var promises = UsesReferences(final) ? CommitmentReferences.Promises(final) : (IReadOnlyList<PromiseState>)final.promises;
            foreach (var p in promises.Where(p => p != null))
            {
                string party = Party(final, new[] { p.fromId, p.toId });
                Count(c.promises, party + ":" + p.kind + ":" + p.status);
                if (party != "player") continue;
                c.playerPromises++;
                if (p.status == PromiseStatus.Fulfilled) c.playerPromisesKept++;
                if (p.status == PromiseStatus.Broken) c.playerPromisesBroken++;
            }
            c.oathsSworn = final.loyaltyOaths?.Count(o => o != null && o.playerId == final.playerId) ?? 0;
            var ceilingWeeks = new HashSet<int>();
            foreach (var s in closings.Concat(new[] { final }))
                if (!ceilingWeeks.Contains(s.week) && AtDealCeiling(s)) ceilingWeeks.Add(s.week);
            c.ceilingWeeks = ceilingWeeks.Count;
        }

        /// <summary>Whether the commitments live partly in the unified store, where the detached references read them.</summary>
        private static bool UsesReferences(EpisodeState s) => s.unifiedCommitmentRulesVersion != 0;

        /// <summary>Whether the deal table refuses the player for their ceiling of arrangements: asked of the gate itself.</summary>
        public static bool AtDealCeiling(EpisodeState s)
        {
            if (s?.Find(s.playerId)?.status != ContestantStatus.Active) return false;
            var someone = s.Active.FirstOrDefault(x => !x.isPlayer);
            return someone != null && !PlayerDeals.CanPropose(s, someone.id, DealKind.InformationSharing, null, out string reason)
                && reason == Negotiation.TooManyArrangements;
        }

        // ---------------------------------------------------------------- the economy

        private static void EconomyOf(Report r, IReadOnlyList<EpisodeState> closings, EpisodeState final)
        {
            var e = r.economy;
            foreach (var s in closings)
            {
                int window = EpisodeEngine.Window(s);
                if (window == Windows.None || s.Find(s.playerId)?.status != ContestantStatus.Active) continue;
                // A window spanning phases (the veto's three) closes with its last: the latest closing wins.
                var row = e.windows.FirstOrDefault(w => w.week == s.week && w.window == window);
                if (row == null) e.windows.Add(row = new WindowRow { week = s.week, window = window });
                row.budget = EpisodeEngine.SocialActionBudget(s);
                row.spent = s.windowActions != null && window < s.windowActions.Count ? s.windowActions[window] : 0;
            }
            e.seatsOffered = e.windows.Sum(w => w.budget);
            e.seatsSpent = e.windows.Sum(w => w.spent);
            e.seatsWasted = e.windows.Sum(w => Math.Max(0, w.budget - w.spent));
            // Bought time, from its log lines as each phase closed (the log rolls over; each line is read once).
            var seen = new HashSet<int>();
            foreach (var s in closings.Concat(new[] { final }))
                foreach (var line in s.events.Where(x => x != null && x.kind == "bought-action"))
                {
                    if (!seen.Add(line.sequence)) continue;
                    e.purchases++;
                    int others = Math.Max(0, s.Active.Count(x => x.id != s.playerId));
                    if (line.text != null && line.text.EndsWith("the whole house felt it.", StringComparison.Ordinal))
                    {
                        e.purchasesFromEveryone++;
                        e.goodwillPaid += -WebSocialVocabulary.SpreadAllCost * others;
                    }
                    else
                    {
                        e.purchasesFromOne++;
                        e.goodwillPaid += -WebSocialVocabulary.BurnOneCost;
                    }
                }
            var haveNotWeeks = new HashSet<int>();
            foreach (var s in closings.Concat(new[] { final }))
                if (s.haveNots != null && s.haveNots.Contains(s.playerId)) haveNotWeeks.Add(s.week);
            e.haveNotWeeks = haveNotWeeks.Count;
        }

        // ---------------------------------------------------------------- agency

        private static void AgencyOf(Report r, IReadOnlyList<PhaseChange> changes)
        {
            foreach (var change in changes)
            {
                var s = change?.before;
                if (s == null || s.phase != EpisodePhase.Social || change.after.phase == EpisodePhase.Social) continue;
                foreach (var npc in s.Active.Where(c => !c.isPlayer))
                    Count(r.agency.agendas, NpcAgendas.Of(s, npc.id)?.kind ?? "none");
            }
            foreach (var week in r.weeksPlayed.Where(w => w.topThreatNominated >= 0))
            {
                r.agency.npcNominations++;
                r.agency.topThreatNominated += week.topThreatNominated;
            }
        }

        // ---------------------------------------------------------------- the pairs

        private static void Pairs(Report r, EpisodeState final)
        {
            var everyone = final.contestants.OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            foreach (var a in everyone)
                foreach (var b in everyone.Where(x => string.CompareOrdinal(a.id, x.id) < 0))
                {
                    double mutual = final.Score(a.id, b.id) + final.Score(b.id, a.id);
                    if (r.warmest == null || mutual > r.warmest.mutual) r.warmest = new Pair { a = a.id, b = b.id, mutual = mutual };
                    if (r.coldest == null || mutual < r.coldest.mutual) r.coldest = new Pair { a = a.id, b = b.id, mutual = mutual };
                }
        }

        // ---------------------------------------------------------------- the jury

        private static void JuryOf(Report r, EpisodeState final)
        {
            if (final.phase != EpisodePhase.Finished || string.IsNullOrEmpty(final.winnerId) || string.IsNullOrEmpty(final.runnerUpId)) return;
            var j = r.jury;
            var ballots = final.votes.Where(v => v != null && (v.targetId == final.winnerId || v.targetId == final.runnerUpId)).ToList();
            j.jurors = ballots.Count;
            j.winnerVotes = ballots.Count(v => v.targetId == final.winnerId);
            j.runnerUpVotes = ballots.Count(v => v.targetId == final.runnerUpId);
            j.margin = j.winnerVotes - j.runnerUpVotes;
            j.playerFinalist = final.winnerId == final.playerId || final.runnerUpId == final.playerId;
            j.playerVotes = j.playerFinalist ? ballots.Count(v => v.targetId == final.playerId) : 0;
            foreach (var ballot in ballots)
            {
                string evictor = final.ledger.power.Where(p => p.evicteeId == ballot.voterId).Select(p => p.hohId).FirstOrDefault();
                if (evictor != final.winnerId && evictor != final.runnerUpId) continue;
                j.jurorsWithEvictorFinalist++;
                if (ballot.targetId != evictor) j.bitterJurors++;
            }
        }

        // ---------------------------------------------------------------- the story

        private static void StoryOf(Report r, IReadOnlyList<EpisodeState> closings, EpisodeState final)
        {
            var st = r.story;
            st.storylines = final.storylines?.Count ?? 0;
            st.completed = final.storylines?.Count(x => x.status == StorylineStatus.Completed) ?? 0;
            var beats = final.houseEvents?.Where(x => x != null && x.IsStory).ToList() ?? new List<HouseEventState>();
            st.asks = beats.Count;
            st.weeksWithACard = beats.Select(x => x.week).Distinct().Count();
            st.npcRemovals = final.story?.removals?.Count(x => x.contestantId != final.playerId) ?? 0;
            st.showmances = final.story?.bonds?.Count(b => b.kind == BondKinds.Showmance && b.aId != final.playerId && b.bId != final.playerId) ?? 0;
            st.pileOns = final.storylines?.Count(x => x.templateId == "the-house-turns") ?? 0;
            // A pariah: somebody (not the reigning Head of Household) three in the house hold forty against, as eviction night closes.
            var pariahWeeks = new HashSet<int>();
            foreach (var s in closings.Where(x => x.phase == EpisodePhase.Eviction && x.story != null))
                if (s.Active.Any(c => !c.isPlayer && c.id != s.hohId
                        && Grudges.HoldersAgainst(s, c.id, 40).Count(h => s.Find(h)?.status == ContestantStatus.Active) >= 3))
                    pariahWeeks.Add(s.week);
            st.pariahWeeks = pariahWeeks.Count;
        }
    }
}
