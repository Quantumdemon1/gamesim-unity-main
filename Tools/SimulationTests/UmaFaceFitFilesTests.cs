using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Gamesim.Uma.Editor;
using NUnit.Framework;

namespace Gamesim.Tools.Tests
{
    /// <summary>
    /// The face-fit export's engine-free half (UmaFaceFitFiles, UmaFaceFitPlan, UmaFaceFitWork): the .npy and .npz bytes,
    /// the JSON, the job's checks, the sample plan, write-once publishing and resume, and a whole sweep assembled from
    /// captures made up here. Unity never compiles this file (it is outside Assets); the Unity half is pinned by
    /// UmaFaceFitExportPlayModeTests.
    ///
    /// <para>With GAMESIM_FACEFIT_PROOF_DIR set, <see cref="ASweepAssembles_FromFinishedSamples"/> leaves its export (and
    /// a plain .npy and .npz) in that folder so numpy can load them by hand; without it, everything goes in a temporary
    /// folder that is deleted.</para>
    /// </summary>
    [TestFixture]
    public sealed class UmaFaceFitFilesTests
    {
        private string folder;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "gamesim-facefit-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }

        private static string Proof => Environment.GetEnvironmentVariable("GAMESIM_FACEFIT_PROOF_DIR");

        // ------------------------------------------------------------------ .npy and .npz

        [Test]
        public void AnNpyHeader_IsNumpysFormatOne_PaddedTo64Bytes()
        {
            var bytes = NpyArray.Float32(new[] { 1f, 2f, 3f, 4f, 5f, 6f }, 2, 3).ToNpy();
            Assert.That(bytes[0], Is.EqualTo(0x93));
            Assert.That(System.Text.Encoding.ASCII.GetString(bytes, 1, 5), Is.EqualTo("NUMPY"));
            Assert.That(bytes[6], Is.EqualTo(1), "format 1.0");
            int headerLength = bytes[8] | (bytes[9] << 8);
            Assert.That((10 + headerLength) % 64, Is.EqualTo(0), "the data starts on a 64-byte boundary");
            string header = System.Text.Encoding.ASCII.GetString(bytes, 10, headerLength);
            Assert.That(header, Does.StartWith("{'descr': '<f4', 'fortran_order': False, 'shape': (2, 3), }"));
            Assert.That(header, Does.EndWith("\n"));
            Assert.That(bytes.Length, Is.EqualTo(10 + headerLength + 24));
            Assert.That(System.Text.Encoding.ASCII.GetString(NpyArray.Header("<i4", new[] { 7 })), Does.Contain("'shape': (7,), }"), "a 1-D shape is written (7,)");
        }

        [Test]
        public void EveryDtype_ReadsBackAsWritten()
        {
            var floats = new[] { 0f, -1.5f, 3.25f, float.MaxValue };
            var doubles = new[] { 0.0, 1e-300, -2.5, Math.PI };
            var ints = new[] { 0, -1, int.MaxValue, 42 };
            var bytes = new sbyte[] { -128, 0, 5, 127 };
            var strings = new[] { "headSize", "", "UMA30_Body_UDIM1001_slot" };
            Assert.That(NpyArray.FromNpy(NpyArray.Float32(floats).ToNpy()).AsFloat32(), Is.EqualTo(floats));
            Assert.That(NpyArray.FromNpy(NpyArray.Float64(doubles).ToNpy()).AsFloat64(), Is.EqualTo(doubles));
            Assert.That(NpyArray.FromNpy(NpyArray.Int32(ints).ToNpy()).AsInt32(), Is.EqualTo(ints));
            Assert.That(NpyArray.FromNpy(NpyArray.Int8(bytes).ToNpy()).AsInt8(), Is.EqualTo(bytes));
            var text = NpyArray.Unicode(strings);
            Assert.That(text.Descr, Is.EqualTo("<U24"), "as wide as the longest string");
            Assert.That(NpyArray.FromNpy(text.ToNpy()).AsUnicode(), Is.EqualTo(strings));
            Assert.Throws<ArgumentException>(() => new NpyArray("<f4", new[] { 3 }, new byte[8]), "a shape that does not match its bytes is refused");
        }

