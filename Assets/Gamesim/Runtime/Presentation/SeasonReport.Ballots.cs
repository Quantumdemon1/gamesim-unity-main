using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// How the house voted (UI-UX-PASS-PLAN J0; decisions 7 and 8): every eviction ballot of the
    /// season, read out once it is over - "the tapes". A card a week: who went and by what count,
    /// then the house's ballots on two sides, one a nominee, each voter's face and name under the
    /// nominee they voted to evict, the Head of Household's deciding vote on a row of its own
    /// outside the count, and a ballot marked LIED, with what the voter told the player, where the
    /// reveal judged that claim a lie. A lean overheard that the ballot went against is a vote that
    /// changed, not a lie anyone told: it is said in a muted line under the name, with no mark. The
    /// final eviction is the last Head of Household's one vote.
    ///
    /// <para>Before the finale the section is sealed and says so: <see cref="SeasonBallots.Read"/>
    /// returns nothing for a season still being played, so no face, no name and no count is drawn
    /// here until the jury has crowned a winner. The jury's own ballots stay named in the juror
    /// strip and the jury column above, as they always were (decision 7).</para>
    ///
    /// <para>Decoration only, in the kit the report already wears (Pack 7's section and row frames,
    /// the house's pills and portrait discs): no control, nothing in the keyboard's ring, and no
    /// caption a test finds a control by. Every label is measured to the words it holds at the
    /// width it is given - a name that does not fit its line takes a second, never an ellipsis -
    /// in a box at least 1.3 times its type, so the section reads whole at either text size on any
    /// frame the report is laid out for; the report scrolls, so fitting is about clipping, never
    /// about height.</para>
    /// </summary>
    public sealed partial class SeasonReport
    {
        /// <summary>The section's parts, by name, for a test and a screen reader.</summary>
        public const string HouseBallotsName = "House ballots", BallotWeekName = "Ballot week", BallotSideName = "Ballot side",
            BallotRowName = "Ballot row", LieMarkName = "Lie mark", LieLineName = "Lie line", ChangedLineName = "Changed line", TieBreakMarkName = "Tie-break mark",
            SoleVoteMarkName = "Sole vote mark", TapesSealedName = "Tapes sealed", NotOnRecordName = "Not on the record",
            MissingBallotsName = "Missing ballots";

        /// <summary>The section's width at and past which the weeks stand two to a row.</summary>
        private const float BallotWeeksTwoUp = 1240f;

        /// <summary>A week card's inner width at and past which its two sides stand side by side.</summary>
        private const float BallotSidesTwoUp = 520f;

        /// <summary>A ballot row's least height: its face and one line of name.</summary>
        private const float BallotRowHeight = 38f;

        /// <summary>The section in the report's scroll, after Game Sense and before the house table.</summary>
        private void HouseBallots(EpisodeState state, Func<string, Texture> portrait)
        {
            Heading(SeasonBallots.Heading);
            float inner = Width - Pad * 2f;
            var section = EndScreenKit.Box(HouseBallotsName, content, Pad, cursor, inner, 10f);
            float height = DrawHouseBallots(section, state, portrait, inner);
            section.sizeDelta = new Vector2(inner, height);
            cursor += height;
        }

        /// <summary>
        /// Draws the section into <paramref name="section"/> at <paramref name="width"/>: the sealed
        /// line before the finale; from it the intro and a card a week, two to a row on a wide
        /// report. Returns the height it took.
        /// </summary>
        private static float DrawHouseBallots(RectTransform section, EpisodeState state, Func<string, Texture> portrait, float width)
        {
            portrait = portrait ?? (_ => null);
            if (!SeasonBallots.Open(state))
            {
                var sealedLine = EndScreenKit.Text(TapesSealedName, section, SeasonBallots.SealedLine, 15f, UiTheme.Muted, 0f, 0f, width, 20f);
                return EndScreenKit.Wrapped(sealedLine, width) + 6f;
            }

            var weeks = SeasonBallots.Read(state);
            if (weeks.Count == 0)
            {
                var none = EndScreenKit.Text("Empty", section, SeasonBallots.EmptyLine, 15f, UiTheme.Muted, 0f, 0f, width, 20f);
                return EndScreenKit.Wrapped(none, width) + 6f;
            }
            var lead = EndScreenKit.Text("Intro", section, SeasonBallots.IntroFor(state, weeks), 15f, UiTheme.Muted, 0f, 0f, width, 20f);
            float y = EndScreenKit.Wrapped(lead, width) + 12f;

            const float gap = 16f;
            int perRow = width >= BallotWeeksTwoUp ? 2 : 1;
            float cardWidth = (width - gap * (perRow - 1)) / perRow;
            for (int first = 0; first < weeks.Count; first += perRow)
            {
                // The cards of a row stand one height, so their frames line up.
                var row = new List<RectTransform>();
                float rowHeight = 0f;
                for (int i = first; i < Mathf.Min(weeks.Count, first + perRow); i++)
                {
                    var card = BallotWeek(section, state, weeks[i], portrait, (i - first) * (cardWidth + gap), y, cardWidth);
                    rowHeight = Mathf.Max(rowHeight, card.sizeDelta.y);
                    row.Add(card);
                }
                foreach (var card in row) card.sizeDelta = new Vector2(cardWidth, rowHeight);
                y += rowHeight + gap;
            }
            return y - gap;
        }

        /// <summary>
        /// One eviction: 'WEEK n', who went and the count, then the two sides - the one who went
        /// first, in danger red, the one who stayed in steel - side by side where the card is wide
        /// enough, else one over the other; the tie-break under them; and whose ballots the record no
        /// longer holds. The final eviction is its one vote. Sized to what it holds.
        /// </summary>
        private static RectTransform BallotWeek(RectTransform parent, EpisodeState s, SeasonBallots.Week week, Func<string, Texture> portrait,
            float x, float y, float width)
        {
            var card = EndScreenKit.Box(BallotWeekName + " " + week.week, parent, x, y, width, 10f);
            const float pad = 16f, gap = 12f;
            float inner = width - pad * 2f, at = pad;

            var eyebrow = EndScreenKit.Text("Eyebrow", card, SeasonBallots.Eyebrow(week), 13f, week.final ? UiTheme.Gold : UiTheme.Heading,
                pad, at, inner, 18f, TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
            eyebrow.characterSpacing = 3f;
            at += EndScreenKit.Wrapped(eyebrow, inner) + 2f;
            var title = EndScreenKit.Text("Evicted", card, SeasonBallots.Title(s, week), 18f, UiTheme.Paper,
                pad, at, inner, 24f, TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
            at += EndScreenKit.Wrapped(title, inner);
            var count = EndScreenKit.Text("Count", card, SeasonBallots.CountWords(s, week), 14f, UiTheme.Muted, pad, at, inner, 20f);
            at += EndScreenKit.Wrapped(count, inner) + 10f;

            if (week.final)
            {
                var choice = week.FinalChoice;
                if (choice != null)
                    at += BallotRow(card, s, choice, UiTheme.Gold, SeasonBallots.SoleVoteWord, UiTheme.Gold, SoleVoteMarkName,
                        SeasonBallots.FinalWords(s, week), "Final line", UiTheme.Muted, portrait, pad, at, inner) + 6f;
            }
            else
            {
                var sides = week.Sides.ToList();
                bool beside = sides.Count == 2 && inner >= BallotSidesTwoUp;
                float sideWidth = beside ? (inner - gap) * .5f : inner;
                float tallest = 0f, stacked = 0f;
                for (int i = 0; i < sides.Count; i++)
                {
                    var tint = sides[i] == week.evictedId ? UiTheme.Danger : EndScreenKit.Steel;
                    float height = BallotSide(card, s, week, sides[i], tint, portrait,
                        pad + (beside ? i * (sideWidth + gap) : 0f), at + (beside ? 0f : stacked), sideWidth);
                    tallest = Mathf.Max(tallest, height);
                    stacked += height + gap;
                }
                at += (beside ? tallest : Mathf.Max(0f, stacked - gap)) + 8f;
                var breaker = week.TieBreak;
                if (breaker != null)
                    at += BallotRow(card, s, breaker, UiTheme.Gold, SeasonBallots.TieBreakWord, UiTheme.Gold, TieBreakMarkName,
                        SeasonBallots.TieBreakWords(s, week), "Tie-break line", UiTheme.Gold, portrait, pad, at, inner) + 6f;
                string missing = SeasonBallots.MissingWords(s, week);
                if (missing != null)
                {
                    var line = EndScreenKit.Text(NotOnRecordName, card, missing, 13f, UiTheme.Muted, pad, at, inner, 18f);
                    at += EndScreenKit.Wrapped(line, inner) + 4f;
                }
            }
            float total = at - 6f + pad;
            card.sizeDelta = new Vector2(width, total);
            EndScreenKit.Frame(card, PackArt.SeasonSection, 16f, UiTheme.Surface);
            return card;
        }

        /// <summary>
        /// One side of a week: 'TO EVICT MAYA (3)' in its colour, the house's count against that
        /// nominee, then a row a ballot - a lie marked - and a line counting the ballots against them
        /// the record no longer holds. Returns its height.
        /// </summary>
        private static float BallotSide(RectTransform card, EpisodeState s, SeasonBallots.Week week, string nomineeId, Color tint,
            Func<string, Texture> portrait, float x, float y, float width)
        {
            var side = EndScreenKit.Box(BallotSideName, card, x, y, width, 10f);
            var head = EndScreenKit.Text("Side", side, SeasonBallots.SideHeading(s, week, nomineeId), 13f, tint, 0f, 0f, width, 18f,
                TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
            head.characterSpacing = 2f;
            float at = EndScreenKit.Wrapped(head, width) + 6f;
            var ballots = week.Side(nomineeId).ToList();
            // A lie told to the player in the warning's orange, filled: apart from the evicted
            // side's red and the other side's steel, so the mark reads as a mark on either side. A
            // lean overheard that the vote went against is no lie told: a muted line, no mark.
            foreach (var ballot in ballots)
                at += ballot.Lied
                    ? BallotRow(side, s, ballot, tint, SeasonBallots.LieWord, UiTheme.Warning, LieMarkName,
                        SeasonBallots.LieWords(s, ballot), LieLineName, UiTheme.Warning, portrait, 0f, at, width) + 6f
                    : BallotRow(side, s, ballot, tint, null, UiTheme.Warning, LieMarkName,
                        SeasonBallots.ChangedWords(s, ballot), ChangedLineName, UiTheme.Muted, portrait, 0f, at, width) + 6f;
            int missing = week.MissingAgainst(nomineeId);
            if (missing > 0 || ballots.Count == 0)
            {
                var line = EndScreenKit.Text(MissingBallotsName, side, missing > 0 ? SeasonBallots.MissingSideWords(missing) : "No votes.", 13f, UiTheme.Muted,
                    0f, at, width, 18f);
                at += EndScreenKit.Wrapped(line, width) + 6f;
            }
            at -= 6f;
            side.sizeDelta = new Vector2(width, at);
            return at;
        }

        /// <summary>
        /// One ballot: the voter's face ringed in <paramref name="ring"/>, their name - 'You' for the
        /// player - and, where given, a filled pill on the right (LIED, TIE-BREAK, SOLE VOTE) and a
        /// line under the name saying what the mark is about. The pill is as wide as its word and the
        /// name takes the rest, wrapping rather than cutting. Returns its height.
        /// </summary>
        private static float BallotRow(RectTransform parent, EpisodeState s, SeasonBallots.Row ballot, Color ring, string mark, Color markTint,
            string markName, string detail, string detailName, Color detailTint, Func<string, Texture> portrait, float x, float y, float width)
        {
            var voter = s.Find(ballot.voterId);
            bool you = ballot.voterId == s.playerId;
            var row = EndScreenKit.Box(BallotRowName, parent, x, y, width, BallotRowHeight);
            const float face = 26f, textX = 44f, top = 9f, markHeight = 22f;
            var disc = HudPrimitives.Portrait(row, portrait(ballot.voterId), ring, face, 2f, false, voter);
            disc.anchorMin = disc.anchorMax = new Vector2(0f, 1f);
            disc.pivot = new Vector2(.5f, .5f);
            disc.anchoredPosition = new Vector2(22f, -BallotRowHeight * .5f);

            float right = 8f;
            if (!string.IsNullOrEmpty(mark))
            {
                // As wide as its word, with the pill's own margins: a word that wrapped in its pill
                // would stand on a second line the pill has no room for.
                var pill = EndScreenKit.Pill(row, mark, markTint, 0f, 8f, 80f, markHeight, true);
                pill.name = markName;
                var word = pill.GetComponentInChildren<TMP_Text>();
                float pillWidth = Mathf.Min(width * .45f, (word != null ? Mathf.Ceil(word.GetPreferredValues(word.text).x) : 60f) + 22f);
                EndScreenKit.Place(pill, width - pillWidth - 8f, (BallotRowHeight - markHeight) * .5f, pillWidth, markHeight);
                right += pillWidth + 8f;
            }
            float nameWidth = Mathf.Max(40f, width - textX - right);
            var name = EndScreenKit.Text("Name", row, HudPrimitives.WithYou(voter?.name ?? ballot.voterId, you), 14f, you ? UiTheme.Accent : UiTheme.Paper,
                textX, top, nameWidth, 19f, TextAlignmentOptions.Left, UiTheme.Weight.Medium);
            float at = top + EndScreenKit.Wrapped(name, nameWidth);
            if (!string.IsNullOrEmpty(detail))
            {
                float detailWidth = Mathf.Max(40f, width - textX - 8f);
                var line = EndScreenKit.Text(detailName, row, detail, 12f, detailTint, textX, at + 1f, detailWidth, 16f);
                at += 1f + EndScreenKit.Wrapped(line, detailWidth);
            }
            float height = Mathf.Max(BallotRowHeight, at + 8f);
            row.sizeDelta = new Vector2(width, height);
            EndScreenKit.Frame(row, PackArt.SeasonJuryRowNeutral, 10f, UiTheme.SurfaceRaised, null, 8);
            return height;
        }
    }
}
