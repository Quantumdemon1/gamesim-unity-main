using System;
using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// A campaign conversation's way on, "Next voter: &lt;first name&gt;" (UI-UX plan D11): the next
    /// voter on the board the player has not talked to this campaign, pinned under the page's
    /// choices, so the house can be worked without walking back to the board. Pressed, it does what
    /// that voter's "Talk to" on the board does. The order and the gates are pinned without Unity
    /// (CampaignTalkTests); this is the control as built, pressed.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>Every control on the screen whose caption is a way on to a voter.</summary>
        private Button[] NextVoterControls() => director.GetComponentsInChildren<Button>(true).Where(button => button.IsActive()
            && button.GetComponentsInChildren<TMP_Text>(true).Any(text => text.text != null
                && text.text.StartsWith(CampaignTalk.NextVoterPrefix, StringComparison.Ordinal))).ToArray();

        private IEnumerator SkipAnOpeningLine()
        {
            if (!director.IsConversationExchangePlaying) yield break;
            ButtonWithCaption(EpisodeHud.ConversationSkipCaption).onClick.Invoke();
            yield return null;
        }

        /// <summary>
        /// In an eight-house campaign four houseguests vote beside the player. Talked to second voter
        /// first and then the first, the first's conversation offers the third, passing over the
        /// second; pressed, it walks to the third and opens on the vote question as the board's Talk
        /// to does, offering the fourth; and once all four are talked to there is no way on.
        /// </summary>
        [UnityTest]
        public IEnumerator CampaignNextVoter_TheWayOnNamesTheNextVoterNotYetTalkedToAndTalksToThem()
        {
            yield return InstallTalkingHouse(8, true);
            yield return SettleCast();
            var state = director.Snapshot;
            var bodies = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy).Select(npc => npc.Id).ToList();
            var voters = EpisodeEngine.Voters(state).Where(voter => !voter.isPlayer).ToList();
            Assert.That(voters.Count, Is.EqualTo(4), "An eight-house campaign: four houseguests vote beside the player.");
            foreach (var voter in voters) Assert.That(bodies, Does.Contain(voter.id), voter.name + " has a body to walk to.");
            int spent = EpisodeEngine.SocialActionsSpent(state);

            yield return TalkTo(voters[1].id);
            yield return SkipAnOpeningLine();
            Assert.That(director.TalkingToId, Is.EqualTo(voters[1].id), "A conversation with the board's second voter.");
            yield return TalkTo(voters[0].id);
            yield return EnterConversationCategory(EpisodeDirector.LearnCategoryCaption);
            const string where = "A campaign conversation with the board's first voter";
            Assert.That(NextVoterControls().Length, Is.EqualTo(1), where + " offers one way on,");
            var next = ButtonWithCaption(CampaignTalk.NextVoterCaption(voters[2].name));
            Assert.That(next.GetComponentInParent<ScrollRect>(), Is.Null, where + ": under the page's choices, not one of them,");
            var foot = ActiveRect(EpisodeHud.ConversationWayOnName);
            Assert.That(foot != null && next.transform.IsChildOf(foot), Is.True, where + ": at the column's foot.");

            next.onClick.Invoke();
            yield return null;
            Assert.That(director.WalkingToId == voters[2].id || director.TalkingToId == voters[2].id, Is.True,
                "Pressed, it walks over to " + voters[2].name + ", passing over the voter already talked to,");
            Assert.That(director.ConversationIntent, Is.EqualTo(EpisodeDirector.IntentAsk), "knowing what the board's Talk to knows.");
            if (!director.IsConversationOpen)
                yield return OpenNearbyNpc(SceneComponents<HouseNpc>().First(npc => npc.Id == voters[2].id && npc.gameObject.activeInHierarchy));
            yield return SkipAnOpeningLine();
            Assert.That(director.TalkingToId, Is.EqualTo(voters[2].id), "The conversation is open with them,");
            Assert.That(director.CurrentConversationCategory, Is.EqualTo(EpisodeDirector.LearnCategoryCaption), "on the vote question, as the board's Talk to opens it,");
            Assert.That(NextVoterControls().Length, Is.EqualTo(1));
            Assert.That(ButtonWithCaption(CampaignTalk.NextVoterCaption(voters[3].name)), Is.Not.Null, "and its way on is the last voter not talked to.");

            yield return TalkTo(voters[3].id);
            yield return EnterConversationCategory(EpisodeDirector.LearnCategoryCaption);
            Assert.That(NextVoterControls(), Is.Empty, "Every voter talked to: there is no way on.");
            Assert.That(EpisodeEngine.SocialActionsSpent(director.Snapshot), Is.EqualTo(spent), "None of it spends an action.");
            director.ClosePanels();
            yield return null;
        }
    }
}
