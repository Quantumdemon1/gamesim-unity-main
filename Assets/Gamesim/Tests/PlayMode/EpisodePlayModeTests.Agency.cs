using System.Collections;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// A season the director starts plays under NPC agency from week one (NPC-AGENCY-PLAN.md §2):
    /// the house has its first impressions of each other before a word is said, and none of the
    /// player's own.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Agency_ASeasonTheDirectorStartsHasItsFirstImpressions()
        {
            director.StartSeason(new SeasonBuilder.Choice { HouseSize = 12 });
            yield return SettleCast();
            var state = director.Snapshot;
            Assert.That(EpisodeEngine.AgencyOn(state), Is.True, "Agency from week one.");
            Assert.That(state.agencyRulesStartWeek, Is.EqualTo(1));
            var npcs = state.contestants.Where(c => !c.isPlayer).ToList();
            foreach (var a in npcs)
                foreach (var b in npcs.Where(c => c.id != a.id))
                    Assert.That(state.Score(a.id, b.id), Is.EqualTo(EpisodeEngine.FirstImpressionPerPoint * TraitAffinity.Compatibility(a, b)).Within(1e-9), a.name + " on " + b.name);
            Assert.That(npcs.Any(a => npcs.Any(b => b.id != a.id && state.Score(a.id, b.id) != 0)), Is.True, "The shipped cast is not indifferent to itself.");
            Assert.That(npcs.All(c => state.Score(state.playerId, c.id) == 0), Is.True, "What you think of them is yours to decide.");
            director.SaveNow();
            director.LoadNow();
            yield return SettleCast();
            Assert.That(EpisodeEngine.AgencyOn(director.Snapshot), Is.True, "and it survives a save.");
        }
    }
}
