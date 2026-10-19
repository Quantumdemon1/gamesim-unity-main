// UNWIRED fixed28 whole reader. This file does not register a loader or migration route.
// The complete isolated semantic closure is staged beside it; release remains separately gated.
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using Gamesim.Persistence.Frozen28Components;

namespace Gamesim.Persistence
{
    internal static class FrozenEpisodeV28
    {
        internal static void Validate(JObject original)
        {
            // This exact reused component budgets the original tree before any recursive
            // shape walk, serializer or hydration. It never edits or normalizes original.
            var state = FrozenV28Input.HydrateRootShapeOnly(original);
            if (original == null || original["schemaVersion"] == null
                || original["schemaVersion"].Type != JTokenType.Integer
                || state == null || state.schemaVersion != 28)
                throw new InvalidDataException("Unsupported frozen episode schema.");

            try
            {
                // Original public mode 0/1/2 selection, every complete semantic domain,
                // then every persistence limit. No older-version projection or repair.
                Frozen28ReadOnly.EpisodeSaveValidation.Validate(state);
            }
            catch (ArgumentException error)
            {
                // Source-local marked refusals and their internal catches have already run.
                // Only this outer contract boundary translates the remaining refusal.
                throw new InvalidDataException(error.Message, error);
            }
        }
    }
}
