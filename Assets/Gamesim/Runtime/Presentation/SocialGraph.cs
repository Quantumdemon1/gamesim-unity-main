using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The house drawn as a network: the player at the centre, everyone else around them, and an
    /// edge per relationship coloured by standing and weighted by strength.
    ///
    /// <para>The notebook already listed the same numbers — "Maya Hassan · Active · Your trust 0" —
    /// and a list is the wrong shape for this question. A player asking "where do I stand" wants to
    /// see the whole board at once: who is close, who is hostile, and how lopsided it is. Six rows of
    /// text make that a reading exercise; a graph makes it a glance.</para>
    ///
    /// <para>Strictly the player's own perspective. The engine stores relationships directionally and
    /// the two directions routinely disagree, so drawing the outbound score as though it were mutual
    /// would invent agreement that the simulation deliberately withholds. Every edge here is what the
    /// player's character believes, which is exactly what the notebook promises.</para>
    /// </summary>
    public static class SocialGraph
    {
        public const string RootName = "Social graph";

        private const float Height = 320f;
        private const float Radius = 116f;
        private const float NodeSize = 46f;
        private const float PlayerSize = 62f;

        /// <summary>
        /// Builds the graph into <paramref name="parent"/> at a fixed height, sized for the panel's
        /// scroll rather than for the screen.
        /// </summary>
        public static RectTransform Build(
            Transform parent, Simulation.EpisodeState state, float scale, TMP_FontAsset font,
            System.Func<string, Texture> portrait)
        {
            var root = new GameObject(RootName, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            var element = root.gameObject.AddComponent<LayoutElement>();
            element.minHeight = Height * scale;
            element.preferredHeight = Height * scale;

            var others = new List<Simulation.ContestantState>();
            foreach (var actor in state.contestants)
            {
                if (actor.id == state.playerId || actor.isPlayer) continue;
                if (actor.status != Simulation.ContestantStatus.Active) continue;
                others.Add(actor);
            }

            Legend(root, scale, font);

            // The centre sits below the legend rather than in the middle of the box, so the ring has
            // room underneath for the names.
            var centre = new Vector2(0f, -28f * scale);

            // Edges first, so nodes draw over them.
            for (int i = 0; i < others.Count; i++)
            {
                var position = Ring(i, others.Count, scale, centre);
                double score = state.Score(state.playerId, others[i].id);
                Edge(root, centre, position, score, scale);
            }

            for (int i = 0; i < others.Count; i++)
            {
                var position = Ring(i, others.Count, scale, centre);
                Node(root, state, others[i], position, NodeSize * scale, scale, font, portrait, false);
            }

            var player = state.Find(state.playerId);
            if (player != null)
                Node(root, state, player, centre, PlayerSize * scale, scale, font, portrait, true);

            return root;
        }

        private static Vector2 Ring(int index, int count, float scale, Vector2 centre)
        {
            if (count <= 0) return centre;
            // Start at the top and go clockwise, so the arrangement is stable between renders and a
            // player can build the same spatial memory the cast rail gives them.
            float angle = Mathf.PI * 0.5f - index * (Mathf.PI * 2f / count);
            return centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (Radius * scale);
        }

        /// <summary>Allied, neutral or hostile, from the player's own outbound score.</summary>
        private static Color Standing(double score)
        {
            if (score >= 15d) return UiTheme.Positive;
            if (score <= -15d) return UiTheme.Danger;
            return UiTheme.Gold;
        }

        private static void Edge(RectTransform root, Vector2 from, Vector2 to, double score, float scale)
        {
            var delta = to - from;
            float length = delta.magnitude;
            if (length <= 0.01f) return;

            // Thickness carries magnitude. Floor of one pixel so a neutral relationship still draws:
            // "no strong feeling" and "no relationship recorded" are different states and the graph
            // should not collapse them into an absent line.
            float weight = Mathf.Lerp(1.5f, 5.5f, Mathf.Clamp01((float)(System.Math.Abs(score) / 60d)));

            var colour = Standing(score);
            var line = HudPrimitives.Fill("Edge", root, new Color(colour.r, colour.g, colour.b, .55f), 1);
            line.anchorMin = new Vector2(.5f, .5f);
            line.anchorMax = new Vector2(.5f, .5f);
            line.pivot = new Vector2(0f, .5f);
            line.anchoredPosition = from;
            line.sizeDelta = new Vector2(length, weight * scale);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private static void Node(
            RectTransform root, Simulation.EpisodeState state, Simulation.ContestantState actor,
            Vector2 position, float size, float scale, TMP_FontAsset font,
            System.Func<string, Texture> portrait, bool isPlayer)
        {
            var ring = isPlayer
                ? UiTheme.Accent
                : Standing(state.Score(state.playerId, actor.id));

            var rim = HudPrimitives.Portrait(root, portrait != null ? portrait(actor.id) : null,
                ring, size, 3f * scale, false);
            rim.anchorMin = new Vector2(.5f, .5f);
            rim.anchorMax = new Vector2(.5f, .5f);
            rim.pivot = new Vector2(.5f, .5f);
            rim.anchoredPosition = position;

            string given = actor.name ?? string.Empty;
            int space = given.IndexOf(' ');
            if (space > 0) given = given.Substring(0, space);

            var label = Label(root, isPlayer ? "YOU" : given, isPlayer ? 12 : 13,
                isPlayer ? UiTheme.Accent : UiTheme.Paper, scale, font);
            label.rectTransform.anchorMin = new Vector2(.5f, .5f);
            label.rectTransform.anchorMax = new Vector2(.5f, .5f);
            label.rectTransform.pivot = new Vector2(.5f, 1f);
            label.rectTransform.anchoredPosition = position + new Vector2(0f, -(size * .5f + 4f * scale));
            label.rectTransform.sizeDelta = new Vector2(96f * scale, 17f * scale);
            label.alignment = TextAlignmentOptions.Top;

            // Standing in the week, in the same vocabulary the cast rail uses.
            string badge = null;
            var tint = UiTheme.Gold;
            if (actor.id == state.hohId) badge = "HOH";
            else if (actor.id == state.vetoHolderId) badge = "VETO";
            else if (state.nominees != null && state.nominees.Contains(actor.id)) { badge = "NOM"; tint = UiTheme.Danger; }
            if (badge == null) return;

            var chip = HudPrimitives.Fill("Badge", root, tint, 4);
            chip.anchorMin = new Vector2(.5f, .5f);
            chip.anchorMax = new Vector2(.5f, .5f);
            chip.pivot = new Vector2(.5f, .5f);
            chip.anchoredPosition = position + new Vector2(0f, size * .5f - 2f * scale);
            chip.sizeDelta = new Vector2(40f * scale, 15f * scale);

            var badgeText = Label(chip, badge, 9, UiTheme.Ink, scale, font);
            badgeText.rectTransform.anchorMin = Vector2.zero;
            badgeText.rectTransform.anchorMax = Vector2.one;
            badgeText.rectTransform.offsetMin = Vector2.zero;
            badgeText.rectTransform.offsetMax = Vector2.zero;
            badgeText.alignment = TextAlignmentOptions.Center;
        }

        /// <summary>
        /// The key. Without it the colours are decoration — a player has no way to know that green
        /// means allied rather than, say, recently spoken to.
        /// </summary>
        private static void Legend(RectTransform root, float scale, TMP_FontAsset font)
        {
            var entries = new[]
            {
                ("Allied", UiTheme.Positive),
                ("Neutral", UiTheme.Gold),
                ("Hostile", UiTheme.Danger),
            };

            float x = 6f * scale;
            foreach (var (caption, colour) in entries)
            {
                var pip = HudPrimitives.Disc("Key", root, colour);
                pip.anchorMin = new Vector2(0f, 1f);
                pip.anchorMax = new Vector2(0f, 1f);
                pip.pivot = new Vector2(0f, 1f);
                pip.anchoredPosition = new Vector2(x, -5f * scale);
                pip.sizeDelta = new Vector2(9f * scale, 9f * scale);

                var text = Label(root, caption, 12, UiTheme.Muted, scale, font);
                text.rectTransform.anchorMin = new Vector2(0f, 1f);
                text.rectTransform.anchorMax = new Vector2(0f, 1f);
                text.rectTransform.pivot = new Vector2(0f, 1f);
                text.rectTransform.anchoredPosition = new Vector2(x + 13f * scale, -1f * scale);
                text.rectTransform.sizeDelta = new Vector2(68f * scale, 16f * scale);
                text.alignment = TextAlignmentOptions.TopLeft;

                x += 88f * scale;
            }

            // A second row for what an edge's weight means, and a third for the role marks a portrait
            // can carry. Both describe things the graph already draws: edge weight is lerped by
            // relationship strength, and the marks are the same sprites the cast rail pins to a face.
            float row = -22f * scale;
            var strength = Label(root, "Strength", 11, UiTheme.Muted, scale, font);
            Place(strength.rectTransform, 6f * scale, row, 58f * scale, 14f * scale);

            foreach (var (caption, weight) in new[] { ("weak", 1.5f), ("strong", 5.5f) })
            {
                var sample = HudPrimitives.Fill("Weight", root, UiTheme.Muted, 1);
                Place(sample, (caption == "weak" ? 66f : 132f) * scale, row - 5f * scale, 22f * scale, weight * scale);
                sample.GetComponent<Image>().raycastTarget = false;
                var text = Label(root, caption, 11, UiTheme.Muted, scale, font);
                Place(text.rectTransform, (caption == "weak" ? 92f : 158f) * scale, row, 44f * scale, 14f * scale);
            }

            float statusX = 210f * scale;
            var status = Label(root, "Status", 11, UiTheme.Muted, scale, font);
            Place(status.rectTransform, statusX, row, 46f * scale, 14f * scale);
            statusX += 44f * scale;

            foreach (var (icon, caption, tint) in new[]
            {
                ("crown", "HoH", UiTheme.Gold),
                ("veto-token", "veto", UiTheme.Gold),
                ("target", "nominee", UiTheme.Danger),
            })
            {
                var glyph = UiTheme.Icon(icon);
                if (glyph != null)
                {
                    var art = new GameObject("Key glyph", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                    art.SetParent(root, false);
                    Place(art, statusX, row - 1f * scale, 12f * scale, 12f * scale);
                    var image = art.GetComponent<Image>();
                    image.sprite = glyph; image.color = tint;
                    image.raycastTarget = false; image.preserveAspect = true;
                }
                else
                {
                    var pip = HudPrimitives.Disc("Key", root, tint);
                    Place(pip, statusX, row - 1f * scale, 10f * scale, 10f * scale);
                }
                var text = Label(root, caption, 11, UiTheme.Muted, scale, font);
                Place(text.rectTransform, statusX + 16f * scale, row, 56f * scale, 14f * scale);
                statusX += 74f * scale;
            }

            var note = Label(root, "Your perspective. Another housemate may feel differently.",
                12, UiTheme.Muted, scale, font);
            note.rectTransform.anchorMin = new Vector2(1f, 1f);
            note.rectTransform.anchorMax = new Vector2(1f, 1f);
            note.rectTransform.pivot = new Vector2(1f, 1f);
            note.rectTransform.anchoredPosition = new Vector2(-6f * scale, -1f * scale);
            note.rectTransform.sizeDelta = new Vector2(330f * scale, 16f * scale);
            note.alignment = TextAlignmentOptions.TopRight;
        }

        /// <summary>Top-left anchored placement, so a legend row reads left to right.</summary>
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
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
