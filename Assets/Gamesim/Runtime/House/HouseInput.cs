using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;

namespace Gamesim.House
{
    /// <summary>
    /// Where everything that is not the rig or the director reads the game's actions (PLAN A, A1):
    /// the ceremony cards, a competition's board and results, the season report, the opening, the
    /// HUD's ring and the prototype house. One asset, so a pad, a rebinding and a test all go the
    /// same way, and nothing reads a device directly but the pointer on the house and typing.
    ///
    /// <para>The rig's actions when a rig is up - it registers them while it is enabled, so the
    /// house reads one asset - and otherwise a shared copy built from the code, enabled from the
    /// moment the game starts. That copy is never switched off while the game runs: a press is only
    /// ever seen by an action that was enabled when it arrived, so an action switched on by the
    /// first frame that asks for it would miss the very press it was asked about. A screen built
    /// on its own, as the presentation tests build them, reads the shared copy.</para>
    ///
    /// <para>It also says which kind of device was pressed this frame (<see cref="PadUsed"/>), for
    /// the hints that follow the device: any button on a pad says pad, any key or mouse button says
    /// keyboard. Each reader keeps its own idea of the last one, as the ceremony cards always have,
    /// so a new scene starts on the keyboard's words whatever the last scene was left on.</para>
    /// </summary>
    public static class HouseInput
    {
        private static readonly List<HouseCameraActions> registered = new List<HouseCameraActions>();
        private static HouseCameraActions shared;
        private static IDisposable buttonWatch;
        private static int padPressFrame = -1, keyPressFrame = -1;

        /// <summary>The actions to read: the most recently enabled rig's, or the shared copy.</summary>
        public static HouseCameraActions Actions
        {
            get
            {
                for (var index = registered.Count - 1; index >= 0; index--)
                {
                    var actions = registered[index];
                    if (actions != null && actions.Asset != null) return actions;
                    registered.RemoveAt(index);
                }
                return Shared;
            }
        }

        /// <summary>The copy built from the code, for when no rig is up.</summary>
        private static HouseCameraActions Shared
        {
            get
            {
                if (shared == null || shared.Asset == null)
                {
                    shared = new HouseCameraActions();
                    shared.Enable();
                }
                WatchButtons();
                return shared;
            }
        }

        /// <summary>A rig's actions, enabled: what the house reads until the rig is disabled.</summary>
        public static void Register(HouseCameraActions actions)
        {
            if (actions == null) return;
            registered.Remove(actions);
            registered.Add(actions);
            WatchButtons();
        }

        /// <summary>A rig going away: the house reads the rig enabled before it, or the shared copy.</summary>
        public static void Unregister(HouseCameraActions actions) => registered.Remove(actions);

        /// <summary>
        /// Where this frame's press came from: true for a pad, false for a key or a mouse button,
        /// null when nothing was pressed. A pad press and a key in the same frame read as the pad,
        /// as the ceremony cards always read them.
        /// </summary>
        public static bool? PadUsed()
        {
            WatchButtons();
            int frame = Time.frameCount;
            if (padPressFrame == frame) return true;
            if (keyPressFrame == frame) return false;
            return null;
        }

        /// <summary>
        /// Whether <paramref name="action"/> was pressed this frame through <paramref name="key"/>:
        /// one of its bindings resolved to that key, and that key went down this frame.
        /// </summary>
        public static bool PressedByKey(InputAction action, Key key)
        {
            if (action == null || !action.WasPressedThisFrame()) return false;
            foreach (var control in action.controls)
                if (control is KeyControl pressed && pressed.keyCode == key && pressed.wasPressedThisFrame) return true;
            return false;
        }

        /// <summary>Whether <paramref name="action"/> was pressed this frame by a keyboard key, rather than a pad or the mouse.</summary>
        public static bool PressedByKeyboard(InputAction action)
        {
            if (action == null || !action.WasPressedThisFrame()) return false;
            foreach (var control in action.controls)
                if (control is KeyControl pressed && pressed.wasPressedThisFrame) return true;
            return false;
        }

        /// <summary>
        /// Tab this frame, through the camera's Next and Previous: the HUD's ring steps on it while
        /// a control is focused, and the director follows on it while none is. Shift+Tab is
        /// <paramref name="reverse"/>.
        /// </summary>
        public static bool TabStep(out bool reverse)
        {
            var actions = Actions;
            reverse = PressedByKey(actions.Previous, Key.Tab);
            return reverse || PressedByKey(actions.Next, Key.Tab);
        }

        private static void WatchButtons()
        {
            if (buttonWatch != null) return;
            buttonWatch = InputSystem.onAnyButtonPress.Call(NoteButton);
        }

        private static void NoteButton(InputControl control)
        {
            if (control == null) return;
            if (control.device is Gamepad) padPressFrame = Time.frameCount;
            else if (control.device is Keyboard || control.device is Mouse) keyPressFrame = Time.frameCount;
        }

        /// <summary>From the first frame: the shared copy is listening before any press can arrive.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ListenFromTheStart() => _ = Shared;

        /// <summary>
        /// Domain reload is off at play start, so these outlive a play session: what the last one
        /// registered and built, and its frame stamps, are let go before the next one begins.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetTheLastSession()
        {
            registered.Clear();
            buttonWatch?.Dispose();
            buttonWatch = null;
            shared?.Dispose();
            shared = null;
            padPressFrame = keyPressFrame = -1;
        }
    }
}
