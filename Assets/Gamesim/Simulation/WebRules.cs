using System;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Tested arithmetic leaves from the preserved web implementation using its
    /// default balance configuration. These functions do not authorize commands,
    /// implement the whole web reducer, or claim parity for NPC planning policies.
    /// </summary>
    public static class WebRules
    {
        public static uint HashSeed(string value) => SeededRandom.HashSeed(value);

        // JavaScript ties round toward positive infinity: -37.5 becomes -37.
        public static double JsRound(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return value;
            var floor = Math.Floor(value);
            return value - floor < 0.5 ? floor : floor + 1;
        }

        public static double ClampScore(double value) => Math.Max(-100, Math.Min(100, value));

        // src/systems/relationship-change.ts; only positive player changes gain the social bonus.
        public static double RelationshipDelta(double change, double social, bool isPlayer)
        {
            if (!isPlayer || change <= 0) return change;
            var bonus = Math.Min(0.1, Math.Max(0, social - 5) * 0.02);
            return JsRound(change * (1 + bonus));
        }

        public static double ReciprocalDelta(double change, double roll)
        {
            ValidateRoll(roll, nameof(roll));
            return change * (0.8 + roll * 0.4);
        }

        // Legacy src/systems/competition-system.ts uses one stat and chooses the
        // later input on a tie. The weighted runner below has a different contract.
        public static double CompetitionScore(double stat, double roll)
        {
            ValidateRoll(roll, nameof(roll));
            return stat * (0.75 + roll * 0.5);
        }

        // src/systems/competition/competition-runner.ts + models/competition.ts.
        // Caller supplies samples in original order. The web runner consumes a
        // competition-name draw before scores; Crapshoot consumes two per actor.
        // Ordered placement uses stable descending scores, so its first tie wins.
        public static double WeightedCompetitionScore(ContestantStats stats, string category,
            bool nominated, double bonus, double roll, double luckRoll = 0)
        {
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            ValidateRoll(roll, nameof(roll));
            double physical, mental, endurance, social, luck;
            switch (category)
            {
                case "Endurance": physical = 0.3; mental = 0.1; endurance = 0.5; social = 0; luck = 0.1; break;
                case "Physical": physical = 0.5; mental = 0.1; endurance = 0.2; social = 0; luck = 0.2; break;
                case "Mental": physical = 0; mental = 0.6; endurance = 0.1; social = 0.1; luck = 0.2; break;
                case "Skill": physical = 0.3; mental = 0.3; endurance = 0.1; social = 0; luck = 0.3; break;
                case "Crapshoot": physical = 0; mental = 0.1; endurance = 0; social = 0; luck = 0.9; break;
                default: throw new ArgumentException("Unknown competition category: " + category, nameof(category));
            }
            var baseScore = stats.physical * physical + stats.mental * mental + stats.endurance * endurance
                + stats.social * social + stats.luck * luck;
            var luckBonus = 0d;
            if (category == "Crapshoot")
            {
                ValidateRoll(luckRoll, nameof(luckRoll));
                luckBonus = luckRoll * 3;
            }
            var clutchBonus = nominated ? stats.competition * 0.5 : 0;
            return baseScore * (0.75 + roll * 0.5) + luckBonus + clutchBonus + bonus;
        }

        public static double PromiseImpact(string type, string status, double loyalty = 5)
        {
            double multiplier;
            switch (type)
            {
                case "safety": multiplier = 1.5; break;
                case "final_2": multiplier = 2; break;
                case "vote": multiplier = 1; break;
                case "alliance_loyalty": multiplier = 1.8; break;
                case "information": multiplier = 0.7; break;
                case "hoh_protection": multiplier = 1.6; break;
                case "veto_use": multiplier = 1.4; break;
                default: throw new ArgumentException("Unknown promise type: " + type, nameof(type));
            }
            if (status == "fulfilled") return JsRound(15 * multiplier * (1 + (loyalty - 5) * 0.15));
            // Preserve code arithmetic: loyalty 5 is 1.25, despite its stale web comment.
            if (status == "broken") return JsRound(-25 * multiplier * (0.5 + loyalty * 0.15));
            return 0;
        }

        public static double PromiseImpact(PromiseKind type, PromiseStatus status, double loyalty = 5)
        {
            string name;
            switch (type)
            {
                case PromiseKind.Safety: name = "safety"; break;
                case PromiseKind.Vote: name = "vote"; break;
                case PromiseKind.FinalTwo: name = "final_2"; break;
                case PromiseKind.AllianceLoyalty: name = "alliance_loyalty"; break;
                case PromiseKind.Information: name = "information"; break;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
            return PromiseImpact(name, status.ToString().ToLowerInvariant(), loyalty);
        }

        public static double SuccessChance(double stat, double required) => Math.Max(10, Math.Min(100, 60 + (stat - required) * 15));
        public static double FailedInteractionPenalty(double change) => change > 0 ? -Math.Ceiling(change * 0.5) : Math.Floor(change * 1.5);

        private static void ValidateRoll(double value, string parameter)
        {
            if (double.IsNaN(value) || value < 0 || value >= 1)
                throw new ArgumentOutOfRangeException(parameter, "Random samples must be in [0, 1).");
        }
    }
}
