// The fixed30 whole reader (talk S0, schema 31's step 1): a frozen schema-30 DTO hydrated from the JSON and a frozen
// copy of the whole schema-30 validator as 64d52f59 has it, carried from Frozen29 by exactly the live validators'
// changes since its image (b208d46d): schema 30's pooled fields, dialogue rules 2, competition rules 5, story rules
// 10, 12 and 15, the holder's offer, the tuning rules' bounds and jury tiebreak, the alumni histories' receipts and
// the house talk's records. BCL and Newtonsoft only, never a live DTO. Wired: at the live 30,
// EpisodeSaveMigrations.PrepareCurrentPayload calls Validate on every direct-30 save and on the end of every older
// chain before it returns.
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using Gamesim.Persistence.Frozen30Components;

namespace Gamesim.Persistence
{
    internal static class FrozenEpisodeV30
    {
        internal static void Validate(JObject original)
        {
            // This exact reused component budgets the original tree before any recursive
            // shape walk, serializer or hydration. It never edits or normalizes original.
            var state = FrozenV30Input.HydrateRootShapeOnly(original);
            if (original == null || original["schemaVersion"] == null
                || original["schemaVersion"].Type != JTokenType.Integer
                || state == null || state.schemaVersion != 30)
                throw new InvalidDataException("Unsupported frozen episode schema.");

            try
            {
                // Original public mode 0/1/2 selection, the dialogue domain, every complete semantic
                // domain, then every persistence limit. No older-version projection or repair.
                Frozen30ReadOnly.EpisodeSaveValidation.Validate(state);
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
