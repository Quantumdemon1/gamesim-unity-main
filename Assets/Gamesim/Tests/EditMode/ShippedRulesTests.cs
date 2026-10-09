using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// <see cref="ShippedRules"/> (BALANCE plan B0): the director's fresh-season setup, moved out of its
    /// StartSeason into the simulation so the director and every harness start a season the same way.
    /// A pure refactor - the season it gives is the one the director's inline lines gave, field for
    /// field - and the pin of what a shipped season plays under.
    /// </summary>
    public sealed class ShippedRulesTests
    {
        /// <summary>Every builder a season the game starts comes from: the quick start, the cast screen's rosters and sizes, the stress house.</summary>
        private static IEnumerable<TestCaseData> Builders()
        {
            yield return new TestCaseData("quick start", 0, CastTemplates.Roster.Regular);
            yield return new TestCaseData("regular", 6, CastTemplates.Roster.Regular);
            yield return new TestCaseData("regular", 8, CastTemplates.Roster.Regular);
            yield return new TestCaseData("regular", 12, CastTemplates.Roster.Regular);
            yield return new TestCaseData("regular", 8, CastTemplates.Roster.AllStars);
            yield return new TestCaseData("stress", 16, CastTemplates.Roster.Regular);
        }

        private static EpisodeState Build(string builder, int size, CastTemplates.Roster roster, uint seed)
        {
            switch (builder)
            {
                case "quick start": return ContentCatalog.Create(seed);
                case "stress": return SeasonBuilder.CreateVerificationStressHouse(size, seed);
                default: return SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size, Roster = roster }, seed);
            }
        }

        /// <summary>What a season the game starts today plays under, every boundary by name.</summary>
        private static readonly Dictionary<string, int> Shipped = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "competitionRulesVersion", 4 },
            { "readRulesStartWeek", 1 },
            { "leverRulesStartWeek", 1 },
            { "weekRulesStartWeek", 1 },
            { "economyRulesVersion", 1 },
            { "agencyRulesStartWeek", 1 },
            { "finaleRulesStartWeek", 1 },
            { "commitmentRulesStartWeek", 1 },
            { "unifiedCommitmentRulesVersion", 1 },
            { "unifiedHearingRulesVersion", 1 },
            { "blocRulesStartWeek", 1 },
            { "socialBudgetRulesStartWeek", 1 },
            { "dealRulesStartWeek", 1 },
            { "eventRulesStartWeek", 1 },
            { "storyRulesStartWeek", 1 },
            { "haveNotRulesStartWeek", 1 },
            { "strategyRulesStartWeek", 1 },
            { "allianceLeakRulesStartWeek", 1 },
            { "pactPlanRulesStartWeek", 1 },
            { "allWeekRulesStartWeek", 1 },
            { "npcSocial.rulesStartWeek", 1 },
            { "story.rulesStartWeek", 1 },
            { "story.rulesVersion", 9 },
        };

        [TestCaseSource(nameof(Builders))]
        public void ApplyFreshSwitchesOnEveryShippedRuleFromWeekOne(string builder, int size, CastTemplates.Roster roster)
        {
            var s = Build(builder, size, roster, 4101u);
            ShippedRules.ApplyFresh(s);
            var fields = ShippedRules.Fields(s);
            Assert.That(fields.Select(f => f.Key), Is.EquivalentTo(Shipped.Keys), "Every boundary is pinned.");
            foreach (var field in fields)
                Assert.That(field.Value, Is.EqualTo(Shipped[field.Key]), builder + " " + size + ": " + field.Key);
            Assert.That(s.competitionRulesVersion, Is.EqualTo(CompetitionRules.Current));
            Assert.That(s.story.rulesVersion, Is.EqualTo(StoryRules.Current));
            Assert.That(s.unifiedCommitmentRulesVersion, Is.EqualTo(UnifiedCommitments.ProspectiveVersion));
            Assert.That(s.unifiedHearingRulesVersion, Is.EqualTo(UnifiedCommitmentHearings.ProspectiveVersion));
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
        }

        /// <summary>
        /// The refactor's proof: <see cref="ShippedRules.ApplyFresh"/> gives the season the director's inline
        /// setup gave at ca7f4da6 (EpisodeDirector.Season.cs StartSeason, copied below line for line), every
        /// public field of it, first impressions and lore included. A rule the shipped game gains later goes
        /// into ApplyFresh and is added to this copy in the same commit, so the two stay one list: D3's war
        /// rooms are the first, added as the two lanes landed together, and D2's all-week beats the next.
        /// </summary>
        [TestCaseSource(nameof(Builders))]
        public void ApplyFreshGivesTheSeasonTheDirectorsInlineSetupGave(string builder, int size, CastTemplates.Roster roster)
        {
            foreach (uint seed in new[] { 1u, 77u, 4242u })
            {
                var shipped = Build(builder, size, roster, seed);
                ShippedRules.ApplyFresh(shipped);
                var inline = Build(builder, size, roster, seed);
                DirectorInlineSetupWithItsLaterRules(inline);
                Assert.That(Json(shipped), Is.EqualTo(Json(inline)), builder + " " + size + " seed " + seed);
            }
        }

        /// <summary>The director's inline setup at ca7f4da6, plus each rule the shipped game has gained since.</summary>
        private static void DirectorInlineSetupWithItsLaterRules(EpisodeState fresh)
        {
            fresh.competitionRulesVersion = CompetitionRules.Current;
            fresh.haveNotRulesStartWeek = 1;
            fresh.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableStory(fresh);
            EpisodeEngine.EnableRead(fresh);
            EpisodeEngine.EnableLevers(fresh);
            EpisodeEngine.EnableWeek(fresh);
            EpisodeEngine.EnableEconomy(fresh);
            EpisodeEngine.EnableAgency(fresh);
            EpisodeEngine.EnableFinale(fresh);
            EpisodeEngine.EnableCommitments(fresh);
            EpisodeEngine.EnableAllianceLeaks(fresh);
            EpisodeEngine.EnablePactPlans(fresh);
            EpisodeEngine.EnableAllWeek(fresh);
            fresh.unifiedCommitmentRulesVersion = UnifiedCommitments.ProspectiveVersion;
            fresh.unifiedHearingRulesVersion = UnifiedCommitmentHearings.ProspectiveVersion;
        }

        [Test]
        public void ApplyFreshDrawsNothingFromTheSeasonsStreamAndCommitsNothing()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 515u);
            uint random = s.randomState;
            int sequence = s.nextSequence, events = s.events.Count;
            ShippedRules.ApplyFresh(s);
            Assert.That(s.randomState, Is.EqualTo(random), "No roll.");
            Assert.That(s.nextSequence, Is.EqualTo(sequence), "No id minted.");
            Assert.That(s.events.Count, Is.EqualTo(events), "Nothing logged.");
            Assert.That(s.revision, Is.Zero);
            Assert.That(s.acceptedCommandIds, Is.Empty);
        }

        /// <summary>Fresh seasons only: the economy refuses a season that has been played, so ApplyFresh does too.</summary>
        [Test]
        public void ApplyFreshRefusesASeasonThatHasBeenPlayed()
        {
            var engine = new EpisodeEngine(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 516u));
            Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            Assert.Throws<ArgumentException>(() => ShippedRules.ApplyFresh(engine.Snapshot));
            Assert.Throws<ArgumentNullException>(() => ShippedRules.ApplyFresh(null));
        }

        /// <summary>
        /// <see cref="ShippedRules.Fields"/> names every rule boundary the state has - each int field of the
        /// season, its NPC world and its story whose name ends in RulesStartWeek or RulesVersion, or is the
        /// nested rulesStartWeek / rulesVersion - and reads each from its own field.
        /// </summary>
        [Test]
        public void FieldsNamesEveryRuleBoundaryTheStateHasAndReadsEachFromItsOwnField()
        {
            var s = new EpisodeState();
            var expected = new List<string>();
            int next = 100;
            void Mark(object owner, string prefix)
            {
                foreach (var field in owner.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Where(f => f.FieldType == typeof(int) && (f.Name.EndsWith("RulesStartWeek", StringComparison.Ordinal)
                        || f.Name.EndsWith("RulesVersion", StringComparison.Ordinal) || f.Name == "rulesStartWeek" || f.Name == "rulesVersion")))
                {
                    field.SetValue(owner, next++);
                    expected.Add(prefix + field.Name + "=" + field.GetValue(owner));
                }
            }
            Mark(s, "");
            Mark(s.npcSocial, "npcSocial.");
            Mark(s.story, "story.");
            Assert.That(expected.Count, Is.GreaterThanOrEqualTo(23), "The state's boundaries were found.");
            Assert.That(ShippedRules.Fields(s).Select(f => f.Key + "=" + f.Value), Is.EquivalentTo(expected));
        }

        private static string Json(EpisodeState state)
            => JsonConvert.SerializeObject(state, new JsonSerializerSettings { ContractResolver = new PublicFields() });

        private sealed class PublicFields : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
                => type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Select(field => base.CreateProperty(field, MemberSerialization.Fields)).ToList();
        }
    }
}
