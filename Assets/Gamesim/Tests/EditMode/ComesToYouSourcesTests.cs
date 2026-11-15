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
        /// no row at all in OF-a, and a confrontation filed behind it is not the card the player can answer, so it is not
        /// listed either.
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
            Assert.That(ComesToYou.Read(s), Is.Empty, "No row: a pitch's walk-up is its own source, not a Card.");

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
