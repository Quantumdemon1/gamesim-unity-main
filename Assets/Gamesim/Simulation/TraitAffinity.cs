using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// How houseguests take to each other by temperament, in the manner of Crusader Kings' trait
    /// opinions: some traits are liked by nearly everyone and some disliked, and beyond that each
    /// trait warms to some traits in others and clashes with others. Two tables, both directional:
    /// <see cref="Reception"/> is what a trait earns from the house at large, and <see cref="Affinity"/>
    /// is what an observer's trait makes of a trait in somebody else. A pair's compatibility is the
    /// sum over both houseguests' traits, so a Loyal, Emotional houseguest and a Deceptive, Sneaky
    /// one start far apart and two Analytical Strategists close, before a word is said.
    ///
    /// <para>Pure data and a pure function. The season decides when it applies (the seed of the
    /// house's standings, the warmth of a conversation, the desire for an alliance), all behind the
    /// agency rules' boundary, so a season from before keeps its outcomes.</para>
    /// </summary>
    public static class TraitAffinity
    {
        /// <summary>What a trait earns from the house at large: liked by nearly everyone, or disliked.</summary>
        public static readonly IReadOnlyDictionary<string, int> Reception = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Loyal", 2 }, { "Funny", 2 }, { "Charming", 2 }, { "Social", 1 }, { "Flexible", 1 },
            { "Manipulative", -2 }, { "Deceptive", -2 }, { "Sneaky", -2 }, { "Confrontational", -2 }, { "Stubborn", -1 }, { "Impulsive", -1 },
        };

        /// <summary>
        /// What an observer's trait makes of a trait in somebody else. Positive is warmth, negative
        /// is a clash. Kinship runs through most of it (like warms to like), with the exceptions that
        /// make a house: schemers see rivals in each other, the intuitive see through the deceptive,
        /// the loyal cannot abide them, and the flexible and the stubborn grate.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Affinity = Build();

        private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Build()
        {
            var table = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            void Row(string observer, params (string trait, int opinion)[] opinions) =>
                table[observer] = opinions.ToDictionary(o => o.trait, o => o.opinion, StringComparer.OrdinalIgnoreCase);

            Row("Loyal", ("Loyal", 2), ("Emotional", 1), ("Stubborn", 1), ("Deceptive", -2), ("Manipulative", -2), ("Sneaky", -2));
            Row("Strategic", ("Strategic", 1), ("Analytical", 1), ("Flexible", 1), ("Impulsive", -1), ("Emotional", -1));
            Row("Emotional", ("Loyal", 1), ("Emotional", 1), ("Funny", 1), ("Confrontational", -2), ("Manipulative", -1), ("Analytical", -1));
            Row("Funny", ("Funny", 1), ("Social", 1), ("Charming", 1), ("Stubborn", -1), ("Introverted", -1));
            Row("Charming", ("Charming", 1), ("Social", 1), ("Funny", 1), ("Confrontational", -1));
            Row("Manipulative", ("Loyal", 1), ("Emotional", 1), ("Manipulative", -1), ("Analytical", -1), ("Intuitive", -1));
            Row("Analytical", ("Analytical", 1), ("Strategic", 1), ("Introverted", 1), ("Impulsive", -2), ("Emotional", -1));
            Row("Impulsive", ("Impulsive", 1), ("Funny", 1), ("Competitive", 1), ("Analytical", -1), ("Introverted", -1));
            Row("Deceptive", ("Deceptive", 1), ("Sneaky", 1), ("Loyal", -1), ("Intuitive", -1));
            Row("Social", ("Social", 1), ("Funny", 1), ("Charming", 1), ("Introverted", -1));
            Row("Introverted", ("Introverted", 1), ("Analytical", 1), ("Social", -1), ("Funny", -1), ("Confrontational", -1));
            Row("Stubborn", ("Stubborn", 1), ("Loyal", 1), ("Flexible", -1), ("Manipulative", -1));
            Row("Flexible", ("Flexible", 1), ("Strategic", 1), ("Stubborn", -2), ("Confrontational", -1));
            Row("Intuitive", ("Intuitive", 1), ("Emotional", 1), ("Deceptive", -2), ("Sneaky", -2), ("Manipulative", -1));
            // The bully and the sneak are natural enemies, both ways: the one symmetric clash in the house.
            Row("Sneaky", ("Sneaky", 1), ("Deceptive", 1), ("Confrontational", -2), ("Intuitive", -1), ("Loyal", -1));
            Row("Confrontational", ("Confrontational", 1), ("Competitive", 1), ("Stubborn", 1), ("Sneaky", -2), ("Deceptive", -1), ("Charming", -1));
            Row("Competitive", ("Competitive", 1), ("Strategic", 1), ("Introverted", -1));
            return table;
        }

        /// <summary>What one houseguest's temperament makes of another's: the sum of the observer's opinions of the other's traits, plus what those traits earn from anyone. Directional.</summary>
        public static int Compatibility(IEnumerable<string> observerTraits, IEnumerable<string> otherTraits)
        {
            var observer = (observerTraits ?? Enumerable.Empty<string>()).Where(t => t != null).ToList();
            var other = (otherTraits ?? Enumerable.Empty<string>()).Where(t => t != null).ToList();
            int total = 0;
            foreach (var theirs in other)
            {
                if (Reception.TryGetValue(theirs, out int received)) total += received;
                foreach (var mine in observer)
                    if (Affinity.TryGetValue(mine, out var opinions) && opinions.TryGetValue(theirs, out int opinion)) total += opinion;
            }
            return total;
        }

        /// <summary>How one houseguest's temperament takes to another's, as the season keeps them.</summary>
        public static int Compatibility(ContestantState observer, ContestantState other) =>
            observer == null || other == null ? 0 : Compatibility(observer.traits, other.traits);

        /// <summary>Compatibility in words, for a card or a log line.</summary>
        public static string Describe(int compatibility) =>
            compatibility >= 4 ? "kindred" : compatibility >= 2 ? "easy company" : compatibility <= -4 ? "oil and water"
            : compatibility <= -2 ? "grating" : "neither here nor there";
    }
}
