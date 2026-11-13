using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UMA;
using UMA.CharacterSystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gamesim.Uma.Editor
{
    /// <summary>
    /// The face-fit export: builds UMA bodies at chosen DNA values and writes their geometry and a manifest to a folder
    /// OUTSIDE the project, so the slider effects can be fitted offline (contract rows S1 and S4). Its only input is a
    /// job file (see <see cref="FaceFitJob"/>); its files are written by the engine-free UmaFaceFitFiles/Plan/Work.
    ///
    /// <para>UMA assembles a body over several frames, so the export is a coroutine run in play mode: from the menu
    /// (the job's path in GAMESIM_FACE_EXPORT_JOB) or from a UMA PlayMode test, which reaches <see cref="Run"/> by
    /// reflection. Every sample is a BARE body of the race (no wardrobe: every body slot, nothing hidden or suppressed)
    /// at a full DNA vector, every name set explicitly; a person's vector is the game's own merge, read back from a body
    /// the game's provider built. Nothing the player sees changes.</para>
    /// </summary>
    public static class UmaFaceFitExport
    {
        public const string JobVariable = "GAMESIM_FACE_EXPORT_JOB";
        public const double BuildTimeoutSeconds = 90;
        public const string FaceArea = "Face";
        private const string StageName = "__Gamesim face-fit export";

        private static double Now => Time.realtimeSinceStartupAsDouble;

        [MenuItem("Gamesim/UMA/Face Fit Export (job from GAMESIM_FACE_EXPORT_JOB)", priority = 120)]
        private static void Menu()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Gamesim UMA",
                    "UMA assembles characters over several frames, so the face-fit export needs play mode.\n\n" +
                    "Enter play mode, then run this again.", "OK");
                return;
            }
            string job = Environment.GetEnvironmentVariable(JobVariable);
            if (string.IsNullOrEmpty(job))
            {
                Debug.LogWarning("[Gamesim.Uma] Face-fit export: set " + JobVariable + " to the job file's path (outside the project) and run this again.");
                return;
            }
            var host = new GameObject(StageName + " (menu)") { hideFlags = HideFlags.DontSave };
            var runner = host.AddComponent<UmaFaceFitRunner>();
            runner.StartCoroutine(runner.Hold(Run(job,
                line => Debug.Log("[Gamesim.Uma] face-fit: " + line),
                outcome =>
                {
                    if (outcome.StartsWith("complete", StringComparison.Ordinal)) Debug.Log("[Gamesim.Uma] face-fit " + outcome);
                    else Debug.LogError("[Gamesim.Uma] face-fit " + outcome);
                })));
        }

        /// <summary>
        /// Runs a job to its end, its time budget or its first error. <paramref name="outcome"/> is called once with
        /// "complete: ...", "resume: ..." (the budget ran out; run the same job again to continue) or "error: ...".
        /// </summary>
        public static IEnumerator Run(string jobPath, Action<string> log, Action<string> outcome)
        {
            log = log ?? (_ => { });
            bool reported = false;
            void Report(string text) { if (reported) return; reported = true; outcome?.Invoke(text); }
            var stage = new GameObject(StageName) { hideFlags = HideFlags.DontSave };
            try
            {
                yield return Guard(RunJob(jobPath, stage.transform, log, Report), e => Report("error: " + e));
            }
            finally
            {
                Object.Destroy(stage);
            }
            Report("error: the export stopped without an outcome");
        }

        /// <summary>
        /// Steps a coroutine and every coroutine it yields, catching what any of them throws (an iterator cannot catch
        /// around its own yields), so a failure reaches the outcome instead of stopping a test with half a message.
        /// </summary>
        private static IEnumerator Guard(IEnumerator root, Action<Exception> failed)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                object current;
                try
                {
                    if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                    current = stack.Peek().Current;
                }
                catch (Exception e)
                {
                    // Every coroutine still open runs its finally blocks (a body destroyed, a handler taken off).
                    foreach (var open in stack)
                        try { (open as IDisposable)?.Dispose(); }
                        catch (Exception) { /* the first failure is the one reported */ }
                    failed(e);
                    yield break;
                }
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return current;
            }
        }

        private sealed class Box<T> { public T Value; public string Error; }

        private static IEnumerator RunJob(string jobPath, Transform stage, Action<string> log, Action<string> report)
        {
            if (string.IsNullOrEmpty(jobPath) || !File.Exists(jobPath)) { report("error: no job file at '" + jobPath + "'"); yield break; }
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            var errors = new List<string>();
            var job = FaceFitJob.Parse(File.ReadAllText(jobPath), projectRoot, errors);
            if (job == null) { report("error: the job " + jobPath + " is wrong:\n - " + string.Join("\n - ", errors)); yield break; }
            string finished = FaceFitWork.Finished(job, out bool complete);
            if (finished != null) { report(finished); yield break; }
            string work = FaceFitWork.Open(job, out int parts);
            log("job " + job.Sha.Substring(0, 16) + " (" + job.Mode + ", " + job.Race + ") into " + job.OutputFolder + (parts > 0 ? "; cleared " + parts + " half-written file(s)" : ""));

            var indexer = UMAAssetIndexer.Instance;
            if (indexer == null) { report("error: no UMA asset index (UMAAssetIndexer.Instance is null)"); yield break; }
            if (indexer.Generator == null) { report("error: UMA provided no generator"); yield break; }
            if (indexer.GetRace(job.Race) == null) { report("error: race '" + job.Race + "' is not in the UMA Global Library"); yield break; }

            var bare = new Box<DynamicCharacterAvatar>();
            yield return BuildBare(job.Race, stage, (avatar, error) => { bare.Value = avatar; bare.Error = error; });
            if (bare.Value == null) { report("error: " + bare.Error); yield break; }
            var avatarRoot = bare.Value;

            // The race's DNA as UMA holds it: every name, the default UMA holds for each, and the face area's names.
            var groups = avatarRoot.umaData.umaRecipe.dnaInstanceCollection.GetDNAByGroup();
            var defaults = new SortedDictionary<string, double>(StringComparer.Ordinal);
            var defaultSource = new Dictionary<string, string>(StringComparer.Ordinal);
            var faceNames = new List<string>();
            foreach (var group in groups)
                foreach (var instance in group.Value)
                {
                    if (instance == null || defaults.ContainsKey(instance.Name)) continue;
                    float value = group.Key != null ? group.Key.GetDefaultValue(instance.Name) : -99f;
                    // UMA answers -99 for a name its group holds no DNA asset for: then the value the race built is the default.
                    bool asset = value > -98f;
                    defaults[instance.Name] = asset ? value : instance.Value;
                    defaultSource[instance.Name] = asset ? "uma-default" : "uma-built";
                    if (group.Key != null && group.Key.DNAArea == FaceArea) faceNames.Add(instance.Name);
                }
            var built = avatarRoot.GetDNA();
            foreach (var pair in built)
                if (!defaults.ContainsKey(pair.Key)) { defaults[pair.Key] = pair.Value.Get(); defaultSource[pair.Key] = "uma-built"; }
            var unknown = job.UnknownNames(defaults.Keys);
            if (unknown.Count > 0) { report("error: the job names DNA the race does not have:\n - " + string.Join("\n - ", unknown)); yield break; }

            // Who is built through the game's merge, and each person checked against the race.
            List<string> persons;
            if (job.Mode == FaceFitJob.SweepMode)
                persons = job.Persons ?? (job.BaselineSource == "house"
                    ? CastTemplates.In(CastTemplates.Roster.AllStars).Select(t => t.Id)
                        .Where(id => UmaCastLibrary.TryGet(id, out var look) && look.Race == job.Race).ToList()
                    : new List<string>());
            else persons = job.Candidates.Select(c => c.AppearanceId).Distinct().ToList();
            foreach (string id in persons.Concat(job.BaselineSource == "look" && job.Mode == FaceFitJob.SweepMode ? new[] { job.BaselineAppearanceId } : new string[0]))
            {
                if (!UmaCastLibrary.TryGet(id, out var look)) { report("error: '" + id + "' has no look in UmaCastLibrary"); yield break; }
                if (look.Race != job.Race) { report("error: '" + id + "' is a " + look.Race + ", not a " + job.Race); yield break; }
            }

            // The baseline: what it requests (every name for a race sweep; the look's own merge around one look), the value of
            // every name at the baseline (which positions are skipped), and where each came from. Fixed in the work folder on the
            // first run, so a resumed run sweeps around the same one.
            Dictionary<string, double> baseline = null, baselineValues = null;
            bool lookSweep = job.Mode == FaceFitJob.SweepMode && job.BaselineSource == "look";
            string baselinePath = Path.Combine(work, "baseline.json");
            if (job.Mode == FaceFitJob.SweepMode)
            {
                if (File.Exists(baselinePath))
                {
                    var stored = (JsonObject)Json.Parse(File.ReadAllText(baselinePath));
                    baseline = FaceFitWork.RequestedOf(stored);
                    baselineValues = ((JsonObject)stored["values"]).ToDictionary(p => p.Key, p => (double)p.Value, StringComparer.Ordinal);
                }
                else
                {
                    var sources = new JsonObject();
                    var record = new JsonObject().Set("source", job.BaselineSource);
                    if (!lookSweep)
                    {
                        baseline = new Dictionary<string, double>(defaults, StringComparer.Ordinal);
                        foreach (var name in defaults.Keys) sources.Set(name, defaultSource[name]);
                        if (job.BaselineSource == "house")
                            foreach (var pair in UmaCastLibrary.HouseProportions)
                            {
                                if (!baseline.ContainsKey(pair.Key)) continue;
                                baseline[pair.Key] = pair.Value;
                                sources.Set(pair.Key, "house");
                            }
                        else
                            foreach (var pair in job.BaselineValues) { baseline[pair.Key] = pair.Value; sources.Set(pair.Key, "job"); }
                        baselineValues = baseline;
                    }
                    else
                    {
                        var merged = new Box<Dictionary<string, double>>();
                        yield return GameMerge(job.BaselineAppearanceId, stage, (values, error) => { merged.Value = values; merged.Error = error; });
                        if (merged.Value == null) { report("error: " + merged.Error); yield break; }
                        baseline = GameRequest(job.BaselineAppearanceId, merged.Value);
                        foreach (var name in baseline.Keys) sources.Set(name, "game-merge");
                        baselineValues = new Dictionary<string, double>(merged.Value, StringComparer.Ordinal);
                        record.Set("appearanceId", job.BaselineAppearanceId).Set("gameReadback", new SortedDictionary<string, double>(merged.Value, StringComparer.Ordinal));
                    }
                    var requested = new JsonObject();
                    foreach (var name in baseline.Keys.OrderBy(n => n, StringComparer.Ordinal)) requested.Set(name, baseline[name]);
                    WriteOnce.Text(baselinePath, Json.Write(record.Set("requested", requested)
                        .Set("values", new SortedDictionary<string, double>(baselineValues, StringComparer.Ordinal)).Set("sources", sources)));
                }
            }

            var sweepNames = job.SweepNames ?? faceNames;
            var plan = job.Mode == FaceFitJob.SweepMode
                ? FaceFitPlan.Sweep(job, sweepNames, baselineValues, persons, unrequested: true, fresh: lookSweep)
                : FaceFitPlan.Readback(job);
            int done = plan.Count(s => FaceFitWork.IsFinished(work, s.Id));
            log(plan.Count + " samples planned, " + done + " finished by an earlier run; " + faceNames.Count + " face names, " + defaults.Count + " DNA names");

            double deadline = Now + job.BudgetMinutes * 60.0;
            string topologyKey = null;
            string topologyJson = Path.Combine(work, FaceFitWork.TopologyName + ".json");
            if (File.Exists(topologyJson)) topologyKey = (string)((JsonObject)Json.Parse(File.ReadAllText(topologyJson)))["topologyKey"];
            foreach (var sample in plan)
            {
                if (FaceFitWork.IsFinished(work, sample.Id)) continue;
                if (Now > deadline)
                {
                    var left = plan.Where(s => !FaceFitWork.IsFinished(work, s.Id)).Select(s => s.Id).ToList();
                    string reason = "the " + job.BudgetMinutes.ToString("0.##", CultureInfo.InvariantCulture) + "-minute budget ran out";
                    string stopped = FaceFitWork.WriteStopped(job, work, plan, reason);
                    report("resume: " + reason + " with " + left.Count + " of " + plan.Count + " samples left (" + string.Join(", ", left.Take(8))
                        + (left.Count > 8 ? ", ..." : "") + "; listed in " + stopped + "); the export is incomplete: run the same job again to continue in " + job.OutputFolder);
                    yield break;
                }

                // What the sample requests.
                Dictionary<string, double> vector;
                var extra = new JsonObject();
                if (sample.Kind == PlannedSample.Person && sample.Repeat == "a")
                {
                    var merged = new Box<Dictionary<string, double>>();
                    yield return GameMerge(sample.AppearanceId, stage, (values, error) => { merged.Value = values; merged.Error = error; });
                    if (merged.Value == null) { report("error: " + merged.Error); yield break; }
                    vector = GameRequest(sample.AppearanceId, merged.Value);
                    extra.Set("gameReadback", new SortedDictionary<string, double>(merged.Value, StringComparer.Ordinal));
                    extra.Set("method", "the look built by UmaBodyProvider.TryCreate and read back; a fresh bare body then requests only the names the game's merge sets (the house proportions' and the look's Dna's), at the game's read-back values");
                }
                else if (sample.Kind == PlannedSample.Person || sample.Kind == PlannedSample.Candidate)
                {
                    string first = FaceFitPlan.SafeId("person-" + sample.AppearanceId + "-a");
                    if (!FaceFitWork.IsFinished(work, first)) { report("error: " + sample.Id + " needs " + first + ", which is not finished"); yield break; }
                    vector = FaceFitWork.RequestedOf(FaceFitWork.ReadRecord(work, first));
                    foreach (var pair in sample.Overrides) vector[pair.Key] = pair.Value;
                    extra.Set("personSample", first);
                }
                else if (sample.Kind == PlannedSample.Unrequested)
                {
                    vector = new Dictionary<string, double>(StringComparer.Ordinal);
                    foreach (var pair in UmaCastLibrary.HouseProportions) if (defaults.ContainsKey(pair.Key)) vector[pair.Key] = pair.Value;
                }
                else
                {
                    vector = new Dictionary<string, double>(baseline, StringComparer.Ordinal);
                    foreach (var pair in sample.Overrides) vector[pair.Key] = pair.Value;
                }

                // A fresh sample gets a new bare body (every unrequested name where the race builds it); the rest share one.
                var target = avatarRoot;
                if (sample.Fresh)
                {
                    var fresh = new Box<DynamicCharacterAvatar>();
                    yield return BuildBare(job.Race, stage, (avatar, error) => { fresh.Value = avatar; fresh.Error = error; });
                    if (fresh.Value == null) { report("error: sample " + sample.Id + ": " + fresh.Error); yield break; }
                    target = fresh.Value;
                }
                var captured = new Box<FaceFitCapture>();
                try
                {
                    yield return CaptureFull(target, vector, BuildTimeoutSeconds, (capture, error) => { captured.Value = capture; captured.Error = error; });
                }
                finally
                {
                    if (target != avatarRoot && target != null) Object.Destroy(target.gameObject);
                }
                if (captured.Value == null) { report("error: sample " + sample.Id + ": " + captured.Error); yield break; }
                var c = captured.Value;

                // The race baseline must read back as requested (the lead's check on a1): never written, it stops the run.
                if (sample.Kind == PlannedSample.Baseline && c.MaxReadbackError() > FaceFitWork.ReadbackTolerance)
                {
                    var off = Enumerable.Range(0, c.DnaNames.Length).Where(d => !double.IsNaN(c.Requested[d]) && Math.Abs(c.Requested[d] - c.Readback[d]) > FaceFitWork.ReadbackTolerance)
                        .Select(d => c.DnaNames[d] + " requested " + c.Requested[d].ToString("R", CultureInfo.InvariantCulture) + " read back " + c.Readback[d].ToString("R", CultureInfo.InvariantCulture));
                    report("error: " + sample.Id + " does not read back as requested (tolerance 1e-5): " + string.Join("; ", off));
                    yield break;
                }
                if (extra.TryGet("gameReadback", out var gameReadback))
                {
                    var game = (SortedDictionary<string, double>)gameReadback;
                    double worst = 0;
                    var apart = new List<string>();
                    for (int d = 0; d < c.DnaNames.Length; d++)
                        if (game.TryGetValue(c.DnaNames[d], out double g))
                        {
                            double gap = Math.Abs(g - c.Readback[d]);
                            worst = Math.Max(worst, gap);
                            // A name the bare body requested at the game's value and read back elsewhere: not the game's merge.
                            if (!double.IsNaN(c.Requested[d]) && gap > FaceFitWork.ReadbackTolerance)
                                apart.Add(c.DnaNames[d] + " game " + g.ToString("R", CultureInfo.InvariantCulture) + " bare " + c.Readback[d].ToString("R", CultureInfo.InvariantCulture));
                        }
                    if (apart.Count > 0)
                    {
                        report("error: " + sample.Id + ": the bare body does not reproduce the game's read-back (tolerance 1e-5): " + string.Join("; ", apart));
                        yield break;
                    }
                    extra.Set("gameVsBareMaxDifference", worst);
                    extra.Set("gameVsBareAgree", worst <= FaceFitWork.ReadbackTolerance);
                }
                foreach (var pair in extra) c.Extra.Set(pair.Key, pair.Value);
                if (topologyKey == null)
                {
                    FaceFitWork.WriteTopology(work, c, job.HeadSlots);
                    topologyKey = c.TopologyKey();
                }
                FaceFitWork.WriteSample(work, sample, c, topologyKey);
                done++;
                if (done % 10 == 0 || done == plan.Count) log(done + " of " + plan.Count + " samples finished");
            }

            var head = Head(job, projectRoot, avatarRoot, defaults, defaultSource, faceNames, baselineValues, persons);
            if (job.Mode == FaceFitJob.SweepMode)
            {
                var manifest = FaceFitWork.AssembleSweep(job, work, plan, sweepNames, head);
                report("complete: " + plan.Count + " samples in " + Path.Combine(job.OutputFolder, FaceFitWork.SamplesName)
                    + "; noise floor " + manifest["noiseFloorMm"] + " mm; topology changed: " + manifest["topologyChanged"]);
            }
            else
            {
                var manifests = FaceFitWork.AssembleReadback(job, work, plan, head);
                report("complete: " + job.Candidates.Count + " candidate(s) in " + manifests.Count + " round folder(s) under " + job.OutputFolder);
            }
        }

        /// <summary>
        /// What a person's sample requests: the names the game's merge sets (the keys of the house proportions and of the
        /// look's Dna, the two inputs UmaBodyProvider merges) at the values read back from the game's own build of the look.
        /// </summary>
        private static Dictionary<string, double> GameRequest(string appearanceId, IDictionary<string, double> gameReadback)
        {
            UmaCastLibrary.TryGet(appearanceId, out var look);
            var request = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (string name in UmaCastLibrary.HouseProportions.Keys.Concat(look?.Dna?.Keys ?? Enumerable.Empty<string>()))
                if (gameReadback.TryGetValue(name, out double value)) request[name] = value;
            return request;
        }

        /// <summary>What the manifest says about the run itself: hashes of the tool, the game's merge and UMA's data; the DNA.</summary>
        private static JsonObject Head(FaceFitJob job, string projectRoot, DynamicCharacterAvatar avatar, IDictionary<string, double> defaults,
            IDictionary<string, string> defaultSource, List<string> faceNames, IDictionary<string, double> baselineValues, IList<string> persons)
        {
            string Hash(string relative)
            {
                string full = Path.Combine(projectRoot, relative);
                return File.Exists(full) ? Sha256.File(full) : null;
            }
            var tool = new JsonObject();
            foreach (var file in Directory.GetFiles(Path.Combine(projectRoot, "Assets/Gamesim/Uma/Editor"), "UmaFaceFit*.cs").OrderBy(f => f, StringComparer.Ordinal))
                tool.Set("Assets/Gamesim/Uma/Editor/" + Path.GetFileName(file), Sha256.File(file));
            var game = new JsonObject();
            foreach (string file in new[] { "Assets/Gamesim/Uma/UmaBodyProvider.cs", "Assets/Gamesim/Uma/UmaCastLibrary.cs", "Assets/Gamesim/Uma/UmaBodyTint.cs" })
                game.Set(file, Hash(file));
            var assets = new JsonObject();
            void Asset(Object asset)
            {
                if (asset == null) return;
                string path = AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrEmpty(path) && assets[path] == null) assets.Set(path, Hash(path));
            }
            Asset(UMAAssetIndexer.Instance.GetRace(job.Race));
            foreach (var group in avatar.umaData.umaRecipe.dnaInstanceCollection.GetDNAByGroup().Keys)
            {
                Asset(group);
                if (group != null) foreach (var dna in group.dnaList) Asset(dna);
            }
            foreach (string guid in AssetDatabase.FindAssets("UMA_GLIB t:Prefab"))
                Asset(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
            foreach (var slot in avatar.umaData.umaRecipe.slotDataList)
                if (slot?.asset != null && job.HeadSlots.Any(h => FaceFitStats.SlotMatches(slot.asset.slotName, h))) Asset(slot.asset);
            var head = new JsonObject()
                .Set("tool", tool).Set("gameCode", game).Set("umaAssets", assets)
                .Set("unityVersion", Application.unityVersion)
                .Set("umaPackageSha256", Hash("Assets/UMA/package.json"));
            if (job.Description != null) head.Set("jobDescription", job.Description);
            head.Set("dnaDefaults", new SortedDictionary<string, double>(defaults, StringComparer.Ordinal));
            var sources = new JsonObject();
            foreach (var name in defaultSource.Keys.OrderBy(n => n, StringComparer.Ordinal)) sources.Set(name, defaultSource[name]);
            head.Set("dnaDefaultSources", sources);
            head.Set("faceNames", faceNames.ToList<object>());
            if (job.Mode == FaceFitJob.SweepMode)
            {
                head.Set("defaultPositions", "per swept name: 0.25, 0.375, 0.625, 0.75, plus 0.5 when its baseline value is not 0.5; a position equal to the baseline value is skipped (plan.positions says, per name, which were the job's)");
                head.Set("random", new JsonObject().Set("count", job.RandomCount).Set("seed", job.RandomSeed)
                    .Set("range", new List<object> { job.RandomMin, job.RandomMax }).Set("generator", "SplitMix64, values rounded to 4 decimals, names in sweepNames order"));
                head.Set("baseline", new JsonObject().Set("source", job.BaselineSource).Set("appearanceId", job.BaselineAppearanceId)
                    .Set("values", new SortedDictionary<string, double>(baselineValues, StringComparer.Ordinal)));
                head.Set("personsBuilt", persons.ToList<object>());
            }
            return head;
        }

        /// <summary>
        /// A bare body of a race as the game builds one (UmaBodyProvider's avatar set-up, no wardrobe, no default recipes,
        /// no animator controller, no expression player), at the origin of <paramref name="parent"/>, waited for on the wall clock.
        /// </summary>
        public static IEnumerator BuildBare(string race, Transform parent, Action<DynamicCharacterAvatar, string> done)
        {
            var indexer = UMAAssetIndexer.Instance;
            if (indexer == null || indexer.Generator == null) { done(null, "no UMA asset index or generator"); yield break; }
            if (indexer.GetRace(race) == null) { done(null, "race '" + race + "' is not in the UMA Global Library"); yield break; }
            var root = new GameObject("UMA bare body (" + race + ")") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            root.AddComponent<Animator>().applyRootMotion = false;
            var avatar = root.AddComponent<DynamicCharacterAvatar>();
            avatar.activeRace.name = race;
            avatar.editorTimeGeneration = false;
            avatar.BuildCharacterEnabled = true;
            avatar.raceAnimationControllers.defaultAnimationController = null;
            avatar.preloadWardrobeRecipes.loadDefaultRecipes = false;
            avatar.preloadWardrobeRecipes.recipes.Clear();
            avatar.OnCharacterBegun += KeepReadable;
            int updates = 0;
            void Count(UMAData data) => updates++;
            avatar.OnCharacterUpdated += Count;
            double deadline = Now + BuildTimeoutSeconds;
            while (updates == 0 && Now < deadline && avatar != null) yield return null;
            if (avatar != null) avatar.OnCharacterUpdated -= Count;
            if (updates == 0)
            {
                Object.Destroy(root);
                done(null, "a bare " + race + " body raised no CharacterUpdated within " + BuildTimeoutSeconds + " s of real time");
                yield break;
            }
            done(avatar, null);
        }

        /// <summary>The built mesh stays readable on the CPU (UMA drops the copy otherwise), so UVs, weights and triangles can be read.</summary>
        private static void KeepReadable(UMAData data)
        {
            if (data != null) data.markNotReadable = false;
        }

        /// <summary>
        /// The game's own merge for a look: the provider builds it (house proportions, then the look's Dna, applied as the
        /// game applies them), and every DNA value is read back once its build state is Ready. The body is then destroyed.
        /// </summary>
        private static IEnumerator GameMerge(string appearanceId, Transform parent, Action<Dictionary<string, double>, string> done)
        {
            var holder = new GameObject("game build (" + appearanceId + ")") { hideFlags = HideFlags.DontSave };
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = new Vector3(3f, 0f, 0f);
            try
            {
                if (!new UmaBodyProvider().TryCreate(appearanceId, holder.transform, Color.white, out var body))
                {
                    done(null, "UmaBodyProvider.TryCreate built no body for '" + appearanceId + "'");
                    yield break;
                }
                var state = body.Root.GetComponent<CharacterBodyBuildState>();
                double deadline = Now + BuildTimeoutSeconds;
                while (state != null && !state.Ready && Now < deadline) yield return null;
                if (state == null || !state.Ready)
                {
                    done(null, "the game's build of '" + appearanceId + "' was not Ready (CharacterBodyBuildState.Ready) within " + BuildTimeoutSeconds + " s of real time");
                    yield break;
                }
                var avatar = body.Root.GetComponent<DynamicCharacterAvatar>();
                var values = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (var pair in avatar.GetDNA()) values[pair.Key] = pair.Value.Get();
                done(values, null);
            }
            finally
            {
                Object.Destroy(holder);
            }
        }

        /// <summary>
        /// Sets a DNA vector on a built body and captures it in the export frame; for a test, the positions only (x, y, z
        /// per vertex in metres, every slot in the recipe's order). See <see cref="CaptureFull"/>.
        /// </summary>
        public static IEnumerator Capture(DynamicCharacterAvatar avatar, IDictionary<string, float> dna, double timeoutSeconds, Action<float[], string> done)
        {
            var values = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var pair in dna) values[pair.Key] = pair.Value;
            yield return CaptureFull(avatar, values, timeoutSeconds, (capture, error) => done(capture?.Positions, error));
        }

        /// <summary>
        /// Sets every value given, as the game does (DnaSetter.Set, then Dirty(dna, mesh)), and captures the body INSIDE the
        /// rebuild's CharacterUpdated: UMA has just reset the skeleton to its T-pose and applied the DNA, and every Animator
        /// on the body is off, so no pose reaches the vertices. Vertices are baked from the skinned renderers and taken into
        /// the root's frame, so where the root stands, how it is turned and scaled, never reaches them either.
        /// </summary>
        internal static IEnumerator CaptureFull(DynamicCharacterAvatar avatar, IDictionary<string, double> values, double timeoutSeconds, Action<FaceFitCapture, string> done)
        {
            if (avatar == null || avatar.umaData == null) { done(null, "no built body to capture"); yield break; }
            foreach (var animator in avatar.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            var setters = avatar.GetDNA();
            foreach (var pair in values)
            {
                if (!setters.TryGetValue(pair.Key, out var setter)) { done(null, "the body has no DNA named '" + pair.Key + "'"); yield break; }
                setter.Set((float)pair.Value);
            }
            FaceFitCapture result = null;
            string failure = null;
            double start = Now;
            void Updated(UMAData data)
            {
                if (result != null || failure != null) return;
                try
                {
                    foreach (var animator in avatar.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                    result = Read(avatar, values);
                }
                catch (Exception e) { failure = e.ToString(); }
            }
            avatar.OnCharacterUpdated += Updated;
            try
            {
                avatar.umaData.Dirty(true, false, true);
                double deadline = Now + timeoutSeconds;
                while (result == null && failure == null && Now < deadline) yield return null;
            }
            finally
            {
                if (avatar != null) avatar.OnCharacterUpdated -= Updated;
            }
            if (failure != null) { done(null, failure); yield break; }
            if (result == null) { done(null, "no CharacterUpdated within " + timeoutSeconds + " s of real time after the DNA rebuild"); yield break; }
            result.Seconds = Now - start;
            done(result, null);
        }

        /// <summary>
        /// Reads the built body with its root put, for the read only, at the world origin, unturned, at world scale 1 (its
        /// place, turn and scale are restored after). The export frame is then exact by construction: every renderer is at
        /// world scale 1, so a baked vertex means the same whatever BakeMesh's scale convention, and the root's frame is
        /// the world's.
        /// </summary>
        private static FaceFitCapture Read(DynamicCharacterAvatar avatar, IDictionary<string, double> requested)
        {
            var root = avatar.transform;
            Vector3 position = root.localPosition, scale = root.localScale;
            Quaternion rotation = root.localRotation;
            try
            {
                root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var lossy = root.lossyScale;
                root.localScale = new Vector3(scale.x / lossy.x, scale.y / lossy.y, scale.z / lossy.z);
                foreach (var renderer in avatar.umaData.GetRenderers() ?? new SkinnedMeshRenderer[0])
                    if (renderer != null && (renderer.transform.lossyScale - Vector3.one).sqrMagnitude > 1e-10f)
                        throw new InvalidOperationException("the renderer " + renderer.name + " stands at world scale " + renderer.transform.lossyScale.ToString("F6")
                            + " with the root at scale 1, so the export frame cannot be made exact");
                return ReadAtOrigin(avatar, requested);
            }
            finally
            {
                if (root != null)
                {
                    root.localPosition = position;
                    root.localRotation = rotation;
                    root.localScale = scale;
                }
            }
        }

        /// <summary>Reads the built body: every slot in the recipe's order, its vertices from the renderer UMA put it in.</summary>
        private static FaceFitCapture ReadAtOrigin(DynamicCharacterAvatar avatar, IDictionary<string, double> requested)
        {
            var data = avatar.umaData;
            var root = avatar.transform;
            var renderers = data.GetRenderers();
            if (renderers == null || renderers.Length == 0) throw new InvalidOperationException("the body has no skinned renderer");

            var baked = new Vector3[renderers.Length][];
            var bakedNormals = new Vector3[renderers.Length][];
            var toRoot = new Matrix4x4[renderers.Length];
            var meshes = new Mesh[renderers.Length];
            var bones = new List<Transform>();
            var bindPoses = new List<Matrix4x4>();
            var boneMap = new int[renderers.Length][];
            for (int r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                var mesh = new Mesh();
                try
                {
                    renderer.BakeMesh(mesh, true);
                    baked[r] = mesh.vertices;
                    bakedNormals[r] = mesh.normals;
                }
                finally { Object.Destroy(mesh); }
                toRoot[r] = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                meshes[r] = renderer.sharedMesh;
                var rendererBones = renderer.bones;
                var poses = meshes[r].bindposes;
                boneMap[r] = new int[rendererBones.Length];
                for (int b = 0; b < rendererBones.Length; b++)
                {
                    int at = bones.IndexOf(rendererBones[b]);
                    if (at < 0) { at = bones.Count; bones.Add(rendererBones[b]); bindPoses.Add(b < poses.Length ? poses[b] : Matrix4x4.identity); }
                    boneMap[r][b] = at;
                }
            }

            var slots = data.umaRecipe.slotDataList.Where(slot => slot != null && !slot.Suppressed && slot.asset != null
                && slot.asset.meshData != null && slot.asset.meshData.vertexCount > 0).ToList();
            int total = slots.Sum(slot => slot.asset.meshData.vertexCount);
            var c = new FaceFitCapture
            {
                SlotNames = new string[slots.Count], SlotOffset = new int[slots.Count], SlotCount = new int[slots.Count],
                Positions = new float[total * 3], Normals = new float[total * 3], Uv0 = new float[total * 2],
                BoneIndices = new int[total * 4], BoneWeights = new float[total * 4],
            };
            var triangles = new List<int>();
            int row = 0;
            var uvCache = new Vector2[renderers.Length][];
            var weightCache = new BoneWeight[renderers.Length][];
            var triangleCache = new int[renderers.Length][][];
            for (int k = 0; k < slots.Count; k++)
            {
                var slot = slots[k];
                int r = slot.skinnedMeshRenderer, offset = slot.vertexOffset, count = slot.asset.meshData.vertexCount;
                if (r < 0 || r >= renderers.Length) throw new InvalidOperationException("slot " + slot.slotName + " names renderer " + r + " of " + renderers.Length);
                if (offset < 0 || offset + count > baked[r].Length)
                    throw new InvalidOperationException("slot " + slot.slotName + " spans vertices " + offset + ".." + (offset + count) + " of a mesh of " + baked[r].Length);
                if (uvCache[r] == null)
                {
                    uvCache[r] = meshes[r].uv;
                    weightCache[r] = meshes[r].boneWeights;
                    var source = meshes[r];
                    triangleCache[r] = Enumerable.Range(0, source.subMeshCount).Select(sub => source.GetTriangles(sub)).ToArray();
                    if (uvCache[r].Length != baked[r].Length || weightCache[r].Length != baked[r].Length)
                        throw new InvalidOperationException("the built mesh is not readable on the CPU (markNotReadable): UVs or weights are missing");
                }
                c.SlotNames[k] = slot.asset.slotName;
                c.SlotOffset[k] = row;
                c.SlotCount[k] = count;
                for (int i = 0; i < count; i++)
                {
                    int v = offset + i, x = row + i;
                    var p = toRoot[r].MultiplyPoint3x4(baked[r][v]);
                    var n = toRoot[r].MultiplyVector(bakedNormals[r].Length > v ? bakedNormals[r][v] : Vector3.zero).normalized;
                    c.Positions[x * 3] = p.x; c.Positions[x * 3 + 1] = p.y; c.Positions[x * 3 + 2] = p.z;
                    c.Normals[x * 3] = n.x; c.Normals[x * 3 + 1] = n.y; c.Normals[x * 3 + 2] = n.z;
                    c.Uv0[x * 2] = uvCache[r][v].x; c.Uv0[x * 2 + 1] = uvCache[r][v].y;
                    var w = weightCache[r][v];
                    int[] index = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                    float[] weight = { w.weight0, w.weight1, w.weight2, w.weight3 };
                    for (int j = 0; j < 4; j++)
                    {
                        c.BoneIndices[x * 4 + j] = index[j] >= 0 && index[j] < boneMap[r].Length ? boneMap[r][index[j]] : -1;
                        c.BoneWeights[x * 4 + j] = weight[j];
                    }
                }
                foreach (var sub in triangleCache[r])
                    for (int t = 0; t + 2 < sub.Length; t += 3)
                    {
                        int a = sub[t];
                        if (a < offset || a >= offset + count) continue;
                        triangles.Add(row + a - offset); triangles.Add(row + sub[t + 1] - offset); triangles.Add(row + sub[t + 2] - offset);
                    }
                row += count;
            }
            c.Triangles = triangles.ToArray();

            int boneCount = bones.Count;
            c.BoneNames = bones.Select(b => b != null ? b.name : "").ToArray();
            c.BoneParents = bones.Select(b => b != null && b.parent != null ? bones.IndexOf(b.parent) : -1).ToArray();

            // The built skeleton itself, not only the skinned bones: which bones sit under Head (the region rule's step 4),
            // and the two EarAdjust bones the manifest must carry whether or not they are skinned.
            var skeleton = root.GetComponentsInChildren<Transform>(true);
            var headBone = skeleton.FirstOrDefault(t => t.name == "Head");
            c.BoneUnderHead = bones.Select(b => b != null && headBone != null && b != headBone && b.IsChildOf(headBone)).ToArray();
            c.EarAdjust = new Dictionary<string, float[]>(StringComparer.Ordinal);
            foreach (string ear in new[] { "LeftEarAdjust", "RightEarAdjust" })
            {
                var bone = skeleton.FirstOrDefault(t => t.name == ear);
                if (bone == null) throw new InvalidOperationException("the built skeleton has no " + ear + " bone: contract S1 needs both EarAdjust positions");
                var at = root.InverseTransformPoint(bone.position);
                c.EarAdjust[ear] = new[] { at.x, at.y, at.z };
            }
            c.BindPoses = new float[boneCount * 16];
            c.BoneLocal = new float[boneCount * 10];
            c.BonePosition = new float[boneCount * 3];
            for (int b = 0; b < boneCount; b++)
            {
                for (int i = 0; i < 16; i++) c.BindPoses[b * 16 + i] = bindPoses[b][i / 4, i % 4];
                var bone = bones[b];
                if (bone == null) continue;
                var lp = bone.localPosition; var lr = bone.localRotation; var ls = bone.localScale;
                var at = new[] { lp.x, lp.y, lp.z, lr.x, lr.y, lr.z, lr.w, ls.x, ls.y, ls.z };
                Array.Copy(at, 0, c.BoneLocal, b * 10, 10);
                var wp = root.InverseTransformPoint(bone.position);
                c.BonePosition[b * 3] = wp.x; c.BonePosition[b * 3 + 1] = wp.y; c.BonePosition[b * 3 + 2] = wp.z;
            }

            var dna = avatar.GetDNA();
            c.DnaNames = dna.Keys.OrderBy(n => n, StringComparer.Ordinal).ToArray();
            c.Requested = c.DnaNames.Select(n => requested.TryGetValue(n, out double v) ? v : double.NaN).ToArray();
            c.Readback = c.DnaNames.Select(n => (double)dna[n].Get()).ToArray();
            return c;
        }
    }
}
