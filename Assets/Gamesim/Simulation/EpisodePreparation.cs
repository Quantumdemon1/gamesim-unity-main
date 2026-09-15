using System.Linq;

namespace Gamesim.Simulation
{
    public sealed partial class EpisodeEngine
    {
        private static void StudyHouse(EpisodeState s, EpisodeCommand c)
        {
            // Native availability/cost adapter: a committed Social action, not the source UI's
            // unguarded targetless dialog. Room proximity belongs to the presentation authority.
            Require(s.phase == EpisodePhase.Social, "Study preparation is available only during free time.");
            Require(s.Find(s.playerId).status == ContestantStatus.Active, "Only an active player can study.");
            Require(s.pendingDiary == null, "Resolve or skip the pending diary reflection before studying.");
            Require(s.socialActions < 18, "This social window is complete. Continue the episode.");
            Require(c.targetId == "memorize-layout" || c.targetId == "sneak-peek", "Choose a supported study approach.");
            // Native cast stores traits, not source personalityTraits. Do not merge those fields.
            var plan = WebStudyHouse.PlanStudy(s.playerStudyBonus, c.targetId, null, () => Roll(s));
            s.playerStudyBonus = plan.studyBonus;
            s.socialActions++;
            string result = c.targetId == "memorize-layout" ? "You studied the house; preparation increased by one, up to its cap." :
                plan.success ? "Your risky preparation attempt succeeded." : "Your risky preparation attempt failed; study progress was reduced, down to its floor.";
            Log(s, "study-house", result + " Preparation: " + s.playerStudyBonus + "/5. One social action spent.", s.playerId);
        }

        private static void SimulateWeeklyCompetition(EpisodeState s, EpisodeCommand c)
        {
            Require((s.phase == EpisodePhase.HoH || s.phase == EpisodePhase.Veto) && !s.competitionResolved,
                "Simulation is available only for an unresolved weekly HoH or Veto.");
            Require(c.performance == 0, "A simulated competition does not accept precision performance input.");
            var players = CompetitionPlayers(s).ToArray();
            Require(players.Any(contestant => contestant.isPlayer), "Only an eligible player can choose this simulation mode.");
            // Source weighted arithmetic and raw fast-forward caller bonus. The category cycle and
            // persisted RNG are the existing native scenario adapter, not original full-run replay.
            string category = s.week % 3 == 1 ? "Skill" : s.week % 3 == 2 ? "Mental" : "Endurance";
            double playerBonus = WebStudyHouse.FastForwardCompetitionBonus(s.phaseEventCompBonus, s.playerStudyBonus);
            s.competitionScores.Clear();
            foreach (var contestant in players)
            {
                double score = WebRules.WeightedCompetitionScore(contestant.stats, category,
                    s.nominees.Contains(contestant.id), contestant.isPlayer ? playerBonus : 0, Roll(s), 0);
                s.competitionScores.Add(new CompetitionScore { contestantId = contestant.id, score = score });
            }
            string winner = s.competitionScores.OrderByDescending(item => item.score).First().contestantId;
            if (s.phase == EpisodePhase.HoH) { s.hohId = winner; s.Find(winner).hohWins++; }
            else { s.vetoHolderId = winner; s.Find(winner).vetoWins++; }
            s.competitionResolved = true;
            Log(s, "competition", "Competition winner: " + Name(s, winner) + " · " + category + " (simulated).");
        }
    }
}
