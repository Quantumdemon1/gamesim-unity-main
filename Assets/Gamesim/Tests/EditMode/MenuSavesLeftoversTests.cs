using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Persistence;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The settings and saves leftovers (the Settings slice's "left for later"): files an interrupted
    /// delete left set aside are removed the next time the list looks, by the store's one remover, and
    /// never a save; a file another program holds for a moment is told from one that cannot be opened;
    /// Recover backup is greyed with its reason where there is no backup; Escape and B on a confirm
    /// card take its safe answer; the game menu's PAUSED is drawn at a readable size at every text size.
    ///
    /// <para>BCL only (the store's delete file, the words, the text sizes and the sources), so the
    /// Unity-free suite runs it as well; the list's own half is MenuSavesLeftoversShelfTests.</para>
    /// </summary>
    public sealed class MenuSavesLeftoversTests
    {
        private const string Prefix = "GamesimMenuSavesLeftovers-";
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

        private string Put(string name)
        {
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, "{\"file\":\"" + name + "\"}");
            return path;
        }

        private Dictionary<string, byte[]> Everything() =>
            Directory.GetFiles(folder).ToDictionary(file => Path.GetFileName(file), file => File.ReadAllBytes(file), StringComparer.OrdinalIgnoreCase);

        // ---------------------------------------------------------------- an interrupted delete's leftovers

        [Test]
        public void Sweep_RemovesWhatAnInterruptedDeleteLeft_AndOnlyThat()
        {
            string gone = "episode-" + Hex() + ".json", token = ".deleting-" + Hex();
            var leftovers = new[]
            {
                gone + token, gone + ".backup" + token, gone + ".pending-" + Hex() + token, gone + ".before-recovery-" + Hex() + ".json" + token,
                "episode.json" + token,
            };
            foreach (var name in leftovers) Put(name);
            string kept = "import-" + Hex() + ".json";
            var stays = new[]
            {
                kept, kept + ".backup", "career.json", "preferences.json", "notes.txt" + token, "episode-" + Hex() + ".json.deleting-notatoken",
                gone + ".backup", "episode-" + Hex() + ".json.deleting-" + Hex() + ".extra",
            };
            foreach (var name in stays) Put(name);
            var before = Everything();

            Assert.That(EpisodeSaveStore.SweepInterruptedDeletes(folder), Is.EqualTo(leftovers.Length), "Each file a delete set aside, for a season with no save.");
            var after = Everything();
            Assert.That(after.Keys, Is.EquivalentTo(stays), "Nothing else: no save, backup, career record, nor a file merely named alike.");
            foreach (var name in stays) Assert.That(after[name], Is.EqualTo(before[name]), name + " is as it was.");
            Assert.That(EpisodeSaveStore.SweepInterruptedDeletes(folder), Is.Zero, "A second look finds nothing.");
        }

        [Test]
        public void Sweep_NeverRemovesARealSave_NorAnythingOfASeasonWhoseSaveIsThere()
        {
            // The first season's name is used again by a new season: its old leftovers stay beside it.
            string save = Put("episode.json"), backup = Put("episode.json.backup");
            string token = ".deleting-" + Hex();
            string besideSave = Put("episode.json" + token), besideBackup = Put("episode.json.backup" + token);
            string other = Put("episode-" + Hex() + ".json");
            string otherLeftover = Put(Path.GetFileName(other) + ".before-recovery-" + Hex() + ".json" + token);
            var before = Everything();

            Assert.That(EpisodeSaveStore.SweepInterruptedDeletes(folder), Is.Zero, "Every season here has its save: nothing is removed.");
            var after = Everything();
            Assert.That(after.Keys, Is.EquivalentTo(before.Keys));
            foreach (var name in before.Keys) Assert.That(after[name], Is.EqualTo(before[name]), name + " is as it was.");
            Assert.That(new[] { save, backup, besideSave, besideBackup, other, otherLeftover }.All(File.Exists), Is.True);
        }

        [Test]
        public void Sweep_GoesThroughNoLink_AndAMissingFolderIsNothing()
        {
            string token = ".deleting-" + Hex();
            string a = Put("episode-" + Hex() + ".json" + token), b = Put("episode-" + Hex() + ".json" + token);
            string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);
            Assert.That(EpisodeSaveStore.SweepInterruptedDeletes(folder, path => string.Equals(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar), root, StringComparison.OrdinalIgnoreCase)),
                Is.Zero, "The folder is a link: nothing is removed.");
            Assert.That(File.Exists(a) && File.Exists(b), Is.True);
            Assert.That(EpisodeSaveStore.SweepInterruptedDeletes(folder, path => string.Equals(Path.GetFullPath(path), Path.GetFullPath(a), StringComparison.OrdinalIgnoreCase)),
                Is.EqualTo(1), "A leftover that is a link is passed over; the other goes.");
            Assert.That(File.Exists(a), Is.True);
            Assert.That(File.Exists(b), Is.False);
            Assert.That(EpisodeSaveStore.SweepInterruptedDeletes(Path.Combine(folder, "missing")), Is.Zero);
            Assert.That(EpisodeSaveStore.SweepInterruptedDeletes(null), Is.Zero);
        }

        [Test]
        public void Sweep_LeavesAHeldLeftoverForTheNextLook()
        {
            string leftover = Put("episode-" + Hex() + ".json.deleting-" + Hex());
            // Windows, where the game runs, refuses to delete a file another handle holds. A system that
            // lets a held file be removed (the Linux machine the pull request checks run on) removes it
            // at the first look; either way nothing throws and the leftover is gone in the end.
            bool aHoldRefusesADelete = Path.DirectorySeparatorChar == '\\';
            using (new FileStream(leftover, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.That(EpisodeSaveStore.SweepInterruptedDeletes(folder), Is.EqualTo(aHoldRefusesADelete ? 0 : 1), "Held, it cannot go now where a hold refuses a delete, and nothing throws,");
            Assert.That(File.Exists(leftover), Is.EqualTo(aHoldRefusesADelete));
            Assert.That(EpisodeSaveStore.SweepInterruptedDeletes(folder), Is.EqualTo(aHoldRefusesADelete ? 1 : 0), "and once let go it does.");
            Assert.That(File.Exists(leftover), Is.False);
        }

        [Test]
        public void TheStore_DeletesThroughItsOneRemover()
        {
            string code = Code(Read("Persistence/EpisodeSaveStore.Delete.cs"));
            Assert.That(Regex.Matches(code, @"\bFile\s*\.\s*Delete\s*\(").Count, Is.EqualTo(1), "One File.Delete in the store's delete file,");
            string remover = Body(code, "private static int RemoveSetAside(");
            Assert.That(remover, Does.Match(@"\bFile\s*\.\s*Delete\s*\("), "and it is the remover's.");
            Assert.That(Body(code, "public static SeasonDeleteResult DeleteSeason("), Does.Contain("RemoveSetAside("), "The delete hands it what it set aside,");
            Assert.That(Body(code, "public static int SweepInterruptedDeletes("), Does.Contain("RemoveSetAside("), "and so does the sweep.");
            string read = Body(Code(Read("Persistence/SeasonShelf.cs")), "public IReadOnlyList<SavedSeason> Read(");
            int sweep = read.IndexOf("EpisodeSaveStore.SweepInterruptedDeletes(folder)", StringComparison.Ordinal);
            Assert.That(sweep, Is.GreaterThan(0), "The list sweeps as it looks,");
            Assert.That(sweep, Is.LessThan(read.IndexOf("EpisodeSaveStore.SeasonFiles(folder)", StringComparison.Ordinal)), "before it reads the seasons.");
        }

        // ---------------------------------------------------------------- a save another program holds

        [Test]
        public void IsHeld_IsAnotherProgramsHold_NotAMissingDeniedOrDamagedFile()
        {
            string save = Put("episode-" + Hex() + ".json");
            Exception held = null;
            using (new FileStream(save, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                try { File.ReadAllBytes(save); }
                catch (Exception error) { held = error; }
            }
            Assert.That(held, Is.Not.Null, "The read failed while the file was held.");
            Assert.That(EpisodeSaveStore.IsHeld(held), Is.True, "A hold is a plain IOException: in use.");
            Exception missing = null;
            try { File.ReadAllBytes(Path.Combine(folder, "nothing.json")); }
            catch (Exception error) { missing = error; }
            Assert.That(missing, Is.InstanceOf<FileNotFoundException>());
            Assert.That(EpisodeSaveStore.IsHeld(missing), Is.False, "A missing file is not in use,");
            Assert.That(EpisodeSaveStore.IsHeld(new DirectoryNotFoundException()), Is.False, "nor a missing folder,");
            Assert.That(EpisodeSaveStore.IsHeld(new UnauthorizedAccessException()), Is.False, "nor a denied one,");
            Assert.That(EpisodeSaveStore.IsHeld(new InvalidDataException()), Is.False, "nor a damaged one,");
            Assert.That(EpisodeSaveStore.IsHeld(null), Is.False, "nor one that opened.");
        }

        [Test]
        public void TheList_CardsAHeldSaveAsInUse_WithTryAgainAndNoDelete()
        {
            Assert.That(SavesWords.InUseTitle, Is.Not.EqualTo(SavesWords.Unreadable));
            Assert.That(SavesWords.InUseTitle, Does.Contain("in use"));
            Assert.That(SavesWords.InUseLine, Does.Contain("Try again"));
            string shelf = Code(Read("Persistence/SeasonShelf.cs"));
            Assert.That(shelf, Does.Match(@"InUse\s*=\s*EpisodeSaveStore\.IsHeld\("), "The list marks a held save in use.");
            string saves = Code(Read("Episode/EpisodeDirector.SettingsSaves.cs"));
            string card = Body(saves, "private void SeasonCardFor(");
            Assert.That(card, Does.Contain("SavesWords.TryAgainCaption"), "Its card offers Try again,");
            Assert.That(card, Does.Match(@"if \(!own && !season\.InUse\)"), "and no Delete,");
            Assert.That(card, Does.Contain("SavesWords.InUseLine"), "with the line that says why.");
            Assert.That(Body(saves, "private static string Title("), Does.Match(@"season\.InUse \? SavesWords\.InUseTitle"), "Its title says in use.");
        }

        // ---------------------------------------------------------------- Recover backup without a backup

        [Test]
        public void RecoverBackup_IsGreyedWithItsReason_WhereThereIsNoBackup()
        {
            Assert.That(SavesWords.NoBackupWhy, Does.StartWith("There is no backup yet."));
            string advanced = Body(Code(Read("Episode/EpisodeDirector.SettingsSaves.cs")), "private void AdvancedSection(");
            Assert.That(advanced, Does.Match(@"hud\.SettingAction\(RecoverBackupCaption,.*backup \? null : SavesWords\.NoBackupWhy\)"),
                "The row is greyed with its reason when there is no backup,");
            Assert.That(advanced, Does.Contain("advancedPick == AdvancedPick.Restore && backup"), "and its card is never drawn then.");
        }

        // ---------------------------------------------------------------- Escape and B on a confirm card

        [Test]
        public void EscapeAndB_OnAConfirmCard_TakeItsSafeAnswer()
        {
            string rows = Body(Code(Read("Episode/EpisodeHud.SettingRows.cs")), "public Button SettingsConfirm(");
            Assert.That(rows, Does.Contain("ConfirmCancelAction = safe;"), "The card hands the keys its safe answer,");
            string wayOut = Body(Code(Read("Episode/EpisodeHud.WayOut.cs")), "private void ForgetWayBack(");
            Assert.That(wayOut, Does.Contain("ConfirmCancelAction = null;"), "which goes with the render that drew it;");
            string back = Body(Code(Read("Episode/EpisodeDirector.Controls.cs")), "private bool BackNestedPage(");
            int cancel = back.IndexOf("hud.ConfirmCancelAction", StringComparison.Ordinal);
            Assert.That(cancel, Is.GreaterThan(0), "Escape and B ask for it");
            Assert.That(cancel, Is.LessThan(back.IndexOf("hud.WayBackAction", StringComparison.Ordinal)), "before the page's way back.");
        }

        // ---------------------------------------------------------------- the game menu's PAUSED

        [Test]
        public void PausedLabel_IsReadable_AtEveryTextSize()
        {
            foreach (var size in TextSizes.All)
            {
                float scale = TextSizes.Scale(size);
                int font = SettingsWords.PausedLabelFont(scale);
                Assert.That(font, Is.GreaterThanOrEqualTo(15), TextSizes.Name(size) + ": at least 15 px (it was 12),");
                Assert.That(font, Is.GreaterThanOrEqualTo((int)Math.Round(15 * scale)), TextSizes.Name(size) + ": growing with the text,");
                Assert.That(SettingsWords.PausedLabelBox(font), Is.GreaterThanOrEqualTo(font * WayOut.LabelBoxRatio), TextSizes.Name(size) + ": in a box 1.3 times its size.");
                Assert.That(32f * scale - SettingsWords.PausedLabelBox(font), Is.GreaterThanOrEqualTo(4f), TextSizes.Name(size) + ": its foot above the title.");
            }
            string card = Body(Code(Read("Episode/EpisodeHud.SettingsFrame.cs")), "private void SizePausedLabel(");
            Assert.That(card, Does.Contain("SettingsWords.PausedLabelFont(s)"));
            Assert.That(card, Does.Contain("SettingsWords.PausedLabelBox(size)"));
            Assert.That(card, Does.Contain("label.enableAutoSizing = false;"), "It is never shrunk to fit.");
            Assert.That(Body(Code(Read("Episode/EpisodeHud.SettingsFrame.cs")), "public void GameMenuCard("), Does.Contain("if (paused) SizePausedLabel(s);"));
        }

        // ---------------------------------------------------------------- sources

        private static string Read(string relative) => File.ReadAllText(Path.Combine(RuntimeRoot(), relative));

        /// <summary>A method's body from its signature to its closing brace.</summary>
        private static string Body(string code, string signature)
        {
            int start = code.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "Not found: " + signature);
            int open = code.IndexOf('{', start), depth = 0;
            Assert.That(open, Is.GreaterThan(start), "No body: " + signature);
            for (int index = open; index < code.Length; index++)
            {
                if (code[index] == '{') depth++;
                else if (code[index] == '}' && --depth == 0) return code.Substring(start, index + 1 - start);
            }
            return code.Substring(start);
        }

        /// <summary>The source with its line comments blanked (the sources read here put no "//" inside a string).</summary>
        private static string Code(string source) => Regex.Replace(source.Replace("\r\n", "\n"), @"//[^\n]*", "");

        private static string RuntimeRoot()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Runtime"));
#else
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "Gamesim", "Runtime");
                if (Directory.Exists(candidate)) return Path.GetFullPath(candidate);
            }
            Assert.Fail("Assets/Gamesim/Runtime was not found above " + AppContext.BaseDirectory);
            return null;
#endif
        }
    }
}
