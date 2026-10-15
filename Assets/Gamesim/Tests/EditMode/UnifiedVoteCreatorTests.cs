using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Vote family V3: every creator of a Vote promise or deal writes a canonical row through the mode-2
    /// store, through the internal engine seam. Each case drives the real command - or, for a story's
    /// effect and a second house pass, the real owner, called as the engine calls it - on a season's
    /// mode-1 copy and on its exact mode-2 twin, and requires the mode-2 result to be the mode-1 result
    /// with its Vote rows canonical: the same ids, sequence, draws, lines, scores, memories and ledger.
    /// Duplicates and capacity are refused whole: nothing spent, drawn, minted or logged.
    ///
    /// <para>A command is atomic, so where in it a refusal falls is not observable here. The reservations a
    /// creator makes before its draw are backstops behind the source's own gates, which in valid play refuse
    /// everything the admission would; only the house's and the story's skips show an order, before the
    /// ledger line (ADuplicateHouseBargainIsRefusedBeforeItsLedgerLine).</para>
    ///
    /// <para>Fixtures are seasons walked with real commands to a moment before the first reveal; the only
    /// constructed facts are a relationship score, the season's next draw, the sequence a keyed coin reads
    /// and rows filed as their owners file them (<see cref="ProspectiveVoteTwins"/>), with the exceptions
    /// that fixture's summary names. Settlement, the reveal's archive, endings and readers are later slices:
    /// nothing here crosses a reveal.</para>
    /// </summary>
    public sealed class UnifiedVoteCreatorTests
    {
        // ------------------------------------------------------------ fixtures

        private static EpisodeState voter, nominee, vetoHolder, houseHoH;

        /// <summary>A first campaign in which the player is an ordinary voter, nothing of theirs spent.</summary>
        private static EpisodeState Voter() => (voter ??= ProspectiveVoteTwins.Find("a first campaign the player votes in",
            seed => ProspectiveVoteTwins.Walk(seed, s => s.phase == EpisodePhase.Campaign && Votes(s)))).Clone();

        /// <summary>A first campaign with the player on the block.</summary>
        private static EpisodeState Nominee() => (nominee ??= ProspectiveVoteTwins.Find("a first campaign with the player on the block",
            seed => ProspectiveVoteTwins.Walk(seed, s => s.phase == EpisodePhase.Campaign && s.nominees.Contains(s.playerId)))).Clone();

        /// <summary>A first veto meeting the player holds the veto at, undecided, with a houseguest on the block.</summary>
        private static EpisodeState VetoHolder() => (vetoHolder ??= ProspectiveVoteTwins.Find("a first veto meeting the player holds the veto at",
            seed => ProspectiveVoteTwins.Walk(seed, s => s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved && s.vetoHolderId == s.playerId
                && s.nominees.Any(id => id != s.playerId), EpisodePhase.Veto))).Clone();

        /// <summary>
        /// A first veto meeting, decided, with a houseguest Head of Household and the player an ordinary voter: the
        /// house's campaign passes are next, and they court the Head of Household with safety too (vote family V3b).
        /// </summary>
        private static EpisodeState HouseHoH() => (houseHoH ??= ProspectiveVoteTwins.Find("a first decided veto meeting with a houseguest Head of Household",
            seed => ProspectiveVoteTwins.Walk(seed, s => s.phase == EpisodePhase.VetoMeeting && s.vetoResolved && s.hohId != s.playerId
                && !s.nominees.Contains(s.playerId)))).Clone();

        private static bool Votes(EpisodeState s) => EpisodeEngine.Voters(s).Any(v => v.isPlayer);
        private static List<string> NpcVoters(EpisodeState s) => EpisodeEngine.Voters(s).Where(v => !v.isPlayer).Select(v => v.id).ToList();
        private static string Name(EpisodeState s, string id) => s.Find(id).name;
        private static IEnumerable<UnifiedCommitmentState> Rows(EpisodeState s, string origin) =>
            s.unifiedCommitments.Where(row => row.kind == UnifiedVoteTogether.Vote && row.origin == origin);
        private static void NoRawVoteRows(EpisodeState s)
        {
            Assert.That(s.promises.Any(p => p.kind == PromiseKind.Vote), Is.False, "No raw Vote promise under mode 2.");
            Assert.That(s.deals.Any(d => KnownBallots.IsVoteDeal(d.type)), Is.False, "No raw Vote deal under mode 2.");
        }

        /// <summary>A row bound now, judged first at this week's reveal, as an immediate owner binds it.</summary>
        private static void BoundNow(UnifiedCommitmentState row, int week, int expires)
        {
            Assert.That((row.status, row.createdWeek, row.expiresWeek, row.voteBindingWeek, row.voteFirstRevealWeek),
                Is.EqualTo((DealStatus.Active, week, expires, week, week)), row.id + ": bound now, judged first at this week's reveal.");
            Assert.That((row.settledWeek, row.brokenById, row.settlementEffectKey), Is.EqualTo((0, (string)null, (string)null)), row.id + ": unsettled.");
        }

        // ------------------------------------------------------------ the player's word

        /// <summary>
        /// "Promise my vote": a canonical player promise - the promise's own id, the player to the houseguest,
        /// the nominee named - and the source's own refusal of a word already given: refused whole, nothing written.
        /// </summary>
        [Test]
        public void APlayerVotePromiseIsACanonicalRowAndADuplicateIsRefusedWhole()
        {
            var s = Voter();
            string blocked = s.nominees[0], to = NpcVoters(s)[0];
            var (legacy, prospective) = ProspectiveVoteTwins.Both(s, ProspectiveVoteTwins.Command(s, EpisodeCommandKind.PromiseVote, to, blocked));
            Assert.That(legacy.accepted, Is.True, legacy.reason);
            Assert.That(prospective.accepted, Is.True, prospective.reason);
            var row = Rows(prospective.state, UnifiedCommitments.PlayerPromise).Single();
            Assert.That((row.id, row.sourcePolicy, row.makerId, row.beneficiaryId, row.targetId, row.reciprocal, row.subtype, row.trustImpact),
                Is.EqualTo(("promise-" + s.nextSequence, UnifiedCommitments.PromisePolicy, s.playerId, to, blocked, false, (string)null, DealTrust.Medium)));
            BoundNow(row, s.week, s.week);
            NoRawVoteRows(prospective.state);
            ProspectiveVoteTwins.AssertParity(legacy.state, prospective.state, "PromiseVote");

            var after = prospective.state;
            var again = ProspectiveVoteTwins.Command(after, EpisodeCommandKind.PromiseVote, to, blocked);
            var refused = ProspectiveVoteFacade.Engine(after).Apply(again);
            Assert.That(refused.reason, Is.EqualTo("This promise is already active."), "The source's own words,");
            ProspectiveVoteTwins.AssertUntouched(after, refused, "A second word on the vote");
            Assert.That(new EpisodeEngine(legacy.state).Apply(again).reason, Is.EqualTo(refused.reason), "as the mode-1 game refuses it.");
        }

        /// <summary>
        /// "Promise support" to a nominee's plea: the same player promise, reserved before the reply moves
        /// anything and written where the reply has always written it; a plea answered to somebody already
        /// promised writes no second word, in either game.
        /// </summary>
        [Test]
        public void APleaAnsweredWithAPromiseIsACanonicalPlayerPromise()
        {
            var s = Voter();
            var plea = s.replyCards.FirstOrDefault(card => card.kind == ReplyCards.Plea && s.nominees.Contains(card.fromId)
                && s.nominees.Contains(card.aboutId ?? ""));
            if (plea == null)
            {
                ReplyCards.Offer(s, ReplyCards.Plea, s.nominees[0], s.nominees[1]);
                plea = ProspectiveVoteTwins.Valid(s).replyCards.Last();
            }
            var (legacy, prospective) = ProspectiveVoteTwins.Both(s, ProspectiveVoteTwins.Command(s, EpisodeCommandKind.ReplyToHouseguest, plea.id, text: "promise"));
            Assert.That(legacy.accepted, Is.True, legacy.reason);
            Assert.That(prospective.accepted, Is.True, prospective.reason);
            var row = Rows(prospective.state, UnifiedCommitments.PlayerPromise).Single();
            Assert.That((row.makerId, row.beneficiaryId, row.targetId), Is.EqualTo((s.playerId, plea.fromId, plea.aboutId)));
            BoundNow(row, s.week, s.week);
            Assert.That(prospective.state.ledger.replies.Last().promised, Is.True);
            NoRawVoteRows(prospective.state);
            ProspectiveVoteTwins.AssertParity(legacy.state, prospective.state, "A plea's promise");

            // Promised already: the reply stands, and no second word is written.
            var twice = legacy.state;
            ReplyCards.Offer(twice, ReplyCards.Plea, plea.fromId, plea.aboutId);
            var second = ProspectiveVoteTwins.Valid(twice).replyCards.Last(card => card.fromId == plea.fromId);
            var (legacyAgain, prospectiveAgain) = ProspectiveVoteTwins.Both(twice,
                ProspectiveVoteTwins.Command(twice, EpisodeCommandKind.ReplyToHouseguest, second.id, text: "promise"));
            Assert.That(prospectiveAgain.accepted, Is.True, prospectiveAgain.reason);
            Assert.That(Rows(prospectiveAgain.state, UnifiedCommitments.PlayerPromise).Count(), Is.EqualTo(1));
            ProspectiveVoteTwins.AssertParity(legacyAgain.state, prospectiveAgain.state, "A plea answered twice");
        }

        // ------------------------------------------------------------ the player's deals

        /// <summary>
        /// A proposed vote deal the houseguest agrees to: written as a canonical player deal with the subtype and
        /// nominee proposed; proposing it again is refused in the deal table's words, whole, nothing written.
        /// </summary>
        [TestCase(DealKind.VoteTogether)] [TestCase(DealKind.VoteSave)] [TestCase(DealKind.VoteEvict)]
        public void AnAgreedVoteDealIsACanonicalPlayerDealAndTheSameDealAgainIsRefusedWhole(string type)
        {
            var s = Voter();
            string npc = NpcVoters(s)[0], about = type == DealKind.VoteTogether ? null : s.nominees[0];
            Assert.That(PlayerDeals.CanPropose(s, npc, type, about, out var why), Is.True, why);
            s.randomState = ProspectiveVoteTwins.Draw(true, PlayerDeals.AcceptanceChance(s, npc, type, about));
            var (legacy, prospective) = ProspectiveVoteTwins.Both(ProspectiveVoteTwins.Valid(s),
                ProspectiveVoteTwins.Command(s, EpisodeCommandKind.ProposeDeal, npc, about, type));
            Assert.That(legacy.accepted, Is.True, legacy.reason);
            Assert.That(prospective.accepted, Is.True, prospective.reason);
            var row = Rows(prospective.state, UnifiedCommitments.PlayerDeal).Single();
            Assert.That((row.id, row.sourcePolicy, row.subtype, row.makerId, row.beneficiaryId, row.targetId, row.reciprocal, row.linkedCommitmentId),
                Is.EqualTo(("deal-player-" + s.nextSequence, UnifiedCommitments.DealPolicy, type, s.playerId, npc, about, true, (string)null)));
            Assert.That(row.trustImpact, Is.EqualTo(DealKind.DefaultTrust(type)));
            BoundNow(row, s.week, s.week);
            NoRawVoteRows(prospective.state);
            // The lever line after a targeted vote deal reads the voter's obligations through the ballot reader, which reads
            // the canonical rows since vote family V5b: the whole command is mode 1's.
            ProspectiveVoteTwins.AssertParity(legacy.state, prospective.state, "ProposeDeal " + type);

            var after = prospective.state;
            var again = ProspectiveVoteTwins.Command(after, EpisodeCommandKind.ProposeDeal, npc, about, type);
            var refused = ProspectiveVoteFacade.Engine(after).Apply(again);
            Assert.That(refused.reason, Is.EqualTo("You already have that arrangement with " + Name(s, npc) + "."));
            ProspectiveVoteTwins.AssertUntouched(after, refused, "The same vote deal again");
            Assert.That(new EpisodeEngine(legacy.state).Apply(again).reason, Is.EqualTo(refused.reason));
        }

        /// <summary>
        /// The player's forty counts canonical rows: with one place left a vote deal takes it, and the next
        /// proposal of any kind is refused in the deal table's words, whole - in both games.
        /// </summary>
        [Test]
        public void ThePlayersFortyCountsCanonicalRowsAndTheNextProposalIsRefusedWhole()
        {
            var s = Voter();
            string npc = NpcVoters(s)[0], other = NpcVoters(s)[1];
            var lapsed = s.contestants.Where(c => !c.isPlayer).Select(c => c.id).Take(2).ToArray();
            int used = s.deals.Count + s.unifiedCommitments.Count(row => row.sourcePolicy == UnifiedCommitments.DealPolicy);
            // Constructed, and no owner files it: filler history that only fills the shelf - expired in its own
            // week, which no expiry writes (Expire ends a deal a week later), under an id no owner mints.
            for (int n = used; n < PlayerDeals.PlayerDealCeiling - 1; n++)
                s.deals.Add(new DealState { id = "deal-lapsed-" + n, type = DealKind.InformationSharing, proposerId = lapsed[0], recipientId = lapsed[1],
                    status = DealStatus.Expired, week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(DealKind.InformationSharing) });
            s.randomState = ProspectiveVoteTwins.Draw(true, PlayerDeals.AcceptanceChance(s, npc, DealKind.VoteTogether, null));
            var (legacy, prospective) = ProspectiveVoteTwins.Both(ProspectiveVoteTwins.Valid(s),
                ProspectiveVoteTwins.Command(s, EpisodeCommandKind.ProposeDeal, npc, null, DealKind.VoteTogether));
            Assert.That(prospective.accepted, Is.True, prospective.reason);
            Assert.That(Rows(prospective.state, UnifiedCommitments.PlayerDeal).Count(), Is.EqualTo(1), "The last place, taken by a canonical row.");
            ProspectiveVoteTwins.AssertParity(legacy.state, prospective.state, "The fortieth deal");

            var after = prospective.state;
            Assert.That(after.deals.Count, Is.LessThan(PlayerDeals.PlayerDealCeiling), "Raw rows alone leave room;");
            var next = ProspectiveVoteTwins.Command(after, EpisodeCommandKind.ProposeDeal, other, null, DealKind.Partnership);
            var refused = ProspectiveVoteFacade.Engine(after).Apply(next);
            Assert.That(refused.reason, Is.EqualTo("You already have more arrangements than you can keep track of."), "the canonical row is counted.");
            ProspectiveVoteTwins.AssertUntouched(after, refused, "A forty-first proposal");
            Assert.That(new EpisodeEngine(legacy.state).Apply(next).reason, Is.EqualTo(refused.reason));
        }

        /// <summary>
        /// A plea for their vote, made with a deal, that lands: the voter's word to keep the player is a
        /// canonical vote-lobby row - the voter's word, the player owed it and named by it.
        /// </summary>
        [Test]
        public void ALandedPleaForTheirVoteIsACanonicalVoteLobbyRow()
        {
            var s = Nominee();
            string decider = NpcVoters(s).First(id => StrategyRules.CanBeAskedForTheirVote(s, id));
            s.randomState = ProspectiveVoteTwins.Draw(true, StrategyRules.Chance(s, decider, LobbyAsk.Vote, s.playerId, LobbyApproach.Deal));
            var (legacy, prospective) = ProspectiveVoteTwins.Both(ProspectiveVoteTwins.Valid(s), ProspectiveVoteTwins.Command(s,
                EpisodeCommandKind.Lobby, decider, s.playerId, LobbyAsk.Encode(LobbyAsk.Vote, LobbyApproach.Deal)));
            Assert.That(legacy.accepted, Is.True, legacy.reason);
            Assert.That(prospective.accepted, Is.True, prospective.reason);
            var row = Rows(prospective.state, UnifiedVoteFamilyValidation.VoteLobby).Single();
            Assert.That((row.id.StartsWith("deal-lobby-", StringComparison.Ordinal), row.subtype, row.makerId, row.beneficiaryId, row.targetId),
                Is.EqualTo((true, DealKind.VoteSave, decider, s.playerId, s.playerId)));
            BoundNow(row, s.week, s.week);
            NoRawVoteRows(prospective.state);
            ProspectiveVoteTwins.AssertParity(legacy.state, prospective.state, "A landed vote plea");
        }

        // ------------------------------------------------------------ the house's word and bargains

        /// <summary>
        /// The house's campaign passes, through the real Advance into the campaign: a nominee's word on the
        /// vote, a nominee's vote bargain with a voter and a nominee's vote offer to the player are canonical
        /// rows - NPC promise, NPC deal and an unbound Proposed NPC offer - in the source's order and identity.
        /// </summary>
        [Test]
        public void TheHousesCampaignWordsBargainsAndOffersAreCanonicalRows()
        {
            var (legacy, prospective) = CampaignPasses();
            var after = prospective.state;
            var promises = Rows(after, UnifiedCommitments.NpcPromise).ToList();
            var deals = Rows(after, UnifiedCommitments.NpcDeal).ToList();
            var offers = Rows(after, UnifiedCommitments.NpcOffer).ToList();
            Assert.That(promises, Is.Not.Empty, "A nominee gave their word on the vote,");
            Assert.That(deals, Is.Not.Empty, "struck a vote bargain with a voter,");
            Assert.That(offers, Is.Not.Empty, "and put one to the player.");
            foreach (var row in promises)
            {
                Assert.That(after.nominees.Contains(row.makerId) && !after.nominees.Contains(row.beneficiaryId) && row.beneficiaryId != after.playerId, Is.True, row.id);
                Assert.That(row.targetId, Is.EqualTo(after.nominees.First(id => id != row.makerId)), row.id + " names the other nominee.");
                Assert.That(row.id, Does.StartWith("promise-npc-"));
                BoundNow(row, after.week, after.week);
            }
            foreach (var row in deals)
            {
                Assert.That((row.subtype, row.targetId), Is.EqualTo((DealKind.VoteSave, row.makerId)), row.id + ": a vote to keep the nominee asking.");
                Assert.That(after.nominees.Contains(row.makerId) && !after.nominees.Contains(row.beneficiaryId), Is.True, row.id);
                Assert.That(row.id, Does.StartWith("deal-npc-"));
                BoundNow(row, after.week, after.week);
            }
            foreach (var row in offers)
            {
                Assert.That((row.subtype, row.targetId, row.beneficiaryId, row.status), Is.EqualTo((DealKind.VoteSave, row.makerId, after.playerId, DealStatus.Proposed)), row.id);
                Assert.That((row.createdWeek, row.expiresWeek, row.voteBindingWeek, row.voteFirstRevealWeek), Is.EqualTo((after.week, after.week, 0, 0)),
                    row.id + ": a question, unbound until it is answered.");
                Assert.That(row.id, Does.StartWith(NpcDeals.OfferPrefix));
            }
            NoRawVoteRows(after);
            ProspectiveVoteTwins.AssertParity(legacy.state, after, "The campaign's house passes");
        }

        /// <summary>
        /// A second promise pass and a second offer round in the same week: the canonical rows already given
        /// are seen - nobody promises the same person twice, and the week's offers are not put again - so the
        /// mode-2 house does exactly what the mode-1 house does.
        /// </summary>
        [Test]
        public void TheHouseSeesItsCanonicalWordsAndOffersBeforeGivingMore()
        {
            var (legacy, prospective) = CampaignPasses();
            var legacyAgain = legacy.state.Clone(); var prospectiveAgain = prospective.state.Clone();
            NpcPromises.Settle(legacyAgain); NpcDeals.Propose(legacyAgain);
            NpcPromises.Settle(prospectiveAgain); NpcDeals.Propose(prospectiveAgain);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(prospectiveAgain, out var error), Is.True, error);
            Assert.That(Rows(prospectiveAgain, UnifiedCommitments.NpcOffer).Count(), Is.EqualTo(Rows(prospective.state, UnifiedCommitments.NpcOffer).Count()),
                "One round of offers a week.");
            var pairs = Rows(prospectiveAgain, UnifiedCommitments.NpcPromise).Select(row => row.makerId + ">" + row.beneficiaryId).ToList();
            Assert.That(pairs.Distinct().Count(), Is.EqualTo(pairs.Count), "Nobody's word to the same person twice.");
            ProspectiveVoteTwins.AssertParity(legacyAgain, prospectiveAgain, "A second house pass");
        }

        /// <summary>
        /// A nominee's vote bargain with a voter it already has: the mode-2 store refuses it, in its own words,
        /// and the house's strike (NpcDeals.Strike, as its pass calls it) writes nothing for it - not the row,
        /// and not its ledger line, which would spend the sequence - where the source's ladder would have written
        /// it twice. With another voter the strike writes both.
        /// </summary>
        [Test]
        public void ADuplicateHouseBargainIsRefusedBeforeItsLedgerLine()
        {
            var (_, prospective) = CampaignPasses();
            var s = prospective.state;
            var row = Rows(s, UnifiedCommitments.NpcDeal).First();
            var duplicate = new DealState { id = "deal-npc-" + s.nextSequence, type = DealKind.VoteSave, proposerId = row.makerId,
                recipientId = row.beneficiaryId, targetId = row.makerId, status = DealStatus.Active, week = s.week, expiresWeek = s.week,
                trustImpact = DealKind.DefaultTrust(DealKind.VoteSave) };
            string before = PinnedVoteSeason.Json(s);
            Assert.That(ProspectiveVoteFacade.StoreTryAddDeal(s, duplicate, UnifiedCommitments.NpcDeal, out var error), Is.False);
            Assert.That(error, Does.Contain("admits only its nominee's VoteSave"));
            Assert.That(PinnedVoteSeason.Json(s), Is.EqualTo(before), "Nothing written.");
            Strike(s, row.makerId, row.beneficiaryId);
            Assert.That(PinnedVoteSeason.Json(s), Is.EqualTo(before), "The strike writes no row and no ledger line for it.");

            string other = s.contestants.First(c => !c.isPlayer && !s.nominees.Contains(c.id) && c.id != row.beneficiaryId
                && !Rows(s, UnifiedCommitments.NpcDeal).Any(d => d.makerId == row.makerId && d.beneficiaryId == c.id)).id;
            int lines = Accepted(s, row.makerId, other);
            Strike(s, row.makerId, other);
            Assert.That(Rows(s, UnifiedCommitments.NpcDeal).Count(d => d.makerId == row.makerId && d.beneficiaryId == other), Is.EqualTo(1),
                "With another voter the strike writes the canonical row,");
            Assert.That(Accepted(s, row.makerId, other), Is.GreaterThan(lines), "and its ledger line.");
        }

        /// <summary>The house's strike of a nominee's vote bargain, as its pass calls it (NpcDeals.Strike).</summary>
        private static void Strike(EpisodeState s, string nominee, string voter) =>
            typeof(NpcDeals).GetMethod("Strike", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { s, nominee, voter, DealKind.VoteSave });

        /// <summary>How many bargains the ledger records between the two of them.</summary>
        private static int Accepted(EpisodeState s, string a, string b) => s.relationships
            .Where(r => r.fromId == a && r.toId == b || r.fromId == b && r.toId == a)
            .Sum(r => r.events.Count(e => e.type == "deal_accepted"));

        /// <summary>
        /// The player's answer to a house offer, on the canonical row: a yes binds it in the answering week and
        /// keeps its creation; a no changes only its consent; an answered offer cannot be answered again.
        /// </summary>
        [TestCase(true)] [TestCase(false)]
        public void AnOffersAnswerIsWrittenOnItsCanonicalRow(bool accept)
        {
            var (campaign, _) = CampaignPasses();
            var s = campaign.state;
            var offer = s.deals.First(d => d.id.StartsWith(NpcDeals.OfferPrefix, StringComparison.Ordinal) && d.type == DealKind.VoteSave
                && d.status == DealStatus.Proposed);
            var (legacy, prospective) = ProspectiveVoteTwins.Both(s, ProspectiveVoteTwins.Command(s, EpisodeCommandKind.RespondToDeal, offer.id,
                text: accept ? EpisodeEngine.AcceptDeal : "decline"));
            Assert.That(legacy.accepted, Is.True, legacy.reason);
            Assert.That(prospective.accepted, Is.True, prospective.reason);
            var row = prospective.state.unifiedCommitments.Single(r => r.id == offer.id);
            Assert.That((row.origin, row.createdWeek, row.makerId, row.beneficiaryId), Is.EqualTo((UnifiedCommitments.NpcOffer, offer.week, offer.proposerId, s.playerId)));
            if (accept) BoundNow(row, s.week, s.week);
            else Assert.That((row.status, row.voteBindingWeek, row.voteFirstRevealWeek, row.expiresWeek), Is.EqualTo((DealStatus.Declined, 0, 0, offer.expiresWeek)));
            NoRawVoteRows(prospective.state);
            ProspectiveVoteTwins.AssertParity(legacy.state, prospective.state, accept ? "An offer taken" : "An offer declined");

            var after = prospective.state;
            var again = ProspectiveVoteTwins.Command(after, EpisodeCommandKind.RespondToDeal, offer.id, text: EpisodeEngine.AcceptDeal, tag: "again");
            var refused = ProspectiveVoteFacade.Engine(after).Apply(again);
            Assert.That(refused.reason, Is.EqualTo("That offer is no longer on the table."));
            ProspectiveVoteTwins.AssertUntouched(after, refused, "An answered offer answered again");
        }

        /// <summary>
        /// The fixture behind the house's cases: the real Advance into the campaign, from a decided veto meeting
        /// with a houseguest Head of Household - whom the house's passes court with safety, canonical in mode 2
        /// as in mode 1 (vote family V3b; UnifiedSafetyModeTwoTests holds those rows).
        /// </summary>
        private static (CommandResult legacy, CommandResult prospective) CampaignPasses()
        {
            var s = HouseHoH();
            // Constructed: every houseguest reads the player at 20 - warm enough for a nominee to bargain - and
            // each nominee and one voter read each other at 45, so a nominee's vote bargain is one both want.
            foreach (var npc in s.contestants.Where(c => !c.isPlayer && c.status == ContestantStatus.Active))
                ProspectiveVoteTwins.Set(s, npc.id, s.playerId, 20);
            var voters = NpcVoters(s);
            for (int i = 0; i < 2; i++)
            {
                ProspectiveVoteTwins.Set(s, s.nominees[i], voters[i], 45);
                ProspectiveVoteTwins.Set(s, voters[i], s.nominees[i], 45);
            }
            int safety = s.unifiedCommitments.Count(row => row.kind == UnifiedCommitments.Safety);
            var both = ProspectiveVoteTwins.Both(ProspectiveVoteTwins.Valid(s), ProspectiveVoteTwins.Command(s, EpisodeCommandKind.Advance));
            Assert.That(both.legacy.accepted, Is.True, both.legacy.reason);
            Assert.That(both.prospective.accepted, Is.True, both.prospective.reason);
            Assert.That(both.prospective.state.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(both.prospective.state.unifiedCommitments.Count(row => row.kind == UnifiedCommitments.Safety), Is.GreaterThan(safety),
                "The passes court the house's Head of Household with safety too, and mode 2 writes it.");
            return both;
        }

        // ------------------------------------------------------------ prices

        /// <summary>
        /// A refused proposal's counter, taken: the deal asked for and the price on it are written together.
        /// A partnership bought with the player's vote to keep its nominee is a raw deal and a canonical
        /// counter price; a voting bloc bought with a safety pact is two canonical rows.
        /// </summary>
        [TestCase(true)] [TestCase(false)]
        public void ATakenCounterWritesBothItsDealsTogether(bool mixed)
        {
            var s = Voter();
            string npc = mixed ? s.nominees.First(id => id != s.playerId) : NpcVoters(s)[0];
            string kind = mixed ? DealKind.Partnership : DealKind.VoteTogether;
            double chance = 0;
            for (double warmth = 30; warmth <= 90 && chance < Negotiation.CounterFloor; warmth += 5)
            {
                ProspectiveVoteTwins.Set(s, npc, s.playerId, warmth); ProspectiveVoteTwins.Set(s, s.playerId, npc, warmth);
                chance = PlayerDeals.AcceptanceChance(s, npc, kind, null);
            }
            Assert.That(chance, Is.GreaterThanOrEqualTo(Negotiation.CounterFloor), "Fixture: close enough to yes for a counter.");
            s.randomState = ProspectiveVoteTwins.Draw(false, chance);
            for (int attempt = s.nextSequence; ; attempt++)
            {
                Assert.That(attempt, Is.LessThan(s.nextSequence + 5000), "Fixture: a landing counter coin.");
                if (StoryRandom.Unit(s, Negotiation.CounterKey(s, npc, kind, null, attempt)) < Negotiation.CounterChance) { s.nextSequence = attempt; break; }
            }
            var (legacyRefusal, prospectiveRefusal) = ProspectiveVoteTwins.Both(ProspectiveVoteTwins.Valid(s),
                ProspectiveVoteTwins.Command(s, EpisodeCommandKind.ProposeDeal, npc, null, kind));
            Assert.That(prospectiveRefusal.accepted, Is.True, prospectiveRefusal.reason);
            Assert.That(Negotiation.OpenCounter(legacyRefusal.state, npc), Is.Not.Null, "Fixture: the counter stands.");
            ProspectiveVoteTwins.AssertParity(legacyRefusal.state, prospectiveRefusal.state, "A refusal that counters");

            var countered = legacyRefusal.state;
            var counter = Negotiation.OpenCounter(countered, npc);
            Assert.That(counter.price.kind, Is.EqualTo(mixed ? DealKind.VoteSave : DealKind.SafetyAgreement));
            var (legacy, prospective) = ProspectiveVoteTwins.Both(countered,
                ProspectiveVoteTwins.Command(countered, EpisodeCommandKind.RespondToDeal, npc, text: EpisodeEngine.AcceptDeal));
            Assert.That(legacy.accepted, Is.True, legacy.reason);
            Assert.That(prospective.accepted, Is.True, prospective.reason);
            var after = prospective.state;
            string boughtId = Negotiation.CounterDealPrefix + countered.nextSequence, priceId = Negotiation.PricePrefix + countered.nextSequence;
            var price = after.unifiedCommitments.Single(row => row.id == priceId);
            Assert.That((price.origin, price.makerId, price.beneficiaryId, price.linkedCommitmentId),
                Is.EqualTo((UnifiedCommitments.CounterPrice, s.playerId, npc, boughtId)));
            if (mixed)
            {
                Assert.That((price.kind, price.subtype, price.targetId), Is.EqualTo((UnifiedVoteTogether.Vote, DealKind.VoteSave, npc)));
                BoundNow(price, s.week, s.week);
                var bought = after.deals.Single(d => d.id == boughtId);
                Assert.That((bought.type, bought.linkedDealId), Is.EqualTo((DealKind.Partnership, priceId)), "The bought partnership stays raw, linked to its price.");
            }
            else
            {
                Assert.That(price.kind, Is.EqualTo(UnifiedCommitments.Safety), "The safety price is the counter bundle's canonical member.");
                var bought = after.unifiedCommitments.Single(row => row.id == boughtId);
                Assert.That((bought.origin, bought.subtype, bought.linkedCommitmentId), Is.EqualTo((UnifiedCommitments.CounterDeal, DealKind.VoteTogether, priceId)));
                BoundNow(bought, s.week, s.week);
            }
            NoRawVoteRows(after);
            ProspectiveVoteTwins.AssertParity(legacy.state, after, mixed ? "A mixed counter" : "A canonical counter");
        }

        /// <summary>
        /// A mixed bundle is written whole or not at all: a counter whose canonical vote price already stands,
        /// or whose raw bought duty already stands, writes neither member.
        /// </summary>
        [TestCase(true)] [TestCase(false)]
        public void AMixedCounterBundleIsRefusedWholeWhenEitherMemberAlreadyStands(bool canonicalMember)
        {
            var s = ProspectiveVoteTwins.Twin(Voter());
            string npc = s.nominees.First(id => id != s.playerId);
            if (canonicalMember)
            {
                var standing = PlayerDeals.Draft(s, npc, DealKind.VoteSave, npc, "deal-player-" + s.nextSequence);
                Assert.That(ProspectiveVoteFacade.StoreTryAddDeal(s, standing, UnifiedCommitments.PlayerDeal, out var error), Is.True, error);
            }
            else s.deals.Add(PlayerDeals.Draft(s, npc, DealKind.Partnership, null, "deal-player-" + s.nextSequence));
            s.nextSequence++;
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(s, out var valid), Is.True, valid);
            var (bought, price) = Counter(s, npc);
            string before = PinnedVoteSeason.Json(s);
            Assert.That(ProspectiveVoteFacade.StoreTryAddCounter(s, bought, price, out var refusal), Is.False);
            Assert.That(refusal, Does.Contain("already stands"));
            Assert.That(PinnedVoteSeason.Json(s), Is.EqualTo(before), "Neither member is written.");

            var clear = ProspectiveVoteTwins.Twin(Voter());
            var (freeBought, freePrice) = Counter(clear, npc);
            Assert.That(ProspectiveVoteFacade.StoreTryAddCounter(clear, freeBought, freePrice, out var error2), Is.True, error2);
            Assert.That(clear.deals.Count(d => d.id == freeBought.id) + clear.unifiedCommitments.Count(r => r.id == freePrice.id), Is.EqualTo(2), "Unencumbered, both are written.");
        }

        /// <summary>A counter as AnswerCounter drafts it: the player's partnership with a nominee, bought with the player's vote to keep them.</summary>
        private static (DealState bought, DealState price) Counter(EpisodeState s, string npc)
        {
            string boughtId = Negotiation.CounterDealPrefix + s.nextSequence, priceId = Negotiation.PricePrefix + s.nextSequence;
            var bought = PlayerDeals.Draft(s, npc, DealKind.Partnership, null, boughtId);
            bought.linkedDealId = priceId;
            var price = Negotiation.DraftPrice(s, new Negotiation.Price { kind = DealKind.VoteSave, payerId = s.playerId, payeeId = npc,
                aboutId = npc, expiresWeek = s.week }, boughtId, priceId);
            return (bought, price);
        }

        /// <summary>
        /// A nominee's veto ask, taken: the price it carries - their vote to keep the player, open until a vote
        /// tests it - is reserved before the yes moves anything and struck where it always was, a canonical
        /// veto-ask price linked to the raw ask.
        /// </summary>
        [Test]
        public void ATakenVetoAskStrikesItsVotePriceAsACanonicalRow()
        {
            var s = VetoHolder();
            var ask = VetoAsk(s);
            string asker = ask.proposerId;
            Assert.That(Negotiation.AskPrice(ProspectiveVoteTwins.Valid(s), ask)?.kind, Is.EqualTo(DealKind.VoteSave), "Fixture: a vote price.");
            var (legacy, prospective) = ProspectiveVoteTwins.Both(s, ProspectiveVoteTwins.Command(s, EpisodeCommandKind.RespondToDeal, ask.id, text: EpisodeEngine.AcceptDeal));
            Assert.That(legacy.accepted, Is.True, legacy.reason);
            Assert.That(prospective.accepted, Is.True, prospective.reason);
            var after = prospective.state;
            var price = Rows(after, UnifiedVoteFamilyValidation.VetoAskPrice).Single();
            Assert.That((price.subtype, price.makerId, price.beneficiaryId, price.targetId, price.linkedCommitmentId),
                Is.EqualTo((DealKind.VoteSave, asker, s.playerId, s.playerId, ask.id)));
            Assert.That(price.id, Does.StartWith(Negotiation.PricePrefix));
            BoundNow(price, s.week, 0);
            var answered = after.deals.Single(d => d.id == ask.id);
            Assert.That((answered.status, answered.linkedDealId), Is.EqualTo((DealStatus.Active, price.id)), "The ask stays raw and names its price.");
            NoRawVoteRows(after);
            ProspectiveVoteTwins.AssertParity(legacy.state, after, "A taken veto ask");
        }

        /// <summary>A nominee's still-pending veto ask to the player: the house's own, or filed as NpcDeals.AskForTheVeto files it.</summary>
        private static DealState VetoAsk(EpisodeState s)
        {
            string asker = s.nominees.First(id => id != s.playerId);
            var ask = s.deals.FirstOrDefault(d => d.id.StartsWith(NpcDeals.VetoAskPrefix, StringComparison.Ordinal) && d.proposerId == asker
                && d.status == DealStatus.Proposed);
            if (ask != null) return ask;
            ask = new DealState { id = NpcDeals.VetoAskPrefix + s.nextSequence++, type = DealKind.VetoUse, proposerId = asker, recipientId = s.playerId,
                status = DealStatus.Proposed, week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(DealKind.VetoUse) };
            s.deals.Add(ask);
            return ask;
        }

        /// <summary>
        /// The ask's price is admitted as it is struck, not assumed from its reservation before the yes: struck at
        /// the sequence the season has reached, it is written and the answered ask names it; at another sequence,
        /// or with the vote it carries already owed to the player by then, nothing is written.
        /// </summary>
        [TestCase("struck")] [TestCase("another sequence")] [TestCase("already owed")]
        public void AStruckAskPriceIsAdmittedAsItIsWritten(string defect)
        {
            var source = VetoHolder();
            string askId = VetoAsk(source).id;
            var s = ProspectiveVoteTwins.Twin(ProspectiveVoteTwins.Valid(source));
            var ask = s.deals.Single(d => d.id == askId);
            var terms = Negotiation.AskPrice(s, ask);
            if (defect == "already owed")
            {
                // A story's vote to keep the player, from the asker, made before the yes.
                var owed = PlayerDeals.Draft(s, ask.proposerId, DealKind.VoteSave, null, "deal-story-" + s.nextSequence);
                owed.proposerId = ask.proposerId; owed.recipientId = s.playerId; owed.targetId = s.playerId;
                Assert.That(ProspectiveVoteFacade.StoreTryAddDeal(s, owed, UnifiedCommitments.StoryDeal, out var owedError), Is.True, owedError);
                s.nextSequence++;
            }
            // The command's yes, as RespondToDeal gives it: the ask answered for this week.
            ask.status = DealStatus.Active; ask.expiresWeek = s.week;
            var price = Negotiation.DraftPrice(s, terms, ask.id, Negotiation.PricePrefix + (s.nextSequence + (defect == "another sequence" ? 1 : 0)));
            string before = PinnedVoteSeason.Json(s);
            bool struck = ProspectiveVoteFacade.StoreTryInstallAskPrice(s, ask, price, out var error);
            if (defect == "struck")
            {
                Assert.That(struck, Is.True, error);
                var row = Rows(s, UnifiedVoteFamilyValidation.VetoAskPrice).Single();
                Assert.That((row.id, row.linkedCommitmentId, ask.linkedDealId), Is.EqualTo((price.id, ask.id, price.id)));
            }
            else
            {
                Assert.That(struck, Is.False);
                Assert.That(PinnedVoteSeason.Json(s), Is.EqualTo(before), "Nothing is struck: " + error);
            }
        }

        /// <summary>
        /// A veto for a price: the player's word on the veto (raw) and the nominee's vote to keep the player
        /// (canonical, open) are written together; with no room for both under the forty it is refused whole,
        /// in the price's own words.
        /// </summary>
        [Test]
        public void AVetoForAVotePriceWritesBothDealsOrIsRefusedWhole()
        {
            var s = VetoHolder();
            string target = s.nominees.First(id => id != s.playerId);
            Assert.That(Negotiation.VetoPriceRefusal(s, target, DealKind.VoteSave), Is.Null, "Fixture: the price can be named.");
            s.randomState = ProspectiveVoteTwins.Draw(true, Negotiation.Chance(s, target, Negotiation.VetoForAPrice, false));
            var move = ProspectiveVoteTwins.Command(s, EpisodeCommandKind.Negotiate, target, text: Negotiation.VetoPriceMove(DealKind.VoteSave));
            var (legacy, prospective) = ProspectiveVoteTwins.Both(ProspectiveVoteTwins.Valid(s), move);
            Assert.That(legacy.accepted, Is.True, legacy.reason);
            Assert.That(prospective.accepted, Is.True, prospective.reason);
            var after = prospective.state;
            string vetoId = "deal-player-" + s.nextSequence, priceId = Negotiation.PricePrefix + s.nextSequence;
            var veto = after.deals.Single(d => d.id == vetoId);
            Assert.That((veto.type, veto.proposerId, veto.recipientId, veto.linkedDealId), Is.EqualTo((DealKind.VetoUse, s.playerId, target, priceId)));
            var price = after.unifiedCommitments.Single(row => row.id == priceId);
            Assert.That((price.origin, price.subtype, price.makerId, price.beneficiaryId, price.targetId, price.linkedCommitmentId),
                Is.EqualTo((UnifiedVoteFamilyValidation.OwnVetoPrice, DealKind.VoteSave, target, s.playerId, s.playerId, vetoId)));
            BoundNow(price, s.week, 0);
            NoRawVoteRows(after);
            ProspectiveVoteTwins.AssertParity(legacy.state, after, "A veto for a price");

            var full = VetoHolder();
            var lapsed = full.contestants.Where(c => !c.isPlayer).Select(c => c.id).Take(2).ToArray();
            // Constructed, as in the forty's own case: filler history no owner files, only filling the shelf.
            for (int n = full.deals.Count; n < PlayerDeals.PlayerDealCeiling - 1; n++)
                full.deals.Add(new DealState { id = "deal-lapsed-" + n, type = DealKind.InformationSharing, proposerId = lapsed[0], recipientId = lapsed[1],
                    status = DealStatus.Expired, week = full.week, expiresWeek = full.week, trustImpact = DealKind.DefaultTrust(DealKind.InformationSharing) });
            var twin = ProspectiveVoteTwins.Twin(ProspectiveVoteTwins.Valid(full));
            var refused = ProspectiveVoteFacade.Engine(twin).Apply(ProspectiveVoteTwins.Command(twin, EpisodeCommandKind.Negotiate, target,
                text: Negotiation.VetoPriceMove(DealKind.VoteSave)));
            Assert.That(refused.reason, Is.EqualTo(Negotiation.TooManyArrangements));
            ProspectiveVoteTwins.AssertUntouched(twin, refused, "A veto price with room for one");
        }

        /// <summary>The own-veto pair is atomic in the store: a vote price the nominee already owes the player writes neither deal.</summary>
        [Test]
        public void AnOwnVetoPriceAlreadyOwedWritesNeitherDeal()
        {
            var s = ProspectiveVoteTwins.Twin(VetoHolder());
            string target = s.nominees.First(id => id != s.playerId);
            var veto = PlayerDeals.Draft(s, target, DealKind.VetoUse, null, "deal-player-" + s.nextSequence);
            veto.linkedDealId = Negotiation.PricePrefix + s.nextSequence;
            var price = Negotiation.DraftPrice(s, Negotiation.VetoPrice(s, target, DealKind.VoteSave), veto.id, veto.linkedDealId);
            var free = s.Clone();
            Assert.That(ProspectiveVoteFacade.StoreTryAddOwnVetoPrice(free, veto, price, out var error), Is.True, error);

            var owed = PlayerDeals.Draft(s, target, DealKind.VoteSave, null, "deal-story-" + s.nextSequence);
            owed.proposerId = target; owed.recipientId = s.playerId; owed.targetId = s.playerId;
            Assert.That(ProspectiveVoteFacade.StoreTryAddDeal(s, owed, UnifiedCommitments.StoryDeal, out error), Is.True, error);
            s.nextSequence++;
            veto.id = "deal-player-" + s.nextSequence; veto.linkedDealId = Negotiation.PricePrefix + s.nextSequence;
            price.id = veto.linkedDealId; price.linkedDealId = veto.id;
            string before = PinnedVoteSeason.Json(s);
            Assert.That(ProspectiveVoteFacade.StoreTryAddOwnVetoPrice(s, veto, price, out error), Is.False);
            Assert.That(PinnedVoteSeason.Json(s), Is.EqualTo(before), "Neither the veto nor its price is written: " + error);
        }

        // ------------------------------------------------------------ a story's word and deals

        /// <summary>
        /// A story's promise on the vote and its vote deals, applied as a beat applies them: canonical story
        /// rows - the promise with no target, as the native story promise has none; the deals in the story's
        /// own party order - and the same effect again writes nothing more, in either game.
        /// </summary>
        [TestCase(StoryEffects.Promise, null)]
        [TestCase(StoryEffects.Deal, DealKind.VoteTogether)] [TestCase(StoryEffects.Deal, DealKind.VoteSave)] [TestCase(StoryEffects.Deal, DealKind.VoteEvict)]
        public void AStorysVoteWordAndDealsAreCanonicalStoryRows(string effect, string dealType)
        {
            var legacy = Voter();
            var voters = NpcVoters(legacy);
            var e = effect == StoryEffects.Promise
                ? new StoryEffectState { kind = StoryEffects.Promise, fromId = voters[0], toId = legacy.nominees[0], type = PromiseKind.Vote.ToString() }
                : new StoryEffectState { kind = StoryEffects.Deal, fromId = legacy.playerId, toId = voters[0], type = dealType,
                    thirdId = dealType == DealKind.VoteTogether ? null : legacy.nominees[0] };
            var prospective = ProspectiveVoteTwins.Twin(legacy);
            long sequence = legacy.nextSequence;
            ApplyStoryEffect(legacy, e); ApplyStoryEffect(prospective, e);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(prospective, out var error), Is.True, error);
            string origin = effect == StoryEffects.Promise ? UnifiedCommitments.StoryPromise : UnifiedCommitments.StoryDeal;
            var row = Rows(prospective, origin).Single();
            Assert.That((row.id, row.makerId, row.beneficiaryId, row.targetId),
                Is.EqualTo(((effect == StoryEffects.Promise ? "promise-" : "deal-story-") + sequence, e.fromId, e.toId, e.thirdId)));
            if (effect == StoryEffects.Deal) Assert.That(row.subtype, Is.EqualTo(dealType));
            BoundNow(row, legacy.week, legacy.week);
            NoRawVoteRows(prospective);
            ProspectiveVoteTwins.AssertParity(legacy, prospective, "A story's " + effect);

            ApplyStoryEffect(legacy, e); ApplyStoryEffect(prospective, e);
            Assert.That(Rows(prospective, origin).Count(), Is.EqualTo(1), "The same story effect again writes nothing more.");
            ProspectiveVoteTwins.AssertParity(legacy, prospective, "A story's " + effect + " again");
        }

        /// <summary>
        /// The 200 a word counts against holds the canonical rows: with the season's raw and canonical promises
        /// at 200, a story's word writes nothing, as in mode 1, where the same vote words are raw rows; one place
        /// short, it is written in both.
        /// </summary>
        [TestCase(true)] [TestCase(false)]
        public void AStorysWordCountsTheCanonicalPromisesAgainstTheTwoHundred(bool full)
        {
            var legacy = Voter();
            var voters = NpcVoters(legacy);
            int held = legacy.promises.Count + legacy.unifiedCommitments.Count(row => row.sourcePolicy == UnifiedCommitments.PromisePolicy);
            // Constructed, and no owner files it: filler history that only fills the promise shelf, expired in its own week.
            for (int n = held; n < NpcPromises.PromiseCeiling - (full ? 0 : 1); n++)
                legacy.promises.Add(new PromiseState { id = "promise-lapsed-" + n, fromId = voters[0], toId = voters[1], kind = PromiseKind.Information,
                    status = PromiseStatus.Expired, week = legacy.week, expiresWeek = legacy.week });
            var prospective = ProspectiveVoteTwins.Twin(ProspectiveVoteTwins.Valid(legacy));
            Assert.That(prospective.unifiedCommitments.Count(row => row.kind == UnifiedVoteTogether.Vote && row.sourcePolicy == UnifiedCommitments.PromisePolicy),
                Is.GreaterThan(0), "Fixture: vote words the twin holds as canonical rows, not raw.");
            var e = new StoryEffectState { kind = StoryEffects.Promise, fromId = voters[1], toId = voters[2], type = PromiseKind.AllianceLoyalty.ToString() };
            ApplyStoryEffect(legacy, e); ApplyStoryEffect(prospective, e);
            Assert.That(prospective.promises.Count(p => p.kind == PromiseKind.AllianceLoyalty && p.fromId == voters[1] && p.toId == voters[2]),
                Is.EqualTo(full ? 0 : 1), full ? "The shelf is full: nothing is written." : "One place short, the word is written.");
            ProspectiveVoteTwins.AssertParity(legacy, prospective, full ? "A story's word on a full shelf" : "A story's word with one place left");
        }

        /// <summary>
        /// A state the complete core already refuses is never silently a skipped row. The owners that skip a draft
        /// the store refuses - a story's word on the vote, a house pass's bargain - meet a failing state as the whole
        /// command's refusal, naming the core's reason, and write nothing. (A draft refused on its own is still
        /// skipped: ADuplicateHouseBargainIsRefusedBeforeItsLedgerLine.)
        /// </summary>
        [TestCase(StoryEffects.Promise)] [TestCase("house bargain")]
        public void AStateTheCoreRefusesIsTheCommandsRefusalNeverASkippedRow(string owner)
        {
            var s = ProspectiveVoteTwins.Twin(Voter());
            var voters = NpcVoters(s);
            // Constructed: a raw vote word the core refuses, as an inconsistency in the middle of a command would leave it.
            s.promises.Add(new PromiseState { id = "promise-mirror", fromId = voters[1], toId = s.nominees[1], targetId = s.nominees[0],
                kind = PromiseKind.Vote, status = PromiseStatus.Active, week = s.week, expiresWeek = s.week });
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(s, out var invalid), Is.False, "Fixture: the core refuses the state.");
            string before = PinnedVoteSeason.Json(s);
            var thrown = Assert.Throws<TargetInvocationException>(() =>
            {
                if (owner == StoryEffects.Promise)
                    ApplyStoryEffect(s, new StoryEffectState { kind = StoryEffects.Promise, fromId = voters[0], toId = s.nominees[0], type = PromiseKind.Vote.ToString() });
                else Strike(s, s.nominees[0], voters[0]);
            });
            Assert.That(thrown.InnerException?.GetType().Name, Is.EqualTo("RuleException"), "The whole command's refusal.");
            Assert.That(thrown.InnerException.Message, Does.StartWith("The prospective Vote state fails its core in the middle of the command: "));
            Assert.That(thrown.InnerException.Message, Does.EndWith(invalid));
            Assert.That(PinnedVoteSeason.Json(s), Is.EqualTo(before), "Nothing is written.");
        }

        /// <summary>One story effect, applied the way a beat applies it (EpisodeEngine.ApplyStoryEffect).</summary>
        private static void ApplyStoryEffect(EpisodeState s, StoryEffectState effect)
        {
            var method = typeof(EpisodeEngine).GetMethod("ApplyStoryEffect", BindingFlags.Static | BindingFlags.NonPublic, null,
                new[] { typeof(EpisodeState), typeof(StoryEffectState), typeof(StorylineState), typeof(string), typeof(string), typeof(int), typeof(bool) }, null);
            Assert.That(method, Is.Not.Null, "The engine's ApplyStoryEffect, the story's one effect router.");
            method.Invoke(null, new object[] { s, effect, null, null, "vote-v3", 0, false });
        }
    }
}
