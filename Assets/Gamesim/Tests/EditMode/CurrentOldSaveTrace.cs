using System.IO;
using Gamesim.Simulation;
using Newtonsoft.Json.Linq;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Current-only admission for the retained historical recorder. OldSaveTrace.cs remains
    /// byte-for-byte reusable by its old scratch builds; it never gains an unchecked current branch:
    /// its schema-29 and schema-30 additions are literals it checks, as every older schema's are.
    /// </summary>
    internal static class CurrentOldSaveTrace
    {
        internal const int Current = 30;

        internal static (string season, string blind, string text) Checkpoint(EpisodeState state, int schema)
        {
            var own = OldSaveTrace.Fields(state);
            // H1b: a season on the alumni rules holds histories that exist only from schema 30, so it has no 29 or 28
            // view. The schema-30 observer admits it natively, and it is checkpointed at the live 30 alone, as the
            // recorder's OldSaveTrace.Checkpoint does a state at its own schema.
            if (LegacyDigestSchema30Observer.OnTheAlumniRules(state))
            {
                if (schema != Current) throw new InvalidDataException("A season on the alumni rules has no reference schema below " + Current + ".");
                return Hash(LegacyDigestSchema30Observer.AdmitNative30(state, own));
            }
            // Always check complete current validity and inert additions, even for a
            // same-schema checkpoint. Never use Project's same-header early return as admission.
            // SCH30: the schema-30 observer binds the state, requires the pooled fields off and hands the retained 29
            // on; the retained-29 observer takes it to 28 as it always did.
            var retained29 = LegacyDigestSchema30Observer.ProjectFields29(state, own);
            var historical = LegacyDigestSchema29Observer.ProjectRetained28(retained29, false);
            // The recorder's own literals take the same state to the same 29 and 28, so a recorder of this
            // build replays an older reference's script to the checkpoints this does.
            if (!JToken.DeepEquals(OldSaveTrace.Project(own, 29), retained29))
                throw new InvalidDataException("OldSaveTrace's schema-30 additions do not take this state to the guarded boundary's schema 29.");
            if (!JToken.DeepEquals(OldSaveTrace.Project(own, 28), historical))
                throw new InvalidDataException("OldSaveTrace's schema-29 additions do not take this state to the guarded boundary's schema 28.");
            return Hash(schema == Current ? own : schema == 29 ? retained29 : OldSaveTrace.Project(historical, schema));
        }

        private static (string season, string blind, string text) Hash(JObject fields)
        {
            var season = (JObject)fields.DeepClone();
            season.Remove("events");
            return (OldSaveTrace.Sha256(OldSaveTrace.Canonical(season, true)),
                OldSaveTrace.Sha256(OldSaveTrace.Canonical(fields, true)),
                OldSaveTrace.Sha256(OldSaveTrace.Canonical(fields, false)));
        }
    }
}
