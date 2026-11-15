using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The competition show's plan (competition show, slice 1): an endurance competition decided
    /// without the ranked game is staged from the standings already committed - the field at its
    /// stations, the drops in the reverse order of the standings, the winner the engine named last
    /// on - and the plan reads the season without writing it or drawing from its generator, so every
    /// seeded season commits and replays exactly as it did. Unity-free: the subset runs it too.
    /// </summary>
    public sealed class CompetitionShowPlanTests
    {
        private static readonly string[] Five = { "Skill", "Mental", "Endurance", "Luck", "Social" };

        [TestCase(EpisodeCommandKind.SimulateCompetition, 0d)]
        [TestCase(EpisodeCommandKind.ThrowCompetition, 0d)]
        [TestCase(EpisodeCommandKind.Compete, .5d)]
        public void TheShowPlaysTheCommittedStandingsFromTheBottomAndTheEnginesWinnerIsLastOn(EpisodeCommandKind way, double performance)
        {
            var before = EnduranceHoH();
            var field = EpisodeEngine.CompetitionPlayers(before).Select(actor => actor.id).ToList();
            var after = Apply(before, way, performance);
            var plan = CompetitionShowPlan.For(after, before.phase, EpisodeEngine.CompetitionCategory(before));
            Assert.That(plan, Is.Not.Null, way + ": an endurance competition is shown.");
            Assert.That(plan.Standings, Is.EquivalentTo(field), "Every competitor the engine scored stands at a station, and nobody else.");
            Assert.That(plan.Standings, Is.EqualTo(after.competitionScores.OrderByDescending(entry => entry.score).Select(entry => entry.contestantId).ToList()),
                "Best first, as the standings card sorts them.");
            Assert.That(plan.WinnerId, Is.EqualTo(after.hohId), "The last one holding on is the Head of Household the commit named.");
            Assert.That(plan.DropOrder, Is.EqualTo(plan.Standings.Skip(1).Reverse().ToList()), "The drops are the standings from the bottom, the winner never among them.");
            Assert.That(plan.PlayerInField, Is.True, "The player competed, so the player's body takes a station like the others.");
            Assert.That(plan.Phase, Is.EqualTo(EpisodePhase.HoH));
        }

        [Test]
        public void FinalPartOneIsShownFromItsDropTimesAndTiesKeepTheirWrittenOrder()
        {
            var state = ContentCatalog.Create(7);
            var ids = state.Active.Take(4).Select(actor => actor.id).ToArray();
            state.competitionScores = new List<CompetitionScore>
            {
                new CompetitionScore { contestantId = ids[0], score = 40 },
                new CompetitionScore { contestantId = ids[1], score = 70 },
                new CompetitionScore { contestantId = ids[2], score = 40 },
                new CompetitionScore { contestantId = ids[3], score = 20 },
            };
            string category = EpisodeEngine.CompetitionCategory(EpisodePhase.FinalHoHPart1, 9, 4, state.seed);
            Assert.That(category, Is.EqualTo("Endurance"));
            var plan = CompetitionShowPlan.For(state, EpisodePhase.FinalHoHPart1, category);
            Assert.That(plan.WinnerId, Is.EqualTo(ids[1]), "The longest time is the winner.");
            Assert.That(plan.Standings, Is.EqualTo(new[] { ids[1], ids[0], ids[2], ids[3] }), "Tied times keep the order the engine wrote them in, as the card does.");
            Assert.That(plan.DropOrder, Is.EqualTo(new[] { ids[3], ids[2], ids[0] }));
            Assert.That(plan.Phase, Is.EqualTo(EpisodePhase.FinalHoHPart1), "The show names the part it played, though the state has moved on.");
        }

        [Test]
        public void OnlyEnduranceIsShownAndTheOtherKindsKeepTheirCardAtOnce()
        {
            var state = ContentCatalog.Create(3);
            state.competitionScores = state.Active.Take(3).Select((actor, i) => new CompetitionScore { contestantId = actor.id, score = i }).ToList();
            foreach (string kind in Five)
                Assert.That(CompetitionShowPlan.For(state, EpisodePhase.HoH, kind) != null, Is.EqualTo(kind == "Endurance"), kind);
            Assert.That(CompetitionShowPlan.Shows(EpisodeEngine.CompetitionCategory(EpisodePhase.FinalHoHPart1, 9, 4, 1)), Is.True, "Part 1 is an endurance show.");
            Assert.That(CompetitionShowPlan.Shows(EpisodeEngine.CompetitionCategory(EpisodePhase.FinalHoHPart2, 9, 4, 1)), Is.False);
            Assert.That(CompetitionShowPlan.Shows(EpisodeEngine.CompetitionCategory(EpisodePhase.FinalHoHPart3, 9, 4, 1)), Is.False);
            state.competitionScores = state.competitionScores.Take(1).ToList();
            Assert.That(CompetitionShowPlan.For(state, EpisodePhase.HoH, "Endurance"), Is.Null, "One person is no show.");
            Assert.That(CompetitionShowPlan.For(null, EpisodePhase.HoH, "Endurance"), Is.Null);
        }

        [Test]
        public void TheShowReadsTheSeasonAndNeitherWritesItNorDrawsFromIt()
        {
            var before = EnduranceHoH();
            string beforeJson = Json(before);
            var after = Apply(before, EpisodeCommandKind.SimulateCompetition, 0);
            var replay = Apply(before.Clone(), EpisodeCommandKind.SimulateCompetition, 0);
            string afterJson = Json(after);
            var random = after.randomState;
            foreach (bool quick in new[] { false, true })
            {
                var plan = CompetitionShowPlan.For(after, before.phase, EpisodeEngine.CompetitionCategory(before));
                for (float t = 0f; t < plan.CardAt(quick, false) + 1f; t += .25f)
                    foreach (var id in plan.Standings) plan.Strain(id, t, quick);
                for (int i = 0; i < plan.DropOrder.Count; i++) plan.DropAt(i, quick);
                plan.CheerAt(quick); plan.HardSeconds(quick, true);
            }
            Assert.That(Json(after), Is.EqualTo(afterJson), "Planning and timing the show writes nothing into the committed state.");
            Assert.That(after.randomState, Is.EqualTo(random), "The show draws nothing from the season's generator.");
            Assert.That(Json(before), Is.EqualTo(beforeJson));
            Assert.That(Json(replay), Is.EqualTo(afterJson), "The same command on the same season commits the same result, byte for byte: the show plays after it, never inside it.");
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(6)]
        [TestCase(12)]
        [TestCase(16)]
        public void TheBeatsFitTheShowsWindowAndTheEffortPeaksAtEachDrop(int fieldSize)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice(), 11);
            var ids = Enumerable.Range(0, fieldSize).Select(i => i < state.contestants.Count ? state.contestants[i].id : "extra-" + i).ToList();
            while (state.contestants.Count < fieldSize)
                state.contestants.Add(new ContestantState { id = ids[state.contestants.Count], name = "Extra " + state.contestants.Count, status = ContestantStatus.Active, stats = new ContestantStats() });
            state.competitionScores = ids.Select((id, i) => new CompetitionScore { contestantId = id, score = 100 - i }).ToList();
            var plan = CompetitionShowPlan.For(state, EpisodePhase.HoH, "Endurance");
            Assert.That(plan.Standings, Has.Count.EqualTo(fieldSize));
            float slow = plan.CardAt(false, false), quick = plan.CardAt(true, false);
            Assert.That(slow, Is.InRange(5f, 15f), "About ten to fifteen seconds once the field is at its stations.");
            Assert.That(quick, Is.LessThan(slow), "The quick pace is quicker.");
            Assert.That(plan.CardAt(false, true), Is.LessThan(quick), "Reduced motion only holds a still pose before the card.");
            foreach (bool pace in new[] { false, true })
            {
                float last = 0f;
                for (int i = 0; i < plan.DropOrder.Count; i++)
                {
                    float at = plan.DropAt(i, pace);
                    Assert.That(at, Is.GreaterThan(last), "Each drop after the last.");
                    Assert.That(plan.Strain(plan.DropOrder[i], at, pace), Is.EqualTo(1f).Within(1e-5), "Effort peaks as they let go.");
                    Assert.That(plan.Strain(plan.DropOrder[i], at * .5f, pace), Is.LessThan(1f), "and grows toward it.");
                    last = at;
                }
                Assert.That(plan.CheerAt(pace), Is.GreaterThan(last), "The winner cheers after the last drop.");
                Assert.That(plan.Strain(plan.WinnerId, plan.CheerAt(pace), pace), Is.LessThan(1f).And.GreaterThan(.5f), "The winner never gives out.");
                Assert.That(plan.HardSeconds(pace, false), Is.GreaterThan(CompetitionShowPlan.AssemblySeconds(pace) + plan.CardAt(pace, false)));
            }
        }

        [Test]
        public void TheLargestFieldPlaysInAboutTwentySecondsAndItsBoundWaitsForTheWinner()
        {
            var plan = CompetitionShowPlan.For(FieldOf(16), EpisodePhase.HoH, "Endurance");
            foreach (bool quick in new[] { false, true })
            {
                float assembled = CompetitionShowPlan.AssemblySeconds(quick) + plan.CardAt(quick, false);
                Assert.That(assembled, Is.LessThanOrEqualTo(20f), "The whole field at its stations by the walk out's end: within twenty seconds.");
                Assert.That(CompetitionShowPlan.PrincipalSeconds(quick), Is.GreaterThan(CompetitionShowPlan.AssemblySeconds(quick)),
                    "A late winner is waited for past the walk out.");
                Assert.That(plan.HardSeconds(quick, false), Is.GreaterThanOrEqualTo(CompetitionShowPlan.PrincipalSeconds(quick) + plan.CardAt(quick, false)),
                    "A hold that starts at the last moment the winner is waited for still reaches its cheer and card.");
                Assert.That(plan.HardSeconds(quick, false), Is.LessThanOrEqualTo(22.5f), "and the bound stays near the twenty.");
            }
        }

        [Test]
        public void ThoseLeftOutNeitherLetGoNorMoveTheOthersOrder()
        {
            var plan = CompetitionShowPlan.For(FieldOf(8), EpisodePhase.HoH, "Endurance");
            var holding = new HashSet<string>(plan.Standings.Where((id, i) => i % 3 != 2));
            var order = plan.DropOrderAmong(holding);
            Assert.That(order, Is.EqualTo(plan.DropOrder.Where(holding.Contains).ToList()), "The standings' order, from the bottom, among those holding on.");
            Assert.That(order, Does.Not.Contain(plan.WinnerId));
            Assert.That(plan.DropOrderAmong(new HashSet<string>(plan.Standings)), Is.EqualTo(plan.DropOrder), "The whole field holding on drops as planned.");
            foreach (bool quick in new[] { false, true })
            {
                for (int i = 0; i < order.Count; i++)
                    Assert.That(plan.Strain(order, order[i], CompetitionShowPlan.DropAt(i, order.Count, quick), quick), Is.EqualTo(1f).Within(1e-5),
                        "Effort peaks as each of those holding on lets go.");
                Assert.That(CompetitionShowPlan.CardAt(order.Count, quick, false), Is.LessThanOrEqualTo(plan.CardAt(quick, false)), "A smaller hold is no longer.");
            }
        }

        // ------------------------------------------------------------ fixtures

        private static EpisodeState FieldOf(int fieldSize)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice(), 11);
            var ids = Enumerable.Range(0, fieldSize).Select(i => i < state.contestants.Count ? state.contestants[i].id : "extra-" + i).ToList();
            while (state.contestants.Count < fieldSize)
                state.contestants.Add(new ContestantState { id = ids[state.contestants.Count], name = "Extra " + state.contestants.Count, status = ContestantStatus.Active, stats = new ContestantStats() });
            state.competitionScores = ids.Select((id, i) => new CompetitionScore { contestantId = id, score = 100 - i }).ToList();
            return state;
        }

        private static EpisodeState EnduranceHoH()
        {
            uint seed = 1;
            while (EpisodeEngine.CompetitionCategory(EpisodePhase.HoH, 1, 4, seed) != "Endurance") seed++;
            var state = ContentCatalog.Create(seed);
            state.competitionRulesVersion = 4; state.phase = EpisodePhase.HoH;
            Assert.That(EpisodeEngine.CompetitionCategory(state), Is.EqualTo("Endurance"));
            return state;
        }

        private static string Json(object value) => JsonConvert.SerializeObject(value);

        private static EpisodeState Apply(EpisodeState state, EpisodeCommandKind kind, double performance)
        {
            var result = new EpisodeEngine(state).Apply(new EpisodeCommand
            {
                id = "competition-show-" + kind, actorId = state.playerId, expectedPhase = state.phase,
                expectedRevision = state.revision, kind = kind, performance = performance,
            });
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }
    }
}
