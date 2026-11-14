using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Offers slice 4b (OFFERS-COME-TO-YOU-PLAN §C.4, "Same-asker chaining"; §H's PlayMode list C1-C3): once the player has
    /// answered a houseguest's ask, in the dialogue or on the card, and the same houseguest has another ask in the queue that
    /// could still come, the page stays up with "There's something else", which opens it in the same conversation or card
    /// (way Chained) - or their pitch, as Hear does. It is never offered before an answer, never for another houseguest's
    /// ask and never for one that could not come; it counts no presentation, sets no asker gap and leaves the queue's order
    /// as it was; every answer sends the command it sends today.
    ///
    /// <para>Every test opts in after the fixture's reload turns asks off (AsksEnabled and ReplyAsksEnabled set explicitly),
    /// pins scaled time with PinnedClock, holds the house's pairing as slice 1's tests do and never ticks the NPC world by
    /// hand. The houses are OF-a's reply house and OF-p's pitch house, amended on disk before the reload where a pair needs
    /// a second ask; neither fixture is edited.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        // ------------------------------------------------------------------ the houses

        /// <summary>
        /// The season on disk, loaded, changed by <paramref name="amend"/>, checked lawful and saved again: a second ask
        /// for a pair, filed the way the fixture files its first.
        /// </summary>
        private void AmendTheSavedSeason(Action<EpisodeState> amend)
        {
            var store = new EpisodeSaveStore(director.SavePath);
            Assert.That(store.TryLoad(out var state, out string error), Is.True, error);
            amend(state);
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
            store.Save(state);
        }

        /// <summary>
        /// OF-a's reply house (Maya's card first; with <paramref name="withOffers"/> the generator's offers from Maya and
        /// Jamie), amended by <paramref name="amend"/>, reloaded, the reply switch on, asks on with the house's pairing held,
        /// and the walk-overs as <paramref name="walkOvers"/> says. No visit waited for.
        /// </summary>
        private IEnumerator InstallComesToYouChainHouse(string kind, bool withOffers, Action<EpisodeState> amend = null,
            bool walkOvers = true, string secondSender = null)
        {
            comesReplyCardId = SaveComesToYouReplyState(kind, withOffers, secondSender);
            if (amend != null) AmendTheSavedSeason(amend);
            yield return ReloadEpisode();
            director.ReplyAsksEnabled = true;
            yield return TurnAsksOnInTheLoadedHouse(new string[0], true);
            director.AskWalkOvers = walkOvers;
            Assert.That(director.Snapshot.replyCards.Select(card => card.id), Does.Contain(comesReplyCardId), "The card survived the load.");
        }

        /// <summary><see cref="InstallComesToYouChainHouse"/> with walk-overs, then Maya's visit reserved for her card.</summary>
        private IEnumerator InstallComesToYouChainVisit(string kind, bool withOffers, Action<EpisodeState> amend = null, string secondSender = null)
        {
            yield return InstallComesToYouChainHouse(kind, withOffers, amend, true, secondSender);
            yield return WaitForTheFirstVisit(new string[0]);
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(comesReplyCardId), "Maya comes over with her card first.");
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesMaya));
        }

        /// <summary>Maya's offer in the installed season, listed in the queue behind her card.</summary>
        private string MayasChainOffer()
        {
            var state = director.Snapshot;
            var offer = NpcDeals.Pending(state).First(row => row.proposerId == ComesMaya);
            var queue = ComesToYou.Read(state);
            Assert.That(queue.Select(ask => (ask.source, ask.askerId)).First(), Is.EqualTo((ComesToYou.Source.Card, ComesMaya)), "Maya's card leads the queue,");
            Assert.That(queue.Any(ask => ask.source == ComesToYou.Source.Offer && ask.recordId == offer.id), Is.True, "and her offer waits in it too.");
            return offer.id;
        }

        /// <summary>The ask clocks and counts the chain must leave as they were: the phase's and week's shown, a record's presentations, an asker's gap.</summary>
        private (int phase, int week, int record, float asker) ChainCounts(string recordId, string askerId) =>
            (NpcRead<int>("askPhaseShown"), NpcRead<int>("askWeekShown"),
             NpcRead<Dictionary<string, int>>("askPresentations").TryGetValue(recordId, out int shown) ? shown : 0,
             NpcRead<Dictionary<string, float>>("askerNextAt").TryGetValue(askerId, out float gap) ? gap : 0f);

        /// <summary>Lets a deal's exchange play out through its own Continue, as a player does; bounded.</summary>
        private IEnumerator PlayOutTheChainExchange()
        {
            for (int step = 0; step < 6 && director.IsConversationExchangePlaying; step++)
            {
                ButtonWithCaptionOrNull(EpisodeHud.ConversationContinueCaption)?.onClick.Invoke();
                yield return null;
            }
            Assert.That(director.IsConversationExchangePlaying, Is.False, "The answer's exchange has played.");
            yield return null;
        }

        /// <summary>The chain's page after an answer: what happened, "There's something else" with the keyboard on it, Back to topics, no pointer hold.</summary>
        private IEnumerator AssertTheChainPage(string nextRecordId, string at)
        {
            yield return null; yield return null;
            Assert.That(FindButton(EpisodeHud.AskSomethingElseCaption), Is.Not.Null, at + ": \"There's something else\" shows,");
            Assert.That(SelectedName(), Is.EqualTo(EpisodeHud.AskSomethingElseCaption), at + ": and the keyboard is on it.");
            Assert.That(FindButton(EpisodeDirector.ConversationTopicsBackCaption), Is.Not.Null, at + ": Back to topics.");
            Assert.That(ShownText(), Does.Contain("WHAT HAPPENED"), at + ": what happened, said first.");
            Assert.That(ComesHud.PointerHeld, Is.False, at + ": no pointer hold: the answer has been given.");
            Assert.That(director.AskChainNextRecordId, Is.EqualTo(nextRecordId), at + ": it opens the same houseguest's next ask.");
        }

        /// <summary>
        /// Presses "There's something else": nothing committed or written, the next ask set aside, and no presentation, cap
        /// count or asker gap taken for it.
        /// </summary>
        private void PressSomethingElse(string nextRecordId, string askerId)
        {
            var before = director.Snapshot;
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            var counts = ChainCounts(nextRecordId, askerId);
            Assert.That(director.IsAskSetAside(nextRecordId), Is.False, "The premise: the next ask was not seen this phase.");
            ButtonWithCaption(EpisodeHud.AskSomethingElseCaption).onClick.Invoke();
            Assert.That(director.IsAskSetAside(nextRecordId), Is.True, "The chained ask is set aside as it opens.");
            Assert.That(ChainCounts(nextRecordId, askerId), Is.EqualTo(counts), "No presentation, cap count or asker gap: the player asked for it.");
            AssertEquivalent(before, director.Snapshot);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(director.SavePath), "Opening it commits nothing.");
        }

        // ------------------------------------------------------------------ C1. a card, then the same houseguest's offer

        [UnityTest]
        public IEnumerator ComesToYou_ChainThereIsSomethingElse() => PinnedClock(ThereIsSomethingElseHeld);

        private IEnumerator ThereIsSomethingElseHeld() =>
            WithTheHouseHeld(() => InstallComesToYouChainVisit(ReplyCards.Confrontation, true), ThereIsSomethingElse);

        private IEnumerator ThereIsSomethingElse()
        {
            string cardId = comesReplyCardId, offerId = MayasChainOffer();
            yield return WaitForAskDialogue(ComesMaya);
            Assert.That((director.CurrentAskWay, director.CurrentAskRecordId), Is.EqualTo((EpisodeDirector.AskWay.WalkOver, cardId)));
            yield return null; yield return null;
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.AskSomethingElseCaption), Is.Null, "Never before an answer.");
            Assert.That(director.AskChainNextRecordId, Is.Null);

            yield return WaitOutThePointerHold();
            var justBefore = director.Snapshot;
            ButtonWithCaption(EpisodeHud.ReplyCaption("Apologize")).onClick.Invoke();
            AssertIntentCommand(justBefore, EpisodeCommandKind.ReplyToHouseguest, cardId, "apologize");
            Assert.That(director.TalkingToId, Is.EqualTo(ComesMaya), "Still with Maya.");
            yield return AssertTheChainPage(offerId, "After the card's answer");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ReplyCaption("Apologize")), Is.Null, "Nothing left to answer on the card.");

            PressSomethingElse(offerId, ComesMaya);
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.Chained), "Chained: the player asked for it.");
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(offerId));
            Assert.That(director.InvitedDealRecordId, Is.EqualTo(offerId));
            Assert.That(director.CurrentConversationCategory, Is.EqualTo(EpisodeDirector.DealsCategoryCaption));
            yield return null; yield return null;
            Assert.That(ShownText(), Does.Contain(EpisodeHud.AskRaisedEyebrow("Maya")), "The offer's own raised eyebrow.");
            Assert.That(FindButton(EpisodeHud.DealAcceptCaption), Is.Not.Null);
            Assert.That(FindButton(EpisodeHud.DealDeclineCaption), Is.Not.Null);
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ThinkItOverCaption), Is.Null, "Asked for, not walked over: no Think it over.");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.AskSomethingElseCaption), Is.Null, "Not before this answer either.");

            yield return WaitOutThePointerHold();
            var beforeDecline = director.Snapshot;
            ButtonWithCaption(EpisodeHud.DealDeclineCaption).onClick.Invoke();
            AssertIntentCommand(beforeDecline, EpisodeCommandKind.RespondToDeal, offerId, "decline");
            yield return PlayOutTheChainExchange();
            // Nothing else from Maya: the chain ends, and the dialogue goes as it always has, onto the Deals page.
            Assert.That(director.AskChainNextRecordId, Is.Null, "Nothing else from Maya is current.");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.AskSomethingElseCaption), Is.Null, "The chain ends.");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None), "The dialogue is let go.");
            Assert.That(director.TalkingToId, Is.EqualTo(ComesMaya));
            director.ClosePanels();
            yield return null;
        }

        // ------------------------------------------------------------------ C2. only the same houseguest, only after an answer

        [UnityTest]
        public IEnumerator ComesToYou_ChainNeverToAnotherHouseguest() => PinnedClock(NeverToAnotherHeld);

        private IEnumerator NeverToAnotherHeld() =>
            WithTheHouseHeld(() => InstallComesToYouChainVisit(ReplyCards.Confrontation, false, secondSender: ComesJamie), NeverToAnother);

        /// <summary>Maya's card and Jamie's behind it: once Maya's is answered Jamie's is the one waiting, and no chain is offered.</summary>
        private IEnumerator NeverToAnother()
        {
            string mayaCard = comesReplyCardId;
            string jamieCard = director.Snapshot.replyCards.Single(row => row.fromId == ComesJamie).id;
            yield return WaitForAskDialogue(ComesMaya);
            yield return null; yield return null;
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.AskSomethingElseCaption), Is.Null, "Never before an answer.");
            yield return WaitOutThePointerHold();
            var justBefore = director.Snapshot;
            ButtonWithCaption(EpisodeHud.ReplyCaption("Apologize")).onClick.Invoke();
            AssertIntentCommand(justBefore, EpisodeCommandKind.ReplyToHouseguest, mayaCard, "apologize");
            Assert.That(ComesToYou.Read(director.Snapshot).Select(ask => (ask.source, ask.askerId, ask.recordId)),
                Is.EqualTo(new[] { (ComesToYou.Source.Card, ComesJamie, jamieCard) }), "The premise: Jamie's card is the one waiting now.");
            yield return null; yield return null;
            Assert.That(director.AskChainNextRecordId, Is.Null, "Never across houseguests.");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.AskSomethingElseCaption), Is.Null);
            Assert.That(director.CurrentConversationCategory, Is.Null, "The dialogue goes as it always has: off the Deals page,");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None));
            Assert.That(ShownText(), Does.Contain("WHAT HAPPENED"), "saying what happened.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ComesToYou_ChainOnlyForAnAskThatCanStillCome() => PinnedClock(OnlyWhatCanStillComeHeld);

        private IEnumerator OnlyWhatCanStillComeHeld() =>
            WithTheHouseHeld(() => InstallComesToYouChainVisit(ReplyCards.Confrontation, true), OnlyWhatCanStillCome);

        /// <summary>Maya's card and her offer, the offer already seen this phase (set aside): it fails AskCanStillCome, so no chain.</summary>
        private IEnumerator OnlyWhatCanStillCome()
        {
            string cardId = comesReplyCardId, offerId = MayasChainOffer();
            yield return WaitForAskDialogue(ComesMaya);
            NpcRead<HashSet<string>>("askSetAside").Add(offerId);
            yield return WaitOutThePointerHold();
            var justBefore = director.Snapshot;
            ButtonWithCaption(EpisodeHud.ReplyCaption("Apologize")).onClick.Invoke();
            AssertIntentCommand(justBefore, EpisodeCommandKind.ReplyToHouseguest, cardId, "apologize");
            Assert.That(NpcDeals.Pending(director.Snapshot).Any(row => row.id == offerId), Is.True, "The premise: her offer still waits.");
            yield return null; yield return null;
            Assert.That(director.AskChainNextRecordId, Is.Null, "An ask set aside this phase cannot still come.");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.AskSomethingElseCaption), Is.Null);
            Assert.That(director.CurrentConversationCategory, Is.Null, "The dialogue goes as it always has.");
            director.ClosePanels();
            yield return null;
        }

        // ------------------------------------------------------------------ C3. on the card host

        [UnityTest]
        public IEnumerator ComesToYou_ChainOnTheCardHost() => PinnedClock(OnTheCardHostHeld);

        private IEnumerator OnTheCardHostHeld() =>
            WithTheHouseHeld(() => InstallComesToYouChainHouse(ReplyCards.Confrontation, true, walkOvers: false), OnTheCardHost);

        private IEnumerator OnTheCardHost()
        {
            string cardId = comesReplyCardId, offerId = MayasChainOffer();
            yield return WaitForAskCardUp("the reply card");
            Assert.That((director.CurrentAskWay, director.CurrentAskRecordId), Is.EqualTo((EpisodeDirector.AskWay.Card, cardId)));
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.AskSomethingElseCaption), Is.Null, "Never before an answer.");
            yield return WaitOutThePointerHold();
            var justBefore = director.Snapshot;
            ButtonWithCaption(EpisodeHud.ReplyCaption("Deflect")).onClick.Invoke();
            AssertIntentCommand(justBefore, EpisodeCommandKind.ReplyToHouseguest, cardId, "deflect");
            Assert.That(director.IsAskCardOpen, Is.True, "The card stays up for the outcome.");
            yield return null; yield return null;
            Assert.That(LastLabel(EpisodeHud.AskOutcomeName), Is.Not.Null, "The answer's own line,");
            var somethingElse = FindButton(EpisodeHud.AskSomethingElseCaption);
            var done = FindButton(EpisodeDirector.SceneCardDoneCaption);
            Assert.That(somethingElse.transform.parent, Is.SameAs(done.transform.parent), "\"There's something else\" beside Back to the house.");
            Assert.That(SelectedName(), Is.EqualTo(EpisodeHud.AskSomethingElseCaption), "The keyboard is on it.");
            Assert.That(director.AskChainNextRecordId, Is.EqualTo(offerId));

            PressSomethingElse(offerId, ComesMaya);
            Assert.That(director.IsAskCardOpen, Is.True, "The same card,");
            Assert.That((director.CurrentAskWay, director.CurrentAskRecordId), Is.EqualTo((EpisodeDirector.AskWay.Chained, offerId)), "now on her offer.");
            yield return null; yield return null;
            Assert.That(ShownText(), Does.Contain(EpisodeHud.AskRaisedEyebrow("Maya")), "The offer's raised eyebrow.");
            Assert.That(FindButton(EpisodeHud.DealDeclineCaption), Is.Not.Null);
            yield return WaitOutThePointerHold();
            var beforeDecline = director.Snapshot;
            ButtonWithCaption(EpisodeHud.DealDeclineCaption).onClick.Invoke();
            AssertIntentCommand(beforeDecline, EpisodeCommandKind.RespondToDeal, offerId, "decline");
            yield return null;
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.AskSomethingElseCaption), Is.Null, "Nothing else from Maya: the chain ends.");
            ButtonWithCaption(EpisodeDirector.SceneCardDoneCaption).onClick.Invoke();
            Assert.That(director.IsAskCardOpen, Is.False);
            Assert.That(director.IsPanelOpen, Is.False, "Back to the house closes the card.");
        }

        // ------------------------------------------------------------------ C4. a card, then the same sender's next card

        [UnityTest]
        public IEnumerator ComesToYou_ChainACardToTheSameSendersNextCard() => PinnedClock(CardToCardHeld);

        private IEnumerator CardToCardHeld() =>
            WithTheHouseHeld(() => InstallComesToYouChainVisit(ReplyCards.Confrontation, false, state =>
            {
                state.nextSequence++;
                ReplyCards.Offer(state, ReplyCards.Gossip, ComesMaya, ComesJamie);
                Assert.That(state.replyCards.Select(card => (card.kind, card.fromId)),
                    Is.EqualTo(new[] { (ReplyCards.Confrontation, ComesMaya), (ReplyCards.Gossip, ComesMaya) }), "Maya's second card, behind her first.");
            }), CardToCard);

        private IEnumerator CardToCard()
        {
            string first = comesReplyCardId;
            string second = director.Snapshot.replyCards.Single(row => row.kind == ReplyCards.Gossip).id;
            Assert.That(ComesToYou.Read(director.Snapshot).Select(ask => ask.recordId), Is.EqualTo(new[] { first }), "Only the card waiting first is listed.");
            yield return WaitForAskDialogue(ComesMaya);
            yield return WaitOutThePointerHold();
            var justBefore = director.Snapshot;
            ButtonWithCaption(EpisodeHud.ReplyCaption("Apologize")).onClick.Invoke();
            AssertIntentCommand(justBefore, EpisodeCommandKind.ReplyToHouseguest, first, "apologize");
            yield return AssertTheChainPage(second, "After the first card's answer");

            PressSomethingElse(second, ComesMaya);
            Assert.That((director.CurrentAskWay, director.CurrentAskRecordId), Is.EqualTo((EpisodeDirector.AskWay.Chained, second)));
            yield return null; yield return null;
            Assert.That(ShownText(), Does.Contain(EpisodeHud.AskCardRaisedEyebrow("Maya")), "A card's raised eyebrow: MAYA WANTS A WORD.");
            Assert.That(LastLabel(EpisodeHud.AskLineName)?.text, Is.EqualTo("\"" + ComesToYou.GossipLine + "\""));
            yield return WaitOutThePointerHold();
            var beforeSecond = director.Snapshot;
            ButtonWithCaption(EpisodeHud.ReplyCaption("Gossip back")).onClick.Invoke();
            AssertIntentCommand(beforeSecond, EpisodeCommandKind.ReplyToHouseguest, second, "gossip-back");
            yield return null;
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.AskSomethingElseCaption), Is.Null, "Nothing else from Maya: the chain ends.");
            Assert.That(director.CurrentConversationCategory, Is.Null, "and the dialogue goes as it always has.");
            director.ClosePanels();
            yield return null;
        }

        // ------------------------------------------------------------------ C5. an offer, then the same houseguest's pitch

        [UnityTest, Timeout(300000)]
        public IEnumerator ComesToYou_ChainAnOfferToThePitch() => PinnedClock(OfferToThePitchHeld);

        /// <summary>The offer <see cref="InstallComesToYouChainPitchHouse"/> added for the first pitcher.</summary>
        private const string ComesChainPitcherOffer = NpcDeals.OfferPrefix + "951";

        private IEnumerator OfferToThePitchHeld() => WithTheHouseHeld(InstallComesToYouChainPitchHouse, OfferToThePitch);

        /// <summary>
        /// OF-p's pitch house with the board, and an offer to work together from the first pitcher on the board, filed the
        /// way a houseguest's offer is (proposed to the player this week, the kind's own trust). Reloaded as OF-p's install does.
        /// </summary>
        private IEnumerator InstallComesToYouChainPitchHouse()
        {
            SaveComesToYouPitchState(true);
            AmendTheSavedSeason(state =>
            {
                string pitcher = state.replyCards[0].fromId;
                state.deals.Add(new DealState
                {
                    id = ComesChainPitcherOffer, type = DealKind.Partnership, proposerId = pitcher, recipientId = state.playerId,
                    status = DealStatus.Proposed, week = state.week, expiresWeek = state.week, trustImpact = DealKind.DefaultTrust(DealKind.Partnership),
                });
                var queue = ComesToYou.Read(state);
                Assert.That(queue.Select(ask => ask.source).First(), Is.EqualTo(ComesToYou.Source.Pitch), "The pitches lead the queue,");
                Assert.That(queue.Any(ask => ask.source == ComesToYou.Source.Offer && ask.recordId == ComesChainPitcherOffer && ask.askerId == pitcher),
                    Is.True, "and the first pitcher's offer waits in it too.");
            });
            yield return ReloadEpisode();
            director.BuildNpcWorldForDiagnostics();
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "The house's world is built.");
            director.ReplyAsksEnabled = true;
            yield return TurnAsksOnInTheLoadedHouse(new string[0], true);
            director.AskWalkOvers = true;
        }

        private IEnumerator OfferToThePitch()
        {
            string pitchId = comesPitchIds[0];
            string pitcher = ComesPitch(pitchId).fromId;
            yield return WaitForTheFirstVisit(new string[0]);
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(pitchId), "The first pitcher walks up with the pitch.");
            yield return WaitForTheLineWithNoSecondDue();
            // Talking to them raises their offer: a pitch opens no conversation, so it is not raised.
            Assert.That(director.TryOpenNpc(pitcher), Is.True);
            Assert.That((director.CurrentAskWay, director.CurrentAskRecordId), Is.EqualTo((EpisodeDirector.AskWay.Raised, ComesChainPitcherOffer)));
            Assert.That(director.IsAskSetAside(pitchId), Is.False, "The pitch was not heard.");
            yield return null; yield return null;
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.AskSomethingElseCaption), Is.Null, "Never before an answer.");
            yield return WaitOutThePointerHold();
            var justBefore = director.Snapshot;
            ButtonWithCaption(EpisodeHud.DealDeclineCaption).onClick.Invoke();
            AssertIntentCommand(justBefore, EpisodeCommandKind.RespondToDeal, ComesChainPitcherOffer, "decline");
            yield return PlayOutTheChainExchange();
            yield return AssertTheChainPage(pitchId, "After the offer's answer");

            var before = director.Snapshot;
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            var counts = ChainCounts(pitchId, pitcher);
            ButtonWithCaption(EpisodeHud.AskSomethingElseCaption).onClick.Invoke();
            // The press frame: the pitches open as Hear opens them.
            Assert.That(director.IsPhasePanelOpen, Is.True, "The nomination's own panel opens,");
            Assert.That(director.TalkingToId, Is.Null, "and the conversation goes with it.");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None), "A pitch opens no ask dialogue.");
            Assert.That(director.IsAskSetAside(pitchId), Is.True, "The pitch is set aside as it opens.");
            Assert.That(ChainCounts(pitchId, pitcher), Is.EqualTo(counts), "No presentation, cap count or asker gap.");
            AssertEquivalent(before, director.Snapshot);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(director.SavePath), "Opening the pitches commits nothing.");
            yield return null;
            LandThePanel();
            yield return null;
            Assert.That(LastActive(EpisodeHud.OfferBoardName), Is.Not.Null, "The pitch board, as Hear opens it.");
            director.ClosePanels();
        }

        // ------------------------------------------------------------------ C6. the queue goes on as it was

        [UnityTest, Timeout(300000)]
        public IEnumerator ComesToYou_ChainLeavesTheQueueAsItWas() => PinnedClock(TheQueueAsItWasHeld);

        private IEnumerator TheQueueAsItWasHeld() =>
            WithTheHouseHeld(() => InstallComesToYouChainVisit(ReplyCards.Confrontation, true), TheQueueAsItWas);

        /// <summary>
        /// After a chain from Maya's card to her offer, closing starts the gap as any close does, one presentation was counted
        /// (her walk-over, never the chain), and once the gap is released the next walk-up is the queue's next: Jamie's offer.
        /// </summary>
        private IEnumerator TheQueueAsItWas()
        {
            string cardId = comesReplyCardId, offerId = MayasChainOffer();
            string jamieOffer = NpcDeals.Pending(director.Snapshot).First(row => row.proposerId == ComesJamie).id;
            yield return WaitForAskDialogue(ComesMaya);
            int shownAfterTheWalk = NpcRead<int>("askPhaseShown");
            yield return WaitOutThePointerHold();
            ButtonWithCaption(EpisodeHud.ReplyCaption("Apologize")).onClick.Invoke();
            yield return AssertTheChainPage(offerId, "After the card's answer");
            PressSomethingElse(offerId, ComesMaya);
            yield return null; yield return null;
            yield return WaitOutThePointerHold();
            var beforeDecline = director.Snapshot;
            ButtonWithCaption(EpisodeHud.DealDeclineCaption).onClick.Invoke();
            AssertIntentCommand(beforeDecline, EpisodeCommandKind.RespondToDeal, offerId, "decline");
            yield return PlayOutTheChainExchange();
            Assert.That(NpcRead<int>("askPhaseShown"), Is.EqualTo(shownAfterTheWalk), "The chain counted no presentation.");

            float clock = NpcRead<float>("askClock");
            ButtonWithCaption(EpisodeDirector.ConversationTopicsBackCaption).onClick.Invoke();
            Assert.That(NpcRead<float>("askNextAt"), Is.GreaterThanOrEqualTo(clock + EpisodeDirector.AskGapSeconds - .01f), "Back to topics starts the gap, as today.");
            director.ClosePanels();
            Assert.That(ComesToYou.Read(director.Snapshot).Where(ask => ask.askerId != ComesMaya).Select(ask => ask.recordId).First(),
                Is.EqualTo(jamieOffer), "The premise: Jamie's offer is the queue's next.");
            yield return null; yield return null;
            NpcWrite("askNextAt", 0f);
            yield return WaitForTheFirstVisit(new string[0]);
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(jamieOffer), "The next walk-up is the queue's next, as it was.");
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesJamie));
        }
    }
}
