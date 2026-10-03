using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Your word in the house on screen (ACTIONS-DEALS-ALLIANCES-PLAN C8, decision 14), in a house that
    /// plays the commitment rules and keeps knowledge. The house's reading of the player's word opens
    /// the Your word page - an eyebrow carrying the page's mark, then a card headed by the reading in
    /// words and in its colour, who has heard, and each breach the house knows of with who knows it -
    /// and ends the note over a conversation's chances, which take the same term. With nothing broken
    /// the card reads "Your word is good" over the page's empty state. Nothing is a control, and without
    /// the rules there is no reading anywhere. Photographed in a batch run as 'your-word-in-the-house',
    /// 'your-word-good' and 'conversation-word-heard', with '-large' forms and the conversation's
    /// '-4x3'. The engine's half is YourWordInTheHouseTests.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private const string WordHeardDealId = "deal-word-heard";

        /// <summary>
        /// The story's knowledge, the commitment rules when <paramref name="rules"/>, and a safety deal the
        /// player broke with the first houseguest - under the rules in front of the house, a fact the two
        /// of them hold that two more houseguests have heard of.
        /// </summary>
        private static System.Action<EpisodeState> WordHeardOf(bool rules) => state =>
        {
            EpisodeEngine.EnableStory(state);
            if (rules) EpisodeEngine.EnableCommitments(state);
            var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            var deal = new DealState
            {
                id = WordHeardDealId, type = DealKind.SafetyAgreement, proposerId = state.playerId, recipientId = npcs[0],
                status = DealStatus.Broken, week = state.week, expiresWeek = state.week, trustImpact = DealKind.DefaultTrust(DealKind.SafetyAgreement),
                brokenById = rules ? state.playerId : null, settledWeek = rules ? state.week : 0,
            };
            state.deals.Add(deal);
            if (!rules) return;
            var fact = Knowledge.BrokenWord(state, deal, state.playerId, npcs[0]);
            Knowledge.AddKnower(state, fact, npcs[1]);
            Knowledge.AddKnower(state, fact, npcs[2]);
        };

        /// <summary>The story's knowledge and the commitment rules, and nothing of the player's broken: the reading's default state.</summary>
        private static void WordUnheard(EpisodeState state)
        {
            EpisodeEngine.EnableStory(state);
            EpisodeEngine.EnableCommitments(state);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator WordInTheHouse_TheReadingHeadsYourWordAndEndsTheOddsNote()
        {
            foreach (bool larger in new[] { false, true })
            {
                string size = larger ? " at the larger text" : "";
                yield return InstallTalkingHouse(8, false, WordHeardOf(true));
                yield return SettleCast();
                yield return ApplyTextSize(larger);
                var state = director.Snapshot;
                Assert.That(YourWord.On(state), Is.True, "Precondition: the house keeps the player's word.");
                Assert.That(YourWord.Hearings(state), Is.EqualTo(3), "Precondition: three have heard of the breach.");
                string odds = YourWord.OddsLine(state);
                Assert.That(odds, Is.Not.Null);

                // The conversation: the one note over the chances says whose read they are first, and the reading last.
                var listener = Listener(state);
                yield return TalkTo(listener.id);
                string where = "A conversation with the player's word heard of" + size;
                Assert.That(director.IsConversationOpen, Is.True, where + ": the conversation opens.");
                var notes = LiveRects(EpisodeHud.OddsNoteName);
                Assert.That(notes, Has.Count.EqualTo(1), where + ": one note over the conversation's chances.");
                string note = notes[0].GetComponent<TMP_Text>().text;
                Assert.That(note, Does.StartWith(EpisodeDirector.OddsReadLine(FinalistRead.FirstName(listener.name))), where + ": whose read the chances are, first,");
                Assert.That(note, Does.EndWith(odds), where + ": and the reading the roll takes too, last.");
                AssertConversationFits(larger, where);
                AssertTagsHaveRoom(where);
                yield return CaptureConversation("conversation-word-heard" + (larger ? "-large" : ""), where, null);
                director.ClosePanels();
                yield return null;

                // The page: the reading first, its eyebrow carrying the page's mark, then the commitments.
                yield return OpenNotebook();
                ButtonWithCaption(EpisodeDirector.YourWordCaption).onClick.Invoke();
                yield return null; yield return null;
                where = "Your word with the player's word heard of" + size;
                Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Word), where + ": the page opens.");
                Canvas.ForceUpdateCanvases();
                var card = ActiveRect(EpisodeHud.WordReadingCardName);
                Assert.That(card, Is.Not.Null, where + ": the house's reading has its card.");
                var title = card.GetComponentsInChildren<TMP_Text>().Single(label => label.name == EpisodeHud.WordReadingTitleName);
                Assert.That(title.text, Is.EqualTo(YourWord.Title(state)), where + ": headed by the reading in words,");
                Assert.That(title.color, Is.EqualTo(EpisodeDirector.WordReadingInk(YourWord.Word(state))), where + ": and in its colour;");
                string words = Words(card);
                Assert.That(words, Does.Contain(YourWord.Summary(state)), where + ": who has heard,");
                var lines = YourWord.Lines(state);
                Assert.That(lines, Has.Count.EqualTo(1));
                Assert.That(words, Does.Contain(lines[0]), where + ": and the breach the house knows of, with who knows it.");
                var mark = ActiveRect(EpisodeDirector.NotebookSection.Word);
                Assert.That(mark, Is.Not.Null, where + " carries its mark,");
                Assert.That(mark.parent, Is.SameAs(card.parent), where + ": in the page's column,");
                Assert.That(mark.GetSiblingIndex(), Is.EqualTo(card.GetSiblingIndex() - 1), where + ": on the eyebrow directly over the reading.");
                Assert.That(mark.GetComponent<TMP_Text>().text, Is.EqualTo(EpisodeDirector.WordInTheHouseHeading));
                var settled = ActiveRect(EpisodeHud.WordSettledCardPrefix + state.Find(state.deals.Single(d => d.id == WordHeardDealId).recipientId).name);
                Assert.That(settled, Is.Not.Null, where + ": the broken deal is settled, under its houseguest.");
                Assert.That(ScreenRect(card).yMin, Is.GreaterThanOrEqualTo(ScreenRect(settled).yMax - 1f), where + ": the reading comes first.");
                Assert.That(card.GetComponentsInChildren<Button>(), Is.Empty, where + ": the reading is not a control.");
                AssertEveryLabelDraws(card, where);
                // Every label at least 1.3 times its words, by the size an auto-sized one is set at, and drawn whole.
                foreach (var label in card.GetComponentsInChildren<TMP_Text>()) AssertLineHasRoom(label, where);
                AssertEveryLabelDraws(ActiveRect("Episode panel"), where);
                if (Application.isBatchMode) yield return CaptureFraming("your-word-in-the-house" + (larger ? "-large" : ""));
                director.ClosePanels();
                yield return null;
            }

            // Under the rules with nothing broken: the page's default state, "Your word is good" over the
            // empty state, the eyebrow carrying the mark.
            foreach (bool larger in new[] { false, true })
            {
                string size = larger ? " at the larger text" : "";
                yield return InstallTalkingHouse(8, false, WordUnheard);
                yield return SettleCast();
                yield return ApplyTextSize(larger);
                var clean = director.Snapshot;
                Assert.That(YourWord.On(clean), Is.True, "Precondition: the house keeps the player's word.");
                Assert.That((YourWord.Word(clean), YourWord.Summary(clean)), Is.EqualTo((YourWord.Good, YourWord.NothingHeard)));
                yield return OpenNotebook();
                ButtonWithCaption(EpisodeDirector.YourWordCaption).onClick.Invoke();
                yield return null; yield return null;
                string where = "Your word with nothing broken" + size;
                Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Word), where + ": the page opens.");
                Canvas.ForceUpdateCanvases();
                var card = ActiveRect(EpisodeHud.WordReadingCardName);
                Assert.That(card, Is.Not.Null, where + ": the reading has its card.");
                var title = card.GetComponentsInChildren<TMP_Text>().Single(label => label.name == EpisodeHud.WordReadingTitleName);
                Assert.That(title.text, Is.EqualTo("Your word is good"), where + ": headed 'Your word is good',");
                Assert.That(title.color, Is.EqualTo(EpisodeDirector.WordReadingInk(YourWord.Good)), where + ": in the allied green;");
                Assert.That(Words(card), Does.Contain(YourWord.NothingHeard), where + ": the house has heard of nothing.");
                var empty = ActiveRect("No commitments");
                Assert.That(empty, Is.Not.Null, where + ": over the page's empty state.");
                Assert.That(ScreenRect(card).yMin, Is.GreaterThanOrEqualTo(ScreenRect(empty).yMax - 1f), where + ": the reading comes first.");
                var mark = ActiveRect(EpisodeDirector.NotebookSection.Word);
                Assert.That(mark, Is.Not.Null, where + " carries its mark,");
                Assert.That(mark.GetSiblingIndex(), Is.EqualTo(card.GetSiblingIndex() - 1), where + ": on the eyebrow directly over the reading.");
                Assert.That(card.GetComponentsInChildren<Button>(), Is.Empty, where + ": the reading is not a control.");
                foreach (var label in card.GetComponentsInChildren<TMP_Text>()) AssertLineHasRoom(label, where);
                AssertEveryLabelDraws(ActiveRect("Episode panel"), where);
                if (Application.isBatchMode) yield return CaptureFraming("your-word-good" + (larger ? "-large" : ""));
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);

            // Without the rules the same breach is no reading: no card, and the note says what it always said.
            yield return InstallTalkingHouse(8, false, WordHeardOf(false));
            yield return SettleCast();
            var plain = director.Snapshot;
            Assert.That(YourWord.On(plain), Is.False);
            var them = Listener(plain);
            yield return TalkTo(them.id);
            Assert.That(director.IsConversationOpen, Is.True, "Without the rules: the conversation opens.");
            var plainNotes = LiveRects(EpisodeHud.OddsNoteName);
            Assert.That(plainNotes, Has.Count.EqualTo(1));
            Assert.That(plainNotes[0].GetComponent<TMP_Text>().text, Does.Not.Contain("Your word is"), "Without the rules the note says nothing of a reading.");
            director.ClosePanels();
            yield return null;
            yield return OpenNotebook();
            ButtonWithCaption(EpisodeDirector.YourWordCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Word));
            Assert.That(ActiveRect(EpisodeHud.WordReadingCardName), Is.Null, "and the page has no reading.");
            Assert.That(ActiveRect(EpisodeDirector.NotebookSection.Word), Is.Not.Null, "The page keeps its mark.");
            director.ClosePanels();
            yield return null;
        }
    }
}
