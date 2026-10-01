using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// What is waiting on the player (ACTIONS-DEALS-ALLIANCES-PLAN V2), read from seeded states:
    /// offers, a nominee's ask for the veto, a plea, nothing, and each again after it lapses through
    /// the engine's own rule. The reader is what the cast strip's badges, the objective's line and
    /// the strategy screens' footer say, so what it claims lapses on an advance is checked against
    /// the advance itself rather than against a second copy of the rule.
    /// </summary>
    public sealed class WaitingOnYouTests
    {
        // ---------------------------------------------------------------- nothing

        [Test]
        public void AQuietHouseHasNothingWaiting()
        {
            var s = Season(51);
            var reading = WaitingOnYou.Read(s);
            Assert.That(reading.items, Is.Empty);
            Assert.That(reading.Any, Is.False);
            Assert.That(WaitingOnYou.ObjectiveLine(s), Is.Null, "Nothing waits, so the objective keeps its own words.");
            Assert.That(WaitingOnYou.AdvanceNote(s), Is.Null, "and moving on lets nothing go.");
        }

        // ---------------------------------------------------------------- offers

        [Test]
        public void TheHousesOffersWaitOnThePlayerUntilTheCampaignOpens()
        {
            var s = Warm(Season(4, 10), 60);
            s.week = 2;
            NpcDeals.Propose(s);
            var offers = NpcDeals.Pending(s);
            Assert.That(offers, Is.Not.Empty, "A warm house puts something to the player.");

            var reading = WaitingOnYou.Read(s);
            Assert.That(reading.items.Select(item => item.id), Is.EqualTo(offers.Select(deal => deal.id)),
                "Every offer still on the table, in the deal table's own order.");
            foreach (var item in reading.items)
            {
                Assert.That(item.kind, Is.Not.EqualTo(WaitingOnYou.Kind.Card));
                Assert.That(reading.OfferFrom(item.fromId), Is.True);
                Assert.That(reading.AnswerFrom(item.fromId), Is.False);
                Assert.That(item.lapses, Is.EqualTo(WaitingOnYou.Lapse.CampaignOpens),
                    "This week's offers go at the house's next round, as next week's campaign opens.");
                Assert.That(item.lapsesOnAdvance, Is.False, "Ending free time does not write them off.");
            }
            Assert.That(WaitingOnYou.ObjectiveLine(s), Is.EqualTo(offers.Count == 1
                ? FirstName(s, offers[0].proposerId) + " has an offer for you" : offers.Count + " offers waiting"));
        }

        [Test]
        public void OneOfferIsNamedAndSeveralAreCounted()
        {
            var s = Season(53);
            var npcs = Npcs(s);
            s.deals.Add(Offer(npcs[0].id, s.playerId, DealKind.Partnership, "deal-ask-1", s.week));
            Assert.That(WaitingOnYou.ObjectiveLine(s), Is.EqualTo(FirstName(s, npcs[0].id) + " has an offer for you"));

            s.deals.Add(Offer(npcs[1].id, s.playerId, DealKind.SafetyAgreement, "deal-ask-2", s.week));
            Assert.That(WaitingOnYou.ObjectiveLine(s), Is.EqualTo("2 offers waiting"));

            s.replyCards.Add(Card(ReplyCards.Confrontation, npcs[2].id, null, "reply-3", s.week));
            Assert.That(WaitingOnYou.ObjectiveLine(s), Is.EqualTo("3 houseguests are waiting on you"),
                "An offer and a card are not both offers: the line counts the people.");

            var alone = Season(53);
            alone.replyCards.Add(Card(ReplyCards.Confrontation, npcs[2].id, null, "reply-3", alone.week));
            Assert.That(WaitingOnYou.ObjectiveLine(alone), Is.EqualTo(FirstName(alone, npcs[2].id) + " is waiting on your answer"));
        }

        /// <summary>
        /// Last week's offers are written off as the campaign opens. The reader says so on the veto
        /// meeting after the decision, under the week's windows - where anybody has time for a word
        /// there - and the advance that opens the campaign does it.
        /// </summary>
        [Test]
        public void LastWeeksOffersLapseAsTheCampaignOpens()
        {
            var s = AtMeeting(Season(55), holder: 3);
            s.vetoResolved = true;
            s.week = 2;
            EpisodeEngine.EnableWeek(s, s.week);
            // Cold toward the player, so the round the campaign opens with puts nothing new to them.
            foreach (var npc in Npcs(s)) Set(s, npc.id, s.playerId, 0);
            string from = Npcs(s).Select(c => c.id).First(id => id != s.hohId && id != s.vetoHolderId && !s.nominees.Contains(id));
            s.deals.Add(Offer(from, s.playerId, DealKind.Partnership, "deal-ask-1", 1));

            var offer = WaitingOnYou.Read(s).items.Single();
            Assert.That(offer.kind, Is.EqualTo(WaitingOnYou.Kind.Offer));
            Assert.That(offer.lapses, Is.EqualTo(WaitingOnYou.Lapse.CampaignOpens));
            Assert.That(offer.lapsesOnAdvance, Is.True, "Continuing to the campaign is what writes it off.");
            Assert.That(WaitingOnYou.AdvanceNote(s), Is.EqualTo(FirstName(s, from) + "'s offer expires when the campaign opens. "
                + EpisodeEngine.AfterNominationsSeats + " unused action will be lost."));

            var before = s.Clone();
            before.vetoResolved = false;
            Assert.That(WaitingOnYou.Read(before).items.Single().lapsesOnAdvance, Is.False,
                "Before the decision the advance is the meeting itself, which writes no offer off.");

            var campaign = Run(s, EpisodeCommandKind.Advance);
            Assert.That(campaign.accepted, Is.True, campaign.reason);
            Assert.That(campaign.state.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(campaign.state.deals.Single(d => d.id == "deal-ask-1").status, Is.EqualTo(DealStatus.Expired),
                "The engine wrote it off where the reader said it would.");
            Assert.That(WaitingOnYou.Read(campaign.state).OfferFrom(from), Is.False, "Lapsed, it waits no more.");
        }

        // ---------------------------------------------------------------- the veto

        [Test]
        public void ANomineesAskForTheVetoWaitsUntilThePlayerDecidesTheVeto()
        {
            var s = AtVetoWon(Season(22));
            string warm = s.nominees[0], cold = s.nominees[1];
            Set(s, warm, s.playerId, 30); Set(s, cold, s.playerId, 0);
            var meeting = Run(s, EpisodeCommandKind.Advance);
            Assert.That(meeting.accepted, Is.True, meeting.reason);

            var reading = WaitingOnYou.Read(meeting.state);
            var ask = reading.items.Single(item => item.kind == WaitingOnYou.Kind.VetoAsk);
            Assert.That(ask.fromId, Is.EqualTo(warm), "The warm nominee asked.");
            Assert.That(ask.lapses, Is.EqualTo(WaitingOnYou.Lapse.VetoDecision));
            Assert.That(ask.lapsesOnAdvance, Is.False, "The player holds the veto: their own decision settles it, not an advance.");
            Assert.That(reading.OfferFrom(warm), Is.True);
            Assert.That(reading.OfferFrom(cold), Is.False);
            Assert.That(WaitingOnYou.ObjectiveLine(meeting.state, reading), Is.EqualTo(FirstName(s, warm) + " has an offer for you"));

            var decided = Run(meeting.state, EpisodeCommandKind.ResolveVeto);
            Assert.That(decided.accepted, Is.True, decided.reason);
            Assert.That(WaitingOnYou.Read(decided.state).items.Where(item => item.kind == WaitingOnYou.Kind.VetoAsk), Is.Empty,
                "Once the veto is decided the question is off the table.");
        }

        [Test]
        public void AnAskForTheVetoAHouseguestDecidesLapsesOnTheMeetingsAdvance()
        {
            var s = AtMeeting(Season(59), holder: 3);
            string nominee = s.nominees[0];
            s.deals.Add(Offer(nominee, s.playerId, DealKind.VetoUse, "deal-veto-1", s.week));

            var ask = WaitingOnYou.Read(s).items.Single();
            Assert.That(ask.kind, Is.EqualTo(WaitingOnYou.Kind.VetoAsk));
            Assert.That(ask.lapsesOnAdvance, Is.True, "A houseguest holding the veto decides it at the advance.");
            Assert.That(WaitingOnYou.AdvanceNote(s), Is.EqualTo(FirstName(s, nominee) + "'s ask for the veto expires once the veto is decided."));

            var held = Run(s, EpisodeCommandKind.Advance);
            Assert.That(held.accepted, Is.True, held.reason);
            Assert.That(held.state.vetoResolved, Is.True);
            Assert.That(held.state.deals.Single(d => d.id == "deal-veto-1").status, Is.EqualTo(DealStatus.Expired));
            Assert.That(WaitingOnYou.Read(held.state).items, Is.Empty);
        }

        // ---------------------------------------------------------------- cards

        [Test]
        public void APleaWaitsUntilCampaigningCloses()
        {
            var s = AtCampaign(Season(56));
            string nominee = s.nominees[0];
            s.replyCards.Add(Card(ReplyCards.Plea, nominee, s.nominees[1], "reply-1", s.week));

            var reading = WaitingOnYou.Read(s);
            var plea = reading.items.Single();
            Assert.That(plea.kind, Is.EqualTo(WaitingOnYou.Kind.Card));
            Assert.That(plea.type, Is.EqualTo(ReplyCards.Plea));
            Assert.That(plea.lapses, Is.EqualTo(WaitingOnYou.Lapse.CampaignCloses));
            Assert.That(plea.lapsesOnAdvance, Is.True);
            Assert.That(reading.AnswerFrom(nominee), Is.True);
            Assert.That(reading.OfferFrom(nominee), Is.False, "A plea is not an offer.");
            Assert.That(WaitingOnYou.ObjectiveLine(s), Is.EqualTo(FirstName(s, nominee) + " is waiting on your answer"));
            Assert.That(WaitingOnYou.AdvanceNote(s), Is.EqualTo(FirstName(s, nominee) + "'s plea goes unanswered when campaigning closes."),
                "Under the old weekly pool the campaign's actions carry into free time, so only the plea goes.");

            EpisodeEngine.EnableWeek(s, s.week);
            Assert.That(WaitingOnYou.AdvanceNote(s), Is.EqualTo(FirstName(s, nominee) + "'s plea goes unanswered when campaigning closes. "
                + EpisodeEngine.AfterVetoSeats + " unused actions will be lost."), "Under the week's windows the campaign's seats go with it.");

            var closed = Run(s, EpisodeCommandKind.Advance);
            Assert.That(closed.accepted, Is.True, closed.reason);
            Assert.That(closed.state.phase, Is.EqualTo(EpisodePhase.Eviction));
            Assert.That(WaitingOnYou.Read(closed.state).AnswerFrom(nominee), Is.False, "Campaigning closed, the plea is gone.");
        }

        [Test]
        public void TwoPleasAreCountedAndAConfrontationIsNamedForWhatItIs()
        {
            var s = AtCampaign(Season(56));
            s.replyCards.Add(Card(ReplyCards.Plea, s.nominees[0], s.nominees[1], "reply-1", s.week));
            s.replyCards.Add(Card(ReplyCards.Plea, s.nominees[1], s.nominees[0], "reply-2", s.week));
            Assert.That(WaitingOnYou.AdvanceNote(s), Is.EqualTo("2 pleas go unanswered when campaigning closes."));

            var social = Season(57);
            string angry = Npcs(social)[0].id;
            social.replyCards.Add(Card(ReplyCards.Confrontation, angry, null, "reply-1", social.week));
            var card = WaitingOnYou.Read(social).items.Single();
            Assert.That(card.lapses, Is.EqualTo(WaitingOnYou.Lapse.FreeTimeEnds));
            Assert.That(WaitingOnYou.AdvanceNote(social), Is.EqualTo(FirstName(social, angry) + "'s confrontation goes unanswered when free time ends."));
        }

        // ---------------------------------------------------------------- the knowledge gate

        [Test]
        public void NothingBetweenTwoHouseguestsIsWaitingOnThePlayer()
        {
            var s = Season(58);
            var npcs = Npcs(s);
            // Put by one houseguest to another, struck between two, the player's own, and one answered.
            s.deals.Add(Offer(npcs[0].id, npcs[1].id, DealKind.Partnership, "deal-npc-1", s.week));
            var struck = Offer(npcs[2].id, npcs[3].id, DealKind.SafetyAgreement, "deal-npc-2", s.week);
            struck.status = DealStatus.Active;
            s.deals.Add(struck);
            var mine = Offer(s.playerId, npcs[4].id, DealKind.Partnership, "deal-player-3", s.week);
            mine.status = DealStatus.Active;
            s.deals.Add(mine);
            var answered = Offer(npcs[5].id, s.playerId, DealKind.Partnership, "deal-ask-4", s.week);
            answered.status = DealStatus.Declined;
            s.deals.Add(answered);

            var reading = WaitingOnYou.Read(s);
            Assert.That(reading.items, Is.Empty, "Nothing of this is the player's to answer: " + string.Join("; ", reading.items));
            foreach (var npc in npcs) Assert.That(reading.OfferFrom(npc.id), Is.False, npc.name + " has nothing waiting on the player.");
            Assert.That(WaitingOnYou.ObjectiveLine(s), Is.Null);
        }

        [Test]
        public void NothingWaitsFromSomebodyWhoHasLeftNorForAPlayerWhoHas()
        {
            var s = Season(60);
            var npcs = Npcs(s);
            string gone = npcs[0].id, here = npcs[1].id;
            s.deals.Add(Offer(gone, s.playerId, DealKind.Partnership, "deal-ask-1", s.week));
            s.replyCards.Add(Card(ReplyCards.Confrontation, here, null, "reply-2", s.week));
            s.Find(gone).status = ContestantStatus.Evicted;

            var reading = WaitingOnYou.Read(s);
            Assert.That(reading.items.Select(item => item.fromId), Is.EqualTo(new[] { here }),
                "The engine refuses an answer to somebody no longer in the house.");

            s.Find(s.playerId).status = ContestantStatus.Evicted;
            Assert.That(WaitingOnYou.Read(s).Any, Is.False, "An evicted player follows the season and answers nothing.");
            Assert.That(WaitingOnYou.ObjectiveLine(s), Is.Null);
        }

        // ---------------------------------------------------------------- story beats

        /// <summary>
        /// A story beat, and the Diary Room's call, are the episode screen's and the diary's to put
        /// in front of the player, and no answer owed to anybody: they make no line, so move-in
        /// night's objective keeps the words the tutorial points at.
        /// </summary>
        [Test]
        public void AStoryBeatOrTheDiaryRoomsCallMakesNoLine()
        {
            var s = Season(61);
            s.houseEvents.Add(Beat("beat-1", s.week, StorySurfaces.Approach));
            s.houseEvents.Add(Beat("summons-2", s.week, StorySurfaces.Summons));
            Assert.That(EpisodeEngine.OpenStoryBeats(s), Has.Count.EqualTo(2), "The fixture has a beat and a summons open.");
            Assert.That(WaitingOnYou.Read(s).Any, Is.False, "Neither is somebody waiting on an answer.");
            Assert.That(WaitingOnYou.ObjectiveLine(s), Is.Null);

            string from = Npcs(s)[0].id;
            s.deals.Add(Offer(from, s.playerId, DealKind.Partnership, "deal-ask-1", s.week));
            Assert.That(WaitingOnYou.ObjectiveLine(s), Is.EqualTo(FirstName(s, from) + " has an offer for you"),
                "An offer still says itself beside them.");
        }

        // ---------------------------------------------------------------- past answering

        /// <summary>
        /// An offer the house files as the Final 3's window opens waits on the player through that
        /// window, and is no longer waiting once the final Head of Household begins: nothing after it
        /// gives a word with anybody, and nothing writes the offer off, so the engine would leave it
        /// on the table for ever. Nor is anything waiting in any phase after that.
        /// </summary>
        [Test]
        public void AnOfferFiledAsTheFinalThreesWindowOpensIsNoLongerWaitingAtTheFinalHeadOfHousehold()
        {
            var s = Warm(FinalThreeWindow(), 60);
            NpcDeals.Propose(s);
            var filed = NpcDeals.Pending(s).Select(deal => deal.id).ToList();
            Assert.That(filed, Is.Not.Empty, "A warm Final 3 puts something to the player as its window opens.");
            Assert.That(WaitingOnYou.Read(s).items.Select(item => item.id), Is.EquivalentTo(filed),
                "Through the window, every one of them waits on the player.");

            var part1 = Run(s, EpisodeCommandKind.Advance);
            Assert.That(part1.accepted, Is.True, part1.reason);
            Assert.That(part1.state.phase, Is.EqualTo(EpisodePhase.FinalHoHPart1));
            Assert.That(NpcDeals.Pending(part1.state).Select(deal => deal.id), Is.EquivalentTo(filed),
                "The engine writes none of them off: no round of the house's runs again.");
            Assert.That(WaitingOnYou.Read(part1.state).Any, Is.False, "But none can be answered any more, so none is waiting.");
            Assert.That(WaitingOnYou.ObjectiveLine(part1.state), Is.Null);

            foreach (var phase in new[] { EpisodePhase.FinalHoHPart2, EpisodePhase.FinalHoHPart3, EpisodePhase.FinalEviction,
                         EpisodePhase.JuryQuestioning, EpisodePhase.FinalSpeeches, EpisodePhase.Jury, EpisodePhase.Finished })
            {
                var later = part1.state.Clone();
                later.phase = phase;
                later.replyCards.Add(Card(ReplyCards.Confrontation, Npcs(later).First().id, null, "reply-late", later.week));
                Assert.That(WaitingOnYou.Read(later).Any, Is.False, "Nothing, offer or card, is waiting at " + phase + ".");
            }
        }

        /// <summary>
        /// Without the week's windows, only the people deciding have time for a word between the
        /// Head of Household and the campaign. At the veto meeting that is the last chance before the
        /// campaign's opening writes last week's offers off: the holder's can still be answered while
        /// the veto is undecided, nobody else's can, and after the decision none is. Under the week's
        /// windows everybody has time for a word, so every one of them is still waiting.
        /// </summary>
        [Test]
        public void WithoutTheWeeksWindowsLastWeeksOfferIsPastAnsweringAtTheVetoMeeting()
        {
            var s = AtMeeting(Season(66), holder: 3);
            s.week = 2;
            string holder = s.vetoHolderId;
            string bystander = Npcs(s).Select(c => c.id).First(id => id != s.hohId && id != holder && !s.nominees.Contains(id));
            s.deals.Add(Offer(bystander, s.playerId, DealKind.Partnership, "deal-ask-1", 1));
            s.deals.Add(Offer(holder, s.playerId, DealKind.SafetyAgreement, "deal-ask-2", 1));
            Assert.That(EpisodeEngine.WeekRulesOn(s), Is.False, "The fixture's season has no week windows.");

            var undecided = WaitingOnYou.Read(s);
            Assert.That(undecided.OfferFrom(holder), Is.True, "The holder has time for a word before deciding.");
            Assert.That(undecided.OfferFrom(bystander), Is.False, "Nobody else does, and the campaign writes the offer off first.");

            s.vetoResolved = true;
            Assert.That(WaitingOnYou.Read(s).Any, Is.False, "After the decision nobody has time for a word before the campaign.");

            EpisodeEngine.EnableWeek(s, s.week);
            var windows = WaitingOnYou.Read(s);
            Assert.That(windows.OfferFrom(bystander) && windows.OfferFrom(holder), Is.True,
                "Under the week's windows everybody has time for a word at the meeting.");
        }

        // ---------------------------------------------------------------- the lapse rule

        /// <summary>
        /// The rule the footer's words rest on, pinned on the engine itself: an offer filed in a
        /// week's free time outlives the week's turn and the next week's nominations and veto, and is
        /// written off as that week's campaign opens - by the advance out of the veto meeting, which
        /// the reader says beforehand.
        /// </summary>
        [Test]
        public void AnOfferOutlivesTheWeeksTurnAndTheNextNominationsAndLapsesAsTheNextCampaignOpens()
        {
            var start = Season(64);
            EpisodeEngine.EnableWeek(start, start.week);
            var engine = new EpisodeEngine(start);
            for (int guard = 0; guard < 200; guard++)
            {
                var now = engine.Snapshot;
                if (now.phase == EpisodePhase.Social && now.evictionResolved) break;
                Assert.That(engine.Apply(Next(now)).accepted, Is.True, "Week one plays to its free time.");
            }
            var free = engine.Snapshot;
            Assert.That(free.phase == EpisodePhase.Social && free.evictionResolved, Is.True, "The week reached its free time after the eviction.");
            Assert.That(free.Find(free.playerId).status, Is.EqualTo(ContestantStatus.Active), "The player is still in the house.");
            int week = free.week;
            string from = Npcs(free).First().id;
            free.deals.Add(Offer(from, free.playerId, DealKind.Partnership, "deal-ask-pin", week));
            engine = new EpisodeEngine(free);
            Assert.That(WaitingOnYou.Read(free).OfferFrom(from), Is.True, "It waits on the player in its own week's free time.");

            bool turned = false, nominated = false, warned = false;
            for (int guard = 0; guard < 200; guard++)
            {
                var before = engine.Snapshot;
                if (before.phase == EpisodePhase.VetoMeeting && before.vetoResolved && before.week == week + 1)
                {
                    var waiting = WaitingOnYou.Read(before).items.SingleOrDefault(item => item.id == "deal-ask-pin");
                    Assert.That(waiting, Is.Not.Null, "Still waiting at the end of the veto meeting.");
                    Assert.That(waiting.lapsesOnAdvance, Is.True, "and the reader says the next advance lets it go.");
                    warned = true;
                }
                var result = engine.Apply(Next(before));
                Assert.That(result.accepted, Is.True, result.reason);
                var after = engine.Snapshot;
                var offer = after.deals.Single(deal => deal.id == "deal-ask-pin");
                if (after.week == week + 1 && after.phase == EpisodePhase.Campaign)
                {
                    Assert.That(before.phase, Is.EqualTo(EpisodePhase.VetoMeeting), "The campaign opens out of the veto meeting.");
                    Assert.That(offer.status, Is.EqualTo(DealStatus.Expired), "It is written off as the next week's campaign opens.");
                    Assert.That(turned && nominated && warned, Is.True, "It outlived the week's turn and the nominations first, and the reader said it was going.");
                    return;
                }
                Assert.That(offer.status, Is.EqualTo(DealStatus.Proposed), "Still on the table at " + after.phase + " in week " + after.week + ".");
                if (after.week == week + 1) turned = true;
                if (after.week == week + 1 && after.phase == EpisodePhase.VetoSelection) nominated = true;
            }
            Assert.Fail("The next week's campaign never opened.");
        }

        // ---------------------------------------------------------------- unused actions

        [Test]
        public void AWindowsSeatsAreLostOnlyByTheAdvanceThatClosesIt()
        {
            var s = Season(62);
            EpisodeEngine.EnableWeek(s, s.week);
            var npcs = Npcs(s);
            s.phase = EpisodePhase.Nomination;
            s.hohId = npcs[0].id;
            Assert.That(WaitingOnYou.AdvanceClosesWindow(s), Is.False, "The Head of Household naming the nominees stays in the window.");
            Assert.That(WaitingOnYou.ActionsLostOnAdvance(s), Is.Zero);

            s.nominees = new List<string> { npcs[1].id, npcs[2].id };
            Assert.That(WaitingOnYou.AdvanceClosesWindow(s), Is.True, "On to the veto's draw: the next window.");
            Assert.That(WaitingOnYou.ActionsLostOnAdvance(s), Is.EqualTo(EpisodeEngine.AfterHoHSeats));
            Assert.That(WaitingOnYou.AdvanceNote(s), Is.EqualTo(EpisodeEngine.AfterHoHSeats + " unused actions will be lost."));
            s.windowActions[Windows.AfterHoH] = 1;
            Assert.That(WaitingOnYou.ActionsLostOnAdvance(s), Is.EqualTo(EpisodeEngine.AfterHoHSeats - 1));
            Assert.That(WaitingOnYou.AdvanceNote(s), Is.EqualTo("1 unused action will be lost."));

            s.phase = EpisodePhase.VetoSelection;
            Assert.That(WaitingOnYou.AdvanceClosesWindow(s), Is.False, "The draw, the veto and its meeting are one window.");
            s.phase = EpisodePhase.Veto;
            Assert.That(WaitingOnYou.AdvanceClosesWindow(s), Is.False);
            s.phase = EpisodePhase.VetoMeeting;
            s.vetoHolderId = npcs[3].id;
            Assert.That(WaitingOnYou.AdvanceClosesWindow(s), Is.False, "A houseguest deciding the veto stays in the window.");
            s.vetoResolved = true;
            Assert.That(WaitingOnYou.ActionsLostOnAdvance(s), Is.EqualTo(EpisodeEngine.AfterNominationsSeats), "On to the campaign.");

            s.phase = EpisodePhase.Campaign;
            Assert.That(WaitingOnYou.ActionsLostOnAdvance(s), Is.EqualTo(EpisodeEngine.AfterVetoSeats));
            // Time bought is the week's: what the campaign leaves of it carries into free time.
            s.boughtActionPoints = 1;
            Assert.That(EpisodeEngine.SocialActionBudget(s), Is.EqualTo(EpisodeEngine.AfterVetoSeats + 1));
            Assert.That(WaitingOnYou.ActionsLostOnAdvance(s), Is.EqualTo(EpisodeEngine.AfterVetoSeats));
            s.phase = EpisodePhase.Social;
            Assert.That(WaitingOnYou.ActionsLostOnAdvance(s), Is.EqualTo(EpisodeEngine.SocialActionBudget(s)),
                "The last window of the week takes everything with it.");

            var pool = s.Clone();
            pool.phase = EpisodePhase.Campaign;
            pool.weekRulesStartWeek = 0;
            Assert.That(WaitingOnYou.AdvanceClosesWindow(pool), Is.False, "The old weekly pool carries to the week's end.");
            Assert.That(WaitingOnYou.ActionsLostOnAdvance(pool), Is.Zero);
        }

        // ---------------------------------------------------------------- reading writes nothing

        [Test]
        public void ReadingTheHouseChangesNothingInIt()
        {
            var s = AtCampaign(Season(63));
            EpisodeEngine.EnableWeek(s, s.week);
            var npcs = Npcs(s);
            s.deals.Add(Offer(npcs[3].id, s.playerId, DealKind.Partnership, "deal-ask-1", s.week));
            s.replyCards.Add(Card(ReplyCards.Plea, s.nominees[0], s.nominees[1], "reply-2", s.week));
            s.houseEvents.Add(Beat("beat-3", s.week));
            string before = Newtonsoft.Json.JsonConvert.SerializeObject(s);

            var reading = WaitingOnYou.Read(s);
            Assert.That(reading.items, Has.Count.EqualTo(2));
            Assert.That(WaitingOnYou.ObjectiveLine(s), Is.Not.Null);
            Assert.That(WaitingOnYou.AdvanceNote(s), Is.Not.Null);
            WaitingOnYou.ActionsLostOnAdvance(s);
            WaitingOnYou.AdvanceClosesWindow(s);

            Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(s), Is.EqualTo(before),
                "Reading what waits writes, rolls and logs nothing, so no later event id moves.");
        }

        // ---------------------------------------------------------------- fixtures

        private static EpisodeState Season(uint seed, int house = 8) =>
            SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = house }, seed);

        private static List<ContestantState> Npcs(EpisodeState s) => s.Active.Where(c => !c.isPlayer).ToList();

        private static string FirstName(EpisodeState s, string id) => FinalistRead.FirstName(s.Find(id).name);

        private static EpisodeState Warm(EpisodeState s, double score)
        {
            foreach (var edge in s.relationships) edge.score = score;
            return s;
        }

        private static void Set(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        /// <summary>An offer in the shape <see cref="NpcDeals.Propose"/> files one: proposed, and lapsing with its own week.</summary>
        private static DealState Offer(string from, string to, string kind, string id, int week) => new DealState
        {
            id = id, type = kind, proposerId = from, recipientId = to, status = DealStatus.Proposed,
            week = week, expiresWeek = week, trustImpact = DealKind.DefaultTrust(kind),
        };

        private static ReplyCardState Card(string kind, string from, string about, string id, int week) =>
            new ReplyCardState { id = id, week = week, kind = kind, fromId = from, aboutId = about };

        private static HouseEventState Beat(string id, int week, string surface = null) => new HouseEventState
        {
            id = id, kind = HouseEventKind.Story, title = "A Word", week = week, surface = surface ?? StorySurfaces.Approach,
            narrative = "Somebody wants a word with you.", closesAnchor = StoryAnchors.SocialClose,
        };

        /// <summary>
        /// The Final 3's window, as the final four's eviction leaves it: three in the house, free
        /// time open on the eviction's week, the rest on the jury.
        /// </summary>
        private static EpisodeState FinalThreeWindow()
        {
            var s = ContentCatalog.Create(337);
            s.strategyRulesStartWeek = 1;
            if (s.npcSocial == null) s.npcSocial = NpcSocialState.Create(337);
            s.week = 4;
            foreach (var actor in s.contestants.Skip(3)) actor.status = ContestantStatus.Jury;
            s.phase = EpisodePhase.Social;
            s.evictionResolved = true;
            return s;
        }

        /// <summary>The engine's next command as a walk plays it, the player giving everything in a competition so they stay in the house.</summary>
        private static EpisodeCommand Next(EpisodeState s)
        {
            var command = EpisodeEngineTests.NextCommand(s);
            command.id = Guid.NewGuid().ToString("N");
            if (command.kind == EpisodeCommandKind.Compete) command.performance = 1.0;
            return command;
        }

        /// <summary>The veto meeting: the first houseguest at the head of the house, the next two nominated, the holder the fourth, a nominee (0, 1) or the player (-1).</summary>
        private static EpisodeState AtMeeting(EpisodeState s, int holder = 3)
        {
            var npcs = Npcs(s).Select(c => c.id).ToList();
            s.phase = EpisodePhase.VetoMeeting;
            s.hohId = npcs[0];
            s.nominees = new List<string> { npcs[1], npcs[2] };
            s.vetoHolderId = holder == -1 ? s.playerId : holder < 2 ? s.nominees[holder] : npcs[holder];
            int seats = EpisodeEngine.VetoPlayerCount(s.Active.Count());
            s.vetoPlayers = new[] { s.hohId, s.nominees[0], s.nominees[1], s.vetoHolderId }
                .Concat(s.Active.Select(c => c.id)).Distinct().Take(seats).ToList();
            s.vetoResolved = false;
            return s;
        }

        /// <summary>The veto competition just won by the player, with the meeting still to come.</summary>
        private static EpisodeState AtVetoWon(EpisodeState s)
        {
            AtMeeting(s, holder: -1);
            s.phase = EpisodePhase.Veto;
            s.competitionResolved = true;
            s.competitionScores = s.vetoPlayers.Select((id, i) => new CompetitionScore { contestantId = id, score = id == s.playerId ? 100 : i }).ToList();
            return s;
        }

        /// <summary>Campaigning, a houseguest having held the veto and kept the block.</summary>
        private static EpisodeState AtCampaign(EpisodeState s)
        {
            AtMeeting(s, holder: 3);
            s.phase = EpisodePhase.Campaign;
            s.vetoResolved = true;
            return s;
        }

        private static CommandResult Run(EpisodeState s, EpisodeCommandKind kind, string target = null) =>
            new EpisodeEngine(s).Apply(new EpisodeCommand
            {
                id = Guid.NewGuid().ToString("N"), actorId = s.playerId, expectedPhase = s.phase, expectedRevision = s.revision,
                kind = kind, targetId = target,
            });
    }
}
