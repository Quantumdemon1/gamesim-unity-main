using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// A final two deal with the player, from minds level 1 (<see cref="MindRules.Decisions"/>), under the commitment
    /// rules: the owner's ruling of 2026-11-21 - its obligation counts once, and it lapses only if the partner nominated
    /// the player.
    ///
    /// <para><b>Counted once.</b> In the final Head of Household's choice a final two deal was weighed twice: as the
    /// obligation (<see cref="EpisodeEngine.FinalTwoTerms"/>, 25 at a plain word) and again in the evaluator's deal term
    /// (a final two pair at 50 before its weight, about 4.5 after it), and a final two promise the player called in the
    /// same way (the obligation, and 10 in the deal term). From this level the deal term leaves out every final two deal
    /// and promise the obligation already counts (<see cref="CountOnce"/>). One the obligation does not count - a Sneaky
    /// Head of Household's, or a promise never called in - stays in the deal term, so it is still counted once.</para>
    ///
    /// <para><b>Lapses only if the partner nominated the player.</b> A houseguest who puts the player on the block - at
    /// the ceremony or as the veto's replacement - while the two of them hold a final two deal has ended it: it lapses
    /// (<see cref="DealStatus.Expired"/>), with a line to the player, and weighs nothing in the final choice. Nothing else
    /// ends it while both are in the house: a final two given as the price of a deal the other side then broke no longer
    /// lapses with it (<see cref="KeepsThePrice"/>). One of the two leaving the house still ends it, as it always did -
    /// there is nobody left to sit beside.</para>
    ///
    /// <para><b>The player who nominates their partner</b> breaks the deal (<see cref="DealStatus.Broken"/>, theirs), as
    /// nominating a safety pact or final three partner breaks it: the partner holds it against them, and the decision
    /// screen's dry run warns them first. A deal between two houseguests answers to no nomination, as before.</para>
    ///
    /// <para>Below the level every season plays exactly as it did: nothing here is read, and nothing draws from the
    /// season's stream that did not before. No saved field: the level is the saved <c>story.mindRules</c>.</para>
    /// </summary>
    public static class FinalTwoDeals
    {
        /// <summary>The minds level the ruling plays from: a lower bound, never enrolled here.</summary>
        public const int Level = MindRules.Decisions;

        /// <summary>Whether the season plays the ruling: the commitment rules this week, and minds level 1 or later.</summary>
        public static bool On(EpisodeState s) => EpisodeEngine.CommitmentRulesOn(s) && MindRules.At(s, Level);

        /// <summary>
        /// A nomination, against a final two deal, from the level: the partner who nominated the player lapses it; the
        /// player who nominated their partner breaks it. Null for anything else: a deal between two houseguests, a
        /// nomination that names neither, or the nominator no party to it. Pure.
        /// </summary>
        public static DealResolution.Verdict AtNomination(EpisodeState s, DealState deal, string nominatorId, IReadOnlyList<string> nominees)
        {
            if (s == null || deal == null || deal.type != DealKind.FinalTwo || nominees == null) return null;
            string partner = DealResolution.Partner(deal, nominatorId);
            if (partner == null) return null;
            if (nominatorId != s.playerId && partner == s.playerId && nominees.Contains(s.playerId))
                return new DealResolution.Verdict { deal = deal, status = DealStatus.Expired, actorId = nominatorId, lapses = true };
            if (nominatorId == s.playerId && nominees.Contains(partner))
                return new DealResolution.Verdict { deal = deal, status = DealStatus.Broken, actorId = nominatorId };
            return null;
        }

        /// <summary>
        /// The lapse (<see cref="DealResolution.Verdict.lapses"/>): the deal ends, and the player is told. No breach, no
        /// record, no memory and no roll: the partner's nomination is its own act, which the house already reads.
        /// </summary>
        internal static void Lapse(EpisodeState s, DealResolution.Verdict verdict)
        {
            var deal = verdict?.deal;
            if (deal == null || !DealStatus.Binds(deal.status)) return;
            deal.status = DealStatus.Expired;
            EpisodeEngine.Log(s, "deal-outcome", LapseLine(s.Find(verdict.actorId)?.name ?? "Your partner"), s.playerId, verdict.actorId);
        }

        /// <summary>The line a lapse is told in: "Maya nominated you, so your final two deal with them is off."</summary>
        public static string LapseLine(string partnerName) => partnerName + " nominated you, so your final two deal with them is off.";

        /// <summary>
        /// Whether a final two given as a price stands though the deal it bought was broken by the one owed it: from the
        /// level it does, since only the partner's nomination of the player ends a final two deal.
        /// </summary>
        public static bool KeepsThePrice(EpisodeState s, DealState price) =>
            price != null && price.type == DealKind.FinalTwo && On(s);

        /// <summary>
        /// The final choice's options, from the level: the evaluator's deal term leaves out every final two deal and
        /// promise the obligation counts (<see cref="EndgameCommitments.Read"/>'s final two evidence). Writes only the
        /// options. <paramref name="context"/> is the season the options were read from.
        /// </summary>
        internal static void CountOnce(EpisodeState context, string hohId, IReadOnlyList<string> finalists, WebVoteOptions options)
        {
            if (context == null || options?.state == null || !On(context)) return;
            var counted = new HashSet<string>(EndgameCommitments.Read(context, hohId, finalists)
                .Where(commitment => commitment.HasFinalTwo).SelectMany(commitment => commitment.EvidenceIds), StringComparer.Ordinal);
            if (counted.Count == 0) return;
            options.state.deals.RemoveAll(deal => deal.type == DealKind.FinalTwo && counted.Contains(deal.id));
            // The options' promises are the raw list in its order, one for one (WebEvictionVoting.FromNative).
            var raw = CommitmentReferences.RawPromises(context);
            if (raw.Count != options.state.promises.Count) return;
            for (int i = raw.Count - 1; i >= 0; i--)
                if (raw[i].kind == PromiseKind.FinalTwo && counted.Contains("promise:" + raw[i].id)) options.state.promises.RemoveAt(i);
        }
    }
}
