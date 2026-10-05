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
    /// Free time as one board on the strategy stage (ACTIONS-DEALS-ALLIANCES-PLAN F1, the owner's
    /// mockup 87). It was a column that took the house event's 403-unit band the moment a story beat
    /// was waiting, and scrolled about 1,450 units through it, or 1,100 through the plain stage's
    /// 429 without one, with the way on at the foot of the scroll (the owner's screenshots 84 to 86).
    ///
    /// <para>Now it is laid for the frame, in fixed-size type: a hero row - whoever came to the
    /// player, else the first story beat waiting as a banner, else the play in motion - beside the
    /// budget card; one line of the story under it; the house as one row of cards, paged when it
    /// runs past ten (eight at the larger text); and the other ways to spend the time as tiles. The
    /// hero takes what it needs and no more than the cards can spare; when the height is short its
    /// longest copy steps down a size or two rather than the board growing a scroll.</para>
    ///
    /// <para>Every word on it is the player's own reading, a public fact, a row the player owns or
    /// the game's own rule copy. The pinned names stay: the budget card is 'Screen head', the cards
    /// are under 'House cards', the tiles under 'House moves', the play is 'Current play' and the
    /// threads 'Threads card'; every control keeps the caption it is found by, and the board commits
    /// nothing but its controls.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The board's parts, by the names a test finds them by.</summary>
        public const string FreeTimeBoardName = "Free time board", FreeTimeHeroName = "Free time hero",
            StoryBannerName = "Story banner", FreeTimeReplyName = "Free time reply", StoryStripName = "Story strip",
            FreeTimeHaveNotName = "Free time have-not line", TalkHeadName = "Talk head", MovesHeadName = "Moves head",
            ActionsLeftCountName = "Actions left count", ActionsLeftWordName = "Actions left word",
            BudgetRuleName = "Budget rule", BudgetCopyName = "Budget copy", UnusedActionsNoteName = "Unused actions",
            BuyActionsName = "Buy actions", BoughtOutName = "Bought out", WaitingBeatsName = "Waiting beats",
            PreparationName = "Preparation", FreeTimeTipName = "Free time tip", SecondaryHeadlineName = "Secondary headline",
            BoardPageName = "Page", MoreWaitingCountName = "More waiting count";

        /// <summary>
        /// The story strip's control for the beats waiting past its room: it opens the first of them
        /// as the step. Its count stands beside it as a label of its own, so the caption never changes.
        /// </summary>
        public const string MoreWaitingCaption = "More waiting";

        /// <summary>What the hero row leads with: whoever came to the player, a story beat waiting, or the play in motion.</summary>
        public enum FreeTimeHero { Play, Beat, Reply }

        /// <summary>One of the other ways to spend the time: its caption, its glyph, what it costs or that it is free, and its risk or its odds.</summary>
        public struct BoardTile
        {
            public string Caption, Glyph, Cost, Corner;
            public Color CornerTint;
            /// <summary>Whether it costs nothing, which its foot says in the allied green.</summary>
            public bool Free;
            /// <summary>Drawn and not pressable, with the reason at its foot: a move that costs an action when none is left.</summary>
            public bool Locked;
            public Action Choose;
        }

        /// <summary>One way to buy an action: the caption it has always had, and its price in goodwill.</summary>
        public struct BuyButton
        {
            public string Caption, Price;
            public Action Choose;
        }

        /// <summary>
        /// What the director hands the board: the words the rules and its own view state decide,
        /// and what each control does. The board reads the faces, the week's roles and the player's
        /// own readings from the committed state itself.
        /// </summary>
        public sealed class FreeTimeBoardSpec
        {
            public FreeTimeHero Hero;
            /// <summary>Whoever came to the player, oldest first: the heading, what they said, how many are waiting, who is next, and the answers.</summary>
            public string ReplyTitle, ReplyMessage, ReplyNext;
            public int RepliesWaiting;
            public IList<CampaignReply> Replies;
            /// <summary>The first story beat waiting: its eyebrow, its title, the moment, the line under it, whether answering is free, and the way in.</summary>
            public string BeatEyebrow, BeatTitle, BeatNarrative, BeatNote;
            public bool BeatFree;
            public Action Answer;
            /// <summary>The play in motion as the hero: its eyebrow, its title, its bar or its offer, and its goal.</summary>
            public string PlayEyebrow, PlayTitle, PlayStatus, PlayGoal, PlayBarLabel;
            public bool PlayBar;
            public int PlayHave, PlayNeed;
            /// <summary>The budget: the actions left, the week's rule, what costs and what is free, what moving on loses, and the ways to buy more.</summary>
            public int ActionsLeft;
            public string Rule, Copy, Unused, BoughtOut;
            public IList<BuyButton> Buys;
            /// <summary>The story strip: the play when the hero is not showing it, the threads, the other beats waiting, the preparation banked, and the Have-Not line.</summary>
            public string StripPlay, Threads, Preparation, HaveNot;
            public IList<(string Caption, Action Open)> Waiting;
            /// <summary>The house as cards, the page shown, and what the controls on a card do.</summary>
            public IList<HouseCard> Cards;
            public int Page;
            public Action<int> ChoosePage;
            public Action<string> Talk, Pick;
            /// <summary>The other ways to spend the time.</summary>
            public IList<BoardTile> Moves;
            /// <summary>
            /// Whether the keyboard opens on a control that commits nothing - the banner's Answer, else
            /// the first card's way to talk - rather than the column's first: the board has just
            /// replaced a step, and the Enter that closed it may be the first of two.
            /// </summary>
            public bool FocusSafely;
        }

        // Heights and widths at the resting text size; each is multiplied by the text scale.
        private const float BoardGap = 10f, BoardHeadGap = 6f, BoardPad = 12f, BoardHeroGap = 12f;
        private const float BoardStripHeight = 26f, BoardHaveNotGap = 4f, BoardHaveNotHeight = 20f;
        /// <summary>A section's head with its line under the title, at the resting size; the title alone at the larger.</summary>
        private const float BoardHeadWithLine = 46f, BoardHeadHeight = 30f;
        private const float BoardTileHeight = 72f, BoardTileLeast = 200f;
        /// <summary>A card is its photo and this much under and around it: the name, your reading and the talk button.</summary>
        private const float BoardCardFoot = 81f;
        /// <summary>A card's photo: never shorter than this, and never much taller than it is wide.</summary>
        private const float BoardPhotoLeast = 48f, BoardPhotoTall = 1.15f;
        /// <summary>A card's width: never narrower than the first, which is what pages a big house; never wider than the second.</summary>
        private const float BoardCardLeast = 112f, BoardCardMost = 180f, BoardCardGap = 12f;
        /// <summary>The hero's least height, and its share of what the cards and it divide between them.</summary>
        private const float BoardHeroLeast = 104f, BoardHeroShare = .55f;
        /// <summary>The budget card's share of the hero row, and the narrowest and widest it runs.</summary>
        private const float BoardBudgetShare = .45f, BoardBudgetLeast = 400f, BoardBudgetMost = 640f;
        private const float BoardReplyTiles = 60f, BoardBannerFoot = 34f, BoardCountBox = 44f;
        /// <summary>
        /// A way to buy an action: its height with the caption on one line over the price, its inset,
        /// and the least size the caption may take on one line. A caption that would have to go under
        /// that goes on two lines at its own size instead, and both buttons grow to hold it.
        /// </summary>
        private const float BoardBuyHeight = 38f, BoardBuyPad = 8f, BoardBuyLeast = 11f;

        /// <summary>
        /// Draws free time's board into the stage's column, at the stage's whole width, fitted to the
        /// height the stage's footer leaves it.
        /// </summary>
        public void FreeTimeBoard(FreeTimeBoardSpec spec)
        {
            var state = director != null ? director.Snapshot : null;
            if (content == null || spec == null || state == null) return;
            // The board is laid for the stage's whole width: the reading column's cap is for
            // paragraphs, and the board has none. Uncapped before measuring anything.
            UncapContent();
            float s = FontScale, width = ContentWidth(), gap = BoardGap * s, headGap = BoardHeadGap * s;
            float room = Mathf.Max(0f, NominationRoomLeft());
            // The heads keep their line under the title at the resting size; at the larger the
            // height is the cards', as the status cards drop theirs.
            bool lines = s <= 1.05f;
            float head = (lines ? BoardHeadWithLine : BoardHeadHeight) * s;
            bool haveNot = !string.IsNullOrEmpty(spec.HaveNot);
            float strip = BoardStripHeight * s + (haveNot ? (BoardHaveNotGap + BoardHaveNotHeight) * s : 0f);

            // The moves: as many abreast as keep each at its least width - five on the 16:9 frame at
            // either size - and a second row only where the frame is narrow and tall enough for one.
            var moves = spec.Moves ?? new List<BoardTile>();
            int perRow = Mathf.Clamp(Mathf.FloorToInt((width + gap) / (BoardTileLeast * s + gap)), 1, Mathf.Max(1, moves.Count));
            int tileRows = moves.Count == 0 ? 0 : Mathf.CeilToInt(moves.Count / (float)perRow);
            float tileHeight = BoardTileHeight * s;
            float tiles = tileRows * tileHeight + Mathf.Max(0, tileRows - 1) * gap;
            float fixedPart = 3f * gap + 2f * headGap + strip + 2f * head + tiles;

            // The house as one row of cards, paged past ten at the resting size and eight at the
            // larger, or fewer where the column is narrower than that many at their least width.
            var cards = spec.Cards ?? new List<HouseCard>();
            float cardGap = BoardCardGap * s;
            int cap = s > 1.05f ? 8 : 10;
            int perPage = Mathf.Clamp(Mathf.FloorToInt((width + cardGap + .5f) / (BoardCardLeast * s + cardGap)), 1, cap);
            int pages = Mathf.Max(1, Mathf.CeilToInt(cards.Count / (float)perPage));
            int page = Mathf.Clamp(spec.Page, 0, pages - 1);
            int across = Mathf.Clamp(cards.Count, 1, perPage);
            float cardWidth = Mathf.Min(BoardCardMost * s, (width - (across - 1) * cardGap) / across);
            float foot = BoardCardFoot * s;
            float cardsLeast = foot + BoardPhotoLeast * s;
            float cardsMost = foot + Mathf.Max(BoardPhotoLeast * s, (cardWidth - 12f * s) * BoardPhotoTall);

            var board = new GameObject(FreeTimeBoardName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            board.SetParent(content, false);

            // The hero takes what its words need, up to its share of what it and the cards divide,
            // and never so much that the cards fall under their least height.
            float budgetWidth = Mathf.Clamp(width * BoardBudgetShare, BoardBudgetLeast * s, BoardBudgetMost * s);
            float heroGap = BoardHeroGap * s, leftWidth = Mathf.Max(120f * s, width - budgetWidth - heroGap);
            var probe = NewText(board, "", 12, Paper);
            float replyTiles = spec.Hero == FreeTimeHero.Reply
                ? CampaignReplyHeight(probe, leftWidth - 2f * BoardPad * s, spec.Replies, BoardReplyTiles * s) : 0f;
            float need = Mathf.Max(BoardHeroNeed(probe, spec, leftWidth, replyTiles), BoardBudgetNeed(probe, spec, budgetWidth));
            probe.gameObject.SetActive(false);
            Destroy(probe.gameObject);
            float shared = Mathf.Max(0f, room - fixedPart);
            float heroMost = Mathf.Max(BoardHeroLeast * s, spec.Hero == FreeTimeHero.Reply
                ? shared - cardsLeast : Mathf.Min(shared * BoardHeroShare, shared - cardsLeast));
            float hero = Mathf.Clamp(need, BoardHeroLeast * s, heroMost);
            // Keep every reply's consequence visible. On a frame too short for that and the
            // house cards, the existing outer scroll is preferable to truncating the choice.
            if (spec.Hero == FreeTimeHero.Reply)
                hero = Mathf.Max(hero, (2f * BoardPad + 17f + 22f + 6f) * s + BoardLine(13) + replyTiles);
            float cardsHeight = Mathf.Clamp(shared - hero, cardsLeast, cardsMost);

            // Laid top to bottom, and built in the order the keyboard should walk it: what came to the
            // player, the story's waiting beats, the cards, the pager, the moves, and the budget's ways
            // to buy time last - the budget card a child of the board, beside the hero rather than in
            // it, since the ring follows the hierarchy - so the panel never opens on spending goodwill.
            // What opens a beat stands over the step's words column, never over its options.
            float y = 0f, words = BoardStepWords(width);
            var heroRow = EndScreenKit.Box(FreeTimeHeroName, board, 0f, y, leftWidth, hero);
            switch (spec.Hero)
            {
                case FreeTimeHero.Reply: BoardReply(heroRow, leftWidth, hero, replyTiles, spec); break;
                case FreeTimeHero.Beat: BoardBanner(heroRow, leftWidth, hero, words, spec); break;
                default: BoardPlay(heroRow, leftWidth, hero, spec); break;
            }
            y += hero + gap;
            BoardStrip(board, y, width, words, spec);
            y += strip + gap;
            float talkY = y;
            y += head + headGap;
            var shown = cards.Skip(page * perPage).Take(perPage).ToList();
            BoardCards(board, y, width, cardsHeight, shown, cardWidth, cardGap, spec, state);
            float reserve = BoardPager(board, talkY, width, page, pages, spec);
            BoardHead(board, TalkHeadName, "people", "TALK TO A HOUSEGUEST", "Choose someone to approach, or select an action below.",
                talkY, width, head, lines, reserve);
            y += cardsHeight + gap;
            BoardHead(board, MovesHeadName, "star", "OTHER WAYS TO SPEND YOUR TIME", "Explore the house, make a move, or find something to do.",
                y, width, head, lines, 0f);
            y += head + headGap;
            BoardMoves(board, y, width, moves, perRow, tileHeight, gap);
            y += tiles;
            BoardBudget(board, leftWidth + heroGap, budgetWidth, hero, spec);
            var size = board.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = y;
            if (!spec.FocusSafely) return;
            // The way into the next beat, which only opens it; else the walk over to the first card's
            // houseguest. Never a reply's answer, a move, a way to buy time or the way on.
            var first = shown.Count > 0 ? state.Find(shown[0].Id) : null;
            if (spec.Hero == FreeTimeHero.Beat) FocusWhenWired(EpisodeDirector.AnswerBeatCaption);
            else if (first != null) FocusWhenWired(CastTalkCaption((first.name ?? "").Split(' ')[0]));
        }

        // ------------------------------------------------------------ measuring

        /// <summary>A size at the player's text scale, rounded as every label's is.</summary>
        private int BoardSized(int size) => Mathf.RoundToInt(size * FontScale);

        /// <summary>One line of a size at the player's text scale, as the end screens' kit counts a line.</summary>
        private float BoardLine(int size) => BoardSized(size) * 1.32f;

        /// <summary>The height <paramref name="words"/> wrap to at <paramref name="width"/>, at a size, a weight and a style, measured on <paramref name="probe"/>.</summary>
        private float BoardMeasure(TMP_Text probe, string words, int size, float width, TMP_FontAsset weight = null, FontStyles style = FontStyles.Normal) =>
            BoardMeasureAt(probe, words, BoardSized(size), width, weight, style);

        /// <summary>The height <paramref name="words"/> wrap to at <paramref name="width"/> at a size already scaled, measured on <paramref name="probe"/>.</summary>
        private float BoardMeasureAt(TMP_Text probe, string words, float fontSize, float width, TMP_FontAsset weight = null, FontStyles style = FontStyles.Normal)
        {
            if (probe == null || string.IsNullOrEmpty(words) || width <= 1f) return 0f;
            probe.font = weight != null ? weight : font;
            probe.fontStyle = style;
            probe.enableAutoSizing = false;
            probe.fontSize = fontSize;
            probe.textWrappingMode = TextWrappingModes.Normal;
            return Mathf.Ceil(probe.GetPreferredValues(Localisation.Text(words), width, 0f).y) + 2f;
        }

        /// <summary>The width <paramref name="words"/> take on one line at a size and a weight, measured on <paramref name="probe"/>.</summary>
        private float BoardWidthOf(TMP_Text probe, string words, int size, TMP_FontAsset weight = null)
        {
            if (probe == null || string.IsNullOrEmpty(words)) return 0f;
            probe.font = weight != null ? weight : font;
            probe.fontStyle = FontStyles.Normal;
            probe.enableAutoSizing = false;
            probe.fontSize = BoardSized(size);
            probe.textWrappingMode = TextWrappingModes.NoWrap;
            return Mathf.Ceil(probe.GetPreferredValues(Localisation.Text(words)).x);
        }

        /// <summary>
        /// How wide the words column is that a story beat's step draws down the left of this same
        /// column (NominationStoryStep): its eyebrow, its name and the moment, and no control. The
        /// board puts whatever opens a beat over it - Answer, the waiting chips, More waiting - so the
        /// second press of a double click lands on the step's words, never on an option nobody read.
        /// </summary>
        private float BoardStepWords(float width) => Mathf.Floor(width * .38f - 9f * FontScale);

        /// <summary>
        /// The buy buttons' row at <paramref name="inner"/>: its height, and whether the captions go
        /// on two lines - when either would have to drop under <see cref="BoardBuyLeast"/> to stay on
        /// one. Both buttons wrap together, so they keep one height and one look.
        /// </summary>
        private float BoardBuyRow(TMP_Text probe, IList<BuyButton> buys, float inner, out bool wraps)
        {
            float s = FontScale;
            wraps = false;
            if (buys == null || buys.Count == 0) return BoardBuyHeight * s;
            float each = (inner - (buys.Count - 1) * 8f * s) / buys.Count, box = each - 2f * BoardBuyPad * s;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            foreach (var buy in buys)
            {
                // The size one line would need, a whisker short of the box for rounding and kerning.
                float wide = BoardWidthOf(probe, buy.Caption, 12, semibold);
                if (wide > 0f && wide * BoardBuyLeast / BoardSized(12) > box - 2f * s) wraps = true;
            }
            return wraps ? BoardBuyCaptionTop * s + BoardBuyCaptionBox(true) + BoardBuyPriceBox * s + 4f * s : BoardBuyHeight * s;
        }

        /// <summary>A buy button's caption: from this far down, in a box 1.3 times its size for each of its lines.</summary>
        private const float BoardBuyCaptionTop = 4f, BoardBuyPriceBox = 15f;
        private float BoardBuyCaptionBox(bool twoLines) => (twoLines ? 2f : 1f) * BoardSized(12) * 1.3f;

        /// <summary>The height the hero's left-hand card needs for its words at <paramref name="width"/>.</summary>
        private float BoardHeroNeed(TMP_Text probe, FreeTimeBoardSpec spec, float width, float replyTiles)
        {
            float s = FontScale, pad = BoardPad * s, inner = width - 2f * pad;
            switch (spec.Hero)
            {
                case FreeTimeHero.Reply:
                    return pad + 17f * s + 22f * s + Mathf.Clamp(BoardMeasure(probe, spec.ReplyMessage, 13, inner, null, FontStyles.Italic), BoardLine(13), 3f * BoardLine(13))
                        + 6f * s + replyTiles + pad;
                case FreeTimeHero.Beat:
                    return pad + 18f * s + 30f * s + Mathf.Clamp(BoardMeasure(probe, spec.BeatNarrative, 14, inner, null, FontStyles.Italic), BoardLine(14), 4f * BoardLine(14))
                        + 8f * s + BoardBannerFoot * s + pad;
                default:
                    float play = pad + 18f * s + 28f * s;
                    if (spec.PlayBar) play += 32f * s;
                    else if (!string.IsNullOrEmpty(spec.PlayStatus)) play += BoardPlayStatusBox() + 2f * s;
                    return play + Mathf.Min(BoardMeasure(probe, spec.PlayGoal, 13, inner), 2f * BoardLine(13)) + pad;
            }
        }

        /// <summary>The play's offer line, in a box 1.3 times its size at the player's text scale.</summary>
        private float BoardPlayStatusBox() => BoardSized(13) * 1.3f;

        /// <summary>The budget card's left-hand column: the count and what moving on loses.</summary>
        private float BoardBudgetLeft(float inner) => Mathf.Clamp(inner * .34f, 118f * FontScale, 190f * FontScale);

        /// <summary>The height the budget card needs for its words at <paramref name="width"/>.</summary>
        private float BoardBudgetNeed(TMP_Text probe, FreeTimeBoardSpec spec, float width)
        {
            float s = FontScale, pad = BoardPad * s, inner = width - 2f * pad;
            float left = BoardBudgetLeft(inner), right = inner - left - 12f * s;
            float leftNeed = 18f * s + (BoardCountBox + 2f) * s
                + (string.IsNullOrEmpty(spec.Unused) ? 0f : Mathf.Min(BoardMeasure(probe, spec.Unused, 12, left), 2f * BoardLine(12)));
            float rightNeed = BoardMeasure(probe, spec.Rule, 12, right) + 4f * s + BoardMeasure(probe, spec.Copy, 12, right);
            return pad + Mathf.Max(leftNeed, rightNeed) + 8f * s + BoardBuyRow(probe, spec.Buys, inner, out _) + pad;
        }

        // ------------------------------------------------------------ the hero row

        /// <summary>
        /// Somebody who came to the player, in the hero's place, as the campaign's plea strip draws
        /// one: the eyebrow, how many are waiting and who is next, the heading and what they said,
        /// and the answers as tiles under the grid name they have always had.
        /// </summary>
        private void BoardReply(RectTransform row, float width, float height, float tiles, FreeTimeBoardSpec spec)
        {
            float s = FontScale, pad = BoardPad * s, inner = width - 2f * pad, y = pad;
            var card = EndScreenKit.Box(FreeTimeReplyName, row, 0f, 0f, width, height);
            EndScreenKit.Frame(card, PackArt.Pack8Section, 14f * s, new Color(Surface.r, Surface.g, Surface.b, .94f),
                new Color(UiTheme.Joke.r, UiTheme.Joke.g, UiTheme.Joke.b, .55f));
            bool counted = spec.RepliesWaiting > 1;
            var eyebrow = FixedText(card, ReplyCardEyebrow, 12, UiTheme.Joke, new Vector2(pad, -y), new Vector2(counted ? inner * .55f : inner, 16f * s));
            eyebrow.name = "Plea eyebrow";
            eyebrow.characterSpacing = 4f;
            AutoSize(eyebrow, 9);
            if (counted)
            {
                // The oldest is answered first; the rest wait, and the count says who is next.
                var count = FixedText(card, "1 of " + spec.RepliesWaiting + (string.IsNullOrEmpty(spec.ReplyNext) ? "" : " · " + spec.ReplyNext + " next"),
                    12, UiTheme.Muted, new Vector2(pad + inner * .55f, -y), new Vector2(inner * .45f, 16f * s));
                count.name = CampaignPleaCountName;
                count.alignment = TextAlignmentOptions.Right;
                AutoSize(count, 9);
            }
            y += 17f * s;
            var title = FixedText(card, spec.ReplyTitle, 16, Paper, new Vector2(pad, -y), new Vector2(inner, 21f * s));
            title.name = "Plea title";
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) title.font = semibold;
            AutoSize(title, 12);
            y += 22f * s;
            // The answers keep the measured space for all consequences; what was said sits between.
            var message = NewText(card, spec.ReplyMessage ?? "", 13, UiTheme.Muted);
            message.name = "Plea message";
            message.fontStyle = FontStyles.Italic;
            message.overflowMode = TextOverflowModes.Ellipsis;
            AutoSize(message, 10);
            Anchor(message.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -y),
                new Vector2(inner, Mathf.Max(BoardLine(13), height - y - 6f * s - tiles - pad)));
            var replies = spec.Replies ?? new List<CampaignReply>();
            if (replies.Count == 0) return;
            float tileGap = 10f * s, each = (inner - (replies.Count - 1) * tileGap) / replies.Count;
            var grid = EndScreenKit.Box(EventChoicesName, card, pad, height - pad - tiles, inner, tiles);
            for (int i = 0; i < replies.Count; i++) CampaignReplyTile(grid, i * (each + tileGap), each, tiles, replies[i]);
        }

        /// <summary>
        /// The first story beat waiting, as a banner (decision 4): the arc's eyebrow, the beat's name,
        /// the moment in words, "Answer", which opens the beat as the step, and beside it what
        /// answering costs and when it closes. Nothing is answered here.
        /// </summary>
        private void BoardBanner(RectTransform row, float width, float height, float stepWords, FreeTimeBoardSpec spec)
        {
            float s = FontScale, pad = BoardPad * s, inner = width - 2f * pad, y = pad;
            var card = EndScreenKit.Box(StoryBannerName, row, 0f, 0f, width, height);
            EndScreenKit.Frame(card, PackArt.Pack8Section, 14f * s, new Color(Surface.r, Surface.g, Surface.b, .94f),
                new Color(UiTheme.Joke.r, UiTheme.Joke.g, UiTheme.Joke.b, .55f));
            float chip = 0f;
            if (spec.BeatFree)
            {
                // Said as a word as well as a colour: a beat that costs nothing to answer.
                chip = 56f * s;
                var free = HudPrimitives.Chip("Free chip", card, "Free", UiTheme.Allied, chip, 20f * s);
                Anchor(free, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-pad, -(pad - 2f * s)), free.sizeDelta);
                chip += 8f * s;
            }
            var eyebrow = FixedText(card, spec.BeatEyebrow ?? "STORY", 12, UiTheme.Joke, new Vector2(pad, -y), new Vector2(Mathf.Max(40f * s, inner - chip), 16f * s));
            eyebrow.name = "Banner eyebrow";
            eyebrow.characterSpacing = 4f;
            eyebrow.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(eyebrow, 9);
            y += 18f * s;
            var title = FixedText(card, spec.BeatTitle, 22, Paper, new Vector2(pad, -y), new Vector2(inner, 29f * s));
            title.name = "Banner title";
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) title.font = semibold;
            AutoSize(title, 14);
            y += 30f * s;
            float footRow = BoardBannerFoot * s;
            var story = NewText(card, spec.BeatNarrative ?? "", 14, UiTheme.Muted);
            story.name = "Banner narrative";
            story.fontStyle = FontStyles.Italic;
            story.overflowMode = TextOverflowModes.Ellipsis;
            AutoSize(story, 10);
            Anchor(story.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -y),
                new Vector2(inner, Mathf.Max(BoardLine(14), height - y - 8f * s - footRow - pad)));

            // The way in, at the foot's left-hand end, and what answering costs beside it. Pressing
            // it redraws the stage as the step, whose options stand right of its words column; at the
            // left, over that column, a double click's second press lands on the beat's words.
            float answerWidth = Mathf.Min(inner * .4f, 150f * s, Mathf.Max(60f * s, stepWords - pad - 8f * s));
            var rect = Panel(EpisodeDirector.AnswerBeatCaption, card, Surface);
            Anchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -(height - pad - footRow)), new Vector2(answerWidth, footRow));
            EndScreenKit.Frame(rect, PackArt.Pack8ButtonPrimary, 10f * s, UiTheme.ActionBlue, UiTheme.Glow);
            var answer = FinishButton(rect, EpisodeDirector.AnswerBeatCaption, spec.Answer ?? (() => { }));
            var words = answer.GetComponentInChildren<TMP_Text>();
            if (words != null)
            {
                words.fontSize = BoardSized(15);
                words.alignment = TextAlignmentOptions.Center;
                var bold = UiTheme.Font(UiTheme.Weight.SemiBold);
                if (bold != null) words.font = bold;
                AutoSize(words, 11);
            }
            var note = FixedText(card, spec.BeatNote ?? "", 12, spec.BeatFree ? UiTheme.Allied : UiTheme.Muted,
                new Vector2(pad + answerWidth + 12f * s, -(height - pad - footRow)), new Vector2(Mathf.Max(40f * s, inner - answerWidth - 12f * s), footRow));
            note.name = "Banner note";
            note.alignment = TextAlignmentOptions.MidlineLeft;
            AutoSize(note, 9);
        }

        /// <summary>
        /// The play in motion, in the hero's place when nothing is waiting on the player: its name and
        /// who it is about, how far it is as a bar or until when it is on offer, and the goal - the
        /// free-time panel's own card, laid across the hero.
        /// </summary>
        private void BoardPlay(RectTransform row, float width, float height, FreeTimeBoardSpec spec)
        {
            float s = FontScale, pad = BoardPad * s, inner = width - 2f * pad, y = pad;
            var card = EndScreenKit.Box(CurrentPlayCardName, row, 0f, 0f, width, height);
            EndScreenKit.Frame(card, PackArt.Pack8Section, 14f * s, new Color(Surface.r, Surface.g, Surface.b, .94f),
                new Color(Accent.r, Accent.g, Accent.b, .45f));
            var eyebrow = FixedText(card, spec.PlayEyebrow ?? "CURRENT PLAY", 12, Accent, new Vector2(pad, -y), new Vector2(inner, 16f * s));
            eyebrow.name = "Play eyebrow";
            eyebrow.characterSpacing = 4f;
            AutoSize(eyebrow, 9);
            y += 18f * s;
            var title = FixedText(card, spec.PlayTitle ?? "", 20, Paper, new Vector2(pad, -y), new Vector2(inner, 26f * s));
            title.name = "Play title";
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) title.font = semibold;
            AutoSize(title, 13);
            y += 28f * s;
            if (spec.PlayBar)
            {
                var label = FixedText(card, spec.PlayBarLabel ?? "", 14, Paper, new Vector2(pad, -y), new Vector2(inner * .7f, 19f * s));
                label.name = "Play bar label";
                if (semibold != null) label.font = semibold;
                var count = FixedText(card, spec.PlayHave + "/" + Mathf.Max(1, spec.PlayNeed), 14, Accent, new Vector2(pad + inner * .7f, -y), new Vector2(inner * .3f, 19f * s));
                count.name = "Progress count";
                count.alignment = TextAlignmentOptions.Right;
                var track = HudPrimitives.Fill("Play track", card, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .55f), 3);
                EndScreenKit.Place(track, pad, y + 23f * s, inner, 6f * s);
                float fill = Mathf.Clamp01(spec.PlayHave / (float)Mathf.Max(1, spec.PlayNeed));
                if (fill > 0f)
                {
                    var bar = HudPrimitives.Fill("Play bar", card, Accent, 3);
                    EndScreenKit.Place(bar, pad, y + 23f * s, inner * fill, 6f * s);
                }
                y += 32f * s;
            }
            else if (!string.IsNullOrEmpty(spec.PlayStatus))
            {
                float box = BoardPlayStatusBox();
                var offer = FixedText(card, spec.PlayStatus, 13, UiTheme.Gold, new Vector2(pad, -y), new Vector2(inner, box));
                offer.name = "Play status";
                AutoSize(offer, 10);
                y += box + 2f * s;
            }
            if (string.IsNullOrEmpty(spec.PlayGoal)) return;
            var goal = NewText(card, spec.PlayGoal, 13, UiTheme.Muted);
            goal.name = "Play goal";
            goal.overflowMode = TextOverflowModes.Ellipsis;
            AutoSize(goal, 10);
            Anchor(goal.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -y),
                new Vector2(inner, Mathf.Max(BoardLine(13), height - y - pad)));
        }

        /// <summary>
        /// The budget card, under the name the free-time head has always had: FREE TIME, the actions
        /// left large, what moving on loses, the week's rule and what costs and what is free, and the
        /// two ways to buy another action with their price in goodwill. When the height is short the
        /// rule and the copy step down a whole size or two together rather than the card growing.
        /// </summary>
        private void BoardBudget(RectTransform row, float x, float width, float height, FreeTimeBoardSpec spec)
        {
            float s = FontScale, pad = BoardPad * s, inner = width - 2f * pad;
            var card = EndScreenKit.Box(ScreenHeadName, row, x, 0f, width, height);
            EndScreenKit.Frame(card, PackArt.Pack8ActionsLeft, 12f * s, new Color(Surface.r, Surface.g, Surface.b, .94f),
                new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .5f));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            float left = BoardBudgetLeft(inner), y = pad;
            var probe = NewText(card, "", 12, Paper);
            var buys = spec.Buys ?? new List<BuyButton>();
            float buttons = BoardBuyRow(probe, buys, inner, out bool wraps);
            float area = Mathf.Max(BoardLine(12), height - pad - buttons - 8f * s - pad);

            var eyebrow = FixedText(card, "FREE TIME", 12, Accent, new Vector2(pad, -y), new Vector2(left, 16f * s));
            eyebrow.name = "Budget eyebrow";
            eyebrow.characterSpacing = 3f;
            if (semibold != null) eyebrow.font = semibold;
            AutoSize(eyebrow, 9);
            y += 18f * s;
            // The count large, and its word beside it: the number the top bar's Actions left shows.
            var count = FixedText(card, spec.ActionsLeft.ToString(), 32, spec.ActionsLeft > 0 ? UiTheme.Glow : UiTheme.Muted,
                new Vector2(pad, -y), new Vector2(left, BoardCountBox * s));
            count.name = ActionsLeftCountName;
            if (semibold != null) count.font = semibold;
            count.textWrappingMode = TextWrappingModes.NoWrap;
            float number = Mathf.Min(left * .5f, Mathf.Ceil(count.GetPreferredValues(count.text).x) + 8f * s);
            count.rectTransform.sizeDelta = new Vector2(number, BoardCountBox * s);
            var word = FixedText(card, spec.ActionsLeft == 1 ? "ACTION LEFT" : "ACTIONS LEFT", 12, Paper,
                new Vector2(pad + number, -(y + (BoardCountBox - 16f) * .5f * s)), new Vector2(Mathf.Max(30f * s, left - number), 16f * s));
            word.name = ActionsLeftWordName;
            word.characterSpacing = 2f;
            if (semibold != null) word.font = semibold;
            AutoSize(word, 9);
            y += (BoardCountBox + 2f) * s;
            if (!string.IsNullOrEmpty(spec.Unused))
            {
                // What moving on loses, in the warning colour: two lines at most, under the count.
                float room = Mathf.Max(BoardLine(12), Mathf.Min(2f * BoardLine(12) + 2f, pad + area - y));
                var unused = NewText(card, spec.Unused, 12, UiTheme.Warning);
                unused.name = UnusedActionsNoteName;
                unused.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(unused, 9);
                Anchor(unused.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -y), new Vector2(left, room));
            }

            // The week's rule and what costs and what is free, on the right, at one size: the largest
            // whole size at which both hold in the room together, down to the least the board's copy
            // takes. Stepping both down together keeps the rule whole - a share of the room by height
            // left the two-line rule under its second line, and it drew nothing.
            float rx = pad + left + 12f * s, rw = Mathf.Max(60f * s, inner - left - 12f * s);
            bool both = !string.IsNullOrEmpty(spec.Rule) && !string.IsNullOrEmpty(spec.Copy);
            float spacing = both ? 4f * s : 0f, size = BoardSized(12), ruleBox = 0f, copyNeed = 0f;
            for (; ; size -= 1f)
            {
                ruleBox = BoardMeasureAt(probe, spec.Rule, size, rw);
                copyNeed = BoardMeasureAt(probe, spec.Copy, size, rw);
                if (ruleBox + spacing + copyNeed <= area || size <= BoardCopyLeast) break;
            }
            probe.gameObject.SetActive(false);
            Destroy(probe.gameObject);
            if (ruleBox > 0f)
            {
                var rule = NewText(card, spec.Rule, 12, UiTheme.Muted);
                rule.name = BudgetRuleName;
                rule.fontSize = size;
                rule.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(rule, BoardCopyLeast);
                Anchor(rule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(rx, -pad), new Vector2(rw, ruleBox));
            }
            if (copyNeed > 0f)
            {
                // The room under the rule; at the least size, should the words still want more, the
                // auto-size and the ellipsis are the last word rather than the buttons under it.
                var copy = NewText(card, spec.Copy, 12, Paper);
                copy.name = BudgetCopyName;
                copy.fontSize = size;
                copy.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(copy, BoardCopyLeast);
                Anchor(copy.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(rx, -(pad + ruleBox + spacing)),
                    new Vector2(rw, Mathf.Max(BoardCopyLeast * 1.32f, area - spacing - ruleBox)));
            }

            // The two ways to buy an action, along the foot; once the house has given all it will,
            // the line that says so in their place.
            float by = height - pad - buttons;
            if (buys.Count == 0)
            {
                if (string.IsNullOrEmpty(spec.BoughtOut)) return;
                var out_ = FixedText(card, spec.BoughtOut, 12, UiTheme.Muted, new Vector2(pad, -by), new Vector2(inner, buttons));
                out_.name = BoughtOutName;
                out_.alignment = TextAlignmentOptions.MidlineLeft;
                AutoSize(out_, 9);
                return;
            }
            var strip = EndScreenKit.Box(BuyActionsName, card, pad, by, inner, buttons);
            float gap = 8f * s, each = (inner - (buys.Count - 1) * gap) / buys.Count;
            for (int i = 0; i < buys.Count; i++) BoardBuy(strip, i * (each + gap), each, buttons, wraps, buys[i]);
        }

        /// <summary>The least whole size the budget card's rule and copy step down to together.</summary>
        private const float BoardCopyLeast = 9f;

        /// <summary>
        /// One way to buy an action: the caption it has always had, word for word, over its price in
        /// goodwill - on one line at no less than <see cref="BoardBuyLeast"/>, or on two at its own
        /// size when <paramref name="wraps"/>.
        /// </summary>
        private void BoardBuy(RectTransform strip, float x, float width, float height, bool wraps, BuyButton buy)
        {
            float s = FontScale, pad = BoardBuyPad * s, top = BoardBuyCaptionTop * s, captionBox = BoardBuyCaptionBox(wraps);
            var rect = Chrome(buy.Caption, strip, UiTheme.Emphasis.Interactive);
            HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
            EndScreenKit.Place(rect, x, 0f, width, height);
            var button = Pressable(rect, buy.Choose ?? (() => { }));
            var colours = button.colors;
            colours.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colours.selectedColor = colours.highlightedColor;
            button.colors = colours;
            var caption = NewText(rect, buy.Caption, 12, Paper);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) caption.font = semibold;
            caption.textWrappingMode = wraps ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            AutoSize(caption, BoardBuyLeast);
            Anchor(caption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -top), new Vector2(width - 2f * pad, captionBox));
            if (string.IsNullOrEmpty(buy.Price)) return;
            var price = NewText(rect, buy.Price, 11, UiTheme.Gold);
            price.name = "Buy price";
            price.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(price, 9);
            Anchor(price.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -(top + captionBox)),
                new Vector2(width - 2f * pad, BoardBuyPriceBox * s));
        }

        // ------------------------------------------------------------ the story strip

        /// <summary>
        /// The week's story in one line: every other beat waiting as a chip that opens it, with More
        /// waiting for those past the chips' room; the play when the hero is not showing it; the
        /// threads and the storylines playing out; and the preparation banked at the right-hand end.
        /// The chips come first, over the step's words column (<paramref name="stepWords"/>), as Answer
        /// stands: what opens a beat redraws the stage as the step, and a double click's second press
        /// lands on the beat's words rather than on its options. The Have-Not line, word for word, under it.
        /// </summary>
        private void BoardStrip(RectTransform board, float y, float width, float stepWords, FreeTimeBoardSpec spec)
        {
            float s = FontScale, height = BoardStripHeight * s, pad = 10f * s, gap = 16f * s;
            var strip = EndScreenKit.Box(StoryStripName, board, 0f, y, width, height);
            EndScreenKit.Frame(strip, PackArt.Pack8CampaignRow, 8f * s, new Color(Surface.r, Surface.g, Surface.b, .9f));
            float x = pad, right = width - pad;
            if (!string.IsNullOrEmpty(spec.Preparation))
            {
                var banked = FixedText(strip, spec.Preparation, 12, UiTheme.Gold, Vector2.zero, new Vector2(200f * s, 17f * s));
                banked.name = PreparationName;
                banked.textWrappingMode = TextWrappingModes.NoWrap;
                float w = Mathf.Min(220f * s, Mathf.Ceil(banked.GetPreferredValues(banked.text).x) + 4f * s);
                Anchor(banked.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(right - w, -(height - 17f * s) * .5f), new Vector2(w, 17f * s));
                banked.alignment = TextAlignmentOptions.Right;
                right -= w + gap;
            }
            var waiting = spec.Waiting ?? new List<(string Caption, Action Open)>();
            if (waiting.Count > 0)
                x += BoardWaiting(strip, x, Mathf.Min(right, stepWords - 8f * s) - x, height, waiting) + gap;
            if (!string.IsNullOrEmpty(spec.StripPlay))
                x += BoardSegment(strip, CurrentPlayCardName, "PLAY", spec.StripPlay, x, Mathf.Min((right - x) * .4f, 340f * s), height) + gap;
            BoardSegment(strip, ThreadsCardName, "THREADS", spec.Threads, x, Mathf.Max(60f * s, right - x), height);
            if (string.IsNullOrEmpty(spec.HaveNot)) return;
            // What being a Have-Not costs, word for word, across the board under the strip.
            var line = FixedText(board, spec.HaveNot, 12, UiTheme.Warning, new Vector2(0f, -(y + height + BoardHaveNotGap * s)),
                new Vector2(width, BoardHaveNotHeight * s));
            line.name = FreeTimeHaveNotName;
            line.overflowMode = TextOverflowModes.Ellipsis;
            AutoSize(line, 9);
        }

        /// <summary>A segment of the story strip: its label in the accent and its words, no wider than <paramref name="most"/>. Returns the width it took.</summary>
        private float BoardSegment(RectTransform strip, string name, string label, string words, float x, float most, float height)
        {
            float s = FontScale;
            var segment = EndScreenKit.Box(name, strip, x, 0f, Mathf.Max(0f, most), height);
            var title = FixedText(segment, label, 11, Accent, Vector2.zero, new Vector2(90f * s, 15f * s));
            title.name = "Segment label";
            title.characterSpacing = 2f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) title.font = semibold;
            float labelWidth = Mathf.Min(most * .5f, Mathf.Ceil(title.GetPreferredValues(title.text).x) + 4f * s);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -(height - 15f * s) * .5f), new Vector2(labelWidth, 15f * s));
            float room = Mathf.Max(30f * s, most - labelWidth - 8f * s);
            var value = FixedText(segment, words ?? "", 13, Paper, Vector2.zero, new Vector2(room, 18f * s));
            value.name = "Segment value";
            value.textWrappingMode = TextWrappingModes.NoWrap;
            value.overflowMode = TextOverflowModes.Ellipsis;
            AutoSize(value, 10);
            float used = Mathf.Min(room, Mathf.Ceil(value.GetPreferredValues(value.text).x) + 4f * s);
            Anchor(value.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(labelWidth + 8f * s, -(height - 18f * s) * .5f), new Vector2(room, 18f * s));
            float taken = labelWidth + 8f * s + used;
            segment.sizeDelta = new Vector2(Mathf.Min(most, taken), height);
            return Mathf.Min(most, taken);
        }

        /// <summary>The waiting row's pills: their height, the gap between them, the inset of More waiting's words, and the narrowest a first chip is squeezed to.</summary>
        private const float BoardChipHeight = 22f, BoardChipGap = 6f, BoardMoreSide = 10f, BoardMoreBetween = 5f, BoardChipLeast = 80f;

        /// <summary>
        /// The beats waiting past the hero's, from <paramref name="x"/> and no wider than
        /// <paramref name="most"/>: the WAITING label; a chip for each, in order, while the row holds
        /// it and still has room for More waiting after it; and More waiting, with the count of the
        /// rest beside it, which opens the first of them. Returns the width it took.
        /// </summary>
        private float BoardWaiting(RectTransform strip, float x, float most, float height, IList<(string Caption, Action Open)> waiting)
        {
            float s = FontScale, chip = BoardChipHeight * s, spacing = BoardChipGap * s;
            var row = EndScreenKit.Box(WaitingBeatsName, strip, x, 0f, Mathf.Max(0f, most), height);
            var label = FixedText(row, "WAITING", 11, UiTheme.Joke, Vector2.zero, new Vector2(80f * s, 15f * s));
            label.name = "Segment label";
            label.characterSpacing = 2f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            float labelWidth = Mathf.Ceil(label.GetPreferredValues(label.text).x) + 4f * s;
            Anchor(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -(height - 15f * s) * .5f), new Vector2(labelWidth, 15f * s));
            var probe = NewText(row, "", 12, Paper);
            float more = BoardMoreWidth(probe, waiting.Count);
            probe.gameObject.SetActive(false);
            Destroy(probe.gameObject);

            float cx = labelWidth + spacing;
            int shown = 0;
            for (; shown < waiting.Count; shown++)
            {
                // Room for this chip, and for More waiting after it unless it is the last.
                bool last = shown == waiting.Count - 1;
                var pill = BoardChip(row, waiting[shown].Caption, waiting[shown].Open, chip, most - cx - (last ? 0f : spacing + more), shown == 0);
                if (pill == null) break;
                Anchor(pill, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(cx, -(height - chip) * .5f), pill.sizeDelta);
                cx += pill.sizeDelta.x + spacing;
            }
            if (shown < waiting.Count)
            {
                var rest = BoardMore(row, waiting.Count - shown, waiting[shown].Open, chip);
                Anchor(rest, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(cx, -(height - chip) * .5f), rest.sizeDelta);
                cx += rest.sizeDelta.x + spacing;
            }
            float used = Mathf.Max(labelWidth, cx - spacing);
            row.sizeDelta = new Vector2(used, height);
            return used;
        }

        /// <summary>
        /// A waiting beat as a chip: a small pressable pill, named and captioned by the beat's title,
        /// no wider than <paramref name="room"/>. Null, with nothing left behind, when it does not fit -
        /// unless it is the row's <paramref name="first"/> and the room still reads, when it is squeezed.
        /// </summary>
        private RectTransform BoardChip(RectTransform parent, string caption, Action open, float height, float room, bool first)
        {
            float s = FontScale;
            var rect = Panel(caption, parent, new Color(UiTheme.Joke.r, UiTheme.Joke.g, UiTheme.Joke.b, .16f), Mathf.RoundToInt(height * .5f) - 1);
            UiTheme.AddBorder(rect, Mathf.RoundToInt(height * .5f) - 1, new Color(UiTheme.Joke.r, UiTheme.Joke.g, UiTheme.Joke.b, .7f));
            var label = NewText(rect, caption, 12, Paper);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            if (medium != null) label.font = medium;
            float width = Mathf.Min(260f * s, Mathf.Ceil(label.GetPreferredValues(label.text).x) + 24f * s);
            if (width > room)
            {
                if (!first || room < BoardChipLeast * s)
                {
                    rect.gameObject.SetActive(false);
                    Destroy(rect.gameObject);
                    return null;
                }
                width = room;
            }
            rect.sizeDelta = new Vector2(width, height);
            Stretch(label.rectTransform, 10f, 2f, 10f, 2f);
            AutoSize(label, 9);
            Pressable(rect, open ?? (() => { }));
            return rect;
        }

        /// <summary>The width More waiting takes with <paramref name="count"/> beside it, measured on <paramref name="probe"/>.</summary>
        private float BoardMoreWidth(TMP_Text probe, int count)
        {
            float s = FontScale;
            return 2f * BoardMoreSide * s + BoardWidthOf(probe, "+" + count, 12, UiTheme.Font(UiTheme.Weight.SemiBold)) + 2f * s
                + BoardMoreBetween * s + BoardWidthOf(probe, MoreWaitingCaption, 12, UiTheme.Font(UiTheme.Weight.Medium)) + 2f * s;
        }

        /// <summary>
        /// More waiting: a pill like the chips, with how many beats are past the row's room as a label
        /// of its own beside the caption, so the caption is the same whatever the count, and the
        /// caption the control's first label, as every control's is. Opens the first of them.
        /// </summary>
        private RectTransform BoardMore(RectTransform parent, int count, Action open, float height)
        {
            float s = FontScale, side = BoardMoreSide * s, box = height - 4f;
            var rect = Panel(MoreWaitingCaption, parent, new Color(Accent.r, Accent.g, Accent.b, .14f), Mathf.RoundToInt(height * .5f) - 1);
            UiTheme.AddBorder(rect, Mathf.RoundToInt(height * .5f) - 1, new Color(Accent.r, Accent.g, Accent.b, .6f));
            var caption = NewText(rect, MoreWaitingCaption, 12, Paper);
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            if (medium != null) caption.font = medium;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            var tally = NewText(rect, "+" + count, 12, UiTheme.Joke);
            tally.name = MoreWaitingCountName;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) tally.font = semibold;
            tally.textWrappingMode = TextWrappingModes.NoWrap;
            tally.alignment = TextAlignmentOptions.MidlineLeft;
            float tallyWidth = Mathf.Ceil(tally.GetPreferredValues(tally.text).x) + 2f * s;
            float captionWidth = Mathf.Ceil(caption.GetPreferredValues(caption.text).x) + 2f * s;
            Anchor(tally.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(side, -2f), new Vector2(tallyWidth, box));
            Anchor(caption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(side + tallyWidth + BoardMoreBetween * s, -2f), new Vector2(captionWidth, box));
            rect.sizeDelta = new Vector2(2f * side + tallyWidth + BoardMoreBetween * s + captionWidth, height);
            Pressable(rect, open ?? (() => { }));
            return rect;
        }

        // ------------------------------------------------------------ the heads

        /// <summary>
        /// A section's head across the board: a glyph, the title in the heading blue, and at the
        /// resting size a line of what the section is for, short of <paramref name="reserve"/> at the
        /// right-hand end for the pager.
        /// </summary>
        private void BoardHead(RectTransform board, string name, string glyph, string title, string line, float y, float width, float height, bool withLine, float reserve)
        {
            float s = FontScale, text = 0f;
            var row = EndScreenKit.Box(name, board, 0f, y, width, height);
            var mark = HudPrimitives.Glyph("Section mark", row, glyph, Accent, new Vector2(0f, -1f * s), 24f * s);
            if (mark != null) text = 32f * s;
            float room = Mathf.Max(80f * s, width - text - reserve);
            var head = FixedText(row, title, 16, UiTheme.Heading, new Vector2(text, -2f * s), new Vector2(room, 22f * s));
            head.name = "Head title";
            head.characterSpacing = 2f;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) head.font = semibold;
            AutoSize(head, 11);
            if (!withLine || string.IsNullOrEmpty(line)) return;
            var under = FixedText(row, line, 13, UiTheme.Muted, new Vector2(text, -25f * s), new Vector2(room, 18f * s));
            under.name = "Head line";
            under.overflowMode = TextOverflowModes.Ellipsis;
            AutoSize(under, 10);
        }

        /// <summary>
        /// The pager at the talk head's right-hand end, when the house runs past one page: where the
        /// page is, and a way back and on, each only where there is somewhere to go. Returns the width
        /// it took. Pressing one is view state.
        /// </summary>
        private float BoardPager(RectTransform board, float y, float width, int page, int pages, FreeTimeBoardSpec spec)
        {
            if (pages <= 1) return 0f;
            float s = FontScale, gap = 8f * s, height = 26f * s, x = width;
            var row = EndScreenKit.Box("Pager", board, 0f, y, width, height);
            // Made in reading order, the way back before the way on, and laid from the right.
            var pills = new List<RectTransform>();
            if (page > 0)
            {
                int previous = page - 1;
                pills.Add((RectTransform)FilterPill(row, CampaignPreviousCaption, false, () => spec.ChoosePage?.Invoke(previous), height).transform);
            }
            if (page < pages - 1)
            {
                int next = page + 1;
                pills.Add((RectTransform)FilterPill(row, CampaignNextCaption, false, () => spec.ChoosePage?.Invoke(next), height).transform);
            }
            for (int i = pills.Count - 1; i >= 0; i--)
            {
                x -= pills[i].sizeDelta.x;
                Anchor(pills[i], new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, 0f), pills[i].sizeDelta);
                x -= gap;
            }
            float where = 96f * s;
            x -= where;
            var at = FixedText(row, "Page " + (page + 1) + " of " + pages, 12, UiTheme.Muted, new Vector2(x, -(height - 17f * s) * .5f), new Vector2(where, 17f * s));
            at.name = BoardPageName;
            at.alignment = TextAlignmentOptions.Right;
            AutoSize(at, 9);
            return width - x + gap;
        }

        // ------------------------------------------------------------ the cards

        /// <summary>The page's houseguests as one row of cards, under the grid name the house's cards have always had.</summary>
        private void BoardCards(RectTransform board, float y, float width, float height, IList<HouseCard> cards, float cardWidth, float gap,
            FreeTimeBoardSpec spec, EpisodeState state)
        {
            var row = EndScreenKit.Box(HouseCardsName, board, 0f, y, width, height);
            float s = FontScale, photoHeight = Mathf.Max(BoardPhotoLeast * s, height - BoardCardFoot * s);
            float photoWidth = Mathf.Min(cardWidth - 12f * s, photoHeight);
            for (int i = 0; i < cards.Count; i++)
                BoardCard(row, i * (cardWidth + gap), cardWidth, height, photoWidth, photoHeight, cards[i], spec, state);
        }

        /// <summary>
        /// A houseguest's card (mockup 87): the photo with the week's role on its foot, the name - the
        /// card's own control, which opens their screen - where the player stands with them by their
        /// own reading, and the walk over to talk. Never their mood: a mood is not where you stand.
        /// </summary>
        private void BoardCard(RectTransform row, float x, float width, float height, float photoWidth, float photoHeight,
            HouseCard entry, FreeTimeBoardSpec spec, EpisodeState state)
        {
            var actor = state.Find(entry.Id);
            if (actor == null) return;
            float s = FontScale, pad = 5f * s, inner = width - 2f * pad;
            string id = entry.Id;
            var card = EndScreenKit.Box("Houseguest · " + actor.name, row, x, 0f, width, height);
            string skin = id == state.hohId ? PackArt.Pack8HouseguestHoh
                : state.nominees != null && state.nominees.Contains(id) ? PackArt.Pack8HouseguestNominee : PackArt.Pack8HouseguestNeutral;
            EndScreenKit.Frame(card, skin, 10f * s, new Color(Surface.r, Surface.g, Surface.b, .92f));
            var picture = HudPrimitives.RectPortrait(card, "Photo", Portrait(id), actor, new Vector2(photoWidth, photoHeight), 7);
            picture.anchorMin = picture.anchorMax = new Vector2(.5f, 1f);
            picture.pivot = new Vector2(.5f, 1f);
            picture.anchoredPosition = new Vector2(0f, -pad);
            if (!string.IsNullOrEmpty(entry.Role))
            {
                float pillWidth = Mathf.Clamp(entry.Role.Length * 7.5f + 16f, 42f, 90f) * s;
                CampaignPill(picture, entry.Role, entry.RoleColour, Mathf.Max(0f, (photoWidth - pillWidth) * .5f), photoHeight - 21f * s);
            }
            float y = pad + photoHeight + 3f * s;
            // The name is the card's own control: it opens their screen. Named by the name, which is
            // how a test and a screen reader find it.
            var seat = Panel(actor.name, card, new Color(0f, 0f, 0f, 0f), 6);
            Anchor(seat, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -y), new Vector2(inner, 21f * s));
            var button = Pressable(seat, () => spec.Pick?.Invoke(id));
            var colours = button.colors;
            colours.normalColor = new Color(1f, 1f, 1f, 0f);
            colours.highlightedColor = new Color(1f, 1f, 1f, .12f);
            colours.selectedColor = colours.highlightedColor;
            colours.pressedColor = new Color(1f, 1f, 1f, .2f);
            button.colors = colours;
            var name = NewText(seat, actor.name, 15, Paper);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            name.alignment = TextAlignmentOptions.Center;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(name.rectTransform, 2f, 0f, 2f, 0f);
            AutoSize(name, 10);
            y += 21f * s;
            var kind = RelationshipWeb.KindOf(state, id);
            var standing = FixedText(card, RelationshipWeb.StandingWord(kind), 12,
                kind == RelationshipWeb.Kind.Neutral ? UiTheme.Muted : RelationshipWeb.StandingColour(kind),
                new Vector2(pad, -y), new Vector2(inner, 16f * s));
            standing.name = "Standing";
            standing.alignment = TextAlignmentOptions.Center;
            AutoSize(standing, 9);
            y += 19f * s;
            CampaignTalkButton(card, actor, pad, y, inner, 28f * s, spec.Talk);
        }

        // ------------------------------------------------------------ the moves

        /// <summary>The other ways to spend the time as tiles, under the grid name the moves have always had.</summary>
        private void BoardMoves(RectTransform board, float y, float width, IList<BoardTile> tiles, int perRow, float tileHeight, float gap)
        {
            if (tiles.Count == 0) return;
            int rows = Mathf.CeilToInt(tiles.Count / (float)perRow);
            var grid = EndScreenKit.Box(HouseMovesName, board, 0f, y, width, rows * tileHeight + (rows - 1) * gap);
            float each = (width - (perRow - 1) * gap) / perRow;
            for (int i = 0; i < tiles.Count; i++)
                BoardMove(grid, (i % perRow) * (each + gap), (i / perRow) * (tileHeight + gap), each, tileHeight, tiles[i]);
        }

        /// <summary>
        /// One way to spend the time: its glyph, the caption it is found by on up to two lines, and
        /// along its foot what it costs - or that it is free - and its risk or its odds. A move that
        /// costs an action when none is left is drawn locked, with the reason where the cost was.
        /// </summary>
        private void BoardMove(RectTransform grid, float x, float y, float width, float height, BoardTile tile)
        {
            float s = FontScale, pad = 8f * s, glyph = 26f * s;
            var emphasis = tile.Locked ? UiTheme.Emphasis.Resting : UiTheme.Emphasis.Interactive;
            var rect = Chrome(tile.Caption, grid, emphasis);
            HudEmphasis.Promote(rect, emphasis);
            EndScreenKit.Place(rect, x, y, width, height);
            var button = Pressable(rect, tile.Choose ?? (() => { }));
            button.interactable = !tile.Locked;
            var colours = button.colors;
            colours.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colours.selectedColor = colours.highlightedColor;
            button.colors = colours;
            Image mark = tile.Locked
                ? EndScreenKit.Picture("Choice mark", rect, PackArt.Pack8IconLock, null, UiTheme.Muted, new Vector2(pad + glyph * .5f, -(pad + glyph * .5f)), glyph)
                : HudPrimitives.Glyph("Choice mark", rect, tile.Glyph ?? "journal", Accent, new Vector2(pad, -pad), glyph);
            float text = pad + (mark != null ? glyph + 8f * s : 0f);
            var caption = NewText(rect, tile.Caption, 14, tile.Locked ? UiTheme.Muted : Paper);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) caption.font = semibold;
            AutoSize(caption, 10);
            // Two lines at the caption's size, in a box 1.3 times each: the longest caption, "Call a
            // house meeting and rally the room", is two lines on the narrowest tile.
            Anchor(caption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(text, -(pad - 1f * s)),
                new Vector2(Mathf.Max(40f * s, width - text - pad), 2f * BoardSized(14) * 1.3f));
            float footBox = 16f * s, footY = height - pad - footBox, corner = 0f;
            if (!tile.Locked && !string.IsNullOrEmpty(tile.Corner))
            {
                var word = FixedText(rect, tile.Corner, 11, tile.CornerTint, Vector2.zero, new Vector2(width * .5f, footBox));
                word.name = "Tile corner";
                word.textWrappingMode = TextWrappingModes.NoWrap;
                corner = Mathf.Min(width * .5f, Mathf.Ceil(word.GetPreferredValues(word.text).x) + 4f * s);
                Anchor(word.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(width - pad - corner, -(footY + 1f * s)), new Vector2(corner, footBox - 1f * s));
                word.alignment = TextAlignmentOptions.Right;
                AutoSize(word, 9);
                corner += 6f * s;
            }
            var cost = FixedText(rect, tile.Cost ?? "", 12, tile.Locked ? UiTheme.Muted : tile.Free ? UiTheme.Allied : Paper,
                new Vector2(pad, -footY), new Vector2(Mathf.Max(30f * s, width - 2f * pad - corner), footBox));
            cost.name = "Tile foot";
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            if (medium != null) cost.font = medium;
            cost.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(cost, 9);
        }

        // ------------------------------------------------------------ the footer's secondary

        /// <summary>
        /// The footer's secondary slot in the mockup's blue, with the mockup's words for it as a
        /// headline of their own over the caption: the caption keeps its words and stays the button's
        /// first label, so a lookup by caption and the footer's measure still find it. Only the
        /// render's secondary is dressed.
        /// </summary>
        public void DressSecondary(Button button, string headline)
        {
            if (button == null || footerSecondary == null || button.transform != footerSecondary) return;
            float s = FontScale;
            EndScreenKit.Frame(footerSecondary, PackArt.Pack8ButtonPrimary, 12f * s, UiTheme.ActionBlue, UiTheme.Glow);
            var caption = button.GetComponentInChildren<TMP_Text>();
            if (string.IsNullOrEmpty(headline) || caption == null) return;
            // Each in a box 1.3 times the size it is drawn at, which is the rounded size at the larger text.
            float headlineBox = BoardSized(13) * 1.3f, captionBox = BoardSized(14) * 1.3f;
            var words = NewText(footerSecondary, headline, 13, Color.white);
            words.name = SecondaryHeadlineName;
            words.characterSpacing = 2f;
            words.alignment = TextAlignmentOptions.Center;
            words.textWrappingMode = TextWrappingModes.NoWrap;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) words.font = semibold;
            AutoSize(words, 10);
            var top = words.rectTransform;
            top.anchorMin = new Vector2(0f, 1f); top.anchorMax = new Vector2(1f, 1f); top.pivot = new Vector2(.5f, 1f);
            top.offsetMin = new Vector2(12f, -(6f * s + headlineBox));
            top.offsetMax = new Vector2(-12f, -6f * s);
            caption.fontSize = BoardSized(14);
            caption.alignment = TextAlignmentOptions.Center;
            caption.color = new Color(1f, 1f, 1f, .85f);
            AutoSize(caption, 11);
            var line = caption.rectTransform;
            line.anchorMin = new Vector2(0f, 0f); line.anchorMax = new Vector2(1f, 0f); line.pivot = new Vector2(.5f, 0f);
            line.offsetMin = new Vector2(12f, 7f * s);
            line.offsetMax = new Vector2(-12f, 7f * s + captionBox);
            LayoutStrategyFooter();
        }

        /// <summary>
        /// Puts the keyboard on the control named <paramref name="name"/> once this render's controls
        /// are wired, rather than on the column's first: a screen opened by a press whose next press
        /// must not answer anything - a story beat opened from free time's board opens on "Back to
        /// free time", so a second Enter goes back rather than choosing the beat's first option.
        /// </summary>
        public void FocusWhenWired(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            // What the selection was as this render began, read as Begin reads it, to know later
            // whether the focus asked for here has landed or the player has moved on.
            var events = EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            focusFrom = selected != null && canvas != null && selected.transform.IsChildOf(canvas.transform) ? selected.name : null;
            focusWanted = name;
            preferredSelection = name;
            restoreSelection = true;
        }

        /// <summary>The control <see cref="FocusWhenWired"/> last asked for until it lands, and the selection's name when it was asked.</summary>
        private string focusWanted, focusFrom;

        /// <summary>
        /// Asks again for a focus the last render asked for, when this render came before the frame
        /// wired it: a houseguest's body finishing its assembly renders the HUD at any moment, and its
        /// Begin reads the press's own control as the selection - a control the new screen has not
        /// got - so the focus would fall to the column's first control, the very option or move the
        /// asking kept it off. Begin reading back the control asked for means it has landed; reading
        /// anything else, that the player has moved the keyboard. Called by every free-time render
        /// before it asks for a focus of its own.
        /// </summary>
        public void KeepFocusAsked()
        {
            if (focusWanted == null) return;
            if (preferredSelection == focusWanted || preferredSelection != focusFrom) { focusWanted = null; return; }
            preferredSelection = focusWanted;
            restoreSelection = true;
        }

        /// <summary>Drops a focus asked for and not yet landed: the screen it was asked on has closed.</summary>
        public void DropFocusAsked() => focusWanted = null;

        // ------------------------------------------------------------ the pointer hold

        /// <summary>
        /// How long, in real time, a panel redrawn under the pointer takes no pointer press: longer
        /// than the gap inside a double click, shorter than a second press anybody means.
        /// </summary>
        public const float PointerHoldSeconds = .45f;

        /// <summary>When the hold on pointer presses ends, in unscaled real time; zero when none is running.</summary>
        private float pointerHeldUntil;

        /// <summary>Whether the panel is taking no pointer presses now. A read for tests.</summary>
        public bool PointerHeld => modal != null && pointerHeldUntil > 0f && Time.realtimeSinceStartup < pointerHeldUntil;

        /// <summary>
        /// Holds pointer presses off the whole panel, footer included, for <paramref name="seconds"/>
        /// of real time: free time's board has just replaced a step under the pointer, or a step's
        /// next press has replaced its options, and the second press of a double click would land
        /// on whatever stands there now - a way to buy time, a move or the way on. Only raycasts are
        /// held: the controls stay interactable and look as they did, so the keyboard and a press
        /// made in code are untouched.
        /// </summary>
        public void HoldPointerOffPanel(float seconds)
        {
            pointerHeldUntil = Mathf.Max(pointerHeldUntil, Time.realtimeSinceStartup + seconds);
            ApplyPointerHold();
        }

        /// <summary>Puts a hold still running on the panel as this render built it: a redraw builds a new panel.</summary>
        public void KeepPointerHold() => ApplyPointerHold();

        /// <summary>Ends a hold now: the panel closed, and the next one opens to a pointer that has moved on.</summary>
        public void EndPointerHold()
        {
            if (pointerHeldUntil <= 0f) return;
            pointerHeldUntil = 0f;
            ApplyPointerHold();
        }

        /// <summary>
        /// The panel's raycasts while a hold runs, and given back once its time is up: on the panel's
        /// own canvas group, the one a reveal fades - alpha is the reveal's, blocking is the hold's.
        /// Called from LateUpdate too, so a hold lifts on time without a render.
        /// </summary>
        private void ApplyPointerHold()
        {
            bool held = pointerHeldUntil > 0f && Time.realtimeSinceStartup < pointerHeldUntil;
            if (modal == null)
            {
                if (!held) pointerHeldUntil = 0f;
                return;
            }
            var group = modal.GetComponent<CanvasGroup>();
            if (group == null)
            {
                if (!held) { pointerHeldUntil = 0f; return; }
                group = modal.gameObject.AddComponent<CanvasGroup>();
            }
            group.blocksRaycasts = !held;
            if (!held) pointerHeldUntil = 0f;
        }
    }
}
