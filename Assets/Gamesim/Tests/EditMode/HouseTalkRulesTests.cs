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
    /// moves <see cref="StoryRules.Latest"/> to it (S1 to 12, S3 to 13, S5a to 14, which repin <see cref="LatestIsStillRoles"/>
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

        /// <summary>HT0 names the levels without defining one: S1 moves the bound to Manners and repins this, then S3 and S5a.</summary>
        [Test]
        public void LatestIsStillRoles()
        {
            Assert.That(StoryRules.Latest, Is.EqualTo(StoryRules.Roles), "No house-talk level is defined until its stack's first slice moves the bound.");
            Assert.That(StoryRules.Current, Is.LessThan(StoryRules.Manners), "No new season plays a house-talk level before its stack's enrolment.");
        }

        [TestCase(StoryRules.Manners)] [TestCase(StoryRules.Gossip)] [TestCase(StoryRules.ComesToYou)]
        public void NoHouseTalkLevelIsDefinedYet(int version)
        {
            Assert.That(version, Is.GreaterThan(StoryRules.Latest), "Claimed above the bound.");
            Assert.That(StoryRules.IsDefined(version), Is.False, "A season may not hold a version no code reads.");
        }

        /// <summary>A fresh season starts below the house talk, and a live season holding one of its levels is refused, unrepaired.</summary>
        [TestCase(StoryRules.Manners)] [TestCase(StoryRules.Gossip)] [TestCase(StoryRules.ComesToYou)]
        public void TheValidatorRefusesALiveSeasonAtAHouseTalkLevel(int version)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 1111u);
            ShippedRules.ApplyFresh(s);
            Assert.That(s.story.rulesVersion, Is.LessThan(StoryRules.Manners), "A season the game starts today plays no house-talk level.");
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
            s.story.rulesVersion = version;
            string before = JObject.FromObject(s).ToString(Formatting.None);
            Assert.That(EpisodeValidation.TryValidate(s, out error), Is.False);
            Assert.That(error, Is.EqualTo("Unsupported story rules version."));
            Assert.That(JObject.FromObject(s).ToString(Formatting.None), Is.EqualTo(before), "Validation repairs nothing.");
        }
    }
}
