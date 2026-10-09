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

        /// <summary>
        /// What accepting a houseguest's offer is worth under the commitment rules
        /// (ACTIONS-DEALS-ALLIANCES-PLAN C1, decision 15): +4, not <see cref="AcceptedImpact"/>'s +12.
        /// Answering costs no action, so +12 for every yes made accepting everything the dominant
        /// play; under the rules the yes is a commitment instead, and an offer accepted and then
        /// broken weighs one step heavier (<see cref="DealResolution.BreachWeight"/>). A deal the
        /// player puts to somebody, which costs an action and a roll, keeps its +12.
        /// </summary>
        public const double CommittedAcceptedImpact = 4;

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
            // A final three deal is the commitment rules' own (C9): without them it is refused in the words
            // an unknown kind always was, so a season without the rules refuses exactly what it refused.
            if (!DealKind.IsKnown(type) || (DealKind.CommitmentRulesOnly(type) && !EpisodeEngine.CommitmentRulesOn(state)))
                return Refuse(out reason, "That is not a deal anybody in this house would recognise.");
            if (state.week < state.dealRulesStartWeek)
                return Refuse(out reason, "The house is not making deals this week.");
            if (UnifiedVoteStore.DealCount(state) >= PlayerDealCeiling)
                return Refuse(out reason, "You already have more arrangements than you can keep track of.");
            // A price binds only what it names (C7, under the commitment rules, where prices are struck): an
            // open vote to keep the player, owed for the veto, is no vote deal about anybody else.
            if (!(UnifiedCommitments.SafetyAuthorityOn(state) && type == DealKind.SafetyAgreement)
                && NpcDeals.Between(state, state.playerId, toId).Any(d => d.type == type
                    && (!Negotiation.IsPrice(d) || d.targetId == (DealKind.NamesATarget(type) ? aboutId : null))))
                return Refuse(out reason, "You already have that arrangement with " + target.name + ".");

            switch (type)
            {
                case DealKind.VetoUse:
                    if (state.vetoHolderId != toId)
                        return Refuse(out reason, target.name + " does not hold the veto.");
                    // A commitment to a decision already taken is one nobody can keep. Seasons from
                    // before the strategy windows keep offering it, as they always did.
                    if (StrategyRules.Apply(state) && state.vetoResolved)
                        return Refuse(out reason, "The veto has already been decided this week.");
                    break;
                case DealKind.AllianceInvite:
                    if (state.Allied(state.playerId, toId))
                        return Refuse(out reason, EpisodeEngine.AlreadyAlliedRefusal);
                    // The player's three (ACTIONS-DEALS-ALLIANCES-PLAN C4, decision 10): under the
                    // commitment rules an invitation agreed now would make a fourth.
                    if (EpisodeEngine.InvitationPastPactCap(state, toId))
                        return Refuse(out reason, EpisodeEngine.PactCapRefusal);
                    break;
                case DealKind.VoteSave:
                case DealKind.VoteEvict:
                case DealKind.VoteTogether:
                    if (state.evictionResolved || state.nominees.Count == 0)
                        return Refuse(out reason, "There is no vote to bargain over yet.");
                    // Under the levers a vote deal names who it is about, so it can enter a voter's
                    // ballot and be judged at the reveal. Before them it named nobody, as it always had.
                    if (EpisodeEngine.LeverRulesOn(state) && type != DealKind.VoteTogether && (aboutId == null || !state.nominees.Contains(aboutId)))
                        return Refuse(out reason, "Say who the vote is about: somebody on the block.");
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
                // The endgame's (C9): from the final six, while there is still a final three to reach.
                case DealKind.FinalThree:
                    if (state.Active.Count() > NpcDeals.EndgameSize)
                        return Refuse(out reason, "It is too early in the season to be talking about the final three.");
                    if (state.Active.Count() <= NpcDeals.FinalThreeSize)
                        return Refuse(out reason, FinalThreeHereRefusal);
                    if (NpcDeals.FinalFourBlockSet(state))
                        return Refuse(out reason, FinalFourBlockSetRefusal);
                    // A final two already binds them further (the review's M2), as the houseguests' own ladder has it.
                    if (NpcDeals.Between(state, state.playerId, toId).Any(d => d.type == DealKind.FinalTwo))
                        return Refuse(out reason, FinalTwoBindsRefusal);
                    break;
            }
            if (UnifiedCommitments.SafetyAuthorityOn(state) && type == DealKind.SafetyAgreement)
                return UnifiedCommitmentStore.CanAddDeal(state,
                    Draft(state, toId, type, aboutId, "deal-player-" + state.nextSequence), UnifiedCommitments.PlayerDeal, out reason);
            return true;
        }

        private static bool Refuse(out string reason, string text) { reason = text; return false; }

        /// <summary>Why a final three deal cannot be put, or an offer of one taken, once the house is down to three (C9).</summary>
        public const string FinalThreeHereRefusal = "The final three is already here.";

        /// <summary>Why a final three deal cannot be put, or taken, once the final four's block is set (C9, <see cref="NpcDeals.FinalFourBlockSet"/>).</summary>
        public const string FinalFourBlockSetRefusal = "The final four's block is set: it is too late for a final three deal.";

        /// <summary>Why a final three deal is not put to somebody a final two deal already binds the player to (C9).</summary>
        public const string FinalTwoBindsRefusal = "You already have a final two deal with them.";

        /// <summary>
        /// Below this view of the player a final three deal is asked as a stretch (C9): the deal table's
        /// "favourable" tier, a lower bar than a final two's fifty, since it asks less - to get there
        /// together, not to sit together at the end.
        /// </summary>
        public const double FinalThreeWarmLine = 35;

        /// <summary>What a final three deal loses below <see cref="FinalThreeWarmLine"/> (C9), as a final two loses 25 below fifty.</summary>
        public const double FinalThreeColdPenalty = 15;

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
                    : EpisodeEngine.LeverRulesOn(state) && (type == DealKind.VoteSave || type == DealKind.VoteEvict)
                        ? state.nominees.Any(id => CanPropose(state, toId, type, id, out _))
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

            double chance = RelationshipChance(relationship);

            // How much of themselves the deal asks the houseguest to spend. A final three deal (C9) is
            // asked as the other endgame commitment is.
            if (type == DealKind.InformationSharing || type == DealKind.Partnership) chance += 10;
            else if (type == DealKind.FinalTwo || type == DealKind.VetoUse || type == DealKind.AllianceInvite || type == DealKind.FinalThree) chance -= 10;

            chance += (ThreatAssessment.TrustScore(state, state.playerId, npcId) - ThreatAssessment.NeutralTrust) * 0.2;
            // The player's word: under the commitment rules the deals they broke (C0, X3), and where the
            // house keeps knowledge, only what it has heard of them (C8, YourWord).
            chance -= WordPenalty(state);

            // Under the commitment rules only an ally whose own commitment holds (Allegiance.Holds; C2, C3).
            bool allied = Allegiance.Holds(state, npcId, state.playerId);
            if (allied)
            {
                chance += 20;
                if (type == DealKind.SafetyAgreement || type == DealKind.VoteTogether || type == DealKind.FinalTwo || type == DealKind.FinalThree)
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

            if (type == DealKind.FinalThree && relationship < FinalThreeWarmLine) chance -= FinalThreeColdPenalty;

            if (type == DealKind.Partnership && allied) chance += 15;

            return Math.Max(MinimumChance, Math.Min(MaximumChance, chance));
        }

        /// <summary>
        /// What the player's word costs them at the table, in points of a deal's chance - the one term
        /// the roll (<see cref="AcceptanceChance"/>) and the odds the player is shown
        /// (<see cref="KnownOdds.Deal"/>) both take, so the two never disagree on it.
        ///
        /// <para>Where the house keeps the player's word as knowledge (ACTIONS-DEALS-ALLIANCES-PLAN C8,
        /// <see cref="YourWord.On"/>), the public reading: what the house has heard of the player going
        /// back on their word, and nothing it has not (<see cref="YourWord.Cost"/>) - a deal broken by
        /// the player's own ballot, which the house never sees, costs nothing here, though the one it
        /// was broken against still holds it in their own view and record. Otherwise
        /// <see cref="BrokenDealPenalty"/> for every deal held against the player: under the commitment
        /// rules the ones they broke (C0, X3), before them every one they were a party to. Under the
        /// canonical Safety rules that fallback counts actual incidents containing deal evidence,
        /// not projected aliases or promise-only incidents. It never replaces the audible knowledge
        /// policy with private canonical history.</para>
        /// </summary>
        public static double WordPenalty(EpisodeState state) =>
            YourWord.On(state) ? YourWord.Cost(state) : BrokenDealPenalty * NpcDeals.BrokenDeals(state, state.playerId);

        /// <summary>
        /// Where the reference's five relationship tiers put a houseguest's willingness, in percent,
        /// before anything else is weighed. Interpolated rather than stepped.
        /// </summary>
        public static double RelationshipChance(double relationship)
        {
            if (relationship >= 70) return 80 + (relationship - 70) / 30 * 15;
            if (relationship >= 35) return 55 + (relationship - 35) / 35 * 20;
            if (relationship >= -10) return 40 + (relationship + 10) / 45 * 15;
            if (relationship >= -50) return 20 + (relationship + 50) / 40 * 15;
            return 5 + Math.Max(0, (relationship + 100) / 50) * 10;
        }

        private static bool Has(ContestantState npc, string trait) =>
            npc.traits.Any(t => string.Equals(t, trait, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// The reference's <c>getTraitDealModifiers</c>, trait for trait. A final three deal, this port's
        /// own (C9), is read as the reference reads a final two: the loyal and the emotional want it.
        /// </summary>
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
                            || type == DealKind.FinalTwo || type == DealKind.FinalThree) modifier += 20;
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
                        if (type == DealKind.FinalTwo || type == DealKind.Partnership || type == DealKind.FinalThree) modifier += 25;
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

        /// <summary>
        /// What the houseguest says, in their own words, from the reference's lines.
        ///
        /// <para>Under the commitment rules the words come only from what the player knows
        /// (ACTIONS-DEALS-ALLIANCES-PLAN C4, the line half of X9): how the houseguest sees the player is
        /// the player's own read of it (<see cref="KnownOdds.PresumedView"/>), and a track record is the
        /// player's own breaches. Before them the line read the houseguest's hidden view and their
        /// private record of the player, so two answers worded differently told the player which of
        /// two houseguests thought less of them - and a refused alliance, its reason.</para>
        /// </summary>
        public static string Reasoning(EpisodeState state, string npcId, string type, bool accepted)
        {
            bool rules = EpisodeEngine.CommitmentRulesOn(state);
            double relationship = rules ? KnownOdds.PresumedView(state, npcId) : state.Score(npcId, state.playerId);
            bool nominated = !state.evictionResolved && state.nominees.Contains(npcId);
            bool allied = Allegiance.Holds(state, npcId, state.playerId);
            // "I've heard you've broken deals" says it only of deals the player broke, under the commitment rules (C0, X3).
            int broken = NpcDeals.BrokenDeals(state, state.playerId);

            if (accepted)
            {
                if (relationship > 40) return "I think we can work well together.";
                if (nominated) return "I need all the help I can get right now.";
                if (allied) return "We're already working together, so this makes sense.";
                return "This could be beneficial for both of us.";
            }
            // "I've heard": where the house keeps the player's word as knowledge (C8), said only of what it
            // heard, and only by somebody who has heard of a breach of theirs themselves.
            bool word = YourWord.On(state);
            if (WordPenalty(state) > 20 && (!word || YourWord.Breaches(state).Any(f => YourWord.HeardBy(state, f).Contains(npcId))))
                return "I've heard you've broken deals before. I can't trust that.";
            if (relationship < 20) return "I don't think I can trust you with that.";
            // A track record: under the commitment rules the player's own broken promises, and their broken
            // deals - where the house keeps their word, only the ones the house has heard of (C8).
            bool brokenDeals = word ? YourWord.Cost(state) > 0 : broken > 0;
            if (rules ? brokenDeals || HasBrokenPromise(state)
                    : ThreatAssessment.TrustScore(state, state.playerId, npcId) < 40)
                return "Your track record concerns me.";
            return "I'm not sure this is the right move for me.";
        }

        private static bool HasBrokenPromise(EpisodeState state)
        {
            // Mode 2 (vote family V5d): mode 1's raw list, its vote promises the canonical rows.
            if (CommitmentReferences.RawPromises(state).Any(p => Breaches.CountsAgainst(state, p, state.playerId))) return true;
            if (!UnifiedCommitments.SafetyAuthorityOn(state)) return false;
            var promises = new HashSet<string>(UnifiedCommitmentHistory.Records(state)
                .Where(row => row.sourcePolicy == UnifiedCommitments.PromisePolicy).Select(row => row.id), StringComparer.Ordinal);
            return UnifiedCommitmentHistory.Breaches(state)
                .Any(incident => incident.ActorId == state.playerId && incident.EvidenceIds.Any(promises.Contains));
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
                // A final three deal runs until the house is down to three (C9), whenever that is.
                expiresWeek = type == DealKind.FinalTwo || type == DealKind.Partnership
                              || type == DealKind.AllianceInvite || type == DealKind.InformationSharing
                              || type == DealKind.FinalThree
                    ? 0 : state.week,
                trustImpact = DealKind.DefaultTrust(type),
            };
    }
}
