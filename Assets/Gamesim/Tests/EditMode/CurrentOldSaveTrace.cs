using System.IO;
using Gamesim.Simulation;
using Newtonsoft.Json.Linq;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Current-only admission for the retained historical recorder. OldSaveTrace.cs remains
    /// byte-for-byte reusable by its old scratch builds; it never gains an unchecked29 branch:
    /// its schema-29 additions are literals it checks, as every older schema's are.
    /// </summary>
    internal static class CurrentOldSaveTrace
    {
        internal const int Current = 29;

        internal static (string season, string blind, string text) Checkpoint(EpisodeState state, int schema)
        {
            var own = OldSaveTrace.Fields(state);
            JObject fields;
            if (LegacyDigestSchema29Observer.IsNative29(state))
            {
                // The native-29 arm (DIALOGUE-PLAN D1): a season on dialogue rules 1 is written at 29 by the build
                // that started it, so it has no older reference and no schema-28 form. It is checkpointed at 29
                // itself, admitted by the observer's native contract, and never projected.
                if (schema != Current)
                    throw new InvalidDataException("A season on dialogue rules 1 has no schema-" + schema + " form; it is checkpointed at " + Current + " itself.");
                fields = LegacyDigestSchema29Observer.NativeFields29(state, own);
                // The recorder hashes the same state with OldSaveTrace alone, which removes nothing at the
                // same schema: the tests and the recorder cannot part here either.
                if (!JToken.DeepEquals(OldSaveTrace.Project(own, Current), fields))
                    throw new InvalidDataException("OldSaveTrace does not take this rules-1 state to itself at schema 29.");
            }
            else
            {
                // Always check complete current validity and inert additions, even for a
                // same-schema checkpoint. Never use Project's same-header early return as admission.
                var historical = LegacyDigestSchema29Observer.ProjectFields28(state, own);
                // The recorder's own literals take the same state to the same 28, so a recorder of this
                // build replays an older reference's script to the checkpoints this does.
                if (!JToken.DeepEquals(OldSaveTrace.Project(own, 28), historical))
                    throw new InvalidDataException("OldSaveTrace's schema-29 additions do not take this state to the guarded boundary's schema 28.");
                fields = schema == Current ? own : OldSaveTrace.Project(historical, schema);
            }
            var season = (JObject)fields.DeepClone();
            season.Remove("events");
            return (OldSaveTrace.Sha256(OldSaveTrace.Canonical(season, true)),
                OldSaveTrace.Sha256(OldSaveTrace.Canonical(fields, true)),
                OldSaveTrace.Sha256(OldSaveTrace.Canonical(fields, false)));
        }
    }
}
