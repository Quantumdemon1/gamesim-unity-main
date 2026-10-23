// RETIRED 2026-10-22, kept for the record, not run: a diagnostic of the interim schema-28 landing that pins the old
// 56-row manifest and its hash. See Assets/Plans/OLD-SAVE-ACCEPTANCE.md section 7. The !UNITY_5_3_OR_NEWER clause
// keeps it out of every Unity compile, diagnostic runs included, since the batch runner executes [Explicit] tests.
#if UNITY_EDITOR && GAMESIM_DIAGNOSTICS && !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>Diagnostic-only phase localization, not a store cost series or latency acceptance.</summary>
    // Unity's NUnit 3.5 fork supports explicit None, not NonParallelizableAttribute.
    [Parallelizable(ParallelScope.None)]
    public sealed class Frozen28PhasePilotDiagnostics
    {
        private const string ManifestHash = "7cda829f77790dcfa6bb9d11667ef7987a54dc2ad71f29a0ea352fe8cd42c9fd";
        private const double CooperativeSeconds = 90;
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        [TestCase("s28-mode2-allweek-w3-campaign")]
        [TestCase("s28-mode1-finished")]
        [Explicit("Thirty phase observations over two retained fixtures; unchanged native timeout; no store writes or latency SLA.")]
        public void ObserveFrozenReaderPhasesWithoutChangingAdmission(string id)
        {
            string run = Guid.NewGuid().ToString("N");
            var wall = Stopwatch.StartNew();
            string fixtureRoot = Path.Combine(Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "OldSaves");
            string manifestPath = Path.Combine(fixtureRoot, "manifest.json");
            byte[] manifestBytes = ReadBounded(manifestPath);
            Assert.That(Hash(manifestBytes), Is.EqualTo(ManifestHash), "Re-review a changed retained fixture matrix.");
            var rows = ((JArray)Parse(manifestBytes)["episodes"]).Cast<JObject>().ToArray();
            Assert.That(rows.Length, Is.EqualTo(56));
            var largest = rows.OrderByDescending(row => (long)row["saveBytes"])
                .ThenBy(row => (string)row["id"], StringComparer.Ordinal).First();
            Assert.That((string)largest["id"], Is.EqualTo("s28-mode1-finished"));
            Assert.That(id, Is.EqualTo("s28-mode2-allweek-w3-campaign").Or.EqualTo("s28-mode1-finished"));
            var row = rows.Single(item => (string)item["id"] == id);
            Assert.That((string)row["save"], Is.EqualTo(id + ".save.json"));
            string fixturePath = Path.Combine(fixtureRoot, id + ".save.json");
            byte[] fixtureBytes = ReadBounded(fixturePath);
            Assert.That(fixtureBytes.LongLength, Is.EqualTo((long)row["saveBytes"]));
            string fixtureHash = Hash(fixtureBytes);
            Assert.That(fixtureHash, Is.EqualTo((string)row["saveSha256"]));
            var original = (JObject)Parse(fixtureBytes)["state"];
            Assert.That((int)original["schemaVersion"], Is.EqualTo(28));
            Assert.That((int)original["unifiedCommitmentRulesVersion"], Is.EqualTo(id.Contains("mode2") ? 2 : 1));
            string originalText = original.ToString(Formatting.None);
            var sourceBefore = SourceImage();

            var assembly = typeof(EpisodeSaveStore).Assembly;
            var input = assembly.GetType("Gamesim.Persistence.Frozen28Components.FrozenV28Input", true);
            var shape = assembly.GetType("Gamesim.Persistence.Frozen28Components.FrozenV28Shape", true);
            var frozenType = assembly.GetType("Gamesim.Persistence.Frozen28Data.EpisodeState", true);
            var semantic = assembly.GetType("Gamesim.Persistence.Frozen28ReadOnly.EpisodeSaveValidation", true);
            var whole = assembly.GetType("Gamesim.Persistence.FrozenEpisodeV28", true);
            var budgetMethod = Bind(input, "CheckTokenBudget", typeof(JToken));
            var shapeMethod = Bind(shape, "CheckComponent", typeof(JToken), typeof(string));
            var hydrateMethod = Bind(input, "HydrateRootShapeOnly", typeof(JObject));
            var semanticMethod = Bind(semantic, "Validate", frozenType);
            var wholeMethod = Bind(whole, "Validate", typeof(JObject));

            Emit(new JObject
            {
                ["kind"] = "environment", ["run"] = run, ["fixture"] = id,
                ["unity"] = Application.unityVersion, ["runtime"] = Environment.Version.ToString(),
                ["runtimeMvid"] = assembly.ManifestModule.ModuleVersionId.ToString(),
                ["testMvid"] = typeof(Frozen28PhasePilotDiagnostics).Assembly.ManifestModule.ModuleVersionId.ToString(),
                ["newtonsoftVersion"] = typeof(JObject).Assembly.GetName().Version.ToString(),
                ["manifestSha256"] = ManifestHash, ["fixtureSha256"] = fixtureHash,
                ["fixtureBytes"] = fixtureBytes.Length, ["sourceSha256"] = JObject.FromObject(sourceBefore),
                ["stopwatchFrequency"] = Stopwatch.Frequency, ["cooperativeSeconds"] = CooperativeSeconds,
                ["coldProcess"] = false, ["allocationMeasured"] = false,
                ["storeOperations"] = 0, ["expectedSamplesThisLeaf"] = 15,
                ["interpretation"] = "Overlapping phase localization only; no baseline, percentile, latency or standalone acceptance."
            });

            int samples = 0;
            try
            {
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    // Actual whole-reader approval always precedes the isolated phases. It is a
                    // measured operation, not a hidden preflight. No phase changes production wiring.
                    Observe("FullFrozenValidate", repeat, () => Invoke(wholeMethod, original));
                    Observe("CheckTokenBudget", repeat, () => Invoke(budgetMethod, original));
                    Observe("Shape", repeat, () => Invoke(shapeMethod, original, "EpisodeState"));
                    object hydrated = Observe("HydrateRootShapeOnly", repeat, () => Invoke(hydrateMethod, original));
                    Assert.That(hydrated, Is.Not.Null);
                    Assert.That(hydrated.GetType(), Is.EqualTo(frozenType));
                    Assert.That((int)frozenType.GetField("schemaVersion").GetValue(hydrated), Is.EqualTo(28));
                    Observe("FrozenSemanticValidate", repeat, () => Invoke(semanticMethod, hydrated));
                    Assert.That(original.ToString(Formatting.None), Is.EqualTo(originalText), "Reader phases must not edit original token fields/order.");
                }
                Assert.That(samples, Is.EqualTo(15));
                Bound("final-identity");
            }
            finally
            {
                Assert.That(Hash(ReadBounded(manifestPath)), Is.EqualTo(ManifestHash));
                Assert.That(ReadBounded(fixturePath), Is.EqualTo(fixtureBytes), "Original retained save is read-only.");
                Assert.That(original.ToString(Formatting.None), Is.EqualTo(originalText));
                var after = SourceImage();
                Assert.That(after.Keys, Is.EqualTo(sourceBefore.Keys));
                foreach (var entry in sourceBefore) Assert.That(after[entry.Key], Is.EqualTo(entry.Value), entry.Key);
            }
            Bound("complete");
            Emit(new JObject
            {
                ["kind"] = "complete", ["run"] = run, ["fixture"] = id, ["samples"] = samples,
                ["sourceAndFixtureChecks"] = "Passed", ["storeOperations"] = 0,
                ["interpretation"] = "Phase observations only; unchanged native deadline and full correctness gates still apply."
            });

            object Observe(string phase, int repeat, Func<object> action)
            {
                Bound(phase);
                object result = null;
                Exception failure = null;
                var watch = Stopwatch.StartNew();
                try { result = action(); }
                catch (Exception error) { failure = error; }
                finally { watch.Stop(); }
                double ms = watch.ElapsedTicks * 1000.0 / Stopwatch.Frequency;
                Assert.That(double.IsNaN(ms) || double.IsInfinity(ms) || ms < 0, Is.False);
                Emit(new JObject
                {
                    ["kind"] = "sample", ["run"] = run, ["fixture"] = id, ["phase"] = phase,
                    ["repeat"] = repeat, ["observation"] = repeat == 0 ? "first-observed-not-cold" : "repeat",
                    ["elapsedTicks"] = watch.ElapsedTicks, ["milliseconds"] = ms,
                    ["returnedNormally"] = failure == null, ["exceptionType"] = failure?.GetType().FullName,
                    ["timedRegion"] = "One pre-bound reflection invocation; no assertions, hashes, logging, fixture I/O or reset."
                });
                samples++;
                if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
                Bound(phase + "-returned");
                return result;
            }
            void Bound(string phase)
            {
                if (wall.Elapsed.TotalSeconds < CooperativeSeconds) return;
                Emit(new JObject { ["kind"] = "bound-reached", ["run"] = run, ["fixture"] = id,
                    ["phase"] = phase, ["completedSamples"] = samples, ["cooperativeSeconds"] = CooperativeSeconds });
                Assert.Fail("Phase pilot reached its 90-second cooperative leaf budget; partial observations are not a complete pilot.");
            }
        }

        private static MethodInfo Bind(Type type, string name, params Type[] arguments) =>
            type.GetMethod(name, Static, null, arguments, null) ?? throw new MissingMethodException(type.FullName, name);
        private static object Invoke(MethodInfo method, params object[] arguments)
        {
            try { return method.Invoke(null, arguments); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
        private static byte[] ReadBounded(string path)
        {
            Assert.That(new FileInfo(path).Length, Is.InRange(1L, 8L * 1024 * 1024));
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(bytes.LongLength, Is.InRange(1L, 8L * 1024 * 1024));
            return bytes;
        }
        private static JObject Parse(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes, false);
            using var text = new StreamReader(stream, Encoding.UTF8, true);
            using var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Double, Culture = CultureInfo.InvariantCulture, MaxDepth = 64 };
            return JObject.Load(reader);
        }
        private static SortedDictionary<string, string> SourceImage()
        {
            // Reuse the already-reviewed exact18 source inventory; this is data access only and
            // does not invoke the existing 972-call store-cost test or change its protocol.
            var field = typeof(Frozen28SaveStoreCostDiagnostics).GetField("FrozenFiles", Static);
            Assert.That(field, Is.Not.Null);
            var names = (string[])field.GetValue(null);
            Assert.That(names, Has.Length.EqualTo(18));
            var relative = names.Select(name => "Gamesim/Runtime/Persistence/" + name + ".cs")
                .Concat(new[] { "Gamesim/Tests/EditMode/Frozen28SaveStoreCostDiagnostics.cs",
                    "Gamesim/Tests/EditMode/Frozen28PhasePilotDiagnostics.cs" });
            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (string path in relative) result.Add(path, Hash(ReadBounded(Path.Combine(Application.dataPath, path))));
            return result;
        }
        private static string Hash(byte[] bytes)
        { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static void Emit(JObject value) => TestContext.Out.WriteLine("FROZEN28_PHASE_PILOT " + value.ToString(Formatting.None));
    }
}
#endif