        [Test]
        public void AnNpz_IsAStoredZipOfNpyMembers_WithChunkedMembersAndCheckedCrcs()
        {
            string path = Path.Combine(folder, "a.npz");
            WriteOnce.Write(path, stream =>
            {
                using (var npz = new NpzWriter(stream))
                {
                    npz.Add("plain", NpyArray.Int32(new[] { 1, 2, 3 }));
                    npz.Add("chunked", "<f4", new[] { 2, 2 }, () => new[] { NpyArray.Float32(new[] { 1f, 2f }).Data, NpyArray.Float32(new[] { 3f, 4f }).Data });
                }
            });
            var read = FaceFitWork.ReadNpz(path);
            Assert.That(read.Keys, Is.EquivalentTo(new[] { "plain", "chunked" }));
            Assert.That(read["plain"].AsInt32(), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(read["chunked"].Shape, Is.EqualTo(new[] { 2, 2 }));
            Assert.That(read["chunked"].AsFloat32(), Is.EqualTo(new[] { 1f, 2f, 3f, 4f }));

            var bytes = File.ReadAllBytes(path);
            bytes[bytes.Length / 2] ^= 0xff;
            Assert.Throws<InvalidDataException>(() => NpzWriter.Read(bytes), "a damaged member fails its CRC");

            Assert.Throws<InvalidDataException>(() => WriteOnce.Write(Path.Combine(folder, "short.npz"), stream =>
            {
                using (var npz = new NpzWriter(stream)) npz.Add("short", "<f4", new[] { 4 }, () => new[] { new byte[8] });
            }), "a member with fewer bytes than its shape says is refused");
        }

        // ------------------------------------------------------------------ JSON

        [Test]
        public void Json_ReadsAndWritesWithLfAndInvariantNumbers()
        {
            var parsed = (JsonObject)Json.Parse("{\"a\": [1, 2.5, -3e-2], \"b\": {\"c\": \"x\\ny\"}, \"d\": true, \"e\": null}");
            Assert.That(((List<object>)parsed["a"]).Cast<double>(), Is.EqualTo(new[] { 1.0, 2.5, -0.03 }));
            Assert.That(((JsonObject)parsed["b"])["c"], Is.EqualTo("x\ny"));
            Assert.That(parsed["d"], Is.EqualTo(true));
            Assert.That(parsed.TryGet("e", out var e) && e == null, Is.True);
            string written = Json.Write(parsed);
            Assert.That(written, Does.Not.Contain("\r"));
            Assert.That(Json.Write(Json.Parse(written)), Is.EqualTo(written), "a written document reads back the same");
            Assert.That(Json.Write(new JsonObject().Set("v", 0.1f)), Does.Contain("0.1"));
            Assert.Throws<FormatException>(() => Json.Parse("{\"a\": 1, \"a\": 2}"), "a key given twice is refused");
            Assert.Throws<FormatException>(() => Json.Parse("{\"a\": 1,}"));
        }

        // ------------------------------------------------------------------ the job

        private string Project => Path.Combine(folder, "project");

        private string JobText(string extra = "", string output = null) =>
            "{\"mode\": \"sweep\", \"race\": \"Human Male 3.0\", \"gameCommit\": \"" + new string('a', 40) + "\", \"outputFolder\": "
            + Json.Write(output ?? Path.Combine(folder, "out")).Trim() + extra + "}";

        private FaceFitJob Parse(string text, out List<string> errors)
        {
            errors = new List<string>();
            return FaceFitJob.Parse(text, Project, errors);
        }

        [Test]
        public void AJob_TakesItsDefaultsFromTheContract()
        {
            var job = Parse(JobText(), out var errors);
            Assert.That(errors, Is.Empty);
            Assert.That(job.BaselineSource, Is.EqualTo("house"));
            Assert.That(job.RandomCount, Is.EqualTo(6));
            Assert.That(job.BudgetMinutes, Is.EqualTo(30));
            Assert.That(job.RepeatBaseline, Is.True);
            Assert.That(job.SweepNames, Is.Null, "every face name of the race, read from UMA");
            Assert.That(job.Persons, Is.Null, "every All-Star of the race, read from the game");
            Assert.That(job.HeadSlots, Is.EqualTo(FaceFitJob.DefaultHeadSlots));
        }

        [Test]
        public void AJob_NamesEveryProblem()
        {
            Parse("{\"mode\": \"fit\", \"race\": \"\", \"gameCommit\": \"abc\", \"outputFolder\": \"relative\", \"colour\": 1}", out var errors);
            string all = string.Join("\n", errors);
            foreach (string expected in new[] { "\"mode\" is \"fit\"", "\"race\" must be", "\"gameCommit\" must be", "\"outputFolder\" must be an absolute path",
                "unknown field \"colour\"" })
                Assert.That(all, Does.Contain(expected), all);
            Assert.That(Parse(JobText(", \"plan\": {\"positions\": [1.5], \"sweepName\": []}"), out errors), Is.Null);
            all = string.Join("\n", errors);
            foreach (string expected in new[] { "\"plan.positions\" holds 1.5", "unknown field \"plan.sweepName\"" })
                Assert.That(all, Does.Contain(expected), all);

            Parse(JobText(output: Path.Combine(Project, "Assets", "export")), out errors);
            Assert.That(string.Join("\n", errors), Does.Contain("is inside the project"), "nothing is written under the project");
            Parse(JobText(output: Project), out errors);
            Assert.That(errors, Is.Not.Empty, "nor in the project's own folder");

            Parse(JobText(", \"candidates\": []"), out errors);
            Assert.That(string.Join("\n", errors), Does.Contain("\"candidates\" belong to a read-back"));
            Parse(JobText(", \"extraSamples\": [{\"name\": \"headSize\", \"value\": 1.2}]"), out errors);
            Assert.That(string.Join("\n", errors), Does.Contain("extraSamples[0].value"));
            Parse(JobText().Replace("\"sweep\"", "\"readback\""), out errors);
            Assert.That(string.Join("\n", errors), Does.Contain("a read-back needs \"candidates\""));
        }

        [Test]
        public void AJobsNames_AreCheckedAgainstTheRace()
        {
            var job = Parse(JobText(", \"plan\": {\"sweepNames\": [\"headSize\", \"noseCurvy\"]}, \"baseline\": {\"source\": \"values\", \"values\": {\"eyeSize\": 0.6}}"), out var errors);
            Assert.That(errors, Is.Empty);
            var unknown = job.UnknownNames(new HashSet<string> { "headSize", "eyeSize" });
            Assert.That(unknown.Count, Is.EqualTo(1));
            Assert.That(unknown[0], Does.Contain("noseCurvy"));
        }

        // ------------------------------------------------------------------ the plan

        [Test]
        public void ThePlan_SweepsEveryNameAroundItsBaseline_AndRepeatsTheBaselineLast()
        {
            var job = Parse(JobText(), out _);
            var baseline = new Dictionary<string, double> { ["headSize"] = .72, ["noseSize"] = .5, ["EyePosition"] = 1 };
            var plan = FaceFitPlan.Sweep(job, new[] { "headSize", "noseSize", "EyePosition" }, baseline, new[] { "dr-will-kirby" }, unrequested: true, fresh: false);
            Assert.That(plan.First().Id, Is.EqualTo("baseline-a"));
            Assert.That(plan.Last().Id, Is.EqualTo("baseline-b"));
            Assert.That(plan.Where(s => s.Name == "noseSize").Select(s => s.Value), Is.EquivalentTo(new[] { .25, .375, .625, .75 }));
            Assert.That(plan.Where(s => s.Name == "headSize").Select(s => s.Value), Is.EquivalentTo(new[] { .25, .375, .5, .625, .75 }));
            Assert.That(plan.Where(s => s.Name == "EyePosition").Select(s => s.Value), Is.EquivalentTo(new[] { .25, .375, .5, .625, .75 }), "EyePosition sits at 1, so .5 is a move too");
            Assert.That(plan.Where(s => s.Kind == PlannedSample.Slider).Select(s => s.PositionSource).Distinct(), Is.EqualTo(new[] { "default" }));
            Assert.That(plan.Count(s => s.Kind == PlannedSample.Random), Is.EqualTo(6));
            Assert.That(plan.Where(s => s.Kind == PlannedSample.Person).Select(s => s.Id), Is.EqualTo(new[] { "person-dr-will-kirby-a", "person-dr-will-kirby-b" }));
            Assert.That(plan.Where(s => s.Fresh).Select(s => s.Id), Is.EqualTo(new[] { "unrequested", "person-dr-will-kirby-a", "person-dr-will-kirby-b" }),
                "the unrequested build and a person's samples are built on a fresh body; a race sweep's others share one");
            Assert.That(plan.Select(s => s.Id).Distinct().Count(), Is.EqualTo(plan.Count), "every id is unique");
            Assert.That(FaceFitPlan.PositionsFor(.75, new[] { .25, .75 }), Is.EqualTo(new[] { .25 }), "a position at the baseline is skipped");
        }

        /// <summary>A run cut short still holds the contract's set: every name at .25 and .75 before any inner pair, .5 after them, extras and random vectors after the sliders.</summary>
        [Test]
        public void ThePlansOrder_PutsTheContractsPairsFirst()
        {
            var job = Parse(JobText(", \"extraSamples\": [{\"name\": \"headSize\", \"value\": 1}]"), out var errors);
            Assert.That(errors, Is.Empty);
            var baseline = new Dictionary<string, double> { ["headSize"] = .72, ["noseSize"] = .5, ["EyePosition"] = 1 };
            var plan = FaceFitPlan.Sweep(job, new[] { "headSize", "noseSize", "EyePosition" }, baseline, Array.Empty<string>(), unrequested: false, fresh: false);
            var kinds = plan.Select(s => s.Kind == PlannedSample.Slider ? "tier" + FaceFitPlan.Tier(s.Value) : s.Kind).ToList();
            var order = new[] { PlannedSample.Baseline, "tier1", "tier2", "tier3", PlannedSample.ExtraKind, PlannedSample.Random };
            for (int i = 1; i < kinds.Count - 1; i++)
                Assert.That(Array.IndexOf(order, kinds[i]), Is.GreaterThanOrEqualTo(Array.IndexOf(order, kinds[i - 1])), string.Join(", ", plan.Select(s => s.Id)));
            Assert.That(plan.Where(s => kinds[plan.IndexOf(s)] == "tier1").Select(s => s.Name + "@" + s.Value.ToString(CultureInfo.InvariantCulture)), Is.EqualTo(new[]
                { "headSize@0.25", "headSize@0.75", "noseSize@0.25", "noseSize@0.75", "EyePosition@0.25", "EyePosition@0.75" }));
            Assert.That(plan.Any(s => s.Kind == PlannedSample.Unrequested), Is.False);
        }

        [Test]
        public void TheJob_CanReplaceThePositions_ForAllNamesOrOne()
        {
            var job = Parse(JobText(", \"plan\": {\"positions\": [0.25, 0.75], \"positionsByName\": {\"headSize\": [0, 0.125, 0.875, 1]}}"), out var errors);
            Assert.That(errors, Is.Empty);
            var baseline = new Dictionary<string, double> { ["headSize"] = .72, ["noseSize"] = .5 };
            var plan = FaceFitPlan.Sweep(job, new[] { "headSize", "noseSize" }, baseline, Array.Empty<string>(), unrequested: false, fresh: true);
            Assert.That(plan.Where(s => s.Name == "headSize").Select(s => s.Value), Is.EquivalentTo(new[] { 0, .125, .875, 1 }));
            Assert.That(plan.Where(s => s.Name == "noseSize").Select(s => s.Value), Is.EquivalentTo(new[] { .25, .75 }));
            Assert.That(plan.Where(s => s.Kind == PlannedSample.Slider).Select(s => s.PositionSource).Distinct(), Is.EqualTo(new[] { "job" }));
            Assert.That(plan.All(s => s.Fresh), Is.True, "a sweep around one look builds every sample on a fresh body");
            Assert.That(job.UnknownNames(new HashSet<string> { "noseSize" }).Single(), Does.Contain("positionsByName"));
        }

        [Test]
        public void TheRandomVectors_AreSeeded_InRange_AndTheSameEveryRun()
        {
            var names = new[] { "a", "b", "c" };
            var first = FaceFitPlan.RandomVectors(20261111, 6, names, .3, .7);
            var again = FaceFitPlan.RandomVectors(20261111, 6, names, .3, .7);
            var other = FaceFitPlan.RandomVectors(7, 6, names, .3, .7);
            Assert.That(first.SelectMany(v => v.Values), Is.EqualTo(again.SelectMany(v => v.Values)));
            Assert.That(first.SelectMany(v => v.Values), Is.Not.EqualTo(other.SelectMany(v => v.Values)));
            Assert.That(first.SelectMany(v => v.Values), Has.All.InRange(.3, .7));
            Assert.That(first.SelectMany(v => v.Values).Distinct().Count(), Is.GreaterThan(12));
        }

        [Test]
        public void AReadback_BuildsEachPersonOnce_ThenTheirCandidates()
        {
            var job = Parse("{\"mode\": \"readback\", \"race\": \"Human Male 3.0\", \"gameCommit\": \"" + new string('b', 40) + "\", \"outputFolder\": "
                + Json.Write(Path.Combine(folder, "rb")).Trim() + ", \"candidates\": [{\"appearanceId\": \"dr-will-kirby\", \"round\": 1, \"values\": {\"jawsSize\": 0.61}},"
                + " {\"appearanceId\": \"dr-will-kirby\", \"round\": 2, \"values\": {\"jawsSize\": 0.63}}]}", out var errors);
            Assert.That(errors, Is.Empty);
            var plan = FaceFitPlan.Readback(job);
            Assert.That(plan.Select(s => s.Id), Is.EqualTo(new[] { "person-dr-will-kirby-a", "person-dr-will-kirby-b", "candidate-r1-dr-will-kirby", "candidate-r2-dr-will-kirby" }));
            Assert.That(plan[2].Overrides["jawsSize"], Is.EqualTo(.61));
        }

        [Test]
        public void ARaceIsSpelledInFileNamesOneWay()
        {
            Assert.That(FaceFitPlan.RaceSlug("Human Male 3.0"), Is.EqualTo("human-male-3-0"));
            Assert.That(FaceFitPlan.RaceSlug("Human Female 3.0"), Is.EqualTo("human-female-3-0"));
            Assert.That(FaceFitPlan.RaceSlug("  A--b__C. "), Is.EqualTo("a-b-c"));
            Assert.That(FaceFitWork.RoundManifestName("Human Male 3.0"), Is.EqualTo("manifest-human-male-3-0.json"));
        }

        private FaceFitJob ReadbackJob(string race, string output, string appearanceId, string extra = "")
        {
            var job = Parse("{\"mode\": \"readback\", \"race\": \"" + race + "\", \"gameCommit\": \"" + new string('c', 40) + "\", \"outputFolder\": "
                + Json.Write(output).Trim() + ", \"candidates\": [{\"appearanceId\": \"" + appearanceId + "\", \"round\": 1, \"values\": {\"headSize\": 0.6}}]" + extra + "}", out var errors);
            Assert.That(errors, Is.Empty);
            return job;
        }

        /// <summary>A read-back run as the Unity side drives it, with made-up captures: null when Finished answered first.</summary>
        private static string RunReadback(FaceFitJob job)
        {
            string finished = FaceFitWork.Finished(job, out _);
            if (finished != null) return finished;
            string work = FaceFitWork.Open(job, out _);
            var plan = FaceFitPlan.Readback(job);
            foreach (var sample in plan)
            {
                if (FaceFitWork.IsFinished(work, sample.Id)) continue;
                var capture = Capture(new[] { "headSize" }, new[] { sample.Overrides.TryGetValue("headSize", out var o) ? o : .72 }, 0f);
                FaceFitWork.WriteTopology(work, capture, job.HeadSlots);
                FaceFitWork.WriteSample(work, sample, capture, null);
            }
            FaceFitWork.AssembleReadback(job, work, plan, new JsonObject());
            return null;
        }

        private static Dictionary<string, string> Hashes(string root) =>
            Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(f => f, Sha256.File);

        /// <summary>Contract S4's readback/r&lt;n&gt;/ holds both races of a round: one manifest per race, each naming only its own candidates.</summary>
        [Test]
        public void BothRacesOfARound_ShareItsFolder_EachWithItsOwnManifest()
        {
            string output = Path.Combine(folder, "readback");
            var male = ReadbackJob("Human Male 3.0", output, "male-look");
            var female = ReadbackJob("Human Female 3.0", output, "female-look");
            Assert.That(RunReadback(male), Is.Null);
            Assert.That(RunReadback(female), Is.Null, "the female job of the same round is not refused");
            string round = Path.Combine(output, "r1");
            Assert.That(File.Exists(Path.Combine(round, FaceFitWork.ManifestName)), Is.False, "no plain manifest.json in a round folder");
            foreach (var (race, look) in new[] { ("human-male-3-0", "male-look"), ("human-female-3-0", "female-look") })
            {
                var manifest = (JsonObject)Json.Parse(File.ReadAllText(Path.Combine(round, "manifest-" + race + ".json")));
                var files = ((List<object>)manifest["candidates"]).Cast<JsonObject>().Select(c => (string)c["file"]).ToList();
                Assert.That(files, Is.EqualTo(new[] { look + ".npz" }), race + "'s manifest names only its own candidates");
                Assert.That(manifest["round"], Is.EqualTo(1));
                Assert.That(File.Exists(Path.Combine(round, look + ".npz")), Is.True);
            }

            // The same job again is finished and changes nothing.
            var before = Hashes(output);
            Assert.That(RunReadback(male), Does.StartWith("complete"));
            Assert.That(Hashes(output), Is.EqualTo(before), "a finished job rewrites no file");

            // Another job of the same race in the same round is refused.
            var other = ReadbackJob("Human Male 3.0", output, "male-look", ", \"description\": \"another job\"");
            Assert.That(RunReadback(other), Does.StartWith("error").And.Contain("another job"));
            Assert.That(Hashes(output), Is.EqualTo(before));
        }

        [Test]
        public void APlainManifestInARoundFolder_StopsTheReadback_AndIsLeftAsItIs()
        {
            string output = Path.Combine(folder, "readback-plain");
            Directory.CreateDirectory(Path.Combine(output, "r1"));
            string plain = Path.Combine(output, "r1", FaceFitWork.ManifestName);
            File.WriteAllText(plain, "{\"someone\": \"else\"}");
            var outcome = RunReadback(ReadbackJob("Human Male 3.0", output, "male-look"));
            Assert.That(outcome, Does.StartWith("error").And.Contain("plain manifest.json"), outcome);
            Assert.That(File.ReadAllText(plain), Is.EqualTo("{\"someone\": \"else\"}"));
            Assert.That(Directory.GetFiles(Path.Combine(output, "r1")), Is.EqualTo(new[] { plain }), "nothing else is written in the round folder");
        }

        /// <summary>A kill after a candidate file was published but before the round manifest: the same job finishes the round.</summary>
        [Test]
        public void AReadbackKilledBeforeItsRoundManifest_FinishesOnTheNextRun()
        {
            string output = Path.Combine(folder, "readback-kill");
            var job = ReadbackJob("Human Male 3.0", output, "male-look");
            Assert.That(RunReadback(job), Is.Null);
            string manifest = Path.Combine(output, "r1", FaceFitWork.RoundManifestName(job.Race));
            string candidate = Path.Combine(output, "r1", "male-look.npz");
            string sha = Sha256.File(candidate);
            File.Delete(manifest);   // as if the run stopped between the candidate file and the manifest
            File.WriteAllText(manifest + WriteOnce.PartSuffix, "half");
            Assert.That(RunReadback(job), Is.Null, "the round is not finished, so the same job runs again");
            Assert.That(File.Exists(manifest), Is.True);
            Assert.That(File.Exists(manifest + WriteOnce.PartSuffix), Is.False);
            Assert.That(Sha256.File(candidate), Is.EqualTo(sha), "the published candidate file is kept, not rewritten");
        }

        // ------------------------------------------------------------------ statistics

        [Test]
        public void Symmetry_IsZeroForAMirroredSlot_AndMeasuresAnOffset()
        {
            var symmetric = new[] { -1f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f };
            Assert.That(FaceFitStats.Symmetry(symmetric, 0, 3), Is.EqualTo(new[] { 0.0, 0.0, 0.0 }));
            var shifted = symmetric.Select((x, i) => i % 3 == 0 ? x + .001f : x).ToArray();
            var stats = FaceFitStats.Symmetry(shifted, 0, 3);
            Assert.That(stats[0], Is.EqualTo(2.0).Within(1e-3), "a 1 mm shift is 2 mm from its mirror");
        }

        /// <summary>dominant-bone-v1: the bone of the largest weight decides, by its name and by where it sits under Head.</summary>
        [Test]
        public void Regions_FollowTheDominantBone_AndWhereItSitsInTheSkeleton()
        {
            //                  0        1       2             3             4                 5                6        7
            var names = new[] { "Spine", "Neck", "NeckAdjust", "Head",       "HeadAdjust",     "LeftEarAdjust", "Jaw",   "LeftClavicle" };
            var parents = new[] { -1,    0,      1,            1,            3,                3,               3,       0 };
            Assert.That(FaceFitStats.RegionOfBone(2, names, parents), Is.EqualTo("neck"));
            Assert.That(FaceFitStats.RegionOfBone(1, names, parents), Is.EqualTo("neck"));
            Assert.That(FaceFitStats.RegionOfBone(3, names, parents), Is.EqualTo("skull"));
            Assert.That(FaceFitStats.RegionOfBone(4, names, parents), Is.EqualTo("skull"));
            Assert.That(FaceFitStats.RegionOfBone(5, names, parents), Is.EqualTo("ears"));
            Assert.That(FaceFitStats.RegionOfBone(6, names, parents), Is.EqualTo("face"));
            Assert.That(FaceFitStats.RegionOfBone(7, names, parents), Is.EqualTo("body"), "a shoulder bone is not face");
            Assert.That(FaceFitStats.RegionOfBone(0, names, parents), Is.EqualTo("body"));
            Assert.That(FaceFitStats.RegionOfBone(-1, names, parents), Is.EqualTo("body"));
            // A tie of weights goes to the lower bone index; a padding slot (-1) never wins.
            Assert.That(FaceFitStats.DominantBone(new[] { 6, 3, -1, -1 }, new[] { .5f, .5f, 0f, 0f }, 0), Is.EqualTo(3));
            Assert.That(FaceFitStats.DominantBone(new[] { 6, 3, 2, 1 }, new[] { .1f, .2f, .6f, .1f }, 0), Is.EqualTo(2));
            Assert.That(FaceFitStats.SlotMatches("UMA30_Body_UDIM1001_slot_baked_Human Male 3.0", "UMA30_Body_UDIM1001_slot"), Is.True);
            Assert.That(FaceFitStats.SlotMatches("UMA30_Body_UDIM1002_slot", "UMA30_Body_UDIM1001_slot"), Is.False);
        }

        [Test]
        public void AHeadSlotWithoutFaceOrEars_StopsTheExport()
        {
            var c = Capture(new[] { "headSize" }, new[] { .72 }, 0f);
            for (int v = 0; v < 4; v++) c.BoneIndices[v * 4] = 2;  // every head vertex on Head: all skull
            var e = Assert.Throws<InvalidDataException>(() => FaceFitWork.WriteTopology(folder, c, FaceFitJob.DefaultHeadSlots));
            Assert.That(e.Message, Does.Contain(FaceFitStats.RegionRule).And.Contain("face"));
            Assert.That(File.Exists(Path.Combine(folder, FaceFitWork.TopologyName + ".npz")), Is.False, "nothing is written for a rig the rule does not fit");
        }

        // ------------------------------------------------------------------ files written once, resume and assembly

        [Test]
        public void AFinishedFile_IsNeverReplaced()
        {
            string path = Path.Combine(folder, "x.txt");
            WriteOnce.Text(path, "one\r\n");
            Assert.That(File.ReadAllText(path), Is.EqualTo("one\n"), "written with LF");
            Assert.Throws<IOException>(() => WriteOnce.Text(path, "two"));
            Assert.That(File.ReadAllText(path), Is.EqualTo("one\n"));
            File.WriteAllText(path + WriteOnce.PartSuffix, "half");
            Assert.That(WriteOnce.DeleteParts(folder), Is.EqualTo(1), "a half-written file is cleared, never taken for a finished one");
        }

        [Test]
        public void Publish_KeepsThisJobsFile_AndRefusesAnotherOne()
        {
            string work = Path.Combine(folder, "work");
            Directory.CreateDirectory(work);
            string target = Path.Combine(folder, "final.bin");
            int writes = 0;
            string sha = FaceFitWork.Publish(work, target, "final", staged => { writes++; WriteOnce.Bytes(staged, new byte[] { 1, 2, 3 }); });
            Assert.That(FaceFitWork.Publish(work, target, "final", staged => { writes++; WriteOnce.Bytes(staged, new byte[] { 9 }); }), Is.EqualTo(sha));
            Assert.That(writes, Is.EqualTo(1), "a second run keeps the file this job published");
            string other = Path.Combine(folder, "other.bin");
            File.WriteAllBytes(other, new byte[] { 7 });
            Assert.Throws<IOException>(() => FaceFitWork.Publish(work, other, "other", staged => WriteOnce.Bytes(staged, new byte[] { 1 })), "another job's file is refused");
            Assert.That(File.ReadAllBytes(other), Is.EqualTo(new byte[] { 7 }));
        }

        /// <summary>
        /// A made-up body: a head slot of four vertices (on Head, LeftEarAdjust, Jaw and NeckAdjust: skull, ears, face, neck)
        /// and a body slot of two (on Spine); the first vertex moved by <paramref name="shift"/> metres.
        /// </summary>
        private static FaceFitCapture Capture(IList<string> dna, double[] values, float shift)
        {
            //                       0        1             2       3                4
            var bones = new[] { "Spine", "NeckAdjust", "Head", "LeftEarAdjust", "Jaw" };
            var dominant = new[] { 2, 3, 4, 1, 0, 0 };
            var c = new FaceFitCapture
            {
                SlotNames = new[] { "UMA30_Body_UDIM1001_slot", "UMA30_Body_UDIM1002_slot" },
                SlotOffset = new[] { 0, 4 },
                SlotCount = new[] { 4, 2 },
                Positions = new[] { 0f, 1.7f, .05f, .07f, 1.6f, 0f, 0f, 1.5f, .08f, 0f, 1.45f, 0f, .1f, 1f, 0f, -.1f, 1f, 0f },
                Normals = new float[18],
                Triangles = new[] { 0, 1, 2, 2, 3, 0, 3, 4, 5 },
                Uv0 = new float[12],
                BoneIndices = dominant.SelectMany(b => new[] { b, 0, 0, 0 }).ToArray(),
                BoneWeights = dominant.SelectMany(b => new[] { 1f, 0f, 0f, 0f }).ToArray(),
                BoneNames = bones,
                BoneParents = new[] { -1, 0, 1, 2, 2 },
                BindPoses = new float[80],
                BoneLocal = new float[50],
                BonePosition = new float[] { 0, 1f, 0, 0, 1.4f, 0, 0, 1.5f, 0, .07f, 1.6f, 0, 0, 1.5f, .07f },
                DnaNames = dna.ToArray(),
                Requested = values.ToArray(),
                Readback = values.ToArray(),
                Seconds = .25,
            };
            c.Positions[0] += shift;
            return c;
        }

        private FaceFitJob OwnJob(string output, string extra = ", \"plan\": {\"randomCount\": 0}, \"persons\": []")
        {
            var job = Parse(JobText(extra, output), out var errors);
            Assert.That(errors, Is.Empty);
            return job;
        }

        /// <summary>
        /// A run killed between a sample's .npz and its record (or the topology's) leaves an .npz with no record. The next
        /// run clears it as unfinished work, builds that sample once more, and the job still assembles.
        /// </summary>
        [Test]
        public void AnNpzWithoutItsRecord_IsUnfinishedWork_AndTheNextRunRebuildsIt()
        {
            var job = OwnJob(Path.Combine(folder, "orphan"));
            var names = new[] { "headSize", "noseSize" };
            var baseline = new Dictionary<string, double> { ["headSize"] = .72, ["noseSize"] = .5 };
            var plan = FaceFitPlan.Sweep(job, names, baseline, Array.Empty<string>(), unrequested: false, fresh: false);
            string work = FaceFitWork.Open(job, out _);
            FaceFitCapture Make(PlannedSample s) => Capture(names, names.Select(n => s.Overrides.TryGetValue(n, out var o) ? o : baseline[n]).ToArray(), 0f);

            // The kill: the topology's .npz and one sample's .npz are in place, neither record is.
            FaceFitWork.WriteTopology(work, Make(plan[0]), job.HeadSlots);
            File.Delete(Path.Combine(work, FaceFitWork.TopologyName + ".json"));
            FaceFitWork.WriteSample(work, plan[0], Make(plan[0]), null);
            File.Delete(Path.Combine(work, plan[0].Id + ".json"));
            Assert.That(FaceFitWork.IsFinished(work, plan[0].Id), Is.False);

            FaceFitWork.Open(job, out int cleared);
            Assert.That(cleared, Is.EqualTo(2), "the next run clears both orphaned .npz files");
            Assert.That(File.Exists(Path.Combine(work, plan[0].Id + ".npz")), Is.False);
            Assert.That(File.Exists(Path.Combine(work, FaceFitWork.TopologyName + ".npz")), Is.False);

            // Even without that sweep, writing the sample and the topology again over an orphan works.
            FaceFitWork.WriteTopology(work, Make(plan[0]), job.HeadSlots);
            File.Delete(Path.Combine(work, FaceFitWork.TopologyName + ".json"));
            FaceFitWork.WriteTopology(work, Make(plan[0]), job.HeadSlots);
            Assert.That(File.Exists(Path.Combine(work, FaceFitWork.TopologyName + ".json")), Is.True, "the topology's record is written after an orphaned .npz");
            FaceFitWork.WriteSample(work, plan[0], Make(plan[0]), null);
            File.Delete(Path.Combine(work, plan[0].Id + ".json"));
            FaceFitWork.WriteSample(work, plan[0], Make(plan[0]), null);
            Assert.That(FaceFitWork.IsFinished(work, plan[0].Id), Is.True);

            foreach (var sample in plan.Skip(1)) FaceFitWork.WriteSample(work, sample, Make(sample), null);
            var manifest = FaceFitWork.AssembleSweep(job, work, plan, names, new JsonObject());
            Assert.That(manifest["complete"], Is.EqualTo(true));
        }

        [Test]
        public void PositionsSharingASampleId_AreRefused()
        {
            Parse(JobText(", \"plan\": {\"positions\": [0.3333, 0.3334]}"), out var errors);
            Assert.That(string.Join("\n", errors), Does.Contain("share the sample id position 0.333"), string.Join("\n", errors));
            Parse(JobText(", \"plan\": {\"positionsByName\": {\"headSize\": [0.1, 0.1004]}}"), out errors);
            Assert.That(string.Join("\n", errors), Does.Contain("share the sample id position"), string.Join("\n", errors));
            var job = OwnJob(Path.Combine(folder, "ids"));
            var baseline = new Dictionary<string, double> { ["headSize"] = .72 };
            Assert.Throws<ArgumentException>(() => FaceFitPlan.Sweep(job, new[] { "headSize", "headSize" }, baseline, Array.Empty<string>(), unrequested: false, fresh: false),
                "a plan whose two samples share an id is refused");
        }

        [Test]
        public void ASweepAssembles_FromFinishedSamples()
        {
            string root = Proof ?? folder;
            string output = Path.Combine(root, "export-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            var job = Parse(JobText(", \"plan\": {\"randomCount\": 1}, \"persons\": []", output), out var errors);
            Assert.That(errors, Is.Empty);
            var names = new[] { "headSize", "noseSize" };
            var baseline = new Dictionary<string, double> { ["headSize"] = .72, ["noseSize"] = .5 };
            var plan = FaceFitPlan.Sweep(job, names, baseline, Array.Empty<string>(), unrequested: true, fresh: false);
            string work = FaceFitWork.Open(job, out int parts);
            Assert.That(parts, Is.Zero);
            Assert.That(FaceFitWork.Finished(job, out bool complete), Is.Null);
            Assert.That(complete, Is.False);

            // A first run stops after two samples (its budget, a crash); a second run finishes the rest, redoing none.
            int built = 0;
            void Run(int limit)
            {
                foreach (var sample in plan)
                {
                    if (FaceFitWork.IsFinished(work, sample.Id)) continue;
                    if (limit-- <= 0) return;
                    var values = names.Select(n => sample.Overrides.TryGetValue(n, out var o) ? o : baseline[n]).ToArray();
                    var capture = Capture(names, values, sample.Name == "noseSize" ? (float)(values[1] - .5) * .01f : 0f);
                    // The unrequested build requests the house's headSize only; its noseSize is read back, not requested.
                    if (sample.Kind == PlannedSample.Unrequested) capture.Requested[1] = double.NaN;
                    FaceFitWork.WriteTopology(work, capture, job.HeadSlots);
                    FaceFitWork.WriteSample(work, sample, capture, null);
                    built++;
                }
            }
            Run(2);
            Assert.That(built, Is.EqualTo(2));
            File.WriteAllText(Path.Combine(work, plan[2].Id + ".npz" + WriteOnce.PartSuffix), "cut off mid-write");
            FaceFitWork.Open(job, out parts);
            Assert.That(parts, Is.EqualTo(1), "the next run clears the half-written file");
            Run(int.MaxValue);
            Assert.That(built, Is.EqualTo(plan.Count), "every sample built exactly once");

            var manifest = FaceFitWork.AssembleSweep(job, work, plan, names, new JsonObject().Set("tool", "test"));
            Assert.That(FaceFitWork.Finished(job, out complete), Does.StartWith("complete"));
            Assert.That(complete, Is.True, "a second run of the same job finds nothing to do");

            var npz = FaceFitWork.ReadNpz(Path.Combine(output, FaceFitWork.SamplesName));
            int s = plan.Count;
            Assert.That(npz["positions"].Shape, Is.EqualTo(new[] { s, 6, 3 }));
            Assert.That(npz["requested"].Shape, Is.EqualTo(new[] { s, 2 }));
            Assert.That(npz["sample_ids"].AsUnicode(), Is.EqualTo(plan.Select(p => p.Id).ToArray()));
            Assert.That(npz["vertex_region"].AsInt8().Select(r => npz["region_names"].AsUnicode()[r]),
                Is.EqualTo(new[] { "skull", "ears", "face", "neck", "body", "body" }));
            Assert.That(npz["dominant_bone"].AsInt32(), Is.EqualTo(new[] { 2, 3, 4, 1, 0, 0 }));
            Assert.That(npz["moved_sample"].AsInt32().Select(i => plan[i].Name).Distinct(), Is.EqualTo(new[] { "noseSize" }), "only the swept slider moved a vertex");
            int unrequested = plan.FindIndex(p => p.Kind == PlannedSample.Unrequested);
            Assert.That(double.IsNaN(npz["requested"].AsFloat64()[unrequested * 2 + 1]), Is.True, "a name a sample did not request is NaN in requested");
            Assert.That((double)manifest["noiseFloorMm"], Is.EqualTo(0.0));
            Assert.That(manifest["topologyChanged"], Is.EqualTo(false));
            Assert.That(manifest["complete"], Is.EqualTo(true));
            Assert.That(manifest["regionRule"], Is.EqualTo("dominant-bone-v1"));
            Assert.That(((JsonObject)manifest["regionBoneMap"])["Jaw"], Is.EqualTo("face"));
            Assert.That(((JsonObject)((JsonObject)manifest["regionCounts"])["UMA30_Body_UDIM1001_slot"])["ears"], Is.EqualTo(1));
            Assert.That(((JsonObject)manifest["unrequestedReadBack"])["noseSize"], Is.EqualTo(.5));
            var noseLine = ((List<object>)manifest["baselineNames"]).Cast<JsonObject>().Single(n => (string)n["name"] == "noseSize");
            Assert.That(noseLine["requested"], Is.EqualTo(.5));
            Assert.That(noseLine["readBack"], Is.EqualTo(.5));
            var planned = (JsonObject)((JsonObject)manifest["plan"])["positions"];
            Assert.That(((List<object>)planned["noseSize"]).Cast<JsonObject>().Select(p => p["source"]).Distinct(), Is.EqualTo(new[] { "default" }));
            Assert.That(((JsonObject)manifest["headSymmetryMm"])["slot"], Is.EqualTo("UMA30_Body_UDIM1001_slot"));
            var ears = (JsonObject)manifest["earAdjust"];
            Assert.That(ears["LeftEarAdjust"], Is.Not.Null);
            Assert.That(ears["RightEarAdjust"], Is.Null, "a bone the body lacks is null, not invented");
            var written = (JsonObject)Json.Parse(File.ReadAllText(Path.Combine(output, FaceFitWork.ManifestName)));
            Assert.That(((JsonObject)written["samplesFile"])["sha256"], Is.EqualTo(Sha256.File(Path.Combine(output, FaceFitWork.SamplesName))));
            Assert.Throws<IOException>(() => FaceFitWork.AssembleSweep(job, work, plan, names, new JsonObject()), "a finished manifest is never rewritten");

            if (Proof != null)
            {
                WriteOnce.Bytes(Path.Combine(output, "plain.npy"), NpyArray.Float32(new[] { 1f, 2f, 3f, 4f, 5f, 6f }, 2, 3).ToNpy());
                FaceFitWork.WriteNpz(Path.Combine(output, "plain.npz"), new[]
                {
                    new KeyValuePair<string, NpyArray>("ints", NpyArray.Int32(new[] { -1, 0, 7 })),
                    new KeyValuePair<string, NpyArray>("doubles", NpyArray.Float64(new[] { .25, Math.PI }, 1, 2)),
                    new KeyValuePair<string, NpyArray>("names", NpyArray.Unicode(new[] { "headSize", "EyePosition" })),
                    new KeyValuePair<string, NpyArray>("small", NpyArray.Int8(new sbyte[] { -3, 3 })),
                });
                TestContext.Progress.WriteLine("face-fit proof files in " + output);
            }
        }
    }
}
