using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// A pact meets in any private room (ACTIONS-DEALS-ALLIANCES-PLAN C6), in a house that plays the
    /// commitment rules. Talking to a pact-mate in a room where nobody listens offers 'Hold an alliance
    /// meeting' - in the bedroom as in the backyard, where it stands in for the old 'Go over the plan in
    /// private' rather than beside it - and an open room offers no meeting. Pressed in a vote week it is
    /// one command: the pact's line, one ally's claim, and the row gone for the week. Without the rules
    /// the backyard's word with one ally is offered as it always was, and no meeting anywhere.
    /// Photographed in a batch run at both text sizes, on a 16:9 frame and a 4:3 one, as
    /// 'conversation-alliance-meeting'. The engine's half is AlliesShareIntelTests.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private const string MeetingPactId = "alliance-meeting-test", MeetingPactName = "The Meeting Pact";

        /// <summary>
        /// The story's staging rules, so the room acts are played, the commitment rules when
        /// <paramref name="rules"/>, and the player in one pact with every houseguest who votes this
        /// week, everybody in it warm on everybody else.
        /// </summary>
        private static System.Action<EpisodeState> PactOfEveryVoter(bool rules) => state =>
        {
            EpisodeEngine.EnableStory(state);
            if (rules) EpisodeEngine.EnableCommitments(state);
            var voters = EpisodeEngine.Voters(state).Where(voter => !voter.isPlayer).Select(voter => voter.id).ToList();
            state.alliances.RemoveAll(pact => pact.members.Contains(state.playerId));
            state.alliances.Add(new AllianceState
            {
                id = MeetingPactId, name = MeetingPactName, active = true,
                members = new List<string> { state.playerId }.Concat(voters).ToList(),
            });
            var members = new List<string> { state.playerId }.Concat(voters).ToList();
            foreach (string from in members)
                foreach (string to in members.Where(id => id != from))
                {
                    var edge = state.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
                    if (edge == null) state.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
                    edge.score = 40;
                }
        };

        /// <summary>
        /// Opens a conversation with <paramref name="id"/> in <paramref name="room"/>: their body put on
        /// the floor of that room, the player put beside them inside it, and the conversation opened
        /// once the house has read where the player stands - the room the conversation's acts are
        /// offered by.
        /// </summary>
        private IEnumerator TalkInRoom(string id, string room)
        {
            director.ClosePanels();
            yield return null;
            Assert.That(HouseRoomQuery.TryCreate(player.gameObject.scene, out var rooms, out var why), Is.True, why);
            var marker = SceneComponents<HouseRoomMarker>().FirstOrDefault(each => each.isActiveAndEnabled && each.RoomName == room);
            Assert.That(marker != null, Is.True, "The house has its " + room + ".");
            var npc = SceneComponents<HouseNpc>().First(body => body.Id == id && body.gameObject.activeInHierarchy);
            var offsets = new[] { Vector3.zero, new Vector3(1f, 0f, 0f), new Vector3(-1f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -1f) };
            foreach (var offset in offsets)
            {
                if (!NavMesh.SamplePosition(marker.transform.position + offset, out var spot, 1.5f, player.Agent.areaMask)) continue;
                if (!rooms.TryLocate(spot.position, player.Agent.radius, out var theirs) || theirs != room) continue;
                if (!PutBodyAt(npc, spot.position)) continue;
                Physics.SyncTransforms();
                for (int direction = 0; direction < 8; direction++)
                {
                    float angle = direction * Mathf.PI / 4f;
                    var candidate = npc.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.7f;
                    if (!NavMesh.SamplePosition(candidate, out var hit, 0.5f, player.Agent.areaMask)) continue;
                    if (!rooms.TryLocate(hit.position, player.Agent.radius, out var mine) || mine != room) continue;
                    if (!player.Agent.Warp(hit.position)) continue;
                    player.Agent.ResetPath();
                    Physics.SyncTransforms();
                    // The house reads where the player stands a few times a second, while it is the view.
                    float by = Time.realtimeSinceStartup + 3f;
                    while (director.PlayerRoom != room && Time.realtimeSinceStartup < by) yield return null;
                    if (director.PlayerRoom != room || !director.TryOpenNpc(id)) continue;
                    yield return null;
                    yield break;
                }
            }
            Assert.Fail("The player could not be put beside " + npc.DisplayName + " in the " + room + " and talk to them there.");
        }

        /// <summary>
        /// Moves a houseguest's body: through their navigation where the house has it bound, and as the
        /// static body it is otherwise - a held house releases every body's motion, and nobody walks it
        /// back while the test talks to them.
        /// </summary>
        private static bool PutBodyAt(HouseNpc npc, Vector3 at)
        {
            var agent = npc.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) return agent.Warp(at);
            npc.transform.position = at;
            return true;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator AllianceMeeting_UnderTheRulesAPactMeetsInAnyPrivateRoomOnceAWeek()
        {
            foreach (bool larger in new[] { false, true })
            {
                string size = larger ? " at the larger text" : "";
                yield return InstallTalkingHouse(8, true, PactOfEveryVoter(true));
                yield return SettleCast();
                yield return ApplyTextSize(larger);
                var listener = Listener(director.Snapshot);
                Assert.That(director.Snapshot.Allied(director.Snapshot.playerId, listener.id), Is.True, "Precondition: the one the player talks to is in the pact.");

                if (!larger)
                {
                    // An open room: the living room's acts have an audience, and no pact meets there.
                    yield return TalkInRoom(listener.id, "Living");
                    Assert.That(director.IsConversationOpen, Is.True, "A conversation in the living room opens.");
                    Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.AllianceMeetingCaption), Is.Null, "No meeting where people listen.");
                    Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.AllianceMeetCaption), Is.Null);

                    // The backyard: the meeting stands in for the old word with one ally, never beside it.
                    yield return TalkInRoom(listener.id, "Yard");
                    Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.AllianceMeetingCaption), Is.Not.Null, "The backyard holds a meeting.");
                    Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.AllianceMeetCaption), Is.Null,
                        "Under the rules 'Go over the plan in private' commits the meeting, so it is not offered beside it.");
                }

                // The bedroom, a private room that is not the backyard.
                yield return TalkInRoom(listener.id, "Bedroom");
                string where = "A conversation with a pact-mate in the bedroom" + size;
                Assert.That(director.IsConversationOpen, Is.True, where + ": the conversation opens.");
                var open = director.Snapshot;
                Assert.That(EpisodeEngine.CommitmentRulesOn(open), Is.True, where + ": the house plays the commitment rules.");
                var row = ButtonWithCaption(EpisodeDirector.AllianceMeetingCaption);
                var pact = open.alliances.Single(a => a.id == MeetingPactId);
                Assert.That(TagOn(row), Is.EqualTo(EpisodeDirector.AllianceMeetingTag(open, pact)), where + ": the row says what it is for,");
                Assert.That(TagOn(row), Is.EqualTo(EpisodeDirector.WarmthTag + " · " + EpisodeDirector.LearnTag),
                    where + ": warmth, and in a vote week with voters at it, learning where a vote is going.");
                Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.AllianceMeetCaption), Is.Null, where + ": and the old word is the backyard's alone, and only without the rules.");
                AssertConversationFits(larger, where);
                AssertTagsHaveRoom(where);
                yield return CaptureConversation("conversation-alliance-meeting" + (larger ? "-large" : ""), where, null);
                if (larger)
                {
                    director.ClosePanels();
                    yield return null;
                    continue;
                }

                // Pressed: one command, the whole pact at it, one ally's vote told. The control is found
                // again by its caption: a capture can render the conversation anew.
                int revision = director.Snapshot.revision;
                ButtonWithCaption(EpisodeDirector.AllianceMeetingCaption).onClick.Invoke();
                yield return null; yield return null;
                var after = director.Snapshot;
                Assert.That(after.revision, Is.EqualTo(revision + 1), where + ": the meeting is one command.");
                var line = after.events.Last(e => e.kind == "conversation");
                Assert.That(line.text, Does.StartWith(MeetingPactName + " met where nobody listens: you, "), where + ": the pact's line,");
                Assert.That(line.audienceIds, Is.EquivalentTo(new[] { after.playerId }.Concat(EpisodeEngine.AtTheMeeting(after, pact))), "told to everybody at it.");
                var claim = after.ledger.claims.Single(k => k.source == ClaimSource.Ally);
                Assert.That(claim.voterId, Is.EqualTo(listener.id), where + ": the one the player talks to says where their vote is going,");
                Assert.That(line.text, Does.EndWith(" told the pact they're voting to evict " + after.Find(claim.targetId).name + "."), "as the line says.");
                Assert.That(EpisodeEngine.MetThisWeek(after, after.alliances.Single(a => a.id == MeetingPactId)), Is.True);

                // Once a week: a word with them again offers no meeting of it.
                yield return TalkInRoom(listener.id, "Bedroom");
                Assert.That(director.IsConversationOpen, Is.True);
                Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.AllianceMeetingCaption), Is.Null, where + ": the pact has met this week.");
                director.ClosePanels();
                yield return null;
            }

            // Without the rules: the backyard's word with one ally, as it always was, and no meeting anywhere.
            yield return InstallTalkingHouse(8, true, PactOfEveryVoter(false));
            yield return SettleCast();
            yield return ApplyTextSize(false);
            var before = Listener(director.Snapshot);
            Assert.That(EpisodeEngine.CommitmentRulesOn(director.Snapshot), Is.False, "Precondition: a season without the commitment rules.");
            yield return TalkInRoom(before.id, "Bedroom");
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.AllianceMeetingCaption), Is.Null, "Without the rules no pact meets in the bedroom,");
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.AllianceMeetCaption), Is.Null);
            yield return TalkInRoom(before.id, "Yard");
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.AllianceMeetCaption), Is.Not.Null, "and the backyard offers the word with one ally,");
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.AllianceMeetingCaption), Is.Null, "never the meeting.");
            director.ClosePanels();
            yield return null;
        }
    }
}
