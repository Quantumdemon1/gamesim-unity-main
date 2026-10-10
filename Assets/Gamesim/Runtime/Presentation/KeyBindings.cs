using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Rebinding (PLAN A, A5; the lead's decision A-2): which of the house's actions a player may give
    /// a key or a pad button of their own, the rules a new binding must keep, the choices as kept in the
    /// preferences, and the words on screen that follow them.
    ///
    /// <list type="bullet">
    /// <item><b>What rebinds.</b> The house's shortcuts and the camera's recentre
    /// (<see cref="Targets"/>), one binding each on the keyboard and one on the pad. The ways out -
    /// Menu (Esc, Start), Back (B) and the event system's Submit and Cancel (Enter, Esc) - are never
    /// offered, and their keys are never taken: nothing a player does leaves the game without a way
    /// to close a panel or press a control (<see cref="ReservedPaths"/>).</item>
    /// <item><b>Conflicts.</b> A key one action of a context already answers to is refused for
    /// another of the same context: the camera and the shortcuts are both live in the house, so they
    /// are one context (<see cref="Check"/>).</item>
    /// <item><b>Kept.</b> As one JSON object, "Map/Action@Scheme" to the control's path, under
    /// <see cref="PreferenceKey"/>: PlayerPrefs, or a playtest root's own file (A13).</item>
    /// <item><b>Shown.</b> A caption keeps its English words as the control's name - the tests and a
    /// screen reader find it by them - and the words a player sees are substituted as they are drawn
    /// (<see cref="Substitute"/>, at the HUD's text sink): "Notebook [J]" reads "Notebook [K]" once
    /// the notebook answers to K. With every binding as shipped, every word on screen is byte for
    /// byte what it was. The pad's words in the hints come from <see cref="PadLabel"/>.</item>
    /// </list>
    ///
    /// <para>Unity-free, so the subset holds the rules, the format and the words; the editor's side
    /// (<c>HouseBindings</c>) listens for the press and applies the choices to the actions.</para>
    /// </summary>
    public sealed class KeyBindings
    {
        public const string PreferenceKey = "Gamesim.Bindings";
        public const string KeyboardScheme = "Keyboard&Mouse", GamepadScheme = "Gamepad";

        /// <summary>The settings' control that sets every binding back as shipped.</summary>
        public const string ResetCaption = "Reset all controls";

        /// <summary>An action a player may rebind: its map and name, and the word its control and messages use.</summary>
        public sealed class Target
        {
            public string Map { get; }
            public string Action { get; }
            public string Name { get; }
            public Target(string map, string action, string name) { Map = map; Action = action; Name = name; }
            /// <summary>"Shortcuts/Notebook".</summary>
            public string Path => Map + "/" + Action;
            /// <summary>The settings' control: "Rebind Notebook".</summary>
            public string Caption => "Rebind " + Name;
        }

        /// <summary>The actions a player may rebind, in the order the settings list them.</summary>
        public static readonly Target[] Targets =
        {
            new Target("Shortcuts", "Notebook", "Notebook"),
            new Target("Shortcuts", "Save", "Save"),
            new Target("Shortcuts", "Diary", "Diary"),
            new Target("Shortcuts", "Interact", "Interact"),
            new Target("Shortcuts", "Hit", "Hit"),
            new Target("Shortcuts", "Overview", "Overview"),
            new Target("Shortcuts", "Emote", "Emote"),
            new Target("Camera", "Recenter", "Recenter"),
        };

        /// <summary>The actions never offered: the ways out of every panel and the press of a control.</summary>
        public static readonly string[] ProtectedActions = { "Shortcuts/Menu", "Shortcuts/Back", "UI/Submit", "UI/Cancel" };

        /// <summary>
        /// The keys and buttons the protected actions answer to, which no other action may take:
        /// Esc (Menu, Cancel), Enter and the keypad's Enter (Submit), Start (Menu) and B (Back).
        /// </summary>
        public static readonly string[] ReservedPaths =
            { "<Keyboard>/escape", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Gamepad>/start", "<Gamepad>/buttonEast" };

        /// <summary>The target for an action, or null when it is not one a player may rebind.</summary>
        public static Target FindTarget(string map, string action)
        {
            foreach (var target in Targets)
                if (target.Map == map && target.Action == action) return target;
            return null;
        }

        /// <summary>The context an action's map belongs to: the camera and the shortcuts are both the house.</summary>
        public static string ContextOf(string map) => map == "Camera" || map == "Shortcuts" ? "House" : map;

        /// <summary>One binding as the actions hold it now, for the conflict check.</summary>
        public readonly struct Binding
        {
            public readonly string Map, Action, Scheme, Path;
            public Binding(string map, string action, string scheme, string path) { Map = map; Action = action; Scheme = scheme; Path = path; }
        }

        // ---------------------------------------------------------------- the choices

        private readonly SortedDictionary<string, string> overrides = new SortedDictionary<string, string>(StringComparer.Ordinal);

        private static string Key(string map, string action, string scheme) => map + "/" + action + "@" + scheme;

        /// <summary>Every choice, "Map/Action@Scheme" to the control's path, in ordinal order.</summary>
        public IReadOnlyDictionary<string, string> Overrides => overrides;

        public int Count => overrides.Count;

        /// <summary>The path a player gave this action on this scheme, or null when it is as shipped.</summary>
        public string Get(string map, string action, string scheme) =>
            overrides.TryGetValue(Key(map, action, scheme), out var path) ? path : null;

        /// <summary>Gives an action a path of the player's on a scheme; null sets it back as shipped.</summary>
        public void Set(string map, string action, string scheme, string path)
        {
            if (FindTarget(map, action) == null) throw new ArgumentException(map + "/" + action + " is not an action a player may rebind.");
            if (string.IsNullOrEmpty(path)) overrides.Remove(Key(map, action, scheme));
            else overrides[Key(map, action, scheme)] = path;
        }

        /// <summary>Every binding as shipped.</summary>
        public void Clear() => overrides.Clear();

        /// <summary>The device a path names: the keyboard's scheme, the pad's, or null for anything else.</summary>
        public static string SchemeOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (path.StartsWith("<Keyboard>/", StringComparison.Ordinal)) return KeyboardScheme;
            if (path.StartsWith("<Gamepad>/", StringComparison.Ordinal)) return GamepadScheme;
            return null;
        }

        /// <summary>
        /// Why <paramref name="path"/> cannot be <paramref name="map"/>/<paramref name="action"/>'s on
        /// <paramref name="scheme"/>, in the player's words, or null when it can: the action is not one
        /// a player may rebind, the path is another device's, it is a way out the game keeps, or
        /// another action of the same context already answers to it (<paramref name="live"/>, every
        /// binding as the actions hold it now - composites' parts included).
        /// </summary>
        public static string Check(IEnumerable<Binding> live, string map, string action, string scheme, string path)
        {
            var target = FindTarget(map, action);
            if (target == null) return map + "/" + action + " cannot be rebound.";
            if (SchemeOf(path) != scheme) return "That is not a " + (scheme == GamepadScheme ? "pad button" : "key") + ".";
            string words = InputGlossary.BindingLabel(path);
            foreach (var reserved in ReservedPaths)
                if (string.Equals(reserved, path, StringComparison.OrdinalIgnoreCase))
                    return words + " closes panels and presses controls, so it stays as it is. " + target.Name + " keeps its " + (scheme == GamepadScheme ? "button" : "key") + ".";
            string context = ContextOf(map);
            if (live != null)
                foreach (var binding in live)
                {
                    if (binding.Scheme != scheme || ContextOf(binding.Map) != context) continue;
                    if (binding.Map == map && binding.Action == action) continue;
                    if (!string.Equals(binding.Path, path, StringComparison.OrdinalIgnoreCase)) continue;
                    var other = FindTarget(binding.Map, binding.Action);
                    string otherName = other != null ? other.Name : binding.Action;
                    return words + " is already " + otherName + "'s. " + target.Name + " keeps its " + (scheme == GamepadScheme ? "button" : "key") + ".";
                }
            return null;
        }

        // ---------------------------------------------------------------- the kept form

        /// <summary>The choices as kept: one JSON object, its keys in ordinal order; "{}" with none.</summary>
        public string ToJson()
        {
            var text = new StringBuilder();
            using (var writer = new StringWriter(text, CultureInfo.InvariantCulture))
            using (var json = new JsonTextWriter(writer) { Formatting = Formatting.None })
            {
                json.WriteStartObject();
                foreach (var pair in overrides)
                {
                    json.WritePropertyName(pair.Key);
                    json.WriteValue(pair.Value);
                }
                json.WriteEndObject();
            }
            return text.ToString();
        }

        /// <summary>
        /// The choices a kept text holds. Anything a player could not have chosen - an action not on
        /// the list, a path of the wrong device, a way out, a malformed text - is dropped, with the
        /// reason; what is left is still a set the game can play with. Never throws.
        /// </summary>
        public static KeyBindings Parse(string json, out string reason)
        {
            reason = null;
            var bindings = new KeyBindings();
            if (string.IsNullOrWhiteSpace(json)) return bindings;
            JObject root;
            try { root = JObject.Parse(json); }
            catch (JsonException error) { reason = "the kept controls are not valid JSON: " + error.Message; return bindings; }
            foreach (var property in root.Properties())
            {
                string key = property.Name;
                int at = key.LastIndexOf('@'), slash = key.IndexOf('/');
                if (at < 0 || slash < 0 || slash > at || property.Value.Type != JTokenType.String) { reason = "a kept control (" + key + ") is not one the game reads"; continue; }
                string map = key.Substring(0, slash), action = key.Substring(slash + 1, at - slash - 1), scheme = key.Substring(at + 1);
                string path = (string)property.Value;
                if (FindTarget(map, action) == null || SchemeOf(path) != scheme || IsReserved(path))
                { reason = "a kept control (" + key + ") is not one a player may choose"; continue; }
                bindings.overrides[Key(map, action, scheme)] = path;
            }
            return bindings;
        }

        private static bool IsReserved(string path)
        {
            foreach (var reserved in ReservedPaths)
                if (string.Equals(reserved, path, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>The choices in the player log's words: "default", or "Notebook:K,Diary:Y(pad)".</summary>
        public string Summary()
        {
            if (overrides.Count == 0) return "default";
            var words = new List<string>();
            foreach (var target in Targets)
            {
                string key = Get(target.Map, target.Action, KeyboardScheme), pad = Get(target.Map, target.Action, GamepadScheme);
                if (key != null) words.Add(target.Name + ":" + InputGlossary.BindingLabel(key));
                if (pad != null) words.Add(target.Name + ":" + InputGlossary.BindingLabel(pad) + "(pad)");
            }
            return string.Join(",", words);
        }

        // ---------------------------------------------------------------- the words on screen

        /// <summary>The choices the words on screen follow: set as the session's are loaded and changed.</summary>
        private static KeyBindings shown;

        /// <summary>Makes <paramref name="bindings"/> the choices the words on screen follow; null is every binding as shipped.</summary>
        public static void Show(KeyBindings bindings) => shown = bindings;

        /// <summary>A rebound action's key in the hints' words ("K"), or null while it is as shipped.</summary>
        public static string KeyLabel(string map, string action)
        {
            string path = shown?.Get(map, action, KeyboardScheme);
            return path == null ? null : InputGlossary.BindingLabel(path);
        }

        /// <summary>A rebound action's pad button in the hints' words ("Y"), or null while it is as shipped.</summary>
        public static string PadLabel(string map, string action)
        {
            string path = shown?.Get(map, action, GamepadScheme);
            return path == null ? null : InputGlossary.BindingLabel(path);
        }

        /// <summary>
        /// A key's place in the words on screen: the text before the key, the key as shipped, the text
        /// after it, and the action whose key it is. Only these places change, so a word that happens
        /// to be a key's letter anywhere else stays as written.
        /// </summary>
        private readonly struct KeyPlace
        {
            public readonly string Before, Key, After, Map, Action;
            public KeyPlace(string before, string key, string after, string map, string action)
            { Before = before; Key = key; After = after; Map = map; Action = action; }
        }

        private static readonly KeyPlace[] places =
        {
            // The captions that name their key in brackets: every [J] is the notebook's, every [F5]
            // the save's, every [R] the diary's, every [E] Interact's. [Space] is the house
            // challenge's Hit only on its STOP control: a competition's Hit, which is not rebound,
            // names Space too.
            new KeyPlace("[", "J", "]", "Shortcuts", "Notebook"),
            new KeyPlace("[", "F5", "]", "Shortcuts", "Save"),
            new KeyPlace("[", "R", "]", "Shortcuts", "Diary"),
            new KeyPlace("[", "E", "]", "Shortcuts", "Interact"),
            new KeyPlace(InputGlossary.HouseChallengeStopCaption.Substring(0, InputGlossary.HouseChallengeStopCaption.Length - "Space]".Length), "Space", "]", "Shortcuts", "Hit"),
            // The help card's lines, the tour's line and the status lines that name a key.
            new KeyPlace("", "F", ": recenter", "Camera", "Recenter"),
            new KeyPlace("", "R", ": diary", "Shortcuts", "Diary"),
            new KeyPlace("", "E", ": interact", "Shortcuts", "Interact"),
            new KeyPlace("Press ", "E", " near", "Shortcuts", "Interact"),
            new KeyPlace("press ", "E", " to open", "Shortcuts", "Interact"),
            new KeyPlace("  ·  ", "E", " to ", "Shortcuts", "Interact"),
            new KeyPlace("  ·  ", "E", " or a click", "Shortcuts", "Interact"),
            new KeyPlace("Press ", "Space", " or STOP", "Shortcuts", "Hit"),
        };

        /// <summary>
        /// The words a player sees for <paramref name="text"/>: every place a rebound key is named,
        /// in the key the player chose; the house prompt's leading key ("E  ·  Talk to Maya") too.
        /// With every key as shipped the text itself comes back, the same string.
        /// </summary>
        public static string Substitute(string text)
        {
            if (string.IsNullOrEmpty(text) || shown == null || shown.overrides.Count == 0) return text;
            // The places whose key the player changed, each with the words it becomes.
            var changed = new List<(string From, string To)>();
            foreach (var place in places)
            {
                string label = KeyLabel(place.Map, place.Action);
                if (label == null || label == place.Key) continue;
                changed.Add((place.Before + place.Key + place.After, place.Before + label + place.After));
            }
            string interact = KeyLabel("Shortcuts", "Interact");
            if (changed.Count == 0 && interact == null) return text;
            // One pass over the text as written, so a key given to one action and another's given to
            // the first - the notebook on R, the diary on J - cannot be swapped twice.
            var result = new StringBuilder(text.Length + 8);
            int at = 0;
            // The house prompt: its leading key, two spaces, and what it does.
            if (interact != null && interact != "E" && text.StartsWith("E  ", StringComparison.Ordinal)) { result.Append(interact); at = 1; }
            while (at < text.Length)
            {
                bool replaced = false;
                foreach (var (from, to) in changed)
                {
                    if (string.CompareOrdinal(text, at, from, 0, from.Length) != 0 || at + from.Length > text.Length) continue;
                    result.Append(to);
                    at += from.Length;
                    replaced = true;
                    break;
                }
                if (!replaced) result.Append(text[at++]);
            }
            string substituted = result.ToString();
            return substituted == text ? text : substituted;
        }
    }
}
