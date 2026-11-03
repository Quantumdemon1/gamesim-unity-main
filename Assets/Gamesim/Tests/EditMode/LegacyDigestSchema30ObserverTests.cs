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
            // Each field's own check runs before the own-view binding, so its words pin it: the binding alone would
            // refuse every edited trace too.
            Refused(state, trace, field == "alumniRulesVersion" ? "Only present integer-zero alumni rules"
                : field == "alumniCatalogEdition" ? "Only a present-null alumni edition"
                : "Only present integer-zero tuning rules");
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

        /// <summary>
        /// A pooled value no slice defines, or the alumni rules without their edition, is an invalid current source, and the
        /// observer refuses it before it projects anything. From H1b the alumni rules define 1 with an edition, and from
        /// section 14 every value to AlumniRules.Latest, so 1 alone is refused for its missing edition and the first
        /// undefined value, Latest + 1, for the pooled rules (the SCH30 form set 1 and read the pooled message for both
        /// fields; H1b's held the literal 2).
        /// </summary>
        [TestCase("alumniRulesVersion", 1, "The alumni rules need the edition their season was built from.")]
        [TestCase("alumniRulesVersion", AlumniRules.Latest + 1, "Schema 30's pooled rules are not enabled in this build.")]
        [TestCase("tuningRulesVersion", 1, "Schema 30's pooled rules are not enabled in this build.")]
        public void ANonzeroPooledValueIsAnInvalidCurrentSourceAndIsNeverErased(string field, int value, string expected)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "promise");
            var enabled = state.Clone();
            typeof(EpisodeState).GetField(field).SetValue(enabled, value);
            Assert.That(EpisodeValidation.TryValidate(enabled, out string reason), Is.False, "No code reads this pooled value: " + field + " " + value);
            Assert.That(reason, Is.EqualTo(expected));
            Refused(enabled, JObject.FromObject(enabled), "The current source state is invalid:");
        }

        /// <summary>
        /// H1b: a valid season on the alumni rules has no schema-29 view - its histories exist only from 30 - so the
        /// observer refuses to project it, by its own alumni check, and changes nothing. Such a season is checkpointed
        /// at the live 30 only.
        /// </summary>
        [Test]
        public void ASeasonOnTheAlumniRulesIsNeverProjectedToTwentyNine()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8, Roster = CastTemplates.Roster.AllStars }, 3005u);
            ShippedRules.ApplyFresh(state);
            Assert.That(state.alumniRulesVersion, Is.EqualTo(AlumniRules.Latest));
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
            Refused(state, JObject.FromObject(state), "Only present integer-zero alumni rules may be projected.");
        }

        /// <summary>
        /// H1b's native-30 arm: a season on the alumni rules is admitted at the live 30 unprojected, under the projection's
        /// own source contract, and checkpointed there only; every defect is refused and changes nothing.
        /// </summary>
        [TestCase(null)] [TestCase("alumni-off")] [TestCase("edition-text")] [TestCase("tuning-json")] [TestCase("order")]
        [TestCase("unknown-root")] [TestCase("header-29")] [TestCase("invalid-source")]
        public void ASeasonOnTheAlumniRulesIsAdmittedNativelyAtThirtyAndOnlyThere(string defect)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8, Roster = CastTemplates.Roster.AllStars }, 3006u);
            ShippedRules.ApplyFresh(state);
            if (defect == "alumni-off") { state.alumniRulesVersion = AlumniRules.Off; state.alumniCatalogEdition = null; }
            if (defect == "invalid-source") state.tuningRulesVersion = 1;
            var fields = LegacyDigestSchema30Observer.WithoutGetters(JObject.FromObject(state));
            switch (defect)
            {
                case "edition-text": fields["alumniCatalogEdition"] = "allstar-histories-2"; break;
                case "tuning-json": fields["tuningRulesVersion"] = 1; break;
                case "order": { var p = fields.Property("alumniRulesVersion"); p.Remove(); fields.Add(p); break; }
                case "unknown-root": fields["unknown30"] = 0; break;
                case "header-29": fields["schemaVersion"] = 29; break;
            }
            string stateBefore = Text(state), fieldsBefore = Text(fields);
            if (defect == null)
            {
                Assert.That(LegacyDigestSchema30Observer.OnTheAlumniRules(state), Is.True);
                Assert.That(Text(LegacyDigestSchema30Observer.AdmitNative30(state, fields)), Is.EqualTo(fieldsBefore), "Admitted as it is.");
                Assert.That(CurrentOldSaveTrace.Checkpoint(state, 30), Is.EqualTo(OldSaveTrace.Checkpoint(state, 30)),
                    "Checkpointed at the live 30 as the recorder checkpoints its own schema.");
                Assert.Throws<InvalidDataException>(() => CurrentOldSaveTrace.Checkpoint(state, 29), "No reference schema below 30.");
                Assert.That(Text(LegacyDigestSchema30Observer.FindDecisionAnswers28(state)),
                    Is.EqualTo(Text(LegacyDigestSchema29Observer.FindDecisionAnswersOfAnAdmittedSource28(state))),
                    "The historical FindDecision reader admits it at 30 and reads the same canonical rows.");
            }
            else
            {
                var error = Assert.Throws<InvalidDataException>(() => LegacyDigestSchema30Observer.AdmitNative30(state, fields));
                string expected = defect switch
                {
                    "alumni-off" => "Only a season on the alumni rules is admitted natively.",
                    "edition-text" => "The native edition must be the season's own.",
                    "tuning-json" => "Only present integer-zero tuning rules may be admitted.",
                    "order" => "The pooled fields must follow the dialogue store, in their declared order.",
                    "unknown-root" => "The JSON is not this current state's exact own view.",
                    "header-29" => "Only literal schema30 is a native input.",
                    _ => "The current source state is invalid:",
                };
                Assert.That(error.Message, Does.Contain(expected));
                if (defect == "invalid-source") Assert.Throws<InvalidDataException>(() => LegacyDigestSchema30Observer.FindDecisionAnswers28(state));
            }
            Assert.That(Text(fields), Is.EqualTo(fieldsBefore));
            Assert.That(Text(state), Is.EqualTo(stateBefore));
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

        /// <summary>
        /// The retained native-29 entry takes D1's schema-29 writer as the build wrote it - dialogue rules 1, its store
        /// holding the conversation it opened - and no live state: admitted as it is and handed back detached. The
        /// projection to 28 refuses it, and so does the native entry for its header, its rules and a getter.
        /// </summary>
        [Test]
        public void TheNativeTwentyNineEntryTakesTheDialogueWritersSchemaTwentyNineFieldsAndNoState()
        {
            var fields = Fixture("s29-mode2-dialogue1-w2-campaign");
            Assert.That((int)fields["schemaVersion"], Is.EqualTo(29));
            Assert.That((int)fields["dialogueRulesVersion"], Is.EqualTo(1));
            Assert.That(((JArray)fields["dialogue"]["episodes"]).Count, Is.GreaterThan(0), "The writer's store holds its conversation.");
            string before = Text(fields);
            var native = LegacyDigestSchema29Observer.NativeRetained29(fields, false);
            Assert.That(native, Is.Not.SameAs(fields), "A detached copy.");
            Assert.That(Text(native), Is.EqualTo(before), "Admitted as it is: every field, number and order.");
            native["contestants"][0]["name"] = "Detached fields";
            Assert.That(Text(fields), Is.EqualTo(before));
            var projected = Assert.Throws<InvalidDataException>(() => LegacyDigestSchema29Observer.ProjectRetained28(fields, false));
            Assert.That(projected.Message, Is.EqualTo("Only present integer-zero dialogue rules may be projected."));
            Assert.That(Text(fields), Is.EqualTo(before));
            foreach (var (defect, reason) in new[]
            {
                ("header-30", "Only literal schema29 is a native input."),
                ("rule-0", "Only present integer-one dialogue rules are admitted natively."),
                ("getter", "A field view must carry no getter."),
            })
            {
                var bad = (JObject)fields.DeepClone();
                if (defect == "header-30") bad["schemaVersion"] = 30;
                else if (defect == "rule-0") bad["dialogueRulesVersion"] = 0;
                else bad["Active"] = new JArray();
                string badBefore = Text(bad);
                var error = Assert.Throws<InvalidDataException>(() => LegacyDigestSchema29Observer.NativeRetained29(bad, false), defect);
                Assert.That(error.Message, Is.EqualTo(reason), defect);
                Assert.That(Text(bad), Is.EqualTo(badBefore));
            }
        }

        /// <summary>
        /// The merge of the histories with dialogue D1 (ALL-STAR-HISTORIES-PLAN 8.5): an All-Stars season started with the
        /// cast screen's preview chip is on the alumni rules and on dialogue rules 1 at once, so both arms' tests hold for
        /// it and their order decides. The alumni arm comes first at both digest boundaries: the season is admitted natively
        /// at 30 and checkpointed there only, its FindDecision answers are read from its canonical rows, and the native-29
        /// arm, which projects to 29, refuses it by the alumni check. Were the arms reordered, every checkpoint and every
        /// answer of such a season would throw.
        /// </summary>
        [Test]
        public void AnAllStarsSeasonStartedWithThePreviewTakesTheAlumniArmAtThirtyAndNeverTheNativeTwentyNine()
        {
            var chip = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8, Roster = CastTemplates.Roster.AllStars }, 3007u);
            ShippedRules.ApplyFresh(chip, new SeasonBuilder.Choice
                { HouseSize = 8, Roster = CastTemplates.Roster.AllStars, CampaignConversations = true });
            Assert.That(chip.alumniRulesVersion, Is.EqualTo(AlumniRules.Latest), "The chip season seats its alumni's histories.");
            Assert.That(chip.dialogueRulesVersion, Is.EqualTo(1), "The chip season plays the preview.");
            Assert.That(chip.schemaVersion, Is.EqualTo(30));
            Assert.That(EpisodeValidation.TryValidate(chip, out string reason), Is.True, reason);
            Assert.That(LegacyDigestSchema30Observer.OnTheAlumniRules(chip), Is.True);
            Assert.That(LegacyDigestSchema30Observer.IsNative29(chip), Is.True, "Both arms' tests hold; the order decides.");
            var fields = OldSaveTrace.Fields(chip);
            var trace = JObject.FromObject(chip);
            string stateBefore = Text(chip), fieldsBefore = Text(fields), traceBefore = Text(trace);

            Assert.That(Text(LegacyDigestSchema30Observer.AdmitNative30(chip, fields)), Is.EqualTo(fieldsBefore), "Admitted as it is.");
            Assert.That(CurrentOldSaveTrace.Checkpoint(chip, 30), Is.EqualTo(OldSaveTrace.Checkpoint(chip, 30)),
                "Checkpointed at the live 30 as the recorder checkpoints its own schema: the hash of its own fields.");
            foreach (int schema in new[] { 29, 28 })
            {
                var below = Assert.Throws<InvalidDataException>(() => CurrentOldSaveTrace.Checkpoint(chip, schema), "schema " + schema);
                Assert.That(below.Message, Is.EqualTo("A season on the alumni rules has no reference schema below 30."), "schema " + schema);
            }
            Assert.That(Text(LegacyDigestSchema30Observer.FindDecisionAnswers28(chip)),
                Is.EqualTo(Text(LegacyDigestSchema29Observer.FindDecisionAnswersOfAnAdmittedSource28(chip))),
                "The historical FindDecision reader admits it at 30 and reads the same canonical rows.");
            var nativeFields = Assert.Throws<InvalidDataException>(() => LegacyDigestSchema30Observer.NativeFields29(chip, fields));
            Assert.That(nativeFields.Message, Is.EqualTo("Only present integer-zero alumni rules may be projected."),
                "The native-29 arm refuses it by the alumni projection's own check.");
            var nativeTrace = Assert.Throws<InvalidDataException>(() => LegacyDigestSchema30Observer.NativeTrace29(chip, trace));
            Assert.That(nativeTrace.Message, Is.EqualTo("Only present integer-zero alumni rules may be projected."));
            Assert.That(Text(chip), Is.EqualTo(stateBefore));
            Assert.That(Text(fields), Is.EqualTo(fieldsBefore));
            Assert.That(Text(trace), Is.EqualTo(traceBefore));
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
