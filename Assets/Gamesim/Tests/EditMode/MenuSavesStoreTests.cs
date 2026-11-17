using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Menu slice 4, part a: the save store's one delete and the Saves section's words, in EditMode and
    /// the Unity-free suite alike. Every case works in a folder of its own under the system's
    /// temporary folder, made in SetUp and checked to be there before TearDown removes it; nothing
    /// here names, lists or deletes in the game's own save folder.
    /// </summary>
    public sealed class MenuSavesStoreTests
    {
        private const string Prefix = "GamesimMenuSavesStore-";
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

        private static string Hex() => Guid.NewGuid().ToString("N");

        /// <summary>A file in <paramref name="where"/> with content of its own, so each can be told from another byte for byte.</summary>
        private static string Put(string where, string name)
        {
            Directory.CreateDirectory(where);
            string path = Path.Combine(where, name);
            File.WriteAllText(path, "{\"file\":\"" + name + "\"}");
            return path;
        }

        /// <summary>Every file under the folder with its bytes, so a case can say nothing else changed.</summary>
        private Dictionary<string, byte[]> Everything() =>
            Directory.GetFiles(folder, "*", SearchOption.AllDirectories).ToDictionary(file => file, file => File.ReadAllBytes(file), StringComparer.OrdinalIgnoreCase);

        private static void AssertUnchanged(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after, string why)
        {
            Assert.That(after.Keys, Is.EquivalentTo(before.Keys), why + ": no file came or went.");
            foreach (var file in before.Keys) Assert.That(after[file], Is.EqualTo(before[file]), why + ": " + Path.GetFileName(file) + " is as it was.");
        }

        // ---------------------------------------------------------------- which files are seasons

        [Test]
        public void SeasonFiles_AreTheNamesTheGameGivesASave()
        {
            string hex = Hex();
            foreach (var name in new[] { "episode.json", "episode-" + hex + ".json", "import-" + hex + ".json", "EPISODE-" + hex.ToUpperInvariant() + ".JSON" })
                Assert.That(EpisodeSaveStore.IsSeasonFileName(name), Is.True, name + " is a season's save.");
            foreach (var name in new[] { "career.json", "career.json.reset-2026-11-17", "preferences.json", "episode.json.backup",
                         "episode.json.pending-" + hex, "episode.json.before-recovery-" + hex + ".json", "episode-1234.json", "import.json",
                         "trace.json", "episode.json.deleting-" + hex, "../episode.json", "Houseguests/episode.json", "", null })
                Assert.That(EpisodeSaveStore.IsSeasonFileName(name), Is.False, (name ?? "null") + " is no season's save.");
        }

        [Test]
        public void SeasonFiles_ListsTheSeasonsAndNothingElse()
        {
            string a = Put(folder, "episode.json"), b = Put(folder, "episode-" + Hex() + ".json"), c = Put(folder, "import-" + Hex() + ".json");
            Put(folder, "episode.json.backup"); Put(folder, "career.json"); Put(folder, "preferences.json");
            Put(folder, "episode.json.before-recovery-" + Hex() + ".json");
            Put(Path.Combine(folder, "Houseguests"), "episode-" + Hex() + ".json");
            Put(Path.Combine(folder, "Imports"), "import-" + Hex() + ".json");
            Assert.That(EpisodeSaveStore.SeasonFiles(folder), Is.EquivalentTo(new[] { a, b, c }.Select(Path.GetFullPath)),
                "The three saves directly in the folder: no backup, kept copy, career, preferences or anything in a folder below.");
            Assert.That(EpisodeSaveStore.SeasonFiles(Path.Combine(folder, "not-there")), Is.Empty, "A folder that is not there has none.");
        }

        // ---------------------------------------------------------------- delete

        [Test]
        public void Delete_RemovesExactlyTheSeasonsOwnFiles()
        {
            string name = "episode-" + Hex() + ".json";
            string save = Put(folder, name);
            string[] beside = { Put(folder, name + ".backup"), Put(folder, name + ".pending-" + Hex()), Put(folder, name + ".before-recovery-" + Hex() + ".json") };
            string other = "episode-" + Hex() + ".json";
            Put(folder, other); Put(folder, other + ".backup");
            string playing = Put(folder, "episode.json");
            Put(folder, "episode.json.backup"); Put(folder, "career.json"); Put(folder, "career.json.reset-2026-11-17"); Put(folder, "preferences.json");
            Put(Path.Combine(folder, "Houseguests"), "someone.json"); Put(Path.Combine(folder, "Imports"), "web.json");
            var before = Everything();

            Assert.That(EpisodeSaveStore.DeleteSeason(folder, save, playing), Is.EqualTo(SeasonDeleteResult.Deleted));

            var after = Everything();
            var gone = new[] { save }.Concat(beside).Select(Path.GetFullPath).ToArray();
            foreach (var file in gone) Assert.That(File.Exists(file), Is.False, Path.GetFileName(file) + " went with its season.");
            AssertUnchanged(before.Where(pair => !gone.Contains(Path.GetFullPath(pair.Key), StringComparer.OrdinalIgnoreCase)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
                after, "Every other season, the career record, the preferences, the library and the imports");
            Assert.That(Directory.GetFiles(folder).Any(file => file.Contains(".deleting-")), Is.False, "Nothing is left set aside.");
        }

        [Test]
        public void Delete_RefusesAPathOutsideTheSaveFolder()
        {
            string saves = Path.Combine(folder, "saves");
            string name = "episode-" + Hex() + ".json";
            Put(saves, "episode.json");
            string above = Put(folder, name);
            string below = Put(Path.Combine(saves, "Houseguests"), name);
            var before = Everything();
            Assert.That(EpisodeSaveStore.DeleteSeason(saves, above, null), Is.EqualTo(SeasonDeleteResult.NotInFolder), "A season above the folder.");
            Assert.That(EpisodeSaveStore.DeleteSeason(saves, Path.Combine(saves, "..", name), null), Is.EqualTo(SeasonDeleteResult.NotInFolder),
                "A path that climbs out, resolved first.");
            Assert.That(EpisodeSaveStore.DeleteSeason(saves, below, null), Is.EqualTo(SeasonDeleteResult.NotInFolder), "A season in a folder below.");
            Assert.That(EpisodeSaveStore.DeleteSeason(saves, Path.Combine(saves, "Houseguests", "..", "..", name), null), Is.EqualTo(SeasonDeleteResult.NotInFolder));
            AssertUnchanged(before, Everything(), "A refused delete");
        }

        [Test]
        public void Delete_RefusesAFileThatIsNoSeason()
        {
            Put(folder, "episode.json");
            var before = Everything();
            foreach (var name in new[] { "career.json", "preferences.json", "episode.json.backup" })
            {
                Put(folder, name);
                before = Everything();
                Assert.That(EpisodeSaveStore.DeleteSeason(folder, Path.Combine(folder, name), null), Is.EqualTo(SeasonDeleteResult.NotASeason), name);
                AssertUnchanged(before, Everything(), name);
            }
        }

        [Test]
        public void Delete_RefusesTheSeasonBeingPlayed()
        {
            string playing = Put(folder, "episode-" + Hex() + ".json");
            Put(folder, Path.GetFileName(playing) + ".backup");
            var before = Everything();
            Assert.That(EpisodeSaveStore.DeleteSeason(folder, playing, playing), Is.EqualTo(SeasonDeleteResult.Playing));
            Assert.That(EpisodeSaveStore.DeleteSeason(folder, playing.ToUpperInvariant(), Path.Combine(folder, ".", Path.GetFileName(playing))),
                Is.EqualTo(SeasonDeleteResult.Playing), "The same file, however its path is spelled.");
            AssertUnchanged(before, Everything(), "The season being played");
        }

        [Test]
        public void Delete_RefusesALink_AndDeletesNothing()
        {
            string name = "episode-" + Hex() + ".json";
            string save = Put(folder, name);
            string backup = Put(folder, name + ".backup");
            Assert.That(EpisodeSaveStore.IsLink(save), Is.False, "An ordinary file is no link,");
            Assert.That(EpisodeSaveStore.IsLink(folder), Is.False, "nor an ordinary folder.");
            var before = Everything();
            var seen = new List<string>();
            Assert.That(EpisodeSaveStore.DeleteSeason(folder, save, null, path => { seen.Add(Path.GetFullPath(path)); return string.Equals(Path.GetFullPath(path), Path.GetFullPath(backup), StringComparison.OrdinalIgnoreCase); }),
                Is.EqualTo(SeasonDeleteResult.Link), "One of the season's files is a link: nothing is deleted.");
            Assert.That(seen, Does.Contain(Path.GetFullPath(backup)), "Every file of the season is asked about.");
            AssertUnchanged(before, Everything(), "A season with a link among its files");
            string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);
            Assert.That(EpisodeSaveStore.DeleteSeason(folder, save, null, path => string.Equals(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar), root, StringComparison.OrdinalIgnoreCase)),
                Is.EqualTo(SeasonDeleteResult.Link), "The save folder itself is a link: nothing is deleted.");
            AssertUnchanged(before, Everything(), "A folder that is a link");
        }

        [Test]
        public void Delete_AFileHeldByAnotherProgramDeletesNothing()
        {
            string name = "episode-" + Hex() + ".json";
            string save = Put(folder, name);
            string backup = Put(folder, name + ".backup");
            string kept = Put(folder, name + ".before-recovery-" + Hex() + ".json");
            var before = Everything();
            foreach (var held in new[] { kept, backup, save })
            {
                // Another program reading it, as a backup or sync client does: a share the delete cannot pass.
                using (new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.Read))
                    Assert.That(EpisodeSaveStore.DeleteSeason(folder, save, null), Is.EqualTo(SeasonDeleteResult.InUse), Path.GetFileName(held) + " is held.");
                AssertUnchanged(before, Everything(), "With " + Path.GetFileName(held) + " held");
                Assert.That(Directory.GetFiles(folder).Any(file => file.Contains(".deleting-")), Is.False, "Nothing is left set aside.");
            }
            Assert.That(EpisodeSaveStore.DeleteSeason(folder, save, null), Is.EqualTo(SeasonDeleteResult.Deleted), "Once let go, it deletes.");
            Assert.That(new[] { save, backup, kept }.Any(File.Exists), Is.False);
        }

        [Test]
        public void Delete_ASaveThatIsNotThereIsMissing()
        {
            Assert.That(EpisodeSaveStore.DeleteSeason(folder, Path.Combine(folder, "episode-" + Hex() + ".json"), null), Is.EqualTo(SeasonDeleteResult.Missing));
            Assert.That(EpisodeSaveStore.DeleteSeason(Path.Combine(folder, "gone"), Path.Combine(folder, "gone", "episode.json"), null), Is.EqualTo(SeasonDeleteResult.Missing));
        }

        // ---------------------------------------------------------------- the words

        [Test]
        public void Words_SayWhenASeasonWasSavedInPlainWords()
        {
            var now = new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
            Assert.That(SavesWords.SavedAgo(now.AddSeconds(-20), now), Is.EqualTo("saved just now"));
            Assert.That(SavesWords.SavedAgo(now.AddMinutes(5), now), Is.EqualTo("saved just now"), "A clock put back is never 'in 5 minutes'.");
            Assert.That(SavesWords.SavedAgo(now.AddMinutes(-1), now), Is.EqualTo("saved 1 minute ago"));
            Assert.That(SavesWords.SavedAgo(now.AddMinutes(-42), now), Is.EqualTo("saved 42 minutes ago"));
            Assert.That(SavesWords.SavedAgo(now.AddHours(-2), now), Is.EqualTo("saved 2 hours ago"));
            Assert.That(SavesWords.SavedAgo(now.AddHours(-1).AddMinutes(-10), now), Is.EqualTo("saved 1 hour ago"));
            Assert.That(SavesWords.SavedAgo(now.AddHours(-30), now), Is.EqualTo("saved yesterday"));
            Assert.That(SavesWords.SavedAgo(now.AddDays(-3), now), Is.EqualTo("saved 3 days ago"));
            Assert.That(SavesWords.SavedAgo(now.AddDays(-45), now), Does.StartWith("saved on ").And.EndWith("2026"));
        }

        [Test]
        public void Words_NameTheSeasonOnItsCardAndInItsQuestions()
        {
            Assert.That(SavesWords.Details(4, 7), Is.EqualTo("Week 4  ·  7 houseguests left"));
            Assert.That(SavesWords.Left(1), Is.EqualTo("1 houseguest left"));
            Assert.That(SavesWords.LoadQuestion("Ava", 4), Is.EqualTo("Load Ava, week 4? Your current season is saved first."));
            Assert.That(SavesWords.DeleteQuestion("Ava", 4), Is.EqualTo("Delete Ava, week 4? This cannot be undone."));
            Assert.That(SavesWords.LoadQuestion(null, 0), Is.EqualTo("Load your houseguest, week 1? Your current season is saved first."),
                "A save that names nobody still asks in whole words.");
            Assert.That(SavesWords.ControlName(SavesWords.LoadCaption, "episode-1a"), Is.EqualTo("Load: episode-1a"));
            Assert.That(SavesWords.InUse, Is.EqualTo("That save is in use. Nothing was deleted."));
            Assert.That(SavesWords.SaveNowCaption, Is.EqualTo("Save now  [F5]"), "The caption the pad's chip map and its tests key on, byte for byte.");
            foreach (var caption in new[] { SavesWords.GoBackCaption, SavesWords.GoBackResult, SavesWords.RestoreResult, SavesWords.SaveAndLoad, SavesWords.DeleteThisSeason })
            {
                Assert.That(caption, Does.Not.StartWith("Back to"), caption + ": no way-back wording,");
                Assert.That(caption, Does.Not.Contain("slot"), caption + ": and 'slot' names a save only.");
            }
        }
    }
}
