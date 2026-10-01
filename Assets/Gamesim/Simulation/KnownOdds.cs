using System;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The chance a deal, a plea or an alliance invitation is shown with, worked out from what the
    /// player knows (ACTIONS-DEALS-ALLIANCES-PLAN V6, closing X9's odds half).
    ///
    /// <para><b>Why not the roll's own number.</b> <see cref="PlayerDeals.AcceptanceChance"/> and
    /// <see cref="StrategyRules.Chance"/> read how the houseguest privately sees the player and the
    /// person an ask is about, and the pacts they are secretly in. Drawn as a word beside every
    /// row, those numbers let a player line up "a target agreement against A, B and C" and read off
    /// whom a houseguest likes and who they are secretly allied with. The roll still reads them -
    /// this changes what is shown, never what happens - so an answer can differ from its word, and
    /// the screen says the word is the player's read, not a promise.</para>
    ///
    /// <para><b>The roll's formula, with each hidden term swapped for what the player has.</b> A
    /// term the player can see is kept as the engine has it: the kind of deal, the houseguest's
    /// traits (the conversation's header names them), the week's roles, the house's size, the
    /// player's own deals and pacts. The hidden ones become:</para>
    /// <list type="bullet">
    /// <item>their view of the player: the player's own reading of them, held inside the band a
    /// current read put them in (<see cref="PresumedView"/>);</item>
    /// <item>their view of anybody else: the band the player last learned for the pair - read, told
    /// or overheard - while it is current (<see cref="Band"/>);</item>
    /// <item>a pact with anybody else: only one the player knows of (<see cref="KnownPact"/>),
    /// counted however it ended, because a pact the player is not in ends on scores the player
    /// never sees;</item>
    /// <item>the ledger's trust, the houseguest's private record of the player: left out, read as
    /// neutral.</item>
    /// </list>
    ///
    /// <para><b>How much it has to go on.</b> <see cref="Estimate.unknowns"/> says how little the
    /// read can see: <see cref="Many"/> when the player has no current read of the houseguest, no
    /// claim from them about the vote and little history with them. A claim counts as knowing them
    /// but stays out of the number, as the vote read keeps it beside its own: it says how somebody
    /// votes, not how they see anybody.</para>
    ///
    /// <para>Pure and read-only: it neither mutates the state nor draws from its generator, and it
    /// lives in the simulation so the Unity-free subset can test it.</para>
    /// </summary>
    public static class KnownOdds
    {
        /// <summary>The deal table's words for a chance, on its thresholds, as it has always said them.</summary>
        public const string Likely = "likely", Favourable = "favourable", AboutEven = "about even", AStretch = "a stretch", Unlikely = "unlikely";

        /// <summary>How much a read cannot see.</summary>
        public const string Few = "few", Some = "some", Many = "many";

        /// <summary>A learned standing's three bands: "warm on you", "not sure about you", "cold on you", and their told and overheard forms.</summary>
        public const string Warm = "warm", Unsure = "unsure", Cold = "cold";

        /// <summary>Where the bands part: the engine's own lines for every read, account and overheard conversation it reports.</summary>
        public const double WarmLine = 25, ColdLine = -25;

        /// <summary>How many records with somebody, past reads and claims, before the two of them have a history.</summary>
        public const int HistoryEnough = 2;

        /// <summary>One chance as shown: the number and its word, and how much the read behind it could not see.</summary>
        public sealed class Estimate
        {
            /// <summary>The chance in percent from the known terms alone, clamped as the roll's is. Never the roll's own.</summary>
            public double chance;
            public string word = Unlikely;
            /// <summary><see cref="Few"/>, <see cref="Some"/> or <see cref="Many"/>.</summary>
            public string unknowns = Many;
            /// <summary>What the player has on the houseguest: a current read, a claim about the vote, and how many records between them.</summary>
            public bool read, claim;
            public int history;
            /// <summary>Whether the player knows anything of where the houseguest stands with the third person the ask is about; true when it is about nobody else.</summary>
            public bool aboutKnown = true;
        }

        /// <summary>"about even" rather than "51%", so the chip reads as a judgement and not a promise.</summary>
        public static string Word(double chance)
        {
            if (chance >= 75) return Likely;
            if (chance >= 55) return Favourable;
            if (chance >= 45) return AboutEven;
            if (chance >= 25) return AStretch;
            return Unlikely;
        }

        // ---------------------------------------------------------------- a deal

        /// <summary>
        /// The chance shown beside a proposal to this houseguest: <see cref="PlayerDeals.AcceptanceChance"/>
        /// term for term, in its order, each hidden term swapped for what the player has.
        /// </summary>
        public static Estimate Deal(EpisodeState s, string npcId, string type, string aboutId)
        {
            var npc = s?.Find(npcId);
            if (npc == null) return new Estimate();
            var e = Start(s, npcId);
            double view = PresumedView(s, npcId);
            double chance = PlayerDeals.RelationshipChance(view);

            if (type == DealKind.InformationSharing || type == DealKind.Partnership) chance += 10;
            else if (type == DealKind.FinalTwo || type == DealKind.VetoUse || type == DealKind.AllianceInvite) chance -= 10;

            // The ledger's trust is the houseguest's own record of the player: left out, as neutral.
            // Deals the player has broken are the player's own record, and count as the roll counts them.
            chance -= PlayerDeals.BrokenDealPenalty * s.deals.Count(d => d.status == DealStatus.Broken
                && (d.proposerId == s.playerId || d.recipientId == s.playerId));

            bool allied = KnownPact(s, npcId, s.playerId);
            if (allied)
            {
                chance += 20;
                if (type == DealKind.SafetyAgreement || type == DealKind.VoteTogether || type == DealKind.FinalTwo)
                    chance += 10;
            }

            if (type == DealKind.TargetAgreement && aboutId != null && aboutId != npcId)
            {
                About(s, e, npcId, aboutId, out string band, out bool pact);
                if (pact) chance -= 40;
                if (band == Cold) chance += 20;
                else if (band == Warm) chance -= 30;
            }

            chance += PlayerDeals.TraitModifier(npc.traits, type);

            bool nominated = !s.evictionResolved && s.nominees.Contains(npcId);
            if (type == DealKind.SafetyAgreement && nominated) chance += 25;
            if (type == DealKind.VoteTogether && nominated) chance += 35;

            if (type == DealKind.VoteSave)
            {
                if (aboutId == s.playerId)
                {
                    // Of the player, the presumed view on the roll's own lines.
                    if (view >= 50) chance += 20;
                    else if (view < 0) chance -= 20;
                }
                else if (aboutId != null && aboutId != npcId)
                {
                    About(s, e, npcId, aboutId, out string band, out _);
                    if (band == Warm) chance += 20;
                    else if (band == Cold) chance -= 20;
                }
                else if (aboutId == null && nominated) chance -= 10;
            }

            if (type == DealKind.VoteEvict && aboutId != null)
            {
                if (aboutId == s.playerId)
                {
                    if (view < -20) chance += 25;
                    else if (view > 50) chance -= 30;
                }
                else if (aboutId != npcId)
                {
                    About(s, e, npcId, aboutId, out string band, out _);
                    if (band == Cold) chance += 25;
                    else if (band == Warm) chance -= 30;
                }
                if (Has(npc, "Loyal") && view >= 80) chance += 15;
                if (Has(npc, "Strategic")) chance += 10;
            }

            if (type == DealKind.FinalTwo)
            {
                if (view < 50) chance -= 25;
                if (s.Active.Count() > NpcDeals.EndgameSize && view < 80) chance -= 15;
            }

            if (type == DealKind.Partnership && allied) chance += 15;

            return Finish(e, chance);
        }

        // ---------------------------------------------------------------- a plea

        /// <summary>
        /// The chance shown beside one way of putting a plea: <see cref="StrategyRules.Chance"/> term
        /// for term, each hidden term swapped for what the player has.
        /// </summary>
        public static Estimate Plea(EpisodeState s, string deciderId, string ask, string subjectId, string approach)
        {
            var decider = s?.Find(deciderId);
            if (decider == null || !LobbyApproach.IsKnown(approach)) return new Estimate();
            var e = Start(s, deciderId);
            double chance = LobbyApproach.Base(approach) + LobbyApproach.TraitFit(decider.traits, approach)
                + JsRound(PresumedView(s, deciderId) / 5);
            if (s.nominees.Contains(s.playerId)) chance += 10;
            if (NpcDeals.Between(s, deciderId, s.playerId).Count > 0) chance += 10;
            switch (ask)
            {
                case LobbyAsk.Vote:
                    if (KnownPact(s, deciderId, s.playerId)) chance += 15;
                    string other = s.nominees.FirstOrDefault(id => id != s.playerId);
                    if (other != null)
                    {
                        About(s, e, deciderId, other, out string band, out bool pact);
                        if (pact) chance -= 20;
                        else if (band == Warm) chance -= 15;
                    }
                    break;
                case LobbyAsk.Target:
                    if (subjectId != null && subjectId != deciderId)
                    {
                        About(s, e, deciderId, subjectId, out string band, out bool pact);
                        if (pact) chance -= 40;
                        if (band == Cold) chance += 20;
                        else if (band == Warm) chance -= 30;
                    }
                    break;
                case LobbyAsk.Save:
                    if (subjectId != null && subjectId != s.playerId && subjectId != deciderId)
                    {
                        About(s, e, deciderId, subjectId, out string band, out _);
                        if (band == Warm) chance += 20;
                        else if (band == Cold) chance -= 20;
                    }
                    break;
                case LobbyAsk.Keep:
                    bool keptAlly = false;
                    foreach (string nominee in s.nominees.Where(id => id != deciderId))
                    {
                        About(s, e, deciderId, nominee, out _, out bool pact);
                        keptAlly |= pact;
                    }
                    if (keptAlly) chance -= 30;
                    break;
            }
            return Finish(e, chance);
        }

        // ---------------------------------------------------------------- what the player has

        /// <summary>
        /// How little the player has on this houseguest, before anything an ask is about: <see cref="Many"/>
        /// with no current read, no claim about the vote and little history; <see cref="Few"/> with
        /// a current read and a history; <see cref="Some"/> between.
        /// </summary>
        public static string Unknowns(EpisodeState s, string npcId) =>
            s?.Find(npcId) == null ? Many : Level(HasRead(s, npcId), HasClaim(s, npcId), History(s, npcId), true);

        /// <summary>
        /// How the houseguest sees the player, as far as the player can tell: the player's own
        /// reading of them, held inside the band a current read put them in. The band is all a read
        /// showed ("warm on you", "not sure about you", "cold on you"), so the number behind it is
        /// never used; without a current read, the player's own reading is all there is.
        /// </summary>
        public static double PresumedView(EpisodeState s, string npcId)
        {
            double own = s.Score(s.playerId, npcId);
            switch (Band(s, npcId, s.playerId))
            {
                case Warm: return Math.Max(own, WarmLine);
                case Cold: return Math.Min(own, ColdLine);
                case Unsure: return Math.Max(ColdLine + 1, Math.Min(WarmLine - 1, own));
                default: return own;
            }
        }

        /// <summary>
        /// The band the player last learned for how one houseguest stands toward another - by a
        /// read, an account or an overheard conversation, within the vote read's shelf life - or
        /// null when there is none current. Only the band: it is what the player was told.
        /// </summary>
        public static string Band(EpisodeState s, string fromId, string toId)
        {
            var row = s?.ledger?.standings?.LastOrDefault(r => r.fromId == fromId && r.toId == toId && Learned(r.source)
                && r.week >= s.week - VoteRead.StandingShelfLife);
            if (row == null) return null;
            return row.score >= WarmLine ? Warm : row.score <= ColdLine ? Cold : Unsure;
        }

        /// <summary>
        /// Whether the player knows of a pact holding these two. One the player is in counts while
        /// it stands, which the player is told; anybody else's counts once the player knows of it
        /// (<see cref="FinalistRead.AllianceCertainty"/>), however it has ended since, because it
        /// ends on scores the player never sees and saying so would be the leak this closes.
        /// </summary>
        public static bool KnownPact(EpisodeState s, string a, string b) =>
            s?.alliances != null && !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && a != b
            && s.alliances.Any(x => x.members.Contains(a) && x.members.Contains(b)
                && (x.members.Contains(s.playerId) ? x.active : FinalistRead.AllianceCertainty(s, x) != null));

        /// <summary>Whether the player holds a current read of how this houseguest sees them.</summary>
        public static bool HasRead(EpisodeState s, string npcId) => Band(s, npcId, s?.playerId) != null;

        /// <summary>Whether this houseguest has ever said, or been overheard saying, how they vote.</summary>
        public static bool HasClaim(EpisodeState s, string npcId) =>
            s?.ledger?.claims != null && s.ledger.claims.Any(k => k.voterId == npcId);

        /// <summary>
        /// The records between the player and this houseguest that the notes page draws, past the
        /// reads and claims counted on their own: their word and the player's, offers either way,
        /// what they came to the player with, the player's calls they answered, shared pacts, and
        /// what the player remembers of them.
        /// </summary>
        public static int History(EpisodeState s, string npcId)
        {
            if (s == null || string.IsNullOrEmpty(npcId)) return 0;
            string me = s.playerId;
            bool Pair(string a, string b) => (a == npcId && b == me) || (a == me && b == npcId);
            return s.promises.Count(p => Pair(p.fromId, p.toId))
                + s.deals.Count(d => Pair(d.proposerId, d.recipientId))
                + (s.ledger?.replies?.Count(r => r.fromId == npcId) ?? 0)
                + (s.ledger?.calls?.Count(c => c.callerId == me && (c.followed.Contains(npcId) || c.defected.Contains(npcId))) ?? 0)
                + s.alliances.Count(a => a.members.Contains(me) && a.members.Contains(npcId))
                + s.memories.Count(m => m.ownerId == me && m.subjectId == npcId && !string.IsNullOrEmpty(m.text));
        }

        // ---------------------------------------------------------------- the parts

        private static Estimate Start(EpisodeState s, string npcId) => new Estimate
        {
            read = HasRead(s, npcId), claim = HasClaim(s, npcId), history = History(s, npcId),
        };

        /// <summary>What the player knows of where a houseguest stands with somebody else: a band, a pact, or neither.</summary>
        private static void About(EpisodeState s, Estimate e, string fromId, string aboutId, out string band, out bool pact)
        {
            band = Band(s, fromId, aboutId);
            pact = KnownPact(s, fromId, aboutId);
            if (band == null && !pact) e.aboutKnown = false;
        }

        private static Estimate Finish(Estimate e, double chance)
        {
            e.chance = Math.Max(PlayerDeals.MinimumChance, Math.Min(PlayerDeals.MaximumChance, chance));
            e.word = Word(e.chance);
            e.unknowns = Level(e.read, e.claim, e.history, e.aboutKnown);
            return e;
        }

        private static string Level(bool read, bool claim, int history, bool aboutKnown)
        {
            bool little = history < HistoryEnough;
            if (!read && !claim && little) return Many;
            return read && !little && aboutKnown ? Few : Some;
        }

        /// <summary>A standing the player learned something from: a read, an account, an overheard conversation, an ally's word. Never an attempt, never a juror's parting view.</summary>
        private static bool Learned(string source) =>
            source == ClaimSource.Read || source == ClaimSource.Told || source == ClaimSource.Overheard || source == ClaimSource.Ally;

        private static bool Has(ContestantState npc, string trait) =>
            npc.traits != null && npc.traits.Any(t => string.Equals(t, trait, StringComparison.OrdinalIgnoreCase));

        /// <summary>JavaScript's <c>Math.round</c>, as the plea's formula rounds: halves go up.</summary>
        private static double JsRound(double value) => Math.Floor(value + 0.5);
    }
}
