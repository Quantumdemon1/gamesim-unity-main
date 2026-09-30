using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The skip a staged ceremony shows (PACK8-PASS-PLAN A1): a chip in the corner of the screen
    /// saying that a press moves the ceremony on, while the house gathers, while its card plays on
    /// the set's screen, and while the evicted walk out. The press already worked - a click, Enter,
    /// Escape, or the pad's A or B - but nothing on screen said so: the HUD stands aside from the
    /// summons on, and the card on the set's screen shows its controls only in the cuts to it.
    ///
    /// <para>It does nothing itself. The press it names is read straight off the devices by whatever
    /// it moves on - the summons, the card, the walk out - by the cards' rule
    /// (<see cref="CeremonyOverlays"/>), so a click on the chip is the same one press as a click
    /// anywhere, and can never step twice. One press is one step: the summons starts the card at
    /// its block or its result, a press on the card closes it, and a press on the walk out ends it.
    /// The outcome is never touched.</para>
    ///
    /// <para>Its rect takes the pointer, with no handler behind it, and nothing else of it does. The
    /// chrome is back while the evicted walk out, and a click on the chip would otherwise also have
    /// pressed whichever face on the cast strip lay under it; the house's click guard asks the event
    /// system the same question, so the player does not walk there either. It is small and up only
    /// while a ceremony is staged, so nothing is stranded behind it.</para>
    ///
    /// <para>Its own canvas, above the cards and below the menus, in the bottom right: the rail
    /// runs down the left, and the ceremony strip across the top. It lives on its own scene root,
    /// out of the director's subtree the HUD's tests read, as the cards do.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CeremonySkipChip : MonoBehaviour
    {
        /// <summary>What the chip says. A caption tests and screen readers find it by: never reworded, never decorated.</summary>
        public const string Caption = "Skip ahead";

        /// <summary>The chip's scene root, for tests to find it by.</summary>
        public const string RootName = "Gamesim Ceremony Skip";

        /// <summary>The chip's rect, under the root.</summary>
        public const string ChipName = "Skip chip";

        /// <summary>The key the chip names on a keyboard: Enter skips, as the cards' own controls line says.</summary>
        public const string KeyboardKey = "Enter";

        /// <summary>The button the chip names on a pad: A skips, as the cards' own controls line says.</summary>
        public const string PadKey = "A";

        /// <summary>Above the key ceremony and the vote reveal (110), below the season report and the menus (120 and up).</summary>
        public const int SortingOrder = 115;

        private const float CaptionSize = 17f, KeySize = 13f;
        /// <summary>Clear of the screen's edge, as the HUD's right column is.</summary>
        private const float Margin = 24f;
        private const float PadX = 12f, PadY = 8f, Gap = 10f, KeyPadX = 7f;
        /// <summary>A label box this many times its font: Inter's line is 1.21 of its size, and a box short of it draws nothing.</summary>
        private const float LineScale = 1.35f;

        private CanvasGroup group;
        private RectTransform chip, keycap;
        private TMP_Text caption, key;
        private bool showing, usingPad;
        private float laidOutFor = -1f;

        /// <summary>Matches the HUD's accessibility preference, as the cards do.</summary>
        public float FontScale { get; set; } = 1f;

        /// <summary>Whether the chip is on screen.</summary>
        public bool IsShowing => showing;

        /// <summary>Creates the chip in <paramref name="owner"/>'s scene, as a root of its own.</summary>
        public static CeremonySkipChip Attach(GameObject owner)
        {
            var root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup),
                typeof(GraphicRaycaster));
            if (owner != null && owner.scene.IsValid()) SceneManager.MoveGameObjectToScene(root, owner.scene);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            return root.AddComponent<CeremonySkipChip>();
        }

        // Inert from the moment it exists, not from the moment it first shows.
        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;
        }

        /// <summary>
        /// Puts the chip up or takes it down. Called every frame with whether a staged ceremony is
        /// running; laid out again only when it goes up or the font scale changed.
        /// </summary>
        public void Show(bool show)
        {
            if (!show)
            {
                if (!showing) return;
                showing = false;
                group.alpha = 0f;
                group.blocksRaycasts = false;
                if (chip != null) chip.gameObject.SetActive(false);
                return;
            }
            if (showing && Mathf.Approximately(laidOutFor, FontScale)) return;
            Build();
            showing = true;
            chip.gameObject.SetActive(true);
            Layout();
            group.alpha = 1f;
            group.blocksRaycasts = true;
        }

        private void Update()
        {
            if (!showing) return;
            // The key follows the device last pressed, as the cards' controls line does.
            var pad = CeremonyTakeover.PadUsed();
            if (!pad.HasValue || pad.Value == usingPad) return;
            usingPad = pad.Value;
            Layout();
        }

        private void Build()
        {
            if (chip != null) return;
            chip = NewPanel(ChipName, (RectTransform)transform, UiTheme.GlassFill, UiTheme.ControlRadius);
            UiTheme.AddBorder(chip, UiTheme.ControlRadius, UiTheme.Hairline);
            keycap = NewPanel("Skip key", chip, UiTheme.SurfaceRaised, UiTheme.ControlRadius);
            UiTheme.AddBorder(keycap, UiTheme.ControlRadius, UiTheme.Outline);
            key = NewText("Skip key label", keycap, KeySize, UiTheme.Paper, UiTheme.Weight.SemiBold);
            key.alignment = TextAlignmentOptions.Center;
            caption = NewText("Skip caption", chip, CaptionSize, UiTheme.Paper, UiTheme.Weight.Medium);
            caption.text = Caption;
            // The chip's own ground takes the pointer, so a click on it presses nothing under it;
            // the borders are images too, and none of the rest does.
            foreach (var graphic in chip.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            chip.GetComponent<Image>().raycastTarget = true;
            chip.gameObject.SetActive(false);
        }

        /// <summary>
        /// Sizes the chip around its words, anchored to the bottom right, so the large-text
        /// preference grows it inward rather than clipping it; every label box at least 1.3 of its
        /// font, or Inter draws nothing in it.
        /// </summary>
        private void Layout()
        {
            float captionSize = Mathf.Round(CaptionSize * FontScale), keySize = Mathf.Round(KeySize * FontScale);
            float captionLine = Mathf.Ceil(captionSize * LineScale), keyLine = Mathf.Ceil(keySize * LineScale);
            caption.fontSize = captionSize;
            key.fontSize = keySize;
            key.text = usingPad ? PadKey : KeyboardKey;

            float captionWidth = Mathf.Ceil(caption.GetPreferredValues(Caption, 2000f, captionLine).x) + 2f;
            float keyWidth = Mathf.Max(keyLine, Mathf.Ceil(key.GetPreferredValues(key.text, 2000f, keyLine).x) + 2f + KeyPadX * 2f);
            float keyHeight = keyLine + 4f;
            float height = PadY * 2f + Mathf.Max(captionLine, keyHeight);
            float width = PadX + keyWidth + Gap + captionWidth + PadX;

            chip.anchorMin = chip.anchorMax = chip.pivot = new Vector2(1f, 0f);
            chip.anchoredPosition = new Vector2(-Margin, Margin);
            chip.sizeDelta = new Vector2(width, height);

            Place(keycap, PadX, keyWidth, keyHeight);
            Stretch(key.rectTransform);
            Place(caption.rectTransform, PadX + keyWidth + Gap, captionWidth, captionLine);
            laidOutFor = FontScale;
        }

        /// <summary>A child at <paramref name="left"/> from the chip's left edge, centred on its height.</summary>
        private static void Place(RectTransform rect, float left, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(left, 0f);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static RectTransform NewPanel(string name, RectTransform parent, Color color, int radius)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            var image = panel.GetComponent<Image>();
            UiTheme.Style(image, color, radius);
            image.raycastTarget = false;
            return rect;
        }

        private static TMP_Text NewText(string name, RectTransform parent, float size, Color color, UiTheme.Weight weight)
        {
            var holder = new GameObject(name, typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            var label = holder.AddComponent<TextMeshProUGUI>();
            var font = UiTheme.Font(weight);
            if (font != null) label.font = font;
            label.fontSize = size;
            label.color = color;
            label.richText = false;
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }
    }
}
