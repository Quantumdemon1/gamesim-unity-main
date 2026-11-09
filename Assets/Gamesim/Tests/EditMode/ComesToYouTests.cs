using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The queue of asks a houseguest can bring to the player (OFFERS-COME-TO-YOU-PLAN §C.1, <see cref="ComesToYou"/>):
    /// what is waiting on the player that a conversation can raise, never a veto board's own ask under the role
    /// moments; in an order that says nothing about how warm anybody is; with the line the asker opens with and how
    /// long it stands. Pure simulation, so the Unity-free subset runs it as well as the editor.
    /// </summary>
    public sealed class ComesToYouTests
    {
        private const string Maya = ContentCatalog.MayaId, Jamie = "jamie-roberts", Taylor = "taylor-kim",
            Casey = "casey-wilson", Riley = "riley-johnson";

        // ------------------------------------------------------------------ what it lists

        /// <summary>
        /// Outside the role moments the queue is exactly what both readers agree on: what is waiting on the player and
        /// still answerable (<see cref="WaitingOnYou.Read"/>, its cards left out), joined to what a houseguest can raise
        /// with them now (<see cref="NpcInvitation.Available"/>). The house below holds one of each that only one reader
        /// lists - an offer about somebody who has left, which waits but cannot be raised, and last week's offer at the
        /// veto meeting from somebody not deciding it, which could be raised but is past answering - and a card.
        /// </summary>
        [Test]
        public void ReadListsTheAnswerableOffersAndVetoAsksOnly()
        {
            var s = State(EpisodePhase.VetoMeeting);
            s.nominees = new List<string> { Taylor, Casey };
            s.hohId = Maya; s.vetoHolderId = Maya;
            s.Find(Riley).status = ContestantStatus.Evicted;
            Offer(s, Maya, DealKind.Partnership, "deal-ask-1");
            Offer(s, Taylor, DealKind.VetoUse, "deal-veto-2");
            Offer(s, Jamie, DealKind.TargetAgreement, "deal-ask-3").targetId = Riley;
            var old = Offer(s, Casey, DealKind.Partnership, "deal-ask-0", 2);
            s.replyCards.Add(new ReplyCardState { id = "reply-4", week = s.week, kind = ReplyCards.Confrontation, fromId = Jamie });
            Assert.That(old.expiresWeek, Is.LessThan(s.week));

            var waiting = WaitingOnYou.Read(s).items;
            var invitations = NpcInvitation.Available(s);
            var waitingIds = waiting.Select(item => item.id).ToList();
            var raisable = invitations.Select(row => row.RecordId).ToList();
            Assert.That((waitingIds.Contains("deal-ask-3"), waitingIds.Contains("reply-4"), waitingIds.Contains("deal-ask-0")), Is.EqualTo((true, true, false)),
                "The house's premise: the departed target's offer and the card wait, last week's offer is past answering.");
            Assert.That((raisable.Contains("deal-ask-0"), raisable.Contains("deal-ask-3")), Is.EqualTo((true, false)),
                "and a conversation could raise last week's offer, not the departed target's.");

            var both = waiting.Where(item => item.kind != WaitingOnYou.Kind.Card
                    && invitations.Any(row => row.RecordId == item.id && row.NpcId == item.fromId))
                .Select(item => item.id).ToList();
            var asks = ComesToYou.Read(s);
            Assert.That(asks.Select(ask => ask.recordId), Is.EquivalentTo(both));
            Assert.That(asks.Select(ask => ask.recordId), Is.EquivalentTo(new[] { "deal-ask-1", "deal-veto-2" }));

            var offer = asks.Single(ask => ask.recordId == "deal-ask-1");
            Assert.That((offer.source, offer.askerId, offer.type, offer.week), Is.EqualTo((ComesToYou.Source.Offer, Maya, DealKind.Partnership, s.week)));
            Assert.That(offer.line, Is.EqualTo(invitations.Single(row => row.RecordId == "deal-ask-1").OpeningLine));
            var veto = asks.Single(ask => ask.recordId == "deal-veto-2");
            Assert.That((veto.source, veto.askerId), Is.EqualTo((ComesToYou.Source.VetoAsk, Taylor)));
            Assert.That(veto.line, Is.EqualTo("Can we talk? I need your help with the veto."));
            Assert.That(asks.All(ask => ask.IsCurrent(s)), Is.True);
        }

        /// <summary>
        /// Under the role moments the veto board keeps its own asks - the nominees' and the Head of Household's ask to
        /// keep the block - whose no and silence carry the card's price, which the board says: the waiting list holds them,
        /// and neither the invitation nor the queue raises them (R7, Q8). An ordinary offer the same week still comes to
        /// the player. Without the role moments the same nominees' asks are listed.
        /// </summary>
        [Test]
        public void RoleBoardAsksStayOnTheBoard()
        {
            var s = RoleToneTests.Meeting(out string first, out string second).Snapshot;
            Assert.That(EpisodeEngine.RolesOn(s), Is.True);
            var nominees = RoleOffers.VetoAsks(s);
            Assert.That(nominees.Select(d => d.proposerId), Is.EquivalentTo(new[] { first, second }), "Both nominees asked.");
            if (!s.deals.Any(d => RoleOffers.IsKeepTheBlockAsk(s, d)))
                s.deals.Add(new DealState
                {
                    id = NpcDeals.KeepTheBlockPrefix + "900", type = DealKind.VetoHold, proposerId = s.hohId, recipientId = s.playerId,
                    status = DealStatus.Proposed, week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(DealKind.VetoHold),
                });
            var keep = s.deals.Single(d => RoleOffers.IsKeepTheBlockAsk(s, d) && d.status == DealStatus.Proposed);
            string other = s.Active.First(c => !c.isPlayer && c.id != first && c.id != second && c.id != s.hohId).id;
            s.deals.Add(new DealState
            {
                id = NpcDeals.OfferPrefix + "901", type = DealKind.Partnership, proposerId = other, recipientId = s.playerId,
                status = DealStatus.Proposed, week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(DealKind.Partnership),
            });
            var board = nominees.Select(d => d.id).Concat(new[] { keep.id }).ToList();
            var waiting = WaitingOnYou.Read(s).items.Select(item => item.id).ToList();
            var invitations = NpcInvitation.Available(s).Select(row => row.RecordId).ToList();
            Assert.That(board.All(id => waiting.Contains(id)), Is.True,
                "The premise: every board ask waits on the player. Waiting: " + string.Join(", ", waiting));
            Assert.That(invitations.Intersect(board), Is.Empty,
                "and no invitation raises one: the board's asks are not the invitation's under the role moments (R7, Q8). Raisable: "
                + string.Join(", ", invitations));

            var asks = ComesToYou.Read(s).Select(ask => ask.recordId).ToList();
            Assert.That(asks.Intersect(board), Is.Empty, "The board's asks stay on the board: " + string.Join(", ", asks));
            Assert.That(asks, Does.Contain(NpcDeals.OfferPrefix + "901"), "An ordinary offer the same week still comes.");

            var plain = s.Clone();
            plain.story.rulesVersion = StoryRules.Roles - 1;
            Assert.That(EpisodeEngine.RolesOn(plain), Is.False);
            Assert.That(ComesToYou.Read(plain).Select(ask => ask.recordId), Is.SupersetOf(nominees.Select(d => d.id)),
                "Without the role moments the nominees' asks come to the player.");
        }

        // ------------------------------------------------------------------ in what order

        /// <summary>
        /// Two offers alike in every public way come in their askers' order, never the filing order: the ids run the
        /// other way, as the generator filing the warmer houseguest first makes them, and the deal table follows the ids.
        /// </summary>
        [Test]
        public void OrderIsPublicNotWarmth()
        {
            var s = State();
            Offer(s, Taylor, DealKind.Partnership, "deal-ask-1");
            Offer(s, Jamie, DealKind.Partnership, "deal-ask-2");
            Assert.That(NpcDeals.Pending(s).Select(d => d.proposerId), Is.EqualTo(new[] { Taylor, Jamie }), "The deal table follows the ids.");
            Assert.That(ComesToYou.Read(s).Select(ask => ask.askerId), Is.EqualTo(new[] { Jamie, Taylor }), "The queue follows the askers.");
        }

        /// <summary>
        /// The order's public keys still lead: an offer the next advance lets go first, then the newest week, then the
        /// louder kind - a vote to keep somebody before a partnership.
        /// </summary>
        [Test]
        public void UrgencyAndWeekStillLead()
        {
            var s = State();
            Offer(s, Jamie, DealKind.Partnership, "deal-ask-1");
            Offer(s, Taylor, DealKind.VoteSave, "deal-ask-2").targetId = Taylor;
            Offer(s, Casey, DealKind.VoteSave, "deal-ask-0", 2).expiresWeek = s.week;
            Assert.That(ComesToYou.Read(s).Select(ask => ask.recordId), Is.EqualTo(new[] { "deal-ask-2", "deal-ask-1", "deal-ask-0" }),
                "This week's before last week's, and the vote to keep before the partnership.");
            Assert.That(ComesToYou.Read(s).Select(ask => ask.lapsesOnAdvance), Has.All.False);

            var meeting = State(EpisodePhase.VetoMeeting);
            meeting.weekRulesStartWeek = 1;
            meeting.nominees = new List<string> { Taylor, Casey };
            meeting.vetoResolved = true;
            Offer(meeting, Jamie, DealKind.VoteSave, "deal-ask-6").targetId = Jamie;
            Offer(meeting, Riley, DealKind.Partnership, "deal-ask-5", 2);
            Assert.That(WaitingOnYou.Read(meeting).items.Single(item => item.id == "deal-ask-5").lapsesOnAdvance, Is.True,
                "The premise: last week's offer goes on this advance.");
            Assert.That(ComesToYou.Read(meeting).Select(ask => ask.recordId), Is.EqualTo(new[] { "deal-ask-5", "deal-ask-6" }),
                "What the next advance lets go comes first, older and quieter as it is.");
        }

        /// <summary>
        /// Outside the role moments the nominees' veto asks come in the order the block was named, then anybody else's;
        /// never by asker or record id. Named the other way round, the order turns with the block. A nominee's ordinary
        /// offer takes no place from the block.
        /// </summary>
        [Test]
        public void VetoAsksFollowTheBlockOrder()
        {
            var s = State(EpisodePhase.VetoMeeting);
            s.nominees = new List<string> { Taylor, Jamie };
            Offer(s, Jamie, DealKind.VetoUse, "deal-veto-1");
            Offer(s, Taylor, DealKind.VetoUse, "deal-veto-2");
            Offer(s, Casey, DealKind.VetoUse, "deal-veto-0");
            Assert.That(EpisodeEngine.RolesOn(s), Is.False);
            Assert.That(ComesToYou.Read(s).Select(ask => ask.askerId), Is.EqualTo(new[] { Taylor, Jamie, Casey }));
            Assert.That(ComesToYou.Read(s).Select(ask => ask.source), Has.All.EqualTo(ComesToYou.Source.VetoAsk));
            s.nominees = new List<string> { Jamie, Taylor };
            Assert.That(ComesToYou.Read(s).Select(ask => ask.askerId), Is.EqualTo(new[] { Jamie, Taylor, Casey }));

            // The block's order is a veto ask's alone: a nominee's ordinary offer waits its turn by asker like anybody's.
            var offers = State(EpisodePhase.VetoMeeting);
            offers.nominees = new List<string> { Taylor, Jamie };
            Offer(offers, Taylor, DealKind.Partnership, "deal-ask-1");
            Offer(offers, Casey, DealKind.Partnership, "deal-ask-2");
            Assert.That(ComesToYou.Read(offers).Select(ask => (ask.source, ask.askerId)),
                Is.EqualTo(new[] { (ComesToYou.Source.Offer, Casey), (ComesToYou.Source.Offer, Taylor) }),
                "Casey's offer before the nominee's: the askers' order, not the block's.");
        }

        // ------------------------------------------------------------------ pure, and current

        [Test]
        public void ReadingWritesNothing()
        {
            var s = State(EpisodePhase.VetoMeeting);
            s.nominees = new List<string> { Taylor, Jamie };
            Offer(s, Maya, DealKind.FinalTwo, "deal-ask-1");
            Offer(s, Taylor, DealKind.VetoUse, "deal-veto-2");
            string before = JsonConvert.SerializeObject(s);
            uint game = s.randomState, npcs = s.npcSocial.randomState;
            var asks = ComesToYou.Read(s);
            Assert.That(asks.Count, Is.EqualTo(2));
            Assert.That(ComesToYou.Find(s, "deal-veto-2")?.askerId, Is.EqualTo(Taylor));
            Assert.That(ComesToYou.Find(s, "nothing"), Is.Null);
            Assert.That(ComesToYou.OtherAskers(asks, Maya, ask => ask.IsCurrent(s)), Is.EqualTo(1));
            Assert.That(asks.All(ask => ask.IsCurrent(s)), Is.True);
            Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(before));
            Assert.That((s.randomState, s.npcSocial.randomState), Is.EqualTo((game, npcs)));
        }

        [TestCase("accepted")] [TestCase("declined")] [TestCase("phase")] [TestCase("week")] [TestCase("terms")] [TestCase("session")]
        public void IsCurrentFollowsTheRecord(string change)
        {
            var s = State();
            var row = Offer(s, Maya, DealKind.Partnership, "deal-ask-1");
            var ask = ComesToYou.Read(s).Single();
            Assert.That(ask.IsCurrent(s), Is.True);
            s.revision++;
            Assert.That(ask.IsCurrent(s), Is.True, "A clock revision does not end the ask.");
            switch (change)
            {
                case "accepted": row.status = DealStatus.Active; break;
                case "declined": row.status = DealStatus.Declined; break;
                case "phase": s.phase = EpisodePhase.Social; break;
                case "week": s.week++; break;
                case "terms": row.type = DealKind.FinalTwo; break;
                default: s.sessionId += "-loaded"; break;
            }
            Assert.That(ask.IsCurrent(s), Is.False);
            Assert.That(new ComesToYou.Ask { source = ComesToYou.Source.Offer, recordId = "deal-ask-1", askerId = Maya }.IsCurrent(s), Is.False,
                "An ask the reader did not build stands for nothing.");
        }

        /// <summary>
        /// How long an offer stands, in the dialogue's words: until the veto is decided, for a question about the veto
        /// under the strategy windows; until this week's campaign opens for an offer that expired last week; until next
        /// week's for one that expires this week, whenever it was filed; and nothing said when nothing takes it away.
        /// </summary>
        [Test]
        public void LapseLineSaysWhen()
        {
            var veto = State(EpisodePhase.VetoMeeting);
            veto.strategyRulesStartWeek = 1;
            veto.nominees = new List<string> { Taylor, Jamie };
            Offer(veto, Taylor, DealKind.VetoUse, "deal-veto-1");
            var ask = ComesToYou.Read(veto).Single();
            Assert.That((ask.lapses, ask.lapseLine), Is.EqualTo((WaitingOnYou.Lapse.VetoDecision, "It stands until the veto is decided.")));

            var thisWeek = State();
            Offer(thisWeek, Maya, DealKind.Partnership, "deal-ask-1");
            ask = ComesToYou.Read(thisWeek).Single();
            Assert.That((ask.lapses, ask.lapseLine), Is.EqualTo((WaitingOnYou.Lapse.CampaignOpens, "It stands until next week's campaign opens.")));

            var lastWeek = State(EpisodePhase.Nomination);
            Offer(lastWeek, Maya, DealKind.Partnership, "deal-ask-1", 2);
            ask = ComesToYou.Read(lastWeek).Single();
            Assert.That((ask.lapses, ask.lapseLine), Is.EqualTo((WaitingOnYou.Lapse.CampaignOpens, "It stands until this week's campaign opens.")));

            // Filed last week and standing through this one: its expiry says when it goes, not the week it was filed.
            var carried = State();
            Offer(carried, Maya, DealKind.Partnership, "deal-ask-1", 2).expiresWeek = carried.week;
            ask = ComesToYou.Read(carried).Single();
            Assert.That((ask.week, ask.lapses, ask.lapseLine), Is.EqualTo((2, WaitingOnYou.Lapse.CampaignOpens, ComesToYou.LapseNextCampaign)));

            var open = State();
            Offer(open, Maya, DealKind.Partnership, "deal-ask-1").expiresWeek = 0;
            ask = ComesToYou.Read(open).Single();
            Assert.That((ask.lapses, ask.lapseLine), Is.EqualTo((WaitingOnYou.Lapse.Open, (string)null)));

            Assert.That(ComesToYou.LapseLine(WaitingOnYou.Lapse.CampaignOpens, true), Is.EqualTo(ComesToYou.LapseThisCampaign));
            Assert.That(ComesToYou.LapseLine(WaitingOnYou.Lapse.CampaignOpens, false), Is.EqualTo(ComesToYou.LapseNextCampaign));
            Assert.That(ComesToYou.LapseLine(WaitingOnYou.Lapse.VetoDecision, false), Is.EqualTo(ComesToYou.LapseAtTheVeto));
            Assert.That(ComesToYou.LapseLine(WaitingOnYou.Lapse.Open, true), Is.Null);
        }

        [Test]
        public void DepartedAskersAndFinalHoHListNothing()
        {
            var s = State();
            Offer(s, Maya, DealKind.Partnership, "deal-ask-1");
            Offer(s, Jamie, DealKind.TargetAgreement, "deal-ask-2").targetId = Casey;
            Assert.That(ComesToYou.Read(s).Count, Is.EqualTo(2), "The control: both come.");

            var asker = s.Clone(); asker.Find(Maya).status = ContestantStatus.Evicted;
            Assert.That(ComesToYou.Read(asker).Select(ask => ask.askerId), Is.EqualTo(new[] { Jamie }), "Nobody brings an ask from outside the house.");
            var target = s.Clone(); target.Find(Casey).status = ContestantStatus.Jury;
            Assert.That(ComesToYou.Read(target).Select(ask => ask.askerId), Is.EqualTo(new[] { Maya }), "Nor one about somebody who has gone.");
            var player = s.Clone(); player.Find(player.playerId).status = ContestantStatus.Evicted;
            Assert.That(ComesToYou.Read(player), Is.Empty, "Nor to a player out of the game.");
            foreach (var phase in new[] { EpisodePhase.FinalHoHPart1, EpisodePhase.FinalHoHPart3, EpisodePhase.FinalEviction, EpisodePhase.Finished })
            {
                var late = s.Clone(); late.phase = phase;
                Assert.That(ComesToYou.Read(late), Is.Empty, phase + ": from the final Head of Household on nobody asks.");
            }
            Assert.That(ComesToYou.Read(null), Is.Empty);
        }

        [TestCase(DealKind.FinalTwo, true)] [TestCase(DealKind.FinalThree, true)] [TestCase(DealKind.InformationSharing, true)]
        [TestCase(DealKind.Partnership, false)] [TestCase(DealKind.SafetyAgreement, false)] [TestCase(DealKind.VoteTogether, false)]
        [TestCase(DealKind.AllianceInvite, false)]
        public void WhisperKindsAreThePrivateOnes(string kind, bool whispered)
        {
            var s = State();
            Offer(s, Maya, kind, "deal-ask-1");
            var ask = ComesToYou.Read(s).Single();
            Assert.That(ask.whisper, Is.EqualTo(whispered));
            Assert.That(ComesToYou.Whispered(kind), Is.EqualTo(whispered));
        }

        [Test]
        public void OtherAskersCountsPeopleNotAsks()
        {
            ComesToYou.Ask Ask(string asker, string record) => new ComesToYou.Ask { askerId = asker, recordId = record };
            var asks = new List<ComesToYou.Ask>
            {
                Ask(Maya, "r1"), Ask(Maya, "r2"), Ask(Jamie, "r3"), Ask(Taylor, "r4"), Ask(Casey, "r5"), Ask(Casey, "r6"),
            };
            Assert.That(ComesToYou.OtherAskers(asks, Casey, ask => true), Is.EqualTo(3), "Maya's two asks are one more houseguest; Casey is the one asking.");
            Assert.That(ComesToYou.OtherAskers(asks, Casey, ask => ask.recordId != "r4"), Is.EqualTo(2), "Taylor cannot come any more.");
            Assert.That(ComesToYou.OtherAskers(asks, Casey, ask => ask.recordId != "r1"), Is.EqualTo(3), "Maya still can, with her other ask.");
            Assert.That(ComesToYou.OtherAskers(asks, Maya, ask => false), Is.Zero);
            Assert.That(ComesToYou.OtherAskers(null, Maya, ask => true), Is.Zero);
        }

        // ------------------------------------------------------------------ the house

        private static EpisodeState State(EpisodePhase phase = EpisodePhase.Campaign, int week = 3)
        {
            var s = ContentCatalog.Create(412);
            s.phase = phase; s.week = week; s.dealRulesStartWeek = 1;
            return s;
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
    }
}
