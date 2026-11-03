using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The room scope (ROOM-SCOPE-PLAN RS1) in the prototype house as it runs: no director, so nothing
    /// builds a scope or hands the rig one, and every move the rig makes reports the whole house. The
    /// EditMode test reads the saved scene; this one runs it.
    /// </summary>
    public sealed partial class HousePrototypeTests
    {
        [UnityTest]
        public IEnumerator RoomScope_TheHousePrototypeHasNoRoomScopeAtRunTime()
        {
            yield return null;
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<EpisodeDirector>(true)), Is.Empty, "The prototype has no director.");
            Assert.That(rig.RoomScope, Is.Null, "Awake and the first frames give the rig no rooms.");
            Assert.That(rig.Rooms.IsWholeHouse, Is.True, "and it reports the whole house.");

            // A follow, a free move and a shot that names a room: still the whole house, with no rooms to resolve.
            rig.FocusSubject(player.transform, false);
            Assert.That(rig.Rooms.IsWholeHouse, Is.True, "Following the player.");
            rig.MoveTo(rig.HouseCenter + new Vector3(-5f, 0f, -5f), 12f);
            Assert.That(rig.Rooms.IsWholeHouse, Is.True, "A free move.");
            rig.MoveTo(new HouseCameraRig.Shot { Focus = rig.HouseCenter, Distance = 12f, Pitch = 45f, Yaw = 0f, Rooms = RoomSet.Of("Kitchen") });
            Assert.That(rig.Rooms.IsWholeHouse, Is.True, "A shot that names the kitchen.");
            rig.ReleaseShot(0f);
            for (int frame = 0; frame < 5; frame++) yield return null;
            Assert.That(rig.RoomScope, Is.Null, "Nothing has handed the rig rooms since.");
            Assert.That(rig.Rooms.IsWholeHouse, Is.True);

            // And nothing is drawn differently: every camera draws the room layers, and nothing stands on one.
            foreach (var camera in roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)))
                Assert.That(camera.cullingMask & RoomLayers.Mask, Is.EqualTo(RoomLayers.Mask), camera.name + " draws every room layer.");
            Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Where(t => RoomLayers.IsRoomLayer(t.gameObject.layer))
                .Select(t => t.name).ToArray(), Is.Empty, "Nothing stands on a room layer.");
        }
    }
}
