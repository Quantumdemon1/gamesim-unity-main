using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class CompetitionSurfacePlayModeTests
    {
        /// <summary>
        /// The result card says the order, the winner and the player's own attempt, and nobody's
        /// score (UI-UX-PASS-PLAN C0, decision 12): no label on the standings or the Score details
        /// page parses as a decimal but the player's own measure, no bar is drawn to a number, the
        /// winner's face carries the badge, the foot is one line in the player's words, and the
        /// card stands on an ink ground under its glass - at both text sizes.
        /// </summary>
        [UnityTest]
        public IEnumerator Result_SaysTheOrderAndThePlayersOwnMeasureAndNobodysScore()
        {
            CreateInput(); screen.Hide();
            foreach (float scale in new[] { 1f, 1.2f })
            {
                var result = CompetitionResult.Attach(owner); result.FontScale = scale;
                string at = " at " + scale;
                try
                {
                    var rows = new[]
                    {
                        new CompetitionResult.Standing("Maya Hassan", 6.42, true, false, null),
                        new CompetitionResult.Standing("You", 6.4, false, true, null),
                        new CompetitionResult.Standing("Riley Johnson", 5.9, false, false, null, null, "Have-Not"),
                        new CompetitionResult.Standing("Casey Wilson", 3.25, false, false, null),
                    };
                    Assert.That(result.Play("Head of Household · Pressure Cooker", "Endurance", 2, rows, true,
                        "You competed: your performance counted on top of what you had earned. " + CompetitionWords.Close,
                        "Your attempt  ·  held 12.4 s  ·  performance 64%"), Is.True);
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    AssertNoCompetitorsNumber(result, "the standings" + at);
                    var texts = result.GetComponentsInChildren<TMP_Text>();
                    Assert.That(texts.Single(text => text.name == "Standings heading").text, Is.EqualTo(CompetitionWords.StandingsHeading));
                    Assert.That(texts.Single(text => text.name == "Performance explanation").text, Is.EqualTo(CompetitionWords.ResultFooter));
                    Assert.That(texts.Single(text => text.name == "Winner").text, Is.EqualTo("Maya Hassan wins!"));
                    Assert.That(texts.Where(text => text.name == "Rank").Select(text => text.text), Is.EqualTo(new[] { "1", "2", "3", "4" }), "The order is the result.");
                    Assert.That(texts.Count(text => text.name == "Winner word"), Is.EqualTo(1), "The winner's word, once.");
                    Assert.That(result.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "Track" || rect.name == "Bar" || rect.name == "Score"),
                        Is.False, "No bar and no number on any row.");
                    var faces = result.GetComponentsInChildren<RectTransform>().Where(rect => rect.name == "Row portrait").ToArray();
                    Assert.That(faces.Length, Is.EqualTo(4), "A face on every row.");
                    Assert.That(faces.Count(HasBadge), Is.EqualTo(1), "One badge on the standings: the winner's.");
                    var first = result.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Standing 1");
                    Assert.That(first.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "Role mark"), Is.True, "and it is on the first row.");
                    Assert.That(first.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Name").text, Is.EqualTo("Maya Hassan"));
                    Assert.That(result.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Standing 3")
                        .GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Name").text, Does.Contain("Have-Not"), "A row's note stays beside the name.");

                    // The ink under the glass, and the glass over it.
                    var column = result.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Card");
                    var ground = column.Find("Card ground") as RectTransform;
                    Assert.That(ground, Is.Not.Null, "The card stands on a ground of its own.");
                    Assert.That(ground.GetSiblingIndex(), Is.Zero, "under everything else on the card,");
                    Assert.That(ground.GetComponent<Image>().color.a, Is.GreaterThanOrEqualTo(.9f), "and near opaque, so the HUD never reads through.");
                    Assert.That(ground.anchorMin, Is.EqualTo(Vector2.zero));
                    Assert.That(ground.anchorMax, Is.EqualTo(Vector2.one));
                    Assert.That(column.Find("Card glass").GetSiblingIndex(), Is.EqualTo(1), "The glass keeps its edge and glow over the ground.");

                    // The Score details page, in words.
                    yield return new WaitForSecondsRealtime(.3f);
                    result.GetComponentsInChildren<Button>().Single(button => button.name == "Review competition score details").onClick.Invoke();
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    var details = result.GetComponentsInChildren<TMP_Text>();
                    Assert.That(details.Single(text => text.name == "Score details heading").text, Is.EqualTo(CompetitionWords.DetailsHeading));
                    Assert.That(details.Single(text => text.name == "Full performance explanation").text, Does.StartWith("You competed"));
                    AssertNoCompetitorsNumber(result, "the details" + at);
                    foreach (var text in details)
                    {
                        text.ForceMeshUpdate();
                        Assert.That(text.isTextOverflowing, Is.False, text.name + at + ": '" + text.text + "' is cut off.");
                    }
                }
                finally { Object.Destroy(result.gameObject); }
                yield return null;
            }
        }

        /// <summary>The player's own win is "You win!", with no name before it; the row below says who.</summary>
        [UnityTest]
        public IEnumerator Result_ThePlayersOwnWinSaysYouWin()
        {
            CreateInput(); screen.Hide();
            var result = CompetitionResult.Attach(owner);
            try
            {
                var rows = new[]
                {
                    new CompetitionResult.Standing("Jordan Lee", 7.1, true, true, null),
                    new CompetitionResult.Standing("Maya Hassan", 6.42, false, false, null),
                };
                Assert.That(result.Play("Power of Veto · Lock In", "Mental", 3, rows, true), Is.True);
                yield return null;
                var texts = result.GetComponentsInChildren<TMP_Text>(true);
                Assert.That(texts.Single(text => text.name == "Winner").text, Is.EqualTo("You win!"));
                Assert.That(result.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Standing 1")
                    .GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Name").text, Is.EqualTo("Jordan Lee (You)"));
                Assert.That(texts.Single(text => text.name == "Full performance explanation").text, Is.EqualTo(CompetitionWords.DetailsFallback),
                    "A card played without a season explains in the fallback's words.");
                AssertNoCompetitorsNumber(result, "a veto result");

                // The veto's medal on the winner's face, read from the award when the card is played
                // on its own; the director's word, when it gives one, is drawn over the award's reading.
                Assert.That(IsTheVetoMedal(BadgeOn(result, "Standing 1")), Is.True, "A veto win wears the veto's medal.");
                Assert.That(result.Play("Head of Household · Lock In", "Mental", 3, rows, true, winnersMark: HudPrimitives.RoleMark.VetoHolder), Is.True);
                yield return null;
                Assert.That(IsTheVetoMedal(BadgeOn(result, "Standing 1")), Is.True, "The director's badge is drawn over the award's reading.");
                Assert.That(result.Play("Power of Veto · Lock In", "Mental", 3, rows, true, winnersMark: HudPrimitives.RoleMark.HeadOfHousehold), Is.True);
                yield return null;
                Assert.That(IsTheVetoMedal(BadgeOn(result, "Standing 1")), Is.False, "and the crown where the director says crown,");
                Assert.That(IsTheCrownBadge(BadgeOn(result, "Standing 1")), Is.True, "whatever the award says.");
            }
            finally { Object.Destroy(result.gameObject); }
        }

        /// <summary>The badge on a row's face: the one "Role mark" under it.</summary>
        private static RectTransform BadgeOn(CompetitionResult result, string row) =>
            result.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == row)
                .GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Role mark");

        /// <summary>The badge is the veto's medal: the generated token where the icon pass has run, the drawn bar where it has not.</summary>
        private static bool IsTheVetoMedal(RectTransform badge)
        {
            var glyph = badge.GetComponentsInChildren<Image>(true).FirstOrDefault(image => image.name == "Role glyph");
            if (glyph != null) return glyph.sprite != null && glyph.sprite == UiTheme.Icon("veto-token");
            return badge.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "Veto bar");
        }

        /// <summary>The badge is the crown: the generated glyph, or the drawn band and points.</summary>
        private static bool IsTheCrownBadge(RectTransform badge)
        {
            var glyph = badge.GetComponentsInChildren<Image>(true).FirstOrDefault(image => image.name == "Role glyph");
            if (glyph != null) return glyph.sprite != null && glyph.sprite == UiTheme.Icon("crown");
            return badge.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "Crown band");
        }

        /// <summary>No label on the card, the hidden details page included, says a number with a fraction - but the player's own attempt.</summary>
        private static void AssertNoCompetitorsNumber(Component root, string where)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "Player attempt") continue;
                Assert.That(CompetitionWords.HasDecimal(text.text), Is.False, where + ": '" + text.name + "' says a number: " + text.text);
            }
        }

        private static bool HasBadge(RectTransform rim) =>
            rim.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "Role mark");
    }
}
