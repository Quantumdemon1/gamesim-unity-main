using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Runtime serializer, storage-validation and migration companions to the pure observer.
    /// Witnesses use actual public Apply. Neutral26 projections are explicitly test-owned
    /// diagnostics from current27, not retained historical bytes or disk-save acceptance.
    /// These cases never create files or replace native SaveStore lifecycle coverage.
    /// </summary>
    public sealed class LegacyDigestSchema27NativeTests
    {
        [TestCase(0, "fresh")] [TestCase(1, "fresh")] [TestCase(2, "fresh")]
        [TestCase(0, "events")] [TestCase(1, "events")] [TestCase(2, "events")]
        public void ActualFieldSerializerExactlyMatchesTheSeparateTypedObserver(int mode, string kind)
        {
            var state = LegacyDigestSchema27TestStates.Witness(mode, kind);
            var field = ActualField(state);
            Assert.That(field.Property("Active"), Is.Null);
            foreach (JObject item in (JArray)field["houseEvents"])
                Assert.That(item.Property("IsStory"), Is.Null);
            var trace = LegacyDigestSchema27TestStates.Trace(state);
            Assert.That(trace["Active"], Is.InstanceOf<JArray>());
            if (kind == "events")
            {
                Assert.That(state.houseEvents.Count, Is.GreaterThan(0));
                Assert.That(trace["houseEvents"][0]["IsStory"].Type, Is.EqualTo(JTokenType.Boolean));
            }
        }

        [TestCase(1, "promise")] [TestCase(2, "promise")]
        [TestCase(1, "deal")] [TestCase(2, "deal")]
        [TestCase(1, "broken-promise")] [TestCase(2, "broken-promise")]
        [TestCase(1, "broken-deal")] [TestCase(2, "broken-deal")]
        public void ActualCanonicalOwnersRetainTheirSourcePolicyThroughNativeSerialization(int mode, string kind)
        {
            var state = LegacyDigestSchema27TestStates.Witness(mode, kind);
            var field = ActualField(state);
            var owner = LegacyDigestSchema27TestStates.Owner(state, mode, kind);
            var stored = (JObject)field["unifiedCommitments"].Single(row => (string)row["id"] == owner.id);
            Assert.That((string)stored["sourcePolicy"], Is.EqualTo(kind.Contains("promise")
                ? UnifiedCommitments.PromisePolicy : UnifiedCommitments.DealPolicy));
            Assert.That(stored["voteBindingWeek"].Type, Is.EqualTo(JTokenType.Integer));
            Assert.That(stored["voteFirstRevealWeek"].Type, Is.EqualTo(JTokenType.Integer));
            Assert.That((int)stored["voteBindingWeek"], Is.Zero);
            Assert.That((int)stored["voteFirstRevealWeek"], Is.Zero);
            Assert.That(stored["targetId"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That(stored["subtype"].Type, Is.EqualTo(JTokenType.Null));
            if (kind == "broken-deal" && mode == 2)
            {
                Assert.That(state.unifiedHearingEvidence.Any(e => e.fact.refId == owner.id), Is.True);
                Assert.That(state.unifiedHearingReceipts.Count(r => r.incidentKey == owner.settlementEffectKey
                    && r.kind == UnifiedCommitmentHearings.Initial), Is.EqualTo(1));
            }
        }

        [TestCase(0, "fresh")] [TestCase(1, "promise")] [TestCase(2, "broken-deal")]
        public void ActualNativeUpgradeHasTheExactNeutralProjectionInverse(int mode, string kind)
        {
            var state = LegacyDigestSchema27TestStates.Witness(mode, kind);
            var field27 = ActualField(state);
            var field26 = Neutral26AfterActualFixedValidation(field27);
            string stateBefore = LegacyDigestSchema27TestStates.Text(state), oldBefore = Text(field26), currentBefore = Text(field27);
            var upgraded = EpisodeSaveMigrations.UpgradeV26ToV27(field26);
            Assert.That(Text(upgraded), Is.EqualTo(currentBefore), "Actual migration preserves complete compact field/property/list order.");
            Shape(upgraded); ValidateHydrated(upgraded);
            var dispatched = EpisodeSaveMigrations.PrepareCurrentPayload(field26, out bool migrated);
            Assert.That(migrated, Is.True);
            Assert.That(Text(dispatched), Is.EqualTo(currentBefore));
            Assert.That(Text(field26), Is.EqualTo(oldBefore));
            Assert.That(Text(field27), Is.EqualTo(currentBefore));
            Assert.That(LegacyDigestSchema27TestStates.Text(state), Is.EqualTo(stateBefore));
            Assert.That(upgraded, Is.Not.SameAs(field26));
            Assert.That(upgraded["contestants"][0], Is.Not.SameAs(field26["contestants"][0]));
            upgraded["contestants"][0]["name"] = "Detached native upgrade";
            ((JArray)upgraded["unifiedCommitments"]).Clear();
            Assert.That(Text(field26), Is.EqualTo(oldBefore));
            Assert.That(Text(field27), Is.EqualTo(currentBefore));
            Assert.That(LegacyDigestSchema27TestStates.Text(state), Is.EqualTo(stateBefore));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ActualPublicTerminalSeasonRetainsNativeStorageAndHistoricalProof(int mode)
        {
            var state = LegacyDigestSchema27TestStates.Witness(mode, "finished");
            Assert.That(state.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(state.contestants.Count(c => c.status == ContestantStatus.Winner), Is.EqualTo(1));
            var field27 = ActualField(state);
            var field26 = Neutral26AfterActualFixedValidation(field27);
            Assert.That(Text(EpisodeSaveMigrations.UpgradeV26ToV27(field26)), Is.EqualTo(Text(field27)));
            Assert.That(Text(LegacyDigestSchema27Observer.ProjectTrace(state, LegacyDigestSchema27TestStates.Trace(state))),
                Is.EqualTo(Text(LegacyDigestSchema27TestStates.Neutral26(LegacyDigestSchema27TestStates.Trace(state)))));
        }

        [TestCase("binding-nonzero")] [TestCase("first-reveal-nonzero")]
        [TestCase("authority-two")] [TestCase("hearing-two")]
        [TestCase("knowledge-off")] [TestCase("missing-initial")]
        public void ActualStorageValidatorRejectsSemanticDefectsAfterAValidPublicSource(string defect)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "broken-deal");
            ActualField(state);
            var owner = LegacyDigestSchema27TestStates.Owner(state, 2, "broken-deal");
            switch (defect)
            {
                case "binding-nonzero": owner.voteBindingWeek = 1; break;
                case "first-reveal-nonzero": owner.voteFirstRevealWeek = 1; break;
                case "authority-two": state.unifiedCommitmentRulesVersion = 2; break;
                case "hearing-two": state.unifiedHearingRulesVersion = 2; break;
                case "knowledge-off": state.story.rulesStartWeek = 0; break;
                case "missing-initial":
                    Assert.That(state.unifiedHearingReceipts.RemoveAll(r => r.incidentKey == owner.settlementEffectKey
                        && r.kind == UnifiedCommitmentHearings.Initial), Is.EqualTo(1)); break;
                default: Assert.Fail("Unknown semantic control."); break;
            }
            string before = LegacyDigestSchema27TestStates.Text(state);
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.False, defect);
            Assert.That(reason, Is.Not.Empty);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveValidation.Validate(state));
            Assert.Throws<ArgumentException>(() => new EpisodeEngine(state));
            Assert.That(LegacyDigestSchema27TestStates.Text(state), Is.EqualTo(before));
        }

        [TestCase("voteBindingWeek", "missing")] [TestCase("voteFirstRevealWeek", "missing")]
        [TestCase("voteBindingWeek", "null")] [TestCase("voteFirstRevealWeek", "null")]
        [TestCase("voteBindingWeek", "string")] [TestCase("voteFirstRevealWeek", "string")]
        [TestCase("voteBindingWeek", "fractional")] [TestCase("voteFirstRevealWeek", "fractional")]
        public void ActualNativeShapeCannotHydrateMissingOrWrongMarkerTypes(string marker, string defect)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "promise");
            var field = ActualField(state);
            var row = (JObject)field["unifiedCommitments"][0];
            switch (defect)
            {
                case "missing": row.Remove(marker); break;
                case "null": row[marker] = JValue.CreateNull(); break;
                case "string": row[marker] = "0"; break;
                case "fractional": row[marker] = 0.5; break;
                default: Assert.Fail("Unknown marker type control."); break;
            }
            string before = Text(field), sourceBefore = LegacyDigestSchema27TestStates.Text(state);
            Assert.Throws<InvalidDataException>(() => Shape(field));
            Assert.That(Text(field), Is.EqualTo(before));
            Assert.That(LegacyDigestSchema27TestStates.Text(state), Is.EqualTo(sourceBefore));
        }

        [TestCase("binding-present-zero")] [TestCase("first-reveal-present-zero")]
        [TestCase("getter-root")] [TestCase("getter-canonical")]
        [TestCase("header27")] [TestCase("header28")]
        [TestCase("target-value")] [TestCase("subtype-value")]
        public void ActualFixed26AndUpgradeNeverEraseUnknownCurrentFieldsOrAuthorities(string defect)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "promise");
            var current = ActualField(state);
            var old = Neutral26AfterActualFixedValidation(current);
            var row = (JObject)old["unifiedCommitments"][0];
            switch (defect)
            {
                case "binding-present-zero": row.Add("voteBindingWeek", 0); break;
                case "first-reveal-present-zero": row.Add("voteFirstRevealWeek", 0); break;
                case "getter-root": old.Add("Active", LegacyDigestSchema27TestStates.Trace(state)["Active"].DeepClone()); break;
                case "getter-canonical": row.Add("IsStory", false); break;
                case "header27": old["schemaVersion"] = 27; break;
                case "header28": old["schemaVersion"] = 28; break;
                case "target-value": row["targetId"] = state.playerId; break;
                case "subtype-value": row["subtype"] = "vote_together"; break;
                default: Assert.Fail("Unknown historical control."); break;
            }
            string before = Text(old), currentBefore = Text(current), sourceBefore = LegacyDigestSchema27TestStates.Text(state);
            Assert.Throws<InvalidDataException>(() => Fixed26(old));
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV26ToV27(old));
            Assert.That(Text(old), Is.EqualTo(before));
            Assert.That(Text(current), Is.EqualTo(currentBefore));
            Assert.That(LegacyDigestSchema27TestStates.Text(state), Is.EqualTo(sourceBefore));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ActualCurrentDispatchClonesWithoutRetaggingHistoricalEvidence(int mode)
        {
            var state = LegacyDigestSchema27TestStates.Witness(mode, "fresh");
            var current = ActualField(state); string before = Text(current), sourceBefore = LegacyDigestSchema27TestStates.Text(state);
            var result = EpisodeSaveMigrations.PrepareCurrentPayload(current, out bool migrated);
            Assert.That(migrated, Is.False);
            Assert.That(Text(result), Is.EqualTo(before));
            Assert.That(result, Is.Not.SameAs(current));
            Shape(result); ValidateHydrated(result);
            result["contestants"][0]["name"] = "Detached current dispatch";
            Assert.That(Text(current), Is.EqualTo(before));
            Assert.That(LegacyDigestSchema27TestStates.Text(state), Is.EqualTo(sourceBefore));
        }

        private static JObject ActualField(EpisodeState state)
        {
            LegacyDigestSchema27TestStates.Valid(state);
            string before = LegacyDigestSchema27TestStates.Text(state);
            var trace = LegacyDigestSchema27TestStates.Trace(state);
            var field = JObject.FromObject(state, Serializer());
            Shape(field); EpisodeSaveValidation.Validate(state); ValidateHydrated(field);
            var observed = LegacyDigestSchema27Observer.CheckFieldObserver(state, trace);
            Assert.That(Text(field), Is.EqualTo(Text(observed)), "Actual SaveJson field order must be proved, never assumed or normalized.");
            Assert.That(Text(field), Is.EqualTo(Text(LegacyDigestSchema27TestStates.WithoutGetters(trace))));
            Assert.That(LegacyDigestSchema27TestStates.Text(state), Is.EqualTo(before));
            return field;
        }

        private static JObject Neutral26AfterActualFixedValidation(JObject current)
        {
            string before = Text(current);
            var old = LegacyDigestSchema27TestStates.Neutral26(current);
            Fixed26(old);
            Assert.That(Text(LegacyDigestSchema27TestStates.Restore27(old)), Is.EqualTo(before));
            Assert.That(Text(current), Is.EqualTo(before));
            return old;
        }

        private static void ValidateHydrated(JObject payload)
        {
            string before = Text(payload);
            var state = payload.ToObject<EpisodeState>(Serializer());
            EpisodeSaveValidation.Validate(state);
            Assert.That(Text(JObject.FromObject(state, Serializer())), Is.EqualTo(before));
            Assert.That(Text(payload), Is.EqualTo(before));
        }

        private static JsonSerializer Serializer() => (JsonSerializer)Invoke(SaveMethod("Serializer", Type.EmptyTypes), new object[0]);
        private static void Shape(JObject payload) => Invoke(SaveMethod("CheckDtoShape", new[] { typeof(JToken), typeof(Type), typeof(string) }),
            new object[] { payload, typeof(EpisodeState), "state(schema27-native-companion)" });

        private static MethodInfo SaveMethod(string name, Type[] arguments)
        {
            var type = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.SaveJson", true);
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, arguments, null);
            Assert.That(method, Is.Not.Null); Assert.That(method.DeclaringType, Is.EqualTo(type));
            return method;
        }

        private static void Fixed26(JObject payload)
        {
            var type = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.FrozenEpisodeV26", true);
            var method = type.GetMethod("Validate", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(JObject) }, null);
            Assert.That(method, Is.Not.Null); Assert.That(method.IsAssembly, Is.True);
            Assert.That(method.DeclaringType, Is.EqualTo(type));
            Invoke(method, new object[] { payload });
        }

        private static object Invoke(MethodInfo method, object[] arguments)
        {
            try { return method.Invoke(null, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        private static string Text(JToken token) => LegacyDigestSchema27TestStates.Text(token);
    }
}
