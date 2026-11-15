using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The sources slice 4 adds to the asks that come to the player (OFFERS-COME-TO-YOU-PLAN §C.4, "Slice 4's sources",
    /// and §H's test list): each pull request appends its own section. Pure simulation, so the Unity-free subset runs it
    /// as well as the editor.
    /// </summary>
    public sealed class ComesToYouSourcesTests
    {
        private const string Maya = ContentCatalog.MayaId, Jamie = "jamie-roberts", Taylor = "taylor-kim",
            Casey = "casey-wilson", Riley = "riley-johnson";

        // ================================================================== OF-a: a reply card comes to you (Source.Card)

        /// <summary>
        /// S1. A confrontation, gossip or a plea waiting first reads as one <see cref="ComesToYou.Source.Card"/> row: the
        /// card's id, its sender as the asker, its kind, its week, the kind's opening line, the phase's lapse line, never
        /// whispered. Gossip's asker is the gossiper, whom all three of its answers address.
        /// </summary>
        [TestCase(ReplyCards.Confrontation)] [TestCase(ReplyCards.Gossip)] [TestCase(ReplyCards.Plea)]
        public void ACardOfEachLegacyKindReadsAsACard(string kind)
        {
            var s = State(kind == ReplyCards.Plea ? EpisodePhase.Campaign : EpisodePhase.Social);
            var card = Card(s, kind, Jamie, kind == ReplyCards.Gossip ? Taylor : kind == ReplyCards.Plea ? Casey : null);
            var ask = ComesToYou.Read(s).Single();
            Assert.That((ask.source, ask.recordId, ask.askerId, ask.type, ask.week),
                Is.EqualTo((ComesToYou.Source.Card, card.id, Jamie, kind, s.week)));
            string line = kind == ReplyCards.Confrontation ? "Can we talk? It's about where we stand."
                : kind == ReplyCards.Gossip ? "Got a minute? I think you've heard what I've been saying."
                : "Can we talk? It's about the vote.";
            Assert.That(ask.line, Is.EqualTo(line));
            Assert.That(ask.line, Does.Not.Contain("\"").And.Not.Contain("“"), "The speech card adds its own quote marks.");
            Assert.That(ComesToYou.CardLine(kind), Is.EqualTo(line));
            var item = WaitingOnYou.Read(s).items.Single(row => row.id == card.id);
            Assert.That((ask.lapses, ask.lapsesOnAdvance), Is.EqualTo((item.lapses, item.lapsesOnAdvance)), "The waiting list's own lapse.");
            Assert.That(ask.lapseLine, Is.EqualTo(kind == ReplyCards.Plea ? "It stands until campaigning closes." : "It stands until free time ends."));
            Assert.That(ask.whisper, Is.False);
            Assert.That(ask.IsCurrent(s), Is.True);
            Assert.That(ComesToYou.Find(s, card.id)?.source, Is.EqualTo(ComesToYou.Source.Card));
        }

        /// <summary>
        /// S2. A pitch is never a Card: a valid pitch waiting first (the player Head of Household in the nomination) lists
        /// no Card row - in OF-a no row at all; from OF-p on it is its own source's one row, a Pitch (S10) - and a
        /// confrontation filed behind it is not the card the player can answer, so it is not listed either.
        /// </summary>
        [Test]
        public void APitchIsNeverACard()
        {
            var s = PitchHouse();
            string pitcher = Npc(s, 0);
            var pitch = new ReplyCardState { id = "reply-pitch-" + s.week + "-" + pitcher, week = s.week, kind = ReplyCards.Pitch, fromId = pitcher };
            s.replyCards.Add(pitch);
            Assert.That(HoHPitches.ValidCard(s, pitch), Is.True, "The premise: a pitch the player can hear.");
            Assert.That(WaitingOnYou.Read(s).items.Single(item => item.id == pitch.id).kind, Is.EqualTo(WaitingOnYou.Kind.Card),
                "and the waiting list holds it as a card.");
            // OF-p (offers slice 4p) repins this line: OF-a read no row here; the pitch is now its own source's row.
            Assert.That(ComesToYou.Read(s).Select(ask => (ask.source, ask.recordId)), Is.EqualTo(new[] { (ComesToYou.Source.Pitch, pitch.id) }),
                "No Card row: a pitch's walk-up is its own source.");

            s.replyCards.Add(new ReplyCardState { id = "reply-90", week = s.week, kind = ReplyCards.Confrontation, fromId = Npc(s, 1) });
            Assert.That(ComesToYou.Read(s).Where(ask => ask.source == ComesToYou.Source.Card), Is.Empty,
                "The confrontation behind the pitch is not the card waiting first.");
        }

        /// <summary>
        /// S3. Only the card waiting first is listed, the one every surface lets the player answer: two cards from two
        /// senders list one row. Once the engine takes the answer to the first, the second is listed.
        /// </summary>
        [Test]
        public void OnlyTheCardWaitingFirstIsListed()
        {
            var s = AnsweringHouse(out string first, out string second);
            var asks = ComesToYou.Read(s);
            Assert.That(asks.Select(ask => (ask.source, ask.askerId)), Is.EqualTo(new[] { (ComesToYou.Source.Card, first) }));

            var answered = Answer(s, s.replyCards[0].id, "apologize");
            Assert.That(answered.replyCards.Select(card => card.fromId), Is.EqualTo(new[] { second }), "The premise: the engine took the answer.");
            Assert.That(ComesToYou.Read(answered).Select(ask => (ask.source, ask.askerId)), Is.EqualTo(new[] { (ComesToYou.Source.Card, second) }));
        }

        /// <summary>
        /// S4. A card is current as read, and stops being current once it is answered or cleared, once its sender leaves,
        /// once its subject changes, once another card stands ahead of it, and in another phase, week or session.
        /// </summary>
        [TestCase("answered")] [TestCase("cleared")] [TestCase("sender")] [TestCase("about")] [TestCase("behind")]
        [TestCase("phase")] [TestCase("week")] [TestCase("session")] [TestCase("player")]
        public void ACardStopsBeingCurrentOnceAnsweredOrLapsed(string change)
        {
            var s = AnsweringHouse(out string first, out string second);
            var ask = ComesToYou.Read(s).Single();
            Assert.That(ask.IsCurrent(s), Is.True);
            s.revision++;
            Assert.That(ask.IsCurrent(s), Is.True, "A clock revision does not end it.");
            var after = s;
            switch (change)
            {
                case "answered": after = Answer(s, ask.recordId, "deflect"); break;
                case "cleared": s.replyCards.Clear(); break;
                case "sender": s.Find(first).status = ContestantStatus.Evicted; break;
                case "about": s.replyCards[0].aboutId = second; break;
                case "behind":
                    s.replyCards.Insert(0, new ReplyCardState { id = "reply-80", week = s.week, kind = ReplyCards.Confrontation, fromId = second });
                    s.replyCards.RemoveAll(card => card.id != "reply-80" && card.fromId == second);
                    break;
                case "phase": s.phase = EpisodePhase.Campaign; break;
                case "week": s.week++; break;
                case "session": s.sessionId += "-loaded"; break;
                default: s.Find(s.playerId).status = ContestantStatus.Evicted; break;
            }
            Assert.That(ask.IsCurrent(after), Is.False, change);
            Assert.That(new ComesToYou.Ask { source = ComesToYou.Source.Card, recordId = ask.recordId, askerId = first }.IsCurrent(s), Is.False,
                "An ask the reader did not build stands for nothing.");
        }

        /// <summary>
        /// S5. With no card waiting the queue is exactly today's: on ComesToYouTests' offer and role-board houses, Read is
        /// the §C.1 oracle - the waiting list's non-card items that an invitation can raise, in the public sort - row for
        /// row and field for field. Adding a card leaves every other row identical and in the same order.
        /// </summary>
        [Test]
        public void WithNoCardTheQueueIsTodays()
        {
            var offers = State(EpisodePhase.VetoMeeting);
            offers.nominees = new List<string> { Taylor, Casey };
            offers.hohId = Maya; offers.vetoHolderId = Maya;
            Offer(offers, Maya, DealKind.Partnership, "deal-ask-1");
            Offer(offers, Taylor, DealKind.VetoUse, "deal-veto-2");
            Offer(offers, Jamie, DealKind.VoteSave, "deal-ask-3");
            Offer(offers, Riley, DealKind.Partnership, "deal-ask-0", 2);

            var board = RoleToneTests.Meeting(out _, out _).Snapshot;
            string other = board.Active.First(c => !c.isPlayer && !board.nominees.Contains(c.id) && c.id != board.hohId).id;
            Offer(board, other, DealKind.Partnership, NpcDeals.OfferPrefix + "901");

            foreach (var (name, s) in new[] { ("offers", offers), ("role board", board) })
            {
                Assert.That(s.replyCards, Is.Empty, name + ": the premise, no card.");
                var today = Rows(ComesToYou.Read(s));
                Assert.That(today, Is.Not.Empty, name + ": the house has asks.");
                Assert.That(today.Select(row => row.Item1), Has.None.EqualTo(ComesToYou.Source.Card));
                Assert.That(today.Select(row => row.Item2), Is.EqualTo(Oracle(s)), name + ": the §C.1 oracle, in its order.");

                string sender = s.Active.First(c => !c.isPlayer).id;
                s.replyCards.Add(new ReplyCardState { id = "reply-70", week = s.week, kind = ReplyCards.Confrontation, fromId = sender });
                var withCard = Rows(ComesToYou.Read(s));
                Assert.That(withCard.Count(row => row.Item1 == ComesToYou.Source.Card), Is.EqualTo(1), name + ": the card is one row.");
                Assert.That(withCard.Where(row => row.Item1 != ComesToYou.Source.Card), Is.EqualTo(today),
                    name + ": every other row the same, in the same order.");
            }
        }

        /// <summary>
        /// S6. A card reads even where no invitation exists: with no offer at all a confrontation is exactly one row.
        /// (Before slice 4a the reader returned empty once there was no invitation.)
        /// </summary>
        [Test]
        public void ACardReadsWithNoInvitation()
        {
            var s = State(EpisodePhase.Social);
            Assert.That(NpcInvitation.Available(s), Is.Empty, "The premise: nothing an invitation can raise.");
            var card = Card(s, ReplyCards.Confrontation, Taylor);
            Assert.That(ComesToYou.Read(s).Select(ask => (ask.source, ask.recordId)), Is.EqualTo(new[] { (ComesToYou.Source.Card, card.id) }));

            // Nor does a phase no invitation plays in hide it: the card's own phase rules are the waiting list's.
            var hoh = State(EpisodePhase.HoH);
            Assert.That(NpcInvitation.PhaseAllows(hoh.phase), Is.False);
            Card(hoh, ReplyCards.Gossip, Taylor, Jamie);
            Assert.That(ComesToYou.Read(hoh).Single().source, Is.EqualTo(ComesToYou.Source.Card));
        }

        /// <summary>
        /// S7. A card takes its place in the public order with no key of its own: urgency 0, no block place. A card the
        /// next advance clears comes before this week's offers; an older card comes after a newer offer; a vote to keep
        /// somebody (urgency 150) comes before a card of the same week; otherwise the askers' order.
        /// </summary>
        [Test]
        public void ACardTakesItsPlaceInThePublicOrder()
        {
            var campaign = State(EpisodePhase.Campaign);
            Offer(campaign, Casey, DealKind.Partnership, "deal-ask-1");
            Card(campaign, ReplyCards.Plea, Taylor, Jamie);
            Assert.That(ComesToYou.Read(campaign).Select(ask => (ask.lapsesOnAdvance, ask.source)),
                Is.EqualTo(new[] { (true, ComesToYou.Source.Card), (false, ComesToYou.Source.Offer) }),
                "The card campaigning's close clears comes first, its asker's id notwithstanding.");

            var older = State(EpisodePhase.Nomination);
            Offer(older, Taylor, DealKind.Partnership, "deal-ask-1");
            Card(older, ReplyCards.Confrontation, Casey).week = older.week - 1;
            Assert.That(ComesToYou.Read(older).Select(ask => ask.source), Is.EqualTo(new[] { ComesToYou.Source.Offer, ComesToYou.Source.Card }),
                "Last week's card after this week's offer.");

            var louder = State(EpisodePhase.Nomination);
            Offer(louder, Taylor, DealKind.VoteSave, "deal-ask-1");
            Card(louder, ReplyCards.Confrontation, Casey);
            Assert.That(ComesToYou.Read(louder).Select(ask => ask.source), Is.EqualTo(new[] { ComesToYou.Source.Offer, ComesToYou.Source.Card }),
                "The vote to keep Taylor before the card.");

            var byAsker = State(EpisodePhase.Nomination);
            Offer(byAsker, Taylor, DealKind.Partnership, "deal-ask-1");
            Card(byAsker, ReplyCards.Confrontation, Jamie);
            Assert.That(ComesToYou.Read(byAsker).Select(ask => ask.askerId), Is.EqualTo(new[] { Jamie, Taylor }), "Jamie's card, then Taylor's offer.");
            var turned = State(EpisodePhase.Nomination);
            Offer(turned, Jamie, DealKind.Partnership, "deal-ask-1");
            Card(turned, ReplyCards.Confrontation, Taylor);
            Assert.That(ComesToYou.Read(turned).Select(ask => (ask.askerId, ask.source)),
                Is.EqualTo(new[] { (Jamie, ComesToYou.Source.Offer), (Taylor, ComesToYou.Source.Card) }), "Turned round, the order turns.");
        }

        /// <summary>S8. Reading a card writes nothing: the state's JSON and both random streams are the same after Read, Find and IsCurrent.</summary>
        [Test]
        public void ReadingACardWritesNothing()
        {
            var s = State(EpisodePhase.Campaign);
            Offer(s, Maya, DealKind.Partnership, "deal-ask-1");
            var card = Card(s, ReplyCards.Plea, Taylor, Casey);
            string before = JsonConvert.SerializeObject(s);
            uint game = s.randomState, npcs = s.npcSocial.randomState;
            var asks = ComesToYou.Read(s);
            Assert.That(asks.Count, Is.EqualTo(2));
            Assert.That(ComesToYou.Find(s, card.id)?.askerId, Is.EqualTo(Taylor));
            Assert.That(asks.All(ask => ask.IsCurrent(s)), Is.True);
            Assert.That(ComesToYou.OtherAskers(asks, Maya, ask => ask.IsCurrent(s)), Is.EqualTo(1));
            Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(before));
            Assert.That((s.randomState, s.npcSocial.randomState), Is.EqualTo((game, npcs)));
        }

        /// <summary>
        /// S9. Cards say when they go: "It stands until campaigning closes." from the Head of Household to the campaign,
        /// "It stands until free time ends." at the eviction and in free time. A departed sender lists nothing, nor does
        /// anything from the final Head of Household on, nor anything to a player out of the game.
        /// </summary>
        [Test]
        public void CardsSayWhenTheyGo()
        {
            foreach (var phase in new[] { EpisodePhase.HoH, EpisodePhase.Nomination, EpisodePhase.VetoMeeting, EpisodePhase.Campaign })
            {
                var s = State(phase);
                Card(s, ReplyCards.Confrontation, Taylor);
                var ask = ComesToYou.Read(s).Single();
                Assert.That((ask.lapses, ask.lapseLine), Is.EqualTo((WaitingOnYou.Lapse.CampaignCloses, ComesToYou.LapseCampaignCloses)), phase.ToString());
            }
            foreach (var phase in new[] { EpisodePhase.Eviction, EpisodePhase.Social })
            {
                var s = State(phase);
                Card(s, ReplyCards.Confrontation, Taylor);
                var ask = ComesToYou.Read(s).Single();
                Assert.That((ask.lapses, ask.lapseLine), Is.EqualTo((WaitingOnYou.Lapse.FreeTimeEnds, ComesToYou.LapseFreeTimeEnds)), phase.ToString());
            }
            Assert.That(ComesToYou.LapseCampaignCloses, Is.EqualTo("It stands until campaigning closes."));
            Assert.That(ComesToYou.LapseFreeTimeEnds, Is.EqualTo("It stands until free time ends."));
            Assert.That(ComesToYou.CardLapseLine(WaitingOnYou.Lapse.Open), Is.Null);

            var social = State(EpisodePhase.Social);
            Card(social, ReplyCards.Confrontation, Taylor);
            var gone = social.Clone(); gone.Find(Taylor).status = ContestantStatus.Evicted;
            Assert.That(ComesToYou.Read(gone), Is.Empty, "Nobody brings a card from outside the house.");
            var player = social.Clone(); player.Find(player.playerId).status = ContestantStatus.Evicted;
            Assert.That(ComesToYou.Read(player), Is.Empty, "Nor to a player out of the game.");
            foreach (var phase in new[] { EpisodePhase.FinalHoHPart1, EpisodePhase.FinalHoHPart2, EpisodePhase.FinalEviction, EpisodePhase.Finished })
            {
                var late = social.Clone(); late.phase = phase;
                Assert.That(ComesToYou.Read(late), Is.Empty, phase + ": from the final Head of Household on nobody comes.");
            }
        }

        // ================================================================== OF-p: the pitchers walk up (Source.Pitch)
        //
        // The pitches are the ones the engine deals as the nominations open (CourtTheHoH), on RolePitchBoardTests' crowned
        // season: at story rules Roles, written by name, the board (HoldTheBoard keeps three, in its order); at Threads, written
        // by name, every courting houseguest's pitch, one at a time. Neither leans on StoryRules.Current.

        /// <summary>
        /// S10. The pitchers walk up in every season. With the board every unanswered board pitch is a
        /// <see cref="ComesToYou.Source.Pitch"/> row, in <c>s.replyCards</c> order: the card's id, its pitcher, the kind, the
        /// week, the pitch's line and lapse line, never whispered. Without it only the pitch waiting first is, the one the
        /// one-at-a-time answers take. Once the nominations are made, none is.
        /// </summary>
        [Test]
        public void PitchersWalkUpInEverySeason()
        {
            var engine = RolePitchBoardTests.Dealt(RolePitchBoardTests.Crowned(), out var board, RoleTone.Pragmatic, RoleTone.Pragmatic, RoleTone.Pragmatic);
            var s = engine.Snapshot;
            Assert.That(HoHPitches.OnTheBoard(s), Is.True, "The premise: the board.");
            var asks = ComesToYou.Read(s);
            Assert.That(asks.Select(ask => (ask.source, ask.recordId, ask.askerId)),
                Is.EqualTo(s.replyCards.Select(card => (ComesToYou.Source.Pitch, card.id, card.fromId))), "Every board pitch, in the board's order.");
            Assert.That(asks.Select(ask => ask.askerId), Is.EqualTo(board));
            foreach (var ask in asks)
            {
                Assert.That((ask.type, ask.week, ask.line, ask.lapseLine, ask.lapses, ask.lapsesOnAdvance, ask.whisper),
                    Is.EqualTo((ReplyCards.Pitch, s.week, ComesToYou.PitchLine, ComesToYou.LapseNominations, WaitingOnYou.Lapse.NominationSet, false, false)), ask.askerId);
                Assert.That(ask.IsCurrent(s), Is.True, ask.askerId);
                Assert.That(ComesToYou.Find(s, ask.recordId)?.source, Is.EqualTo(ComesToYou.Source.Pitch));
            }

            var others = s.Active.Where(c => !c.isPlayer && !board.Contains(c.id)).Select(c => c.id).Take(2).ToArray();
            var nominated = RolePitchBoardTests.Apply(engine, RolePitchBoardTests.Command(s, EpisodeCommandKind.Nominate, others[0], others[1]));
            Assert.That(HoHPitches.Available(nominated), Is.False, "The premise: the nominations are made.");
            Assert.That(ComesToYou.Read(nominated).Where(ask => ask.source == ComesToYou.Source.Pitch), Is.Empty, "Nobody pitches after the nominations.");
            Assert.That(asks.Any(ask => ask.IsCurrent(nominated)), Is.False);

            var old = OneAtATime(out var oldEngine);
            var first = old.replyCards[0];
            var oldAsks = ComesToYou.Read(old);
            Assert.That(oldAsks.Select(ask => (ask.source, ask.recordId, ask.askerId)), Is.EqualTo(new[] { (ComesToYou.Source.Pitch, first.id, first.fromId) }),
                "Before the board only the pitch waiting first comes.");
            Assert.That((oldAsks[0].line, oldAsks[0].lapseLine), Is.EqualTo((ComesToYou.PitchLine, ComesToYou.LapseNominations)));
        }

        /// <summary>
        /// S11. The pitches come first, in the board's order: before every offer and every card, in <c>s.replyCards</c> order,
        /// even where the pitchers' ids run the other way. A pitch is never a Card row.
        /// </summary>
        [Test]
        public void PitchesComeFirstInTheBoardsOrder()
        {
            var crowned = RolePitchBoardTests.Crowned();
            var board = Enumerable.Reverse(RolePitchBoardTests.Board(crowned)).ToArray();
            Assert.That(board, Is.EqualTo(board.OrderByDescending(id => id, StringComparer.Ordinal)), "The premise: the ids run downward.");
            var shaped = RolePitchBoardTests.Shaped(crowned, board, RoleTone.Pragmatic, RoleTone.Pragmatic, RoleTone.Pragmatic);
            var engine = new EpisodeEngine(shaped);
            var s = RolePitchBoardTests.Apply(engine, RolePitchBoardTests.Command(shaped, EpisodeCommandKind.Advance)).Clone();
            Assert.That(s.replyCards.Select(card => card.fromId), Is.EqualTo(board), "The board, dealt warmest first: the ids downward.");
            Assert.That(ComesToYou.Read(s).Select(ask => ask.askerId), Is.EqualTo(board), "The pitches in the board's order, never by id.");

            // An offer and a card besides: a confrontation waiting first, the one card the player can answer, and an offer.
            var others = s.Active.Where(c => !c.isPlayer && !board.Contains(c.id)).Select(c => c.id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            s.replyCards.Insert(0, new ReplyCardState { id = "reply-60", week = s.week, kind = ReplyCards.Confrontation, fromId = others[0] });
            Offer(s, others[0], DealKind.Partnership, NpcDeals.OfferPrefix + "902");
            var asks = ComesToYou.Read(s);
            Assert.That(asks.Select(ask => ask.source), Has.Member(ComesToYou.Source.Card).And.Member(ComesToYou.Source.Offer),
                "The premise: a card and an offer are listed too.");
            Assert.That(asks.Take(board.Length).Select(ask => (ask.source, ask.askerId)), Is.EqualTo(board.Select(id => (ComesToYou.Source.Pitch, id))),
                "The pitches first, in the board's order.");
            Assert.That(asks.Skip(board.Length).Select(ask => ask.source), Has.None.EqualTo(ComesToYou.Source.Pitch), "and nothing after them is a pitch.");
            Assert.That(asks.Where(ask => ask.source == ComesToYou.Source.Card).Select(ask => ask.type), Has.None.EqualTo(ReplyCards.Pitch), "A pitch is never a Card.");
        }

        /// <summary>
        /// S12. A pitch stops being current once it is answered, but not on a feel-out: an answer takes that pitcher out of
        /// the queue and leaves the others; a feel-out leaves it standing; making the nominations ends them all. Before the
        /// board, answering the pitch waiting first brings the next pitcher.
        /// </summary>
        [Test]
        public void APitchStopsBeingCurrentOnceAnsweredButNotOnAFeelOut()
        {
            var engine = RolePitchBoardTests.Dealt(RolePitchBoardTests.Crowned(), out var board, RoleTone.Pragmatic, RoleTone.Pragmatic, RoleTone.Pragmatic);
            var s = engine.Snapshot;
            var asks = ComesToYou.Read(s);
            Assert.That(asks, Has.Count.EqualTo(3));
            string json = JsonConvert.SerializeObject(s);
            Assert.That(asks.All(ask => ask.IsCurrent(s)), Is.True);
            Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(json), "Reading the pitches writes nothing.");

            var heard = RolePitchBoardTests.Apply(engine, RolePitchBoardTests.Answer(s, asks[1].recordId, HoHPitches.HearKey));
            Assert.That(asks.Select(ask => ask.IsCurrent(heard)), Is.EqualTo(new[] { true, false, true }), "The answered pitcher leaves; the others stay.");
            Assert.That(ComesToYou.Read(heard).Select(ask => ask.askerId), Is.EqualTo(new[] { board[0], board[2] }));

            var felt = RolePitchBoardTests.Apply(engine, RolePitchBoardTests.Answer(heard, asks[0].recordId, HoHPitches.FeelOutKey));
            Assert.That(HoHPitches.Assessed(felt, felt.replyCards.Single(card => card.id == asks[0].recordId)), Is.True, "The premise: felt out.");
            Assert.That(asks[0].IsCurrent(felt), Is.True, "A feel-out is not an answer: the pitch still stands.");
            Assert.That(ComesToYou.Read(felt).Select(ask => ask.askerId), Is.EqualTo(new[] { board[0], board[2] }), "and still comes.");

            var others = felt.Active.Where(c => !c.isPlayer && !board.Contains(c.id)).Select(c => c.id).Take(2).ToArray();
            var nominated = RolePitchBoardTests.Apply(engine, RolePitchBoardTests.Command(felt, EpisodeCommandKind.Nominate, others[0], others[1]));
            Assert.That(asks.Any(ask => ask.IsCurrent(nominated)), Is.False, "The nominations end them all.");

            var old = OneAtATime(out var oldEngine);
            var firstAsk = ComesToYou.Read(old).Single();
            var oldFelt = RolePitchBoardTests.Apply(oldEngine, RolePitchBoardTests.Answer(old, firstAsk.recordId, HoHPitches.FeelOutKey));
            Assert.That(firstAsk.IsCurrent(oldFelt), Is.True, "Before the board too, a feel-out leaves the pitch standing.");
            var next = old.replyCards[1];
            var oldHeard = RolePitchBoardTests.Apply(oldEngine, RolePitchBoardTests.Answer(oldFelt, firstAsk.recordId, HoHPitches.TurnDownKey));
            Assert.That(firstAsk.IsCurrent(oldHeard), Is.False, "Answered, it stops.");
            Assert.That(ComesToYou.Read(oldHeard).Select(ask => (ask.source, ask.recordId)), Is.EqualTo(new[] { (ComesToYou.Source.Pitch, next.id) }),
                "and the next pitcher comes, one at a time.");
        }

        /// <summary>S13. A pitch opens "Can we talk about your nominations?" and says "It stands until you nominate.", plain words.</summary>
        [Test]
        public void APitchSaysItStandsUntilYouNominate()
        {
            Assert.That(ComesToYou.PitchLine, Is.EqualTo("Can we talk about your nominations?"));
            Assert.That(ComesToYou.PitchLine, Does.Not.Contain("\"").And.Not.Contain("“"), "The speech card adds its own quote marks.");
            Assert.That(ComesToYou.LapseNominations, Is.EqualTo("It stands until you nominate."));
            Assert.That(ComesToYou.PitchLapseLine(WaitingOnYou.Lapse.NominationSet), Is.EqualTo(ComesToYou.LapseNominations));
            Assert.That(ComesToYou.PitchLapseLine(WaitingOnYou.Lapse.CampaignCloses), Is.Null);
            Assert.That(ComesToYou.CardLine(ReplyCards.Pitch), Is.Null, "A pitch has no card's line.");

            var s = OneAtATime(out _);
            var ask = ComesToYou.Read(s).Single();
            Assert.That((ask.line, ask.lapseLine), Is.EqualTo((ComesToYou.PitchLine, ComesToYou.LapseNominations)));
        }

        /// <summary>
        /// RolePitchBoardTests' crowned season at story rules Threads, written by name, advanced into the nominations by the
        /// engine: every courting houseguest's pitch, answered one at a time.
        /// </summary>
        private static EpisodeState OneAtATime(out EpisodeEngine engine)
        {
            var crowned = RolePitchBoardTests.Crowned(roles: false);
            Assert.That(crowned.story.rulesVersion, Is.EqualTo(StoryRules.Threads), "The premise: a season before the role moments.");
            engine = new EpisodeEngine(crowned);
            var s = RolePitchBoardTests.Apply(engine, RolePitchBoardTests.Command(crowned, EpisodeCommandKind.Advance));
            Assert.That(HoHPitches.Available(s) && !HoHPitches.OnTheBoard(s), Is.True, "Pitches heard, with no board.");
            Assert.That(s.replyCards.Count(card => card.kind == ReplyCards.Pitch), Is.GreaterThan(1), "More than one pitcher came.");
            Assert.That(s.replyCards.All(card => card.kind == ReplyCards.Pitch), Is.True, "Only pitches wait.");
            return s;
        }

        // ------------------------------------------------------------------ the houses

        private static EpisodeState State(EpisodePhase phase = EpisodePhase.Campaign, int week = 3)
        {
            var s = ContentCatalog.Create(412);
            s.phase = phase; s.week = week; s.dealRulesStartWeek = 1;
            return s;
        }

        /// <summary>A card filed directly, as a fixture writes one (ReplyCards.Offer files it only under the strategy windows).</summary>
        private static ReplyCardState Card(EpisodeState s, string kind, string from, string about = null)
        {
            var card = new ReplyCardState { id = "reply-" + (50 + s.replyCards.Count), week = s.week, kind = kind, fromId = from, aboutId = about };
            s.replyCards.Add(card);
            return card;
        }

        private static DealState Offer(EpisodeState s, string from, string type, string id, int week = 0)
        {
            int filed = week > 0 ? week : s.week;
            var row = new DealState
            {
                id = id, type = type, proposerId = from, recipientId = s.playerId, status = DealStatus.Proposed,
                week = filed, expiresWeek = filed, trustImpact = DealKind.DefaultTrust(type),
            };
            if (type == DealKind.VoteSave || type == DealKind.TargetAgreement || type == DealKind.VoteEvict) row.targetId = from;
            s.deals.Add(row);
            return row;
        }

        /// <summary>
        /// A lawful house under the strategy windows where the engine answers cards (ReplyCardPayoffTests' house): two
        /// confrontations from two senders, filed through <see cref="ReplyCards.Offer"/>, the first waiting first.
        /// </summary>
        private static EpisodeState AnsweringHouse(out string first, out string second)
        {
            var s = EconomyRulesTests.Fresh(8);
            s.strategyRulesStartWeek = 1; EpisodeEngine.EnableCommitments(s);
            first = Npc(s, 0); second = Npc(s, 1);
            ReplyCards.Offer(s, ReplyCards.Confrontation, first, null);
            s.nextSequence++;
            ReplyCards.Offer(s, ReplyCards.Confrontation, second, null);
            Assert.That(s.replyCards.Select(card => card.fromId), Is.EqualTo(new[] { first, second }), "The premise: two cards, filed by the rules' own Offer.");
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
            return s;
        }

        private static EpisodeState Answer(EpisodeState s, string cardId, string key)
        {
            var result = new EpisodeEngine(s).Apply(new EpisodeCommand
            {
                id = "answer-" + s.revision, actorId = s.playerId, expectedRevision = s.revision, expectedPhase = s.phase,
                kind = EpisodeCommandKind.ReplyToHouseguest, targetId = cardId, text = key,
            });
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }

        /// <summary>The player Head of Household in the nomination, pitches heard (HoHPitchTests' house).</summary>
        private static EpisodeState PitchHouse()
        {
            var s = EconomyRulesTests.Fresh(8);
            s.strategyRulesStartWeek = 1; s.agencyRulesStartWeek = 1;
            EpisodeEngine.EnableCommitments(s); EpisodeEngine.EnableRead(s);
            s.phase = EpisodePhase.Nomination; s.hohId = s.playerId; s.nominees.Clear();
            Assert.That(HoHPitches.Available(s), Is.True, "The premise: pitches are heard.");
            return s;
        }

        private static string Npc(EpisodeState s, int index) => s.Active.Where(c => !c.isPlayer).ElementAt(index).id;

        /// <summary>Every field of each row, in order, so two readings compare row for row and field for field.</summary>
        private static List<(ComesToYou.Source, string, string, string, string, string, int, WaitingOnYou.Lapse, bool, bool)> Rows(IEnumerable<ComesToYou.Ask> asks) =>
            asks.Select(ask => (ask.source, ask.recordId, ask.askerId, ask.type, ask.line, ask.lapseLine, ask.week, ask.lapses, ask.lapsesOnAdvance, ask.whisper)).ToList();

        /// <summary>
        /// §C.1's oracle for the deal rows: the waiting list's items that are not cards and that an invitation can raise
        /// from the same asker, without the role moments' board asks, in the public order - what the next advance lets go
        /// first, the newest week, the louder kind, a veto ask by its asker's place on the block, the asker, the record.
        /// </summary>
        private static List<string> Oracle(EpisodeState s)
        {
            var invitations = NpcInvitation.Available(s);
            var pending = NpcDeals.Pending(s);
            bool roles = EpisodeEngine.RolesOn(s);
            return WaitingOnYou.Read(s).items
                .Where(item => item.kind != WaitingOnYou.Kind.Card
                    && invitations.Any(row => row.RecordId == item.id && row.NpcId == item.fromId)
                    && !(roles && RoleOffers.IsBoardAsk(s, pending.First(d => d.id == item.id))))
                .GroupBy(item => item.id).Select(group => group.First())
                .OrderByDescending(item => item.lapsesOnAdvance).ThenByDescending(item => item.week)
                .ThenByDescending(item => NpcDeals.Urgency(item.type))
                .ThenBy(item => item.kind == WaitingOnYou.Kind.VetoAsk && s.nominees.Contains(item.fromId) ? s.nominees.IndexOf(item.fromId) : int.MaxValue)
                .ThenBy(item => item.fromId, StringComparer.Ordinal).ThenBy(item => item.id, StringComparer.Ordinal)
                .Select(item => item.id).ToList();
        }
    }
}
