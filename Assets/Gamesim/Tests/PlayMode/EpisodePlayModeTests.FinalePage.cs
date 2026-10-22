using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The finale page on the strategy stage (UI-UX-PASS-PLAN F0, the owner's mockup 92). The
    /// finished panel was a column of sentences - the winner, the Game Sense line, a line a ballot -
    /// and six rows that scrolled (the owner's screenshot 93). These hold that the page draws the
    /// three cards by name with the finalists' names on them, a face a ballot ringed in the colour
    /// of the finalist it was for with that finalist's first name under it, the count in tiles and a
    /// bar split by it (a tie as equal halves), the six highlights at the state's own counts, and the
    /// six ways on once each, with their lines; that it holds without a scroll at both text sizes on
    /// the 16:9 frame, with a jury of fourteen too; that a press on a juror's face shows the reason
    /// they gave, word for word, and a second press folds it, committing nothing; that each face is
    /// in the keyboard ring once and Enter on "Season report" still opens the report; and that the
    /// jury's questions open as the step with a way back.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>A part of the finale page now on screen, under the panel: the last active rect of that name.</summary>
        private RectTransform FinalePart(string name)
        {
            var panel = ActiveRect(ModalRoot);
            Assert.That(panel, Is.Not.Null, "The finale's panel is open.");
            return panel.GetComponentsInChildren<RectTransform>().LastOrDefault(rect => rect.name == name && rect.gameObject.activeInHierarchy);
        }

        /// <summary>A juror's face on the jury card: the control captioned with their name, and a player could press it.</summary>
        private Button JurorFace(ContestantState juror) => FinaleControl(EpisodeDirector.JurorFaceCaption(juror));

        /// <summary>The words of the reason line now on the jury card's foot, or null while it shows the hint.</summary>
        private string FinaleReasonLine()
        {
            var line = FinalePart(EpisodeHud.FinaleReasonLineName);
            return line != null ? line.GetComponent<TMP_Text>().text : null;
        }

        /// <summary>A finished season built rather than played, installed as the director's own and loaded from its save.</summary>
        private IEnumerator InstallBuiltFinale(EpisodeState state)
        {
            HoldTheHouseForTheFixture();
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            yield return null;
        }

        /// <summary>The EndScreens tie fixture: a finished season of eight with half the ballots each way, the house's tie rule deciding it.</summary>
        private static EpisodeState TiedFinale()
        {
            var state = Finished();
            var champion = state.Find(state.winnerId);
            var runnerUp = state.Find(state.runnerUpId);
            var ballots = state.votes.Where(v => v.targetId == champion.id || v.targetId == runnerUp.id).ToList();
            for (int i = 0; i < ballots.Count; i++) ballots[i].targetId = i % 2 == 0 ? champion.id : runnerUp.id;
            if (ballots.Count % 2 == 1) state.votes.Remove(ballots.Last());
            return state;
        }

        /// <summary>
        /// The EndScreens fixture with the player in the final two: the runner-up's place and the
        /// ballots for them handed to the player, the player's own ballot withdrawn, and the former
        /// runner-up on the jury with a ballot of their own for the player.
        /// </summary>
        private static EpisodeState PlayerRunnerUp()
        {
            var state = Finished();
            var you = state.Find(state.playerId);
            var runnerUp = state.Find(state.runnerUpId);
            runnerUp.status = ContestantStatus.Jury;
            you.status = ContestantStatus.RunnerUp;
            state.runnerUpId = you.id;
            state.votes.RemoveAll(vote => vote.voterId == you.id);
            foreach (var vote in state.votes.Where(vote => vote.targetId == runnerUp.id)) vote.targetId = you.id;
            state.votes.Add(new VoteState { voterId = runnerUp.id, targetId = you.id, reason = "You never lied to me, and I know what that cost you." });
            return state;
        }

        /// <summary>The number in one of the page's tally tiles.</summary>
        private static string TileNumber(RectTransform card, string name) =>
            card.GetComponentsInChildren<RectTransform>().Last(rect => rect.name == name)
                .GetComponentsInChildren<TMP_Text>().Single(label => label.name == "Number").text;

        /// <summary>
        /// Everything the closed page holds, at whichever text size it is open at: the stage, no
        /// scroll, the three cards by name in a row with the names on them, a face a ballot in cast
        /// order ringed and chipped in the finalist's colour and name, the tally, the highlights at
        /// the state's counts, the ways on once each with their lines, every label drawn in a box
        /// Inter draws in, and each face and caption in the keyboard ring once.
        ///
        /// <para>At the standard and the larger text that is one page. At the largest it is exactly
        /// <paramref name="largestPages"/> - the suite's literal for the frame, measured, never the
        /// renderer's arithmetic (the owner's contract, EpisodeHud.LargestPages.cs) - each holding
        /// without a scroll, the page's three parts - the cards' row, the highlights, the ways on -
        /// each whole on exactly one page and in that order across them, everything above held of the
        /// parts each page has, and a part a page has not nowhere on it. Ends on the first page.</para>
        /// </summary>
        private void AssertFinalePage(string where, int largestPages = 1)
        {
            Canvas.ForceUpdateCanvases();
            bool paged = largestPages > 1 && director.TextSize == TextSize.Largest;
            var pageOf = new Dictionary<string, int>();
            AssertLargestPages(where, largestPages, page =>
                AssertFinalePageParts(paged ? where + ", page " + page + " of " + largestPages : where, page, pageOf));
            for (int i = 0; i < FinalePageParts.Length; i++)
            {
                Assert.That(pageOf.ContainsKey(FinalePageParts[i]), Is.True, where + " has its '" + FinalePageParts[i] + "': it is on no page.");
                if (i > 0)
                    Assert.That(pageOf[FinalePageParts[i]], Is.GreaterThanOrEqualTo(pageOf[FinalePageParts[i - 1]]),
                        where + ": '" + FinalePageParts[i] + "' comes after '" + FinalePageParts[i - 1] + "' in the pages' order.");
            }
        }

        /// <summary>The finale page's three parts in reading order, by the names of the part that says each is there: the cards' row (its jury card), the highlights, the ways on.</summary>
        private static readonly string[] FinalePageParts = { EpisodeHud.FinaleJuryCardName, EpisodeHud.FinaleHighlightsName, EpisodeHud.FinaleWaysName };

        /// <summary>One page of the finale's: every assertion of <see cref="AssertFinalePage"/> on the parts it has; <paramref name="pageOf"/> records where each part is.</summary>
        private void AssertFinalePageParts(string where, int pageNumber, Dictionary<string, int> pageOf)
        {
            Canvas.ForceUpdateCanvases();
            var state = director.Snapshot;
            var winner = state.Find(state.winnerId);
            var runnerUp = state.Find(state.runnerUpId);
            var ballots = SeasonReport.JuryBallots(state);
            int forWinner = ballots.Count(b => b.FinalistId == winner.id), forRunnerUp = ballots.Count(b => b.FinalistId == runnerUp.id);
            var hud = director.GetComponentInChildren<EpisodeHud>();
            Assert.That(hud.CurrentActivityLayout, Is.EqualTo(EpisodeHud.ActivityLayout.Strategy), where + " takes the strategy stage.");
            var page = FinalePart(EpisodeHud.FinalePageName);
            Assert.That(page, Is.Not.Null, where + " is drawn as the page.");
            var content = ActiveRect("Episode content");
            var viewport = (RectTransform)content.parent;
            Assert.That(content.rect.height, Is.LessThanOrEqualTo(viewport.rect.height + .5f),
                where + " holds without a scroll: " + content.rect.height.ToString("0") + " in " + viewport.rect.height.ToString("0") + ".");
            var panel = ActiveRect("Episode panel");
            AssertInside(ScreenRect(panel), page, where + "'s page");
            void On(string part)
            {
                Assert.That(pageOf.ContainsKey(part), Is.False, where + ": '" + part + "' is on one page only.");
                pageOf[part] = pageNumber;
            }

            // The three cards by name, in a row, the finalists' names on them: all three or none.
            var winnerCard = FinalePart(EpisodeHud.FinaleWinnerCardName);
            var two = FinalePart(EpisodeHud.FinaleFinalTwoCardName);
            var jury = FinalePart(EpisodeHud.FinaleJuryCardName);
            Assert.That(winnerCard != null && two != null, Is.EqualTo(jury != null), where + ": the three cards stand in one row, on one page.");
            var castOrder = state.contestants.Where(c => ballots.Any(b => b.JurorId == c.id)).Select(EpisodeDirector.JurorFaceCaption).ToList();
            if (jury != null)
            {
                On(EpisodeHud.FinaleJuryCardName);
                foreach (var card in new[] { winnerCard, two, jury })
                {
                    Assert.That(card != null && card.IsChildOf(page), Is.True, where + " has its three cards on the page.");
                    AssertInside(ScreenRect(page), card, where + "'s '" + card.name + "'");
                }
                Assert.That(ScreenRect(winnerCard).xMax, Is.LessThanOrEqualTo(ScreenRect(two).xMin + .5f), where + ": WINNER stands left of FINAL TWO.");
                Assert.That(ScreenRect(two).xMax, Is.LessThanOrEqualTo(ScreenRect(jury).xMin + .5f), where + ": FINAL TWO stands left of JURY.");
                var winnerWords = LabelsUnder(winnerCard);
                Assert.That(winnerWords, Does.Contain("WINNER").And.Contain(HudPrimitives.WithYou(winner.name, winner.isPlayer)), where + ": the winner's card names them.");
                Assert.That(winnerWords, Does.Contain("Winner: " + winner.name + ". Runner-up: " + runnerUp.name + "."),
                    where + ": the sentence the whole-season walk reads is the winner card's foot line.");
                var sense = GameSense.Evaluate(state);
                Assert.That(winnerWords, Does.Contain("Game Sense " + sense.score + " · Competitions " + sense.competitions)
                    .And.Contain("Strategy " + sense.strategy + " · Social " + sense.social), where + ": the Game Sense numbers on two lines.");
                var twoWords = LabelsUnder(two);
                Assert.That(twoWords, Does.Contain(EpisodeHud.FinaleFinalTwoCardName).And.Contain(HudPrimitives.WithYou(winner.name, winner.isPlayer))
                    .And.Contain(HudPrimitives.WithYou(runnerUp.name, runnerUp.isPlayer)).And.Contain(EpisodeHud.FinaleWinnerRole).And.Contain(EpisodeHud.FinaleRunnerUpRole),
                    where + ": the final two by name and role.");
                Assert.That(LabelsUnder(jury), Does.Contain(EpisodeHud.FinaleJuryHeading), where + ": the jury card keeps its heading.");

                // A face a ballot, in cast order, ringed in the finalist's colour, the finalist's first name on its chip.
                var faces = jury.GetComponentsInChildren<Button>().Where(button => button.IsActive()).ToList();
                Assert.That(faces.Count, Is.EqualTo(ballots.Count), where + ": a face a ballot.");
                Assert.That(faces.Select(face => face.name), Is.EqualTo(castOrder), where + ": the faces in cast order.");
                foreach (var ballot in ballots)
                {
                    var juror = state.Find(ballot.JurorId);
                    string caption = EpisodeDirector.JurorFaceCaption(juror);
                    var face = faces.SingleOrDefault(button => button.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text == caption));
                    Assert.That(face, Is.Not.Null, where + ": " + juror.name + "'s face is a control captioned with their name.");
                    Assert.That(face.IsInteractable(), Is.True, where + ": and pressable.");
                    var rim = face.GetComponentsInChildren<Image>(true).First(image => image.name == "Ring");
                    var expected = ballot.FinalistId == winner.id ? UiTheme.Gold : EndScreenKit.Steel;
                    Assert.That(rim.color, Is.EqualTo(expected), where + ": " + juror.name + "'s ring is in the colour of the finalist they chose.");
                    var chip = face.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == EpisodeHud.FinaleJurorChipWordName);
                    var chosen = state.Find(ballot.FinalistId);
                    Assert.That(chip.text, Is.EqualTo(chosen.isPlayer ? "You" : FinalistRead.FirstName(ballot.Finalist)),
                        where + ": the chip names the finalist by first name, or 'You' for the player.");
                    Assert.That(chip.text, Is.EqualTo(EpisodeDirector.FinalistChipWord(chosen)));
                    AssertInside(ScreenRect(jury), (RectTransform)face.transform, where + ": " + juror.name + "'s face");
                }
                Assert.That(FinalePart(EpisodeHud.FinaleReasonLineName), Is.Null, where + ": no reason is open until a face is pressed,");
                Assert.That(FinalePart(EpisodeHud.FinaleReasonHintName), Is.Not.Null, where + ": the foot says to press one.");

                // The count in tiles of each colour, the bar split by it, and the eyebrow as the reveal says it.
                Assert.That(TileNumber(two, EpisodeHud.FinaleWinnerTallyName), Is.EqualTo(forWinner.ToString()), where + ": the winner's count.");
                Assert.That(TileNumber(two, EpisodeHud.FinaleRunnerUpTallyName), Is.EqualTo(forRunnerUp.ToString()), where + ": the runner-up's count.");
                Assert.That(two.GetComponentsInChildren<TMP_Text>().Single(label => label.name == EpisodeHud.FinaleCountEyebrowName).text,
                    Is.EqualTo(JuryReveal.CountEyebrow(forWinner, forRunnerUp)), where + ": the count's eyebrow.");
                var bar = two.GetComponentsInChildren<RectTransform>().Last(rect => rect.name == "Vote bar");
                var first = bar.Find("First") as RectTransform;
                var second = bar.Find("Second") as RectTransform;
                Assert.That(first != null, Is.EqualTo(forWinner > 0), where + ": the bar's first part is there exactly when the winner has votes.");
                Assert.That(second != null, Is.EqualTo(forRunnerUp > 0), where + ": and the second when the runner-up has.");
                // Each part at the width the kit's rule gives it - its share less the gap, never under the
                // bar's height, a part lifted to that least taking the difference from the other - and
                // both inside the track: the first from its start, the second to its end, never meeting.
                float barWidth = bar.rect.width, barHeight = bar.rect.height;
                var (expectedFirst, expectedSecond) = EndScreenKit.SplitWidths(barWidth, barHeight, forWinner, forRunnerUp);
                if (first != null)
                {
                    Assert.That(first.rect.width, Is.EqualTo(expectedFirst).Within(.5f), where + ": the winner's part of the bar.");
                    Assert.That(first.anchoredPosition.x, Is.EqualTo(0f).Within(.5f), where + ": from the track's start.");
                }
                if (second != null)
                {
                    Assert.That(second.rect.width, Is.EqualTo(expectedSecond).Within(.5f), where + ": the runner-up's part of the bar.");
                    Assert.That(second.anchoredPosition.x + second.rect.width, Is.EqualTo(barWidth).Within(.5f), where + ": to the track's end.");
                }
                if (first != null && second != null)
                {
                    Assert.That(first.anchoredPosition.x + first.rect.width, Is.LessThanOrEqualTo(second.anchoredPosition.x + .5f), where + ": the two parts never meet.");
                    // Where neither part is lifted to its least, the split is the ballots' own.
                    if (expectedFirst > barHeight + .5f && expectedSecond > barHeight + .5f)
                    {
                        float ratio = forWinner / (float)forRunnerUp;
                        Assert.That((first.rect.width + 2f) / (second.rect.width + 2f), Is.EqualTo(ratio).Within(ratio * .05f),
                            where + ": the bar is split as the ballots were.");
                    }
                }
            }

            // The highlights: six tiles at the state's own counts, worked out here rather than read back.
            var strip = FinalePart(EpisodeHud.FinaleHighlightsName);
            if (strip != null)
            {
                On(EpisodeHud.FinaleHighlightsName);
                Assert.That(strip.IsChildOf(page), Is.True, where + " has its highlights.");
                int evictions = state.contestants.Count(c => c.status == ContestantStatus.Jury || c.status == ContestantStatus.Evicted);
                var expectedTiles = new[]
                {
                    (SeasonReport.HouseguestsTileCaption, state.contestants.Count.ToString()),
                    (state.week == 1 ? "Week" : "Weeks", state.week.ToString()),
                    (evictions == 1 ? EpisodeDirector.EvictionTileCaption : EpisodeDirector.EvictionsTileCaption, evictions.ToString()),
                    (SeasonReport.CompetitionsTileCaption, state.contestants.Sum(c => FinalistRead.Wins(state, c)).ToString()),
                    (EpisodeDirector.JuryVoteTileCaption, forWinner + "–" + forRunnerUp),
                    (EpisodeDirector.WinnerTileCaption, EpisodeDirector.WinnerTileValue),
                };
                var tiles = strip.Cast<Transform>().Where(child => expectedTiles.Any(tile => tile.Item1 == child.name)).ToList();
                Assert.That(tiles.Select(tile => tile.name), Is.EqualTo(expectedTiles.Select(tile => tile.Item1)), where + ": six tiles, in the mockup's order.");
                foreach (var (caption, value) in expectedTiles)
                {
                    var tile = (RectTransform)strip.Find(caption);
                    Assert.That(tile.GetComponentsInChildren<TMP_Text>().Single(label => label.name == "Value").text, Is.EqualTo(value), where + ": '" + caption + "'.");
                    Assert.That(tile.GetComponentsInChildren<TMP_Text>().Single(label => label.name == "Caption").text, Is.EqualTo(caption));
                    AssertInside(ScreenRect(strip), tile, where + "'s tile '" + caption + "'");
                }
                Assert.That(EpisodeDirector.FinaleHighlights(state).Select(tile => (tile.Caption, tile.Value)), Is.EqualTo(expectedTiles),
                    where + ": the page's own reading of the counts is the same.");
                if (jury != null) Assert.That(ScreenRect(jury).yMin, Is.GreaterThanOrEqualTo(ScreenRect(strip).yMax - .5f), where + ": the cards over the highlights.");
            }

            // The ways on: the six captions once each, the wide three with their lines over the thin three - or none of them.
            var ways = FinalePart(EpisodeHud.FinaleWaysName);
            var thin = FinalePart(EpisodeHud.FinaleSecondRowName);
            Assert.That(thin != null, Is.EqualTo(ways != null), where + ": the ways on's two rows stand on one page.");
            if (ways != null)
            {
                On(EpisodeHud.FinaleWaysName);
                foreach (var caption in FinaleWaysOn)
                    Assert.That(FinaleControlCount(caption), Is.EqualTo(1), where + " offers '" + caption + "' once.");
                Assert.That(LabelsUnder(ways), Does.Contain(EpisodeDirector.SeasonReportLine).And.Contain(EpisodeDirector.WatchFinaleReplayLine)
                    .And.Contain(EpisodeDirector.MainMenuLine), where + ": each wide way says where it leads.");
                Assert.That(LabelsUnder(thin), Does.Contain(EpisodeDirector.NewSeasonLine).And.Contain(EpisodeDirector.ReviewSeasonLine)
                    .And.Contain(EpisodeDirector.JuryQuestionsLine), where + ": and so does the thin row.");
                Assert.That(ScreenRect(ways).yMin, Is.GreaterThanOrEqualTo(ScreenRect(thin).yMax - .5f), where + ": the wide row stands over the thin one.");
                if (strip != null) Assert.That(ScreenRect(strip).yMin, Is.GreaterThanOrEqualTo(ScreenRect(ways).yMax - .5f), where + ": the highlights over the ways on.");
                foreach (var button in ways.GetComponentsInChildren<Button>().Concat(thin.GetComponentsInChildren<Button>()))
                    Assert.That(button.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(button.name), where + ": a tile's caption is its first label and its name.");
            }
            else
                foreach (var caption in FinaleWaysOn)
                    Assert.That(FinaleControlCount(caption), Is.EqualTo(0), where + ": '" + caption + "' is on the ways on's page, not this one.");

            // Every label draws, in a box at least 1.3 times its size.
            AssertEveryLabelDraws(page, where);
            foreach (var label in page.GetComponentsInChildren<TMP_Text>())
            {
                float size = label.enableAutoSizing ? label.fontSizeMax : label.fontSize;
                Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(size * 1.3f - .5f),
                    where + ": '" + label.text + "' (" + label.name + ") has a box Inter draws in: " + label.rectTransform.rect.height.ToString("0.#") + " for " + size + ".");
            }

            // Each face and each caption on the page in the keyboard ring once.
            var ring = ActiveRect(ModalRoot).GetComponentsInChildren<Button>()
                .Where(button => button.IsActive() && button.IsInteractable() && button.navigation.mode != Navigation.Mode.None).ToList();
            foreach (var caption in (jury != null ? castOrder : new List<string>()).Concat(ways != null ? FinaleWaysOn : new string[0]))
                Assert.That(ring.Count(button => button.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text == caption)), Is.EqualTo(1),
                    where + ": '" + caption + "' is in the keyboard ring once.");
        }

        /// <summary>
        /// The pages the finale's page has at the largest text, on the 16:9 frame and on the 4:3 (the
        /// batch canvas is 4:3): the suite's literals for the owner's contract (EpisodeHud.LargestPages.cs),
        /// measured on 2026-10-16, never the renderer's arithmetic. A season played to its end, and the
        /// jury of fourteen. At the standard and the larger text the page is one.
        /// </summary>
        private static readonly (int At16x9, int At4x3) FinalePagesPlayed = (2, 1), FinalePagesFourteen = (2, 1);

        /// <summary>
        /// The page as CaptureFraming draws it, re-laid for its frame (1600x900, or 1200x900 for the
        /// 4:3 canvas): on a frame of <paramref name="aspect"/>, holding without a scroll, the page
        /// inside the stage's view and its three cards in a row inside it - on its page, of the
        /// <paramref name="largestPages"/> the suite names for the frame at the largest text.
        /// </summary>
        private void AssertTheFinalePageOnTheFrame(string where, float aspect, int largestPages = 1)
        {
            Canvas.ForceUpdateCanvases();
            var shown = 0;
            AssertLargestPages(where, largestPages, pageNumber =>
            {
                string here = largestPages > 1 && director.TextSize == TextSize.Largest ? where + ", page " + pageNumber + " of " + largestPages : where;
                var page = ActiveRect(EpisodeHud.FinalePageName);
                Assert.That(page, Is.Not.Null, here + " is the page.");
                var frame = (RectTransform)page.GetComponentInParent<Canvas>().rootCanvas.transform;
                Assert.That(frame.rect.width / frame.rect.height, Is.EqualTo(aspect).Within(.02f),
                    here + " is laid out on the frame asked for, not the batch canvas's " + frame.rect.size + ".");
                var content = ActiveRect("Episode content");
                var viewport = (RectTransform)content.parent;
                Assert.That(content.rect.height, Is.LessThanOrEqualTo(viewport.rect.height + .5f),
                    here + " holds without a scroll: " + content.rect.height.ToString("0") + " in " + viewport.rect.height.ToString("0") + ".");
                Rect view = CanvasRect(viewport), whole = CanvasRect(page);
                Assert.That(whole.yMin >= view.yMin - .5f && whole.yMax <= view.yMax + .5f, Is.True, here + "'s page is inside the stage's view.");
                var cards = new[] { EpisodeHud.FinaleWinnerCardName, EpisodeHud.FinaleFinalTwoCardName, EpisodeHud.FinaleJuryCardName }
                    .Select(name => page.GetComponentsInChildren<RectTransform>().LastOrDefault(rect => rect.name == name)).ToArray();
                if (cards.All(card => card == null)) return;
                shown++;
                for (int i = 0; i < cards.Length; i++)
                {
                    Assert.That(cards[i], Is.Not.Null, here + ": the three cards stand in one row.");
                    var card = CanvasRect(cards[i]);
                    Assert.That(card.xMin >= whole.xMin - .5f && card.xMax <= whole.xMax + .5f && card.yMin >= whole.yMin - .5f && card.yMax <= whole.yMax + .5f,
                        Is.True, here + ": '" + cards[i].name + "' is inside the page.");
                    if (i > 0) Assert.That(CanvasRect(cards[i - 1]).xMax, Is.LessThanOrEqualTo(card.xMin + .5f), here + ": '" + cards[i].name + "' stands right of '" + cards[i - 1].name + "'.");
                }
            });
            Assert.That(shown, Is.EqualTo(1), where + ": the three cards are on one page.");
        }

        /// <summary>
        /// Each juror's face pressed in turn: the reason they gave with their vote is the jury card's
        /// foot line, word for word ("Your ballot" for the player's own), and a second press folds it.
        /// Nothing is committed. The career test's pins on the old one-line-a-ballot panel moved here.
        /// </summary>
        private IEnumerator AssertEachJurorsReasonOnAPress(List<SeasonReport.JuryBallot> ballots, string where)
        {
            var state = director.Snapshot;
            int revision = state.revision;
            foreach (var ballot in ballots)
            {
                var juror = state.Find(ballot.JurorId);
                JurorFace(juror).onClick.Invoke();
                yield return null;
                Assert.That(director.Snapshot.revision, Is.EqualTo(revision), where + ": a press on a face commits nothing.");
                Assert.That(director.FinalePressedJuror, Is.EqualTo(juror.id), where + ": " + juror.name + "'s face is the pressed one.");
                string line = FinaleReasonLine();
                Assert.That(line, Is.Not.Null, where + ": a press on " + juror.name + "'s face shows their reason at the card's foot.");
                Assert.That(line, Is.EqualTo(EpisodeDirector.JurorReasonLine(ballot)), where + ": the line is the ballot's.");
                if (ballot.IsPlayer) Assert.That(line, Is.EqualTo(EpisodeDirector.YourBallotLine), where + ": the player's own ballot says so.");
                else Assert.That(line, Is.EqualTo(ballot.Reason), where + ": the reason as they gave it, word for word.");
                var eyebrow = FinalePart(EpisodeHud.FinaleReasonEyebrowName);
                Assert.That(eyebrow, Is.Not.Null, where + ": over an eyebrow naming them and their choice,");
                var chosen = state.Find(ballot.FinalistId);
                Assert.That(eyebrow.GetComponent<TMP_Text>().text, Does.EndWith("VOTED " + (chosen.isPlayer ? "YOU" : FinalistRead.FirstName(ballot.Finalist).ToUpperInvariant())),
                    where + ": the finalist by first name, or 'YOU' for the player.");
                Assert.That(FinalePart(EpisodeHud.FinaleReasonHintName), Is.Null, where + ": in the hint's place.");
                JurorFace(juror).onClick.Invoke();
                yield return null;
                Assert.That(FinaleReasonLine(), Is.Null, where + ": a second press folds " + juror.name + "'s reason.");
                Assert.That(director.FinalePressedJuror, Is.Null);
            }
        }

        /// <summary>
        /// Every assertion of <see cref="AssertFinalePage"/> - each part whole on one page and in
        /// order, the six ways on once each, the highlights at their counts, every label box at least
        /// 1.3 times its type, the ring - and of <see cref="AssertTheFinalePageOnTheFrame"/>, on the HUD
        /// laid out for the 16:9 frame and for the 4:3, each at the suite's literal pages for that frame
        /// (review S4C-R1: the 16:9 frame is the one that pages, and its ways on are a page of their own).
        /// </summary>
        private IEnumerator AssertFinalePageOnBothFrames(string where, (int At16x9, int At4x3) largestPages) =>
            AtBothFrames(frame =>
            {
                string on = where + " on the " + frame + " frame";
                AssertFinalePage(on, PagesOn(frame, largestPages));
                AssertTheFinalePageOnTheFrame(on, frame == "16:9" ? 16f / 9f : 4f / 3f, PagesOn(frame, largestPages));
            });

        /// <summary>
        /// A season played to its end and loaded from its save: the page at both text sizes, with
        /// every assertion of <see cref="AssertFinalePage"/>; a face the keyboard is on seen by its
        /// lifted edge; the keyboard ring walked; and, in a batch run, photographed on the 16:9 frame
        /// at each size and on the 4:3 one at the resting size, held on each without a scroll. Laid
        /// for the 16:9 frame and the 4:3 at each size, it is one page at the standard and the larger
        /// text, and at the largest the suite's literal pages for each frame (<see cref="FinalePagesPlayed"/>),
        /// every page held to every assertion of <see cref="AssertFinalePage"/> on its frame, and each
        /// photographed in a batch run.
        /// </summary>
        [UnityTest]
        public IEnumerator FinalePage_TheCardsTheTallyAndTheWaysOnHoldAtBothTextSizes()
        {
            yield return InstallFinishedSeason();
            director.SuspendNpcAutonomyForDiagnostics();
            foreach (var (textSize, larger) in TextScales.Steps)
            {
                if (larger) { director.ClosePanels(); yield return ApplyTextSize(textSize); }
                yield return OpenFinalePanel();
                string where = "The finale page" + (larger ? " " + TextScales.At(textSize) : "");
                AssertFinalePage(where + " on the run's canvas", PagesOnTheHudNow(FinalePagesPlayed));
                yield return AssertFinalePageOnBothFrames(where, FinalePagesPlayed);

                // A face the keyboard is on is seen: its edge steps up from the others' (HudEmphasis).
                var faces = FinalePart(EpisodeHud.FinaleJuryCardName).GetComponentsInChildren<Button>().Where(button => button.IsActive()).ToList();
                Assert.That(faces.Count, Is.GreaterThanOrEqualTo(2), where + ": two faces to tell apart.");
                Color Edge(Button face) => face.transform.Find("Border").GetComponent<Image>().color;
                EventSystem.current.SetSelectedGameObject(faces[0].gameObject);
                yield return null;
                Assert.That(Edge(faces[0]), Is.EqualTo(UiTheme.Edge(UiTheme.Emphasis.Interactive)), where + ": the face the keyboard is on wears the lifted edge,");
                Assert.That(Edge(faces[1]), Is.EqualTo(UiTheme.Edge(UiTheme.Emphasis.Resting)), where + ": the others their resting edge,");
                Assert.That(Edge(faces[0]), Is.Not.EqualTo(Edge(faces[1])), where + ": so the two are told apart.");

                yield return AssertKeyboardRing(where, ModalRoot);
                if (!Application.isBatchMode) continue;
                yield return CaptureFraming("finale-page" + TextScales.Suffix(textSize),
                    inspect: frame => AssertTheFinalePageOnTheFrame(where + " at 16:9", 16f / 9f, FinalePagesPlayed.At16x9));
                if (!larger)
                    yield return CaptureFraming("finale-page-43", inspect: frame => AssertTheFinalePageOnTheFrame(where + " at 4:3", 4f / 3f), width: 1200, height: 900);
                if (textSize == TextSize.Largest) yield return CaptureLargestPagesPerFrame("finale-page", FinalePagesPlayed);
            }
            director.ClosePanels();
            yield return ApplyTextSize(false);
        }

        /// <summary>
        /// Runs <paramref name="steps"/> - real presses and the frames they take - on the HUD laid out
        /// for a frame of this shape the whole time, where <see cref="AtFrame"/> lays it out for one
        /// synchronous look: through the capture's lens on a target of that size, the HUD rendered for
        /// it. The steps are driven here, nested enumerators and all, so a failure in one still closes
        /// the lens; then the HUD is put back on the screen's own shape.
        /// </summary>
        private IEnumerator AtFrameThroughout(int width, int height, System.Func<IEnumerator> steps)
        {
            var lens = new CaptureLens(cameraRig.ViewCamera, width, height);
            var running = new Stack<IEnumerator>();
            try
            {
                Canvas.ForceUpdateCanvases();
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                RenderHudForTheCurrentCanvas();
                yield return null;
                Canvas.ForceUpdateCanvases();
                var root = ((RectTransform)HudCanvas().transform).rect;
                Assert.That(root.width / root.height, Is.EqualTo((float)width / height).Within(.01f),
                    "The HUD is laid out for the " + width + "x" + height + " frame: " + root.size + ".");
                running.Push(steps());
                while (running.Count > 0)
                {
                    var top = running.Peek();
                    if (!top.MoveNext()) { running.Pop(); continue; }
                    if (top.Current is IEnumerator nested) { running.Push(nested); continue; }
                    yield return top.Current;
                }
            }
            finally
            {
                while (running.Count > 0) (running.Pop() as System.IDisposable)?.Dispose();
                lens.Dispose();
            }
            Canvas.ForceUpdateCanvases();
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            RenderHudForTheCurrentCanvas();
            yield return null;
        }

        /// <summary>
        /// The owner's contract of 2026-10-15 on the target frame (1920x1080, 16:9) at the largest text,
        /// where the finale is the suite's two pages (<see cref="FinalePagesPlayed"/>) and the season's
        /// only exits - the six ways on - are the second (review S4C-R1). Laid out for the 16:9 frame
        /// for the whole walk: the page opens on its first, with no way on on it. From where it opens,
        /// Tab walks the ring to "Next page" and Enter turns to the ways on, each offered once, the
        /// keyboard staying on the pager; Enter on "Previous page" turns back. Opened again, the d-pad
        /// walks down from where it opens to "Next page" and the pad's A turns to the ways on, and the
        /// d-pad walks on from the pager to "Season report", where A opens the report. Every walk and
        /// turn leaves the season, both random streams and every save byte as they were.
        /// </summary>
        [UnityTest]
        public IEnumerator FinalePage_AtTheLargestTextOnTheWideFrameTheWaysOnAreAPageTheKeyboardAndPadReach()
        {
            yield return InstallFinishedSeason();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return ApplyTextSize(TextSize.Largest);
            yield return OpenFinalePanel();
            yield return AtFrameThroughout(1600, 900, ReachTheFinalesWaysOnOnTheWideFrame);
            director.ClosePanels();
            yield return ApplyTextSize(false);
        }

        /// <summary>The walk of <see cref="FinalePage_AtTheLargestTextOnTheWideFrameTheWaysOnAreAPageTheKeyboardAndPadReach"/>, on the HUD laid out for 16:9.</summary>
        private IEnumerator ReachTheFinalesWaysOnOnTheWideFrame()
        {
            string where = "The finale page " + TextScales.At(TextSize.Largest) + " on the 16:9 frame";
            Assert.That(LargestFrameOf(HudCanvas()), Is.EqualTo("16:9"), where + " is laid out for that frame.");
            int pages = FinalePagesPlayed.At16x9;
            Assert.That(pages, Is.EqualTo(2), where + ": the suite names two pages for it.");
            yield return Frames(2);
            Assert.That(LargestPageIndicator(), Is.EqualTo((1, pages)), where + ": the page opens on the first of its " + pages + ".");
            foreach (var caption in FinaleWaysOn)
                Assert.That(FinaleControlCount(caption), Is.EqualTo(0), where + ": '" + caption + "' is on the ways on's page, not the first.");
            int revision = director.Snapshot.revision;
            string Walk(List<string> walked) => " Walked: [" + string.Join(" | ", walked) + "].";

            // The keyboard: Tab walks the ring from where the page opens to "Next page", and Enter turns to the ways on.
            Assert.That(Selected(), Is.Not.Null, where + ": the page opens with the keyboard on a control.");
            var before = FreeTimePagerStateNow11();
            var walked = new List<string> { SelectedName() };
            for (int step = 0; step < 40 && SelectedName() != EpisodeHud.NextPageCaption; step++)
            {
                yield return PressKey(UnityEngine.InputSystem.Key.Tab);
                walked.Add(SelectedName());
            }
            Assert.That(SelectedName(), Is.EqualTo(EpisodeHud.NextPageCaption), where + ": Tab walks the ring to '" + EpisodeHud.NextPageCaption + "'." + Walk(walked));
            Assert.That(LargestPageIndicator(), Is.EqualTo((1, pages)), where + ": walking to the pager turns nothing.");
            yield return PressKey(UnityEngine.InputSystem.Key.Enter);
            yield return Frames(2);
            Assert.That(LargestPageIndicator(), Is.EqualTo((2, pages)), where + ": Enter on '" + EpisodeHud.NextPageCaption + "' turns to the ways on,");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), where + ": committing nothing.");
            FreeTimeAssertPagerNeutral11(before, where + ", Tab's walk and Enter on '" + EpisodeHud.NextPageCaption + "'");
            foreach (var caption in FinaleWaysOn)
                Assert.That(FinaleControlCount(caption), Is.EqualTo(1), where + ": the ways on's page offers '" + caption + "' once.");
            Assert.That(SelectedName(), Is.EqualTo(EpisodeHud.PreviousPageCaption), where + ": the keyboard stays on the pager as it turns.");

            // Enter on the way back turns back.
            before = FreeTimePagerStateNow11();
            yield return PressKey(UnityEngine.InputSystem.Key.Enter);
            yield return Frames(2);
            Assert.That(LargestPageIndicator(), Is.EqualTo((1, pages)), where + ": Enter on '" + EpisodeHud.PreviousPageCaption + "' turns back,");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), where + ": committing nothing.");
            FreeTimeAssertPagerNeutral11(before, where + ", Enter on '" + EpisodeHud.PreviousPageCaption + "'");
            Assert.That(SelectedName(), Is.EqualTo(EpisodeHud.NextPageCaption), where + ": and the keyboard is on the way on again.");

            // The pad, from where the page opens: the d-pad walks the ring down to the way on and A turns to the ways on.
            director.ClosePanels();
            yield return null;
            yield return OpenFinalePanel();
            yield return Frames(2);
            Assert.That(LargestPageIndicator(), Is.EqualTo((1, pages)), where + ": opened again, the page is on its first.");
            revision = director.Snapshot.revision;
            before = FreeTimePagerStateNow11();
            walked = new List<string> { SelectedName() };
            for (int step = 0; step < 40 && SelectedName() != EpisodeHud.NextPageCaption; step++)
            {
                yield return PressOnPad(UnityEngine.InputSystem.LowLevel.GamepadButton.DpadDown);
                walked.Add(SelectedName());
            }
            Assert.That(SelectedName(), Is.EqualTo(EpisodeHud.NextPageCaption), where + ": the d-pad walks down to '" + EpisodeHud.NextPageCaption + "'." + Walk(walked));
            Assert.That(LargestPageIndicator(), Is.EqualTo((1, pages)), where + ": walking to the pager turns nothing.");
            yield return PressOnPad(UnityEngine.InputSystem.LowLevel.GamepadButton.South);
            yield return Frames(2);
            Assert.That(LargestPageIndicator(), Is.EqualTo((2, pages)), where + ": the pad's A on '" + EpisodeHud.NextPageCaption + "' turns to the ways on,");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), where + ": committing nothing.");
            FreeTimeAssertPagerNeutral11(before, where + ", the d-pad's walk and A on '" + EpisodeHud.NextPageCaption + "'");

            // On the ways on's page the d-pad walks on from the pager to "Season report", and A there opens the report.
            before = FreeTimePagerStateNow11();
            walked = new List<string> { SelectedName() };
            for (int step = 0; step < 40 && SelectedName() != SeasonReportRowCaption; step++)
            {
                yield return PressOnPad(UnityEngine.InputSystem.LowLevel.GamepadButton.DpadDown);
                walked.Add(SelectedName());
            }
            Assert.That(SelectedName(), Is.EqualTo(SeasonReportRowCaption), where + ": the d-pad walks from the pager to '" + SeasonReportRowCaption + "'." + Walk(walked));
            Assert.That(LargestPageIndicator(), Is.EqualTo((2, pages)), where + ": walking the ways on's page turns nothing.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), where + ": the walk commits nothing.");
            FreeTimeAssertPagerNeutral11(before, where + ", the d-pad's walk from the pager to '" + SeasonReportRowCaption + "'");
            yield return PressOnPad(UnityEngine.InputSystem.LowLevel.GamepadButton.South);
            yield return Frames(2);
            Assert.That(director.IsSeasonReportOpen, Is.True, where + ": the pad's A on '" + SeasonReportRowCaption + "' opens the report.");
            ReportButtons(SeasonReport.CloseCaption)[0].onClick.Invoke();
            yield return null;
            Assert.That(director.IsSeasonReportOpen, Is.False, where + ": and Close closes it.");
        }

        /// <summary>
        /// A press on a juror's face shows the reason they gave as the jury card's foot line and a
        /// second press folds it; a press on another juror's face while one is open swaps to theirs;
        /// the page still holds without a scroll with a reason open; and, in a batch run, the page
        /// is photographed with the first juror's reason open.
        /// </summary>
        [UnityTest]
        public IEnumerator FinalePage_AJurorsFacePressedShowsTheirReasonAndASecondPressFoldsIt()
        {
            yield return InstallFinishedSeason();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return OpenFinalePanel();
            var state = director.Snapshot;
            var ballots = SeasonReport.JuryBallots(state);
            Assert.That(ballots, Is.Not.Empty, "The season has a jury.");
            yield return AssertEachJurorsReasonOnAPress(ballots, "The finale page");

            if (ballots.Count >= 2)
            {
                var firstJuror = state.Find(ballots[0].JurorId);
                var secondJuror = state.Find(ballots[1].JurorId);
                JurorFace(firstJuror).onClick.Invoke();
                yield return null;
                JurorFace(secondJuror).onClick.Invoke();
                yield return null;
                Assert.That(FinaleReasonLine(), Is.EqualTo(EpisodeDirector.JurorReasonLine(ballots[1])), "Another face pressed while one is open swaps to theirs.");
                JurorFace(secondJuror).onClick.Invoke();
                yield return null;
                Assert.That(FinaleReasonLine(), Is.Null, "and a press on the open one folds it.");
            }

            // With a reason open the page still holds, and its labels still draw.
            var opened = state.Find(ballots.First(ballot => !ballot.IsPlayer).JurorId);
            JurorFace(opened).onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var content = ActiveRect("Episode content");
            Assert.That(content.rect.height, Is.LessThanOrEqualTo(((RectTransform)content.parent).rect.height + .5f), "A reason open holds without a scroll.");
            AssertEveryLabelDraws(FinalePart(EpisodeHud.FinalePageName), "The finale page with a reason open");
            if (Application.isBatchMode) yield return CaptureFraming("finale-votes", inspect: frame => AssertTheFinalePageOnTheFrame("The finale page with a reason open at 16:9", 16f / 9f));
            director.ClosePanels();
        }

        /// <summary>The number on one of the highlights' tiles, by the tile's caption.</summary>
        private string HighlightValue(string caption) =>
            ((RectTransform)FinalePart(EpisodeHud.FinaleHighlightsName).Find(caption)).GetComponentsInChildren<TMP_Text>().Single(label => label.name == "Value").text;

        /// <summary>
        /// A jury of fourteen (the EndScreens fixture of sixteen) at both text sizes: a face a ballot,
        /// in no more than three rows, inside the card, the page holding without a scroll, and the
        /// highlights at the fixture's known counts; in a batch run photographed on the 16:9 frame. Laid
        /// for the 16:9 frame and the 4:3, it is one page at the standard and the larger text, and at
        /// the largest the suite's literal pages for each frame (<see cref="FinalePagesFourteen"/>),
        /// every page held to every assertion of <see cref="AssertFinalePage"/> on its frame.
        /// Then the tie fixture: equal halves of the bar, the jury called tied, and the highlights'
        /// vote tile saying so. Then a player in the final two: "You" on the chips of the ballots
        /// for them and "VOTED YOU" over the reason, as every reader names the player.
        /// </summary>
        [UnityTest]
        public IEnumerator FinalePage_AJuryOfFourteenHoldsATieDrawsEqualHalvesAndAPlayerFinalistIsYou()
        {
            var sixteen = Finished(16);
            Assert.That(SeasonReport.JuryBallots(sixteen), Has.Count.EqualTo(14), "A jury of fourteen.");
            yield return InstallBuiltFinale(sixteen);
            foreach (var (textSize, larger) in TextScales.Steps)
            {
                if (larger) { director.ClosePanels(); yield return ApplyTextSize(textSize); }
                yield return OpenFinalePanel();
                string where = "A jury of fourteen" + (larger ? " " + TextScales.At(textSize) : "");
                AssertFinalePage(where + " on the run's canvas", PagesOnTheHudNow(FinalePagesFourteen));
                yield return AssertFinalePageOnBothFrames(where, FinalePagesFourteen);
                var jury = FinalePart(EpisodeHud.FinaleJuryCardName);
                var faces = jury.GetComponentsInChildren<Button>().Where(button => button.IsActive()).ToList();
                Assert.That(faces, Has.Count.EqualTo(14), where + ": fourteen faces.");
                int rows = faces.Select(face => Mathf.RoundToInt(ScreenRect((RectTransform)face.transform).yMax)).Distinct().Count();
                Assert.That(rows, Is.LessThanOrEqualTo(3), where + ": in no more than three rows.");
                // The counts the fixture is known to have: sixteen houseguests in week one, fourteen
                // out, the champion's three HoHs and two vetoes the only wins, thirteen votes to one.
                Assert.That(HighlightValue("Houseguests"), Is.EqualTo("16"), where);
                Assert.That(HighlightValue("Week"), Is.EqualTo("1"), where);
                Assert.That(HighlightValue("Evictions"), Is.EqualTo("14"), where);
                Assert.That(HighlightValue("Competitions held"), Is.EqualTo("5"), where);
                Assert.That(HighlightValue("Jury vote"), Is.EqualTo("13–1"), where);
                Assert.That(HighlightValue("Winner"), Is.EqualTo("One"), where);
                if (Application.isBatchMode && !larger)
                    yield return CaptureFraming("finale-page-14", inspect: frame => AssertTheFinalePageOnTheFrame(where + " at 16:9", 16f / 9f));
            }
            director.ClosePanels();
            yield return ApplyTextSize(false);

            var tied = TiedFinale();
            var champion = tied.Find(tied.winnerId);
            var runnerUp = tied.Find(tied.runnerUpId);
            int each = tied.votes.Count(v => v.targetId == champion.id);
            Assert.That(each, Is.EqualTo(3), "Six ballots, three each way.");
            Assert.That(tied.votes.Count(v => v.targetId == runnerUp.id), Is.EqualTo(each), "The fixture is tied.");
            yield return InstallBuiltFinale(tied);
            yield return OpenFinalePanel();
            AssertFinalePage("A tied jury");
            var two = FinalePart(EpisodeHud.FinaleFinalTwoCardName);
            var bar = two.GetComponentsInChildren<RectTransform>().Last(rect => rect.name == "Vote bar");
            var first = (RectTransform)bar.Find("First");
            var second = (RectTransform)bar.Find("Second");
            Assert.That(first.rect.width, Is.EqualTo(second.rect.width).Within(.5f), "A tie draws equal halves.");
            Assert.That(two.GetComponentsInChildren<TMP_Text>().Single(label => label.name == EpisodeHud.FinaleCountEyebrowName).text,
                Is.EqualTo("THE JURY IS TIED"), "and is called a tie, as the reveal calls it.");
            Assert.That(TileNumber(two, EpisodeHud.FinaleWinnerTallyName), Is.EqualTo("3"));
            Assert.That(TileNumber(two, EpisodeHud.FinaleRunnerUpTallyName), Is.EqualTo("3"));
            Assert.That(HighlightValue("Houseguests"), Is.EqualTo("8"));
            Assert.That(HighlightValue("Evictions"), Is.EqualTo("6"));
            Assert.That(HighlightValue("Jury vote"), Is.EqualTo("3–3"));
            director.ClosePanels();

            // A player who reached the final two is "you" where the page names the finalist.
            var yours = PlayerRunnerUp();
            yield return InstallBuiltFinale(yours);
            yield return OpenFinalePanel();
            AssertFinalePage("A player in the final two");
            var state = director.Snapshot;
            Assert.That(state.runnerUpId, Is.EqualTo(state.playerId), "The player is the runner-up.");
            var forYou = SeasonReport.JuryBallots(state).Where(ballot => ballot.FinalistId == state.playerId).ToList();
            Assert.That(forYou, Is.Not.Empty, "Somebody voted for the player.");
            var chips = FinalePart(EpisodeHud.FinaleJuryCardName).GetComponentsInChildren<TMP_Text>()
                .Where(label => label.name == EpisodeHud.FinaleJurorChipWordName).Select(label => label.text).ToList();
            Assert.That(chips.Count(word => word == "You"), Is.EqualTo(forYou.Count), "A 'You' chip for each ballot for the player,");
            Assert.That(chips.Count(word => word == FinalistRead.FirstName(state.Find(state.winnerId).name)), Is.EqualTo(chips.Count - forYou.Count),
                "the winner's first name on the rest.");
            var juror = state.Find(forYou[0].JurorId);
            JurorFace(juror).onClick.Invoke();
            yield return null;
            Assert.That(FinalePart(EpisodeHud.FinaleReasonEyebrowName).GetComponent<TMP_Text>().text, Does.EndWith("VOTED YOU"),
                "and 'VOTED YOU' over their reason.");
            Assert.That(FinaleReasonLine(), Is.EqualTo(forYou[0].Reason));
            director.ClosePanels();
        }

        /// <summary>
        /// The ways on lead where they say: Enter on "Season report" opens the report, as the
        /// keyboard walk has always pressed it; "The jury's questions" opens the record as the step,
        /// with the way back pinned and the keyboard on it, the page gone from under it and the
        /// disclosure still one control; the way back brings the page back with nothing committed.
        /// </summary>
        [UnityTest]
        public IEnumerator FinalePage_EnterOpensTheReportAndTheJurysQuestionsOpenAsTheStep()
        {
            yield return InstallFinishedSeason();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return OpenFinalePanel();
            var state = director.Snapshot;
            var ballots = SeasonReport.JuryBallots(state);

            yield return KeyboardSubmit(SeasonReportRowCaption);
            Assert.That(director.IsSeasonReportOpen, Is.True, "Enter on 'Season report' opens the report.");
            ReportButtons(SeasonReport.CloseCaption)[0].onClick.Invoke();
            yield return null;
            Assert.That(director.IsSeasonReportOpen, Is.False, "Close closes it,");
            Assert.That(director.IsPhasePanelOpen, Is.True, "and leaves the page under it.");

            int revision = director.Snapshot.revision;
            FinaleControl(EpisodeDirector.JuryQuestionsCaption).onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Opening the jury's questions commits nothing.");
            Assert.That(FinalePart(EpisodeHud.FinalePageName), Is.Null, "The record takes the stage as the step.");
            Assert.That(FinalePanelLines(EpisodeDirector.JuryQuestionsReasonName), Has.Length.EqualTo(ballots.Count), "A line for each juror's ballot.");
            Assert.That(FinaleControlCount(EpisodeDirector.JuryQuestionsCaption), Is.EqualTo(1), "The disclosure is still one control, to fold it.");
            var back = FinaleControl(EpisodeDirector.BackToFinaleCaption);
            Assert.That(back.transform.parent, Is.SameAs(ActiveRect("Episode panel")), "The way back is pinned in the footer, not in the scroll.");
            var focus = EventSystem.current.currentSelectedGameObject;
            Assert.That(focus != null ? focus.name : "nothing", Is.EqualTo(EpisodeDirector.BackToFinaleCaption),
                "The keyboard is on the way back as the step opens, so a second Enter goes back.");
            AssertEveryLabelDraws("The jury's questions as the step");

            back.onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Going back commits nothing.");
            Assert.That(FinalePart(EpisodeHud.FinalePageName), Is.Not.Null, "The page is back,");
            Assert.That(FinalePanelLines(EpisodeDirector.JuryQuestionsQuestionName), Is.Empty, "with the questions folded,");
            Assert.That(FinaleControlCount(EpisodeDirector.BackToFinaleCaption), Is.EqualTo(0), "and no way back on it.");
            AssertFinalePage("The finale page after the jury's questions");
            director.ClosePanels();
        }
    }
}
