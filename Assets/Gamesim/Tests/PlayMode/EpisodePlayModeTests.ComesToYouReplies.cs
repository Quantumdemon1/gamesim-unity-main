using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Offers slice 4a (OFFERS-COME-TO-YOU-PLAN §C.4, source Card; §H's PlayMode list): a houseguest who came to the
    /// player with a confrontation or gossip walks up and says so, and the dialogue opens on the card's own three answers,
    /// which send the one command every card surface sends, ReplyToHouseguest with the card's id and the answer's key.
    /// Every test opts in after the fixture's reload turns asks off (AsksEnabled and ReplyAsksEnabled set explicitly),
    /// pins scaled time with PinnedClock, holds the house's pairing as slice 1's tests do, and never ticks the NPC world
    /// by hand. The invitation flow's house and slice 1-3's fixtures are not edited: this file builds its own card.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The id of the card <see cref="SaveComesToYouReplyState"/> filed.</summary>
        private string comesReplyCardId;

        /// <summary>
        /// The invitation flow's house (SaveNpcInvitationFlowState's relationships and spent actions), with the generator's
        /// offers from Maya and Jamie only when <paramref name="withOffers"/>, the strategy windows on from this week, and one
        /// card from Maya filed by the rules' own <see cref="ReplyCards.Offer"/>: a confrontation or gossip (about Jamie), in
        /// free time; or a plea about Jamie in the campaign, Maya and Jamie on the block (offers slice 4p's leftovers of 4a).
        /// With <paramref name="secondSender"/>, a confrontation from them filed after Maya's. Saved; returns Maya's card's id.
        /// </summary>
        private string SaveComesToYouReplyState(string kind, bool withOffers, string secondSender = null)
        {
            var state = ContentCatalog.Create(6123);
            state.dealRulesStartWeek = 1;
            foreach (var edge in state.relationships) edge.score = 0;
            foreach (var npc in state.Active.Where(person => !person.isPlayer))
            {
                state.relationships.Single(edge => edge.fromId == npc.id && edge.toId == state.playerId).score = 10;
                state.relationships.Single(edge => edge.fromId == state.playerId && edge.toId == npc.id).score = 60;
            }
            state.relationships.Single(edge => edge.fromId == ComesMaya && edge.toId == state.playerId).score = 90;
            state.relationships.Single(edge => edge.fromId == ComesJamie && edge.toId == state.playerId).score = 35;
            if (withOffers)
            {
                NpcDeals.Propose(state);
                Assert.That(NpcDeals.Pending(state).Select(row => row.proposerId), Is.SupersetOf(new[] { ComesMaya, ComesJamie }),
                    "The actual generator puts offers from Maya and Jamie.");
            }
            else Assert.That(NpcDeals.Pending(state), Is.Empty, "No offer in the house.");
            Assert.That(state.phase, Is.EqualTo(EpisodePhase.Social), "Free time, where a confrontation and gossip wait.");
            if (kind == ReplyCards.Plea) OnTheBlockInTheCampaign(state);
            state.socialActions = EpisodeEngine.SocialActionBudget(state);
            Assert.That(state.replyCards, Is.Empty, "The invitation flow's house carries no card, so slices 1-3 read today's queue.");
            state.strategyRulesStartWeek = state.week;
            Assert.That(StrategyRules.Apply(state), Is.True);
            ReplyCards.Offer(state, kind, ComesMaya, kind == ReplyCards.Gossip || kind == ReplyCards.Plea ? ComesJamie : null);
            var filed = new List<(string, string)> { (kind, ComesMaya) };
            if (secondSender != null)
            {
                state.nextSequence++;
                ReplyCards.Offer(state, ReplyCards.Confrontation, secondSender, null);
                filed.Add((ReplyCards.Confrontation, secondSender));
            }
            Assert.That(state.replyCards.Select(card => (card.kind, card.fromId)), Is.EqualTo(filed),
                "Maya's card first, filed by the rules' own Offer.");
            var first = ComesToYou.Read(state).First();
            Assert.That((first.source, first.recordId), Is.EqualTo((ComesToYou.Source.Card, state.replyCards[0].id)),
                "The card leads the queue: free time's end clears it on the next advance.");
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(state);
            return state.replyCards[0].id;
        }

        /// <summary>
        /// The campaign, where a plea waits: Maya and Jamie on the block, Head of Household and veto holder the first
        /// houseguest who is neither, the veto settled and its lineup seated with all three.
        /// </summary>
        private static void OnTheBlockInTheCampaign(EpisodeState state)
        {
            var others = state.Active.Where(person => !person.isPlayer && person.id != ComesMaya && person.id != ComesJamie)
                .Select(person => person.id).ToList();
            state.phase = EpisodePhase.Campaign;
            state.hohId = others[0];
            state.nominees = new List<string> { ComesMaya, ComesJamie };
            state.vetoHolderId = others[0];
            state.vetoPlayers = new[] { others[0], ComesMaya, ComesJamie }.Concat(state.Active.Select(person => person.id)).Distinct()
                .Take(EpisodeEngine.VetoPlayerCount(state.Active.Count())).ToList();
            state.vetoResolved = true;
        }

        /// <summary>
        /// The reply house saved and reloaded, the reply cards' switch set to <paramref name="replies"/>, asks on and taken
        /// effect with the house's pairing held (TurnAsksOnInTheLoadedHouse). No visit waited for.
        /// </summary>
        private IEnumerator InstallComesToYouReplyHouse(string kind, bool withOffers, bool? replies = true, bool walkOvers = true,
            string secondSender = null)
        {
            comesReplyCardId = SaveComesToYouReplyState(kind, withOffers, secondSender);
            yield return ReloadEpisode();
            director.ReplyAsksEnabled = replies;
            yield return TurnAsksOnInTheLoadedHouse(new string[0], true);
            director.AskWalkOvers = walkOvers;
            Assert.That(director.Snapshot.replyCards.Select(card => card.id), Does.Contain(comesReplyCardId), "The card survived the load.");
        }

        /// <summary><see cref="InstallComesToYouReplyHouse"/>, then the first visit reserved: Maya's, for her card.</summary>
        private IEnumerator InstallComesToYouReplyVisit(string kind, bool withOffers)
        {
            yield return InstallComesToYouReplyHouse(kind, withOffers);
            yield return WaitForTheFirstVisit(new string[0]);
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(comesReplyCardId), "Maya comes over with her card first.");
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesMaya));
        }

        /// <summary>Holds the line up (the auto-open waits) until the bubble shows; returns it.</summary>
        private IEnumerator WaitForTheReplyLine(List<HouseNpcInvitationBubble> shown)
        {
            float deadline = Time.realtimeSinceStartup + 35f;
            while (!director.NpcInvitationIsVisible && Time.realtimeSinceStartup < deadline)
            {
                NpcWrite("askSpokeFor", 0f);
                yield return null;
            }
            NpcWrite("askSpokeFor", 0f);
            Assert.That(director.NpcInvitationIsVisible, Is.True, "Maya's line is up.");
            shown.Add(NpcRead<HouseNpcInvitationBubble>("npcInvitationBubble"));
        }

        // ------------------------------------------------------------------ R1. a confrontation walks up

        [UnityTest]
        public IEnumerator ComesToYou_ReplyAConfrontationWalksUpAndOpensAsADialogue() => PinnedClock(AConfrontationWalksUpHeld);

        private IEnumerator AConfrontationWalksUpHeld() =>
            WithTheHouseHeld(() => InstallComesToYouReplyVisit(ReplyCards.Confrontation, true), AConfrontationWalksUp);

        private IEnumerator AConfrontationWalksUp()
        {
            string cardId = comesReplyCardId;
            var shown = new List<HouseNpcInvitationBubble>();
            yield return WaitForTheReplyLine(shown);
            var bubble = shown.Single();
            Assert.That(bubble.CurrentText, Is.EqualTo(ComesToYou.ConfrontationLine), "Maya says the card's opening line.");
            Assert.That(bubble.HearControl.name, Is.EqualTo(HouseNpcInvitationBubble.ReplyHearCaption), "The bubble's yes is the card's own caption,");
            Assert.That(bubble.NotNowControl.name, Is.EqualTo(HouseNpcInvitationBubble.ReplyNotNowCaption), "and its no.");
            Assert.That(FindButton(HouseNpcInvitationBubble.ReplyHearCaption), Is.SameAs(bubble.HearControl), "One control wears the yes.");
            Assert.That(FindButton(HouseNpcInvitationBubble.ReplyNotNowCaption), Is.SameAs(bubble.NotNowControl), "One control wears the no.");
            Assert.That(ButtonWithCaptionOrNull(HouseNpcInvitationBubble.HearCaption), Is.Null, "Not the offer's \"Hear their offer\".");

            // The line held long enough: let the dialogue open by itself.
            yield return WaitForAskDialogue(ComesMaya);
            Assert.That(ComesHud.PointerHeld, Is.True, "Pointer presses are held off the panel as it opens.");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.WalkOver));
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(cardId));
            Assert.That(director.InvitedDealRecordId, Is.EqualTo(cardId));
            Assert.That(director.CurrentConversationCategory, Is.EqualTo(EpisodeDirector.DealsCategoryCaption));
            Assert.That(director.IsAskSetAside(cardId), Is.True);
            yield return null; yield return null;
            Assert.That(SelectedName(), Is.EqualTo(EpisodeHud.ThinkItOverCaption), "The keyboard opens on the way out that answers nothing.");
            yield return WaitForBodies();

            var state = director.Snapshot;
            var card = state.replyCards.Single(row => row.id == cardId);
            Assert.That(LastLabel(EpisodeHud.AskLineName)?.text, Is.EqualTo("\"" + ComesToYou.ConfrontationLine + "\""));
            Assert.That(ShownText(), Does.Contain(EpisodeHud.AskWalkOverEyebrow("Maya")));
            Assert.That(ShownText(), Does.Contain(ReplyCards.Title(state, card)), "The card's title is the heading.");
            Assert.That(LastLabel(EpisodeHud.AskTermsName)?.text, Is.EqualTo(ReplyCards.Message(state, card)), "The card's words are the terms.");
            Assert.That(LastLabel(EpisodeHud.AskLapseName)?.text, Is.EqualTo(ComesToYou.LapseFreeTimeEnds));
            foreach (var reply in ReplyCards.Replies(ReplyCards.Confrontation))
                Assert.That(FindButton(EpisodeHud.ReplyCaption(reply.Label)), Is.Not.Null, reply.Label); // Single: exactly one.
            Assert.That(FindButton(EpisodeHud.ThinkItOverCaption), Is.Not.Null);
            Assert.That(FindButton(EpisodeDirector.ConversationTopicsBackCaption), Is.Not.Null);
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.DealAcceptCaption), Is.Null, "A card is no offer: no Accept,");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.DealDeclineCaption), Is.Null, "and no Turn down.");

            yield return WaitOutThePointerHold();
            var apologize = ButtonWithCaption(EpisodeHud.ReplyCaption("Apologize"));
            Assert.That(ComesHud.TryReadOfferControl(apologize, out var reading), Is.True, "A screen reader says the answer,");
            Assert.That(reading, Does.Contain(state.Find(ComesMaya).name).And.Contain(ReplyCards.Title(state, card)), "who asks and the card's title.");
            Assert.That(director.Snapshot.replyCards.Any(row => row.id == cardId), Is.True, "Hearing the card does not answer it.");

            var justBefore = director.Snapshot;
            apologize.onClick.Invoke();
            var answered = director.Snapshot;
            Assert.That(answered.revision, Is.EqualTo(justBefore.revision + 1));
            Assert.That(answered.acceptedCommandIds.Count, Is.EqualTo(justBefore.acceptedCommandIds.Count + 1));
            Assert.That(answered.replyCards.Any(row => row.id == cardId), Is.False, "The card is answered.");
            Assert.That(answered.socialActions, Is.EqualTo(justBefore.socialActions), "Answering is free.");
            var expected = new EpisodeEngine(justBefore).Apply(new EpisodeCommand
            {
                id = answered.acceptedCommandIds.Last(), actorId = justBefore.playerId,
                expectedPhase = justBefore.phase, expectedRevision = justBefore.revision,
                kind = EpisodeCommandKind.ReplyToHouseguest, targetId = cardId, text = "apologize",
            });
            Assert.That(expected.accepted, Is.True, expected.reason);
            AssertEquivalent(expected.state, answered);

            // The dialogue let go: still talking to Maya, off the Deals page, the answer's own line said.
            yield return null;
            Assert.That(director.TalkingToId, Is.EqualTo(ComesMaya));
            Assert.That(director.CurrentConversationCategory, Is.Null, "Off the Deals page, which invites nothing for a card.");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None));
            Assert.That(director.CurrentAskRecordId, Is.Null);
            Assert.That(ShownText(), Does.Contain("WHAT HAPPENED"), "What happened, as the conversation says it after any answer.");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ReplyCaption("Apologize")), Is.Null, "Nothing left to answer.");
            yield return WaitOutThePointerHold();
            ButtonWithCaption(EpisodeDirector.ConversationTopicsBackCaption).onClick.Invoke();
            Assert.That(director.CurrentConversationCategory, Is.Null);
            Assert.That(FindButton(EpisodeDirector.DealsCategoryCaption), Is.Not.Null, "Back to topics: the wheel.");
        }

        // ------------------------------------------------------------------ R3. gossip comes from the gossiper

        [UnityTest]
        public IEnumerator ComesToYou_ReplyGossipComesFromTheGossiper() => PinnedClock(GossipFromTheGossiperHeld);

        private IEnumerator GossipFromTheGossiperHeld() =>
            WithTheHouseHeld(() => InstallComesToYouReplyVisit(ReplyCards.Gossip, false), GossipFromTheGossiper);

        private IEnumerator GossipFromTheGossiper()
        {
            string cardId = comesReplyCardId;
            var card = director.Snapshot.replyCards.Single(row => row.id == cardId);
            Assert.That((card.fromId, card.aboutId), Is.EqualTo((ComesMaya, ComesJamie)), "Maya talked to Jamie about the player.");
            yield return WaitForAskDialogue(ComesMaya);
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesMaya), "The gossiper comes, whom all three answers address.");
            Assert.That(LastLabel(EpisodeHud.AskLineName)?.text, Is.EqualTo("\"" + ComesToYou.GossipLine + "\""));
            yield return WaitOutThePointerHold();
            var justBefore = director.Snapshot;
            ButtonWithCaption(EpisodeHud.ReplyCaption("Gossip back")).onClick.Invoke();
            var answered = director.Snapshot;
            Assert.That(answered.acceptedCommandIds.Count, Is.EqualTo(justBefore.acceptedCommandIds.Count + 1));
            var expected = new EpisodeEngine(justBefore).Apply(new EpisodeCommand
            {
                id = answered.acceptedCommandIds.Last(), actorId = justBefore.playerId,
                expectedPhase = justBefore.phase, expectedRevision = justBefore.revision,
                kind = EpisodeCommandKind.ReplyToHouseguest, targetId = cardId, text = "gossip-back",
            });
            Assert.That(expected.accepted, Is.True, expected.reason);
            AssertEquivalent(expected.state, answered);
        }

        // ------------------------------------------------------------------ R4. talking to the sender raises the card

        [UnityTest]
        public IEnumerator ComesToYou_ReplyTalkingToTheSenderRaisesTheCard() => PinnedClock(TalkingToTheSenderHeld);

        private IEnumerator TalkingToTheSenderHeld() =>
            WithTheHouseHeld(() => InstallComesToYouReplyVisit(ReplyCards.Confrontation, false), TalkingToTheSender);

        private IEnumerator TalkingToTheSender()
        {
            string cardId = comesReplyCardId;
            yield return WaitForTheLineWithNoSecondDue();
            Assert.That(director.TryOpenNpc(ComesMaya), Is.True);
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.Raised));
            Assert.That(director.InvitedDealRecordId, Is.EqualTo(cardId));
            Assert.That(ComesHud.PointerHeld, Is.True);
            yield return null; yield return null;
            Assert.That(SelectedName(), Is.EqualTo(EpisodeDirector.ConversationTopicsBackCaption), "Raised: the keyboard on Back to topics.");
            Assert.That(ShownText(), Does.Contain(EpisodeHud.AskCardRaisedEyebrow("Maya")), "MAYA WANTS A WORD,");
            Assert.That(ShownText(), Does.Not.Contain(EpisodeHud.AskRaisedEyebrow("Maya")), "never an offer she has not made.");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ThinkItOverCaption), Is.Null, "A raised dialogue has no Think it over.");

            yield return WaitOutThePointerHold();
            ButtonWithCaption(EpisodeDirector.ConversationTopicsBackCaption).onClick.Invoke();
            Assert.That(director.CurrentConversationCategory, Is.Null, "Back to topics: the wheel.");
            Assert.That(director.Snapshot.replyCards.Any(row => row.id == cardId), Is.True, "Going back answers nothing.");
            director.ClosePanels();
            yield return null;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (NpcWholeSecondDue() && Time.realtimeSinceStartup < deadline) yield return null;
            yield return OpenNearbyNpc(SceneComponents<HouseNpc>().Single(npc => npc.Id == ComesMaya));
            Assert.That(director.CurrentConversationCategory, Is.Null, "Raised once a phase: the wheel the second time.");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None));
        }

        // ------------------------------------------------------------------ R6. a card comes with no offer in the house

        [UnityTest]
        public IEnumerator ComesToYou_ReplyACardComesWithNoOfferInTheHouse() => PinnedClock(ACardWithNoOfferHeld);

        private IEnumerator ACardWithNoOfferHeld() =>
            WithTheHouseHeld(() => InstallComesToYouReplyHouse(ReplyCards.Confrontation, false), ACardWithNoOffer);

        private IEnumerator ACardWithNoOffer()
        {
            Assert.That(NpcInvitation.Available(director.Snapshot), Is.Empty, "The premise: no offer a houseguest could raise.");
            Assert.That(ComesToYou.Read(director.Snapshot).Select(ask => ask.recordId), Is.EqualTo(new[] { comesReplyCardId }));
            yield return WaitForTheFirstVisit(new string[0]);
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(comesReplyCardId), "Maya still walks up with her card.");
            yield return WaitForAskDialogue(ComesMaya);
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(comesReplyCardId));
        }

        // ------------------------------------------------------------------ R7. the card host shows the outcome

        [UnityTest]
        public IEnumerator ComesToYou_ReplyTheCardHostShowsTheOutcome() => PinnedClock(TheCardHostShowsTheOutcomeHeld);

        private IEnumerator TheCardHostShowsTheOutcomeHeld() =>
            WithTheHouseHeld(() => InstallComesToYouReplyHouse(ReplyCards.Confrontation, false, true, false), TheCardHostShowsTheOutcome);

        private IEnumerator TheCardHostShowsTheOutcome()
        {
            string cardId = comesReplyCardId;
            yield return WaitForAskCardUp("the reply card");
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(cardId), "The card host carries Maya's card.");
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesMaya));
            yield return WaitForBodies();
            var state = director.Snapshot;
            var card = state.replyCards.Single(row => row.id == cardId);
            Assert.That(ShownText(), Does.Contain(EpisodeHud.AskDirectEyebrow));
            Assert.That(ShownText(), Does.Contain(ReplyCards.Title(state, card)));
            Assert.That(LastLabel(EpisodeHud.AskLineName)?.text, Is.EqualTo("\"" + ComesToYou.ConfrontationLine + "\""));
            foreach (var reply in ReplyCards.Replies(ReplyCards.Confrontation))
                Assert.That(FindButton(EpisodeHud.ReplyCaption(reply.Label)), Is.Not.Null, reply.Label);
            Assert.That(FindButton(EpisodeHud.ThinkItOverCaption), Is.Not.Null);

            yield return WaitOutThePointerHold();
            var justBefore = director.Snapshot;
            ButtonWithCaption(EpisodeHud.ReplyCaption("Deflect")).onClick.Invoke();
            var answered = director.Snapshot;
            Assert.That(answered.acceptedCommandIds.Count, Is.EqualTo(justBefore.acceptedCommandIds.Count + 1));
            Assert.That(answered.replyCards.Any(row => row.id == cardId), Is.False);
            var expected = new EpisodeEngine(justBefore).Apply(new EpisodeCommand
            {
                id = answered.acceptedCommandIds.Last(), actorId = justBefore.playerId,
                expectedPhase = justBefore.phase, expectedRevision = justBefore.revision,
                kind = EpisodeCommandKind.ReplyToHouseguest, targetId = cardId, text = "deflect",
            });
            Assert.That(expected.accepted, Is.True, expected.reason);
            AssertEquivalent(expected.state, answered);

            Assert.That(director.IsAskCardOpen, Is.True, "The card stays up for the outcome.");
            Assert.That(LastLabel(EpisodeHud.AskOutcomeName)?.text,
                Is.EqualTo(EpisodeDirector.EventLine(answered, answered.events.Last(entry => entry.kind == "reply"))), "The answer's own line.");
            Assert.That(ShownText(), Does.Not.Contain(EpisodeHud.AskGoneLine), "Never the offer's \"no longer on the table\".");
            Assert.That(ShownText(), Does.Not.Contain(EpisodeHud.AskCardGoneLine), "It was answered, not gone.");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ThinkItOverCaption), Is.Null, "Nothing is left to think over.");
            ButtonWithCaption(EpisodeDirector.SceneCardDoneCaption).onClick.Invoke();
            Assert.That(director.IsAskCardOpen, Is.False);
            Assert.That(director.IsPanelOpen, Is.False, "Back to the house closes the card.");
        }

        // ------------------------------------------------------------------ R8. off with the switch, and by default

        [UnityTest]
        public IEnumerator ComesToYou_ReplyOffWithTheSwitchAndByDefault() => PinnedClock(OffWithTheSwitchHeld);

        private IEnumerator OffWithTheSwitchHeld() =>
            WithTheHouseHeld(() => InstallComesToYouReplyHouse(ReplyCards.Confrontation, true, false), OffWithTheSwitch);

        private IEnumerator OffWithTheSwitch()
        {
            Assert.That(director.ReplyAsksEnabled, Is.False);
            Assert.That(ComesToYou.Read(director.Snapshot).First().recordId, Is.EqualTo(comesReplyCardId), "The reader still lists the card first.");
            yield return WaitForTheFirstVisit(new string[0]);
            string recordId = director.CurrentNpcInvitationRecordId;
            Assert.That(recordId, Is.Not.EqualTo(comesReplyCardId), "With the switch off the card does not come,");
            Assert.That(NpcDeals.Pending(director.Snapshot).Select(row => row.id), Does.Contain(recordId), "an offer does, as in slices 1-3.");
            director.ReplyAsksEnabled = null;
            Assert.That(director.AsksAreOn, Is.True, "Null follows the asks' switch, which the test turned on.");

            // With the asks at the fixture's default (off), nothing comes, and the free-time screen still draws the card.
            comesReplyCardId = SaveComesToYouReplyState(ReplyCards.Confrontation, false);
            yield return ReloadEpisode();
            Assert.That(director.AsksEnabled, Is.False, "The fixture turns asks off as it binds the director.");
            Assert.That(director.ReplyAsksEnabled, Is.Null, "The reply cards' switch follows it by default.");
            yield return WaitForBodies();
            float until = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < until)
            {
                Assert.That(director.TalkingToId, Is.Null, "Nothing opens by itself with asks off.");
                Assert.That(director.IsAskCardOpen, Is.False, "and no card comes.");
                Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, "Nobody walks up: the invitation carries offers only.");
                yield return null;
            }
            yield return OpenStation();
            Assert.That(ShownText(), Does.Contain(EpisodeHud.ReplyCardEyebrow), "The free-time screen still draws the card,");
            var state = director.Snapshot;
            Assert.That(ShownText(), Does.Contain(ReplyCards.Title(state, state.replyCards.Single(row => row.id == comesReplyCardId))), "Maya's.");
            director.ClosePanels();
        }

        // ================================================================== slice 4a's leftovers, built in 4p (the lead's ruling 4)

        /// <summary>The advance from where the season stands, as the episode screen's "Continue episode" commits it: free time ends, and every card goes.</summary>
        private CommandResult AdvanceTheSeason()
        {
            var state = director.Snapshot;
            var result = director.Submit(new EpisodeCommand
            {
                id = "comes-to-you-reply-advance-" + state.revision, actorId = state.playerId, expectedPhase = state.phase,
                expectedRevision = state.revision, kind = EpisodeCommandKind.Advance,
            });
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(director.Snapshot.replyCards.Any(row => row.id == comesReplyCardId), Is.False, "The premise: the card went, unanswered.");
            return result;
        }

        // ------------------------------------------------------------------ R2. a plea walks up in the campaign

        [UnityTest]
        public IEnumerator ComesToYou_ReplyAPleaWalksUpInTheCampaign() => PinnedClock(APleaInTheCampaignHeld);

        private IEnumerator APleaInTheCampaignHeld() =>
            WithTheHouseHeld(() => InstallComesToYouReplyVisit(ReplyCards.Plea, false), APleaInTheCampaign);

        private IEnumerator APleaInTheCampaign()
        {
            string cardId = comesReplyCardId;
            var installed = director.Snapshot;
            var card = installed.replyCards.Single(row => row.id == cardId);
            Assert.That(installed.phase, Is.EqualTo(EpisodePhase.Campaign), "The campaign, where a plea waits.");
            Assert.That((card.kind, card.fromId, card.aboutId), Is.EqualTo((ReplyCards.Plea, ComesMaya, ComesJamie)), "Maya pleads, Jamie the other nominee.");
            yield return WaitForAskDialogue(ComesMaya);
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.WalkOver));
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(cardId));
            yield return WaitForBodies();
            Assert.That(LastLabel(EpisodeHud.AskLineName)?.text, Is.EqualTo("\"" + ComesToYou.PleaLine + "\""));
            Assert.That(LastLabel(EpisodeHud.AskLapseName)?.text, Is.EqualTo(ComesToYou.LapseCampaignCloses));
            foreach (var reply in ReplyCards.Replies(ReplyCards.Plea))
                Assert.That(FindButton(EpisodeHud.ReplyCaption(reply.Label)), Is.Not.Null, reply.Label);

            yield return WaitOutThePointerHold();
            var justBefore = director.Snapshot;
            ButtonWithCaption(EpisodeHud.ReplyCaption(ReplyCards.Find(ReplyCards.Plea, "promise").Label)).onClick.Invoke();
            // The campaign board's own press: ReplyToHouseguest with the card's id and the key "promise".
            AssertIntentCommand(justBefore, EpisodeCommandKind.ReplyToHouseguest, cardId, "promise");
            Assert.That(director.Snapshot.socialActions, Is.EqualTo(justBefore.socialActions), "Answering is free.");
            director.ClosePanels();
        }

        // ------------------------------------------------------------------ R5. two cards, one at a time

        [UnityTest, Timeout(300000)]
        public IEnumerator ComesToYou_ReplyOnlyTheCardWaitingFirstComes() => PinnedClock(OnlyTheFirstCardHeld);

        private IEnumerator OnlyTheFirstCardHeld() =>
            WithTheHouseHeld(() => InstallComesToYouReplyHouse(ReplyCards.Confrontation, false, secondSender: ComesJamie), OnlyTheFirstCard);

        private IEnumerator OnlyTheFirstCard()
        {
            string mayaCard = comesReplyCardId;
            string jamieCard = director.Snapshot.replyCards.Single(row => row.fromId == ComesJamie).id;
            Assert.That(ComesToYou.Read(director.Snapshot).Select(ask => (ask.source, ask.recordId)),
                Is.EqualTo(new[] { (ComesToYou.Source.Card, mayaCard) }), "Two cards wait; only the first is listed.");
            yield return WaitForTheFirstVisit(new string[0]);
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(mayaCard), "Maya, whose card waits first, comes.");
            yield return WaitForAskDialogue(ComesMaya);
            yield return WaitOutThePointerHold();
            var justBefore = director.Snapshot;
            ButtonWithCaption(EpisodeHud.ReplyCaption("Apologize")).onClick.Invoke();
            AssertIntentCommand(justBefore, EpisodeCommandKind.ReplyToHouseguest, mayaCard, "apologize");
            Assert.That(ComesToYou.Read(director.Snapshot).Select(ask => (ask.source, ask.recordId)),
                Is.EqualTo(new[] { (ComesToYou.Source.Card, jamieCard) }), "Answered, Jamie's card is the one waiting first.");
            director.ClosePanels();
            // The closed dialogue starts the gap on the next tick; then the gap is released.
            yield return null; yield return null;
            NpcWrite("askNextAt", 0f);
            yield return WaitForTheFirstVisit(new string[0]);
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(jamieCard), "Jamie comes with the next card.");
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesJamie));
        }

        // ------------------------------------------------------------------ R9. the dialogue fits at Largest

        [UnityTest]
        public IEnumerator ComesToYou_ReplyTheDialogueFitsAtLargest() => PinnedClock(TheReplyFitsAtLargestHeld);

        private IEnumerator TheReplyFitsAtLargestHeld() =>
            WithTheHouseHeld(() => InstallComesToYouReplyAtLargest(), TheReplyFitsAtLargest);

        /// <summary>The reply house, then Largest chosen as a player chooses it (the reload restores the default size), before any visit.</summary>
        private IEnumerator InstallComesToYouReplyAtLargest()
        {
            yield return InstallComesToYouReplyHouse(ReplyCards.Confrontation, false);
            yield return ApplyTextSize(TextSize.Largest);
            Assert.That(director.TextSize, Is.EqualTo(TextSize.Largest), "The size took, so this pass is not vacuous.");
        }

        private IEnumerator TheReplyFitsAtLargest()
        {
            yield return WaitForAskDialogue(ComesMaya, 45f);
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(comesReplyCardId));
            yield return WaitForBodies();
            yield return WaitOutThePointerHold();
            Assert.That(director.TextSize, Is.EqualTo(TextSize.Largest), "Still at Largest.");
            AssertEveryLabelDraws("The reply dialogue at Largest");
            foreach (var name in new[] { EpisodeHud.AskLineName, EpisodeHud.AskStandingName, EpisodeHud.AskTermsName, EpisodeHud.AskLapseName })
            {
                var label = LastLabel(name);
                Assert.That(label, Is.Not.Null, name + " is drawn.");
                Canvas.ForceUpdateCanvases();
                Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.fontSize * 1.3f - .5f), name + "'s box.");
                Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(12f), name + " at 1080p.");
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f), name + " fits.");
            }
            // At 1920x1080 (the HUD's 1600x900 canvas) the card's three answers sit inside the viewport as the dialogue opens.
            yield return AtFrame(1600, 900, () =>
            {
                foreach (var reply in ReplyCards.Replies(ReplyCards.Confrontation))
                    AssertInsideItsViewport(FindButton(EpisodeHud.ReplyCaption(reply.Label)), "At Largest, 1920x1080: " + reply.Label);
            });
            director.ClosePanels();
        }

        // ------------------------------------------------------------------ a card gone without an answer, in the conversation

        [UnityTest]
        public IEnumerator ComesToYou_ReplyACardGoneInTheConversationSaysWhatHappened() => PinnedClock(ACardGoneInTheConversationHeld);

        private IEnumerator ACardGoneInTheConversationHeld() =>
            WithTheHouseHeld(() => InstallComesToYouReplyVisit(ReplyCards.Confrontation, false), ACardGoneInTheConversation);

        /// <summary>
        /// The lead's ruling 1: once the card has gone - here before any answer, free time ending under the open dialogue - the
        /// conversation leaves the Deals page and says what happened, with Back to topics; nothing is left to answer.
        /// </summary>
        private IEnumerator ACardGoneInTheConversation()
        {
            yield return WaitForAskDialogue(ComesMaya);
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(comesReplyCardId));
            yield return WaitOutThePointerHold();
            AdvanceTheSeason();
            // The commit's own render.
            Assert.That(director.TalkingToId, Is.EqualTo(ComesMaya), "Still with Maya.");
            Assert.That(director.CurrentConversationCategory, Is.Null, "Off the Deals page, which invites nothing for a card.");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None), "The dialogue is let go.");
            Assert.That(director.CurrentAskRecordId, Is.Null);
            foreach (var reply in ReplyCards.Replies(ReplyCards.Confrontation))
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ReplyCaption(reply.Label)), Is.Null, "Nothing left to answer: " + reply.Label);
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ThinkItOverCaption), Is.Null, "Nothing left to think over.");
            Assert.That(FindButton(EpisodeDirector.ConversationTopicsBackCaption), Is.Not.Null, "Back to topics.");
            director.ClosePanels();
            yield return null;
        }

        // ------------------------------------------------------------------ the card host's "They've moved on."

        [UnityTest]
        public IEnumerator ComesToYou_ReplyTheCardHostSaysTheyMovedOn() => PinnedClock(TheCardHostSaysTheyMovedOnHeld);

        private IEnumerator TheCardHostSaysTheyMovedOnHeld() =>
            WithTheHouseHeld(() => InstallComesToYouReplyHouse(ReplyCards.Confrontation, false, true, false), TheCardHostSaysTheyMovedOn);

        private IEnumerator TheCardHostSaysTheyMovedOn()
        {
            yield return WaitForAskCardUp("the reply card");
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(comesReplyCardId), "The card host carries Maya's card.");
            yield return WaitForBodies();
            yield return WaitOutThePointerHold();
            AdvanceTheSeason();
            // The commit's own render: the card went before any answer.
            Assert.That(director.IsAskCardOpen, Is.True, "The card stays up to say so.");
            Assert.That(ShownText(), Does.Contain(EpisodeHud.AskCardGoneLine), "\"They've moved on.\"");
            Assert.That(EpisodeHud.AskCardGoneLine, Is.EqualTo("They've moved on."));
            Assert.That(ShownText(), Does.Not.Contain(EpisodeHud.AskGoneLine), "Never the offer's \"no longer on the table\".");
            Assert.That(LastLabel(EpisodeHud.AskOutcomeName), Is.Null, "Nothing was answered, so no outcome.");
            foreach (var reply in ReplyCards.Replies(ReplyCards.Confrontation))
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ReplyCaption(reply.Label)), Is.Null, "Nothing left to answer: " + reply.Label);
            FindButton(EpisodeDirector.SceneCardDoneCaption).onClick.Invoke();
            Assert.That(director.IsAskCardOpen, Is.False, "Back to the house closes the card.");
            yield return null;
        }

        // ------------------------------------------------------------------ the review's two notes

        /// <summary>
        /// Slices 1-3 read today's queue with the reply cards' switch at its default: the invitation flow's own saved house -
        /// as installed, and with Taylor's third ask - carries no reply card, so nothing a card or a pitch adds reaches their
        /// queue; and a freshly bound director's reply switch is null, following asks, which the fixture turns off.
        /// </summary>
        [UnityTest]
        public IEnumerator ComesToYou_ReplyNoCardInSliceOnesHouseAndTheSwitchFollowsByDefault()
        {
            Assert.That(director.AsksEnabled, Is.False, "The fixture turns asks off as it binds the director.");
            Assert.That(director.ReplyAsksEnabled, Is.Null, "A freshly bound director's reply switch follows the asks.");
            foreach (bool threeAsks in new[] { false, true })
            {
                SaveNpcInvitationFlowState(threeAsks);
                Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var saved, out string error), Is.True, error);
                Assert.That(saved.replyCards, Is.Empty, "The invitation flow's saved house carries no reply card (three asks: " + threeAsks + ").");
                Assert.That(ComesToYou.Read(saved).Select(ask => ask.source), Has.None.EqualTo(ComesToYou.Source.Card).And.None.EqualTo(ComesToYou.Source.Pitch),
                    "Its queue is the offers' alone.");
            }
            yield break;
        }
    }
}
