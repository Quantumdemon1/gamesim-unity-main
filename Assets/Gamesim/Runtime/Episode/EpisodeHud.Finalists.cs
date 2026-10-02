using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The finalists as cards (ENDGAME-PLAN F2, mockups 30-32; MOCKUP-PASS M8, mockups 50 and 59):
    /// one column per finalist the player is weighing, each a card of who they are and what the
    /// player knows about them - every line with how sure the player can be of it - and what
    /// taking them would mean: the comparison in the window at three, and the juror's cases.
    ///
    /// <para>The final Head of Household's decision is a page of its own now (UI-UX-PASS-PLAN Q0,
    /// EpisodeHud.FinalChoice.cs), which keeps this file's pieces of a choice: the gold edge a
    /// choosing card lights, the ring on its control, the lights that answer to one another, and
    /// the VS between the two.</para>
    ///
    /// <para>The card is the mockups' now: the photo beside who they are, their traits as chips
    /// and their own line in a quote box; the competition record as counts, where the player
    /// stands with them as the cast strip's bar, and the jury read as three counts with the
    /// jurors named under each. The juror's variant (<see cref="FinalistColumn.JurorCase"/>) is the
    /// finale's card: a bleed photo, the name in capitals, the first line of their final speech,
    /// and the three numbers a juror weighs.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The parts a test finds by name.</summary>
        public const string FinalistColumnsName = "Finalist columns", FinalistCardPrefix = "Finalist · ",
            FinalistWarningName = "Choice warning", CertaintyLegendName = "Certainty legend", FactRowName = "Fact";

        /// <summary>The line under a choice's warning, the VS between two columns, and a choosing card's gold edge.</summary>
        public const string FinalistConsequenceName = "Choice consequence", VersusName = "Finalist versus", ChosenEdgeName = "Chosen edge";

        /// <summary>The legend under a finalist screen's head, in the words the cards use.</summary>
        public const string CertaintyLegendWords =
            "Confirmed: you saw it or were part of it. Suspected: you heard it. Unknown: nothing to go on.";

        /// <summary>The gap two columns stand apart by side by side: room for the VS between them.</summary>
        private const float VersusGap = 44f;

        /// <summary>
        /// The finale card's frame (Pack 3's <c>finalist_card_9slice</c>): the corner it is drawn
        /// at, in canvas units, and how deep its baked glow is in the art's own pixels, so the
        /// visible edge lands on the card's rect rather than a glow's width inside it.
        /// </summary>
        private const float FinaleFrameBorder = 40f, FinaleFrameGlow = 40f;

        /// <summary>One finalist's column: who, what is known, and what taking them would mean.</summary>
        public struct FinalistColumn
        {
            public ContestantState Actor;
            public FinalistRead.Finalist Read;
            public string BulletsHeading;
            public IList<string> Bullets;
            /// <summary>
            /// The juror's variant (mockup 50): the case the player on the jury reads, framed in the
            /// finale's card with a bleed photo, the name in capitals, the three numbers a juror
            /// weighs and the player's own standing and agreement with them on one line.
            /// </summary>
            public bool JurorCase;
            /// <summary>The quote on the card, and where it was said; the card's own intro line when null (the comparison's cards).</summary>
            public string Quote, QuoteSource;
        }

        /// <summary>A certainty word's colour: confirmed in the allied green, suspected in the amber, unknown muted.</summary>
        public static Color CertaintyTint(string certainty) =>
            certainty == FinalistRead.Confirmed ? UiTheme.Allied : certainty == FinalistRead.Suspected ? UiTheme.Joke : UiTheme.Muted;

        /// <summary>
        /// The colour the relationship web draws the player's standing in, by the word the card says
        /// it with (<see cref="FinalistRead.StandingWord"/>, held to the web's words by a test).
        /// </summary>
        private static Color StandingTint(string word)
        {
            switch (word)
            {
                case "Allied": return RelationshipWeb.StandingColour(RelationshipWeb.Kind.Alliance);
                case "Friendly": return RelationshipWeb.StandingColour(RelationshipWeb.Kind.Friendship);
                case "Wary": return RelationshipWeb.StandingColour(RelationshipWeb.Kind.Distrust);
                case "Hostile": return RelationshipWeb.StandingColour(RelationshipWeb.Kind.Rivalry);
                default: return UiTheme.Muted;
            }
        }

        /// <summary>The record's grade in a colour: strong in the power's gold, moderate in the accent, light muted.</summary>
        private static Color GradeTint(string grade) =>
            grade == FinalistRead.Strong ? UiTheme.Gold : grade == FinalistRead.Moderate ? UiTheme.Accent : UiTheme.Muted;

        /// <summary>The legend: what Confirmed, Suspected and Unknown mean on the cards, as one footnote line.</summary>
        public void CertaintyLegend()
        {
            var text = FlowText(CertaintyLegendWords, 12, UiTheme.Muted);
            text.name = CertaintyLegendName;
            text.alignment = TextAlignmentOptions.Center;
        }

        /// <summary>
        /// The columns, side by side at the resting text on a wide stage and one under another at
        /// the larger text or on a narrow one, as the paired choices go. Side by side, the columns
        /// are one height and each card takes up the difference, so the two cards end level.
        /// </summary>
        public void FinalistColumns(IList<FinalistColumn> finalists)
        {
            if (content == null || finalists == null || finalists.Count == 0) return;
            float s = FontScale, width = ContentWidth();
            bool beside = finalists.Count > 1 && width >= 720f && FontScale <= 1.05f;
            float gap = (beside ? VersusGap : 16f) * s;
            var row = new GameObject(FinalistColumnsName, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            HorizontalOrVerticalLayoutGroup layout = beside
                ? (HorizontalOrVerticalLayoutGroup)row.gameObject.AddComponent<HorizontalLayoutGroup>()
                : row.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = gap; layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = beside;
            float columnWidth = beside ? (width - gap * (finalists.Count - 1)) / finalists.Count : width;
            for (int i = 0; i < finalists.Count; i++) FinalistColumnIn(row, finalists[i], columnWidth, beside, i);
            if (!beside) return;
            // Two people and the case between them: the VS stands in the gap level with their faces,
            // a juror's photo bleeding to the card's top and a comparison's under its padding.
            if (finalists.Count == 2)
                Versus(row, gap, finalists[0].JurorCase ? JurorPhoto.y * s * .5f : 14f * s + ChoicePhoto.y * s * .5f);
        }

        /// <summary>The photo each card's header carries: beside who they are on a comparison's card, bleeding to the corner on a juror's.</summary>
        private static readonly Vector2 ChoicePhoto = new Vector2(104f, 128f), JurorPhoto = new Vector2(150f, 200f);

        private void FinalistColumnIn(RectTransform row, FinalistColumn finalist, float width, bool level, int index)
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
            var card = FinalistCard(finalist, width, index);
            // The card takes whatever height the row gives the column past its own, so the two end level.
            if (level) card.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            PopContent();
        }

        /// <summary>
        /// The ring inside a finalist's control (mockup 59), filled while the control has the
        /// pointer, or the keyboard the player moved there, and the gold edge of the card it is
        /// about lit with it: the choice the player is about to make, shown on the person it is
        /// about. Decoration on the column's one control, never a second one; a single press still
        /// commits.
        /// </summary>
        private void SelectionMark(Button button, GameObject edge)
        {
            if (button == null) return;
            float s = FontScale, side = 24f * s;
            var rect = (RectTransform)button.transform;
            GameObject fill = null;
            var ring = UiTheme.Pack(PackArt.SelectionRing);
            if (ring != null)
            {
                var mark = new GameObject("Selection ring", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                mark.rectTransform.SetParent(rect, false);
                Anchor(mark.rectTransform, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(16f, 0f), new Vector2(side, side));
                mark.sprite = ring; mark.preserveAspect = true; mark.raycastTarget = false;
                var dot = HudPrimitives.Disc("Selection fill", mark.rectTransform, UiTheme.Gold);
                dot.anchorMin = dot.anchorMax = new Vector2(.5f, .5f); dot.pivot = new Vector2(.5f, .5f);
                dot.anchoredPosition = Vector2.zero; dot.sizeDelta = new Vector2(side * .46f, side * .46f);
                fill = dot.gameObject;
                // The caption starts past the ring; its words and its right-hand end are as they were.
                var caption = button.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.transform.parent == rect);
                if (caption != null)
                    caption.rectTransform.offsetMin = new Vector2(16f + side + 10f * s, caption.rectTransform.offsetMin.y);
            }
            if (fill == null && edge == null) return;
            var light = button.gameObject.AddComponent<ChoiceLight>();
            light.Hud = this; light.Edge = edge; light.Fill = fill;
            light.Apply();
        }

        /// <summary>
        /// Lights a finalist's column while its control is the one a press would take: the card's
        /// gold edge and the filled ring on the control. It is not a control - the Button beside it
        /// is the column's only one - and it listens to the same select and pointer events the
        /// Button does, so the column says which choice a press would make before it is made.
        ///
        /// <para>The columns are one group, and one card in it is lit at a time. The keyboard's column
        /// wins whenever the player has put the keyboard on one of the group's controls, and keeps the
        /// light while the pointer passes over the other: Enter presses the selected control, so the
        /// lit card is always the one Enter would take (UI-UX-PASS-PLAN Q0's review; the pointer's
        /// column used to win, and Enter could press the unlit control). With neither control holding
        /// the keyboard, the pointer's lights its column, a preview of what a click there would do. Lit
        /// separately, the two could light both cards at once.</para>
        ///
        /// <para>The keyboard lights a column only when the player put it there. The HUD never puts
        /// it on one of these controls of its own accord (<see cref="MayFocusOnItsOwn"/>: the page
        /// opens on Close), and a selection the HUD makes on its own account (<see cref="RestoreFocus"/>)
        /// lights nothing unless it hands back the control the player had lit before a rebuild - a
        /// card lit before the player has done anything would read as the game's pick, on a screen
        /// whose head says nothing on it is a prediction. The Button's own tint still shows where the
        /// keyboard is.</para>
        ///
        /// <para>No OnDisable reset: the HUD deactivates a whole panel before it destroys it, and a
        /// SetActive on the edge from inside that deactivation is one Unity refuses. A rebuilt panel
        /// is lit again by the selection the HUD restores to it and the pointer still on it.</para>
        /// </summary>
        [DisallowMultipleComponent]
        private sealed class ChoiceLight : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
        {
            public EpisodeHud Hud;
            public GameObject Edge, Fill;
            /// <summary>Every column's light on the screen, this one among them.</summary>
            public ChoiceLight[] Group;
            private bool focused, hovered;

            public void OnSelect(BaseEventData data)
            {
                bool restored = Hud != null && Hud.restoringFocus;
                if (!restored && Hud != null) Hud.litChoice = gameObject.name;
                focused = !restored || gameObject.name == Hud.litChoice;
                Refresh();
            }

            public void OnDeselect(BaseEventData data) { focused = false; Refresh(); }
            public void OnPointerEnter(PointerEventData data) { hovered = true; Refresh(); }
            public void OnPointerExit(PointerEventData data) { hovered = false; Refresh(); }

            /// <summary>Lights the whole group again: one change can move the light from one column to the other.</summary>
            private void Refresh()
            {
                if (Group == null) { Apply(); return; }
                foreach (var light in Group)
                    if (light != null) light.Apply();
            }

            public void Apply()
            {
                // The player's keyboard on any of the group's controls holds the light; only without
                // it does the pointer's control light its column.
                bool chosen = focused;
                if (Group != null)
                    foreach (var light in Group) chosen |= light != null && light.focused;
                bool lit = chosen ? focused : hovered;
                if (Edge != null) Edge.SetActive(lit);
                if (Fill != null) Fill.SetActive(lit);
            }
        }

        /// <summary>
        /// The VS between two columns (mockups 50 and 59): a thin glow down the middle of the gap and
        /// a small gold "VS" on a disc level with the faces. Laid over the row, never in it, so the
        /// columns keep their equal widths.
        /// </summary>
        private void Versus(RectTransform row, float gap, float centre)
        {
            float s = FontScale;
            var versus = new GameObject(VersusName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            versus.SetParent(row, false);
            versus.GetComponent<LayoutElement>().ignoreLayout = true;
            versus.anchorMin = new Vector2(.5f, 0f); versus.anchorMax = new Vector2(.5f, 1f);
            versus.pivot = new Vector2(.5f, .5f);
            versus.anchoredPosition = Vector2.zero; versus.sizeDelta = new Vector2(gap, 0f);
            Stroke(versus, "Versus glow", 8f * s, new Color(UiTheme.Glow.r, UiTheme.Glow.g, UiTheme.Glow.b, .12f));
            Stroke(versus, "Versus line", 2f * s, new Color(UiTheme.Glow.r, UiTheme.Glow.g, UiTheme.Glow.b, .55f));
            float side = 38f * s;
            var rim = HudPrimitives.Disc("Versus rim", versus, new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .8f));
            rim.anchorMin = rim.anchorMax = new Vector2(.5f, 1f); rim.pivot = new Vector2(.5f, .5f);
            rim.anchoredPosition = new Vector2(0f, -centre); rim.sizeDelta = new Vector2(side + 3f * s, side + 3f * s);
            var badge = HudPrimitives.Disc("Versus badge", versus, UiTheme.Surface);
            badge.anchorMin = badge.anchorMax = new Vector2(.5f, 1f); badge.pivot = new Vector2(.5f, .5f);
            badge.anchoredPosition = new Vector2(0f, -centre); badge.sizeDelta = new Vector2(side, side);
            var word = FixedText(badge, "VS", 14, UiTheme.Gold, Vector2.zero, new Vector2(side, side));
            word.rectTransform.anchorMin = Vector2.zero; word.rectTransform.anchorMax = Vector2.one;
            word.rectTransform.offsetMin = Vector2.zero; word.rectTransform.offsetMax = Vector2.zero;
            word.alignment = TextAlignmentOptions.Center;
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) word.font = bold;
        }

        /// <summary>A vertical stroke down the middle of <paramref name="parent"/>, short of its top and foot.</summary>
        private void Stroke(RectTransform parent, string name, float width, Color colour)
        {
            var stroke = HudPrimitives.Fill(name, parent, colour, 1);
            stroke.anchorMin = new Vector2(.5f, 0f); stroke.anchorMax = new Vector2(.5f, 1f);
            stroke.pivot = new Vector2(.5f, .5f);
            stroke.anchoredPosition = Vector2.zero; stroke.sizeDelta = new Vector2(width, -16f * FontScale);
        }

        // ------------------------------------------------------------ the card

        private RectTransform FinalistCard(FinalistColumn finalist, float width, int index)
        {
            float s = FontScale;
            var actor = finalist.Actor;
            var read = finalist.Read;
            var card = HudPrimitives.KitCard(FinalistCardPrefix + actor.name, content, false, 12f);
            foreach (Transform decoration in card) decoration.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            // The juror's two cases wear the finale's card, the first finalist's edge in gold and the
            // second's in the accent, as mockup 50 tells the two apart.
            if (finalist.JurorCase) FinaleFrame(card, index == 0 ? UiTheme.Gold : Accent);
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            int pad = Mathf.RoundToInt(16f * s), top = Mathf.RoundToInt(14f * s);
            layout.padding = new RectOffset(pad, pad, top, pad);
            layout.spacing = 4f * s;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            float inner = width - 2f * pad;
            PushContent(card, inner);

            if (finalist.JurorCase)
            {
                JurorHeader(finalist, inner, new Vector2(pad, top));
                if (read != null) { Gap(4f); JurorStats(read, actor, inner); }
            }
            else ChoiceHeader(finalist, inner);

            // What the player knows, every line with how sure it is.
            if (read != null)
            {
                Gap(6f);
                FinalistEyebrow("WHAT YOU KNOW");
                if (finalist.JurorCase) JurorFacts(read, actor, inner);
                else if (read.caseFacts != null) foreach (var fact in read.Facts) FactRow(fact, inner);
                else ChoiceFacts(read, actor, inner);
            }

            if (finalist.Bullets != null && finalist.Bullets.Count > 0)
            {
                Gap(6f);
                FinalistEyebrow(finalist.BulletsHeading ?? "IF YOU TAKE " + FinalistRead.FirstName(actor.name).ToUpperInvariant(),
                    PackArt.IconTrophy, UiTheme.Gold);
                foreach (var bullet in finalist.Bullets) FlowText("• " + bullet, 15, Paper);
            }
            PopContent();
            return card;
        }

        /// <summary>
        /// The gold edge a choosing card lights while its control is the one about to be pressed:
        /// Kit 6's focus edge, the family the card's own resting edge is from, in the crown's gold.
        /// Off until <see cref="ChoiceLight"/> lights it.
        /// </summary>
        private static GameObject ChosenEdge(RectTransform card)
        {
            var edge = new GameObject(ChosenEdgeName, typeof(RectTransform), typeof(Image), typeof(LayoutElement)).GetComponent<Image>();
            edge.rectTransform.SetParent(card, false);
            edge.GetComponent<LayoutElement>().ignoreLayout = true;
            edge.rectTransform.anchorMin = Vector2.zero; edge.rectTransform.anchorMax = Vector2.one;
            edge.rectTransform.offsetMin = Vector2.zero; edge.rectTransform.offsetMax = Vector2.zero;
            edge.raycastTarget = false;
            if (!UiTheme.PackSliced(edge, PackArt.KitCardEdgeFocus, 12f, UiTheme.Gold))
            {
                UiTheme.Style(edge, new Color(0f, 0f, 0f, 0f), 12);
                UiTheme.AddBorder(edge.rectTransform, 12, UiTheme.Gold);
            }
            edge.gameObject.SetActive(false);
            return edge.gameObject;
        }

        /// <summary>
        /// The finale's card behind a juror's case: Pack 3's finalist card, drawn out past the rect
        /// by its baked glow so its body lands on the card, and the card's own edge in the column's
        /// colour over it. The art is not tinted: its edge is cyan, and gold over cyan is green.
        /// </summary>
        private void FinaleFrame(RectTransform card, Color tint)
        {
            var sprite = UiTheme.Pack(PackArt.FinalistCard);
            if (sprite != null)
            {
                var art = new GameObject("Finale frame", typeof(RectTransform), typeof(Image), typeof(LayoutElement)).GetComponent<Image>();
                art.rectTransform.SetParent(card, false);
                art.rectTransform.SetAsFirstSibling();
                art.GetComponent<LayoutElement>().ignoreLayout = true;
                art.raycastTarget = false;
                float border = FinaleFrameBorder * FontScale;
                UiTheme.PackSliced(art, PackArt.FinalistCard, border);
                float authored = Mathf.Max(1f, Mathf.Max(sprite.border.x, sprite.border.y, sprite.border.z, sprite.border.w));
                float overhang = FinaleFrameGlow * border / authored;
                art.rectTransform.anchorMin = Vector2.zero; art.rectTransform.anchorMax = Vector2.one;
                art.rectTransform.offsetMin = new Vector2(-overhang, -overhang);
                art.rectTransform.offsetMax = new Vector2(overhang, overhang);
            }
            var edge = card.Find("Border");
            var image = edge != null ? edge.GetComponent<Image>() : null;
            if (image != null) image.color = new Color(tint.r, tint.g, tint.b, .9f);
        }

        // ------------------------------------------------------------ who they are

        /// <summary>
        /// The comparison's header (mockup 59): the photo on the left; beside it the name, the archetype
        /// in the heading blue, age and job, the traits as chips and their own line in a quote box.
        /// </summary>
        private void ChoiceHeader(FinalistColumn finalist, float inner)
        {
            var actor = finalist.Actor;
            var identity = HeaderRow(inner, ChoicePhoto * FontScale, Vector2.zero, actor, out float right);
            PushContent(identity, right);
            var name = FlowText(actor.name, 22, Paper);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            if (finalist.Read != null && finalist.Read.finalHead) CrownCaption(right);
            if (!string.IsNullOrEmpty(actor.archetype))
                FlowText(actor.archetype, 14, UiTheme.Heading).fontStyle = FontStyles.Italic;
            string about = AgeAndJob(actor);
            if (about != null) FlowText(about, 13, UiTheme.Muted);
            TraitChips(actor, right);
            string quote = finalist.Quote ?? finalist.Read?.line;
            if (!string.IsNullOrEmpty(quote)) QuoteBox(quote, finalist.QuoteSource, right);
            PopContent();
        }

        /// <summary>
        /// The juror's header (mockup 50): a tall photo bleeding to the card's corner, the name in
        /// capitals, the crown when they are the final Head of Household, the archetype, and the
        /// first line of their final speech - or their own line, or nothing.
        /// </summary>
        private void JurorHeader(FinalistColumn finalist, float inner, Vector2 padding)
        {
            var actor = finalist.Actor;
            var identity = HeaderRow(inner, JurorPhoto * FontScale, padding, actor, out float right);
            PushContent(identity, right);
            var name = FlowText(actor.name.ToUpperInvariant(), 28, Paper);
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) name.font = bold;
            if (finalist.Read != null && finalist.Read.finalHead) CrownCaption(right);
            if (!string.IsNullOrEmpty(actor.archetype))
                FlowText(actor.archetype, 13, UiTheme.Heading).fontStyle = FontStyles.Italic;
            if (!string.IsNullOrEmpty(finalist.Quote)) QuoteBox(finalist.Quote, finalist.QuoteSource, right);
            PopContent();
        }

        /// <summary>
        /// A card's header row: the photo, <paramref name="photo"/> in size, reaching
        /// <paramref name="bleed"/> into the card's padding at its top-left, and beside it the
        /// column the rest of who they are goes in, which it returns with its width.
        /// </summary>
        private RectTransform HeaderRow(float inner, Vector2 photo, Vector2 bleed, ContestantState actor, out float right)
        {
            float s = FontScale, gap = 14f * s;
            var row = new GameObject("Card header", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = gap; layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            // The photo's place in the row is what it covers past the padding it bleeds into.
            var holder = new GameObject("Photo row", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            holder.SetParent(row, false);
            var place = holder.GetComponent<LayoutElement>();
            place.minWidth = place.preferredWidth = photo.x - bleed.x;
            place.minHeight = place.preferredHeight = photo.y - bleed.y;
            var portrait = HudPrimitives.RectPortrait(holder, "Photo", CharacterPortraits.Get(actor), actor, photo, 10);
            portrait.anchorMin = portrait.anchorMax = new Vector2(0f, 1f);
            portrait.pivot = new Vector2(0f, 1f); portrait.anchoredPosition = new Vector2(-bleed.x, bleed.y);

            right = Mathf.Max(80f * s, inner - (photo.x - bleed.x) - gap);
            var identity = new GameObject("Identity", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
            identity.SetParent(row, false);
            var stack = identity.GetComponent<VerticalLayoutGroup>();
            stack.spacing = 2f * s;
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = true; stack.childForceExpandHeight = false;
            var element = identity.GetComponent<LayoutElement>();
            element.minWidth = 0f; element.preferredWidth = right; element.flexibleWidth = 1f;
            return identity;
        }

        private static string AgeAndJob(ContestantState actor)
        {
            var parts = new List<string>();
            if (actor.age > 0) parts.Add(actor.age.ToString());
            if (!string.IsNullOrEmpty(actor.occupation)) parts.Add(actor.occupation);
            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }

        /// <summary>The crown on the final Head of Household, captioned (decision 33): it says what they won, never who is favoured.</summary>
        private void CrownCaption(float width)
        {
            float s = FontScale, side = 16f * s;
            var row = new GameObject("Final Head of Household", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 20f * s;
            float x = Mark("Crown", row, PackArt.KitIconCrown, "crown", UiTheme.Gold, new Vector2(0f, -2f * s), side) != null ? side + 6f * s : 0f;
            var words = FixedText(row, "FINAL HEAD OF HOUSEHOLD", 11, UiTheme.Gold, new Vector2(x, 0f), new Vector2(width - x, 20f * s));
            words.characterSpacing = 2f;
            OneLine(words, 8f);
        }

        /// <summary>
        /// Their traits as the cast screen's chips, each as wide as its word, running onto another
        /// line when the column is full.
        /// </summary>
        private void TraitChips(ContestantState actor, float width)
        {
            if (actor.traits == null || actor.traits.Count == 0) return;
            float s = FontScale, height = 20f * s, gap = 6f * s, x = 0f, y = 0f;
            var row = new GameObject("Trait chips", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            foreach (var trait in actor.traits)
            {
                string word = Localisation.Text(trait);
                var chip = HudPrimitives.Chip("Trait", row, word, CastSelect.TraitTint(trait), width, height);
                var label = chip.GetComponentInChildren<TMP_Text>();
                float wanted = Mathf.Min(width, Mathf.Ceil(label.GetPreferredValues(word).x) + 22f);
                if (x > 0f && x + wanted > width) { x = 0f; y += height + gap; }
                chip.anchorMin = chip.anchorMax = new Vector2(0f, 1f); chip.pivot = new Vector2(0f, 1f);
                chip.sizeDelta = new Vector2(wanted, height);
                chip.anchoredPosition = new Vector2(x, -y);
                // A word longer than the column shrinks to fit it rather than losing its end.
                OneLine(label, 8f);
                x += wanted + gap;
            }
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = y + height;
        }

        /// <summary>A line of theirs in Season Complete's quote panel, with where it was said under it when that is not their introduction.</summary>
        private void QuoteBox(string quote, string source, float width)
        {
            float s = FontScale;
            int pad = Mathf.RoundToInt(10f * s), rim = Mathf.RoundToInt(8f * s);
            var box = new GameObject("Quote box", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            box.SetParent(content, false);
            var layout = box.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(pad, pad, rim, rim);
            layout.spacing = 2f * s;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var art = EndScreenKit.Frame(box, PackArt.SeasonQuote, 10f * s, UiTheme.Surface);
            art.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            PushContent(box, width - 2f * pad);
            var words = FlowText("“" + quote + "”", 14, Paper);
            words.name = "Quote";
            words.fontStyle = FontStyles.Italic;
            if (!string.IsNullOrEmpty(source)) FlowText(source, 11, UiTheme.Muted).name = "Quote source";
            PopContent();
        }

        // ------------------------------------------------------------ what the player knows

        /// <summary>
        /// The comparison's facts (mockup 59): the record as counts, the player's standing as the cast
        /// strip's bar, the agreement and the alliances, and the jury read as three counts.
        /// </summary>
        private void ChoiceFacts(FinalistRead.Finalist read, ContestantState actor, float inner)
        {
            RecordSection(read, actor, inner);
            RelationshipSection(read, inner);
            FactRow(read.agreement, inner);
            FactRow(read.alliances, inner);
            JuryRead(read, inner);
        }

        /// <summary>
        /// The juror's facts (mockup 50): the player's standing with them and the agreement between
        /// them on one line, then the rest of the case - the record, the alliances the player knew
        /// and what they did to the player.
        /// </summary>
        private void JurorFacts(FinalistRead.Finalist read, ContestantState actor, float inner)
        {
            string standing = read.relationship != null ? read.relationship.value : "Neutral";
            string agreement = read.agreement == null || read.agreement.certainty == FinalistRead.Unknown
                ? "No Final 2 agreement" : read.agreement.value;
            FactRow(new FinalistRead.Fact("You and " + FinalistRead.FirstName(actor.name), standing + " · " + agreement, FinalistRead.Confirmed), inner);
            foreach (var fact in read.Facts)
                if (fact != read.relationship && fact != read.agreement) FactRow(fact, inner);
        }

        /// <summary>A fact's own column of rows, named as the fact rows are.</summary>
        private RectTransform FactSection(string label)
        {
            var row = new GameObject(FactRowName + " · " + label, typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var stack = row.GetComponent<VerticalLayoutGroup>();
            stack.spacing = 2f * FontScale;
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = true; stack.childForceExpandHeight = false;
            return row;
        }

        /// <summary>
        /// COMPETITION RECORD (mockup 59): the grade and how sure on its line, then the Heads of
        /// Household, vetoes and nominations as counts beside their marks, and a line for each final
        /// part they won.
        /// </summary>
        private void RecordSection(FinalistRead.Finalist read, ContestantState actor, float inner)
        {
            var resume = read.resume;
            if (resume == null) return;
            float s = FontScale, height = 24f * s, gap = 8f * s;
            var section = FactSection(resume.label);
            FactHead(section, resume.label, resume.certainty, read.grade, GradeTint(read.grade));
            var counts = new GameObject("Record counts", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            counts.SetParent(section, false);
            var element = counts.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;
            float cell = (inner - 2f * gap) / 3f;
            CountCell(counts, 0f, cell, height, PackArt.KitIconCrown, "crown", UiTheme.Gold, actor.hohWins, "HoH wins");
            CountCell(counts, cell + gap, cell, height, null, "veto-token", UiTheme.Heading, actor.vetoWins, "Veto wins");
            CountCell(counts, 2f * (cell + gap), cell, height, PackArt.KitIconPerson, "people", UiTheme.Muted, actor.timesNominated, "Times nominated");
            foreach (int part in read.finalPartsWon)
            {
                var line = new GameObject("Final part", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
                line.SetParent(section, false);
                var size = line.GetComponent<LayoutElement>();
                size.minHeight = size.preferredHeight = 20f * s;
                float x = Mark("Final part mark", line, PackArt.KitIconCrown, "crown", UiTheme.Gold, new Vector2(0f, -2f * s), 16f * s) != null ? 24f * s : 0f;
                var words = FixedText(line, "Won Final HoH Part " + part, 13, UiTheme.Gold, new Vector2(x, 0f), new Vector2(inner - x, 20f * s));
                OneLine(words, 9f);
            }
        }

        /// <summary>A count beside its mark and its word, in a cell of a row: "♛ 2 HoH wins".</summary>
        private void CountCell(RectTransform row, float x, float width, float height, string pack, string glyph, Color tint, int value, string words)
        {
            float s = FontScale, side = 18f * s;
            float left = x + (Mark("Count mark", row, pack, glyph, tint, new Vector2(x, -(height - side) * .5f), side) != null ? side + 6f * s : 0f);
            var number = FixedText(row, value.ToString(), 16, Paper, new Vector2(left, 0f), new Vector2(26f * s, height));
            number.name = "Count";
            number.alignment = TextAlignmentOptions.MidlineLeft;
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) number.font = bold;
            OneLine(number, 10f);
            float after = left + 28f * s;
            var label = FixedText(row, words, 12, UiTheme.Muted, new Vector2(after, 0f), new Vector2(Mathf.Max(10f, x + width - after), height));
            label.alignment = TextAlignmentOptions.MidlineLeft;
            OneLine(label, 8f);
        }

        /// <summary>
        /// YOUR RELATIONSHIP (mockup 59): the standing word in its colour, a bar labelled "Your
        /// standing" drawn from the player's own score as the cast strip's is, and a line from the
        /// record under it. What the player feels, never what they do.
        /// </summary>
        private void RelationshipSection(FinalistRead.Finalist read, float inner)
        {
            var fact = read.relationship;
            if (fact == null) return;
            float s = FontScale;
            var section = FactSection(fact.label);
            FactHead(section, fact.label, fact.certainty);
            var tint = StandingTint(fact.value);
            bool neutral = tint == UiTheme.Muted;
            var word = NewText(section, fact.value, 15, neutral ? Paper : tint);
            word.name = "Fact value";
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) word.font = semibold;
            word.gameObject.AddComponent<LayoutElement>().minHeight = 15f * s + 6f;

            var bar = new GameObject("Your standing", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            bar.SetParent(section, false);
            var element = bar.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 18f * s;
            float labelWidth = 96f * s;
            var label = FixedText(bar, "Your standing", 12, UiTheme.Muted, Vector2.zero, new Vector2(labelWidth, 18f * s));
            label.alignment = TextAlignmentOptions.MidlineLeft;
            OneLine(label, 8f);
            float trackWidth = Mathf.Max(20f, inner - labelWidth - 8f * s);
            var track = HudPrimitives.Fill("Standing track", bar, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .55f), 3);
            track.anchorMin = track.anchorMax = new Vector2(0f, 1f); track.pivot = new Vector2(0f, .5f);
            track.anchoredPosition = new Vector2(labelWidth + 8f * s, -9f * s); track.sizeDelta = new Vector2(trackWidth, 6f * s);
            float share = Mathf.Clamp01((float)(read.standing + 100.0) / 200f);
            if (share > 0f)
            {
                var fill = HudPrimitives.Fill("Standing fill", track, neutral ? UiTheme.Muted : tint, 3);
                fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(share, 1f);
                fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            }
            if (!string.IsNullOrEmpty(read.standingLine))
            {
                var line = NewText(section, read.standingLine, 13, UiTheme.Muted);
                line.name = "Standing line";
                line.gameObject.AddComponent<LayoutElement>().minHeight = 13f * s + 6f;
            }
        }

        /// <summary>
        /// JURY READ (mockup 59): how many jurors the player knows to be close to them, bitter, or
        /// cannot read, each count beside the jury mark in its colour and the jurors named under it
        /// with the evidence. The three rows keep the facts' own words: Known jury support, Jury
        /// bitterness, Uncertain jurors - never a lean the jury model holds.
        /// </summary>
        private void JuryRead(FinalistRead.Finalist read, float inner)
        {
            int jurors = read.jurors.Count;
            Gap(4f);
            FinalistEyebrow("JURY READ (" + jurors + (jurors == 1 ? " JUROR)" : " JURORS)"));
            JuryCount(read, read.support, FinalistRead.Support, UiTheme.Allied, inner);
            JuryCount(read, read.bitterness, FinalistRead.Bitter, UiTheme.Danger, inner);
            JuryCount(read, read.uncertain, FinalistRead.Uncertain, UiTheme.Muted, inner);
        }

        private void JuryCount(FinalistRead.Finalist read, FinalistRead.Fact fact, string lean, Color tint, float inner)
        {
            if (fact == null) return;
            float s = FontScale, height = 22f * s, indent = 26f * s, sure = 76f * s, count = 36f * s;
            var leans = read.jurors.Where(juror => juror.lean == lean).ToList();
            var section = FactSection(fact.label);
            var line = new GameObject("Jury count", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            line.SetParent(section, false);
            var element = line.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;
            Mark("Jury mark", line, PackArt.KitIconJury, "people", tint, new Vector2(0f, -2f * s), 18f * s);
            var label = FixedText(line, fact.label.ToUpperInvariant(), 12, UiTheme.Muted, new Vector2(indent, 0f),
                new Vector2(Mathf.Max(10f, inner - indent - sure - count - 8f * s), height));
            label.characterSpacing = 3f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            OneLine(label, 8f);
            var number = FixedText(line, leans.Count.ToString(), 16, leans.Count > 0 ? tint : UiTheme.Muted,
                new Vector2(inner - sure - count, 0f), new Vector2(count, height));
            number.name = "Jury count value";
            number.alignment = TextAlignmentOptions.MidlineRight;
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) number.font = bold;
            OneLine(number, 10f);
            var certainty = FixedText(line, fact.certainty, 12, CertaintyTint(fact.certainty), new Vector2(inner - sure, 0f), new Vector2(sure, height));
            certainty.name = "Certainty";
            certainty.alignment = TextAlignmentOptions.MidlineRight;
            OneLine(certainty, 8f);
            // Who, and on what evidence. An uncertain juror with nothing to go on is just named.
            string names = string.Join(", ", leans.Select(juror => lean != FinalistRead.Uncertain || juror.reason == FinalistRead.MixedSignals
                ? juror.name + " (" + juror.reason + ")" : juror.name));
            if (names.Length == 0) return;
            var list = NewText(section, names, 12, UiTheme.Muted);
            list.name = "Jury names";
            list.margin = new Vector4(indent, 0f, 0f, 0f);
            list.gameObject.AddComponent<LayoutElement>().minHeight = 12f * s + 4f;
        }

        /// <summary>
        /// The juror's three numbers (mockup 50), as tiles: competition wins, eviction votes
        /// survived (decision 32) and times on the block. All of them public.
        /// </summary>
        private void JurorStats(FinalistRead.Finalist read, ContestantState actor, float inner)
        {
            float s = FontScale, height = 48f * s;
            var row = new GameObject(FinalistStatsName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;
            StatTiles(row, read, actor, inner, height, false);
        }

        /// <summary>
        /// The three public numbers' tiles across <paramref name="row"/>, <paramref name="width"/>
        /// wide: the juror's case's, and the final choice's portrait card's, which wears them on the
        /// pack's stat tile (<paramref name="packed"/>).
        /// </summary>
        private void StatTiles(RectTransform row, FinalistRead.Finalist read, ContestantState actor, float width, float height, bool packed)
        {
            float gap = 8f * FontScale, cell = (width - 2f * gap) / 3f;
            StatTile(row, 0f, cell, height, null, "trophy", UiTheme.Gold, read.wins, "Comp wins", packed);
            StatTile(row, cell + gap, cell, height, null, "people", UiTheme.Accent, read.votesSurvived, "Votes survived", packed);
            StatTile(row, 2f * (cell + gap), cell, height, PackArt.KitIconShield, "veto-token", UiTheme.Heading, actor.timesNominated, "Times on block", packed);
        }

        private void StatTile(RectTransform row, float x, float width, float height, string pack, string glyph, Color tint, int value, string words,
            bool packed = false)
        {
            float s = FontScale, side = 20f * s, pad = 10f * s;
            var tile = HudPrimitives.Fill("Stat tile", row, new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .7f), 8);
            tile.anchorMin = tile.anchorMax = new Vector2(0f, 1f); tile.pivot = new Vector2(0f, 1f);
            tile.anchoredPosition = new Vector2(x, 0f); tile.sizeDelta = new Vector2(width, height);
            // The pack's stat tile over the plain ground, its edge in the number's colour, as the
            // finale's highlights wear it; the plain ground alone where the pack is not installed.
            if (packed && UiTheme.Pack(PackArt.Pack9SeasonFinaleStatTileFill) != null)
                EndScreenKit.Skin(tile, PackArt.Pack9SeasonFinaleStatTileFill, PackArt.Pack9SeasonFinaleStatTileEdge, 10f * s,
                    new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .9f), new Color(tint.r, tint.g, tint.b, .45f),
                    null, UiTheme.SurfaceRaised, null, 8);
            float left = Mark("Stat mark", tile, pack, glyph, tint, new Vector2(pad, -(height - side) * .5f), side) != null ? pad + side + 8f * s : pad;
            float room = Mathf.Max(10f, width - left - 6f * s);
            var number = FixedText(tile, value.ToString(), 20, Paper, new Vector2(left, -3f * s), new Vector2(room, 26f * s));
            number.name = "Stat value";
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) number.font = bold;
            OneLine(number, 12f);
            var label = FixedText(tile, words, 11, UiTheme.Muted, new Vector2(left, -28f * s), new Vector2(room, 16f * s));
            label.name = "Stat label";
            OneLine(label, 8f);
        }

        /// <summary>
        /// A kit icon at a parent's upper-left: the pack sprite when there is one, the generated glyph
        /// when not, and null with neither, so the caller lays its words out without a mark.
        /// </summary>
        private static Image Mark(string name, Transform parent, string pack, string glyph, Color tint, Vector2 position, float side)
        {
            var sprite = string.IsNullOrEmpty(pack) ? null : UiTheme.Pack(pack);
            if (sprite == null && !string.IsNullOrEmpty(glyph)) sprite = UiTheme.Icon(glyph);
            if (sprite == null) return null;
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = image.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f); rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position; rect.sizeDelta = new Vector2(side, side);
            image.sprite = sprite; image.color = tint; image.preserveAspect = true; image.raycastTarget = false;
            return image;
        }

        /// <summary>A fixed label kept to one line, shrinking toward <paramref name="floor"/> rather than losing its end.</summary>
        private static void OneLine(TMP_Text label, float floor)
        {
            label.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(label, floor);
        }

        private void FinalistEyebrow(string words, string icon = null, Color? tint = null)
        {
            if (string.IsNullOrEmpty(icon))
            {
                var eyebrow = FlowText(words, 12, Accent);
                eyebrow.characterSpacing = 6f;
                return;
            }
            // With a mark before it: its own row, the words where the mark leaves them.
            float s = FontScale, side = 16f * s;
            var row = new GameObject("Eyebrow", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 20f * s;
            float x = Mark("Eyebrow mark", row, icon, null, tint ?? Accent, new Vector2(0f, -2f * s), side) != null ? side + 8f * s : 0f;
            var label = FixedText(row, words, 12, Accent, new Vector2(x, 0f), new Vector2(Mathf.Max(10f, ContentWidth() - x), 20f * s));
            label.characterSpacing = 6f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            OneLine(label, 8f);
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
            var row = FactSection(fact.label);
            row.GetComponent<VerticalLayoutGroup>().spacing = 0f;
            FactHead(row, fact.label, fact.certainty);
            var value = NewText(row, fact.value, 15, Paper);
            value.name = "Fact value";
            value.gameObject.AddComponent<LayoutElement>().minHeight = 15f * FontScale + 6f;
        }

        /// <summary>A fact's head: its label, tracked and muted, then an optional note in its colour and how sure, at the right.</summary>
        private void FactHead(Transform parent, string label, string certainty, string note = null, Color? noteTint = null)
        {
            float s = FontScale;
            var head = new GameObject("Fact head", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            head.SetParent(parent, false);
            var line = head.GetComponent<HorizontalLayoutGroup>();
            line.spacing = 8f * s; line.childControlWidth = line.childControlHeight = true;
            line.childForceExpandWidth = false; line.childForceExpandHeight = false;
            var words = NewText(head, label.ToUpperInvariant(), 12, UiTheme.Muted);
            words.characterSpacing = 3f;
            var wordsElement = words.gameObject.AddComponent<LayoutElement>();
            wordsElement.flexibleWidth = 1f; wordsElement.minWidth = 0f;
            if (!string.IsNullOrEmpty(note))
            {
                var extra = NewText(head, note, 12, noteTint ?? Paper);
                extra.name = "Fact note";
                extra.alignment = TextAlignmentOptions.Right;
                var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
                if (semibold != null) extra.font = semibold;
            }
            var sure = NewText(head, certainty, 12, CertaintyTint(certainty));
            sure.name = "Certainty";
            sure.alignment = TextAlignmentOptions.Right;
            sure.gameObject.AddComponent<LayoutElement>().minWidth = 72f * s;
        }
    }
}
