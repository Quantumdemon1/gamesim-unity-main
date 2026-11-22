using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The settings and saves leftovers, on the list of saved seasons (SeasonShelf): a save another
    /// program holds for a moment is listed as in use, not as a save that cannot be opened, and is a
    /// good season again once let go; files an interrupted delete left set aside are removed as the
    /// list looks, and every save beside them - its bytes and its time - is as it was. EditMode only:
    /// the reader is the save store's own. Every case works in a folder of its own under the system's
    /// temporary folder.
    /// </summary>
    public sealed class MenuSavesLeftoversShelfTests
    {
        private const string Prefix = "GamesimMenuSavesLeftoversShelf-";
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

        private string Current(uint seed, string name = null)
        {
            string path = Path.Combine(folder, name ?? "episode-" + Guid.NewGuid().ToString("N") + ".json");
            new EpisodeSaveStore(path).Save(ContentCatalog.Create(seed));
            return Path.GetFullPath(path);
        }

        [Test]
        public void TheList_SaysInUse_ForASaveAnotherProgramHolds_NotThatItCannotBeOpened()
        {
            string save = Current(5);
            string garbage = Path.Combine(folder, "import-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(garbage, "not a save at all");
            var shelf = new SeasonShelf();
            using (new FileStream(save, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var held = shelf.Read(folder).Single(season => season.Path == save);
                Assert.That(held.Readable, Is.False, "Held, it cannot be read now,");
                Assert.That(held.InUse, Is.True, "and the list says it is in use.");
            }
            var listed = shelf.Read(folder);
            var free = listed.Single(season => season.Path == save);
            Assert.That(free.Readable, Is.True, "Let go, it is a good season,");
            Assert.That(free.InUse, Is.False, "no longer in use.");
            var broken = listed.Single(season => season.Path == Path.GetFullPath(garbage));
            Assert.That(broken.Readable, Is.False);
            Assert.That(broken.InUse, Is.False, "A file that is no save is not in use: it cannot be opened.");
        }

        [Test]
        public void TheList_RemovesWhatAnInterruptedDeleteLeft_AndEverySaveBesideItIsAsItWas()
        {
            string first = Current(1, "episode.json");
            string second = Current(2);
            new EpisodeSaveStore(second).Save(ContentCatalog.Create(3));
            string backup = second + ".backup";
            Assert.That(File.Exists(backup), Is.True, "The second season has a backup.");
            string gone = "episode-" + Guid.NewGuid().ToString("N") + ".json", token = ".deleting-" + Guid.NewGuid().ToString("N");
            var leftovers = new[] { gone + token, gone + ".backup" + token }.Select(name => Path.Combine(folder, name)).ToArray();
            foreach (var leftover in leftovers) File.Copy(second, leftover);
            // An old leftover of the first season's name, which a season uses again: it stays beside the save.
            string besideFirst = Path.Combine(folder, "episode.json" + token);
            File.Copy(first, besideFirst);
            var stamp = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
            foreach (var file in Directory.GetFiles(folder)) File.SetLastWriteTimeUtc(file, stamp);
            var keep = new[] { first, second, backup, besideFirst };
            var before = keep.ToDictionary(file => file, file => File.ReadAllBytes(file));

            var listed = new SeasonShelf().Read(folder);

            Assert.That(leftovers.Any(File.Exists), Is.False, "The leftovers of a season with no save are gone,");
            Assert.That(Directory.GetFiles(folder).Select(Path.GetFullPath), Is.EquivalentTo(keep), "and nothing else went.");
            foreach (var file in keep)
            {
                Assert.That(File.ReadAllBytes(file), Is.EqualTo(before[file]), Path.GetFileName(file) + ": not a byte changed,");
                Assert.That(File.GetLastWriteTimeUtc(file), Is.EqualTo(stamp), Path.GetFileName(file) + ": nor its time.");
            }
            Assert.That(listed.Select(season => season.Path), Is.EquivalentTo(new[] { first, second }), "The list is the two seasons,");
            Assert.That(listed.All(season => season.Readable), Is.True, "both good.");
        }
    }
}
