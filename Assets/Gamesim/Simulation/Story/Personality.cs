using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Who a houseguest is, as the story system reads it: six axes built from both traits.
    ///
    /// <para>Story-scoped on purpose. The NPC verb repertoires in <see cref="NpcSocialActions"/>
    /// and the test that pins them are untouched (MASTER-PLAN:63's caution about authoring a
    /// personality system); these axes decide only odds, how a beat is cast, and what an NPC who
    /// holds a story choice picks.</para>
    ///
    /// <para>The table covers all seventeen traits in <see cref="WebTraits"/>, so a houseguest the
    /// player authored in the creator has axes as surely as a card from the roster does. A test
    /// pins that every trait has a row.</para>
    /// </summary>
    public static class Personality
    {
        /// <summary>One houseguest's axes.</summary>
        public readonly struct Axes
        {
            public readonly int Bold, Warm, Honest, Vengeful, Sociable, Steady;
            public Axes(int bold, int warm, int honest, int vengeful, int sociable, int steady)
            { Bold = bold; Warm = warm; Honest = honest; Vengeful = vengeful; Sociable = sociable; Steady = steady; }
            public static Axes operator +(Axes a, Axes b) => new Axes(a.Bold + b.Bold, a.Warm + b.Warm,
                a.Honest + b.Honest, a.Vengeful + b.Vengeful, a.Sociable + b.Sociable, a.Steady + b.Steady);
        }

        /// <summary>Each trait's contribution: Bold, Warm, Honest, Vengeful, Sociable, Steady (20 §2.10).</summary>
        public static readonly IReadOnlyDictionary<string, Axes> Table = new Dictionary<string, Axes>(StringComparer.OrdinalIgnoreCase)
        {
            { "Competitive",     new Axes( 2,  0,  0,  1,  0,  0) },
            { "Strategic",       new Axes( 0,  0, -1,  0,  0,  2) },
            { "Loyal",           new Axes( 0,  1,  2,  1,  0,  0) },
            { "Emotional",       new Axes( 0,  2,  0,  1,  1, -1) },
            { "Social",          new Axes( 0,  1,  0,  0,  2,  0) },
            { "Funny",           new Axes( 0,  1,  0, -1,  2,  0) },
            { "Charming",        new Axes( 1,  0, -1,  0,  2,  0) },
            { "Manipulative",    new Axes( 0, -1, -2,  0,  0,  1) },
            { "Analytical",      new Axes( 0,  0,  0,  0, -1,  2) },
            { "Impulsive",       new Axes( 2,  0,  0,  1,  0, -2) },
            { "Deceptive",       new Axes( 0,  0, -2,  0,  1,  0) },
            { "Introverted",     new Axes(-1,  0,  0,  0, -2,  1) },
            { "Stubborn",        new Axes( 1,  0,  0,  2,  0,  0) },
            { "Flexible",        new Axes( 0,  1,  0, -2,  0,  1) },
            { "Intuitive",       new Axes( 0,  1,  0,  0,  0,  1) },
            { "Sneaky",          new Axes(-1,  0, -1,  0,  0,  1) },
            { "Confrontational", new Axes( 2, -1,  1,  1,  0,  0) },
        };

        /// <summary>A houseguest's axes: the sum of their traits' rows. Unknown traits add nothing.</summary>
        public static Axes Of(ContestantState who)
        {
            var axes = new Axes(0, 0, 0, 0, 0, 0);
            if (who?.traits == null) return axes;
            foreach (var trait in who.traits)
                if (trait != null && Table.TryGetValue(trait, out var row)) axes += row;
            return axes;
        }

        public static bool Has(ContestantState who, params string[] traits) =>
            who?.traits != null && traits.Any(t => who.traits.Any(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)));

        // ---------------------------------------------------------------- stress

        /// <summary>The engine's stress ladder, lowest first.</summary>
        public static readonly string[] StressLadder = { "Relaxed", "Normal", "Tense", "Stressed", "Overwhelmed" };

        /// <summary>Steps above Normal: Tense 1, Stressed 2, Overwhelmed 3. Relaxed and Normal are 0.</summary>
        public static int StressStep(ContestantState who)
        {
            int index = Array.IndexOf(StressLadder, who?.stressLevel ?? "Normal");
            return Math.Max(0, index - 1);
        }

        /// <summary>Moves someone's stress by whole steps, staying on the ladder.</summary>
        public static void AdjustStress(ContestantState who, int steps)
        {
            if (who == null || steps == 0) return;
            int index = Array.IndexOf(StressLadder, who.stressLevel ?? "Normal");
            if (index < 0) index = 1;
            who.stressLevel = StressLadder[Math.Max(0, Math.Min(StressLadder.Length - 1, index + steps))];
        }

        // ---------------------------------------------------------------- volatility

        /// <summary>
        /// How close to shouting someone is right now.
        ///
        /// <para>Bold + Vengeful − Steady, plus what the week is doing to them: a stress step, a
        /// nomination this week, a Have-Not week, and a grudge of sixty or more against the other
        /// party. Temperament sets the shape of a fight and state sets its timing: Quinn shouts,
        /// Riley goes cold, and a nomination can push anyone towards the line.</para>
        /// </summary>
        public static int Volatility(EpisodeState state, string id, string againstId = null)
        {
            var who = state?.Find(id);
            if (who == null) return 0;
            var a = Of(who);
            int value = a.Bold + a.Vengeful - a.Steady + StressStep(who);
            if (state.nominees.Contains(id)) value += 1;
            if (HaveNots.Is(state, id)) value += 1;
            if (againstId != null && Grudges.Severity(state, id, againstId) >= 60) value += 1;
            return value;
        }

        /// <summary>Base volatility, from temperament alone.</summary>
        public static int BaseVolatility(ContestantState who)
        {
            var a = Of(who);
            return a.Bold + a.Vengeful - a.Steady;
        }

        // ---------------------------------------------------------------- reception

        /// <summary>How an option approaches somebody.</summary>
        public static class Approach
        {
            public const string Warm = "warm", Calculated = "calculated", Bold = "bold", Candid = "candid";
            public const string Playful = "playful", Yield = "yield", Hardball = "hardball";
            public static readonly string[] All = { Warm, Calculated, Bold, Candid, Playful, Yield, Hardball };
            public static bool IsKnown(string approach) => approach != null && Array.IndexOf(All, approach) >= 0;
        }

        /// <summary>How an approach lands on somebody: +1 resonates, −1 grates, 0 neither.</summary>
        public static int Reception(ContestantState subject, string approach)
        {
            if (subject == null || approach == null) return 0;
            var a = Of(subject);
            switch (approach)
            {
                case Approach.Warm:
                    if (a.Warm >= 2 || a.Sociable >= 2) return 1;
                    if (a.Warm <= -1 && a.Steady >= 2) return -1;
                    return 0;
                case Approach.Calculated:
                    if (a.Steady >= 2 && a.Warm <= 0) return 1;
                    if (a.Warm >= 2 && a.Steady <= 0) return -1;
                    return 0;
                case Approach.Bold:
                    if (a.Bold >= 2) return 1;
                    if (a.Bold <= -1 || a.Steady >= 3) return -1;
                    return 0;
                case Approach.Candid:
                    if (a.Honest >= 1) return 1;
                    if (a.Honest <= -2) return -1;
                    return 0;
                case Approach.Playful:
                    if (a.Sociable >= 2 && a.Steady <= 1) return 1;
                    if (a.Steady >= 2 && a.Sociable <= 0) return -1;
                    return 0;
                case Approach.Yield:
                    if (a.Bold >= 2 || a.Vengeful >= 2) return 1;
                    if (a.Honest >= 2 && a.Bold <= 0) return -1;
                    return 0;
                case Approach.Hardball:
                    if (a.Bold >= 2 && a.Honest >= 1) return 1;
                    if (a.Warm >= 2 || a.Vengeful >= 2) return -1;
                    return 0;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// A gain as it lands: half again when it resonates, turned into half a loss when it grates.
        /// Losses are not softened - an insult that lands well is still an insult.
        /// </summary>
        public static double Received(double gain, int reception)
        {
            if (gain <= 0 || reception == 0) return gain;
            return reception > 0 ? Math.Round(gain * 1.5) : -Math.Round(gain * 0.5);
        }

        // ---------------------------------------------------------------- npc choices

        /// <summary>
        /// The web's <c>ai_chance</c>: how much an NPC who holds a story choice wants each option,
        /// scaled by their axes. Never below a small floor, so a choice is unlikely rather than
        /// impossible.
        /// </summary>
        public static double Weight(ContestantState npc, double baseWeight, double bold = 0, double warm = 0,
            double honest = 0, double vengeful = 0, double sociable = 0, double steady = 0)
        {
            var a = Of(npc);
            double factor = 1 + bold * a.Bold + warm * a.Warm + honest * a.Honest + vengeful * a.Vengeful
                            + sociable * a.Sociable + steady * a.Steady;
            return Math.Max(0.05, baseWeight * factor);
        }
    }
}
