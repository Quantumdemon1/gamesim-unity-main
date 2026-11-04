using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Test-only current30 to retained29 boundary (SCH30, ALL-STAR-HISTORIES-PLAN 8.6). Not a save migration and not an
    /// authority to discard any rule. The complete current validator, the binding of the JSON to the state's own view,
    /// and every pooled field at its off literal in its fixed order run before a detached copy loses a field; the copy
    /// is checked as the exact inverse of the migration's literals and against the frozen schema-29 whole reader, then
    /// handed to the retained-29 observer, which takes it on to 28 as it always did. Every digest of a current state
    /// passes here, so a recorded digest never moves for the bump: one that moves is a regression, never a re-record.
    /// A season on dialogue rules 1 (DIALOGUE-PLAN D1) is taken to the same 29, its store kept, and admitted there by the
    /// retained-29 observer's native contract, never projected to 28 (<see cref="NativeFields29"/>).
    /// </summary>
    internal static class LegacyDigestSchema30Observer
    {
        /// <summary>Schema 30's pooled root fields, in their declaration order (5.1), with their off literals.</summary>
        internal static readonly string[] Pooled = { "alumniRulesVersion", "alumniCatalogEdition", "tuningRulesVersion" };

        internal static JObject ProjectTrace29(EpisodeState state, JObject trace) => Project(state, trace, true);
        internal static JObject ProjectFields29(EpisodeState state, JObject fields) => Project(state, fields, false);

        /// <summary>
        /// Whether a state is on the alumni rules (H1b, ALL-STAR-HISTORIES-PLAN 8.6): its histories exist only from schema 30,
        /// so it has no schema-29 view, and its digests are taken at the live 30 through <see cref="AdmitNative30"/>.
        /// </summary>
        internal static bool OnTheAlumniRules(EpisodeState state) => state != null && state.alumniRulesVersion != 0;

        /// <summary>
        /// Whether a state is the native-29 arm's (DIALOGUE-PLAN D1): any dialogue rules but 0. The native admission then
        /// holds them to exactly 1. A season on the alumni rules is the native-30 arm's first, whatever its dialogue rules.
        /// </summary>
        internal static bool IsNative29(EpisodeState state) => state != null && state.dialogueRulesVersion != 0;

        /// <summary>
        /// The native-29 arm's state-bound entries (DIALOGUE-PLAN D1, carried to schema 30 at the merge with SCH30,
        /// ALL-STAR-HISTORIES-PLAN 8.5): a current state on dialogue rules 1, the pilot the cast screen's preview chip
        /// starts, taken to its schema-29 view with its store kept - never to 28, where a store holding conversations has
        /// no form - and admitted there by the retained-29 observer's native contract
        /// (<see cref="LegacyDigestSchema29Observer.NativeRetained29"/>), which hands it back as a detached copy.
        /// Admission is D1's, at the live header: the complete current validator, literal schema 30, literal dialogue
        /// rules 1 on the state and its JSON, the pilot's own validator (DialogueRules.TryValidate, On), the closed
        /// pilot's storage, and the binding of this JSON to this state's own view, getters kept for a trace and dropped
        /// for a field view; then this boundary's projection to 29 (the pooled fields off and in order, the frozen
        /// schema-29 reader and the migration's exact inverse), which removes nothing of the store.
        /// </summary>
        internal static JObject NativeTrace29(EpisodeState state, JObject trace) => Native29(state, trace, true);
        internal static JObject NativeFields29(EpisodeState state, JObject fields) => Native29(state, fields, false);

        /// <summary>
        /// H1b's native-30 arm: a season on the alumni rules admitted at the live 30, unprojected. The same contract the
        /// projection holds a source to before it removes anything - the complete current validator, literal schema 30,
        /// the JSON the state's own view, the pooled fields after the dialogue store in their declared order - with the
        /// alumni rules and edition the state's own (validation has bound the edition to the rules) and the tuning rules
        /// the state's own (0 before B9, 1 for a fresh season from it). Nothing is removed and nothing changes; the fields
        /// come back as they went in.
        /// </summary>
        internal static JObject AdmitNative30(EpisodeState state, JObject fields)
        {
            try
            {
                Require(state != null && fields != null, "A current source state and its own JSON are required.");
                LegacyDigestSchema27Observer.RequireValidSource(state);
                Require(Integer(fields["schemaVersion"], 30), "Only literal schema30 is a native input.");
                string stateBefore = Json(JObject.FromObject(state)), sourceBefore = Json(fields);
                Require(AlumniRules.On(state) && Integer(fields["alumniRulesVersion"], state.alumniRulesVersion),
                    "Only a season on the alumni rules is admitted natively.");
                Require(fields["alumniCatalogEdition"]?.Type == JTokenType.String && (string)fields["alumniCatalogEdition"] == state.alumniCatalogEdition,
                    "The native edition must be the season's own.");
                // From B9 a fresh season starts on tuning 1 (TuningRules.Shipped), which exists only from 30 too: the native
                // arm admits the season's own value, as it does its alumni rules.
                Require(Integer(fields["tuningRulesVersion"], state.tuningRulesVersion), "The native tuning rules must be the season's own.");
                var names = fields.Properties().Select(p => p.Name).ToList();
                int dialogue = names.IndexOf("dialogue");
                Require(dialogue >= 0 && Pooled.Select((name, i) => names.IndexOf(name) == dialogue + 1 + i).All(at => at),
                    "The pooled fields must follow the dialogue store, in their declared order.");
                Require(Json(WithoutGetters(JObject.FromObject(state))) == sourceBefore, "The JSON is not this current state's exact own view.");
                Require(Json(fields) == sourceBefore && Json(JObject.FromObject(state)) == stateBefore,
                    "The current source, JSON, numeric tokens and property/list order must remain unchanged.");
                return fields;
            }
            catch (Exception error) when (error is JsonException || error is OverflowException
                || error is ArgumentException || error is InvalidOperationException || error is FormatException)
            { throw new InvalidDataException("The schema30 digest observer exceeds its fixed contract.", error); }
        }

        /// <summary>The source-bound current trace through the retained 29 observer to 28.</summary>
        internal static JObject ProjectTrace28(EpisodeState state, JObject trace) =>
            LegacyDigestSchema29Observer.ProjectRetained28(ProjectTrace29(state, trace), true);

        internal static JObject ProjectFields28(EpisodeState state, JObject fields) =>
            LegacyDigestSchema29Observer.ProjectRetained28(ProjectFields29(state, fields), false);

        /// <summary>The source-bound current trace through the unchanged literal29/28/27 contracts to 26.</summary>
        internal static JObject ProjectTrace(EpisodeState state, JObject trace) =>
            LegacyDigestSchema28Observer.ProjectFields(ProjectTrace28(state, trace));

        /// <summary>
        /// The retained FindDecision answer view of the current state (the schema-29 observer's, which bound the state
        /// itself until SCH30): the whole source is admitted through this boundary first, then each answer's typed
        /// Record loses its present-null conditional term, as before. Schema 30 adds nothing to a commitment row. A season
        /// on the alumni rules (H1b) has no 28 view of its whole source, so it is admitted natively at 30 instead
        /// (<see cref="AdmitNative30"/>); its answers are read from the same canonical rows, which the alumni rules never touch.
        /// A season on dialogue rules 1 (D1) has no 28 view either: it is admitted by the native-29 arm
        /// (<see cref="NativeFields29"/>), and its answers are its canonical rows', which the pilot holds to a null conditional term.
        /// </summary>
        internal static JArray FindDecisionAnswers28(EpisodeState state)
        {
            string before = state == null ? null : Json(JObject.FromObject(state));
            if (OnTheAlumniRules(state)) AdmitNative30(state, OldSaveTrace.Fields(state));
            else if (IsNative29(state)) NativeFields29(state, OldSaveTrace.Fields(state));
            else ProjectFields28(state, state == null ? null : OldSaveTrace.Fields(state));
            var answers = LegacyDigestSchema29Observer.FindDecisionAnswersOfAnAdmittedSource28(state);
            Require(Json(JObject.FromObject(state)) == before,
                "The actual FindDecision source and all its fields/order/numbers must remain unchanged.");
            return answers;
        }

        private static JObject Project(EpisodeState state, JObject source, bool getterTrace)
        {
            try
            {
                Require(state != null && source != null, "A current source state and its own JSON are required.");
                LegacyDigestSchema27Observer.RequireValidSource(state);
                Require(Integer(source["schemaVersion"], 30), "Only literal schema30 is a current projection input.");
                string stateBefore = Json(JObject.FromObject(state)), sourceBefore = Json(source);
                Require(Integer(source["alumniRulesVersion"], 0), "Only present integer-zero alumni rules may be projected.");
                Require(source["alumniCatalogEdition"]?.Type == JTokenType.Null, "Only a present-null alumni edition may be projected.");
                Require(Integer(source["tuningRulesVersion"], 0), "Only present integer-zero tuning rules may be projected.");
                var names = source.Properties().Select(p => p.Name).ToList();
                int dialogue = names.IndexOf("dialogue");
                Require(dialogue >= 0 && Pooled.Select((name, i) => names.IndexOf(name) == dialogue + 1 + i).All(at => at),
                    "The pooled fields must follow the dialogue store, in their declared order.");
                // Both entry points require the complete same current state and its exact corresponding JSON.
                var own = JObject.FromObject(state);
                if (!getterTrace) own = WithoutGetters(own);
                Require(Json(own) == sourceBefore, "The JSON is not this current state's exact own view.");

                var projected = (JObject)source.DeepClone();
                foreach (string name in Pooled) projected.Remove(name);
                projected["schemaVersion"] = 29;
                var fields29 = WithoutGetters(projected);
#if UNITY_5_3_OR_NEWER
                // The real step, where it is compiled: it runs the frozen schema-29 whole reader on the projection first,
                // and its literals must be this projection's exact inverse.
                Require(Json(Gamesim.Persistence.EpisodeSaveMigrations.UpgradeV29ToV30(fields29)) == Json(WithoutGetters(source)),
                    "UpgradeV29ToV30 must restore the projection's every field, order and numeric token.");
#else
                // The Unity-free subset compiles no migration chain: the same frozen reader, reached directly.
                Fixed29(fields29);
#endif
                Require(Json(Restore(projected, source)) == sourceBefore,
                    "The current projection must have the exact three-addition/header structural and order inverse.");
                Require(Json(source) == sourceBefore && Json(JObject.FromObject(state)) == stateBefore,
                    "The current source, JSON, numeric tokens and property/list order must remain unchanged.");
                return projected;
            }
            catch (Exception error) when (error is JsonException || error is OverflowException
                || error is ArgumentException || error is InvalidOperationException || error is FormatException)
            { throw new InvalidDataException("The schema30 digest observer exceeds its fixed contract.", error); }
        }

        private static JObject Native29(EpisodeState state, JObject source, bool getterTrace)
        {
            try
            {
                // D1's admission, in D1's order, at the live header: each refusal comes from the check that owns it.
                Require(state != null && source != null, "A current source state and its own JSON are required.");
                LegacyDigestSchema27Observer.RequireValidSource(state);
                Require(Integer(source["schemaVersion"], 30) && state.schemaVersion == 30, "Only literal schema30 is a native input.");
                string stateBefore = Json(JObject.FromObject(state)), sourceBefore = Json(source);
                Require(Integer(source["dialogueRulesVersion"], 1) && state.dialogueRulesVersion == 1,
                    "Only present integer-one dialogue rules are admitted natively.");
                Require(DialogueRules.TryValidate(state, out string pilot) && DialogueRules.On(state),
                    "The pilot's own validator refuses the source: " + pilot);
                LegacyDigestSchema29Observer.RequireNativeStore(source);
                var own = JObject.FromObject(state);
                if (!getterTrace) own = WithoutGetters(own);
                Require(Json(own) == sourceBefore, "The JSON is not this current state's exact own view.");
                // The 30-to-29 layer: this boundary's own projection (the pooled fields off and in order, the frozen
                // schema-29 reader, the migration's exact inverse), which keeps the store; then the native contract at 29.
                var native = LegacyDigestSchema29Observer.NativeRetained29(Project(state, source, getterTrace), getterTrace);
                Require(Json(source) == sourceBefore && Json(JObject.FromObject(state)) == stateBefore,
                    "The native source, JSON, numeric tokens and property/list order must remain unchanged.");
                return native;
            }
            catch (Exception error) when (error is JsonException || error is OverflowException
                || error is ArgumentException || error is InvalidOperationException || error is FormatException)
            { throw new InvalidDataException("The schema30 digest observer exceeds its native contract.", error); }
        }

        /// <summary>The literals UpgradeV29ToV30 writes, by hand, each before the property that follows it in the source.</summary>
        private static JObject Restore(JObject projected, JObject original)
        {
            var restored = (JObject)projected.DeepClone();
            restored["schemaVersion"] = 30;
            RestoreProperty(restored, original, "alumniRulesVersion", new JValue(0));
            RestoreProperty(restored, original, "alumniCatalogEdition", JValue.CreateNull());
            RestoreProperty(restored, original, "tuningRulesVersion", new JValue(0));
            return restored;
        }

        private static void RestoreProperty(JObject restored, JObject original, string name, JToken literal)
        {
            var next = original.Properties().SkipWhile(p => p.Name != name).Skip(1)
                .Select(p => restored.Property(p.Name)).FirstOrDefault(p => p != null);
            var addition = new JProperty(name, literal);
            if (next == null) restored.Add(addition); else next.AddBeforeSelf(addition);
        }

        internal static JObject WithoutGetters(JObject source)
        {
            var fields = (JObject)source.DeepClone();
            fields.Remove("Active");
            if (fields["houseEvents"] is JArray events)
                foreach (var row in events) if (row is JObject item) item.Remove("IsStory");
            return fields;
        }

        private static void Fixed29(JObject fields)
        {
#if UNITY_5_3_OR_NEWER
            var assembly = typeof(Gamesim.Persistence.EpisodeSaveStore).Assembly;
#else
            var assembly = typeof(EpisodeState).Assembly;
#endif
            var owner = assembly.GetType("Gamesim.Persistence.FrozenEpisodeV29", true);
            var method = owner.GetMethod("Validate", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(JObject) }, null);
            Require(owner.Assembly == assembly && method != null && method.DeclaringType == owner
                && method.IsAssembly && method.ReturnType == typeof(void),
                "The exact independent fixed29 historical contract is required; no fallback is allowed.");
            try { method.Invoke(null, new object[] { fields }); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        private static bool Integer(JToken token, long expected) => token?.Type == JTokenType.Integer && (long)token == expected;
        private static string Json(JToken token) => token.ToString(Formatting.None);
        private static void Require(bool accepted, string reason)
        { if (!accepted) throw new InvalidDataException(reason); }
    }
}
