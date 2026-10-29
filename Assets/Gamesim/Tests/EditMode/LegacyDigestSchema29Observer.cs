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
    /// Test-only current29 to retained28 boundary. This is not a save migration or an
    /// authority to discard dialogue. The real complete current validator, exact source
    /// binding, and every off literal run before a detached copy loses any field.
    /// </summary>
    internal static class LegacyDigestSchema29Observer
    {
        internal static JObject ProjectTrace28(EpisodeState state, JObject trace) => Project(state, trace, true);
        internal static JObject ProjectFields28(EpisodeState state, JObject fields) => Project(state, fields, false);

        /// <summary>The source-bound current trace through the unchanged literal28/27 contracts to26.</summary>
        internal static JObject ProjectTrace(EpisodeState state, JObject trace) =>
            LegacyDigestSchema28Observer.ProjectFields(ProjectTrace28(state, trace));

        /// <summary>
        /// The native-29 arm (DIALOGUE-PLAN D1): a current state on dialogue rules 1, the pilot the cast screen's
        /// preview chip starts, admitted at schema 29 itself and handed back unchanged, as a detached copy. Never
        /// projected: a store holding conversations has no schema-28 form, and dropping it would digest another
        /// season. The projections above still take only rules 0 with the empty store, exactly as before.
        /// Admission is the real complete current validator and the pilot's own (DialogueRules.TryValidate, On),
        /// the literal header, the closed pilot's storage - the three ordered lists with no role grant and no veto
        /// occasion, and a present-null conditional term on every canonical row - and the binding of this JSON to
        /// this state's own view, getters kept for a trace and dropped for a field view.
        /// </summary>
        internal static JObject NativeTrace29(EpisodeState state, JObject trace) => Native(state, trace, true);
        internal static JObject NativeFields29(EpisodeState state, JObject fields) => Native(state, fields, false);

        /// <summary>Whether a state is the native arm's: any dialogue rules but 0. The native admission then holds them to exactly 1.</summary>
        internal static bool IsNative29(EpisodeState state) => state != null && state.dialogueRulesVersion != 0;

        /// <summary>
        /// The retained FindDecision answer view, owned by this exact reader and canonical
        /// source. This cannot project caller-supplied decisions or arbitrary named JSON keys.
        /// Actual reader exceptions still propagate unchanged to the historical Ask boundary.
        /// A rules-1 source is admitted by the native arm instead (the answers are its canonical
        /// rows', which the pilot holds to a null conditional term), and every other check is the same.
        /// </summary>
        internal static JArray FindDecisionAnswers28(EpisodeState state)
        {
            string before = state == null ? null : Json(JObject.FromObject(state));
            // Admit the complete current source, exact inert additions and fixed28 contract
            // once for the batch, including modes with no decisions and their null slots.
            if (IsNative29(state)) NativeFields29(state, OldSaveTrace.Fields(state));
            else ProjectFields28(state, state == null ? null : OldSaveTrace.Fields(state));
            var answers = new JArray();
            foreach (var canonical in state.unifiedCommitments)
            {
                var decision = UnifiedVoteHistory.FindDecision(state, canonical.id);
                if (decision == null) { answers.Add(JValue.CreateNull()); continue; }
                Require(decision.Record != null && decision.Record.id == canonical.id
                    && !ReferenceEquals(decision.Record, canonical)
                    && Json(OldSaveTrace.Fields(decision.Record)) == Json(OldSaveTrace.Fields(canonical)),
                    "An actual FindDecision Record must be the exact detached canonical row for this ID.");
                var frames = state.unifiedVoteReveals.Where(frame => frame.week == decision.SettledWeek).ToArray();
                Require(frames.Length == 1 && decision.Reveal != null
                    && !ReferenceEquals(decision.Reveal, frames[0])
                    && Json(OldSaveTrace.Fields(decision.Reveal)) == Json(OldSaveTrace.Fields(frames[0]))
                    && decision.SettledWeek == canonical.settledWeek && decision.Status == canonical.status
                    && (decision.Status != DealStatus.Broken || decision.ActorId == canonical.brokenById),
                    "An actual FindDecision Reveal and verdict must retain their exact canonical archived source.");
                var raw = OldSaveTrace.Fields(decision);
                string rawBefore = Json(raw);
                Require(raw["Record"] is JObject && raw["Record"]["conditionalVeto"]?.Type == JTokenType.Null
                    && canonical.conditionalVeto == null && decision.Record.conditionalVeto == null,
                    "Only this actual canonical Record's present-null conditional term may be projected.");
                var projected = (JObject)raw.DeepClone();
                ((JObject)projected["Record"]).Remove("conditionalVeto");
                var restored = (JObject)projected.DeepClone();
                RestoreProperty((JObject)restored["Record"], (JObject)raw["Record"],
                    "conditionalVeto", JValue.CreateNull());
                Require(Json(restored) == rawBefore && Json(raw) == rawBefore,
                    "The typed reader projection must have an exact single-field/order/numeric inverse.");
                answers.Add(projected);
            }
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
                Require(Integer(source["schemaVersion"], 29), "Only literal schema29 is a current projection input.");
                string stateBefore = Json(JObject.FromObject(state)), sourceBefore = Json(source);
                Require(Integer(source["dialogueRulesVersion"], 0), "Only present integer-zero dialogue rules may be projected.");
                Require(source["dialogue"] is JObject, "The complete inert dialogue object is required.");
                var dialogue = (JObject)source["dialogue"];
                Require(dialogue.Properties().Select(p => p.Name).SequenceEqual(new[] { "episodes", "grants", "vetoOccasions" })
                    && Empty(dialogue["episodes"]) && Empty(dialogue["grants"]) && Empty(dialogue["vetoOccasions"]),
                    "Only the exact ordered three empty dialogue lists may be projected.");
                Require(source["unifiedCommitments"] is JArray, "The complete canonical commitment array is required.");
                foreach (var token in (JArray)source["unifiedCommitments"])
                    Require(token is JObject && token["conditionalVeto"]?.Type == JTokenType.Null,
                        "Every canonical row must retain a present-null conditionalVeto.");
                // The private view selector is not a validation bypass: both entry points
                // require the complete same current state and its exact corresponding JSON.
                var own = JObject.FromObject(state);
                if (!getterTrace) own = WithoutGetters(own);
                Require(Json(own) == sourceBefore, "The JSON is not this current state's exact own view.");

                var projected = (JObject)source.DeepClone();
                projected.Remove("dialogueRulesVersion");
                projected.Remove("dialogue");
                foreach (JObject row in (JArray)projected["unifiedCommitments"]) row.Remove("conditionalVeto");
                projected["schemaVersion"] = 28;
                Fixed28(WithoutGetters(projected));
                Require(Json(Restore(projected, source)) == sourceBefore,
                    "The current projection must have the exact three-addition/header structural and order inverse.");
                Require(Json(source) == sourceBefore && Json(JObject.FromObject(state)) == stateBefore,
                    "The current source, JSON, numeric tokens and property/list order must remain unchanged.");
                return projected;
            }
            catch (Exception error) when (error is JsonException || error is OverflowException
                || error is ArgumentException || error is InvalidOperationException || error is FormatException)
            { throw new InvalidDataException("The schema29 digest observer exceeds its fixed contract.", error); }
        }

        private static JObject Native(EpisodeState state, JObject source, bool getterTrace)
        {
            try
            {
                Require(state != null && source != null, "A current source state and its own JSON are required.");
                LegacyDigestSchema27Observer.RequireValidSource(state);
                Require(Integer(source["schemaVersion"], 29) && state.schemaVersion == 29, "Only literal schema29 is a native input.");
                string stateBefore = Json(JObject.FromObject(state)), sourceBefore = Json(source);
                Require(Integer(source["dialogueRulesVersion"], 1) && state.dialogueRulesVersion == 1,
                    "Only present integer-one dialogue rules are admitted natively.");
                Require(DialogueRules.TryValidate(state, out string pilot) && DialogueRules.On(state),
                    "The pilot's own validator refuses the source: " + pilot);
                Require(source["dialogue"] is JObject, "The complete dialogue object is required.");
                var dialogue = (JObject)source["dialogue"];
                Require(dialogue.Properties().Select(p => p.Name).SequenceEqual(new[] { "episodes", "grants", "vetoOccasions" })
                    && dialogue["episodes"] is JArray && Empty(dialogue["grants"]) && Empty(dialogue["vetoOccasions"]),
                    "Only the pilot's ordered dialogue lists, with no grant and no veto occasion, are admitted natively.");
                Require(source["unifiedCommitments"] is JArray, "The complete canonical commitment array is required.");
                foreach (var token in (JArray)source["unifiedCommitments"])
                    Require(token is JObject && token["conditionalVeto"]?.Type == JTokenType.Null,
                        "Every canonical row must retain a present-null conditionalVeto.");
                var own = JObject.FromObject(state);
                if (!getterTrace) own = WithoutGetters(own);
                Require(Json(own) == sourceBefore, "The JSON is not this current state's exact own view.");
                var native = (JObject)source.DeepClone();
                Require(Json(native) == sourceBefore && Json(source) == sourceBefore && Json(JObject.FromObject(state)) == stateBefore,
                    "The native source, JSON, numeric tokens and property/list order must remain unchanged.");
                return native;
            }
            catch (Exception error) when (error is JsonException || error is OverflowException
                || error is ArgumentException || error is InvalidOperationException || error is FormatException)
            { throw new InvalidDataException("The schema29 digest observer exceeds its native contract.", error); }
        }

        private static JObject Restore(JObject projected, JObject original)
        {
            var restored = (JObject)projected.DeepClone();
            restored["schemaVersion"] = 29;
            RestoreProperty(restored, original, "dialogueRulesVersion", new JValue(0));
            RestoreProperty(restored, original, "dialogue", new JObject {
                ["episodes"] = new JArray(), ["grants"] = new JArray(), ["vetoOccasions"] = new JArray() });
            var rows = (JArray)restored["unifiedCommitments"];
            var originals = (JArray)original["unifiedCommitments"];
            for (int index = 0; index < rows.Count; index++)
                RestoreProperty((JObject)rows[index], (JObject)originals[index], "conditionalVeto", JValue.CreateNull());
            return restored;
        }

        private static void RestoreProperty(JObject restored, JObject original, string name, JToken literal)
        {
            var next = original.Properties().SkipWhile(p => p.Name != name).Skip(1)
                .Select(p => restored.Property(p.Name)).FirstOrDefault(p => p != null);
            var addition = new JProperty(name, literal);
            if (next == null) restored.Add(addition); else next.AddBeforeSelf(addition);
        }

        private static JObject WithoutGetters(JObject source)
        {
            var fields = (JObject)source.DeepClone();
            fields.Remove("Active");
            if (fields["houseEvents"] is JArray events)
                foreach (var row in events) if (row is JObject item) item.Remove("IsStory");
            return fields;
        }

        private static void Fixed28(JObject fields)
        {
#if UNITY_5_3_OR_NEWER
            var assembly = typeof(Gamesim.Persistence.EpisodeSaveStore).Assembly;
#else
            var assembly = typeof(EpisodeState).Assembly;
#endif
            var owner = assembly.GetType("Gamesim.Persistence.FrozenEpisodeV28", true);
            var method = owner.GetMethod("Validate", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(JObject) }, null);
            Require(owner.Assembly == assembly && method != null && method.DeclaringType == owner
                && method.IsAssembly && method.ReturnType == typeof(void),
                "The exact independent fixed28 historical contract is required; no fallback is allowed.");
            try { method.Invoke(null, new object[] { fields }); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        private static bool Empty(JToken token) => token is JArray array && array.Count == 0;
        private static bool Integer(JToken token, long expected) => token?.Type == JTokenType.Integer && (long)token == expected;
        private static string Json(JToken token) => token.ToString(Formatting.None);
        private static void Require(bool accepted, string reason)
        { if (!accepted) throw new InvalidDataException(reason); }
    }
}
