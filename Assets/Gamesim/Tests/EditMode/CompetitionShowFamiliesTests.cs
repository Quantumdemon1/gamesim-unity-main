using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The staging families (competition staging, slice 3): every game the show knows is staged in
    /// the family its equipment belongs to - podium, table, course, or slice 1's hold - mapped by the
    /// game, never by its kind, so a game the show does not know commits straight to its card. Each
    /// family's beats are read from the committed standings and tell them in order, within a bound
    /// that holds for sixteen; planning reads the season and never writes it or draws from it.
    /// Unity-free: the subset runs it too.
    /// </summary>
    public sealed class CompetitionShowFamiliesTests
    {
        private static readonly string[] Five = { "Skill", "Mental", "Endurance", "Luck", "Social" };

        [Test]
        public void EveryGameInTheCatalogueIsStagedInTheFamilyOfItsEquipment()
        {
            var expected = new Dictionary<CompetitionDefinition, CompetitionShowFamily>
            {
                { CompetitionDefinitions.HoldYourGround, CompetitionShowFamily.Hold },
                { CompetitionDefinitions.PressureCooker, CompetitionShowFamily.Hold },
                { CompetitionDefinitions.HouseMemory, CompetitionShowFamily.Podium },
                { CompetitionDefinitions.FirstImpressions, CompetitionShowFamily.Podium },
                { CompetitionDefinitions.RollTheDice, CompetitionShowFamily.Table },
                { CompetitionDefinitions.WordScramble, CompetitionShowFamily.Table },
                { CompetitionDefinitions.HouseguestScramble, CompetitionShowFamily.Table },
                { CompetitionDefinitions.SignalSprint, CompetitionShowFamily.Course },
                { CompetitionDefinitions.SwitchbackSignals, CompetitionShowFamily.Course },
            };
            Assert.That(expected.Keys, Is.EquivalentTo(CompetitionDefinitions.All), "Every game of the catalogue has its family decided here.");
            foreach (var pair in expected)
                Assert.That(CompetitionShowFamilies.For(pair.Key.Id, pair.Key.Category), Is.EqualTo(pair.Value), pair.Key.Title);
            Assert.That(CompetitionShowFamilies.Games.Keys, Is.EquivalentTo(CompetitionDefinitions.All.Select(game => game.Id)), "The show knows these games and no others.");
        }

        [TestCase("Skill")]
        [TestCase("Mental")]
        [TestCase("Endurance")]
        [TestCase("Luck")]
        [TestCase("Social")]
        public void AGameTheShowDoesNotKnowCommitsToItsCardWhateverItsKind(string kind)
        {
            var state = FieldOf(6);
            Assert.That(CompetitionShowFamilies.For("ring-tower-v5", kind), Is.Null, "A new game is never given a family by its kind.");
            Assert.That(CompetitionShowPlan.For(state, EpisodePhase.HoH, kind, "ring-tower-v5"), Is.Null, "No show: the card plays at once.");
            Assert.That(CompetitionShowPlan.For(state, EpisodePhase.HoH, kind, ""), Is.Null);
        }

        [Test]
        public void WithoutAuthoredGamesOnlyEnduranceIsStagedAsSliceOneStagedIt()
        {
            var state = FieldOf(4);
            foreach (string kind in Five)
            {
                var plan = CompetitionShowPlan.For(state, EpisodePhase.HoH, kind, null);
                Assert.That(plan != null, Is.EqualTo(kind == "Endurance"), kind + ": a season before rules 3 has no games to map.");
                if (plan != null) Assert.That(plan.Family, Is.EqualTo(CompetitionShowFamily.Hold));
            }
            Assert.That(CompetitionShowPlan.For(state, EpisodePhase.HoH, "Endurance").Family, Is.EqualTo(CompetitionShowFamily.Hold), "Slice 1's own plan is a hold.");
        }

        [TestCase("Skill", CompetitionShowFamily.Course)]
        [TestCase("Mental", CompetitionShowFamily.Podium)]
        [TestCase("Endurance", CompetitionShowFamily.Hold)]
        [TestCase("Luck", CompetitionShowFamily.Table)]
        [TestCase("Social", CompetitionShowFamily.Table)]
        public void ASimulatedRules4CompetitionIsStagedInItsGamesFamilyFromItsCommittedStandings(string kind, CompetitionShowFamily family)
        {
            var before = HoHOf(kind);
            var game = CompetitionDefinitions.For(before);
            Assert.That(game, Is.Not.Null);
            var after = Apply(before, EpisodeCommandKind.SimulateCompetition);
            var plan = CompetitionShowPlan.For(after, before.phase, kind, game.Id);
            Assert.That(plan, Is.Not.Null, kind + " (" + game.Title + ") is staged.");
            Assert.That(plan.Family, Is.EqualTo(family));
            Assert.That(plan.Category, Is.EqualTo(kind));
            Assert.That(plan.Standings, Is.EqualTo(after.competitionScores.OrderByDescending(entry => entry.score).Select(entry => entry.contestantId).ToList()),
                "Best first, as the standings card sorts them.");
            Assert.That(plan.WinnerId, Is.EqualTo(after.hohId), "The show's winner is the Head of Household the commit named.");
            Assert.That(plan.PlayerInField, Is.True);
        }

        [TestCase(CompetitionShowFamily.Podium, 2)]
        [TestCase(CompetitionShowFamily.Podium, 3)]
        [TestCase(CompetitionShowFamily.Podium, 8)]
        [TestCase(CompetitionShowFamily.Podium, 16)]
        [TestCase(CompetitionShowFamily.Table, 2)]
        [TestCase(CompetitionShowFamily.Table, 3)]
        [TestCase(CompetitionShowFamily.Table, 8)]
        [TestCase(CompetitionShowFamily.Table, 16)]
        [TestCase(CompetitionShowFamily.Course, 2)]
        [TestCase(CompetitionShowFamily.Course, 3)]
        [TestCase(CompetitionShowFamily.Course, 8)]
        [TestCase(CompetitionShowFamily.Course, 16)]
        public void TheBeatsTellTheCommittedStandingsInOrder(CompetitionShowFamily family, int fieldSize)
        {
            var standings = FieldOf(fieldSize).competitionScores.Select(entry => entry.contestantId).ToList();
            foreach (bool quick in new[] { false, true })
            {
                var timeline = CompetitionShowTimeline.For(family, standings, quick);
                Assert.That(timeline, Is.Not.Null);
                var beats = timeline.Beats;
                for (int i = 1; i < beats.Count; i++) Assert.That(beats[i].At, Is.GreaterThanOrEqualTo(beats[i - 1].At), "In time order: " + beats[i]);
                Assert.That(beats.Last().Kind, Is.EqualTo(CompetitionShowBeatKind.Card), "The standings card ends the show.");
                Assert.That(beats.Last().At, Is.EqualTo(timeline.CardAt));
                Assert.That(beats.Count(beat => beat.Kind == CompetitionShowBeatKind.Cheer), Is.LessThanOrEqualTo(1));
                switch (family)
                {
                    case CompetitionShowFamily.Podium:
                        var reveals = beats.Where(beat => beat.Kind == CompetitionShowBeatKind.Reveal).ToList();
                        Assert.That(reveals.SelectMany(beat => beat.Ids), Is.EqualTo(standings.Skip(1).Reverse()), "Out from the bottom of the standings up, the winner never.");
                        Assert.That(reveals.Last().Ids, Is.EqualTo(new[] { standings[1] }), "The runner-up goes out alone in the last round.");
                        foreach (var reveal in reveals)
                        {
                            var question = beats.Single(beat => beat.Kind == CompetitionShowBeatKind.Question && beat.Round == reveal.Round);
                            var lockIn = beats.Single(beat => beat.Kind == CompetitionShowBeatKind.LockIn && beat.Round == reveal.Round);
                            Assert.That(question.At, Is.LessThan(lockIn.At)); Assert.That(lockIn.At, Is.LessThan(reveal.At));
                            Assert.That(lockIn.Ids, Is.SupersetOf(reveal.Ids), "Those going out locked an answer in that round.");
                            Assert.That(lockIn.Ids, Does.Contain(standings[0]), "The winner answers every round.");
                        }
                        var cheer = beats.Single(beat => beat.Kind == CompetitionShowBeatKind.Cheer);
                        Assert.That(cheer.Ids, Is.EqualTo(new[] { standings[0] }));
                        Assert.That(cheer.At, Is.GreaterThan(reveals.Last().At), "The winner cheers once the last one is out.");
                        Assert.That(timeline.Order, Is.EqualTo(standings.Skip(1).Reverse().Concat(new[] { standings[0] })));
                        break;
                    case CompetitionShowFamily.Table:
                        var finishes = beats.Where(beat => beat.Kind == CompetitionShowBeatKind.Finish).ToList();
                        Assert.That(finishes.SelectMany(beat => beat.Ids), Is.EqualTo(standings.Take(fieldSize - 1)), "They finish in the standings' order, the winner first.");
                        for (int i = 1; i < finishes.Count; i++) Assert.That(finishes[i].At, Is.GreaterThan(finishes[i - 1].At));
                        var timeUp = beats.Single(beat => beat.Kind == CompetitionShowBeatKind.TimeUp);
                        Assert.That(timeUp.Ids, Is.EqualTo(new[] { standings.Last() }), "Time is called on the last place.");
                        Assert.That(timeUp.At, Is.GreaterThan(finishes.Last().At));
                        Assert.That(beats.Single(beat => beat.Kind == CompetitionShowBeatKind.Cheer).At, Is.EqualTo(finishes[0].At), "The winner cheers as they finish.");
                        Assert.That(beats.First().Kind, Is.EqualTo(CompetitionShowBeatKind.Work));
                        Assert.That(beats.First().Ids, Is.EquivalentTo(standings), "Everyone works.");
                        break;
                    case CompetitionShowFamily.Course:
                        var turns = beats.Where(beat => beat.Kind == CompetitionShowBeatKind.Turn).ToList();
                        Assert.That(turns.SelectMany(beat => beat.Ids), Is.EqualTo(standings), "They turn for home in the standings' order, the winner first.");
                        float lastBy = 0f;
                        foreach (var id in standings)
                        {
                            float by = timeline.FinishBy(id);
                            Assert.That(by, Is.GreaterThan(lastBy), "Each has longer to get home than the one above them.");
                            lastBy = by;
                        }
                        Assert.That(beats[0].Kind, Is.EqualTo(CompetitionShowBeatKind.Go));
                        Assert.That(beats[1].Kind, Is.EqualTo(CompetitionShowBeatKind.Depart));
                        Assert.That(turns[0].At, Is.GreaterThan(beats[1].At));
                        Assert.That(timeline.CardAt, Is.GreaterThan(lastBy));
                        Assert.That(float.IsNaN(timeline.FinishBy("nobody")), Is.True);
                        break;
                }
            }
        }

        [TestCase(CompetitionShowFamily.Podium)]
        [TestCase(CompetitionShowFamily.Table)]
        [TestCase(CompetitionShowFamily.Course)]
        public void SixteenPlayWithinTheHardBoundAndQuickIsQuicker(CompetitionShowFamily family)
        {
            var plan = PlanOf(16, family);
            Assert.That(plan.Family, Is.EqualTo(family));
            foreach (bool quick in new[] { false, true })
            {
                float card = plan.Timeline(quick).CardAt;
                Assert.That(card, Is.LessThanOrEqualTo(15f), "Once the field is at its stations, fifteen seconds at most.");
                Assert.That(plan.HardSeconds(quick, false), Is.GreaterThanOrEqualTo(CompetitionShowPlan.PrincipalSeconds(quick) + card),
                    "A show that starts at the last moment the winner is waited for still reaches its card.");
                Assert.That(plan.HardSeconds(quick, false), Is.LessThanOrEqualTo(27f), "The bound holds for sixteen.");
                Assert.That(plan.HardSeconds(quick, true), Is.LessThan(plan.HardSeconds(quick, false)), "Reduced motion only holds a still field.");
            }
            Assert.That(plan.Timeline(true).CardAt, Is.LessThan(plan.Timeline(false).CardAt), "The quick pace is quicker.");
            Assert.That(PlanOf(2, family).Timeline(false).CardAt, Is.InRange(3f, 12f), "A pair still plays a short show.");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(15)]
        public void APodiumPutsOutTheWholeFieldButOneInFiveRoundsAtMost(int drops)
        {
            var sizes = CompetitionShowTimeline.RoundSizes(drops);
            Assert.That(sizes.Sum(), Is.EqualTo(drops));
            Assert.That(sizes.Count, Is.InRange(1, CompetitionShowTimeline.MostRounds));
            Assert.That(sizes.Last(), Is.EqualTo(1), "The runner-up goes out alone.");
            Assert.That(sizes.All(size => size >= 1), Is.True);
            for (int i = 1; i < sizes.Count - 1; i++) Assert.That(sizes[i], Is.LessThanOrEqualTo(sizes[i - 1]), "The early rounds take the most.");
        }

        [Test]
        public void ThoseNotAtTheirStationsAreNotStagedAndTheOthersKeepTheirOrder()
        {
            var standings = FieldOf(8).competitionScores.Select(entry => entry.contestantId).ToList();
            var staged = standings.Where((id, i) => i % 3 != 2).ToList();
            foreach (var family in new[] { CompetitionShowFamily.Podium, CompetitionShowFamily.Table, CompetitionShowFamily.Course })
            {
                var timeline = CompetitionShowTimeline.For(family, staged, false);
                Assert.That(timeline.Order, Is.EquivalentTo(staged), family + ": only those staged.");
                Assert.That(timeline.Beats.SelectMany(beat => beat.Ids).Distinct(), Is.SubsetOf(staged));
                Assert.That(timeline.WinnerId, Is.EqualTo(standings[0]));
            }
            Assert.That(CompetitionShowTimeline.For(CompetitionShowFamily.Podium, standings.Take(1).ToList(), false), Is.Null, "One is no show.");
            Assert.That(CompetitionShowTimeline.For(CompetitionShowFamily.Hold, standings, false), Is.Null, "Slice 1 times the hold.");
        }

        [Test]
        public void StagingReadsTheSeasonAndNeitherWritesItNorDrawsFromIt()
        {
            foreach (string kind in new[] { "Skill", "Mental", "Luck" })
            {
                var before = HoHOf(kind);
                var after = Apply(before, EpisodeCommandKind.SimulateCompetition);
                var replay = Apply(before.Clone(), EpisodeCommandKind.SimulateCompetition);
                string afterJson = JsonConvert.SerializeObject(after);
                var random = after.randomState;
                var plan = CompetitionShowPlan.For(after, before.phase, kind, CompetitionDefinitions.For(before).Id);
                foreach (bool quick in new[] { false, true })
                {
                    var timeline = plan.Timeline(quick);
                    foreach (var id in plan.Standings) timeline.FinishBy(id);
                    plan.HardSeconds(quick, false); plan.HardSeconds(quick, true);
                }
                Assert.That(JsonConvert.SerializeObject(after), Is.EqualTo(afterJson), kind + ": staging writes nothing into the committed state.");
                Assert.That(after.randomState, Is.EqualTo(random), "and draws nothing from the season's generator.");
                Assert.That(JsonConvert.SerializeObject(replay), Is.EqualTo(afterJson), "The same command commits the same result, byte for byte: the show plays after it.");
            }
        }

        // ------------------------------------------------------------ fixtures

        private static CompetitionShowPlan PlanOf(int fieldSize, CompetitionShowFamily family)
        {
            string game = CompetitionShowFamilies.Games.First(pair => pair.Value == family).Key;
            return CompetitionShowPlan.For(FieldOf(fieldSize), EpisodePhase.HoH, CompetitionDefinitions.All.First(d => d.Id == game).Category, game);
        }

        private static EpisodeState FieldOf(int fieldSize)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice(), 11);
            var ids = Enumerable.Range(0, fieldSize).Select(i => i < state.contestants.Count ? state.contestants[i].id : "extra-" + i).ToList();
            while (state.contestants.Count < fieldSize)
                state.contestants.Add(new ContestantState { id = ids[state.contestants.Count], name = "Extra " + state.contestants.Count, status = ContestantStatus.Active, stats = new ContestantStats() });
            state.competitionScores = ids.Select((id, i) => new CompetitionScore { contestantId = id, score = 100 - i }).ToList();
            return state;
        }

        private static EpisodeState HoHOf(string kind)
        {
            // Pinned to rules 4 (Widened), as the cases' name says: a later rules version may deal a game the show does not know.
            uint seed = 1;
            while (EpisodeEngine.CompetitionCategory(EpisodePhase.HoH, 1, CompetitionRules.Widened, seed) != kind) seed++;
            var state = ContentCatalog.Create(seed);
            state.competitionRulesVersion = CompetitionRules.Widened; state.phase = EpisodePhase.HoH;
            Assert.That(EpisodeEngine.CompetitionCategory(state), Is.EqualTo(kind));
            return state;
        }

        private static EpisodeState Apply(EpisodeState state, EpisodeCommandKind kind)
        {
            var result = new EpisodeEngine(state).Apply(new EpisodeCommand
            {
                id = "competition-staging-" + kind, actorId = state.playerId, expectedPhase = state.phase,
                expectedRevision = state.revision, kind = kind,
            });
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }
    }
}
