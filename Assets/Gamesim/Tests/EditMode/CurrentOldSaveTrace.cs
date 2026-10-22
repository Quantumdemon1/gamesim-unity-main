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
            // Always check complete current validity and inert additions, even for a
            // same-schema checkpoint. Never use Project's same-header early return as admission.
            var historical = LegacyDigestSchema29Observer.ProjectFields28(state, own);
            // The recorder's own literals take the same state to the same 28, so a recorder of this
            // build replays an older reference's script to the checkpoints this does.
            if (!JToken.DeepEquals(OldSaveTrace.Project(own, 28), historical))
                throw new InvalidDataException("OldSaveTrace's schema-29 additions do not take this state to the guarded boundary's schema 28.");
            var fields = schema == Current ? own : OldSaveTrace.Project(historical, schema);
            var season = (JObject)fields.DeepClone();
            season.Remove("events");
            return (OldSaveTrace.Sha256(OldSaveTrace.Canonical(season, true)),
                OldSaveTrace.Sha256(OldSaveTrace.Canonical(fields, true)),
                OldSaveTrace.Sha256(OldSaveTrace.Canonical(fields, false)));
        }
    }
}
