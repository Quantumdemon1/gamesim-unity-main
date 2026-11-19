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
    /// Free talk slice 1's season soak: fresh seasons the game starts (talk rules 1) in which the player takes every free talk on
    /// offer in every window, to the end of the season, on twelve seeds of each cast. Every breach the soak checks fails the
    /// case (<see cref="FreeTalkIntegrationTests.Soak"/>): the cap, the season's stream byte-equal across each talk, no talk
    /// said twice except "the usual" and the weekly standing talk, the bank never dry, the All-Stars' star pool of layers 1, 2
    /// and 4, every state valid and the same after a save and reload. Each season prints one SOAK line of counts.
    /// </summary>
    public sealed class FreeTalkSeasonSoak
    {
        private const string Why = "Free talk slice 1's whole-season soak: twelve seasons a cast, each to its end. Run by name.";

        [Test, Explicit(Why)] public void RegularCastTwelveSeasons() => Seasons(CastTemplates.Roster.Regular, 3501u);

        [Test, Explicit(Why)] public void AllStarsTwelveSeasons() => Seasons(CastTemplates.Roster.AllStars, 3601u);

        private static void Seasons(CastTemplates.Roster roster, uint first)
        {
            var tallies = new List<FreeTalkIntegrationTests.Tally>();
            for (uint seed = first; seed < first + 12; seed++)
            {
                var tally = FreeTalkIntegrationTests.Soak(seed, roster, 0);
                TestContext.WriteLine(tally.Line(roster + "/" + seed));
                Assert.That(tally.Finished, Is.True, roster + "/" + seed + ": the season reached its end.");
                tallies.Add(tally);
            }
            TestContext.WriteLine("SOAK TOTAL " + roster + ": seasons " + tallies.Count + ", talks " + tallies.Sum(t => t.Talks)
                + ", windows " + tallies.Sum(t => t.Windows) + ", usual repeats " + tallies.Sum(t => t.Repeats) + ", standing repeats "
                + tallies.Sum(t => t.StandingRepeats) + ", facts " + tallies.Sum(t => t.Facts) + ", standing lines "
                + tallies.Sum(t => t.StandingLines) + ", star-pool talks " + tallies.Sum(t => t.StarTalks) + ", most rapport "
                + tallies.Max(t => t.MostRapport));
            Assert.That(tallies.Sum(t => t.Talks), Is.GreaterThan(0));
        }
    }
}
#endif
