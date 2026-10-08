using Gamesim.Persistence.Frozen25Data;
// Exact semantic snapshot of EpisodeLedgerValidation.cs at f4566b69; SHA256 b3f04f667cfe548eaf1f769b571cead3301f15a58dfd35c2d832a0d00ec11fe9.
// UNUSED contract. Never route this snapshot through later live validators or rule helpers.
using System.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Schema 17's invariants, and schema 18's two lists: the season ledger. Every list is bounded by its cap, every id names a
    /// houseguest, every week is one the season has reached, and every vocabulary word is one the
    /// ledger knows, so a save cannot carry a claim from nobody, about nobody, made never.
    /// </summary>
    internal static partial class FrozenV25Validator
    {
        private static bool TryValidateLedger(EpisodeState s, out string error)
        {
            error = null;
            var l = s.ledger;
            if (l == null) return Fail(out error, "Missing season ledger.");
            bool Id(string id) => id != null && s.contestants.Any(c => c.id == id);
            bool OptionalId(string id) => string.IsNullOrEmpty(id) || Id(id);
            bool Week(int week) => week >= 1 && week <= s.week;
            int most = SeasonLedger.MostRows;

            if (l.dropped < 0 || l.dropped > 1000000) return Fail(out error, "Invalid ledger drop count.");
            if (l.claims == null || l.claims.Count > most || l.claims.Any(k => k == null || !Id(k.voterId) || !Id(k.targetId)
                    || k.voterId == k.targetId || !Week(k.week) || !ClaimSource.IsKnown(k.source) || !ClaimStatus.IsKnown(k.status)))
                return Fail(out error, "Invalid vote claim data.");
            if (l.standings == null || l.standings.Count > most || l.standings.Any(r => r == null || !Id(r.fromId) || !Id(r.toId)
                    || r.fromId == r.toId || !Week(r.week) || !ClaimSource.IsKnown(r.source) || !Finite(r.score) || r.score < -1000 || r.score > 1000))
                return Fail(out error, "Invalid standing data.");
            if (l.ballots == null || l.ballots.Count > most || l.ballots.Any(b => b == null || !Id(b.voterId) || !Id(b.targetId)
                    || b.voterId == b.targetId || !OptionalId(b.readBefore) || !Week(b.week)))
                return Fail(out error, "Invalid ballot record data.");
            if (l.opportunities == null || l.opportunities.Count > most || l.opportunities.Any(o => o == null || !Text(o.id, 160)
                    || !OpportunityKinds.IsKnown(o.kind) || !ShortOrAbsent(o.anchor, 64) || !ShortOrAbsent(o.source, 160)
                    || !OpportunityResponse.IsKnown(o.response) || !OpportunityOutcome.IsKnown(o.outcome)
                    || !(string.IsNullOrEmpty(o.currency) || PayoffCurrency.IsKnown(o.currency)) || !ShortOrAbsent(o.note, 1000)
                    || !Week(o.week) || !Finite(o.payoff) || o.steps == null || o.steps.Count > 32
                    || o.steps.Any(step => step == null || !ShortOrAbsent(step.at, 64) || !ShortOrAbsent(step.choice, 160)))
                || l.opportunities.GroupBy(o => o.id).Any(g => g.Count() > 1))
                return Fail(out error, "Invalid opportunity data.");
            if (l.competitions == null || l.competitions.Count > most || l.competitions.Any(c => c == null || !Week(c.week)
                    || !ShortOrAbsent(c.kind, 64) || !CompetitionEntry.IsKnown(c.entry) || c.field < 0 || c.field > MaximumCast
                    || c.placement < 0 || c.placement > MaximumCast || !Finite(c.performance) || c.performance < 0 || c.performance > 1
                    || !Finite(c.expectedWin) || c.expectedWin < 0 || c.expectedWin > 1))
                return Fail(out error, "Invalid competition record data.");
            if (l.power == null || l.power.Count > most || l.power.Any(p => p == null || !Week(p.week) || !OptionalId(p.hohId)
                    || !OptionalId(p.vetoHolderId) || !OptionalId(p.savedId) || !OptionalId(p.replacementId) || !OptionalId(p.evicteeId)
                    || !OptionalId(p.backdoorTargetId) || !ShortOrAbsent(p.backdoorResult, 64) || p.nominees == null || p.nominees.Count > 2
                    || p.nominees.Any(id => !Id(id)) || p.tally == null || p.tally.Count > 2 || p.tally.Any(n => n < 0 || n > MaximumCast)))
                return Fail(out error, "Invalid power record data.");
            if (l.alliances == null || l.alliances.Count > most || l.alliances.Any(a => a == null || !Text(a.id, 160)
                    || !ShortOrAbsent(a.why, 160) || !Week(a.startedWeek) || (a.endedWeek != 0 && !Week(a.endedWeek))))
                return Fail(out error, "Invalid alliance record data.");
            if (l.replies == null || l.replies.Count > most || l.replies.Any(r => r == null || !Text(r.cardId, 160) || !ShortOrAbsent(r.kind, 64)
                    || !Id(r.fromId) || !OptionalId(r.listenerId) || !ShortOrAbsent(r.replyKey, 64) || !Week(r.week)
                    || !Finite(r.toThem) || r.toThem < -100 || r.toThem > 100
                    || (r.kind == ReplyCards.Pitch && (s.economyRulesVersion != 1 || r.week < s.weekRulesStartWeek
                        || r.fromId == s.playerId || r.promised || r.listenerId == r.fromId || r.listenerId == s.playerId
                        || (r.replyKey == HoHPitches.FeelOutKey ? r.toThem != 0
                            : ReplyCards.Find(ReplyCards.Pitch, r.replyKey) == null
                                || r.toThem != ReplyCards.Find(ReplyCards.Pitch, r.replyKey).ToThem))))
                || l.replies.Where(r => r.kind == ReplyCards.Pitch).GroupBy(r => r.week).Any(g => g.Count() > 48)
                || l.replies.Where(r => r.kind == ReplyCards.Pitch)
                    .GroupBy(r => new { r.week, r.fromId, inspected = r.replyKey == HoHPitches.FeelOutKey }).Any(g => g.Count() > 1))
                return Fail(out error, "Invalid reply record data.");
            if (l.calls == null || l.calls.Count > most || l.calls.Any(c => c == null || !Text(c.allianceId, 160) || !Id(c.callerId) || !Id(c.targetId)
                    || !Week(c.week) || c.followed == null || c.followed.Count > MaximumCast || c.followed.Any(id => !Id(id))
                    || c.defected == null || c.defected.Count > MaximumCast || c.defected.Any(id => !Id(id))))
                return Fail(out error, "Invalid bloc call record data.");
            return true;
        }
    }
}
