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
            DecisionText(root, "KNOWN HOUSE EVENTS", 17, Accent);
            DecisionText(root, DecisionContext.KnownEventSummary(state), 16, UiTheme.Muted);
            DecisionText(root, "Counts of events you know; not anyone's private feelings.", 15, UiTheme.Muted);
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
