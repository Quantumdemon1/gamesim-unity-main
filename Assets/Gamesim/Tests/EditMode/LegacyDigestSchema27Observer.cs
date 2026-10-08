using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Test-only, source-bound observer of the default getter-bearing digest trace. This is
    /// NOT SaveJson or a production migration. Native companions separately compare the real APIs.
    /// Unknown members are retained for the independent fixed historical contract to refuse.
    /// </summary>
    internal static class LegacyDigestSchema27Observer
    {
        private static readonly string[] Markers = { "voteBindingWeek", "voteFirstRevealWeek" };
        private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
        private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

        internal static JObject CheckFieldObserver(EpisodeState state, JObject trace)
        {
            try { return Check(state, trace); }
            catch (Exception error) when (error is JsonException || error is OverflowException
                || error is ArgumentException || error is InvalidOperationException || error is FormatException)
            { throw new InvalidDataException("The schema27 digest observer exceeds its fixed contract.", error); }
        }

        internal static JObject ProjectTrace(EpisodeState state, JObject trace)
        {
            CheckFieldObserver(state, trace);
            var detached = (JObject)trace.DeepClone();
            RemoveMarkers(detached);
            detached["schemaVersion"] = 26;
            // Getter values remain in this trace. Only the separate field observer excludes them.
            return detached;
        }

        private static JObject Check(EpisodeState state, JObject trace)
        {
            Require(state != null && trace != null, "A source state and its default trace are required.");
            var validationType = typeof(EpisodeState).Assembly.GetType("Gamesim.Simulation.EpisodeValidation", true);
            var validation = validationType.GetMethod("TryValidate", PublicStatic, null,
                new[] { typeof(EpisodeState), typeof(string).MakeByRefType() }, null);
            Require(validation != null && validation.DeclaringType == validationType
                && validation.ReturnType == typeof(bool), "The real current public validator is required.");
            var arguments = new object[] { state, null };
            Require((bool)Invoke(validation, arguments), "The current source state is invalid: " + arguments[1]);
            Require(Integer(trace["schemaVersion"], 27), "Only literal schema27 is an observer input.");
            string originalTrace = Json(trace), originalState = Json(JObject.FromObject(state));

            CheckGetter(typeof(EpisodeState), "Active", typeof(IEnumerable<ContestantState>));
            CheckGetter(typeof(HouseEventState), "IsStory", typeof(bool));
            Require(trace["contestants"] is JArray && trace["Active"] is JArray,
                "The actual contestants and Active getter arrays are required.");
            var expectedActive = new JArray();
            foreach (var token in (JArray)trace["contestants"])
            {
                Require(token is JObject, "A contestant must be a stored object.");
                if (Integer(token["status"], (int)ContestantStatus.Active)) expectedActive.Add(token.DeepClone());
            }
            Require(Json(expectedActive) == Json(trace["Active"]),
                "Active must equal the complete source-order active contestant rows.");
            Require(trace["houseEvents"] is JArray, "The actual stored event array is required.");
            foreach (var token in (JArray)trace["houseEvents"])
            {
                Require(token is JObject && token["IsStory"]?.Type == JTokenType.Boolean
                    && token["kind"]?.Type == JTokenType.String, "Every actual event requires its typed IsStory getter.");
                Require((bool)token["IsStory"] == ((string)token["kind"] == HouseEventKind.Story),
                    "IsStory must equal the original kind comparison.");
            }

            Require(Integer(trace["unifiedCommitmentRulesVersion"], 0)
                || Integer(trace["unifiedCommitmentRulesVersion"], 1), "Only recorded public modes0/1 are observable.");
            Require(trace["unifiedVoteReveals"] is JArray archive && archive.Count == 0,
                "No real private reveal evidence can be projected.");
            Require(trace["unifiedCommitments"] is JArray rows && rows.Count <= 400,
                "The bounded canonical list must be present.");
            foreach (var token in (JArray)trace["unifiedCommitments"])
            {
                Require(token is JObject row && (string)row["kind"] == UnifiedCommitments.Safety,
                    "Only existing Safety rows are observable.");
                Require(token["targetId"]?.Type == JTokenType.Null && token["subtype"]?.Type == JTokenType.Null,
                    "The prior inert extensions must remain present-null.");
                foreach (var marker in Markers)
                    Require(Integer(token[marker], 0), "A chronology marker must be present integer zero: " + marker);
            }

            var field27 = (JObject)trace.DeepClone();
            field27.Remove("Active");
            foreach (JObject row in (JArray)field27["houseEvents"]) row.Remove("IsStory");
            CheckNeutralInverse(field27);
            // A separately valid payload is not necessarily this source state's observer. Binding
            // comes after fixed validation so unknown members are not erased or silently filtered.
            Require(Json(trace) == originalState, "The trace is not the actual default JSON of this source state.");
            Require(Json(trace) == originalTrace && Json(JObject.FromObject(state)) == originalState,
                "The source state, trace, fields and property/list order must remain unchanged.");
            return field27;
        }

        private static void CheckNeutralInverse(JObject field27)
        {
            var field26 = (JObject)field27.DeepClone();
            RemoveMarkers(field26);
            field26["schemaVersion"] = 26;
#if UNITY_5_3_OR_NEWER
            var historicalAssembly = typeof(Gamesim.Persistence.EpisodeSaveStore).Assembly;
#else
            // The pure project compiles ONLY the isolated fixed contracts beside the simulation.
            var historicalAssembly = typeof(EpisodeState).Assembly;
#endif
            var contract = historicalAssembly.GetType("Gamesim.Persistence.FrozenEpisodeV26", true);
            var validate = contract.GetMethod("Validate", BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(JObject) }, null);
            Require(contract.Assembly == historicalAssembly && validate != null
                && validate.DeclaringType == contract && validate.IsAssembly && validate.ReturnType == typeof(void),
                "The exact independent fixed26 historical contract is required; no fallback is allowed.");
            Invoke(validate, new object[] { field26 });

            var inverse = (JObject)field26.DeepClone();
            inverse["schemaVersion"] = 27;
            var sourceRows = (JArray)field27["unifiedCommitments"];
            var inverseRows = (JArray)inverse["unifiedCommitments"];
            for (int index = 0; index < sourceRows.Count; index++)
            {
                var original = (JObject)sourceRows[index];
                var restored = (JObject)inverseRows[index];
                var names = original.Properties().Select(property => property.Name).ToArray();
                // Preserve marker insertion positions as test-local metadata, not stored history.
                foreach (string marker in names.Where(name => Markers.Contains(name)))
                {
                    int position = Array.IndexOf(names, marker);
                    var next = names.Skip(position + 1).Select(restored.Property).FirstOrDefault(property => property != null);
                    var value = new JProperty(marker, 0);
                    if (next == null) restored.Add(value); else next.AddBeforeSelf(value);
                }
            }
            Require(JToken.DeepEquals(inverse, field27) && Json(inverse) == Json(field27),
                "The historical projection must have the exact two-zero/header structural and order inverse.");
        }

        private static void RemoveMarkers(JObject payload)
        {
            foreach (JObject row in (JArray)payload["unifiedCommitments"])
                foreach (var marker in Markers) row.Remove(marker);
        }

        private static void CheckGetter(Type owner, string name, Type result)
        {
            var property = owner.GetProperty(name, PublicInstance);
            Require(owner.IsSealed && owner.GetField(name, PublicInstance) == null && property != null
                && property.DeclaringType == owner && property.PropertyType == result
                && property.GetIndexParameters().Length == 0 && property.GetGetMethod() != null
                && property.GetSetMethod(true) == null, "The exact audited getter binding is required: " + name);
        }

        private static bool Integer(JToken token, long expected) =>
            token?.Type == JTokenType.Integer && (long)token == expected;
        private static string Json(JToken token) => token.ToString(Formatting.None);
        private static object Invoke(MethodInfo method, object[] arguments)
        {
            try { return method.Invoke(null, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private static void Require(bool accepted, string reason)
        { if (!accepted) throw new InvalidDataException(reason); }
    }
}
