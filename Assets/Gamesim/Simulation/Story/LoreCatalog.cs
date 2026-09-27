using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The lore catalogue: authored sheets for the fictional roster, legacy sheets for alumni, and
    /// the trait pools every other houseguest's sheet is derived from.
    /// </summary>
    public static partial class Lore
    {
        /// <summary>Authored sheets, keyed by template id. Written in <c>LoreSheets.cs</c>.</summary>
        public static readonly Dictionary<string, Sheet> Authored = Index(AuthoredSheets());

        /// <summary>Legacy sheets for identity-intact alumni, keyed by template id. Written in <c>LoreLegacy.cs</c>.</summary>
        public static readonly Dictionary<string, Sheet> Legacy = Index(LegacySheets());

        private static Dictionary<string, Sheet> Index(IEnumerable<Sheet> sheets) =>
            sheets.ToDictionary(s => s.key, StringComparer.Ordinal);

        /// <summary>
        /// A sheet built from a houseguest's own card and their traits: what they told the house
        /// about themselves, and how they behave. Nothing invented about family, romance or a past
        /// the player did not write.
        /// </summary>
        public static Sheet Derived(ContestantState who)
        {
            if (who == null) return null;
            var facts = new List<Fact>();
            string key = "derived:" + who.id;
            string primary = who.traits?.FirstOrDefault(t => Pools.ContainsKey(t));
            string secondary = who.traits?.Skip(1).FirstOrDefault(t => Pools.ContainsKey(t));
            if (!string.IsNullOrWhiteSpace(who.bio))
                facts.Add(new Fact { id = key + ":told", facet = Facets.Origin, depth = 1,
                    text = "What they told you about themselves: \"" + who.bio.Trim() + "\"" });
            if (!string.IsNullOrWhiteSpace(who.occupation) || !string.IsNullOrWhiteSpace(who.hometown))
                facts.Add(new Fact { id = key + ":work", facet = Facets.Work, depth = 1,
                    text = Join("Works as " + Article(who.occupation), who.hometown == null ? null : "from " + who.hometown) });
            foreach (var trait in new[] { primary, secondary }.Where(t => t != null).Distinct())
                foreach (var fact in Pools[trait])
                    facts.Add(new Fact
                    {
                        id = key + ":" + trait.ToLowerInvariant() + ":" + fact.id, facet = fact.facet, depth = fact.depth, text = fact.text,
                        approach = fact.approach, sign = fact.sign, topic = fact.topic,
                    });
            return new Sheet
            {
                key = key, source = LoreSources.Derived, primaryTrait = primary,
                romance = null, conflictStyle = primary == null ? null : ConflictStyleFor(primary),
                facts = facts.GroupBy(f => f.id).Select(g => g.First()).Take(16).ToArray(),
            };
        }

        private static string Article(string occupation)
        {
            if (string.IsNullOrWhiteSpace(occupation)) return null;
            string word = occupation.Trim();
            return ("aeiou".IndexOf(char.ToLowerInvariant(word[0])) >= 0 ? "an " : "a ") + word.ToLowerInvariant();
        }

        private static string Join(string first, string second)
        {
            if (string.IsNullOrEmpty(first)) return second == null ? null : char.ToUpperInvariant(second[0]) + second.Substring(1) + ".";
            return second == null ? first + "." : first + ", " + second + ".";
        }

        private static string ConflictStyleFor(string trait)
        {
            switch (trait)
            {
                case "Confrontational": case "Impulsive": case "Stubborn": return "loud";
                case "Analytical": case "Strategic": case "Introverted": case "Sneaky": return "cold";
                case "Emotional": case "Loyal": return "hurt";
                default: return "even";
            }
        }

        /// <summary>
        /// The trait pools: behaviour, never biography. Each trait gives a respects fact (its
        /// primary-trait answer on the jury matches it), a hot-button and a tendency. Depths are
        /// shallow because behaviour is what a house sees first.
        /// </summary>
        public static readonly Dictionary<string, Fact[]> Pools = new Dictionary<string, Fact[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "Competitive", new[] {
                F("respects", Facets.Respects, 2, "Respects anyone who beats them fair and square.", Personality.Approach.Bold, 1),
                F("hot", Facets.HotButton, 2, "Hates being told a win was luck.", Personality.Approach.Playful, -1, "competition"),
                F("tend", Facets.Tendency, 1, "Keeps count of every competition, including the ones nobody else remembers.") } },
            { "Strategic", new[] {
                F("respects", Facets.Respects, 2, "Respects a move with a reason behind it.", Personality.Approach.Calculated, 1),
                F("hot", Facets.HotButton, 2, "Can't stand a plan made on feelings.", Personality.Approach.Warm, -1, "game"),
                F("tend", Facets.Tendency, 1, "Answers a question with a question when the game comes up.") } },
            { "Loyal", new[] {
                F("respects", Facets.Respects, 2, "Respects someone who keeps their word when it costs them.", Personality.Approach.Candid, 1),
                F("hot", Facets.HotButton, 2, "Takes a broken promise personally, every time.", Personality.Approach.Hardball, -1, "loyalty"),
                F("tend", Facets.Tendency, 1, "Checks on the people they have promised things to.") } },
            { "Emotional", new[] {
                F("respects", Facets.Respects, 2, "Respects someone who owns it when they hurt them.", Personality.Approach.Candid, 1),
                F("hot", Facets.HotButton, 2, "Hates being called too emotional to play.", Personality.Approach.Calculated, -1, "feelings"),
                F("tend", Facets.Tendency, 1, "Wears the day on their face before they say a word.") } },
            { "Social", new[] {
                F("respects", Facets.Respects, 2, "Respects someone who makes the room easier to be in.", Personality.Approach.Warm, 1),
                F("hot", Facets.HotButton, 2, "Hates being left out of a conversation they can see happening.", Personality.Approach.Yield, -1, "exclusion"),
                F("tend", Facets.Tendency, 1, "Knows where everybody is sitting at dinner and why.") } },
            { "Funny", new[] {
                F("respects", Facets.Respects, 2, "Respects anyone who can laugh at themselves.", Personality.Approach.Playful, 1),
                F("hot", Facets.HotButton, 2, "Shuts down when a joke is treated as a confession.", Personality.Approach.Hardball, -1, "jokes"),
                F("tend", Facets.Tendency, 1, "Turns a tense room with a bit before anybody notices it was tense.") } },
            { "Charming", new[] {
                F("respects", Facets.Respects, 2, "Respects someone who plays along.", Personality.Approach.Playful, 1),
                F("hot", Facets.HotButton, 2, "Hates being told their charm is an act.", Personality.Approach.Candid, -1, "sincerity"),
                F("tend", Facets.Tendency, 1, "Remembers everybody's name, and the name of their dog.") } },
            { "Manipulative", new[] {
                F("respects", Facets.Respects, 2, "Respects someone who can keep a straight face.", Personality.Approach.Calculated, 1),
                F("hot", Facets.HotButton, 2, "Hates being read out loud.", Personality.Approach.Candid, -1, "exposure"),
                F("tend", Facets.Tendency, 1, "Always seems to know who said what first.") } },
            { "Analytical", new[] {
                F("respects", Facets.Respects, 2, "Respects a reasoned move, even one against them.", Personality.Approach.Calculated, 1),
                F("hot", Facets.HotButton, 2, "Hates being rushed into a decision.", Personality.Approach.Bold, -1, "pressure"),
                F("tend", Facets.Tendency, 1, "Keeps a mental tally of every vote since week one.") } },
            { "Impulsive", new[] {
                F("respects", Facets.Respects, 2, "Respects someone who just goes for it.", Personality.Approach.Bold, 1),
                F("hot", Facets.HotButton, 2, "Hates a lecture about thinking things through.", Personality.Approach.Calculated, -1, "caution"),
                F("tend", Facets.Tendency, 1, "Decides before the question is finished.") } },
            { "Deceptive", new[] {
                F("respects", Facets.Respects, 2, "Respects someone who never shows their cards.", Personality.Approach.Calculated, 1),
                F("hot", Facets.HotButton, 2, "Goes quiet when asked for a straight answer.", Personality.Approach.Hardball, -1, "honesty"),
                F("tend", Facets.Tendency, 1, "Tells a slightly different story to each person who asks.") } },
            { "Introverted", new[] {
                F("respects", Facets.Respects, 2, "Respects someone who knows when to leave them alone.", Personality.Approach.Yield, 1),
                F("hot", Facets.HotButton, 2, "Hates being put on the spot in front of the house.", Personality.Approach.Bold, -1, "attention"),
                F("tend", Facets.Tendency, 1, "Disappears to the quietest room in the house after every ceremony.") } },
            { "Stubborn", new[] {
                F("respects", Facets.Respects, 2, "Respects someone who holds their ground.", Personality.Approach.Hardball, 1),
                F("hot", Facets.HotButton, 2, "Digs in harder the moment they are told they are wrong.", Personality.Approach.Candid, -1, "being wrong"),
                F("tend", Facets.Tendency, 1, "Has never once changed their mind in public.") } },
            { "Flexible", new[] {
                F("respects", Facets.Respects, 2, "Respects someone who can change plans without a scene.", Personality.Approach.Yield, 1),
                F("hot", Facets.HotButton, 2, "Hates being pinned to an ultimatum.", Personality.Approach.Hardball, -1, "ultimatums"),
                F("tend", Facets.Tendency, 1, "Goes with the house until the house is wrong.") } },
            { "Intuitive", new[] {
                F("respects", Facets.Respects, 2, "Respects someone who trusts their gut.", Personality.Approach.Warm, 1),
                F("hot", Facets.HotButton, 2, "Hates being asked for proof of a feeling.", Personality.Approach.Calculated, -1, "proof"),
                F("tend", Facets.Tendency, 1, "Reads a room the second they walk into it.") } },
            { "Sneaky", new[] {
                F("respects", Facets.Respects, 2, "Respects someone who can keep a secret.", Personality.Approach.Calculated, 1),
                F("hot", Facets.HotButton, 2, "Hates being caught listening.", Personality.Approach.Hardball, -1, "being caught"),
                F("tend", Facets.Tendency, 1, "Always seems to be in the next room when something is said.") } },
            { "Confrontational", new[] {
                F("respects", Facets.Respects, 2, "Respects someone who says it to their face.", Personality.Approach.Hardball, 1),
                F("hot", Facets.HotButton, 2, "Hates being told to calm down.", Personality.Approach.Yield, -1, "calm down"),
                F("tend", Facets.Tendency, 1, "Would rather have the argument now than the whisper later.") } },
        };

        private static Fact F(string id, string facet, int depth, string text, string approach = null, int sign = 0, string topic = null) =>
            new Fact { id = id, facet = facet, depth = depth, text = text, approach = approach, sign = sign, topic = topic };
    }
}
