using System;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class EpisodeEngineTests
    {
        [Test]
        public void EverySeed_CanFinishASeasonThroughValidatedCommands()
        {
            for (uint seed = 0; seed < 100; seed++)
            {
                var engine = new EpisodeEngine(ContentCatalog.Create(seed));
                int guard = 0;
                while (engine.Snapshot.phase != EpisodePhase.Finished && guard++ < 260)
                {
                    var command = NextCommand(engine.Snapshot);
                    var result = engine.Apply(command);
                    Assert.That(result.accepted, Is.True, "Seed " + seed + ", " + command.expectedPhase + ", " + command.kind + ": " + result.reason);
                }
                var state = engine.Snapshot;
                Assert.That(state.phase, Is.EqualTo(EpisodePhase.Finished), "Seed " + seed + " did not finish.");
                Assert.That(state.contestants.Count(c => c.status == ContestantStatus.Jury), Is.EqualTo(4));
                Assert.That(state.winnerId, Is.Not.Empty);
                Assert.That(state.runnerUpId, Is.Not.EqualTo(state.winnerId));
                Assert.That(state.week, Is.EqualTo(4));
            }
        }

        [Test]
        public void RejectedAndDuplicateCommands_DoNotConsumeRandomnessOrChangeState()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(123));
            var before = engine.Snapshot;
            var command = NextCommand(before); command.expectedRevision++;
            Assert.That(engine.Apply(command).accepted, Is.False);
            AssertEquivalent(before, engine.Snapshot);
            command.expectedRevision = before.revision;
            Assert.That(engine.Apply(command).accepted, Is.True);
            var after = engine.Snapshot;
            Assert.That(engine.Apply(command).duplicate, Is.True);
            AssertEquivalent(after, engine.Snapshot);
            command = NextCommand(after); command.performance = double.NaN;
            Assert.That(engine.Apply(command).accepted, Is.False);
            AssertEquivalent(after, engine.Snapshot);
        }

        [Test]
        public void SnapshotAndInputAreDetachedFromCommittedAuthority()
        {
            var input = ContentCatalog.Create(45);
            var engine = new EpisodeEngine(input);
            input.contestants[0].name = "Tampered"; input.randomState = 2;
            var snapshot = engine.Snapshot;
            Assert.That(snapshot.contestants[0].name, Is.Not.EqualTo("Tampered"));
            snapshot.contestants.Clear(); snapshot.acceptedCommandIds.Add("fake");
            Assert.That(engine.Snapshot.contestants, Has.Count.EqualTo(6));
            Assert.That(engine.Snapshot.acceptedCommandIds, Is.Empty);
        }

        [Test]
        public void ReloadingEveryCommand_ProducesIdenticalFinalOutcome()
        {
            var a = new EpisodeEngine(ContentCatalog.Create(890));
            var b = new EpisodeEngine(ContentCatalog.Create(890));
            for (int i = 0; i < 260 && a.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var command = NextCommand(a.Snapshot);
                Assert.That(a.Apply(command).accepted, Is.True);
                Assert.That(b.Apply(command).accepted, Is.True);
                b = new EpisodeEngine(b.Snapshot);
                AssertEquivalent(a.Snapshot, b.Snapshot);
            }
            Assert.That(a.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished));
        }

        [Test]
        public void BetrayingSafety_ChangesTrustAndLaterNominationPreference()
        {
            var initial = ContentCatalog.Create(12);
            var target = initial.contestants.First(c => !c.isPlayer).id;
            var engine = new EpisodeEngine(initial);
            var promise = Command(engine.Snapshot, EpisodeCommandKind.PromiseSafety); promise.targetId = target;
            Assert.That(engine.Apply(promise).accepted, Is.True);
            var state = engine.Snapshot;
            state.phase = EpisodePhase.Nomination; state.hohId = state.playerId;
            engine = new EpisodeEngine(state);
            var nominate = Command(state, EpisodeCommandKind.Nominate);
            nominate.targetId = target; nominate.secondTargetId = state.contestants.Last().id;
            var result = engine.Apply(nominate);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.promises.Single().status, Is.EqualTo(PromiseStatus.Broken));
            Assert.That(result.state.Score(target, state.playerId), Is.LessThan(state.Score(target, state.playerId) - 20));
            Assert.That(result.state.memories.Any(m => m.ownerId == target && m.subjectId == state.playerId && m.text.Contains("broke")), Is.True);
            // A later HoH nomination reads committed trust, not an unrelated random/canned response.
            state = result.state; state.phase = EpisodePhase.Nomination; state.hohId = target; state.nominees.Clear();
            engine = new EpisodeEngine(state);
            result = engine.Apply(Command(state, EpisodeCommandKind.Advance));
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.nominees, Does.Contain(state.playerId));
        }

        [Test]
        public void InvalidNominationAndVeto_AreAtomic()
        {
            var state = ContentCatalog.Create(56); state.phase = EpisodePhase.Nomination; state.hohId = state.playerId;
            var engine = new EpisodeEngine(state);
            var bad = Command(state, EpisodeCommandKind.Nominate); bad.targetId = state.playerId; bad.secondTargetId = state.contestants[1].id;
            Assert.That(engine.Apply(bad).accepted, Is.False); AssertEquivalent(state, engine.Snapshot);
            bad.targetId = state.contestants[1].id; bad.secondTargetId = bad.targetId;
            Assert.That(engine.Apply(bad).accepted, Is.False); AssertEquivalent(state, engine.Snapshot);
            bad.secondTargetId = state.contestants[2].id; Assert.That(engine.Apply(bad).accepted, Is.True);
            state = engine.Snapshot; state.phase = EpisodePhase.VetoMeeting; state.vetoHolderId = state.playerId;
            state.vetoPlayers = state.Active.Select(c => c.id).ToList();
            engine = new EpisodeEngine(state);
            bad = Command(state, EpisodeCommandKind.ResolveVeto); bad.useVeto = true; bad.targetId = state.nominees[0]; bad.secondTargetId = state.playerId;
            Assert.That(engine.Apply(bad).accepted, Is.False); AssertEquivalent(state, engine.Snapshot);
        }

        private static void AssertEquivalent(EpisodeState a, EpisodeState b)
        {
            Assert.That(b.revision, Is.EqualTo(a.revision)); Assert.That(b.randomState, Is.EqualTo(a.randomState));
            Assert.That(b.phase, Is.EqualTo(a.phase)); Assert.That(b.winnerId, Is.EqualTo(a.winnerId));
            Assert.That(b.events.Select(e => e.text), Is.EqualTo(a.events.Select(e => e.text)));
            Assert.That(b.relationships.Select(r => r.score), Is.EqualTo(a.relationships.Select(r => r.score)));
            Assert.That(b.nominees, Is.EqualTo(a.nominees)); Assert.That(b.acceptedCommandIds, Is.EqualTo(a.acceptedCommandIds));
        }

        public static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
            { id = "test-" + s.revision, actorId = s.playerId, expectedRevision = s.revision, expectedPhase = s.phase, kind = kind };

        /// <summary>
        /// Walks eviction night to the stage at which the house votes.
        ///
        /// <para>Reaching a ballot used to be one Advance out of campaigning. The night now runs
        /// interaction, then speeches, then voting, so a test that wants to cast a vote has to walk
        /// it to the point where voting is open — which is what a player does too.</para>
        /// </summary>
        public static void OpenTheVote(EpisodeEngine engine)
        {
            for (int guard = 0; guard < 8; guard++)
            {
                var state = engine.Snapshot;
                if (state.phase == EpisodePhase.Eviction && (state.evictionStage == EvictionStage.Voting
                    || state.evictionStage == EvictionStage.Tiebreaker)) return;
                var result = engine.Apply(NextCommand(state));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            Assert.Fail("Eviction night did not reach its voting stage.");
        }

        public static EpisodeCommand NextCommand(EpisodeState s)
        {
            var c = Command(s, EpisodeCommandKind.Advance);
            if (s.pendingDiary != null) { c.kind = EpisodeCommandKind.SkipDiary; c.targetId = s.pendingDiary.id; }
            else if (EpisodeEngine.IsCompetition(s.phase) && !s.competitionResolved && EpisodeEngine.CompetitionPlayers(s).Any(p => p.isPlayer))
            { c.kind = EpisodeCommandKind.Compete; c.performance = 0.5; }
            else if (s.phase == EpisodePhase.Nomination && s.nominees.Count == 0 && s.hohId == s.playerId)
            {
                c.kind = EpisodeCommandKind.Nominate; var pool = EpisodeEngine.NominationCandidates(s).ToArray(); c.targetId = pool[0].id; c.secondTargetId = pool[1].id;
            }
            else if (s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved && (s.vetoHolderId == s.playerId || (s.hohId == s.playerId && EpisodeEngine.NpcVetoSave(s) != null)))
            {
                // Declining is the only legal answer when the Final 4 lock applies, and it is
                // what the HUD offers there too.
                c.kind = EpisodeCommandKind.ResolveVeto;
                c.useVeto = EpisodeEngine.ReplacementCandidates(s).Any() && !EpisodeEngine.VetoIsLockedAtFinalFour(s);
                c.targetId = s.vetoHolderId == s.playerId ? s.nominees[0] : EpisodeEngine.NpcVetoSave(s);
                c.secondTargetId = EpisodeEngine.ReplacementCandidates(s).FirstOrDefault()?.id;
            }
            else if (s.phase == EpisodePhase.Eviction && s.evictionStage == EvictionStage.Speeches
                && s.nominees.Contains(s.playerId) && !s.evictionSpeeches.Any(x => x.speakerId == s.playerId))
            { c.kind = EpisodeCommandKind.SubmitEvictionSpeech; c.text = "I'd like to stay. I've been straight with all of you."; }
            else if (s.phase == EpisodePhase.Eviction && !s.evictionResolved
                && (s.evictionStage == EvictionStage.Voting || s.evictionStage == EvictionStage.Tiebreaker)
                && !s.votes.Any(v => v.voterId == s.playerId) &&
                (EpisodeEngine.Voters(s).Any(v => v.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(s)))
            { c.kind = EpisodeCommandKind.CastVote; c.targetId = s.nominees[0]; }
            else if (s.phase == EpisodePhase.FinalEviction && s.hohId == s.playerId)
            { c.kind = EpisodeCommandKind.FinalEvict; c.targetId = s.Active.First(p => !p.isPlayer).id; }
            else if (s.phase == EpisodePhase.JuryQuestioning && !s.juryExchanges[s.juryQuestionIndex].completed)
            {
                var exchange = s.juryExchanges[s.juryQuestionIndex]; c.kind = EpisodeCommandKind.AnswerJury;
                c.targetId = exchange.finalistId == s.playerId ? exchange.questionerId : exchange.finalistId;
                c.secondTargetId = exchange.finalistId == s.playerId ? "A" : "neutral";
            }
            else if (s.phase == EpisodePhase.FinalSpeeches && s.Active.Any(p => p.isPlayer) && !s.finalSpeeches.Any(p => p.speakerId == s.playerId))
            { c.kind = EpisodeCommandKind.SubmitSpeech; c.text = "I stood by my choices. Thank you for sharing this season."; }
            else if (s.phase == EpisodePhase.Jury && !s.Active.Any(p => p.isPlayer) && !s.votes.Any(v => v.voterId == s.playerId))
            { c.kind = EpisodeCommandKind.CastVote; c.targetId = s.Active.First().id; }
            return c;
        }
    }
}
