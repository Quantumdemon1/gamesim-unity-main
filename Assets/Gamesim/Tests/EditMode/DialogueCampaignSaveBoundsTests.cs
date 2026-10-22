using System;
using System.IO;
using System.Linq;
using System.Text;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Native SaveStore tests, not for the pure csproj. Uses real public-command dialogue states
    /// with labelled additive capacity padding. Parser-boundary controls use separate new slots;
    /// neither controls nor TryLoad may alter the actual save or its rotated backup.
    /// </summary>
    public sealed class DialogueCampaignSaveBoundsTests
    {
        private const int Limit = 8 * 1024 * 1024;
        private EpisodeState campaign;
        private string voter, directory;

        [OneTimeSetUp]
        public void FindPublicCampaign() => campaign = DialogueCapacityFixture.FindCampaign(out voter);

        [SetUp]
        public void IsolatedDirectory()
        {
            directory = Path.Combine(Path.GetTempPath(), "GamesimDialogueSaveBounds-" + Guid.NewGuid().ToString("N"));
            Assert.That(Directory.Exists(directory), Is.False);
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void RemoveOnlyThisFixtureDirectory()
        {
            if (directory == null) return;
            string resolved = Path.GetFullPath(directory);
            Assert.That(resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(resolved), Does.StartWith("GamesimDialogueSaveBounds-"));
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }

        [Test]
        public void RealSaveStoreRoundTripsEveryPaidNodeAtBothHistoryCapsAndReportsActualSizeAndDepth()
        {
            var source = DialogueCapacityFixture.Pad(campaign, 397, 99);
            var store = new EpisodeSaveStore(Path.Combine(directory, "actual.json"));
            var engine = new EpisodeEngine(SaveAndReload(store, source, "before-entry"));
            var entered = DialogueCapacityFixture.Accept(engine, DialogueCapacityFixture.Start(engine.Snapshot, voter));
            string id = entered.dialogue.episodes.Single().id;
            engine = new EpisodeEngine(SaveAndReload(store, entered, "case"));
            var middle = DialogueCapacityFixture.Accept(engine,
                DialogueCapacityFixture.Choose(engine.Snapshot, id, "case", "appeal-personally"));
            engine = new EpisodeEngine(SaveAndReload(store, middle, "finish-personal"));
            var closed = DialogueCapacityFixture.Accept(engine,
                DialogueCapacityFixture.Choose(engine.Snapshot, id, "finish-personal", "make-plea"));
            var loaded = SaveAndReload(store, closed, "answered");
            Assert.That(loaded.houseEvents.Count, Is.EqualTo(400));
            Assert.That(loaded.storylines.Count, Is.EqualTo(100));
            DialogueCapacityFixture.AssertClosed(loaded, id, "answered", 3);
            DialogueCapacityFixture.AssertPaddingUnchanged(source, loaded);
        }

        [TestCase(0, true)]
        [TestCase(1, false)]
        public void ExactEightMiBFileBoundaryUsesARealChecksummedSavePlusOnlyTrailingWhitespace(int extra, bool accepts)
        {
            var original = OriginalAtFinish(out var state);
            var before = CaptureOriginals(original);
            byte[] actual = before[0];
            Assert.That(actual.Length, Is.LessThan(Limit));
            var control = new byte[Limit + extra];
            Buffer.BlockCopy(actual, 0, control, 0, actual.Length);
            for (int i = actual.Length; i < control.Length; i++) control[i] = 0x20;
            string path = Path.Combine(directory, "size-control-" + extra + ".json");
            CreateNew(path, control);
            var boundaryStore = new EpisodeSaveStore(path);
            bool result = boundaryStore.TryLoad(out var loaded, out string error);
            Assert.That(result, Is.EqualTo(accepts), error);
            if (accepts)
            {
                DialogueCapacityFixture.Valid(loaded);
                Assert.That(DialogueCapacityFixture.Json(loaded), Is.EqualTo(DialogueCapacityFixture.Json(state)));
            }
            else
            {
                Assert.That(loaded, Is.Null);
                Assert.That(error, Is.EqualTo("Local save could not be loaded: Save exceeds the supported size limit."));
            }
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(control));
            Assert.That(File.Exists(boundaryStore.BackupPath), Is.False);
            AssertOriginals(original, before);
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(3));
            // The exact-limit positive is a FILE/parser boundary, not a worst-case payload claim.
        }

        [TestCase(64)]
        [TestCase(65)]
        public void DepthBoundaryDistinguishesEnvelopeRefusalFromParserRefusalWithoutChangingOriginals(int depth)
        {
            var original = OriginalAtFinish(out _);
            var before = CaptureOriginals(original);
            string text = Encoding.UTF8.GetString(before[0]);
            int end = text.LastIndexOf('}');
            Assert.That(end, Is.GreaterThan(0));
            Assert.That(text.Substring(end + 1).Trim(), Is.Empty);
            // Root object is container depth 1. This unknown envelope member is deliberately
            // invalid at BOTH depths; 64 must reach envelope admission, 65 must stop in parsing.
            string probe = text.Substring(0, end) + ",\"capacityDepthProbe\":"
                + new string('[', depth - 1) + "0" + new string(']', depth - 1) + "}";
            byte[] bytes = Encoding.UTF8.GetBytes(probe);
            Assert.That(bytes.Length, Is.LessThan(Limit));
            Assert.That(ContainerDepth(probe), Is.EqualTo(depth));
            string path = Path.Combine(directory, "depth-control-" + depth + ".json");
            CreateNew(path, bytes);
            var boundaryStore = new EpisodeSaveStore(path);
            Assert.That(boundaryStore.TryLoad(out var loaded, out string error), Is.False);
            Assert.That(loaded, Is.Null);
            if (depth == 64)
                Assert.That(error, Is.EqualTo("Local save could not be loaded: Unknown or unsupported Unity save envelope."));
            else
            {
                Assert.That(error, Does.Contain("MaxDepth of 64 has been exceeded"));
                Assert.That(error, Does.Not.Contain("Unknown or unsupported Unity save envelope"));
            }
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
            Assert.That(File.Exists(boundaryStore.BackupPath), Is.False);
            AssertOriginals(original, before);
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(3));
        }

        private EpisodeSaveStore OriginalAtFinish(out EpisodeState state)
        {
            var source = DialogueCapacityFixture.Pad(campaign, 397, 99);
            var engine = new EpisodeEngine(source);
            var entered = DialogueCapacityFixture.Accept(engine, DialogueCapacityFixture.Start(source, voter));
            string id = entered.dialogue.episodes.Single().id;
            state = DialogueCapacityFixture.Accept(engine,
                DialogueCapacityFixture.Choose(entered, id, "case", "appeal-personally"));
            var store = new EpisodeSaveStore(Path.Combine(directory, "original.json"));
            SaveAndReload(store, entered, "original-backup-case");
            SaveAndReload(store, state, "original-primary-finish");
            Assert.That(File.Exists(store.BackupPath), Is.True);
            return store;
        }

        private static EpisodeState SaveAndReload(EpisodeSaveStore store, EpisodeState state, string node)
        {
            DialogueCapacityFixture.Valid(state);
            string before = DialogueCapacityFixture.Json(state);
            store.Save(state);
            Assert.That(DialogueCapacityFixture.Json(state), Is.EqualTo(before));
            byte[] primary = File.ReadAllBytes(store.SavePath);
            byte[] backup = File.Exists(store.BackupPath) ? File.ReadAllBytes(store.BackupPath) : null;
            int depth = ContainerDepth(Encoding.UTF8.GetString(primary));
            Assert.That(primary.Length, Is.LessThanOrEqualTo(Limit));
            Assert.That(depth, Is.LessThanOrEqualTo(64));
            TestContext.Out.WriteLine("Actual capacity SaveStore output: node=" + node
                + " events=" + state.houseEvents.Count + " cycles=" + state.storylines.Count
                + " bytes=" + primary.Length + " containerDepth=" + depth + ".");
            Assert.That(store.TryLoad(out var loaded, out string message), Is.True, message);
            DialogueCapacityFixture.Valid(loaded);
            Assert.That(DialogueCapacityFixture.Json(loaded), Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(primary));
            if (backup == null) Assert.That(File.Exists(store.BackupPath), Is.False);
            else Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(backup));
            return loaded;
        }

        private static int ContainerDepth(string json)
        {
            using var text = new StringReader(json);
            // Independent finite-fixture measurement, not the store's configured parser limit.
            using var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = null };
            int maximum = 0;
            while (reader.Read())
                if (reader.TokenType == JsonToken.StartObject || reader.TokenType == JsonToken.StartArray)
                    maximum = Math.Max(maximum, reader.Depth + 1);
            return maximum;
        }

        private static byte[][] CaptureOriginals(EpisodeSaveStore store) =>
            new[] { File.ReadAllBytes(store.SavePath), File.ReadAllBytes(store.BackupPath) };

        private static void AssertOriginals(EpisodeSaveStore store, byte[][] before)
        {
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(before[0]));
            Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(before[1]));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(store.SavePath), "*.pending-*"), Is.Empty);
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(store.SavePath), "*.before-recovery-*"), Is.Empty);
        }

        private static void CreateNew(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
        }
    }
}
