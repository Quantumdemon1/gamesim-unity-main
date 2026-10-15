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
    /// The NPC invitation bubble and the Story Pull (lane S2a, D18): captions of the bubble's own, and the
    /// two never on screen together.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// D18: the invitation bubble has captions of its own, and it and the Story Pull never share the
        /// screen: whichever is up keeps the floor until it is answered. Here the invitation is up when a
        /// walk-in is found; the Pull waits, and comes up once the invitation is turned down - and no
        /// other invitation comes up over it.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator HudStatus_TheInvitationBubbleAndTheStoryPullNeverShareTheScreen()
        {
            var pullCaptions = new[] { EpisodeHud.StepInCaption, EpisodeHud.StayOutCaption, EpisodeHud.HearThemOutCaption,
                EpisodeHud.NotNowCaption, EpisodeHud.JoinMeetingCaption, EpisodeHud.TakeItOnCaption };
            foreach (string own in new[] { HouseNpcInvitationBubble.HearCaption, HouseNpcInvitationBubble.NotNowCaption })
                Assert.That(pullCaptions, Has.No.Member(own), "The bubble's '" + own + "' is a caption of its own.");
            Assert.That(HouseNpcInvitationBubble.HearCaption, Is.Not.EqualTo(HouseNpcInvitationBubble.NotNowCaption));

            var state = ContentCatalog.Create(6123);
            state.dealRulesStartWeek = 1;
            foreach (var edge in state.relationships) edge.score = 0;
            foreach (var npc in state.Active.Where(person => !person.isPlayer))
            {
                state.relationships.Single(edge => edge.fromId == npc.id && edge.toId == state.playerId).score = 10;
                state.relationships.Single(edge => edge.fromId == state.playerId && edge.toId == npc.id).score = 60;
            }
            state.relationships.Single(edge => edge.fromId == ContentCatalog.MayaId && edge.toId == state.playerId).score = 90;
            state.relationships.Single(edge => edge.fromId == "jamie-roberts" && edge.toId == state.playerId).score = 35;
            NpcDeals.Propose(state);
            Assert.That(NpcDeals.Pending(state).Count, Is.GreaterThanOrEqualTo(2), "More than one offer could come to the player.");
            state.socialActions = EpisodeEngine.SocialActionBudget(state);
            // The story system on, with nothing open: a Pull comes only from the walk-in this test stages.
            EpisodeEngine.EnableStory(state, state.week);
            Assert.That(EpisodeEngine.OpenStoryBeats(state), Is.Empty);
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            Assert.That(NpcRead<bool>("npcDiagnosticsSuspended"), Is.False, "The house's own world runs: it is what brings the invitation.");
            // The walk-in watch's clock held off, so the staged walk-in is the only one offered.
            NpcWrite("proximityWatch", -1e6f);
            Assert.That(director.ShowOverview(), Is.True);
            yield return WaitForVisibleNpcInvitation();
            var bubble = director.GetComponent<HouseNpcInvitationBubble>();
            Assert.That(FindButton(HouseNpcInvitationBubble.HearCaption), Is.SameAs(bubble.HearControl), "One control wears the bubble's yes.");
            Assert.That(FindButton(HouseNpcInvitationBubble.NotNowCaption), Is.SameAs(bubble.NotNowControl), "One control wears its no.");

            var now = director.Snapshot;
            var npcs = now.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).OrderBy(id => id, StringComparer.Ordinal).ToList();
            string first = null, second = null, room = null;
            for (int a = 0; a < npcs.Count && first == null; a++)
                for (int b = a + 1; b < npcs.Count && first == null; b++)
                    foreach (var where in RoomWords.Rooms)
                        if (EpisodeEngine.ProximityOpen(now, npcs[a], npcs[b], where)) { first = npcs[a]; second = npcs[b]; room = where; break; }
            Assert.That(first, Is.Not.Null, "Some pair could be walked in on.");
            Assert.That(director.StageWalkInForDiagnostics(first, second, room), Is.True, "The walk-in is staged.");
            NpcWrite("proximityWatch", -1e6f);

            int together = 0, frames = 0;
            void Sample()
            {
                frames++;
                bool pull = director.PullOffered != null || ActiveRect(EpisodeHud.PullCardName) != null;
                if (director.NpcInvitationIsVisible && pull) together++;
            }
            for (int frame = 0; frame < 30; frame++) { yield return null; Sample(); }
            Assert.That(together, Is.Zero, "The bubble and the Pull were on screen together on " + together + " of " + frames + " frames.");
            Assert.That(director.NpcInvitationIsVisible, Is.True, "The invitation already up keeps the floor,");
            Assert.That(director.PullOffered, Is.Null, "and the walk-in's Pull waits.");

            bubble.NotNowControl.onClick.Invoke();
            Assert.That(director.NpcInvitationIsVisible, Is.False, "Turned down, the bubble goes.");
            float until = Time.realtimeSinceStartup + 2.5f;
            while (Time.realtimeSinceStartup < until) { yield return null; Sample(); }
            Assert.That(together, Is.Zero, "The bubble and the Pull were on screen together on " + together + " of " + frames + " frames.");
            Assert.That(director.PullOffered, Is.EqualTo(EpisodeHud.StepInCaption), "The walk-in's Pull takes its turn,");
            Assert.That(ActiveRect(EpisodeHud.PullCardName), Is.Not.Null, "on screen,");
            Assert.That(director.NpcInvitationIsVisible, Is.False, "and no other invitation comes up over it.");
        }
    }
}
