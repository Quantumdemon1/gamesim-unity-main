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

        private CanvasGroup group;
        private RectTransform column, scrim, slotRow, stage;
        private TMP_Text eyebrow, title, hohLine, progress, controls, speedMark;
        private readonly List<RectTransform> slots = new List<RectTransform>();
        private List<Person> safe = new List<Person>();
        private List<Person> nominated = new List<Person>();
        private CeremonyPace pace = CeremonyPace.Suspenseful;
        private int shown = -1;
        private float elapsed, upFor, speed = 1f;
        private bool playing, reduced, blockShown, lastKeyPending, usingPad;

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
        /// must not be one that says who is safe before the card does.
        /// </summary>
        public bool Play(int week, string hohName, bool hohIsPlayer, IList<Person> safeHouseguests,
            IList<Person> block, bool reducedMotion, CeremonyPace pace = CeremonyPace.Suspenseful)
        {
            if (safeHouseguests == null || block == null || block.Count == 0) return false;

            safe = new List<Person>(safeHouseguests);
            nominated = new List<Person>(block);
            this.pace = pace;

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
            return true;
        }

        public void Cancel()
        {
            playing = false;
            if (group != null) group.alpha = 0f;
            if (column != null) column.gameObject.SetActive(false);
            if (scrim != null) scrim.gameObject.SetActive(false);
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
                elapsed = Mathf.Max(elapsed, KeysEnd);
                Show(safe.Count, false, false);
                Block();
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

            if (elapsed >= KeysEnd && !blockShown) Block();
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

            for (int i = 0; i < slots.Count; i++)
                slots[i].GetComponent<Image>().color = i < count ? UiTheme.Positive
                    : pending && i == slots.Count - 1 ? UiTheme.Gold : UiTheme.Outline;

            if (pending)
            {
                // Everybody still waiting is waiting on this key - the last safe name and the block
                // alike - so the card says that one is left, and never whose.
                progress.text = "One key left";
                StageLastKey();
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
        private void Block()
        {
            blockShown = true;
            lastKeyPending = false;
            progress.text = nominated.Count == 2 ? "Nominated for eviction" : "On the block";
            // The mockup's subtitle once the keys are out: the screen now shows who they are.
            hohLine.text = nominated.Count == 1 ? "Tonight's Nominee" : "Tonight's Nominees";
            StageBlock();
            Raise(HouseAudio.Cue.Nomination);
        }

        private void Build()
        {
            var root = (RectTransform)transform;
            if (column != null)
            {
                foreach (Transform child in column) Destroy(child.gameObject);
                slots.Clear();
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

            float scale = Mathf.Max(0.5f, FontScale);
            const float width = CardWidth;
            column.anchoredPosition = new Vector2(0f, -CardTop * scale);
            column.sizeDelta = new Vector2(width * scale, CardHeight * scale);
            CardGlass(column, scale);

            eyebrow = HudPrimitives.Label("Week", column, 11f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            eyebrow.characterSpacing = 10f;
            Place(eyebrow.rectTransform, width * scale, 16f * scale, -10f * scale);

            // A broadcast's fast-forward bug in the screen's corner, up only while the reveal is sped
            // up, so a player who pressed Space by accident can see why the keys are hurrying.
            speedMark = HudPrimitives.Label("Speed", column, 11f * scale, UiTheme.Glow, TextAlignmentOptions.Right);
            speedMark.text = CeremonyTakeover.SpeedCaption;
            Place(speedMark.rectTransform, 150f * scale, 16f * scale, -10f * scale, (width * .5f - 87f) * scale);
            speedMark.gameObject.SetActive(false);

            // A trophy and the title in the display weight, in the glow blue the board is lit in.
            title = HudPrimitives.Label("Title", column, 22f * scale, UiTheme.Glow, TextAlignmentOptions.Center);
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) title.font = bold;
            title.characterSpacing = 3f;
            title.text = "NOMINATION CEREMONY";
            Place(title.rectTransform, width * scale, 30f * scale, -26f * scale);
            float titleWidth = title.GetPreferredValues(title.text).x;
            var trophy = HudPrimitives.Glyph("Title mark", column, "trophy", UiTheme.Glow, Vector2.zero, 24f * scale);
            if (trophy != null)
            {
                var mark = trophy.rectTransform;
                mark.anchorMin = mark.anchorMax = new Vector2(.5f, 1f);
                mark.pivot = new Vector2(1f, 1f);
                mark.anchoredPosition = new Vector2(-(titleWidth * .5f) - 8f * scale, -29f * scale);
            }

            hohLine = HudPrimitives.Label("HoH", column, 15f * scale, UiTheme.Accent, TextAlignmentOptions.Center);
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            if (medium != null) hohLine.font = medium;
            Place(hohLine.rectTransform, width * scale, 22f * scale, -58f * scale);

            // The stage: whichever faces the ceremony is on right now.
            stage = new GameObject("Stage", typeof(RectTransform)).GetComponent<RectTransform>();
            stage.SetParent(column, false);
            Place(stage, width * scale, SlotHeight * scale, -88f * scale);

            // One key per houseguest who draws, lit as each is handed out.
            slotRow = new GameObject("Keys", typeof(RectTransform)).GetComponent<RectTransform>();
            slotRow.SetParent(column, false);
            Place(slotRow, width * scale, 20f * scale, -(96f + SlotHeight) * scale);

            float pip = 16f * scale, step = 24f * scale;
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
            Place(progress.rectTransform, width * scale, 18f * scale, -(120f + SlotHeight) * scale);

            // A hairline rule and a quiet instruction close the card, as the web build closes
            // every phase card.
            var rule = HudPrimitives.Fill("Rule", column,
                new Color(UiTheme.Muted.r, UiTheme.Muted.g, UiTheme.Muted.b, 0.35f), 1);
            Place(rule, 96f * scale, 1f, -(144f + SlotHeight) * scale);
            rule.GetComponent<Image>().raycastTarget = false;

            var dismiss = HudPrimitives.Label("Dismiss", column, 11f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            dismiss.text = CeremonyTakeover.DismissCaption;
            Place(dismiss.rectTransform, width * scale, 16f * scale, -(150f + SlotHeight) * scale);

            // And what else it answers to: the reveal can be sped up, or skipped to the block. The
            // keys named follow the device last used on the card.
            controls = HudPrimitives.Label("Controls", column, 11f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            controls.text = CeremonyTakeover.ControlsFor(usingPad);
            Place(controls.rectTransform, width * scale, 16f * scale, -(168f + SlotHeight) * scale);

            // The show's line under the screen, the way mockup-10 writes it on the wall below the
            // board.
            var tagline = HudPrimitives.Label("Tagline", column, 12f * scale, UiTheme.Glow, TextAlignmentOptions.Center);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) tagline.font = semibold;
            tagline.fontStyle = FontStyles.Italic;
            tagline.characterSpacing = 12f;
            tagline.text = "SAME HOUSE.  DIFFERENT STORIES.";
            Place(tagline.rectTransform, width * 1.4f * scale, 18f * scale, -(CardHeight + 22f) * scale);
        }

        /// <summary>The screen's size and where it hangs, in reference units at the standard text size.</summary>
        private const float CardWidth = 420f;
        private const float CardHeight = 330f;
        private const float CardTop = 96f;
        private const float SlotWidth = 104f;
        private const float SlotHeight = 134f;

        /// <summary>Draws one houseguest on the stage, or clears it.</summary>
        private void Stage(Person? person, string badge, Color tint)
        {
            for (int i = stage.childCount - 1; i >= 0; i--) Destroy(stage.GetChild(i).gameObject);
            if (person == null) return;

            float scale = Mathf.Max(0.5f, FontScale);
            var slot = Slot(person.Value, 0f, scale, tint, "Name");
            if (string.IsNullOrEmpty(badge)) return;
            // The word the key means, pinned to the photo's shoulder.
            var chip = HudPrimitives.Fill("Badge", slot, tint, 4);
            chip.anchorMin = chip.anchorMax = new Vector2(.5f, 1f);
            chip.pivot = new Vector2(.5f, .5f);
            chip.anchoredPosition = Vector2.zero;
            chip.sizeDelta = new Vector2(70f * scale, 20f * scale);
            chip.GetComponent<Image>().raycastTarget = false;
            var chipText = HudPrimitives.Label("Badge text", chip, 12f * scale, UiTheme.Ink, TextAlignmentOptions.Center);
            chipText.text = badge;
            chipText.rectTransform.anchorMin = Vector2.zero;
            chipText.rectTransform.anchorMax = Vector2.one;
            chipText.rectTransform.offsetMin = Vector2.zero;
            chipText.rectTransform.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// The stage during the beat before the last key: the key itself, held up in the gold of the
        /// slot waiting for it, and nobody's face. Its name deliberately does not begin with "Key ",
        /// the prefix the suite counts the key slots by.
        /// </summary>
        private void StageLastKey()
        {
            Stage(null, null, UiTheme.Accent);
            float scale = Mathf.Max(0.5f, FontScale);
            var held = HudPrimitives.Glyph("Last key", stage, "key", UiTheme.Gold, Vector2.zero, 64f * scale);
            if (held == null) return;
            var rect = held.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
        }

        /// <summary>The block, both nominees side by side in their frames (mockup-10).</summary>
        private void StageBlock()
        {
            for (int i = stage.childCount - 1; i >= 0; i--) Destroy(stage.GetChild(i).gameObject);

            float scale = Mathf.Max(0.5f, FontScale);
            float step = (SlotWidth + 44f) * scale;
            float start = -(nominated.Count - 1) * step * 0.5f;
            for (int i = 0; i < nominated.Count; i++)
                Slot(nominated[i], start + i * step, scale, UiTheme.Conflict, "Nominee");
        }

        /// <summary>
        /// One face on the screen: the pack's slot frame, the photo inside it, and a name plate
        /// across the photo's foot. The plate's label is named <paramref name="label"/> - the block's
        /// are "Nominee", which is how the suite counts who is on it.
        /// </summary>
        private RectTransform Slot(Person person, float x, float scale, Color tint, string label)
        {
            var slot = new GameObject("Nominee slot", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            slot.SetParent(stage, false);
            Place(slot, SlotWidth * scale, SlotHeight * scale, 0f, x);
            var frame = slot.GetComponent<Image>();
            frame.raycastTarget = false;
            // The block's frame is the pack's, red hairline and all; a houseguest handed a key is
            // framed in the colour of what the key means instead.
            if (tint != UiTheme.Conflict || !UiTheme.PackSliced(frame, PackArt.Nominee, 12f * scale))
            {
                UiTheme.Style(frame, UiTheme.SurfaceRaised, 8);
                UiTheme.AddBorder(slot, 8, tint);
            }

            var photo = HudPrimitives.RectPortrait(slot, "Photo", person.Portrait, person.Character,
                new Vector2((SlotWidth - 8f) * scale, (SlotHeight - 8f) * scale), 6);
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
            name.enableAutoSizing = true; name.fontSizeMax = name.fontSize; name.fontSizeMin = Mathf.Min(10f, name.fontSize);
            name.rectTransform.anchorMin = Vector2.zero; name.rectTransform.anchorMax = Vector2.one;
            name.rectTransform.offsetMin = new Vector2(4f, 0f); name.rectTransform.offsetMax = new Vector2(-4f, 0f);
            return slot;
        }

        /// <summary>
        /// The mockups' glass ground behind the card's column (VISUAL-TARGET.md §4, mockup-08 and
        /// -10): the night background at 85 %, a cyan hairline on the edge and a soft glow outside
        /// it. Built as the column's first child so every piece of the ceremony draws over it, and
        /// stretched to the column so it grows with the large-text preference. Its name deliberately
        /// does not begin with "Key ": that prefix is how the suite counts the ceremony's key slots.
        /// </summary>
        private static RectTransform CardGlass(RectTransform column, float scale)
        {
            var glass = HudPrimitives.Fill("Card glass", column, UiTheme.GlassFill, UiTheme.GlassRadius);
            glass.SetAsFirstSibling();
            glass.anchorMin = Vector2.zero;
            glass.anchorMax = Vector2.one;
            glass.offsetMin = new Vector2(-12f * scale, -12f * scale);
            glass.offsetMax = new Vector2(12f * scale, 12f * scale);
            UiTheme.Glass(glass, UiTheme.GlassRadius);
            // The board's neon: the pack's danger frame, its baked glow outside the glass's edge.
            var neon = new GameObject("Screen frame", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            neon.SetParent(column, false);
            neon.SetSiblingIndex(glass.GetSiblingIndex() + 1);
            neon.anchorMin = Vector2.zero; neon.anchorMax = Vector2.one;
            neon.offsetMin = new Vector2(-30f * scale, -30f * scale);
            neon.offsetMax = new Vector2(30f * scale, 30f * scale);
            var image = neon.GetComponent<Image>();
            image.raycastTarget = false;
            if (UiTheme.PackSliced(image, PackArt.PanelDanger, 36f * scale))
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
