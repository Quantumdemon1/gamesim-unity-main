using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Vote family V5a: the readers' views of mode 2's commitments and their cheap storage check.
    ///
    /// <para><see cref="CommitmentReferences.RawPromises"/> / <see cref="CommitmentReferences.RawDeals"/> are what a mode-1
    /// reader of the raw lists reads: the very lists in modes 0 and 1, and in mode 2 the list mode 1's would have been - raw
    /// rows and projected Vote rows interleaved where mode 1 appended them. <see cref="CommitmentReferences.Promises"/> and
    /// <see cref="CommitmentReferences.Deals"/> are mode 1's in mode 2 too: that list, then the Safety rows. Both, and the
    /// offers waiting on the player (<see cref="NpcDeals.Pending"/>, whose gate V5a flips), are checked on the exact mode-2
    /// projection of a public mode-1 season at every one of its commands (pattern P2), and the offers against a ballot the
    /// player cannot know (pattern P3).</para>
    ///
    /// <para>Readers check the storage only (<see cref="UnifiedVoteFamilyValidation.TryValidateStorage"/>), never the complete
    /// core: a reader may run in the middle of a command. The check accepts every state the core accepts on the walks and
    /// refuses each storage defect. The core, no longer read in a command's middle, refuses a Results state whose evictee's
    /// Vote deal has already ended (the lead's decision D4) and mode 2 without the deal pass from week one (D5); a reader's
    /// refusal inside a mode-2 command refuses the command, where modes 0 and 1 keep their error path (D3).</para>
    /// </summary>
    public sealed class CommitmentReaderViewTests
    {
        // ------------------------------------------------------------ the views, element for element (P2)

        /// <summary>
        /// The views and the offers waiting, at one command of a walk (<see cref="ModeTwoReaderParityTests"/> runs it at every
        /// command of seeds 1 to 32): mode 1's raw lists are the lists themselves; mode 2's are mode 1's, element for element;
        /// the projection is a season the complete core accepts, and so does the storage check.
        /// </summary>
        internal static void CheckViews(EpisodeState mode1, EpisodeState mode2, string where)
        {
            Assert.That(CommitmentReferences.RawPromises(mode1), Is.SameAs(mode1.promises), where + ": mode 1's raw promises are the list itself.");
            Assert.That(CommitmentReferences.RawDeals(mode1), Is.SameAs(mode1.deals), where + ": mode 1's raw deals are the list itself.");
            Assert.That(Json(CommitmentReferences.RawPromises(mode2)), Is.EqualTo(Json(mode1.promises)), where + ": mode 2's raw promises are mode 1's.");
            Assert.That(Json(CommitmentReferences.RawDeals(mode2)), Is.EqualTo(Json(mode1.deals)), where + ": mode 2's raw deals are mode 1's.");
            Assert.That(Json(CommitmentReferences.Promises(mode2)), Is.EqualTo(Json(CommitmentReferences.Promises(mode1))), where + ": the promise view.");
            Assert.That(Json(CommitmentReferences.Deals(mode2)), Is.EqualTo(Json(CommitmentReferences.Deals(mode1))), where + ": the deal view.");
            Assert.That(Json(NpcDeals.Pending(mode2)), Is.EqualTo(Json(NpcDeals.Pending(mode1))), where + ": the offers waiting on the player.");
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(mode2, out var core), Is.True, where + ": the projection is a season the core accepts. " + core);
            Assert.That(UnifiedVoteFamilyValidation.TryValidateStorage(mode2, out var storage), Is.True, where + ": the storage check accepts what the core accepts. " + storage);
        }

        [Test]
        public void TheWalksMergeVoteRowsAmongRawOnesAndShowTheHousesOffers()
        {
            // The sweep above means something only if its seasons hold what the views interleave: Vote rows between raw
            // ones, in both lists, and offers waiting on the player.
            bool interleavedPromise = false, interleavedDeal = false, offer = false;
            // Seed 5's house puts a vote bargain to the player; the first three interleave both lists.
            foreach (uint seed in new uint[] { 1, 2, 3, 5 })
                ModeTwoReaderSweep.Walk(seed, (mode1, mode2, where) =>
                {
                    interleavedPromise |= Interleaved(mode1.promises.Select(p => p.kind == PromiseKind.Vote).ToList());
                    interleavedDeal |= Interleaved(mode1.deals.Select(d => KnownBallots.IsVoteDeal(d.type)).ToList());
                    offer |= NpcDeals.Pending(mode2).Any(d => KnownBallots.IsVoteDeal(d.type));
                });
            Assert.That(interleavedPromise, Is.True, "A Vote promise between raw ones.");
            Assert.That(interleavedDeal, Is.True, "A Vote deal between raw ones.");
            Assert.That(offer, Is.True, "A canonical Vote offer waiting on the player.");
        }

        /// <summary>A Vote row with a raw row after it: a merge, not an append.</summary>
        private static bool Interleaved(List<bool> vote) => vote.Select((isVote, i) => isVote && vote.Skip(i + 1).Any(next => !next)).Any(x => x);

        // ------------------------------------------------------------ the storage check

        private static EpisodeState stored;

        /// <summary>A projection the walk reached that holds Vote promises and deals.</summary>
        private static EpisodeState Stored() => (stored ??= ModeTwoReaderSweep.Find("a season holding Vote promises and deals", (mode1, mode2) =>
            mode2.unifiedCommitments.Any(r => r.kind == UnifiedVoteTogether.Vote && r.sourcePolicy == UnifiedCommitments.PromisePolicy)
            && mode2.unifiedCommitments.Any(r => r.kind == UnifiedVoteTogether.Vote && r.sourcePolicy == UnifiedCommitments.DealPolicy)
            && mode2.deals.Count > 0).mode2).Clone();

        [Test]
        public void TheStorageCheckAcceptsTheSeasonAsItIs()
        {
            var s = Stored();
            Assert.That(UnifiedVoteFamilyValidation.TryValidateStorage(s, out var error), Is.True, error);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(s, out error), Is.True, error);
        }

        [TestCase("raw-vote-promise")] [TestCase("raw-vote-deal")] [TestCase("raw-safety-deal")] [TestCase("null-row")]
        [TestCase("duplicate-id")] [TestCase("bad-id")] [TestCase("unknown-kind")] [TestCase("unknown-policy")] [TestCase("unknown-origin")]
        [TestCase("promise-subtype")] [TestCase("deal-subtype")] [TestCase("unknown-status")] [TestCase("accepted-status")]
        [TestCase("proposed-promise")] [TestCase("unknown-party")] [TestCase("same-parties")] [TestCase("unknown-target")]
        [TestCase("unresolved-link")] [TestCase("self-link")] [TestCase("promise-link")] [TestCase("capacity")]
        [TestCase("promise-trust")] [TestCase("deal-trust")] [TestCase("unknown-trust")] [TestCase("missing-target")]
        public void TheStorageCheckRefusesEachDefectAndSoDoesEveryReader(string defect)
        {
            var s = Stored();
            var promise = s.unifiedCommitments.First(r => r.kind == UnifiedVoteTogether.Vote && r.sourcePolicy == UnifiedCommitments.PromisePolicy);
            var deal = s.unifiedCommitments.First(r => r.kind == UnifiedVoteTogether.Vote && r.sourcePolicy == UnifiedCommitments.DealPolicy);
            string other = s.contestants.First(c => c.id != deal.makerId && c.id != deal.beneficiaryId).id;
            switch (defect)
            {
                case "raw-vote-promise":
                    s.promises.Add(new PromiseState { id = "mirror-promise", fromId = s.playerId, toId = other, targetId = deal.makerId,
                        kind = PromiseKind.Vote, status = PromiseStatus.Active, week = s.week, expiresWeek = s.week }); break;
                case "raw-vote-deal":
                    s.deals.Add(new DealState { id = "mirror-deal", proposerId = s.playerId, recipientId = other, type = DealKind.VoteTogether,
                        status = DealStatus.Active, week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(DealKind.VoteTogether) }); break;
                case "raw-safety-deal":
                    s.deals.Add(new DealState { id = "mirror-safety", proposerId = s.playerId, recipientId = other, type = DealKind.SafetyAgreement,
                        status = DealStatus.Active, week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(DealKind.SafetyAgreement) }); break;
                case "null-row": s.unifiedCommitments.Add(null); break;
                case "duplicate-id": deal.id = s.deals[0].id; break;
                case "bad-id": deal.id = "deal-\u0001"; break;
                case "unknown-kind": deal.kind = "pact"; break;
                case "unknown-policy": deal.sourcePolicy = "oath"; break;
                case "unknown-origin": promise.origin = UnifiedCommitments.HoHPitch; break;
                case "promise-subtype": promise.subtype = DealKind.VoteSave; break;
                case "deal-subtype": deal.subtype = DealKind.FinalTwo; break;
                case "unknown-status": deal.status = "maybe"; break;
                case "accepted-status": deal.status = DealStatus.Accepted; break;
                case "proposed-promise": promise.status = DealStatus.Proposed; break;
                case "unknown-party": deal.makerId = "nobody"; break;
                case "same-parties": deal.beneficiaryId = deal.makerId; break;
                case "unknown-target": deal.targetId = "nobody"; break;
                case "unresolved-link": deal.linkedCommitmentId = "deal-price-999999"; break;
                case "self-link": deal.linkedCommitmentId = deal.id; break;
                case "promise-link": promise.linkedCommitmentId = deal.id; break;
                // The pre-V6 save-contract review: the weight a reader prices a row by is the source's, as the core holds it.
                case "promise-trust": promise.trustImpact = DealTrust.Critical; break;
                case "deal-trust": deal.trustImpact = deal.trustImpact == DealTrust.Low ? DealTrust.High : DealTrust.Low; break;
                case "unknown-trust": deal.trustImpact = "enormous"; break;
                // A word or deal its source gave a target, without one.
                case "missing-target":
                    s.unifiedCommitments.First(r => r.kind == UnifiedVoteTogether.Vote && r.targetId != null).targetId = null; break;
                case "capacity":
                    // Filler history that only fills a shelf: no owner files 200 rows in a test season.
                    int promises = s.promises.Count + s.unifiedCommitments.Count(r => r.sourcePolicy == UnifiedCommitments.PromisePolicy);
                    for (int i = promises; i <= UnifiedCommitments.FamilyCapacity; i++)
                        s.promises.Add(new PromiseState { id = "filler-" + i, fromId = s.playerId, toId = other, kind = PromiseKind.FinalTwo,
                            status = PromiseStatus.Expired, week = 1 });
                    break;
                default: Assert.Fail("Unknown defect " + defect); break;
            }
            Assert.That(UnifiedVoteFamilyValidation.TryValidateStorage(s, out var error), Is.False, defect + ": the storage check refuses it.");
            Assert.That(error, Is.Not.Empty);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(s, out _), Is.False, defect + ": and the complete core does too.");
            // Each a commitment reader's own refusal, which a mode-2 command takes as its refusal (D3, narrowed at the pre-V6 review).
            foreach (var (reader, read) in new (string, TestDelegate)[] {
                ("the deal view", () => CommitmentReferences.Deals(s)), ("the raw promise view", () => CommitmentReferences.RawPromises(s)),
                ("the Safety history", () => UnifiedCommitmentHistory.Breaches(s)), ("the Vote history", () => UnifiedVoteHistory.Records(s)),
                ("the Vote breaches", () => UnifiedVoteHistory.Breaches(s)), ("the Vote decisions", () => UnifiedVoteHistory.Decisions(s)) })
            {
                var refusal = Assert.Throws<ArgumentException>(read, defect + ": " + reader + " refuses it.");
                Assert.That(ProspectiveVoteFacade.IsStorageRefusal(refusal), Is.True, defect + ": " + reader + "'s refusal is marked as a reader's.");
            }
            Assert.That(UnifiedCommitmentHearings.ValidateStorage(s, out _), Is.False, defect + ": the hearing storage check refuses it.");
        }

        [Test]
        public void TheStorageCheckJudgesNoChronologyBallotOrEnding()
        {
            // What a reader in the middle of a command may meet before the command finishes: a decided row whose frame is
            // not yet published, a row past its term. The core refuses each; the storage check reads them.
            var s = Stored();
            var deal = s.unifiedCommitments.First(r => r.kind == UnifiedVoteTogether.Vote && r.sourcePolicy == UnifiedCommitments.DealPolicy);
            var unpublished = s.Clone();
            var row = unpublished.unifiedCommitments.Single(r => r.id == deal.id);
            row.status = DealStatus.Fulfilled; row.settledWeek = unpublished.week;
            row.voteFirstRevealWeek = row.voteBindingWeek = unpublished.week;
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(unpublished, out _), Is.False, "No frame decided it yet.");
            Assert.That(UnifiedVoteFamilyValidation.TryValidateStorage(unpublished, out var error), Is.True, error);
            Assert.DoesNotThrow(() => CommitmentReferences.Deals(unpublished));
            Assert.DoesNotThrow(() => UnifiedVoteHistory.Records(unpublished));
            Assert.Throws<ArgumentException>(() => UnifiedVoteHistory.Decisions(unpublished),
                "A decision needs its published frame: read outside the reveal that decides it.");
        }

        // ------------------------------------------------------------ the middle of a command, and the save gate (D4)

        private static (EpisodeState mode1, EpisodeState mode2) results;

        /// <summary>Regular Results the walk reached, a Vote deal still binding the evictee: the next Advance ends it.</summary>
        private static EpisodeState Results()
        {
            if (results.mode2 == null)
                results = ModeTwoReaderSweep.Find("regular Results with a Vote deal still binding the evictee", (mode1, mode2) =>
                    mode2.phase == EpisodePhase.Eviction && mode2.evictionResolved && mode2.evictionStage == EvictionStage.Results
                    && EvicteeDeal(mode2) != null);
            return results.mode2.Clone();
        }

        private static UnifiedCommitmentState EvicteeDeal(EpisodeState s)
        {
            string evictee = s.ledger.power.LastOrDefault(p => p.week == s.week)?.evicteeId;
            return evictee == null ? null : s.unifiedCommitments.FirstOrDefault(r => r.kind == UnifiedVoteTogether.Vote
                && r.sourcePolicy == UnifiedCommitments.DealPolicy && DealStatus.Binds(r.status)
                && (r.makerId == evictee || r.beneficiaryId == evictee || r.targetId == evictee));
        }

        [Test]
        public void InTheMiddleOfTheAdvanceTheReadersReadWhatTheCoreWouldRefuseAsASave()
        {
            var s = Results();
            var binding = EvicteeDeal(s);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(s, out var error), Is.True, "Results as the reveal left it. " + error);
            // The Advance's departure ending writes first (EndWithTheEvictee); the eviction still reads Results.
            var middle = s.Clone();
            middle.unifiedCommitments.Single(r => r.id == binding.id).status = DealStatus.Expired;
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(middle, out error), Is.False,
                "No command leaves Results with the evictee's deal already ended: the core refuses it as a save (D4).");
            Assert.That(error, Does.Contain("expired"));
            Assert.That(UnifiedVoteFamilyValidation.TryValidateStorage(middle, out error), Is.True, error);
            Assert.That(CommitmentReferences.Deals(middle).Single(d => d.id == binding.id).status, Is.EqualTo(DealStatus.Expired));
            Assert.DoesNotThrow(() => UnifiedCommitmentHistory.Breaches(middle));
            Assert.DoesNotThrow(() => NpcDeals.Pending(middle));
            Assert.That(UnifiedCommitmentHearings.ValidateStorage(middle, out error), Is.True, "The Safety gateway's check in that Advance. " + error);
            // And the real Advance, through the seam, ends it and leaves a season the core accepts.
            var after = ProspectiveVoteFacade.Engine(s).Apply(ProspectiveVoteTwins.Command(s, EpisodeCommandKind.Advance));
            Assert.That(after.accepted, Is.True, after.reason);
            Assert.That(after.state.unifiedCommitments.Single(r => r.id == binding.id).status, Is.EqualTo(DealStatus.Expired));
            Assert.That(after.state.phase, Is.Not.EqualTo(EpisodePhase.Eviction));
        }

        // ------------------------------------------------------------ the deal pass from week one (D5)

        [Test]
        public void ModeTwoRequiresTheHousesDealPassFromTheFirstWeek()
        {
            var fresh = PinnedVoteSeason.Fresh(1);
            var mode2 = PinnedVoteSeason.Project(fresh, new Dictionary<string, ProspectiveVoteOwner>(), Array.Empty<UnifiedVoteRevealState>());
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(mode2, out var error), Is.True, error);
            mode2.dealRulesStartWeek = 2;
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(mode2, out error), Is.False);
            Assert.That(error, Does.Contain("deal pass"));
            Assert.Throws<ArgumentException>(() => ProspectiveVoteFacade.Engine(mode2), "The seam refuses it at its input.");
            fresh.dealRulesStartWeek = 2;
            Assert.That(EpisodeValidation.TryValidate(fresh, out error), Is.True, "Mode 1 keeps its own boundary. " + error);
        }

        // ------------------------------------------------------------ a reader's refusal inside a command (D3)

        private static readonly FieldInfo Current = typeof(EpisodeEngine).GetField("current", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        /// An engine at <paramref name="valid"/>, then holding <paramref name="corrupt"/> as its state: a state no command
        /// leaves, installed past the engine's input check so that only a reader inside the command meets it.
        /// </summary>
        private static EpisodeEngine Holding(EpisodeEngine engine, EpisodeState corrupt)
        {
            Assert.That(Current, Is.Not.Null, "EpisodeEngine.current is the engine's state.");
            Current.SetValue(engine, corrupt);
            return engine;
        }

        [Test]
        public void InModeTwoAReadersRefusalRefusesTheCommand()
        {
            var s = Results();
            var corrupt = s.Clone();
            // A raw Vote mirror: the storage every reader checks refuses it, first at the Advance's Safety gateway.
            corrupt.deals.Add(new DealState { id = "mirror-deal", proposerId = s.playerId, recipientId = EvicteeDeal(s).makerId == s.playerId
                    ? EvicteeDeal(s).beneficiaryId : EvicteeDeal(s).makerId, type = DealKind.VoteTogether, status = DealStatus.Active,
                week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(DealKind.VoteTogether) });
            var engine = Holding(ProspectiveVoteFacade.Engine(s), corrupt);
            string before = Json(engine.Snapshot);
            var result = engine.Apply(ProspectiveVoteTwins.Command(corrupt, EpisodeCommandKind.Advance));
            Assert.That(result.accepted, Is.False);
            Assert.That(result.reason, Does.Contain("raw mirrors"));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before), "A refusal installs nothing.");
        }

        /// <summary>
        /// The refusal a mode-2 command takes as its own (the pre-V6 review narrowed D3's catch): exactly an ArgumentException a
        /// commitment reader marked (CommitmentReferences.StorageRefusal) - never an ArgumentNullException or
        /// ArgumentOutOfRangeException from a bug, and never an unmarked ArgumentException, which escape as in modes 0 and 1.
        /// </summary>
        [Test]
        public void OnlyACommitmentReadersOwnRefusalIsTheCommandsRefusal()
        {
            var corrupt = Stored();
            corrupt.deals.Add(new DealState { id = "mirror-deal", proposerId = corrupt.playerId, recipientId = corrupt.contestants.First(c => !c.isPlayer).id,
                type = DealKind.VoteTogether, status = DealStatus.Active, week = corrupt.week, expiresWeek = corrupt.week,
                trustImpact = DealKind.DefaultTrust(DealKind.VoteTogether) });
            var refusal = Assert.Throws<ArgumentException>(() => CommitmentReferences.Deals(corrupt));
            Assert.That(refusal.GetType(), Is.EqualTo(typeof(ArgumentException)), "The type the readers have always thrown.");
            Assert.That(ProspectiveVoteFacade.IsStorageRefusal(refusal), Is.True);
            Assert.That(ProspectiveVoteFacade.IsStorageRefusal(new ArgumentException(refusal.Message)), Is.False, "Unmarked.");
            Assert.That(ProspectiveVoteFacade.IsStorageRefusal(new ArgumentNullException("s")), Is.False, "A bug's null.");
            Assert.That(ProspectiveVoteFacade.IsStorageRefusal(new ArgumentOutOfRangeException("s")), Is.False, "A bug's range.");
            var marked = new ArgumentNullException("s");
            foreach (var key in refusal.Data.Keys) marked.Data[key] = refusal.Data[key];
            Assert.That(ProspectiveVoteFacade.IsStorageRefusal(marked), Is.False, "Exactly an ArgumentException, whatever its data.");
            Assert.That(ProspectiveVoteFacade.IsStorageRefusal(null), Is.False);
        }

        /// <summary>
        /// The narrowed catch at a real command: a mode-2 diary reflection whose reader (WebDiaryRoom) refuses a malformed persona
        /// with an unmarked ArgumentException. It escapes the command, as the same refusal does in mode 0 (below) - before the
        /// pre-V6 review mode 2 turned it into a refusal.
        /// </summary>
        [Test]
        public void InModeTwoAnyOtherArgumentExceptionStillEscapesTheCommand()
        {
            EpisodeEngine engine = null;
            for (uint seed = 1; seed <= 32 && engine == null; seed++)
            {
                var fresh = PinnedVoteSeason.Project(PinnedVoteSeason.Fresh(seed), new Dictionary<string, ProspectiveVoteOwner>(), Array.Empty<UnifiedVoteRevealState>());
                var walk = ProspectiveVoteFacade.Engine(fresh);
                for (int step = 0; step < 600 && walk.Snapshot.pendingDiary == null && walk.Snapshot.phase != EpisodePhase.Finished; step++)
                    Assert.That(walk.Apply(EpisodeEngineTests.NextCommand(walk.Snapshot)).accepted, Is.True);
                if (walk.Snapshot.pendingDiary != null) engine = walk;
            }
            Assert.That(engine, Is.Not.Null, "Fixture: a mode-2 walk reaches a diary reflection.");
            var s = engine.Snapshot;
            Assert.That(s.unifiedCommitmentRulesVersion, Is.EqualTo(UnifiedVoteFamilyValidation.Version), "Fixture: mode 2.");
            var reflect = ProspectiveVoteTwins.Command(s, EpisodeCommandKind.ReflectDiary, s.pendingDiary.id, EpisodeEngine.CurrentDiary(s).choices[0].id);
            Assert.That(ProspectiveVoteFacade.Engine(s).Apply(reflect).accepted, Is.True, "Fixture: the reflection is one the season takes.");
            var corrupt = s.Clone();
            corrupt.playerPersona.scores[0].score = -1;
            Holding(engine, corrupt);
            var error = Assert.Throws<ArgumentException>(() => engine.Apply(reflect), "Not a commitment reader's refusal: it escapes, as in mode 0.");
            Assert.That(error.Message, Does.Contain("malformed"));
            Assert.That(ProspectiveVoteFacade.IsStorageRefusal(error), Is.False);
        }

        [Test]
        public void InModeOneAReadersRefusalStillEscapesTheCommand()
        {
            Results();
            var s = results.mode1.Clone();
            var corrupt = s.Clone();
            // A raw Safety mirror beside canonical Safety: mode 1's storage check refuses it at the same gateway.
            string other = s.contestants.First(c => !c.isPlayer && c.status == ContestantStatus.Active).id;
            corrupt.deals.Add(new DealState { id = "mirror-safety", proposerId = s.playerId, recipientId = other, type = DealKind.SafetyAgreement,
                status = DealStatus.Active, week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(DealKind.SafetyAgreement) });
            var engine = Holding(new EpisodeEngine(s), corrupt);
            Assert.Throws<ArgumentException>(() => engine.Apply(ProspectiveVoteTwins.Command(corrupt, EpisodeCommandKind.Advance)),
                "Modes 0 and 1 keep their error path exactly (D3).");
        }

        [Test]
        public void InModeZeroAReadersRefusalStillEscapesTheCommand()
        {
            // A mode-0 season at its first diary reflection.
            EpisodeEngine engine = null;
            for (uint seed = 1; seed <= 32 && engine == null; seed++)
            {
                var walk = new EpisodeEngine(PinnedVoteSeason.Fresh(seed, mode: 0));
                for (int step = 0; step < 600 && walk.Snapshot.pendingDiary == null && walk.Snapshot.phase != EpisodePhase.Finished; step++)
                    Assert.That(walk.Apply(EpisodeEngineTests.NextCommand(walk.Snapshot)).accepted, Is.True);
                if (walk.Snapshot.pendingDiary != null) engine = walk;
            }
            Assert.That(engine, Is.Not.Null, "Fixture: a mode-0 walk reaches a diary reflection.");
            var s = engine.Snapshot;
            Assert.That((s.unifiedCommitmentRulesVersion, UnifiedCommitments.SafetyAuthorityOn(s)), Is.EqualTo((0, false)), "Fixture: mode 0.");
            var reflect = ProspectiveVoteTwins.Command(s, EpisodeCommandKind.ReflectDiary, s.pendingDiary.id, EpisodeEngine.CurrentDiary(s).choices[0].id);
            Assert.That(new EpisodeEngine(s).Apply(reflect).accepted, Is.True, "Fixture: the reflection is one the season takes.");
            // A malformed persona: the diary's reader (WebDiaryRoom) refuses the state it is given, inside the command.
            var corrupt = s.Clone();
            corrupt.playerPersona.scores[0].score = -1;
            Holding(engine, corrupt);
            var error = Assert.Throws<ArgumentException>(() => engine.Apply(reflect), "Modes 0 and 1 keep their error path exactly (D3).");
            Assert.That(error.Message, Does.Contain("malformed"));
        }

        // ------------------------------------------------------------ a hidden ballot (P3)

        [Test]
        public void TheOffersWaitingShowNothingOfABallotThePlayerCannotKnow()
        {
            var blind = ModeTwoReaderSweep.Flip(false);
            blind.AssertBlind("NpcDeals.Pending", s => NpcDeals.Pending(s));
            blind.AssertBlind("The known ballots", s => KnownBallots.Read(s, s.week));
            var told = ModeTwoReaderSweep.Flip(true);
            told.AssertControl("The known ballots", s => KnownBallots.Read(s, s.week));
        }

        private static string Json(object value) => PinnedVoteSeason.Json(value);

