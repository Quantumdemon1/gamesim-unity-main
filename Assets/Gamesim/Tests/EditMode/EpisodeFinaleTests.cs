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

        /// <summary>
        /// Moods move with the week (playtest, 2026-09-27). A nominee read Angry for the rest of the
        /// season, winning Head of Household or the veto included: the web defines "saved" and
        /// "competition_win" steps and never calls them. Now a competition won, a veto save and
        /// surviving the vote each lift two steps, every new week takes one step back toward
        /// Neutral, and nobody starts a week Angry.
        /// </summary>
        [Test]
        public void MoodsMoveWithTheWeekAndNobodyStartsOneAngry()
        {
            // A season this build starts: the story's rules, the ones stress relief plays under.
            var engine = new EpisodeEngine(StorySeasonTests.StorySeason(77));
            int turns = 0, wins = 0, saves = 0, survivals = 0;
            for (int i = 0; i < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var before = engine.Snapshot;
                var result = engine.Apply(StorySeasonTests.StoryNext(before, 77));
                Assert.That(result.accepted, Is.True, result.reason);
                var after = result.state;
                if (after.week > before.week)
                {
                    turns++;
                    foreach (var guest in after.Active)
                    {
                        Assert.That(MoodAt(after, guest.id), Is.EqualTo(Settled(MoodAt(before, guest.id))),
                            guest.name + " takes one step toward Neutral as week " + after.week + " begins.");
                        Assert.That(guest.mood, Is.Not.EqualTo("Angry"), guest.name + " starts week " + after.week + " Angry.");
                    }
                }
                if (!before.competitionResolved && after.competitionResolved && after.competitionScores.Count > 0
                    && (before.phase == EpisodePhase.HoH || before.phase == EpisodePhase.Veto))
                {
                    wins++;
                    string winner = before.phase == EpisodePhase.HoH ? after.hohId : after.vetoHolderId;
                    Assert.That(MoodAt(after, winner), Is.EqualTo(Math.Min(4, MoodAt(before, winner) + 2)), "Winning lifts two steps.");
                }
                if (!before.vetoResolved && after.vetoResolved)
                    foreach (var saved in before.nominees.Except(after.nominees))
                    {
                        saves++;
                        Assert.That(MoodAt(after, saved), Is.EqualTo(Math.Min(4, MoodAt(before, saved) + 2)), "The veto's save lifts two steps.");
                    }
                if (!before.evictionResolved && after.evictionResolved && after.week == before.week)
                    foreach (var survivor in before.nominees.Where(id => after.Find(id).status == ContestantStatus.Active))
                    {
                        survivals++;
                        Assert.That(MoodAt(after, survivor), Is.EqualTo(Math.Min(4, MoodAt(before, survivor) + 2)), "Surviving the vote lifts two steps.");
                    }
            }
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That((turns > 0, wins > 0, saves > 0, survivals > 0), Is.EqualTo((true, true, true, true)),
                "Every rule came up: " + turns + " turns, " + wins + " wins, " + saves + " saves, " + survivals + " survivals.");
        }

        [Test]
        public void ASeasonBeforeTheStoryRulesKeepsItsMoodsAsTheyWere()
        {
            // Recorded seasons replay exactly: without the story's rules nothing lifts or settles a mood.
            var engine = new EpisodeEngine(ContentCatalog.Create(77));
            for (int i = 0; i < 4000 && engine.Snapshot.week < 3 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var before = engine.Snapshot;
                var result = engine.Apply(EpisodeEngineTests.NextCommand(before));
                Assert.That(result.accepted, Is.True, result.reason);
                foreach (var guest in result.state.contestants)
                    Assert.That(MoodAt(result.state, guest.id), Is.LessThanOrEqualTo(MoodAt(before, guest.id)),
                        guest.name + "'s mood rose in a season without the story's rules.");
            }
            Assert.That(engine.Snapshot.week, Is.GreaterThanOrEqualTo(3), "The walk reached a third week.");
        }

        [Test]
        public void AnAngryNomineeIsAngryAtTheHeadOfHouseholdWhoNominatedThem()
        {
            var state = ContentCatalog.Create(101); state.phase = EpisodePhase.Nomination; state.hohId = state.playerId;
            var c = EpisodeEngineTests.Command(state, EpisodeCommandKind.Nominate);
            c.targetId = state.contestants[1].id; c.secondTargetId = state.contestants[2].id;
            var result = new EpisodeEngine(state).Apply(c); Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(EpisodeEngine.MoodTarget(result.state, c.targetId, out var why), Is.EqualTo(state.playerId));
            Assert.That(why, Is.EqualTo("nominated them in week " + state.week));
            Assert.That(EpisodeEngine.MoodTarget(result.state, state.contestants[3].id, out _), Is.Null, "Nobody is sore at anybody for no reason.");
        }

        private static int MoodAt(EpisodeState s, string id) => Array.IndexOf(EpisodeEngine.Moods, s.Find(id).mood);
        private static int Settled(int mood) => mood < 2 ? mood + 1 : mood > 2 ? mood - 1 : mood;

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

        [Test]
        public void AJurorWhoContinuesBeforeVotingReadsNoBallot()
        {
            var state = ContentCatalog.Create(337); state.week = 4; state.phase = EpisodePhase.Jury;
            string first = state.contestants[1].id, second = state.contestants[2].id;
            foreach (var actor in state.contestants.Where(c => c.id != first && c.id != second)) actor.status = ContestantStatus.Jury;
            state.hohId = first; state.finalPart1WinnerId = first; state.finalPart2WinnerId = second;
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
            var engine = new EpisodeEngine(state);

            var early = engine.Apply(EpisodeEngineTests.Command(engine.Snapshot, EpisodeCommandKind.Advance));
            Assert.That(early.accepted, Is.False);
            Assert.That(early.reason, Does.Contain("Cast your jury vote"));
            Assert.That(engine.Snapshot.votes, Is.Empty, "Not one juror's ballot is cast,");
            Assert.That(engine.Snapshot.events.Any(e => e.kind == "jury-vote"), Is.False, "or read out, before the player's own.");

            var vote = EpisodeEngineTests.Command(engine.Snapshot, EpisodeCommandKind.CastVote); vote.targetId = first;
            var cast = engine.Apply(vote);
            Assert.That(cast.accepted, Is.True, cast.reason);
            var done = engine.Apply(EpisodeEngineTests.Command(engine.Snapshot, EpisodeCommandKind.Advance));
            Assert.That(done.accepted, Is.True, done.reason);
            Assert.That(done.state.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(done.state.votes, Has.Count.EqualTo(state.contestants.Count - 2), "Then the whole jury votes.");
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
