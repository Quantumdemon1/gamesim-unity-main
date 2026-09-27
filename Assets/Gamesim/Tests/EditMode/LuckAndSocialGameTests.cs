using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The luck and social games as data: the dice's real choice between rolls, and the reference's
    /// Word Scramble - its scoring, its verdicts and its scrambles - with the port's Backspace.
    /// </summary>
    public sealed class LuckAndSocialGameTests
    {
        private const int Rules = CompetitionMiniGames.WidenedRules;

        // ------------------------------------------------------------ the dice

        private static MiniGameRun Dice(uint seed = 11) => new MiniGameRun(CompetitionMiniGames.Kind.Dice, seed, Rules);

        private static int Land(MiniGameRun run)
        {
            run.Tick(MiniGameRun.RollSeconds + .01);
            return run.RollTotal;
        }

        [Test]
        public void ARerollReplacesTheRollYouHave()
        {
            for (uint seed = 1; seed <= 40; seed++)
            {
                var run = Dice(seed);
                Assert.That(run.Roll(), Is.True);
                int first = Land(run);
                Assert.That(run.Roll(), Is.True);
                int second = Land(run);
                int showing = Enumerable.Range(0, MiniGameRun.DiceCount).Sum(run.Face);
                Assert.That(second, Is.EqualTo(showing), "The total is the dice now showing: the roll just made, not the one it replaced.");
                Assert.That(run.Keep(), Is.True);
                Assert.That(run.KeptTotal, Is.EqualTo(showing), "The roll kept is the roll showing, not the better of the two: re-rolling gives the first up.");
                Assert.That(run.Score, Is.EqualTo(CompetitionMiniGames.DiceScore(showing)));
                Assert.That(first, Is.InRange(3, 18));
            }
        }

        [Test]
        public void TheThirdRollStandsWithNoneLeft()
        {
            var run = Dice();
            for (int roll = 1; roll <= MiniGameRun.MaxRolls; roll++)
            {
                Assert.That(run.CanRoll, Is.True, "Roll " + roll + " is there to take.");
                run.Roll();
                if (roll < MiniGameRun.MaxRolls) { Land(run); Assert.That(run.Finished, Is.False, "A roll with more to come waits to be kept or given up."); }
            }
            Assert.That(run.CanRoll, Is.False, "Three rolls and no more.");
            int total = Land(run);
            Assert.That(run.Finished, Is.True, "The third roll lands and stands.");
            Assert.That(run.KeptTotal, Is.EqualTo(total));
            Assert.That(run.Roll(), Is.False);
        }

        [Test]
        public void TheDiceLandOneAfterAnotherAndNothingShowsEarly()
        {
            var run = Dice();
            run.Roll();
            Assert.That(run.Rolling, Is.True);
            Assert.That(run.CanKeep, Is.False, "A tumbling roll cannot be kept.");
            Assert.That(run.CanRoll, Is.False, "or rolled over.");
            Assert.That(run.RollTotal, Is.Zero, "The total is not shown before the dice land.");
            for (int die = 0; die < MiniGameRun.DiceCount; die++)
            {
                Assert.That(run.Face(die), Is.Zero, "Die " + (die + 1) + " shows nothing before it lands.");
                run.Tick(MiniGameRun.DieSettlesAfter(die) - run.Elapsed + .001);
                Assert.That(run.DieLanded(die), Is.True);
                Assert.That(run.Face(die), Is.InRange(1, 6));
                if (die + 1 < MiniGameRun.DiceCount) Assert.That(run.DieLanded(die + 1), Is.False, "The next die is still tumbling.");
            }
            Assert.That(run.Rolling, Is.False);
            Assert.That(run.RollTotal, Is.EqualTo(Enumerable.Range(0, 3).Sum(run.Face)));
        }

        [Test]
        public void TheSameAttemptRollsTheSameDice()
        {
            var a = Dice(99); var b = Dice(99);
            for (int roll = 0; roll < 3; roll++)
            {
                a.Roll(); b.Roll();
                // Pressed at different moments: the dice were dealt when the run was made.
                a.Tick(MiniGameRun.RollSeconds + .01); b.Tick(.3); b.Tick(MiniGameRun.RollSeconds);
                Assert.That(a.RollTotal, Is.EqualTo(b.RollTotal), "Roll " + (roll + 1) + ": a cancel or a reload rolls the same dice.");
            }
        }

        [Test]
        public void TheWhistleKeepsATumblingRollAndNoRollScoresNothing()
        {
            var idle = Dice();
            idle.Tick(idle.TimeLimit + 1);
            Assert.That(idle.Finished, Is.True);
            Assert.That(idle.KeptTotal, Is.Zero);
            Assert.That(idle.Score, Is.Zero, "No roll, no score.");

            var late = Dice();
            late.Tick(late.TimeLimit - .2);
            late.Roll();
            late.Tick(1);
            Assert.That(late.Finished, Is.True);
            Assert.That(late.KeptTotal, Is.InRange(3, 18), "Dice thrown before the whistle land where they were headed.");
            Assert.That(late.Score, Is.EqualTo(CompetitionMiniGames.DiceScore(late.KeptTotal)));
        }

        [Test]
        public void TheKeptTotalIsOutOfTenAsTheReferenceScalesIt()
        {
            Assert.That(CompetitionMiniGames.DiceScore(3), Is.Zero);
            Assert.That(CompetitionMiniGames.DiceScore(18), Is.EqualTo(10));
            Assert.That(CompetitionMiniGames.DiceScore(10), Is.EqualTo(4.67));
            Assert.That(CompetitionMiniGames.DiceScore(0), Is.Zero);
        }

        [Test]
        public void TheLuckAndSocialGamesAreRulesFours()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MiniGameRun(CompetitionMiniGames.Kind.Dice, 1, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MiniGameRun(CompetitionMiniGames.Kind.Words, 1, 3));
            Assert.That(CompetitionMiniGames.For("Luck"), Is.EqualTo(CompetitionMiniGames.Kind.Dice));
            Assert.That(CompetitionMiniGames.For("Social"), Is.EqualTo(CompetitionMiniGames.Kind.Words));
            foreach (CompetitionMiniGames.Kind kind in Enum.GetValues(typeof(CompetitionMiniGames.Kind)))
                if (kind != CompetitionMiniGames.Kind.Precision)
                    Assert.That(CompetitionMiniGames.For(CompetitionMiniGames.CategoryOf(kind)), Is.EqualTo(kind), kind + "'s category plays it.");
            var captions = Enum.GetValues(typeof(CompetitionMiniGames.Kind)).Cast<CompetitionMiniGames.Kind>().Select(CompetitionMiniGames.EnterCaption).ToList();
            Assert.That(captions.Distinct().Count(), Is.EqualTo(captions.Count), "Every game's control has words of its own.");
            Assert.That(new MiniGameRun(CompetitionMiniGames.Kind.Dice, 1, Rules).Definition, Is.SameAs(CompetitionDefinitions.RollTheDice));
            Assert.That(new MiniGameRun(CompetitionMiniGames.Kind.Words, 1, Rules).Definition, Is.SameAs(CompetitionDefinitions.WordScramble));
            Assert.Throws<ArgumentException>(() => new MiniGameRun(CompetitionMiniGames.Kind.Dice, 1, Rules, CompetitionDefinitions.WordScramble),
                "A game plays its own kind's definitions only.");
        }

        // ------------------------------------------------------------ the words

        private static MiniGameRun Words(uint seed = 5, IReadOnlyList<string> words = null) =>
            new MiniGameRun(CompetitionMiniGames.Kind.Words, seed, Rules, null, words);

        /// <summary>The tiles that spell the word, in order: each letter's first tile not yet used.</summary>
        private static List<int> SpellOrder(MiniGameRun run)
        {
            var used = new HashSet<int>();
            var order = new List<int>();
            foreach (char letter in run.Word)
            {
                int index = Enumerable.Range(0, run.Scrambled.Length).First(i => run.Scrambled[i] == letter && !used.Contains(i));
                used.Add(index); order.Add(index);
            }
            return order;
        }

        [Test]
        public void ASpelledWordScoresByItsLengthAndTheNextComesUp()
        {
            var run = Words();
            string word = run.Word;
            foreach (int tile in SpellOrder(run)) Assert.That(run.PickTile(tile), Is.True);
            Assert.That(run.VerdictRight, Is.True, "Spelled right.");
            Assert.That(run.WordsSolved, Is.EqualTo(1));
            Assert.That(run.WordPoints, Is.EqualTo(CompetitionMiniGames.WordPoints(word.Length)));
            Assert.That(run.Word, Is.EqualTo(word), "The word stays up for its verdict.");
            run.Tick(MiniGameRun.VerdictSeconds + .01);
            Assert.That(run.Word, Is.Not.EqualTo(word), "Then the next word comes up.");
            Assert.That(run.Picked, Is.Empty);
        }

        [Test]
        public void AWrongSpellingClearsAfterItsVerdictAndTheWordStays()
        {
            var run = Words();
            string word = run.Word;
            for (int tile = 0; tile < run.Scrambled.Length; tile++) run.PickTile(tile);
            Assert.That(run.Spelled, Is.EqualTo(run.Scrambled));
            Assert.That(run.ShowingVerdict && !run.VerdictRight, Is.True, "The scramble itself is never the word.");
            Assert.That(run.WrongSpellings, Is.EqualTo(1));
            Assert.That(run.PickTile(0), Is.False, "Nothing is taken while a verdict shows.");
            run.Tick(MiniGameRun.VerdictSeconds + .01);
            Assert.That(run.Picked, Is.Empty, "The letters clear.");
            Assert.That(run.Word, Is.EqualTo(word), "and the same word waits.");
            Assert.That(run.WordPoints, Is.Zero);
        }

        [Test]
        public void TypingChoosesTheFirstOpenTileCarryingTheLetter()
        {
            var run = Words();
            char first = run.Word[0];
            Assert.That(run.TypeLetter(char.ToLowerInvariant(first)), Is.True, "Lower case types too.");
            Assert.That(run.Picked.Single(), Is.EqualTo(run.Scrambled.IndexOf(first)));
            char absent = Enumerable.Range('A', 26).Select(c => (char)c).First(c => !run.Scrambled.Contains(c));
            Assert.That(run.TypeLetter(absent), Is.False, "A letter the board does not have does nothing.");
            foreach (char letter in run.Word.Substring(1)) run.TypeLetter(letter);
            Assert.That(run.VerdictRight, Is.True, "The whole word, typed.");
        }

        [Test]
        public void BackspaceAndClearTakeLettersBack()
        {
            var run = Words();
            var order = SpellOrder(run);
            run.PickTile(order[0]); run.PickTile(order[1]);
            Assert.That(run.Undo(), Is.True);
            Assert.That(run.Picked, Is.EqualTo(new[] { order[0] }), "The last letter goes back.");
            run.PickTile(order[1]);
            Assert.That(run.ClearTiles(), Is.True);
            Assert.That(run.Picked, Is.Empty);
            Assert.That(run.Undo(), Is.False, "Nothing to take back.");
            Assert.That(run.WrongSpellings, Is.Zero, "Taking letters back is not a wrong spelling.");
        }

        [Test]
        public void SkippingAWordCostsNothingAndMovesOn()
        {
            var run = Words();
            string word = run.Word;
            Assert.That(run.SkipWord(), Is.True);
            Assert.That(run.Word, Is.Not.EqualTo(word));
            Assert.That(run.WordsSkipped, Is.EqualTo(1));
            Assert.That(run.WordPoints, Is.Zero);
            Assert.That(run.ShowingVerdict, Is.False, "A skip has no verdict to wait out.");
        }

        [Test]
        public void TheScoreIsThePointsCappedAtTen()
        {
            var run = Words(7);
            while (run.WordPoints < 12)
            {
                foreach (int tile in SpellOrder(run)) run.PickTile(tile);
                run.Tick(MiniGameRun.VerdictSeconds + .001);
                Assert.That(run.Finished, Is.False);
            }
            run.Finish();
            Assert.That(run.Score, Is.EqualTo(10), "Past ten, the reference caps it.");
            Assert.That(CompetitionMiniGames.WordsScore(7.5), Is.EqualTo(7.5));
        }

        [Test]
        public void TheWordPointsAreTheReferencesTable()
        {
            Assert.That(CompetitionMiniGames.WordPoints(4), Is.EqualTo(1.5));
            Assert.That(CompetitionMiniGames.WordPoints(5), Is.EqualTo(2));
            Assert.That(CompetitionMiniGames.WordPoints(6), Is.EqualTo(2));
            Assert.That(CompetitionMiniGames.WordPoints(7), Is.EqualTo(2.5));
            Assert.That(CompetitionMiniGames.WordPoints(8), Is.EqualTo(2.5));
            Assert.That(CompetitionMiniGames.WordPoints(9), Is.EqualTo(3));
            Assert.That(CompetitionMiniGames.WordPoints(11), Is.EqualTo(3));
            Assert.That(CompetitionMiniGames.BigBrotherWords.All(word => word.Length >= 5), Is.True, "The reference keeps its list to five letters or more.");
        }

        [Test]
        public void EveryScrambleMovesAtLeastTwoLettersAndKeepsThemAll()
        {
            for (uint seed = 1; seed <= 30; seed++)
            {
                var run = Words(seed);
                for (int word = 0; word < 25; word++)
                {
                    Assert.That(run.Scrambled.OrderBy(c => c), Is.EqualTo(run.Word.OrderBy(c => c)), "The tiles are the word's letters.");
                    int moved = Enumerable.Range(0, run.Word.Length).Count(i => run.Scrambled[i] != run.Word[i]);
                    Assert.That(moved, Is.GreaterThanOrEqualTo(2), run.Word + " scrambled to " + run.Scrambled);
                    Assert.That(run.Word.Length, Is.LessThanOrEqualTo(CompetitionGameScreen.MaxTiles), "The board has a tile for every letter.");
                    run.SkipWord();
                }
            }
        }

        [Test]
        public void TheSameAttemptDealsTheSameWords()
        {
            var a = Words(42); var b = Words(42);
            for (int i = 0; i < 10; i++)
            {
                Assert.That(a.Word, Is.EqualTo(b.Word));
                Assert.That(a.Scrambled, Is.EqualTo(b.Scrambled));
                a.SkipWord(); b.SkipWord();
            }
        }

        [Test]
        public void TheHouseguestScrambleSpellsTheSeasonsFirstNames()
        {
            var names = new[] { "Maya Hassan", "Taylor Kim", "Jo Park", "Anne-Marie Cole", "Casey Wilson", "Jordan Taylor", "Emma Brown" };
            var run = Words(3, names);
            var dealt = new HashSet<string>();
            for (int i = 0; i < 12; i++) { dealt.Add(run.Word); run.SkipWord(); }
            Assert.That(dealt, Is.EquivalentTo(new[] { "MAYA", "TAYLOR", "ANNEMARIE", "CASEY", "JORDAN", "EMMA" }),
                "First names in capitals, letters only; a name under four letters is too short to scramble.");
            Assert.That(CompetitionMiniGames.ScrambleForm("  zoë nguyen "), Is.EqualTo("ZO"), "Only the plain letters a keyboard types.");

            var small = Words(3, new[] { "Jo", "Kai", "Ann" });
            var topped = new HashSet<string>();
            for (int i = 0; i < 10; i++) { topped.Add(small.Word); small.SkipWord(); }
            Assert.That(topped.Count, Is.EqualTo(CompetitionMiniGames.FewestScrambleWords), "A house of short names is topped up from the reference's words.");
            Assert.That(topped.All(word => CompetitionMiniGames.BigBrotherWords.Contains(word)), Is.True);
        }
    }
}
