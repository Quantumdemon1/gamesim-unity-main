#if !UNITY_5_3_OR_NEWER
// Unity-free only (Tools/SimulationTests): Unity's test runner executes [Explicit] tests in a batch run, so this whole-season
// soak is compiled out of the editor's assemblies and run by name with dotnet test.
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The trivia competition's soak: fresh seasons the game starts, at competition rules 6 (written again by name), each
    /// played to its end by the engine tests' walker. It says in which weeks the trivia is dealt (never before week 4, never
    /// before three evictions), in how many seasons, and how its wins spread: the player (who plays every competition at
    /// half marks), and the winner's rank by the mental statistic in the field. Every quiz it deals has five questions.
    /// </summary>
    public sealed class CompetitionTriviaSeasonSoak
    {
        private const string Why = "The trivia competition's whole-season soak, forty seasons a cast size. Run by name.";

        [Test, Explicit(Why)] public void TenHousematesFortySeasons() => Seasons(10, 4101u);
        [Test, Explicit(Why)] public void TwelveHousematesFortySeasons() => Seasons(12, 4201u);

        private static void Seasons(int houseSize, uint first)
        {
            var weeks = new SortedDictionary<int, int>();
            int seasons = 0, withTrivia = 0, dealt = 0, playerWins = 0, playerIn = 0, topMental = 0;
            var rankWins = new SortedDictionary<int, int>();
            for (uint seed = first; seed < first + 40; seed++)
            {
                var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = houseSize }, seed);
                ShippedRules.ApplyFresh(state);
                state.competitionRulesVersion = CompetitionRules.Trivia;
                var engine = new EpisodeEngine(state);
                var seen = new HashSet<string>();
                int before = dealt;
                for (int step = 0; step < 20000 && engine.Snapshot.phase != EpisodePhase.Finished; step++)
                {
                    var s = engine.Snapshot;
                    bool trivia = EpisodeEngine.IsCompetition(s.phase) && !s.competitionResolved
                        && CompetitionDefinitions.For(s) == CompetitionDefinitions.Trivia && seen.Add(s.week + "/" + s.phase);
                    if (trivia)
                    {
                        Assert.That(s.week, Is.GreaterThanOrEqualTo(CompetitionTrivia.EarliestWeek), "seed " + seed);
                        Assert.That(s.ledger.power.Count(r => r.week < s.week && r.evicteeId != null), Is.GreaterThanOrEqualTo(CompetitionTrivia.EvictionsFirst), "seed " + seed);
                        Assert.That(CompetitionTrivia.Quiz(s).Count, Is.EqualTo(CompetitionTrivia.Questions), "seed " + seed);
                        weeks[s.week] = weeks.TryGetValue(s.week, out int n) ? n + 1 : 1;
                        dealt++;
                    }
                    var result = engine.Apply(AllWeekSeasonDigests.Walk(s));
                    Assert.That(result.accepted, Is.True, "seed " + seed + " step " + step + ": " + result.reason);
                    if (!trivia) continue;
                    var after = engine.Snapshot;
                    string winner = s.phase == EpisodePhase.HoH ? after.hohId : after.vetoHolderId;
                    var field = after.competitionScores.Select(c => after.Find(c.contestantId)).ToList();
                    if (field.Any(c => c.isPlayer)) playerIn++;
                    if (winner == after.playerId) playerWins++;
                    int rank = 1 + field.Count(c => c.stats.mental > after.Find(winner).stats.mental);
                    rankWins[rank] = rankWins.TryGetValue(rank, out int r) ? r + 1 : 1;
                    if (rank == 1) topMental++;
                }
                Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished), "seed " + seed + " reached its end");
                seasons++;
                if (dealt > before) withTrivia++;
                TestContext.WriteLine("SOAK " + houseSize + "/" + seed + ": weeks " + engine.Snapshot.week + ", trivia " + (dealt - before));
            }
            TestContext.WriteLine("SOAK TOTAL house " + houseSize + ": seasons " + seasons + ", with trivia " + withTrivia + ", trivia competitions " + dealt
                + "; by week " + string.Join(", ", weeks.Select(p => "w" + p.Key + " " + p.Value))
                + "; player in the field " + playerIn + ", player won " + playerWins
                + "; winner's mental rank in the field " + string.Join(", ", rankWins.Select(p => "#" + p.Key + " " + p.Value))
                + "; top mental won " + topMental);
            Assert.That(dealt, Is.GreaterThan(0));
            Assert.That(weeks.Keys.Min(), Is.GreaterThanOrEqualTo(CompetitionTrivia.EarliestWeek));
        }
    }
}
#endif
