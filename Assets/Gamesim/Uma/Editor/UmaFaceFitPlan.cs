using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Gamesim.Uma.Editor
{
    // The face-fit export's job, plan, statistics and final assembly, with nothing of Unity's in them (the dotnet
    // subset compiles this file). The Unity side (UmaFaceFitExport) builds the bodies and captures them into one
    // file per sample; everything here is arithmetic and files. No fit set, bound or clamp is written here: which
    // names are swept, at which values, is the job's (or, when it says nothing, every name of the race's face area).

    /// <summary>The job file, read and checked: every problem is collected and named, none is guessed around.</summary>
    internal sealed class FaceFitJob
    {
        public const string SweepMode = "sweep", ReadbackMode = "readback";
        public const long DefaultSeed = 20261111;
        public static readonly string[] DefaultHeadSlots =
        {
            "UMA30_Body_UDIM1001_slot", "UMA30_Body_UDIM1003_slot", "UMA30_Eyes_slot", "UMA30_Eyelashes_slot", "UMA30_InnerMouth_slot",
        };

        public string Mode, Race, GameCommit, OutputFolder;
        public string BaselineSource = "house", BaselineAppearanceId;
        public Dictionary<string, double> BaselineValues = new Dictionary<string, double>(StringComparer.Ordinal);
        public List<string> SweepNames;
        public List<double> Positions;
        public Dictionary<string, List<double>> PositionsByName;
        public int RandomCount = 6;
        public long RandomSeed = DefaultSeed;
        public double RandomMin = .3, RandomMax = .7;
        public bool RepeatBaseline = true;
        public List<Extra> Extras = new List<Extra>();
        public List<string> Persons;
        public List<Candidate> Candidates = new List<Candidate>();
        public double BudgetMinutes = 30, MovedThresholdMm = .01;
        public List<string> HeadSlots = new List<string>(DefaultHeadSlots);
        public string Text, Sha, Description;

        public sealed class Extra { public string Name; public double Value; public Dictionary<string, double> Values; }
        public sealed class Candidate { public string AppearanceId; public int Round; public Dictionary<string, double> Values; }

        private static readonly string[] Known =
        {
            "mode", "race", "gameCommit", "outputFolder", "baseline", "plan", "extraSamples", "persons", "candidates",
            "budgetMinutes", "movedThresholdMm", "headSlots", "description",
        };
        private static readonly string[] KnownPlan = { "sweepNames", "positions", "positionsByName", "randomCount", "randomSeed", "randomRange", "repeatBaseline" };

        /// <summary>Reads a job; returns null and fills <paramref name="errors"/> when anything is wrong.</summary>
        public static FaceFitJob Parse(string text, string projectRoot, List<string> errors)
        {
            object root;
            try { root = Json.Parse(text); }
            catch (FormatException e) { errors.Add("the job is not JSON: " + e.Message); return null; }
            if (!(root is JsonObject o)) { errors.Add("the job is not a JSON object"); return null; }
            var job = new FaceFitJob { Text = text, Sha = Sha256.Text(text) };
            foreach (var pair in o)
                if (Array.IndexOf(Known, pair.Key) < 0) errors.Add("unknown field \"" + pair.Key + "\" (known: " + string.Join(", ", Known) + ")");

            job.Mode = Str(o, "mode", errors, required: true);
            if (job.Mode != null && job.Mode != SweepMode && job.Mode != ReadbackMode)
                errors.Add("\"mode\" is \"" + job.Mode + "\"; it must be \"sweep\" or \"readback\"");
            job.Race = Str(o, "race", errors, required: true);
            job.GameCommit = Str(o, "gameCommit", errors, required: true);
            if (job.GameCommit != null && (job.GameCommit.Length != 40 || job.GameCommit.Any(c => !((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))))
                errors.Add("\"gameCommit\" must be the game commit's full id, 40 lower-case hex characters (it is recorded, never guessed)");
            job.OutputFolder = Str(o, "outputFolder", errors, required: true);
            if (job.OutputFolder != null)
            {
                string problem = OutsideProject(job.OutputFolder, projectRoot);
                if (problem != null) errors.Add("\"outputFolder\" " + problem);
            }

            bool sweep = job.Mode == SweepMode;
            if (o.TryGet("baseline", out var baseline))
            {
                if (!sweep) errors.Add("\"baseline\" belongs to a sweep; a read-back's baseline is each candidate's own look");
                else if (!(baseline is JsonObject b)) errors.Add("\"baseline\" must be an object");
                else
                {
                    foreach (var pair in b)
                        if (pair.Key != "source" && pair.Key != "appearanceId" && pair.Key != "values")
                            errors.Add("unknown field \"baseline." + pair.Key + "\"");
                    job.BaselineSource = Str(b, "source", errors, required: true, prefix: "baseline.");
                    if (job.BaselineSource == "look")
                    {
                        job.BaselineAppearanceId = Str(b, "appearanceId", errors, required: true, prefix: "baseline.");
                        if (b.TryGet("values", out _)) errors.Add("\"baseline.values\" belongs to source \"values\"");
                    }
                    else if (job.BaselineSource == "values")
                    {
                        job.BaselineValues = Values(b, "values", errors, "baseline.values", allowEmpty: false) ?? job.BaselineValues;
                        if (b.TryGet("appearanceId", out _)) errors.Add("\"baseline.appearanceId\" belongs to source \"look\"");
                    }
                    else if (job.BaselineSource == "house")
                    {
                        if (b.Count > 1) errors.Add("\"baseline\" of source \"house\" takes no other field");
                    }
                    else if (job.BaselineSource != null)
                        errors.Add("\"baseline.source\" is \"" + job.BaselineSource + "\"; it must be \"house\", \"look\" or \"values\"");
                }
            }

            if (o.TryGet("plan", out var plan))
            {
                if (!sweep) errors.Add("\"plan\" belongs to a sweep");
                else if (!(plan is JsonObject p)) errors.Add("\"plan\" must be an object");
                else
                {
                    foreach (var pair in p)
                        if (Array.IndexOf(KnownPlan, pair.Key) < 0) errors.Add("unknown field \"plan." + pair.Key + "\" (known: " + string.Join(", ", KnownPlan) + ")");
                    if (p.TryGet("sweepNames", out _)) job.SweepNames = Strings(p, "sweepNames", errors, "plan.sweepNames");
                    if (p.TryGet("positions", out var positions)) job.Positions = Positions01(positions, errors, "plan.positions");
                    if (p.TryGet("positionsByName", out var byName))
                    {
                        if (!(byName is JsonObject map) || map.Count == 0) errors.Add("\"plan.positionsByName\" must be an object of name: [positions]");
                        else
                        {
                            job.PositionsByName = new Dictionary<string, List<double>>(StringComparer.Ordinal);
                            foreach (var pair in map) job.PositionsByName[pair.Key] = Positions01(pair.Value, errors, "plan.positionsByName." + pair.Key);
                        }
                    }
                    if (p.TryGet("randomCount", out var count))
                    {
                        if (!(count is double c) || c < 0 || c > 1000 || c != Math.Floor(c)) errors.Add("\"plan.randomCount\" must be a whole number from 0 to 1000");
                        else job.RandomCount = (int)c;
                    }
                    if (p.TryGet("randomSeed", out var seed))
                    {
                        if (!(seed is double s) || s < 0 || s > 9007199254740991d || s != Math.Floor(s)) errors.Add("\"plan.randomSeed\" must be a whole number from 0 to 2^53-1");
                        else job.RandomSeed = (long)s;
                    }
                    if (p.TryGet("randomRange", out var range))
                    {
                        if (!(range is List<object> r) || r.Count != 2 || !(r[0] is double lo) || !(r[1] is double hi) || lo < 0 || hi > 1 || lo >= hi)
                            errors.Add("\"plan.randomRange\" must be [low, high] with 0 <= low < high <= 1");
                        else { job.RandomMin = lo; job.RandomMax = hi; }
                    }
                    if (p.TryGet("repeatBaseline", out var repeat))
                    {
                        if (!(repeat is bool rb)) errors.Add("\"plan.repeatBaseline\" must be true or false");
                        else job.RepeatBaseline = rb;
                    }
                }
            }

            if (o.TryGet("extraSamples", out var extras))
            {
                if (!sweep) errors.Add("\"extraSamples\" belong to a sweep");
                else if (!(extras is List<object> list)) errors.Add("\"extraSamples\" must be an array");
                else for (int i = 0; i < list.Count; i++)
                {
                    string at = "extraSamples[" + i + "]";
                    if (!(list[i] is JsonObject e)) { errors.Add("\"" + at + "\" must be an object"); continue; }
                    var extra = new Extra();
                    bool single = e.TryGet("name", out _) || e.TryGet("value", out _);
                    bool whole = e.TryGet("values", out _);
                    foreach (var pair in e)
                        if (pair.Key != "name" && pair.Key != "value" && pair.Key != "values") errors.Add("unknown field \"" + at + "." + pair.Key + "\"");
                    if (single == whole) { errors.Add("\"" + at + "\" must hold either \"name\" and \"value\", or \"values\""); continue; }
                    if (single)
                    {
                        extra.Name = Str(e, "name", errors, required: true, prefix: at + ".");
                        extra.Value = Number01(e, "value", errors, at + ".value");
                    }
                    else extra.Values = Values(e, "values", errors, at + ".values", allowEmpty: false);
                    job.Extras.Add(extra);
                }
            }

            if (o.TryGet("persons", out _))
            {
                if (!sweep) errors.Add("\"persons\" belong to a sweep; a read-back builds each candidate's person sample itself");
                else job.Persons = Strings(o, "persons", errors, "persons", allowEmpty: true);
            }

            if (o.TryGet("candidates", out var candidates))
            {
                if (sweep) errors.Add("\"candidates\" belong to a read-back");
                else if (!(candidates is List<object> list) || list.Count == 0) errors.Add("\"candidates\" must be a non-empty array");
                else for (int i = 0; i < list.Count; i++)
                {
                    string at = "candidates[" + i + "]";
                    if (!(list[i] is JsonObject c)) { errors.Add("\"" + at + "\" must be an object"); continue; }
                    foreach (var pair in c)
                        if (pair.Key != "appearanceId" && pair.Key != "round" && pair.Key != "values") errors.Add("unknown field \"" + at + "." + pair.Key + "\"");
                    var candidate = new Candidate
                    {
                        AppearanceId = Str(c, "appearanceId", errors, required: true, prefix: at + "."),
                        Values = Values(c, "values", errors, at + ".values", allowEmpty: false),
                    };
                    if (!c.TryGet("round", out var round) || !(round is double r) || r < 1 || r > 999 || r != Math.Floor(r))
                        errors.Add("\"" + at + ".round\" must be a whole number from 1 to 999");
                    else candidate.Round = (int)r;
                    if (job.Candidates.Any(x => x.AppearanceId == candidate.AppearanceId && x.Round == candidate.Round))
                        errors.Add("\"" + at + "\" repeats " + candidate.AppearanceId + " in round " + candidate.Round);
                    job.Candidates.Add(candidate);
                }
            }
            else if (job.Mode == ReadbackMode) errors.Add("a read-back needs \"candidates\"");

            if (o.TryGet("budgetMinutes", out var budget))
            {
                if (!(budget is double m) || m <= 0 || m > 1440) errors.Add("\"budgetMinutes\" must be a number of minutes above 0 and at most 1440");
                else job.BudgetMinutes = m;
            }
            if (o.TryGet("movedThresholdMm", out var threshold))
            {
                if (!(threshold is double t) || t <= 0 || t > 100) errors.Add("\"movedThresholdMm\" must be a number of millimetres above 0 and at most 100");
                else job.MovedThresholdMm = t;
            }
            if (o.TryGet("headSlots", out _)) job.HeadSlots = Strings(o, "headSlots", errors, "headSlots") ?? job.HeadSlots;
            if (o.TryGet("description", out var description))
            {
                if (!(description is string given)) errors.Add("\"description\" must be a string (it is copied into the manifest)");
                else job.Description = given;
            }

            return errors.Count == 0 ? job : null;
        }

        /// <summary>Null when <paramref name="folder"/> is absolute and outside the project; otherwise what is wrong with it.</summary>
        public static string OutsideProject(string folder, string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Path.IsPathRooted(folder)) return "must be an absolute path";
            string full, root;
            try
            {
                full = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException)
            {
                return "is not a usable path (" + e.Message + ")";
            }
            var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (string.Equals(full, root, comparison) || full.StartsWith(root + Path.DirectorySeparatorChar, comparison))
                return "is inside the project (" + root + "); the export is written outside it, never under Assets, ProjectSettings or Library";
            if (root.StartsWith(full + Path.DirectorySeparatorChar, comparison))
                return "holds the project (" + root + "); choose a folder beside it";
            return null;
        }

        private static string Show(object value) => value is string s ? "\"" + s + "\"" : value is double d ? d.ToString("R", CultureInfo.InvariantCulture) : value == null ? "null" : value.ToString();

        private static List<double> Positions01(object value, List<string> errors, string label)
        {
            var positions = new List<double>();
            if (!(value is List<object> list) || list.Count == 0) { errors.Add("\"" + label + "\" must be a non-empty array of numbers from 0 to 1"); return positions; }
            foreach (var item in list)
            {
                if (!(item is double d) || d < 0 || d > 1) { errors.Add("\"" + label + "\" holds " + Show(item) + "; each must be a number from 0 to 1"); continue; }
                if (positions.Any(x => Math.Abs(x - d) < 1e-9)) errors.Add("\"" + label + "\" names " + Show(d) + " twice");
                // A slider sample's id carries its position to three decimals, so two positions must differ there.
                else if (positions.Any(x => FaceFitPlan.Num(x) == FaceFitPlan.Num(d)))
                    errors.Add("\"" + label + "\" names " + Show(positions.First(x => FaceFitPlan.Num(x) == FaceFitPlan.Num(d))) + " and " + Show(d)
                        + ", which share the sample id position " + FaceFitPlan.Num(d) + "; positions must differ in their first three decimals");
                else positions.Add(d);
            }
            return positions;
        }

        private static string Str(JsonObject o, string key, List<string> errors, bool required, string prefix = "")
        {
            if (!o.TryGet(key, out var value)) { if (required) errors.Add("\"" + prefix + key + "\" is missing"); return null; }
            if (!(value is string s) || s.Trim().Length == 0) { errors.Add("\"" + prefix + key + "\" must be a non-empty string"); return null; }
            return s;
        }

        private static double Number01(JsonObject o, string key, List<string> errors, string label)
        {
            if (!o.TryGet(key, out var value) || !(value is double d) || d < 0 || d > 1)
            {
                errors.Add("\"" + label + "\" must be a number from 0 to 1");
                return double.NaN;
            }
            return d;
        }

        private static Dictionary<string, double> Values(JsonObject o, string key, List<string> errors, string label, bool allowEmpty)
        {
            if (!o.TryGet(key, out var value) || !(value is JsonObject map)) { errors.Add("\"" + label + "\" must be an object of name: value"); return null; }
            if (!allowEmpty && map.Count == 0) { errors.Add("\"" + label + "\" is empty"); return null; }
            var values = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var pair in map)
            {
                if (!(pair.Value is double d) || d < 0 || d > 1) errors.Add("\"" + label + "." + pair.Key + "\" is " + Show(pair.Value) + "; it must be a number from 0 to 1");
                else values[pair.Key] = d;
            }
            return values;
        }

        private static List<string> Strings(JsonObject o, string key, List<string> errors, string label, bool allowEmpty = false)
        {
            if (!o.TryGet(key, out var value) || !(value is List<object> list) || (!allowEmpty && list.Count == 0))
            {
                errors.Add("\"" + label + "\" must be " + (allowEmpty ? "an array" : "a non-empty array") + " of strings");
                return null;
            }
            var strings = new List<string>();
            foreach (var item in list)
            {
                if (!(item is string s) || s.Trim().Length == 0) { errors.Add("\"" + label + "\" holds " + Show(item) + "; each must be a non-empty string"); continue; }
                if (strings.Contains(s)) errors.Add("\"" + label + "\" names \"" + s + "\" twice");
                else strings.Add(s);
            }
            return strings;
        }

        /// <summary>Every DNA name the job names that the race does not have, as errors.</summary>
        public List<string> UnknownNames(ICollection<string> raceNames)
        {
            var errors = new List<string>();
            void Check(string name, string where) { if (name != null && !raceNames.Contains(name)) errors.Add(where + " names \"" + name + "\", which is not a DNA name of " + Race); }
            foreach (var name in BaselineValues.Keys) Check(name, "\"baseline.values\"");
            if (SweepNames != null) foreach (var name in SweepNames) Check(name, "\"plan.sweepNames\"");
            for (int i = 0; i < Extras.Count; i++)
            {
                Check(Extras[i].Name, "\"extraSamples[" + i + "]\"");
                if (Extras[i].Values != null) foreach (var name in Extras[i].Values.Keys) Check(name, "\"extraSamples[" + i + "].values\"");
            }
            for (int i = 0; i < Candidates.Count; i++)
                if (Candidates[i].Values != null) foreach (var name in Candidates[i].Values.Keys) Check(name, "\"candidates[" + i + "].values\"");
            if (PositionsByName != null) foreach (var name in PositionsByName.Keys) Check(name, "\"plan.positionsByName\"");
            return errors;
        }
    }

    /// <summary>
    /// One sample to build: the requested DNA is the baseline's request with <see cref="Overrides"/> laid over it. A FRESH
    /// sample is built on a new bare body, so every name it does not request stands at what the race builds by itself
    /// (a person's sample, the unrequested build); the others reuse one bare body with every name requested.
    /// </summary>
    internal sealed class PlannedSample
    {
        public string Id, Kind, Name, AppearanceId, Repeat, PositionSource;
        public double Value = double.NaN;
        public int Round;
        public bool Fresh;
        public Dictionary<string, double> Overrides = new Dictionary<string, double>(StringComparer.Ordinal);

        public const string Baseline = "baseline", Slider = "slider", Random = "random", ExtraKind = "extra",
            Person = "person", Candidate = "candidate", Unrequested = "unrequested";
    }

    internal static class FaceFitPlan
    {
        /// <summary>The default positions of every swept name: the contract's pairs, for every name (the curved ones' inner pair included).</summary>
        public static readonly double[] DefaultPositions = { .25, .375, .625, .75 };

        public static string Num(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);

        /// <summary>A file-safe id: letters, digits, '.', '_' and '-' kept, everything else '_'.</summary>
        public static string SafeId(string id)
        {
            var chars = id.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!(char.IsLetterOrDigit(chars[i]) && chars[i] < 128) && chars[i] != '.' && chars[i] != '_' && chars[i] != '-') chars[i] = '_';
            return new string(chars);
        }

        /// <summary>
        /// The one spelling of a race in a file or folder name: lowercase, every run of characters outside [a-z0-9] one
        /// hyphen, no hyphen at either end ("Human Male 3.0" is "human-male-3-0").
        /// </summary>
        public static string RaceSlug(string race)
        {
            var slug = new System.Text.StringBuilder();
            foreach (char ch in (race ?? "").ToLowerInvariant())
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')) slug.Append(ch);
                else if (slug.Length > 0 && slug[slug.Length - 1] != '-') slug.Append('-');
            }
            return slug.ToString().TrimEnd('-');
        }

        /// <summary>The positions one name is swept to around its baseline value.</summary>
        public static List<double> PositionsFor(double baseline, IList<double> positions)
        {
            var result = new List<double>();
            if (positions != null) result.AddRange(positions);
            else
            {
                result.AddRange(DefaultPositions);
                if (Math.Abs(baseline - .5) > 1e-6) result.Add(.5);
            }
            result.RemoveAll(p => Math.Abs(p - baseline) < 1e-6);
            result.Sort();
            return result;
        }

        /// <summary>Which pass of the sweep a position belongs to: .25 and .75 first, then .375 and .625, then .5, then any other.</summary>
        public static int Tier(double position)
        {
            bool Is(double x) => Math.Abs(position - x) < 1e-9;
            if (Is(.25) || Is(.75)) return 1;
            if (Is(.375) || Is(.625)) return 2;
            if (Is(.5)) return 3;
            return 4;
        }

        /// <summary>The positions a name is swept to, and where they came from ("job" for the job's own, "default" otherwise).</summary>
        public static List<double> PositionsOf(FaceFitJob job, string name, double baseline, out string source)
        {
            IList<double> given = job.PositionsByName != null && job.PositionsByName.TryGetValue(name, out var own) ? own : job.Positions;
            source = given != null ? "job" : "default";
            return PositionsFor(baseline, given);
        }

        /// <summary>
        /// A sweep, in the order a run cut short still holds the contract's set in: baseline a; every swept name at .25 and
        /// .75; every name at .375 and .625; the .5 samples; any other position; the job's extra samples; the random vectors;
        /// the build with only the house values requested (<paramref name="unrequested"/>); each person twice (a, b); and
        /// baseline b last, so the noise floor spans the whole run. <paramref name="baselineValues"/> holds the value every
        /// swept name has at the baseline (requested or read back), which decides the positions skipped. With
        /// <paramref name="fresh"/> (a sweep around one person's look) every sample is built on a new bare body.
        /// </summary>
        public static List<PlannedSample> Sweep(FaceFitJob job, IList<string> sweepNames, IDictionary<string, double> baselineValues,
            IList<string> persons, bool unrequested, bool fresh)
        {
            var plan = new List<PlannedSample> { new PlannedSample { Id = "baseline-a", Kind = PlannedSample.Baseline, Repeat = "a", Fresh = fresh } };
            var sliders = new List<(int tier, int order, double value, PlannedSample sample)>();
            for (int n = 0; n < sweepNames.Count; n++)
            {
                string name = sweepNames[n];
                if (!baselineValues.TryGetValue(name, out double at)) throw new ArgumentException("the baseline has no value for " + name);
                foreach (double p in PositionsOf(job, name, at, out string source))
                {
                    var sample = new PlannedSample { Id = SafeId("slider-" + name + "-" + Num(p)), Kind = PlannedSample.Slider, Name = name, Value = p, PositionSource = source, Fresh = fresh };
                    sample.Overrides[name] = p;
                    sliders.Add((Tier(p), n, p, sample));
                }
            }
            plan.AddRange(sliders.OrderBy(s => s.tier).ThenBy(s => s.order).ThenBy(s => s.value).Select(s => s.sample));
            for (int i = 0; i < job.Extras.Count; i++)
            {
                var extra = job.Extras[i];
                var sample = new PlannedSample { Id = "extra-" + i, Kind = PlannedSample.ExtraKind, Name = extra.Name, Value = extra.Name != null ? extra.Value : double.NaN, Fresh = fresh };
                if (extra.Name != null) sample.Overrides[extra.Name] = extra.Value;
                else foreach (var pair in extra.Values) sample.Overrides[pair.Key] = pair.Value;
                plan.Add(sample);
            }
            var vectors = RandomVectors(job.RandomSeed, job.RandomCount, sweepNames, job.RandomMin, job.RandomMax);
            for (int k = 0; k < vectors.Count; k++)
                plan.Add(new PlannedSample { Id = "random-" + k, Kind = PlannedSample.Random, Overrides = vectors[k], Fresh = fresh });
            if (unrequested) plan.Add(new PlannedSample { Id = "unrequested", Kind = PlannedSample.Unrequested, Fresh = true });
            foreach (string person in persons)
                foreach (string repeat in new[] { "a", "b" })
                    plan.Add(new PlannedSample { Id = SafeId("person-" + person + "-" + repeat), Kind = PlannedSample.Person, AppearanceId = person, Repeat = repeat, Fresh = true });
            if (job.RepeatBaseline) plan.Add(new PlannedSample { Id = "baseline-b", Kind = PlannedSample.Baseline, Repeat = "b", Fresh = fresh });
            // One id is one file: two samples sharing one would stack the same capture into two rows.
            var twice = plan.GroupBy(p => p.Id, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (twice.Count > 0) throw new ArgumentException("the plan gives two samples the same id: " + string.Join(", ", twice));
            return plan;
        }

        /// <summary>A read-back: each candidate's person sample twice (shared by that person's candidates), then the candidate.</summary>
        public static List<PlannedSample> Readback(FaceFitJob job)
        {
            var plan = new List<PlannedSample>();
            foreach (var candidate in job.Candidates)
            {
                foreach (string repeat in new[] { "a", "b" })
                {
                    string id = SafeId("person-" + candidate.AppearanceId + "-" + repeat);
                    if (!plan.Any(p => p.Id == id))
                        plan.Add(new PlannedSample { Id = id, Kind = PlannedSample.Person, AppearanceId = candidate.AppearanceId, Repeat = repeat, Fresh = true });
                }
                var sample = new PlannedSample
                {
                    Id = SafeId("candidate-r" + candidate.Round + "-" + candidate.AppearanceId), Kind = PlannedSample.Candidate,
                    AppearanceId = candidate.AppearanceId, Round = candidate.Round, Fresh = true,
                };
                foreach (var pair in candidate.Values) sample.Overrides[pair.Key] = pair.Value;
                plan.Add(sample);
            }
            return plan;
        }

        /// <summary>
        /// Seeded random vectors over the swept names, each value in [low, high) rounded to 4 decimals. SplitMix64, so the
        /// same seed gives the same vectors under Mono and .NET alike (System.Random promises no such thing).
        /// </summary>
        public static List<Dictionary<string, double>> RandomVectors(long seed, int count, IList<string> names, double low, double high)
        {
            ulong state = unchecked((ulong)seed);
            ulong Next()
            {
                state = unchecked(state + 0x9E3779B97F4A7C15UL);
                ulong z = state;
                z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
                z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
                return z ^ (z >> 31);
            }
            var vectors = new List<Dictionary<string, double>>();
            for (int k = 0; k < count; k++)
            {
                var vector = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (string name in names)
                {
                    double u = (Next() >> 11) * (1.0 / 9007199254740992.0);
                    vector[name] = Math.Round(low + u * (high - low), 4);
                }
                vectors.Add(vector);
            }
            return vectors;
        }
    }

    /// <summary>Distances, regions and symmetry, all in millimetres of the export frame (positions are metres).</summary>
    internal static class FaceFitStats
    {
        public const string Body = "body";
        public static readonly string[] HeadRegions = { "face", "ears", "skull", "neck" };

        /// <summary>Whether a built slot is the slot a job names: the same name, or UMA's baked copy of it ("&lt;name&gt;_baked_&lt;race&gt;").</summary>
        public static bool SlotMatches(string built, string wanted) =>
            built == wanted || (built != null && built.StartsWith(wanted + "_baked", StringComparison.Ordinal));

        public const string RegionRule = "dominant-bone-v1";

        /// <summary>The rule "dominant-bone-v1" (the lead's ruling on fs2-build-1), one sentence a step, as the manifest states it.</summary>
        public static readonly string[] RegionSteps =
        {
            "1. Take the bone carrying the vertex's largest skin weight on the built mesh (a tie goes to the lower bone index).",
            "2. A bone whose name contains \"Neck\" makes the vertex neck.",
            "3. The bone Head or HeadAdjust itself makes it skull.",
            "4. A descendant of Head in the built skeleton makes it ears when its name contains \"Ear\", face otherwise.",
            "5. Any other bone makes it body, inside the head slot too; the other head-subset slots are regions named after themselves, every other slot is body.",
        };

        /// <summary>The bone of a vertex's largest weight (of four); a tie goes to the lower bone index; -1 when it has none.</summary>
        public static int DominantBone(int[] boneIndices, float[] boneWeights, int v)
        {
            int best = -1;
            float weight = float.NegativeInfinity;
            for (int j = 0; j < 4; j++)
            {
                int bone = boneIndices[v * 4 + j];
                float w = boneWeights[v * 4 + j];
                if (bone < 0) continue;
                if (w > weight || (w == weight && bone < best)) { best = bone; weight = w; }
            }
            return best;
        }

        /// <summary>The head slot's region of a vertex whose dominant bone is <paramref name="bone"/> (steps 2 to 5 of dominant-bone-v1).</summary>
        public static string RegionOfBone(int bone, IList<string> boneNames, IList<int> boneParents) => RegionOfBone(bone, boneNames, boneParents, null);

        /// <summary>
        /// As above, with step 4 answered by <paramref name="underHead"/> (per bone: a descendant of Head in the built
        /// skeleton, read from the real hierarchy) when it is given, so a bone whose ancestors carry no skin weight, and
        /// so are not in the bone list, is still found under Head.
        /// </summary>
        public static string RegionOfBone(int bone, IList<string> boneNames, IList<int> boneParents, IList<bool> underHead)
        {
            if (bone < 0 || bone >= boneNames.Count) return Body;
            string name = boneNames[bone] ?? "";
            if (name.IndexOf("Neck", StringComparison.Ordinal) >= 0) return "neck";
            if (name == "Head" || name == "HeadAdjust") return "skull";
            if (underHead != null && bone < underHead.Count)
                return underHead[bone] ? (name.IndexOf("Ear", StringComparison.Ordinal) >= 0 ? "ears" : "face") : Body;
            int guard = 0;
            for (int up = boneParents[bone]; up >= 0 && up < boneNames.Count && guard++ < 1000; up = boneParents[up])
                if (boneNames[up] == "Head") return name.IndexOf("Ear", StringComparison.Ordinal) >= 0 ? "ears" : "face";
            return Body;
        }

        /// <summary>
        /// Every vertex's region by dominant-bone-v1, and its dominant bone (-1 for none). Returns the region names in code order:
        /// face, ears, skull, neck, then the other head-subset slots, then body.
        /// </summary>
        public static sbyte[] Regions(IList<string> slotNames, int[] slotOffset, int[] slotCount, IList<string> headSlots,
            int[] boneIndices, float[] boneWeights, IList<string> boneNames, IList<int> boneParents,
            out List<string> regionNames, out int[] dominant, IList<bool> underHead = null)
        {
            regionNames = new List<string>(HeadRegions);
            for (int h = 1; h < headSlots.Count; h++) regionNames.Add(headSlots[h]);
            regionNames.Add(Body);
            int total = 0;
            for (int k = 0; k < slotCount.Length; k++) total = Math.Max(total, slotOffset[k] + slotCount[k]);
            var regions = new sbyte[total];
            dominant = new int[total];
            int bodyCode = regionNames.IndexOf(Body);
            for (int k = 0; k < slotNames.Count; k++)
            {
                int head = -1;
                for (int h = 0; h < headSlots.Count; h++) if (SlotMatches(slotNames[k], headSlots[h])) { head = h; break; }
                for (int v = slotOffset[k]; v < slotOffset[k] + slotCount[k]; v++)
                {
                    int bone = DominantBone(boneIndices, boneWeights, v);
                    dominant[v] = bone;
                    int code = bodyCode;
                    if (head == 0) code = regionNames.IndexOf(RegionOfBone(bone, boneNames, boneParents, underHead));
                    else if (head > 0) code = regionNames.IndexOf(headSlots[head]);
                    regions[v] = (sbyte)code;
                }
            }
            return regions;
        }

        public static double DistanceMm(float[] a, float[] b, int v)
        {
            double dx = a[v * 3] - b[v * 3], dy = a[v * 3 + 1] - b[v * 3 + 1], dz = a[v * 3 + 2] - b[v * 3 + 2];
            return Math.Sqrt(dx * dx + dy * dy + dz * dz) * 1000.0;
        }

        /// <summary>The largest distance between two captures over a range of vertices, in millimetres.</summary>
        public static double MaxMm(float[] a, float[] b, int start, int count)
        {
            double max = 0;
            for (int v = start; v < start + count; v++) max = Math.Max(max, DistanceMm(a, b, v));
            return max;
        }

        /// <summary>
        /// The mirror symmetry of one slot about x = 0: for each vertex, the distance from its mirror image (-x, y, z) to the
        /// nearest vertex of the same slot. Returns {max, 99th percentile, median} in millimetres (nearest-rank percentiles).
        /// </summary>
        public static double[] Symmetry(float[] positions, int start, int count)
        {
            if (count <= 0) return new[] { double.NaN, double.NaN, double.NaN };
            var distances = new double[count];
            for (int i = 0; i < count; i++)
            {
                int vi = (start + i) * 3;
                double mx = -positions[vi], my = positions[vi + 1], mz = positions[vi + 2];
                double best = double.MaxValue;
                for (int j = 0; j < count; j++)
                {
                    int vj = (start + j) * 3;
                    double dx = positions[vj] - mx, dy = positions[vj + 1] - my, dz = positions[vj + 2] - mz;
                    double d = dx * dx + dy * dy + dz * dz;
                    if (d < best) best = d;
                }
                distances[i] = Math.Sqrt(best) * 1000.0;
            }
            Array.Sort(distances);
            return new[] { distances[count - 1], Rank(distances, .99), Rank(distances, .5) };
        }

        private static double Rank(double[] sorted, double fraction)
        {
            int index = (int)Math.Ceiling(fraction * sorted.Length) - 1;
            return sorted[Math.Max(0, Math.Min(sorted.Length - 1, index))];
        }
    }
}
