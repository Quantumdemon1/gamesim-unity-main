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

            public Person(string id, string name, Texture portrait)
            {
                Id = id; Name = name; Portrait = portrait;
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
        public bool Play(int week, string hohName, IList<Person> safeHouseguests,
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
            hohLine.text = string.IsNullOrEmpty(hohName)
                ? "The keys go up"
                : hohName + " has made their decision";

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

        private void Update()
        {
            if (!playing) return;
            elapsed += Time.unscaledDeltaTime;

            float keysStart = FadeIn + IntroHold;
            float keysEnd = keysStart + safe.Count * PerKey;

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

                scrim = HudPrimitives.Fill("Scrim", root,
                    new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.93f), 1);
                scrim.anchorMin = Vector2.zero; scrim.anchorMax = Vector2.one;
                scrim.offsetMin = Vector2.zero; scrim.offsetMax = Vector2.zero;

                column = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>();
                column.SetParent(root, false);
                column.anchorMin = new Vector2(.5f, .5f);
                column.anchorMax = new Vector2(.5f, .5f);
                column.pivot = new Vector2(.5f, .5f);
            }

            float scale = Mathf.Max(0.5f, FontScale);
            const float width = 900f;
            column.sizeDelta = new Vector2(width * scale, 520f * scale);

            eyebrow = HudPrimitives.Label("Week", column, 15f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            eyebrow.characterSpacing = 14f;
            Place(eyebrow.rectTransform, width * scale, 22f * scale, 0f);

            title = HudPrimitives.Label("Title", column, 44f * scale, UiTheme.Paper, TextAlignmentOptions.Center);
            title.text = "NOMINATION CEREMONY";
            Place(title.rectTransform, width * scale, 56f * scale, -26f * scale);

            hohLine = HudPrimitives.Label("HoH", column, 18f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            hohLine.fontStyle = FontStyles.Italic;
            Place(hohLine.rectTransform, width * scale, 26f * scale, -84f * scale);

            // The stage: whichever face the ceremony is on right now.
            stage = new GameObject("Stage", typeof(RectTransform)).GetComponent<RectTransform>();
            stage.SetParent(column, false);
            Place(stage, width * scale, 230f * scale, -122f * scale);

            // One slot per key, filling green as each is handed out.
            slotRow = new GameObject("Keys", typeof(RectTransform)).GetComponent<RectTransform>();
            slotRow.SetParent(column, false);
            Place(slotRow, width * scale, 26f * scale, -362f * scale);

            float pip = 16f * scale, step = 30f * scale;
            float first = -(safe.Count - 1) * step * 0.5f;
            for (int i = 0; i < safe.Count; i++)
            {
                var slot = HudPrimitives.Fill("Key " + (i + 1), slotRow, UiTheme.Outline, 3);
                slot.anchorMin = new Vector2(.5f, .5f); slot.anchorMax = new Vector2(.5f, .5f);
                slot.pivot = new Vector2(.5f, .5f);
                slot.anchoredPosition = new Vector2(first + i * step, 0f);
                slot.sizeDelta = new Vector2(pip, pip * 1.6f);
                slots.Add(slot);
            }

            progress = HudPrimitives.Label("Progress", column, 17f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            Place(progress.rectTransform, width * scale, 24f * scale, -398f * scale);
        }

        /// <summary>Draws one houseguest on the stage, or clears it.</summary>
        private void Stage(Person? person, string badge, Color tint)
        {
            for (int i = stage.childCount - 1; i >= 0; i--) Destroy(stage.GetChild(i).gameObject);
            if (person == null) return;

            float scale = Mathf.Max(0.5f, FontScale);
            float portrait = 132f * scale;

            var rim = HudPrimitives.Portrait(stage, person.Value.Portrait, tint, portrait, 5f * scale, false);
            rim.anchorMin = new Vector2(.5f, 1f); rim.anchorMax = new Vector2(.5f, 1f); rim.pivot = new Vector2(.5f, 1f);
            rim.anchoredPosition = Vector2.zero;

            var name = HudPrimitives.Label("Name", stage, 30f * scale, UiTheme.Paper, TextAlignmentOptions.Center);
            name.text = person.Value.Name;
            Place(name.rectTransform, 640f * scale, 38f * scale, -(portrait + 12f * scale));

            if (string.IsNullOrEmpty(badge)) return;
            var chip = HudPrimitives.Fill("Badge", stage, tint, 4);
            Place(chip, 120f * scale, 24f * scale, -(portrait + 54f * scale));
            var chipText = HudPrimitives.Label("Badge text", chip, 14f * scale, UiTheme.Ink, TextAlignmentOptions.Center);
            chipText.text = badge;
            chipText.rectTransform.anchorMin = Vector2.zero;
            chipText.rectTransform.anchorMax = Vector2.one;
            chipText.rectTransform.offsetMin = Vector2.zero;
            chipText.rectTransform.offsetMax = Vector2.zero;
        }

        /// <summary>The block, both nominees side by side.</summary>
        private void StageBlock()
        {
            for (int i = stage.childCount - 1; i >= 0; i--) Destroy(stage.GetChild(i).gameObject);

            float scale = Mathf.Max(0.5f, FontScale);
            float portrait = 116f * scale;
            float slotWidth = 250f * scale;
            float start = -(nominated.Count - 1) * slotWidth * 0.5f;

            for (int i = 0; i < nominated.Count; i++)
            {
                var person = nominated[i];

                var rim = HudPrimitives.Portrait(stage, person.Portrait, UiTheme.Danger, portrait, 5f * scale, false);
                rim.anchorMin = new Vector2(.5f, 1f); rim.anchorMax = new Vector2(.5f, 1f); rim.pivot = new Vector2(.5f, 1f);
                rim.anchoredPosition = new Vector2(start + i * slotWidth, 0f);

                var name = HudPrimitives.Label("Nominee", stage, 22f * scale, UiTheme.Paper, TextAlignmentOptions.Center);
                name.text = person.Name;
                Place(name.rectTransform, slotWidth, 30f * scale, -(portrait + 10f * scale), start + i * slotWidth);

                var chip = HudPrimitives.Fill("Nominated", stage, UiTheme.Danger, 4);
                Place(chip, 130f * scale, 23f * scale, -(portrait + 44f * scale), start + i * slotWidth);
                var chipText = HudPrimitives.Label("Nominated text", chip, 13f * scale, UiTheme.Ink, TextAlignmentOptions.Center);
                chipText.text = "NOMINATED";
                chipText.rectTransform.anchorMin = Vector2.zero;
                chipText.rectTransform.anchorMax = Vector2.one;
                chipText.rectTransform.offsetMin = Vector2.zero;
                chipText.rectTransform.offsetMax = Vector2.zero;
            }
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
