#if !UNITY_5_3_OR_NEWER
// The held replays' rules levels: Tools/SimulationTests only, beside the replays it guards (they are compiled out of the
// editor's assemblies, as Unity's batch runner executes [Explicit] tests).
using System;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// A held replay plays the free talk and minds levels its digests were recorded at, written by name, so enrolling a new
    /// shipped level never moves one. The replays seeded through <see cref="ShippedRules.ApplyFresh"/> (the role moments'
    /// seasons and the all-week rules' fresh seasons) hold free talk rules 2 and the minds' rules 0; the commitment rules'
    /// seasons and mode 1's, built without ApplyFresh, stay at free talk rules 0. These cases are ordinary and quick: each
    /// replay's own first season, as the replay builds it. Each case is labelled in plain words, never by the replay's class
    /// name: a filter "FullyQualifiedName~&lt;replay&gt;" that matched an ordinary case would run it and skip the replay's own
    /// [Explicit] cases, and read as a pass.
    /// </summary>
    public sealed class HeldReplayTalkLevelTests
    {
        private const BindingFlags Statics = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

        [TestCase("the role moments", TalkRules.Chemistry)]
        [TestCase("the all-week rules", TalkRules.Chemistry)]
        [TestCase("the commitment rules", TalkRules.Off)]
        [TestCase("mode one", TalkRules.Off)]
        [TestCase("the house talk's manners", TalkRules.Romance)]
        public void AHeldReplayPlaysTheTalkAndMindLevelsItWasRecordedAt(string replay, int talkRules)
        {
            var season = Season(replay);
            Assert.That(season.story, Is.Not.Null, replay + " plays the story.");
            Assert.That(season.story.talkRules, Is.EqualTo(talkRules), replay + " holds the free talk level its digests were recorded at.");
            Assert.That(season.story.mindRules, Is.EqualTo(MindRules.Off), replay + " holds the minds' level its digests were recorded at.");
            Assert.That(EpisodeValidation.TryValidate(season, out string invalid), Is.True, invalid);
        }

        /// <summary>
        /// A-ON (story rules 12 enrolled): a held replay plays the story rules it was recorded or measured at, written by name,
        /// so each enrolment leaves it where it was. The role moments' seasons and the all-week rules' fresh seasons hold 11;
        /// the house talk's manners hold 12 for Stack B; the commitment rules' and mode 1's hold threads (9).
        /// </summary>
        [TestCase("the role moments", StoryRules.Roles)]
        [TestCase("the all-week rules", StoryRules.Roles)]
        [TestCase("the commitment rules", StoryRules.Threads)]
        [TestCase("mode one", StoryRules.Threads)]
        [TestCase("the house talk's manners", StoryRules.Manners)]
        public void AHeldReplayPlaysTheStoryRulesItWasRecordedAt(string replay, int storyRules)
        {
            var season = Season(replay);
            Assert.That(season.story.rulesVersion, Is.EqualTo(storyRules), replay + " holds the story rules its digests were recorded at.");
            Assert.That(EpisodeValidation.TryValidate(season, out string invalid), Is.True, invalid);
        }

        /// <summary>The replay's own first season: house of six, seed 1, its director set where it has one.</summary>
        private static EpisodeState Season(string replay)
        {
            switch (replay)
            {
                case "the role moments": return Invoke(typeof(RolesSeasonDigests), "Season", 6, 1u);
                case "the all-week rules": return Invoke(typeof(AllWeekSeasonDigests), "Held", 6, 1u);
                case "the commitment rules": return Invoke(typeof(CommitmentRulesSeasonDigests), "Season", "director", 6, 1u, true);
                case "mode one": return Invoke(typeof(ModeOneSeasonDigests), "Season", "director", 6, 1u);
                case "the house talk's manners": return Invoke(typeof(MannersSeasonDigests), "Season", 6, 1u);
            }
            throw new ArgumentException("No held replay " + replay + ".");
        }

        private static EpisodeState Invoke(Type replay, string name, params object[] arguments)
        {
            var method = replay.GetMethod(name, Statics, null, arguments.Select(a => a.GetType()).ToArray(), null);
            Assert.That(method, Is.Not.Null, replay.Name + "." + name + " builds its own seasons.");
            return (EpisodeState)method.Invoke(null, arguments);
        }
    }
}
#endif
