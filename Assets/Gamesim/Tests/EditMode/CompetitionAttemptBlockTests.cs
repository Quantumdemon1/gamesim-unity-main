using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The competition result can be read (UI-UX plan D12): the standings card is given the finish
    /// plate's headline over the attempt's measure, and splits them again to draw them; leaving is
    /// "Back to the briefing" in the ranked policy as on the button; and the house conversation's way
    /// out is "Back to the house". These name the screens' own types, so they run in EditMode (and
    /// under the offline Mono harness), not in the Unity-free suite.
    /// </summary>
    public sealed class CompetitionAttemptBlockTests
    {
        [Test]
        public void TheAttemptBlock_IsTheFinishHeadlineOverTheAttemptLine()
        {
            var memory = new MiniGameRun(CompetitionMiniGames.Kind.Memory, 9, 3);
            Assert.That(CompetitionGameScreen.AttemptBlock(memory), Is.Null, "Nothing to say before the attempt ends.");
            memory.Finish();
            string block = CompetitionGameScreen.AttemptBlock(memory);
            Assert.That(block, Is.EqualTo("TIME" + CompetitionResult.AttemptHeadlineBreak + CompetitionGameScreen.AttemptLine(memory)),
                "A board the clock ended: the plate's TIME over the line.");
            Assert.That(CompetitionResult.AttemptHeadlineOf(block), Is.EqualTo("TIME"));
            Assert.That(CompetitionResult.AttemptLineOf(block), Is.EqualTo(CompetitionGameScreen.AttemptLine(memory)));
            Assert.That(CompetitionResult.AttemptLineOf(block), Does.StartWith("Your attempt").And.Contain("0 / 8 pairs"));

            var endurance = new MiniGameRun(CompetitionMiniGames.Kind.Endurance, 9, 3);
            endurance.SetHolding(true); endurance.Tick(4); endurance.Finish();
            string held = CompetitionGameScreen.AttemptBlock(endurance);
            Assert.That(CompetitionResult.AttemptHeadlineOf(held), Is.Not.Null.And.Not.Empty, "Every played game has its plate's headline.");
            Assert.That(CompetitionResult.AttemptLineOf(held), Does.Contain("held " + endurance.Held.ToString("0.0") + " s"));
        }

        [Test]
        public void AnAttemptWithoutAHeadline_IsOneLine_AsItWas()
        {
            const string thrown = "You threw it  ·  3 of 6";
            Assert.That(CompetitionResult.AttemptHeadlineOf(thrown), Is.Null);
            Assert.That(CompetitionResult.AttemptLineOf(thrown), Is.EqualTo(thrown));
            Assert.That(CompetitionResult.AttemptHeadlineOf(null), Is.Null);
            Assert.That(CompetitionResult.AttemptLineOf(null), Is.Null);
            Assert.That(CompetitionResult.JoinAttempt(null, thrown), Is.EqualTo(thrown));
            Assert.That(CompetitionResult.JoinAttempt("", thrown), Is.EqualTo(thrown));
            Assert.That(CompetitionResult.JoinAttempt("TIME", null), Is.Null);
            Assert.That(CompetitionResult.JoinAttempt("TIME", thrown), Is.EqualTo("TIME\n" + thrown));
        }

        [Test]
        public void TheRankedPolicy_NamesTheWayBackByItsCaption()
        {
            Assert.That(CompetitionWords.RankedPolicy, Does.StartWith("Choose " + CompetitionGameScreen.BackToBriefingCaption + " or reload"));
            Assert.That(CompetitionGameScreen.BackToBriefingCaption, Is.EqualTo(WayOut.BackTo(CompetitionWords.BriefingPlace)));
        }

        [Test]
        public void TheHouseConversationsWayOut_SaysWhereItGoes()
        {
            Assert.That(HouseInteraction.LeaveConversationCaption, Is.EqualTo("Back to the house    [Esc]"));
            Assert.That(HouseInteraction.LeaveConversationCaption, Does.StartWith(WayOut.BackTo(WayOut.House)));
        }
    }
}
