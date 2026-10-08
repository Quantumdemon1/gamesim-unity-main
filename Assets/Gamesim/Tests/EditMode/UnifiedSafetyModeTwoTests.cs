using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Vote family V3b: Safety under the prospective mode 2. Canonical Safety stays the season's one Safety
    /// authority there (<see cref="UnifiedCommitments.SafetyAuthorityOn"/>): every Safety creator writes the
    /// canonical row mode 1 writes, through the Safety store, and the nomination, replacement, spared and
    /// expiry gateways settle those rows - the Safety rows only, beside the Vote family's - with the hearing
    /// lineage a player's audible pact breach installs. Without it a mode-2 command that wrote a Safety row
    /// wrote a raw one, which the complete core refuses: a house with a houseguest Head of Household stalled
    /// at its first campaign.
    ///
    /// <para>Each case drives the real command - or, for a story's effect and the expiry boundaries, the real
    /// owner, called as the engine calls it - on a season's mode-1 copy and on its exact mode-2 twin through
    /// the internal seam, and requires the mode-2 result to be the mode-1 result field for field
    /// (<see cref="ProspectiveVoteTwins.AssertParity"/>). The constructed facts are the twins' own: a
    /// relationship score, the season's next draw, the sequence a keyed coin reads and rows filed as their
    /// owners file them; the expiry boundaries also take the week a turned week would have, on a detached
    /// copy, since a mode-2 season cannot yet cross a reveal (vote family V4).</para>
    /// </summary>
    public sealed class UnifiedSafetyModeTwoTests
    {
        // ------------------------------------------------------------ fixtures

        private static EpisodeState nominating, pleading, houseHoH, playerHoH;

        /// <summary>The week-one social window of a fresh season, nothing spent.</summary>
        private static EpisodeState Fresh() => PinnedVoteSeason.Fresh(3);

        /// <summary>A first nomination the player makes as Head of Household, the block still empty.</summary>
        private static EpisodeState Nominating() => (nominating ??= ProspectiveVoteTwins.Find("a first nomination the player makes",
            seed => ProspectiveVoteTwins.Walk(seed, s => s.phase == EpisodePhase.Nomination && s.hohId == s.playerId && s.nominees.Count == 0,
                EpisodePhase.HoH))).Clone();

        /// <summary>A first nomination a houseguest Head of Household makes, before the names are said.</summary>
        private static EpisodeState Pleading() => (pleading ??= ProspectiveVoteTwins.Find("a first nomination a houseguest makes",
            seed => ProspectiveVoteTwins.Walk(seed, s => s.phase == EpisodePhase.Nomination && s.hohId != null && s.hohId != s.playerId
                && s.nominees.Count == 0))).Clone();

        /// <summary>A first decided veto meeting with a houseguest Head of Household and the player an ordinary voter.</summary>
        private static EpisodeState HouseHoH() => (houseHoH ??= ProspectiveVoteTwins.Find("a first decided veto meeting with a houseguest Head of Household",
            seed => ProspectiveVoteTwins.Walk(seed, s => s.phase == EpisodePhase.VetoMeeting && s.vetoResolved && s.hohId != s.playerId
                && !s.nominees.Contains(s.playerId)))).Clone();

        /// <summary>A first decided veto meeting with the player Head of Household: the house's campaign passes are next.</summary>
        private static EpisodeState PlayerHoH() => (playerHoH ??= ProspectiveVoteTwins.Find("a first decided veto meeting with the player Head of Household",
            seed => ProspectiveVoteTwins.Walk(seed, s => s.phase == EpisodePhase.VetoMeeting && s.vetoResolved && s.hohId == s.playerId,
                EpisodePhase.HoH))).Clone();

        private static string Npc(EpisodeState s, int index = 0) =>
            s.contestants.Where(c => !c.isPlayer && c.status == ContestantStatus.Active).Skip(index).First().id;

        private static IEnumerable<UnifiedCommitmentState> Rows(EpisodeState s, string origin) =>
            s.unifiedCommitments.Where(row => row.kind == UnifiedCommitments.Safety && row.origin == origin);

        private static void NoRawSafety(EpisodeState s)
        {
            Assert.That(s.promises.Any(p => p.kind == PromiseKind.Safety), Is.False, "No raw safety promise under mode 2.");
            Assert.That(s.deals.Any(d => d.type == DealKind.SafetyAgreement), Is.False, "No raw safety pact under mode 2.");
        }

        private static void Accepted((CommandResult legacy, CommandResult prospective) both)
        {
            Assert.That(both.legacy.accepted, Is.True, "Mode 1: " + both.legacy.reason);
            Assert.That(both.prospective.accepted, Is.True, "Mode 2: " + both.prospective.reason);
        }

        /// <summary>A command the fixture needs the public mode-1 game to take.</summary>
        private static EpisodeState Step(EpisodeState s, EpisodeCommand command)
        {
            var result = new EpisodeEngine(s).Apply(command);
            Assert.That(result.accepted, Is.True, "Fixture: " + command.kind + ": " + result.reason);
            return result.state;
        }

        /// <summary>The player's real proposal of a safety pact, with a draw that says yes.</summary>
        private static EpisodeState Pact(EpisodeState s, string npc)
        {
            s.randomState = ProspectiveVoteTwins.Draw(true, PlayerDeals.AcceptanceChance(s, npc, DealKind.SafetyAgreement, null));
            return Step(ProspectiveVoteTwins.Valid(s), ProspectiveVoteTwins.Command(s, EpisodeCommandKind.ProposeDeal, npc, null, DealKind.SafetyAgreement));
        }

        // ------------------------------------------------------------ the authority

        [Test]
        public void CanonicalSafetyIsTheAuthorityInModeOneAndModeTwoOnly()
        {
            var s = Fresh();
            foreach (var (mode, on) in new[] { (0, false), (1, true), (2, true), (3, false), (-1, false) })
            {
                s.unifiedCommitmentRulesVersion = mode;
                Assert.That(UnifiedCommitments.SafetyAuthorityOn(s), Is.EqualTo(on), "Mode " + mode + ".");
                Assert.That(UnifiedCommitments.RulesOn(s), Is.EqualTo(mode == 1), "RulesOn stays mode 1's: the readers not yet moved keep it.");
            }
            Assert.That(UnifiedCommitments.SafetyAuthorityOn(null), Is.False);
        }

        // ------------------------------------------------------------ the player's word and pacts

        /// <summary>
        /// "Promise safety": the canonical row mode 1 writes - the promise's own id, the player to the
        /// houseguest, through next week - and a second word on the same duty is refused in mode 1's words.
        /// </summary>
        [Test]
        public void APromiseOfSafetyIsTheCanonicalRowModeOneWritesAndASecondIsRefusedInItsWords()
        {
            var s = Fresh(); string npc = Npc(s);
            var both = ProspectiveVoteTwins.Both(s, ProspectiveVoteTwins.Command(s, EpisodeCommandKind.PromiseSafety, npc));
            Accepted(both);
            var after = both.prospective.state;
            var row = Rows(after, UnifiedCommitments.PlayerPromise).Single();
            Assert.That((row.id, row.sourcePolicy, row.makerId, row.beneficiaryId, row.reciprocal, row.createdWeek, row.expiresWeek, row.status),
                Is.EqualTo(("promise-" + s.nextSequence, UnifiedCommitments.PromisePolicy, s.playerId, npc, false, s.week, s.week + 1, DealStatus.Active)));
            NoRawSafety(after);
            ProspectiveVoteTwins.AssertParity(both.legacy.state, after, "PromiseSafety");

            var again = ProspectiveVoteTwins.Command(after, EpisodeCommandKind.PromiseSafety, npc);
            var refused = ProspectiveVoteFacade.Engine(after).Apply(again);
            Assert.That(refused.reason, Is.EqualTo("The same safety duty and term already stand."));
            ProspectiveVoteTwins.AssertUntouched(after, refused, "A second word of safety");
            Assert.That(new EpisodeEngine(both.legacy.state).Apply(again).reason, Is.EqualTo(refused.reason), "As the mode-1 game refuses it.");
        }

        /// <summary>A Head of Household's promise to a houseguest's pitch: a canonical HoH-pitch row.</summary>
        [Test]
        public void AHeadOfHouseholdsPromiseToAPitchIsACanonicalPitchRow()
        {
            var s = Nominating();
            var card = s.replyCards.FirstOrDefault(c => c.kind == ReplyCards.Pitch && HoHPitches.ValidCard(s, c));
            if (card == null)
            {
                // Filed as HoHPitches.Offer files it, for a houseguest who could go up.
                string from = EpisodeEngine.NominationCandidates(s).First(c => !c.isPlayer).id;
                card = new ReplyCardState { id = "reply-pitch-" + s.week + "-" + from, week = s.week, kind = ReplyCards.Pitch, fromId = from };
                s.replyCards.Insert(0, card);
                ProspectiveVoteTwins.Valid(s);
            }
            Assert.That(ReplyCards.Pending(s)?.id, Is.EqualTo(card.id), "Fixture: the pitch is the one being heard.");
            var both = ProspectiveVoteTwins.Both(s, ProspectiveVoteTwins.Command(s, EpisodeCommandKind.ReplyToHouseguest, card.id, text: "promise-safety"));
            Accepted(both);
            var row = Rows(both.prospective.state, UnifiedCommitments.HoHPitch).Single();
            Assert.That((row.id, row.makerId, row.beneficiaryId, row.expiresWeek),
                Is.EqualTo(("promise-" + s.nextSequence, s.playerId, card.fromId, s.week + 1)));
            NoRawSafety(both.prospective.state);
            ProspectiveVoteTwins.AssertParity(both.legacy.state, both.prospective.state, "A pitch promised safety");
        }

        /// <summary>
        /// A safety pact the houseguest agrees to: the canonical player pact mode 1 writes, and the same pact
        /// again is refused in mode 1's words - by the deal table's own check, as the screen asks it, and by
        /// the command, with nothing written.
        /// </summary>
        [Test]
        public void AnAgreedSafetyPactIsACanonicalPlayerPactAndTheSamePactAgainIsRefusedInItsWords()
        {
            var s = Fresh(); string npc = Npc(s);
            Assert.That(PlayerDeals.CanPropose(s, npc, DealKind.SafetyAgreement, null, out var why), Is.True, why);
            s.randomState = ProspectiveVoteTwins.Draw(true, PlayerDeals.AcceptanceChance(s, npc, DealKind.SafetyAgreement, null));
            var both = ProspectiveVoteTwins.Both(ProspectiveVoteTwins.Valid(s),
                ProspectiveVoteTwins.Command(s, EpisodeCommandKind.ProposeDeal, npc, null, DealKind.SafetyAgreement));
            Accepted(both);
            var after = both.prospective.state;
            var row = Rows(after, UnifiedCommitments.PlayerDeal).Single();
            Assert.That((row.id, row.sourcePolicy, row.makerId, row.beneficiaryId, row.reciprocal, row.expiresWeek, row.trustImpact),
                Is.EqualTo(("deal-player-" + s.nextSequence, UnifiedCommitments.DealPolicy, s.playerId, npc, true, s.week,
                    DealKind.DefaultTrust(DealKind.SafetyAgreement))));
            NoRawSafety(after);
            ProspectiveVoteTwins.AssertParity(both.legacy.state, after, "A safety pact");

            Assert.That(PlayerDeals.CanPropose(after, npc, DealKind.SafetyAgreement, null, out var twice), Is.False);
            Assert.That(twice, Is.EqualTo("The same safety duty and term already stand."), "The deal table reads the canonical pact.");
            Assert.That(PlayerDeals.CanPropose(both.legacy.state, npc, DealKind.SafetyAgreement, null, out var legacyTwice), Is.False);
            Assert.That(legacyTwice, Is.EqualTo(twice), "As mode 1's reads it.");
            var again = ProspectiveVoteTwins.Command(after, EpisodeCommandKind.ProposeDeal, npc, null, DealKind.SafetyAgreement);
            var refused = ProspectiveVoteFacade.Engine(after).Apply(again);
            Assert.That(refused.reason, Is.EqualTo("The same safety duty and term already stand."));
            ProspectiveVoteTwins.AssertUntouched(after, refused, "The same pact again");
            Assert.That(new EpisodeEngine(both.legacy.state).Apply(again).reason, Is.EqualTo(refused.reason));
        }

        /// <summary>
        /// A vote pact never stands for a safety pact: with the player's canonical vote pact with a voter standing
        /// for this week, a safety pact with the same voter for the same week is still theirs to make, as in mode 1,
        /// where the vote pact is a raw row the safety authority never reads.
        /// </summary>
        [Test]
        public void AVotePactDoesNotStandForASafetyPactOfTheSameTerm()
        {
            var s = ProspectiveVoteTwins.Find("a first campaign the player votes in", seed => ProspectiveVoteTwins.Walk(seed,
                x => x.phase == EpisodePhase.Campaign && EpisodeEngine.Voters(x).Any(v => v.isPlayer)));
            string voter = EpisodeEngine.Voters(s).First(v => !v.isPlayer).id;
            s.randomState = ProspectiveVoteTwins.Draw(true, PlayerDeals.AcceptanceChance(s, voter, DealKind.VoteTogether, null));
            s = Step(ProspectiveVoteTwins.Valid(s), ProspectiveVoteTwins.Command(s, EpisodeCommandKind.ProposeDeal, voter, null, DealKind.VoteTogether));
            s.randomState = ProspectiveVoteTwins.Draw(true, PlayerDeals.AcceptanceChance(s, voter, DealKind.SafetyAgreement, null));
            var both = ProspectiveVoteTwins.Both(ProspectiveVoteTwins.Valid(s),
                ProspectiveVoteTwins.Command(s, EpisodeCommandKind.ProposeDeal, voter, null, DealKind.SafetyAgreement));
            Accepted(both);
            var after = both.prospective.state;
            var vote = after.unifiedCommitments.Single(row => row.kind == UnifiedVoteTogether.Vote && row.subtype == DealKind.VoteTogether);
            var safety = Rows(after, UnifiedCommitments.PlayerDeal).Single();
            Assert.That((vote.beneficiaryId, vote.expiresWeek, vote.status), Is.EqualTo((voter, s.week, DealStatus.Active)), "Fixture: the vote pact stands.");
            Assert.That((safety.beneficiaryId, safety.expiresWeek, safety.status), Is.EqualTo((voter, s.week, DealStatus.Active)), "The safety pact of the same term.");
            ProspectiveVoteTwins.AssertParity(both.legacy.state, after, "A safety pact beside a vote pact");
        }

        /// <summary>A plea to the Head of Household, made with a deal, that lands: a canonical lobby pact through next week.</summary>
        [Test]
        public void ALandedPleaToTheHeadOfHouseholdMadeWithADealIsACanonicalLobbyPact()
        {
            var s = Pleading(); string hoh = s.hohId;
            s.randomState = ProspectiveVoteTwins.Draw(true, StrategyRules.Chance(s, hoh, LobbyAsk.Spare, s.playerId, LobbyApproach.Deal));
            var both = ProspectiveVoteTwins.Both(ProspectiveVoteTwins.Valid(s), ProspectiveVoteTwins.Command(s, EpisodeCommandKind.Lobby, hoh,
                s.playerId, LobbyAsk.Encode(LobbyAsk.Spare, LobbyApproach.Deal)));
            Accepted(both);
            var row = Rows(both.prospective.state, UnifiedCommitments.Lobby).Single();
            Assert.That((row.id.StartsWith("deal-lobby-", StringComparison.Ordinal), row.makerId, row.beneficiaryId, row.expiresWeek),
                Is.EqualTo((true, s.playerId, hoh, s.week + 1)));
            NoRawSafety(both.prospective.state);
            ProspectiveVoteTwins.AssertParity(both.legacy.state, both.prospective.state, "A landed plea with a deal");
        }

        /// <summary>
        /// A refused partnership's counter, priced with the player's safety: the raw partnership and the
        /// canonical safety price, written together by the safety store as mode 1 writes them.
        /// </summary>
        [Test]
        public void ATakenCounterPricedWithSafetyWritesBothDealsTogether()
        {
            var s = Fresh(); string npc = Npc(s);
            double chance = 0;
            for (double warmth = 30; warmth <= 90 && chance < Negotiation.CounterFloor; warmth += 5)
            {
                ProspectiveVoteTwins.Set(s, npc, s.playerId, warmth); ProspectiveVoteTwins.Set(s, s.playerId, npc, warmth);
                chance = PlayerDeals.AcceptanceChance(s, npc, DealKind.Partnership, null);
            }
            Assert.That(chance, Is.GreaterThanOrEqualTo(Negotiation.CounterFloor), "Fixture: close enough to yes for a counter.");
            s.randomState = ProspectiveVoteTwins.Draw(false, chance);
            for (int attempt = s.nextSequence; ; attempt++)
            {
                Assert.That(attempt, Is.LessThan(s.nextSequence + 5000), "Fixture: a landing counter coin.");
                if (StoryRandom.Unit(s, Negotiation.CounterKey(s, npc, DealKind.Partnership, null, attempt)) < Negotiation.CounterChance) { s.nextSequence = attempt; break; }
            }
            var refusal = ProspectiveVoteTwins.Both(ProspectiveVoteTwins.Valid(s),
                ProspectiveVoteTwins.Command(s, EpisodeCommandKind.ProposeDeal, npc, null, DealKind.Partnership));
            Accepted(refusal);
            var countered = refusal.legacy.state;
            var counter = Negotiation.OpenCounter(countered, npc);
            Assert.That(counter?.price.kind, Is.EqualTo(DealKind.SafetyAgreement), "Fixture: a counter priced with safety.");

            var both = ProspectiveVoteTwins.Both(countered, ProspectiveVoteTwins.Command(countered, EpisodeCommandKind.RespondToDeal, npc, text: EpisodeEngine.AcceptDeal));
            Accepted(both);
            var after = both.prospective.state;
            string boughtId = Negotiation.CounterDealPrefix + countered.nextSequence, priceId = Negotiation.PricePrefix + countered.nextSequence;
            var price = Rows(after, UnifiedCommitments.CounterPrice).Single();
            Assert.That((price.id, price.makerId, price.beneficiaryId, price.linkedCommitmentId), Is.EqualTo((priceId, s.playerId, npc, boughtId)));
            var bought = after.deals.Single(d => d.id == boughtId);
            Assert.That((bought.type, bought.linkedDealId), Is.EqualTo((DealKind.Partnership, priceId)), "The bought partnership stays raw, linked to its price.");
            NoRawSafety(after);
            ProspectiveVoteTwins.AssertParity(both.legacy.state, after, "A counter priced with safety");
        }

        /// <summary>
        /// The safety store answers, writes and voids safety rows only. In mode 2 a vote offer, a counter with a
        /// vote member and a vote price are the Vote family's (its store; its endings are vote family V4): the
        /// safety store refuses each and writes nothing.
        /// </summary>
        [Test]
        public void TheSafetyStoreAnswersWritesAndVoidsOnlySafetyRows()
        {
            var s = ProspectiveVoteTwins.Twin(ProspectiveVoteTwins.Find("a first campaign the player votes in", seed => ProspectiveVoteTwins.Walk(seed,
                x => x.phase == EpisodePhase.Campaign && EpisodeEngine.Voters(x).Any(v => v.isPlayer))));
            // A vote offer the house put: not the safety store's to answer.
            var offer = s.unifiedCommitments.FirstOrDefault(row => row.kind == UnifiedVoteTogether.Vote && row.origin == UnifiedCommitments.NpcOffer
                && row.status == DealStatus.Proposed);
            if (offer == null)
            {
                string asking = s.nominees.First(id => id != s.playerId);
                var put = new DealState { id = NpcDeals.OfferPrefix + s.nextSequence, type = DealKind.VoteSave, proposerId = asking, recipientId = s.playerId,
                    targetId = asking, status = DealStatus.Proposed, week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(DealKind.VoteSave) };
                Assert.That(ProspectiveVoteFacade.StoreTryAddDeal(s, put, UnifiedCommitments.NpcOffer, out var putError), Is.True, putError);
                s.nextSequence++;
                offer = s.unifiedCommitments.Single(row => row.id == put.id);
            }
            // The other nominee, whom no vote pact or offer binds to the player yet.
            string npc = s.nominees.First(id => id != s.playerId && id != offer.makerId);
            string before = PinnedVoteSeason.Json(s);
            Assert.That(ProspectiveVoteFacade.SafetyStoreTryRespond(s, offer.id, true, out _), Is.False, "A vote offer is answered by the Vote store.");
            Assert.That(PinnedVoteSeason.Json(s), Is.EqualTo(before));

            // A counter with a vote member: not the safety store's to write, even with a safety member beside it.
            string boughtId = Negotiation.CounterDealPrefix + s.nextSequence, priceId = Negotiation.PricePrefix + s.nextSequence;
            var safetyBought = PlayerDeals.Draft(s, npc, DealKind.SafetyAgreement, null, boughtId);
            safetyBought.linkedDealId = priceId;
            var votePrice = Negotiation.DraftPrice(s, new Negotiation.Price { kind = DealKind.VoteSave, payerId = s.playerId, payeeId = npc,
                aboutId = npc, expiresWeek = s.week }, boughtId, priceId);
            Assert.That(ProspectiveVoteFacade.SafetyStoreTryAddLinkedDeals(s, safetyBought, UnifiedCommitments.CounterDeal, votePrice,
                UnifiedCommitments.CounterPrice, out var counterError), Is.False);
            Assert.That(counterError, Does.Contain("Vote family's store"));
            Assert.That(PinnedVoteSeason.Json(s), Is.EqualTo(before), "Neither member is written.");

            // A vote price, voided by its payee's breach of what it bought: the Vote family's ending, not the safety store's.
            var bought = PlayerDeals.Draft(s, npc, DealKind.InformationSharing, null, boughtId);
            bought.linkedDealId = priceId;
            Assert.That(ProspectiveVoteFacade.StoreTryAddCounter(s, bought, votePrice, out var voteError), Is.True, voteError);
            s.nextSequence++;
            var broken = s.deals.Single(d => d.id == boughtId);
            // Constructed: the houseguest broke the information pact the price bought, in front of the house.
            broken.status = DealStatus.Broken; broken.brokenById = npc; broken.settledWeek = s.week;
            before = PinnedVoteSeason.Json(s);
            Assert.That(ProspectiveVoteFacade.SafetyStoreTryExpireLinkedPrice(s, boughtId, npc, out _), Is.False, "The vote price is not the safety store's to void.");
            Assert.That(PinnedVoteSeason.Json(s), Is.EqualTo(before));
        }

        /// <summary>
        /// The mode-2 storage check holds every stored safety row to the Safety policy, as mode 1's ValidateRecords
        /// does: with a malformed safety row stored, the policy offers nothing more and reads nothing.
        /// </summary>
        [Test]
        public void TheModeTwoStorageCheckHoldsEverySafetyRowToThePolicy()
        {
            var legacy = Fresh();
            legacy = Step(legacy, ProspectiveVoteTwins.Command(legacy, EpisodeCommandKind.PromiseSafety, Npc(legacy)));
            var s = ProspectiveVoteTwins.Twin(legacy);
            var stored = s.unifiedCommitments.Single(row => row.kind == UnifiedCommitments.Safety);
            var draft = stored.Clone(); draft.id = "promise-" + s.nextSequence; draft.beneficiaryId = Npc(s, 1);
            Assert.That(UnifiedCommitments.Offer(s, draft, false, out var error), Is.Not.Null, "Fixture: a lawful second word. " + error);
            stored.expiresWeek++;   // Constructed: the stored word's term no longer its policy's.
            Assert.That(UnifiedCommitments.Offer(s, draft, false, out error), Is.Null);
            Assert.That(error, Is.EqualTo("Invalid canonical safety term."));
            Assert.Throws<ArgumentException>(() => UnifiedCommitments.Find(s, stored.id), "No reader takes a malformed row as Safety.");
        }

        /// <summary>
        /// A counter priced with safety knows what it bought in mode 2 as in mode 1: the safety price finds the
        /// canonical vote pact it was paid for (Negotiation.BoughtWith), so the safety gateways weigh its breach as
        /// an accepted offer's (DealResolution.AcceptedOffer), as mode 1 weighs it.
        /// </summary>
        [Test]
        public void ACounterPricedWithSafetyKnowsWhatItBoughtInModeTwo()
        {
            var s = ProspectiveVoteTwins.Find("a first campaign the player votes in", seed => ProspectiveVoteTwins.Walk(seed,
                x => x.phase == EpisodePhase.Campaign && EpisodeEngine.Voters(x).Any(v => v.isPlayer)));
            string npc = EpisodeEngine.Voters(s).First(v => !v.isPlayer).id;
            double chance = 0;
            for (double warmth = 30; warmth <= 90 && chance < Negotiation.CounterFloor; warmth += 5)
            {
                ProspectiveVoteTwins.Set(s, npc, s.playerId, warmth); ProspectiveVoteTwins.Set(s, s.playerId, npc, warmth);
                chance = PlayerDeals.AcceptanceChance(s, npc, DealKind.VoteTogether, null);
            }
            Assert.That(chance, Is.GreaterThanOrEqualTo(Negotiation.CounterFloor), "Fixture: close enough to yes for a counter.");
            s.randomState = ProspectiveVoteTwins.Draw(false, chance);
            for (int attempt = s.nextSequence; ; attempt++)
            {
                Assert.That(attempt, Is.LessThan(s.nextSequence + 5000), "Fixture: a landing counter coin.");
                if (StoryRandom.Unit(s, Negotiation.CounterKey(s, npc, DealKind.VoteTogether, null, attempt)) < Negotiation.CounterChance) { s.nextSequence = attempt; break; }
            }
            var countered = Step(ProspectiveVoteTwins.Valid(s), ProspectiveVoteTwins.Command(s, EpisodeCommandKind.ProposeDeal, npc, null, DealKind.VoteTogether));
            Assert.That(Negotiation.OpenCounter(countered, npc)?.price.kind, Is.EqualTo(DealKind.SafetyAgreement), "Fixture: a vote pact priced with safety.");
            var both = ProspectiveVoteTwins.Both(countered, ProspectiveVoteTwins.Command(countered, EpisodeCommandKind.RespondToDeal, npc, text: EpisodeEngine.AcceptDeal));
            Accepted(both);
            string boughtId = Negotiation.CounterDealPrefix + countered.nextSequence, priceId = Negotiation.PricePrefix + countered.nextSequence;
            foreach (var (state, mode) in new[] { (both.legacy.state, "mode 1"), (both.prospective.state, "mode 2") })
            {
                var price = CommitmentReferences.FindDeal(state, priceId);
                Assert.That(price?.type, Is.EqualTo(DealKind.SafetyAgreement), mode + ": the safety price.");
                Assert.That(Negotiation.BoughtWith(state, price)?.id, Is.EqualTo(boughtId), mode + ": it knows the vote pact it bought.");
                Assert.That(Negotiation.FromACounter(state, price), Is.True, mode);
                Assert.That(DealResolution.AcceptedOffer(state, price), Is.True, mode + ": its breach weighs as an accepted offer's.");
            }
        }

        // ------------------------------------------------------------ the house's word and pacts

        /// <summary>
        /// The house's campaign passes with a houseguest Head of Household - the moment a mode-2 season used to
        /// stall: the house's word of safety to the Head of Household, and a pact struck with them, are canonical
        /// NPC rows in the source's order and identity.
        /// </summary>
        [Test]
        public void TheHousesWordAndPactsWithAHouseguestHeadOfHouseholdAreCanonicalRows()
        {
            var s = HouseHoH();
            // Constructed: one houseguest off the block and the Head of Household read each other at 60, so the
            // house strikes a pact of safety as well as giving its word.
            string courting = s.contestants.First(c => !c.isPlayer && c.status == ContestantStatus.Active && c.id != s.hohId
                && !s.nominees.Contains(c.id) && !s.Allied(c.id, s.hohId)).id;
            ProspectiveVoteTwins.Set(s, courting, s.hohId, 60); ProspectiveVoteTwins.Set(s, s.hohId, courting, 60);
            var both = ProspectiveVoteTwins.Both(ProspectiveVoteTwins.Valid(s), ProspectiveVoteTwins.Command(s, EpisodeCommandKind.Advance));
            Accepted(both);
            var after = both.prospective.state;
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.Campaign));
            var words = Rows(after, UnifiedCommitments.NpcPromise).ToList();
            var pacts = Rows(after, UnifiedCommitments.NpcDeal).ToList();
            Assert.That(words, Is.Not.Empty, "The house gave the Head of Household its word of safety,");
            Assert.That(pacts, Is.Not.Empty, "and struck a pact of safety with them.");
            foreach (var row in words)
            {
                Assert.That((row.beneficiaryId, row.expiresWeek, row.status), Is.EqualTo((s.hohId, s.week + 1, DealStatus.Active)), row.id);
                Assert.That(row.makerId, Is.Not.EqualTo(s.playerId), row.id);
                Assert.That(row.id, Does.StartWith("promise-npc-"));
            }
            foreach (var row in pacts)
            {
                Assert.That(row.makerId == s.hohId || row.beneficiaryId == s.hohId, Is.True, row.id + " is with the Head of Household.");
                Assert.That((row.expiresWeek, row.status), Is.EqualTo((s.week, DealStatus.Active)), row.id);
                Assert.That(row.id, Does.StartWith("deal-npc-"));
            }
            NoRawSafety(after);
            ProspectiveVoteTwins.AssertParity(both.legacy.state, after, "The campaign's passes with a houseguest Head of Household");
        }

        /// <summary>
        /// A safety pact a houseguest puts to the player Head of Household is a canonical Proposed offer, and
        /// the player's answer is written on that row: a yes binds it this week, a no changes only its consent.
        /// </summary>
        [TestCase(true)] [TestCase(false)]
        public void AHousesSafetyOfferIsACanonicalOfferAnsweredOnItsRow(bool accept)
        {
            var s = PlayerHoH();
            // Constructed: the house reads its Head of Household at 60, warm enough to court them with a pact.
            foreach (var npc in s.contestants.Where(c => !c.isPlayer && c.status == ContestantStatus.Active))
                ProspectiveVoteTwins.Set(s, npc.id, s.playerId, 60);
            var campaign = ProspectiveVoteTwins.Both(ProspectiveVoteTwins.Valid(s), ProspectiveVoteTwins.Command(s, EpisodeCommandKind.Advance));
            Accepted(campaign);
            var offers = Rows(campaign.prospective.state, UnifiedCommitments.NpcOffer).ToList();
            Assert.That(offers, Is.Not.Empty, "The house courted its Head of Household with a pact.");
            foreach (var put in offers)
                Assert.That((put.beneficiaryId, put.status, put.createdWeek, put.expiresWeek), Is.EqualTo((s.playerId, DealStatus.Proposed, s.week, s.week)), put.id);
            NoRawSafety(campaign.prospective.state);
            ProspectiveVoteTwins.AssertParity(campaign.legacy.state, campaign.prospective.state, "The house's offers");

            var table = campaign.legacy.state;
            var offer = offers.First();
            var both = ProspectiveVoteTwins.Both(table, ProspectiveVoteTwins.Command(table, EpisodeCommandKind.RespondToDeal, offer.id,
                text: accept ? EpisodeEngine.AcceptDeal : "decline"));
            Accepted(both);
            var row = both.prospective.state.unifiedCommitments.Single(r => r.id == offer.id);
            Assert.That((row.origin, row.createdWeek, row.makerId), Is.EqualTo((UnifiedCommitments.NpcOffer, offer.createdWeek, offer.makerId)));
            Assert.That((row.status, row.expiresWeek), Is.EqualTo(accept ? (DealStatus.Active, table.week) : (DealStatus.Declined, offer.expiresWeek)));
            NoRawSafety(both.prospective.state);
            ProspectiveVoteTwins.AssertParity(both.legacy.state, both.prospective.state, accept ? "A safety offer taken" : "A safety offer declined");

            var after = both.prospective.state;
            var again = ProspectiveVoteTwins.Command(after, EpisodeCommandKind.RespondToDeal, offer.id, text: EpisodeEngine.AcceptDeal, tag: "again");
            var refused = ProspectiveVoteFacade.Engine(after).Apply(again);
            Assert.That(refused.reason, Is.EqualTo("That offer is no longer on the table."));
            ProspectiveVoteTwins.AssertUntouched(after, refused, "An answered offer answered again");
        }

        /// <summary>
        /// Nor does a vote pact stand for a safety offer's term: with the player's vote pact with the houseguest
        /// binding this week - a story's, canonical in mode 2 - their safety offer is still the player's to take,
        /// as in mode 1, where the vote pact is a raw row the safety store never reads.
        /// </summary>
        [Test]
        public void AHousesSafetyOfferIsTakenBesideAVotePactOfTheSameTerm()
        {
            var s = PlayerHoH();
            foreach (var npc in s.contestants.Where(c => !c.isPlayer && c.status == ContestantStatus.Active))
                ProspectiveVoteTwins.Set(s, npc.id, s.playerId, 60);
            var campaign = Step(ProspectiveVoteTwins.Valid(s), ProspectiveVoteTwins.Command(s, EpisodeCommandKind.Advance));
            var offer = campaign.unifiedCommitments.First(row => row.kind == UnifiedCommitments.Safety && row.origin == UnifiedCommitments.NpcOffer
                && row.status == DealStatus.Proposed);
            // Through the story's own effect: the player and the houseguest made a vote pact this week.
            ApplyStoryEffect(campaign, new StoryEffectState { kind = StoryEffects.Deal, fromId = campaign.playerId, toId = offer.makerId,
                type = DealKind.VoteSave, thirdId = campaign.nominees.First(id => id != offer.makerId) });
            var pact = ProspectiveVoteTwins.Valid(campaign).deals.Single(d => d.type == DealKind.VoteSave && d.proposerId == campaign.playerId
                && d.recipientId == offer.makerId);
            Assert.That((pact.status, pact.expiresWeek), Is.EqualTo((DealStatus.Active, campaign.week)), "Fixture: the vote pact binds this week.");
            var both = ProspectiveVoteTwins.Both(campaign, ProspectiveVoteTwins.Command(campaign, EpisodeCommandKind.RespondToDeal, offer.id,
                text: EpisodeEngine.AcceptDeal));
            Accepted(both);
            var row = both.prospective.state.unifiedCommitments.Single(r => r.id == offer.id);
            Assert.That((row.status, row.expiresWeek), Is.EqualTo((DealStatus.Active, campaign.week)), "Taken, for the same week as the vote pact.");
            ProspectiveVoteTwins.AssertParity(both.legacy.state, both.prospective.state, "A safety offer taken beside a vote pact");
        }

        /// <summary>A story's word of safety and its safety pact, applied as a beat applies them: canonical story rows, once.</summary>
        [TestCase(StoryEffects.Promise)] [TestCase(StoryEffects.Deal)]
        public void AStorysSafetyWordAndPactAreCanonicalStoryRows(string effect)
        {
            var legacy = Fresh();
            var e = effect == StoryEffects.Promise
                ? new StoryEffectState { kind = StoryEffects.Promise, fromId = Npc(legacy, 0), toId = Npc(legacy, 1), type = PromiseKind.Safety.ToString() }
                : new StoryEffectState { kind = StoryEffects.Deal, fromId = legacy.playerId, toId = Npc(legacy, 0), type = DealKind.SafetyAgreement };
            var prospective = ProspectiveVoteTwins.Twin(legacy);
            long sequence = legacy.nextSequence;
            ApplyStoryEffect(legacy, e); ApplyStoryEffect(prospective, e);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(prospective, out var error), Is.True, error);
            string origin = effect == StoryEffects.Promise ? UnifiedCommitments.StoryPromise : UnifiedCommitments.StoryDeal;
            var row = Rows(prospective, origin).Single();
            Assert.That((row.id, row.makerId, row.beneficiaryId),
                Is.EqualTo(((effect == StoryEffects.Promise ? "promise-" : "deal-story-") + sequence, e.fromId, e.toId)));
            NoRawSafety(prospective);
            ProspectiveVoteTwins.AssertParity(legacy, prospective, "A story's safety " + effect);

            ApplyStoryEffect(legacy, e); ApplyStoryEffect(prospective, e);
            Assert.That(Rows(prospective, origin).Count(), Is.EqualTo(1), "The same story effect again writes nothing more.");
            ProspectiveVoteTwins.AssertParity(legacy, prospective, "A story's safety " + effect + " again");
        }

        /// <summary>One story effect, applied the way a beat applies it (EpisodeEngine.ApplyStoryEffect).</summary>
        private static void ApplyStoryEffect(EpisodeState s, StoryEffectState effect)
        {
            var method = typeof(EpisodeEngine).GetMethod("ApplyStoryEffect", BindingFlags.Static | BindingFlags.NonPublic, null,
                new[] { typeof(EpisodeState), typeof(StoryEffectState), typeof(StorylineState), typeof(string), typeof(string), typeof(int), typeof(bool) }, null);
            Assert.That(method, Is.Not.Null, "The engine's ApplyStoryEffect, the story's one effect router.");
            method.Invoke(null, new object[] { s, effect, null, null, "vote-v3b", 0, false });
        }

        // ------------------------------------------------------------ the gateways

        /// <summary>
        /// The player as Head of Household names the houseguest they gave their word, or struck a pact, of
        /// safety: the canonical row is broken by the nomination, with mode 1's effect identity and effects;
        /// a broken pact also leaves the hearing lineage the complete core asks of an audible breach.
        /// </summary>
        [TestCase(false)] [TestCase(true)]
        public void NominatingTheOneThePlayerPromisedSafetyBreaksTheCanonicalRow(bool pact)
        {
            var s = Nominating();
            var pool = EpisodeEngine.NominationCandidates(s).Where(c => !c.isPlayer).Select(c => c.id).ToList();
            string promised = pool[0], other = pool[1];
            s = pact ? Pact(s, promised) : Step(s, ProspectiveVoteTwins.Command(s, EpisodeCommandKind.PromiseSafety, promised));
            string id = (pact ? "deal-player-" : "promise-") + (s.nextSequence - 1);
            Assert.That(s.unifiedCommitments.Any(row => row.kind == UnifiedCommitments.Safety && row.status == DealStatus.Active
                && row.makerId == s.playerId && row.beneficiaryId == promised), Is.True, "Fixture: the word stands.");

            var both = ProspectiveVoteTwins.Both(s, ProspectiveVoteTwins.Command(s, EpisodeCommandKind.Nominate, promised, other));
            Accepted(both);
            var after = both.prospective.state;
            var row = after.unifiedCommitments.Single(r => r.kind == UnifiedCommitments.Safety && r.beneficiaryId == promised);
            Assert.That((row.status, row.brokenById, row.settledWeek), Is.EqualTo((DealStatus.Broken, s.playerId, s.week)), "Broken by the nomination.");
            Assert.That(row.settlementEffectKey, Does.StartWith("safety:" + s.week + ":"));
            Assert.That(row.settlementEffectKey, Does.Contain("nomination"));
            if (pact)
            {
                var evidence = after.unifiedHearingEvidence.Single(e => e.fact.refId == row.id);
                Assert.That(evidence.incidentKey, Is.EqualTo(row.settlementEffectKey), "The audible breach's fact is archived,");
                Assert.That(after.unifiedHearingReceipts.Any(r => r.kind == UnifiedCommitmentHearings.Initial && r.listenerId == promised
                    && r.incidentKey == row.settlementEffectKey), Is.True, "and the one it wronged heard it first.");
            }
            NoRawSafety(after);
            ProspectiveVoteTwins.AssertParity(both.legacy.state, after, pact ? "A nominated pact" : "A nominated promise");
        }

        /// <summary>
        /// At the veto meeting the player as Head of Household names a replacement. A pact with somebody they
        /// spared is kept on its canonical row; one with the replacement they named is broken by that naming.
        /// </summary>
        [TestCase(false)] [TestCase(true)]
        public void APactIsKeptBySparingAndBrokenByNamingTheReplacement(bool named)
        {
            var meeting = Meeting(out string partner);
            var command = EpisodeEngineTests.NextCommand(meeting);
            Assert.That(command.kind, Is.EqualTo(EpisodeCommandKind.ResolveVeto), "Fixture: the player names the replacement.");
            var candidates = EpisodeEngine.ReplacementCandidates(meeting).Select(c => c.id).ToList();
            command.secondTargetId = named ? partner : candidates.First(id => id != partner);
            var both = ProspectiveVoteTwins.Both(meeting, command);
            Accepted(both);
            var after = both.prospective.state;
            var row = after.unifiedCommitments.Single(r => r.kind == UnifiedCommitments.Safety && r.origin == UnifiedCommitments.PlayerDeal
                && r.beneficiaryId == partner);
            if (named)
            {
                Assert.That((row.status, row.brokenById, row.settledWeek), Is.EqualTo((DealStatus.Broken, meeting.playerId, meeting.week)));
                Assert.That(row.settlementEffectKey, Does.Contain("replacement"));
            }
            else Assert.That((row.status, row.brokenById, row.settledWeek), Is.EqualTo((DealStatus.Fulfilled, (string)null, meeting.week)), "Kept: spared.");
            NoRawSafety(after);
            ProspectiveVoteTwins.AssertParity(both.legacy.state, after, named ? "A pact broken by the replacement" : "A pact kept by sparing");
        }

        /// <summary>
        /// The veto meeting a player Head of Household decides with a replacement to name, and a safety pact the
        /// player struck before the nominations with a houseguest they did not name: the real season to there.
        /// </summary>
        private static EpisodeState Meeting(out string partner)
        {
            string found = null;
            var meeting = ProspectiveVoteTwins.Find("a veto meeting the player names a replacement at, a pact standing", seed =>
            {
                var s = ProspectiveVoteTwins.Walk(seed, x => x.phase == EpisodePhase.Nomination && x.hohId == x.playerId && x.nominees.Count == 0,
                    EpisodePhase.HoH);
                if (s == null) return null;
                var pool = EpisodeEngine.NominationCandidates(s).Where(c => !c.isPlayer).Select(c => c.id).ToList();
                if (pool.Count < 4) return null;
                string spared = pool[2];
                s.randomState = ProspectiveVoteTwins.Draw(true, PlayerDeals.AcceptanceChance(s, spared, DealKind.SafetyAgreement, null));
                var engine = new EpisodeEngine(s);
                if (!engine.Apply(ProspectiveVoteTwins.Command(s, EpisodeCommandKind.ProposeDeal, spared, null, DealKind.SafetyAgreement)).accepted) return null;
                var named = engine.Snapshot;
                if (!engine.Apply(ProspectiveVoteTwins.Command(named, EpisodeCommandKind.Nominate, pool[0], pool[1])).accepted) return null;
                for (int step = 0; step < 40; step++)
                {
                    var now = engine.Snapshot;
                    if (now.phase == EpisodePhase.VetoMeeting && !now.vetoResolved)
                    {
                        var next = EpisodeEngineTests.NextCommand(now);
                        bool replaces = next.kind == EpisodeCommandKind.ResolveVeto && next.useVeto
                            && EpisodeEngine.ReplacementCandidates(now).Any(c => c.id == spared)
                            && EpisodeEngine.ReplacementCandidates(now).Count() > 1
                            && now.unifiedCommitments.Any(r => r.beneficiaryId == spared && r.status == DealStatus.Active);
                        if (!replaces) return null;
                        found = spared;
                        return now;
                    }
                    if (ProspectiveVoteTwins.Revealed(now) || !engine.Apply(EpisodeEngineTests.NextCommand(now)).accepted) return null;
                }
                return null;
            });
            partner = found;
            return meeting.Clone();
        }

        /// <summary>
        /// Each expiry boundary ends only Safety rows: beside them, the Vote family's rows - which the same
        /// predicate would also end - are left to their own endings (vote family V4).
        /// </summary>
        [TestCase(UnifiedCommitmentExpiry.PromiseWeekTurn)] [TestCase(UnifiedCommitmentExpiry.DealPass)]
        [TestCase(UnifiedCommitmentExpiry.Departure)] [TestCase(UnifiedCommitmentExpiry.Expulsion)]
        public void EachExpiryBoundaryEndsOnlyTheSafetyRows(UnifiedCommitmentExpiry boundary)
        {
            var s = Mixed(out string partner);
            var copy = s.Clone();
            string departed = null;
            if (boundary == UnifiedCommitmentExpiry.PromiseWeekTurn || boundary == UnifiedCommitmentExpiry.DealPass) copy.week += 2;
            else
            {
                // Constructed: the partner has left, as the departure or the production removal would leave them.
                departed = partner;
                copy.Find(partner).status = boundary == UnifiedCommitmentExpiry.Expulsion ? ContestantStatus.Expelled : ContestantStatus.Evicted;
            }
            string before = PinnedVoteSeason.Json(copy);
            var evaluation = UnifiedCommitments.Expire(copy, boundary, departed);
            Assert.That(PinnedVoteSeason.Json(copy), Is.EqualTo(before), "The policy writes nothing.");
            var ended = evaluation.Changes.Select(change => change.Record).ToList();
            Assert.That(ended, Is.Not.Empty, "Fixture: the boundary ends a safety row.");
            Assert.That(ended.All(row => row.kind == UnifiedCommitments.Safety && row.status == DealStatus.Expired), Is.True, "Safety rows only.");
            var policy = boundary == UnifiedCommitmentExpiry.PromiseWeekTurn ? UnifiedCommitments.PromisePolicy : UnifiedCommitments.DealPolicy;
            Assert.That(copy.unifiedCommitments.Any(row => row.kind == UnifiedVoteTogether.Vote && row.sourcePolicy == policy
                && DealStatus.Binds(row.status) && (departed == null || row.makerId == departed || row.beneficiaryId == departed)), Is.True,
                "Fixture: a Vote row the same predicate would end stands beside them.");
            // Mode 1 ends the same Safety rows of the same season.
            var legacy = s.Clone(); legacy.unifiedCommitmentRulesVersion = UnifiedCommitments.ProspectiveVersion;
            legacy.unifiedCommitments.RemoveAll(row => row.kind != UnifiedCommitments.Safety);
            if (departed == null) legacy.week = copy.week; else legacy.Find(departed).status = copy.Find(departed).status;
            Assert.That(UnifiedCommitments.Expire(legacy, boundary, departed).Changes.Select(change => change.Record.id),
                Is.EqualTo(ended.Select(row => row.id)), "Mode 1 ends exactly these.");
        }

        /// <summary>
        /// The house's deal pass as the second week's campaign opens - the real Advance, through the seam - ends
        /// the player's week-one safety pact, as mode 1's ends it. The twin is projected past the week-one
        /// reveal with its observed owners and frames (<see cref="PinnedVoteSeason"/>).
        /// </summary>
        [Test]
        public void TheCampaignsDealPassEndsAWeekOldSafetyPactAsModeOneDoes()
        {
            var season = SecondWeek(out string partner, out string pactId);
            var mode1 = season.State;
            var twin = PinnedVoteSeason.Project(mode1, season.Owners, season.Frames);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(twin, out var error), Is.True, error);
            Assert.That(twin.unifiedCommitments.Single(row => row.id == pactId).status, Is.EqualTo(DealStatus.Active), "Fixture: the pact stands past its week.");

            var advance = ProspectiveVoteTwins.Command(mode1, EpisodeCommandKind.Advance);
            var legacy = season.Apply(advance);
            var prospective = ProspectiveVoteFacade.Engine(twin).Apply(advance);
            Accepted((legacy, prospective));
            Assert.That(prospective.state.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(prospective.state, out error), Is.True, error);
            var pact = prospective.state.unifiedCommitments.Single(row => row.id == pactId);
            Assert.That((pact.status, pact.beneficiaryId, pact.settledWeek), Is.EqualTo((DealStatus.Expired, partner, 0)), "The deal pass ended it: no word broken or kept.");
            NoRawSafety(prospective.state);
            ProspectiveVoteTwins.AssertProjection(PinnedVoteSeason.Project(legacy.state, season.Owners, season.Frames), prospective.state,
                "The second campaign's deal pass");
        }

        /// <summary>
        /// The second week's decided veto meeting, the player's week-one safety pact still standing until the
        /// campaign's deal pass: the real season to there with its week-one vote pinned, and every vote offer the
        /// house put to the player declined by the player's real answer, so no Vote row waits on the deal pass
        /// mode 2 cannot yet run for the Vote family (vote family V4).
        /// </summary>
        private static PinnedVoteSeason SecondWeek(out string partner, out string pactId)
        {
            PinnedVoteSeason found = null; string with = null, id = null;
            ProspectiveVoteTwins.Find("a second week's decided veto meeting with a week-one safety pact standing", seed =>
            {
                var fresh = PinnedVoteSeason.Fresh(seed);
                string npc = Npc(fresh, 2);
                fresh.randomState = ProspectiveVoteTwins.Draw(true, PlayerDeals.AcceptanceChance(fresh, npc, DealKind.SafetyAgreement, null));
                var season = new PinnedVoteSeason(seed, fresh);
                string pact = "deal-player-" + fresh.nextSequence;
                if (!season.Apply(ProspectiveVoteTwins.Command(fresh, EpisodeCommandKind.ProposeDeal, npc, null, DealKind.SafetyAgreement)).accepted)
                    return null;
                for (int step = 0; step < 400; step++)
                {
                    var s = season.State;
                    if (s.week == 2 && s.phase == EpisodePhase.VetoMeeting && s.vetoResolved)
                    {
                        if (!season.Supported || s.unifiedCommitments.All(row => row.id != pact || row.status != DealStatus.Active)
                            || s.deals.Any(d => KnownBallots.IsVoteDeal(d.type) && DealStatus.Binds(d.status))
                            || s.promises.Any(p => p.kind == PromiseKind.Vote && p.status == PromiseStatus.Active)) return null;
                        found = season; with = npc; id = pact;
                        return s;
                    }
                    if (s.week > 2) return null;
                    if (s.week == 1 && PinnedVoteSeason.OpenVote(s))
                    {
                        string evict = s.nominees.FirstOrDefault(n => n != npc && n != s.playerId);
                        if (evict == null) return null;
                        season.PlayPinnedVote(evict);
                        continue;
                    }
                    var offer = s.deals.FirstOrDefault(d => d.status == DealStatus.Proposed && d.recipientId == s.playerId
                        && KnownBallots.IsVoteDeal(d.type) && d.id.StartsWith(NpcDeals.OfferPrefix, StringComparison.Ordinal));
                    var command = offer != null
                        ? ProspectiveVoteTwins.Command(s, EpisodeCommandKind.RespondToDeal, offer.id, text: "decline")
                        : EpisodeEngineTests.NextCommand(s);
                    if (!season.Apply(command).accepted) return null;
                }
                return null;
            });
            partner = with; pactId = id;
            return found;
        }

        /// <summary>
        /// Production removes a houseguest the player gave their word and a pact of safety: the real removal
        /// (EpisodeEngine.Expel) ends both canonical rows in mode 2 as in mode 1, through the expulsion gateway.
        /// Called directly in week one: production's removal window opens only after a reveal, which a mode-2
        /// season cannot yet cross (vote family V4).
        /// </summary>
        [Test]
        public void AProductionRemovalEndsTheRemovedHouseguestsSafetyRowsAsModeOneDoes()
        {
            var legacy = Fresh(); string removed = Npc(legacy);
            legacy = Step(legacy, ProspectiveVoteTwins.Command(legacy, EpisodeCommandKind.PromiseSafety, removed));
            legacy = Pact(legacy, removed);
            var prospective = ProspectiveVoteTwins.Twin(legacy);
            Expel(legacy, removed); Expel(prospective, removed);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(prospective, out var error), Is.True, error);
            var ended = prospective.unifiedCommitments.Where(row => row.beneficiaryId == removed).ToList();
            Assert.That(ended.Select(row => row.sourcePolicy).OrderBy(policy => policy, StringComparer.Ordinal),
                Is.EqualTo(new[] { UnifiedCommitments.DealPolicy, UnifiedCommitments.PromisePolicy }), "Fixture: a word and a pact of safety.");
            Assert.That(ended.All(row => row.status == DealStatus.Expired && row.settledWeek == 0), Is.True, "The removal ended both: nothing broken or kept.");
            NoRawSafety(prospective);
            ProspectiveVoteTwins.AssertParity(legacy, prospective, "A production removal");
        }

        /// <summary>
        /// A safety price is void once the one it was owed to breaks what it bought (C7): in mode 2 the safety store
        /// lapses the canonical price, and the two of them are told, as in mode 1. The price is a refused target
        /// pact's counter; the breach is constructed, and the engine's own VoidThePrice judges it.
        /// </summary>
        [Test]
        public void AVoidedSafetyPriceLapsesAsModeOneLapsesIt()
        {
            var s = Fresh(); string npc = Npc(s), about = Npc(s, 1);
            double chance = 0;
            for (double warmth = 30; warmth <= 90 && chance < Negotiation.CounterFloor; warmth += 5)
            {
                ProspectiveVoteTwins.Set(s, npc, s.playerId, warmth); ProspectiveVoteTwins.Set(s, s.playerId, npc, warmth);
                chance = PlayerDeals.AcceptanceChance(s, npc, DealKind.TargetAgreement, about);
            }
            Assert.That(chance, Is.GreaterThanOrEqualTo(Negotiation.CounterFloor), "Fixture: close enough to yes for a counter.");
            s.randomState = ProspectiveVoteTwins.Draw(false, chance);
            for (int attempt = s.nextSequence; ; attempt++)
            {
                Assert.That(attempt, Is.LessThan(s.nextSequence + 5000), "Fixture: a landing counter coin.");
                if (StoryRandom.Unit(s, Negotiation.CounterKey(s, npc, DealKind.TargetAgreement, about, attempt)) < Negotiation.CounterChance) { s.nextSequence = attempt; break; }
            }
            var countered = Step(ProspectiveVoteTwins.Valid(s), ProspectiveVoteTwins.Command(s, EpisodeCommandKind.ProposeDeal, npc, about, DealKind.TargetAgreement));
            Assert.That(Negotiation.OpenCounter(countered, npc)?.price.kind, Is.EqualTo(DealKind.SafetyAgreement), "Fixture: a target pact priced with safety.");
            var legacy = Step(countered, ProspectiveVoteTwins.Command(countered, EpisodeCommandKind.RespondToDeal, npc, text: EpisodeEngine.AcceptDeal));
            string boughtId = Negotiation.CounterDealPrefix + countered.nextSequence, priceId = Negotiation.PricePrefix + countered.nextSequence;
            var prospective = ProspectiveVoteTwins.Twin(legacy);
            foreach (var state in new[] { legacy, prospective })
            {
                // Constructed: the houseguest broke the target pact the price bought, in front of the house.
                var bought = state.deals.Single(d => d.id == boughtId);
                bought.status = DealStatus.Broken; bought.brokenById = npc; bought.settledWeek = state.week;
                VoidThePrice(state, bought, npc);
            }
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(prospective, out var error), Is.True, error);
            var price = prospective.unifiedCommitments.Single(row => row.id == priceId);
            Assert.That((price.kind, price.status, price.settledWeek), Is.EqualTo((UnifiedCommitments.Safety, DealStatus.Expired, 0)), "The price lapsed: nothing broken.");
            Assert.That(prospective.events.Last().kind, Is.EqualTo("deal-outcome"), "And the two of them were told.");
            ProspectiveVoteTwins.AssertParity(legacy, prospective, "A voided safety price");
        }

        /// <summary>EpisodeEngine.VoidThePrice, as a breach's settlement calls it.</summary>
        private static void VoidThePrice(EpisodeState s, DealState bought, string breakerId)
        {
            var method = typeof(EpisodeEngine).GetMethod("VoidThePrice", BindingFlags.Static | BindingFlags.NonPublic, null,
                new[] { typeof(EpisodeState), typeof(DealState), typeof(string) }, null);
            Assert.That(method, Is.Not.Null, "The engine's price void.");
            try { method.Invoke(null, new object[] { s, bought, breakerId }); }
            catch (TargetInvocationException wrapped) when (wrapped.InnerException != null)
            {
                Assert.Fail("VoidThePrice: " + wrapped.InnerException.Message);
            }
        }

        /// <summary>
        /// The house hears of the pact the player broke by nominating, as in mode 1: the gossip that carries the
        /// broken word to a houseguest records that hearing in mode 2's lineage too (HeardOfYourWord), and every
        /// step to it plays as mode 1's.
        /// </summary>
        [Test]
        public void TheHouseHearsOfAPactThePlayerBrokeAsInModeOne()
        {
            EpisodeState legacy = null, prospective = null;
            ProspectiveVoteTwins.Find("a pact the player broke by nominating that the house then hears of", seed =>
            {
                var s = ProspectiveVoteTwins.Walk(seed, x => x.phase == EpisodePhase.Nomination && x.hohId == x.playerId && x.nominees.Count == 0,
                    EpisodePhase.HoH);
                if (s == null) return null;
                var pool = EpisodeEngine.NominationCandidates(s).Where(c => !c.isPlayer).Select(c => c.id).ToList();
                s = Pact(s, pool[0]);
                var nominate = ProspectiveVoteTwins.Command(s, EpisodeCommandKind.Nominate, pool[0], pool[1]);
                var one = new EpisodeEngine(s);
                var two = ProspectiveVoteFacade.Engine(ProspectiveVoteTwins.Twin(s));
                var command = nominate;
                for (int step = 0; step < 30; step++)
                {
                    var left = one.Apply(command); var right = two.Apply(command);
                    Assert.That(left.accepted, Is.True, "Mode 1: " + left.reason);
                    Assert.That(right.accepted, Is.True, "Mode 2: " + right.reason);
                    ProspectiveVoteTwins.AssertParity(left.state, right.state, "Step " + step + ", " + command.kind);
                    if (left.state.unifiedHearingReceipts.Any(r => r.kind == UnifiedCommitmentHearings.Spread))
                    {
                        legacy = left.state; prospective = right.state;
                        return left.state;
                    }
                    command = EpisodeEngineTests.NextCommand(left.state);
                    if (PinnedVoteSeason.OpenVote(left.state)) return null;
                }
                return null;
            });
            var heard = prospective.unifiedHearingReceipts.Where(r => r.kind == UnifiedCommitmentHearings.Spread).ToList();
            Assert.That(heard, Is.Not.Empty, "A houseguest heard of the broken pact, and mode 2 recorded the hearing.");
            Assert.That(heard.All(r => prospective.unifiedHearingEvidence.Any(e => e.incidentKey == r.incidentKey)), Is.True, "Each of its actual archived leaf.");
        }

        /// <summary>EpisodeEngine.Expel: production's removal, as the social window's close carries it out.</summary>
        private static void Expel(EpisodeState s, string id)
        {
            var method = typeof(EpisodeEngine).GetMethod("Expel", BindingFlags.Static | BindingFlags.NonPublic, null,
                new[] { typeof(EpisodeState), typeof(string) }, null);
            Assert.That(method, Is.Not.Null, "The engine's production removal.");
            try { method.Invoke(null, new object[] { s, id }); }
            catch (TargetInvocationException wrapped) when (wrapped.InnerException != null)
            {
                Assert.Fail("Expel: " + wrapped.InnerException.Message);
            }
        }

        /// <summary>
        /// A campaign's mode-2 twin holding both families around one nominee: their canonical word on the vote and
        /// vote pact with a voter, written by the house's real campaign passes, and the player's canonical promise
        /// and pact of safety with them, through the real commands on the seam.
        /// </summary>
        private static EpisodeState Mixed(out string partner)
        {
            var s = HouseHoH();
            string nominee = s.nominees[0], voter = EpisodeEngine.Voters(s).First(v => !v.isPlayer).id;
            // Constructed, as the house's vote fixtures construct it: the nominee and a voter read each other at 45.
            ProspectiveVoteTwins.Set(s, nominee, voter, 45); ProspectiveVoteTwins.Set(s, voter, nominee, 45);
            var campaign = Step(ProspectiveVoteTwins.Valid(s), ProspectiveVoteTwins.Command(s, EpisodeCommandKind.Advance));
            var engine = ProspectiveVoteFacade.Engine(ProspectiveVoteTwins.Twin(campaign));
            var promised = engine.Apply(ProspectiveVoteTwins.Command(engine.Snapshot, EpisodeCommandKind.PromiseSafety, nominee));
            Assert.That(promised.accepted, Is.True, "Fixture: " + promised.reason);
            var now = engine.Snapshot;
            now.randomState = ProspectiveVoteTwins.Draw(true, PlayerDeals.AcceptanceChance(now, nominee, DealKind.SafetyAgreement, null));
            engine = ProspectiveVoteFacade.Engine(now);
            var pact = engine.Apply(ProspectiveVoteTwins.Command(now, EpisodeCommandKind.ProposeDeal, nominee, null, DealKind.SafetyAgreement));
            Assert.That(pact.accepted, Is.True, "Fixture: " + pact.reason);
            var mixed = engine.Snapshot;
            Assert.That(mixed.unifiedCommitments.Count(row => row.kind == UnifiedCommitments.Safety && row.beneficiaryId == nominee), Is.EqualTo(2),
                "Fixture: the player's word and pact of safety with the nominee.");
            Assert.That(mixed.unifiedCommitments.Any(row => row.kind == UnifiedVoteTogether.Vote && row.makerId == nominee
                && row.sourcePolicy == UnifiedCommitments.PromisePolicy), Is.True, "Fixture: the nominee's word on the vote.");
            Assert.That(mixed.unifiedCommitments.Any(row => row.kind == UnifiedVoteTogether.Vote && row.makerId == nominee
                && row.sourcePolicy == UnifiedCommitments.DealPolicy), Is.True, "Fixture: the nominee's vote pact.");
            partner = nominee;
            return mixed;
        }
    }
}
