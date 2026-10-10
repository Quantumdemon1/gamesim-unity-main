using System;
using System.Collections.Generic;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The houseguest directory and a houseguest's profile, as Refinement Kit 6 draws them.
    ///
    /// <para>The directory was one sentence a person - "Dana · Active · Your trust -9" - so the three
    /// things it said ran together and the smallest of them set the size of all three. It is a table
    /// now: face and name, their status in the season, your own trust in them, and their profile,
    /// each in a column of its own. A row is one button, named for the person, and opens the
    /// profile; the filters and the search only choose which rows are shown.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>A directory row is named for its houseguest, so a test can find anyone's.</summary>
        public const string RosterRowPrefix = "Houseguest · ";
        public const string RosterSearchName = "Search houseguests";
        public const string RosterEmptyName = "No houseguests shown";
        public const string ProfileIdentityName = "Profile identity";
        public const string ProfileTrustName = "Profile trust";
        public const string ProfileRecordsName = "Profile records";
        public const string ProfileBackCaption = "Back to Houseguests";

        /// <summary>One row of the directory, in words the director has already decided.</summary>
        public sealed class RosterEntry
        {
            public string Name, Detail, Status, Trust;
            public Color TrustTint;
            public ContestantState Character;
            public Action Open;
        }

        /// <summary>What a profile shows: only records the player's character is allowed to hold.</summary>
        public sealed class ProfileView
        {
            public string Name, Archetype, Facts, Hometown, About, Status, Mood, Trust, TrustNote;
            public Color TrustTint;
            public ContestantState Character;
            public IList<string> Traits = new List<string>();
            public IList<string> Memories = new List<string>();
            public IList<string> Records = new List<string>();
            public Action Back;
        }

        private readonly List<(GameObject Row, string Key)> rosterRows = new List<(GameObject, string)>();
        private GameObject rosterEmpty;
        private TMP_Text rosterEmptyLine;
        private string rosterEmptyCopy;

        /// <summary>
        /// A search field at the right-hand end of a filter row. It narrows the directory in place as
        /// it is typed into - nothing re-renders, so the caret stays where it is - and it keeps what
        /// was typed through a repaint, because the director holds the words.
        /// </summary>
        public TMP_InputField SearchBox(RectTransform row, string placeholder, string text, Action<string> changed)
        {
            if (row == null) return null;
            float s = FontScale, height = 36f * s;
            float width = Mathf.Round(Mathf.Min(380f * s, ContentWidth() * .36f));
            // A repaint while someone is typing builds a new field; give it the caret back.
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool refocus = selected != null && selected.name == RosterSearchName && selected.GetComponent<TMP_InputField>() != null;

            var rect = Panel(RosterSearchName, row, UiTheme.SurfaceRaised, Mathf.RoundToInt(height * .5f) - 1);
            UiTheme.PackSliced(rect.GetComponent<Image>(), PackArt.KitPillFill, height * .5f, UiTheme.SurfaceRaised);
            var edge = new GameObject("Border", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            edge.rectTransform.SetParent(rect, false);
            edge.rectTransform.anchorMin = Vector2.zero; edge.rectTransform.anchorMax = Vector2.one;
            edge.rectTransform.offsetMin = Vector2.zero; edge.rectTransform.offsetMax = Vector2.zero;
            edge.raycastTarget = false;
            if (!UiTheme.PackSliced(edge, PackArt.KitPillEdge, height * .5f, UiTheme.Edge(UiTheme.Emphasis.Interactive)))
                edge.color = new Color(0f, 0f, 0f, 0f);
            Anchor(rect, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0f, -2f * s), new Vector2(width, height));

            float left = 14f * s;
            if (KitGlyph(rect, PackArt.KitIconSearch, UiTheme.Muted, new Vector2(0, .5f), new Vector2(left, 0f), 18f * s) != null)
                left += 28f * s;
            var area = new GameObject("Text area", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            area.SetParent(rect, false);
            Stretch(area, left, 2f, 16f * s, 2f);
            var value = NewText(area, "", 15, Paper);
            var hint = NewText(area, placeholder, 15, UiTheme.Muted);
            foreach (var line in new[] { value, hint })
            {
                Stretch(line.rectTransform, 0f, 0f, 0f, 0f);
                line.alignment = TextAlignmentOptions.MidlineLeft;
                line.textWrappingMode = TextWrappingModes.NoWrap;
            }
            // A pad's d-pad passes over the box rather than stopping in it (EpisodeTextField, Risk R7).
            var input = rect.gameObject.AddComponent<EpisodeTextField>();
            input.textViewport = area; input.textComponent = value; input.placeholder = hint;
            input.lineType = TMP_InputField.LineType.SingleLine; input.characterLimit = 40;
            input.onFocusSelectAll = false;
            input.customCaretColor = true; input.caretColor = Accent;
            input.selectionColor = new Color(Accent.r, Accent.g, Accent.b, .3f);
            input.text = text ?? string.Empty;
            input.onValueChanged.AddListener(words => { changed?.Invoke(words); FilterRoster(words); });
            if (refocus && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(input.gameObject);
                input.ActivateInputField();
                input.MoveTextEnd(false);
            }
            return input;
        }

        /// <summary>
        /// The directory: column heads, then a row a houseguest. <paramref name="emptyCopy"/> is what
        /// the page says when the filter leaves nobody - it has to be true of the filter, so it is
        /// the caller's.
        /// </summary>
        public void RosterTable(IList<RosterEntry> entries, string search, string emptyCopy)
        {
            if (content == null) return;
            rosterRows.Clear();
            float s = FontScale, width = ContentWidth();
            var columns = RosterColumns(width, s);

            var heads = new GameObject("Houseguest columns", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            heads.SetParent(content, false);
            var headSize = heads.GetComponent<LayoutElement>();
            headSize.minHeight = headSize.preferredHeight = 24f * s;
            void Head(string words, float x, float w)
            {
                var label = FixedText(heads, words, 13, UiTheme.Muted, new Vector2(x, -4f * s), new Vector2(w, 18f * s));
                var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
                if (semibold != null) label.font = semibold;
                label.characterSpacing = 4f;
            }
            Head("HOUSEGUEST", columns.Face, columns.Status - columns.Face - 12f * s);
            Head("STATUS", columns.Status, columns.Trust - columns.Status - 12f * s);
            Head("YOUR TRUST", columns.Trust, columns.Profile - columns.Trust - 12f * s);
            Head("PROFILE", columns.Profile, width - columns.Profile);

            foreach (var entry in entries) RosterRow(entry, columns, width);

            rosterEmptyCopy = emptyCopy;
            rosterEmptyLine = FlowText(emptyCopy ?? string.Empty, 17, UiTheme.Muted);
            rosterEmpty = rosterEmptyLine.gameObject;
            rosterEmpty.name = RosterEmptyName;
            FilterRoster(search);
        }

        /// <summary>Shows the rows whose name or card line holds <paramref name="search"/>; hides the rest.</summary>
        public void FilterRoster(string search)
        {
            string needle = (search ?? string.Empty).Trim();
            int shown = 0;
            foreach (var (row, key) in rosterRows)
            {
                if (row == null) continue;
                bool on = needle.Length == 0 || key.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
                row.SetActive(on);
                if (on) shown++;
            }
            if (rosterEmpty == null) return;
            rosterEmpty.SetActive(shown == 0);
            if (shown == 0)
                rosterEmptyLine.text = needle.Length > 0 && rosterRows.Count > 0
                    ? "No houseguest here matches “" + needle + "”."
                    : rosterEmptyCopy ?? string.Empty;
        }

        private readonly struct RosterGrid
        {
            public readonly float Face, Name, Status, Trust, Profile;
            public RosterGrid(float face, float name, float status, float trust, float profile)
            { Face = face; Name = name; Status = status; Trust = trust; Profile = profile; }
        }

        // Kit 6's houseguest row (1564 wide at the 1920 reference): the face at 20, the name at 96,
        // status at 666, trust at 948 and the profile at 1254 - kept as shares of the row, so the
        // columns hold their lines at any width the notebook gives them.
        private static RosterGrid RosterColumns(float width, float s) =>
            new RosterGrid(18f * s, 78f * s, Mathf.Round(width * .426f), Mathf.Round(width * .606f), Mathf.Round(width * .79f));

        private const float RosterFace = 44f;
        private const float RosterRowHeight = 66f;

        private void RosterRow(RosterEntry entry, RosterGrid columns, float width)
        {
            float s = FontScale, height = RosterRowHeight * s;
            var row = HudPrimitives.KitCard(RosterRowPrefix + entry.Name, content);
            row.GetComponent<Image>().raycastTarget = true;
            var size = row.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = height;

            var face = HudPrimitives.Portrait(row, CharacterPortraits.Get(entry.Character), UiTheme.Outline,
                RosterFace * s, 2f * s, false, entry.Character);
            face.name = "Face";
            Anchor(face, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(columns.Face, 0f), face.sizeDelta);

            float nameWidth = columns.Status - columns.Name - 16f * s;
            float top = string.IsNullOrEmpty(entry.Detail) ? (height - 26f * s) * .5f : 9f * s;
            var name = FixedText(row, entry.Name, 20, Paper, new Vector2(columns.Name, -top), new Vector2(nameWidth, 26f * s));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(name, 14);
            if (!string.IsNullOrEmpty(entry.Detail))
            {
                var detail = FixedText(row, entry.Detail, 15, UiTheme.Muted, new Vector2(columns.Name, -(top + 28f * s)), new Vector2(nameWidth, 20f * s));
                detail.textWrappingMode = TextWrappingModes.NoWrap;
                AutoSize(detail, 11);
            }

            var status = KitPill(row, "Status", entry.Status, UiTheme.SurfaceRaised, Paper, 15, 28f * s);
            Anchor(status, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(columns.Status, 0f), status.sizeDelta);

            var trust = NewText(row, entry.Trust, 22, entry.TrustTint);
            trust.gameObject.name = "Trust";
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) trust.font = bold;
            trust.alignment = TextAlignmentOptions.MidlineLeft;
            trust.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(trust.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(columns.Trust, 0f),
                new Vector2(columns.Profile - columns.Trust - 12f * s, 30f * s));

            var open = NewText(row, "View profile", 16, Paper);
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            if (medium != null) open.font = medium;
            open.alignment = TextAlignmentOptions.MidlineLeft;
            open.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(open.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(columns.Profile, 0f),
                new Vector2(width - columns.Profile - 44f * s, 22f * s));
            KitGlyph(row, PackArt.KitIconChevronRight, UiTheme.Muted, new Vector2(1, .5f), new Vector2(-16f * s, 0f), 18f * s);

            if (entry.Open != null) Pressable(row, entry.Open);
            rosterRows.Add((row.gameObject, entry.Name + "\n" + entry.Detail));
        }

        /// <summary>
        /// A houseguest's profile (Kit 6's preview 07): who they are on the left, and on the right
        /// your own trust in them, what you remember of them and the records you hold - then the way
        /// back to the directory. Everything on it is something the player's character could know;
        /// a record that does not exist is left out rather than drawn as a zero.
        /// </summary>
        public RectTransform HouseguestProfile(ProfileView view)
        {
            if (content == null || view == null) return null;
            float s = FontScale, width = ContentWidth(), gap = 20f * s;
            float leftWidth = Mathf.Round(width * .31f), rightWidth = width - leftWidth - gap;
            var root = new GameObject("Houseguest profile", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            root.SetParent(content, false);

            // Who they are.
            var identity = HudPrimitives.KitCard(ProfileIdentityName, root, false, 14f);
            float pad = 24f * s, inner = leftWidth - 2f * pad, y = 26f * s;
            float diameter = Mathf.Min(168f * s, inner);
            var face = HudPrimitives.Portrait(identity, CharacterPortraits.Get(view.Character), UiTheme.Outline, diameter, 3f * s, false, view.Character);
            face.name = "Face";
            Anchor(face, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0f, -y), face.sizeDelta);
            y += face.sizeDelta.y + 20f * s;
            y = PlacedCopy(identity, view.Name, 28, UiTheme.Weight.Bold, Paper, pad, y, inner) + 2f * s;
            if (!string.IsNullOrEmpty(view.Archetype))
                y = PlacedCopy(identity, view.Archetype, 19, UiTheme.Weight.SemiBold, UiTheme.Muted, pad, y, inner) + 2f * s;
            if (!string.IsNullOrEmpty(view.Facts))
                y = PlacedCopy(identity, view.Facts, 17, UiTheme.Weight.Regular, UiTheme.Muted, pad, y, inner);
            if (!string.IsNullOrEmpty(view.Hometown))
                y = PlacedCopy(identity, view.Hometown, 16, UiTheme.Weight.Regular, UiTheme.Muted, pad, y, inner);
            y += 14f * s;

            // The pills wrap: traits first, then where they stand in the season, then mood.
            float x = pad, pill = 30f * s;
            void Pill(string words, string name, Color fill, Color ink)
            {
                var chip = KitPill(identity, name, words, fill, ink, 15, pill);
                if (x > pad && x + chip.sizeDelta.x > leftWidth - pad) { x = pad; y += pill + 8f * s; }
                Anchor(chip, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), chip.sizeDelta);
                x += chip.sizeDelta.x + 8f * s;
            }
            var traitInk = Color.Lerp(Paper, UiTheme.Strategic, .45f);
            var traitFill = Color.Lerp(UiTheme.SurfaceRaised, UiTheme.Strategic, .16f);
            foreach (var trait in view.Traits) Pill(trait, "Trait", traitFill, traitInk);
            if (view.Traits.Count > 0) { x = pad; y += pill + 10f * s; }
            if (!string.IsNullOrEmpty(view.Status)) Pill(view.Status, "Status", UiTheme.SurfaceRaised, Paper);
            if (!string.IsNullOrEmpty(view.Mood)) Pill(view.Mood, "Mood", UiTheme.SurfaceRaised, Paper);
            if (x > pad) y += pill;
            if (!string.IsNullOrEmpty(view.About))
                y = PlacedCopy(identity, view.About, 16, UiTheme.Weight.Regular, Paper, pad, y + 16f * s, inner);
            float identityHeight = y + pad;

            // Your trust, and what the number is not.
            float left = leftWidth + gap, ry = 0f;
            var trust = HudPrimitives.KitCard(ProfileTrustName, root, false, 14f);
            float figureWidth = 150f * s;
            var eyebrow = FixedText(trust, "YOUR TRUST", 13, UiTheme.Muted, new Vector2(pad, -22f * s), new Vector2(figureWidth, 18f * s));
            var eyebrowFace = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (eyebrowFace != null) eyebrow.font = eyebrowFace;
            var figure = FixedText(trust, view.Trust, 40, view.TrustTint, new Vector2(pad, -44f * s), new Vector2(figureWidth, 54f * s));
            var figureFace = UiTheme.Font(UiTheme.Weight.Bold);
            if (figureFace != null) figure.font = figureFace;
            figure.gameObject.name = "Trust";
            float noteLeft = pad + figureWidth + 16f * s;
            float noteBottom = PlacedCopy(trust, view.TrustNote, 18, UiTheme.Weight.Regular, Paper, noteLeft, 20f * s, rightWidth - noteLeft - pad);
            float trustHeight = Mathf.Max(122f * s, noteBottom + 22f * s);
            Anchor(trust, new Vector2(0, 1), new Vector2(0, 1), new Vector2(left, -ry), new Vector2(rightWidth, trustHeight));
            ry += trustHeight + 24f * s;

            // What you remember of them, newest first.
            if (view.Memories.Count > 0)
            {
                ry = PlacedCopy(root, "What you remember", 21, UiTheme.Weight.SemiBold, Paper, left + 4f * s, ry, rightWidth) + 10f * s;
                var memories = HudPrimitives.KitCard("Profile memories", root, false, 14f);
                float my = 18f * s;
                foreach (var memory in view.Memories)
                    my = PlacedCopy(memories, memory, 17, UiTheme.Weight.Regular, Paper, pad, my, rightWidth - 2f * pad) + 8f * s;
                float memoriesHeight = my + 10f * s;
                Anchor(memories, new Vector2(0, 1), new Vector2(0, 1), new Vector2(left, -ry), new Vector2(rightWidth, memoriesHeight));
                ry += memoriesHeight + 24f * s;
            }

            // The records you hold that involve them.
            ry = PlacedCopy(root, "Other records", 21, UiTheme.Weight.SemiBold, Paper, left + 4f * s, ry, rightWidth) + 8f * s;
            var records = new GameObject(ProfileRecordsName, typeof(RectTransform)).GetComponent<RectTransform>();
            records.SetParent(root, false);
            float rh = 0f;
            foreach (var record in view.Records)
                rh = PlacedCopy(records, record, 17, UiTheme.Weight.Regular, UiTheme.Muted, 0f, rh, rightWidth - 8f * s) + 6f * s;
            Anchor(records, new Vector2(0, 1), new Vector2(0, 1), new Vector2(left + 4f * s, -ry), new Vector2(rightWidth - 4f * s, rh));
            ry += rh + 20f * s;

            if (view.Back != null)
            {
                var back = KitButton(root, ProfileBackCaption, PackArt.KitIconArrowBack, view.Back, out _);
                Anchor(back, new Vector2(0, 1), new Vector2(0, 1), new Vector2(left, -ry), back.sizeDelta);
                ry += back.sizeDelta.y;
            }

            float total = Mathf.Max(identityHeight, ry);
            Anchor(identity, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(leftWidth, total));
            var size = root.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = total + 8f * s;
            return root;
        }
    }
}
