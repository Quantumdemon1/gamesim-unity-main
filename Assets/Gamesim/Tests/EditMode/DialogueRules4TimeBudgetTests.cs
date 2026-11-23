using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Dialogue rules 4's conversation time, the review's two corrections: a -1 in an earlier window of the week moves that
    /// window's seats for the later windows too, so an extra spent past them is used and not offered again; and the advance
    /// note's "unused actions will be lost" counts the open window's receipt as its own seats, as the free-time board does.
    /// On the rules-4 campaign of DialogueRules4TimeTests, with receipts placed by hand. Shared cases: the dotnet subset runs them.
    /// </summary>
    public sealed class DialogueRules4TimeBudgetTests
    {
        // the actions spent in the nominations window (one seat, a -1 there), the week's extras, the campaign's budget
        [TestCase(1, 1, 2)]
        [TestCase(1, 2, 3)]
        [TestCase(2, 2, 2)]
        public void AnExtraSpentInAMinusOneWindowIsUsed(int spentThere, int extras, int budget)
        {
            var s = DialogueRules4TimeTests.QuietCampaign();
            Assert.That(EpisodeEngine.Window(s), Is.EqualTo(Windows.AfterVeto));
            s.boughtActionPoints = extras;
            for (int w = 0; w < s.windowActions.Count; w++) s.windowActions[w] = 0;
            s.windowActions[Windows.AfterNominations] = spentThere;
            DialogueRules4TimeTests.Receipt(s, s.week, Windows.AfterNominations, -1);
            Assert.That(EpisodeEngine.SocialActionBudget(s), Is.EqualTo(budget));
            Assert.That(spentThere + EpisodeEngine.SocialActionBudget(s), Is.EqualTo(1 + 2 + extras - 1),
                "The week's two windows hold their seats and extras, less the -1.");
        }

        // the campaign window's receipt, the week's extras, what the advance note says is lost
        [TestCase(1, 1, 3)]
        [TestCase(-1, 1, 1)]
        public void TheAdvanceNoteCountsTheWindowsReceipt(int receipt, int extras, int lost)
        {
            var s = DialogueRules4TimeTests.QuietCampaign();
            Assert.That(s.Find(s.playerId).status, Is.EqualTo(ContestantStatus.Active));
            Assert.That(EpisodeEngine.Window(s), Is.EqualTo(Windows.AfterVeto));
            s.boughtActionPoints = extras;
            for (int w = 0; w < s.windowActions.Count; w++) s.windowActions[w] = 0;
            DialogueRules4TimeTests.Receipt(s, s.week, Windows.AfterVeto, receipt);
            Assert.That(WaitingOnYou.AdvanceClosesWindow(s), Is.True);
            Assert.That(WaitingOnYou.ActionsLostOnAdvance(s), Is.EqualTo(lost));
        }
    }
}
