using System.Collections.Generic;
using Gamesim.House;
using TMPro;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The room names over the house while the overview holds (VISUAL-TARGET.md V5): a glass chip
    /// above each room marker, turned to the camera every frame the way the name tags are. Built
    /// when the overview opens and destroyed when it ends, so nothing of it exists in an ordinary
    /// frame and nothing of it can drift into a capture that did not ask for it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomLabels : MonoBehaviour
    {
        public const string RootName = "Room labels";
        /// <summary>Metres above the floor the chips float: over the walls, under the camera.</summary>
        public const float Height = 3.4f;
        private const float ChipWidth = 330f, ChipHeight = 56f, WorldScale = 0.017f;

        private readonly List<RectTransform> chips = new List<RectTransform>();
        private Transform root;

        public static RoomLabels Attach(GameObject host)
        {
            var existing = host.GetComponent<RoomLabels>();
            return existing != null ? existing : host.AddComponent<RoomLabels>();
        }

        public bool IsShowing => root != null;
        public int Count => chips.Count;

        /// <summary>A room marker's name as the set paints it on the floor.</summary>
        public static string Title(string roomName)
        {
            switch (roomName)
            {
                case "Living": return "LIVING ROOM";
                case "Kitchen": return "KITCHEN";
                case "Bedroom": return "BEDROOM";
                case "Private": return "PRIVATE ROOM";
                case "Yard": return "COMPETITION YARD";
                case "HoH": return "HOH SUITE";
                case "Nomination": return "NOMINATION ROOM";
                case "Games": return "GAME ROOM";
                default: return string.IsNullOrEmpty(roomName) ? "" : roomName.ToUpperInvariant();
            }
        }

        public void Show(IEnumerable<HouseRoomMarker> markers, float scale)
        {
            Hide();
            root = new GameObject(RootName).transform;
            root.SetParent(transform, false);
            foreach (var marker in markers)
            {
                if (marker == null) continue;
                var canvas = new GameObject(marker.RoomName + " label", typeof(RectTransform), typeof(Canvas)).GetComponent<RectTransform>();
                canvas.SetParent(root, false);
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                canvas.sizeDelta = new Vector2(ChipWidth, ChipHeight);
                canvas.localScale = Vector3.one * (WorldScale * scale);
                canvas.position = marker.transform.position + Vector3.up * Height;

                var glass = HudPrimitives.Glass("Chip", canvas, 12);
                glass.anchorMin = Vector2.zero; glass.anchorMax = Vector2.one;
                glass.offsetMin = Vector2.zero; glass.offsetMax = Vector2.zero;
                var word = HudPrimitives.Heading("Word", glass, 22f, UiTheme.Accent, TextAlignmentOptions.Center);
                word.text = Title(marker.RoomName);
                word.rectTransform.anchorMin = Vector2.zero; word.rectTransform.anchorMax = Vector2.one;
                word.rectTransform.offsetMin = new Vector2(10f, 0f); word.rectTransform.offsetMax = new Vector2(-10f, 0f);
                chips.Add(canvas);
            }
            Face();
        }

        public void Hide()
        {
            chips.Clear();
            if (root == null) return;
            Destroy(root.gameObject);
            root = null;
        }

        private void LateUpdate() => Face();

        private void Face()
        {
            var camera = Camera.main;
            if (camera == null) return;
            foreach (var chip in chips)
                if (chip != null) chip.rotation = camera.transform.rotation;
        }
    }
}
