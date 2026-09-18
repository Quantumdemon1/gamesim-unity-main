using System;

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

        public DealState Clone() => (DealState)MemberwiseClone();
    }

    /// <summary>The ten deal types, spelled as the reference and the eviction vote spell them.</summary>
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

        public static readonly string[] All =
        {
            TargetAgreement, SafetyAgreement, VoteTogether, VoteSave, VoteEvict,
            VetoUse, InformationSharing, FinalTwo, Partnership, AllianceInvite,
        };

        public static bool IsKnown(string type) => type != null && Array.IndexOf(All, type) >= 0;

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
                case AllianceInvite: return DealTrust.High;
                case InformationSharing: return DealTrust.Low;
                default: return DealTrust.Medium;
            }
        }

        /// <summary>The title the reference shows, used for the log line a deal writes.</summary>
        public static string Title(string type)
        {
            switch (type)
            {
                case TargetAgreement: return "Target Agreement";
                case SafetyAgreement: return "Safety Pact";
                case VoteTogether: return "Voting Block";
                case VoteSave: return "Vote to Save";
                case VoteEvict: return "Vote to Evict";
                case VetoUse: return "Veto Commitment";
                case InformationSharing: return "Information Sharing";
                case FinalTwo: return "Final Two Deal";
                case AllianceInvite: return "Alliance Invitation";
                default: return "Partnership";
            }
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
    }
}
