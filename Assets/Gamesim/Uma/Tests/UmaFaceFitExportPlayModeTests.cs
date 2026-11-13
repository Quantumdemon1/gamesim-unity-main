using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UMA.CharacterSystem;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Uma.Tests
{
    /// <summary>
    /// The face-fit export tool (Gamesim.Uma.Editor's UmaFaceFitExport, reached by reflection as the lead's run reaches it)
    /// on real UMA bodies: two samples, a library look's person samples (the game's own merge) and a repeat of the
    /// baseline into a folder outside the project, the files it
    /// writes, the export frame, that a slider moves vertices and a repeat does not, that a finished file is never
    /// overwritten, that a stopped run resumes without redoing a sample, and that a wrong job stops before anything is built.
    /// Stock UMA data only; the temporary folder is deleted.
    /// </summary>
    public sealed class UmaFaceFitExportPlayModeTests
    {
        private const string Race = UmaCastLibrary.MaleRace;
        private const string Commit = "0123456789abcdef0123456789abcdef01234567";
        private string folder;
        private GameObject stage;

        [SetUp]
        public void SetUp()
        {
            // UMA warns about bones some parts lack; what is checked here comes back as the tool's outcome and files.
            LogAssert.ignoreFailingMessages = true;
            folder = Path.Combine(Path.GetTempPath(), "gamesim-facefit-u6-" + Guid.NewGuid().ToString("N").Substring(0, 12));
            Directory.CreateDirectory(folder);
            string project = Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
            Assert.That(Path.GetFullPath(folder).StartsWith(project, StringComparison.OrdinalIgnoreCase), Is.False, "the test's folder is outside the project");
        }

        [TearDown]
        public void TearDown()
        {
            if (stage != null) UnityEngine.Object.Destroy(stage);
            if (folder != null && Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>A public regular-season look of the race: its person samples go through the game's own merge (no All-Star needed).</summary>
        private const string Person = "alex-chen";

        private string Job(string name, string output, string extra = "", string persons = "")
        {
            string path = Path.Combine(folder, name + ".json");
            File.WriteAllText(path, "{\n  \"mode\": \"sweep\",\n  \"race\": \"" + Race + "\",\n  \"gameCommit\": \"" + Commit + "\",\n  \"outputFolder\": \""
                + output.Replace("\\", "\\\\") + "\",\n  \"plan\": {\"sweepNames\": [\"jawsSize\"], \"positions\": [0.75], \"randomCount\": 0},\n  \"persons\": [" + persons + "]" + extra + "\n}\n");
            return path;
        }

        private static IEnumerator Export(string job, List<string> outcomes)
        {
            string outcome = null;
            yield return UmaFaceFitTool.Run(job, line => Debug.Log("[face-fit test] " + line), result => outcome = result);
            Assert.That(outcome, Is.Not.Null, "the export ended without an outcome");
            Debug.Log("[face-fit test] outcome: " + outcome);
            outcomes.Add(outcome);
        }

        private static Dictionary<string, DateTime> Stamps(string root) =>
            Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(f => f, File.GetLastWriteTimeUtc);

        // ------------------------------------------------------------------ U6: the export's files

        [UnityTest, Timeout(900000)]
        public IEnumerator TwoSamplesExport_WithTheirManifestAndArrays_AndASecondRunRedoesNothing()
        {
            string output = Path.Combine(folder, "export");
            Assert.That(UmaCastLibrary.TryGet(Person, out var look) && look.Race == Race, Is.True, Person + " is a " + Race + " look of the library");
            string job = Job("job", output, persons: "\"" + Person + "\"");
            var outcomes = new List<string>();
            yield return Export(job, outcomes);
            Assert.That(outcomes[0], Does.StartWith("complete"), outcomes[0]);

            // The manifest: what the contract's row S1 asks of it.
            var manifest = File.ReadAllText(Path.Combine(output, "manifest.json"));
            Assert.That(manifest, Does.Not.Contain("\r"), "LF only");
            foreach (string field in new[] { "\"format\": \"gamesim-face-export\"", "\"gameCommit\": \"" + Commit + "\"", "\"race\": \"" + Race + "\"",
                "\"units\": \"metres\"", "\"midsagittal\"", "\"LeftEarAdjust\": [", "\"RightEarAdjust\": [", "\"headSymmetryMm\"", "\"p99\"", "\"median\"",
                "\"noiseFloorMm\"", "\"topologyChanged\": false", "\"dnaNames\"", "\"faceNames\"", "\"umaAssets\"", "\"samplesFile\"", "\"readbackWithin1e5\": true",
                "\"complete\": true", "\"regionRule\": \"dominant-bone-v1\"", "\"regionBoneMap\"", "\"regionCounts\"", "\"baselineNames\"",
                "\"unrequestedReadBack\"", "\"source\": \"uma-default\"", "\"source\": \"house\"", "\"plan\"" })
                Assert.That(manifest, Does.Contain(field), field + " is in the manifest");
            Assert.That(manifest, Does.Not.Contain("\"readbackWithin1e5\": false"), "every sample read back its request within 1e-5");
            double noise = Number(manifest, "noiseFloorMm");
            Debug.Log("[face-fit test] noise floor (baseline built twice): " + noise.ToString("R", CultureInfo.InvariantCulture) + " mm; head symmetry max "
                + Number(manifest, "max") + " mm, p99 " + Number(manifest, "p99") + " mm, median " + Number(manifest, "median") + " mm");
            Assert.That(noise, Is.LessThan(.01), "the same DNA built twice lands within a hundredth of a millimetre");

            // The arrays: six samples (baseline a, jawsSize .75, the house-only build, the person twice, baseline b), every
            // slot's vertices, every DNA name.
            var npz = NpzPeek.Read(Path.Combine(output, "samples.npz"));
            var ids = npz["sample_ids"].Strings();
            Assert.That(ids, Is.EqualTo(new[] { "baseline-a", "slider-jawsSize-0.750", "unrequested", "person-" + Person + "-a", "person-" + Person + "-b", "baseline-b" }));
            const int PersonA = 3, PersonB = 4, BaselineB = 5, Samples = 6;
            int v = npz["slot_count"].Ints().Sum();
            int d = npz["dna_names"].Strings().Length;
            Assert.That(v, Is.GreaterThan(1000), "a whole body's vertices");
            Assert.That(npz["positions"].Shape, Is.EqualTo(new[] { Samples, v, 3 }));
            Assert.That(npz["normals"].Shape, Is.EqualTo(new[] { Samples, v, 3 }));
            Assert.That(npz["requested"].Shape, Is.EqualTo(new[] { Samples, d }));
            Assert.That(npz["readback"].Shape, Is.EqualTo(new[] { Samples, d }));
            Assert.That(npz["dominant_bone"].Shape, Is.EqualTo(new[] { v }));
            Assert.That(npz["uv0"].Shape, Is.EqualTo(new[] { v, 2 }));
            Assert.That(npz["bone_weights"].Shape, Is.EqualTo(new[] { v, 4 }));
            Assert.That(npz["vertex_region"].Shape, Is.EqualTo(new[] { v }));
            Assert.That(npz["triangles"].Shape[1], Is.EqualTo(3));
            Assert.That(npz["positions"].Descr, Is.EqualTo("<f4"));
            Assert.That(npz["requested"].Descr, Is.EqualTo("<f8"));
            var slots = npz["slot_names"].Strings();
            Debug.Log("[face-fit test] " + v + " vertices in " + slots.Length + " slots (" + string.Join(", ", slots) + "), " + d + " DNA names");
            Assert.That(slots.Count(s => s.StartsWith("UMA30_Body_UDIM1001_slot", StringComparison.Ordinal)), Is.EqualTo(1), "one head slot");

            var names = npz["dna_names"].Strings().ToList();
            var requested = npz["requested"].Doubles();
            var readback = npz["readback"].Doubles();
            int jaw = names.IndexOf("jawsSize");
            Assert.That(jaw, Is.GreaterThanOrEqualTo(0));
            Assert.That(requested[1 * d + jaw], Is.EqualTo(.75).Within(1e-9));
            for (int i = 0; i < requested.Length; i++)
                if (!double.IsNaN(requested[i]))
                    Assert.That(readback[i], Is.EqualTo(requested[i]).Within(1e-5), names[i % d] + " of sample " + ids[i / d] + " reads back as requested");
            Assert.That(Enumerable.Range(0, d).All(n => !double.IsNaN(requested[n])), Is.True, "the race baseline requests every name");

            // A person's sample is the game's own merge: it requests exactly the names the game sets (the house proportions'
            // and the look's Dna's), at the values the game's build read back, and the bare body reproduces them.
            var gameSets = new HashSet<string>(UmaCastLibrary.HouseProportions.Keys.Concat(look.Dna.Keys).Where(names.Contains));
            foreach (int row in new[] { PersonA, PersonB })
            {
                var asked = new HashSet<string>(Enumerable.Range(0, d).Where(n => !double.IsNaN(requested[row * d + n])).Select(n => names[n]));
                Assert.That(asked, Is.EquivalentTo(gameSets), ids[row] + " requests exactly the names the game's merge sets");
            }
            Assert.That(manifest, Does.Contain("\"gameVsBareAgree\": true"), "the bare body reproduces the game's read-back");
            Assert.That(manifest, Does.Not.Contain("\"gameVsBareAgree\": false"));
            Assert.That(Number(manifest, "gameVsBareMaxDifference"), Is.LessThanOrEqualTo(1e-5));

            // A name nobody sets is requested at the default UMA holds for it, never at .5 by our own hand: EyePosition's.
            int eye = names.IndexOf("EyePosition");
            Assert.That(eye, Is.GreaterThanOrEqualTo(0), "the race has EyePosition");
            double eyeDefault = double.NaN;
            stage = new GameObject("face-fit default probe");
            DynamicCharacterAvatar probe = null;
            string problem = null;
            yield return UmaFaceFitTool.BuildBare(Race, stage.transform, (built, error) => { probe = built; problem = error; });
            Assert.That(probe, Is.Not.Null, "a bare body to read UMA's own default from: " + problem);
            foreach (var group in probe.umaData.umaRecipe.dnaInstanceCollection.GetDNAByGroup().Keys)
                if (group != null && group.dnaList.Any(dna => dna != null && dna.name == "EyePosition")) eyeDefault = group.GetDefaultValue("EyePosition");
            Debug.Log("[face-fit test] EyePosition: requested at the baseline " + requested[eye] + ", UMA's own default " + eyeDefault + ", the unrequested build reads back " + readback[2 * d + eye]);
            Assert.That(requested[eye], Is.EqualTo(eyeDefault).Within(1e-6), "EyePosition is requested at UMA's own default");
            Assert.That(requested[eye], Is.Not.EqualTo(.5).Within(1e-6), "and not at .5");

            // A changed slider moves vertices; the repeated baseline does not.
            var positions = npz["positions"].Floats();
            double Max(int a, int b)
            {
                double max = 0;
                for (int x = 0; x < v * 3; x += 3)
                {
                    double dx = positions[a * v * 3 + x] - positions[b * v * 3 + x], dy = positions[a * v * 3 + x + 1] - positions[b * v * 3 + x + 1], dz = positions[a * v * 3 + x + 2] - positions[b * v * 3 + x + 2];
                    max = Math.Max(max, Math.Sqrt(dx * dx + dy * dy + dz * dz) * 1000);
                }
                return max;
            }
            double slider = Max(1, 0), repeat = Max(BaselineB, 0), person = Max(PersonB, PersonA);
            Debug.Log("[face-fit test] jawsSize .75 moves a vertex up to " + slider + " mm; the repeated baseline " + repeat + " mm; the person built twice " + person + " mm");
            Assert.That(slider, Is.GreaterThan(.5), "jawsSize .75 moves the jaw");
            Assert.That(repeat, Is.LessThan(.01), "the repeated baseline stays put");
            Assert.That(person, Is.LessThan(.01), "the person built twice lands within a hundredth of a millimetre (their noise floor)");
            var moved = npz["moved_sample"].Ints().Distinct().ToArray();
            Assert.That(moved, Has.Member(1), "the slider sample has moved vertices");
            Assert.That(moved, Has.Member(PersonA), "the person (another height and build) differs from the house baseline");
            Assert.That(moved, Has.No.Member(BaselineB), "the repeated baseline has none");

            // The same job again: nothing is redone, nothing rewritten.
            var before = Stamps(output);
            yield return Export(job, outcomes);
            Assert.That(outcomes[1], Does.StartWith("complete"), outcomes[1]);
            Assert.That(Stamps(output), Is.EqualTo(before), "a second run of the same job rewrites no file");

            // Another job aimed at the same folder is refused; the finished files stay as they are.
            string other = Job("other", output, ",\n  \"description\": \"another job\"");
            yield return Export(other, outcomes);
            Assert.That(outcomes[2], Does.StartWith("error"), outcomes[2]);
            Assert.That(outcomes[2], Does.Contain("another job").Or.Contain("not this job's"), outcomes[2]);
            Assert.That(Stamps(output).Where(pair => before.ContainsKey(pair.Key)), Is.EqualTo(before), "the finished export is untouched");
        }

        /// <summary>The first number-valued field of that name in the manifest (the description holds same-named text fields).</summary>
        private static double Number(string json, string field)
        {
            string key = "\"" + field + "\": ";
            for (int at = json.IndexOf(key, StringComparison.Ordinal); at >= 0; at = json.IndexOf(key, at + 1, StringComparison.Ordinal))
            {
                int start = at + key.Length, end = start;
                while (end < json.Length && "+-0123456789.eE".IndexOf(json[end]) >= 0) end++;
                if (end > start) return double.Parse(json.Substring(start, end - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            Assert.Fail(field + " is a number in the manifest");
            return double.NaN;
        }

        // ------------------------------------------------------------------ U6: resume

        /// <summary>
        /// A budget too short for more than one sample stops each run cleanly ("resume: ..."); the same job run again
        /// continues where the last stopped and never builds a finished sample again.
        /// </summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator AStoppedRunResumes_AndRedoesNoFinishedSample()
        {
            string output = Path.Combine(folder, "resume");
            string job = Job("short", output, ",\n  \"budgetMinutes\": 0.0001");
            var outcomes = new List<string>();
            var stamps = new Dictionary<string, DateTime>();
            for (int run = 0; run < 6 && (outcomes.Count == 0 || !outcomes.Last().StartsWith("complete", StringComparison.Ordinal)); run++)
            {
                yield return Export(job, outcomes);
                Assert.That(outcomes.Last(), Does.StartWith("resume").Or.StartWith("complete"), outcomes.Last());
                foreach (var file in Directory.GetFiles(output, "*.json", SearchOption.AllDirectories).Where(f => Path.GetFileName(f).StartsWith("baseline-") || Path.GetFileName(f).StartsWith("slider-") || Path.GetFileName(f) == "unrequested.json"))
                {
                    if (stamps.TryGetValue(file, out var stamp)) Assert.That(File.GetLastWriteTimeUtc(file), Is.EqualTo(stamp), Path.GetFileName(file) + " was not built again");
                    else stamps[file] = File.GetLastWriteTimeUtc(file);
                }
            }
            Assert.That(outcomes.First(), Does.StartWith("resume"), "the budget stopped the first run");
            Assert.That(outcomes.First(), Does.Contain("run the same job again"));
            Assert.That(outcomes.Last(), Does.StartWith("complete"), string.Join("\n", outcomes));
            Assert.That(stamps.Count, Is.EqualTo(4), "four samples, each built once");
            Assert.That(Directory.GetFiles(output, "run-stopped-*.json").Length, Is.EqualTo(outcomes.Count - 1), "each stopped run left its record of what is present and missing");
        }

        // ------------------------------------------------------------------ U6: the export frame

        /// <summary>
        /// The export frame: the root at the origin, unturned, scale 1, T-pose after DNA. A body whose root has wandered,
        /// turned and grown, and whose arm and head an animator and a hand have posed, captures exactly as it did standing
        /// at the origin, because the capture is taken in the root's frame inside the DNA rebuild that resets the pose.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator APosedMovedTurnedBody_CapturesAsItDoesStandingAtTheOrigin()
        {
            stage = new GameObject("face-fit frame stage");
            DynamicCharacterAvatar avatar = null;
            string error = null;
            yield return UmaFaceFitTool.BuildBare(Race, stage.transform, (built, problem) => { avatar = built; error = problem; });
            Assert.That(avatar, Is.Not.Null, "a bare " + Race + " body was built: " + error);
            var dna = new Dictionary<string, float> { ["headSize"] = .72f, ["jawsSize"] = .5f };

            float[] standing = null;
            yield return UmaFaceFitTool.Capture(avatar, dna, 60, (positions, problem) => { standing = positions; error = problem; });
            Assert.That(standing, Is.Not.Null, "the standing capture: " + error);

            // Wander, turn, grow; give it the houseguest controller and let it play; bend an arm and the head by hand.
            var root = avatar.transform;
            root.position = new Vector3(2.5f, .3f, -4f);
            root.rotation = Quaternion.Euler(10f, 90f, 15f);
            root.localScale = Vector3.one * 1.3f;
            var animator = avatar.GetComponent<Animator>();
            var handle = Resources.Load<AnimatorOverrideController>("Animation/GamesimHumanoid");
            if (animator != null && handle != null && handle.runtimeAnimatorController != null)
            {
                animator.runtimeAnimatorController = handle.runtimeAnimatorController;
                animator.enabled = true;
            }
            double until = Time.realtimeSinceStartupAsDouble + .5;
            while (Time.realtimeSinceStartupAsDouble < until) yield return null;
            var bones = avatar.GetComponentsInChildren<Transform>(true);
            var arm = bones.FirstOrDefault(t => t.name == "LeftArm");
            var head = bones.FirstOrDefault(t => t.name == "Head");
            Assert.That(arm, Is.Not.Null, "the body has a LeftArm bone");
            Assert.That(head, Is.Not.Null, "the body has a Head bone");
            arm.localRotation *= Quaternion.Euler(0f, 0f, 60f);
            head.localRotation *= Quaternion.Euler(25f, 30f, 0f);

            float[] disturbed = null;
            yield return UmaFaceFitTool.Capture(avatar, dna, 60, (positions, problem) => { disturbed = positions; error = problem; });
            Assert.That(disturbed, Is.Not.Null, "the disturbed capture: " + error);
            Assert.That(disturbed.Length, Is.EqualTo(standing.Length));
            double worst = 0;
            for (int x = 0; x < standing.Length; x += 3)
            {
                double dx = standing[x] - disturbed[x], dy = standing[x + 1] - disturbed[x + 1], dz = standing[x + 2] - disturbed[x + 2];
                worst = Math.Max(worst, Math.Sqrt(dx * dx + dy * dy + dz * dz));
            }
            Debug.Log("[face-fit test] a posed, moved, turned and scaled body captures within " + (worst * 1000).ToString("0.######", CultureInfo.InvariantCulture) + " mm of the standing one");
            Assert.That(worst, Is.LessThan(1e-5), "no pose, place, turn or scale reaches the exported vertices (largest difference " + worst * 1000 + " mm)");

            // And the frame is the root's: the body stands on the ground at x = 0, its head up.
            float minY = float.MaxValue, maxY = float.MinValue, sumX = 0;
            for (int x = 0; x < standing.Length; x += 3) { minY = Mathf.Min(minY, standing[x + 1]); maxY = Mathf.Max(maxY, standing[x + 1]); sumX += standing[x]; }
            Debug.Log("[face-fit test] standing body: feet at y " + minY + ", top at y " + maxY + ", mean x " + sumX / (standing.Length / 3));
            Assert.That(minY, Is.EqualTo(0f).Within(.1f), "the feet are at the root");
            Assert.That(maxY, Is.InRange(1f, 2.5f), "the head is up, a body's height above the root");
            Assert.That(sumX / (standing.Length / 3), Is.EqualTo(0f).Within(.05f), "the body is centred on x = 0");
        }

        // ------------------------------------------------------------------ U6: a wrong job

        [UnityTest]
        public IEnumerator AJobWritingIntoTheProject_OrNamingNoCommit_StopsBeforeAnythingIsBuilt()
        {
            string inside = Path.Combine(Application.dataPath, "FaceFitExportMustNotExist");
            var outcomes = new List<string>();
            yield return Export(Job("inside", inside), outcomes);
            Assert.That(outcomes[0], Does.StartWith("error"), outcomes[0]);
            Assert.That(outcomes[0], Does.Contain("inside the project"), outcomes[0]);
            Assert.That(Directory.Exists(inside), Is.False, "nothing was written under Assets");

            string path = Path.Combine(folder, "nocommit.json");
            File.WriteAllText(path, "{\"mode\": \"sweep\", \"race\": \"" + Race + "\", \"outputFolder\": \"" + Path.Combine(folder, "x").Replace("\\", "\\\\") + "\"}");
            yield return Export(path, outcomes);
            Assert.That(outcomes[1], Does.Contain("\"gameCommit\" is missing"), outcomes[1]);
            Assert.That(Directory.Exists(Path.Combine(folder, "x")), Is.False, "a wrong job writes nothing");
        }

        /// <summary>A reader of the .npz the export writes (stored members, .npy format 1.0), so the test checks the bytes themselves.</summary>
        private sealed class NpzPeek
        {
            public string Descr;
            public int[] Shape;
            public byte[] Data;

            public static Dictionary<string, NpzPeek> Read(string path)
            {
                var bytes = File.ReadAllBytes(path);
                var members = new Dictionary<string, NpzPeek>();
                int at = 0;
                while (at + 30 <= bytes.Length && BitConverter.ToUInt32(bytes, at) == 0x04034b50u)
                {
                    Assert.That(BitConverter.ToUInt16(bytes, at + 8), Is.EqualTo(0), "members are stored");
                    int size = (int)BitConverter.ToUInt32(bytes, at + 18);
                    int nameLength = BitConverter.ToUInt16(bytes, at + 26), extra = BitConverter.ToUInt16(bytes, at + 28);
                    string name = Encoding.UTF8.GetString(bytes, at + 30, nameLength);
                    int data = at + 30 + nameLength + extra;
                    Assert.That(bytes[data], Is.EqualTo(0x93), name + " is an .npy");
                    int headerLength = bytes[data + 8] | (bytes[data + 9] << 8);
                    string header = Encoding.ASCII.GetString(bytes, data + 10, headerLength);
                    string descr = header.Split(new[] { "'descr': '" }, StringSplitOptions.None)[1].Split('\'')[0];
                    string dims = header.Split(new[] { "'shape': (" }, StringSplitOptions.None)[1].Split(')')[0];
                    var member = new NpzPeek
                    {
                        Descr = descr,
                        Shape = dims.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0)
                            .Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray(),
                        Data = new byte[size - 10 - headerLength],
                    };
                    Buffer.BlockCopy(bytes, data + 10 + headerLength, member.Data, 0, member.Data.Length);
                    members[name.EndsWith(".npy") ? name.Substring(0, name.Length - 4) : name] = member;
                    at = data + size;
                }
                Assert.That(members, Is.Not.Empty, path + " holds .npy members");
                return members;
            }

            public float[] Floats() { Assert.That(Descr, Is.EqualTo("<f4")); var v = new float[Data.Length / 4]; Buffer.BlockCopy(Data, 0, v, 0, Data.Length); return v; }
            public double[] Doubles() { Assert.That(Descr, Is.EqualTo("<f8")); var v = new double[Data.Length / 8]; Buffer.BlockCopy(Data, 0, v, 0, Data.Length); return v; }
            public int[] Ints() { Assert.That(Descr, Is.EqualTo("<i4")); var v = new int[Data.Length / 4]; Buffer.BlockCopy(Data, 0, v, 0, Data.Length); return v; }

            public string[] Strings()
            {
                Assert.That(Descr, Does.StartWith("<U"));
                int width = int.Parse(Descr.Substring(2), CultureInfo.InvariantCulture) * 4;
                var utf32 = new UTF32Encoding(false, false);
                return Enumerable.Range(0, Data.Length / width).Select(i => utf32.GetString(Data, i * width, width).TrimEnd('\0')).ToArray();
            }
        }
    }
}
