using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// B+ integration retained across schema29. Reflection reaches the actual internal
    /// frozen reader, direct calls prove dispatch wiring, and real temporary slots prove the
    /// checksum/load/save/recovery boundary. The 4,781 internal pure cases remain outside Unity.
    /// Retained fixtures are read-only; all refusal edits are detached JSON controls.
    /// </summary>
    public sealed class FrozenEpisodeV28IntegrationTests
    {
        private const string EdgeReason = "The all-week rules' records are only ever between two houseguests.";
        private const string ActivationReason = "A season without the all-week rules has none of their records.";
        private static readonly string[] CurrentFixtures =
        {
            "s28-mode1-allweek-w2-campaign", "s28-mode1-finished",
            "s28-mode2-allweek-w3-campaign", "s28-mode2-finished"
        };
        private static string FixtureRoot => Path.Combine(UnityEngine.Application.dataPath,
            "Gamesim", "Tests", "EditMode", "Fixtures", "OldSaves");

        [TestCaseSource(nameof(CurrentFixtures))]
        public void CurrentDispatchAndTheActualFrozenReaderAcceptRetainedWriterPayloads(string id)
        {
            var payload = Fixture(id);
            var before = new TokenImage(payload);
            Frozen(28, payload);
            var result = EpisodeSaveMigrations.PrepareCurrentPayload(payload, out bool migrated);
            Assert.That(migrated, Is.True, "Literal28 is historical and must pass its original frozen gate before inert29 defaults.");
            Assert.That(result, Is.Not.SameAs(payload));
            Assert.That(result.ToString(Formatting.None),
                Is.EqualTo(EpisodeSaveMigrations.UpgradeV28ToV29(payload).ToString(Formatting.None)));
            Assert.That((int)result["schemaVersion"], Is.EqualTo(29));
            var inverse28 = (JObject)result.DeepClone(); PersistenceMigrationTests.StripSchema29(inverse28);
            Assert.That(inverse28.ToString(Formatting.None), Is.EqualTo(payload.ToString(Formatting.None)));
            Assert.That((int)result["unifiedCommitmentRulesVersion"], Is.EqualTo(id.Contains("mode2") ? 2 : 1));
            if (id.Contains("mode2"))
            {
                Assert.That(((JArray)result["unifiedVoteReveals"]).Count, Is.GreaterThan(0));
                if (id.Contains("campaign"))
                    Assert.That(((JArray)result["unifiedCommitments"]).Any(row =>
                        (string)row["kind"] == "vote" && (string)row["status"] == "active"), Is.True);
                else Assert.That((int)result["phase"], Is.EqualTo(13));
            }
            // No nested token of the detached return belongs to the original tree.
            var originals = new HashSet<JToken>(payload.DescendantsAndSelf(), TokenIdentity.Instance);
            foreach (var copy in result.DescendantsAndSelf())
                Assert.That(originals.Contains(copy), Is.False, "No returned old or new token aliases any retained input token.");
            ((JArray)result["npcSocial"]["beatPlan"]).Add("caller mutation");
            before.AssertUnchanged(payload);
        }

        [TestCase("s28-mode1-allweek-w2-campaign", "missing-field")]
        [TestCase("s28-mode1-allweek-w2-campaign", "unknown-field")]
        [TestCase("s28-mode1-allweek-w2-campaign", "player-edge")]
        [TestCase("s28-mode2-allweek-w3-campaign", "missing-field")]
        [TestCase("s28-mode2-allweek-w3-campaign", "unknown-field")]
        [TestCase("s28-mode2-allweek-w3-campaign", "player-edge")]
        public void MalformedCurrent28IsRefusedByDispatchItselfBeforeAnyLiveDtoHydration(string id, string defect)
        {
            var control = Fixture(id);
            Frozen(28, control);
            Assert.DoesNotThrow(() => EpisodeSaveMigrations.PrepareCurrentPayload(control, out _));
            var bad = (JObject)control.DeepClone();
            if (defect == "missing-field") bad.Remove("allWeekRulesStartWeek");
            else if (defect == "unknown-field") ((JObject)bad["npcSocial"]).Add("futureDialogueBudget", 1);
            else AddRecord(bad, PlayerEdge(bad, true), "npc-conversation", (int)bad["week"]);
            var before = new TokenImage(bad);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var frozen = Assert.Throws<InvalidDataException>(() => Frozen(28, bad));
                bool migrated = true;
                var dispatch = Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(bad, out migrated));
                Assert.That(migrated, Is.False);
                Assert.That(dispatch.Message, Is.EqualTo(frozen.Message));
                if (defect == "player-edge") Assert.That(dispatch.Message, Is.EqualTo(EdgeReason));
                before.AssertUnchanged(bad);
            }
        }

        [TestCase("s27-mode1-w3-social")]
        [TestCase("s27-default-w2-social")]
        public void Retained27RunsThroughTheFinalFrozen28GateAndRemainsUnmodified(string id)
        {
            var original = Fixture(id);
            var before = new TokenImage(original);
            Frozen(27, original);
            var expected = EpisodeSaveMigrations.UpgradeV27ToV28(original);
            Frozen(28, expected);
            var current = EpisodeSaveMigrations.PrepareCurrentPayload(original, out bool migrated);
            Assert.That(migrated, Is.True);
            Assert.That(current, Is.Not.SameAs(original));
            Assert.That(current.ToString(Formatting.None),
                Is.EqualTo(EpisodeSaveMigrations.UpgradeV28ToV29(expected).ToString(Formatting.None)));
            Assert.That((int)current["schemaVersion"], Is.EqualTo(29));
            Assert.That((int)current["allWeekRulesStartWeek"], Is.Zero);
            Assert.That((int)current["allianceLeakRulesStartWeek"], Is.Zero);
            Assert.That((int)current["pactPlanRulesStartWeek"], Is.Zero);
            Assert.That((int)current["npcSocial"]["beatWindow"], Is.EqualTo(-1));
            Assert.That((JArray)current["ledger"]["plans"], Is.Empty);
            before.AssertUnchanged(original);
        }

        [Test]
        public void A27PayloadAcceptedByItsOwnReaderStillFacesTheFinal28Gate()
        {
            var control = Fixture("s27-mode1-w3-social");
            Frozen(27, control);
            Assert.DoesNotThrow(() => EpisodeSaveMigrations.PrepareCurrentPayload(control, out _));
            // Schema 27's open relationship type text cannot enforce 28's future activation
            // rule. Its accepted shape is a deliberate negative control, not a played history.
            var bad = (JObject)control.DeepClone();
            string player = (string)bad["playerId"];
            var pair = Edges(bad).First(edge => (string)edge["fromId"] != player && (string)edge["toId"] != player);
            AddRecord(bad, pair, "npc-conversation", 1);
            Frozen(27, bad);
            var before = new TokenImage(bad);
            var stepped = EpisodeSaveMigrations.UpgradeV27ToV28(bad);
            Assert.That(Assert.Throws<InvalidDataException>(() => Frozen(28, stepped)).Message, Is.EqualTo(ActivationReason));
            var error = Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(bad, out _));
            Assert.That(error.Message, Is.EqualTo(ActivationReason), "Proves the final gate, not only direct28 dispatch.");
            before.AssertUnchanged(bad);
        }

        [TestCaseSource(nameof(CurrentFixtures))]
        public void RealSlotsLoadSaveRotateAndReloadModeOneAndModeTwoWriterFixtures(string id)
        {
            byte[] source = FixtureBytes(id);
            using var slot = new Slot();
            slot.Write(slot.Store.SavePath, source);
            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(File.ReadAllBytes(slot.Store.SavePath), Is.EqualTo(source), "Loading is read-only.");
            var loadedImage = Payload(loaded);
            Assert.That((int)loadedImage["schemaVersion"], Is.EqualTo(29));
            var historical28 = (JObject)loadedImage.DeepClone(); PersistenceMigrationTests.StripSchema29(historical28);
            Frozen(28, historical28);
            slot.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(slot.Store.BackupPath), Is.EqualTo(source), "Rotation retains the exact writer envelope.");
            Assert.That(slot.Store.TryLoad(out var again, out message), Is.True, message);
            Assert.That(JToken.DeepEquals(Payload(again), loadedImage), Is.True, "State, RNG, authority, receipts and reveal archive.");
            Assert.That(File.ReadAllBytes(Path.Combine(FixtureRoot, id + ".save.json")), Is.EqualTo(source));
        }

        [TestCase(true)] [TestCase(false)]
        public void PlayerEdgeRefusalBlocksOverwriteAndRequiresExplicitBytePreservingBackupRecovery(bool fromPlayer)
        {
            const string id = "s28-mode2-allweek-w3-campaign";
            byte[] source = FixtureBytes(id);
            using var slot = new Slot();
            slot.Write(slot.Store.SavePath, source);
            Assert.That(slot.Store.TryLoad(out var good, out string message), Is.True, message);
            slot.Store.Save(good);
            byte[] backup = File.ReadAllBytes(slot.Store.BackupPath);
            var bad = Fixture(id);
            AddRecord(bad, PlayerEdge(bad, fromPlayer), "veto-save", (int)bad["week"]);
            Assert.That(Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(bad, out _)).Message,
                Is.EqualTo(EdgeReason), "The same actual frozen gate refuses this payload independently of the live validator.");
            slot.Write(slot.Store.SavePath, System.Text.Encoding.UTF8.GetBytes(PersistenceMigrationTests.Envelope(bad)));
            var retained = slot.Image();
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.That(slot.Store.TryLoad(out var refused, out message), Is.False);
                Assert.That(refused, Is.Null);
                Assert.That(message, Does.Contain(EdgeReason).And.Contain("Recover backup").And.Not.Contain("checksum"));
                Assert.Throws<InvalidDataException>(() => slot.Store.Save(good));
                slot.AssertImage(retained);
            }
            Assert.That(slot.Store.TryRecoverBackup(out var recovered, out message), Is.True, message);
            Assert.That(JToken.DeepEquals(Payload(recovered), Payload(good)), Is.True);
            Assert.That(File.ReadAllBytes(slot.Store.SavePath), Is.EqualTo(backup));
            Assert.That(File.ReadAllBytes(slot.Store.BackupPath), Is.EqualTo(backup));
            var held = Directory.GetFiles(slot.Root, "episode.json.before-recovery-*.json");
            Assert.That(held, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllBytes(held[0]), Is.EqualTo(retained["episode.json"]));
            Assert.That(File.ReadAllBytes(Path.Combine(FixtureRoot, id + ".save.json")), Is.EqualTo(source));
        }

        [Test]
        public void ChecksumFailurePrecedesFrozenSemanticRefusalAndPreservesBothCopies()
        {
            byte[] source = FixtureBytes("s28-mode2-allweek-w3-campaign");
            using var slot = new Slot();
            slot.Write(slot.Store.SavePath, source);
            Assert.That(slot.Store.TryLoad(out var good, out string message), Is.True, message);
            slot.Store.Save(good);
            var envelope = JObject.Parse(System.Text.Encoding.UTF8.GetString(source));
            var bad = (JObject)envelope["state"];
            AddRecord(bad, PlayerEdge(bad, true), "veto-forced", (int)bad["week"]);
            Assert.That(Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(bad, out _)).Message,
                Is.EqualTo(EdgeReason));
            // Keep the writer's now-invalid checksum: loader must stop before semantic dispatch.
            slot.Write(slot.Store.SavePath, System.Text.Encoding.UTF8.GetBytes(envelope.ToString(Formatting.Indented)));
            var retained = slot.Image();
            Assert.That(slot.Store.TryLoad(out var refused, out message), Is.False);
            Assert.That(refused, Is.Null);
            Assert.That(message, Does.Contain("checksum").And.Not.Contain(EdgeReason));
            Assert.Throws<InvalidDataException>(() => slot.Store.Save(good));
            slot.AssertImage(retained);
        }

        [TestCase(true)] [TestCase(false)]
        public void InstallRefusesAChecksummedBadModeTwoCandidateBeforeEitherValidCopyChanges(bool rotateBackup)
        {
            const string id = "s28-mode2-allweek-w3-campaign";
            using var slot = new Slot();
            slot.Write(slot.Store.SavePath, FixtureBytes(id));
            Assert.That(slot.Store.TryLoad(out var good, out string message), Is.True, message);
            slot.Store.Save(good);
            Assert.That(slot.Store.TryLoad(out _, out message), Is.True, "Valid primary before Install: " + message);
            var backupReader = new EpisodeSaveStore(slot.Store.BackupPath);
            Assert.That(backupReader.TryLoad(out _, out message), Is.True, "Valid backup before Install: " + message);
            var bad = Fixture(id);
            AddRecord(bad, PlayerEdge(bad, true), "npc-gossip", (int)bad["week"]);
            byte[] candidate = System.Text.Encoding.UTF8.GetBytes(PersistenceMigrationTests.Envelope(bad));
            var retained = slot.Image();
            Assert.That(retained.Keys, Is.EquivalentTo(new[] { "episode.json", "episode.json.backup" }));

            // Save(good) over an already bad primary only tests preflight. Reach the existing
            // private install boundary directly, with valid copies and an independently
            // checksummed bad candidate, so the temporary file must face frozen validation.
            var error = Assert.Throws<InvalidDataException>(() => PrivateInstall(slot.Store, candidate, rotateBackup));

            Assert.That(error.Message, Is.EqualTo("Local save could not be loaded: " + EdgeReason));
            slot.AssertImage(retained);
            Assert.That(Directory.GetFiles(slot.Root, "episode.json.pending-*"), Is.Empty,
                "Refusal cleans its owned pending file without rotating or replacing either valid copy.");
            Assert.That(slot.Store.TryLoad(out var loaded, out message), Is.True, message);
            Assert.That(JToken.DeepEquals(Payload(loaded), Payload(good)), Is.True);
        }

        [Test]
        public void AnActualAcceptedPlayerLieIsNotRejectedByTheNpcOnlyFrozenGuard()
        {
            EpisodeState witnessed = null;
            for (uint seed = 1; seed <= 40 && witnessed == null; seed++)
            {
                var engine = new EpisodeEngine(UnifiedVotePublicCommandTests.Fresh(seed));
                for (int step = 0; step < 800 && engine.Snapshot.phase != EpisodePhase.Finished && engine.Snapshot.week <= 6; step++)
                {
                    var before = engine.Snapshot;
                    var house = before.Active.Where(person => !person.isPlayer).ToArray();
                    if (before.pendingDiary == null && before.Find(before.playerId).status == ContestantStatus.Active && house.Length >= 2)
                    {
                        var lie = EpisodeEngineTests.Command(before, EpisodeCommandKind.SpreadLie);
                        lie.id = "frozen28-lie-" + before.revision;
                        lie.targetId = house[0].id; lie.secondTargetId = house[1].id;
                        var tried = new EpisodeEngine(before.Clone()).Apply(lie);
                        if (tried.accepted && tried.state.relationships.Any(edge =>
                            (edge.fromId == before.playerId || edge.toId == before.playerId)
                            && edge.events.Any(record => record.type == "lie" && record.sequence >= before.nextSequence)))
                        { witnessed = tried.state; break; }
                    }
                    var next = ModeTwoReaderSweep.Next(before);
                    var moved = engine.Apply(next);
                    Assert.That(moved.accepted, Is.True, moved.reason);
                }
            }
            Assert.That(witnessed, Is.Not.Null, "A public SpreadLie command must actually produce a player-edge lie; no synthesized history.");
            Assert.That(witnessed.allWeekRulesStartWeek, Is.EqualTo(1));
            var payload = Payload(witnessed);
            var beforePayload = new TokenImage(payload);
            var historical28 = (JObject)payload.DeepClone(); PersistenceMigrationTests.StripSchema29(historical28);
            var beforeHistorical = new TokenImage(historical28);
            Frozen(28, historical28);
            var prepared = EpisodeSaveMigrations.PrepareCurrentPayload(historical28, out bool migrated);
            Assert.That(migrated, Is.True);
            Assert.That(prepared.ToString(Formatting.None), Is.EqualTo(payload.ToString(Formatting.None)));
            beforeHistorical.AssertUnchanged(historical28);
            beforePayload.AssertUnchanged(payload);
            using var slot = new Slot();
            slot.Store.Save(witnessed);
            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(JToken.DeepEquals(Payload(loaded), payload), Is.True);
        }

        private static JObject Fixture(string id) => (JObject)JObject.Parse(System.Text.Encoding.UTF8.GetString(FixtureBytes(id)))["state"];
        private static byte[] FixtureBytes(string id) => File.ReadAllBytes(Path.Combine(FixtureRoot, id + ".save.json"));
        private static IEnumerable<JObject> Edges(JObject payload) => ((JArray)payload["relationships"]).Cast<JObject>();
        private static JObject PlayerEdge(JObject payload, bool fromPlayer)
        {
            string player = (string)payload["playerId"];
            return Edges(payload).First(edge => fromPlayer ? (string)edge["fromId"] == player : (string)edge["toId"] == player);
        }
        private static void AddRecord(JObject payload, JObject edge, string type, int week)
        {
            int sequence = (int)payload["nextSequence"];
            ((JArray)edge["events"]).Add(new JObject
            {
                ["sequence"] = sequence, ["week"] = week, ["type"] = type,
                ["description"] = "Detached historical gate control", ["impactScore"] = 25.0, ["decayable"] = true
            });
            payload["nextSequence"] = sequence + 1;
        }
        private static void Frozen(int version, JObject original)
        {
            var type = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.FrozenEpisodeV" + version, true);
            var method = type.GetMethod("Validate", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            try { method.Invoke(null, new object[] { original }); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
        private static void PrivateInstall(EpisodeSaveStore store, byte[] candidate, bool rotateBackup)
        {
            var method = typeof(EpisodeSaveStore).GetMethod("Install", BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(byte[]), typeof(bool) }, null);
            Assert.That(method, Is.Not.Null, "The actual production Install method, not a test seam.");
            try { method.Invoke(store, new object[] { candidate, rotateBackup }); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
        private static JObject Payload(EpisodeState state)
        {
            var serializer = (JsonSerializer)typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.SaveJson", true)
                .GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            return JObject.FromObject(state, serializer);
        }

        private sealed class TokenIdentity : IEqualityComparer<JToken>
        {
            internal static readonly TokenIdentity Instance = new TokenIdentity();
            public bool Equals(JToken first, JToken second) => ReferenceEquals(first, second);
            public int GetHashCode(JToken token) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(token);
        }

        private sealed class TokenImage
        {
            private readonly string text;
            private readonly JToken[] tokens, parents, previous, next;
            internal TokenImage(JObject root)
            {
                text = root.ToString(Formatting.None);
                tokens = root.DescendantsAndSelf().ToArray();
                parents = tokens.Select(t => (JToken)t.Parent).ToArray();
                previous = tokens.Select(t => t.Previous).ToArray();
                next = tokens.Select(t => t.Next).ToArray();
            }
            internal void AssertUnchanged(JObject root)
            {
                Assert.That(root.ToString(Formatting.None), Is.EqualTo(text));
                var now = root.DescendantsAndSelf().ToArray();
                Assert.That(now.Length, Is.EqualTo(tokens.Length));
                for (int i = 0; i < now.Length; i++)
                {
                    Assert.That(now[i], Is.SameAs(tokens[i]));
                    Assert.That(now[i].Parent, Is.SameAs(parents[i]));
                    Assert.That(now[i].Previous, Is.SameAs(previous[i]));
                    Assert.That(now[i].Next, Is.SameAs(next[i]));
                }
            }
        }

        private sealed class Slot : IDisposable
        {
            private const string Prefix = "GamesimFrozen28Gate-";
            private readonly string leaf = Prefix + Guid.NewGuid().ToString("N");
            internal readonly string Root;
            internal readonly EpisodeSaveStore Store;
            internal Slot()
            {
                Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), leaf));
                Guard(); Directory.CreateDirectory(Root);
                Store = new EpisodeSaveStore(Path.Combine(Root, "episode.json"));
            }
            internal void Write(string path, byte[] bytes)
            {
                Guard();
                Assert.That(path == Store.SavePath || path == Store.BackupPath, Is.True);
                File.WriteAllBytes(path, bytes);
            }
            internal SortedDictionary<string, byte[]> Image()
            {
                Guard();
                Assert.That(Directory.GetDirectories(Root), Is.Empty);
                return new SortedDictionary<string, byte[]>(Directory.GetFiles(Root).ToDictionary(Path.GetFileName, File.ReadAllBytes),
                    StringComparer.Ordinal);
            }
            internal void AssertImage(SortedDictionary<string, byte[]> expected)
            {
                var actual = Image();
                Assert.That(actual.Keys, Is.EqualTo(expected.Keys), "No extra pending, retained or replaced files during refusal.");
                foreach (var pair in expected) Assert.That(actual[pair.Key], Is.EqualTo(pair.Value), pair.Key);
            }
            private void Guard()
            {
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Assert.That(Path.GetDirectoryName(Root), Is.EqualTo(temp));
                Assert.That(Path.GetFileName(Root), Is.EqualTo(leaf));
                Assert.That(leaf, Does.StartWith(Prefix));
                if (!Directory.Exists(Root)) return;
                Assert.That(File.GetAttributes(Root) & FileAttributes.ReparsePoint, Is.EqualTo((FileAttributes)0));
                Assert.That(Directory.GetDirectories(Root), Is.Empty);
                foreach (string file in Directory.GetFiles(Root))
                    Assert.That(File.GetAttributes(file) & FileAttributes.ReparsePoint, Is.EqualTo((FileAttributes)0));
            }
            public void Dispose()
            {
                Guard();
                if (Directory.Exists(Root)) Directory.Delete(Root, true);
            }
        }
    }
}
