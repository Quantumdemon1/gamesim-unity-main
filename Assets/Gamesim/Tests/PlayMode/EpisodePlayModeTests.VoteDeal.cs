using System.Collections;
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
    /// The levers on screen (STRATEGY-LOOP-PLAN.md §3): under the levers a vote deal is offered per
    /// nominee, names who it is about, and commits with that person as its target, so it can enter
    /// the voter's ballot and be judged at the reveal.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator VoteDeal_AVoteDealNamesWhoItIsAboutAndCommitsWithThem()
        {
            yield return InstallCampaign(46, state => { EpisodeEngine.EnableLevers(state); WarmToPlayer(state, 30); });
            director.ClosePanels();
            yield return null;
            var state = director.Snapshot;
            Assume.That(EpisodeEngine.LeverRulesOn(state), Is.True);
            var bodies = SceneComponents<HouseNpc>().Where(n => n.gameObject.activeInHierarchy).ToList();
            var voter = EpisodeEngine.Voters(state).FirstOrDefault(v => !v.isPlayer && bodies.Any(n => n.Id == v.id)
                && PlayerDeals.Available(state, v.id).Contains(DealKind.VoteEvict));
            Assume.That(voter, Is.Not.Null, "A voter with a body who could be put a vote deal.");
            var npc = bodies.Single(n => n.Id == voter.id);
            WarpPlayer(npc.transform.position + (player.transform.position - npc.transform.position).normalized * 1.4f);
            yield return null;
            Assert.That(director.TryOpenNpc(voter.id), Is.True, "The conversation opens.");
            yield return null;

            var nominees = state.nominees.Select(id => state.Find(id)).ToList();
            foreach (var nominee in nominees)
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.VoteDealCaption(DealKind.VoteEvict, nominee.isPlayer ? "you" : nominee.name)), Is.Not.Null,
                    "A vote to evict is offered per nominee: " + nominee.name);
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.DealProposeCaption(DealKind.Title(DealKind.VoteEvict).ToLowerInvariant())), Is.Null,
                "and never as a deal about nobody.");

            var about = nominees.First(n => !n.isPlayer);
            int revision = director.Snapshot.revision;
            ButtonWithCaption(EpisodeHud.VoteDealCaption(DealKind.VoteEvict, about.name)).onClick.Invoke();
            yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(revision + 1), "The row commits the proposal.");
            Assert.That(after.events.Skip(state.events.Count).Any(e => e.kind == "deal"), Is.True, "and the house says something about it.");
            var deal = after.deals.FirstOrDefault(d => d.proposerId == after.playerId && d.recipientId == voter.id && d.type == DealKind.VoteEvict);
            if (deal != null)
            {
                Assert.That(deal.targetId, Is.EqualTo(about.id), "The deal names who it is about.");
                Assert.That(after.events.Any(e => e.kind == "lever" && e.text.StartsWith(voter.name + ": ")), Is.True,
                    "and the lever says what it moved.");
            }
            director.ClosePanels();
            yield return null;
        }
    }
}
