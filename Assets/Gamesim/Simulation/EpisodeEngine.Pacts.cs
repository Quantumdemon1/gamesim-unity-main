using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// One way to form an alliance (ACTIONS-DEALS-ALLIANCES-PLAN C4), under the commitment rules.
    ///
    /// <para><b>The proposal rolls.</b> 'Propose an alliance' used to pass a gate on how the
    /// houseguest privately sees the player - eight or more - and draw no roll, so a refusal cost
    /// nothing and told the player that number was under eight (X9's free probe). Under the rules it
    /// asks the alliance invitation's own question: one roll from the season's stream against
    /// <see cref="AllianceChance"/>, the invitation's <see cref="PlayerDeals.AcceptanceChance"/>, and the
    /// player is shown that chance as <see cref="KnownOdds.Alliance"/> works it out from what they
    /// know (V6). A houseguest who holds forty or more against the player says no whatever the roll
    /// (the web's grudge line for a pact, as <see cref="NpcAlliances.WouldPropose"/> draws it); the
    /// roll is drawn all the same, so every proposal draws exactly one. The one such grudge the
    /// player can know of - the one their own walk-out from a pact with them left - the shown chance
    /// takes too. Either answer spends the action, and a no is worded from what the player knows
    /// (<see cref="PlayerDeals.Reasoning"/>) and logged as its own kind (<see cref="AllianceRefusedKind"/>).
    /// The deal table's alliance invitation asks the same question with the same grudge.</para>
    ///
    /// <para><b>Three at once.</b> The player holds at most <see cref="PlayerPactCap"/> pacts
    /// (decision 10). A fourth is refused before anybody is asked - by the proposal, the deal table's
    /// invitation, the yes to an invitation put to the player, and a story's pact alike - and nobody
    /// puts an invitation to a player who holds three (<see cref="NpcDeals.Offer"/>). The player's
    /// own pacts are theirs to know, so that refusal spends nothing.</para>
    ///
    /// <para><b>A pact is on the record.</b> Every pact the player comes into writes the ledger's
    /// permanent 'alliance-formed' between them and each partner, both ways, as a pact between
    /// houseguests always has (<see cref="NpcAlliances"/>), so it builds the trust the house reads
    /// (<see cref="ThreatAssessment.TrustScore"/>).</para>
    ///
    /// <para>Before the rules every path plays as it did, roll for roll and line for line: the gate,
    /// no roll, no cap and no record.</para>
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>How many pacts the player may hold at once under the commitment rules (decision 10): as many as a houseguest keeps (<see cref="NpcAlliances.MaximumEach"/>).</summary>
        public const int PlayerPactCap = 3;

        /// <summary>A grudge this heavy refuses a pact: the web's line, the one <see cref="NpcAlliances.WouldPropose"/> draws.</summary>
        public const double AllianceGrudgeLine = 40;

        /// <summary>
        /// What walking out of a pact leaves every other member holding against whoever walked: the
        /// web's alliance-betrayed eighty (<c>StoryAllianceLeft</c>), fading by
        /// <see cref="Grudges.DecayPerWeek"/> a week. The one grudge the player can reckon for
        /// themselves, since their own act wrote it (<see cref="KnownOdds.KnownWalkOut"/>).
        /// </summary>
        public const double AllianceLeftGrudge = 80;

        /// <summary>What forming a pact is worth on the ledger, either way round: the source's +30 for 'alliance-formed', which never fades.</summary>
        public const double AllianceFormedImpact = 30;

        /// <summary>Why a pact the player already shares cannot be proposed again.</summary>
        public const string AlreadyAlliedRefusal = "You already share an active alliance.";

        /// <summary>Why a fourth pact is refused: the player's own three, which they can count.</summary>
        public const string PactCapRefusal = "You already hold three alliances, the most you can keep at once. Leave one before you make another.";

        /// <summary>
        /// The kind of the line a refused proposal logs: its own, so it is never drawn with an
        /// alliance's green and handshake or read as one of the week's alliance lines, which are
        /// pacts formed and ended.
        /// </summary>
        public const string AllianceRefusedKind = "alliance-refused";

        /// <summary>The active pacts the player is in.</summary>
        public static int PlayerPactsHeld(EpisodeState s) =>
            s?.alliances == null ? 0 : s.alliances.Count(a => a != null && a.active && a.members.Contains(s.playerId));

        /// <summary>Whether the player holds as many pacts as the commitment rules let them (decision 10). Never before the rules.</summary>
        public static bool AtPactCap(EpisodeState s) => CommitmentRulesOn(s) && PlayerPactsHeld(s) >= PlayerPactCap;

        /// <summary>
        /// Why the player cannot put an alliance to this houseguest at all, or null when they can: a
        /// pact the two of them already share, or, under the commitment rules, the player's three.
        /// Both are the player's own to know, so a press refused for either spends nothing.
        /// </summary>
        public static string AllianceRefusal(EpisodeState s, string npcId)
        {
            if (s == null || string.IsNullOrEmpty(npcId)) return null;
            if (s.Allied(s.playerId, npcId)) return AlreadyAlliedRefusal;
            return AtPactCap(s) ? PactCapRefusal : null;
        }

        /// <summary>
        /// Whether an alliance invitation between the player and this houseguest, agreed now, would
        /// bring the player into a pact past their three: under the commitment rules, where the
        /// strategy windows make an agreed invitation a pact (<c>AllyThroughInvitation</c>) and the
        /// two do not already share one.
        /// </summary>
        public static bool InvitationPastPactCap(EpisodeState s, string npcId) =>
            AtPactCap(s) && StrategyRules.Apply(s) && !s.Allied(s.playerId, npcId);

        /// <summary>
        /// The chance, in percent, that this houseguest says yes to the player's alliance: the alliance
        /// invitation's, which is the roll a proposal draws against under the commitment rules. It
        /// reads how they privately see the player; the player is shown <see cref="KnownOdds.Alliance"/>.
        /// </summary>
        public static double AllianceChance(EpisodeState s, string npcId) =>
            PlayerDeals.AcceptanceChance(s, npcId, DealKind.AllianceInvite, null);

        /// <summary>
        /// Whether a grudge keeps this houseguest out of a pact with the player whatever the roll:
        /// forty or more held against the player, under the commitment rules and where grudges exist
        /// at all. Never before the rules, where the gate and the deal's roll were all there was.
        /// </summary>
        public static bool GrudgeRefusesAlliance(EpisodeState s, string npcId) =>
            CommitmentRulesOn(s) && StoryAt(s, StoryRules.Grudges)
            && Grudges.Severity(s, npcId, s.playerId) >= AllianceGrudgeLine;

        /// <summary>
        /// 'Propose an alliance' under the commitment rules: refused for nothing when the player can
        /// see it cannot be had; otherwise one roll against the invitation's chance, a grudge's no
        /// whatever it says, and the answer - a pact, or a line saying they turned it down. The caller
        /// spends the action either way.
        /// </summary>
        private static void ProposeAlliance(EpisodeState s, ContestantState target)
        {
            string refusal = AllianceRefusal(s, target.id);
            Require(refusal == null, refusal);
            double chance = AllianceChance(s, target.id);
            bool grudge = GrudgeRefusesAlliance(s, target.id);
            // The roll first and always: one per proposal, whatever the grudge says.
            bool accepted = Roll(s) * 100 < chance && !grudge;
            if (accepted)
            {
                FormPact(s, target, () => Roll(s));
                if (StoryAt(s, StoryRules.Bonds)) Knowledge.AllianceFormed(s, s.alliances.Last());
                return;
            }
            string said = PlayerDeals.Reasoning(s, target.id, DealKind.AllianceInvite, false);
            Log(s, AllianceRefusedKind, target.name + " turned down your alliance. “" + said + "”", s.playerId, target.id);
        }

        /// <summary>
        /// The ledger's permanent 'alliance-formed' between the player and each of their partners in
        /// a pact they have just come into who is still in the house - a pact of three or more can
        /// carry a member already evicted - both ways and at the source's thirty, as
        /// <see cref="NpcAlliances"/> writes it for a pact between houseguests. Under the commitment
        /// rules only (C4): before them a player's pact was never on the record.
        /// </summary>
        private static void RecordPactFormed(EpisodeState s, AllianceState pact, string description)
        {
            if (!CommitmentRulesOn(s) || pact == null) return;
            foreach (string partner in pact.members.Where(id => id != s.playerId && s.Find(id)?.status == ContestantStatus.Active).ToList())
                RelationshipLedger.Record(s, s.playerId, partner, "alliance-formed", AllianceFormedImpact, description);
        }
    }
}
