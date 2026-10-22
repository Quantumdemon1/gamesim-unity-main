// The old-save recorder (Assets/Plans/OLD-SAVE-ACCEPTANCE.md). Not part of any Unity assembly: Tools/OldSaves/build.sh
// compiles it, with Assets/Gamesim/Tests/EditMode/OldSaveTrace.cs from the current tree, against one historical
// build's own Simulation and Persistence sources, and runs it under the editor's Mono - the runtime the EditMode
// tests replay on. C# 9 and nothing newer than that build's API: it has to compile at every commit it is pointed at.
//
//   record  <save> <out>                     load a save with that build's store, play it to its end, write the script and digest
//   replay  <save> <record folder>           what the EditMode test does: apply a recorded script, say where it parts (OLDSAVE_RESULT, OLDSAVE_DUMP)
//   generate <out> <seed> <size> <rules> <week> <phase>   play a fresh seeded season to a point and save it with that build's store
//   continue <save> <out> <week> <phase>     load an older save with that build's store, play it on to a point and save it
//                                            with that store: the file a player who carried the season on has, at that build's schema
//   shape                                    every saved field path of that build (OLDSAVE_DEFINES=OLDSAVE_SHAPE_ONLY compiles only this)
//   migrate <save> <out>                     the schema-28 payload this build's chain hands on (what a schema-29 step would validate)
//   resave  <save> <out>                     load with that build's store and save again, as the next autosave would
//   reseal22 <save> <out>                    a schema-22 file of the version's earlier shapes, given the two later fields as
//                                            the literals FrozenEpisodeV22.WithLaterFields writes and sealed again, so a
//                                            build of the last shape (d44bf492 on) can load it and be its reference
//
// Nothing here is read by the tests: they read the fixture, its script and its digests.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Gamesim.Tests.EditMode;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamesim.OldSaves
{
    public static class Recorder
    {
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length >= 1 && args[0] == "shape") { Shape(typeof(EpisodeState), "", 0); return 0; }
                if (args.Length >= 3 && args[0] == "resave")
                {
                    var slot = Path.Combine(Path.GetTempPath(), "oldsave-resave-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(slot);
                    var file = Path.Combine(slot, "episode.json");
                    File.WriteAllBytes(file, File.ReadAllBytes(args[1]));
                    var store = new EpisodeSaveStore(file);
                    if (!store.TryLoad(out var state, out var message)) { Console.Error.WriteLine("LOAD REFUSED: " + message); return 3; }
                    store.Save(state);
                    File.WriteAllBytes(args[2], File.ReadAllBytes(file));
                    Directory.Delete(slot, true);
                    Console.WriteLine("resaved at schema " + state.schemaVersion + ": " + message);
                    return 0;
                }
                if (args.Length >= 3 && args[0] == "reseal22") return Reseal22(args[1], args[2]);
#if !OLDSAVE_BEFORE_SCHEMA3
                if (args.Length >= 3 && args[0] == "migrate")
                {
                    var envelope = JObject.Parse(File.ReadAllText(args[1]));
                    var payload = EpisodeSaveMigrations.PrepareCurrentPayload((JObject)envelope["state"], out _);
                    File.WriteAllText(args[2], payload.ToString(Formatting.None));
                    return 0;
                }
#endif
#if !OLDSAVE_SHAPE_ONLY
                if (args.Length >= 3 && args[0] == "record") return Record(args[1], args[2]);
                if (args.Length >= 3 && args[0] == "replay") return Replay(args[1], args[2]);
                if (args.Length >= 7 && args[0] == "generate")
                    return Generate(args[1], uint.Parse(args[2]), int.Parse(args[3]), args[4], int.Parse(args[5]), (EpisodePhase)Enum.Parse(typeof(EpisodePhase), args[6]));
                if (args.Length >= 5 && args[0] == "continue")
                    return Continue(args[1], args[2], int.Parse(args[3]), (EpisodePhase)Enum.Parse(typeof(EpisodePhase), args[4]));
#endif
                Console.Error.WriteLine("usage: record <save> <out> | generate <out> <seed> <size> <rules> <week> <phase> | continue <save> <out> <week> <phase>");
                return 2;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("FAILED: " + error);
                return 1;
            }
        }

