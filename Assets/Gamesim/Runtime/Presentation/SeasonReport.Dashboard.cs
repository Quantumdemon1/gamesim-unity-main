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
    /// The season at a glance (the owner's Season Complete mockup, Pack 7): the winner, large, with
    /// the final jury vote under them - the runner-up, the verdict, a card for every ballot and the
    /// count as a bar (MOCKUP-PASS M6) - the five Game Sense cards, then three columns - the final
    /// standings, how the jury voted, and the season week by week - and the career under them.
    ///
    /// <para>Nothing here is invented. The quote under a finalist is the first sentence of their own
    /// final speech (the runner-up's next one where the first repeats the winner's), or nothing; the
    /// count is the jury's ballots; a card's line is that face's own strongest row in the notebook;
    /// the weeks are the ledger's power rows, one a week, the final week's three parts and the last
    /// Head of Household's choice included. The mockup's taglines, its "3 — 2 — 1" and its quotes
    /// had no source, and are not here.</para>
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

        /// <summary>
        /// The muted line under the career's COMP WINS caption, and its label's name: what the career
        /// counts, which is not what the season's own COMP WINS counts (<see cref="CareerStrip"/>).
        /// </summary>
        public const string CareerCompWinsNote = "HoH and veto", CareerCaptionNoteName = "Caption note";

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

        /// <summary>The parts of the hero, by name, for a test and a screen reader (MOCKUP-PASS M6).</summary>
        public const string FinalJuryVoteName = "Final jury vote", JurorStripName = "Juror strip", TallyName = "Tally",
            HeroStatsName = "Hero stats", JurorCardName = "Juror card";

        /// <summary>
        /// The hero's four counts. Worded apart from the report's pinned HOH WINS and COMP WINS,
        /// which tests count exactly: these are the same numbers said the way the mockup says them.
        /// </summary>
        public const string CompetitionWinsWords = "Competition wins", HohWinsWords = "HoH wins", VetoWinsWords = "Veto wins",
            NominationsSurvivedWords = "Nominations survived";

        /// <summary>Past this many ballots the juror strip wraps to two rows.</summary>
        private const int StripRowLimit = 8;

        /// <summary>The winner's portrait, about as the mockup draws it, and the lit well around it.</summary>
        private static readonly Vector2 WinnerFace = new Vector2(230f, 270f);
        private const float WinnerWell = 10f;

        /// <summary>The name's gold, lit from above as the title's blue is.</summary>
        private static readonly Color LightGold = new Color(1f, .9f, .58f);

        /// <summary>
        /// The winner, large, and under them the final jury vote (MOCKUP-PASS M6): the winner's face
        /// in a lit well, the season's number and the crown before 'WINNER', the name on the pack's
        /// gold plate, their traits, the first line of their final speech, their share of the jury
        /// and their four counts; then the runner-up beside the verdict - the crown line and the
        /// count word for word - with a card for every ballot and the count as a bar. A season still
        /// being played has no winner, and says so.
        ///
        /// <para>Everything the runner-up's card and the verdict said before is here, in the same
        /// words and inside the hero, so the lines the finale's exits and the whole-season walk read
        /// are where they were.</para>
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
            float winnerHeight = WinnerCard(hero, state, winner, portrait, inner, forWinner);
            float voteHeight = FinalJuryVote(hero, state, winner, runnerUp, portrait, winnerHeight + Gap, inner, ballots, forWinner, forRunnerUp);
            float total = winnerHeight + Gap + voteHeight;
            hero.sizeDelta = new Vector2(inner, total);
            return total;
        }

        /// <summary>
        /// The winner's card: their face in a well lit gold from below, a few gold sparkles on its
        /// edge, and beside it the season's number, the crown and 'WINNER' (its own label, as it
        /// always was), the name large on the gold plate, a chip for each trait and, for a player who
        /// won, one for the final argument they made; the first line of their final speech and their
        /// share of the jury; and their four counts in a column of their own, or under it all on a
        /// narrow card. Returns the card's height.
        /// </summary>
        private float WinnerCard(RectTransform hero, EpisodeState state, ContestantState winner, Func<string, Texture> portrait,
            float width, int votes)
        {
            var card = EndScreenKit.Box("WINNER", hero, 0f, 0f, width, 10f);
            const float pad = 26f, statsWidth = 240f, statsGap = 24f, narrowestText = 380f;
            var well = new Vector2(WinnerFace.x + WinnerWell * 2f, WinnerFace.y + WinnerWell * 2f);

            // The winner lit in gold: this is where the season is won.
            var gold = UiTheme.Pack(PackArt.GlowGold);
            if (gold != null)
            {
                var light = new GameObject("Winner glow", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                light.rectTransform.SetParent(card, false);
                float side = well.y + 80f;
                EndScreenKit.Place(light.rectTransform, pad + well.x * .5f - side * .5f, pad + well.y * .5f - side * .5f, side, side);
                light.sprite = gold; light.color = new Color(1f, 1f, 1f, .45f); light.preserveAspect = true; light.raycastTarget = false;
            }
            var ground = HudPrimitives.Fill("Portrait well", card, new Color(.07f, .06f, .04f, 1f), 12);
            EndScreenKit.Place(ground, pad, pad, well.x, well.y);
            ground.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            EndScreenKit.Ramp("Gold light", ground, new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .7f));
            var face = HudPrimitives.RectPortrait(ground, "Winner portrait", portrait(winner.id), winner, WinnerFace, 10);
            EndScreenKit.Place(face, WinnerWell, WinnerWell, WinnerFace.x, WinnerFace.y);
            UiTheme.AddBorder(ground, 12, new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .85f));
            Sparkles(card, state, pad, pad, well.x, well.y);

            float textX = pad + well.x + 28f;
            bool statsBeside = width - textX - pad - statsWidth - statsGap >= narrowestText;
            float textWidth = Mathf.Max(160f, statsBeside ? width - textX - pad - statsWidth - statsGap : width - textX - pad);

            // The season's number and the crown before 'WINNER', which is a label of its own.
            float y = 30f, x = textX;
            var crown = EndScreenKit.Picture("Crown mark", card, PackArt.SeasonWinnerCrown, "crown", UiTheme.Gold, new Vector2(x + 14f, -(y + 10f)), 28f);
            if (crown != null) x += 38f;
            if (shownSeason.HasValue)
            {
                var season = EndScreenKit.Text("Season", card, "SEASON " + shownSeason.Value, 15f, new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .8f),
                    x, y, 150f, 20f, TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
                season.characterSpacing = 3f;
                float used = Mathf.Ceil(season.GetPreferredValues(season.text).x) + 4f;
                season.rectTransform.sizeDelta = new Vector2(used, 20f);
                x += used + 14f;
            }
            var badge = EndScreenKit.Text("Badge", card, "WINNER", 15f, UiTheme.Gold, x, y, Mathf.Max(80f, textX + textWidth - x), 20f,
                TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
            badge.characterSpacing = 3f;
            y += 30f;

            // The name, large and in gold on the pack's plate; shrunk to fit rather than wrapped.
            const float plateHeight = 78f;
            var plate = EndScreenKit.Box("Nameplate", card, textX, y, textWidth, plateHeight);
            EndScreenKit.Frame(plate, PackArt.SeasonWinnerNameplate, 16f, new Color(.2f, .17f, .06f, .95f), UiTheme.Gold);
            var name = EndScreenKit.Text("Name", plate, HudPrimitives.WithYou(winner.name, winner.isPlayer).ToUpperInvariant(), 50f, Color.white,
                18f, 6f, textWidth - 36f, 66f, TextAlignmentOptions.Left, UiTheme.Weight.Bold);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.enableAutoSizing = true; name.fontSizeMax = 50f; name.fontSizeMin = 28f;
            name.enableVertexGradient = true;
            name.colorGradient = new VertexGradient(LightGold, LightGold, UiTheme.Gold, UiTheme.Gold);
            y += plateHeight + 12f;

            y = TraitChips(card, state, winner, textX, y, textWidth);

            string SpeechOf(string id) => state.finalSpeeches?.FirstOrDefault(s => s.speakerId == id)?.text;
            string quote = EndScreenKit.Excerpt(SpeechOf(winner.id), 110);
            if (quote != null)
            {
                var said = EndScreenKit.Text("Quote", card, "“" + quote + "”", 15f, new Color(UiTheme.Paper.r, UiTheme.Paper.g, UiTheme.Paper.b, .86f),
                    textX, y, textWidth, 20f);
                said.fontStyle = FontStyles.Italic;
                y += EndScreenKit.Wrapped(said, textWidth, 2) + 2f;
                EndScreenKit.Text("Attribution", card, "From the final speech", 12f, UiTheme.Muted, textX, y, textWidth, 18f);
                y += 22f;
            }
            EndScreenKit.Text("Votes", card, votes + (votes == 1 ? " jury vote" : " jury votes"), 16f, UiTheme.Gold,
                textX, y + 4f, textWidth, 22f, TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
            y += 30f;

            float bottom = Mathf.Max(pad + well.y, y);
            if (statsBeside) bottom = Mathf.Max(bottom, HeroStats(card, state, winner, width - pad - statsWidth, 34f, statsWidth, 1));
            else bottom = HeroStats(card, state, winner, pad, bottom + 16f, width - pad * 2f, 2);
            float height = bottom + pad;
            card.sizeDelta = new Vector2(width, height);
            EndScreenKit.Frame(card, PackArt.SeasonWinnerHero, 18f, UiTheme.SurfaceRaised, UiTheme.Gold);
            return height;
        }

        /// <summary>
        /// A chip for each of the winner's traits, as the house knows them, and a gold one for the
        /// final argument a player who won made; wrapped under each other when they run out of room.
        /// Never padded to a count the mockup draws. Returns where the line under them starts.
        /// </summary>
        private static float TraitChips(RectTransform card, EpisodeState state, ContestantState who, float x, float y, float width)
        {
            var words = (who.traits ?? new List<string>()).Where(trait => !string.IsNullOrWhiteSpace(trait))
                .Select(trait => (word: trait, filled: false)).ToList();
            string argued = who.isPlayer && state.finalArgument != null ? FinalArgument.Label(state.finalArgument.theme) : null;
            if (argued != null) words.Add((argued, true));
            if (words.Count == 0) return y;
            const float height = 26f, gap = 8f;
            float at = x;
            foreach (var (word, filled) in words)
            {
                var pill = EndScreenKit.Pill(card, word, filled ? UiTheme.Gold : UiTheme.Heading, at, y, 120f, height, filled);
                var label = pill.GetComponentInChildren<TMP_Text>();
                float chip = Mathf.Min(width, (label != null ? Mathf.Ceil(label.GetPreferredValues(label.text).x) : 80f) + 20f);
                if (at > x && at + chip > x + width)
                {
                    at = x;
                    y += height + 6f;
                }
                EndScreenKit.Place(pill, at, y, chip, height);
                at += chip + gap;
            }
            return y + height + 12f;
        }

        /// <summary>
        /// The winner's four counts, a row each with its glyph: every competition won, the final
        /// Head of Household's parts included (<see cref="FinalistRead.Wins"/>); Heads of Household;
        /// vetoes; and nominations survived, which for the winner is every nomination. In one column
        /// beside the card's words, or two under them. Returns where the column ends.
        /// </summary>
        private static float HeroStats(RectTransform card, EpisodeState state, ContestantState winner, float x, float y, float width, int columns)
        {
            var rows = new (string words, int value, string icon, string fallback, Color tint)[]
            {
                (CompetitionWinsWords, FinalistRead.Wins(state, winner), PackArt.SeasonIconCompetitions, "trophy", UiTheme.Positive),
                (HohWinsWords, winner.hohWins, PackArt.KitIconCrown, "crown", UiTheme.Accent),
                (VetoWinsWords, winner.vetoWins, null, "veto-token", UiTheme.Gold),
                (NominationsSurvivedWords, winner.timesNominated, PackArt.KitIconShield, "target", UiTheme.Warning),
            };
            const float rowHeight = 44f, rowGap = 6f, columnGap = 12f, glyphSide = 24f;
            int perColumn = (rows.Length + columns - 1) / columns;
            float columnWidth = (width - columnGap * (columns - 1)) / columns;
            var stats = EndScreenKit.Box(HeroStatsName, card, x, y, width, perColumn * rowHeight + (perColumn - 1) * rowGap);
            for (int i = 0; i < rows.Length; i++)
            {
                var (words, value, icon, fallback, tint) = rows[i];
                int column = columns == 1 ? 0 : i % columns, line = columns == 1 ? i : i / columns;
                var row = EndScreenKit.Box(words, stats, column * (columnWidth + columnGap), line * (rowHeight + rowGap), columnWidth, rowHeight);
                EndScreenKit.Frame(row, PackArt.SeasonCareerCell, 10f, UiTheme.Surface, null, 8);
                // The pack's own icon keeps its colours; Kit 6's white glyphs and the generated ones take the row's tint.
                var glyph = EndScreenKit.Picture("Glyph", row, icon, fallback, tint, new Vector2(12f + glyphSide * .5f, -rowHeight * .5f), glyphSide);
                if (glyph != null && icon != null && icon.StartsWith("Kit6_") && glyph.sprite == UiTheme.Pack(icon)) glyph.color = tint;
                float wordsX = glyph != null ? 12f + glyphSide + 10f : 12f;
                var label = EndScreenKit.Text("Words", row, words, 13f, UiTheme.Paper, wordsX, (rowHeight - 18f) * .5f, columnWidth - wordsX - 58f, 18f);
                label.enableAutoSizing = true; label.fontSizeMax = 13f; label.fontSizeMin = 10f;
                var number = EndScreenKit.Text("Value", row, value.ToString(), 24f, tint, columnWidth - 56f, (rowHeight - 32f) * .5f, 44f, 32f,
                    TextAlignmentOptions.Right, UiTheme.Weight.SemiBold);
                number.enableAutoSizing = true; number.fontSizeMax = 24f; number.fontSizeMin = 16f;
            }
            return y + stats.sizeDelta.y;
        }

        /// <summary>
        /// The final jury vote: the runner-up's card - their face, their place, the first line of
        /// their final speech that is not the winner's, their share of the jury - beside the
        /// verdict, or over it on a narrow card. Returns the panel's height.
        /// </summary>
        private float FinalJuryVote(RectTransform hero, EpisodeState state, ContestantState winner, ContestantState runnerUp,
            Func<string, Texture> portrait, float y, float width, List<JuryBallot> ballots, int forWinner, int forRunnerUp)
        {
            var panel = EndScreenKit.Box(FinalJuryVoteName, hero, 0f, y, width, 10f);
            const float pad = 18f;
            bool beside = width >= 980f;
            float runnerWidth = runnerUp == null ? 0f : beside ? Mathf.Clamp(width * .34f, 320f, 470f) : width - pad * 2f;
            float runnerHeight = runnerUp == null ? 0f : RunnerUpCard(panel, state, runnerUp, portrait, pad, pad, runnerWidth, forRunnerUp);
            float verdictX = runnerUp != null && beside ? pad + runnerWidth + 22f : pad;
            float verdictY = runnerUp != null && !beside ? pad + runnerHeight + 16f : pad;
            float verdictHeight = Verdict(panel, state, winner, runnerUp, portrait, verdictX, verdictY, width - verdictX - pad,
                ballots, forWinner, forRunnerUp);
            float height = Mathf.Max(runnerUp != null ? pad + runnerHeight : 0f, verdictY + verdictHeight) + pad;
            panel.sizeDelta = new Vector2(width, height);
            EndScreenKit.Frame(panel, PackArt.SeasonJurySummary, 14f, UiTheme.Surface);
            return height;
        }

        /// <summary>The runner-up's card, in steel: their face, 'RUNNER-UP', the name on the pack's plate, their line and their votes.</summary>
        private static float RunnerUpCard(RectTransform parent, EpisodeState state, ContestantState who, Func<string, Texture> portrait,
            float x, float y, float width, int votes)
        {
            var card = EndScreenKit.Box("RUNNER-UP", parent, x, y, width, 10f);
            const float pad = 16f;
            var faceSize = new Vector2(96f, 116f);
            var face = HudPrimitives.RectPortrait(card, "Runner-up portrait", portrait(who.id), who, faceSize, 10);
            EndScreenKit.Place(face, pad, pad, faceSize.x, faceSize.y);
            UiTheme.AddBorder(face, 10, new Color(Steel.r, Steel.g, Steel.b, .8f));
            EndScreenKit.Picture("Runner-up mark", card, PackArt.SeasonBadgeRunnerUp, "star", Steel, new Vector2(pad + faceSize.x - 4f, -(pad + 4f)), 28f);

            float textX = pad + faceSize.x + 16f, textWidth = Mathf.Max(80f, width - textX - pad);
            var badge = EndScreenKit.Text("Badge", card, "RUNNER-UP", 14f, Steel, textX, pad, textWidth, 20f, TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
            badge.characterSpacing = 3f;
            const float plateHeight = 44f;
            var plate = EndScreenKit.Box("Nameplate", card, textX, pad + 24f, textWidth, plateHeight);
            EndScreenKit.Frame(plate, PackArt.SeasonRunnerUpNameplate, 12f, UiTheme.SurfaceRaised, Steel);
            var name = EndScreenKit.Text("Name", plate, HudPrimitives.WithYou(who.name, who.isPlayer), 22f, UiTheme.Paper,
                12f, 6f, textWidth - 24f, 32f, TextAlignmentOptions.Left, UiTheme.Weight.Bold);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.enableAutoSizing = true; name.fontSizeMax = 22f; name.fontSizeMin = 14f;
            float at = pad + 24f + plateHeight + 10f;

            string SpeechOf(string id) => state.finalSpeeches?.FirstOrDefault(s => s.speakerId == id)?.text;
            // The runner-up's line never repeats the winner's: two speeches that open alike give the
            // runner-up their next sentence, or no quote.
            string quote = EndScreenKit.ExcerptBeside(SpeechOf(who.id), 80, SpeechOf(state.winnerId));
            if (quote != null)
            {
                var said = EndScreenKit.Text("Quote", card, "“" + quote + "”", 14f, new Color(UiTheme.Paper.r, UiTheme.Paper.g, UiTheme.Paper.b, .86f),
                    textX, at, textWidth, 20f);
                said.fontStyle = FontStyles.Italic;
                at += EndScreenKit.Wrapped(said, textWidth, 3) + 2f;
                EndScreenKit.Text("Attribution", card, "From the final speech", 12f, UiTheme.Muted, textX, at, textWidth, 18f);
                at += 22f;
            }
            EndScreenKit.Text("Votes", card, votes + (votes == 1 ? " jury vote" : " jury votes"), 15f, Steel,
                textX, at + 2f, textWidth, 22f, TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
            at += 26f;
            float height = Mathf.Max(pad + faceSize.y + pad, at + pad);
            card.sizeDelta = new Vector2(width, height);
            EndScreenKit.Frame(card, PackArt.SeasonRunnerUpHero, 16f, UiTheme.SurfaceRaised, Steel);
            return height;
        }

        /// <summary>
        /// The verdict: 'FINAL JURY VOTE', the crown line and the count in words - each word for
        /// word, as the finale's exits and the whole-season walk read them - then a card for every
        /// ballot and the count as a bar between the two finalists' numbers. Returns its height.
        /// </summary>
        private float Verdict(RectTransform panel, EpisodeState state, ContestantState winner, ContestantState runnerUp,
            Func<string, Texture> portrait, float x, float y, float width, List<JuryBallot> ballots, int forWinner, int forRunnerUp)
        {
            var verdict = EndScreenKit.Box("Verdict", panel, x, y, width, 10f);
            EndScreenKit.Text("Eyebrow", verdict, "FINAL JURY VOTE", 13f, UiTheme.Heading, 0f, 0f, width, 20f,
                TextAlignmentOptions.Left, UiTheme.Weight.SemiBold).characterSpacing = 3f;
            // The crown line, word for word: the finale's exits and the whole-season walk read it.
            var crown = EndScreenKit.Text("Crown", verdict, runnerUp != null
                    ? winner.name + " beat " + runnerUp.name + " in the jury vote."
                    : winner.name + " wins the season.", 18f, UiTheme.Paper, 0f, 24f, width, 26f,
                TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
            float at = 24f + EndScreenKit.Wrapped(crown, width) + 4f;
            foreach (string line in VerdictLines(winner, runnerUp, forWinner, forRunnerUp))
            {
                var words = EndScreenKit.Text("Count", verdict, line, 14f, UiTheme.Muted, 0f, at, width, 20f);
                at += EndScreenKit.Wrapped(words, width) + 2f;
            }
            if (ballots.Count > 0) at += 10f + JurorStrip(verdict, state, portrait, ballots, winner, 0f, at + 10f, width);
            if (runnerUp != null && forWinner + forRunnerUp > 0) at += 14f + Tally(verdict, winner, runnerUp, forWinner, forRunnerUp, 0f, at + 14f, width);
            verdict.sizeDelta = new Vector2(width, at);
            return at;
        }

        /// <summary>
        /// A card for every ballot, in the order they were cast: the juror's face, their first name
        /// or 'You', and 'VOTED' over the first name of the finalist they chose, in that finalist's
        /// gold or steel. Never the words 'voted for', which the jury column below counts, one a
        /// ballot. Two rows past <see cref="StripRowLimit"/> ballots, and narrower cards before more
        /// rows. Returns the strip's height.
        /// </summary>
        private static float JurorStrip(RectTransform parent, EpisodeState state, Func<string, Texture> portrait, List<JuryBallot> ballots,
            ContestantState winner, float x, float y, float width)
        {
            var strip = EndScreenKit.Box(JurorStripName, parent, x, y, width, 10f);
            const float gap = 8f, widest = 80f, narrowest = 60f;
            int count = ballots.Count;
            int perRow = count > StripRowLimit ? (count + 1) / 2 : count;
            float cardWidth = Mathf.Min(widest, (width - gap * (perRow - 1)) / perRow);
            if (cardWidth < narrowest)
            {
                cardWidth = narrowest;
                perRow = Mathf.Max(1, Mathf.FloorToInt((width + gap) / (narrowest + gap)));
            }
            float side = Mathf.Min(64f, cardWidth - 12f), cardHeight = side + 66f;
            int rows = (count + perRow - 1) / perRow;
            for (int i = 0; i < count; i++)
            {
                var ballot = ballots[i];
                bool forWinner = winner != null && ballot.FinalistId == winner.id;
                var tint = forWinner ? UiTheme.Gold : Steel;
                var card = EndScreenKit.Box(JurorCardName, strip, (i % perRow) * (cardWidth + gap), (i / perRow) * (cardHeight + gap), cardWidth, cardHeight);
                EndScreenKit.Frame(card, forWinner ? PackArt.SeasonJuryRowWinner : ballot.IsPlayer ? PackArt.SeasonJuryRowNeutral : PackArt.SeasonJuryRowRunnerUp,
                    10f, UiTheme.SurfaceRaised, forWinner ? UiTheme.Gold : (Color?)null, 8);
                var juror = ballot.JurorId == null ? null : state.Find(ballot.JurorId);
                var face = HudPrimitives.RectPortrait(card, "Juror portrait", ballot.JurorId == null ? null : portrait(ballot.JurorId), juror,
                    new Vector2(side, side), 8);
                EndScreenKit.Place(face, (cardWidth - side) * .5f, 8f, side, side);
                var name = EndScreenKit.Text("Juror name", card, ballot.IsPlayer ? "You" : FinalistRead.FirstName(ballot.Juror), 12f,
                    ballot.IsPlayer ? UiTheme.Accent : UiTheme.Paper, 4f, side + 12f, cardWidth - 8f, 16f, TextAlignmentOptions.Center, UiTheme.Weight.Medium);
                name.enableAutoSizing = true; name.fontSizeMax = 12f; name.fontSizeMin = 9f;
                var voted = EndScreenKit.Text("Voted", card, "VOTED", 9f, UiTheme.Muted, 4f, side + 30f, cardWidth - 8f, 12f,
                    TextAlignmentOptions.Center, UiTheme.Weight.SemiBold);
                voted.characterSpacing = 1.5f;
                var pick = EndScreenKit.Text("Their vote", card, FinalistRead.FirstName(ballot.Finalist), 12f, tint, 4f, side + 43f, cardWidth - 8f, 16f,
                    TextAlignmentOptions.Center, UiTheme.Weight.SemiBold);
                pick.enableAutoSizing = true; pick.fontSizeMax = 12f; pick.fontSizeMin = 9f;
            }
            float height = rows * cardHeight + (rows - 1) * gap;
            strip.sizeDelta = new Vector2(width, height);
            return height;
        }

        /// <summary>
        /// The count as a bar between the two finalists' numbers, each in a tile of its colour, and
        /// their full names under the ends. A tie draws equal halves. Returns the tally's height.
        /// </summary>
        private static float Tally(RectTransform parent, ContestantState winner, ContestantState runnerUp, int forWinner, int forRunnerUp,
            float x, float y, float width)
        {
            var tally = EndScreenKit.Box(TallyName, parent, x, y, width, 10f);
            const float tile = 48f, tileHeight = 40f, barHeight = 28f, gap = 10f;
            NumberTile(tally, "Winner tally", forWinner, UiTheme.Gold, 0f, 0f, tile, tileHeight);
            NumberTile(tally, "Runner-up tally", forRunnerUp, Steel, width - tile, 0f, tile, tileHeight);
            EndScreenKit.SplitBar(tally, tile + gap, (tileHeight - barHeight) * .5f, Mathf.Max(20f, width - (tile + gap) * 2f), barHeight,
                forWinner, forRunnerUp, UiTheme.Gold, Steel);
            float half = width * .5f - 6f;
            var first = EndScreenKit.Text("Winner name", tally, HudPrimitives.WithYou(winner.name, winner.isPlayer), 13f, UiTheme.Gold,
                0f, tileHeight + 6f, half, 18f, TextAlignmentOptions.Left, UiTheme.Weight.Medium);
            first.enableAutoSizing = true; first.fontSizeMax = 13f; first.fontSizeMin = 10f;
            var second = EndScreenKit.Text("Runner-up name", tally, HudPrimitives.WithYou(runnerUp.name, runnerUp.isPlayer), 13f, Steel,
                width - half, tileHeight + 6f, half, 18f, TextAlignmentOptions.Right, UiTheme.Weight.Medium);
            second.enableAutoSizing = true; second.fontSizeMax = 13f; second.fontSizeMin = 10f;
            float height = tileHeight + 6f + 18f;
            tally.sizeDelta = new Vector2(width, height);
            return height;
        }

        /// <summary>One end of the tally: the count in 26 pt, in a tile of the finalist's colour.</summary>
        private static void NumberTile(RectTransform parent, string name, int count, Color tint, float x, float y, float width, float height)
        {
            var tile = HudPrimitives.Fill(name, parent, new Color(tint.r, tint.g, tint.b, .18f), 8);
            EndScreenKit.Place(tile, x, y, width, height);
            UiTheme.AddBorder(tile, 8, new Color(tint.r, tint.g, tint.b, .8f));
            EndScreenKit.Text("Number", tile, count.ToString(), 26f, tint, 0f, (height - 34f) * .5f, width, 34f,
                TextAlignmentOptions.Center, UiTheme.Weight.SemiBold);
        }

        /// <summary>How many sparkles the winner's well carries.</summary>
        private const int SparkleCount = 7;

        /// <summary>
        /// A few gold sparkles on the edge of the winner's well, placed from the season's seed so a
        /// season's report draws the same ones every time it opens. They twinkle (<see cref="Twinkle"/>)
        /// only while reduced motion is off; with it on they hold still.
        /// </summary>
        private void Sparkles(RectTransform card, EpisodeState state, float x, float y, float width, float height)
        {
            var star = UiTheme.Icon("star");
            if (star == null) return;
            var random = new System.Random(unchecked((int)state.seed) ^ 0x5A17);
            for (int i = 0; i < SparkleCount; i++)
            {
                float side = 9f + (float)random.NextDouble() * 9f;
                float along = (float)random.NextDouble(), off = ((float)random.NextDouble() - .5f) * 14f;
                // Round the well's edge, a side each in turn, where the frame's gold is: never on the face's middle.
                Vector2 at;
                switch (i % 4)
                {
                    case 0: at = new Vector2(x + along * width, y + off); break;
                    case 1: at = new Vector2(x + width + off, y + along * height); break;
                    case 2: at = new Vector2(x + along * width, y + height + off); break;
                    default: at = new Vector2(x + off, y + along * height); break;
                }
                var image = new GameObject("Sparkle", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.rectTransform.SetParent(card, false);
                EndScreenKit.Place(image.rectTransform, at.x - side * .5f, at.y - side * .5f, side, side);
                image.sprite = star;
                image.preserveAspect = true;
                image.raycastTarget = false;
                float glow = .55f + (float)random.NextDouble() * .4f;
                image.color = new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, glow);
                sparkles.Add((image, (float)random.NextDouble() * Mathf.PI * 2f, 1.4f + (float)random.NextDouble() * 1.6f, glow));
            }
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
            foreach (var (who, place) in StandingsOrder(state))
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

        /// <summary>
        /// Everybody in the order they finished, with the place the standings print: the one
        /// placement every screen reads (<see cref="CareerLedger.Placement"/>), and 0 for anyone
        /// still in the house, who comes last.
        ///
        /// <para>Without the jury ledger (a save from before it, or a season built for a test) that
        /// placement falls back to a coarse count, and every juror shares one number: a house of
        /// eight printed "7" five times. The week each of them left, their power row, breaks the
        /// tie: the latest to leave finished highest, and the tied group takes the run of places its
        /// number stands for, between the rows around it and inside the house. Two who left in the
        /// same week, or two the ledger has no row for, share the lowest place of their part of the
        /// run, as the coarse count gives a tied jury, rather than an order nothing on the record
        /// gives. A group whose run does not fit there keeps the number it shares.</para>
        /// </summary>
        public static List<(ContestantState who, int place)> StandingsOrder(EpisodeState state)
        {
            int? Left(ContestantState who) => JuryHouseRead.LeftWeek(state, who.id);
            var order = state.contestants
                .Select(c => (who: c, place: c.status == ContestantStatus.Active ? 0 : CareerLedger.Placement(state, c)))
                .OrderBy(e => e.place == 0 ? int.MaxValue : e.place).ThenBy(e => PlacementRank(e.who.status))
                .ThenByDescending(e => Left(e.who) ?? int.MinValue).ThenBy(e => e.who.name, StringComparer.CurrentCulture)
                .ToList();
            int above = 0, house = state.contestants.Count;
            for (int start = 0; start < order.Count;)
            {
                int place = order[start].place, end = start;
                while (end < order.Count && order[end].place == place) end++;
                int size = end - start;
                if (place > 0 && size > 1)
                {
                    // The run a coarse number stands for. A juror's number is the lowest seat the jury
                    // holds, so their run reaches back from it. A pre-jury evictee's is the highest
                    // seat below the jury, and the row before it is the jury's last, so their run
                    // starts at the number and goes down the table. Either run holds the number
                    // itself, because the row before printed a smaller one.
                    int first = Math.Max(above + 1, place - size + 1), last = first + size - 1;
                    // A save whose jury ledger began part way through the season mixes the ledger's
                    // places with the fallback's, and there a run can reach the next row's number or
                    // pass the foot of the house. That group keeps the number it shares, as it always
                    // printed, rather than a place printed twice or a place the house never had.
                    int next = end < order.Count && order[end].place > 0 ? order[end].place : house + 1;
                    if (last <= house && last < next)
                    {
                        for (int i = start; i < end;)
                        {
                            int same = i;
                            while (same < end && Left(order[same].who) == Left(order[i].who)) same++;
                            for (int n = i; n < same; n++) order[n] = (order[n].who, first + (same - start) - 1);
                            i = same;
                        }
                    }
                }
                if (place > 0) above = order[end - 1].place;
                start = end;
            }
            return order;
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
        ///
        /// <para>The career's COMP WINS counts Heads of Household and vetoes, the two a
        /// <see cref="CareerSeason"/> stores, while the season's own COMP WINS below counts the final
        /// Head of Household's first two parts as well (<see cref="FinalistRead.Wins"/>). A one-season
        /// career would show two different numbers under one caption, so the career's cell says
        /// what it counts in a muted line under its caption, which stays word for word.</para>
        /// </summary>
        private float CareerStrip(RectTransform board, CareerSummary career, float y, float inner)
        {
            const float height = 112f, pad = 18f;
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
            var cells = new (string caption, string value, Color tint, bool best, string note)[]
            {
                ("SEASONS", career.Seasons.ToString(), UiTheme.Paper, false, null),
                ("WINS", career.Wins.ToString(), UiTheme.Gold, false, null),
                ("MEDIAN FINISH", CareerSummary.PlaceWord(career.MedianPlacement), UiTheme.Accent, false, null),
                ("BEST FINISH", CareerSummary.PlaceWord(career.BestPlacement), UiTheme.Positive, true, null),
                ("COMP WINS", (career.HohWins + career.VetoWins).ToString(), UiTheme.Positive, false, CareerCompWinsNote),
            };
            float cellGap = 10f, cellWidth = (cellsWidth - cellGap * (cells.Length - 1)) / cells.Length;
            for (int i = 0; i < cells.Length; i++)
            {
                var (caption, value, tint, best, note) = cells[i];
                var cell = EndScreenKit.Box(caption, strip, cellsX + i * (cellWidth + cellGap), 14f, cellWidth, height - 28f);
                EndScreenKit.Frame(cell, best ? PackArt.SeasonCareerBest : PackArt.SeasonCareerCell, 10f, UiTheme.SurfaceRaised, null, 8);
                var number = EndScreenKit.Text("Value", cell, value, 26f, tint, 6f, 8f, cellWidth - 12f, 34f, TextAlignmentOptions.Center, UiTheme.Weight.SemiBold);
                number.enableAutoSizing = true; number.fontSizeMax = 26f; number.fontSizeMin = 14f;
                var label = EndScreenKit.Text("Caption", cell, caption, 12f, UiTheme.Muted, 6f, 44f, cellWidth - 12f, 18f, TextAlignmentOptions.Center);
                label.enableAutoSizing = true; label.fontSizeMax = 12f; label.fontSizeMin = 9f;
                if (note == null) continue;
                // A label of its own under the pinned caption, never words added to it.
                var counts = EndScreenKit.Text(CareerCaptionNoteName, cell, note, 11f, UiTheme.Muted, 6f, 62f, cellWidth - 12f, 16f, TextAlignmentOptions.Center);
                counts.enableAutoSizing = true; counts.fontSizeMax = 11f; counts.fontSizeMin = 9f;
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
