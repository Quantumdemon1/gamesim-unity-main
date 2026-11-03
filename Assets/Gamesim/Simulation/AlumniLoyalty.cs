using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Alumni rules 2, <see cref="AlumniRules.Loyalty"/> (ALL-STAR-HISTORIES-PLAN §14.B): inherited loyalty that changes
    /// and decays through what the house does. A past game's receipt (<see cref="AlumniHistories.ReceiptType"/>) counts
    /// for a graded share of itself instead of all or nothing:
    /// <list type="bullet">
    /// <item><b>Erosion</b>: what the other did to the holder this season - the permanent wrongs on the holder's own edge,
    /// and every week they put the holder up at a public ceremony (<see cref="PutUpWeeks"/>), worth
    /// <see cref="NominationErosion"/> at the act and <see cref="NominationFadePerWeek"/> less each week after.</item>
    /// <item><b>Repair</b>: what they built together this season - the permanent positive entries on the holder's edge (a
    /// pact, a word given or kept, a positive story receipt) and a kept deal at its fading weight - capped at one
    /// <see cref="LoyaltySpan"/>, and set against the erosion at <see cref="RepairWeight"/>.</item>
    /// <item><b>A betrayal</b>, one permanent wrong of <see cref="BetrayalLine"/> or worse, ends the goodwill for good:
    /// no repair undoes it, so an old partner's betrayal never hurts less than a stranger's (§7.4).</item>
    /// <item><b>An old grievance</b> (a negative receipt) heals by play: repair beyond the erosion takes it away, a span of
    /// it entirely. Time alone heals nothing, and nothing fades for neglect.</item>
    /// </list>
    ///
    /// <para>It reads only the holder's own edge and the public record of the ceremonies (the ledger's power rows and the
    /// live block): never a ballot, a grudge, a private deal or another houseguest's score (§2's rule, §14.B.2). It is a
    /// pure reading: nothing is written, drawn or minted. An edge with no receipt reads exactly the ledger's own sum.</para>
    ///
    /// <para><b>The one write</b> (§14.B.4) is <see cref="SplitInPublic"/>: when one of a shared alliance's two houseguests
    /// puts the other up, the standing the past game added is written off, once, through
    /// <see cref="RelationshipLedger.Move"/>.</para>
    ///
    /// <para>Every member asks <see cref="AlumniRules.At"/> with <see cref="AlumniRules.Loyalty"/> first, so a season at
    /// alumni rules 0 or 1 reads and plays exactly as before.</para>
    /// </summary>
    public static class AlumniLoyalty
    {
        /// <summary>How much erosion takes a receipt's goodwill to nothing: twice a past alliance's +15. One pact (+30) heals an old grievance in full, and it is the most repair can bank.</summary>
        public const double LoyaltySpan = 30;

        /// <summary>A single permanent wrong this heavy is a betrayal: a High deal broken (-30), a final two broken (-45), a broken vote promise (-31). An information promise (-22) and Low or Medium deals (-15, -22.5) are lesser wrongs.</summary>
        public const double BetrayalLine = 30;

        /// <summary>What repair is worth against erosion: half, so the capped span absorbs at most 15 points of wrongs.</summary>
        public const double RepairWeight = 0.5;

        /// <summary>Being put up at a public ceremony, from the public record: half a nomination's grudge of 70.</summary>
        public const double NominationErosion = 35;

        /// <summary>How fast "you put me up" fades: half the grudge store's two a week, so it stays a season-long memory.</summary>
        public const double NominationFadePerWeek = 1;

        /// <summary>The ledger type a kept deal writes; it repairs at its own fading weight.</summary>
        private const string DealFulfilled = "deal_fulfilled";

        /// <summary>
        /// The holder's trust sum on one edge (<see cref="RelationshipLedger.WeightedImpact"/> at <see cref="AlumniRules.Loyalty"/>):
        /// each entry in stored order, a receipt at its graded share (<see cref="GoodwillShare"/> when positive,
        /// <see cref="GrievanceShare"/> when negative) and every other entry at its ordinary weight. An edge that holds no
        /// receipt returns the ledger's own sum, bit for bit.
        /// </summary>
        internal static double WeightedImpact(EpisodeState s, RelationshipState edge)
        {
            if (!edge.events.Any(AlumniHistories.IsReceipt))
                return edge.events.Sum(entry => entry.impactScore * RelationshipLedger.Weight(entry, s.week));
            var (goodwill, grievance) = Shares(s, edge.fromId, edge.toId, edge);
            return edge.events.Sum(entry => AlumniHistories.IsReceipt(entry)
                ? entry.impactScore * (entry.impactScore > 0 ? goodwill : grievance)
                : entry.impactScore * RelationshipLedger.Weight(entry, s.week));
        }

        /// <summary>
        /// The share of a positive past-game receipt the holder still counts (§14.B.1), from 0 to 1: nothing after a
        /// betrayal, otherwise one less the erosion left after repair, in spans. 1 below <see cref="AlumniRules.Loyalty"/>,
        /// where the share is not read (alumni rules 1's breach rule is the ledger's own).
        /// </summary>
        public static double GoodwillShare(EpisodeState s, string holderId, string aboutId) =>
            AlumniRules.At(s, AlumniRules.Loyalty) ? Shares(s, holderId, aboutId, Edge(s, holderId, aboutId)).goodwill : 1;

        /// <summary>
        /// The share of a negative past-game receipt the holder still counts (§14.B.1), from 0 to 1: one less the repair
        /// beyond the erosion, in spans. 1 below <see cref="AlumniRules.Loyalty"/>, where every grievance counts in full.
        /// </summary>
        public static double GrievanceShare(EpisodeState s, string holderId, string aboutId) =>
            AlumniRules.At(s, AlumniRules.Loyalty) ? Shares(s, holderId, aboutId, Edge(s, holderId, aboutId)).grievance : 1;

        /// <summary>
        /// The weeks in which <paramref name="byId"/>, as Head of Household, put <paramref name="ofId"/> up at a public
        /// ceremony, in order, each once: a nomination (a veto save included) or the replacement, read from the ledger's
        /// power rows (<see cref="FinalistRead"/>'s put-up rule) and never from the final eviction's row. Earlier weeks
        /// only, unless <paramref name="includeThisWeek"/>: then this week's row and the live block too. Empty below
        /// <see cref="AlumniRules.Loyalty"/>.
        /// </summary>
        public static List<int> PutUpWeeks(EpisodeState s, string byId, string ofId, bool includeThisWeek)
        {
            var weeks = new List<int>();
            if (!AlumniRules.At(s, AlumniRules.Loyalty) || byId == null || ofId == null || byId == ofId) return weeks;
            var rows = s.ledger?.power;
            if (rows != null)
                foreach (var row in rows)
                {
                    if (row == null || row.hohId != byId || row.nominees == null || FinalEviction(row)) continue;
                    if (row.week > s.week || (row.week == s.week && !includeThisWeek)) continue;
                    if (FinalistRead.PutUp(row, ofId) && !weeks.Contains(row.week)) weeks.Add(row.week);
                }
            // The live block: this week's nominations from the moment they are said, before the veto and the reveal
            // write them into this week's row. A week counts once.
            if (includeThisWeek && s.hohId == byId && s.nominees != null && s.nominees.Contains(ofId) && !weeks.Contains(s.week))
                weeks.Add(s.week);
            weeks.Sort();
            return weeks;
        }

        /// <summary>
        /// The first week either of two houseguests put the other up at a public ceremony (<see cref="PutUpWeeks"/>, both
        /// ways), or 0 for never: the week their old partnership split in public. 0 below <see cref="AlumniRules.Loyalty"/>.
        /// </summary>
        public static int PublicSplitWeek(EpisodeState s, string a, string b, bool includeThisWeek)
        {
            var weeks = PutUpWeeks(s, a, b, includeThisWeek).Concat(PutUpWeeks(s, b, a, includeThisWeek)).ToList();
            return weeks.Count == 0 ? 0 : weeks.Min();
        }

        /// <summary>
        /// A public split (§14.B.4), at the end of a nomination's effects (the first nominations and the replacement):
        /// when the Head of Household and the nominee are the two houseguests of a seated shared alliance
        /// (<see cref="AlumniPairFact.Standing"/> above 0, by <see cref="AlumniHistories.Seated"/>'s identity rule) and
        /// neither has put the other up in an earlier week, the standing the past game added is written off both ways
        /// through <see cref="RelationshipLedger.Move"/>: symmetric, no entry, no draw, no sequence. Once per pair a
        /// season, driven by the act. Never on the player's edge (D-H3), and nothing below <see cref="AlumniRules.Loyalty"/>.
        /// </summary>
        internal static void SplitInPublic(EpisodeState s, string hohId, string nomineeId)
        {
            if (!AlumniRules.At(s, AlumniRules.Loyalty) || hohId == null || nomineeId == null || hohId == nomineeId) return;
            var hoh = s.Find(hohId);
            var nominee = s.Find(nomineeId);
            if (hoh == null || nominee == null || hoh.isPlayer || nominee.isPlayer) return;
            foreach (var pair in AlumniHistories.SeatedPairs(s, AlumniHistories.EditionOf(s)))
            {
                bool theirs = (pair.FirstId == hohId && pair.SecondId == nomineeId) || (pair.FirstId == nomineeId && pair.SecondId == hohId);
                if (!theirs || pair.Fact.Standing <= 0) continue;
                if (PublicSplitWeek(s, hohId, nomineeId, includeThisWeek: false) != 0) return;
                RelationshipLedger.Move(s, hohId, nomineeId, -pair.Fact.Standing);
                return;
            }
        }

        /// <summary>
        /// Both shares for one holder's edge about another (§14.B.1). Erosion: the permanent wrongs on the edge (their
        /// size) and the public put-ups, each worth <see cref="NominationErosion"/> less a week's fade since. Repair: the
        /// permanent positive entries and a kept deal at its weight, capped at a span. Receipts are neither.
        /// </summary>
        private static (double goodwill, double grievance) Shares(EpisodeState s, string holderId, string aboutId, RelationshipState edge)
        {
            double wrongs = 0, repair = 0;
            bool betrayed = false;
            if (edge != null)
                foreach (var entry in edge.events)
                {
                    if (AlumniHistories.IsReceipt(entry)) continue;
                    double impact = entry.impactScore;
                    if (!entry.decayable && impact < 0)
                    {
                        wrongs -= impact;
                        if (impact <= -BetrayalLine) betrayed = true;
                    }
                    else if (entry.type == DealFulfilled) repair += impact * RelationshipLedger.Weight(entry, s.week);
                    else if (!entry.decayable && impact > 0) repair += impact;
                }
            double erosion = wrongs;
            foreach (int week in PutUpWeeks(s, aboutId, holderId, includeThisWeek: true))
                erosion += Math.Max(0, NominationErosion - NominationFadePerWeek * (s.week - week));
            repair = Math.Min(LoyaltySpan, repair);
            double goodwill = betrayed ? 0 : Clamp01(1 - Math.Max(0, erosion - RepairWeight * repair) / LoyaltySpan);
            double grievance = Clamp01(1 - Math.Max(0, repair - erosion) / LoyaltySpan);
            return (goodwill, grievance);
        }

        /// <summary>The last Head of Household's choice, as a power row of its own: no vote, no veto. It nominates nobody.</summary>
        private static bool FinalEviction(PowerRow row) => (row.tally == null || row.tally.Count == 0) && row.evicteeId != null && row.vetoHolderId == null;

        private static double Clamp01(double value) => Math.Max(0, Math.Min(1, value));

        private static RelationshipState Edge(EpisodeState s, string from, string to) =>
            s?.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
    }
}
