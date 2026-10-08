using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Schema 17's invariants, and schema 18's two lists: the season ledger. Every list is bounded by its cap, every id names a
    /// houseguest, every week is one the season has reached, and every vocabulary word is one the
    /// ledger knows, so a save cannot carry a claim from nobody, about nobody, made never.
    /// </summary>
    public static partial class EpisodeValidation
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
            // Schema 28: D3's war-room plans (WAVE-D-NPC-PACTS-PLAN §3.3), none while its start week is 0
            // and none before it. These are the storage bounds; D3's engine slice adds the call links below.
            if (l.plans == null || l.plans.Any(p => p == null))
                return Fail(out error, "Invalid pact plan data.");
            if (s.pactPlanRulesStartWeek == 0 && l.plans.Count != 0)
                return Fail(out error, "A season without the war rooms has none of their plans.");
            bool Ids(System.Collections.Generic.List<string> ids) => ids != null && ids.Count <= MaximumCast
                && ids.All(Id) && ids.Distinct(System.StringComparer.Ordinal).Count() == ids.Count;
            if (l.plans.Count > most || l.plans.Any(p => !Week(p.week) || p.week < s.pactPlanRulesStartWeek
                    || !Text(p.allianceId, 160) || !Id(p.throughId) || !PactPlanStance.IsKnown(p.stance)
                    || !Ids(p.present) || !Ids(p.cameRound) || !Ids(p.followed)
                    || p.cameRound.Any(id => !p.present.Contains(id)) || p.followed.Any(id => !p.present.Contains(id))
                    || p.says == null || p.says.Count > MaximumCast || p.says.Any(say => say == null || !Id(say.memberId) || !Id(say.targetId))
                    || p.says.Select(say => say.memberId).Distinct(System.StringComparer.Ordinal).Count() != p.says.Count
                    || !OptionalId(p.counterId) || !OptionalId(p.targetId) || !OptionalId(p.callerId)
                    // An open plan is this campaign's: Social is phase 0, so "at most Campaign" would admit the next week.
                    || (p.stance == PactPlanStance.Open && (s.phase != EpisodePhase.Campaign || p.week != s.week)))
                || l.plans.GroupBy(p => new { p.week, p.allianceId }).Any(g => g.Count() > 1))
                return Fail(out error, "Invalid pact plan data.");
            // D3's engine slice: what each stance holds, and the call links. An open plan names no target,
            // caller or counter and binds nobody yet; a void one names nobody either; every other settled one
            // names its target and its caller. The player says nothing at the table and is never bound.
            if (l.plans.Any(p => (p.stance == PactPlanStance.Open || p.stance == PactPlanStance.Void)
                    ? !string.IsNullOrEmpty(p.targetId) || !string.IsNullOrEmpty(p.callerId) || !string.IsNullOrEmpty(p.counterId)
                      || p.cameRound.Count != 0 || p.followed.Count != 0
                    : !Id(p.targetId) || !Id(p.callerId))
                || l.plans.Any(p => p.followed.Contains(s.playerId) || p.cameRound.Contains(s.playerId) || p.says.Any(say => say.memberId == s.playerId)))
                return Fail(out error, "Invalid pact plan data.");
            // A plan the player backs is that week's call in their name: exactly one call row of the pact that
            // week, its target and who is with it, nobody defected - at least one while the calls have never
            // reached their cap. Every call row in a week a pact planned belongs to its plan.
            foreach (var p in l.plans)
            {
                var calls = l.calls.Where(k => k.week == p.week && k.allianceId == p.allianceId).ToList();
                bool backed = !string.IsNullOrEmpty(p.callerId) && p.callerId == s.playerId;
                if (!backed ? calls.Count != 0
                        : calls.Count > 1 || (calls.Count == 0 && l.calls.Count < most)
                          || calls.Any(k => k.callerId != s.playerId || k.targetId != p.targetId || k.defected.Count != 0
                              || !k.followed.SequenceEqual(p.followed)))
                    return Fail(out error, "A pact plan the player backs is that week's call in their alliance, and only it.");
            }
            return true;
        }
    }
}
