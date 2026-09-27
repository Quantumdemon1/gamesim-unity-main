using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The social game: the reference's Word Scramble. Letters of a Big Brother word - or, in the
    /// houseguest variant, a houseguest's name - come as shuffled tiles, and the word is spelled by
    /// choosing them in order: a click, a typed letter, or the pad's A on a tile.
    ///
    /// <para>The reference's rules: a finished spelling is checked at once; a right one scores by its
    /// length (2 for five or six letters, 2.5 for seven or eight, 3 for nine or more, 1.5 for a
    /// shorter name) and the next word comes up; a wrong one clears after half a second. A word can be
    /// skipped for nothing. Thirty seconds, and the total, capped at ten, is the score. Backspace, which
    /// takes back the last letter, is this port's: a keyboard needs it.</para>
    ///
    /// <para>The order of the words and every scramble come from the run's own generator, so a ranked
    /// attempt deals the same words after a cancel or a reload, as every board does.</para>
    /// </summary>
    public sealed partial class MiniGameRun
    {
        /// <summary>How long a spelling's verdict shows before the tiles move on.</summary>
        public const double VerdictSeconds = .5;

        private readonly List<string> wordPool = new List<string>();
        private readonly List<int> picked = new List<int>();
        private int wordCursor = -1;
        private double verdictUntil = -1;
        private bool verdictRight;

        /// <summary>The word being spelled, in capitals.</summary>
        public string Word { get; private set; } = "";

        /// <summary>Its letters as the tiles show them.</summary>
        public string Scrambled { get; private set; } = "";

        /// <summary>The tiles chosen so far, in the order they were chosen.</summary>
        public IReadOnlyList<int> Picked => picked;

        /// <summary>The letters chosen so far, as a word.</summary>
        public string Spelled => new string(picked.Select(index => Scrambled[index]).ToArray());

        public int WordsSolved { get; private set; }
        public int WordsSkipped { get; private set; }
        public int WrongSpellings { get; private set; }
        public double WordPoints { get; private set; }

        /// <summary>Whether a verdict is showing: the tiles take nothing until it clears.</summary>
        public bool ShowingVerdict => verdictUntil >= 0 && Elapsed < verdictUntil;

        /// <summary>Whether the verdict showing is a right spelling.</summary>
        public bool VerdictRight => ShowingVerdict && verdictRight;

        public bool TilePicked(int index) => picked.Contains(index);

        private void DealWords(IReadOnlyList<string> words)
        {
            var usable = (words ?? CompetitionMiniGames.BigBrotherWords)
                .Select(CompetitionMiniGames.ScrambleForm)
                .Where(word => word.Length >= CompetitionMiniGames.ShortestScrambleWord && word.Length <= CompetitionMiniGames.LongestScrambleWord)
                .Distinct().ToList();
            // A house of short names still makes a game: the reference's words fill it out.
            if (usable.Count < CompetitionMiniGames.FewestScrambleWords)
                usable.AddRange(CompetitionMiniGames.BigBrotherWords.Where(word => !usable.Contains(word))
                    .Take(CompetitionMiniGames.FewestScrambleWords - usable.Count));
            Shuffle(usable);
            wordPool.AddRange(usable);
            NextWord();
        }

        private void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = (int)(random.NextDouble() * (i + 1));
                if (j > i) j = i;
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        /// <summary>The next word, scrambled so that at least two letters move and it never reads as itself.</summary>
        private void NextWord()
        {
            wordCursor = (wordCursor + 1) % wordPool.Count;
            Word = wordPool[wordCursor];
            picked.Clear();
            var letters = Word.ToCharArray();
            for (int attempt = 0; attempt < 20; attempt++)
            {
                Shuffle(letters);
                int moved = 0;
                for (int i = 0; i < letters.Length; i++) if (letters[i] != Word[i]) moved++;
                if (moved >= 2) break;
            }
            Scrambled = new string(letters);
        }

        /// <summary>Chooses a tile. False when it is already chosen, a verdict is showing, or the game is over.</summary>
        public bool PickTile(int index)
        {
            if (Finished || Kind != CompetitionMiniGames.Kind.Words || ShowingVerdict) return false;
            if (index < 0 || index >= Scrambled.Length || picked.Contains(index)) return false;
            picked.Add(index);
            if (picked.Count == Scrambled.Length) Judge();
            return true;
        }

        /// <summary>A typed letter chooses the first tile carrying it that is not already chosen.</summary>
        public bool TypeLetter(char letter)
        {
            if (Finished || Kind != CompetitionMiniGames.Kind.Words || ShowingVerdict) return false;
            letter = char.ToUpperInvariant(letter);
            for (int i = 0; i < Scrambled.Length; i++)
                if (Scrambled[i] == letter && !picked.Contains(i)) return PickTile(i);
            return false;
        }

        /// <summary>Takes back the last letter chosen.</summary>
        public bool Undo()
        {
            if (Finished || Kind != CompetitionMiniGames.Kind.Words || ShowingVerdict || picked.Count == 0) return false;
            picked.RemoveAt(picked.Count - 1);
            return true;
        }

        /// <summary>Clears every letter chosen.</summary>
        public bool ClearTiles()
        {
            if (Finished || Kind != CompetitionMiniGames.Kind.Words || ShowingVerdict || picked.Count == 0) return false;
            picked.Clear();
            return true;
        }

        /// <summary>Moves on to the next word for nothing, as the reference allows.</summary>
        public bool SkipWord()
        {
            if (Finished || Kind != CompetitionMiniGames.Kind.Words || ShowingVerdict) return false;
            WordsSkipped++;
            Feedback = "Skipped " + Word;
            NextWord();
            return true;
        }

        private void Judge()
        {
            verdictRight = Spelled == Word;
            verdictUntil = Elapsed + VerdictSeconds;
            if (verdictRight)
            {
                WordsSolved++;
                WordPoints += CompetitionMiniGames.WordPoints(Word.Length);
                Feedback = Word + "  +" + CompetitionMiniGames.WordPoints(Word.Length).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            }
            else { WrongSpellings++; Feedback = "Not quite"; }
        }

        /// <summary>A verdict clears: a right word gives way to the next, a wrong spelling to empty tiles.</summary>
        private void StepWords()
        {
            if (verdictUntil < 0 || Elapsed < verdictUntil) return;
            verdictUntil = -1;
            if (verdictRight) NextWord(); else picked.Clear();
        }

        private double WordsScore() => CompetitionMiniGames.WordsScore(WordPoints);
    }
}
