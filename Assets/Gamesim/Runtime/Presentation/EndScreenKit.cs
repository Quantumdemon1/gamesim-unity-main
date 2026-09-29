using System;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The pieces the two summary screens are built from: the season's end (<see cref="SeasonReport"/>)
    /// and the week's (<see cref="WeeklyRecapScreen"/>). Season Complete Pack 7's frames, drawn
    /// through <see cref="UiTheme.PackSliced"/> with the procedural card as the fallback on a clone
    /// without the pack; its whole sprites; section headings; stat cards; a bar drawn from real
    /// counts; and a label measured to the height its words need.
    ///
    /// <para>Every piece is decoration around what the screens already say. None of them is a
    /// control, none catches the mouse, and none carries a caption a test finds a control by: the
    /// screens keep their controls and their words, and these only frame them.</para>
    /// </summary>
    public static class EndScreenKit
    {
        /// <summary>
        /// The pack files whose frame sits inside a baked glow 38 px deep; the rest sit 8 px in. A
        /// frame is drawn out past its rect by that much, scaled, so the visible edge lands on the
        /// rect whichever kind it is and a winner's row lines up with a juror's.
        /// </summary>
        private static float BodyInset(string path) =>
            path == PackArt.SeasonWinnerHero || path == PackArt.SeasonStandingWinner || path == PackArt.SeasonButtonPrimary ? 38f : 8f;

        /// <summary>
        /// Frames <paramref name="host"/> with a pack sprite on a child named "Art", drawn behind
        /// everything else on it, its corners <paramref name="border"/> canvas units a side. Without
        /// the pack, the plainer card: <paramref name="fallback"/> with a hairline edge.
        /// </summary>
        /// <summary>What a frame drawn without the pack is called: the procedural card, which is also sliced.</summary>
        public const string DrawnFrameName = "Art (drawn)";

        /// <summary>Whether a frame <see cref="Frame"/> returned is the pack's own art rather than the drawn fallback.</summary>
        public static bool Packed(Image art) => art != null && art.name == "Art" && art.sprite != null;

        public static Image Frame(RectTransform host, string path, float border, Color fallback, Color? edge = null, int radius = 12)
        {
            var art = new GameObject("Art", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = art.rectTransform;
            rect.SetParent(host, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            art.raycastTarget = false;
            if (UiTheme.PackSliced(art, path, border))
            {
                var sprite = art.sprite;
                float authored = Mathf.Max(1f, Mathf.Max(sprite.border.x, sprite.border.y, sprite.border.z, sprite.border.w));
                float overhang = BodyInset(path) * border / authored;
                rect.offsetMin = new Vector2(-overhang, -overhang);
                rect.offsetMax = new Vector2(overhang, overhang);
            }
            else
            {
                // The drawn card, named apart so a caller can tell the pack's face from the fallback's.
                art.name = DrawnFrameName;
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                UiTheme.Style(art, fallback, radius);
                UiTheme.AddBorder(rect, radius, edge ?? new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .3f));
            }
            return art;
        }

        /// <summary>
        /// A whole pack sprite (a badge, an icon, a node) at <paramref name="side"/> units square,
        /// centred on <paramref name="centre"/> in its parent's upper-left space. Without the pack,
        /// the generated glyph <paramref name="fallbackIcon"/> in <paramref name="tint"/>; with
        /// neither, null, and the caller draws what it drew before.
        /// </summary>
        public static Image Picture(string name, RectTransform parent, string path, string fallbackIcon, Color tint,
            Vector2 centre, float side)
        {
            var sprite = UiTheme.Pack(path);
            bool pack = sprite != null;
            if (!pack) sprite = string.IsNullOrEmpty(fallbackIcon) ? null : UiTheme.Icon(fallbackIcon);
            if (sprite == null) return null;
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = image.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(.5f, .5f);
            // A pack icon carries about a ninth of its size in clear padding each side; the glyph
            // set draws to its edges. The pack one is drawn larger so the two read the same size.
            float drawn = pack ? side * 1.28f : side;
            rect.sizeDelta = new Vector2(drawn, drawn);
            rect.anchoredPosition = centre;
            image.sprite = sprite;
            image.color = pack ? Color.white : tint;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A plain rect in a parent's upper-left space: the layout the two screens place everything with.</summary>
        public static RectTransform Box(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Place(rect, x, y, width, height);
            return rect;
        }

        public static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        /// <summary>A label placed in a parent's upper-left space, its words through the localisation table.</summary>
        public static TMP_Text Text(string name, Transform parent, string words, float size, Color colour,
            float x, float y, float width, float height, TextAlignmentOptions align = TextAlignmentOptions.Left,
            UiTheme.Weight weight = UiTheme.Weight.Regular)
        {
            var label = HudPrimitives.Label(name, parent, size, colour, align);
            label.text = Localisation.Text(words ?? string.Empty);
            if (weight != UiTheme.Weight.Regular)
            {
                var font = UiTheme.Font(weight);
                if (font != null) label.font = font;
            }
            Place(label.rectTransform, x, y, width, height);
            return label;
        }

        /// <summary>
        /// A label as tall as its words at <paramref name="width"/>, never shorter than a line and a
        /// third of its size - the box Inter needs before it draws anything at all. Returns the height.
        /// </summary>
        public static float Wrapped(TMP_Text label, float width, int maxLines = 0)
        {
            label.textWrappingMode = TextWrappingModes.Normal;
            float line = label.fontSize * 1.32f;
            float height = Mathf.Ceil(label.GetPreferredValues(label.text, width, 0f).y) + 2f;
            if (maxLines > 0 && height > line * maxLines + 2f)
            {
                height = line * maxLines + 2f;
                label.overflowMode = TextOverflowModes.Ellipsis;
            }
            height = Mathf.Max(height, line);
            label.rectTransform.sizeDelta = new Vector2(width, height);
            return height;
        }

        /// <summary>
        /// A section's head, as the mockups draw one: a glyph, the title letterspaced in the heading
        /// blue, and a muted line under it when there is one. Returns the height it took.
        /// </summary>
        public static float Heading(RectTransform parent, string title, string subtitle, string icon, float x, float y, float width)
        {
            float left = x;
            var glyph = Picture("Heading mark", parent, null, icon, UiTheme.Heading, new Vector2(x + 13f, -(y + 13f)), 24f);
            if (glyph != null) left += 34f;
            var head = Text("Heading", parent, Localisation.Text(title).ToUpperInvariant(), 17f, UiTheme.Paper,
                left, y, width - (left - x), 26f, TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
            head.characterSpacing = 2f;
            float used = 28f;
            if (!string.IsNullOrEmpty(subtitle))
            {
                var line = Text("Subheading", parent, subtitle, 13f, UiTheme.Muted, left, y + used, width - (left - x), 20f);
                used += Wrapped(line, width - (left - x)) + 2f;
            }
            return used + 6f;
        }

        /// <summary>
        /// A bar of two parts drawn from real counts: <paramref name="a"/> in <paramref name="tintA"/>
        /// from the left, <paramref name="b"/> in <paramref name="tintB"/> after it, on a quiet track.
        /// Nothing here is a picture of a number: a baked chart would show a split the season never had.
        /// </summary>
        public static RectTransform SplitBar(RectTransform parent, float x, float y, float width, float height,
            int a, int b, Color tintA, Color tintB)
        {
            var track = Box("Vote bar", parent, x, y, width, height);
            int radius = Mathf.Max(2, Mathf.RoundToInt(height * .5f) - 1);
            var ground = HudPrimitives.Fill("Track", track, new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .9f), radius);
            ground.anchorMin = Vector2.zero; ground.anchorMax = Vector2.one; ground.offsetMin = Vector2.zero; ground.offsetMax = Vector2.zero;
            int total = Math.Max(0, a) + Math.Max(0, b);
            if (total == 0) return track;
            float first = width * a / total;
            if (a > 0)
            {
                var left = HudPrimitives.Fill("First", track, tintA, radius);
                Place(left, 0f, 0f, Mathf.Max(height, first - (b > 0 ? 2f : 0f)), height);
            }
            if (b > 0)
            {
                var right = HudPrimitives.Fill("Second", track, tintB, radius);
                Place(right, first + (a > 0 ? 2f : 0f), 0f, Mathf.Max(height, width - first - (a > 0 ? 2f : 0f)), height);
            }
            return track;
        }

        /// <summary>
        /// A stat card: its frame, its icon, the number in its colour, the caption the screen has
        /// always given that number (word for word; tests read it), and a line under them.
        /// </summary>
        public static RectTransform StatCard(RectTransform parent, string name, float x, float y, float width, float height,
            string frame, string icon, string fallbackIcon, string value, string caption, string line, Color tint)
        {
            var card = Box(name, parent, x, y, width, height);
            Frame(card, frame, 14f, new Color(UiTheme.Surface.r, UiTheme.Surface.g, UiTheme.Surface.b, .92f),
                new Color(tint.r, tint.g, tint.b, .55f));
            float pad = 16f, iconSide = Mathf.Min(54f, height * .42f);
            bool roomForIcon = width >= 230f;
            float textX = pad;
            if (roomForIcon && Picture("Icon", card, icon, fallbackIcon, tint, new Vector2(pad + iconSide * .5f, -(pad + iconSide * .5f + 4f)), iconSide) != null)
                textX = pad + iconSide + 12f;
            float textWidth = width - textX - pad;
            var number = Text("Value", card, value, 34f, tint, textX, pad - 4f, textWidth, 44f, TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
            number.enableAutoSizing = true; number.fontSizeMax = 34f; number.fontSizeMin = 18f;
            var label = Text("Caption", card, caption, 14f, tint, textX, pad + 40f, textWidth, 20f, TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
            label.characterSpacing = 1f;
            label.enableAutoSizing = true; label.fontSizeMax = 14f; label.fontSizeMin = 10f;
            if (!string.IsNullOrEmpty(line))
            {
                float top = pad + 64f;
                var words = Text("Line", card, line, 13f, UiTheme.Muted, pad, top, width - pad * 2f, 18f);
                float room = Mathf.Max(18f, height - top - 10f);
                int lines = Mathf.Max(1, Mathf.FloorToInt(room / (13f * 1.32f)));
                Wrapped(words, width - pad * 2f, lines);
            }
            return card;
        }

        /// <summary>A small filled or outlined pill with a word in it, placed in a parent's upper-left space.</summary>
        public static RectTransform Pill(RectTransform parent, string word, Color tint, float x, float y, float width, float height, bool filled = false)
        {
            var pill = HudPrimitives.Chip("Pill", parent, Localisation.Text(word), tint, width, height, filled);
            Place(pill, x, y, width, height);
            return pill;
        }

        /// <summary>The first sentence of <paramref name="text"/>, cut at a word under <paramref name="limit"/> characters.</summary>
        public static string Excerpt(string text, int limit = 110)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string clean = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
            int stop = -1;
            for (int i = 0; i < clean.Length; i++)
                if ((clean[i] == '.' || clean[i] == '!' || clean[i] == '?') && (i + 1 == clean.Length || clean[i + 1] == ' ')) { stop = i; break; }
            string sentence = stop >= 0 ? clean.Substring(0, stop + 1) : clean;
            if (sentence.Length <= limit) return sentence;
            int cut = sentence.LastIndexOf(' ', limit - 1);
            return (cut > limit / 2 ? sentence.Substring(0, cut) : sentence.Substring(0, limit - 1)).TrimEnd(',', ';', ':') + "…";
        }

        /// <summary>A houseguest's place as the career words it: "1st", "2nd", "3rd", "11th".</summary>
        public static string PlaceWord(int value)
        {
            if (value <= 0) return "—";
            string suffix;
            if (value % 100 >= 11 && value % 100 <= 13) suffix = "th";
            else
                switch (value % 10)
                {
                    case 1: suffix = "st"; break;
                    case 2: suffix = "nd"; break;
                    case 3: suffix = "rd"; break;
                    default: suffix = "th"; break;
                }
            return value + suffix;
        }
    }
}
