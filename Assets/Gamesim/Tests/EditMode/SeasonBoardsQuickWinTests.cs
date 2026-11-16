using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The menu review's quick wins on the in-season boards (part c): one name for the player's deals
    /// (quick win 20), and the free-time board's "Last move" line (quick win 18), whose words are read
    /// from the season's own log and never outlive the week they belong to.
    /// </summary>
    public sealed class SeasonBoardsQuickWinTests
    {
        [Test]
        public void ThePlayersDealsHaveOneNameOnTheDoorThePageAndTheConversation()
        {
            Assert.That(EpisodeDirector.DealsAndPromisesName, Is.EqualTo("Deals & promises"));
            Assert.That(EpisodeDirector.YourWordCaption, Is.EqualTo(EpisodeDirector.DealsAndPromisesName), "The notes page's door says it,");
            Assert.That(EpisodeDirector.DealsCategoryCaption, Is.EqualTo(EpisodeDirector.DealsAndPromisesName), "and so does the conversation's topic.");
        }

        [Test]
        public void ThePickersShortWarningNamesThePageItPointsTo()
        {
            Assert.That(EpisodeDirector.NominationBrief(1, 1, 0),
                Is.EqualTo("Committing these breaks 1 entry in Deals & promises and lets 1 storyline pass."));
            string two = EpisodeDirector.NominationBrief(2, 0, 1);
            Assert.That(two, Is.EqualTo("Committing these breaks 2 entries in Deals & promises. 1 pitch expires when you nominate."));
            Assert.That(two, Does.Not.Contain("commitments").And.Not.Contain("Your word"), "No second name for the same page.");
        }

        private static EpisodeState FreeTime(int week)
        {
            var state = new EpisodeState { playerId = "you", week = week, phase = EpisodePhase.Social };
            state.events.Add(new EpisodeEvent { sequence = 7, week = week, phase = EpisodePhase.Social, kind = "conversation",
                text = "You and Maya talked about the game.", audienceIds = { "you", "maya" } });
            state.events.Add(new EpisodeEvent { sequence = 8, week = week, phase = EpisodePhase.Social, kind = "conversation",
                text = "Maya and Theo talked in the kitchen.", audienceIds = { "maya", "theo" } });
            return state;
        }

        [Test]
        public void TheLastMoveLineIsTheLoggedLineInTheStatusLinesWords()
        {
            var state = FreeTime(3);
            Assert.That(EpisodeDirector.LastMoveLine(state, 7), Is.EqualTo("Last move · You and Maya talked about the game."));
            Assert.That(EpisodeDirector.LastMoveLine(state, 7), Does.StartWith(EpisodeDirector.LastMovePrefix));
        }

        [Test]
        public void ThereIsNoLastMoveBeforeTheFirstOrForALineTheLogNoLongerHolds()
        {
            var state = FreeTime(3);
            Assert.That(EpisodeDirector.LastMoveLine(state, -1), Is.Null, "Nothing before the week's first move.");
            Assert.That(EpisodeDirector.LastMoveLine(state, 99), Is.Null, "Nothing for a line the log has dropped.");
            Assert.That(EpisodeDirector.LastMoveLine(state, 8), Is.Null, "Nothing the player was not party to.");
            Assert.That(EpisodeDirector.LastMoveLine(null, 7), Is.Null);
        }

        [Test]
        public void ALastMoveNeverOutlivesItsWeekOrFreeTime()
        {
            var turned = FreeTime(3);
            turned.week = 4;
            Assert.That(EpisodeDirector.LastMoveLine(turned, 7), Is.Null, "A new week starts with no last move.");
            var moved = FreeTime(3);
            moved.phase = EpisodePhase.HoH;
            Assert.That(EpisodeDirector.LastMoveLine(moved, 7), Is.Null, "Nor is there one once free time is over.");
            var ceremony = FreeTime(3);
            ceremony.events[0].phase = EpisodePhase.Nomination;
            Assert.That(EpisodeDirector.LastMoveLine(ceremony, 7), Is.Null, "A line from outside free time is not a move.");
        }
    }
}
