using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The social board: the word being spelled across the top, its letters as tiles under it, and
    /// Clear and Skip word at the foot - the reference's Word Scramble laid out on this surface.
    ///
    /// <para>Three ways to choose a letter, each counted the same: click its tile, type it, or press
    /// A on it with the pad. Typing reads the key's letter on the keyboard's own layout. While the
    /// board is being played every letter types - P included - so the pause is Start or the Pause
    /// button, and Esc still asks before leaving.</para>
    ///
    /// <para>A tile's first text is its letter, and its name says which tile it is ("Letter tile 3"),
    /// which is how a test and a screen reader find it.</para>
    /// </summary>
    public sealed partial class CompetitionGameScreen
    {
        /// <summary>The words on the word board's controls. Captions are a contract.</summary>
        public const string ClearLettersCaption = "Clear letters", SkipWordCaption = "Skip word";

        /// <summary>The most letters a word can have on the board.</summary>
        public const int MaxTiles = CompetitionMiniGames.LongestScrambleWord;

        private readonly Button[] tiles = new Button[MaxTiles];
        private readonly TMP_Text[] tileLetters = new TMP_Text[MaxTiles];
        private readonly Image[] tileFaces = new Image[MaxTiles];
        private RectTransform spelledPanel;
        private TMP_Text spelledWord, wordHint, wordsChip;
        private Button clearButton, skipButton;
        private string shownWord;
        private int shownSolved, shownWrongSpellings, shownSkipped;

        /// <summary>Whether this is the word board: typing spells on it while it is played.</summary>
        private bool WordsBoard => run != null && run.Kind == CompetitionMiniGames.Kind.Words;

        private void BuildWords()
        {
            spelledPanel = HudPrimitives.Fill("Spelled word", playArea, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .6f), 14);
            UiTheme.AddBorder(spelledPanel, 14, UiTheme.Outline);
            spelledPanel.GetComponent<Image>().raycastTarget = false;
            spelledWord = HudPrimitives.Label("Spelled letters", spelledPanel, 40f * FontScale, UiTheme.Paper, TextAlignmentOptions.Center);
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) spelledWord.font = bold;
            spelledWord.characterSpacing = 18f; spelledWord.textWrappingMode = TextWrappingModes.NoWrap; Fit(spelledWord, 18);
            Stretch(spelledWord.rectTransform, 16f, 4f, 16f, 4f);
            wordHint = HudPrimitives.Label("Word hint", playArea, 16f * FontScale, UiTheme.Muted, TextAlignmentOptions.Center);
            wordHint.textWrappingMode = TextWrappingModes.NoWrap; Fit(wordHint, 11);

            for (int i = 0; i < MaxTiles; i++)
            {
                int tile = i;
                var root = HudPrimitives.Fill("Letter tile " + (i + 1), playArea, UiTheme.ActionBlue, 10);
                var face = root.GetComponent<Image>(); face.raycastTarget = true;
                var button = root.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                Untinted(button);
                button.onClick.AddListener(() => PickLetterTile(tile));
                var letter = HudPrimitives.Label("Label", root, 30f * FontScale, Color.white, TextAlignmentOptions.Center);
                if (bold != null) letter.font = bold;
                letter.textWrappingMode = TextWrappingModes.NoWrap; Fit(letter, 14);
                Stretch(letter.rectTransform, 2f, 2f, 2f, 2f);
                var ring = new GameObject("Focus ring", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                ring.rectTransform.SetParent(root, false); Stretch(ring.rectTransform, -5f, -5f, -5f, -5f);
                ring.raycastTarget = false;
                if (!UiTheme.PackSliced(ring, PackArt.KitCardEdgeFocus, 14f, UiTheme.Paper)) UiTheme.Style(ring, new Color(1f, 1f, 1f, .9f), 14);
                ring.gameObject.SetActive(false);
                root.gameObject.AddComponent<CompetitionCardFocus>().Ring = ring.gameObject;
                root.gameObject.AddComponent<HudPress>().ReducedMotion = ReducedMotion;
                tiles[i] = button; tileLetters[i] = letter; tileFaces[i] = face;
            }

            clearButton = Button("Clear letters", playArea, ClearLettersCaption, 0, 0, 220, 52, ClearLetters);
            Untinted(clearButton); Focusable(clearButton); Secondary(clearButton);
            skipButton = Button("Skip word", playArea, SkipWordCaption, 0, 0, 220, 52, SkipScrambledWord);
            Untinted(skipButton); Focusable(skipButton); Secondary(skipButton);

            wordsChip = HudPrimitives.Label("Words solved", boardChips, 15f * FontScale, UiTheme.Positive, TextAlignmentOptions.MidlineLeft);
            wordsChip.textWrappingMode = TextWrappingModes.NoWrap; Fit(wordsChip, 11);
            shownWord = null; shownSolved = 0; shownWrongSpellings = 0; shownSkipped = 0;
            WireWordNavigation();
        }

        /// <summary>
        /// Left and right walk the tiles showing; down goes to Clear and Skip word, up from them back
        /// to the tiles. A word of fewer letters leaves its spare tiles out of the ring.
        /// </summary>
        private void WireWordNavigation()
        {
            int count = Mathf.Min(MaxTiles, run.Scrambled.Length);
            for (int i = 0; i < MaxTiles; i++)
            {
                if (i >= count) { tiles[i].navigation = new Navigation { mode = Navigation.Mode.None }; continue; }
                tiles[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnLeft = tiles[(i + count - 1) % count], selectOnRight = tiles[(i + 1) % count],
                    selectOnUp = cancel, selectOnDown = i < count / 2 ? clearButton : skipButton };
            }
            clearButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = skipButton, selectOnRight = skipButton,
                selectOnUp = tiles[0], selectOnDown = pause };
            skipButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = clearButton, selectOnRight = clearButton,
                selectOnUp = tiles[Mathf.Max(0, count - 1)], selectOnDown = pause };
        }

        private void PlaceWords()
        {
            var field = PlayField;
            int count = Mathf.Max(1, Mathf.Min(MaxTiles, run.Scrambled.Length));
            float gap = 10f;
            float side = Mathf.Clamp(Mathf.Min((field.width - 48f - (count - 1) * gap) / count, field.height * .22f), 40f, 96f);
            float top = field.y + Mathf.Max(14f, field.height * .08f);
            float panelWidth = Mathf.Min(field.width - 48f, Mathf.Max(360f, count * (side + gap)));
            Place(spelledPanel, (field.width - panelWidth) * .5f, top, panelWidth, 70f * FontScale);
            top += 70f * FontScale + 8f;
            Place(wordHint.rectTransform, 0f, top, field.width, 24f * FontScale);
            top += 24f * FontScale + 18f;
            float row = count * side + (count - 1) * gap, left = (field.width - row) * .5f;
            for (int i = 0; i < MaxTiles; i++)
                Place((RectTransform)tiles[i].transform, left + i * (side + gap), top, side, side);
            top += side + 26f;
            float buttonWidth = Mathf.Min(240f, (field.width - 60f) * .5f), buttonHeight = 52f * FontScale;
            top = Mathf.Min(top, field.y + field.height - buttonHeight - 14f);
            Place((RectTransform)clearButton.transform, field.width * .5f - buttonWidth - 10f, top, buttonWidth, buttonHeight);
            Place((RectTransform)skipButton.transform, field.width * .5f + 10f, top, buttonWidth, buttonHeight);
            Place(wordsChip.rectTransform, 0f, 0f, surfaceWidth * .5f, BandHeight - 1f);
        }

        private void RefreshWords()
        {
            bool hidden = Paused || held;
            int count = Mathf.Min(MaxTiles, run.Scrambled.Length);
            if (run.Word != shownWord)
            {
                // A new word: its tiles take their letters, the spare ones step out, and the ring follows.
                shownWord = run.Word;
                WireWordNavigation();
                PlaceWords();
            }
            // The board's own group takes the tiles off while it is not live. A chosen tile goes
            // dark but stays a control - the game refuses a letter already chosen - because a
            // Selectable made non-interactable under the keyboard drops the selection, and a pad
            // player would be left choosing nothing after every letter.
            for (int i = 0; i < MaxTiles; i++)
            {
                bool shown = i < count;
                tiles[i].gameObject.SetActive(shown);
                if (!shown) continue;
                bool picked = run.TilePicked(i);
                // A paused board shows no letters: a pause is not time to think.
                tileLetters[i].text = hidden ? "?" : run.Scrambled[i].ToString();
                tileFaces[i].color = picked ? new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .7f) : UiTheme.ActionBlue;
                tileLetters[i].color = picked ? UiTheme.Muted : Color.white;
            }
            string spelled = run.Spelled;
            spelledWord.text = hidden ? "" : spelled.Length > 0 ? spelled : "";
            spelledWord.color = run.VerdictRight ? UiTheme.Positive : run.ShowingVerdict ? UiTheme.Danger : UiTheme.Paper;
            wordHint.text = hidden ? "Paused" : run.Word.Length + " letters  ·  worth "
                + CompetitionMiniGames.WordPoints(run.Word.Length).ToString("0.#") + " points";
            // Clear stays a control with nothing to clear, for the same reason: pressing it then does nothing.
            clearButton.interactable = !run.Finished;
            skipButton.interactable = !run.Finished;
            wordsChip.text = Plural(run.WordsSolved, "word") + "  ·  " + run.WordPoints.ToString("0.#") + " points";
            status.text = Plural(run.WordsSolved, "word") + " spelled  ·  " + run.WordPoints.ToString("0.#") + " points";

            if (run.WordsSolved > shownSolved) SetFeedback("Solved: " + LastSolvedFeedback());
            else if (run.WrongSpellings > shownWrongSpellings) SetFeedback("Not quite: the letters clear");
            else if (run.WordsSkipped > shownSkipped) SetFeedback("Skipped");
            shownSolved = run.WordsSolved; shownWrongSpellings = run.WrongSpellings; shownSkipped = run.WordsSkipped;
            // The keyboard moves on from a letter it has just chosen to the next one still open, so
            // A, A, A spells along the row; a spare tile a shorter word stepped out hands it back.
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (IsPlaying && events != null && events.currentSelectedGameObject != null && !run.ShowingVerdict)
            {
                int index = System.Array.IndexOf(tiles, events.currentSelectedGameObject.GetComponent<Button>());
                if (index >= count) Select(NextOpenTile(-1));
                else if (index >= 0 && run.TilePicked(index))
                {
                    var next = NextOpenTile(index);
                    if (next != skipButton) Select(next);
                }
            }
        }

        private string LastSolvedFeedback() => run.Feedback != null && run.Feedback.Length > 0 ? run.Feedback : "a word";

        private Selectable NextOpenTile(int from)
        {
            int count = Mathf.Min(MaxTiles, run.Scrambled.Length);
            for (int step = 1; step <= count; step++)
            {
                int i = (from + step) % count;
                if (!run.TilePicked(i)) return tiles[i];
            }
            return skipButton;
        }

        private void PickLetterTile(int tile)
        {
            if (!IsPlaying || run == null || !run.PickTile(tile)) return;
            LetterCue();
        }

        private void TypeWordLetter(char letter)
        {
            if (!IsPlaying || run == null || !run.TypeLetter(letter)) return;
            LetterCue();
        }

        private void UndoLetter()
        {
            if (!IsPlaying || run == null || !run.Undo()) return;
            Cue(HouseAudio.Cue.Hover);
            Refresh();
        }

        private void ClearLetters()
        {
            if (!IsPlaying || run == null || !run.ClearTiles()) return;
            Cue(HouseAudio.Cue.Hover);
            Refresh();
        }

        private void SkipScrambledWord()
        {
            if (!IsPlaying || run == null || !run.SkipWord()) return;
            Cue(HouseAudio.Cue.Button);
            Refresh();
        }

        /// <summary>A letter clicks; the last one sounds the verdict, a solve rising and a miss falling.</summary>
        private void LetterCue()
        {
            Cue(!run.ShowingVerdict ? HouseAudio.Cue.Button : run.VerdictRight ? HouseAudio.Cue.SocialUp : HouseAudio.Cue.SocialDown);
            Refresh();
        }

        /// <summary>
        /// The keyboard on the word board: each letter key types the letter it carries on the
        /// keyboard's own layout, Backspace takes one back, and the pad's X does too.
        /// </summary>
        private void ReadWordKeys(Keyboard keyboard, Gamepad pad)
        {
            if (!WordsBoard || !IsPlaying) return;
            if (keyboard != null)
            {
                for (var key = Key.A; key <= Key.Z; key++)
                {
                    var control = keyboard[key];
                    if (!control.wasPressedThisFrame) continue;
                    string name = control.displayName;
                    TypeWordLetter(!string.IsNullOrEmpty(name) && char.IsLetter(name[0]) ? name[0] : (char)('A' + (key - Key.A)));
                }
                if (keyboard.backspaceKey.wasPressedThisFrame) UndoLetter();
            }
            if (pad != null && pad.buttonWest.wasPressedThisFrame) UndoLetter();
        }
    }
}
