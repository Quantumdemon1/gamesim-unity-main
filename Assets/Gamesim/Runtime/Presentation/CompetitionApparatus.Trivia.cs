using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The player's memory console during the trivia (competition rules 6): its first keys stand for the
    /// questions, numbered, the one being asked lit pale, each answered one green when it was right and red when
    /// it was wrong; the keys past the last question are blank. The readout says which question is up and how
    /// many were right. A trivia attempt has no cards, so the console never reads the memory board's.
    /// </summary>
    public sealed partial class CompetitionApparatus
    {
        private void SyncTrivia(MiniGameRun run, bool started, bool paused)
        {
            VisibleMemoryFaces = 0;
            var round = run.Trivia;
            int count = round?.Count ?? 0;
            for (int i = 0; i < indicators.Count && i < glyphs.Count; i++)
            {
                if (i >= count) { SetWord(glyphs[i], ""); LightKey(i, null); continue; }
                SetWord(glyphs[i], (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
                int chosen = round.Chosen[i];
                Color? lit = chosen >= 0 ? (chosen == round.Questions[i].Answer ? UiTheme.Positive : UiTheme.Danger)
                    : started && !paused && i == round.Index ? UiTheme.Accent : (Color?)null;
                LightKey(i, lit);
            }
        }

        private static string TriviaReadout(MiniGameRun run) =>
            run.Trivia == null ? "Ready" : run.Trivia.CountLine + " · " + run.Trivia.Right + " right";
    }
}
