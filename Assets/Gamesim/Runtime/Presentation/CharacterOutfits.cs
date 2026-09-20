using System;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>Activity dressing is a projection of the saved look, never a season command or RNG draw.</summary>
    public static class CharacterOutfits
    {
        public const string Everyday = "Everyday", Competition = "Competition", Formal = "Formal";
        public const string Sleepwear = "Sleepwear", Swimwear = "Swimwear";

        public static string ContextFor(EpisodePhase phase)
        {
            switch (phase)
            {
                case EpisodePhase.HoH: case EpisodePhase.Veto:
                case EpisodePhase.FinalHoHPart1: case EpisodePhase.FinalHoHPart2: case EpisodePhase.FinalHoHPart3:
                    return Competition;
                case EpisodePhase.Nomination: case EpisodePhase.VetoMeeting: case EpisodePhase.Eviction:
                case EpisodePhase.FinalEviction: case EpisodePhase.Jury: case EpisodePhase.JuryQuestioning:
                case EpisodePhase.FinalSpeeches: case EpisodePhase.Finished:
                    return Formal;
                default: return Everyday;
            }
        }

        /// <summary>Absent activity sets fall back to Everyday, the saved selection, then the first set.</summary>
        public static CharacterAppearance Resolve(CharacterAppearance source, string context)
        {
            if (source == null) return null;
            var copy = source.Clone();
            var outfits = copy.outfits;
            if (outfits == null || outfits.Count == 0) return copy; // Legacy/preset provider defaults.
            var selected = outfits.FirstOrDefault(item => string.Equals(item.id, context, StringComparison.Ordinal))
                ?? outfits.FirstOrDefault(item => item.id == Everyday)
                ?? outfits.FirstOrDefault(item => item.id == copy.activeOutfit)
                ?? outfits[0];
            copy.activeOutfit = selected.id;
            return copy;
        }

        public static ContestantState ForPhase(ContestantState source, EpisodePhase phase)
        {
            if (source == null) return null;
            var copy = source.Clone();
            copy.appearance = Resolve(source.appearance, ContextFor(phase));
            return copy;
        }
    }
}
