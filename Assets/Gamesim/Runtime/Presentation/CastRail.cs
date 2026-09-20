using System.Collections.Generic;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The permanent cast strip down the left edge: every houseguest's face, name, mood and
    /// standing, visible in every frame of the episode.
    ///
    /// <para>Before this, the cast was legible only as 1.4%-of-frame silhouettes on the set, and the
    /// HUD named people in prose — so "Jamie Roberts is the replacement nominee" asked the player to
    /// remember which of six tiny figures that was. A broadcast never does that: the faces are
    /// always on screen and the badges carry the state. This is the single change that most affects
    /// how every other frame reads, which is why it is permanent chrome rather than a panel someone
    /// has to open.</para>
    ///
    /// <para>V2 (VISUAL-TARGET.md §4, mockup-01 and -10) makes each entry a <em>portrait chip</em>
    /// rather than a bare portrait: the mockups' glass ground with a cyan hairline, the face, a mood
    /// face glyph drawn in the mood's own colour, and the mood as a single word underneath. The
    /// glyph and the word say the same thing twice on purpose — a coloured face is not a state a
    /// screen reader can announce, so the word carries it and the glyph decorates it.</para>
    ///
    /// <para>Rebuilt from committed state on each HUD render and never animated per frame: the rail
    /// shows what the simulation holds and holds no opinion of its own.</para>
    /// </summary>
    public static class CastRail
    {
        public const string RootName = "Cast rail";
        public const float Width = 92f;

        /// <summary>Names the chip's parts carry, so a test can find them without guessing.</summary>
        public const string ChipName = "Chip";
        public const string MoodGlyphName = "Mood";
        public const string MoodWordName = "Mood word";
        public const string BadgeName = "Badge";

        // The chip is one line deeper than the bare portrait it replaces — name, then mood — and the
        // depth comes out of the face rather than out of the column: the rail has to end above the
        // lower third at the default house of eight, and it did so at exactly this height before.
        private const float Portrait = 42f;
        private const float RingPadding = 3f;
        private const float EntryHeight = 96f;
        private const int ChipRadius = 10;

        /// <summary>The rail's own margins: the gap at the top, and the band the lower third owns.</summary>
        private const float TopMargin = 24f;
        private const float BottomReserve = 100f;
        /// <summary>
        /// How small the rail may draw itself to fit the column it has.
        ///
        /// <para>Eight chips fit at full size, which is the default house; a bigger cast does not,
        /// and a rail whose last houseguest slides under the lower third is worse than one drawn a
        /// little smaller. So the whole chip scales — portrait, name and mood together — down to
        /// this floor, and past it the rail stops shrinking and runs long instead. A rail nobody can
        /// read is not an improvement on a rail that overflows.</para>
        /// </summary>
        private const float MinimumFit = 0.72f;
        // The chip is inset inside the entry so consecutive chips read as separate cards rather
        // than as one long column, and so the glow on the player's chip has somewhere to go.
        private const float ChipInsetX = 2f;
        private const float ChipTop = 2f;
        private const float ChipBottom = 4f;

        /// <summary>How a houseguest is standing right now, and the colour that says so.</summary>
        private readonly struct Standing
        {
            public readonly string Badge;
            public readonly Color Colour;
            public readonly bool Dim;

            public Standing(string badge, Color colour, bool dim)
            {
                Badge = badge; Colour = colour; Dim = dim;
            }
        }

        /// <summary>
        /// Builds the rail under <paramref name="parent"/> and returns it.
        ///
        /// <para><paramref name="portrait"/> is injected rather than resolved here so the rail need
        /// not know how personas map to art — the HUD already owns that mapping, and a second copy
        /// of it is how the two would drift apart.</para>
        /// </summary>
        public static RectTransform Build(
            Transform parent, EpisodeState state, float fontScale, TMP_FontAsset font,
            System.Func<string, Texture> portrait, System.Action<string> onSelect = null)
        {
            var root = new GameObject(RootName, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(14f, -TopMargin);

            var order = Order(state);
            float scale = Fit(parent as RectTransform, order.Count, fontScale);
            root.sizeDelta = new Vector2(Width, order.Count * EntryHeight * scale);

            for (int index = 0; index < order.Count; index++)
                Entry(root, state, order[index], index, scale, font, portrait, onSelect);
            return root;
        }

        /// <summary>
        /// The scale a house of <paramref name="count"/> chips fits the canvas at: never larger than
        /// the player's text preference, and never below <see cref="MinimumFit"/>.
        ///
        /// <para>The floor is absolute rather than a fraction of the preference, so asking for
        /// larger text can shrink the rail back toward its resting size to keep the house on screen
        /// but can never push it smaller than a player who asked for nothing would get.</para>
        ///
        /// <para>A canvas whose rect is not laid out yet — before the first layout pass, or in a
        /// fixture with no screen — reports zero height; that gives the preference back unchanged
        /// rather than collapsing the rail to nothing.</para>
        /// </summary>
        private static float Fit(RectTransform canvas, int count, float fontScale)
        {
            if (canvas == null || count <= 0) return fontScale;
            float available = canvas.rect.height - TopMargin - BottomReserve;
            if (available <= 0f) return fontScale;
            float floor = Mathf.Min(fontScale, MinimumFit);
            return Mathf.Clamp(Mathf.Min(fontScale, available / (count * EntryHeight)), floor, fontScale);
        }

        /// <summary>
        /// The player first, then everyone still playing, then the evicted in the order they left.
        ///
        /// <para>A rail that reshuffled every week would cost the player the spatial memory that
        /// makes it worth having, so only an eviction moves anyone.</para>
        /// </summary>
        private static List<ContestantState> Order(EpisodeState state)
        {
            var active = new List<ContestantState>();
            var gone = new List<ContestantState>();
            ContestantState player = null;

            foreach (var actor in state.contestants)
            {
                if (actor.isPlayer || actor.id == state.playerId) { player = actor; continue; }
                if (actor.status == ContestantStatus.Active) active.Add(actor); else gone.Add(actor);
            }

            var order = new List<ContestantState>();
            if (player != null) order.Add(player);
            order.AddRange(active);
            order.AddRange(gone);
            return order;
        }

        /// <summary>The badge word the rail already shows, expressed as a portrait mark.</summary>
        private static HudPrimitives.RoleMark MarkFor(string badge)
        {
            switch (badge)
            {
                case "HOH": return HudPrimitives.RoleMark.HeadOfHousehold;
                case "VETO": return HudPrimitives.RoleMark.VetoHolder;
                case "NOM": return HudPrimitives.RoleMark.Nominee;
                default: return HudPrimitives.RoleMark.None;
            }
        }

        /// <summary>
        /// Whether a houseguest is out of the house, and so drawn dimmed with their mood replaced by
        /// the fact of it. A winner and a runner-up are not active either, and they are not out: the
        /// finale's two are the whole point of the rail on its last night. Public because the test
        /// that pins the dimming has to ask the same question rather than guess at it - they drifted
        /// apart once already.
        /// </summary>
        public static bool IsOut(ContestantState actor) => actor != null
            && actor.status != ContestantStatus.Active
            && actor.status != ContestantStatus.Winner
            && actor.status != ContestantStatus.RunnerUp;

        private static Standing Read(EpisodeState state, ContestantState actor)
        {
            if (actor.status == ContestantStatus.Winner) return new Standing("WINNER", UiTheme.Gold, false);
            if (actor.status == ContestantStatus.RunnerUp) return new Standing("FINAL 2", UiTheme.Accent, false);
            if (IsOut(actor)) return new Standing("OUT", UiTheme.Muted, true);
            if (actor.id == state.hohId) return new Standing("HOH", UiTheme.Gold, false);
            if (actor.id == state.vetoHolderId) return new Standing("VETO", UiTheme.Gold, false);
            if (state.nominees != null && state.nominees.Contains(actor.id)) return new Standing("NOM", UiTheme.Danger, false);
            if (actor.isPlayer || actor.id == state.playerId) return new Standing("YOU", UiTheme.Accent, false);
            return new Standing(null, UiTheme.Outline, false);
        }

        /// <summary>
        /// The one word a chip shows for a mood. Engine moods are already single words — Happy,
        /// Content, Neutral, Upset, Angry — so this only fills the gap when a save carries none,
        /// and routes the result through the localisation sink like every other string on screen.
        /// </summary>
        private static string MoodWord(ContestantState actor, Standing standing)
        {
            if (standing.Dim) return Localisation.Text("Evicted");
            string mood = actor.mood;
            if (string.IsNullOrEmpty(mood)) return Localisation.Text("Neutral");
            // Defensive: a word is what fits in a 92px chip, so anything composed is cut at the
            // first space rather than allowed to clip.
            int space = mood.IndexOf(' ');
            if (space > 0) mood = mood.Substring(0, space);
            return Localisation.Text(mood);
        }

        private static RectTransform Entry(
            RectTransform root, EpisodeState state, ContestantState actor, int index, float scale,
            TMP_FontAsset font, System.Func<string, Texture> portrait, System.Action<string> onSelect)
        {
            var standing = Read(state, actor);
            bool isPlayer = actor.isPlayer || actor.id == state.playerId;
            var moodColour = standing.Dim ? UiTheme.Muted : RelationshipWeb.MoodColour(actor.mood);

            var entry = new GameObject(actor.name, typeof(RectTransform)).GetComponent<RectTransform>();
            entry.SetParent(root, false);
            entry.anchorMin = new Vector2(0f, 1f);
            entry.anchorMax = new Vector2(0f, 1f);
            entry.pivot = new Vector2(0f, 1f);
            entry.anchoredPosition = new Vector2(0f, -index * EntryHeight * scale);
            entry.sizeDelta = new Vector2(Width, EntryHeight * scale);

            // The mockups' card: the night ground at 85 %, a cyan hairline, corners at the theme's
            // radius. The player's chip is the one that also carries the glow, so "which of these
            // is me" is answered from the far edge of the frame.
            var chip = HudPrimitives.Fill(ChipName, entry, UiTheme.GlassFill, ChipRadius);
            chip.anchorMin = Vector2.zero; chip.anchorMax = Vector2.one;
            chip.offsetMin = new Vector2(ChipInsetX, ChipBottom * scale);
            chip.offsetMax = new Vector2(-ChipInsetX, -ChipTop * scale);
            if (isPlayer) UiTheme.Glass(chip, ChipRadius);
            else UiTheme.AddBorder(chip, ChipRadius,
                new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, standing.Dim ? 0.28f : 0.55f));

            // The ring is the status: a coloured disc showing through as a rim around the face. Every
            // row below it is measured from the ring's own foot, so the chip stacks rather than
            // relying on offsets that would each have to be retuned if the portrait ever changed.
            float ring = (Portrait + RingPadding * 2f) * scale;
            float top = 6f * scale;
            var rim = Disc("Ring", entry, standing.Colour);
            rim.anchorMin = new Vector2(.5f, 1f); rim.anchorMax = new Vector2(.5f, 1f); rim.pivot = new Vector2(.5f, 1f);
            rim.anchoredPosition = new Vector2(0f, -top);
            rim.sizeDelta = new Vector2(ring, ring);

            // The role badge the web build pins to a portrait: a target on a nominee, a crown on
            // the Head of Household. It repeats what the chip below already says in words, so a
            // reader who cannot see the shape loses nothing.
            HudPrimitives.AddRoleMark(rim, MarkFor(standing.Badge), ring);
            // The mood face takes the opposite shoulder, so the two marks can never collide.
            MoodFace(rim, actor.mood, moodColour, ring);

            // A circular mask over the portrait render. The face texture is square, so without this
            // the cast reads as a row of tiles rather than as a row of people.
            var frame = Disc("Frame", entry, standing.Dim ? new Color(1f, 1f, 1f, .45f) : Color.white);
            frame.anchorMin = new Vector2(.5f, 1f); frame.anchorMax = new Vector2(.5f, 1f); frame.pivot = new Vector2(.5f, 1f);
            frame.anchoredPosition = new Vector2(0f, -(top + RingPadding * scale));
            frame.sizeDelta = new Vector2(Portrait * scale, Portrait * scale);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var face = portrait != null ? portrait(actor.id) : null;
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
                raw.color = standing.Dim ? new Color(.6f, .65f, .7f, 1f) : Color.white;
            }
            else
            {
                // No authored art for this persona: the initial on the surface tone. Still a
                // face-shaped slot, so a missing portrait cannot knock the rail out of alignment.
                var fill = Disc("Initial", frame, UiTheme.SurfaceRaised);
                fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
                fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
                var glyph = Label(frame, string.IsNullOrEmpty(actor.name) ? "?" : actor.name.Substring(0, 1),
                    19, standing.Dim ? UiTheme.Muted : UiTheme.Paper, scale, font, TextAlignmentOptions.Center);
                glyph.rectTransform.anchorMin = Vector2.zero;
                glyph.rectTransform.anchorMax = Vector2.one;
                glyph.rectTransform.offsetMin = Vector2.zero;
                glyph.rectTransform.offsetMax = Vector2.zero;
            }

            // Given name only. Surnames double the width of the rail, and a player refers to these
            // people the way the house does.
            string given = actor.name ?? string.Empty;
            int nameSpace = given.IndexOf(' ');
            if (nameSpace > 0) given = given.Substring(0, nameSpace);

            var name = Label(entry, given, 12, standing.Dim ? UiTheme.Muted : UiTheme.Paper, scale, font, TextAlignmentOptions.Top);
            name.rectTransform.anchorMin = new Vector2(0f, 1f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.pivot = new Vector2(.5f, 1f);
            name.rectTransform.anchoredPosition = new Vector2(0f, -(top + ring + 6f * scale));
            name.rectTransform.sizeDelta = new Vector2(-8f, 16f * scale);

            // The mood in a word, in the mood's colour, under the name. The mockups put it exactly
            // here, and it is the half of the mood a screen reader can actually read out.
            var mood = Label(entry, MoodWord(actor, standing), 11, moodColour, scale, font, TextAlignmentOptions.Top);
            mood.name = MoodWordName;
            mood.rectTransform.anchorMin = new Vector2(0f, 1f);
            mood.rectTransform.anchorMax = new Vector2(1f, 1f);
            mood.rectTransform.pivot = new Vector2(.5f, 1f);
            mood.rectTransform.anchoredPosition = new Vector2(0f, -(top + ring + 23f * scale));
            mood.rectTransform.sizeDelta = new Vector2(-8f, 14f * scale);

            if (!string.IsNullOrEmpty(standing.Badge))
            {
                // The badge sits over the bottom of the rim rather than beside it, so the rail stays
                // one column wide however many people are holding something this week.
                var badgeChip = new GameObject(BadgeName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                badgeChip.SetParent(entry, false);
                badgeChip.anchorMin = new Vector2(.5f, 1f); badgeChip.anchorMax = new Vector2(.5f, 1f); badgeChip.pivot = new Vector2(.5f, 1f);
                badgeChip.anchoredPosition = new Vector2(0f, -(top + ring - 11f * scale));
                badgeChip.sizeDelta = new Vector2(46f * scale, 15f * scale);
                var chipImage = badgeChip.GetComponent<Image>();
                UiTheme.Style(chipImage, standing.Colour, 4);
                chipImage.raycastTarget = false;

                var badge = Label(badgeChip, standing.Badge, 10, UiTheme.Ink, scale, font, TextAlignmentOptions.Center);
                badge.rectTransform.anchorMin = Vector2.zero;
                badge.rectTransform.anchorMax = Vector2.one;
                badge.rectTransform.offsetMin = Vector2.zero;
                badge.rectTransform.offsetMax = Vector2.zero;
            }

            // A portrait is a button: click it and the camera follows that houseguest. The button
            // carries the name chip as its caption, so the keyboard and a screen reader find it the
            // way they find every other control. The hit graphic is the last child rather than the
            // entry's own Image, so the press wash reads over the glass instead of behind it.
            if (onSelect == null) return entry;
            string id = actor.id;
            var hit = HudPrimitives.Fill("Press", entry, new Color(1f, 1f, 1f, 0f), ChipRadius);
            hit.anchorMin = Vector2.zero; hit.anchorMax = Vector2.one;
            hit.offsetMin = new Vector2(ChipInsetX, ChipBottom * scale);
            hit.offsetMax = new Vector2(-ChipInsetX, -ChipTop * scale);
            var hitImage = hit.GetComponent<Image>();
            hitImage.raycastTarget = true;
            var button = entry.gameObject.AddComponent<Button>();
            button.targetGraphic = hitImage;
            var colours = button.colors;
            colours.normalColor = new Color(1f, 1f, 1f, 0f);
            colours.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
            colours.selectedColor = colours.highlightedColor;
            colours.pressedColor = new Color(1f, 1f, 1f, 0.25f);
            button.colors = colours;
            button.onClick.AddListener(() => onSelect(id));
            return entry;
        }

        /// <summary>
        /// The mood face on the portrait's upper-left shoulder, drawn in the mood's own colour.
        ///
        /// <para>The role mark owns the upper-right, so the two never overlap however a week goes.
        /// A clone that has not generated the glyph set gets a plain coloured pip instead, which is
        /// the same information at lower fidelity rather than an empty square.</para>
        /// </summary>
        private static void MoodFace(RectTransform rim, string mood, Color colour, float diameter)
        {
            if (rim == null) return;
            float size = Mathf.Max(13f, diameter * 0.33f);
            var badge = Disc(MoodGlyphName, rim, UiTheme.Ink);
            badge.anchorMin = new Vector2(0f, 1f);
            badge.anchorMax = new Vector2(0f, 1f);
            badge.pivot = new Vector2(.5f, .5f);
            badge.anchoredPosition = new Vector2(size * 0.28f, -size * 0.28f);
            badge.sizeDelta = new Vector2(size, size);

            var glyph = UiTheme.Icon(RelationshipWeb.MoodIcon(mood)) ?? UiTheme.Icon("mood-neutral");
            if (glyph == null)
            {
                var pip = Disc("Face", badge, colour);
                pip.anchorMin = new Vector2(.5f, .5f); pip.anchorMax = new Vector2(.5f, .5f);
                pip.pivot = new Vector2(.5f, .5f);
                pip.anchoredPosition = Vector2.zero;
                pip.sizeDelta = new Vector2(size * .55f, size * .55f);
                return;
            }

            var art = new GameObject("Face", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            art.SetParent(badge, false);
            art.anchorMin = new Vector2(.5f, .5f); art.anchorMax = new Vector2(.5f, .5f);
            art.pivot = new Vector2(.5f, .5f);
            art.anchoredPosition = Vector2.zero;
            art.sizeDelta = new Vector2(size * .82f, size * .82f);
            var image = art.GetComponent<Image>();
            image.sprite = glyph;
            image.color = colour;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private static RectTransform Disc(string name, Transform parent, Color colour)
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

        private static TMP_Text Label(
            Transform parent, string value, int size, Color colour, float scale, TMP_FontAsset font,
            TextAlignmentOptions alignment)
        {
            var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(parent, false);
            if (font != null) text.font = font;
            text.fontSize = Mathf.RoundToInt(size * scale);
            text.color = colour;
            text.text = value;
            text.richText = false;
            text.raycastTarget = false;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }
    }
}
