using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// NPC agency (NPC-AGENCY-PLAN.md): houseguests with temperaments that take to or grate on each
    /// other (<see cref="TraitAffinity"/>), agendas that say what each is trying to do this week
    /// (<see cref="NpcAgendas"/>), and decisions that act on both: a Head of Household who weighs
    /// threat and consults their pact, nominees who campaign to the voters worth the visit, and a
    /// houseguest looking for a partner who asks the player. Keyed to
    /// <see cref="EpisodeState.agencyRulesStartWeek"/>; before it the house is exactly what it was.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>Points of standing per point of compatibility at the first impression (§2).</summary>
        public const double FirstImpressionPerPoint = 3;

        /// <summary>What a talk gains or loses per point of the speaker's compatibility with the listener, and its bound (§3.2).</summary>
        public const double TalkPerPoint = 0.5, TalkAffinityBound = 2;

        /// <summary>Desire per point of compatibility (§3.3).</summary>
        public const double DesirePerPoint = 2;

        /// <summary>A conversation's bias: mutual compatibility over this, rounded and clamped to one either way (§3.1).</summary>
        public const double ConversationBiasDivisor = 6;

        /// <summary>How much of a houseguest's threat the Head of Household weighs against them, and how much of their pact-mates' reading (§5.1).</summary>
        public const double ThreatWeight = 0.4, AlliesThreatWeight = 0.2;

        /// <summary>A voter whose margin between the nominees is inside this is worth a nominee's visit (§5.2).</summary>
        public const double PersuadableMargin = 20;

        /// <summary>How many voters a nominee visits under agency.</summary>
        public const int CampaignVisits = 2;

        /// <summary>What a pact-mate's word against a threat costs the threat with the listener (§4).</summary>
        public const double HuntImpact = -6;

        /// <summary>
        /// Switches the agency rules on from a week and, on a season that has not begun, seeds the
        /// first impressions (§2). A boundary already set is never re-seeded, and a season under way
        /// (the importer's, a test's) keeps every standing it has.
        /// </summary>
        public static void EnableAgency(EpisodeState s, int fromWeek = 1)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            bool unstarted = s.agencyRulesStartWeek == 0 && s.week == 1 && s.revision == 0 && s.acceptedCommandIds.Count == 0;
            s.agencyRulesStartWeek = Math.Max(1, Math.Min(fromWeek, s.week + 1));
            if (unstarted && s.agencyRulesStartWeek == 1) SeedFirstImpressions(s);
        }

        /// <summary>Whether the house plays under the agency rules this week.</summary>
        public static bool AgencyOn(EpisodeState s) => s != null && s.agencyRulesStartWeek >= 1 && s.week >= s.agencyRulesStartWeek;

        /// <summary>
        /// First impressions (§2): every houseguest's view of every other moves by temperament, the
        /// player's own view of everybody excepted, because what you think of them is yours to
        /// decide. Scores only, no ledger row, so trust starts neutral as it always did.
        /// </summary>
        public static void SeedFirstImpressions(EpisodeState s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            foreach (var observer in s.contestants.Where(c => !c.isPlayer))
                foreach (var other in s.contestants.Where(c => c.id != observer.id))
                {
                    int compatibility = TraitAffinity.Compatibility(observer, other);
                    if (compatibility != 0) WriteScore(s, observer.id, other.id, compatibility * FirstImpressionPerPoint);
                }
        }

        /// <summary>What a talk is worth from this speaker to this listener: the ordinary talk and, under agency, half the speaker's compatibility with the listener, bounded (§3.2).</summary>
        public static double TalkWarmth(EpisodeState s, string fromId, string toId)
        {
            if (!AgencyOn(s)) return NpcSocialActions.TalkImpact;
            double affinity = WebRules.JsRound(TraitAffinity.Compatibility(s.Find(fromId), s.Find(toId)) * TalkPerPoint);
            return NpcSocialActions.TalkImpact + Math.Max(-TalkAffinityBound, Math.Min(TalkAffinityBound, affinity));
        }

        /// <summary>The bias a completed conversation carries under agency: mutual compatibility over six, rounded and clamped to one either way (§3.1).</summary>
        public static int ConversationBias(EpisodeState s, string firstId, string secondId)
        {
            if (!AgencyOn(s)) return 0;
            var first = s.Find(firstId); var second = s.Find(secondId);
            int mutual = TraitAffinity.Compatibility(first, second) + TraitAffinity.Compatibility(second, first);
            return Math.Max(-1, Math.Min(1, (int)WebRules.JsRound(mutual / ConversationBiasDivisor)));
        }

        /// <summary>
        /// How much a houseguest's threat counts against them when this Head of Household ranks the
        /// house (§5.1): their own reading, and a fifth of what their pact-mates read. Nought without
        /// agency, so the ranking is exactly what it was.
        /// </summary>
        public static double ThreatTerm(EpisodeState s, string hohId, string id)
        {
            if (!AgencyOn(s) || hohId == null || id == null || hohId == id) return 0;
            double term = ThreatWeight * ThreatAssessment.Total(s, hohId, id);
            var allies = NpcAgendas.PactMates(s, hohId).Where(m => m != id).ToList();
            if (allies.Count > 0) term += AlliesThreatWeight * allies.Average(m => ThreatAssessment.Total(s, m, id));
            return term;
        }

        /// <summary>
        /// The voters worth a nominee's visit (§5.2): not the other nominee's allies, not those sure
        /// to keep them or sure to evict them (a margin of twenty either way), the closest to torn
        /// first, ties in cast order. Empty without agency, so the campaign runs as it did.
        /// </summary>
        public static List<ContestantState> PersuadableVoters(EpisodeState s, string nomineeId)
        {
            if (!AgencyOn(s) || !s.nominees.Contains(nomineeId)) return new List<ContestantState>();
            string other = s.nominees.FirstOrDefault(id => id != nomineeId);
            return Voters(s)
                .Where(v => other == null || !s.Allied(v.id, other))
                .Select(v => (voter: v, margin: s.Score(v.id, nomineeId) - (other == null ? 0 : s.Score(v.id, other))))
                .Where(x => Math.Abs(x.margin) < PersuadableMargin)
                .OrderBy(x => Math.Abs(x.margin))
                .ThenBy(x => x.voter.id, StringComparer.Ordinal)
                .Select(x => x.voter)
                .ToList();
        }

        /// <summary>
        /// Courting the Head of Household (§4): as the nominations open, every houseguest whose
        /// agenda is to court them spends a word with them, which their ranking of the house feels.
        /// The player as Head of Household hears who came. Nothing without agency.
        /// </summary>
        private static void CourtTheHoH(EpisodeState s)
        {
            if (!AgencyOn(s) || string.IsNullOrEmpty(s.hohId)) return;
            var hoh = s.Find(s.hohId);
            if (hoh == null || hoh.status != ContestantStatus.Active) return;
            foreach (var npc in s.contestants.Where(c => c.status == ContestantStatus.Active && !c.isPlayer && c.id != s.hohId).ToList())
            {
                var agenda = NpcAgendas.Of(s, npc.id);
                if (agenda == null || agenda.kind != Agendas.Court) continue;
                NpcSocialActions.Court(s, npc, hoh);
                // Under the all-week rules (D2) the court is one of the week's acts, recorded with no draw;
                // it fills the houseguest's pursuit for the week.
                if (AllWeekOn(s)) RecordAct(s, NpcActKinds.Court, s.week + "-" + Windows.AfterHoH + "-court-" + npc.id, npc.id, hoh.id, null);
            }
        }
    }
}
