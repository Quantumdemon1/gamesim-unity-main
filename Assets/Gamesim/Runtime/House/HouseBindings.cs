using System;
using System.Collections.Generic;
using Gamesim.Persistence;
using Gamesim.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Gamesim.House
{
    /// <summary>
    /// Rebinding in the house (PLAN A, A5): the player's choices (<see cref="KeyBindings"/>) applied
    /// to every copy of the game's actions - the rig's while one is up and the shared copy beside it
    /// (<see cref="HouseInput"/>) - kept in the session's preferences (A13), and the one press a
    /// rebinding listens for. Esc cancels a rebinding, and B one the pad asked for; a key the rules
    /// refuse leaves the action as it was and says why; nothing else the press might mean in the house
    /// acts on it (<see cref="Busy"/>).
    /// </summary>
    public static class HouseBindings
    {
        private static KeyBindings current = new KeyBindings();
        private static InputActionRebindingExtensions.RebindingOperation operation;
        private static InputAction probe;
        private static int settledFrame = int.MinValue;

        /// <summary>The player's choices as they stand.</summary>
        public static KeyBindings Current => current;

        /// <summary>Whether a rebinding is listening for its press.</summary>
        public static bool Listening => operation != null;

        /// <summary>The action a rebinding is listening for, or null.</summary>
        public static KeyBindings.Target ListeningFor { get; private set; }

        /// <summary>Whether the rebinding listening is listening to the pad: its words name the pad's way out, B.</summary>
        public static bool ListeningOnPad { get; private set; }

        /// <summary>
        /// Whether the pad's B cancels the rebinding listening: the pad asked for it, whichever device
        /// it listens to - a camera key pressed for from the pad listens for a key, and B is still the
        /// pad's way out (lane-input-5).
        /// </summary>
        public static bool PadCancels { get; private set; }

        /// <summary>
        /// Whether the house should leave this frame's presses alone: a rebinding is listening, or one
        /// ended this frame or the last - the press that ended it (a key, Esc, Enter) must not go on
        /// to open the notebook, close the settings or press the control that started it again.
        /// </summary>
        public static bool Busy => Listening || Holds(Time.frameCount, settledFrame);

        /// <summary>Whether <paramref name="stamped"/> is this frame or the one before: a stamp from another session is neither.</summary>
        private static bool Holds(int now, int stamped) => stamped != int.MinValue && now >= stamped && now - stamped <= 1;

        /// <summary>The session's choices from its store: PlayerPrefs, a root's own file, or the defaults. Applied at once everywhere.</summary>
        public static void Load(IPreferenceStore store)
        {
            Cancel();
            string json = store != null ? store.GetString(KeyBindings.PreferenceKey, "") : "";
            current = KeyBindings.Parse(json, out var reason);
            if (reason != null) Debug.LogWarning("Gamesim controls read as shipped where they could not be read: " + reason + ".");
            ApplyEverywhere();
        }

        /// <summary>Keeps the choices in the session's store.</summary>
        public static void Save(IPreferenceStore store)
        {
            if (store == null) return;
            store.SetString(KeyBindings.PreferenceKey, current.Count == 0 ? "" : current.ToJson());
            store.Save();
        }

        /// <summary>The choices on every live copy of the actions, and on the words on screen.</summary>
        public static void ApplyEverywhere()
        {
            KeyBindings.Show(current);
            foreach (var actions in HouseInput.Live()) Apply(actions);
        }

        /// <summary>
        /// The choices on one copy of the actions: every binding of a rebindable action as shipped,
        /// then each choice over its own - an action's own binding, or one part of its composite (the
        /// pan's four keys share one action, so its overrides go once and its four choices after).
        /// </summary>
        public static void Apply(HouseCameraActions actions)
        {
            if (actions?.Asset == null) return;
            var cleared = new HashSet<InputAction>();
            foreach (var target in KeyBindings.Targets)
            {
                var action = actions.Asset.FindAction(target.Path);
                if (action == null) continue;
                if (cleared.Add(action)) action.RemoveAllBindingOverrides();
                foreach (var scheme in new[] { KeyBindings.KeyboardScheme, KeyBindings.GamepadScheme })
                {
                    if (!target.Takes(scheme)) continue;
                    string path = current.Get(target, scheme);
                    int index = BindingIndex(action, scheme, target.Part);
                    if (path != null && index >= 0) action.ApplyBindingOverride(index, path);
                }
            }
        }

        /// <summary>The action's own binding on a scheme: its first that is not a composite or a part of one.</summary>
        public static int BindingIndex(InputAction action, string scheme) => BindingIndex(action, scheme, null);

        /// <summary>
        /// The binding a target rebinds on a scheme: with no <paramref name="part"/>, the action's first
        /// own binding of the scheme; with one, that part of the action's first composite on the scheme
        /// (the pan's WASD, not the arrows after it; the zoom's - and =, not the pad's triggers before them).
        /// </summary>
        public static int BindingIndex(InputAction action, string scheme, string part)
        {
            var bindings = action.bindings;
            if (part == null)
            {
                for (int index = 0; index < bindings.Count; index++)
                {
                    var binding = bindings[index];
                    if (binding.isComposite || binding.isPartOfComposite) continue;
                    if (InGroup(binding, scheme)) return index;
                }
                return -1;
            }
            for (int composite = 0; composite < bindings.Count; composite++)
            {
                if (!bindings[composite].isComposite) continue;
                int found = -1;
                bool ofScheme = false;
                for (int index = composite + 1; index < bindings.Count && bindings[index].isPartOfComposite; index++)
                {
                    if (InGroup(bindings[index], scheme)) ofScheme = true;
                    if (string.Equals(bindings[index].name, part, StringComparison.OrdinalIgnoreCase)) found = index;
                }
                if (ofScheme && found >= 0) return found;
            }
            return -1;
        }

        private static bool InGroup(InputBinding binding, string scheme)
        {
            foreach (var group in (binding.groups ?? "").Split(';'))
                if (group == scheme) return true;
            return false;
        }

        /// <summary>
        /// Every binding of the actions as they stand, composites' parts included, and the event
        /// system's navigation beside them (<see cref="PanelNavigation"/>), for the conflict check.
        /// </summary>
        public static List<KeyBindings.Binding> LiveBindings(HouseCameraActions actions) => LiveBindings(actions, PanelNavigation());

        /// <summary>
        /// Every binding of the actions as they stand, and <paramref name="panelNavigation"/>'s - the
        /// UI module's Navigate - as the <see cref="KeyBindings.PanelMap"/>'s: the D-pad, the sticks,
        /// WASD and the arrows that move between a panel's controls (D4).
        /// </summary>
        public static List<KeyBindings.Binding> LiveBindings(HouseCameraActions actions, InputAction panelNavigation)
        {
            var live = new List<KeyBindings.Binding>();
            if (actions?.Asset != null)
                foreach (var map in actions.Asset.actionMaps)
                    foreach (var action in map.actions)
                        AddBindings(live, map.name, action);
            if (panelNavigation != null) AddBindings(live, KeyBindings.PanelMap, panelNavigation);
            return live;
        }

        private static void AddBindings(List<KeyBindings.Binding> live, string map, InputAction action)
        {
            foreach (var binding in action.bindings)
            {
                if (binding.isComposite) continue;
                foreach (var group in (binding.groups ?? "").Split(';'))
                    if (group == KeyBindings.KeyboardScheme || group == KeyBindings.GamepadScheme)
                        live.Add(new KeyBindings.Binding(map, map == KeyBindings.PanelMap ? KeyBindings.PanelNavigate : action.name, group,
                            binding.effectivePath, binding.isPartOfComposite ? binding.name : null));
            }
        }

        /// <summary>
        /// The event system's navigation: the scene's UI module's Navigate, which moves between a
        /// panel's controls in every panel, or null where there is no UI module to ask.
        /// </summary>
        private static InputAction PanelNavigation()
        {
            var events = EventSystem.current;
            if (events == null) return null;
            var module = events.currentInputModule as InputSystemUIInputModule;
            if (module == null) module = events.GetComponent<InputSystemUIInputModule>();
            if (module == null || module.move == null) return null;
            return module.move.action;
        }

        /// <summary>
        /// Listens for the next press on the keyboard (or the pad, <paramref name="pad"/>, for a target
        /// the pad has a binding of its own for: the camera's keys always listen to the keyboard) and
        /// gives it to <paramref name="target"/>, if the rules allow; Esc cancels, and when the pad
        /// asked, B, its way out, as well - a camera key's rebinding among them (lane-input-5).
        /// <paramref name="done"/> is told the outcome in the player's words, and whether anything
        /// changed. One rebinding at a time: asking for another while one listens cancels the first.
        /// </summary>
        public static void Listen(KeyBindings.Target target, bool pad, IPreferenceStore store, Action<string, bool> done)
        {
            Cancel();
            if (target == null) return;
            bool padAsked = pad;
            pad = pad && target.OnPad;
            string scheme = pad ? KeyBindings.GamepadScheme : KeyBindings.KeyboardScheme;
            string keeps = target.Name + " keeps its " + (pad ? "button" : "key") + ".";
            ListeningFor = target;
            ListeningOnPad = pad;
            PadCancels = padAsked;
            // A probe of its own, never one of the game's actions: nothing the house reads is
            // switched off while it listens, and the probe is thrown away after.
            probe = new InputAction("Rebind " + target.Name, InputActionType.Button, pad ? "<Gamepad>/buttonSouth" : "<Keyboard>/space");
            var listen = probe.PerformInteractiveRebinding(0)
                .WithControlsHavingToMatchPath(pad ? "<Gamepad>" : "<Keyboard>");
            // A key asked for from the pad listens to the pad's B as well, its way out: the paths add up.
            if (padAsked && !pad) listen = listen.WithControlsHavingToMatchPath(PadWayOut);
            operation = listen
                .WithControlsExcluding("<Keyboard>/anyKey")
                .WithControlsExcluding("<Mouse>")
                .WithExpectedControlType("Button")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0f)
                .OnComplete(finished =>
                {
                    string path = probe.bindings[0].effectivePath;
                    End();
                    // The pad's B is its way out: asked for from the pad, it cancels as Esc does. Only one
                    // control can cancel a rebinding, and "*/{Cancel}" would take the opening press's
                    // release on a pad whose A is its Cancel (the Switch's and iOS layouts), so B is
                    // read here, as the press the rebinding took.
                    if (padAsked && string.Equals(path, PadWayOut, StringComparison.OrdinalIgnoreCase)) { done?.Invoke(keeps, false); return; }
                    string refusal = KeyBindings.Check(LiveBindings(HouseInput.Actions), target, scheme, path);
                    if (refusal != null) { done?.Invoke(refusal, false); return; }
                    current.Set(target, scheme, path);
                    ApplyEverywhere();
                    Save(store);
                    done?.Invoke(target.Name + " is now " + InputGlossary.BindingLabel(path) + (pad ? " on the pad." : "."), true);
                })
                .OnCancel(cancelled =>
                {
                    End();
                    done?.Invoke(keeps, false);
                })
                .Start();
        }

        /// <summary>Every binding as shipped again, applied everywhere and kept.</summary>
        public static void ResetAll(IPreferenceStore store)
        {
            Cancel();
            current.Clear();
            ApplyEverywhere();
            Save(store);
        }

        /// <summary>Stops a rebinding that is listening, with nothing changed and nobody told.</summary>
        public static void Cancel()
        {
            if (operation == null) return;
            var listening = operation;
            operation = null;
            listening.Dispose();
            End();
        }

        /// <summary>The pad's B: Back and the panels' Cancel, which a rebinding the pad asked for takes as its cancel.</summary>
        private const string PadWayOut = "<Gamepad>/buttonEast";

        private static void End()
        {
            settledFrame = Time.frameCount;
            ListeningFor = null;
            ListeningOnPad = false;
            PadCancels = false;
            var finished = operation;
            operation = null;
            finished?.Dispose();
            probe?.Dispose();
            probe = null;
        }

        /// <summary>Domain reload is off at play start: a session starts with every binding as shipped and nothing listening.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetTheLastSession()
        {
            operation?.Dispose();
            operation = null;
            probe?.Dispose();
            probe = null;
            ListeningFor = null;
            ListeningOnPad = false;
            PadCancels = false;
            settledFrame = int.MinValue;
            current = new KeyBindings();
            KeyBindings.Show(null);
        }
    }
}
