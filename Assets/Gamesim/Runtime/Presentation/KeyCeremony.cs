using Gamesim.Simulation;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The nomination ceremony as the format actually runs it: keys handed out one at a time, and
    /// whoever does not get one is on the block.
    ///
    /// <para>The engine decides both nominations in a single commit, and the HUD reported them in
    /// one sentence. That is the right shape for a save file and the wrong shape for the scene it
    /// describes — the whole tension of a nomination ceremony is in the order, because every name
    /// called is one fewer chance of being the name that is not. Revealing safety in sequence from
    /// an already-decided result costs nothing and is the entire beat.</para>
    ///
    /// <para>The house has six, so five keys are in play: the Head of Household does not draw for
    /// their own safety. Three come out safe and two do not.</para>
    ///
    /// <para>It is paced to build rather than to report (<see cref="CeremonyPacing"/>): a beat on
    /// every key, and a longer one before the last, when everybody still waiting is waiting on one
    /// name. The player can speed it up or skip straight to the block while it plays, and the
    /// settings can make every ceremony quick. Skipping gives up the order, never the result.</para>
    ///
    /// <para>Same rules as the other overlays: nothing raycasts, it ends on a timer rather than on
    /// acknowledgement, and every name is read from committed state so the ceremony cannot disagree
    /// with the save. The presses that speed it up and skip it are read straight off the devices,
    /// never through the event system.</para>
    ///
    /// <para>It plays in one of two frames. On the HUD it is the board under the top bar, 420 wide,
    /// as mockup-10 draws it. On the set's ceremony screen (<see cref="ScreenSurface"/>, the
    /// ceremony cut scenes) it is the screen's whole face: the same card at the screen's own shape,
    /// the keys along its foot at key size, so a camera cut to the screen reads it the way the room
    /// does. Every child keeps its name in both.</para>
    ///
    /// <para>The screen's frame is the owner's nomination mockup as well (UI-UX-PASS-PLAN N1,
    /// decision 9): a roster of ring discs across the middle - the Head of Household first with the
    /// crown, every houseguest with a name - with a chip under each that stays empty until the card
    /// says so: HOH from the start, SAFE as that key comes out, NOMINATED only at the block, so the
    /// roster never says who is on the block before the keys do. The key stands on its pedestal in
    /// the middle of the roster through every beat, lit blue, and goes gold only while the last key
    /// waits. The stage's own beat - the safe face as a key comes out, the two nominees at the
    /// block - plays over the pedestal in the roster's middle column, where the room's cuts expect
    /// to see it. The HUD's board has no roster and no pedestal: it is drawn as it always was, the
    /// gold key alone on its stage while the last key waits.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeyCeremony : MonoBehaviour
    {
        /// <summary>
        /// How long past its fade the card must have been up, in real seconds, before a press moves it
        /// on. Real seconds rather than the card's own clock, so a sped-up reveal does not shorten it.
        /// </summary>
        private const float ReadDelay = 0.35f;

        /// <summary>One houseguest in the ceremony, with the face the card shows.</summary>
        public readonly struct Person
        {
            public readonly string Id;
            public readonly string Name;
            public readonly Texture Portrait;
            public readonly ContestantState Character;

            public Person(string id, string name, Texture portrait, ContestantState character = null)
            {
                Id = id; Name = name; Portrait = portrait; Character = character?.Clone();
            }
        }

        /// <summary>
        /// A sound the ceremony wants played as it reaches a beat: the safe chime as each key is handed
        /// out, and the nomination's sound when the block is shown. The card makes no sound of its own
        /// - the director plays these, so a muted house stays muted - and the block's sound waits for
        /// the block, where the commit used to play it before the first key was out. A skip asks only
        /// for the block's: the keys it hands out at once would be one chime on top of another.
        /// </summary>
        public event System.Action<HouseAudio.Cue> CueRequested;

        /// <summary>
        /// Each beat as the card reaches it - the card up, a key out, the beat before the last, the
        /// block, the card down - so the house can cut to whoever it is about and act it out in time
        /// with the screen. A skip reports every key it hands out at once, marked as skipped.
        /// </summary>
        public event System.Action<CeremonyBeat> BeatReached;

        private CanvasGroup group;
        private RectTransform column, scrim, slotRow, stage, faces, rosterRow, titleMark;
        private Image heldKey;
        private TMP_Text eyebrow, title, hohLine, progress, controls, speedMark;
        private readonly List<RectTransform> slots = new List<RectTransform>();
        private readonly List<RosterEntry> roster = new List<RosterEntry>();
        private List<Person> safe = new List<Person>();
        private List<Person> nominated = new List<Person>();
        private List<Person> cast = new List<Person>();
        private string hohName;
        private CeremonyPace pace = CeremonyPace.Suspenseful;
        private int shown = -1;
        private float elapsed, upFor, speed = 1f;
        private bool playing, reduced, blockShown, lastKeyPending, usingPad, markPlaced;
        private Frame frame;
        private ScreenSurface surface;

        public float FontScale { get; set; } = 1f;
        public bool IsPlaying => playing;

        /// <summary>True once every key is out and the block is on screen.</summary>
        public bool ShowingBlock => blockShown;

        /// <summary>The pace the ceremony was played at.</summary>
        public CeremonyPace Pace => pace;

        /// <summary>How fast the card's clock runs: 1, or <see cref="CeremonyPacing.SpeedUp"/> while the player has sped it up.</summary>
        public float SpeedMultiplier => speed;

        /// <summary>How many keys have been handed out so far.</summary>
        public int KeysShown => Mathf.Max(0, shown);

        /// <summary>How long each key holds the stage at the pace played, in seconds of the card's clock.</summary>
        public float KeyHoldSeconds => KeyHold;

        /// <summary>The beat before the last key, at the pace played.</summary>
        public float LastKeyBeatSeconds => LastKeyWait;

        /// <summary>How long the block holds once the keys are out, at the pace played.</summary>
        public float BlockHoldSeconds => CeremonyPacing.BlockHold(pace);

        /// <summary>The screen the card is playing on, or null while it plays on the HUD.</summary>
        public ScreenSurface Surface => playing ? surface : null;

        /// <summary>
        /// How long the ceremony takes at its own speed, start to finish: the fade, the Head of
        /// Household's line, a hold on every key, the beat before the last one, the block, and the fade
        /// out. Exactly what the card runs for when nobody speeds it up or skips it.
        /// </summary>
        public float Duration => KeysEnd + CeremonyPacing.BlockHold(pace) + CeremonyPacing.FadeOut;

        /// <summary>When the first key comes out, on the card's clock.</summary>
        private float KeysStart => CeremonyPacing.FadeIn + CeremonyPacing.KeyIntro(pace);

        /// <summary>How long each key holds the stage.</summary>
        private float KeyHold => CeremonyPacing.PerKey(pace, safe.Count);

        /// <summary>The extra wait before the last key; none when there are no keys at all.</summary>
        private float LastKeyWait => safe.Count > 0 ? CeremonyPacing.LastKeyBeat(pace) : 0f;

        /// <summary>When the beat before the last key begins: the key before it has had its hold.</summary>
        private float LastKeyCalled => KeysStart + (safe.Count - 1) * KeyHold;

        /// <summary>When the block comes up: the last key has had its hold too.</summary>
        private float KeysEnd => KeysStart + safe.Count * KeyHold + LastKeyWait;

        public static KeyCeremony Attach(GameObject owner)
        {
            var root = new GameObject("Gamesim Key Ceremony",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            if (owner != null && owner.scene.IsValid()) SceneManager.MoveGameObjectToScene(root, owner.scene);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Alongside the vote reveal at 110: the two never play together, and both replace the
            // generic ceremony card rather than stacking on it.
            canvas.sortingOrder = 110;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            return root.AddComponent<KeyCeremony>();
        }

        /// <summary>
        /// Plays the ceremony at <paramref name="pace"/>. Declines a shape it cannot narrate — no keys
        /// to hand out, or nobody on the block — and the generic card plays instead, so the beat is
        /// never silent. The keys come out in the order given, so the caller decides that order; it
        /// must not be one that says who is safe before the card does. With a <paramref name="screen"/>
        /// the card plays on that screen's face instead of the HUD, with a roster of the house across
        /// it: <paramref name="roster"/> in cast order with the Head of Household first, or, when the
        /// caller gives none, the Head of Household by name, then whoever draws a key, then the block.
        /// </summary>
        public bool Play(int week, string hohName, bool hohIsPlayer, IList<Person> safeHouseguests,
            IList<Person> block, bool reducedMotion, CeremonyPace pace = CeremonyPace.Suspenseful, ScreenSurface screen = null,
            IList<Person> roster = null)
        {
            if (safeHouseguests == null || block == null || block.Count == 0) return false;

            safe = new List<Person>(safeHouseguests);
            nominated = new List<Person>(block);
            this.hohName = hohName;
            cast = roster != null ? new List<Person>(roster) : DefaultRoster(hohName);
            this.pace = pace;
            surface = screen;
            frame = screen != null ? Frame.OnScreen(safe.Count) : Frame.Hud(Mathf.Max(0.5f, FontScale));

            Build();
            reduced = reducedMotion;
            elapsed = 0f;
            upFor = 0f;
            shown = -1;
            blockShown = false;
            lastKeyPending = false;
            // Every ceremony starts at its own pace; speeding one up is for that one.
            SetSpeed(1f);
            playing = true;

            eyebrow.text = "WEEK " + Mathf.Max(1, week);
            // Second person when the player is the one holding the keys. The engine's event text
            // learned this already; a card that says "You has made their decision" undoes it in the
            // most prominent place on screen.
            hohLine.text = string.IsNullOrEmpty(hohName) ? "The keys go up"
                : hohIsPlayer ? "You have made your decision"
                : hohName + " has made their decision";
            hohLine.color = UiTheme.Accent;

            Show(0, false, false);
            column.gameObject.SetActive(true);
            scrim.gameObject.SetActive(true);
            group.alpha = reduced ? 1f : 0f;
            Beat(new CeremonyBeat(CeremonyBeatKind.Opened));
            return true;
        }

        /// <summary>The roster when the caller gives none: the Head of Household by name, then the keys' holders, then the block.</summary>
        private List<Person> DefaultRoster(string hoh)
        {
            var people = new List<Person>();
            if (!string.IsNullOrEmpty(hoh)) people.Add(new Person(null, hoh, null));
            people.AddRange(safe);
            people.AddRange(nominated);
            return people;
        }

        public void Cancel()
        {
            bool was = playing;
            playing = false;
            if (group != null) group.alpha = 0f;
            if (column != null) column.gameObject.SetActive(false);
            if (scrim != null) scrim.gameObject.SetActive(false);
            if (was) Beat(new CeremonyBeat(CeremonyBeatKind.Closed, -1, null, !blockShown));
        }

        /// <summary>
        /// The first press's step: every remaining key handed out and the block shown - the order
        /// given up, never the result. A press on the block ends the card. Shared with a staged
        /// ceremony, whose press on its summons starts the card here, so one press is one step
        /// however the ceremony is played. Nothing once the block is up.
        /// </summary>
        public void SkipToResult()
        {
            if (!playing || blockShown) return;
            elapsed = Mathf.Max(elapsed, KeysEnd);
            Show(safe.Count, false, false);
            Block(true);
        }

        /// <summary>
        /// True once the card has been up long enough to have been read; after that a press moves
        /// it on. The same delay as <see cref="CeremonyTakeover"/>'s, for the same reason: a press
        /// already in flight when the card appears - the Enter that committed the nominations, say -
        /// must not skip what it has not yet shown.
        /// </summary>
        private bool Dismissable => upFor >= CeremonyPacing.FadeIn + ReadDelay;

        private void Update()
        {
            if (!playing) return;
            CeremonyOverlays.Showing();
            upFor += Time.unscaledDeltaTime;
            FollowDevice();

            // The card says what moves it on, so those presses do. Read from the devices rather than
            // through a raycaster for the takeover's reason: nothing on a ceremony card may take a
            // click meant for the house. Space (the pad's X) runs the reveal faster, or back at its
            // own pace; it changes how long the order takes, never the order. The first skip hands
            // out every remaining key and shows the block - skipping the order, never the result -
            // and a skip on the block ends the card.
            if (Dismissable && CeremonyTakeover.SpeedPressed()) SetSpeed(speed > 1f ? 1f : CeremonyPacing.SpeedUp);
            if (Dismissable && CeremonyTakeover.SkipPressed())
            {
                if (blockShown) { Cancel(); return; }
                SkipToResult();
                return;
            }

            elapsed += Time.unscaledDeltaTime * speed;
            float blockEnd = KeysEnd + CeremonyPacing.BlockHold(pace);

            if (reduced) group.alpha = 1f;
            else if (elapsed < CeremonyPacing.FadeIn) group.alpha = Eased(elapsed / CeremonyPacing.FadeIn);
            else if (elapsed < blockEnd) group.alpha = 1f;
            else
            {
                float exit = (elapsed - blockEnd) / CeremonyPacing.FadeOut;
                if (exit >= 1f) { Cancel(); return; }
                group.alpha = 1f - Eased(exit);
            }

            // Reduced motion keeps the timings: they are reading time, not movement.
            if (reduced && elapsed >= Duration) { Cancel(); return; }

            int due = KeysDue(elapsed);
            bool pending = LastKeyPendingAt(elapsed);
            if (!blockShown && (due != shown || pending != lastKeyPending)) Show(due, pending, true);

            if (elapsed >= KeysEnd && !blockShown) Block(false);
        }

        /// <summary>
        /// The title's mark, placed once more from the title as drawn, on the card's first playing
        /// frame. Build measures the title awake, so this normally moves it by nothing; it is the
        /// guard for a build that measured a label before its Awake - the week-two defect (UI-UX-PASS
        /// 1.2), where a tenth of the title's width hung the mark over the T - whatever order the
        /// card is built and activated in.
        /// </summary>
        private void LateUpdate()
        {
            if (!playing || markPlaced) return;
            markPlaced = true;
            PlaceTitleMark();
        }

        private static float Eased(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>How many keys are out at <paramref name="t"/> on the card's clock.</summary>
        private int KeysDue(float t)
        {
            if (safe.Count == 0 || t < KeysStart) return 0;
            int due = Mathf.Min(safe.Count, Mathf.FloorToInt((t - KeysStart) / KeyHold) + 1);
            // The last key keeps the others' rhythm and then waits its beat on top of it.
            if (due == safe.Count && t < LastKeyCalled + LastKeyWait) due--;
            return due;
        }

        /// <summary>Whether <paramref name="t"/> falls in the beat before the last key.</summary>
        private bool LastKeyPendingAt(float t) =>
            safe.Count > 0 && LastKeyWait > 0f && t >= LastKeyCalled && t < LastKeyCalled + LastKeyWait;

        /// <summary>Runs the card's clock at <paramref name="value"/>, and says so in the corner while it is fast.</summary>
        private void SetSpeed(float value)
        {
            speed = value;
            if (speedMark != null) speedMark.gameObject.SetActive(speed > 1f);
        }

        /// <summary>
        /// The key hints follow the device last used on the card, as the competition screen's do: a
        /// pad press shows the pad's buttons, a key or a click the keyboard's.
        /// </summary>
        private void FollowDevice()
        {
            var pad = CeremonyTakeover.PadUsed();
            if (!pad.HasValue || pad.Value == usingPad) return;
            usingPad = pad.Value;
            if (controls != null) controls.text = CeremonyTakeover.ControlsFor(usingPad);
        }

        private void Raise(HouseAudio.Cue cue) => CueRequested?.Invoke(cue);

        private void Beat(CeremonyBeat beat) => BeatReached?.Invoke(beat);

        /// <summary>
        /// Puts the first <paramref name="count"/> keys out - whoever holds the last of them is safe
        /// - or, while <paramref name="pending"/>, holds the last key up with nobody on the stage.
        /// <paramref name="announce"/> asks for the safe chime once for every key newly out.
        /// </summary>
        private void Show(int count, bool pending, bool announce)
        {
            int before = Mathf.Max(0, shown);
            shown = count;
            lastKeyPending = pending;
            if (announce) for (int key = before; key < count; key++) Raise(HouseAudio.Cue.Save);
            for (int key = before; key < count; key++) Beat(new CeremonyBeat(CeremonyBeatKind.KeyShown, key, safe[key].Id, !announce));

            for (int i = 0; i < slots.Count; i++)
                slots[i].GetComponent<Image>().color = i < count ? UiTheme.Positive
                    : pending && i == slots.Count - 1 ? UiTheme.Gold : UiTheme.Outline;
            DressRoster(count, false);

            if (pending)
            {
                // Everybody still waiting is waiting on this key - the last safe name and the block
                // alike - so the card says that one is left, and never whose.
                progress.text = "One key left";
                StageLastKey();
                Beat(new CeremonyBeat(CeremonyBeatKind.LastKeyPending, safe.Count - 1));
                return;
            }

            if (count == 0)
            {
                progress.text = (safe.Count == 1 ? "1 key. " : safe.Count + " keys. ")
                    + Spelled(nominated.Count) + " will not get one.";
                Stage(null, null, UiTheme.Accent);
                return;
            }

            var person = safe[count - 1];
            progress.text = "Revealing key " + count + " of " + safe.Count;
            Stage(person, "SAFE", UiTheme.Positive);
        }

        /// <summary>A small count in words, as the card says it aloud.</summary>
        private static string Spelled(int count)
        {
            switch (count)
            {
                case 1: return "One";
                case 2: return "Two";
                case 3: return "Three";
                case 4: return "Four";
                default: return count.ToString();
            }
        }

        /// <summary>The close: the two who never heard their name.</summary>
        private void Block(bool skipped)
        {
            blockShown = true;
            lastKeyPending = false;
            progress.text = nominated.Count == 2 ? "Nominated for eviction" : "On the block";
            // The mockup's subtitle once the keys are out: the screen now shows who they are.
            hohLine.text = nominated.Count == 1 ? "Tonight's Nominee" : "Tonight's Nominees";
            DressRoster(safe.Count, true);
            StageBlock();
            Raise(HouseAudio.Cue.Nomination);
            Beat(new CeremonyBeat(CeremonyBeatKind.BlockShown, -1, nominated.Count > 0 ? nominated[0].Id : null, skipped));
        }

        /// <summary>
        /// The card's frame: how big its parts are and where they sit, in canvas units. The HUD's
        /// is the board under the top bar at the standard text size, scaled by the large-text
        /// preference; the screen's is the screen's whole face at 3:2, the roster's band over half
        /// its height, the keys along the foot at key size - each about a twelfth of the width,
        /// closer together only when more keys are in play than that leaves room for - and the
        /// HUD's margin under the controls line, so nothing is drawn on the face's edge.
        /// </summary>
        private readonly struct Frame
        {
            public readonly bool Screen;
            /// <summary>The column's size, and how far under the top of the canvas it hangs.</summary>
            public readonly float Width, Height, Top;
            /// <summary>One face on the stage (the beat's disc, on the screen); the block's faces, side by side, and their spacing.</summary>
            public readonly float SlotW, SlotH, BlockW, BlockH, BlockStep;
            /// <summary>A key's size and the spacing of the row.</summary>
            public readonly float Pip, Step;
            /// <summary>The type's scale relative to the HUD's at the standard size.</summary>
            public readonly float Text;
            /// <summary>Where the rows sit, from the top, and how tall each is.</summary>
            public readonly float EyebrowY, EyebrowH, TitleY, TitleH, Trophy, HohY, HohH, StageY, KeysY, KeysH,
                ProgressY, ProgressH, RuleY, DismissY, DismissH, ControlsY, ControlsH, TaglineY, Glass, Neon;

            private Frame(bool screen, float width, float height, float top, float slotW, float slotH, float blockW, float blockH,
                float blockStep, float pip, float step, float text, float eyebrowY, float eyebrowH, float titleY, float titleH,
                float trophy, float hohY, float hohH, float stageY, float keysY, float keysH, float progressY, float progressH,
                float ruleY, float dismissY, float dismissH, float controlsY, float controlsH, float taglineY, float glass, float neon)
            {
                Screen = screen; Width = width; Height = height; Top = top; SlotW = slotW; SlotH = slotH;
                BlockW = blockW; BlockH = blockH; BlockStep = blockStep; Pip = pip; Step = step; Text = text;
                EyebrowY = eyebrowY; EyebrowH = eyebrowH; TitleY = titleY; TitleH = titleH; Trophy = trophy; HohY = hohY; HohH = hohH;
                StageY = stageY; KeysY = keysY; KeysH = keysH; ProgressY = progressY; ProgressH = progressH; RuleY = ruleY;
                DismissY = dismissY; DismissH = dismissH; ControlsY = controlsY; ControlsH = controlsH; TaglineY = taglineY;
                Glass = glass; Neon = neon;
            }

            /// <summary>The HUD's board: 420 × 330 under the top bar, a 104 × 134 face, 16-unit keys.</summary>
            public static Frame Hud(float scale)
            {
                const float slotH = SlotHeight;
                return new Frame(false, CardWidth * scale, CardHeight * scale, CardTop * scale, SlotWidth * scale, slotH * scale,
                    SlotWidth * scale, slotH * scale, (SlotWidth + 44f) * scale, 16f * scale, 24f * scale, scale,
                    -10f * scale, 16f * scale, -26f * scale, 30f * scale, 24f * scale, -58f * scale, 22f * scale, -88f * scale,
                    -(96f + slotH) * scale, 20f * scale, -(120f + slotH) * scale, 18f * scale, -(144f + slotH) * scale,
                    -(150f + slotH) * scale, 16f * scale, -(168f + slotH) * scale, 16f * scale, -(CardHeight + 22f) * scale,
                    12f * scale, 30f * scale);
            }

            /// <summary>
            /// The set's screen: its whole face, the roster's band from the Head of Household's line
            /// to the keys, the keys at key size, and thirty units of air under the controls line
            /// (the HUD's own margin, 12 of 330), where the line used to end four units off the edge.
            /// The eyebrow starts 3 % of the face's height inside its top and ends where the title
            /// starts; no two rows' boxes cross.
            /// </summary>
            public static Frame OnScreen(int keys)
            {
                const float text = 2.4f;
                float step = keys > 0 ? Mathf.Min(96f, 1100f / keys) : 96f;
                return new Frame(true, ScreenSurface.ReferenceWidth, ScreenSurface.ReferenceHeight, 0f, ScreenRoster.BeatFace, ScreenRoster.BandHeight,
                    ScreenRoster.BlockFace, ScreenRoster.BlockFace, ScreenRoster.BlockStep, Mathf.Min(76f, step * 0.8f), step, text,
                    -24f, 36f, -60f, 72f, 58f, -136f, 52f, ScreenRoster.BandTop, -596f, 88f, -690f, 40f, float.NaN, float.NaN, 0f,
                    -734f, 36f, float.NaN, 0f, 16f);
            }
        }

        /// <summary>
        /// The screen frame's roster and stage, in canvas units on the 1200 × 800 face (the owner's
        /// mockup, UI-UX-PASS-PLAN decision 9). The band runs from the Head of Household's line to the
        /// keys; its middle column is the stage's - the key on its pedestal, the beat's face over it -
        /// and the roster stands either side of that column: one row of 104-unit faces three a side
        /// up to six, two rows of 88-unit faces three a side to twelve, two rows of 78-unit faces four
        /// a side from thirteen, so a full house of sixteen is two rows of eight. A face takes the
        /// room its ring art does - the pack's ring is drawn larger than the face by the hole's share
        /// of it, its glow in the quad's corners - so no ring reaches into a line above or a name
        /// below; the rows fit the band with air to spare (227 in one row; 2 × 196 + 8 = 400 and
        /// 2 × 179 + 8 = 366 in two, of 408). Every name is in a box at least 1.3 times its type.
        /// </summary>
        private static class ScreenRoster
        {
            public const float BandTop = -188f, BandHeight = 408f;
            /// <summary>The stage's column in the band's middle, which the roster leaves clear.</summary>
            public const float Column = 300f;
            /// <summary>Faces to six, in one row: a disc on a step, three a side, with a name and a chip under each.</summary>
            public const float Face = 104f, Step = 140f, NamePt = 36f, NameH = 47f, ChipH = 28f, ChipPt = 16f;
            /// <summary>Faces from seven to twelve, in two rows, three a side.</summary>
            public const float MidFace = 88f, MidNamePt = 30f, MidNameH = 40f, MidChipH = 26f, MidChipPt = 14f;
            /// <summary>Faces from thirteen, in two rows, four a side.</summary>
            public const float SmallFace = 78f, SmallStep = 105f, SmallNamePt = 28f, SmallNameH = 37f, SmallChipH = 26f, SmallChipPt = 14f;
            public const int Side = 3, SmallSide = 4, OneRow = 6, ThreeASide = 12;
            /// <summary>
            /// The chips and the names are as wide as the step allows and their words are drawn
            /// smaller to fit: NOMINATED at 16 points is about a hundred units.
            /// </summary>
            public const float Ring = 4f, Gap = 4f, RowGap = 8f, NameMinPt = 12f, ChipMinPt = 10f, Air = 2f, Inset = 8f;
            /// <summary>The pack ring's hole as a share of its size (its edge at 91 of 128), so a ring is drawn to hug the face it rings.</summary>
            public const float RingHole = 0.711f;

            /// <summary>
            /// The stage's beat over the pedestal: the safe face, and the block's two. Each hangs with
            /// its ring art's top a little under the band's top, and its name under the art. The
            /// block's two stand 66 either side of the axis at 112, so each one's ring art (157.5
            /// square) ends at 144.8, nine units short of the roster's innermost name boxes, which
            /// start 154 out on every roster shape; at 120 on a 156 step the art reached 162.4 and
            /// crossed the first row's names on a two-row roster. Their names are 128 wide, two
            /// units apart in the middle.
            /// </summary>
            public const float BeatFace = 140f, BeatRing = 5f, BeatNamePt = 30f, BeatNameH = 40f, BeatNameW = 280f,
                BeatChipW = 120f, BeatChipH = 44f, BeatChipPt = 26f;
            public const float BlockFace = 112f, BlockRing = 4f, BlockStep = 132f, BlockNameW = 128f, BlockNamePt = 28f, BlockNameH = 37f;

            /// <summary>The key on its pedestal, under the beat's names: the glyph's size and its top under the band's top, and the pedestal's width.</summary>
            public const float KeySize = 100f, KeyY = -246f, Pedestal = 187f;
            /// <summary>The pedestal's top surface, as a share of its height from the image's top (measured on the mask: 114 of 256).</summary>
            public const float PedestalSurface = 0.445f;

            /// <summary>The room a face's ring art takes, square: the face over the hole's share of the ring.</summary>
            public static float Footprint(float face) => face / RingHole;
        }

        /// <summary>The roster's sizes for a house: the faces, their step and how many stand a side, the names' and the chips' type and boxes.</summary>
        private readonly struct RosterSizes
        {
            public readonly float Face, Step, NamePt, NameH, ChipH, ChipPt;
            public readonly int Side, Rows;

            private RosterSizes(float face, float step, int side, int rows, float namePt, float nameH, float chipH, float chipPt)
            {
                Face = face; Step = step; Side = side; Rows = rows; NamePt = namePt; NameH = nameH; ChipH = chipH; ChipPt = chipPt;
            }

            public static RosterSizes For(int houseguests) =>
                houseguests <= ScreenRoster.OneRow
                    ? new RosterSizes(ScreenRoster.Face, ScreenRoster.Step, ScreenRoster.Side, 1, ScreenRoster.NamePt, ScreenRoster.NameH,
                        ScreenRoster.ChipH, ScreenRoster.ChipPt)
                    : houseguests <= ScreenRoster.ThreeASide
                        ? new RosterSizes(ScreenRoster.MidFace, ScreenRoster.Step, ScreenRoster.Side, 2, ScreenRoster.MidNamePt,
                            ScreenRoster.MidNameH, ScreenRoster.MidChipH, ScreenRoster.MidChipPt)
                        : new RosterSizes(ScreenRoster.SmallFace, ScreenRoster.SmallStep, ScreenRoster.SmallSide, 2, ScreenRoster.SmallNamePt,
                            ScreenRoster.SmallNameH, ScreenRoster.SmallChipH, ScreenRoster.SmallChipPt);
        }

        private void Build()
        {
            var root = (RectTransform)transform;
            var canvas = GetComponent<Canvas>();
            if (surface != null) surface.Mount(canvas);
            else ScreenSurface.Unmount(canvas);

            if (column != null)
            {
                foreach (Transform child in column) Destroy(child.gameObject);
                slots.Clear();
                roster.Clear();
            }
            else
            {
                group = GetComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;

                // The ceremony plays IN the room (mockup-10): the house, the cast and the chrome
                // all stay up, darkened only at the frame's edges. It sat on a 97.5 % scrim, which
                // blacked out the very room the ceremony was happening in - the scrim is kept, at
                // nothing, because the overlays' shared bookkeeping shows and hides it by name.
                scrim = HudPrimitives.Fill("Scrim", root, new Color(0f, 0f, 0f, 0f), 1);
                scrim.anchorMin = Vector2.zero; scrim.anchorMax = Vector2.one;
                scrim.offsetMin = Vector2.zero; scrim.offsetMax = Vector2.zero;
                scrim.GetComponent<Image>().raycastTarget = false;
                HudPrimitives.Vignette(scrim);

                column = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>();
                column.SetParent(root, false);
                // The screen hangs top-centre, under the top bar, where mockup-10 puts the room's
                // ceremony board: clear of the rail, the right column and a panel docked below.
                column.anchorMin = new Vector2(.5f, 1f);
                column.anchorMax = new Vector2(.5f, 1f);
                column.pivot = new Vector2(.5f, 1f);
            }

            // Awake before anything is measured. A label made under a column Cancel had deactivated
            // has not run Awake, and TextMesh Pro measures an un-awake label at a tenth of its width
            // (TMP_Text's m_isOrthographic is set in Awake): from week two the title's mark hung
            // over the T of NOMINATION (UI-UX-PASS-PLAN 1.2). The group's alpha is still nothing,
            // so nothing shows before Play wants it to.
            column.gameObject.SetActive(true);

            var f = frame;
            float scale = f.Text;
            float width = f.Width;
            column.anchoredPosition = new Vector2(0f, -f.Top);
            column.sizeDelta = new Vector2(width, f.Height);
            CardGlass(column, f.Glass, f.Neon, f.Screen);

            eyebrow = HudPrimitives.Label("Week", column, 11f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            eyebrow.characterSpacing = 10f;
            Place(eyebrow.rectTransform, width, f.EyebrowH, f.EyebrowY);

            // A broadcast's fast-forward bug in the screen's corner, up only while the reveal is sped
            // up, so a player who pressed Space by accident can see why the keys are hurrying.
            speedMark = HudPrimitives.Label("Speed", column, 11f * scale, UiTheme.Glow, TextAlignmentOptions.Right);
            speedMark.text = CeremonyTakeover.SpeedCaption;
            Place(speedMark.rectTransform, 150f * scale, f.EyebrowH, f.EyebrowY, width * .5f - 87f * scale);
            speedMark.gameObject.SetActive(false);

            // The key and the title in the display weight, in the glow blue the board is lit in.
            title = HudPrimitives.Label("Title", column, 22f * scale, UiTheme.Glow, TextAlignmentOptions.Center);
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) title.font = bold;
            title.characterSpacing = 3f;
            title.text = "NOMINATION CEREMONY";
            Place(title.rectTransform, width, f.TitleH, f.TitleY);
            // The mark is the key the card is about - the pack's, the generated one without the
            // pack - and never inside the title: it hangs left of the whole word, measured now that
            // the title is awake, and measured again as drawn on the first playing frame.
            titleMark = null;
            var mark = Mark("Title mark", column, KeyArt(), UiTheme.Glow, f.Trophy);
            if (mark != null)
            {
                titleMark = mark.rectTransform;
                titleMark.anchorMin = titleMark.anchorMax = new Vector2(.5f, 1f);
                titleMark.pivot = new Vector2(1f, 1f);
            }
            PlaceTitleMark();
            markPlaced = false;

            hohLine = HudPrimitives.Label("HoH", column, 15f * scale, UiTheme.Accent, TextAlignmentOptions.Center);
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            if (medium != null) hohLine.font = medium;
            Place(hohLine.rectTransform, width, f.HohH, f.HohY);

            // The stage: on the screen the key on its pedestal through every beat, and over it
            // whichever faces the ceremony is on right now; on the HUD the faces alone.
            stage = new GameObject("Stage", typeof(RectTransform)).GetComponent<RectTransform>();
            stage.SetParent(column, false);
            Place(stage, width, f.SlotH, f.StageY);
            heldKey = null;
            if (f.Screen) BuildPedestal();
            faces = new GameObject("Stage faces", typeof(RectTransform)).GetComponent<RectTransform>();
            faces.SetParent(stage, false);
            Place(faces, width, f.SlotH, 0f);

            if (f.Screen) BuildRoster();

            // One key per houseguest who draws, lit as each is handed out.
            slotRow = new GameObject("Keys", typeof(RectTransform)).GetComponent<RectTransform>();
            slotRow.SetParent(column, false);
            Place(slotRow, width, f.KeysH, f.KeysY);

            float pip = f.Pip, step = f.Step;
            float first = -(safe.Count - 1) * step * 0.5f;
            var key = UiTheme.Icon("key");
            for (int i = 0; i < safe.Count; i++)
            {
                var slot = HudPrimitives.Fill("Key " + (i + 1), slotRow, UiTheme.Outline, 3);
                slot.anchorMin = new Vector2(.5f, .5f); slot.anchorMax = new Vector2(.5f, .5f);
                slot.pivot = new Vector2(.5f, .5f);
                slot.anchoredPosition = new Vector2(first + i * step, 0f);
                var image = slot.GetComponent<Image>();
                image.raycastTarget = false;
                if (key != null)
                {
                    // The key itself, not a pip: the thing each houseguest is waiting to be handed.
                    image.sprite = key; image.type = Image.Type.Simple; image.preserveAspect = true;
                    slot.sizeDelta = new Vector2(pip * 1.2f, pip * 1.2f);
                }
                else slot.sizeDelta = new Vector2(pip * .7f, pip * 1.1f);
                slots.Add(slot);
            }

            progress = HudPrimitives.Label("Progress", column, 12f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            Place(progress.rectTransform, width, f.ProgressH, f.ProgressY);

            if (!f.Screen)
            {
                // A hairline rule and a quiet instruction close the card, as the web build closes
                // every phase card.
                var rule = HudPrimitives.Fill("Rule", column,
                    new Color(UiTheme.Muted.r, UiTheme.Muted.g, UiTheme.Muted.b, 0.35f), 1);
                Place(rule, 96f * scale, 1f, f.RuleY);
                rule.GetComponent<Image>().raycastTarget = false;

                var dismiss = HudPrimitives.Label("Dismiss", column, 11f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
                dismiss.text = CeremonyTakeover.DismissCaption;
                Place(dismiss.rectTransform, width, f.DismissH, f.DismissY);
            }

            // And what else it answers to: the reveal can be sped up, or skipped to the block. The
            // keys named follow the device last used on the card.
            controls = HudPrimitives.Label("Controls", column, 11f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            controls.text = CeremonyTakeover.ControlsFor(usingPad);
            Place(controls.rectTransform, width, f.ControlsH, f.ControlsY);

            if (!f.Screen)
            {
                // The show's line under the screen, the way mockup-10 writes it on the wall below the
                // board. On the set's screen the wall is the wall.
                var tagline = HudPrimitives.Label("Tagline", column, 12f * scale, UiTheme.Glow, TextAlignmentOptions.Center);
                var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
                if (semibold != null) tagline.font = semibold;
                tagline.fontStyle = FontStyles.Italic;
                tagline.characterSpacing = 12f;
                tagline.text = "SAME HOUSE.  DIFFERENT STORIES.";
                Place(tagline.rectTransform, width * 1.4f, 18f * scale, f.TaglineY);
            }
        }

        /// <summary>The screen's size and where it hangs, in reference units at the standard text size.</summary>
        private const float CardWidth = 420f;
        private const float CardHeight = 330f;
        private const float CardTop = 96f;
        private const float SlotWidth = 104f;
        private const float SlotHeight = 134f;

        /// <summary>
        /// Hangs the title's mark left of the title as drawn: its right edge a short gap off the
        /// text's left edge, read from the text's own bounds once the label has a mesh, and from its
        /// preferred width before it has one. Both readings are right only on an awake label, which
        /// is why Build activates the column first.
        /// </summary>
        private void PlaceTitleMark()
        {
            if (titleMark == null || title == null) return;
            float scale = frame.Text;
            float left = -title.GetPreferredValues(title.text).x * .5f;
            if (title.gameObject.activeInHierarchy)
            {
                title.ForceMeshUpdate();
                var bounds = title.textBounds;
                if (bounds.size.x > 0f) left = bounds.min.x;
            }
            titleMark.anchoredPosition = new Vector2(left - 8f * scale, frame.TitleY - 3f * scale);
        }

        /// <summary>
        /// The key the card is about, for its mark and its stage: the pack's key at 128 (Icons/ic_key),
        /// then the pack's NominationCeremony/key_icon - which ships the same 128-pixel drawing in
        /// the top-left corner of a 256 canvas, so it is the fallback and not the first choice - and
        /// the generated icon without the pack. Null in a clone with neither.
        /// </summary>
        private static Sprite KeyArt()
        {
            var sprite = UiTheme.Pack(PackArt.Pack9IconsIcKey);
            if (sprite == null) sprite = UiTheme.Pack(PackArt.Pack9NominationCeremonyKeyIcon);
            if (sprite == null) sprite = UiTheme.Icon("key");
            return sprite;
        }

        /// <summary>The Head of Household's crown for the roster's badge: the pack's at 128, then its 160 twin (the same drawing, off-centre), then the generated one.</summary>
        private static Sprite CrownArt()
        {
            var sprite = UiTheme.Pack(PackArt.Pack9IconsIcCrown);
            if (sprite == null) sprite = UiTheme.Pack(PackArt.Pack9NominationCeremonyHohCrownIcon);
            if (sprite == null) sprite = UiTheme.Icon("crown");
            return sprite;
        }

        /// <summary>
        /// One of the pack's marks, drawn whole at <paramref name="side"/> square and tinted, hung
        /// from <paramref name="parent"/>'s top-left like <see cref="HudPrimitives.Glyph"/>. Null,
        /// and nothing drawn, without a sprite: every caller keeps the plainer shape it drew before.
        /// </summary>
        private static Image Mark(string name, Transform parent, Sprite sprite, Color tint, float side)
        {
            if (sprite == null) return null;
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(side, side);
            var image = rect.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = tint;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// The key on its pedestal, the screen stage's own fixture: the pack's pedestal mask lit in
        /// the board's blue, and the key standing upright on its top surface, blue through every beat
        /// and gold only while the last key waits. Both are the stage's children under whatever
        /// faces the beat puts up, so Show(0) no longer leaves the stage empty (the owner's frame,
        /// UI-UX-PASS-PLAN 1.2). The HUD's board has neither: its two nominee frames stand 44 units
        /// apart at the block, and a key between them showed through the gap. The glyph keeps its
        /// name, "Last key": the suite counts the key slots by the "Key " prefix, and this is not one.
        /// </summary>
        private void BuildPedestal()
        {
            float keyBottom = ScreenRoster.KeyY - ScreenRoster.KeySize;
            var pedestal = Mark("Pedestal", stage, UiTheme.Pack(PackArt.Pack9NominationCeremonyKeyPedestalMask),
                new Color(UiTheme.Glow.r, UiTheme.Glow.g, UiTheme.Glow.b, 0.6f), ScreenRoster.Pedestal);
            if (pedestal != null)
            {
                float height = ScreenRoster.Pedestal * 0.5f;
                // The key stands on the surface, which sits under the mask's top by a measured share.
                Place(pedestal.rectTransform, ScreenRoster.Pedestal, height, keyBottom + height * ScreenRoster.PedestalSurface);
            }
            var held = Mark("Last key", stage, KeyArt(), UiTheme.Glow, ScreenRoster.KeySize);
            if (held == null) return;
            var key = held.rectTransform;
            key.anchorMin = key.anchorMax = new Vector2(.5f, 1f);
            key.pivot = new Vector2(.5f, .5f);
            key.anchoredPosition = new Vector2(0f, ScreenRoster.KeyY - ScreenRoster.KeySize * .5f);
            // The pack draws its key lying down, bow to the left; a quarter turn stands it on its
            // teeth with the bow up, the way the mockup's key stands on its podium.
            key.localRotation = Quaternion.Euler(0f, 0f, -90f);
            heldKey = held;
        }

        /// <summary>Lights the screen's key gold while the last key waits, and blue through every other beat.</summary>
        private void LightTheKey(bool gold)
        {
            if (heldKey != null) heldKey.color = gold ? UiTheme.Gold : UiTheme.Glow;
        }

        /// <summary>Draws one houseguest on the stage, or clears it.</summary>
        private void Stage(Person? person, string badge, Color tint)
        {
            ClearFaces();
            LightTheKey(false);
            if (person == null) return;

            if (frame.Screen)
            {
                BeatFace(person.Value, badge, tint);
                return;
            }

            float scale = frame.Text;
            var slot = Slot(person.Value, 0f, frame.SlotW, frame.SlotH, tint, "Name");
            if (string.IsNullOrEmpty(badge)) return;
            // The word the key means, pinned to the photo's shoulder.
            float chipHeight = 20f * scale;
            var chip = HudPrimitives.Fill("Badge", slot, tint, 4);
            chip.anchorMin = chip.anchorMax = new Vector2(.5f, 1f);
            chip.pivot = new Vector2(.5f, .5f);
            chip.anchoredPosition = Vector2.zero;
            chip.sizeDelta = new Vector2(70f * scale, chipHeight);
            chip.GetComponent<Image>().raycastTarget = false;
            BadgeText(chip, badge, 12f * scale, UiTheme.Ink);
        }

        private void ClearFaces()
        {
            if (faces == null) return;
            for (int i = faces.childCount - 1; i >= 0; i--) Destroy(faces.GetChild(i).gameObject);
        }

        private static TMP_Text BadgeText(RectTransform chip, string badge, float size, Color colour)
        {
            var chipText = HudPrimitives.Label("Badge text", chip, size, colour, TextAlignmentOptions.Center);
            chipText.text = badge;
            chipText.rectTransform.anchorMin = Vector2.zero;
            chipText.rectTransform.anchorMax = Vector2.one;
            chipText.rectTransform.offsetMin = Vector2.zero;
            chipText.rectTransform.offsetMax = Vector2.zero;
            return chipText;
        }

        /// <summary>
        /// The beat's face on the screen's frame: a ring disc over the pedestal in the roster's
        /// middle column, as the vote's and the roster's faces are drawn, hung with its ring art's
        /// top a little under the band's top, the word the key means in a chip pinned inside the
        /// disc's top - under the foot of the Head of Household's line, as the chip on the photo was
        /// moved to be (MOCKUP-PASS-PLAN M2) - and the name under the ring art.
        /// </summary>
        private void BeatFace(Person person, string badge, Color tint)
        {
            float face = frame.SlotW;
            float footprint = ScreenRoster.Footprint(face);
            float centre = -(footprint * .5f + ScreenRoster.Air);
            var rim = Disc(faces, "Stage face", person, tint, face, ScreenRoster.BeatRing, RingArtFor(tint), new Vector2(0f, centre));
            NameUnder(faces, "Name", person.Name, ScreenRoster.BeatNamePt, ScreenRoster.BeatNameW, ScreenRoster.BeatNameH,
                centre - footprint * .5f - ScreenRoster.Gap, 0f, UiTheme.Weight.SemiBold);
            if (string.IsNullOrEmpty(badge)) return;
            var chip = HudPrimitives.Fill("Badge", rim, tint, Mathf.RoundToInt(ScreenRoster.BeatChipH * .5f) - 1);
            chip.anchorMin = chip.anchorMax = new Vector2(.5f, 1f);
            chip.pivot = new Vector2(.5f, 1f);
            chip.anchoredPosition = new Vector2(0f, -2f);
            chip.sizeDelta = new Vector2(ScreenRoster.BeatChipW, ScreenRoster.BeatChipH);
            var art = chip.GetComponent<Image>();
            art.raycastTarget = false;
            UiTheme.PackSliced(art, PackArt.Pack9NominationCeremonyStatusTagFill, ScreenRoster.BeatChipH * .5f - 2f, tint);
            BadgeText(chip, badge, ScreenRoster.BeatChipPt, UiTheme.OnColor(tint));
        }

        /// <summary>
        /// The stage during the beat before the last key: the key itself, gold, and nobody's face -
        /// the screen's key on its pedestal lit gold, or the HUD's gold key alone on its stage, as
        /// its board always drew it.
        /// </summary>
        private void StageLastKey()
        {
            ClearFaces();
            if (frame.Screen) { LightTheKey(true); return; }
            var held = Mark("Last key", faces, KeyArt(), UiTheme.Gold, 64f * frame.Text);
            if (held == null) return;
            var rect = held.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// The block, both nominees side by side in their frames (mockup-10) - ring discs on the
        /// screen's frame - with their names at one size, as the roster's are.
        /// </summary>
        private void StageBlock()
        {
            ClearFaces();
            LightTheKey(false);

            float step = frame.BlockStep;
            float start = -(nominated.Count - 1) * step * 0.5f;
            var names = new List<TMP_Text>(nominated.Count);
            if (frame.Screen)
            {
                float face = frame.BlockW;
                float footprint = ScreenRoster.Footprint(face);
                float centre = -(footprint * .5f + ScreenRoster.Air);
                for (int i = 0; i < nominated.Count; i++)
                {
                    float x = start + i * step;
                    var rim = Disc(faces, "Nominee face", nominated[i], UiTheme.Conflict, face, ScreenRoster.BlockRing,
                        RingArtFor(UiTheme.Conflict), new Vector2(x, centre));
                    HudPrimitives.AddRoleMark(rim, HudPrimitives.RoleMark.Nominee, face);
                    names.Add(NameUnder(faces, "Nominee", nominated[i].Name, ScreenRoster.BlockNamePt, ScreenRoster.BlockNameW, ScreenRoster.BlockNameH,
                        centre - footprint * .5f - ScreenRoster.Gap, x, UiTheme.Weight.SemiBold));
                }
                OneSize(names);
                return;
            }

            for (int i = 0; i < nominated.Count; i++)
                Slot(nominated[i], start + i * step, frame.BlockW, frame.BlockH, UiTheme.Conflict, "Nominee", names);
            OneSize(names);
        }

        /// <summary>
        /// One face on the HUD's board: the pack's slot frame, the photo inside it, and a name plate
        /// across the photo's foot. The plate's label is named <paramref name="label"/> - the block's
        /// are "Nominee", which is how the suite counts who is on it - and is added to
        /// <paramref name="names"/> when the caller wants it, to size a set of plates together.
        /// </summary>
        private RectTransform Slot(Person person, float x, float slotWidth, float slotHeight, Color tint, string label,
            List<TMP_Text> names = null)
        {
            float scale = frame.Text;
            var slot = new GameObject("Nominee slot", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            slot.SetParent(faces, false);
            Place(slot, slotWidth, slotHeight, 0f, x);
            var frameImage = slot.GetComponent<Image>();
            frameImage.raycastTarget = false;
            // The block's frame is the pack's, red hairline and all; a houseguest handed a key is
            // framed in the colour of what the key means instead.
            if (tint != UiTheme.Conflict || !UiTheme.PackSliced(frameImage, PackArt.Nominee, 12f * scale))
            {
                UiTheme.Style(frameImage, UiTheme.SurfaceRaised, 8);
                UiTheme.AddBorder(slot, 8, tint);
            }

            var photo = HudPrimitives.RectPortrait(slot, "Photo", person.Portrait, person.Character,
                new Vector2(slotWidth - 8f * scale, slotHeight - 8f * scale), 6);
            photo.anchorMin = photo.anchorMax = new Vector2(.5f, .5f);
            photo.pivot = new Vector2(.5f, .5f);
            photo.anchoredPosition = Vector2.zero;

            var plate = HudPrimitives.Fill("Name plate", photo, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .86f), 0);
            plate.anchorMin = new Vector2(0f, 0f); plate.anchorMax = new Vector2(1f, 0f);
            plate.pivot = new Vector2(.5f, 0f);
            plate.offsetMin = Vector2.zero; plate.offsetMax = new Vector2(0f, 26f * scale);
            plate.GetComponent<Image>().raycastTarget = false;
            var name = HudPrimitives.Label(label, plate, 15f * scale, UiTheme.Paper, TextAlignmentOptions.Center);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            name.text = person.Name;
            name.enableAutoSizing = true; name.fontSizeMax = name.fontSize; name.fontSizeMin = Mathf.Min(10f * scale, name.fontSize);
            name.rectTransform.anchorMin = Vector2.zero; name.rectTransform.anchorMax = Vector2.one;
            name.rectTransform.offsetMin = new Vector2(4f * scale, 0f); name.rectTransform.offsetMax = new Vector2(-4f * scale, 0f);
            names?.Add(name);
            return slot;
        }

        // ------------------------------------------------------------ the screen's roster (decision 9)

        /// <summary>One houseguest on the roster: the disc, its ring art, the chip and the badges the beats add.</summary>
        private sealed class RosterEntry
        {
            public Person Person;
            public bool HeadOfHousehold;
            public float Face;
            public RectTransform Rim, Chip, NomineeMark;
            public Image RimDisc, RingArt, ChipArt;
            public TMP_Text Name, ChipText;
        }

        /// <summary>
        /// The roster across the band, either side of the stage's column: a disc, a name and an empty
        /// chip for everyone in the house in the order given, the Head of Household first with the
        /// crown and the HOH chip. Reading order is the mockup's - left to right, top to bottom - so
        /// the Head of Household is the first face of the first row. The names are drawn at one size,
        /// the largest at which every one of them fits its box (<see cref="OneSize"/>). The labels are
        /// named "Roster name" and "Roster badge text", never "Nominee" (the block's count) nor
        /// anything starting with "Key " (the key slots' count).
        /// </summary>
        private void BuildRoster()
        {
            roster.Clear();
            int n = cast.Count;
            if (n == 0) return;
            var sizes = RosterSizes.For(n);
            float footprint = ScreenRoster.Footprint(sizes.Face);
            float rowH = footprint + ScreenRoster.Gap + sizes.NameH + ScreenRoster.Gap * .5f + sizes.ChipH;
            float blockH = sizes.Rows * rowH + (sizes.Rows - 1) * ScreenRoster.RowGap;
            float top = -(ScreenRoster.BandHeight - blockH) * .5f;
            float nameW = sizes.Step - ScreenRoster.Inset;

            rosterRow = new GameObject("Roster", typeof(RectTransform)).GetComponent<RectTransform>();
            rosterRow.SetParent(column, false);
            Place(rosterRow, frame.Width, ScreenRoster.BandHeight, ScreenRoster.BandTop);

            int first = sizes.Rows == 1 ? n : (n + 1) / 2;
            int index = 0;
            for (int row = 0; row < sizes.Rows; row++)
            {
                int count = row == 0 ? first : n - first;
                float rowTop = top - row * (rowH + ScreenRoster.RowGap);
                // A row short of full keeps to the slots nearest the column, more on the left when
                // the count is odd, so the Head of Household is still read first.
                int left = Mathf.Min(sizes.Side, (count + 1) / 2), right = Mathf.Min(sizes.Side, count - left);
                for (int i = 0; i < left + right; i++)
                {
                    bool onLeft = i < left;
                    int slot = onLeft ? left - 1 - i : i - left;     // 0 is the slot nearest the column
                    float x = (onLeft ? -1f : 1f) * (ScreenRoster.Column * .5f + sizes.Step * .5f + slot * sizes.Step);
                    var person = cast[index];
                    bool hoh = index == 0 && !string.IsNullOrEmpty(hohName);
                    var entry = new RosterEntry { Person = person, HeadOfHousehold = hoh, Face = sizes.Face };
                    var ringColour = hoh ? UiTheme.Gold : UiTheme.Brass;
                    entry.Rim = Disc(rosterRow, "Roster face", person, ringColour, sizes.Face, ScreenRoster.Ring,
                        RingArtFor(ringColour), new Vector2(x, rowTop - footprint * .5f));
                    entry.RimDisc = entry.Rim.GetComponent<Image>();
                    entry.RingArt = RingArtOf(entry.Rim);
                    if (hoh)
                    {
                        var crown = HudPrimitives.AddRoleMark(entry.Rim, HudPrimitives.RoleMark.HeadOfHousehold, sizes.Face);
                        SwapRoleGlyph(crown, CrownArt());
                    }
                    float nameTop = rowTop - footprint - ScreenRoster.Gap;
                    entry.Name = NameUnder(rosterRow, "Roster name", person.Name, sizes.NamePt, nameW, sizes.NameH, nameTop, x, UiTheme.Weight.Medium);

                    float chipTop = nameTop - sizes.NameH - ScreenRoster.Gap * .5f;
                    entry.Chip = HudPrimitives.Fill("Roster badge", rosterRow, UiTheme.Muted, Mathf.RoundToInt(sizes.ChipH * .5f) - 1);
                    Place(entry.Chip, nameW, sizes.ChipH, chipTop, x);
                    entry.ChipArt = entry.Chip.GetComponent<Image>();
                    entry.ChipArt.raycastTarget = false;
                    UiTheme.PackSliced(entry.ChipArt, PackArt.Pack9NominationCeremonyStatusTagFill, sizes.ChipH * .5f - 2f, UiTheme.Muted);
                    entry.ChipText = HudPrimitives.Label("Roster badge text", entry.Chip, sizes.ChipPt, UiTheme.Ink, TextAlignmentOptions.Center);
                    var medium = UiTheme.Font(UiTheme.Weight.Medium);
                    if (medium != null) entry.ChipText.font = medium;
                    entry.ChipText.characterSpacing = 1f;
                    // One word on one line, drawn smaller rather than cut: the HUD's labels truncate.
                    entry.ChipText.textWrappingMode = TextWrappingModes.NoWrap;
                    entry.ChipText.enableAutoSizing = true;
                    entry.ChipText.fontSizeMax = sizes.ChipPt;
                    entry.ChipText.fontSizeMin = ScreenRoster.ChipMinPt;
                    entry.ChipText.text = string.Empty;
                    entry.ChipText.rectTransform.anchorMin = Vector2.zero;
                    entry.ChipText.rectTransform.anchorMax = Vector2.one;
                    entry.ChipText.rectTransform.offsetMin = new Vector2(6f, 0f);
                    entry.ChipText.rectTransform.offsetMax = new Vector2(-6f, 0f);
                    entry.Chip.gameObject.SetActive(false);
                    roster.Add(entry);
                    index++;
                }
            }
            var names = new List<TMP_Text>(roster.Count);
            foreach (var entry in roster) names.Add(entry.Name);
            OneSize(names);
            DressRoster(0, false);
        }

        /// <summary>
        /// What the roster says at this beat: HOH on the Head of Household from the start, SAFE on
        /// each of the first <paramref name="keysOut"/> keys' holders as that key comes out, and
        /// NOMINATED, with the target, on the block only once <paramref name="block"/> is up. Every
        /// other chip stays empty, so the roster says nothing the keys have not.
        /// </summary>
        private void DressRoster(int keysOut, bool block)
        {
            foreach (var entry in roster)
            {
                string id = entry.Person.Id;
                int key = id == null ? -1 : safe.FindIndex(p => p.Id == id);
                bool onBlock = block && id != null && nominated.Exists(p => p.Id == id);
                string word = entry.HeadOfHousehold ? "HOH" : key >= 0 && key < keysOut ? "SAFE" : onBlock ? "NOMINATED" : null;
                var tint = entry.HeadOfHousehold ? UiTheme.Gold : word == "SAFE" ? UiTheme.Positive : onBlock ? UiTheme.Conflict : UiTheme.Brass;

                entry.RimDisc.color = tint;
                SetRingArt(entry.RingArt, RingArtFor(tint), tint);
                if (word == null) entry.Chip.gameObject.SetActive(false);
                else
                {
                    entry.Chip.gameObject.SetActive(true);
                    entry.ChipArt.color = tint;
                    entry.ChipText.text = word;
                    entry.ChipText.color = UiTheme.OnColor(tint);
                }
                if (onBlock && entry.NomineeMark == null)
                    entry.NomineeMark = HudPrimitives.AddRoleMark(entry.Rim, HudPrimitives.RoleMark.Nominee, entry.Face);
            }
        }

        /// <summary>
        /// A ring disc as the kit draws one, under <paramref name="parent"/> at <paramref name="at"/>
        /// from its top-centre, named <paramref name="name"/>: the pack's ring - drawn larger than
        /// the face by the hole's share, so the ring hugs the face and its glow stands outside -
        /// where the pack is there, and the kit's plain ring where it is not.
        /// </summary>
        private static RectTransform Disc(Transform parent, string name, Person person, Color ring, float face, float ringWidth,
            Sprite ringArt, Vector2 at)
        {
            var rim = HudPrimitives.Portrait(parent, person.Portrait, ring, face, ringArt != null ? 0f : ringWidth, false, person.Character);
            rim.name = name;
            rim.anchorMin = rim.anchorMax = new Vector2(.5f, 1f);
            rim.pivot = new Vector2(.5f, .5f);
            rim.anchoredPosition = at;
            if (ringArt == null) return rim;
            float size = ScreenRoster.Footprint(face);
            var art = new GameObject("Ring art", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            art.SetParent(rim, false);
            art.SetAsFirstSibling();
            art.anchorMin = art.anchorMax = new Vector2(.5f, .5f);
            art.pivot = new Vector2(.5f, .5f);
            art.anchoredPosition = Vector2.zero;
            art.sizeDelta = new Vector2(size, size);
            var image = art.GetComponent<Image>();
            image.sprite = ringArt;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = ring;
            image.raycastTarget = false;
            return rim;
        }

        private static Image RingArtOf(RectTransform rim)
        {
            foreach (Transform child in rim) if (child.name == "Ring art") return child.GetComponent<Image>();
            return null;
        }

        /// <summary>
        /// The pack's ring for a tint: the glowing one on the block, the plain one for everybody
        /// else. The pack's safe_ring is portrait_ring's picture and its nominee_ring is
        /// portrait_ring_glow's, so the rings are told apart by tint, not by file.
        /// </summary>
        private static Sprite RingArtFor(Color tint) =>
            tint == UiTheme.Conflict ? UiTheme.Pack(PackArt.Pack9NominationCeremonyNomineeRing) : UiTheme.Pack(PackArt.Pack9NominationCeremonySafeRing);

        private static void SetRingArt(Image art, Sprite sprite, Color tint)
        {
            if (art == null) return;
            if (sprite != null) art.sprite = sprite;
            art.color = tint;
        }

        /// <summary>The pack's crown on the badge's glyph where the pack is there; the generated crown stays otherwise.</summary>
        private static void SwapRoleGlyph(RectTransform badge, Sprite art)
        {
            if (badge == null || art == null) return;
            foreach (Transform child in badge)
            {
                if (child.name != "Role glyph") continue;
                var image = child.GetComponent<Image>();
                if (image != null) image.sprite = art;
            }
        }

        /// <summary>A name under a face, on one line: drawn smaller rather than wrapped when the box is narrower than it.</summary>
        private static TMP_Text NameUnder(Transform parent, string name, string text, float size, float width, float height, float y, float x,
            UiTheme.Weight weight)
        {
            var label = HudPrimitives.Label(name, parent, size, UiTheme.Paper, TextAlignmentOptions.Center);
            var font = UiTheme.Font(weight);
            if (font != null) label.font = font;
            label.text = text ?? string.Empty;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMax = size;
            label.fontSizeMin = Mathf.Min(ScreenRoster.NameMinPt, size);
            Place(label.rectTransform, width, height, y, x);
            return label;
        }

        /// <summary>
        /// Draws a set of names at one size: the largest at which every one of them fits its box,
        /// never above their type. Each label auto-sizes to its own box, which drew "Noah Kim" at
        /// the full thirty points beside "Riley Johnson" at twenty on the same row - two weights of
        /// name on one roster (the nomination-card-roster-8 capture). Each is fitted on its own
        /// first and every box then takes the smallest of those fits as its ceiling, so a name only
        /// ever shrinks, and the whole set shrinks together. A label not yet awake measures nothing
        /// and keeps its type, which leaves the set auto-sized name by name.
        /// </summary>
        private static void OneSize(List<TMP_Text> names)
        {
            float size = float.MaxValue;
            foreach (var name in names)
            {
                if (name == null) continue;
                name.ForceMeshUpdate(true);
                size = Mathf.Min(size, name.fontSize);
            }
            if (size == float.MaxValue) return;
            foreach (var name in names)
            {
                if (name == null) continue;
                name.fontSizeMax = size;
                name.fontSize = size;
            }
        }

        /// <summary>
        /// The mockups' glass ground behind the card's column (VISUAL-TARGET.md §4, mockup-08 and
        /// -10): the night background at 85 %, a cyan hairline on the edge and a soft glow outside
        /// it. Built as the column's first child so every piece of the ceremony draws over it, and
        /// stretched to the column so it grows with the large-text preference. Its name deliberately
        /// does not begin with "Key ": that prefix is how the suite counts the ceremony's key slots.
        ///
        /// <para>On the set's screen the glass stands on a ground of its own, a plain fill in ink at
        /// full alpha, as the column's very first child: through 85 % glass alone the screen's own
        /// lit face showed as a warm blob in the band and lighter diagonals from the corners
        /// (UI-UX-PASS-PLAN 1.2). The ground is the kit's own rounded fill and not the pack's
        /// ceremony panel: that sprite is a flat white whose centre is 236 of 255, and through the
        /// nineteen in 255 it let pass the bright board behind the card lifted the band's pixels
        /// (0.13 where the ground is 0.04, the frames being blended in linear light). The HUD's
        /// card keeps its glass over the room.</para>
        /// </summary>
        private static RectTransform CardGlass(RectTransform column, float glassMargin, float neonMargin, bool screen)
        {
            var glass = HudPrimitives.Fill("Card glass", column, UiTheme.GlassFill, UiTheme.GlassRadius);
            glass.SetAsFirstSibling();
            glass.anchorMin = Vector2.zero;
            glass.anchorMax = Vector2.one;
            glass.offsetMin = new Vector2(-glassMargin, -glassMargin);
            glass.offsetMax = new Vector2(glassMargin, glassMargin);
            UiTheme.Glass(glass, UiTheme.GlassRadius);
            if (screen)
            {
                var ink = new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 1f);
                var ground = HudPrimitives.Fill("Screen ground", column, ink, UiTheme.GlassRadius);
                ground.SetAsFirstSibling();
                ground.anchorMin = Vector2.zero;
                ground.anchorMax = Vector2.one;
                ground.offsetMin = new Vector2(-glassMargin, -glassMargin);
                ground.offsetMax = new Vector2(glassMargin, glassMargin);
                ground.GetComponent<Image>().raycastTarget = false;
            }
            // The board's neon: the pack's danger frame, its baked glow outside the glass's edge.
            var neon = new GameObject("Screen frame", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            neon.SetParent(column, false);
            neon.SetSiblingIndex(glass.GetSiblingIndex() + 1);
            neon.anchorMin = Vector2.zero; neon.anchorMax = Vector2.one;
            neon.offsetMin = new Vector2(-neonMargin, -neonMargin);
            neon.offsetMax = new Vector2(neonMargin, neonMargin);
            var image = neon.GetComponent<Image>();
            image.raycastTarget = false;
            if (UiTheme.PackSliced(image, PackArt.PanelDanger, Mathf.Max(12f, neonMargin * 1.2f)))
            {
                // Only its edge and glow: the glass under it is the screen's ground.
                image.fillCenter = false;
            }
            else image.color = new Color(0f, 0f, 0f, 0f);
            return glass;
        }

        private static void Place(RectTransform rect, float width, float height, float y, float x = 0f)
        {
            rect.anchorMin = new Vector2(.5f, 1f);
            rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
