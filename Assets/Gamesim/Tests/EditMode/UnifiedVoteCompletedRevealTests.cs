using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Pure local reveal-proof tests. Actual factory/public-Apply positives are separate from
    /// detached tamper/removal-boundary controls. The bounded public witness searches found real
    /// ties/majorities, but no qualifying ordinary-voter production removal: that runtime gate
    /// remains open. No archive is installed, private knowledge granted, or authority activated.
    /// </summary>
    public sealed class UnifiedVoteCompletedRevealTests
    {
        private sealed class Played
        {
            internal EpisodeState First, Social, Finished;
            internal readonly Dictionary<int, EpisodeState> Reveals = new Dictionary<int, EpisodeState>();
            internal readonly Dictionary<int, EpisodeState> Turned = new Dictionary<int, EpisodeState>();
        }
        private static readonly Dictionary<string, Played> Games = new Dictionary<string, Played>(StringComparer.Ordinal);
        private static readonly object Gate = new object();

        [TestCase(0, 4)] [TestCase(0, 5)] [TestCase(0, 6)] [TestCase(0, 8)] [TestCase(0, 10)] [TestCase(0, 12)]
        [TestCase(1, 4)] [TestCase(1, 5)] [TestCase(1, 6)] [TestCase(1, 8)] [TestCase(1, 10)] [TestCase(1, 12)]
        public void ActualPublicCompletedRegularRevealHasExactFactoryCastAndPrivateBox(int mode, int size)
        {
            var s = Game(mode, size).First.Clone();
            Assert.That(s.contestants.Count, Is.EqualTo(size));
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.Eviction));
            Assert.That(s.evictionStage, Is.EqualTo(EvictionStage.Results));
            SourceValid(s);
            Probe(s, Reveal(s), true);
        }

        [TestCase(0, 4)] [TestCase(0, 8)] [TestCase(0, 12)]
        [TestCase(1, 4)] [TestCase(1, 8)] [TestCase(1, 12)]
        public void ActualPostRevealSocialRetainsTheCompletedPrivateBox(int mode, int size)
        {
            var s = Game(mode, size).Social.Clone();
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.Social));
            Assert.That(s.evictionResolved, Is.True);
            Assert.That(s.evictionStage, Is.EqualTo(EvictionStage.Interaction));
            SourceValid(s);
            Probe(s, Reveal(s), true);
        }

        [TestCase(0)] [TestCase(1)]
        public void ActualWeekTurnUsesDurablePowerRatherThanTheClearedCurrentBox(int mode)
        {
            var played = Game(mode, 8);
            var s = played.Turned[1].Clone();
            Assert.That(s.week, Is.EqualTo(2));
            Assert.That(s.votes, Is.Empty);
            SourceValid(s);
            Probe(s, Reveal(played.First), true);
        }

        [TestCase(0, 4)] [TestCase(0, 8)] [TestCase(1, 4)] [TestCase(1, 8)]
        public void ActualFinishedSeasonPreservesOldRegularRevealsAcrossFinalChoiceAndJuryBallots(int mode, int size)
        {
            var played = Game(mode, size);
            var s = played.Finished.Clone();
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(s.ledger.power.Any(row => row.evicteeId != null && row.tally.Count == 0), Is.True,
                "The actual final-eviction row must remain distinct from a regular reveal.");
            SourceValid(s);
            Probe(s, Reveal(played.First), true);
        }

        [TestCase(0, false)] [TestCase(0, true)] [TestCase(1, false)] [TestCase(1, true)]
        public void ActualSourceWitnessTieTallyExcludesTheRealHohDecidingBallot(int mode, bool tied)
        {
            // Both immutable source-witness searches observed these exact ordinary seed1 casts.
            var s = Game(mode, tied ? 5 : 4).First.Clone();
            var row = Power(s);
            Assert.That(row.tally[0] == row.tally[1], Is.EqualTo(tied));
            Assert.That(s.votes.Count(vote => vote.voterId == row.hohId), Is.EqualTo(tied ? 1 : 0));
            Assert.That(row.tally.Sum(), Is.EqualTo(s.votes.Count - (tied ? 1 : 0)));
            SourceValid(s);
            Probe(s, Reveal(s), true);
        }

        [TestCase(0)] [TestCase(1)]
        public void DetachedSameWeekRemovalBoundaryKeepsItsEarlierOrdinaryVoterButPriorWeekRemovalDoesNot(int mode)
        {
            // This is an explicit detached chronology control, NOT a public removal witness or
            // a full-save-valid claim. Its baseline is actual week2 public voting and week3 turn.
            var played = Game(mode, 8);
            var source = played.Reveals[2];
            var s = played.Turned[2].Clone();
            var reveal = Reveal(source);
            SourceValid(s);
            Probe(s, reveal, true);
            string ordinary = reveal.ballots.Select(ballot => ballot.voterId)
                .First(id => id != Power(source).hohId && s.Find(id).status == ContestantStatus.Active);
            s.Find(ordinary).status = ContestantStatus.Expelled;
            s.story.removals.Add(new RemovalState { contestantId = ordinary, week = reveal.week, reasonId = "conduct" });
            Assert.That(reveal.ballots.Any(ballot => ballot.voterId == ordinary), Is.True);
            Probe(s, reveal, true);
            s.story.removals.Last().week = reveal.week - 1;
            Probe(s, reveal, false);
        }

        [TestCase(0, 0)] [TestCase(0, 1)] [TestCase(0, 2)]
        [TestCase(1, 0)] [TestCase(1, 1)] [TestCase(1, 2)]
        public void DetachedOrderingChangesCannotChangeARecordedVerdictOrRewriteInputs(int mode, int order)
        {
            var played = Game(mode, 8);
            var s = played.Finished.Clone();
            var reveal = Reveal(played.First);
            if (order == 0) reveal.ballots.Reverse();
            if (order == 1) s.contestants.Reverse();
            if (order == 2) s.ledger.power.Reverse();
            Probe(s, reveal, true);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)]
        public void InvalidRevealAndContextBoundsAreRefusedWithoutNormalization(int defect)
        {
            var s = Game(0, 5).First.Clone(); var reveal = Reveal(s);
            Probe(s, reveal, true);
            switch (defect)
            {
                case 0: reveal = null; break;
                case 1: reveal.week = 0; break;
                case 2: reveal.week = s.week + 1; break;
                case 3: reveal.ballots = null; break;
                case 4: reveal.ballots.Add(null); break;
                case 5: reveal.ballots = Enumerable.Range(0, 17).Select(_ => reveal.ballots[0].Clone()).ToList(); break;
                case 6: s = null; break;
                case 7: s.week = 0; break;
                case 8: s.week = 101; break;
                case 9: s.phase = (EpisodePhase)999; break;
            }
            Probe(s, reveal, false);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void WholeCastIsValidatedBeforeAnySelectedRoleLookup(int defect)
        {
            var s = Game(0, 5).First.Clone(); var reveal = Reveal(s);
            Probe(s, reveal, true);
            switch (defect)
            {
                case 0: s.contestants = null; break;
                case 1: s.contestants.RemoveRange(2, s.contestants.Count - 2); break;
                case 2: while (s.contestants.Count < 17) s.contestants.Add(s.contestants[0].Clone()); break;
                case 3: s.contestants[0] = null; break;
                case 4: s.contestants[0].id = s.contestants[1].id; break;
                case 5: s.contestants[0].id = " "; break;
                case 6: s.contestants[0].id = new string('x', 101); break;
                case 7: s.contestants[0].status = (ContestantStatus)999; break;
            }
            Probe(s, reveal, false);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void DetachedKnownIdentityAndAllReferencesEnforceControlAndExactLengthBoundaries(int boundary)
        {
            // This is a detached local identity control, not an authored public character or a
            // whole-save claim. Rename a real ordinary voter everywhere so failure cannot be
            // attributed merely to an unknown cast reference or a mismatched private box.
            var s = Game(0, 5).First.Clone(); var reveal = Reveal(s);
            SourceValid(s);
            Probe(s, reveal, true);
            string oldId = reveal.ballots.First(ballot => ballot.voterId != Power(s).hohId).voterId;
            string[] controls = { "\n", "\r", "\t", "\0", "\u001f", "\u007f" };
            string newId = boundary < controls.Length ? "known" + controls[boundary] + "voter"
                : new string('x', boundary == 6 ? 100 : 101);
            RewriteExactId(s, oldId, newId);
            RewriteExactId(reveal, oldId, newId);
            Assert.That(s.contestants.Count(person => person.id == newId), Is.EqualTo(1));
            Assert.That(s.votes.Any(vote => vote.voterId == newId), Is.True);
            Assert.That(reveal.ballots.Any(ballot => ballot.voterId == newId), Is.True);
            Assert.That(s.votes.Any(vote => vote.voterId == oldId || vote.targetId == oldId), Is.False);
            Assert.That(reveal.ballots.Any(ballot => ballot.voterId == oldId || ballot.targetId == oldId), Is.False);
            Assert.That(s.votes.Select(vote => vote.voterId), Is.EqualTo(reveal.ballots.Select(ballot => ballot.voterId)));
            Probe(s, reveal, boundary == 6);
        }

        [TestCase(0, 16)] [TestCase(0, 17)] [TestCase(1, 16)] [TestCase(1, 17)]
        public void DetachedUniqueCastCapacityRetainsEveryOrdinaryBallotAtTheLocalSixteenLimit(int mode, int size)
        {
            // SeasonBuilder clamps its actual creator roster to twelve before fitting custom
            // guests. This is expressly a detached LOCAL sixteen-capacity control, never public
            // play or full-save acceptance. Its genuine four-person source reveal is proved first.
            var s = Game(mode, 4).First.Clone(); var reveal = Reveal(s); var row = Power(s);
            SourceValid(s);
            Probe(s, reveal, true);
            Assert.That(row.tally[0] == row.tally[1], Is.False);
            var ordinary = s.Find(reveal.ballots.Single(ballot => ballot.voterId != row.hohId).voterId);
            int targetIndex = row.nominees.IndexOf(row.evicteeId);
            while (s.contestants.Count < size)
            {
                var extra = ordinary.Clone();
                extra.id = "detached-capacity-" + s.contestants.Count;
                extra.isPlayer = false; extra.status = ContestantStatus.Active;
                s.contestants.Add(extra);
                s.votes.Add(new VoteState { voterId = extra.id, targetId = row.evicteeId,
                    reason = "Detached local capacity control" });
                reveal.ballots.Add(new UnifiedVoteBallotState { voterId = extra.id, targetId = row.evicteeId });
                row.tally[targetIndex]++;
            }
            Assert.That(s.contestants.Count, Is.EqualTo(size));
            Assert.That(s.contestants.Select(person => person.id).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(size));
            Assert.That(reveal.ballots.Count, Is.EqualTo(size - 3));
            Assert.That(row.tally.Sum(), Is.EqualTo(reveal.ballots.Count));
            Probe(s, reveal, size == EpisodeValidation.MaximumCast);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)]
        public void IncompleteAmbiguousOrUnownedPowerCannotBecomeARegularReveal(int defect)
        {
            var s = Game(0, 5).First.Clone(); var reveal = Reveal(s); var row = Power(s);
            Probe(s, reveal, true);
            switch (defect)
            {
                case 0: s.ledger = null; break;
                case 1: s.ledger.power = null; break;
                case 2: s.ledger.power.Add(null); break;
                case 3: s.ledger.power.Add(row.Clone()); break;
                case 4: s.ledger.power.Clear(); break;
                case 5: row.hohId = "unknown"; break;
                case 6: row.vetoHolderId = null; break;
                case 7: row.nominees = null; break;
                case 8: row.nominees[1] = row.nominees[0]; break;
                case 9: row.tally = null; break;
            }
            Probe(s, reveal, false);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)]
        public void TheActualVetoDecisionMustOwnItsSavedAndReplacementRoles(int defect)
        {
            var s = Game(0, 5).First.Clone(); var reveal = Reveal(s); var row = Power(s);
            Assert.That(row.vetoUsed, Is.True, "The actual seed1 source witness used its veto.");
            Probe(s, reveal, true);
            switch (defect)
            {
                case 0: row.vetoUsed = false; break;
                case 1: row.savedId = null; break;
                case 2: row.replacementId = null; break;
                case 3: row.savedId = row.hohId; break;
                case 4: row.savedId = row.nominees[0]; break;
                case 5: row.replacementId = row.vetoHolderId; break;
                case 6: row.replacementId = row.savedId; break;
                case 7: s.vetoResolved = false; break;
                case 8: row.savedId = "unknown"; break;
                case 9: row.replacementId = "unknown"; break;
            }
            Probe(s, reveal, false);
        }

        [TestCase(0)] [TestCase(1)]
        public void ActualFourPersonUsedVetoHolderBelongedToTheOriginalBlock(int mode)
        {
            var s = Game(mode, 4).First.Clone(); var row = Power(s);
            SourceValid(s);
            Assert.That(s.contestants.Count, Is.EqualTo(4));
            Assert.That(row.vetoUsed, Is.True, "The actual seed1 four-person source witness used its veto.");
            Assert.That(row.vetoHolderId == row.savedId
                || row.nominees.Any(id => id != row.replacementId && id == row.vetoHolderId), Is.True);
            Probe(s, Reveal(s), true);
        }

        [TestCase(0, 0)] [TestCase(0, 1)] [TestCase(0, 2)]
        [TestCase(1, 0)] [TestCase(1, 1)] [TestCase(1, 2)]
        public void DetachedOriginalBlockLockUsesTheHistoricalRosterNotHolderIdentityAlone(int mode, int context)
        {
            // These are detached local role controls, NOT public veto commands or whole saves.
            // At four an otherwise matching used-veto row with the HoH holding veto is unlawful;
            // the same off-block holder is allowed at five. The old reveal is retained unchanged.
            var played = Game(mode, context == 2 ? 5 : 4);
            var s = context == 1 ? played.Finished.Clone() : played.First.Clone();
            var reveal = Reveal(played.First);
            SourceValid(s);
            Probe(s, reveal, true);
            var row = s.ledger.power.Single(power => power.week == reveal.week);
            Assert.That(row.vetoUsed, Is.True);
            if (context == 1)
            {
                Assert.That(s.phase, Is.EqualTo(EpisodePhase.Finished));
                Assert.That(reveal.week, Is.LessThan(s.week));
            }
            string actualBoxBefore = Fingerprint(s.votes);
            row.vetoHolderId = row.hohId;
            if (reveal.week == s.week) s.vetoHolderId = row.vetoHolderId;
            Assert.That(row.vetoHolderId == row.savedId
                || row.nominees.Any(id => id != row.replacementId && id == row.vetoHolderId), Is.False);
            Assert.That(Fingerprint(s.votes), Is.EqualTo(actualBoxBefore), "Do not manufacture a private box to trigger this role guard.");
            if (reveal.week == s.week)
                Assert.That(s.votes.Select(vote => vote.voterId), Is.EqualTo(reveal.ballots.Select(ballot => ballot.voterId)));
            Probe(s, reveal, context == 2);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)] [TestCase(12)] [TestCase(13)]
        public void DurableHistoryBoundsAndEarlierDeparturesCannotBeHiddenByAValidSelectedRow(int defect)
        {
            var played = Game(0, 8);
            EpisodeState s; UnifiedVoteRevealState reveal;
            if (defect >= 8 && defect <= 11)
            {
                // Explicit detached removal-boundary control, not public/full-save acceptance.
                s = played.Turned[2].Clone(); reveal = Reveal(played.Reveals[2]);
                string ordinary = reveal.ballots.Select(ballot => ballot.voterId)
                    .First(id => id != Power(played.Reveals[2]).hohId && s.Find(id).status == ContestantStatus.Active);
                s.Find(ordinary).status = ContestantStatus.Expelled;
                s.story.removals.Add(new RemovalState { contestantId = ordinary, week = reveal.week, reasonId = "conduct" });
            }
            else if (defect >= 12) { s = played.Finished.Clone(); reveal = Reveal(played.Reveals[2]); }
            else { s = played.First.Clone(); reveal = Reveal(s); }
            var row = s.ledger.power.Single(power => power.week == reveal.week);
            Probe(s, reveal, true);
            switch (defect)
            {
                case 0: while (s.ledger.power.Count <= SeasonLedger.MostRows) s.ledger.power.Add(row.Clone()); break;
                case 1: row.week = 0; break;
                case 2: row.week = s.week + 1; break;
                case 3: row.hohId = null; break;
                case 4: row.evicteeId = null; break;
                case 5: row.tally.Clear(); break;
                case 6: row.hohId = row.nominees[0]; break;
                case 7: row.nominees[0] = "unknown"; break;
                case 8: while (s.story.removals.Count <= s.contestants.Count) s.story.removals.Add(s.story.removals[0].Clone()); break;
                case 9: s.story.removals[0].contestantId = "unknown"; break;
                case 10: s.story.removals[0].reasonId = "invented"; break;
                case 11: s.story.removals[0].week = s.week + 1; break;
                case 12: row.hohId = s.ledger.power.Single(power => power.week == 1).evicteeId; break;
                case 13: row.vetoHolderId = s.ledger.power.Single(power => power.week == 1).evicteeId; break;
            }
            Probe(s, reveal, false);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void MissingRepeatedAndContradictoryDeparturesCannotForgePastEligibility(int defect)
        {
            var s = Game(0, 8).Finished.Clone(); var reveal = Reveal(Game(0, 8).First);
            var row = s.ledger.power.First(power => power.week == reveal.week);
            string evicted = row.evicteeId;
            Probe(s, reveal, true);
            switch (defect)
            {
                case 0: s.story = null; break;
                case 1: s.story.removals = null; break;
                case 2: s.story.removals.Add(null); break;
                case 3: s.Find(evicted).status = ContestantStatus.Active; break;
                case 4: s.Find(evicted).status = ContestantStatus.Expelled; break;
                case 5: row.evicteeId = null; break;
                case 6: s.ledger.power.Last(power => power.week != row.week).evicteeId = evicted; break;
                case 7: s.story.removals.Add(new RemovalState { contestantId = evicted, week = row.week, reasonId = "conduct" }); break;
            }
            Probe(s, reveal, false);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void EveryActualOrdinaryVoterAndOnlyTheFinalBlockAreRequired(int defect)
        {
            var s = Game(0, 5).First.Clone(); var reveal = Reveal(s); var row = Power(s);
            Probe(s, reveal, true);
            var ordinary = reveal.ballots.First(ballot => ballot.voterId != row.hohId);
            switch (defect)
            {
                case 0: reveal.ballots.Remove(ordinary); break;
                case 1: reveal.ballots.Add(ordinary.Clone()); break;
                case 2: ordinary.voterId = "unknown"; break;
                case 3: ordinary.voterId = row.nominees[0]; break;
                case 4: ordinary.targetId = "unknown"; break;
                case 5: ordinary.targetId = row.hohId; break;
                case 6: ordinary.voterId = null; break;
                case 7: ordinary.targetId = null; break;
            }
            Probe(s, reveal, false);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void OrdinaryTallyAndTheActualTieBreakHaveDifferentRoles(int defect)
        {
            var s = Game(0, defect == 5 ? 4 : 5).First.Clone(); var reveal = Reveal(s); var row = Power(s);
            Probe(s, reveal, true);
            switch (defect)
            {
                case 0: row.tally[0]++; break;
                case 1: row.tally[1] = -1; break;
                case 2: row.tally.Add(0); break;
                case 3: reveal.ballots.RemoveAll(ballot => ballot.voterId == row.hohId); break;
                case 4: reveal.ballots.First(ballot => ballot.voterId == row.hohId).targetId = row.nominees.First(id => id != row.evicteeId); break;
                case 5: reveal.ballots.Add(new UnifiedVoteBallotState { voterId = row.hohId, targetId = row.evicteeId }); break;
            }
            Probe(s, reveal, false);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void CurrentWeekMustMatchItsActualCommittedPrivateBox(int defect)
        {
            var s = Game(0, 5).First.Clone(); var reveal = Reveal(s);
            Probe(s, reveal, true);
            switch (defect)
            {
                case 0: s.votes.Clear(); break;
                case 1: s.votes[0].targetId = s.nominees.First(id => id != s.votes[0].targetId); break;
                case 2: s.votes.Add(s.votes[0].Clone()); break;
                case 3: s.nominees.Reverse(); break;
            }
            Probe(s, reveal, false);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void UnfinishedCurrentStagesAndFinalChoiceRowsAreNotCompletedRegularReveals(int defect)
        {
            var played = Game(0, 5); var s = played.First.Clone(); var reveal = Reveal(s);
            Probe(s, reveal, true);
            switch (defect)
            {
                case 0: s.evictionResolved = false; break;
                case 1: s.evictionStage = EvictionStage.Voting; break;
                case 2: s.phase = EpisodePhase.Campaign; s.evictionStage = EvictionStage.Interaction; break;
                case 3:
                    s = played.Finished.Clone();
                    reveal.week = s.ledger.power.Single(row => row.evicteeId != null && row.tally.Count == 0).week;
                    break;
            }
            Probe(s, reveal, false);
        }

        [TestCase(0)] [TestCase(1)]
        public void RepeatProofLeavesAllInputFieldsRulesArchiveAndPrivateKnowledgeUnchanged(int mode)
        {
            var s = Game(mode, 5).First.Clone(); var reveal = Reveal(s);
            string stateBefore = Fingerprint(s), revealBefore = Fingerprint(reveal);
            for (int repeat = 0; repeat < 3; repeat++) Probe(s, reveal, true);
            var detached = reveal.Clone(); detached.ballots[0].targetId = "detached-only";
            Assert.That(Fingerprint(s), Is.EqualTo(stateBefore));
            Assert.That(Fingerprint(reveal), Is.EqualTo(revealBefore));
            Assert.That(s.unifiedCommitmentRulesVersion, Is.EqualTo(mode));
            Assert.That(s.unifiedHearingRulesVersion, Is.EqualTo(mode));
            Assert.That(s.unifiedVoteReveals, Is.Empty);
            Assert.That(EpisodeValidation.TryValidate(s, out var reason), Is.True, reason);
        }

        private static Played Game(int mode, int size)
        {
            string key = mode + ":" + size;
            lock (Gate)
            {
                if (Games.TryGetValue(key, out var played)) return played;
                var initial = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size, Roster = CastTemplates.Roster.Regular }, 1);
                Assert.That(initial.contestants.Count, Is.EqualTo(size), "No factory clamp may masquerade as tested cast coverage.");
                initial.competitionRulesVersion = CompetitionRules.Current;
                initial.haveNotRulesStartWeek = 1; initial.strategyRulesStartWeek = 1;
                EpisodeEngine.EnableStory(initial); EpisodeEngine.EnableRead(initial); EpisodeEngine.EnableLevers(initial);
                EpisodeEngine.EnableWeek(initial); EpisodeEngine.EnableEconomy(initial); EpisodeEngine.EnableAgency(initial);
                EpisodeEngine.EnableFinale(initial); EpisodeEngine.EnableCommitments(initial);
                initial.unifiedCommitmentRulesVersion = mode; initial.unifiedHearingRulesVersion = mode;
                SourceValid(initial);
                string initialBefore = Fingerprint(initial);
                var engine = new EpisodeEngine(initial);
                Assert.That(Fingerprint(initial), Is.EqualTo(initialBefore));
                played = new Played();
                for (int step = 0; step < 512 && engine.Snapshot.phase != EpisodePhase.Finished; step++)
                {
                    var before = engine.Snapshot;
                    var command = EpisodeEngineTests.NextCommand(before);
                    if (command.kind == EpisodeCommandKind.CastVote && before.phase == EpisodePhase.Eviction)
                        command.targetId = before.nominees[1]; // Exact seed1 ordinary witness policy.
                    if (command.kind == EpisodeCommandKind.AnswerJury && EpisodeEngine.FinaleOn(before))
                    {
                        var exchange = before.juryExchanges[before.juryQuestionIndex];
                        if (exchange.finalistId == before.playerId)
                            command.secondTargetId = FinaleQuestions.Offered(exchange.category, exchange.receiptKind)[0];
                    }
                    string input = Fingerprint(command);
                    var result = engine.Apply(command);
                    Assert.That(result.accepted && !result.duplicate, Is.True,
                        "Actual public command " + command.kind + " at " + before.week + "/" + before.phase + ": " + result.reason);
                    Assert.That(Fingerprint(command), Is.EqualTo(input));
                    Assert.That(result.state.revision, Is.EqualTo(before.revision + 1));
                    Assert.That(result.state.acceptedCommandIds.Last(), Is.EqualTo(command.id));
                    SourceValid(result.state);
                    if (!before.evictionResolved && result.state.evictionResolved && result.state.phase == EpisodePhase.Eviction)
                    {
                        played.Reveals.Add(result.state.week, result.state);
                        if (played.First == null) played.First = result.state;
                    }
                    if (before.phase == EpisodePhase.Eviction && before.evictionResolved && result.state.phase == EpisodePhase.Social && played.Social == null)
                        played.Social = result.state;
                    if (result.state.week == before.week + 1 && played.Reveals.ContainsKey(before.week))
                        played.Turned.Add(before.week, result.state);
                }
                played.Finished = engine.Snapshot;
                Assert.That(played.Finished.phase, Is.EqualTo(EpisodePhase.Finished), "A bounded genuine source season must finish, not skip.");
                Assert.That(played.First, Is.Not.Null); Assert.That(played.Social, Is.Not.Null);
                Games.Add(key, played);
                return played;
            }
        }
        private static PowerRow Power(EpisodeState s) => s.ledger.power.Single(row => row.week == s.week);
        private static UnifiedVoteRevealState Reveal(EpisodeState s) => new UnifiedVoteRevealState
        {
            week = s.week,
            ballots = s.votes.Select(vote => new UnifiedVoteBallotState { voterId = vote.voterId, targetId = vote.targetId }).ToList()
        };
        private static void SourceValid(EpisodeState s)
        {
            Assert.That(EpisodeValidation.TryValidate(s, out var reason), Is.True, reason);
            Assert.That(s.unifiedVoteReveals, Is.Empty, "No source positive installs an archive.");
        }
        private static void Probe(EpisodeState s, UnifiedVoteRevealState reveal, bool accepted)
        {
            string before = Fingerprint(s), revealBefore = Fingerprint(reveal);
            bool result = UnifiedVoteCompletedReveal.TryValidate(s, reveal, out var reason);
            Assert.That(result, Is.EqualTo(accepted), reason);
            if (accepted) Assert.That(reason, Is.Null);
            else Assert.That(reason, Is.Not.Null.And.Not.Empty);
            Assert.That(Fingerprint(s), Is.EqualTo(before), "All source input fields, including private knowledge, must remain unchanged.");
            Assert.That(Fingerprint(reveal), Is.EqualTo(revealBefore), "Do not sort, repair or normalize actual evidence.");
        }
        private static void RewriteExactId(object value, string before, string after)
        {
            if (value == null || value is string || value.GetType().IsValueType) return;
            if (value is IList list)
            {
                for (int index = 0; index < list.Count; index++)
                {
                    if (list[index] is string id) { if (id == before) list[index] = after; }
                    else RewriteExactId(list[index], before, after);
                }
                return;
            }
            foreach (var field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object entry = field.GetValue(value);
                if (entry is string id) { if (id == before) field.SetValue(value, after); }
                else RewriteExactId(entry, before, after);
            }
        }
        // BCL-only structural fingerprint: every public instance field, including nulls and list
        // order. This is a test value comparison, not save serialization or a portable JSON claim.
        private static string Fingerprint(object value)
        {
            var text = new StringBuilder();
            Append(value, text);
            return text.ToString();
        }
        private static void Append(object value, StringBuilder text)
        {
            if (value == null) { text.Append("null;"); return; }
            Type type = value.GetType();
            text.Append(type.FullName).Append(':');
            if (value is string word) { text.Append(word.Length).Append(':').Append(word).Append(';'); return; }
            if (type.IsValueType)
            {
                text.Append(value is IFormattable formattable
                    ? formattable.ToString(value is double || value is float ? "R" : null, CultureInfo.InvariantCulture)
                    : value.ToString()).Append(';');
                return;
            }
            if (value is IEnumerable sequence)
            {
                text.Append('['); foreach (var item in sequence) Append(item, text); text.Append("];"); return;
            }
            text.Append('{');
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.Name, StringComparer.Ordinal))
            { text.Append(field.Name).Append('='); Append(field.GetValue(value), text); }
            text.Append("};");
        }
    }
}
