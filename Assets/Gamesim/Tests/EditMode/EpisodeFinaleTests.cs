using System;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class EpisodeFinaleTests
    {
        [Test]
        public void FinalEvictionPersistsQuestionAndExactlyTwoSourceDraws()
        {
            var state = FinalThree(); var rng = new SeededRandom(state.randomState);
            rng.NextDouble(); rng.NextDouble();
            var result = EnterQuestioning(state);
            Assert.That(result.phase, Is.EqualTo(EpisodePhase.JuryQuestioning));
            Assert.That(result.randomState, Is.EqualTo(rng.State));
            Assert.That(result.juryExchanges, Has.Count.EqualTo(1));
            var reloaded = new EpisodeEngine(JsonConvert.DeserializeObject<EpisodeState>(JsonConvert.SerializeObject(result),
                new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace }));
            Assert.That(JsonConvert.SerializeObject(reloaded.Snapshot), Is.EqualTo(JsonConvert.SerializeObject(result)));
        }

        [TestCase(true)] [TestCase(false)]
        public void FinalistAnswerUsesSeparateEventAndScoreDrawsExactlyOnce(bool correct)
        {
            var state = EnterQuestioning(FinalThree()); var q = state.juryExchanges[0];
            double delta = correct ? 10 : -10;
            var rng = new SeededRandom(state.randomState);
            double eventDelta = WebRules.ReciprocalDelta(delta, rng.NextDouble());
            double scoreDelta = WebRules.ReciprocalDelta(delta, rng.NextDouble());
            var command = EpisodeEngineTests.Command(state, EpisodeCommandKind.AnswerJury);
            command.targetId = q.questionerId;
            command.secondTargetId = correct ? q.correctChoice : q.correctChoice == "A" ? "B" : "A";
            var engine = new EpisodeEngine(state); var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.randomState, Is.EqualTo(rng.State));
            Assert.That(result.state.Score(q.questionerId, state.playerId), Is.EqualTo(WebRules.ClampScore(state.Score(q.questionerId, state.playerId) + delta)));
            Assert.That(result.state.Score(state.playerId, q.questionerId), Is.EqualTo(WebRules.ClampScore(state.Score(state.playerId, q.questionerId) + scoreDelta)));
            var reverse = result.state.relationships.Single(r => r.fromId == state.playerId && r.toId == q.questionerId);
            Assert.That(reverse.events.Last().impactScore, Is.EqualTo(eventDelta));
            Assert.That(reverse.notes.Last(), Does.Contain(correct ? "impressed" : "unconvinced"));
            Assert.That(result.state.relationshipArcs.Single().weeklyHistory.Single().delta, Is.EqualTo(delta));
            string after = JsonConvert.SerializeObject(engine.Snapshot);
            Assert.That(engine.Apply(command).duplicate, Is.True);
            command = EpisodeEngineTests.Command(engine.Snapshot, EpisodeCommandKind.AnswerJury);
            command.targetId = q.questionerId; command.secondTargetId = "A";
            Assert.That(engine.Apply(command).accepted, Is.False);
            Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(after));
        }

        [Test]
        public void IncorrectTargetChoiceAndEarlyAdvanceDoNotMutateQuestionOrRandomStream()
        {
            var state = EnterQuestioning(FinalThree()); var engine = new EpisodeEngine(state);
            foreach (var kind in new[] { EpisodeCommandKind.Advance, EpisodeCommandKind.AnswerJury, EpisodeCommandKind.SubmitSpeech })
            {
                var command = EpisodeEngineTests.Command(state, kind); command.targetId = state.playerId; command.secondTargetId = "C";
                Assert.That(engine.Apply(command).accepted, Is.False);
                Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(JsonConvert.SerializeObject(state)));
            }
        }

        [Test]
        public void PlayerJurorQuestionsUseOneFallbackDrawAndNoTrustChange()
        {
            var state = FinalThree();
            state.hohId = state.contestants[1].id; state.finalPart1WinnerId = state.hohId;
            state.finalPart2WinnerId = state.playerId;
            // Use the NPC final-HoH command path, choose a state that evicts the player deterministically.
            foreach (var relationship in state.relationships.Where(r => r.fromId == state.hohId)) relationship.score = relationship.toId == state.playerId ? -100 : 100;
            var engine = new EpisodeEngine(state);
            var next = engine.Apply(EpisodeEngineTests.Command(state, EpisodeCommandKind.Advance));
            Assert.That(next.accepted, Is.True, next.reason); state = next.state;
            Assert.That(state.Find(state.playerId).status, Is.EqualTo(ContestantStatus.Jury));
            Assert.That(state.randomState, Is.EqualTo(FinalThree().randomState), "Preparing a juror prompt has no RNG.");
            var rng = new SeededRandom(state.randomState); rng.NextDouble();
            var c = EpisodeEngineTests.Command(state, EpisodeCommandKind.AnswerJury);
            c.targetId = state.juryExchanges[0].finalistId; c.secondTargetId = "bitter";
            next = engine.Apply(c); Assert.That(next.accepted, Is.True, next.reason);
            Assert.That(next.state.randomState, Is.EqualTo(rng.State));
            Assert.That(next.state.relationships.Select(r => r.score), Is.EqualTo(state.relationships.Select(r => r.score)));
            Assert.That(next.state.juryExchanges[0].answer, Is.Not.Empty);
            Assert.That(next.state.juryExchanges[0].tone, Is.EqualTo("bitter"));
        }

        [Test]
        public void SkipRetainsEarlierAnswersAndSpeechSurvivesReloadWithoutRegeneration()
        {
            var engine = new EpisodeEngine(EnterQuestioning(FinalThree()));
            var answer = EpisodeEngineTests.NextCommand(engine.Snapshot); Assert.That(engine.Apply(answer).accepted, Is.True);
            var completed = engine.Snapshot;
            Assert.That(engine.Apply(EpisodeEngineTests.Command(completed, EpisodeCommandKind.Advance)).accepted, Is.True);
            var result = engine.Apply(EpisodeEngineTests.Command(engine.Snapshot, EpisodeCommandKind.SkipQuestioning));
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.phase, Is.EqualTo(EpisodePhase.FinalSpeeches));
            Assert.That(result.state.juryExchanges, Has.Count.EqualTo(1));
            Assert.That(result.state.juryExchanges.Single().completed, Is.True);
            Assert.That(result.state.relationships.Select(r => r.score), Is.EqualTo(completed.relationships.Select(r => r.score)));
            Assert.That(result.state.finalSpeeches, Has.Count.EqualTo(1));
            var reloaded = new EpisodeEngine(result.state);
            Assert.That(JsonConvert.SerializeObject(reloaded.Snapshot), Is.EqualTo(JsonConvert.SerializeObject(result.state)));
            var command = EpisodeEngineTests.Command(result.state, EpisodeCommandKind.SubmitSpeech);
            command.text = "<b>My own speech</b>\nNo network required.";
            result = reloaded.Apply(command); Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.finalSpeeches.Single(x => x.isPlayerAuthored).text, Is.EqualTo(command.text));
            Assert.That(reloaded.Apply(EpisodeEngineTests.Command(result.state, EpisodeCommandKind.SubmitSpeech)).accepted, Is.False);
            result = reloaded.Apply(EpisodeEngineTests.Command(result.state, EpisodeCommandKind.Advance));
            Assert.That(result.accepted, Is.True, result.reason); Assert.That(result.state.phase, Is.EqualTo(EpisodePhase.Jury));
        }

        [Test]
        public void LongOrControlCharacterSpeechIsRejectedWithoutChangingState()
        {
            var engine = new EpisodeEngine(EnterQuestioning(FinalThree()));
            Assert.That(engine.Apply(EpisodeEngineTests.Command(engine.Snapshot, EpisodeCommandKind.SkipQuestioning)).accepted, Is.True);
            var state = engine.Snapshot;
            foreach (string text in new[] { new string('a', 2001), "invalid\0speech" })
            {
                var command = EpisodeEngineTests.Command(state, EpisodeCommandKind.SubmitSpeech); command.text = text;
                Assert.That(engine.Apply(command).accepted, Is.False);
                Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(JsonConvert.SerializeObject(state)));
            }
        }

        [Test]
        public void NominationMatchesArcAndMentalStateWithoutSpuriousTrustPenalty()
        {
            var state = ContentCatalog.Create(101); state.phase = EpisodePhase.Nomination; state.hohId = state.playerId;
            var c = EpisodeEngineTests.Command(state, EpisodeCommandKind.Nominate);
            c.targetId = state.contestants[1].id; c.secondTargetId = state.contestants[2].id;
            var result = new EpisodeEngine(state).Apply(c); Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.relationships.Select(r => r.score), Is.EqualTo(state.relationships.Select(r => r.score)));
            Assert.That(result.state.randomState, Is.EqualTo(state.randomState));
            Assert.That(result.state.Find(c.targetId).mood, Is.EqualTo("Angry"));
            Assert.That(result.state.Find(c.targetId).stressLevel, Is.EqualTo("Stressed"));
            Assert.That(result.state.relationshipArcs, Has.Count.EqualTo(2));
            Assert.That(result.state.relationshipArcs.All(a => a.weeklyHistory.Single().delta == -8), Is.True);
        }

        [Test]
        public void RecoveryRejectsTamperedQuestionArcAndMentalState()
        {
            var original = EnterQuestioning(FinalThree());
            var bad = original.Clone(); bad.juryExchanges[0].optionA = "tampered";
            Assert.That(EpisodeValidation.TryValidate(bad, out _), Is.False);
            bad = original.Clone(); bad.contestants[0].stressLevel = "Not a state";
            Assert.That(EpisodeValidation.TryValidate(bad, out _), Is.False);
            bad = original.Clone(); bad.juryQuestionIndex = 2;
            Assert.That(EpisodeValidation.TryValidate(bad, out _), Is.False);
            bad = original.Clone(); bad.phase = EpisodePhase.Jury;
            Assert.That(EpisodeValidation.TryValidate(bad, out _), Is.False);
        }

        private static EpisodeState FinalThree()
        {
            var state = ContentCatalog.Create(337); state.week = 4; state.phase = EpisodePhase.FinalEviction;
            foreach (var actor in state.contestants.Skip(3)) actor.status = ContestantStatus.Jury;
            state.hohId = state.playerId; state.finalPart1WinnerId = state.playerId; state.finalPart2WinnerId = state.contestants[1].id;
            return state;
        }

        private static EpisodeState EnterQuestioning(EpisodeState state)
        {
            var command = EpisodeEngineTests.Command(state, EpisodeCommandKind.FinalEvict); command.targetId = state.contestants[2].id;
            var result = new EpisodeEngine(state).Apply(command); Assert.That(result.accepted, Is.True, result.reason); return result.state;
        }
    }
}
