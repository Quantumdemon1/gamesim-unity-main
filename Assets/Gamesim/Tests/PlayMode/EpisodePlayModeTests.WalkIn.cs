using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Walking in on two houseguests (playtest, 2026-09-27): the walk-in watch took the first two
    /// people in the player's room by id, wherever in the room they stood and whatever they were
    /// doing, and the card said they were mid-argument. It is on two who are actually together now,
    /// with the player close by - the reference's second houseguest within three units of one
    /// within five of the player.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator WalkIn_IsOnTwoHouseguestsWhoAreActuallyTogether()
        {
            yield return null;
            // The bodies stay where the test stands them.
            director.SuspendNpcAutonomyForDiagnostics();
            var pair = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy)
                .OrderBy(npc => npc.Id, System.StringComparer.Ordinal).Take(2).ToArray();
            Assert.That(pair, Has.Length.EqualTo(2));
            var standing = SceneComponents<HousePlayerController>().First().transform;
            var ids = new List<string> { pair[0].Id, pair[1].Id };
            var origin = standing.position;

            pair[0].transform.position = origin + Vector3.right * 2.5f;
            pair[1].transform.position = origin - Vector3.right * 2.5f;
            Assert.That(WalkInPair(ids, out _, out _), Is.False, "Two at opposite ends of the room, the player between them, are nobody's scene.");

            pair[1].transform.position = origin + Vector3.right * 3.5f;
            Assert.That(WalkInPair(ids, out var first, out var second), Is.True, "Two standing together beside the player are a walk-in.");
            Assert.That((first, second), Is.EqualTo((pair[0].Id, pair[1].Id)));

            standing.position = origin - Vector3.right * 12f;
            Assert.That(WalkInPair(ids, out _, out _), Is.False, "A pair the player is nowhere near is not walked in on.");
        }

        private bool WalkInPair(List<string> ids, out string first, out string second)
        {
            var method = typeof(EpisodeDirector).GetMethod("TryWalkInPair", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "The walk-in's pair is chosen in one place.");
            var arguments = new object[] { ids, null, null };
            bool found = (bool)method.Invoke(director, arguments);
            first = (string)arguments[1];
            second = (string)arguments[2];
            return found;
        }
    }
}