#if !OLDSAVE_SHAPE_ONLY
        // ------------------------------------------------------------------ record

        private static int Record(string save, string outDir)
        {
            Directory.CreateDirectory(outDir);
            var bytes = File.ReadAllBytes(save);
            var slot = Path.Combine(outDir, "slot");
            if (Directory.Exists(slot)) Directory.Delete(slot, true);
            Directory.CreateDirectory(slot);
            var file = Path.Combine(slot, "episode.json");
            File.WriteAllBytes(file, bytes);
            var store = new EpisodeSaveStore(file);
            if (!store.TryLoad(out var state, out var message))
            {
                Console.Error.WriteLine("LOAD REFUSED: " + message);
                return 3;
            }
            if (!File.ReadAllBytes(file).SequenceEqual(bytes)) throw new InvalidOperationException("Loading changed the file.");
            int schema = state.schemaVersion;
            var season = new List<string>();
            var blind = new List<string>();
            var text = new List<string>();
            var rows = new JArray();
            var script = new JArray();
            string dumpAt = Environment.GetEnvironmentVariable("OLDSAVE_DUMP_AT"), dumpTo = Environment.GetEnvironmentVariable("OLDSAVE_DUMP");
            void Check(EpisodeState s, int index)
            {
                var (g, b, t) = OldSaveTrace.Checkpoint(s, schema);
                season.Add(g); blind.Add(b); text.Add(t);
                if (dumpTo != null && dumpAt == index.ToString())
                    File.WriteAllText(dumpTo, JToken.Parse(OldSaveTrace.Canonical(OldSaveTrace.Fields(s), false)).ToString(Formatting.Indented));
                rows.Add(new JObject
                {
                    ["i"] = index, ["week"] = s.week, ["phase"] = s.phase.ToString(), ["revision"] = s.revision,
                    ["randomState"] = s.randomState, ["season"] = g.Substring(0, 16), ["blind"] = b.Substring(0, 16), ["text"] = t.Substring(0, 16),
                });
            }

            Check(state, 0);
            var engine = new EpisodeEngine(state);
            var walker = new Walker();
            int step = 0;
            for (; step < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; step++)
            {
                var s = engine.Snapshot;
                CommandResult applied = null;
                EpisodeCommand chosen = null;
                foreach (var command in walker.Candidates(s, step))
                {
                    var tried = engine.Apply(command);
                    if (tried.accepted) { applied = tried; chosen = command; break; }
                    if (!walker.MayRefuse(command))
                        throw new InvalidOperationException("Week " + s.week + " " + s.phase + " " + command.kind + " refused: " + tried.reason);
                }
                if (applied == null) throw new InvalidOperationException("Week " + s.week + " " + s.phase + ": no command was accepted.");
                var row = OldSaveTrace.Fields(chosen);
                row["kindName"] = chosen.kind.ToString();
                script.Add(row);
                Check(applied.state, step + 1);
            }
            var final = engine.Snapshot;
            if (final.phase != EpisodePhase.Finished) throw new InvalidOperationException("Unfinished after " + step + " commands.");
            var record = new JObject
            {
                ["saveSha256"] = Sha(bytes),
                ["saveBytes"] = bytes.Length,
                ["loadMessage"] = message,
                ["referenceSchema"] = schema,
                ["commands"] = step,
                ["digest"] = OldSaveTrace.Digest(season),
                ["stateDigest"] = OldSaveTrace.Digest(blind),
                ["textDigest"] = OldSaveTrace.Digest(text),
                ["finalWeek"] = final.week,
                ["winnerId"] = final.winnerId,
                ["runnerUpId"] = final.runnerUpId,
                ["checkpoints"] = rows,
            };
            File.WriteAllText(Path.Combine(outDir, "record.json"), record.ToString(Formatting.Indented) + "\n");
            File.WriteAllText(Path.Combine(outDir, "script.json"), script.ToString(Formatting.Indented) + "\n");
            Directory.Delete(slot, true);
            Console.WriteLine("schema " + schema + " commands " + step + " digest " + record["digest"] + " state " + record["stateDigest"] + " text " + record["textDigest"]
                + " winner " + final.winnerId + " | " + message);
            return 0;
        }

        /// <summary>
        /// What the EditMode test does, on this build: load the save, apply a recorded script, hash every checkpoint
        /// at the record's schema, and say where the first checkpoint parts from the record's, if one does.
        /// </summary>
        private static int Replay(string save, string recordDir)
        {
            var record = JObject.Parse(File.ReadAllText(Path.Combine(recordDir, "record.json")));
            var script = JArray.Parse(File.ReadAllText(Path.Combine(recordDir, "script.json")));
            int schema = (int)record["referenceSchema"];
            var expected = (JArray)record["checkpoints"];
            var slot = Path.Combine(Path.GetTempPath(), "oldsave-replay-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(slot);
            var file = Path.Combine(slot, "episode.json");
            File.WriteAllBytes(file, File.ReadAllBytes(save));
            try
            {
                if (!new EpisodeSaveStore(file).TryLoad(out var state, out var message)) { Console.WriteLine("LOAD REFUSED: " + message); return 3; }
                var season = new List<string>();
                var blind = new List<string>();
                var text = new List<string>();
                int firstSeason = -1, firstBlind = -1, firstText = -1;
                string refused = null;
                string Head(string h) => h.Substring(0, Math.Min(16, h.Length));
                void Check(EpisodeState s, int index)
                {
                    (string g, string b, string t) pair;
                    try { pair = OldSaveTrace.Checkpoint(s, schema); }
                    catch (InvalidDataException error) { refused = refused ?? ("checkpoint " + index + ": " + error.Message); pair = ("x", "x", "x"); }
                    season.Add(pair.g); blind.Add(pair.b); text.Add(pair.t);
                    var row = index < expected.Count ? expected[index] : null;
                    if (firstSeason < 0 && (row == null || Head(pair.g) != (string)row["season"])) firstSeason = index;
                    if (firstBlind < 0 && (row == null || Head(pair.b) != (string)row["blind"])) firstBlind = index;
                    if (firstText < 0 && (row == null || Head(pair.t) != (string)row["text"])) firstText = index;
                }
                Check(state, 0);
                var engine = new EpisodeEngine(state);
                int i = 0;
                foreach (JObject row in script)
                {
                    var command = row.ToObject<EpisodeCommand>();
                    var applied = engine.Apply(command);
                    i++;
                    if (!applied.accepted) { Console.WriteLine("REFUSED command " + i + " " + command.kind + " week " + engine.Snapshot.week + " " + engine.Snapshot.phase + ": " + applied.reason); break; }
                    Check(applied.state, i);
                }
                string digest = OldSaveTrace.Digest(season), stateDigest = OldSaveTrace.Digest(blind), textDigest = OldSaveTrace.Digest(text);
                Console.WriteLine("replay " + i + "/" + script.Count + " season " + digest + (digest == (string)record["digest"] ? " MATCH" : " DIFFERS@" + firstSeason)
                    + " state " + stateDigest + (stateDigest == (string)record["stateDigest"] ? " MATCH" : " DIFFERS@" + firstBlind)
                    + " text " + textDigest + (textDigest == (string)record["textDigest"] ? " MATCH" : " DIFFERS@" + firstText)
                    + (refused != null ? " | " + refused : "") + " | " + message);
                if (Environment.GetEnvironmentVariable("OLDSAVE_RESULT") is string resultFile)
                    File.WriteAllText(resultFile, new JObject
                    {
                        ["replayed"] = i, ["of"] = script.Count, ["refused"] = refused, ["loadMessage"] = message,
                        ["digest"] = digest, ["stateDigest"] = stateDigest, ["textDigest"] = textDigest,
                        ["firstSeason"] = firstSeason, ["firstState"] = firstBlind, ["firstText"] = firstText,
                        ["finalPhase"] = engine.Snapshot.phase.ToString(), ["winnerId"] = engine.Snapshot.winnerId,
                    }.ToString(Formatting.Indented) + "\n");
                if (Environment.GetEnvironmentVariable("OLDSAVE_DUMP") is string dump)
                {
                    int at = int.TryParse(Environment.GetEnvironmentVariable("OLDSAVE_DUMP_AT"), out int given) ? given : firstText;
                    if (at >= 0) DumpAt(save, script, schema, at, dump);
                }
                return digest == (string)record["digest"] ? 0 : 4;
            }
            finally { Directory.Delete(slot, true); }
        }

        /// <summary>Writes this build's projected checkpoint at <paramref name="index"/>, for a diff against the reference build's.</summary>
        private static void DumpAt(string save, JArray script, int schema, int index, string outFile)
        {
            var slot = Path.Combine(Path.GetTempPath(), "oldsave-dump-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(slot);
            var file = Path.Combine(slot, "episode.json");
            File.WriteAllBytes(file, File.ReadAllBytes(save));
            new EpisodeSaveStore(file).TryLoad(out var state, out _);
            var engine = new EpisodeEngine(state);
            for (int i = 0; i < index; i++) engine.Apply(((JObject)script[i]).ToObject<EpisodeCommand>());
            var fields = OldSaveTrace.Project(OldSaveTrace.Fields(engine.Snapshot), schema);
            File.WriteAllText(outFile, JToken.Parse(OldSaveTrace.Canonical(fields, false)).ToString(Formatting.Indented));
            Directory.Delete(slot, true);
        }
#endif

        /// <summary>
        /// The saved shape of this build: every public instance field a save stores, one "path type" a line, through
        /// lists and the simulation's own classes. Two builds of one schema with different shapes mean a field was
        /// added without a version, and the older build's files cannot pass the newer one's exact shape check.
        /// </summary>
        private static void Shape(Type type, string path, int depth)
        {
            if (depth > 10) return;
            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            {
                var t = field.FieldType;
                string here = path + field.Name;
                bool list = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>);
                var element = list ? t.GetGenericArguments()[0] : t;
                Console.WriteLine(here + (list ? "[]" : "") + " " + (element.IsEnum ? "enum" : element.Name));
                if (!element.IsPrimitive && !element.IsEnum && element != typeof(string) && element.Namespace != null
                    && element.Namespace.StartsWith("Gamesim", StringComparison.Ordinal))
                    Shape(element, here + (list ? "[]." : "."), depth + 1);
            }
        }

        /// <summary>
        /// Schema 22's earlier shapes (no deals[].linkedDealId before c55a4535, no alliances[].playerJoined before d44bf492)
        /// given the two fields as FrozenEpisodeV22.WithLaterFields gives them - null and false, on every row of a list
        /// where every row lacks the field, nowhere else - and sealed again with this build's own SaveJson canonical form
        /// and hash (by reflection, so the recorder compiles at every build). Nothing else in the file changes; the
        /// result is what a last-shape build reads, so it can record the season as the reference.
        /// </summary>
        private static int Reseal22(string save, string outFile)
        {
            var json = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.SaveJson", true);
            var envelope = (JObject)json.GetMethod("ParseObject").Invoke(null, new object[] { File.ReadAllText(save) });
            var state = (JObject)envelope["state"];
            if ((int)state["schemaVersion"] != 22) throw new InvalidDataException("reseal22 takes a schema-22 save.");
            int linked = Complete(state["deals"] as JArray, "linkedDealId", JValue.CreateNull());
            int joined = Complete(state["alliances"] as JArray, "playerJoined", new JValue(false));
            string canonical = (string)json.GetMethod("Canonical").Invoke(null, new object[] { state });
            envelope["checksum"] = (string)json.GetMethod("Hash").Invoke(null, new object[] { canonical });
            File.WriteAllText(outFile, envelope.ToString(Formatting.Indented));
            Console.WriteLine("resealed: linkedDealId added to " + linked + " deals, playerJoined to " + joined + " alliances");
            return 0;
        }

        private static int Complete(JArray rows, string field, JValue literal)
        {
            if (rows == null || rows.Count == 0 || !rows.All(row => row is JObject o && o.Property(field) == null)) return 0;
            foreach (JObject row in rows) row.Add(field, literal.DeepClone());
            return rows.Count;
        }

        private static string Sha(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

#if !OLDSAVE_SHAPE_ONLY
        // ------------------------------------------------------------------ generate

        /// <summary>
        /// A fresh season as that build's director started one (<paramref name="rules"/> "director": the season
        /// builder's house of <paramref name="size"/>, then that build's own fresh rules, Rules.Fresh) or as its default
        /// scene engine did ("default": the authored catalogue season, nothing switched on; the director's default
        /// engine uses seed 20260910), played by the walker to the first state at <paramref name="week"/> and
        /// <paramref name="phase"/>, then saved by that build's store. The session id is fixed, so a regeneration
        /// writes the same state; only the envelope's savedAt differs.
        /// </summary>
        private static int Generate(string outFile, uint seed, int size, string rules, int week, EpisodePhase phase)
        {
            EpisodeState fresh;
            if (rules == "director")
            {
#if OLDSAVE_BEFORE_SEASON_BUILDER
                throw new ArgumentException("Schema 6's build has no season builder: its director played the catalogue season (default).");
#else
                fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);
                Rules.Fresh(fresh);
#endif
            }
            else if (rules == "default") fresh = ContentCatalog.Create(seed);
            else throw new ArgumentException("rules is director or default");
            fresh.sessionId = "oldsave-" + rules + "-" + seed + "-" + size;
            return PlayOnAndSave(fresh, new Walker("generate-"), outFile, week, phase);
        }

        /// <summary>
        /// An older save carried on by this build, as a player carries it: <paramref name="save"/> loaded by that build's
        /// store from a slot copy (so the file is never touched) - migrated in memory, if it is older - played by the
        /// walker to the first state at <paramref name="week"/> and <paramref name="phase"/>, then saved by that build's
        /// store at its own schema. The walker names its commands "continue-", so they meet neither the save's own ids
        /// nor those of a script recorded from the result.
        /// </summary>
        private static int Continue(string save, string outFile, int week, EpisodePhase phase)
        {
            var slot = Path.Combine(Path.GetTempPath(), "oldsave-continue-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(slot);
            try
            {
                var file = Path.Combine(slot, "episode.json");
                File.WriteAllBytes(file, File.ReadAllBytes(save));
                if (!new EpisodeSaveStore(file).TryLoad(out var state, out var message)) { Console.Error.WriteLine("LOAD REFUSED: " + message); return 3; }
                Console.WriteLine("loaded week " + state.week + " " + state.phase + " revision " + state.revision + ": " + message);
                return PlayOnAndSave(state, new Walker("continue-"), outFile, week, phase);
            }
            finally { Directory.Delete(slot, true); }
        }

        /// <summary>Plays a season by the walker to the first state at <paramref name="week"/> and <paramref name="phase"/> and saves it with that build's store.</summary>
        private static int PlayOnAndSave(EpisodeState start, Walker walker, string outFile, int week, EpisodePhase phase)
        {
            var engine = new EpisodeEngine(start);
            for (int step = 0; step < 4000; step++)
            {
                var s = engine.Snapshot;
                if (s.week == week && s.phase == phase) break;
                if (s.phase == EpisodePhase.Finished || s.week > week) throw new InvalidOperationException("Passed week " + week + " " + phase + " without stopping.");
                CommandResult applied = null;
                foreach (var command in walker.Candidates(s, step))
                {
                    var tried = engine.Apply(command);
                    if (tried.accepted) { applied = tried; break; }
                    if (!walker.MayRefuse(command))
                        throw new InvalidOperationException("Week " + s.week + " " + s.phase + " " + command.kind + " refused: " + tried.reason);
                }
                if (applied == null) throw new InvalidOperationException("Week " + s.week + " " + s.phase + ": no command was accepted.");
            }
            var at = engine.Snapshot;
            if (at.week != week || at.phase != phase) throw new InvalidOperationException("Never reached week " + week + " " + phase + ".");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile)));
            if (File.Exists(outFile)) File.Delete(outFile);
            new EpisodeSaveStore(outFile).Save(at);
            Console.WriteLine("saved schema " + at.schemaVersion + " week " + at.week + " " + at.phase + " revision " + at.revision + " to " + outFile);
            return 0;
        }
