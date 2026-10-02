using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Episode;
using Gamesim.Presentation;
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
        /// A competition shows who won and the order; nobody in the house ever sees a 6.42
        /// (UI-UX-PASS-PLAN C0, decision 12). Through the director: the result card a commit opens,
        /// its Score details in the player's words, and the results phase's FINAL STANDINGS as
        /// rows with a face, a name and the crown on the winner - at both text sizes, with the
        /// larger size's captures.
        /// </summary>
        [UnityTest]
        public IEnumerator CompetitionNumbers_TheCardTheDetailsAndTheStandingsSayTheOrderAndNobodysScore()
        {
            yield return SettleCast();
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Accessible alternative: steady 1-point bonus").onClick.Invoke();
            yield return null; yield return null;
            var card = SceneComponents<CompetitionResult>().Single(result => result.IsPlaying);
            yield return AssertTheCardSaysNoScore(card, "standard");
            yield return DismissTheCard(card);
            AssertTheStandingsAreRows("standard");

            director.ClosePanels();
            yield return null;
            yield return ApplyTextSize(true);
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            AssertTheStandingsAreRows("larger");
            if (Application.isBatchMode) yield return CaptureFraming("phase-results-large");
            ButtonWithCaption("Review competition results").onClick.Invoke();
            yield return null; yield return null;
            card = SceneComponents<CompetitionResult>().Single(result => result.IsPlaying);
            Assert.That(card.FontScale, Is.EqualTo(1.2f), "The card honours the larger text.");
            yield return AssertTheCardSaysNoScore(card, "larger");
            if (Application.isBatchMode) yield return CaptureFraming("competition-result-large");
            yield return DismissTheCard(card);
            director.ClosePanels();
            yield return null;
            yield return ApplyTextSize(false);
        }

        /// <summary>
        /// The briefing says what each way in does for the player, in the player's words
        /// (CompetitionWords): no "seeded rolls", no "weighted statistics", no "LEGACY RULES", no
        /// preparation count in the engine's form. The captions keep their words: they are the
        /// controls' contracts, and the description is a line of its own under each.
        /// </summary>
        [UnityTest]
        public IEnumerator CompetitionNumbers_TheBriefingSpeaksThePlayersWords()
        {
            yield return SettleCast();
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null;
            ButtonWithCaption(EpisodeHud.ViewRulesCaption).onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var modal = ActiveRect("Episode panel");
            var words = PanelWords(modal);
            foreach (string jargon in new[] { "seeded", "Weighted statistics", "weighted stats", "LEGACY RULES", "/5", "Zero performance bonus" })
                Assert.That(words.FirstOrDefault(word => word.Contains(jargon)), Is.Null, "The briefing says '" + jargon + "'.");
            Assert.That(words.Any(word => word.StartsWith("Performance adds") && word.Contains("Who you are and how the day goes decide the rest")), Is.True,
                "The full rules say what performance is worth in the player's words.");

            int rules = director.Snapshot.competitionRulesVersion;
            var simulate = ButtonWithCaption(EpisodeHud.SimulateCompetitionCaption);
            var simulateWords = simulate.GetComponentsInChildren<TMP_Text>().Select(text => text.text).ToArray();
            Assert.That(simulateWords, Does.Contain(EpisodeHud.SimulateCompetitionCaption), "The caption is the contract.");
            Assert.That(simulateWords, Does.Contain(CompetitionWords.SimulateDescription(rules)), "and what it does for the player is said under it.");
            Assert.That(CompetitionWords.SimulateDescription(rules), Does.StartWith("Let the day decide"));
            var throwCard = ButtonWithCaption(EpisodeHud.ThrowCompetitionCaption);
            var throwWords = throwCard.GetComponentsInChildren<TMP_Text>().Select(text => text.text).ToArray();
            Assert.That(throwWords, Does.Contain(EpisodeHud.ThrowCompetitionCaption), "The caption is the contract.");
            Assert.That(throwWords, Does.Contain(CompetitionWords.ThrowDescription(rules)));
            Assert.That(CompetitionWords.ThrowDescription(rules), Does.StartWith("You compete to lose"));
            director.ClosePanels();
            yield return null;
        }

        /// <summary>Continue, pressed on the card itself, once its opening grace has passed.</summary>
        private IEnumerator DismissTheCard(CompetitionResult card)
        {
            yield return new WaitForSecondsRealtime(.3f);
            card.GetComponentsInChildren<Button>().Single(button => button.name == "Continue from competition results").onClick.Invoke();
            yield return null; yield return null;
            Assert.That(card.IsPlaying, Is.False, "Continue dismisses the card.");
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// The card the director played: ranks, faces and names, the winner's crown, the one-line
        /// foot, and no decimal anywhere but the player's own attempt; then the Score details page,
        /// which is the words read from the committed state and no number at all.
        /// </summary>
        private IEnumerator AssertTheCardSaysNoScore(CompetitionResult card, string at)
        {
            Canvas.ForceUpdateCanvases();
            var state = director.Snapshot;
            Assert.That(state.competitionResolved, Is.True);
            SweepTheCard(card, at + " text, the standings");
            var texts = card.GetComponentsInChildren<TMP_Text>();
            Assert.That(texts.Single(text => text.name == "Standings heading").text, Is.EqualTo(CompetitionWords.StandingsHeading), at);
            Assert.That(texts.Single(text => text.name == "Performance explanation").text, Is.EqualTo(CompetitionWords.ResultFooter), at);
            Assert.That(texts.Count(text => text.name == "Rank"), Is.EqualTo(state.competitionScores.Count), at + ": a rank a competitor.");
            Assert.That(card.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "Score" || rect.name == "Bar" || rect.name == "Track"), Is.False,
                at + ": no bar and no number on a row.");
            var faces = card.GetComponentsInChildren<RectTransform>().Where(rect => rect.name == "Row portrait").ToArray();
            Assert.That(faces.Length, Is.EqualTo(state.competitionScores.Count), at + ": a face on every row.");
            var badges = card.GetComponentsInChildren<RectTransform>(true).Where(rect => rect.name == "Role mark").ToArray();
            Assert.That(badges.Length, Is.EqualTo(1), at + ": the winner's badge alone.");
            Assert.That(badges[0].IsChildOf(card.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Standing 1")), Is.True, at + ": on the first row.");
            Assert.That(IsTheCrown(badges[0]), Is.True, at + ": a Head of Household's crown.");

            yield return new WaitForSecondsRealtime(.3f);
            var details = card.GetComponentsInChildren<Button>().Single(button => button.name == "Review competition score details");
            details.onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var full = card.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Full performance explanation");
            Assert.That(full.text, Is.EqualTo(CompetitionWords.Explanation(state)), at + ": the details are the words read from the committed state.");
            Assert.That(full.text, Does.StartWith("You competed"), at);
            Assert.That(Regex.IsMatch(full.text, @"\d"), Is.False, at + ": no number in the words: " + full.text);
            full.ForceMeshUpdate();
            Assert.That(full.isTextOverflowing, Is.False, at + ": the words fit the page.");
            SweepTheCard(card, at + " text, the details");
            details.onClick.Invoke();
            yield return null;
            Assert.That(card.GetComponentsInChildren<TMP_Text>().Any(text => text.name == "Full performance explanation"), Is.False, at + ": back to the standings.");
        }

        /// <summary>No label on the card, the hidden page included, says a number with a fraction - but the player's own attempt.</summary>
        private static void SweepTheCard(CompetitionResult card, string where)
        {
            foreach (var text in card.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "Player attempt") continue;
                Assert.That(CompetitionWords.HasDecimal(text.text), Is.False, where + ": '" + text.name + "' says a number: " + text.text);
            }
        }

        /// <summary>
        /// The results phase's FINAL STANDINGS: a row a competitor in the engine's order, each with
        /// its rank, a face and a name, the crown and the word on the winner's alone, every label
        /// in a box of at least 1.3 times its type, and not a decimal on the panel.
        /// </summary>
        private void AssertTheStandingsAreRows(string at)
        {
            var state = director.Snapshot;
            var panel = ActiveRect("Episode panel");
            Assert.That(panel, Is.Not.Null, at + ": the results panel is up.");
            var words = PanelWords(panel);
            Assert.That(words, Does.Contain("FINAL STANDINGS"), at);
            foreach (var word in words) Assert.That(CompetitionWords.HasDecimal(word), Is.False, at + ": the panel says a number: " + word);
            var ranked = state.competitionScores.OrderByDescending(score => score.score).ToList();
            for (int rank = 0; rank < ranked.Count; rank++)
            {
                var who = state.Find(ranked[rank].contestantId);
                var row = ActiveRect(EpisodeHud.StandingRowName + " " + (rank + 1));
                Assert.That(row, Is.Not.Null, at + ": " + who.name + " is ranked " + (rank + 1) + ".");
                var name = row.GetComponentsInChildren<TMP_Text>().Single(text => text.name == EpisodeHud.StandingNameName);
                Assert.That(name.text, Does.StartWith(HudPrimitives.WithYou(who.name, who.isPlayer)), at);
                Assert.That(row.GetComponentsInChildren<TMP_Text>().Single(text => text.name == EpisodeHud.StandingRankName).text, Is.EqualTo((rank + 1).ToString()), at);
                Assert.That(row.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == EpisodeHud.StandingPortraitName), Is.True, at + ": a face on every row.");
                var badge = row.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(rect => rect.name == "Role mark");
                Assert.That(badge != null, Is.EqualTo(rank == 0), at + ": the badge is the winner's alone.");
                if (badge != null) Assert.That(IsTheCrown(badge), Is.True, at + ": the crown on the winner.");
                Assert.That(row.GetComponentsInChildren<TMP_Text>().Any(text => text.name == EpisodeHud.StandingWordName), Is.EqualTo(rank == 0),
                    at + ": " + EpisodeHud.StandingWinnerWord + " on the first row only.");
                foreach (var label in row.GetComponentsInChildren<TMP_Text>())
                {
                    label.ForceMeshUpdate();
                    Assert.That(label.isTextOverflowing, Is.False, at + ": '" + label.text + "' is cut off.");
                    Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.fontSize * 1.3f - .5f),
                        at + ": '" + label.name + "' is boxed " + label.rectTransform.rect.height + " for type of " + label.fontSize + ".");
                }
                Assert.That(ScreenRect(row).width, Is.LessThanOrEqualTo(ScreenRect(panel).width + .5f), at + ": the row fits the panel.");
            }
            Assert.That(ActiveRect(EpisodeHud.StandingRowName + " " + (ranked.Count + 1)), Is.Null, at + ": no row past the field.");
        }

        /// <summary>The badge is the crown: the generated glyph where the icon pass has run, the drawn band and points where it has not.</summary>
        private static bool IsTheCrown(RectTransform badge)
        {
            var glyph = badge.GetComponentsInChildren<Image>(true).FirstOrDefault(image => image.name == "Role glyph");
            if (glyph != null) return glyph.sprite != null && glyph.sprite == UiTheme.Icon("crown");
            return badge.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "Crown band");
        }
    }
}
