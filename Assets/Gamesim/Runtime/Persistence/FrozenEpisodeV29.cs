// The fixed29 whole reader (SCH30, ALL-STAR-HISTORIES-PLAN 8.1-8.3): a frozen 29 DTO hydrated from the JSON and a
// frozen copy of the whole schema-29 validator as b208d46d has it, carried from Frozen28 by exactly the live
// validators' changes since its image (the dialogue store at rules 0 and 1, story rules to Roles, the role moments'
// keep-the-block deal, pitch board and recorded impacts). BCL and Newtonsoft only, never a live DTO. At the live
// schema 29, EpisodeSaveMigrations.PrepareCurrentPayload calls Validate on every direct-29 save and on the end of
// every older chain before it returns.
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using Gamesim.Persistence.Frozen29Components;

namespace Gamesim.Persistence
{
    internal static class FrozenEpisodeV29
    {
        internal static void Validate(JObject original)
        {
            // This exact reused component budgets the original tree before any recursive
            // shape walk, serializer or hydration. It never edits or normalizes original.
            var state = FrozenV29Input.HydrateRootShapeOnly(original);
            if (original == null || original["schemaVersion"] == null
                || original["schemaVersion"].Type != JTokenType.Integer
                || state == null || state.schemaVersion != 29)
                throw new InvalidDataException("Unsupported frozen episode schema.");

            try
            {
                // Original public mode 0/1/2 selection, the dialogue domain, every complete semantic
                // domain, then every persistence limit. No older-version projection or repair.
                Frozen29ReadOnly.EpisodeSaveValidation.Validate(state);
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
