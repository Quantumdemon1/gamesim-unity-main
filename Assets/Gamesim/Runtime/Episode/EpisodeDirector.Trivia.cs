using System.Collections.Generic;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The trivia competition in the house (competition rules 6). The ranked game asks the competition's own
    /// quiz (<see cref="CompetitionTrivia.Quiz(EpisodeState)"/>), the same after a cancel or a reload; practice
    /// asks about the house's rules instead (<see cref="TriviaRound.PracticeQuiz"/>), so practicing gives nothing
    /// of the ranked quiz away. It is played at the memory consoles the arena already stands for a mental game,
    /// and watched or simulated it is staged as their quiz show. Only a season at competition rules 6 deals it.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The questions an attempt asks: null for every game but the trivia.</summary>
        private static IReadOnlyList<TriviaQuestion> TriviaQuiz(EpisodeState state, bool practice) =>
            CompetitionDefinitions.For(state)?.Pattern != CompetitionPattern.Trivia ? null
            : practice ? TriviaRound.PracticeQuiz : CompetitionTrivia.Quiz(state);

        /// <summary>The briefing's first fact for the trivia: how many questions, and what they are about.</summary>
        private static EpisodeHud.BriefingFact TriviaBriefingFact() =>
            new EpisodeHud.BriefingFact(UiTheme.Icon("chat"), CompetitionTrivia.Questions + " questions", "About your season");
    }
}
