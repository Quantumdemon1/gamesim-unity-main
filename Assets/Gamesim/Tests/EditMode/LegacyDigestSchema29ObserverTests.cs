using System;
using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Current29 oracle admission, not a new historical writer or a replacement digest.
    /// These cases require the separately reviewed schema29 foundation before GREEN.
    /// </summary>
    public sealed class LegacyDigestSchema29ObserverTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void InertCurrentSourcesProjectOnlyTheThreeAdditionsWithoutMutation(int mode)
        {
            var state = Fresh(mode);
            var trace = JObject.FromObject(state);
            var fields = OldSaveTrace.Fields(state);
            string stateBefore = Text(state), traceBefore = Text(trace), fieldsBefore = Text(fields);
            var expectedTrace = Expected28(trace);
            var expectedFields = Expected28(fields);
            var projectedTrace = LegacyDigestSchema29Observer.ProjectTrace28(state, trace);
            var projectedFields = LegacyDigestSchema29Observer.ProjectFields28(state, fields);
            Assert.That(Text(projectedTrace), Is.EqualTo(Text(expectedTrace)), "Every retained number, property and list position stays.");
            Assert.That(Text(projectedFields), Is.EqualTo(Text(expectedFields)));
            Assert.That(JToken.DeepEquals(projectedTrace["Active"], trace["Active"]), Is.True);
            Assert.That(projectedFields.Property("Active"), Is.Null);
            Assert.That(projectedTrace["unifiedCommitmentRulesVersion"].Value<int>(), Is.EqualTo(mode));
            if (mode > 0) Assert.That(((JArray)projectedTrace["unifiedCommitments"]).Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(Text(Restore29(projectedTrace)), Is.EqualTo(traceBefore));
            Assert.That(Text(Restore29(projectedFields)), Is.EqualTo(fieldsBefore));
            projectedTrace["contestants"][0]["name"] = "Detached trace";
            projectedFields["contestants"][0]["name"] = "Detached fields";
            Assert.That(Text(state), Is.EqualTo(stateBefore));
            Assert.That(Text(trace), Is.EqualTo(traceBefore));
            Assert.That(Text(fields), Is.EqualTo(fieldsBefore));
        }

        [TestCase(0)] [TestCase(1)]
        public void Literal28SplitRetainsExactOldFieldAndLegacyTraceContracts(int mode)
        {
            var state = Fresh(mode);
            var source = JObject.FromObject(state);
            var trace28 = LegacyDigestSchema29Observer.ProjectTrace28(state, source);
            string before = Text(trace28), stateBefore = Text(state);
            var field28 = LegacyDigestSchema28Observer.CheckFields(trace28);
            Assert.That(Text(field28), Is.EqualTo(Text(Expected28(OldSaveTrace.Fields(state)))));
            var trace26 = LegacyDigestSchema28Observer.ProjectFields(trace28);
            var expected26 = (JObject)trace28.DeepClone();
            LegacyDigestSchema28Observer.Remove(expected26);
            foreach (JObject row in (JArray)expected26["unifiedCommitments"])
            { row.Remove("voteBindingWeek"); row.Remove("voteFirstRevealWeek"); }
            expected26["schemaVersion"] = 26;
            Assert.That(Text(trace26), Is.EqualTo(Text(expected26)));
            Assert.That(Text(LegacyDigestSchema29Observer.ProjectTrace(state, source)), Is.EqualTo(Text(trace26)));
            // The old source entry is not weakened into accepting an unbound historical JSON.
            var error = Assert.Throws<InvalidDataException>(() => LegacyDigestSchema28Observer.ProjectTrace(state, trace28));
            Assert.That(error.Message, Does.Contain("The trace is not the actual default JSON"));
            Assert.That(Text(trace28), Is.EqualTo(before));
            Assert.That(Text(state), Is.EqualTo(stateBefore));
        }

        [TestCase("start")] [TestCase("cadence")] [TestCase("plan")] [TestCase("order")]
        [TestCase("unknown")] [TestCase("getter")] [TestCase("chronology")]
        public void Literal28SplitPreservesTheSourceEntrysEarlierRefusals(string defect)
        {
            var state = Fresh(1);
            var trace = LegacyDigestSchema29Observer.ProjectTrace28(state, JObject.FromObject(state));
            Assert.DoesNotThrow(() => LegacyDigestSchema28Observer.CheckFields(trace));
            switch (defect)
            {
                case "start": trace["allWeekRulesStartWeek"] = 1; break;
                case "cadence": trace["npcSocial"]["beatWindow"] = 0; break;
                case "plan": trace["ledger"]["plans"] = new JArray(new JObject()); break;
                case "order":
                    var moved = trace.Property("allWeekRulesStartWeek"); moved.Remove(); trace.AddFirst(moved); break;
                case "unknown": trace["unknown28"] = 0; break;
                case "getter": trace["Active"] = new JArray(); break;
                default: trace["unifiedCommitments"][1]["voteBindingWeek"] = 1; break;
            }
            string stateBefore = Text(state), traceBefore = Text(trace);
            var bound = Assert.Throws<InvalidDataException>(() => LegacyDigestSchema28Observer.CheckFieldObserver(state, trace));
            var fields = Assert.Throws<InvalidDataException>(() => LegacyDigestSchema28Observer.CheckFields(trace));
            var projected = Assert.Throws<InvalidDataException>(() => LegacyDigestSchema28Observer.ProjectFields(trace));
            Assert.That(fields.Message, Is.EqualTo(bound.Message));
            Assert.That(projected.Message, Is.EqualTo(bound.Message));
            Assert.That(bound.Message, Does.Not.Contain("not the actual default JSON"), "The original field guard must refuse before source binding.");
            Assert.That(Text(state), Is.EqualTo(stateBefore));
            Assert.That(Text(trace), Is.EqualTo(traceBefore));
        }

        [TestCase("rule")] [TestCase("dialogue")] [TestCase("episodes")] [TestCase("grants")]
        [TestCase("vetoOccasions")] [TestCase("first-term")] [TestCase("last-term")]
        public void EveryRequiredInertAdditionRefusesMissingWrongTypedOrNonInertJson(string owner)
        {
            var state = Fresh(2);
            foreach (string defect in new[] { "missing", "null", "wrong-type", "non-inert" })
            {
                var trace = JObject.FromObject(state);
                Assert.DoesNotThrow(() => LegacyDigestSchema29Observer.ProjectTrace28(state, trace));
                JObject holder = trace;
                string name = owner == "rule" ? "dialogueRulesVersion" : owner;
                if (owner == "episodes" || owner == "grants" || owner == "vetoOccasions") holder = (JObject)trace["dialogue"];
                if (owner.EndsWith("-term", StringComparison.Ordinal))
                { holder = (JObject)trace["unifiedCommitments"][owner == "first-term" ? 0 : ((JArray)trace["unifiedCommitments"]).Count - 1]; name = "conditionalVeto"; }
                if (defect == "missing") holder.Remove(name);
                else if (defect == "null") holder[name] = JValue.CreateNull();
                else if (defect == "wrong-type") holder[name] = "0";
                else holder[name] = owner == "rule" ? (JToken)1 : new JObject();
                if (owner.EndsWith("-term", StringComparison.Ordinal) && defect == "null") continue; // The required literal.
                Refused(state, trace);
            }
        }

        [Test]
        public void EnabledAndPopulatedActualSourcesAreNeverErasedForParity()
        {
            var valid = Fresh(2);
            var rule = typeof(EpisodeState).GetField("dialogueRulesVersion");
            Assert.That(rule, Is.Not.Null);
            var enabled = valid.Clone(); rule.SetValue(enabled, 1);
            string enabledBefore = Text(enabled);
            Valid(enabled); // R2 admits an enabled rule with genuinely empty dialogue.
            var enabledTrace = JObject.FromObject(enabled);
            var enabledFields = OldSaveTrace.Fields(enabled);
            string enabledTraceBefore = Text(enabledTrace), enabledFieldsBefore = Text(enabledFields);
            var traceError = Assert.Throws<InvalidDataException>(() => LegacyDigestSchema29Observer.ProjectTrace28(enabled, enabledTrace));
            var fieldsError = Assert.Throws<InvalidDataException>(() => LegacyDigestSchema29Observer.ProjectFields28(enabled, enabledFields));
            Assert.That(traceError.Message, Is.EqualTo("Only present integer-zero dialogue rules may be projected."));
            Assert.That(fieldsError.Message, Is.EqualTo(traceError.Message));
            Assert.That(Text(enabled), Is.EqualTo(enabledBefore));
            Assert.That(Text(enabledTrace), Is.EqualTo(enabledTraceBefore));
            Assert.That(Text(enabledFields), Is.EqualTo(enabledFieldsBefore));
            Valid(enabled);
            foreach (string name in new[] { "episodes", "grants", "vetoOccasions" })
            {
                var populated = valid.Clone();
                var dialogue = typeof(EpisodeState).GetField("dialogue").GetValue(populated);
                var field = dialogue.GetType().GetField(name);
                var list = (IList)field.GetValue(dialogue);
                list.Add(Activator.CreateInstance(field.FieldType.GetGenericArguments()[0]));
                RefusedInvalidSource(populated);
            }
            var terms = valid.Clone();
            var conditional = typeof(UnifiedCommitmentState).GetField("conditionalVeto");
            Assert.That(conditional, Is.Not.Null);
            conditional.SetValue(terms.unifiedCommitments.Last(), Activator.CreateInstance(conditional.FieldType));
            RefusedInvalidSource(terms);
        }

        [TestCase("session")] [TestCase("rng")] [TestCase("unknown-root")]
        [TestCase("unknown-dialogue")] [TestCase("unknown-row")] [TestCase("order")] [TestCase("future")]
        public void AValidSourceCannotAuthorizeForeignUnknownReorderedOrFutureJson(string defect)
        {
            var state = Fresh(2);
            var other = state.Clone();
            if (defect == "session") other.sessionId += "-another";
            if (defect == "rng") other.randomState = unchecked(other.randomState + 1u);
            Valid(other);
            var trace = JObject.FromObject(other);
            switch (defect)
            {
                case "unknown-root": trace["unknown29"] = 0; break;
                case "unknown-dialogue": trace["dialogue"]["unknown29"] = new JArray(); break;
                case "unknown-row": trace["unifiedCommitments"][1]["unknown29"] = JValue.CreateNull(); break;
                case "order":
                    var moved = trace.Property("dialogue"); moved.Remove(); trace.AddFirst(moved); break;
                case "future": trace["schemaVersion"] = 30; break;
            }
            Refused(state, trace);
        }

        [Test]
        public void CompleteCurrentValidationRunsBeforeOtherwiseInertProjection()
        {
            var state = Fresh(2);
            state.contestants[1].id = state.contestants[0].id;
            RefusedInvalidSource(state);
        }

        [TestCase(28)] [TestCase(27)] [TestCase(26)] [TestCase(29)]
        public void CurrentOldSaveCheckpointUsesTheGuardedBoundaryAndRetainedHashAlgorithm(int target)
        {
            var state = Fresh(1);
            string before = Text(state);
            var own = OldSaveTrace.Fields(state);
            var expected = target == 29 ? own : OldSaveTrace.Project(Expected28(own), target);
            var season = (JObject)expected.DeepClone(); season.Remove("events");
            var actual = CurrentOldSaveTrace.Checkpoint(state, target);
            Assert.That(actual.season, Is.EqualTo(OldSaveTrace.Sha256(OldSaveTrace.Canonical(season, true))));
            Assert.That(actual.blind, Is.EqualTo(OldSaveTrace.Sha256(OldSaveTrace.Canonical(expected, true))));
            Assert.That(actual.text, Is.EqualTo(OldSaveTrace.Sha256(OldSaveTrace.Canonical(expected, false))));
            Assert.That(Text(state), Is.EqualTo(before));
            // An old utility's early same-header return is not the current admission boundary.
            var invalid = state.Clone(); invalid.contestants[1].id = invalid.contestants[0].id;
            Assert.Throws<InvalidDataException>(() => CurrentOldSaveTrace.Checkpoint(invalid, target));
        }


        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void CurrentFindDecisionAnswersRetainTheActualEmptyNullOrRefusalBoundaryInEveryMode(int mode)
        {
            var state = Fresh(mode);
            string before = Text(state);
            if (mode == 1)
            {
                Assert.That(state.unifiedCommitments, Is.Not.Empty, "Mode1 really reaches the actual reader's storage refusal.");
                var original = Assert.Catch(() => UnifiedVoteHistory.FindDecision(state, state.unifiedCommitments[0].id));
                var refused = Assert.Catch(() => LegacyDigestSchema29Observer.FindDecisionAnswers28(state));
                Assert.That(refused.GetType(), Is.EqualTo(original.GetType()));
                Assert.That(refused.Message, Is.EqualTo(original.Message), "The original reader exception is neither swallowed nor rewritten.");
                Assert.That(Text(state), Is.EqualTo(before));
                return;
            }
            var expected = new JArray(state.unifiedCommitments.Select(row => {
                var decision = UnifiedVoteHistory.FindDecision(state, row.id);
                Assert.That(decision, Is.Null, "This actual fresh witness has no completed Vote decision.");
                return JValue.CreateNull();
            }));
            var actual = LegacyDigestSchema29Observer.FindDecisionAnswers28(state);
            Assert.That(Text(actual), Is.EqualTo(Text(expected)));
            Assert.That(actual.Count, Is.EqualTo(state.unifiedCommitments.Count));
            actual.Add("caller-only");
            Assert.That(Text(state), Is.EqualTo(before));
        }

        [Test]
        public void CurrentFindDecisionAnswersRemoveOnlyTheOwnedNullTermAfterActualPublicSeasonPlay()
        {
            var state = PlayedDecisionWitness();
            string before = Text(state);
            var actual = LegacyDigestSchema29Observer.FindDecisionAnswers28(state);
            Assert.That(actual.Count, Is.EqualTo(state.unifiedCommitments.Count));
            int decisions = 0, nullSlots = 0;
            for (int index = 0; index < state.unifiedCommitments.Count; index++)
            {
                var canonical = state.unifiedCommitments[index];
                var decision = UnifiedVoteHistory.FindDecision(state, canonical.id);
                if (decision == null)
                {
                    nullSlots++; Assert.That(actual[index].Type, Is.EqualTo(JTokenType.Null)); continue;
                }
                decisions++;
                var raw = OldSaveTrace.Fields(decision);
                var expected = (JObject)raw.DeepClone();
                Assert.That(raw["Record"]["conditionalVeto"].Type, Is.EqualTo(JTokenType.Null));
                ((JObject)expected["Record"]).Remove("conditionalVeto");
                Assert.That(Text(actual[index]), Is.EqualTo(Text(expected)),
                    "All other actual answer fields, nested arrays, numeric values and order stay exact.");
                var restored = (JObject)actual[index].DeepClone();
                ((JObject)restored["Record"]).Add("conditionalVeto", JValue.CreateNull());
                Assert.That(Text(restored), Is.EqualTo(Text(raw)), "The actual appended DTO field has its exact inverse.");
                Assert.That(Text(raw["Reveal"]), Is.EqualTo(Text(OldSaveTrace.Fields(decision.Reveal))));
                ((JObject)actual[index]["Record"])["id"] = "detached-answer";
                actual[index]["Reveal"]["week"] = -99;
            }
            Assert.That(decisions, Is.GreaterThan(0), "A real completed decision is required, never an empty positive.");
            Assert.That(nullSlots, Is.GreaterThan(0), "Null Safety/nonterminal slots are retained, not filtered out.");
            Assert.That(Text(state), Is.EqualTo(before));
            Assert.That(Text(LegacyDigestSchema29Observer.FindDecisionAnswers28(state)),
                Is.EqualTo(Text(ExpectedFindDecisionAnswers(state))));
        }

        /// <summary>
        /// Every invalid source is refused, whole and unchanged, before any answer is projected. Since dialogue D1 a
        /// valid source on dialogue rules 1 is no longer among the refusals: the native arm admits it (the test below).
        /// What stays refused on rules 1 is what the pilot never holds - an unknown rules version, a role grant, a veto
        /// occasion, a conditional term - and each of those is an invalid save, as on rules 0.
        /// </summary>
        [TestCase("schema")] [TestCase("duplicate-cast")] [TestCase("enabled-dialogue-unknown-version")]
        [TestCase("episodes")] [TestCase("grants")] [TestCase("vetoOccasions")]
        [TestCase("enabled-dialogue-grants")] [TestCase("enabled-dialogue-vetoOccasions")] [TestCase("enabled-dialogue-conditional-term")]
        [TestCase("conditional-term")] [TestCase("archive")]
        public void CurrentFindDecisionAnswersRefuseUnsupportedInvalidOrNonInertActualSourcesWithoutMutation(string defect)
        {
            var control = PlayedDecisionWitness();
            string controlBefore = Text(control);
            var state = control.Clone();
            Assert.That(LegacyDigestSchema29Observer.FindDecisionAnswers28(state)
                .OfType<JObject>().Count(), Is.GreaterThan(0), "The control really exercises completed decision projection.");
            string owner = defect;
            if (defect.StartsWith("enabled-dialogue-", StringComparison.Ordinal))
            {
                state.dialogueRulesVersion = 1;
                owner = defect.Substring("enabled-dialogue-".Length);
            }
            switch (owner)
            {
                case "schema": state.schemaVersion = 28; break;
                case "duplicate-cast": state.contestants[1].id = state.contestants[0].id; break;
                case "unknown-version": state.dialogueRulesVersion = 2; break;
                case "conditional-term": state.unifiedCommitments.Last().conditionalVeto = new ConditionalVetoTermsState(); break;
                case "archive": state.unifiedVoteReveals[0].ballots[0].targetId = "not-a-cast-id"; break;
                default:
                    var field = typeof(DialogueState).GetField(owner);
                    var list = (IList)field.GetValue(state.dialogue);
                    list.Add(Activator.CreateInstance(field.FieldType.GetGenericArguments()[0]));
                    break;
            }
            string before = Text(state);
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False, "Every original defect, on either rules, remains invalid.");
            Assert.Throws<InvalidDataException>(() => LegacyDigestSchema29Observer.FindDecisionAnswers28(state));
            Assert.That(Text(state), Is.EqualTo(before));
            Assert.That(Text(control), Is.EqualTo(controlBefore));
            Valid(control);
            Assert.That(Text(LegacyDigestSchema29Observer.FindDecisionAnswers28(control)),
                Is.EqualTo(Text(ExpectedFindDecisionAnswers(control))), "The unchanged valid inert control still projects its actual answers.");
            Assert.That(Text(control), Is.EqualTo(controlBefore));
            Valid(control);
        }

        /// <summary>
        /// The pin that replaced the old refusal (DIALOGUE-PLAN D1): the same played season with its dialogue rules
        /// set to 1 is a valid source - the pilot holds every conditional term null - and the native arm admits it,
        /// so the reader answers it, with exactly the decisions it answers on rules 0, nothing mutated. The rules-0
        /// projections themselves still refuse it, word for word as before.
        /// </summary>
        [Test]
        public void CurrentFindDecisionAnswersAdmitAValidRules1SourceNativelyWithItsActualAnswers()
        {
            var control = PlayedDecisionWitness();
            var state = control.Clone();
            state.dialogueRulesVersion = 1;
            Valid(state);
            string before = Text(state);
            var answers = LegacyDigestSchema29Observer.FindDecisionAnswers28(state);
            Assert.That(answers.OfType<JObject>().Count(), Is.GreaterThan(0), "Real completed decisions are answered.");
            Assert.That(Text(answers), Is.EqualTo(Text(ExpectedFindDecisionAnswers(state))));
            Assert.That(Text(answers), Is.EqualTo(Text(LegacyDigestSchema29Observer.FindDecisionAnswers28(control))),
                "The dialogue rules move no decision.");
            Assert.That(Text(state), Is.EqualTo(before));
            var error = Assert.Throws<InvalidDataException>(() => LegacyDigestSchema29Observer.ProjectFields28(state, OldSaveTrace.Fields(state)));
            Assert.That(error.Message, Is.EqualTo("Only present integer-zero dialogue rules may be projected."));
            Assert.That(Text(state), Is.EqualTo(before));
        }

        // ------------------------------------------------------------------ the native-29 arm (dialogue D1)

        /// <summary>
        /// A season on the preview, before its conversation (rules 1, the empty store) and with it open (one
        /// campaign episode, its storyline and its house events): each is admitted at 29 as it is - the trace with
        /// its getters, the fields without - returned detached, never projected, and checkpointed at 29 with the
        /// retained hash algorithm. It has no schema-28 form, so a checkpoint below 29 and both rules-0
        /// projections refuse it.
        /// </summary>
        [TestCase("campaign")] [TestCase("conversation")]
        public void ARules1SourceIsAdmittedNativelyAtItsOwnSchemaAndNeverProjected(string which)
        {
            var witness = DialoguePreviewWitness.Find();
            var state = which == "campaign" ? witness.Campaign : witness.Conversation;
            Valid(state);
            Assert.That(state.dialogueRulesVersion, Is.EqualTo(1));
            Assert.That(state.dialogue.episodes.Count, Is.EqualTo(which == "campaign" ? 0 : 1));
            string stateBefore = Text(state);
            var trace = JObject.FromObject(state);
            var fields = OldSaveTrace.Fields(state);
            string traceBefore = Text(trace), fieldsBefore = Text(fields);
            var nativeTrace = LegacyDigestSchema29Observer.NativeTrace29(state, trace);
            var nativeFields = LegacyDigestSchema29Observer.NativeFields29(state, fields);
            Assert.That(Text(nativeTrace), Is.EqualTo(traceBefore), "The trace, getters and all, as it is.");
            Assert.That(Text(nativeFields), Is.EqualTo(fieldsBefore), "The field view as it is.");
            Assert.That(nativeTrace, Is.Not.SameAs(trace));
            Assert.That(nativeFields, Is.Not.SameAs(fields));
            nativeTrace["contestants"][0]["name"] = "Detached trace";
            nativeFields["contestants"][0]["name"] = "Detached fields";
            Assert.That(Text(trace), Is.EqualTo(traceBefore));
            Assert.That(Text(fields), Is.EqualTo(fieldsBefore));
            Assert.That(Text(state), Is.EqualTo(stateBefore));

            var season = (JObject)fields.DeepClone();
            season.Remove("events");
            var actual = CurrentOldSaveTrace.Checkpoint(state, 29);
            Assert.That(actual.season, Is.EqualTo(OldSaveTrace.Sha256(OldSaveTrace.Canonical(season, true))));
            Assert.That(actual.blind, Is.EqualTo(OldSaveTrace.Sha256(OldSaveTrace.Canonical(fields, true))));
            Assert.That(actual.text, Is.EqualTo(OldSaveTrace.Sha256(OldSaveTrace.Canonical(fields, false))));
            Assert.That(actual, Is.EqualTo(OldSaveTrace.Checkpoint(state, 29)), "The recorder hashes the same state the same way.");
            foreach (int older in new[] { 28, 26 })
                Assert.Throws<InvalidDataException>(() => CurrentOldSaveTrace.Checkpoint(state, older), "No schema-" + older + " form.");
            var traceError = Assert.Throws<InvalidDataException>(() => LegacyDigestSchema29Observer.ProjectTrace28(state, JObject.FromObject(state)));
            var fieldsError = Assert.Throws<InvalidDataException>(() => LegacyDigestSchema29Observer.ProjectFields28(state, OldSaveTrace.Fields(state)));
            Assert.That(traceError.Message, Is.EqualTo("Only present integer-zero dialogue rules may be projected."));
            Assert.That(fieldsError.Message, Is.EqualTo(traceError.Message));
            Assert.That(Text(state), Is.EqualTo(stateBefore));
        }

        /// <summary>
        /// The native arm takes nothing but a valid rules-1 source and its own JSON: not a rules-0 state (the
        /// projection is its arm), not an invalid one - an unknown version, a grant, a veto occasion, a conditional
        /// term - and not a JSON that is another state's, carries a foreign or a reordered key, a future header, or
        /// the other view's getters. Each refusal leaves the state and the JSON as they were.
        /// </summary>
        [TestCase("rules-0")] [TestCase("unknown-version")] [TestCase("grants")] [TestCase("vetoOccasions")]
        [TestCase("conditional-term")] [TestCase("foreign")] [TestCase("unknown-dialogue")] [TestCase("order")]
        [TestCase("future")] [TestCase("getters")]
        public void TheNativeArmAdmitsNothingButAValidRules1SourceAndItsOwnJson(string defect)
        {
            var source = DialoguePreviewWitness.Find().Conversation;
            switch (defect)
            {
                case "rules-0": source = Fresh(2); break;
                case "unknown-version": source.dialogueRulesVersion = 2; break;
                case "conditional-term":
                    // A conditional term needs a canonical row to sit on: the played season's, on rules 1.
                    source = PlayedDecisionWitness();
                    source.dialogueRulesVersion = 1;
                    Valid(source);
                    source.unifiedCommitments.Last().conditionalVeto = new ConditionalVetoTermsState();
                    break;
                case "grants":
                case "vetoOccasions":
                    var field = typeof(DialogueState).GetField(defect);
                    var list = (IList)field.GetValue(source.dialogue);
                    list.Add(Activator.CreateInstance(field.FieldType.GetGenericArguments()[0]));
                    break;
            }
            bool invalid = defect == "unknown-version" || defect == "grants" || defect == "vetoOccasions" || defect == "conditional-term";
            Assert.That(EpisodeValidation.TryValidate(source, out _), Is.EqualTo(!invalid), "Only the four store defects are invalid saves.");
            var view = defect == "foreign" ? Foreign(source) : source;
            var trace = JObject.FromObject(view);
            var fields = OldSaveTrace.Fields(view);
            switch (defect)
            {
                case "unknown-dialogue":
                    trace["dialogue"]["unknown29"] = new JArray();
                    fields["dialogue"]["unknown29"] = new JArray();
                    break;
                case "order":
                    foreach (var json in new[] { trace, fields })
                    {
                        var moved = json.Property("dialogue");
                        moved.Remove();
                        json.AddFirst(moved);
                    }
                    break;
                case "future":
                    trace["schemaVersion"] = 30;
                    fields["schemaVersion"] = 30;
                    break;
                case "getters":
                    var swap = trace;
                    trace = fields;
                    fields = swap;
                    break;
            }
            string stateBefore = Text(source), traceBefore = Text(trace), fieldsBefore = Text(fields);
            // Each refused for its own reason, by the check that owns it, not by a later one that happens to hold too.
            string reason = invalid ? "The current source state is invalid:"
                : defect == "rules-0" ? "Only present integer-one dialogue rules are admitted natively."
                : defect == "future" ? "Only literal schema29 is a native input."
                : defect == "unknown-dialogue" ? "Only the pilot's ordered dialogue lists, with no grant and no veto occasion, are admitted natively."
                : "The JSON is not this current state's exact own view.";
            foreach (var native in new System.Func<JObject>[] {
                () => LegacyDigestSchema29Observer.NativeTrace29(source, trace),
                () => LegacyDigestSchema29Observer.NativeFields29(source, fields) })
                Assert.That(Assert.Throws<InvalidDataException>(() => native()).Message, Does.StartWith(reason), defect);
            Assert.That(Text(source), Is.EqualTo(stateBefore));
            Assert.That(Text(trace), Is.EqualTo(traceBefore));
            Assert.That(Text(fields), Is.EqualTo(fieldsBefore));
            if (defect == "rules-0") Assert.DoesNotThrow(() => CurrentOldSaveTrace.Checkpoint(source, 29), "Rules 0 keeps its own arm.");
            if (invalid) Assert.Throws<InvalidDataException>(() => CurrentOldSaveTrace.Checkpoint(source, 29));
            Assert.That(Text(source), Is.EqualTo(stateBefore));
        }

        /// <summary>A valid twin of a state that is not it: the same season in another session.</summary>
        private static EpisodeState Foreign(EpisodeState state)
        {
            var other = state.Clone();
            other.sessionId += "-another";
            Valid(other);
            return other;
        }

        [Test]
        public void CurrentFindDecisionAnswerEntryCannotAcceptCallerSuppliedResultsOrArbitraryJson()
        {
            var entries = typeof(LegacyDigestSchema29Observer).GetMethods(
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Where(method => method.Name == nameof(LegacyDigestSchema29Observer.FindDecisionAnswers28)).ToArray();
            Assert.That(entries, Has.Length.EqualTo(1));
            Assert.That(entries[0].ReturnType, Is.EqualTo(typeof(JArray)));
            Assert.That(entries[0].GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(EpisodeState) }),
                "The fixed reader owns its invocation, IDs, Records and Reveals. No object/JToken result projector.");
            Assert.Throws<InvalidDataException>(() => LegacyDigestSchema29Observer.FindDecisionAnswers28(null));
        }

        private static EpisodeState PlayedDecisionWitness()
        {
            var state = UnifiedVotePublicCommandTests.Play(6u, 8).Final.Clone();
            Valid(state);
            Assert.That(state.schemaVersion, Is.EqualTo(29));
            Assert.That(state.unifiedCommitmentRulesVersion, Is.EqualTo(2));
            Assert.That(state.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(UnifiedVoteHistory.Decisions(state).Count, Is.GreaterThan(0));
            return state;
        }

        private static JArray ExpectedFindDecisionAnswers(EpisodeState state) =>
            new JArray(state.unifiedCommitments.Select(row => {
                var decision = UnifiedVoteHistory.FindDecision(state, row.id);
                if (decision == null) return (JToken)JValue.CreateNull();
                var raw = OldSaveTrace.Fields(decision);
                ((JObject)raw["Record"]).Remove("conditionalVeto");
                return raw;
            }));

        private static EpisodeState Fresh(int mode)
        {
            var state = ContentCatalog.Create(2939u);
            Assert.That(state.schemaVersion, Is.EqualTo(29), "Adopt the reviewed foundation first; do not relabel a schema28 object.");
            if (mode == 2) ShippedRules.ApplyFresh(state);
            else if (mode == 1)
            {
                state.competitionRulesVersion = CompetitionRules.Current;
                state.haveNotRulesStartWeek = state.strategyRulesStartWeek = 1;
                EpisodeEngine.EnableStory(state); EpisodeEngine.EnableRead(state); EpisodeEngine.EnableLevers(state);
                EpisodeEngine.EnableWeek(state); EpisodeEngine.EnableAgency(state); EpisodeEngine.EnableFinale(state);
                EpisodeEngine.EnableCommitments(state); EpisodeEngine.EnableEconomy(state);
                state.unifiedCommitmentRulesVersion = 1;
            }
            Valid(state);
            if (mode > 0)
            {
                var engine = new EpisodeEngine(state);
                var targets = state.Active.Where(c => !c.isPlayer).Take(2).Select(c => c.id).ToArray();
                foreach (string target in targets)
                {
                    var snapshot = engine.Snapshot;
                    var result = engine.Apply(new EpisodeCommand {
                        id = "current29-observer-promise-" + snapshot.revision, actorId = snapshot.playerId,
                        expectedRevision = snapshot.revision, expectedPhase = snapshot.phase,
                        kind = EpisodeCommandKind.PromiseSafety, targetId = target });
                    Assert.That(result.accepted, Is.True, result.reason);
                }
                state = engine.Snapshot;
                Assert.That(state.unifiedCommitments.Count(row => row.origin == UnifiedCommitments.PlayerPromise), Is.EqualTo(2));
            }
            Assert.That(state.unifiedCommitmentRulesVersion, Is.EqualTo(mode));
            Valid(state);
            return state;
        }

        private static JObject Expected28(JObject source)
        {
            var result = (JObject)source.DeepClone();
            result.Remove("dialogueRulesVersion"); result.Remove("dialogue");
            foreach (JObject row in (JArray)result["unifiedCommitments"]) row.Remove("conditionalVeto");
            result["schemaVersion"] = 28;
            return result;
        }

        private static JObject Restore29(JObject source)
        {
            var result = (JObject)source.DeepClone(); result["schemaVersion"] = 29;
            var active = result.Property("Active");
            var rule = new JProperty("dialogueRulesVersion", 0);
            var dialogue = new JProperty("dialogue", new JObject {
                ["episodes"] = new JArray(), ["grants"] = new JArray(), ["vetoOccasions"] = new JArray() });
            if (active == null) { result.Add(rule); result.Add(dialogue); }
            else { active.AddBeforeSelf(rule); active.AddBeforeSelf(dialogue); }
            foreach (JObject row in (JArray)result["unifiedCommitments"]) row.Add("conditionalVeto", JValue.CreateNull());
            return result;
        }

        private static void RefusedInvalidSource(EpisodeState state)
        {
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False);
            Refused(state, JObject.FromObject(state), "The current source state is invalid:");
        }

        private static void Refused(EpisodeState state, JObject trace, string reason = null)
        {
            string stateBefore = Text(state), traceBefore = Text(trace);
            var error = Assert.Throws<InvalidDataException>(() => LegacyDigestSchema29Observer.ProjectTrace28(state, trace));
            if (reason != null) Assert.That(error.Message, Does.Contain(reason));
            var fields = (JObject)trace.DeepClone(); fields.Remove("Active");
            foreach (JObject row in (JArray)fields["houseEvents"]) row.Remove("IsStory");
            string fieldsBefore = Text(fields);
            Assert.Throws<InvalidDataException>(() => LegacyDigestSchema29Observer.ProjectFields28(state, fields));
            Assert.That(Text(fields), Is.EqualTo(fieldsBefore));
            Assert.That(Text(state), Is.EqualTo(stateBefore));
            Assert.That(Text(trace), Is.EqualTo(traceBefore));
        }

        private static void Valid(EpisodeState state) =>
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
        private static string Text(EpisodeState state) => Text(JObject.FromObject(state));
        private static string Text(JToken token) => token.ToString(Formatting.None);
    }
}
