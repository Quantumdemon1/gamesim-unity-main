using System.Collections;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// BALANCE plan B0: the director starts every season through <see cref="ShippedRules.ApplyFresh"/>, so the
    /// season it installs carries exactly the rule boundaries ApplyFresh gives the same build of the same seed -
    /// on the quick start, on the cast screen's season, and (dialogue D1) on a cast-screen season with the
    /// conversation preview turned on, which ApplyFresh reads from the choice and nothing else sets.
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
            Assert.That(started.dialogueRulesVersion, Is.Zero, "The quick start plays no preview.");

            director.StartSeason(new SeasonBuilder.Choice());
            yield return SettleCast();
            started = director.Snapshot;
            expected = SeasonBuilder.Create(new SeasonBuilder.Choice(), started.seed);
            ShippedRules.ApplyFresh(expected);
            Assert.That(ShippedRules.Fields(started), Is.EqualTo(ShippedRules.Fields(expected)), "The cast screen's season.");
            Assert.That(started.allianceLeakRulesStartWeek, Is.EqualTo(1), "A rule ApplyFresh switches on is on.");
            Assert.That(started.dialogueRulesVersion, Is.Zero, "The default is unchanged: no preview unless it is chosen.");

            var preview = new SeasonBuilder.Choice { CampaignConversations = true };
            director.StartSeason(preview);
            yield return SettleCast();
            started = director.Snapshot;
            expected = SeasonBuilder.Create(new SeasonBuilder.Choice(), started.seed);
            ShippedRules.ApplyFresh(expected, preview);
            Assert.That(ShippedRules.Fields(started), Is.EqualTo(ShippedRules.Fields(expected)), "The cast screen's season with the preview on.");
            Assert.That(started.dialogueRulesVersion, Is.EqualTo(1), "The preview starts the season on the pinned dialogue rules 1.");
            Assert.That(DialogueRules.On(started), Is.True);
            Assert.That(preview.CampaignConversations, Is.True, "The director reads the choice and writes nothing to it.");
        }
    }
}
