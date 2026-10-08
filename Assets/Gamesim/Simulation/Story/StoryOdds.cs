using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The chance a story option works: one pure function, called by the card that shows it and by
    /// the engine that rolls it, so the odds shown are the odds used.
    ///
    /// <para><b>Derived from</b> the web's <c>calculateSuccessChance</c>
    /// (<c>contextual-action-generator.ts:45-67</c>), not at parity with it. Every changed term is
    /// listed in 20 §2.10: the relationship term gains a floor so one bad week cannot zero every
    /// option; trust is dropped, because trust is the NPC's private view and outside what the
    /// player can see; the intense-rivalry term reads a permanent negative receipt the player was
    /// party to rather than an arc; reception, known facts and the player's stress are native.</para>
    ///
    /// <para><b>Only inputs the player can see go in.</b> A hidden hot-button changes the payoff
    /// (<see cref="Lore"/>), never the chance.</para>
    /// </summary>
    public static class StoryOdds
    {
        public const int Floor = 5, Ceiling = 95;

        /// <summary>One visible term of a chance, for "Show the odds".</summary>
        public readonly struct Term
        {
            public readonly string label;
            public readonly int value;
            public Term(string label, int value) { this.label = label; this.value = value; }
        }

        /// <summary>The chance, 5-95, of an option on a beat. −1 when the option is certain.</summary>
        public static int Chance(EpisodeState state, HouseEventState beat, HouseEventChoice choice) =>
            choice == null || choice.checkBase < 0 ? -1 : Clamp(Terms(state, beat, choice).Sum(t => t.value));

        /// <summary>The terms that make up a chance, base first. Empty when the option is certain.</summary>
        public static List<Term> Terms(EpisodeState state, HouseEventState beat, HouseEventChoice choice)
        {
            var terms = new List<Term>();
            if (state == null || choice == null || choice.checkBase < 0) return terms;
            terms.Add(new Term("Base", (int)Math.Round(choice.checkBase)));
            var player = state.Find(state.playerId);
            var subject = state.Find(choice.subjectId);

            if (subject != null && subject.id != state.playerId)
            {
                int standing = (int)Math.Round(Math.Max(-25, Math.Min(25, 0.5 * state.Score(state.playerId, subject.id))));
                if (standing != 0) terms.Add(new Term("your standing", standing));
                if (state.Allied(state.playerId, subject.id)) terms.Add(new Term("you're allied", 15));
                if (state.deals.Any(d => d.status == DealStatus.Active && Pair(d.proposerId, d.recipientId, state.playerId, subject.id))
                    || (UnifiedCommitments.RulesOn(state) && UnifiedCommitments.Binding(state, state.playerId, subject.id)
                        .Any(row => row.sourcePolicy == UnifiedCommitments.DealPolicy)))
                    terms.Add(new Term("a live deal", 10));
                if (state.nominees.Contains(subject.id)) terms.Add(new Term(subject.name + " is on the block", 10));
                if (PermanentBadBlood(state, state.playerId, subject.id)) terms.Add(new Term("bad blood between you", -20));
                if (PlayerBrokeTheirWord(state, subject.id)) terms.Add(new Term("you broke your word", -30));

                int reception = VisibleReception(state, subject, choice.approach);
                if (reception > 0) terms.Add(new Term(ReceptionLabel(subject, choice.approach, true), 10));
                else if (reception < 0) terms.Add(new Term(ReceptionLabel(subject, choice.approach, false), -10));

                int known = Math.Min(2, Lore.KnownRelevant(state, subject.id, choice.approach));
                if (known > 0) terms.Add(new Term("what you know about " + subject.name, 10 * known));
            }

            if (player != null && choice.bonusTraits != null && choice.bonusTraits.Count > 0
                && Personality.Has(player, choice.bonusTraits.ToArray()))
                terms.Add(new Term("you're " + choice.bonusTraits.First(t => Personality.Has(player, t)), 15));

            int stress = Personality.StressStep(player);
            if (stress > 0) terms.Add(new Term("your stress", -5 * stress));
            return terms;
        }

        /// <summary>
        /// How an approach is received as far as the player can tell: from the subject's visible
        /// traits and the lore the player has learned. A hot-button the player has not learned is
        /// not in here - it bites on the payoff instead.
        /// </summary>
        public static int VisibleReception(EpisodeState state, ContestantState subject, string approach)
        {
            if (subject == null || approach == null) return 0;
            int lore = Lore.KnownReception(state, subject.id, approach);
            if (lore != 0) return lore;
            return Personality.Reception(subject, approach);
        }

        /// <summary>"about 7 in 10", which is how the tile says it.</summary>
        public static string InTen(int chance) =>
            chance < 0 ? "certain" : "about " + Math.Max(1, Math.Min(9, (int)Math.Round(chance / 10.0))) + " in 10";

        public static int Clamp(int value) => Math.Max(Floor, Math.Min(Ceiling, value));

        private static string ReceptionLabel(ContestantState subject, string approach, bool resonates)
        {
            string trait = subject.traits?.FirstOrDefault(t => Personality.Reception(
                new ContestantState { traits = new List<string> { t } }, approach) != 0) ?? subject.traits?.FirstOrDefault();
            string name = subject.name;
            return resonates
                ? (trait == null ? name + " likes this approach" : name + " is " + trait)
                : (trait == null ? name + " resists this approach" : name + " is " + trait);
        }

        private static bool Pair(string a, string b, string first, string second) =>
            (a == first && b == second) || (a == second && b == first);

        /// <summary>A permanent negative story receipt either way between the player and them.</summary>
        public static bool PermanentBadBlood(EpisodeState state, string a, string b) =>
            state.relationships.Where(r => (r.fromId == a && r.toId == b) || (r.fromId == b && r.toId == a))
                .SelectMany(r => r.events)
                .Any(e => !e.decayable && e.impactScore < 0 && e.type != null && e.type.StartsWith(StoryReceipts.Prefix, StringComparison.Ordinal));

        /// <summary>
        /// Whether the player broke a deal or a promise with them. Under the commitment rules (C0, X3)
        /// the deal's own record says who broke it (<see cref="Breaches.Broke"/>); before them the
        /// ledger's line was read, which both sides of a breach carried, so a deal the houseguest
        /// broke read as the player's broken word.
        /// </summary>
        public static bool PlayerBrokeTheirWord(EpisodeState state, string npcId) =>
            state.promises.Any(p => p.status == PromiseStatus.Broken && p.fromId == state.playerId && p.toId == npcId)
            || state.deals.Any(d => d.status == DealStatus.Broken && Pair(d.proposerId, d.recipientId, state.playerId, npcId)
                                    && (EpisodeEngine.CommitmentRulesOn(state) ? Breaches.Broke(state, d, state.playerId) : BrokeIt(state, d, state.playerId)))
            || (UnifiedCommitments.RulesOn(state) && UnifiedCommitmentHistory.Breaches(state)
                .Any(incident => incident.ActorId == state.playerId && incident.WrongedId == npcId));

        private static bool BrokeIt(EpisodeState state, DealState deal, string who) =>
            // The ledger records the wronged party's view of whoever broke it (EpisodeEngine.SettleDeals).
            state.relationships.Any(r => r.toId == who && (r.fromId == deal.proposerId || r.fromId == deal.recipientId)
                                         && r.events.Any(e => e.type == "deal_broken"));
    }

    /// <summary>
    /// The story system's ledger marks. One-way, typed <c>story:*</c>, and the only thing later
    /// text is allowed to cite as "between you".
    /// </summary>
    public static class StoryReceipts
    {
        public const string Prefix = "story:";

        // Permanent: acts that changed where somebody stands.
        public const string StoodUpFor = "story:stood-up-for", SoldOut = "story:sold-out";
        public const string SecretKept = "story:secret-kept", SecretExposed = "story:secret-exposed";
        public const string Showmance = "story:showmance", ShowmanceBetrayed = "story:showmance-betrayed";
        public const string PublicBlowup = "story:public-blowup", MadePeace = "story:made-peace";
        // Fading: ordinary social weather.
        public const string HeardOut = "story:heard-out", Snubbed = "story:snubbed", Argued = "story:argued";
        public const string TooNosy = "story:too-nosy", TookIt = "story:took-it";

        public static readonly string[] Permanent =
            { StoodUpFor, SoldOut, SecretKept, SecretExposed, Showmance, ShowmanceBetrayed, PublicBlowup, MadePeace };
        public static readonly string[] Fading = { HeardOut, Snubbed, Argued, TooNosy, TookIt };

        public static bool IsKnown(string type) => type != null
            && (Array.IndexOf(Permanent, type) >= 0 || Array.IndexOf(Fading, type) >= 0);

        /// <summary>A receipt's default size.</summary>
        public static double Impact(string type)
        {
            switch (type)
            {
                case StoodUpFor: return 15;
                case SoldOut: return -20;
                case SecretKept: return 15;
                case SecretExposed: return -25;
                case Showmance: return 15;
                case ShowmanceBetrayed: return -30;
                case PublicBlowup: return -20;
                case MadePeace: return 10;
                case HeardOut: return 5;
                case Snubbed: return -4;
                case Argued: return -6;
                case TooNosy: return -3;
                case TookIt: return 4;
                default: return 0;
            }
        }

        /// <summary>How a receipt reads in "Between you", past tense and second person.</summary>
        public static string Describe(string type, string otherName, bool youDidIt)
        {
            switch (type)
            {
                case StoodUpFor: return youDidIt ? "you stood up for " + otherName : otherName + " stood up for you";
                case SoldOut: return youDidIt ? "you sold " + otherName + " out" : otherName + " sold you out";
                case SecretKept: return youDidIt ? "you kept " + otherName + "'s secret" : otherName + " kept your secret";
                case SecretExposed: return youDidIt ? "you exposed " + otherName + "'s secret" : otherName + " exposed your secret";
                case Showmance: return "you and " + otherName + " stopped pretending";
                case ShowmanceBetrayed: return youDidIt ? "you left " + otherName + " on the block" : otherName + " left you on the block";
                case PublicBlowup: return youDidIt ? "you blew up at " + otherName : otherName + " blew up at you";
                case MadePeace: return "you and " + otherName + " made peace";
                case HeardOut: return youDidIt ? "you heard " + otherName + " out" : otherName + " heard you out";
                case Snubbed: return youDidIt ? "you brushed " + otherName + " off" : otherName + " brushed you off";
                case Argued: return "you and " + otherName + " argued";
                case TooNosy: return youDidIt ? "you pried with " + otherName : otherName + " pried";
                case TookIt: return youDidIt ? "you took it on the chin in front of " + otherName : otherName + " took it on the chin";
                default: return null;
            }
        }
    }
}
