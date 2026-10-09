using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Keyboard and pad paths for what was the mouse's alone (PLAN A, A7): a trip to every room from
    /// the notebook's Who is where page, the camera turned by Q and C, the camera's speed and tilt
    /// applied to the mouse, the stick and the keys alike, and a walk-in stepped into by E.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>Whether the player has reached a room's trip: standing in it, or seated-ready at the diary.</summary>
        private bool ArrivedIn(string room) => director.PlayerRoom == room || room == "Private" && director.CanUseDiary;

        [UnityTest, Timeout(900000)]
        public IEnumerator Travel_EveryRoomIsAKeyboardTripFromTheNotebook()
        {
            HoldTheHouseForTheFixture();
            var rooms = director.TravelRooms();
            Assert.That(rooms, Is.Not.Empty, "The house has rooms to go to.");

            // The page itself, photographed at both text sizes on both frames, every trip on it in words that fit.
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                director.ShowNotebookSection(EpisodeDirector.NotebookSection.Rooms);
                yield return Frames(2);
                yield return OnBothFrames("notebook-rooms" + (larger ? "-large" : ""), "The Who is where page" + (larger ? " at larger text" : ""), where =>
                {
                    foreach (var room in rooms)
                    {
                        string caption = EpisodeDirector.GoToRoomCaption(room);
                        var control = ControlCarrying(caption);
                        Assert.That(control, Is.Not.Null, where + ": '" + caption + "' is on the page and can be pressed.");
                        var label = control.GetComponentsInChildren<TMP_Text>().Last(text => text.text == caption);
                        AssertLineHasRoom(label, where);
                    }
                });
                director.ClosePanels();
                yield return Frames(2);
            }
            yield return ApplyTextSize(false);

            Assert.That(rooms.Select(EpisodeDirector.GoToRoomCaption).Distinct().Count(), Is.EqualTo(rooms.Count), "One caption a room.");
            player.Agent.speed = 25; player.Agent.acceleration = 100;
            foreach (var room in rooms)
            {
                director.ShowNotebookSection(EpisodeDirector.NotebookSection.Rooms);
                yield return Frames(2);
                string caption = EpisodeDirector.GoToRoomCaption(room);
                yield return KeyboardSubmit(caption);
                Assert.That(director.IsPanelOpen, Is.False, caption + " closes the notebook and sets off: " + lastSubmit);
                float deadline = Time.realtimeSinceStartup + 25f;
                while (!ArrivedIn(room) && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(ArrivedIn(room), Is.True, caption + ": the player reached it (in " + (director.PlayerRoom ?? "nowhere")
                    + "; the line reads '" + director.StatusMessage + "').");
            }
        }

        /// <summary>Holds a key down on the test keyboard for a quarter second of real time, then lets it go.</summary>
        private IEnumerator HoldKey(Key key)
        {
            if (testKeyboard == null) testKeyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(key));
            yield return new WaitForSecondsRealtime(0.25f);
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null;
        }

        /// <summary>The player's added tilt back to none, as a fixture sets the rig's distance: by its private field.</summary>
        private void LevelTheTilt() =>
            typeof(Gamesim.House.HouseCameraRig).GetField("pitchOffset", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(cameraRig, 0f);

        /// <summary>How far the camera turned around the house between two yaws, signed: positive to the right.</summary>
        private static float Turned(float before, float after) => Mathf.DeltaAngle(before, after);

        [UnityTest]
        public IEnumerator CameraInput_KeysOrbit()
        {
            director.ClosePanels();
            SetCameraDistance(20f);
            yield return SettleCamera();
            cameraRig.ControlsEnabled = true;

            float before = cameraRig.Yaw;
            yield return HoldKey(Key.Q);
            float left = Turned(before, cameraRig.Yaw);
            before = cameraRig.Yaw;
            yield return HoldKey(Key.C);
            float right = Turned(before, cameraRig.Yaw);
            Assert.That(left, Is.LessThan(-1f), "Q turns the camera one way.");
            Assert.That(right, Is.GreaterThan(1f), "C turns it the other.");
            Assert.That(cameraRig.Actions.OrbitRate.bindings.Any(binding => binding.path == "<Keyboard>/q"), Is.True);

            // A panel holds the camera still: a field typed into under a panel - a name, a speech,
            // the word game's letters - never turns it.
            director.OpenSettings();
            yield return Frames(2);
            Assert.That(cameraRig.ControlsEnabled, Is.False, "The settings hold the camera.");
            before = cameraRig.Yaw;
            yield return HoldKey(Key.Q);
            Assert.That(Mathf.Abs(Turned(before, cameraRig.Yaw)), Is.LessThan(.01f), "Q under a panel turns nothing.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// A7's "check, don't assume": the word game spells with every letter key, Q and C among
        /// them, and its board holds the camera (the render's ControlsEnabled, off while the episode
        /// screen the game is played from is open), so Q held while the letters are up turns nothing.
        /// </summary>
        [UnityTest]
        public IEnumerator CameraInput_QHeldUnderTheWordGameNeverTurnsTheCamera()
        {
            yield return InstallRules4AtFirstHoH(SeedOpeningWith("Social"));
            var found = new CompetitionGameScreen[1];
            yield return EnterRanked(CompetitionMiniGames.Kind.Words, found);
            Assert.That(ChallengeRun().Kind, Is.EqualTo(CompetitionMiniGames.Kind.Words), "The word game is up.");
            // The game's own framing lands first, so nothing but a key could turn the view during the hold.
            float settled = Time.realtimeSinceStartup + 5f;
            while (cameraRig.IsTravelling && Time.realtimeSinceStartup < settled) yield return null;
            Assert.That(cameraRig.ControlsEnabled, Is.False, "The word game holds the camera.");

            float yaw = cameraRig.Yaw;
            if (testKeyboard == null) testKeyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Q));
            float release = Time.realtimeSinceStartup + .25f;
            while (Time.realtimeSinceStartup < release)
            {
                yield return null;
                Assert.That(cameraRig.ControlsEnabled, Is.False, "The camera stays held while Q is down.");
            }
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null;
            Assert.That(Mathf.Abs(Turned(yaw, cameraRig.Yaw)), Is.LessThan(.01f),
                "Q held under the word game turns nothing (a shot holds the camera: " + cameraRig.HasShot + ").");
            Assert.That(director.IsChallengeActive, Is.True, "and the game is still being played.");
        }

        /// <summary>One right-drag of <paramref name="delta"/> at the screen's middle: what it did to the turn and the tilt.</summary>
        private IEnumerator RightDrag(Vector2 delta, List<Vector2> moved)
        {
            var mouse = TestMouse();
            var at = new Vector2(Screen.width * .5f, Screen.height * .5f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = at }.WithButton(MouseButton.Right));
            yield return null;
            float yaw = cameraRig.Yaw, tilt = cameraRig.PitchOffset;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = at, delta = delta }.WithButton(MouseButton.Right));
            // The rig reads its input in LateUpdate, after this coroutine's turn in the frame the
            // delta arrives in, so the turn is there a frame on - when the delta has gone back to
            // nothing and the held button turns no further.
            yield return null;
            yield return null;
            moved.Add(new Vector2(Turned(yaw, cameraRig.Yaw), cameraRig.PitchOffset - tilt));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = at });
            yield return null;
        }

        /// <summary>
        /// The camera's speed and tilt, set as the settings set them, reach every way of turning it:
        /// the mouse's drag twice as far at twice the speed, the tilt the other way inverted, and the
        /// stick and Q/C turning faster at the faster speed.
        /// </summary>
        [UnityTest]
        public IEnumerator CameraInput_SpeedAndInvertApplyToMouseStickAndKeys()
        {
            director.ClosePanels();
            SetCameraDistance(20f);
            yield return SettleCamera();
            cameraRig.ControlsEnabled = true;
            Assert.That(director.CameraSpeed, Is.EqualTo(1f), "The camera starts at its own speed.");
            Assert.That(director.InvertTilt, Is.False, "and its tilt the usual way.");

            // The mouse: one drag at the camera's own speed, one the other way at twice it.
            LevelTheTilt();
            var moved = new List<Vector2>();
            yield return RightDrag(new Vector2(40f, 10f), moved);
            director.SetCameraSpeed(2f);
            Assert.That(cameraRig.OrbitSpeedScale, Is.EqualTo(2f), "The settings reach the rig.");
            yield return RightDrag(new Vector2(-40f, -10f), moved);
            Assert.That(Mathf.Abs(moved[0].x), Is.GreaterThan(.5f), "A right-drag turns the camera.");
            Assert.That(-moved[1].x / moved[0].x, Is.EqualTo(2f).Within(.05f), "Twice the speed turns twice as far: " + moved[0] + " then " + moved[1]);
            Assert.That(-moved[1].y / moved[0].y, Is.EqualTo(2f).Within(.05f), "and tilts twice as far.");

            // Inverted, the same drag tilts the other way and still turns the same way.
            director.SetCameraSpeed(1f);
            director.SetInvertTilt(true);
            Assert.That(cameraRig.InvertTilt, Is.True);
            yield return RightDrag(new Vector2(40f, 10f), moved);
            Assert.That(Mathf.Sign(moved[2].y), Is.EqualTo(-Mathf.Sign(moved[0].y)), "Inverted, the drag tilts the other way: " + moved[0] + " then " + moved[2]);
            Assert.That(Mathf.Sign(moved[2].x), Is.EqualTo(Mathf.Sign(moved[0].x)), "and turns the same way.");

            // The stick's tilt follows the inversion too, each measured from a level tilt so the clamp
            // at twenty degrees either way cannot swallow it.
            var pad = TestGamepad();
            LevelTheTilt();
            float tilt = cameraRig.PitchOffset;
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = new Vector2(0f, .5f) });
            yield return new WaitForSecondsRealtime(.1f);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            float invertedStick = cameraRig.PitchOffset - tilt;
            director.SetInvertTilt(false);
            LevelTheTilt();
            tilt = cameraRig.PitchOffset;
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = new Vector2(0f, .5f) });
            yield return new WaitForSecondsRealtime(.1f);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            float plainStick = cameraRig.PitchOffset - tilt;
            Assert.That(Mathf.Abs(plainStick), Is.GreaterThan(.1f), "The stick tilts the camera.");
            Assert.That(Mathf.Sign(invertedStick), Is.EqualTo(-Mathf.Sign(plainStick)), "Inverted, the stick tilts the other way.");

            // Q, per second of holding it, at the camera's own speed and at twice it.
            float slow = 0f, fast = 0f;
            foreach (float speed in new[] { 1f, 2f })
            {
                director.SetCameraSpeed(speed);
                float before = cameraRig.Yaw, began = Time.realtimeSinceStartup;
                yield return HoldKey(Key.Q);
                float rate = Mathf.Abs(Turned(before, cameraRig.Yaw)) / (Time.realtimeSinceStartup - began);
                if (speed == 1f) slow = rate; else fast = rate;
            }
            Assert.That(slow, Is.GreaterThan(1f), "Q turns the camera.");
            Assert.That(fast / slow, Is.InRange(1.5f, 2.6f), "Q turns about twice as fast at twice the speed: " + slow + " then " + fast + " degrees a second.");

            // Kept as the preferences keep them, and back to the camera's own for the next test.
            director.SetCameraSpeed(1f);
            Assert.That(director.CameraSpeed, Is.EqualTo(1f));
            Assert.That(cameraRig.OrbitSpeedScale, Is.EqualTo(1f));
        }

        /// <summary>
        /// A season with the story system on from its first week, at its start, and a pair of
        /// houseguests some walk-in arc will take in some room this week: what the proximity watch
        /// offers when the player walks in on them.
        /// </summary>
        private IEnumerator InstallWalkInFixture(System.Action<string, string, string> found)
        {
            EpisodeState fixture = null;
            string first = null, second = null, where = null;
            for (uint seed = 1; seed <= 60 && fixture == null; seed++)
            {
                var state = ContentCatalog.Create(seed);
                if (state.phase != EpisodePhase.Social) continue;
                EpisodeEngine.EnableStory(state, state.week);
                var pairs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).OrderBy(id => id, System.StringComparer.Ordinal).ToList();
                for (int a = 0; a < pairs.Count && fixture == null; a++)
                    for (int b = a + 1; b < pairs.Count && fixture == null; b++)
                        foreach (var room in RoomWords.Rooms)
                            if (EpisodeEngine.ProximityOpen(state, pairs[a], pairs[b], room))
                            { fixture = state; first = pairs[a]; second = pairs[b]; where = room; break; }
            }
            Assert.That(fixture, Is.Not.Null, "No bounded fixture with a walk-in the engine would take was found.");
            director.SuspendNpcAutonomyForDiagnostics();
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return Frames(2);
            found(first, second, where);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Story_AWalkInIsSteppedIntoByE()
        {
            string first = null, second = null, room = null;
            yield return InstallWalkInFixture((a, b, at) => { first = a; second = b; room = at; });
            director.ClosePanels();
            yield return Frames(2);
            Assert.That(director.StageWalkInForDiagnostics(first, second, room), Is.True, "The walk-in is staged.");
            yield return Frames(2);
            Assert.That(director.PullOffered, Is.EqualTo(EpisodeHud.StepInCaption), "Walking in on them offers Step in.");

            var before = director.Snapshot;
            yield return PressKey(Key.E);
            yield return Frames(2);
            Assert.That(director.IsSceneCardOpen, Is.True, "E steps in: the walk-in's card opens where the player stands.");
            // Read after the card is open: opening a panel may flush the held NPC tick as well.
            Assert.That(director.Snapshot.revision, Is.GreaterThan(before.revision), "Stepping in commits the walk-in.");
            Assert.That(EpisodeEngine.OpenStoryBeats(director.Snapshot).Count, Is.GreaterThan(EpisodeEngine.OpenStoryBeats(before).Count),
                "and its beat is open on the card.");
            Assert.That(director.PullOffered, Is.Null, "The Pull steps aside for the card.");
            director.ClosePanels();
            yield return null;
        }
    }
}
