using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    // Transient, detached JSON DTOs. These are not Unity-serialized state, scheduling
    // authority, a save-schema proposal, or permission to mutate relationship scores.
    public sealed class WebNpcConversationMemory
    {
        public string partnerId, lastTopic;
        public int count;
        public double lastTime;
        public WebNpcConversationMemory Clone() => (WebNpcConversationMemory)MemberwiseClone();
    }
    public sealed class WebNpcConversationGameContext
    {
        public string phase;
        public List<string> nominees, recentEvictees;
        // The source topic picker does not read playerPersonaLabel.
    }
    public sealed class WebNpcConversationTopicInput
    {
        public List<string> traits1 = new List<string>(), traits2 = new List<string>();
        public double score;
        public bool areAllied;
        public WebNpcConversationMemory memory;
        public WebNpcConversationGameContext gameContext;
        public string seekIntent;
    }
    public sealed class WebNpcConversationMemoryPair
    {
        public WebNpcConversationMemory forward, reverse;
    }
    public sealed class WebNpcConversationCompletionInput
    {
        public List<string> participants = new List<string>();
        public string topic, playerId;
        public List<string> traits1 = new List<string>(), traits2 = new List<string>();
        public WebNpcConversationMemory memory;
        public List<string> allNpcIds;
    }
    public sealed class WebNpcConversationGossipTarget
    {
        public string id;
        public int delta;
    }
    public sealed class WebNpcConversationEnd
    {
        public List<string> participants = new List<string>();
        public string topic;
        public int delta;
        public WebNpcConversationGossipTarget gossipTarget;
    }

    /// <summary>
    /// Pure leaf rules from useNPCSocialInteractions.ts. Callers supply mechanical
    /// draws, original phase labels, completion-time traits/memory, and a clock.
    /// No clock/RNG ownership, cooldown, pairing, React effect, gesture, visibility,
    /// relationship reducer, persistence or trusted command authority is implied.
    /// </summary>
    public static class WebNpcConversations
    {
        private static readonly string[] Topics =
            { "bonding", "strategy", "gossip", "tension", "casual", "nominations", "alliance_talk", "rivalry" };
        private static readonly int[] MinimumDelta = { 1, 0, -1, -3, 0, -1, 1, -3 };
        private static readonly int[] MaximumDelta = { 2, 2, 1, -1, 1, 2, 3, 0 };

        public static string[] TopicOrder => (string[])Topics.Clone();

        public static double[] TraitWeights(IReadOnlyList<string> traits)
        {
            CheckTraits(traits);
            var weights = new double[] { 20, 20, 20, 20, 20, 5, 5, 5 };
            foreach (string trait in traits)
            {
                switch (trait)
                {
                    case "Strategic": case "Analytical": weights[1] += 40; weights[4] -= 10; break;
                    case "Social": case "Charming": case "Funny": weights[0] += 30; weights[4] += 10; break;
                    case "Manipulative": case "Deceptive": case "Sneaky": weights[2] += 30; weights[3] += 15; break;
                    case "Competitive": case "Confrontational": weights[3] += 25; weights[1] += 15; break;
                    case "Emotional": weights[0] += 15; weights[3] += 15; break;
                    case "Loyal": weights[0] += 25; weights[1] += 10; break;
                    case "Introverted": weights[4] += 20; weights[0] -= 10; break;
                }
            }
            for (int index = 0; index < weights.Length; index++) weights[index] = Math.Max(weights[index], 1);
            return weights;
        }

        public static double[] TopicWeights(WebNpcConversationTopicInput input)
            => TopicWeights(input, input?.gameContext?.recentEvictees != null && input.gameContext.recentEvictees.Count > 0);

        // Native adapter overload: supplies a truthful evidence-presence flag without
        // manufacturing evictee IDs. Original supplied-list API forwards unchanged.
        public static double[] TopicWeights(WebNpcConversationTopicInput input, bool hasRecentEvictionEvidence)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            Finite(input.score, "relationship score"); CheckMemory(input.memory);
            var first = TraitWeights(input.traits1); var second = TraitWeights(input.traits2);
            var blended = new double[8];
            for (int index = 0; index < blended.Length; index++) blended[index] = (first[index] + second[index]) / 2;
            if (input.areAllied) { blended[1] *= 1.5; blended[0] *= 1.3; blended[6] *= 3.0; }
            if (input.score > 50) blended[0] *= 1.4;
            if (input.score < -20) { blended[3] *= 2.0; blended[2] *= 1.3; blended[7] *= 2.5; }
            var context = input.gameContext;
            if (context != null)
            {
                // Exact source substring matching, not a native EpisodePhase mapping.
                string phase = context.phase?.ToLowerInvariant();
                if (phase != null && (phase.Contains("nomination") || phase.Contains("veto") || phase.Contains("pov"))) blended[5] *= 4.0;
                if (context.nominees != null && context.nominees.Count > 0) blended[5] *= 2.0;
            }
            if (hasRecentEvictionEvidence) blended[2] *= 1.5;
            switch (input.seekIntent)
            {
                case "ally_check": blended[6] *= 5.0; blended[1] *= 2.0; break;
                case "confront": blended[7] *= 5.0; blended[3] *= 2.0; break;
                case "get_to_know": blended[0] *= 2.0; blended[4] *= 2.0; break;
                case "gossip_share": blended[2] *= 5.0; break;
            }
            // Deliberately no memory-age check: the source reads momentum before the
            // accepted start resets an old entry. Zero score chooses tension, not bonding.
            if (input.memory != null && input.memory.count >= 3)
            { if (input.score > 0) blended[0] *= 1.5; else blended[3] *= 1.5; }
            return blended;
        }

        public static string PickTopic(WebNpcConversationTopicInput input, Func<double> nextRoll)
            => PickTopic(input, input?.gameContext?.recentEvictees != null && input.gameContext.recentEvictees.Count > 0, nextRoll);

        public static string PickTopic(WebNpcConversationTopicInput input, bool hasRecentEvictionEvidence, Func<double> nextRoll)
        {
            var weights = TopicWeights(input, hasRecentEvictionEvidence);
            double total = 0; foreach (double weight in weights) total += weight;
            double roll = Draw(nextRoll) * total;
            for (int index = 0; index < Topics.Length; index++)
            { roll -= weights[index]; if (roll <= 0) return Topics[index]; }
            return "casual";
        }

        public static double DurationMilliseconds(string topic, bool areAllied, double score, Func<double> nextRoll)
        {
            TopicIndex(topic); Finite(score, "relationship score");
            int minimum, maximum;
            if (topic == "alliance_talk" || topic == "rivalry" || (topic == "strategy" && areAllied))
            { minimum = 20000; maximum = 40000; }
            else if (topic == "nominations" || Math.Abs(score) > 30) { minimum = 15000; maximum = 30000; }
            else if (topic == "casual" || Math.Abs(score) < 10) { minimum = 8000; maximum = 15000; }
            else { minimum = 10000; maximum = 25000; }
            return minimum + Draw(nextRoll) * (maximum - minimum);
        }

        public static WebNpcConversationMemoryPair RecordPairMemory(string firstId, string secondId, string topic,
            double nowMilliseconds, WebNpcConversationMemory forward, WebNpcConversationMemory reverse)
        {
            CheckPair(firstId, secondId); TopicIndex(topic); Clock(nowMilliseconds); CheckMemory(forward); CheckMemory(reverse);
            if ((forward != null && forward.partnerId != secondId) || (reverse != null && reverse.partnerId != firstId))
                throw new ArgumentException("Directed memory must identify the matching conversation partner.");
            WebNpcConversationMemory Record(string partner, WebNpcConversationMemory old)
            {
                bool expired = old != null && nowMilliseconds - old.lastTime > 120000;
                int count = expired ? 1 : checked((old?.count ?? 0) + 1);
                return new WebNpcConversationMemory { partnerId = partner, count = count, lastTopic = topic, lastTime = nowMilliseconds };
            }
            // Both entries are detached, including when only one direction existed. A
            // backwards caller clock is not silently corrected; source subtraction applies.
            return new WebNpcConversationMemoryPair { forward = Record(secondId, forward), reverse = Record(firstId, reverse) };
        }

        public static WebNpcConversationEnd Complete(WebNpcConversationCompletionInput input, Func<double> nextRoll)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.participants == null || input.participants.Count != 2) throw new ArgumentException("Exactly two participants are required.");
            CheckPair(input.participants[0], input.participants[1]); int index = TopicIndex(input.topic);
            CheckTraits(input.traits1); CheckTraits(input.traits2); CheckMemory(input.memory);
            if (input.allNpcIds != null && input.allNpcIds.Any(string.IsNullOrEmpty)) throw new ArgumentException("Supplied NPC IDs must not be empty.");
            int delta = JsRound(MinimumDelta[index] + Draw(nextRoll) * (MaximumDelta[index] - MinimumDelta[index]));
            if (input.traits1.Contains("Emotional") || input.traits2.Contains("Emotional")) delta = JsRound(delta * 1.5);
            if (input.memory != null && input.memory.count >= 3) delta = JsRound(delta * .5);
            var result = new WebNpcConversationEnd { participants = new List<string>(input.participants), topic = input.topic, delta = delta };
            // Draw the chance before testing null/empty cast. Source keeps supplied order
            // (including repeated third-party IDs) and excludes both participants/player.
            if (input.topic == "gossip" && Draw(nextRoll) < .3 && input.allNpcIds != null)
            {
                var others = input.allNpcIds.Where(id => id != input.participants[0] && id != input.participants[1] && id != input.playerId).ToArray();
                if (others.Length > 0)
                    result.gossipTarget = new WebNpcConversationGossipTarget
                    { id = others[(int)Math.Floor(Draw(nextRoll) * others.Length)], delta = Draw(nextRoll) < .5 ? -2 : -3 };
            }
            return result;
        }

        private static int JsRound(double value) => (int)Math.Floor(value + .5);
        private static double Draw(Func<double> nextRoll)
        {
            if (nextRoll == null) throw new ArgumentNullException(nameof(nextRoll));
            double value = nextRoll();
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value >= 1)
                throw new ArgumentOutOfRangeException(nameof(nextRoll), "A mechanical draw must be in [0,1).");
            return value;
        }
        private static int TopicIndex(string topic)
        {
            int index = Array.IndexOf(Topics, topic);
            if (index < 0) throw new ArgumentException("Unknown source conversation topic.", nameof(topic));
            return index;
        }
        private static void CheckTraits(IReadOnlyList<string> traits)
        {
            if (traits == null || traits.Count > 1024 || traits.Any(trait => trait == null))
                throw new ArgumentException("Supply a bounded ordered list of non-null trait names.");
        }
        private static void CheckPair(string firstId, string secondId)
        {
            if (string.IsNullOrEmpty(firstId) || string.IsNullOrEmpty(secondId) || firstId == secondId)
                throw new ArgumentException("A conversation requires two distinct nonempty actor IDs.");
        }
        private static void CheckMemory(WebNpcConversationMemory memory)
        {
            if (memory == null) return;
            if (memory.count < 0) throw new ArgumentException("Memory count cannot be negative.");
            Clock(memory.lastTime);
        }
        private static void Clock(double value)
        {
            Finite(value, "clock");
            if (value < 0 || value > 9007199254740991d) throw new ArgumentOutOfRangeException(nameof(value), "Clock must fit a nonnegative JS-safe millisecond value.");
        }
        private static void Finite(double value, string label)
        { if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(label, "Input must be finite."); }
    }
}
