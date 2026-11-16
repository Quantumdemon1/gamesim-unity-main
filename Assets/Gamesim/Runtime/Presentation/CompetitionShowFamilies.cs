using System.Collections.Generic;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// How a competition is staged in the yard when it is decided without the ranked game
    /// (competition staging, slice 3): one family per game, read from what the game's equipment is,
    /// so that each family reads as a different competition at a glance.
    /// </summary>
    public enum CompetitionShowFamily
    {
        /// <summary>Endurance at grip rigs: hold on, let go from the bottom up (slice 1).</summary>
        Hold,
        /// <summary>Quiz-style at consoles: a question, everyone locks in, the reveal puts the wrong ones out.</summary>
        Podium,
        /// <summary>Hands at a tray or letter console: throw, spell; they finish in the standings' order.</summary>
        Table,
        /// <summary>The skill games on the course: a leg run from a post and back, home in the standings' order.</summary>
        Course,
    }

    /// <summary>
    /// The family of each game the show knows, by the game's published id (never by its kind: a new
    /// game of a known kind - the ring tower being built beside this slice - is not given a family by
    /// accident). A game the show does not know commits straight to its standings card, as every
    /// game did before. Pure: names no Unity type, reads nothing of the season.
    /// </summary>
    public static class CompetitionShowFamilies
    {
        private static readonly Dictionary<string, CompetitionShowFamily> games = new Dictionary<string, CompetitionShowFamily>
        {
            // Grip rigs: endurance, as slice 1 stages it.
            { CompetitionDefinitions.HoldYourGround.Id, CompetitionShowFamily.Hold },
            { CompetitionDefinitions.PressureCooker.Id, CompetitionShowFamily.Hold },
            // Memory consoles: a quiz at a console, answers locked in and revealed on the display.
            { CompetitionDefinitions.HouseMemory.Id, CompetitionShowFamily.Podium },
            { CompetitionDefinitions.FirstImpressions.Id, CompetitionShowFamily.Podium },
            // A dice tray (throw) and letter consoles (spell): work with the hands at a table.
            { CompetitionDefinitions.RollTheDice.Id, CompetitionShowFamily.Table },
            { CompetitionDefinitions.WordScramble.Id, CompetitionShowFamily.Table },
            { CompetitionDefinitions.HouseguestScramble.Id, CompetitionShowFamily.Table },
            // Reaction pads: the skill games, run on the course.
            { CompetitionDefinitions.SignalSprint.Id, CompetitionShowFamily.Course },
            { CompetitionDefinitions.SwitchbackSignals.Id, CompetitionShowFamily.Course },
        };

        /// <summary>Every game the show knows, by id, with its family.</summary>
        public static IReadOnlyDictionary<string, CompetitionShowFamily> Games => games;

        /// <summary>
        /// The family a competition is staged in, or null when it is not staged. A game is looked up
        /// by its id; with no game at all (a season before rules 3, which had no authored games) an
        /// endurance competition is a hold, as slice 1 staged it, and every other kind is not staged.
        /// </summary>
        public static CompetitionShowFamily? For(string gameId, string category)
        {
            if (gameId == null) return category == CompetitionShowPlan.EnduranceKind ? CompetitionShowFamily.Hold : (CompetitionShowFamily?)null;
            return games.TryGetValue(gameId, out var family) ? family : (CompetitionShowFamily?)null;
        }
    }
}
