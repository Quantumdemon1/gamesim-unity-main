using System;
using System.Collections.Generic;
using System.Text;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Every thing the player presses, as one table (PLAN A, A0): where it works, the keyboard's way,
    /// the pad's way, and what it does. Unity-free on purpose, so the subset checks the table itself
    /// and the editor checks it against the actions map (<c>HouseCameraActions</c>): a row whose map
    /// is one of that asset's names an action there, and every action there has exactly one row.
    ///
    /// <para>Three kinds of row stand outside the asset, each named as a map of its own: the event
    /// system's module (<see cref="InterfaceMap"/>, the package's default UI actions), the pointer
    /// (<see cref="PointerMap"/>, clicks on the house itself) and typing (<see cref="TypingMap"/>, a
    /// text field or the word game's letters). Those last two are the only direct device reads the
    /// raw-read scan allows.</para>
    ///
    /// <para>The key and pad columns are written the way <see cref="BindingLabel"/> words a binding,
    /// so the editor test can hold each row to the map it describes: a binding that moves and a row
    /// that does not fails there, not in front of a player.</para>
    /// </summary>
    public static class InputGlossary
    {
        public const string InterfaceMap = "UI";
        public const string PointerMap = "Pointer";
        public const string TypingMap = "Typing";

        public const string CameraContext = "Camera";
        public const string HouseContext = "In the house";
        public const string PanelsContext = "Panels and menus";
        public const string CeremonyContext = "Ceremony cards";
        public const string CompetitionContext = "Competitions";
        public const string ReportContext = "Season report";
        public const string PrototypeContext = "Prototype house";

        /// <summary>One thing the player presses.</summary>
        public sealed class Row
        {
            public string Context { get; }
            public string Map { get; }
            public string Action { get; }
            /// <summary>The keyboard and mouse way, as <see cref="BindingLabel"/> words it; empty when there is none.</summary>
            public string Keyboard { get; }
            /// <summary>The pad's way; empty when there is none.</summary>
            public string Pad { get; }
            /// <summary>What it does, in the words the controls page shows.</summary>
            public string Hint { get; }

            public Row(string context, string map, string action, string keyboard, string pad, string hint)
            {
                Context = context; Map = map; Action = action;
                Keyboard = keyboard ?? ""; Pad = pad ?? ""; Hint = hint;
            }

            /// <summary>Whether the row stands outside the actions asset: the UI module, the pointer or typing.</summary>
            public bool OutsideTheAsset => Map == InterfaceMap || Map == PointerMap || Map == TypingMap;

            public override string ToString() => Map + "/" + Action;
        }

        private static readonly Row[] rows =
        {
            // The camera map.
            new Row(CameraContext, "Camera", "Orbit", "Right-drag", "", "Turn the camera around the house"),
            new Row(CameraContext, "Camera", "OrbitRate", "", "Right stick", "Turn the camera"),
            new Row(CameraContext, "Camera", "Pan", "WASD / Arrows", "Left stick", "Move the camera"),
            new Row(CameraContext, "Camera", "Drag", "Middle-drag", "", "Drag the ground under the cursor"),
            new Row(CameraContext, "Camera", "Zoom", "Wheel", "", "Zoom in and out"),
            new Row(CameraContext, "Camera", "ZoomRate", "- / =", "LT / RT", "Zoom in and out, held"),
            new Row(CameraContext, "Camera", "Recenter", "F", "R3", "Back to your houseguest"),
            new Row(CameraContext, "Camera", "Point", "Pointer", "", "Zoom toward the cursor, and pan at the screen's edges"),
            new Row(CameraContext, "Camera", "Next", "] / Tab", "RB", "Follow the next houseguest (Tab with nothing focused)"),
            new Row(CameraContext, "Camera", "Previous", "[ / Shift+Tab", "LB", "Follow the previous houseguest"),

            // The house's shortcuts.
            new Row(HouseContext, "Shortcuts", "Menu", "Esc", "Start", "Close the top panel; with nothing open, the pause menu"),
            new Row(HouseContext, "Shortcuts", "Notebook", "J", "Select", "Open the notebook"),
            new Row(HouseContext, "Shortcuts", "Save", "F5", "L3", "Save now"),
            new Row(HouseContext, "Shortcuts", "Diary", "R", "Y", "Walk to the diary room"),
            new Row(HouseContext, "Shortcuts", "Interact", "E", "X", "Talk, enter the diary room, or open the episode screen"),
            new Row(HouseContext, "Shortcuts", "Hit", "Space", "A", "Stop the marker in a house challenge"),
            new Row(HouseContext, "Shortcuts", "Overview", "M", "D-pad up", "The whole house from above, and back"),
            new Row(HouseContext, "Shortcuts", "Emote", "G", "D-pad right", "Your moves: cheer, dance, shrug"),

            // The ceremony cards, and the opening's Space.
            new Row(CeremonyContext, "Ceremony", "Skip", "Enter / Num Enter / Esc / Left click", "A / B", "Skip a reveal to its result; again to close the card"),
            new Row(CeremonyContext, "Ceremony", "Speed", "Space", "X", "Speed a reveal up, or back to its own pace"),
            new Row(CeremonyContext, "Ceremony", "Advance", "Space", "", "Move the opening on"),

            // A competition's board and its results.
            new Row(CompetitionContext, "Competition", "Up", "Up / W", "D-pad up / Left stick up", "Answer up"),
            new Row(CompetitionContext, "Competition", "Right", "Right / D", "D-pad right / Left stick right", "Answer right"),
            new Row(CompetitionContext, "Competition", "Down", "Down / S", "D-pad down / Left stick down", "Answer down"),
            new Row(CompetitionContext, "Competition", "Left", "Left / A", "D-pad left / Left stick left", "Answer left"),
            new Row(CompetitionContext, "Competition", "Hit", "Space", "", "Hit the target in the classic reaction game"),
            new Row(CompetitionContext, "Competition", "Hold", "Space", "RT", "Hold on in the endurance game"),
            new Row(CompetitionContext, "Competition", "Pause", "P", "Start", "Pause and resume"),
            new Row(CompetitionContext, "Competition", "Back", "Esc", "B", "Leave the board; in a ranked attempt the first press asks"),
            new Row(CompetitionContext, "Competition", "Undo", "Backspace", "X", "Take a letter back in the word game"),
            new Row(CompetitionContext, "Competition", "FocusNext", "Tab", "RB", "The board's next control"),
            new Row(CompetitionContext, "Competition", "FocusPrevious", "Shift+Tab", "LB", "The board's previous control"),
            new Row(CompetitionContext, "Competition", "Confirm", "Enter", "A", "Continue from the results"),

            // The season report.
            new Row(ReportContext, "Report", "PageUp", "Page Up", "LB", "Read back a page"),
            new Row(ReportContext, "Report", "PageDown", "Page Down", "RB", "Read on a page"),
            new Row(ReportContext, "Report", "Home", "Home", "LT", "The top of the season"),
            new Row(ReportContext, "Report", "End", "End", "RT", "The foot of the season"),
            new Row(ReportContext, "Report", "Scroll", "", "Right stick", "Scroll the season"),
            new Row(ReportContext, "Report", "Walk", "", "D-pad / Left stick", "Walk the report's controls; the stick stops scrolling while you do"),

            // The prototype house's conversation.
            new Row(PrototypeContext, "Dialogue", "Reply1", "1 / Num 1", "", "Choose the first reply"),
            new Row(PrototypeContext, "Dialogue", "Reply2", "2 / Num 2", "", "Choose the second reply"),
            new Row(PrototypeContext, "Dialogue", "Reply3", "3 / Num 3", "", "Choose the third reply"),

            // The event system's module: the package's default UI actions.
            new Row(PanelsContext, InterfaceMap, "Navigate", "Up / Down", "D-pad / Left stick", "Move between a panel's controls"),
            new Row(PanelsContext, InterfaceMap, "Submit", "Enter", "A", "Press the focused control"),
            new Row(PanelsContext, InterfaceMap, "Click", "Left click", "", "Press a control"),
            new Row(PanelsContext, InterfaceMap, "ScrollWheel", "Wheel", "", "Scroll a panel"),

            // The pointer on the house itself, and typing: the two direct reads the scan allows.
            new Row(HouseContext, PointerMap, "Walk", "Left click", "", "Walk to the floor clicked"),
            new Row(HouseContext, PointerMap, "Beacon", "Left click", "", "Travel to the room a beacon marks"),
            new Row(PanelsContext, TypingMap, "Fields", "Keyboard", "", "Type a name, a speech, a search or a file path"),
            new Row(CompetitionContext, TypingMap, "Letters", "A-Z", "", "Spell in the word game"),
        };

        /// <summary>Every row, in the order the controls page shows them.</summary>
        public static IReadOnlyList<Row> Rows => rows;

        /// <summary>The row for an action, or null.</summary>
        public static Row Find(string map, string action)
        {
            foreach (var row in rows)
                if (row.Map == map && row.Action == action) return row;
            return null;
        }

        // ---------------------------------------------------------------- binding words

        /// <summary>
        /// One binding path in the words the controls page and the hints use: "&lt;Keyboard&gt;/escape"
        /// is "Esc", "&lt;Gamepad&gt;/buttonEast" is "B". A path it does not know comes back as its
        /// control's own name, so a new binding reads as something rather than nothing.
        /// </summary>
        public static string BindingLabel(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            int slash = path.IndexOf('/');
            string device = slash > 0 ? path.Substring(0, slash) : "";
            string control = slash >= 0 ? path.Substring(slash + 1) : path;
            switch (device)
            {
                case "<Keyboard>": return KeyLabel(control);
                case "<Gamepad>": return PadLabel(control);
                case "<Mouse>":
                    switch (control)
                    {
                        case "leftButton": return "Left click";
                        case "rightButton": return "Right button";
                        case "middleButton": return "Middle button";
                        case "scroll/y": case "scroll": return "Wheel";
                        case "delta": return "Move";
                    }
                    break;
                case "<Pointer>":
                    if (control == "position") return "Pointer";
                    break;
            }
            return control;
        }

        private static string KeyLabel(string key)
        {
            switch (key)
            {
                case "escape": return "Esc";
                case "enter": return "Enter";
                case "numpadEnter": return "Num Enter";
                case "space": return "Space";
                case "tab": return "Tab";
                case "backspace": return "Backspace";
                case "shift": return "Shift";
                case "pageUp": return "Page Up";
                case "pageDown": return "Page Down";
                case "home": return "Home";
                case "end": return "End";
                case "upArrow": return "Up";
                case "downArrow": return "Down";
                case "leftArrow": return "Left";
                case "rightArrow": return "Right";
                case "leftBracket": return "[";
                case "rightBracket": return "]";
                case "minus": return "-";
                case "equals": return "=";
            }
            if (key.StartsWith("numpad", StringComparison.Ordinal) && key.Length == 7 && char.IsDigit(key[6])) return "Num " + key[6];
            if (key.Length == 1) return key.ToUpperInvariant();
            if (key.Length <= 3 && key[0] == 'f' && char.IsDigit(key[1])) return key.ToUpperInvariant();
            return key;
        }

        private static string PadLabel(string control)
        {
            switch (control)
            {
                case "buttonSouth": return "A";
                case "buttonEast": return "B";
                case "buttonWest": return "X";
                case "buttonNorth": return "Y";
                case "start": return "Start";
                case "select": return "Select";
                case "leftShoulder": return "LB";
                case "rightShoulder": return "RB";
                case "leftTrigger": return "LT";
                case "rightTrigger": return "RT";
                case "leftStick": return "Left stick";
                case "rightStick": return "Right stick";
                case "leftStickPress": return "L3";
                case "rightStickPress": return "R3";
                case "dpad": return "D-pad";
                case "dpad/up": return "D-pad up";
                case "dpad/down": return "D-pad down";
                case "dpad/left": return "D-pad left";
                case "dpad/right": return "D-pad right";
                case "leftStick/up": return "Left stick up";
                case "leftStick/down": return "Left stick down";
                case "leftStick/left": return "Left stick left";
                case "leftStick/right": return "Left stick right";
            }
            return control;
        }

        /// <summary>
        /// A composite binding in one phrase: WASD, the arrows, a modifier and its key ("Shift+Tab"),
        /// a mouse drag ("Right-drag"), or the two ends of an axis ("- / =").
        /// </summary>
        public static string CompositeLabel(string composite, IList<KeyValuePair<string, string>> parts)
        {
            string Part(string name)
            {
                foreach (var part in parts) if (string.Equals(part.Key, name, StringComparison.OrdinalIgnoreCase)) return part.Value;
                return null;
            }
            switch (composite)
            {
                case "2DVector":
                {
                    string up = Part("Up"), down = Part("Down"), left = Part("Left"), right = Part("Right");
                    if (up == "<Keyboard>/w" && down == "<Keyboard>/s" && left == "<Keyboard>/a" && right == "<Keyboard>/d") return "WASD";
                    if (up == "<Keyboard>/upArrow" && down == "<Keyboard>/downArrow" && left == "<Keyboard>/leftArrow" && right == "<Keyboard>/rightArrow")
                        return "Arrows";
                    return Join(new[] { BindingLabel(up), BindingLabel(down), BindingLabel(left), BindingLabel(right) }, "/");
                }
                case "OneModifier":
                case "ButtonWithOneModifier":
                {
                    string modifier = Part("Modifier") ?? Part("Modifier1"), binding = Part("Binding") ?? Part("Button");
                    if (binding == "<Mouse>/delta")
                    {
                        if (modifier == "<Mouse>/rightButton") return "Right-drag";
                        if (modifier == "<Mouse>/middleButton") return "Middle-drag";
                        if (modifier == "<Mouse>/leftButton") return "Left-drag";
                    }
                    return BindingLabel(modifier) + "+" + BindingLabel(binding);
                }
                case "1DAxis":
                    return BindingLabel(Part("Negative")) + " / " + BindingLabel(Part("Positive"));
            }
            var words = new List<string>();
            foreach (var part in parts) words.Add(BindingLabel(part.Value));
            return Join(words, "+");
        }

        /// <summary>Distinct, non-empty words joined: an action bound twice to one key says it once.</summary>
        public static string Join(IEnumerable<string> words, string separator = " / ")
        {
            var seen = new List<string>();
            foreach (var word in words)
                if (!string.IsNullOrEmpty(word) && !seen.Contains(word)) seen.Add(word);
            var builder = new StringBuilder();
            for (var index = 0; index < seen.Count; index++)
            {
                if (index > 0) builder.Append(separator);
                builder.Append(seen[index]);
            }
            return builder.ToString();
        }
    }
}
