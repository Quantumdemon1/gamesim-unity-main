using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Menu slice 2, part d, in words: every way back on the decisions, the finale, the jury house,
    /// the competitions' cards and the conversations is worded "Back to &lt;place&gt;", the jury house's
    /// names the panel it really goes back to, and the decision's summary of the week counts in the
    /// words the house's own reading uses (one name per thing).
    /// </summary>
    public sealed class MenuOneWayBackDecisionsWordsTests
    {
        [Test]
        public void EveryWayBack_IsWordedBackToAPlace()
        {
            foreach (var caption in new[]
            {
                EpisodeHud.BackToNomineesCaption, EpisodeDirector.BackToStoryChoicesCaption, EpisodeDirector.BackToPreparationCaption,
                EpisodeDirector.BackToFinaleCaption, EpisodeDirector.BackToQuestioningCaption, EpisodeDirector.BackToSpeechesCaption,
                EpisodeDirector.ConversationTopicsBackCaption, CompetitionGameScreen.BackToBriefingCaption, CompetitionResult.BackToStandingsCaption,
            })
                Assert.That(WayOut.IsWayBack(caption), Is.True, caption);
            Assert.That(CompetitionGameScreen.BackToBriefingCaption, Is.EqualTo("Back to the briefing"),
                "The game's, the assembly's and a practice's way back: one wording, where it said 'Back to briefing' and 'Return to briefing'.");
            Assert.That(CompetitionResult.BackToStandingsCaption, Is.EqualTo("Back to the standings"));
            Assert.That(EpisodeDirector.BackToStoryChoicesCaption, Is.EqualTo("Back to the choices"),
                "A story's second press names where it goes back to, not a bare 'Back'.");
        }

        [Test]
        public void TheJuryHouse_GoesBackToThePanelItWasOpenedOver()
        {
            Assert.That(EpisodeDirector.JuryHouseBackCaption(new EpisodeState { phase = EpisodePhase.JuryQuestioning }),
                Is.EqualTo("Back to the questioning"));
            Assert.That(EpisodeDirector.JuryHouseBackCaption(new EpisodeState { phase = EpisodePhase.FinalSpeeches }),
                Is.EqualTo("Back to the speeches"));
            Assert.That(EpisodeDirector.JuryHouseBackCaption(new EpisodeState { phase = EpisodePhase.Social }),
                Is.EqualTo(EpisodeDirector.BackToPreparationCaption), "At three, the window: endgame preparation.");
        }

        [Test]
        public void TheWeeksSummary_CountsInTheHousesOwnWords()
        {
            var state = ContentCatalog.Create(7);
            var reading = HouseVibe.Of(state);
            string summary = DecisionContext.KnownEventSummary(state);
            Assert.That(summary, Is.EqualTo(
                DecisionContext.Counted(reading.Activity, DecisionContext.SummaryConversationWord, DecisionContext.SummaryConversationsWord) + " · "
                + DecisionContext.Counted(reading.Commitments, DecisionContext.SummaryDealWord, DecisionContext.SummaryDealsWord) + " · "
                + DecisionContext.Counted(reading.GameStakes, DecisionContext.SummaryMoveWord, DecisionContext.SummaryMovesWord) + " this week"),
                "The house reading's three counts, each in its row's word.");
            Assert.That(summary, Does.Not.Contain("commitments").And.Not.Contain("activity").And.Not.Contain("game stakes"),
                "The event schema's category names are not the player's words.");
            Assert.That(DecisionContext.SummaryDealsWord, Is.EqualTo("deals & alliances"),
                "The same count the house's reading calls 'Deals & alliances' (HouseVibe).");
            Assert.That(EpisodeDirector.NominationBudgetLabel, Is.EqualTo("ACTIONS LEFT"),
                "The nomination's budget card names the weekly budget as the top bar does, where it said 'conversations left'.");
        }

        [Test]
        public void TheFinalCase_GoesBackToThePanelAndSaysWhenAChoiceIsLetGo()
        {
            string questioning = EpisodeDirector.FinalCaseBackCaption(new EpisodeState { phase = EpisodePhase.JuryQuestioning }, false);
            Assert.That(questioning, Is.EqualTo("Back to the questioning"), "Over the questioning, back to it; nothing chosen, nothing said.");
            string window = EpisodeDirector.FinalCaseBackCaption(new EpisodeState { phase = EpisodePhase.Social }, true);
            Assert.That(window, Is.EqualTo("Back to endgame preparation (choice discarded)"),
                "At three, back to the window, saying the unlocked choice goes - where a row said 'Close your final case'.");
            Assert.That(WayOut.IsWayBack(questioning) && WayOut.IsWayBack(window), Is.True);
        }

        [Test]
        public void AConversationsInnerWaysBack_NameTheLevelTheyGoBackTo()
        {
            Assert.That(EpisodeDirector.ConversationChoicesBackCaption, Is.EqualTo("Back to the choices"),
                "With a verb's people open, back to the page's choices, as Escape goes.");
            Assert.That(EpisodeDirector.ConversationAsksBackCaption, Is.EqualTo("Back to the asks"),
                "With a plea's approaches open, back to what to ask, where a row said 'Ask something else'.");
            foreach (var caption in new[] { EpisodeDirector.ConversationChoicesBackCaption, EpisodeDirector.ConversationAsksBackCaption })
                Assert.That(WayOut.IsWayBack(caption), Is.True, caption);
        }

        [Test]
        public void TheWeeksSummary_SaysOneInTheSingular()
        {
            Assert.That(DecisionContext.Counted(1, DecisionContext.SummaryMoveWord, DecisionContext.SummaryMovesWord), Is.EqualTo("1 game move"));
            Assert.That(DecisionContext.Counted(1, DecisionContext.SummaryDealWord, DecisionContext.SummaryDealsWord), Is.EqualTo("1 deal or alliance"));
            Assert.That(DecisionContext.Counted(0, DecisionContext.SummaryConversationWord, DecisionContext.SummaryConversationsWord), Is.EqualTo("0 conversations"));
            Assert.That(DecisionContext.Counted(2, DecisionContext.SummaryMoveWord, DecisionContext.SummaryMovesWord), Is.EqualTo("2 game moves"));
        }
    }
}
