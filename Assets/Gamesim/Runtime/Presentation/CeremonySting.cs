using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// A broadcast title card for the four beats the episode already models but previously rendered
    /// as a plain status-line swap: the nomination ceremony, the veto meeting, an eviction, and the
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
    ///
    /// <para>It is drawn on the kit the other ceremony cards are (UI-UX-PASS-PLAN V0, decision 14):
    /// the glass every card stands on, a medallion for the beat at its left - Pack 8's veto mark for
    /// the veto meeting, as the meeting's screen and its card wear it, and otherwise the beat's
    /// glyph in a ring of the beat's colour, on Pack 9's portrait ring - and the beat's name beside
    /// it, set as the meeting's screen sets its title. The accent bar it used to lead with was the
    /// HUD's older banner, beside cards that had all moved on.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CeremonySting : MonoBehaviour
    {
        // Event kinds, as the simulation records them on EpisodeEvent.kind.
        public const string NominationKind = "nomination";
        public const string VetoKind = "veto";
        public const string EvictionKind = "eviction";
        public const string WinnerKind = "winner";
        /// <summary>The final Head of Household choosing who sits beside them: the engine's own line for it.</summary>
        public const string FinalEvictionKind = "final-eviction";
        /// <summary>The evicted walking out through the front door: not a logged ceremony, the house's goodbye.</summary>
        public const string WalkOutKind = "walk-out";

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
        private const float RightInset = 24f + Episode.EpisodeHud.RightColumnWidth + 16f;  // column margin, width, gap
        // Under the top bar: the objective is a chip on the band now, and the card may cover no
        // part of it.
        private const float TopInset = 80f;
        private const float HeadlineSize = 34f;
        private const float DetailSize = 19f;
        private const float TopPad = 14f, Gap = 6f, BottomPad = 14f;

        /// <summary>
        /// The medallion's side at the resting text size, its inset from the card's left edge, the
        /// room between it and the words, and the room the words leave at the right.
        /// </summary>
        private const float MarkSize = 58f, MarkInset = 20f, MarkGap = 16f, RightPad = 28f;

        /// <summary>
        /// Pack 9's portrait ring as shares of its side: the ring's outer edge (24 to 233 of 256) and
        /// its hole (37 to 219), which the medallion's core fills.
        /// </summary>
        private const float RingOuter = 0.816f, RingHole = 0.711f;

        private CanvasGroup group;
        private RectTransform card, mark;
        private TMP_Text headline, detail;
        private Image ring, core, glyph;
        private float markSide;
        private Vector2 restPosition;
        private float elapsed;
        private bool playing, reduced;
        private string playingKind;

        /// <summary>Matches the HUD's accessibility preference so a large-text player gets a large card.</summary>
        public float FontScale { get; set; } = 1f;

        /// <summary>Whether the card is still on its own timer, as every sibling card reports.</summary>
        public bool IsPlaying => playing;

        /// <summary>
        /// The kind on screen, or null when no card is playing: what the director asks to keep the
        /// status line from saying the same beat beside it (UI-UX-PASS-PLAN V0).
        /// </summary>
        public string PlayingKind => playing ? playingKind : null;

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
            kind == NominationKind || kind == VetoKind || kind == EvictionKind || kind == WinnerKind || kind == FinalEvictionKind || kind == WalkOutKind;

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
            playingKind = kind;

            Layout();
            var tint = Tint(kind);
            headline.text = HeadlineFor(kind);
            headline.color = tint;
            detail.text = body ?? string.Empty;
            DressMark(kind, tint);

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

        /// <summary>
        /// The beat's name on the strip. The veto meeting is the veto meeting here as everywhere else
        /// it is named - the card, its screen, the episode screen's band, the week chip - where the
        /// strip called it a ceremony (UI-UX-PASS-PLAN V0, decision 14: one meeting, one name).
        /// </summary>
        public static string HeadlineFor(string kind)
        {
            switch (kind)
            {
                case NominationKind: return "NOMINATION CEREMONY";
                case VetoKind: return "VETO MEETING";
                case EvictionKind: return "EVICTION";
                case WinnerKind: return "THE WINNER";
                case FinalEvictionKind: return "THE FINAL TWO";
                case WalkOutKind: return "GOODBYE";
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
                case FinalEvictionKind: return UiTheme.Gold;
                default: return UiTheme.Accent;
            }
        }

        /// <summary>
        /// The veto meeting's own mark - Pack 8's veto medallion, its gold ring and dark core drawn
        /// in - which the meeting's screen titles itself with and its card leads with, so the
        /// meeting wears one mark wherever it is announced (UI-UX-PASS-PLAN V0). Null for every other
        /// beat, and without the pack.
        /// </summary>
        public static Sprite AuthoredMark(string kind) => kind == VetoKind ? UiTheme.Pack(PackArt.Pack8IconVeto) : null;

        /// <summary>
        /// The beat's glyph in its medallion: Pack 9's where the ceremony and finale pack drew one -
        /// white, drawn in the beat's colour - and the generated set's otherwise; null with neither.
        /// <paramref name="fromPack"/> says which, since a pack icon carries clear padding round its
        /// drawing and the generated set draws to its edges.
        /// </summary>
        private static Sprite Glyph(string kind, out bool fromPack)
        {
            string pack = null, generated = null;
            switch (kind)
            {
                case NominationKind: pack = PackArt.Pack9IconsIcKey; generated = "key"; break;
                case VetoKind: generated = "veto-token"; break;
                case EvictionKind: pack = PackArt.Pack9IconsIcBallot; generated = "evicted"; break;
                case WinnerKind: pack = PackArt.Pack9IconsIcTrophy; generated = "trophy"; break;
                case FinalEvictionKind: pack = PackArt.Pack9IconsIcPeople; generated = "people"; break;
                case WalkOutKind: pack = PackArt.Pack9IconsIcHome; generated = "exit"; break;
            }
            var sprite = UiTheme.Pack(pack);
            fromPack = sprite != null;
            return fromPack ? sprite : UiTheme.Icon(generated);
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

            // The mockups' running bug is a glass strip: the night ground at 85 %, a cyan hairline
            // on the edge and a soft glow outside it (mockup-08, -10). The glow is a child that
            // reaches past the rect, and every geometry check in the suite reads the rect.
            card = HudPrimitives.Glass("Card", transform);
            // Stretched across the top, inset past the chrome on both sides.
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(1f, 1f);
            card.pivot = new Vector2(0.5f, 1f);

            // The beat's medallion at the card's left, where the accent bar stood: a ring of the
            // beat's colour round a dark core with the beat's glyph on it, or the veto meeting's own
            // mark whole. Three layers, dressed per play (DressMark), since one strip plays every beat.
            mark = new GameObject("Sting mark", typeof(RectTransform)).GetComponent<RectTransform>();
            mark.SetParent(card, false);
            mark.anchorMin = mark.anchorMax = new Vector2(0f, .5f);
            mark.pivot = new Vector2(0f, .5f);
            ring = MarkLayer("Mark ring", mark);
            core = MarkLayer("Mark core", mark);
            glyph = MarkLayer("Mark glyph", mark);

            // The beat's name as the meeting's screen sets its title: the bold cut, tracked, in the
            // beat's colour.
            headline = HudPrimitives.Label("Sting headline", card, HeadlineSize, UiTheme.Accent);
            headline.overflowMode = TextOverflowModes.Overflow;
            headline.textWrappingMode = TextWrappingModes.NoWrap;
            headline.characterSpacing = 2f;
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) headline.font = bold;
            detail = HudPrimitives.Label("Sting detail", card, DetailSize, UiTheme.Paper);

            card.gameObject.SetActive(false);
        }

        /// <summary>One of the medallion's layers, centred on it, inert like everything on the card.</summary>
        private static Image MarkLayer(string name, RectTransform parent)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = image.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// The medallion for this beat: the veto meeting's own mark whole where the pack is in
        /// (<see cref="AuthoredMark"/>); otherwise Pack 9's portrait ring in the beat's colour - the
        /// kit's plain disc of the colour without the pack - a dark core filling its hole, as the
        /// veto mark's own core is dark, and the beat's glyph on the core.
        /// </summary>
        private void DressMark(string kind, Color tint)
        {
            var authored = AuthoredMark(kind);
            if (authored != null)
            {
                Layer(ring, authored, Color.white, markSide);
                core.enabled = false;
                glyph.enabled = false;
                return;
            }
            var art = UiTheme.Pack(PackArt.Pack9SharedPortraitRing);
            if (art != null) Layer(ring, art, tint, markSide);
            else Layer(ring, UiTheme.Circle(), tint, markSide * RingOuter);
            Layer(core, UiTheme.Circle(), new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .92f), markSide * RingHole);
            var icon = Glyph(kind, out bool fromPack);
            if (icon == null) { glyph.enabled = false; return; }
            Layer(glyph, icon, tint, markSide * (fromPack ? .5f : .4f));
        }

        private static void Layer(Image image, Sprite sprite, Color colour, float side)
        {
            image.enabled = true;
            image.sprite = sprite;
            image.color = colour;
            image.rectTransform.sizeDelta = new Vector2(side, side);
        }

        /// <summary>
        /// Sizes the card around its text rather than around a fixed rectangle, so raising the
        /// large-text preference grows the card instead of clipping the headline inside it.
        /// </summary>
        private void Layout()
        {
            float head = Mathf.Round(HeadlineSize * FontScale);
            float body = Mathf.Round(DetailSize * FontScale);
            // Inter's line is taller than 1.2 of its size, and a headline in a box one pixel short of
            // its line is truncated whole: every sting drew its detail under an empty band. The kit's
            // rule is a box 1.3 times its type, the detail's included (it was 1.25).
            float headLine = Mathf.Ceil(head * 1.35f);
            float bodyLine = Mathf.Ceil(body * 1.32f);
            float height = TopPad + headLine + Gap + bodyLine + BottomPad;

            card.offsetMin = new Vector2(LeftInset, -(TopInset + height));
            card.offsetMax = new Vector2(-RightInset, -TopInset);
            restPosition = card.anchoredPosition;

            // The medallion grows with the type, and never past the card's height less a margin over
            // and under it; the words start a gap to its right.
            markSide = Mathf.Min(Mathf.Round(MarkSize * FontScale), height - 24f);
            mark.anchoredPosition = new Vector2(MarkInset, 0f);
            mark.sizeDelta = new Vector2(markSide, markSide);
            float left = MarkInset + markSide + MarkGap;

            // The name on its one line, drawn a size or two smaller rather than run past the card's
            // edge: on the 4:3 canvas the strip is 525 wide, and NOMINATION CEREMONY at the larger
            // text is wider than the words' room beside the medallion.
            headline.fontSize = head;
            headline.enableAutoSizing = true;
            headline.fontSizeMax = head;
            headline.fontSizeMin = Mathf.Round(head * 0.6f);
            Stretch(headline.rectTransform, left, TopPad, RightPad, height - TopPad - headLine);

            // Committed event text varies a lot in length and the card is only one line deep, so the
            // detail is allowed to shrink rather than truncate. A slightly smaller sentence is a far
            // better outcome than a name cut in half at the moment someone is evicted.
            detail.fontSize = body;
            detail.enableAutoSizing = true;
            detail.fontSizeMax = body;
            detail.fontSizeMin = Mathf.Max(11f, body * 0.7f);
            Stretch(detail.rectTransform, left, TopPad + headLine + Gap, RightPad, BottomPad);
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

        private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
