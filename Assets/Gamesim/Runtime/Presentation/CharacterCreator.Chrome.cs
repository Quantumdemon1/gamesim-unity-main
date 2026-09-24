using System;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>Shows a control's focus ring while the keyboard or the pad is on it.</summary>
    internal sealed class CreatorFocus : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public Image Ring;

        public void OnSelect(BaseEventData eventData) { if (Ring != null) Ring.enabled = true; }
        public void OnDeselect(BaseEventData eventData) { if (Ring != null) Ring.enabled = false; }

        private void OnEnable()
        {
            if (Ring != null) Ring.enabled = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        }
    }

    /// <summary>
    /// The creator's frame, as the four mockups draw it: the brand in the corner, the title and its
    /// strap in the middle, the house's line in neon at the right, the five steps as tabs under
    /// them, glass panels for the page, and a footer with the way back on the left and the way on
    /// at the right.
    ///
    /// <para>Laid out from the frame the canvas actually has - the scaler's reference, shrunk by the
    /// larger text and stretched by the screen's shape - so a page fills a 4:3 window and a 16:9 one
    /// alike, and the larger text grows everything rather than clipping it.</para>
    ///
    /// <para>Every control keeps the caption it has always had as its first text: the tests, the
    /// season walk and the cast screen find them by it. Where the mockup draws a glyph - a swatch,
    /// an arrow, a plus - the caption is the control's name and a text that is never drawn, the
    /// same accessible-name pattern an icon button uses anywhere.</para>
    /// </summary>
    public sealed partial class CharacterCreator
    {
        private const float Margin = 36f, HeaderBottom = 176f, FooterHeight = 104f, Gap = 20f;
        private Vector2 frame = new Vector2(1920f, 1080f);
        private bool frameChanged;

        private static readonly string[] Pages = { "Appearance", "Identity", "Personality", "My Houseguests", "Review" };

        /// <summary>
        /// The canvas in reference units: what it is drawn to - the screen, or the camera a capture
        /// hands it to - through the scaler's own settings. Not the canvas's rect, which for the
        /// first frame after it is made is the raw pixels.
        /// </summary>
        private Vector2 Frame()
        {
            var canvas = GetComponent<Canvas>();
            float width = Screen.width, height = Screen.height;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null)
            { width = canvas.worldCamera.pixelWidth; height = canvas.worldCamera.pixelHeight; }
            if (scaler != null && width > 0 && height > 0)
            {
                var reference = scaler.referenceResolution;
                float log = Mathf.Lerp(Mathf.Log(width / reference.x, 2f), Mathf.Log(height / reference.y, 2f), scaler.matchWidthOrHeight);
                float scale = Mathf.Pow(2f, log);
                return new Vector2(width / scale, height / scale);
            }
            var rect = ((RectTransform)transform).rect;
            return new Vector2(rect.width > 0 ? rect.width : 1920f, rect.height > 0 ? rect.height : 1080f);
        }

        /// <summary>
        /// A window resized, or a canvas handed to a camera of another shape: the page is laid out
        /// from the frame, so it is laid out again - on the next frame, never inside the layout
        /// pass that noticed.
        /// </summary>
        private void OnRectTransformDimensionsChange()
        {
            if (IsShowing && (Frame() - frame).sqrMagnitude > 1f) frameChanged = true;
        }

        private void RefitToFrame()
        {
            if (!frameChanged) return;
            frameChanged = false;
            if (IsShowing && (Frame() - frame).sqrMagnitude > 1f) Rebuild();
        }

        /// <summary>The page's area, between the tabs and the footer.</summary>
        private Rect PageArea => new Rect(Margin, HeaderBottom, frame.x - 2f * Margin, frame.y - HeaderBottom - FooterHeight - 8f);

        // ---------------------------------------------------------------- the frame

        private void BuildBackdrop(RectTransform scrim)
        {
            // A soft pool of the house's blue behind the page, the studio lights the mockups glow under.
            var pool = new GameObject("Studio light", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            pool.rectTransform.SetParent(scrim, false);
            pool.sprite = UiTheme.Pack(PackArt.GlowCyan) ?? UiTheme.Circle();
            pool.color = new Color(UiTheme.AccentDeep.r, UiTheme.AccentDeep.g, UiTheme.AccentDeep.b, .22f);
            pool.raycastTarget = false;
            pool.rectTransform.anchorMin = pool.rectTransform.anchorMax = new Vector2(.5f, .55f);
            pool.rectTransform.sizeDelta = new Vector2(frame.x * .9f, frame.y * .8f);
            var vignette = HudPrimitives.Vignette(scrim);
            vignette.name = "Studio vignette";
            vignette.GetComponent<Image>().color = new Color(1f, 1f, 1f, .7f);
        }

        private void BuildHeader(RectTransform scrim)
        {
            BuildBrand(scrim);
            var title = HudPrimitives.Label("Studio title", scrim, 44f, Color.white, TextAlignmentOptions.Center);
            title.text = Localisation.Text(editingCastSlot ? "EDIT CAST HOUSEGUEST" : "CREATE A HOUSEGUEST");
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) title.font = bold;
            title.characterSpacing = 2f;
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(UiTheme.Paper, UiTheme.Paper, UiTheme.Glow, UiTheme.Glow);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            PlaceTop(title.rectTransform, 0f, 18f, 900f, 58f);
            var strap = HudPrimitives.Label("Studio strap", scrim, 15f, UiTheme.Accent, TextAlignmentOptions.Center);
            strap.text = Localisation.Text("SAME HOUSE. DIFFERENT STORIES.");
            strap.characterSpacing = 9f; strap.textWrappingMode = TextWrappingModes.NoWrap;
            PlaceTop(strap.rectTransform, 0f, 78f, 640f, 22f);
            foreach (float side in new[] { -1f, 1f })
            {
                var rule = HudPrimitives.Fill("Strap rule", scrim, new Color(UiTheme.Glow.r, UiTheme.Glow.g, UiTheme.Glow.b, .7f), 1);
                PlaceTop(rule, side * 360f, 89f, 72f, 2f);
            }
            if (frame.x >= 1560f) BuildTagline(scrim);
            BuildSteps(scrim);
        }

        private void BuildBrand(RectTransform scrim)
        {
            var mark = new GameObject("Brand mark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            mark.rectTransform.SetParent(scrim, false);
            mark.sprite = UiTheme.Pack(PackArt.IconHome) ?? UiTheme.Icon("house");
            mark.color = UiTheme.Heading; mark.preserveAspect = true; mark.raycastTarget = false;
            Place(mark.rectTransform, Margin - 4f, 24f, 54f, 54f);
            var word = HudPrimitives.Label("Brand word", scrim, 36f, Color.white, TextAlignmentOptions.TopLeft);
            word.text = Localisation.Text("GAMESIM");
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) word.font = bold;
            word.characterSpacing = 2f; word.enableVertexGradient = true;
            word.colorGradient = new VertexGradient(UiTheme.Hex("6CC0FF"), UiTheme.Hex("6CC0FF"), UiTheme.Hex("3A86FF"), UiTheme.Hex("3A86FF"));
            word.textWrappingMode = TextWrappingModes.NoWrap;
            Place(word.rectTransform, Margin + 60f, 20f, 240f, 46f);
            var strap = HudPrimitives.Label("Brand strap", scrim, 14f, UiTheme.Muted, TextAlignmentOptions.TopLeft);
            strap.text = Localisation.Text("THE HOUSE");
            strap.characterSpacing = 3f;
            Place(strap.rectTransform, Margin + 62f, 64f, 240f, 20f);
        }

        /// <summary>The house's line in the corner, lit like the sign on the set.</summary>
        private void BuildTagline(RectTransform scrim)
        {
            var glow = new GameObject("Tagline glow", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            glow.rectTransform.SetParent(scrim, false);
            glow.sprite = UiTheme.Pack(PackArt.GlowPurple) ?? UiTheme.Circle();
            glow.color = new Color(UiTheme.Flirt.r, UiTheme.Flirt.g, UiTheme.Flirt.b, .22f); glow.raycastTarget = false;
            PlaceRight(glow.rectTransform, Margin + 170f, 6f, 300f, 110f);
            var neon = HudPrimitives.Label("Tagline", scrim, 26f, UiTheme.Hex("FF7AD9"), TextAlignmentOptions.Center);
            neon.text = Localisation.Text("Good People\nBigger Stories");
            neon.fontStyle = FontStyles.Italic; neon.lineSpacing = -18f;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold); if (semibold != null) neon.font = semibold;
            PlaceRight(neon.rectTransform, Margin + 190f, 16f, 260f, 80f);
            neon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 6f);
            var rule = HudPrimitives.Fill("Tagline rule", scrim, new Color(UiTheme.Muted.r, UiTheme.Muted.g, UiTheme.Muted.b, .5f), 1);
            PlaceRight(rule, Margin + 166f, 22f, 2f, 70f);
            var line = HudPrimitives.Label("Tagline strap", scrim, 12f, UiTheme.Muted, TextAlignmentOptions.TopLeft);
            line.text = Localisation.Text("YOU\nCHOOSE\nTHE NEXT ICON.");
            line.characterSpacing = 5f; line.lineSpacing = 6f;
            PlaceRight(line.rectTransform, Margin, 22f, 150f, 70f);
        }

        /// <summary>The five steps, each a tab with its glyph; the one on screen is lit.</summary>
        private void BuildSteps(RectTransform scrim)
        {
            var navigation = new GameObject("Setup steps", typeof(RectTransform)).GetComponent<RectTransform>();
            navigation.SetParent(scrim, false);
            float width = Mathf.Min(1420f, frame.x - 2f * Margin), gap = 12f, tab = (width - gap * (Pages.Length - 1)) / Pages.Length;
            PlaceTop(navigation, 0f, 112f, width, 52f);
            for (int i = 0; i < Pages.Length; i++)
            {
                string page = Pages[i];
                bool on = studioPage == page;
                var button = Pill(navigation, page, new Rect(i * (tab + gap), 0f, tab, 52f), on ? Tone.TabActive : Tone.Tab,
                    () => GoTo(page), icon: StepIcon(page));
                button.name = page;
            }
        }

        private static string StepIcon(string page)
        {
            switch (page)
            {
                case "Appearance": return PackArt.KitIconPerson;
                case "Identity": return PackArt.KitIconNote;
                case "Personality": return PackArt.KitIconHeart;
                case "My Houseguests": return PackArt.KitIconPeople;
                default: return PackArt.KitIconCheck;
            }
        }

        private int PageIndex => Math.Max(0, Array.IndexOf(Pages, studioPage));

        /// <summary>
        /// To another step. What belonged to the page being left goes with it: the comparison with the
        /// original look, an armed delete, an armed new houseguest.
        /// </summary>
        private void GoTo(string page)
        {
            studioPage = page;
            comparingOriginal = false;
            pendingDeleteProfile = null;
            confirmingNew = false;
            Rebuild();
        }

        /// <summary>The name to show for a draft that has none yet.</summary>
        private string DisplayName => string.IsNullOrWhiteSpace(draft.Name)
            ? (editingCastSlot ? "This houseguest" : "Your houseguest") : draft.Name.Trim();

        /// <summary>
        /// The footer: the way back on the left, what the form still needs in the middle, and the way
        /// on at the right - the next step, and starting the season from wherever you are. On the
        /// last step the two are one.
        /// </summary>
        private void BuildFooter(RectTransform scrim)
        {
            var footer = new GameObject("Fixed footer", typeof(RectTransform)).GetComponent<RectTransform>();
            footer.SetParent(scrim, false);
            footer.anchorMin = new Vector2(0f, 0f); footer.anchorMax = new Vector2(1f, 0f); footer.pivot = new Vector2(.5f, 0f);
            footer.sizeDelta = new Vector2(0f, FooterHeight); footer.anchoredPosition = Vector2.zero;
            bool ready = draft.TryValidate(out var error);
            float y = 18f;
            Pill(footer, editingCastSlot ? CancelSlotCaption : BackCaption, new Rect(Margin, y, 280f, 64f), Tone.Secondary, Dismiss,
                icon: PackArt.KitIconArrowBack);
            bool last = PageIndex == Pages.Length - 1;
            string start = editingCastSlot ? ApplySlotCaption : StartCaption;
            float right = frame.x - Margin;
            if (!last)
            {
                var next = Pages[PageIndex + 1];
                Pill(footer, "Continue", new Rect(right - 300f, y - 4f, 300f, 72f), Tone.Primary, () => GoTo(next),
                    subtitle: "Next: " + next + "  ·  Step " + (PageIndex + 1) + " of " + Pages.Length, icon: PackArt.KitIconChevronRight, iconRight: true);
                right -= 300f + 16f;
            }
            var go = Pill(footer, start, new Rect(right - (last ? 360f : 300f), last ? y - 4f : y, last ? 360f : 300f, last ? 72f : 64f),
                last ? (ready ? Tone.Primary : Tone.Secondary) : (ready ? Tone.Ready : Tone.Secondary), StartSeason,
                subtitle: last ? (editingCastSlot ? "Back to the cast with this edit" : "Step " + Pages.Length + " of " + Pages.Length) : null);
            go.name = start;
            float statusLeft = Margin + 280f + 24f, statusRight = right - (last ? 360f : 300f) - 24f;
            var status = HudPrimitives.Label("Footer status", footer, 16f,
                ready && string.IsNullOrEmpty(resumeError) ? UiTheme.Positive : UiTheme.Warning, TextAlignmentOptions.Center);
            status.text = Localisation.Text(!string.IsNullOrEmpty(resumeError) ? resumeError : ready
                ? draft.Remaining > 0 ? "Ready. " + draft.Remaining + " spare point" + (draft.Remaining == 1 ? "" : "s") + " left unspent, which is allowed." : "Ready."
                : error);
            Place(status.rectTransform, statusLeft, y, Mathf.Max(80f, statusRight - statusLeft), 64f);
            status.enableAutoSizing = true; status.fontSizeMax = 16f; status.fontSizeMin = 12f;
        }

        private void StartSeason()
        {
            if (!draft.TryValidate(out _)) { Rebuild(); return; }
            resumeError = null;
            pending.Authored = draft.Copy();
            var start = onStart;
            Hide();
            start?.Invoke(pending);
        }

        // ---------------------------------------------------------------- pieces

        private enum Tone { Primary, Ready, Secondary, Quiet, Danger, Tab, TabActive, Selected }

        /// <summary>
        /// A button in the mockups' language: the primary in the action blue, the rest glass with a
        /// hairline, a tab on the pack's step art. Its caption is its first text, always.
        /// </summary>
        private Button Pill(Transform parent, string caption, Rect r, Tone tone, Action click,
            string subtitle = null, string icon = null, bool iconRight = false, float size = 0f)
        {
            var box = HudPrimitives.Fill(caption, parent, UiTheme.SurfaceRaised, 10);
            Place(box, r.x, r.y, r.width, r.height);
            var image = box.GetComponent<Image>(); image.raycastTarget = true;
            Color word = UiTheme.Paper;
            switch (tone)
            {
                case Tone.Primary:
                    // The mockups' way on is lit: the action blue, a bright edge and its glow. The
                    // pack's primary is a navy plate, which beside the glass read as one more panel.
                    image.color = UiTheme.ActionBlue;
                    UiTheme.AddBorder(box, 10, UiTheme.Glow);
                    UiTheme.AddGlow(box, 12); word = Color.white; break;
                case Tone.Ready:
                    image.color = new Color(UiTheme.ActionBlue.r, UiTheme.ActionBlue.g, UiTheme.ActionBlue.b, .35f);
                    UiTheme.AddBorder(box, 10, UiTheme.Glow); word = Color.white; break;
                case Tone.TabActive:
                    if (!UiTheme.PackSliced(image, PackArt.CreatorStepActive, 16f)) image.color = UiTheme.ActionBlue;
                    image.color = new Color(.55f, .75f, 1f, 1f);
                    UiTheme.AddGlow(box, 12); word = Color.white; break;
                case Tone.Tab:
                    if (!UiTheme.PackSliced(image, PackArt.CreatorStepInactive, 14f)) image.color = UiTheme.Surface;
                    break;
                case Tone.Selected:
                    image.color = new Color(UiTheme.AccentDeep.r, UiTheme.AccentDeep.g, UiTheme.AccentDeep.b, .55f);
                    UiTheme.AddBorder(box, 10, UiTheme.Glow); word = Color.white; break;
                case Tone.Danger:
                    image.color = new Color(UiTheme.Danger.r * .35f, UiTheme.Danger.g * .2f, UiTheme.Danger.b * .2f, .9f);
                    UiTheme.AddBorder(box, 10, new Color(UiTheme.Danger.r, UiTheme.Danger.g, UiTheme.Danger.b, .8f)); word = UiTheme.Danger; break;
                case Tone.Quiet:
                    image.color = new Color(UiTheme.GlassFill.r, UiTheme.GlassFill.g, UiTheme.GlassFill.b, .6f);
                    UiTheme.AddBorder(box, 10, UiTheme.Outline); break;
                default:
                    if (!UiTheme.PackSliced(image, PackArt.ButtonSecondary, 12f)) image.color = UiTheme.Surface;
                    UiTheme.AddBorder(box, 10, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .55f)); break;
            }
            float left = 14f, rightPad = 14f;
            var mark = icon != null ? UiTheme.Pack(icon) : null;
            float glyph = Mathf.Min(26f, r.height - 18f);
            var label = HudPrimitives.Label("Label", box, size > 0f ? size : (subtitle != null ? 20f : r.height >= 56f ? 19f : 16f), word,
                mark != null && !iconRight ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center);
            var weight = UiTheme.Font(tone == Tone.Tab ? UiTheme.Weight.Medium : UiTheme.Weight.SemiBold); if (weight != null) label.font = weight;
            label.text = Localisation.Text(caption);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true; label.fontSizeMax = label.fontSize; label.fontSizeMin = Mathf.Min(11f, label.fontSize);
            if (mark != null)
            {
                var art = new GameObject("Glyph", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                art.rectTransform.SetParent(box, false);
                art.sprite = mark; art.color = tone == Tone.Tab ? UiTheme.Accent : word; art.preserveAspect = true; art.raycastTarget = false;
                art.rectTransform.anchorMin = art.rectTransform.anchorMax = new Vector2(iconRight ? 1f : 0f, .5f);
                art.rectTransform.pivot = new Vector2(iconRight ? 1f : 0f, .5f);
                art.rectTransform.sizeDelta = new Vector2(glyph, glyph);
                art.rectTransform.anchoredPosition = new Vector2(iconRight ? -16f : 16f, 0f);
                if (iconRight) rightPad = glyph + 26f; else left = glyph + 30f;
            }
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, subtitle != null ? r.height * .42f : 2f);
            rect.offsetMax = new Vector2(-rightPad, subtitle != null ? -4f : -2f);
            if (subtitle != null)
            {
                label.alignment = TextAlignmentOptions.Bottom;
                // Full strength on the action blue: dimmed, a subtitle fell short of the contrast floor.
                var under = HudPrimitives.Label("Subtitle", box, 13f, new Color(word.r, word.g, word.b, tone == Tone.Primary ? 1f : .8f), TextAlignmentOptions.Top);
                under.text = Localisation.Text(subtitle); under.textWrappingMode = TextWrappingModes.NoWrap;
                under.enableAutoSizing = true; under.fontSizeMax = 13f; under.fontSizeMin = 10f;
                under.rectTransform.anchorMin = Vector2.zero; under.rectTransform.anchorMax = Vector2.one;
                under.rectTransform.offsetMin = new Vector2(left, 4f); under.rectTransform.offsetMax = new Vector2(-rightPad, -r.height * .6f);
            }
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => click?.Invoke());
            Seen(button, 10);
            return button;
        }

        /// <summary>
        /// A control drawn as a glyph - an arrow, a plus - whose caption is its name: kept as its first
        /// text, never drawn, so it is found by what it does and seen as what it is.
        /// </summary>
        private Button GlyphButton(Transform parent, string caption, Rect r, string glyphText, Sprite glyphArt, Action click, bool circle = true)
        {
            var box = HudPrimitives.Fill(caption, parent, new Color(UiTheme.Surface.r, UiTheme.Surface.g, UiTheme.Surface.b, .9f), circle ? 18 : 8);
            Place(box, r.x, r.y, r.width, r.height);
            var image = box.GetComponent<Image>(); image.raycastTarget = true;
            if (circle) { image.sprite = UiTheme.Circle(); image.type = Image.Type.Simple; }
            UiTheme.AddBorder(box, circle ? Mathf.RoundToInt(r.height * .5f) - 1 : 8, UiTheme.Glow);
            var name = HudPrimitives.Label("Label", box, 12f, Color.white, TextAlignmentOptions.Center);
            name.text = Localisation.Text(caption); name.enabled = false;
            // The control's own size: a label left at its default rect would reach past the button.
            name.rectTransform.anchorMin = Vector2.zero; name.rectTransform.anchorMax = Vector2.one;
            name.rectTransform.offsetMin = name.rectTransform.offsetMax = Vector2.zero;
            if (glyphArt != null)
            {
                var art = new GameObject("Glyph", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                art.rectTransform.SetParent(box, false);
                art.sprite = glyphArt; art.color = UiTheme.Paper; art.preserveAspect = true; art.raycastTarget = false;
                art.rectTransform.anchorMin = art.rectTransform.anchorMax = new Vector2(.5f, .5f);
                art.rectTransform.sizeDelta = new Vector2(r.height * .5f, r.height * .5f);
            }
            else
            {
                var mark = HudPrimitives.Label("Glyph", box, r.height * .6f, UiTheme.Paper, TextAlignmentOptions.Center);
                mark.text = glyphText;
                var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) mark.font = bold;
                mark.rectTransform.anchorMin = Vector2.zero; mark.rectTransform.anchorMax = Vector2.one;
                mark.rectTransform.offsetMin = mark.rectTransform.offsetMax = Vector2.zero;
            }
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => click?.Invoke());
            Seen(button, circle ? Mathf.RoundToInt(r.height * .5f) : 8);
            return button;
        }

        /// <summary>
        /// Focus that can be seen. Unity's default tint darkens a selected control by four percent,
        /// which on dark glass or a colour swatch is nothing: the keyboard or the pad on a control
        /// lights a ring round it, and a hover brightens it, as the rest of the HUD does.
        /// </summary>
        private static void Seen(Button button, int radius)
        {
            var colours = button.colors;
            colours.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colours.selectedColor = colours.highlightedColor;
            button.colors = colours;
            var rect = (RectTransform)button.transform;
            UiTheme.AddBorder(rect, radius, UiTheme.Glow);
            var ring = rect.GetChild(rect.childCount - 1).GetComponent<Image>();
            ring.name = "Focus ring";
            ring.rectTransform.offsetMin = new Vector2(-4f, -4f); ring.rectTransform.offsetMax = new Vector2(4f, 4f);
            ring.enabled = false;
            button.gameObject.AddComponent<CreatorFocus>().Ring = ring;
        }

        /// <summary>
        /// On or off, and seen to be: a glyph's ring and mark are not the button's target graphic, so
        /// the disabled tint alone left a spent + looking live.
        /// </summary>
        private static void Enable(Button button, bool on)
        {
            button.interactable = on;
            var group = button.GetComponent<CanvasGroup>();
            if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
            group.alpha = on ? 1f : .35f;
        }

        /// <summary>One of the page's glass panels.</summary>
        private static RectTransform Panel(string name, Transform parent, Rect r, bool lit = false)
        {
            var panel = HudPrimitives.Fill(name, parent, new Color(UiTheme.GlassFill.r, UiTheme.GlassFill.g, UiTheme.GlassFill.b, .88f), UiTheme.GlassRadius);
            Place(panel, r.x, r.y, r.width, r.height);
            if (lit) UiTheme.AddGlow(panel, UiTheme.GlassRadius);
            UiTheme.AddBorder(panel, UiTheme.GlassRadius, lit ? UiTheme.Edge(UiTheme.Emphasis.Active) : UiTheme.Edge(UiTheme.Emphasis.Interactive));
            return panel;
        }

        /// <summary>A panel's heading: its glyph, its name in capitals, and a count at the right if it has one.</summary>
        private static float SectionHeading(Transform parent, float x, float y, float width, string icon, string title, string detail = null,
            string detailCaption = null, float size = 24f)
        {
            float left = x;
            var mark = icon != null ? UiTheme.Pack(icon) : null;
            if (mark != null)
            {
                var art = new GameObject("Heading glyph", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                art.rectTransform.SetParent(parent, false);
                art.sprite = mark; art.color = UiTheme.Glow; art.preserveAspect = true; art.raycastTarget = false;
                Place(art.rectTransform, x, y + 2f, size + 6f, size + 6f);
                left += size + 18f;
            }
            var heading = HudPrimitives.Label("Section heading", parent, size, UiTheme.Paper, TextAlignmentOptions.MidlineLeft);
            heading.text = Localisation.Text(title);
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) heading.font = bold;
            heading.characterSpacing = 1.5f; heading.textWrappingMode = TextWrappingModes.NoWrap;
            heading.enableAutoSizing = true; heading.fontSizeMax = size; heading.fontSizeMin = 14f;
            float detailWidth = detail != null ? 200f : 0f;
            Place(heading.rectTransform, left, y, width - (left - x) - detailWidth, size * 1.4f);
            if (detail != null)
            {
                var count = HudPrimitives.Label("Section count", parent, size, UiTheme.Paper, TextAlignmentOptions.TopRight);
                count.text = detail;
                if (bold != null) count.font = bold;
                Place(count.rectTransform, x + width - detailWidth, y - 2f, detailWidth, size * 1.3f);
                if (detailCaption != null)
                {
                    var caption = HudPrimitives.Label("Section count caption", parent, 12f, UiTheme.Heading, TextAlignmentOptions.TopRight);
                    caption.text = Localisation.Text(detailCaption); caption.characterSpacing = 2f;
                    Place(caption.rectTransform, x + width - detailWidth - 40f, y + size * 1.3f, detailWidth + 40f, 18f);
                }
            }
            // Under the count's caption when there is one: the line after a heading starts clear of both.
            return y + Mathf.Max(size * 1.4f, detailCaption != null ? size * 1.3f + 18f : 0f) + 6f;
        }

        private static TMP_Text Words(Transform parent, string text, float size, Color colour, Rect r,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft, string name = "Words")
        {
            var label = HudPrimitives.Label(name, parent, size, colour, align);
            label.text = Localisation.Text(text ?? string.Empty);
            Place(label.rectTransform, r.x, r.y, r.width, r.height);
            return label;
        }

        private static Image Glyph(Transform parent, string icon, Color tint, Rect r, string name = "Glyph")
        {
            var sprite = icon == null ? null : (icon.Contains("/") ? UiTheme.Pack(icon) : UiTheme.Icon(icon));
            var art = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            art.rectTransform.SetParent(parent, false);
            art.sprite = sprite; art.color = tint; art.preserveAspect = true; art.raycastTarget = false;
            art.enabled = sprite != null;
            Place(art.rectTransform, r.x, r.y, r.width, r.height);
            return art;
        }

        /// <summary>A colour to choose, drawn as a round swatch; lit when it is the one worn.</summary>
        private Button Swatch(Transform parent, string caption, Color colour, bool chosen, Rect r, Action click)
        {
            var button = GlyphButton(parent, caption, r, "", null, click);
            var disc = button.GetComponent<Image>();
            disc.color = colour;
            var edge = button.transform.Find("Border");
            if (edge != null) edge.GetComponent<Image>().color = chosen ? Color.white : new Color(1f, 1f, 1f, .18f);
            if (chosen)
            {
                var ring = new GameObject("Chosen ring", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                ring.rectTransform.SetParent(button.transform, false);
                ring.sprite = UiTheme.Ring(); ring.color = UiTheme.Glow; ring.raycastTarget = false;
                ring.rectTransform.anchorMin = Vector2.zero; ring.rectTransform.anchorMax = Vector2.one;
                ring.rectTransform.offsetMin = new Vector2(-5f, -5f); ring.rectTransform.offsetMax = new Vector2(5f, 5f);
            }
            return button;
        }

        /// <summary>
        /// A thumbnail to choose: the pack's frame, lit when chosen, with the picture and its name. A
        /// <paramref name="face"/> is a houseguest's portrait, bound so it lands when it is ready.
        /// </summary>
        private Button Thumbnail(Transform parent, string name, string label, Rect r, bool chosen, Sprite art, Texture texture,
            string fallbackIcon, Action click, ContestantState face = null)
        {
            var card = HudPrimitives.Fill(name, parent, UiTheme.Surface, 10);
            Place(card, r.x, r.y, r.width, r.height);
            var image = card.GetComponent<Image>(); image.raycastTarget = true;
            if (!UiTheme.PackSliced(image, chosen ? PackArt.CreatorThumbnailSelected : PackArt.CreatorThumbnail, 12f))
                UiTheme.AddBorder(card, 10, chosen ? UiTheme.Glow : UiTheme.Outline);
            if (chosen) UiTheme.AddGlow(card, 12);
            var caption = HudPrimitives.Label("Label", card, 12f, chosen ? Color.white : UiTheme.Paper, TextAlignmentOptions.Bottom);
            caption.text = Localisation.Text(label); caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.overflowMode = TextOverflowModes.Ellipsis;
            caption.rectTransform.anchorMin = new Vector2(0f, 0f); caption.rectTransform.anchorMax = new Vector2(1f, 0f);
            caption.rectTransform.pivot = new Vector2(.5f, 0f);
            caption.rectTransform.sizeDelta = new Vector2(-10f, 18f); caption.rectTransform.anchoredPosition = new Vector2(0f, 5f);
            float picture = Mathf.Max(20f, Mathf.Min(r.width - 14f, r.height - 30f));
            if (face != null)
            {
                var photo = HudPrimitives.RectPortrait(card, "Picture", null, face, new Vector2(picture, picture), 8);
                photo.anchorMin = photo.anchorMax = new Vector2(.5f, 1f); photo.pivot = new Vector2(.5f, 1f);
                photo.anchoredPosition = new Vector2(0f, -6f);
            }
            else if (texture != null)
            {
                var raw = new GameObject("Picture", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                raw.rectTransform.SetParent(card, false); raw.texture = texture; raw.raycastTarget = false;
                raw.rectTransform.anchorMin = raw.rectTransform.anchorMax = new Vector2(.5f, 1f); raw.rectTransform.pivot = new Vector2(.5f, 1f);
                raw.rectTransform.sizeDelta = new Vector2(picture, picture); raw.rectTransform.anchoredPosition = new Vector2(0f, -6f);
            }
            else
            {
                var sprite = art ?? (fallbackIcon == null ? null : fallbackIcon.Contains("/") ? UiTheme.Pack(fallbackIcon) : UiTheme.Icon(fallbackIcon));
                var pic = new GameObject("Picture", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                pic.rectTransform.SetParent(card, false); pic.sprite = sprite; pic.preserveAspect = true; pic.raycastTarget = false;
                pic.color = art != null ? Color.white : new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, .7f);
                pic.enabled = sprite != null;
                float side = art != null ? picture : picture * .55f;
                pic.rectTransform.anchorMin = pic.rectTransform.anchorMax = new Vector2(.5f, 1f); pic.rectTransform.pivot = new Vector2(.5f, .5f);
                pic.rectTransform.sizeDelta = new Vector2(side, side); pic.rectTransform.anchoredPosition = new Vector2(0f, -6f - picture * .5f);
            }
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => click?.Invoke());
            Seen(button, 10);
            return button;
        }

        // ---------------------------------------------------------------- placement

        /// <summary>Top-left anchored, in the parent's own units, y down.</summary>
        private static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f); rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
        }

        /// <summary>Centred on the frame horizontally, <paramref name="x"/> from the middle, y down from the top.</summary>
        private static void PlaceTop(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f); rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
        }

        /// <summary>From the frame's right edge, y down from the top.</summary>
        private static void PlaceRight(RectTransform rect, float right, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-right, -y); rect.sizeDelta = new Vector2(w, h);
        }
    }
}
