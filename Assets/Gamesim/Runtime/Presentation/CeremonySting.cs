using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// A broadcast title card for the four beats the episode already models but previously rendered
    /// as a plain status-line swap: the nomination ceremony, the veto ceremony, an eviction, and the
    /// jury's winner.
    ///
    /// Three constraints shape this more than the visual design does.
    ///
    /// <para>It never takes input. There is no <see cref="GraphicRaycaster"/> on the canvas and every
    /// graphic sets <c>raycastTarget = false</c>, so the card cannot swallow a click or a key during
    /// the seconds it is up. A sting that interrupted play would be worse than no sting.</para>
    ///
    /// <para>It lives on its own scene root rather than under the director. The PlayMode suite reads
    /// the UI through <c>director.GetComponentsInChildren&lt;TMP_Text&gt;()</c>, so anything parented
    /// there joins those queries; keeping the card outside that subtree makes it provably inert to
    /// the existing assertions instead of merely unlikely to disturb them.</para>
    ///
    /// <para>It shows only what the player is allowed to see. The caller passes the last committed
    /// event that already passed the audience filter, so the card cannot become a side channel for
    /// private coordination the notebook deliberately withholds.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CeremonySting : MonoBehaviour
    {
        // Event kinds, as the simulation records them on EpisodeEvent.kind.
        public const string NominationKind = "nomination";
        public const string VetoKind = "veto";
        public const string EvictionKind = "eviction";
        public const string WinnerKind = "winner";

        private const float FadeIn = 0.22f;
        private const float Hold = 2.0f;
        private const float FadeOut = 0.38f;
        private const float Rise = 18f;

        /// <summary>
        /// The card is inset from the chrome rather than given a fixed width and centre.
        ///
        /// It shares a band with the left column and the navigation panel, so it has to live between
        /// them. Absolute coordinates cannot express that: the HUD's CanvasScaler matches width and
        /// height equally, so the canvas reference size moves with the aspect ratio — at 4:3 it is
        /// 1385x1039, not 1600x900, and any constant tuned against one shape is wrong in the other.
        /// Anchoring to both edges and insetting past each panel holds at every aspect ratio.
        ///
        /// This was found by playing an episode and looking at the frame: a centred 880-wide card
        /// drew straight over the Notebook button during every ceremony.
        /// </summary>
        private const float LeftInset = Episode.EpisodeHud.LeftColumnX + 330f + 16f; // column start, width, gap
        private const float RightInset = 24f + 465f + 16f;  // navigation margin, width, gap
        private const float TopInset = 18f;
        private const float HeadlineSize = 34f;
        private const float DetailSize = 19f;
        private const float TopPad = 14f, Gap = 6f, BottomPad = 14f;

        private CanvasGroup group;
        private RectTransform card, ruleRect;
        private TMP_Text headline, detail;
        private Image rule;
        private Vector2 restPosition;
        private float elapsed;
        private bool playing, reduced;

        /// <summary>Matches the HUD's accessibility preference so a large-text player gets a large card.</summary>
        public float FontScale { get; set; } = 1f;

        /// <summary>Whether the card is still on its own timer, as every sibling card reports.</summary>
        public bool IsPlaying => playing;

        /// <summary>
        /// Creates the sting in <paramref name="owner"/>'s scene, as a root object so it unloads with
        /// that scene without being a child of anything the tests enumerate.
        /// </summary>
        public static CeremonySting Attach(GameObject owner)
        {
            var root = new GameObject("Gamesim Ceremony Sting",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            if (owner != null && owner.scene.IsValid()) SceneManager.MoveGameObjectToScene(root, owner.scene);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the HUD's 70 so the card reads over the panels, not behind them.
            canvas.sortingOrder = 90;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            return root.AddComponent<CeremonySting>();
        }

        /// <summary>Returns true when this kind of committed event deserves a card.</summary>
        public static bool IsCeremony(string kind) =>
            kind == NominationKind || kind == VetoKind || kind == EvictionKind || kind == WinnerKind;

        /// <summary>
        /// Plays the card for a committed event. <paramref name="detail"/> must already be
        /// player-visible text. An unrecognised kind is ignored rather than guessed at.
        /// </summary>
        public void Play(string kind, string body, bool reducedMotion)
        {
            if (!IsCeremony(kind)) return;
            Build();
            reduced = reducedMotion;
            elapsed = 0f;
            playing = true;

            Layout();
            headline.text = Headline(kind);
            headline.color = Tint(kind);
            rule.color = Tint(kind);
            detail.text = body ?? string.Empty;

            card.gameObject.SetActive(true);
            Apply(reduced ? 1f : 0f);
        }

        /// <summary>Hides the card immediately, for a scene change or a panel that must own the screen.</summary>
        public void Cancel()
        {
            playing = false;
            // The canvas goes with it. The fade-out ends by calling this on the first frame past
            // its end rather than by drawing a last frame at zero, so without this line the card
            // is stranded at whatever alpha it drew before - a thousandth at a steady frame rate,
            // three-quarters of full if one long frame crossed the fade in a single step. Nothing
            // renders either way, because the card itself is deactivated; but the group keeps the
            // number, and anything that asks the screen whether a card is still fading believes it
            // forever. Every sibling card zeroes its group here; this one used not to.
            if (group != null) group.alpha = 0f;
            if (card != null) card.gameObject.SetActive(false);
        }

        private static string Headline(string kind)
        {
            switch (kind)
            {
                case NominationKind: return "NOMINATION CEREMONY";
                case VetoKind: return "VETO CEREMONY";
                case EvictionKind: return "EVICTION";
                case WinnerKind: return "THE WINNER";
                default: return string.Empty;
            }
        }

        private static Color Tint(string kind)
        {
            switch (kind)
            {
                case NominationKind: return UiTheme.Danger;
                case VetoKind: return UiTheme.Gold;
                case EvictionKind: return UiTheme.Danger;
                case WinnerKind: return UiTheme.Gold;
                default: return UiTheme.Accent;
            }
        }

        // Set here rather than in Build so the card is inert from the moment it exists, not from
        // the moment it first plays.
        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;
        }

        private void Build()
        {
            if (card != null) return;

            card = NewPanel("Card", (RectTransform)transform, UiTheme.GlassFill, UiTheme.GlassRadius);
            // Stretched across the top, inset past the chrome on both sides.
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(1f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            // The mockups' running bug is a glass strip: the night ground at 85 %, a cyan hairline
            // on the edge and a soft glow outside it (mockup-08, -10). The glow is a child that
            // reaches past the rect, and every geometry check in the suite reads the rect.
            UiTheme.Glass(card, UiTheme.GlassRadius);

            rule = NewPanel("Sting rule", card, UiTheme.Accent, 2).GetComponent<Image>();
            ruleRect = (RectTransform)rule.transform;
            ruleRect.anchorMin = new Vector2(0f, 0.5f);
            ruleRect.anchorMax = new Vector2(0f, 0.5f);
            ruleRect.pivot = new Vector2(0f, 0.5f);

            headline = NewText("Sting headline", card, HeadlineSize, UiTheme.Accent);
            detail = NewText("Sting detail", card, DetailSize, UiTheme.Paper);

            card.gameObject.SetActive(false);
        }

        /// <summary>
        /// Sizes the card around its text rather than around a fixed rectangle, so raising the
        /// large-text preference grows the card instead of clipping the headline inside it.
        /// </summary>
        private void Layout()
        {
            float head = Mathf.Round(HeadlineSize * FontScale);
            float body = Mathf.Round(DetailSize * FontScale);
            float headLine = Mathf.Ceil(head * 1.2f);
            float bodyLine = Mathf.Ceil(body * 1.25f);
            float height = TopPad + headLine + Gap + bodyLine + BottomPad;

            card.offsetMin = new Vector2(LeftInset, -(TopInset + height));
            card.offsetMax = new Vector2(-RightInset, -TopInset);
            restPosition = card.anchoredPosition;

            ruleRect.anchoredPosition = new Vector2(30f, 0f);
            ruleRect.sizeDelta = new Vector2(6f, Mathf.Max(8f, height - 28f));

            headline.fontSize = head;
            Stretch(headline.rectTransform, 56f, TopPad, 40f, height - TopPad - headLine);

            // Committed event text varies a lot in length and the card is only one line deep, so the
            // detail is allowed to shrink rather than truncate. A slightly smaller sentence is a far
            // better outcome than a name cut in half at the moment someone is evicted.
            detail.fontSize = body;
            detail.enableAutoSizing = true;
            detail.fontSizeMax = body;
            detail.fontSizeMin = Mathf.Max(11f, body * 0.7f);
            Stretch(detail.rectTransform, 56f, TopPad + headLine + Gap, 40f, BottomPad);
        }

        private void LateUpdate()
        {
            if (!playing || card == null) return;
            elapsed += Time.unscaledDeltaTime;

            if (reduced)
            {
                // Reduced motion drops the movement, not the information: the card still holds
                // long enough to read, it simply does not travel or fade on the way.
                if (elapsed < FadeIn + Hold + FadeOut) return;
                Cancel();
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
            // Ease out so the card arrives quickly and settles, the way a broadcast graphic snaps in.
            float eased = 1f - (1f - t) * (1f - t);
            group.alpha = eased;
            // It descends into place rather than rising into it. Rising would put the card below its
            // resting position for the length of the entrance, and its resting position is chosen to
            // be the lowest it may ever sit without covering the episode panel's Close button.
            card.anchoredPosition = restPosition + new Vector2(0f, (1f - eased) * Rise);
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

        private static TMP_Text NewText(string name, RectTransform parent, float size, Color color)
        {
            var holder = new GameObject(name, typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            var label = holder.AddComponent<TextMeshProUGUI>();
            var font = TMP_Settings.defaultFontAsset != null
                ? TMP_Settings.defaultFontAsset
                : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null) label.font = font;
            label.fontSize = size;
            label.color = color;
            // Event text is authored copy, not markup; the rest of the HUD reads it literally too.
            label.richText = false;
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Truncate;
            return label;
        }

        private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
