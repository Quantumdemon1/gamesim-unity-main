using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Schema 28 (WAVE-D-NPC-PACTS-PLAN §0.3, W28): the inert27-to-28 step and real test-owned
    /// SaveStore controls. A literal-27 payload here is the 27 build's own output: the retained 26
    /// corpus through the real UpgradeV26ToV27, which is exactly what that build loads it as and
    /// writes on its next save. The envelopes sealed around them are synthetic, not retained bytes.
    /// The step adds three zero start weeks, D2's 0/-1/0/0/[]/[] cadence on the NPC world and D3's
    /// empty plans on the ledger - literals only, behind the independent fixed27 contract.
    /// </summary>
    public sealed class PersistenceV28MigrationTests
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Type JsonApi = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.SaveJson", true);
        private static readonly Type Frozen27 = JsonApi.Assembly.GetType("Gamesim.Persistence.FrozenEpisodeV27", true);

        [TestCase(0, 73)] [TestCase(1, 80)] [TestCase(2, 83)]
        public void EveryGenuineAccepted26AliasReachesCurrentThroughExactlyTwoInertSteps(int mode, int count)
        {
            int found = 0;
            foreach (var alias in FrozenV26TestCorpus.Aliases.Where(a => AcceptedAlias(a) && (int)a["mode"] == mode))
            {
                var old = FrozenV26TestCorpus.Payload(alias); string before = Text(old);
                var former27 = EpisodeSaveMigrations.UpgradeV26ToV27(old); Fixed27(former27);
                var next = EpisodeSaveMigrations.UpgradeV27ToV28(former27); Exact(former27, next);
                Assert.That(JToken.DeepEquals(EpisodeSaveMigrations.PrepareV27Payload(old, out bool formerMigrated), former27), Is.True);
                Assert.That(formerMigrated, Is.True);
                var dispatch = EpisodeSaveMigrations.PrepareCurrentPayload(old, out bool migrated);
                Assert.That(migrated, Is.True); Assert.That(JToken.DeepEquals(dispatch, next), Is.True);
                var from27 = EpisodeSaveMigrations.PrepareCurrentPayload(former27, out bool migrated27);
                Assert.That(migrated27, Is.True); Assert.That(Text(from27), Is.EqualTo(Text(next)));
                Assert.That(Text(old), Is.EqualTo(before)); found++;
            }
            Assert.That(found, Is.EqualTo(count)); FrozenV26TestCorpus.AssertPhysicalUnchanged();
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)] [TestCase(12)]
        [TestCase(13)] [TestCase(14)] [TestCase(15)] [TestCase(16)] [TestCase(17)] [TestCase(18)]
        [TestCase(19)] [TestCase(20)] [TestCase(21)] [TestCase(22)] [TestCase(23)] [TestCase(24)]
        [TestCase(25)] [TestCase(26)] [TestCase(27)]
        public void EveryPriorDispatcherKeepsItsExactFormer27ResultBeforeTheSingleStep(int version)
        {
            // Synthetic v1 ancestry is distinct from the retained managed26 corpus.
            var v1 = PersistenceMigrationTests.V1Fixture();
            var old = version == 1 ? v1 : (JObject)typeof(EpisodeSaveMigrations).GetMethod("PrepareV" + version + "Payload", Static)
                .Invoke(null, new object[] { v1, false });
            string before = Text(old);
            var former = EpisodeSaveMigrations.PrepareV27Payload(old, out bool formerMigrated); Fixed27(former);
            Assert.That(formerMigrated, Is.EqualTo(version != 27));
            var next = EpisodeSaveMigrations.PrepareCurrentPayload(old, out bool migrated);
            Assert.That(migrated, Is.True); Exact(former, next); Assert.That(Text(old), Is.EqualTo(before));
            Assert.That((int)former["schemaVersion"], Is.EqualTo(27)); Assert.That((int)next["schemaVersion"], Is.EqualTo(28));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void TheStepAppendsLiteralsWhereTheSerializerDeclaresThem(int mode)
        {
            var former = Former27(mode); var next = EpisodeSaveMigrations.UpgradeV27ToV28(former); Exact(former, next);
            var root = next.Properties().Select(p => p.Name).ToArray();
            Assert.That(root.Skip(root.Length - 3), Is.EqualTo(PersistenceMigrationTests.Schema28Root));
            var social = ((JObject)next["npcSocial"]).Properties().Select(p => p.Name).ToArray();
            Assert.That(social.Skip(social.Length - 6), Is.EqualTo(PersistenceMigrationTests.Schema28NpcSocial));
            Assert.That(((JObject)next["ledger"]).Properties().Last().Name, Is.EqualTo("plans"));
            // The serializer writes the hydrated state's Wave D fields last, in the step's order.
            var written = Payload(next.ToObject<EpisodeState>(Serializer()));
            Assert.That(JToken.DeepEquals(written, next), Is.True);
            var writtenRoot = written.Properties().Select(p => p.Name).ToArray();
            Assert.That(writtenRoot.Skip(writtenRoot.Length - 3), Is.EqualTo(PersistenceMigrationTests.Schema28Root));
            var writtenSocial = ((JObject)written["npcSocial"]).Properties().Select(p => p.Name).ToArray();
            Assert.That(writtenSocial.Skip(writtenSocial.Length - 6), Is.EqualTo(PersistenceMigrationTests.Schema28NpcSocial));
            Assert.That(((JObject)written["ledger"]).Properties().Last().Name, Is.EqualTo("plans"));
            var inverse = (JObject)next.DeepClone(); PersistenceMigrationTests.StripSchema28(inverse);
            Assert.That(Text(inverse), Is.EqualTo(Text(former)), "Removing the literals restores the former compact text exactly.");
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void Current28DispatchIsDetachedAndNeverNormalizesCorruptWaveDStorage(int mode)
        {
            var now = EpisodeSaveMigrations.UpgradeV27ToV28(Former27(mode)); Current(now);
            string before = Text(now); var again = EpisodeSaveMigrations.PrepareCurrentPayload(now, out bool migrated);
            Assert.That(migrated, Is.False); Assert.That(again, Is.Not.SameAs(now)); Assert.That(Text(again), Is.EqualTo(before));
            again["npcSocial"]["beatPlan"] = new JArray("a detached caller"); Assert.That(Text(now), Is.EqualTo(before));
            now["npcSocial"]["beatsFired"] = 1; ((JArray)now["ledger"]["plans"]).Add(JValue.CreateNull()); before = Text(now);
            again = EpisodeSaveMigrations.PrepareCurrentPayload(now, out migrated); Assert.That(migrated, Is.False);
            Assert.That(Text(again), Is.EqualTo(before), "The current dispatch never repairs what it was given.");
            Assert.Throws<InvalidDataException>(() => Current(again)); Assert.That(Text(now), Is.EqualTo(before));
        }

        [TestCase("null-input")] [TestCase("missing-header")] [TestCase("null-header")] [TestCase("text-header")]
        [TestCase("fraction-header")] [TestCase("boolean-header")] [TestCase("old-header")] [TestCase("current-header")]
        [TestCase("future-header")] [TestCase("allianceLeakRulesStartWeek")] [TestCase("pactPlanRulesStartWeek")]
        [TestCase("allWeekRulesStartWeek")] [TestCase("beatWeek")] [TestCase("beatWindow")] [TestCase("beatsFired")]
        [TestCase("beatSeats")] [TestCase("beatPlan")] [TestCase("acts")] [TestCase("plans")]
        [TestCase("unknown-root")] [TestCase("unknown-nested")] [TestCase("missing-marker")] [TestCase("nonzero-marker")]
        public void UpgradeCannotDefaultAnIncompleteOrFutureFormer27Tree(string defect)
        {
            var old = Former27(2);
            var social = (JObject)old["npcSocial"];
            switch (defect)
            {
                case "null-input": old = null; break;
                case "missing-header": old.Remove("schemaVersion"); break;
                case "null-header": old["schemaVersion"] = JValue.CreateNull(); break;
                case "text-header": old["schemaVersion"] = "27"; break;
                case "fraction-header": old["schemaVersion"] = 27.0; break;
                case "boolean-header": old["schemaVersion"] = true; break;
                case "old-header": old["schemaVersion"] = 26; break;
                case "current-header": old["schemaVersion"] = 28; break;
                case "future-header": old["schemaVersion"] = 29; break;
                case "allianceLeakRulesStartWeek": case "pactPlanRulesStartWeek": case "allWeekRulesStartWeek": old.Add(defect, 0); break;
                case "beatWindow": social.Add(defect, -1); break;
                case "beatPlan": case "acts": social.Add(defect, new JArray()); break;
                case "plans": ((JObject)old["ledger"]).Add(defect, new JArray()); break;
                case "unknown-root": old["futureAuthority"] = false; break;
                case "unknown-nested": old["story"]["futurePrivateTruth"] = new JArray(); break;
                case "missing-marker": ((JObject)old["unifiedCommitments"][0]).Remove("voteBindingWeek"); break;
                case "nonzero-marker": old["unifiedCommitments"][0]["voteFirstRevealWeek"] = 1; break;
                default: social.Add(defect, 0); break;
            }
            RefusedUpgrade(old); FrozenV26TestCorpus.AssertPhysicalUnchanged();
        }

        [TestCase("allianceLeakRulesStartWeek", "missing")] [TestCase("allianceLeakRulesStartWeek", "null")]
        [TestCase("allianceLeakRulesStartWeek", "text")] [TestCase("allianceLeakRulesStartWeek", "far")]
        [TestCase("pactPlanRulesStartWeek", "missing")] [TestCase("pactPlanRulesStartWeek", "negative")]
        [TestCase("allWeekRulesStartWeek", "missing")] [TestCase("allWeekRulesStartWeek", "fraction")]
        [TestCase("npcSocial.beatWeek", "one")] [TestCase("npcSocial.beatWindow", "zero")] [TestCase("npcSocial.beatsFired", "one")]
        [TestCase("npcSocial.beatSeats", "one")] [TestCase("npcSocial.beatPlan", "row")] [TestCase("npcSocial.acts", "row")]
        [TestCase("npcSocial.beatPlan", "missing")] [TestCase("npcSocial.acts", "null")] [TestCase("ledger.plans", "row")]
        [TestCase("ledger.plans", "missing")] [TestCase("npcSocial.beatCursor", "unknown")]
        public void CurrentWaveDGuardsRejectWithoutARepairedCandidate(string path, string defect)
        {
            var now = EpisodeSaveMigrations.UpgradeV27ToV28(Former27(2)); Current(now);
            var parts = path.Split('.'); var owner = parts.Length == 1 ? now : (JObject)now[parts[0]]; string name = parts[parts.Length - 1];
            string npc = (string)((JArray)now["contestants"]).OfType<JObject>().First(c => !(bool)c["isPlayer"])["id"];
            switch (defect)
            {
                case "missing": owner.Remove(name); break;
                case "null": owner[name] = JValue.CreateNull(); break;
                case "text": owner[name] = "0"; break;
                case "far": owner[name] = (int)now["week"] + 2; break;
                case "negative": owner[name] = -1; break;
                case "fraction": owner[name] = 0.5; break;
                case "one": owner[name] = 1; break;
                case "zero": owner[name] = 0; break;
                case "unknown": owner[name] = 0; break;
                default:
                    if (name == "beatPlan") ((JArray)owner[name]).Add(npc);
                    else if (name == "acts") ((JArray)owner[name]).Add(JObject.FromObject(new NpcActState { id = "1-3-0", kind = "talk", actorId = npc, week = (int)now["week"], window = 3 }, Serializer()));
                    else ((JArray)owner[name]).Add(JObject.FromObject(new PactPlanRow { week = (int)now["week"], allianceId = "pact-x", throughId = npc, stance = PactPlanStance.Agreed }, Serializer()));
                    break;
            }
            string before = Text(now); Assert.Throws<InvalidDataException>(() => Current(now));
            var refusal = Assert.Catch(() => PersistenceMigrationTests.StripSchema28(now));
            if (refusal is TargetInvocationException invocation) refusal = invocation.InnerException ?? invocation;
            Assert.That(refusal is AssertionException || refusal is InvalidDataException, Is.True, "Never strip neutral-looking fields from an invalid current state.");
            Assert.That(Text(now), Is.EqualTo(before));
        }

        [TestCase(0, "fresh")] [TestCase(1, "promise")] [TestCase(2, "broken-deal")] [TestCase(2, "events")] [TestCase(1, "finished")]
        public void TheTestOnlyInverseOfLiveWitnessesIsTheExactStepInverse(int mode, string kind)
        {
            var state = LegacyDigestSchema27TestStates.Witness(mode, kind);
            var payload = Payload(state); Current(payload); string before = Text(payload);
            var former = (JObject)payload.DeepClone(); PersistenceMigrationTests.StripSchema28(former); Fixed27(former);
            Assert.That(Text(EpisodeSaveMigrations.UpgradeV27ToV28(former)), Is.EqualTo(before));
            Assert.That(JToken.DeepEquals(former, LegacyDigestSchema27TestStates.Neutral27(payload)), Is.True);
            foreach (string week in PersistenceMigrationTests.Schema28Root)
            {
                var enabled = (JObject)payload.DeepClone(); enabled[week] = 1;
                var hydrated = enabled.ToObject<EpisodeState>(Serializer());
                Assert.That(EpisodeValidation.TryValidate(hydrated, out string reason), Is.True, reason);
                Assert.Throws<AssertionException>(() => PersistenceMigrationTests.StripSchema28(enabled), "A design a season plays is never erased.");
            }
        }

        // ------------------------------------------------------------------ the real store

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void A27SaveLoadsReadOnlyAndOnlyAnExplicitSaveWritesSchema28(int mode)
        {
            var old = Former27(mode); var expected = EpisodeSaveMigrations.UpgradeV27ToV28(old); Exact(old, expected);
            using var slot = new Slot(); slot.Replace(slot.Store.SavePath, Encoding.UTF8.GetBytes(PersistenceMigrationTests.Envelope(old)));
            var image = slot.Image();
            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Contain("Schema 27").And.Contain("schema 28 in memory"));
            Assert.That(JToken.DeepEquals(Payload(loaded), expected), Is.True); slot.Unchanged(image);
            Assert.That(File.Exists(slot.Store.BackupPath), Is.False, "Loading does not create or rotate a backup.");
            slot.Store.Save(loaded); Assert.That(slot.Read(slot.Store.BackupPath).SequenceEqual(image["episode.json"]), Is.True);
            var saved = slot.Image(); Assert.That((int)EnvelopeState(saved["episode.json"])["schemaVersion"], Is.EqualTo(28));
            Assert.That(slot.Store.TryLoad(out var resumed, out message), Is.True, message); slot.Unchanged(saved);
            Assert.That(message, Does.Not.Contain("migrated"));
            Assert.That(JToken.DeepEquals(Payload(resumed), expected), Is.True);
            slot.Store.Save(PlayOne(resumed)); Assert.That(slot.Read(slot.Store.BackupPath).SequenceEqual(saved["episode.json"]), Is.True);
            FrozenV26TestCorpus.AssertPhysicalUnchanged();
        }

        [TestCase("allWeekRulesStartWeek")] [TestCase("npcSocial.beatPlan")] [TestCase("ledger.plans")]
        public void AChecksummed27CarryingWaveDStorageCannotBeMistakenForTheInertStep(string path)
        {
            var old = Former27(2); var parts = path.Split('.');
            var owner = parts.Length == 1 ? old : (JObject)old[parts[0]]; string name = parts[parts.Length - 1];
            owner.Add(name, name == "allWeekRulesStartWeek" ? (JToken)new JValue(0) : new JArray());
            RefusedUpgrade(old); string before = Text(old);
            using var slot = new Slot(); slot.Replace(slot.Store.SavePath, Encoding.UTF8.GetBytes(PersistenceMigrationTests.Envelope(old)));
            var image = slot.Image();
            Assert.That(slot.Store.TryLoad(out var absent, out string message), Is.False); Assert.That(absent, Is.Null);
            Assert.That(message, Does.Not.Contain("checksum")); slot.Unchanged(image); Assert.That(Text(old), Is.EqualTo(before));
        }

        [TestCase(1)] [TestCase(2)]
        public void ABadChecksumOnA27SaveWinsBeforeAnyMigration(int mode)
        {
            var envelope = JObject.Parse(PersistenceMigrationTests.Envelope(Former27(mode))); envelope["checksum"] = new string('0', 64);
            using var slot = new Slot(); slot.Replace(slot.Store.SavePath, Encoding.UTF8.GetBytes(envelope.ToString(Formatting.Indented)));
            var image = slot.Image();
            Assert.That(slot.Store.TryLoad(out var absent, out string message), Is.False); Assert.That(absent, Is.Null);
            Assert.That(message, Does.Contain("checksum")); slot.Unchanged(image);
        }

        [TestCase("beats-off")] [TestCase("plan-off")] [TestCase("far-start-week")] [TestCase("line-off")] [TestCase("receipt-off")]
        public void SaveRefusesWaveDStorageItsRulesDoNotAllowAndKeepsBothFiles(string defect)
        {
            using var slot = new Slot(); slot.Replace(slot.Store.SavePath, Encoding.UTF8.GetBytes(PersistenceMigrationTests.Envelope(Former27(2))));
            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
            slot.Store.Save(loaded); var image = slot.Image();
            var candidate = loaded.Clone(); string npc = candidate.contestants.First(c => !c.isPlayer).id;
            switch (defect)
            {
                case "beats-off": candidate.npcSocial.beatPlan.Add(npc); break;
                case "plan-off": candidate.ledger.plans.Add(new PactPlanRow { week = candidate.week, allianceId = "pact-x", throughId = npc, stance = PactPlanStance.Agreed }); break;
                case "far-start-week": candidate.allianceLeakRulesStartWeek = candidate.week + 2; break;
                case "line-off":
                    if (candidate.events.Count >= 256) candidate.events.RemoveAt(0);
                    candidate.events.Add(new EpisodeEvent { sequence = candidate.nextSequence++, week = candidate.week, phase = candidate.phase,
                        kind = WaveDEventKinds.DoubleDealing, text = "A line no rule wrote.", audienceIds = new List<string> { candidate.playerId } });
                    break;
                default:
                    candidate.relationships.First().events.Add(new RelationshipEventState { sequence = candidate.nextSequence++, week = candidate.week,
                        type = StoryReceipts.DoubleDealt, description = "A receipt no rule wrote.", impactScore = -10 });
                    break;
            }
            for (int i = 0; i < 2; i++) { Assert.Throws<InvalidDataException>(() => slot.Store.Save(candidate)); slot.Unchanged(image); }
            Assert.That(slot.Store.TryLoad(out var resumed, out message), Is.True, message);
            slot.Store.Save(PlayOne(resumed)); Assert.That(slot.Read(slot.Store.BackupPath).SequenceEqual(image["episode.json"]), Is.True);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>The 27 build's own output for a retained 26 checkpoint: the real 26-to-27 step.</summary>
        private static JObject Former27(int mode)
        {
            var alias = FrozenV26TestCorpus.Aliases.Single(a => (string)a["kind"] == "checkpoint" && (int)a["mode"] == mode && (string)a["label"] == "promise");
            var old = FrozenV26TestCorpus.Payload(alias);
            var former = EpisodeSaveMigrations.UpgradeV26ToV27(old); Fixed27(former);
            if (mode != 0) Assert.That((JArray)former["unifiedCommitments"], Is.Not.Empty);
            return former;
        }

        private static bool AcceptedAlias(JObject alias) => (string)alias["kind"] == "opening" || (string)alias["kind"] == "checkpoint" || (string)alias["kind"] == "season-boundary";

        /// <summary>The step is exactly the ten inert literals and the header: removing them gives back the former tree, unchanged.</summary>
        private static void Exact(JObject old, JObject next)
        {
            string oldBefore = Text(old), nextBefore = Text(next); Fixed27(old); Current(next);
            Assert.That((int)old["schemaVersion"], Is.EqualTo(27)); Assert.That((int)next["schemaVersion"], Is.EqualTo(28));
            var reverse = (JObject)next.DeepClone(); reverse["schemaVersion"] = 27;
            foreach (string name in PersistenceMigrationTests.Schema28Root)
            { Assert.That(reverse[name]?.Type, Is.EqualTo(JTokenType.Integer)); Assert.That((int)reverse[name], Is.Zero); reverse.Remove(name); }
            var social = (JObject)reverse["npcSocial"];
            foreach (string name in new[] { "beatWeek", "beatWindow", "beatsFired", "beatSeats" })
            { Assert.That(social[name]?.Type, Is.EqualTo(JTokenType.Integer)); Assert.That((int)social[name], Is.EqualTo(name == "beatWindow" ? -1 : 0)); social.Remove(name); }
            foreach (string name in new[] { "beatPlan", "acts" })
            { Assert.That(social[name], Is.TypeOf<JArray>()); Assert.That((JArray)social[name], Is.Empty); social.Remove(name); }
            Assert.That(reverse["ledger"]["plans"], Is.TypeOf<JArray>()); Assert.That((JArray)reverse["ledger"]["plans"], Is.Empty);
            ((JObject)reverse["ledger"]).Remove("plans");
            Assert.That(JToken.DeepEquals(reverse, old), Is.True, "Every RNG, ID, rule, receipt, line and list order survives; nothing is inferred.");
            var guarded = (JObject)next.DeepClone(); PersistenceMigrationTests.StripSchema28(guarded);
            Assert.That(JToken.DeepEquals(guarded, old), Is.True); Assert.That(next, Is.Not.SameAs(old));
            Assert.That(Text(old), Is.EqualTo(oldBefore)); Assert.That(Text(next), Is.EqualTo(nextBefore));
        }

        private static void RefusedUpgrade(JObject old)
        {
            string before = old == null ? "null" : Text(old);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV27ToV28(old));
            Assert.Throws<InvalidDataException>(() => Fixed27(old));
            Assert.That(old == null ? "null" : Text(old), Is.EqualTo(before));
        }

        private static void Fixed27(JObject payload) => Invoke(Frozen27, "Validate", payload);

        private static void Current(JObject payload)
        {
            string before = Text(payload); Invoke(JsonApi, "CheckDtoShape", payload, typeof(EpisodeState), "state");
            var state = payload.ToObject<EpisodeState>(Serializer()); EpisodeSaveValidation.Validate(state);
            Assert.That(state.schemaVersion, Is.EqualTo(28)); Assert.That(Text(payload), Is.EqualTo(before));
        }

        private static EpisodeState PlayOne(EpisodeState s)
        {
            var next = (EpisodeCommand)Invoke(typeof(FrozenEpisodeV25ContractTests), "Next", s, true);
            var engine = new EpisodeEngine(s); string before = Text(Payload(s)); var result = engine.Apply(next);
            Assert.That(result.accepted, Is.True, result.reason); Assert.That(result.duplicate, Is.False);
            Assert.That(result.state.revision, Is.EqualTo(s.revision + 1)); Assert.That(Text(Payload(s)), Is.EqualTo(before));
            Current(Payload(result.state)); return result.state;
        }

        private static JObject EnvelopeState(byte[] bytes) => (JObject)JObject.Parse(new UTF8Encoding(false, true).GetString(bytes))["state"].DeepClone();
        private static object Invoke(Type type, string name, params object[] arguments)
        { try { return type.GetMethod(name, Static).Invoke(null, arguments); } catch (TargetInvocationException error) { throw error.InnerException ?? error; } }
        private static JsonSerializer Serializer() => (JsonSerializer)Invoke(JsonApi, "Serializer");
        private static JObject Payload(EpisodeState s) => JObject.FromObject(s, Serializer());
        private static string Text(JToken token) => token.ToString(Formatting.None);

        private sealed class Slot : IDisposable
        {
            private readonly string id = Guid.NewGuid().ToString("N");
            internal readonly string Root;
            internal readonly EpisodeSaveStore Store;
            internal Slot()
            {
                Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "GamesimV28-" + id)); NoReparse(Root);
                Assert.That(Directory.Exists(Root) || File.Exists(Root), Is.False); Directory.CreateDirectory(Root); NoReparse(Root);
                Store = new EpisodeSaveStore(Path.Combine(Root, "episode.json"));
            }
            internal void Replace(string path, byte[] bytes)
            {
                Assert.That(path == Store.SavePath || path == Store.BackupPath, Is.True); NoReparse(path);
                Assert.That(bytes.Length, Is.LessThanOrEqualTo(8 * 1024 * 1024)); File.WriteAllBytes(path, bytes);
                Assert.That(Read(path).SequenceEqual(bytes), Is.True);
            }
            internal byte[] Read(string path)
            {
                Assert.That(Path.GetDirectoryName(Path.GetFullPath(path)), Is.EqualTo(Root)); NoReparse(path);
                return File.ReadAllBytes(path);
            }
            internal Dictionary<string, byte[]> Image()
            {
                NoReparse(Root); Assert.That(Directory.GetDirectories(Root), Is.Empty); var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (string path in Directory.GetFiles(Root)) result.Add(Path.GetFileName(path), Read(path));
                Assert.That(result.Keys.Where(name => name.Contains(".pending-")), Is.Empty); return result;
            }
            internal void Unchanged(Dictionary<string, byte[]> before)
            {
                var after = Image(); Assert.That(after.Keys, Is.EquivalentTo(before.Keys));
                foreach (var file in before) Assert.That(after[file.Key].SequenceEqual(file.Value), Is.True, file.Key);
            }
            public void Dispose()
            {
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Assert.That(string.Equals(Path.GetDirectoryName(Root), temp, StringComparison.OrdinalIgnoreCase), Is.True);
                Assert.That(Path.GetFileName(Root), Is.EqualTo("GamesimV28-" + id)); NoReparse(Root);
                if (!Directory.Exists(Root)) return;
                Assert.That(Directory.GetDirectories(Root), Is.Empty); foreach (string path in Directory.GetFiles(Root)) NoReparse(path);
                Directory.Delete(Root, true);
            }
            private static void NoReparse(string path)
            {
                for (string at = Path.GetFullPath(path); !string.IsNullOrEmpty(at); at = Path.GetDirectoryName(at))
                { if (File.Exists(at) || Directory.Exists(at)) Assert.That((File.GetAttributes(at) & FileAttributes.ReparsePoint) == 0, Is.True); }
            }
        }
    }
}
