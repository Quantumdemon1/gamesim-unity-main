using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    [Serializable]
    public sealed class WebStudyPlan
    {
        public int chance;
        public bool success;
        public int bonusDelta;
        public int studyBonus;
    }

    /// <summary>
    /// Targetless Study the House path from contextual-action-generator, StrategicApproachDialog,
    /// use-strategic-action-listener, and player-action-reducer. Not a general action authority.
    /// Source personalityTraits is intentionally separate from the ordinary houseguest traits.
    /// </summary>
    public static class WebStudyHouse
    {
        public static bool IsAvailable(string sourcePhase, bool activePlayer) => activePlayer &&
            (sourcePhase == "SocialInteraction" || sourcePhase == "HoH" || sourcePhase == "PoV");

        public static int SuccessChance(string approachId, IReadOnlyList<string> sourcePersonalityTraits)
        {
            ValidateApproach(approachId);
            int chance = approachId == "memorize-layout" ? 100 : 45;
            if (approachId == "sneak-peek" && sourcePersonalityTraits != null && sourcePersonalityTraits.Contains("Strategic"))
                chance += 15;
            // Study has no target; every other contextual success-chance input is zero/false.
            return Math.Max(5, Math.Min(95, chance));
        }

        public static WebStudyPlan PlanStudy(int currentBonus, string approachId,
            IReadOnlyList<string> sourcePersonalityTraits, double roll)
        {
            int chance = SuccessChance(approachId, sourcePersonalityTraits);
            CheckRoll(roll);
            bool success = roll * 100 <= chance;
            // Actual outcome handler grants memorize +1 even when the dialog roll failed.
            int delta = approachId == "memorize-layout" ? 1 : success ? 2 : -1;
            return new WebStudyPlan { chance = chance, success = success, bonusDelta = delta,
                studyBonus = ApplyBonus(currentBonus, delta) };
        }

        public static WebStudyPlan PlanStudy(int currentBonus, string approachId,
            IReadOnlyList<string> sourcePersonalityTraits, Func<double> nextRoll)
        {
            ValidateApproach(approachId);
            if (nextRoll == null) throw new ArgumentNullException(nameof(nextRoll));
            return PlanStudy(currentBonus, approachId, sourcePersonalityTraits, nextRoll());
        }

        public static int ApplyBonus(int currentBonus, int delta) =>
            (int)Math.Max(0L, Math.Min(5L, (long)currentBonus + delta));

        /// <summary>Original fast-forward.ts caller, not the normal played/skip scoring route.</summary>
        public static double FastForwardCompetitionBonus(double phaseEventCompBonus, int playerStudyBonus)
        {
            CheckFinite(phaseEventCompBonus, nameof(phaseEventCompBonus));
            return phaseEventCompBonus + playerStudyBonus;
        }

        internal static void CheckRoll(double roll)
        {
            if (double.IsNaN(roll) || roll < 0 || roll >= 1)
                throw new ArgumentOutOfRangeException(nameof(roll), "Expected an original random sample in [0,1).");
        }

        internal static void CheckFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name);
        }

        private static void ValidateApproach(string approachId)
        {
            if (approachId != "memorize-layout" && approachId != "sneak-peek")
                throw new ArgumentException("Unknown source study approach.", nameof(approachId));
        }
    }

    [Serializable]
    public sealed class WebMiniGameContestant
    {
        public string id;
        public string name;
        public bool isPlayer;
        public ContestantStats stats;
    }

    [Serializable]
    public sealed class WebMiniGameScore
    {
        public string houseguestId;
        public string name;
        public double score;
        public bool isPlayer;
    }

    [Serializable]
    public sealed class WebMiniGamePlacement
    {
        public string id;
        public string name;
        public int position;
        public double score;
        public bool isPlayer;
    }

    /// <summary>
    /// Normal web NPCScoring.ts route, deliberately separate from WebRules' weighted runner.
    /// Study is only an explicit SkipScore input. Played/throw merge has no study parameter.
    /// </summary>
    public static class WebMiniGameScoring
    {
        public static double ScoreNpc(ContestantStats stats, string type, double roll)
        {
            WebStudyHouse.CheckRoll(roll);
            double effective = EffectiveStat(stats, type);
            double score = Math.Min(10, Math.Max(0, effective * (0.85 + roll * 0.3)));
            return WebRules.JsRound(score * 100) / 100;
        }

        public static double SkipScore(ContestantStats stats, string type, double studyBonus, double roll)
        {
            WebStudyHouse.CheckRoll(roll);
            WebStudyHouse.CheckFinite(studyBonus, nameof(studyBonus));
            double raw = EffectiveStat(stats, type) * (0.85 + roll * 0.3) + studyBonus * 0.3;
            return Math.Min(10, Math.Max(0, WebRules.JsRound(raw * 100) / 100));
        }

        public static double ThrowScore(double roll)
        {
            WebStudyHouse.CheckRoll(roll);
            return WebRules.JsRound((0.5 + roll) * 100) / 100;
        }

        public static double ApplyWeekFocusScore(double playerScore, double weekFocusBonus)
        {
            WebStudyHouse.CheckFinite(playerScore, nameof(playerScore));
            WebStudyHouse.CheckFinite(weekFocusBonus, nameof(weekFocusBonus));
            return Math.Min(10, Math.Max(0, playerScore + weekFocusBonus * 0.3));
        }

        public static List<WebMiniGameScore> GenerateNpcScores(IReadOnlyList<WebMiniGameContestant> participants,
            string type, Func<double> nextRoll)
        {
            if (participants == null) throw new ArgumentNullException(nameof(participants));
            if (nextRoll == null) throw new ArgumentNullException(nameof(nextRoll));
            var scores = new List<WebMiniGameScore>();
            foreach (var guest in participants)
            {
                if (guest == null) throw new ArgumentException("Missing contestant.", nameof(participants));
                if (guest.isPlayer) continue;
                scores.Add(new WebMiniGameScore { houseguestId = guest.id, name = guest.name,
                    score = ScoreNpc(guest.stats, type, nextRoll()), isPlayer = false });
            }
            return scores;
        }

        public static List<WebMiniGamePlacement> MergePlayerWithNpcScores(string playerId, string playerName,
            double playerScore, IReadOnlyList<WebMiniGameScore> npcScores, double weekFocusBonus = 0)
        {
            if (npcScores == null) throw new ArgumentNullException(nameof(npcScores));
            var all = new List<WebMiniGameScore> { new WebMiniGameScore { houseguestId = playerId,
                name = playerName, score = ApplyWeekFocusScore(playerScore, weekFocusBonus), isPlayer = true } };
            foreach (var npc in npcScores)
            {
                if (npc == null) throw new ArgumentException("Missing score.", nameof(npcScores));
                WebStudyHouse.CheckFinite(npc.score, nameof(npcScores));
                all.Add(npc);
            }
            // Enumerable.OrderByDescending is stable, matching JS stable sort with player first.
            return all.OrderByDescending(item => item.score).Select((item, index) => new WebMiniGamePlacement {
                id = item.houseguestId, name = item.name, position = index + 1, score = item.score, isPlayer = item.isPlayer }).ToList();
        }

        private static double EffectiveStat(ContestantStats stats, string type)
        {
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            double value;
            switch (type)
            {
                case "physical": value = stats.physical; break;
                case "mental": value = stats.mental; break;
                case "endurance": value = stats.endurance; break;
                case "social": value = stats.social; break;
                case "luck": value = 5 + (stats.luck - 5) * 0.2; break;
                default: throw new ArgumentException("Unknown source competition type.", nameof(type));
            }
            WebStudyHouse.CheckFinite(value, nameof(stats));
            return value;
        }
    }
}
