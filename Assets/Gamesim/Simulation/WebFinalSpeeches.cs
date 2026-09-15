using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Gamesim.Simulation
{
    [Serializable] public sealed class WebSpeechStoryline
    {
        public string title, category;
        public string[] involvedNPCNames = Array.Empty<string>();
    }
    [Serializable] public sealed class WebSpeechDeal { public string partnerName, dealType; }
    [Serializable] public sealed class WebSpeechOath { public string partnerName; }
    [Serializable] public sealed class WebSpeechWins { public int hoh, pov, other; }
    [Serializable] public sealed class WebSpeechContext
    {
        public WebSpeechStoryline[] activeStorylines = Array.Empty<WebSpeechStoryline>();
        public WebSpeechStoryline[] completedStorylines = Array.Empty<WebSpeechStoryline>();
        public WebSpeechDeal[] brokenDeals = Array.Empty<WebSpeechDeal>(), activeDeals = Array.Empty<WebSpeechDeal>();
        public string[] allianceNames = Array.Empty<string>();
        public WebSpeechOath[] loyaltyOaths = Array.Empty<WebSpeechOath>();
        public WebSpeechWins compWins;
        public int week;
    }

    /// <summary>
    /// Offline generateFinalSpeech/appendContextFragments from web speech-generator.ts.
    /// The source has no speech-score side effect. Native callers persist returned text once.
    /// </summary>
    public static class WebFinalSpeeches
    {
        /// <summary>
        /// Native knowledge-safe adapter: actual wins and the speaker's own alliances only.
        /// Source UI uses all alliance names; intentionally do not leak unrelated private alliances.
        /// Arcs are not storylines, and promises are not deals/oaths: missing systems stay empty.
        /// </summary>
        public static string GenerateNative(EpisodeState state, string finalistId, Func<double> nextRoll)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var finalist = state.Find(finalistId);
            if (finalist == null) throw new ArgumentException("Finalist not found.", nameof(finalistId));
            var context = new WebSpeechContext
            {
                allianceNames = state.alliances.Where(alliance => alliance.members.Contains(finalistId))
                    .Select(alliance => string.IsNullOrEmpty(alliance.name) ? "unnamed alliance" : alliance.name).ToArray(),
                compWins = new WebSpeechWins { hoh = finalist.hohWins, pov = finalist.vetoWins, other = 0 }, week = state.week
            };
            return Generate(finalist.traits, finalist.name, context, nextRoll);
        }

        public static string Generate(IReadOnlyList<string> traits, string name, WebSpeechContext context, Func<double> nextRoll)
        {
            if (traits == null) throw new ArgumentNullException(nameof(traits));
            if (nextRoll == null) throw new ArgumentNullException(nameof(nextRoll));
            string flavor = TraitFlavor(traits);
            string speech = Pick(WebFinalSpeechCatalog.Finale[flavor], nextRoll);
            if (context == null) return speech;

            var stories = (context.activeStorylines ?? Array.Empty<WebSpeechStoryline>()).Concat(
                (context.completedStorylines ?? Array.Empty<WebSpeechStoryline>()).Select(story =>
                    new WebSpeechStoryline { title = story.title, category = story.category, involvedNPCNames = Array.Empty<string>() })).ToArray();
            if (stories.Length > 0 && Roll(nextRoll) < 0.7)
            {
                var story = Pick(stories, nextRoll);
                var pool = story.category != null && WebFinalSpeechCatalog.Stories.TryGetValue(story.category, out var known)
                    ? known : WebFinalSpeechCatalog.Stories["social_drama"];
                string fragment = ReplaceSourceToken(Pick(pool, nextRoll), "{storyline}", story.title);
                string npc = story.involvedNPCNames == null || story.involvedNPCNames.Length == 0
                    || string.IsNullOrEmpty(story.involvedNPCNames[0]) ? "them" : story.involvedNPCNames[0];
                speech += ReplaceSourceToken(fragment, "{npc}", npc);
            }
            if (context.brokenDeals != null && context.brokenDeals.Length > 0 && Roll(nextRoll) < 0.5)
                speech += Pick(WebFinalSpeechCatalog.Betrayal, nextRoll);
            if (context.activeDeals != null && context.activeDeals.Length > 0 && Roll(nextRoll) < 0.4)
                speech += Pick(new[]
                {
                    " I've kept every promise I've made in this house.",
                    " My deal with " + context.activeDeals[0].partnerName + " proves I'm trustworthy.",
                    " I've honored my word, and that should count for something.",
                    " The deals I've made weren't just strategy — they were commitments I kept."
                }, nextRoll);
            if (context.allianceNames != null && context.allianceNames.Length > 0 && Roll(nextRoll) < 0.35)
            {
                string alliance = Pick(context.allianceNames, nextRoll);
                speech += Pick(new[] { " The " + alliance + " has been my backbone in this game.",
                    " Being part of " + alliance + " defined my journey here.",
                    " Everything I've done, I did for " + alliance + "." }, nextRoll);
            }
            if (context.loyaltyOaths != null && context.loyaltyOaths.Length > 0 && Roll(nextRoll) < 0.45)
            {
                var oath = Pick(context.loyaltyOaths, nextRoll);
                speech += Pick(new[] { " I swore loyalty to " + oath.partnerName + ", and I meant every word.",
                    " My oath to " + oath.partnerName + " is something I take seriously.",
                    " I made a promise of loyalty to " + oath.partnerName + " — that bond runs deep.",
                    " " + oath.partnerName + " and I swore an oath. That's not something I break." }, nextRoll);
            }
            long wins = context.compWins == null ? 0 : (long)context.compWins.hoh + context.compWins.pov + context.compWins.other;
            if (wins >= 2 && Roll(nextRoll) < 0.4)
                speech += ReplaceSourceToken(Pick(WebFinalSpeechCatalog.Brag[flavor], nextRoll), "{wins}", wins.ToString(CultureInfo.InvariantCulture));
            return speech;
        }

        public static string TraitFlavor(IReadOnlyList<string> traits)
        {
            if (traits == null) throw new ArgumentNullException(nameof(traits));
            string lead = traits.Count == 0 || string.IsNullOrEmpty(traits[0]) ? "Social" : traits[0];
            switch (lead)
            {
                case "Strategic": case "Analytical": return "cerebral";
                case "Social": case "Charming": case "Funny": case "Loyal": return "social";
                case "Competitive": case "Confrontational": case "Stubborn": return "aggressive";
                case "Manipulative": case "Deceptive": case "Sneaky": case "Flexible": return "sneaky";
                case "Emotional": case "Impulsive": case "Introverted": case "Intuitive": return "emotional";
                default: return "social";
            }
        }

        private static T Pick<T>(T[] items, Func<double> nextRoll) => items[(int)Math.Floor(Roll(nextRoll) * items.Length)];
        private static double Roll(Func<double> nextRoll)
        {
            double roll = nextRoll();
            if (double.IsNaN(roll) || double.IsInfinity(roll) || roll < 0 || roll >= 1)
                throw new ArgumentOutOfRangeException(nameof(nextRoll), "Random samples must be in [0, 1).");
            return roll;
        }

        // JavaScript String.replace(regex, string) expands $$, $&, $` and $' in replacement
        // strings. Literal C# Replace would differ for a user-authored title containing '$'.
        private static string ReplaceSourceToken(string text, string token, string replacement)
        {
            replacement = replacement ?? "null";
            var result = new StringBuilder();
            int start = 0, match;
            while ((match = text.IndexOf(token, start, StringComparison.Ordinal)) >= 0)
            {
                result.Append(text, start, match - start);
                for (int index = 0; index < replacement.Length; index++)
                {
                    char character = replacement[index];
                    if (character == '$' && index + 1 < replacement.Length)
                    {
                        char next = replacement[index + 1];
                        if (next == '$') { result.Append('$'); index++; continue; }
                        if (next == '&') { result.Append(token); index++; continue; }
                        if (next == '`') { result.Append(text, 0, match); index++; continue; }
                        if (next == '\'') { result.Append(text, match + token.Length, text.Length - match - token.Length); index++; continue; }
                    }
                    result.Append(character);
                }
                start = match + token.Length;
            }
            return result.Append(text, start, text.Length - start).ToString();
        }
    }
}
