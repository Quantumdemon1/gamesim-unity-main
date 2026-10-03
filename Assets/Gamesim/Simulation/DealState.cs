using System;
using System.Collections.Generic;

namespace Gamesim.Simulation
{
    /// <summary>
    /// What two houseguests have agreed to do for each other.
    ///
    /// <para>Ported from the reference's <c>src/models/deal.ts</c>. The type and status are strings
    /// rather than enums, and deliberately: <see cref="WebEvictionVoting"/> has read them as strings
    /// since it was written — <c>"vote_save"</c>, <c>"safety_agreement"</c>, <c>"final_two"</c> — and
    /// <c>WebSaveImporter</c> reads them out of web saves in the same spelling. Making them an enum
    /// here would mean translating at both boundaries for no gain, and would silently drop a deal
    /// type the reference adds later rather than carrying it through.</para>
    ///
    /// <para>The <see cref="DealKind"/> and <see cref="DealStatus"/> classes below are the vocabulary,
    /// not a type — they exist so the rest of the project can spell these consistently.</para>
    /// </summary>
    [Serializable]
    public sealed class DealState
    {
        public string id;
        public string type = DealKind.Partnership;
        public string proposerId, recipientId;

        /// <summary>
        /// Who the deal is <i>about</i>, where that is not one of the two parties: the houseguest a
        /// <c>vote_evict</c> aims at, or a <c>target_agreement</c> names. Null otherwise.
        /// </summary>
        public string targetId;

        public string status = DealStatus.Proposed;
        public int week;

        /// <summary>
        /// The week this stops binding, or zero for open-ended.
        ///
        /// <para>Zero rather than a large number, matching <see cref="PromiseState.expiresWeek"/>,
        /// so a final two and a one-week voting block are told apart by shape rather than by
        /// comparing against a sentinel.</para>
        /// </summary>
        public int expiresWeek;

        /// <summary>
        /// How much of a houseguest's word a deal stakes, from the reference's
        /// <c>defaultTrustImpact</c>. Read when a deal is kept or broken, so that walking away from a
        /// veto commitment costs more than walking away from an information swap.
        /// </summary>
        public string trustImpact = DealTrust.Medium;

        /// <summary>
        /// Schema 22 (ACTIONS-DEALS-ALLIANCES-PLAN C0): who broke a broken deal, as the verdict that
        /// settled it named them - null for a voting bloc, which both parties settle at once - and the
        /// week a verdict kept or broke it. Written under the commitment rules
        /// (<see cref="EpisodeEngine.CommitmentRulesOn"/>); null and 0 on a deal settled before them,
        /// which <see cref="Breaches.DealBreaker"/> reads by <see cref="FinalistRead.DealBreaker"/>'s rule.
        /// </summary>
        public string brokenById;
        public int settledWeek;

        /// <summary>
        /// Schema 22 (ACTIONS-DEALS-ALLIANCES-PLAN C7): the deal this one is struck together with, by id -
        /// a deal and the price paid for it name each other (<see cref="Negotiation"/>). A nominee's veto
        /// ask and the vote save or final two they give for it; the veto the player holds and the price
        /// they named for it; a deal the player asked for and the price a houseguest's counter-offer put
        /// on it. Exactly one of the two is the price (<see cref="Negotiation.PricePrefix"/>), owed while
        /// what it bought stands and void once the one it was owed to breaks what it bought. Written only
        /// under the commitment rules (<see cref="EpisodeEngine.CommitmentRulesOn"/>); null on every other
        /// deal, and on every deal of a season saved before it.
        /// </summary>
        public string linkedDealId;

        public DealState Clone() => (DealState)MemberwiseClone();
    }

    /// <summary>
    /// The deal types: the reference's ten, spelled as the reference and the eviction vote spell them,
    /// and one of this port's own, the final three deal (<see cref="FinalThree"/>).
    /// </summary>
    public static class DealKind
    {
        public const string TargetAgreement = "target_agreement";
        public const string SafetyAgreement = "safety_agreement";
        public const string VoteTogether = "vote_together";
        public const string VoteSave = "vote_save";
        public const string VoteEvict = "vote_evict";
        public const string VetoUse = "veto_use";
        public const string InformationSharing = "information_sharing";
        public const string FinalTwo = "final_two";
        public const string Partnership = "partnership";
        public const string AllianceInvite = "alliance_invite";

        /// <summary>
        /// Native, not the reference's (ACTIONS-DEALS-ALLIANCES-PLAN C9): two houseguests take each other
        /// to the final three. It binds what a safety pact binds - neither puts the other up, at the
        /// nominations or as the veto's replacement, which breaks it - from the week it is struck until
        /// the house is down to three, and it is weighed where a safety pact is: in a Head of
        /// Household's reluctance to nominate the partner and in a ballot on them. It is kept when the
        /// two of them reach the final three together, and ends, blaming nobody, with whichever of them
        /// leaves the house first (X4). A kept one is an obligation in the final Head of Household's
        /// choice at half a final two's (<see cref="EpisodeEngine.FinalChoiceTerms"/>). Put only from
        /// the final six to the final four, and only under the commitment rules: a season without them
        /// never holds one, and validation refuses one there.
        /// </summary>
        public const string FinalThree = "final_three";

        public static readonly string[] All =
        {
            TargetAgreement, SafetyAgreement, VoteTogether, VoteSave, VoteEvict,
            VetoUse, InformationSharing, FinalTwo, Partnership, AllianceInvite,
            FinalThree,
        };

