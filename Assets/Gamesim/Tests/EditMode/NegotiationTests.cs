using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Negotiation (ACTIONS-DEALS-ALLIANCES-PLAN C7), under the commitment rules: a refused proposal may
    /// come back as a counter - the same deal with a price on it - on a coin keyed to the attempt, so the
    /// season's stream is exactly what a plain refusal leaves; its terms for each kind; one round, answered
    /// at once or lapsed; a nominee's veto ask that carries a price, struck with the yes and linked to it,
    /// owed when the ask is honoured and void when it is not; and the web's three situation moves, each
    /// with its odds and its costs - calling in a promise, mending fences after a breach, and a veto for a
    /// price. Each is shown without the rules too, where nothing moves. Unity-free, so the dotnet subset
    /// runs it (Tools/SimulationTests). The screen's half is EpisodePlayModeTests.Negotiation, the save's
    /// half PersistenceV22MigrationTests.
    /// </summary>
    public sealed class NegotiationTests
    {
        /// <summary>
        /// Negotiate is appended after C5's two kinds, as they landed, so no recorded ordinal moves: a save
        /// and a recorded season store the number.
        /// </summary>
        [Test]
        public void NegotiateIsAppendedAfterTheAlliancesKinds()
        {
            Assert.That((int)EpisodeCommandKind.RenameAlliance, Is.EqualTo(60), "The last kind before C7.");
            Assert.That((int)EpisodeCommandKind.Negotiate, Is.EqualTo(61));
        }

        // ------------------------------------------------------------ the counter

        /// <summary>
        /// A partnership the roll turned down, from a houseguest the roll found close to yes, whose coin for
        /// this attempt lands: the refusal is said as it always was, and then the counter - the same deal,
        /// and the price they want, a safety pact - to the two of them, as the last line said. No deal is
        /// written for it: nothing waits anywhere but in the line.
        /// </summary>
        [Test]
        public void UnderTheRulesARefusalTheRollFoundCloseToYesMayComeBackAsTheSameDealWithAPrice()
        {
            var s = NearMiss(Rules(Season(71)), DealKind.Partnership, null, coinLands: true, out var npc);
            var result = Apply(new EpisodeEngine(s), EpisodeCommandKind.ProposeDeal, npc.id, null, DealKind.Partnership);
            Assert.That(result.accepted, Is.True, result.reason);
            var after = result.state;
            Assert.That(after.deals.Count, Is.EqualTo(s.deals.Count), "Nothing is struck, and nothing waits as a deal.");
            var refusal = after.events[after.events.Count - 2];
            Assert.That(refusal.kind, Is.EqualTo("deal"));
            Assert.That(refusal.text, Does.StartWith(npc.name + " turned down a partnership."), "The refusal is said as it always was,");
            var line = after.events.Last();
            Assert.That(line.kind, Is.EqualTo(CounterKind), "and then the counter, in a line of its own,");
            Assert.That(line.sequence, Is.EqualTo(after.nextSequence - 1), "the last thing the house has said,");
            Assert.That(line.audienceIds, Is.EqualTo(new[] { s.playerId, npc.id }), "between the two of them.");
            Assert.That(line.text, Is.EqualTo(npc.name + " would agree to the partnership after all, if you add a safety pact."));

            var counter = Open(after, npc.id);
            Assert.That(counter, Is.Not.Null, "It stands.");
            Assert.That((counter.kind, counter.aboutId), Is.EqualTo((DealKind.Partnership, (string)null)), "The deal the player asked for,");
            Assert.That((counter.price.kind, counter.price.payerId, counter.price.payeeId), Is.EqualTo((DealKind.SafetyAgreement, s.playerId, npc.id)),
                "and the price: a safety pact, the player's to pay.");
        }

        /// <summary>
        /// The counter never draws from the season's stream. The same refusal three ways - under the rules
        /// with the attempt's coin landing (a counter), with it missing (none), and in a season without the
        /// rules (a plain refusal) - leaves the stream in exactly the same place; and the counter's answer,
        /// yes or no, draws nothing from it either.
        /// </summary>
        [Test]
        public void ARefusedProposalLeavesTheSeasonsStreamExactlyAsAPlainRefusalDoes()
        {
            var countered = Refuse(NearMiss(Rules(Season(72)), DealKind.Partnership, null, coinLands: true, out var npc), npc.id);
            var missed = Refuse(NearMiss(Rules(Season(72)), DealKind.Partnership, null, coinLands: false, out _), npc.id);
            var plain = Refuse(NearMiss(Levers(Season(72)), DealKind.Partnership, null, coinLands: true, out _), npc.id);
            Assert.That(Open(countered, npc.id), Is.Not.Null, "The coin landed: a counter stands.");
            Assert.That(Open(missed, npc.id), Is.Null, "The coin missed: none.");
            Assert.That(plain.events.Last().kind, Is.EqualTo("deal"), "Without the rules the refusal is the last word.");
            Assert.That(countered.randomState, Is.EqualTo(plain.randomState), "A counter leaves the stream as a plain refusal does,");
            Assert.That(missed.randomState, Is.EqualTo(plain.randomState), "and so does a coin that missed.");

            foreach (string answer in new[] { AcceptAnswer, "decline" })
            {
                var answered = Apply(new EpisodeEngine(countered), EpisodeCommandKind.RespondToDeal, npc.id, null, answer);
                Assert.That(answered.accepted, Is.True, answer + ": " + answered.reason);
                Assert.That(answered.state.randomState, Is.EqualTo(countered.randomState), answer + ": the answer draws nothing from the season's stream.");
            }
        }

        /// <summary>
        /// The coin is keyed to the attempt: the week, the player, the houseguest, the kind, who it is about
        /// and the sequence the season had reached. The same key always draws the same number, whatever the
        /// season's stream holds; the next attempt is a different key; two engines given the same refusal say
        /// the same thing; and over many attempts the coin lands about as often as the web's 70% says.
        /// </summary>
        [Test]
        public void TheCountersCoinIsKeyedToTheAttemptSoTheSameAttemptAlwaysDrawsTheSameAnswer()
        {
            var s = Rules(Season(73));
            var npc = Plain(Npcs(s)[0]);
            string key = Key(s, npc.id, DealKind.Partnership, null, s.nextSequence);
            double first = StoryRandom.Unit(s, key);
            s.randomState = 12345u;
            Assert.That(StoryRandom.Unit(s, key), Is.EqualTo(first), "The season's stream does not move the coin.");
            Assert.That(Key(s, npc.id, DealKind.Partnership, null, s.nextSequence + 1), Is.Not.EqualTo(key), "Every attempt has a coin of its own,");
            Assert.That(Key(s, npc.id, DealKind.SafetyAgreement, null, s.nextSequence), Is.Not.EqualTo(key), "and every kind,");
            Assert.That(Key(s, Npcs(s)[1].id, DealKind.Partnership, null, s.nextSequence), Is.Not.EqualTo(key), "and every houseguest.");

            var twice = NearMiss(Rules(Season(74)), DealKind.Partnership, null, coinLands: true, out var asked);
            var one = Refuse(twice, asked.id);
            var other = Refuse(twice, asked.id);
            Assert.That(other.events.Select(e => e.text), Is.EqualTo(one.events.Select(e => e.text)), "The same refusal says the same thing twice.");

            int lands = 0;
            for (int attempt = 1; attempt <= 1000; attempt++)
                if (StoryRandom.Unit(s, Key(s, npc.id, DealKind.Partnership, null, attempt)) < CounterChance) lands++;
            Assert.That(lands, Is.InRange(640, 760), "About seven in ten attempts land.");
        }

        /// <summary>
        /// The counter's terms for each kind of deal: the deal as the player asked for it, and the first
        /// price that fits - the player's vote to keep a houseguest on the block, a final two at the
        /// endgame, a safety pact, a voting bloc this week - never the kind asked for, never one already
        /// binding them. A safety pact in free time, with no vote to bloc on and no endgame, has none, so
        /// it comes back with no counter.
        /// </summary>
        [Test]
        public void TheCountersTermsForEachKindOfDeal()
        {
            var free = Rules(Season(75));
            var campaign = Campaign(76);
            var endgame = Rules(Season(77, 6));
            var veto = VetoMeeting(78, playerHolds: false);
            string Price(EpisodeState s, string npcId, string kind, string about)
            {
                Assert.That(PlayerDeals.CanPropose(s, npcId, kind, about, out var why), Is.True, kind + " can be put: " + why);
                var counter = Terms(s, npcId, kind, about);
                if (counter == null) return "none";
                Assert.That((counter.kind, counter.price.payerId, counter.price.payeeId), Is.EqualTo((kind, s.playerId, npcId)), kind);
                return counter.price.kind + (counter.price.aboutId != null ? ":" + counter.price.aboutId : "");
            }
            var f = Npcs(free); var c = Npcs(campaign); var e = Npcs(endgame); var v = Npcs(veto);
            string nominee = c[1].id, other = c[2].id;
            Assert.That(Price(free, f[0].id, DealKind.TargetAgreement, f[1].id), Is.EqualTo(DealKind.SafetyAgreement));
            Assert.That(Price(free, f[0].id, DealKind.SafetyAgreement, null), Is.EqualTo("none"), "No vote to bloc on, no endgame: nothing to ask for a safety pact.");
            Assert.That(Price(free, f[0].id, DealKind.InformationSharing, null), Is.EqualTo(DealKind.SafetyAgreement));
            Assert.That(Price(free, f[0].id, DealKind.AllianceInvite, null), Is.EqualTo(DealKind.SafetyAgreement));
            Assert.That(Price(campaign, nominee, DealKind.SafetyAgreement, null), Is.EqualTo(DealKind.VoteSave + ":" + nominee),
                "On the block, they want the player's vote to keep them.");
            Assert.That(Price(campaign, nominee, DealKind.VoteTogether, null), Is.EqualTo(DealKind.VoteSave + ":" + nominee));
            Assert.That(Price(campaign, nominee, DealKind.VoteEvict, other), Is.EqualTo(DealKind.VoteSave + ":" + nominee));
            Assert.That(Price(campaign, nominee, DealKind.VoteEvict, nominee), Is.EqualTo(DealKind.SafetyAgreement),
                "Never a price that undoes the deal: not their own keep for a vote to evict them.");
            Assert.That(Price(campaign, nominee, DealKind.VoteSave, other), Is.EqualTo(DealKind.SafetyAgreement), "Never the kind asked for.");
            Assert.That(Price(veto, veto.vetoHolderId, DealKind.VetoUse, null), Is.EqualTo(DealKind.SafetyAgreement));
            Assert.That(Price(endgame, e[0].id, DealKind.Partnership, null), Is.EqualTo(DealKind.FinalTwo), "At the endgame, a final two.");
            Assert.That(Price(endgame, e[0].id, DealKind.FinalTwo, null), Is.EqualTo(DealKind.SafetyAgreement));

            // Already binding them, a price is passed over for the next.
            campaign.deals.Add(new DealState
            {
                id = "deal-held-save", type = DealKind.VoteSave, proposerId = campaign.playerId, recipientId = nominee, targetId = nominee,
                status = DealStatus.Active, week = campaign.week, expiresWeek = campaign.week, trustImpact = DealKind.DefaultTrust(DealKind.VoteSave),
            });
            Assert.That(Price(campaign, nominee, DealKind.Partnership, null), Is.EqualTo(DealKind.SafetyAgreement), "A vote they already have is no price.");

            // The words, as the houseguest says them.
            var said = Terms(campaign, nominee, DealKind.VoteEvict, other);
            Assert.That(CounterLine(campaign, said), Is.EqualTo(campaign.Find(nominee).name + " would agree to the vote to evict " + campaign.Find(other).name
                + " after all, if you add a safety pact."));
            var kept = Terms(Campaign(76), nominee, DealKind.SafetyAgreement, null);
            Assert.That(CounterLine(Campaign(76), kept), Is.EqualTo(campaign.Find(nominee).name + " would agree to the safety pact after all, if you add your vote to keep "
                + FinalistRead.FirstName(campaign.Find(nominee).name) + " this week."));
        }

        /// <summary>
        /// A counter is answered at once or lapses: anything said after it - a word with somebody else - and
        /// it no longer stands, and an answer naming the houseguest finds nothing on the table.
        /// </summary>
        [Test]
        public void ACounterIsAnsweredAtOnceOrItLapses()
        {
            var countered = Refuse(NearMiss(Rules(Season(79)), DealKind.Partnership, null, coinLands: true, out var npc), npc.id);
            Assert.That(Open(countered, npc.id), Is.Not.Null);
            var engine = new EpisodeEngine(countered);
            Assert.That(Apply(engine, EpisodeCommandKind.SmallTalk, Npcs(countered)[2].id).accepted, Is.True);
            Assert.That(Open(engine.Snapshot, npc.id), Is.Null, "Something else was said: the counter has lapsed.");
            var late = Apply(engine, EpisodeCommandKind.RespondToDeal, npc.id, null, AcceptAnswer);
            Assert.That(late.accepted, Is.False, "There is nothing left to answer.");
            Assert.That(late.reason, Is.EqualTo("That offer is no longer on the table."));
            Assert.That(engine.Snapshot.deals.Count, Is.EqualTo(countered.deals.Count), "and nothing is struck.");
        }

        /// <summary>
        /// A counter stands through anything the player does not hear: a line between two other
        /// houseguests, and a ledger row's or a story's id minted with no line at all, leave it on the
        /// table, and it can still be taken. The next line the player hears lapses it - one said to them,
        /// or one said to the whole house - and nothing is left to answer. Nor does it outlast its phase.
        /// </summary>
        [Test]
        public void ACounterStandsThroughWhatThePlayerDoesNotHearAndLapsesWithWhatTheyDo()
        {
            var countered = Refuse(NearMiss(Rules(Season(101)), DealKind.Partnership, null, coinLands: true, out var npc), npc.id);
            var others = Npcs(countered).Where(c => c.id != npc.id).ToList();
            var unheard = countered.Clone();
            Say(unheard, "npc-conversation", others[0].id, others[1].id);
            unheard.nextSequence += 3;
            Assert.That(Open(unheard, npc.id), Is.Not.Null, "A line between two others, and ids minted with no line, leave it standing,");
            var taken = Apply(new EpisodeEngine(unheard), EpisodeCommandKind.RespondToDeal, npc.id, null, AcceptAnswer);
            Assert.That(taken.accepted, Is.True, taken.reason);
            Assert.That(taken.state.deals.Count(IsPrice), Is.EqualTo(1), "and it can still be taken.");

            foreach (var audience in new[] { new[] { unheard.playerId, others[0].id }, new string[0] })
            {
                string what = audience.Length == 0 ? "A line said to the whole house" : "A line said to the player";
                var heard = unheard.Clone();
                Say(heard, "conversation", audience);
                Assert.That(Open(heard, npc.id), Is.Null, what + " lapses it:");
                var late = Apply(new EpisodeEngine(heard), EpisodeCommandKind.RespondToDeal, npc.id, null, AcceptAnswer);
                Assert.That(late.accepted, Is.False, what + ": nothing is left to answer.");
                Assert.That(late.reason, Is.EqualTo("That offer is no longer on the table."), what);
            }

            var later = unheard.Clone();
            later.phase = EpisodePhase.Campaign;
            Assert.That(Open(later, npc.id), Is.Null, "Nor does it outlast the phase it was said in.");
        }

        /// <summary>
        /// The yes: both deals at once, with no roll - the partnership the player asked for and the safety
        /// pact that was its price, each naming the other, the price the player's to pay - taken as chances
        /// taken; free, as answering any offer is; +4, the yes to an offer, its reciprocal draw from the
        /// counter's own keyed stream. One round: nothing stands after it, and nothing more can be answered.
        /// </summary>
        [Test]
        public void TakingTheCounterStrikesBothDealsLinkedWithNoRollAndIsTheOneRound()
        {
            var countered = Refuse(NearMiss(Rules(Season(80)), DealKind.Partnership, null, coinLands: true, out var npc), npc.id);
            double view = countered.Score(countered.playerId, npc.id);
            var result = Apply(new EpisodeEngine(countered), EpisodeCommandKind.RespondToDeal, npc.id, null, AcceptAnswer);
            Assert.That(result.accepted, Is.True, result.reason);
            var after = result.state;
            var added = after.deals.Skip(countered.deals.Count).ToList();
            Assert.That(added, Has.Count.EqualTo(2), "Both at once.");
            var bought = added.Single(d => !IsPrice(d));
            var price = added.Single(IsPrice);
            Assert.That((bought.type, bought.proposerId, bought.recipientId, bought.status), Is.EqualTo((DealKind.Partnership, after.playerId, npc.id, DealStatus.Active)),
                "The deal the player asked for, as they asked for it,");
            Assert.That((price.type, price.proposerId, price.recipientId, price.status), Is.EqualTo((DealKind.SafetyAgreement, after.playerId, npc.id, DealStatus.Active)),
                "and its price, theirs to be owed and the player's to pay,");
            Assert.That((Link(bought), Link(price)), Is.EqualTo((price.id, bought.id)), "each naming the other.");
            Assert.That(price.id, Does.StartWith(PricePrefix));
            Assert.That(after.randomState, Is.EqualTo(countered.randomState), "No roll.");
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(countered)), "Free, as answering an offer is.");
            Assert.That(after.Score(after.playerId, npc.id), Is.GreaterThan(view), "The yes is warmth both ways, from the counter's own stream.");
            Assert.That(after.ledger.opportunities.Single(o => o.id == bought.id).response, Is.EqualTo(OpportunityResponse.Taken));
            Assert.That(after.ledger.opportunities.Single(o => o.id == price.id).response, Is.EqualTo(OpportunityResponse.Taken));
            Assert.That(after.events.Last(e => e.kind == "deal").text,
                Is.EqualTo("You took " + npc.name + "'s counter: the partnership, and a safety pact."));
            Assert.That(Open(after, npc.id), Is.Null, "Nothing stands after the yes:");
            var again = Apply(new EpisodeEngine(after), EpisodeCommandKind.RespondToDeal, npc.id, null, AcceptAnswer);
            Assert.That(again.accepted, Is.False, "one round.");
            Assert.That(EpisodeValidation.TryValidate(after, out var error), Is.True, error);
        }

        /// <summary>
        /// Taking a counter draws nothing from the season's stream whatever it strikes: a vote deal says
        /// what its lever moved, as any vote deal struck does, and an invitation makes the pact, as any
        /// accepted invitation does - each reading and writing without the stream.
        /// </summary>
        [Test]
        public void TakingACounterForAVoteOrAnInvitationDrawsNothingFromTheSeasonsStream()
        {
            var campaign = Campaign(109);
            string nominee = Npcs(campaign)[1].id;
            Set(campaign, Npcs(campaign)[3].id, nominee, 0);
            var vc = Refuse(NearMiss(campaign, DealKind.VoteEvict, nominee, coinLands: true, out var voter, 3), voter.id, DealKind.VoteEvict, nominee);
            Assert.That(Open(vc, voter.id), Is.Not.Null, "A vote to evict comes back as a counter.");
            var vt = Take(vc, voter.id);
            Assert.That(vt.randomState, Is.EqualTo(vc.randomState), "Taken, it draws nothing from the season's stream,");
            Assert.That(vt.events.Count(e => e.sequence >= vc.nextSequence && e.kind == "lever"), Is.EqualTo(1),
                "and says what the vote deal's lever moved.");

            var free = Rules(Season(110));
            var ic = Refuse(NearMiss(free, DealKind.AllianceInvite, null, coinLands: true, out var friend), friend.id, DealKind.AllianceInvite);
            Assert.That(Open(ic, friend.id), Is.Not.Null, "An invitation comes back as a counter.");
            Assert.That(ic.Allied(ic.playerId, friend.id), Is.False);
            var it = Take(ic, friend.id);
            Assert.That(it.randomState, Is.EqualTo(ic.randomState), "Taken, it draws nothing from the season's stream,");
            Assert.That(it.Allied(it.playerId, friend.id), Is.True, "and the pact is made, as any accepted invitation makes it.");
        }

        /// <summary>
        /// A counter's two deals are the player's yes to the houseguest's offer, so either one broken weighs
        /// one step heavier, as any offer the player accepted does (decision 15). A proposal of the player's
        /// own the houseguest agreed to, and the veto the player named a price for, weigh their own.
        /// </summary>
        [Test]
        public void ACountersDealsWeighOneStepHeavierBrokenAsAnyOfferThePlayerAcceptedDoes()
        {
            var countered = Refuse(NearMiss(Rules(Season(106)), DealKind.Partnership, null, coinLands: true, out var npc), npc.id);
            var taken = Take(countered, npc.id);
            var bought = taken.deals.Single(d => d.id.StartsWith(CounterDealPrefix, System.StringComparison.Ordinal));
            var price = taken.deals.Single(IsPrice);
            foreach (var deal in new[] { bought, price })
            {
                Assert.That(DealResolution.AcceptedOffer(taken, deal), Is.True, deal.id + ": the yes to their offer,");
                Assert.That(DealResolution.BreachWeight(taken, deal), Is.EqualTo(DealTrust.Weight(DealTrust.Heavier(deal.trustImpact))),
                    deal.id + ": weighs one step heavier broken,");
                Assert.That(DealResolution.BreachWeight(taken, deal), Is.GreaterThan(DealTrust.Weight(deal.trustImpact)), deal.id);
            }

            var agreed = Rules(Season(106));
            var asked = Plain(Npcs(agreed)[0]);
            Set(agreed, asked.id, agreed.playerId, 30); Set(agreed, agreed.playerId, asked.id, 30);
            agreed.randomState = Draw(agreed, true, PlayerDeals.AcceptanceChance(agreed, asked.id, DealKind.Partnership, null));
            var struck = Apply(new EpisodeEngine(agreed), EpisodeCommandKind.ProposeDeal, asked.id, null, DealKind.Partnership).state;
            var own = struck.deals.Single(d => d.type == DealKind.Partnership && d.proposerId == struck.playerId);
            Assert.That(DealResolution.AcceptedOffer(struck, own), Is.False, "A proposal of the player's own is no yes to an offer,");
            Assert.That(DealResolution.BreachWeight(struck, own), Is.EqualTo(DealTrust.Weight(own.trustImpact)), "and weighs its own.");

            var meeting = VetoMeeting(106, playerHolds: true);
            var nominee = Plain(meeting.Find(meeting.nominees[0]));
            meeting.randomState = Draw(meeting, true, MoveChance(meeting, nominee.id, VetoMove, false));
            var named = Apply(new EpisodeEngine(meeting), NegotiateKind, nominee.id, null, VetoPriceOf(DealKind.VoteSave)).state;
            var pair = named.deals.Where(d => Link(d) != null).ToList();
            Assert.That(pair, Has.Count.EqualTo(2), "The veto for a price: the player's word and the price.");
            foreach (var deal in pair)
                Assert.That(DealResolution.BreachWeight(named, deal), Is.EqualTo(DealTrust.Weight(deal.trustImpact)),
                    deal.id + ": the price the player named is no yes to an offer of theirs, and weighs its own.");
        }

        /// <summary>The no is a plain no: the line, and nothing struck, moved or drawn.</summary>
        [Test]
        public void TurningTheCounterDownIsAPlainNo()
        {
            var countered = Refuse(NearMiss(Rules(Season(81)), DealKind.Partnership, null, coinLands: true, out var npc), npc.id);
            var result = Apply(new EpisodeEngine(countered), EpisodeCommandKind.RespondToDeal, npc.id, null, "decline");
            Assert.That(result.accepted, Is.True, result.reason);
            var after = result.state;
            Assert.That(after.deals.Count, Is.EqualTo(countered.deals.Count), "Nothing struck,");
            Assert.That((after.Score(after.playerId, npc.id), after.Score(npc.id, after.playerId)),
                Is.EqualTo((countered.Score(countered.playerId, npc.id), countered.Score(npc.id, countered.playerId))), "nothing moved,");
            Assert.That(after.randomState, Is.EqualTo(countered.randomState), "nothing drawn;");
            Assert.That(after.events.Last().text, Is.EqualTo("You turned down " + npc.name + "'s counter."), "only the line.");
            Assert.That(Open(after, npc.id), Is.Null);
        }

        /// <summary>
        /// No counter without the rules, where a refusal is the last word as it always was; none from a
        /// houseguest the roll found far from yes, however the coin falls; and none for an alliance a grudge
        /// refuses, since no price buys it.
        /// </summary>
        [Test]
        public void NoCounterWithoutTheRulesFarFromYesOrAgainstAGrudge()
        {
            var off = NearMiss(Levers(Season(82)), DealKind.Partnership, null, coinLands: true, out var npc);
            var plain = Refuse(off, npc.id);
            Assert.That(plain.events.Count(e => e.kind == CounterKind), Is.Zero, "Without the rules there is no counter,");
            Assert.That(plain.nextSequence - off.nextSequence, Is.EqualTo(3), "and the refusal mints what it always did: two records and its line.");
            var answer = Apply(new EpisodeEngine(plain), EpisodeCommandKind.RespondToDeal, npc.id, null, AcceptAnswer);
            Assert.That(answer.accepted, Is.False, "Nor can one be answered.");

            var far = Rules(Season(83));
            var cold = Plain(Npcs(far)[0]);
            Set(far, cold.id, far.playerId, -40); Set(far, far.playerId, cold.id, -40);
            double chance = PlayerDeals.AcceptanceChance(far, cold.id, DealKind.Partnership, null);
            Assert.That(chance, Is.LessThan(CounterFloor), "Far from yes.");
            far.randomState = Draw(far, false, chance);
            Coin(far, cold.id, DealKind.Partnership, null, true);
            Assert.That(Refuse(far, cold.id).events.Last().kind, Is.EqualTo("deal"), "However the coin falls, no counter.");

            var grudge = Rules(Season(84));
            EpisodeEngine.EnableStory(grudge);
            var sore = Plain(Npcs(grudge)[0]);
            Set(grudge, sore.id, grudge.playerId, 60); Set(grudge, grudge.playerId, sore.id, 60);
            Assert.That(Grudges.Add(grudge, sore.id, grudge.playerId, 60, GrudgeCauses.Story), Is.Not.Null);
            Assert.That(PlayerDeals.AcceptanceChance(grudge, sore.id, DealKind.AllianceInvite, null), Is.GreaterThanOrEqualTo(CounterFloor));
            Coin(grudge, sore.id, DealKind.AllianceInvite, null, true);
            var refused = Apply(new EpisodeEngine(grudge), EpisodeCommandKind.ProposeDeal, sore.id, null, DealKind.AllianceInvite);
            Assert.That(refused.accepted, Is.True, refused.reason);
            Assert.That(refused.state.deals.Count, Is.EqualTo(grudge.deals.Count), "The grudge refuses the invitation,");
            Assert.That(refused.state.events.Count(e => e.kind == CounterKind), Is.Zero, "and no price buys it.");
        }

        // ------------------------------------------------------------ a nominee's ask carries a price

        /// <summary>
        /// A nominee's veto ask carries a price under the rules: their vote to keep the player the next time
        /// the player is on the block, or at the endgame a final two. The yes strikes it - a real deal from
        /// them to the player, standing at once, open until a vote or the final choice judges it - the ask
        /// and the price naming each other. Without the rules the ask carries nothing, as it always did.
        /// </summary>
        [Test]
        public void ANomineesVetoAskCarriesAPriceStruckWithTheYes()
        {
            foreach (int size in new[] { 8, 6 })
            {
                var s = VetoMeeting(85, playerHolds: true, size: size);
                var ask = Ask(s, s.nominees[0]);
                string where = "A house of " + size;
                var price = AskPriceOf(s, ask);
                Assert.That(price, Is.Not.Null, where + ": the ask carries a price.");
                string expected = size == 6 ? DealKind.FinalTwo : DealKind.VoteSave;
                Assert.That((price.kind, price.payerId, price.payeeId), Is.EqualTo((expected, ask.proposerId, s.playerId)), where);

                var result = Apply(new EpisodeEngine(s), EpisodeCommandKind.RespondToDeal, ask.id, null, AcceptAnswer);
                Assert.That(result.accepted, Is.True, where + ": " + result.reason);
                var after = result.state;
                var taken = after.deals.Single(d => d.id == ask.id);
                var struck = after.deals.Single(IsPrice);
                Assert.That(taken.status, Is.EqualTo(DealStatus.Active), where + ": the ask is taken,");
                Assert.That((struck.type, struck.proposerId, struck.recipientId, struck.status, struck.expiresWeek),
                    Is.EqualTo((expected, ask.proposerId, after.playerId, DealStatus.Active, 0)), where + ": and its price stands, open until it is judged,");
                Assert.That(struck.targetId, Is.EqualTo(expected == DealKind.VoteSave ? after.playerId : null), where + ": a vote save names the player.");
                Assert.That((Link(taken), Link(struck)), Is.EqualTo((struck.id, taken.id)), where + ": each names the other.");
                Assert.That(after.events.Last().text, Is.EqualTo("For your word on the veto, " + after.Find(ask.proposerId).name + " owes you "
                    + (size == 6 ? "a final two" : "their vote to keep you, the next time you are on the block") + "."), where);
                Assert.That(EpisodeValidation.TryValidate(after, out var error), Is.True, where + ": " + error);
            }

            var off = VetoMeeting(86, playerHolds: true, rules: false);
            var plain = Ask(off, off.nominees[0]);
            Assert.That(AskPriceOf(off, plain), Is.Null, "Without the rules the ask carries nothing,");
            var answered = Apply(new EpisodeEngine(off), EpisodeCommandKind.RespondToDeal, plain.id, null, AcceptAnswer);
            Assert.That(answered.accepted, Is.True, answered.reason);
            Assert.That(answered.state.deals, Has.Count.EqualTo(off.deals.Count), "and the yes strikes nothing more.");
            Assert.That(answered.state.deals.All(d => Link(d) == null), Is.True);
        }

        /// <summary>
        /// The link's resolution. The ask honoured - the player uses the veto on them - and the price is owed:
        /// it stands, nobody's breach on it. The ask not honoured - the veto unused, or used on the other
        /// nominee - and the player broke it (C0's record: broken by the player, settled that week), so the
        /// price they were offered is void: it lapses, broken by nobody, settled never, and the two of them
        /// are told. The price is judged after that by its own rule: the vote that next tests it.
        /// </summary>
        [Test]
        public void ThePriceIsOwedWhenTheAskIsHonouredAndVoidWhenItIsNot()
        {
            foreach (string decision in new[] { "honoured", "unused", "the other" })
            {
                var s = VetoMeeting(87, playerHolds: true);
                var ask = Ask(s, s.nominees[0]);
                var engine = new EpisodeEngine(s);
                Assert.That(Apply(engine, EpisodeCommandKind.RespondToDeal, ask.id, null, AcceptAnswer).accepted, Is.True);
                var before = engine.Snapshot;
                var veto = Command(before, EpisodeCommandKind.ResolveVeto, decision == "unused" ? null : decision == "honoured" ? before.nominees[0] : before.nominees[1],
                    EpisodeEngine.ReplacementCandidates(before).First().id);
                veto.useVeto = decision != "unused";
                var result = engine.Apply(veto);
                Assert.That(result.accepted, Is.True, decision + ": " + result.reason);
                var after = result.state;
                var settled = after.deals.Single(d => d.id == ask.id);
                var price = after.deals.Single(IsPrice);
                if (decision == "honoured")
                {
                    Assert.That((settled.status, settled.brokenById, settled.settledWeek), Is.EqualTo((DealStatus.Fulfilled, (string)null, after.week)),
                        "The player kept their word on the veto,");
                    Assert.That((price.status, price.brokenById, price.settledWeek), Is.EqualTo((DealStatus.Active, (string)null, 0)), "and the price is owed.");
                    Assert.That(Voided(after, price), Is.False);
                }
                else
                {
                    Assert.That((settled.status, settled.brokenById, settled.settledWeek), Is.EqualTo((DealStatus.Broken, after.playerId, after.week)),
                        decision + ": the player broke their word on the veto,");
                    Assert.That((price.status, price.brokenById, price.settledWeek), Is.EqualTo((DealStatus.Expired, (string)null, 0)),
                        decision + ": so the price is void - broken by nobody, settled never.");
                    Assert.That(Voided(after, price), Is.True);
                    Assert.That(CommitmentsRead.Of(after).Single(c => c.id == price.id).status, Is.EqualTo(VoidedStatus),
                        decision + ": the Your word page says it is void, and why.");
                    var told = after.events.Last(e => e.kind == "deal-outcome" && e.text.Contains(" no longer owes "));
                    Assert.That(told.text, Is.EqualTo(after.Find(ask.proposerId).name
                        + " no longer owes you their vote to keep you, the next time you are on the block: you did not keep the veto commitment it paid for."));
                    Assert.That(told.text, Does.Not.Contain("broke"),
                        decision + ": not a second breach in the weekly recap's count - the veto commitment's own line says that.");
                    Assert.That(told.audienceIds, Is.EquivalentTo(new[] { after.playerId, ask.proposerId }), "Told to the two of them.");
                }
                Assert.That(EpisodeValidation.TryValidate(after, out var error), Is.True, decision + ": " + error);
            }

            // Owed, the price is judged by its own rule: the vote that next tests it.
            var owed = new DealState
            {
                id = PricePrefix + "9", type = DealKind.VoteSave, proposerId = "payer", recipientId = "player", targetId = "player",
                status = DealStatus.Active, week = 1, expiresWeek = 0, trustImpact = DealTrust.Medium,
            };
            var nominees = new List<string> { "player", "other" };
            Assert.That(DealResolution.VoteDeal(owed, new List<VoteState> { new VoteState { voterId = "payer", targetId = "player" } }, nominees, out var broke),
                Is.EqualTo(DealStatus.Broken));
            Assert.That(broke, Is.EqualTo("payer"), "A vote against the player breaks it, by the one who owed it;");
            Assert.That(DealResolution.VoteDeal(owed, new List<VoteState> { new VoteState { voterId = "payer", targetId = "other" } }, nominees, out var kept),
                Is.EqualTo(DealStatus.Fulfilled));
            Assert.That(kept, Is.EqualTo("payer"), "a vote for the other nominee keeps it;");
            Assert.That(DealResolution.VoteDeal(owed, new List<VoteState>(), new List<string> { "a", "b" }, out _), Is.Null, "a vote the player is not up for does not test it.");
        }

        /// <summary>
        /// Who broke what a price bought decides the price, through the settlement itself, on a counter the
        /// player took - a target agreement, paid for with a safety pact the player owes. Broken by the
        /// player, who pays the price, it stands: they still owe it. Broken by the houseguest it is owed to,
        /// it is void, said to the two of them, and the Your word page says so. And a ballot's breach voids
        /// nothing - a vote to evict paid for the same way, broken at the vote by the houseguest - since the
        /// price's line would tell the ballot (decision 4).
        /// </summary>
        [Test]
        public void APriceStandsWhenItsPayerBreaksWhatItBoughtIsVoidWhenItsPayeeDoesAndABallotVoidsNothing()
        {
            var s = Rules(Season(107));
            var asked = Npcs(s)[0];
            string about = Npcs(s).First(c => c.id != asked.id && !s.Allied(asked.id, c.id)).id;
            Set(s, asked.id, about, 0);
            var countered = Refuse(NearMiss(s, DealKind.TargetAgreement, about, coinLands: true, out var npc), npc.id, DealKind.TargetAgreement, about);
            var taken = Take(countered, npc.id);
            var bought = taken.deals.Single(d => d.id.StartsWith(CounterDealPrefix, System.StringComparison.Ordinal));
            var price = taken.deals.Single(IsPrice);
            Assert.That((bought.type, price.type, price.proposerId, price.recipientId),
                Is.EqualTo((DealKind.TargetAgreement, DealKind.SafetyAgreement, taken.playerId, npc.id)), "A target agreement, and a safety pact the player owes for it.");

            var byPayer = taken.Clone();
            Settle(byPayer, bought.id, DealStatus.Broken, byPayer.playerId);
            var owed = byPayer.deals.Single(d => d.id == price.id);
            Assert.That((owed.status, Voided(byPayer, owed)), Is.EqualTo((DealStatus.Active, false)), "Broken by the one who pays the price, it stands: they still owe it,");
            Assert.That(byPayer.events.Any(e => e.text != null && e.text.Contains(" no longer owe")), Is.False, "and nothing says otherwise.");
            Assert.That(EpisodeValidation.TryValidate(byPayer, out var error), Is.True, error);

            var byPayee = taken.Clone();
            Settle(byPayee, bought.id, DealStatus.Broken, npc.id);
            var lapsed = byPayee.deals.Single(d => d.id == price.id);
            Assert.That((lapsed.status, lapsed.brokenById, lapsed.settledWeek), Is.EqualTo((DealStatus.Expired, (string)null, 0)),
                "Broken by the houseguest it is owed to, it is void - lapsed, broken by nobody, settled never -");
            Assert.That(Voided(byPayee, lapsed), Is.True);
            var told = byPayee.events.Last(e => e.kind == "deal-outcome" && e.text.Contains(" no longer owe"));
            Assert.That(told.text, Is.EqualTo("You no longer owe " + npc.name + " a safety pact: " + npc.name + " did not keep the "
                + DealKind.Title(DealKind.TargetAgreement).ToLowerInvariant() + " it paid for."));
            Assert.That(told.audienceIds, Is.EquivalentTo(new[] { byPayee.playerId, npc.id }), "said to the two of them,");
            Assert.That(CommitmentsRead.Of(byPayee).Single(c => c.id == price.id).status, Is.EqualTo(VoidedStatus), "and the Your word page says so.");
            Assert.That(EpisodeValidation.TryValidate(byPayee, out error), Is.True, error);

            var campaign = Campaign(108);
            string nominee = Npcs(campaign)[1].id;
            Set(campaign, Npcs(campaign)[3].id, nominee, 0);
            var vc = Refuse(NearMiss(campaign, DealKind.VoteEvict, nominee, coinLands: true, out var voter, 3), voter.id, DealKind.VoteEvict, nominee);
            var vt = Take(vc, voter.id);
            var vote = vt.deals.Single(d => d.id.StartsWith(CounterDealPrefix, System.StringComparison.Ordinal));
            var paid = vt.deals.Single(IsPrice);
            Assert.That((vote.type, paid.recipientId), Is.EqualTo((DealKind.VoteEvict, voter.id)), "A vote to evict, paid for with a price owed to the voter.");
            Settle(vt, vote.id, DealStatus.Broken, voter.id);
            var stands = vt.deals.Single(d => d.id == paid.id);
            Assert.That((stands.status, Voided(vt, stands)), Is.EqualTo((DealStatus.Active, false)),
                "Broken by the voter's ballot, the price still stands: its line would tell the ballot.");
            Assert.That(vt.events.Any(e => e.text != null && e.text.Contains(" no longer owe")), Is.False);
        }

        /// <summary>
        /// A price that ran out its own week before what it bought was broken lapsed as any deal does, and
        /// is not called void: only a breach while it still bound voids it. A final two paid for with that
        /// week's safety pact, broken by the houseguest weeks later, leaves the pact lapsed; broken the same
        /// week, while the pact bound, voids it.
        /// </summary>
        [Test]
        public void APriceThatRanOutBeforeWhatItBoughtWasBrokenLapsedAndIsNotCalledVoid()
        {
            foreach (int brokenIn in new[] { 2, 5 })
            {
                var s = Rules(Season(111));
                s.week = 5;
                var npc = Npcs(s)[0];
                var bought = SetLink(new DealState
                {
                    id = CounterDealPrefix + "40", type = DealKind.FinalTwo, proposerId = s.playerId, recipientId = npc.id, status = DealStatus.Broken,
                    week = 2, expiresWeek = 0, trustImpact = DealKind.DefaultTrust(DealKind.FinalTwo), brokenById = npc.id, settledWeek = brokenIn,
                }, PricePrefix + "40");
                var price = SetLink(new DealState
                {
                    id = PricePrefix + "40", type = DealKind.SafetyAgreement, proposerId = s.playerId, recipientId = npc.id, status = DealStatus.Expired,
                    week = 2, expiresWeek = 2, trustImpact = DealKind.DefaultTrust(DealKind.SafetyAgreement),
                }, bought.id);
                s.deals.Add(bought); s.deals.Add(price);
                bool bound = brokenIn <= price.expiresWeek;
                string where = "Broken in week " + brokenIn + (bound ? ", while the price bound" : ", after the price ran out");
                Assert.That(Voided(s, price), Is.EqualTo(bound), where + (bound ? ": void." : ": lapsed, and not void."));
                string status = CommitmentsRead.Of(s).Single(c => c.id == price.id).status;
                if (bound) Assert.That(status, Is.EqualTo(VoidedStatus), where);
                else Assert.That(status, Is.Not.EqualTo(VoidedStatus), where);
            }
        }

        /// <summary>
        /// The counter's own pair, through the engine's nominations: a target agreement the player bought from
        /// a houseguest with a safety pact the player owes them, the same week. She wins the house and puts the
        /// player up - at the ceremony, or as the replacement for the nominee the veto saved - which breaks the
        /// target agreement, and the pact with it, both in the one act. The pact is void, and only void: it
        /// lapses, broken by nobody and settled never, said once in the void's own line and never as a breach,
        /// held against nobody, and the Your word page says it is void.
        /// </summary>
        [Test]
        public void APriceVoidedAtTheNominationsIsNotBrokenAgainByItsOwnVerdict()
        {
            foreach (bool replacement in new[] { false, true })
            {
                string where = replacement ? "Named the replacement" : "Nominated";
                var s = CounterForATarget(out var npc, out string about);
                var bought = s.deals.Single(d => d.id.StartsWith(CounterDealPrefix, System.StringComparison.Ordinal));
                var price = s.deals.Single(IsPrice);
                Assert.That((bought.type, price.type, price.proposerId, price.recipientId, price.expiresWeek),
                    Is.EqualTo((DealKind.TargetAgreement, DealKind.SafetyAgreement, s.playerId, npc.id, s.week)), where + ": the pair, this week.");
                var others = Npcs(s).Where(c => c.id != npc.id && c.id != about).Select(c => c.id).ToList();
                // She thinks well of everybody, the target of their agreement too, and nothing of the player
                // - and, at the ceremony, of one other.
                foreach (var c in s.Active.Where(c => c.id != npc.id)) Set(s, npc.id, c.id, 100);
                Set(s, npc.id, s.playerId, -100);
                s.hohId = npc.id;
                if (replacement)
                {
                    // She put up two others, which judged neither deal; the veto holder saves the first of them.
                    s.phase = EpisodePhase.VetoMeeting;
                    s.nominees = new List<string> { others[0], others[1] };
                    s.vetoHolderId = others[2];
                    Lineup(s);
                    Set(s, others[2], others[0], 100); Set(s, others[2], others[1], -100);
                }
                else
                {
                    s.phase = EpisodePhase.Nomination;
                    Set(s, npc.id, others[0], -100);
                }
                Valid(s);
                int seen = s.events.Max(e => e.sequence);

                var result = Apply(new EpisodeEngine(s), EpisodeCommandKind.Advance);
                Assert.That(result.accepted, Is.True, where + ": " + result.reason);
                var after = result.state;
                Assert.That(after.nominees, Does.Contain(after.playerId).And.Not.Contain(about), where + ": the player is up, and not the target;");
                var broke = after.deals.Single(d => d.id == bought.id);
                Assert.That((broke.status, broke.brokenById), Is.EqualTo((DealStatus.Broken, npc.id)), where + ": the target agreement is broken, by her,");
                var lapsed = after.deals.Single(d => d.id == price.id);
                Assert.That((lapsed.status, lapsed.brokenById, lapsed.settledWeek), Is.EqualTo((DealStatus.Expired, (string)null, 0)),
                    where + ": and the pact the player paid for it is void - lapsed, broken by nobody, settled never -");
                Assert.That(Voided(after, lapsed), Is.True, where);
                string pact = DealKind.Title(DealKind.SafetyAgreement).ToLowerInvariant();
                Assert.That(after.events.Where(e => e.sequence > seen && e.text != null && e.text.Contains(pact)).Select(e => e.text),
                    Is.EqualTo(new[] { "You no longer owe " + npc.name + " a " + pact + ": " + npc.name + " did not keep the "
                        + DealKind.Title(DealKind.TargetAgreement).ToLowerInvariant() + " it paid for." }),
                    where + ": said once, as void, and never as a breach,");
                Assert.That(after.relationships.Single(r => r.fromId == after.playerId && r.toId == npc.id).events.Count(e => e.type == "deal_broken"),
                    Is.EqualTo(1), where + ": held against nobody - the player holds the target agreement against her, and nothing more -");
                Assert.That(CommitmentsRead.Of(after).Single(c => c.id == price.id).status, Is.EqualTo(VoidedStatus), where + ": and the Your word page says it is void.");
                Assert.That(EpisodeValidation.TryValidate(after, out var error), Is.True, where + ": " + error);
            }
        }

        /// <summary>
        /// A price struck in free time runs out with its week, though the house writes its lapse only when it
        /// next expires its deals: the same pair, judged at the next week's nominations, where she puts the
        /// player up. What it bought is broken; the pact had run out by then, so it is not void - no line says
        /// it is, and nor does the record - and its own verdict judges it: broken, by her. The Your word page
        /// reads it broken, not void.
        /// </summary>
        [Test]
        public void APriceWhoseWeekHadRunOutIsJudgedByItsOwnRuleNotVoided()
        {
            var s = CounterForATarget(out var npc, out string about);
            var price = s.deals.Single(IsPrice);
            int struck = s.week;
            foreach (var c in s.Active.Where(c => c.id != npc.id)) Set(s, npc.id, c.id, 100);
            Set(s, npc.id, s.playerId, -100);
            Set(s, npc.id, Npcs(s).First(c => c.id != npc.id && c.id != about).id, -100);
            s.week = struck + 1;
            s.phase = EpisodePhase.Nomination;
            s.hohId = npc.id;
            Valid(s);
            Assert.That(price.status, Is.EqualTo(DealStatus.Active), "Its lapse is not written yet.");

            var result = Apply(new EpisodeEngine(s), EpisodeCommandKind.Advance);
            Assert.That(result.accepted, Is.True, result.reason);
            var after = result.state;
            Assert.That(after.nominees, Does.Contain(after.playerId).And.Not.Contain(about), "The player is up, and not the target.");
            var judged = after.deals.Single(d => d.id == price.id);
            Assert.That((judged.status, judged.brokenById, judged.settledWeek), Is.EqualTo((DealStatus.Broken, npc.id, struck + 1)),
                "The pact had run out, so its own verdict judges it: broken, by her.");
            Assert.That(after.events.Any(e => e.text != null && e.text.Contains(" no longer owe")), Is.False, "No line calls it void,");
            Assert.That(Voided(after, judged), Is.False, "nor does the record,");
            var read = CommitmentsRead.Of(after).Single(c => c.id == price.id);
            Assert.That((read.outcome, read.status == VoidedStatus), Is.EqualTo((CommitmentsRead.Outcomes.Broken, false)), "and the Your word page reads it broken.");
            Assert.That(EpisodeValidation.TryValidate(after, out var error), Is.True, error);
        }

        /// <summary>
        /// A price binds only what it names: the vote to keep the player a houseguest owes for the veto -
        /// open until a vote tests it - blocks no vote deal with them about anybody else, so the player can
        /// still put a vote to keep a nominee to them. A vote deal of the player's own with them still
        /// blocks another, as it always did.
        /// </summary>
        [Test]
        public void AVoteSaveOwedForTheVetoBindsOnlyWhatItNames()
        {
            var s = Campaign(104);
            var npcs = Npcs(s);
            var payer = npcs[3];
            s.deals.Add(SetLink(new DealState
            {
                id = "deal-veto-5", type = DealKind.VetoUse, proposerId = payer.id, recipientId = s.playerId, status = DealStatus.Fulfilled,
                week = s.week, expiresWeek = s.week, trustImpact = DealTrust.Critical, settledWeek = s.week,
            }, PricePrefix + "5"));
            s.deals.Add(SetLink(new DealState
            {
                id = PricePrefix + "5", type = DealKind.VoteSave, proposerId = payer.id, recipientId = s.playerId, targetId = s.playerId,
                status = DealStatus.Active, week = s.week, expiresWeek = 0, trustImpact = DealTrust.Medium,
            }, "deal-veto-5"));
            Valid(s);
            Assert.That(PlayerDeals.CanPropose(s, payer.id, DealKind.VoteSave, npcs[1].id, out var why), Is.True,
                "Their vote to keep the player is no vote deal about anybody else: " + why);
            Assert.That(PlayerDeals.Available(s, payer.id), Does.Contain(DealKind.VoteSave), "The table offers one,");
            var put = Apply(new EpisodeEngine(s), EpisodeCommandKind.ProposeDeal, payer.id, npcs[1].id, DealKind.VoteSave);
            Assert.That(put.accepted, Is.True, "and it can be put: " + put.reason);

            var held = Campaign(104);
            var with = Npcs(held)[3];
            held.deals.Add(new DealState
            {
                id = "deal-player-6", type = DealKind.VoteSave, proposerId = held.playerId, recipientId = with.id, targetId = Npcs(held)[2].id,
                status = DealStatus.Active, week = held.week, expiresWeek = held.week, trustImpact = DealTrust.Medium,
            });
            Valid(held);
            Assert.That(PlayerDeals.CanPropose(held, with.id, DealKind.VoteSave, Npcs(held)[1].id, out why), Is.False, "A vote deal of the player's own still blocks another,");
            Assert.That(why, Is.EqualTo("You already have that arrangement with " + with.name + "."));
        }

        /// <summary>
        /// At the season's own ceiling of deals (validation's bound) a nominee's veto ask carries no price
        /// and says none - it is put as it always was - and the player's yes is taken, striking nothing
        /// more: never refused for a price there is no room to strike.
        /// </summary>
        [Test]
        public void AtTheSeasonsDealCeilingAnAskCarriesNoPriceAndItsYesIsNeverRefused()
        {
            var s = VetoMeeting(102, playerHolds: true);
            var ask = Ask(s, s.nominees[0]);
            Assert.That(AskPriceOf(s, ask), Is.Not.Null, "With room, the ask carries a price.");
            Fill(s, NpcDeals.DealCeiling);
            Assert.That(AskPriceOf(s, ask), Is.Null, "With the season full it carries none,");
            Assert.That(AskPriceLineOf(s, ask), Is.Null, "and says none.");
            var yes = Apply(new EpisodeEngine(s), EpisodeCommandKind.RespondToDeal, ask.id, null, AcceptAnswer);
            Assert.That(yes.accepted, Is.True, "The yes is taken: " + yes.reason);
            Assert.That(yes.state.deals.Count, Is.EqualTo(NpcDeals.DealCeiling), "and strikes nothing more,");
            Assert.That(yes.state.deals.Single(d => d.id == ask.id).status, Is.EqualTo(DealStatus.Active), "the ask taken as it always was.");
        }

        /// <summary>
        /// Owed, a vote save is an obligation in its payer's ballot whenever the player is on the block, as
        /// any vote deal of the player's is (the levers' Obligations): the term is on keeping the player.
        /// </summary>
        [Test]
        public void AnOwedVoteSaveWeighsInItsPayersBallotWhenThePlayerIsUp()
        {
            var s = Campaign(88);
            var npcs = Npcs(s);
            s.nominees = new List<string> { s.playerId, npcs[1].id };
            var payer = Plain(npcs[2]);
            Set(s, payer.id, s.playerId, 50);
            s.deals.Add(SetLink(new DealState
            {
                id = "deal-veto-3", type = DealKind.VetoUse, proposerId = payer.id, recipientId = s.playerId, status = DealStatus.Fulfilled,
                week = s.week, expiresWeek = s.week, trustImpact = DealTrust.Critical, settledWeek = s.week,
            }, PricePrefix + "4"));
            s.deals.Add(SetLink(new DealState
            {
                id = PricePrefix + "4", type = DealKind.VoteSave, proposerId = payer.id, recipientId = s.playerId, targetId = s.playerId,
                status = DealStatus.Active, week = s.week, expiresWeek = 0, trustImpact = DealTrust.Medium,
            }, "deal-veto-3"));
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
            var term = EpisodeEngine.Obligations(s, payer.id).SingleOrDefault(t => t.nomineeId == s.playerId);
            Assert.That(term, Is.Not.Null, "The price is a term in their ballot,");
            Assert.That(term.value, Is.GreaterThan(0), "on keeping the player.");
        }

        /// <summary>
        /// The price a nominee's veto ask carries is part of the bargain the player accepted, as a counter's
        /// price is: struck by the same yes, its payer breaking it weighs one step heavier (decision 15), on
        /// the player's record of them, through the settlement itself. The same price struck any other way -
        /// named by the player, holding the veto - is the player's own ask, and weighs its own. And the rule
        /// is the offer's, whatever the offer: a price on any offer put to the player and accepted is as heavy.
        /// </summary>
        [Test]
        public void AnAcceptedAsksPriceBrokenByItsPayerWeighsOneStepHeavier()
        {
            var s = VetoMeeting(112, playerHolds: true);
            var ask = Ask(s, s.nominees[0]);
            var accepted = Apply(new EpisodeEngine(s), EpisodeCommandKind.RespondToDeal, ask.id, null, AcceptAnswer).state;
            var price = accepted.deals.Single(IsPrice);
            Assert.That((price.type, price.proposerId, price.recipientId), Is.EqualTo((DealKind.VoteSave, ask.proposerId, accepted.playerId)),
                "The ask's price: their vote to keep the player, theirs to pay.");
            double own = DealTrust.Weight(price.trustImpact), heavier = DealTrust.Weight(DealTrust.Heavier(price.trustImpact));
            Assert.That(heavier, Is.GreaterThan(own), "A vote save has a heavier step to take.");
            Assert.That(DealResolution.AcceptedOffer(accepted, price), Is.True, "Struck by the yes, the price is part of the offer the player accepted,");
            Assert.That(DealResolution.BreachWeight(accepted, price), Is.EqualTo(heavier), "so broken it weighs one step heavier,");
            Settle(accepted, price.id, DealStatus.Broken, ask.proposerId);
            Assert.That(BrokenEntry(accepted, ask.proposerId).impactScore, Is.EqualTo(DealResolution.BrokenBase * heavier).Within(1e-9),
                "and is held so on the player's record of the one who broke it.");

            var named = VetoMeeting(112, playerHolds: true);
            var nominee = Plain(named.Find(named.nominees[0]));
            named.randomState = Draw(named, true, MoveChance(named, nominee.id, VetoMove, false));
            var struck = Apply(new EpisodeEngine(named), NegotiateKind, nominee.id, null, VetoPriceOf(DealKind.VoteSave)).state;
            var same = struck.deals.Single(IsPrice);
            Assert.That((same.type, same.proposerId, same.recipientId, same.trustImpact), Is.EqualTo((price.type, price.proposerId, price.recipientId, price.trustImpact)),
                "The same price, named by the player for the veto,");
            Assert.That(DealResolution.AcceptedOffer(struck, same), Is.False, "is no offer of theirs the player accepted,");
            Settle(struck, same.id, DealStatus.Broken, nominee.id);
            Assert.That(BrokenEntry(struck, nominee.id).impactScore, Is.EqualTo(DealResolution.BrokenBase * own).Within(1e-9), "and broken weighs its own.");

            var offered = Rules(Season(112));
            var from = Npcs(offered)[0];
            string offer = NpcDeals.OfferPrefix + "7";
            offered.deals.Add(SetLink(new DealState
            {
                id = offer, type = DealKind.Partnership, proposerId = from.id, recipientId = offered.playerId, status = DealStatus.Active,
                week = offered.week, expiresWeek = 0, trustImpact = DealKind.DefaultTrust(DealKind.Partnership),
            }, PricePrefix + "7"));
            offered.deals.Add(SetLink(new DealState
            {
                id = PricePrefix + "7", type = DealKind.SafetyAgreement, proposerId = from.id, recipientId = offered.playerId, status = DealStatus.Active,
                week = offered.week, expiresWeek = offered.week, trustImpact = DealKind.DefaultTrust(DealKind.SafetyAgreement),
            }, offer));
            Valid(offered);
            Assert.That(DealResolution.AcceptedOffer(offered, offered.deals.Single(IsPrice)), Is.True,
                "A price on any offer put to the player and accepted is part of it.");
        }

        // ------------------------------------------------------------ calling in a promise

        /// <summary>
        /// Calling in a promise of safety the houseguest made the player, each of the web's three ways, as it
        /// lands and as it does not: one roll on the season's stream against the approach's own chance (60,
        /// 50, 40, and half their view of the player), the action spent, and its own cost in their view of
        /// the player whatever they answer - nothing for a reminder, 5 for a demand, 15 for a threat - one
        /// way. Where it lands they are held to it, once, half again or twice over; where it does not, they
        /// are not. Either way it is on their record, and called in for the week.
        /// </summary>
        [Test]
        public void CallingInAPromiseRollsOnItsApproachsOddsAndCostsWhatTheWebSays()
        {
            foreach (string approach in Approaches)
            foreach (bool lands in new[] { true, false })
            {
                var s = Rules(Season(89));
                var npc = Plain(Npcs(s)[0]);
                Set(s, npc.id, s.playerId, 20); Set(s, s.playerId, npc.id, 20);
                var promise = StoryPromise(s, npc.id, PromiseKind.Safety);
                string where = approach + (lands ? ", landing" : ", missing");
                double chance = MoveChance(s, npc.id, approach, false);
                Assert.That(chance, Is.EqualTo(BaseOf(approach) + 10).Within(1e-9), where + ": the web's base, and half their view.");
                Assert.That(MoveChance(s, npc.id, approach, true), Is.EqualTo(chance).Within(1e-9), where + ": known alike, the chance shown is the roll's.");
                s.randomState = Draw(s, lands, chance);
                var stream = new SeededRandom(s.randomState);
                stream.NextDouble();

                var result = Apply(new EpisodeEngine(s), NegotiateKind, npc.id, promise.id, CallInMove(approach));
                Assert.That(result.accepted, Is.True, where + ": " + result.reason);
                var after = result.state;
                Assert.That(after.randomState, Is.EqualTo(stream.State), where + ": one roll on the season's stream.");
                Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(s) + 1), where + ": the action is spent.");
                Assert.That(after.Score(npc.id, after.playerId), Is.EqualTo(20 + CostOf(approach)).Within(1e-9), where + ": its cost, whatever they answer,");
                Assert.That(after.Score(after.playerId, npc.id), Is.EqualTo(20).Within(1e-9), where + ": in their view of the player alone.");
                Assert.That(HeldTo(after, after.promises.Single(p => p.id == promise.id)), Is.EqualTo(lands ? HoldOf(approach) : 0).Within(1e-9),
                    where + ": held to it as hard as the approach, where it landed.");
                var entry = after.relationships.Single(r => r.fromId == npc.id && r.toId == after.playerId).events.Last();
                Assert.That(entry.type, Does.StartWith(lands ? "promise-held:" : "promise-pressed:"), where + ": on their record of the player.");
                Assert.That(after.events.Last().audienceIds, Is.EqualTo(new[] { after.playerId, npc.id }), where + ": said between the two of them.");

                var twice = Apply(new EpisodeEngine(after), NegotiateKind, npc.id, promise.id, CallInMove(Remind));
                Assert.That(twice.accepted, Is.False, where + ": once a week.");
                Assert.That(twice.reason, Is.EqualTo("You have already called that promise in this week."));
            }
        }

        /// <summary>
        /// A promise of safety the player held its maker to weighs in that maker's nominations, as Head of
        /// Household: a safety deal's weight, times how hard it was held. Nothing for anybody else, nothing for
        /// a call-in that missed, nothing for a promise never called in.
        /// </summary>
        [Test]
        public void AHeldPromiseOfSafetyWeighsInItsMakersNominations()
        {
            var s = Rules(Season(90));
            var npc = Plain(Npcs(s)[0]);
            Set(s, npc.id, s.playerId, 20); Set(s, s.playerId, npc.id, 20);
            var promise = StoryPromise(s, npc.id, PromiseKind.Safety);
            Assert.That(StrategyRules.NominationReluctance(s, npc.id, s.playerId), Is.EqualTo(s.Score(npc.id, s.playerId)).Within(1e-9),
                "Never called in, the promise weighs nothing.");
            s.randomState = Draw(s, true, MoveChance(s, npc.id, Demand, false));
            var after = Apply(new EpisodeEngine(s), NegotiateKind, npc.id, promise.id, CallInMove(Demand)).state;
            after.hohId = npc.id;
            double weight = StrategyRules.DealWeight(DealKind.SafetyAgreement) * HoldOf(Demand);
            Assert.That(StrategyRules.NominationReluctance(after, npc.id, after.playerId), Is.EqualTo(after.Score(npc.id, after.playerId) + weight).Within(1e-9),
                "Held to it by a demand: a safety deal's weight, half again.");
            Assert.That(StrategyRules.NominationReluctance(after, npc.id, Npcs(after)[1].id), Is.EqualTo(after.Score(npc.id, Npcs(after)[1].id)).Within(1e-9),
                "Nobody else is spared by it.");

            s.randomState = Draw(s, false, MoveChance(s, npc.id, Demand, false));
            var missed = Apply(new EpisodeEngine(s), NegotiateKind, npc.id, promise.id, CallInMove(Demand)).state;
            Assert.That(StrategyRules.NominationReluctance(missed, npc.id, missed.playerId), Is.EqualTo(missed.Score(npc.id, missed.playerId)).Within(1e-9),
                "A demand they refused holds them to nothing.");
        }

        /// <summary>
        /// A final two promise the player held its maker to is an obligation in that maker's final choice: a
        /// final two deal's term (25 at a view of fifty), times how hard it was held - a threat twice over.
        /// </summary>
        [Test]
        public void AHeldFinalTwoPromiseIsAnObligationInItsMakersFinalChoice()
        {
            var s = Rules(Season(91));
            var npc = Plain(Npcs(s)[0]);
            Set(s, npc.id, s.playerId, 60); Set(s, s.playerId, npc.id, 60);
            var promise = StoryPromise(s, npc.id, PromiseKind.FinalTwo);
            var finalists = new List<string> { s.playerId, Npcs(s)[1].id };
            Assert.That(EpisodeEngine.FinalTwoTerms(s, npc.id, finalists), Is.Empty, "Never called in, it is no term.");
            s.randomState = Draw(s, true, MoveChance(s, npc.id, Threaten, false));
            var after = Apply(new EpisodeEngine(s), NegotiateKind, npc.id, promise.id, CallInMove(Threaten)).state;
            var term = EpisodeEngine.FinalTwoTerms(after, npc.id, finalists).Single();
            Assert.That(term.nomineeId, Is.EqualTo(after.playerId));
            double scale = after.Score(npc.id, after.playerId) / EpisodeEngine.ObligationFullView;
            Assert.That(term.value, Is.EqualTo(EpisodeEngine.FinalTwoObligation * scale * HoldOf(Threaten)).Within(1e-9),
                "A final two deal's term, twice over for a threat that landed.");
        }

        /// <summary>
        /// The odds a move is shown with are the player's read: two houseguests who see the player very
        /// differently, but whom the player knows alike, show the same chance, while the roll reads how each
        /// really sees them.
        /// </summary>
        [Test]
        public void AMovesShownOddsAreThePlayersReadNeverTheHiddenView()
        {
            var s = Rules(Season(92));
            var warm = Plain(Npcs(s)[0]);
            var cold = Plain(Npcs(s)[1]);
            Set(s, s.playerId, warm.id, 20); Set(s, s.playerId, cold.id, 20);
            Set(s, warm.id, s.playerId, 90); Set(s, cold.id, s.playerId, -90);
            foreach (string move in new[] { Remind, Demand, Threaten, MendMove, VetoMove })
            {
                Assert.That(MoveChance(s, warm.id, move, false), Is.GreaterThan(MoveChance(s, cold.id, move, false)), move + ": the roll reads how each sees the player,");
                Assert.That(MoveChance(s, cold.id, move, true), Is.EqualTo(MoveChance(s, warm.id, move, true)).Within(1e-9), move + ": the read does not.");
            }
        }

        // ------------------------------------------------------------ mending fences

        /// <summary>
        /// Mending fences after a breach of the player's: the web's odds (55, less 30 for the deal between
        /// them the player broke), one roll, the action spent. Landed, their view of the player rises by the
        /// web's 10 and their grudge eases as much; missed, it falls by 5. One way, permanent on their record
        /// - and the breach is untouched: the deal still broken, by the player, still held against them. Once
        /// for each breach, landed or not.
        /// </summary>
        [Test]
        public void MendingFencesIsABoundedRepairThatNeverErasesTheBreach()
        {
            foreach (bool lands in new[] { true, false })
            {
                var s = Rules(Season(93));
                EpisodeEngine.EnableStory(s);
                var npc = Plain(Npcs(s)[0]);
                Set(s, npc.id, s.playerId, 20); Set(s, s.playerId, npc.id, 20);
                var breach = BrokenByThePlayer(s, npc.id);
                Grudges.Add(s, npc.id, s.playerId, 60, GrudgeCauses.DealBroken);
                string where = lands ? "Landed" : "Missed";
                Assert.That(MendRefusalOf(s, npc.id), Is.Null, where + ": a breach of the player's to make amends for.");
                double chance = MoveChance(s, npc.id, MendMove, false);
                Assert.That(chance, Is.EqualTo(55 + 10 - 30).Within(1e-9), where + ": the web's 55, half their view, less 30 for the broken deal.");
                int brokenBefore = NpcDeals.BrokenDeals(s, s.playerId);
                s.randomState = Draw(s, lands, chance);
                var stream = new SeededRandom(s.randomState);
                stream.NextDouble();

                var result = Apply(new EpisodeEngine(s), NegotiateKind, npc.id, null, MendMove);
                Assert.That(result.accepted, Is.True, where + ": " + result.reason);
                var after = result.state;
                Assert.That(after.randomState, Is.EqualTo(stream.State), where + ": one roll.");
                Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(s) + 1), where + ": the action spent.");
                Assert.That(after.Score(npc.id, after.playerId), Is.EqualTo(lands ? 30 : 15).Within(1e-9), where + ": their view of the player,");
                Assert.That(after.Score(after.playerId, npc.id), Is.EqualTo(20).Within(1e-9), where + ": one way.");
                Assert.That(Grudges.Severity(after, npc.id, after.playerId), Is.EqualTo(lands ? 50 : 60).Within(1e-9), where + ": the grudge eases only where it landed.");
                var entry = after.relationships.Single(r => r.fromId == npc.id && r.toId == after.playerId).events.Last();
                Assert.That((entry.decayable, entry.impactScore), Is.EqualTo((false, lands ? 10.0 : -5.0)), where + ": permanent on their record.");
                var still = after.deals.Single(d => d.id == breach.id);
                Assert.That((still.status, still.brokenById, still.settledWeek), Is.EqualTo((DealStatus.Broken, after.playerId, breach.settledWeek)),
                    where + ": the breach is untouched,");
                Assert.That(NpcDeals.BrokenDeals(after, after.playerId), Is.EqualTo(brokenBefore), where + ": and still held against the player.");
                var again = Apply(new EpisodeEngine(after), NegotiateKind, npc.id, null, MendMove);
                Assert.That(again.accepted, Is.False, where + ": once for each breach.");
                Assert.That(again.reason, Is.EqualTo("You have tried to make amends for every word you broke with " + npc.name + "."));
            }
        }

        /// <summary>Nothing to mend where the player broke nothing: a breach of the houseguest's is theirs, not the player's to make amends for.</summary>
        [Test]
        public void ThereAreNoFencesToMendWhereThePlayerBrokeNothing()
        {
            var s = Rules(Season(94));
            var npc = Plain(Npcs(s)[0]);
            Assert.That(MendRefusalOf(s, npc.id), Is.EqualTo("You have broken no word with " + npc.name + " to make amends for."));
            s.deals.Add(new DealState
            {
                id = "deal-npc-broke", type = DealKind.SafetyAgreement, proposerId = npc.id, recipientId = s.playerId, status = DealStatus.Broken,
                week = s.week, expiresWeek = s.week, trustImpact = DealTrust.High, brokenById = npc.id, settledWeek = s.week,
            });
            Assert.That(MendRefusalOf(s, npc.id), Is.Not.Null, "Their breach is not the player's to mend.");
            var refused = Apply(new EpisodeEngine(s), NegotiateKind, npc.id, null, MendMove);
            Assert.That(refused.accepted, Is.False);
            Assert.That(refused.state.revision, Is.EqualTo(s.revision), "and nothing is spent.");
        }

        /// <summary>
        /// A voting bloc that fell apart is broken by both ballots at once, and the player knows only their
        /// own (decision 4): the player voted with the house and their partner did not, and a 3-2 count
        /// proves nobody's ballot. Until the player knows the partner's there are no fences to mend over it
        /// - no row, and the engine refuses the move - and the odds every move with them is shown with carry
        /// nothing of it, while the roll still reads the breach, as it reads their true view. Told how the
        /// partner voted, and the reveal bearing it out, the player knows: the mend opens, and the odds shown
        /// are the roll's.
        /// </summary>
        [Test]
        public void ABlocTheOthersSecretBallotBrokeIsNoBreachToMendUntilThePlayerKnowsHowTheyVoted()
        {
            var s = Rules(Season(95));
            var npcs = Npcs(s);
            string hoh = npcs[0].id, evicted = npcs[1].id, other = npcs[2].id;
            var partner = Plain(npcs[3]);
            s.week = 2;
            s.ledger.power.Add(new PowerRow
            {
                week = 1, hohId = hoh, evicteeId = evicted, nominees = new List<string> { evicted, other }, tally = new List<int> { 3, 2 },
            });
            s.ledger.ballots.Add(new BallotRow { week = 1, voterId = s.playerId, targetId = evicted });
            var bloc = new DealState
            {
                id = "deal-player-bloc", type = DealKind.VoteTogether, proposerId = s.playerId, recipientId = partner.id, status = DealStatus.Broken,
                week = 1, expiresWeek = 1, trustImpact = DealKind.DefaultTrust(DealKind.VoteTogether), settledWeek = 1,
            };
            s.deals.Add(bloc);
            Set(s, partner.id, s.playerId, 20); Set(s, s.playerId, partner.id, 20);
            Valid(s);
            Assert.That(KnownBallots.Read(s, 1).voters, Has.Count.EqualTo(5).And.Contains(partner.id), "Five voted, the partner among them;");
            Assert.That(KnownBallots.DealOutcomeKnown(s, bloc), Is.False, "the count proves nothing of the partner's ballot,");
            Assert.That(Breaches.CountsAgainst(s, bloc, s.playerId), Is.True, "though both ballots broke the bloc, the player's among them.");

            Assert.That(MendRefusalOf(s, partner.id), Is.EqualTo("You have broken no word with " + partner.name + " to make amends for."),
                "No fences to mend: the row would tell the player the partner voted the other way.");
            var refused = Apply(new EpisodeEngine(s), NegotiateKind, partner.id, null, MendMove);
            Assert.That(refused.accepted, Is.False, "The engine refuses it too,");
            Assert.That(refused.state.revision, Is.EqualTo(s.revision), "and nothing is spent.");
            foreach (string move in new[] { Remind, Demand, Threaten, MendMove, VetoMove })
            {
                Assert.That(MoveChance(s, partner.id, move, true), Is.EqualTo(BaseOf(move) + 10).Within(1e-9),
                    move + ": the odds shown carry nothing of a breach the player cannot know of,");
                Assert.That(MoveChance(s, partner.id, move, false), Is.EqualTo(BaseOf(move) + 10 - 30).Within(1e-9),
                    move + ": while the roll reads it, as it reads their true view.");
            }

            s.ledger.claims.Add(new ClaimRow { week = 1, voterId = partner.id, targetId = other, source = ClaimSource.Told, status = ClaimStatus.Kept });
            Assert.That(KnownBallots.DealOutcomeKnown(s, bloc), Is.True, "Told, and the reveal bore it out: the player knows how the partner voted.");
            Assert.That(MendRefusalOf(s, partner.id), Is.Null, "Now there are fences to mend,");
            foreach (string move in new[] { Remind, Demand, Threaten, MendMove, VetoMove })
                Assert.That(MoveChance(s, partner.id, move, true), Is.EqualTo(MoveChance(s, partner.id, move, false)).Within(1e-9),
                    move + ": and the odds shown are the roll's.");
            var mended = Apply(new EpisodeEngine(s), NegotiateKind, partner.id, null, MendMove);
            Assert.That(mended.accepted, Is.True, mended.reason);
            Assert.That(mended.state.events.Last(e => e.kind == "amends").text,
                Does.Contain("the " + DealKind.Title(DealKind.VoteTogether).ToLowerInvariant() + " you broke"), "The mend names the bloc.");
        }

        // ------------------------------------------------------------ a veto for a price

        /// <summary>
        /// A veto for a price, the web's own (the veto holder's leverage): the player, holding the veto before
        /// the meeting, names a price to a nominee - their vote to keep the player, or at the endgame a final
        /// two - at the web's odds (75, and half their view), one roll. Taken: the player's word on the veto
        /// and the price, struck at once and linked, the nominee's own ask answered by it, and the other
        /// nominee can no longer be given a word the veto cannot keep. Refused: nothing struck.
        /// </summary>
        [Test]
        public void AVetoForAPriceStrikesTheVetoAndItsPriceLinkedOrNothing()
        {
            foreach (bool taken in new[] { true, false })
            {
                var s = VetoMeeting(95, playerHolds: true);
                var nominee = Plain(s.Find(s.nominees[0]));
                Set(s, nominee.id, s.playerId, 20); Set(s, s.playerId, nominee.id, 20);
                var ask = Ask(s, nominee.id);
                Assert.That(VetoPricesOf(s, nominee.id), Is.EqualTo(new[] { DealKind.VoteSave }), "In a house of eight, their vote to keep the player.");
                double chance = MoveChance(s, nominee.id, VetoMove, false);
                Assert.That(chance, Is.EqualTo(75 + 10).Within(1e-9), "The web's 75, and half their view.");
                s.randomState = Draw(s, taken, chance);
                string where = taken ? "Taken" : "Refused";

                var result = Apply(new EpisodeEngine(s), NegotiateKind, nominee.id, null, VetoPriceOf(DealKind.VoteSave));
                Assert.That(result.accepted, Is.True, where + ": " + result.reason);
                var after = result.state;
                Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(s) + 1), where + ": a word in the window, spent.");
                if (!taken)
                {
                    Assert.That(after.deals.Count, Is.EqualTo(s.deals.Count), "Refused: nothing struck,");
                    Assert.That(after.deals.Single(d => d.id == ask.id).status, Is.EqualTo(DealStatus.Proposed), "and their own ask still stands.");
                    Assert.That(after.events.Last().text, Is.EqualTo(nominee.name + " would not pay your price for the veto."));
                    continue;
                }
                var veto = after.deals.Single(d => d.type == DealKind.VetoUse && d.proposerId == after.playerId);
                var price = after.deals.Single(IsPrice);
                Assert.That((veto.recipientId, veto.status, veto.expiresWeek), Is.EqualTo((nominee.id, DealStatus.Active, after.week)), "The player's word on the veto,");
                Assert.That((price.type, price.proposerId, price.recipientId, price.targetId, price.status),
                    Is.EqualTo((DealKind.VoteSave, nominee.id, after.playerId, after.playerId, DealStatus.Active)), "and the price, theirs to pay,");
                Assert.That((Link(veto), Link(price)), Is.EqualTo((price.id, veto.id)), "linked.");
                Assert.That(after.deals.Single(d => d.id == ask.id).status, Is.EqualTo(DealStatus.Expired), "Their own ask is answered by it.");
                Assert.That(VetoPriceRefusalOf(after, after.nominees[1], DealKind.VoteSave),
                    Is.EqualTo("You have already given your word on the veto this week, and it saves one of them."), "The other nominee cannot be given a word too.");
                Assert.That(EpisodeValidation.TryValidate(after, out var error), Is.True, error);

                // Honoured, the price is owed; not, it is void.
                foreach (bool use in new[] { true, false })
                {
                    var decide = Command(after, EpisodeCommandKind.ResolveVeto, use ? nominee.id : null, EpisodeEngine.ReplacementCandidates(after).First().id);
                    decide.useVeto = use;
                    var resolved = new EpisodeEngine(after).Apply(decide);
                    Assert.That(resolved.accepted, Is.True, resolved.reason);
                    var owed = resolved.state.deals.Single(IsPrice);
                    Assert.That(owed.status, Is.EqualTo(use ? DealStatus.Active : DealStatus.Expired), use ? "Saved: the price is owed." : "Not saved: the price is void.");
                }
            }

            var endgame = VetoMeeting(96, playerHolds: true, size: 6);
            Assert.That(VetoPricesOf(endgame, endgame.nominees[0]), Is.EquivalentTo(new[] { DealKind.VoteSave, DealKind.FinalTwo }), "At the endgame a final two can be named too.");
        }

        /// <summary>
        /// A veto for a price answers the nominee's own ask with the player's word, so the ask is a chance
        /// the player took: reconciled, its row says taken, and Game Sense never calls it an offer left on
        /// the table. An ask the player let lapse unanswered still is one.
        /// </summary>
        [Test]
        public void AVetoForAPriceAnswersTheNomineesAskAsAChanceTakenNeverAnOfferLeftOnTheTable()
        {
            var s = VetoMeeting(105, playerHolds: true);
            var nominee = Plain(s.Find(s.nominees[0]));
            Set(s, nominee.id, s.playerId, 20); Set(s, s.playerId, nominee.id, 20);
            var ask = Ask(s, nominee.id);
            s.randomState = Draw(s, true, MoveChance(s, nominee.id, VetoMove, false));
            var after = Apply(new EpisodeEngine(s), NegotiateKind, nominee.id, null, VetoPriceOf(DealKind.VoteSave)).state;
            Assert.That(after.deals.Single(d => d.id == ask.id).status, Is.EqualTo(DealStatus.Expired), "The ask is answered by the price,");
            EpisodeEngine.ReconcileOpportunities(after);
            Assert.That(after.ledger.opportunities.Single(o => o.id == ask.id).response, Is.EqualTo(OpportunityResponse.Taken), "a chance the player took,");
            Assert.That(GameSense.Evaluate(after).notes.Any(n => n.rowId == ask.id && n.text.Contains("an offer left on the table")), Is.False,
                "never an offer left on the table.");

            var lapsed = VetoMeeting(105, playerHolds: true);
            var left = Ask(lapsed, lapsed.nominees[0]);
            left.status = DealStatus.Expired;
            EpisodeEngine.ReconcileOpportunities(lapsed);
            Assert.That(lapsed.ledger.opportunities.Single(o => o.id == left.id).response, Is.EqualTo(OpportunityResponse.Expired));
            Assert.That(GameSense.Evaluate(lapsed).notes.Any(n => n.rowId == left.id && n.text.Contains("an offer left on the table")), Is.True,
                "An ask the player let lapse unanswered still is one.");
        }

        /// <summary>
        /// A veto for a price strikes two deals, so it needs room for both under the player's own ceiling:
        /// with room for two it can be named; with room for one it is refused in the deal table's own
        /// words and not offered, and the refusal spends, draws and mints nothing.
        /// </summary>
        [Test]
        public void AVetoForAPriceNeedsRoomForBothItsDealsUnderThePlayersCeiling()
        {
            var s = VetoMeeting(103, playerHolds: true);
            string nominee = s.nominees[0];
            Fill(s, PlayerDeals.PlayerDealCeiling - 2);
            Assert.That(VetoPriceRefusalOf(s, nominee, DealKind.VoteSave), Is.Null, "Room for both: the price can be named.");
            Fill(s, PlayerDeals.PlayerDealCeiling - 1);
            Assert.That(VetoPriceRefusalOf(s, nominee, DealKind.VoteSave), Is.EqualTo(TooManyArrangements), "Room for one: refused,");
            Assert.That(VetoPricesOf(s, nominee), Is.Empty, "and not offered.");
            var full = VetoMeeting(103, playerHolds: true);
            Fill(full, PlayerDeals.PlayerDealCeiling);
            Assert.That(PlayerDeals.CanPropose(full, Npcs(full)[4].id, DealKind.Partnership, null, out var tableWords), Is.False);
            Assert.That(TooManyArrangements, Is.EqualTo(tableWords), "The refusal is the deal table's own words.");
            var refused = Apply(new EpisodeEngine(s), NegotiateKind, nominee, null, VetoPriceOf(DealKind.VoteSave));
            Assert.That(refused.accepted, Is.False);
            Assert.That(refused.reason, Is.EqualTo(TooManyArrangements));
            Assert.That((refused.state.revision, refused.state.randomState, refused.state.nextSequence), Is.EqualTo((s.revision, s.randomState, s.nextSequence)),
                "Nothing is spent, drawn or minted.");
        }

        /// <summary>Each move is offered only where it can happen: who holds the veto, who is on the block, which promise, which breach.</summary>
        [Test]
        public void EachMoveIsRefusedWhereItCannotHappen()
        {
            var s = VetoMeeting(97, playerHolds: true);
            Assert.That(VetoPriceRefusalOf(s, s.nominees[0], DealKind.FinalTwo), Is.EqualTo("It is too early in the season to be talking about the final two."));
            Assert.That(VetoPriceRefusalOf(s, s.hohId, DealKind.VoteSave), Is.EqualTo("Name a price to somebody on the block."));
            var held = VetoMeeting(97, playerHolds: false);
            Assert.That(VetoPriceRefusalOf(held, held.nominees[0], DealKind.VoteSave), Is.EqualTo("Only the veto holder can name a price, before the veto meeting decides."));
            var free = Rules(Season(97));
            Assert.That(VetoPriceRefusalOf(free, Npcs(free)[0].id, DealKind.VoteSave), Is.Not.Null, "Not outside the veto meeting.");

            var npc = Plain(Npcs(free)[0]);
            var promise = StoryPromise(free, npc.id, PromiseKind.Safety);
            Assert.That(OwedTo(free, npc.id).Select(p => p.id), Is.EqualTo(new[] { promise.id }), "Their promise can be called in;");
            Assert.That(OwedTo(free, Npcs(free)[1].id), Is.Empty, "nobody else owes the player one.");
            Assert.That(CallInRefusalOf(free, Npcs(free)[1].id, promise.id, Remind), Is.EqualTo("Name a promise " + Npcs(free)[1].name + " made you."));
            Assert.That(CallInRefusalOf(free, npc.id, promise.id, "beg"), Is.EqualTo("That is no way to call in a promise."));
            var loyalty = StoryPromise(free, npc.id, PromiseKind.AllianceLoyalty);
            Assert.That(CallInRefusalOf(free, npc.id, loyalty.id, Remind), Is.EqualTo("Nothing that promise binds is decided anywhere you could hold them to it."));
            Assert.That(OwedTo(free, npc.id).Any(p => p.id == loyalty.id), Is.False, "and it is not offered.");
            var mine = new PromiseState { id = "promise-mine", fromId = free.playerId, toId = npc.id, kind = PromiseKind.Safety, status = PromiseStatus.Active, week = 1, expiresWeek = 2 };
            free.promises.Add(mine);
            Assert.That(CallInRefusalOf(free, npc.id, mine.id, Remind), Is.Not.Null, "The player's own promise is not theirs to call in.");
        }

        /// <summary>
        /// Without the rules nothing of this moves: no move is taken, and refused, nothing is spent, drawn or
        /// minted; a counter cannot be answered; the shown and rolled chances are never asked.
        /// </summary>
        [Test]
        public void WithoutTheRulesNoMoveIsTakenAndNothingIsSpent()
        {
            var s = Levers(Season(98));
            var npc = Plain(Npcs(s)[0]);
            var promise = StoryPromise(s, npc.id, PromiseKind.Safety);
            BrokenByThePlayer(s, npc.id, rules: false);
            foreach (var (second, move) in new (string second, string move)[] { (promise.id, CallInMove(Remind)), (null, MendMove), (null, VetoPriceOf(DealKind.VoteSave)) })
            {
                var result = Apply(new EpisodeEngine(s), NegotiateKind, npc.id, second, move);
                Assert.That(result.accepted, Is.False, move);
                Assert.That(result.reason, Is.EqualTo(Negotiation.NotThisSeason), move);
                Assert.That((result.state.revision, result.state.randomState, result.state.nextSequence), Is.EqualTo((s.revision, s.randomState, s.nextSequence)), move);
            }
            Assert.That(OwedTo(s, npc.id), Is.Empty);
            Assert.That(MendRefusalOf(s, npc.id), Is.EqualTo(Negotiation.NotThisSeason));
        }

        // ------------------------------------------------------------ the link's shape

        /// <summary>
        /// Validation holds a link to its shape: a deal and its price name each other, both ways, the same two
        /// houseguests, struck the same week, exactly one of them a price; a price never stands alone; and a
        /// season without the rules holds no link at all.
        /// </summary>
        [Test]
        public void ValidationHoldsALinkToItsShape()
        {
            EpisodeState Linked(System.Action<EpisodeState, DealState, DealState> damage = null, bool rules = true)
            {
                var s = rules ? Rules(Season(99)) : Levers(Season(99));
                var npc = Npcs(s)[0];
                var bought = SetLink(new DealState
                {
                    id = "deal-player-5", type = DealKind.Partnership, proposerId = s.playerId, recipientId = npc.id, status = DealStatus.Active,
                    week = s.week, expiresWeek = 0, trustImpact = DealTrust.Medium,
                }, PricePrefix + "5");
                var price = SetLink(new DealState
                {
                    id = PricePrefix + "5", type = DealKind.SafetyAgreement, proposerId = s.playerId, recipientId = npc.id, status = DealStatus.Active,
                    week = s.week, expiresWeek = s.week, trustImpact = DealTrust.High,
                }, "deal-player-5");
                damage?.Invoke(s, bought, price);
                s.deals.Add(bought); s.deals.Add(price);
                return s;
            }
            Assert.That(EpisodeValidation.TryValidate(Linked(), out var error), Is.True, error);
            var damages = new Dictionary<string, System.Action<EpisodeState, DealState, DealState>>
            {
                { "a link to nothing", (s, b, p) => SetLink(b, "deal-gone") },
                { "a link one way", (s, b, p) => SetLink(p, null) },
                { "a price for a price", (s, b, p) => { b.id = PricePrefix + "6"; SetLink(p, b.id); } },
                { "two deals and no price", (s, b, p) => { p.id = "deal-player-6"; SetLink(b, p.id); } },
                { "another pair", (s, b, p) => p.recipientId = Npcs(s)[1].id },
                { "a link to itself", (s, b, p) => SetLink(b, b.id) },
            };
            foreach (var damage in damages)
                Assert.That(EpisodeValidation.TryValidate(Linked(damage.Value), out _), Is.False, damage.Key);
            var alone = Rules(Season(99));
            alone.deals.Add(new DealState
            {
                id = PricePrefix + "7", type = DealKind.SafetyAgreement, proposerId = alone.playerId, recipientId = Npcs(alone)[0].id,
                status = DealStatus.Active, week = alone.week, expiresWeek = alone.week, trustImpact = DealTrust.High,
            });
            Assert.That(EpisodeValidation.TryValidate(alone, out _), Is.False, "A price never stands alone.");
            Assert.That(EpisodeValidation.TryValidate(Linked(rules: false), out error), Is.False, "Without the rules there is no link at all.");
            Assert.That(error, Is.EqualTo("A season without the commitment rules has none of their records."));
        }

        // ------------------------------------------------------------ the names C7 added
        //
        // Every name C7 added is read through these, and only these, so the file compiles against the
        // build before it (1597bc0) with their bodies stubbed to that build's behaviour - which is how each
        // test above was seen to fail there.

        private static string CounterKind => Negotiation.CounterEventKind;
        private static double CounterFloor => Negotiation.CounterFloor;
        private static double CounterChance => Negotiation.CounterChance;
        private static string PricePrefix => Negotiation.PricePrefix;
        private static EpisodeCommandKind NegotiateKind => EpisodeCommandKind.Negotiate;
        private static string[] Approaches => Negotiation.Approaches;
        private static string Remind => Negotiation.Remind;
        private static string Demand => Negotiation.Demand;
        private static string Threaten => Negotiation.Threaten;
        private static string MendMove => Negotiation.MendFences;
        private static string VetoMove => Negotiation.VetoForAPrice;
        private static string AcceptAnswer => EpisodeEngine.AcceptDeal;
        private static string VoidedStatus => CommitmentsRead.VoidedWord;
        private static string CounterDealPrefix => Negotiation.CounterDealPrefix;
        private static string TooManyArrangements => Negotiation.TooManyArrangements;
        private static string AskPriceLineOf(EpisodeState s, DealState ask) => Negotiation.AskPriceLine(s, ask);

        private static string Key(EpisodeState s, string npcId, string kind, string about, int attempt) => Negotiation.CounterKey(s, npcId, kind, about, attempt);
        private static Negotiation.Counter Open(EpisodeState s, string npcId) => Negotiation.OpenCounter(s, npcId);
        private static Negotiation.Counter Terms(EpisodeState s, string npcId, string kind, string about) => Negotiation.CounterTo(s, npcId, kind, about);
        private static string CounterLine(EpisodeState s, Negotiation.Counter counter) => Negotiation.CounterLine(s, counter);
        private static bool IsPrice(DealState d) => Negotiation.IsPrice(d);
        private static string Link(DealState d) => d.linkedDealId;
        private static DealState SetLink(DealState d, string id) { d.linkedDealId = id; return d; }
        private static bool Voided(EpisodeState s, DealState price) => Negotiation.Voided(s, price);
        private static Negotiation.Price AskPriceOf(EpisodeState s, DealState ask) => Negotiation.AskPrice(s, ask);
        private static double MoveChance(EpisodeState s, string npcId, string move, bool asKnown) => Negotiation.Chance(s, npcId, move, asKnown);
        private static double BaseOf(string move) => Negotiation.Base(move);
        private static double CostOf(string approach) => Negotiation.Cost(approach);
        private static double HoldOf(string approach) => Negotiation.Hold(approach);
        private static double HeldTo(EpisodeState s, PromiseState p) => Negotiation.HeldTo(s, p);
        private static string CallInMove(string approach) => Negotiation.CallIn(approach);
        private static string VetoPriceOf(string kind) => Negotiation.VetoPriceMove(kind);
        private static List<string> VetoPricesOf(EpisodeState s, string nomineeId) => Negotiation.VetoPrices(s, nomineeId);
        private static string VetoPriceRefusalOf(EpisodeState s, string nomineeId, string kind) => Negotiation.VetoPriceRefusal(s, nomineeId, kind);
        private static string MendRefusalOf(EpisodeState s, string npcId) => Negotiation.MendRefusal(s, npcId);
        private static string CallInRefusalOf(EpisodeState s, string npcId, string promiseId, string approach) => Negotiation.CallInRefusal(s, npcId, promiseId, approach);
        private static List<PromiseState> OwedTo(EpisodeState s, string npcId) => Negotiation.Owed(s, npcId);

        // ------------------------------------------------------------ fixtures

        private static EpisodeState Season(uint seed, int size = 8)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);
            // No traits of the player's own, so no move takes a trait's bonus.
            s.Find(s.playerId).traits = new List<string>();
            return s;
        }

        /// <summary>The levers, where vote deals are judged, without the commitment rules.</summary>
        private static EpisodeState Levers(EpisodeState s)
        {
            EpisodeEngine.EnableLevers(s);
            return s;
        }

        /// <summary>The levers and the commitment rules.</summary>
        private static EpisodeState Rules(EpisodeState s)
        {
            EpisodeEngine.EnableLevers(s);
            EpisodeEngine.EnableCommitments(s);
            return s;
        }

        /// <summary>A campaign under the rules: the first houseguest Head of Household, the next two on the block, the fourth holding the veto, used.</summary>
        private static EpisodeState Campaign(uint seed)
        {
            var s = Rules(Season(seed));
            var npcs = Npcs(s).Select(c => c.id).ToList();
            s.phase = EpisodePhase.Campaign;
            s.hohId = npcs[0];
            s.nominees = new List<string> { npcs[1], npcs[2] };
            s.vetoHolderId = npcs[3];
            Lineup(s);
            s.vetoResolved = true;
            Valid(s);
            return s;
        }

        /// <summary>
        /// The veto meeting under the rules and the week's windows (so a word with a nominee is open): the
        /// first houseguest Head of Household, the next two on the block, the player - or the fourth -
        /// holding the veto, undecided.
        /// </summary>
        private static EpisodeState VetoMeeting(uint seed, bool playerHolds, int size = 8, bool rules = true)
        {
            var s = rules ? Rules(Season(seed, size)) : Levers(Season(seed, size));
            EpisodeEngine.EnableWeek(s);
            var npcs = Npcs(s).Select(c => c.id).ToList();
            s.phase = EpisodePhase.VetoMeeting;
            s.hohId = npcs[0];
            s.nominees = new List<string> { npcs[1], npcs[2] };
            s.vetoHolderId = playerHolds ? s.playerId : npcs[3];
            Lineup(s);
            Valid(s);
            return s;
        }

        private static void Lineup(EpisodeState s)
        {
            s.vetoPlayers = s.Active.Select(c => c.id).Take(EpisodeEngine.VetoPlayerCount(s.Active.Count())).ToList();
            if (!s.vetoPlayers.Contains(s.vetoHolderId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.vetoHolderId;
        }

        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);

        /// <summary>A nominee's veto ask put to the player, as NpcDeals.AskForTheVeto files it.</summary>
        private static DealState Ask(EpisodeState s, string nomineeId)
        {
            var ask = new DealState
            {
                id = NpcDeals.VetoAskPrefix + s.nextSequence++, type = DealKind.VetoUse, proposerId = nomineeId, recipientId = s.playerId,
                status = DealStatus.Proposed, week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(DealKind.VetoUse),
            };
            s.deals.Add(ask);
            Valid(s);
            return ask;
        }

        /// <summary>A promise a story had a houseguest make the player, as StoryPromise writes it.</summary>
        private static PromiseState StoryPromise(EpisodeState s, string fromId, PromiseKind kind)
        {
            var promise = new PromiseState
            {
                id = "promise-" + s.nextSequence++, fromId = fromId, toId = s.playerId, kind = kind, status = PromiseStatus.Active,
                week = s.week, expiresWeek = kind == PromiseKind.FinalTwo ? 0 : kind == PromiseKind.Safety ? s.week + 1 : s.week,
            };
            s.promises.Add(promise);
            return promise;
        }

        /// <summary>A safety pact the player broke with this houseguest, as the settlement records it under the rules (or before them, with no record).</summary>
        private static DealState BrokenByThePlayer(EpisodeState s, string npcId, bool rules = true)
        {
            var deal = new DealState
            {
                id = "deal-player-broken-" + npcId, type = DealKind.SafetyAgreement, proposerId = s.playerId, recipientId = npcId,
                status = DealStatus.Broken, week = s.week, expiresWeek = s.week, trustImpact = DealTrust.High,
                brokenById = rules ? s.playerId : null, settledWeek = rules ? s.week : 0,
            };
            s.deals.Add(deal);
            Valid(s);
            return deal;
        }

        /// <summary>
        /// A proposal the roll is about to refuse from a houseguest close to yes - the first, or the one
        /// <paramref name="which"/> names; each sees the other at 30, no traits - with the attempt's coin set
        /// to land or to miss (the season's sequence moved to where it does: the coin is keyed to it).
        /// </summary>
        private static EpisodeState NearMiss(EpisodeState s, string kind, string about, bool coinLands, out ContestantState npc, int which = 0)
        {
            npc = Plain(Npcs(s)[which]);
            Set(s, npc.id, s.playerId, 30); Set(s, s.playerId, npc.id, 30);
            double chance = PlayerDeals.AcceptanceChance(s, npc.id, kind, about);
            Assert.That(chance, Is.GreaterThanOrEqualTo(40), "Close to yes.");
            s.randomState = Draw(s, false, chance);
            Coin(s, npc.id, kind, about, coinLands);
            return s;
        }

        /// <summary>Moves the season's sequence to the next attempt whose counter coin lands, or misses.</summary>
        private static void Coin(EpisodeState s, string npcId, string kind, string about, bool lands)
        {
            for (int attempt = s.nextSequence; attempt < s.nextSequence + 5000; attempt++)
                if ((StoryRandom.Unit(s, Key(s, npcId, kind, about, attempt)) < CounterChance) == lands)
                {
                    s.nextSequence = attempt;
                    return;
                }
            Assert.Fail("No attempt's coin " + (lands ? "lands" : "misses") + ".");
        }

        /// <summary>A proposal to this houseguest - a partnership, unless another kind is named - refused.</summary>
        private static EpisodeState Refuse(EpisodeState s, string npcId, string kind = DealKind.Partnership, string about = null)
        {
            var result = Apply(new EpisodeEngine(s), EpisodeCommandKind.ProposeDeal, npcId, about, kind);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.deals.Count, Is.EqualTo(s.deals.Count), "The proposal was refused.");
            return result.state;
        }

        /// <summary>The counter that stands with this houseguest, taken: both its deals at once.</summary>
        private static EpisodeState Take(EpisodeState countered, string npcId)
        {
            var result = Apply(new EpisodeEngine(countered), EpisodeCommandKind.RespondToDeal, npcId, null, AcceptAnswer);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.deals.Count, Is.EqualTo(countered.deals.Count + 2), "Taken, a counter strikes both its deals at once.");
            return result.state;
        }

        /// <summary>
        /// The counter's standard pair, taken in free time: a target agreement the player asked a houseguest
        /// for, refused, and bought back with a safety pact the player owes them.
        /// </summary>
        private static EpisodeState CounterForATarget(out ContestantState npc, out string about)
        {
            var s = Rules(Season(107));
            var asked = Npcs(s)[0];
            about = Npcs(s).First(c => c.id != asked.id && !s.Allied(asked.id, c.id)).id;
            Set(s, asked.id, about, 0);
            var countered = Refuse(NearMiss(s, DealKind.TargetAgreement, about, coinLands: true, out npc), npc.id, DealKind.TargetAgreement, about);
            return Take(countered, npc.id);
        }

        /// <summary>A line said in the house to <paramref name="audience"/> - with nobody named, to the whole house - as the engine logs one.</summary>
        private static void Say(EpisodeState s, string kind, params string[] audience) =>
            s.events.Add(new EpisodeEvent
            {
                sequence = s.nextSequence++, week = s.week, phase = s.phase, kind = kind, text = "Something was said.",
                audienceIds = new List<string>(audience),
            });

        /// <summary>
        /// The season's deals made up to <paramref name="count"/> with lapsed bargains between the last two
        /// houseguests, whom no fixture here puts anywhere: the ceilings count every deal the season wrote.
        /// </summary>
        private static void Fill(EpisodeState s, int count)
        {
            var npcs = Npcs(s);
            string a = npcs[npcs.Count - 2].id, b = npcs[npcs.Count - 1].id;
            for (int n = s.deals.Count; n < count; n++)
                s.deals.Add(new DealState
                {
                    id = "deal-lapsed-" + n, type = DealKind.InformationSharing, proposerId = a, recipientId = b, status = DealStatus.Expired,
                    week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(DealKind.InformationSharing),
                });
            Valid(s);
        }

        /// <summary>The breach the player holds against this houseguest last, as the settlement records it under the rules.</summary>
        private static RelationshipEventState BrokenEntry(EpisodeState s, string breakerId) =>
            s.relationships.Single(r => r.fromId == s.playerId && r.toId == breakerId).events.Last(e => e.type == "deal_broken");

        /// <summary>The settlement itself (EpisodeEngine.SettleDeals, which only commands reach): one deal, how it ended, and whose doing that was.</summary>
        private static void Settle(EpisodeState s, string dealId, string status, string actorId)
        {
            var verdicts = new List<DealResolution.Verdict>
            {
                new DealResolution.Verdict { deal = s.deals.Single(d => d.id == dealId), status = status, actorId = actorId },
            };
            typeof(EpisodeEngine).GetMethod("SettleDeals", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { s, verdicts });
        }

        private static List<ContestantState> Npcs(EpisodeState s) => s.Active.Where(c => !c.isPlayer).ToList();

        /// <summary>No traits, so a houseguest's own lines on the roll stay out of the way.</summary>
        private static ContestantState Plain(ContestantState npc)
        {
            npc.traits = new List<string>();
            return npc;
        }

        private static void Set(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        /// <summary>A random state whose next draw says yes, or no, to a chance.</summary>
        private static uint Draw(EpisodeState s, bool yes, double chance)
        {
            for (uint n = 1; n < 10000; n++)
            {
                uint state = n * 2654435761u;
                if ((new SeededRandom(state).NextDouble() * 100 < chance) == yes) return state;
            }
            Assert.Fail("No draw says " + (yes ? "yes" : "no") + " to " + chance + ".");
            return 0;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            new EpisodeCommand
            {
                id = "negotiation-" + kind + "-" + s.revision + "-" + target + "-" + text, actorId = s.playerId, kind = kind,
                targetId = target, secondTargetId = second, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        private static CommandResult Apply(EpisodeEngine engine, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            engine.Apply(Command(engine.Snapshot, kind, target, second, text));
    }
}
