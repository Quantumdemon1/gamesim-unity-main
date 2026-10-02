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
    /// <item>their view of anybody else: the middle of the band the player last learned for the
    /// pair - read, told or overheard - while it is current (<see cref="Band"/>,
    /// <see cref="BandScore"/>), read on the roll's own lines;</item>
    /// <item>a pact with anybody else: only one the player knows of (<see cref="KnownPact"/>),
    /// counted however it ended, because a pact the player is not in ends on scores the player
    /// never sees;</item>
    /// <item>the ledger's trust, the houseguest's private record of the player: left out, read as
    /// neutral;</item>
    /// <item>a grudge, which refuses an alliance under the commitment rules whatever the roll: only
    /// the one the player's own walk-out left, reckoned from what walking out leaves and how it
    /// fades (<see cref="KnownWalkOut"/>).</item>
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
            /// <summary>The chance in percent from the known terms alone, clamped as the roll's is - or nought where a grudge the player knows of refuses the ask (<see cref="grudge"/>). Never the roll's own.</summary>
            public double chance;
            public string word = Unlikely;
            /// <summary><see cref="Few"/>, <see cref="Some"/> or <see cref="Many"/>.</summary>
            public string unknowns = Many;
            /// <summary>What the player has on the houseguest: a current read, a claim about the vote, and how many records between them.</summary>
            public bool read, claim;
            public int history;
            /// <summary>Whether the player knows anything of where the houseguest stands with the third person the ask is about; true when it is about nobody else.</summary>
            public bool aboutKnown = true;
            /// <summary>
            /// Whether the player knows of a grudge that refuses this ask whatever its chance: under the
            /// commitment rules, the one their own walk-out from a pact with the houseguest left
            /// (<see cref="KnownWalkOut"/>). The chance is then nought and the word <see cref="NoChance"/>.
            /// </summary>
            public bool grudge;
        }

        /// <summary>The word for an ask the player knows a grudge refuses: no roll can say yes to it.</summary>
        public const string NoChance = "no chance";

        /// <summary>"about even" rather than "51%", so the chip reads as a judgement and not a promise.</summary>
        public static string Word(double chance)
        {
            if (chance >= 75) return Likely;
            if (chance >= 55) return Favourable;
            if (chance >= 45) return AboutEven;
            if (chance >= 25) return AStretch;
            return Unlikely;
        }

        /// <summary>What a person's card in a picker says of a chance with nothing behind it (<see cref="CardWord"/>).</summary>
        public const string NoRead = "no read";

        /// <summary>
        /// The chance as a person's card in the conversation's deal picker says it (UI-UX-PASS-PLAN
        /// P1): <see cref="NoRead"/> where the estimate has nothing behind it, and its word otherwise.
        ///
        /// <para>Nothing behind it is <see cref="Many"/> unknowns about the houseguest the deal is put
        /// to - no current read, no claim about the vote, little history, which is exactly when the
        /// table above says "Many unknowns" - and nothing known of where they stand with the person the
        /// card names (<see cref="Estimate.aboutKnown"/>). A grid of a dozen target agreements each
        /// reading "about even" at the player's own trust of nought said one guess a dozen times as
        /// if it were a dozen reads (the play sweep's row 15). A card whose person the player does
        /// know something of - a pact they know of, a standing they learned - keeps its word, because
        /// that word carries what the player knows.</para>
        ///
        /// <para>Words only: the estimate, and the roll, are untouched.</para>
        /// </summary>
        public static string CardWord(Estimate e) =>
            e == null || (e.unknowns == Many && !e.aboutKnown) ? NoRead : e.word;

        // ---------------------------------------------------------------- a deal

        /// <summary>
        /// The chance shown beside a proposal to this houseguest: <see cref="PlayerDeals.AcceptanceChance"/>
        /// term for term and line for line, in its order, each hidden term swapped for what the
        /// player has. Where the player knows every term the roll reads, the two are equal; a
        /// test holds them so (KnownOddsTests), so a change to the roll that is not made here fails.
        /// </summary>
        public static Estimate Deal(EpisodeState s, string npcId, string type, string aboutId)
        {
            var npc = s?.Find(npcId);
            if (npc == null) return new Estimate();
            var e = Start(s, npcId);
            double relationship = PresumedView(s, npcId);

            double chance = PlayerDeals.RelationshipChance(relationship);

            if (type == DealKind.InformationSharing || type == DealKind.Partnership) chance += 10;
            else if (type == DealKind.FinalTwo || type == DealKind.VetoUse || type == DealKind.AllianceInvite) chance -= 10;

            // The ledger's trust is the houseguest's own record of the player: left out, as neutral.
            // Deals the player has broken are the player's own record, and count as the roll counts them
            // - under the commitment rules only the ones they broke, which are their own acts (C0, X3).
            chance -= PlayerDeals.BrokenDealPenalty * NpcDeals.BrokenDeals(s, s.playerId);

            // The roll asks whether the ally's own commitment holds (Allegiance.Holds); the player's odds
            // ask it as the player knows it, never of a betrayal or a view they cannot see (C2, C3).
            bool allied = KnownPact(s, npcId, s.playerId) && Allegiance.HoldsAsKnown(s, npcId);
            if (allied)
            {
                chance += 20;
                if (type == DealKind.SafetyAgreement || type == DealKind.VoteTogether || type == DealKind.FinalTwo)
                    chance += 10;
            }

            if (type == DealKind.TargetAgreement && aboutId != null)
            {
                if (KnownPact(s, npcId, aboutId)) chance -= 40;
                double? toTarget = Toward(s, e, npcId, aboutId);
                if (toTarget < -20) chance += 20;
                else if (toTarget > 30) chance -= 30;
            }

            chance += PlayerDeals.TraitModifier(npc.traits, type);

            bool nominated = !s.evictionResolved && s.nominees.Contains(npcId);
            if (type == DealKind.SafetyAgreement && nominated) chance += 25;
            if (type == DealKind.VoteTogether && nominated) chance += 35;

            if (type == DealKind.VoteSave)
            {
                if (aboutId != null)
                {
                    double? toTarget = Toward(s, e, npcId, aboutId);
                    if (toTarget >= 50) chance += 20;
                    else if (toTarget < 0) chance -= 20;
                }
                else if (nominated) chance -= 10;
            }

            if (type == DealKind.VoteEvict && aboutId != null)
            {
                double? toTarget = Toward(s, e, npcId, aboutId);
                if (toTarget < -20) chance += 25;
                else if (toTarget > 50) chance -= 30;
                if (Has(npc, "Loyal") && relationship >= 80) chance += 15;
                if (Has(npc, "Strategic")) chance += 10;
            }

            if (type == DealKind.FinalTwo)
            {
                if (relationship < 50) chance -= 25;
                if (s.Active.Count() > NpcDeals.EndgameSize && relationship < 80) chance -= 15;
            }

            if (type == DealKind.Partnership && allied) chance += 15;

            Finish(e, chance);
            // Under the commitment rules a grudge of forty or more refuses an alliance whatever the
            // roll (C4), and the player knows of the one their own walk-out left (KnownWalkOut).
            if (type == DealKind.AllianceInvite && KnownWalkOut(s, npcId))
            {
                e.grudge = true;
                e.chance = 0;
                e.word = NoChance;
            }
            return e;
        }

        // ---------------------------------------------------------------- an alliance

        /// <summary>
        /// The chance shown beside 'Propose an alliance' under the commitment rules
        /// (ACTIONS-DEALS-ALLIANCES-PLAN C4): the alliance invitation's, worked out as the deal table
        /// works it out (<see cref="Deal"/>), because the invitation's roll is the one a proposal draws
        /// (<see cref="EpisodeEngine.AllianceChance"/>). Where the player knows every term that roll
        /// reads, the two are equal.
        ///
        /// <para>A grudge of forty or more refuses whatever the chance. One such grudge the player can
        /// know of, because their own act wrote it: walking out of a pact leaves every other member
        /// holding <see cref="EpisodeEngine.AllianceLeftGrudge"/> against them, fading two a week. While
        /// that reckoning stays at the line, the shown chance is nought, said <see cref="NoChance"/>
        /// (<see cref="KnownWalkOut"/>). Any other grudge - for a nomination, a broken word, a story's
        /// falling-out - is the houseguest's own and never shown, and a walk-out's grudge a story has
        /// since eased still reads as the player reckons it.</para>
        /// </summary>
        public static Estimate Alliance(EpisodeState s, string npcId) => Deal(s, npcId, DealKind.AllianceInvite, null);

        /// <summary>
        /// Whether the player knows this houseguest still holds what the player's own walk-out from a
        /// pact with them left: under the commitment rules and where grudges exist, a pact of the
        /// player's with them that the player left, as the alliances page says it ("You left it.",
        /// <see cref="AllianceRead.YouLeft"/>, from the player's own line while the log holds it),
        /// whose <see cref="EpisodeEngine.AllianceLeftGrudge"/>, less <see cref="Grudges.DecayPerWeek"/>
        /// a week since, is still at <see cref="EpisodeEngine.AllianceGrudgeLine"/> or more.
        /// </summary>
        public static bool KnownWalkOut(EpisodeState s, string npcId)
        {
            if (s?.alliances == null || string.IsNullOrEmpty(npcId) || npcId == s.playerId) return false;
            if (!EpisodeEngine.CommitmentRulesOn(s) || !EpisodeEngine.StoryAt(s, StoryRules.Grudges)) return false;
            // The cheap test first: an ended pact holding the two of them at all.
            if (!s.alliances.Any(a => a != null && !a.active && a.members != null && a.members.Contains(s.playerId) && a.members.Contains(npcId)))
                return false;
            int left = AllianceRead.Yours(s)
                .Where(p => !p.active && p.ended == AllianceRead.YouLeft && p.endedWeek > 0 && p.members.Any(m => m.id == npcId))
                .Select(p => p.endedWeek).DefaultIfEmpty(0).Max();
            return left > 0
                && EpisodeEngine.AllianceLeftGrudge - Grudges.DecayPerWeek * (s.week - left) >= EpisodeEngine.AllianceGrudgeLine;
        }

        // ---------------------------------------------------------------- a plea

        /// <summary>
        /// The chance shown beside one way of putting a plea: <see cref="StrategyRules.Chance"/> term
        /// for term and line for line, each hidden term swapped for what the player has. Held equal
        /// to the roll where the player knows every term, as <see cref="Deal"/> is.
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
                    if (KnownPact(s, deciderId, s.playerId) && Allegiance.HoldsAsKnown(s, deciderId)) chance += 15;
                    string other = s.nominees.FirstOrDefault(id => id != s.playerId);
                    if (other != null && KnownPact(s, deciderId, other)) chance -= 20;
                    else if (other != null && Toward(s, e, deciderId, other) > 30) chance -= 15;
                    break;
                case LobbyAsk.Target:
                    if (KnownPact(s, deciderId, subjectId)) chance -= 40;
                    double? toTarget = Toward(s, e, deciderId, subjectId);
                    if (toTarget < -20) chance += 20;
                    else if (toTarget > 30) chance -= 30;
                    break;
                case LobbyAsk.Save:
                    if (subjectId != s.playerId)
                    {
                        double? toNominee = Toward(s, e, deciderId, subjectId);
                        if (toNominee >= 50) chance += 20;
                        else if (toNominee < 0) chance -= 20;
                    }
                    break;
                case LobbyAsk.Keep:
                    // The roll's EpisodeState.Allied has no a != b guard, so a veto holder who is
                    // also on the block reads as keeping an ally whenever they are in any active
                    // pact at all (StrategyRules.Chance's Keep line asks Allied(holder, holder)).
                    // KnownPact keeps the same shape, so the shown odds take that term too - but only
                    // for a pact the player knows of.
                    if (s.nominees.Any(id => KnownPact(s, deciderId, id))) chance -= 30;
                    foreach (string nominee in s.nominees.Where(id => id != deciderId)) Note(s, e, deciderId, nominee);
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
        ///
        /// <para>The shape of <see cref="EpisodeState.Allied"/>, which the roll asks, down to its
        /// missing a != b guard: with <paramref name="a"/> and <paramref name="b"/> the same
        /// houseguest it asks whether they are in any pact the player knows of, as Allied asks
        /// whether they are in any pact at all (the Keep plea's quirk, in <see cref="Plea"/>).</para>
        /// </summary>
        public static bool KnownPact(EpisodeState s, string a, string b) =>
            s?.alliances != null && !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b)
            && s.alliances.Any(x => x.members.Contains(a) && x.members.Contains(b)
                && (x.members.Contains(s.playerId) ? x.active : FinalistRead.AllianceCertainty(s, x) != null));

        /// <summary>
        /// A learned band as one score the roll's own comparisons read, so every line stays where
        /// the roll draws it (over 30, at least 50, over 50, under -20, under nought) instead of
        /// each band being mapped onto each term by hand. The score is the middle of what the band
        /// allows on the roll's scale of -100 to 100 (<see cref="WebRules.ClampScore"/>): warm runs
        /// from 25 to 100, so 62.5; not sure lies either side of nought, so 0; cold runs from -100 to
        /// -25, so -62.5. The player was told the band and nothing about where in it the pair stand,
        /// so its middle is the fair guess.
        ///
        /// <para>What it means on each line: warm clears every upper line and cold every lower one;
        /// not sure clears none, though a pair the player heard were "careful with each other" may
        /// sit a point under nought, where the roll's line for saving a nominee falls.</para>
        /// </summary>
        public static double BandScore(string band) =>
            band == Warm ? (WarmLine + ScoreCeiling) / 2 : band == Cold ? (ColdLine + ScoreFloor) / 2 : 0;

        /// <summary>The scale a relationship score lives on: <see cref="WebRules.ClampScore"/>'s.</summary>
        private const double ScoreFloor = -100, ScoreCeiling = 100;

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

        /// <summary>
        /// How one houseguest sees somebody, as a score the roll's comparisons can read, or null when
        /// the player has nothing on it - and no comparison holds for a null, as no term would for a
        /// view the player never learned. The player's presumed view of them when it is the player;
        /// nought when it is themselves or nobody, which is what the roll reads there too; the
        /// middle of a band the player learned; otherwise nothing.
        /// </summary>
        private static double? Toward(EpisodeState s, Estimate e, string fromId, string aboutId)
        {
            Note(s, e, fromId, aboutId);
            if (string.IsNullOrEmpty(aboutId) || aboutId == fromId) return 0;
            if (aboutId == s.playerId) return PresumedView(s, fromId);
            string band = Band(s, fromId, aboutId);
            return band == null ? (double?)null : BandScore(band);
        }

        /// <summary>Marks the estimate when the player knows nothing of where a houseguest stands with the person an ask is about: no band, no pact.</summary>
        private static void Note(EpisodeState s, Estimate e, string fromId, string aboutId)
        {
            if (string.IsNullOrEmpty(aboutId) || aboutId == fromId || aboutId == s.playerId) return;
            if (Band(s, fromId, aboutId) == null && !KnownPact(s, fromId, aboutId)) e.aboutKnown = false;
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
