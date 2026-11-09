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
    /// Test-only retained29 to retained28 boundary. This is not a save migration or an authority to discard dialogue.
    /// Since SCH30 (ALL-STAR-HISTORIES-PLAN 8.6) it takes a retained schema-29 JSON object and no live state, as
    /// <see cref="LegacyDigestSchema28Observer.ProjectFields"/> takes a retained 28: a current state is bound to its own
    /// JSON, validated and taken to 29 by <see cref="LegacyDigestSchema30Observer"/>, which hands its projection on
    /// here. The literal-29 input check, every off literal and the exact inverse run before a detached copy loses a
    /// field, and the projection faces the complete independent frozen28 contract. A retained 29 on dialogue rules 1
    /// has no 28: it is admitted at 29 as it is by the native contract (<see cref="NativeRetained29"/>), never projected.
    /// </summary>
    internal static class LegacyDigestSchema29Observer
    {
        /// <summary>A retained schema-29 trace (getters kept) or field view, taken to the retained 28.</summary>
        internal static JObject ProjectRetained28(JObject source, bool getterTrace) => Project(source, getterTrace);

        /// <summary>A retained schema-29 trace through the unchanged literal28/27 contracts to 26.</summary>
        internal static JObject ProjectRetained(JObject trace) =>
            LegacyDigestSchema28Observer.ProjectFields(ProjectRetained28(trace, true));

        /// <summary>
        /// The native-29 arm's contract (DIALOGUE-PLAN D1): a retained schema-29 object on dialogue rules 1, the pilot the
        /// cast screen's preview chip started until it moved to rules 2, admitted at schema 29 itself and handed back unchanged,
        /// as a detached copy.
        /// Never projected: a store holding conversations has no schema-28 form, and dropping it would digest another
        /// season. The projections above still take only rules 0 with the empty store, exactly as before.
        /// <para>Since the merge with SCH30 (ALL-STAR-HISTORIES-PLAN 8.5) it takes a retained schema-29 JSON object and no
        /// live state, as <see cref="ProjectRetained28"/> does: a current state is bound to its own JSON, validated whole
        /// and by the pilot's own validator, and taken to 29 with its store kept by
        /// <see cref="LegacyDigestSchema30Observer.NativeTrace29"/> and <see cref="LegacyDigestSchema30Observer.NativeFields29"/>,
        /// which hand that 29 on here. Admission here is the literal header, literal dialogue rules 1, the closed pilot's
        /// storage - the three ordered lists with no role grant and no veto occasion, and a present-null conditional term
        /// on every canonical row - no getter in a field view, and the complete independent frozen29 contract, which
        /// accepts the pilot's rules 1 and nothing later.</para>
        /// </summary>
        internal static JObject NativeRetained29(JObject source, bool getterTrace) => Native(source, getterTrace);

        /// <summary>
        /// The closed pilot's storage on a JSON object of any schema that carries the store: the dialogue object with its
        /// three ordered lists, no role grant and no veto occasion, and a present-null conditional term on every canonical
        /// row. The native contract's own storage check, which the schema-30 observer's native entry also runs on the
        /// current JSON before it projects, so each refusal comes from the check that owns it.
        /// </summary>
        internal static void RequireNativeStore(JObject source)
        {
            Require(source != null && source["dialogue"] is JObject, "The complete dialogue object is required.");
            var dialogue = (JObject)source["dialogue"];
            Require(dialogue.Properties().Select(p => p.Name).SequenceEqual(new[] { "episodes", "grants", "vetoOccasions" })
                && dialogue["episodes"] is JArray && Empty(dialogue["grants"]) && Empty(dialogue["vetoOccasions"]),
                "Only the pilot's ordered dialogue lists, with no grant and no veto occasion, are admitted natively.");
            Require(source["unifiedCommitments"] is JArray, "The complete canonical commitment array is required.");
            foreach (var token in (JArray)source["unifiedCommitments"])
                Require(token is JObject && token["conditionalVeto"]?.Type == JTokenType.Null,
                    "Every canonical row must retain a present-null conditionalVeto.");
        }

        /// <summary>
        /// The retained FindDecision answer view, owned by this exact reader and canonical source, for a state the
        /// caller has already admitted whole (<see cref="LegacyDigestSchema30Observer.FindDecisionAnswers28"/>). This
        /// cannot project caller-supplied decisions or arbitrary named JSON keys. Actual reader exceptions still
        /// propagate unchanged to the historical Ask boundary. A rules-1 source is admitted there by the native arm
        /// instead (the answers are its canonical rows', which the pilot holds to a null conditional term), and every
        /// other check is the same.
        /// </summary>
        internal static JArray FindDecisionAnswersOfAnAdmittedSource28(EpisodeState state)
        {
            Require(state != null, "A current source state is required.");
            string before = Json(JObject.FromObject(state));
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

        private static JObject Project(JObject source, bool getterTrace)
        {
            try
            {
                Require(source != null, "A retained schema-29 JSON object is required.");
                Require(Integer(source["schemaVersion"], 29), "Only literal schema29 is a retained projection input.");
                string sourceBefore = Json(source);
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
                // A field view never carries the getters; only a trace view may.
                if (!getterTrace)
                    Require(Json(WithoutGetters(source)) == sourceBefore, "A field view must carry no getter.");

                var projected = (JObject)source.DeepClone();
                projected.Remove("dialogueRulesVersion");
                projected.Remove("dialogue");
                foreach (JObject row in (JArray)projected["unifiedCommitments"]) row.Remove("conditionalVeto");
                projected["schemaVersion"] = 28;
                Fixed28(WithoutGetters(projected));
                Require(Json(Restore(projected, source)) == sourceBefore,
                    "The retained projection must have the exact three-addition/header structural and order inverse.");
                Require(Json(source) == sourceBefore,
                    "The retained source, JSON, numeric tokens and property/list order must remain unchanged.");
                return projected;
            }
            catch (Exception error) when (error is JsonException || error is OverflowException
                || error is ArgumentException || error is InvalidOperationException || error is FormatException)
            { throw new InvalidDataException("The schema29 digest observer exceeds its fixed contract.", error); }
        }

        private static JObject Native(JObject source, bool getterTrace)
        {
            try
            {
                Require(source != null, "A retained schema-29 JSON object is required.");
                Require(Integer(source["schemaVersion"], 29), "Only literal schema29 is a native input.");
                string sourceBefore = Json(source);
                Require(Integer(source["dialogueRulesVersion"], 1), "Only present integer-one dialogue rules are admitted natively.");
                RequireNativeStore(source);
                // A field view never carries the getters; only a trace view may.
                if (!getterTrace)
                    Require(Json(WithoutGetters(source)) == sourceBefore, "A field view must carry no getter.");
                var native = (JObject)source.DeepClone();
                // The object itself faces the complete independent frozen29 contract, as a projection faces frozen28's.
                Fixed29(WithoutGetters(native));
                Require(Json(native) == sourceBefore && Json(source) == sourceBefore,
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

        /// <summary>The frozen schema-29 whole reader, reached as <see cref="LegacyDigestSchema30Observer"/> reaches it in the subset.</summary>
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

        private static bool Empty(JToken token) => token is JArray array && array.Count == 0;
        private static bool Integer(JToken token, long expected) => token?.Type == JTokenType.Integer && (long)token == expected;
        private static string Json(JToken token) => token.ToString(Formatting.None);
        private static void Require(bool accepted, string reason)
        { if (!accepted) throw new InvalidDataException(reason); }
    }
}
