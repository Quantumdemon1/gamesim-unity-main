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

        private const float CardHeight = 74f;
        private const float FaceSize = 32f;

        /// <summary>One person standing in a room.</summary>
        public readonly struct Occupant
        {
            public readonly string Name;
            public readonly Texture Portrait;
            public readonly bool IsPlayer;

            public Occupant(string name, Texture portrait, bool isPlayer)
            {
                Name = name; Portrait = portrait; IsPlayer = isPlayer;
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

            // The player's room is outlined rather than filled, so the eye finds it without the card
            // competing with the portraits inside it.
            var card = HudPrimitives.Fill("Room card", root,
                hasPlayer ? new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, .14f) : UiTheme.Surface,
                UiTheme.ControlRadius);
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(1f, 1f);
            card.pivot = new Vector2(.5f, 1f);
            card.offsetMin = new Vector2(2f, 0f);
            card.offsetMax = new Vector2(-2f, 0f);
            card.anchoredPosition = new Vector2(0f, -index * CardHeight * scale);
            card.sizeDelta = new Vector2(-4f, (CardHeight - 6f) * scale);

            var name = Label(card, room.Name, 15, hasPlayer ? UiTheme.Accent : UiTheme.Paper, scale, font);
            name.rectTransform.anchorMin = new Vector2(0f, 1f);
            name.rectTransform.anchorMax = new Vector2(0f, 1f);
            name.rectTransform.pivot = new Vector2(0f, 1f);
            name.rectTransform.anchoredPosition = new Vector2(12f * scale, -6f * scale);
            name.rectTransform.sizeDelta = new Vector2(240f * scale, 20f * scale);

            // An empty room says so. A card with a name and nothing under it reads as a rendering
            // failure rather than as an empty kitchen.
            if (room.Occupants.Count == 0)
            {
                var empty = Label(card, "empty", 13, UiTheme.Muted, scale, font);
                empty.rectTransform.anchorMin = new Vector2(0f, 1f);
                empty.rectTransform.anchorMax = new Vector2(0f, 1f);
                empty.rectTransform.pivot = new Vector2(0f, 1f);
                empty.rectTransform.anchoredPosition = new Vector2(12f * scale, -30f * scale);
                empty.rectTransform.sizeDelta = new Vector2(200f * scale, 18f * scale);
                return;
            }

            var tally = Label(card, room.Occupants.Count.ToString(), 13, UiTheme.Muted, scale, font);
            tally.rectTransform.anchorMin = new Vector2(1f, 1f);
            tally.rectTransform.anchorMax = new Vector2(1f, 1f);
            tally.rectTransform.pivot = new Vector2(1f, 1f);
            tally.rectTransform.anchoredPosition = new Vector2(-12f * scale, -6f * scale);
            tally.rectTransform.sizeDelta = new Vector2(40f * scale, 18f * scale);
            tally.alignment = TextAlignmentOptions.TopRight;

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
                rim.anchoredPosition = new Vector2(x, -28f * scale);
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
