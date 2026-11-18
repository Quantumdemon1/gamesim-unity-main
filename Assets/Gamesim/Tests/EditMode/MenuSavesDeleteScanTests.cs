using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Menu slice 4's one way to delete a season, held in the source: in every Runtime file the slice
    /// added or changed, nothing deletes or moves a file or a folder (File.Delete, Directory.Delete,
    /// File.Move, Directory.Move, a FileInfo's or DirectoryInfo's Delete) except the save store's one
    /// delete method, EpisodeSaveStore.DeleteSeason, which fences the save folder. Each other call is
    /// one listed line with its reason, and a listed line whose call is gone fails too.
    ///
    /// <para>Unity-free, so the subset runs it as well; the editor finds the scripts through
    /// <c>Application.dataPath</c>, the subset by walking up from its own binary.</para>
    /// </summary>
    public sealed class MenuSavesDeleteScanTests
    {
        /// <summary>The Runtime files menu slice 4 added or changed, relative to Assets/Gamesim/Runtime.</summary>
        private static readonly string[] SliceFiles =
        {
            "Episode/EpisodeDirector.CameraSettings.cs", "Episode/EpisodeDirector.Career.cs", "Episode/EpisodeDirector.CeremonyTruth.cs",
            "Episode/EpisodeDirector.Controls.cs", "Episode/EpisodeDirector.Display.cs", "Episode/EpisodeDirector.Season.cs",
            "Episode/EpisodeDirector.SettingsFrame.cs", "Episode/EpisodeDirector.SettingsPrefs.cs", "Episode/EpisodeDirector.SettingsSaves.cs",
            "Episode/EpisodeDirector.cs", "Episode/EpisodeHud.SeasonCards.cs", "Episode/EpisodeHud.SettingRows.cs",
            "Episode/EpisodeHud.SettingsFrame.cs", "Episode/EpisodeHud.WayOut.cs", "Episode/PortVerification.Autonomy.cs",
            "Episode/PortVerification.Creator.cs", "Episode/PortVerification.Season.cs", "Episode/PortVerification.cs",
            "Episode/SettingRowButton.cs", "Episode/SettingRowSlider.cs", "Persistence/EpisodeSaveStore.Delete.cs",
            "Persistence/EpisodeSaveStore.cs", "Persistence/SeasonShelf.cs", "Presentation/CaptionTiming.cs",
            "Presentation/MainMenu.cs", "Presentation/SavesWords.cs", "Presentation/SettingRowWords.cs",
            "Presentation/SettingsWords.cs", "Presentation/SoundPictures.cs", "Presentation/TextSizes.cs",
        };

        /// <summary>The one method allowed to delete: its file and its declaration.</summary>
        private const string StoreFile = "Persistence/EpisodeSaveStore.Delete.cs";
        private const string StoreMethod = "public static SeasonDeleteResult DeleteSeason(";

        private sealed class Exemption
        {
            public readonly string File, Call, Reason;
            public Exemption(string file, string call, string reason) { File = file; Call = call; Reason = reason; }
        }

        /// <summary>Each call outside the store's delete that is allowed, one line with its reason.</summary>
        private static readonly Exemption[] Exemptions =
        {
            new Exemption("Persistence/EpisodeSaveStore.cs", "File.Delete(temporary)", "the store's own staging file, removed after every save; never a season (older than the slice)"),
            new Exemption("Persistence/EpisodeSaveStore.cs", "File.Move(pending, target)", "the save's swap moving the store's own pending file into its target, when no target exists yet; it writes a save and removes none (older than the slice)"),
        };

        private static readonly Regex DeleteCall = new Regex(
            @"\b(?:File|Directory)\s*\.\s*(?:Delete|Move)\s*\([^;{}]*?\)|\.\s*Delete\s*\(\s*(?:true|false)?\s*\)|\bFileSystem\s*\.\s*Delete\w*\s*\(",
            RegexOptions.CultureInvariant);

        [Test]
        public void TheSlicesFiles_DeleteNothingOutsideTheStoresOneDelete()
        {
            string root = RuntimeRoot();
            var problems = new List<string>();
            var used = new HashSet<Exemption>();
            foreach (string file in SliceFiles)
            {
                string path = Path.Combine(root, file);
                Assert.That(File.Exists(path), Is.True, "A listed file is missing: " + file);
                problems.AddRange(Problems(file, File.ReadAllText(path), used));
            }
            Assert.That(problems, Is.Empty, "Only EpisodeSaveStore.DeleteSeason deletes: " + string.Join("; ", problems));
            Assert.That(Exemptions.Where(item => !used.Contains(item)).Select(item => item.File + ": " + item.Call), Is.Empty,
                "An exemption whose call is gone is removed with it.");
        }

        [Test]
        public void TheScan_FlagsAPlantedDelete_AndLeavesTheStoresOwnAlone()
        {
            var used = new HashSet<Exemption>();
            Assert.That(Problems("Episode/Planted.cs", "class A { void B() { File.Delete(path); } }", used), Has.Count.EqualTo(1),
                "A File.Delete is flagged.");
            Assert.That(Problems("Episode/Planted.cs", "class A { void B() { Directory.Delete(folder, true); new FileInfo(p).Delete(); } }", used),
                Has.Count.EqualTo(2), "A Directory.Delete and a FileInfo's Delete are flagged.");
            Assert.That(Problems("Episode/Planted.cs", "class A { void B() { File.Move(save, elsewhere); } }", used), Has.Count.EqualTo(1),
                "A move out of the folder is flagged.");
            Assert.That(Problems("Episode/Planted.cs", "class A { void B() { /* File.Delete(path); */ var s = \"File.Delete(x)\"; PressDelete(c); } } // Directory.Delete(d)", used),
                Is.Empty, "Comments, strings and a method merely named for a delete are not deletes.");
            Assert.That(Problems(StoreFile, "class S { " + StoreMethod + "string f) { if (f != null) { File.Delete(f); } return 0; } }", used), Is.Empty,
                "The store's one delete may delete.");
            Assert.That(Problems(StoreFile, "class S { " + StoreMethod + "string f) { return 0; } static void Other(string f) { File.Delete(f); } }", used),
                Has.Count.EqualTo(1), "Another method in the store's own file may not.");
        }

        private static List<string> Problems(string file, string source, HashSet<Exemption> used)
        {
            string code = Code(source);
            int allowedFrom = -1, allowedTo = -1;
            if (file == StoreFile)
            {
                int start = code.IndexOf(StoreMethod, StringComparison.Ordinal);
                Assert.That(start, Is.GreaterThanOrEqualTo(0), "The store's one delete method is where the scan expects it.");
                allowedFrom = start;
                allowedTo = EndOfBody(code, code.IndexOf('{', start));
            }
            var problems = new List<string>();
            foreach (Match match in DeleteCall.Matches(code))
            {
                if (match.Index >= allowedFrom && match.Index < allowedTo) continue;
                string call = Regex.Replace(match.Value, @"\s+", "");
                var exemption = Exemptions.FirstOrDefault(item => item.File == file && Regex.Replace(item.Call, @"\s+", "") == call);
                if (exemption != null) { used.Add(exemption); continue; }
                problems.Add(file + ": " + match.Value);
            }
            return problems;
        }

        private static int EndOfBody(string code, int open)
        {
            if (open < 0) return -1;
            int depth = 0;
            for (int index = open; index < code.Length; index++)
            {
                if (code[index] == '{') depth++;
                else if (code[index] == '}' && --depth == 0) return index;
            }
            return code.Length;
        }

        /// <summary>The source with its comments and string and character literals blanked, its length kept.</summary>
        private static string Code(string source)
        {
            var code = new StringBuilder(source.Length);
            for (int index = 0; index < source.Length; index++)
            {
                char c = source[index];
                char next = index + 1 < source.Length ? source[index + 1] : '\0';
                if (c == '/' && next == '/')
                {
                    while (index < source.Length && source[index] != '\n') { code.Append(' '); index++; }
                    if (index < source.Length) code.Append('\n');
                }
                else if (c == '/' && next == '*')
                {
                    int end = source.IndexOf("*/", index + 2, StringComparison.Ordinal);
                    end = end < 0 ? source.Length : end + 2;
                    for (; index < end; index++) code.Append(source[index] == '\n' ? '\n' : ' ');
                    index--;
                }
                else if (c == '"' || c == '\'')
                {
                    bool verbatim = c == '"' && index > 0 && source[index - 1] == '@';
                    code.Append(' ');
                    for (index++; index < source.Length; index++)
                    {
                        if (!verbatim && source[index] == '\\') { code.Append("  "); index++; continue; }
                        if (source[index] == c)
                        {
                            if (verbatim && index + 1 < source.Length && source[index + 1] == '"') { code.Append("  "); index++; continue; }
                            break;
                        }
                        code.Append(source[index] == '\n' ? '\n' : ' ');
                    }
                    code.Append(' ');
                }
                else code.Append(c);
            }
            return code.ToString();
        }

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
