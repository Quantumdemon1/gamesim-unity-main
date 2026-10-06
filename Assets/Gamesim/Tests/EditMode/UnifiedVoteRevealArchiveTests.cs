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
    /// Separate prospective archive evidence from real public modes0/1. No archive is installed
    /// in an engine/save. Detached removal controls do not claim a public production-removal
    /// witness: all three bounded searches remained incomplete on that separate runtime gate.
    /// </summary>
    public sealed class UnifiedVoteRevealArchiveTests
    {
        private sealed class Point
        {
            internal EpisodeState State;
            internal List<UnifiedVoteRevealState> Frames;
            internal Point Copy() => new Point { State = State.Clone(), Frames = Frames.Select(frame => frame.Clone()).ToList() };
        }
        private sealed class Played
        {
            internal Point Opening, First, Social, Turned, FinalSetup, FinalChoice, FinalSpeeches, Jury, Finished;
            internal readonly Dictionary<int, Point> Regular = new Dictionary<int, Point>();
            internal readonly Dictionary<string, Point> Pending = new Dictionary<string, Point>(StringComparer.Ordinal);
        }
        private static readonly Dictionary<string, Played> Games = new Dictionary<string, Played>(StringComparer.Ordinal);
        private static readonly object Gate = new object();

        [TestCase(0, 3)] [TestCase(0, 4)] [TestCase(0, 5)] [TestCase(0, 8)] [TestCase(0, 12)]
        [TestCase(1, 3)] [TestCase(1, 4)] [TestCase(1, 5)] [TestCase(1, 8)] [TestCase(1, 12)]
        public void ActualFreshOpeningAcceptsAnEmptyHistoryButCannotProject(int mode, int size)
        {
            var point = Game(mode, size).Opening.Copy();
            Assert.That(point.State.contestants.Count, Is.EqualTo(size));
            Assert.That(point.State.week, Is.EqualTo(1));
            Assert.That(point.State.ledger.power, Is.Empty);
            Assert.That(point.Frames, Is.Empty);
            SourceValid(point.State);
            Validate(point, true); Project(point, false);
        }

        [TestCase(0, 4)] [TestCase(0, 5)] [TestCase(0, 8)] [TestCase(0, 12)]
        [TestCase(1, 4)] [TestCase(1, 5)] [TestCase(1, 8)] [TestCase(1, 12)]
        public void ActualRegularRevealProjectsTheIndependentActualPrivateBox(int mode, int size)
        {
            var point = Game(mode, size).First.Copy();
            Assert.That(point.State.contestants.Count, Is.EqualTo(size));
            Assert.That(point.State.phase, Is.EqualTo(EpisodePhase.Eviction));
            Assert.That(point.State.evictionStage, Is.EqualTo(EvictionStage.Results));
            SourceValid(point.State); Validate(point, true);
            var prior = new Point { State = point.State, Frames = new List<UnifiedVoteRevealState>() };
            var projected = Project(prior, true);
            Assert.That(Fingerprint(projected), Is.EqualTo(Fingerprint(point.Frames)),
                "Expected evidence is independently copied from public returned ballots, not built by the archive API.");
        }

        [TestCase(0, 4)] [TestCase(0, 8)] [TestCase(0, 12)]
        [TestCase(1, 4)] [TestCase(1, 8)] [TestCase(1, 12)]
        public void ActualPostRevealSocialKeepsTheSameRegularFrame(int mode, int size)
        {
            var point = Game(mode, size).Social.Copy();
            Assert.That(point.State.phase, Is.EqualTo(EpisodePhase.Social));
            Assert.That(point.State.evictionStage, Is.EqualTo(EvictionStage.Interaction));
            Assert.That(point.State.evictionResolved, Is.True);
            SourceValid(point.State); Validate(point, true);
            Assert.That(Fingerprint(Project(point, true)), Is.EqualTo(Fingerprint(point.Frames)));
        }

        [TestCase(0, 3)] [TestCase(0, 4)] [TestCase(0, 5)] [TestCase(0, 8)] [TestCase(0, 12)]
        [TestCase(1, 3)] [TestCase(1, 4)] [TestCase(1, 5)] [TestCase(1, 8)] [TestCase(1, 12)]
        public void ActualWholeSeasonRetainsEveryRegularWeekButNotTheFinalSelection(int mode, int size)
        {
            var point = Game(mode, size).Finished.Copy();
            Assert.That(point.State.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(point.State.contestants.Count, Is.EqualTo(size));
            Assert.That(point.Frames.Count, Is.EqualTo(size - 3), "This public walker has no production removals.");
            Assert.That(point.Frames.Select(frame => frame.week), Is.EqualTo(Enumerable.Range(1, size - 3)));
            var final = point.State.ledger.power.Single(row => row.tally.Count == 0 && row.evicteeId != null);
            Assert.That(final.week, Is.EqualTo(point.State.week));
            Assert.That(final.nominees, Does.Not.Contain(final.hohId));
            Assert.That(final.nominees, Does.Contain(final.evicteeId));
            Assert.That(point.Frames.Any(frame => frame.week == final.week), Is.False);
            SourceValid(point.State); Validate(point, true); Project(point, false);
        }

        [TestCase(0, 3, "setup")] [TestCase(0, 3, "choice")] [TestCase(0, 3, "speeches")] [TestCase(0, 3, "jury")]
        [TestCase(1, 3, "setup")] [TestCase(1, 3, "choice")] [TestCase(1, 3, "speeches")] [TestCase(1, 3, "jury")]
        [TestCase(0, 8, "setup")] [TestCase(0, 8, "choice")] [TestCase(0, 8, "speeches")] [TestCase(0, 8, "jury")]
        [TestCase(1, 8, "setup")] [TestCase(1, 8, "choice")] [TestCase(1, 8, "speeches")] [TestCase(1, 8, "jury")]
        public void ActualFinalStagesValidateOnlyTheirEarlierRegularHistory(int mode, int size, string boundary)
        {
            var played = Game(mode, size);
            var point = (boundary == "setup" ? played.FinalSetup : boundary == "choice" ? played.FinalChoice
                : boundary == "speeches" ? played.FinalSpeeches : played.Jury).Copy();
            Assert.That(point.Frames.Count, Is.EqualTo(size - 3));
            Assert.That(point.Frames.All(frame => frame.week < point.State.week), Is.True);
            SourceValid(point.State); Validate(point, true); Project(point, false);
        }

        [TestCase(0)] [TestCase(1)]
        public void ActualWeekTurnClearsOnlyTheCurrentBoxNotTheRequiredHistory(int mode)
        {
            var point = Game(mode, 8).Turned.Copy();
            Assert.That(point.State.week, Is.EqualTo(2));
            Assert.That(point.State.votes, Is.Empty);
            Assert.That(point.State.ledger.power.Any(row => row.week == point.State.week), Is.False);
            Assert.That(point.Frames.Count, Is.EqualTo(1));
            SourceValid(point.State); Validate(point, true); Project(point, false);
        }

        [TestCase(0, 1, "nomination")] [TestCase(0, 1, "veto-unresolved")] [TestCase(0, 1, "veto-decided")]
        [TestCase(0, 1, "campaign")] [TestCase(0, 1, "speeches")] [TestCase(0, 1, "voting")]
        [TestCase(1, 1, "nomination")] [TestCase(1, 1, "veto-unresolved")] [TestCase(1, 1, "veto-decided")]
        [TestCase(1, 1, "campaign")] [TestCase(1, 1, "speeches")] [TestCase(1, 1, "voting")]
        [TestCase(0, 2, "nomination")] [TestCase(0, 2, "veto-unresolved")] [TestCase(0, 2, "veto-decided")]
        [TestCase(0, 2, "campaign")] [TestCase(0, 2, "speeches")] [TestCase(0, 2, "voting")]
        [TestCase(1, 2, "nomination")] [TestCase(1, 2, "veto-unresolved")] [TestCase(1, 2, "veto-decided")]
        [TestCase(1, 2, "campaign")] [TestCase(1, 2, "speeches")] [TestCase(1, 2, "voting")]
        public void ActualPendingStagesKeepPriorHistoryValidButCannotProject(int mode, int week, string boundary)
        {
            var played = Game(mode, 8);
            Assert.That(played.Pending.ContainsKey(week + ":" + boundary), Is.True,
                "A required public boundary must be reached within the fixed walk, not skipped or manufactured.");
            var point = played.Pending[week + ":" + boundary].Copy();
            Assert.That(point.Frames.Count, Is.EqualTo(week - 1));
            Assert.That(point.State.evictionResolved, Is.False);
            SourceValid(point.State); Validate(point, true); Project(point, false);
        }

        [TestCase(0, false)] [TestCase(0, true)] [TestCase(1, false)] [TestCase(1, true)]
        public void ActualTieAndMajorityProjectionRetainTheRealHohBallotWithoutInflatingTally(int mode, bool tied)
        {
            var point = Game(mode, tied ? 5 : 4).First.Copy();
            var row = point.State.ledger.power.Single(power => power.week == point.State.week);
            Assert.That(row.tally[0] == row.tally[1], Is.EqualTo(tied));
            Assert.That(point.State.votes.Count(vote => vote.voterId == row.hohId), Is.EqualTo(tied ? 1 : 0));
            Assert.That(row.tally.Sum(), Is.EqualTo(point.State.votes.Count - (tied ? 1 : 0)));
            SourceValid(point.State); Validate(point, true);
            var projected = Project(point, true);
            Assert.That(projected.Last().ballots.Select(ballot => ballot.voterId),
                Is.EqualTo(point.State.votes.Select(vote => vote.voterId)));
        }

        [TestCase(0, false)] [TestCase(0, true)] [TestCase(1, false)] [TestCase(1, true)]
        public void CurrentOrderIsExactButHistoricalBallotOrderIsNotInventedFromTallies(int mode, bool historical)
        {
            var played = Game(mode, 5);
            var point = (historical ? played.Finished : played.First).Copy();
            SourceValid(point.State); Validate(point, true);
            point.Frames[0].ballots.Reverse();
            Assert.That(point.Frames[0].ballots.Count, Is.GreaterThan(1));
            Assert.That(UnifiedVoteCompletedReveal.TryValidate(point.State, point.Frames[0], out var reason), Is.True, reason);
            Validate(point, historical);
            if (!historical) Project(point, false);
        }

        [TestCase(0, false)] [TestCase(0, true)] [TestCase(1, false)] [TestCase(1, true)]
        public void ProjectionAndRepeatAreDeeplyDetachedWhetherCurrentFrameWasAlreadyPresent(int mode, bool currentPresent)
        {
            var point = Game(mode, 8).Regular[3].Copy();
            SourceValid(point.State); Validate(point, true);
            string expected = Fingerprint(point.Frames);
            if (!currentPresent) point.Frames.RemoveAt(point.Frames.Count - 1);
            string stateBefore = Fingerprint(point.State), priorBefore = Fingerprint(point.Frames);
            var first = Project(point, true);
            Assert.That(Fingerprint(first), Is.EqualTo(expected));
            var secondInput = new Point { State = point.State, Frames = first };
            var second = Project(secondInput, true);
            Assert.That(Fingerprint(second), Is.EqualTo(expected));
            Assert.That(ReferenceEquals(first, second), Is.False);
            for (int index = 0; index < second.Count; index++)
            {
                Assert.That(ReferenceEquals(first[index], second[index]), Is.False);
                Assert.That(ReferenceEquals(first[index].ballots, second[index].ballots), Is.False);
                Assert.That(ReferenceEquals(first[index].ballots[0], second[index].ballots[0]), Is.False);
                if (index < point.Frames.Count)
                {
                    Assert.That(ReferenceEquals(point.Frames[index], first[index]), Is.False);
                    Assert.That(ReferenceEquals(point.Frames[index].ballots[0], first[index].ballots[0]), Is.False);
                }
            }
            second[0].ballots[0].targetId = "returned-only"; second[0].ballots.Clear(); second.Clear();
            Assert.That(Fingerprint(first), Is.EqualTo(expected));
            first[0].ballots[0].voterId = "detached-only"; first[0].week = 99; first.Clear();
            Assert.That(Fingerprint(point.State), Is.EqualTo(stateBefore));
            Assert.That(Fingerprint(point.Frames), Is.EqualTo(priorBefore));
            Assert.That(point.State.unifiedVoteReveals, Is.Empty);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)] [TestCase(12)] [TestCase(13)]
        public void ArchiveNullBoundsDuplicatesOrderAndMissingCoverageRefuseWithoutRepair(int defect)
        {
            var point = Game(0, 8).Regular[3].Copy();
            SourceValid(point.State); Validate(point, true);
            switch (defect)
            {
                case 0: point.Frames = null; break;
                case 1: point.Frames[0] = null; break;
                case 2: point.Frames[0].week = 0; break;
                case 3: point.Frames.Last().week = point.State.week + 1; break;
                case 4: point.Frames.Insert(1, point.Frames[0].Clone()); break;
                case 5: point.Frames.Reverse(); break;
                case 6: point.Frames.RemoveAt(0); break;
                case 7: point.Frames.RemoveAt(1); break;
                case 8: point.Frames.RemoveAt(2); break;
                case 9: point.Frames.Clear(); break;
                case 10: point.Frames = Enumerable.Range(0, 101).Select(_ => point.Frames[0].Clone()).ToList(); break;
                case 11: point.Frames[0].ballots = null; break;
                case 12: point.Frames[0].ballots.Add(null); break;
                case 13: point.Frames[0].ballots[0].targetId = "unknown"; break;
            }
            Validate(point, false);
            // Omitting only the current frame is the legal prior input for projection, not a
            // complete archive. All other corruption must return no partially repaired result.
            if (defect == 8) Project(point, true); else Project(point, false);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void ErasingAFrameAndItsPowerCannotHideAFirstMiddleOrCurrentReveal(int erased)
        {
            var point = Game(0, 8).Regular[3].Copy();
            SourceValid(point.State); Validate(point, true);
            int week = erased == 0 ? 1 : erased == 1 ? 2 : 3;
            var row = point.State.ledger.power.Single(power => power.week == week);
            if (erased != 3)
            {
                // Deliberately coherent detached status erasure isolates the complete chronology
                // guard, rather than relying only on the global missing-departure refusal.
                point.State.Find(row.evicteeId).status = ContestantStatus.Active;
            }
            point.State.ledger.power.Remove(row);
            point.Frames.RemoveAll(frame => frame.week == week);
            Validate(point, false); Project(point, false);
            UnifiedVoteRevealArchive.TryValidateComplete(point.State, point.Frames, out var reason);
            if (week < point.State.week && erased != 3) Assert.That(reason, Does.Contain("missing a prior regular power owner"));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)]
        [TestCase(16)] [TestCase(17)] [TestCase(18)] [TestCase(19)] [TestCase(20)]
        public void FinalChoiceClassificationCannotExcuseMissingOrHybridRegularOwners(int defect)
        {
            var point = Game(0, 8).Finished.Copy();
            SourceValid(point.State); Validate(point, true);
            var final = point.State.ledger.power.Single(row => row.tally.Count == 0 && row.evicteeId != null);
            var regular = point.State.ledger.power.Single(row => row.week == 1);
            switch (defect)
            {
                case 0: regular.tally.Clear(); break;
                case 1: final.tally.Add(0); break;
                case 2: final.vetoHolderId = final.hohId; break;
                case 3: final.vetoUsed = true; break;
                case 4: final.savedId = final.nominees[0]; break;
                case 5: final.replacementId = final.nominees[1]; break;
                case 6: final.backdoorTargetId = final.nominees[0]; break;
                case 7: final.backdoorResult = "made"; break;
                case 8: final.nominees[final.nominees.IndexOf(final.nominees.First(id => id != final.evicteeId))] = final.hohId; break;
                case 9: final.hohId = final.nominees.First(id => id != final.evicteeId); break;
                case 10: final.week--; break;
                case 11: point.State.phase = EpisodePhase.FinalEviction; break;
                case 12: point.Frames.Add(new UnifiedVoteRevealState { week = final.week }); break;
                case 13:
                    point.State.Find(final.evicteeId).status = ContestantStatus.Active;
                    point.State.ledger.power.Remove(final); break;
                case 14:
                    point.State.Find(regular.evicteeId).status = ContestantStatus.Active;
                    regular.evicteeId = null; regular.nominees.Clear(); regular.tally.Clear();
                    point.Frames.RemoveAt(0); break;
                case 15: point.State.week++; break;
                case 16: point.State.vetoResolved = true; break;
                case 17: point.State.evictionResolved = true; break;
                case 18: point.State.vetoHolderId = final.hohId; break;
                case 19: point.State.nominees.Add(final.nominees[0]); break;
                // Detached source-classification defect: FinalEvict unconditionally sets Jury,
                // although the shared generic departure proof permits an ordinary Evicted row.
                case 20: point.State.Find(final.evicteeId).status = ContestantStatus.Evicted; break;
            }
            Validate(point, false); Project(point, false);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)] [TestCase(16)] [TestCase(17)]
        public void EmptyArchiveStillRequiresWholeContextAndDepartureProof(int defect)
        {
            var point = Game(0, 8).Opening.Copy();
            SourceValid(point.State); Validate(point, true);
            switch (defect)
            {
                case 0: point.State = null; break;
                case 1: point.State.week = 0; break;
                case 2: point.State.week = 101; break;
                case 3: point.State.phase = (EpisodePhase)999; break;
                case 4: point.State.evictionStage = (EvictionStage)999; break;
                case 5: point.State.contestants = null; break;
                case 6: point.State.contestants[0] = null; break;
                case 7: point.State.contestants[0].id = point.State.contestants[1].id; break;
                case 8: point.State.contestants[0].id = "known\tidentity"; break;
                case 9: point.State.contestants[0].status = ContestantStatus.Jury; break;
                case 10: point.State.ledger = null; break;
                case 11: point.State.ledger.power = null; break;
                case 12: point.State.ledger.power.Add(null); break;
                case 13: point.State.story = null; break;
                case 14: point.State.story.removals = null; break;
                case 15: point.State.story.removals.Add(null); break;
                case 16:
                    point.State.story.removals.Add(new RemovalState
                        { contestantId = point.State.playerId, week = 1, reasonId = "conduct" }); break;
                case 17: point.State.week = 2; break;
            }
            Validate(point, false); Project(point, false);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)]
        public void ActualPendingVetoOwnerCannotBeReplacedByMalformedHybrids(int defect)
        {
            var point = Game(0, 8).Pending["2:campaign"].Copy();
            SourceValid(point.State); Validate(point, true);
            var row = point.State.ledger.power.Single(power => power.week == point.State.week);
            switch (defect)
            {
                case 0: point.State.ledger.power.Remove(row); break;
                case 1: row.nominees.Add(point.State.nominees[0]); break;
                case 2: row.tally.Add(0); break;
                case 3: row.evicteeId = ""; break;
                case 4: row.hohId = null; break;
                case 5: row.vetoHolderId = null; break;
                case 6: point.State.vetoResolved = false; break;
                case 7: row.backdoorTargetId = point.State.nominees[0]; break;
                case 8: row.backdoorResult = "made"; break;
                case 9: point.State.nominees.Clear(); break;
            }
            Validate(point, false); Project(point, false);
        }

        [TestCase(0, false)] [TestCase(0, true)] [TestCase(1, false)] [TestCase(1, true)]
        public void DetachedPendingCurrentFramesAndTieStagesDoNotBecomeCompletedEvidence(int mode, bool tieStage)
        {
            var played = Game(mode, 8);
            var point = played.Pending["2:voting"].Copy();
            SourceValid(point.State); Validate(point, true); Project(point, false);
            if (tieStage)
            {
                // Explicit detached stage control, NOT a public player-HoH tie-break witness.
                // The fixed seed1 five-person tie is an NPC HoH decided inside one real command.
                point.State.evictionStage = EvictionStage.Tiebreaker;
                Validate(point, true); Project(point, false);
            }
            point.Frames.Add(played.Regular[2].Frames.Last().Clone());
            Validate(point, false); Project(point, false);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)]
        public void CurrentActualBoxAndCompletedFlagsAreRequiredBeforeAnyProjectionEscapes(int defect)
        {
            var point = Game(0, 5).First.Copy();
            SourceValid(point.State); Validate(point, true);
            switch (defect)
            {
                case 0: point.State.votes = null; break;
                case 1: point.State.votes[0] = null; break;
                case 2: point.State.votes.Add(point.State.votes[0].Clone()); break;
                case 3: point.State.votes.Clear(); break;
                case 4: point.State.votes.Reverse(); break;
                case 5: point.State.evictionResolved = false; break;
                case 6: point.State.vetoResolved = false; break;
                case 7: point.State.evictionStage = EvictionStage.Voting; break;
                case 8: point.State.nominees.Reverse(); break;
                case 9: point.State.ledger.power.Single(row => row.week == 1).tally[0]++; break;
            }
            Validate(point, false); Project(point, false);
        }

        [TestCase(0, false)] [TestCase(0, true)] [TestCase(1, false)] [TestCase(1, true)]
        public void DetachedRemovalChronologyIncludesSameWeekOrdinaryBallotsButNotEarlierRemoval(int mode, bool earlier)
        {
            var played = Game(mode, 8);
            var point = played.Pending["3:nomination"].Copy();
            SourceValid(point.State); Validate(point, true);
            var reveal = point.Frames.Last();
            var power = point.State.ledger.power.Single(row => row.week == reveal.week);
            string voter = reveal.ballots.First(ballot => ballot.voterId != power.hohId
                && point.State.Find(ballot.voterId).status == ContestantStatus.Active).voterId;
            // These are detached local role/date controls, not actual removal commands, whole
            // saves, or a waiver of the still-open genuine public ordinary-removal gate.
            point.State.Find(voter).status = ContestantStatus.Expelled;
            point.State.story.removals.Add(new RemovalState
                { contestantId = voter, week = earlier ? reveal.week - 1 : reveal.week, reasonId = "conduct" });
            Validate(point, !earlier); Project(point, false);
        }

        [TestCase(0)] [TestCase(1)]
        public void HistoricalArchiveOrderAndPrunedUnrelatedEvidenceRemainUnchanged(int mode)
        {
            var point = Game(mode, 8).Finished.Copy();
            SourceValid(point.State); Validate(point, true);
            point.State.ledger.power.Reverse(); point.State.contestants.Reverse();
            foreach (var frame in point.Frames) frame.ballots.Reverse();
            point.State.events.Clear(); point.State.ledger.claims.Clear(); point.State.ledger.ballots.Clear();
            point.State.ledger.dropped = 12;
            Validate(point, true); Project(point, false);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void SharedContextExtractionRetainsExistingInitialRefusalReasons(int defect)
        {
            var point = Game(0, 5).First.Copy(); var reveal = point.Frames[0].Clone();
            string expected;
            switch (defect)
            {
                case 0: reveal = null; expected = "Invalid completed-reveal context or week."; break;
                case 1: point.State.week = 0; expected = "Invalid completed-reveal context or week."; break;
                case 2: point.State.contestants = null; expected = "Invalid completed-reveal cast."; break;
                case 3: point.State.ledger.power = null; expected = "Invalid or ambiguous durable reveal power rows."; break;
                case 4: point.State.story.removals = null; expected = "Invalid durable reveal removals."; break;
                default: point.State.Find(point.State.ledger.power[0].evicteeId).status = ContestantStatus.Active;
                    expected = "Cast status contradicts its durable departure."; break;
            }
            string before = Fingerprint(point.State), frameBefore = Fingerprint(reveal);
            Assert.That(UnifiedVoteCompletedReveal.TryValidate(point.State, reveal, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo(expected));
            Assert.That(Fingerprint(point.State), Is.EqualTo(before));
            Assert.That(Fingerprint(reveal), Is.EqualTo(frameBefore));
        }

        private static Played Game(int mode, int size)
        {
            string key = mode + ":" + size;
            lock (Gate)
            {
                if (Games.TryGetValue(key, out var played)) return played;
                var initial = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size, Roster = CastTemplates.Roster.Regular }, 1);
                Assert.That(initial.contestants.Count, Is.EqualTo(size), "A clamped factory size is not public coverage.");
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
                played = new Played(); var frames = new List<UnifiedVoteRevealState>();
                played.Opening = Capture(engine.Snapshot, frames);
                for (int step = 0; step < 512 && engine.Snapshot.phase != EpisodePhase.Finished; step++)
                {
                    var before = engine.Snapshot;
                    var command = EpisodeEngineTests.NextCommand(before);
                    if (command.kind == EpisodeCommandKind.CastVote && before.phase == EpisodePhase.Eviction)
                        command.targetId = before.nominees[1]; // Exact seed1 actual tie/majority recipe.
                    if (command.kind == EpisodeCommandKind.AnswerJury && EpisodeEngine.FinaleOn(before))
                    {
                        var exchange = before.juryExchanges[before.juryQuestionIndex];
                        if (exchange.finalistId == before.playerId)
                            command.secondTargetId = FinaleQuestions.Offered(exchange.category, exchange.receiptKind)[0];
                    }
                    string commandBefore = Fingerprint(command);
                    var result = engine.Apply(command);
                    Assert.That(result.accepted && !result.duplicate, Is.True,
                        "Public " + command.kind + " at " + before.week + "/" + before.phase + ": " + result.reason);
                    Assert.That(Fingerprint(command), Is.EqualTo(commandBefore));
                    Assert.That(result.state.revision, Is.EqualTo(before.revision + 1));
                    Assert.That(result.state.acceptedCommandIds.Last(), Is.EqualTo(command.id));
                    SourceValid(result.state);
                    if (!before.evictionResolved && result.state.evictionResolved && result.state.phase == EpisodePhase.Eviction)
                    {
                        frames.Add(new UnifiedVoteRevealState
                        {
                            week = result.state.week,
                            ballots = result.state.votes.Select(vote => new UnifiedVoteBallotState
                                { voterId = vote.voterId, targetId = vote.targetId }).ToList()
                        });
                        var point = Capture(result.state, frames);
                        played.Regular.Add(result.state.week, point);
                        if (played.First == null) played.First = point;
                    }
                    if (played.Social == null && before.phase == EpisodePhase.Eviction && before.evictionResolved
                        && result.state.phase == EpisodePhase.Social) played.Social = Capture(result.state, frames);
                    if (played.Turned == null && result.state.week == before.week + 1 && frames.Count > 0)
                        played.Turned = Capture(result.state, frames);
                    if (played.FinalSetup == null && result.state.phase == EpisodePhase.FinalHoHPart1)
                        played.FinalSetup = Capture(result.state, frames);
                    if (played.FinalChoice == null && result.state.phase == EpisodePhase.JuryQuestioning)
                        played.FinalChoice = Capture(result.state, frames);
                    if (played.FinalSpeeches == null && result.state.phase == EpisodePhase.FinalSpeeches)
                        played.FinalSpeeches = Capture(result.state, frames);
                    if (played.Jury == null && result.state.phase == EpisodePhase.Jury)
                        played.Jury = Capture(result.state, frames);
                    string label = PendingLabel(result.state);
                    string boundary = result.state.week + ":" + label;
                    if (label != null && !played.Pending.ContainsKey(boundary))
                        played.Pending.Add(boundary, Capture(result.state, frames));
                }
                played.Finished = Capture(engine.Snapshot, frames);
                Assert.That(played.Finished.State.phase, Is.EqualTo(EpisodePhase.Finished), "Fixed public season must finish within512 accepted steps.");
                Assert.That(played.FinalSetup, Is.Not.Null); Assert.That(played.FinalChoice, Is.Not.Null);
                Assert.That(played.FinalSpeeches, Is.Not.Null); Assert.That(played.Jury, Is.Not.Null);
                if (size > 3) { Assert.That(played.First, Is.Not.Null); Assert.That(played.Social, Is.Not.Null); }
                Games.Add(key, played);
                return played;
            }
        }
        private static string PendingLabel(EpisodeState state)
        {
            if (state.phase == EpisodePhase.Nomination && state.nominees.Count == 0) return "nomination";
            if (state.phase == EpisodePhase.VetoMeeting) return state.vetoResolved ? "veto-decided" : "veto-unresolved";
            if (state.phase == EpisodePhase.Campaign) return "campaign";
            if (state.phase == EpisodePhase.Eviction && !state.evictionResolved)
                return state.evictionStage == EvictionStage.Speeches ? "speeches"
                    : state.evictionStage == EvictionStage.Voting ? "voting"
                    : state.evictionStage == EvictionStage.Tiebreaker ? "tie" : null;
            return null;
        }
        private static Point Capture(EpisodeState state, List<UnifiedVoteRevealState> frames)
            => new Point { State = state.Clone(), Frames = frames.Select(frame => frame.Clone()).ToList() };
        private static void SourceValid(EpisodeState state)
        {
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            Assert.That(state.unifiedVoteReveals, Is.Empty, "No public source positive installs archive evidence.");
        }
        private static void Validate(Point point, bool accepted)
        {
            string stateBefore = Fingerprint(point.State), framesBefore = Fingerprint(point.Frames);
            bool result = UnifiedVoteRevealArchive.TryValidateComplete(point.State, point.Frames, out var reason);
            Assert.That(result, Is.EqualTo(accepted), reason);
            if (accepted) Assert.That(reason, Is.Null); else Assert.That(reason, Is.Not.Null.And.Not.Empty);
            Assert.That(Fingerprint(point.State), Is.EqualTo(stateBefore), "Every input state field and private knowledge must remain unchanged.");
            Assert.That(Fingerprint(point.Frames), Is.EqualTo(framesBefore), "No sorting, missing-history repair or normalization.");
        }
        private static List<UnifiedVoteRevealState> Project(Point point, bool accepted)
        {
            string stateBefore = Fingerprint(point.State), framesBefore = Fingerprint(point.Frames);
            bool result = UnifiedVoteRevealArchive.TryProjectCurrent(point.State, point.Frames, out var projected, out var reason);
            Assert.That(result, Is.EqualTo(accepted), reason);
            if (accepted)
            {
                Assert.That(reason, Is.Null); Assert.That(projected, Is.Not.Null);
                Assert.That(UnifiedVoteRevealArchive.TryValidateComplete(point.State, projected, out reason), Is.True, reason);
            }
            else { Assert.That(reason, Is.Not.Null.And.Not.Empty); Assert.That(projected, Is.Null, "No partial output escapes refusal."); }
            Assert.That(Fingerprint(point.State), Is.EqualTo(stateBefore));
            Assert.That(Fingerprint(point.Frames), Is.EqualTo(framesBefore));
            return projected;
        }
        // BCL structural comparison of all public instance fields, including null/list order;
        // not a save serializer, portable corpus, private-knowledge grant or shipping proof.
        private static string Fingerprint(object value)
        {
            var text = new StringBuilder(); Append(value, text); return text.ToString();
        }
        private static void Append(object value, StringBuilder text)
        {
            if (value == null) { text.Append("null;"); return; }
            Type type = value.GetType(); text.Append(type.FullName).Append(':');
            if (value is string word) { text.Append(word.Length).Append(':').Append(word).Append(';'); return; }
            if (type.IsValueType)
            {
                text.Append(value is IFormattable formattable
                    ? formattable.ToString(value is double || value is float ? "R" : null, CultureInfo.InvariantCulture)
                    : value.ToString()).Append(';'); return;
            }
            if (value is IEnumerable sequence)
            { text.Append('['); foreach (var item in sequence) Append(item, text); text.Append("];"); return; }
            text.Append('{');
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.Name, StringComparer.Ordinal))
            { text.Append(field.Name).Append('='); Append(field.GetValue(value), text); }
            text.Append("};");
        }
    }
}
