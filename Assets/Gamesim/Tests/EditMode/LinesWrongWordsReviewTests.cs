using System;
using System.IO;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The review's two fixes to the wording slice: the results phase's ranking carries the same
    /// heading as the result card's ("HOW THEY PLACED", not "FINAL STANDINGS"), and the notes
    /// page's line about earlier weeks names each week when a week inside the span has no recap
    /// button, so a range never covers a week the page cannot open.
    ///
    /// <para>Plain C#: runs in the Unity-free suite as well as in EditMode.</para>
    /// </summary>
    public sealed class LinesWrongWordsReviewTests
    {
        [Test]
        public void TheResultsPhasesRanking_IsHeadedLikeTheResultCard()
        {
            string director = Read("Episode/EpisodeDirector.cs");
            Assert.That(director, Does.Contain("hud.Section(CompetitionWords.StandingsHeading);"),
                "The results phase's ranking draws the card's heading.");
            Assert.That(director, Does.Not.Contain("\"FINAL STANDINGS\""), "One name for the ranking.");
            Assert.That(CompetitionWords.StandingsHeading, Is.EqualTo("HOW THEY PLACED"));
        }

        [Test]
        public void TheEarlierWeeksLine_NamesEachWeek_WhenAWeekInTheSpanHasNoRecap()
        {
            Func<int, string> caption = week => "Read the week " + week + " recap";
            Assert.That(StorySoFarWords.EarlierWeeksLine(3, new[] { 1, 3 }, caption),
                Is.EqualTo("The last 3 weeks are shown here. Weeks 1 and 3 are under WEEKS SO FAR above: Read the week 1 recap and Read the week 3 recap."));
            Assert.That(StorySoFarWords.EarlierWeeksLine(3, new[] { 4, 1, 2 }, caption),
                Is.EqualTo("The last 3 weeks are shown here. Weeks 1, 2 and 4 are under WEEKS SO FAR above: Read the week 1 recap, Read the week 2 recap and Read the week 4 recap."));
            Assert.That(StorySoFarWords.EarlierWeeksLine(3, new[] { 2, 3, 4 }, caption),
                Is.EqualTo("The last 3 weeks are shown here. Weeks 2 to 4 are under WEEKS SO FAR above: Read the week 2 recap to Read the week 4 recap."),
                "Weeks with no gap keep the range.");
        }

        private static string Read(string relative) => File.ReadAllText(Path.Combine(RuntimeRoot(), relative));

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
