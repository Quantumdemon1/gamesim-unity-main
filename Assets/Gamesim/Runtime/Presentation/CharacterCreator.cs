using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Setup step two: the screen where you are not one of the cards.
    ///
    /// <para><see cref="CastSelect"/> answered "who are you playing as" with a grid of people who
    /// already existed. This answers the question the reference build asks next, and the one this
    /// port has never asked at all — who are you, if you are nobody on the list. Name, age,
    /// occupation, hometown, bio and pronouns; two personality traits; eight stats starting at five
    /// with five spare points between them.</para>
    ///
    /// <para>It decides nothing. Like the cast screen it collects a
    /// <see cref="SeasonBuilder.Choice"/> and hands it back, so a houseguest that cannot be saved
    /// fails where the slot logic already knows how to keep the current season intact.</para>
    ///
    /// <para>Every stat row is a pair of real buttons whose labels name the stat — "Raise social",
    /// "Lower social" — rather than a plus and a minus that read as "button, button" to a screen
    /// reader. The same reason the cast cards spell out "Playing as" instead of relying on a gold
    /// border.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterCreator : MonoBehaviour
    {
        private const float Width = 1180f;
        private const float Pad = 28f;

        /// <summary>Captions tests and the tour find these controls by.</summary>
        public const string StartCaption = "Start with this houseguest";
        public const string BackCaption = "Back to the cast";

        /// <summary>How the cast screen offers this one.</summary>
        public const string CreateCaption = "Create your own houseguest";
        public const string CustomiseCaption = "Customise this houseguest";

        public static string RaiseCaption(string stat) => "Raise " + stat;
        public static string LowerCaption(string stat) => "Lower " + stat;

        private RectTransform content;
        private CanvasGroup group;
        private CanvasScaler scaler;
        private float cursor;

        private CharacterDraft draft = CharacterDraft.Blank();
        private SeasonBuilder.Choice pending;
        private Action<SeasonBuilder.Choice> onStart;
        private Action onBack;

        /// <summary>See <see cref="CastSelect.FontScale"/> — a fixed layout is magnified, not retyped.</summary>
        public float FontScale
        {
            set
            {
                if (scaler == null) return;
                float scale = Mathf.Clamp(value, 0.5f, 2f);
                scaler.referenceResolution = new Vector2(1920f / scale, 1080f / scale);
            }
        }

        public bool IsShowing => group != null && group.alpha > 0f;

        /// <summary>The draft as it currently stands, for tests and for the tour.</summary>
        public CharacterDraft Draft => draft;

        /// <summary>
        /// Above the cast screen, because it is opened from it and returns to it. Raycasts, like the
        /// cast screen and unlike the ceremony overlays: it is a form.
        /// </summary>
        public static CharacterCreator Attach(GameObject owner)
        {
            var root = new GameObject("Gamesim Character Creator",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(owner.transform, false);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 127;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var screen = root.AddComponent<CharacterCreator>();
            screen.scaler = scaler;
            screen.group = root.GetComponent<CanvasGroup>();
            screen.Hide();
            return screen;
        }

        public void Hide()
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        /// <summary>
        /// Opens the form on a draft.
        ///
        /// <para><paramref name="choice"/> carries the roster and house size already settled on the
        /// cast screen, and the card the draft came from if it came from one — that id still matters
        /// after customising, because it is what keeps the player from also being cast as an NPC of
        /// the person they are playing.</para>
        /// </summary>
        public void Show(SeasonBuilder.Choice choice, CharacterDraft start,
            Action<SeasonBuilder.Choice> commit, Action back)
        {
            pending = choice?.Copy() ?? new SeasonBuilder.Choice();
            draft = start ?? CharacterDraft.Blank();
            onStart = commit;
            onBack = back;
            Rebuild();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        /// <summary>Closes without building. Escape routes here.</summary>
        public void Dismiss()
        {
            if (!IsShowing) return;
            var back = onBack;
            Hide();
            back?.Invoke();
        }

        // ---------------------------------------------------------------- build

        private bool rebuilding;

        /// <summary>
        /// Rebuilds the form once, however many things ask during the rebuild. Tearing a field
        /// down raises its <c>onEndEdit</c>, whose listener is this method; a rebuild that
        /// re-entered itself would parent a new scrim under a root that is mid-deactivation, which
        /// Unity refuses. Keyboard focus survives by control name, so pressing a chip does not throw
        /// the selection back to the first field.
        /// </summary>
        private void Rebuild()
        {
            if (rebuilding) return;
            rebuilding = true;
            var events = EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            string keep = selected != null && selected.transform.IsChildOf(transform) ? selected.name : null;
            try
            {
                RebuildForm();
            }
            finally
            {
                rebuilding = false;
            }
            if (keep == null || events == null) return;
            var again = GetComponentsInChildren<Selectable>(true)
                .FirstOrDefault(item => item.name == keep && item.IsActive() && item.IsInteractable());
            if (again != null) events.SetSelectedGameObject(again.gameObject);
        }

        private void RebuildForm()
        {
            // Deactivated before the deferred Destroy, for the reason CastSelect.Rebuild explains:
            // this screen rebuilds on every click and a control would otherwise match twice for a
            // frame.
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            var scrim = HudPrimitives.Fill("Scrim", transform, new Color(0.02f, 0.04f, 0.06f, 0.97f), 1);
            Stretch(scrim);

            var viewport = HudPrimitives.Fill("Viewport", scrim, new Color(0f, 0f, 0f, 0f), 1);
            viewport.anchorMin = new Vector2(0.5f, 0f);
            viewport.anchorMax = new Vector2(0.5f, 1f);
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.sizeDelta = new Vector2(Width, 0f);
            viewport.anchoredPosition = Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>();

            content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            cursor = 0f;
            Header();
            Preview();
            Details();
            TraitChips();
            Stats();
            Footer();

            content.sizeDelta = new Vector2(0f, cursor + Pad);
        }

        private void Header()
        {
            Space(Pad);
            Text("BUILD YOUR HOUSEGUEST", 26f, UiTheme.Gold, 34f, TextAlignmentOptions.Center);
            Text("Who the house meets on the first night. Everything here is yours; the rest of the cast is unchanged.",
                15f, UiTheme.Muted, 24f, TextAlignmentOptions.Center);
            Space(8f);
        }

        /// <summary>
        /// The live preview.
        ///
        /// <para>A wardrobe colour and the generated silhouette, exactly as the cast cards show an
        /// unbuilt houseguest — the same answer to "who is this" before a body exists to render. The
        /// colour is the player's own, so the disc here is what they will actually be wearing.</para>
        /// </summary>
        private void Preview()
        {
            var row = Row(132f);
            var wardrobe = CastPalette.For(ContentCatalog.PlayerId);

            var rim = HudPrimitives.Disc("Ring", row, UiTheme.Gold);
            rim.anchorMin = new Vector2(0.5f, 0.5f);
            rim.anchorMax = new Vector2(0.5f, 0.5f);
            rim.pivot = new Vector2(0.5f, 0.5f);
            rim.sizeDelta = new Vector2(112f, 112f);
            rim.anchoredPosition = new Vector2(-380f, 0f);

            var face = HudPrimitives.Disc("Face", rim, wardrobe);
            face.anchorMin = new Vector2(0.5f, 0.5f);
            face.anchorMax = new Vector2(0.5f, 0.5f);
            face.pivot = new Vector2(0.5f, 0.5f);
            face.sizeDelta = new Vector2(104f, 104f);
            face.anchoredPosition = Vector2.zero;

            var silhouette = UiTheme.Icon("houseguest");
            if (silhouette != null)
            {
                var art = new GameObject("Silhouette", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                art.SetParent(face, false);
                art.anchorMin = new Vector2(0.5f, 0f);
                art.anchorMax = new Vector2(0.5f, 0f);
                art.pivot = new Vector2(0.5f, 0f);
                art.sizeDelta = new Vector2(86f, 86f);
                art.anchoredPosition = new Vector2(0f, 5f);
                var portrait = art.GetComponent<Image>();
                portrait.sprite = silhouette;
                portrait.color = UiTheme.OnColor(wardrobe);
                portrait.preserveAspect = true;
                portrait.raycastTarget = false;
            }

            Caption(row, string.IsNullOrWhiteSpace(draft.Name) ? "Your houseguest" : draft.Name.Trim(),
                20f, UiTheme.Paper, new Vector2(120f, 34f), 560f);
            Caption(row, Summary(), 14f, UiTheme.Muted, new Vector2(120f, 6f), 560f);
            Caption(row, draft.Traits.Count > 0 ? string.Join(" · ", draft.Traits) : "No traits yet",
                13f, UiTheme.Positive, new Vector2(120f, -20f), 560f);
            Space(6f);
        }

        private string Summary()
        {
            var parts = new List<string> { draft.Age.ToString() };
            if (!string.IsNullOrWhiteSpace(draft.Occupation)) parts.Add(draft.Occupation.Trim());
            if (!string.IsNullOrWhiteSpace(draft.Hometown)) parts.Add(draft.Hometown.Trim());
            parts.Add(draft.Pronouns);
            return string.Join(" · ", parts);
        }

        private void Details()
        {
            Space(6f);
            Text("WHO YOU ARE", 15f, UiTheme.Accent, 26f, TextAlignmentOptions.Left);

            Field("Name", draft.Name, CharacterDraft.NameLimit, false,
                value => draft.Name = value);
            Field("Occupation", draft.Occupation, CharacterDraft.ShortLimit, false,
                value => draft.Occupation = value);
            Field("Hometown", draft.Hometown, CharacterDraft.ShortLimit, false,
                value => draft.Hometown = value);
            Field("Bio", draft.Bio, CharacterDraft.BioLimit, true,
                value => draft.Bio = value);

            // Age and pronouns are steppers and chips rather than free text: both have a small set
            // of legal answers, and a form that lets you type an illegal one only to refuse it later
            // is a worse form than one that does not offer it.
            var ages = Row(52f);
            var ageLabel = HudPrimitives.Label("Age", ages, 16f, UiTheme.Paper, TextAlignmentOptions.Center);
            ageLabel.text = "Age " + draft.Age;
            ageLabel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            ageLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ageLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ageLabel.rectTransform.sizeDelta = new Vector2(240f, 26f);
            ageLabel.rectTransform.anchoredPosition = Vector2.zero;
            Chip(ages, "Younger", -300f, 200f, false, () =>
            {
                draft.Age = Mathf.Max(CharacterDraft.MinimumAge, draft.Age - 1);
                Rebuild();
            });
            Chip(ages, "Older", 300f, 200f, false, () =>
            {
                draft.Age = Mathf.Min(CharacterDraft.MaximumAge, draft.Age + 1);
                Rebuild();
            });

            var pronouns = Row(48f);
            float span = 200f;
            float x = -(CharacterDraft.PronounOptions.Length - 1) * span / 2f;
            foreach (var option in CharacterDraft.PronounOptions)
            {
                var pick = option;
                Chip(pronouns, pick, x, span - 12f,
                    string.Equals(draft.Pronouns, pick, StringComparison.Ordinal),
                    () => { draft.Pronouns = pick; Rebuild(); });
                x += span;
            }
        }

        /// <summary>
        /// The seventeen traits, two at a time.
        ///
        /// <para>Adding a third is refused rather than silently swapping one out, and the screen says
        /// which two are held. A form that quietly drops a choice the player made a moment ago is
        /// worse than one that tells them they are full.</para>
        /// </summary>
        private void TraitChips()
        {
            Space(10f);
            Text("PERSONALITY — TWO AT MOST", 15f, UiTheme.Accent, 26f, TextAlignmentOptions.Left);
            Text("Each trait raises two stats: two points on the first, one on the second. Removing it takes the boost back.",
                12f, UiTheme.Muted, 20f, TextAlignmentOptions.Left);

            var names = WebTraits.Boosts.Keys.ToList();
            const int columns = 5;
            const float gutter = 10f;
            float chipWidth = (Width - Pad * 2f - gutter * (columns - 1)) / columns;

            for (int index = 0; index < names.Count; index += columns)
            {
                var bar = Row(42f);
                for (int column = 0; column < columns && index + column < names.Count; column++)
                {
                    var name = names[index + column];
                    bool held = draft.HasTrait(name);
                    float x = -Width / 2f + Pad + chipWidth / 2f + column * (chipWidth + gutter);
                    Chip(bar, name, x, chipWidth, held, () =>
                    {
                        if (held) draft.RemoveTrait(name);
                        else draft.AddTrait(name);
                        Rebuild();
                    });
                }
            }

            string crowded = draft.Crowded;
            Text(draft.Traits.Count == 0
                    ? "No traits chosen. Your stats stay flat."
                    : crowded != null
                        ? "Holding " + string.Join(" and ", draft.Traits) + ". Remove one before adding another."
                        : "Holding " + draft.Traits[0] + ". One more if you want it.",
                12f, crowded != null ? UiTheme.Warning : UiTheme.Muted, 22f, TextAlignmentOptions.Left);
        }

        private void Stats()
        {
            Space(10f);
            Text("STATS — " + draft.Remaining + " OF " + CharacterDraft.SparePoints + " SPARE POINTS LEFT",
                15f, draft.Remaining > 0 ? UiTheme.Gold : UiTheme.Accent, 26f, TextAlignmentOptions.Left);
            Text("Everyone starts at five. Traits move these too, and nothing goes below one or above ten.",
                12f, UiTheme.Muted, 20f, TextAlignmentOptions.Left);

            foreach (var stat in WebTraits.StatNames)
            {
                var name = stat;
                var row = Row(44f);
                double value = WebTraits.Get(draft.Stats, name);

                var label = HudPrimitives.Label(name, row, 15f, UiTheme.Paper, TextAlignmentOptions.Left);
                label.text = Capitalised(name);
                label.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                label.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                label.rectTransform.pivot = new Vector2(0f, 0.5f);
                label.rectTransform.sizeDelta = new Vector2(220f, 24f);
                label.rectTransform.anchoredPosition = new Vector2(10f, 0f);

                // The bar is decoration; the number beside it is the fact. A track on its own is not
                // a value anybody can read out.
                var track = HudPrimitives.Fill("Track", row, UiTheme.Surface, 6);
                track.anchorMin = new Vector2(0f, 0.5f);
                track.anchorMax = new Vector2(0f, 0.5f);
                track.pivot = new Vector2(0f, 0.5f);
                track.sizeDelta = new Vector2(360f, 12f);
                track.anchoredPosition = new Vector2(240f, 0f);

                var fill = HudPrimitives.Fill("Fill", track, UiTheme.Accent, 6);
                fill.anchorMin = new Vector2(0f, 0f);
                fill.anchorMax = new Vector2(0f, 1f);
                fill.pivot = new Vector2(0f, 0.5f);
                fill.sizeDelta = new Vector2(360f * (float)(value / WebTraits.Maximum), 0f);
                fill.anchoredPosition = Vector2.zero;
                fill.GetComponent<Image>().raycastTarget = false;

                var number = HudPrimitives.Label("Value", row, 16f, UiTheme.Paper, TextAlignmentOptions.Center);
                number.text = value.ToString("0");
                number.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                number.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                number.rectTransform.pivot = new Vector2(0f, 0.5f);
                number.rectTransform.sizeDelta = new Vector2(60f, 24f);
                number.rectTransform.anchoredPosition = new Vector2(630f, 0f);

                Chip(row, LowerCaption(name), 330f, 170f, false, () => { draft.Lower(name); Rebuild(); });
                Chip(row, RaiseCaption(name), 505f, 170f, false, () => { draft.Raise(name); Rebuild(); });
            }
        }

        private void Footer()
        {
            Space(10f);
            bool ready = draft.TryValidate(out var error);
            Text(ready
                    ? draft.Remaining > 0
                        ? "Ready. " + draft.Remaining + " spare point" + (draft.Remaining == 1 ? "" : "s")
                          + " left unspent, which is allowed."
                        : "Ready."
                    : error,
                14f, ready ? UiTheme.Positive : UiTheme.Warning, 24f, TextAlignmentOptions.Center);

            var bar = Row(56f);
            Chip(bar, StartCaption, -150f, 300f, ready, () =>
            {
                if (!draft.TryValidate(out _)) { Rebuild(); return; }
                pending.Authored = draft.Copy();
                var start = onStart;
                Hide();
                start?.Invoke(pending);
            });
            Chip(bar, BackCaption, 180f, 230f, false, Dismiss);
            Space(Pad);
        }

        // ---------------------------------------------------------------- pieces

        /// <summary>
        /// A labelled text field.
        ///
        /// <para>Built on <see cref="EpisodeSpeechInputField"/> rather than a plain
        /// <c>TMP_InputField</c> so Escape closes the screen instead of silently rolling the field
        /// back, and Tab moves on instead of typing a tab — the same two keys the speech field had
        /// to take back, for the same reason.</para>
        ///
        /// <para>The value is written on every keystroke rather than on submit, because the screen
        /// rebuilds itself whenever anything else is touched and an uncommitted field would be lost.
        /// </para>
        /// </summary>
        private void Field(string caption, string value, int limit, bool multiline, Action<string> write)
        {
            float height = multiline ? 92f : 52f;
            var row = Row(height);

            var label = HudPrimitives.Label(caption, row, 14f, UiTheme.Muted, TextAlignmentOptions.Left);
            label.text = Localisation.Text(caption);
            label.rectTransform.anchorMin = new Vector2(0f, 1f);
            label.rectTransform.anchorMax = new Vector2(0f, 1f);
            label.rectTransform.pivot = new Vector2(0f, 1f);
            label.rectTransform.sizeDelta = new Vector2(220f, 20f);
            label.rectTransform.anchoredPosition = new Vector2(10f, 0f);

            var box = HudPrimitives.Fill(caption + " field", row, UiTheme.Surface, 8);
            box.anchorMin = new Vector2(0f, 0f);
            box.anchorMax = new Vector2(1f, 1f);
            box.offsetMin = new Vector2(240f, 4f);
            box.offsetMax = new Vector2(-10f, -4f);
            UiTheme.AddBorder(box, 8, UiTheme.Outline);
            box.GetComponent<Image>().raycastTarget = true;

            var text = HudPrimitives.Label("Text", box, 16f, UiTheme.Paper, TextAlignmentOptions.TopLeft);
            text.text = string.Empty;
            StretchInto(text.rectTransform, 12f, 6f);

            var hint = HudPrimitives.Label("Placeholder", box, 16f, UiTheme.Muted, TextAlignmentOptions.TopLeft);
            hint.text = caption;
            StretchInto(hint.rectTransform, 12f, 6f);

            var input = box.gameObject.AddComponent<EpisodeSpeechInputField>();
            input.textViewport = box;
            input.textComponent = text;
            input.placeholder = hint;
            input.characterLimit = limit;
            input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            input.text = value ?? string.Empty;
            input.onValueChanged.AddListener(written => write(written));
            // The preview only catches up when the field is left. Rebuilding on every keystroke
            // would destroy the field being typed into. And only while the field is alive: a
            // focused field raises onEndEdit from its own OnDisable, which is also what a scene
            // unload or a rebuild does to it, and a form rebuilt under a root being torn down is
            // a scrim parented mid-deactivation.
            input.onEndEdit.AddListener(_ => { if (input.isActiveAndEnabled && isActiveAndEnabled) Rebuild(); });
        }

        private static Button Chip(Transform parent, string text, float x, float width, bool active, Action action)
        {
            var pill = HudPrimitives.Fill(text, parent, active ? UiTheme.AccentDeep : UiTheme.SurfaceRaised, 18);
            pill.anchorMin = new Vector2(0.5f, 0.5f);
            pill.anchorMax = new Vector2(0.5f, 0.5f);
            pill.pivot = new Vector2(0.5f, 0.5f);
            pill.sizeDelta = new Vector2(width, 36f);
            pill.anchoredPosition = new Vector2(x, 0f);
            UiTheme.AddBorder(pill, 18, active ? UiTheme.Accent : UiTheme.Outline);

            var image = pill.GetComponent<Image>();
            image.raycastTarget = true;

            var label = HudPrimitives.Label("Label", pill, 13f, active ? UiTheme.Paper : UiTheme.Muted,
                TextAlignmentOptions.Center);
            label.text = Localisation.Text(text);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(6f, 0f);
            label.rectTransform.offsetMax = new Vector2(-6f, 0f);

            var button = pill.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            return button;
        }

        private static void Caption(Transform parent, string value, float size, Color colour,
            Vector2 offset, float width)
        {
            var label = HudPrimitives.Label("Caption", parent, size, colour, TextAlignmentOptions.Left);
            label.text = Localisation.Text(value);
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width, size + 8f);
            rect.anchoredPosition = offset;
        }

        private static string Capitalised(string value) =>
            string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value.Substring(1);

        // ---------------------------------------------------------------- layout

        private RectTransform Row(float height)
        {
            var row = HudPrimitives.Fill("Row", content, new Color(0f, 0f, 0f, 0f), 1);
            Place(row, Width - Pad * 2f, height, -cursor);
            cursor += height + 6f;
            return row;
        }

        private void Text(string value, float size, Color colour, float height, TextAlignmentOptions align)
        {
            var label = HudPrimitives.Label("Text", content, size, colour, align);
            label.text = Localisation.Text(value);
            Place(label.rectTransform, Width - Pad * 2f, height, -cursor);
            cursor += height;
        }

        private void Space(float amount) => cursor += amount;

        private static void Place(RectTransform rect, float width, float height, float y)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(0f, y);
        }

        private static void StretchInto(RectTransform rect, float x, float y)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(x, y);
            rect.offsetMax = new Vector2(-x, -y);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
