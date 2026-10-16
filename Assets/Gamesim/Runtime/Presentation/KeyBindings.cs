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
    /// <item><b>What rebinds.</b> The house's shortcuts and the camera's recentre, one binding each on
    /// the keyboard and one on the pad; the camera's own keys - the orbit's Q and C, the zoom's -
    /// and =, the pan's W, A, S and D - each a part of its action's keyboard composite, on the
    /// keyboard only: the pad turns, zooms and pans with its sticks and triggers; and a competition
    /// board's grip (Space, RT) and take-back (Backspace, X), never on a letter, which the word board
    /// spells with (<see cref="Targets"/>). The ways out - Menu (Esc, Start), Back (B) and the event system's
    /// Submit and Cancel (Enter, A; Esc, B) - are never offered, and their keys are never taken: nothing a
    /// player does leaves the game without a way to close a panel or press a control
    /// (<see cref="ReservedPaths"/>).</item>
    /// <item><b>Conflicts.</b> A key one action of a context already answers to is refused for
    /// another of the same context: the camera and the shortcuts are both live in the house, so they
    /// are one context (<see cref="Check(IEnumerable{Binding}, Target, string, string)"/>). A
    /// composite's parts are bindings too: the pan's up is refused the pan's left; and a binding to a
    /// whole control answers for its parts - the pan's left stick for each of its directions. The
    /// event system's navigation moves between a panel's controls in every panel, so an action the
    /// house answers to over a panel (<see cref="Target.OverPanels"/>) is refused its keys and
    /// buttons too (<see cref="PanelMap"/>); and with no panel open it walks the HUD's ring up and
    /// down, so no house shortcut may have its up or its down. An action's own key or button as
    /// shipped is always its to take back, beside the bindings nobody rebinds that shared it as
    /// shipped.</item>
    /// <item><b>Kept.</b> As one JSON object, "Map/Action@Scheme" - "Map/Action#Part@Scheme" for a
    /// composite's part - to the control's path, under <see cref="PreferenceKey"/>: PlayerPrefs, or a
    /// playtest root's own file (A13).</item>
    /// <item><b>Shown.</b> A caption keeps its English words as the control's name - the tests and a
    /// screen reader find it by them - and the words a player sees are substituted as they are drawn
    /// (<see cref="Substitute"/>, at the HUD's text sink): "Notebook [J]" reads "Notebook [K]" once
    /// the notebook answers to K, and the help card's "WASD/arrows: pan" names the pan's keys as they
    /// are. With every binding as shipped, every word on screen is byte for byte what it was. The
    /// pad's words in the hints come from <see cref="PadLabel"/>.</item>
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

        /// <summary>An action a player may rebind - or one part of its keyboard composite - and the word its control and messages use.</summary>
        public sealed class Target
        {
            public string Map { get; }
            public string Action { get; }
            /// <summary>The composite part this target rebinds ("Up", "Negative"), or null for the action's own binding.</summary>
            public string Part { get; }
            public string Name { get; }
            /// <summary>Whether the pad has a binding of its own to rebind here: the camera's keys are the keyboard's alone.</summary>
            public bool OnPad { get; }
            /// <summary>
            /// Whether the house answers to it while a panel is open: the notebook's key jumps to its
            /// page and the save's saves from inside any panel (EpisodeDirector.Update), where the
            /// event system's navigation is moving between the panel's controls. The rest act with no
            /// panel open - the diary, Interact, the overview and your moves, and the camera - but for
            /// the house challenge's Hit, which is read over the timing bar's panel, a board no
            /// shipped season plays (EpisodeDirector.StartTimingBarForDiagnostics); its ring walks up
            /// and down only, as every house shortcut's navigation does (<see cref="PanelMap"/>).
            /// </summary>
            public bool OverPanels { get; }
            public Target(string map, string action, string name, string part = null, bool onPad = true, bool overPanels = false)
            {
                Map = map; Action = action; Name = name; Part = part; OnPad = onPad; OverPanels = overPanels;
            }
            /// <summary>"Shortcuts/Notebook": the action, as the actions asset finds it.</summary>
            public string Path => Map + "/" + Action;
            /// <summary>The settings' control: "Rebind Notebook".</summary>
            public string Caption => "Rebind " + Name;
            /// <summary>Whether this target takes a binding on <paramref name="scheme"/>.</summary>
            public bool Takes(string scheme) => scheme == KeyboardScheme || scheme == GamepadScheme && OnPad;
        }

        /// <summary>The actions a player may rebind, in the order the settings list them.</summary>
        public static readonly Target[] Targets =
        {
            new Target("Shortcuts", "Notebook", "Notebook", overPanels: true),
            new Target("Shortcuts", "Save", "Save", overPanels: true),
            new Target("Shortcuts", "Diary", "Diary"),
            new Target("Shortcuts", "Interact", "Interact"),
            new Target("Shortcuts", "Hit", "Hit"),
            new Target("Shortcuts", "Overview", "Overview"),
            new Target("Shortcuts", "Emote", "Emote"),
            new Target("Camera", "Recenter", "Recenter"),
            // The camera's keys: parts of the keyboard's composites (HouseCameraActions). The pan's
            // are WASD's; the arrows beside them stay as shipped.
            new Target("Camera", "OrbitRate", "Orbit left", "Left", onPad: false),
            new Target("Camera", "OrbitRate", "Orbit right", "Right", onPad: false),
            new Target("Camera", "ZoomRate", "Zoom out", "Negative", onPad: false),
            new Target("Camera", "ZoomRate", "Zoom in", "Positive", onPad: false),
            new Target("Camera", "Pan", "Pan up", "Up", onPad: false),
            new Target("Camera", "Pan", "Pan left", "Left", onPad: false),
            new Target("Camera", "Pan", "Pan down", "Down", onPad: false),
            new Target("Camera", "Pan", "Pan right", "Right", onPad: false),
            // A competition's board: the endurance game's grip, held, and the word game's take-back.
            // Never a letter: the word board spells with every one (CompetitionLetterRefusal).
            new Target("Competition", "Hold", "Hold grip"),
            new Target("Competition", "Undo", "Take back letter"),
        };

        /// <summary>The actions never offered: the ways out of every panel and the press of a control.</summary>
        public static readonly string[] ProtectedActions = { "Shortcuts/Menu", "Shortcuts/Back", "UI/Submit", "UI/Cancel" };

        /// <summary>
        /// The keys and buttons the protected actions answer to, which no other action may take:
        /// Esc (Menu, Cancel), Enter and the keypad's Enter (Submit), Start (Menu), B (Back, Cancel)
        /// and A (Submit: the UI module presses the focused control with it). A is the house
        /// challenge's Hit as shipped, which it keeps: an action's own button as shipped is always
        /// its own (<see cref="Check(IEnumerable{Binding}, Target, string, string)"/>).
        /// </summary>
        public static readonly string[] ReservedPaths =
            { "<Keyboard>/escape", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Gamepad>/start", "<Gamepad>/buttonEast", "<Gamepad>/buttonSouth" };

        /// <summary>
        /// The event system's map and its navigation: the UI module moves between a panel's controls
        /// with Navigate - the D-pad, the sticks, WASD and the arrows - in every panel. The conflict
        /// check reads it beside the house's own bindings (HouseBindings.LiveBindings), and refuses
        /// its keys and buttons to an action the house answers to over a panel
        /// (<see cref="Target.OverPanels"/>): a D-pad press meant to move down a list must not open
        /// the notebook. With no panel open the HUD keeps a control focused and its ring walks up
        /// and down (EpisodeHud), so Navigate's up and down - the D-pad's, the left stick's, W, S
        /// and the up and down arrows - are no house shortcut's either: a press meant to walk the
        /// ring must not send the player to the diary. Left and right, which the rings never use,
        /// stay free, as your moves' D-pad right always has; an action's own as shipped is its own
        /// (the overview's D-pad up); and the camera's keys move the view beside the ring, as the
        /// pan's WASD and arrows always have.
        /// </summary>
        public const string PanelMap = "UI", PanelNavigate = "Navigate";

        /// <summary>The target for an action's own binding, or null when it is not one a player may rebind.</summary>
        public static Target FindTarget(string map, string action) => FindTarget(map, action, null);

        /// <summary>The target for an action's binding or one part of its composite, or null.</summary>
        public static Target FindTarget(string map, string action, string part)
        {
            foreach (var target in Targets)
                if (target.Map == map && target.Action == action && target.Part == part) return target;
            return null;
        }

        /// <summary>The context an action's map belongs to: the camera and the shortcuts are both the house.</summary>
        public static string ContextOf(string map) => map == "Camera" || map == "Shortcuts" ? "House" : map;

        /// <summary>One binding as the actions hold it now, for the conflict check; <see cref="Part"/> names a composite's part.</summary>
        public readonly struct Binding
        {
            public readonly string Map, Action, Scheme, Path, Part;
            public Binding(string map, string action, string scheme, string path, string part = null)
            { Map = map; Action = action; Scheme = scheme; Path = path; Part = part; }
        }

        // ---------------------------------------------------------------- the choices

        private readonly SortedDictionary<string, string> overrides = new SortedDictionary<string, string>(StringComparer.Ordinal);

        private static string Key(Target target, string scheme) =>
            target.Map + "/" + target.Action + (target.Part != null ? "#" + target.Part : "") + "@" + scheme;

        /// <summary>Every choice, "Map/Action@Scheme" (or "Map/Action#Part@Scheme") to the control's path, in ordinal order.</summary>
        public IReadOnlyDictionary<string, string> Overrides => overrides;

        public int Count => overrides.Count;

        /// <summary>The path a player gave this action on this scheme, or null when it is as shipped.</summary>
        public string Get(string map, string action, string scheme)
        {
            var target = FindTarget(map, action);
            return target == null ? null : Get(target, scheme);
        }

        /// <summary>The path a player gave this target on this scheme, or null when it is as shipped.</summary>
        public string Get(Target target, string scheme) =>
            target != null && overrides.TryGetValue(Key(target, scheme), out var path) ? path : null;

        /// <summary>Gives an action a path of the player's on a scheme; null sets it back as shipped.</summary>
        public void Set(string map, string action, string scheme, string path)
        {
            var target = FindTarget(map, action);
            if (target == null) throw new ArgumentException(map + "/" + action + " is not an action a player may rebind.");
            Set(target, scheme, path);
        }

        /// <summary>
        /// Gives a target a path of the player's on a scheme; null - or the target's own path as
        /// shipped (<see cref="ShippedPath(Target, string)"/>) - sets it back as shipped, so a choice
        /// taken back is no choice at all: nothing kept, nothing said in the log.
        /// </summary>
        public void Set(Target target, string scheme, string path)
        {
            if (target == null || !target.Takes(scheme))
                throw new ArgumentException((target != null ? target.Name : "That") + " is not rebound on " + scheme + ".");
            if (string.IsNullOrEmpty(path) || IsShipped(target, scheme, path)) overrides.Remove(Key(target, scheme));
            else overrides[Key(target, scheme)] = path;
        }

        /// <summary>Whether <paramref name="path"/> is the one <paramref name="target"/> answers to on <paramref name="scheme"/> as shipped.</summary>
        private static bool IsShipped(Target target, string scheme, string path) =>
            path != null && string.Equals(path, ShippedPath(target, scheme), StringComparison.OrdinalIgnoreCase);

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
        /// Why <paramref name="path"/> cannot be <paramref name="map"/>/<paramref name="action"/>'s own
        /// binding on <paramref name="scheme"/>, in the player's words, or null when it can
        /// (<see cref="Check(IEnumerable{Binding}, Target, string, string)"/>).
        /// </summary>
        public static string Check(IEnumerable<Binding> live, string map, string action, string scheme, string path)
        {
            var target = FindTarget(map, action);
            if (target == null) return map + "/" + action + " cannot be rebound.";
            return Check(live, target, scheme, path);
        }

        /// <summary>
        /// Why <paramref name="path"/> cannot be <paramref name="target"/>'s on <paramref name="scheme"/>,
        /// in the player's words, or null when it can: the target takes nothing on that device, the
        /// path is another device's, it is a way out the game keeps, or another binding of the same
        /// context already answers to it (<paramref name="live"/>, every binding as the actions hold it
        /// now - composites' parts included, and the event system's navigation,
        /// <see cref="PanelMap"/>: all of it for the notebook and the save, its up and down for every
        /// other house shortcut). A binding to a whole control answers for its parts: the pan's left
        /// stick for each of its directions, the panels' D-pad for each of its four. An action's own
        /// binding, or the same part of its composite, is never a conflict with itself; and its own
        /// key or button as shipped is always its to take back - beside the bindings nobody rebinds
        /// that shared it as shipped (the board's Hit on the grip's Space), though never from another
        /// action a player has since given it.
        /// </summary>
        public static string Check(IEnumerable<Binding> live, Target target, string scheme, string path)
        {
            if (target == null) return "That cannot be rebound.";
            if (!target.Takes(scheme)) return target.Name + " is a key: press the new one on the keyboard.";
            if (SchemeOf(path) != scheme) return "That is not a " + (scheme == GamepadScheme ? "pad button" : "key") + ".";
            string words = InputGlossary.BindingLabel(path);
            string keeps = target.Name + " keeps its " + (scheme == GamepadScheme ? "button" : "key") + ".";
            bool shipped = IsShipped(target, scheme, path);
            // The house challenge's Hit has A back, though A is the panels' Submit: it shipped there.
            if (!shipped && IsReserved(path))
                return words + " closes panels and presses controls, so it stays as it is. " + keeps;
            if (IsBoardLetter(target, path)) return words + " spells on the word board, so it stays a letter. " + keeps;
            string context = ContextOf(target.Map);
            if (live != null)
                foreach (var binding in live)
                {
                    if (binding.Scheme != scheme || !Answers(binding.Path, path)) continue;
                    if (binding.Map == PanelMap)
                    {
                        if (shipped) continue;
                        // The panels' navigation: an action that works over a panel may share none of it,
                        if (target.OverPanels)
                            return words + " moves between a panel's controls, and " + target.Name + " works with a panel open. " + keeps;
                        // and no house shortcut its up or down, which walk the HUD's ring with no panel open.
                        if (context == "House" && target.Part == null && Vertical(binding, path))
                            return words + " moves between the controls on screen. " + keeps;
                        continue;
                    }
                    if (ContextOf(binding.Map) != context) continue;
                    if (binding.Map == target.Map && binding.Action == target.Action && (target.Part == null || binding.Part == target.Part)) continue;
                    var other = FindTarget(binding.Map, binding.Action, binding.Part) ?? FindTarget(binding.Map, binding.Action);
                    if (other != null) return words + " is already " + other.Name + "'s. " + keeps;
                    // Nobody rebinds it, so it answers to what it shipped with: shared as shipped, it stays shared.
                    if (shipped) continue;
                    // An action nobody rebinds - the camera's follow, a board's next control - by where it is.
                    return words + " already does something else " + (context == "House" ? "in the house" : "on a competition's board") + ". " + keeps;
                }
            return null;
        }

        /// <summary>
        /// Whether a navigation binding answers to <paramref name="chosen"/> as an up or a down - the
        /// way the HUD's rings walk: a composite's up or down part (W, the up arrow, the left stick's
        /// up), or the up or down of a whole control bound as one (the D-pad's).
        /// </summary>
        private static bool Vertical(Binding navigation, string chosen)
        {
            if (navigation.Part != null)
                return string.Equals(navigation.Part, "up", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(navigation.Part, "down", StringComparison.OrdinalIgnoreCase);
            string control = navigation.Path != null && navigation.Path.Length > chosen.Length ? navigation.Path : chosen;
            return control.EndsWith("/up", StringComparison.OrdinalIgnoreCase) || control.EndsWith("/down", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether a binding to <paramref name="bound"/> answers to <paramref name="chosen"/>: the same
        /// control, or one is a part of the other ("&lt;Gamepad&gt;/dpad" and "&lt;Gamepad&gt;/dpad/down").
        /// </summary>
        private static bool Answers(string bound, string chosen)
        {
            if (string.IsNullOrEmpty(bound) || string.IsNullOrEmpty(chosen)) return false;
            if (string.Equals(bound, chosen, StringComparison.OrdinalIgnoreCase)) return true;
            return chosen.Length > bound.Length && chosen[bound.Length] == '/' && chosen.StartsWith(bound, StringComparison.OrdinalIgnoreCase)
                || bound.Length > chosen.Length && bound[chosen.Length] == '/' && bound.StartsWith(chosen, StringComparison.OrdinalIgnoreCase);
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
        /// reason; what is left is still a set the game can play with. A target kept on its own path
        /// as shipped is as shipped, and no choice (<see cref="Set(Target, string, string)"/>). Never throws.
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
                string map = key.Substring(0, slash), named = key.Substring(slash + 1, at - slash - 1), scheme = key.Substring(at + 1);
                int hash = named.IndexOf('#');
                string action = hash < 0 ? named : named.Substring(0, hash), part = hash < 0 ? null : named.Substring(hash + 1);
                string path = (string)property.Value;
                var target = FindTarget(map, action, part);
                if (target != null && target.Takes(scheme) && IsShipped(target, scheme, path)) continue;
                if (target == null || !target.Takes(scheme) || SchemeOf(path) != scheme || IsReserved(path) || IsBoardLetter(target, path))
                { reason = "a kept control (" + key + ") is not one a player may choose"; continue; }
                bindings.overrides[Key(target, scheme)] = path;
            }
            return bindings;
        }

        /// <summary>
        /// Drops every choice the conflict check refuses against <paramref name="live"/>, and says
        /// which and why, or null when every choice stands. A kept text cannot see the event system's
        /// navigation (<see cref="PanelMap"/>), so a choice made before its rules - the notebook on
        /// the D-pad's down, the diary on it - still reads; the editor drops it as it loads, against
        /// the navigation alone, as every other rule was checked when the choice was made.
        /// </summary>
        public string DropRefused(IEnumerable<Binding> live)
        {
            var bindings = live != null ? new List<Binding>(live) : new List<Binding>();
            string reason = null;
            foreach (var target in Targets)
                foreach (var scheme in new[] { KeyboardScheme, GamepadScheme })
                {
                    string path = Get(target, scheme);
                    if (path == null) continue;
                    string refusal = Check(bindings, target, scheme, path);
                    if (refusal == null) continue;
                    overrides.Remove(Key(target, scheme));
                    string said = "a kept control (" + Key(target, scheme) + ") is not one a player may choose now: " + refusal.TrimEnd('.');
                    reason = reason == null ? said : reason + "; " + said;
                }
            return reason;
        }

        private static bool IsReserved(string path)
        {
            foreach (var reserved in ReservedPaths)
                if (string.Equals(reserved, path, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// Whether <paramref name="path"/> is a letter key given to a competition's action: the word
        /// board reads every letter as one it spells, so a board's key that was a letter would spell
        /// and act at once.
        /// </summary>
        private static bool IsBoardLetter(Target target, string path)
        {
            if (target == null || ContextOf(target.Map) != "Competition" || path == null) return false;
            const string prefix = "<Keyboard>/";
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || path.Length != prefix.Length + 1) return false;
            char key = char.ToLowerInvariant(path[prefix.Length]);
            return key >= 'a' && key <= 'z';
        }

        /// <summary>
        /// The choices as the player log's <c>keys=</c> says them, one word that reads back
        /// (<see cref="ParseSummary"/>): "default", or each choice in the settings' order, joined by
        /// commas - the action's name with its spaces as underscores, a colon, and the control's own
        /// name, the path after its device, with "(pad)" after a pad's: "Notebook:k,Diary:dpad/left(pad),
        /// Pan_up:i,Zoom_in:numpadPlus". A control's name never holds a space, an equals sign, a comma,
        /// a colon or a bracket, where the keys' printed labels do ("=", ",", "Left Shift").
        /// </summary>
        public string Summary()
        {
            if (overrides.Count == 0) return "default";
            var words = new List<string>();
            foreach (var target in Targets)
            {
                string key = Get(target, KeyboardScheme), pad = target.OnPad ? Get(target, GamepadScheme) : null;
                if (key != null) words.Add(LogName(target) + ":" + ControlName(key));
                if (pad != null) words.Add(LogName(target) + ":" + ControlName(pad) + PadMark);
            }
            return string.Join(",", words);
        }

        private const string PadMark = "(pad)";

        /// <summary>A target's name as one word: "Pan up" is "Pan_up".</summary>
        private static string LogName(Target target) => target.Name.Replace(' ', '_');

        /// <summary>The control a path names, after its device: "&lt;Gamepad&gt;/dpad/left" is "dpad/left".</summary>
        private static string ControlName(string path)
        {
            int slash = path.IndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1) : path;
        }

        /// <summary>
        /// The choices a log's <c>keys=</c> word says (<see cref="Summary"/>), on the same terms as a
        /// kept text (<see cref="Parse"/>): an entry that is not a choice a player could make is
        /// dropped, with the reason. Never throws.
        /// </summary>
        public static KeyBindings ParseSummary(string summary, out string reason)
        {
            reason = null;
            var bindings = new KeyBindings();
            if (string.IsNullOrWhiteSpace(summary) || summary == "default") return bindings;
            foreach (string entry in summary.Split(','))
            {
                bool pad = entry.EndsWith(PadMark, StringComparison.Ordinal);
                string said = pad ? entry.Substring(0, entry.Length - PadMark.Length) : entry;
                int colon = said.IndexOf(':');
                Target target = null;
                if (colon > 0)
                {
                    string name = said.Substring(0, colon).Replace('_', ' ');
                    foreach (var each in Targets) if (each.Name == name) { target = each; break; }
                }
                string scheme = pad ? GamepadScheme : KeyboardScheme;
                string path = colon > 0 ? (pad ? "<Gamepad>/" : "<Keyboard>/") + said.Substring(colon + 1) : null;
                if (target == null || colon + 1 >= said.Length || !target.Takes(scheme) || IsReserved(path) && !IsShipped(target, scheme, path) || IsBoardLetter(target, path))
                { reason = "a logged control (" + entry + ") is not one a player may choose"; continue; }
                bindings.Set(target, scheme, path);
            }
            return bindings;
        }

        // ---------------------------------------------------------------- the words on screen

        /// <summary>The choices the words on screen follow: set as the session's are loaded and changed.</summary>
        private static KeyBindings shown;

        /// <summary>Makes <paramref name="bindings"/> the choices the words on screen follow; null is every binding as shipped.</summary>
        public static void Show(KeyBindings bindings) => shown = bindings;

        /// <summary>Whether the words on screen follow any choice at all: false while every binding is as shipped.</summary>
        public static bool AnyChosen => shown != null && shown.overrides.Count > 0;

        /// <summary>A rebound action's key in the hints' words ("K"), or null while it is as shipped.</summary>
        public static string KeyLabel(string map, string action) => KeyLabel(FindTarget(map, action));

        /// <summary>A rebound target's key in the hints' words, or null while it is as shipped.</summary>
        public static string KeyLabel(Target target)
        {
            string path = target != null ? shown?.Get(target, KeyboardScheme) : null;
            return path == null ? null : InputGlossary.BindingLabel(path);
        }

        /// <summary>A rebound action's pad button in the hints' words ("Y"), or null while it is as shipped.</summary>
        public static string PadLabel(string map, string action)
        {
            var target = FindTarget(map, action);
            string path = target != null && target.OnPad ? shown?.Get(target, GamepadScheme) : null;
            return path == null ? null : InputGlossary.BindingLabel(path);
        }

        /// <summary>
        /// The words a competition's board says for one of its rebindable keys (the Competition map's
        /// Hold or Undo): the key or button the player chose, or <paramref name="shipped"/>, the board's
        /// own words as it was written - so with every binding as shipped the board reads as it did.
        /// A board's copy is not on the HUD's text sink; it asks here where it names those keys.
        /// </summary>
        public static string BoardKey(string action, bool pad, string shipped) =>
            (pad ? PadLabel("Competition", action) : KeyLabel("Competition", action)) ?? shipped;

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
        /// A phrase that names several keys at once - the help card's WASD, the camera settings' Q and
        /// C - as shipped, and how to say it with the keys as they are now.
        /// </summary>
        private readonly struct KeyPhrase
        {
            public readonly string Shipped;
            public readonly Target[] Keys;
            public readonly Func<string[], string> Say;
            public KeyPhrase(string shipped, Func<string[], string> say, params Target[] keys) { Shipped = shipped; Say = say; Keys = keys; }
        }

        /// <summary>Keys run together as WASD is, when each is one character; spaced when any is longer ("Num 8 A S D").</summary>
        private static string RunTogether(string[] keys)
        {
            foreach (var key in keys) if (key.Length != 1) return string.Join(" ", keys);
            return string.Concat(keys);
        }

        private static readonly KeyPhrase[] phrases =
        {
            // The help card: "WASD/arrows: pan", the pan's up, left, down and right in WASD's order.
            new KeyPhrase("WASD/arrows: pan", keys => RunTogether(keys) + "/arrows: pan",
                FindTarget("Camera", "Pan", "Up"), FindTarget("Camera", "Pan", "Left"), FindTarget("Camera", "Pan", "Down"), FindTarget("Camera", "Pan", "Right")),
            // The camera settings' paragraph: "the right stick and Q and C alike".
            new KeyPhrase("the right stick and Q and C alike", keys => "the right stick and " + keys[0] + " and " + keys[1] + " alike",
                FindTarget("Camera", "OrbitRate", "Left"), FindTarget("Camera", "OrbitRate", "Right")),
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
            foreach (var phrase in phrases)
            {
                bool any = false;
                var keys = new string[phrase.Keys.Length];
                for (int i = 0; i < keys.Length; i++)
                {
                    string label = KeyLabel(phrase.Keys[i]);
                    any |= label != null;
                    keys[i] = label ?? InputGlossary.BindingLabel(ShippedPath(phrase.Keys[i]));
                }
                if (!any) continue;
                string said = phrase.Say(keys);
                if (said != phrase.Shipped) changed.Add((phrase.Shipped, said));
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
                    if (at + from.Length > text.Length || string.CompareOrdinal(text, at, from, 0, from.Length) != 0) continue;
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

        /// <summary>
        /// The key a camera key's target answers to as shipped, for the keys of a phrase the player
        /// left alone; null for the rest. The editor's suite holds it to the actions asset
        /// (HouseCameraActions), part by part.
        /// </summary>
        public static string ShippedPath(Target target) =>
            target != null && target.Part != null ? ShippedPath(target, KeyboardScheme) : null;

        /// <summary>
        /// The key or button <paramref name="target"/> answers to on <paramref name="scheme"/> as
        /// shipped, or null where it takes none: the one a choice puts back, and the one it may always
        /// have again (<see cref="Check(IEnumerable{Binding}, Target, string, string)"/>). The editor's
        /// suite holds every one to the actions asset (HouseCameraActions).
        /// </summary>
        public static string ShippedPath(Target target, string scheme)
        {
            if (target == null || !target.Takes(scheme)) return null;
            bool pad = scheme == GamepadScheme;
            switch (target.Map + "/" + target.Action + "#" + target.Part)
            {
                case "Shortcuts/Notebook#": return pad ? "<Gamepad>/select" : "<Keyboard>/j";
                case "Shortcuts/Save#": return pad ? "<Gamepad>/leftStickPress" : "<Keyboard>/f5";
                case "Shortcuts/Diary#": return pad ? "<Gamepad>/buttonNorth" : "<Keyboard>/r";
                case "Shortcuts/Interact#": return pad ? "<Gamepad>/buttonWest" : "<Keyboard>/e";
                case "Shortcuts/Hit#": return pad ? "<Gamepad>/buttonSouth" : "<Keyboard>/space";
                case "Shortcuts/Overview#": return pad ? "<Gamepad>/dpad/up" : "<Keyboard>/m";
                case "Shortcuts/Emote#": return pad ? "<Gamepad>/dpad/right" : "<Keyboard>/g";
                case "Camera/Recenter#": return pad ? "<Gamepad>/rightStickPress" : "<Keyboard>/f";
                case "Camera/Pan#Up": return "<Keyboard>/w";
                case "Camera/Pan#Left": return "<Keyboard>/a";
                case "Camera/Pan#Down": return "<Keyboard>/s";
                case "Camera/Pan#Right": return "<Keyboard>/d";
                case "Camera/OrbitRate#Left": return "<Keyboard>/q";
                case "Camera/OrbitRate#Right": return "<Keyboard>/c";
                case "Camera/ZoomRate#Negative": return "<Keyboard>/minus";
                case "Camera/ZoomRate#Positive": return "<Keyboard>/equals";
                case "Competition/Hold#": return pad ? "<Gamepad>/rightTrigger" : "<Keyboard>/space";
                case "Competition/Undo#": return pad ? "<Gamepad>/buttonWest" : "<Keyboard>/backspace";
                default: return null;
            }
        }
    }
}
