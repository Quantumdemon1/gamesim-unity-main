using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The free "Just talk" tile's locked state (free talk slice 1): a tile of a grid drawn this render, found by its caption,
    /// that the player can see and cannot press - the talk for this window is had, or none is on offer - with the reason as
    /// its line. The caption stays exactly as the open tile's, so a test and a screen reader find it by the same words.
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>
        /// Makes the tile <paramref name="caption"/> of this render's grid <paramref name="gridName"/> unpressable, and returns it
        /// (null when there is none). The last grid and the last tile of the name are this render's: a same-frame find by name
        /// also meets the old chrome, whose Destroy is deferred.
        /// </summary>
        public Button LockTile(string gridName, string caption)
        {
            if (content == null || string.IsNullOrEmpty(gridName) || string.IsNullOrEmpty(caption)) return null;
            var grid = content.GetComponentsInChildren<RectTransform>(true).LastOrDefault(rect => rect.name == gridName);
            if (grid == null) return null;
            var tile = grid.GetComponentsInChildren<Button>(true).LastOrDefault(button => button.name == caption);
            if (tile == null) return null;
            tile.interactable = false;
            // The edge stops lifting under the pointer; the words stay readable and the line says why.
            var colours = tile.colors;
            colours.highlightedColor = colours.selectedColor = colours.normalColor;
            tile.colors = colours;
            return tile;
        }
    }
}
