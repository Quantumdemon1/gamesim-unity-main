using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>A finalist's strongest applicable endgame commitment, with every supporting record retained.</summary>
    public sealed class EndgameCommitment
    {
        public readonly string FinalistId;
        public readonly double Strength;
        public readonly bool HasFinalTwo;
        public readonly IReadOnlyList<string> EvidenceIds;

        internal EndgameCommitment(string finalistId, double strength, bool hasFinalTwo, IEnumerable<string> evidence)
        {
            FinalistId = finalistId; Strength = strength; HasFinalTwo = hasFinalTwo;
            EvidenceIds = evidence.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList().AsReadOnly();
        }

        public WebVoteObligation ToObligation() => new WebVoteObligation
        { nomineeId = FinalistId, code = "obligation", value = Strength, evidenceIds = EvidenceIds.ToList() };
    }

    /// <summary>
    /// Formal status determines whether a final commitment counts; current liking is weighed separately by
    /// the vote evaluator. A final two is 25, a kept final three is 12.5, and a called-in final two promise
    /// uses its existing hold multiplier. Loyal and Sneaky keep their existing word multipliers. Only the
    /// strongest commitment to each finalist counts numerically; duplicate or overlapping records retain
    /// their evidence without increasing protection. Pacts and jury estimates remain separate. Pure.
    /// </summary>
    public static class EndgameCommitments
    {
        public static List<EndgameCommitment> Read(EpisodeState state, string hohId, IReadOnlyList<string> finalists)
        {
            var result = new List<EndgameCommitment>();
            var hoh = state?.Find(hohId);
            if (hoh == null || hoh.isPlayer || hoh.status != ContestantStatus.Active || finalists == null
                || !EpisodeEngine.CommitmentRulesOn(state)) return result;
            double word = hoh.traits.Contains("Sneaky") ? 0 : hoh.traits.Contains("Loyal") ? EpisodeEngine.LoyalObligation : 1;
            if (word == 0) return result;

            foreach (string finalist in finalists.Distinct(StringComparer.Ordinal))
            {
                if (finalist == hohId || state.Find(finalist)?.status != ContestantStatus.Active) continue;
                double strongest = 0;
                bool finalTwo = false;
                var evidence = new List<string>();
                foreach (var deal in state.deals.Where(deal => DealResolution.Partner(deal, hohId) == finalist))
                {
                    double value;
                    if (deal.type == DealKind.FinalTwo && deal.status == DealStatus.Active)
                    { value = EpisodeEngine.FinalTwoObligation * word; finalTwo = true; }
                    else if (deal.type == DealKind.FinalThree && deal.status == DealStatus.Fulfilled)
                        value = EpisodeEngine.FinalThreeObligation * word;
                    else continue;
                    strongest = Math.Max(strongest, value);
                    evidence.Add(deal.id);
                }
                foreach (var promise in state.promises.Where(promise => promise.kind == PromiseKind.FinalTwo
                    && promise.status == PromiseStatus.Active && promise.fromId == hohId && promise.toId == finalist))
                {
                    double hold = Negotiation.HeldTo(state, promise);
                    if (hold <= 0) continue;
                    finalTwo = true;
                    strongest = Math.Max(strongest, EpisodeEngine.FinalTwoObligation * word * hold);
                    evidence.Add("promise:" + promise.id);
                }
                if (strongest > 0) result.Add(new EndgameCommitment(finalist, strongest, finalTwo, evidence));
            }
            return result;
        }
    }
}
