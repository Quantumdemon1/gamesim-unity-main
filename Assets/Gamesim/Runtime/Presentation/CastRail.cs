using System.Collections.Generic;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The permanent cast strip down the left edge: every houseguest's face, name and standing,
    /// visible in every frame of the episode.
    ///
    /// <para>Before this, the cast was legible only as 1.4%-of-frame silhouettes on the set, and the
    /// HUD named people in prose — so "Jamie Roberts is the replacement nominee" asked the player to
    /// remember which of six tiny figures that was. A broadcast never does that: the faces are
    /// always on screen and the badges carry the state. This is the single change that most affects
    /// how every other frame reads, which is why it is permanent chrome rather than a panel someone
    /// has to open.</para>
    ///
    /// <para>Rebuilt from committed state on each HUD render and never animated per frame: the rail
    /// shows what the simulation holds and holds no opinion of its own.</para>
    /// </summary>
    public static class CastRail
    {
        public const string RootName = "Cast rail";
        public const float Width = 92f;

        private const float Portrait = 54f;
        private const float RingPadding = 4f;
        private const float EntryHeight = 96f;

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
            System.Func<string, Texture> portrait)
        {
            var root = new GameObject(RootName, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(14f, -24f);

            var order = Order(state);
            root.sizeDelta = new Vector2(Width, order.Count * EntryHeight * fontScale);

            for (int index = 0; index < order.Count; index++)
            {
                Entry(root, state, order[index], index, fontScale, font, portrait);
            }
            return root;
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

        private static Standing Read(EpisodeState state, ContestantState actor)
        {
            if (actor.status == ContestantStatus.Winner) return new Standing("WINNER", UiTheme.Gold, false);
            if (actor.status == ContestantStatus.RunnerUp) return new Standing("FINAL 2", UiTheme.Accent, false);
            if (actor.status != ContestantStatus.Active) return new Standing("OUT", UiTheme.Muted, true);
            if (actor.id == state.hohId) return new Standing("HOH", UiTheme.Gold, false);
            if (actor.id == state.vetoHolderId) return new Standing("VETO", UiTheme.Gold, false);
            if (state.nominees != null && state.nominees.Contains(actor.id)) return new Standing("NOM", UiTheme.Danger, false);
            if (actor.isPlayer || actor.id == state.playerId) return new Standing("YOU", UiTheme.Accent, false);
            return new Standing(null, UiTheme.Outline, false);
        }

        private static void Entry(
            RectTransform root, EpisodeState state, ContestantState actor, int index, float scale,
            TMP_FontAsset font, System.Func<string, Texture> portrait)
        {
            var standing = Read(state, actor);

            var entry = new GameObject(actor.name, typeof(RectTransform)).GetComponent<RectTransform>();
            entry.SetParent(root, false);
            entry.anchorMin = new Vector2(0f, 1f);
            entry.anchorMax = new Vector2(0f, 1f);
            entry.pivot = new Vector2(0f, 1f);
            entry.anchoredPosition = new Vector2(0f, -index * EntryHeight * scale);
            entry.sizeDelta = new Vector2(Width, EntryHeight * scale);

            // The ring is the status: a coloured disc showing through as a rim around the face.
            float ring = (Portrait + RingPadding * 2f) * scale;
            var rim = Disc("Ring", entry, standing.Colour);
            rim.anchorMin = new Vector2(.5f, 1f); rim.anchorMax = new Vector2(.5f, 1f); rim.pivot = new Vector2(.5f, 1f);
            rim.anchoredPosition = Vector2.zero;
            rim.sizeDelta = new Vector2(ring, ring);

            // A circular mask over the portrait render. The face texture is square, so without this
            // the cast reads as a row of tiles rather than as a row of people.
            var frame = Disc("Frame", entry, standing.Dim ? new Color(1f, 1f, 1f, .45f) : Color.white);
            frame.anchorMin = new Vector2(.5f, 1f); frame.anchorMax = new Vector2(.5f, 1f); frame.pivot = new Vector2(.5f, 1f);
            frame.anchoredPosition = new Vector2(0f, -RingPadding * scale);
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
                    22, standing.Dim ? UiTheme.Muted : UiTheme.Paper, scale, font, TextAlignmentOptions.Center);
                glyph.rectTransform.anchorMin = Vector2.zero;
                glyph.rectTransform.anchorMax = Vector2.one;
                glyph.rectTransform.offsetMin = Vector2.zero;
                glyph.rectTransform.offsetMax = Vector2.zero;
            }

            // Given name only. Surnames double the width of the rail, and a player refers to these
            // people the way the house does.
            string given = actor.name ?? string.Empty;
            int space = given.IndexOf(' ');
            if (space > 0) given = given.Substring(0, space);

            var name = Label(entry, given, 13, standing.Dim ? UiTheme.Muted : UiTheme.Paper, scale, font, TextAlignmentOptions.Top);
            name.rectTransform.anchorMin = new Vector2(0f, 1f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.pivot = new Vector2(.5f, 1f);
            name.rectTransform.anchoredPosition = new Vector2(0f, -(ring + 12f * scale));
            name.rectTransform.sizeDelta = new Vector2(0f, 17f * scale);

            if (string.IsNullOrEmpty(standing.Badge)) return;

            // The badge sits over the bottom of the rim rather than beside it, so the rail stays one
            // column wide however many people are holding something this week.
            var chip = new GameObject("Badge", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            chip.SetParent(entry, false);
            chip.anchorMin = new Vector2(.5f, 1f); chip.anchorMax = new Vector2(.5f, 1f); chip.pivot = new Vector2(.5f, 1f);
            chip.anchoredPosition = new Vector2(0f, -(ring - 8f * scale));
            chip.sizeDelta = new Vector2(46f * scale, 17f * scale);
            var chipImage = chip.GetComponent<Image>();
            UiTheme.Style(chipImage, standing.Colour, 4);
            chipImage.raycastTarget = false;

            var badge = Label(chip, standing.Badge, 10, UiTheme.Ink, scale, font, TextAlignmentOptions.Center);
            badge.rectTransform.anchorMin = Vector2.zero;
            badge.rectTransform.anchorMax = Vector2.one;
            badge.rectTransform.offsetMin = Vector2.zero;
            badge.rectTransform.offsetMax = Vector2.zero;
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
