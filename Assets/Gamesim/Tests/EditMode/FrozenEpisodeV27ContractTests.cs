using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The independent fixed schema-27 contract (FrozenEpisodeV27, W28): literal 27 is fixed26 plus a
    /// present integer-zero pair of chronology markers on every canonical row, and never carries any of
    /// schema 28's Wave D storage. The repository holds no generator for a retained 27 corpus, so the
    /// accepted payloads here are representative: actual lawful public witnesses' field views, taken to
    /// literal 27 by the test-owned inverse of schema 28's inert additions. The retained 26 corpus
    /// reaches 27 through the real step in PersistenceV28MigrationTests. Every control is a detached
    /// copy after a proved baseline, and no input is ever mutated. Pure JSON and reflection, so the
    /// Unity-free subset runs it as well as the editor.
    /// </summary>
    public sealed class FrozenEpisodeV27ContractTests
    {
        private static readonly string[] Root28 = { "allianceLeakRulesStartWeek", "pactPlanRulesStartWeek", "allWeekRulesStartWeek" };
        private static readonly string[] NpcSocial28 = { "beatWeek", "beatWindow", "beatsFired", "beatSeats", "beatPlan", "acts" };

        [TestCase(0, "fresh")] [TestCase(1, "fresh")] [TestCase(2, "fresh")]
        [TestCase(1, "promise")] [TestCase(2, "promise")] [TestCase(1, "deal")] [TestCase(2, "deal")]
        [TestCase(1, "broken-promise")] [TestCase(2, "broken-promise")] [TestCase(1, "broken-deal")] [TestCase(2, "broken-deal")]
        [TestCase(2, "events")] [TestCase(0, "finished")] [TestCase(1, "finished")] [TestCase(2, "finished")]
        public void RepresentativeLiteral27PayloadsAreAccepted(int mode, string kind)
        {
            var payload = Literal27(mode, kind);
            Accept(payload);
            Assert.That(payload["schemaVersion"].Type, Is.EqualTo(JTokenType.Integer));
            Assert.That((int)payload["schemaVersion"], Is.EqualTo(27));
            foreach (string name in Root28) Assert.That(payload.Property(name), Is.Null);
            // The contract is fixed26 plus the markers: their literal-26 projection is fixed26's, and
            // fixed26 refuses the literal 27 itself.
            var former26 = LegacyDigestSchema27TestStates.Neutral26(payload);
            Assert.DoesNotThrow(() => Contract("FrozenEpisodeV26", former26));
            string before = Text(payload);
            Assert.Throws<InvalidDataException>(() => Contract("FrozenEpisodeV26", payload));
            Assert.That(Text(payload), Is.EqualTo(before));
        }

        [TestCase("missing")] [TestCase("null")] [TestCase("text")] [TestCase("fraction")] [TestCase("boolean")]
        [TestCase("former")] [TestCase("current")] [TestCase("negative")]
        public void TheHeaderMustBeLiteralInteger27(string defect)
        {
            var payload = Literal27(2, "promise"); Accept(payload);
            switch (defect)
            {
                case "missing": payload.Remove("schemaVersion"); break;
                case "null": payload["schemaVersion"] = JValue.CreateNull(); break;
                case "text": payload["schemaVersion"] = "27"; break;
                case "fraction": payload["schemaVersion"] = 27.0; break;
                case "boolean": payload["schemaVersion"] = true; break;
                case "former": payload["schemaVersion"] = 26; break;
                case "current": payload["schemaVersion"] = 28; break;
                default: payload["schemaVersion"] = -27; break;
            }
            Refused(payload, "The historical schema27 header must be literal integer27.");
        }

        [TestCase("voteBindingWeek", "missing")] [TestCase("voteBindingWeek", "null")] [TestCase("voteBindingWeek", "text")]
        [TestCase("voteBindingWeek", "fraction")] [TestCase("voteBindingWeek", "boolean")] [TestCase("voteBindingWeek", "one")]
        [TestCase("voteBindingWeek", "negative")] [TestCase("voteBindingWeek", "huge")]
        [TestCase("voteFirstRevealWeek", "missing")] [TestCase("voteFirstRevealWeek", "null")] [TestCase("voteFirstRevealWeek", "text")]
        [TestCase("voteFirstRevealWeek", "fraction")] [TestCase("voteFirstRevealWeek", "boolean")] [TestCase("voteFirstRevealWeek", "one")]
        [TestCase("voteFirstRevealWeek", "negative")] [TestCase("voteFirstRevealWeek", "huge")]
        public void EveryCanonicalRowCarriesBothIntegerZeroMarkers(string marker, string defect)
        {
            foreach (bool last in new[] { false, true })
            {
                var payload = Literal27(2, "broken-deal"); Accept(payload);
                var rows = (JArray)payload["unifiedCommitments"];
                Assert.That(rows.Count, Is.GreaterThan(0));
                var row = (JObject)(last ? rows.Last : rows.First);
                switch (defect)
                {
                    case "missing": row.Remove(marker); break;
                    case "null": row[marker] = JValue.CreateNull(); break;
                    case "text": row[marker] = "0"; break;
                    case "fraction": row[marker] = 0.0; break;
                    case "boolean": row[marker] = false; break;
                    case "one": row[marker] = 1; break;
                    case "negative": row[marker] = -1; break;
                    default: row[marker] = new JValue(BigInteger.One << 100); break;
                }
                Refused(payload, defect == "huge" ? null : "Every historical schema27 canonical row requires present integer-zero chronology markers.");
            }
        }

        /// <summary>Schema 28's additions are refused by name even at their inert literal values.</summary>
        [TestCase("allianceLeakRulesStartWeek")] [TestCase("pactPlanRulesStartWeek")] [TestCase("allWeekRulesStartWeek")]
        [TestCase("npcSocial.beatWeek")] [TestCase("npcSocial.beatWindow")] [TestCase("npcSocial.beatsFired")]
        [TestCase("npcSocial.beatSeats")] [TestCase("npcSocial.beatPlan")] [TestCase("npcSocial.acts")] [TestCase("ledger.plans")]
        public void ALiteral27NeverCarriesSchema28Storage(string path)
        {
            var payload = Literal27(1, "promise"); Accept(payload);
            string[] parts = path.Split('.');
            var owner = parts.Length == 1 ? payload : (JObject)payload[parts[0]];
            string name = parts[parts.Length - 1];
            owner.Add(name, name == "beatWindow" ? new JValue(-1) : name == "beatPlan" || name == "acts" || name == "plans" ? (JToken)new JArray() : new JValue(0));
            Refused(payload, "Historical schema27 cannot carry schema28 storage.");
        }

        [TestCase("missing")] [TestCase("null")] [TestCase("object")] [TestCase("row-null")] [TestCase("row-number")] [TestCase("401-rows")]
        public void TheCanonicalListIsABoundedListOfObjects(string defect)
        {
            var payload = Literal27(2, "promise"); Accept(payload);
            var rows = (JArray)payload["unifiedCommitments"];
            switch (defect)
            {
                case "missing": payload.Remove("unifiedCommitments"); break;
                case "null": payload["unifiedCommitments"] = JValue.CreateNull(); break;
                case "object": payload["unifiedCommitments"] = new JObject(); break;
                case "row-null": rows.Add(JValue.CreateNull()); break;
                case "row-number": rows.Add(1); break;
                default: payload["unifiedCommitments"] = new JArray(Enumerable.Range(0, 401).Select(_ => rows[0].DeepClone())); break;
            }
            Refused(payload, defect.StartsWith("row-", StringComparison.Ordinal)
                ? "Every historical schema27 canonical row is an object." : "Historical schema27 requires its bounded canonical list.");
        }

        /// <summary>Everything the markers do not cover is fixed26's, and through it fixed25's, to refuse.</summary>
        [TestCase("unknown-root")] [TestCase("unknown-npc-social")] [TestCase("unknown-ledger")] [TestCase("unknown-row")]
        [TestCase("missing-archive")] [TestCase("nonempty-archive")] [TestCase("target-value")] [TestCase("subtype-value")]
        [TestCase("authority-two")] [TestCase("bad-revision")] [TestCase("bad-stat")] [TestCase("missing-ledger")]
        public void EverythingElseIsTheFixed26ContractsToRefuse(string defect)
        {
            var payload = Literal27(2, "broken-deal"); Accept(payload);
            var row = (JObject)payload["unifiedCommitments"][0];
            switch (defect)
            {
                case "unknown-root": payload["futureAuthority"] = false; break;
                case "unknown-npc-social": payload["npcSocial"]["beatCursor"] = 0; break;
                case "unknown-ledger": payload["ledger"]["planCalls"] = new JArray(); break;
                case "unknown-row": row["voteChronologyVersion"] = 0; break;
                case "missing-archive": payload.Remove("unifiedVoteReveals"); break;
                case "nonempty-archive": ((JArray)payload["unifiedVoteReveals"]).Add(new JObject { ["week"] = 1, ["ballots"] = new JArray() }); break;
                case "target-value": row["targetId"] = row["beneficiaryId"].DeepClone(); break;
                case "subtype-value": row["subtype"] = "vote_save"; break;
                case "authority-two": payload["unifiedCommitmentRulesVersion"] = 2; break;
                case "bad-revision": payload["revision"] = -1; break;
                case "bad-stat": payload["contestants"][0]["stats"]["physical"] = 11; break;
                default: payload.Remove("ledger"); break;
            }
            Refused(payload, null);
        }

        [TestCase("null-input")] [TestCase("deep")] [TestCase("long-text")] [TestCase("long-name")] [TestCase("wide-text")]
        [TestCase("huge-integer")] [TestCase("negative-huge-integer")] [TestCase("date")] [TestCase("bytes")] [TestCase("raw")] [TestCase("undefined")]
        public void PreflightBoundsComeBeforeAnyCloneOrHeader(string defect)
        {
            var payload = Literal27(2, "promise"); Accept(payload);
            string expected = null;
            switch (defect)
            {
                case "null-input": payload = null; expected = "Historical schema27 payload is missing."; break;
                case "deep": AddDepth(payload, 64); expected = "Historical schema27 tree is too deeply nested."; break;
                case "long-text": payload["futureMember"] = new string('x', 8 * 1024 * 1024 + 1); expected = "Historical schema27 text is too large."; break;
                case "long-name": payload[new string('x', 8 * 1024 * 1024 + 1)] = true; expected = "Historical schema27 property name is too large."; break;
                case "wide-text": payload["futureMember"] = new string('ࠀ', 3 * 1024 * 1024); expected = "Historical schema27 payload exceeds eight MiB."; break;
                case "huge-integer": payload["futureMember"] = new JValue(BigInteger.One << 1024); expected = "Historical schema27 integer exceeds every finite numeric contract."; break;
                case "negative-huge-integer": payload["futureMember"] = new JValue(-(BigInteger.One << 1024)); expected = "Historical schema27 integer exceeds every finite numeric contract."; break;
                case "date": payload["futureMember"] = new JValue(new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc)); expected = "Historical schema27 contains a non-JSON token."; break;
                case "bytes": payload["futureMember"] = new JValue(new byte[] { 1, 2, 3 }); expected = "Historical schema27 contains a non-JSON token."; break;
                case "raw": payload["futureMember"] = new JRaw("null"); expected = "Historical schema27 contains a non-JSON token."; break;
                default: payload["futureMember"] = JValue.CreateUndefined(); expected = "Historical schema27 contains a non-JSON token."; break;
            }
            // A header defect would be refused too; the bound must be what refuses first.
            if (payload != null) payload["schemaVersion"] = "not a header";
            Refused(payload, expected);
        }

        /// <summary>Just inside the bounds, the payload reaches its real shape refusal rather than a preflight one.</summary>
        [TestCase("depth-63")] [TestCase("largest-integer")]
        public void NearBoundInputsReachTheirShapeRefusal(string defect)
        {
            var payload = Literal27(2, "promise"); Accept(payload);
            if (defect == "depth-63") AddDepth(payload, 63);
            else payload["futureMember"] = new JValue((BigInteger.One << 1024) - BigInteger.One);
            var refusal = Refused(payload, null);
            Assert.That(refusal.Message.Contains("deeply nested") || refusal.Message.Contains("integer exceeds"), Is.False, refusal.Message);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A lawful witness's field view (no getters) at the current schema, taken to literal 27.</summary>
        private static JObject Literal27(int mode, string kind)
        {
            var state = LegacyDigestSchema27TestStates.Witness(mode, kind);
            LegacyDigestSchema27TestStates.Valid(state);
            var field28 = LegacyDigestSchema27TestStates.WithoutGetters(LegacyDigestSchema27TestStates.Trace(state));
            return LegacyDigestSchema27TestStates.Neutral27(field28);
        }

        private static void Accept(JObject payload)
        {
            string before = Text(payload);
            Assert.DoesNotThrow(() => Contract("FrozenEpisodeV27", payload));
            Assert.That(Text(payload), Is.EqualTo(before), "Acceptance never repairs, defaults or reorders the caller's tree.");
        }

        private static InvalidDataException Refused(JObject payload, string exact)
        {
            var order = payload?.DescendantsAndSelf().OfType<JProperty>().Select(p => p.Name).ToArray();
            var before = payload?.DeepClone();
            var refusal = Assert.Throws<InvalidDataException>(() => Contract("FrozenEpisodeV27", payload));
            if (exact != null) Assert.That(refusal.Message, Is.EqualTo(exact));
            if (payload != null)
            {
                Assert.That(JToken.DeepEquals(before, payload), Is.True, "A refused historical payload is preserved, not repaired.");
                Assert.That(payload.DescendantsAndSelf().OfType<JProperty>().Select(p => p.Name), Is.EqualTo(order));
            }
            return refusal;
        }

        private static void Contract(string name, JObject payload)
        {
#if UNITY_5_3_OR_NEWER
            var historicalAssembly = typeof(Gamesim.Persistence.EpisodeSaveStore).Assembly;
#else
            var historicalAssembly = typeof(EpisodeState).Assembly;
#endif
            var contract = historicalAssembly.GetType("Gamesim.Persistence." + name, true);
            var validate = contract.GetMethod("Validate", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(JObject) }, null);
            Assert.That(validate, Is.Not.Null); Assert.That(validate.IsAssembly, Is.True); Assert.That(validate.ReturnType, Is.EqualTo(typeof(void)));
            try { validate.Invoke(null, new object[] { payload }); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        private static void AddDepth(JObject value, int depth)
        {
            JObject cursor = value;
            for (int i = 0; i < depth; i++) { var child = new JObject(); cursor["deep"] = child; cursor = child; }
        }

        private static string Text(JToken token) => token == null ? "null" : token.ToString(Formatting.None);
    }
}
