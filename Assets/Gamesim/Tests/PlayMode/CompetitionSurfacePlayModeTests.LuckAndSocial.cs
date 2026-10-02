using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The luck and social boards on the competition surface: the dice's two choices by pointer,
    /// keyboard and pad, a tumble that gives nothing away, and the word board that types - P
    /// included - takes a letter back, and keeps the keyboard on a letter it can still choose.
    /// </summary>
    public sealed partial class CompetitionSurfacePlayModeTests
    {
        private const int Widened = CompetitionMiniGames.WidenedRules;

        private IEnumerator ShowLive(MiniGameRun run, bool practice = true)
        {
            screen.Show(run, "Head of Household · " + run.Definition.Title, "Player", practice, i => {}, () => {}, d => {}, () => {}, () => screen.Hide());
            yield return null; screen.AdvanceReady(4f); yield return null;
            Assert.That(screen.IsPlaying, Is.True, "The board is live.");
        }

        private Button Control(string name) => screen.GetComponentsInChildren<Button>().SingleOrDefault(button => button.name == name);

        private void Land(MiniGameRun run) { run.Tick(MiniGameRun.RollSeconds + .01); screen.Refresh(); }

        [UnityTest]
        public IEnumerator Dice_RollAndKeepAreTheBoardsControlsByKeyboardAndPad()
        {
            CreateInput();
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Dice, 7, Widened);
            yield return ShowLive(run);
            AssertSelected("Roll dice");
            Assert.That(Control("Keep roll"), Is.Null, "There is nothing to keep before the first roll.");
            Assert.That(Control("Roll dice").GetComponentInChildren<TMP_Text>().text, Is.EqualTo(CompetitionGameScreen.RollCaption));

            yield return KeyPress(Key.Enter);
            Assert.That(run.RollsUsed, Is.EqualTo(1), "Enter rolls.");
            screen.Refresh();
            Assert.That(Text("Dice total").text, Is.EqualTo("Rolling..."), "No total while the dice tumble.");
            Assert.That(screen.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Die 1").GetComponentInChildren<TMP_Text>().text,
                Is.EqualTo("Die 1: rolling"), "and no face named before it lands.");
            Land(run);
            int total = run.RollTotal;
            Assert.That(Text("Dice total").text, Is.EqualTo("Total " + total));
            for (int die = 0; die < MiniGameRun.DiceCount; die++)
                Assert.That(screen.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Die " + (die + 1))
                    .GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Die " + (die + 1) + ": " + run.Face(die)), "Each die says its face.");
            Assert.That(Text("Rolls left").text, Does.Contain("Rolling again gives up this " + total), "The choice says what it costs.");
            Assert.That(Control("Roll dice").GetComponentInChildren<TMP_Text>().text, Is.EqualTo(CompetitionGameScreen.RollAgainCaption));

            yield return PadPress(GamepadButton.DpadRight);
            AssertSelected("Keep roll");
            yield return PadPress(GamepadButton.South);
            Assert.That(run.Finished, Is.True, "A keeps the roll.");
            Assert.That(run.KeptTotal, Is.EqualTo(total));
        }

        [UnityTest]
        public IEnumerator Dice_ATumbleShowsNothingUnderReducedMotionAndTheThirdRollStands()
        {
            CreateInput();
            screen.ReducedMotion = true;
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Dice, 7, Widened);
            yield return ShowLive(run);
            for (int roll = 0; roll < 2; roll++)
            {
                yield return KeyPress(Key.Enter);
                screen.Refresh();
                var pips = screen.GetComponentsInChildren<Image>().Where(image => image.name.StartsWith("Pip "));
                Assert.That(pips, Is.Empty, "Under reduced motion a tumbling die is blank: no flicker, and no face before it lands.");
                Land(run);
                Assert.That(screen.GetComponentsInChildren<Image>().Count(image => image.name.StartsWith("Pip ")), Is.EqualTo(run.RollTotal > 0
                    ? Enumerable.Range(0, 3).Sum(run.Face) : 0), "Landed, each die shows its pips: one per point.");
                if (roll == 0) AssertSelected("Roll dice");
            }
            // One roll left: rolling it gives the second roll up for good.
            Assert.That(run.CanRoll, Is.True);
            Select("Roll dice");
            yield return KeyPress(Key.Enter);
            Land(run);
            Assert.That(run.Finished, Is.True, "The third roll stands.");
            Assert.That(run.KeptTotal, Is.EqualTo(run.RollTotal));
        }

        [UnityTest]
        public IEnumerator Words_TypingSpellsAndPIsALetterWhileTheBoardIsPlayed()
        {
            CreateInput();
            uint seed = 1;
            while (!new MiniGameRun(CompetitionMiniGames.Kind.Words, seed, Widened).Word.Contains("P")) seed++;
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Words, seed, Widened);
            yield return ShowLive(run);
            string word = run.Word;
            foreach (char letter in word)
            {
                yield return KeyPress((Key)((int)Key.A + (letter - 'A')));
                Assert.That(screen.Paused, Is.False, "Typing " + letter + " spells; it does not pause.");
            }
            screen.Refresh();
            Assert.That(run.VerdictRight, Is.True, word + " typed out.");
            Assert.That(Text("Spelled letters").text, Is.EqualTo(word));
            run.Tick(MiniGameRun.VerdictSeconds + .01); screen.Refresh();
            Assert.That(run.Word, Is.Not.EqualTo(word));
            Assert.That(Text("Words solved").text, Does.StartWith("1 word"));

            yield return KeyPress((Key)((int)Key.A + (run.Word[0] - 'A')));
            Assert.That(run.Picked.Count, Is.EqualTo(1));
            yield return KeyPress(Key.Backspace);
            Assert.That(run.Picked, Is.Empty, "Backspace takes it back.");
            yield return KeyPress((Key)((int)Key.A + (run.Word[0] - 'A')));
            yield return PadPress(GamepadButton.West);
            Assert.That(run.Picked, Is.Empty, "and so does the pad's X.");

            yield return PadPress(GamepadButton.Start);
            Assert.That(screen.Paused, Is.True, "Start pauses the word board.");
            screen.Refresh();
            Assert.That(screen.GetComponentsInChildren<Button>(true).Where(b => b.name.StartsWith("Letter tile ") && b.gameObject.activeInHierarchy)
                .All(b => b.GetComponentInChildren<TMP_Text>().text == "?"), Is.True, "A paused board hides its letters: a pause is not time to think.");
            yield return KeyPress(Key.P);
            Assert.That(screen.Paused, Is.False, "Paused, P resumes: there is nothing to spell.");
        }

        [UnityTest]
        public IEnumerator Words_TheKeyboardStaysOnALetterItCanStillChooseAndReachesClearAndSkip()
        {
            CreateInput();
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Words, 9, Widened);
            yield return ShowLive(run);
            AssertSelected("Letter tile 1");
            yield return KeyPress(Key.Enter);
            Assert.That(run.Picked, Is.EqualTo(new[] { 0 }), "Enter chooses the letter under the keyboard.");
            screen.Refresh(); yield return null;
            AssertSelected("Letter tile 2");
            yield return KeyPress(Key.DownArrow);
            AssertSelected("Clear letters");
            yield return KeyPress(Key.Enter);
            Assert.That(run.Picked, Is.Empty, "Clear letters clears them.");
            yield return KeyPress(Key.RightArrow);
            AssertSelected("Skip word");
            string word = run.Word;
            yield return KeyPress(Key.Enter);
            Assert.That(run.Word, Is.Not.EqualTo(word), "Skip word moves on.");
            Assert.That(run.WordsSkipped, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Words_TheLegendSaysTypingSpellsAndOffersNoPauseKey()
        {
            CreateInput();
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Words, 3, Widened);
            yield return ShowLive(run);
            string legend = string.Join(" | ", Rect("Control legend").GetComponentsInChildren<TMP_Text>().Select(text => text.text));
            Assert.That(legend, Does.Contain("A–Z").And.Contain("Spell").And.Contain("Backspace"));
            Assert.That(legend, Does.Not.Contain("| P |"), "P spells on this board, so the legend offers no P: " + legend);
            yield return PadPress(GamepadButton.South);
            legend = string.Join(" | ", Rect("Control legend").GetComponentsInChildren<TMP_Text>().Select(text => text.text));
            Assert.That(legend, Does.Contain("Start").And.Contain("Pause"), "With the pad, Start pauses: " + legend);
        }

        [UnityTest]
        public IEnumerator Words_TheLongestNameFitsItsTilesAtLargeText()
        {
            owner = new GameObject("Words fit"); screen = CompetitionGameScreen.Attach(owner); screen.FontScale = 1.2f;
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Words, 3, Widened, Gamesim.Simulation.CompetitionDefinitions.HouseguestScramble,
                new[] { "Christabelle Moon", "Maya Hassan", "Taylor Kim", "Casey Wilson", "Jordan Taylor", "Emma Brown" });
            while (run.Word.Length < CompetitionMiniGames.LongestScrambleWord) run.SkipWord();
            yield return ShowLive(run);
            Canvas.ForceUpdateCanvases();
            var area = Rect("Game surface").rect;
            var tiles = screen.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Letter tile ")).ToList();
            Assert.That(tiles.Count, Is.EqualTo(CompetitionMiniGames.LongestScrambleWord));
            foreach (var tile in tiles)
            {
                var rect = (RectTransform)tile.transform;
                Assert.That(rect.anchoredPosition.x, Is.GreaterThanOrEqualTo(0f), tile.name + " starts on the board.");
                Assert.That(rect.anchoredPosition.x + rect.rect.width, Is.LessThanOrEqualTo(area.width + .5f), tile.name + " ends on the board.");
                var letter = tile.GetComponentInChildren<TMP_Text>();
                letter.ForceMeshUpdate();
                Assert.That(letter.isTextOverflowing, Is.False, tile.name + "'s letter fits.");
                Assert.That(letter.text.Length, Is.EqualTo(1));
            }
        }

        /// <summary>
        /// The rules line says what the pool holds (UI-UX-PASS-PLAN M0): a six-house whose player is
        /// "You" has five names to deal, so the line says house words follow them; a house with six
        /// names says nothing of the kind. The authored summary stays in both, and at the larger text
        /// the longer line still fits its header: the rules shrink to their floor before they cut.
        /// </summary>
        [UnityTest]
        public IEnumerator Words_TheRulesSayWhenHouseWordsFollowTheNames([Values(1f, 1.2f)] float scale)
        {
            owner = new GameObject("Words rules"); screen = CompetitionGameScreen.Attach(owner); screen.FontScale = scale;
            var filled = new MiniGameRun(CompetitionMiniGames.Kind.Words, 3, Widened, Gamesim.Simulation.CompetitionDefinitions.HouseguestScramble,
                new[] { "You", "Maya Hassan", "Jamie Roberts", "Casey Wilson", "Riley Johnson", "Taylor Kim" });
            Assert.That(filled.DealsHouseWords, Is.True, "Five names fall short of six.");
            yield return ShowLive(filled);
            Canvas.ForceUpdateCanvases();
            var rules = Text("Rules");
            Assert.That(rules.text, Does.Contain(filled.Definition.Summary).And.Contain(CompetitionGameScreen.HouseWordsFollowTheNames),
                "The rules name the house words that follow the names.");
            rules.ForceMeshUpdate();
            Assert.That(rules.isTextTruncated, Is.False, "Every word of the longer line is drawn at text scale " + scale + ", at " + rules.fontSize + " pt.");

            var full = new MiniGameRun(CompetitionMiniGames.Kind.Words, 3, Widened, Gamesim.Simulation.CompetitionDefinitions.HouseguestScramble,
                new[] { "Maya Hassan", "Jamie Roberts", "Casey Wilson", "Riley Johnson", "Taylor Kim", "Jordan Lee" });
            Assert.That(full.DealsHouseWords, Is.False, "Six names need no house word.");
            yield return ShowLive(full);
            Canvas.ForceUpdateCanvases();
            rules = Text("Rules");
            Assert.That(rules.text, Does.Contain(full.Definition.Summary).And.Not.Contain("house words"), "and the line says nothing of them.");
            rules.ForceMeshUpdate();
            Assert.That(rules.isTextTruncated, Is.False, "and is drawn whole at text scale " + scale + ".");
        }

        private static void Select(string name)
        {
            var target = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Single(button => button.name == name);
            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }
    }
}
