using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The word board's deal (UI-UX-PASS-PLAN M0). <see cref="MiniGameRun"/> and
    /// <see cref="CompetitionMiniGames"/> read nothing but System and the simulation, so these run
    /// in the dotnet subset on every push as well as in the editor's suite.
    /// </summary>
    public sealed class ScrambleDealTests
    {
        private static MiniGameRun Words(uint seed, IReadOnlyList<string> words = null) =>
            new MiniGameRun(CompetitionMiniGames.Kind.Words, seed, CompetitionMiniGames.WidenedRules, null, words);

        /// <summary>The first <paramref name="count"/> boards a run deals, each as its word and its tiles: "WORD/TILES".</summary>
        private static List<string> Boards(MiniGameRun run, int count)
        {
            var boards = new List<string>();
            for (int i = 0; i < count; i++) { boards.Add(run.Word + "/" + run.Scrambled); run.SkipWord(); }
            return boards;
        }

        /// <summary>
        /// A six-house whose player is "You" has five names long enough to scramble, so the board is
        /// filled out with one house word - after the names, shuffled among themselves, so the first
        /// board is a name under a rule that says names. The board says when it holds house words;
        /// a house with six names deals none, and the plain scramble's words are its own, not a
        /// fill. Every draw is the run's own generator's, so the deal is the same after a cancel or
        /// a reload.
        /// </summary>
        [Test]
        public void TheHouseguestScrambleDealsTheNamesBeforeTheHouseWords()
        {
            var sixHouse = new[] { "You", "Maya Hassan", "Jamie Roberts", "Casey Wilson", "Riley Johnson", "Taylor Kim" };
            var names = new[] { "MAYA", "JAMIE", "CASEY", "RILEY", "TAYLOR" };
            for (uint seed = 1; seed <= 40; seed++)
            {
                var run = Words(seed, sixHouse);
                Assert.That(run.DealsHouseWords, Is.True, "Five names fall short of six, so a house word fills the list.");
                var dealt = new List<string>();
                for (int i = 0; i < 6; i++) { dealt.Add(run.Word); run.SkipWord(); }
                Assert.That(names, Does.Contain(dealt[0]), "Seed " + seed + ": the first board is a name, not " + dealt[0] + ".");
                Assert.That(dealt.Take(5), Is.EquivalentTo(names), "Seed " + seed + ": every name is dealt before any house word: " + string.Join(", ", dealt));
                Assert.That(CompetitionMiniGames.BigBrotherWords, Does.Contain(dealt[5]), "Seed " + seed + ": the house word follows them.");
                Assert.That(names, Does.Contain(run.Word), "Seed " + seed + ": and the pool comes round to the names again.");
                var again = Words(seed, sixHouse);
                Assert.That(again.Word, Is.EqualTo(dealt[0]), "Seed " + seed + ": the same attempt deals the same first name.");
            }
            Assert.That(Words(3, new[] { "Maya Hassan", "Taylor Kim", "Casey Wilson", "Jordan Taylor", "Emma Brown", "Alex Chen" }).DealsHouseWords, Is.False,
                "Six names need no house word.");
            Assert.That(Words(3).DealsHouseWords, Is.False, "The plain scramble's words are its own.");
            Assert.That(Words(3, new[] { "Jo", "Kai", "Ann" }).DealsHouseWords, Is.True, "A house of short names is all house words.");
        }

        /// <summary>
        /// A house of six names or more, and the plain scramble, deal exactly as they did before
        /// M0: these are the boards - words and tiles - 8414078 deals for the same runs, printed by a
        /// program built against that commit's own sources, not worked out. The names-first deal
        /// moves only a fill, and these have none; a change that spent one more draw of the run's
        /// generator anywhere in the deal would move every tile after it.
        /// </summary>
        [Test]
        public void SixNamesOrMoreDealExactlyAsBefore()
        {
            var seven = new[] { "Maya Hassan", "Taylor Kim", "Jo Park", "Anne-Marie Cole", "Casey Wilson", "Jordan Taylor", "Emma Brown" };
            var named = Words(3, seven);
            Assert.That(named.DealsHouseWords, Is.False, "Six names - Jo is too short to scramble - need no house word.");
            CollectionAssert.AreEqual(
                new[] { "ANNEMARIE/EAIANRNEM", "CASEY/ACYES", "EMMA/AMEM", "TAYLOR/RATYOL", "MAYA/AMYA", "JORDAN/ONAJDR" },
                Boards(named, 6), "The seven names on seed 3 deal as 8414078 dealt them.");
            CollectionAssert.AreEqual(
                new[] { "BLOCK/BCKOL", "CEREMONY/YNREEOCM", "COMPETITION/NEOIMICTPOT", "FINAL/LFINA", "BACKDOOR/BDOAROKC", "POWER/WPORE" },
                Boards(Words(3), 6), "The plain scramble on seed 3 deals as 8414078 dealt it.");
        }
    }
}
