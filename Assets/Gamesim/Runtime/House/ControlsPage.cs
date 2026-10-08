using System.Collections.Generic;
using Gamesim.Presentation;

namespace Gamesim.House
{
    /// <summary>
    /// The settings' CONTROLS section (PLAN A, A4), rendered from the actions map: one line for
    /// every action of the episode's maps, in the map's order, saying what it does (the glossary's
    /// words) and what it is bound to now - the keys and the pad's buttons read off its bindings, so
    /// the page says what the map does rather than what someone remembered to write down. The
    /// prototype house's dialogue map is not the episode's and is left out.
    /// </summary>
    public static class ControlsPage
    {
        /// <summary>One action's line.</summary>
        public readonly struct Line
        {
            public readonly string Context, Map, Action, Keys, Pad, Hint;

            public Line(string context, string map, string action, string keys, string pad, string hint)
            {
                Context = context; Map = map; Action = action; Keys = keys; Pad = pad; Hint = hint;
            }

            /// <summary>The name its paragraph carries on the page, so a test finds it.</summary>
            public string Name => RowPrefix + Map + "/" + Action;

            /// <summary>What the page says: "Open the notebook:  J   ·   Pad Select".</summary>
            public string Text => Words(Hint, Keys, Pad);
        }

        /// <summary>The prefix of every line's paragraph name.</summary>
        public const string RowPrefix = "Control row ";

        /// <summary>Every action of the episode's maps, as the page lists them.</summary>
        public static List<Line> Lines(HouseCameraActions actions)
        {
            var lines = new List<Line>();
            if (actions == null || actions.Asset == null) return lines;
            foreach (var map in actions.Asset.actionMaps)
            {
                if (map.name == HouseCameraActions.DialogueMapName) continue;
                foreach (var action in map.actions)
                {
                    var row = InputGlossary.Find(map.name, action.name);
                    lines.Add(new Line(row != null ? row.Context : map.name, map.name, action.name,
                        HouseCameraActions.BindingWords(action, HouseCameraActions.KeyboardMouseScheme),
                        HouseCameraActions.BindingWords(action, HouseCameraActions.GamepadScheme),
                        row != null ? row.Hint : action.name));
                }
            }
            return lines;
        }

        /// <summary>A line's words: what it does, then the keys, then the pad's buttons, each only when there are some.</summary>
        public static string Words(string hint, string keys, string pad)
        {
            string ways = string.IsNullOrEmpty(keys) ? "" : keys;
            if (!string.IsNullOrEmpty(pad)) ways += (ways.Length > 0 ? "   ·   " : "") + "Pad " + pad;
            return hint + ":  " + ways;
        }
    }
}
