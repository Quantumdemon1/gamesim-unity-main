using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The holder's offer, "I'll use it on you, for a price" (ROLE-MOMENTS-PLAN R8, §3.4b), through the public engine.
    ///
    /// <para>The ladder: story rules 15 (<see cref="StoryRules.HolderOffer"/>) is defined ahead of 13 and 14 by the one
    /// documented exception (the owner, 2026-11-17), and no season the game starts plays it.</para>
    ///
    /// <para>Filing: under the holder's offer, a houseguest who holds the veto offers to use it on the player, who is on the
    /// block, as the meeting opens - roll-free, once a week, and only with every condition the plan names, the save within
    /// reach once their word is given. The price is the player's: a voting bloc at this week's vote, or at the endgame a
    /// final two.</para>
    ///
    /// <para>Answering: a yes strikes the price in every commitment mode, mode 2's as a canonical row of its own origin. The
    /// offer and a Save plea to the holder substitute for each other. The holder's decision judges the word: kept, or broken
    /// by the holder with the player's price void. A no and silence cost the holder's tone's.</para>
    ///
    /// <para>Validation refuses every record of it in a season without it. A season at Roles - every season the game starts -
    /// never sees one. Pure simulation: the Unity-free subset runs it.</para>
    /// </summary>
    public sealed class RoleHolderOfferTests
    {
        // ------------------------------------------------------------------ the ladder

        [Test]
        public void FifteenIsTheHolderOffersDefinedAheadOfThirteenAndFourteen()
        {
            Assert.That(StoryRules.HolderOffer, Is.EqualTo(15), "A role moment above the house talk's 12 to 14 (the owner, 2026-10-30).");
            Assert.That(StoryRules.IsDefined(StoryRules.HolderOffer), Is.True, "Defined by the documented exception (the owner, 2026-11-17).");
            Assert.That(StoryRules.IsDefined(StoryRules.Gossip), Is.False, "13 stays refused until its stack defines it.");
            Assert.That(StoryRules.IsDefined(StoryRules.ComesToYou), Is.False, "14 stays refused until its stack defines it.");
            Assert.That(StoryRules.IsDefined(StoryRules.HolderOffer + 1), Is.False, "Nothing past 15 is defined.");
            Assert.That(StoryRules.Current, Is.LessThan(StoryRules.HolderOffer), "No season the game starts plays it before its enrolment.");
            var shipped = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 1515u);
            ShippedRules.ApplyFresh(shipped);
            Assert.That(EpisodeEngine.HolderOfferOn(shipped), Is.False, "A season the game starts today plays no holder's offer.");
            Assert.That(EpisodeEngine.HolderOfferOn(null), Is.False);
        }

        /// <summary>A live season may hold 15: a test opts in by writing it after ApplyFresh, and the validator accepts it, repairing nothing.</summary>
        [Test]
        public void ALiveSeasonAtTheHolderOfferIsValid()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 1516u);
            ShippedRules.ApplyFresh(s);
            s.story.rulesVersion = StoryRules.HolderOffer;
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
            Assert.That(EpisodeEngine.HolderOfferOn(s), Is.True);
        }

        /// <summary>Every frozen reader keeps its own ladder, which never reaches 15, so a historical save can never hold it.</summary>
        [TestCase("FrozenEpisodeV25")] [TestCase("FrozenEpisodeV26")] [TestCase("FrozenEpisodeV27")] [TestCase("FrozenEpisodeV28")]
        public void EveryFrozenReaderRefusesAHistoricalSeasonAtTheHolderOffer(string reader) =>
            RefusedAt.Invoke(null, new object[] { reader, StoryRules.HolderOffer });

        private static readonly MethodInfo RefusedAt =
            typeof(RoleRulesTests).GetMethod("RefusedAt", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string), typeof(int) }, null)
            ?? throw new InvalidOperationException("RoleRulesTests.RefusedAt is the frozen readers' refusal check.");

        // ------------------------------------------------------------------ filing

        /// <summary>
        /// The holder offers as the meeting opens, once, as the plan's row: a veto commitment proposed by them to the player,
        /// binding this week, Critical at stake, with the holder's offer's own id; the player is told, and nothing is drawn.
        /// </summary>
        [Test]
        public void TheHolderOffersAsThePlansRow([Values(0, 1, 2)] int mode)
        {
            var meeting = Meeting(mode, out string holder).Snapshot;
            var offer = OfferOf(meeting);
            Assert.That((offer.type, offer.status, offer.week, offer.expiresWeek, offer.trustImpact, offer.proposerId, offer.recipientId, offer.targetId, offer.linkedDealId),
                Is.EqualTo((DealKind.VetoUse, DealStatus.Proposed, meeting.week, meeting.week, DealTrust.Critical, holder, meeting.playerId, (string)null, (string)null)));
            Assert.That(offer.id, Does.StartWith(NpcDeals.HolderOfferPrefix));
            Assert.That(meeting.deals.Count(d => NpcDeals.IsHolderOfferId(d.id)), Is.EqualTo(1), "One a week.");
            Assert.That(meeting.events.Count(e => e.kind == "deal" && e.text == meeting.Find(holder).name + " holds the veto and will use it on you, for a price."),
                Is.EqualTo(1), "The player is told.");
            Assert.That(RoleOffers.IsHolderOffer(meeting, offer), Is.True);
            Assert.That(RoleOffers.IsBoardAsk(meeting, offer), Is.False, "Never a card of the veto board, which only a holding player sees.");
            Assert.That(NpcInvitation.Available(meeting).SingleOrDefault(i => i.RecordId == offer.id)?.OpeningLine,
                Is.EqualTo("Can we talk? I've got the veto, and I could use it on you."), "It comes to the player as an incoming offer.");
            Assert.That(RoleOffers.MomentOf(offer), Is.EqualTo(RoleQuotes.HolderOffer));
            Assert.That(RoleOffers.ToneOf(meeting, offer), Is.EqualTo(RoleTone.Of(meeting, holder, RoleTone.Role.VetoHolder)), "The holder's tone, as holder.");
            Assert.That(EpisodeValidation.TryValidate(meeting, out string error), Is.True, error);

            var s = Basis(meeting);
            uint before = s.randomState;
            NpcDeals.OfferTheVeto(s);
            Assert.That(s.randomState, Is.EqualTo(before), "Filing draws nothing from the season's stream.");
            Assert.That(s.deals.Count(d => NpcDeals.IsHolderOfferId(d.id)), Is.EqualTo(1));
        }

        /// <summary>
        /// A season at Roles, the same week: nobody offers, and the meeting holds exactly what it held before R8. Every season
        /// the game starts is such a season.
        /// </summary>
        [Test]
        public void AtRolesNobodyOffers()
        {
            var s = BeforeTheMeeting(2, out _);
            s.story.rulesVersion = StoryRules.Roles;
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
            var meeting = RoleVetoBoardTests.Apply(new EpisodeEngine(s), RoleVetoBoardTests.Command(s, EpisodeCommandKind.Advance));
            Assert.That(meeting.deals.Any(d => NpcDeals.IsHolderOfferId(d.id)), Is.False);
            Assert.That(meeting.events.Any(e => e.text != null && e.text.Contains("holds the veto and will use it on you")), Is.False);
        }

        /// <summary>Every condition is needed (§3.4b): each change below, alone, keeps the holder from offering, while the same state without it offers.</summary>
        [Test]
        public void EachConditionAloneKeepsTheOfferAway()
        {
            var meeting = Meeting(2, out string holder).Snapshot;
            Assert.That(Filed(Basis(meeting)), Is.True, "The control: the same state offers.");
            string other = meeting.nominees.Single(id => id != meeting.playerId);
            string bystander = meeting.Active.First(c => !c.isPlayer && c.id != holder && c.id != meeting.hohId && !meeting.nominees.Contains(c.id)).id;
            var cases = new (string name, Action<EpisodeState> change)[]
            {
                ("without the holder's offer", s => s.story.rulesVersion = StoryRules.Roles),
                ("the player holds the veto", s => s.vetoHolderId = s.playerId),
                ("the holder is the Head of Household", s => s.vetoHolderId = s.hohId),
                ("the holder is on the block", s => s.vetoHolderId = other),
                ("the holder has left the house", s => s.Find(holder).status = ContestantStatus.Expelled),
                ("the veto is decided", s => s.vetoResolved = true),
                ("the player is not on the block", s => s.nominees[s.nominees.IndexOf(s.playerId)] = bystander),
                ("the player is out of the game", s => s.Find(s.playerId).status = ContestantStatus.Expelled),
                ("too cold to bargain", s => Set(s, holder, s.playerId, NpcDeals.DealingFloor - 1)),
                ("a veto word already binds them", s => s.deals.Add(Word(s, holder, DealKind.VetoUse, "deal-story-9001"))),
                ("the player holds a favour over the holder", s => s.story.hooks.Add(new HookState
                {
                    id = "hook-r8", holderId = s.playerId, overId = holder, week = 1,
                })),
                ("a Save plea already put to them this week", s => s.lobbies.Add(new LobbyState
                {
                    week = s.week, phase = EpisodePhase.Veto, deciderId = holder, ask = LobbyAsk.Save, subjectId = s.playerId,
                    approach = LobbyApproach.Emotional, response = LobbyResponse.Receptive,
                })),
                ("a grudge of sixty: the holder will not save them", s => s.story.grudges.Add(new GrudgeState
                {
                    holderId = holder, targetId = s.playerId, cause = GrudgeCauses.Story, severity = 60, originWeek = 1, count = 1,
                })),
                ("the save out of reach even with the word", s =>
                {
                    s.deals.Add(Word(s, holder, DealKind.VetoUse, "deal-story-9002", other));
                    Set(s, holder, other, 100);
                }),
                ("an offer of theirs already this week", s => s.deals.Add(new DealState
                {
                    id = NpcDeals.HolderOfferPrefix + "0", type = DealKind.VetoUse, proposerId = holder, recipientId = s.playerId,
                    status = DealStatus.Declined, week = s.week, expiresWeek = s.week, trustImpact = DealTrust.Critical,
                })),
                ("no room for the offer and its price", s => Fill(s, NpcDeals.DealCeiling - 1)),
            };
            foreach (var (name, change) in cases)
            {
                var s = Basis(meeting);
                change(s);
                Assert.That(Filed(s), Is.False, name + ": no offer.");
            }

            var warm = Basis(meeting);
            Set(warm, holder, warm.playerId, NpcDeals.DealingFloor);
            Assert.That(Filed(warm), Is.True, "At the dealing floor exactly.");
            var room = Basis(meeting);
            Fill(room, NpcDeals.DealCeiling - 2);
            Assert.That(Filed(room), Is.True, "Room for the offer and its price exactly.");
        }

        /// <summary>
        /// A plea cannot come before the offer: the holder is someone the player can plead with only at the meeting
        /// (<see cref="StrategyRules.Deciders"/>), and the offer is filed as it opens. So the offer's "no Save plea to them
        /// this week" holds by construction today, and its guard (a plea on the record, above) only keeps it so.
        /// </summary>
        [Test]
        public void APleaCannotComeBeforeTheOffer()
        {
            var s = BeforeTheMeeting(2, out string holder);
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.Veto));
            Assert.That(s.vetoHolderId, Is.EqualTo(holder), "The holder is known.");
            Assert.That(StrategyRules.CanLobby(s, holder, LobbyAsk.Save, s.playerId, out string refusal), Is.False);
            Assert.That(refusal, Is.EqualTo("Nobody is deciding anything you could change right now."));
            var meeting = Meeting(2, out _).Snapshot;
            Assert.That(meeting.lobbies.Any(l => l.week == meeting.week && l.deciderId == holder), Is.False, "No plea before the offer.");
        }

        // ------------------------------------------------------------------ the price

        /// <summary>
        /// The price is the player's, the first that fits: a voting bloc with the holder at this week's vote; where one already
        /// binds them, at the endgame a final two; past the endgame with a bloc already binding, no price fits and no offer is
        /// made.
        /// </summary>
        [Test]
        public void ThePriceIsABlocThisWeekThenAFinalTwo()
        {
            // Mode 1: a bloc already binding the holder is written here as a plain deal, which mode 2 keeps only as a canonical row.
            var meeting = Meeting(1, out string holder).Snapshot;
            var bloc = Negotiation.HolderOfferPrice(meeting, holder);
            Assert.That((bloc.kind, bloc.payerId, bloc.payeeId, bloc.aboutId, bloc.expiresWeek),
                Is.EqualTo((DealKind.VoteTogether, meeting.playerId, holder, (string)null, meeting.week)));
            Assert.That(Negotiation.HolderOfferPriceLine(meeting, OfferOf(meeting)),
                Is.EqualTo("In return, you would owe " + meeting.Find(holder).name + " a voting bloc this week."));

            var blocHeld = Basis(meeting);
            blocHeld.deals.Add(Word(blocHeld, holder, DealKind.VoteTogether, "deal-player-9003", by: blocHeld.playerId));
            Assert.That(blocHeld.Active.Count(), Is.GreaterThan(NpcDeals.EndgameSize));
            Assert.That(Negotiation.HolderOfferPrice(blocHeld, holder), Is.Null, "Past the endgame, with a bloc already binding them, nothing fits.");
            Assert.That(Filed(blocHeld), Is.False, "No price, no offer.");

            var endgame = blocHeld.Clone();
            foreach (var gone in endgame.Active.Where(c => !c.isPlayer && c.id != holder && c.id != endgame.hohId && !endgame.nominees.Contains(c.id))
                         .Take(endgame.Active.Count() - NpcDeals.EndgameSize).ToList())
                gone.status = ContestantStatus.Jury;
            Assert.That(endgame.Active.Count(), Is.EqualTo(NpcDeals.EndgameSize));
            var finalTwo = Negotiation.HolderOfferPrice(endgame, holder);
            Assert.That((finalTwo?.kind, finalTwo?.payerId, finalTwo?.payeeId, finalTwo?.expiresWeek),
                Is.EqualTo((DealKind.FinalTwo, endgame.playerId, holder, 0)), "At the endgame, with the bloc held, a final two.");
        }

        // ------------------------------------------------------------------ answering

        /// <summary>
        /// A yes binds the holder's word and strikes the player's price, linked both ways, in every commitment mode: mode 2's
        /// bloc as a canonical row of the holder's price's origin, the others as a plain deal. +4 with the holder, as any yes
        /// under the commitment rules, and the state validates.
        /// </summary>
        [Test]
        public void TakingItStrikesTheBlocThePlayerPays([Values(0, 1, 2)] int mode)
        {
            var engine = Meeting(mode, out string holder);
            var meeting = engine.Snapshot;
            var offer = OfferOf(meeting);
            var taken = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(meeting, offer.id, true));
            var word = taken.deals.Single(d => d.id == offer.id);
            Assert.That(word.status, Is.EqualTo(DealStatus.Active));
            Assert.That(RoleVetoBoardTests.Events(taken, taken.playerId, holder, "deal_accepted").Select(e => e.impactScore),
                Is.EqualTo(new[] { PlayerDeals.CommittedAcceptedImpact }));
            var price = Negotiation.PriceOf(taken, word);
            Assert.That(price, Is.Not.Null, "The price is struck.");
            Assert.That((price.type, price.proposerId, price.recipientId, price.targetId, price.status, price.expiresWeek, price.linkedDealId, word.linkedDealId),
                Is.EqualTo((DealKind.VoteTogether, taken.playerId, holder, (string)null, DealStatus.Active, taken.week, word.id, price.id)));
            if (mode == 2)
            {
                var row = taken.unifiedCommitments.Single(r => r.id == price.id);
                Assert.That((row.origin, row.makerId, row.beneficiaryId, row.subtype, row.linkedCommitmentId),
                    Is.EqualTo((UnifiedVoteFamilyValidation.HolderOfferPrice, taken.playerId, holder, DealKind.VoteTogether, word.id)));
            }
            else Assert.That(taken.deals.Any(d => d.id == price.id), Is.True, "A plain deal outside mode 2.");
            Assert.That(taken.events.Count(e => e.text == "For their word on the veto, you owe " + taken.Find(holder).name + " a voting bloc this week."),
                Is.EqualTo(1), "The player is told what they owe.");
            Assert.That(EpisodeValidation.TryValidate(taken, out string error), Is.True, error);
        }

        /// <summary>
        /// The offer and a Save plea to the holder substitute for each other (§3.4b): while the offer waits, the plea is
        /// refused until it is answered; taken, the offer was the plea; turned down, the seat is open again.
        /// </summary>
        [Test]
        public void TheOfferAndAPleaSubstitute()
        {
            var engine = Meeting(2, out string holder);
            var meeting = engine.Snapshot;
            var offer = OfferOf(meeting);
            Assert.That(StrategyRules.CanLobby(meeting, holder, LobbyAsk.Save, meeting.playerId, out string waiting), Is.False);
            Assert.That(waiting, Is.EqualTo(StrategyRules.AnswerTheOfferRefusal));

            var taken = new EpisodeEngine(meeting.Clone());
            var after = RoleVetoBoardTests.Apply(taken, RoleVetoBoardTests.Answer(meeting, offer.id, true));
            Assert.That(StrategyRules.CanLobby(after, holder, LobbyAsk.Save, after.playerId, out string word), Is.False);
            Assert.That(word, Is.EqualTo(StrategyRules.HolderWordRefusal));

            var declined = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(meeting, offer.id, false));
            Assert.That(StrategyRules.CanLobby(declined, holder, LobbyAsk.Save, declined.playerId, out string open), Is.True, open);
        }

        /// <summary>
        /// The regression sibling of the AP-0 Cite pin (60d8f629): at no conversations left, taking the offer still moves the
        /// holder's decision, but only with a binding price the player owes on the record; and the offer and a plea never
        /// stack, because once the offer is taken the plea is refused.
        /// </summary>
        [Test]
        public void AtNoConversationsLeftTheOfferMovesTheDecisionOnlyAtItsPrice()
        {
            var engine = Meeting(2, out string holder);
            var meeting = engine.Snapshot;
            Spend(meeting);
            Assert.That(EpisodeEngine.NpcVetoSave(meeting), Is.Not.EqualTo(meeting.playerId), "Without the word, not saved.");
            var offer = OfferOf(meeting);
            var declined = RoleVetoBoardTests.Apply(new EpisodeEngine(meeting.Clone()), RoleVetoBoardTests.Answer(meeting, offer.id, false));
            Assert.That(EpisodeEngine.NpcVetoSave(declined), Is.Not.EqualTo(declined.playerId), "The control: turned down, nothing moves.");
            var spent = new EpisodeEngine(meeting);
            var taken = RoleVetoBoardTests.Apply(spent, RoleVetoBoardTests.Answer(meeting, offer.id, true));
            var price = Negotiation.PriceOf(taken, taken.deals.Single(d => d.id == offer.id));
            Assert.That(price != null && DealStatus.Binds(price.status) && price.proposerId == taken.playerId, Is.True, "A binding price the player owes.");
            Assert.That(EpisodeEngine.NpcVetoSave(taken), Is.EqualTo(taken.playerId), "The word moves the decision.");
            Assert.That(StrategyRules.CanLobby(taken, holder, LobbyAsk.Save, taken.playerId, out _), Is.False, "No plea on top of it.");
        }

        // ------------------------------------------------------------------ the decision

        /// <summary>Taken, the holder's decision keeps it: the player is saved, the word Fulfilled, and the bloc stands, owed at this week's vote.</summary>
        [Test]
        public void KeptTheHolderSavesThePlayerAndTheBlocIsOwed()
        {
            var engine = Meeting(2, out string holder);
            var meeting = engine.Snapshot;
            var offer = OfferOf(meeting);
            Assert.That(EpisodeEngine.NpcVetoSave(meeting), Is.Not.EqualTo(meeting.playerId), "Without the word, not saved.");
            var taken = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(meeting, offer.id, true));
            var decided = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Command(taken, EpisodeCommandKind.Advance));
            Assert.That(decided.nominees, Does.Not.Contain(decided.playerId), "Saved, by the word.");
            var word = decided.deals.Single(d => d.id == offer.id);
            Assert.That(word.status, Is.EqualTo(DealStatus.Fulfilled));
            Assert.That(DealStatus.Binds(Negotiation.PriceOf(decided, word).status), Is.True, "The bloc is owed at this week's vote.");
            Assert.That(EpisodeValidation.TryValidate(decided, out string error), Is.True, error);
        }

        /// <summary>
        /// Taken and not kept - a grudge of sixty set in after the yes - the holder broke their word: Broken by the holder, and
        /// the player's price is void, never owed for a save that never came.
        /// </summary>
        [Test]
        public void BrokenByTheHolderThePlayersPriceIsVoid([Values(0, 1, 2)] int mode, [Values(false, true)] bool allies)
        {
            var engine = Meeting(mode, out string holder);
            var meeting = engine.Snapshot;
            var offer = OfferOf(meeting);
            var taken = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(meeting, offer.id, true)).Clone();
            taken.story.grudges.Add(new GrudgeState { holderId = holder, targetId = taken.playerId, cause = GrudgeCauses.Story, severity = 60, originWeek = 1, count = 1 });
            if (allies) taken.alliances.Add(new AllianceState
            {
                id = "alliance-r8", name = "The Veto Pact", active = true, members = new[] { taken.playerId, holder }.ToList(),
            });
            Assert.That(EpisodeValidation.TryValidate(taken, out string shaped), Is.True, shaped);
            var decided = RoleVetoBoardTests.Apply(new EpisodeEngine(taken), RoleVetoBoardTests.Command(taken, EpisodeCommandKind.Advance));
            Assert.That(decided.nominees, Does.Contain(decided.playerId), "Left on the block.");
            var word = decided.deals.Single(d => d.id == offer.id);
            Assert.That((word.status, word.brokenById), Is.EqualTo((DealStatus.Broken, holder)), "The holder broke it.");
            var price = Negotiation.PriceOf(decided, word);
            Assert.That(price.status, Is.EqualTo(DealStatus.Expired), "The player's price is void.");
            Assert.That(Negotiation.Voided(decided, price), Is.True);
            string name = decided.Find(holder).name;
            Assert.That(decided.events.Count(e => e.text == "You no longer owe " + name + " a voting bloc this week: " + name
                + " did not keep the veto commitment it paid for."), Is.EqualTo(1), "The player is told, in the words for a price they owe.");
            Assert.That(RoleVetoBoardTests.Events(decided, decided.playerId, holder, Allegiance.BetrayedType).Count, Is.EqualTo(allies ? 1 : 0),
                allies ? "An ally who broke it betrayed the pact." : "No pact, no betrayal.");
            Assert.That(EpisodeValidation.TryValidate(decided, out string error), Is.True, error);
        }

        /// <summary>A no costs the holder's tone's no (§3.0), read before the answer moves anything; the holder's column carries no rider.</summary>
        [Test]
        public void ANoCostsTheTonesNo()
        {
            var engine = Meeting(2, out string holder);
            var meeting = engine.Snapshot;
            var offer = OfferOf(meeting);
            string tone = RoleOffers.ToneOf(meeting, offer);
            var no = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(meeting, offer.id, false));
            Assert.That(RoleVetoBoardTests.Events(no, no.playerId, holder, "deal_declined").Select(e => e.impactScore),
                Is.EqualTo(new[] { RoleTone.Stakes(tone).decline }), tone);
        }

        /// <summary>
        /// Left unanswered, the holder's decision lapses it at the tone's silence (§3.0): a change in how they feel and nothing
        /// else - Expired, never Broken - and the player is told. Silence costs more than a no.
        /// </summary>
        [Test]
        public void SilenceCostsTheTonesLapse()
        {
            var engine = Meeting(2, out string holder);
            var meeting = engine.Snapshot;
            var offer = OfferOf(meeting);
            string tone = RoleOffers.ToneOf(meeting, offer);
            var decided = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Command(meeting, EpisodeCommandKind.Advance));
            Assert.That(decided.deals.Single(d => d.id == offer.id).status, Is.EqualTo(DealStatus.Expired));
            Assert.That(RoleVetoBoardTests.Events(decided, decided.playerId, holder, EpisodeEngine.LapsedOfferEvent).Select(e => e.impactScore),
                Is.EqualTo(new[] { RoleTone.Stakes(tone).lapse }), tone);
            Assert.That(RoleTone.Stakes(tone).lapse, Is.LessThan(RoleTone.Stakes(tone).decline + 0.0001), "Silence costs at least a no.");
            Assert.That(decided.events.Count(e => e.text == "You left a veto commitment from " + decided.Find(holder).name + " unanswered."), Is.EqualTo(1));
        }

        // ------------------------------------------------------------------ validation

        /// <summary>
        /// A season without the holder's offer holds none of its records: the offer by its id, and mode 2's price by its origin,
        /// which also needs the offer it was paid for. An offer of any other shape - another kind, a named target, a stake but
        /// Critical, two in a week - is refused in a season with it.
        /// </summary>
        [Test]
        public void ValidationRefusesItsRecordsWithoutItAndAnyOtherShape()
        {
            var engine = Meeting(2, out string holder);
            var meeting = engine.Snapshot;
            var offer = OfferOf(meeting);
            var taken = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(meeting, offer.id, true));
            var priceRow = taken.unifiedCommitments.Single(r => r.origin == UnifiedVoteFamilyValidation.HolderOfferPrice);

            Assert.That(Refusal(meeting, s => s.story.rulesVersion = StoryRules.Roles), Is.EqualTo("A season without the holder's offer has none of its records."));
            Assert.That(Refusal(taken, s => s.story.rulesVersion = StoryRules.Roles), Is.EqualTo("A season without the holder's offer has none of its records."),
                "The offer's own check comes first.");
            Assert.That(priceRow.origin, Is.EqualTo(UnifiedVoteFamilyValidation.HolderOfferPrice));
            Assert.That(Refusal(taken, s => s.deals.RemoveAll(d => d.id == offer.id)),
                Is.EqualTo("Consideration needs two reciprocal true owners, one price and the actual pair."), "A price with no offer behind it.");

            foreach (var (name, change) in new (string, Action<EpisodeState>)[]
            {
                ("another kind", s => s.deals.Single(d => d.id == offer.id).type = DealKind.VetoHold),
                ("a named target", s => s.deals.Single(d => d.id == offer.id).targetId = holder),
                ("a stake but Critical", s => s.deals.Single(d => d.id == offer.id).trustImpact = DealTrust.High),
                ("past its week", s => s.deals.Single(d => d.id == offer.id).expiresWeek = s.week + 1),
                ("put by the player", s => { var d = s.deals.Single(x => x.id == offer.id); d.proposerId = s.playerId; d.recipientId = holder; }),
                ("two in a week", s => { var copy = s.deals.Single(d => d.id == offer.id).Clone(); copy.id = NpcDeals.HolderOfferPrefix + "0"; copy.status = DealStatus.Declined; s.deals.Add(copy); }),
            })
                Assert.That(Refusal(meeting, change), Is.EqualTo(EpisodeValidation.HolderOfferShapeRefusal), name);
        }

        /// <summary>What validation says of the state with <paramref name="change"/> made to a copy, or null where it validates.</summary>
        private static string Refusal(EpisodeState state, Action<EpisodeState> change)
        {
            var s = state.Clone();
            change(s);
            return EpisodeValidation.TryValidate(s, out string error) ? null : error;
        }

        // ------------------------------------------------------------------ taken back

        /// <summary>
        /// Something moves between the offer and the answer - here a grudge of sixty - and the holder could no longer keep their
        /// word: the offer is taken back. A yes is refused and moves nothing; a no costs what any offer's does, not the tone's;
        /// left, it lapses at the decision as any offer does, costing nothing.
        /// </summary>
        [Test]
        public void AnOfferThatNoLongerStandsIsTakenBack()
        {
            var meeting = Meeting(2, out string holder).Snapshot;
            var offer = OfferOf(meeting);
            Assert.That(EpisodeEngine.HolderOfferStands(meeting, offer), Is.True);
            var moved = meeting.Clone();
            moved.story.grudges.Add(new GrudgeState { holderId = holder, targetId = moved.playerId, cause = GrudgeCauses.Story, severity = 60, originWeek = 1, count = 1 });
            Assert.That(EpisodeValidation.TryValidate(moved, out string shaped), Is.True, shaped);
            Assert.That(EpisodeEngine.HolderOfferStands(moved, OfferOf(moved)), Is.False);

            var yes = new EpisodeEngine(moved.Clone()).Apply(RoleVetoBoardTests.Answer(moved, offer.id, true));
            Assert.That((yes.accepted, yes.reason), Is.EqualTo((false, Negotiation.HolderWordGoneRefusal)));

            var no = RoleVetoBoardTests.Apply(new EpisodeEngine(moved.Clone()), RoleVetoBoardTests.Answer(moved, offer.id, false));
            Assert.That(RoleVetoBoardTests.Events(no, no.playerId, holder, "deal_declined").Select(e => e.impactScore),
                Is.EqualTo(new[] { PlayerDeals.DeclineImpact }), "Any offer's no.");

            var left = RoleVetoBoardTests.Apply(new EpisodeEngine(moved.Clone()), RoleVetoBoardTests.Command(moved, EpisodeCommandKind.Advance));
            Assert.That(left.deals.Single(d => d.id == offer.id).status, Is.EqualTo(DealStatus.Expired));
            Assert.That(RoleVetoBoardTests.Events(left, left.playerId, holder, EpisodeEngine.LapsedOfferEvent), Is.Empty, "Silence costs nothing.");
        }

        /// <summary>A yes after the price stopped fitting - a bloc already binding the two of them, past the endgame - is refused and moves nothing.</summary>
        [Test]
        public void AYesAfterThePriceStoppedFittingIsRefused()
        {
            var meeting = Meeting(1, out string holder).Snapshot;
            var offer = OfferOf(meeting);
            var moved = meeting.Clone();
            moved.deals.Add(Word(moved, holder, DealKind.VoteTogether, "deal-player-9004", by: moved.playerId));
            Assert.That(Negotiation.HolderOfferPrice(moved, holder), Is.Null);
            var yes = new EpisodeEngine(moved).Apply(RoleVetoBoardTests.Answer(moved, offer.id, true));
            Assert.That((yes.accepted, yes.reason), Is.EqualTo((false, Negotiation.HolderPriceGoneRefusal)));
        }

        /// <summary>While the offer waits, the player's own veto commitment to the holder waits on it too, as a plea does.</summary>
        [Test]
        public void ThePlayersOwnVetoDealWaitsOnTheOffer()
        {
            var meeting = Meeting(2, out string holder).Snapshot;
            Assert.That(PlayerDeals.CanPropose(meeting, holder, DealKind.VetoUse, null, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo(StrategyRules.AnswerTheOfferRefusal));
            var declined = RoleVetoBoardTests.Apply(new EpisodeEngine(meeting.Clone()), RoleVetoBoardTests.Answer(meeting, OfferOf(meeting).id, false));
            Assert.That(PlayerDeals.CanPropose(declined, holder, DealKind.VetoUse, null, out string open), Is.True, open);
        }

        // ------------------------------------------------------------------ fixtures

        private static readonly Dictionary<int, EpisodeState> Walks = new Dictionary<int, EpisodeState>();

        /// <summary>
        /// The meeting, not yet reached: an eight-house season at the holder's offer in commitment mode <paramref name="mode"/>,
        /// walked through the public engine to a veto competition with the player on the block and not Head of Household; then
        /// the veto handed to a houseguest who votes, who reads the player warmly enough to bargain but would not save them
        /// without their word - so every test of a kept word is a test of the word.
        /// </summary>
        internal static EpisodeState BeforeTheMeeting(int mode, out string holder)
        {
            if (!Walks.TryGetValue(mode, out var walked))
            {
                for (uint seed = 7601; seed < 7801 && walked == null; seed++)
                {
                    var start = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                    ShippedRules.ApplyFresh(start);
                    start.story.rulesVersion = StoryRules.HolderOffer;
                    start.unifiedCommitmentRulesVersion = mode == 2 ? UnifiedVoteFamilyValidation.Version : mode;
                    start.unifiedHearingRulesVersion = mode == 0 ? 0 : UnifiedCommitmentHearings.ProspectiveVersion;
                    var walk = new EpisodeEngine(start);
                    for (int i = 0; i < 200 && walk.Snapshot.phase != EpisodePhase.Veto; i++)
                    {
                        var step = walk.Apply(EpisodeEngineTests.NextCommand(walk.Snapshot));
                        Assert.That(step.accepted, Is.True, "seed " + seed + ": " + step.reason);
                    }
                    var s = walk.Snapshot;
                    if (s.phase != EpisodePhase.Veto || s.competitionResolved || s.Find(s.playerId).status != ContestantStatus.Active
                        || s.hohId == s.playerId || !s.nominees.Contains(s.playerId)) continue;
                    string h = s.Active.Where(c => !c.isPlayer && c.id != s.hohId && !s.nominees.Contains(c.id)).Select(c => c.id)
                        .OrderBy(id => id, StringComparer.Ordinal).FirstOrDefault();
                    if (h == null) continue;
                    s.vetoHolderId = h;
                    if (!s.vetoPlayers.Contains(h)) s.vetoPlayers[s.vetoPlayers.Count - 1] = h;
                    s.competitionResolved = true;
                    s.competitionScores = s.vetoPlayers.Select((id, i) => new CompetitionScore { contestantId = id, score = id == h ? 100 : i }).ToList();
                    // Warm enough to bargain, short of the line: without the word the holder would not save them.
                    Set(s, h, s.playerId, 15);
                    if (NpcDeals.Adjusted(s, h, s.playerId) < NpcDeals.DealingFloor || EpisodeEngine.NpcVetoSave(s) == s.playerId
                        || Hooks.Has(s, s.playerId, h) || StoryConsumers.WillNotSave(s, h, s.playerId)
                        || !EpisodeEngine.SavesWithTheWord(s, h, s.playerId) || Negotiation.Binding(s, h, s.playerId, DealKind.VetoUse, null)
                        || !EpisodeValidation.TryValidate(s, out _)) continue;
                    walked = s;
                }
                Assert.That(walked, Is.Not.Null, "No seed reached a veto competition with the player on the block.");
                Walks[mode] = walked;
            }
            var copy = walked.Clone();
            holder = copy.vetoHolderId;
            return copy;
        }

        /// <summary><see cref="BeforeTheMeeting"/>, advanced into the meeting by the engine: the holder has offered.</summary>
        internal static EpisodeEngine Meeting(int mode, out string holder)
        {
            var s = BeforeTheMeeting(mode, out holder);
            var engine = new EpisodeEngine(s);
            var meeting = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Command(s, EpisodeCommandKind.Advance));
            Assert.That(meeting.phase, Is.EqualTo(EpisodePhase.VetoMeeting));
            Assert.That(meeting.deals.Count(d => RoleOffers.IsHolderOffer(meeting, d)), Is.EqualTo(1), "The holder offered.");
            return engine;
        }

        /// <summary>This week's holder's offer.</summary>
        internal static DealState OfferOf(EpisodeState s) => s.deals.Single(d => RoleOffers.IsHolderOffer(s, d));

        /// <summary>The meeting with the holder's offer taken back out: the state <see cref="NpcDeals.OfferTheVeto"/> meets.</summary>
        private static EpisodeState Basis(EpisodeState meeting)
        {
            var s = meeting.Clone();
            s.deals.RemoveAll(d => NpcDeals.IsHolderOfferId(d.id));
            return s;
        }

        /// <summary>Whether the holder offers from this state: one more holder's offer filed.</summary>
        private static bool Filed(EpisodeState s)
        {
            int before = s.deals.Count(d => NpcDeals.IsHolderOfferId(d.id));
            NpcDeals.OfferTheVeto(s);
            return s.deals.Count(d => NpcDeals.IsHolderOfferId(d.id)) > before;
        }

        /// <summary>A deal of this week, standing, between the holder and the player - or another nominee - made by <paramref name="by"/>, the holder by default.</summary>
        private static DealState Word(EpisodeState s, string holder, string kind, string id, string with = null, string by = null)
        {
            string other = with ?? s.playerId, maker = by ?? holder;
            return new DealState
            {
                id = id, type = kind, proposerId = maker, recipientId = maker == holder ? other : holder,
                status = DealStatus.Active, week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(kind),
            };
        }

        /// <summary>The player's conversations for the week, all spent.</summary>
        private static void Spend(EpisodeState s)
        {
            var spend = typeof(EpisodeEngine).GetMethod("SpendSocialAction", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("EpisodeEngine.SpendSocialAction spends one.");
            var spent = typeof(EpisodeEngine).GetMethod("SocialActionsSpent", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
            var budget = typeof(EpisodeEngine).GetMethod("SocialActionBudget", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
            for (int i = 0; i < 50 && (int)spent.Invoke(null, new object[] { s }) < (int)budget.Invoke(null, new object[] { s }); i++)
                spend.Invoke(null, new object[] { s });
        }

        /// <summary>Pads the season's deals to <paramref name="count"/>, as the ceiling counts them, with settled deals between two houseguests.</summary>
        private static void Fill(EpisodeState s, int count)
        {
            var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            // The ceiling's count in mode 2, where these tests fill it: raw deals and the canonical deal-policy rows.
            for (int i = 0; s.deals.Count + s.unifiedCommitments.Count(r => r.sourcePolicy == UnifiedCommitments.DealPolicy) < count; i++)
                s.deals.Add(new DealState
                {
                    id = "deal-fill-" + i, type = DealKind.InformationSharing, proposerId = npcs[i % npcs.Count], recipientId = npcs[(i + 1) % npcs.Count],
                    status = DealStatus.Expired, week = 1, trustImpact = DealTrust.Low,
                });
        }

        private static void Set(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }
    }
}
