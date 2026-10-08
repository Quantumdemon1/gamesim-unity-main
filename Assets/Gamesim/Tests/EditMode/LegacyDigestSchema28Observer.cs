using System;
using System.IO;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Test-only, source-bound observer of a live schema-28 digest trace (WAVE-D-NPC-PACTS-PLAN §0.3,
    /// X3). NOT SaveJson or a production migration. In order it
    /// <list type="number">
    /// <item>runs the real current validator on the source state;</item>
    /// <item>requires every schema-28 field present and inert: three zero start weeks, D2's
    /// 0/-1/0/0/[]/[] cadence on the NPC world and D3's empty plans on the ledger;</item>
    /// <item>removes exactly those fields and sets the header to 27;</item>
    /// <item>proves the independent fixed27 contract accepts the projection's field view, and that
    /// the literals restored where the serializer puts them give back the field view exactly;</item>
    /// <item>hands the projection to the schema-27 observer's field checks, which take it on to 26;</item>
    /// </list>
    /// and last binds the trace to the state's own default JSON. Unknown members are never pruned:
    /// fixed27 refuses them.
    /// </summary>
    internal static class LegacyDigestSchema28Observer
    {
        internal static readonly string[] RootFields = { "allianceLeakRulesStartWeek", "pactPlanRulesStartWeek", "allWeekRulesStartWeek" };
        internal static readonly string[] NpcSocialFields = { "beatWeek", "beatWindow", "beatsFired", "beatSeats", "beatPlan", "acts" };
        internal static readonly string[] LedgerFields = { "plans" };

        /// <summary>The checks, returning the detached getter-free schema-28 field view.</summary>
        internal static JObject CheckFieldObserver(EpisodeState state, JObject trace) => Check(state, trace, out _);

        /// <summary>The checks, returning the detached trace26 with its getter values retained.</summary>
        internal static JObject ProjectTrace(EpisodeState state, JObject trace)
        {
            Check(state, trace, out var trace26);
            return trace26;
        }

        private static JObject Check(EpisodeState state, JObject trace, out JObject trace26)
        {
            trace26 = null;
            try
            {
                Require(state != null && trace != null, "A source state and its default trace are required.");
                LegacyDigestSchema27Observer.RequireValidSource(state);
                Require(Integer(trace["schemaVersion"], 28), "Only literal schema28 is an observer input.");
                string originalTrace = Json(trace), originalState = Json(JObject.FromObject(state));

                Require(trace["npcSocial"] is JObject && trace["ledger"] is JObject, "The NPC world and the ledger must be stored objects.");
                foreach (string name in RootFields) Require(Integer(trace[name], 0), "A Wave D start week must be present integer zero: " + name);
                var social = (JObject)trace["npcSocial"];
                Require(Integer(social["beatWeek"], 0) && Integer(social["beatWindow"], -1) && Integer(social["beatsFired"], 0)
                    && Integer(social["beatSeats"], 0), "D2's beat cadence must be present and inert: 0, -1, 0, 0.");
                Require(Empty(social["beatPlan"]) && Empty(social["acts"]), "D2's beat plan and acts must be present and empty.");
                Require(Empty(trace["ledger"]["plans"]), "D3's plans must be present and empty.");

                var trace27 = (JObject)trace.DeepClone();
                Remove(trace27);
                trace27["schemaVersion"] = 27;

                var field28 = WithoutGetters(trace);
                var field27 = WithoutGetters(trace27);
                Fixed27(field27);
                Require(Json(Restore(field27)) == Json(field28),
                    "The historical projection must have the exact Wave D literal/header structural and order inverse.");

                trace26 = LegacyDigestSchema27Observer.ProjectFields(trace27);
                // Bound last, so no unknown member was filtered away before the fixed contracts saw it.
                LegacyDigestSchema27Observer.RequireOwnTrace(state, trace);
                Require(Json(trace) == originalTrace && Json(JObject.FromObject(state)) == originalState,
                    "The source state, trace, fields and property/list order must remain unchanged.");
                return field28;
            }
            catch (Exception error) when (error is JsonException || error is OverflowException
                || error is ArgumentException || error is InvalidOperationException || error is FormatException)
            { throw new InvalidDataException("The schema28 digest observer exceeds its fixed contract.", error); }
        }

        /// <summary>Removes exactly the schema-28 fields, wherever they stand.</summary>
        internal static void Remove(JObject payload)
        {
            foreach (string name in RootFields) payload.Remove(name);
            var social = (JObject)payload["npcSocial"];
            foreach (string name in NpcSocialFields) social.Remove(name);
            var ledger = (JObject)payload["ledger"];
            foreach (string name in LedgerFields) ledger.Remove(name);
        }

        /// <summary>The inert literals appended where the serializer declares them: last in each owner.</summary>
        private static JObject Restore(JObject field27)
        {
            var restored = (JObject)field27.DeepClone();
            restored["schemaVersion"] = 28;
            foreach (string name in RootFields) restored.Add(name, 0);
            var social = (JObject)restored["npcSocial"];
            social.Add("beatWeek", 0); social.Add("beatWindow", -1); social.Add("beatsFired", 0); social.Add("beatSeats", 0);
            social.Add("beatPlan", new JArray()); social.Add("acts", new JArray());
            ((JObject)restored["ledger"]).Add("plans", new JArray());
            return restored;
        }

        private static JObject WithoutGetters(JObject trace)
        {
            var field = (JObject)trace.DeepClone();
            field.Remove("Active");
            if (field["houseEvents"] is JArray events)
                foreach (var row in events) if (row is JObject item) item.Remove("IsStory");
            return field;
        }

        private static void Fixed27(JObject field27)
        {
#if UNITY_5_3_OR_NEWER
            var historicalAssembly = typeof(Gamesim.Persistence.EpisodeSaveStore).Assembly;
#else
            // The pure project compiles ONLY the isolated fixed contracts beside the simulation.
            var historicalAssembly = typeof(EpisodeState).Assembly;
#endif
            var contract = historicalAssembly.GetType("Gamesim.Persistence.FrozenEpisodeV27", true);
            var validate = contract.GetMethod("Validate", BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(JObject) }, null);
            Require(contract.Assembly == historicalAssembly && validate != null
                && validate.DeclaringType == contract && validate.IsAssembly && validate.ReturnType == typeof(void),
                "The exact independent fixed27 historical contract is required; no fallback is allowed.");
            try { validate.Invoke(null, new object[] { field27 }); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        private static bool Empty(JToken token) => token is JArray array && array.Count == 0;
        private static bool Integer(JToken token, long expected) =>
            token?.Type == JTokenType.Integer && (long)token == expected;
        private static string Json(JToken token) => token.ToString(Formatting.None);
        private static void Require(bool accepted, string reason)
        { if (!accepted) throw new InvalidDataException(reason); }
    }
}
