using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The three shapes the broadcast overlays are built from: a disc, a themed panel, and a label.
    ///
    /// <para>Extracted at the third copy. <see cref="CastRail"/>, <see cref="CeremonyTakeover"/> and
    /// <see cref="VoteReveal"/> each need a circular portrait frame and a non-raycasting label, and
    /// three private copies is where they start to disagree — one forgets <c>raycastTarget</c>, one
    /// forgets the font fallback, and the bug shows up as an overlay quietly eating a click.</para>
    ///
    /// <para>Everything here is inert by construction: no graphic takes raycasts. Overlays that sit
    /// over the set during the most consequential seconds of the episode must not be able to swallow
    /// input, and making that a property of the primitives is stronger than remembering it at each
    /// call site.</para>
    /// </summary>
    public static class HudPrimitives
    {
        /// <summary>A filled circle. Used for portrait frames, status rings and ceremony marks.</summary>
        public static RectTransform Disc(string name, Transform parent, Color colour)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var image = rect.GetComponent<Image>();
            image.sprite = UiTheme.Circle();
            image.type = Image.Type.Simple;
            image.color = colour;
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>
        /// A themed rounded rectangle. Always routed through <see cref="UiTheme.Style"/>: an Image
        /// with a bare colour and no sprite does not draw, which cost a debugging pass when a
        /// full-screen scrim silently rendered nothing.
        /// </summary>
        /// <summary>One of the mockups' glass cards: the night ground, a cyan hairline, a soft glow.</summary>
        public static RectTransform Glass(string name, Transform parent, int radius = UiTheme.GlassRadius)
        {
            var rect = Fill(name, parent, UiTheme.GlassFill, radius);
            UiTheme.Glass(rect, radius);
            return rect;
        }

        /// <summary>
        /// A card heading in the mockups' voice: the semibold weight, tracked. Uppercasing is the
        /// caller's, after localisation, as the season report already does.
        /// </summary>
        public static TMP_Text Heading(string name, Transform parent, float size, Color colour,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var label = Label(name, parent, size, colour, alignment);
            var font = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (font != null) label.font = font;
            label.characterSpacing = 6f;
            return label;
        }

        /// <summary>
        /// One of the mockups' glyphs, sized and centred inside <paramref name="parent"/>'s
        /// upper-left at <paramref name="position"/>. Returns null when the icon pass has not been
        /// run, so every caller falls back to the plainer shape it drew before rather than to an
        /// empty square.
        /// </summary>
        public static Image Glyph(string name, Transform parent, string icon, Color tint, Vector2 position, float side)
        {
            var sprite = UiTheme.Icon(icon);
            if (sprite == null) return null;
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(side, side);
            var image = rect.GetComponent<Image>();
            image.sprite = sprite;
            image.color = tint;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// The "there is more behind this" mark at the right-hand end of a card, as every option row
        /// in mockup-11 carries.
        ///
        /// <para>Drawn from two bars rather than from a glyph: the generated set has no chevron, and
        /// a font character is not an option — the shipped atlas is LiberationSans SDF, which renders
        /// a dingbat as tofu. It is decoration only; what the row does is in its caption.</para>
        /// </summary>
        public static RectTransform Chevron(Transform parent, Color tint, float side)
        {
            var mark = new GameObject("Chevron", typeof(RectTransform)).GetComponent<RectTransform>();
            mark.SetParent(parent, false);
            mark.anchorMin = new Vector2(1f, .5f);
            mark.anchorMax = new Vector2(1f, .5f);
            mark.pivot = new Vector2(1f, .5f);
            mark.sizeDelta = new Vector2(side, side);
            float arm = Mathf.Max(2f, side * 0.62f);
            float thickness = Mathf.Max(1.5f, side * 0.13f);
            for (int i = 0; i < 2; i++)
            {
                var bar = Fill("Arm", mark, tint, 1);
                Centre(bar, arm, thickness);
                bar.anchoredPosition = new Vector2(0f, (i == 0 ? 1f : -1f) * arm * 0.25f);
                bar.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? -45f : 45f);
            }
            return mark;
        }

        /// <summary>
        /// The mockups' conversation dial (mockup-06, mockup-12): a hub carrying the speaker's face,
        /// and a ring of seats around it for the petals the screen adds.
        ///
        /// <para>Geometry only. The petals are the screen's buttons, with the screen's captions on
        /// them, because a caption is the contract a test and a screen reader identify a control by
        /// — the dial decides where a control sits and never what it says.</para>
        /// </summary>
        public sealed class Dial
        {
            /// <summary>The block the whole dial occupies, for the layout that hosts it.</summary>
            public RectTransform Root { get; }
            /// <summary>The speaker's portrait at the centre.</summary>
            public RectTransform Hub { get; }
            /// <summary>How large one petal is.</summary>
            public Vector2 Petal { get; }
            /// <summary>How many seats the ring has.</summary>
            public int Seats { get; }
            /// <summary>How many of them are taken.</summary>
            public int Taken { get; private set; }

            private readonly float radius;

            internal Dial(RectTransform root, RectTransform hub, int seats, float radius, Vector2 petal)
            {
                Root = root; Hub = hub; Seats = seats; Petal = petal; this.radius = radius;
            }

            /// <summary>Where the seat at <paramref name="index"/> sits, clockwise from the top.</summary>
            public Vector2 Seat(int index)
            {
                float angle = (index % Seats) * Mathf.PI * 2f / Seats;
                return new Vector2(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius);
            }

            /// <summary>Puts <paramref name="rect"/> on the next free seat and returns it.</summary>
            public RectTransform Place(RectTransform rect)
            {
                var seat = Seat(Taken);
                Taken++;
                rect.anchorMin = new Vector2(.5f, .5f);
                rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = seat;
                rect.sizeDelta = Petal;
                return rect;
            }
        }

        /// <summary>The petal's unscaled footprint and the unscaled ring radius, in that order.</summary>
        public static readonly Vector2 DialPetal = new Vector2(150f, 128f);
        public const float DialRadius = 205f;
        public const float DialHub = 112f;

        /// <summary>
        /// Builds a <see cref="Dial"/> under <paramref name="parent"/> at <paramref name="scale"/>.
        ///
        /// <para>The petal is a rectangle rather than the mockups' disc because the captions are
        /// sentences, not single words, and a disc wide enough to hold "Tell them something personal"
        /// at seven-around would not fit the panel. The ring, the hub and the order are the mockup's;
        /// the petal is the shape the words need.</para>
        /// </summary>
        public static Dial Radial(string name, Transform parent, Texture face, int seats, float scale)
        {
            seats = Mathf.Max(1, seats);
            scale = Mathf.Max(0.1f, scale);
            var petal = DialPetal * scale;
            float radius = DialRadius * scale;

            var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = new Vector2(.5f, .5f);
            root.anchorMax = new Vector2(.5f, .5f);
            root.pivot = new Vector2(.5f, .5f);
            root.sizeDelta = new Vector2(2f * (radius + petal.x * .5f), 2f * (radius + petal.y * .5f));

            var hub = Portrait(root, face, UiTheme.Hairline, DialHub * scale, 4f * scale, false);
            hub.name = "Dial hub";
            hub.anchorMin = new Vector2(.5f, .5f);
            hub.anchorMax = new Vector2(.5f, .5f);
            hub.pivot = new Vector2(.5f, .5f);
            hub.anchoredPosition = Vector2.zero;
            return new Dial(root, hub, seats, radius, petal);
        }

        /// <summary>A pill chip: a tinted ground at 18 %, a hairline in the tint, the word in the tint.</summary>
        public static RectTransform Chip(string name, Transform parent, string text, Color tint, float width, float height)
        {
            int radius = Mathf.Clamp(Mathf.RoundToInt(height * 0.5f), 4, 16);
            var rect = Fill(name, parent, new Color(tint.r, tint.g, tint.b, 0.18f), radius);
            rect.sizeDelta = new Vector2(width, height);
            UiTheme.AddBorder(rect, radius, new Color(tint.r, tint.g, tint.b, 0.8f));
            var label = Label("Word", rect, Mathf.Max(10f, height * 0.5f), tint, TextAlignmentOptions.Center);
            label.text = text;
            var font = UiTheme.Font(UiTheme.Weight.Medium);
            if (font != null) label.font = font;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8f, 0f);
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);
            return rect;
        }

        public static RectTransform Fill(string name, Transform parent, Color colour, int radius)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var image = rect.GetComponent<Image>();
            UiTheme.Style(image, colour, Mathf.Max(1, radius));
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>A non-interactive label, with the project's font fallback already applied.</summary>
        public static TMP_Text Label(string name, Transform parent, float size, Color colour,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var holder = new GameObject(name, typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            var label = holder.AddComponent<TextMeshProUGUI>();
            var font = TMP_Settings.defaultFontAsset != null
                ? TMP_Settings.defaultFontAsset
                : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null) label.font = font;
            label.fontSize = size;
            label.color = colour;
            // Event and cast text is authored copy, never markup; the HUD reads it literally too.
            label.richText = false;
            label.raycastTarget = false;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Truncate;
            return label;
        }

        /// <summary>
        /// A circular portrait, masked, with a status ring behind it and a graceful hole when the
        /// persona has no authored art.
        /// </summary>
        public static RectTransform Portrait(
            Transform parent, Texture face, Color ring, float diameter, float ringWidth, bool dim)
        {
            var rim = Disc("Ring", parent, ring);
            rim.sizeDelta = new Vector2(diameter + ringWidth * 2f, diameter + ringWidth * 2f);

            var frame = Disc("Frame", rim, dim ? new Color(1f, 1f, 1f, .45f) : Color.white);
            frame.anchorMin = new Vector2(.5f, .5f);
            frame.anchorMax = new Vector2(.5f, .5f);
            frame.pivot = new Vector2(.5f, .5f);
            frame.anchoredPosition = Vector2.zero;
            frame.sizeDelta = new Vector2(diameter, diameter);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            if (face != null)
            {
                var raw = new GameObject("Face", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                raw.rectTransform.SetParent(frame, false);
                raw.rectTransform.anchorMin = Vector2.zero;
                raw.rectTransform.anchorMax = Vector2.one;
                raw.rectTransform.offsetMin = Vector2.zero;
                raw.rectTransform.offsetMax = Vector2.zero;
                raw.texture = face;
                raw.raycastTarget = false;
                raw.color = dim ? new Color(.6f, .65f, .7f, 1f) : Color.white;
            }
            else
            {
                var hole = Disc("Initial", frame, UiTheme.SurfaceRaised);
                hole.anchorMin = Vector2.zero; hole.anchorMax = Vector2.one;
                hole.offsetMin = Vector2.zero; hole.offsetMax = Vector2.zero;

                // A houseguest with no body yet gets the generated silhouette rather than an empty
                // disc, which used to read as a rendering failure rather than as "not loaded".
                var silhouette = UiTheme.Icon("houseguest");
                if (silhouette != null)
                {
                    var art = new GameObject("Silhouette", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                    art.SetParent(hole, false);
                    art.anchorMin = new Vector2(0.5f, 0f);
                    art.anchorMax = new Vector2(0.5f, 0f);
                    art.pivot = new Vector2(0.5f, 0f);
                    art.sizeDelta = new Vector2(diameter * .82f, diameter * .82f);
                    art.anchoredPosition = new Vector2(0f, diameter * .05f);
                    var image = art.GetComponent<Image>();
                    image.sprite = silhouette;
                    image.color = new Color(1f, 1f, 1f, dim ? .18f : .28f);
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                }
            }
            return rim;
        }

        /// <summary>Which role badge sits on a portrait, if any.</summary>
        public enum RoleMark { None, Nominee, HeadOfHousehold, VetoHolder }

        /// <summary>
        /// Pins a small role badge to a portrait's upper-right, the way the web build marks a
        /// nominee with a target and the Head of Household with a crown.
        ///
        /// <para>Drawn from a generated sprite where one exists, and from discs and bars where it
        /// does not. Neither path uses a font glyph: the shipped atlas is LiberationSans SDF, which
        /// has no dingbats, so a crown character renders as tofu — the constraint that made the
        /// ceremony mark a shape and the icon rail a set of drawn forms. The sprites come from
        /// <c>Gamesim ▸ U07 ▸ Generate HUD icons</c>, and the drawn shapes remain so that a clone
        /// which has never run that pass still gets a badge.</para>
        ///
        /// <para>The badge is decoration on top of a status that is already stated in words
        /// elsewhere. It never carries information on its own, because a coloured shape is not
        /// something a screen reader can announce.</para>
        /// </summary>
        public static RectTransform AddRoleMark(RectTransform rim, RoleMark mark, float diameter)
        {
            if (rim == null || mark == RoleMark.None) return null;

            float size = Mathf.Max(14f, diameter * 0.34f);
            var badge = Disc("Role mark", rim, Tint(mark));
            badge.anchorMin = new Vector2(1f, 1f);
            badge.anchorMax = new Vector2(1f, 1f);
            badge.pivot = new Vector2(.5f, .5f);
            // Sat on the rim's shoulder at roughly 45 degrees, so it clears the face either side.
            badge.anchoredPosition = new Vector2(-size * 0.28f, -size * 0.28f);
            badge.sizeDelta = new Vector2(size, size);

            // The generated sprite when the icon pass has been run, and the drawn shape when it has
            // not. The badge is the same size and colour either way, so a clone without the art
            // loses fidelity and nothing else.
            var glyph = UiTheme.Icon(IconFor(mark));
            if (glyph != null)
            {
                var art = new GameObject("Role glyph", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                art.SetParent(badge, false);
                Centre(art, size * 0.70f, size * 0.70f);
                var image = art.GetComponent<Image>();
                image.sprite = glyph;
                image.color = UiTheme.OnColor(Tint(mark));
                image.raycastTarget = false;
                image.preserveAspect = true;
                return badge;
            }

            switch (mark)
            {
                case RoleMark.Nominee:
                    // A target: ring, gap, core.
                    Ring(badge, size * 0.62f, UiTheme.Ink);
                    Ring(badge, size * 0.30f, Tint(mark));
                    break;
                case RoleMark.VetoHolder:
                    // A medal: a dark bar across a gold field.
                    var bar = Fill("Veto bar", badge, UiTheme.Ink, 1);
                    Centre(bar, size * 0.58f, Mathf.Max(2f, size * 0.14f));
                    bar.localRotation = Quaternion.Euler(0f, 0f, -35f);
                    break;
                case RoleMark.HeadOfHousehold:
                    // A crown: a band with three points above it.
                    var band = Fill("Crown band", badge, UiTheme.Ink, 1);
                    Centre(band, size * 0.56f, Mathf.Max(2f, size * 0.12f));
                    band.anchoredPosition = new Vector2(0f, -size * 0.14f);
                    float point = Mathf.Max(2f, size * 0.13f);
                    for (int i = -1; i <= 1; i++)
                    {
                        var spike = Fill("Crown point", badge, UiTheme.Ink, 1);
                        Centre(spike, point, point * (i == 0 ? 1.7f : 1.2f));
                        spike.anchoredPosition = new Vector2(i * size * 0.20f, size * 0.06f);
                    }
                    break;
            }
            return badge;
        }

        /// <summary>The generated icon each role uses, when the set exists.</summary>
        private static string IconFor(RoleMark mark)
        {
            switch (mark)
            {
                case RoleMark.Nominee: return "target";
                case RoleMark.HeadOfHousehold: return "crown";
                case RoleMark.VetoHolder: return "veto-token";
                default: return null;
            }
        }

        private static Color Tint(RoleMark mark)
        {
            switch (mark)
            {
                case RoleMark.Nominee: return UiTheme.Danger;
                case RoleMark.VetoHolder: return UiTheme.Gold;
                case RoleMark.HeadOfHousehold: return UiTheme.Gold;
                default: return UiTheme.Muted;
            }
        }

        private static void Ring(RectTransform parent, float size, Color colour)
        {
            var ring = Disc("Ring", parent, colour);
            Centre(ring, size, size);
        }

        private static void Centre(RectTransform rect, float width, float height)
        {
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
