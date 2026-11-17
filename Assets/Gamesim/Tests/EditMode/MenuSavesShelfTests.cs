using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Menu slice 4, part a: the list of saved seasons (SeasonShelf) reads every season's save as a
    /// load would and writes nothing - not a byte, not a file time, not a migration - with an old
    /// version and a file that cannot be opened among them. EditMode only: the reader is the save
    /// store's own. Every case works in a folder of its own under the system's temporary folder;
    /// nothing here lists the game's own save folder.
    /// </summary>
    public sealed class MenuSavesShelfTests
    {
        private const string Prefix = "GamesimMenuSavesShelf-";
        private string folder;

        [SetUp]
        public void MakeAFolderOfItsOwn()
        {
            folder = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
            Assert.That(Directory.Exists(folder), Is.False);
            Directory.CreateDirectory(folder);
        }

        [TearDown]
        public void RemoveOnlyThatFolder()
        {
            if (string.IsNullOrEmpty(folder)) return;
            var resolved = Path.GetFullPath(folder);
            var root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(resolved), Does.StartWith(Prefix));
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }

        private string Named(string stem) => Path.Combine(folder, stem + "-" + Guid.NewGuid().ToString("N") + ".json");

        /// <summary>A season saved by the store, as the game saves one.</summary>
        private string Current(uint seed, string path = null)
        {
            path = path ?? Named("episode");
            new EpisodeSaveStore(path).Save(ContentCatalog.Create(seed));
            return path;
        }

        /// <summary>Schema 1's pinned shape in a good envelope: the store brings it up to date in memory.</summary>
        private string Old()
        {
            string path = Named("episode");
            File.WriteAllText(path, PersistenceMigrationTests.Envelope(PersistenceMigrationTests.V1Fixture()), new UTF8Encoding(false));
            return path;
        }

        private sealed class Seen
        {
            public byte[] Bytes; public DateTime Written, Created;
        }

        private Dictionary<string, Seen> Everything() => Directory.GetFiles(folder, "*", SearchOption.AllDirectories).ToDictionary(file => file,
            file => new Seen { Bytes = File.ReadAllBytes(file), Written = File.GetLastWriteTimeUtc(file), Created = File.GetCreationTimeUtc(file) },
            StringComparer.OrdinalIgnoreCase);

        [Test]
        public void TheList_ReadsEverySeason_OldAndUnreadableToo_AndWritesNothing()
        {
            string current = Current(7, Path.Combine(folder, "episode.json"));
            string old = Old();
            string garbage = Named("import");
            File.WriteAllText(garbage, "not a save at all");
            string damaged = Named("episode");
            var envelope = JObject.Parse(PersistenceMigrationTests.Envelope(PersistenceMigrationTests.V1Fixture()));
            envelope["state"]["week"] = 9;
            File.WriteAllText(damaged, envelope.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder, "career.json"), "{}");
            // Times well in the past, so a write by the list could not hide inside the same tick.
            var stamp = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
            foreach (var file in Directory.GetFiles(folder)) { File.SetLastWriteTimeUtc(file, stamp); File.SetCreationTimeUtc(file, stamp); }
            var before = Everything();

            var shelf = new SeasonShelf();
            var listed = shelf.Read(folder);
            shelf.Read(folder);
            new SeasonShelf().Read(folder);

            var after = Everything();
            Assert.That(after.Keys, Is.EquivalentTo(before.Keys), "Listing makes no file and removes none.");
            foreach (var file in before.Keys)
            {
                Assert.That(after[file].Bytes, Is.EqualTo(before[file].Bytes), Path.GetFileName(file) + ": not a byte changed (no migration on disk).");
                Assert.That(after[file].Written, Is.EqualTo(before[file].Written), Path.GetFileName(file) + ": its time is as it was.");
                Assert.That(after[file].Created, Is.EqualTo(before[file].Created), Path.GetFileName(file) + ": its creation time too.");
            }

            Assert.That(listed.Select(season => season.Path), Is.EquivalentTo(new[] { current, old, garbage, damaged }.Select(Path.GetFullPath)),
                "Every season's save and nothing else (the career record is no season).");
            Assert.That(listed.Single(season => season.Path == Path.GetFullPath(current)).Readable, Is.True);
            var oldCard = listed.Single(season => season.Path == Path.GetFullPath(old));
            Assert.That(new EpisodeSaveStore(old).TryLoad(out var loaded, out var message), Is.True, message);
            Assert.That(oldCard.Readable, Is.True, "An old version is read as far as it can be read: here, wholly.");
            Assert.That(oldCard.PlayerName, Is.EqualTo(loaded.Find(loaded.playerId).name));
            Assert.That(oldCard.Week, Is.EqualTo(loaded.week));
            Assert.That(oldCard.Left, Is.EqualTo(loaded.contestants.Count(actor => actor.status == ContestantStatus.Active)));
            Assert.That(oldCard.SavedUtc, Is.EqualTo(stamp), "When it was saved is the file's own time.");
            Assert.That(listed.Single(season => season.Path == Path.GetFullPath(garbage)).Readable, Is.False, "A file that is no save cannot be opened.");
            Assert.That(listed.Single(season => season.Path == Path.GetFullPath(damaged)).Readable, Is.False,
                "Nor can a save whose checksum fails: the list asks what a load asks.");
        }

        [Test]
        public void TheList_IsNewestFirst()
        {
            string a = Current(1), b = Current(2), c = Old();
            File.SetLastWriteTimeUtc(a, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(b, new DateTime(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(c, new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc));
            Assert.That(new SeasonShelf().Read(folder).Select(season => season.Path), Is.EqualTo(new[] { b, c, a }.Select(Path.GetFullPath)));
        }

        [Test]
        public void TheList_ReadsASaveAgainOnlyWhenItChanged()
        {
            string a = Current(1), b = Current(2);
            var shelf = new SeasonShelf();
            Assert.That(shelf.Read(folder), Has.Count.EqualTo(2));
            Assert.That(shelf.LastOpened, Is.EqualTo(2), "The first look opens each save.");
            shelf.Read(folder);
            Assert.That(shelf.LastOpened, Is.Zero, "A look at an unchanged folder opens none.");
            new EpisodeSaveStore(a).Save(ContentCatalog.Create(3));
            File.SetLastWriteTimeUtc(a, DateTime.UtcNow.AddMinutes(1));
            var again = shelf.Read(folder);
            Assert.That(shelf.LastOpened, Is.EqualTo(1), "A save written since is read again; the other is not.");
            File.Delete(b);
            Assert.That(shelf.Read(folder).Select(season => season.Path), Is.EqualTo(new[] { Path.GetFullPath(a) }), "A save gone is gone from the list.");
            Assert.That(again.Any(season => season.Path == Path.GetFullPath(a) && season.Readable), Is.True);
        }

        [Test]
        public void TheList_LooksAgainAtASaveAnotherProgramHeld()
        {
            string a = Current(5);
            var shelf = new SeasonShelf();
            using (new FileStream(a, FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.That(shelf.Read(folder).Single().Readable, Is.False, "Held, it cannot be opened now,");
            Assert.That(shelf.Read(folder).Single().Readable, Is.True, "and once let go it is read, not remembered as unreadable.");
        }
    }
}
