using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The house screen's menus and side cards follow the text size, the ways out keep clear of the
    /// strategy stage's header, and the nomination's "who?" grid keeps no room for a row that is gone
    /// (U9, backlog item 23). The arithmetic is <see cref="TextSizeFit"/>'s, checked at the three text
    /// sizes; the screens' use of it is held in their source.
    ///
    /// <para>Unity-free, so the subset runs it too; the editor finds the folder through
    /// <c>Application.dataPath</c>, the subset by walking up from its own binary.</para>
    ///
    /// <para>Every measure here is from the top of its frame or its card, which are anchored to the top
    /// of the canvas: they are the same on a 16:9 canvas (1600x900) and a 16:10 one (1518x949). What
    /// the canvas's height decides - whether the right column folds - is decided against the week
    /// card's height, and a side card no taller than the week card leaves that decision as it was.</para>
    /// </summary>
    public sealed class TextSizeFitTests
    {
        [TestCase(TextSize.Standard), TestCase(TextSize.Larger), TestCase(TextSize.Largest)]
        public void MenuRowsAndBoardButtons_GrowWithTheText_AndTheirBoxesHoldTheWords(TextSize size)
        {
            float scale = TextSizes.Scale(size);
            int row = TextSizeFit.Font(TextSizeFit.MenuRowFont, scale), side = TextSizeFit.Font(TextSizeFit.SideButtonFont, scale);
            Assert.That(row, Is.EqualTo((int)Math.Round(16 * scale)), "The cast and moves menus' rows are drawn at 16 times the text scale.");
            Assert.That(side, Is.EqualTo((int)Math.Round(15 * scale)), "Listen in, the Pull's answers and the boards' talk buttons at 15 times the scale.");
            if (size == TextSize.Standard)
            {
                Assert.That(row, Is.EqualTo(16), "At the standard size the menus' rows are as they were.");
                Assert.That(side, Is.EqualTo(15), "and so are the side cards' and the boards' buttons.");
            }
            else Assert.That(row, Is.GreaterThan(16), "At the " + TextSizes.Name(size) + " text the menus grow.");
            // The menus' rows and the boards' talk buttons are 36 tall times the scale already.
            Assert.That(36f * scale, Is.GreaterThanOrEqualTo(row * TextSizeFit.LabelBoxRatio + TextSizeFit.ButtonInsets),
                "A menu row holds its words at 1.3 times their size.");
            Assert.That(36f * scale, Is.GreaterThanOrEqualTo(side * TextSizeFit.LabelBoxRatio + TextSizeFit.ButtonInsets),
                "A board's talk button holds its words at 1.3 times their size.");
        }

        [TestCase(TextSize.Standard), TestCase(TextSize.Larger), TestCase(TextSize.Largest)]
        public void SideCardButtons_GrowOnlyWhereTheirWordsNeedIt_AndTheCardsKeepTheWeekCardsPlace(TextSize size)
        {
            float scale = TextSizes.Scale(size);
            float button = TextSizeFit.SideButton(scale);
            foreach (int resting in new[] { TextSizeFit.SideButtonFont, TextSizeFit.PullNarrowFont })
            {
                int font = TextSizeFit.Font(resting, scale);
                Assert.That(button - TextSizeFit.ButtonInsets, Is.GreaterThanOrEqualTo(font * TextSizeFit.LabelBoxRatio),
                    "A side card's button holds its " + font + " words at 1.3 times their size at the " + TextSizes.Name(size) + " text.");
            }
            if (size == TextSize.Standard)
            {
                Assert.That(button, Is.EqualTo(36f), "At the standard size the buttons are as they were,");
                Assert.That(TextSizeFit.SideCard(scale), Is.EqualTo(168f), "and so are the cards.");
            }
            Assert.That(TextSizeFit.SideCard(scale), Is.EqualTo(TextSizeFit.SideCardHeight + button - 36f),
                "What is under the buttons moves down by what they grew, and the card with it.");
            Assert.That(TextSizeFit.SideCard(scale), Is.LessThanOrEqualTo(TextSizeFit.WeekCardRoom),
                "The Nearby card and the Pull stay no taller than the week card whose place they take.");
        }

        [TestCase(TextSize.Standard), TestCase(TextSize.Larger), TestCase(TextSize.Largest)]
        public void WaysOut_EndAboveTheStrategyHeader_AndStillHoldTheirWords(TextSize size)
        {
            float scale = TextSizes.Scale(size);
            float height = TextSizeFit.StrategyWayOutHeight(scale), foot = TextSizeFit.StrategyWayOutFoot(scale);
            Assert.That(foot, Is.LessThan(TextSizeFit.StrategyBandFoot),
                "At the " + TextSizes.Name(size) + " text Close and a way back end above the header: " + foot + " against " + TextSizeFit.StrategyBandFoot + ".");
            Assert.That(height, Is.GreaterThanOrEqualTo(WayOut.FontSize(scale) * WayOut.LabelBoxRatio),
                "and still hold their words at 1.3 times their size.");
            Assert.That(height, Is.LessThanOrEqualTo(WayOut.Height(scale)), "A way out on the stage is never taller than anywhere else.");
            if (size == TextSize.Standard)
                Assert.That(height, Is.EqualTo(WayOut.Height(scale)), "At the standard size it is as it was.");
            else
                Assert.That(WayOut.Inset(scale) + WayOut.Height(scale), Is.GreaterThanOrEqualTo(TextSizeFit.StrategyBandFoot - .01f),
                    "Without the stage's fit, a way out at the " + TextSizes.Name(size) + " text reaches the header (why the fit exists).");
        }

        [Test]
        public void TheScreens_DrawWithTheFit_NotWithFixedSizes()
        {
            string episode = Path.Combine(GamesimRoot(), "Runtime", "Episode");
            var literal = new Regex(@"\bfontSize(Max)?\s*=\s*\d");
            var problems = new List<string>();
            foreach (var file in new[] { "EpisodeHud.CastMenu.cs", "EpisodeHud.EmoteMenu.cs", "EpisodeHud.FreeTime.cs", "EpisodeHud.CeremonyBoard.cs" })
                if (literal.IsMatch(Code(Path.Combine(episode, file))))
                    problems.Add(file + " sets a control's words to a fixed size.");

            string nearby = Between(Code(Path.Combine(episode, "EpisodeHud.Chrome.cs")), "void NearbyCard(", "void SetNearby(");
            if (literal.IsMatch(nearby)) problems.Add("The Nearby card's Listen in is drawn at a fixed size.");
            if (!nearby.Contains("TextSizeFit.SideButton(") || nearby.Contains("36f)")) problems.Add("The Nearby card's button does not take its height from the fit.");

            string pull = Between(Code(Path.Combine(episode, "EpisodeHud.Story.cs")), "void PullCard(", "void SetPull(");
            if (Regex.IsMatch(pull, @"PullWords\(\s*button\s*,\s*\d")) problems.Add("The Pull's answers are drawn at a fixed size.");
            if (!pull.Contains("TextSizeFit.SideButton(") || pull.Contains("36f)")) problems.Add("The Pull's answers do not take their height from the fit.");

            string stage = Code(Path.Combine(episode, "EpisodeHud.StrategyStage.cs"));
            if (!stage.Contains("TextSizeFit.StrategyWayOutHeight(")) problems.Add("The strategy stage does not fit its ways out above the header.");
            if (!Regex.IsMatch(stage, @"StrategyBandFoot\s*=\s*TextSizeFit\.StrategyBandFoot")) problems.Add("The stage's header line is not the one the fit is checked against.");
            foreach (var file in new[] { "EpisodeHud.WayOut.cs", "EpisodeHud.DecisionPageBack.cs" })
                if (!Code(Path.Combine(episode, file)).Contains("KeepWayOutsAboveTheHeader()")) problems.Add(file + " draws a way back the stage does not fit.");

            if (Code(Path.Combine(episode, "EpisodeHud.NominationScreen.cs")).Contains("reserveBelow"))
                problems.Add("The nomination's people grid still keeps room for a row under it.");
            if (Regex.IsMatch(Code(Path.Combine(episode, "EpisodeDirector.NominationScreen.cs")), @"57f\s*\*\s*hud\.FontScale"))
                problems.Add("The nomination still reserves the removed Back row's room.");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        /// <summary>The text between two markers, or all of it after the first when the second is not found.</summary>
        private static string Between(string code, string from, string to)
        {
            int start = code.IndexOf(from, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "'" + from + "' is in the file.");
            int end = code.IndexOf(to, start, StringComparison.Ordinal);
            return end > start ? code.Substring(start, end - start) : code.Substring(start);
        }

        /// <summary>A file's code with its comments blanked, so a note about an old size is not a size.</summary>
        private static string Code(string path)
        {
            Assert.That(File.Exists(path), Is.True, path + " exists.");
            string text = File.ReadAllText(path);
            text = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(text, @"//[^\n]*", " ");
        }

        private static string GamesimRoot()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "Gamesim"));
#else
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "Gamesim");
                if (Directory.Exists(Path.Combine(candidate, "Runtime"))) return Path.GetFullPath(candidate);
            }
            Assert.Fail("Assets/Gamesim was not found above " + AppContext.BaseDirectory);
            return null;
#endif
        }
    }
}