#if !UNITY_5_3_OR_NEWER
        // ------------------------------------------------------------ cost (measured, never asserted)

        [Test, Explicit("Measures the readers' cost on a late mode-2 season against mode 1: numbers for the build log.")]
        public void TheReadersCostOnALateSeason()
        {
            var late = ModeTwoReaderSweep.Find("a late campaign in a house of 12", (mode1, mode2) =>
                mode2.phase == EpisodePhase.Campaign && mode2.week >= 7, 12);
            var command = EpisodeEngineTests.NextCommand(late.mode1);
            string Median(Action run)
            {
                for (int i = 0; i < 20; i++) run();
                var times = new List<double>();
                for (int i = 0; i < 200; i++)
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    run();
                    times.Add(watch.Elapsed.TotalMilliseconds);
                }
                times.Sort();
                return times[times.Count / 2].ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " ms";
            }
            TestContext.Out.WriteLine("Week " + late.mode2.week + ", " + late.mode2.unifiedCommitments.Count + " canonical rows, "
                + late.mode2.deals.Count + " raw deals, " + late.mode2.promises.Count + " raw promises.");
            TestContext.Out.WriteLine("CommitmentReferences.Deals: mode 1 " + Median(() => CommitmentReferences.Deals(late.mode1))
                + ", mode 2 " + Median(() => CommitmentReferences.Deals(late.mode2)));
            TestContext.Out.WriteLine("CommitmentReferences.RawDeals: mode 1 " + Median(() => CommitmentReferences.RawDeals(late.mode1))
                + ", mode 2 " + Median(() => CommitmentReferences.RawDeals(late.mode2)));
            TestContext.Out.WriteLine("NpcDeals.Pending: mode 1 " + Median(() => NpcDeals.Pending(late.mode1))
                + ", mode 2 " + Median(() => NpcDeals.Pending(late.mode2)));
            TestContext.Out.WriteLine("The full family check (before V5a, every reader call): "
                + Median(() => UnifiedVoteFamilyValidation.TryValidate(late.mode2, late.mode2.unifiedVoteReveals, out _))
                + "; the storage check: " + Median(() => UnifiedVoteFamilyValidation.TryValidateStorage(late.mode2, out _)));
            // An engine accepts a command once, so each run builds its own: the construction (one validation) alone, then with the Apply.
            TestContext.Out.WriteLine("An engine: mode 1 " + Median(() => new EpisodeEngine(late.mode1))
                + ", mode 2 " + Median(() => ProspectiveVoteFacade.Engine(late.mode2)));
            TestContext.Out.WriteLine("An engine and one Apply (" + command.kind + "): mode 1 " + Median(() => new EpisodeEngine(late.mode1).Apply(command))
                + ", mode 2 " + Median(() => ProspectiveVoteFacade.Engine(late.mode2).Apply(command)));
        }
#endif
    }
}
