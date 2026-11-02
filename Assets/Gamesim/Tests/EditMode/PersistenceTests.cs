using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.EditMode
{
    public sealed class PersistenceTests
    {
        private string directory;
        private EpisodeSaveStore store;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "GamesimPersistenceTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            store = new EpisodeSaveStore(Path.Combine(directory, "episode.json"));
        }

        [TearDown]
        public void TearDown()
        {
            var resolved = Path.GetFullPath(directory);
            Assert.That(resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(resolved), Does.StartWith("GamesimPersistenceTests-"));
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }

        /// <summary>
        /// A save that will not load has to say why, and the two kinds of "why" are not the same.
        /// A filesystem failure knows only the name of a file the player cannot act on - and on
        /// Windows that path contains their user name - so it reports its kind and nothing else.
        /// A failure about the CONTENT is described in words this project wrote, and those are the
        /// words the player needs; the migration suites assert on them, and so does the second half
        /// of this test. Suppressing both is the mistake this pins shut.
        /// </summary>
        [Test]
        public void AFilesystemFailureNamesItsKindWhileAContentFailureKeepsItsExplanation()
        {
            // The file has to EXIST but be unreadable, or the store answers "no save here" and
            // never reaches the filesystem branch at all. An exclusive lock does it: the first
            // version of this test read a directory instead, and passed through the wrong path.
            File.WriteAllText(store.SavePath, "{}");
            string ioMessage;
            using (File.Open(store.SavePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.That(store.TryLoad(out _, out ioMessage), Is.False);
            }
            Assert.That(ioMessage, Does.Not.Contain(directory),
                "A save failure must never print the file's path back at the player.");
            Assert.That(ioMessage, Does.Contain("IOException").Or.Contain("UnauthorizedAccessException"),
                "but it must still say what kind of failure it was.");

            // The other branch: a payload this project rejects for its content, not its location.
            File.WriteAllText(store.SavePath, "{\"format\":\"gamesim-unity-save\",\"payload\":{},\"checksum\":\"wrong\"}");
            Assert.That(store.TryLoad(out _, out var dataMessage), Is.False);
            Assert.That(dataMessage, Does.Not.Contain(directory),
                "and a content failure must not leak the path either.");
            Assert.That(dataMessage, Does.Not.Contain("Exception"),
                "a content failure is described in this project's own words, not by a type name.");
            Assert.That(dataMessage.Length, Is.GreaterThan("Local save could not be loaded: ".Length + 8),
                "but it must keep the explanation, which is what tells the player what is wrong.");
        }

        [Test]
        public void LocalRoundTrip_PreservesZeroRandomStateAndDetachedHistory()
        {
            var original = ContentCatalog.Create(0);
            original.randomState = 0;
            original.relationships[0].score = 37;
            store.Save(original);
            original.relationships[0].score = -90;
            Assert.That(store.TryLoad(out var loaded, out var message), Is.True, message);
            Assert.That(loaded.seed, Is.EqualTo(0));
            Assert.That(loaded.randomState, Is.EqualTo(0));
            Assert.That(loaded.relationships[0].score, Is.EqualTo(37));
            Assert.That(loaded.events[0].text, Is.EqualTo(original.events[0].text));
            Assert.That(loaded.memories.Count, Is.EqualTo(original.memories.Count));
            Assert.That(Directory.GetFiles(directory, "*.pending-*"), Is.Empty);
        }

        [Test]
        public void DamagedPrimary_BlocksOverwriteAndRequiresExplicitBackupRecovery()
        {
            var first = ContentCatalog.Create(17);
            store.Save(first);
            var second = first.Clone();
            second.revision = 1;
            second.relationships[0].score = 42;
            store.Save(second);
            var backupBytes = File.ReadAllBytes(store.BackupPath);
            const string damaged = "interrupted external write";
            File.WriteAllText(store.SavePath, damaged);
            Assert.That(store.TryLoad(out var failed, out var message), Is.False);
            Assert.That(failed, Is.Null);
            Assert.That(message, Does.Contain("Recover backup"));
            Assert.Throws<InvalidDataException>(() => store.Save(second));
            Assert.That(File.ReadAllText(store.SavePath), Is.EqualTo(damaged));
            Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(backupBytes));
            Assert.That(store.TryRecoverBackup(out var restored, out message), Is.True, message);
            Assert.That(restored.revision, Is.EqualTo(first.revision));
            Assert.That(restored.relationships[0].score, Is.EqualTo(first.relationships[0].score));
            Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(backupBytes));
            var archives = Directory.GetFiles(directory, "*.before-recovery-*.json");
            Assert.That(archives, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(archives[0]), Is.EqualTo(damaged));
            Assert.That(store.TryLoad(out _, out message), Is.True, message);
        }

        [Test]
        public void InvalidCandidate_DoesNotChangeAnyGoodSave()
        {
            var valid = ContentCatalog.Create(31);
            store.Save(valid);
            var before = File.ReadAllBytes(store.SavePath);
            var invalid = valid.Clone();
            invalid.contestants[1].id = invalid.contestants[0].id;
            Assert.Throws<InvalidDataException>(() => store.Save(invalid));
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(before));
            Assert.That(File.Exists(store.BackupPath), Is.False);
        }

        [Test]
        public void ChangedPayload_FailsChecksumBeforeStateInstallation()
        {
            store.Save(ContentCatalog.Create(7));
            var document = JObject.Parse(File.ReadAllText(store.SavePath));
            document["state"]["week"] = 99;
            File.WriteAllText(store.SavePath, document.ToString());
            Assert.That(store.TryLoad(out var loaded, out var message), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(message, Does.Contain("checksum"));
        }

        [Test]
        public void FutureEnvelope_IsPreservedAndRejected()
        {
            store.Save(ContentCatalog.Create(7));
            var document = JObject.Parse(File.ReadAllText(store.SavePath));
            document["version"] = 2;
            File.WriteAllText(store.SavePath, document.ToString());
            var before = File.ReadAllBytes(store.SavePath);
            Assert.That(store.TryLoad(out var loaded, out var message), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(message, Does.Contain("unsupported"));
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(before));
        }

        [Test]
        public void MissingOrDamagedBackup_DoesNotReplaceThePrimary()
        {
            store.Save(ContentCatalog.Create(7));
            var before = File.ReadAllBytes(store.SavePath);
            Assert.That(store.TryRecoverBackup(out _, out _), Is.False);
            File.WriteAllText(store.BackupPath, "broken backup");
            Assert.That(store.TryRecoverBackup(out _, out _), Is.False);
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(before));
        }

        /// <summary>
        /// A virus scan, the search indexer or a sync client holding the save for a moment is not a
        /// failed save. The swap is tried again until the hold lifts, and the previous save still
        /// rotates into the backup as it would have without the lock.
        /// </summary>
        [Test]
        public void Save_WaitsOutABriefLockOnTheSave()
        {
            var first = ContentCatalog.Create(17);
            store.Save(first);
            var firstBytes = File.ReadAllBytes(store.SavePath);
            var second = first.Clone();
            second.revision = 1;
            using (var held = new HeldFile(store.SavePath))
            {
                held.ReleaseAfter(50);
                store.Save(second);
            }
            Assert.That(store.TryLoad(out var loaded, out var message), Is.True, message);
            Assert.That(loaded.revision, Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(firstBytes));
            Assert.That(Directory.GetFiles(directory, "*.pending-*"), Is.Empty);
        }

        /// <summary>
        /// A hold that does not lift ends in the IOException a save always threw, which the
        /// director's recovery path catches as an expected failure, and nothing on disk moves.
        /// The hold refuses the swap under Windows' sharing rules only (<see cref="HeldFile"/>).
        /// </summary>
        [Test]
        [UnityPlatform(RuntimePlatform.WindowsEditor)]
        public void Save_ALockThatDoesNotLiftThrowsTheIOExceptionAndChangesNothing()
        {
            var first = ContentCatalog.Create(17);
            store.Save(first);
            var before = File.ReadAllBytes(store.SavePath);
            var second = first.Clone();
            second.revision = 1;
            using (new HeldFile(store.SavePath))
            {
                Assert.Throws<IOException>(() => store.Save(second));
            }
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(before));
            Assert.That(File.Exists(store.BackupPath), Is.False, "Nothing rotated into the backup.");
            Assert.That(Directory.GetFiles(directory, "*.pending-*"), Is.Empty, "The pending copy is still cleaned up.");
        }

        /// <summary>
        /// A hold that shares nothing - a backup or sync client copying the save - stops the read
        /// that checks the current save before replacing it. That read used to take the locked
        /// file for an unreadable one and refuse the save as damaged; it now waits the hold out
        /// as the swap does.
        /// </summary>
        [Test]
        public void Save_WaitsOutABriefExclusiveHoldOnTheSave()
        {
            var first = ContentCatalog.Create(17);
            store.Save(first);
            var firstBytes = File.ReadAllBytes(store.SavePath);
            var second = first.Clone();
            second.revision = 1;
            using (var held = new HeldFile(store.SavePath, FileShare.None))
            {
                held.ReleaseAfter(50);
                store.Save(second);
            }
            Assert.That(store.TryLoad(out var loaded, out var message), Is.True, message);
            Assert.That(loaded.revision, Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(firstBytes));
            Assert.That(Directory.GetFiles(directory, "*.pending-*"), Is.Empty);
        }

        /// <summary>
        /// An exclusive hold that outlasts the wait still blocks the overwrite, but as a failed
        /// save - an IOException carrying the sharing violation - and not as the
        /// InvalidDataException that calls the save damaged and sends the player to backup
        /// recovery (<see cref="DamagedPrimary_BlocksOverwriteAndRequiresExplicitBackupRecovery"/>).
        /// Nothing on disk moves, and once the hold lifts the save is the good one it always was.
        /// </summary>
        [Test]
        public void Save_AnExclusiveHoldThatDoesNotLiftIsAFailedSaveNotADamagedOne()
        {
            var first = ContentCatalog.Create(17);
            store.Save(first);
            var before = File.ReadAllBytes(store.SavePath);
            var second = first.Clone();
            second.revision = 1;
            using (new HeldFile(store.SavePath, FileShare.None))
            {
                var error = Assert.Throws<IOException>(() => store.Save(second));
                Assert.That(error.InnerException, Is.TypeOf<IOException>(), "The read's own failure is kept.");
            }
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(before));
            Assert.That(File.Exists(store.BackupPath), Is.False, "Nothing rotated into the backup.");
            Assert.That(Directory.GetFiles(directory, "*.pending-*"), Is.Empty);
            Assert.That(store.TryLoad(out var loaded, out var message), Is.True, message);
            Assert.That(loaded.revision, Is.EqualTo(first.revision));
            store.Save(second);
            Assert.That(store.TryLoad(out loaded, out message), Is.True, message);
            Assert.That(loaded.revision, Is.EqualTo(1));
        }

        /// <summary>
        /// The check of the primary before it is overwritten was a whole load of it, a third of every
        /// save. A store that proved the primary's bytes itself - installed them, or loaded them -
        /// now compares the file with those instead, and anything that is not exactly them gets the
        /// whole check and today's outcome. These pin which saves compare, and that every other primary
        /// is still read in full: one byte changed, an older save put back, a hold, a failed swap.
        /// </summary>
        [Test]
        public void Save_AfterALoadComparesTheLoadedBytesInsteadOfParsingThem()
        {
            var first = ContentCatalog.Create(17);
            new EpisodeSaveStore(store.SavePath).Save(first);
            Assert.That(store.TryLoad(out var loaded, out var message), Is.True, message);
            var second = loaded.Clone();
            second.revision = 1;
            store.Save(second);
            Assert.That(PrimaryParsesSkipped(store), Is.EqualTo(1), "The first save after the load compared the bytes it loaded.");
            var third = second.Clone();
            third.revision = 2;
            store.Save(third);
            Assert.That(PrimaryParsesSkipped(store), Is.EqualTo(2), "The next compared the bytes the first installed.");

            var stranger = new EpisodeSaveStore(store.SavePath);
            var fourth = third.Clone();
            fourth.revision = 3;
            stranger.Save(fourth);
            Assert.That(PrimaryParsesSkipped(stranger), Is.EqualTo(0), "A store that has proved nothing reads the primary in full.");
            Assert.That(store.TryLoad(out loaded, out message), Is.True, message);
            Assert.That(loaded.revision, Is.EqualTo(3));
        }

        [Test]
        public void Save_APrimaryChangedByOneByteGetsTheWholeCheckAndIsRefused()
        {
            var first = ContentCatalog.Create(17);
            store.Save(first);
            var second = first.Clone();
            second.revision = 1;
            store.Save(second);
            Assert.That(PrimaryParsesSkipped(store), Is.EqualTo(1));
            var proved = File.ReadAllBytes(store.SavePath);
            var backup = File.ReadAllBytes(store.BackupPath);

            // Another writer changes one digit of the checksum: the file still parses, and only the
            // whole check tells it from the save this store wrote. The envelope ahead of the state is
            // ASCII, so a character's index is its byte's.
            const string field = "\"checksum\": \"";
            int digit = Encoding.UTF8.GetString(proved).IndexOf(field, StringComparison.Ordinal);
            Assert.That(digit, Is.GreaterThan(0));
            digit += field.Length;
            var changed = (byte[])proved.Clone();
            changed[digit] = changed[digit] == (byte)'0' ? (byte)'1' : (byte)'0';
            File.WriteAllBytes(store.SavePath, changed);
            var third = second.Clone();
            third.revision = 2;
            var error = Assert.Throws<InvalidDataException>(() => store.Save(third));
            Assert.That(error.Message, Is.EqualTo("The current save is unreadable. Choose backup recovery or another slot before saving; existing copies were preserved."));
            Assert.That(PrimaryParsesSkipped(store), Is.EqualTo(1), "The changed primary was read in full.");
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(changed));
            Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(backup));
            Assert.That(Directory.GetFiles(directory, "*.pending-*"), Is.Empty);

            // A refused primary leaves nothing proven: even the bytes this store wrote are read in
            // full when they come back.
            File.WriteAllBytes(store.SavePath, proved);
            store.Save(third);
            Assert.That(PrimaryParsesSkipped(store), Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(proved));
            Assert.That(store.TryLoad(out var loaded, out var message), Is.True, message);
            Assert.That(loaded.revision, Is.EqualTo(2));
        }

        [Test]
        public void Save_APrimaryReplacedByAnOlderValidSaveGetsTheWholeCheckAndProceeds()
        {
            var first = ContentCatalog.Create(17);
            store.Save(first);
            var older = File.ReadAllBytes(store.SavePath);
            var second = first.Clone();
            second.revision = 1;
            store.Save(second);

            // A sync client or a second copy of the game puts the earlier save back. This store wrote
            // those bytes once, but it remembers only the last it proved.
            File.WriteAllBytes(store.SavePath, older);
            var third = second.Clone();
            third.revision = 2;
            store.Save(third);
            Assert.That(PrimaryParsesSkipped(store), Is.EqualTo(1), "Only the second save compared; the older primary was read in full.");
            Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(older), "It rotated into the backup, as a good primary does.");
            Assert.That(store.TryLoad(out var loaded, out var message), Is.True, message);
            Assert.That(loaded.revision, Is.EqualTo(2));
        }

        /// <summary>
        /// The comparison reads the primary through the same waits as the whole check, so an
        /// exclusive hold that outlasts them is still the IOException of a failed save
        /// (<see cref="Save_AnExclusiveHoldThatDoesNotLiftIsAFailedSaveNotADamagedOne"/>), here on a
        /// primary the store had proved.
        /// </summary>
        [Test]
        public void Save_AnExclusiveHoldOnAProvenPrimaryIsStillAFailedSave()
        {
            var first = ContentCatalog.Create(17);
            store.Save(first);
            var second = first.Clone();
            second.revision = 1;
            store.Save(second);
            Assert.That(PrimaryParsesSkipped(store), Is.EqualTo(1), "The primary is one this store proved.");
            var before = File.ReadAllBytes(store.SavePath);
            var backup = File.ReadAllBytes(store.BackupPath);
            var third = second.Clone();
            third.revision = 2;
            using (new HeldFile(store.SavePath, FileShare.None))
            {
                var error = Assert.Throws<IOException>(() => store.Save(third));
                Assert.That(error.Message, Is.EqualTo("The current save could not be opened to check it before saving; it was left as it is."));
                Assert.That(error.InnerException, Is.TypeOf<IOException>(), "The read's own failure is kept.");
            }
            Assert.That(PrimaryParsesSkipped(store), Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(backup));
            Assert.That(Directory.GetFiles(directory, "*.pending-*"), Is.Empty);

            store.Save(third);
            Assert.That(PrimaryParsesSkipped(store), Is.EqualTo(1), "A read that failed leaves nothing proven, so the next save reads the primary in full.");
            Assert.That(store.TryLoad(out var loaded, out var message), Is.True, message);
            Assert.That(loaded.revision, Is.EqualTo(2));
        }

        /// <summary>
        /// A hold that lets reads through refuses only the swap (<see cref="HeldFile"/>), after the
        /// comparison passed. A swap that fails can have moved the primary aside, so it leaves nothing
        /// proven, and the next save reads the same bytes in full.
        /// </summary>
        [Test]
        [UnityPlatform(RuntimePlatform.WindowsEditor)]
        public void Save_AFailedSwapLeavesNothingProven()
        {
            var first = ContentCatalog.Create(17);
            store.Save(first);
            var before = File.ReadAllBytes(store.SavePath);
            var second = first.Clone();
            second.revision = 1;
            using (new HeldFile(store.SavePath))
            {
                Assert.Throws<IOException>(() => store.Save(second));
            }
            Assert.That(PrimaryParsesSkipped(store), Is.EqualTo(1), "The comparison passed before the swap failed.");
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(before));
            Assert.That(File.Exists(store.BackupPath), Is.False);

            store.Save(second);
            Assert.That(PrimaryParsesSkipped(store), Is.EqualTo(1), "The unchanged primary was read in full after the failed swap.");
            Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(before));
            Assert.That(store.TryLoad(out var loaded, out var message), Is.True, message);
            Assert.That(loaded.revision, Is.EqualTo(1));
        }

        [Test]
        public void Save_AfterABackupRecoveryComparesTheRestoredBytes()
        {
            var first = ContentCatalog.Create(17);
            store.Save(first);
            var second = first.Clone();
            second.revision = 1;
            store.Save(second);
            File.WriteAllText(store.SavePath, "interrupted external write");
            Assert.That(store.TryLoad(out _, out _), Is.False);
            Assert.That(store.TryRecoverBackup(out var restored, out var message), Is.True, message);
            int compared = PrimaryParsesSkipped(store);
            var next = restored.Clone();
            next.revision = 1;
            store.Save(next);
            Assert.That(PrimaryParsesSkipped(store), Is.EqualTo(compared + 1), "The restored bytes were proved as they were read back.");
            Assert.That(store.TryLoad(out var loaded, out message), Is.True, message);
            Assert.That(loaded.revision, Is.EqualTo(1));
        }

        /// <summary>The store's own count of saves that compared the primary with bytes it had proved
        /// instead of parsing it. It is private: read it the way the director's fields are read.</summary>
        private static int PrimaryParsesSkipped(EpisodeSaveStore of)
        {
            var field = typeof(EpisodeSaveStore).GetField("primaryParsesSkipped", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "The production store's own count.");
            return (int)field.GetValue(of);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void SupportedWebEnvelope_PreservesIdentityDirectedScoresAndOriginal(int version)
        {
            var document = WebFixture();
            if (version > 0) document["version"] = version;
            var json = "\n" + (version == 0 ? document["state"] : document).ToString(Formatting.Indented) + "\n";
            var archive = Path.Combine(directory, "imports");
            Assert.That(WebSaveImporter.TryImport(json, archive, out var imported, out var message), Is.True, message);
            Assert.That(imported.playerId, Is.EqualTo("external-player"));
            Assert.That(imported.contestants[1].id, Is.EqualTo("external-maya"));
            Assert.That(imported.Score("external-player", "external-maya"), Is.EqualTo(25));
            Assert.That(imported.Score("external-maya", "external-player"), Is.EqualTo(-12));
            Assert.That(imported.contestants[0].stats.social, Is.EqualTo(6));
            Assert.That(imported.contestants[0].hohWins, Is.EqualTo(1));
            Assert.That(imported.phase, Is.EqualTo(EpisodePhase.Social));
            // Wave D's leak rules (D4), war rooms (D3) and the house's turns all week (D2) from the week after
            // the import, as the commitment rules; the all-week rules beside the week rules they play in.
            Assert.That(imported.allianceLeakRulesStartWeek, Is.EqualTo(imported.week + 1));
            Assert.That(imported.pactPlanRulesStartWeek, Is.EqualTo(imported.week + 1));
            Assert.That(imported.commitmentRulesStartWeek, Is.EqualTo(imported.week + 1));
            Assert.That(imported.allWeekRulesStartWeek, Is.EqualTo(imported.week + 1));
            Assert.That(imported.weekRulesStartWeek, Is.EqualTo(imported.week + 1));
            Assert.That(EpisodeEngine.AllWeekOn(imported), Is.False, "The import week plays the weekly pass.");
            // Never the unified rules: the importer selects no canonical authority, though a fresh season is mode 2 (vote family V6).
            Assert.That((imported.unifiedCommitmentRulesVersion, imported.unifiedHearingRulesVersion, imported.unifiedVoteReveals.Count),
                Is.EqualTo((0, 0, 0)), "The import is mode 0.");
            // Story rules 9 whatever a fresh season gets, as the schema-16 step pins it: a later milestone's
            // enrolment (Roles, ROLE-MOMENTS-PLAN decision 14) never reaches an import.
            Assert.That((imported.story.rulesStartWeek, imported.story.rulesVersion), Is.EqualTo((imported.week + 1, StoryRules.Threads)),
                "The importer's own literal, never a new season's StoryRules.Current.");
            var originals = Directory.GetFiles(archive);
            Assert.That(originals, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(originals[0]), Is.EqualTo(json));
            Assert.That(message, Does.Contain("defaults").And.Contain("Original preserved"));
            Assert.That(WebSaveImporter.TryImport(json, archive, out var repeated, out _), Is.True);
            Assert.That(repeated.randomState, Is.EqualTo(imported.randomState));
            Assert.That(Directory.GetFiles(archive), Has.Length.EqualTo(1));
        }

        [TestCase("receipt")]
        [TestCase("phase")]
        [TestCase("cast")]
        [TestCase("duplicate-id")]
        [TestCase("unknown-field")]
        [TestCase("storyline")]
        [TestCase("malformed-flag")]
        [TestCase("future")]
        public void UnsupportedWebGameplay_IsRejectedAndOriginalArchived(string problem)
        {
            var document = WebFixture();
            var source = (JObject)document["state"];
            switch (problem)
            {
                case "receipt": source["nominationResolution"] = new JObject(); break;
                case "phase": source["phase"] = "Eviction"; break;
                case "cast": ((JArray)source["houseguests"]).Add(new JObject { ["id"] = "seventh", ["name"] = "Seventh" }); break;
                case "duplicate-id": source["houseguests"][1]["id"] = "external-player"; break;
                case "unknown-field": source["unmodeledGameplay"] = 12; break;
                case "storyline": source["activeStorylines"] = new JArray(new JObject { ["id"] = "unfinished-story" }); break;
                case "malformed-flag": source["isFinalStage"] = ""; break;
                case "future": document["version"] = 100; break;
            }

            var json = document.ToString();
            var archive = Path.Combine(directory, "imports");
            Assert.That(WebSaveImporter.TryImport(json, archive, out var imported, out var message), Is.False);
            Assert.That(imported, Is.Null);
            Assert.That(message, Does.Contain("not installed").And.Contain("Original preserved"));
            Assert.That(File.ReadAllText(Directory.GetFiles(archive).Single()), Is.EqualTo(json));
            Assert.That(File.Exists(store.SavePath), Is.False);
        }

        [Test]
        public void DuplicateJsonProperties_AreRejectedWithoutGuessing()
        {
            const string json = "{\"week\":1,\"week\":2}";
            var archive = Path.Combine(directory, "imports");
            Assert.That(WebSaveImporter.TryImport(json, archive, out var imported, out _), Is.False);
            Assert.That(imported, Is.Null);
            Assert.That(File.ReadAllText(Directory.GetFiles(archive).Single()), Is.EqualTo(json));
        }

        [Test]
        public void ArchiveFailure_PreventsImportInstallation()
        {
            var blockedDirectory = Path.Combine(directory, "file-not-directory");
            File.WriteAllText(blockedDirectory, "preserve this file");
            Assert.That(WebSaveImporter.TryImport(WebFixture().ToString(), blockedDirectory, out var imported, out _), Is.False);
            Assert.That(imported, Is.Null);
            Assert.That(File.ReadAllText(blockedDirectory), Is.EqualTo("preserve this file"));
        }

        /// <summary>
        /// Another process's handle on a save file. By default it is held the way a virus scan or
        /// the search indexer holds one: open for reading, sharing reads but not deletion. The
        /// stores' own reads before a write go through, as they do in the field, and only the swap
        /// that replaces the file meets the lock. <see cref="FileShare.None"/> holds it the way a
        /// backup or sync client copying it can, sharing nothing, and then those reads meet the
        /// lock first.
        ///
        /// <para>Those are Windows' sharing rules. Mono on Linux and macOS enforces a share mode
        /// itself, and only when the file is opened: a FileShare.None hold refuses reads there too,
        /// but a FileShare.Delete hold lets them through, and File.Replace is a rename that no hold
        /// stops. A test that needs either runs in the Windows editor only.</para>
        /// </summary>
        internal sealed class HeldFile : IDisposable
        {
            private readonly FileStream stream;
            private Thread releaser;

            public HeldFile(string path, FileShare share = FileShare.Read)
            {
                stream = new FileStream(path, FileMode.Open, FileAccess.Read, share);
            }

            /// <summary>Lets go from another thread after the given time, while the write is waiting.</summary>
            public void ReleaseAfter(int milliseconds)
            {
                releaser = new Thread(() =>
                {
                    Thread.Sleep(milliseconds);
                    stream.Dispose();
                }) { IsBackground = true };
                releaser.Start();
            }

            public void Dispose()
            {
                releaser?.Join();
                stream.Dispose();
            }
        }

        private static JObject WebFixture()
        {
            var ids = new[] { "external-player", "external-maya", "external-taylor", "external-jamie", "external-casey", "external-riley" };
            var cast = ContentCatalog.Create(13).contestants;
            var guests = new JArray(cast.Select((actor, index) => new JObject
            {
                ["id"] = ids[index], ["name"] = actor.name, ["isPlayer"] = index == 0, ["status"] = "Active",
                ["stats"] = JObject.FromObject(actor.stats), ["traits"] = new JArray(actor.traits),
                ["competitionsWon"] = new JObject { ["hoh"] = index == 0 ? 1 : 0, ["pov"] = 0, ["other"] = 0 },
                ["nominations"] = new JObject { ["times"] = 0, ["receivedOn"] = new JArray() }
            }));
            return new JObject
            {
                ["format"] = "gamesim-save", ["version"] = 1, ["savedAt"] = "2026-09-10T00:00:00.000Z",
                ["state"] = new JObject
                {
                    ["phase"] = "SocialInteraction", ["week"] = 1, ["houseguests"] = guests,
                    ["relationships"] = new JObject
                    {
                        [ids[0]] = new JObject { [ids[1]] = new JObject { ["score"] = 25 } },
                        [ids[1]] = new JObject { [ids[0]] = new JObject { ["score"] = -12 } }
                    }
                }
            };
        }
    }
}
