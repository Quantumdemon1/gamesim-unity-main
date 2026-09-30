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
    /// Prepare your final case as a screen (MOCKUP-PASS M15, mockup 57): the station's band named
    /// for it, a head with the case's state, the jury chip and the brand line, then three columns -
    /// the narratives, the season's résumé, the moments as cards with the faces they are about - and
    /// under them the tray of the moments chosen and the gold lock.
    ///
    /// <para>Every control is the one the screen always had, under the caption it always had: a
    /// narrative is still a row captioned by its label, a moment still a control captioned by its
    /// words, the lock still "Lock final argument" (its capitals are the font style's, not the
    /// text's). The moments stay in the page's own flow, two to a row where they fit, so every one
    /// is a control the panel scrolls to; nothing is filtered and nothing scrolls on its own.</para>
    ///
    /// <para>Faces come from the portrait studio, bound so a face still being built lands when it is
    /// ready. Nothing here reads past the player's own record, and nothing here commits: the lock is
    /// the director's.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The final case's parts a test finds by name.</summary>
        public const string FinalCaseHeadName = "Final case head", FinalCaseChipName = "Final case chip", FinalCaseQuoteName = "Final case quote",
            FinalCaseColumnsName = "Final case columns", FinalCaseStatsName = "Résumé stats", FinalCaseTrayName = "Final case tray",
            FinalCaseSlotPrefix = "Tray slot · ", FinalCaseLockRowName = "Final case lock", FinalCaseLockedName = "Locked narrative";

        /// <summary>The word a chosen card wears beside its ring: the signal that is not a colour.</summary>
        public const string ChosenWord = "Chosen";

        /// <summary>A radio ring's side at the resting text size.</summary>
        private const float RadioSide = 22f;

        /// <summary>Where the scroll starts once the station's hint has gone: under the band, as a house event's does.</summary>
        private const float StationScreenTop = 72f;

        /// <summary>The selected panel's corners, in canvas units, and how deep its baked glow runs in the sprite's pixels.</summary>
        private const float SelectedEdgeBorder = 18f, SelectedGlowPixels = 40f;

        /// <summary>The band's parts, as <see cref="PhaseBand"/> built them this render.</summary>
        private TMP_Text bandTitle, bandLine;
        private Image bandGlyph, bandStroke;

        // ------------------------------------------------------------ the frame

        /// <summary>
        /// Makes the station's panel a screen of its own: the band's title becomes the screen's name,
        /// with its glyph and its line in place of the week's; the control hint goes, as a house
        /// event's does, and the scroll rises into its row; and the stage's reading cap lifts, so a
        /// screen of columns has the frame's width. Called before the screen's rows, which measure
        /// the column as they are built.
        /// </summary>
        public void StationScreen(string title, string line, string glyph, Color tint)
        {
            if (modal == null) return;
            UncapContent();
            var hint = modal.Find(PanelHintName);
            if (hint != null) hint.gameObject.SetActive(false);
            if (modalScroll != null)
            {
                var scroll = (RectTransform)modalScroll.transform;
                scroll.offsetMax = new Vector2(scroll.offsetMax.x, -StationScreenTop);
            }
            if (bandTitle != null) bandTitle.text = Localisation.Text(title);
            if (bandLine != null) { bandLine.text = Localisation.Text(line); bandLine.color = UiTheme.Muted; }
            var sprite = UiTheme.Icon(glyph);
            if (bandGlyph != null && sprite != null) { bandGlyph.sprite = sprite; bandGlyph.color = tint; }
            if (bandStroke != null) bandStroke.color = tint;
        }

        /// <summary>
        /// The final case's head: the case's state large on the left - what the jury will remember,
        /// or that the argument is locked - with the line under it and a chip that is not a door;
        /// the brand's line in Season Complete's quote frame on the right, unattributed. One under
        /// the other at the larger text or on a narrow stage.
        /// </summary>
        public void FinalCaseHead(string headline, string line, string chip, string chipGlyph, string quote)
        {
            if (content == null) return;
            float s = FontScale, width = ContentWidth(), gap = 18f * s;
            bool beside = width >= 760f && s <= 1.05f && !string.IsNullOrEmpty(quote);
            var head = new GameObject(FinalCaseHeadName, typeof(RectTransform)).GetComponent<RectTransform>();
            head.SetParent(content, false);
            HorizontalOrVerticalLayoutGroup layout = beside
                ? (HorizontalOrVerticalLayoutGroup)head.gameObject.AddComponent<HorizontalLayoutGroup>()
                : head.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = beside ? gap : 10f * s; layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            float quoteWidth = beside ? Mathf.Min(380f * s, width * .36f) : width;
            float words = beside ? width - quoteWidth - gap : width;
            var column = Column("Final case headline", head, words, beside ? 1f : 0f);
            column.GetComponent<VerticalLayoutGroup>().spacing = 4f * s;
            PushContent(column, words - 4f);
            var big = FlowText(headline, 26, UiTheme.Glow);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) big.font = semibold;
            big.characterSpacing = 3f;
            big.name = "Headline";
            if (!string.IsNullOrEmpty(line)) FlowText(line, 14, UiTheme.Muted);
            if (!string.IsNullOrEmpty(chip)) GlyphChip(FinalCaseChipName, chip, chipGlyph, UiTheme.Gold);
            PopContent();
            if (!string.IsNullOrEmpty(quote)) BrandQuote(head, quote, quoteWidth);
        }

        /// <summary>A chip with a glyph in it, on a row of its own in the current column. Words, never a control.</summary>
        private void GlyphChip(string name, string words, string glyph, Color tint)
        {
            float s = FontScale, height = 24f * s, mark = 14f * s, lead = 8f * s + mark + 6f * s;
            var row = new GameObject(name, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;
            float width = Mathf.Min(ContentWidth(), (words.Length * 7.5f + 18f) * s + lead);
            var chip = HudPrimitives.Chip("Chip", row, words, tint, width, height);
            Anchor(chip, new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, chip.sizeDelta);
            if (chip.Find("Word") is RectTransform label) label.offsetMin = new Vector2(lead, 0f);
            HudPrimitives.Glyph("Chip mark", chip, glyph, tint, new Vector2(8f * s, -(height - mark) * .5f), mark);
        }

        /// <summary>A line of the brand's in Season Complete's quote frame, centred and in italics, as tall as its words.</summary>
        private void BrandQuote(RectTransform parent, string quote, float width)
        {
            float s = FontScale, pad = 16f * s;
            var frame = new GameObject(FinalCaseQuoteName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            frame.SetParent(parent, false);
            EndScreenKit.Frame(frame, PackArt.SeasonQuote, 14f, new Color(UiTheme.Surface.r, UiTheme.Surface.g, UiTheme.Surface.b, .92f));
            var words = NewText(frame, quote, 17, Paper);
            words.fontStyle = FontStyles.Italic;
            words.alignment = TextAlignmentOptions.Center;
            float height = Mathf.Max(Mathf.Ceil(words.GetPreferredValues(words.text, width - 2f * pad, 0f).y) + 2f, words.fontSize * 1.32f);
            Stretch(words.rectTransform, pad, 10f * s, pad, 10f * s);
            var element = frame.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minHeight = element.preferredHeight = height + 20f * s;
        }

        // ------------------------------------------------------------ the columns

        private readonly List<(RectTransform column, float width)> caseColumns = new List<(RectTransform, float)>();
        private int caseColumn;

        /// <summary>
        /// Opens the screen's columns at <paramref name="shares"/> of the width, side by side at the
        /// resting text on a wide stage and one under another otherwise; the rows go to the first
        /// until <see cref="NextCaseColumn"/>.
        /// </summary>
        public void BeginCaseColumns(params float[] shares)
        {
            caseColumns.Clear();
            if (content == null || shares == null || shares.Length == 0) return;
            float s = FontScale, width = ContentWidth(), gap = 18f * s;
            bool beside = width >= 900f && s <= 1.05f;
            var row = new GameObject(FinalCaseColumnsName, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            HorizontalOrVerticalLayoutGroup layout = beside
                ? (HorizontalOrVerticalLayoutGroup)row.gameObject.AddComponent<HorizontalLayoutGroup>()
                : row.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = beside ? gap : 16f * s; layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            float room = beside ? width - gap * (shares.Length - 1) : width;
            for (int i = 0; i < shares.Length; i++)
            {
                float each = beside ? room * shares[i] : width;
                caseColumns.Add((Column("Case column " + (i + 1), row, each, 0f), each - 4f));
            }
            caseColumn = 0;
            PushContent(caseColumns[0].column, caseColumns[0].width);
        }

        /// <summary>The rows go to the next column from here.</summary>
        public void NextCaseColumn()
        {
            if (caseColumn + 1 >= caseColumns.Count) return;
            PopContent();
            caseColumn++;
            PushContent(caseColumns[caseColumn].column, caseColumns[caseColumn].width);
        }

        /// <summary>Back to the panel's own column, under all of them.</summary>
        public void EndCaseColumns()
        {
            if (caseColumns.Count == 0) return;
            PopContent();
            caseColumns.Clear();
        }

        // ------------------------------------------------------------ chosen things

        /// <summary>
        /// The chosen card's edge: Pack 1's selected panel under its words, drawn out past the card
        /// by its baked glow so its frame lands on the card's own edge, and the card's edge lit.
        /// Where the pack is missing, the lit edge and the house's glow.
        /// </summary>
        private static void SelectedEdge(RectTransform card)
        {
            HudEmphasis.Promote(card, UiTheme.Emphasis.Active);
            var sprite = UiTheme.Pack(PackArt.PanelSelected);
            if (sprite == null) { UiTheme.AddGlow(card, UiTheme.GlassRadius); return; }
            var art = new GameObject("Selected edge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = art.rectTransform;
            rect.SetParent(card, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            art.raycastTarget = false;
            UiTheme.PackSliced(art, PackArt.PanelSelected, SelectedEdgeBorder);
            float overhang = SelectedGlowPixels * SelectedEdgeBorder / Mathf.Max(1f, sprite.border.x);
            rect.offsetMin = new Vector2(-overhang, -overhang);
            rect.offsetMax = new Vector2(overhang, overhang);
            // A little of the card's own ground shows through, so a hover or the keyboard's focus
            // still lights the chosen card as it lights the rest.
            art.color = new Color(1f, 1f, 1f, .85f);
        }

        /// <summary>A radio: Pack 1's selection ring, dim until chosen, and filled with the accent once it is.</summary>
        private static void RadioRing(RectTransform card, bool chosen, float side, Vector2 anchor, Vector2 position)
        {
            var ring = new GameObject("Choice ring", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            ring.rectTransform.SetParent(card, false);
            Anchor(ring.rectTransform, anchor, anchor, position, new Vector2(side, side));
            var sprite = UiTheme.Pack(PackArt.SelectionRing);
            ring.sprite = sprite != null ? sprite : UiTheme.Ring();
            ring.color = sprite != null ? new Color(1f, 1f, 1f, chosen ? 1f : .45f) : chosen ? Accent : UiTheme.Muted;
            ring.preserveAspect = true;
            ring.raycastTarget = false;
            if (!chosen) return;
            var dot = HudPrimitives.Disc("Chosen mark", ring.rectTransform, Accent);
            dot.anchorMin = dot.anchorMax = dot.pivot = new Vector2(.5f, .5f);
            dot.anchoredPosition = Vector2.zero;
            dot.sizeDelta = new Vector2(side * .46f, side * .46f);
        }

        /// <summary>
        /// A choice already made, as the row that made it looks - its glyph, its caption, its line,
        /// its filled ring and its corner's word - but not a control: a locked narrative read back.
        /// </summary>
        public void ChosenTile(string name, MoveTile tile)
        {
            if (content == null) return;
            var rect = Chrome(name, content, UiTheme.Emphasis.Resting);
            rect.GetComponent<Image>().raycastTarget = false;
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 78f * FontScale;
            tile.Selected = true;
            RowTile(rect, tile, ContentWidth());
        }

        // ------------------------------------------------------------ the moments

        /// <summary>
        /// One moment as a card: the face it is about, its week, a title in a few words, the moment's
        /// own words - the control's caption, which is how a test and a screen reader find it - the
        /// narrative it backs, and a radio. Without <see cref="Choose"/>, a card to read, not a control.
        /// </summary>
        public struct MomentCard
        {
            public string Caption, SubjectId, Title, Backs, Glyph;
            public int Week;
            public bool? Selected;
            public Action Choose;
        }

        /// <summary>
        /// The moments as cards in a grid named <paramref name="gridName"/>: two to a row where two
        /// fit, one otherwise, each row as tall as its taller card. Every card is in the page's own
        /// flow, so the panel's scroll reaches each one.
        /// </summary>
        public void MomentCards(string gridName, IList<MomentCard> cards)
        {
            if (content == null || cards == null || cards.Count == 0) return;
            float s = FontScale, width = ContentWidth(), gap = 10f * s;
            int perRow = (width - gap) * .5f >= 200f * s ? 2 : 1;
            float cardWidth = perRow == 2 ? (width - gap) * .5f : width;
            var grid = new GameObject(gridName, typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            grid.SetParent(content, false);
            var stack = grid.GetComponent<VerticalLayoutGroup>();
            stack.spacing = gap;
            stack.childControlWidth = true; stack.childControlHeight = true;
            stack.childForceExpandWidth = true; stack.childForceExpandHeight = false;
            RectTransform row = null;
            for (int i = 0; i < cards.Count; i++)
            {
                if (i % perRow == 0)
                {
                    row = new GameObject("Moment row", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
                    row.SetParent(grid, false);
                    var line = row.GetComponent<HorizontalLayoutGroup>();
                    line.spacing = gap; line.childAlignment = TextAnchor.UpperLeft;
                    line.childControlWidth = true; line.childControlHeight = true;
                    line.childForceExpandWidth = false; line.childForceExpandHeight = true;
                }
                MomentCardIn(row, cards[i], cardWidth);
            }
        }

        private void MomentCardIn(RectTransform row, MomentCard card, float width)
        {
            float s = FontScale, pad = 10f * s, ring = RadioSide * .9f * s;
            var photo = new Vector2(48f, 60f) * s;
            bool control = card.Choose != null;
            var rect = Chrome(card.Caption, row, control ? UiTheme.Emphasis.Interactive : UiTheme.Emphasis.Resting);
            if (control)
            {
                HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
                var button = Pressable(rect, card.Choose);
                var colours = button.colors;
                colours.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
                colours.selectedColor = colours.highlightedColor;
                button.colors = colours;
            }
            // A card to read takes no pointer, so it never lights under one as a control would.
            else rect.GetComponent<Image>().raycastTarget = false;
            if (card.Selected == true) SelectedEdge(rect);

            // The face the moment is about; its glyph where the row it came from is gone.
            var state = director != null ? director.Snapshot : null;
            var actor = state?.Find(card.SubjectId);
            RectTransform face;
            if (actor != null) face = HudPrimitives.RectPortrait(rect, "Moment photo", Portrait(card.SubjectId), actor, photo, 6);
            else
            {
                face = Panel("Moment photo", rect, UiTheme.SurfaceRaised, 6);
                face.GetComponent<Image>().raycastTarget = false;
                HudPrimitives.Glyph("Moment mark", face, card.Glyph ?? "journal", Accent, new Vector2((photo.x - 26f * s) * .5f, -(photo.y - 26f * s) * .5f), 26f * s);
            }
            Anchor(face, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -pad), photo);
            // The word that is not a colour, on the face's foot as the house's cards carry a role.
            if (card.Selected == true)
            {
                var pill = Panel("Chosen pill", face, UiTheme.Allied, 4);
                pill.GetComponent<Image>().raycastTarget = false;
                Anchor(pill, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 3f * s), new Vector2(photo.x - 6f * s, 16f * s));
                var word = FixedText(pill, ChosenWord, 10, UiTheme.Ink, Vector2.zero, pill.sizeDelta);
                word.alignment = TextAlignmentOptions.Center;
                word.name = "Chosen word";
            }

            float x = pad + photo.x + 10f * s;
            float right = card.Selected.HasValue ? ring + 6f * s : 0f;
            if (card.Selected.HasValue) RadioRing(rect, card.Selected.Value, ring, new Vector2(1f, 1f), new Vector2(-pad, -pad));
            string when = card.Week > 0 ? "Week " + card.Week : "Season";
            float chip = Mathf.Min((when.Length * 7f + 18f) * s, Mathf.Max(44f * s, width - x - pad - right - 4f * s));
            var week = HudPrimitives.Chip("Moment week", rect, when, UiTheme.Heading, chip, 18f * s);
            Anchor(week, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -pad), week.sizeDelta);
            var title = FixedText(rect, card.Title ?? "", 14, Paper, new Vector2(x, -(pad + 24f * s)), new Vector2(width - x - pad, 20f * s));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) title.font = semibold;
            AutoSize(title, 10);
            title.name = "Moment title";

            // The moment's own words under the face, as tall as they need: the control's caption.
            float top = pad + photo.y + 6f * s, inner = width - 2f * pad;
            var words = NewText(rect, card.Caption, 13, Paper);
            float height = Mathf.Max(Mathf.Ceil(words.GetPreferredValues(words.text, inner, 0f).y) + 2f, words.fontSize * 1.32f);
            Anchor(words.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -top), new Vector2(inner, height));
            top += height + 6f * s;
            if (!string.IsNullOrEmpty(card.Backs))
            {
                float backsWidth = Mathf.Min((card.Backs.Length * 7f + 18f) * s, inner);
                var backs = HudPrimitives.Chip("Moment backs", rect, card.Backs, UiTheme.Allied, backsWidth, 18f * s);
                Anchor(backs, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -top), backs.sizeDelta);
                top += 18f * s;
            }
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width; element.flexibleWidth = 0f;
            element.minHeight = element.preferredHeight = top + pad;
        }

        // ------------------------------------------------------------ the résumé

        /// <summary>The résumé's card: whose it is, how far they got, the quote slot, and the season's read.</summary>
        public struct ResumeCard
        {
            public ContestantState Actor;
            public string Standing, QuoteLabel, Quote, QuoteEmpty;
            public FinalCaseResume Read;
        }

        /// <summary>
        /// The season's résumé as a card named <see cref="FinalCaseResumeName"/>: the player's photo
        /// with their name, how far they got and the quote slot beside it; four of Season Complete's
        /// stat cards; and the moves, the alliances, the broken word and the weeks, each a short list.
        /// </summary>
        public void FinalCaseResumeCard(ResumeCard card)
        {
            if (content == null || card.Read == null) return;
            float s = FontScale;
            var root = HudPrimitives.KitCard(FinalCaseResumeName, content, false, 12f);
            // The kit card's own edge stays stretched over it: the group lays out only the rows.
            foreach (Transform decoration in root) decoration.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            int pad = Mathf.RoundToInt(14f * s);
            layout.padding = new RectOffset(pad, pad, pad, pad);
            layout.spacing = 6f * s;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            PushContent(root, ContentWidth() - 2f * pad);
            ResumeHead(card);
            ResumeStats(card.Read);
            var read = card.Read;
            ResumeSection("MAJOR MOVES", read.majorMoves.Select(line => "Week " + line.week + " · " + line.text), "No move of yours on the record yet.");
            var pacts = read.alliances.ToList();
            if (read.moreAlliances > 0) pacts.Add("and " + read.moreAlliances + " more");
            ResumeSection("KEY ALLIANCES", pacts, "None.");
            ResumeSection("BETRAYALS", new[] { read.brokenByYou + " by you · " + read.brokenAgainstYou + " against you" }.Concat(read.betrayals), null);
            ResumeSection("KEY WEEKS", read.keyWeeks.Select(line => "Week " + line.week + " – " + line.text), null);
            PopContent();
        }

        private void ResumeHead(ResumeCard card)
        {
            float s = FontScale, width = ContentWidth(), gap = 12f * s;
            var photo = new Vector2(120f, 150f) * s;
            bool beside = width - photo.x - gap >= 150f * s;
            var row = new GameObject("Résumé head", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            HorizontalOrVerticalLayoutGroup layout = beside
                ? (HorizontalOrVerticalLayoutGroup)row.gameObject.AddComponent<HorizontalLayoutGroup>()
                : row.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = gap; layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            var holder = new GameObject("Résumé photo", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            holder.SetParent(row, false);
            var element = holder.GetComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = photo.x;
            element.minHeight = element.preferredHeight = photo.y;
            if (card.Actor != null)
            {
                var picture = HudPrimitives.RectPortrait(holder, "Photo", CharacterPortraits.Get(card.Actor), card.Actor, photo, 10);
                Anchor(picture, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, photo);
            }
            float words = beside ? width - photo.x - gap : width;
            var column = Column("Résumé words", row, words, beside ? 1f : 0f);
            column.GetComponent<VerticalLayoutGroup>().spacing = 4f * s;
            PushContent(column, words - 4f);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            var name = FlowText(card.Actor != null ? card.Actor.name : "You", 20, Paper);
            if (semibold != null) name.font = semibold;
            name.name = "Résumé name";
            if (!string.IsNullOrEmpty(card.Standing))
            {
                var standing = FlowText(card.Standing.ToUpperInvariant(), 13, UiTheme.Gold);
                if (semibold != null) standing.font = semibold;
                standing.characterSpacing = 3f;
            }
            if (!string.IsNullOrEmpty(card.QuoteLabel))
            {
                var label = FlowText(card.QuoteLabel, 11, UiTheme.Heading);
                if (semibold != null) label.font = semibold;
                label.characterSpacing = 3f;
            }
            // The claim unquoted, and after the lock the speech's opening (decision 44): the
            // player's own words, never a line put in anybody's mouth.
            bool said = !string.IsNullOrEmpty(card.Quote);
            var quote = FlowText(said ? card.Quote : card.QuoteEmpty ?? "", 14, said ? Paper : UiTheme.Muted);
            if (said) quote.fontStyle = FontStyles.Italic;
            quote.name = "Résumé quote";
            PopContent();
        }

        /// <summary>Four of Season Complete's stat cards, two to a row in a narrow column and four in a wide one.</summary>
        private void ResumeStats(FinalCaseResume read)
        {
            float s = FontScale, width = ContentWidth(), gap = 8f * s;
            var tiles = new[]
            {
                ("COMPETITION WINS", read.wins, UiTheme.Gold, PackArt.SeasonStatCompetitions, "trophy"),
                ("NOMINATIONS SURVIVED", read.nominationsSurvived, UiTheme.Danger, PackArt.SeasonStatNeutral, "target"),
                ("WEEKS IN THE HOUSE", read.weeks, Accent, PackArt.SeasonStatNeutral, "house"),
                ("WEEKS IN POWER", read.weeksInPower, UiTheme.Gold, PackArt.SeasonStatCompetitions, "crown"),
            };
            int perRow = width >= 520f * s ? 4 : 2;
            float cell = (width - gap * (perRow - 1)) / perRow, height = 66f * s, pad = 10f * s, mark = 16f * s;
            int rows = (tiles.Length + perRow - 1) / perRow;
            var grid = new GameObject(FinalCaseStatsName, typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
            grid.SetParent(content, false);
            var layout = grid.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(cell, height);
            layout.spacing = new Vector2(gap, gap);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = perRow;
            var size = grid.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = rows * height + (rows - 1) * gap;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            foreach (var (caption, value, tint, frame, glyph) in tiles)
            {
                var tile = new GameObject(caption, typeof(RectTransform)).GetComponent<RectTransform>();
                tile.SetParent(grid, false);
                EndScreenKit.Frame(tile, frame, 12f, new Color(UiTheme.Surface.r, UiTheme.Surface.g, UiTheme.Surface.b, .92f),
                    new Color(tint.r, tint.g, tint.b, .55f));
                HudPrimitives.Glyph("Stat mark", tile, glyph, tint, new Vector2(cell - pad - mark, -pad), mark);
                var number = FixedText(tile, value.ToString(), 22, tint, new Vector2(pad, -6f * s), new Vector2(cell - 2f * pad - mark - 4f * s, 30f * s));
                if (semibold != null) number.font = semibold;
                AutoSize(number, 14);
                number.name = "Stat value";
                var words = FixedText(tile, caption, 10, UiTheme.Muted, new Vector2(pad, -36f * s), new Vector2(cell - 2f * pad, 26f * s));
                if (semibold != null) words.font = semibold;
                words.characterSpacing = 1f;
                AutoSize(words, 8);
                words.name = "Stat caption";
            }
        }

        /// <summary>A short list under an eyebrow; <paramref name="empty"/> in the muted grey when there is nothing to list.</summary>
        private void ResumeSection(string title, IEnumerable<string> lines, string empty)
        {
            var eyebrow = FlowText(title, 11, UiTheme.Heading);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) eyebrow.font = semibold;
            eyebrow.characterSpacing = 3f;
            eyebrow.name = "Résumé section";
            bool any = false;
            foreach (var line in lines) { FlowText(line, 13, Paper); any = true; }
            if (!any && !string.IsNullOrEmpty(empty)) FlowText(empty, 13, UiTheme.Muted);
        }

        // ------------------------------------------------------------ the tray and the lock

        /// <summary>One of the tray's slots: the chosen moment's face, week and title, or an empty slot when <see cref="Title"/> is null.</summary>
        public struct TraySlot
        {
            public string SubjectId, Title;
            public int Week;
        }

        /// <summary>
        /// The tray: the moments chosen so far, in the order they were chosen, with the count, and a
        /// dimmed slot for each still to choose. A slot shows the face, the week and the title - never
        /// the moment's own words, which are its card's caption - and nothing on it is a control.
        /// </summary>
        public void FinalCaseTray(string heading, string count, IList<TraySlot> slots)
        {
            if (content == null || slots == null || slots.Count == 0) return;
            float s = FontScale, width = ContentWidth(), pad = 14f * s, gap = 10f * s;
            var tray = HudPrimitives.KitCard(FinalCaseTrayName, content, false, 12f);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            var title = FixedText(tray, heading, 13, UiTheme.Heading, new Vector2(pad, -pad), new Vector2(width * .6f, 20f * s));
            if (semibold != null) title.font = semibold;
            title.characterSpacing = 3f;
            var tally = FixedText(tray, count, 13, Paper, Vector2.zero, new Vector2(width * .3f, 20f * s));
            Anchor(tally.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-pad, -pad), tally.rectTransform.sizeDelta);
            tally.alignment = TextAlignmentOptions.Right;
            tally.name = "Tray count";
            float top = pad + 28f * s;
            int perRow = Mathf.Clamp(Mathf.FloorToInt((width - 2f * pad + gap) / (180f * s + gap)), 1, slots.Count);
            float slotWidth = (width - 2f * pad - gap * (perRow - 1)) / perRow, slotHeight = 66f * s;
            var state = director != null ? director.Snapshot : null;
            for (int i = 0; i < slots.Count; i++)
            {
                int column = i % perRow, band = i / perRow;
                var slot = slots[i];
                bool filled = !string.IsNullOrEmpty(slot.Title);
                var ground = filled ? UiTheme.SurfaceRaised : new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .35f);
                var rect = Panel(FinalCaseSlotPrefix + (i + 1), tray, ground, 10);
                rect.GetComponent<Image>().raycastTarget = false;
                Anchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad + column * (slotWidth + gap), -(top + band * (slotHeight + gap))),
                    new Vector2(slotWidth, slotHeight));
                UiTheme.AddBorder(rect, 10, filled ? new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .5f)
                    : new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .5f));
                if (!filled)
                {
                    var empty = FixedText(rect, "Moment " + (i + 1), 13, new Color(UiTheme.Muted.r, UiTheme.Muted.g, UiTheme.Muted.b, .6f),
                        new Vector2(12f * s, -(slotHeight - 20f * s) * .5f), new Vector2(slotWidth - 24f * s, 20f * s));
                    empty.alignment = TextAlignmentOptions.Center;
                    continue;
                }
                float x = 12f * s;
                var actor = state?.Find(slot.SubjectId);
                if (actor != null)
                {
                    var face = HudPrimitives.RectPortrait(rect, "Slot photo", Portrait(slot.SubjectId), actor, new Vector2(40f, 50f) * s, 6);
                    Anchor(face, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f * s, -8f * s), face.sizeDelta);
                    x = 58f * s;
                }
                var when = FixedText(rect, slot.Week > 0 ? "WEEK " + slot.Week : "SEASON", 11, UiTheme.Heading, new Vector2(x, -10f * s),
                    new Vector2(slotWidth - x - 10f * s, 16f * s));
                when.characterSpacing = 3f;
                var name = FixedText(rect, slot.Title, 14, Paper, new Vector2(x, -28f * s), new Vector2(slotWidth - x - 10f * s, 20f * s));
                if (semibold != null) name.font = semibold;
                AutoSize(name, 10);
            }
            int rows = (slots.Count + perRow - 1) / perRow;
            var element = tray.gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = top + rows * slotHeight + (rows - 1) * gap + pad;
        }

        /// <summary>
        /// The lock's row: the gold control, and beside it what comes next and, until the choice is
        /// whole, what it still wants. The control keeps its caption word for word; the capitals are
        /// the font style's.
        /// </summary>
        public Button FinalCaseLock(string caption, Action action, bool ready, string next, string wanting)
        {
            if (content == null) return null;
            float s = FontScale, width = ContentWidth(), gap = 18f * s;
            bool beside = width >= 760f && s <= 1.05f;
            var row = new GameObject(FinalCaseLockRowName, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            HorizontalOrVerticalLayoutGroup layout = beside
                ? (HorizontalOrVerticalLayoutGroup)row.gameObject.AddComponent<HorizontalLayoutGroup>()
                : row.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = beside ? gap : 8f * s; layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            float buttonWidth = beside ? Mathf.Min(420f * s, width * .45f) : width;
            var button = GoldAction(row, caption, action, buttonWidth);
            button.interactable = ready;
            float notes = beside ? width - buttonWidth - gap : width;
            var column = Column("Lock notes", row, notes, beside ? 1f : 0f);
            column.GetComponent<VerticalLayoutGroup>().spacing = 2f * s;
            PushContent(column, notes - 4f);
            FlowText(next, 15, Paper);
            if (!string.IsNullOrEmpty(wanting)) Footnote(wanting);
            PopContent();
            return button;
        }

        /// <summary>A row in the gold the final is played for, with a crown before its caption and the chevron after it.</summary>
        private Button GoldAction(RectTransform parent, string caption, Action action, float width)
        {
            float s = FontScale, crown = 22f * s, mark = 18f * s;
            var rect = Chrome(caption, parent, UiTheme.Emphasis.Interactive);
            HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
            rect.GetComponent<Image>().color = UiTheme.Gold;
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 57f * s;
            element.preferredWidth = width;
            var ink = UiTheme.OnColor(UiTheme.Gold);
            var button = FinishButton(rect, caption, action, 16f + crown + 10f, 16f + mark + 10f);
            var label = rect.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.text == Localisation.Text(caption));
            if (label != null)
            {
                label.color = ink;
                label.fontStyle = FontStyles.UpperCase;
                var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
                if (semibold != null) label.font = semibold;
                label.characterSpacing = 1f;
            }
            var glyph = HudPrimitives.Glyph("Lock crown", rect, "crown", ink, Vector2.zero, crown);
            if (glyph != null)
            {
                var place = glyph.rectTransform;
                place.anchorMin = place.anchorMax = place.pivot = new Vector2(0f, .5f);
                place.anchoredPosition = new Vector2(16f, 0f);
            }
            HudPrimitives.Chevron(rect, ink, mark).anchoredPosition = new Vector2(-16f, 0f);
            return button;
        }
    }
}
