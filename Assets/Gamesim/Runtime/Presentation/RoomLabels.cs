using System.Collections.Generic;
using Gamesim.House;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        private const float ChipHeight = 56f, WorldScale = 0.017f, MarkSide = 30f;

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

        /// <summary>The glyph a room's chip leads with, as mockup-03 marks each room.</summary>
        public static string Glyph(string roomName)
        {
            switch (roomName)
            {
                case "Living": return "house";
                case "Kitchen": return "fork";
                case "Bedroom": return "bed";
                case "Private": return "chat";
                case "Yard": return "trophy";
                case "HoH": return "crown";
                case "Nomination": return "gavel";
                case "Games": return "target";
                default: return "house";
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
                canvas.localScale = Vector3.one * (WorldScale * scale);
                canvas.position = marker.transform.position + Vector3.up * Height;

                // Mockup-03's chip: dark glass with a hairline, the room's glyph, and its name in
                // white, as wide as the name - a fixed 330 left GYM floating in a bar of glass.
                var chip = HudPrimitives.Fill("Chip", canvas, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .9f), 12);
                chip.anchorMin = Vector2.zero; chip.anchorMax = Vector2.one;
                chip.offsetMin = Vector2.zero; chip.offsetMax = Vector2.zero;
                UiTheme.AddBorder(chip, 12, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .6f));
                var word = HudPrimitives.Heading("Word", chip, 22f, UiTheme.Paper, TextAlignmentOptions.MidlineLeft);
                word.text = Title(marker.RoomName);
                word.characterSpacing = 2f;
                word.textWrappingMode = TextWrappingModes.NoWrap;
                float words = Mathf.Ceil(word.GetPreferredValues(word.text).x) + 4f;
                var sprite = UiTheme.Icon(Glyph(marker.RoomName));
                float lead = sprite != null ? 18f + MarkSide + 12f : 22f;
                canvas.sizeDelta = new Vector2(lead + words + 22f, ChipHeight);
                word.rectTransform.anchorMin = Vector2.zero; word.rectTransform.anchorMax = Vector2.one;
                word.rectTransform.offsetMin = new Vector2(lead, 0f); word.rectTransform.offsetMax = new Vector2(-18f, 0f);
                if (sprite != null)
                {
                    var mark = new GameObject("Mark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    mark.rectTransform.SetParent(chip, false);
                    mark.rectTransform.anchorMin = mark.rectTransform.anchorMax = new Vector2(0f, .5f);
                    mark.rectTransform.pivot = new Vector2(0f, .5f);
                    mark.rectTransform.sizeDelta = new Vector2(MarkSide, MarkSide);
                    mark.rectTransform.anchoredPosition = new Vector2(18f, 0f);
                    mark.sprite = sprite; mark.preserveAspect = true; mark.raycastTarget = false;
                    // Gold is power: the Head of Household's suite is the one room that wears it.
                    mark.color = marker.RoomName == "HoH" ? UiTheme.Gold : UiTheme.Heading;
                }
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
