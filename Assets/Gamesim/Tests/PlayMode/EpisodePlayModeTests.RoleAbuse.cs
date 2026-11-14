using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The role moments' abuse checklist on the director's boards (ROLE-MOMENTS-PLAN R7 (5); the R7 build plan's C2): (6) a load
    /// mid-board - the veto board's yes kept from before a reload commits nothing, and the reloaded board's own yes commits the
    /// rows' answer; (8) a failed save - a board yes and a pitch promise whose durable write fails before the save is replaced
    /// commit nothing, leave the save's bytes as they were and lock no other card, and a new press commits. The boards are the
    /// role moments' own fixtures (<see cref="InstallOfferBoardMeeting"/> at Roles, <see cref="InstallPitchBoard"/> at Roles),
    /// each opting in by assignment, and the failure is the durable-save hook every decision's tests inject
    /// (<see cref="FailUiWriteBeforeReplacement"/>). <c>RoleAbuseTests</c> holds the rest of the checklist, in the engine.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The veto board's first yes, kept from before a load: pressed after <see cref="EpisodeDirector.LoadNow"/>, it commits
        /// nothing, in memory or on disk; the reloaded board's own yes commits exactly what the rows' answer commits.
        /// </summary>
        [UnityTest]
        public IEnumerator RoleAbuse_AReloadRetiresTheBoardsOldYesAndItsOwnYesCommits()
        {
            yield return InstallOfferBoardMeeting(true);
            yield return OpenStation();
            LandThePanel();
            yield return null;
            var asks = RoleOffers.VetoAsks(director.Snapshot);
            var board = LastActive(EpisodeHud.OfferBoardName);
            Assert.That(board, Is.Not.Null, "Under the role moments the asks are a board.");
            var old = OfferButton(OfferCards(board)[0], EpisodeHud.DealAcceptCaption).onClick;

            director.LoadNow();
            HoldTheHouseForTheFixture();
            yield return OpenStation();
            LandThePanel();
            yield return null;
            var reloaded = director.Snapshot;
            Assert.That(RoleOffers.VetoAsks(reloaded).Select(ask => ask.id), Is.EqualTo(asks.Select(ask => ask.id)), "The reload shows the same asks.");
            old.Invoke();
            AssertIntentDurable(reloaded);

            board = LastActive(EpisodeHud.OfferBoardName);
            Assert.That(board, Is.Not.Null, "The board is still up after the old yes.");
            var yes = OfferButton(OfferCards(board)[0], EpisodeHud.DealAcceptCaption);
            ScrollIntoView(yes);
            AssertPressable(yes, EpisodeHud.DealAcceptCaption);
            yes.onClick.Invoke();
            var accepted = director.Snapshot;
            yield return null;
            AssertTheRowsAnswer(reloaded, accepted, asks[0].id, true, "The reloaded board's yes");
        }

        /// <summary>
        /// A yes on the veto board whose durable save fails before the save is replaced: nothing is committed, the save's bytes are
        /// as they were, the other card's yes is open - no word was given to lock it - and a new press commits the rows' answer.
        /// </summary>
        [UnityTest]
        public IEnumerator RoleAbuse_AFailedSaveOnABoardYesCommitsNothingAndLocksNoOtherCard()
        {
            yield return InstallOfferBoardMeeting(true);
            yield return OpenStation();
            LandThePanel();
            yield return null;
            var before = director.Snapshot;
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            var asks = RoleOffers.VetoAsks(before);
            var board = LastActive(EpisodeHud.OfferBoardName);
            var yes = OfferButton(OfferCards(board)[0], EpisodeHud.DealAcceptCaption);
            ScrollIntoView(yes);
            AssertPressable(yes, EpisodeHud.DealAcceptCaption);
            FailUiWriteBeforeReplacement(yes.onClick.Invoke);
            AssertIntentDurable(before);
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytes), "The save's bytes are as they were.");
            yield return null;

            board = LastActive(EpisodeHud.OfferBoardName);
            Assert.That(board, Is.Not.Null, "The board is still up.");
            var cards = OfferCards(board);
            Assert.That(cards, Has.Length.EqualTo(asks.Count), "Every ask is still a card.");
            Assert.That(OfferLabel(cards[1], EpisodeHud.OfferLockName), Is.Null, "No word was given, so nothing locks the other card's yes,");
            Assert.That(OfferButton(cards[1], EpisodeHud.DealAcceptCaption).IsInteractable(), Is.True, "and it can be pressed.");
            var again = OfferButton(cards[0], EpisodeHud.DealAcceptCaption);
            ScrollIntoView(again);
            AssertPressable(again, EpisodeHud.DealAcceptCaption);
            again.onClick.Invoke();
            var accepted = director.Snapshot;
            yield return null;
            AssertTheRowsAnswer(before, accepted, asks[0].id, true, "A new press after the failed save");
        }

        /// <summary>
        /// A promise to a pitcher whose durable save fails before the save is replaced: nothing is committed, the save's bytes are as
        /// they were, every card is still on the board with its promise open, and a new press commits the engine's answer.
        /// </summary>
        [UnityTest]
        public IEnumerator RoleAbuse_AFailedSaveOnAPitchPromiseCommitsNothingAndLocksNoOtherCard()
        {
            yield return InstallPitchBoard(true);
            yield return OpenStation();
            LandThePanel();
            yield return null;
            var before = director.Snapshot;
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            var pitches = RoleOffers.PitchBoardCards(before);
            string promise = EpisodeHud.ReplyCaption(ReplyCards.Find(ReplyCards.Pitch, HoHPitches.PromiseKey).Label);
            var board = LastActive(EpisodeHud.OfferBoardName);
            var press = OfferButton(OfferCards(board)[0], promise);
            ScrollIntoView(press);
            AssertPressable(press, promise);
            FailUiWriteBeforeReplacement(press.onClick.Invoke);
            AssertIntentDurable(before);
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytes), "The save's bytes are as they were.");
            yield return null;

            board = LastActive(EpisodeHud.OfferBoardName);
            Assert.That(board, Is.Not.Null, "The board is still up.");
            var cards = OfferCards(board);
            Assert.That(cards, Has.Length.EqualTo(pitches.Count), "Every pitch is still a card.");
            foreach (var card in cards)
            {
                Assert.That(OfferLabel(card, EpisodeHud.OfferLockName), Is.Null, card.name + "'s promise is not locked,");
                Assert.That(OfferButton(card, promise).IsInteractable(), Is.True, card.name + "'s promise can be pressed.");
            }
            var again = OfferButton(cards[0], promise);
            ScrollIntoView(again);
            AssertPressable(again, promise);
            again.onClick.Invoke();
            var promised = director.Snapshot;
            yield return null;
            AssertThePitchAnswer(before, promised, pitches[0].id, HoHPitches.PromiseKey, "A new press after the failed save");
        }
    }
}
