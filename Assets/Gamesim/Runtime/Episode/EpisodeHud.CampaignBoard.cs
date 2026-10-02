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
    /// The campaign as one screen on the strategy stage (PACK8-PASS-PLAN B4, mockup 83). It was a
    /// column of 21-point paragraphs - the week's roles, a title, a meter, the block as faces, the
    /// votes as cards, the storylines - that scrolled on its own and collapsed into a house event's
    /// 403-unit band the moment a nominee came pleading (the owner's screenshots 77 to 80).
    ///
    /// <para>Now it is laid for the frame, in fixed-size type, as one board: the plea when one is
    /// waiting, then the block as heroes beside the week's title and situation, a row of tabs, the
    /// chosen tab's body, and the goals, the intel and a tip along the foot. When the height is
    /// short - a plea, a story beat, the larger text on a wide screen - the three foot cards fold
    /// into a tab of their own rather than the board growing a scroll, and when what is waiting
    /// leaves no room for a tab at all, the tabs give way to a line saying where they went until
    /// it is answered. Every word on it is the
    /// player's own reading, a public fact, a row the player owns, or existing rule copy; it
    /// commits nothing, and the tab and the page are view state the director holds.</para>
    ///
    /// <para>The pinned names stay: the 'Ceremony title' reads "Campaign", the grid is 'Campaign
    /// voters', each voter a 'Voter · {name}' card whose one 'Read' line is its direct child, the
    /// plea's answers are in 'House event choices', and every control keeps its caption.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The board's parts, by the names a test finds them by.</summary>
        public const string CampaignBoardName = "Campaign board", CampaignHeroRowName = "Campaign hero",
            CampaignHeadlineName = "Campaign headline", CampaignSituationName = "Campaign situation",
            CampaignActionsName = "Campaign actions", CampaignPleaName = "Campaign plea", CampaignPleaCountName = "Plea count",
            CampaignTabsName = "Campaign tabs", CampaignBodyName = "Campaign tab body", CampaignCardsName = "Campaign cards",
            CampaignGoalsName = "Campaign goals", CampaignIntelName = "Campaign intel", CampaignTipName = "Campaign tip",
            CampaignOutlookName = "Campaign outlook", CampaignHaveNotName = "Campaign have-not line", CampaignTabLineName = "Tab line",
            CampaignWaitingName = "Campaign waiting";

        /// <summary>What the board says in place of its tabs while something waiting above it leaves no room for them.</summary>
        public const string CampaignWaitingLine = "The rest of the campaign comes back here once you answer what is waiting above.";

        /// <summary>The tabs' captions, new with the board and found by these words.</summary>
        public const string CampaignTabTalk = "Talk to Houseguests", CampaignTabIntel = "Relationship Intel",
            CampaignTabPoints = "Talking Points", CampaignTabStories = "Storylines", CampaignTabOutlook = "Voting Outlook",
            CampaignTabGoals = "Goals and intel";

        /// <summary>The pager's captions, shown only when a tab's houseguests run past one page.</summary>
        public const string CampaignPreviousCaption = "Previous houseguests", CampaignNextCaption = "Next houseguests";

        /// <summary>What the Head of Household's card says in place of a read: they vote only to break a tie.</summary>
        public const string CampaignTieLine = "Votes only on a tie";

        /// <summary>The tabs of the board, in their order along the row.</summary>
        public enum CampaignTab { Talk, Intel, Points, Stories, Outlook, Goals }

        /// <summary>One answer to the plea on the board: its caption, what it means, how far it could rebound, and what it commits.</summary>
        public sealed class CampaignReply
        {
            public string Caption, Key, Description, Risk;
            public Action Choose;
        }

        /// <summary>
        /// What the director hands the board: the words that come from the rules or the director's
        /// view state, and what each control does. The board reads the rest - the faces, the
        /// player's own readings, the vote read, the week's roles - from the committed state itself.
        /// </summary>
        public sealed class CampaignBoardSpec
        {
            public string Headline, Line, Tip;
            public CampaignTab Tab;
            public int Page;
            public bool More, HaveNot;
            /// <summary>Whether something is waiting on the player - a plea, a beat, a house event - so the foot cards fold into a tab.</summary>
            public bool Folded;
            /// <summary>The plea waiting, oldest first: its heading, what they said, how many are waiting, and who comes next.</summary>
            public string PleaTitle, PleaMessage, PleaNext;
            public int PleasWaiting;
            public IList<CampaignReply> Replies;
            public IList<string> TalkingPoints;
            public IList<(string Title, string Line)> Stories;
            public IList<CampaignBrief.Goal> Goals;
            public IList<CampaignBrief.Intel> Intel;
            public Action<string> Talk;
            public Action<CampaignTab> ChooseTab;
            public Action<int> ChoosePage;
            public Action ToggleMore;
        }

        // Heights and widths at the resting text size; each is multiplied by the text scale.
        private const float CampaignHeroHeight = 132f, CampaignTabsHeight = 34f, CampaignLineHeight = 26f, CampaignCardsHeight = 128f;
        /// <summary>A voter card is its photo and this much under and around it: the name, your reading and its bar, the read, the talk button.</summary>
        private const float CampaignCardChrome = 115f;
        /// <summary>A voter card's photo: never shorter than the first, folded below the second, never taller than the third.</summary>
        private const float CampaignPhotoLeast = 40f, CampaignPhotoUnfolded = 64f, CampaignPhotoMost = 100f;
        /// <summary>
        /// A voter card's width: the tallest photo, square, and its margins. It was clamped between
        /// this and 150 by the photo's height, but the photo is never taller than 100, so the clamp
        /// always gave this.
        /// </summary>
        private const float CampaignCardWidth = 112f;
        private const float CampaignGap = 10f;
        /// <summary>The key the conversations left are remembered under between renders, so their bar drains: the caption of the meter the chip replaced.</summary>
        private const string CampaignActionsMeter = "Interactions available";

        /// <summary>
        /// Lays the campaign's column at the stage's whole width, before the director draws anything
        /// into it: the reading column's cap is for paragraphs, and a house event's tiles measure the
        /// column as they are built.
        /// </summary>
        public void CampaignColumn() => UncapContent();

        /// <summary>
        /// Draws the campaign's board into the stage's column and returns the tab it shows: the one
        /// asked for, except the folded cards' tab when there is room for the cards themselves. While
        /// the board gives way to what is waiting above it, that is the tab it comes back on.
        /// </summary>
        public CampaignTab CampaignBoard(CampaignBoardSpec spec)
        {
            var state = director != null ? director.Snapshot : null;
            if (content == null || spec == null || state == null) return CampaignTab.Talk;
            // The board is laid for the stage's whole width: the reading column's cap is for
            // paragraphs, and the board has none. Uncapped before measuring what is above it.
            UncapContent();
            float s = FontScale, width = ContentWidth(), gap = CampaignGap * s;
            float room = CampaignRoom();
            var sheet = VoteRead.Read(state);

            var board = new GameObject(CampaignBoardName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            board.SetParent(content, false);
            float y = 0f;
            // Somebody who came to the player comes first, as it always has, and is where the panel
            // opens: its answers are the board's first controls.
            if (spec.Replies != null && spec.Replies.Count > 0) y += CampaignPlea(board, width, spec) + gap;

            float hero = CampaignHeroHeight * s, tabs = CampaignTabsHeight * s, line = CampaignLineHeight * s, cards = CampaignCardsHeight * s;
            float haveNot = spec.HaveNot ? 22f * s : 0f;
            float fixedPart = y + hero + gap + tabs + 8f * s + haveNot + line + 6f * s;
            float least = (CampaignCardChrome + CampaignPhotoLeast) * s;
            var size = board.GetComponent<LayoutElement>();
            // Something waiting above the board - a story beat, a house event, a plea - is the step
            // (PACK8-PASS-PLAN decision 7). When it leaves no room for a tab's body even with the
            // foot cards folded, the board gives way to it rather than holding its least height and
            // pushing the column past the stage: the tabs make way for a line saying where they
            // went, and the heroes go too when even they would not fit. Answered, it is gone and
            // the whole board is back. With nothing waiting, the board keeps its least height.
            if (spec.Folded && room - fixedPart < least)
            {
                size.minHeight = size.preferredHeight = CampaignWaiting(board, y, width, room, state, sheet, spec);
                return spec.Tab;
            }
            bool folded = spec.Folded || room - fixedPart - gap - cards < (CampaignCardChrome + CampaignPhotoUnfolded) * s;
            float body = Mathf.Clamp(room - fixedPart - (folded ? 0f : gap + cards), least, (CampaignCardChrome + CampaignPhotoMost) * s);
            var tab = spec.Tab == CampaignTab.Goals && !folded ? CampaignTab.Talk : spec.Tab;

            CampaignHeroRow(board, y, width, hero, state, sheet, spec);
            y += hero + gap;
            CampaignTabs(board, y, width, tabs, spec, tab, folded);
            y += tabs + 8f * s;
            if (spec.HaveNot) y += CampaignHaveNot(board, y, width);
            var bodyRect = EndScreenKit.Box(CampaignBodyName, board, 0f, y, width, line + 6f * s + body);
            switch (tab)
            {
                case CampaignTab.Intel: CampaignIntelTab(bodyRect, width, line, body, state, spec); break;
                case CampaignTab.Points: CampaignPointsTab(bodyRect, width, line, body, spec); break;
                case CampaignTab.Stories: CampaignStoriesTab(bodyRect, width, line, body, spec); break;
                case CampaignTab.Outlook: CampaignOutlookTab(bodyRect, width, line, body, state, sheet, spec); break;
                case CampaignTab.Goals:
                    CampaignTabLine(bodyRect, width, line, "Your goals this week, what you have learned, and a tip.");
                    CampaignCards(bodyRect, line + 6f * s, width, Mathf.Min(body, cards * 1.5f), state, spec);
                    break;
                default: CampaignTalkTab(bodyRect, width, line, body, state, sheet, spec); break;
            }
            y += line + 6f * s + body;
            if (!folded)
            {
                y += gap;
                CampaignCards(board, y, width, cards, state, spec);
                y += cards;
            }
            size.minHeight = size.preferredHeight = y;
            return tab;
        }

        /// <summary>
        /// The board while something waiting above it leaves no room for a tab: the heroes when they
        /// still fit, then a row with the line that says where the rest went and "More ways to
        /// campaign" at its end, so the house's other ways are as near as they always are, and the
        /// Have-Not line under it. Returns the board's height.
        /// </summary>
        private float CampaignWaiting(RectTransform board, float y, float width, float room, EpisodeState state, VoteRead.Sheet sheet, CampaignBoardSpec spec)
        {
            float s = FontScale, gap = CampaignGap * s, hero = CampaignHeroHeight * s, row = CampaignTabsHeight * s;
            float haveNot = spec.HaveNot ? 8f * s + 22f * s : 0f;
            if (room - y - (hero + gap + row + haveNot) >= 0f)
            {
                CampaignHeroRow(board, y, width, hero, state, sheet, spec);
                y += hero + gap;
            }
            var strip = EndScreenKit.Box(CampaignWaitingName, board, 0f, y, width, row);
            var more = FilterPill(strip, spec.More ? EpisodeDirector.CampaignLessCaption : EpisodeDirector.CampaignMoreCaption, spec.More,
                () => spec.ToggleMore?.Invoke(), row);
            var moreRect = (RectTransform)more.transform;
            Anchor(moreRect, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, moreRect.sizeDelta);
            CampaignTabLine(strip, Mathf.Max(100f * s, width - moreRect.sizeDelta.x - 12f * s), row, CampaignWaitingLine);
            // Built first to measure what it leaves the line, and read after it, as it stands.
            moreRect.SetAsLastSibling();
            y += row;
            if (spec.HaveNot) y += 8f * s + CampaignHaveNot(board, y + 8f * s, width);
            return y;
        }

        /// <summary>What being a Have-Not costs, beside the conversations it costs one of, as a line across the board. Returns the height it takes.</summary>
        private float CampaignHaveNot(RectTransform board, float y, float width)
        {
            float s = FontScale;
            var cost = FixedText(board, EpisodeDirector.HaveNotLine, 12, UiTheme.Warning, new Vector2(0f, -y), new Vector2(width, 18f * s));
            cost.name = CampaignHaveNotName;
            cost.overflowMode = TextOverflowModes.Ellipsis;
            AutoSize(cost, 9);
            return 22f * s;
        }

        /// <summary>
        /// The height the scroll has left for the board: the stage's viewport once the footer is
        /// pinned under it, less its padding and whatever the column already holds above the board
        /// - a story beat, a house event - measured as the layout will lay it.
        /// </summary>
        private float CampaignRoom()
        {
            if (modal == null || modalScroll == null || content == null) return 600f * FontScale;
            var scroll = (RectTransform)modalScroll.transform;
            float top = -scroll.offsetMax.y;
            // The footer's row is pinned after the board is drawn; the scroll will stop above it.
            float foot = PinnedMargin + pinnedNoteHeight + PinnedHeight * FontScale + 10f;
            float viewport = modal.sizeDelta.y - top - foot;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            float padding = layout != null ? layout.padding.vertical : 24f;
            bool anything = false;
            foreach (Transform child in content)
                if (child.gameObject.activeSelf) { anything = true; break; }
            if (!anything) return viewport - padding;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            return viewport - LayoutUtility.GetPreferredHeight(content) - (layout != null ? layout.spacing : 12f);
        }

        // ------------------------------------------------------------ the plea

        /// <summary>
        /// The houseguest who came to the player, as a strip across the board rather than the house
        /// event's band: the eyebrow, how many are waiting, the heading and what they said on the
        /// left, and the answers as tiles on the right, under the grid name the answers have always
        /// had. Returns the strip's height.
        /// </summary>
        private float CampaignPlea(RectTransform board, float width, CampaignBoardSpec spec)
        {
            float s = FontScale, pad = 10f * s, gap = 12f * s;
            var strip = EndScreenKit.Box(CampaignPleaName, board, 0f, 0f, width, 100f * s);
            EndScreenKit.Frame(strip, PackArt.Pack8Section, 14f * s, new Color(Surface.r, Surface.g, Surface.b, .94f),
                new Color(UiTheme.Joke.r, UiTheme.Joke.g, UiTheme.Joke.b, .55f));
            float column = Mathf.Clamp(width * .36f, 260f * s, 520f * s), inner = column - pad;
            float y = pad;
            bool counted = spec.PleasWaiting > 1;
            var eyebrow = FixedText(strip, ReplyCardEyebrow, 12, UiTheme.Joke, new Vector2(pad, -y), new Vector2(counted ? inner * .55f : inner, 16f * s));
            eyebrow.name = "Plea eyebrow";
            eyebrow.characterSpacing = 4f;
            AutoSize(eyebrow, 9);
            if (counted)
            {
                // The oldest is answered first; the rest wait, and the count says who is next, so a
                // status line naming the last to come is not read as naming this card's houseguest.
                var count = FixedText(strip, "1 of " + spec.PleasWaiting + (string.IsNullOrEmpty(spec.PleaNext) ? "" : " · " + spec.PleaNext + " next"),
                    12, UiTheme.Muted, new Vector2(pad + inner * .55f, -y), new Vector2(inner * .45f, 16f * s));
                count.name = CampaignPleaCountName;
                count.alignment = TextAlignmentOptions.Right;
                AutoSize(count, 9);
            }
            y += 17f * s;
            var title = FixedText(strip, spec.PleaTitle, 16, Paper, new Vector2(pad, -y), new Vector2(inner, 21f * s));
            title.name = "Plea title";
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) title.font = semibold;
            AutoSize(title, 12);
            y += 22f * s;
            var message = NewText(strip, spec.PleaMessage ?? "", 13, UiTheme.Muted);
            message.name = "Plea message";
            message.fontStyle = FontStyles.Italic;
            var said = message.rectTransform;
            said.anchorMin = said.anchorMax = new Vector2(0f, 1f);
            said.pivot = new Vector2(0f, 1f);
            said.anchoredPosition = new Vector2(pad, -y);
            y += EndScreenKit.Wrapped(message, inner, 3) + pad;

            // The answers, abreast, as tall as a caption, two lines of what each means, and a gap.
            float tiles = 64f * s, height = Mathf.Max(y, tiles + 2f * pad);
            float left = column + gap, room = Mathf.Max(120f * s, width - left - pad);
            int count2 = spec.Replies.Count;
            float tileGap = 10f * s, each = (room - (count2 - 1) * tileGap) / count2;
            var grid = EndScreenKit.Box(EventChoicesName, strip, left, (height - tiles) * .5f, room, tiles);
            for (int i = 0; i < count2; i++) CampaignReplyTile(grid, i * (each + tileGap), each, tiles, spec.Replies[i]);
            EndScreenKit.Place(strip, 0f, 0f, width, height);
            return height;
        }

        /// <summary>One answer as a tile on Pack 8's face for its kind: the caption, how far it could rebound, and what it means.</summary>
        private void CampaignReplyTile(RectTransform grid, float x, float width, float height, CampaignReply reply)
        {
            float s = FontScale, pad = 10f * s;
            var tile = Chrome(reply.Caption, grid, UiTheme.Emphasis.Interactive);
            HudEmphasis.Promote(tile, UiTheme.Emphasis.Interactive);
            EndScreenKit.Place(tile, x, 0f, width, height);
            EndScreenKit.Frame(tile, CampaignChoiceFace(reply.Key), 10f * s, new Color(Surface.r, Surface.g, Surface.b, .92f));
            var button = Pressable(tile, reply.Choose ?? (() => { }));
            var colours = button.colors;
            colours.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colours.selectedColor = colours.highlightedColor;
            button.colors = colours;
            float risk = 0f;
            if (!string.IsNullOrEmpty(reply.Risk))
            {
                var word = FixedText(tile, reply.Risk, 11, RiskTint(reply.Risk), Vector2.zero, new Vector2(90f * s, 15f * s));
                word.name = "Risk word";
                word.textWrappingMode = TextWrappingModes.NoWrap;
                risk = Mathf.Min(90f * s, Mathf.Ceil(word.GetPreferredValues(word.text).x) + 4f * s);
                Anchor(word.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-pad, -(pad - 1f * s)), new Vector2(risk, 15f * s));
                word.alignment = TextAlignmentOptions.Right;
                risk += 6f * s;
            }
            var caption = NewText(tile, reply.Caption, 14, Paper);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) caption.font = semibold;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(caption, 10);
            Anchor(caption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -(pad - 2f * s)),
                new Vector2(Mathf.Max(40f * s, width - 2f * pad - risk), 19f * s));
            if (string.IsNullOrEmpty(reply.Description)) return;
            var line = NewText(tile, reply.Description, 12, UiTheme.Muted);
            AutoSize(line, 9);
            Anchor(line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -(pad + 19f * s)),
                new Vector2(width - 2f * pad, height - pad - 19f * s - 4f * s));
        }

        /// <summary>A plea's answers on Pack 8's faces, by the kind of answer rather than by who asked: warm, quiet, a refusal, or a fight.</summary>
        private static string CampaignChoiceFace(string key)
        {
            switch (key)
            {
                case "promise":
                case "apologize": return PackArt.Pack8ChoiceWarm;
                case "noncommittal":
                case "slide":
                case "deflect": return PackArt.Pack8ChoiceQuiet;
                case "refuse": return PackArt.Pack8ChoiceRefuse;
                default: return PackArt.Pack8ChoiceBold;
            }
        }

        // ------------------------------------------------------------ the hero row

        /// <summary>
        /// The block as heroes, the week's title between them and the situation, and the situation
        /// card at the right-hand end (mockup 83). A nominee's hero carries the player's own reading
        /// of them and the one way to talk to them on the screen; the player's own, on the block,
        /// carries the whip count instead, since there is no reading of yourself.
        /// </summary>
        private void CampaignHeroRow(RectTransform board, float y, float width, float height, EpisodeState state, VoteRead.Sheet sheet, CampaignBoardSpec spec)
        {
            float s = FontScale, gap = 12f * s;
            var row = EndScreenKit.Box(CampaignHeroRowName, board, 0f, y, width, height);
            var block = state.nominees.Select(state.Find).Where(actor => actor != null).ToList();
            float situation = Mathf.Clamp(width * .25f, 240f * s, 330f * s);
            float title = 180f * s;
            float hero = block.Count == 0 ? 0f
                : Mathf.Clamp((width - situation - title - (block.Count + 1) * gap) / block.Count, 120f * s, 310f * s);
            float x = 0f;
            foreach (var actor in block)
            {
                CampaignHero(row, x, hero, height, state, sheet, actor, spec.Talk);
                x += hero + gap;
            }
            CampaignTitleBlock(row, x, Mathf.Max(60f * s, width - situation - gap - x), height, state, spec);
            CampaignSituation(row, width - situation, situation, height, state);
        }

        /// <summary>One nominee's hero: the photo, the NOM pill, the name, and your reading and the talk button, or, for yourself, the whip count.</summary>
        private void CampaignHero(RectTransform row, float x, float width, float height, EpisodeState state, VoteRead.Sheet sheet,
            ContestantState actor, Action<string> talk)
        {
            float s = FontScale, pad = 10f * s;
            bool you = actor.isPlayer;
            var card = EndScreenKit.Box("Nominee · " + actor.name, row, x, 0f, width, height);
            var edge = you ? UiTheme.Danger : Accent;
            EndScreenKit.Frame(card, you ? PackArt.Pack8CampaignHeroDanger : PackArt.Pack8CampaignHero, 14f * s,
                new Color(Surface.r, Surface.g, Surface.b, .92f), new Color(edge.r, edge.g, edge.b, .55f));
            float photoHeight = height - 2f * pad, photoWidth = Mathf.Min(photoHeight * .78f, width * .38f);
            var photo = HudPrimitives.RectPortrait(card, "Photo", Portrait(actor.id), actor, new Vector2(photoWidth, photoHeight), 7);
            photo.anchorMin = photo.anchorMax = new Vector2(0f, 1f);
            photo.pivot = new Vector2(0f, 1f);
            photo.anchoredPosition = new Vector2(pad, -pad);
            float left = pad + photoWidth + 10f * s, inner = Mathf.Max(40f * s, width - left - pad);
            CampaignPill(card, "NOM", UiTheme.Danger, left, pad);
            var name = FixedText(card, HudPrimitives.WithYou(actor.name, you), 15, Paper, new Vector2(left, -(pad + 21f * s)), new Vector2(inner, 20f * s));
            name.name = "Name";
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            AutoSize(name, 11);
            float below = pad + 43f * s;
            if (!you)
            {
                CampaignStanding(card, state, actor.id, left, below, inner, TextAlignmentOptions.Left);
                CampaignTalkButton(card, actor, left, height - pad - 28f * s, inner, 28f * s, talk);
                return;
            }
            // On the block yourself: where the known votes stand, as the notebook's read counts them.
            if (!sheet.available || sheet.nomineeIds.Count != 2) return;
            int mine = sheet.nomineeIds[0] == actor.id ? sheet.evictFirst : sheet.evictSecond;
            int theirs = sheet.nomineeIds[0] == actor.id ? sheet.evictSecond : sheet.evictFirst;
            var other = state.Find(sheet.nomineeIds.FirstOrDefault(id => id != actor.id));
            string otherName = other != null ? (other.name ?? "").Split(' ')[0] : "them";
            var whip = FixedText(card, "Evict you " + mine + " · Evict " + otherName + " " + theirs + " · Unknown " + sheet.unknown,
                12, Paper, new Vector2(left, -below), new Vector2(inner, height - below - pad));
            whip.name = "Campaign whip";
            whip.textWrappingMode = TextWrappingModes.Normal;
            AutoSize(whip, 9);
        }

        /// <summary>A week's role as a small filled pill, at a point in its parent's upper-left space.</summary>
        private void CampaignPill(RectTransform parent, string role, Color colour, float x, float y)
        {
            float s = FontScale;
            var pill = Panel("Role", parent, colour, 4);
            float pillWidth = Mathf.Clamp(role.Length * 7.5f + 16f, 42f, 90f) * s;
            Anchor(pill, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(pillWidth, 17f * s));
            pill.GetComponent<Image>().raycastTarget = false;
            var word = FixedText(pill, role, 11, UiTheme.Ink, Vector2.zero, pill.sizeDelta);
            word.alignment = TextAlignmentOptions.Center;
        }

        /// <summary>"You: Wary -22" - where the player stands with somebody, said as their own reading, in the relationship web's word and number.</summary>
        public static string CampaignStandingWords(EpisodeState state, string id) =>
            "You: " + RelationshipWeb.StandingWord(RelationshipWeb.KindOf(state, id)) + " " + state.Score(state.playerId, id).ToString("+0;-0;0");

        /// <summary>
        /// The player's own reading of somebody on a card: the words, and under them a bar from the
        /// same number in the web's colour for it, as the cast strip draws one. Never the other way:
        /// how a houseguest sees the player is theirs.
        /// </summary>
        private void CampaignStanding(RectTransform card, EpisodeState state, string id, float x, float y, float width, TextAlignmentOptions align)
        {
            float s = FontScale;
            var kind = RelationshipWeb.KindOf(state, id);
            var tint = kind == RelationshipWeb.Kind.Neutral ? UiTheme.Muted : RelationshipWeb.StandingColour(kind);
            var words = FixedText(card, CampaignStandingWords(state, id), 12, tint, new Vector2(x, -y), new Vector2(width, 16f * s));
            words.name = "Standing";
            words.alignment = align;
            AutoSize(words, 9);
            float inset = align == TextAlignmentOptions.Center ? 4f * s : 0f, bar = width - 2f * inset;
            var track = HudPrimitives.Fill("Standing track", card, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .55f), 2);
            EndScreenKit.Place(track, x + inset, y + 19f * s, bar, 4f * s);
            float fill = Mathf.Clamp01((float)(state.Score(state.playerId, id) + 100.0) / 200f);
            if (fill <= 0f) return;
            var filled = HudPrimitives.Fill("Standing bar", card, tint, 2);
            EndScreenKit.Place(filled, x + inset, y + 19f * s, bar * fill, 4f * s);
        }

        /// <summary>The walk over to talk to somebody, on Pack 8's talk face, captioned as it has always been.</summary>
        private void CampaignTalkButton(RectTransform card, ContestantState actor, float x, float y, float width, float height, Action<string> talk)
        {
            string id = actor.id, first = (actor.name ?? "").Split(' ')[0];
            var button = FixedButton(card, CastTalkCaption(first), new Vector2(x, -y), new Vector2(width, height), () => talk?.Invoke(id));
            // The face's edge, not the face's under the fixed button's own.
            WearFace(button, PackArt.Pack8TalkButton, 8f * FontScale,
                new Color(Surface.r, Surface.g, Surface.b, .92f), new Color(Accent.r, Accent.g, Accent.b, .6f));
            var words = button.GetComponentInChildren<TMP_Text>();
            if (words == null) return;
            words.fontSize = Mathf.RoundToInt(13 * FontScale);
            words.fontSizeMax = words.fontSize;
            words.fontSizeMin = Mathf.Min(10f, words.fontSize);
            words.enableAutoSizing = true;
            words.alignment = TextAlignmentOptions.Center;
            words.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(words.rectTransform, 6f, 2f, 6f, 2f);
        }

        /// <summary>
        /// The week's title between the heroes and the situation: the 'Ceremony title' with the word
        /// it has always had, a headline of its own over it in the mockup's words, the line under
        /// them, and the conversations left as a chip at its foot.
        /// </summary>
        private void CampaignTitleBlock(RectTransform row, float x, float width, float height, EpisodeState state, CampaignBoardSpec spec)
        {
            float s = FontScale, pad = 6f * s, inner = Mathf.Max(40f * s, width - 2f * pad), y = 4f * s;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            var title = FixedText(row, "Campaign", 13, Accent, new Vector2(x + pad, -y), new Vector2(inner, 17f * s));
            title.name = CeremonyTitleName;
            title.characterSpacing = 4f;
            if (semibold != null) title.font = semibold;
            AutoSize(title, 10);
            y += 18f * s;
            // No headline for a player who is out of the game: there is no case to build or vote
            // to swing, and the line moves up under the title.
            if (!string.IsNullOrEmpty(spec.Headline))
            {
                var headline = FixedText(row, spec.Headline, 20, UiTheme.Gold, new Vector2(x + pad, -y), new Vector2(inner, 26f * s));
                headline.name = CampaignHeadlineName;
                if (semibold != null) headline.font = semibold;
                AutoSize(headline, 13);
                y += 27f * s;
            }
            if (!string.IsNullOrEmpty(spec.Line))
            {
                var line = FixedText(row, spec.Line, 13, UiTheme.Muted, new Vector2(x + pad, -y), new Vector2(inner, 34f * s));
                line.name = "Campaign line";
                line.textWrappingMode = TextWrappingModes.Normal;
                line.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(line, 10);
            }

            // The conversations left: the meter's words, kept as text, on the pack's chip.
            int budget = EpisodeEngine.SocialActionBudget(state);
            int left = Mathf.Max(0, budget - EpisodeEngine.SocialActionsSpent(state));
            float chipHeight = 40f * s, chipWidth = Mathf.Min(inner, 230f * s);
            var chip = EndScreenKit.Box(CampaignActionsName, row, x + pad, height - chipHeight - 2f * s, chipWidth, chipHeight);
            EndScreenKit.Frame(chip, PackArt.Pack8ActionsLeft, 10f * s, new Color(Surface.r, Surface.g, Surface.b, .92f),
                new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .5f));
            float mark = 20f * s, words = 10f * s;
            if (EndScreenKit.Picture("Icon", chip, null, "star", UiTheme.Gold, new Vector2(10f * s + mark * .5f, -chipHeight * .5f), mark) != null)
                words += mark + 8f * s;
            var label = FixedText(chip, "Interactions available", 11, UiTheme.Muted, new Vector2(words, -3f * s), new Vector2(chipWidth - words - 8f * s, 15f * s));
            label.name = "Actions label";
            AutoSize(label, 9);
            var count = FixedText(chip, left + " of " + budget, 15, UiTheme.Gold, new Vector2(words, -18f * s), new Vector2(chipWidth - words - 8f * s, 20f * s));
            count.name = "Actions count";
            if (semibold != null) count.font = semibold;
            AutoSize(count, 11);
            CampaignActionsBar(chip, words + Mathf.Ceil(count.GetPreferredValues(count.text).x) + 10f * s, 26f * s, chipWidth - 12f * s, left, budget);
        }

        /// <summary>
        /// The conversations left as a slim bar after the count, from <paramref name="x"/> to
        /// <paramref name="right"/>: the drain the meter this chip replaced had. It travels from
        /// where it was last drawn to where it is now, so a conversation spent is seen leaving when
        /// the campaign opens again; under reduced motion it is simply where it is. Nothing where
        /// the chip has no width left for it beside the count.
        /// </summary>
        private void CampaignActionsBar(RectTransform chip, float x, float y, float right, int left, int budget)
        {
            float s = FontScale, width = right - x;
            if (width < 24f * s) return;
            var track = HudPrimitives.Fill("Actions track", chip, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .55f), 2);
            EndScreenKit.Place(track, x, y, width, 4f * s);
            float target = budget <= 0 ? 0f : Mathf.Clamp01((float)left / budget);
            float shown = meterShown.TryGetValue(CampaignActionsMeter, out var previous) ? previous : target;
            meterShown[CampaignActionsMeter] = target;
            var fill = HudPrimitives.Fill("Actions fill", track, UiTheme.Gold, 2);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(target, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            if (!ReducedMotion && Mathf.Abs(shown - target) > .001f) fill.gameObject.AddComponent<HudFill>().Play(shown, target);
        }

        /// <summary>
        /// This week's situation (mockup 83): who holds the house, who is on the block, who held the
        /// veto and whether it was used, and how many are left - the public facts the roles banner
        /// gathers, on the pack's panel with a glyph each.
        /// </summary>
        private void CampaignSituation(RectTransform row, float x, float width, float height, EpisodeState state)
        {
            float s = FontScale, pad = 10f * s;
            var card = EndScreenKit.Box(CampaignSituationName, row, x, 0f, width, height);
            EndScreenKit.Frame(card, PackArt.Pack8Situation, 14f * s, new Color(Surface.r, Surface.g, Surface.b, .92f));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            var head = FixedText(card, "This Week's Situation", 13, Accent, new Vector2(pad, -pad), new Vector2(width - 2f * pad, 17f * s));
            head.name = "Situation heading";
            if (semibold != null) head.font = semibold;
            AutoSize(head, 10);

            string Named(string id) { var who = state.Find(id); return who == null ? null : HudPrimitives.WithYou(who.name, who.isPlayer); }
            var block = state.nominees.Select(Named).Where(name => name != null).ToList();
            var power = state.ledger.power.LastOrDefault(p => p.week == state.week);
            string veto = Named(state.vetoHolderId);
            if (veto != null && state.vetoResolved && power != null) veto += power.vetoUsed ? " · used" : " · not used";
            var rows = new List<(string pack, string glyph, Color tint, string label, string value)>
            {
                (PackArt.Pack8IconHoh, "crown", UiTheme.Gold, "HoH", Named(state.hohId) ?? "Nobody yet"),
                (PackArt.Pack8IconTarget, "target", UiTheme.Danger, "On the block", block.Count > 0 ? string.Join(" and ", block) : "Nobody yet"),
                (PackArt.Pack8IconVeto, "veto-token", Accent, "Veto", veto ?? "Nobody yet"),
                (PackArt.Pack8IconPeople, "people", Accent, "Houseguests", state.Active.Count() + " of " + state.contestants.Count + " remain"),
            };
            float top = pad + 20f * s, each = (height - top - pad) / rows.Count, mark = Mathf.Min(18f * s, each - 4f * s);
            float labels = 86f * s;
            for (int i = 0; i < rows.Count; i++)
            {
                float y = top + i * each, middle = y + each * .5f;
                float left = pad;
                if (EndScreenKit.Picture("Icon", card, rows[i].pack, rows[i].glyph, rows[i].tint, new Vector2(pad + mark * .5f, -middle), mark) != null)
                    left += mark + 8f * s;
                var label = FixedText(card, rows[i].label, 11, UiTheme.Muted, new Vector2(left, -(middle - 7.5f * s)), new Vector2(labels, 15f * s));
                label.name = "Situation label";
                AutoSize(label, 9);
                var value = FixedText(card, rows[i].value, 12, Paper, new Vector2(left + labels, -(middle - 8f * s)),
                    new Vector2(Mathf.Max(30f * s, width - left - labels - pad), 16f * s));
                value.name = "Situation value";
                value.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(value, 9);
            }
        }

        // ------------------------------------------------------------ the tabs

        /// <summary>
        /// The row of tabs, and "More ways to campaign" as the row's trailing control. The tabs share
        /// what the toggle leaves them, each as wide as its words until they do not fit, when they
        /// narrow together and their words take a size down. Choosing one is view state.
        /// </summary>
        private void CampaignTabs(RectTransform board, float y, float width, float height, CampaignBoardSpec spec, CampaignTab shown, bool folded)
        {
            float s = FontScale, gap = 6f * s;
            var row = EndScreenKit.Box(CampaignTabsName, board, 0f, y, width, height);
            var more = FilterPill(row, spec.More ? EpisodeDirector.CampaignLessCaption : EpisodeDirector.CampaignMoreCaption, spec.More,
                () => spec.ToggleMore?.Invoke(), height);
            var moreRect = (RectTransform)more.transform;
            Anchor(moreRect, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, moreRect.sizeDelta);
            float room = Mathf.Max(100f * s, width - moreRect.sizeDelta.x - 12f * s);

            var tabs = new List<(string caption, CampaignTab tab)>
            {
                (CampaignTabTalk, CampaignTab.Talk), (CampaignTabIntel, CampaignTab.Intel), (CampaignTabPoints, CampaignTab.Points),
                (CampaignTabStories, CampaignTab.Stories), (CampaignTabOutlook, CampaignTab.Outlook),
            };
            if (folded) tabs.Add((CampaignTabGoals, CampaignTab.Goals));
            int stories = spec.Stories != null ? spec.Stories.Count : 0;
            var built = new List<(RectTransform rect, float natural)>();
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            foreach (var entry in tabs)
            {
                bool active = entry.tab == shown;
                var rect = Panel(entry.caption, row, active ? new Color(Accent.r, Accent.g, Accent.b, .28f) : UiTheme.SurfaceRaised, 8);
                EndScreenKit.Frame(rect, active ? PackArt.Pack8TabActive : PackArt.Pack8TabInactive, 10f * s,
                    active ? new Color(Accent.r, Accent.g, Accent.b, .28f) : new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .9f),
                    active ? Accent : UiTheme.Edge(UiTheme.Emphasis.Interactive));
                var label = NewText(rect, entry.caption, 13, active ? Color.white : Paper);
                var weight = active ? semibold : medium;
                if (weight != null) label.font = weight;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                float words = Mathf.Ceil(label.GetPreferredValues(label.text).x);
                float badge = 0f;
                if (entry.tab == CampaignTab.Stories && stories > 0)
                {
                    // How many stories are running, beside the tab's caption and never in it.
                    badge = 20f * s;
                    var number = FixedText(rect, stories.ToString(), 11, UiTheme.Gold, Vector2.zero, new Vector2(badge, 15f * s));
                    number.name = "Tab count";
                    number.alignment = TextAlignmentOptions.Center;
                    Anchor(number.rectTransform, new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(-6f * s, 0f), new Vector2(badge, 15f * s));
                }
                Stretch(label.rectTransform, 8f * s, 2f * s, 8f * s + (badge > 0f ? badge + 2f * s : 0f), 2f * s);
                AutoSize(label, 9);
                var tab = entry.tab;
                Pressable(rect, () => spec.ChooseTab?.Invoke(tab));
                built.Add((rect, Mathf.Max(96f * s, words + 28f * s + (badge > 0f ? badge + 2f * s : 0f))));
            }
            float natural = built.Sum(part => part.natural) + gap * (built.Count - 1);
            float squeeze = natural > room ? (room - gap * (built.Count - 1)) / built.Sum(part => part.natural) : 1f;
            float x = 0f;
            foreach (var part in built)
            {
                float each = part.natural * squeeze;
                Anchor(part.rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, 0f), new Vector2(each, height));
                x += each + gap;
            }
            // Built first to measure what it leaves the tabs, and read after them, as it stands.
            moreRect.SetAsLastSibling();
        }

        /// <summary>The line at the head of a tab's body, left of the pager when there is one.</summary>
        private void CampaignTabLine(RectTransform body, float width, float line, string words)
        {
            float s = FontScale;
            var text = FixedText(body, words ?? "", 12, UiTheme.Muted, new Vector2(0f, -(line - 17f * s) * .5f), new Vector2(Mathf.Max(40f * s, width), 17f * s));
            text.name = CampaignTabLineName;
            text.overflowMode = TextOverflowModes.Ellipsis;
            AutoSize(text, 9);
        }

        /// <summary>
        /// The pager at the right-hand end of a tab's line, when its houseguests run past one page:
        /// where the page is, and a way back and on, each only where there is somewhere to go.
        /// Returns the width it took.
        /// </summary>
        private float CampaignPager(RectTransform body, float width, float line, int page, int pages, CampaignBoardSpec spec)
        {
            if (pages <= 1) return 0f;
            float s = FontScale, gap = 8f * s, x = width;
            // Made in reading order, the way back before the way on, and laid from the right.
            var pills = new List<RectTransform>();
            if (page > 0)
            {
                int previous = page - 1;
                pills.Add((RectTransform)FilterPill(body, CampaignPreviousCaption, false, () => spec.ChoosePage?.Invoke(previous), line).transform);
            }
            if (page < pages - 1)
            {
                int next = page + 1;
                pills.Add((RectTransform)FilterPill(body, CampaignNextCaption, false, () => spec.ChoosePage?.Invoke(next), line).transform);
            }
            for (int i = pills.Count - 1; i >= 0; i--)
            {
                x -= pills[i].sizeDelta.x;
                Anchor(pills[i], new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, 0f), pills[i].sizeDelta);
                x -= gap;
            }
            float where = 90f * s;
            x -= where;
            var at = FixedText(body, "Page " + (page + 1) + " of " + pages, 12, UiTheme.Muted, new Vector2(x, -(line - 17f * s) * .5f), new Vector2(where, 17f * s));
            at.name = "Page";
            at.alignment = TextAlignmentOptions.Right;
            AutoSize(at, 9);
            return width - x + gap;
        }

        // ------------------------------------------------------------ the tabs' bodies

        /// <summary>
        /// Talk to Houseguests: the Head of Household and the voters as cards, each one press from
        /// walking over to talk, paged when the house runs past one row. The nominees' talk buttons
        /// are on their heroes, so no caption is on the screen twice.
        /// </summary>
        private void CampaignTalkTab(RectTransform body, float width, float line, float height, EpisodeState state, VoteRead.Sheet sheet, CampaignBoardSpec spec)
        {
            float s = FontScale, gap = CampaignGap * s;
            var ids = new List<string>();
            var hoh = state.Find(state.hohId);
            if (hoh != null && !hoh.isPlayer && hoh.status == ContestantStatus.Active) ids.Add(hoh.id);
            ids.AddRange(EpisodeEngine.Voters(state).Where(voter => !voter.isPlayer).Select(voter => voter.id));
            float photo = Mathf.Max(CampaignPhotoLeast * s, height - CampaignCardChrome * s);
            float cardWidth = CampaignCardWidth * s;
            int perPage = Mathf.Max(1, Mathf.FloorToInt((width + gap) / (cardWidth + gap)));
            int pages = Mathf.Max(1, Mathf.CeilToInt(ids.Count / (float)perPage));
            int page = Mathf.Clamp(spec.Page, 0, pages - 1);
            float pager = CampaignPager(body, width, line, page, pages, spec);
            CampaignTabLine(body, width - pager, line, ids.Count == 0 ? "Nobody else votes this week." : RelationshipWeb.PerspectiveCopy);
            var grid = EndScreenKit.Box(CampaignVotersName, body, 0f, line + 6f * s, width, height);
            int slot = 0;
            foreach (var id in ids.Skip(page * perPage).Take(perPage))
                CampaignVoterCard(grid, slot++ * (cardWidth + gap), cardWidth, height, photo, state, id, hoh != null && id == hoh.id, sheet, spec.Talk);
        }

        /// <summary>
        /// A voter's card: the photo, a role pill if the week gave them one, the name, your reading and
        /// its bar, their read on its Pack 8 chip - the chip a sibling behind the 'Read' line, so the
        /// line is still the card's own child - and the talk button. The Head of Household's card
        /// says they vote only on a tie, and has no read.
        /// </summary>
        private void CampaignVoterCard(RectTransform grid, float x, float width, float height, float photo, EpisodeState state, string id,
            bool headOfHouse, VoteRead.Sheet sheet, Action<string> talk)
        {
            var actor = state.Find(id);
            if (actor == null) return;
            float s = FontScale, pad = 6f * s, inner = width - 2f * pad;
            var kind = RelationshipWeb.KindOf(state, id);
            var card = EndScreenKit.Box((headOfHouse ? "Head of Household · " : "Voter · ") + actor.name, grid, x, 0f, width, height);
            EndScreenKit.Frame(card, kind == RelationshipWeb.Kind.Rivalry ? PackArt.Pack8VoterDanger : PackArt.Pack8VoterCard, 12f * s,
                new Color(Surface.r, Surface.g, Surface.b, .92f));
            var picture = HudPrimitives.RectPortrait(card, "Photo", Portrait(id), actor, new Vector2(inner, photo), 7);
            picture.anchorMin = picture.anchorMax = new Vector2(0f, 1f);
            picture.pivot = new Vector2(0f, 1f);
            picture.anchoredPosition = new Vector2(pad, -pad);
            string role = RoleFor(state, id, out var roleColour);
            if (role != null)
            {
                float pillWidth = Mathf.Clamp(role.Length * 7.5f + 16f, 42f, 90f) * s;
                CampaignPill(picture, role, roleColour, (inner - pillWidth) * .5f, photo - 21f * s);
            }
            float y = pad + photo + 5f * s;
            var name = FixedText(card, actor.name, 14, Paper, new Vector2(pad, -y), new Vector2(inner, 19f * s));
            name.name = "Name";
            name.alignment = TextAlignmentOptions.Center;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            AutoSize(name, 10);
            y += 20f * s;
            CampaignStanding(card, state, id, pad, y, inner, TextAlignmentOptions.Center);
            y += 28f * s;
            if (headOfHouse)
            {
                var tie = FixedText(card, CampaignTieLine, 12, UiTheme.Muted, new Vector2(pad, -y), new Vector2(inner, 16f * s));
                tie.name = "Tie line";
                tie.alignment = TextAlignmentOptions.Center;
                AutoSize(tie, 9);
            }
            else
            {
                var read = sheet.voters.FirstOrDefault(r => r.voterId == id);
                var chip = EndScreenKit.Box("Vote chip", card, pad + 2f * s, y - 1f * s, inner - 4f * s, 18f * s);
                EndScreenKit.Frame(chip, CampaignVoteChip(state, read), 6f * s,
                    new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .85f));
                bool blank = read == null || (read.confidence == VoteRead.Unknown && read.saysId == null);
                var lean = FixedText(card, read == null ? "" : EpisodeDirector.ReadHeadline(state, read), 12, blank ? UiTheme.Muted : Paper,
                    new Vector2(pad, -y), new Vector2(inner, 16f * s));
                lean.alignment = TextAlignmentOptions.Center;
                lean.name = VoteReadLineName;
                AutoSize(lean, 8);
            }
            y += 22f * s;
            CampaignTalkButton(card, actor, pad, y, inner, 28f * s, talk);
        }

        /// <summary>
        /// The chip behind a read: how sure it is, and which way. Keep and evict are the player's
        /// only when the player is on the block - a lean against the other nominee keeps them -
        /// and otherwise every lean is an eviction's.
        /// </summary>
        private static string CampaignVoteChip(EpisodeState state, VoteRead.VoterRead read)
        {
            if (read == null) return PackArt.Pack8VoteUnknown;
            if (read.confidence == VoteRead.Torn) return PackArt.Pack8VoteUndecided;
            string leaning = read.confidence != VoteRead.Unknown ? read.leaningId : read.saysId;
            if (leaning == null) return PackArt.Pack8VoteUnknown;
            bool keeps = state.nominees.Contains(state.playerId) && leaning != state.playerId;
            bool firm = read.confidence == VoteRead.Firm;
            return keeps ? (firm ? PackArt.Pack8VoteLikelyKeep : PackArt.Pack8VoteLeanKeep)
                : (firm ? PackArt.Pack8VoteLikelyEvict : PackArt.Pack8VoteLeanEvict);
        }

        /// <summary>
        /// Relationship Intel: everybody still in the house, as a row each - your reading of them,
        /// and the newest thing you have on them, from the notebook's own notes - paged when they
        /// run past the body's height.
        /// </summary>
        private void CampaignIntelTab(RectTransform body, float width, float line, float height, EpisodeState state, CampaignBoardSpec spec)
        {
            float s = FontScale, gap = 8f * s, rowHeight = 42f * s;
            var people = state.contestants.Where(actor => !actor.isPlayer && actor.status == ContestantStatus.Active).ToList();
            int columns = Mathf.Max(1, Mathf.FloorToInt((width + gap) / (300f * s + gap)));
            int rows = Mathf.Max(1, Mathf.FloorToInt((height + gap) / (rowHeight + gap)));
            int perPage = columns * rows;
            int pages = Mathf.Max(1, Mathf.CeilToInt(people.Count / (float)perPage));
            int page = Mathf.Clamp(spec.Page, 0, pages - 1);
            float pager = CampaignPager(body, width, line, page, pages, spec);
            CampaignTabLine(body, width - pager, line, RelationshipWeb.PerspectiveCopy);
            float each = (width - (columns - 1) * gap) / columns, top = line + 6f * s;
            int slot = 0;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            foreach (var actor in people.Skip(page * perPage).Take(perPage))
            {
                float x = (slot / rows) * (each + gap), y = top + (slot % rows) * (rowHeight + gap);
                slot++;
                var row = EndScreenKit.Box("Intel · " + actor.name, body, x, y, each, rowHeight);
                EndScreenKit.Frame(row, PackArt.Pack8CampaignRow, 8f * s, new Color(Surface.r, Surface.g, Surface.b, .9f));
                float pad = 10f * s, standing = Mathf.Min(each * .45f, 150f * s);
                var name = FixedText(row, actor.name, 13, Paper, new Vector2(pad, -4f * s), new Vector2(each - 2f * pad - standing, 17f * s));
                name.name = "Name";
                if (semibold != null) name.font = semibold;
                AutoSize(name, 10);
                var kind = RelationshipWeb.KindOf(state, actor.id);
                var reading = FixedText(row, CampaignStandingWords(state, actor.id), 12,
                    kind == RelationshipWeb.Kind.Neutral ? UiTheme.Muted : RelationshipWeb.StandingColour(kind),
                    new Vector2(each - pad - standing, -4f * s), new Vector2(standing, 17f * s));
                reading.name = "Standing";
                reading.alignment = TextAlignmentOptions.Right;
                AutoSize(reading, 9);
                string brief = HouseguestNotes.Brief(state, actor.id);
                var note = FixedText(row, string.IsNullOrEmpty(brief) ? "Nothing on them yet" : brief, 12, UiTheme.Muted,
                    new Vector2(pad, -22f * s), new Vector2(each - 2f * pad, 17f * s));
                note.name = "Note";
                note.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(note, 9);
            }
        }

        /// <summary>
        /// Talking Points: what this week's rules let the player do in a conversation, as plain words
        /// - never a control, and never a conversation's caption, which are the conversation's to show.
        /// Talking to somebody is where they are used.
        /// </summary>
        private void CampaignPointsTab(RectTransform body, float width, float line, float height, CampaignBoardSpec spec)
        {
            float s = FontScale, gap = 6f * s, rowHeight = 30f * s;
            CampaignTabLine(body, width, line, "What this week's rules let you do in a conversation. Talk to somebody to use them.");
            var points = spec.TalkingPoints ?? new List<string>();
            int columns = width >= 900f * s ? 2 : 1;
            int rows = Mathf.Max(1, Mathf.FloorToInt((height + gap) / (rowHeight + gap)));
            float each = (width - (columns - 1) * 12f * s) / columns, top = line + 6f * s;
            int shown = Mathf.Min(points.Count, columns * rows);
            for (int i = 0; i < shown; i++)
            {
                float x = (i / rows) * (each + 12f * s), y = top + (i % rows) * (rowHeight + gap);
                var row = EndScreenKit.Box("Talking point", body, x, y, each, rowHeight);
                EndScreenKit.Frame(row, PackArt.Pack8CampaignRow, 8f * s, new Color(Surface.r, Surface.g, Surface.b, .9f));
                float pad = 10f * s, left = pad;
                float mark = 16f * s;
                if (EndScreenKit.Picture("Icon", row, PackArt.Pack8IconChat, "chat", Accent, new Vector2(pad + mark * .5f, -rowHeight * .5f), mark) != null)
                    left += mark + 8f * s;
                var words = FixedText(row, points[i], 13, Paper, new Vector2(left, -(rowHeight - 18f * s) * .5f), new Vector2(each - left - pad, 18f * s));
                words.name = "Point";
                words.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(words, 9);
            }
        }

        /// <summary>
        /// Storylines: the plays, the threads, the storylines and what the stories left the player,
        /// one row each, as the free-time panel lists them. What does not fit says how much more the
        /// notebook's story pages hold.
        /// </summary>
        private void CampaignStoriesTab(RectTransform body, float width, float line, float height, CampaignBoardSpec spec)
        {
            float s = FontScale, gap = 6f * s, rowHeight = 40f * s;
            var stories = spec.Stories ?? new List<(string Title, string Line)>();
            CampaignTabLine(body, width, line, stories.Count == 0 ? "No plays, threads or storylines are running this week."
                : "Your plays, threads and storylines this week.");
            int columns = Mathf.Max(1, Mathf.FloorToInt((width + 12f * s) / (420f * s + 12f * s)));
            int rows = Mathf.Max(1, Mathf.FloorToInt((height + gap) / (rowHeight + gap)));
            int room = columns * rows;
            int shown = stories.Count > room ? room - 1 : stories.Count;
            float each = (width - (columns - 1) * 12f * s) / columns, top = line + 6f * s;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            for (int i = 0; i <= shown && i < room; i++)
            {
                float x = (i / rows) * (each + 12f * s), y = top + (i % rows) * (rowHeight + gap);
                if (i == shown)
                {
                    if (shown >= stories.Count) break;
                    var more = FixedText(body, (stories.Count - shown) + " more in your notebook, under The story so far.", 12, UiTheme.Muted,
                        new Vector2(x + 10f * s, -(y + (rowHeight - 17f * s) * .5f)), new Vector2(each - 20f * s, 17f * s));
                    more.name = "More stories";
                    AutoSize(more, 9);
                    break;
                }
                var row = EndScreenKit.Box("Story · " + stories[i].Title, body, x, y, each, rowHeight);
                EndScreenKit.Frame(row, PackArt.Pack8CampaignRow, 8f * s, new Color(Surface.r, Surface.g, Surface.b, .9f));
                float pad = 10f * s;
                var title = FixedText(row, stories[i].Title, 13, Paper, new Vector2(pad, -3f * s), new Vector2(each - 2f * pad, 17f * s));
                title.name = "Story title";
                if (semibold != null) title.font = semibold;
                title.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(title, 10);
                var said = FixedText(row, stories[i].Line, 12, UiTheme.Muted, new Vector2(pad, -21f * s), new Vector2(each - 2f * pad, 16f * s));
                said.name = "Story line";
                said.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(said, 9);
            }
        }

        /// <summary>
        /// Voting Outlook: the read of the house as the notebook's read tab counts it - who the known
        /// leanings evict, and each voter's read - under the votes the week has. The same sheet, so
        /// it never says more than the notebook does.
        /// </summary>
        private void CampaignOutlookTab(RectTransform body, float width, float line, float height, EpisodeState state, VoteRead.Sheet sheet, CampaignBoardSpec spec)
        {
            float s = FontScale, gap = 8f * s, rowHeight = 40f * s;
            if (!sheet.available || sheet.nomineeIds.Count != 2)
            {
                CampaignTabLine(body, width, line, "There is no vote to read yet.");
                return;
            }
            int votes = sheet.voters.Count + (EpisodeEngine.Voters(state).Any(voter => voter.isPlayer) ? 1 : 0);
            var first = state.Find(sheet.nomineeIds[0]);
            var second = state.Find(sheet.nomineeIds[1]);
            float top = line + 6f * s, card = 52f * s;
            var summary = EndScreenKit.Box(CampaignOutlookName, body, 0f, top, width, card);
            EndScreenKit.Frame(summary, PackArt.Pack8Section, 10f * s, new Color(Surface.r, Surface.g, Surface.b, .92f));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            string headline = sheet.predictedEvicteeId != null ? "The house is leaning " + state.Find(sheet.predictedEvicteeId)?.name
                : sheet.unknown == sheet.voters.Count ? "No read on the house yet" : "Too close to call";
            var lean = FixedText(summary, headline, 15, Paper, new Vector2(12f * s, -6f * s), new Vector2(width - 24f * s, 20f * s));
            lean.name = "Outlook headline";
            if (semibold != null) lean.font = semibold;
            AutoSize(lean, 11);
            var count = FixedText(summary, "Evict " + first?.name + " " + sheet.evictFirst + " · Evict " + second?.name + " " + sheet.evictSecond
                + " · Unknown " + sheet.unknown, 13, UiTheme.Muted, new Vector2(12f * s, -28f * s), new Vector2(width - 24f * s, 18f * s));
            count.name = "Outlook count";
            AutoSize(count, 10);

            float listTop = top + card + gap, room = Mathf.Max(rowHeight, height - card - gap);
            int columns = Mathf.Max(1, Mathf.FloorToInt((width + gap) / (280f * s + gap)));
            int rows = Mathf.Max(1, Mathf.FloorToInt((room + gap) / (rowHeight + gap)));
            int perPage = columns * rows;
            int pages = Mathf.Max(1, Mathf.CeilToInt(sheet.voters.Count / (float)perPage));
            int page = Mathf.Clamp(spec.Page, 0, pages - 1);
            float pager = CampaignPager(body, width, line, page, pages, spec);
            CampaignTabLine(body, width - pager, line, votes + (votes == 1 ? " vote" : " votes") + " this week; a tie goes to the Head of Household.");
            float each = (width - (columns - 1) * gap) / columns;
            int slot = 0;
            foreach (var read in sheet.voters.Skip(page * perPage).Take(perPage))
            {
                var voter = state.Find(read.voterId);
                if (voter == null) continue;
                float x = (slot / rows) * (each + gap), y = listTop + (slot % rows) * (rowHeight + gap);
                slot++;
                var row = EndScreenKit.Box("Outlook · " + voter.name, body, x, y, each, rowHeight);
                EndScreenKit.Frame(row, PackArt.Pack8CampaignRow, 8f * s, new Color(Surface.r, Surface.g, Surface.b, .9f));
                float pad = 10f * s;
                var name = FixedText(row, voter.name, 13, Paper, new Vector2(pad, -3f * s), new Vector2(each - 2f * pad, 17f * s));
                name.name = "Name";
                if (semibold != null) name.font = semibold;
                AutoSize(name, 10);
                bool blank = read.confidence == VoteRead.Unknown && read.saysId == null;
                var said = FixedText(row, EpisodeDirector.ReadHeadline(state, read), 12, blank ? UiTheme.Muted : Paper,
                    new Vector2(pad, -21f * s), new Vector2(each - 2f * pad, 16f * s));
                said.name = "Outlook read";
                said.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(said, 9);
            }
        }

        // ------------------------------------------------------------ the foot cards

        /// <summary>The three cards along the foot (mockup 83): the week's goals, the newest intel, and a tip.</summary>
        private void CampaignCards(RectTransform parent, float y, float width, float height, EpisodeState state, CampaignBoardSpec spec)
        {
            float s = FontScale, gap = 12f * s, each = (width - 2f * gap) / 3f;
            var row = EndScreenKit.Box(CampaignCardsName, parent, 0f, y, width, height);
            CampaignGoalsCard(row, 0f, each, height, spec.Goals);
            CampaignIntelCard(row, each + gap, each, height, state, spec.Intel);
            CampaignTipCard(row, 2f * (each + gap), each, height, spec.Tip);
        }

        /// <summary>A foot card's frame and heading; returns where its rows start.</summary>
        private float CampaignCardHead(RectTransform card, string frame, string heading, Color tint, string pack, string glyph)
        {
            float s = FontScale, pad = 10f * s, left = pad;
            EndScreenKit.Frame(card, frame, 12f * s, new Color(Surface.r, Surface.g, Surface.b, .92f));
            float mark = 16f * s;
            var icon = string.IsNullOrEmpty(glyph) ? null : EndScreenKit.Picture("Icon", card, pack, glyph, tint, new Vector2(pad + mark * .5f, -(pad + 8.5f * s)), mark);
            if (icon != null)
            {
                // Kit 6's parts are white and tinted where they are used; Pack 8's bake their own colour.
                if (pack != null && pack.StartsWith("Kit6_", StringComparison.Ordinal)) icon.color = tint;
                left += mark + 8f * s;
            }
            var head = FixedText(card, heading, 13, tint, new Vector2(left, -pad), new Vector2(Mathf.Max(40f * s, card.sizeDelta.x - left - pad), 17f * s));
            head.name = "Card heading";
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) head.font = semibold;
            AutoSize(head, 10);
            return pad + 21f * s;
        }

        /// <summary>
        /// Your Campaign Goals: a row each, a hollow ring while it is open and the gold check once it
        /// is done, as the endgame's objectives mark theirs, with where it stands at the row's end.
        /// </summary>
        private void CampaignGoalsCard(RectTransform row, float x, float width, float height, IList<CampaignBrief.Goal> goals)
        {
            float s = FontScale, pad = 10f * s, rowHeight = 22f * s;
            var card = EndScreenKit.Box(CampaignGoalsName, row, x, 0f, width, height);
            float y = CampaignCardHead(card, PackArt.Pack8GoalPanel, "Your Campaign Goals", Accent, null, null);
            goals = goals ?? new List<CampaignBrief.Goal>();
            int fits = Mathf.Max(1, Mathf.FloorToInt((height - y - pad) / rowHeight));
            if (goals.Count == 0)
            {
                var none = FixedText(card, "Nothing on your list this week yet.", 12, UiTheme.Muted, new Vector2(pad, -y), new Vector2(width - 2f * pad, 17f * s));
                none.name = "No goals";
                AutoSize(none, 9);
                return;
            }
            var open = UiTheme.Muted; open.a = .8f;
            var check = UiTheme.Pack(PackArt.KitIconCheck);
            int shown = goals.Count > fits ? fits - 1 : goals.Count;
            for (int i = 0; i < shown; i++)
            {
                var goal = goals[i];
                float top = y + i * rowHeight;
                var mark = HudPrimitives.Disc("Goal mark", card, goal.done ? UiTheme.Gold : open);
                if (!goal.done) mark.GetComponent<Image>().sprite = UiTheme.Ring();
                EndScreenKit.Place(mark, pad, top + 3f * s, 13f * s, 13f * s);
                if (goal.done && check != null)
                {
                    var tick = new GameObject("Goal check", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    tick.rectTransform.SetParent(card, false);
                    EndScreenKit.Place(tick.rectTransform, pad + 2f * s, top + 5f * s, 9f * s, 9f * s);
                    tick.sprite = check; tick.color = UiTheme.Ink; tick.preserveAspect = true; tick.raycastTarget = false;
                }
                float words = pad + 21f * s, progress = string.IsNullOrEmpty(goal.progress) ? 0f : Mathf.Min(width * .38f, 130f * s);
                var text = FixedText(card, goal.text, 12, goal.done ? UiTheme.Muted : Paper, new Vector2(words, -top), new Vector2(width - words - pad - progress, 17f * s));
                text.name = "Goal";
                text.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(text, 9);
                if (progress <= 0f) continue;
                var where = FixedText(card, goal.progress, 11, goal.done ? UiTheme.Gold : UiTheme.Muted, new Vector2(width - pad - progress, -(top + 1f * s)),
                    new Vector2(progress, 15f * s));
                where.name = "Goal progress";
                where.alignment = TextAlignmentOptions.Right;
                where.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(where, 9);
            }
            if (shown < goals.Count)
            {
                var more = FixedText(card, (goals.Count - shown) + " more", 11, UiTheme.Muted, new Vector2(pad + 21f * s, -(y + shown * rowHeight)),
                    new Vector2(width - 2f * pad - 21f * s, 15f * s));
                more.name = "More goals";
                AutoSize(more, 9);
            }
        }

        /// <summary>Recent Intel: the newest things the player has learned, dated by the week, since the sim keeps weeks and not days.</summary>
        private void CampaignIntelCard(RectTransform row, float x, float width, float height, EpisodeState state, IList<CampaignBrief.Intel> intel)
        {
            float s = FontScale, pad = 10f * s, rowHeight = 24f * s;
            var card = EndScreenKit.Box(CampaignIntelName, row, x, 0f, width, height);
            float y = CampaignCardHead(card, PackArt.Pack8IntelPanel, "Recent Intel", Accent, null, null);
            intel = intel ?? new List<CampaignBrief.Intel>();
            if (intel.Count == 0)
            {
                var none = FixedText(card, "Nothing learned yet. Ask, read, listen in.", 12, UiTheme.Muted, new Vector2(pad, -y), new Vector2(width - 2f * pad, 17f * s));
                none.name = "No intel";
                AutoSize(none, 9);
                return;
            }
            int fits = Mathf.Max(1, Mathf.FloorToInt((height - y - pad) / rowHeight));
            float mark = 14f * s, when = Mathf.Min(width * .3f, 76f * s);
            for (int i = 0; i < Mathf.Min(fits, intel.Count); i++)
            {
                float top = y + i * rowHeight, left = pad;
                if (EndScreenKit.Picture("Icon", card, PackArt.Pack8IconChat, "chat", Accent, new Vector2(pad + mark * .5f, -(top + 8.5f * s)), mark) != null)
                    left += mark + 8f * s;
                var text = FixedText(card, intel[i].text, 12, Paper, new Vector2(left, -top), new Vector2(width - left - pad - when - 6f * s, 17f * s));
                text.name = "Intel";
                text.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(text, 9);
                var date = FixedText(card, CampaignBrief.When(state, intel[i].week), 11, UiTheme.Muted, new Vector2(width - pad - when, -(top + 1f * s)), new Vector2(when, 15f * s));
                date.name = "Intel week";
                date.alignment = TextAlignmentOptions.Right;
                AutoSize(date, 9);
            }
        }

        /// <summary>Pro Tip: a line of the game's own rule copy, never a reading of the state.</summary>
        private void CampaignTipCard(RectTransform row, float x, float width, float height, string tip)
        {
            float s = FontScale, pad = 10f * s;
            var card = EndScreenKit.Box(CampaignTipName, row, x, 0f, width, height);
            float y = CampaignCardHead(card, PackArt.Pack8ProTip, "Pro Tip", UiTheme.Gold, PackArt.KitIconInfo, "bulb");
            var words = NewText(card, tip ?? "", 12, Paper);
            words.name = "Tip";
            var rect = words.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(pad, -y);
            int lines = Mathf.Max(1, Mathf.FloorToInt((height - y - pad) / (12f * FontScale * 1.32f)));
            EndScreenKit.Wrapped(words, width - 2f * pad, lines);
        }
    }
}
