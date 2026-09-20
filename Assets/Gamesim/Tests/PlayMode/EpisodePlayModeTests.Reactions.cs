using System.Collections;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The ceremony beats reach the bodies: when the keys are turned the nominees are asked to
    /// react as nominated, and when the vote is read the evicted houseguest reacts as evicted.
    /// Asked, and recorded, whichever body is underneath - the authored clip plays only where the
    /// controller declares its trigger, which is the same rule Seated and Talking follow.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Reactions_TheNomineesAndTheEvictedAreAskedToActTheBeatOut()
        {
            string[] nominees = null;
            for (int guard = 0; guard < 200 && nominees == null; guard++)
            {
                var before = director.Snapshot;
                if (before.phase == EpisodePhase.Finished) break;
                var result = director.Submit(NextCommand(before));
                yield return null;
                if (!result.accepted) continue;
                if (before.nominees.Count == 0 && result.state.nominees.Count > 0) nominees = result.state.nominees.ToArray();
            }
            Assert.That(nominees, Is.Not.Null.And.Not.Empty, "The season must reach a nomination ceremony.");
            foreach (var id in nominees)
            {
                var visual = Body(id);
                Assert.That(visual, Is.Not.Null, id + " has a body in the house");
                Assert.That(visual.LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Nominated), id + " was asked to react as nominated");
                Assert.That(visual.LookTarget, Is.Null, id + " is the one being looked at, not a looker");
            }
            // V6: the room turns to look at the first nominee; the nominees themselves do not.
            var lookedAt = Body(nominees[0]).transform;
            var crowd = director.Snapshot.contestants.Where(c => c.status == ContestantStatus.Active && !nominees.Contains(c.id))
                .Select(c => Body(c.id)).Where(b => b != null).ToArray();
            Assert.That(crowd, Is.Not.Empty);
            foreach (var onlooker in crowd)
                Assert.That(onlooker.LookTarget, Is.SameAs(lookedAt), onlooker.CharacterId + " turns to the nominee");

            string evicted = null;
            for (int guard = 0; guard < 400 && evicted == null; guard++)
            {
                var before = director.Snapshot;
                if (before.phase == EpisodePhase.Finished) break;
                var wasActive = before.contestants.Where(c => c.status == ContestantStatus.Active).Select(c => c.id).ToArray();
                var result = director.Submit(NextCommand(before));
                yield return null;
                if (!result.accepted) continue;
                evicted = wasActive.FirstOrDefault(id => result.state.Find(id)?.status != ContestantStatus.Active);
            }
            Assert.That(evicted, Is.Not.Null, "The season must reach an eviction.");
            var evictedBody = Body(evicted);
            Assert.That(evictedBody, Is.Not.Null, evicted + " has a body in the house");
            Assert.That(evictedBody.LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Evicted), evicted + " was asked to react as evicted");
        }

        private CharacterPresentation Body(string id)
        {
            var npc = SceneComponents<HouseNpc>().FirstOrDefault(n => n.Id == id);
            if (npc != null) return npc.GetComponent<CharacterPresentation>();
            return id == director.Snapshot.playerId ? player.GetComponent<CharacterPresentation>() : null;
        }
    }
}
