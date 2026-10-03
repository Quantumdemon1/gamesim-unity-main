using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The final Head of Household's choice when it is a houseguest's (ACTIONS-DEALS-ALLIANCES-PLAN
    /// C9): which of the other two finalists they take to the final two. One function, so the engine's
    /// final eviction and anything that measures it read the same choice.
    ///
    /// <para><b>What it was.</b> The weekly eviction vote's evaluator, with the two finalists as its
    /// nominees. Sampled over 1,500 seasons (FinalChoiceSeasonHarness, and the plan's build log), a
    /// houseguest who held the final Head of Household evicted the player in 94% of the final threes
    /// the player reached: nearly every one of the evaluator's terms leaned to the other finalist - the
    /// threat term's endgame weighting on the player's competition record, story bonds and grudges, the
    /// Head of Household's view - by 47 points on average, while a pact or a final two deal moved it by
    /// a few. And it worked against the Head of Household's own endgame: the jury would have given them
    /// a better chance to win beside the player in five finals of six.</para>
    ///
    /// <para><b>Under the commitment rules</b> the choice is the final three's own (<see cref="FinalChoiceTerms"/>):</para>
    /// <list type="bullet">
    /// <item>the evaluator's terms about the game still to be played - the threat a houseguest poses in
    /// the weeks to come and their strategic value for them - are left out (<see cref="FinalChoiceLeavesOut"/>):
    /// no competition and no nomination is left, only the jury's vote, which the jury term reads;</item>
    /// <item>the jury (<see cref="FinalJuryWeight"/>): the Head of Household's chance of winning beside each
    /// finalist, the other one cut and on the jury, read from the jury's own reader
    /// (<see cref="FinalJuryChance"/>) - they would rather sit beside somebody they can beat;</item>
    /// <item>a pact that holds from the Head of Household's side (<see cref="FinalPactTerm"/>, read through
    /// <see cref="Allegiance.Holds"/>): one they have turned on, or gone cold on, holds nothing here;</item>
    /// <item>the endgame's deals: a final two deal and a final two promise called in, as C1 and C7 made
    /// them (<see cref="FinalTwoTerms"/>), and a final three deal the two of them kept to the final three
    /// (<see cref="FinalThreeObligation"/>);</item>
    /// <item>and the rest of the evaluator as it is: the Head of Household's view of each, the history and
    /// the traits they share, the pacts and deals between them, a story's grudge or bond.</item>
    /// </list>
    /// <para>Each new term is bounded, and a season without the rules chooses exactly as it did.</para>
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// The final Head of Household's choice, weighed as the eviction vote weighs a ballot: the two
        /// finalists other than the Head of Household as its nominees, the one with the lower score
        /// evicted (<see cref="WebVoteEvaluation.selectedNomineeId"/>). Without memories or the
        /// player's persona, as the source's fast-forward final selection supplies none. Under the
        /// commitment rules a final two deal is an obligation in it (C1, <see cref="FinalTwoTerms"/>),
        /// and the choice is the final three's own (C9, <see cref="FinalChoiceTerms"/>). Pure: no roll,
        /// no write. A tie is broken by the evaluator's own keyed draw, not the season's.
        /// </summary>
        public static WebVoteEvaluation FinalChoice(EpisodeState s)
        {
            var finalContext = s.Clone();
            finalContext.nominees = s.Active.Where(c => c.id != s.hohId).Select(c => c.id).ToList();
            var finalOptions = WebEvictionVoting.FromNative(finalContext, s.hohId);
            // Match the source fast-forward final-selection caller, which supplies no memory/persona context.
            finalOptions.memories.Clear(); finalOptions.playerPersonaLabel = null;
            if (CommitmentRulesOn(s))
            {
                // Under the commitment rules (C1) a final two deal is a real obligation in the
                // choice, not the web's deal term alone (about 4.5 points after its weight).
                finalOptions.obligations.AddRange(FinalTwoTerms(s, s.hohId, finalContext.nominees));
                // And (C9) the choice is the final three's: the jury, the pacts that hold, a final three
                // kept, and none of the terms for a game still to play.
                finalOptions.obligations.AddRange(FinalChoiceTerms(s, s.hohId, finalContext.nominees));
                finalOptions.omittedFactors.AddRange(FinalChoiceLeavesOut);
            }
            return WebEvictionVoting.Evaluate(finalOptions);
        }

        // ---------------------------------------------------------------- C9: the final three's terms

        /// <summary>
        /// The evaluator's terms the final choice leaves out under the commitment rules (C9): the threat a
        /// finalist poses in the weeks to come - their competition record, at the evaluator's endgame
        /// weight of one and a half - and their strategic value for them. At the final three no week is
        /// left to come; the jury term reads what is left, the competition record included (the jury's
        /// respect for it, <see cref="WebJuryVoting.GameplayRespect"/>).
        /// </summary>
        public static readonly string[] FinalChoiceLeavesOut = { "threat", "strategicValue" };

        /// <summary>
        /// What the jury weighs in the final choice (C9): the term on each finalist is this times the
        /// Head of Household's chance of winning beside them, less a half (<see cref="FinalJuryChance"/>),
        /// so it runs from −25 to +25, and a Head of Household sure to win beside one finalist and sure to
        /// lose beside the other leans fifty points to the first - more than any one feeling the evaluator
        /// weighs (a story bond is 25, a grudge at most 25, a pact with its alliance term about 29), less
        /// than a pact and a final two deal kept together (about 59 at a plain word and a view of fifty).
        /// Every Head of Household can count a jury: no trait moves it.
        /// </summary>
        public const double FinalJuryWeight = 50;

        /// <summary>
        /// What a pact that holds from the Head of Household's side weighs for its partner in the final
        /// choice (C9), on top of the evaluator's own alliance term: a commitment kept to the end. By
        /// their word, as a final two deal's obligation is: Loyal ×1.5, Sneaky nothing. Once however
        /// many pacts the two share.
        /// </summary>
        public const double FinalPactTerm = 20;

        /// <summary>
        /// What a final three deal the two of them kept to the final three weighs in the final choice
        /// (C9): half a final two deal's obligation, scaled the same way - by the Head of Household's view
        /// of the finalist and their word. It asked them to get there together, not to sit there together.
        /// </summary>
        public const double FinalThreeObligation = FinalTwoObligation / 2;

        /// <summary>The factor codes C9's terms become: a pact, the jury, and a final three kept (an obligation, as a final two deal is).</summary>
        public const string PactFactor = "pact", JuryFactor = "jury";

        /// <summary>
        /// The final three's terms in a houseguest's final choice, under the commitment rules (C9): for
        /// each finalist, the jury (<see cref="FinalJuryWeight"/>), a pact with them that holds from the
        /// Head of Household's side (<see cref="FinalPactTerm"/>) and a final three deal the two kept
        /// (<see cref="FinalThreeObligation"/>). Empty without the rules and for a Head of Household who
        /// is the player, whose choice is their own. Pure: no roll.
        /// </summary>
        public static List<WebVoteObligation> FinalChoiceTerms(EpisodeState s, string hohId, IReadOnlyList<string> finalists)
        {
            var terms = new List<WebVoteObligation>();
            var hoh = s?.Find(hohId);
            if (hoh == null || hoh.isPlayer || finalists == null || !CommitmentRulesOn(s)) return terms;
            double word = hoh.traits.Contains("Sneaky") ? 0 : hoh.traits.Contains("Loyal") ? LoyalObligation : 1;
            foreach (string finalist in finalists.Distinct())
            {
                if (finalist == hohId || s.Find(finalist)?.status != ContestantStatus.Active) continue;

                double chance = FinalJuryChance(s, hohId, finalist);
                double jury = FinalJuryWeight * (chance - 0.5);
                if (jury != 0) terms.Add(Term(finalist, JuryFactor, jury, "jury:" + hohId + ":" + finalist));

                if (Allegiance.Holds(s, hohId, finalist) && word > 0)
                {
                    var pact = Term(finalist, PactFactor, FinalPactTerm * word);
                    pact.evidenceIds.AddRange(s.alliances.Where(a => a.active && a.members.Contains(hohId) && a.members.Contains(finalist)).Select(a => a.id));
                    terms.Add(pact);
                }

                double scale = Math.Max(0, Math.Min(1, s.Score(hohId, finalist) / ObligationFullView));
                foreach (var deal in s.deals.Where(d => d.type == DealKind.FinalThree && d.status == DealStatus.Fulfilled
                             && DealResolution.Partner(d, hohId) == finalist))
                {
                    double value = FinalThreeObligation * scale * word;
                    if (value == 0) continue;
                    var kept = terms.FirstOrDefault(t => t.nomineeId == finalist && t.code == "obligation");
                    if (kept == null) terms.Add(kept = Term(finalist, "obligation", 0));
                    kept.value += value;
                    kept.evidenceIds.Add(deal.id);
                }
            }
            return terms;
        }

        private static WebVoteObligation Term(string nomineeId, string code, double value, string evidence = null)
        {
            var term = new WebVoteObligation { nomineeId = nomineeId, code = code, value = value };
            if (evidence != null) term.evidenceIds.Add(evidence);
            return term;
        }

        /// <summary>
        /// The final Head of Household's chance of winning the jury's vote beside this finalist (C9): the
        /// jury as it will sit - everybody already on it, and the third finalist, cut - each juror weighing
        /// the two as the jury does (<see cref="WebJuryVoting.Score"/>), with a final impression of up to
        /// <see cref="WebJuryVoting.FinalImpression"/> either way on each, as the engine's jury draws them;
        /// the chance of a majority of those votes, and a tie as the reveal awards it, to the second of the
        /// two in cast order. A player on the jury is read by their own views. Pure: no roll, no write.
        /// 0 for anybody not in the house, or the Head of Household themselves.
        /// </summary>
        public static double FinalJuryChance(EpisodeState s, string hohId, string finalistId)
        {
            if (s == null || hohId == finalistId || s.Find(hohId)?.status != ContestantStatus.Active
                || s.Find(finalistId)?.status != ContestantStatus.Active) return 0;
            var jurors = s.contestants.Where(c => c.status == ContestantStatus.Jury || c.status == ContestantStatus.Evicted).Select(c => c.id).ToList();
            jurors.AddRange(s.Active.Where(c => c.id != hohId && c.id != finalistId).Select(c => c.id));
            // How many of them vote for the Head of Household: the chance of each count, juror by juror.
            var votes = new double[jurors.Count + 1];
            votes[0] = 1;
            foreach (string juror in jurors)
            {
                double p = BeatChance(WebJuryVoting.Score(s, juror, hohId) - WebJuryVoting.Score(s, juror, finalistId));
                for (int k = jurors.Count; k >= 0; k--) votes[k] = votes[k] * (1 - p) + (k > 0 ? votes[k - 1] * p : 0);
            }
            bool tieIsTheirs = s.contestants.FindIndex(c => c.id == hohId) > s.contestants.FindIndex(c => c.id == finalistId);
            double win = 0;
            for (int k = 0; k <= jurors.Count; k++)
            {
                int against = jurors.Count - k;
                if (k > against || (k == against && tieIsTheirs)) win += votes[k];
            }
            return Math.Max(0, Math.Min(1, win));
        }

        /// <summary>
        /// The chance a juror who scores the Head of Household <paramref name="lead"/> above the finalist
        /// votes for them, when each score carries its own final impression, uniform within
        /// <see cref="WebJuryVoting.FinalImpression"/> either way: the triangle the difference of the two draws makes.
        /// </summary>
        private static double BeatChance(double lead)
        {
            double spread = 2 * WebJuryVoting.FinalImpression;
            if (lead <= -spread) return 0;
            if (lead >= spread) return 1;
            return lead <= 0 ? (lead + spread) * (lead + spread) / (2 * spread * spread)
                : 1 - (spread - lead) * (spread - lead) / (2 * spread * spread);
        }

        /// <summary>
        /// The house down to its final three, under the commitment rules (C9): every final three deal
        /// between two of the three is kept, by both at once - the line to both, the record both ways - as
        /// any deal two kept together is settled. Run as the final three's Head of Household begins, so a
        /// final three that production's removal made keeps its deals too. No roll.
        /// </summary>
        private static void KeepTheFinalThree(EpisodeState s)
        {
            if (!CommitmentRulesOn(s) || s.Active.Count() != NpcDeals.FinalThreeSize) return;
            SettleDeals(s, DealResolution.Verdicts(s, DealResolution.ReachesTheFinalThree, null));
        }
    }
}
