using Gamesim.Simulation;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
    /// <para>Same rules as the other overlays: nothing raycasts, it ends on a timer rather than on
    /// acknowledgement, and every name is read from committed state so the ceremony cannot disagree
    /// with the save.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeyCeremony : MonoBehaviour
    {
        private const float FadeIn = 0.30f;
        private const float IntroHold = 1.15f;
        private const float PerKey = 0.62f;
        private const float BlockHold = 2.1f;
        private const float FadeOut = 0.45f;

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

        private CanvasGroup group;
        private RectTransform column, scrim, slotRow, stage;
        private TMP_Text eyebrow, title, hohLine, progress;
        private readonly List<RectTransform> slots = new List<RectTransform>();
        private List<Person> safe = new List<Person>();
        private List<Person> nominated = new List<Person>();
        private int shown = -1;
        private float elapsed;
        private bool playing, reduced, blockShown;

        public float FontScale { get; set; } = 1f;
        public bool IsPlaying => playing;

        /// <summary>True once every key is out and the block is on screen.</summary>
        public bool ShowingBlock => blockShown;

        public float Duration => FadeIn + IntroHold + safe.Count * PerKey + BlockHold + FadeOut;

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
        /// Plays the ceremony. Declines a shape it cannot narrate — no keys to hand out, or nobody
        /// on the block — and the generic card plays instead, so the beat is never silent.
        /// </summary>
        public bool Play(int week, string hohName, bool hohIsPlayer, IList<Person> safeHouseguests,
            IList<Person> block, bool reducedMotion)
        {
            if (safeHouseguests == null || block == null || block.Count == 0) return false;

            safe = new List<Person>(safeHouseguests);
            nominated = new List<Person>(block);

            Build();
            reduced = reducedMotion;
            elapsed = 0f;
            shown = -1;
            blockShown = false;
            playing = true;

            eyebrow.text = "WEEK " + Mathf.Max(1, week);
            // Second person when the player is the one holding the keys. The engine's event text
            // learned this already; a card that says "You has made their decision" undoes it in the
            // most prominent place on screen.
            hohLine.text = string.IsNullOrEmpty(hohName) ? "The keys go up"
                : hohIsPlayer ? "You have made your decision"
                : hohName + " has made their decision";
            hohLine.color = UiTheme.Accent;

            Reveal(0);
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
        /// True once the card has been up long enough to have been read; after that a click moves
        /// it on. The same delay as <see cref="CeremonyTakeover"/>'s, for the same reason: a click
        /// already in flight when the card appears must not skip what it has not yet shown.
        /// </summary>
        private bool Dismissable => elapsed >= FadeIn + 0.35f;

        private void Update()
        {
            if (!playing) return;
            CeremonyOverlays.Showing();
            elapsed += Time.unscaledDeltaTime;

            float keysStart = FadeIn + IntroHold;
            float keysEnd = keysStart + safe.Count * PerKey;

            // The card says "Click anywhere to continue", so a click continues. It used to say so
            // and read nothing. The first click hands out every remaining key and shows the block -
            // skipping the order, never the result - and a click on the block ends the card. Read
            // from the device rather than through a raycaster for the takeover's reason: nothing
            // on a ceremony card may take a click meant for the house.
            if (Dismissable && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (blockShown) { Cancel(); return; }
                elapsed = Mathf.Max(elapsed, keysEnd);
                Reveal(safe.Count);
                Block();
                return;
            }

            if (reduced) group.alpha = 1f;
            else if (elapsed < FadeIn) group.alpha = Eased(elapsed / FadeIn);
            else if (elapsed < keysEnd + BlockHold) group.alpha = 1f;
            else
            {
                float exit = (elapsed - keysEnd - BlockHold) / FadeOut;
                if (exit >= 1f) { Cancel(); return; }
                group.alpha = 1f - Eased(exit);
            }

            if (reduced && elapsed >= Duration) { Cancel(); return; }

            int target = elapsed < keysStart
                ? 0
                : Mathf.Min(safe.Count, Mathf.FloorToInt((elapsed - keysStart) / PerKey) + 1);
            if (target != shown) Reveal(target);

            if (elapsed >= keysEnd && !blockShown) Block();
        }

        private static float Eased(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>Shows the nth key: whoever it belongs to is safe.</summary>
        private void Reveal(int count)
        {
            shown = count;
            for (int i = 0; i < slots.Count; i++)
                slots[i].GetComponent<Image>().color = i < count ? UiTheme.Positive : UiTheme.Outline;

            if (count == 0)
            {
                progress.text = safe.Count + " keys. Two will not get one.";
                Stage(null, null, UiTheme.Accent);
                return;
            }

            var person = safe[count - 1];
            progress.text = "Revealing key " + count + " of " + safe.Count;
            Stage(person, "SAFE", UiTheme.Positive);
        }

        /// <summary>The close: the two who never heard their name.</summary>
        private void Block()
        {
            blockShown = true;
            progress.text = nominated.Count == 2 ? "Nominated for eviction" : "On the block";
            // The mockup's subtitle once the keys are out: the screen now shows who they are.
            hohLine.text = nominated.Count == 1 ? "Tonight's Nominee" : "Tonight's Nominees";
            StageBlock();
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
