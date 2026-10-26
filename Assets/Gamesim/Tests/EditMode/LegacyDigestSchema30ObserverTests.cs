using System;
using System.IO;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// SCH30's current30 to retained29 boundary (<see cref="LegacyDigestSchema30Observer"/>, ALL-STAR-HISTORIES-PLAN 8.6),
    /// and the retained-29 entry it hands on to. An inert current source projects exactly; every pooled field missing,
    /// null, nonzero or out of order, an unknown root key, a wrong header, a foreign source and an invalid one are
    /// refused, and the refused source is unchanged. Not a new historical writer or a replacement digest.
    /// </summary>
    public sealed class LegacyDigestSchema30ObserverTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void AnInertCurrentSourceProjectsToTheRetained29WithoutMutation(int mode)
        {
            var state = LegacyDigestSchema27TestStates.Witness(mode, mode == 0 ? "events" : "promise");
            var trace = JObject.FromObject(state);
            var fields = OldSaveTrace.Fields(state);
            string stateBefore = Text(state), traceBefore = Text(trace), fieldsBefore = Text(fields);
            Assert.That((int)trace["schemaVersion"], Is.EqualTo(30));
            var projectedTrace = LegacyDigestSchema30Observer.ProjectTrace29(state, trace);
            var projectedFields = LegacyDigestSchema30Observer.ProjectFields29(state, fields);
            Assert.That(Text(projectedTrace), Is.EqualTo(Text(Expected29(trace))), "Every retained number, property and list position stays.");
            Assert.That(Text(projectedFields), Is.EqualTo(Text(Expected29(fields))));
            Assert.That(JToken.DeepEquals(projectedTrace["Active"], trace["Active"]), Is.True, "A trace keeps its getters.");
            Assert.That(projectedFields.Property("Active"), Is.Null);
            Assert.That(Text(projectedFields), Is.EqualTo(Text(OldSaveTrace.Project(fields, 29))),
                "OldSaveTrace's schema-30 literals take the same state to the same 29.");
            // The composed entries hand the projection to the retained-29 observer unchanged.
            Assert.That(Text(LegacyDigestSchema30Observer.ProjectTrace28(state, trace)),
                Is.EqualTo(Text(LegacyDigestSchema29Observer.ProjectRetained28(projectedTrace, true))));
            Assert.That(Text(LegacyDigestSchema30Observer.ProjectFields28(state, fields)),
                Is.EqualTo(Text(LegacyDigestSchema29Observer.ProjectRetained28(projectedFields, false))));
            projectedTrace["contestants"][0]["name"] = "Detached trace";
            projectedFields["contestants"][0]["name"] = "Detached fields";
            Assert.That(Text(state), Is.EqualTo(stateBefore));
            Assert.That(Text(trace), Is.EqualTo(traceBefore));
            Assert.That(Text(fields), Is.EqualTo(fieldsBefore));
        }

        [TestCase("alumniRulesVersion", "missing")] [TestCase("alumniRulesVersion", "null")]
        [TestCase("alumniRulesVersion", "nonzero")] [TestCase("alumniRulesVersion", "text")]
        [TestCase("alumniCatalogEdition", "missing")] [TestCase("alumniCatalogEdition", "edition")]
        [TestCase("alumniCatalogEdition", "empty")]
        [TestCase("tuningRulesVersion", "missing")] [TestCase("tuningRulesVersion", "null")]
        [TestCase("tuningRulesVersion", "nonzero")] [TestCase("tuningRulesVersion", "fraction")]
        public void EveryPooledFieldRefusesMissingNullOrNonInertJson(string field, string defect)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "promise");
            var trace = JObject.FromObject(state);
            Assert.DoesNotThrow(() => LegacyDigestSchema30Observer.ProjectTrace29(state, trace));
            switch (defect)
            {
                case "missing": trace.Remove(field); break;
                case "null": trace[field] = JValue.CreateNull(); break;
                case "nonzero": trace[field] = 1; break;
                case "text": trace[field] = "0"; break;
                case "fraction": trace[field] = 0.0; break;
                case "edition": trace[field] = "allstar-histories-1"; break;
                default: trace[field] = ""; break;
            }
            Refused(state, trace);
        }

        [TestCase("first")] [TestCase("swapped")] [TestCase("after-getter")]
        public void ThePooledFieldsMustFollowTheDialogueStoreInTheirDeclaredOrder(string defect)
        {
            var state = LegacyDigestSchema27TestStates.Witness(1, "promise");
            var trace = JObject.FromObject(state);
            if (defect == "first") { var moved = trace.Property("alumniRulesVersion"); moved.Remove(); trace.AddFirst(moved); }
            else if (defect == "swapped")
            { var moved = trace.Property("tuningRulesVersion"); moved.Remove(); trace.Property("alumniRulesVersion").AddBeforeSelf(moved); }
            else { var moved = trace.Property("alumniCatalogEdition"); moved.Remove(); trace.Add(moved); }
            Refused(state, trace, "in their declared order");
        }

        [TestCase("unknown-root")] [TestCase("header-29")] [TestCase("header-31")] [TestCase("header-text")]
        [TestCase("session")] [TestCase("rng")]
        public void AValidSourceCannotAuthorizeUnknownFutureOrForeignJson(string defect)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "promise");
            var other = state.Clone();
            if (defect == "session") other.sessionId += "-another";
            if (defect == "rng") other.randomState = unchecked(other.randomState + 1u);
            var trace = JObject.FromObject(other);
            switch (defect)
            {
                case "unknown-root": trace["unknown30"] = 0; break;
                case "header-29": trace["schemaVersion"] = 29; break;
                case "header-31": trace["schemaVersion"] = 31; break;
                case "header-text": trace["schemaVersion"] = "30"; break;
            }
            Refused(state, trace, defect.StartsWith("header-", StringComparison.Ordinal) ? "Only literal schema30" : null);
        }

        [Test]
        public void CompleteCurrentValidationRunsBeforeOtherwiseInertProjection()
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "promise");
            state.contestants[1].id = state.contestants[0].id;
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False);
            Refused(state, JObject.FromObject(state), "The current source state is invalid:");
        }

        [Test]
        public void ANonzeroPooledValueIsAnInvalidCurrentSourceAndIsNeverErased()
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "promise");
            foreach (string field in new[] { "alumniRulesVersion", "tuningRulesVersion" })
            {
                var enabled = state.Clone();
                typeof(EpisodeState).GetField(field).SetValue(enabled, 1);
                Assert.That(EpisodeValidation.TryValidate(enabled, out string reason), Is.False, "No code reads a pooled value yet: " + field);
                Assert.That(reason, Is.EqualTo("Schema 30's pooled rules are not enabled in this build."));
                Refused(enabled, JObject.FromObject(enabled), "The current source state is invalid:");
            }
        }

        /// <summary>The retained-29 entry takes a schema-29 JSON object and no live state, as a 29 build wrote it.</summary>
        [TestCase("s29-default-w2-social")] [TestCase("s29-mode1-allweek-w3-veto-meeting")] [TestCase("s29-mode2-allweek-w3-campaign")]
        public void TheRetainedTwentyNineEntryTakesAWritersSchemaTwentyNineFieldsAndNoState(string id)
        {
            var fields = Fixture(id);
            string before = Text(fields);
            var projected = LegacyDigestSchema29Observer.ProjectRetained28(fields, false);
            Assert.That((int)projected["schemaVersion"], Is.EqualTo(28));
            Assert.That(Text(projected), Is.EqualTo(Text(OldSaveTrace.Project(fields, 28))));
            Assert.That(Text(fields), Is.EqualTo(before));
            foreach (string defect in new[] { "header-30", "rule-1", "getter" })
            {
                var bad = (JObject)fields.DeepClone();
                if (defect == "header-30") bad["schemaVersion"] = 30;
                else if (defect == "rule-1") bad["dialogueRulesVersion"] = 1;
                else bad["Active"] = new JArray();
                string badBefore = Text(bad);
                Assert.Throws<InvalidDataException>(() => LegacyDigestSchema29Observer.ProjectRetained28(bad, false), defect);
                Assert.That(Text(bad), Is.EqualTo(badBefore));
            }
        }

        private static JObject Expected29(JObject source)
        {
            var result = (JObject)source.DeepClone();
            foreach (string name in LegacyDigestSchema30Observer.Pooled) result.Remove(name);
            result["schemaVersion"] = 29;
            return result;
        }

        private static void Refused(EpisodeState state, JObject trace, string reason = null)
        {
            string stateBefore = Text(state), traceBefore = Text(trace);
            var error = Assert.Throws<InvalidDataException>(() => LegacyDigestSchema30Observer.ProjectTrace29(state, trace));
            if (reason != null) Assert.That(error.Message, Does.Contain(reason));
            Assert.Throws<InvalidDataException>(() => LegacyDigestSchema30Observer.ProjectTrace28(state, trace));
            var fields = LegacyDigestSchema30Observer.WithoutGetters(trace);
            string fieldsBefore = Text(fields);
            Assert.Throws<InvalidDataException>(() => LegacyDigestSchema30Observer.ProjectFields29(state, fields));
            Assert.That(Text(fields), Is.EqualTo(fieldsBefore));
            Assert.That(Text(state), Is.EqualTo(stateBefore));
            Assert.That(Text(trace), Is.EqualTo(traceBefore));
        }

        private static JObject Fixture(string id)
        {
#if UNITY_EDITOR
            string root = Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "OldSaves");
#else
            string root = Path.Combine(AppContext.BaseDirectory, "OldSaves");
#endif
            return (JObject)JObject.Parse(File.ReadAllText(Path.Combine(root, id + ".save.json")))["state"];
        }

        private static string Text(EpisodeState state) => Text(JObject.FromObject(state));
        private static string Text(JToken token) => token.ToString(Formatting.None);
    }
}
