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
    /// Inactive local predicates plus real public mode0/1 producer/reveal witnesses. Canonical
    /// Vote rows and complete archives are detached arguments, never installed or save-accepted.
    /// Story-null/term/role projections are explicitly local diagnostics, not producer history.
    /// No original-web targeted settlement, applied effects, Unity/disk or mode2 claim is made.
    /// </summary>
    public sealed class UnifiedVoteObligationsTests
    {
        private const string Promise = "promise";
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Point> Witnesses = new Dictionary<string, Point>();

        [TestCase(false, false, false)]
        [TestCase(false, false, true)]
        [TestCase(false, true, false)]
        [TestCase(false, true, true)]
        [TestCase(true, false, false)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        [TestCase(true, true, true)]
        public void NamedPromiseUsesOnlyItsActualMaker(bool makerVotes, bool kept, bool reverse)
        {
            var ballots = Ballots(Ballot("B", kept ? "D" : "C"), Ballot("outsider", "D"));
            if (makerVotes) ballots.Add(Ballot("A", kept ? "C" : "D"));
            if (reverse) ballots.Reverse();
            Probe(Row(Promise), ballots, Block(), true,
                makerVotes ? kept ? DealStatus.Fulfilled : DealStatus.Broken : null, makerVotes ? "A" : null);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void OtherPartyOrAnEmptyBoxCannotInventTheMakersBallot(int who)
            => Probe(Row(Promise), who == 0 ? Ballots() : Ballots(Ballot(who == 1 ? "B" : "outsider", "C")),
                Block(), true, null, null);

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void GenuineStoryNullShapeRetainsTheDocumentedNativeNotOriginalPredicate(bool makerVotes, bool reverse)
        {
            var row = Row(Promise); row.origin = UnifiedCommitments.StoryPromise; row.targetId = null;
            var ballots = Ballots(Ballot("B", "C"));
            if (makerVotes) ballots.Add(Ballot("A", "D"));
            if (reverse) ballots.Reverse();
            // This is a source-shape diagnostic, not proof StoryPromise ran here. Original TS
            // unspecified voteTarget/preference stays pending; native null vs actual target breaks.
            Probe(row, ballots, Block(), true, makerVotes ? DealStatus.Broken : null, makerVotes ? "A" : null);
        }

        [TestCase(DealKind.VoteSave, 0)]
        [TestCase(DealKind.VoteSave, 1)]
        [TestCase(DealKind.VoteSave, 2)]
        [TestCase(DealKind.VoteSave, 3)]
        [TestCase(DealKind.VoteSave, 4)]
        [TestCase(DealKind.VoteSave, 5)]
        [TestCase(DealKind.VoteSave, 6)]
        [TestCase(DealKind.VoteSave, 7)]
        [TestCase(DealKind.VoteSave, 8)]
        [TestCase(DealKind.VoteEvict, 0)]
        [TestCase(DealKind.VoteEvict, 1)]
        [TestCase(DealKind.VoteEvict, 2)]
        [TestCase(DealKind.VoteEvict, 3)]
        [TestCase(DealKind.VoteEvict, 4)]
        [TestCase(DealKind.VoteEvict, 5)]
        [TestCase(DealKind.VoteEvict, 6)]
        [TestCase(DealKind.VoteEvict, 7)]
        [TestCase(DealKind.VoteEvict, 8)]
        public void TargetedNativeC0AttributionFollowsPartiesNotBallotInsertion(string type, int pattern)
        {
            foreach (bool reverseParties in new[] { false, true })
            foreach (bool reverseBallots in new[] { false, true })
            {
                var row = Row(type);
                if (reverseParties) { row.makerId = "B"; row.beneficiaryId = "A"; }
                string keep = type == DealKind.VoteSave ? "D" : "C", fail = keep == "C" ? "D" : "C";
                var ballots = Ballots();
                if (pattern == 1 || pattern == 5 || pattern == 6) ballots.Add(Ballot(row.makerId, keep));
                if (pattern == 2 || pattern == 7 || pattern == 8) ballots.Add(Ballot(row.makerId, fail));
                if (pattern == 3 || pattern == 5 || pattern == 7) ballots.Add(Ballot(row.beneficiaryId, keep));
                if (pattern == 4 || pattern == 6 || pattern == 8) ballots.Add(Ballot(row.beneficiaryId, fail));
                if (reverseBallots) ballots.Reverse();
                string status = pattern == 0 ? null : pattern == 2 || pattern == 4 || pattern == 6 || pattern == 7 || pattern == 8
                    ? DealStatus.Broken : DealStatus.Fulfilled;
                string actor = pattern == 0 || pattern == 8 ? null
                    : pattern == 3 || pattern == 4 || pattern == 6 ? row.beneficiaryId : row.makerId;
                Probe(row, ballots, Block(), true, status, actor);
            }
        }

        [TestCase(DealKind.VoteSave, 0)]
        [TestCase(DealKind.VoteSave, 1)]
        [TestCase(DealKind.VoteSave, 2)]
        [TestCase(DealKind.VoteSave, 3)]
        [TestCase(DealKind.VoteEvict, 0)]
        [TestCase(DealKind.VoteEvict, 1)]
        [TestCase(DealKind.VoteEvict, 2)]
        [TestCase(DealKind.VoteEvict, 3)]
        public void NamedNomineePayerOrBeneficiaryIsNotInventedAsAVoter(string type, int role)
        {
            var row = Row(type);
            var block = role == 0 ? Block("A", "D") : role == 1 ? Block("B", "D") : role == 2 ? Block("A", "B") : Block();
            row.targetId = block[0];
            string keepingTarget = type == DealKind.VoteSave ? block[1] : block[0];
            var ballots = Ballots();
            if (role != 0 && role != 2) ballots.Add(Ballot("A", keepingTarget));
            if (role != 1 && role != 2) ballots.Add(Ballot("B", keepingTarget));
            Probe(row, ballots, block, true, role == 2 ? null : DealStatus.Fulfilled,
                role == 2 ? null : role == 0 ? "B" : "A");
        }

        [TestCase(Promise, DealStatus.Fulfilled)]
        [TestCase(Promise, DealStatus.Broken)]
        [TestCase(Promise, DealStatus.Expired)]
        [TestCase(DealKind.VoteSave, DealStatus.Proposed)]
        [TestCase(DealKind.VoteSave, DealStatus.Accepted)]
        [TestCase(DealKind.VoteSave, DealStatus.Declined)]
        [TestCase(DealKind.VoteSave, DealStatus.Expired)]
        [TestCase(DealKind.VoteSave, DealStatus.Fulfilled)]
        [TestCase(DealKind.VoteSave, DealStatus.Broken)]
        public void EveryValidInactiveRowStillValidatesTheWholeBox(string family, string status)
        {
            var row = Row(family); row.status = status;
            Probe(row, Ballots(Ballot("A", "C"), Ballot("B", "D")), Block(), true, null, null);
            Probe(row, Ballots(Ballot("A", "C"), Ballot("outsider", "unknown")), Block(), false, null, null);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)] [TestCase(16)] [TestCase(17)]
        [TestCase(18)] [TestCase(19)] [TestCase(20)] [TestCase(21)]
        public void InvalidFamilyRowAndNarrowStoryNullShapesRefuseWithoutRepair(int defect)
        {
            var row = Row(defect >= 12 ? Promise : DealKind.VoteSave);
            switch (defect)
            {
                case 0: row = null; break;
                case 1: row.kind = UnifiedCommitments.Safety; break;
                case 2: row.id = " "; break;
                case 3: row.id = new string('x', 161); break;
                case 4: row.makerId = "A\n"; break;
                case 5: row.beneficiaryId = row.makerId; break;
                case 6: row.sourcePolicy = "future"; break;
                case 7: row.reciprocal = false; break;
                case 8: row.subtype = DealKind.VoteTogether; break;
                case 9: row.targetId = null; break;
                case 10: row.targetId = ""; break;
                case 11: row.status = "future"; break;
                case 12: row.reciprocal = true; break;
                case 13: row.subtype = ""; break;
                case 14: row.targetId = null; break;
                case 15: row.targetId = ""; row.origin = UnifiedCommitments.StoryPromise; break;
                case 16: row.targetId = " "; row.origin = UnifiedCommitments.StoryPromise; break;
                case 17: row.targetId = null; row.origin = UnifiedCommitments.NpcPromise; break;
                case 18: row.status = DealStatus.Proposed; break;
                case 19: row.status = DealStatus.Accepted; break;
                case 20: row.status = DealStatus.Declined; break;
                case 21: row.targetId = "C\0"; break;
            }
            Probe(row, Ballots(Ballot("A", "C")), Block(), false, null, null);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void LocalFinalBlockIsTwoDistinctValidNames(int defect)
        {
            var block = Block();
            switch (defect)
            {
                case 0: block = null; break;
                case 1: block.Clear(); break;
                case 2: block.RemoveAt(1); break;
                case 3: block.Add("E"); break;
                case 4: block[1] = block[0]; break;
                case 5: block[0] = " "; break;
                case 6: block[1] = "D\n"; break;
            }
            Probe(Row(Promise), Ballots(), block, false, null, null);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
        public void EntireActualBoxIsBoundedUniqueAndLocallyEligible(int defect)
        {
            var ballots = Ballots(Ballot("A", "C"), Ballot("B", "D"));
            switch (defect)
            {
                case 0: ballots = null; break;
                case 1: ballots.Add(null); break;
                case 2: ballots.Add(Ballot("A", "C")); break;
                case 3: ballots.Add(Ballot("A", "D")); break;
                case 4: ballots.Add(Ballot("C", "D")); break;
                case 5: ballots.Add(Ballot("outsider", "E")); break;
                case 6: ballots.Add(Ballot(" ", "C")); break;
                case 7: ballots.Add(Ballot("outsider", null)); break;
                case 8: ballots.Add(Ballot("outsider\r", "C")); break;
                case 9: ballots.Add(Ballot(new string('v', 161), "C")); break;
                case 10: while (ballots.Count <= 16) ballots.Add(Ballot("extra-" + ballots.Count, "C")); break;
            }
            Probe(Row(Promise), ballots, Block(), false, null, null);
        }

        [TestCase(Promise)] [TestCase(DealKind.VoteSave)] [TestCase(DealKind.VoteEvict)]
        public void ExactLocalCapacityDoesNotPretendItProvesARealSixteenPersonSeason(string family)
        {
            var ballots = Ballots(Ballot("A", "C"));
            for (int index = 1; index < 16; index++) ballots.Add(Ballot("extra-" + index, "C"));
            Probe(Row(family), ballots, Block(), true,
                family == DealKind.VoteSave ? DealStatus.Broken : DealStatus.Fulfilled, "A");
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void LocalExpiryOpenAndLateMetadataNeverInventsAnExpiryOrAdmissionOwner(int term)
        {
            var row = Row(DealKind.VoteSave);
            row.createdWeek = 2; row.expiresWeek = term == 0 ? 0 : term == 1 ? 2 : term == 2 ? 1 : 5;
            row.linkedCommitmentId = "actual-shape-only-price-link";
            row.settledWeek = 73; row.brokenById = "unused-saved-field"; row.settlementEffectKey = "unused-key";
            // Deliberately not full row/term/link validation; these untouched fields prove the
            // local predicate does not secretly expire, normalize or accept a saved record.
            Probe(row, Ballots(Ballot("A", "D")), Block(), true, DealStatus.Fulfilled, "A");
        }

        [TestCase(Promise)] [TestCase(DealKind.VoteSave)] [TestCase(DealKind.VoteEvict)]
        public void OffBlockNamedTargetsFollowTheirDifferentActualSourcePredicates(string family)
        {
            var row = Row(family); row.targetId = "E";
            Probe(row, Ballots(Ballot("A", "C")), Block(), true,
                family == Promise ? DealStatus.Broken : null, family == Promise ? "A" : null);
        }

        [Test]
        public void LocalRefusalPrecedenceAndImmutableResultHaveNoStoredOrMutableSurface()
        {
            var row = Row(Promise); row.id = "";
            Assert.That(UnifiedVoteObligations.TryVerdict(row, null, null, out var value, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("A Vote obligation requires valid distinct parties and a row identity."));
            row = Row(Promise);
            Assert.That(UnifiedVoteObligations.TryVerdict(row, null, null, out value, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("A Vote obligation requires the two distinct final nominees."));
            row.status = DealStatus.Fulfilled;
            Assert.That(UnifiedVoteObligations.TryVerdict(row, null, Block(), out value, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("An actual Vote snapshot must be nonnull and within the house bound."));
            var fields = typeof(UnifiedVoteObligationVerdict).GetFields(BindingFlags.Public | BindingFlags.Instance);
            Assert.That(fields.Select(field => field.Name).OrderBy(name => name), Is.EqualTo(new[] { "ActorId", "Status" }));
            Assert.That(fields.All(field => field.IsInitOnly && field.FieldType == typeof(string)), Is.True);
            Assert.That(typeof(UnifiedVoteObligationVerdict).IsSerializable, Is.False);
        }

        [Test]
        public void StructuralFingerprintRecursesActualDictionaryObjectsListsKeysAndOrder()
        {
            // A BCL test-proof control, not game creation or a new product authority seam.
            var promise = new PromiseState { id = "fingerprint-only", targetId = "first", status = PromiseStatus.Active };
            var original = new Dictionary<string, object>
            {
                { "object", promise }, { "list", new List<string> { "first", "second" } },
            };
            var clone = new Dictionary<string, object>
            {
                { "object", promise.Clone() }, { "list", new List<string>((List<string>)original["list"]) },
            };
            string baseline = Fingerprint(original);
            Assert.That(Fingerprint(clone), Is.EqualTo(baseline), "Unchanged detached values have equal full fingerprints.");
            ((PromiseState)clone["object"]).targetId = "changed";
            Assert.That(Fingerprint(clone), Is.Not.EqualTo(baseline), "Dictionary object Value must be recursively compared.");
            ((PromiseState)clone["object"]).targetId = promise.targetId;
            Assert.That(Fingerprint(clone), Is.EqualTo(baseline));
            ((List<string>)clone["list"])[0] = "changed";
            Assert.That(Fingerprint(clone), Is.Not.EqualTo(baseline), "Dictionary list Value must be recursively compared.");
            ((List<string>)clone["list"])[0] = "first";
            ((List<string>)clone["list"]).Reverse();
            Assert.That(Fingerprint(clone), Is.Not.EqualTo(baseline), "List insertion order is part of the proof.");
            ((List<string>)clone["list"]).Reverse();
            Assert.That(Fingerprint(clone), Is.EqualTo(baseline));
            clone.Remove("list"); clone.Add("different-key", new List<string> { "first", "second" });
            Assert.That(Fingerprint(clone), Is.Not.EqualTo(baseline), "Dictionary Key is part of the proof.");
            Assert.That(Fingerprint(original), Is.EqualTo(baseline), "Detached diagnostic mutations preserve the source object.");
        }

        [TestCase(0, Promise, false)] [TestCase(0, Promise, true)]
        [TestCase(1, Promise, false)] [TestCase(1, Promise, true)]
        [TestCase(0, DealKind.VoteSave, false)] [TestCase(0, DealKind.VoteSave, true)]
        [TestCase(1, DealKind.VoteSave, false)] [TestCase(1, DealKind.VoteSave, true)]
        [TestCase(0, DealKind.VoteEvict, false)] [TestCase(0, DealKind.VoteEvict, true)]
        [TestCase(1, DealKind.VoteEvict, false)] [TestCase(1, DealKind.VoteEvict, true)]
        public void ActualPublicPredecisionProducerAndCompletedRevealAgreeWithoutInstallingCanonicalVote(int mode, string family, bool kept)
        {
            var point = Witness(mode, 8, family, kept);
            Assert.That(point.Row.status, Is.EqualTo(DealStatus.Active), "Retained predecision row, not a reopened terminal record.");
            Assert.That(point.CreatedCommand, Is.Not.Null);
            Assert.That(point.CreatedCommand.kind, Is.EqualTo(family == Promise ? EpisodeCommandKind.PromiseVote : EpisodeCommandKind.ProposeDeal));
            Assert.That(point.ActualStatus, Is.EqualTo(kept ? DealStatus.Fulfilled : DealStatus.Broken));
            Assert.That(point.Row.id, Is.EqualTo(family == Promise ? point.RawPromise.id : point.RawDeal.id));
            Assert.That(point.Row.createdWeek, Is.EqualTo(family == Promise ? point.RawPromise.week : point.RawDeal.week));
            Assert.That(point.Row.expiresWeek, Is.EqualTo(family == Promise ? point.RawPromise.expiresWeek : point.RawDeal.expiresWeek));
            ArchiveProbe(point, point.Row, point.State.week, true, point.ActualStatus, point.ActualActor);
            Assert.That(point.State.unifiedCommitmentRulesVersion, Is.EqualTo(mode));
            Assert.That(point.State.unifiedHearingRulesVersion, Is.EqualTo(mode));
            Assert.That(point.State.unifiedCommitments.All(row => row.kind == UnifiedCommitments.Safety), Is.True);
        }

        [TestCase(0, 4)] [TestCase(0, 5)] [TestCase(1, 4)] [TestCase(1, 5)]
        public void ActualRegularFrameIncludesHohOnlyForItsGenuineTieBreak(int mode, int size)
        {
            var point = Witness(mode, size, null, true);
            var power = point.State.ledger.power.Single(row => row.week == point.State.week);
            var ballot = point.Frames.Last().ballots.FirstOrDefault(vote => vote.voterId == power.hohId);
            Assert.That(ballot != null, Is.EqualTo(size == 5), "Fixed seed1 recipe must prove the actual expected tie, not inject a ballot.");
            // Detached named-promise shape on actual source roles, not a claim its producer ran.
            var row = Row(Promise); row.makerId = power.hohId; row.beneficiaryId = power.nominees[0]; row.targetId = power.nominees[0];
            ArchiveProbe(point, row, power.week, true, ballot == null ? null
                : ballot.targetId == row.targetId ? DealStatus.Fulfilled : DealStatus.Broken, ballot == null ? null : row.makerId);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)] [TestCase(16)] [TestCase(17)]
        [TestCase(18)] [TestCase(19)]
        public void FullArchiveAndCastQualificationCannotBeBypassedByAnInactiveOrOtherwiseValidRow(int defect)
        {
            var point = Witness(1, 8, DealKind.VoteSave, true).Copy();
            ArchiveProbe(point, point.Row, point.State.week, true, point.ActualStatus, point.ActualActor);
            int requested = point.State.week;
            var frame = point.Frames.Last();
            var power = point.State.ledger.power.Single(row => row.week == frame.week);
            switch (defect)
            {
                case 0: point.Frames = null; break;
                case 1: point.Frames[0] = null; break;
                case 2: point.Frames.Add(frame.Clone()); break;
                case 3: point.Frames.RemoveAt(0); break;
                case 4: point.Frames.Remove(frame); point.State.ledger.power.Remove(power); break;
                case 5: frame.ballots = null; break;
                case 6: frame.ballots.Add(frame.ballots[0].Clone()); break;
                case 7: frame.ballots[0].voterId = "unknown"; break;
                case 8: frame.ballots[0].targetId = "unknown"; break;
                case 9: power.tally[0]++; break;
                case 10: power.evicteeId = null; break;
                case 11: point.State.votes.Reverse(); break;
                case 12: requested = point.State.week + 1; break;
                case 13: requested = 0; break;
                case 14: point.State = null; break;
                case 15: point.Row.status = DealStatus.Fulfilled; point.Frames = null; break;
                case 16: point.Row.makerId = "unknown"; break;
                case 17: point.Row.targetId = "unknown"; break;
                case 18: point.Row = null; break;
                case 19: point.State.phase = EpisodePhase.Eviction; point.State.evictionStage = EvictionStage.Voting; point.State.evictionResolved = false; break;
            }
            ArchiveProbe(point, point.Row, requested, false, null, null);
        }

        [TestCase(0, Promise)] [TestCase(1, Promise)]
        [TestCase(0, DealKind.VoteSave)] [TestCase(1, DealKind.VoteSave)]
        [TestCase(0, DealKind.VoteEvict)] [TestCase(1, DealKind.VoteEvict)]
        public void ActualWeekTurnRetainsHistoricalPrivateVerdictButDoesNotInstallHistoryOrGrantKnowledge(int mode, string family)
        {
            var original = Witness(mode, 8, family, true);
            var later = Later(original);
            Assert.That(later.State.week, Is.GreaterThan(original.State.week));
            Assert.That(later.State.votes, Is.Empty, "Real week-turn cleared the private box.");
            Assert.That(later.State.nominees, Is.Empty, "Historical block must come from its actual PowerRow.");
            ArchiveProbe(later, original.Row, original.State.week, true, original.ActualStatus, original.ActualActor);
            later.State.events.Clear();
            ArchiveProbe(later, original.Row, original.State.week, true, original.ActualStatus, original.ActualActor);
        }

        private sealed class Point
        {
            internal EpisodeState State;
            internal List<UnifiedVoteRevealState> Frames;
            internal UnifiedCommitmentState Row;
            internal PromiseState RawPromise;
            internal DealState RawDeal;
            internal EpisodeCommand CreatedCommand;
            internal string ActualStatus, ActualActor;
            internal Point Copy() => new Point
            {
                State = State?.Clone(), Frames = Frames?.Select(frame => frame?.Clone()).ToList(), Row = Row?.Clone(),
                RawPromise = RawPromise?.Clone(), RawDeal = RawDeal?.Clone(), CreatedCommand = CopyCommand(CreatedCommand),
                ActualStatus = ActualStatus, ActualActor = ActualActor,
            };
        }

        private static Point Witness(int mode, int size, string family, bool kept)
        {
            string key = mode + ":" + size + ":" + family + ":" + kept;
            lock (Gate)
            {
                if (Witnesses.TryGetValue(key, out var cached)) return cached.Copy();
                // Fixed public recipes first. Any producer search is bounded up front to seeds
                // 1..32, <=512 accepted commands each, without forcing roles/history/status/RNG.
                for (uint seed = 1; seed <= (family == null ? 1 : 32); seed++)
                {
                    var initial = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size, Roster = CastTemplates.Roster.Regular }, seed);
                    Assert.That(initial.contestants.Count, Is.EqualTo(size));
                    Assert.That(initial.promises, Is.Empty); Assert.That(initial.deals, Is.Empty);
                    initial.competitionRulesVersion = CompetitionRules.Current;
                    initial.haveNotRulesStartWeek = 1; initial.strategyRulesStartWeek = 1;
                    EpisodeEngine.EnableStory(initial); EpisodeEngine.EnableRead(initial); EpisodeEngine.EnableLevers(initial);
                    EpisodeEngine.EnableWeek(initial); EpisodeEngine.EnableEconomy(initial); EpisodeEngine.EnableAgency(initial);
                    EpisodeEngine.EnableFinale(initial); EpisodeEngine.EnableCommitments(initial);
                    initial.unifiedCommitmentRulesVersion = mode; initial.unifiedHearingRulesVersion = mode;
                    SourceValid(initial);
                    var engine = new EpisodeEngine(initial);
                    var frames = new List<UnifiedVoteRevealState>(); var attempted = new HashSet<int>();
                    Point pending = null;
                    for (int step = 0; step < 512 && engine.Snapshot.phase != EpisodePhase.Finished; step++)
                    {
                        var before = engine.Snapshot;
                        var command = EpisodeEngineTests.NextCommand(before);
                        bool create = family != null && pending == null && before.pendingDiary == null
                            && before.phase == EpisodePhase.Campaign && EpisodeEngine.Voters(before).Any(person => person.isPlayer)
                            && before.Active.Count() >= 5
                            && !attempted.Contains(before.week) && EpisodeEngine.SocialActionsSpent(before) < EpisodeEngine.SocialActionBudget(before);
                        if (create)
                        {
                            string target = before.nominees[0];
                            string to = family == Promise || family == DealKind.VoteEvict ? before.nominees[1] : target;
                            create = family == Promise
                                ? !before.promises.Any(row => row.status == PromiseStatus.Active && row.kind == PromiseKind.Vote
                                    && row.fromId == before.playerId && row.toId == to)
                                : PlayerDeals.CanPropose(before, to, family, target, out _);
                            if (create)
                            {
                                attempted.Add(before.week);
                                command = EpisodeEngineTests.Command(before, family == Promise ? EpisodeCommandKind.PromiseVote : EpisodeCommandKind.ProposeDeal);
                                command.targetId = to; command.secondTargetId = target; command.text = family == Promise ? null : family;
                            }
                        }
                        if (command.kind == EpisodeCommandKind.CastVote && before.phase == EpisodePhase.Eviction)
                        {
                            if (pending == null) command.targetId = before.nominees[1]; // Retained seed1 actual tie/majority recipe.
                            else
                            {
                                bool evictNamed = family == Promise || family == DealKind.VoteEvict ? kept : !kept;
                                command.targetId = evictNamed ? pending.Row.targetId : before.nominees.Single(id => id != pending.Row.targetId);
                            }
                        }
                        if (family != null && before.pendingDiary == null && before.phase == EpisodePhase.VetoMeeting
                            && !before.vetoResolved && before.vetoHolderId == before.playerId && before.nominees.Contains(before.playerId)
                            && !EpisodeEngine.VetoIsLockedAtFinalFour(before) && EpisodeEngine.ReplacementCandidates(before).Any())
                        {
                            // Ordinary legal self-save; the real owner, not the fixture, chooses an NPC HoH's replacement.
                            command = EpisodeEngineTests.Command(before, EpisodeCommandKind.ResolveVeto);
                            command.useVeto = true; command.targetId = before.playerId;
                            command.secondTargetId = before.hohId == before.playerId ? EpisodeEngine.ReplacementCandidates(before).First().id : null;
                        }
                        if (command.kind == EpisodeCommandKind.AnswerJury && EpisodeEngine.FinaleOn(before))
                        {
                            var exchange = before.juryExchanges[before.juryQuestionIndex];
                            if (exchange.finalistId == before.playerId)
                                command.secondTargetId = FinaleQuestions.Offered(exchange.category, exchange.receiptKind)[0];
                        }
                        var after = Step(engine, before, command);
                        if (create)
                        {
                            string id = (family == Promise ? "promise-" : "deal-player-") + before.nextSequence;
                            var promise = family == Promise ? after.promises.FirstOrDefault(row => row.id == id) : null;
                            var deal = family == Promise ? null : after.deals.FirstOrDefault(row => row.id == id && row.status == DealStatus.Active);
                            if (promise != null || deal != null)
                            {
                                pending = new Point { RawPromise = promise?.Clone(), RawDeal = deal?.Clone(), CreatedCommand = command };
                                pending.Row = promise != null ? Project(promise) : Project(deal);
                                Assert.That(pending.Row.status, Is.EqualTo(DealStatus.Active));
                            }
                        }
                        if (!before.evictionResolved && after.evictionResolved && after.phase == EpisodePhase.Eviction)
                        {
                            Assert.That(UnifiedVoteRevealArchive.TryProjectCurrent(after, frames, out var complete, out var reason), Is.True, reason);
                            frames = complete;
                            if (family == null || pending != null)
                            {
                                var point = pending ?? new Point(); point.State = after.Clone(); point.Frames = frames.Select(frame => frame.Clone()).ToList();
                                if (family != null)
                                {
                                    if (family == Promise)
                                    {
                                        var ended = after.promises.Single(row => row.id == point.RawPromise.id);
                                        point.ActualStatus = ended.status.ToString().ToLowerInvariant(); point.ActualActor = ended.fromId;
                                        Assert.That(ended.brokenById, Is.EqualTo(kept ? null : ended.fromId));
                                    }
                                    else
                                    {
                                        var ended = after.deals.Single(row => row.id == point.RawDeal.id);
                                        point.ActualStatus = ended.status;
                                        Assert.That(DealResolution.VoteDeal(point.RawDeal, after.votes,
                                            after.ledger.power.Single(row => row.week == after.week).nominees, out var actor, bothNameNobody: true),
                                            Is.EqualTo(ended.status));
                                        point.ActualActor = actor; Assert.That(ended.brokenById, Is.EqualTo(kept ? null : actor));
                                    }
                                    Assert.That(point.ActualStatus, Is.EqualTo(kept ? DealStatus.Fulfilled : DealStatus.Broken));
                                }
                                Witnesses.Add(key, point); return point.Copy();
                            }
                        }
                    }
                }
                Assert.Fail("Actual public producer/reveal witness missing within32 seeds x512 accepted commands: " + key);
                return null;
            }
        }

        private static Point Later(Point point)
        {
            var later = point.Copy(); var engine = new EpisodeEngine(later.State);
            for (int step = 0; step < 512 && engine.Snapshot.week == point.State.week; step++)
            {
                var before = engine.Snapshot;
                var after = Step(engine, before, EpisodeEngineTests.NextCommand(before));
                later.State = after.Clone();
            }
            Assert.That(later.State.week, Is.EqualTo(point.State.week + 1));
            Assert.That(UnifiedVoteRevealArchive.TryValidateComplete(later.State, later.Frames, out var reason), Is.True, reason);
            return later;
        }

        private static EpisodeState Step(EpisodeEngine engine, EpisodeState before, EpisodeCommand command)
        {
            string input = Fingerprint(before), packet = Fingerprint(command);
            var result = engine.Apply(command);
            Assert.That(result.accepted && !result.duplicate, Is.True,
                "Actual " + command.kind + " at " + before.week + "/" + before.phase + ": " + result.reason);
            Assert.That(result.state.revision, Is.EqualTo(before.revision + 1));
            Assert.That(result.state.acceptedCommandIds.Last(), Is.EqualTo(command.id));
            Assert.That(Fingerprint(before), Is.EqualTo(input)); Assert.That(Fingerprint(command), Is.EqualTo(packet));
            SourceValid(result.state); return result.state;
        }
        private static void SourceValid(EpisodeState state)
        {
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            Assert.That(state.unifiedVoteReveals, Is.Empty, "Detached evidence never becomes public installed history.");
        }
        private static UnifiedCommitmentState Project(PromiseState row) => new UnifiedCommitmentState
        {
            id = row.id, kind = UnifiedVoteTogether.Vote, sourcePolicy = UnifiedCommitments.PromisePolicy,
            origin = UnifiedCommitments.PlayerPromise, makerId = row.fromId, beneficiaryId = row.toId,
            reciprocal = false, createdWeek = row.week, expiresWeek = row.expiresWeek,
            status = row.status.ToString().ToLowerInvariant(), targetId = row.targetId, trustImpact = row.impact,
            settledWeek = row.settledWeek, brokenById = row.brokenById,
        };
        private static UnifiedCommitmentState Project(DealState row) => new UnifiedCommitmentState
        {
            id = row.id, kind = UnifiedVoteTogether.Vote, sourcePolicy = UnifiedCommitments.DealPolicy,
            origin = UnifiedCommitments.PlayerDeal, makerId = row.proposerId, beneficiaryId = row.recipientId,
            reciprocal = true, createdWeek = row.week, expiresWeek = row.expiresWeek,
            status = row.status, targetId = row.targetId, subtype = row.type, trustImpact = row.trustImpact,
            linkedCommitmentId = row.linkedDealId, settledWeek = row.settledWeek, brokenById = row.brokenById,
        };
        private static EpisodeCommand CopyCommand(EpisodeCommand command) => command == null ? null : new EpisodeCommand
        {
            id = command.id, actorId = command.actorId, targetId = command.targetId, secondTargetId = command.secondTargetId,
            text = command.text, expectedRevision = command.expectedRevision, expectedPhase = command.expectedPhase,
            kind = command.kind, useVeto = command.useVeto, performance = command.performance,
        };
        private static UnifiedCommitmentState Row(string family) => new UnifiedCommitmentState
        {
            id = "vote-local", kind = UnifiedVoteTogether.Vote,
            sourcePolicy = family == Promise ? UnifiedCommitments.PromisePolicy : UnifiedCommitments.DealPolicy,
            origin = family == Promise ? UnifiedCommitments.PlayerPromise : UnifiedCommitments.PlayerDeal,
            makerId = "A", beneficiaryId = "B", reciprocal = family != Promise,
            createdWeek = 2, expiresWeek = 2, status = DealStatus.Active, targetId = "C",
            subtype = family == Promise ? null : family, trustImpact = DealTrust.Medium,
        };
        private static UnifiedVoteBallotState Ballot(string voter, string target) => new UnifiedVoteBallotState { voterId = voter, targetId = target };
        private static List<UnifiedVoteBallotState> Ballots(params UnifiedVoteBallotState[] rows) => new List<UnifiedVoteBallotState>(rows);
        private static List<string> Block(string first = "C", string second = "D") => new List<string> { first, second };

        private static void Probe(UnifiedCommitmentState row, List<UnifiedVoteBallotState> ballots, List<string> block,
            bool valid, string status, string actor)
        {
            string before = Fingerprint(new object[] { row, ballots, block });
            var references = ballots?.ToArray();
            bool result = UnifiedVoteObligations.TryVerdict(row, ballots, block, out var verdict, out var reason);
            Check(result, verdict, reason, valid, status, actor);
            Assert.That(Fingerprint(new object[] { row, ballots, block }), Is.EqualTo(before));
            if (references != null) for (int index = 0; index < references.Length; index++) Assert.AreSame(references[index], ballots[index]);
            bool repeated = UnifiedVoteObligations.TryVerdict(row, ballots, block, out var again, out var repeatedReason);
            Check(repeated, again, repeatedReason, valid, status, actor);
            if (verdict != null) Assert.AreNotSame(verdict, again);
            Assert.That(Fingerprint(new object[] { row, ballots, block }), Is.EqualTo(before));
        }
        private static void ArchiveProbe(Point point, UnifiedCommitmentState row, int week,
            bool valid, string status, string actor)
        {
            string before = Fingerprint(new object[] { point.State, point.Frames, row });
            bool result = UnifiedVoteObligations.TryFromArchive(point.State, point.Frames, row, week, out var verdict, out var reason);
            Check(result, verdict, reason, valid, status, actor);
            Assert.That(Fingerprint(new object[] { point.State, point.Frames, row }), Is.EqualTo(before));
            bool repeated = UnifiedVoteObligations.TryFromArchive(point.State, point.Frames, row, week, out var again, out var repeatedReason);
            Check(repeated, again, repeatedReason, valid, status, actor);
            if (verdict != null) Assert.AreNotSame(verdict, again);
            Assert.That(Fingerprint(new object[] { point.State, point.Frames, row }), Is.EqualTo(before));
        }
        private static void Check(bool actual, UnifiedVoteObligationVerdict verdict, string reason,
            bool valid, string status, string actor)
        {
            Assert.That(actual, Is.EqualTo(valid), reason);
            if (status == null) Assert.That(verdict, Is.Null);
            else { Assert.That(verdict, Is.Not.Null); Assert.That(verdict.Status, Is.EqualTo(status)); Assert.That(verdict.ActorId, Is.EqualTo(actor)); }
            if (valid) Assert.That(reason, Is.Null);
            else { Assert.That(reason, Is.Not.Null.And.Not.Empty); Assert.That(reason.Length, Is.LessThanOrEqualTo(180)); }
        }
        private static string Fingerprint(object value)
        { var text = new StringBuilder(); Append(value, text); return text.ToString(); }
        private static void Append(object value, StringBuilder text)
        {
            if (value == null) { text.Append("null;"); return; }
            Type type = value.GetType(); text.Append(type.FullName).Append(':');
            if (value is string word) { text.Append(word.Length).Append(':').Append(word).Append(';'); return; }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                text.Append("{Key="); Append(type.GetProperty("Key").GetValue(value, null), text);
                text.Append("Value="); Append(type.GetProperty("Value").GetValue(value, null), text);
                text.Append("};"); return;
            }
            if (type.IsPrimitive || type.IsEnum || type == typeof(decimal))
            { text.Append(value is IFormattable formatted ? formatted.ToString(value is double || value is float ? "R" : null, CultureInfo.InvariantCulture) : value.ToString()).Append(';'); return; }
            if (value is IEnumerable sequence)
            { text.Append('['); foreach (var item in sequence) Append(item, text); text.Append("];"); return; }
            text.Append('{');
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.Name, StringComparer.Ordinal))
            { text.Append(field.Name).Append('='); Append(field.GetValue(value), text); }
            text.Append("};");
        }
    }
}
