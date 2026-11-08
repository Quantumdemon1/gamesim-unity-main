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
        [TestCase("tuningRulesVersion", TuningRules.Latest + 1, "Schema 30's pooled rules are not enabled in this build.")]
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
            if (defect == "invalid-source") state.tuningRulesVersion = TuningRules.Latest + 1;
            var fields = LegacyDigestSchema30Observer.WithoutGetters(JObject.FromObject(state));
            switch (defect)
            {
                case "edition-text": fields["alumniCatalogEdition"] = "allstar-histories-2"; break;
                case "tuning-json": fields["tuningRulesVersion"] = state.tuningRulesVersion + 1; break;
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
                    "tuning-json" => "The native tuning rules must be the season's own.",
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

        /// <summary>
        /// B9's native-30 arm: a fresh Regular season since B9-3 is on the tuning rules alone (tuning 1, alumni rules 0), which
        /// exist only from 30, so it is admitted at the live 30 unprojected, under the alumni arm's contract with the alumni
        /// rules at zero and the edition null, and checkpointed there only; the alumni arm still refuses it, and every defect
        /// is refused and changes nothing.
        /// </summary>
        [TestCase(null)] [TestCase("tuning-off")] [TestCase("alumni-json")] [TestCase("edition-text")] [TestCase("tuning-json")]
        [TestCase("order")] [TestCase("unknown-root")] [TestCase("header-29")] [TestCase("invalid-source")]
        public void ASeasonOnTheTuningRulesIsAdmittedNativelyAtThirtyAndOnlyThere(string defect)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 3007u);
            ShippedRules.ApplyFresh(state);
            Assert.That((state.alumniRulesVersion, state.tuningRulesVersion), Is.EqualTo((AlumniRules.Off, TuningRules.Shipped)),
                "Precondition: a fresh Regular season, on tuning 1 and not on the alumni rules.");
            if (defect == "tuning-off") state.tuningRulesVersion = TuningRules.Off;
            if (defect == "invalid-source") state.tuningRulesVersion = TuningRules.Latest + 1;
            var fields = LegacyDigestSchema30Observer.WithoutGetters(JObject.FromObject(state));
            switch (defect)
            {
                case "alumni-json": fields["alumniRulesVersion"] = 1; break;
                case "edition-text": fields["alumniCatalogEdition"] = "allstar-histories-1"; break;
                case "tuning-json": fields["tuningRulesVersion"] = state.tuningRulesVersion + 1; break;
                // The tuning rules are the state's last field, so moving them to the end would change nothing: they go ahead
                // of the alumni rules instead, as the pooled-order test's "swapped" case moves them.
                case "order": { var p = fields.Property("tuningRulesVersion"); p.Remove(); fields.Property("alumniRulesVersion").AddBeforeSelf(p); break; }
                case "unknown-root": fields["unknown30"] = 0; break;
                case "header-29": fields["schemaVersion"] = 29; break;
            }
            string stateBefore = Text(state), fieldsBefore = Text(fields);
            if (defect == "order")
                Assert.That(fields.Properties().Select(p => p.Name).Where(name => Array.IndexOf(LegacyDigestSchema30Observer.Pooled, name) >= 0),
                    Is.EqualTo(new[] { "tuningRulesVersion", "alumniRulesVersion", "alumniCatalogEdition" }), "The defect really moves the field.");
            if (defect == null)
            {
                Assert.That(LegacyDigestSchema30Observer.OnTheTuningRulesAlone(state), Is.True);
                Assert.That(LegacyDigestSchema30Observer.OnTheAlumniRules(state), Is.False);
                Assert.That(Text(LegacyDigestSchema30Observer.AdmitNativeTuned30(state, fields)), Is.EqualTo(fieldsBefore), "Admitted as it is.");
                var alumniArm = Assert.Throws<InvalidDataException>(() => LegacyDigestSchema30Observer.AdmitNative30(state, fields));
                Assert.That(alumniArm.Message, Does.Contain("Only a season on the alumni rules is admitted natively."), "The alumni arm is unchanged.");
                Refused(state, JObject.FromObject(state), "Only present integer-zero tuning rules may be projected.");
                Assert.That(CurrentOldSaveTrace.Checkpoint(state, 30), Is.EqualTo(OldSaveTrace.Checkpoint(state, 30)),
                    "Checkpointed at the live 30 as the recorder checkpoints its own schema.");
                var below = Assert.Throws<InvalidDataException>(() => CurrentOldSaveTrace.Checkpoint(state, 29));
                Assert.That(below.Message, Is.EqualTo("A season on the tuning rules has no reference schema below 30."));
                Assert.That(Text(LegacyDigestSchema30Observer.FindDecisionAnswers28(state)),
                    Is.EqualTo(Text(LegacyDigestSchema29Observer.FindDecisionAnswersOfAnAdmittedSource28(state))),
                    "The historical FindDecision reader admits it at 30 and reads the same canonical rows.");
            }
            else
            {
                var error = Assert.Throws<InvalidDataException>(() => LegacyDigestSchema30Observer.AdmitNativeTuned30(state, fields));
                string expected = defect switch
                {
                    "tuning-off" => "Only a season on the tuning rules alone is admitted natively by the tuning arm.",
                    "alumni-json" => "Only present integer-zero alumni rules are admitted by the tuning arm.",
                    "edition-text" => "Only a present-null alumni edition is admitted by the tuning arm.",
                    "tuning-json" => "The native tuning rules must be the season's own.",
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

        // ------------------------------------------------------------------ dialogue rules 2 (D2) at the native-30 arms

        /// <summary>
        /// D2's season at the digest boundaries: rules 2 exists only from 30, so a season on it has no schema-29 view and the
        /// native-29 arm never takes it. Played by the public walk - the campaign, the conversation opened, its first reply
        /// answered - it is the tuning arm's as it starts, every fresh season from B9 being on tuning 1, and held at tuning 0
        /// by hand it is the dialogue arm's own: either way admitted at 30 as it is, checkpointed there alone, and answered by
        /// the historical FindDecision reader. Every other arm refuses it in its own words, and nothing changes.
        /// </summary>
        [TestCase("tuning", "campaign")] [TestCase("tuning", "conversation")] [TestCase("tuning", "reply")]
        [TestCase("alone", "campaign")] [TestCase("alone", "conversation")] [TestCase("alone", "reply")]
        public void ASeasonOnDialogueRules2IsAdmittedNativelyAtThirtyAndOnlyThere(string arm, string stage)
        {
            var state = Rules2Season(stage, tuningHeld: arm == "alone");
            Assert.That(state.schemaVersion, Is.EqualTo(30), "The live schema.");
            Assert.That(state.dialogueRulesVersion, Is.EqualTo(2));
            Assert.That(state.dialogue.episodes.Count, Is.EqualTo(stage == "campaign" ? 0 : 1));
            Assert.That((state.alumniRulesVersion, state.tuningRulesVersion),
                Is.EqualTo((AlumniRules.Off, arm == "alone" ? TuningRules.Off : TuningRules.Shipped)));
            Assert.That(LegacyDigestSchema30Observer.IsNative29(state), Is.False, "Rules 2 has no schema-29 view.");
            Assert.That(LegacyDigestSchema30Observer.OnTheAlumniRules(state), Is.False);
            Assert.That(LegacyDigestSchema30Observer.OnTheTuningRulesAlone(state), Is.EqualTo(arm == "tuning"));
            Assert.That(LegacyDigestSchema30Observer.OnDialogueRules2Alone(state), Is.EqualTo(arm == "alone"));
            var trace = JObject.FromObject(state);
            var fields = OldSaveTrace.Fields(state);
            string stateBefore = Text(state), traceBefore = Text(trace), fieldsBefore = Text(fields);
            string below;
            if (arm == "alone")
            {
                var nativeTrace = LegacyDigestSchema30Observer.Rules2Trace30(state, trace);
                var nativeFields = LegacyDigestSchema30Observer.Rules2Fields30(state, fields);
                Assert.That(Text(nativeTrace), Is.EqualTo(traceBefore), "The trace at 30, getters and all, as it is.");
                Assert.That(Text(nativeFields), Is.EqualTo(fieldsBefore), "The field view at 30 as it is, store and all.");
                Assert.That(nativeTrace, Is.Not.SameAs(trace));
                Assert.That(nativeFields, Is.Not.SameAs(fields));
                nativeTrace["contestants"][0]["name"] = "Detached trace";
                nativeFields["contestants"][0]["name"] = "Detached fields";
                Assert.That(Text(trace), Is.EqualTo(traceBefore));
                Assert.That(Text(fields), Is.EqualTo(fieldsBefore));
                Assert.That(Refusal(() => LegacyDigestSchema30Observer.AdmitNativeTuned30(state, fields)),
                    Is.EqualTo("Only a season on the tuning rules alone is admitted natively by the tuning arm."));
                below = "A season on dialogue rules 2 has no reference schema below 30.";
            }
            else
            {
                Assert.That(Text(LegacyDigestSchema30Observer.AdmitNativeTuned30(state, fields)), Is.EqualTo(fieldsBefore), "Admitted as it is.");
                Assert.That(Refusal(() => LegacyDigestSchema30Observer.Rules2Trace30(state, trace)),
                    Is.EqualTo("Only a season on dialogue rules 2 alone is admitted natively by the dialogue arm."));
                Assert.That(Refusal(() => LegacyDigestSchema30Observer.Rules2Fields30(state, fields)),
                    Is.EqualTo("Only a season on dialogue rules 2 alone is admitted natively by the dialogue arm."));
                below = "A season on the tuning rules has no reference schema below 30.";
            }
            Assert.That(CurrentOldSaveTrace.Checkpoint(state, 30), Is.EqualTo(OldSaveTrace.Checkpoint(state, 30)),
                "Checkpointed at the live 30 as the recorder checkpoints its own schema.");
            foreach (int schema in new[] { 29, 28, 26 })
                Assert.That(Refusal(() => CurrentOldSaveTrace.Checkpoint(state, schema)), Is.EqualTo(below),
                    "schema " + schema + ": no form below 30, never the rules-1 words.");
            Assert.That(Text(LegacyDigestSchema30Observer.FindDecisionAnswers28(state)),
                Is.EqualTo(Text(LegacyDigestSchema29Observer.FindDecisionAnswersOfAnAdmittedSource28(state))),
                "The historical FindDecision reader admits it at 30 and reads the same canonical rows.");
            Assert.That(Refusal(() => LegacyDigestSchema30Observer.AdmitNative30(state, fields)),
                Is.EqualTo("Only a season on the alumni rules is admitted natively."));
            Assert.That(Refusal(() => LegacyDigestSchema30Observer.NativeFields29(state, fields)),
                Is.EqualTo("Only present integer-one dialogue rules are admitted natively."), "The native-29 arm takes rules 1 alone.");
            Assert.That(Refusal(() => LegacyDigestSchema30Observer.NativeTrace29(state, trace)),
                Is.EqualTo("Only present integer-one dialogue rules are admitted natively."));
            // The rules-0 projection: the tuning literal, or, with tuning held at 0, the frozen schema-29 reader, which
            // never defined rules 2.
            Assert.That(Refusal(() => LegacyDigestSchema30Observer.ProjectFields29(state, fields)),
                Is.EqualTo(arm == "alone" ? "Dialogue rules are not enabled in this build." : "Only present integer-zero tuning rules may be projected."));
            Assert.That(Text(state), Is.EqualTo(stateBefore));
            Assert.That(Text(trace), Is.EqualTo(traceBefore));
            Assert.That(Text(fields), Is.EqualTo(fieldsBefore));
        }

        /// <summary>
        /// An All-Stars season on dialogue rules 2 is on the alumni rules too, so the alumni arm, asked first, admits it at
        /// 30 - holding its dialogue rules as the dialogue arm does - and it is checkpointed there alone; the dialogue arm and
        /// the native-29 arm refuse it, each in its own words.
        /// </summary>
        [Test]
        public void AnAllStarsSeasonOnDialogueRules2TakesTheAlumniArmAtThirty()
        {
            var state = AllStarsOnRules2();
            Assert.That(LegacyDigestSchema30Observer.OnTheAlumniRules(state), Is.True);
            Assert.That(LegacyDigestSchema30Observer.OnDialogueRules2Alone(state), Is.False);
            Assert.That(LegacyDigestSchema30Observer.IsNative29(state), Is.False);
            var trace = JObject.FromObject(state);
            var fields = OldSaveTrace.Fields(state);
            string stateBefore = Text(state), traceBefore = Text(trace), fieldsBefore = Text(fields);
            Assert.That(Text(LegacyDigestSchema30Observer.AdmitNative30(state, fields)), Is.EqualTo(fieldsBefore), "Admitted as it is.");
            Assert.That(CurrentOldSaveTrace.Checkpoint(state, 30), Is.EqualTo(OldSaveTrace.Checkpoint(state, 30)));
            foreach (int schema in new[] { 29, 28 })
                Assert.That(Refusal(() => CurrentOldSaveTrace.Checkpoint(state, schema)),
                    Is.EqualTo("A season on the alumni rules has no reference schema below 30."), "schema " + schema);
            Assert.That(Text(LegacyDigestSchema30Observer.FindDecisionAnswers28(state)),
                Is.EqualTo(Text(LegacyDigestSchema29Observer.FindDecisionAnswersOfAnAdmittedSource28(state))));
            Assert.That(Refusal(() => LegacyDigestSchema30Observer.Rules2Trace30(state, trace)),
                Is.EqualTo("Only a season on dialogue rules 2 alone is admitted natively by the dialogue arm."));
            Assert.That(Refusal(() => LegacyDigestSchema30Observer.NativeFields29(state, fields)),
                Is.EqualTo("Only present integer-one dialogue rules are admitted natively."));
            Assert.That(Text(state), Is.EqualTo(stateBefore));
            Assert.That(Text(trace), Is.EqualTo(traceBefore));
            Assert.That(Text(fields), Is.EqualTo(fieldsBefore));
        }

        /// <summary>
        /// The cast screen's chip season from B9 - dialogue rules 1 on tuning 1, here by the literal 1, never the chip - is on
        /// the tuning arm, which is asked before the native-29 arm, and since the dialogue arm landed the tuning arm holds the
        /// season's dialogue rules too (the matrix below refuses each defect). Before the conversation and with it open it is admitted at 30
        /// as it is and checkpointed there alone; the native-29 arm refuses it by the projection's tuning literal.
        /// </summary>
        [TestCase("start")] [TestCase("conversation")]
        public void ASeasonOnDialogueRules1AndTheTuningRulesTakesTheTuningArmAtThirty(string stage)
        {
            var state = Rules1Tuned(stage);
            Assert.That(LegacyDigestSchema30Observer.OnTheTuningRulesAlone(state), Is.True);
            Assert.That(LegacyDigestSchema30Observer.IsNative29(state), Is.True, "Both arms' tests hold; the order decides.");
            Assert.That(LegacyDigestSchema30Observer.OnDialogueRules2Alone(state), Is.False);
            var trace = JObject.FromObject(state);
            var fields = OldSaveTrace.Fields(state);
            string stateBefore = Text(state), traceBefore = Text(trace), fieldsBefore = Text(fields);
            Assert.That(Text(LegacyDigestSchema30Observer.AdmitNativeTuned30(state, fields)), Is.EqualTo(fieldsBefore), "Admitted as it is.");
            Assert.That(CurrentOldSaveTrace.Checkpoint(state, 30), Is.EqualTo(OldSaveTrace.Checkpoint(state, 30)));
            foreach (int schema in new[] { 29, 28 })
                Assert.That(Refusal(() => CurrentOldSaveTrace.Checkpoint(state, schema)),
                    Is.EqualTo("A season on the tuning rules has no reference schema below 30."), "schema " + schema);
            Assert.That(Text(LegacyDigestSchema30Observer.FindDecisionAnswers28(state)),
                Is.EqualTo(Text(LegacyDigestSchema29Observer.FindDecisionAnswersOfAnAdmittedSource28(state))));
            Assert.That(Refusal(() => LegacyDigestSchema30Observer.NativeFields29(state, fields)),
                Is.EqualTo("Only present integer-zero tuning rules may be projected."));
            Assert.That(Refusal(() => LegacyDigestSchema30Observer.Rules2Fields30(state, fields)),
                Is.EqualTo("Only a season on dialogue rules 2 alone is admitted natively by the dialogue arm."));
            Assert.That(Text(state), Is.EqualTo(stateBefore));
            Assert.That(Text(trace), Is.EqualTo(traceBefore));
            Assert.That(Text(fields), Is.EqualTo(fieldsBefore));
        }

        /// <summary>
        /// Every native-30 arm holds a season's dialogue rules to its own frozen set, as the native-29 arm holds rules 1: the
        /// present integer literal on the JSON - a fraction, a text, the other set's literal or an undefined one is refused,
        /// against a state on 2 and against one on 1 - and the closed store, where an unknown key is refused; each in its own
        /// words, ahead of the binding, which alone would refuse every edited JSON. Before the rules-2 arm the alumni arm and
        /// the tuning arm checked none of it. Each refusal leaves the state and the JSON as they were.
        /// </summary>
        [TestCase("alumni", "fraction")] [TestCase("alumni", "text")] [TestCase("alumni", "other")]
        [TestCase("alumni", "undefined")] [TestCase("alumni", "unknown-dialogue")]
        [TestCase("tuning", "fraction")] [TestCase("tuning", "text")] [TestCase("tuning", "other")]
        [TestCase("tuning", "undefined")] [TestCase("tuning", "unknown-dialogue")]
        [TestCase("alone", "fraction")] [TestCase("alone", "text")] [TestCase("alone", "other")]
        [TestCase("alone", "undefined")] [TestCase("alone", "unknown-dialogue")]
        [TestCase("tuning-rules1", "fraction")] [TestCase("tuning-rules1", "text")] [TestCase("tuning-rules1", "other")]
        [TestCase("tuning-rules1", "undefined")] [TestCase("tuning-rules1", "unknown-dialogue")]
        public void EveryNativeThirtyArmHoldsTheDialogueRulesToTheSeasonsFrozenSet(string arm, string defect)
        {
            var state = arm == "alumni" ? AllStarsOnRules2()
                : arm == "tuning-rules1" ? Rules1Tuned("conversation")
                : Rules2Season("conversation", tuningHeld: arm == "alone");
            int frozen = state.dialogueRulesVersion;
            Assert.That(frozen, Is.EqualTo(arm == "tuning-rules1" ? 1 : 2));
            var trace = JObject.FromObject(state);
            var fields = OldSaveTrace.Fields(state);
            foreach (var json in new[] { trace, fields })
                switch (defect)
                {
                    case "fraction": json["dialogueRulesVersion"] = (double)frozen; break;
                    case "text": json["dialogueRulesVersion"] = frozen == 2 ? "2" : "1"; break;
                    case "other": json["dialogueRulesVersion"] = 3 - frozen; break;
                    case "undefined": json["dialogueRulesVersion"] = 3; break;
                    default: json["dialogue"]["unknown30"] = new JArray(); break;
                }
            string stateBefore = Text(state), traceBefore = Text(trace), fieldsBefore = Text(fields);
            string expected = defect == "unknown-dialogue"
                ? "Only the pilot's ordered dialogue lists, with no grant and no veto occasion, are admitted natively."
                : frozen == 2 ? "Only present integer-two dialogue rules are admitted natively." : "Only present integer-one dialogue rules are admitted natively.";
            if (arm == "alumni")
                Assert.That(Refusal(() => LegacyDigestSchema30Observer.AdmitNative30(state, fields)), Is.EqualTo(expected), "the alumni arm");
            else if (arm == "alone")
            {
                Assert.That(Refusal(() => LegacyDigestSchema30Observer.Rules2Trace30(state, trace)), Is.EqualTo(expected), "the dialogue arm's trace");
                Assert.That(Refusal(() => LegacyDigestSchema30Observer.Rules2Fields30(state, fields)), Is.EqualTo(expected), "the dialogue arm's fields");
            }
            else Assert.That(Refusal(() => LegacyDigestSchema30Observer.AdmitNativeTuned30(state, fields)), Is.EqualTo(expected), "the tuning arm");
            Assert.That(Text(state), Is.EqualTo(stateBefore));
            Assert.That(Text(trace), Is.EqualTo(traceBefore));
            Assert.That(Text(fields), Is.EqualTo(fieldsBefore));
        }

        /// <summary>
        /// The dialogue arm takes nothing but a valid season on dialogue rules 2 alone and its own JSON: not a season on rules
        /// 1 or on the tuning rules (theirs are other arms), not an invalid one (a grant), and not a JSON with a pooled field
        /// off its literal or out of its order, another header, a foreign key, another state's values or the other view's
        /// getters. Each is refused by the check that owns it, and leaves the state and the JSON as they were.
        /// </summary>
        [TestCase("rules-1")] [TestCase("tuning-1")] [TestCase("alumni-json")] [TestCase("edition-text")] [TestCase("tuning-json")]
        [TestCase("order")] [TestCase("header-29")] [TestCase("header-31")] [TestCase("unknown-root")] [TestCase("foreign")]
        [TestCase("getters")] [TestCase("grants")]
        public void TheDialogueArmAdmitsNothingButAValidRules2SeasonAloneAndItsOwnJson(string defect)
        {
            var state = defect == "rules-1" ? DialoguePreviewWitness.Find().Conversation
                : Rules2Season("conversation", tuningHeld: defect != "tuning-1");
            if (defect == "grants") state.dialogue.grants.Add(new DialogueRoleGrantState());
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.EqualTo(defect != "grants"), "Only the grant is an invalid save.");
            var view = state;
            if (defect == "foreign")
            {
                view = state.Clone();
                view.sessionId += "-another";
                Assert.That(EpisodeValidation.TryValidate(view, out string reason), Is.True, reason);
            }
            var trace = JObject.FromObject(view);
            var fields = OldSaveTrace.Fields(view);
            foreach (var json in new[] { trace, fields })
                switch (defect)
                {
                    case "alumni-json": json["alumniRulesVersion"] = 1; break;
                    case "edition-text": json["alumniCatalogEdition"] = "allstar-histories-1"; break;
                    case "tuning-json": json["tuningRulesVersion"] = 1; break;
                    // The tuning rules are the state's last field, as in the tuning arm's test: they go ahead of the alumni rules.
                    case "order": { var p = json.Property("tuningRulesVersion"); p.Remove(); json.Property("alumniRulesVersion").AddBeforeSelf(p); break; }
                    case "header-29": json["schemaVersion"] = 29; break;
                    case "header-31": json["schemaVersion"] = 31; break;
                    case "unknown-root": json["unknown30"] = 0; break;
                }
            if (defect == "getters") { var swap = trace; trace = fields; fields = swap; }
            string stateBefore = Text(state), traceBefore = Text(trace), fieldsBefore = Text(fields);
            string expected = defect == "rules-1" || defect == "tuning-1" ? "Only a season on dialogue rules 2 alone is admitted natively by the dialogue arm."
                : defect == "alumni-json" ? "Only present integer-zero alumni rules are admitted by the dialogue arm."
                : defect == "edition-text" ? "Only a present-null alumni edition is admitted by the dialogue arm."
                : defect == "tuning-json" ? "Only present integer-zero tuning rules are admitted by the dialogue arm."
                : defect == "order" ? "The pooled fields must follow the dialogue store, in their declared order."
                : defect.StartsWith("header-", StringComparison.Ordinal) ? "Only literal schema30 is a native input."
                : defect == "grants" ? "The current source state is invalid:"
                : "The JSON is not this current state's exact own view.";
            Assert.That(Refusal(() => LegacyDigestSchema30Observer.Rules2Trace30(state, trace)), Does.StartWith(expected), defect + ": the trace");
            Assert.That(Refusal(() => LegacyDigestSchema30Observer.Rules2Fields30(state, fields)), Does.StartWith(expected), defect + ": the fields");
            if (defect == "grants")
            {
                Assert.Throws<InvalidDataException>(() => CurrentOldSaveTrace.Checkpoint(state, 30));
                Assert.Throws<InvalidDataException>(() => LegacyDigestSchema30Observer.FindDecisionAnswers28(state));
            }
            Assert.That(Text(state), Is.EqualTo(stateBefore));
            Assert.That(Text(trace), Is.EqualTo(traceBefore));
            Assert.That(Text(fields), Is.EqualTo(fieldsBefore));
        }

        private static DialogueRules2Season.Campaign rules2;

        /// <summary>
        /// A genuine season on dialogue rules 2 at a stage: <see cref="DialogueRules2Season.FindCampaign"/>'s nominated
        /// first-week campaign (found once, handed out detached), the conversation opened with its first voter through the
        /// HUD's entry plan, or that conversation's reply answered with its first option but the exit. Played, it is on
        /// tuning 1, as every fresh season from B9; <paramref name="tuningHeld"/> holds it at tuning 0 by hand, which makes
        /// it the dialogue arm's season (B9-BALANCE-PLAN decision 8: a harness may hold one).
        /// </summary>
        internal static EpisodeState Rules2Season(string stage, bool tuningHeld)
        {
            if (rules2 == null) rules2 = DialogueRules2Season.FindCampaign();
            var state = rules2.State.Clone();
            if (stage != "campaign")
            {
                string voter = rules2.Voters[0];
                var engine = new EpisodeEngine(state);
                var entered = DialogueRules2Season.Accepted(engine, DialogueRules2Season.Entry(state, voter));
                state = entered;
                if (stage == "reply")
                {
                    var episode = DialogueRules2Season.Conversation(entered, voter);
                    var reply = entered.houseEvents.Single(e => e.id == DialogueRules2Season.Cycle(entered, episode).eventId);
                    string option = reply.choices.First(c => c.optionId != DialogueCampaignWord.Leave).optionId;
                    state = DialogueRules2Season.Accepted(engine, DialogueRules2Season.Choice(entered, option));
                }
                state = state.Clone();
            }
            Assert.That((state.dialogueRulesVersion, state.tuningRulesVersion), Is.EqualTo((2, TuningRules.Shipped)),
                "Played as it starts: rules 2 by the literal, tuning 1.");
            if (tuningHeld) state.tuningRulesVersion = TuningRules.Off;
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
            return state;
        }

        /// <summary>An All-Stars season enrolled on dialogue rules 2 by the literal (the chip offers rules 1 until rules 2 freezes).</summary>
        private static EpisodeState AllStarsOnRules2()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8, Roster = CastTemplates.Roster.AllStars }, 3008u);
            ShippedRules.ApplyFresh(state);
            Assert.That(state.dialogueRulesVersion, Is.Zero, "A shipped season starts on dialogue rules 0.");
            state.dialogueRulesVersion = 2;
            Assert.That((state.alumniRulesVersion, state.schemaVersion), Is.EqualTo((AlumniRules.Latest, 30)));
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
            return state;
        }

        /// <summary>
        /// A season on dialogue rules 1 and tuning 1, the chip's season from B9, by the literal 1: the shipped start, or the
        /// preview witness's open conversation with its tuning held at 0 moved to tuning 1 by hand.
        /// </summary>
        private static EpisodeState Rules1Tuned(string stage)
        {
            EpisodeState state;
            if (stage == "start")
            {
                state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 3009u);
                ShippedRules.ApplyFresh(state);
                Assert.That(state.dialogueRulesVersion, Is.Zero, "A shipped season starts on dialogue rules 0.");
                state.dialogueRulesVersion = 1;
            }
            else
            {
                state = DialoguePreviewWitness.Find().Conversation;
                Assert.That(state.tuningRulesVersion, Is.EqualTo(TuningRules.Off), "The witness holds tuning 0.");
                state.tuningRulesVersion = TuningRules.Shipped;
            }
            Assert.That((state.dialogueRulesVersion, state.tuningRulesVersion, state.alumniRulesVersion),
                Is.EqualTo((1, TuningRules.Shipped, AlumniRules.Off)));
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
            return state;
        }

        private static string Refusal(TestDelegate call) => Assert.Throws<InvalidDataException>(call).Message;

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
