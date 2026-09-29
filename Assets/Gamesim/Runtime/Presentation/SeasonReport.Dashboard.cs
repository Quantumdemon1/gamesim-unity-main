using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The season at a glance (the owner's Season Complete mockup, Pack 7): the winner and the
    /// runner-up with the jury's count, the five Game Sense cards, then three columns - the final
    /// standings, how the jury voted, and the season week by week - and the career under them.
    ///
    /// <para>Nothing here is invented. The quote under a finalist is the first sentence of their own
    /// final speech, or nothing; the count is the jury's ballots; a card's line is that face's own
    /// strongest row in the notebook; the weeks are the ledger's power rows, one a week, the final
    /// week's three parts and the last Head of Household's choice included. The mockup's taglines,
    /// its "3 — 2 — 1" and its quotes had no source, and are not here.</para>
    ///
    /// <para>Words tests read are kept word for word: the Game Sense captions, "How the jury voted",
    /// "voted for {name}" and each juror's recorded reason, the crown line "{winner} beat
    /// {runner-up} in the jury vote.", and the career's captions. The standings and the weeks say
    /// "HoH", never the "HOH WINS" the cards below count.</para>
    /// </summary>
    public sealed partial class SeasonReport
    {
        /// <summary>The runner-up's steel: not the gold of the win, and not the cyan the controls wear.</summary>
        private static readonly Color Steel = new Color(.68f, .77f, .86f);
        /// <summary>The Game Sense card's teal, and the social card's violet, as the pack draws those cards.</summary>
        private static readonly Color Teal = new Color(.33f, .87f, .86f);
        private static readonly Color Violet = new Color(.66f, .49f, 1f);

        private const float Gap = 16f;

        private void Dashboard(EpisodeState state, Func<string, Texture> portrait)
        {
            float inner = Width - Pad * 2f;
            var board = EndScreenKit.Box("Dashboard", content, Pad, cursor, inner, 10f);
            float y = 0f;
            y += Hero(board, state, portrait, inner) + Gap;
            y += StatCards(board, state, y, inner) + Gap;
            y += Columns(board, state, portrait, y, inner) + Gap;
            if (shownCareer != null && shownCareer.Seasons > 0) y += CareerStrip(board, shownCareer, y, inner) + Gap;
            board.sizeDelta = new Vector2(inner, y);
            cursor += y;
        }

        // ---------------------------------------------------------------- the hero

        /// <summary>
        /// The winner in gold and the runner-up in steel, each with their face, their place, the
        /// first line of their final speech and their share of the jury; and the verdict beside
        /// them: the crown line, the count in words and as a bar. A season still being played has
        /// no winner, and says so.
        /// </summary>
        private float Hero(RectTransform board, EpisodeState state, Func<string, Texture> portrait, float inner)
        {
            var winner = state.Find(state.winnerId);
            var runnerUp = state.Find(state.runnerUpId);
            var hero = EndScreenKit.Box(HeroName, board, 0f, 0f, inner, 10f);
            if (winner == null)
            {
                const float waiting = 96f;
                hero.sizeDelta = new Vector2(inner, waiting);
                EndScreenKit.Frame(hero, PackArt.SeasonSection, 16f, UiTheme.Surface);
                EndScreenKit.Text("Waiting", hero, "No winner yet: the season is still being played.", 20f, UiTheme.Paper,
                    24f, 22f, inner - 48f, 28f, TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
                EndScreenKit.Text("Week", hero, "Week " + state.week + ". The jury votes at the finale.", 15f, UiTheme.Muted,
                    24f, 54f, inner - 48f, 22f);
                return waiting;
            }

            var ballots = JuryBallots(state);
            int forWinner = ballots.Count(b => b.FinalistId == winner.id);
            int forRunnerUp = runnerUp == null ? 0 : ballots.Count(b => b.FinalistId == runnerUp.id);
            bool wide = inner >= 1150f;
            float height = 206f;
            float winnerWidth = wide ? inner * .41f : (inner - Gap) * .55f;
            float runnerWidth = runnerUp == null ? 0f : wide ? inner * .30f : inner - winnerWidth - Gap;
            float verdictX = wide ? winnerWidth + (runnerUp == null ? 0f : runnerWidth + Gap) + Gap : 0f;
            float verdictWidth = wide ? inner - verdictX : inner;

            Finalist(hero, state, winner, portrait, 0f, winnerWidth, height, true, forWinner);
            if (runnerUp != null) Finalist(hero, state, runnerUp, portrait, winnerWidth + Gap, runnerWidth, height, false, forRunnerUp);

            float verdictY = wide ? 0f : height + Gap;
            float verdictHeight = wide ? height : 132f;
            var verdict = EndScreenKit.Box("Verdict", hero, verdictX, verdictY, verdictWidth, verdictHeight);
            EndScreenKit.Frame(verdict, PackArt.SeasonJurySummary, 14f, UiTheme.Surface);
            float pad = 20f;
            EndScreenKit.Text("Eyebrow", verdict, "THE JURY'S VERDICT", 13f, UiTheme.Heading, pad, 16f, verdictWidth - pad * 2f, 20f,
                TextAlignmentOptions.Left, UiTheme.Weight.SemiBold).characterSpacing = 3f;
            // The crown line, word for word: the finale's exits and the whole-season walk read it.
            var crown = EndScreenKit.Text("Crown", verdict, runnerUp != null
                    ? winner.name + " beat " + runnerUp.name + " in the jury vote."
                    : winner.name + " wins the season.", 18f, UiTheme.Paper, pad, 40f, verdictWidth - pad * 2f, 26f,
                TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
            float at = 40f + EndScreenKit.Wrapped(crown, verdictWidth - pad * 2f) + 6f;
            foreach (string line in VerdictLines(winner, runnerUp, forWinner, forRunnerUp))
            {
                var words = EndScreenKit.Text("Count", verdict, line, 14f, UiTheme.Muted, pad, at, verdictWidth - pad * 2f, 20f);
                at += EndScreenKit.Wrapped(words, verdictWidth - pad * 2f) + 2f;
            }
            if (runnerUp != null && forWinner + forRunnerUp > 0)
            {
                float barY = Mathf.Max(at + 8f, verdictHeight - 50f);
                EndScreenKit.SplitBar(verdict, pad, barY, verdictWidth - pad * 2f, 12f, forWinner, forRunnerUp, UiTheme.Gold, Steel);
                EndScreenKit.Text("Winner count", verdict, FinalistRead.FirstName(winner.name) + " " + forWinner, 13f, UiTheme.Gold,
                    pad, barY + 16f, (verdictWidth - pad * 2f) * .5f, 20f);
                EndScreenKit.Text("Runner-up count", verdict, forRunnerUp + " " + FinalistRead.FirstName(runnerUp.name), 13f, Steel,
                    verdictWidth * .5f, barY + 16f, verdictWidth * .5f - pad, 20f, TextAlignmentOptions.Right);
            }
            float total = wide ? height : height + Gap + verdictHeight;
            hero.sizeDelta = new Vector2(inner, total);
            return total;
        }

        /// <summary>The count in words, the house's own: a majority, a tie and its rule, or a jury of one.</summary>
        private static IEnumerable<string> VerdictLines(ContestantState winner, ContestantState runnerUp, int forWinner, int forRunnerUp)
        {
            if (runnerUp == null || forWinner + forRunnerUp == 0) yield break;
            if (forWinner == forRunnerUp)
            {
                yield return "The jury is tied, " + forWinner + " to " + forRunnerUp + ".";
                yield return "Under the house's tie rule, the win goes to " + HudPrimitives.WithYou(winner.name, winner.isPlayer) + ".";
            }
            else if (forWinner + forRunnerUp == 1) yield return "With the jury's only vote.";
            else yield return "By a vote of " + forWinner + " to " + forRunnerUp + ".";
        }

        /// <summary>One finalist's half of the hero.</summary>
        private void Finalist(RectTransform hero, EpisodeState state, ContestantState who, Func<string, Texture> portrait,
            float x, float width, float height, bool won, int votes)
        {
            var card = EndScreenKit.Box(won ? "WINNER" : "RUNNER-UP", hero, x, 0f, width, height);
            EndScreenKit.Frame(card, won ? PackArt.SeasonWinnerHero : PackArt.SeasonRunnerUpHero, won ? 18f : 16f,
                UiTheme.SurfaceRaised, won ? UiTheme.Gold : Steel);
            // The winner lit in gold: this is where the season is won.
            if (won)
            {
                var gold = UiTheme.Pack(PackArt.GlowGold);
                if (gold != null)
                {
                    var light = new GameObject("Winner glow", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    light.rectTransform.SetParent(card, false);
                    EndScreenKit.Place(light.rectTransform, -20f, -20f, 220f, 220f);
                    light.sprite = gold; light.color = new Color(1f, 1f, 1f, .45f); light.preserveAspect = true; light.raycastTarget = false;
                }
            }
            float diameter = won ? 132f : 110f;
            float faceX = 22f + diameter * .5f + 4f;
            var face = HudPrimitives.Portrait(card, portrait(who.id), won ? UiTheme.Gold : Steel, diameter, 4f, false, who);
            face.anchorMin = face.anchorMax = new Vector2(0f, 1f);
            face.pivot = new Vector2(.5f, .5f);
            face.anchoredPosition = new Vector2(faceX, -height * .5f);
            var mark = EndScreenKit.Picture(won ? "Crown mark" : "Runner-up mark", card, won ? PackArt.SeasonWinnerCrown : PackArt.SeasonBadgeRunnerUp,
                won ? "crown" : "star", won ? UiTheme.Gold : Steel, new Vector2(faceX - diameter * .38f, -(height * .5f - diameter * .40f)), won ? 40f : 32f);

            float textX = faceX + diameter * .5f + 22f;
            float textWidth = Mathf.Max(80f, width - textX - 18f);
            var badge = EndScreenKit.Text("Badge", card, won ? "WINNER" : "RUNNER-UP", 15f, won ? UiTheme.Gold : Steel,
                textX, 26f, textWidth, 22f, TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
            badge.characterSpacing = 3f;
            var name = EndScreenKit.Text("Name", card, HudPrimitives.WithYou(who.name, who.isPlayer), won ? 30f : 24f, UiTheme.Paper,
                textX, 50f, textWidth, won ? 40f : 34f, TextAlignmentOptions.Left, UiTheme.Weight.Bold);
            name.enableAutoSizing = true; name.fontSizeMax = won ? 30f : 24f; name.fontSizeMin = 16f;
            float y = won ? 94f : 88f;
            string quote = EndScreenKit.Excerpt(state.finalSpeeches?.FirstOrDefault(s => s.speakerId == who.id)?.text, won ? 110 : 80);
            if (quote != null)
            {
                var said = EndScreenKit.Text("Quote", card, "“" + quote + "”", 15f, new Color(UiTheme.Paper.r, UiTheme.Paper.g, UiTheme.Paper.b, .86f),
                    textX, y, textWidth, 20f);
                said.fontStyle = FontStyles.Italic;
                y += EndScreenKit.Wrapped(said, textWidth, 2) + 2f;
                EndScreenKit.Text("Attribution", card, "From the final speech", 12f, UiTheme.Muted, textX, y, textWidth, 18f);
            }
            EndScreenKit.Text("Votes", card, votes + (votes == 1 ? " jury vote" : " jury votes"), 16f, won ? UiTheme.Gold : Steel,
                textX, height - 42f, textWidth, 22f, TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
        }

        // ---------------------------------------------------------------- the stat cards

        /// <summary>
        /// Game Sense's number and its three faces, and the chances the season offered, as five
        /// cards. Each carries its caption word for word (tests read them) and one real line: the
        /// face's own strongest row in the notebook, or what the card is.
        /// </summary>
        private float StatCards(RectTransform board, EpisodeState state, float y, float inner)
        {
            var report = GameSense.Evaluate(state);
            int offered = state.ledger.opportunities.Count;
            int taken = state.ledger.opportunities.Count(o => o.response == OpportunityResponse.Taken);
            var cards = new[]
            {
                ("GAME SENSE", report.score.ToString(), Teal, PackArt.SeasonStatGameSense, PackArt.SeasonIconGameSense, "bulb",
                    "Every point is a row in the notebook."),
                ("COMPETITIONS", report.competitions.ToString(), UiTheme.Gold, PackArt.SeasonStatCompetitions, PackArt.SeasonIconCompetitions, "trophy",
                    FaceLine(report, GameSense.Competitions)),
                ("STRATEGY", report.strategy.ToString(), UiTheme.Positive, PackArt.SeasonStatStrategy, PackArt.SeasonIconStrategy, "target",
                    FaceLine(report, GameSense.Strategy)),
                ("SOCIAL", report.social.ToString(), Violet, PackArt.SeasonStatSocial, PackArt.SeasonIconSocial, "people",
                    FaceLine(report, GameSense.Social)),
                ("CHANCES TAKEN", taken + " of " + offered, offered == 0 || taken * 2 >= offered ? UiTheme.Positive : UiTheme.Danger,
                    PackArt.SeasonStatChances, PackArt.SeasonIconChances, "task", "The offers, pleas and plays you took up."),
            };
            int perRow = inner >= 1100f ? 5 : 3;
            float width = (inner - Gap * (perRow - 1)) / perRow;
            // Tall enough for a line of three under the number: a face's row in the notebook runs long.
            const float height = 144f;
            var row = EndScreenKit.Box(GameSenseCardName, board, 0f, y, inner, 10f);
            for (int i = 0; i < cards.Length; i++)
            {
                var (caption, value, tint, frame, icon, fallback, line) = cards[i];
                int column = i % perRow, band = i / perRow;
                EndScreenKit.StatCard(row, caption, column * (width + Gap), band * (height + Gap), width, height,
                    frame, icon, fallback, value, caption, line, tint);
            }
            int bands = (cards.Length + perRow - 1) / perRow;
            float total = bands * height + (bands - 1) * Gap;
            row.sizeDelta = new Vector2(inner, total);
            return total;
        }

        /// <summary>A face's strongest row in the notebook, either way: the one that moved it most.</summary>
        private static string FaceLine(GameSense.Report report, string face)
        {
            var note = report.notes.Where(n => n.face == face && n.rowKind != "standing")
                .OrderByDescending(n => Math.Abs(n.points)).ThenBy(n => n.week).FirstOrDefault();
            return note == null ? "No rows this season." : note.text;
        }

        // ---------------------------------------------------------------- the three columns

        private float Columns(RectTransform board, EpisodeState state, Func<string, Texture> portrait, float y, float inner)
        {
            bool three = inner >= 1150f;
            float standingsWidth = three ? (inner - Gap * 2f) * .30f : (inner - Gap) * .42f;
            float juryWidth = three ? (inner - Gap * 2f) * .38f : inner - standingsWidth - Gap;
            float timelineWidth = three ? inner - standingsWidth - juryWidth - Gap * 2f : inner;

            var standings = EndScreenKit.Box(StandingsName, board, 0f, y, standingsWidth, 10f);
            var jury = EndScreenKit.Box(JuryColumnName, board, standingsWidth + Gap, y, juryWidth, 10f);
            float standingsHeight = Standings(standings, state, portrait, standingsWidth);
            float juryHeight = JuryColumn(jury, state, portrait, juryWidth);
            float firstRow = Mathf.Max(standingsHeight, juryHeight);
            float timelineX = three ? standingsWidth + juryWidth + Gap * 2f : 0f;
            float timelineY = three ? y : y + firstRow + Gap;
            var timeline = EndScreenKit.Box(TimelineName, board, timelineX, timelineY, timelineWidth, 10f);
            float timelineHeight = Timeline(timeline, state, timelineWidth);

            // One height for the columns on a row, so their frames line up.
            float rowHeight = three ? Mathf.Max(firstRow, timelineHeight) : firstRow;
            foreach (var column in three ? new[] { standings, jury, timeline } : new[] { standings, jury })
            {
                column.sizeDelta = new Vector2(column.sizeDelta.x, rowHeight);
                EndScreenKit.Frame(column, column == timeline ? PackArt.SeasonSection : PackArt.SeasonSection, 16f, UiTheme.Surface);
            }
            if (!three)
            {
                timeline.sizeDelta = new Vector2(timelineWidth, timelineHeight);
                EndScreenKit.Frame(timeline, PackArt.SeasonSection, 16f, UiTheme.Surface);
                return firstRow + Gap + timelineHeight;
            }
            return rowHeight;
        }

        /// <summary>
        /// Everybody, in the order they finished: the one placement every screen reads
        /// (<see cref="CareerLedger.Placement"/>), the order the jury ledger filled with production's
        /// removals merged in by week. The winner in gold, the runner-up in steel, third in bronze,
        /// the jury and the rest muted.
        /// </summary>
        private float Standings(RectTransform column, EpisodeState state, Func<string, Texture> portrait, float width)
        {
            const float pad = 16f, rowHeight = 42f, rowGap = 6f;
            float y = pad + EndScreenKit.Heading(column, "Final standings", null, "trophy", pad, pad, width - pad * 2f);
            var order = state.contestants
                .Select(c => (who: c, place: c.status == ContestantStatus.Active ? 0 : CareerLedger.Placement(state, c)))
                .OrderBy(e => e.place == 0 ? int.MaxValue : e.place).ThenBy(e => PlacementRank(e.who.status)).ThenBy(e => e.who.name, StringComparer.CurrentCulture)
                .ToList();
            foreach (var (who, place) in order)
            {
                var row = EndScreenKit.Box("Standing", column, pad, y, width - pad * 2f, rowHeight);
                string frame = who.status == ContestantStatus.Winner ? PackArt.SeasonStandingWinner
                    : who.status == ContestantStatus.RunnerUp ? PackArt.SeasonStandingRunnerUp
                    : place == 3 && who.status == ContestantStatus.Jury ? PackArt.SeasonStandingThird
                    : who.status == ContestantStatus.Jury ? PackArt.SeasonStandingJury : PackArt.SeasonStandingPreJury;
                EndScreenKit.Frame(row, frame, 10f, who.status == ContestantStatus.Winner ? new Color(.2f, .17f, .06f, .95f) : UiTheme.Surface,
                    who.status == ContestantStatus.Winner ? UiTheme.Gold : (Color?)null, 8);
                float w = width - pad * 2f;
                var tint = StandingTint(who.status, place);
                EndScreenKit.Text("Place", row, place > 0 ? place.ToString() : "·", 17f, place == 1 ? UiTheme.Gold : UiTheme.Paper,
                    8f, 10f, 28f, 22f, TextAlignmentOptions.Center, UiTheme.Weight.SemiBold);
                var face = HudPrimitives.Portrait(row, portrait(who.id), tint, 28f, 2f, who.status == ContestantStatus.Evicted, who);
                face.anchorMin = face.anchorMax = new Vector2(0f, .5f);
                face.pivot = new Vector2(.5f, .5f);
                face.anchoredPosition = new Vector2(56f, 0f);
                float pillWidth = Mathf.Min(118f, w * .34f);
                var name = EndScreenKit.Text("Name", row, HudPrimitives.WithYou(who.name, who.isPlayer), 15f,
                    who.isPlayer ? UiTheme.Accent : UiTheme.Paper, 80f, 10f, Mathf.Max(40f, w - 80f - pillWidth - 12f), 22f,
                    TextAlignmentOptions.Left, UiTheme.Weight.Medium);
                name.enableAutoSizing = true; name.fontSizeMax = 15f; name.fontSizeMin = 11f;
                var pill = EndScreenKit.Pill(row, PillWord(who.status), tint, w - pillWidth - 8f, 10f, pillWidth, 22f,
                    who.status == ContestantStatus.Winner);
                var word = pill.GetComponentInChildren<TMP_Text>();
                if (word != null) { word.enableAutoSizing = true; word.fontSizeMax = 11f; word.fontSizeMin = 9f; }
                y += rowHeight + rowGap;
            }
            return y + pad - rowGap;
        }

        /// <summary>A status as short as a standings pill holds it; the house table keeps the long words.</summary>
        private static string PillWord(ContestantStatus status)
        {
            switch (status)
            {
                case ContestantStatus.Expelled: return "REMOVED";
                case ContestantStatus.Active: return "IN THE HOUSE";
                default: return StatusWord(status).ToUpperInvariant();
            }
        }

        private static Color StandingTint(ContestantStatus status, int place)
        {
            switch (status)
            {
                case ContestantStatus.Winner: return UiTheme.Gold;
                case ContestantStatus.RunnerUp: return Steel;
                case ContestantStatus.Jury: return place == 3 ? UiTheme.Warning : UiTheme.Muted;
                case ContestantStatus.Expelled: return UiTheme.Conflict;
                case ContestantStatus.Active: return UiTheme.Positive;
                default: return UiTheme.Muted;
            }
        }

        /// <summary>
        /// How the jury voted: every ballot, in the order cast, with the juror's face, whom they voted
        /// for, and the reason the engine recorded, whole - a reason cut short in its text would be
        /// words the juror did not give. The player's own row says it was theirs.
        /// </summary>
        private float JuryColumn(RectTransform column, EpisodeState state, Func<string, Texture> portrait, float width)
        {
            const float pad = 16f, rowGap = 6f;
            var ballots = JuryBallots(state);
            var winner = state.Find(state.winnerId);
            var runnerUp = state.Find(state.runnerUpId);
            string tally = winner != null && runnerUp != null
                ? winner.name + " " + ballots.Count(b => b.FinalistId == winner.id) + " · " + runnerUp.name + " " + ballots.Count(b => b.FinalistId == runnerUp.id)
                : null;
            float y = pad + EndScreenKit.Heading(column, "How the jury voted", tally ?? "And why, in each juror's words.", "gavel", pad, pad, width - pad * 2f);
            if (ballots.Count == 0)
            {
                EndScreenKit.Text("Empty", column, "The jury has not voted.", 14f, UiTheme.Muted, pad, y, width - pad * 2f, 22f);
                return y + 22f + pad;
            }
            float w = width - pad * 2f;
            foreach (var ballot in ballots)
            {
                bool forWinner = winner != null && ballot.FinalistId == winner.id;
                var row = EndScreenKit.Box("Ballot", column, pad, y, w, 50f);
                var face = HudPrimitives.Portrait(row, ballot.JurorId == null ? null : portrait(ballot.JurorId),
                    UiTheme.Outline, 28f, 2f, false, ballot.JurorId == null ? null : state.Find(ballot.JurorId));
                face.anchorMin = face.anchorMax = new Vector2(0f, 1f);
                face.pivot = new Vector2(.5f, .5f);
                face.anchoredPosition = new Vector2(24f, -25f);
                float nameWidth = Mathf.Min(190f, (w - 50f) * .45f);
                var name = EndScreenKit.Text("Juror", row, HudPrimitives.WithYou(ballot.Juror, ballot.IsPlayer), 14f,
                    ballot.IsPlayer ? UiTheme.Accent : UiTheme.Paper, 48f, 8f, nameWidth, 20f, TextAlignmentOptions.Left, UiTheme.Weight.Medium);
                name.enableAutoSizing = true; name.fontSizeMax = 14f; name.fontSizeMin = 10f;
                // Word for word: tests count these, one for every ballot.
                var pick = EndScreenKit.Text("Pick", row, "voted for " + ballot.Finalist, 14f, forWinner ? UiTheme.Gold : Steel,
                    48f + nameWidth + 8f, 8f, w - 56f - nameWidth - 8f, 20f);
                pick.enableAutoSizing = true; pick.fontSizeMax = 14f; pick.fontSizeMin = 10f;
                var why = EndScreenKit.Text("Reason", row, ballot.IsPlayer ? "Your ballot" : ballot.Reason ?? string.Empty, 13f,
                    new Color(UiTheme.Paper.r, UiTheme.Paper.g, UiTheme.Paper.b, .82f), 48f, 30f, w - 56f, 18f);
                if (!ballot.IsPlayer) why.fontStyle = FontStyles.Italic;
                float reasonHeight = EndScreenKit.Wrapped(why, w - 56f);
                float height = Mathf.Max(50f, 30f + reasonHeight + 8f);
                row.sizeDelta = new Vector2(w, height);
                EndScreenKit.Frame(row, forWinner ? PackArt.SeasonJuryRowWinner : ballot.IsPlayer ? PackArt.SeasonJuryRowNeutral : PackArt.SeasonJuryRowRunnerUp,
                    10f, UiTheme.SurfaceRaised, forWinner ? UiTheme.Gold : (Color?)null, 8);
                y += height + rowGap;
            }
            return y + pad - rowGap;
        }

        /// <summary>
        /// The season week by week, from the ledger's power rows - one a week, kept whole where the
        /// event log keeps only its last lines: who held the house, whom they put up, the veto and
        /// what became of it, and who went home by what count. The final week is its own: the three
        /// parts, and the last Head of Household's choice. A week the ledger has no row for (a save
        /// from before it kept them) says so rather than guessing.
        /// </summary>
        private float Timeline(RectTransform column, EpisodeState state, float width)
        {
            const float pad = 16f, rowGap = 6f, node = 30f;
            float y = pad + EndScreenKit.Heading(column, "Season timeline", "A look back at each week.", "calendar", pad, pad, width - pad * 2f);
            float w = width - pad * 2f;
            var rows = state.ledger?.power ?? new List<PowerRow>();
            string Name(string id) => id == null ? "—" : HudPrimitives.WithYou(state.Find(id)?.name ?? id, id == state.playerId);
            for (int week = 1; week <= state.week; week++)
            {
                var power = rows.FirstOrDefault(p => p.week == week);
                bool final = power != null && power.tally.Count == 0 && power.evicteeId != null && power.vetoHolderId == null;
                string first, second, gone = null;
                bool yourPower = false, youUp = false;
                if (power == null)
                {
                    var nominees = state.contestants.Where(c => c.nominationWeeks != null && c.nominationWeeks.Contains(week)).Select(c => c.name).ToList();
                    if (nominees.Count == 0 && week == state.week && state.phase != EpisodePhase.Finished) break;
                    first = "Not recorded in the season's ledger.";
                    second = nominees.Count > 0 ? "Noms: " + string.Join(", ", nominees) : null;
                }
                else if (final)
                {
                    var parts = new List<string>();
                    if (state.finalPart1WinnerId != null) parts.Add("Part 1: " + Name(state.finalPart1WinnerId));
                    if (state.finalPart2WinnerId != null) parts.Add("Part 2: " + Name(state.finalPart2WinnerId));
                    first = "Final HoH: " + Name(power.hohId) + (parts.Count > 0 ? " · " + string.Join(" · ", parts) : "");
                    second = Name(power.hohId) + " chose between " + string.Join(" and ", power.nominees.Select(Name)) + ".";
                    gone = power.evicteeId;
                    yourPower = power.hohId == state.playerId;
                    youUp = power.nominees.Contains(state.playerId);
                }
                else
                {
                    first = "HoH: " + Name(power.hohId);
                    var up = power.nominees.ToList();
                    if (power.savedId != null && !up.Contains(power.savedId)) up.Insert(0, power.savedId);
                    string veto = power.vetoHolderId == null ? "Veto: —"
                        : "Veto: " + Name(power.vetoHolderId) + (power.vetoUsed
                            ? (power.savedId != null ? ", used on " + Name(power.savedId) : ", used") + (power.replacementId != null ? "; " + Name(power.replacementId) + " up" : "")
                            : ", not used");
                    second = "Noms: " + (up.Count > 0 ? string.Join(", ", up.Select(Name)) : "—") + " · " + veto;
                    gone = power.evicteeId;
                    yourPower = power.hohId == state.playerId || power.vetoHolderId == state.playerId;
                    youUp = power.nominees.Contains(state.playerId);
                }
                var row = EndScreenKit.Box("Week " + week, column, pad, y, w, 50f);
                string mark = yourPower ? PackArt.SeasonNodePower : youUp ? PackArt.SeasonNodeEviction : PackArt.SeasonNodeNormal;
                var dot = EndScreenKit.Picture("Node", row, mark, null, UiTheme.Accent, new Vector2(10f + node * .5f, -25f), node);
                if (dot == null)
                {
                    var disc = HudPrimitives.Disc("Node", row, yourPower ? UiTheme.Gold : youUp ? UiTheme.Danger : UiTheme.Heading);
                    disc.anchorMin = disc.anchorMax = new Vector2(0f, 1f);
                    disc.pivot = new Vector2(.5f, .5f);
                    disc.sizeDelta = new Vector2(node, node);
                    disc.anchoredPosition = new Vector2(10f + node * .5f, -25f);
                }
                EndScreenKit.Text("Number", row, week.ToString(), 14f, Color.white, 10f, 16f, node, 20f, TextAlignmentOptions.Center, UiTheme.Weight.SemiBold);
                float textX = 10f + node + 12f;
                float goneWidth = gone != null ? Mathf.Min(170f, w * .34f) : 0f;
                var top = EndScreenKit.Text("Power", row, first, 14f, yourPower ? UiTheme.Gold : UiTheme.Paper, textX, 8f,
                    w - textX - goneWidth - 10f, 20f, TextAlignmentOptions.Left, UiTheme.Weight.Medium);
                float topHeight = EndScreenKit.Wrapped(top, w - textX - goneWidth - 10f);
                if (gone != null)
                {
                    var out_ = EndScreenKit.Text("Evicted", row, "Evicted: " + Name(gone) + Count(power), 13f, UiTheme.Danger,
                        w - goneWidth - 8f, 8f, goneWidth, 20f, TextAlignmentOptions.Right);
                    // A long name wraps the count onto a second line; the detail starts under both.
                    topHeight = Mathf.Max(topHeight, EndScreenKit.Wrapped(out_, goneWidth));
                }
                float height = 8f + topHeight + 4f;
                if (!string.IsNullOrEmpty(second))
                {
                    var detail = EndScreenKit.Text("Detail", row, second, 13f, UiTheme.Muted, textX, height, w - textX - 10f, 18f);
                    height += EndScreenKit.Wrapped(detail, w - textX - 10f);
                }
                height = Mathf.Max(50f, height + 8f);
                row.sizeDelta = new Vector2(w, height);
                EndScreenKit.Frame(row, yourPower ? PackArt.SeasonWeekRowPower : youUp ? PackArt.SeasonWeekRowEviction : PackArt.SeasonWeekRow,
                    10f, UiTheme.SurfaceRaised, null, 8);
                y += height + rowGap;
            }
            return y + pad - rowGap;
        }

        /// <summary>The vote that sent a week's evictee home, as counts: "(5–2)". Nothing for a week with no count.</summary>
        private static string Count(PowerRow power)
        {
            if (power == null || power.tally == null || power.tally.Count < 2) return string.Empty;
            var counts = power.tally.OrderByDescending(n => n).ToList();
            return " (" + string.Join("–", counts) + ")";
        }

        // ---------------------------------------------------------------- the career

        /// <summary>
        /// The player's record across seasons, under this one, in a strip: the five numbers with the
        /// captions they have always had, and the career's own line. Only when there is a career.
        /// </summary>
        private float CareerStrip(RectTransform board, CareerSummary career, float y, float inner)
        {
            const float height = 104f, pad = 18f;
            var strip = EndScreenKit.Box(CareerStripName, board, 0f, y, inner, height);
            EndScreenKit.Frame(strip, PackArt.SeasonCareerStrip, 16f, UiTheme.Surface);
            bool wide = inner >= 1150f;
            float titleWidth = wide ? 230f : 0f;
            if (wide)
            {
                EndScreenKit.Picture("Career mark", strip, null, "star", UiTheme.Heading, new Vector2(pad + 14f, -(height * .5f - 12f)), 26f);
                EndScreenKit.Text("Career title", strip, "YOUR CAREER", 18f, UiTheme.Paper, pad + 36f, height * .5f - 24f, titleWidth - 40f, 24f,
                    TextAlignmentOptions.Left, UiTheme.Weight.SemiBold).characterSpacing = 2f;
                var careerLine = EndScreenKit.Text("Career line", strip, "Every season you have finished.", 13f, UiTheme.Muted, pad + 36f, height * .5f + 2f, titleWidth - 40f, 20f);
                EndScreenKit.Wrapped(careerLine, titleWidth - 40f, 2);
            }
            float noteWidth = wide ? Mathf.Min(330f, inner * .24f) : 0f;
            float cellsX = pad + titleWidth, cellsWidth = inner - cellsX - noteWidth - pad * (wide ? 2f : 1f);
            var cells = new[]
            {
                ("SEASONS", career.Seasons.ToString(), UiTheme.Paper, false),
                ("WINS", career.Wins.ToString(), UiTheme.Gold, false),
                ("MEDIAN FINISH", CareerSummary.PlaceWord(career.MedianPlacement), UiTheme.Accent, false),
                ("BEST FINISH", CareerSummary.PlaceWord(career.BestPlacement), UiTheme.Positive, true),
                ("COMP WINS", (career.HohWins + career.VetoWins).ToString(), UiTheme.Positive, false),
            };
            float cellGap = 10f, cellWidth = (cellsWidth - cellGap * (cells.Length - 1)) / cells.Length;
            for (int i = 0; i < cells.Length; i++)
            {
                var (caption, value, tint, best) = cells[i];
                var cell = EndScreenKit.Box(caption, strip, cellsX + i * (cellWidth + cellGap), 14f, cellWidth, height - 28f);
                EndScreenKit.Frame(cell, best ? PackArt.SeasonCareerBest : PackArt.SeasonCareerCell, 10f, UiTheme.SurfaceRaised, null, 8);
                var number = EndScreenKit.Text("Value", cell, value, 26f, tint, 6f, 8f, cellWidth - 12f, 34f, TextAlignmentOptions.Center, UiTheme.Weight.SemiBold);
                number.enableAutoSizing = true; number.fontSizeMax = 26f; number.fontSizeMin = 14f;
                var label = EndScreenKit.Text("Caption", cell, caption, 12f, UiTheme.Muted, 6f, 44f, cellWidth - 12f, 18f, TextAlignmentOptions.Center);
                label.enableAutoSizing = true; label.fontSizeMax = 12f; label.fontSizeMin = 9f;
            }
            if (wide)
            {
                var note = EndScreenKit.Text("Career note", strip, career.Note(), 13f, UiTheme.Muted, inner - noteWidth - pad, 20f, noteWidth, 20f);
                EndScreenKit.Wrapped(note, noteWidth, 4);
                return height;
            }
            var under = EndScreenKit.Text("Career note", strip, career.Note(), 13f, UiTheme.Muted, pad, height, inner - pad * 2f, 20f);
            float extra = EndScreenKit.Wrapped(under, inner - pad * 2f, 3) + 10f;
            strip.sizeDelta = new Vector2(inner, height + extra);
            return height + extra;
        }
    }
}
