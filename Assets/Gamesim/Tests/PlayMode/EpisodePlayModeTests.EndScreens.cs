using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The two summary screens as the owner's mockups lay them out, in Season Complete Pack 7's
    /// frames. The season's end: the winner and runner-up, the five cards, the standings, the jury's
    /// ballots and the weeks side by side, the career under them, the tabs that scroll to the detail -
    /// the counts told as the house tells them, a tie as a tie. The week's end: who left and where
    /// they finished, the five headline facts, and five tabs that each show a part of the week with
    /// one Continue at the foot whichever is open. Reading either commits nothing.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private RectTransform ReportPart(string name) => Report().GetComponentsInChildren<RectTransform>(true)
            .LastOrDefault(rect => rect.name == name && rect.gameObject.activeInHierarchy);

        private static string[] LabelsUnder(RectTransform root) =>
            root.GetComponentsInChildren<TMP_Text>().Where(label => label.isActiveAndEnabled).Select(label => label.text).ToArray();

        /// <summary>
        /// A finished season of eight with its record in the ledger, as the engine keeps it: six
        /// weeks, one evictee a week, the last the final eviction; the jury ledger filled in the
        /// order they left; ballots from the jury only, not from the one out before it; and two
        /// final speeches that open on the same sentence.
        /// </summary>
        private static EpisodeState FinishedWithWeeks()
        {
            var state = Finished();
            var cast = state.contestants;
            var champion = state.Find(state.winnerId);
            var runnerUp = state.Find(state.runnerUpId);
            var you = state.Find(state.playerId);
            var early = cast.Single(c => c.status == ContestantStatus.Evicted);
            var jury = cast.Where(c => c.status == ContestantStatus.Jury && !c.isPlayer).ToList();
            state.votes.RemoveAll(vote => vote.voterId == early.id);
            state.week = 6;
            state.ledger.power.Add(new PowerRow { week = 1, hohId = champion.id, vetoHolderId = runnerUp.id, vetoUsed = false,
                nominees = new List<string> { early.id, jury[0].id }, evicteeId = early.id, tally = new List<int> { 4, 1 } });
            state.ledger.power.Add(new PowerRow { week = 2, hohId = runnerUp.id, vetoHolderId = runnerUp.id, vetoUsed = true, savedId = jury[2].id,
                replacementId = jury[1].id, nominees = new List<string> { jury[1].id, jury[3].id }, evicteeId = jury[1].id, tally = new List<int> { 3, 1 } });
            state.ledger.power.Add(new PowerRow { week = 3, hohId = jury[3].id, vetoHolderId = champion.id, vetoUsed = false,
                nominees = new List<string> { jury[0].id, you.id }, evicteeId = jury[0].id, tally = new List<int> { 2, 1 } });
            state.ledger.power.Add(new PowerRow { week = 4, hohId = jury[2].id, vetoHolderId = runnerUp.id, vetoUsed = false,
                nominees = new List<string> { you.id, champion.id }, evicteeId = you.id, tally = new List<int> { 2, 0 } });
            state.ledger.power.Add(new PowerRow { week = 5, hohId = champion.id, vetoHolderId = champion.id, vetoUsed = false,
                nominees = new List<string> { jury[2].id, jury[3].id }, evicteeId = jury[2].id, tally = new List<int> { 1, 0 } });
            state.ledger.power.Add(new PowerRow { week = 6, hohId = champion.id, nominees = new List<string> { runnerUp.id, jury[3].id }, evicteeId = jury[3].id });
            // The jury ledger fills as each one leaves, and its order is the placement's.
            foreach (var gone in new[] { early, jury[1], jury[0], you, jury[2], jury[3] })
                state.jurySentiment = WebJurySentiment.AddJuror(state.jurySentiment, gone.id, gone.name, 0);
            state.finalPart1WinnerId = champion.id;
            state.finalPart2WinnerId = runnerUp.id;
            state.finalSpeeches.Add(new FinalSpeechState { speakerId = champion.id,
                text = "I played every week like it was my last. I won when I had to, and I never hid from it." });
            state.finalSpeeches.Add(new FinalSpeechState { speakerId = runnerUp.id,
                text = "I played every week like it was my last. I kept my word to every one of you." });
            return state;
        }

        /// <summary>The first sentence of the runner-up's speech is the winner's; their card quotes the next.</summary>
        private const string RunnerUpNextSentence = "I kept my word to every one of you.";

        [UnityTest]
        public IEnumerator EndScreens_TheSeasonReportReadsTheSeasonAtAGlance()
        {
            var state = FinishedWithWeeks();
            var champion = state.Find(state.winnerId);
            var runnerUp = state.Find(state.runnerUpId);
            var record = new CareerRecord();
            record.seasons.Add(new CareerSeason { sessionId = "a", outcome = "Runner-up", placement = 2, hohWins = 1, vetoWins = 1 });
            foreach (bool larger in new[] { false, true })
            {
                Report().FontScale = larger ? 1.2f : 1f;
                yield return null;
                Report().Show(state, _ => null, null, CareerSummary.Of(record));
                yield return null;
                Canvas.ForceUpdateCanvases();

                // The hero: the two finalists and the jury's verdict, the crown line word for word.
                var hero = ReportPart(SeasonReport.HeroName);
                Assert.That(hero, Is.Not.Null, "The winner and the runner-up lead the screen.");
                var heroWords = LabelsUnder(hero);
                Assert.That(heroWords, Does.Contain("WINNER").And.Contain("RUNNER-UP"));
                Assert.That(heroWords, Does.Contain(champion.name + " beat " + runnerUp.name + " in the jury vote."));
                int forChampion = state.votes.Count(v => v.targetId == champion.id), forRunnerUp = state.votes.Count(v => v.targetId == runnerUp.id);
                Assert.That(heroWords, Does.Contain(forChampion + " jury votes"));
                Assert.That(heroWords, Does.Contain("By a vote of " + forChampion + " to " + forRunnerUp + "."));
                Assert.That(heroWords.Any(text => text.Contains("3 — 2 — 1")), Is.False, "No number the season never had.");
                // Each finalist's quote is their own: the two speeches open on the same sentence, and
                // the runner-up's card quotes their next one rather than the winner's line again.
                string QuoteOn(string card) => hero.GetComponentsInChildren<RectTransform>().Last(rect => rect.name == card)
                    .GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Quote").text;
                string winnerQuote = QuoteOn("WINNER"), runnerUpQuote = QuoteOn("RUNNER-UP");
                Assert.That(runnerUpQuote, Is.Not.EqualTo(winnerQuote), "The winner's and the runner-up's quotes differ.");
                Assert.That(winnerQuote, Does.Contain("I played every week like it was my last."));
                Assert.That(runnerUpQuote, Does.Contain(RunnerUpNextSentence), "The runner-up's next sentence.");

                // The five cards, their captions word for word.
                var cards = ReportPart(SeasonReport.GameSenseCardName);
                Assert.That(cards, Is.Not.Null);
                Assert.That(LabelsUnder(cards), Is.SupersetOf(new[] { "GAME SENSE", "COMPETITIONS", "STRATEGY", "SOCIAL", "CHANCES TAKEN" }));

                // The standings: the winner first, the runner-up second, the one out before the jury last.
                var standings = ReportPart(SeasonReport.StandingsName);
                var order = standings.GetComponentsInChildren<TMP_Text>().Where(t => t.name == "Name").Select(t => t.text).ToList();
                Assert.That(order.Count, Is.EqualTo(state.contestants.Count), "Everybody is in the standings.");
                Assert.That(order[0], Is.EqualTo(champion.name));
                Assert.That(order[1], Is.EqualTo(runnerUp.name));
                Assert.That(order.Last(), Is.EqualTo(state.contestants.Single(c => c.status == ContestantStatus.Evicted).name));
                // One place each, 1 to 8, in the order the jury ledger filled: never a place printed twice.
                var places = standings.GetComponentsInChildren<TMP_Text>().Where(t => t.name == "Place").Select(t => t.text).ToList();
                Assert.That(places, Is.EqualTo(Enumerable.Range(1, state.contestants.Count).Select(n => n.ToString()).ToList()),
                    "The standings read 1 to " + state.contestants.Count + ": " + string.Join(", ", places));

                // The jury's ballots and the weeks, from the ledger: every week's Head of Household.
                Assert.That(LabelsUnder(ReportPart(SeasonReport.JuryColumnName)).Count(t => t == "voted for " + champion.name), Is.EqualTo(forChampion));
                var timeline = ReportPart(SeasonReport.TimelineName);
                var weeks = LabelsUnder(timeline);
                Assert.That(weeks, Does.Contain("HoH: " + champion.name));
                Assert.That(weeks, Does.Contain("HoH: " + runnerUp.name));
                Assert.That(weeks.Any(t => t.StartsWith("Final HoH: " + champion.name) && t.Contains("Part 2: " + runnerUp.name)), Is.True, "The final week is its own.");
                Assert.That(weeks.Any(t => t.Contains("used on " + state.Find(state.ledger.power[1].savedId).name)), Is.True, "The veto and whom it saved.");
                Assert.That(weeks.Any(t => t.Contains("Not recorded")), Is.False, "Every week is in the ledger.");
                // Under each week, the evictee's line (a long name wraps its count) ends above the detail.
                int measured = 0;
                foreach (Transform week in timeline)
                {
                    if (!week.name.StartsWith("Week ")) continue;
                    var gone = week.Find("Evicted") as RectTransform;
                    var detail = week.Find("Detail") as RectTransform;
                    if (gone == null || detail == null) continue;
                    Assert.That(ScreenRect(gone).yMin, Is.GreaterThanOrEqualTo(ScreenRect(detail).yMax - .5f),
                        week.name + ": \"" + gone.GetComponent<TMP_Text>().text + "\" runs into the line under it.");
                    measured++;
                }
                Assert.That(measured, Is.EqualTo(state.week), "Every week has an evictee and a detail line to measure.");

                // COMP WINS on the season's own cards counts the final parts (FinalistRead.Wins); the
                // career's counts what a career season stores, and says so under its pinned caption.
                var career = ReportPart(SeasonReport.CareerStripName);
                Assert.That(career, Is.Not.Null, "The career under the season.");
                var seasonCounts = Report().GetComponentsInChildren<RectTransform>()
                    .Where(rect => rect.name == "COMP WINS" && rect.gameObject.activeInHierarchy && !rect.IsChildOf(career))
                    .Select(rect => rect.Find("Value").GetComponent<TMP_Text>().text).ToList();
                Assert.That(seasonCounts, Is.EqualTo(new[] { FinalistRead.Wins(state, champion).ToString(),
                    FinalistRead.Wins(state, state.Find(state.playerId)).ToString() }), "The champion's road, then your season.");
                Assert.That(FinalistRead.Wins(state, champion), Is.EqualTo(champion.hohWins + champion.vetoWins + 1), "Part 1 is a win.");
                var careerCell = career.GetComponentsInChildren<RectTransform>().Last(rect => rect.name == "COMP WINS");
                var careerCaption = careerCell.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Caption");
                var careerNote = careerCell.GetComponentsInChildren<TMP_Text>().Single(text => text.name == SeasonReport.CareerCaptionNoteName);
                Assert.That(careerCaption.text, Is.EqualTo("COMP WINS"), "The caption stays word for word.");
                Assert.That(careerNote.text, Is.EqualTo(SeasonReport.CareerCompWinsNote));
                Assert.That(ScreenRect(careerNote.rectTransform).yMax, Is.LessThanOrEqualTo(ScreenRect(careerCaption.rectTransform).yMin + .5f), "Under the caption.");
                Assert.That(ScreenRect(careerNote.rectTransform).yMin, Is.GreaterThanOrEqualTo(ScreenRect(careerCell).yMin - .5f), "Inside its cell.");

                foreach (var part in new[] { SeasonReport.StandingsName, SeasonReport.JuryColumnName, SeasonReport.TimelineName, SeasonReport.CareerStripName })
                    AssertDecisionCopyFits(ReportPart(part));
                if (Application.isBatchMode) yield return CaptureFraming(larger ? "season-complete-large" : "season-complete");
                Report().Hide();
            }
            Report().FontScale = 1f;
            yield return null;

            // Without the jury ledger (an older save) the placement falls back to a count that seats
            // every juror alike; the week each left breaks the tie, and the standings read the same.
            var coarse = FinishedWithWeeks();
            coarse.jurySentiment = WebJurySentiment.CreateInitial();
            Assert.That(SeasonReport.StandingsOrder(coarse).Select(entry => entry.who.id + " " + entry.place),
                Is.EqualTo(SeasonReport.StandingsOrder(state).Select(entry => entry.who.id + " " + entry.place)),
                "The fallback's tie is broken by the week each juror left.");

            // The tabs scroll to the detail, and back; they hide nothing and change nothing.
            Report().Show(state, _ => null, null, CareerSummary.Of(record));
            yield return null;
            var scroll = Report().GetComponentsInChildren<ScrollRect>().First();
            string before = JsonUtility.ToJson(state);
            ReportButtons(SeasonReport.DetailTabCaption).Single().onClick.Invoke();
            yield return null;
            Assert.That(scroll.content.anchoredPosition.y, Is.GreaterThan(100f), "The detail is below the dashboard.");
            ReportButtons(SeasonReport.HouseTabCaption).Single().onClick.Invoke();
            yield return null;
            Assert.That(ReportButtons(SeasonReport.SortCaption(SeasonReport.CastSort.Name)), Has.Length.EqualTo(1), "The table's chips are there.");
            ReportButtons(SeasonReport.OverviewTabCaption).Single().onClick.Invoke();
            yield return null;
            Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(0f).Within(.5f));
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before), "Reading the season changes nothing.");
            Report().Hide();
        }

        [UnityTest]
        public IEnumerator EndScreens_ATiedJuryIsCalledATieAndTheCrownLineStays()
        {
            var state = Finished();
            var champion = state.Find(state.winnerId);
            var runnerUp = state.Find(state.runnerUpId);
            // Half the ballots each way: the house's tie rule decided it.
            var ballots = state.votes.Where(v => v.targetId == champion.id || v.targetId == runnerUp.id).ToList();
            for (int i = 0; i < ballots.Count; i++) ballots[i].targetId = i % 2 == 0 ? champion.id : runnerUp.id;
            if (ballots.Count % 2 == 1) state.votes.Remove(ballots.Last());
            int each = state.votes.Count(v => v.targetId == champion.id);
            Assert.That(state.votes.Count(v => v.targetId == runnerUp.id), Is.EqualTo(each));
            Report().Show(state, _ => null, null);
            yield return null;
            var words = LabelsUnder(ReportPart(SeasonReport.HeroName));
            Assert.That(words, Does.Contain("The jury is tied, " + each + " to " + each + "."));
            Assert.That(words, Does.Contain("Under the house's tie rule, the win goes to " + champion.name + "."));
            Assert.That(words, Does.Contain(champion.name + " beat " + runnerUp.name + " in the jury vote."));
            Assert.That(words.Any(t => t.StartsWith("By a vote of")), Is.False, "A tie is not a count.");
            Report().Hide();
        }

        /// <summary>A season played to its first eviction, as a recap would close it.</summary>
        private static EpisodeState FirstWeekClosed()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(20260929u));
            for (int guard = 0; guard < 400; guard++)
            {
                var state = engine.Snapshot;
                if (state.evictionResolved && state.events.Any(e => e.week == state.week && e.kind == "eviction")) return state;
                Assert.That(engine.Apply(NextCommand(state)).accepted, Is.True);
            }
            Assert.Fail("The season never reached its first eviction.");
            return null;
        }

        [UnityTest]
        public IEnumerator EndScreens_TheWeekRecapShowsWhoLeftAndEachTabKeepsOneContinue()
        {
            var screen = director.GetComponentInChildren<WeeklyRecapScreen>(true);
            var state = FirstWeekClosed();
            var recap = WeeklyRecap.Build(state, state.week);
            var gone = state.Find(recap.evictedId);
            Assert.That(gone, Is.Not.Null, "The week closed with an eviction.");
            string before = JsonUtility.ToJson(state);
            foreach (bool larger in new[] { false, true })
            {
                screen.FontScale = larger ? 1.2f : 1f;
                yield return null;
                screen.Show(state, () => { });
                yield return Frames(2);
                Canvas.ForceUpdateCanvases();
                var hero = LastActive(WeeklyRecapScreen.HeroName);
                Assert.That(hero, Is.Not.Null);
                var heroWords = LabelsUnder(hero);
                Assert.That(heroWords, Does.Contain(HudPrimitives.WithYou(gone.name, gone.isPlayer)));
                Assert.That(heroWords.Any(t => t.Contains(WeeklyRecap.PlaceWords(recap.placement).ToUpperInvariant())), Is.True, "Where they finished.");
                Assert.That(heroWords, Does.Contain("EVICTED"));
                if (recap.evicteeQuote != null)
                    Assert.That(heroWords.Any(t => t.Contains("from the block on eviction night")), Is.True, "Their words are said where they were said.");
                var cards = LastActive(WeeklyRecapScreen.CardsName);
                Assert.That(LabelsUnder(cards), Is.SupersetOf(new[] { "HOH", "NOMINATED", "VETO USED", "EVICTED", "REMAINING" }));
                Assert.That(screen.Lines, Does.Contain("Evicted: " + recap.evicted));

                for (int tab = 0; tab < WeeklyRecapScreen.TabCaptions.Length; tab++)
                {
                    ButtonWithCaption(WeeklyRecapScreen.TabCaptions[tab]).onClick.Invoke();
                    yield return Frames(2);
                    Canvas.ForceUpdateCanvases();
                    Assert.That(screen.OpenTab, Is.EqualTo(tab));
                    Assert.That(ButtonWithCaption(WeeklyRecapScreen.ContinueCaption), Is.Not.Null, "One Continue, whichever tab is open.");
                    Assert.That(LastActive(WeeklyRecapScreen.TabBodyName), Is.Not.Null);
                    AssertDecisionCopyFits(LastActive(WeeklyRecapScreen.TabBodyName));
                    if (tab == 0 && Application.isBatchMode) yield return CaptureFraming(larger ? "weekly-recap-overview-large" : "weekly-recap-overview");
                }
                screen.Hide();
                yield return null;
            }
            screen.FontScale = 1f;
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before), "Reading the week changes nothing.");
        }
    }
}
