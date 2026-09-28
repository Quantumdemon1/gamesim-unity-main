using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The verdict on screen (STRATEGY-LOOP-PLAN.md §5): a GAME SENSE block on the season report
    /// after your season, with the number, its three faces, the chances taken, the moments that made
    /// the difference and the chances missed, each a ledger row in words.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator GameSense_TheSeasonReportGivesTheVerdictAndItsReasons()
        {
            var state = Finished();
            state.week = 5;
            var npc = state.contestants.First(c => !c.isPlayer);
            state.ledger.competitions.Add(new CompetitionRow { week = 2, kind = "Veto", field = 6, placement = 1, entry = CompetitionEntry.Played, performance = 0.9, expectedWin = 0.2 });
            state.ledger.power.Add(new PowerRow { week = 2, hohId = npc.id, evicteeId = npc.id, nominees = new List<string> { npc.id, state.contestants[2].id }, savedId = state.playerId, vetoUsed = true, vetoHolderId = state.playerId });
            state.ledger.opportunities.Add(new OpportunityRow { id = "play-1", kind = OpportunityKinds.Play, week = 3, source = "the-secret-alliance", response = OpportunityResponse.Expired, outcome = OpportunityOutcome.NotApplicable });
            var report = GameSense.Evaluate(state);
            Assume.That(report.moments, Is.Not.Empty); Assume.That(report.missed, Is.Not.Empty);

            Report().Show(state, _ => null, null);
            yield return null;

            var labels = ReportLabels();
            Assert.That(labels, Does.Contain("GAME SENSE"), "The number has its cell.");
            Assert.That(labels, Does.Contain(report.score.ToString()), "and it is the referee's number.");
            foreach (var face in new[] { "COMPETITIONS", "STRATEGY", "SOCIAL", "CHANCES TAKEN" })
                Assert.That(labels, Does.Contain(face), face);
            Assert.That(labels, Does.Contain(SeasonReport.MomentsHeading));
            Assert.That(labels, Does.Contain(SeasonReport.MissedHeading));
            Assert.That(labels.Any(text => text.Contains("won the veto") && text.Contains("from the block")), Is.True, "The moment, as a row in words.");
            Assert.That(labels.Any(text => text.Contains("a play let pass")), Is.True, "The chance missed, as a row in words.");
            Assert.That(SeasonReport.GameSenseLine(state), Does.StartWith("Game Sense " + report.score + ":"), "The finished panel's line carries the same number.");
        }
    }
}
