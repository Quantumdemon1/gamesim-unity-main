using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Menu slice 2, part d, held in the source: on the decisions, the finale, the jury house, the
    /// competitions' cards and the conversations a page's way back is drawn by the shared helper
    /// (EpisodeHud.DecisionPageBack, which draws WayOut.AddBack) and never as a row, a pinned action or
    /// a footer secondary; Escape and the pad's B reach it; the exit words it folded in are gone; and
    /// the season report's Close is its corner, not one of its ways on. The built screens are checked
    /// in PlayMode (EpisodePlayModeTests.MenuOneWayBackDecisions.cs).
    ///
    /// <para>Unity-free, so the subset runs it too; the editor finds the scripts through
    /// <c>Application.dataPath</c>, the subset by walking up from its own binary.</para>
    /// </summary>
    public sealed class MenuOneWayBackDecisionsScanTests
    {
        /// <summary>The screens of part d that draw a page's way back, relative to Assets/Gamesim/Runtime.</summary>
        private static readonly string[] PageFiles =
        {
            "Episode/EpisodeDirector.NominationScreen.cs", "Episode/EpisodeDirector.FinalThree.cs",
            "Episode/EpisodeDirector.FinalePage.cs", "Episode/EpisodeDirector.JuryHouse.cs",
            "Episode/EpisodeDirector.ConversationNavigation.cs", "Episode/EpisodeDirector.ComesToYou.cs",
            "Episode/EpisodeDirector.FinalCase.cs",
        };

        /// <summary>Every way back those pages have, by the name the code gives its caption.</summary>
        private static readonly string[] WayBackNames =
        {
            "BackToNomineesCaption", "BackToStoryChoicesCaption", "BackToPreparationCaption", "BackToFinaleCaption",
            "JuryHouseBackCaption", "ConversationTopicsBackCaption", "FinalCaseBackCaption",
        };

        /// <summary>Part d's other files whose words are checked for the exit words folded in.</summary>
        private static readonly string[] CardFiles =
        {
            "Episode/EpisodeDirector.Challenge.cs", "Presentation/CompetitionGameScreen.cs",
            "Presentation/CompetitionGameScreen.Chrome.cs", "Presentation/CompetitionGameScreen.Assembly.cs",
            "Presentation/CompetitionGameScreen.Finish.cs", "Presentation/CompetitionResult.cs", "Presentation/SeasonReport.cs",
        };

        /// <summary>The words a way out had before it was folded into the one way out (menu slice 2, ruling 2).</summary>
        private static readonly string[] FoldedWords =
        {
            "Leave the jury house", "Return to briefing", "Back to briefing", "Back to standings", "Return to the house",
            "Close your final case",
            // and the weekly budget's second name on the nomination's card (one name per thing: "Actions left").
            "CONVERSATIONS LEFT",
        };

        private static readonly Regex Literal = new Regex("@?\"(?:[^\"\\\\\\n]|\\\\.)*\"", RegexOptions.CultureInvariant);
        private static readonly Regex OtherDrawing = new Regex(@"hud\.(Action|PinnedAction|PinnedSecondary)\(\s*(?:EpisodeHud\.|EpisodeDirector\.)?(\w+)", RegexOptions.CultureInvariant);
        private static readonly Regex HelperDrawing = new Regex(@"hud\.DecisionPageBack\(\s*(?:EpisodeHud\.|EpisodeDirector\.)?(\w+)", RegexOptions.CultureInvariant);

        [Test]
        public void DecisionPages_DrawEveryWayBackWithTheSharedHelper()
        {
            var problems = new List<string>();
            var drawn = new HashSet<string>();
            foreach (var file in PageFiles)
            {
                string code = Code(Read(file));
                foreach (Match match in OtherDrawing.Matches(code))
                    if (WayBackNames.Contains(match.Groups[2].Value) || match.Groups[2].Value == "StoryBackCaption")
                        problems.Add(file + " draws " + match.Groups[2].Value + " with hud." + match.Groups[1].Value + ": a way back is the helper's, at the top left.");
                foreach (Match match in HelperDrawing.Matches(code)) drawn.Add(match.Groups[1].Value);
            }
            foreach (var name in WayBackNames.Where(name => !drawn.Contains(name)))
                problems.Add(name + " is drawn by no hud.DecisionPageBack call.");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void EscapeAndB_TakeThePagesWayBack()
        {
            // The back chain's one level (EpisodeDirector.Controls.cs BackOut calls BackConversationNavigation):
            // a conversation's own level first, then a decision page's way back, then the panel closes.
            string code = Code(Read("Episode/EpisodeDirector.ConversationNavigation.cs"));
            Assert.That(code, Does.Contain("hud.TakeDecisionPageBack()"), "Escape and B do what the page's way back does.");
            string hud = Code(Read("Episode/EpisodeHud.DecisionPageBack.cs"));
            Assert.That(hud, Does.Contain("WayOut.AddBack("), "The page's way back is drawn by the shared helper.");
        }

        [Test]
        public void TheFoldedExitWords_AreGone_AndTheReportsCloseIsItsCorner()
        {
            var problems = new List<string>();
            foreach (var file in PageFiles.Concat(CardFiles))
                foreach (var text in Literals(Code(Read(file))))
                {
                    string words = text.TrimStart('@').Trim('"');
                    if (FoldedWords.Any(folded => words.IndexOf(folded, StringComparison.Ordinal) >= 0) || words == "Back")
                        problems.Add(file + " says " + text + ": folded into the one way out.");
                }
            string report = Code(Read("Presentation/SeasonReport.cs"));
            if (Regex.IsMatch(report, @"ways\.Add\(\(\s*CloseCaption"))
                problems.Add("Presentation/SeasonReport.cs offers Close among the ways on: it is the card's corner.");
            if (!report.Contains("WayOut.AddClose("))
                problems.Add("Presentation/SeasonReport.cs draws no Close with the shared helper.");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        /// <summary>
        /// A card over the house has the panel's Close alone: the answered ask card draws no "Back to the house" row
        /// (SceneCardDoneCaption) beside it. The final case, a page over the station's panel, has its one way back from
        /// the helper and no row of its own.
        /// </summary>
        [Test]
        public void TheAnsweredAskCardAndTheFinalCase_HaveOneWayOutEach()
        {
            var problems = new List<string>();
            string asks = Code(Read("Episode/EpisodeDirector.ComesToYou.cs"));
            if (asks.Contains("SceneCardDoneCaption"))
                problems.Add("EpisodeDirector.ComesToYou.cs draws SceneCardDoneCaption: the ask card's one way out is the panel's Close.");
            string finalCase = Code(Read("Episode/EpisodeDirector.FinalCase.cs"));
            if (!Regex.IsMatch(finalCase, @"hud\.DecisionPageBack\(\s*FinalCaseBackCaption\("))
                problems.Add("EpisodeDirector.FinalCase.cs draws no way back with hud.DecisionPageBack(FinalCaseBackCaption(...)).");
            if (Regex.IsMatch(finalCase, @"hud\.Action\(\s*\w*Leave\w*Caption"))
                problems.Add("EpisodeDirector.FinalCase.cs draws a leave row beside the panel's Close.");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        /// <summary>
        /// A conversation's page with a level open inside it - a verb's people, a plea's approaches - names that level
        /// in its one way back, and Escape goes back the same level (the key beside the words is true). The plea's own
        /// "Ask something else" row is gone into it.
        /// </summary>
        [Test]
        public void AConversationsInnerLevel_IsThePagesOneWayBack()
        {
            var problems = new List<string>();
            string navigation = Code(Read("Episode/EpisodeDirector.ConversationNavigation.cs"));
            if (!Regex.IsMatch(navigation, @"string\s+pageBack\s*=\s*ConversationPageBackCaption\(") || !navigation.Contains("hud.DecisionPageBack(pageBack,"))
                problems.Add("The category page's way back is not the one ConversationPageBackCaption names.");
            if (!Regex.IsMatch(navigation, @"else if \(!toTopics && PleaDraftShowing\("))
                problems.Add("Escape's level for a plea is not the one the page's way back names (PleaDraftShowing).");
            string strategy = Code(Read("Episode/EpisodeDirector.Strategy.cs"));
            if (strategy.Contains("LobbyBackCaption"))
                problems.Add("EpisodeDirector.Strategy.cs draws the plea's 'Ask something else' row: a second way back.");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        private static List<string> Literals(string code) => Literal.Matches(code).Cast<Match>().Select(match => match.Value).ToList();

        private static string Read(string relative) => File.ReadAllText(Path.Combine(RuntimeRoot(), relative));

        /// <summary>A source with its comments blanked and its line breaks kept: line comments and block comments both, never inside a string.</summary>
        private static string Code(string source)
        {
            var code = new StringBuilder(source.Length);
            bool block = false, text = false;
            for (var index = 0; index < source.Length; index++)
            {
                char c = source[index];
                char next = index + 1 < source.Length ? source[index + 1] : '\0';
                if (block)
                {
                    if (c == '*' && next == '/') { block = false; index++; }
                    else if (c == '\n') code.Append('\n');
                    continue;
                }
                if (text)
                {
                    code.Append(c);
                    if (c == '\\' && next != '\0') { code.Append(next); index++; }
                    else if (c == '"' || c == '\n') text = false;
                    continue;
                }
                if (c == '"') { text = true; code.Append(c); continue; }
                if (c == '\'' && index + 2 < source.Length)
                {
                    // A char literal, '"' among them, is copied whole so it never opens a string.
                    int end = source.IndexOf('\'', index + (next == '\\' ? 3 : 2));
                    if (end > index && end - index <= 4) { code.Append(source, index, end - index + 1); index = end; continue; }
                }
                if (c == '/' && next == '*') { block = true; index++; continue; }
                if (c == '/' && next == '/')
                {
                    while (index < source.Length && source[index] != '\n') index++;
                    code.Append('\n');
                    continue;
                }
                code.Append(c);
            }
            return code.ToString();
        }

        private static string RuntimeRoot()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Runtime"));
#else
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "Gamesim", "Runtime");
                if (Directory.Exists(candidate)) return Path.GetFullPath(candidate);
            }
            Assert.Fail("Assets/Gamesim/Runtime was not found above " + AppContext.BaseDirectory);
            return null;
#endif
        }
    }
}
