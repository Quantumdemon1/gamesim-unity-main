using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Old-save acceptance (the owner's decision; Assets/Plans/OLD-SAVE-ACCEPTANCE.md): every committed save, one per
    /// save version and rules combination that has a real file, loads in this build, migrates in memory, validates,
    /// replays the command script its reference build recorded to that build's season digest, byte for byte, and every
    /// reader the screens use renders it without an exception, a hidden ballot or a number for odds.
    ///
    /// <para>The fixtures are real saves written by the standalone verification walks of 2026-09-28 to 2026-10-10
    /// (Fixtures/OldSaves/manifest.json), copied byte for byte. The reference build is the oldest build that loads the
    /// save with this build's rules: the save's own schema's from 21 on, and for 16 to 20 the build that introduced
    /// schema 20 (7f398b94), because the migrations to 17, 18, 19 and 20 start their rules the week after the save's, so
    /// a build before 20 plays the next week under fewer of them by design. Tools/OldSaves recorded each script and
    /// digest with that build's own sources, under the editor's Mono.</para>
    ///
    /// <para>Three digests (<see cref="OldSaveTrace"/>): the season's - the cross-build gate - must be the reference
    /// build's; the whole state's, blind to prose, and the verbatim text are this build's, pinned when the fixture was
    /// curated (where they differ from the reference build's, the plan says why: the ballot privacy of UI-UX-PASS-PLAN
    /// B0 and writing passes). A change that moves any of them on an old season is caught here.</para>
    /// </summary>
    public sealed class OldSaveAcceptanceTests
    {
        private static string Root =>
#if UNITY_EDITOR
            Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "OldSaves");
#else
            Path.Combine(AppContext.BaseDirectory, "OldSaves");
#endif

        private static JObject manifest;
        private static JObject Manifest => manifest ?? (manifest = JObject.Parse(File.ReadAllText(Path.Combine(Root, "manifest.json"))));

        private static JObject Entry(string id) =>
            Manifest["episodes"].Concat(Manifest["careers"]).Cast<JObject>().Single(e => (string)e["id"] == id);

        /// <summary>The episode fixtures by id. A fixed list, so a fixture that goes missing from the manifest fails rather than vanishing.</summary>
        private static readonly string[] Episodes =
        {
            "s16-director-w1-hoh", "s16-director-final-speeches", "s16-director-finished-winner", "s16-default-w1-social",
            "s17-director-finished", "s17-default-w1-social",
            "s18-director-w1-eviction", "s18-default-w1-social",
            "s19-director-finished", "s19-default-w1-social",
            "s20-director-finished-runner-up", "s20-default-w1-social",
            "s21-director-w1-hoh", "s21-default-w1-social",
            "s26-mode1-finished", "s26-default-w1-social",
            "s28-mode1-finished", "s28-mode2-finished", "s28-default-w1-social",
        };

        private static readonly string[] Careers = { "career-earliest", "career-latest" };

        // ------------------------------------------------------------------ the fixture set

        [Test]
        public void TheManifestListsEveryFixtureAndEveryFileIsTheSourceByteForByte()
        {
            var listed = Manifest["episodes"].Select(e => (string)e["id"]).ToList();
            Assert.That(listed, Is.EquivalentTo(Episodes), "The manifest's episodes are the fixtures this class plays.");
            Assert.That(Manifest["careers"].Select(e => (string)e["id"]), Is.EquivalentTo(Careers));
            foreach (var entry in Manifest["episodes"].Concat(Manifest["careers"]).Cast<JObject>())
            {
                var bytes = File.ReadAllBytes(Path.Combine(Root, (string)entry["save"]));
                Assert.That(bytes.Length, Is.EqualTo((int)entry["saveBytes"]), (string)entry["id"]);
                Assert.That(Sha256(bytes), Is.EqualTo((string)entry["saveSha256"]), (string)entry["id"] + ": never reseal or renormalise a real save.");
                Assert.That((string)entry["source"], Does.StartWith("GamesimVerify/"), "Where it came from, without a local path.");
            }
            // One fixture per real cell (the plan's matrix): each schema with a real file, in each profile it was saved under.
            var cells = Manifest["episodes"].Select(e => (int)e["schema"] + "/" + (string)e["profile"] + "/uc" + ((int?)e["rules"]["unifiedCommitmentRulesVersion"] ?? 0)
                + "/allweek" + Math.Min(1, (int?)e["rules"]["allWeekRulesStartWeek"] ?? 0)).Distinct().ToList();
            Assert.That(cells, Is.EquivalentTo(new[]
            {
                "16/director/uc0/allweek0", "16/default/uc0/allweek0", "17/director/uc0/allweek0", "17/default/uc0/allweek0",
                "18/director/uc0/allweek0", "18/default/uc0/allweek0", "19/director/uc0/allweek0", "19/default/uc0/allweek0",
                "20/director/uc0/allweek0", "20/default/uc0/allweek0", "21/director/uc0/allweek0", "21/default/uc0/allweek0",
                "26/director/uc1/allweek0", "26/default/uc0/allweek0",
                "28/director/uc1/allweek0", "28/director/uc2/allweek1", "28/default/uc0/allweek0",
            }));
        }

        // ------------------------------------------------------------------ load, migrate, validate, replay

        [TestCaseSource(nameof(Episodes))]
        public void AnOldSaveLoadsMigratesValidatesAndReplaysToItsRecordedDigest(string id)
        {
            var entry = Entry(id);
            int schema = (int)entry["schema"];
            using var slot = new Slot((string)entry["save"]);
            var payload = (JObject)JObject.Parse(File.ReadAllText(slot.Store.SavePath))["state"];
            Assert.That((int)payload["schemaVersion"], Is.EqualTo(schema));

            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
            if (schema < 28) Assert.That(message, Does.Contain("Schema " + schema + " history was migrated to schema 28 in memory."));
            else Assert.That(message, Does.Not.Contain("migrated"));
            Assert.That(File.ReadAllBytes(slot.Store.SavePath), Is.EqualTo(slot.Original), "Loading never rewrites the save.");
            Assert.That(File.Exists(slot.Store.BackupPath), Is.False, "Loading neither creates nor rotates a backup.");
            Assert.That(loaded.schemaVersion, Is.EqualTo(28));
            Assert.That(EpisodeValidation.TryValidate(loaded, out var error), Is.True, error);
            AssertRulesKept(payload, loaded);

            var (digest, state, text, final) = Replay(id, loaded);
            Assert.That(digest, Is.EqualTo((string)entry["digest"]),
                id + ": the season played from this save is not the season its reference build " + entry["referenceBuild"] + " played.");
            Assert.That(state, Is.EqualTo((string)entry["stateDigest"]), id + ": the state this build writes for the old season moved.");
            Assert.That(text, Is.EqualTo((string)entry["textDigest"]), id + ": the words this build writes for the old season moved.");
            Assert.That(final.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(final.winnerId, Is.EqualTo((string)entry["winnerId"]));

            // The disk format changes only on an explicit save: the original bytes become the backup, and the season reloads as it was.
            slot.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(slot.Store.BackupPath), Is.EqualTo(slot.Original), "The pre-migration bytes are kept as the backup.");
            Assert.That(slot.Store.TryLoad(out var again, out message), Is.True, message);
            Assert.That(message, Does.Not.Contain("migrated"));
            Assert.That(OldSaveTrace.Checkpoint(again, 28), Is.EqualTo(OldSaveTrace.Checkpoint(loaded, 28)), "Saved and loaded again, the season is unchanged.");
        }

        /// <summary>
        /// A migration keeps every rule field the save carries and starts each one it adds where its step says: the read,
        /// lever, week and agency rules the week after the save's (schemas 17 to 20), every later rule switched off.
        /// </summary>
        private static void AssertRulesKept(JObject payload, EpisodeState loaded)
        {
            int week = (int)payload["week"];
            var after = new Dictionary<string, int>
            {
                ["readRulesStartWeek"] = week + 1, ["leverRulesStartWeek"] = week + 1, ["weekRulesStartWeek"] = week + 1, ["agencyRulesStartWeek"] = week + 1,
            };
            foreach (var field in ShippedRules.Fields(loaded).Where(f => !f.Key.Contains(".")))
            {
                if (payload[field.Key] is JValue kept)
                    Assert.That(field.Value, Is.EqualTo((int)kept), field.Key + " is the save's own.");
                else
                    Assert.That(field.Value, Is.EqualTo(after.TryGetValue(field.Key, out int start) ? start : 0), field.Key + " is the migration's.");
            }
            Assert.That(loaded.story.rulesStartWeek, Is.EqualTo((int)payload["story"]["rulesStartWeek"]));
            Assert.That(loaded.story.rulesVersion, Is.EqualTo((int)payload["story"]["rulesVersion"]));
            Assert.That(loaded.npcSocial.rulesStartWeek, Is.EqualTo((int)payload["npcSocial"]["rulesStartWeek"]));
        }

        /// <summary>The recorded script, applied: every command accepted, every checkpoint hashed at the reference schema.</summary>
        private static (string season, string state, string text, EpisodeState final) Replay(string id, EpisodeState loaded, Action<EpisodeState, EpisodeState> each = null)
        {
            var replay = JObject.Parse(File.ReadAllText(Path.Combine(Root, (string)Entry(id)["replay"])));
            int schema = (int)replay["referenceSchema"];
            var expected = (JArray)replay["checkpoints"];
            var season = new List<string>();
            var state = new List<string>();
            var text = new List<string>();
            void Check(EpisodeState s, int index)
            {
                var (g, b, t) = OldSaveTrace.Checkpoint(s, schema);
                season.Add(g); state.Add(b); text.Add(t);
                Assert.That(g.Substring(0, 16), Is.EqualTo((string)expected[index]["season"]),
                    id + ": checkpoint " + index + " (week " + s.week + " " + s.phase + ", revision " + s.revision + ") is not the reference build's.");
            }

            Check(loaded, 0);
            each?.Invoke(null, loaded);
            var engine = new EpisodeEngine(loaded);
            int i = 0;
            foreach (JObject row in (JArray)replay["commands"])
            {
                var before = engine.Snapshot;
                var command = row.ToObject<EpisodeCommand>();
                var applied = engine.Apply(command);
                i++;
                Assert.That(applied.accepted, Is.True, id + ": command " + i + " (" + command.kind + ", week " + before.week + " " + before.phase + ") refused: " + applied.reason);
                Check(applied.state, i);
                each?.Invoke(before, applied.state);
            }
            Assert.That(i, Is.EqualTo(expected.Count - 1));
            return (OldSaveTrace.Digest(season), OldSaveTrace.Digest(state), OldSaveTrace.Digest(text), engine.Snapshot);
        }

        // ------------------------------------------------------------------ the readers

        [TestCaseSource(nameof(Episodes))]
        public void EveryReaderRendersAnOldSaveWithoutAHiddenBallotOrOdds(string id)
        {
            using var slot = new Slot((string)Entry(id)["save"]);
            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
            int states = 0, lines = 0, hidden = 0;
            Replay(id, loaded, (before, after) =>
            {
                // The loaded state, every phase and every eviction stage the season passes through, and its end.
                if (before != null && before.phase == after.phase && before.evictionStage == after.evictionStage && before.week == after.week
                    && after.phase != EpisodePhase.Finished) return;
                var read = ReadersOf(after, id);
                states++; lines += read.Count;
                hidden += AssertNoHiddenBallot(after, read, id);
                AssertNoOdds(read, id + " week " + after.week + " " + after.phase);
            });
            TestContext.Out.WriteLine(id + ": " + states + " states read, " + lines + " lines, " + hidden + " ballots the player could not place checked.");
            Assert.That(states, Is.GreaterThan(0));
            Assert.That(lines, Is.GreaterThan(0));
        }

        /// <summary>
        /// Every line the readers give the screens for a state: the ballot sentinel's readers (Your week, the houseguest
        /// notes, the commitments, the finalist read, the jury house, the finale's receipts, the player-visible log and
        /// the player's memories, which the notebook, the diary room and a profile print), and beside them Your word,
        /// the alliance read, the vote read where it is available, the weekly recap of every week, the season report's
        /// lines and the career entry at the end. Every string in what each returns, gathered by reflection.
        /// </summary>
        private static List<string> ReadersOf(EpisodeState s, string id)
        {
            var lines = new List<string>();
            void Add(string reader, Func<object> read)
            {
                List<string> said;
                try { said = StringsOf(read(), 0).ToList(); }
                catch (Exception error) { Assert.Fail(id + " week " + s.week + " " + s.phase + ": " + reader + " threw " + error); return; }
                lines.AddRange(said);
            }
            Add("the ballot sentinel's readers", () => BallotPrivacyTests.ReaderLines(s));
            Add("Your word", () => new object[] { YourWord.Word(s), YourWord.Breaches(s).Select(f => YourWord.HeardBy(s, f).Select(l => YourWord.HeardLine(s, f, l)).ToList()).ToList() });
            Add("the alliance read", () => AllianceRead.Read(s));
            Add("the vote read", () => VoteRead.Available(s) ? (object)VoteRead.Read(s) : null);
            Add("the weekly recap", () => WeeklyRecap.Season(s));
            Add("the season report", () => new object[] { SeasonReport.CountLine(s), SeasonReport.GameSenseLine(s) });
            if (s.phase == EpisodePhase.Finished)
            {
                Add("the career entry", () => CareerLedger.Entry(s));
                Assert.That(SeasonBallots.Open(s), Is.True);
            }
            else
            {
                Assert.That(SeasonBallots.Open(s), Is.False, id + ": the tapes are sealed before the finale.");
                Assert.That(SeasonBallots.Read(s), Is.Empty, id + ": the table reads no ballot before the finale.");
            }
            return lines.Where(line => !string.IsNullOrEmpty(line)).ToList();
        }

        /// <summary>
        /// The ballot sentinel (BallotPrivacyTests) over these lines: a ballot the engine logged privately to its voter,
        /// in a week the player cannot place it, and a ballot cast but not yet revealed, are never named by a line. The
        /// season report's ballot table and the jury's ballots are left out by not being read: they open at the finale.
        /// </summary>
        private static int AssertNoHiddenBallot(EpisodeState s, List<string> lines, string id)
        {
            var hidden = BallotPrivacyTests.PrivateBallots(s).Where(b => !KnownBallots.Read(s, b.week).Knows(b.voterId)).ToList();
            if (s.phase == EpisodePhase.Eviction && !s.evictionResolved)
            {
                var sheet = KnownBallots.Read(s, s.week);
                hidden.AddRange(s.votes.Where(v => v.voterId != s.playerId && !sheet.Knows(v.voterId)).Select(v => (s.week, v.voterId, v.targetId)));
            }
            foreach (var (week, voterId, targetId) in hidden)
            {
                // An undated line ("Quinn · Emma · voted to evict them") may be telling the same voter's ballot against the
                // same houseguest in a week the player does know (s21-director-w1-hoh: unknown in week 1, proven in week 4).
                bool knownElsewhere = KnownBallots.Weeks(s).Any(other => other != week && KnownBallots.Read(s, other).TargetOf(voterId) == targetId);
                foreach (var line in lines)
                {
                    var dated = BallotPrivacyTests.WeekOf(line);
                    if (dated != null ? dated != week : knownElsewhere) continue;
                    Assert.That(BallotPrivacyTests.NamesTheBallot(s, line, voterId, targetId), Is.False,
                        id + ", week " + week + ": " + s.Find(voterId).name + "'s ballot is not the player's to know, and a reader says it: \"" + line + "\"");
                    Assert.That(KnownBallots.TellsAnUnknownBallot(s, voterId, BallotPrivacyTests.Unprefixed(line), week), Is.False,
                        id + ", week " + week + ": a reader tells " + s.Find(voterId).name + "'s ballot by its verdict: \"" + line + "\"");
                }
            }
            return hidden.Count;
        }

        /// <summary>
        /// A number for odds: a percentage in a sentence that speaks of somebody's chance - how likely they are to say yes,
        /// accept, vote, keep or break their word. The player is shown KnownOdds' words for those, never the number. A
        /// percentage anywhere else is a competition's own figure: its rules ("drains grip 70% faster") or the player's
        /// performance ("Played: performance 50%").
        /// </summary>
        private static readonly Regex Percentage = new Regex(@"\d\s*%|\d\s*per ?cent", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly string[] ChanceWords = { "chance", "odds", "likel", "probab", "accept", "agree", "say yes", "succeed", "success", "vote", "evict", "trust", "keep", "break", "lean" };

        private static void AssertNoOdds(List<string> lines, string where)
        {
            foreach (var line in lines)
                foreach (var sentence in line.Split(new[] { ". ", "? ", "! ", "\n", " · " }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!Percentage.IsMatch(sentence)) continue;
                    string lower = sentence.ToLowerInvariant();
                    Assert.That(ChanceWords.Where(word => lower.Contains(word)), Is.Empty, where + ": a reader shows a number for odds: \"" + line + "\"");
                }
        }

        /// <summary>Every string a reader's answer holds, through lists and the readers' own types; never a saved DTO's (*State), which a screen prints only through a reader.</summary>
        private static IEnumerable<string> StringsOf(object value, int depth)
        {
            if (value == null || depth > 6) yield break;
            if (value is string text) { yield return text; yield break; }
            if (value is IEnumerable items)
            {
                foreach (var item in items) foreach (var s in StringsOf(item, depth + 1)) yield return s;
                yield break;
            }
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type.Name.EndsWith("State", StringComparison.Ordinal)) yield break;
            if (type.IsGenericType && type.FullName.StartsWith("System.ValueTuple", StringComparison.Ordinal))
            {
                foreach (var field in type.GetFields()) foreach (var s in StringsOf(field.GetValue(value), depth + 1)) yield return s;
                yield break;
            }
            if (type.Namespace == null || !type.Namespace.StartsWith("Gamesim", StringComparison.Ordinal)) yield break;
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                foreach (var s in StringsOf(field.GetValue(value), depth + 1)) yield return s;
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.GetIndexParameters().Length == 0 && p.CanRead))
                foreach (var s in StringsOf(property.GetValue(value), depth + 1)) yield return s;
        }

        // ------------------------------------------------------------------ the career

        [TestCaseSource(nameof(Careers))]
        public void AnOldCareerFileLoadsAndAddsUp(string id)
        {
            var entry = Entry(id);
            var directory = Path.Combine(Path.GetTempPath(), "GamesimOldCareer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var original = File.ReadAllBytes(Path.Combine(Root, (string)entry["save"]));
                var ledger = new CareerLedger(directory);
                File.WriteAllBytes(ledger.FilePath, original);
                var record = ledger.Load();
                Assert.That(ledger.Notice, Is.Null, "A real career file is read, never set aside.");
                Assert.That(ledger.Blocked, Is.False);
                Assert.That(record.schemaVersion, Is.EqualTo((int)entry["schema"]));
                Assert.That(record.seasons, Has.Count.EqualTo((int)entry["seasons"]));
                Assert.That(record.seasons[0].placement, Is.EqualTo((int)entry["placement"]));
                Assert.That(record.seasons[0].outcome, Is.EqualTo((string)entry["outcome"]));
                var summary = CareerSummary.Of(record);
                Assert.That(summary.Seasons, Is.EqualTo(1));
                Assert.That(summary.Line(), Does.StartWith("1 season"));
                Assert.That(summary.Note(), Does.StartWith("Win rate "));
                Assert.That(File.ReadAllBytes(ledger.FilePath), Is.EqualTo(original), "Reading never rewrites the career.");

                // A finished old season goes on the record beside it, once.
                using var slot = new Slot((string)Entry("s16-director-finished-winner")["save"]);
                Assert.That(slot.Store.TryLoad(out var finished, out string message), Is.True, message);
                Assert.That(ledger.Record(finished), Is.True);
                Assert.That(ledger.Record(finished), Is.False, "The same season is recorded once.");
                var again = ledger.Load();
                Assert.That(ledger.Notice, Is.Null);
                Assert.That(again.seasons, Has.Count.EqualTo(2));
                Assert.That(again.seasons[0].sessionId, Is.EqualTo(record.seasons[0].sessionId), "The old entry is kept as it was.");
            }
            finally
            {
                try { Directory.Delete(directory, true); } catch (IOException) { }
            }
        }

        // ------------------------------------------------------------------ helpers

        private static string Sha256(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>A fixture's bytes in a slot of its own, deleted afterwards.</summary>
        private sealed class Slot : IDisposable
        {
            private readonly string directory = Path.Combine(Path.GetTempPath(), "GamesimOldSave-" + Guid.NewGuid().ToString("N"));
            public readonly EpisodeSaveStore Store;
            public readonly byte[] Original;

            public Slot(string save)
            {
                Directory.CreateDirectory(directory);
                Original = File.ReadAllBytes(Path.Combine(Root, save));
                Store = new EpisodeSaveStore(Path.Combine(directory, "episode.json"));
                File.WriteAllBytes(Store.SavePath, Original);
            }

            public void Dispose()
            {
                try { Directory.Delete(directory, true); } catch (IOException) { }
            }
        }
    }
}
