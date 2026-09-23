using Gamesim.Simulation;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Who is in which room: the notebook's room directory.
    ///
    /// <para>The 3D house is the real spatial view and this does not replace it — but a houseguest
    /// occupies 1.4% of frame height at the shipped camera distance, which is a measured figure, not
    /// an impression. At that size the set shows where people are and not who they are, so "is Maya
    /// alone in the kitchen right now" is a question the 3D view technically answers and practically
    /// does not. This answers it in one glance, with faces and names.</para>
    ///
    /// <para>Drawn as Refinement Kit 6 draws it (Previews/02_who_is_where.png): room cards in two
    /// columns, each a glyph, a name and a count over the people in it as face-and-name tokens. The
    /// people are the content, not the frame: the cards rest quiet, with no glow - every row used to
    /// glow, the empty ones too - and an empty room is a short card that says so.</para>
    ///
    /// <para>It reports the live scene rather than the simulation: the rooms are the markers the
    /// player walks to, and occupancy is nearest-marker. The house shows everyone - the overview,
    /// the follow camera and the live feed all do - so "Empty" is true of an empty room here, and
    /// nothing is "unknown". The one exception is a houseguest the house has no body for, which is
    /// said as it is: their location is unavailable, with no guess and no "last seen".</para>
    /// </summary>
    public static class HouseMap
    {
        public const string RootName = "House map";
        /// <summary>A room's card is named for its room, so a test can find the kitchen's.</summary>
        public const string CardPrefix = "Room card · ";
        /// <summary>The card for houseguests the house has no body to place.</summary>
        public const string UnplacedName = "Location unavailable card";

        /// <summary>Which rooms the directory shows. View state only: nothing about it is saved.</summary>
        public enum Filter { All, Occupied, Unplaced }

        // Refinement Kit 6's room directory at the 1600-wide canvas: cards in two columns with a
        // 20-unit gutter, a head of glyph, name and count, a line under it, then the people in the
        // room as face-and-name tokens that wrap. Sized to what they hold, so an empty kitchen is a
        // short card and a room holding the whole house grows rather than shrinking its faces.
        private const float Gap = 20f;
        private const float Pad = 18f;
        private const float Face = 40f;
        private const float TokenRow = 54f;
        private const float TwoColumns = 760f;

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
            public readonly ContestantState Character;
            public readonly bool IsPlayer;

            public Occupant(string id, string name, Texture portrait, bool isPlayer, ContestantState character = null)
            {
                Id = id; Name = name; Portrait = portrait; Character = character?.Clone(); IsPlayer = isPlayer;
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

        /// <summary>
        /// The line over the directory: how many are in the house, how many are placed, and - only
        /// when there are any - how many the house could not place. It must agree with the top bar's
        /// house count, so it is counted from the same rooms it draws.
        /// </summary>
        public static string Summary(IList<Room> rooms, IList<Occupant> unplaced)
        {
            int placed = rooms == null ? 0 : rooms.Sum(room => room.Occupants?.Count ?? 0);
            int missing = unplaced?.Count ?? 0;
            string line = (placed + missing) + " in the house · " + placed + " placed";
            return missing > 0 ? line + " · " + missing + " location unavailable" : line;
        }

        public static RectTransform Build(Transform parent, IList<Room> rooms, float scale, TMP_FontAsset font)
            => Build(parent, rooms, null, Filter.All, scale, font, 1200f * scale);

        public static RectTransform Build(Transform parent, IList<Room> rooms, IList<Occupant> unplaced, Filter filter,
            float scale, TMP_FontAsset font, float width)
        {
            var root = new GameObject(RootName, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            var element = root.gameObject.AddComponent<LayoutElement>();
            rooms = rooms ?? new List<Room>();
            int missing = unplaced?.Count ?? 0;

            // Occupied rooms first, the player's own first of all, then the empty ones; in each group
            // the house's own order. "Occupied" leaves the empty ones out; nothing else drops a room.
            var shown = rooms
                .Select((room, index) => (room, index))
                .Where(item => filter == Filter.All || (filter == Filter.Occupied && item.room.Occupants.Count > 0))
                .OrderBy(item => item.room.Occupants.Any(o => o.IsPlayer) ? 0 : item.room.Occupants.Count > 0 ? 1 : 2)
                .ThenBy(item => item.index)
                .Select(item => item.room).ToList();

            bool side = missing > 0 && filter == Filter.All;
            bool alone = filter == Filter.Unplaced;
            float sideWidth = side ? Mathf.Round(width * .24f) : 0f;
            float gridWidth = alone ? 0f : width - (side ? sideWidth + Gap * scale : 0f);
            int columns = gridWidth >= TwoColumns * scale ? 2 : 1;
            float cardWidth = columns == 0 ? 0f : (gridWidth - Gap * scale * (columns - 1)) / columns;

            // A row of cards is as tall as its tallest: each card measures what it holds, then the
            // row evens them up so the grid keeps its lines.
            float y = 0f;
            for (int start = 0; start < shown.Count && !alone; start += columns)
            {
                var row = new List<RectTransform>();
                float rowHeight = 0f;
                for (int c = 0; c < columns && start + c < shown.Count; c++)
                {
                    var card = Card(root, shown[start + c], new Vector2(c * (cardWidth + Gap * scale), -y), cardWidth, scale, font, out float need);
                    row.Add(card);
                    rowHeight = Mathf.Max(rowHeight, need);
                }
                foreach (var card in row) card.sizeDelta = new Vector2(cardWidth, rowHeight);
                y += rowHeight + Gap * scale;
            }
            float gridHeight = Mathf.Max(0f, y - Gap * scale);

            float sideHeight = 0f;
            if (side || alone)
            {
                float w = alone ? Mathf.Min(width, 520f * scale) : sideWidth;
                float x = alone ? 0f : width - sideWidth;
                sideHeight = UnplacedCard(root, unplaced ?? new List<Occupant>(), new Vector2(x, 0f), w, scale, font);
            }

            float height = Mathf.Max(gridHeight, sideHeight);
            element.minHeight = height;
            element.preferredHeight = height;
            return root;
        }

        /// <summary>One room's card, its contents laid from the top; <paramref name="need"/> is the height they take.</summary>
        private static RectTransform Card(RectTransform root, Room room, Vector2 at, float width, float scale, TMP_FontAsset font, out float need)
        {
            var card = HudPrimitives.KitCard(CardPrefix + room.Name, root);
            card.anchorMin = card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.anchoredPosition = at;
            var size = new Vector2(width, 0f);
            card.sizeDelta = size;
            int count = room.Occupants.Count;

            // The head: the room's mark in the heading blue, its name, and the count on a quiet pill.
            float left = Pad * scale;
            var mark = RoomLabels.Mark(room.Name);
            if (mark != null)
            {
                var art = new GameObject("Room glyph", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                art.rectTransform.SetParent(card, false);
                Place(art.rectTransform, new Vector2(left, -(Pad + 3f) * scale), new Vector2(24f * scale, 24f * scale));
                art.sprite = mark; art.color = count > 0 ? UiTheme.Heading : UiTheme.Muted;
                art.preserveAspect = true; art.raycastTarget = false;
                left += 34f * scale;
            }
            var name = Label(card, RoomLabels.Name(room.Name), 20, UiTheme.Paper, scale, font);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            Place(name.rectTransform, new Vector2(left, -Pad * scale), new Vector2(size.x - left - 70f * scale, 28f * scale));
            name.enableAutoSizing = true; name.fontSizeMax = name.fontSize; name.fontSizeMin = 14f * scale;

            var pill = HudPrimitives.Fill("Tally", card, UiTheme.SurfaceRaised, Mathf.RoundToInt(13f * scale));
            UiTheme.PackSliced(pill.GetComponent<Image>(), PackArt.KitPillFill, 13f * scale, UiTheme.SurfaceRaised);
            pill.anchorMin = pill.anchorMax = new Vector2(1f, 1f);
            pill.pivot = new Vector2(1f, 1f);
            pill.anchoredPosition = new Vector2(-Pad * scale, -(Pad - 1f) * scale);
            pill.sizeDelta = new Vector2(44f * scale, 28f * scale);
            var tally = Label(pill, count.ToString(), 15, count > 0 ? UiTheme.Paper : UiTheme.Muted, scale, font);
            tally.alignment = TextAlignmentOptions.Center;
            tally.rectTransform.anchorMin = Vector2.zero; tally.rectTransform.anchorMax = Vector2.one;
            tally.rectTransform.offsetMin = Vector2.zero; tally.rectTransform.offsetMax = Vector2.zero;

            string line = count == 0 ? "Empty" : count == 1 ? "1 here" : count + " here";
            var under = Label(card, line, 15, UiTheme.Muted, scale, font);
            Place(under.rectTransform, new Vector2(Pad * scale, -(Pad + 32f) * scale), new Vector2(size.x - 2f * Pad * scale, 22f * scale));
            need = (Pad + 58f + Pad - 4f) * scale;
            if (count == 0) return card;

            // The people: a face and a first name each, the player's ring in the accent and their
            // name marked "(You)" - the one emphasis this card carries.
            float x = Pad * scale, y = (Pad + 64f) * scale, span = size.x - 2f * Pad * scale;
            foreach (var occupant in room.Occupants.OrderBy(o => o.IsPlayer ? 0 : 1).ThenBy(o => o.Name, System.StringComparer.Ordinal))
            {
                var who = Label(card, HudPrimitives.WithYou(occupant.Name?.Split(' ')[0], occupant.IsPlayer), 15, UiTheme.Paper, scale, font);
                if (semibold != null) who.font = semibold;
                float words = Mathf.Min(Mathf.Ceil(who.GetPreferredValues(who.text).x) + 2f, 150f * scale);
                float token = (Face + 10f) * scale + words;
                if (x > Pad * scale && x - Pad * scale + token > span) { x = Pad * scale; y += TokenRow * scale; }
                var rim = HudPrimitives.Portrait(card, occupant.Portrait, occupant.IsPlayer ? UiTheme.Accent : UiTheme.Outline,
                    Face * scale, 2f * scale, false, occupant.Character);
                rim.name = "Occupant · " + occupant.Name;
                rim.anchorMin = rim.anchorMax = new Vector2(0f, 1f);
                rim.pivot = new Vector2(0f, 1f);
                rim.anchoredPosition = new Vector2(x, -y);
                Place(who.rectTransform, new Vector2(x + (Face + 10f) * scale, -y - (Face * .5f - 11f) * scale), new Vector2(words, 22f * scale));
                x += token + 20f * scale;
            }
            need = y + (Face + 4f + Pad) * scale;
            return card;
        }

        /// <summary>
        /// Houseguests the house has no body for: their location is unavailable, and the card says
        /// only that. Returns its height.
        /// </summary>
        private static float UnplacedCard(RectTransform root, IList<Occupant> unplaced, Vector2 at, float width, float scale, TMP_FontAsset font)
        {
            var card = HudPrimitives.KitCard(UnplacedName, root);
            card.anchorMin = card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.anchoredPosition = at;
            float y = Pad * scale;
            var mark = UiTheme.Pack(PackArt.KitIconLocationUnknown);
            if (mark != null)
            {
                var art = new GameObject("Unplaced glyph", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                art.rectTransform.SetParent(card, false);
                Place(art.rectTransform, new Vector2(Pad * scale, -y), new Vector2(26f * scale, 26f * scale));
                art.sprite = mark; art.color = UiTheme.Muted; art.preserveAspect = true; art.raycastTarget = false;
                y += 36f * scale;
            }
            var title = Label(card, "Location unavailable", 19, UiTheme.Paper, scale, font);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) title.font = semibold;
            Place(title.rectTransform, new Vector2(Pad * scale, -y), new Vector2(width - 2f * Pad * scale, 26f * scale));
            y += 30f * scale;
            var body = Label(card, "The house has no room to show for them right now.", 14, UiTheme.Muted, scale, font);
            body.textWrappingMode = TextWrappingModes.Normal;
            float bodyHeight = Mathf.Ceil(body.GetPreferredValues(body.text, width - 2f * Pad * scale, 0f).y) + 4f;
            Place(body.rectTransform, new Vector2(Pad * scale, -y), new Vector2(width - 2f * Pad * scale, bodyHeight));
            y += bodyHeight + 12f * scale;
            foreach (var occupant in unplaced)
            {
                var rim = HudPrimitives.Portrait(card, occupant.Portrait, UiTheme.Outline, Face * scale, 2f * scale, false, occupant.Character);
                rim.name = "Occupant · " + occupant.Name;
                rim.anchorMin = rim.anchorMax = new Vector2(0f, 1f);
                rim.pivot = new Vector2(0f, 1f);
                rim.anchoredPosition = new Vector2(Pad * scale, -y);
                var who = Label(card, HudPrimitives.WithYou(occupant.Name, occupant.IsPlayer), 15, UiTheme.Paper, scale, font);
                if (semibold != null) who.font = semibold;
                Place(who.rectTransform, new Vector2((Pad + Face + 10f) * scale, -y - (Face * .5f - 11f) * scale),
                    new Vector2(width - (2f * Pad + Face + 10f) * scale, 22f * scale));
                y += TokenRow * scale;
            }
            float height = y + (Pad - 8f) * scale;
            card.sizeDelta = new Vector2(width, height);
            return height;
        }

        private static void Place(RectTransform rect, Vector2 at, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
        }

        private static TMP_Text Label(
            Transform parent, string value, int size, Color colour, float scale, TMP_FontAsset font)
        {
            var label = HudPrimitives.Label("Text", parent, Mathf.RoundToInt(size * scale), colour);
            if (label.font == null && font != null) label.font = font;
            label.text = value;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }
    }
}
