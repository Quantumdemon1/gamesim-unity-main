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
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The Head of Household's card on the offer board (ROLE-MOMENTS-PLAN R4, §3.3, §5.1): under the role moments the Head of
    /// Household's "Don't Use the Veto" stands after the nominees' cards, at the veto decision and in the Diary Room's veto
    /// column - their face under the crown with the HOH pill, the chip and its line, the tone, "Head of Household", the read,
    /// their keep-the-block line, the terms, the stakes and what a no and silence cost, each in a label of its own over the
    /// two answers every card has, captioned as they always were and described as the offer's controls; a screen reader hears
    /// the caption, "Name, Head of Household, don't use the veto" and the card's line. A yes to it locks the nominees' yes,
    /// each saying why, and a nominee's yes locks it.
    ///
    /// <para>The meeting is <see cref="OfferBoardSeason"/>'s, with the Head of Household warm enough toward the player to
    /// bargain and planning no backdoor, so the advance into the meeting files their ask after the nominees'.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// <see cref="OfferBoardSeason"/>'s meeting at Roles, its Head of Household warm toward the player and planning no
        /// backdoor: both nominees ask and the Head of Household asks after them. A seed whose Head of Household plans a
        /// backdoor - who never asks - is passed over.
        /// </summary>
        private static EpisodeState KeepTheBlockSeason()
        {
            for (uint seed = 7001; seed < 7041; seed++)
            {
                var start = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(start);
                start.story.rulesVersion = StoryRules.Roles;
                var walk = new EpisodeEngine(start);
                for (int i = 0; i < 200 && walk.Snapshot.phase != EpisodePhase.Veto; i++)
                {
                    var step = walk.Apply(NextCommand(walk.Snapshot));
                    Assert.That(step.accepted, Is.True, "seed " + seed + ": " + step.reason);
                }
                var s = walk.Snapshot;
                if (s.phase != EpisodePhase.Veto || s.competitionResolved || s.hohId == s.playerId || s.nominees.Contains(s.playerId)
                    || EpisodeEngine.BackdoorPlanned(s) != null) continue;
                string first = s.nominees[0], second = s.nominees[1];
                s.Find(first).traits = new List<string> { "Competitive", "Stubborn" };
                s.Find(second).traits = new List<string> { "Emotional" };
                s.Find(s.hohId).traits = new List<string> { "Analytical" };
                foreach (var (holder, toward, score) in new[] { (first, s.playerId, 40.0), (second, s.playerId, 60.0), (s.hohId, s.playerId, 50.0),
                             (s.playerId, first, 0.0), (s.playerId, second, 0.0), (s.playerId, s.hohId, 0.0) })
                {
                    var edge = s.relationships.FirstOrDefault(r => r.fromId == holder && r.toId == toward);
                    if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = holder, toId = toward });
                    edge.score = score;
                }
                s.vetoHolderId = s.playerId;
                if (!s.vetoPlayers.Contains(s.playerId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.playerId;
                s.competitionResolved = true;
                s.competitionScores = s.vetoPlayers.Select((id, i) => new CompetitionScore { contestantId = id, score = id == s.playerId ? 100 : i }).ToList();
                var meeting = new EpisodeEngine(s).Apply(new EpisodeCommand
                {
                    id = "keep-the-block-meeting", actorId = s.playerId, kind = EpisodeCommandKind.Advance,
                    expectedRevision = s.revision, expectedPhase = s.phase,
                });
                Assert.That(meeting.accepted, Is.True, "seed " + seed + ": " + meeting.reason);
                Assert.That(meeting.state.phase, Is.EqualTo(EpisodePhase.VetoMeeting));
                Assert.That(RoleOffers.BoardAsks(meeting.state).Select(ask => ask.proposerId), Is.EqualTo(new[] { first, second, s.hohId }),
                    "Both nominees asked, in the order they were named, and the Head of Household after them.");
                return meeting.state;
            }
            Assert.Fail("No seed reached a veto the player could be handed with a Head of Household who plans no backdoor.");
            return null;
        }

        /// <summary>Installs <see cref="KeepTheBlockSeason"/>, saved and loaded, with the house's own clock held so nothing commits under the test.</summary>
        private IEnumerator InstallKeepTheBlockMeeting()
        {
            var state = KeepTheBlockSeason();
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
        }

        /// <summary>
        /// The Head of Household's card stands after the nominees', every word its own label and both answers the offer's
        /// described controls under their unchanged captions, read as "Name, Head of Household, don't use the veto"; its yes
        /// commits exactly what the engine's does, and then the nominees' cards say why their yes is locked, over a yes that
        /// cannot be pressed.
        /// </summary>
        [UnityTest]
        public IEnumerator OfferBoard_TheHeadOfHouseholdsCardStandsAfterTheNomineesAndItsYesLocksTheirs()
        {
            yield return InstallKeepTheBlockMeeting();
            yield return OpenStation();
            LandThePanel();
            yield return null;
            var state = director.Snapshot;
            var asks = RoleOffers.BoardAsks(state);
            var hold = asks.Last();
            Assert.That(hold.type, Is.EqualTo(DealKind.VetoHold));
            var board = LastActive(EpisodeHud.OfferBoardName);
            Assert.That(board, Is.Not.Null, "The asks are a board.");
            var cards = OfferCards(board);
            Assert.That(cards.Select(card => card.name), Is.EqualTo(asks.Select(ask => EpisodeHud.OfferCardPrefix + state.Find(ask.proposerId).name)),
                "A card an ask: the nominees in the order they were named, then the Head of Household.");
            AssertOfferBoardCaptions(board, "The board with the Head of Household's card");
            AssertNoBoardTypeUnderTwelvePixels(board, "The board with the Head of Household's card");

            var words = RoleOffers.KeepTheBlockCard(state, hold);
            var card = cards.Last();
            string who = words.name;
            Assert.That(OfferLabel(card, EpisodeHud.OfferNameName), Is.EqualTo(who));
            Assert.That(OfferLabel(card, EpisodeHud.OfferChipName), Is.EqualTo("DON'T USE THE VETO"), who);
            Assert.That(OfferLabel(card, EpisodeHud.OfferChipLineName), Is.EqualTo(RoleOffers.KeepTheBlockLine), who);
            Assert.That(OfferLabel(card, EpisodeHud.OfferToneWordName), Is.EqualTo(RoleTone.Calculated), who + " reads Calculated (Analytical).");
            Assert.That(OfferLabel(card, EpisodeHud.OfferSituationName), Is.EqualTo("Head of Household"), who);
            Assert.That(OfferLabel(card, EpisodeHud.OfferReadName), Is.EqualTo(words.read), who);
            Assert.That(RoleQuotes.Lines(RoleQuotes.KeepTheBlock, RoleTone.Calculated).Select(line => "“" + line + "”"), Does.Contain(OfferLabel(card, EpisodeHud.OfferQuoteName)),
                who + " says a keep-the-block line.");
            Assert.That(OfferLabel(card, EpisodeHud.OfferTermsName), Is.EqualTo("In return: nothing promised"), who);
            Assert.That(OfferLabel(card, EpisodeHud.OfferStakesName), Is.EqualTo("Keep it: they remember it. Break it: the house may hear. Veto anyway: personal."), who);
            Assert.That(OfferLabel(card, EpisodeHud.OfferAnswersName), Is.EqualTo(words.answers), who);
            Assert.That(OfferLabel(card, EpisodeHud.OfferLockName), Is.Null, who + "'s yes is open.");
            var photo = card.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Photo");
            Assert.That(photo.GetComponentsInChildren<CharacterPortraitBinding>(true), Has.Length.EqualTo(1), who + "'s face is bound to the portrait studio.");
            Assert.That(photo.GetComponentsInChildren<TMP_Text>().Select(label => label.text), Is.EqualTo(new[] { "HOH" }),
                who + "'s face carries the crown's word and no initials.");
            var hud = Hud;
            foreach (bool accept in new[] { true, false })
            {
                string caption = accept ? EpisodeHud.DealAcceptCaption : EpisodeHud.DealDeclineCaption;
                var button = OfferButton(card, caption);
                Assert.That(button.GetComponentsInChildren<TMP_Text>(true).Select(label => label.text), Is.EqualTo(new[] { caption }), who + ": '" + caption + "', unchanged.");
                Assert.That(hud.TryDescribeOfferControl(button, out var row), Is.True, who + ": '" + caption + "' is the offer's control.");
                Assert.That((row.OfferId, row.ProposerId, row.RecipientId, row.Accept), Is.EqualTo((hold.id, hold.proposerId, state.playerId, accept)));
                Assert.That(hud.TryReadOfferControl(button, out var reading), Is.True);
                Assert.That(reading, Is.EqualTo(RoleOffers.Spoken(caption, words)));
                Assert.That(reading, Does.StartWith(caption + ". " + who + ", Head of Household, don't use the veto. Tone: Calculated."));
            }

            var before = director.Snapshot;
            var yes = OfferButton(card, EpisodeHud.DealAcceptCaption);
            ScrollIntoView(yes);
            AssertPressable(yes, EpisodeHud.DealAcceptCaption);
            yes.onClick.Invoke();
            var accepted = director.Snapshot;
            yield return null; yield return null;
            Assert.That(accepted.deals.Single(deal => deal.id == hold.id).status, Is.EqualTo(DealStatus.Active));
            AssertTheRowsAnswer(before, accepted, hold.id, true, "Taking the Head of Household's ask");

            board = LastActive(EpisodeHud.OfferBoardName);
            cards = OfferCards(board);
            Assert.That(cards.Select(c => c.name), Is.EqualTo(asks.Take(2).Select(ask => EpisodeHud.OfferCardPrefix + accepted.Find(ask.proposerId).name)),
                "Answered, the Head of Household's card leaves; the nominees' stay.");
            for (int i = 0; i < cards.Length; i++)
            {
                string line = Negotiation.BlockKeptLine(accepted, hold.proposerId, asks[i].proposerId);
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferLockName), Is.EqualTo(line), cards[i].name + " says why, in a label of its own.");
                var locked = OfferButton(cards[i], EpisodeHud.DealAcceptCaption);
                Assert.That(locked.IsInteractable(), Is.False, cards[i].name + ": the player's word on the veto is given.");
                Assert.That(locked.GetComponentsInChildren<TMP_Text>(true).Select(label => label.text), Is.EqualTo(new[] { EpisodeHud.DealAcceptCaption }));
                Assert.That(OfferButton(cards[i], EpisodeHud.DealDeclineCaption).IsInteractable(), Is.True, cards[i].name + ": the no stays the player's.");
            }
            int revision = director.Snapshot.revision;
            OfferButton(cards[0], EpisodeHud.DealAcceptCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Pressed anyway, a locked yes commits nothing.");
        }

        /// <summary>A nominee's yes locks the Head of Household's card the other way round, in a label of its own over a disabled yes.</summary>
        [UnityTest]
        public IEnumerator OfferBoard_ANomineesYesLocksTheHeadOfHouseholdsCard()
        {
            yield return InstallKeepTheBlockMeeting();
            yield return OpenStation();
            LandThePanel();
            yield return null;
            var asks = RoleOffers.BoardAsks(director.Snapshot);
            var first = OfferButton(OfferCards(LastActive(EpisodeHud.OfferBoardName))[0], EpisodeHud.DealAcceptCaption);
            ScrollIntoView(first);
            AssertPressable(first, EpisodeHud.DealAcceptCaption);
            first.onClick.Invoke();
            var after = director.Snapshot;
            yield return null; yield return null;
            Assert.That(after.deals.Single(deal => deal.id == asks[0].id).status, Is.EqualTo(DealStatus.Active));
            var card = OfferCards(LastActive(EpisodeHud.OfferBoardName)).Last();
            Assert.That(card.name, Is.EqualTo(EpisodeHud.OfferCardPrefix + after.Find(asks[2].proposerId).name));
            string line = Negotiation.VetoGivenLine(after, asks[0].proposerId, asks[2].proposerId);
            Assert.That(OfferLabel(card, EpisodeHud.OfferLockName), Is.EqualTo(line));
            var locked = OfferButton(card, EpisodeHud.DealAcceptCaption);
            Assert.That(locked.IsInteractable(), Is.False);
            Assert.That(Hud.TryReadOfferControl(locked, out var reading), Is.True);
            Assert.That(reading, Does.EndWith(line), "A screen reader hears why, last.");
        }

        /// <summary>The Diary Room's veto column has the Head of Household's card after the nominees', and its no saves as it is pressed.</summary>
        [UnityTest]
        public IEnumerator OfferBoard_TheDiaryRoomsVetoColumnHasTheHeadOfHouseholdsCard()
        {
            yield return InstallKeepTheBlockMeeting();
            yield return OpenDiaryFixturePanel();
            LandThePanel();
            yield return null;
            var state = director.Snapshot;
            var asks = RoleOffers.BoardAsks(state);
            Assert.That(director.StatusMessage, Is.EqualTo(EpisodeDirector.DiaryOffersInsideMessage));
            var board = LastActive(EpisodeHud.OfferBoardName);
            Assert.That(board, Is.Not.Null, "The diary's veto column has the board.");
            var cards = OfferCards(board);
            Assert.That(cards.Select(card => card.name), Is.EqualTo(asks.Select(ask => EpisodeHud.OfferCardPrefix + state.Find(ask.proposerId).name)));
            AssertOfferBoardCaptions(board, "The diary's board");
            AssertEveryLabelDraws(board, "The diary's board");

            var before = director.Snapshot;
            var decline = OfferButton(cards.Last(), EpisodeHud.DealDeclineCaption);
            ScrollIntoView(decline);
            AssertPressable(decline, EpisodeHud.DealDeclineCaption);
            decline.onClick.Invoke();
            var declined = director.Snapshot;
            yield return null; yield return null;
            Assert.That(director.IsDiaryOpen, Is.True, "Still in the chair.");
            Assert.That(declined.deals.Single(deal => deal.id == asks[2].id).status, Is.EqualTo(DealStatus.Declined), "The no saved as it was pressed.");
            AssertTheRowsAnswer(before, declined, asks[2].id, false, "Turning the Head of Household down in the diary");
        }
    }
}
