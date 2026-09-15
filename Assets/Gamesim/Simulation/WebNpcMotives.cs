using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Gamesim.Simulation
{
    // JSON-only leaf inputs/results, not Unity-serialized or persisted state contracts.
    public sealed class WebNpcMotiveState
    {
        public double social, rest, fun, energy, hygiene;
        public WebNpcMotiveState Clone() => (WebNpcMotiveState)MemberwiseClone();
        public double Get(string motive)
        {
            switch (motive)
            {
                case "social": return social; case "rest": return rest; case "fun": return fun;
                case "energy": return energy; case "hygiene": return hygiene;
                default: throw new ArgumentException("Unknown motive.", nameof(motive));
            }
        }
        internal void Set(string motive, double value)
        {
            switch (motive)
            {
                case "social": social = value; break; case "rest": rest = value; break;
                case "fun": fun = value; break; case "energy": energy = value; break;
                case "hygiene": hygiene = value; break;
                default: throw new ArgumentException("Unknown motive.", nameof(motive));
            }
        }
    }

    /// <summary>One present key of a source partial record, retaining Object.entries order.</summary>
    public sealed class WebNpcMotiveAmount
    {
        public string motive;
        public double amount;
        public WebNpcMotiveAmount() { }
        public WebNpcMotiveAmount(string name, double value) { motive = name; amount = value; }
    }

    public sealed class WebNpcMoodBoost { public double speedBoost, socialBoost; }

    /// <summary>
    /// Pure supplied-input port of npcMotives.ts. No clock, scheduler, persistence,
    /// room authority or shared random source. This is not the React caller's state publication.
    /// </summary>
    public static class WebNpcMotives
    {
        public static readonly IReadOnlyList<string> MotiveNames = Array.AsReadOnly(new[] { "social", "rest", "fun", "energy", "hygiene" });
        public static readonly IReadOnlyDictionary<string, double> CriticalThresholds =
            new ReadOnlyDictionary<string, double>(new Dictionary<string, double>(StringComparer.Ordinal)
            { ["social"] = 25, ["rest"] = 20, ["fun"] = 30, ["energy"] = 15, ["hygiene"] = 10 });
        private static readonly double[] BaseDecay = { .3, .2, .4, .15, .1 };
        private static readonly double[] Emergency = { 12, 10, 15, 8, 5 };
        private static readonly Dictionary<string, WebNpcMotiveAmount[]> TraitDecay = new Dictionary<string, WebNpcMotiveAmount[]>(StringComparer.Ordinal)
        {
            ["Social"] = Entries("social",1.5), ["Competitive"] = Entries("fun",1.4),
            ["Introverted"] = Entries("social",.5,"rest",1.3), ["Analytical"] = Entries("energy",.7),
            ["Emotional"] = Entries("social",1.2,"rest",1.1), ["Funny"] = Entries("fun",1.2,"social",1.2),
            ["Charming"] = Entries("social",1.3), ["Loyal"] = Entries("social",1.1),
            ["Stubborn"] = Entries("energy",1.2), ["Impulsive"] = Entries("fun",1.3,"energy",1.2)
        };
        private static readonly Dictionary<string, WebNpcMotiveAmount[]> InitialBiases = new Dictionary<string, WebNpcMotiveAmount[]>(StringComparer.Ordinal)
        {
            ["Social"] = Entries("social",-10), ["Introverted"] = Entries("social",10,"rest",-5),
            ["Competitive"] = Entries("fun",-10), ["Analytical"] = Entries("energy",5), ["Emotional"] = Entries("rest",-5)
        };
        private static readonly Dictionary<string, WebNpcMotiveAmount[]> ActivitySatisfaction = new Dictionary<string, WebNpcMotiveAmount[]>(StringComparer.Ordinal)
        {
            ["sit"] = Entries("social",3,"rest",3,"fun",2), ["stand"] = Entries("social",2,"energy",2),
            ["cook"] = Entries("energy",6,"social",2), ["sleep"] = Entries("rest",10),
            ["game"] = Entries("fun",8,"social",2), ["groom"] = Entries("hygiene",10),
            ["lounge"] = Entries("rest",6,"fun",4), ["swim"] = Entries("fun",7,"hygiene",3,"social",3),
            ["eat"] = Entries("energy",5,"fun",3), ["clean"] = Entries("hygiene",8),
            ["watch_tv"] = Entries("fun",5,"rest",3), ["exercise"] = Entries("energy",-3,"fun",4)
        };

        public static WebNpcMotiveState CreateInitialMotives(IReadOnlyList<string> traits = null)
        {
            var result = new WebNpcMotiveState { social = 75, rest = 75, fun = 75, energy = 75, hygiene = 75 };
            if (traits != null) foreach (var trait in traits)
                if (trait != null && InitialBiases.TryGetValue(trait, out var biases))
                    foreach (var bias in biases)
                        result.Set(bias.motive, Math.Max(0, Math.Min(100, result.Get(bias.motive) + bias.amount)));
            return result;
        }

        public static WebNpcMotiveState DecayMotives(WebNpcMotiveState state, double deltaSeconds, IReadOnlyList<string> traits = null)
        {
            Validate(state); Finite(deltaSeconds, nameof(deltaSeconds));
            var result = state.Clone();
            for (int index = 0; index < MotiveNames.Count; index++)
            {
                string motive = MotiveNames[index]; double rate = BaseDecay[index];
                if (traits != null) foreach (var trait in traits)
                    if (trait != null && TraitDecay.TryGetValue(trait, out var modifiers))
                        foreach (var modifier in modifiers) if (modifier.motive == motive) rate *= modifier.amount;
                // Deliberately lower-only. Negative supplied time may increase a value past100.
                result.Set(motive, Math.Max(0, result.Get(motive) - rate * deltaSeconds));
            }
            return result;
        }

        public static WebNpcMotiveState SatisfyMotives(WebNpcMotiveState state, IReadOnlyList<WebNpcMotiveAmount> satisfaction)
        {
            Validate(state); Validate(satisfaction);
            var result = state.Clone();
            foreach (var entry in satisfaction)
                if (entry.amount > 0) result.Set(entry.motive, Math.Min(100, result.Get(entry.motive) + entry.amount));
            return result;
        }

        public static double GetUrgency(double value)
        {
            Finite(value, nameof(value));
            return Math.Pow(1 - Math.Max(0, Math.Min(100, value)) / 100, 1.5);
        }

        public static List<WebNpcMotiveAmount> GetActivitySatisfaction(string activity)
        {
            var result = new List<WebNpcMotiveAmount>();
            if (activity != null && ActivitySatisfaction.TryGetValue(activity, out var entries))
                foreach (var entry in entries) result.Add(new WebNpcMotiveAmount(entry.motive, entry.amount));
            return result;
        }

        public static double GetActivityDurationMultiplier(WebNpcMotiveState motives, IReadOnlyList<WebNpcMotiveAmount> satisfaction)
        {
            Validate(motives); Validate(satisfaction);
            double result = 1;
            foreach (var entry in satisfaction)
            {
                if (entry.amount <= 0) continue;
                double value = motives.Get(entry.motive);
                if (value < 10) result = Math.Max(result, 2);
                else if (value < 20) result = Math.Max(result, 1.5);
            }
            return result;
        }

        public static double GetWalkSpeedMultiplier(WebNpcMotiveState motives, IReadOnlyList<WebNpcMotiveAmount> satisfaction)
        {
            Validate(motives); Validate(satisfaction);
            foreach (var entry in satisfaction)
                if (entry.amount > 0 && motives.Get(entry.motive) < CriticalThresholds[entry.motive]) return 1.5;
            foreach (string motive in MotiveNames) if (motives.Get(motive) <= 60) return 1;
            return .7;
        }

        public static string GetIdleMicroBehavior(WebNpcMotiveState motives, Func<double> nextRoll, IReadOnlyList<string> traits = null)
        {
            Validate(motives);
            if (nextRoll == null) throw new ArgumentNullException(nameof(nextRoll));
            double roll = nextRoll(); Finite(roll, "roll");
            if (roll < 0 || roll >= 1) throw new ArgumentOutOfRangeException(nameof(nextRoll), "Draws must be in [0,1).");
            // The source _traits argument is intentionally unused, including when traits are present.
            if (roll > .3) return null;
            if (motives.energy < 25) return "yawn";
            if (motives.fun < 30) return "fidget";
            if (motives.rest < 25) return "stretch";
            return "look_around";
        }

        public static string GetCriticalMotive(WebNpcMotiveState motives)
        {
            Validate(motives);
            for (int index = 0; index < MotiveNames.Count; index++)
                if (motives.Get(MotiveNames[index]) < Emergency[index]) return MotiveNames[index];
            return null;
        }

        public static WebNpcMoodBoost GetMoodBoost(WebNpcMotiveState before, WebNpcMotiveState after)
        {
            Validate(before); Validate(after);
            foreach (string motive in MotiveNames)
                if (after.Get(motive) - before.Get(motive) > 30) return new WebNpcMoodBoost { speedBoost = 1.2, socialBoost = 10 };
            return null;
        }

        private static WebNpcMotiveAmount[] Entries(params object[] pairs)
        {
            var result = new WebNpcMotiveAmount[pairs.Length / 2];
            for (int index = 0; index < result.Length; index++)
                result[index] = new WebNpcMotiveAmount((string)pairs[index * 2], Convert.ToDouble(pairs[index * 2 + 1], System.Globalization.CultureInfo.InvariantCulture));
            return result;
        }
        private static void Validate(WebNpcMotiveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            foreach (string motive in MotiveNames) Finite(state.Get(motive), motive);
        }
        private static void Validate(IReadOnlyList<WebNpcMotiveAmount> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || entry.motive == null || !CriticalThresholds.ContainsKey(entry.motive) || !names.Add(entry.motive))
                    throw new ArgumentException("Satisfaction must contain distinct known motive keys.", nameof(entries));
                Finite(entry.amount, entry.motive);
            }
        }
        private static void Finite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name, "Leaf inputs must be finite.");
        }
    }
}
