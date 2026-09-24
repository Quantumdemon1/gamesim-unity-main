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

        /// <summary>
        /// What a houseguest wears to swim or to sleep: their own set for it if they made one, and
        /// otherwise their everyday set with the outer layers off - hair, brows and beard kept, and
        /// what they wear underneath, which in the water reads as swimwear and in bed as nightwear
        /// (a shirt stays on for bed). Only derived when there is underwear to keep: a look with
        /// none goes in what it is wearing rather than in nothing.
        ///
        /// <para>Presentation only, like every outfit: a copy of the saved look, never saved back.</para>
        /// </summary>
        public static CharacterAppearance ForActivity(CharacterAppearance source, string context)
        {
            var resolved = Resolve(source, context);
            if (resolved == null || resolved.activeOutfit == context
                || (context != Swimwear && context != Sleepwear) || resolved.outfits == null) return resolved;
            var everyday = resolved.outfits.FirstOrDefault(item => item.id == resolved.activeOutfit);
            if (everyday?.wardrobe == null) return resolved;
            bool Worn(string slot) => everyday.wardrobe.Any(item => item != null && item.slot == slot);
            bool female = (resolved.bodyId ?? "").IndexOf("Female", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!Worn("BottomUnderlayer") || (female && !Worn("TopUnderlayer"))) return resolved;
            var kept = context == Sleepwear ? SleepSlots : SwimSlots;
            var derived = everyday.Clone();
            derived.id = context;
            derived.wardrobe = derived.wardrobe.Where(item => item != null && kept.Contains(item.slot)).ToList();
            resolved.outfits.RemoveAll(item => item.id == context);
            resolved.outfits.Add(derived);
            resolved.activeOutfit = context;
            return resolved;
        }

        private static readonly string[] SwimSlots = { "Hair", "Beard", "Eyebrows", "TopUnderlayer", "BottomUnderlayer" };
        private static readonly string[] SleepSlots = { "Hair", "Beard", "Eyebrows", "TopUnderlayer", "BottomUnderlayer", "Chest" };

        /// <summary>A copy of the houseguest dressed for <paramref name="context"/>: a phase's set, or an activity's.</summary>
        public static ContestantState ForContext(ContestantState source, string context)
        {
            if (source == null) return null;
            var copy = source.Clone();
            copy.appearance = ForActivity(source.appearance, context);
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
