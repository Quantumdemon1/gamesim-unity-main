// Frozen29 (SCH30): carried from Tools/SimulationTests/Frozen28/FrozenV28InputTests.cs by name, then to schema 29
// where a case builds a whole schema-29 state or payload.
// SOURCE-ONLY component tests. No historical/game SaveStore, corpus or native acceptance.
// Fixtures below are explicit historical component fields, not current DTO/factory output.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Persistence.Frozen29Components.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class FrozenV29InputTests
    {
        private const string Alliance = "{\"id\":\"a\",\"name\":\"Alliance\",\"members\":[\"second\",null,\"first\",\"second\"],\"active\":true,\"playerJoined\":false}";
        private const string AllianceRow = "{\"id\":\"a\",\"why\":null,\"startedWeek\":0,\"endedWeek\":0}";

        [Test]
        public void Parse_PreservesPropertyOrderDatesStringsAndInputBytes()
        {
            var bytes = Utf8("{\"z\":\"2026-10-12T00:00:00Z\",\"a\":\"/Date(1198908717056)/\"}");
            var before = (byte[])bytes.Clone();
            var parsed = FrozenV29Input.ParseObject(bytes);
            CollectionAssert.AreEqual(new[] { "z", "a" }, parsed.Properties().Select(x => x.Name));
            Assert.That(parsed["z"].Type, Is.EqualTo(JTokenType.String));
            Assert.That(parsed["a"].Type, Is.EqualTo(JTokenType.String));
            Assert.That((string)parsed["z"], Is.EqualTo("2026-10-12T00:00:00Z"));
            CollectionAssert.AreEqual(before, bytes);
        }

        [Test]
        public void Parse_AcceptsLeadingUtf8BomWithoutChangingIt()
        {
            var bytes = new byte[] { 0xef, 0xbb, 0xbf, (byte)'{', (byte)'}' };
            var before = (byte[])bytes.Clone();
            Assert.That(FrozenV29Input.ParseObject(bytes).Count, Is.EqualTo(0));
            CollectionAssert.AreEqual(before, bytes);
        }

        [TestCase("/* leading */ {x:'text', y:[1,2,],}")]
        [TestCase("{\"x\":/* interior */\"text\",\"y\":[1,2]}")]
        public void Parse_RetainsHistoricalJsonNetSyntaxExtensions(string text)
        {
            var parsed = FrozenV29Input.ParseObject(Utf8(text));
            Assert.That((string)parsed["x"], Is.EqualTo("text"));
            CollectionAssert.AreEqual(new[] { 1, 2 }, ((JArray)parsed["y"]).Select(x => (int)x));
        }

        [TestCase("{\"n\":NaN}")]
        [TestCase("{\"n\":Infinity}")]
        [TestCase("{\"n\":-Infinity}")]
        public void Parse_DoesNotPretendToValidateNonfiniteNumericSemantics(string text)
        {
            var parsed = FrozenV29Input.ParseObject(Utf8(text));
            Assert.That(parsed["n"].Type, Is.EqualTo(JTokenType.Float));
            var number = (double)parsed["n"];
            Assert.That(double.IsNaN(number) || double.IsInfinity(number), Is.True);
        }

        [TestCase("")]
        [TestCase("null")]
        [TestCase("[]")]
        [TestCase("\"object\"")]
        [TestCase("true")]
        [TestCase("0")]
        [TestCase("{} {}")]
        [TestCase("{} null")]
        [TestCase("{} /* trailing */")]
        [TestCase("{} // trailing")]
        [TestCase("{\"x\":")]
        [TestCase("{\"x\":\"unterminated}")]
        [TestCase("{\"x\":1,\"x\":2}")]
        [TestCase("{\"o\":{\"x\":1,\"x\":2}}")]
        [TestCase("{\"a\":[{\"x\":1,\"x\":2}]}")]
        [TestCase("{\"x\":1,\"\\u0078\":2}")]
        [TestCase("{\"x\":1} garbage")]
        public void Parse_RefusesMissingWrongRootDuplicateTrailingAndMalformedData(string text)
        {
            var bytes = Utf8(text);
            var before = (byte[])bytes.Clone();
            Assert.Throws<InvalidDataException>(() => FrozenV29Input.ParseObject(bytes));
            CollectionAssert.AreEqual(before, bytes);
        }

        [Test]
        public void Parse_DuplicateNamesAreOrdinalNotCaseFolded()
        {
            var token = FrozenV29Input.ParseObject(Utf8("{\"id\":1,\"ID\":2}"));
            CollectionAssert.AreEqual(new[] { "id", "ID" }, token.Properties().Select(x => x.Name));
        }

        [Test]
        public void Parse_NullBytesRefusedWithDeterministicDataException()
        {
            var error = Assert.Throws<InvalidDataException>(() => FrozenV29Input.ParseObject(null));
            Assert.That(error.Message, Is.EqualTo("Frozen29 component input is missing or exceeds eight MiB."));
        }

        private static IEnumerable<TestCaseData> MalformedUtf8()
        {
            yield return new TestCaseData((object)new byte[] { (byte)'{', (byte)'"', (byte)'x', (byte)'"', (byte)':', (byte)'"', 0xc0, 0xaf, (byte)'"', (byte)'}' }).SetName("Parse_StrictUtf8RefusesMalformedAndUtf16BomBytes_OverlongSlash");
            yield return new TestCaseData((object)new byte[] { (byte)'{', (byte)'"', (byte)'x', (byte)'"', (byte)':', (byte)'"', 0xed, 0xa0, 0x80, (byte)'"', (byte)'}' }).SetName("Parse_StrictUtf8RefusesMalformedAndUtf16BomBytes_EncodedSurrogate");
            yield return new TestCaseData((object)new byte[] { 0xf4, 0x90, 0x80, 0x80 }).SetName("Parse_StrictUtf8RefusesMalformedAndUtf16BomBytes_BeyondUnicodeRange");
            yield return new TestCaseData((object)new byte[] { 0xc2 }).SetName("Parse_StrictUtf8RefusesMalformedAndUtf16BomBytes_IncompleteMultibyte");
            yield return new TestCaseData((object)new byte[] { 0xff, 0xfe, (byte)'{', 0, (byte)'}', 0 }).SetName("Parse_StrictUtf8RefusesMalformedAndUtf16BomBytes_Utf16LittleEndianBom");
            yield return new TestCaseData((object)new byte[] { 0xfe, 0xff, 0, (byte)'{', 0, (byte)'}' }).SetName("Parse_StrictUtf8RefusesMalformedAndUtf16BomBytes_Utf16BigEndianBom");
        }

        [TestCaseSource(nameof(MalformedUtf8))]
        public void Parse_StrictUtf8RefusesMalformedAndUtf16BomBytes(byte[] bytes)
        {
            var before = (byte[])bytes.Clone();
            var error = Assert.Throws<InvalidDataException>(() => FrozenV29Input.ParseObject(bytes));
            Assert.That(error.Message, Is.EqualTo("Frozen29 component input is not strict UTF8."));
            CollectionAssert.AreEqual(before, bytes);
        }

        [Test]
        public void Parse_ExactEightMiBLimitAcceptedNextByteRefusedAndBomCounts()
        {
            var exact = Utf8("{\"x\":\"" + new string('x', 8 * 1024 * 1024 - 8) + "\"}");
            Assert.That(exact.Length, Is.EqualTo(8 * 1024 * 1024));
            Assert.That(((string)FrozenV29Input.ParseObject(exact)["x"]).Length, Is.EqualTo(8 * 1024 * 1024 - 8));
            var over = new byte[exact.Length + 1];
            Buffer.BlockCopy(exact, 0, over, 0, exact.Length);
            over[over.Length - 1] = (byte)' ';
            Assert.Throws<InvalidDataException>(() => FrozenV29Input.ParseObject(over));
            var withBom = new byte[exact.Length + 3];
            withBom[0] = 0xef; withBom[1] = 0xbb; withBom[2] = 0xbf;
            Buffer.BlockCopy(exact, 0, withBom, 3, exact.Length);
            Assert.Throws<InvalidDataException>(() => FrozenV29Input.ParseObject(withBom));
        }

        [Test]
        public void Parse_ByteLimitCountsMultibyteTextNotUtf16Length()
        {
            // Three UTF8 bytes per character: this text is well below8MiB UTF16 characters.
            var bytes = Utf8("{\"x\":\"" + new string('\u20ac', (8 * 1024 * 1024) / 3 + 1) + "\"}");
            Assert.That(bytes.Length, Is.GreaterThan(8 * 1024 * 1024));
            Assert.Throws<InvalidDataException>(() => FrozenV29Input.ParseObject(bytes));
        }

        [Test]
        public void Parse_Depth64AcceptedDepth65Refused()
        {
            var exact = "{\"x\":" + new string('[', 63) + "0" + new string(']', 63) + "}";
            Assert.That(FrozenV29Input.ParseObject(Utf8(exact)), Is.Not.Null);
            var over = "{\"x\":" + new string('[', 64) + "0" + new string(']', 64) + "}";
            Assert.Throws<InvalidDataException>(() => FrozenV29Input.ParseObject(Utf8(over)));
        }

        [Test]
        public void Hydrate_ExplicitNullReferencesAreNotRepaired()
        {
            var token = FrozenV29Input.ParseObject(Utf8("{\"id\":null,\"name\":null,\"members\":null,\"active\":false,\"playerJoined\":false}"));
            var before = new TreeWitness(token);
            var result = (Frozen29Data.AllianceState)FrozenV29Input.HydrateComponentShapeOnly(token, "AllianceState");
            Assert.That(result.id, Is.Null);
            Assert.That(result.name, Is.Null);
            Assert.That(result.members, Is.Null);
            Assert.That(result.active, Is.False);
            before.AssertUnchanged(token);
        }

        [Test]
        public void Hydrate_ListsAreDetachedReplacedAndOrderedIncludingNullDuplicates()
        {
            var token = FrozenV29Input.ParseObject(Utf8(Alliance));
            var before = new TreeWitness(token);
            var first = (Frozen29Data.AllianceState)FrozenV29Input.HydrateComponentShapeOnly(token, "AllianceState");
            var second = (Frozen29Data.AllianceState)FrozenV29Input.HydrateComponentShapeOnly(token, "AllianceState");
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first.members, Is.Not.SameAs(second.members));
            CollectionAssert.AreEqual(new[] { "second", null, "first", "second" }, first.members);
            CollectionAssert.AreEqual(new[] { "second", null, "first", "second" }, second.members);
            first.members.Clear(); first.id = "changed";
            Assert.That(second.id, Is.EqualTo("a"));
            Assert.That(second.members.Count, Is.EqualTo(4));
            before.AssertUnchanged(token);
        }

        [Test]
        public void Hydrate_PropertyOrderDoesNotBecomeListOrderOrMutateInput()
        {
            var token = FrozenV29Input.ParseObject(Utf8("{\"playerJoined\":false,\"active\":true,\"members\":[\"b\",\"a\"],\"name\":\"Alliance\",\"id\":\"a\"}"));
            var before = new TreeWitness(token);
            var result = (Frozen29Data.AllianceState)FrozenV29Input.HydrateComponentShapeOnly(token, "AllianceState");
            Assert.That(result.id, Is.EqualTo("a"));
            CollectionAssert.AreEqual(new[] { "b", "a" }, result.members);
            before.AssertUnchanged(token);
        }

        [TestCase("id")]
        [TestCase("name")]
        [TestCase("members")]
        [TestCase("active")]
        [TestCase("playerJoined")]
        public void Hydrate_MissingComponentFieldRefusedBeforeDefaults(string field)
        {
            var token = FrozenV29Input.ParseObject(Utf8(Alliance));
            token.Property(field).Remove();
            var before = new TreeWitness(token);
            Assert.Throws<InvalidDataException>(() => FrozenV29Input.HydrateComponentShapeOnly(token, "AllianceState"));
            before.AssertUnchanged(token);
        }

        [TestCase("$type")]
        [TestCase("$id")]
        [TestCase("$ref")]
        [TestCase("$values")]
        [TestCase("schemaVersion")]
        [TestCase("ID")]
        public void Hydrate_UnknownMetadataAndHeaderFieldsAreNotSilentlyDiscarded(string field)
        {
            var token = FrozenV29Input.ParseObject(Utf8(AllianceRow));
            token.Add(field, "System.IO.FileInfo, System.Private.CoreLib");
            var before = new TreeWitness(token);
            Assert.Throws<InvalidDataException>(() => FrozenV29Input.HydrateComponentShapeOnly(token, "AllianceRow"));
            before.AssertUnchanged(token);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("EpisodeState ")]
        [TestCase("Gamesim.Simulation.EpisodeState")]
        [TestCase("System.Object")]
        [TestCase("List<AllianceState>")]
        [TestCase("EpisodePhase")]
        [TestCase("int")]
        public void Hydrate_OnlyLiteralComponentNamesResolve(string name)
        {
            var token = FrozenV29Input.ParseObject(Utf8(AllianceRow));
            var before = new TreeWitness(token);
            Assert.Throws<InvalidDataException>(() => FrozenV29Input.HydrateComponentShapeOnly(token, name));
            before.AssertUnchanged(token);
        }

        [Test]
        public void Hydrate_NullTokenAndIncompleteRootRefusedButExplicitReferenceNullPreserved()
        {
            Assert.Throws<InvalidDataException>(() => FrozenV29Input.HydrateRootShapeOnly(null));
            Assert.Throws<InvalidDataException>(() => FrozenV29Input.HydrateComponentShapeOnly(null, "AllianceState"));
            Assert.Throws<InvalidDataException>(() => FrozenV29Input.HydrateRootShapeOnly(new JObject()));
            var nil = JValue.CreateNull();
            var before = new TreeWitness(nil);
            Assert.That(FrozenV29Input.HydrateComponentShapeOnly(nil, "AllianceState"), Is.Null);
            before.AssertUnchanged(nil);
        }

        [TestCase("2147483648")]
        [TestCase("-2147483649")]
        [TestCase("9223372036854775808")]
        public void Hydrate_NumericOverflowRefusedWithoutTokenCoercion(string integer)
        {
            var token = FrozenV29Input.ParseObject(Utf8("{\"id\":\"a\",\"why\":null,\"startedWeek\":" + integer + ",\"endedWeek\":0}"));
            var before = new TreeWitness(token);
            Assert.Throws<InvalidDataException>(() => FrozenV29Input.HydrateComponentShapeOnly(token, "AllianceRow"));
            before.AssertUnchanged(token);
        }

        [Test]
        public void Hydrate_FiniteBigIntegerBackingCanConvertToDeclaredIntWithoutMutation()
        {
            var token = FrozenV29Input.ParseObject(Utf8(AllianceRow));
            token["startedWeek"] = new JValue(new BigInteger(17));
            var before = new TreeWitness(token);
            var result = (Frozen29Data.AllianceRow)FrozenV29Input.HydrateComponentShapeOnly(token, "AllianceRow");
            Assert.That(result.startedWeek, Is.EqualTo(17));
            Assert.That(((JValue)token["startedWeek"]).Value, Is.TypeOf<BigInteger>());
            before.AssertUnchanged(token);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Hydrate_NonfiniteDirectNumbersRefusedBeforeSerializingThemAsStrings(double number)
        {
            var token = new JObject { ["id"] = "c", ["r"] = number, ["g"] = 0, ["b"] = 0, ["a"] = 1 };
            var before = new TreeWitness(token);
            Assert.Throws<InvalidDataException>(() => FrozenV29Input.HydrateComponentShapeOnly(token, "AppearanceColor"));
            before.AssertUnchanged(token);
        }

        [Test]
        public void Hydrate_EnormousBigIntegerRefusedBeforeDecimalRendering()
        {
            var token = FrozenV29Input.ParseObject(Utf8(AllianceRow));
            token["startedWeek"] = new JValue(BigInteger.One << 1000000);
            var before = new TreeWitness(token);
            var error = Assert.Throws<InvalidDataException>(() => FrozenV29Input.HydrateComponentShapeOnly(token, "AllianceRow"));
            Assert.That(error.Message, Is.EqualTo("Frozen29 component number is not finite."));
            before.AssertUnchanged(token);
        }

        [Test]
        public void Hydrate_DirectTokenDepthIsBoundedBeforeShapeOrRecursiveSerialization()
        {
            var token = FrozenV29Input.ParseObject(Utf8(Alliance));
            var outer = new JArray();
            var current = outer;
            for (int i = 1; i < 10000; i++) { var next = new JArray(); current.Add(next); current = next; }
            current.Add("a");
            token["members"] = outer;
            var before = new TreeWitness(token);
            var error = Assert.Throws<InvalidDataException>(() => FrozenV29Input.HydrateComponentShapeOnly(token, "AllianceState"));
            Assert.That(error.Message, Is.EqualTo("Frozen29 component token exceeds depth64."));
            before.AssertUnchanged(token);
        }

        [Test]
        public void Hydrate_DirectTokenRawAndEscapedByteBudgetsBothRefuseWithoutMutation()
        {
            var oversized = FrozenV29Input.ParseObject(Utf8(AllianceRow));
            oversized["why"] = new string('x', 8 * 1024 * 1024 + 1);
            var beforeRaw = new TreeWitness(oversized);
            Assert.Throws<InvalidDataException>(() => FrozenV29Input.HydrateComponentShapeOnly(oversized, "AllianceRow"));
            beforeRaw.AssertUnchanged(oversized);
            var escaped = FrozenV29Input.ParseObject(Utf8(AllianceRow));
            // Raw UTF8 is ~1.4MiB, but each control character emits six JSON bytes.
            escaped["why"] = new string('\u0001', (8 * 1024 * 1024) / 6 + 1);
            var beforeEscaped = new TreeWitness(escaped);
            Assert.Throws<InvalidDataException>(() => FrozenV29Input.HydrateComponentShapeOnly(escaped, "AllianceRow"));
            beforeEscaped.AssertUnchanged(escaped);
        }

        [Test]
        public void Hydrate_DirectCompactEncodingExactLimitAcceptedNextByteRefused()
        {
            const string prefix = "{\"id\":\"a\",\"why\":\"";
            const string suffix = "\",\"startedWeek\":0,\"endedWeek\":0}";
            int overhead = Utf8(prefix + suffix).Length;
            var exactText = new string('x', 8 * 1024 * 1024 - overhead);
            var token = new JObject { ["id"] = "a", ["why"] = exactText, ["startedWeek"] = 0, ["endedWeek"] = 0 };
            var before = new TreeWitness(token);
            var result = (Frozen29Data.AllianceRow)FrozenV29Input.HydrateComponentShapeOnly(token, "AllianceRow");
            Assert.That(result.why, Is.EqualTo(exactText));
            before.AssertUnchanged(token);
            token["why"] = exactText + "x";
            var beforeOver = new TreeWitness(token);
            Assert.Throws<InvalidDataException>(() => FrozenV29Input.HydrateComponentShapeOnly(token, "AllianceRow"));
            beforeOver.AssertUnchanged(token);
        }

        [Test]
        public void Hydrate_DirectPropertyNameSizeAndUnicodeAreBoundedWithoutMutation()
        {
            var oversized = new JObject { [new string('x', 8 * 1024 * 1024 + 1)] = 0 };
            var before = new TreeWitness(oversized);
            Assert.Throws<InvalidDataException>(() => FrozenV29Input.HydrateComponentShapeOnly(oversized, "AllianceRow"));
            before.AssertUnchanged(oversized);
            var invalid = new JObject { ["\ud800"] = 0 };
            var beforeInvalid = new TreeWitness(invalid);
            Assert.Throws<InvalidDataException>(() => FrozenV29Input.HydrateComponentShapeOnly(invalid, "AllianceRow"));
            beforeInvalid.AssertUnchanged(invalid);
        }

        [Test]
        public void Hydrate_UnicodeAcrossStreamingChunkBoundaryIsPreserved()
        {
            var token = FrozenV29Input.ParseObject(Utf8(AllianceRow));
            var text = new string('x', 4095) + "\ud83d\ude80" + new string('\u20ac', 4097);
            token["why"] = text;
            var before = new TreeWitness(token);
            var result = (Frozen29Data.AllianceRow)FrozenV29Input.HydrateComponentShapeOnly(token, "AllianceRow");
            Assert.That(result.why, Is.EqualTo(text));
            before.AssertUnchanged(token);
        }

        [Test]
        public void Parse_RepeatedRefusalsAreDeterministicAndDoNotEchoOrMutateData()
        {
            var bytes = Utf8("{\"secretFixtureText\":");
            var before = (byte[])bytes.Clone();
            string message = null;
            for (int i = 0; i < 32; i++)
            {
                var error = Assert.Throws<InvalidDataException>(() => FrozenV29Input.ParseObject(bytes));
                if (message == null) message = error.Message;
                Assert.That(error.Message, Is.EqualTo(message));
                Assert.That(error.Message, Does.Not.Contain("secretFixtureText"));
                CollectionAssert.AreEqual(before, bytes);
            }
        }

        private static IEnumerable<TestCaseData> NonPlainValues()
        {
            yield return new TestCaseData((object)new JValue(new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc))).SetName("Hydrate_ExternallyConstructedNonJsonTokensRefusedWithoutMutation_Date");
            yield return new TestCaseData((object)new JValue(new byte[] { 1, 2 })).SetName("Hydrate_ExternallyConstructedNonJsonTokensRefusedWithoutMutation_Bytes");
            yield return new TestCaseData((object)new JRaw("null")).SetName("Hydrate_ExternallyConstructedNonJsonTokensRefusedWithoutMutation_Raw");
            yield return new TestCaseData((object)JValue.CreateUndefined()).SetName("Hydrate_ExternallyConstructedNonJsonTokensRefusedWithoutMutation_Undefined");
            yield return new TestCaseData((object)new JConstructor("Date", 1)).SetName("Hydrate_ExternallyConstructedNonJsonTokensRefusedWithoutMutation_Constructor");
            yield return new TestCaseData((object)new JValue(new Guid("01234567-89ab-cdef-0123-456789abcdef"))).SetName("Hydrate_ExternallyConstructedNonJsonTokensRefusedWithoutMutation_Guid");
            yield return new TestCaseData((object)new JValue(new Uri("https://example.invalid/"))).SetName("Hydrate_ExternallyConstructedNonJsonTokensRefusedWithoutMutation_Uri");
            yield return new TestCaseData((object)new JValue(TimeSpan.FromSeconds(1))).SetName("Hydrate_ExternallyConstructedNonJsonTokensRefusedWithoutMutation_TimeSpan");
            yield return new TestCaseData((object)new JValue("\ud800")).SetName("Hydrate_ExternallyConstructedNonJsonTokensRefusedWithoutMutation_UnpairedSurrogate");
        }

        [TestCaseSource(nameof(NonPlainValues))]
        public void Hydrate_ExternallyConstructedNonJsonTokensRefusedWithoutMutation(JToken bad)
        {
            var token = FrozenV29Input.ParseObject(Utf8(AllianceRow));
            token["why"] = bad;
            var before = new TreeWitness(token);
            Assert.Throws<InvalidDataException>(() => FrozenV29Input.HydrateComponentShapeOnly(token, "AllianceRow"));
            before.AssertUnchanged(token);
        }

        [Test]
        public void Hydrate_VirtualTokenSubclassIsNotInvoked()
        {
            var token = new ThrowingObject();
            token.Add("id", "a"); token.Add("why", JValue.CreateNull()); token.Add("startedWeek", 0); token.Add("endedWeek", 0);
            var before = new TreeWitness(token);
            Assert.Throws<InvalidDataException>(() => FrozenV29Input.HydrateComponentShapeOnly(token, "AllianceRow"));
            Assert.That(token.WriteCalled, Is.False);
            before.AssertUnchanged(token);
        }

        [Test]
        public void Hydrate_RepeatedAndConcurrentCallsHaveIndependentDetachedResults()
        {
            var token = FrozenV29Input.ParseObject(Utf8(Alliance));
            var before = new TreeWitness(token);
            var results = new Frozen29Data.AllianceState[32];
            var failures = new ConcurrentQueue<Exception>();
            Parallel.For(0, results.Length, i =>
            {
                try { results[i] = (Frozen29Data.AllianceState)FrozenV29Input.HydrateComponentShapeOnly(token, "AllianceState"); }
                catch (Exception error) { failures.Enqueue(error); }
            });
            Assert.That(failures, Is.Empty);
            for (int i = 0; i < results.Length; i++)
            {
                Assert.That(results[i].id, Is.EqualTo("a"));
                CollectionAssert.AreEqual(new[] { "second", null, "first", "second" }, results[i].members);
                for (int j = 0; j < i; j++)
                {
                    Assert.That(results[i], Is.Not.SameAs(results[j]));
                    Assert.That(results[i].members, Is.Not.SameAs(results[j].members));
                }
            }
            before.AssertUnchanged(token);
        }

        [Test]
        public void ParseAndHydrate_DoNotReadReplaceOrLeakGlobalDefaultSettings()
        {
            var previous = JsonConvert.DefaultSettings;
            int calls = 0;
            Func<JsonSerializerSettings> hostile = () =>
            {
                Interlocked.Increment(ref calls);
                return new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All,
                    ObjectCreationHandling = ObjectCreationHandling.Auto,
                    Culture = CultureInfo.GetCultureInfo("fr-FR"),
                    Converters = new List<JsonConverter> { new RejectEveryConverter() }
                };
            };
            try
            {
                JsonConvert.DefaultSettings = hostile;
                var token = FrozenV29Input.ParseObject(Utf8(Alliance));
                var before = new TreeWitness(token);
                var result = (Frozen29Data.AllianceState)FrozenV29Input.HydrateComponentShapeOnly(token, "AllianceState");
                Assert.That(result.id, Is.EqualTo("a"));
                Assert.That(calls, Is.EqualTo(0));
                Assert.That(JsonConvert.DefaultSettings, Is.SameAs(hostile));
                var bad = FrozenV29Input.ParseObject(Utf8(AllianceRow)); bad.Add("extra", 0);
                Assert.Throws<InvalidDataException>(() => FrozenV29Input.HydrateComponentShapeOnly(bad, "AllianceRow"));
                Assert.That(calls, Is.EqualTo(0));
                Assert.That(JsonConvert.DefaultSettings, Is.SameAs(hostile));
                before.AssertUnchanged(token);
            }
            finally { JsonConvert.DefaultSettings = previous; }
            Assert.That(JsonConvert.DefaultSettings, Is.SameAs(previous));
        }

        [Test]
        public void Hydrate_AnnotatedTreeSurvivesPassingDetachedHydration()
        {
            var token = FrozenV29Input.ParseObject(Utf8(AllianceRow));
            var property = token.Property("startedWeek");
            var child = property.Value;
            var rootMarker = new object();
            var propertyMarker = new object();
            var childMarker = new object();
            token.AddAnnotation(rootMarker);
            property.AddAnnotation(propertyMarker);
            child.AddAnnotation(childMarker);
            Assert.That(token.Annotations<object>(), Does.Contain(rootMarker));
            Assert.That(property.Annotations<object>(), Does.Contain(propertyMarker));
            Assert.That(child.Annotations<object>(), Does.Contain(childMarker));
            var before = new TreeWitness(token);

            var result = (Frozen29Data.AllianceRow)FrozenV29Input.HydrateComponentShapeOnly(token, "AllianceRow");

            Assert.That(result.id, Is.EqualTo("a"));
            Assert.That(result.startedWeek, Is.EqualTo(0));
            before.AssertUnchanged(token);
            Assert.That(token.Annotations<object>().Single(x => ReferenceEquals(x, rootMarker)), Is.SameAs(rootMarker));
            Assert.That(property.Annotations<object>().Single(x => ReferenceEquals(x, propertyMarker)), Is.SameAs(propertyMarker));
            Assert.That(child.Annotations<object>().Single(x => ReferenceEquals(x, childMarker)), Is.SameAs(childMarker));
        }

        [Test]
        public void Hydrate_AnnotatedTreeSurvivesExpectedShapeRefusal()
        {
            var token = FrozenV29Input.ParseObject(Utf8(AllianceRow));
            token.Add("unexpected", true);
            var property = token.Property("startedWeek");
            var child = property.Value;
            var rootMarker = new object();
            var propertyMarker = new object();
            var childMarker = new object();
            token.AddAnnotation(rootMarker);
            property.AddAnnotation(propertyMarker);
            child.AddAnnotation(childMarker);
            Assert.That(token.Annotations<object>(), Does.Contain(rootMarker));
            Assert.That(property.Annotations<object>(), Does.Contain(propertyMarker));
            Assert.That(child.Annotations<object>(), Does.Contain(childMarker));
            var before = new TreeWitness(token);

            Assert.Throws<InvalidDataException>(() => FrozenV29Input.HydrateComponentShapeOnly(token, "AllianceRow"));

            before.AssertUnchanged(token);
            Assert.That(token.Annotations<object>().Single(x => ReferenceEquals(x, rootMarker)), Is.SameAs(rootMarker));
            Assert.That(property.Annotations<object>().Single(x => ReferenceEquals(x, propertyMarker)), Is.SameAs(propertyMarker));
            Assert.That(child.Annotations<object>().Single(x => ReferenceEquals(x, childMarker)), Is.SameAs(childMarker));
        }

        [Test]
        public void Hydrate_AnnotatedTreeSurvivesHydrationExceptionUnwinding()
        {
            var token = FrozenV29Input.ParseObject(Utf8(AllianceRow));
            token["startedWeek"] = new JValue(long.MaxValue);
            var property = token.Property("startedWeek");
            var child = property.Value;
            var rootMarker = new object();
            var propertyMarker = new object();
            var childMarker = new object();
            token.AddAnnotation(rootMarker);
            property.AddAnnotation(propertyMarker);
            child.AddAnnotation(childMarker);
            Assert.That(token.Annotations<object>(), Does.Contain(rootMarker));
            Assert.That(property.Annotations<object>(), Does.Contain(propertyMarker));
            Assert.That(child.Annotations<object>(), Does.Contain(childMarker));
            var before = new TreeWitness(token);
            bool unwound = false;

            var error = Assert.Throws<InvalidDataException>(() =>
            {
                try
                {
                    // This is an Integer token, so literal shape accepts it. Conversion to
                    // the fixed Int32 field must throw inside the actual hydration boundary.
                    FrozenV29Shape.CheckComponent(token, "AllianceRow");
                    FrozenV29Input.HydrateComponentShapeOnly(token, "AllianceRow");
                }
                finally
                {
                    before.AssertUnchanged(token);
                    unwound = true;
                }
            });

            Assert.That(error.Message, Is.EqualTo("Frozen29 component hydration failed."));
            Assert.That(unwound, Is.True);
            Assert.That(token.Annotations<object>().Single(x => ReferenceEquals(x, rootMarker)), Is.SameAs(rootMarker));
            Assert.That(property.Annotations<object>().Single(x => ReferenceEquals(x, propertyMarker)), Is.SameAs(propertyMarker));
            Assert.That(child.Annotations<object>().Single(x => ReferenceEquals(x, childMarker)), Is.SameAs(childMarker));
        }

        private static byte[] Utf8(string text) { return new UTF8Encoding(false, true).GetBytes(text); }

        private sealed class ThrowingObject : JObject
        {
            internal bool WriteCalled;
            public override void WriteTo(JsonWriter writer, params JsonConverter[] converters)
            {
                WriteCalled = true;
                throw new InvalidOperationException("A virtual token writer must never be called.");
            }
        }

        private sealed class RejectEveryConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) { return true; }
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new InvalidOperationException("Global converters must never participate.");
            }
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new InvalidOperationException("Global converters must never participate.");
            }
        }

        // Iterative capture also supports the10,000-level refusal fixture. No ToString or clone
        // hides a changed identity/order/annotation, and no recursive witness precedes the guard.
        private sealed class TreeWitness
        {
            private readonly List<NodeWitness> nodes;
            internal TreeWitness(JToken root) { nodes = Walk(root).Select(x => new NodeWitness(x)).ToList(); }
            internal void AssertUnchanged(JToken root)
            {
                var after = Walk(root).ToList();
                Assert.That(after.Count, Is.EqualTo(nodes.Count));
                for (int i = 0; i < nodes.Count; i++) nodes[i].AssertUnchanged(after[i]);
            }
            private static IEnumerable<JToken> Walk(JToken root)
            {
                var current = root;
                while (true)
                {
                    yield return current;
                    var container = current as JContainer;
                    if (container != null && container.First != null) { current = container.First; continue; }
                    while (!ReferenceEquals(current, root) && current.Next == null) current = current.Parent;
                    if (ReferenceEquals(current, root)) yield break;
                    current = current.Next;
                }
            }
        }

        private sealed class NodeWitness
        {
            private readonly JToken token, parent, previous, next, first, last;
            private readonly JTokenType kind;
            private readonly object value;
            private readonly string name;
            private readonly object[] annotations;
            internal NodeWitness(JToken token)
            {
                this.token = token; parent = token.Parent; previous = token.Previous; next = token.Next;
                var container = token as JContainer;
                first = container == null ? null : container.First; last = container == null ? null : container.Last;
                kind = token.Type;
                var primitive = token as JValue; value = primitive == null ? null : primitive.Value;
                var property = token as JProperty; name = property == null ? null : property.Name;
                annotations = token.Annotations<object>().ToArray();
            }
            internal void AssertUnchanged(JToken actual)
            {
                Assert.That(actual, Is.SameAs(token)); Assert.That(actual.Type, Is.EqualTo(kind));
                Assert.That(actual.Parent, Is.SameAs(parent)); Assert.That(actual.Previous, Is.SameAs(previous)); Assert.That(actual.Next, Is.SameAs(next));
                var container = actual as JContainer;
                Assert.That(container == null ? null : container.First, Is.SameAs(first));
                Assert.That(container == null ? null : container.Last, Is.SameAs(last));
                var primitive = actual as JValue;
                Assert.That(primitive == null ? null : primitive.Value, Is.SameAs(value));
                var property = actual as JProperty;
                Assert.That(property == null ? null : property.Name, Is.EqualTo(name));
                var afterAnnotations = actual.Annotations<object>().ToArray();
                Assert.That(afterAnnotations.Length, Is.EqualTo(annotations.Length));
                for (int i = 0; i < annotations.Length; i++) Assert.That(afterAnnotations[i], Is.SameAs(annotations[i]));
            }
        }
    }
}
