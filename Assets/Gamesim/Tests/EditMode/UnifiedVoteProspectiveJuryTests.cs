using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Complete-core saved-reference diagnostics, not a mode2 producer, converter, save or game.
    /// Each witness is a constructed public mode1 season (<see cref="PinnedVoteSeason"/>): the player
    /// makes the agreement with real commands, and the only constructed facts are NPC ballots, pinned
    /// on a detached snapshot after the real NPC batch (or, in a week the player does not vote, cast
    /// before the Advance that would cast and count them at once) and installed through public
    /// validation. The source then settles everything itself: the tally and tie-break, promise, oath
    /// and deal verdicts, the told claim's verdict, the reveal's records, who is evicted. Every owner,
    /// original term, actual verdict, private frame and knowledge claim is the source's. Only detached
    /// canonical representation and source-qualified pending question selection are projected; the
    /// latter is not claimed to be the RNG-selected menu. No role, phase, claim, RNG, departure,
    /// receipt owner or knowledge is planted into a positive base.
    /// </summary>
    public sealed class UnifiedVoteProspectiveJuryTests
    {
        // 0: kept promise; 1: broken promise; 2: own sole targeted breach;
        // 3: kept Together with a real told claim; 4: collective unknown Together breach.
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void ActualSourceOwnersSupportTheirExactSavedJuryCategoryAndFirstDecision(int scenario)
        {
            var witness = Source(scenario); var s = Candidate(witness);
            Check(s, true);
            var row = Selected(s, witness); var decision = UnifiedVoteHistory.FindDecision(s, row.id);
            Assert.That(decision, Is.Not.Null);
            Assert.That(decision.SettledWeek, Is.EqualTo(witness.Receipt.week));
            Assert.That(decision.Status, Is.EqualTo(row.status));
            Assert.That(decision.ActorId, Is.EqualTo(witness.ActualActor));
            if (scenario == 0)
            {
                Assert.That(decision.ActorId, Is.EqualTo(witness.State.promises.Single(item => item.id == witness.SelectedId).fromId),
                    "A kept promise's decision actor is its actual maker, not a breaker.");
                Assert.That(row.brokenById, Is.Null);
                Assert.That(witness.State.promises.Single(item => item.id == witness.SelectedId).brokenById, Is.Null);
            }
            Assert.That(s.unifiedVoteReveals.Single(frame => frame.week == decision.SettledWeek).ballots
                .Any(ballot => ballot.voterId == s.playerId), Is.True);
            if (scenario >= 2)
                Assert.That(KnownBallots.DealOutcomeKnown(s, CommitmentReferences.FindDeal(s, row.id)), Is.True);
            // Flipped at vote family V6: public validation takes mode 2 to the same complete core (it refused it until V6).
            Assert.That(EpisodeValidation.TryValidate(s, out var publicError), Is.True, publicError);
            Assert.DoesNotThrow(() => new EpisodeEngine(s));
            Assert.That(UnifiedCommitments.RulesOn(s), Is.False);
            Assert.That(UnifiedCommitmentHearings.RulesOn(s), Is.False);
            Assert.That(Trace(witness.State), Is.EqualTo(witness.SourceImage));
        }

        [TestCase(0, 0)] [TestCase(0, 1)] [TestCase(0, 2)] [TestCase(0, 3)]
        [TestCase(1, 0)] [TestCase(1, 1)] [TestCase(1, 2)] [TestCase(1, 3)]
        [TestCase(2, 0)] [TestCase(2, 1)] [TestCase(2, 2)] [TestCase(2, 3)]
        [TestCase(3, 0)] [TestCase(3, 1)] [TestCase(3, 2)] [TestCase(3, 3)]
        public void SavedJuryTypeCategoryPartyAndMissingOwnerRefuseAfterAnAcceptedWholeBase(int scenario, int defect)
        {
            var witness = Source(scenario); var s = Candidate(witness); Check(s, true);
            var row = Selected(s, witness); var q = s.juryExchanges[s.juryQuestionIndex];
            switch (defect)
            {
                case 0:
                    q.receiptKind = q.receiptKind == FinaleQuestions.PromiseReceipt
                        ? FinaleQuestions.DealReceipt : FinaleQuestions.PromiseReceipt;
                    SelectQuestion(s, q, q.category, q.receiptKind, q.receiptId, row.settledWeek);
                    break;
                case 1:
                    SelectQuestion(s, q, q.category == FinaleQuestions.Personal
                        ? FinaleQuestions.Accountability : FinaleQuestions.Personal,
                        q.receiptKind, q.receiptId, row.settledWeek);
                    break;
                case 2:
                    // This is an explicitly corrupted party, not a second claimed source owner.
                    // The actual ballot still supplies the SAME agreement-local verdict.
                    row.beneficiaryId = witness.OtherParty;
                    // This defect stays internally coherent; no effect was executed or replayed.
                    if (row.status == DealStatus.Broken) row.settlementEffectKey = UnifiedVoteHistory.Key(row, row.settledWeek);
                    Assert.That(UnifiedVoteHistory.FindDecision(s, row.id)?.Status, Is.EqualTo(row.status));
                    break;
                default:
                    q.receiptId = "missing-vote-jury-owner";
                    break;
            }
            Assert.That(UnifiedVoteFamilyValidation.TryValidate(s, s.unifiedVoteReveals, out var familyError),
                Is.True, familyError);
            Check(s, false);
            Assert.That(Trace(witness.State), Is.EqualTo(witness.SourceImage));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void CoherentTerminalMetadataCannotRewriteTheActualFirstRecordedVerdict(int scenario)
        {
            var witness = Source(scenario); var s = Candidate(witness); Check(s, true);
            var row = Selected(s, witness);
            row.status = row.status == DealStatus.Fulfilled ? DealStatus.Broken : DealStatus.Fulfilled;
            row.brokenById = row.status == DealStatus.Broken
                ? row.subtype == DealKind.VoteTogether ? null : s.playerId : null;
            row.settlementEffectKey = row.status == DealStatus.Broken ? UnifiedVoteHistory.Key(row, row.settledWeek) : null;
            Assert.That(UnifiedVoteFamilyValidation.TryValidate(s, s.unifiedVoteReveals, out _), Is.False);
            Check(s, false);
            Assert.That(Trace(witness.State), Is.EqualTo(witness.SourceImage));
        }

        [Test]
        public void RemovingTheActualToldClaimDoesNotLetPrivateArchiveSupplyPartnerKnowledge()
        {
            var witness = Source(3); var s = Candidate(witness); Check(s, true);
            var row = Selected(s, witness); string archive = JsonConvert.SerializeObject(s.unifiedVoteReveals);
            Assert.That(KnownBallots.Knows(s, row.settledWeek, row.beneficiaryId), Is.True);
            int removed = s.ledger.claims.RemoveAll(claim => claim.week == row.settledWeek && claim.voterId == row.beneficiaryId);
            Assert.That(removed, Is.GreaterThan(0), "Remove genuine source-written knowledge, never fabricate a hidden ballot.");
            Assert.That(KnownBallots.Knows(s, row.settledWeek, row.beneficiaryId), Is.False);
            Assert.That(KnownBallots.DealOutcomeKnown(s, CommitmentReferences.FindDeal(s, row.id)), Is.False);
            Assert.That(UnifiedVoteFamilyValidation.TryValidate(s, s.unifiedVoteReveals, out var familyError), Is.True, familyError);
            Check(s, false);
            Assert.That(JsonConvert.SerializeObject(s.unifiedVoteReveals), Is.EqualTo(archive));
            Assert.That(Trace(witness.State), Is.EqualTo(witness.SourceImage));
        }

        [Test]
        public void ActualCollectivePrivateBreachCannotClaimAPlayerAccountabilityOwner()
        {
            var witness = Source(4); var s = Project(witness.State, witness.Tracked, witness.Frames);
            Check(s, true);
            var row = Selected(s, witness); var decision = UnifiedVoteHistory.FindDecision(s, row.id);
            Assert.That(row.status, Is.EqualTo(DealStatus.Broken)); Assert.That(row.brokenById, Is.Null);
            Assert.That(decision.ActorId, Is.Null);
            Assert.That(FinalistRead.DealBreaker(s, CommitmentReferences.FindDeal(s, row.id)), Is.Null);
            Assert.That(KnownBallots.Knows(s, row.settledWeek, row.beneficiaryId), Is.False);
            Assert.That(KnownBallots.DealOutcomeKnown(s, CommitmentReferences.FindDeal(s, row.id)), Is.False);
            var q = s.juryExchanges[s.juryQuestionIndex];
            SelectQuestion(s, q, FinaleQuestions.Accountability, FinaleQuestions.DealReceipt, row.id, row.settledWeek);
            Assert.That(UnifiedVoteFamilyValidation.TryValidate(s, s.unifiedVoteReveals, out var familyError), Is.True, familyError);
            Check(s, false);
            Assert.That(KnownBallots.Knows(s, row.settledWeek, row.beneficiaryId), Is.False);
            Assert.That(Trace(witness.State), Is.EqualTo(witness.SourceImage));
        }

        /// <summary>
        /// Vote family V5e: a real mode-2 game's juror asks an Accountability question whose receipt is a canonical Vote row, as
        /// mode 1's asks it of the raw row - the row its Rule2 incident's owner (UnifiedVoteHistory.Incidents). A duplicate in the
        /// same incident, equal in grade and later by id, owns nothing: the juror never asks about it, and a saved question that
        /// names it is refused (the lead's decision D7).
        /// </summary>
        [TestCase(1)] [TestCase(2)]
        public void AJurorsReceiptIsTheCanonicalVoteOwnerNeverItsDuplicate(int scenario)
        {
            var witness = Source(scenario); var s = Candidate(witness); Check(s, true);
            var row = Selected(s, witness); var q = s.juryExchanges[s.juryQuestionIndex];
            Assert.That(FinaleQuestions.Receipts(s, q.questionerId).Single(item => item.category == FinaleQuestions.Accountability).id, Is.EqualTo(row.id),
                "The juror's receipt is the canonical Vote row, as mode 1's is the raw one.");
            Assert.That(Receipt(witness.State, q.questionerId), Is.EqualTo(Receipt(s, q.questionerId)), "Mode 2's receipt is mode 1's.");

            // A duplicate: the same duty, decided by the same ballot, an id after the owner's.
            var duplicate = row.Clone();
            int next = s.nextSequence;
            string prefix = row.id.Substring(0, row.id.LastIndexOf('-') + 1);
            while (string.CompareOrdinal(prefix + next, row.id) <= 0 || s.unifiedCommitments.Any(item => item.id == prefix + next)) next++;
            duplicate.id = prefix + next; s.nextSequence = next + 1;
            if (duplicate.status == DealStatus.Broken) duplicate.settlementEffectKey = UnifiedVoteHistory.Key(duplicate, duplicate.settledWeek);
            s.unifiedCommitments.Add(duplicate);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveVoteStorage(s, out var storage), Is.True, storage);
            var incident = UnifiedVoteHistory.Incidents(s).Single(item => item.EvidenceIds.Contains(row.id));
            Assert.That(incident.EvidenceIds, Is.EquivalentTo(new[] { row.id, duplicate.id }), "Fixture: one incident of two rows.");
            Assert.That(incident.OwnerId, Is.EqualTo(row.id), "Fixture: equal in grade, the smaller id owns it.");
            Assert.That(FinaleQuestions.Receipts(s, q.questionerId).Single(item => item.category == FinaleQuestions.Accountability).id, Is.EqualTo(row.id),
                "The juror asks about the owner, never the duplicate met after it.");
            Check(s, true);
            q.receiptId = duplicate.id;
            Check(s, false);
            Assert.That(Trace(witness.State), Is.EqualTo(witness.SourceImage));
        }

        /// <summary>
        /// The pre-V6 save-contract review of D7: an Accountability receipt is the owner of the player's breach of the juror, not of
        /// any group. One ballot of the player's broke their vote promise to the juror and the vote deal they struck, which the
        /// juror's ballot broke too: the promise, the heavier, owns the player's breach of the juror, and the deal - broken by both -
        /// owns only the juror's breach of the player. The juror asks about the promise, as mode 1's asks; a saved question naming
        /// the deal is refused; and the receipts never pick the deal, even where they reach it before the promise (a constructed
        /// reader case: the promise dated a week earlier, which only the storage check reads).
        /// </summary>
        [Test]
        public void AnAccountabilityReceiptOwnsThePlayersBreachOfTheJurorNotTheJurorsOfThePlayer()
        {
            var witness = Source(5); var s = Candidate(witness); Check(s, true);
            var deal = Selected(s, witness); var q = s.juryExchanges[s.juryQuestionIndex];
            string player = s.playerId, juror = q.questionerId, word = witness.Receipt.id;
            Assert.That((deal.status, deal.brokenById), Is.EqualTo((DealStatus.Broken, (string)null)), "Fixture: both of them broke the deal.");
            Assert.That(FinalistRead.DealBreaker(s, CommitmentReferences.FindDeal(s, deal.id)), Is.EqualTo(player),
                "Fixture: the player's own ballot broke it, so the player can name themself its breaker.");
            var incidents = UnifiedVoteHistory.Incidents(s).Where(item => item.Week == deal.settledWeek).ToList();
            var mine = incidents.Single(item => item.ActorId == player && item.WrongedId == juror);
            var theirs = incidents.Single(item => item.ActorId == juror && item.WrongedId == player);
            Assert.That(mine.EvidenceIds, Is.EquivalentTo(new[] { word, deal.id }), "Fixture: the player's breach of the juror holds both rows.");
            Assert.That(mine.OwnerId, Is.EqualTo(word), "Fixture: the promise, the heavier, owns it.");
            Assert.That(theirs.OwnerId, Is.EqualTo(deal.id), "Fixture: the deal owns the juror's breach of the player alone.");
            Assert.That(FinaleQuestions.Receipts(s, juror).Single(item => item.category == FinaleQuestions.Accountability).id, Is.EqualTo(word));
            Assert.That(Receipt(witness.State, juror), Is.EqualTo(Receipt(s, juror)), "Mode 2's receipts are mode 1's.");

            var named = s.Clone(); var question = named.juryExchanges[named.juryQuestionIndex];
            SelectQuestion(named, question, FinaleQuestions.Accountability, FinaleQuestions.DealReceipt, deal.id, deal.settledWeek);
            Assert.That(UnifiedVoteFamilyValidation.TryValidate(named, named.unifiedVoteReveals, out var familyError), Is.True, familyError);
            Check(named, false);

            // The reader: reach the deal first by dating the word a week earlier; the deal is still not the player's receipt.
            var earlier = s.Clone();
            earlier.unifiedCommitments.Single(item => item.id == word).createdWeek--;
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveVoteStorage(earlier, out var storage), Is.True, storage);
            Assert.That(UnifiedVoteHistory.Incidents(earlier).Single(item => item.ActorId == player && item.WrongedId == juror && item.Week == deal.settledWeek).OwnerId,
                Is.EqualTo(word), "Fixture: the same owners.");
            Assert.That(FinaleQuestions.Receipts(earlier, juror).Single(item => item.category == FinaleQuestions.Accountability).id, Is.EqualTo(word),
                "The deal both broke is never the player's receipt.");
            Assert.That(Trace(witness.State), Is.EqualTo(witness.SourceImage));
        }

        /// <summary>A question is prepared with exactly two draws in both modes (vote family V5e), and to the same receipt where no group holds two rows.</summary>
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void PreparingAQuestionDrawsTwoRollsInBothModes(int scenario)
        {
            var witness = Source(scenario);
            var prepared = new List<string>();
            foreach (var s in new[] { witness.State.Clone(), Candidate(witness) })
            {
                var q = s.juryExchanges[s.juryQuestionIndex];
                int draws = 0;
                var entry = new JuryExchangeState { questionerId = q.questionerId, finalistId = q.finalistId };
                FinaleQuestions.Prepare(s, s.Find(q.questionerId), s.juryQuestionIndex, entry, () => { draws++; return 0.25; });
                Assert.That(draws, Is.EqualTo(2), "Two draws, the category and the wording.");
                prepared.Add(JsonConvert.SerializeObject(entry));
            }
            Assert.That(prepared[1], Is.EqualTo(prepared[0]), "Mode 2 prepares mode 1's question.");
        }

        private static string Receipt(EpisodeState s, string juror) =>
            JsonConvert.SerializeObject(FinaleQuestions.Receipts(s, juror));

        private sealed class Witness
        {
            internal EpisodeState State;
            internal string SelectedId, ActualActor, OtherParty, SourceImage, Construction;
            internal FinaleQuestions.Receipt Receipt;
            internal List<UnifiedVoteRevealState> Frames;
            internal Dictionary<string, ProspectiveVoteOwner> Tracked;
        }
        private static readonly Dictionary<int, Witness> Cache = new Dictionary<int, Witness>();
        private static readonly Dictionary<int, string> FailedSearches = new Dictionary<int, string>();
        private const int CommandBound = 512;

        /// <summary>
        /// The witness for a scenario: seeds 1..32 in order, each constructed by <see cref="Build"/>,
        /// the first success cached. A seed whose lawful choices cannot reach the setup (no ordinary
        /// vote week, every partner declined, the player out before the final two) is a miss with its
        /// reason; a completed miss fails every dependent case with the same retained reasons.
        /// </summary>
        private static Witness Source(int scenario)
        {
            if (Cache.TryGetValue(scenario, out var cached)) return cached;
            if (FailedSearches.TryGetValue(scenario, out var failed)) { Assert.Fail(failed); return null; }
            var misses = new List<string>();
            for (uint seed = 1; seed <= 32; seed++)
            {
                var found = Build(scenario, seed, out string miss);
                if (found != null)
                {
                    TestContext.Out.WriteLine("Jury witness " + scenario + ": " + found.Construction + " (" + misses.Count + " earlier seeds missed).");
                    Cache.Add(scenario, found); return found;
                }
                misses.Add("seed=" + seed + ": " + miss);
            }
            string failure = "No constructed jury witness " + scenario + " within seeds 1..32 x" + CommandBound
                + " accepted public commands; no fabricated receipt or skipped case.\n" + string.Join("\n", misses);
            FailedSearches.Add(scenario, failure); Assert.Fail(failure);
            return null;
        }

        /// <summary>
        /// One constructed season. A: the first Campaign where the player is an ordinary voter among
        /// at least four, with two seats left; until then the player competes at no effort (their own
        /// lawful input), so the first week can be it. B: the player's real promise or proposal to the
        /// first ordinal eligible partner (not in a pact with the player; for scenario 3 not one who
        /// would deflect the question; scenario 2's partner is a nominee, as staged), the next one after
        /// a decline while seats remain, and the next such week where nobody took it. C: scenario 3
        /// asks the partner for real after the batch. D: the week's NPC ballots are pinned to the
        /// designed split and the player casts a real ballot. E: the player competes at full effort,
        /// with lawful choices and the same pinning for later votes that put the partner or the player
        /// on the block, until the partner questions the player. Guards G1-G5 assert what the
        /// construction promises; a failed guard is a fixture defect.
        /// </summary>
        private static Witness Build(int scenario, uint seed, out string miss)
        {
            var walk = new PinnedVoteSeason(seed, Fresh(seed));
            bool promise = scenario < 2;
            // 5: the player's vote deal to evict the first nominee with an ordinary voter, and the player's vote promise to
            // them on the same nominee; both of them vote the other one out (the pre-V6 save-contract review's direction).
            string type = scenario == 2 || scenario == 5 ? DealKind.VoteEvict : DealKind.VoteTogether;
            string selected = null, partner = null, word = null; int week = 0; var refusals = new List<string>();
            while (selected == null)
            {
                // A. The vote week: the next one, where the last found no partner.
                int tried = week;
                if (!WalkTo(walk, null, 0, s => s.week > tried && s.pendingDiary == null && s.phase == EpisodePhase.Campaign
                        && PinnedVoteSeason.PlayerVotes(s) && EpisodeEngine.Voters(s).Count() >= 4 && Seats(s) >= 2,
                        "an ordinary-voter Campaign with two seats" + (refusals.Count > 0 ? " (" + string.Join("; ", refusals) + ")" : ""), out miss))
                    return null;
                var campaign = walk.State; week = campaign.week;

                // B. The agreement, made by the player's real command.
                var pool = (scenario == 2 ? campaign.nominees.AsEnumerable() : PinnedVoteSeason.NpcVoters(campaign))
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .Where(id => !campaign.Allied(campaign.playerId, id) && !EpisodeEngine.SharesIntel(campaign, id)
                        && (scenario != 3 || !Deflects(campaign, id)))
                    .ToList();
                if (pool.Count == 0) refusals.Add("week " + week + ": no eligible partner");
                foreach (string id in pool)
                {
                    var s = walk.State;
                    if (Seats(s) < (scenario == 5 ? 2 : 1)) { refusals.Add("week " + week + ": no seat left"); break; }
                    string target = promise || scenario == 5 ? s.nominees[0] : scenario == 2 ? id : null;
                    if (promise ? s.promises.Any(row => row.kind == PromiseKind.Vote && row.fromId == s.playerId && row.toId == id && row.status == PromiseStatus.Active)
                        : !PlayerDeals.CanPropose(s, id, type, target, out _)) { refusals.Add("week " + week + ": " + id + " not offerable"); continue; }
                    var command = EpisodeEngineTests.Command(s, promise ? EpisodeCommandKind.PromiseVote : EpisodeCommandKind.ProposeDeal);
                    command.id = "jury-source-" + scenario + "-" + seed + "-" + s.revision + "-" + id;
                    command.targetId = id; command.secondTargetId = target; command.text = promise ? null : type;
                    var result = walk.Apply(command);
                    if (!result.accepted) { refusals.Add("week " + week + ": " + id + ": " + result.reason); continue; }
                    string created = (promise ? "promise-" : "deal-player-") + s.nextSequence;
                    if (promise ? result.state.promises.Any(row => row.id == created && row.status == PromiseStatus.Active)
                        : result.state.deals.Any(row => row.id == created && row.type == type && row.status == DealStatus.Active))
                    {
                        selected = created; partner = id;
                        if (scenario == 5)
                        {
                            // The player's word to the same partner on the same nominee, by the real command.
                            var pledge = EpisodeEngineTests.Command(result.state, EpisodeCommandKind.PromiseVote);
                            pledge.id = "jury-source-word-" + seed + "-" + result.state.revision; pledge.targetId = id; pledge.secondTargetId = target;
                            var pledged = walk.Step(pledge);
                            word = "promise-" + result.state.nextSequence;
                            Assert.That(pledged.promises.Count(row => row.id == word && row.kind == PromiseKind.Vote && row.status == PromiseStatus.Active
                                && row.toId == id && row.targetId == target), Is.EqualTo(1), "The player's real vote promise to the partner.");
                        }
                        break;
                    }
                    refusals.Add("week " + week + ": " + id + " declined");
                }
            }

            // C. To the open vote, and the real batch; scenario 3's real question after it.
            if (!WalkTo(walk, null, 0, PinnedVoteSeason.OpenVote, "week " + week + "'s open vote", out miss)) return null;
            Assert.That(walk.State.week, Is.EqualTo(week), "The open vote is the agreement's week.");
            walk.RunNpcBatch();
            var box = walk.State; string x, y;
            if (scenario == 3)
            {
                if (!VoteRead.Available(box) || EpisodeEngine.AskedThisWeek(box, partner) || Deflects(box, partner))
                { miss = "week " + week + ": the partner cannot be asked, or would deflect, after the batch"; return null; }
                var ask = EpisodeEngineTests.Command(box, EpisodeCommandKind.AskVote);
                ask.id = "jury-real-told-" + seed + "-" + box.revision; ask.targetId = partner;
                box = walk.Step(ask);
                var told = box.ledger.claims.Where(claim => claim.week == week && claim.voterId == partner && claim.source == ClaimSource.Told).ToList();
                Assert.That(told, Has.Count.EqualTo(1), "The real question wrote the partner's one told claim.");
                x = told[0].targetId;
            }
            else x = scenario == 2 ? box.nominees.First(id => id != partner) : box.nominees[0];
            y = box.nominees.First(id => id != x);

            // D. The designed split, pinned on the batch's ballots; then the player's real ballot and the real reveal.
            var npc = PinnedVoteSeason.NpcVoters(box).OrderBy(id => id, StringComparer.Ordinal).ToList();
            string other = npc.First(id => id != partner);
            var targets = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string voter in npc)
                targets[voter] = scenario == 3 ? (voter == partner || voter == other ? x : y)
                    : scenario == 4 ? (voter == partner || voter == other ? y : x)
                    : scenario == 5 ? (voter == partner ? y : x)
                    : scenario == 2 ? partner : x;
            string own = scenario == 1 || scenario == 5 ? y : x;
            walk.PinCast(targets);
            walk.CastVote(own);
            var revealed = walk.Reveal();
            Assert.That(revealed.week, Is.EqualTo(week));

            // G1: the count is the designed split; the partner and "other" voted as designed.
            var power = revealed.ledger.power.Single(row => row.week == week);
            var designed = power.nominees.Select(id => targets.Values.Count(t => t == id) + (own == id ? 1 : 0)).ToList();
            Assert.That(power.tally, Is.EqualTo(designed), "G1: the week's count is the designed split.");
            if (scenario == 2) Assert.That(power.nominees, Does.Contain(partner), "G1: scenario 2's partner is on the block.");
            else Assert.That(revealed.votes.Single(ballot => ballot.voterId == partner).targetId, Is.EqualTo(targets[partner]),
                "G1: the partner is an ordinary voter who voted as designed.");
            Assert.That(revealed.votes.Single(ballot => ballot.voterId == other).targetId, Is.EqualTo(targets[other]), "G1: the other voter voted as designed.");
            Assert.That(revealed.votes.Single(ballot => ballot.voterId == revealed.playerId).targetId, Is.EqualTo(own));

            // G2 / G3: what the player knows of the partner's ballot.
            var sheet = KnownBallots.Read(revealed, week).ballots.SingleOrDefault(ballot => ballot.voterId == partner);
            if (scenario == 3)
            {
                var told = revealed.ledger.claims.Where(claim => claim.week == week && claim.voterId == partner && claim.source == ClaimSource.Told).ToList();
                Assert.That(told, Has.Count.EqualTo(1), "G2: exactly one told claim for the week and the partner.");
                Assert.That(told[0].status, Is.EqualTo(ClaimStatus.Kept), "G2: the source judged the claim kept.");
                Assert.That(sheet?.basis, Is.EqualTo(KnownBallots.Basis.Told), "G2: the partner's ballot is known as told, never proven or revealed.");
                var lost = revealed.Clone(); lost.ledger.claims.RemoveAll(claim => claim.week == week && claim.voterId == partner);
                Assert.That(KnownBallots.Knows(lost, week, partner), Is.False, "G2: the claim is necessary.");
            }
            if (scenario == 4)
            {
                Assert.That(revealed.ledger.claims.Any(claim => claim.week == week && claim.voterId == partner), Is.False, "G3: no claim for the partner.");
                Assert.That(KnownBallots.Knows(revealed, week, partner), Is.False, "G3: the partner's ballot is unknown.");
                Assert.That(sheet, Is.Null, "G3: the partner's ballot is an unknown slot.");
            }

            // G4: the source wrote the agreement's verdict.
            string status, breaker;
            if (promise)
            {
                var row = revealed.promises.Single(item => item.id == selected);
                status = row.status == PromiseStatus.Fulfilled ? DealStatus.Fulfilled : row.status == PromiseStatus.Broken ? DealStatus.Broken : row.status.ToString();
                breaker = row.brokenById; Assert.That(row.settledWeek, Is.EqualTo(week), "G4: settled in the vote week.");
            }
            else
            {
                var row = revealed.deals.Single(item => item.id == selected);
                status = row.status; breaker = row.brokenById; Assert.That(row.settledWeek, Is.EqualTo(week), "G4: settled in the vote week.");
            }
            Assert.That(status, Is.EqualTo(scenario == 0 || scenario == 3 ? DealStatus.Fulfilled : DealStatus.Broken), "G4: the designed verdict.");
            Assert.That(breaker, Is.EqualTo(scenario == 1 || scenario == 2 ? revealed.playerId : null), "G4: the designed breaker.");
            if (scenario == 5)
            {
                var pledge = revealed.promises.Single(item => item.id == word);
                Assert.That((pledge.status, pledge.brokenById, pledge.settledWeek), Is.EqualTo((PromiseStatus.Broken, revealed.playerId, week)),
                    "G4: the player's own ballot broke their word as well.");
            }

            string category = scenario == 0 || scenario == 3 ? FinaleQuestions.Personal : FinaleQuestions.Accountability;
            // Scenario 5's juror asks about the player's word, which owns the player's breach of them (see its test).
            var receipt = scenario == 4 ? null
                : FinaleQuestions.Receipts(revealed, partner).FirstOrDefault(item => item.id == (scenario == 5 ? word : selected) && item.category == category);
            if (scenario != 4 && receipt == null)
            { miss = "week " + week + ": no source-returned " + category + " receipt for the selected owner"; return null; }

            // E. To the partner's question to the player.
            if (!WalkTo(walk, partner, 1, s => s.phase == EpisodePhase.JuryQuestioning && s.pendingDiary == null
                    && !s.juryExchanges[s.juryQuestionIndex].completed && s.juryExchanges[s.juryQuestionIndex].finalistId == s.playerId
                    && s.juryExchanges[s.juryQuestionIndex].questionerId == partner, "the partner's question to the player", out miss)) return null;
            var before = walk.State;
            // G5: the player is an active finalist, the partner a juror, and the partner is asking.
            var pending = before.juryExchanges[before.juryQuestionIndex];
            Assert.That(before.Find(before.playerId).status, Is.EqualTo(ContestantStatus.Active), "G5: the player is a finalist.");
            Assert.That(before.Find(partner).status, Is.EqualTo(ContestantStatus.Jury), "G5: the partner is on the jury.");
            Assert.That(pending.questionerId, Is.EqualTo(partner)); Assert.That(pending.finalistId, Is.EqualTo(before.playerId));
            if (!walk.Supported) { miss = "an actual birth this observer cannot order: " + walk.FirstUnsupported; return null; }

            var projected = Project(before, walk.Owners, walk.Frames);
            if (!UnifiedVoteFamilyValidation.TryValidate(projected, projected.unifiedVoteReveals, out var familyError))
            { miss = "family: " + familyError; return null; }
            var projectedRow = projected.unifiedCommitments.Single(item => item.id == selected);
            var decision = UnifiedVoteHistory.FindDecision(projected, selected);
            string otherParty = OtherParty(projected, projectedRow, walk.Frames);
            var reasons = new List<string>();
            if (decision == null) reasons.Add("no qualifying decision in the complete frames");
            if (otherParty == null && scenario != 5) reasons.Add("no alternative party preserves the agreement-local verdict");
            if (scenario == 3 && !ClaimIsNecessary(projected, projectedRow)) reasons.Add("the told claim is absent or unnecessary");
            if (scenario == 4 && (KnownBallots.Knows(projected, projectedRow.settledWeek, partner) || decision?.ActorId != null))
                reasons.Add("the partner is known or the decision has an actor");
            if (reasons.Count > 0) { miss = string.Join("; ", reasons); return null; }
            // Decision ownership and breach attribution are different source facts:
            // Promise decisions name the maker even when kept; kept brokenById stays null.
            string actualActor = promise ? before.promises.Single(item => item.id == selected).fromId : projectedRow.brokenById;
            var found = new Witness { State = before.Clone(), SelectedId = selected, ActualActor = actualActor,
                OtherParty = otherParty, SourceImage = Trace(before), Frames = walk.Frames.Select(frame => frame.Clone()).ToList(),
                Tracked = walk.Owners.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal), Receipt = receipt };
            if (!ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(scenario == 4 ? projected : Candidate(found), out var coreError))
            { miss = "whole core: " + coreError; return null; }
            found.Construction = "seed " + seed + ", vote week " + week + ", partner " + partner + ", " + walk.Pins.Count
                + " pinned ballots, " + walk.Accepted + " accepted commands";
            miss = null;
            return found;
        }

        /// <summary>Plays steered public commands until <paramref name="until"/> holds; false with a reason when it cannot.</summary>
        private static bool WalkTo(PinnedVoteSeason walk, string partner, double effort, Func<EpisodeState, bool> until, string what, out string miss)
        {
            while (walk.Accepted < CommandBound)
            {
                var s = walk.State;
                if (until(s)) { miss = null; return true; }
                if (s.phase == EpisodePhase.Finished || s.Find(s.playerId).status != ContestantStatus.Active)
                { miss = what + " not reached: the season ended or the player left it (week " + s.week + ", " + s.phase + ")"; return false; }
                if (partner != null && s.Find(partner).status == ContestantStatus.Active && s.Active.Count() <= 2)
                { miss = what + " not reached: the partner reached the final two"; return false; }
                Steer(walk, partner, effort);
            }
            miss = what + " not reached within " + CommandBound + " accepted commands";
            return false;
        }

        /// <summary>
        /// One steered step. An open vote with the partner on the block evicts the partner; one with the
        /// player on it evicts the other nominee; both by pinned NPC ballots. Anything else is the
        /// ordinary next command with the scenario's lawful choices.
        /// </summary>
        private static void Steer(PinnedVoteSeason walk, string partner, double effort)
        {
            var s = walk.State;
            if (PinnedVoteSeason.OpenVote(s))
            {
                if (partner != null && s.nominees.Contains(partner)) { walk.PlayPinnedVote(partner); return; }
                if (s.nominees.Contains(s.playerId)) { walk.PlayPinnedVote(s.nominees.First(id => id != s.playerId)); return; }
            }
            walk.Step(Next(s, partner, effort));
        }

        private static int Seats(EpisodeState s) => EpisodeEngine.SocialActionBudget(s) - EpisodeEngine.SocialActionsSpent(s);

        /// <summary>The source's own deflection test for a question about the vote (EpisodeEngine.AskVote).</summary>
        private static bool Deflects(EpisodeState s, string id)
        {
            var person = s.Find(id);
            return !EpisodeEngine.SharesIntel(s, id) && person.traits.Contains("Strategic") && !person.traits.Contains("Loyal")
                && s.Score(id, s.playerId) < 25;
        }

        private static EpisodeState Fresh(uint seed) => PinnedVoteSeason.Fresh(seed);

        private static EpisodeState Project(EpisodeState source, Dictionary<string, ProspectiveVoteOwner> tracked, List<UnifiedVoteRevealState> frames) =>
            PinnedVoteSeason.Project(source, tracked, frames);

        private static EpisodeState Candidate(Witness witness)
        {
            var s = Project(witness.State, witness.Tracked, witness.Frames);
            var q = s.juryExchanges[s.juryQuestionIndex];
            Assert.That(q.completed, Is.False); Assert.That(q.finalistId, Is.EqualTo(s.playerId));
            var row = Selected(s, witness); Assert.That(q.questionerId, Is.EqualTo(row.beneficiaryId));
            SelectQuestion(s, q, witness.Receipt.category, witness.Receipt.kind, witness.Receipt.id, witness.Receipt.week);
            return s;
        }
        private static void SelectQuestion(EpisodeState s, JuryExchangeState q, string category, string kind, string id, int week)
        {
            q.category = category; q.receiptKind = kind; q.receiptId = id; q.tone = FinaleQuestions.Tone(category);
            q.question = FinaleQuestions.QuestionsFor(s, category, new FinaleQuestions.Receipt { category = category, kind = kind, id = id, week = week }, q.questionerId)[0];
        }
        private static UnifiedCommitmentState Selected(EpisodeState s, Witness witness) => s.unifiedCommitments.Single(row => row.id == witness.SelectedId);
        private static string OtherParty(EpisodeState s, UnifiedCommitmentState row, List<UnifiedVoteRevealState> frames)
        {
            if (row.settledWeek == 0) return null;
            IEnumerable<string> candidates;
            if (row.sourcePolicy == UnifiedCommitments.PromisePolicy)
                candidates = s.contestants.Where(person => person.id != s.playerId && person.id != row.beneficiaryId
                    && s.ledger.power.Single(power => power.week == row.settledWeek).nominees.Contains(person.id)).Select(person => person.id);
            else if (row.subtype != DealKind.VoteTogether)
                candidates = s.ledger.power.Single(item => item.week == row.settledWeek).nominees.Where(id => id != row.beneficiaryId);
            else
            {
                var frame = frames.Single(item => item.week == row.settledWeek);
                string own = frame.ballots.Single(ballot => ballot.voterId == s.playerId).targetId;
                candidates = frame.ballots.Where(ballot => ballot.voterId != s.playerId && ballot.voterId != row.beneficiaryId
                    && (row.status == DealStatus.Fulfilled ? ballot.targetId == own : ballot.targetId != own))
                    .Select(ballot => ballot.voterId);
            }
            foreach (string id in candidates)
            {
                var altered = s.Clone(); var changed = altered.unifiedCommitments.Single(item => item.id == row.id);
                changed.beneficiaryId = id;
                if (changed.status == DealStatus.Broken) changed.settlementEffectKey = UnifiedVoteHistory.Key(changed, changed.settledWeek);
                if (UnifiedVoteFamilyValidation.TryValidate(altered, altered.unifiedVoteReveals, out _)
                    && UnifiedVoteHistory.FindDecision(altered, row.id)?.Status == row.status) return id;
            }
            return null;
        }
        private static bool ClaimIsNecessary(EpisodeState s, UnifiedCommitmentState row)
        {
            if (!KnownBallots.Knows(s, row.settledWeek, row.beneficiaryId)) return false;
            var lost = s.Clone();
            int removed = lost.ledger.claims.RemoveAll(claim => claim.week == row.settledWeek && claim.voterId == row.beneficiaryId);
            return removed > 0 && !KnownBallots.Knows(lost, row.settledWeek, row.beneficiaryId);
        }

        /// <summary>
        /// The next command, with the scenario's lawful choices: the player competes at the given effort
        /// and uses a veto on themself; with a partner chosen, the player as Head of Household nominates
        /// them (or names them as the replacement), keeps them on the block with a veto the player
        /// holds, and evicts them as the final Head of Household.
        /// </summary>
        private static EpisodeCommand Next(EpisodeState s, string partner, double effort)
        {
            var command = EpisodeEngineTests.NextCommand(s);
            if (command.kind == EpisodeCommandKind.Compete) command.performance = effort;
            bool partnerActive = partner != null && s.Find(partner)?.status == ContestantStatus.Active;
            if (s.pendingDiary == null && s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved)
            {
                if (s.vetoHolderId == s.playerId && s.nominees.Contains(s.playerId) && !EpisodeEngine.VetoIsLockedAtFinalFour(s)
                    && EpisodeEngine.ReplacementCandidates(s).Any())
                { command.kind = EpisodeCommandKind.ResolveVeto; command.useVeto = true; command.targetId = s.playerId; command.secondTargetId = null; }
                else if (partnerActive && s.vetoHolderId == s.playerId && s.nominees.Contains(partner))
                { command.kind = EpisodeCommandKind.ResolveVeto; command.useVeto = false; command.secondTargetId = null; }
                else if (partnerActive && command.kind == EpisodeCommandKind.ResolveVeto && s.hohId == s.playerId && s.vetoHolderId != s.playerId
                    && command.useVeto && EpisodeEngine.ReplacementCandidates(s).Any(person => person.id == partner))
                    command.secondTargetId = partner;
            }
            if (partnerActive && command.kind == EpisodeCommandKind.Nominate && s.hohId == s.playerId)
            {
                var candidates = EpisodeEngine.NominationCandidates(s).ToArray();
                if (candidates.Any(person => person.id == partner) && candidates.Any(person => person.id != partner))
                { command.targetId = partner; command.secondTargetId = candidates.First(person => person.id != partner).id; }
            }
            if (partnerActive && command.kind == EpisodeCommandKind.FinalEvict && s.hohId == s.playerId && partner != s.playerId)
                command.targetId = partner;
            if (command.kind == EpisodeCommandKind.AnswerJury && EpisodeEngine.FinaleOn(s))
            {
                var q = s.juryExchanges[s.juryQuestionIndex];
                command.secondTargetId = q.finalistId == s.playerId ? FinaleQuestions.Offered(q.category, q.receiptKind)[0]
                    : WebJuryQuestioning.GetJurorQuestionOptions(s.juryQuestionIndex)[0].tone;
            }
            return command;
        }
        private static void Check(EpisodeState s, bool expected)
        {
            string before = Trace(s);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(s, out var error), Is.EqualTo(expected), error);
            if (!expected) Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(Trace(s), Is.EqualTo(before));
        }
        private static string Trace(EpisodeState s) => JsonConvert.SerializeObject(s);
    }
}