        public static bool IsKnown(string type) => type != null && Array.IndexOf(All, type) >= 0;

        /// <summary>Whether this kind exists only under the commitment rules (<see cref="EpisodeEngine.CommitmentRulesOn"/>): the final three deal.</summary>
        public static bool CommitmentRulesOnly(string type) => type == FinalThree;

        /// <summary>Whether this kind of deal is about a third houseguest rather than the pair.</summary>
        public static bool NamesATarget(string type) =>
            type == TargetAgreement || type == VoteEvict || type == VoteSave;

        /// <summary>
        /// What the reference's <c>DEAL_TYPE_INFO</c> gives each type as its default trust weight.
        /// </summary>
        public static string DefaultTrust(string type)
        {
            switch (type)
            {
                case VetoUse:
                case FinalTwo: return DealTrust.Critical;
                case TargetAgreement:
                case SafetyAgreement:
                case AllianceInvite:
                // The safety pact's weight: it is broken by the same act, a nomination.
                case FinalThree: return DealTrust.High;
                case InformationSharing: return DealTrust.Low;
                default: return DealTrust.Medium;
            }
        }

        /// <summary>
        /// The title the reference shows, used for the log line a deal writes - and, lowered, for
        /// the caption that proposes one ("Propose a voting bloc"). One departure from the
        /// reference's spelling: its "Voting Block" is a voting bloc (UI-UX-PASS-PLAN D0, the play
        /// sweep's row 31); <see cref="Titles"/> keeps the old spelling for the lines written under it.
        /// </summary>
        public static string Title(string type)
        {
            switch (type)
            {
                case TargetAgreement: return "Target Agreement";
                case SafetyAgreement: return "Safety Pact";
                case VoteTogether: return "Voting Bloc";
                case VoteSave: return "Vote to Save";
                case VoteEvict: return "Vote to Evict";
                case VetoUse: return "Veto Commitment";
                case InformationSharing: return "Information Sharing";
                case FinalTwo: return "Final Two Deal";
                case AllianceInvite: return "Alliance Invitation";
                case FinalThree: return "Final Three Deal";
                default: return "Partnership";
            }
        }

        /// <summary>
        /// Every spelling a deal's title has been written under, the current one first. A line of
        /// record - a ledger entry, a memory - is matched by rebuilding the engine's own sentence
        /// from the title (<see cref="YourWeek.ReadDeal"/>, <see cref="KnownBallots.TellsAnUnknownBallot"/>),
        /// so a reader tries each spelling: a season that settled a "voting block" before the word
        /// was corrected still reads as what it was.
        /// </summary>
        public static IEnumerable<string> Titles(string type)
        {
            yield return Title(type);
            if (type == VoteTogether) yield return "Voting Block";
        }
    }

    /// <summary>
    /// A deal's life, spelled as the reference spells it.
    ///
    /// <para><see cref="WebEvictionVoting"/> only distinguishes <c>active</c>, <c>broken</c> and
    /// <c>fulfilled</c>; the rest exist because a proposal the player has not answered yet is a real
    /// state and the reference models it.</para>
    /// </summary>
    public static class DealStatus
    {
        public const string Proposed = "proposed";
        public const string Accepted = "accepted";
        public const string Active = "active";
        public const string Fulfilled = "fulfilled";
        public const string Broken = "broken";
        public const string Declined = "declined";
        public const string Expired = "expired";

        public static readonly string[] All =
            { Proposed, Accepted, Active, Fulfilled, Broken, Declined, Expired };

        public static bool IsKnown(string status) => status != null && Array.IndexOf(All, status) >= 0;

        /// <summary>Whether the deal still binds anybody.</summary>
        public static bool Binds(string status) => status == Proposed || status == Accepted || status == Active;
    }

    public static class DealTrust
    {
        public const string Low = "low", Medium = "medium", High = "high", Critical = "critical";
        public static readonly string[] All = { Low, Medium, High, Critical };
        public static bool IsKnown(string trust) => trust != null && Array.IndexOf(All, trust) >= 0;

        /// <summary>
        /// What keeping or breaking a deal of this weight is worth on the ledger.
        ///
        /// <para>The reference's own <c>impactMultiplier</c> in <c>applyDealOutcome</c>: low 1,
        /// medium 1.5, high 2, critical 3. Applied to <see cref="DealResolution.FulfilledBase"/> and
        /// <see cref="DealResolution.BrokenBase"/>, a broken veto commitment moves −45 and a broken
        /// information swap −15 — which is why <c>trustImpact</c> exists at all, and why walking
        /// away from the block costs three times what walking away from gossip does.</para>
        /// </summary>
        public static double Weight(string trust)
        {
            switch (trust)
            {
                case Critical: return 3;
                case High: return 2;
                case Medium: return 1.5;
                default: return 1;
            }
        }

        /// <summary>
        /// One step heavier than <paramref name="trust"/>: low to medium, medium to high, high to
        /// critical. Critical is the heaviest there is and stays critical; anything unknown reads as
        /// low, as <see cref="Weight"/> reads it, and goes to medium. What an offer the player
        /// accepted stakes when it breaks, under the commitment rules (ACTIONS-DEALS-ALLIANCES-PLAN
        /// C1, decision 15; <see cref="DealResolution.BreachWeight"/>).
        /// </summary>
        public static string Heavier(string trust)
        {
            switch (trust)
            {
                case Critical:
                case High: return Critical;
                case Medium: return High;
                default: return Medium;
            }
        }
    }
}
