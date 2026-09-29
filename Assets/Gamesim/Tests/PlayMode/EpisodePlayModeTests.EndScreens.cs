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

        /// <summary>A finished season with a week-by-week record in the ledger: three weeks, the last the final eviction.</summary>
        private static EpisodeState FinishedWithWeeks()
        {
            var state = Finished();
            var cast = state.contestants;
            var champion = state.Find(state.winnerId);
            var runnerUp = state.Find(state.runnerUpId);
            var jury = cast.Where(c => c.status == ContestantStatus.Jury && !c.isPlayer).ToList();
            state.week = 3;
            state.ledger.power.Add(new PowerRow { week = 1, hohId = champion.id, vetoHolderId = runnerUp.id, vetoUsed = false,
                nominees = new List<string> { jury[0].id, jury[1].id }, evicteeId = jury[0].id, tally = new List<int> { 4, 1 } });
            state.ledger.power.Add(new PowerRow { week = 2, hohId = runnerUp.id, vetoHolderId = runnerUp.id, vetoUsed = true, savedId = jury[2].id,
                replacementId = jury[1].id, nominees = new List<string> { jury[1].id, jury[3].id }, evicteeId = jury[1].id, tally = new List<int> { 3, 1 } });
            state.ledger.power.Add(new PowerRow { week = 3, hohId = champion.id, nominees = new List<string> { runnerUp.id, jury[3].id }, evicteeId = jury[3].id });
            state.finalPart1WinnerId = champion.id;
            state.finalPart2WinnerId = runnerUp.id;
            return state;
        }

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

                // The jury's ballots and the weeks, from the ledger: every week's Head of Household.
                Assert.That(LabelsUnder(ReportPart(SeasonReport.JuryColumnName)).Count(t => t == "voted for " + champion.name), Is.EqualTo(forChampion));
                var weeks = LabelsUnder(ReportPart(SeasonReport.TimelineName));
                Assert.That(weeks, Does.Contain("HoH: " + champion.name));
                Assert.That(weeks, Does.Contain("HoH: " + runnerUp.name));
                Assert.That(weeks.Any(t => t.StartsWith("Final HoH: " + champion.name) && t.Contains("Part 2: " + runnerUp.name)), Is.True, "The final week is its own.");
                Assert.That(weeks.Any(t => t.Contains("used on " + state.Find(state.ledger.power[1].savedId).name)), Is.True, "The veto and whom it saved.");
                Assert.That(weeks.Any(t => t.Contains("Not recorded")), Is.False, "Every week is in the ledger.");
                Assert.That(ReportPart(SeasonReport.CareerStripName), Is.Not.Null, "The career under the season.");

                foreach (var part in new[] { SeasonReport.StandingsName, SeasonReport.JuryColumnName, SeasonReport.TimelineName, SeasonReport.CareerStripName })
                    AssertDecisionCopyFits(ReportPart(part));
                if (Application.isBatchMode) yield return CaptureFraming(larger ? "season-complete-large" : "season-complete");
                Report().Hide();
            }
            Report().FontScale = 1f;
            yield return null;

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
