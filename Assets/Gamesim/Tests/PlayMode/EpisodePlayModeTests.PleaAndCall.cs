using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The levers on screen, second half (STRATEGY-LOOP-PLAN.md §3): from the block a voter can be
    /// asked for their vote in conversation, and through an ally the bloc's vote can be called.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private IEnumerator OpenVoterConversation(ContestantState voter)
        {
            var npc = SceneComponents<HouseNpc>().Single(n => n.Id == voter.id);
            WarpPlayer(npc.transform.position + (player.transform.position - npc.transform.position).normalized * 1.4f);
            yield return null;
            Assert.That(director.TryOpenNpc(voter.id), Is.True, "The conversation opens.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Plea_FromTheBlockAVoterCanBeAskedForTheirVote()
        {
            yield return InstallCampaign(46, state =>
            {
                EpisodeEngine.EnableLevers(state); WarmToPlayer(state, 30);
                state.nominees[1] = state.playerId;   // the veto lineup already seats everyone in a six-house
            });
            director.ClosePanels();
            yield return null;
            var state = director.Snapshot;
            Assume.That(state.nominees.Contains(state.playerId), "The player is on the block.");
            var bodies = SceneComponents<HouseNpc>().Where(n => n.gameObject.activeInHierarchy).Select(n => n.Id).ToList();
            var voter = EpisodeEngine.Voters(state).FirstOrDefault(v => !v.isPlayer && bodies.Contains(v.id) && StrategyRules.CanBeAskedForTheirVote(state, v.id));
            Assume.That(voter, Is.Not.Null, "A voter with a body who can be asked.");
            yield return OpenVoterConversation(voter);

            string ask = EpisodeHud.LobbyAskCaption(state, voter.name, LobbyAsk.Vote, state.playerId);
            Assert.That(ask, Is.EqualTo("Ask " + voter.name + " to vote to keep you"));
            ButtonWithCaption(ask).onClick.Invoke();
            yield return null;
            var approach = ButtonWithCaptionOrNull(EpisodeHud.LobbyApproachCaption(LobbyApproach.Emotional, true));
            Assert.That(approach, Is.Not.Null, "A plea for yourself has its own words.");
            int spent = EpisodeEngine.SocialActionsSpent(director.Snapshot);
            approach.onClick.Invoke();
            yield return null;
            var after = director.Snapshot;
            var plea = after.lobbies.SingleOrDefault(l => l.week == after.week && l.deciderId == voter.id && l.ask == LobbyAsk.Vote);
            Assert.That(plea, Is.Not.Null, "The plea is on the record");
            Assert.That(plea.subjectId, Is.EqualTo(after.playerId));
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(spent + 1), "and costs a conversation.");
            Assert.That(after.events.Any(e => e.kind == "lever" && e.text.StartsWith(voter.name + ": ")), Is.True, "The lever says what it moved.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Call_ThroughAnAllyTheBlocsVoteCanBeCalled()
        {
            yield return InstallCampaign(46, state =>
            {
                EpisodeEngine.EnableLevers(state); WarmToPlayer(state, 30);
                state.blocRulesStartWeek = 1;
                var voters = EpisodeEngine.Voters(state).Where(v => !v.isPlayer).Take(2).Select(v => v.id).ToList();
                state.alliances.Add(new AllianceState { id = "alliance-playmode", name = "The Playmode Pact", members = new List<string> { state.playerId }.Concat(voters).ToList(), active = true });
            });
            director.ClosePanels();
            yield return null;
            var state = director.Snapshot;
            var pact = state.alliances.Single(a => a.id == "alliance-playmode");
            var bodies = SceneComponents<HouseNpc>().Where(n => n.gameObject.activeInHierarchy).Select(n => n.Id).ToList();
            var ally = pact.members.Where(id => id != state.playerId && bodies.Contains(id)).Select(state.Find).FirstOrDefault();
            Assume.That(ally, Is.Not.Null, "An ally with a body.");
            yield return OpenVoterConversation(ally);

            var target = state.Find(state.nominees.First(id => id != state.playerId));
            var button = ButtonWithCaptionOrNull(EpisodeHud.CallTheVoteCaption(pact.name, target.name));
            Assert.That(button, Is.Not.Null, "The call is offered per nominee.");
            int revision = director.Snapshot.revision;
            button.onClick.Invoke();
            yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(revision + 1));
            var call = after.ledger.calls.SingleOrDefault(k => k.allianceId == pact.id && k.week == after.week);
            Assert.That(call, Is.Not.Null, "The call is on the record");
            Assert.That(call.targetId, Is.EqualTo(target.id));
            Assert.That(call.followed.Concat(call.defected), Is.EquivalentTo(pact.members.Where(id => id != after.playerId)), "and every member decided.");
            Assert.That(after.events.Any(e => e.kind == "lever" && e.text.Contains("You called it in " + pact.name)), Is.True);
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.CallTheVoteCaption(pact.name, target.name)), Is.Null, "Once a week.");
            director.ClosePanels();
            yield return null;
        }
    }
}
