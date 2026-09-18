using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The player's side of the deal table: what they may put to a houseguest, and how that
    /// houseguest makes up their mind.
    ///
    /// <para><see cref="NpcDeals"/> is the house bargaining among itself and deliberately leaves the
    /// player out, because a deal nobody asked them about is not a deal. This is the other half —
    /// the player asks, and the answer is a roll.</para>
    ///
    /// <para>Ported from <c>deal-system.ts</c>'s <c>evaluatePlayerDeal</c>. Unlike the NPC pass, this
    /// <b>does</b> draw from the season's generator: it runs only inside a committed command, where
    /// the draw is recorded and replays identically. That is the same line <see cref="NpcSocialActions"/>
    /// sits on, and the reason this lives here rather than in <see cref="NpcDeals"/>.</para>
    ///
    /// <para>The reference's counter-offer map is <b>not</b> ported. A counter-offer is a second
    /// proposal aimed back at the player, and there is nowhere for one to wait until the reference's
    /// pending-proposal queue is ported too; inventing one here would be a store the save format has
    /// no room for. A declined deal is simply declined, and the note is recorded so the omission is
    /// visible rather than lost.</para>
    /// </summary>
    public static class PlayerDeals
    {
        /// <summary>The floor the reference clamps acceptance to, in percent.</summary>
        public const double MinimumChance = 5, MaximumChance = 95;

        /// <summary>What each deal the player has broken costs them at the table.</summary>
        public const double BrokenDealPenalty = 12;

        /// <summary>What declining costs the houseguest who asked. The reference's number.</summary>
        public const double DeclineImpact = -3;

        /// <summary>What striking a bargain is worth on the ledger, either way round.</summary>
        public const double AcceptedImpact = 12, RefusedImpact = -4;

        /// <summary>How many deals a season lets the player hold at once, proposals included.</summary>
        public const int PlayerDealCeiling = 40;

        // ---------------------------------------------------------------- what may be asked

        /// <summary>
        /// Whether this kind of deal is one the player can put to this houseguest at all.
        ///
        /// <para>Separate from whether they would say yes. The reference gates a few types on the
        /// situation rather than on willingness — a veto commitment means nothing from somebody who
        /// does not hold the veto — and a control that offers those is a control that can only
        /// produce a refusal.</para>
        /// </summary>
        public static bool CanPropose(EpisodeState state, string toId, string type, string aboutId, out string reason)
        {
            reason = null;
            var target = state?.Find(toId);
            if (state == null || target == null || target.isPlayer || target.status != ContestantStatus.Active)
                return Refuse(out reason, "Approach an active housemate.");
            if (!DealKind.IsKnown(type))
                return Refuse(out reason, "That is not a deal anybody in this house would recognise.");
            if (state.week < state.dealRulesStartWeek)
                return Refuse(out reason, "The house is not making deals this week.");
            if (state.deals.Count >= PlayerDealCeiling)
                return Refuse(out reason, "You already have more arrangements than you can keep track of.");
            if (NpcDeals.Between(state, state.playerId, toId).Any(d => d.type == type))
                return Refuse(out reason, "You already have that arrangement with " + target.name + ".");

            switch (type)
            {
                case DealKind.VetoUse:
                    if (state.vetoHolderId != toId)
                        return Refuse(out reason, target.name + " does not hold the veto.");
                    break;
                case DealKind.AllianceInvite:
                    if (state.Allied(state.playerId, toId))
                        return Refuse(out reason, "You already share an active alliance.");
                    break;
                case DealKind.VoteSave:
                case DealKind.VoteEvict:
                case DealKind.VoteTogether:
                    if (state.evictionResolved || state.nominees.Count == 0)
                        return Refuse(out reason, "There is no vote to bargain over yet.");
                    break;
                case DealKind.TargetAgreement:
                    if (state.Find(aboutId) == null || aboutId == state.playerId || aboutId == toId
                        || state.Find(aboutId).status != ContestantStatus.Active)
                        return Refuse(out reason, "Name an active housemate you both want out.");
                    break;
                case DealKind.FinalTwo:
                    if (state.Active.Count() > NpcDeals.EndgameSize)
                        return Refuse(out reason, "It is too early in the season to be talking about the final two.");
                    break;
            }
            return true;
        }

        private static bool Refuse(out string reason, string text) { reason = text; return false; }

        /// <summary>
        /// Everything the player could put to this houseguest right now.
        ///
        /// <para>A target agreement is about somebody, so asking whether it is available with nobody
        /// named would always answer no. It is offered when there is <i>anybody</i> it could be
        /// about; which of them is a choice the control makes, not this list.</para>
        /// </summary>
        public static List<string> Available(EpisodeState state, string toId) =>
            DealKind.All.Where(type => type == DealKind.TargetAgreement
                    ? Subjects(state, toId).Any()
                    : CanPropose(state, toId, type, null, out _))
                .ToList();

        /// <summary>Everybody a deal with this houseguest could legitimately be about.</summary>
        public static List<string> Subjects(EpisodeState state, string toId) =>
            state == null ? new List<string>()
                : state.Active.Where(c => !c.isPlayer && c.id != toId)
                    .Select(c => c.id)
                    .Where(about => CanPropose(state, toId, DealKind.TargetAgreement, about, out _))
                    .ToList();

        // ---------------------------------------------------------------- what they would say

        /// <summary>
        /// The chance, in percent, that this houseguest agrees.
        ///
        /// <para>Every term and every number is the reference's, in its order. The five relationship
        /// tiers are interpolated rather than stepped, so a houseguest at 69 and one at 70 are not a
        /// coin flip apart; the trait table, the reputation penalty and the situational adjustments
        /// are applied on top, and the whole thing is clamped to 5–95 so nothing is ever certain.
        /// </para>
        /// </summary>
        public static double AcceptanceChance(EpisodeState state, string npcId, string type, string aboutId)
        {
            var npc = state.Find(npcId);
            if (npc == null) return 0;
            double relationship = state.Score(npcId, state.playerId);

            double chance;
            if (relationship >= 70) chance = 80 + (relationship - 70) / 30 * 15;
            else if (relationship >= 35) chance = 55 + (relationship - 35) / 35 * 20;
            else if (relationship >= -10) chance = 40 + (relationship + 10) / 45 * 15;
            else if (relationship >= -50) chance = 20 + (relationship + 50) / 40 * 15;
            else chance = 5 + Math.Max(0, (relationship + 100) / 50) * 10;

            // How much of themselves the deal asks the houseguest to spend.
            if (type == DealKind.InformationSharing || type == DealKind.Partnership) chance += 10;
            else if (type == DealKind.FinalTwo || type == DealKind.VetoUse || type == DealKind.AllianceInvite) chance -= 10;

            chance += (ThreatAssessment.TrustScore(state, state.playerId, npcId) - ThreatAssessment.NeutralTrust) * 0.2;
            chance -= BrokenDealPenalty * state.deals.Count(d => d.status == DealStatus.Broken
                && (d.proposerId == state.playerId || d.recipientId == state.playerId));

            bool allied = state.Allied(npcId, state.playerId);
            if (allied)
            {
                chance += 20;
                if (type == DealKind.SafetyAgreement || type == DealKind.VoteTogether || type == DealKind.FinalTwo)
                    chance += 10;
            }

            if (type == DealKind.TargetAgreement && aboutId != null)
            {
                if (state.Allied(npcId, aboutId)) chance -= 40;
                double toTarget = state.Score(npcId, aboutId);
                if (toTarget < -20) chance += 20;
                else if (toTarget > 30) chance -= 30;
            }

            chance += TraitModifier(npc.traits, type);

            bool nominated = !state.evictionResolved && state.nominees.Contains(npcId);
            if (type == DealKind.SafetyAgreement && nominated) chance += 25;
            if (type == DealKind.VoteTogether && nominated) chance += 35;

            if (type == DealKind.VoteSave)
            {
                if (aboutId != null)
                {
                    double toTarget = state.Score(npcId, aboutId);
                    if (toTarget >= 50) chance += 20;
                    else if (toTarget < 0) chance -= 20;
                }
                else if (nominated) chance -= 10;
            }

            if (type == DealKind.VoteEvict && aboutId != null)
            {
                double toTarget = state.Score(npcId, aboutId);
                if (toTarget < -20) chance += 25;
                else if (toTarget > 50) chance -= 30;
                if (Has(npc, "Loyal") && relationship >= 80) chance += 15;
                if (Has(npc, "Strategic")) chance += 10;
            }

            if (type == DealKind.FinalTwo)
            {
                if (relationship < 50) chance -= 25;
                if (state.Active.Count() > NpcDeals.EndgameSize && relationship < 80) chance -= 15;
            }

            if (type == DealKind.Partnership && allied) chance += 15;

            return Math.Max(MinimumChance, Math.Min(MaximumChance, chance));
        }

        private static bool Has(ContestantState npc, string trait) =>
            npc.traits.Any(t => string.Equals(t, trait, StringComparison.OrdinalIgnoreCase));

        /// <summary>The reference's <c>getTraitDealModifiers</c>, trait for trait.</summary>
        public static double TraitModifier(IEnumerable<string> traits, string type)
        {
            double modifier = 0;
            foreach (string trait in traits ?? Enumerable.Empty<string>())
            {
                switch ((trait ?? "").ToLowerInvariant())
                {
                    case "strategic":
                        if (type == DealKind.TargetAgreement || type == DealKind.Partnership) modifier += 10;
                        break;
                    case "loyal":
                        if (type == DealKind.SafetyAgreement || type == DealKind.AllianceInvite
                            || type == DealKind.FinalTwo) modifier += 20;
                        if (type == DealKind.TargetAgreement) modifier -= 10;
                        break;
                    case "sneaky":
                        if (type == DealKind.InformationSharing) modifier += 15;
                        modifier -= 5;
                        break;
                    case "competitive":
                        if (type == DealKind.TargetAgreement) modifier += 15;
                        break;
                    case "emotional":
                        if (type == DealKind.FinalTwo || type == DealKind.Partnership) modifier += 25;
                        break;
                    case "paranoid":
                        if (type == DealKind.SafetyAgreement) modifier += 10;
                        modifier -= 15;
                        break;
                    case "analytical":
                        if (type == DealKind.InformationSharing || type == DealKind.TargetAgreement) modifier += 15;
                        if (type == DealKind.Partnership || type == DealKind.SafetyAgreement) modifier += 5;
                        break;
                }
            }
            return modifier;
        }

        /// <summary>What the houseguest says, in their own words, from the reference's lines.</summary>
        public static string Reasoning(EpisodeState state, string npcId, string type, bool accepted)
        {
            double relationship = state.Score(npcId, state.playerId);
            bool nominated = !state.evictionResolved && state.nominees.Contains(npcId);
            bool allied = state.Allied(npcId, state.playerId);
            int broken = state.deals.Count(d => d.status == DealStatus.Broken
                && (d.proposerId == state.playerId || d.recipientId == state.playerId));

            if (accepted)
            {
                if (relationship > 40) return "I think we can work well together.";
                if (nominated) return "I need all the help I can get right now.";
                if (allied) return "We're already working together, so this makes sense.";
                return "This could be beneficial for both of us.";
            }
            if (BrokenDealPenalty * broken > 20) return "I've heard you've broken deals before. I can't trust that.";
            if (relationship < 20) return "I don't think I can trust you with that.";
            if (ThreatAssessment.TrustScore(state, state.playerId, npcId) < 40) return "Your track record concerns me.";
            return "I'm not sure this is the right move for me.";
        }

        /// <summary>
        /// The deal that would be written if this houseguest said yes.
        ///
        /// <para>Split out from the command so the same expiry and trust rules serve the HUD's
        /// preview and the engine's commit — the reference's own bug class is a screen promising one
        /// thing and the reducer writing another.</para>
        /// </summary>
        public static DealState Draft(EpisodeState state, string toId, string type, string aboutId, string id) =>
            new DealState
            {
                id = id,
                type = type,
                proposerId = state.playerId,
                recipientId = toId,
                targetId = DealKind.NamesATarget(type) ? aboutId : null,
                status = DealStatus.Active,
                week = state.week,
                expiresWeek = type == DealKind.FinalTwo || type == DealKind.Partnership
                              || type == DealKind.AllianceInvite || type == DealKind.InformationSharing
                    ? 0 : state.week,
                trustImpact = DealKind.DefaultTrust(type),
            };
    }
}
