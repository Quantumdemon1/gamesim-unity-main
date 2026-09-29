using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The finalists as cards (ENDGAME-PLAN F2, mockups 30-32): one column per finalist the player
    /// is weighing, each a card of who they are and what the player knows about them - every line
    /// with how sure the player can be of it - and, on the final Head of Household's decision, what
    /// taking them means, the choice's headline and the control that makes it.
    ///
    /// <para>The control keeps its pinned caption, <c>Evict {name}</c>; the headline over it says
    /// what pressing it means for the column it sits in, "Take {name} to the Final 2" (the plan's
    /// fourth principle: decorate a pinned caption, never rename it). The cards themselves are not
    /// controls, so the only pressable things in a column are the one the caption names.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The parts a test finds by name.</summary>
        public const string FinalistColumnsName = "Finalist columns", FinalistCardPrefix = "Finalist · ",
            FinalistHeadlineName = "Choice headline", FinalistWarningName = "Choice warning", CertaintyLegendName = "Certainty legend", FactRowName = "Fact";

        /// <summary>The legend under a finalist screen's head, in the words the cards use.</summary>
        public const string CertaintyLegendWords =
            "Confirmed: you saw it or were part of it. Suspected: you heard it. Unknown: nothing to go on.";

        /// <summary>One finalist's column: who, what is known, and - on the decision - what taking them means and the control.</summary>
        public struct FinalistColumn
        {
            public ContestantState Actor;
            public FinalistRead.Finalist Read;
            public string BulletsHeading;
            public IList<string> Bullets;
            public string Headline, Caption;
            /// <summary>A line in the warning colour between the headline and the control: what cannot be undone.</summary>
            public string Warning;
            public Action Choose;
        }

        /// <summary>A certainty word's colour: confirmed in the allied green, suspected in the amber, unknown muted.</summary>
        public static Color CertaintyTint(string certainty) =>
            certainty == FinalistRead.Confirmed ? UiTheme.Allied : certainty == FinalistRead.Suspected ? UiTheme.Joke : UiTheme.Muted;

        /// <summary>The legend: what Confirmed, Suspected and Unknown mean on the cards below it.</summary>
        public void CertaintyLegend()
        {
            var text = FlowText(CertaintyLegendWords, 13, UiTheme.Muted);
            text.name = CertaintyLegendName;
        }

        /// <summary>
        /// The columns, side by side at the resting text on a wide stage and one under another at
        /// the larger text or on a narrow one, as the paired choices go.
        /// </summary>
        public void FinalistColumns(IList<FinalistColumn> finalists)
        {
            if (content == null || finalists == null || finalists.Count == 0) return;
            float s = FontScale, width = ContentWidth(), gap = 16f * s;
            bool beside = finalists.Count > 1 && width >= 720f && FontScale <= 1.05f;
            var row = new GameObject(FinalistColumnsName, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            HorizontalOrVerticalLayoutGroup layout = beside
                ? (HorizontalOrVerticalLayoutGroup)row.gameObject.AddComponent<HorizontalLayoutGroup>()
                : row.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = gap; layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            float columnWidth = beside ? (width - gap * (finalists.Count - 1)) / finalists.Count : width;
            foreach (var finalist in finalists) FinalistColumnIn(row, finalist, columnWidth);
        }

        private void FinalistColumnIn(RectTransform row, FinalistColumn finalist, float width)
        {
            float s = FontScale;
            var column = new GameObject("Finalist column · " + finalist.Actor.name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement))
                .GetComponent<RectTransform>();
            column.SetParent(row, false);
            var layout = column.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f * s;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var element = column.GetComponent<LayoutElement>();
            element.minWidth = 0f; element.preferredWidth = width; element.flexibleWidth = 1f;

            PushContent(column, width);
            FinalistCard(finalist, width);
            if (!string.IsNullOrEmpty(finalist.Headline))
            {
                var headline = FlowText(finalist.Headline, 17, UiTheme.Heading);
                headline.name = FinalistHeadlineName;
                headline.alignment = TextAlignmentOptions.Center;
                var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
                if (semibold != null) headline.font = semibold;
            }
            // The warning sits on the control it is about, so it is on screen whenever the control is.
            if (!string.IsNullOrEmpty(finalist.Warning))
            {
                var warning = FlowText(finalist.Warning, 13, UiTheme.Warning);
                warning.name = FinalistWarningName;
                warning.alignment = TextAlignmentOptions.Center;
            }
            // The one control in the column, under the pinned caption it has always had.
            if (!string.IsNullOrEmpty(finalist.Caption) && finalist.Choose != null) Action(finalist.Caption, finalist.Choose);
            PopContent();
        }

        private void FinalistCard(FinalistColumn finalist, float width)
        {
            float s = FontScale;
            var actor = finalist.Actor;
            var read = finalist.Read;
            var card = HudPrimitives.KitCard(FinalistCardPrefix + actor.name, content, false, 12f);
            foreach (Transform decoration in card) decoration.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            int pad = Mathf.RoundToInt(16f * s);
            layout.padding = new RectOffset(pad, pad, Mathf.RoundToInt(14f * s), pad);
            layout.spacing = 4f * s;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            float inner = width - 2f * pad;
            PushContent(card, inner);

            // Who they are: the photo, the name, the card line, the traits, and their own line.
            var photoRow = new GameObject("Photo row", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            photoRow.SetParent(content, false);
            var photoSize = new Vector2(104f, 128f) * s;
            photoRow.GetComponent<LayoutElement>().minHeight = photoSize.y;
            var photo = HudPrimitives.RectPortrait(photoRow, "Photo", CharacterPortraits.Get(actor), actor, photoSize, 10);
            photo.anchorMin = photo.anchorMax = new Vector2(.5f, 1f);
            photo.pivot = new Vector2(.5f, 1f); photo.anchoredPosition = Vector2.zero;

            var name = FlowText(actor.name, 22, Paper);
            name.alignment = TextAlignmentOptions.Center;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            string cardLine = EpisodeDirector.CardLine(actor);
            if (!string.IsNullOrEmpty(cardLine)) FlowText(cardLine, 14, UiTheme.Muted).alignment = TextAlignmentOptions.Center;
            if (actor.traits != null && actor.traits.Count > 0)
                FlowText(string.Join(" · ", actor.traits.Select(Localisation.Text)), 13, UiTheme.Joke).alignment = TextAlignmentOptions.Center;
            if (!string.IsNullOrEmpty(read?.line))
            {
                var quote = FlowText("“" + read.line + "”", 15, Paper);
                quote.alignment = TextAlignmentOptions.Center;
                quote.fontStyle = FontStyles.Italic;
            }

            // What the player knows, a line each, every one with how sure it is.
            if (read != null)
            {
                Gap(6f);
                FinalistEyebrow("WHAT YOU KNOW");
                foreach (var fact in read.Facts) FactRow(fact, inner);
            }

            if (finalist.Bullets != null && finalist.Bullets.Count > 0)
            {
                Gap(6f);
                FinalistEyebrow(finalist.BulletsHeading ?? "IF YOU TAKE " + actor.name.Split(' ')[0].ToUpperInvariant());
                foreach (var bullet in finalist.Bullets) FlowText("• " + bullet, 15, Paper);
            }
            PopContent();
        }

        private void FinalistEyebrow(string words)
        {
            var eyebrow = FlowText(words, 12, Accent);
            eyebrow.characterSpacing = 6f;
        }

        private void Gap(float height)
        {
            var gap = new GameObject("Gap", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            gap.SetParent(content, false);
            gap.GetComponent<LayoutElement>().minHeight = height * FontScale;
        }

        /// <summary>A fact: its label and how sure on one line, what is known under them.</summary>
        private void FactRow(FinalistRead.Fact fact, float width)
        {
            if (fact == null) return;
            float s = FontScale;
            var row = new GameObject(FactRowName + " · " + fact.label, typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var stack = row.GetComponent<VerticalLayoutGroup>();
            stack.spacing = 0f; stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = true; stack.childForceExpandHeight = false;

            var head = new GameObject("Fact head", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            head.SetParent(row, false);
            var line = head.GetComponent<HorizontalLayoutGroup>();
            line.spacing = 8f * s; line.childControlWidth = line.childControlHeight = true;
            line.childForceExpandWidth = false; line.childForceExpandHeight = false;
            var label = NewText(head, fact.label.ToUpperInvariant(), 12, UiTheme.Muted);
            label.characterSpacing = 3f;
            var labelElement = label.gameObject.AddComponent<LayoutElement>();
            labelElement.flexibleWidth = 1f; labelElement.minWidth = 0f;
            var sure = NewText(head, fact.certainty, 12, CertaintyTint(fact.certainty));
            sure.name = "Certainty";
            sure.alignment = TextAlignmentOptions.Right;
            sure.gameObject.AddComponent<LayoutElement>().minWidth = 72f * s;

            var value = NewText(row, fact.value, 15, Paper);
            value.name = "Fact value";
            value.gameObject.AddComponent<LayoutElement>().minHeight = 15f * s + 6f;
        }
    }
}
