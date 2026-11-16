using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Competition rules 5 (competition ring tower, slice 4): a new season deals the Ring Tower as every other
    /// mental competition, starting with its first; a season under rules 4 or 3 deals exactly what it always
    /// dealt; and the rules change nothing but the game - a season played the same way under rules 4 and 5
    /// commits the same results. Unity-free, so the subset runs it on every push.
    /// </summary>
    public sealed class CompetitionRules5Tests
    {
        private static readonly EpisodePhase[] Phases =
        {
            EpisodePhase.HoH, EpisodePhase.Veto, EpisodePhase.FinalHoHPart1, EpisodePhase.FinalHoHPart2, EpisodePhase.FinalHoHPart3,
        };

        /// <summary>Every selection a selector makes over seeds 1 to 64, weeks 1 to 12 and every competition phase, as one line each.</summary>
        private static IEnumerable<string> Selections(Func<uint, int, EpisodePhase, CompetitionDefinition> selector)
        {
            for (uint seed = 1; seed <= 64; seed++)
                for (int week = 1; week <= 12; week++)
                    foreach (var phase in Phases)
                        yield return seed + "/" + week + "/" + phase + "=" + selector(seed, week, phase).Id;
        }

        private static string Digest(IEnumerable<string> lines)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines)))).Replace("-", "").Substring(0, 16).ToLowerInvariant();
        }

        // Recorded at competition slice 4. Rules 3's and 4's selectors are untouched by rules 5: these are the selections
        // they made before it, and a digest that moves is a regression, never a re-record.
        private const string Version3Digest = "893f38c3d8af13da", Version4Digest = "b16b6b29cf4e18a1";
        // Rules 5's own selections, new with it.
        private const string Version5Digest = "42734bfe8c35d936";

        [Test]
        public void TheRulesThreeAndFourSelectionsAreThePublishedOnes()
        {
            string three = Digest(Selections(CompetitionDefinitions.Version3)), four = Digest(Selections(CompetitionDefinitions.Version4));
            Assert.That(three, Is.EqualTo(Version3Digest), "RECORD Version3Digest " + three);
            Assert.That(four, Is.EqualTo(Version4Digest), "RECORD Version4Digest " + four);
        }

        [Test]
        public void TheRulesFiveSelectionsArePinned()
        {
            string five = Digest(Selections(CompetitionDefinitions.Version5));
            Assert.That(five, Is.EqualTo(Version5Digest), "RECORD Version5Digest " + five);
        }

        [Test]
        public void ASeasonKeepsTheSelectorOfTheRulesItStartedUnder()
        {
            for (uint seed = 1; seed <= 40; seed++)
                for (int week = 1; week <= 12; week++)
                    foreach (var phase in Phases)
                    {
                        var state = new EpisodeState { seed = seed, week = week, phase = phase };
                        state.competitionRulesVersion = 3;
                        Assert.That(CompetitionDefinitions.For(state), Is.SameAs(CompetitionDefinitions.Version3(seed, week, phase)));
                        state.competitionRulesVersion = 4;
                        Assert.That(CompetitionDefinitions.For(state), Is.SameAs(CompetitionDefinitions.Version4(seed, week, phase)));
                        state.competitionRulesVersion = 5;
                        Assert.That(CompetitionDefinitions.For(state), Is.SameAs(CompetitionDefinitions.Version5(seed, week, phase)));
                        if (CompetitionDefinitions.Version4(seed, week, phase) != CompetitionDefinitions.RingTower)
                            Assert.That(CompetitionDefinitions.Version3(seed, week, phase), Is.Not.SameAs(CompetitionDefinitions.RingTower));
                    }
            Assert.That(CompetitionDefinitions.All.Contains(CompetitionDefinitions.RingTower), Is.False, "The console games' list stays rules 4's.");
            var ids = CompetitionDefinitions.Catalogue.Select(definition => definition.Id).ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count), "Every published id is its own.");
            Assert.That(ids.Last(), Is.EqualTo("ring-tower-v5"));
            Assert.That(CompetitionDefinitions.RingTower.Category, Is.EqualTo(CompetitionRules.Mental));
            Assert.That(CompetitionDefinitions.RingTower.Pattern, Is.EqualTo(CompetitionPattern.RingTower));
            Assert.That(CompetitionDefinitions.RingTower.Duration, Is.EqualTo(90));
        }

        [Test]
        public void RulesFiveDealsRulesFoursGamesButEveryOtherMentalOneIsTheTower()
        {
            for (uint seed = 1; seed <= 300; seed++)
            {
                var mental = new List<CompetitionDefinition>();
                for (int week = 1; week <= 12; week++)
                    foreach (var phase in new[] { EpisodePhase.HoH, EpisodePhase.Veto })
                    {
                        var four = CompetitionDefinitions.Version4(seed, week, phase);
                        var five = CompetitionDefinitions.Version5(seed, week, phase);
                        Assert.That(five.Category, Is.EqualTo(four.Category), "The kinds and their order are rules 4's.");
                        if (four.Category != CompetitionRules.Mental) { Assert.That(five, Is.SameAs(four)); continue; }
                        mental.Add(five);
                    }
                Assert.That(mental, Is.Not.Empty);
                for (int i = 0; i < mental.Count; i++)
                    if (i % 2 == 0) Assert.That(mental[i], Is.SameAs(CompetitionDefinitions.RingTower), "seed " + seed + ": mental competition " + (i + 1));
                    else Assert.That(mental[i], Is.SameAs(CompetitionDefinitions.HouseMemory).Or.SameAs(CompetitionDefinitions.FirstImpressions),
                        "seed " + seed + ": mental competition " + (i + 1) + " is a memory board");
                foreach (var phase in new[] { EpisodePhase.FinalHoHPart1, EpisodePhase.FinalHoHPart2, EpisodePhase.FinalHoHPart3 })
                    Assert.That(CompetitionDefinitions.Version5(seed, 9, phase), Is.SameAs(CompetitionDefinitions.Version4(seed, 9, phase)), "The final parts keep their rounds.");
            }
        }

        /// <summary>Where a new season's first ring tower is: always its first mental competition, in the first three weeks.</summary>
        [Test]
        public void ANewSeasonsFirstMentalCompetitionIsTheTowerInItsFirstThreeWeeks()
        {
            var weeks = new Dictionary<string, int>();
            for (uint seed = 1; seed <= 500; seed++)
            {
                string first = null;
                for (int week = 1; week <= 3 && first == null; week++)
                    foreach (var phase in new[] { EpisodePhase.HoH, EpisodePhase.Veto })
                        if (CompetitionDefinitions.Version5(seed, week, phase) == CompetitionDefinitions.RingTower) { first = "week " + week + " " + phase; break; }
                Assert.That(first, Is.Not.Null, "seed " + seed + " deals the tower in its first three weeks");
                weeks[first] = weeks.TryGetValue(first, out int n) ? n + 1 : 1;
            }
            TestContext.Out.WriteLine("The first ring tower over 500 seasons: " + string.Join(", ", weeks.OrderBy(p => p.Key).Select(p => p.Key + " " + p.Value)));
            Assert.That(weeks.Keys, Is.SubsetOf(new[] { "week 1 HoH", "week 1 Veto", "week 2 HoH", "week 2 Veto", "week 3 Veto" }));
        }

        /// <summary>
        /// The rules change only the game: the same season, played by the same commands, held at rules 4 and started on
        /// rules 5, commits the same scores, draws the same rolls and ends each step on the same state - but for the
        /// rules' own number and the line that names the game dealt.
        /// </summary>
        [Test]
        public void ANewSeasonStartsOnRulesFiveAndCommitsWhatRulesFourWould()
        {
            uint seed = 1;
            while (CompetitionRules.Category(EpisodePhase.HoH, 1, seed) != CompetitionRules.Mental) seed++;
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
            Assert.That(fresh.competitionRulesVersion, Is.EqualTo(CompetitionRules.Widened), "A built season starts on rules 4.");
            ShippedRules.ApplyFresh(fresh);
            Assert.That(fresh.competitionRulesVersion, Is.EqualTo(CompetitionRules.RingTower), "A season the game starts plays rules 5.");
            var held = fresh.Clone();
            held.competitionRulesVersion = CompetitionRules.Widened;
            Assert.That(EpisodeValidation.TryValidate(fresh, out string reason), Is.True, reason);
            Assert.That(EpisodeValidation.TryValidate(held, out reason), Is.True, reason);
            var five = new EpisodeEngine(fresh);
            var four = new EpisodeEngine(held);
            bool towerDealt = false;
            for (int step = 0; step < 1500 && five.Snapshot.week <= 3 && five.Snapshot.phase != EpisodePhase.Finished; step++)
            {
                var command = EpisodeEngineTests.NextCommand(five.Snapshot);
                var a = five.Apply(command);
                var b = four.Apply(command);
                Assert.That(a.accepted, Is.True, a.reason);
                Assert.That(b.accepted, Is.True, b.reason);
                Assert.That(Normal(five.Snapshot), Is.EqualTo(Normal(four.Snapshot)), "step " + step + ": " + command.kind);
                towerDealt |= five.Snapshot.events.Any(e => e.kind == "competition-definition" && e.text.StartsWith("ring-tower-v5", StringComparison.Ordinal));
            }
            Assert.That(towerDealt, Is.True, "The season's first HoH is mental, so rules 5 dealt the tower and logged it.");
            Assert.That(four.Snapshot.events.Any(e => e.kind == "competition-definition" && e.text.StartsWith("ring-tower-v5", StringComparison.Ordinal)), Is.False,
                "Rules 4 never deals it.");
        }

        /// <summary>The state as JSON with the rules' number and the dealt game's line taken out.</summary>
        private static string Normal(EpisodeState state)
        {
            var o = JObject.FromObject(state);
            o["competitionRulesVersion"] = 0;
            foreach (var entry in (JArray)o["events"])
                if ((string)entry["kind"] == "competition-definition") entry["text"] = "";
            return o.ToString(Formatting.None);
        }
    }
}
