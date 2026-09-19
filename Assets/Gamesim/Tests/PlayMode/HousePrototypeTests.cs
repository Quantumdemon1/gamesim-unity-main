using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Bootstrap;
using Gamesim.Episode;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    public sealed class HousePrototypeTests
    {
        private const string HouseScene = "HousePrototype";
        private readonly List<GameObject> temporaryObjects = new List<GameObject>();
        private HousePlayerController player;
        private HouseCameraRig rig;
        private HouseInteraction interaction;
        private HouseNpc npc;
        private HouseRoomMarker[] rooms;
        private Keyboard testKeyboard;
        private Mouse testMouse;
        private bool observedHouseSceneLoaded;
        private bool agentEnabledAtSceneLoad;
        private string bootstrapSaveDirectory;

        [UnitySetUp]
        public IEnumerator LoadHouse()
        {
            observedHouseSceneLoaded = false;
            agentEnabledAtSceneLoad = false;
            SceneManager.sceneLoaded += ObserveHouseStartup;
            if (GamesimBootstrap.Instance != null)
            {
                Object.Destroy(GamesimBootstrap.Instance.gameObject);
                yield return null;
            }

            Assert.That(Application.CanStreamedLevelBeLoaded(HouseScene), Is.True,
                "HousePrototype must be included in build settings.");
            yield return SceneManager.LoadSceneAsync(HouseScene, LoadSceneMode.Single);
            yield return null;
            FindHouseComponents();
            AssertAgentStartupOrder();
            Assert.That(player.Agent.isOnNavMesh, Is.True, "The player must spawn on the baked NavMesh.");
            player.Agent.speed = 20f;
            player.Agent.acceleration = 100f;
            player.Agent.angularSpeed = 720f;
        }

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            SceneManager.sceneLoaded -= ObserveHouseStartup;
            if (interaction != null)
            {
                interaction.EndDialogue();
            }

            if (testKeyboard != null && testKeyboard.added)
            {
                InputSystem.RemoveDevice(testKeyboard);
            }

            testKeyboard = null;
            if (testMouse != null && testMouse.added)
            {
                InputSystem.RemoveDevice(testMouse);
            }

            testMouse = null;
            foreach (var temporary in temporaryObjects)
            {
                if (temporary != null)
                {
                    Object.Destroy(temporary);
                }
            }

            temporaryObjects.Clear();
            if (GamesimBootstrap.Instance != null)
            {
                Object.Destroy(GamesimBootstrap.Instance.gameObject);
            }

            yield return null;
            if (bootstrapSaveDirectory != null)
            {
                var episode = SceneManager.GetSceneByName("EpisodeHouse");
                if (episode.IsValid() && episode.isLoaded)
                {
                    var cleanup = SceneManager.CreateScene("Bootstrap test cleanup " + Guid.NewGuid().ToString("N"));
                    SceneManager.SetActiveScene(cleanup);
                    yield return SceneManager.UnloadSceneAsync(episode);
                }
                EpisodeDirector.SaveRootOverride = null;
                var resolved = Path.GetFullPath(bootstrapSaveDirectory);
                Assert.That(resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase), Is.True);
                Assert.That(Path.GetFileName(resolved), Does.StartWith("GamesimBootstrapTests-"));
                if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
                bootstrapSaveDirectory = null;
            }
        }

        [UnityTest]
        public IEnumerator BootstrapBuildIndexZero_EntersPlayableEpisodeHouse()
        {
            var bootstrapPath = SceneUtility.GetScenePathByBuildIndex(0);
            Assert.That(bootstrapPath, Does.EndWith("/Bootstrap.unity"));
            bootstrapSaveDirectory = Path.Combine(Path.GetTempPath(), "GamesimBootstrapTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(bootstrapSaveDirectory);
            EpisodeDirector.SaveRootOverride = bootstrapSaveDirectory;
            observedHouseSceneLoaded = false;
            yield return SceneManager.LoadSceneAsync(0, LoadSceneMode.Single);
            var deadline = Time.realtimeSinceStartup + 10f;
            while (SceneManager.GetActiveScene().name != "EpisodeHouse" && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("EpisodeHouse"));
            Assert.That(GamesimBootstrap.Instance, Is.Not.Null);
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            player = roots.SelectMany(root => root.GetComponentsInChildren<HousePlayerController>(true)).Single();
            rig = roots.SelectMany(root => root.GetComponentsInChildren<HouseCameraRig>(true)).Single();
            var episode = roots.SelectMany(root => root.GetComponentsInChildren<EpisodeDirector>(true)).Single();
            deadline = Time.realtimeSinceStartup + 10f;
            while (!episode.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.That(episode.IsReady, Is.True);
            Assert.That(episode.Snapshot.contestants, Has.Count.EqualTo(6));
            AssertAgentStartupOrder();
            Assert.That(player.Agent.isOnNavMesh, Is.True);
            Assert.That(player.InputEnabled, Is.True);
            Assert.That(rig.ViewCamera.isActiveAndEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator Player_ActuallyReachesEveryRoomThroughCompletePaths()
        {
            var expectedRooms = new[] { "Living", "Kitchen", "Bedroom", "Private", "Yard" };
            Assert.That(rooms.Select(room => room.RoomName), Is.EquivalentTo(expectedRooms));
            foreach (var roomName in expectedRooms)
            {
                var marker = Room(roomName);
                Assert.That(player.TryMoveTo(marker.transform.position), Is.True,
                    "No complete path to " + roomName);
                yield return null;
                Assert.That(player.Agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete), roomName);
                var deadline = Time.realtimeSinceStartup + 8f;
                while (!player.HasArrived && Time.realtimeSinceStartup < deadline)
                {
                    Assert.That(player.Agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete),
                        "Path became partial while walking to " + roomName);
                    yield return null;
                }

                Assert.That(player.HasArrived, Is.True, "Agent did not arrive at " + roomName);
                Assert.That(Vector3.Distance(player.transform.position, marker.transform.position),
                    Is.LessThan(player.Agent.stoppingDistance + 0.65f),
                    "Arrival must correspond to the actual room location: " + roomName);
            }
        }

        [UnityTest]
        public IEnumerator MouseAndKeyboardInput_MovePlayerOrbitZoomPanAndRecenter()
        {
            // Center the fixture over this known bare floor so the same test works in small Game views.
            rig.ConfigureBounds(new Vector3(-4f, 1.2f, -6f), new Vector2(16f, 17f));
            var destination = new Vector3(-3f, 0f, -7f);
            var screen = rig.ViewCamera.WorldToScreenPoint(destination);
            Assert.That(screen.z, Is.GreaterThan(0f));
            Assert.That(rig.ViewCamera.pixelRect.Contains(new Vector2(screen.x, screen.y)), Is.True,
                "The floor click must lie inside the rendered Game view.");
            var pointerPosition = new Vector2(screen.x, screen.y);
            var ray = rig.ViewCamera.ScreenPointToRay(pointerPosition);
            Assert.That(Physics.Raycast(ray, out var groundHit, 500f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(groundHit.collider.GetComponentInParent<HouseWalkable>(), Is.Not.Null,
                "The synthetic click must hit bare walkable floor, not furniture or a character.");

            testMouse = InputSystem.AddDevice<Mouse>();
            testKeyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = pointerPosition });
            yield return null;
            yield return null;
            if (EventSystem.current != null)
            {
                var uiHits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
                    { position = pointerPosition }, uiHits);
                Assert.That(uiHits, Is.Empty, "The test click must not overlap interactive HUD graphics.");
                Assert.That(EventSystem.current.IsPointerOverGameObject(), Is.False);
            }

            var originalPlayerPosition = player.transform.position;
            InputSystem.QueueStateEvent(testMouse,
                new MouseState { position = pointerPosition }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = pointerPosition });
            yield return null;
            Assert.That(Vector3.Distance(player.Agent.destination, groundHit.point), Is.LessThan(0.35f),
                "The actual mouse press must set the clicked floor destination.");
            var deadline = Time.realtimeSinceStartup + 4f;
            while ((!player.HasArrived || Vector3.Distance(player.transform.position, destination) > 0.6f)
                && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(player.Agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete));
                yield return null;
            }

            Assert.That(player.HasArrived, Is.True);
            Assert.That(Vector3.Distance(player.transform.position, destination), Is.LessThan(0.6f));
            Assert.That(Vector3.Distance(player.transform.position, originalPlayerPosition), Is.GreaterThan(1f));

            var initialRotation = rig.transform.rotation;
            InputSystem.QueueStateEvent(testMouse,
                new MouseState { position = pointerPosition, delta = new Vector2(90f, 0f) }
                    .WithButton(MouseButton.Right));
            yield return null;
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = pointerPosition });
            deadline = Time.realtimeSinceStartup + 2f;
            while (Quaternion.Angle(rig.transform.rotation, initialRotation) < 5f
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(Quaternion.Angle(rig.transform.rotation, initialRotation), Is.GreaterThanOrEqualTo(5f),
                "Right-drag mouse input must orbit the camera.");
            var initialDistance = rig.ViewCamera.transform.localPosition.magnitude;
            InputSystem.QueueStateEvent(testMouse,
                new MouseState { position = pointerPosition, scroll = new Vector2(0f, 240f) });
            yield return null;
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = pointerPosition });
            deadline = Time.realtimeSinceStartup + 2f;
            while (rig.ViewCamera.transform.localPosition.magnitude > initialDistance - 0.5f
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(rig.ViewCamera.transform.localPosition.magnitude, Is.LessThan(initialDistance - 0.5f),
                "Mouse wheel input must zoom the camera closer.");
            var initialFocus = rig.transform.position;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.D));
            yield return new WaitForSecondsRealtime(0.35f);
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null;
            Assert.That(Vector3.Distance(rig.transform.position, initialFocus), Is.GreaterThan(0.2f),
                "Keyboard pan input must move the camera focus.");

            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.F));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return WaitForCameraFocus(new Vector3(player.transform.position.x, 1.2f, player.transform.position.z));
        }

        [UnityTest]
        public IEnumerator InvalidOrLockedCommands_PreserveTheExistingDestination()
        {
            Assert.That(player.TryMoveTo(Room("Yard").transform.position), Is.True);
            var destination = player.Agent.destination;
            Assert.That(player.Agent.hasPath, Is.True);
            var invalidPositions = new[]
            {
                new Vector3(float.NaN, 0f, 0f),
                new Vector3(float.PositiveInfinity, 0f, 0f),
                new Vector3(10000f, 0f, 10000f)
            };
            foreach (var invalid in invalidPositions)
            {
                Assert.That(player.TryMoveTo(invalid), Is.False);
                AssertDestination(destination);
            }

            player.SetInputEnabled(false);
            Assert.That(player.Agent.isStopped, Is.True);
            Assert.That(player.TryMoveTo(Room("Kitchen").transform.position), Is.False);
            AssertDestination(destination);
            player.SetInputEnabled(true);
            Assert.That(player.Agent.isStopped, Is.False);
            AssertDestination(destination);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DialogueButtons_PauseWalkingShowResponsesAndRestoreExploration()
        {
            yield return MoveNextToMaya();
            var previousFocus = rig.transform.position;
            Assert.That(player.TryMoveTo(Room("Yard").transform.position), Is.True);
            var destination = player.Agent.destination;
            var stoppedPosition = player.transform.position;
            Assert.That(interaction.TryBeginDialogue(), Is.True);
            Assert.That(interaction.IsDialogueOpen, Is.True);
            Assert.That(player.InputEnabled, Is.False);
            Assert.That(player.Agent.isStopped, Is.True);
            Assert.That(rig.IsConversationFocused, Is.True);
            Assert.That(player.TryMoveTo(Room("Bedroom").transform.position), Is.False);
            AssertDestination(destination);

            // The conversation's framing is the two-shot (V5): the pair's midpoint, a head higher.
            var expectedFocus = (player.transform.position + npc.transform.position) * 0.5f + Vector3.up * (1f + HouseCameraRig.TwoShotLift);
            yield return WaitForCameraFocus(expectedFocus);
            Assert.That(Vector3.Distance(player.transform.position, stoppedPosition), Is.LessThan(0.05f));
            var responseText = interaction.GetComponentsInChildren<TMPro.TMP_Text>(true)
                .Single(text => text.name == "Conversation text");
            var previousText = responseText.text;
            for (var response = 1; response <= 3; response++)
            {
                var button = ButtonNamed("Response " + response);
                Assert.That(button.IsActive() && button.IsInteractable(), Is.True);
                button.onClick.Invoke();
                Assert.That(responseText.text, Is.Not.Empty.And.Not.EqualTo(previousText));
                Assert.That(interaction.IsDialogueOpen, Is.True);
                previousText = responseText.text;
            }

            var leave = ButtonNamed("Leave conversation");
            Assert.That(leave.IsActive() && leave.IsInteractable(), Is.True);
            leave.onClick.Invoke();
            Assert.That(interaction.IsDialogueOpen, Is.False);
            Assert.That(player.InputEnabled, Is.True);
            Assert.That(player.Agent.isStopped, Is.False);
            Assert.That(rig.IsConversationFocused, Is.False);
            Assert.That(leave.gameObject.activeInHierarchy, Is.False);
            AssertDestination(destination);
            yield return WaitForCameraFocus(previousFocus);
            Assert.That(Vector3.Distance(player.transform.position, stoppedPosition), Is.GreaterThan(0.1f),
                "Closing dialogue must resume the existing route.");
        }

        [UnityTest]
        public IEnumerator EscapeKey_ClosesDialogueAndRestoresInput()
        {
            yield return MoveNextToMaya();
            Assert.That(interaction.TryBeginDialogue(), Is.True);
            testKeyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
            var deadline = Time.realtimeSinceStartup + 2f;
            while (interaction.IsDialogueOpen && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            Assert.That(interaction.IsDialogueOpen, Is.False, "The actual Escape input must close dialogue.");
            Assert.That(player.InputEnabled, Is.True);
            Assert.That(rig.IsConversationFocused, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NearbyNpc_CannotBeTalkedToThroughAWall()
        {
            yield return MoveNextToMaya();
            Assert.That(interaction.CanInteract, Is.True);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Test dialogue occluder";
            temporaryObjects.Add(wall);
            wall.transform.position = (player.transform.position + npc.transform.position) * 0.5f + Vector3.up;
            wall.transform.localScale = new Vector3(0.5f, 2f, 0.5f);
            Physics.SyncTransforms();
            Assert.That(interaction.CanInteract, Is.False);
            Assert.That(interaction.TryBeginDialogue(), Is.False);
            Assert.That(player.InputEnabled, Is.True);
            Assert.That(rig.IsConversationFocused, Is.False);
            Object.Destroy(wall);
            yield return null;
            Physics.SyncTransforms();
            Assert.That(interaction.CanInteract, Is.True);
        }

        private void ObserveHouseStartup(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != HouseScene && scene.name != "EpisodeHouse")
            {
                return;
            }

            // sceneLoaded runs after OnEnable and before Start. Registration must precede agent activation.
            var loadedPlayer = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HousePlayerController>(true)).FirstOrDefault();
            observedHouseSceneLoaded = loadedPlayer != null;
            agentEnabledAtSceneLoad = loadedPlayer != null && loadedPlayer.Agent.enabled;
        }

        private void AssertAgentStartupOrder()
        {
            Assert.That(observedHouseSceneLoaded, Is.True, "The test must observe the authored house scene loading.");
            Assert.That(agentEnabledAtSceneLoad, Is.False,
                "The authored agent must stay disabled until scene NavMesh surfaces have registered.");
            Assert.That(player.Agent.enabled, Is.True, "Startup must enable the agent after NavMesh registration.");
        }

        private void FindHouseComponents()
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            player = roots.SelectMany(root => root.GetComponentsInChildren<HousePlayerController>(true)).Single();
            rig = roots.SelectMany(root => root.GetComponentsInChildren<HouseCameraRig>(true)).Single();
            interaction = roots.SelectMany(root => root.GetComponentsInChildren<HouseInteraction>(true)).Single();
            npc = roots.SelectMany(root => root.GetComponentsInChildren<HouseNpc>(true)).Single(character => character.Id == "maya");
            rooms = roots.SelectMany(root => root.GetComponentsInChildren<HouseRoomMarker>(true)).ToArray();
        }

        private IEnumerator MoveNextToMaya()
        {
            var direction = (player.transform.position - npc.transform.position).normalized;
            var nearby = npc.transform.position + direction * 1.65f;
            Assert.That(NavMesh.SamplePosition(nearby, out var hit, 0.75f, player.Agent.areaMask), Is.True);
            Assert.That(player.Agent.Warp(hit.position), Is.True);
            player.Agent.ResetPath();
            Physics.SyncTransforms();
            yield return null;
            Assert.That(interaction.CanInteract, Is.True, "The authored Maya location must permit nearby conversation.");
        }

        private HouseRoomMarker Room(string roomName)
        {
            return rooms.Single(room => room.RoomName == roomName);
        }

        private Button ButtonNamed(string name)
        {
            return interaction.GetComponentsInChildren<Button>(true).Single(button => button.name == name);
        }

        private void AssertDestination(Vector3 expected)
        {
            Assert.That(player.Agent.hasPath, Is.True);
            Assert.That(Vector3.Distance(player.Agent.destination, expected), Is.LessThan(0.001f));
            Assert.That(player.Agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete));
        }

        private IEnumerator WaitForCameraFocus(Vector3 focus)
        {
            var deadline = Time.realtimeSinceStartup + 2f;
            while (Vector3.Distance(rig.transform.position, focus) > 0.1f && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(Vector3.Distance(rig.transform.position, focus), Is.LessThanOrEqualTo(0.1f));
        }
    }
}
