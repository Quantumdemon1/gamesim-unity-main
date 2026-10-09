using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>What a directed atom of a decided row's source recipe is to that row.</summary>
    internal enum UnifiedVoteAtomRole
    {
        /// <summary>The write that carries one of the row's own directed consequences.</summary>
        Primary,
        /// <summary>A kept named deal's second ledger direction: the actor's record of the partner, at the same weight.</summary>
        Mirror,
        /// <summary>A broken named deal's breaker-side record at zero: what they did, weighing nothing. Never an incident.</summary>
        Context,
    }

    /// <summary>
    /// One directed write a decided Vote row's source recipe makes: a score (the holder's view of the other moves)
    /// or a ledger entry (the holder's record of the other). The Rule2 plan selects it, or not; the row's own
    /// source recipe writes only what is selected, at its own occurrence.
    /// </summary>
    internal sealed class UnifiedVoteAtom
    {
        internal readonly string HolderId, AboutId;
        internal readonly double Delta;
        internal readonly UnifiedVoteAtomRole Role;
        internal bool Selected;

        internal UnifiedVoteAtom(string holderId, string aboutId, double delta, UnifiedVoteAtomRole role)
        { HolderId = holderId; AboutId = aboutId; Delta = delta; Role = role; }
    }

    /// <summary>
    /// One canonical Vote row a completed regular reveal decides: the detached row as it stood before the
    /// decision, the source's own verdict, its native source consequence (for ranking only) and the directed
    /// atoms its source recipe writes. Not a knowledge grant and not a stored value: a command-local plan.
    /// </summary>
    internal sealed class UnifiedVoteVerdict
    {
        internal readonly UnifiedCommitmentState Row;
        internal readonly string Status, ActorId;
        internal readonly double Nominal;
        internal readonly IReadOnlyList<UnifiedVoteAtom> Scores, Ledger;

        internal UnifiedVoteVerdict(UnifiedCommitmentState row, string status, string actorId, double nominal,
            IReadOnlyList<UnifiedVoteAtom> scores, IReadOnlyList<UnifiedVoteAtom> ledger)
        { Row = row.Clone(); Status = status; ActorId = actorId; Nominal = nominal; Scores = scores; Ledger = ledger; }

        internal bool Promise => Row.sourcePolicy == UnifiedCommitments.PromisePolicy;
        internal bool Kept => Status == DealStatus.Fulfilled;
        /// <summary>A deal both parties decided at once: a voting bloc, or a targeted deal both of them broke.</summary>
        internal bool Collective => !Promise && ActorId == null;
        /// <summary>Whether any of this row's directed consequences won its group: only then do its memory, line, Story and witness lanes run.</summary>
        internal bool Owns => Scores.Any(atom => atom.Selected);

        /// <summary>The row's source-shaped deal, its status the verdict's, as the source settles it.</summary>
        internal DealState Deal()
        {
            var deal = UnifiedVoteReferences.ProjectDeal(Row);
            deal.status = Status;
            return deal;
        }
    }

    /// <summary>
    /// A reveal's command-local settlement plan: every canonical Vote row this reveal decides, in the order the
    /// source meets them, with the Rule2 selection made. Built once, before any effect, from the validated
    /// state; executed by the reveal in its own lanes; then the stamped rows and the frame are published together.
    /// </summary>
    internal sealed class UnifiedVoteRevealPlan
    {
        internal readonly int Week;
        internal readonly IReadOnlyList<UnifiedVoteVerdict> Verdicts;
        internal readonly HashSet<string> Stamped = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>
        /// The betrayal identity of every row this reveal decides - kept or broken, stamped already or later in it - which
        /// the Story threat its recipes scale a grudge by leaves out of the breaker's reputation (vote family V5c, the approved
        /// policy's current-reveal exclusion). A breach of an earlier reveal is no part of it, and still counts.
        /// </summary>
        internal readonly IReadOnlyCollection<string> VoteEffects;

        internal UnifiedVoteRevealPlan(int week, IReadOnlyList<UnifiedVoteVerdict> verdicts)
        {
            Week = week; Verdicts = verdicts;
            VoteEffects = new HashSet<string>(verdicts.Select(verdict => UnifiedVoteHistory.Key(verdict.Row, week)), StringComparer.Ordinal);
        }

        /// <summary>A voter's decided promises, in the canonical list's order - the order the source settled them in.</summary>
        internal IEnumerable<UnifiedVoteVerdict> PromisesOf(string voterId) =>
            Verdicts.Where(verdict => verdict.Promise && verdict.Row.makerId == voterId);

        /// <summary>The decided deals, in the canonical list's order; the reveal merges them with the raw verdicts by occurrence.</summary>
        internal IEnumerable<UnifiedVoteVerdict> Deals => Verdicts.Where(verdict => !verdict.Promise);

        /// <summary>Every directed consequence group this reveal decided, with the owner Rule2 selected (<see cref="UnifiedVoteSettlement.Groups"/>).</summary>
        internal IReadOnlyList<UnifiedVoteIncident> Groups() => UnifiedVoteSettlement.Groups(Week, Verdicts);
    }

    /// <summary>
    /// The Vote family's settlement at a completed regular reveal, under the prospective mode 2 (vote family V4).
    /// Pure: it reads the state and returns a plan; it writes, rolls, mints and logs nothing.
    ///
    /// <para><b>Which rows the reveal decides.</b> Every Active canonical Vote row that has bound, met at this
    /// week by the same eligibility the family validation proves a terminal row by (its first reveal floor, its
    /// term, its ending cutoff, and the levers' week for a targeted deal), judged against the actual private box
    /// and block by the same leaves (<see cref="UnifiedVoteTogether"/>, <see cref="UnifiedVoteObligations"/>). A
    /// row the box does not decide - its maker cast no ballot, its target is off the block - stays Active.</para>
    ///
    /// <para><b>Rule2, the approved overlap policy</b> (VOTE_EFFECT_POLICY_APPROVAL.md, alternative A). Every decided
    /// row keeps its own terminal stamp. Its consequences are directed: a promise is the beneficiary's view of the
    /// maker; a named deal (a sole keeper or breaker) is the partner's view of the actor; a collective deal (a bloc,
    /// or a targeted deal both broke) is both views. Directed consequences group by polarity, actor and partner at
    /// this reveal, and one wins each group: kept by the largest native source consequence, broken by the most
    /// severe, then by ordinal row id. Only a row with a winning consequence is an owner. Its ledger entries are
    /// candidates: a broken entry follows its own consequence (a breach's zero context follows its row); a kept
    /// directed edge is won by the largest source delta among the owners' entries, primary or mirror alike, then
    /// by ordinal id. An owner runs its memory, line, Story and witness lanes once; a row that owns nothing is a
    /// terminal receipt only. Execution keeps the source's occurrence order: ids rank winners, never the run.</para>
    ///
    /// <para>This is a deliberate new overlap rule, not byte-identical execution of overlapping legacy effects.
    /// A reveal with no overlap writes exactly what mode 1 writes. Modes 0 and 1 never reach this class.</para>
    ///
    /// <para>The policy's typed command-local exclusions of this reveal's Vote incidents (later selected winners
    /// included) from the Story threat assessment the recipes' StoryWordBroken reads are the plan's
    /// <see cref="UnifiedVoteRevealPlan.VoteEffects"/> (vote family V5c): ThreatAssessment.ReputationThreat counts an
    /// earlier reveal's canonical breach as mode 1 counts it, and leaves out this one's.</para>
    /// </summary>
    internal static class UnifiedVoteSettlement
    {
        /// <summary>
        /// Test-only, and null in play: sees each plan as <see cref="Plan"/> returns it, the Rule2 selection made (vote family
        /// V5e's walk observer, set through the tests' facade on the thread that walks). It reads; it never changes a plan.
        /// </summary>
        [ThreadStatic] internal static Action<object> Observed;

        /// <summary>
        /// The plan for the reveal the state stands at: the box complete, the eviction decided, nothing settled.
        /// Throws if a decided row cannot be judged by its own leaf (malformed evidence), which the caller turns
        /// into the whole command's refusal.
        /// </summary>
        internal static UnifiedVoteRevealPlan Plan(EpisodeState s)
        {
            var ballots = s.votes.Select(vote => new UnifiedVoteBallotState { voterId = vote.voterId, targetId = vote.targetId }).ToList();
            var verdicts = new List<UnifiedVoteVerdict>();
            foreach (var row in s.unifiedCommitments)
            {
                if (row.kind != UnifiedVoteTogether.Vote || row.status != DealStatus.Active || row.voteBindingWeek == 0) continue;
                if (!UnifiedVoteFamilyValidation.Eligible(s, row, s.week, UnifiedVoteFamilyValidation.EndingCutoff(s, row))) continue;
                string status, actor, error;
                if (row.sourcePolicy == UnifiedCommitments.DealPolicy && row.subtype == DealKind.VoteTogether)
                {
                    if (!UnifiedVoteTogether.TryVerdict(row, ballots, out status, out error))
                        throw new ArgumentException(error, nameof(s));
                    actor = null;
                }
                else
                {
                    if (!UnifiedVoteObligations.TryVerdict(row, ballots, s.nominees, out var verdict, out error))
                        throw new ArgumentException(error, nameof(s));
                    status = verdict?.Status; actor = verdict?.ActorId;
                }
                if (status == null) continue;
                verdicts.Add(Verdict(s, row, status, actor));
            }
            Select(verdicts);
            var plan = new UnifiedVoteRevealPlan(s.week, verdicts.AsReadOnly());
            Observed?.Invoke(plan);
            return plan;
        }

        /// <summary>
        /// Each directed consequence group of one reveal's verdicts - polarity, the one the view is of and the one whose view
        /// it is - as the Rule2 selection left it (<see cref="Select"/> run first): the reveal's week, the owner (the row whose
        /// consequence won), every row in the group as evidence, and the group's first deal row by the same ranking. Ordered by
        /// polarity (breaches first), actor and holder.
        /// </summary>
        internal static IReadOnlyList<UnifiedVoteIncident> Groups(int week, IReadOnlyList<UnifiedVoteVerdict> verdicts) =>
            Array.AsReadOnly(verdicts.SelectMany(verdict => verdict.Scores.Select(atom => (verdict, atom)))
                .GroupBy(item => (item.verdict.Kept, item.atom.AboutId, item.atom.HolderId))
                .OrderBy(group => group.Key.Kept).ThenBy(group => group.Key.AboutId, StringComparer.Ordinal)
                .ThenBy(group => group.Key.HolderId, StringComparer.Ordinal)
                .Select(group =>
                {
                    var owner = group.Single(item => item.atom.Selected).verdict;
                    var ranked = (group.Key.Kept ? group.OrderByDescending(item => item.verdict.Nominal) : group.OrderBy(item => item.verdict.Nominal))
                        .ThenBy(item => item.verdict.Row.id, StringComparer.Ordinal).ToList();
                    var deal = ranked.FirstOrDefault(item => !item.verdict.Promise).verdict;
                    return new UnifiedVoteIncident(week, group.Key.AboutId, group.Key.HolderId, owner.Row.id, deal?.Row.id, owner.Promise,
                        owner.Nominal, group.Select(item => item.verdict.Row.id).Distinct(), group.Key.Kept);
                }).ToArray());

        /// <summary>
        /// The verdicts an archived reveal decided, rebuilt after it from the rows it first decided (vote family V5e): each row
        /// as the plan met it, its status and actor the frame's own (<see cref="UnifiedVoteHistory.Decisions"/>), its native
        /// consequence from the row, its link and the static rule weeks; then the Rule2 selection, as the plan made it.
        /// </summary>
        internal static IReadOnlyList<UnifiedVoteVerdict> Rebuild(EpisodeState s, IEnumerable<UnifiedVoteDecision> decided)
        {
            var verdicts = decided.Select(decision =>
            {
                var row = decision.Record.Clone();
                row.status = DealStatus.Active; row.settledWeek = 0; row.brokenById = null; row.settlementEffectKey = null;
                return Verdict(s, row, decision.Status, decision.ActorId);
            }).ToList();
            Select(verdicts);
            return verdicts.AsReadOnly();
        }

        /// <summary>
        /// Where a deal stands in the source's own deal order: the raw list and the canonical rows were one list
        /// in mode 1, appended as they were struck, each id carrying the sequence it was minted at. A bought deal
        /// and its price share a sequence and were appended bought first.
        /// </summary>
        internal static (long sequence, int price, int canonical, int index) Occurrence(string id, bool canonical, int index)
        {
            long sequence = long.MaxValue;
            int dash = id?.LastIndexOf('-') ?? -1;
            if (dash >= 0 && long.TryParse(id.Substring(dash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out long parsed))
                sequence = parsed;
            bool price = id != null && id.StartsWith(Negotiation.PricePrefix, StringComparison.Ordinal);
            return (sequence, price ? 1 : 0, canonical ? 1 : 0, index);
        }

        private static UnifiedVoteVerdict Verdict(EpisodeState s, UnifiedCommitmentState row, string status, string actor)
        {
            bool kept = status == DealStatus.Fulfilled;
            if (row.sourcePolicy == UnifiedCommitments.PromisePolicy)
            {
                // The beneficiary's view of the maker, and the beneficiary's permanent one-way record of it.
                double impact = WebRules.PromiseImpact("vote", kept ? "fulfilled" : "broken");
                return new UnifiedVoteVerdict(row, status, actor, impact,
                    new[] { new UnifiedVoteAtom(row.beneficiaryId, row.makerId, impact, UnifiedVoteAtomRole.Primary) },
                    new[] { new UnifiedVoteAtom(row.beneficiaryId, row.makerId, impact, UnifiedVoteAtomRole.Primary) });
            }
            var deal = UnifiedVoteReferences.ProjectDeal(row);
            deal.status = status;
            double delta = DealResolution.Impact(s, deal, status);
            if (actor == null)
                // Both views, and both directions of the record, in the source's own order.
                return new UnifiedVoteVerdict(row, status, null, delta,
                    new[] { new UnifiedVoteAtom(deal.proposerId, deal.recipientId, delta, UnifiedVoteAtomRole.Primary),
                        new UnifiedVoteAtom(deal.recipientId, deal.proposerId, delta, UnifiedVoteAtomRole.Primary) },
                    new[] { new UnifiedVoteAtom(deal.proposerId, deal.recipientId, delta, UnifiedVoteAtomRole.Primary),
                        new UnifiedVoteAtom(deal.recipientId, deal.proposerId, delta, UnifiedVoteAtomRole.Primary) });
            string partner = DealResolution.Partner(deal, actor);
            return new UnifiedVoteVerdict(row, status, actor, delta,
                new[] { new UnifiedVoteAtom(partner, actor, delta, UnifiedVoteAtomRole.Primary) },
                new[] { new UnifiedVoteAtom(partner, actor, delta, UnifiedVoteAtomRole.Primary),
                    kept ? new UnifiedVoteAtom(actor, partner, delta, UnifiedVoteAtomRole.Mirror)
                         : new UnifiedVoteAtom(actor, partner, 0, UnifiedVoteAtomRole.Context) });
        }

        /// <summary>
        /// The Rule2 selection, in place on the verdicts' atoms - a reveal's plan, or a frame's verdicts rebuilt after it
        /// (<see cref="Rebuild"/>). See the class summary.
        /// </summary>
        internal static void Select(IReadOnlyList<UnifiedVoteVerdict> verdicts)
        {
            // Directed consequences: one winner per polarity, actor (the one the view is of) and partner (whose view).
            foreach (var group in verdicts.SelectMany(verdict => verdict.Scores.Select(atom => (verdict, atom)))
                         .GroupBy(item => (item.verdict.Kept, item.atom.AboutId, item.atom.HolderId)))
            {
                var ranked = group.Key.Kept
                    ? group.OrderByDescending(item => item.verdict.Nominal)
                    : group.OrderBy(item => item.verdict.Nominal);
                ranked.ThenBy(item => item.verdict.Row.id, StringComparer.Ordinal).First().atom.Selected = true;
            }
            // A breach's record follows its own consequence; its zero context follows its row.
            foreach (var verdict in verdicts.Where(verdict => !verdict.Kept && verdict.Owns))
                foreach (var atom in verdict.Ledger)
                    atom.Selected = atom.Role == UnifiedVoteAtomRole.Context
                        || verdict.Scores.Any(score => score.Selected && score.HolderId == atom.HolderId && score.AboutId == atom.AboutId);
            // A kept edge of the record: the owners' entries compete, primary and mirror alike, by source delta then id.
            foreach (var edge in verdicts.Where(verdict => verdict.Kept && verdict.Owns)
                         .SelectMany(verdict => verdict.Ledger.Select(atom => (verdict, atom)))
                         .GroupBy(item => (item.atom.HolderId, item.atom.AboutId)))
                edge.OrderByDescending(item => item.atom.Delta).ThenBy(item => item.verdict.Row.id, StringComparer.Ordinal)
                    .First().atom.Selected = true;
        }
    }
}
