using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    [Serializable] public sealed class WebEnduranceElimination
    {
        public string contestantId;
        public double time;
    }

    [Serializable] public sealed class WebEnduranceResult
    {
        public string winnerId;
        public List<CompetitionScore> scores = new List<CompetitionScore>();
        public List<WebEnduranceElimination> eliminationOrder = new List<WebEnduranceElimination>();
    }

    /// <summary>Pure runEnduranceCompetition port from the web competition runner.</summary>
    public static class WebEnduranceCompetition
    {
        public static WebEnduranceResult Run(IReadOnlyList<ContestantState> participants, Func<double> random)
        {
            if (participants == null || participants.Count == 0) throw new ArgumentException("Competition requires at least one participant.", nameof(participants));
            if (participants.Any(c => c == null || c.stats == null) || participants.Select(c => c.id).Distinct().Count() != participants.Count)
                throw new ArgumentException("Participants require distinct identities and stats.", nameof(participants));
            if (random == null) throw new ArgumentNullException(nameof(random));
            foreach (var participant in participants)
                if (!Finite(participant.stats.physical) || !Finite(participant.stats.endurance))
                    throw new ArgumentException("Endurance stats must be finite.", nameof(participants));

            // The source picks an Endurance competition name first. Preserve that RNG draw even
            // when the native presentation uses a fixed title; presentation delays draw nothing.
            Next(random);
            var remaining = participants.ToList();
            var result = new WebEnduranceResult();
            double time = 0;
            while (remaining.Count > 1)
            {
                time += 10 + Next(random) * 20;
                var chances = remaining.Select(contestant => new
                {
                    contestant,
                    chance = (contestant.stats.endurance + contestant.stats.physical * 0.3) * (0.5 + Next(random) * 0.5)
                }).ToArray();
                // LINQ's stable ordering matches JS stable sort: equal survival eliminates the
                // earliest remaining participant, not the highest ID or the last tied candidate.
                var eliminated = chances.OrderBy(entry => entry.chance).First().contestant;
                result.eliminationOrder.Add(new WebEnduranceElimination { contestantId = eliminated.id, time = time });
                remaining.Remove(eliminated);
            }
            result.winnerId = remaining[0].id;
            result.scores.Add(new CompetitionScore { contestantId = result.winnerId, score = time + 10 });
            result.scores.AddRange(result.eliminationOrder.AsEnumerable().Reverse().Select(elimination =>
                new CompetitionScore { contestantId = elimination.contestantId, score = elimination.time }));
            return result;
        }

        private static double Next(Func<double> random)
        {
            double value = random();
            if (!Finite(value) || value < 0 || value >= 1) throw new ArgumentOutOfRangeException(nameof(random), "Random samples must be in [0,1).");
            return value;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
