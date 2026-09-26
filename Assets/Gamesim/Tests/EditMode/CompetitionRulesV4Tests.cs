using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Competition rules 4: five kinds dealt in a season's own order, luck and social scored, full
    /// marks worth three points, a throw that loses about nine times in ten and says why when it
    /// does not - and every earlier season left exactly as it was.
    /// </summary>
    public sealed class CompetitionRulesV4Tests
    {
        private static readonly string[] Five = { "Skill", "Mental", "Endurance", "Luck", "Social" };

        [Test]
        public void TheFiveKindsComeRoundOnceAsEachInEveryFiveWeeks()
        {
            var orders = new HashSet<string>();
            for (uint seed = 1; seed <= 40; seed++)
            {
                for (int start = 1; start <= 6; start += 5)
                {
                    var hoh = Enumerable.Range(start, 5).Select(week => EpisodeEngine.CompetitionCategory(EpisodePhase.HoH, week, 4, seed)).ToList();
                    var veto = Enumerable.Range(start, 5).Select(week => EpisodeEngine.CompetitionCategory(EpisodePhase.Veto, week, 4, seed)).ToList();
                    Assert.That(hoh, Is.EquivalentTo(Five), "Every kind is a Head of Household competition once in five weeks.");
                    Assert.That(veto, Is.EquivalentTo(Five), "and a veto once.");
                    for (int i = 0; i < 5; i++) Assert.That(veto[i], Is.Not.EqualTo(hoh[i]), "A week's two competitions are never the same kind.");
                }
                Assert.That(EpisodeEngine.CompetitionCategory(EpisodePhase.HoH, 3, 4, seed),
                    Is.EqualTo(EpisodeEngine.CompetitionCategory(EpisodePhase.HoH, 3, 4, seed)), "A reload deals the same season.");
                orders.Add(string.Join(",", Enumerable.Range(1, 5).Select(week => EpisodeEngine.CompetitionCategory(EpisodePhase.HoH, week, 4, seed))));
            }
            Assert.That(orders.Count, Is.GreaterThan(10), "Seasons deal the kinds in orders of their own, not one order for all.");
        }

        [Test]
        public void EarlierSeasonsKeepTheirThreeKinds()
        {
            for (uint seed = 1; seed <= 8; seed++)
                for (int week = 1; week <= 6; week++)
                {
                    string three = week % 3 == 1 ? "Skill" : week % 3 == 2 ? "Mental" : "Endurance";
                    string threeVeto = (week + 1) % 3 == 1 ? "Skill" : (week + 1) % 3 == 2 ? "Mental" : "Endurance";
                    Assert.That(EpisodeEngine.CompetitionCategory(EpisodePhase.HoH, week, 3, seed), Is.EqualTo(three));
                    Assert.That(EpisodeEngine.CompetitionCategory(EpisodePhase.Veto, week, 3, seed), Is.EqualTo(threeVeto));
                    Assert.That(EpisodeEngine.CompetitionCategory(EpisodePhase.Veto, week, 1, seed), Is.EqualTo(three), "Rules 1 had no veto offset.");
                }
            Assert.That(EpisodeEngine.CompetitionCategory(EpisodePhase.FinalHoHPart1, 9, 4, 5), Is.EqualTo("Endurance"), "The Final HoH keeps its rounds.");
            Assert.That(EpisodeEngine.CompetitionCategory(EpisodePhase.FinalHoHPart2, 9, 4, 5), Is.EqualTo("Skill"));
            Assert.That(EpisodeEngine.CompetitionCategory(EpisodePhase.FinalHoHPart3, 9, 4, 5), Is.EqualTo("Mental"));
        }

        [Test]
        public void LuckIsTheReferencesCrapshootWithTheLuckSqueezed()
        {
            var lucky = new ContestantStats { mental = 4, luck = 10, competition = 6 };
            var unlucky = new ContestantStats { mental = 4, luck = 0, competition = 6 };
            Assert.That(CompetitionRules.Score(lucky, "Luck", false, 0, .5, 0), Is.EqualTo(4 * .1 + 6 * .9).Within(1e-9),
                "Luck 10 counts as 6: squeezed toward five, as the reference's dice game squeezes it.");
            Assert.That(CompetitionRules.Score(lucky, "Luck", false, 0, .5, 0) - CompetitionRules.Score(unlucky, "Luck", false, 0, .5, 0),
                Is.EqualTo(10 * .2 * .9).Within(1e-9), "A lucky houseguest is favoured by a fifth of the gap, not crowned by all of it.");
            Assert.That(CompetitionRules.Score(lucky, "Luck", false, 0, .5, .5) - CompetitionRules.Score(lucky, "Luck", false, 0, .5, 0),
                Is.EqualTo(1.5).Within(1e-9), "The second roll is worth up to three.");
            Assert.That(CompetitionRules.Score(lucky, "Luck", true, 1, .5, 0) - CompetitionRules.Score(lucky, "Luck", false, 0, .5, 0),
                Is.EqualTo(3 + 1).Within(1e-9), "The nominee's clutch bonus and the player's bonus ride on top.");
            Assert.That(CompetitionRules.RollsTwice("Luck"), Is.True);
            Assert.That(Five.Where(kind => kind != "Luck").Any(CompetitionRules.RollsTwice), Is.False);
        }

        [Test]
        public void SocialWeighsSocialMentalAndLuck()
        {
            var stats = new ContestantStats { physical = 9, mental = 5, endurance = 9, social = 8, luck = 3, competition = 4 };
            Assert.That(CompetitionRules.Score(stats, "Social", false, 0, .5), Is.EqualTo(5 * .2 + 8 * .6 + 3 * .2).Within(1e-9),
                "Three fifths social, a fifth mental, a fifth luck; strength and stamina do not come into it.");
            Assert.That(CompetitionRules.Score(stats, "Social", false, 0, 0), Is.EqualTo((5 * .2 + 8 * .6 + 3 * .2) * .75).Within(1e-9),
                "The roll moves it a quarter either way, as every kind's roll does.");
            Assert.That(CompetitionRules.Score(stats, "Skill", true, 2, .3),
                Is.EqualTo(WebRules.WeightedCompetitionScore(stats, "Skill", true, 2, .3)), "The reference's three kinds are its weighted runner, unchanged.");
        }

        [TestCase(EpisodePhase.HoH)]
        [TestCase(EpisodePhase.Veto)]
        public void FullMarksAreWorthThreePointsAndTheRollsAreTheSame(EpisodePhase phase)
        {
            var state = Fixture(1708, phase);
            var none = Apply(state, EpisodeCommandKind.Compete, 0);
            var half = Apply(state, EpisodeCommandKind.Compete, .5);
            var full = Apply(state, EpisodeCommandKind.Compete, 1);
            Assert.That(full.randomState, Is.EqualTo(none.randomState), "Performance draws nothing.");
            foreach (var score in none.competitionScores)
            {
                double expected = score.contestantId == state.playerId ? 3 : 0;
                Assert.That(Score(full, score.contestantId) - score.score, Is.EqualTo(expected).Within(1e-9));
                Assert.That(Score(half, score.contestantId) - score.score, Is.EqualTo(expected / 2).Within(1e-9),
                    "The accessible alternative's half marks are worth one and a half.");
            }
            Assert.That(CompetitionRules.PerformanceWeight(3), Is.EqualTo(2), "Rules 3 and before keep two.");
        }

        [Test]
        public void EveryKindIsScoredByTheRulesAndDrawsItsRollsInOrder()
        {
            foreach (string kind in Five)
            {
                uint seed = SeedWhoseFirstHoHIs(kind);
                var state = Fixture(seed, EpisodePhase.HoH);
                Assert.That(EpisodeEngine.CompetitionCategory(state), Is.EqualTo(kind));
                var result = Apply(state, EpisodeCommandKind.Compete, .4);
                var random = new SeededRandom(state.randomState);
                foreach (var actor in EpisodeEngine.CompetitionPlayers(state))
                {
                    double roll = random.NextDouble();
                    double second = kind == "Luck" ? random.NextDouble() : 0;
                    double bonus = actor.isPlayer ? EpisodeEngine.CommonCompetitionBonus(state) + .4 * 3 : 0;
                    Assert.That(Score(result, actor.id), Is.EqualTo(CompetitionRules.Score(actor.stats, kind, state.nominees.Contains(actor.id), bonus, roll, second)),
                        kind + ": " + actor.name);
                }
                Assert.That(result.randomState, Is.EqualTo(random.State), kind + " draws one roll a competitor, two for luck, and nothing else.");
                var simulated = Apply(state, EpisodeCommandKind.SimulateCompetition, 0);
                Assert.That(simulated.randomState, Is.EqualTo(random.State), kind + ": simulating draws what playing draws.");
            }
        }

        [Test]
        public void TheExplanationAddsUpForEveryKindAndEveryWayIn()
        {
            foreach (string kind in Five)
            {
                var state = Fixture(SeedWhoseFirstHoHIs(kind), EpisodePhase.HoH);
                state.nominees.Clear();
                var player = state.Find(state.playerId);
                player.stats.physical = 3.173; player.stats.mental = 6.891; player.stats.endurance = 7.047;
                player.stats.social = 2.515; player.stats.luck = 8.869;
                foreach (var way in new[] { EpisodeCommandKind.Compete, EpisodeCommandKind.SimulateCompetition, EpisodeCommandKind.ThrowCompetition })
                {
                    var result = Apply(state, way, way == EpisodeCommandKind.Compete ? .371 : 0);
                    string text = result.events.Last(e => e.kind == "competition-performance").text;
                    string terms = text.Substring(text.IndexOf("Your weighted stats:", StringComparison.Ordinal));
                    var values = Regex.Matches(terms, @"(?:physical|mental|endurance|social|luck|Chance|second roll|nominee|preparation|event|storyline|performance|throw|rounding) ([+-]?\d+(?:\.\d+)?)")
                        .Cast<Match>().Select(m => decimal.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToArray();
                    int expectedTerms = 5 + 1 + (kind == "Luck" ? 1 : 0) + 1 + (way == EpisodeCommandKind.ThrowCompetition ? 1 : 4) + 1;
                    Assert.That(values.Length, Is.EqualTo(expectedTerms), kind + " " + way + ": " + text);
                    decimal sum = decimal.Parse(Regex.Match(text, @"Sum = (\d+\.\d+)").Groups[1].Value, CultureInfo.InvariantCulture);
                    Assert.That(values.Sum(), Is.EqualTo(sum), kind + " " + way + ": " + text);
                    Assert.That(Math.Abs(values.Last()), Is.LessThanOrEqualTo(.08m), "Rounding must not hide a missing term: " + text);
                    Assert.That(sum, Is.EqualTo(Math.Round((decimal)Score(result, state.playerId), 2, MidpointRounding.AwayFromZero)), text);
                    if (way == EpisodeCommandKind.Compete) Assert.That(text, Does.Contain("performance +1.11"), "Three points for full marks: .371 is 1.11.");
                }
            }
        }

        [TestCase(EpisodePhase.HoH)]
        [TestCase(EpisodePhase.Veto)]
        public void AThrowGivesUpTheBonusesAndCountsPartOfTheScore(EpisodePhase phase)
        {
            var state = Fixture(1708, phase);
            var thrown = Apply(state, EpisodeCommandKind.ThrowCompetition, 0);
            var played = Apply(state, EpisodeCommandKind.Compete, 0);
            var field = EpisodeEngine.CompetitionPlayers(state).ToList();
            string kind = EpisodeEngine.CompetitionCategory(state);
            var random = new SeededRandom(state.randomState);
            foreach (var actor in field)
            {
                double roll = random.NextDouble();
                double second = kind == "Luck" ? random.NextDouble() : 0;
                double raw = CompetitionRules.Score(actor.stats, kind, state.nominees.Contains(actor.id), 0, roll, second);
                double expected = actor.isPlayer ? raw * CompetitionRules.ThrowShare(field.Count) : raw;
                Assert.That(Score(thrown, actor.id), Is.EqualTo(expected), actor.name);
                if (!actor.isPlayer) Assert.That(Score(thrown, actor.id), Is.EqualTo(Score(played, actor.id)), "A throw touches nobody else's score.");
            }
            Assert.That(thrown.randomState, Is.EqualTo(random.State));
            Assert.That(Score(thrown, state.playerId), Is.LessThan(Score(played, state.playerId)),
                "Below what the same roll scores entering with no performance: the bonuses and a share of the score are gone.");
            var marker = thrown.events.Single(e => e.kind == EpisodeEngine.ThrowEventKind);
            Assert.That(marker.audienceIds, Is.EqualTo(new[] { state.playerId }), "The throw is the player's to know; the house is not told.");
            Assert.That(thrown.events.Last(e => e.kind == "competition-performance").text, Does.StartWith("You threw it"));
            Assert.That(played.events.Any(e => e.kind == EpisodeEngine.ThrowEventKind), Is.False, "Only a throw is marked as one.");
        }

        [Test]
        public void TheShareOfAThrownScoreKeepsAWinNearOneInTenAtEverySize()
        {
            double last = 0;
            for (int field = 2; field <= 16; field++)
            {
                double share = CompetitionRules.ThrowShare(field);
                Assert.That(share, Is.GreaterThanOrEqualTo(last), "A bigger field needs less cut, never more.");
                Assert.That(share, Is.InRange(.5, 1));
                last = share;
            }
            Assert.That(CompetitionRules.ThrowShare(3), Is.LessThan(CompetitionRules.ThrowShare(6)),
                "A final-four throw against two needs a deeper cut than one against five.");
        }

        /// <summary>
        /// "A throw should result in a loss 90% of the time": measured on the seasons players play -
        /// the regular roster, the player as each of its houseguests in turn, every kind of
        /// competition - at the final four's field of three, a mid-season five, the veto's six, and
        /// the opening weeks' eleven.
        /// </summary>
        [Test]
        public void AThrowLosesAboutNineTimesInTenInEveryField()
        {
            var regular = CastTemplates.In(CastTemplates.Roster.Regular).Select(template => template.Id).ToList();
            int allWins = 0, allTries = 0;
            foreach (int field in new[] { 3, 5, 6, 11 })
            {
                int wins = 0, tries = 0;
                for (uint seed = 1; seed <= 500; seed++)
                {
                    var state = SeasonBuilder.Create(new SeasonBuilder.Choice
                        { HouseSize = 12, PlayerTemplateId = regular[(int)(seed % regular.Count)] }, seed);
                    state.phase = EpisodePhase.HoH; state.week = 1 + (int)(seed % 10);
                    int active = Math.Max(4, field);
                    foreach (var actor in state.contestants.Where(c => !c.isPlayer).Skip(active - 1)) actor.status = ContestantStatus.Evicted;
                    // The final four's HoH: the outgoing Head of Household sits it out, leaving three.
                    if (active > field) state.previousHohId = state.Active.First(c => !c.isPlayer).id;
                    var result = Apply(state, EpisodeCommandKind.ThrowCompetition, 0);
                    Assert.That(result.competitionScores.Count, Is.EqualTo(field));
                    tries++;
                    if (result.hohId == state.playerId) wins++;
                }
                allWins += wins; allTries += tries;
                Assert.That((double)wins / tries, Is.InRange(.05, .16), "A field of " + field + ": thrown " + tries + " times, won " + wins + ".");
            }
            Assert.That((double)allWins / allTries, Is.InRange(.07, .13), "Thrown " + allTries + " times in all, won " + allWins + ".");
        }

        [Test]
        public void AThrowThatWinsSaysTheyGotLuckyAndHow()
        {
            EpisodeState won = null, lost = null;
            for (uint seed = 1; seed <= 400 && (won == null || lost == null); seed++)
            {
                var state = ContentCatalog.Create(seed);
                state.competitionRulesVersion = 4; state.phase = EpisodePhase.HoH;
                var result = Apply(state, EpisodeCommandKind.ThrowCompetition, 0);
                if (result.hohId == state.playerId) won = won ?? result; else lost = lost ?? result;
            }
            Assert.That(won, Is.Not.Null, "Some throw in four hundred wins.");
            string story = won.events.Last(e => e.kind == "competition-performance").text;
            var ordered = won.competitionScores.OrderByDescending(entry => entry.score).ToList();
            Assert.That(story, Does.StartWith("You threw it and won anyway: you got lucky. A throw gives up every bonus"));
            Assert.That(story, Does.Contain("It lowers only your own score, and yours came to "
                + Math.Round((decimal)ordered[0].score, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture)
                + " while the best of the rest scored "
                + Math.Round((decimal)ordered[1].score, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture) + "."),
                "How a throw can still win: it lowers only the thrower's own score. " + story);

            string loss = lost.events.Last(e => e.kind == "competition-performance").text;
            int place = lost.competitionScores.OrderByDescending(entry => entry.score).ToList().FindIndex(entry => entry.contestantId == lost.playerId) + 1;
            Assert.That(loss, Does.StartWith("You threw it. A throw gives up every bonus and counts 94% of your score in a field of 6."));
            Assert.That(loss, Does.Contain(", " + place + (place == 2 ? "nd" : place == 3 ? "rd" : "th") + " of 6."), loss);
        }

        [Test]
        public void OnlyARulesFourWeeklyCompetitionCanBeThrown()
        {
            var three = Fixture(1708, EpisodePhase.HoH); three.competitionRulesVersion = 3;
            Assert.That(Reject(three, EpisodeCommandKind.ThrowCompetition, 0), Does.Contain("throw at the floor"),
                "An earlier season throws as it always did, with a Compete at no performance.");
            var four = Fixture(1708, EpisodePhase.HoH);
            Assert.That(Reject(four, EpisodeCommandKind.ThrowCompetition, .5), Does.Contain("does not accept performance"));
            var finale = Fixture(1708, EpisodePhase.FinalHoHPart1);
            foreach (var actor in finale.contestants.Skip(3)) actor.status = ContestantStatus.Jury;
            Assert.That(Reject(finale, EpisodeCommandKind.ThrowCompetition, 0), Does.Contain("weekly HoH or Veto"));
        }

        [Test]
        public void EachKindsGameChangesEveryTimeItComesRound()
        {
            for (uint seed = 1; seed <= 24; seed++)
            {
                var seen = new Dictionary<string, List<string>>();
                for (int week = 1; week <= 10; week++)
                    foreach (var phase in new[] { EpisodePhase.HoH, EpisodePhase.Veto })
                    {
                        var definition = CompetitionDefinitions.Version4(seed, week, phase);
                        string kind = EpisodeEngine.CompetitionCategory(phase, week, 4, seed);
                        Assert.That(definition.Category, Is.EqualTo(kind), "The game is the kind's own.");
                        if (!seen.ContainsKey(kind)) seen[kind] = new List<string>();
                        seen[kind].Add(definition.Id);
                    }
                foreach (var entry in seen)
                {
                    if (entry.Key == "Luck") { Assert.That(entry.Value.Distinct(), Is.EqualTo(new[] { "roll-the-dice-v4" })); continue; }
                    for (int i = 1; i < entry.Value.Count; i++)
                        Assert.That(entry.Value[i], Is.Not.EqualTo(entry.Value[i - 1]), entry.Key + " plays a different game each time it comes round.");
                }
            }
            var state = Fixture(1708, EpisodePhase.Veto); state.week = 4;
            Assert.That(CompetitionDefinitions.For(state), Is.SameAs(CompetitionDefinitions.Version4(state.seed, 4, EpisodePhase.Veto)));
            state.competitionRulesVersion = 3;
            Assert.That(CompetitionDefinitions.For(state), Is.SameAs(CompetitionDefinitions.Version3(state.seed, 4, EpisodePhase.Veto)),
                "A rules-3 season keeps its frozen selector.");
        }

        [Test]
        public void NewSeasonsPlayRulesFourAndTheSaveAcceptsThem()
        {
            Assert.That(CompetitionRules.Current, Is.EqualTo(4));
            var state = ContentCatalog.Create(9);
            state.competitionRulesVersion = 4;
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            state.competitionRulesVersion = 5;
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False, "Rules from the future are refused.");
        }

        // ------------------------------------------------------------ fixtures

        private static uint SeedWhoseFirstHoHIs(string kind)
        {
            for (uint seed = 1; seed < 500; seed++)
                if (EpisodeEngine.CompetitionCategory(EpisodePhase.HoH, 1, 4, seed) == kind) return seed;
            throw new InvalidOperationException("No seed opens with " + kind);
        }

        private static EpisodeState Fixture(uint seed, EpisodePhase phase)
        {
            var state = ContentCatalog.Create(seed);
            state.competitionRulesVersion = 4; state.phase = phase;
            state.playerStudyBonus = 3; state.phaseEventCompBonus = 1;
            state.activeModifiers.Add(new StoryModifierState { id = "focus", name = "Distracted", weeksLeft = 2, competitionBonus = -1 });
            if (phase == EpisodePhase.Veto)
            {
                state.hohId = state.Active.First(actor => !actor.isPlayer).id;
                state.nominees = state.Active.Where(actor => actor.id != state.hohId).Take(2).Select(actor => actor.id).ToList();
                state.vetoPlayers = state.Active.Select(actor => actor.id).ToList();
            }
            return state;
        }

        private static double Score(EpisodeState state, string id) => state.competitionScores.Single(entry => entry.contestantId == id).score;

        private static EpisodeState Apply(EpisodeState state, EpisodeCommandKind kind, double performance)
        {
            var result = new EpisodeEngine(state).Apply(Command(state, kind, performance));
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }

        private static string Reject(EpisodeState state, EpisodeCommandKind kind, double performance)
        {
            var result = new EpisodeEngine(state).Apply(Command(state, kind, performance));
            Assert.That(result.accepted, Is.False, "Expected a refusal.");
            return result.reason;
        }

        private static EpisodeCommand Command(EpisodeState state, EpisodeCommandKind kind, double performance) => new EpisodeCommand
        {
            id = Guid.NewGuid().ToString("N"), actorId = state.playerId, expectedPhase = state.phase,
            expectedRevision = state.revision, kind = kind, performance = performance,
        };
    }
}
