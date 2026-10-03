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
    /// port has never asked at all — who are you, if you are nobody on the list. Appearance; name,
    /// age, occupation, hometown, bio and pronouns; two personality traits; eight stats starting at
    /// five with five spare points between them; and a library of houseguests you have made.</para>
    ///
    /// <para>It decides nothing. Like the cast screen it collects a
    /// <see cref="SeasonBuilder.Choice"/> and hands it back, so a houseguest that cannot be saved
    /// fails where the slot logic already knows how to keep the current season intact.</para>
    ///
    /// <para>Drawn as the four creator mockups draw it (<c>CharacterCreator.Chrome</c>): one page a
    /// step, each on glass panels, the live preview standing on its lit platform wherever there is
    /// room for it.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class CharacterCreator : MonoBehaviour
    {
        /// <summary>Captions tests and the tour find these controls by.</summary>
        public const string StartCaption = "Start with this houseguest";
        public const string BackCaption = "Back to the cast";
        public const string ApplySlotCaption = "Apply to cast slot";
        public const string CancelSlotCaption = "Cancel slot edit";

        /// <summary>How the cast screen offers this one.</summary>
        public const string CreateCaption = "Create your own houseguest";
        public const string CustomiseCaption = "Edit appearance";

        public static string RaiseCaption(string stat) => "Raise " + stat;
        public static string LowerCaption(string stat) => "Lower " + stat;

        private CanvasGroup group;
        private CanvasScaler scaler;

        private CharacterDraft draft = CharacterDraft.Blank();
        private SeasonBuilder.Choice pending;
        private Action<SeasonBuilder.Choice> onStart;
        private Action onBack;
        private bool editingCastSlot;
        private string resumeError;

        /// <summary>See <see cref="CastSelect.FontScale"/> — a fixed layout is magnified, not retyped.</summary>
        public float FontScale
        {
            set
            {
                if (scaler == null) return;
                float scale = Mathf.Clamp(value, 0.5f, 2f);
                scaler.referenceResolution = new Vector2(1920f / scale, 1080f / scale);
                if (IsShowing) Rebuild();
            }
        }

        public bool IsShowing => group != null && group.alpha > 0f;

        /// <summary>The draft as it currently stands, for tests and for the tour.</summary>
        public CharacterDraft Draft => draft;
        public CharacterStudioPreview StudioPreview => studioPreview;

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
            if (studioPreview != null) { Destroy(studioPreview.gameObject); studioPreview = null; }
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
            => ShowCore(choice, start, commit, back, EntryMode.Detailed);

        /// <summary>Starts with a name, age, pronouns and a complete starting look.</summary>
        public void ShowQuick(SeasonBuilder.Choice choice, CharacterDraft start,
            Action<SeasonBuilder.Choice> commit, Action back)
            => ShowCore(choice, start, commit, back, EntryMode.Quick);

        private void ShowCore(SeasonBuilder.Choice choice, CharacterDraft start,
            Action<SeasonBuilder.Choice> commit, Action back, EntryMode mode)
        {
            pending = choice?.Copy() ?? new SeasonBuilder.Choice();
            editingCastSlot = false;
            draft = (start ?? CharacterDraft.Blank()).Copy();
            appearanceUndo.Clear(); appearanceRedo.Clear();
            initialAppearance = draft.Appearance?.Clone();
            entryMode = mode;
            lastDetailedPage = "Appearance";
            studioPage = DefaultAppearancePage;
            quickScrollY = 0f;
            profileId = focusedProfile = null;
            confirmingNew = false;
            comparingOriginal = false;
            appearanceNotice = pendingDeleteProfile = libraryMessage = null;
            resumeError = null;
            onStart = commit;
            onBack = back;
            Rebuild();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        /// <summary>Edits one proposed NPC snapshot. Applying returns to cast setup and never starts a season.</summary>
        public void ShowForCastSlot(SeasonBuilder.Choice choice, CharacterDraft start, Action<CharacterDraft> apply, Action cancel)
        {
            Show(choice, start, result => apply?.Invoke(result.Authored.Copy()), cancel);
            editingCastSlot = true;
            Rebuild();
        }

        /// <summary>Closes without building. Escape routes here.</summary>
        public void Dismiss()
        {
            if (!IsShowing) return;
            var back = onBack;
            Hide();
            back?.Invoke();
        }

        /// <summary>Restores the same setup after a failed save; no edits or allocations are reset.</summary>
        public void Resume(string error = null)
        {
            if (onStart == null) return;
            resumeError = error;
            Rebuild();
            group.alpha = 1f; group.blocksRaycasts = true; group.interactable = true;
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
                if (quickControls != null) quickScrollY = quickControls.anchoredPosition.y;
                if (studioControls != null)
                    studioScrollY = lastStudioCategory == appearanceCategory ? studioControls.anchoredPosition.y : 0f;
                RebuildForm();
            }
            finally
            {
                rebuilding = false;
            }
            // Not while the event system is mid-selection: a field losing focus to a press ends its edit
            // there, and selecting again from inside that is refused and costs the press.
            if (keep == null || events == null || events.alreadySelecting) return;
            var again = GetComponentsInChildren<Selectable>(true)
                .FirstOrDefault(item => item.name == keep && item.IsActive() && item.IsInteractable());
            if (again != null) events.SetSelectedGameObject(again.gameObject);
        }

        private void RebuildForm()
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            frame = Frame();
            studioControls = null;
            quickControls = null;
            previewOnPage = false;
            previewStatus = null; retryPreviewButton = null; cardSummary = bioCount = cardAge = null;
            rebuildSoon = false;
            undoAppearanceButton = redoAppearanceButton = null;
            var scrim = HudPrimitives.Fill("Scrim", transform, new Color(.02f, .04f, .06f, .98f), 1);
            Stretch(scrim);
            // The cast screen's ground: the pack's night navy, the studio it opens from.
            var night = UiTheme.Pack(PackArt.BackgroundNavy);
            if (night != null)
            {
                var ground = scrim.GetComponent<Image>();
                ground.sprite = night; ground.type = Image.Type.Simple; ground.color = Color.white;
            }
            BuildBackdrop(scrim);
            BuildHeader(scrim);
            switch (studioPage)
            {
                case QuickPageCaption: BuildQuickPage(scrim); break;
                case "Appearance": BuildAppearancePage(scrim); break;
                case "Identity": BuildIdentityPage(scrim); break;
                case "Personality": BuildPersonalityPage(scrim); break;
                case "My Houseguests": BuildLibraryPage(scrim); break;
                default: BuildReviewPage(scrim); break;
            }
            // Pages without the live preview put it to sleep rather than render it for nobody.
            if (studioPreview != null && !previewOnPage) studioPreview.gameObject.SetActive(false);
            BuildFooter(scrim);
        }

        private string Summary()
        {
            var parts = new List<string> { draft.Age.ToString() };
            if (!string.IsNullOrWhiteSpace(draft.Occupation)) parts.Add(draft.Occupation.Trim());
            if (!string.IsNullOrWhiteSpace(draft.Hometown)) parts.Add(draft.Hometown.Trim());
            parts.Add(draft.Pronouns);
            return string.Join(" · ", parts);
        }

        private static string Capitalised(string value) =>
            string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value.Substring(1);

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
