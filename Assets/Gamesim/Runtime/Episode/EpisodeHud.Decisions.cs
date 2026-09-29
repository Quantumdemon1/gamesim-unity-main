using System;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeHud
    {
        public const string ShowCandidateContextCaption = "Compare selected candidates";
        public const string HideCandidateContextCaption = "Hide candidate comparison";
        public const string CandidateContextName = "Selected candidate comparison";
        public const string HouseEventContextName = "Known house event context";

        public void KnownHouseEventContext(EpisodeState state, HouseEventState item)
        {
            var root = DecisionColumn(HouseEventContextName, content, true);
            // "KNOWN HOUSE EVENTS" was reported, in those words, as a heading nobody could act
            // on: it named the data structure behind the card rather than the question the card
            // answers. What the card actually holds is a tally of what this character has been
            // present for this week, which is the only evidence they are allowed to decide on.
            DecisionText(root, "WHAT YOU HAVE SEEN THIS WEEK", 17, Accent);
            DecisionText(root, DecisionContext.KnownEventSummary(state), 16, UiTheme.Muted);
            DecisionText(root, "Only what you witnessed. Not anyone's private feelings.", 15, UiTheme.Muted);
            var participants = DecisionContext.Participants(state, item);
            if (participants.Length == 0) return;
            var strip = DecisionColumns("Involved houseguests", root);
            foreach (var participant in participants.Take(4))
                DecisionIdentity(strip, participant.Character, 108f, 15);
            if (participants.Length > 4)
                DecisionText(root, "Also involved: " + string.Join(", ", participants.Skip(4).Select(p => p.Character.name)), 16, Paper);
        }

        /// <summary>Comparison is optional and sits after commit, so expanding it never displaces the decision controls.</summary>
        public void ChooseNominationPair(EpisodeState state, Option[] options, Action<string, string> commit, string commitCaption)
        {
            string first = null, second = null;
            Action refresh = null;
            ChoosePair(options, commit, commitCaption, (a, b) => { first = a; second = b; refresh?.Invoke(); });
            bool expanded = false;
            Button toggle = null;
            RectTransform comparison = null;
            toggle = Action(ShowCandidateContextCaption, () =>
            {
                expanded = !expanded;
                toggle.name = expanded ? HideCandidateContextCaption : ShowCandidateContextCaption;
                toggle.GetComponentInChildren<TMP_Text>().text = toggle.name;
                refresh();
            });
            comparison = DecisionColumn(CandidateContextName, content);
            refresh = () =>
            {
                if (comparison == null) return; // Ignore a detached control from an older HUD revision.
                foreach (Transform child in comparison)
                { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                comparison.gameObject.SetActive(expanded);
                if (!expanded) return;
                DecisionText(comparison, "PUBLIC RECORD · YOUR PERSPECTIVE", 17, Accent);
                if (first == null && second == null)
                { DecisionText(comparison, "Choose up to two candidates above to compare their records.", 17, Paper); return; }
                var columns = DecisionColumns("Candidate records", comparison);
                foreach (string id in new[] { first, second }.Where(id => id != null))
                {
                    var candidate = DecisionContext.ForCandidate(state, id);
                    if (candidate == null) continue;
                    var card = DecisionColumn("Candidate context " + id, columns, true);
                    var width = card.gameObject.AddComponent<LayoutElement>();
                    width.minWidth = width.preferredWidth = 0f; width.flexibleWidth = 1f;
                    DecisionIdentity(card, candidate.Character, 78f, 18);
                    DecisionText(card, candidate.Record, 17, Paper);
                    DecisionText(card, candidate.Relationship, 17, Paper);
                    DecisionText(card, candidate.Promises, 16, UiTheme.Muted);
                }
            };
            refresh();
        }

        /// <summary>The event's choices, so a test can find them the way it finds a named panel.</summary>
        public const string EventChoicesName = "House event choices";

        /// <summary>
        /// A house event's header (mockup-04): what kind of moment this is, what happened, and the
        /// question - in the voice of the web build's event dialog rather than a shouted heading.
        /// </summary>
        public void HouseEventHeader(string title, string narrative, string kind = "HOUSE EVENT")
        {
            SetActivityLayout(ActivityLayout.HouseEvent);
            var eyebrow = DecisionText(content, kind, 12, UiTheme.Joke);
            eyebrow.characterSpacing = 8f;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            var heading = DecisionText(content, title, 22, Paper);
            if (semibold != null) heading.font = semibold;
            var story = DecisionText(content, narrative, 16, UiTheme.Muted);
            story.fontStyle = FontStyles.Italic;
        }

        /// <summary>
        /// The choices as tiles, two to a row (mockup-04): a glyph for the kind of response, the
        /// caption the control has always had, what it means in a line under it, and how far it
        /// could rebound as a coloured word in the corner - the colour and the word together,
        /// because a warning carried by colour alone is one some players never receive.
        /// </summary>
        public void EventChoices(System.Collections.Generic.IList<(string Caption, string Description, string Risk, Action Choose)> choices)
        {
            DecisionText(content, "How should you respond?", 16, UiTheme.Muted).alignment = TextAlignmentOptions.Center;
            // The same tiles the free-time screen draws its moves as (EpisodeHud.FreeTime.cs), the
            // risk word in the corner in the web's colour and the glyph read from the caption.
            var tiles = new System.Collections.Generic.List<MoveTile>();
            foreach (var choice in choices)
                tiles.Add(new MoveTile
                {
                    Caption = choice.Caption, Description = choice.Description, Corner = choice.Risk,
                    CornerTint = RiskTint(choice.Risk), Glyph = ChoiceGlyph(choice.Caption), Choose = choice.Choose,
                });
            ChoiceTiles(EventChoicesName, tiles);
        }

        /// <summary>The glyph a response is drawn with, from the words of its caption.</summary>
        private static string ChoiceGlyph(string caption)
        {
            string words = (caption ?? string.Empty).ToLowerInvariant();
            if (words.Contains("join") || words.Contains("interven") || words.Contains("side") || words.Contains("back ")) return "people";
            if (words.Contains("watch") || words.Contains("listen") || words.Contains("observe")) return "eye";
            if (words.Contains("calm") || words.Contains("cool") || words.Contains("apolog") || words.Contains("defuse") || words.Contains("peace")) return "handshake";
            if (words.Contains("leave") || words.Contains("walk") || words.Contains("away") || words.Contains("ignore")) return "exit";
            if (words.Contains("confront") || words.Contains("call") || words.Contains("fight") || words.Contains("stand")) return "target";
            if (words.Contains("ask") || words.Contains("say") || words.Contains("talk") || words.Contains("tell") || words.Contains("deny") || words.Contains("own")) return "chat";
            return "journal";
        }

        /// <summary>A risk word's colour: the web's green, amber and red.</summary>
        private static Color RiskTint(string risk)
        {
            string word = (risk ?? string.Empty).ToLowerInvariant();
            if (word.Contains("high")) return UiTheme.Conflict;
            if (word.Contains("some") || word.Contains("medium")) return UiTheme.Joke;
            return UiTheme.Allied;
        }

        /// <summary>The ballot's row, so a test can find it the way it finds a named panel.</summary>
        public const string BallotRowName = "Ballot row";
        /// <summary>The juror's "What matters to you" card (ENDGAME-PLAN F6).</summary>
        public const string JurorMattersName = "What matters to you";
        private const float BallotCardWidth = 196f;
        private const float BallotCardHeight = 250f;

        /// <summary>
        /// The eviction ballot as the mockup draws it (mockup-08): one portrait card per nominee,
        /// side by side, each with the houseguest's name, two of their traits and a ring to mark the
        /// one you choose. Each card IS the "Vote to evict" control and carries that caption as its
        /// visible foot line, so the caption a test or a screen reader finds the control by is
        /// still the words on it. <paramref name="chosenId"/> marks the card already chosen, while
        /// the choice is waiting to be confirmed.
        /// </summary>
        public void BallotCards(EpisodeState state, System.Collections.Generic.IList<string> nominees, string chosenId,
            Func<string, string> caption, Action<string> press,
            string heading = "EVICTION VOTE", string line = "Choose one houseguest to evict from the house.", string glyph = "gavel")
        {
            string headingWords = heading;
            var headingRow = new GameObject("Ballot heading", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            headingRow.SetParent(content, false);
            headingRow.GetComponent<LayoutElement>().minHeight = 40f * FontScale;
            // Centred, the gavel and the title together, over a centred line, as mockup-08 heads
            // its ballot.
            float markSide = 30f * FontScale;
            var title = FixedText(headingRow, headingWords, 26, Paper, new Vector2(markSide + 12f * FontScale, -2f),
                new Vector2(ContentWidth() - 2f * (markSide + 12f * FontScale), 36f * FontScale));
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) title.font = bold;
            title.characterSpacing = 2f;
            AutoSize(title, 16);
            title.alignment = TextAlignmentOptions.Center;
            float words = Mathf.Min(title.rectTransform.sizeDelta.x, Mathf.Ceil(title.GetPreferredValues(title.text).x));
            HudPrimitives.Glyph("Ballot mark", headingRow, glyph, Paper,
                new Vector2((ContentWidth() - words) * .5f - markSide - 10f * FontScale, -4f), markSide);
            DecisionText(content, line, 16, Paper).alignment = TextAlignmentOptions.Center;

            var row = new GameObject(BallotRowName, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f * FontScale;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            // Large on a large screen - the two people the vote is between are what it shows - and
            // never wider than the column holds them side by side.
            float fitsWidth = (ContentWidth() - 24f * FontScale) / (2f * BallotCardWidth);
            float fitsHeight = float.MaxValue;
            if (modal != null && modalScroll != null)
            {
                var scroll = (RectTransform)modalScroll.transform;
                float viewport = modal.sizeDelta.y + scroll.offsetMax.y - scroll.offsetMin.y;
                // The heading, the line under it, the spacing, and a Confirm row under the cards.
                fitsHeight = (viewport - (6f + 40f + 30f + 3f * 12f + 57f) * FontScale) / BallotCardHeight;
            }
            float scale = Mathf.Max(Mathf.Min(FontScale, fitsWidth), Mathf.Min(1.4f * FontScale, Mathf.Min(fitsWidth, fitsHeight)));
            row.GetComponent<LayoutElement>().minHeight = BallotCardHeight * scale + 4f;
            foreach (var id in nominees)
            {
                string captured = id;
                BallotCard(row, state.Find(id), caption(id), id == chosenId, scale, () => press(captured));
            }
        }

        private void BallotCard(RectTransform row, ContestantState actor, string caption, bool chosen, float scale, Action press)
        {
            if (actor == null) return;
            var rect = Chrome(caption, row, chosen ? UiTheme.Emphasis.Active : UiTheme.Emphasis.Interactive);
            rect.sizeDelta = new Vector2(BallotCardWidth * scale, BallotCardHeight * scale);
            HudEmphasis.Promote(rect, chosen ? UiTheme.Emphasis.Active : UiTheme.Emphasis.Interactive);
            var button = Pressable(rect, press);
            var colours = button.colors;
            colours.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colours.selectedColor = colours.highlightedColor;
            button.colors = colours;
            if (chosen) UiTheme.AddGlow(rect, UiTheme.GlassRadius);

            float width = BallotCardWidth * scale;
            var photo = HudPrimitives.RectPortrait(rect, "Photo", Portrait(actor.id), actor, new Vector2(width - 10f * scale, 150f * scale), 8);
            photo.anchorMin = photo.anchorMax = new Vector2(.5f, 1f);
            photo.pivot = new Vector2(.5f, 1f);
            photo.anchoredPosition = new Vector2(0f, -5f * scale);

            // The radio: the pack's selection ring, filled with its check once this is the choice.
            var ring = UiTheme.Pack(PackArt.SelectionRing);
            if (ring != null)
            {
                var mark = new GameObject("Choice ring", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                mark.rectTransform.SetParent(rect, false);
                Anchor(mark.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10f * scale, -10f * scale), new Vector2(30f * scale, 30f * scale));
                mark.sprite = ring; mark.preserveAspect = true; mark.raycastTarget = false;
            }
            if (chosen)
            {
                var check = UiTheme.Pack(PackArt.BadgeSelected);
                var tick = new GameObject("Chosen", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                tick.rectTransform.SetParent(rect, false);
                Anchor(tick.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12f * scale, -12f * scale), new Vector2(26f * scale, 26f * scale));
                tick.sprite = check != null ? check : UiTheme.Circle();
                tick.color = check != null ? Color.white : UiTheme.Accent;
                tick.preserveAspect = true; tick.raycastTarget = false;
            }

            var name = FixedText(rect, actor.name, 18, Paper, new Vector2(10f * scale, -161f * scale), new Vector2(width - 20f * scale, 24f * scale));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            AutoSize(name, 12);

            // Two traits as the cast screen shows them - the card is a person, not a number.
            float x = 10f * scale;
            foreach (var trait in (actor.traits ?? new System.Collections.Generic.List<string>()).Take(2))
            {
                string word = Localisation.Text(trait);
                float chipWidth = Mathf.Min(width * .5f - 12f * scale, (word.Length * 7f + 20f) * scale);
                var chip = HudPrimitives.Chip("Trait", rect, word, CastSelect.TraitTint(trait), chipWidth, 20f * scale);
                Anchor(chip, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -190f * scale), chip.sizeDelta);
                chip.GetComponent<Image>().raycastTarget = false;
                var chipWord = chip.GetComponentInChildren<TMP_Text>();
                if (chipWord != null) { chipWord.enableAutoSizing = true; chipWord.fontSizeMax = chipWord.fontSize; chipWord.fontSizeMin = Mathf.Min(8f, chipWord.fontSize); }
                x += chipWidth + 6f * scale;
            }

            // The control's caption, on the card: what pressing it does, in the words it is known by.
            var foot = FixedText(rect, caption, 12, UiTheme.Muted, new Vector2(10f * scale, -220f * scale), new Vector2(width - 20f * scale, 22f * scale));
            AutoSize(foot, 9);
        }

        private RectTransform DecisionColumn(string name, Transform parent, bool card = false)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var layout = rect.GetComponent<VerticalLayoutGroup>();
            if (card)
            {
                UiTheme.Style(rect.gameObject.AddComponent<Image>(), Surface, UiTheme.ControlRadius);
                rect.GetComponent<Image>().raycastTarget = false;
                layout.padding = new RectOffset(10,10,10,10);
            }
            layout.spacing = 6f * FontScale; layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            return rect;
        }

        private RectTransform DecisionColumns(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var layout = rect.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f * FontScale; layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            return rect;
        }

        private void DecisionIdentity(Transform parent, ContestantState actor, float height, int size)
        {
            var row = DecisionColumn("Decision identity " + actor.id, parent);
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.minWidth = 0f; element.preferredWidth = 0f; element.flexibleWidth = 1f;
            element.minHeight = height * FontScale;
            float side = 42f * FontScale;
            var portraitRow = new GameObject("Portrait row",typeof(RectTransform),typeof(LayoutElement)).GetComponent<RectTransform>();
            portraitRow.SetParent(row,false);
            portraitRow.GetComponent<LayoutElement>().minHeight = side;
            var portrait = HudPrimitives.Portrait(portraitRow, null, UiTheme.Outline, side, 2f * FontScale, false, actor);
            portrait.name = "Decision portrait " + actor.id;
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, 1f);
            portrait.pivot = new Vector2(.5f, 1f); portrait.anchoredPosition = Vector2.zero;
            var label = DecisionText(row, actor.name, size, Paper);
            label.name = "Decision name " + actor.id; label.alignment = TextAlignmentOptions.Top;
        }

        private TMP_Text DecisionText(Transform parent, string value, int size, Color color)
        {
            var text = NewText(parent, value, size, color);
            text.gameObject.AddComponent<LayoutElement>().minHeight = size * FontScale + 8f;
            return text;
        }
    }
}
