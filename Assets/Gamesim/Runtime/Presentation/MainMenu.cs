using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The front door: continue a season, start a new one, open settings, or leave.
    ///
    /// <para>The project had no way in. You pressed Play on a scene and were standing in the house
    /// mid-season, which is the one part of the reference build's flow that had no counterpart here
    /// at all — everything else was a screen that existed and looked wrong, while this was a screen
    /// that did not exist.</para>
    ///
    /// <para>It is an overlay rather than a scene. A separate menu scene would mean loading and
    /// unloading the house around it, and the house is the expensive thing to build; opening in
    /// front of a house that is already standing costs nothing and cannot strand the player in a
    /// scene the episode director does not run.</para>
    ///
    /// <para><b>Continue is offered only when there is something to continue.</b> A fresh install
    /// has a save path but no file, and a button that loads nothing is worse than one that is not
    /// there — so the caller decides whether it is offered, from the disk rather than from the fact
    /// that a season object exists in memory.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenu : MonoBehaviour
    {
        private const float Width = 560f;

        /// <summary>Captions. Tests and screen readers identify these controls by their words.</summary>
        public const string ContinueCaption = "Continue your season";
        public const string NewSeasonCaption = "Start a new season";
        public const string SettingsCaption = "Settings and saves";
        public const string QuitCaption = "Quit the game";

        private CanvasGroup group;
        private CanvasScaler scaler;

        public bool IsShowing => group != null && group.alpha > 0f;

        /// <summary>
        /// The "larger text" setting, applied by magnifying the whole screen. The layout is fixed in
        /// reference pixels, so growing the type alone would push it out of boxes that did not grow.
        /// </summary>
        public float FontScale
        {
            set
            {
                if (scaler == null) return;
                float scale = Mathf.Clamp(value, 0.5f, 2f);
                scaler.referenceResolution = new Vector2(1920f / scale, 1080f / scale);
            }
        }

        /// <summary>Above the cast screen, which it opens; nothing is above this.</summary>
        public static MainMenu Attach(GameObject owner)
        {
            var root = new GameObject("Gamesim Main Menu",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(owner.transform, false);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 130;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var menu = root.AddComponent<MainMenu>();
            menu.group = root.GetComponent<CanvasGroup>();
            menu.scaler = scaler;
            menu.Hide();
            return menu;
        }

        public void Hide()
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        /// <summary>
        /// Opens the menu. <paramref name="canContinue"/> decides whether the first control is
        /// offered at all; <paramref name="note"/> is the one line under the title, which is where
        /// a failed load or a retained slot gets explained.
        /// </summary>
        public void Show(bool canContinue, string note,
            Action onContinue, Action onNewSeason, Action onSettings, Action onQuit,
            string career = null)
        {
            Rebuild(canContinue, note, onContinue, onNewSeason, onSettings, onQuit, career);
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        private void Rebuild(bool canContinue, string note,
            Action onContinue, Action onNewSeason, Action onSettings, Action onQuit, string career)
        {
            // Deactivated before Destroy, which runs at the end of the frame: otherwise a rebuild
            // leaves the previous controls live alongside the new ones for a frame.
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            var scrim = HudPrimitives.Fill("Scrim", transform, new Color(0.02f, 0.04f, 0.06f, 0.98f), 1);
            // Fill makes non-interactive art, and a modal's scrim is the exception: without this
            // the gameplay HUD underneath stays clickable straight through the menu.
            scrim.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            scrim.anchorMin = Vector2.zero;
            scrim.anchorMax = Vector2.one;
            scrim.sizeDelta = Vector2.zero;
            scrim.anchoredPosition = Vector2.zero;
            // The mockups' ground: the pack's night navy rather than one flat colour, lit from
            // behind the title and darkened at the edges, the way the cast screen is.
            var night = UiTheme.Pack(PackArt.BackgroundNavy);
            if (night != null)
            {
                var ground = scrim.GetComponent<Image>();
                ground.sprite = night; ground.type = Image.Type.Simple; ground.color = Color.white;
            }
            CornerLight(scrim, PackArt.GlowPurple, new Vector2(0f, 0f), new Vector2(1100f, 800f), .45f);
            CornerLight(scrim, PackArt.GlowCyan, new Vector2(1f, 1f), new Vector2(1000f, 700f), .30f);
            HudPrimitives.Vignette(scrim);

            var column = new GameObject("Column", typeof(RectTransform)).GetComponent<RectTransform>();
            column.SetParent(scrim, false);
            column.anchorMin = new Vector2(0.5f, 0.5f);
            column.anchorMax = new Vector2(0.5f, 0.5f);
            column.pivot = new Vector2(0.5f, 0.5f);
            column.sizeDelta = new Vector2(CardWidth, 640f);
            column.anchoredPosition = Vector2.zero;

            float cursor = 0f;

            // A pool of light behind the name, as the HUD's brand is lit on the set's neon.
            var halo = new GameObject("Title glow", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            halo.rectTransform.SetParent(column, false);
            Place(halo.rectTransform, 820f, 300f, 60f);
            halo.sprite = UiTheme.Pack(PackArt.GlowCyan);
            halo.color = halo.sprite != null ? new Color(1f, 1f, 1f, .35f) : Color.clear;
            halo.raycastTarget = false;

            // The brand as the HUD's top bar draws it, at the size of a title: the house mark,
            // then the name in the title blue, lit from above. It said BIG BROTHER in gold here and
            // on the opening card while every other surface - the HUD, the mockups, the neon on the
            // set - said GAMESIM; and gold is the colour of power in this house, which a title is not.
            var brand = new GameObject("Brand", typeof(RectTransform)).GetComponent<RectTransform>();
            brand.SetParent(column, false);
            Place(brand, CardWidth, 96f, -cursor);
            var word = HudPrimitives.Label("Text", brand, 76f, Color.white, TextAlignmentOptions.MidlineLeft);
            word.text = Localisation.Text("GAMESIM");
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) word.font = bold;
            word.characterSpacing = 4f;
            word.enableVertexGradient = true;
            word.colorGradient = new VertexGradient(UiTheme.Hex("6CC0FF"), UiTheme.Hex("6CC0FF"), UiTheme.Hex("3A86FF"), UiTheme.Hex("3A86FF"));
            word.textWrappingMode = TextWrappingModes.NoWrap;
            float wordWidth = Mathf.Ceil(word.GetPreferredValues(word.text).x) + 4f;
            float markSide = 86f, gap = 18f;
            float left = -(markSide + gap + wordWidth) * .5f;
            var houseMark = UiTheme.Pack(PackArt.IconHome);
            var mark = new GameObject("Mark", typeof(RectTransform)).GetComponent<RectTransform>();
            mark.SetParent(brand, false);
            mark.anchorMin = mark.anchorMax = new Vector2(.5f, .5f);
            mark.pivot = new Vector2(0f, .5f);
            mark.sizeDelta = new Vector2(markSide, markSide);
            mark.anchoredPosition = new Vector2(left, 0f);
            if (houseMark != null || UiTheme.Icon("house") != null)
            {
                var image = mark.gameObject.AddComponent<Image>();
                image.sprite = houseMark != null ? houseMark : UiTheme.Icon("house");
                image.color = UiTheme.Heading;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            else
            {
                var ring = HudPrimitives.Disc("Ring", mark, UiTheme.Heading);
                Centre(ring, markSide, markSide);
                var pupil = HudPrimitives.Disc("Pupil", mark, UiTheme.Ink);
                Centre(pupil, 36f, 36f);
            }
            word.rectTransform.anchorMin = word.rectTransform.anchorMax = new Vector2(.5f, .5f);
            word.rectTransform.pivot = new Vector2(0f, .5f);
            word.rectTransform.sizeDelta = new Vector2(wordWidth, 96f);
            word.rectTransform.anchoredPosition = new Vector2(left + markSide + gap, 0f);
            cursor += 104f;

            // The show is Gamesim: The House, and its name is a lockup wherever it stands - here, on
            // the opening's title card and over its front door: "THE HOUSE" letterspaced under the
            // word and centred on it. The word's own label is untouched; things find it by its text.
            var houseLine = HudPrimitives.Label("Brand line", column, 26f, UiTheme.Accent, TextAlignmentOptions.Center);
            houseLine.text = Localisation.Text("THE HOUSE");
            var lineFont = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (lineFont != null) houseLine.font = lineFont;
            houseLine.characterSpacing = 60f;
            houseLine.textWrappingMode = TextWrappingModes.NoWrap;
            float lineWidth = Mathf.Max(wordWidth, Mathf.Ceil(houseLine.GetPreferredValues(houseLine.text).x) + 8f);
            // Tucked 10 px up into the gap under the brand's box, so its capitals sit close under the word's.
            Place(houseLine.rectTransform, lineWidth, 36f, -(cursor - 10f));
            houseLine.rectTransform.anchoredPosition = new Vector2(left + markSide + gap + wordWidth * .5f, -(cursor - 10f));
            cursor += 30f;

            // The show's line, letterspaced as the HUD's tagline is, and the promise under it.
            var strap = Text(column, "GOOD PEOPLE  \u00b7  BIGGER STORIES", 15f, UiTheme.Heading, 26f, ref cursor);
            strap.characterSpacing = 10f;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) strap.font = semibold;
            var promise = Text(column, "A house, a vote, and everyone watching.", 19f, UiTheme.Paper, 34f, ref cursor);
            promise.fontStyle = FontStyles.Italic;
            if (!string.IsNullOrEmpty(note)) Text(column, note, 14f, UiTheme.Warning, 40f, ref cursor);
            cursor += 22f;

            // The controls on one glass card, the pack's resting panel, as every mockup gathers
            // its choices.
            int count = (canContinue ? 1 : 0) + 3;
            float cardHeight = CardPad * 2f + count * ButtonHeight + (count - 1) * ButtonGap;
            var card = HudPrimitives.Fill("Menu card", column, UiTheme.GlassFill, 16);
            Place(card, CardWidth, cardHeight, -cursor);
            if (!UiTheme.PackSliced(card.GetComponent<Image>(), PackArt.PanelResting, 20f))
                UiTheme.AddBorder(card, 16, UiTheme.Outline);
            float inner = CardPad;
            if (canContinue) Button(card, ContinueCaption, true, "house", onContinue, ref inner);
            Button(card, NewSeasonCaption, !canContinue, "people", onNewSeason, ref inner);
            Button(card, SettingsCaption, false, "settings", onSettings, ref inner);
            Button(card, QuitCaption, false, "exit", onQuit, ref inner);
            cursor += cardHeight + 18f;

            // The career line, once there is a career: what the seasons so far add up to. It
            // sits under the buttons rather than the title because it is a fact about the
            // player, not a note about this launch.
            if (!string.IsNullOrEmpty(career)) Text(column, career, 15f, UiTheme.Accent, 26f, ref cursor);

            cursor += 6f;
            Text(column, "Plays offline. No account, no connection, and nothing is uploaded.",
                13f, UiTheme.Muted, 24f, ref cursor);

            column.sizeDelta = new Vector2(CardWidth, cursor);
        }

        // ---------------------------------------------------------------- pieces

        private const float CardWidth = 600f;
        private const float CardPad = 26f;
        private const float ButtonHeight = 58f;
        private const float ButtonGap = 12f;

        /// <summary>A soft pool of pack light in a corner of the ground.</summary>
        internal static void CornerLight(Transform parent, string path, Vector2 corner, Vector2 size, float alpha)
        {
            var sprite = UiTheme.Pack(path);
            if (sprite == null) return;
            var rect = new GameObject("Light", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = corner;
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            var image = rect.GetComponent<Image>();
            image.sprite = sprite; image.color = new Color(1f, 1f, 1f, alpha); image.raycastTarget = false;
        }

        /// <summary>
        /// One of the menu's controls: the pack's button art - the lit primary for the one the
        /// player most likely wants, the quiet secondary for the rest - its glyph, and the caption
        /// it is known by, centred together. The primary's art carries its glow inside the slice,
        /// so it is drawn on a child that overhangs the control by the glow's width and the lit
        /// edge lands on the control's own bounds.
        /// </summary>
        private static void Button(Transform parent, string caption, bool primary, string glyph, Action action, ref float cursor)
        {
            float width = CardWidth - CardPad * 2f;
            var panel = HudPrimitives.Fill(caption, parent, primary ? UiTheme.ActionBlue : UiTheme.SurfaceRaised, 10);
            Place(panel, width, ButtonHeight, -cursor);
            var ground = panel.GetComponent<Image>();
            ground.raycastTarget = true;

            var art = new GameObject("Art", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            art.rectTransform.SetParent(panel, false);
            float overhang = primary ? 12f : 4f;
            art.rectTransform.anchorMin = Vector2.zero; art.rectTransform.anchorMax = Vector2.one;
            art.rectTransform.offsetMin = new Vector2(-overhang, -overhang);
            art.rectTransform.offsetMax = new Vector2(overhang, overhang);
            art.raycastTarget = false;
            if (UiTheme.PackSliced(art, primary ? PackArt.ButtonPrimary : PackArt.ButtonSecondary, primary ? 24f : 16f))
                ground.color = new Color(0f, 0f, 0f, 0f);
            else
            {
                art.enabled = false;
                UiTheme.AddBorder(panel, 10, primary ? UiTheme.Accent : UiTheme.Outline);
            }

            var label = HudPrimitives.Label("Label", panel, 20f, primary ? Color.white : UiTheme.Paper, TextAlignmentOptions.Center);
            label.text = Localisation.Text(caption);
            var weight = UiTheme.Font(primary ? UiTheme.Weight.SemiBold : UiTheme.Weight.Medium);
            if (weight != null) label.font = weight;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12f, 0f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);

            // The glyph stands just left of the words, the pair centred as one.
            var sprite = UiTheme.Icon(glyph);
            if (sprite != null)
            {
                float words = Mathf.Min(width - 80f, label.GetPreferredValues(label.text).x);
                const float side = 24f, space = 12f;
                label.rectTransform.offsetMin = new Vector2(12f + (side + space) * .5f, 0f);
                label.rectTransform.offsetMax = new Vector2(-12f + (side + space) * .5f, 0f);
                var icon = new GameObject("Glyph", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                icon.rectTransform.SetParent(panel, false);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(.5f, .5f);
                icon.rectTransform.pivot = new Vector2(.5f, .5f);
                icon.rectTransform.sizeDelta = new Vector2(side, side);
                icon.rectTransform.anchoredPosition = new Vector2(-(words + side + space) * .5f + side * .5f, 0f);
                icon.sprite = sprite; icon.preserveAspect = true; icon.raycastTarget = false;
                icon.color = primary ? Color.white : UiTheme.Heading;
            }

            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = art.enabled ? art : ground;
            var colours = button.colors;
            colours.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            colours.selectedColor = colours.highlightedColor;
            colours.pressedColor = new Color(.85f, .85f, .85f);
            button.colors = colours;
            if (action != null) button.onClick.AddListener(() => action());
            cursor += ButtonHeight + ButtonGap;
        }

        private static TMP_Text Text(Transform parent, string value, float size, Color colour,
            float height, ref float cursor)
        {
            var label = HudPrimitives.Label("Text", parent, size, colour, TextAlignmentOptions.Center);
            label.text = Localisation.Text(value);
            Place(label.rectTransform, Width, height, -cursor);
            cursor += height;
            return label;
        }

        private static void Place(RectTransform rect, float width, float height, float y)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(0f, y);
        }

        private static void Centre(RectTransform rect, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
