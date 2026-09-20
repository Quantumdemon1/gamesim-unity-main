using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The competition result: who took the power, and the ranked standings behind it.
    ///
    /// <para>The engine already scores every competitor and keeps those numbers in committed state,
    /// but the HUD reported one line — "Competition winner: Maya Hassan · Skill." — and threw the
    /// rest away. A player who lost had no way to know whether they lost by a hair or were never in
    /// it, which is the difference between a competition and a coin toss. The standings are the
    /// whole point: they are what makes the next nomination feel earned or arbitrary.</para>
    ///
    /// <para>Bars are relative to the top score, not absolute, because the scales differ by
    /// competition type and an absolute bar would be unreadable in one and saturated in another.</para>
    ///
    /// <para>Same rules as the other overlays: nothing raycasts, and it ends on a timer.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompetitionResult : MonoBehaviour
    {
        private const float FadeIn = 0.30f;
        private const float Hold = 2.9f;
        private const float FadeOut = 0.45f;
        private const float Rise = 24f;

        /// <summary>One competitor's line in the standings.</summary>
        public readonly struct Standing
        {
            public readonly string Name;
            public readonly double Score;
            public readonly bool IsWinner;
            public readonly bool IsPlayer;
            public readonly Texture Portrait;

            public Standing(string name, double score, bool isWinner, bool isPlayer, Texture portrait)
            {
                Name = name; Score = score; IsWinner = isWinner; IsPlayer = isPlayer; Portrait = portrait;
            }
        }

        private CanvasGroup group;
        private RectTransform column, scrim;
        private float elapsed;
        private bool playing, reduced;

        public float FontScale { get; set; } = 1f;
        public bool IsPlaying => playing;

        public static CompetitionResult Attach(GameObject owner)
        {
            var root = new GameObject("Gamesim Competition Result",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            if (owner != null && owner.scene.IsValid()) SceneManager.MoveGameObjectToScene(root, owner.scene);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 105;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            return root.AddComponent<CompetitionResult>();
        }

        /// <summary>
        /// Plays the card. Declines an empty field rather than drawing an empty board, so a
        /// competition the engine scored differently degrades to the status line instead of to a
        /// blank takeover.
        /// </summary>
        public bool Play(string award, string category, int week, IList<Standing> standings, bool reducedMotion)
        {
            if (standings == null || standings.Count == 0) return false;

            Build(award, category, week, standings);
            reduced = reducedMotion;
            elapsed = 0f;
            playing = true;

            column.gameObject.SetActive(true);
            scrim.gameObject.SetActive(true);
            Apply(reduced ? 1f : 0f);
            return true;
        }

        public void Cancel()
        {
            playing = false;
            if (group != null) group.alpha = 0f;
            if (column != null) column.gameObject.SetActive(false);
            if (scrim != null) scrim.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!playing) return;
            elapsed += Time.unscaledDeltaTime;

            if (reduced)
            {
                if (elapsed >= FadeIn + Hold + FadeOut) { Cancel(); return; }
                Apply(1f);
                return;
            }
            if (elapsed < FadeIn) { Apply(elapsed / FadeIn); return; }
            if (elapsed < FadeIn + Hold) { Apply(1f); return; }

            float exit = (elapsed - FadeIn - Hold) / FadeOut;
            if (exit >= 1f) { Cancel(); return; }
            Apply(1f - exit);
        }

        private void Apply(float t)
        {
            float eased = 1f - (1f - t) * (1f - t);
            group.alpha = eased;
            column.anchoredPosition = new Vector2(0f, (1f - eased) * -Rise);
        }

        private void Build(string award, string category, int week, IList<Standing> standings)
        {
            var root = (RectTransform)transform;
            if (column != null)
            {
                foreach (Transform child in column) Destroy(child.gameObject);
            }
            else
            {
                group = GetComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;

                // Near-opaque, matching the web build and the other phase cards.
                scrim = HudPrimitives.Fill("Scrim", root,
                    new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.975f), 1);
                scrim.anchorMin = Vector2.zero; scrim.anchorMax = Vector2.one;
                scrim.offsetMin = Vector2.zero; scrim.offsetMax = Vector2.zero;

                column = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>();
                column.SetParent(root, false);
                column.anchorMin = new Vector2(.5f, .5f);
                column.anchorMax = new Vector2(.5f, .5f);
                column.pivot = new Vector2(.5f, .5f);
            }

            float scale = Mathf.Max(0.5f, FontScale);
            const float width = 760f;
            float rowHeight = 34f * scale;
            float y = 0f;
            CardGlass(column, scale);

            var eyebrow = HudPrimitives.Label("Week", column, 15f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            eyebrow.text = "WEEK " + Mathf.Max(1, week);
            eyebrow.characterSpacing = 14f;
            Place(eyebrow.rectTransform, width * scale, 22f * scale, ref y);
            y -= 8f * scale;

            var title = HudPrimitives.Label("Award", column, 40f * scale, UiTheme.Paper, TextAlignmentOptions.Center);
            title.text = (award ?? "COMPETITION").ToUpperInvariant();
            Place(title.rectTransform, width * scale, 52f * scale, ref y);
            y -= 10f * scale;

            Standing winner = standings[0];
            for (int i = 0; i < standings.Count; i++) if (standings[i].IsWinner) winner = standings[i];

            // The banner: the result stated once, loudly, before any of the detail. A player who
            // looks away and back should be able to read the outcome without parsing the board.
            // Tinted by the competition's category, not by the award. The reference build colours an
            // endurance banner green and a mental one violet, which is a correction to an earlier
            // pass here that painted every banner violet: two thirds of them were then the wrong
            // colour, and the pill underneath disagreed with the banner above it.
            var categoryTint = CategoryTint(category);
            var banner = HudPrimitives.Fill("Winner banner", column, categoryTint, UiTheme.PanelRadius);
            Place(banner, width * scale, 86f * scale, ref y);
            var bannerInk = UiTheme.OnColor(categoryTint);

            var bannerText = HudPrimitives.Label("Winner banner text", banner, 26f * scale,
                bannerInk, TextAlignmentOptions.Center);
            bannerText.text = winner.IsPlayer ? "You win!" : winner.Name + " wins!";
            bannerText.rectTransform.anchorMin = new Vector2(0f, .5f);
            bannerText.rectTransform.anchorMax = new Vector2(1f, .5f);
            bannerText.rectTransform.sizeDelta = new Vector2(0f, 34f * scale);
            bannerText.rectTransform.anchoredPosition = new Vector2(0f, 12f * scale);

            // The category repeated inside the banner, as the web build does, so the headline says
            // what kind of competition was won and not merely that one was.
            if (!string.IsNullOrEmpty(category))
            {
                var bannerSub = HudPrimitives.Label("Winner banner category", banner, 15f * scale,
                    new Color(bannerInk.r, bannerInk.g, bannerInk.b, .85f), TextAlignmentOptions.Center);
                bannerSub.text = category + " competition";
                bannerSub.rectTransform.anchorMin = new Vector2(0f, .5f);
                bannerSub.rectTransform.anchorMax = new Vector2(1f, .5f);
                bannerSub.rectTransform.sizeDelta = new Vector2(0f, 22f * scale);
                bannerSub.rectTransform.anchoredPosition = new Vector2(0f, -16f * scale);
            }
            y -= 12f * scale;

            if (!string.IsNullOrEmpty(category))
            {
                var pill = HudPrimitives.Fill("Category", column, new Color(categoryTint.r, categoryTint.g, categoryTint.b, .22f), 10);
                Place(pill, 132f * scale, 24f * scale, ref y);
                var pillText = HudPrimitives.Label("Category text", pill, 13f * scale, categoryTint, TextAlignmentOptions.Center);
                pillText.text = category.ToUpperInvariant();
                pillText.characterSpacing = 6f;
                pillText.rectTransform.anchorMin = Vector2.zero;
                pillText.rectTransform.anchorMax = Vector2.one;
                pillText.rectTransform.offsetMin = Vector2.zero;
                pillText.rectTransform.offsetMax = Vector2.zero;
                y -= 10f * scale;
            }

            float portrait = 92f * scale;
            var rim = HudPrimitives.Portrait(column, winner.Portrait, UiTheme.Gold, portrait, 5f * scale, false);
            rim.anchorMin = new Vector2(.5f, 1f); rim.anchorMax = new Vector2(.5f, 1f); rim.pivot = new Vector2(.5f, 1f);
            rim.anchoredPosition = new Vector2(0f, y);
            y -= portrait + 14f * scale;

            var name = HudPrimitives.Label("Winner", column, 34f * scale, UiTheme.Gold, TextAlignmentOptions.Center);
            name.text = winner.Name;
            Place(name.rectTransform, width * scale, 44f * scale, ref y);

            var strap = HudPrimitives.Label("Strap", column, 17f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            strap.text = winner.IsPlayer ? "You won it." : "Wins the competition.";
            Place(strap.rectTransform, width * scale, 24f * scale, ref y);
            y -= 14f * scale;

            var heading = HudPrimitives.Label("Standings heading", column, 13f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            heading.text = "COMPETITION STANDINGS";
            heading.characterSpacing = 10f;
            Place(heading.rectTransform, width * scale, 20f * scale, ref y);
            y -= 4f * scale;

            // Relative to the leader, so the shape of the result reads the same whichever scoring
            // scale this competition type used.
            double best = 0d;
            foreach (var entry in standings) if (entry.Score > best) best = entry.Score;
            if (best <= 0d) best = 1d;

            for (int i = 0; i < standings.Count; i++)
            {
                Row(column, standings[i], i + 1, best, width * scale, rowHeight, scale, ref y);
            }

            column.sizeDelta = new Vector2(width * scale, -y);
        }

        private static void Row(RectTransform parent, Standing entry, int rank, double best,
            float width, float height, float scale, ref float y)
        {
            var row = HudPrimitives.Fill("Standing", parent,
                entry.IsWinner ? new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .16f) : UiTheme.Surface,
                UiTheme.ControlRadius);
            Place(row, width, height - 5f * scale, ref y);
            y -= 5f * scale; // the gap between rows, so Place stays a pure stacker

            var colour = entry.IsWinner ? UiTheme.Gold : UiTheme.Paper;

            var place = HudPrimitives.Label("Rank", row, 14f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            place.text = rank.ToString();
            Anchor(place.rectTransform, new Vector2(10f * scale, 0f), new Vector2(24f * scale, height - 12f * scale));

            var who = HudPrimitives.Label("Name", row, 16f * scale, colour, TextAlignmentOptions.Left);
            who.text = entry.IsPlayer ? entry.Name + "  (You)" : entry.Name;
            Anchor(who.rectTransform, new Vector2(42f * scale, 0f), new Vector2(210f * scale, height - 12f * scale));

            // The bar track, then the filled portion.
            float trackX = 262f * scale;
            float trackW = width - trackX - 74f * scale;
            var track = HudPrimitives.Fill("Track", row, UiTheme.Ink, 3);
            Anchor(track, new Vector2(trackX, 0f), new Vector2(trackW, 8f * scale));

            float fraction = best <= 0d ? 0f : Mathf.Clamp01((float)(entry.Score / best));
            var fill = HudPrimitives.Fill("Bar", row, colour, 3);
            fill.anchorMin = new Vector2(0f, .5f); fill.anchorMax = new Vector2(0f, .5f); fill.pivot = new Vector2(0f, .5f);
            fill.anchoredPosition = new Vector2(trackX, 0f);
            fill.sizeDelta = new Vector2(Mathf.Max(2f, trackW * fraction), 8f * scale);

            var score = HudPrimitives.Label("Score", row, 15f * scale, colour, TextAlignmentOptions.Right);
            score.text = entry.Score.ToString("0.0");
            Anchor(score.rectTransform, new Vector2(width - 66f * scale, 0f), new Vector2(56f * scale, height - 12f * scale));
        }

        /// <summary>
        /// The mockups' glass ground behind the card's column (VISUAL-TARGET.md §4, mockup-08 and
        /// -10): the night background at 85 %, a cyan hairline on the edge and a soft glow outside
        /// it. Built as the column's first child so every standing draws over it, and stretched to
        /// the column, whose height is only known once the board has been laid out.
        /// </summary>
        private static RectTransform CardGlass(RectTransform column, float scale)
        {
            var glass = HudPrimitives.Fill("Card glass", column, UiTheme.GlassFill, UiTheme.GlassRadius);
            glass.SetAsFirstSibling();
            glass.anchorMin = Vector2.zero;
            glass.anchorMax = Vector2.one;
            glass.offsetMin = new Vector2(-34f * scale, -26f * scale);
            glass.offsetMax = new Vector2(34f * scale, 26f * scale);
            UiTheme.Glass(glass, UiTheme.GlassRadius);
            return glass;
        }

        /// <summary>Left-anchored, vertically centred inside a row.</summary>
        private static void Anchor(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, .5f);
            rect.anchorMax = new Vector2(0f, .5f);
            rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Place(RectTransform rect, float width, float height, ref float y)
        {
            rect.anchorMin = new Vector2(.5f, 1f);
            rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(width, height);
            y -= height;
        }
        /// <summary>
        /// The colour a competition category wears. Endurance is green and mental is violet in the
        /// reference build; skill takes the room-outline blue, which is the remaining accent and the
        /// one this project already uses for a competition in progress.
        /// </summary>
        private static Color CategoryTint(string category)
        {
            if (string.IsNullOrEmpty(category)) return UiTheme.Award;
            switch (category.Trim().ToLowerInvariant())
            {
                case "endurance": return UiTheme.PositiveDeep;
                case "mental": return UiTheme.Award;
                case "skill": return UiTheme.AccentDeep;
                default: return UiTheme.Award;
            }
        }


    }
}
