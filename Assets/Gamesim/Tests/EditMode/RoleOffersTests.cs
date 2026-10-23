using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The offer board's cards in words (ROLE-MOMENTS-PLAN R2, §5.1): <see cref="RoleOffers"/>. The board holds the very
    /// asks the old rows held, in the order the block was named - never the order they were filed in, which follows a
    /// houseguest's hidden view of the player; a card says the row's terms, public facts and the player's own reading and
    /// record, and nothing the player cannot see moves a word of it - its quote included, keyed on the week and the asker
    /// (R3); its stakes name the rider R3 settles, and its answers line what a no and silence cost; and a screen reader
    /// hears a control's caption, the card's heading and one line in words. Pure simulation, so the
    /// Unity-free subset runs it as well as the editor; the HUD that draws it is the PlayMode suite's.
    /// </summary>
    public sealed class RoleOffersTests
    {
        private const string BaseStakes = "Keep it: they remember it. Break it: the house may hear.";

        // ------------------------------------------------------------------ which asks, in what order

        /// <summary>
        /// The board's set is the old rows' set - this week's veto asks still waiting on the player, from houseguests
        /// still in the game - and its order is the block's. The asks' ids are made to run the other way, as filing the
        /// warmer nominee first can make them, and <see cref="NpcDeals.Pending"/> follows the ids; the board does not.
        /// Naming the block the other way round turns the board with it.
        /// </summary>
        [Test]
        public void TheBoardHoldsTheRowsAsksInTheOrderTheBlockWasNamed()
        {
            var s = AtTheMeeting(out string first, out string second, out var npcs);
            Ask(s, "deal-veto-20", first);
            Ask(s, "deal-veto-100", second);
            // Never on the board, as never in the rows: answered, lapsed, last week's, another kind, to somebody else, from somebody gone.
            Ask(s, "deal-veto-7", first).status = DealStatus.Declined;
            Ask(s, "deal-veto-8", second).status = DealStatus.Expired;
            Ask(s, "deal-veto-9", first).week = s.week - 1;
            Ask(s, "deal-ask-10", first).type = DealKind.Partnership;
            Ask(s, "deal-veto-11", first).recipientId = npcs[5];
            var gone = npcs[6];
            s.Find(gone).status = ContestantStatus.Evicted;
            Ask(s, "deal-veto-12", gone);

            var rows = NpcDeals.Pending(s).Where(d => d.type == DealKind.VetoUse && d.week == s.week
                && s.Find(d.proposerId)?.status == ContestantStatus.Active).ToList();
            Assert.That(rows.Select(d => d.proposerId), Is.EqualTo(new[] { second, first }), "The rows' order follows the ids.");
            var board = RoleOffers.VetoAsks(s);
            Assert.That(board.Select(d => d.id), Is.EquivalentTo(rows.Select(d => d.id)), "The board holds the rows' asks and only them.");
            Assert.That(board.Select(d => d.proposerId), Is.EqualTo(new[] { first, second }), "in the order the block was named.");
            Assert.That(RoleOffers.VetoBoard(s).Select(card => card.offerId), Is.EqualTo(new[] { "deal-veto-20", "deal-veto-100" }));

            s.nominees = new List<string> { second, first };
            Assert.That(RoleOffers.VetoAsks(s).Select(d => d.proposerId), Is.EqualTo(new[] { second, first }), "Named the other way, the board turns with it.");
        }

        /// <summary>Through the public engine: an answered ask leaves the board, as it leaves the rows, and the other stays.</summary>
        [Test]
        public void AnAnsweredAskLeavesTheBoardAndTheOtherStays()
        {
            var engine = RoleToneTests.Meeting(out string first, out string second);
            var meeting = engine.Snapshot;
            Assert.That(RoleOffers.VetoAsks(meeting).Select(d => d.proposerId), Is.EqualTo(meeting.nominees.ToArray()), "Both nominees asked, in block order.");
            var asks = RoleOffers.VetoAsks(meeting);
            var accept = engine.Apply(Command(meeting, asks[0].id, EpisodeEngine.AcceptDeal));
            Assert.That(accept.accepted, Is.True, accept.reason);
            Assert.That(RoleOffers.VetoAsks(accept.state).Select(d => d.id), Is.EqualTo(new[] { asks[1].id }), "The yes takes the first card away.");
            var decline = engine.Apply(Command(accept.state, asks[1].id, "decline"));
            Assert.That(decline.accepted, Is.True, decline.reason);
            Assert.That(RoleOffers.VetoAsks(decline.state), Is.Empty, "and the no the second.");
        }

        // ------------------------------------------------------------------ what a card says

        /// <summary>
        /// On a real meeting through the public engine, each card says the row's chip and terms, the asker's tone in
        /// their role, the situation word, the player's read and the line keyed on the row's own id - R1's words, read,
        /// never written again here.
        /// </summary>
        [Test]
        public void ACardSaysTheRowsTermsAndTheTonesWords()
        {
            var s = RoleToneTests.Meeting(out string first, out _).Snapshot;
            var cards = RoleOffers.VetoBoard(s);
            Assert.That(cards.Select(card => card.speakerId), Is.EqualTo(s.nominees.ToArray()));
            foreach (var card in cards)
            {
                var ask = s.deals.Single(d => d.id == card.offerId);
                string tone = RoleTone.Of(s, card.speakerId, RoleTone.Role.Nominee);
                Assert.That((card.name, card.role, card.moment, card.kind), Is.EqualTo((s.Find(card.speakerId).name, RoleTone.Role.Nominee, RoleQuotes.VetoAsk, DealKind.VetoUse)));
                Assert.That((card.chip, card.chipLine), Is.EqualTo(("Veto Commitment", RoleOffers.VetoAskLine)));
                Assert.That(card.tone, Is.EqualTo(tone));
                Assert.That(card.situation, Is.EqualTo(RoleTone.Situation(s, card.speakerId, RoleTone.Role.Nominee)));
                Assert.That(card.read, Is.EqualTo(RoleTone.ReadLine(s, card.speakerId)));
                Assert.That(card.quote, Is.Not.Null.And.EqualTo(RoleQuotes.Line(s, RoleQuotes.VetoAsk, tone, "veto-ask:w" + s.week + ":" + card.speakerId)),
                    "The quote is keyed on the week and the asker (R3), never the row's id.");
                Assert.That(card.terms, Is.EqualTo(RoleOffers.TermsPrefix + Negotiation.PriceWords(s, Negotiation.AskPrice(s, ask))));
                Assert.That(Negotiation.AskPriceLine(s, ask), Does.Contain(card.terms.Substring(RoleOffers.TermsPrefix.Length)), "in the ask's own words.");
                Assert.That(card.stakes, Is.EqualTo(RoleOffers.Stakes(RoleQuotes.VetoAsk, tone, true)), "The stakes name the tone's rider, which R3 settles.");
                Assert.That(card.answers, Is.Not.Null.And.EqualTo(RoleOffers.Answers(RoleQuotes.VetoAsk, tone)));
                Assert.That(card.history, Is.EqualTo(RoleOffers.History(s, card.speakerId)));
            }
            Assert.That(cards[0].tone, Is.EqualTo(RoleTone.Threatening), "The fixture's first nominee reads Threatening by their traits,");
            Assert.That(cards[0].stakes, Is.EqualTo("Keep it: they remember it. Refuse or ignore them, leave them up: a grudge."), "and says their rider.");
        }

        /// <summary>
        /// A card's quote is keyed on what the player can see - the week and who is asking - never the row's id, whose
        /// order follows the filing, which follows how warmly each nominee reads the player (R2's review left the key to
        /// R3). The same two asks filed the other way round, the ids swapped, show every word of every card the same.
        /// </summary>
        [Test]
        public void TheQuoteIsKeyedOnTheWeekAndTheAskerNeverTheFilingOrder()
        {
            var filed = AtTheMeeting(out string first, out string second, out _);
            Ask(filed, "deal-veto-20", first);
            Ask(filed, "deal-veto-21", second);
            var swapped = AtTheMeeting(out _, out _, out _);
            Ask(swapped, "deal-veto-20", second);
            Ask(swapped, "deal-veto-21", first);
            Assert.That(RoleOffers.VetoAsks(filed).Select(d => d.id), Is.Not.EqualTo(RoleOffers.VetoAsks(swapped).Select(d => d.id)), "The ids follow the filing.");
            List<string> Words(EpisodeState s) => Board(s).Select(line => line.Substring(line.IndexOf(" | ", StringComparison.Ordinal))).ToList();
            Assert.That(Words(swapped), Is.EqualTo(Words(filed)), "Every word of every card, the quote among them, stands.");
            var ask = RoleOffers.VetoAsks(filed).First();
            Assert.That(RoleOffers.QuoteOccasion(ask), Is.EqualTo("veto-ask:w" + filed.week + ":" + ask.proposerId));
            Assert.That(RoleOffers.QuoteOccasion(null), Is.Null);
        }

        /// <summary>
        /// Under the role moments silence costs more than a no, for every tone, and the board says so (§5.1): each card's
        /// answers line names what an honest no and silence cost with them, in words and never a number, within the text
        /// cap, silence always the dearer - the very stakes the engine charges (R3).
        /// </summary>
        [Test]
        public void TheAnswersSaySilenceCostsMoreThanANoForEveryTone()
        {
            Assert.That(RoleOffers.SilenceLine, Is.EqualTo("Silence costs more than a no."));
            var scale = new[] { "nothing", "a little goodwill", "some goodwill", "a lot of goodwill" };
            foreach (string tone in RoleTone.Ladder)
            {
                var stake = RoleTone.Stakes(tone);
                Assert.That(stake.lapse, Is.LessThan(stake.decline), tone + ": silence costs more than a no.");
                string line = RoleOffers.Answers(RoleQuotes.VetoAsk, tone);
                Assert.That(line, Is.EqualTo("Turn it down: costs " + RoleOffers.CostWords(stake.decline) + ". Leave it unanswered: costs " + RoleOffers.CostWords(stake.lapse) + "."), tone);
                Assert.That(Array.IndexOf(scale, RoleOffers.CostWords(stake.lapse)), Is.GreaterThan(Array.IndexOf(scale, RoleOffers.CostWords(stake.decline))),
                    tone + ": in words too.");
                Assert.That(RoleOffers.WordCount(line), Is.LessThanOrEqualTo(RoleOffers.MostStakesWords), line);
                Assert.That(line.Any(char.IsDigit), Is.False, line);
            }
            Assert.That(RoleOffers.Answers(RoleQuotes.VetoAsk, RoleTone.Calculated), Is.EqualTo("Turn it down: costs nothing. Leave it unanswered: costs a little goodwill."));
            Assert.That(RoleOffers.Answers(RoleQuotes.VetoAsk, RoleTone.Bitter), Is.EqualTo("Turn it down: costs some goodwill. Leave it unanswered: costs a lot of goodwill."));
            Assert.That(new[] { 0.0, -1, -2, -3, -4, -5 }.Select(RoleOffers.CostWords),
                Is.EqualTo(new[] { "nothing", "a little goodwill", "some goodwill", "some goodwill", "a lot of goodwill", "a lot of goodwill" }));
            foreach (string moment in RoleQuotes.Moments.Where(moment => moment != RoleQuotes.VetoAsk))
                Assert.That(RoleOffers.Answers(moment, RoleTone.Pragmatic), Is.Null, moment + " has no board yet.");
            Assert.That(RoleOffers.Answers(RoleQuotes.VetoAsk, "Gleeful"), Is.Null);
        }

        /// <summary>
        /// The terms are the price the row carries: their vote to keep the player where the levers run, a final two at
        /// the endgame, and "nothing promised" where no price fits - a row that is not a nominee's ask, or a season
        /// without the commitment rules.
        /// </summary>
        [Test]
        public void TheTermsAreThePriceTheRowCarriesOrNothing()
        {
            var s = RoleToneTests.Meeting(out string first, out _).Snapshot;
            var ask = RoleOffers.VetoAsks(s).First(d => d.proposerId == first);
            Assert.That(RoleOffers.Terms(s, ask), Is.EqualTo("In return: their vote to keep you, the next time you are on the block"));

            var endgame = s.Clone();
            foreach (var extra in endgame.Active.Where(c => !c.isPlayer && c.id != endgame.hohId && !endgame.nominees.Contains(c.id)).Take(2).ToList())
                extra.status = ContestantStatus.Jury;
            Assert.That(endgame.Active.Count(), Is.EqualTo(NpcDeals.EndgameSize));
            Assert.That(RoleOffers.Terms(endgame, ask), Is.EqualTo("In return: a final two"));

            var other = ask.Clone();
            other.id = "deal-word-1";
            Assert.That(RoleOffers.Terms(s, other), Is.EqualTo("In return: " + RoleOffers.NothingPromised));
            var without = s.Clone();
            without.commitmentRulesStartWeek = 0;
            Assert.That(RoleOffers.Terms(without, ask), Is.EqualTo("In return: nothing promised"));
        }

        /// <summary>
        /// The stakes name each rider tone's rider now the veto decision settles them (R3), and the other tones say what
        /// keeping and breaking a word does; unsettled, every tone said only that. Every line keeps to the text cap and
        /// states no number. No moment but the veto ask has a board yet.
        /// </summary>
        [Test]
        public void TheStakesNameTheRiderTheDecisionSettles()
        {
            Assert.That(RoleOffers.RidersSettle, Is.True, "R3 settles the riders, so a card names them.");
            foreach (string tone in RoleTone.Ladder)
            {
                Assert.That(RoleOffers.Stakes(RoleQuotes.VetoAsk, tone, false), Is.EqualTo(BaseStakes), tone);
                string settled = RoleOffers.Stakes(RoleQuotes.VetoAsk, tone, RoleOffers.RidersSettle);
                string rider = RoleTone.Stakes(tone).rider;
                if (rider == null) Assert.That(settled, Is.EqualTo(BaseStakes), tone + " has no rider.");
                else Assert.That(settled, Does.Contain(rider == RoleTone.Riders.OwesAFavour ? "favour" : "grudge"), tone + "'s rider, " + rider);
                foreach (string line in new[] { RoleOffers.Stakes(RoleQuotes.VetoAsk, tone, false), settled })
                {
                    Assert.That(RoleOffers.WordCount(line), Is.LessThanOrEqualTo(RoleOffers.MostStakesWords), line);
                    Assert.That(line.Any(char.IsDigit), Is.False, line);
                }
            }
            foreach (string moment in RoleQuotes.Moments.Where(moment => moment != RoleQuotes.VetoAsk))
                Assert.That(RoleOffers.Stakes(moment, RoleTone.Pragmatic, false), Is.Null, moment);
        }

        /// <summary>
        /// The history line is the player's own latest word with them settled before this week, as their Your word
        /// page has it: kept or broken, by whom where the record says, and when. A word settled this week, a lapsed
        /// one and nobody's are no history.
        /// </summary>
        [Test]
        public void TheHistoryIsThePlayersLatestWordSettledBeforeThisWeek()
        {
            var s = AtTheMeeting(out string first, out _, out _);
            s.week = 3;
            Assert.That(RoleOffers.History(s, first), Is.Null, "Nothing between them yet.");
            s.deals.Add(Word(s, s.playerId, first, DealStatus.Expired, null, 1, 1));
            Assert.That(RoleOffers.History(s, first), Is.Null, "A lapsed word is no history.");
            s.deals.Add(Word(s, s.playerId, first, DealStatus.Fulfilled, null, 1, 1));
            Assert.That(RoleOffers.History(s, first), Is.EqualTo("Your deal with them held in week 1"));
            s.deals.Add(Word(s, first, s.playerId, DealStatus.Fulfilled, null, 1, 2));
            Assert.That(RoleOffers.History(s, first), Is.EqualTo("Your deal with them held in week 2"), "The latest settled is the one said.");
            s.deals.Add(Word(s, first, s.playerId, DealStatus.Broken, first, 2, 2));
            Assert.That(RoleOffers.History(s, first), Is.EqualTo("They broke their word to you in week 2"), "In one week, the later record.");
            s.deals.Add(Word(s, s.playerId, first, DealStatus.Broken, s.playerId, 2, 3));
            Assert.That(RoleOffers.History(s, first), Is.EqualTo("They broke their word to you in week 2"), "A word broken this week waits for the next.");
            s.week = 4;
            Assert.That(RoleOffers.History(s, first), Is.EqualTo("You broke your word to them in week 3"));
        }

        /// <summary>
        /// Who kept a word is said only where the record says it, as the Your word page says it. A deal binds both of
        /// them and the page says only "honoured", so a kept one names nobody - whoever proposed it: a nominee's veto
        /// ask the player used the veto on is the nominee's proposal and the player's kept word, and the card never
        /// says the nominee kept it. A promise is one-sided, so a kept one is whoever gave it. A call asked for their
        /// vote, so they followed it or ignored it.
        /// </summary>
        [Test]
        public void TheHistorySaysWhoKeptAWordOnlyWhereTheRecordDoes()
        {
            var s = AtTheMeeting(out string first, out string second, out var npcs);
            s.week = 4;
            // Last week's veto ask from the nominee, which the player kept by using the veto on them.
            string ask = NpcDeals.VetoAskPrefix + s.nextSequence++;
            s.deals.Add(new DealState
            {
                id = ask, type = DealKind.VetoUse, proposerId = first, recipientId = s.playerId,
                status = DealStatus.Fulfilled, week = 3, expiresWeek = 3, trustImpact = DealKind.DefaultTrust(DealKind.VetoUse), settledWeek = 3,
            });
            Assert.That(CommitmentsRead.With(s, first).Single(c => c.id == ask).yours, Is.False, "The ask was theirs,");
            Assert.That(RoleOffers.History(s, first), Is.EqualTo("Your deal with them held in week 3"), "and the card names nobody as keeping it.");
            Assert.That(RoleOffers.History(s, first), Does.Not.StartWith("They kept"));

            s.promises.Add(Promise(s, s.playerId, second, PromiseStatus.Fulfilled, 1, 2));
            Assert.That(RoleOffers.History(s, second), Is.EqualTo("You kept your word to them in week 2"), "The player's promise, kept.");
            s.promises.Add(Promise(s, second, s.playerId, PromiseStatus.Fulfilled, 2, 3));
            Assert.That(RoleOffers.History(s, second), Is.EqualTo("They kept their word to you in week 3"), "Theirs, kept.");

            string member = npcs[3], ignored = npcs[4];
            s.alliances.Add(new AllianceState { id = "alliance-role-offers-calls", name = "The Calls", members = new List<string> { s.playerId, member, ignored }, active = true });
            s.ledger.calls.Add(new BlocCallRow
            {
                week = 2, allianceId = "alliance-role-offers-calls", callerId = s.playerId, targetId = npcs[5],
                followed = new List<string> { s.playerId, member }, defected = new List<string> { ignored },
            });
            Assert.That(CommitmentsRead.With(s, member).Single(c => c.kind == CommitmentsRead.Kinds.Call).yours, Is.True, "A call is the player's,");
            Assert.That(RoleOffers.History(s, member), Is.EqualTo("They followed your call in week 2"), "and a member who followed it followed it,");
            Assert.That(RoleOffers.History(s, ignored), Is.EqualTo("They ignored your call in week 2"), "and one who did not ignored it.");
        }

        // ------------------------------------------------------------------ the card in words

        /// <summary>
        /// A screen reader hears a control's caption, the card's heading and one line that says the tone, the
        /// situation, the read, the terms, the stakes, what a no and silence cost, and any lock in words - never the
        /// colour or the mark alone.
        /// </summary>
        [Test]
        public void AScreenReaderHearsTheCaptionTheHeadingAndOneLine()
        {
            var card = new RoleOffers.Card
            {
                offerId = "deal-veto-9", speakerId = "danielle", name = "Danielle Reyes", role = RoleTone.Role.Nominee, moment = RoleQuotes.VetoAsk,
                kind = DealKind.VetoUse, chip = "Veto Commitment", chipLine = RoleOffers.VetoAskLine, tone = RoleTone.Desperate,
                situation = "On the block again", read = "Your read: Neutral", quote = "Save me and I'll owe you. I mean it.",
                terms = "In return: a final two", stakes = RoleOffers.Stakes(RoleQuotes.VetoAsk, RoleTone.Desperate, true),
                answers = RoleOffers.Answers(RoleQuotes.VetoAsk, RoleTone.Desperate), history = "Your deal with them held in week 2",
            };
            Assert.That(RoleOffers.Heading(card), Is.EqualTo("Danielle Reyes, nominee, veto commitment"));
            Assert.That(RoleOffers.Description(card), Is.EqualTo(
                "Tone: Desperate. On the block again. Your read: Neutral. In return: a final two. Keep it: they owe you a favour. Break it: the house may hear. "
                + "Turn it down: costs some goodwill. Leave it unanswered: costs a lot of goodwill."));
            string locked = "The veto can only save one of them, and you have already given Marcus Webb your word. Turn Danielle down, or the offer lapses at the meeting.";
            Assert.That(RoleOffers.Description(card, locked), Is.EqualTo(RoleOffers.Description(card) + " " + locked), "A lock is said last.");
            Assert.That(RoleOffers.Spoken("Accept this offer", card, locked), Is.EqualTo(
                "Accept this offer. Danielle Reyes, nominee, veto commitment. " + RoleOffers.Description(card, locked)));
            Assert.That(RoleOffers.Spoken("Turn this offer down", card), Does.StartWith("Turn this offer down. Danielle Reyes, nominee, veto commitment. Tone: Desperate."));
            Assert.That(RoleOffers.Description(card), Does.Not.Contain(card.quote), "The quote is a label of its own, said where it stands.");

            Assert.That(RoleTone.Role.All.Select(RoleOffers.RoleWord), Is.EqualTo(new[] { "nominee", "Head of Household", "veto holder", "pitching to you" }));
            Assert.That(RoleOffers.RoleWord("someone"), Is.Null);
        }

        /// <summary>Every tone has a mark of its own, a plain character, so a tone is never told by its colour alone (§5.4).</summary>
        [Test]
        public void EveryToneHasAMarkOfItsOwn()
        {
            var marks = RoleTone.Ladder.Select(RoleOffers.ToneMark).ToList();
            Assert.That(marks, Has.All.Not.Null);
            Assert.That(marks.Distinct().Count(), Is.EqualTo(RoleTone.Ladder.Length), "One shape a tone.");
            Assert.That(marks.All(mark => mark.Length == 1 && mark[0] > ' ' && mark[0] < 127), Is.True, "Plain characters every font draws: " + string.Join(" ", marks));
            Assert.That(RoleOffers.ToneMark("Gleeful"), Is.Null);
        }

        // ------------------------------------------------------------------ what the player cannot see

        /// <summary>
        /// The knowledge gate (§5.3), scoped as the plan scopes it: with the set of filed rows kept, every houseguest's
        /// view of everybody - the player included - their grudges, moods, stress and memories, the pacts and deals the
        /// player is not in, and the season's generator all move, and the board - its order and every word of every card
        /// - does not.
        /// </summary>
        [Test]
        public void NothingThePlayerCannotSeeMovesTheBoard()
        {
            var s = RoleToneTests.Meeting(out _, out _).Snapshot;
            var before = Board(s);
            Assert.That(before, Has.Count.EqualTo(2));
            var hidden = s.Clone();
            Hide(hidden);
            Assert.That(RoleOffers.VetoAsks(hidden).Select(d => d.id), Is.EqualTo(RoleOffers.VetoAsks(s).Select(d => d.id)), "The same rows.");
            Assert.That(Board(hidden), Is.EqualTo(before));
        }

        /// <summary>Reading the board neither mutates the season nor draws from its generator, and a second read says the same.</summary>
        [Test]
        public void ReadingTheBoardChangesNothing()
        {
            var s = RoleToneTests.Meeting(out _, out _).Snapshot;
            string json = JsonConvert.SerializeObject(s);
            var board = Board(s);
            Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(json));
            Assert.That(Board(s), Is.EqualTo(board));
        }

        // ------------------------------------------------------------------ fixtures

        /// <summary>A season at Roles, eight in the house, at the veto meeting of week 2 with the player holding the veto and the second and third houseguests on the block.</summary>
        private static EpisodeState AtTheMeeting(out string first, out string second, out List<string> npcs)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 1111);
            ShippedRules.ApplyFresh(s);
            s.story.rulesVersion = StoryRules.Roles;
            s.week = 2;
            s.phase = EpisodePhase.VetoMeeting;
            npcs = s.contestants.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            s.hohId = npcs[0];
            first = npcs[1]; second = npcs[2];
            s.nominees = new List<string> { first, second };
            s.vetoHolderId = s.playerId;
            return s;
        }

        /// <summary>A veto ask from <paramref name="from"/> to the player, this week, waiting.</summary>
        private static DealState Ask(EpisodeState s, string id, string from)
        {
            var ask = new DealState
            {
                id = id, type = DealKind.VetoUse, proposerId = from, recipientId = s.playerId, status = DealStatus.Proposed,
                week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(DealKind.VetoUse),
            };
            s.deals.Add(ask);
            return ask;
        }

        /// <summary>A word between the player and a houseguest, settled as the commitment rules settle one: an information swap.</summary>
        private static DealState Word(EpisodeState s, string proposer, string recipient, string status, string brokenBy, int week, int settled) => new DealState
        {
            id = "deal-word-" + s.nextSequence++, type = DealKind.InformationSharing, proposerId = proposer, recipientId = recipient,
            status = status, week = week, trustImpact = DealKind.DefaultTrust(DealKind.InformationSharing),
            brokenById = brokenBy, settledWeek = settled,
        };

        /// <summary>A promise of information from <paramref name="from"/> to <paramref name="to"/>, made in <paramref name="week"/> and settled in <paramref name="settled"/>.</summary>
        private static PromiseState Promise(EpisodeState s, string from, string to, PromiseStatus status, int week, int settled) => new PromiseState
        {
            id = "promise-word-" + s.nextSequence++, fromId = from, toId = to, kind = PromiseKind.Information, status = status,
            week = week, expiresWeek = 0, settledWeek = settled,
        };

        private static EpisodeCommand Command(EpisodeState s, string target, string text) => new EpisodeCommand
        {
            id = "role-offers-" + s.revision + "-" + target, actorId = s.playerId, kind = EpisodeCommandKind.RespondToDeal,
            targetId = target, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
        };

        /// <summary>The board as text: each card's every word, in the board's order.</summary>
        private static List<string> Board(EpisodeState s) =>
            RoleOffers.VetoBoard(s).Select(card => string.Join(" | ", card.offerId, card.speakerId, card.name, card.role, card.chip, card.chipLine,
                card.tone, card.situation, card.read, card.quote, card.terms, card.stakes, card.history,
                RoleOffers.Heading(card), RoleOffers.Description(card), RoleOffers.ToneMark(card.tone))).ToList();

        /// <summary>
        /// Moves everything a card must not read: every houseguest's view of everybody, the player included; their
        /// grudges, moods, stress and memories; pacts and deals among themselves; the season's generator.
        /// </summary>
        private static void Hide(EpisodeState s)
        {
            var npcs = s.contestants.Where(c => !c.isPlayer).ToList();
            int flip = 0;
            foreach (var edge in s.relationships.Where(r => r.fromId != s.playerId)) edge.score = flip++ % 2 == 0 ? 100 : -100;
            foreach (var npc in npcs)
            {
                npc.mood = flip++ % 2 == 0 ? "Happy" : "Angry";
                npc.stressLevel = "Overwhelmed";
                s.story.grudges.Add(new GrudgeState { holderId = npc.id, targetId = s.playerId, cause = GrudgeCauses.Story, severity = 100, originWeek = 1, count = 1 });
                s.memories.Add(new MemoryState { ownerId = npc.id, subjectId = s.playerId, text = "Lied to me in week 1.", week = 1, isPrivate = true });
            }
            s.alliances.Add(new AllianceState { id = "alliance-role-offers", name = "The Hidden Pact", members = new List<string> { npcs[0].id, npcs[1].id }, active = true });
            s.deals.Add(new DealState
            {
                id = "deal-hidden-" + s.nextSequence++, type = DealKind.InformationSharing, proposerId = npcs[2].id, recipientId = npcs[3].id,
                status = DealStatus.Broken, week = 1, trustImpact = DealTrust.Medium, brokenById = npcs[2].id, settledWeek = 1,
            });
            s.randomState ^= 0x5bd1e995u;
        }
    }
}
