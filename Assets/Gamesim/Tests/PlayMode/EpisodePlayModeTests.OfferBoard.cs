using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The offer board (ROLE-MOMENTS-PLAN R2, §5.1): under the role moments the nominees' veto asks are portrait cards at
    /// the veto decision and in the Diary Room's veto column - one an ask, in the order the block was named, each saying
    /// the tone, the situation, the read, the quote, the terms, the stakes and (R3) what a no and silence cost in labels
    /// of their own over the two answers the rows always had, captioned as they always were and described as the offer's
    /// controls, under a head that says silence costs more than a no (R3); a card whose yes cannot be kept says why over a
    /// disabled yes; at the largest text the cards page; nothing on the board moves; and a screen reader hears a control's
    /// caption, the card's heading and one line. A season without the role moments draws the rows exactly as it did.
    ///
    /// <para>The meeting is reached through the engine, as R1's tone tests reach it: an eight-house season under the
    /// shipped rules - at story rules Roles, or left at Threads - walked to week one's veto, the player handed it, and
    /// the advance into the meeting files both nominees' asks the way the engine always does.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// An eight-house season under the shipped rules walked through the engine to week one's veto meeting, with the
        /// player holding the veto and neither Head of Household nor on the block, and both nominees warm enough to ask:
        /// the first Threatening by their traits, the second Earnest, the player reading both at nothing. The second named
        /// is the warmer toward the player - a view the player cannot see - so their ask is filed first and the rows'
        /// order, which follows the filing, is not the block's: a board drawn in the rows' order shows. A seed whose ids
        /// happen to sort the other way is passed over. At <paramref name="roles"/> the story plays at Roles, the Head of
        /// Household held at nothing toward the player so they never ask to keep the block (R4) and the board stays the
        /// nominees' two cards, whatever the seed's Head of Household makes of the player; otherwise at the Threads every
        /// shipped season plays.
        /// </summary>
        private static EpisodeState OfferBoardSeason(bool roles)
        {
            for (uint seed = 7001; seed < 7041; seed++)
            {
                var start = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(start);
                if (roles) start.story.rulesVersion = StoryRules.Roles;
                var walk = new EpisodeEngine(start);
                for (int i = 0; i < 200 && walk.Snapshot.phase != EpisodePhase.Veto; i++)
                {
                    var step = walk.Apply(NextCommand(walk.Snapshot));
                    Assert.That(step.accepted, Is.True, "seed " + seed + ": " + step.reason);
                }
                var s = walk.Snapshot;
                if (s.phase != EpisodePhase.Veto || s.competitionResolved || s.hohId == s.playerId || s.nominees.Contains(s.playerId)) continue;
                string first = s.nominees[0], second = s.nominees[1];
                s.Find(first).traits = new List<string> { "Competitive", "Stubborn" };
                s.Find(second).traits = new List<string> { "Emotional" };
                var scores = new List<(string, string, double)> { (first, s.playerId, 40.0), (second, s.playerId, 60.0), (s.playerId, first, 0.0), (s.playerId, second, 0.0) };
                // A Head of Household warm enough toward the player asks to keep the block under the role moments (R4): held
                // below the dealing floor, they never do here.
                if (roles) scores.Add((s.hohId, s.playerId, 0.0));
                foreach (var (holder, toward, score) in scores)
                {
                    var edge = s.relationships.FirstOrDefault(r => r.fromId == holder && r.toId == toward);
                    if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = holder, toId = toward });
                    edge.score = score;
                }
                s.vetoHolderId = s.playerId;
                if (!s.vetoPlayers.Contains(s.playerId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.playerId;
                s.competitionResolved = true;
                s.competitionScores = s.vetoPlayers.Select((id, i) => new CompetitionScore { contestantId = id, score = id == s.playerId ? 100 : i }).ToList();
                var engine = new EpisodeEngine(s);
                var meeting = engine.Apply(new EpisodeCommand
                {
                    id = "offer-board-meeting", actorId = s.playerId, kind = EpisodeCommandKind.Advance,
                    expectedRevision = s.revision, expectedPhase = s.phase,
                });
                Assert.That(meeting.accepted, Is.True, "seed " + seed + ": " + meeting.reason);
                Assert.That(meeting.state.phase, Is.EqualTo(EpisodePhase.VetoMeeting));
                Assert.That(EpisodeEngine.RolesOn(meeting.state), Is.EqualTo(roles));
                Assert.That(RoleOffers.VetoAsks(meeting.state).Select(ask => ask.proposerId), Is.EquivalentTo(new[] { first, second }), "Both nominees asked.");
                Assert.That(meeting.state.deals.Where(deal => deal.type == DealKind.VetoHold), Is.Empty, "The Head of Household asked nothing: the board is the nominees'.");
                if (!FiledAsks(meeting.state).SequenceEqual(new[] { second, first })) continue;
                return meeting.state;
            }
            Assert.Fail("No seed reached a veto the player could be handed, with the asks filed against the block's order.");
            return null;
        }

        /// <summary>Who asked for the veto this week, in the order the rows draw them (<see cref="NpcDeals.Pending"/>): the order the asks were filed in.</summary>
        private static string[] FiledAsks(EpisodeState s) =>
            NpcDeals.Pending(s).Where(deal => deal.type == DealKind.VetoUse && deal.week == s.week).Select(deal => deal.proposerId).ToArray();

        /// <summary>
        /// Lands the panel's arrival if it is still in flight, before a control on it is read: the panel fades in from
        /// nothing over 160 ms, a batchmode frame is far shorter, and <see cref="EpisodeHud.TryDescribeOfferControl"/>
        /// reads no control drawn at nothing. Only a reveal in flight, as <see cref="AssertPressable"/> lands one; the
        /// canvas carries the alpha down on the next frame, so a caller yields one after it.
        /// </summary>
        private void LandThePanel()
        {
            var panel = LastActive("Episode panel");
            if (panel == null) return;
            foreach (var reveal in panel.GetComponents<HudReveal>()) reveal.Finish();
        }

        /// <summary>Installs <see cref="OfferBoardSeason"/>, saved and loaded, with the house's own clock held so nothing commits under the test.</summary>
        private IEnumerator InstallOfferBoardMeeting(bool roles)
        {
            var state = OfferBoardSeason(roles);
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
        }

        private static RectTransform[] OfferCards(RectTransform board) =>
            board.GetComponentsInChildren<RectTransform>().Where(rect => rect.name.StartsWith(EpisodeHud.OfferCardPrefix, System.StringComparison.Ordinal)).ToArray();

        private static Button OfferButton(RectTransform card, string caption) =>
            card.GetComponentsInChildren<Button>(true).Single(button => button.name == caption);

        private static string OfferLabel(RectTransform card, string name) =>
            card.GetComponentsInChildren<TMP_Text>().Where(label => label.name == name).Select(label => label.text).SingleOrDefault();

        /// <summary>The player's free answer to an offer, as the board and the rows both commit it.</summary>
        private static EpisodeCommand OfferAnswer(EpisodeState s, string offerId, bool accept) => new EpisodeCommand
        {
            id = "offer-board-expected-" + s.revision, actorId = s.playerId, kind = EpisodeCommandKind.RespondToDeal,
            targetId = offerId, text = accept ? EpisodeEngine.AcceptDeal : "decline", expectedRevision = s.revision, expectedPhase = s.phase,
        };

        /// <summary>
        /// The season the press committed is the one the same answer commits on the engine: the rows' answer, exactly. Only
        /// the command's own id differs, which the engine records and nothing reads.
        /// </summary>
        private static void AssertTheRowsAnswer(EpisodeState before, EpisodeState after, string offerId, bool accept, string what)
        {
            var expected = new EpisodeEngine(before).Apply(OfferAnswer(before, offerId, accept));
            Assert.That(expected.accepted, Is.True, expected.reason);
            Assert.That(after.revision, Is.EqualTo(before.revision + 1), what + " is one commit.");
            var mine = expected.state.Clone();
            mine.acceptedCommandIds = new List<string>(after.acceptedCommandIds);
            Assert.That(JsonUtility.ToJson(after), Is.EqualTo(JsonUtility.ToJson(mine)), what + " commits what the rows' answer commits.");
        }

        /// <summary>
        /// The captions sweep: every control on the board says one caption and nothing else, is named by it, and is one of
        /// the answers the rows always had or the largest text's pager; each card has one of each answer.
        /// </summary>
        private static void AssertOfferBoardCaptions(RectTransform board, string where)
        {
            var allowed = new[] { EpisodeHud.DealAcceptCaption, EpisodeHud.DealDeclineCaption, EpisodeHud.PreviousOfferCaption, EpisodeHud.NextOfferCaption };
            foreach (var button in board.GetComponentsInChildren<Button>())
            {
                var words = button.GetComponentsInChildren<TMP_Text>(true).Select(label => label.text).ToArray();
                Assert.That(words, Has.Length.EqualTo(1), where + ": '" + button.name + "' says its caption and nothing else: " + string.Join(" | ", words));
                Assert.That(allowed, Does.Contain(words[0]), where + ": '" + words[0] + "' is a caption the board may carry.");
                Assert.That(button.name, Is.EqualTo(words[0]), where + ": a control is named by its caption.");
            }
            foreach (var card in OfferCards(board))
                foreach (var caption in new[] { EpisodeHud.DealAcceptCaption, EpisodeHud.DealDeclineCaption })
                    Assert.That(card.GetComponentsInChildren<Button>().Count(button => button.name == caption), Is.EqualTo(1), where + ": " + card.name + " has one '" + caption + "'.");
        }

        /// <summary>No word on the board is set under ten units: twelve pixels on a 1080p screen, whatever the auto-size chose.</summary>
        private static void AssertNoBoardTypeUnderTwelvePixels(RectTransform board, string where)
        {
            foreach (var label in board.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate(true);
                Assert.That(label.fontSize * 1080f / EpisodeHud.ReferenceHeight, Is.GreaterThanOrEqualTo(12f - .01f),
                    where + ": '" + label.text + "' is drawn at " + label.fontSize.ToString("0.#") + ", under twelve pixels at 1080p.");
            }
        }

        /// <summary>
        /// Under the role moments the veto decision's asks are the board's cards, in the order the block was named, every
        /// word the card's own label and every answer the offer's described control under its unchanged caption; pressing
        /// one commits exactly what the rows' answer commits, and the decision itself is still to make.
        /// </summary>
        [UnityTest]
        public IEnumerator OfferBoard_UnderRolesEachAskIsACardInBlockOrderAndAnswersAsTheRowsDid()
        {
            yield return InstallOfferBoardMeeting(true);
            yield return OpenStation();
            LandThePanel();
            yield return null;
            var state = director.Snapshot;
            var asks = RoleOffers.VetoAsks(state);
            Assert.That(asks.Select(ask => ask.proposerId), Is.EqualTo(state.nominees.ToArray()), "Both nominees asked, in the order they were named.");
            Assert.That(FiledAsks(state), Is.Not.EqualTo(state.nominees.ToArray()),
                "The rows' order - the filing order, which follows who is warmer toward the player - is not the block's here, so a board drawn in it shows.");
            var board = LastActive(EpisodeHud.OfferBoardName);
            Assert.That(board, Is.Not.Null, "Under the role moments the asks are a board,");
            Assert.That(ShownText(), Does.Not.Contain("AN OFFER FROM"), "not the rows.");
            Assert.That(OfferLabel(board, EpisodeHud.OfferEyebrowName), Is.EqualTo(EpisodeHud.OfferBoardEyebrow));
            Assert.That(OfferLabel(board, EpisodeHud.OfferLineName), Is.EqualTo(EpisodeDirector.VetoOfferBoardLine));
            Assert.That(OfferLabel(board, EpisodeHud.OfferFreeName), Is.EqualTo(EpisodeHud.OfferBoardFreeLine));
            Assert.That(OfferLabel(board, EpisodeHud.OfferSilenceName), Is.EqualTo(RoleOffers.SilenceLine),
                "R3 makes silence cost more than a no, and the board's head says so in a label of its own.");
            var head = board.GetComponentsInChildren<TMP_Text>();
            Rect said = ScreenRect(head.Single(label => label.name == EpisodeHud.OfferLineName).rectTransform);
            Rect silence = ScreenRect(head.Single(label => label.name == EpisodeHud.OfferSilenceName).rectTransform);
            Assert.That(silence.xMin >= said.xMax - .5f || silence.yMax <= said.yMin + .5f, Is.True,
                "Beside the board's line or under it, never over it: " + said + " / " + silence);

            var cards = OfferCards(board);
            Assert.That(cards.Select(card => card.name), Is.EqualTo(asks.Select(ask => EpisodeHud.OfferCardPrefix + state.Find(ask.proposerId).name)),
                "A card an ask, in the order the block was named,");
            Assert.That(ScreenRect(cards[0]).xMax, Is.LessThanOrEqualTo(ScreenRect(cards[1]).xMin + .5f), "side by side, the first named first.");
            var hud = Hud;
            for (int i = 0; i < cards.Length; i++)
            {
                var words = RoleOffers.VetoAskCard(state, asks[i]);
                string who = words.name;
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferNameName), Is.EqualTo(who));
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferChipName), Is.EqualTo("VETO COMMITMENT"), who);
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferChipLineName), Is.EqualTo(RoleOffers.VetoAskLine), who);
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferToneWordName), Is.EqualTo(words.tone), who);
                Assert.That(cards[i].GetComponentsInChildren<TMP_Text>().Single(label => label.transform.parent.name == EpisodeHud.OfferToneMarkName).text,
                    Is.EqualTo(RoleOffers.ToneMark(words.tone)), who + "'s tone has its own mark beside its word.");
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferSituationName), Is.EqualTo(words.situation), who);
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferReadName), Is.EqualTo(words.read), who);
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferQuoteName), Is.EqualTo("“" + words.quote + "”"), who);
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferTermsName), Is.EqualTo(words.terms), who);
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferStakesName), Is.EqualTo(words.stakes), who);
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferAnswersName), Is.Not.Null.And.EqualTo(words.answers), who + ": what a no and silence cost.");
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferHistoryName), Is.EqualTo(words.history), who);
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferLockName), Is.Null, who + "'s yes is open.");
                // The face is the character's portrait, bound as every face in the house is - never an initials avatar.
                var photo = cards[i].GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Photo");
                Assert.That(photo.GetComponentsInChildren<CharacterPortraitBinding>(true), Has.Length.EqualTo(1), who + "'s face is bound to the portrait studio.");
                Assert.That(photo.GetComponentsInChildren<TMP_Text>().Select(label => label.text), Is.EqualTo(new[] { "NOM" }),
                    who + "'s face carries the role's word and no initials.");
                foreach (bool accept in new[] { true, false })
                {
                    string caption = accept ? EpisodeHud.DealAcceptCaption : EpisodeHud.DealDeclineCaption;
                    var button = OfferButton(cards[i], caption);
                    Assert.That(button.GetComponentsInChildren<TMP_Text>(true).Select(label => label.text), Is.EqualTo(new[] { caption }),
                        who + ": '" + caption + "' says its caption, unchanged, and nothing more.");
                    Assert.That(hud.TryDescribeOfferControl(button, out var row), Is.True, who + ": '" + caption + "' is described as the offer's control.");
                    Assert.That((row.OfferId, row.ProposerId, row.RecipientId, row.Accept), Is.EqualTo((asks[i].id, asks[i].proposerId, state.playerId, accept)));
                    Assert.That(button.IsInteractable(), Is.True, who + ": '" + caption + "' can be pressed.");
                }
            }

            // The second card's no, then the first card's yes: each the rows' own answer. A press commits as it returns,
            // so the season is read then, before anything else the house does in the frames after can commit too.
            var before = director.Snapshot;
            var decline = OfferButton(cards[1], EpisodeHud.DealDeclineCaption);
            ScrollIntoView(decline);
            AssertPressable(decline, EpisodeHud.DealDeclineCaption);
            decline.onClick.Invoke();
            var declined = director.Snapshot;
            yield return null; yield return null;
            Assert.That(declined.deals.Single(deal => deal.id == asks[1].id).status, Is.EqualTo(DealStatus.Declined));
            AssertTheRowsAnswer(before, declined, asks[1].id, false, "Turning the second nominee down");

            board = LastActive(EpisodeHud.OfferBoardName);
            Assert.That(OfferCards(board).Select(card => card.name), Is.EqualTo(new[] { EpisodeHud.OfferCardPrefix + declined.Find(asks[0].proposerId).name }),
                "Answered, the card leaves the board; the other stays.");
            var yes = OfferButton(OfferCards(board)[0], EpisodeHud.DealAcceptCaption);
            ScrollIntoView(yes);
            AssertPressable(yes, EpisodeHud.DealAcceptCaption);
            yes.onClick.Invoke();
            var accepted = director.Snapshot;
            yield return null; yield return null;
            Assert.That(accepted.deals.Single(deal => deal.id == asks[0].id).status, Is.EqualTo(DealStatus.Active));
            AssertTheRowsAnswer(declined, accepted, asks[0].id, true, "Taking the first nominee's ask");
            Assert.That(ActiveRect(EpisodeHud.OfferBoardName), Is.Null, "With every ask answered there is no board.");
            Assert.That(ButtonWithCaptionOrNull("Do not use the veto"), Is.Not.Null, "The decision itself is still to make.");
            Assert.That(accepted.vetoResolved, Is.False);
        }

        /// <summary>
        /// A season at Threads - every season shipped today - draws the meeting's asks as the rows it always drew, piece
        /// for piece where they always stood: in <see cref="NpcDeals.Pending"/>'s order, a heading, the deal's sentence
        /// with its price, the yes and the no, each the offer's described control, between the meeting's head and "USE
        /// THE VETO"; and no board.
        /// </summary>
        [UnityTest]
        public IEnumerator OfferBoard_WithoutRolesTheMeetingDrawsTheRowsItAlwaysDrew()
        {
            yield return InstallOfferBoardMeeting(false);
            yield return OpenStation();
            LandThePanel();
            yield return null;
            var state = director.Snapshot;
            Assert.That(state.story.rulesVersion, Is.EqualTo(StoryRules.Threads), "The shipped story rules.");
            Assert.That(ActiveRect(EpisodeHud.OfferBoardName), Is.Null, "No board without the role moments.");

            var offers = NpcDeals.Pending(state).Where(deal => deal.type == DealKind.VetoUse && deal.week == state.week).ToList();
            Assert.That(offers, Has.Count.EqualTo(2));
            var content = LastActive("Episode content");
            var rows = content.Cast<Transform>().Where(child => child.gameObject.activeSelf).ToList();
            int at = rows.FindIndex(child => child.GetComponent<TMP_Text>() != null
                && child.GetComponent<TMP_Text>().text == "AN OFFER FROM " + state.Find(offers[0].proposerId).name.ToUpperInvariant());
            Assert.That(at, Is.GreaterThan(0), "The first row heads the first offer, under the meeting's head.");
            Assert.That(rows.Take(at).Select(child => child.name), Does.Contain(EpisodeHud.MeetingHeadName), "The meeting's head stands over the rows.");
            var hud = Hud;
            for (int i = 0; i < offers.Count; i++)
            {
                var offer = offers[i];
                string name = state.Find(offer.proposerId).name, price = Negotiation.AskPriceLine(state, offer);
                var heading = rows[at + 4 * i].GetComponent<TMP_Text>();
                var sentence = rows[at + 4 * i + 1].GetComponent<TMP_Text>();
                Assert.That(heading.text, Is.EqualTo("AN OFFER FROM " + name.ToUpperInvariant()));
                Assert.That(sentence.text, Is.EqualTo(name + " is on the block and wants your word that you will use the veto on them." + (price == null ? "" : " " + price)));
                Assert.That(price, Is.Not.Null, "The commitment rules put a price on the ask, and the row says it.");
                foreach (var (index, accept) in new[] { (2, true), (3, false) })
                {
                    string caption = accept ? EpisodeHud.DealAcceptCaption : EpisodeHud.DealDeclineCaption;
                    var button = rows[at + 4 * i + index].GetComponent<Button>();
                    Assert.That(button, Is.Not.Null, name + ": '" + caption + "' is a row of the column.");
                    Assert.That(button.name, Is.EqualTo(caption));
                    Assert.That(button.GetComponentsInChildren<TMP_Text>(true).First().text, Is.EqualTo(caption));
                    // A row is drawn for the offer's id, which is nobody's face: the plain row, its chevron at the end and
                    // no portrait or reading of anybody, as it always was.
                    Assert.That(button.transform.Find("Chevron"), Is.Not.Null, name + ": '" + caption + "' is the plain row, with its chevron,");
                    Assert.That(button.transform.Find("Portrait"), Is.Null, name + ": '" + caption + "' is fronted by no face,");
                    Assert.That(button.GetComponentsInChildren<TMP_Text>(true).Any(label => label.name == EpisodeHud.TrustReadingName), Is.False,
                        name + ": '" + caption + "' carries no trust reading.");
                    Assert.That(hud.TryDescribeOfferControl(button, out var row), Is.True);
                    Assert.That((row.OfferId, row.Accept), Is.EqualTo((offer.id, accept)));
                }
            }
            Assert.That(rows[at + 4 * offers.Count].name, Is.EqualTo(EpisodeHud.MeetingEyebrowName), "\"USE THE VETO\" follows the rows, as it always did.");
            Assert.That(director.GetComponentsInChildren<Button>().Count(button => button.IsActive() && button.name == EpisodeHud.DealAcceptCaption), Is.EqualTo(2));
            Assert.That(director.GetComponentsInChildren<Button>().Count(button => button.IsActive() && button.name == EpisodeHud.DealDeclineCaption), Is.EqualTo(2));
            Assert.That(ShownText(), Does.Not.Contain(EpisodeHud.OfferBoardEyebrow));
            Assert.That(ShownText(), Does.Not.Contain(RoleOffers.SilenceLine), "Without the role moments silence costs nothing, and nothing says it does.");
        }

        /// <summary>
        /// At every text size every control on the board says its caption and nothing else, every label draws its copy in
        /// its box and none is set under twelve pixels at 1080p. At the standard and the larger text every card is on the
        /// one page, side by side; at the largest they page, a card a page, with "Next offer", "Previous offer" and "1 of
        /// 2", and a turn commits nothing and keeps the keyboard on the pager. The board is photographed at each size, and
        /// the largest's second page, for review; the frames are not compared.
        /// </summary>
        [UnityTest]
        public IEnumerator OfferBoard_EveryCaptionAndLabelHoldsAtEveryTextSizeAndTheLargestPages()
        {
            yield return InstallOfferBoardMeeting(true);
            var asks = RoleOffers.VetoAsks(director.Snapshot);
            foreach (var size in TextSizes.All)
            {
                if (size != TextSize.Standard)
                {
                    director.ClosePanels();
                    yield return ApplyTextSize(size);
                }
                yield return OpenStation();
                yield return null;
                string where = "The offer board " + TextScales.At(size);
                var board = LastActive(EpisodeHud.OfferBoardName);
                Assert.That(board, Is.Not.Null, where + " is up.");
                var cards = OfferCards(board);
                bool largest = size == TextSize.Largest;
                if (!largest)
                {
                    Assert.That(cards, Has.Length.EqualTo(asks.Count), where + " has every card on the one page,");
                    Assert.That(ScreenRect(cards[0]).yMax, Is.EqualTo(ScreenRect(cards[1]).yMax).Within(.5f), where + ": side by side.");
                    Assert.That(ActiveRect(EpisodeHud.OfferCountName), Is.Null, where + " has no pager.");
                }
                else
                {
                    Assert.That(cards, Has.Length.EqualTo(1), where + " pages, a card a page.");
                    Assert.That(OfferLabel(board, EpisodeHud.OfferCountName), Is.EqualTo("1 of " + asks.Count), where + " says where the player is.");
                    Assert.That(ButtonWithCaptionOrNull(EpisodeHud.PreviousOfferCaption), Is.Null, "No way back from the first card.");
                }
                AssertOfferBoardCaptions(board, where);
                AssertEveryLabelDraws(board, where);
                AssertNoMeetingLabelOverflows(board, where);
                AssertNoBoardTypeUnderTwelvePixels(board, where);
                if (Application.isBatchMode) yield return CaptureFraming("veto-offer-board" + TextScales.Suffix(size));
                if (!largest) continue;

                int revision = director.Snapshot.revision;
                ButtonWithCaption(EpisodeHud.NextOfferCaption).onClick.Invoke();
                int turned = director.Snapshot.revision;
                yield return null; yield return null;
                board = LastActive(EpisodeHud.OfferBoardName);
                string next = where + ", turned on";
                Assert.That(OfferCards(board).Select(card => card.name), Is.EqualTo(new[] { EpisodeHud.OfferCardPrefix + director.Snapshot.Find(asks[1].proposerId).name }),
                    next + ", shows the second card.");
                Assert.That(OfferLabel(board, EpisodeHud.OfferCountName), Is.EqualTo("2 of " + asks.Count), next);
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.NextOfferCaption), Is.Null, next + ": no way on from the last card,");
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.PreviousOfferCaption), Is.Not.Null, next + ": and a way back.");
                Assert.That(EventSystem.current.currentSelectedGameObject != null
                    && EventSystem.current.currentSelectedGameObject.name == EpisodeHud.PreviousOfferCaption, Is.True, next + ": the keyboard stays on the pager.");
                Assert.That(turned, Is.EqualTo(revision), "Turning a page commits nothing.");
                AssertOfferBoardCaptions(board, next);
                AssertEveryLabelDraws(board, next);
                AssertNoMeetingLabelOverflows(board, next);
                if (Application.isBatchMode) yield return CaptureFraming("veto-offer-board-largest-page-2");
            }
            director.ClosePanels();
            yield return ApplyTextSize(false);
        }

        /// <summary>
        /// Nothing on the board travels or fades: under "Reduce character motion" the panel cuts in and the board carries
        /// no reveal, fill or fade and stands still; with motion on, the cards still cut in - only the panel arrives.
        /// </summary>
        [UnityTest]
        public IEnumerator OfferBoard_TheCardsCutInAndNothingOnTheBoardMoves()
        {
            yield return InstallOfferBoardMeeting(true);
            director.OpenSettings();
            yield return null;
            ButtonWithCaption("Reduce character motion").onClick.Invoke();
            yield return null;
            director.ClosePanels();
            yield return null;
            Assert.That(Hud.ReducedMotion, Is.True, "The setting reaches the HUD.");
            yield return OpenStation();
            var panel = LastActive("Episode panel");
            Assert.That(panel.GetComponent<HudReveal>(), Is.Null, "Under reduced motion the panel cuts in,");
            var board = LastActive(EpisodeHud.OfferBoardName);
            AssertTheBoardIsStill(board, "Under reduced motion");
            // On the board, not on the screen: a scroll to the selected control is the column's, not a card's travel.
            var placed = OfferCards(board).ToDictionary(card => card.name, card => (card.anchoredPosition, card.sizeDelta, card.localScale));
            float until = Time.realtimeSinceStartup + .3f;
            while (Time.realtimeSinceStartup < until) yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (var card in OfferCards(LastActive(EpisodeHud.OfferBoardName)))
                Assert.That((card.anchoredPosition, card.sizeDelta, card.localScale), Is.EqualTo(placed[card.name]),
                    card.name + " stands where it was drawn, a third of a second on.");

            director.OpenSettings();
            yield return null;
            ButtonWithCaption("Enable character motion").onClick.Invoke();
            yield return null;
            director.ClosePanels();
            yield return null;
            Assert.That(Hud.ReducedMotion, Is.False);
            yield return OpenStation();
            AssertTheBoardIsStill(LastActive(EpisodeHud.OfferBoardName), "With motion on");
        }

        private static void AssertTheBoardIsStill(RectTransform board, string where)
        {
            Assert.That(board, Is.Not.Null, where + ", the board is up.");
            Assert.That(board.GetComponentsInChildren<HudReveal>(true), Is.Empty, where + ", no card travels or fades in.");
            Assert.That(board.GetComponentsInChildren<HudFill>(true), Is.Empty, where + ", nothing on the board eases.");
            Assert.That(board.GetComponentsInChildren<HudFade>(true), Is.Empty, where + ", nothing fades.");
            Assert.That(board.GetComponentsInChildren<Animator>(true), Is.Empty, where + ", nothing animates.");
            Assert.That(board.GetComponentsInChildren<CanvasGroup>(true).All(group => group.alpha >= .999f), Is.True, where + ", nothing is drawn part-way in.");
        }

        /// <summary>
        /// A screen reader hears each of a card's controls as its caption, then the card's heading - "{name}, nominee,
        /// veto commitment" - then one line saying the tone, the situation, the read, the terms, the stakes and what a no
        /// and silence cost, in words.
        /// A control off the board has no such line.
        /// </summary>
        [UnityTest]
        public IEnumerator OfferBoard_AScreenReaderHearsTheCaptionTheHeadingAndOneLine()
        {
            yield return InstallOfferBoardMeeting(true);
            yield return OpenStation();
            yield return null;
            var state = director.Snapshot;
            var asks = RoleOffers.VetoAsks(state);
            var cards = OfferCards(LastActive(EpisodeHud.OfferBoardName));
            var hud = Hud;
            for (int i = 0; i < cards.Length; i++)
            {
                var words = RoleOffers.VetoAskCard(state, asks[i]);
                foreach (var caption in new[] { EpisodeHud.DealAcceptCaption, EpisodeHud.DealDeclineCaption })
                {
                    Assert.That(hud.TryReadOfferControl(OfferButton(cards[i], caption), out var reading), Is.True, words.name + ": '" + caption + "' is read.");
                    Assert.That(reading, Is.EqualTo(RoleOffers.Spoken(caption, words)));
                    Assert.That(reading, Does.StartWith(caption + ". " + words.name + ", nominee, veto commitment. Tone: " + words.tone + "."),
                        "The caption, the heading, then the line, which opens on the tone in words.");
                    foreach (var part in new[] { words.situation, words.read, words.terms, words.stakes, words.answers })
                        Assert.That(reading, Does.Contain(part), words.name + ": the line says '" + part + "'.");
                }
            }
            Assert.That(hud.TryReadOfferControl(ButtonWithCaption("Do not use the veto"), out _), Is.False, "Only the board's controls carry its line.");
        }

        /// <summary>
        /// The cards carry the same two captions, so a rebuild the house orders on its own account - a body finishing its
        /// assembly is one - puts the keyboard back on the answer it was on, read as the offer's control: the same offer,
        /// the same answer. Not the first control of that caption, which is the first card's, where Enter would answer a
        /// nominee the player was not looking at.
        /// </summary>
        [UnityTest]
        public IEnumerator OfferBoard_ARebuildKeepsTheKeyboardOnTheCardItWasOn()
        {
            yield return InstallOfferBoardMeeting(true);
            yield return OpenStation();
            LandThePanel();
            yield return null;
            var asks = RoleOffers.VetoAsks(director.Snapshot);
            foreach (bool accept in new[] { false, true })
            {
                string caption = accept ? EpisodeHud.DealAcceptCaption : EpisodeHud.DealDeclineCaption;
                var held = OfferButton(OfferCards(LastActive(EpisodeHud.OfferBoardName))[1], caption);
                EventSystem.current.SetSelectedGameObject(held.gameObject);
                Assert.That(Hud.TryDescribeOfferControl(held, out var was), Is.True);
                Assert.That((was.OfferId, was.Accept), Is.EqualTo((asks[1].id, accept)), "The keyboard is on the second card's '" + caption + "'.");
                RenderHudForTheCurrentCanvas();
                Assert.That(held.gameObject.activeInHierarchy, Is.False, "The rebuild threw that control away,");
                // The HUD hands the keyboard back as the frame ends.
                yield return null;
                var now = EventSystem.current.currentSelectedGameObject;
                Assert.That(now != null && now.activeInHierarchy && now.name == caption, Is.True,
                    "and the keyboard is on a live '" + caption + "' again: " + (now != null ? now.name : "nothing") + ",");
                Assert.That(Hud.TryDescribeOfferControl(now.GetComponent<Button>(), out var row), Is.True, "the offer's control,");
                Assert.That((row.OfferId, row.Accept), Is.EqualTo((asks[1].id, accept)), "the second card's, as it was, not the first card's of that caption.");
            }
        }

        /// <summary>
        /// Once the player has given one nominee their word on the veto, the other's card says why its yes is locked - the
        /// rows' own words, in a label of its own, never in the caption - over a yes drawn as the house draws a locked
        /// option, still the offer's described control, which commits nothing pressed; the card's no is the player's still.
        /// </summary>
        [UnityTest]
        public IEnumerator OfferBoard_ALockedCardSaysWhyInALabelOfItsOwnOverADisabledYes()
        {
            yield return InstallOfferBoardMeeting(true);
            yield return OpenStation();
            LandThePanel();
            yield return null;
            var asks = RoleOffers.VetoAsks(director.Snapshot);
            var first = OfferButton(OfferCards(LastActive(EpisodeHud.OfferBoardName))[0], EpisodeHud.DealAcceptCaption);
            ScrollIntoView(first);
            AssertPressable(first, EpisodeHud.DealAcceptCaption);
            first.onClick.Invoke();
            var after = director.Snapshot;
            yield return null; yield return null;

            Assert.That(after.deals.Single(deal => deal.id == asks[0].id).status, Is.EqualTo(DealStatus.Active), "The first yes is taken.");
            var card = OfferCards(LastActive(EpisodeHud.OfferBoardName)).Single();
            Assert.That(card.name, Is.EqualTo(EpisodeHud.OfferCardPrefix + after.Find(asks[1].proposerId).name));
            string line = EpisodeDirector.VetoSavesOneLine(after, asks[0].proposerId, asks[1].proposerId);
            Assert.That(OfferLabel(card, EpisodeHud.OfferLockName), Is.EqualTo(line), "The card says why, in the rows' own words, in a label of its own.");
            var locked = OfferButton(card, EpisodeHud.DealAcceptCaption);
            Assert.That(locked.IsInteractable(), Is.False, "The veto saves one of them: a second yes is a word that cannot be kept.");
            Assert.That(locked.GetComponentsInChildren<TMP_Text>(true).Select(label => label.text), Is.EqualTo(new[] { EpisodeHud.DealAcceptCaption }),
                "The locked yes keeps its caption, and the reason is not in it.");
            Assert.That(locked.GetComponentsInChildren<TMP_Text>(true).Single().color, Is.EqualTo(UiTheme.Muted), "drawn muted,");
            Assert.That(locked.GetComponent<HudEmphasis>() == null && locked.GetComponent<HudPress>() == null, Is.True, "with no lift or dip under the pointer,");
            if (UiTheme.Icon("lock") != null)
                Assert.That(locked.transform.Find(EpisodeHud.LockMarkName) != null, Is.True, "and a lock where the icon set has one.");
            Assert.That(Hud.TryDescribeOfferControl(locked, out var row), Is.True, "A genuine disabled lock is still the offer's described control.");
            Assert.That((row.OfferId, row.Accept), Is.EqualTo((asks[1].id, true)));
            Assert.That(Hud.TryReadOfferControl(locked, out var reading), Is.True);
            Assert.That(reading, Does.EndWith(line), "A screen reader hears why, last.");

            int revision = director.Snapshot.revision;
            locked.onClick.Invoke();
            var pressed = director.Snapshot;
            yield return null;
            Assert.That(pressed.revision, Is.EqualTo(revision), "Pressed anyway, it commits nothing,");
            Assert.That(pressed.deals.Single(deal => deal.id == asks[1].id).status, Is.EqualTo(DealStatus.Proposed), "and the offer is still on the table.");

            var decline = OfferButton(OfferCards(LastActive(EpisodeHud.OfferBoardName)).Single(), EpisodeHud.DealDeclineCaption);
            ScrollIntoView(decline);
            AssertPressable(decline, EpisodeHud.DealDeclineCaption);
            decline.onClick.Invoke();
            var declined = director.Snapshot;
            yield return null; yield return null;
            Assert.That(declined.deals.Single(deal => deal.id == asks[1].id).status, Is.EqualTo(DealStatus.Declined),
                "Turning the other nominee down is still the player's to do.");
            Assert.That(ActiveRect(EpisodeHud.OfferBoardName), Is.Null, "Answered, the board goes with the offer.");
        }

        /// <summary>
        /// The Diary Room's veto column has the same board under the role moments, over the decision it keeps there; the
        /// room says an answer saves as it is pressed, and one does. Its answers are the offer's described controls.
        /// </summary>
        [UnityTest]
        public IEnumerator OfferBoard_TheDiaryRoomsVetoColumnHasTheSameBoard()
        {
            yield return InstallOfferBoardMeeting(true);
            yield return OpenDiaryFixturePanel();
            LandThePanel();
            yield return null;
            var state = director.Snapshot;
            var asks = RoleOffers.VetoAsks(state);
            Assert.That(director.StatusMessage, Is.EqualTo(EpisodeDirector.DiaryOffersInsideMessage), "The room says how its answers save.");
            var board = LastActive(EpisodeHud.OfferBoardName);
            Assert.That(board, Is.Not.Null, "The diary's veto column has the board.");
            var cards = OfferCards(board);
            Assert.That(cards.Select(card => card.name), Is.EqualTo(asks.Select(ask => EpisodeHud.OfferCardPrefix + state.Find(ask.proposerId).name)));
            foreach (var card in cards)
                foreach (var caption in new[] { EpisodeHud.DealAcceptCaption, EpisodeHud.DealDeclineCaption })
                    Assert.That(Hud.TryDescribeOfferControl(OfferButton(card, caption), out _), Is.True, card.name + ": '" + caption + "'.");
            AssertOfferBoardCaptions(board, "The diary's board");
            AssertEveryLabelDraws(board, "The diary's board");
            var keep = FindButton("Do not use the veto");
            Assert.That(keep.transform.parent, Is.SameAs(board.parent), "The decision keeps the column,");
            Assert.That(keep.transform.GetSiblingIndex(), Is.GreaterThan(board.GetSiblingIndex()), "under the board.");

            var before = director.Snapshot;
            var decline = OfferButton(cards[0], EpisodeHud.DealDeclineCaption);
            ScrollIntoView(decline);
            AssertPressable(decline, EpisodeHud.DealDeclineCaption);
            decline.onClick.Invoke();
            var declined = director.Snapshot;
            yield return null; yield return null;
            Assert.That(director.IsDiaryOpen, Is.True, "Still in the chair.");
            Assert.That(declined.deals.Single(deal => deal.id == asks[0].id).status, Is.EqualTo(DealStatus.Declined), "The no saved as it was pressed.");
            AssertTheRowsAnswer(before, declined, asks[0].id, false, "Turning a nominee down in the diary");
        }

        /// <summary>A season without the role moments keeps the diary's veto column it always had: no board, no offers, and the room's own line.</summary>
        [UnityTest]
        public IEnumerator OfferBoard_WithoutRolesTheDiaryKeepsItsColumn()
        {
            yield return InstallOfferBoardMeeting(false);
            yield return OpenDiaryFixturePanel();
            Assert.That(director.StatusMessage, Is.EqualTo(EpisodeDirector.DiaryInsideMessage));
            Assert.That(ActiveRect(EpisodeHud.OfferBoardName), Is.Null, "No board,");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.DealAcceptCaption), Is.Null, "and no offer: the diary never drew the asks.");
            Assert.That(ButtonWithCaptionOrNull("Do not use the veto"), Is.Not.Null, "The decision is where it was.");
        }
    }
}
