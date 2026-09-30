using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// What every final part's briefing carries (MOCKUP-PASS-PLAN M13): the part tracker at the
        /// head of the sheet, over the hero, saying the bracket's own lines; the gold band at its
        /// foot, under every way on, saying what the part's winner goes on to; and the hero's stakes
        /// still the explanation. Every word of the tracker and the band is drawn whole.
        /// </summary>
        private void AssertTheSheetCarriesItsBracketAndBand(EpisodeState state, FinalBracket bracket)
        {
            Assert.That(bracket, Is.Not.Null, state.phase + " is a final part.");
            var content = ActiveRect("Episode content");
            var row = ActiveRect(FinalBracketView.BracketName);
            Assert.That(row, Is.Not.Null, "A final part's sheet carries the part tracker.");
            Assert.That(row.parent, Is.SameAs(content));
            var hero = ActiveRect(EpisodeHud.BriefingHeroName);
            Assert.That(hero, Is.Not.Null, "The hero stays.");
            Assert.That(row.GetSiblingIndex(), Is.LessThan(hero.GetSiblingIndex()), "The tracker heads the sheet, over the hero.");
            for (int number = 1; number <= 3; number++)
                Assert.That(row.Find(FinalBracketView.PartName(number)), Is.Not.Null, "Part " + number + " has its cell.");
            var lines = row.GetComponentsInChildren<TMP_Text>().Where(text => text.name == FinalBracketView.PartLineName).Select(text => text.text).ToArray();
            Assert.That(lines, Is.EqualTo(bracket.parts.Select(part => part.line).ToArray()), "The tracker says the bracket's own lines.");
            int current = FinalBracket.PartOf(state.phase);
            Assert.That(bracket.parts[current - 1].standing, Is.EqualTo(FinalBracket.Standing.Playing));
            if (current >= 2)
                Assert.That(lines[0], Is.EqualTo("Winner: " + FinalBracket.Name(state, state.Find(state.finalPart1WinnerId))), "Part 1 is played, and says who won it.");

            var band = ActiveRect(FinalBracketView.BandName);
            Assert.That(band, Is.Not.Null, "A final part's sheet ends in the gold band.");
            Assert.That(band.parent, Is.SameAs(content));
            Assert.That(band.GetComponentsInChildren<TMP_Text>().Single(text => text.name == FinalBracketView.BandLineName).text,
                Is.EqualTo(FinalBracket.AdvanceLine(state.phase)));
            int lastControl = content.Cast<Transform>().Where(child => child.GetComponentInChildren<Button>() != null)
                .Select(child => child.GetSiblingIndex()).DefaultIfEmpty(-1).Max();
            Assert.That(band.GetSiblingIndex(), Is.GreaterThan(lastControl), "The band is the sheet's foot, under every way on.");
            Assert.That(HeroText(hero), Does.Contain(EpisodeDirector.FinalPartStakes(state)), "The stakes line is still the explanation.");
            foreach (var text in row.GetComponentsInChildren<TMP_Text>().Concat(band.GetComponentsInChildren<TMP_Text>()))
                Assert.That(ShowsAllOf(text), Is.True, "'" + text.text + "' (" + text.name + ") is cut short.");
        }

        /// <summary>The sheet's new pieces fit at both text sizes; it is left open at the resting size.</summary>
        private IEnumerator AssertTheFinalPartSheetFitsAtBothTextSizes()
        {
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                yield return OpenFinalePanel();
                Canvas.ForceUpdateCanvases();
                foreach (var name in new[] { FinalBracketView.BracketName, FinalBracketView.BandName, EpisodeHud.CompetitorCardsName, EpisodeHud.BriefingHeroName })
                {
                    var rect = ActiveRect(name);
                    if (rect == null) continue;
                    foreach (var text in rect.GetComponentsInChildren<TMP_Text>())
                        Assert.That(ShowsAllOf(text), Is.True,
                            "At " + (larger ? "larger" : "standard") + " text '" + text.text + "' (" + text.name + " in " + name + ") is cut short.");
                }
            }
            yield return ApplyTextSize(false);
            yield return OpenFinalePanel();
        }

        /// <summary>
        /// MOCKUP-PASS-PLAN M13, mockup 58: a final part the player plays. The briefing heads its
        /// sheet with the part tracker and ends in the gold band; the hero keeps the player's face.
        /// On the game screen the tracker and how every part is scored stand in the challenge card,
        /// the band on the legend row, and the pair down the field as cards - a blue and a red - whose
        /// names are the field list's own lines. Only the player's own progress is drawn. The part
        /// then resolves through its own control, with one live way on from its standings.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_AFinalPartThePlayerPlaysCarriesItsBracketOnTheSheetAndTheGameScreen()
        {
            HoldTheHouseForTheFixture();
            yield return InstallDiaryFixture(state => EpisodeDirector.IsFinalHoHPart(state.phase) && !state.competitionResolved
                    && state.pendingDiary == null && EpisodeEngine.CompetitionPlayers(state).Count() == 2
                    && EpisodeEngine.CompetitionPlayers(state).Any(actor => actor.isPlayer),
                "an unresolved final part the player plays against one other");
            yield return PutAwayTheCards();
            director.BuildNpcWorldForDiagnostics();
            yield return OpenFinalePanel();
            Canvas.ForceUpdateCanvases();
            var state = director.Snapshot;
            var bracket = FinalBracket.For(state);
            AssertTheSheetCarriesItsBracketAndBand(state, bracket);
            var hero = ActiveRect(EpisodeHud.BriefingHeroName);
            Assert.That(hero.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "Hero face"), Is.True,
                "The player's own briefing keeps their face in the hero.");
            Assert.That(ActiveRect(EpisodeHud.CompetitorCardsName), Is.Null, "The pair's cards are the game screen's and the watcher's.");
            AssertEveryLabelDraws("A final part's briefing");
            if (Application.isBatchMode) yield return CaptureFraming("final-hoh-part-briefing");
            yield return AssertTheFinalPartSheetFitsAtBothTextSizes();

            ButtonWithCaption("Practice this competition").onClick.Invoke();
            yield return null;
            var screen = SceneComponents<CompetitionGameScreen>().Single();
            Assert.That(screen.IsShowing, Is.True, "The practice starts: " + director.StatusMessage);
            if (screen.IsAssembling)
                screen.GetComponentsInChildren<Button>().First(button => button.name == "Continue to competition").onClick.Invoke();
            float playing = Time.realtimeSinceStartup + 3.6f;
            while (Time.realtimeSinceStartup < playing && screen.IsShowing) yield return null;
            Assert.That(screen.IsShowing, Is.True, "The practice should still be in play.");
            yield return null;
            Canvas.ForceUpdateCanvases();

            var rects = screen.GetComponentsInChildren<RectTransform>();
            var texts = screen.GetComponentsInChildren<TMP_Text>();
            var challenge = rects.Single(rect => rect.name == "Competition challenge");
            var strip = rects.Single(rect => rect.name == FinalBracketView.BracketName);
            Assert.That(strip.IsChildOf(challenge), Is.True, "The tracker is in the challenge card.");
            Assert.That(strip.GetComponentsInChildren<TMP_Text>().Where(text => text.name == FinalBracketView.PartLineName).Select(text => text.text),
                Is.EqualTo(bracket.parts.Select(part => part.line)));
            Assert.That(texts.Single(text => text.name == "Scoring rule").text, Is.EqualTo(FinalBracket.ScoringLine));
            Assert.That(texts.Single(text => text.name == FinalBracketView.BandLineName).text, Is.EqualTo(bracket.advance));
            var cards = rects.Where(rect => rect.name == "Competitor card").ToArray();
            Assert.That(cards, Has.Length.EqualTo(2), "Two in the field, two cards.");
            string listed = texts.Single(text => text.name == "Competition field").text;
            foreach (var actor in EpisodeEngine.CompetitionPlayers(state))
                Assert.That(listed, Does.Contain(HudPrimitives.WithYou(actor.name, actor.isPlayer)), "The field's list keeps its words: '" + listed + "'.");
            Assert.That(texts.Where(text => text.name == "Competitor eyebrow").Select(text => text.text),
                Is.EqualTo(new[] { "IN THE COMPETITION", "IN THE COMPETITION" }));
            Assert.That(texts.Count(text => text.name == "Progress"), Is.EqualTo(1), "Only the player's own progress is drawn.");
            foreach (var text in strip.GetComponentsInChildren<TMP_Text>().Concat(cards.SelectMany(card => card.GetComponentsInChildren<TMP_Text>()))
                .Concat(texts.Where(text => text.name == "Scoring rule" || text.name == FinalBracketView.BandLineName)))
                Assert.That(ShowsAllOf(text), Is.True, "On the game screen '" + text.text + "' (" + text.name + ") is cut short.");
            if (Application.isBatchMode && screen.IsShowing) yield return CaptureFraming("final-hoh-part-game");
            Assert.That(director.Snapshot.revision, Is.EqualTo(state.revision), "A practice commits nothing.");

            // Back to the briefing, and the part resolves through its own control as ever.
            screen.GetComponentsInChildren<Button>().Single(button => button.name == "Cancel attempt").onClick.Invoke();
            yield return Frames(2);
            Assert.That(screen.IsShowing, Is.False);
            yield return ResolveFinalPart(card => Assert.That(card.GetComponentsInChildren<Button>()
                .Count(button => button.name == "Continue from competition results" && button.IsActive()), Is.EqualTo(1),
                "One live way on from the part's standings."));
        }

        /// <summary>
        /// MOCKUP-PASS-PLAN M13, mockup 58: a final part the player sits out. The sheet carries the
        /// tracker and the band; the hero wears the part's art rather than the watcher's own face;
        /// the pair stand under it as cards, a blue and a red, each named and "IN THE COMPETITION";
        /// the 'Competing:' paragraph stays, and so does the one live way to watch.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_AFinalPartThePlayerWatchesPutsThePairUnderThePartsArt()
        {
            HoldTheHouseForTheFixture();
            yield return InstallDiaryFixture(state => EpisodeDirector.IsFinalHoHPart(state.phase) && !state.competitionResolved
                    && state.pendingDiary == null && state.Find(state.playerId).status == ContestantStatus.Active
                    && EpisodeEngine.CompetitionPlayers(state).Count() == 2
                    && !EpisodeEngine.CompetitionPlayers(state).Any(actor => actor.isPlayer),
                "an unresolved final part the player sits out");
            yield return PutAwayTheCards();
            director.BuildNpcWorldForDiagnostics();
            yield return OpenFinalePanel();
            Canvas.ForceUpdateCanvases();
            var state = director.Snapshot;
            AssertTheSheetCarriesItsBracketAndBand(state, FinalBracket.For(state));

            var hero = ActiveRect(EpisodeHud.BriefingHeroName);
            var heroParts = hero.GetComponentsInChildren<RectTransform>().Select(rect => rect.name).ToArray();
            Assert.That(heroParts, Does.Not.Contain("Hero face"), "The hero wears the part's art, not the watcher's own face.");
            Assert.That(heroParts, Does.Contain("Part art glow"));
            Assert.That(HeroText(hero), Does.Contain("PART " + FinalBracket.PartOf(state.phase)));

            var row = ActiveRect(EpisodeHud.CompetitorCardsName);
            Assert.That(row, Is.Not.Null, "The pair stand under the hero.");
            Assert.That(row.GetSiblingIndex(), Is.EqualTo(hero.GetSiblingIndex() + 1));
            var pair = row.Cast<Transform>().Where(child => child.name == "Competitor card").Cast<RectTransform>().ToArray();
            Assert.That(pair, Has.Length.EqualTo(2));
            var field = EpisodeEngine.CompetitionPlayers(state).ToArray();
            for (int i = 0; i < 2; i++)
            {
                var words = pair[i].GetComponentsInChildren<TMP_Text>().Select(text => text.text).ToArray();
                Assert.That(words, Does.Contain(field[i].name), "Card " + i + " names who plays.");
                Assert.That(words, Does.Contain(EpisodeHud.InTheCompetition));
                Assert.That(pair[i].GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "Competitor photo"), Is.True);
            }
            Assert.That(ScreenRect(pair[0]).Overlaps(ScreenRect(pair[1])), Is.False, "Side by side, apart.");
            Assert.That(ActiveRect("Episode content").GetComponentsInChildren<TMP_Text>().Any(text => text.text.StartsWith("Competing: ", System.StringComparison.Ordinal)),
                Is.True, "The 'Competing:' paragraph stays.");
            Assert.That(FindButton("Watch eligible housemates compete").IsInteractable(), Is.True, "One live way to watch.");
            AssertEveryLabelDraws("A final part's watcher's briefing");
            if (Application.isBatchMode) yield return CaptureFraming("final-hoh-part-watching");
            yield return AssertTheFinalPartSheetFitsAtBothTextSizes();

            yield return ResolveFinalPart(card => Assert.That(card.GetComponentsInChildren<Button>()
                .Count(button => button.name == "Continue from competition results" && button.IsActive()), Is.EqualTo(1),
                "One live way on from the part's standings."));
        }
    }
}
