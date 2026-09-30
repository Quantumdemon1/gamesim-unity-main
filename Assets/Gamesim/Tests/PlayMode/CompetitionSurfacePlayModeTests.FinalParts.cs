using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// MOCKUP-PASS-PLAN M13: a final Head of Household part on the game screen. The challenge card
    /// carries the part tracker and how every part is scored, the legend row the gold band, and a
    /// field of two is two cards whose names are the field list's own lines. An ordinary week's
    /// competition has none of it.
    /// </summary>
    public sealed partial class CompetitionSurfacePlayModeTests
    {
        /// <summary>Part 2 of a Final 3: the first of the others won Part 1; the player and the second play.</summary>
        private static EpisodeState FinalPartTwo()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 6 }, 41);
            foreach (var juror in s.contestants.Where(c => !c.isPlayer).Take(3)) juror.status = ContestantStatus.Jury;
            s.phase = EpisodePhase.FinalHoHPart2;
            s.competitionResolved = false;
            s.finalPart1WinnerId = s.Active.First(c => !c.isPlayer).id;
            return s;
        }

        /// <summary>A label draws every character it holds and stays in its box.</summary>
        private static void AssertDrawsWhole(TMP_Text label, string where)
        {
            label.ForceMeshUpdate(true);
            var info = label.textInfo;
            int held = 0, drawn = 0;
            for (int i = 0; i < info.characterCount; i++)
            {
                if (char.IsWhiteSpace(info.characterInfo[i].character)) continue;
                held++;
                if (info.characterInfo[i].isVisible) drawn++;
            }
            Assert.That(drawn, Is.EqualTo(held), where + ": '" + label.text + "' (" + label.name + ") loses characters.");
            Assert.That(label.isTextOverflowing, Is.False, where + ": '" + label.text + "' (" + label.name + ") runs out of its box.");
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return new Rect(corners[0].x, corners[0].y, corners[2].x - corners[0].x, corners[2].y - corners[0].y);
        }

        [UnityTest]
        public IEnumerator FinalPart_TheChallengeCardCarriesTheBracketAndThePairAreCards()
        {
            owner = new GameObject("Final part test"); screen = CompetitionGameScreen.Attach(owner);
            var state = FinalPartTwo();
            var bracket = FinalBracket.For(state);
            var field = EpisodeEngine.CompetitionPlayers(state).ToList();
            Assert.That(field, Has.Count.EqualTo(2));
            var entrants = field.Select(c => new CompetitionEntrant(c.id, c.name, c.isPlayer, null, c)).ToList();
            string listed = string.Join("\n", field.Select(c => HudPrimitives.WithYou(c.name, c.isPlayer)));
            foreach (float scale in new[] { 1f, 1.2f })
            {
                screen.FontScale = scale;
                var run = new MiniGameRun(CompetitionMiniGames.Kind.Memory, 5, 3, CompetitionDefinitions.HouseMemory);
                screen.Show(run, "Final HoH, Part 2 of 3 · " + run.Definition.Title, listed, true, i => {}, () => {}, d => {}, () => {}, () => {},
                    entrants: entrants, bracket: bracket);
                yield return null;
                Canvas.ForceUpdateCanvases();
                string at = " at " + scale;

                // The tracker, in the challenge card under the game's name, and the scoring line over the policy.
                var card = Rect("Competition challenge");
                var strip = Rect(FinalBracketView.BracketName);
                Assert.That(strip.IsChildOf(card), Is.True, "The tracker is in the challenge card" + at + ".");
                Assert.That(strip.Cast<Transform>().Count(child => child.name.StartsWith("Bracket part ", System.StringComparison.Ordinal)), Is.EqualTo(3));
                Assert.That(strip.GetComponentsInChildren<TMP_Text>().Where(text => text.name == FinalBracketView.PartLineName).Select(text => text.text),
                    Is.EqualTo(bracket.parts.Select(part => part.line)), "The tracker says the bracket's own lines" + at + ".");
                var scoring = Text("Scoring rule");
                Assert.That(scoring.text, Is.EqualTo(FinalBracket.ScoringLine));
                var rules = Text("Rules");
                Assert.That(rules.text, Does.Contain(run.Definition.Summary), "The game's own rules stay" + at + ".");
                float Top(RectTransform rect) => -rect.anchoredPosition.y;
                float Bottom(RectTransform rect) => -rect.anchoredPosition.y + rect.rect.height;
                Assert.That(Top(rules.rectTransform), Is.GreaterThanOrEqualTo(Bottom(strip) - .5f), "The rules start under the tracker" + at + ".");
                Assert.That(Bottom(rules.rectTransform), Is.LessThanOrEqualTo(Top(scoring.rectTransform) + .5f), "and end over the scoring line" + at + ".");
                Assert.That(Bottom(scoring.rectTransform), Is.LessThanOrEqualTo(Top(Text("Attempt policy").rectTransform) + .5f));
                Assert.That(Bottom(Text("Attempt policy").rectTransform), Is.LessThanOrEqualTo(card.rect.height + .5f), "All of it inside the card" + at + ".");
                foreach (var label in new[] { rules, scoring, Text("Attempt policy") }.Concat(strip.GetComponentsInChildren<TMP_Text>()))
                    AssertDrawsWhole(label, "The challenge card" + at);

                // The band at the legend row's end, clear of the keys and inside the board's width.
                var band = Rect(FinalBracketView.BandName);
                Assert.That(Text(FinalBracketView.BandLineName).text, Is.EqualTo(bracket.advance));
                var legend = Rect("Control legend");
                var surface = Rect("Game surface");
                Assert.That(band.anchoredPosition.x, Is.GreaterThanOrEqualTo(legend.anchoredPosition.x + legend.rect.width - .5f), "The keys keep their own room" + at + ".");
                Assert.That(band.anchoredPosition.x + band.rect.width, Is.LessThanOrEqualTo(surface.anchoredPosition.x + surface.rect.width + .5f));
                Assert.That(band.anchoredPosition.y, Is.EqualTo(legend.anchoredPosition.y).Within(.5f), "On the legend's row" + at + ".");
                AssertDrawsWhole(Text(FinalBracketView.BandLineName), "The band" + at);

                // Two in the field, two cards: each a photo, the line saying they play, and a name
                // that is the field list's own line.
                var cards = screen.GetComponentsInChildren<RectTransform>().Where(rect => rect.name == "Competitor card").ToArray();
                Assert.That(cards, Has.Length.EqualTo(2), "Two in the field, two cards" + at + ".");
                var list = Text("Competition field");
                Assert.That(list.text, Is.EqualTo(listed), "The field's list keeps its words" + at + ".");
                list.ForceMeshUpdate(true);
                Assert.That(list.textInfo.lineCount, Is.EqualTo(2));
                var fieldCard = WorldRect(Rect("Competition field card"));
                var status = WorldRect(Text("Arena status").rectTransform);
                for (int i = 0; i < 2; i++)
                {
                    var own = WorldRect(cards[i]);
                    Assert.That(fieldCard.Contains(own.min) && fieldCard.Contains(own.max), Is.True, "Card " + i + " sits in the field card" + at + ".");
                    var row = list.textInfo.lineInfo[i];
                    var name = list.rectTransform.TransformPoint(new Vector3(4f, (row.ascender + row.descender) * .5f, 0f));
                    Assert.That(own.Contains(new Vector2(name.x, name.y)), Is.True, "Line " + i + " ('" + listed.Split('\n')[i] + "') is its card's name" + at + ".");
                    var photo = cards[i].GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Competitor photo");
                    Assert.That(photo.rect.height, Is.GreaterThanOrEqualTo(48f));
                    Assert.That(WorldRect(photo).yMin, Is.GreaterThan(name.y), "The name is under the photo" + at + ".");
                    var eyebrow = cards[i].GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Competitor eyebrow");
                    Assert.That(eyebrow.text, Is.EqualTo("IN THE COMPETITION"));
                    foreach (var label in cards[i].GetComponentsInChildren<TMP_Text>()) AssertDrawsWhole(label, "Card " + i + at);
                    foreach (var chip in cards[i].GetComponentsInChildren<RectTransform>().Where(rect => rect.name == "Trait"))
                        Assert.That(WorldRect(chip).xMax, Is.LessThanOrEqualTo(own.xMax + .5f), "A trait stays on its card" + at + ".");
                }
                Assert.That(WorldRect(cards[0]).Overlaps(WorldRect(cards[1])), Is.False, "The cards stand apart" + at + ".");
                Assert.That(WorldRect(cards[1]).yMin, Is.GreaterThanOrEqualTo(status.yMax - .5f), "and clear of the arena's status" + at + ".");
                AssertDrawsWhole(list, "The field list" + at);
                // Only the player's own progress is drawn: nothing on a card says how anybody is doing.
                Assert.That(screen.GetComponentsInChildren<TMP_Text>().Count(text => text.name == "Progress"), Is.EqualTo(1));
            }

            // An ordinary week's competition has none of it.
            screen.FontScale = 1f;
            var plain = new MiniGameRun(CompetitionMiniGames.Kind.Memory, 5, 3, CompetitionDefinitions.HouseMemory);
            screen.Show(plain, "Head of Household · " + plain.Definition.Title, "Player\nMaya\nJo", true, i => {}, () => {}, d => {}, () => {}, () => {});
            yield return null;
            Assert.That(screen.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == FinalBracketView.BracketName
                || rect.name == FinalBracketView.BandName || rect.name == "Competitor card"), Is.False);
            Assert.That(screen.GetComponentsInChildren<TMP_Text>().Any(label => label.name == "Scoring rule"), Is.False);
        }
    }
}
