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

        /// <summary>
        /// The schema the native-29 arm is keyed to: 29, the one that introduced the dialogue store and that the observer's
        /// native contract admits. A literal, never <see cref="Current"/>, the live bound: the schema bump moved Current to
        /// 30, and a rules-1 fixture saved at 29 still names 29 here. It reaches 29 through the schema-30 observer's
        /// projection, which keeps the store (DIALOGUE-PLAN §6, item 6; ALL-STAR-HISTORIES-PLAN 8.5).
        /// </summary>
        private const int NativeSchema = 29;

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
            // The native-29 arm (DIALOGUE-PLAN D1): a season on dialogue rules 1 is written at 29 by the build that
            // started it, so it has no older reference and no schema-28 form. Since SCH30 it loads at 30: the schema-30
            // observer binds the state, admits it whole and by the pilot's own validator, and takes it to its 29 with the
            // store kept, where the native contract admits it as it is. It is checkpointed at 29, its own build's schema
            // and its fixture's reference, or at the live 30, and never projected below 29.
            if (LegacyDigestSchema30Observer.IsNative29(state))
            {
                if (schema != NativeSchema && schema != Current)
                    throw new InvalidDataException("A season on dialogue rules 1 has no schema-" + schema + " form; it is checkpointed at "
                        + NativeSchema + " or " + Current + ".");
                var native29 = LegacyDigestSchema30Observer.NativeFields29(state, own);
                // The recorder hashes the same state with OldSaveTrace alone, which removes only schema 30's literals at
                // 29: the tests and the recorder cannot part here either.
                if (!JToken.DeepEquals(OldSaveTrace.Project(own, NativeSchema), native29))
                    throw new InvalidDataException("OldSaveTrace's schema-30 additions do not take this rules-1 state to the native arm's schema 29.");
                return Hash(schema == Current ? own : native29);
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
