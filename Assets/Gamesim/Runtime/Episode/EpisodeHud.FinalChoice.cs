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
    /// The final Head of Household's choice as one page on the strategy stage (UI-UX-PASS-PLAN Q0):
    /// a card per finalist, side by side - the finalist's portrait card (the photo beside who they
    /// are and where the player stands with them, and the three public stat tiles), a row of the
    /// jurors' chips coloured by what the player can tell of each one's lean, and the bullets of
    /// what taking them means - and under each card the warning, what the choice does and its one
    /// control, "Take Maya · evict Taylor". It was two ledgers of label-and-value rows under cards
    /// the screen scrolled off the top (the show sweep's rows 12 and 37).
    ///
    /// <para>Laid for the frame in fixed boxes and fitted to the height the stage leaves it, as the
    /// finale page is: the record's line, the photo, the bullets' type and the chips step down a size,
    /// and at the last the line of what the choice does goes and the controls shorten, before the page
    /// grows, so it holds without a scroll at both text sizes on the 16:9 frame and the 4:3 canvas.
    /// The two columns are measured together, so the cards end level and the controls under them
    /// stand level whatever each card holds.</para>
    ///
    /// <para>An Enter the player did not aim commits nothing: the page opens with the keyboard on
    /// Close, never on a choice (<see cref="MayFocusOnItsOwn"/>), and a card is lit while its control
    /// holds the keyboard, so Enter only ever presses the lit control (<see cref="ChoiceLight"/>).</para>
    ///
    /// <para>Every word is the player's read. The chips are <see cref="FinalistRead.Lean"/>'s, which
    /// takes only what the player saw, was part of or was told - an alliance they know of, a
    /// showmance they know of, a standing they were told or overheard, the public record of who put
    /// whom on the block, and a ballot they know (<see cref="KnownBallots"/>) - never the jury
    /// model's own sentiment. A chip is filled for a lean the player saw or was part of and outlined
    /// for one they only heard; the word over its group says the lean as well as the colour does.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The page's parts, by the names a test finds them by.</summary>
        public const string FinalChoicePageName = "Final choice page", FinalistStatsName = "Finalist stats", JuryChipsName = "Jury chips",
            JurorChipPrefix = "Juror chip · ", JurorChipWordName = "Juror name", ChipGroupPrefix = "Chip group · ", ChipLegendName = "Chip legend",
            FinalistBulletName = "If you take", BulletsEyebrowName = "If you take eyebrow", JuryEyebrowName = "Jury read eyebrow",
            RelationshipWordName = "Relationship word", AgreementLineName = "Agreement", RecordLineName = "Record line", EitherWayName = "Either way",
            NoJuryName = "No jury";

        /// <summary>One juror's chip on a finalist's card: who, the name the chip carries, and what the player can tell of their lean toward that finalist.</summary>
        public struct FinalChoiceJuror
        {
            public string Id, Name, Lean, Certainty;
        }

        /// <summary>
        /// One finalist's column: who they are and the player's read of them, the jurors' chips in the
        /// order they stand, the bullets of what taking them means, and the words under the card - what
        /// the choice would break of the player's word, the warning, what it does - over its control.
        /// </summary>
        public struct FinalChoiceColumn
        {
            public ContestantState Actor;
            public FinalistRead.Finalist Read;
            public IList<FinalChoiceJuror> Jurors;
            /// <summary>The bullets' eyebrow, "IF YOU TAKE MAYA", by the name the page calls them (<see cref="FinalChoiceWords.BulletsEyebrow"/>).</summary>
            public string BulletsHeading;
            public IList<string> Bullets;
            public string Caption, Warning, Consequence, Breach;
            public Action Choose;
        }

        /// <summary>What the director hands the page: the line under the band, what breaks whichever finalist is taken, and the two columns.</summary>
        public sealed class FinalChoiceSpec
        {
            public string Line, EitherWay;
            public IList<FinalChoiceColumn> Columns;
        }

        // Sizes at the resting text size; each is multiplied by the text scale.
        private const float ChoicePad = 14f, ChoiceRowGap = 12f, ChoiceSectionGap = 10f, ChoicePhotoGap = 14f,
            ChoiceTileHeight = 48f, ChoiceTileGap = 8f, ChoiceControlHeight = 57f;
        /// <summary>The narrowest a stat tile stands beside the photo; narrower, the tiles take a row of their own under it.</summary>
        private const float ChoiceTileLeast = 112f;
        /// <summary>The photo's shape: the choice's card photo as it always was, 104 by 128.</summary>
        private const float ChoicePhotoAspect = 104f / 128f;

        /// <summary>
        /// One way the page can be laid, largest first: the photo's height, whether the archetype and
        /// the record's line are said, the bullets' type, the chips' height and type, the gaps between
        /// the page's parts (<see cref="Gaps"/> of their resting size), whether the line of what the
        /// choice does stands over the control, and the control's height.
        /// </summary>
        private readonly struct ChoiceTier
        {
            public readonly float Photo, Chip, Gaps, Control;
            public readonly int Bullet, ChipType;
            public readonly bool Archetype, Record, Consequence;

            public ChoiceTier(float photo, bool archetype, bool record, int bullet, float chip, int chipType, float gaps,
                bool consequence = true, float control = ChoiceControlHeight)
            {
                Photo = photo; Archetype = archetype; Record = record; Bullet = bullet; Chip = chip; ChipType = chipType; Gaps = gaps;
                Consequence = consequence; Control = control;
            }
        }

        /// <summary>
        /// The tiers, largest first. The last gives up the line of what the choice does - the last
        /// bullet already says who joins the jury - and stands the control 48 tall: headroom for the
        /// worst case Q0's review measured, a house of sixteen at the larger text on the 4:3 canvas
        /// with Final 2 deals between the player and both finalists and one with a juror - five and
        /// six bullets, a breach over each control and the either-way line over both.
        /// </summary>
        private static readonly ChoiceTier[] ChoiceTiers =
        {
            new ChoiceTier(128f, true, true, 14, 22f, 12, 1f),
            new ChoiceTier(112f, true, false, 14, 22f, 12, 1f),
            new ChoiceTier(96f, true, false, 13, 20f, 11, 1f),
            new ChoiceTier(84f, true, false, 12, 18f, 10, .7f),
            new ChoiceTier(72f, false, false, 12, 18f, 10, .6f),
            new ChoiceTier(64f, false, false, 12, 18f, 10, .5f, consequence: false, control: 48f),
        };

        /// <summary>Where a chip, or the word over its group, stands in its card's row of chips.</summary>
        private struct ChipSpot
        {
            public float X, Y, Width;
            /// <summary>The juror's index in the column's list, or -1 for the word over a group.</summary>
            public int Juror;
            public string Lean;
        }

        /// <summary>The page measured at one tier: every part's place, in the page's and the column's upper-left space.</summary>
        private sealed class ChoiceLayout
        {
            public ChoiceTier Tier;
            public float Width, Gap, ColumnWidth, Inner, PhotoW, PhotoH, Right, IdentityH, HeaderH, TilesX, TilesY, TilesW;
            public bool TilesBeside, Archetype, Record, Bullets;
            public float JuryY, ChipsY, ChipsH, BulletsY, BulletsListY, BulletsH, CardH;
            public float BreachY, BreachH, WarningY, WarningH, ConsequenceY, ConsequenceH, ControlY, ControlH, ColumnsH;
            public float HeadH, EitherY, EitherH, ColumnsY, LegendY, LegendH, Total;
            public List<ChipSpot>[] Chips;
            public List<float>[] BulletHeights;
        }

        /// <summary>
        /// Draws the final Head of Household's choice into the stage's column at the stage's whole
        /// width, fitted to the height the stage leaves it: the line under the band, what breaks either
        /// way, the two columns with the VS between them, and the chips' legend. Built in the order the
        /// keyboard should walk it: the first finalist's control, then the second's.
        /// </summary>
        public void FinalChoicePage(FinalChoiceSpec spec)
        {
            var state = director != null ? director.Snapshot : null;
            if (content == null || spec == null || state == null || spec.Columns == null || spec.Columns.Count != 2) return;
            UncapContent();
            float s = FontScale, width = ContentWidth(), room = FinalChoiceRoom();
            var page = new GameObject(FinalChoicePageName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            page.SetParent(content, false);
            // A probe to measure words on, as the finale page measures; gone once the page is laid.
            var probe = NewText(page, "", 12, Paper);
            ChoiceLayout layout = null;
            // The largest tier that holds in the room; when none does, the smallest, and the page grows
            // past the room rather than its words running out of their boxes.
            foreach (var tier in ChoiceTiers)
            {
                layout = ChoiceMeasure(spec, probe, width, tier);
                if (layout.Total <= room + .5f) break;
            }

            if (layout.HeadH > 0f)
            {
                var head = EndScreenKit.Box(ScreenHeadName, page, 0f, 0f, width, layout.HeadH);
                var line = FixedText(head, spec.Line, 14, UiTheme.Muted, Vector2.zero, new Vector2(width, layout.HeadH));
                line.name = "Head line";
                line.alignment = TextAlignmentOptions.Top;
            }
            if (layout.EitherH > 0f)
            {
                var either = FixedText(page, spec.EitherWay, 13, UiTheme.Warning, new Vector2(0f, -layout.EitherY), new Vector2(width, layout.EitherH));
                either.name = EitherWayName;
                either.alignment = TextAlignmentOptions.Top;
            }
            var row = EndScreenKit.Box(FinalistColumnsName, page, 0f, layout.ColumnsY, width, layout.ColumnsH);
            for (int i = 0; i < spec.Columns.Count; i++) FinalChoiceColumnIn(row, spec.Columns[i], layout, i, probe);
            // The columns' lights answer to one another, so one card is lit at a time.
            var lights = row.GetComponentsInChildren<ChoiceLight>(true);
            foreach (var light in lights) light.Group = lights;
            // Two people and one choice between them: the VS in the gap, level with their faces.
            Versus(row, layout.Gap, ChoicePad * s + layout.PhotoH * .5f);
            var legend = FixedText(page, FinalChoiceWords.Legend, 12, UiTheme.Muted, new Vector2(0f, -layout.LegendY), new Vector2(width, layout.LegendH));
            legend.name = ChipLegendName;
            legend.alignment = TextAlignmentOptions.Top;

            probe.gameObject.SetActive(false);
            Destroy(probe.gameObject);
            var size = page.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = layout.Total;
        }

        // ------------------------------------------------------------ measuring

        /// <summary>
        /// The height the stage's scroll gives the page: the finale's room for a step with nothing
        /// pinned under it, less whatever the column already holds over the page - a story beat waiting
        /// on the player, which comes before every decision.
        /// </summary>
        private float FinalChoiceRoom()
        {
            float room = FinaleRoom();
            if (content == null || content.childCount == 0) return room;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var column = content.GetComponent<VerticalLayoutGroup>();
            float padding = column != null ? column.padding.top + column.padding.bottom : 0f;
            float spacing = column != null ? column.spacing : 0f;
            return Mathf.Max(0f, room - (LayoutUtility.GetPreferredHeight(content) - padding + spacing));
        }

        /// <summary>A bullet as the card prints it.</summary>
        private static string ChoiceBullet(string line) => "• " + line;

        /// <summary>
        /// The page at one tier: the columns' width, the card's parts top to bottom - the photo row,
        /// the tiles beside the photo where they stand at least <see cref="ChoiceTileLeast"/> wide and
        /// under it otherwise, the jury read, the bullets - and under the card the breach, the warning,
        /// what the choice does and the control, each the taller of the two columns', so the two
        /// controls stand level; then the page's head line, its either-way line and the legend.
        /// </summary>
        private ChoiceLayout ChoiceMeasure(FinalChoiceSpec spec, TMP_Text probe, float width, ChoiceTier tier)
        {
            float s = FontScale, pad = ChoicePad * s, section = ChoiceSectionGap * s * tier.Gaps, between = ChoiceRowGap * s * tier.Gaps;
            var columns = spec.Columns;
            var layout = new ChoiceLayout { Tier = tier, Width = width, Gap = VersusGap * s };
            layout.ColumnWidth = Mathf.Floor((width - layout.Gap) * .5f);
            layout.Inner = layout.ColumnWidth - 2f * pad;
            layout.PhotoH = Mathf.Round(tier.Photo * s);
            layout.PhotoW = Mathf.Round(layout.PhotoH * ChoicePhotoAspect);
            layout.Right = Mathf.Max(80f * s, layout.Inner - layout.PhotoW - ChoicePhotoGap * s);

            // Who they are, beside the photo: the name, the archetype, where the player stands and any
            // agreement on one line, and the record's line.
            layout.Archetype = tier.Archetype && columns.Any(column => column.Actor != null && !string.IsNullOrEmpty(column.Actor.archetype));
            layout.Record = tier.Record && columns.Any(column => column.Read != null && !string.IsNullOrEmpty(column.Read.standingLine));
            float identity = BoardLine(20) + 2f * s + BoardLine(13);
            if (layout.Archetype) identity += 2f * s + BoardLine(13);
            if (layout.Record) identity += 2f * s + BoardLine(12);
            layout.IdentityH = identity;
            float tile = ChoiceTileHeight * s, tileGap = ChoiceTileGap * s;
            layout.TilesBeside = (layout.Right - 2f * tileGap) / 3f >= ChoiceTileLeast * s;
            float y = pad;
            if (layout.TilesBeside)
            {
                // The tiles under who they are, their foot on the photo's.
                layout.HeaderH = Mathf.Max(layout.PhotoH, identity + 8f * s + tile);
                layout.TilesX = pad + layout.PhotoW + ChoicePhotoGap * s;
                layout.TilesW = layout.Right;
                layout.TilesY = pad + layout.HeaderH - tile;
                y += layout.HeaderH;
            }
            else
            {
                layout.HeaderH = Mathf.Max(layout.PhotoH, identity);
                y += layout.HeaderH + 8f * s;
                layout.TilesX = pad;
                layout.TilesW = layout.Inner;
                layout.TilesY = y;
                y += tile;
            }

            // The jury read: its eyebrow, and the chips in rows - or a line saying nobody is on it yet.
            layout.JuryY = y + section;
            layout.ChipsY = layout.JuryY + BoardLine(12) + 4f * s;
            layout.Chips = new List<ChipSpot>[columns.Count];
            for (int i = 0; i < columns.Count; i++)
            {
                layout.Chips[i] = FlowChips(probe, columns[i].Jurors, layout.Inner, tier, out float height);
                layout.ChipsH = Mathf.Max(layout.ChipsH, height);
            }
            if (layout.ChipsH <= 0f) layout.ChipsH = BoardLine(12);
            y = layout.ChipsY + layout.ChipsH;

            // What taking them means, under its eyebrow.
            layout.Bullets = columns.Any(column => column.Bullets != null && column.Bullets.Count > 0);
            layout.BulletHeights = new List<float>[columns.Count];
            if (layout.Bullets)
            {
                layout.BulletsY = y + section;
                layout.BulletsListY = layout.BulletsY + 20f * s + 4f * s;
                for (int i = 0; i < columns.Count; i++)
                {
                    var heights = new List<float>();
                    float total = 0f;
                    var bullets = columns[i].Bullets ?? new List<string>();
                    for (int b = 0; b < bullets.Count; b++)
                    {
                        float height = Mathf.Max(BoardLine(tier.Bullet), BoardMeasure(probe, ChoiceBullet(bullets[b]), tier.Bullet, layout.Inner));
                        heights.Add(height);
                        total += height + (b > 0 ? 2f * s : 0f);
                    }
                    layout.BulletHeights[i] = heights;
                    layout.BulletsH = Mathf.Max(layout.BulletsH, total);
                }
                y = layout.BulletsListY + layout.BulletsH;
            }
            layout.CardH = y + pad;

            // Under the card: what it breaks of the player's word, the warning, what it does, the control.
            y = layout.CardH + section;
            foreach (var column in columns)
                if (!string.IsNullOrEmpty(column.Breach))
                    layout.BreachH = Mathf.Max(layout.BreachH, Mathf.Max(BoardLine(13), BoardMeasure(probe, column.Breach, 13, layout.ColumnWidth)));
            if (layout.BreachH > 0f) { layout.BreachY = y; y += layout.BreachH + 6f * s; }
            layout.WarningY = y;
            layout.WarningH = columns.Any(column => !string.IsNullOrEmpty(column.Warning)) ? BoardLine(13) : 0f;
            if (layout.WarningH > 0f) y += layout.WarningH + 4f * s;
            // What the choice does, where the tier keeps it; the last tier lets the last bullet say it.
            if (tier.Consequence)
                foreach (var column in columns)
                    if (!string.IsNullOrEmpty(column.Consequence))
                        layout.ConsequenceH = Mathf.Max(layout.ConsequenceH, Mathf.Max(BoardLine(13), BoardMeasure(probe, column.Consequence, 13, layout.ColumnWidth)));
            layout.ConsequenceY = y;
            if (layout.ConsequenceH > 0f) y += layout.ConsequenceH + 8f * s;
            layout.ControlY = y;
            layout.ControlH = tier.Control * s;
            layout.ColumnsH = y + layout.ControlH;

            // The page: the line under the band, what breaks either way, the columns, the legend.
            layout.HeadH = string.IsNullOrEmpty(spec.Line) ? 0f : Mathf.Max(BoardLine(14), BoardMeasure(probe, spec.Line, 14, width));
            y = layout.HeadH > 0f ? layout.HeadH + between : 0f;
            if (!string.IsNullOrEmpty(spec.EitherWay))
            {
                layout.EitherY = y;
                layout.EitherH = Mathf.Max(BoardLine(13), BoardMeasure(probe, spec.EitherWay, 13, width));
                y += layout.EitherH + 8f * s;
            }
            layout.ColumnsY = y;
            y += layout.ColumnsH;
            layout.LegendY = y + between;
            layout.LegendH = Mathf.Max(BoardLine(12), BoardMeasure(probe, FinalChoiceWords.Legend, 12, width));
            layout.Total = layout.LegendY + layout.LegendH;
            return layout;
        }

        /// <summary>A chip's width: its name at the chip's type and weight between its insets, never wider than the row.</summary>
        private float ChipWidth(TMP_Text probe, string name, ChoiceTier tier, float width, TMP_FontAsset weight) =>
            Mathf.Min(width, BoardWidthOf(probe, name, tier.ChipType, weight) + 18f * FontScale + 2f);

        /// <summary>
        /// The chips in rows across <paramref name="width"/>: close first, then a grudge, then no read,
        /// each group led by its word, which keeps to its first chip's row; a row full, the next one
        /// down. Returns where each word and chip stands and, through <paramref name="height"/>, the
        /// rows' height - nothing for nobody.
        /// </summary>
        private List<ChipSpot> FlowChips(TMP_Text probe, IList<FinalChoiceJuror> jurors, float width, ChoiceTier tier, out float height)
        {
            float s = FontScale, chip = tier.Chip * s, gap = 6f * s, wordGap = 5f * s, groupGap = 12f * s, rowGap = 6f * s;
            int wordType = Mathf.Max(9, tier.ChipType - 1);
            var spots = new List<ChipSpot>();
            height = 0f;
            if (jurors == null || jurors.Count == 0) return spots;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            float x = 0f, y = 0f;
            bool any = false;
            foreach (var lean in FinalChoiceWords.LeanOrder)
            {
                int rank = FinalChoiceWords.LeanRank(lean);
                var members = new List<int>();
                for (int i = 0; i < jurors.Count; i++)
                    if (FinalChoiceWords.LeanRank(jurors[i].Lean) == rank) members.Add(i);
                if (members.Count == 0) continue;
                float word = Mathf.Min(width, BoardWidthOf(probe, FinalChoiceWords.GroupWord(lean), wordType, semibold) + 2f);
                float first = ChipWidth(probe, jurors[members[0]].Name, tier, width, medium);
                float start = any ? x + groupGap : 0f;
                // The word goes with its group's first chip: both start a new row together.
                if (any && start + word + wordGap + first > width) { y += chip + rowGap; start = 0f; }
                spots.Add(new ChipSpot { X = start, Y = y, Width = word, Juror = -1, Lean = lean });
                x = start + word + wordGap;
                any = true;
                for (int k = 0; k < members.Count; k++)
                {
                    float w = k == 0 ? first : ChipWidth(probe, jurors[members[k]].Name, tier, width, medium);
                    if (x > 0f && x + w > width) { y += chip + rowGap; x = 0f; }
                    w = Mathf.Min(w, width - x);
                    spots.Add(new ChipSpot { X = x, Y = y, Width = w, Juror = members[k], Lean = lean });
                    x += w + gap;
                }
                x -= gap;
            }
            height = y + chip;
            return spots;
        }

        // ------------------------------------------------------------ a column

        /// <summary>
        /// One finalist's column: the card - the photo beside who they are, the tiles, the jury read
        /// and the bullets - and under it the breach, the warning, what the choice does and the control.
        /// </summary>
        private void FinalChoiceColumnIn(RectTransform row, FinalChoiceColumn column, ChoiceLayout layout, int index, TMP_Text probe)
        {
            float s = FontScale, pad = ChoicePad * s;
            var actor = column.Actor;
            var read = column.Read;
            var box = EndScreenKit.Box("Finalist column · " + actor.name, row, index * (layout.ColumnWidth + layout.Gap), 0f, layout.ColumnWidth, layout.ColumnsH);
            var card = EndScreenKit.Box(FinalistCardPrefix + actor.name, box, 0f, 0f, layout.ColumnWidth, layout.CardH);
            // The pack's finalist card, as the finale's final two wear it, and the gold edge the
            // column's control lights while it is the one a press would take.
            EndScreenKit.Skin(card, PackArt.Pack9SeasonFinaleFinalistCardFill, PackArt.Pack9SeasonFinaleFinalistCardEdge, 14f * s,
                new Color(Surface.r, Surface.g, Surface.b, .94f), new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .5f),
                null, UiTheme.CardFill);
            var chosen = ChosenEdge(card);

            // The portrait card: the photo, and beside it who they are and where the player stands.
            var photo = HudPrimitives.RectPortrait(card, "Photo", Portrait(actor.id), actor, new Vector2(layout.PhotoW, layout.PhotoH), 10);
            EndScreenKit.Place(photo, pad, pad, layout.PhotoW, layout.PhotoH);
            ChoiceIdentity(card, actor, read, layout, probe);

            // The three numbers the house saw, as tiles (the juror's case's own).
            float tile = ChoiceTileHeight * s;
            var stats = EndScreenKit.Box(FinalistStatsName, card, layout.TilesX, layout.TilesY, layout.TilesW, tile);
            if (read != null) StatTiles(stats, read, actor, layout.TilesW, tile, true);

            // The jury read: the jurors as chips, grouped and coloured by the lean the player can tell.
            int jurors = column.Jurors != null ? column.Jurors.Count : 0;
            var juryEyebrow = FixedText(card, FinalChoiceWords.JuryEyebrow(jurors), 12, Accent, new Vector2(pad, -layout.JuryY), new Vector2(layout.Inner, BoardLine(12)));
            juryEyebrow.name = JuryEyebrowName;
            juryEyebrow.characterSpacing = 4f;
            juryEyebrow.alignment = TextAlignmentOptions.MidlineLeft;
            OneLine(juryEyebrow, 8f);
            if (jurors == 0)
            {
                var none = FixedText(card, FinalChoiceWords.NoJury, 12, UiTheme.Muted, new Vector2(pad, -layout.ChipsY), new Vector2(layout.Inner, BoardLine(12)));
                none.name = NoJuryName;
            }
            else
            {
                var chips = EndScreenKit.Box(JuryChipsName, card, pad, layout.ChipsY, layout.Inner, layout.ChipsH);
                float chip = layout.Tier.Chip * s;
                int wordType = Mathf.Max(9, layout.Tier.ChipType - 1);
                var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
                foreach (var spot in layout.Chips[index])
                {
                    if (spot.Juror >= 0) { JurorChip(chips, column.Jurors[spot.Juror], spot.X, spot.Y, spot.Width, chip, layout.Tier.ChipType); continue; }
                    string word = FinalChoiceWords.GroupWord(spot.Lean);
                    var label = FixedText(chips, word, wordType, LeanTint(spot.Lean), new Vector2(spot.X, -spot.Y), new Vector2(spot.Width, chip));
                    label.name = ChipGroupPrefix + word;
                    if (semibold != null) label.font = semibold;
                    label.alignment = TextAlignmentOptions.MidlineLeft;
                    OneLine(label, 8f);
                }
            }

            // What taking them means: the read's own bullets.
            if (layout.Bullets && column.Bullets != null && column.Bullets.Count > 0)
            {
                float side = 16f * s;
                float x = Mark("Eyebrow mark", card, PackArt.IconTrophy, null, UiTheme.Gold, new Vector2(pad, -(layout.BulletsY + 2f * s)), side) != null ? side + 8f * s : 0f;
                var eyebrow = FixedText(card, column.BulletsHeading ?? FinalChoiceWords.BulletsEyebrow(actor.name, null), 12, Accent,
                    new Vector2(pad + x, -layout.BulletsY), new Vector2(Mathf.Max(10f, layout.Inner - x), 20f * s));
                eyebrow.name = BulletsEyebrowName;
                eyebrow.characterSpacing = 4f;
                eyebrow.alignment = TextAlignmentOptions.MidlineLeft;
                OneLine(eyebrow, 8f);
                float y = layout.BulletsListY;
                var heights = layout.BulletHeights[index];
                for (int b = 0; b < column.Bullets.Count; b++)
                {
                    var bullet = FixedText(card, ChoiceBullet(column.Bullets[b]), layout.Tier.Bullet, Paper, new Vector2(pad, -y), new Vector2(layout.Inner, heights[b]));
                    bullet.name = FinalistBulletName;
                    y += heights[b] + 2f * s;
                }
            }

            // Under the card, level with the other column's: what this choice alone would break of the
            // player's word (ACTIONS-DEALS-ALLIANCES-PLAN V1), the warning, what the choice does.
            if (!string.IsNullOrEmpty(column.Breach))
            {
                var breach = FixedText(box, column.Breach, 13, UiTheme.Warning, new Vector2(0f, -layout.BreachY), new Vector2(layout.ColumnWidth, layout.BreachH));
                breach.name = BreachWarningName;
                breach.alignment = TextAlignmentOptions.Top;
            }
            if (!string.IsNullOrEmpty(column.Warning)) ChoiceWarning(box, column.Warning, layout, probe);
            if (layout.ConsequenceH > 0f && !string.IsNullOrEmpty(column.Consequence))
            {
                var line = FixedText(box, column.Consequence, 13, UiTheme.Muted, new Vector2(0f, -layout.ConsequenceY), new Vector2(layout.ColumnWidth, layout.ConsequenceH));
                line.name = FinalistConsequenceName;
                line.alignment = TextAlignmentOptions.Top;
            }
            // The column's one control, its caption the choice's two halves.
            if (!string.IsNullOrEmpty(column.Caption) && column.Choose != null)
                FinalChoiceControl(box, column.Caption, column.Choose, layout.ControlY, layout.ColumnWidth, layout.ControlH, chosen);
        }

        /// <summary>
        /// Who they are, beside the photo: the name, the archetype in the heading blue, where the player
        /// stands with them in the relationship web's word and colour with any Final 2 agreement after
        /// it, and the record's line (<see cref="FinalistRead.RelationshipLine"/>) where the tier says it.
        /// Each on one line in a box 1.32 times its type, shrinking toward a floor before an ellipsis.
        /// </summary>
        private void ChoiceIdentity(RectTransform card, ContestantState actor, FinalistRead.Finalist read, ChoiceLayout layout, TMP_Text probe)
        {
            float s = FontScale, x = ChoicePad * s + layout.PhotoW + ChoicePhotoGap * s, y = ChoicePad * s, width = layout.Right;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            var name = FixedText(card, actor.name, 20, Paper, new Vector2(x, -y), new Vector2(width, BoardLine(20)));
            name.name = "Name";
            if (semibold != null) name.font = semibold;
            OneLine(name, 12f);
            name.overflowMode = TextOverflowModes.Ellipsis;
            y += BoardLine(20) + 2f * s;
            if (layout.Archetype)
            {
                if (!string.IsNullOrEmpty(actor.archetype))
                {
                    var archetype = FixedText(card, actor.archetype, 13, UiTheme.Heading, new Vector2(x, -y), new Vector2(width, BoardLine(13)));
                    archetype.name = "Archetype";
                    archetype.fontStyle = FontStyles.Italic;
                    OneLine(archetype, 9f);
                    archetype.overflowMode = TextOverflowModes.Ellipsis;
                }
                y += BoardLine(13) + 2f * s;
            }
            // The player's own standing - what they feel, never what the finalist does - and the
            // agreement between the two, which the player is party to, as the juror's case says them.
            string standing = read != null && read.relationship != null ? read.relationship.value : FinalistRead.StandingWord(director.Snapshot, actor.id);
            var tint = StandingTint(standing);
            float standingWidth = Mathf.Min(width * .5f, BoardWidthOf(probe, standing, 13, semibold) + 2f);
            var word = FixedText(card, standing, 13, tint == UiTheme.Muted ? Paper : tint, new Vector2(x, -y), new Vector2(standingWidth, BoardLine(13)));
            word.name = RelationshipWordName;
            if (semibold != null) word.font = semibold;
            OneLine(word, 9f);
            string agreement = read == null || read.agreement == null || read.agreement.certainty == FinalistRead.Unknown
                ? "No Final 2 agreement" : read.agreement.value;
            float after = standingWidth + 6f * s;
            var deal = FixedText(card, "· " + agreement, 13, UiTheme.Muted, new Vector2(x + after, -y), new Vector2(Mathf.Max(10f, width - after), BoardLine(13)));
            deal.name = AgreementLineName;
            OneLine(deal, 9f);
            deal.overflowMode = TextOverflowModes.Ellipsis;
            y += BoardLine(13) + 2f * s;
            if (layout.Record && read != null && !string.IsNullOrEmpty(read.standingLine))
            {
                var record = FixedText(card, read.standingLine, 12, UiTheme.Muted, new Vector2(x, -y), new Vector2(width, BoardLine(12)));
                record.name = RecordLineName;
                OneLine(record, 9f);
                record.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        /// <summary>The colour a lean's chips and group word wear: close in the allied green, a grudge in the danger red, no read muted.</summary>
        private static Color LeanTint(string lean) =>
            lean == FinalistRead.Support ? UiTheme.Allied : lean == FinalistRead.Bitter ? UiTheme.Danger : UiTheme.Muted;

        /// <summary>
        /// One juror's chip: the pack's pill in the lean's colour - filled for a lean the player saw or
        /// was part of, a wash with an edge for one they only heard and for no read - and the juror's
        /// name on it. Not a control: the column's only control is its caption's.
        /// </summary>
        private void JurorChip(RectTransform parent, FinalChoiceJuror juror, float x, float y, float width, float height, int type)
        {
            float s = FontScale;
            var tint = LeanTint(juror.Lean);
            bool filled = FinalChoiceWords.Filled(juror.Lean, juror.Certainty);
            var chip = EndScreenKit.Box(JurorChipPrefix + juror.Id, parent, x, y, width, height);
            var ground = chip.gameObject.AddComponent<Image>();
            ground.raycastTarget = false;
            var colour = filled ? tint : new Color(tint.r, tint.g, tint.b, .18f);
            int radius = Mathf.Clamp(Mathf.RoundToInt(height * .5f) - 1, 4, 31);
            if (!UiTheme.PackSliced(ground, PackArt.Pack9SharedPillFill, height * .5f, colour)) UiTheme.Style(ground, colour, radius);
            if (!filled)
            {
                var edgeTint = new Color(tint.r, tint.g, tint.b, .8f);
                if (UiTheme.Pack(PackArt.Pack9SharedPillEdge) != null)
                {
                    var edge = new GameObject("Edge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    edge.rectTransform.SetParent(chip, false);
                    edge.rectTransform.anchorMin = Vector2.zero; edge.rectTransform.anchorMax = Vector2.one;
                    edge.rectTransform.offsetMin = Vector2.zero; edge.rectTransform.offsetMax = Vector2.zero;
                    edge.raycastTarget = false;
                    UiTheme.PackSliced(edge, PackArt.Pack9SharedPillEdge, height * .5f, edgeTint);
                }
                else UiTheme.AddBorder(chip, radius, edgeTint);
            }
            var word = NewText(chip, juror.Name, type, filled ? UiTheme.OnColor(tint) : tint);
            word.name = JurorChipWordName;
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            if (medium != null) word.font = medium;
            word.alignment = TextAlignmentOptions.Center;
            word.textWrappingMode = TextWrappingModes.NoWrap;
            word.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(word.rectTransform, 9f * s, 1f, 9f * s, 1f);
            AutoSize(word, 8f);
        }

        /// <summary>The warning under a card, with the kit's warning mark before it, the two centred together.</summary>
        private void ChoiceWarning(RectTransform column, string words, ChoiceLayout layout, TMP_Text probe)
        {
            float s = FontScale, side = 15f * s, gap = 6f * s;
            float wordWidth = Mathf.Min(layout.ColumnWidth - side - gap, BoardWidthOf(probe, words, 13) + 4f);
            float left = Mathf.Max(0f, (layout.ColumnWidth - side - gap - wordWidth) * .5f);
            var mark = Mark("Warning mark", column, PackArt.KitIconWarning, null, UiTheme.Warning,
                new Vector2(left, -(layout.WarningY + (layout.WarningH - side) * .5f)), side);
            float x = mark != null ? left + side + gap : Mathf.Max(0f, (layout.ColumnWidth - wordWidth) * .5f);
            var warning = FixedText(column, words, 13, UiTheme.Warning, new Vector2(x, -layout.WarningY), new Vector2(wordWidth, layout.WarningH));
            warning.name = FinalistWarningName;
            warning.alignment = TextAlignmentOptions.MidlineLeft;
            OneLine(warning, 9f);
        }

        /// <summary>
        /// A column's control: the pack's action tile under the HUD's interactive chrome, the selection
        /// ring before the caption and the chevron after it. The caption is the control's name and its
        /// first label, word for word - every lookup by caption finds it - kept to one line and shrunk
        /// to fit rather than cut. The ring and the card's gold edge light while it is the control a
        /// press would take (<see cref="ChoiceLight"/>); one press commits.
        /// </summary>
        private Button FinalChoiceControl(RectTransform column, string caption, Action choose, float y, float width, float height, GameObject edge)
        {
            float s = FontScale, mark = 18f * s;
            var rect = Chrome(caption, column, UiTheme.Emphasis.Interactive);
            HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
            EndScreenKit.Place(rect, 0f, y, width, height);
            if (UiTheme.Pack(PackArt.Pack9SeasonFinaleActionTileFill) != null)
                EndScreenKit.Skin(rect, PackArt.Pack9SeasonFinaleActionTileFill, PackArt.Pack9SeasonFinaleActionTileEdge, 12f * s,
                    new Color(Surface.r, Surface.g, Surface.b, .94f), UiTheme.Edge(UiTheme.Emphasis.Interactive), null, UiTheme.Surface);
            var button = FinishButton(rect, caption, choose, 16f, 16f + mark + 10f);
            var label = button.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.transform.parent == rect);
            if (label != null)
            {
                var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
                if (semibold != null) label.font = semibold;
                OneLine(label, 12f);
            }
            HudPrimitives.Chevron(rect, UiTheme.Hairline, mark).anchoredPosition = new Vector2(-16f, 0f);
            SelectionMark(button, edge);
            return button;
        }
    }
}
