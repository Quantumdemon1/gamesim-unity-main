using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The prepared block speech (PLAN A, A6; the lead's decision 8): the speech a pad player gives
    /// without typing is the chosen approach's own authored line, <c>BlockSpeeches.NpcClosing</c>
    /// trimmed, sent as the ordinary speech command. For every approach it is never blank - so never
    /// Quiet - and the engine takes it with that approach on its receipt, rolling nothing. The
    /// director's <c>EpisodeDirector.PreparedSpeech</c> is exactly this; the PlayMode case presses it.
    /// </summary>
    public sealed class PreparedSpeechTests
    {
        private static string Prepared(string approach) => BlockSpeeches.NpcClosing(approach).Trim();

        private static EpisodeState House()
        {
            var s = EconomyRulesTests.Fresh(8);
            s.strategyRulesStartWeek = 1;
            s.agencyRulesStartWeek = 1;
            EpisodeEngine.EnableRead(s);
            EpisodeEngine.EnableCommitments(s);
            var npcs = s.contestants.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            s.phase = EpisodePhase.Eviction;
            s.evictionStage = EvictionStage.Speeches;
            s.hohId = npcs[1];
            s.vetoHolderId = s.hohId;
            s.vetoPlayers = s.Active.Take(EpisodeEngine.VetoPlayerCount(8)).Select(c => c.id).ToList();
            s.vetoResolved = true;
            s.nominees = new List<string> { s.playerId, npcs[0] };
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
            return s;
        }

        [TestCase(LobbyApproach.Emotional)]
        [TestCase(LobbyApproach.Strategic)]
        [TestCase(LobbyApproach.Deal)]
        [TestCase(LobbyApproach.Pressure)]
        public void ThePreparedSpeech_CarriesItsApproachAndRollsNothing(string approach)
        {
            Assert.That(BlockSpeeches.Approaches, Does.Contain(approach));
            string text = Prepared(approach);
            Assert.That(string.IsNullOrWhiteSpace(text), Is.False, approach + ": never blank, so never Quiet.");
            var before = House();
            Assert.That(BlockSpeeches.RulesOn(before), Is.True);
            var result = new EpisodeEngine(before).Apply(new EpisodeCommand
            {
                id = "prepared-" + approach, actorId = before.playerId, expectedRevision = before.revision, expectedPhase = before.phase,
                kind = EpisodeCommandKind.SubmitEvictionSpeech, text = text, secondTargetId = approach,
            });
            Assert.That(result.accepted, Is.True, result.reason);
            var after = result.state;
            var speech = after.evictionSpeeches.Single(item => item.speakerId == after.playerId);
            Assert.That(speech.text, Is.EqualTo(text));
            Assert.That(BlockSpeeches.Approach(after, speech), Is.EqualTo(approach), "The receipt carries the chosen approach.");
            Assert.That(after.randomState, Is.EqualTo(before.randomState), "Nothing is rolled.");
            Assert.That(JsonConvert.SerializeObject(after.relationships), Is.EqualTo(JsonConvert.SerializeObject(before.relationships)));
        }

        [Test]
        public void EachApproach_HasItsOwnPreparedLine()
        {
            var lines = BlockSpeeches.Approaches.Select(Prepared).ToList();
            Assert.That(lines.Distinct().Count(), Is.EqualTo(lines.Count), "The four lines differ, so the approach is heard in the words too.");
            foreach (var line in lines) Assert.That(line, Is.EqualTo(line.Trim()));
        }
    }
}
