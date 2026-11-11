using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Gamesim.Uma.Editor
{
    // The face-fit export's work folder and its final assembly, with nothing of Unity's in them (the dotnet subset
    // compiles this file). A run writes one .npz and one .json per sample into <output>/.work-<job sha256 16>/; a
    // sample is finished when its .json exists (written after its .npz, both under a temporary name, then renamed).
    // When every sample is finished the run stacks them into samples.npz (or one file per read-back candidate) and
    // writes manifest.json last.

    /// <summary>One built body as the Unity side captured it: plain arrays in the export frame (metres, root at the origin).</summary>
    internal sealed class FaceFitCapture
    {
        public string[] SlotNames;
        public int[] SlotOffset, SlotCount;
        public float[] Positions, Normals;      // V x 3
        public int[] Triangles;                 // T x 3, rows of Positions
        public float[] Uv0;                     // V x 2
        public int[] BoneIndices;               // V x 4, the four largest weights
        public float[] BoneWeights;             // V x 4
        public string[] BoneNames;
        public int[] BoneParents;               // -1 for a bone whose parent is not a bone of the mesh
        public float[] BindPoses;               // B x 4 x 4, row-major
        public float[] BoneLocal;               // B x 10: local position xyz, rotation xyzw, scale xyz (after DNA)
        public float[] BonePosition;            // B x 3, in the export frame
        public string[] DnaNames;
        public double[] Requested, Readback;    // D, in DnaNames order
        public double Seconds;
        public JsonObject Extra = new JsonObject();

        public int VertexCount => Positions.Length / 3;

        /// <summary>The topology's fingerprint: slot names and counts, and the triangles.</summary>
        public string TopologyKey()
        {
            var bytes = new List<byte>();
            for (int k = 0; k < SlotNames.Length; k++)
            {
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(SlotNames[k] + ":" + SlotCount[k] + ";"));
            }
            var tris = new byte[Triangles.Length * 4];
            Buffer.BlockCopy(Triangles, 0, tris, 0, tris.Length);
            bytes.AddRange(tris);
            return Sha256.Hex(bytes.ToArray());
        }

        public double MaxReadbackError()
        {
            double max = 0;
            for (int d = 0; d < Requested.Length; d++)
            {
                // A name nobody requested (NaN) has no request to meet; every sample requests every name, so none is skipped there.
                if (double.IsNaN(Requested[d])) continue;
                max = Math.Max(max, Math.Abs(Requested[d] - Readback[d]));
            }
            return max;
        }
    }

    internal static class FaceFitWork
    {
        public const double ReadbackTolerance = 1e-5;
        public const string ManifestName = "manifest.json", SamplesName = "samples.npz", TopologyName = "topology";

        /// <summary>
        /// A read-back round's manifest: one per race in r&lt;n&gt;/ (manifest-&lt;race slug&gt;.json), so the male and the
        /// female candidates of one round share contract S4's folder. A plain manifest.json there is never this tool's.
        /// </summary>
        public static string RoundManifestName(string race) => "manifest-" + FaceFitPlan.RaceSlug(race) + ".json";

        public static string RoundFolder(string outputFolder, int round) => Path.Combine(outputFolder, "r" + round.ToString(CultureInfo.InvariantCulture));

        public static string Folder(string outputFolder, string jobSha) => Path.Combine(outputFolder, ".work-" + jobSha.Substring(0, 16));

        public static bool IsFinished(string work, string id) => File.Exists(Path.Combine(work, id + ".json"));

        /// <summary>Opens (or starts) the work folder of a job: refuses a folder another job started.</summary>
        public static string Open(FaceFitJob job, out int partsDeleted)
        {
            Directory.CreateDirectory(job.OutputFolder);
            string work = Folder(job.OutputFolder, job.Sha);
            Directory.CreateDirectory(work);
            partsDeleted = WriteOnce.DeleteParts(work);
            string copy = Path.Combine(work, "job.json");
            if (!File.Exists(copy)) WriteOnce.Text(copy, job.Text);
            else if (Sha256.File(copy) != Sha256.Text(job.Text.Replace("\r\n", "\n")))
                throw new IOException("the work folder " + work + " holds a different job; give this job its own output folder");
            return work;
        }

        public static void WriteNpz(string path, IEnumerable<KeyValuePair<string, NpyArray>> members)
        {
            WriteOnce.Write(path, stream =>
            {
                using (var npz = new NpzWriter(stream))
                    foreach (var member in members) npz.Add(member.Key, member.Value);
            });
        }

        public static Dictionary<string, NpyArray> ReadNpz(string path) => NpzWriter.Read(File.ReadAllBytes(path));

        /// <summary>
        /// Puts a finished file in its final place without ever replacing one: it is written whole in the work folder,
        /// its sha256 recorded there, then renamed into place. A run stopped at any point leaves either nothing in place
        /// (the next run writes it again) or this job's own file, which the record proves and the next run keeps. A file
        /// in place that the record does not prove is another job's, and the run stops.
        /// </summary>
        public static string Publish(string work, string finalPath, string tag, Action<string> write)
        {
            string staged = Path.Combine(work, "publish-" + tag);
            string record = Path.Combine(work, "published-" + tag + ".json");
            string Recorded() => File.Exists(record) ? (string)((JsonObject)Json.Parse(File.ReadAllText(record)))["sha256"] : null;
            if (File.Exists(finalPath))
            {
                string sha = Sha256.File(finalPath);
                if (Recorded() == sha) return sha;
                throw new IOException("refusing to overwrite the finished file " + finalPath + ": it is not this job's");
            }
            if (File.Exists(staged) && Recorded() != Sha256.File(staged)) File.Delete(staged);
            if (File.Exists(record) && !File.Exists(staged)) File.Delete(record);
            if (!File.Exists(staged)) write(staged);
            string staging = Sha256.File(staged);
            if (!File.Exists(record)) WriteOnce.Text(record, Json.Write(new JsonObject().Set("file", finalPath).Set("sha256", staging)));
            File.Move(staged, finalPath);
            return staging;
        }

        private static KeyValuePair<string, NpyArray> M(string name, NpyArray array) => new KeyValuePair<string, NpyArray>(name, array);

        /// <summary>
        /// The topology (from the first capture), once: slots, triangles, UVs, weights, bones, every vertex's region and
        /// dominant bone. Throws when the region rule does not fit the rig: no head slot, or a head slot without a face or
        /// ears vertex (the lead's self-check on dominant-bone-v1).
        /// </summary>
        public static void WriteTopology(string work, FaceFitCapture c, IList<string> headSlots)
        {
            string path = Path.Combine(work, TopologyName + ".npz");
            if (File.Exists(path)) return;
            int v = c.VertexCount, b = c.BoneNames.Length;
            var regions = FaceFitStats.Regions(c.SlotNames, c.SlotOffset, c.SlotCount, headSlots, c.BoneIndices, c.BoneWeights, c.BoneNames, c.BoneParents,
                out var regionNames, out var dominant);

            // The rule's own record: per slot the vertices of each region, and every bone that decided a head-slot vertex.
            var counts = new JsonObject();
            var boneMap = new SortedDictionary<string, string>(StringComparer.Ordinal);
            int head = Array.FindIndex(c.SlotNames, name => FaceFitStats.SlotMatches(name, headSlots[0]));
            for (int k = 0; k < c.SlotNames.Length; k++)
            {
                var perRegion = new int[regionNames.Count];
                for (int x = c.SlotOffset[k]; x < c.SlotOffset[k] + c.SlotCount[k]; x++)
                {
                    perRegion[regions[x]]++;
                    if (k == head && dominant[x] >= 0) boneMap[c.BoneNames[dominant[x]]] = regionNames[regions[x]];
                }
                var slotCounts = new JsonObject();
                for (int r = 0; r < regionNames.Count; r++) if (perRegion[r] > 0) slotCounts.Set(regionNames[r], perRegion[r]);
                counts.Set(c.SlotNames[k], slotCounts);
            }
            if (head < 0) throw new InvalidDataException("the head slot " + headSlots[0] + " is not on the built body, so the regions cannot be drawn");
            foreach (string needed in new[] { "face", "ears" })
                if (!(((JsonObject)counts[c.SlotNames[head]])[needed] is int n) || n == 0)
                    throw new InvalidDataException("the region rule " + FaceFitStats.RegionRule + " found no " + needed + " vertex in " + c.SlotNames[head]
                        + ": it does not match this rig (regions there: " + Json.Write(counts[c.SlotNames[head]]).Trim() + ")");

            WriteNpz(path, new[]
            {
                M("dna_names", NpyArray.Unicode(c.DnaNames)),
                M("slot_names", NpyArray.Unicode(c.SlotNames)),
                M("slot_offset", NpyArray.Int32(c.SlotOffset)),
                M("slot_count", NpyArray.Int32(c.SlotCount)),
                M("triangles", NpyArray.Int32(c.Triangles, c.Triangles.Length / 3, 3)),
                M("uv0", NpyArray.Float32(c.Uv0, v, 2)),
                M("bone_indices", NpyArray.Int32(c.BoneIndices, v, 4)),
                M("bone_weights", NpyArray.Float32(c.BoneWeights, v, 4)),
                M("bone_names", NpyArray.Unicode(c.BoneNames)),
                M("bone_parents", NpyArray.Int32(c.BoneParents)),
                M("bind_poses", NpyArray.Float32(c.BindPoses, b, 4, 4)),
                M("vertex_region", NpyArray.Int8(regions)),
                M("region_names", NpyArray.Unicode(regionNames)),
                M("dominant_bone", NpyArray.Int32(dominant)),
            });
            var map = new JsonObject();
            foreach (var pair in boneMap) map.Set(pair.Key, pair.Value);
            WriteOnce.Text(Path.Combine(work, TopologyName + ".json"), Json.Write(new JsonObject().Set("topologyKey", c.TopologyKey())
                .Set("regionCounts", counts).Set("regionBoneMap", map)));
        }

        /// <summary>One finished sample: its arrays, then its record (the record's existence is what "finished" means).</summary>
        public static void WriteSample(string work, PlannedSample s, FaceFitCapture c, string topologyKey)
        {
            int v = c.VertexCount, b = c.BoneNames.Length;
            WriteNpz(Path.Combine(work, s.Id + ".npz"), new[]
            {
                M("positions", NpyArray.Float32(c.Positions, v, 3)),
                M("normals", NpyArray.Float32(c.Normals, v, 3)),
                M("bone_local", NpyArray.Float32(c.BoneLocal, b, 10)),
                M("bone_position", NpyArray.Float32(c.BonePosition, b, 3)),
                M("requested", NpyArray.Float64(c.Requested)),
                M("readback", NpyArray.Float64(c.Readback)),
            });
            var requested = new JsonObject();
            var readback = new JsonObject();
            for (int d = 0; d < c.DnaNames.Length; d++)
            {
                // A name the sample did not request (NaN) is left out of "requested" and still read back.
                if (!double.IsNaN(c.Requested[d])) requested.Set(c.DnaNames[d], c.Requested[d]);
                readback.Set(c.DnaNames[d], c.Readback[d]);
            }
            var slots = new List<object>();
            for (int k = 0; k < c.SlotNames.Length; k++)
                slots.Add(new JsonObject().Set("name", c.SlotNames[k]).Set("offset", c.SlotOffset[k]).Set("count", c.SlotCount[k]));
            string key = c.TopologyKey();
            var record = new JsonObject()
                .Set("id", s.Id).Set("kind", s.Kind).Set("name", s.Name).Set("value", double.IsNaN(s.Value) ? null : (object)s.Value)
                .Set("appearanceId", s.AppearanceId).Set("round", s.Round == 0 ? null : (object)s.Round).Set("repeat", s.Repeat)
                .Set("positionSource", s.PositionSource).Set("fresh", s.Fresh)
                .Set("vertices", v).Set("bones", b).Set("topologyKey", key).Set("topologyChanged", topologyKey != null && key != topologyKey)
                .Set("maxReadbackError", c.MaxReadbackError()).Set("readbackWithin1e5", c.MaxReadbackError() <= ReadbackTolerance)
                .Set("seconds", Math.Round(c.Seconds, 3)).Set("slots", slots).Set("requested", requested).Set("readback", readback);
            foreach (var pair in c.Extra) record.Set(pair.Key, pair.Value);
            WriteOnce.Text(Path.Combine(work, s.Id + ".json"), Json.Write(record));
        }

        public static JsonObject ReadRecord(string work, string id) => (JsonObject)Json.Parse(File.ReadAllText(Path.Combine(work, id + ".json")));

        /// <summary>A finished sample's requested values, by name (a person's vector is read back from its first build).</summary>
        public static Dictionary<string, double> RequestedOf(JsonObject record)
        {
            var values = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var pair in (JsonObject)record["requested"]) values[pair.Key] = (double)pair.Value;
            return values;
        }

        private static float[] Floats(string work, string id, string member) => ReadNpz(Path.Combine(work, id + ".npz"))[member].AsFloat32();

        private static byte[] Bytes(float[] values)
        {
            var bytes = new byte[values.Length * 4];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        private static IEnumerable<byte[]> Stacked(string work, IList<PlannedSample> plan, string member, int length)
        {
            foreach (var s in plan)
            {
                var values = Floats(work, s.Id, member);
                if (values.Length != length)
                {
                    // A sample whose topology changed cannot be stacked with the others: its row is NaN (the manifest says why).
                    values = new float[length];
                    for (int i = 0; i < length; i++) values[i] = float.NaN;
                }
                yield return Bytes(values);
            }
        }

        /// <summary>Per slot and per region of the reference topology, the largest distance between two captures, in mm.</summary>
        public static JsonObject[] Largest(float[] a, float[] b, Dictionary<string, NpyArray> topology)
        {
            var slotNames = topology["slot_names"].AsUnicode();
            var offsets = topology["slot_offset"].AsInt32();
            var counts = topology["slot_count"].AsInt32();
            var regions = topology["vertex_region"].AsInt8();
            var regionNames = topology["region_names"].AsUnicode();
            var bySlot = new JsonObject();
            var byRegion = new double[regionNames.Length];
            for (int k = 0; k < slotNames.Length; k++)
            {
                double max = 0;
                for (int v = offsets[k]; v < offsets[k] + counts[k]; v++)
                {
                    double d = FaceFitStats.DistanceMm(a, b, v);
                    if (d > max) max = d;
                    if (d > byRegion[regions[v]]) byRegion[regions[v]] = d;
                }
                bySlot.Set(slotNames[k], max);
            }
            var regionObject = new JsonObject();
            for (int r = 0; r < regionNames.Length; r++) regionObject.Set(regionNames[r], byRegion[r]);
            return new[] { bySlot, regionObject };
        }

        private static List<object> Point(float[] values, int index) => new List<object> { (double)values[index * 3], (double)values[index * 3 + 1], (double)values[index * 3 + 2] };

        /// <summary>The frame, the midsagittal plane, the two ear bones and the head slot's symmetry, measured on one capture.</summary>
        public static void Measure(JsonObject manifest, string work, string id, Dictionary<string, NpyArray> topology, IList<string> headSlots)
        {
            var positions = Floats(work, id, "positions");
            var bones = topology["bone_names"].AsUnicode();
            var bonePositions = Floats(work, id, "bone_position");
            manifest.Set("units", "metres");
            manifest.Set("frame", "the character's root at the world origin, no rotation, scale 1; T-pose after DNA (every Animator off, "
                + "vertices baked in the DNA rebuild's own CharacterUpdated); Unity axes (x right, y up, z forward); the midsagittal plane is x = 0");
            manifest.Set("midsagittal", new JsonObject().Set("point", new List<object> { 0.0, 0.0, 0.0 }).Set("normal", new List<object> { 1.0, 0.0, 0.0 }));
            var ears = new JsonObject().Set("measuredOn", id);
            foreach (string ear in new[] { "LeftEarAdjust", "RightEarAdjust" })
            {
                int at = Array.IndexOf(bones, ear);
                ears.Set(ear, at < 0 ? null : (object)Point(bonePositions, at));
            }
            manifest.Set("earAdjust", ears);
            var slotNames = topology["slot_names"].AsUnicode();
            var offsets = topology["slot_offset"].AsInt32();
            var counts = topology["slot_count"].AsInt32();
            int head = Array.FindIndex(slotNames, name => FaceFitStats.SlotMatches(name, headSlots[0]));
            var symmetry = new JsonObject().Set("measuredOn", id).Set("slot", head < 0 ? headSlots[0] + " (not built)" : slotNames[head]);
            if (head >= 0)
            {
                var stats = FaceFitStats.Symmetry(positions, offsets[head], counts[head]);
                symmetry.Set("vertices", counts[head]).Set("max", stats[0]).Set("p99", stats[1]).Set("median", stats[2])
                    .Set("method", "per vertex, the distance from its mirror image (-x, y, z) to the nearest vertex of the same slot; nearest-rank percentiles; printed, never a failing check");
            }
            manifest.Set("headSymmetryMm", symmetry);
        }

        public static JsonObject Description(string mode) => new JsonObject()
            .Set("what", mode == FaceFitJob.SweepMode
                ? "Slider-effect samples of UMA bodies built by Gamesim's UmaFaceFitExport tool. Every sample is a bare body of the race (no wardrobe: every body slot, nothing hidden or suppressed). A race sweep requests every DNA name explicitly; a person's sample, the unrequested build and a sweep around one look are built on a fresh bare body and request only what they set."
                : "Read-back of candidate slider values: each candidate is a fresh bare body requesting the person's own merge (house, then the look) with the candidate's values laid over it, compared with that person's sample built twice in the same run.")
            .Set("samples.npz", "positions/normals (S,V,3) float32 metres in the export frame; requested/readback (S,D) float64 in dna_names order, requested NaN where the sample did not request the name; slot_names/slot_offset/slot_count: rows offset..offset+count-1 are that slot's vertices, so the slot-local index is row - offset; triangles (T,3) rows; uv0 (V,2); bone_indices/bone_weights (V,4) the four largest skin weights; bone_names, bone_parents (-1: none), bind_poses (B,4,4) row-major; bone_local (S,B,10) local position, rotation xyzw, scale after DNA; bone_position (S,B,3) export frame; vertex_region (V,) index into region_names; dominant_bone (V,) the bone that decided the region (-1: none); moved_sample/moved_vertex/moved_mm: every vertex of every sample farther than movedThresholdMm from baseline-a")
            .Set("baseline", "source house = Gamesim's house proportions; look = that look's own merge (the names the game sets, house then the look's Dna, at the values read back from the game's build); values = given. In a race sweep every name the source does not set is requested at the default UMA holds for it (its DNA asset's defaultValue), listed per name in baselineNames with its source and read-back.")
            .Set("unrequestedReadBack", "every name read back from a fresh bare body that requested the house values and nothing else: what the race leaves an unrequested name at (information only)")
            .Set("person samples", "built through the game's own entry (UmaBodyProvider.TryCreate with the look's appearance id) and read back; then a fresh bare body requesting only the names the game's merge sets (the keys of the house proportions and of the look's Dna) at the game's read-back values, every other name left where the race builds it. gameReadback and gameVsBareMaxDifference record both read-backs. Built twice (a, b): their largest difference is that person's noise floor.")
            .Set("noiseFloorMm", "the largest vertex distance between two builds of the same request (baseline-a and baseline-b, or a person's a and b)")
            .Set("regions", "rule dominant-bone-v1 (regionSteps): inside the first head slot, the bone carrying a vertex's largest skin weight decides face, ears, skull, neck or body; regionBoneMap lists every bone that decided a vertex, regionCounts the vertices of each region per slot")
            .Set("readbackWithin1e5", "every DNA name the sample requested reads back from the built body within 1e-5 of the request")
            .Set("topologyChanged", "true when any sample's slots, vertex counts or triangles differ from the first capture's");

        /// <summary>The region rule's record, from the topology the first capture wrote.</summary>
        private static void Regions(JsonObject manifest, string work)
        {
            var topology = (JsonObject)Json.Parse(File.ReadAllText(Path.Combine(work, TopologyName + ".json")));
            manifest.Set("regionRule", FaceFitStats.RegionRule);
            manifest.Set("regionSteps", FaceFitStats.RegionSteps.ToList<object>());
            manifest.Set("regionBoneMap", topology["regionBoneMap"]);
            manifest.Set("regionCounts", topology["regionCounts"]);
        }

        /// <summary>The plan that was run: the swept names, each one's positions and where they came from, and the order.</summary>
        private static JsonObject PlanRecord(IList<PlannedSample> plan, IList<string> sweepNames)
        {
            var positions = new JsonObject();
            foreach (string name in sweepNames)
                positions.Set(name, plan.Where(p => p.Kind == PlannedSample.Slider && p.Name == name)
                    .Select(p => (object)new JsonObject().Set("value", p.Value).Set("source", p.PositionSource)).ToList());
            return new JsonObject()
                .Set("order", "baseline-a; every swept name at .25 and .75; at .375 and .625; at .5; at any other position; the job's extra samples; the random vectors; the unrequested build; each person a, b; baseline-b")
                .Set("sweepNames", sweepNames.ToList<object>())
                .Set("positions", positions)
                .Set("sampleIds", plan.Select(p => (object)p.Id).ToList());
        }

        private static readonly string[] TopologyMembers =
        {
            "slot_names", "slot_offset", "slot_count", "triangles", "uv0", "bone_indices", "bone_weights",
            "bone_names", "bone_parents", "bind_poses", "vertex_region", "region_names", "dominant_bone",
        };

        /// <summary>The sweep's samples.npz and manifest.json (written last), from a work folder whose every sample is finished.</summary>
        public static JsonObject AssembleSweep(FaceFitJob job, string work, IList<PlannedSample> plan, IList<string> sweepNames, JsonObject head)
        {
            var topology = ReadNpz(Path.Combine(work, TopologyName + ".npz"));
            string topologyKey = (string)((JsonObject)Json.Parse(File.ReadAllText(Path.Combine(work, TopologyName + ".json"))))["topologyKey"];
            var dnaNames = topology["dna_names"].AsUnicode();
            int v = topology["slot_count"].AsInt32().Sum(), b = topology["bone_names"].AsUnicode().Length, s = plan.Count, d = dnaNames.Length;
            var records = plan.Select(p => ReadRecord(work, p.Id)).ToList();
            var baseline = Floats(work, "baseline-a", "positions");

            var requested = new double[s * d];
            var readback = new double[s * d];
            var movedSample = new List<int>();
            var movedVertex = new List<int>();
            var movedMm = new List<float>();
            var entries = new List<object>();
            bool topologyChanged = false;
            double threshold = job.MovedThresholdMm;
            for (int i = 0; i < s; i++)
            {
                var arrays = ReadNpz(Path.Combine(work, plan[i].Id + ".npz"));
                Array.Copy(arrays["requested"].AsFloat64(), 0, requested, i * d, d);
                Array.Copy(arrays["readback"].AsFloat64(), 0, readback, i * d, d);
                var positions = arrays["positions"].AsFloat32();
                var record = records[i];
                bool changed = (string)record["topologyKey"] != topologyKey;
                topologyChanged |= changed;
                var entry = new JsonObject().Set("index", i);
                foreach (var key in new[] { "id", "kind", "name", "value", "positionSource", "appearanceId", "repeat", "fresh", "maxReadbackError", "readbackWithin1e5", "seconds" })
                    entry.Set(key, record[key]);
                entry.Set("topologyChanged", changed);
                if (plan[i].Overrides.Count > 0) entry.Set("overrides", new SortedDictionary<string, double>(plan[i].Overrides, StringComparer.Ordinal));
                foreach (var key in new[] { "gameReadback", "gameVsBareMaxDifference" })
                    if (record.TryGet(key, out var value)) entry.Set(key, value);
                if (!changed && positions.Length == baseline.Length)
                {
                    int moved = 0;
                    for (int x = 0; x < v; x++)
                    {
                        double mm = FaceFitStats.DistanceMm(positions, baseline, x);
                        if (mm <= threshold) continue;
                        movedSample.Add(i); movedVertex.Add(x); movedMm.Add((float)mm); moved++;
                    }
                    var largest = Largest(positions, baseline, topology);
                    entry.Set("movedVertices", moved).Set("maxMmBySlot", largest[0]).Set("maxMmByRegion", largest[1]);
                }
                entries.Add(entry);
            }

            var manifest = new JsonObject()
                .Set("format", "gamesim-face-export").Set("formatVersion", 1).Set("mode", job.Mode)
                .Set("description", Description(job.Mode))
                .Set("race", job.Race).Set("gameCommit", job.GameCommit);
            foreach (var pair in head) manifest.Set(pair.Key, pair.Value);
            manifest.Set("job", new JsonObject().Set("sha256", job.Sha).Set("copy", Path.GetFileName(work) + "/job.json").Set("text", job.Text));
            Measure(manifest, work, "baseline-a", topology, job.HeadSlots);
            manifest.Set("noiseFloorMm", plan.Any(p => p.Id == "baseline-b")
                ? (object)FaceFitStats.MaxMm(baseline, Floats(work, "baseline-b", "positions"), 0, v) : null);
            var persons = new List<object>();
            foreach (var person in plan.Where(p => p.Kind == PlannedSample.Person && p.Repeat == "a"))
            {
                string other = person.Id.Substring(0, person.Id.Length - 1) + "b";
                var a = Floats(work, person.Id, "positions");
                var bb = plan.Any(p => p.Id == other) ? Floats(work, other, "positions") : null;
                persons.Add(new JsonObject().Set("appearanceId", person.AppearanceId).Set("sample", person.Id)
                    .Set("noiseFloorMm", bb != null && bb.Length == a.Length ? (object)FaceFitStats.MaxMm(a, bb, 0, a.Length / 3) : null)
                    .Set("gameVsBareMaxDifference", records[plan.IndexOf(person)]["gameVsBareMaxDifference"]));
            }
            manifest.Set("persons", persons);

            // Per name of the baseline: what was requested, where the value came from, and what it read back as.
            var baselineFile = Path.Combine(work, "baseline.json");
            var sources = File.Exists(baselineFile) ? ((JsonObject)Json.Parse(File.ReadAllText(baselineFile)))["sources"] as JsonObject : null;
            var baselineRecord = records[plan.IndexOf(plan.First(p => p.Id == "baseline-a"))];
            var baselineRequested = (JsonObject)baselineRecord["requested"];
            var baselineReadback = (JsonObject)baselineRecord["readback"];
            manifest.Set("baselineNames", dnaNames.Select(name => (object)new JsonObject().Set("name", name)
                .Set("requested", baselineRequested[name])
                .Set("source", sources?[name] ?? (baselineRequested.TryGet(name, out _) ? "job" : "unrequested"))
                .Set("readBack", baselineReadback[name])).ToList());
            int unrequested = plan.ToList().FindIndex(p => p.Kind == PlannedSample.Unrequested);
            if (unrequested >= 0) manifest.Set("unrequestedReadBack", records[unrequested]["readback"]);

            manifest.Set("plan", PlanRecord(plan, sweepNames));
            manifest.Set("complete", true);
            manifest.Set("movedThresholdMm", threshold).Set("topologyChanged", topologyChanged);
            manifest.Set("dnaNames", dnaNames.ToList<object>());
            manifest.Set("headSlots", job.HeadSlots.ToList<object>());
            manifest.Set("regionNames", topology["region_names"].AsUnicode().ToList<object>());
            Regions(manifest, work);
            var slots = new List<object>();
            var slotNames = topology["slot_names"].AsUnicode();
            var offsets = topology["slot_offset"].AsInt32();
            var counts = topology["slot_count"].AsInt32();
            for (int k = 0; k < slotNames.Length; k++)
                slots.Add(new JsonObject().Set("name", slotNames[k]).Set("offset", offsets[k]).Set("count", counts[k])
                    .Set("head", job.HeadSlots.Any(h => FaceFitStats.SlotMatches(slotNames[k], h))));
            manifest.Set("slots", slots).Set("vertexCount", v).Set("boneCount", b).Set("sampleCount", s);
            manifest.Set("samples", entries);

            string samplesPath = Path.Combine(job.OutputFolder, SamplesName);
            string samplesSha = Publish(work, samplesPath, SamplesName, staged => WriteOnce.Write(staged, stream =>
            {
                using (var npz = new NpzWriter(stream))
                {
                    npz.Add("sample_ids", NpyArray.Unicode(plan.Select(p => p.Id).ToList()));
                    npz.Add("sample_kinds", NpyArray.Unicode(plan.Select(p => p.Kind).ToList()));
                    npz.Add("dna_names", topology["dna_names"]);
                    npz.Add("requested", NpyArray.Float64(requested, s, d));
                    npz.Add("readback", NpyArray.Float64(readback, s, d));
                    npz.Add("positions", "<f4", new[] { s, v, 3 }, () => Stacked(work, plan, "positions", v * 3));
                    npz.Add("normals", "<f4", new[] { s, v, 3 }, () => Stacked(work, plan, "normals", v * 3));
                    npz.Add("bone_local", "<f4", new[] { s, b, 10 }, () => Stacked(work, plan, "bone_local", b * 10));
                    npz.Add("bone_position", "<f4", new[] { s, b, 3 }, () => Stacked(work, plan, "bone_position", b * 3));
                    foreach (var name in TopologyMembers) npz.Add(name, topology[name]);
                    npz.Add("moved_sample", NpyArray.Int32(movedSample.ToArray()));
                    npz.Add("moved_vertex", NpyArray.Int32(movedVertex.ToArray()));
                    npz.Add("moved_mm", NpyArray.Float32(movedMm.ToArray()));
                }
            }));
            manifest.Set("samplesFile", new JsonObject().Set("name", SamplesName).Set("sha256", samplesSha));
            manifest.Set("finishedUtc", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            WriteOnce.Text(Path.Combine(job.OutputFolder, ManifestName), Json.Write(manifest));
            return manifest;
        }

        /// <summary>The read-back's files: per round r&lt;n&gt;/&lt;appearanceId&gt;.npz and r&lt;n&gt;/manifest-&lt;race slug&gt;.json (written last).</summary>
        public static List<JsonObject> AssembleReadback(FaceFitJob job, string work, IList<PlannedSample> plan, JsonObject head)
        {
            var topology = ReadNpz(Path.Combine(work, TopologyName + ".npz"));
            string topologyKey = (string)((JsonObject)Json.Parse(File.ReadAllText(Path.Combine(work, TopologyName + ".json"))))["topologyKey"];
            var manifests = new List<JsonObject>();
            foreach (int round in plan.Where(p => p.Kind == PlannedSample.Candidate).Select(p => p.Round).Distinct().OrderBy(r => r))
            {
                string folder = RoundFolder(job.OutputFolder, round);
                Directory.CreateDirectory(folder);
                if (File.Exists(Path.Combine(folder, ManifestName)))
                    throw new IOException(Path.Combine(folder, ManifestName) + " is a plain manifest.json in a read-back round folder; this tool writes " + RoundManifestName(job.Race) + " and never touches that file");
                // A round whose manifest this job wrote already (a run stopped between two rounds' manifests) is done.
                if (File.Exists(Path.Combine(folder, RoundManifestName(job.Race)))) continue;
                var candidates = new List<object>();
                foreach (var c in plan.Where(p => p.Kind == PlannedSample.Candidate && p.Round == round))
                {
                    string personA = FaceFitPlan.SafeId("person-" + c.AppearanceId + "-a"), personB = FaceFitPlan.SafeId("person-" + c.AppearanceId + "-b");
                    var record = ReadRecord(work, c.Id);
                    var arrays = ReadNpz(Path.Combine(work, c.Id + ".npz"));
                    var positions = arrays["positions"].AsFloat32();
                    var person = Floats(work, personA, "positions");
                    var repeat = Floats(work, personB, "positions");
                    bool changed = (string)record["topologyKey"] != topologyKey || positions.Length != person.Length;
                    int v = person.Length / 3;
                    var displacement = new float[v];
                    if (!changed) for (int x = 0; x < v; x++) displacement[x] = (float)FaceFitStats.DistanceMm(positions, person, x);
                    string file = FaceFitPlan.SafeId(c.AppearanceId) + ".npz";
                    var members = new List<KeyValuePair<string, NpyArray>>
                    {
                        M("dna_names", topology["dna_names"]),
                        M("requested", arrays["requested"]), M("readback", arrays["readback"]),
                        M("positions", arrays["positions"]), M("normals", arrays["normals"]),
                        M("bone_local", arrays["bone_local"]), M("bone_position", arrays["bone_position"]),
                        M("person_positions", NpyArray.Float32(person, v, 3)),
                        M("displacement_mm", NpyArray.Float32(displacement)),
                    };
                    foreach (var name in TopologyMembers) members.Add(M(name, topology[name]));
                    string path = Path.Combine(folder, file);
                    string sha = Publish(work, path, "r" + round.ToString(CultureInfo.InvariantCulture) + "-" + file, staged => WriteNpz(staged, members));
                    var entry = new JsonObject().Set("appearanceId", c.AppearanceId).Set("file", file).Set("sha256", sha)
                        .Set("values", new SortedDictionary<string, double>(c.Overrides, StringComparer.Ordinal))
                        .Set("maxReadbackError", record["maxReadbackError"]).Set("readbackWithin1e5", record["readbackWithin1e5"])
                        .Set("topologyChanged", changed)
                        .Set("noiseFloorMm", repeat.Length == person.Length ? (object)FaceFitStats.MaxMm(person, repeat, 0, v) : null)
                        .Set("personSample", personA)
                        .Set("gameVsBareMaxDifference", ReadRecord(work, personA)["gameVsBareMaxDifference"]);
                    if (!changed)
                    {
                        var largest = Largest(positions, person, topology);
                        entry.Set("maxMmBySlot", largest[0]).Set("maxMmByRegion", largest[1]);
                    }
                    candidates.Add(entry);
                }
                var manifest = new JsonObject()
                    .Set("format", "gamesim-face-readback").Set("formatVersion", 1).Set("mode", job.Mode).Set("round", round)
                    .Set("description", Description(job.Mode))
                    .Set("race", job.Race).Set("gameCommit", job.GameCommit);
                foreach (var pair in head) manifest.Set(pair.Key, pair.Value);
                manifest.Set("job", new JsonObject().Set("sha256", job.Sha).Set("copy", Path.GetFileName(work) + "/job.json").Set("text", job.Text));
                var firstPerson = plan.First(p => p.Kind == PlannedSample.Person);
                Measure(manifest, work, firstPerson.Id, topology, job.HeadSlots);
                manifest.Set("headSlots", job.HeadSlots.ToList<object>());
                manifest.Set("regionNames", topology["region_names"].AsUnicode().ToList<object>());
                Regions(manifest, work);
                manifest.Set("complete", true);
                manifest.Set("candidates", candidates);
                manifest.Set("finishedUtc", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
                WriteOnce.Text(Path.Combine(folder, RoundManifestName(job.Race)), Json.Write(manifest));
                manifests.Add(manifest);
            }
            return manifests;
        }

        /// <summary>
        /// A run its budget stopped: a record in the output folder (a new file each stop, never replacing one) of which
        /// samples are present and which are missing, saying the export is incomplete. manifest.json is written only when
        /// every sample is present.
        /// </summary>
        public static string WriteStopped(FaceFitJob job, string work, IList<PlannedSample> plan, string reason)
        {
            var status = new JsonObject()
                .Set("format", "gamesim-face-export-stopped").Set("complete", false).Set("reason", reason)
                .Set("mode", job.Mode).Set("race", job.Race).Set("gameCommit", job.GameCommit).Set("jobSha256", job.Sha)
                .Set("present", plan.Where(p => IsFinished(work, p.Id)).Select(p => (object)p.Id).ToList())
                .Set("missing", plan.Where(p => !IsFinished(work, p.Id)).Select(p => (object)p.Id).ToList())
                .Set("stoppedUtc", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
            string stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);
            string path = Path.Combine(job.OutputFolder, "run-stopped-" + stamp + ".json");
            for (int n = 1; File.Exists(path); n++) path = Path.Combine(job.OutputFolder, "run-stopped-" + stamp + "-" + n + ".json");
            WriteOnce.Text(path, Json.Write(status));
            return path;
        }

        /// <summary>
        /// Whether the job's final files exist already: "complete" when they do and name this job, an error when a finished
        /// file of another job is in the way, null when there is work left.
        /// </summary>
        public static string Finished(FaceFitJob job, out bool complete)
        {
            complete = false;
            var manifests = new List<string>();
            if (job.Mode == FaceFitJob.SweepMode) manifests.Add(Path.Combine(job.OutputFolder, ManifestName));
            else
                foreach (int round in job.Candidates.Select(c => c.Round).Distinct().OrderBy(r => r))
                {
                    string plain = Path.Combine(RoundFolder(job.OutputFolder, round), ManifestName);
                    if (File.Exists(plain))
                        return "error: " + plain + " is a plain manifest.json in a read-back round folder; it is not this tool's (each race's round manifest is "
                            + RoundManifestName(job.Race) + "), so the job stops and leaves it as it is";
                    manifests.Add(Path.Combine(RoundFolder(job.OutputFolder, round), RoundManifestName(job.Race)));
                }
            int found = 0;
            foreach (var path in manifests)
            {
                if (!File.Exists(path)) continue;
                found++;
                string sha = null;
                try { sha = (string)((JsonObject)((JsonObject)Json.Parse(File.ReadAllText(path)))["job"])["sha256"]; }
                catch (Exception e) when (e is FormatException || e is InvalidCastException || e is NullReferenceException) { }
                if (sha != job.Sha) return "error: " + path + " is a finished manifest of another job; it is never overwritten";
            }
            if (found == manifests.Count) { complete = true; return "complete: " + string.Join(", ", manifests) + " already written by this job"; }
            return null;
        }
    }
}
