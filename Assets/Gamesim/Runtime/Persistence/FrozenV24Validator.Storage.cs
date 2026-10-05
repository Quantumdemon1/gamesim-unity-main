using System.IO;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Persistence
{
    /// <summary>Frozen schema-24 storage guards from 6ea7d2ec; no live validator is called.</summary>
    internal static partial class FrozenV24Validator
    {
        public static void Validate(EpisodeState state)
        {
            if (!TryValidate(state, out var reason)) throw new InvalidDataException(reason);
            Require(state.schemaVersion == 24, "Unsupported simulation schema version.");
            Require(state.unifiedCommitmentRulesVersion == 0 && state.unifiedCommitments != null && state.unifiedCommitments.Count == 0,
                "Unified commitments are not enabled in this persistence foundation.");
            Require(state.sessionId != null && state.sessionId.Length <= 256, "Session identifier is too long.");
            Require(state.week <= 10000 && state.promises.Count <= 10000 && state.alliances.Count <= 1000
                && state.memories.Count <= 100000 && state.events.Count <= 100000
                && state.acceptedCommandIds.Count <= 100000, "Save exceeds supported collection limits.");
            Require(state.contestants.All(actor => actor.id.Length <= 128 && actor.name.Length <= 128
                && actor.traits.Count <= 64 && actor.traits.All(trait => trait != null && trait.Length <= 128)),
                "Contestant text exceeds supported limits.");
            Require(state.memories.All(memory => memory.text != null && memory.text.Length <= 16384)
                && state.events.All(item => item.text != null && item.text.Length <= 16384),
                "Saved history text is missing or too long.");
            // Simulation validation owns phase/reference/enum semantics and exact
            // per-record invariants. Persistence additionally bounds saved prose.
            Require(state.relationships.All(edge => edge.notes.All(note => note.Length <= 16384)
                && edge.events.All(item => item.description.Length <= 16384)), "Relationship history text is too long.");
            Require(state.relationshipArcs.All(arc => arc.weeklyHistory.All(item => item.reason.Length <= 16384)),
                "Relationship arc history text is too long.");
            Require(state.finalSpeeches.All(speech => speech.text.Length <= 16384), "Final speech is too long.");
            // randomState is deliberately not normalized: zero is a valid wrapped generator state.
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }
    }
}
