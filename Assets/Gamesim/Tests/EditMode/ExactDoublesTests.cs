using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Gamesim.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
#if UNITY_EDITOR
using System.Reflection;
using System.Runtime.ExceptionServices;
using Gamesim.Simulation;
#endif

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// F9 (OLD-SAVE-ACCEPTANCE): under Unity's Mono, Json.NET's read of a float literal could give a
    /// double one unit off ("3.4694441460073", written for 0x400bc16beeb33334, read as ...35), and the
    /// stores, which re-hash the canonical form of what they read, refused the save, the load and the
    /// backup. <see cref="ExactDoubles"/> settles every float the checksummed stores read on a double the
    /// writer writes as the file's text, nearest first; the web import keeps Json.NET's read.
    ///
    /// <para>Shared with the dotnet subset, where the save store cases (UNITY_EDITOR) are left out and
    /// <see cref="Parse"/> stands in for SaveJson.ParseObject with the same reader and the same pass.
    /// .NET reads correctly, so there these hold the exact conversion against .NET's own parse; only
    /// the editor's Mono misreads, so only EditMode shows the pass repairing a read. Every expected
    /// bit pattern below was checked with Python's float(), which is correctly rounded.</para>
    /// </summary>
    public sealed class ExactDoublesTests
    {
        private const ulong F9 = 0x400bc16beeb33334UL;
        private const string F9Text = "3.4694441460073";

        // Mono still parses as the old .NET Framework did; .NET's parse is correctly rounded and is the oracle there.
        private static readonly bool RuntimeParsesExactly = Type.GetType("Mono.Runtime") == null;

        [Test]
        public void TheF9TextReadsAsTheDoubleItWasWrittenFor()
        {
            double written = Double(F9);
            Assert.That(JsonConvert.ToString(written), Is.EqualTo(F9Text), "the writer's text for the F9 double");
            var read = Parse("{\"x\":" + F9Text + "}");
            Assert.That(Bits((double)read["x"]), Is.EqualTo(F9));
            Assert.That(JsonConvert.ToString((double)read["x"]), Is.EqualTo(F9Text), "so its canonical form is the one hashed");

            // Mono's misread, one double over, made here on any runtime: the pass puts the written double back.
            string text = "{\"x\":" + F9Text + "}";
            var misread = ReadOnly(text);
            ((JValue)misread["x"]).Value = Double(F9 + 1);
            Assert.That(ExactDoubles.Correct(misread, text), Is.EqualTo(1));
            Assert.That(Bits((double)misread["x"]), Is.EqualTo(F9));
        }

        [TestCase(F9Text, 0x400bc16beeb33334UL)]
        [TestCase("-3.4694441460073", 0xc00bc16beeb33334UL)]
        [TestCase("3.0", 0x4008000000000000UL)]
        [TestCase("-0.0", 0x8000000000000000UL)]
        [TestCase("0.0", 0x0000000000000000UL)]
        [TestCase("1e-30", 0x39b4484bfeebc2a0UL)]
        [TestCase("-1e-30", 0xb9b4484bfeebc2a0UL)]
        [TestCase("4.9E-324", 0x0000000000000001UL)]
        [TestCase("2.4703282292062327e-324", 0x0000000000000000UL)]
        [TestCase("2.4703282292062328e-324", 0x0000000000000001UL)]
        [TestCase("2.2250738585072011e-308", 0x000fffffffffffffUL)]
        [TestCase("-2.2250738585072011e-308", 0x800fffffffffffffUL)]
        [TestCase("2.2250738585072014e-308", 0x0010000000000000UL)]
        [TestCase("-1.5e-310", 0x80001b9cd1295941UL)]
        [TestCase("1.7976931348623157E+308", 0x7fefffffffffffffUL)]
        [TestCase("1.7976931348623158e308", 0x7fefffffffffffffUL)]
        [TestCase("8.98846567431158e307", 0x7fe0000000000000UL)]
        [TestCase("1.234567890123456789012345678901234567890e39", 0x480d064903ae06e0UL)]
        [TestCase("0.1234567890123456789012345678901234567890", 0x3fbf9add3746f65fUL)]
        [TestCase("0.1000000000000000055511151231257827021181583404541015625", 0x3fb999999999999aUL)]
        [TestCase("1.00000000000000011102230246251565404236316680908203125", 0x3ff0000000000000UL)]
        [TestCase("1.00000000000000011102230246251565404236316680908203126", 0x3ff0000000000001UL)]
        [TestCase("9007199254740993.0", 0x4340000000000000UL)]
        [TestCase("9007199254740995.0", 0x4340000000000002UL)]
        [TestCase("0.30000000000000004", 0x3fd3333333333334UL)]
        [TestCase("1e23", 0x44b52d02c7e14af6UL)]
        [TestCase("1E+22", 0x4480f0cf064dd592UL)]
        [TestCase("123456.789e-3", 0x405edd3c07ee0b0bUL)]
        [TestCase(".5", 0x3fe0000000000000UL)]
        [TestCase("-.5", 0xbfe0000000000000UL)]
        [TestCase("1.", 0x3ff0000000000000UL)]
        public void AnEdgeTextReadsAsItsNearestDouble(string literal, ulong expected)
        {
            var read = Parse("{\"x\":" + literal + ",\"list\":[" + literal + "]}");
            Assert.That(Bits((double)read["x"]), Is.EqualTo(expected), "in an object, through the store's reader");
            Assert.That(Bits((double)read["list"][0]), Is.EqualTo(expected), "in an array, through the store's reader");
            Assert.That(ExactDoubles.TryParse(literal, out double exact), Is.True);
            Assert.That(Bits(exact), Is.EqualTo(expected), "the exact conversion");
            foreach (long step in new long[] { -5, -2, -1, 1, 2, 5 })
            {
                double start = BitConverter.Int64BitsToDouble((long)expected + step);
                if (double.IsNaN(start) || double.IsInfinity(start)) continue;
                Assert.That(ExactDoubles.TryParseFrom(literal, start, out double walked), Is.True, "from " + step);
                Assert.That(Bits(walked), Is.EqualTo(expected), "the exact walk from " + step + " doubles away");
            }
        }

        [TestCase("half the smallest double, exactly: a tie, to the even zero", 0x0000000000000000UL)]
        [TestCase("a digit past half the smallest double", 0x0000000000000001UL)]
        [TestCase("a digit short of half the smallest double", 0x0000000000000000UL)]
        [TestCase("one and half an ulp, exactly: a tie, to the even one", 0x3ff0000000000000UL)]
        [TestCase("the tie, then 800 zeros and a 1", 0x3ff0000000000001UL)]
        [TestCase("the tie, then 800 zeros", 0x3ff0000000000000UL)]
        [TestCase("0.1 behind 400 zeros, scaled back", 0x3fb999999999999aUL)]
        [TestCase("1 and a thousand zeros, scaled back", 0x3ff0000000000000UL)]
        public void ALongLiteralRoundsOnEveryDigit(string name, ulong expected)
        {
            string tie = "1.00000000000000011102230246251565404236316680908203125";
            var half = BigInteger.Pow(5, 1075);
            string literal = name.StartsWith("half") ? half + "e-1075"
                : name.StartsWith("a digit past") ? half * 10 + 1 + "e-1076"
                : name.StartsWith("a digit short") ? half * 10 - 1 + "e-1076"
                : name.StartsWith("one and half") ? (BigInteger.Pow(2, 53) + 1) * BigInteger.Pow(5, 53) + "e-53"
                : name.EndsWith("and a 1") ? tie + new string('0', 800) + "1"
                : name.EndsWith("800 zeros") ? tie + new string('0', 800)
                : name.StartsWith("0.1") ? "0." + new string('0', 400) + "1e400"
                : "1" + new string('0', 1000) + "e-1000";
            Assert.That(ExactDoubles.TryParse(literal, out double exact), Is.True);
            Assert.That(Bits(exact), Is.EqualTo(expected), "the exact conversion of " + literal.Length + " characters");
            Assert.That(Bits((double)Parse("{\"x\":" + literal + "}")["x"]), Is.EqualTo(expected), "through the store's reader");
        }

        /// <summary>
        /// The writer's text for each of 50,000 seeded doubles, in an object and in an array, reads back
        /// as that double. Under Mono today's read misses about one product of short decimals in 4,000
        /// (F9); and Mono's writer gives about one double in 2,000 a text whose nearest double is another,
        /// so a read of the nearest double alone would miss those.
        /// </summary>
        [TestCase("uniform bit patterns")]
        [TestCase("magnitudes 1e-6 to 1e6")]
        [TestCase("rounded to 1 to 14 places")]
        [TestCase("products of short decimals")]
        public void EveryWrittenDoubleReadsBackAsItself(string sweep)
        {
            var random = new SplitMix(0x5eed0000UL + (ulong)sweep.Length);
            var values = new List<double>();
            while (values.Count < 50000)
            {
                double value;
                switch (sweep)
                {
                    case "uniform bit patterns": value = BitConverter.Int64BitsToDouble((long)random.Next()); break;
                    case "magnitudes 1e-6 to 1e6": value = Math.Pow(10, -6 + 12 * random.Unit()) * (random.Next() % 2 == 0 ? 1 : -1); break;
                    case "rounded to 1 to 14 places": value = Math.Round(-100 + 200 * random.Unit(), 1 + (int)(random.Next() % 14)); break;
                    default: value = Math.Round(10 * random.Unit(), 2) * Math.Round(random.Unit(), 3) * (1 + Math.Round(random.Unit(), 1)); break;
                }
                // Mono's writer drops the sign of zero, so a negative zero cannot come back from its text.
                if (double.IsNaN(value) || double.IsInfinity(value) || value == 0) continue;
                values.Add(value);
            }

            var texts = values.Select(value => JsonConvert.ToString(value)).ToList();
            var json = new StringBuilder("{\"values\":{");
            for (int i = 0; i < texts.Count; i++) json.Append(i == 0 ? "" : ",").Append("\"v").Append(i).Append("\":").Append(texts[i]);
            json.Append("},\"list\":[").Append(string.Join(",", texts)).Append("]}");
            var read = Parse(json.ToString());
            var inObject = (JObject)read["values"];
            var inArray = (JArray)read["list"];

            var examples = new List<string>();
            int misses = 0, canonicalChanges = 0;
            for (int i = 0; i < values.Count; i++)
            {
                foreach (var token in new[] { inObject["v" + i], inArray[i] })
                {
                    double back = (double)token;
                    if (JsonConvert.ToString(back) != texts[i]) canonicalChanges++;
                    if (Bits(back) == Bits(values[i])) continue;
                    if (++misses <= 5) examples.Add(texts[i] + " written for " + Bits(values[i]).ToString("x16") + ", read as " + Bits(back).ToString("x16"));
                }
            }
            Assert.That(misses, Is.Zero, misses + " of " + 2 * values.Count + " reads missed, " + canonicalChanges
                + " with another canonical text (a checksum refusal): " + string.Join("; ", examples));
        }

        /// <summary>
        /// The float fields the stores checksum (the appearance values, in a save and in a houseguest
        /// profile) are written with the float's own text and read back as doubles. Each of 50,000 seeded
        /// floats comes back with the text it was written with and as the same float. Under Mono today's
        /// read changes the canonical text of a few in 50,000, each a checksum refusal. Floats of 1e7 and
        /// over are left out: the float writer writes them in E notation ("-2.770227E+10") and the double
        /// writer does not, which no reader can mend, and the game clamps appearance values to [0, 1].
        /// </summary>
        [TestCase("magnitudes 1e-3 to 1e3")]
        [TestCase("unit rounded 1-6 places")]
        [TestCase("products of short decimals")]
        public void EveryWrittenFloatReadsBackAsItself(string sweep)
        {
            var random = new SplitMix(0xF10A7000UL + (ulong)sweep.Length);
            var floats = new List<float>();
            while (floats.Count < 50000)
            {
                float value;
                switch (sweep)
                {
                    case "magnitudes 1e-3 to 1e3": value = (float)(Math.Pow(10, -3 + 6 * random.Unit()) * (random.Next() % 2 == 0 ? 1 : -1)); break;
                    case "unit rounded 1-6 places": value = (float)Math.Round(random.Unit(), 1 + (int)(random.Next() % 6)); break;
                    default: value = (float)(Math.Round(10 * random.Unit(), 2) * Math.Round(random.Unit(), 3)); break;
                }
                if (float.IsNaN(value) || float.IsInfinity(value) || value == 0 || Math.Abs(value) >= 1e7f) continue;
                floats.Add(value);
            }

            // As the stores write a float field: JObject.FromObject keeps the float, and its text is the float's.
            var written = JObject.FromObject(new FloatRow { v = floats.ToArray() });
            var read = Parse(written.ToString(Formatting.None));
            var writtenValues = (JArray)written["v"];
            var readValues = (JArray)read["v"];
            var examples = new List<string>();
            int misses = 0;
            for (int i = 0; i < floats.Count; i++)
            {
                string text = writtenValues[i].ToString(Formatting.None), back = readValues[i].ToString(Formatting.None);
                if (back == text && (float)(double)readValues[i] == floats[i]) continue;
                if (++misses <= 5) examples.Add(text + " read back as " + back);
            }
            Assert.That(misses, Is.Zero, misses + " of " + floats.Count + " floats came back with another canonical text"
                + " or another float (a checksum refusal): " + string.Join("; ", examples));
        }

        private sealed class FloatRow
        {
            public float[] v;
        }

        [Test]
        public void TheExactWalkEndsOnTheNearestDoubleFromEitherSide()
        {
            var random = new SplitMix(0x3146UL);
            int walks = 0;
            for (int i = 0; i < 4000; i++)
            {
                double value = i % 2 == 0 ? BitConverter.Int64BitsToDouble((long)random.Next()) : Math.Pow(10, -6 + 12 * random.Unit());
                if (double.IsNaN(value) || double.IsInfinity(value)) continue;
                string text = JsonConvert.ToString(value);
                Assert.That(ExactDoubles.TryParse(text, out double nearest), Is.True, text);
                if (RuntimeParsesExactly)
                    Assert.That(Bits(nearest), Is.EqualTo(Bits(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture))),
                        "the runtime's own parse is correctly rounded here: " + text);
                foreach (long step in new long[] { -3, -1, 1, 3 })
                {
                    double start = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(nearest) + step);
                    if (double.IsNaN(start) || double.IsInfinity(start)) continue;
                    Assert.That(ExactDoubles.TryParseFrom(text, start, out double walked), Is.True, text);
                    Assert.That(Bits(walked), Is.EqualTo(Bits(nearest)), text + " from " + step);
                    walks++;
                }
            }
            Assert.That(walks, Is.GreaterThan(15000));
        }

        /// <summary>
        /// Json.NET reads more than JSON: comments, single quotes, unquoted names, NaN and the
        /// infinities, octal and hexadecimal integers, constructors. The scan pairs through all of it:
        /// every float, read one double off, comes back; nothing else moves.
        /// </summary>
        [Test]
        public void TheFormsJsonNetReadsBeyondJsonPairAsWritten()
        {
            const string text = "{ /* a comment, 9.5 */ 'quoted': 'it\\'s 1.5, \"2.5\" // not a comment', unquoted: 0.1, // a line, 7.5\n"
                + " \"list\": [017, 0x1F, " + F9Text + ", -0.0, 2.5, NaN, Infinity, -Infinity, true, false, null, undefined, 12345678901234567890123],\n"
                + " \"made\": new Date(1, 1E5), name_$2: -4.9E-324, \"last\": 2 }";
            var exact = new Dictionary<string, ulong>
            {
                ["unquoted"] = 0x3fb999999999999aUL, ["list[2]"] = F9, ["list[3]"] = 0x8000000000000000UL, ["list[4]"] = 0x4004000000000000UL,
                ["made[1]"] = 0x40f86a0000000000UL, ["name_$2"] = 0x8000000000000001UL
            };
            var read = ReadOnly(text);
            var floats = read.Descendants().OfType<JValue>().Where(value => value.Type == JTokenType.Float).ToList();
            Assert.That(floats.Select(value => value.Path), Is.EqualTo(new[]
                { "unquoted", "list[2]", "list[3]", "list[4]", "list[5]", "list[6]", "list[7]", "made[1]", "name_$2" }));
            var integers = read.Descendants().OfType<JValue>().Where(value => value.Type == JTokenType.Integer).Select(value => value.ToString()).ToList();
            Assert.That(integers, Is.EqualTo(new[] { "15", "31", "12345678901234567890123", "1", "2" }));

            foreach (var value in floats.Where(value => exact.ContainsKey(value.Path)))
                value.Value = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits((double)value.Value) + 1);
            Assert.That(ExactDoubles.Correct(read, text), Is.EqualTo(exact.Count));
            foreach (var pair in exact) Assert.That(Bits((double)read.SelectToken(pair.Key)), Is.EqualTo(pair.Value), pair.Key);
            Assert.That(double.IsNaN((double)read["list"][5]) && double.IsPositiveInfinity((double)read["list"][6])
                && double.IsNegativeInfinity((double)read["list"][7]), Is.True, "NaN and the infinities stay for validation to refuse");
            Assert.That(read["quoted"].ToString(), Is.EqualTo("it's 1.5, \"2.5\" // not a comment"));
            Assert.That(read.Descendants().OfType<JValue>().Where(value => value.Type == JTokenType.Integer).Select(value => value.ToString()),
                Is.EqualTo(integers));
        }

        /// <summary>
        /// When the text and the values do not pair one to one, a literal is not a decimal number, or
        /// one lies far from its value, every value stays as Json.NET read it - the one the pass could
        /// have put back too. A text Json.NET reads and the scan does not follow (a NUL between tokens)
        /// keeps Json.NET's read.
        /// </summary>
        [Test]
        public void ATextThatDoesNotPairLeavesEveryValueAsRead()
        {
            string good = "{\"x\":" + F9Text + ",\"y\":1.5,\"n\":2}";
            foreach (var other in new[]
            {
                "{\"x\":" + F9Text + ",\"y\":1.5}",
                "{\"x\":" + F9Text + ",\"y\":1.5,\"n\":2,\"z\":3}",
                "{\"x\":" + F9Text + ",\"y\":NaN,\"n\":2}",
                "{\"x\":" + F9Text + "e,\"y\":1.5,\"n\":2}",
                "{\"x\":" + F9Text + ",\"y\":9.5,\"n\":2}",
                "{\"x\":" + F9Text + ",\"y\":1.5,\"n\":2.0}",
                "{\"x\":\"" + F9Text + "\",\"y\":1.5,\"n\":2,\"z\":3}",
                "{\"x\":" + F9Text + ",\"y\":1.5,\"n\":2",
                "{\"x\":" + F9Text + ",\"y\":1.5,\"n\":2} /* unclosed",
                "{\"x\":\u0000" + F9Text + ",\"y\":1.5,\"n\":2}",
            })
            {
                var read = ReadOnly(good);
                ((JValue)read["x"]).Value = Double(F9 + 1);
                Assert.That(ExactDoubles.Correct(read, other), Is.EqualTo(-1), other);
                Assert.That(Bits((double)read["x"]), Is.EqualTo(F9 + 1), "left as read against " + other);
                Assert.That(Bits((double)read["y"]), Is.EqualTo(Bits(1.5)));
            }
            var paired = ReadOnly(good);
            ((JValue)paired["x"]).Value = Double(F9 + 1);
            Assert.That(ExactDoubles.Correct(paired, good), Is.EqualTo(1), "the same values pair with their own text");
            Assert.That(Bits((double)paired["x"]), Is.EqualTo(F9));

            const string nul = "{\"x\":\u00001.5}";
            Assert.That(Bits((double)Parse(nul)["x"]), Is.EqualTo(Bits(1.5)), "Json.NET reads a NUL between tokens; the store still does");
        }

