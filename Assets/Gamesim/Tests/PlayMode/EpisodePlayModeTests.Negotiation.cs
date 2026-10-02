using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Negotiation on screen (ACTIONS-DEALS-ALLIANCES-PLAN C7), in houses that play the commitment rules.
    /// A counter that stands is drawn first in the conversation it was said in, with its terms and the
    /// player's own read of the deal they asked for, and its two answers commit there; a nominee's veto
    /// ask says what it offers in return, and the yes strikes the price; the web's situation moves - a
    /// promise called in three ways, fences mended, a veto for a price - are offered where they can
    /// happen, each with its cost and the player's read of its chance, and each commits what it names.
    /// Photographed in a batch run as 'conversation-counter' and 'conversation-negotiation', with
    /// '-large' and '-4x3' forms. The engine's half is NegotiationTests.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The talking house the conversation tests use, under the commitment rules, installed twice: once
        /// to learn who the conversation is held with (<see cref="Listener"/>, which reads the house's
        /// bodies), and again shaped for them by <paramref name="shape"/>. Returns their id through
        /// <paramref name="listener"/>.
        /// </summary>
        private IEnumerator InstallNegotiatingHouse(System.Action<EpisodeState, string> shape, string[] listener)
        {
            yield return InstallTalkingHouse(8, false, state => EpisodeEngine.EnableCommitments(state));
            string id = Listener(director.Snapshot).id;
            listener[0] = id;
            yield return InstallTalkingHouse(8, false, state =>
            {
                EpisodeEngine.EnableCommitments(state);
                shape(state, id);
            });
            yield return SettleCast();
        }

        /// <summary>
        /// A counter to a partnership the player proposed, standing as the last thing said - the line the
        /// engine writes when a refusal comes back with a price (NegotiationTests shows the engine writing it).
        /// </summary>
        private static void CounterStands(EpisodeState state, string npcId)
        {
            var counter = Negotiation.CounterTo(state, npcId, DealKind.Partnership, null);
            Assert.That(counter, Is.Not.Null, "A partnership with them could come back with a price.");
            state.events.Add(new EpisodeEvent
            {
                sequence = state.nextSequence++, week = state.week, phase = state.phase, kind = Negotiation.CounterEventKind,
                text = Negotiation.CounterLine(state, counter), audienceIds = new List<string> { state.playerId, npcId },
            });
        }

        /// <summary>A promise of safety a story had this houseguest make the player, and a safety pact the player broke with them.</summary>
        private static void OwedAndWronged(EpisodeState state, string npcId)
        {
            state.promises.Add(new PromiseState
            {
                id = "promise-story-negotiation", fromId = npcId, toId = state.playerId, kind = PromiseKind.Safety,
                status = PromiseStatus.Active, week = state.week, expiresWeek = state.week + 1,
            });
            state.deals.Add(new DealState
            {
                id = "deal-player-broken-negotiation", type = DealKind.SafetyAgreement, proposerId = state.playerId, recipientId = npcId,
                status = DealStatus.Broken, week = state.week, expiresWeek = state.week, trustImpact = DealTrust.High,
                brokenById = state.playerId, settledWeek = state.week,
            });
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Negotiation_ACounterStandsAtTheHeadOfTheConversationAndIsAnsweredThere()
        {
            foreach (bool larger in new[] { false, true })
            foreach (bool take in new[] { true, false })
            {
                string size = larger ? " at the larger text" : "";
                var listener = new string[1];
                yield return InstallNegotiatingHouse(CounterStands, listener);
                yield return ApplyTextSize(larger);
                string id = listener[0];
                yield return TalkTo(id);
                string where = "A counter standing" + size + (take ? ", taken" : ", turned down");
                Assert.That(director.IsConversationOpen, Is.True, where + ": the conversation opens.");
                var open = director.Snapshot;
                var counter = Negotiation.OpenCounter(open, id);
                Assert.That(counter, Is.Not.Null, where + ": opening the conversation said nothing, so the counter still stands.");
                string name = open.Find(id).name, first = FinalistRead.FirstName(name);

                string shown = ShownText();
                Assert.That(shown, Does.Contain(EpisodeDirector.CounterHeading(name)), where + ": who it is from,");
                Assert.That(shown, Does.Contain(Negotiation.CounterTermsLine(open, counter)), where + ": what it binds, that it is no roll and that it lapses,");
                Assert.That(shown, Does.Contain(EpisodeDirector.CounterReadLine(open, counter)), where + ": and the player's own read of what they asked for.");
                var yes = ButtonWithCaption(EpisodeHud.CounterAcceptCaption(first));
                var no = ButtonWithCaption(EpisodeHud.CounterDeclineCaption(first));
                Assert.That(TagOn(yes), Is.EqualTo(EpisodeDirector.CounterAcceptTag), where + ": taking it binds the player, with no roll;");
                Assert.That(TagOn(no), Is.EqualTo(EpisodeDirector.CounterDeclineTag), where + ": turning it down is free.");
                AssertConversationFits(larger, where);
                AssertTagsHaveRoom(where);
                if (take) yield return CaptureConversation("conversation-counter" + (larger ? "-large" : ""), where, null);

                int revision = director.Snapshot.revision, deals = director.Snapshot.deals.Count;
                (take ? yes : no).onClick.Invoke();
                yield return null;
                var after = director.Snapshot;
                Assert.That(after.revision, Is.EqualTo(revision + 1), where + ": one command.");
                if (take)
                {
                    var struck = after.deals.Skip(deals).ToList();
                    Assert.That(struck, Has.Count.EqualTo(2), where + ": the partnership and its price, at once,");
                    var price = struck.Single(Negotiation.IsPrice);
                    var bought = struck.Single(d => !Negotiation.IsPrice(d));
                    Assert.That((bought.type, price.type), Is.EqualTo((DealKind.Partnership, counter.price.kind)), where);
                    Assert.That((bought.linkedDealId, price.linkedDealId), Is.EqualTo((price.id, bought.id)), where + ": each naming the other.");
                }
                else Assert.That(after.deals.Count, Is.EqualTo(deals), where + ": a plain no strikes nothing.");
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.CounterAcceptCaption(first)), Is.Null, where + ": answered, the counter is gone.");
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);
        }

        [UnityTest]
        public IEnumerator Negotiation_ANomineesVetoAskSaysWhatItOffersAndTheYesStrikesIt()
        {
            yield return InstallStrategySeason(43, state =>
            {
                AtVetoMeeting(state, true);
                EpisodeEngine.EnableLevers(state);
                EpisodeEngine.EnableCommitments(state);
                state.deals.Add(new DealState
                {
                    id = "deal-veto-9", type = DealKind.VetoUse, proposerId = state.nominees[0], recipientId = state.playerId,
                    status = DealStatus.Proposed, week = state.week, expiresWeek = state.week, trustImpact = DealKind.DefaultTrust(DealKind.VetoUse),
                });
            });
            yield return OpenStation();
            var state = director.Snapshot;
            var ask = state.deals.Single(deal => deal.id == "deal-veto-9");
            string offered = Negotiation.AskPriceLine(state, ask);
            Assert.That(offered, Is.Not.Null, "Under the rules the ask carries a price.");
            string asking = state.Find(ask.proposerId).name;
            Assert.That(ShownText(), Does.Contain(asking + " is on the block and wants your word that you will use the veto on them. " + offered),
                "The ask says what it offers in return, after the words it always had.");

            ButtonWithCaption(EpisodeHud.DealAcceptCaption).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            var taken = after.deals.Single(deal => deal.id == ask.id);
            var price = after.deals.Single(Negotiation.IsPrice);
            Assert.That(taken.status, Is.EqualTo(DealStatus.Active), "The yes is taken,");
            Assert.That((price.proposerId, price.recipientId, price.status), Is.EqualTo((ask.proposerId, after.playerId, DealStatus.Active)),
                "and the price is struck with it, theirs to pay,");
            Assert.That((taken.linkedDealId, price.linkedDealId), Is.EqualTo((price.id, taken.id)), "the two naming each other.");
            Assert.That(ButtonWithCaptionOrNull("Do not use the veto"), Is.Not.Null, "The decision itself is still to make.");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Negotiation_APromiseOwedAndABreachOfYoursAreOfferedAsMovesAndEachCommits()
        {
            foreach (bool larger in new[] { false, true })
            {
                string size = larger ? " at the larger text" : "";
                var listener = new string[1];
                yield return InstallNegotiatingHouse(OwedAndWronged, listener);
                yield return ApplyTextSize(larger);
                string id = listener[0];
                yield return TalkTo(id);
                string where = "A promise owed and a breach of the player's" + size;
                Assert.That(director.IsConversationOpen, Is.True, where + ": the conversation opens.");
                var open = director.Snapshot;
                string first = FinalistRead.FirstName(open.Find(id).name);
                var promise = open.promises.Single(p => p.id == "promise-story-negotiation");
                foreach (string approach in Negotiation.Approaches)
                {
                    var row = ButtonWithCaption(EpisodeHud.CallInCaption(approach, first, Negotiation.PromiseWords(promise.kind)));
                    Assert.That(TagOn(row), Is.EqualTo(EpisodeDirector.CallInTag(approach) + " · " + Negotiation.ShownWord(open, id, approach)),
                        where + ": calling it in to " + approach + " says its cost and the player's read of its chance.");
                }
                var mend = ButtonWithCaption(EpisodeHud.MendFencesCaption(first));
                Assert.That(TagOn(mend), Is.EqualTo(EpisodeDirector.MendTag + " · " + Negotiation.ShownWord(open, id, Negotiation.MendFences)), where);
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.VetoPriceCaption(first, DealKind.VoteSave)), Is.Null,
                    where + ": nobody holds the veto, so there is no price to name for it.");
                Assert.That(LiveRects(EpisodeHud.OddsNoteName), Has.Count.EqualTo(1), where + ": one note says whose read the chances are.");
                AssertConversationFits(larger, where);
                AssertTagsHaveRoom(where);
                yield return CaptureConversation("conversation-negotiation" + (larger ? "-large" : ""), where, null);

                int revision = director.Snapshot.revision;
                ButtonWithCaption(EpisodeHud.CallInCaption(Negotiation.Remind, first, Negotiation.PromiseWords(promise.kind))).onClick.Invoke();
                yield return null;
                var called = director.Snapshot;
                Assert.That(called.revision, Is.EqualTo(revision + 1), where + ": the reminder is one command,");
                Assert.That(called.relationships.Single(r => r.fromId == id && r.toId == called.playerId).events.Last().type,
                    Does.StartWith("promise-").And.Contain(":" + PromiseKind.Safety + ":" + Negotiation.Remind), where + ": on their record of the player,");
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.CallInCaption(Negotiation.Demand, first, Negotiation.PromiseWords(promise.kind))), Is.Null,
                    where + ": and called in for the week.");

                ButtonWithCaption(EpisodeHud.MendFencesCaption(first)).onClick.Invoke();
                yield return null;
                var mended = director.Snapshot;
                Assert.That(mended.revision, Is.EqualTo(called.revision + 1), where + ": mending fences is one command,");
                Assert.That(mended.deals.Single(d => d.id == "deal-player-broken-negotiation").brokenById, Is.EqualTo(mended.playerId),
                    where + ": the breach stands on the record,");
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.MendFencesCaption(first)), Is.Null, where + ": and one breach is mended once.");
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);
        }

        [UnityTest]
        public IEnumerator Negotiation_HoldingTheVetoAPriceCanBeNamedToANomineeAndTakenStrikesBoth()
        {
            string nominee = null;
            yield return InstallStrategySeason(46, state =>
            {
                AtVetoMeeting(state, true);
                EpisodeEngine.EnableLevers(state);
                EpisodeEngine.EnableWeek(state);
                EpisodeEngine.EnableCommitments(state);
                nominee = state.nominees[0];
                // The season's next draw takes the price: nothing draws from it before the press.
                double chance = Negotiation.Chance(state, nominee, Negotiation.VetoForAPrice, false);
                for (uint n = 1; n < 10000; n++)
                    if (new SeededRandom(n * 2654435761u).NextDouble() * 100 < chance) { state.randomState = n * 2654435761u; break; }
            });
            HoldTheHouseForTheFixture();
            yield return TalkTo(nominee);
            Assert.That(director.IsConversationOpen, Is.True, "In the week's window a word with a nominee is open.");
            var open = director.Snapshot;
            string first = FinalistRead.FirstName(open.Find(nominee).name);
            var prices = Negotiation.VetoPrices(open, nominee);
            Assert.That(prices, Is.Not.Empty, "The player holds the veto: a price can be named.");
            foreach (string kind in prices)
                Assert.That(TagOn(ButtonWithCaption(EpisodeHud.VetoPriceCaption(first, kind))),
                    Is.EqualTo(EpisodeDirector.BindsYouTag + " · " + Negotiation.ShownWord(open, nominee, Negotiation.VetoForAPrice)), kind);

            int revision = open.revision;
            ButtonWithCaption(EpisodeHud.VetoPriceCaption(first, prices[0])).onClick.Invoke();
            yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(revision + 1), "One command,");
            var veto = after.deals.Single(d => d.type == DealKind.VetoUse && d.proposerId == after.playerId && d.recipientId == nominee);
            var price = after.deals.Single(Negotiation.IsPrice);
            Assert.That((price.type, price.proposerId, price.recipientId), Is.EqualTo((prices[0], nominee, after.playerId)), "the price, theirs to pay,");
            Assert.That((veto.linkedDealId, price.linkedDealId), Is.EqualTo((price.id, veto.id)), "and the player's word on the veto, naming each other.");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.VetoPriceCaption(first, prices[0])), Is.Null, "Given, the word cannot be given again.");
            director.ClosePanels();
            yield return null;
        }
    }
}
