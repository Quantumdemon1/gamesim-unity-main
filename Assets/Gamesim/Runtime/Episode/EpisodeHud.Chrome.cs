using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The fixed chrome the mockups are recognised by (VISUAL-TARGET.md §4, V2 item 5): a top bar
    /// carrying the brand, the week and the house's numbers, and a right column of cards.
    ///
    /// <para>The names on these panels — 'Brand', 'Navigation', 'Objective', 'House pill',
    /// 'Live feed', 'Exploration controls', 'Status', 'Overview column' — are a contract of their
    /// own: the accessibility suite looks each one up by name and asserts that no two of them
    /// overlap at either text size. They are reshaped here, never renamed.</para>
    ///
    /// <para>Every rectangle below is also placed against two canvas shapes, not one. The HUD's
    /// scaler matches width and height equally, so a 16:9 window gives a 1600×900 reference and a
    /// 4:3 one gives 1386×1039 — the shape a batchmode run actually renders at. A chip tuned only to
    /// the wider reference runs under the navigation in the narrower one, which is the failure this
    /// file's constants are chosen to avoid.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The top bar: how far below the top edge it starts, how tall it is, and its gutter.</summary>
        private const float TopBarTop = 14f;
        private const float TopBarHeight = 64f;
        private const float TopBarGap = 12f;

        /// <summary>The brand block's width, and the objective card's below it.</summary>
        private const float BrandWidth = 280f;
        private const float ObjectiveWidth = 330f;
        private const float ObjectiveHeight = 276f;

        /// <summary>
        /// The week · house · HoH chip, and one of its three cells.
        ///
        /// <para>340 wide rather than the mockup's half-screen banner, because the gap it has to sit
        /// in is the narrowest thing on the top bar. The pill is centred and the navigation is
        /// anchored to the right edge, so the clearance between them is
        /// <c>width/2 − 170 − 489</c> reference pixels: positive only above a 1318-wide canvas. A
        /// 4:3 window gives 1386 and a 5:4 one gives 1342, so this fits both; the 392 it was first
        /// drawn at did not fit the second.</para>
        /// </summary>
        private const float PillWidth = 340f;
        private const float PillCell = 108f;

        /// <summary>
        /// The right column of cards: inset past the icon rail, which owns the last 52 px of the
        /// gutter, and started below the top bar.
        /// </summary>
        public const float RightColumnWidth = 286f;
        private const float RightColumnInset = 88f;
        private const float RightColumnTop = 104f;
        private const float RightColumnGap = 12f;

        /// <summary>The recent-events card, named so a test can find it the way the others are found.</summary>
        public const string RecentEventsCardName = "Recent events";

        /// <summary>How many events the column shows, and the longest line one of them may occupy.</summary>
        private const int RecentEventRows = 4;
        private const int RecentEventLetters = 54;

        /// <summary>
        /// The mockups' masthead: the house mark, the wordmark in the display weight, and the season
        /// as a strap beneath it.
        /// </summary>
        private void BrandCard(RectTransform column, EpisodeState state)
        {
            var brand = Chrome("Brand", column);
            Size(brand, BrandWidth, TopBarHeight);

            float left = 16f;
            if (HudPrimitives.Glyph("Brand mark", brand, "house", Accent, new Vector2(16f, -19f), 26f) != null) left = 52f;

            // 24 in a 36-high box, not 32 in a 42: Inter's line is taller than Liberation Sans's at
            // the same point size, and this box does not grow with the larger-text preference.
            var mark = FixedText(brand, "GAMESIM", 24, Accent, new Vector2(left, -5f), new Vector2(BrandWidth - left - 14f, 36f));
            var display = UiTheme.Font(UiTheme.Weight.Bold);
            if (display != null) mark.font = display;
            mark.characterSpacing = 6f;

            FixedText(brand, "THE HOUSE  ·  " + (state == null ? "A SEASON" : state.contestants.Count + "-PERSON SEASON"),
                11, UiTheme.Muted, new Vector2(left + 1f, -43f), new Vector2(BrandWidth - left - 14f, 17f));
        }

        /// <summary>
        /// The objective chip: a glyph, the mockups' eyebrow, where the player is being sent next,
        /// the week's beat on a broadcast bug, and the two controls that act on it.
        ///
        /// <para>It stays in the left column rather than moving into the middle of the top bar, where
        /// the mockup draws it, because a ceremony's card spans that middle band and the ceremony
        /// suite asserts the card covers no part of 'Objective'. The band between the brand and the
        /// navigation belongs to the house pill, which that suite deliberately excludes.</para>
        /// </summary>
        private void ObjectiveCard(RectTransform column, EpisodeState state, bool recovery)
        {
            var objective = Chrome("Objective", column);
            Size(objective, ObjectiveWidth, Compact ? 238f : ObjectiveHeight);
            // What to do next is navigation, not an achievement - and this heading is on screen
            // for the whole session, so it set the tone for what gold appeared to mean.
            CardHeading(objective, "CURRENT OBJECTIVE", "task", UiTheme.Accent);

            FixedText(objective, state.pendingDiary != null ? "Next stop: private diary room"
                : EpisodeEngine.IsCompetition(state.phase) ? "Next stop: competition yard"
                : "Next stop: ceremony screen",
                18, Paper, new Vector2(18f, -44f), new Vector2(294f, 48f));

            if(Compact)
            {
                FixedText(objective,Mathf.Max(0,EpisodeEngine.SocialActionBudget(state)-EpisodeEngine.SocialActionsSpent(state))+" social actions remaining",
                    14,Paper,new Vector2(18,-94),new Vector2(294,24));
                FixedButton(objective,"Go to episode screen",new Vector2(18,-126),new Vector2(294,44),director.GoToStation);
                FixedButton(objective,DiaryTravelCaption,new Vector2(18,-178),new Vector2(294,44),director.GoToDiary).interactable=
                    director.HasDiaryRoom && !recovery && state.Find(state.playerId)?.status==ContestantStatus.Active;
                return;
            }

            // Broadcast bug: an accent rule leads the beat of the week, the way a running TV
            // graphic is built.
            var bug = Panel("Phase bug", objective, Accent, 2);
            Anchor(bug, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18f, -106f), new Vector2(4f, 24f));
            bug.GetComponent<Image>().raycastTarget = false;
            // The phase alone. The week moved to the top bar's chip, and carrying it in both places
            // was the same fact twice within 200 px - and the string that overran this box.
            FixedText(objective, EpisodeDirector.PhaseTitle(state.phase).ToUpperInvariant(),
                14, UiTheme.Muted, new Vector2(30f, -106f), new Vector2(284f, 24f));
            FixedText(objective, Mathf.Max(0, EpisodeEngine.SocialActionBudget(state) - EpisodeEngine.SocialActionsSpent(state)) + " social actions remaining",
                14, Paper, new Vector2(18f,-133f),new Vector2(294f,24f));
            FixedButton(objective, "Go to episode screen", new Vector2(18f, -162f), new Vector2(294f, 44f), director.GoToStation);
            FixedButton(objective, DiaryTravelCaption, new Vector2(18f, -214f), new Vector2(294f, 44f), director.GoToDiary).interactable =
                director.HasDiaryRoom && !recovery && state.Find(state.playerId)?.status == ContestantStatus.Active;
        }

        /// <summary>
        /// The top bar's week chip and stat chips, on one pill: the week, how many are still in the
        /// house, and who holds it.
        /// </summary>
        private void HousePill(EpisodeState state)
        {
            var pill = Chrome("House pill", canvas.transform);
            Anchor(pill, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0f, -TopBarTop), new Vector2(PillWidth, TopBarHeight));

            var holder = state.Find(state.hohId);
            StatCell(pill, 8f, "calendar", Accent, "WEEK " + state.week, "SEASON");
            StatCell(pill, 116f, "people", Paper, state.Active.Count() + " of " + state.contestants.Count, "ACTIVE");
            // The first name only, and in lower case: "AWAITING HOH" in capitals is the string that
            // overflowed this cell when the chip was first measured at the larger text size.
            StatCell(pill, 224f, "crown", UiTheme.Gold,
                holder == null ? "Awaiting" : holder.name.Split(' ')[0],
                holder == null ? "NO HOH" : "HOH");
        }

        /// <summary>One of the mockups' stat chips: a glyph, a value, and a small tracked caption.</summary>
        private void StatCell(RectTransform pill, float x, string icon, Color tint, string value, string label)
        {
            float text = x + 4f;
            if (HudPrimitives.Glyph("Stat mark", pill, icon, tint, new Vector2(x + 4f, -22f), 18f) != null) text = x + 26f;
            float width = PillCell - (text - x);
            FixedText(pill, value, 15, tint, new Vector2(text, -13f), new Vector2(width, 24f));
            var caption = FixedText(pill, label, 10, UiTheme.Muted, new Vector2(text + 1f, -38f), new Vector2(width, 16f));
            caption.characterSpacing = 6f;
        }

        /// <summary>
        /// A card heading in the mockups' voice: a glyph, then small tracked capitals in the semibold
        /// weight. The box is sized for Inter's taller line rather than for Liberation Sans's.
        /// </summary>
        private TMP_Text CardHeading(RectTransform card, string words, string icon, Color? tint = null)
        {
            var colour = tint ?? Accent;
            float left = 16f;
            if (HudPrimitives.Glyph("Card mark", card, icon, colour, new Vector2(14f, -10f), 20f) != null) left = 40f;
            // The tint this heading was given, not Accent: the glyph above already uses it, and
            // passing Accent here is what made every card heading in the product the same blue
            // however it was tinted - so nothing could be emphasised by colour.
            var label = FixedText(card, words, 13, colour, new Vector2(left, -9f), new Vector2(card.sizeDelta.x - left - 34f, 24f));
            var font = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (font != null) label.font = font;
            label.characterSpacing = 8f;
            return label;
        }


        /// <summary>The vibe card's name, so a test can find it without guessing.</summary>
        public const string HouseVibeCardName = "House vibe";
        private const float VibeRowHeight = 30f;

        /// <summary>
        /// The house-vibe bars (VISUAL-TARGET.md V2, mockup-04): how much fun, trust, drama and
        /// harmony this week has had in it, as four sums over the week's events.
        ///
        /// <para>Under the objective card rather than in the right column, which is already three
        /// cards deep and runs into the exploration controls. The mockup puts it low on the left
        /// beside the cast for the same reason: it is a glance, not a read, and it wants to be near
        /// the faces it is about.</para>
        ///
        /// <para>Every row carries the count as well as the bar. A length is not something a screen
        /// reader can announce, and the bars are drawn against the largest of the four rather than
        /// against a fixed ceiling - so a quiet week reads as a quiet week rather than as four
        /// empty troughs.</para>
        /// </summary>
        private void HouseVibeCard(RectTransform column, EpisodeState state)
        {
            if (state == null) return;
            var reading = HouseVibe.Of(state);
            var card = Chrome(HouseVibeCardName, column);
            Size(card, ObjectiveWidth, 46f + 4f * VibeRowHeight + 26f);
            var heading = CardHeading(card, "KNOWN HOUSE EVENTS", "people");
            heading.characterSpacing = 2f;

            int index = 0;
            foreach (var row in reading.Rows())
            {
                float y = -(44f + index * VibeRowHeight);
                float textX = 16f;
                if (HudPrimitives.Glyph("Vibe mark", card, row.Icon, row.Tint,
                        new Vector2(14f, y - 2f), 16f) != null) textX = 38f;
                FixedText(card, row.Word, 13, Paper, new Vector2(textX, y), new Vector2(108f, 20f));

                float trackX = textX + 112f;
                float trackWidth = ObjectiveWidth - trackX - 44f;
                var track = Panel("Vibe track", card, UiTheme.Outline, 4);
                Anchor(track, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(trackX, y - 5f), new Vector2(trackWidth, 9f));
                track.GetComponent<Image>().raycastTarget = false;

                float fraction = reading.Fraction(row.Count);
                if (fraction > 0f)
                {
                    var bar = Panel("Vibe fill", card, row.Tint, 4);
                    Anchor(bar, new Vector2(0, 1), new Vector2(0, 1),
                        new Vector2(trackX, y - 5f), new Vector2(trackWidth * fraction, 9f));
                    bar.GetComponent<Image>().raycastTarget = false;
                }

                FixedText(card, row.Count.ToString(), 13, UiTheme.Muted,
                    new Vector2(ObjectiveWidth - 38f, y), new Vector2(26f, 20f));
                index++;
            }

            FixedText(card, HouseVibe.Tension(reading), 12, UiTheme.Muted,
                new Vector2(16f, -(48f + 4f * VibeRowHeight)), new Vector2(ObjectiveWidth - 32f, 20f));
        }

        /// <summary>
        /// The right column (mockup-01, mockup-04): the live feed's picture over a timeline of what
        /// the house has just done.
        ///
        /// <para>The overview is a camera mode rather than a page, and its own column answers the
        /// same question from the same gutter, so the two never share it.</para>
        /// </summary>
        private void RightColumn(EpisodeState state)
        {
            if (director.IsOverview) { OverviewColumn(canvas.transform, RightColumnTop); return; }
            if(Compact)return;
            float top = RightColumnTop;
            if (director.LiveFeedTexture != null) top += LiveFeedCard(canvas.transform, top) + RightColumnGap;
            RecentEventsCard(canvas.transform, state, top);
        }

        /// <summary>
        /// Recent Events as the mockups draw it: a glyph, the week it happened in, and the line the
        /// engine wrote, newest first.
        ///
        /// <para>Only what the player is allowed to have seen. The engine tags a private event with
        /// its audience, and the same rule the notebook and the weekly recap use applies here — a
        /// column that quietly reported NPC-to-NPC secrets would be handing the player information
        /// their character does not have.</para>
        /// </summary>
        private void RecentEventsCard(Transform parent, EpisodeState state, float top)
        {
            if (state == null) return;
            var entries = state.events
                .Where(entry => entry.audienceIds.Count == 0 || entry.audienceIds.Contains(state.playerId))
                .Reverse()
                .Take(RecentEventRows)
                .ToList();

            const float RowHeight = 54f;
            var card = Chrome(RecentEventsCardName, parent);
            Anchor(card, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-RightColumnInset, -top),
                new Vector2(RightColumnWidth, 46f + Mathf.Max(1, entries.Count) * RowHeight));
            CardHeading(card, "RECENT EVENTS", "journal");

            if (entries.Count == 0)
            {
                FixedText(card, "Nothing has happened yet.", 13, UiTheme.Muted, new Vector2(16f, -44f),
                    new Vector2(RightColumnWidth - 32f, 22f));
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                float y = -(40f + i * RowHeight);
                float text = 16f;
                if (HudPrimitives.Glyph("Event mark", card, EventGlyph(entry.kind), Accent, new Vector2(14f, y - 2f), 18f) != null)
                    text = 40f;
                FixedText(card, "WEEK " + entry.week, 10, UiTheme.Muted, new Vector2(text, y), new Vector2(90f, 16f));
                FixedText(card, Excerpt(entry.text, RecentEventLetters), 13, Paper,
                    new Vector2(text, y - 15f), new Vector2(RightColumnWidth - text - 14f, 36f));
            }
        }

        /// <summary>
        /// The glyph a kind of event is drawn with. Decoration on top of a line that already says
        /// what happened; nothing here is the only place a fact appears.
        /// </summary>
        private static string EventGlyph(string kind)
        {
            switch (kind)
            {
                case "nomination": case "final-eviction": return "target";
                case "eviction": case "vote-reveal": case "private-vote": return "gavel";
                case "veto": case "veto-selection": return "veto-token";
                case "competition": return "trophy";
                case "winner": case "jury-vote": case "jury-tie": return "crown";
                case "alliance": return "handshake";
                case "arrival": return "house";
                case "conversation": case "eviction-speech": case "final-speech": return "chat";
                default: return "journal";
            }
        }

        /// <summary>
        /// Trims a line to what a two-line card row holds, on a word boundary.
        ///
        /// <para>The accessibility suite fails any copy that clips, and an engine line has no length
        /// limit — an eviction sentence naming two people and the jury runs well past what 286 px of
        /// card can show. Cutting it here is the difference between a column that ends in an ellipsis
        /// and one that ends mid-word behind an invisible edge.</para>
        /// </summary>
        private static string Excerpt(string value, int letters)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= letters) return value;
            int cut = value.LastIndexOf(' ', Mathf.Min(letters, value.Length - 1));
            if (cut < letters / 2) cut = letters;
            return value.Substring(0, cut).TrimEnd(' ', ',', ';', ':', '·') + "…";
        }
    }
}