#if UNITY_EDITOR
        private string directory;

        [SetUp]
        public void CreateTheSlotDirectory()
        {
            directory = Path.Combine(Path.GetTempPath(), "GamesimExactDoublesTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void RemoveTheSlotDirectory()
        {
            if (directory == null) return;
            var resolved = Path.GetFullPath(directory);
            Assert.That(resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(resolved), Does.StartWith("GamesimExactDoublesTests-"));
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }

        /// <summary>
        /// F9's own failure: a season holding the double was refused by Save's read-back, by a load and
        /// by the backup's recovery. Every relationship score is a seeded double besides.
        /// </summary>
        [Test]
        public void ASaveHoldingTheF9DoubleSavesLoadsAndRecoversWithItsBits()
        {
            var store = new EpisodeSaveStore(Path.Combine(directory, "episode.json"));
            var first = StateWithF9(3146);
            store.Save(first);
            Assert.That(store.TryLoad(out var loaded, out var message), Is.True, message);
            AssertScores(first, loaded, "loaded");

            var second = first.Clone();
            second.revision = first.revision + 1;
            second.relationships[1].score = Double(F9);
            store.Save(second);
            Assert.That(store.TryLoad(out var reloaded, out message), Is.True, message);
            AssertScores(second, reloaded, "the second save");

            File.WriteAllText(store.SavePath, "interrupted external write");
            Assert.That(store.TryLoad(out _, out message), Is.False);
            Assert.That(message, Does.Contain("Recover backup"));
            Assert.That(store.TryRecoverBackup(out var recovered, out message), Is.True, message);
            AssertScores(first, recovered, "recovered from the backup");
        }

        /// <summary>
        /// A save file a hand edited with comments, single quotes and an unquoted name loads as it did,
        /// its doubles settled as the writer wrote them.
        /// </summary>
        [Test]
        public void ASaveWithCommentsAndSingleQuotesStillLoadsWithItsBits()
        {
            var store = new EpisodeSaveStore(Path.Combine(directory, "episode.json"));
            var state = StateWithF9(17);
            store.Save(state);
            string text = File.ReadAllText(store.SavePath);
            string edited = text
                .Replace("\"format\": \"gamesim-unity-save\"", "/* a hand edit */ 'format': 'gamesim-unity-save' // the format\n")
                .Replace("\"version\": 1", "version: 1");
            Assert.That(edited, Does.Contain("'format': 'gamesim-unity-save'").And.Contain("/* a hand edit */").And.Contain("version: 1"));
            File.WriteAllText(store.SavePath, edited);
            Assert.That(store.TryLoad(out var loaded, out var message), Is.True, message);
            AssertScores(state, loaded, "loaded from the edited file");
        }

        /// <summary>A save the scan does not follow (a NUL Json.NET skips) loads as Json.NET reads it, as before.</summary>
        [Test]
        public void ASaveTheScanCannotFollowLoadsAsJsonNetReadsIt()
        {
            var store = new EpisodeSaveStore(Path.Combine(directory, "episode.json"));
            var state = ContentCatalog.Create(17);
            store.Save(state);
            string edited = File.ReadAllText(store.SavePath).Replace("\"format\": \"gamesim-unity-save\"", "\"format\": \u0000\"gamesim-unity-save\"");
            Assert.That(edited, Does.Contain("\u0000"));
            File.WriteAllText(store.SavePath, edited);
            Assert.That(ExactDoubles.Correct(ReadOnly(edited), edited), Is.EqualTo(-1));
            Assert.That(store.TryLoad(out var loaded, out var message), Is.True, message);
            Assert.That(loaded.relationships.Select(r => Bits(r.score)), Is.EqualTo(state.relationships.Select(r => Bits(r.score))));
        }

        /// <summary>
        /// The web import reads a file as Json.NET reads it (SaveJson.ParseObjectAsRead). A web file has no
        /// checksum for the settle step to serve, and under Mono that step would move "80.95655" and
        /// "46.5564", which Mono reads as their nearest doubles, to the neighbours Mono's writer gives those
        /// texts. The store then saves and loads the imported scores with their bits.
        /// </summary>
        [Test]
        public void AWebImportKeepsTheDoublesJsonNetReads()
        {
            var ids = new[] { "external-player", "external-maya", "external-taylor", "external-jamie", "external-casey", "external-riley" };
            var cast = ContentCatalog.Create(13).contestants;
            var guests = new JArray(cast.Select((actor, index) => new JObject
            {
                ["id"] = ids[index], ["name"] = actor.name, ["isPlayer"] = index == 0, ["status"] = "Active",
                ["stats"] = JObject.FromObject(actor.stats), ["traits"] = new JArray(actor.traits),
                ["competitionsWon"] = new JObject { ["hoh"] = 0, ["pov"] = 0, ["other"] = 0 },
                ["nominations"] = new JObject { ["times"] = 0, ["receivedOn"] = new JArray() }
            }));
            // The literals as the web writes them, the shortest texts of their doubles (checked with Python's float()).
            var document = new JObject
            {
                ["format"] = "gamesim-save", ["version"] = 1, ["savedAt"] = "2026-09-10T00:00:00.000Z",
                ["state"] = new JObject
                {
                    ["phase"] = "SocialInteraction", ["week"] = 1, ["houseguests"] = guests,
                    ["relationships"] = new JObject
                    {
                        [ids[0]] = new JObject { [ids[1]] = new JObject { ["score"] = new JRaw("80.95655") } },
                        [ids[1]] = new JObject { [ids[0]] = new JObject { ["score"] = new JRaw("46.5564") } }
                    }
                }
            };
            string json = document.ToString(Formatting.Indented);
            Assert.That(json, Does.Contain("\"score\": 80.95655").And.Contain("\"score\": 46.5564"));
            var asRead = ReadOnly(json)["state"]["relationships"];
            ulong first = Bits((double)asRead[ids[0]][ids[1]]["score"]), second = Bits((double)asRead[ids[1]][ids[0]]["score"]);

            Assert.That(WebSaveImporter.TryImport(json, Path.Combine(directory, "imports"), out var imported, out var message), Is.True, message);
            Assert.That(Bits(imported.Score(ids[0], ids[1])), Is.EqualTo(first), "the import keeps Json.NET's read of 80.95655");
            Assert.That(Bits(imported.Score(ids[1], ids[0])), Is.EqualTo(second), "the import keeps Json.NET's read of 46.5564");
            Assert.That((first, second), Is.EqualTo((0x40543d381d7dbf48UL, 0x404747381d7dbf48UL)), "the nearest doubles, the web's values");

            var store = new EpisodeSaveStore(Path.Combine(directory, "episode.json"));
            store.Save(imported);
            Assert.That(store.TryLoad(out var loaded, out message), Is.True, message);
            Assert.That((Bits(loaded.Score(ids[0], ids[1])), Bits(loaded.Score(ids[1], ids[0]))), Is.EqualTo((first, second)),
                "saved and loaded with their bits");
        }

        private static EpisodeState StateWithF9(uint seed)
        {
            var state = ContentCatalog.Create(seed);
            var random = new SplitMix(seed);
            foreach (var relationship in state.relationships)
                relationship.score = Math.Round(-100 + 200 * random.Unit(), (int)(random.Next() % 15));
            state.relationships[0].score = Double(F9);
            return state;
        }

        private static void AssertScores(EpisodeState saved, EpisodeState read, string what)
        {
            Assert.That(read.relationships.Count, Is.EqualTo(saved.relationships.Count));
            for (int i = 0; i < saved.relationships.Count; i++)
                Assert.That(JsonConvert.ToString(read.relationships[i].score), Is.EqualTo(JsonConvert.ToString(saved.relationships[i].score)),
                    what + ": relationship " + i);
            Assert.That(Bits(read.relationships[0].score), Is.EqualTo(F9), what);
        }

        // The store's own reader, SaveJson.ParseObject (internal to Gamesim.Runtime), with the pass in it.
        private static readonly MethodInfo SaveJsonParse = typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("ParseObject", BindingFlags.Public | BindingFlags.Static);

        private static JObject Parse(string json)
        {
            try { return (JObject)SaveJsonParse.Invoke(null, new object[] { json }); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
        }
#else
        // The subset has no store: SaveJson.ParseObject's reader and its pass.
        private static JObject Parse(string json)
        {
            var read = ReadOnly(json);
            ExactDoubles.Correct(read, json);
            return read;
        }
#endif

        // Json.NET alone, as SaveJson.ParseObject reads before its pass.
        private static JObject ReadOnly(string json)
        {
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 })
            {
                var token = JToken.ReadFrom(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (!(token is JObject result) || reader.Read()) throw new InvalidDataException("Expected one JSON object.");
                return result;
            }
        }

        private static ulong Bits(double value) => (ulong)BitConverter.DoubleToInt64Bits(value);
        private static double Double(ulong bits) => BitConverter.Int64BitsToDouble((long)bits);

        // SplitMix64: the same seeded doubles on every runtime.
        private sealed class SplitMix
        {
            private ulong state;
            public SplitMix(ulong seed) { state = seed; }
            public ulong Next()
            {
                state += 0x9e3779b97f4a7c15UL;
                ulong z = state;
                z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9UL;
                z = (z ^ (z >> 27)) * 0x94d049bb133111ebUL;
                return z ^ (z >> 31);
            }
            public double Unit() => (Next() >> 11) * (1.0 / (1UL << 53));
        }
    }
}
