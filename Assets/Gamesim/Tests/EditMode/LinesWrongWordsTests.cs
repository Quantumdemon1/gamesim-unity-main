using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Lines that said the wrong thing (UI-UX plan U8 and D12; gameplay backlog items 20 and 30):
    /// the house conversation's way out says where it goes ("Back to the house"), the diary room's
    /// lines stop sending the player to "the episode screen", leaving a competition is "Back to the
    /// briefing" in every line that names it, the notes page names the week buttons it leaves its
    /// earlier weeks to, and the competition's ranking is headed "HOW THEY PLACED".
    ///
    /// <para>Plain C#: the words and the source reads run in the Unity-free suite as well as in
    /// EditMode (the source root from <c>Application.dataPath</c> in the editor, by walking up from
    /// the binary in the subset).</para>
    /// </summary>
    public sealed class LinesWrongWordsTests
    {
        /// <summary>The old wordings, each with the file that said it.</summary>
        private static readonly (string file, string words)[] OldLines =
        {
            ("House/HouseInteraction.cs", "Back to exploring"),
            ("Episode/EpisodeDirector.DiaryRoom.cs", "Return to the episode screen"),
            ("Episode/EpisodeDirector.Challenge.cs", "Go back to the briefing"),
            ("Episode/EpisodeDirector.CompetitionArena.cs", "Return to the briefing"),
            ("Presentation/CompetitionGameScreen.Chrome.cs", "Cancel or reload returns"),
            ("Episode/EpisodeDirector.Journal.cs", "Earlier weeks are in the save"),
        };

        [Test]
        public void TheOldWordings_AreGoneFromTheGame()
        {
            foreach (var (file, words) in OldLines)
                Assert.That(Read(file), Does.Not.Contain(words), file + " still says \"" + words + "\".");
            var anywhere = Directory.GetFiles(RuntimeRoot(), "*.cs", SearchOption.AllDirectories)
                .Where(path => OldLines.Any(old => File.ReadAllText(path).Contains(old.words)))
                .Select(path => Path.GetFileName(path)).ToList();
            Assert.That(anywhere, Is.Empty, "No script says an old wording.");
        }

        [Test]
        public void TheHouseConversationsWayOut_IsBackToTheHouse()
        {
            string code = Read("House/HouseInteraction.cs");
            Assert.That(code, Does.Contain("LeaveConversationCaption = WayOut.BackPrefix + WayOut.House + \"    [Esc]\";"),
                "The caption is the helper's \"Back to the house\" with its key.");
            Assert.That(code, Does.Contain("MakeButton(\"Leave conversation\", conversationRect, LeaveConversationCaption,"),
                "The button draws that caption.");
            Assert.That(WayOut.IsWayBack(WayOut.BackPrefix + WayOut.House), Is.True);
        }

        [Test]
        public void TheDiaryRoomsLines_SayHowToLeaveIt()
        {
            string code = Read("Episode/EpisodeDirector.DiaryRoom.cs");
            Assert.That(Regex.Matches(code, Regex.Escape("Your ballot has already been recorded. Close the diary room: the eviction reveal happens at the episode screen.")).Count,
                Is.EqualTo(2), "Both ballot lines say the diary's own way out, Close, and where the reveal is.");
            Assert.That(code, Does.Contain("No actions left in this window. Close the diary room when you are ready to go on."));
            Assert.That(code, Does.Not.Contain("Return to the"), "No \"Return to\": the one way out is Close, or \"Back to <place>\".");
        }

        [Test]
        public void LeavingACompetition_IsBackToTheBriefing_InEveryLine()
        {
            Assert.That(CompetitionWords.RankedPolicy, Does.StartWith("Choose " + WayOut.BackTo(CompetitionWords.BriefingPlace) + " or reload"),
                "The ranked board's policy names the way back by its caption.");
            Assert.That(WayOut.BackTo(CompetitionWords.BriefingPlace), Is.EqualTo("Back to the briefing"));
            Assert.That(Read("Presentation/CompetitionGameScreen.Chrome.cs"), Does.Contain(": CompetitionWords.RankedPolicy,"),
                "The ranked board's card draws it.");
            Assert.That(Read("Presentation/CompetitionGameScreen.cs"), Does.Contain("BackToBriefingCaption = WayOut.BackTo(\"the briefing\")"),
                "The screen's way back is the same place.");
            Assert.That(Read("Episode/EpisodeDirector.Challenge.cs"),
                Does.Contain("\"The episode changed during this attempt. Choose \" + CompetitionGameScreen.BackToBriefingCaption + \" to refresh.\""),
                "The stale attempt's line names the button under it.");
            string arena = Read("Episode/EpisodeDirector.CompetitionArena.cs");
            Assert.That(Regex.Matches(arena, Regex.Escape("You are back at the briefing: try again.")).Count, Is.EqualTo(2),
                "The arena's refusals cancel the attempt themselves: the player is already back at the briefing.");
        }

        [Test]
        public void TheNotesPage_NamesTheWeekButtonsItLeavesEarlierWeeksTo()
        {
            Func<int, string> caption = week => "Read the week " + week + " recap";
            Assert.That(StorySoFarWords.EarlierWeeksLine(3, new int[0], caption), Is.EqualTo("The last 3 weeks are shown here."));
            Assert.That(StorySoFarWords.EarlierWeeksLine(3, null, caption), Is.EqualTo("The last 3 weeks are shown here."));
            Assert.That(StorySoFarWords.EarlierWeeksLine(3, new[] { 1 }, caption),
                Is.EqualTo("The last 3 weeks are shown here. Week 1 is under WEEKS SO FAR above: Read the week 1 recap."));
            Assert.That(StorySoFarWords.EarlierWeeksLine(3, new[] { 4, 1, 2, 3, 4 }, caption),
                Is.EqualTo("The last 3 weeks are shown here. Weeks 1 to 4 are under WEEKS SO FAR above: Read the week 1 recap to Read the week 4 recap."));
            Assert.That(StorySoFarWords.EarlierWeeksLine(3, new[] { 2 }, caption), Does.Not.Contain("save"));
            Assert.Throws<ArgumentNullException>(() => StorySoFarWords.EarlierWeeksLine(3, new[] { 1 }, null));

            string journal = Read("Episode/EpisodeDirector.Journal.cs");
            Assert.That(journal, Does.Contain("StorySoFarWords.EarlierWeeksLine(weeks,"), "The notes page draws the line.");
            Assert.That(journal, Does.Contain("EpisodeHud.ReviewWeekCaption)"), "with the week buttons' own caption.");
            Assert.That(Read("Episode/EpisodeHud.cs"), Does.Contain("ReviewWeekCaption(int week) => \"Read the week \" + week + \" recap\";"),
                "The buttons' caption, as the case above spells it.");
            Assert.That(Read("Episode/EpisodeDirector.cs"), Does.Contain("hud.Heading(StorySoFarWords.WeeksSoFarHeading);"),
                "The heading the line points at is the one the buttons are under.");
        }

        [Test]
        public void TheCompetitionsRanking_IsHeadedHowTheyPlaced()
        {
            Assert.That(CompetitionWords.StandingsHeading, Is.EqualTo("HOW THEY PLACED"));
            Assert.That(Read("Presentation/CompetitionResult.cs"), Does.Contain("Label(\"Standings heading\",standingsPanel,CompetitionWords.StandingsHeading,"));
        }

        [Test]
        public void TheStandingsCard_CarriesTheAttemptsHeadline_AndEnterSkipsThePracticeCount()
        {
            Assert.That(Read("Episode/EpisodeDirector.Challenge.cs"), Does.Contain("pendingAttemptLine = CompetitionGameScreen.AttemptBlock(challengeRun);"),
                "The card is given the finish plate's headline over the measure.");
            string result = Read("Presentation/CompetitionResult.cs");
            Assert.That(result, Does.Contain("Label(\"Attempt headline\",column,attemptHeadline,"));
            Assert.That(result, Does.Contain("Label(\"Player attempt\",column,AttemptLineOf(playedAttempt),"));
            string screen = Read("Presentation/CompetitionGameScreen.cs");
            Assert.That(screen, Does.Contain("if (input.CompetitionConfirm.WasPressedThisFrame()) SkipPracticeCount();"),
                "Confirm - Enter, the pad's A - skips the practice's count.");
            Assert.That(screen, Does.Contain("if (ranked || !IsShowing || playing || previewStarted || held || Paused || IsAssembling) return false;"),
                "Never a ranked board's count.");
        }

        // ---------------------------------------------------------------- reading code

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
