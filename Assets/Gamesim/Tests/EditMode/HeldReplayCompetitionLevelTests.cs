#if !UNITY_5_3_OR_NEWER
// The held replays' competition rules: Tools/SimulationTests only, beside the replays it guards (they are compiled out of the
// editor's assemblies, as Unity's batch runner executes [Explicit] tests).
using System;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// A held replay plays the competition rules its digests were recorded at, written by name, so the trivia's switch-on
    /// (competition rules 6, written by <see cref="ShippedRules.ApplyFresh"/>) never moves one. The all-week rules' fresh
    /// seasons hold rules 5, the ring tower's; the role moments' seasons hold rules 4, as they have since the ring tower;
    /// the commitment rules' seasons and mode 1's, built without ApplyFresh or held by their director set, stay at rules 4.
    /// These cases are ordinary and quick: each replay's own first season, as the replay builds it. Each case is labelled in
    /// plain words, never by the replay's class name, so a filter naming a replay never runs one of these in its place.
    /// </summary>
    public sealed class HeldReplayCompetitionLevelTests
    {
        private const BindingFlags Statics = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

        [TestCase("the role moments", CompetitionRules.Widened)]
        [TestCase("the all-week rules", CompetitionRules.RingTower)]
        [TestCase("the commitment rules", CompetitionRules.Widened)]
        [TestCase("mode one", CompetitionRules.Widened)]
        public void AHeldReplayPlaysTheCompetitionRulesItWasRecordedAt(string replay, int competitionRules)
        {
            var season = Season(replay);
            Assert.That(season.competitionRulesVersion, Is.EqualTo(competitionRules), replay + " holds the competition rules its digests were recorded at.");
            Assert.That(season.competitionRulesVersion, Is.Not.EqualTo(CompetitionRules.Trivia), replay + " never plays the trivia's rules.");
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
