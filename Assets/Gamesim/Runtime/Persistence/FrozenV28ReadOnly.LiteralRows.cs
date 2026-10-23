// Fixed28 literal-data behavior adapters. No DTO field or initializer changes.
using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Persistence.Frozen28Data;

namespace Gamesim.Persistence.Frozen28ReadOnly
{
// Nonserialized operations on the unchanged, fields-only literal payload types.
    internal static class LiteralRows
    {
        // SOURCE EpisodeState.Active and EpisodeState.Find. No null/default repair.
        internal static IEnumerable<ContestantState> Active(EpisodeState state) =>
            state.contestants.Where(c => c.status == ContestantStatus.Active);
        internal static ContestantState Find(this EpisodeState state, string id) =>
            state.contestants.FirstOrDefault(c => c.id == id);

        // SOURCE HouseEventState.IsStory.
        internal static bool IsStory(HouseEventState row) => row.kind == HouseEventKind.Story;

        // Source-exact fresh fallback used only by the three semantic reader fallback expressions.
        // This is never used by hydration or to repair a supplied payload.
        internal static SeasonLedger NewSeasonLedger() => new SeasonLedger
        {
            opportunities = new List<OpportunityRow>(), competitions = new List<CompetitionRow>(),
            power = new List<PowerRow>(), ballots = new List<BallotRow>(),
            claims = new List<ClaimRow>(), alliances = new List<AllianceRow>(),
            standings = new List<StandingRow>(), replies = new List<ReplyRow>(),
            calls = new List<BlocCallRow>(), dropped = 0, plans = new List<PactPlanRow>()
        };

        // Source MemberwiseClone adapted to the already-proven full scalar copy.
        internal static PromiseState Clone(this PromiseState row) =>
            Gamesim.Persistence.Frozen28Semantics.FrozenV28ReferenceRows.Clone(row);

        // Source MemberwiseClone adapted to the already-proven full scalar copy.
        internal static DealState Clone(this DealState row) =>
            Gamesim.Persistence.Frozen28Semantics.FrozenV28ReferenceRows.Clone(row);

        // Source MemberwiseClone adapted to the already-proven full scalar copy.
        internal static UnifiedCommitmentState Clone(this UnifiedCommitmentState row) =>
            Gamesim.Persistence.Frozen28Semantics.FrozenV28ReferenceRows.Clone(row);

        // Source UnifiedVoteBallotState.Clone: all 2 literal fields, preserving nulls and row order.
        internal static UnifiedVoteBallotState Clone(this UnifiedVoteBallotState row) => new UnifiedVoteBallotState
        {
            voterId = row.voterId,
            targetId = row.targetId
        };

        // Source UnifiedVoteRevealState.Clone: all 2 literal fields, preserving nulls and row order.
        internal static UnifiedVoteRevealState Clone(this UnifiedVoteRevealState row) => new UnifiedVoteRevealState
        {
            week = row.week,
            ballots = row.ballots?.Select(ballot => ballot?.Clone()).ToList()
        };
    }

    internal static class AppearanceRules
    {
        private static bool Fail(out string error, string message) { error = message; return false; }

        private static bool Key(string value, bool empty = false) => value != null && value.Length <= 128
                    && (empty || !string.IsNullOrWhiteSpace(value)) && !value.Any(char.IsControl)
                    && value.IndexOfAny(new[] { '/', '\\', ':' }) < 0;

        internal static bool TryValidate(this CharacterAppearance recipe, out string error)
                {
                    error = null;
                    if (recipe.version != 1 || !Key(recipe.provider) || !Key(recipe.presetId, true) || !Key(recipe.bodyId, true)
                        || !Key(recipe.fallbackId) || !Key(recipe.activeOutfit)) return Fail(out error, "Invalid appearance identity or version.");
                    if (recipe.dna == null || recipe.dna.Count > 128 || recipe.dna.Any(x => x == null || !Key(x.id) || !Unit(x.value))
                        || recipe.dna.Select(x => x.id).Distinct(StringComparer.Ordinal).Count() != recipe.dna.Count)
                        return Fail(out error, "Appearance sliders must have unique names and finite values between zero and one.");
                    if (!ValidColors(recipe.colors)) return Fail(out error, "Invalid appearance colors.");
                    if (recipe.outfits == null || recipe.outfits.Count > 8 || recipe.outfits.Any(x => x == null || !Key(x.id))
                        || recipe.outfits.Select(x => x.id).Distinct(StringComparer.Ordinal).Count() != recipe.outfits.Count)
                        return Fail(out error, "An appearance supports up to eight uniquely named outfits.");
                    foreach (var outfit in recipe.outfits)
                        if (!ValidColors(outfit.colors) || outfit.wardrobe == null || outfit.wardrobe.Count > 32
                            || outfit.wardrobe.Any(x => x == null || !Key(x.slot) || !Key(x.itemId, true))
                            || outfit.wardrobe.Select(x => x.slot).Distinct(StringComparer.Ordinal).Count() != outfit.wardrobe.Count)
                            return Fail(out error, "Outfit items must occupy unique slots with valid catalog identifiers.");
                    if (recipe.outfits.Count > 0 && !recipe.outfits.Any(x => x.id == recipe.activeOutfit))
                        return Fail(out error, "The active outfit is missing.");
                    return true;
                }

        private static bool Unit(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= 1;

        private static bool ValidColors(List<AppearanceColor> values) => values != null && values.Count <= 64
                    && values.All(x => x != null && Key(x.id) && Unit(x.r) && Unit(x.g) && Unit(x.b) && Unit(x.a))
                    && values.Select(x => x.id).Distinct(StringComparer.Ordinal).Count() == values.Count;

    }
}
