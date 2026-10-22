#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Actual accepted old-only public Apply reference, not newly generated expected data.
    /// These ordinary eight-positive/two-refusal cases are NOT the funded V2 private-leaf oracle.
    /// Proposed raw fixture admission remains a separate root decision; absence fails, never skips.
    /// </summary>
    public sealed class StrategicOldControls05ParityTests
    {
        private const string CorpusSha = "9CF45D15202DB19DE11F05ED6C6C2131D2198B6FA8929CAED89CCC476173E7B8";
        private static readonly string[] Cases = {
            "legacy-off/receptive", "legacy-off/open", "legacy-off/skeptical", "legacy-off/hostile",
            "mode2-all-week/receptive", "mode2-all-week/open", "mode2-all-week/skeptical", "mode2-all-week/hostile",
            "legacy-off/ap0-refusal", "mode2-all-week/ap0-refusal"
        };
        private static readonly JsonSerializerSettings Fields = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None, DateParseHandling = DateParseHandling.None,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            FloatParseHandling = FloatParseHandling.Double, MaxDepth = 64, ContractResolver = new PublicFieldsOnly()
        };
        private JObject[] records;

        [OneTimeSetUp]
        public void ReadOnlyAcceptedOriginalsNeverCurrentEngineCapture()
        {
            string path = Path.Combine(Application.dataPath, "Gamesim/Tests/EditMode/Fixtures/StrategicOldControls05/probe.stdout.jsonl");
            Assert.That(File.Exists(path), Is.True, "Explicit unchanged-byte fixture admission is required; no capture fallback.");
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(bytes.Length, Is.EqualTo(4667052)); Assert.That(Sha(bytes), Is.EqualTo(CorpusSha));
            Assert.That(bytes.All(value => value < 128), Is.True);
            string text = new UTF8Encoding(false, true).GetString(bytes);
            Assert.That(text.Contains('\r'), Is.False);
            var lines = text.Split('\n'); Assert.That(lines.Length, Is.EqualTo(13)); Assert.That(lines[12], Is.Empty);
            records = lines.Take(12).Select(Parse).ToArray();
        }

        [Test]
        public void ExactInventoryContainsAllTenAndAllSixtyTwoOriginalBytePacks()
        {
            Assert.That((string)records[0]["kind"], Is.EqualTo("old-strategic-input-plan"));
            Assert.That(records[0]["expectedCaseIds"].Values<string>(), Is.EqualTo(Cases));
            Assert.That(records.Skip(1).Take(10).Select(row => (string)row["caseId"]), Is.EqualTo(Cases));
            Assert.That((string)records[11]["kind"], Is.EqualTo("old-strategic-summary"));
            Assert.That((int)records[11]["positiveControls"], Is.EqualTo(8));
            Assert.That((int)records[11]["ap0Refusals"], Is.EqualTo(2));
            var packs = records.SelectMany(row => row.Descendants().OfType<JProperty>())
                .Where(property => property.Name.EndsWith("OriginalUtf8", StringComparison.Ordinal)).ToArray();
            Assert.That(packs.Length, Is.EqualTo(62));
            foreach (var pack in packs) Original((JObject)pack.Value);
        }

        [TestCase("legacy-off/receptive")]
        [TestCase("legacy-off/open")]
        [TestCase("legacy-off/skeptical")]
        [TestCase("legacy-off/hostile")]
        [TestCase("mode2-all-week/receptive")]
        [TestCase("mode2-all-week/open")]
        [TestCase("mode2-all-week/skeptical")]
        [TestCase("mode2-all-week/hostile")]
        [TestCase("legacy-off/ap0-refusal")]
        [TestCase("mode2-all-week/ap0-refusal")]
        public void CompleteOrdinaryApplyAndReadMatchImmutableOldReference(string caseId)
        {
            var row = records.Single(record => (string)record["caseId"] == caseId);
            string before = Original((JObject)row["inputOriginalUtf8"]);
            string request = Original((JObject)row["commandOriginalUtf8"]);
            var input = JsonConvert.DeserializeObject<EpisodeState>(before, Fields);
            var command = JsonConvert.DeserializeObject<EpisodeCommand>(request, Fields);
            Assert.That(EpisodeValidation.TryValidate(input, out string inputError), Is.True, inputError);
            Assert.That(Image(input), Is.EqualTo(before)); Assert.That(Image(command), Is.EqualTo(request));
            var beforeRead = Parse(Image(VoteRead.Read(input)));
            Assert.That(ExactCapturedJson.Equal(beforeRead, row["beforeRead"]), Is.True,
                "Exact complete captured old DTO values; no separately retained original read-fragment bytes or new numeric derivation.");
            Assert.That(Image(input), Is.EqualTo(before));
            var engine = new EpisodeEngine(input); var result = engine.Apply(command); var published = engine.Snapshot;
            Assert.That(Image(input), Is.EqualTo(before)); Assert.That(Image(command), Is.EqualTo(request));
            Assert.That(Image(result), Is.EqualTo(Original((JObject)row["resultOriginalUtf8"])), "Entire CommandResult, not filtered owner fields.");
            Assert.That(Image(result.state), Is.EqualTo(Original((JObject)row["resultStateOriginalUtf8"])));
            Assert.That(Image(published), Is.EqualTo(Original((JObject)row["publishedOriginalUtf8"])));
            Assert.That(EpisodeValidation.TryValidate(published, out string error), Is.True, error);
            var afterRead = Parse(Image(VoteRead.Read(published)));
            Assert.That(ExactCapturedJson.Equal(afterRead, row["afterRead"]), Is.True,
                "Exact ordered/type/integer/binary64 captured-value comparison, including signed zero.");
            Assert.That(Image(published), Is.EqualTo(Image(engine.Snapshot)), "Read has no state/RNG side effect.");
            Assert.That(result.duplicate, Is.False);
            if (caseId.EndsWith("ap0-refusal", StringComparison.Ordinal))
            {
                Assert.That(result.accepted, Is.False); Assert.That(result.reason, Is.EqualTo("You have no conversations left this week."));
                Assert.That(Image(published), Is.EqualTo(before));
            }
            else
            {
                Assert.That(result.accepted, Is.True); Assert.That(published.revision, Is.EqualTo(input.revision + 1));
                Assert.That(published.lobbies.Last().approach, Is.EqualTo(LobbyApproach.Strategic));
                Assert.That(published.lobbies.Last().response, Is.EqualTo(caseId.Split('/')[1]));
                if (caseId.EndsWith("skeptical", StringComparison.Ordinal)) Assert.That(published.lobbies.Last().influence, Is.EqualTo(-5));
                Assert.That(published.outOfPhaseSocialActions, Is.EqualTo(input.outOfPhaseSocialActions + 1));
                Assert.That(published.windowActions[Windows.AfterVeto], Is.EqualTo(input.windowActions[Windows.AfterVeto] + 1));
            }
        }

        [TestCase("identical-complete-values")]
        [TestCase("adjacent-double")]
        [TestCase("nearby-double")]
        [TestCase("signed-zero")]
        [TestCase("huge-integer")]
        [TestCase("missing-member")]
        [TestCase("extra-member")]
        [TestCase("member-order")]
        [TestCase("array-order")]
        [TestCase("token-type")]
        [TestCase("string-value")]
        [TestCase("boolean-value")]
        [TestCase("nested-value")]
        [TestCase("null-type")]
        [TestCase("nonfinite-nan")]
        [TestCase("nonfinite-infinity")]
        [TestCase("unsupported-date")]
        public void StrictCapturedJsonComparisonPreservesCompleteOrderedValuesAndExactNumbers(string change)
        {
            JToken expected = Parse("{\"a\":1,\"b\":[true,null,\"exact\",{\"nested\":2}]}");
            JToken actual = expected.DeepClone(); bool equal = false;
            switch (change)
            {
                case "identical-complete-values":
                    expected = Parse("{\"a\":-9223372036854775808,\"b\":18446744073709551615,"
                        + "\"huge\":18446744073709551616000000000000000001,"
                        + "\"negativeHuge\":-18446744073709551616000000000000000001,"
                        + "\"values\":[true,false,null,\"exact\",{\"nested\":2},1.7976931348623157E+308,5E-324]}");
                    actual = expected.DeepClone(); equal = true; break;
                case "adjacent-double":
                    expected = Floating(1d); actual = Floating(BitConverter.Int64BitsToDouble(0x3ff0000000000001L)); break;
                case "nearby-double": expected = Floating(1d); actual = Floating(1.000000000000001d); break;
                case "signed-zero": expected = Floating(0d); actual = Floating(BitConverter.Int64BitsToDouble(long.MinValue)); break;
                case "huge-integer":
                    expected = Parse("{\"value\":18446744073709551616000000000000000001}");
                    actual = Parse("{\"value\":18446744073709551616000000000000000002}"); break;
                case "missing-member": ((JObject)actual).Remove("b"); break;
                case "extra-member": ((JObject)actual)["extra"] = 0; break;
                case "member-order": actual = Parse("{\"b\":[true,null,\"exact\",{\"nested\":2}],\"a\":1}"); break;
                case "array-order": actual = Parse("{\"a\":1,\"b\":[null,true,\"exact\",{\"nested\":2}]}"); break;
                case "token-type": expected = Parse("{\"value\":1}"); actual = Parse("{\"value\":1.0}"); break;
                case "string-value": actual["b"][2] = "Exact"; break;
                case "boolean-value": actual["b"][0] = false; break;
                case "nested-value": actual["b"][3]["nested"] = 3; break;
                case "null-type": actual["b"][1] = "null"; break;
                case "nonfinite-nan": expected = Floating(double.NaN); actual = Floating(double.NaN); break;
                case "nonfinite-infinity": expected = Floating(double.PositiveInfinity); actual = Floating(double.PositiveInfinity); break;
                case "unsupported-date":
                    expected = new JObject(new JProperty("value", new JValue(new DateTime(2026, 10, 20))));
                    actual = expected.DeepClone(); break;
                default: Assert.Fail("Unknown fixed comparer control."); break;
            }
            Assert.That(ExactCapturedJson.Equal(actual, expected), Is.EqualTo(equal), change);
            Assert.That(ExactCapturedJson.Equal(expected, actual), Is.EqualTo(equal), change + " / symmetric");
        }

        // Boxed on purpose: under Json.NET 13.0.2, JValue(double) routes through a cached boxed zero and
        // turns -0.0 into +0.0, so the signed-zero control would compare two positive zeros.
        // JValue(object) keeps the caller's bits. Never "simplify" this back to new JValue(value).
        private static JObject Floating(double value) => new JObject(new JProperty("value", new JValue((object)value)));

        private static string Original(JObject pack)
        {
            Assert.That((string)pack["encoding"], Is.EqualTo("original-compact-public-field-json-utf8-base64"));
            string base64 = (string)pack["base64"]; byte[] bytes = Convert.FromBase64String(base64);
            Assert.That(bytes.Length, Is.EqualTo((int)pack["bytes"])); Assert.That(bytes.Length, Is.LessThanOrEqualTo(8 * 1024 * 1024));
            Assert.That(Convert.ToBase64String(bytes), Is.EqualTo(base64)); Assert.That(Sha(bytes), Is.EqualTo((string)pack["sha256"]));
            string image = new UTF8Encoding(false, true).GetString(bytes); Parse(image); return image;
        }
        private static JObject Parse(string json)
        {
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double, MaxDepth = 64 };
            var result = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            Assert.That(reader.Read(), Is.False, "Exactly one original JSON object."); return result;
        }
        private static string Image(object value) => JsonConvert.SerializeObject(value, Formatting.None, Fields);
        private static string Sha(byte[] bytes) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""); }
        // Exact comparison of captured JSON values, never a new gameplay oracle.
        // Read DTOs have captured-value provenance, NOT separately retained original fragment bytes.
        private static class ExactCapturedJson
        {
            internal static bool Equal(JToken left, JToken right) => Equal(left, right, 0);
            private static bool Equal(JToken left, JToken right, int depth)
            {
                if (left == null || right == null || depth > 64 || left.Type != right.Type) return false;
                if (left.Type == JTokenType.Object)
                {
                    if (!(left is JObject a) || !(right is JObject b)) return false;
                    var first = a.Properties().ToArray(); var second = b.Properties().ToArray();
                    if (first.Length != second.Length) return false;
                    var names = new HashSet<string>(StringComparer.Ordinal);
                    for (int at = 0; at < first.Length; at++)
                        if (!names.Add(first[at].Name)
                            || !string.Equals(first[at].Name, second[at].Name, StringComparison.Ordinal)
                            || !Equal(first[at].Value, second[at].Value, depth + 1)) return false;
                    return true;
                }
                if (left.Type == JTokenType.Array)
                {
                    if (!(left is JArray a) || !(right is JArray b) || a.Count != b.Count) return false;
                    for (int at = 0; at < a.Count; at++) if (!Equal(a[at], b[at], depth + 1)) return false;
                    return true;
                }
                if (!(left is JValue x) || !(right is JValue y)) return false;
                switch (left.Type)
                {
                    case JTokenType.Integer:
                        return Integer(x.Value, out string firstInteger) && Integer(y.Value, out string secondInteger)
                            && string.Equals(firstInteger, secondInteger, StringComparison.Ordinal);
                    case JTokenType.Float:
                        return x.Value is double firstDouble && y.Value is double secondDouble
                            && !double.IsNaN(firstDouble) && !double.IsInfinity(firstDouble)
                            && !double.IsNaN(secondDouble) && !double.IsInfinity(secondDouble)
                            && BitConverter.DoubleToInt64Bits(firstDouble) == BitConverter.DoubleToInt64Bits(secondDouble);
                    case JTokenType.String:
                        return x.Value is string firstString && y.Value is string secondString
                            && string.Equals(firstString, secondString, StringComparison.Ordinal);
                    case JTokenType.Boolean:
                        return x.Value is bool firstBoolean && y.Value is bool secondBoolean && firstBoolean == secondBoolean;
                    case JTokenType.Null:
                        return x.Value == null && y.Value == null;
                    default:
                        return false; // No Date/Bytes/Raw/Undefined/constructor or numeric coercion.
                }
            }
            private static bool Integer(object value, out string exact)
            {
                exact = null;
                if (!(value is sbyte || value is byte || value is short || value is ushort
                    || value is int || value is uint || value is long || value is ulong || value is BigInteger)) return false;
                exact = ((IFormattable)value).ToString("D", CultureInfo.InvariantCulture);
                return true; // Canonical arbitrary-range decimal text; never narrowed through double/Int64.
            }
        }
        private sealed class PublicFieldsOnly : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization serialization) =>
                type.GetFields(BindingFlags.Instance | BindingFlags.Public).Select(field => base.CreateProperty(field, MemberSerialization.Fields)).ToList();
        }
    }
}
#endif
