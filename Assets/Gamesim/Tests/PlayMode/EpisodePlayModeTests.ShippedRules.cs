using System.Collections;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// BALANCE plan B0: the director starts every season through <see cref="ShippedRules.ApplyFresh"/>, so the
    /// season it installs carries exactly the rule boundaries ApplyFresh gives the same build of the same seed -
    /// on the quick start and on the cast screen's season alike.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator ShippedRules_TheDirectorsSeasonCarriesExactlyWhatApplyFreshGives()
        {
            director.StartSeason(null);
            yield return SettleCast();
            var started = director.Snapshot;
            var expected = ContentCatalog.Create(started.seed);
            ShippedRules.ApplyFresh(expected);
            Assert.That(ShippedRules.Fields(started), Is.EqualTo(ShippedRules.Fields(expected)), "The quick start.");

            director.StartSeason(new SeasonBuilder.Choice());
            yield return SettleCast();
            started = director.Snapshot;
            expected = SeasonBuilder.Create(new SeasonBuilder.Choice(), started.seed);
            ShippedRules.ApplyFresh(expected);
            Assert.That(ShippedRules.Fields(started), Is.EqualTo(ShippedRules.Fields(expected)), "The cast screen's season.");
            Assert.That(started.allianceLeakRulesStartWeek, Is.EqualTo(1), "A rule ApplyFresh switches on is on.");
        }
    }
}
