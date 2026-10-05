using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Your word in the house (ACTIONS-DEALS-ALLIANCES-PLAN C8, decision 14): what the house has heard
    /// of the player going back on their word, and what that costs them at the deal table. A public
    /// reading - the player sees it, on the Your word page and in the note over a conversation's odds -
    /// built only from breaches the house saw.
    ///
    /// <para><b>Its inputs.</b> A deal the player breaks by an act the house watches - a nomination or
    /// a replacement, the veto meeting, the final choice - is written as a <see cref="FactKinds.BrokenWord"/>
    /// fact (<see cref="Knowledge.BrokenWord"/>): the player its actor, the one wronged its subject, the
    /// deal its reference, out as a whisper and known at first to the two of them. The house's own
    /// gossip carries it from there (<see cref="Knowledge.Spread"/>: one more houseguest at a time, on
    /// keyed odds, never the season's stream). A deal broken by the player's ballot writes nothing - the
    /// house votes in secret, and a breach a ballot decided never becomes house knowledge this way
    /// (<see cref="KnownBallots.SettledByABallot"/>) - and a voting bloc both walked away from names no
    /// breaker. The reading reads those facts and nothing else: never the deals record, never a
    /// houseguest's view of the player, never their private record of them. A private fact never counts.</para>
    ///
    /// <para><b>It changes only with a line the player hears.</b> Its first knowers are the two of them,
    /// and the settlement tells the player the breach ("You broke a safety pact with Alex Reed."); every
    /// houseguest the gossip reaches after that is told to the player in a whispered line saying who
    /// heard what (<see cref="HeardLine"/>), as that houseguest thinks less of them (<see cref="HeardImpact"/>).
    /// Nothing else moves it: somebody who heard and has since left the house still heard (the knowers
    /// stay), an eviction from the house's list of facts never drops one (X14, <see cref="Knowledge.MakeRoom"/>),
    /// and no other writer touches a fact of this kind.</para>
    ///
    /// <para><b>What it costs, bounded.</b> Every houseguest who has heard of a breach is a hearing
    /// (<see cref="Hearings"/>), up to <see cref="WholeHouse"/> a breach: a breach that many have heard
    /// of is the house's, and weighs at the table what any breach of the player's weighed before
    /// (<see cref="PlayerDeals.BrokenDealPenalty"/>), <see cref="PerHearing"/> a hearing. All of it
    /// together is at most <see cref="Most"/>, three breaches' worth (<see cref="Cost"/>). It is a term of
    /// the acceptance roll (<see cref="PlayerDeals.WordPenalty"/>) and, since the player sees the
    /// reading, of the odds they are shown (<see cref="KnownOdds.Deal"/>) - the same term in both. That
    /// roll is the deal table's, and the alliance invitation's that 'Propose an alliance' and a bring-in
    /// draw. A plea's deal, a veto for a price and a story's choice strike deals on odds of their own or
    /// on none, which never take it, yet the Your word page can list the deal any of them strikes as one
    /// the player proposed - so the player is told what it weighs on and, by name, what it does not
    /// (<see cref="WeighsOn"/>).</para>
    ///
    /// <para><b>Under what rules.</b> Only under the commitment rules, and only where the house keeps
    /// knowledge - the story's facts and the gossip that carries them (<see cref="StoryRules.Bonds"/>):
    /// <see cref="On"/>. The director starts every season with both. A season with the commitment rules
    /// and without the story's knowledge (a test, a harness's legacy set) holds the player's breaches
    /// against them as C0 did, every one at <see cref="PlayerDeals.BrokenDealPenalty"/>, and a season
    /// without the commitment rules plays exactly as it did.</para>
    ///
    /// <para>Pure and read-only: it neither mutates the state nor draws from its generator.</para>
    /// </summary>
    public static class YourWord
    {
        /// <summary>
        /// Whether the house keeps the player's word as knowledge: the commitment rules this week, and
        /// the story's facts and gossip (<see cref="StoryRules.Bonds"/>).
        /// </summary>
        public static bool On(EpisodeState s) =>
            s != null && EpisodeEngine.CommitmentRulesOn(s) && EpisodeEngine.StoryAt(s, StoryRules.Bonds);

        // ---------------------------------------------------------------- the inputs

        /// <summary>
        /// Whether a fact is one the reading reads: the player's broken word (<see cref="FactKinds.BrokenWord"/>
        /// with the player as its actor), and not a private one.
        /// </summary>
        public static bool IsYours(EpisodeState s, HouseFactState fact) =>
            s != null && fact != null && !string.IsNullOrEmpty(s.playerId) && fact.kind == FactKinds.BrokenWord
            && fact.actorId == s.playerId && fact.visibility != FactVisibility.Private;

        /// <summary>The player's broken word the house knows of, in the order the season wrote it. Empty while the house keeps no knowledge of it (<see cref="On"/>).</summary>
        public static List<HouseFactState> Breaches(EpisodeState s)
        {
            var facts = On(s) && s.story?.facts != null ? s.story.facts.Where(f => IsYours(s, f)).ToList() : new List<HouseFactState>();
            if (!UnifiedCommitments.RulesOn(s)) return facts;
            // Hearings count one actual betrayal, even if several agreement references retain
            // knowledge of it. Only combine knowers actually recorded on its audible facts.
            var incidents = UnifiedCommitmentHistory.Breaches(s);
            string Key(HouseFactState fact) => incidents.FirstOrDefault(incident => incident.ActorId == fact.actorId
                && incident.WrongedId == fact.subjectId && incident.EvidenceIds.Contains(fact.refId))?.EffectKey ?? "fact:" + fact.id;
            return facts.GroupBy(Key, StringComparer.Ordinal).Select(group => {
                var first = group.First();
                var incident = incidents.FirstOrDefault(item => item.EffectKey == group.Key);
                return new HouseFactState { id = first.id, kind = first.kind, actorId = first.actorId, subjectId = first.subjectId,
                    refId = first.refId, visibility = group.Any(f => f.visibility == FactVisibility.Public) ? FactVisibility.Public : first.visibility,
                    week = incident == null ? first.week : CommitmentReferences.FindCanonical(s, incident.EffectOwnerId).settledWeek,
                    knowers = group.SelectMany(f => f.knowers).Distinct(StringComparer.Ordinal).ToList() };
            }).ToList();
        }

        /// <summary>
        /// Who has heard of one breach: every houseguest but the player who knows it - in the house or
        /// since gone, since nobody's knowledge leaves with them - in the house's order. A public fact
        /// is everybody's.
        /// </summary>
        public static List<string> HeardBy(EpisodeState s, HouseFactState fact)
        {
            if (s == null || fact == null) return new List<string>();
            return s.contestants.Where(c => c.id != s.playerId && (fact.visibility == FactVisibility.Public || fact.knowers.Contains(c.id)))
                .Select(c => c.id).ToList();
        }

        /// <summary>Everybody who has heard of any of it, in the house's order.</summary>
        public static List<string> Hearers(EpisodeState s)
        {
            var heard = new HashSet<string>(Breaches(s).SelectMany(f => HeardBy(s, f)), StringComparer.Ordinal);
            return s == null ? new List<string>() : s.contestants.Where(c => heard.Contains(c.id)).Select(c => c.id).ToList();
        }

        // ---------------------------------------------------------------- the reading

        /// <summary>How many houseguests hearing of one breach make it the house's: from then on it weighs what a breach always weighed.</summary>
        public const int WholeHouse = 6;

        /// <summary>What each hearing costs at the deal table: a breach's whole weight, <see cref="PlayerDeals.BrokenDealPenalty"/>, over <see cref="WholeHouse"/>.</summary>
        public const double PerHearing = PlayerDeals.BrokenDealPenalty / WholeHouse;

        /// <summary>The most the reading costs at the table: three breaches' worth.</summary>
        public const double Most = 3 * PlayerDeals.BrokenDealPenalty;

        /// <summary>The hearings the reading counts: each houseguest who has heard of each breach, up to <see cref="WholeHouse"/> a breach.</summary>
        public static int Hearings(EpisodeState s) => Breaches(s).Sum(f => Math.Min(WholeHouse, HeardBy(s, f).Count));

        /// <summary>What the player's word costs them at the table, in points of a deal's chance: <see cref="PerHearing"/> a hearing, at most <see cref="Most"/>.</summary>
        public static double Cost(EpisodeState s) => Math.Min(Most, PerHearing * Hearings(s));

        /// <summary>The reading's words, on its cost: nothing heard, less than a breach's worth, less than two, two or more.</summary>
        public const string Good = "good", Questioned = "questioned", Doubted = "doubted", Broken = "broken";

        /// <summary>The reading in a word: <see cref="Good"/>, <see cref="Questioned"/>, <see cref="Doubted"/> or <see cref="Broken"/>.</summary>
        public static string Word(EpisodeState s)
        {
            double cost = Cost(s);
            if (cost <= 0) return Good;
            if (cost < PlayerDeals.BrokenDealPenalty) return Questioned;
            return cost < 2 * PlayerDeals.BrokenDealPenalty ? Doubted : Broken;
        }

        // ---------------------------------------------------------------- hearing of it

        /// <summary>
        /// How much a houseguest who hears of the player's broken word thinks less of them: the middle of
        /// the reference's −5 to −15 for hearing of a betrayal, which the house's own spread draws
        /// (<see cref="DealResolution.BetrayalFloor"/>), here with no draw. On their view of the player, on
        /// their record (<see cref="HeardType"/>, fading as it always did) and into the player's arc with
        /// them, as talk about the player moves a listener (<c>EpisodeEngine.HeardAbout</c>).
        /// </summary>
        public const double HeardImpact = -10;

        /// <summary>The record a hearing leaves on the listener's view of the player: the house's own for hearing of a betrayal.</summary>
        public const string HeardType = "heard_about_betrayal";

        /// <summary>The opening every line of the house's gossip has.</summary>
        public const string HeardOpening = "Word in the house: ";

        /// <summary>
        /// The line the player hears when the gossip carries their broken word to somebody: who heard
        /// what. "Word in the house: Maya Hassan heard you went back on your safety deal with Alex Reed."
        /// </summary>
        public static string HeardLine(EpisodeState s, HouseFactState fact, string listenerId) =>
            HeardOpening + Name(s, listenerId) + " heard you went back on your " + What(s, fact, false) + ".";

        // ---------------------------------------------------------------- what the player is shown

        /// <summary>What the reading says while the house has heard of no breach of the player's.</summary>
        public const string NothingHeard = "The house has heard of no deal you broke in front of it.";

        /// <summary>
        /// What the reading weighs on, as the player is told it, and what it does not: "every deal and
        /// alliance you propose, but not on a plea, a price for the veto or a story's choice". It weighs on
        /// the deal table's rows (and so on the counter a refused one may bring, which comes only where its
        /// chance was near yes) and on the alliance invitation's roll, which 'Propose an alliance' and "Bring
        /// Maya into The Riley Pact" draw. The plea's 'Make a deal' and a veto for a price strike deals too,
        /// on odds the reading never touches, and so does a story's choice - 'Make the pitch', 'Our secret' -
        /// in a conversation or out of one, on the story's own odds (<see cref="StoryOdds"/>) or on none. The
        /// Your word page can list the deal any of them strikes as one "you proposed"
        /// (<see cref="CommitmentsRead"/>): so each is named, never left to a caption's verb. The note over
        /// a conversation's odds names the first two alone (<paramref name="page"/> false): a story's choice
        /// is never drawn under it, since a beat a conversation raised is the conversation until it is
        /// answered.
        /// </summary>
        private static string WeighsOn(bool page) =>
            "every deal and alliance you propose, but not on a plea" + (page ? ", a price for the veto or a story's choice" : " or a price for the veto");

        /// <summary>The reading as the Your word page heads it: "Your word is doubted".</summary>
        public static string Title(EpisodeState s) => "Your word is " + Word(s);

        /// <summary>
        /// The reading in a sentence: that the house has heard of no breach in front of it, or who has heard
        /// and what it does (<see cref="WeighsOn"/>). "3 houseguests have heard of you going back on your word:
        /// Alex, Maya and Jo. The further it spreads, the more it weighs on every deal and alliance you
        /// propose, but not on a plea, a price for the veto or a story's choice." The first never claims
        /// nobody knows of a breach at all: one a ballot decided is the player's and the one it was broken
        /// against, and the page's SETTLED list says it was broken.
        /// </summary>
        public static string Summary(EpisodeState s)
        {
            var heard = Hearers(s);
            if (heard.Count == 0) return NothingHeard;
            return Many(heard.Count) + " heard of you going back on your word: " + Join(heard.Select(id => First(s, id)).ToList())
                + ". The further it spreads, the more it weighs on " + WeighsOn(true) + ".";
        }

        /// <summary>
        /// A line for each breach the house knows of, as the Your word page lists them: "Your safety deal
        /// with Alex, broken in week 3 · known to Alex and Maya".
        /// </summary>
        public static List<string> Lines(EpisodeState s) =>
            Breaches(s).Select(f => "Your " + What(s, f, true) + ", broken in week " + f.week + " · known to "
                + Join(HeardBy(s, f).Select(id => First(s, id)).ToList())).ToList();

        /// <summary>
        /// What follows the note over a table of chances once anybody has heard (null before): the reading,
        /// who has heard, and what it weighs on and does not (<see cref="WeighsOn"/>; no story's choice is
        /// ever drawn under the note, so it names the plea and the veto's price alone). "Your word is
        /// doubted: 4 houseguests have heard of you going back on it, and it weighs on every deal and
        /// alliance you propose, but not on a plea or a price for the veto."
        /// </summary>
        public static string OddsLine(EpisodeState s)
        {
            if (Cost(s) <= 0) return null;
            return Title(s) + ": " + Many(Hearers(s).Count) + " heard of you going back on it, and it weighs on " + WeighsOn(false) + ".";
        }

        // ---------------------------------------------------------------- the parts

        /// <summary>The broken deal and who it was with: "safety deal with Alex Reed", or with a first name for the page.</summary>
        private static string What(EpisodeState s, HouseFactState fact, bool first)
        {
            var deal = s == null ? null : CommitmentReferences.FindDeal(s, fact.refId);
            string with = first ? First(s, fact.subjectId) : Name(s, fact.subjectId);
            if (first && UnifiedCommitments.RulesOn(s))
            {
                var incident = UnifiedCommitmentHistory.Breaches(s).FirstOrDefault(item => item.ActorId == fact.actorId
                    && item.WrongedId == fact.subjectId && item.EvidenceIds.Contains(fact.refId));
                if (incident != null && s.story.facts.Where(item => IsYours(s, item) && item.actorId == fact.actorId
                    && item.subjectId == fact.subjectId && incident.EvidenceIds.Contains(item.refId))
                    .Select(item => CommitmentReferences.FindCanonical(s, item.refId)?.sourcePolicy).Distinct().Count() > 1)
                    return "word of safety to " + with;
            }
            if (deal == null && s != null && CommitmentReferences.FindCanonical(s, fact.refId)?.sourcePolicy == UnifiedCommitments.PromisePolicy)
                return "promise of safety to " + with;
            return deal == null ? "word to " + with : CommitmentsRead.DealNoun(deal.type) + " with " + with;
        }

        private static string Many(int n) => n == 1 ? "1 houseguest has" : n + " houseguests have";

        private static string Name(EpisodeState s, string id) => s?.Find(id)?.name ?? "somebody";

        private static string First(EpisodeState s, string id)
        {
            var who = s?.Find(id);
            return who == null ? "somebody" : FinalistRead.FirstName(who.name);
        }

        /// <summary>"a", "a and b", "a, b and c".</summary>
        private static string Join(IList<string> items)
        {
            if (items.Count == 0) return "nobody";
            if (items.Count == 1) return items[0];
            return string.Join(", ", items.Take(items.Count - 1)) + " and " + items[items.Count - 1];
        }
    }
}
