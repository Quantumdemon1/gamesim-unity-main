using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The house talk's story-rules levels (plan 32 §L and §Q1, "The house talks back"): 12 <see cref="StoryRules.Manners"/>
    /// (Stack A), 13 <see cref="StoryRules.Gossip"/> (Stack B) and 14 <see cref="StoryRules.ComesToYou"/> (Stack C), claimed by
    /// the owner on 2026-10-30 and named by HT0 before any code reads them. Each is refused until its stack's first slice
    /// moves <see cref="StoryRules.Latest"/> to it (S1 to 12, S3 to 13, S5a to 14, which repin <see cref="LatestIsManners"/>
    /// and add their cases here), and no new season plays one until its stack's enrolment moves <see cref="StoryRules.Current"/>.
    /// The exact pin of <see cref="StoryRules.Latest"/> lives here; RoleRulesTests holds it only as a bound. Pure simulation
    /// and JSON, so the Unity-free subset runs it as well as the editor.
    /// </summary>
    public sealed class HouseTalkRulesTests
    {
        [Test]
        public void TheThreeLevelsAreClaimedAsTwelveThirteenAndFourteen()
        {
            Assert.That(StoryRules.Manners, Is.EqualTo(12), "Stack A: manners, candour, tells and overhearing.");
            Assert.That(StoryRules.Gossip, Is.EqualTo(13), "Stack B: how they seem to see you, and the talkers.");
            Assert.That(StoryRules.ComesToYou, Is.EqualTo(14), "Stack C: the acquaintance threads, quiet asks and the house's events.");
            Assert.That(StoryRules.Manners, Is.EqualTo(StoryRules.Roles + 1), "The ladder has no gap: 12 follows the role moments' 11.");
        }

        /// <summary>
        /// HT0 named the levels without defining one; S1 moved the bound to Manners (the vote answer's manners and tells) and
        /// repinned this, then S3 to Gossip and S5a to ComesToYou. Defined is not played: A-ON enrolled 12 (the owner's ruling of
        /// 2026-11-13, house talk on by default), so every new season plays it.
        /// </summary>
        [Test]
        public void LatestIsManners()
        {
            Assert.That(StoryRules.Latest, Is.EqualTo(StoryRules.Manners), "S1 defines 12, Stack A's level.");
            Assert.That(StoryRules.IsDefined(StoryRules.Manners), Is.True);
            Assert.That(StoryRules.Current, Is.EqualTo(StoryRules.Manners), "A-ON enrolled Stack A: every new season plays 12.");
        }

        /// <summary>A live season at 12 is valid: every season the game starts holds it, and a test may write it by name too.</summary>
        [Test]
        public void ALiveSeasonAtMannersIsValid()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 1111u);
            ShippedRules.ApplyFresh(s);
            Assert.That(s.story.rulesVersion, Is.EqualTo(StoryRules.Manners), "A season the game starts today plays Stack A.");
            s.story.rulesVersion = StoryRules.Manners;
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
        }

        /// <summary>
        /// A-ON: every season the game starts (<see cref="ShippedRules.ApplyFresh"/>), in every cast and size the cast screen
        /// offers, plays story rules 12 from its first week, and validates. A season held at 11 by name - an older save, which
        /// load, migration and recovery never pass through EnableStory - stays at 11 and plays none of 12's rules.
        /// </summary>
        [TestCase(6, CastTemplates.Roster.Regular)] [TestCase(8, CastTemplates.Roster.Regular)] [TestCase(12, CastTemplates.Roster.Regular)]
        [TestCase(8, CastTemplates.Roster.AllStars)]
        public void EveryNewSeasonPlaysMannersAndASeasonHeldAtRolesDoesNot(int size, CastTemplates.Roster roster)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size, Roster = roster }, 1212u);
            ShippedRules.ApplyFresh(s);
            Assert.That(s.story.rulesVersion, Is.EqualTo(StoryRules.Manners), "ApplyFresh writes story rules 12.");
            Assert.That(s.story.rulesStartWeek, Is.EqualTo(1));
            Assert.That(EpisodeEngine.StoryAt(s, StoryRules.Manners), Is.True, "Stack A is on from week 1.");
            Assert.That(EpisodeEngine.RolesOn(s), Is.True, "Every gate is a lower bound: the role moments stay on at 12.");
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
            s.story.rulesVersion = StoryRules.Roles;
            Assert.That(EpisodeEngine.StoryAt(s, StoryRules.Manners), Is.False, "A season held at 11 plays none of 12's rules.");
            Assert.That(EpisodeValidation.TryValidate(s, out error), Is.True, error);
        }

        /// <summary>
        /// Below schema 30 12 is refused: every frozen reader keeps its own ladder, which ends at threads, so a historical save
        /// can never hold it (plan 32 §L, D-H11). The same lawful witness and refusal as RoleRulesTests' cases for 10 and 11.
        /// </summary>
        [TestCase("FrozenEpisodeV25")] [TestCase("FrozenEpisodeV26")] [TestCase("FrozenEpisodeV27")] [TestCase("FrozenEpisodeV28")]
        public void EveryFrozenReaderRefusesAHistoricalSeasonAtManners(string reader) =>
            RefusedAt.Invoke(null, new object[] { reader, StoryRules.Manners });

        private static readonly MethodInfo RefusedAt =
            typeof(RoleRulesTests).GetMethod("RefusedAt", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string), typeof(int) }, null)
            ?? throw new System.InvalidOperationException("RoleRulesTests.RefusedAt is the frozen readers' refusal check.");

        [TestCase(StoryRules.Gossip)] [TestCase(StoryRules.ComesToYou)]
        public void NoLaterHouseTalkLevelIsDefinedYet(int version)
        {
            Assert.That(version, Is.GreaterThan(StoryRules.Latest), "Claimed above the bound.");
            Assert.That(StoryRules.IsDefined(version), Is.False, "A season may not hold a version no code reads.");
        }

        /// <summary>A fresh season starts on Stack A's level, and a live season holding a later one is refused, unrepaired.</summary>
        [TestCase(StoryRules.Gossip)] [TestCase(StoryRules.ComesToYou)]
        public void TheValidatorRefusesALiveSeasonAtALaterHouseTalkLevel(int version)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 1111u);
            ShippedRules.ApplyFresh(s);
            Assert.That(s.story.rulesVersion, Is.EqualTo(StoryRules.Manners), "A season the game starts today plays Stack A's level only.");
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
            s.story.rulesVersion = version;
            string before = JObject.FromObject(s).ToString(Formatting.None);
            Assert.That(EpisodeValidation.TryValidate(s, out error), Is.False);
            Assert.That(error, Is.EqualTo("Unsupported story rules version."));
            Assert.That(JObject.FromObject(s).ToString(Formatting.None), Is.EqualTo(before), "Validation repairs nothing.");
        }
    }
}
