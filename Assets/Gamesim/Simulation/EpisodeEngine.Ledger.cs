using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The ledger's writers (STRATEGY-LOOP-PLAN.md §6): competitions as they resolve, the week's
    /// power at the veto and the reveal, alliances reconciled after every command, a juror's view of
    /// the player as they leave, and the chances the season offered — plays, deals, pleas, the read —
    /// brought up to date as they are answered, lapse or end. The verdict reads these rows and
    /// nothing else, so a season's record never rolls off with the event log.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>How sharply a stronger competitor is favoured in the expected win: strengths cubed, then shared.</summary>
        private const double ExpectedWinSharpness = 3;

        /// <summary>The line under which a member has soured on an alliance, the NPC pacts' own.</summary>
        private const double AllianceSourLine = -20;

        /// <summary>
        /// A competitor's expected chance of winning this competition: the deterministic part of
        /// everybody's score (the roll at its midpoint, no luck draw, no bonus), raised to a power
        /// and shared, so a field of equals is one in n and a stronger competitor is favoured more
        /// than linearly. Not the engine's roll, which the verdict never sees: the odds the player
        /// beat or fell short of.
        /// </summary>
        public static double ExpectedWin(EpisodeState s, IReadOnlyList<ContestantState> players, string category, string whoId)
        {
            if (s == null || players == null || players.Count == 0 || players.All(p => p.id != whoId)) return 0;
            double Strength(ContestantState c)
            {
                bool nominated = s.nominees.Contains(c.id);
                double raw = s.phase == EpisodePhase.FinalHoHPart1 ? c.stats.endurance + c.stats.physical * .3
                    : s.competitionRulesVersion >= CompetitionRules.Widened
                        ? CompetitionRules.Score(c.stats, category, nominated, 0, 0.5, 0.5)
                        : WebRules.WeightedCompetitionScore(c.stats, category, nominated, 0, 0.5, 0);
                return Math.Pow(Math.Max(0.01, raw), ExpectedWinSharpness);
            }
            double total = players.Sum(Strength);
            return total <= 0 ? 1.0 / players.Count : Strength(players.First(p => p.id == whoId)) / total;
        }

        /// <summary>The competition just resolved, on the record: the field, where the player placed, how they entered, and the odds they had.</summary>
        private static void RecordCompetition(EpisodeState s, IReadOnlyList<ContestantState> players, string category, string entry, double performance)
        {
            var you = players.FirstOrDefault(p => p.isPlayer);
            var order = s.competitionScores.OrderByDescending(x => x.score).Select(x => x.contestantId).ToList();
            SeasonLedger.Append(s.ledger, s.ledger.competitions, new CompetitionRow
            {
                week = s.week, kind = s.phase.ToString(), field = players.Count,
                placement = you == null ? 0 : order.IndexOf(you.id) + 1,
                entry = you == null ? CompetitionEntry.Watched : entry,
                performance = you == null ? 0 : Math.Max(0, Math.Min(1, performance)),
                expectedWin = you == null ? 0 : Math.Max(0, Math.Min(1, ExpectedWin(s, players, category, you.id))),
            });
        }

        // ---------------------------------------------------------------- the week's power

        /// <summary>This week's power row, begun by the first thing that settles it.</summary>
        private static PowerRow PowerThisWeek(EpisodeState s)
        {
            var row = s.ledger.power.LastOrDefault(p => p.week == s.week);
            if (row == null) SeasonLedger.Append(s.ledger, s.ledger.power, row = new PowerRow { week = s.week });
            return row;
        }

        /// <summary>The veto decided: used or not, who it saved, who went up in their place.</summary>
        private static void RecordVeto(EpisodeState s, bool used, string savedId, string replacementId)
        {
            var row = PowerThisWeek(s);
            row.hohId = s.hohId; row.vetoHolderId = s.vetoHolderId; row.vetoUsed = used;
            row.savedId = used ? savedId : null; row.replacementId = used ? replacementId : null;
        }

        /// <summary>
        /// The reveal: the final block, who went, by what tally, and whether a backdoor planned this
        /// week (the player's, or an NPC Head of Household's) was made, survived or missed.
        /// </summary>
        private static void RecordReveal(EpisodeState s, string evictedId, IReadOnlyList<int> tally)
        {
            var row = PowerThisWeek(s);
            row.hohId = s.hohId; row.vetoHolderId = row.vetoHolderId ?? s.vetoHolderId;
            row.nominees = new List<string>(s.nominees); row.tally = new List<int>(tally); row.evicteeId = evictedId;
            string target = s.backdoorTargetId ?? BackdoorPlanned(s);
            row.backdoorTargetId = target;
            row.backdoorResult = target == null ? null : target == evictedId ? "made" : s.nominees.Contains(target) ? "survived" : "missed";
        }

        /// <summary>The final eviction: the last Head of Household's choice, as a power row of its own.</summary>
        private static void RecordFinalEviction(EpisodeState s, string evictedId, IReadOnlyList<string> finalists)
        {
            var row = PowerThisWeek(s);
            row.hohId = s.hohId; row.nominees = finalists.Take(2).ToList(); row.evicteeId = evictedId;
        }

        /// <summary>A juror's view of the player as they leave, kept for the verdict's social face.</summary>
        private static void RecordJurorStanding(EpisodeState s, string evictedId)
        {
            if (string.IsNullOrEmpty(evictedId) || evictedId == s.playerId) return;
            AddStanding(s, evictedId, s.playerId, ClaimSource.Juror, s.Score(evictedId, s.playerId));
        }

        // ---------------------------------------------------------------- alliances

        /// <summary>
        /// The alliance rows brought up to date with the alliances themselves, after every command:
        /// a new alliance gains a row that says where it came from, and a row whose alliance is gone
        /// learns when and why. Reconciled rather than hooked, so every path that forms or ends one
        /// — the player's, an invitation's, the NPC pass's, a story's — is covered without a hook in it.
        /// </summary>
        public static void ReconcileAllianceRows(EpisodeState s)
        {
            if (s?.ledger == null) return;
            foreach (var alliance in s.alliances.Where(a => a.active))
                if (s.ledger.alliances.All(r => r.id != alliance.id))
                    SeasonLedger.Append(s.ledger, s.ledger.alliances,
                        new AllianceRow { id = alliance.id, startedWeek = Math.Max(1, s.week), why = AllianceOrigin(alliance) });
            foreach (var row in s.ledger.alliances.Where(r => r.endedWeek == 0).ToList())
            {
                var alliance = s.alliances.FirstOrDefault(a => a.id == row.id);
                if (alliance != null && alliance.active) continue;
                row.endedWeek = Math.Max(row.startedWeek, s.week);
                row.why = row.why + "/" + AllianceEnding(s, alliance);
            }
        }

        private static string AllianceOrigin(AllianceState alliance) =>
            alliance.id.StartsWith("alliance-npc-", StringComparison.Ordinal) ? "npc"
            : alliance.id.StartsWith("alliance-story-", StringComparison.Ordinal) ? "story" : "player";

        /// <summary>
        /// Why an alliance ended, as far as the state can tell: somebody left the house, it turned on the
        /// player, it soured, or it simply ended. Under the commitment rules a pact ends with a departure
        /// only once fewer than two of it are left in the house - a member who left a bigger one left it,
        /// and it went on without them (X5), so only those still in it are read for the rest - and one
        /// a member had turned on ended in a betrayal (C2): the player cut ties, or it went its way since.
        /// </summary>
        private static string AllianceEnding(EpisodeState s, AllianceState alliance)
        {
            if (alliance == null) return "ended";
            bool rules = CommitmentRulesOn(s);
            if (rules ? alliance.members.Count(id => s.Find(id)?.status == ContestantStatus.Active) < 2
                      : alliance.members.Any(id => s.Find(id)?.status != ContestantStatus.Active)) return "left-house";
            var members = rules ? alliance.members.Where(id => s.Find(id)?.status == ContestantStatus.Active).ToList() : alliance.members;
            if (rules && members.Contains(s.playerId) && members.Any(id => id != s.playerId && Allegiance.Stands(s, id, alliance))) return "betrayed";
            if (members.Contains(s.playerId)
                && members.Where(id => id != s.playerId).Any(id => s.Score(id, s.playerId) < AllianceSourLine)) return "turned";
            if (members.Any(from => members.Any(to => to != from && s.Score(from, to) < AllianceSourLine))) return "soured";
            return "ended";
        }

        // ---------------------------------------------------------------- opportunities

        /// <summary>The row for a chance, begun the first time it is seen.</summary>
        private static OpportunityRow Opportunity(EpisodeState s, string id, string kind, int week)
        {
            var row = s.ledger.opportunities.FirstOrDefault(o => o.id == id);
            if (row == null)
                SeasonLedger.Append(s.ledger, s.ledger.opportunities, row = new OpportunityRow
                {
                    id = id, kind = kind, week = Math.Max(1, Math.Min(week, s.week)),
                    response = OpportunityResponse.Ignored, outcome = OpportunityOutcome.NotApplicable,
                });
            return row;
        }

        /// <summary>
        /// The pleas on the record before the lobbying clears with the week: each one taken, and won
        /// or lost by how it was heard. Every row in the list: the turn has already moved the week on
        /// when this runs, so the rows are last week's, which is what their own week says.
        /// </summary>
        private static void RecordPleas(EpisodeState s)
        {
            foreach (var plea in s.lobbies)
            {
                var row = Opportunity(s, "lobby-" + plea.week + "-" + plea.phase + "-" + plea.deciderId + "-" + plea.ask,
                    plea.ask == LobbyAsk.Vote ? OpportunityKinds.Plea : OpportunityKinds.Lobby, plea.week);
                row.anchor = plea.phase.ToString();
                row.source = plea.ask + (plea.subjectId != null ? ":" + plea.subjectId : "");
                row.response = OpportunityResponse.Taken;
                row.outcome = StrategyRules.Landed(plea.response) ? OpportunityOutcome.Won : OpportunityOutcome.Lost;
                row.note = plea.approach + ", " + plea.response;
                row.payoff = plea.influence;
            }
        }

        /// <summary>
        /// Every chance on the record brought up to date: deals offered to the player and put by them,
        /// plays offered by the story (taken, declined, let pass; won, part, lost), and the week's read.
        /// Run at the reveal, the final eviction and the end, when statuses have settled.
        /// </summary>
        public static void ReconcileOpportunities(EpisodeState s)
        {
            if (s?.ledger == null) return;
            foreach (var deal in s.deals.Where(d => d.proposerId == s.playerId || d.recipientId == s.playerId))
            {
                var row = Opportunity(s, deal.id, OpportunityKinds.Deal, deal.week);
                row.source = deal.type + (deal.targetId != null ? ":" + deal.targetId : "");
                row.note = (deal.proposerId == s.playerId ? "put to " + deal.recipientId : "offered by " + deal.proposerId) + ", " + deal.status;
                row.response = deal.status == DealStatus.Declined ? OpportunityResponse.Declined
                    : deal.status == DealStatus.Expired ? OpportunityResponse.Expired
                    : deal.status == DealStatus.Proposed ? OpportunityResponse.Ignored : OpportunityResponse.Taken;
                row.outcome = deal.status == DealStatus.Fulfilled ? OpportunityOutcome.Won
                    : deal.status == DealStatus.Broken ? OpportunityOutcome.Lost : OpportunityOutcome.NotApplicable;
            }
            foreach (var cycle in s.storylines)
            {
                var play = StoryCatalog.Find(cycle.templateId)?.play;
                if (play == null) continue;
                var row = Opportunity(s, cycle.id, OpportunityKinds.Play, cycle.week);
                row.anchor = play.deadline; row.source = cycle.templateId;
                row.currency = PayoffCurrency.IsKnown(play.currency) ? play.currency : null;
                bool taken = TakenOn(cycle);
                row.response = taken ? OpportunityResponse.Taken
                    : cycle.endingId == PlayEndings.Declined
                        ? (cycle.path.Any(step => step.optionId == PlayOptions.NotNow && step.result != "lapsed") ? OpportunityResponse.Declined : OpportunityResponse.Expired)
                        : OpportunityResponse.Ignored;
                row.outcome = PlayEndings.IsDecided(cycle.endingId) ? cycle.endingId : OpportunityOutcome.NotApplicable;
                row.note = cycle.endingId;
                row.steps = cycle.path.Where(step => step.optionId != PlayOptions.TakeItOn && step.optionId != PlayOptions.NotNow)
                    .Take(32).Select(step => new OpportunityStep { at = step.beatId, choice = step.optionId }).ToList();
            }
            if (s.nominees.Count == 2 && s.phase == EpisodePhase.Eviction)
            {
                var row = Opportunity(s, "read-" + s.week, OpportunityKinds.Read, s.week);
                row.anchor = "EvictionNight";
                bool read = s.ledger.claims.Any(k => k.week == s.week)
                    || s.ledger.standings.Any(r => r.week == s.week && r.toId == s.playerId && (r.source == ClaimSource.Read || ClaimSource.IsAttempt(r.source)))
                    || s.ledger.ballots.Any(b => b.week == s.week && b.readBefore != null);
                var ballot = s.ledger.ballots.LastOrDefault(b => b.week == s.week && b.voterId == s.playerId);
                row.response = read ? OpportunityResponse.Taken : OpportunityResponse.Ignored;
                row.outcome = ballot == null || ballot.readBefore == null ? OpportunityOutcome.NotApplicable
                    : ballot.correct ? OpportunityOutcome.Won : OpportunityOutcome.Lost;
            }
        }
    }
}
