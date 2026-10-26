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
    /// </summary>
    internal static class LegacyDigestSchema30Observer
    {
        /// <summary>Schema 30's pooled root fields, in their declaration order (5.1), with their off literals.</summary>
        internal static readonly string[] Pooled = { "alumniRulesVersion", "alumniCatalogEdition", "tuningRulesVersion" };

        internal static JObject ProjectTrace29(EpisodeState state, JObject trace) => Project(state, trace, true);
        internal static JObject ProjectFields29(EpisodeState state, JObject fields) => Project(state, fields, false);

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
        /// Record loses its present-null conditional term, as before. Schema 30 adds nothing to a commitment row.
        /// </summary>
        internal static JArray FindDecisionAnswers28(EpisodeState state)
        {
            string before = state == null ? null : Json(JObject.FromObject(state));
            ProjectFields28(state, state == null ? null : OldSaveTrace.Fields(state));
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
