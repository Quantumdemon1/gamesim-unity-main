using System.Collections.Generic;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The full-screen title card that opens a ceremony: week, mark, name of the beat, a line of
    /// flavour, and the faces it is about.
    ///
    /// <para>This is the shot the episode was missing. <see cref="CeremonySting"/> is the running
    /// broadcast bug — a strip that reports a result while play continues — and it is good at that,
    /// but a nomination and an eviction are not status changes, they are scenes. Giving them the
    /// screen for three seconds is most of the distance between reading like a simulation log and
    /// reading like an episode.</para>
    ///
    /// <para>It keeps the sting's two hard rules. It takes no input — no <see cref="GraphicRaycaster"/>
    /// and every graphic non-raycasting — so it cannot swallow a click, and it dismisses on a timer
    /// rather than on acknowledgement, so nothing can be stranded behind it: not a player mid-walk,
    /// and not an automated season driving 56 decisions in under a minute. And it lives on its own
    /// scene root, outside the subtree the PlayMode suite enumerates through the director.</para>
    ///
    /// <para>Subjects are supplied by the caller, already audience-filtered. The card shows faces,
    /// which makes it a more attractive side channel than the sting's text ever was — so it learns
    /// nothing about the cast on its own.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CeremonyTakeover : MonoBehaviour
    {
        private const float FadeIn = 0.30f;
        private const float Hold = 2.6f;
        private const float FadeOut = 0.45f;
        private const float Rise = 26f;

        /// <summary>One face on the card, with what it is doing there.</summary>
        public readonly struct Subject
        {
            public readonly string Name;
            public readonly string Badge;
            public readonly Texture Portrait;
            public readonly ContestantState Character;

            public Subject(string name, string badge, Texture portrait, ContestantState character = null)
            {
                Name = name; Badge = badge; Portrait = portrait;
                Character = character?.Clone();
            }
        }

        /// <summary>What the card says it is waiting for, matching the web build's wording.</summary>
        public const string DismissCaption = "Click anywhere to continue";

        private RectTransform rule, glassGround;
        private TMP_Text dismiss;
        private CanvasGroup group;
        private RectTransform column, scrim, faces;
        private TMP_Text eyebrow, title, flavour;
        private RectTransform markOuter, markInner;
        private float elapsed;
        private bool playing, reduced;

        /// <summary>Matches the HUD's accessibility preference so a large-text player gets a large card.</summary>
        public float FontScale { get; set; } = 1f;

        public bool IsPlaying => playing;

        /// <summary>Creates the takeover as a root object in <paramref name="owner"/>'s scene.</summary>
        public static CeremonyTakeover Attach(GameObject owner)
        {
            var root = new GameObject("Gamesim Ceremony Takeover",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            if (owner != null && owner.scene.IsValid()) SceneManager.MoveGameObjectToScene(root, owner.scene);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the sting's 90: when a beat has both, the scene plays over the running bug.
            canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            return root.AddComponent<CeremonyTakeover>();
        }

        /// <summary>
        /// The veto field. Not a <see cref="CeremonySting"/> kind — it has no strip — but it is a
        /// beat, and it was previously the one phase the episode passed through in silence.
        /// </summary>
        public const string VetoSelectionKind = "veto-selection";

        /// <summary>The title a beat announces itself with, or null when it does not get a card.</summary>
        public static string TitleFor(string kind)
        {
            switch (kind)
            {
                case CeremonySting.NominationKind: return "Nomination Ceremony";
                case CeremonySting.VetoKind: return "Veto Meeting";
                case CeremonySting.EvictionKind: return "Live Eviction";
                case CeremonySting.WinnerKind: return "The Winner";
                case VetoSelectionKind: return "Power of Veto";
                default: return null;
            }
        }

        /// <summary>
        /// The line under the title. Deliberately about the room rather than about the result — the
        /// card opens the scene, and the sting that follows reports what happened in it.
        /// </summary>
        public static string FlavourFor(string kind)
        {
            switch (kind)
            {
                case CeremonySting.NominationKind:
                    return "The keys go up on the memory wall. Two will not get one.";
                case CeremonySting.VetoKind:
                    return "The veto holder decides. Save a nominee, or leave the block as it stands.";
                case CeremonySting.EvictionKind:
                    return "The house falls silent. One of them leaves tonight.";
                case CeremonySting.WinnerKind:
                    return "The jury has spoken.";
                case VetoSelectionKind:
                    // No chips to draw: the engine seats everyone still in the house, and inventing a
                    // draw animation for a selection that does not happen would be theatre for a
                    // decision nobody made.
                    return "Everyone still in the house plays. The winner can take a nominee off the block.";
                default: return string.Empty;
            }
        }

        /// <summary>The generated icon each beat uses, when the set exists.</summary>
        private static string IconFor(string kind)
        {
            switch (kind)
            {
                case CeremonySting.NominationKind: return "target";
                case CeremonySting.VetoKind: return "gavel";
                case CeremonySting.EvictionKind: return "evicted";
                case CeremonySting.WinnerKind: return "trophy";
                case VetoSelectionKind: return "veto-token";
                default: return null;
            }
        }

        private static Color Tint(string kind)
        {
            switch (kind)
            {
                case CeremonySting.NominationKind: return UiTheme.Danger;
                case CeremonySting.VetoKind: return UiTheme.Gold;
                case CeremonySting.EvictionKind: return UiTheme.Danger;
                case CeremonySting.WinnerKind: return UiTheme.Gold;
                case VetoSelectionKind: return UiTheme.Gold;
                default: return UiTheme.Accent;
            }
        }

        /// <summary>
        /// Plays the card. An unrecognised kind is ignored rather than guessed at, matching the
        /// sting: a beat nobody wrote a title for should show nothing, not a blank screen.
        /// </summary>
        public void Play(string kind, int week, IList<Subject> subjects, bool reducedMotion)
        {
            string name = TitleFor(kind);
            if (string.IsNullOrEmpty(name)) return;

            Build();
            reduced = reducedMotion;
            elapsed = 0f;
            playing = true;

            var tint = Tint(kind);
            eyebrow.text = "WEEK " + Mathf.Max(1, week);
            title.text = name;
            flavour.text = FlavourFor(kind);
            // A generated glyph when the icon set exists, and the two-disc mark when it does not.
            var glyph = UiTheme.Icon(IconFor(kind));
            markOuter.GetComponent<Image>().sprite = glyph != null ? glyph : UiTheme.Circle();
            markOuter.GetComponent<Image>().color = tint;
            markOuter.GetComponent<Image>().preserveAspect = glyph != null;
            markInner.gameObject.SetActive(glyph == null);
            markInner.GetComponent<Image>().color = tint;
            title.color = UiTheme.Paper;

            Faces(subjects, tint);

            column.gameObject.SetActive(true);
            scrim.gameObject.SetActive(true);
            Apply(reduced ? 1f : 0f);
        }

        /// <summary>Hides the card immediately, for a scene change or a panel that must own the screen.</summary>
        public void Cancel()
        {
            playing = false;
            if (group != null) group.alpha = 0f;
            if (column != null) column.gameObject.SetActive(false);
            if (scrim != null) scrim.gameObject.SetActive(false);
        }

        /// <summary>
        /// True once the card has been up long enough to have been read, after which a click ends
        /// it. The delay matters: without it a click already in flight when the card appears
        /// dismisses it before anyone has seen what it said.
        /// </summary>
        private bool Dismissable => elapsed >= FadeIn + 0.35f;

        private void Update()
        {
            if (!playing) return;
            CeremonyOverlays.Showing();
            elapsed += Time.unscaledDeltaTime;

            // Read the device directly rather than through the event system. The card carries no
            // GraphicRaycaster and every graphic on it is non-raycasting — an acceptance criterion,
            // because a ceremony must never be able to swallow a click meant for the house. Polling
            // the mouse keeps that true while still letting the card close on demand the way the
            // web build's does.
            if (Dismissable && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            { Cancel(); return; }

            if (reduced)
            {
                // No movement and no fade, but the same time on screen: a player who asked for less
                // motion asked for less motion, not less of the episode.
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

        private void Build()
        {
            if (column != null) { Layout(); return; }

            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var root = (RectTransform)transform;

            // The set stays visible behind the card, but only just. The web build darkens almost to
            // black here and the house reads as a texture rather than as a room; at 0.93 the set was
            // bright enough to compete with the title for attention.
            scrim = NewPanel("Scrim", root, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.975f), 1);
            scrim.anchorMin = Vector2.zero; scrim.anchorMax = Vector2.one;
            scrim.offsetMin = Vector2.zero; scrim.offsetMax = Vector2.zero;

            column = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>();
            column.SetParent(root, false);
            column.anchorMin = new Vector2(.5f, .5f);
            column.anchorMax = new Vector2(.5f, .5f);
            column.pivot = new Vector2(.5f, .5f);

            // The mockups' glass ground behind the whole composition (VISUAL-TARGET.md §4,
            // mockup-08 and -10): the night background at 85 %, a cyan hairline, a soft glow. First
            // child, so every piece of the card draws over it; stretched, so it follows the column
            // as the large-text preference grows it.
            glassGround = NewPanel("Card glass", column, UiTheme.GlassFill, UiTheme.GlassRadius);
            glassGround.anchorMin = Vector2.zero;
            glassGround.anchorMax = Vector2.one;
            UiTheme.Glass(glassGround, UiTheme.GlassRadius);

            eyebrow = NewText("Takeover week", column, 15f, UiTheme.Muted);
            eyebrow.alignment = TextAlignmentOptions.Center;
            eyebrow.characterSpacing = 14f;

            // The mark: two concentric discs. It reads as a target for the block and as a medal for
            // the veto and the win, which is the whole reason it is a shape and not a glyph — the
            // shipped font has no dingbats, and a missing glyph renders as tofu.
            markOuter = Disc("Takeover mark", column, UiTheme.Danger);
            markInner = Disc("Takeover mark core", markOuter, UiTheme.Danger);

            title = NewText("Takeover title", column, 62f, UiTheme.Paper);
            title.alignment = TextAlignmentOptions.Center;

            flavour = NewText("Takeover flavour", column, 20f, UiTheme.Muted);
            flavour.alignment = TextAlignmentOptions.Center;
            flavour.fontStyle = FontStyles.Italic;

            faces = new GameObject("Takeover subjects", typeof(RectTransform)).GetComponent<RectTransform>();
            faces.SetParent(column, false);

            // The web build closes every phase card with a hairline rule and a quiet instruction.
            // It is the thing that tells you the card is waiting for you rather than simply playing
            // at you, and without it a card that also happens to time out reads as a cutscene.
            rule = NewPanel("Takeover rule", column, new Color(UiTheme.Muted.r, UiTheme.Muted.g, UiTheme.Muted.b, 0.35f), 1);
            dismiss = NewText("Takeover dismiss", column, 15f, UiTheme.Muted);
            dismiss.alignment = TextAlignmentOptions.Center;
            dismiss.text = DismissCaption;

            Layout();
        }

        /// <summary>
        /// Stacks the card by measured height rather than at fixed offsets, so the large-text
        /// preference grows the whole composition instead of overlapping it.
        /// </summary>
        private void Layout()
        {
            float scale = Mathf.Max(0.5f, FontScale);
            const float width = 820f;

            eyebrow.fontSize = 15f * scale;
            title.fontSize = 62f * scale;
            flavour.fontSize = 20f * scale;

            float mark = 54f * scale;
            float eyebrowH = 22f * scale;
            float titleH = 76f * scale;
            float flavourH = 54f * scale;
            float facesH = faces.childCount > 0 ? 116f * scale : 0f;
            float gap = 14f * scale;
            float ruleH = 1f;
            float dismissH = 24f * scale;

            dismiss.fontSize = 15f * scale;

            float total = eyebrowH + gap + mark + gap + titleH + flavourH
                + (facesH > 0f ? gap + facesH : 0f)
                + gap * 1.6f + ruleH + gap + dismissH;
            column.sizeDelta = new Vector2(width * scale, total);
            if (glassGround != null)
            {
                glassGround.offsetMin = new Vector2(-36f * scale, -28f * scale);
                glassGround.offsetMax = new Vector2(36f * scale, 28f * scale);
            }

            float y = 0f;
            Place(eyebrow.rectTransform, width * scale, eyebrowH, ref y);
            y -= gap;
            PlaceRect(markOuter, mark, mark, ref y);
            float core = mark * 0.42f;
            markInner.anchorMin = new Vector2(.5f, .5f);
            markInner.anchorMax = new Vector2(.5f, .5f);
            markInner.pivot = new Vector2(.5f, .5f);
            markInner.anchoredPosition = Vector2.zero;
            markInner.sizeDelta = new Vector2(core, core);
            markInner.GetComponent<Image>().color = UiTheme.Ink;
            y -= gap;
            Place(title.rectTransform, width * scale, titleH, ref y);
            Place(flavour.rectTransform, width * scale, flavourH, ref y);
            if (facesH > 0f)
            {
                y -= gap;
                PlaceRect(faces, width * scale, facesH, ref y);
            }

            // A short centred rule, not a full-width one: the web's is about a sixth of the card.
            y -= gap * 1.6f;
            PlaceRect(rule, 132f * scale, ruleH, ref y);
            y -= gap;
            Place(dismiss.rectTransform, width * scale, dismissH, ref y);
        }

        private void Place(RectTransform rect, float width, float height, ref float y)
            => PlaceRect(rect, width, height, ref y);

        private static void PlaceRect(RectTransform rect, float width, float height, ref float y)
        {
            rect.anchorMin = new Vector2(.5f, 1f);
            rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(width, height);
            y -= height;
        }

        /// <summary>Lays the subject portraits out in a centred row, rebuilt per play.</summary>
        private void Faces(IList<Subject> subjects, Color tint)
        {
            for (int i = faces.childCount - 1; i >= 0; i--) Destroy(faces.GetChild(i).gameObject);

            int count = subjects == null ? 0 : subjects.Count;
            if (count == 0) { Layout(); return; }

            float scale = Mathf.Max(0.5f, FontScale);
            float portrait = 78f * scale;
            float slot = 150f * scale;
            float start = -(count - 1) * slot * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var subject = subjects[i];

                var slotRect = new GameObject(subject.Name ?? "Subject", typeof(RectTransform))
                    .GetComponent<RectTransform>();
                slotRect.SetParent(faces, false);
                slotRect.anchorMin = new Vector2(.5f, 1f);
                slotRect.anchorMax = new Vector2(.5f, 1f);
                slotRect.pivot = new Vector2(.5f, 1f);
                slotRect.anchoredPosition = new Vector2(start + i * slot, 0f);
                slotRect.sizeDelta = new Vector2(slot, 116f * scale);

                var rim = Disc("Ring", slotRect, tint);
                rim.anchorMin = new Vector2(.5f, 1f); rim.anchorMax = new Vector2(.5f, 1f);
                rim.pivot = new Vector2(.5f, 1f);
                rim.anchoredPosition = Vector2.zero;
                rim.sizeDelta = new Vector2(portrait + 6f * scale, portrait + 6f * scale);

                var frame = Disc("Frame", slotRect, Color.white);
                frame.anchorMin = new Vector2(.5f, 1f); frame.anchorMax = new Vector2(.5f, 1f);
                frame.pivot = new Vector2(.5f, 1f);
                frame.anchoredPosition = new Vector2(0f, -3f * scale);
                frame.sizeDelta = new Vector2(portrait, portrait);
                frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;

                if (subject.Portrait != null || subject.Character != null)
                {
                    var raw = new GameObject("Face", typeof(RectTransform), typeof(RawImage))
                        .GetComponent<RawImage>();
                    raw.rectTransform.SetParent(frame, false);
                    raw.rectTransform.anchorMin = Vector2.zero;
                    raw.rectTransform.anchorMax = Vector2.one;
                    raw.rectTransform.offsetMin = Vector2.zero;
                    raw.rectTransform.offsetMax = Vector2.zero;
                    raw.texture = subject.Portrait;
                    if (subject.Character != null) CharacterPortraits.Bind(raw, subject.Character);
                    raw.raycastTarget = false;
                }
                else
                {
                    var fill = Disc("Initial", frame, UiTheme.SurfaceRaised);
                    fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
                    fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
                }

                var label = NewText("Subject name", slotRect, 17f * scale, UiTheme.Paper);
                label.alignment = TextAlignmentOptions.Top;
                label.text = subject.Name ?? string.Empty;
                label.rectTransform.anchorMin = new Vector2(.5f, 1f);
                label.rectTransform.anchorMax = new Vector2(.5f, 1f);
                label.rectTransform.pivot = new Vector2(.5f, 1f);
                label.rectTransform.anchoredPosition = new Vector2(0f, -(portrait + 8f * scale));
                label.rectTransform.sizeDelta = new Vector2(slot, 22f * scale);

                if (string.IsNullOrEmpty(subject.Badge)) continue;

                var chip = NewPanel("Subject badge", slotRect, tint, 4);
                chip.anchorMin = new Vector2(.5f, 1f); chip.anchorMax = new Vector2(.5f, 1f);
                chip.pivot = new Vector2(.5f, 1f);
                chip.anchoredPosition = new Vector2(0f, -(portrait + 32f * scale));
                chip.sizeDelta = new Vector2(104f * scale, 21f * scale);

                var badge = NewText("Subject badge text", chip, 12f * scale, UiTheme.Ink);
                badge.alignment = TextAlignmentOptions.Center;
                badge.text = subject.Badge;
                badge.rectTransform.anchorMin = Vector2.zero;
                badge.rectTransform.anchorMax = Vector2.one;
                badge.rectTransform.offsetMin = Vector2.zero;
                badge.rectTransform.offsetMax = Vector2.zero;
            }
            Layout();
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

        private static RectTransform NewPanel(string name, Transform parent, Color color, int radius)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var image = rect.GetComponent<Image>();
            // Always through the theme, never a bare colour on a spriteless Image. That was the
            // scrim bug: with no sprite the fill did not draw, so the card dimmed the HUD panels
            // it sorted above and left the 3D set at full brightness behind them — which looks
            // like a sorting problem and is not one. Radius 1 is the theme's floor and is
            // invisible at full-screen size.
            UiTheme.Style(image, color, Mathf.Max(1, radius));
            image.raycastTarget = false;
            return rect;
        }

        private static TMP_Text NewText(string name, Transform parent, float size, Color color)
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
            label.richText = false;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Truncate;
            return label;
        }
    }
}