#endif
    }

#if !OLDSAVE_SHAPE_ONLY
    /// <summary>
    /// The fixed command walker: the plain legal walk the engine tests use (EpisodeEngineTests.NextCommand), and in the
    /// social week up to two conversations, with the houseguest the season's revision picks. Deterministic in the state
    /// alone, so the build that records a script and any build that replays it agree on what it chose; only the
    /// recorder runs it - a replay reads the script.
    /// </summary>
    public sealed class Walker
    {
        private readonly Dictionary<int, int> talks = new Dictionary<int, int>();
        private readonly string prefix;
        private int sequence;

        /// <summary>A walker naming its commands <paramref name="prefix"/>0001 on: "oldsave-" records a script, "generate-" plays a fresh season to its save point and "continue-" an older save on to its, so a script's ids never meet the save's own.</summary>
        public Walker(string prefix = "oldsave-") { this.prefix = prefix; }

        public bool MayRefuse(EpisodeCommand command) => command.kind == EpisodeCommandKind.Talk
            || (command.kind == EpisodeCommandKind.AnswerJury && command.secondTargetId == "A");

        public IEnumerable<EpisodeCommand> Candidates(EpisodeState s, int step)
        {
#if OLDSAVE_BEFORE_SCHEMA3
            // Schema 2: no diary room, so nothing can be pending.
            if (s.phase == EpisodePhase.Social)
#else
            if (s.phase == EpisodePhase.Social && s.pendingDiary == null)
#endif
            {
                talks.TryGetValue(s.week, out int done);
                var others = s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
                if (done < 2 && others.Count > 0)
                {
                    talks[s.week] = done + 1;
                    var talk = Command(s, EpisodeCommandKind.Talk);
                    talk.targetId = others[(int)((uint)s.revision % (uint)others.Count)].id;
                    yield return talk;
                }
            }
            var next = Next(s);
            yield return next;
            // Under the finale rules (schema 21 on) the player finalist answers with one of the responses the question
            // offers, not the catalogue's A: tried after A, in the order the build offers them.
            if (next.kind == EpisodeCommandKind.AnswerJury && next.secondTargetId == "A")
                foreach (string key in Offered(s))
                {
                    var answer = Command(s, EpisodeCommandKind.AnswerJury);
                    answer.targetId = next.targetId;
                    answer.secondTargetId = key;
                    yield return answer;
                }
        }

        /// <summary>The responses the current jury question offers, by reflection: a build before schema 21 has none.</summary>
        private static IEnumerable<string> Offered(EpisodeState s)
        {
            var exchange = s.juryExchanges[s.juryQuestionIndex];
            var questions = typeof(EpisodeState).Assembly.GetType("Gamesim.Simulation.FinaleQuestions");
            var offered = questions?.GetMethod("Offered", new[] { typeof(string), typeof(string) });
            if (offered == null) return new string[0];
            string category = typeof(JuryExchangeState).GetField("category")?.GetValue(exchange) as string;
            string receipt = typeof(JuryExchangeState).GetField("receiptKind")?.GetValue(exchange) as string;
            return (string[])offered.Invoke(null, new object[] { category, receipt }) ?? new string[0];
        }

        private EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = prefix + (++sequence).ToString("D4"),
            actorId = s.playerId,
            expectedRevision = s.revision,
            expectedPhase = s.phase,
            kind = kind,
        };

        /// <summary>EpisodeEngineTests.NextCommand at ae294b35, line for line.</summary>
        private EpisodeCommand Next(EpisodeState s)
        {
            var c = Command(s, EpisodeCommandKind.Advance);
#if OLDSAVE_BEFORE_SCHEMA3
            if (false) { }
#else
            if (s.pendingDiary != null) { c.kind = EpisodeCommandKind.SkipDiary; c.targetId = s.pendingDiary.id; }
#endif
            else if (EpisodeEngine.IsCompetition(s.phase) && !s.competitionResolved && EpisodeEngine.CompetitionPlayers(s).Any(p => p.isPlayer))
            { c.kind = EpisodeCommandKind.Compete; c.performance = 0.5; }
            else if (s.phase == EpisodePhase.Nomination && s.nominees.Count == 0 && s.hohId == s.playerId)
            {
                c.kind = EpisodeCommandKind.Nominate; var pool = EpisodeEngine.NominationCandidates(s).ToArray(); c.targetId = pool[0].id; c.secondTargetId = pool[1].id;
            }
            else if (s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved && (s.vetoHolderId == s.playerId || (s.hohId == s.playerId && EpisodeEngine.NpcVetoSave(s) != null)))
            {
                c.kind = EpisodeCommandKind.ResolveVeto;
#if OLDSAVE_BEFORE_SCHEMA8
                // Schemas 6 and 7: no Final 4 lock and no eviction stages (EpisodeEngineTests.NextCommand at 44fa4580).
                c.useVeto = EpisodeEngine.ReplacementCandidates(s).Any();
#else
                c.useVeto = EpisodeEngine.ReplacementCandidates(s).Any() && !EpisodeEngine.VetoIsLockedAtFinalFour(s);
#endif
                c.targetId = s.vetoHolderId == s.playerId ? s.nominees[0] : EpisodeEngine.NpcVetoSave(s);
                c.secondTargetId = EpisodeEngine.ReplacementCandidates(s).FirstOrDefault()?.id;
            }
#if OLDSAVE_BEFORE_SCHEMA8
            else if (s.phase == EpisodePhase.Eviction && !s.evictionResolved && !s.votes.Any(v => v.voterId == s.playerId) &&
                (EpisodeEngine.Voters(s).Any(v => v.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(s)))
            { c.kind = EpisodeCommandKind.CastVote; c.targetId = s.nominees[0]; }
#else
            else if (s.phase == EpisodePhase.Eviction && s.evictionStage == EvictionStage.Speeches
                && s.nominees.Contains(s.playerId) && !s.evictionSpeeches.Any(x => x.speakerId == s.playerId))
            { c.kind = EpisodeCommandKind.SubmitEvictionSpeech; c.text = "I'd like to stay. I've been straight with all of you."; }
            else if (s.phase == EpisodePhase.Eviction && !s.evictionResolved
                && (s.evictionStage == EvictionStage.Voting || s.evictionStage == EvictionStage.Tiebreaker)
                && !s.votes.Any(v => v.voterId == s.playerId) &&
                (EpisodeEngine.Voters(s).Any(v => v.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(s)))
            { c.kind = EpisodeCommandKind.CastVote; c.targetId = s.nominees[0]; }
#endif
            else if (s.phase == EpisodePhase.FinalEviction && s.hohId == s.playerId)
            { c.kind = EpisodeCommandKind.FinalEvict; c.targetId = s.Active.First(p => !p.isPlayer).id; }
            else if (s.phase == EpisodePhase.JuryQuestioning && !s.juryExchanges[s.juryQuestionIndex].completed)
            {
                var exchange = s.juryExchanges[s.juryQuestionIndex]; c.kind = EpisodeCommandKind.AnswerJury;
                c.targetId = exchange.finalistId == s.playerId ? exchange.questionerId : exchange.finalistId;
                c.secondTargetId = exchange.finalistId == s.playerId ? "A" : "neutral";
            }
            else if (s.phase == EpisodePhase.FinalSpeeches && s.Active.Any(p => p.isPlayer) && !s.finalSpeeches.Any(p => p.speakerId == s.playerId))
            { c.kind = EpisodeCommandKind.SubmitSpeech; c.text = "I stood by my choices. Thank you for sharing this season."; }
            else if (s.phase == EpisodePhase.Jury && !s.Active.Any(p => p.isPlayer) && !s.votes.Any(v => v.voterId == s.playerId))
            { c.kind = EpisodeCommandKind.CastVote; c.targetId = s.Active.First().id; }
            return c;
        }
    }
#endif
}
