using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Who is in which room, as a card per room.
    ///
    /// <para>The 3D house is the real spatial view and this does not replace it — but a houseguest
    /// occupies 1.4% of frame height at the shipped camera distance, which is a measured figure, not
    /// an impression. At that size the set shows where people are and not who they are, so "is Maya
    /// alone in the kitchen right now" is a question the 3D view technically answers and practically
    /// does not. This answers it in one glance, with faces.</para>
    ///
    /// <para>It reports the live scene rather than the simulation: the rooms are the same markers the
    /// player walks to, and occupancy is nearest-marker. Nothing here is authoritative over anything;
    /// it is a reading of where the bodies are.</para>
    /// </summary>
    public static class HouseMap
    {
        public const string RootName = "House map";

        /// <summary>The pitch between cards: the card, plus room for its glow to clear the next.</summary>
        private const float CardHeight = 86f;
        private const float CardGap = 12f;
        private const float FaceSize = 32f;

        /// <summary>
        /// The glyph a room's chip carries, the way mockup-03 labels every room with one. Matched
        /// on the marker's name, so a house with new rooms gets the house mark rather than nothing.
        /// </summary>
        public static string RoomIcon(string roomName)
        {
            string name = (roomName ?? string.Empty).ToLowerInvariant();
            if (name.Contains("hoh")) return "crown";
            if (name.Contains("bed")) return "bed";
            if (name.Contains("kitchen") || name.Contains("dining")) return "fork";
            if (name.Contains("gym")) return "dumbbell";
            if (name.Contains("diary")) return "chat";
            if (name.Contains("nomination")) return "target";
            if (name.Contains("living") || name.Contains("lounge")) return "people";
            if (name.Contains("yard") || name.Contains("pool")) return "star";
            return "house";
        }

        /// <summary>One person standing in a room.</summary>
        public readonly struct Occupant
        {
            /// <summary>
            /// Who this is, in the simulation's terms.
            ///
            /// <para>The map itself only ever draws the name. The id is here because the proximity
            /// event source needs to say which two houseguests were in a room together, and this is
            /// already the one place that knows — computing it a second time would be two answers to
            /// one question, and the second is always the one that drifts.</para>
            /// </summary>
            public readonly string Id;
            public readonly string Name;
            public readonly Texture Portrait;
            public readonly bool IsPlayer;

            public Occupant(string id, string name, Texture portrait, bool isPlayer)
            {
                Id = id; Name = name; Portrait = portrait; IsPlayer = isPlayer;
            }
        }

        /// <summary>A room and who is currently in it.</summary>
        public readonly struct Room
        {
            public readonly string Name;
            public readonly IList<Occupant> Occupants;

            public Room(string name, IList<Occupant> occupants)
            {
                Name = name; Occupants = occupants;
            }
        }

        public static RectTransform Build(
            Transform parent, IList<Room> rooms, float scale, TMP_FontAsset font)
        {
            var root = new GameObject(RootName, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);

            int count = rooms == null ? 0 : rooms.Count;
            float height = count * CardHeight * scale;
            var element = root.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            if (count == 0) return root;

            for (int i = 0; i < count; i++) Card(root, rooms[i], i, scale, font);
            return root;
        }

        private static void Card(RectTransform root, Room room, int index, float scale, TMP_FontAsset font)
        {
            bool hasPlayer = false;
            for (int i = 0; i < room.Occupants.Count; i++) if (room.Occupants[i].IsPlayer) hasPlayer = true;

            // One of the mockups' glass cards a room. The player's room is outlined in the accent
            // on top of the hairline, so the eye finds it without the card competing with the
            // portraits inside it.
            var card = HudPrimitives.Glass("Room card", root);
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(1f, 1f);
            card.pivot = new Vector2(.5f, 1f);
            card.offsetMin = new Vector2(2f, 0f);
            card.offsetMax = new Vector2(-2f, 0f);
            card.anchoredPosition = new Vector2(0f, -index * CardHeight * scale);
            card.sizeDelta = new Vector2(-4f, (CardHeight - CardGap) * scale);
            if (hasPlayer) UiTheme.AddBorder(card, UiTheme.GlassRadius, new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, .85f));

            // The room's chip: a glyph and the name in the mockups' tracked capitals.
            float textLeft = 12f * scale;
            var glyph = UiTheme.Icon(RoomIcon(room.Name));
            if (glyph != null)
            {
                var art = new GameObject("Room glyph", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                art.SetParent(card, false);
                art.anchorMin = new Vector2(0f, 1f);
                art.anchorMax = new Vector2(0f, 1f);
                art.pivot = new Vector2(0f, 1f);
                art.anchoredPosition = new Vector2(12f * scale, -8f * scale);
                art.sizeDelta = new Vector2(15f * scale, 15f * scale);
                var image = art.GetComponent<Image>();
                image.sprite = glyph;
                image.color = hasPlayer ? UiTheme.Accent : UiTheme.Glow;
                image.raycastTarget = false;
                image.preserveAspect = true;
                textLeft += 22f * scale;
            }

            var name = HudPrimitives.Heading("Text", card, Mathf.RoundToInt(13f * scale),
                hasPlayer ? UiTheme.Accent : UiTheme.Paper);
            if (name.font == null && font != null) name.font = font;
            name.text = Localisation.Text(room.Name).ToUpperInvariant();
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.enableAutoSizing = true;
            name.fontSizeMax = name.fontSize;
            name.fontSizeMin = Mathf.Min(9f * scale, name.fontSize);
            name.rectTransform.anchorMin = new Vector2(0f, 1f);
            name.rectTransform.anchorMax = new Vector2(0f, 1f);
            name.rectTransform.pivot = new Vector2(0f, 1f);
            name.rectTransform.anchoredPosition = new Vector2(textLeft, -6f * scale);
            name.rectTransform.sizeDelta = new Vector2(260f * scale, 20f * scale);

            // An empty room says so. A card with a name and nothing under it reads as a rendering
            // failure rather than as an empty kitchen.
            if (room.Occupants.Count == 0)
            {
                var empty = Label(card, "empty", 13, UiTheme.Muted, scale, font);
                empty.rectTransform.anchorMin = new Vector2(0f, 1f);
                empty.rectTransform.anchorMax = new Vector2(0f, 1f);
                empty.rectTransform.pivot = new Vector2(0f, 1f);
                empty.rectTransform.anchoredPosition = new Vector2(12f * scale, -32f * scale);
                empty.rectTransform.sizeDelta = new Vector2(200f * scale, 20f * scale);
                return;
            }

            // The head count as a pill, the way the mockups' cards carry their counts.
            var tally = HudPrimitives.Chip("Tally", card, room.Occupants.Count.ToString(),
                hasPlayer ? UiTheme.Accent : UiTheme.Glow, 36f * scale, 20f * scale);
            tally.anchorMin = new Vector2(1f, 1f);
            tally.anchorMax = new Vector2(1f, 1f);
            tally.pivot = new Vector2(1f, 1f);
            tally.anchoredPosition = new Vector2(-10f * scale, -6f * scale);
            var tallyLabel = tally.GetComponentInChildren<TMP_Text>();
            if (tallyLabel != null) tallyLabel.textWrappingMode = TextWrappingModes.NoWrap;

            float x = 12f * scale;
            float step = (FaceSize + 8f) * scale;
            for (int i = 0; i < room.Occupants.Count; i++)
            {
                var occupant = room.Occupants[i];
                var rim = HudPrimitives.Portrait(card, occupant.Portrait,
                    occupant.IsPlayer ? UiTheme.Accent : UiTheme.Outline, FaceSize * scale, 2f * scale, false);
                rim.anchorMin = new Vector2(0f, 1f);
                rim.anchorMax = new Vector2(0f, 1f);
                rim.pivot = new Vector2(0f, 1f);
                rim.anchoredPosition = new Vector2(x, -32f * scale);
                x += step;
            }
        }

        private static TMP_Text Label(
            Transform parent, string value, int size, Color colour, float scale, TMP_FontAsset font)
        {
            var label = HudPrimitives.Label("Text", parent, Mathf.RoundToInt(size * scale), colour);
            if (font != null) label.font = font;
            label.text = value;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }
    }
}
