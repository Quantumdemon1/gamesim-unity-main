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
    /// <summary>Shared library browsing; six visible profiles bound portrait work without changing saved data.</summary>
    public sealed class CharacterProfileBrowser
    {
        public const int PageSize = 6;
        private string query = string.Empty, editingQuery = string.Empty;
        private int page;

        public void Reset() { query = editingQuery = string.Empty; page = 0; }

        public IReadOnlyList<CharacterProfile> Draw(Transform row, IReadOnlyList<CharacterProfile> profiles, Action rebuild)
        {
            // Creator and cast FontScale magnify their whole CanvasScaler reference layout.
            // Keep reference-pixel type sizes here; multiplying again would scale this row twice.
            var matches = profiles.Where(profile => string.IsNullOrEmpty(query)
                || (profile.name ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            int pages = Math.Max(1, (matches.Count + PageSize - 1) / PageSize);
            page = Math.Min(page, pages - 1);
            var owner = row.GetComponentInParent<Canvas>();
            void Refresh(Action update, string focus)
            {
                update(); rebuild();
                if (EventSystem.current == null || owner == null) return;
                var selected = owner.GetComponentsInChildren<Selectable>()
                    .FirstOrDefault(item => item.name == focus && item.IsActive() && item.IsInteractable())
                    ?? owner.GetComponentsInChildren<Selectable>()
                        .FirstOrDefault(item => item.name == "Profile name search" && item.IsActive() && item.IsInteractable());
                if (selected != null) EventSystem.current.SetSelectedGameObject(selected.gameObject);
            }

            var box = HudPrimitives.Fill("Profile name search", row, UiTheme.Surface, 8);
            Place(box, -310f, 440f);
            box.GetComponent<Image>().raycastTarget = true;
            var text = HudPrimitives.Label("Search text", box, 15f, UiTheme.Paper, TextAlignmentOptions.MidlineLeft);
            var hint = HudPrimitives.Label("Search hint", box, 15f, UiTheme.Muted, TextAlignmentOptions.MidlineLeft);
            hint.text = "Search saved houseguests by name";
            foreach (var label in new[] { text, hint })
            {
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(10f, 3f); label.rectTransform.offsetMax = new Vector2(-10f, -3f);
            }
            var input = box.gameObject.AddComponent<EpisodeSpeechInputField>();
            input.textViewport = box; input.textComponent = text; input.placeholder = hint;
            input.characterLimit = 100; input.lineType = TMP_InputField.LineType.SingleLine;
            input.text = editingQuery;
            input.onValueChanged.AddListener(value => editingQuery = value);
            void Search() => Refresh(() => { query = editingQuery.Trim(); editingQuery = query; page = 0; }, box.name);
            input.onSubmit.AddListener(_ => Search());
            Button(row, "Search profiles", -25f, 100f, Search);
            Button(row, "Clear search", 85f, 100f, () => Refresh(Reset, box.name));
            Button(row, "Previous profiles", 215f, 115f,
                () => Refresh(() => page--, "Previous profiles")).interactable = page > 0;
            var pageLabel = HudPrimitives.Label("Profile page", row, 15f, UiTheme.Muted, TextAlignmentOptions.Center);
            Place(pageLabel.rectTransform, 320f, 74f);
            pageLabel.text = matches.Count == 0 ? "0 / 0" : (page + 1) + " / " + pages;
            Button(row, "Next profiles", 442f, 130f,
                () => Refresh(() => page++, "Next profiles")).interactable = page + 1 < pages;
            return matches.Skip(page * PageSize).Take(PageSize).ToArray();
        }

        public static void Thumbnail(Transform row, CharacterProfile profile, float x)
        {
            var portrait = HudPrimitives.Portrait(row, null, UiTheme.Accent, 38f, 2f, false, profile.contestant);
            portrait.name = "Profile portrait " + profile.id;
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.pivot = new Vector2(.5f, .5f); portrait.anchoredPosition = new Vector2(x, 0f);
        }

        private static Button Button(Transform row, string caption, float x, float width, Action click)
        {
            var box = HudPrimitives.Fill(caption, row, UiTheme.SurfaceRaised, 8);
            Place(box, x, width);
            box.GetComponent<Image>().raycastTarget = true;
            var label = HudPrimitives.Label("Label", box, 15f, UiTheme.Paper, TextAlignmentOptions.Center);
            label.text = caption; label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(3f, 0f); label.rectTransform.offsetMax = new Vector2(-3f, 0f);
            var button = box.gameObject.AddComponent<Button>(); button.targetGraphic = box.GetComponent<Image>();
            button.onClick.AddListener(() => click()); return button;
        }

        private static void Place(RectTransform rect, float x, float width)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(width, 38f); rect.anchoredPosition = new Vector2(x, 0f);
        }
    }
}
