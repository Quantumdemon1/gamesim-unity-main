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
    /// Offers slice 4p (OFFERS-COME-TO-YOU-PLAN §C.4, source Pitch; §H's PlayMode list P1-P5): when the player is Head of
    /// Household, the houseguests pitching them walk up before the nominations, one at a time in the board's order, and
    /// "Hear their pitch" opens the nomination screen on the pitches - the role moments' board, every card in its dealt order
    /// and unchanged, or before them the one-at-a-time answer - where every answer sends the command it always sent. Hearing
    /// and "Let it wait" commit nothing; a pitcher who cannot come gets no card, and the nomination screen's "Hear houseguest
    /// pitches" stays the way in.
    ///
    /// <para>Every test opts in after the fixture's reload turns asks off (AsksEnabled and ReplyAsksEnabled set explicitly),
    /// pins scaled time with PinnedClock, holds the house's pairing as slice 1's tests do and never ticks the NPC world by
    /// hand. The house is past free time, so its world is built for the test (BuildNpcWorldForDiagnostics). The season with
    /// the board writes story rules Roles by name; the one without it has no story at all; neither leans on
    /// StoryRules.Current.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The pitches <see cref="SaveComesToYouPitchState"/> left waiting, in <c>s.replyCards</c> order: the board's.</summary>
        private string[] comesPitchIds = new string[0];

        /// <summary>
        /// The player Head of Household in the nomination before naming anybody, with no pact and no offer, five
        /// houseguests' pitches filed as <see cref="HoHPitches.Offer"/> files a courting houseguest's (its id and its
        /// recommendation), read by the player at five different scores. With <paramref name="board"/>, the story at Roles,
        /// written by name, and the board kept as the engine keeps it as the nominations open (<see cref="HoHPitches.HoldTheBoard"/>,
        /// CourtTheHoH's own step): three, the two the player reads warmest and the one they read coolest, in that order -
        /// not the filing order. Without it, no story: every pitch waits, answered one at a time. Saved.
        /// </summary>
        private void SaveComesToYouPitchState(bool board)
        {
            var state = FullHouse(4401, 8);
            state.strategyRulesStartWeek = 1;
            state.deals.Clear();
            EpisodeEngine.EnableWeek(state);
            EpisodeEngine.EnableLevers(state);
            EpisodeEngine.EnableCommitments(state);
            EpisodeEngine.EnableAgency(state);
            if (board)
            {
                EpisodeEngine.EnableStory(state);
                state.story.rulesVersion = StoryRules.Roles;
            }
            state.economyRulesVersion = 1;
            state.phase = EpisodePhase.Nomination;
            state.hohId = state.playerId;
            state.nominees.Clear();
            state.houseEvents.Clear();
            state.replyCards.Clear();
            state.alliances.RemoveAll(pact => pact.members.Contains(state.playerId));
            var npcs = state.Active.Where(person => !person.isPlayer).Take(5).ToArray();
            // The board keeps the third (read warmest), the second, then the first (read coolest): against the cast's order.
            var readings = new[] { -30.0, 10.0, 20.0, -5.0, 0.0 };
            for (int i = 0; i < npcs.Length; i++)
            {
                var edge = state.relationships.FirstOrDefault(r => r.fromId == state.playerId && r.toId == npcs[i].id);
                if (edge == null) state.relationships.Add(edge = new RelationshipState { fromId = state.playerId, toId = npcs[i].id });
                edge.score = readings[i];
            }
            foreach (var npc in npcs)
            {
                string target = ThreatAssessment.RankedTargets(state, npc.id)
                    .FirstOrDefault(other => other != state.playerId && !state.Allied(npc.id, other));
                state.replyCards.Add(new ReplyCardState { id = "reply-pitch-" + state.week + "-" + npc.id, week = state.week,
                    kind = ReplyCards.Pitch, fromId = npc.id, aboutId = target });
            }
            HoHPitches.HoldTheBoard(state);
            Assert.That(HoHPitches.Available(state), Is.True, "The premise: the player Head of Household hears pitches.");
            Assert.That(HoHPitches.OnTheBoard(state), Is.EqualTo(board), board ? "The board, under Roles written by name." : "No board: no story.");
            if (board)
            {
                Assert.That(state.replyCards.Select(card => card.fromId), Is.EqualTo(HoHPitches.BoardPicks(state, npcs.Select(npc => npc.id))),
                    "The board's three, in its order.");
                Assert.That(state.replyCards.Select(card => card.fromId), Is.Not.EqualTo(state.replyCards.Select(card => card.fromId).OrderBy(id => id, System.StringComparer.Ordinal)),
                    "and that order is not the ids', so the walk-ups' order is the board's own.");
            }
            else Assert.That(state.replyCards, Has.Count.EqualTo(npcs.Length), "Every pitch waits, one at a time.");
            Assert.That(ComesToYou.Read(state).Select(ask => ask.recordId),
                Is.EqualTo(board ? state.replyCards.Select(card => card.id) : new[] { state.replyCards[0].id }), "The reader lists the pitches that come.");
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
            comesPitchIds = state.replyCards.Select(card => card.id).ToArray();
            new EpisodeSaveStore(director.SavePath).Save(state);
        }

        /// <summary>
        /// The pitch house saved and reloaded, its world built (the house is past free time), the reply switch on, asks on
        /// and taken effect with the house's pairing held (TurnAsksOnInTheLoadedHouse). No visit waited for.
        /// </summary>
        private IEnumerator InstallComesToYouPitchHouse(bool board, bool walkOvers = true)
        {
            SaveComesToYouPitchState(board);
            yield return ReloadEpisode();
            director.BuildNpcWorldForDiagnostics();
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "The house's world is built.");
            director.ReplyAsksEnabled = true;
            yield return TurnAsksOnInTheLoadedHouse(new string[0], true);
            director.AskWalkOvers = walkOvers;
            Assert.That(director.Snapshot.replyCards.Select(card => card.id), Is.EqualTo(comesPitchIds), "The pitches survived the load.");
        }

        /// <summary>The pitch waiting with this id in the installed season.</summary>
        private ReplyCardState ComesPitch(string id) => director.Snapshot.replyCards.Single(card => card.id == id);

        /// <summary>The first visit, which must be the first pitch's (the board's first, or the one waiting first); returns its bubble once the line is up.</summary>
        private IEnumerator WaitForThePitchLine(string pitchId, List<HouseNpcInvitationBubble> shown)
        {
            yield return WaitForTheFirstVisit(new string[0]);
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(pitchId), "The pitch listed first comes first.");
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesPitch(pitchId).fromId), "Its pitcher walks up.");
            Assert.That(director.IsAskCardOpen, Is.False, "In person, not as a card.");
            yield return WaitForTheReplyLine(shown);
        }

        /// <summary>The bubble a pitcher says their line in: the pitch's line and its own two captions, on the bubble's two controls.</summary>
        private void AssertThePitchBubble(HouseNpcInvitationBubble bubble)
        {
            Assert.That(bubble.CurrentText, Is.EqualTo(ComesToYou.PitchLine), "The pitcher says the pitch's line.");
            Assert.That(bubble.HearControl.name, Is.EqualTo(HouseNpcInvitationBubble.PitchHearCaption), "The bubble's yes is the pitch's own caption,");
            Assert.That(bubble.NotNowControl.name, Is.EqualTo(HouseNpcInvitationBubble.PitchNotNowCaption), "and its no.");
            Assert.That(FindButton(HouseNpcInvitationBubble.PitchHearCaption), Is.SameAs(bubble.HearControl), "One control wears the yes.");
            Assert.That(FindButton(HouseNpcInvitationBubble.PitchNotNowCaption), Is.SameAs(bubble.NotNowControl), "One control wears the no.");
            Assert.That(ButtonWithCaptionOrNull(HouseNpcInvitationBubble.HearCaption), Is.Null, "Not the offer's \"Hear their offer\",");
            Assert.That(ButtonWithCaptionOrNull(HouseNpcInvitationBubble.ReplyHearCaption), Is.Null, "nor a card's \"Hear what they want\".");
        }

        /// <summary>
        /// Presses Hear on the bubble: the nomination's own panel opens on the pitches, nothing is committed or written, no
        /// conversation or ask dialogue opens, and the walker's pitch is set aside for the phase.
        /// </summary>
        private IEnumerator HearThePitch(HouseNpcInvitationBubble bubble, string pitchId)
        {
            var before = director.Snapshot;
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            bubble.HearControl.onClick.Invoke();
            // The press frame.
            Assert.That(director.IsPhasePanelOpen, Is.True, "Hear opens the nomination's own panel.");
            Assert.That(director.TalkingToId, Is.Null, "A pitch opens no conversation,");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None), "and no ask dialogue.");
            Assert.That(director.IsAskCardOpen, Is.False);
            Assert.That(director.IsAskSetAside(pitchId), Is.True, "The walker's pitch is set aside for the phase.");
            AssertEquivalent(before, director.Snapshot);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(director.SavePath), "Hearing commits nothing.");
            yield return null;
            LandThePanel();
            yield return null;
        }

        // ------------------------------------------------------------------ P1. a pitch walks up, and Hear opens the board

        [UnityTest]
        public IEnumerator ComesToYou_PitchWalksUpAndHearOpensTheBoard() => PinnedClock(APitchWalksUpToTheBoardHeld);

        private IEnumerator APitchWalksUpToTheBoardHeld() =>
            WithTheHouseHeld(() => InstallComesToYouPitchHouse(true), APitchWalksUpToTheBoard);

        private IEnumerator APitchWalksUpToTheBoard()
        {
            var pitches = comesPitchIds;
            var shown = new List<HouseNpcInvitationBubble>();
            yield return WaitForThePitchLine(pitches[0], shown);
            AssertThePitchBubble(shown.Single());
            yield return HearThePitch(shown.Single(), pitches[0]);

            var state = director.Snapshot;
            var board = LastActive(EpisodeHud.OfferBoardName);
            Assert.That(board, Is.Not.Null, "Hear opens the pitch board,");
            Assert.That(ActiveRect(EpisodeHud.StoryStepName), Is.Null, "not the one pitch at a time.");
            var cards = OfferCards(board);
            Assert.That(cards.Select(card => card.name), Is.EqualTo(pitches.Select(id => EpisodeHud.OfferCardPrefix + state.Find(ComesPitch(id).fromId).name)),
                "Every card, in its dealt order: the walker's is one of them, unchanged.");
            AssertPitchBoardCaptions(board, "The board a walk-up opened");

            // An answer there is the board's own: the command and key it always sent.
            var before = director.Snapshot;
            var hear = OfferButton(cards[0], EpisodeHud.ReplyCaption(ReplyCards.Find(ReplyCards.Pitch, HoHPitches.HearKey).Label));
            ScrollIntoView(hear);
            AssertPressable(hear, hear.name);
            hear.onClick.Invoke();
            var heard = director.Snapshot;
            yield return null; yield return null;
            AssertThePitchAnswer(before, heard, pitches[0], HoHPitches.HearKey, "Hearing the walker's pitch on the board");
            director.ClosePanels();
        }

        // ------------------------------------------------------------------ P2. before the role moments, the one-at-a-time answer

        [UnityTest]
        public IEnumerator ComesToYou_PitchBeforeR7OpensTheOneAtATimeAnswers() => PinnedClock(BeforeTheBoardHeld);

        private IEnumerator BeforeTheBoardHeld() =>
            WithTheHouseHeld(() => InstallComesToYouPitchHouse(false), BeforeTheBoard);

        private IEnumerator BeforeTheBoard()
        {
            string first = comesPitchIds[0];
            Assert.That(ComesToYou.Read(director.Snapshot).Select(ask => ask.recordId), Is.EqualTo(new[] { first }),
                "Only the pitch waiting first comes.");
            var shown = new List<HouseNpcInvitationBubble>();
            yield return WaitForThePitchLine(first, shown);
            AssertThePitchBubble(shown.Single());
            yield return HearThePitch(shown.Single(), first);

            var state = director.Snapshot;
            Assert.That(LastActive(EpisodeHud.OfferBoardName), Is.Null, "No board before the role moments:");
            Assert.That(ActiveRect(EpisodeHud.StoryStepName), Is.Not.Null, "the one-at-a-time answer,");
            Assert.That(ShownText(), Does.Contain(ReplyCards.Title(state, ComesPitch(first))), "on the walker's pitch.");
            foreach (var key in HoHPitchAnswers) Assert.That(FindButton(HoHPitchCaption(key)), Is.Not.Null, key);

            var before = director.Snapshot;
            ButtonWithCaption(HoHPitchCaption(HoHPitches.HearKey)).onClick.Invoke();
            AssertHoHPitchCommit(before, HoHPitches.HearKey);
            director.ClosePanels();
        }

        // ------------------------------------------------------------------ P3. one walker at a time, in the board's order

        [UnityTest, Timeout(300000)]
        public IEnumerator ComesToYou_PitchOneWalkerAtATimeInTheBoardsOrder() => PinnedClock(OneWalkerAtATimeHeld);

        private IEnumerator OneWalkerAtATimeHeld() =>
            WithTheHouseHeld(() => InstallComesToYouPitchHouse(true), OneWalkerAtATime);

        private IEnumerator OneWalkerAtATime()
        {
            var pitches = comesPitchIds;
            var shown = new List<HouseNpcInvitationBubble>();
            yield return WaitForThePitchLine(pitches[0], shown);
            yield return HearThePitch(shown.Single(), pitches[0]);

            // The first two answered on the board: the walker's and the next one's.
            foreach (var id in pitches.Take(2))
            {
                var board = LastActive(EpisodeHud.OfferBoardName);
                var card = OfferCards(board).Single(row => row.name == EpisodeHud.OfferCardPrefix + director.Snapshot.Find(ComesPitch(id).fromId).name);
                var before = director.Snapshot;
                var hear = OfferButton(card, EpisodeHud.ReplyCaption(ReplyCards.Find(ReplyCards.Pitch, HoHPitches.HearKey).Label));
                ScrollIntoView(hear);
                AssertPressable(hear, hear.name);
                hear.onClick.Invoke();
                var heard = director.Snapshot;
                yield return null; yield return null;
                AssertThePitchAnswer(before, heard, id, HoHPitches.HearKey, "Hearing " + id);
            }
            Assert.That(ComesToYou.Read(director.Snapshot).Select(ask => ask.recordId), Is.EqualTo(new[] { pitches[2] }),
                "An answered pitcher leaves the queue.");
            director.ClosePanels();
            yield return null;

            // The gap released: the next unanswered pitcher walks up, and an answered one never does.
            NpcWrite("askNextAt", 0f);
            yield return WaitForTheFirstVisit(new string[0]);
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(pitches[2]), "The next unanswered pitcher, in the board's order.");
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesPitch(pitches[2]).fromId));
            Assert.That(director.IsAskCardOpen, Is.False);
        }

        // ------------------------------------------------------------------ P4. Let it wait costs nothing

        [UnityTest]
        public IEnumerator ComesToYou_PitchNotNowCostsNothing() => PinnedClock(NotNowCostsNothingHeld);

        private IEnumerator NotNowCostsNothingHeld() =>
            WithTheHouseHeld(() => InstallComesToYouPitchHouse(true), NotNowCostsNothing);

        private IEnumerator NotNowCostsNothing()
        {
            var pitches = comesPitchIds;
            var shown = new List<HouseNpcInvitationBubble>();
            yield return WaitForThePitchLine(pitches[0], shown);
            var before = director.Snapshot;
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            shown.Single().NotNowControl.onClick.Invoke();
            // The press frame.
            Assert.That(director.IsPanelOpen, Is.False, "Let it wait opens nothing.");
            Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, "The pitcher goes.");
            AssertEquivalent(before, director.Snapshot);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(director.SavePath), "Let it wait commits nothing.");
            Assert.That(director.IsAskSetAside(pitches[0]), Is.True, "Put off for the phase.");
            Assert.That(director.Snapshot.replyCards.Select(card => card.id), Is.EqualTo(pitches), "Every pitch still waits: silence costs nothing.");
            yield return null;

            // The nomination screen's "Hear houseguest pitches" is still the way in.
            yield return OpenStation();
            LandThePanel();
            yield return null;
            ButtonWithCaption(EpisodeHud.BackToNomineesCaption).onClick.Invoke();
            yield return null;
            Assert.That(LastActive(EpisodeHud.OfferBoardName), Is.Null, "Back at the nominees.");
            ButtonWithCaption(HearHoHPitchesCaption).onClick.Invoke();
            yield return null;
            LandThePanel();
            yield return null;
            var board = LastActive(EpisodeHud.OfferBoardName);
            Assert.That(board, Is.Not.Null, "\"Hear houseguest pitches\" opens the board,");
            Assert.That(OfferCards(board), Has.Length.EqualTo(pitches.Length), "every pitch on it.");
            AssertEquivalent(before, director.Snapshot);
            director.ClosePanels();
        }

        // ------------------------------------------------------------------ P5. none once the nominations are made; no card for a pitch

        [UnityTest, Timeout(300000)]
        public IEnumerator ComesToYou_PitchNoneOnceTheNominationsAreMade() => PinnedClock(NoneOnceNominatedHeld);

        private IEnumerator NoneOnceNominatedHeld() =>
            WithTheHouseHeld(() => InstallComesToYouPitchHouse(true), NoneOnceNominated);

        private IEnumerator NoneOnceNominated()
        {
            // A pitcher out of their own reach long enough gets no card: the next pitcher walks up instead.
            var pitches = comesPitchIds;
            NpcRead<Dictionary<string, float>>("askUnreachable")[pitches[0]] = EpisodeDirector.AskUnreachableSecondsBeforeCard;
            yield return WaitForTheFirstVisit(new string[0]);
            Assert.That(director.IsAskCardOpen, Is.False, "A pitch never comes as a card,");
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(pitches[1]), "so the next pitcher in the board's order walks up.");
            Assert.That(director.AskMissCount(pitches[0]), Is.Zero);

            // The nominations made: nobody pitches any more, and nobody walks up with a pitch.
            var state = director.Snapshot;
            var named = EpisodeEngine.NominationCandidates(state).Select(c => c.id)
                .Where(id => state.replyCards.All(card => card.fromId != id)).Take(2).ToArray();
            Assert.That(named, Has.Length.EqualTo(2), "Two houseguests who are not pitching to name.");
            var result = director.Submit(new EpisodeCommand
            {
                id = "comes-to-you-pitch-nominate", actorId = state.playerId, expectedPhase = state.phase,
                expectedRevision = state.revision, kind = EpisodeCommandKind.Nominate, targetId = named[0], secondTargetId = named[1],
            });
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(HoHPitches.Available(director.Snapshot), Is.False, "The premise: the nominations are made.");
            Assert.That(ComesToYou.Read(director.Snapshot).Where(ask => ask.source == ComesToYou.Source.Pitch), Is.Empty, "No pitch is listed.");
            director.ClosePanels();
            // The visit in flight goes on the next frame's tick, which reads the committed season.
            yield return null; yield return null;
            float until = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < until)
            {
                Assert.That(pitches, Has.No.Member(director.CurrentNpcInvitationRecordId), "Nobody walks up with a pitch.");
                Assert.That(director.IsAskCardOpen, Is.False, "and no card comes.");
                yield return null;
            }

            // With walk-overs off every ask comes as a card, and a pitch has none: nobody comes, and the board waits.
            yield return InstallComesToYouPitchHouse(true, false);
            NpcWrite("npcApproachDiagnosticsSuppressed", true);
            until = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < until)
            {
                Assert.That(director.IsAskCardOpen, Is.False, "No card for a pitch.");
                Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, "and nobody walks over.");
                yield return null;
            }
            yield return OpenStation();
            LandThePanel();
            yield return null;
            Assert.That(OfferCards(LastActive(EpisodeHud.OfferBoardName)), Has.Length.EqualTo(comesPitchIds.Length),
                "The nomination screen still has every pitch.");
            director.ClosePanels();
        }

        // ------------------------------------------------------------------ P6-P8 (stack O2's fixes): who the "N more" line counts, and the switch

        /// <summary>The "N more" line's count as a dialogue or card would draw it beside somebody who is not pitching: every pitcher who could still come.</summary>
        private int PitchersStillComing() => director.AskMoreWaitingForDiagnostics(director.Snapshot.playerId);

        /// <summary>
        /// The review's finding on 4p, its first half: the "N more" line counts only asks that can still come by themselves.
        /// A pitcher out of their own reach long enough is due as a card, and a pitch has none (P5), so they are walked over
        /// no more this phase and are not counted - the others still are.
        /// </summary>
        [UnityTest]
        public IEnumerator ComesToYou_PitchDueAsACardIsNotCountedAsWaiting() => PinnedClock(APitchDueAsACardHeld);

        private IEnumerator APitchDueAsACardHeld() =>
            WithTheHouseHeld(() => InstallComesToYouPitchHouse(true), APitchDueAsACard);

        private IEnumerator APitchDueAsACard()
        {
            var pitches = comesPitchIds;
            Assert.That(PitchersStillComing(), Is.EqualTo(pitches.Length), "The premise: every pitcher on the board can still walk up.");
            var unreachable = NpcRead<Dictionary<string, float>>("askUnreachable");
            unreachable[pitches[0]] = EpisodeDirector.AskUnreachableSecondsBeforeCard;
            Assert.That(PitchersStillComing(), Is.EqualTo(pitches.Length - 1), "A pitcher due as a card never comes, so is not counted.");
            unreachable.Remove(pitches[0]);
            Assert.That(PitchersStillComing(), Is.EqualTo(pitches.Length), "Back in reach, counted again.");
            yield return null;
        }

        /// <summary>
        /// The finding's second half: with walk-overs off every ask comes as a card, and a pitch has none, so no pitcher is
        /// counted as still coming; turned back on, every one is.
        /// </summary>
        [UnityTest]
        public IEnumerator ComesToYou_PitchWithWalkOversOffIsNotCountedAsWaiting() => PinnedClock(PitchesWithWalkOversOffHeld);

        private IEnumerator PitchesWithWalkOversOffHeld() =>
            WithTheHouseHeld(() => InstallComesToYouPitchHouse(true, false), PitchesWithWalkOversOff);

        private IEnumerator PitchesWithWalkOversOff()
        {
            Assert.That(director.AskWalkOvers, Is.False, "The premise: walk-overs off.");
            Assert.That(PitchersStillComing(), Is.Zero, "No pitcher is counted: none can come as a card.");
            director.AskWalkOvers = true;
            Assert.That(PitchersStillComing(), Is.EqualTo(comesPitchIds.Length), "Walk-overs on: every pitcher can come again.");
            director.AskWalkOvers = false;
            yield return null;
        }

        /// <summary>
        /// Plan C.4 item 11 (the review's finding on 4p): with the reply asks' switch off the queue is the offers' alone, so
        /// no pitch is listed, nobody walks up with one, none is counted, and the nomination screen still has every pitch. The
        /// switch is off before asks are, so no visit can set out on the queue with the pitches in it.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator ComesToYou_PitchNoneWithReplyAsksOff() => PinnedClock(NoPitchWithRepliesOffHeld);

        private IEnumerator NoPitchWithRepliesOffHeld() =>
            WithTheHouseHeld(() => InstallComesToYouPitchHouseRepliesOff(), NoPitchWithRepliesOff);

        /// <summary><see cref="InstallComesToYouPitchHouse"/> with <see cref="EpisodeDirector.ReplyAsksEnabled"/> false from before asks turn on.</summary>
        private IEnumerator InstallComesToYouPitchHouseRepliesOff()
        {
            SaveComesToYouPitchState(true);
            yield return ReloadEpisode();
            director.BuildNpcWorldForDiagnostics();
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "The house's world is built.");
            director.ReplyAsksEnabled = false;
            yield return TurnAsksOnInTheLoadedHouse(new string[0], true);
            Assert.That(director.Snapshot.replyCards.Select(card => card.id), Is.EqualTo(comesPitchIds), "The pitches survived the load.");
        }

        private IEnumerator NoPitchWithRepliesOff()
        {
            var pitches = comesPitchIds;
            Assert.That(director.AsksAreOn, Is.True, "Asks are on,");
            Assert.That(director.ReplyAsksEnabled, Is.False, "the reply asks' switch is off,");
            Assert.That(ComesToYou.Read(director.Snapshot).Select(ask => ask.recordId), Is.EqualTo(pitches), "and the reader still lists every pitch.");
            Assert.That(PitchersStillComing(), Is.Zero, "The director's queue drops them: none is counted.");
            NpcWrite("npcApproachDiagnosticsSuppressed", true);
            float until = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < until)
            {
                Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, "Nobody walks up with a pitch.");
                Assert.That(director.IsAskCardOpen, Is.False, "and no card comes.");
                yield return null;
            }
            yield return OpenStation();
            LandThePanel();
            yield return null;
            Assert.That(OfferCards(LastActive(EpisodeHud.OfferBoardName)), Has.Length.EqualTo(pitches.Length),
                "The nomination screen still has every pitch.");
            director.ClosePanels();
            yield return null;
            director.ReplyAsksEnabled = true;
            Assert.That(PitchersStillComing(), Is.EqualTo(pitches.Length), "Switched on, the pitches are the queue's again.");
        }
    }
}
