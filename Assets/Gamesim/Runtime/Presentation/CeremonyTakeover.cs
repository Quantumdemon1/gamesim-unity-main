using System.Collections.Generic;
using System.Globalization;
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
    ///
    /// <para>The veto meeting also plays on the living room's screen when the house stages it
    /// (<see cref="PlayVetoMeeting"/>, PACK8-PASS-PLAN C1): the same canvas hung on the screen's
    /// face, turning pages the stage in the house cuts with - the holder, the question, the
    /// decision, the replacement, the block that goes to the vote. Only a staged meeting does; the
    /// HUD's card, a batch run's and reduced motion's play exactly as they always did.</para>
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

        /// <summary>
        /// What the key ceremony and the live eviction say speeds them up and skips them, on a
        /// keyboard. A caption other code and tests identify the line by, like <see cref="DismissCaption"/>.
        /// </summary>
        public const string ControlsCaption = "Space  Speed up   ·   Enter  Skip";

        /// <summary>The same line for a player on a pad: X speeds a reveal up, A skips it.</summary>
        public const string PadControlsCaption = "X  Speed up   ·   A  Skip";

        /// <summary>What a reveal says in its corner while it is sped up.</summary>
        public static string SpeedCaption =>
            "Fast-forward ×" + CeremonyPacing.SpeedUp.ToString("0.#", CultureInfo.InvariantCulture);

        /// <summary>The controls line for the device last used on a card.</summary>
        public static string ControlsFor(bool pad) => pad ? PadControlsCaption : ControlsCaption;

        /// <summary>
        /// A press that moves a ceremony card on: a left click, Enter (either one), Escape, or the
        /// pad's A or B - the Ceremony map's Skip. Read off the actions, never through the event
        /// system, so the rule in the class notes holds for every card that uses it - a card cannot
        /// take a click, or a Submit, that was meant for the house, and nothing can be stranded
        /// behind one.
        /// </summary>
        internal static bool SkipPressed() => Gamesim.House.HouseInput.Actions.Skip.WasPressedThisFrame();

        /// <summary>A press that speeds a reveal up, or back to its own pace: Space, or the pad's X (the Ceremony map's Speed).</summary>
        internal static bool SpeedPressed() => Gamesim.House.HouseInput.Actions.Speed.WasPressedThisFrame();

        /// <summary>
        /// Where this frame's press came from, for the key hints to follow: true for the pad, false
        /// for a key or a click, null when nothing was pressed.
        /// </summary>
        internal static bool? PadUsed() => Gamesim.House.HouseInput.PadUsed();

        /// <summary>
        /// The card up and the card down, so a stage in the house can cut with it as it does with
        /// the reveals; and on the veto meeting's screen, its decision and its replacement between them.
        /// </summary>
        public event System.Action<CeremonyBeat> BeatReached;

        private RectTransform rule, glassGround;
        private TMP_Text dismiss;
        private CanvasGroup group;
        private RectTransform column, scrim, faces;
        private TMP_Text eyebrow, title, flavour;
        private RectTransform markOuter, markInner;
        private float elapsed;
        private bool playing, reduced;

        /// <summary>
        /// The frame the card was played in. Neither it nor the next is the card's own time, nor the
        /// meeting's on a set's screen: the first update's delta can be the frame before the card was
        /// played, and the next's is the frame it was played in - the commit, its save and the
        /// house's render - before anybody had seen it. Counted, a long one spent the card's reading
        /// time, its fade in first, before it was drawn (UI-UX-PASS-PLAN V0).
        /// </summary>
        private int playedFrame;

        /// <summary>Matches the HUD's accessibility preference so a large-text player gets a large card.</summary>
        public float FontScale { get; set; } = 1f;

        public bool IsPlaying => playing;

        /// <summary>
        /// Holds the card where it is: its clock stops, so nothing fades, turns a page or ends, and no
        /// press moves it on, while the click guard stays up. A hold taken as a page turns - by a
        /// handler of that page's beat - stops the meeting on that page. For a frame taken of one
        /// moment, which the card's unscaled clock would otherwise walk past between the moment and the
        /// capture, as <see cref="VoteReveal.Held"/> holds the count. Cleared by the next play.
        /// </summary>
        public bool Held { get; set; }

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

        /// <summary>
        /// The house down to three: not a ceremony the engine logs, but the finale's opening, as the
        /// reference opens it with its own card ("Only three remain...").
        /// </summary>
        public const string FinalThreeKind = "final-three";

        /// <summary>
        /// The final Head of Household's bracket (ENDGAME-PLAN F3): the card that opens Part 2 and
        /// Part 3, who won the part before and who plays now. Its title and line are the bracket's,
        /// passed with the card.
        /// </summary>
        public const string FinalHoHPartKind = "final-hoh-part";

        /// <summary>The final Head of Household crowned: the card on the commit that enters the final eviction.</summary>
        public const string FinalHoHCrownedKind = "final-hoh-crowned";

        /// <summary>The kind on screen, or null when no card is playing. A read for tests.</summary>
        public string PlayingKind => playing ? playingKind : null;
        private string playingKind;

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
                case FinalThreeKind: return "The Final Three";
                case FinalHoHPartKind: return "Final HoH";
                case FinalHoHCrownedKind: return "Final Head of Household";
                case CeremonySting.FinalEvictionKind: return "The Final Two";
                // The story's ceremonies (plan §5.1) keep their own table.
                default: return StoryFallout.TitleFor(kind);
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
                    // True only when the field takes the whole house, at six or fewer; a bigger
                    // house's card is given the director's VetoFieldLine instead, which says who
                    // plays by right and how many the draw added.
                    return "Everyone still in the house plays. The winner can take a nominee off the block.";
                case FinalThreeKind:
                    // The reference's line for the final Head of Household's card.
                    return "Only three remain. The final battle for power begins now.";
                case CeremonySting.FinalEvictionKind:
                    return "The final Head of Household chooses who sits beside them. The other joins the jury.";
                case FinalHoHPartKind: return "The bracket moves on.";
                case FinalHoHCrownedKind: return "One decision remains.";
                default: return StoryFallout.FlavourFor(kind) ?? string.Empty;
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
                case FinalThreeKind: return "trophy";
                case FinalHoHPartKind: return "crown";
                case FinalHoHCrownedKind: return "crown";
                case CeremonySting.FinalEvictionKind: return "trophy";
                default: return StoryFallout.IconFor(kind);
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
                case FinalThreeKind: return UiTheme.Gold;
                case FinalHoHPartKind: return UiTheme.Gold;
                case FinalHoHCrownedKind: return UiTheme.Gold;
                case CeremonySting.FinalEvictionKind: return UiTheme.Gold;
                default: return StoryFallout.TintFor(kind);
            }
        }

        /// <summary>
        /// Plays the card. An unrecognised kind is ignored rather than guessed at, matching the
        /// sting: a beat nobody wrote a title for should show nothing, not a blank screen.
        /// </summary>
        public void Play(string kind, int week, IList<Subject> subjects, bool reducedMotion) =>
            Play(kind, week, subjects, reducedMotion, null, null);

        /// <summary>
        /// Plays the card with a title and a line of its own in place of the kind's: the final Head
        /// of Household's bracket and crowning say who, and a kind's table cannot. Either left null
        /// keeps the kind's. A kind with no title in the tables still shows nothing.
        /// </summary>
        public void Play(string kind, int week, IList<Subject> subjects, bool reducedMotion, string titleText, string line)
        {
            if (string.IsNullOrEmpty(TitleFor(kind))) return;
            // A card after a meeting on the set's screen plays where every card did: on the HUD frame.
            LeaveTheScreen();
            string name = string.IsNullOrEmpty(titleText) ? TitleFor(kind) : titleText;
            playingKind = kind;

            Build();
            reduced = reducedMotion;
            elapsed = 0f;
            playedFrame = Time.frameCount;
            Held = false;
            playing = true;

            var tint = Tint(kind);
            eyebrow.text = "WEEK " + Mathf.Max(1, week);
            title.text = name;
            flavour.text = string.IsNullOrEmpty(line) ? FlavourFor(kind) : line;
            // The veto meeting leads with its own mark, drawn as authored, as its screen and the strip
            // reporting it do (UI-UX-PASS-PLAN V0): one meeting, one mark. Every other beat has a
            // generated glyph when the icon set exists, and the two-disc mark when it does not.
            var authored = CeremonySting.AuthoredMark(kind);
            var glyph = authored != null ? authored : UiTheme.Icon(IconFor(kind));
            var markImage = markOuter.GetComponent<Image>();
            markImage.sprite = glyph != null ? glyph : UiTheme.Circle();
            markImage.color = authored != null ? Color.white : tint;
            markImage.preserveAspect = glyph != null;
            // At the strip's scale for the veto mark, so its medallion is the size the strip's is.
            float markScale = authored != null ? CeremonySting.VetoMarkScale : 1f;
            markOuter.localScale = new Vector3(markScale, markScale, 1f);
            markInner.gameObject.SetActive(glyph == null);
            markInner.GetComponent<Image>().color = tint;
            title.color = Color.white;

            Faces(subjects, tint);

            column.gameObject.SetActive(true);
            scrim.gameObject.SetActive(true);
            Apply(reduced ? 1f : 0f);
            BeatReached?.Invoke(new CeremonyBeat(CeremonyBeatKind.Opened));
        }

        /// <summary>Hides the card immediately, for a scene change or a panel that must own the screen.</summary>
        public void Cancel()
        {
            bool was = playing;
            playing = false;
            if (group != null) group.alpha = 0f;
            if (column != null) column.gameObject.SetActive(false);
            if (scrim != null) scrim.gameObject.SetActive(false);
            if (meeting != null)
            {
                // Off the set's screen and its idle graphic back: the screen is the room's again.
                // The meeting is kept until the next card, so whoever cuts with its beats still
                // knows which screen this last one was on (MeetingScreen).
                if (meetingRoot != null) meetingRoot.gameObject.SetActive(false);
                if (mounted) { ScreenSurface.Unmount(GetComponent<Canvas>()); mounted = false; }
                if (was) BeatReached?.Invoke(new CeremonyBeat(CeremonyBeatKind.Closed, -1, null, elapsed < MeetingEnd));
                return;
            }
            if (was) BeatReached?.Invoke(new CeremonyBeat(CeremonyBeatKind.Closed, -1, null, elapsed < FadeIn + Hold));
        }

        /// <summary>
        /// True once the card has been up long enough to have been read, after which a press ends
        /// it. The delay matters: without it a click - or the Enter that committed the beat - already
        /// in flight when the card appears dismisses it before anyone has seen what it said.
        /// </summary>
        private bool Dismissable => elapsed >= FadeIn + 0.35f;

        private void Update()
        {
            if (!playing) return;
            CeremonyOverlays.Showing();
            if (Held) return;
            // Nothing from before its first frame on screen is the card's time (playedFrame).
            if (Time.frameCount - playedFrame <= 1) return;
            elapsed += Time.unscaledDeltaTime;
            // The meeting on the set's screen keeps its own pages; the HUD's card below is as it was.
            if (meeting != null) { TickMeeting(); return; }

            // Read the devices directly rather than through the event system. The card carries no
            // GraphicRaycaster and every graphic on it is non-raycasting — an acceptance criterion,
            // because a ceremony must never be able to swallow a click meant for the house. Polling
            // the devices keeps that true while still letting the card close on demand the way the
            // web build's does: a click, Enter, Escape, or the pad's A or B.
            if (Dismissable && SkipPressed()) { Cancel(); return; }

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
            // Dimmed and vignetted, so the room the ceremony is in stays a room behind the card.
            scrim = NewPanel("Scrim", root, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.55f), 1);
            scrim.anchorMin = Vector2.zero; scrim.anchorMax = Vector2.one;
            scrim.offsetMin = Vector2.zero; scrim.offsetMax = Vector2.zero;
            HudPrimitives.Vignette(scrim);

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

            // The beat's name in the bold cut, lit from above as every title in the mockups is; the
            // regular weight at this size read as a caption blown up rather than as a title card.
            title = NewText("Takeover title", column, 56f, UiTheme.Paper);
            title.alignment = TextAlignmentOptions.Center;
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) title.font = bold;
            title.characterSpacing = 1f;
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(Color.white, Color.white, UiTheme.Glow, UiTheme.Glow);

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
            title.fontSize = 56f * scale;
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
                // 1.35 of the type: Inter's line is 1.21 of its size and the 22 box was 1.29, drawn
                // only because a name has few descenders; the chip below keeps a unit of air.
                label.rectTransform.sizeDelta = new Vector2(slot, 23f * scale);

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

        // ------------------------------------------------------------ the veto meeting on the set's screen

        /// <summary>The meeting's pages, in the order the card turns them.</summary>
        public enum MeetingPage { Intro, Question, Decision, Replacement, Final }

        /// <summary>
        /// How long past its fade the meeting must have been up, in real seconds, before a press moves
        /// it on: the reveals' delay, and on the same clock as theirs, so a skip that jumps the card's
        /// own clock to its last page does not also make the press that jumped it a second one.
        /// </summary>
        private const float MeetingReadDelay = 0.35f;

        /// <summary>The badges the meeting's faces wear.</summary>
        public const string VetoBadge = "VETO", OnTheBlockBadge = "ON THE BLOCK", SavedBadge = "SAVED", ReplacementBadge = "REPLACEMENT";

        /// <summary>
        /// The meeting's title on the screen: the veto meeting, the name the strip, the card, the
        /// episode screen and the week chip give it (UI-UX-PASS-PLAN V0, decision 14). It was
        /// POWER OF VETO MEETING here while the strip called the same meeting a ceremony.
        /// </summary>
        public const string MeetingTitle = "VETO MEETING";

        private VetoMeetingScript meeting;
        private ScreenSurface surface;
        private bool mounted;
        private MeetingPage page;
        private CeremonyPace meetingPace = CeremonyPace.Suspenseful;
        private float upFor;
        private RectTransform meetingRoot, meetingFaces;
        private TMP_Text meetingWeek, meetingHeadline, meetingLine;

        /// <summary>Whether the veto meeting is playing on a set's screen.</summary>
        public bool PlayingMeeting => playing && meeting != null;

        /// <summary>The screen the card is playing on, or null while it plays on the HUD or not at all.</summary>
        public ScreenSurface Surface => playing ? surface : null;

        /// <summary>
        /// The screen the last veto meeting played on, kept until the next card: a stage cutting with
        /// the meeting's beats reads it to tell the meeting's from another card's, the closing beat
        /// included. Null once any other card has played.
        /// </summary>
        public ScreenSurface MeetingScreen => meeting != null ? surface : null;

        /// <summary>The meeting's page on the screen, or null when no meeting is playing.</summary>
        public MeetingPage? Page => PlayingMeeting ? page : (MeetingPage?)null;

        /// <summary>The pace the meeting was played at.</summary>
        public CeremonyPace MeetingPace => meetingPace;

        /// <summary>How long the meeting takes at its own pace, start to finish, when nobody skips it.</summary>
        public float MeetingDuration => meeting != null ? MeetingEnd + CeremonyPacing.FadeOut : 0f;

        private bool MeetingHasReplacement => meeting != null && meeting.Meeting.HasReplacement;
        private float QuestionAt => CeremonyPacing.FadeIn + CeremonyPacing.VetoIntro(meetingPace);
        private float DecisionAt => QuestionAt + CeremonyPacing.VetoQuestion(meetingPace);
        private float ReplacementAt => DecisionAt + CeremonyPacing.VetoDecision(meetingPace);
        private float FinalAt => ReplacementAt + (MeetingHasReplacement ? CeremonyPacing.VetoReplacement(meetingPace) : 0f);
        private float MeetingEnd => FinalAt + CeremonyPacing.VetoFinal(meetingPace);

        /// <summary>
        /// Plays the veto meeting on a set's screen, page by page: the holder and the block, the
        /// question, the decision, the replacement when there is one, and the block that goes to the
        /// vote. Every line is the script's, read from committed state and the block before the
        /// commit, never from the event's sentence. Declines - and the caller plays the generic card
        /// on the HUD - without a screen, or a meeting it cannot tell; the HUD, a batch run and
        /// reduced motion never come here.
        /// </summary>
        public bool PlayVetoMeeting(VetoMeetingScript script, bool reducedMotion, CeremonyPace pace, ScreenSurface screen)
        {
            if (screen == null || script == null || !script.Playable) return false;
            playingKind = CeremonySting.VetoKind;
            // The HUD's pieces are built for the group they share, and kept down while the meeting plays.
            Build();
            column.gameObject.SetActive(false);
            scrim.gameObject.SetActive(false);
            meeting = script;
            surface = screen;
            meetingPace = pace;
            BuildMeeting();
            // Off any screen it was still on first, so that screen's idle graphic comes back.
            var canvas = GetComponent<Canvas>();
            if (mounted) ScreenSurface.Unmount(canvas);
            screen.Mount(canvas);
            mounted = true;
            reduced = reducedMotion;
            elapsed = 0f;
            upFor = 0f;
            page = MeetingPage.Intro;
            playedFrame = Time.frameCount;
            Held = false;
            playing = true;
            meetingWeek.text = "WEEK " + Mathf.Max(1, script.Meeting.week);
            ShowPage(MeetingPage.Intro);
            meetingRoot.gameObject.SetActive(true);
            group.alpha = reduced ? 1f : 0f;
            BeatReached?.Invoke(new CeremonyBeat(CeremonyBeatKind.Opened));
            return true;
        }

        /// <summary>
        /// The first press's step on the meeting: straight to the block that goes to the vote, the
        /// decision and the replacement reported on the way as skipped - the order given up, never
        /// the outcome. A press on that last page ends the card. Shared with a staged meeting, whose
        /// press on its summons starts the card here, so one press is one step however the meeting
        /// is played. Nothing once the last page is up, or on the HUD's card.
        /// </summary>
        public void SkipToResult()
        {
            if (!playing || meeting == null || page == MeetingPage.Final) return;
            elapsed = Mathf.Max(elapsed, FinalAt);
            Turn(MeetingPage.Final, true);
        }

        private void TickMeeting()
        {
            upFor += Time.unscaledDeltaTime;
            // The card says nothing of its controls on the screen: the skip chip names the press.
            if (upFor >= CeremonyPacing.FadeIn + MeetingReadDelay && SkipPressed())
            {
                if (page == MeetingPage.Final) { Cancel(); return; }
                SkipToResult();
                return;
            }
            float end = MeetingEnd;
            if (reduced)
            {
                // Reading time, not movement: the same time on screen with no fade.
                group.alpha = 1f;
                if (elapsed >= end + CeremonyPacing.FadeOut) { Cancel(); return; }
            }
            else if (elapsed < CeremonyPacing.FadeIn) group.alpha = Eased(elapsed / CeremonyPacing.FadeIn);
            else if (elapsed < end) group.alpha = 1f;
            else
            {
                float exit = (elapsed - end) / CeremonyPacing.FadeOut;
                if (exit >= 1f) { Cancel(); return; }
                group.alpha = 1f - Eased(exit);
            }
            var due = PageAt(elapsed);
            if (due > page) Turn(due, false);
        }

        private static float Eased(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>The page the card's clock has reached.</summary>
        private MeetingPage PageAt(float t)
        {
            if (t >= FinalAt) return MeetingPage.Final;
            if (MeetingHasReplacement && t >= ReplacementAt) return MeetingPage.Replacement;
            if (t >= DecisionAt) return MeetingPage.Decision;
            if (t >= QuestionAt) return MeetingPage.Question;
            return MeetingPage.Intro;
        }

        /// <summary>
        /// Turns to <paramref name="to"/>, reporting every beat on the way - the decision, the
        /// replacement - marked <paramref name="skipped"/> when a press took the card past them.
        /// </summary>
        private void Turn(MeetingPage to, bool skipped)
        {
            var read = meeting.Meeting;
            bool heldBefore = Held;
            while (page < to)
            {
                page++;
                if (page == MeetingPage.Replacement && !read.HasReplacement) continue;
                if (page == MeetingPage.Decision)
                    BeatReached?.Invoke(new CeremonyBeat(CeremonyBeatKind.VetoDecided, -1, read.used ? read.savedId : null, skipped));
                else if (page == MeetingPage.Replacement)
                    BeatReached?.Invoke(new CeremonyBeat(CeremonyBeatKind.ReplacementNamed, -1, read.replacementId, skipped));
                // A handler that took the card down has the screen now.
                if (!playing || meeting == null) return;
                // A handler that held the card keeps it on the page its beat turned to (Held), however
                // far the clock or a skip was taking it.
                if (Held && !heldBefore) break;
            }
            ShowPage(page);
        }

        /// <summary>A face on a page, with what it is doing there: a badge, and a second for a holder on the block.</summary>
        private readonly struct MeetingFace
        {
            public readonly string Id, Badge, Second;
            public readonly Color Tint, SecondTint;

            public MeetingFace(string id, string badge, Color tint, string second = null, Color secondTint = default)
            {
                Id = id; Badge = badge; Tint = tint; Second = second; SecondTint = secondTint;
            }
        }

        /// <summary>Draws a page: its headline, its line and its faces, every one read from the script.</summary>
        private void ShowPage(MeetingPage shown)
        {
            var read = meeting.Meeting;
            string headline = null, line = null;
            var headlineColour = UiTheme.Paper;
            var row = new List<MeetingFace>();
            bool arrow = false;
            switch (shown)
            {
                case MeetingPage.Intro:
                case MeetingPage.Question:
                    headline = shown == MeetingPage.Intro ? read.introLine : read.questionLine;
                    // The holder, then the block; a holder on the block is one card with both badges.
                    if (read.holderId != null && !read.holderOnTheBlock) row.Add(new MeetingFace(read.holderId, VetoBadge, UiTheme.Gold));
                    foreach (var id in read.blockBefore)
                        row.Add(id == read.holderId
                            ? new MeetingFace(id, VetoBadge, UiTheme.Gold, OnTheBlockBadge, UiTheme.Danger)
                            : new MeetingFace(id, OnTheBlockBadge, UiTheme.Danger));
                    break;
                case MeetingPage.Decision:
                    headline = read.decisionHeadline;
                    headlineColour = read.used ? UiTheme.Gold : UiTheme.Accent;
                    line = read.decisionLine;
                    foreach (var id in read.blockBefore)
                        row.Add(read.used && id == read.savedId ? new MeetingFace(id, SavedBadge, UiTheme.Gold) : new MeetingFace(id, OnTheBlockBadge, UiTheme.Danger));
                    break;
                case MeetingPage.Replacement:
                    headline = VetoMeetingRead.ReplacementHeadline;
                    headlineColour = UiTheme.Danger;
                    line = read.replacementLine;
                    row.Add(new MeetingFace(read.savedId, SavedBadge, UiTheme.Gold));
                    row.Add(new MeetingFace(read.replacementId, ReplacementBadge, UiTheme.Danger));
                    arrow = true;
                    break;
                case MeetingPage.Final:
                    headline = VetoMeetingRead.FinalHeadline;
                    headlineColour = UiTheme.Gold;
                    line = read.outcomeLine;
                    // The replacement keeps the pill the page before gave them, under the block's
                    // (UI-UX-PASS-PLAN V0): the block that goes to the vote says who went up in whose
                    // place, as the meeting's row on the episode screen does. Named by now - a skip
                    // reports the naming on its way here - and never on a page before the naming.
                    foreach (var id in read.finalBlock)
                        row.Add(read.HasReplacement && id == read.replacementId
                            ? new MeetingFace(id, OnTheBlockBadge, UiTheme.Danger, ReplacementBadge, UiTheme.Danger)
                            : new MeetingFace(id, OnTheBlockBadge, UiTheme.Danger));
                    break;
            }
            meetingHeadline.text = headline ?? string.Empty;
            meetingHeadline.color = headlineColour;
            meetingLine.text = line ?? string.Empty;
            MeetingFaces(row, arrow);
        }

        /// <summary>The screen's frame, in its own canvas units: the face's whole 1200 × 800, type at about 2.2 times the HUD card's.</summary>
        private const float FaceWidth = 300f, FaceHeight = 380f, FaceStep = 360f, ArrowSide = 92f;

        private void BuildMeeting()
        {
            if (meetingRoot != null) return;
            var root = (RectTransform)transform;
            meetingRoot = new GameObject("Veto meeting", typeof(RectTransform)).GetComponent<RectTransform>();
            meetingRoot.SetParent(root, false);
            meetingRoot.anchorMin = Vector2.zero; meetingRoot.anchorMax = Vector2.one;
            meetingRoot.offsetMin = Vector2.zero; meetingRoot.offsetMax = Vector2.zero;

            // The screen's ground: the mockups' night glass, edged in the veto's gold.
            var ground = NewPanel("Meeting glass", meetingRoot, UiTheme.GlassFill, UiTheme.GlassRadius);
            ground.anchorMin = Vector2.zero; ground.anchorMax = Vector2.one;
            ground.offsetMin = Vector2.zero; ground.offsetMax = Vector2.zero;
            UiTheme.AddBorder(ground, UiTheme.GlassRadius, UiTheme.Gold);

            // Every label box at least 1.3 of its font: Inter's line is 1.21 of its size, and a box
            // short of it draws nothing.
            meetingWeek = NewText("Meeting week", meetingRoot, 24f, UiTheme.Muted);
            meetingWeek.alignment = TextAlignmentOptions.Center;
            meetingWeek.characterSpacing = 10f;
            PlaceTop(meetingWeek.rectTransform, 1100f, 36f, -26f);

            var title = NewText("Meeting title", meetingRoot, 44f, UiTheme.Gold);
            title.alignment = TextAlignmentOptions.Center;
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) title.font = bold;
            title.characterSpacing = 3f;
            title.text = MeetingTitle;
            PlaceTop(title.rectTransform, 1100f, 64f, -64f);
            // Pack 8's veto mark before the title, when the pack is in.
            var mark = UiTheme.Pack(PackArt.Pack8IconVeto);
            if (mark != null)
            {
                var icon = new GameObject("Meeting mark", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                icon.SetParent(meetingRoot, false);
                float titleWidth = Mathf.Min(1100f, title.GetPreferredValues(MeetingTitle).x);
                PlaceTop(icon, 56f, 56f, -68f, -(titleWidth * .5f) - 40f);
                var image = icon.GetComponent<Image>();
                image.sprite = mark; image.preserveAspect = true; image.raycastTarget = false;
            }
            var rule = NewPanel("Meeting rule", meetingRoot, new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .55f), 1);
            PlaceTop(rule, 240f, 2f, -136f);

            meetingHeadline = NewText("Meeting headline", meetingRoot, 40f, UiTheme.Paper);
            meetingHeadline.alignment = TextAlignmentOptions.Center;
            if (bold != null) meetingHeadline.font = bold;
            meetingHeadline.textWrappingMode = TextWrappingModes.NoWrap;
            meetingHeadline.enableAutoSizing = true; meetingHeadline.fontSizeMax = 40f; meetingHeadline.fontSizeMin = 26f;
            PlaceTop(meetingHeadline.rectTransform, 1120f, 60f, -150f);

            meetingLine = NewText("Meeting line", meetingRoot, 28f, UiTheme.Paper);
            meetingLine.alignment = TextAlignmentOptions.Top;
            meetingLine.enableAutoSizing = true; meetingLine.fontSizeMax = 28f; meetingLine.fontSizeMin = 20f;
            PlaceTop(meetingLine.rectTransform, 1100f, 80f, -216f);

            meetingFaces = new GameObject("Meeting faces", typeof(RectTransform)).GetComponent<RectTransform>();
            meetingFaces.SetParent(meetingRoot, false);
            PlaceTop(meetingFaces, 1160f, FaceHeight, -318f);
            meetingRoot.gameObject.SetActive(false);
        }

        /// <summary>The page's faces in a centred row, rebuilt per page; the replacement's page puts Pack 8's arrow between its two.</summary>
        private void MeetingFaces(List<MeetingFace> row, bool arrow)
        {
            for (int i = meetingFaces.childCount - 1; i >= 0; i--) Destroy(meetingFaces.GetChild(i).gameObject);
            var shown = new List<MeetingFace>();
            foreach (var spec in row) if (spec.Id != null && meeting.TryFace(spec.Id, out _)) shown.Add(spec);
            if (shown.Count == 0) return;
            // Centre to centre: a face's width and a gap, or with the arrow between them its width and a gap each side.
            float step = arrow && shown.Count == 2 ? FaceWidth + ArrowSide + 60f : FaceStep;
            float start = -(shown.Count - 1) * step * .5f;
            for (int i = 0; i < shown.Count; i++) MeetingFaceSlot(shown[i], start + i * step);
            if (!arrow || shown.Count != 2) return;
            var sprite = UiTheme.Pack(PackArt.Pack8ArrowRight);
            if (sprite == null) return;
            var mark = new GameObject("Meeting arrow", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            mark.SetParent(meetingFaces, false);
            PlaceTop(mark, ArrowSide, ArrowSide, -(FaceHeight - ArrowSide) * .5f);
            var image = mark.GetComponent<Image>();
            image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
        }

        /// <summary>One face on the screen: Pack 8's veto card frame, the photo, a name plate on its foot and its badges at its head.</summary>
        private void MeetingFaceSlot(MeetingFace spec, float x)
        {
            meeting.TryFace(spec.Id, out var face);
            var slot = new GameObject("Meeting face", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            slot.SetParent(meetingFaces, false);
            PlaceTop(slot, FaceWidth, FaceHeight, 0f, x);
            var frame = slot.GetComponent<Image>();
            frame.raycastTarget = false;
            bool gold = spec.Tint == UiTheme.Gold;
            if (!UiTheme.PackSliced(frame, gold ? PackArt.Pack8VetoAutoHoh : PackArt.Pack8VetoAutoNominee, 16f))
            {
                UiTheme.Style(frame, UiTheme.SurfaceRaised, 10);
                UiTheme.AddBorder(slot, 10, spec.Tint);
            }

            var photo = HudPrimitives.RectPortrait(slot, "Photo", face.Portrait, face.Character,
                new Vector2(FaceWidth - 16f, FaceHeight - 16f), 8);
            photo.anchorMin = photo.anchorMax = new Vector2(.5f, .5f);
            photo.pivot = new Vector2(.5f, .5f);
            photo.anchoredPosition = Vector2.zero;

            var plate = HudPrimitives.Fill("Name plate", photo, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .86f), 0);
            plate.anchorMin = new Vector2(0f, 0f); plate.anchorMax = new Vector2(1f, 0f);
            plate.pivot = new Vector2(.5f, 0f);
            plate.offsetMin = Vector2.zero; plate.offsetMax = new Vector2(0f, 52f);
            plate.GetComponent<Image>().raycastTarget = false;
            var name = NewText("Name", plate, 28f, UiTheme.Paper);
            name.alignment = TextAlignmentOptions.Center;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.enableAutoSizing = true; name.fontSizeMax = 28f; name.fontSizeMin = 18f;
            name.text = face.Name ?? string.Empty;
            name.rectTransform.anchorMin = Vector2.zero; name.rectTransform.anchorMax = Vector2.one;
            name.rectTransform.offsetMin = new Vector2(10f, 0f); name.rectTransform.offsetMax = new Vector2(-10f, 0f);

            MeetingBadge(photo, spec.Badge, spec.Tint, 0);
            if (!string.IsNullOrEmpty(spec.Second)) MeetingBadge(photo, spec.Second, spec.SecondTint, 1);
        }

        /// <summary>The height of a badge on a face, and the type in it: a box 1.8 times its words, where Inter needs 1.21.</summary>
        public const float MeetingBadgeHeight = 40f, MeetingBadgeType = 22f;

        /// <summary>
        /// A badge just inside the photo's top edge, the second under the first: the kit's pill - Pack
        /// 9's status tag, the chip the nomination card's roster wears SAFE and NOMINATED in - in the
        /// badge's colour, its word in whatever reads on it (UI-UX-PASS-PLAN V0). The kit's own
        /// rounded fill stands in without the pack. Named "Badge" over "Badge text", as a test finds it.
        /// </summary>
        private static void MeetingBadge(RectTransform photo, string text, Color tint, int index)
        {
            if (string.IsNullOrEmpty(text)) return;
            const float height = MeetingBadgeHeight, width = 230f, gap = 8f;
            var chip = HudPrimitives.Fill("Badge", photo, tint, Mathf.RoundToInt(height * .5f) - 1);
            chip.anchorMin = chip.anchorMax = new Vector2(.5f, 1f);
            chip.pivot = new Vector2(.5f, 1f);
            chip.anchoredPosition = new Vector2(0f, -10f - index * (height + gap));
            chip.sizeDelta = new Vector2(width, height);
            UiTheme.PackSliced(chip.GetComponent<Image>(), PackArt.Pack9NominationCeremonyStatusTagFill, height * .5f - 2f, tint);
            var label = NewText("Badge text", chip, MeetingBadgeType, UiTheme.OnColor(tint));
            label.alignment = TextAlignmentOptions.Center;
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) label.font = bold;
            label.characterSpacing = 1f;
            // One word on one line, a size smaller rather than cut where a longer one comes.
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMax = MeetingBadgeType;
            label.fontSizeMin = 16f;
            label.text = text;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12f, 0f); label.rectTransform.offsetMax = new Vector2(-12f, 0f);
        }

        /// <summary>A rect hung from its parent's top centre, <paramref name="y"/> down and <paramref name="x"/> across.</summary>
        private static void PlaceTop(RectTransform rect, float width, float height, float y, float x = 0f)
        {
            rect.anchorMin = new Vector2(.5f, 1f);
            rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>
        /// Back to the HUD frame before a card plays there: off the set's screen, the meeting's page
        /// down and forgotten. Nothing changes for a takeover that never played a meeting.
        /// </summary>
        private void LeaveTheScreen()
        {
            meeting = null;
            surface = null;
            if (meetingRoot != null) meetingRoot.gameObject.SetActive(false);
            if (mounted) { ScreenSurface.Unmount(GetComponent<Canvas>()); mounted = false; }
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
            var font = UiTheme.Font(UiTheme.Weight.Regular);
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
