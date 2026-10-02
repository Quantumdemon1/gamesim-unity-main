using System.Collections;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Schema 22's commitment rules (ACTIONS-DEALS-ALLIANCES-PLAN R0, C0) on a season the director
    /// starts: it plays them from its first week, and they survive a save.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator CommitmentRules_ASeasonTheDirectorStartsPlaysThem()
        {
            director.StartSeason(null);
            yield return SettleCast();
            Assert.That(director.Snapshot.commitmentRulesStartWeek, Is.EqualTo(1), "The quick start.");
            director.StartSeason(new SeasonBuilder.Choice());
            yield return SettleCast();
            Assert.That(director.Snapshot.commitmentRulesStartWeek, Is.EqualTo(1), "The cast screen's season.");
            Assert.That(EpisodeEngine.CommitmentRulesOn(director.Snapshot), Is.True);
            director.SaveNow();
            director.LoadNow();
            yield return SettleCast();
            Assert.That(EpisodeEngine.CommitmentRulesOn(director.Snapshot), Is.True, "and they survive a save.");
        }
    }
}
