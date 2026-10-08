using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// One way to form an alliance on screen (ACTIONS-DEALS-ALLIANCES-PLAN C4), in a house that plays
    /// the commitment rules. The 'Propose an alliance' row carries the player's read of the
    /// invitation's odds beside what it is for, under the one note that says whose read the chances
    /// are. At three pacts the row is drawn locked under the reason and commits nothing; an
    /// invitation put to the player then has its yes locked under a line saying why, and its no still
    /// commits. Photographed in a batch run at both text sizes, on a 16:9 frame and a 4:3 one, as
    /// 'conversation-alliance-odds' and 'conversation-alliance-cap'. The engine's half is
    /// AllianceProposalTests.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The row's caption, as the walk and the conversation tests press it.</summary>
        private const string ProposeAllianceCaption = "Propose an alliance";

        /// <summary>
        /// The commitment rules, the player in a pact of two with each of the last three houseguests,
        /// and an alliance invitation on the table from every other one - whoever the conversation is
        /// held with (<see cref="Listener"/>) has one.
        /// </summary>
        private static void AtThreePacts(EpisodeState state)
        {
            EpisodeEngine.EnableCommitments(state);
            var npcs = state.Active.Where(actor => !actor.isPlayer).ToList();
            var held = npcs.Skip(npcs.Count - EpisodeEngine.PlayerPactCap).ToList();
            foreach (var partner in held)
                state.alliances.Add(new AllianceState
                {
                    id = "alliance-held-" + partner.id, name = "The " + partner.name.Split(' ')[0] + " Pact",
                    members = new List<string> { state.playerId, partner.id }, active = true,
                });
            foreach (var asking in npcs.Except(held))
                state.deals.Add(new DealState
                {
                    id = "deal-ask-" + asking.id, type = DealKind.AllianceInvite, proposerId = asking.id, recipientId = state.playerId,
                    status = DealStatus.Proposed, week = state.week, expiresWeek = state.week,
                    trustImpact = DealKind.DefaultTrust(DealKind.AllianceInvite),
                });
        }

        /// <summary>Every live object of this name under the director: a panel's rows, or its notes.</summary>
        private List<RectTransform> LiveRects(string name) => director.GetComponentsInChildren<RectTransform>(true)
            .Where(rect => rect.name == name && rect.gameObject.activeInHierarchy).ToList();

        /// <summary>The live label carrying exactly these words.</summary>
        private TMP_Text LiveLabel(string words)
        {
            var labels = director.GetComponentsInChildren<TMP_Text>(true)
                .Where(label => label.gameObject.activeInHierarchy && label.text == words).ToList();
            Assert.That(labels, Has.Count.EqualTo(1), "One line says: '" + words + "'.");
            return labels[0];
        }

        /// <summary>A line drawn directly over a control, in the same column.</summary>
        private static void AssertDirectlyOver(Component line, Component control, string where)
        {
            Assert.That(line.transform.parent, Is.SameAs(control.transform.parent), where + ": the line and the control share the column.");
            Assert.That(line.transform.GetSiblingIndex(), Is.EqualTo(control.transform.GetSiblingIndex() - 1), where + ": the line stands directly over it.");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator AllianceProposal_UnderTheRulesTheRowShowsItsOddsAndThreePactsLockIt()
        {
            foreach (bool larger in new[] { false, true })
            {
                string size = larger ? " at the larger text" : "";

                // The rules on and nothing held: the row carries the player's read of its chance.
                yield return InstallTalkingHouse(8, false, state => EpisodeEngine.EnableCommitments(state));
                yield return SettleCast();
                yield return ApplyTextSize(larger);
                var listener = Listener(director.Snapshot);
                yield return TalkTo(listener.id);
                string where = "A conversation under the commitment rules" + size;
                Assert.That(director.IsConversationOpen, Is.True, where + ": the conversation opens.");
                var open = director.Snapshot;
                Assert.That(EpisodeEngine.CommitmentRulesOn(open), Is.True, where + ": the house plays the commitment rules.");
                var row = ButtonWithCaption(ProposeAllianceCaption);
                Assert.That(TagOn(row), Is.EqualTo(EpisodeDirector.VerbTag(EpisodeCommandKind.FormAlliance) + " · " + KnownOdds.Alliance(open, listener.id).word),
                    where + ": the row says what it is for and the player's read of the invitation's odds - never the roll's own number.");
                var notes = LiveRects(EpisodeHud.OddsNoteName);
                Assert.That(notes, Has.Count.EqualTo(1), where + ": one note says whose read the conversation's chances are.");
                Assert.That(notes[0].parent, Is.SameAs(row.transform.parent), where + ": the note is in the row's column,");
                Assert.That(notes[0].GetSiblingIndex(), Is.LessThan(row.transform.GetSiblingIndex()), where + ": above the first chance it shows.");
                AssertConversationFits(larger, where);
                AssertTagsHaveRoom(where);
                yield return CaptureConversation("conversation-alliance-odds" + (larger ? "-large" : ""), where, null);
                director.ClosePanels();
                yield return null;

                // Three pacts held, and an invitation from the one the player talks to.
                yield return InstallTalkingHouse(8, false, AtThreePacts);
                yield return SettleCast();
                yield return ApplyTextSize(larger);
                listener = Listener(director.Snapshot);
                yield return TalkTo(listener.id);
                where = "A conversation at three pacts" + size;
                Assert.That(director.IsConversationOpen, Is.True, where + ": the conversation opens.");
                var held = director.Snapshot;
                Assert.That(EpisodeEngine.AtPactCap(held), Is.True, where + ": the player holds three.");
                Assert.That(held.Allied(held.playerId, listener.id), Is.False, where + ": none of them with " + listener.name + ".");
                var offer = NpcDeals.Pending(held).Single(deal => deal.proposerId == listener.id);
                Assert.That(offer.type, Is.EqualTo(DealKind.AllianceInvite));

                // The row: locked, under the reason, and pressing it commits nothing.
                var locked = FindButton(ProposeAllianceCaption);
                Assert.That(locked.interactable, Is.False, where + ": 'Propose an alliance' is drawn locked.");
                AssertDirectlyOver(LiveLabel(EpisodeEngine.PactCapRefusal), locked, where + ": the reason");
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.DealProposeCaption(DealKind.Title(DealKind.AllianceInvite).ToLowerInvariant())), Is.Null,
                    where + ": the deal table offers no invitation either.");
                int revision = director.Snapshot.revision;
                locked.onClick.Invoke();
                yield return null;
                Assert.That(director.Snapshot.revision, Is.EqualTo(revision), where + ": the locked row commits nothing.");

                // The invitation's yes: locked, under the line that says why; the no still commits.
                var accept = FindButton(EpisodeHud.DealAcceptCaption);
                Assert.That(accept.interactable, Is.False, where + ": the invitation's yes is drawn locked.");
                AssertDirectlyOver(LiveLabel(EpisodeDirector.PactCapOfferLine(held, listener.id)), accept, where + ": the line saying why");
                accept.onClick.Invoke();
                yield return null;
                Assert.That(director.Snapshot.revision, Is.EqualTo(revision), where + ": the locked yes commits nothing.");
                AssertConversationFits(larger, where);
                AssertTagsHaveRoom(where);
                yield return CaptureConversation("conversation-alliance-cap" + (larger ? "-large" : ""), where, null);

                ButtonWithCaption(EpisodeHud.DealDeclineCaption).onClick.Invoke();
                yield return null;
                var after = director.Snapshot;
                Assert.That(after.revision, Is.EqualTo(revision + 1), where + ": the no is one command,");
                Assert.That(after.deals.Single(deal => deal.id == offer.id).status, Is.EqualTo(DealStatus.Declined), "and it turns the invitation down.");
                Assert.That(EpisodeEngine.PlayerPactsHeld(after), Is.EqualTo(EpisodeEngine.PlayerPactCap), "The player still holds three.");
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);
        }
    }
}
