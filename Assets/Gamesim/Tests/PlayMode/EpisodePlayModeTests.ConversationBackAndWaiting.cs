using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The conversation's navigation after the redesign (lane S2a, PR-5 to PR-7): Back during a line, what
    /// waits under Deals and promises, and a locked yes with no cost word.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// PR-5: Escape (and B, the same chain) while a line plays skips the line, as the exchange's own
        /// Skip does - one level back, not the whole conversation. The next press leaves the category,
        /// and the one after closes the conversation.
        /// </summary>
        [UnityTest]
        public IEnumerator HudStatus_EscapeDuringALineSkipsItAndKeepsTheConversation()
        {
            yield return InstallExchangeFlowHouse();
            yield return EnterConversationCategory(EpisodeDirector.ConnectCategoryCaption);
            var before = director.Snapshot;
            ButtonWithCaption(EpisodeHud.SmallTalkCaption).onClick.Invoke();
            var committed = director.Snapshot;
            Assert.That(committed.revision, Is.EqualTo(before.revision + 1));
            byte[] savedBytes = File.ReadAllBytes(director.SavePath);
            yield return null;
            Assert.That(director.IsConversationExchangePlaying, Is.True, "The line is playing.");

            yield return PressKey(Key.Escape);
            Assert.That(director.IsConversationOpen, Is.True, "Escape during the line keeps the conversation open,");
            Assert.That(director.IsConversationExchangePlaying, Is.False, "and skips the line.");
            Assert.That(ActiveRect(EpisodeHud.ConversationExchangeLineName), Is.Null);
            Assert.That(director.CurrentConversationCategory, Is.EqualTo(EpisodeDirector.ConnectCategoryCaption),
                "It returns to the category the line was said from.");
            Assert.That(ButtonWithCaption(EpisodeDirector.ConversationTopicsBackCaption), Is.Not.Null);
            AssertExchangeFlowUnchanged(committed, savedBytes);

            yield return PressKey(Key.Escape);
            Assert.That(director.IsConversationOpen, Is.True, "The next Escape leaves the category,");
            Assert.That(director.CurrentConversationCategory, Is.Null, "for the topics.");
            Assert.That(ActiveRect(EpisodeHud.DialName), Is.Not.Null);

            yield return PressKey(Key.Escape);
            Assert.That(director.IsConversationOpen, Is.False, "And the one after that closes the conversation.");
            AssertExchangeFlowUnchanged(committed, savedBytes);
        }

        /// <summary>
        /// PR-6: the topics say when something is waiting under "Deals and promises" - an offer, a
        /// counter, a plan or a declaration this houseguest has put to the player - as a tag beside the
        /// petal, never in its caption. With nothing waiting the petal carries no tag.
        /// </summary>
        [UnityTest]
        public IEnumerator HudStatus_TheDealsTopicSaysWhenSomethingIsWaitingThere()
        {
            yield return InstallTalkingHouse(8, false);
            var listener = Listener(director.Snapshot);
            var quiet = director.Snapshot;
            Assert.That(NpcDeals.Pending(quiet).Any(deal => deal.proposerId == listener.id), Is.False, "The control has no offer waiting,");
            Assert.That(quiet.oathOpportunities.Contains(listener.id), Is.False, "and no declaration.");
            yield return TalkTo(listener.id);
            var petal = ButtonWithCaption(EpisodeDirector.DealsCategoryCaption);
            Assert.That(TagOn(petal) ?? "", Does.Not.Contain("waiting"), "Nothing waiting, nothing said.");

            yield return InstallTalkingHouse(8, false, AtThreePacts);
            listener = Listener(director.Snapshot);
            Assert.That(NpcDeals.Pending(director.Snapshot).Count(deal => deal.proposerId == listener.id), Is.EqualTo(1),
                "The fixture's houseguest has put one offer to the player.");
            yield return TalkTo(listener.id);
            Assert.That(director.CurrentConversationCategory, Is.Null, "The conversation opens on its topics.");
            petal = ButtonWithCaption(EpisodeDirector.DealsCategoryCaption);
            Assert.That(petal.GetComponentsInChildren<TMP_Text>(true).Any(text => text.text == EpisodeDirector.DealsCategoryCaption), Is.True,
                "The caption is unchanged.");
            Assert.That(TagOn(petal), Is.EqualTo("1 waiting"), "The petal says one thing is waiting there.");
            foreach (string other in PetalCaptions.Where(caption => caption != EpisodeDirector.DealsCategoryCaption))
                Assert.That(TagOn(ButtonWithCaption(other)) ?? "", Does.Not.Contain("waiting"), other + " says nothing of it.");
            AssertEveryLabelDraws(ActiveRect(EpisodeHud.DialName), "The topics with an offer waiting");
        }

        /// <summary>
        /// PR-6, counted and at every size: an offer and a loyalty declaration from the same houseguest
        /// read "2 waiting" on the Deals petal at the standard, larger and largest text, in the full HUD
        /// and in the compact one, with every label on the topics drawn whole.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator HudStatus_TheDealsTopicCountsWhatWaitsAtEveryTextSize()
        {
            string talker = null;
            yield return InstallTalkingHouse(8, false, state =>
            {
                AtThreePacts(state);
                talker = state.Active.First(c => !c.isPlayer && c.id != state.hohId && !state.nominees.Contains(c.id)
                    && c.id != state.vetoHolderId).id;
                // A loyalty declaration on offer as the engine leaves one: its milestone shown, not yet sworn.
                state.shownOathMilestones.Add(talker);
                state.oathOpportunities.Add(talker);
            });
            var listener = Listener(director.Snapshot);
            Assert.That(listener.id, Is.EqualTo(talker), "The declaration is the listener's.");
            Assert.That(NpcDeals.Pending(director.Snapshot).Count(deal => deal.proposerId == listener.id), Is.EqualTo(1), "One offer,");
            Assert.That(director.Snapshot.oathOpportunities, Does.Contain(listener.id), "and one declaration.");
            try
            {
                foreach (bool compact in new[] { false, true })
                    foreach (var size in TextSizes.All)
                    {
                        yield return ApplyTextSize(size);
                        director.SetCompactHud(compact);
                        yield return TalkTo(listener.id);
                        string where = (compact ? "The compact HUD " : "The full HUD ") + TextScales.At(size);
                        Assert.That(director.CurrentConversationCategory, Is.Null, where + ": the conversation opens on its topics.");
                        var petal = ButtonWithCaption(EpisodeDirector.DealsCategoryCaption);
                        Assert.That(TagOn(petal), Is.EqualTo(EpisodeDirector.WaitingTag(2)), where + ": the petal counts both.");
                        AssertEveryLabelDraws(ActiveRect(EpisodeHud.DialName), where + ", the topics with two things waiting");
                        director.ClosePanels();
                        yield return null;
                    }
            }
            finally { if (director != null) director.SetCompactHud(false); }
            yield return ApplyTextSize(TextSize.Standard);
        }

        /// <summary>
        /// PR-7: an invited offer's yes that the engine would refuse is drawn locked and carries no
        /// "free" - a locked control is not a move the player can make. The no keeps its tag.
        /// </summary>
        [UnityTest]
        public IEnumerator HudStatus_AnInvitedOffersLockedYesIsNotTaggedFree()
        {
            yield return InstallTalkingHouse(8, false, AtThreePacts);
            var listener = Listener(director.Snapshot);
            var offer = NpcDeals.Pending(director.Snapshot).Single(deal => deal.proposerId == listener.id);
            Assert.That(EpisodeEngine.InvitationPastPactCap(director.Snapshot, listener.id), Is.True, "The yes would be refused.");
            yield return TalkTo(listener.id);
            yield return EnterConversationCategory(EpisodeDirector.DealsCategoryCaption);
            // The invitation's own way in (HearNpcInvitation) opens Deals on this offer; set it as that does.
            NpcWrite("invitedDealId", offer.id);
            typeof(EpisodeDirector).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, null);
            yield return null;
            Assert.That(director.InvitedDealRecordId, Is.EqualTo(offer.id));
            Assert.That(ShownText(), Does.Contain("THEIR OFFER"), "The invited offer's own view is drawn.");
            var accept = FindButton(EpisodeHud.DealAcceptCaption);
            Assert.That(accept.interactable, Is.False, "The yes is drawn locked.");
            Assert.That(TagOn(accept) ?? "", Does.Not.Contain(EpisodeDirector.FreeTag), "A locked yes is not tagged free.");
            Assert.That(TagOn(ButtonWithCaption(EpisodeHud.DealDeclineCaption)), Does.Contain(EpisodeDirector.FreeTag), "The no still is.");
        }
    }
}
