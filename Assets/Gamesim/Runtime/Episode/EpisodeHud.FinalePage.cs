using System;
using System.Collections.Generic;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The season's finale as one page on the strategy stage (UI-UX-PASS-PLAN F0, the owner's mockup
    /// 92): the winner's card, the final two with the jury's count between them, the jury as a ring
    /// of faces each in the colour of the finalist they chose, the season's highlights as six real
    /// counts, and the ways on as tiles. It was a column of sentences and six rows that scrolled.
    ///
    /// <para>Laid for the frame in fixed-size type and fitted to the height the stage leaves it, so
    /// the closed page holds without a scroll on the 16:9 frame at both text sizes with a jury of up
    /// to fourteen; the cards' row takes what the winner's portrait and the jury's faces want and no
    /// more than the room, and the faces step down a size before the page grows. Every word is the
    /// committed record's: the ballots as the engine wrote them, public at the finale (decision 7),
    /// the Game Sense report, the ledger's counts. Every caption is kept word for word and drawn
    /// once. A juror's face is a control captioned with their name, and a press shows the reason
    /// they gave as the jury card's foot line; the whole of every reason stays in the season report
    /// and the jury's questions.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The page's parts, by the names a test finds them by.</summary>
        public const string FinalePageName = "Finale page", FinaleWinnerCardName = "WINNER", FinaleFinalTwoCardName = "FINAL TWO",
            FinaleJuryCardName = "JURY", FinaleJuryFacesName = "Jury faces", FinaleHighlightsName = "Season highlights",
            FinaleWaysName = "Finale ways on", FinaleSecondRowName = "Finale second row", FinaleWinnerLineName = "Winner line",
            FinaleGameSenseName = "Game sense line", FinaleJurorNameName = "Juror name", FinaleJurorChipName = "Vote",
            FinaleJurorChipWordName = "Vote for", FinaleReasonEyebrowName = "Jury reason eyebrow", FinaleReasonLineName = "Jury reason",
            FinaleReasonHintName = "Jury reason hint", FinaleCountEyebrowName = "Count eyebrow", FinaleWayLineName = "Way line",
            FinaleFinalistPrefix = "Finalist · ", FinaleWinnerTallyName = "Winner tally", FinaleRunnerUpTallyName = "Runner-up tally";

        /// <summary>The jury card's heading, word for word: the career test reads it on the panel.</summary>
        public const string FinaleJuryHeading = "HOW THE JURY VOTED";
        public const string FinaleHighlightsHeading = "SEASON HIGHLIGHTS";
        /// <summary>The jury card's foot while no juror is pressed.</summary>
        public const string FinaleReasonHint = "Select a juror to read the reason they gave.";
        /// <summary>The finalists' roles under their names on the FINAL TWO card.</summary>
        public const string FinaleWinnerRole = "Winner", FinaleRunnerUpRole = "Runner-Up";

        /// <summary>One juror's face: who, the caption their control carries, whom they chose, and the line a press shows.</summary>
        public struct FinaleJuror
        {
            public string Id, Caption, FinalistId, Reason;
            public bool IsPlayer;
        }

        /// <summary>One of the highlights' tiles: the caption it is found by, its count, and its mark.</summary>
        public struct FinaleStat
        {
            public string Caption, Value, Icon, Fallback;
            public Color Tint;
        }

        /// <summary>One way on: its caption, the line under it, its mark, and what it does.</summary>
        public struct FinaleWay
        {
            public string Caption, Line, Icon, Fallback;
            public Action Choose;
        }

        /// <summary>What the director hands the page: the words the record decides, its view state, and what each control does.</summary>
        public sealed class FinalePageSpec
        {
            public string WinnerId, RunnerUpId, WinnerLine, GameSenseFirst, GameSenseSecond;
            public IList<FinaleJuror> Jurors;
            public int ForWinner, ForRunnerUp;
            /// <summary>The juror whose reason is the jury card's foot line, or null for the hint.</summary>
            public string PressedJurorId;
            public Action<string> PressJuror;
            public IList<FinaleStat> Highlights;
            public IList<FinaleWay> Ways, SecondRow;
        }

        // Sizes at the resting text size; each is multiplied by the text scale unless it is a picture.
        private const float FinaleGap = 12f, FinalePad = 14f, FinaleRowGap = 10f, FinaleWell = 6f;
        /// <summary>The winner's portrait: as tall as this, and never shorter than the least when the row is squeezed.</summary>
        private const float FinalePortrait = 180f, FinalePortraitLeast = 120f;
        private const float FinaleHighlightTile = 60f, FinaleWayHeight = 64f, FinaleThinHeight = 46f;
        /// <summary>The jury's face sizes, tried largest first: a jury past eight starts smaller.</summary>
        private static readonly float[] FinaleFacesSmall = { 72f, 64f, 56f, 48f, 40f, 36f }, FinaleFacesLarge = { 56f, 48f, 44f, 40f, 36f };
        private static readonly Color FinaleLightGold = new Color(1f, .9f, .58f);

        /// <summary>
        /// Draws the finale's page into the stage's column at the stage's whole width, fitted to the
        /// height the stage leaves it. Built in the order the keyboard should walk it - the ways on,
        /// then the jury's faces - and laid top to bottom: the cards, the highlights, the ways.
        /// </summary>
        public void FinalePage(FinalePageSpec spec)
        {
            var state = director != null ? director.Snapshot : null;
            if (content == null || spec == null || state == null) return;
            UncapContent();
            float s = FontScale, width = ContentWidth(), gap = FinaleGap * s;
            float room = FinaleRoom();
            float highlights = FinaleHighlightsHeight(), ways = FinaleWayHeight * s, thin = FinaleThinHeight * s;
            float rest = gap + highlights + gap + ways + FinaleRowGap * s + thin;
            int jurors = spec.Jurors != null ? spec.Jurors.Count : 0;
            var faces = jurors > 8 ? FinaleFacesLarge : FinaleFacesSmall;
            // The widths: the jury's card takes its share of the row, or what two rows of the smallest
            // faces need on a narrow frame (the 4:3 batch canvas with a jury of fourteen), as far as
            // the other two cards can give at their least; the winner's card and the final two's
            // divide the rest as the mockup does.
            float remainder = width - 2f * gap, smallest = faces[faces.Length - 1];
            float twoRows = 2f * FinalePad * s + Mathf.CeilToInt(jurors / 2f) * (smallest + 6f * s + 10f * s) - 10f * s;
            float juryWidth = Mathf.Max(Mathf.Floor(remainder * .38f), Mathf.Ceil(twoRows));
            juryWidth = Mathf.Min(juryWidth, Mathf.Max(0f, remainder - FinaleWinnerLeastWidth() - FinaleTwoLeastWidth()));
            float others = remainder - juryWidth;
            float winnerWidth = Mathf.Floor(others * (.32f / .62f)), twoWidth = others - winnerWidth;
            // The cards' row: what the winner's portrait and the jury's faces want at their largest,
            // held to the room the rest leaves, and never under the least portrait.
            float least = FinaleWinnerFixed() + FinalePortraitLeast;
            float most = Mathf.Max(least, room - rest);
            float wanted = Mathf.Max(FinaleWinnerFixed() + FinalePortrait, FinaleJuryFixed() + FinaleJuryArea(jurors, juryWidth, faces[0]));
            float cards = Mathf.Clamp(wanted, least, most);
            // The largest face that holds in the card; when none does, the smallest, and the row grows
            // past the room rather than the faces running out of their card.
            float face = 0f;
            foreach (float candidate in faces)
                if (FinaleJuryArea(jurors, juryWidth, candidate) <= cards - FinaleJuryFixed() + .5f) { face = candidate; break; }
            if (face <= 0f)
            {
                face = faces[faces.Length - 1];
                cards = Mathf.Max(cards, FinaleJuryFixed() + FinaleJuryArea(jurors, juryWidth, face));
            }

            var board = new GameObject(FinalePageName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            board.SetParent(content, false);
            float y = cards + gap + highlights + gap;
            FinaleWayRow(board, FinaleWaysName, y, width, ways, spec.Ways, true);
            y += ways + FinaleRowGap * s;
            FinaleWayRow(board, FinaleSecondRowName, y, width, thin, spec.SecondRow, false);
            float total = y + thin;
            FinaleWinnerCard(board, 0f, 0f, winnerWidth, cards, spec, state);
            FinaleFinalTwoCard(board, winnerWidth + gap, 0f, twoWidth, cards, spec, state);
            FinaleJuryCard(board, winnerWidth + gap + twoWidth + gap, 0f, juryWidth, cards, face, spec, state);
            FinaleHighlights(board, cards + gap, width, highlights, spec.Highlights);
            var size = board.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = total;
        }

        // ------------------------------------------------------------ measuring

        /// <summary>
        /// The height the stage's scroll gives one step with no footer pinned under it: the frame
        /// less the phase band, any header rows, the scroll's own foot inset and the column's padding.
        /// </summary>
        private float FinaleRoom()
        {
            if (modal == null || content == null) return 0f;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            float padding = layout != null ? layout.padding.top + layout.padding.bottom : 24f;
            return Mathf.Max(0f, modal.sizeDelta.y - StrategyBandFoot - strategyHeaderUsed - 22f - padding - 2f);
        }

        /// <summary>The winner's card without its portrait: the crown's lift, 'WINNER', the plate, two Game Sense lines and the foot line.</summary>
        private float FinaleWinnerFixed()
        {
            float s = FontScale;
            return FinalePad * s + 10f * s + 2f * FinaleWell * s + 8f * s + BoardLine(14) + 4f * s + 40f * s + 6f * s
                + 2f * BoardLine(13) + 4f * s + BoardLine(12) + FinalePad * s;
        }

        /// <summary>The FINAL TWO card without its portraits: the heading, each finalist's name and role row, the count's eyebrow and the bar.</summary>
        private float FinaleTwoFixed()
        {
            float s = FontScale;
            return FinalePad * s + BoardLine(14) + 8f * s + 8f * s + 6f * s + BoardLine(15) + 4f * s + 28f * s + 8f * s
                + 8f * s + BoardLine(11) + 4f * s + 22f * s + FinalePad * s;
        }

        /// <summary>The narrowest the winner's card runs: its portrait at full width in its well, and the padding.</summary>
        private float FinaleWinnerLeastWidth() => 150f + 2f * FinaleWell * FontScale + 2f * FinalePad * FontScale;

        /// <summary>The narrowest the FINAL TWO card runs: two finalist cards of a 76-wide portrait each, their gap and the padding.</summary>
        private float FinaleTwoLeastWidth() => 2f * FinalePad * FontScale + 2f * (2f * 8f * FontScale + 76f) + 12f * FontScale;

        /// <summary>The jury card without its faces: the heading, the gaps and the foot line's room.</summary>
        private float FinaleJuryFixed()
        {
            float s = FontScale;
            return FinalePad * s + BoardLine(14) + 8f * s + 6f * s + FinaleReasonFootHeight() + FinalePad * s;
        }

        /// <summary>The foot of the jury card: an eyebrow over two lines of the reason, each in a box 1.32 times its size.</summary>
        private float FinaleReasonFootHeight() => BoardLine(10) + 2f * FontScale + 2f * BoardLine(12);

        /// <summary>A face's row: the ring, the name on two lines, and the chip under it.</summary>
        private float FinaleFaceRow(float face) => face + 6f * FontScale + 4f * FontScale + 2f * BoardLine(10) + 3f * FontScale + 18f * FontScale;

        /// <summary>How many faces of <paramref name="face"/> stand across a jury card of <paramref name="cardWidth"/>.</summary>
        private int FinaleFacesAcross(float cardWidth, float face)
        {
            float s = FontScale, inner = cardWidth - 2f * FinalePad * s, step = face + 6f * s + 10f * s;
            // A whisker of tolerance: a card sized for exactly seven faces must take seven.
            return Mathf.Max(1, Mathf.FloorToInt((inner + 10f * s) / step + .001f));
        }

        /// <summary>The height <paramref name="count"/> faces of <paramref name="face"/> take in rows across a card of <paramref name="cardWidth"/>.</summary>
        private float FinaleJuryArea(int count, float cardWidth, float face)
        {
            if (count <= 0) return 0f;
            int rows = Mathf.CeilToInt(count / (float)FinaleFacesAcross(cardWidth, face));
            return rows * FinaleFaceRow(face) + (rows - 1) * 6f * FontScale;
        }

        private float FinaleHighlightsHeight() => 12f * FontScale + BoardLine(14) + 6f * FontScale + FinaleHighlightTile * FontScale + 12f * FontScale;

        // ------------------------------------------------------------ the pieces

        /// <summary>A card's heading in the heading blue, letterspaced, in a box 1.32 times its size.</summary>
        private void FinaleHeading(RectTransform card, string words, float x, float y, float width)
        {
            var head = FixedText(card, words, 14, UiTheme.Heading, new Vector2(x, -y), new Vector2(width, BoardLine(14)));
            head.name = "Heading";
            head.characterSpacing = 2f;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) head.font = semibold;
            head.textWrappingMode = TextWrappingModes.NoWrap;
            head.overflowMode = TextOverflowModes.Ellipsis;
            AutoSize(head, 10);
        }

        /// <summary>
        /// A mark: the pack's own icon, white and tinted, when it is installed; else another pack's
        /// (Pack 7's crown, Kit 6's glyphs, tinted when white) or the generated glyph; else nothing.
        /// </summary>
        private Image FinalePicture(string name, RectTransform parent, string pack9, string otherPack, string fallback, Color tint, Vector2 centre, float side)
        {
            if (!string.IsNullOrEmpty(pack9) && UiTheme.Pack(pack9) != null)
            {
                var mark = EndScreenKit.Picture(name, parent, pack9, null, tint, centre, side);
                if (mark != null) mark.color = tint;
                return mark;
            }
            var image = EndScreenKit.Picture(name, parent, otherPack, fallback, tint, centre, side);
            if (image != null && otherPack != null && otherPack.StartsWith("Kit6_", StringComparison.Ordinal) && image.sprite == UiTheme.Pack(otherPack))
                image.color = tint;
            return image;
        }

        /// <summary>
        /// The winner's card: the pack's card over its halo, the face in a well lit gold from below with
        /// the crown over its top edge, 'WINNER', the name on the pack's plate, the Game Sense numbers
        /// on two lines, and at the foot the sentence the whole-season walk reads.
        /// </summary>
        private void FinaleWinnerCard(RectTransform board, float x, float y, float width, float height, FinalePageSpec spec, EpisodeState state)
        {
            float s = FontScale, pad = FinalePad * s, inner = width - 2f * pad;
            var winner = state.Find(spec.WinnerId);
            if (winner == null) return;
            var card = EndScreenKit.Box(FinaleWinnerCardName, board, x, y, width, height);
            // The pack's card, then its halo put in front of it in the hierarchy, which draws it behind.
            EndScreenKit.Skin(card, PackArt.Pack9SeasonFinaleWinnerCardFill, PackArt.Pack9SeasonFinaleWinnerCardEdge, 14f * s,
                new Color(Surface.r, Surface.g, Surface.b, .96f), UiTheme.Gold, PackArt.SeasonWinnerHero, UiTheme.SurfaceRaised, UiTheme.Gold);
            EndScreenKit.Halo(card, PackArt.Pack9SeasonFinaleWinnerCardHalo, 28f * s, new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .6f));

            // The portrait as tall as the card leaves it, up to 180, in a well lit gold from below.
            float well = FinaleWell * s;
            float portraitHeight = Mathf.Clamp(height - FinaleWinnerFixed(), FinalePortraitLeast * .5f, FinalePortrait);
            float portraitWidth = Mathf.Min(inner - 2f * well, portraitHeight * (150f / 180f));
            float wellWidth = portraitWidth + 2f * well, wellHeight = portraitHeight + 2f * well;
            float at = pad + 10f * s, wellX = (width - wellWidth) * .5f;
            var gold = UiTheme.Pack(PackArt.GlowGold);
            if (gold != null)
            {
                var light = new GameObject("Winner glow", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                light.rectTransform.SetParent(card, false);
                float side = wellHeight + 60f * s;
                EndScreenKit.Place(light.rectTransform, wellX + wellWidth * .5f - side * .5f, at + wellHeight * .5f - side * .5f, side, side);
                light.sprite = gold; light.color = new Color(1f, 1f, 1f, .45f); light.preserveAspect = true; light.raycastTarget = false;
            }
            var ground = HudPrimitives.Fill("Portrait well", card, new Color(.07f, .06f, .04f, 1f), 12);
            EndScreenKit.Place(ground, wellX, at, wellWidth, wellHeight);
            ground.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            EndScreenKit.Ramp("Gold light", ground, new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .7f));
            var face = HudPrimitives.RectPortrait(ground, "Winner portrait", Portrait(winner.id), winner, new Vector2(portraitWidth, portraitHeight), 10);
            EndScreenKit.Place(face, well, well, portraitWidth, portraitHeight);
            UiTheme.AddBorder(ground, 12, new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .85f));
            // The crown over the well's top edge.
            float crown = 28f * s;
            FinalePicture("Crown mark", card, PackArt.Pack9SeasonFinaleCrownIcon, PackArt.SeasonWinnerCrown, "crown", UiTheme.Gold,
                new Vector2(width * .5f, -(at - 2f * s)), crown);
            at += wellHeight + 8f * s;

            // 'WINNER', its own label as it always was.
            var badge = FixedText(card, "WINNER", 14, UiTheme.Gold, new Vector2(pad, -at), new Vector2(inner, BoardLine(14)));
            badge.name = "Badge";
            badge.characterSpacing = 4f;
            badge.alignment = TextAlignmentOptions.Center;
            badge.textWrappingMode = TextWrappingModes.NoWrap;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) badge.font = semibold;
            at += BoardLine(14) + 4f * s;

            // The name, on the pack's plate; shrunk to fit rather than wrapped.
            float plateHeight = 40f * s;
            var plate = EndScreenKit.Box("Nameplate", card, pad, at, inner, plateHeight);
            EndScreenKit.Frame(plate, PackArt.SeasonWinnerNameplate, 12f * s, new Color(.2f, .17f, .06f, .95f), UiTheme.Gold);
            float nameBox = BoardLine(20);
            var name = FixedText(plate, HudPrimitives.WithYou(winner.name, winner.isPlayer), 20, Color.white,
                new Vector2(10f * s, -(plateHeight - nameBox) * .5f), new Vector2(inner - 20f * s, nameBox));
            name.name = "Name";
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) name.font = bold;
            name.alignment = TextAlignmentOptions.Center;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.enableVertexGradient = true;
            name.colorGradient = new VertexGradient(FinaleLightGold, FinaleLightGold, UiTheme.Gold, UiTheme.Gold);
            AutoSize(name, 12);
            at += plateHeight + 6f * s;

            // The Game Sense numbers on two lines; the report has the whole of the verdict.
            foreach (string line in new[] { spec.GameSenseFirst, spec.GameSenseSecond })
            {
                if (string.IsNullOrEmpty(line)) continue;
                var words = FixedText(card, line, 13, Paper, new Vector2(pad, -at), new Vector2(inner, BoardLine(13)));
                words.name = FinaleGameSenseName;
                words.alignment = TextAlignmentOptions.Center;
                words.textWrappingMode = TextWrappingModes.NoWrap;
                words.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(words, 9);
                at += BoardLine(13);
            }

            // The sentence the whole-season walk and the reveal's test read, muted, at the foot.
            var foot = FixedText(card, spec.WinnerLine ?? "", 12, UiTheme.Muted, new Vector2(pad, -(height - pad - BoardLine(12))), new Vector2(inner, BoardLine(12)));
            foot.name = FinaleWinnerLineName;
            foot.alignment = TextAlignmentOptions.Center;
            foot.textWrappingMode = TextWrappingModes.NoWrap;
            foot.overflowMode = TextOverflowModes.Ellipsis;
            AutoSize(foot, 8);
        }

        /// <summary>
        /// The final two: the winner's card and the runner-up's side by side, each a face with its
        /// edge in gold or steel, the name, the role and the count in a tile of the same colour; then
        /// the count's eyebrow - a tie called a tie, a jury of one said so - and the bar between them.
        /// </summary>
        private void FinaleFinalTwoCard(RectTransform board, float x, float y, float width, float height, FinalePageSpec spec, EpisodeState state)
        {
            float s = FontScale, pad = FinalePad * s, inner = width - 2f * pad, gap = 12f * s;
            var winner = state.Find(spec.WinnerId);
            var runnerUp = state.Find(spec.RunnerUpId);
            if (winner == null || runnerUp == null) return;
            var card = EndScreenKit.Box(FinaleFinalTwoCardName, board, x, y, width, height);
            EndScreenKit.Skin(card, PackArt.Pack9SeasonFinaleJuryPanelFill, PackArt.Pack9SeasonFinaleJuryPanelEdge, 14f * s,
                new Color(Surface.r, Surface.g, Surface.b, .94f), new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .5f),
                PackArt.Pack8Section, UiTheme.Surface);
            float at = pad;
            FinaleHeading(card, FinaleFinalTwoCardName, pad, at, inner);
            at += BoardLine(14) + 8f * s;

            float cardWidth = (inner - gap) * .5f, cardPad = 8f * s;
            // The faces as tall as the card leaves them, up to 180, and as wide as their card allows:
            // never squashed, so a narrow card gets a shorter face.
            float portraitHeight = Mathf.Clamp(height - FinaleTwoFixed(), 48f, FinalePortrait);
            float portraitWidth = Mathf.Max(40f, Mathf.Min(cardWidth - 2f * cardPad, portraitHeight * (150f / 180f)));
            portraitHeight = Mathf.Min(portraitHeight, portraitWidth * (180f / 150f));
            float finalistHeight = cardPad + portraitHeight + 6f * s + BoardLine(15) + 4f * s + 28f * s + cardPad;
            FinaleFinalist(card, pad, at, cardWidth, finalistHeight, portraitWidth, portraitHeight, winner, FinaleWinnerRole, UiTheme.Gold,
                spec.ForWinner, FinaleWinnerTallyName);
            FinaleFinalist(card, pad + cardWidth + gap, at, cardWidth, finalistHeight, portraitWidth, portraitHeight, runnerUp, FinaleRunnerUpRole,
                EndScreenKit.Steel, spec.ForRunnerUp, FinaleRunnerUpTallyName);
            at += finalistHeight + 8f * s;

            // The count's eyebrow, as the reveal says it, and the bar from the real counts.
            var eyebrow = FixedText(card, JuryReveal.CountEyebrow(spec.ForWinner, spec.ForRunnerUp), 11, UiTheme.Gold,
                new Vector2(pad, -at), new Vector2(inner, BoardLine(11)));
            eyebrow.name = FinaleCountEyebrowName;
            eyebrow.characterSpacing = 3f;
            eyebrow.alignment = TextAlignmentOptions.Center;
            eyebrow.textWrappingMode = TextWrappingModes.NoWrap;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) eyebrow.font = semibold;
            AutoSize(eyebrow, 8);
            at += BoardLine(11) + 4f * s;
            EndScreenKit.SplitBar(card, pad, at, inner, 22f * s, spec.ForWinner, spec.ForRunnerUp, UiTheme.Gold, EndScreenKit.Steel);
        }

        /// <summary>One finalist on the FINAL TWO card: the pack's finalist card, the face, the name, the role word and the count tile.</summary>
        private void FinaleFinalist(RectTransform parent, float x, float y, float width, float height, float portraitWidth, float portraitHeight,
            ContestantState who, string role, Color tint, int votes, string tallyName)
        {
            float s = FontScale, pad = 8f * s, inner = width - 2f * pad;
            var card = EndScreenKit.Box(FinaleFinalistPrefix + who.name, parent, x, y, width, height);
            EndScreenKit.Skin(card, PackArt.Pack9SeasonFinaleFinalistCardFill, PackArt.Pack9SeasonFinaleFinalistCardEdge, 10f * s,
                new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .9f), tint, null, UiTheme.SurfaceRaised,
                new Color(tint.r, tint.g, tint.b, .8f), 10);
            var face = HudPrimitives.RectPortrait(card, "Finalist portrait", Portrait(who.id), who, new Vector2(portraitWidth, portraitHeight), 8);
            EndScreenKit.Place(face, (width - portraitWidth) * .5f, pad, portraitWidth, portraitHeight);
            UiTheme.AddBorder(face, 8, new Color(tint.r, tint.g, tint.b, .85f));
            float at = pad + portraitHeight + 6f * s;
            var name = FixedText(card, HudPrimitives.WithYou(who.name, who.isPlayer), 15, Paper, new Vector2(pad, -at), new Vector2(inner, BoardLine(15)));
            name.name = "Name";
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            name.alignment = TextAlignmentOptions.Center;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            AutoSize(name, 10);
            at += BoardLine(15) + 4f * s;
            // The role word and the count's tile on one row.
            float tileWidth = 40f * s, tileHeight = 28f * s, roleBox = BoardLine(12);
            var word = FixedText(card, role, 12, tint, new Vector2(pad, -(at + (tileHeight - roleBox) * .5f)),
                new Vector2(Mathf.Max(30f, inner - tileWidth - 6f * s), roleBox));
            word.name = "Role";
            if (semibold != null) word.font = semibold;
            word.alignment = TextAlignmentOptions.MidlineLeft;
            word.textWrappingMode = TextWrappingModes.NoWrap;
            word.overflowMode = TextOverflowModes.Ellipsis;
            AutoSize(word, 9);
            EndScreenKit.NumberTile(card, tallyName, votes, tint, width - pad - tileWidth, at, tileWidth, tileHeight, Mathf.RoundToInt(15 * s));
        }

        /// <summary>
        /// The jury: a ring of faces in cast order, each ringed in the colour of the finalist they
        /// chose with that finalist's first name on a chip under it, in rows as many as the card
        /// holds; and the foot line, the pressed juror's reason or the hint to press one.
        /// </summary>
        private void FinaleJuryCard(RectTransform board, float x, float y, float width, float height, float face, FinalePageSpec spec, EpisodeState state)
        {
            float s = FontScale, pad = FinalePad * s, inner = width - 2f * pad;
            var card = EndScreenKit.Box(FinaleJuryCardName, board, x, y, width, height);
            EndScreenKit.Skin(card, PackArt.Pack9SeasonFinaleJuryPanelFill, PackArt.Pack9SeasonFinaleJuryPanelEdge, 14f * s,
                new Color(Surface.r, Surface.g, Surface.b, .94f), new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .5f),
                PackArt.Pack8Section, UiTheme.Surface);
            float at = pad;
            FinaleHeading(card, FinaleJuryHeading, pad, at, inner);
            at += BoardLine(14) + 8f * s;

            var jurors = spec.Jurors ?? new List<FinaleJuror>();
            float ring = 3f * s, rim = face + 2f * ring, step = rim + 10f * s, rowHeight = FinaleFaceRow(face), rowGap = 6f * s;
            int perRow = FinaleFacesAcross(width, face);
            int rows = jurors.Count == 0 ? 0 : Mathf.CeilToInt(jurors.Count / (float)perRow);
            var grid = EndScreenKit.Box(FinaleJuryFacesName, card, pad, at, inner, rows * rowHeight + Mathf.Max(0, rows - 1) * rowGap);
            for (int i = 0; i < jurors.Count; i++)
            {
                int row = i / perRow, column = i % perRow;
                int inRow = Mathf.Min(perRow, jurors.Count - row * perRow);
                // Each row centred, as the reveal centres its jury.
                float left = (inner - (inRow * step - 10f * s)) * .5f;
                FinaleJurorFace(grid, left + column * step, row * (rowHeight + rowGap), rim, rowHeight, face, ring, jurors[i], spec, state);
            }
            FinaleReasonFoot(card, pad, height - pad - FinaleReasonFootHeight(), inner, spec, state);
        }

        /// <summary>
        /// One juror's face: a control captioned with their name - the name under the face, on up to
        /// two lines, is that caption - the ring in the finalist's colour over the pack's glow, and
        /// the finalist's first name on a chip in the same colour. A press shows their reason; a
        /// second press folds it. Nothing is committed.
        /// </summary>
        private void FinaleJurorFace(RectTransform grid, float x, float y, float rim, float height, float face, float ring, FinaleJuror juror,
            FinalePageSpec spec, EpisodeState state)
        {
            float s = FontScale;
            var actor = state.Find(juror.Id);
            var finalist = state.Find(juror.FinalistId);
            bool forWinner = juror.FinalistId == spec.WinnerId;
            var tint = forWinner ? UiTheme.Gold : EndScreenKit.Steel;
            bool pressed = spec.PressedJurorId != null && spec.PressedJurorId == juror.Id;
            // A faint seat behind the face, which the keyboard's and the pointer's tints multiply up:
            // a tint on a clear panel shows nothing, and a face the keyboard is on must be seen.
            var seat = Panel(juror.Caption, grid, new Color(1f, 1f, 1f, pressed ? .1f : .04f), 8);
            EndScreenKit.Place(seat, x, y, rim, height);
            string id = juror.Id;
            var button = Pressable(seat, () => spec.PressJuror?.Invoke(id));
            var colours = button.colors;
            colours.highlightedColor = new Color(3f, 3f, 3f);
            colours.selectedColor = colours.highlightedColor;
            colours.pressedColor = new Color(4f, 4f, 4f);
            button.colors = colours;
            // The pack's glow behind the ring, in the finalist's colour, brighter on the pressed face.
            var glow = EndScreenKit.Picture("Ring glow", seat, pressed ? PackArt.Pack9SharedFocusHalo : PackArt.Pack9SharedPortraitRingGlow, null, tint,
                new Vector2(rim * .5f, -rim * .5f), rim);
            if (glow != null) glow.color = new Color(tint.r, tint.g, tint.b, pressed ? .95f : .6f);
            var portrait = HudPrimitives.Portrait(seat, Portrait(juror.Id), tint, face, ring, false, actor);
            Anchor(portrait, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(rim, rim));
            float at = rim + 4f * s, nameBox = 2f * BoardLine(10);
            var name = FixedText(seat, juror.Caption, 10, juror.IsPlayer ? Accent : Paper, new Vector2(-4f * s, -at), new Vector2(rim + 8f * s, nameBox));
            name.name = FinaleJurorNameName;
            name.alignment = TextAlignmentOptions.Top;
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            if (medium != null) name.font = medium;
            AutoSize(name, 7);
            at += nameBox + 3f * s;
            // The finalist's first name on a chip in the finalist's colour, as the reveal's chips say it.
            float chipHeight = 18f * s;
            var chip = HudPrimitives.Fill(FinaleJurorChipName, seat, tint, UiTheme.ControlRadius);
            UiTheme.PackSliced(chip.GetComponent<Image>(), PackArt.Pack9SharedPillFill, chipHeight * .5f, tint);
            EndScreenKit.Place(chip, -2f * s, at, rim + 4f * s, chipHeight);
            var word = NewText(chip, FinalistRead.FirstName(finalist != null ? finalist.name : juror.FinalistId), 10, UiTheme.OnColor(tint));
            word.name = FinaleJurorChipWordName;
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) word.font = bold;
            word.alignment = TextAlignmentOptions.Center;
            word.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(word.rectTransform, 2f, 0f, 2f, 0f);
            AutoSize(word, 7);
        }

        /// <summary>
        /// The jury card's foot: the pressed juror's reason, as they gave it with their vote, under an
        /// eyebrow naming them and their choice; or, with nobody pressed, the hint to press a face.
        /// </summary>
        private void FinaleReasonFoot(RectTransform card, float x, float y, float width, FinalePageSpec spec, EpisodeState state)
        {
            float s = FontScale, eyebrowBox = BoardLine(10), lineBox = 2f * BoardLine(12);
            int index = -1;
            if (spec.Jurors != null && spec.PressedJurorId != null)
                for (int i = 0; i < spec.Jurors.Count && index < 0; i++)
                    if (spec.Jurors[i].Id == spec.PressedJurorId) index = i;
            if (index < 0)
            {
                var hint = FixedText(card, FinaleReasonHint, 12, UiTheme.Muted, new Vector2(x, -(y + eyebrowBox + 2f * s)), new Vector2(width, lineBox));
                hint.name = FinaleReasonHintName;
                hint.alignment = TextAlignmentOptions.Top;
                hint.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(hint, 9);
                return;
            }
            var juror = spec.Jurors[index];
            bool forWinner = juror.FinalistId == spec.WinnerId;
            var tint = forWinner ? UiTheme.Gold : EndScreenKit.Steel;
            var finalist = state.Find(juror.FinalistId);
            string chosen = FinalistRead.FirstName(finalist != null ? finalist.name : "");
            var eyebrow = FixedText(card, (juror.IsPlayer ? "YOU" : juror.Caption.ToUpperInvariant()) + " · VOTED " + chosen.ToUpperInvariant(), 10, tint,
                new Vector2(x, -y), new Vector2(width, eyebrowBox));
            eyebrow.name = FinaleReasonEyebrowName;
            eyebrow.characterSpacing = 2f;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) eyebrow.font = semibold;
            eyebrow.textWrappingMode = TextWrappingModes.NoWrap;
            eyebrow.overflowMode = TextOverflowModes.Ellipsis;
            AutoSize(eyebrow, 7);
            var line = FixedText(card, juror.Reason ?? "", 12, Paper, new Vector2(x, -(y + eyebrowBox + 2f * s)), new Vector2(width, lineBox));
            line.name = FinaleReasonLineName;
            line.alignment = TextAlignmentOptions.Top;
            line.overflowMode = TextOverflowModes.Ellipsis;
            AutoSize(line, 9);
        }

        /// <summary>SEASON HIGHLIGHTS: six tiles of real counts, each with its mark, its number and the caption it is found by.</summary>
        private void FinaleHighlights(RectTransform board, float y, float width, float height, IList<FinaleStat> stats)
        {
            float s = FontScale, pad = 12f * s, inner = width - 2f * pad, gap = 10f * s;
            var strip = EndScreenKit.Box(FinaleHighlightsName, board, 0f, y, width, height);
            EndScreenKit.Skin(strip, PackArt.Pack9SeasonFinaleJuryPanelFill, PackArt.Pack9SeasonFinaleJuryPanelEdge, 14f * s,
                new Color(Surface.r, Surface.g, Surface.b, .94f), new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .5f),
                PackArt.Pack8Section, UiTheme.Surface);
            FinaleHeading(strip, FinaleHighlightsHeading, pad, pad, inner);
            float at = pad + BoardLine(14) + 6f * s, tileHeight = FinaleHighlightTile * s;
            int count = stats != null ? stats.Count : 0;
            if (count == 0) return;
            float tileWidth = (inner - gap * (count - 1)) / count;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            for (int i = 0; i < count; i++)
            {
                var stat = stats[i];
                var tile = EndScreenKit.Box(stat.Caption, strip, pad + i * (tileWidth + gap), at, tileWidth, tileHeight);
                EndScreenKit.Skin(tile, PackArt.Pack9SeasonFinaleStatTileFill, PackArt.Pack9SeasonFinaleStatTileEdge, 10f * s,
                    new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .9f), new Color(stat.Tint.r, stat.Tint.g, stat.Tint.b, .45f),
                    PackArt.SeasonStatNeutral, UiTheme.SurfaceRaised, null, 10);
                float glyph = Mathf.Min(26f * s, tileHeight - 16f * s), left = 10f * s;
                var mark = FinalePicture("Icon", tile, stat.Icon, null, stat.Fallback, stat.Tint, new Vector2(left + glyph * .5f, -tileHeight * .5f), glyph);
                if (mark != null) left += glyph + 8f * s;
                float textWidth = Mathf.Max(30f, tileWidth - left - 8f * s);
                float valueBox = BoardLine(20), captionBox = BoardLine(11);
                float top = Mathf.Max(2f * s, (tileHeight - valueBox - 2f * s - captionBox) * .5f);
                var value = FixedText(tile, stat.Value, 20, Paper, new Vector2(left, -top), new Vector2(textWidth, valueBox));
                value.name = "Value";
                if (semibold != null) value.font = semibold;
                value.textWrappingMode = TextWrappingModes.NoWrap;
                value.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(value, 12);
                var caption = FixedText(tile, stat.Caption, 11, UiTheme.Muted, new Vector2(left, -(top + valueBox + 2f * s)), new Vector2(textWidth, captionBox));
                caption.name = "Caption";
                caption.textWrappingMode = TextWrappingModes.NoWrap;
                caption.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(caption, 8);
            }
        }

        /// <summary>A row of the ways on: as many tiles abreast as there are ways, each the width the row divides into.</summary>
        private void FinaleWayRow(RectTransform board, string name, float y, float width, float height, IList<FinaleWay> ways, bool wide)
        {
            float s = FontScale, gap = 12f * s;
            var row = EndScreenKit.Box(name, board, 0f, y, width, height);
            int count = ways != null ? ways.Count : 0;
            if (count == 0) return;
            float each = (width - gap * (count - 1)) / count;
            for (int i = 0; i < count; i++) FinaleWayTile(row, i * (each + gap), each, height, ways[i], wide);
        }

        /// <summary>
        /// One way on as a tile: the mark, the caption - the control's name and its first label, so
        /// every lookup by caption still finds it - the line under it saying where it leads, and the
        /// chevron. The pack's action tile over the glass when the pack is there.
        /// </summary>
        private void FinaleWayTile(RectTransform row, float x, float width, float height, FinaleWay way, bool wide)
        {
            float s = FontScale, pad = (wide ? 12f : 8f) * s;
            var rect = Chrome(way.Caption, row, UiTheme.Emphasis.Interactive);
            HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
            EndScreenKit.Place(rect, x, 0f, width, height);
            if (UiTheme.Pack(PackArt.Pack9SeasonFinaleActionTileFill) != null)
                EndScreenKit.Skin(rect, PackArt.Pack9SeasonFinaleActionTileFill, PackArt.Pack9SeasonFinaleActionTileEdge, 12f * s,
                    new Color(Surface.r, Surface.g, Surface.b, .94f), UiTheme.Edge(UiTheme.Emphasis.Interactive), null, UiTheme.Surface);
            var button = Pressable(rect, way.Choose ?? (() => { }));
            var colours = button.colors;
            colours.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colours.selectedColor = colours.highlightedColor;
            button.colors = colours;
            float glyph = (wide ? 28f : 20f) * s, left = pad;
            var mark = FinalePicture("Way mark", rect, way.Icon, null, way.Fallback, Accent, new Vector2(left + glyph * .5f, -height * .5f), glyph);
            if (mark != null) left += glyph + 10f * s;
            float chevron = 18f * s, right = 16f + chevron + 10f;
            float textWidth = Mathf.Max(40f, width - left - right);
            float captionBox = BoardLine(wide ? 16 : 14), lineBox = BoardLine(wide ? 12 : 11);
            float top = Mathf.Max(2f * s, (height - captionBox - 2f * s - lineBox) * .5f);
            var caption = FixedText(rect, way.Caption, wide ? 16 : 14, Paper, new Vector2(left, -top), new Vector2(textWidth, captionBox));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) caption.font = semibold;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.overflowMode = TextOverflowModes.Ellipsis;
            AutoSize(caption, 11);
            var line = FixedText(rect, way.Line ?? "", wide ? 12 : 11, UiTheme.Muted, new Vector2(left, -(top + captionBox + 2f * s)), new Vector2(textWidth, lineBox));
            line.name = FinaleWayLineName;
            line.textWrappingMode = TextWrappingModes.NoWrap;
            line.overflowMode = TextOverflowModes.Ellipsis;
            AutoSize(line, 8);
            HudPrimitives.Chevron(rect, UiTheme.Hairline, chevron).anchoredPosition = new Vector2(-16f, 0f);
        }
    }
}
