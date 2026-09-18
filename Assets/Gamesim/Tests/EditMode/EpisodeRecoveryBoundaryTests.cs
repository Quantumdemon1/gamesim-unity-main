using System;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>Regression cases found by independently reviewing restore and decision boundaries.</summary>
    public sealed class EpisodeRecoveryBoundaryTests
    {
        [Test]
        public void RestoreRejectsVetoWithNoEligibleLineup()
        {
            var state = WeeklyState(EpisodePhase.Veto);
            state.vetoPlayers.Clear();
            AssertInvalidRestore(state);
        }

        [Test]
        public void RestoreRejectsResolvedCompetitionWithoutItsWinner()
        {
            var state = ContentCatalog.Create(10);
            state.phase = EpisodePhase.HoH;
            state.competitionResolved = true;
            foreach (var contestant in state.Active)
                state.competitionScores.Add(new CompetitionScore { contestantId = contestant.id, score = 5 });
            state.hohId = null;
            AssertInvalidRestore(state);
        }

        [Test]
        public void RestoreRejectsHoHAmongTheirOwnNominees()
        {
            var state = WeeklyState(EpisodePhase.VetoMeeting);
            state.nominees[0] = state.hohId;
            AssertInvalidRestore(state);
        }

        [Test]
        public void RestoreRejectsRegularWeekWithOnlyThreeContestants()
        {
            var state = FinalState(EpisodePhase.HoH);
            AssertInvalidRestore(state);
        }

        [Test]
        public void RestoreRejectsSocialWindowAfterOnlyTwoFinalistsRemain()
        {
            var state = FinalState(EpisodePhase.Social);
            state.contestants[2].status = ContestantStatus.Jury;
            AssertInvalidRestore(state);
        }

        [Test]
        public void RestoreRejectsHalfCommittedNominationPair()
        {
            var state = WeeklyState(EpisodePhase.Nomination);
            state.nominees.RemoveAt(1);
            AssertInvalidRestore(state);
        }

        [TestCase(EpisodePhase.FinalHoHPart2)]
        [TestCase(EpisodePhase.FinalHoHPart3)]
        [TestCase(EpisodePhase.FinalEviction)]
        public void RestoreRejectsFinalStageWithoutQualifyingWinners(EpisodePhase phase)
        {
            var state = FinalState(phase);
            state.finalPart1WinnerId = null;
            AssertInvalidRestore(state);
        }

        [Test]
        public void RestoreRejectsDuplicateFinalQualifiers()
        {
            var state = FinalState(EpisodePhase.FinalHoHPart3);
            state.finalPart2WinnerId = state.finalPart1WinnerId;
            AssertInvalidRestore(state);
        }

        [Test]
        public void RestoreRejectsResolvedFinalWithoutQualifiersInsteadOfThrowingDuringValidation()
        {
            var state = FinalState(EpisodePhase.FinalHoHPart3);
            state.finalPart1WinnerId = null;
            state.finalPart2WinnerId = null;
            state.competitionResolved = true;
            AssertInvalidRestore(state);
        }

        [Test]
        public void RestoreRejectsPreviouslyEvictedBallotInCurrentEviction()
        {
            var state = TiedVoteState();
            state.votes.Add(new VoteState { voterId = state.contestants[5].id, targetId = state.nominees[0] });
            AssertInvalidRestore(state);
        }

        [Test]
        public void RestoreRejectsFinalistCastingAJuryBallot()
        {
            var state = FinalState(EpisodePhase.Jury);
            state.contestants[2].status = ContestantStatus.Jury;
            state.votes.Add(new VoteState { voterId = state.contestants[0].id, targetId = state.contestants[1].id });
            AssertInvalidRestore(state);
        }

        [Test]
        public void FinalHoHPartThreeCreditsExactlyOneWinAcrossRetryAndReload()
        {
            var initial = FinalState(EpisodePhase.FinalHoHPart3);
            foreach (var contestant in initial.contestants) contestant.hohWins = 2;
            int totalBefore = initial.contestants.Sum(c => c.hohWins);
            var engine = new EpisodeEngine(initial);
            var compete = Command(engine.Snapshot, EpisodeCommandKind.Compete);
            compete.performance = 1;
            var result = engine.Apply(compete);
            Assert.That(result.accepted, Is.True, result.reason);
            string winner = result.state.hohId;
            Assert.That(result.state.Find(winner).hohWins, Is.EqualTo(3));
            Assert.That(result.state.contestants.Sum(c => c.hohWins), Is.EqualTo(totalBefore + 1));
            Assert.That(engine.Apply(compete).duplicate, Is.True);
            AssertUnchanged(result.state, engine.Snapshot);

            engine = new EpisodeEngine(engine.Snapshot);
            Assert.That(engine.Apply(compete).duplicate, Is.True, "Receipts survive a restored snapshot.");
            var replay = Command(engine.Snapshot, EpisodeCommandKind.Compete);
            Assert.That(engine.Apply(replay).accepted, Is.False, "An already-resolved competition cannot run again.");
            AssertUnchanged(result.state, engine.Snapshot);

            var advance = Command(engine.Snapshot, EpisodeCommandKind.Advance);
            result = engine.Apply(advance);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.phase, Is.EqualTo(EpisodePhase.FinalEviction));
            Assert.That(result.state.Find(winner).hohWins, Is.EqualTo(3));
            Assert.That(result.state.contestants.Sum(c => c.hohWins), Is.EqualTo(totalBefore + 1));
            engine = new EpisodeEngine(result.state);
            Assert.That(engine.Apply(advance).duplicate, Is.True);
            AssertUnchanged(result.state, engine.Snapshot);
        }

        [Test]
        public void PlayerHoHTieBreakStagesNpcVotesAndSurvivesReloadWithoutRollbackLoop()
        {
            var engine = new EpisodeEngine(TiedVoteState());
            var before = engine.Snapshot;
            var premature = Command(before, EpisodeCommandKind.CastVote);
            premature.targetId = before.nominees[0];
            var result = engine.Apply(premature);
            Assert.That(result.accepted, Is.False, "HoH cannot vote before regular ballots tie.");
            AssertUnchanged(before, engine.Snapshot);

            var collect = Command(before, EpisodeCommandKind.Advance);
            result = engine.Apply(collect);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.votes, Has.Count.EqualTo(2));
            Assert.That(result.state.votes.Select(v => v.targetId).Distinct().Count(), Is.EqualTo(2));
            Assert.That(result.state.evictionResolved, Is.False);
            Assert.That(EpisodeEngine.NeedsPlayerTieBreak(result.state), Is.True);
            var staged = result.state;
            Assert.That(engine.Apply(collect).duplicate, Is.True);
            AssertUnchanged(staged, engine.Snapshot);

            engine = new EpisodeEngine(staged);
            result = engine.Apply(Command(staged, EpisodeCommandKind.Advance));
            Assert.That(result.accepted, Is.False, "The next advance must wait for the player, retaining staged NPC votes.");
            AssertUnchanged(staged, engine.Snapshot);

            var vote = Command(staged, EpisodeCommandKind.CastVote);
            vote.targetId = staged.nominees[1];
            result = engine.Apply(vote);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.votes, Has.Count.EqualTo(3));
            var afterVote = result.state;
            Assert.That(engine.Apply(vote).duplicate, Is.True);
            AssertUnchanged(afterVote, engine.Snapshot);

            engine = new EpisodeEngine(afterVote);
            result = engine.Apply(Command(afterVote, EpisodeCommandKind.Advance));
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.evictionResolved, Is.True);
            Assert.That(result.state.Find(vote.targetId).status, Is.EqualTo(ContestantStatus.Jury));
            Assert.That(result.state.votes.Count(v => v.voterId == result.state.hohId), Is.EqualTo(1));
            Assert.That(result.state.events.Count(e => e.kind == "eviction"), Is.EqualTo(1));
        }

        [Test]
        public void FinalFourNonNominatedVetoHolderCanDeclineButCannotInventAReplacement()
        {
            var state = WeeklyState(EpisodePhase.VetoMeeting);
            state.hohId = state.contestants[1].id;
            state.nominees[0] = state.contestants[2].id;
            state.nominees[1] = state.contestants[3].id;
            foreach (var contestant in state.contestants.Skip(4)) contestant.status = ContestantStatus.Jury;
            state.vetoPlayers = state.Active.Select(c => c.id).ToList();
            state.relationships.Single(r => r.fromId == state.vetoHolderId && r.toId == state.nominees[0]).score = 80;
            Assert.That(EpisodeEngine.ReplacementCandidates(state), Is.Empty);
            Assert.That(EpisodeEngine.NpcVetoSave(state), Is.Null, "No policy may propose a save that requires an impossible replacement.");

            var engine = new EpisodeEngine(state);
            var invalid = Command(state, EpisodeCommandKind.ResolveVeto);
            invalid.useVeto = true;
            invalid.targetId = state.nominees[0];
            CommandResult result = null;
            Assert.DoesNotThrow(() => result = engine.Apply(invalid));
            Assert.That(result.accepted, Is.False);
            AssertUnchanged(state, engine.Snapshot);

            var decline = Command(state, EpisodeCommandKind.ResolveVeto);
            decline.useVeto = false;
            result = engine.Apply(decline);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.vetoResolved, Is.True);
            Assert.That(result.state.nominees, Is.EqualTo(state.nominees));
            engine = new EpisodeEngine(result.state);
            result = engine.Apply(Command(engine.Snapshot, EpisodeCommandKind.Advance));
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.phase, Is.EqualTo(EpisodePhase.Campaign));
        }

        private static EpisodeState WeeklyState(EpisodePhase phase)
        {
            var state = ContentCatalog.Create(10);
            state.phase = phase;
            state.hohId = state.playerId;
            state.vetoHolderId = state.playerId;
            state.nominees.Add(state.contestants[1].id);
            state.nominees.Add(state.contestants[2].id);
            state.vetoPlayers = state.Active.Select(c => c.id).ToList();
            state.vetoResolved = phase == EpisodePhase.Campaign || phase == EpisodePhase.Eviction;
            // A hand-built eviction night is placed at its voting stage. Campaigning is the
            // interaction stage, so a season already sitting in Eviction is past the speeches —
            // and these fixtures are about ballots, which is the stage that takes them.
            if (phase == EpisodePhase.Eviction) state.evictionStage = EvictionStage.Voting;
            return state;
        }

        private static EpisodeState TiedVoteState()
        {
            var state = WeeklyState(EpisodePhase.Eviction);
            state.contestants[5].status = ContestantStatus.Jury;
            state.vetoPlayers.Remove(state.contestants[5].id);
            string first = state.nominees[0], second = state.nominees[1];
            foreach (var relation in state.relationships)
            {
                if (relation.fromId == state.contestants[3].id)
                    relation.score = relation.toId == first ? -50 : relation.toId == second ? 50 : 0;
                if (relation.fromId == state.contestants[4].id)
                    relation.score = relation.toId == first ? 50 : relation.toId == second ? -50 : 0;
            }
            return state;
        }

        private static EpisodeState FinalState(EpisodePhase phase)
        {
            var state = ContentCatalog.Create(90);
            state.week = 4;
            foreach (var contestant in state.contestants.Skip(3)) contestant.status = ContestantStatus.Jury;
            state.phase = phase;
            state.finalPart1WinnerId = state.playerId;
            state.finalPart2WinnerId = state.contestants[1].id;
            state.hohId = state.playerId;
            return state;
        }

        private static void AssertInvalidRestore(EpisodeState state)
        {
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.False, "Malformed state was accepted.");
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.Throws<ArgumentException>(() => new EpisodeEngine(state));
        }

        private static EpisodeCommand Command(EpisodeState state, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = "boundary-" + state.revision,
            actorId = state.playerId,
            expectedRevision = state.revision,
            expectedPhase = state.phase,
            kind = kind
        };

        private static void AssertUnchanged(EpisodeState expected, EpisodeState actual)
        {
            Assert.That(actual.phase, Is.EqualTo(expected.phase));
            Assert.That(actual.revision, Is.EqualTo(expected.revision));
            Assert.That(actual.randomState, Is.EqualTo(expected.randomState));
            Assert.That(actual.nextSequence, Is.EqualTo(expected.nextSequence));
            Assert.That(actual.hohId, Is.EqualTo(expected.hohId));
            Assert.That(actual.evictionResolved, Is.EqualTo(expected.evictionResolved));
            Assert.That(actual.contestants.Select(c => c.status), Is.EqualTo(expected.contestants.Select(c => c.status)));
            Assert.That(actual.contestants.Select(c => c.hohWins), Is.EqualTo(expected.contestants.Select(c => c.hohWins)));
            Assert.That(actual.votes.Select(v => v.voterId + ":" + v.targetId), Is.EqualTo(expected.votes.Select(v => v.voterId + ":" + v.targetId)));
            Assert.That(actual.relationships.Select(r => r.score), Is.EqualTo(expected.relationships.Select(r => r.score)));
            Assert.That(actual.promises.Select(p => p.status), Is.EqualTo(expected.promises.Select(p => p.status)));
            Assert.That(actual.memories.Select(m => m.text), Is.EqualTo(expected.memories.Select(m => m.text)));
            Assert.That(actual.events.Select(e => e.text), Is.EqualTo(expected.events.Select(e => e.text)));
            Assert.That(actual.acceptedCommandIds, Is.EqualTo(expected.acceptedCommandIds));
        }
    }
}
