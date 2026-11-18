using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Menu slice 3, held together: every caller that opens a docked panel names a layout the kinds
    /// table lists (so no panel opens without a kind), the alliance faces' rims say trust the chip's
    /// one way, and a profile link's backing is at rest before its press takes it.
    ///
    /// <para>Plain C#, read from the source: the cases run in the Unity-free suite as well as in
    /// EditMode (the source root from <c>Application.dataPath</c> in the editor, by walking up from
    /// the binary in the subset).</para>
    /// </summary>
    public sealed class MenuNotebookIntegrationTests
    {
        private static readonly Regex OpenCall = new Regex(@"(?<!void\s)\bSetActivityLayout\s*\(\s*([^)]*)\)");
        private static readonly Regex NamedLayout = new Regex(@"^(?:EpisodeHud\s*\.\s*)?ActivityLayout\s*\.\s*(\w+)$");

        [Test]
        public void EveryCallerThatOpensADockedPanel_NamesALayoutWithAKind()
        {
            int calls = 0;
            foreach (var path in Directory.GetFiles(RuntimeRoot(), "*.cs", SearchOption.AllDirectories))
            {
                var code = Code(File.ReadAllText(path));
                foreach (Match call in OpenCall.Matches(code))
                {
                    string argument = call.Groups[1].Value.Trim();
                    string where = Path.GetFileName(path) + ": SetActivityLayout(" + argument + ")";
                    var named = NamedLayout.Match(argument);
                    Assert.That(named.Success, Is.True, where + " names no layout: a docked panel opens with one the kinds table can check.");
                    Assert.That(ReadingFrame.TryKind(named.Groups[1].Value, out _), Is.True,
                        where + " opens a layout that ReadingFrame.Kinds does not list as Reading or Quick.");
                    calls++;
                }
            }
            Assert.That(calls, Is.GreaterThanOrEqualTo(10), "The scan found the HUD's layout calls (" + calls + "), so it is not empty.");
        }

        [Test]
        public void TheAlliancesFaces_DrawTheirRimInTheChipsBand_WhereTheChipReadsTrust()
        {
            var code = Code(File.ReadAllText(Path.Combine(RuntimeRoot(), "Episode", "EpisodeHud.Alliances.cs")));
            Assert.That(code, Does.Contain("face.Away ? UiTheme.Outline : RimTint(face)"),
                "A member's rim comes from RimTint, not the five-band reading tint.");
            Assert.That(code, Does.Contain("TrustChip.Tint(TrustChip.BandOf("), "RimTint takes the chip's own band and tint.");
        }

        [Test]
        public void AProfileLinksBacking_IsAtRestBeforeItsPressTakesIt()
        {
            var code = Code(File.ReadAllText(Path.Combine(RuntimeRoot(), "Episode", "EpisodeHud.ProfileLinks.cs")));
            int rest = code.IndexOf("backing.canvasRenderer.SetAlpha(0f)", StringComparison.Ordinal);
            int taken = code.IndexOf("button.targetGraphic = backing", StringComparison.Ordinal);
            Assert.That(rest, Is.GreaterThan(0), "The backing is set to its rest alpha at once.");
            Assert.That(taken, Is.GreaterThan(rest), "Before the press takes it, so its tint starts from hidden: no flash on every render.");
        }

        /// <summary>A source with its comments blanked and its line breaks kept, never inside a string.</summary>
        private static string Code(string source)
        {
            var code = new StringBuilder(source.Length);
            bool block = false, text = false;
            for (var index = 0; index < source.Length; index++)
            {
                char c = source[index];
                char next = index + 1 < source.Length ? source[index + 1] : '\0';
                if (block)
                {
                    if (c == '*' && next == '/') { block = false; index++; }
                    else if (c == '\n') code.Append('\n');
                    continue;
                }
                if (text)
                {
                    code.Append(c);
                    if (c == '\\' && next != '\0') { code.Append(next); index++; }
                    else if (c == '"' || c == '\n') text = false;
                    continue;
                }
                if (c == '"') { text = true; code.Append(c); continue; }
                if (c == '\'' && index + 2 < source.Length)
                {
                    int end = source.IndexOf('\'', index + (next == '\\' ? 3 : 2));
                    if (end > index && end - index <= 4) { code.Append(source, index, end - index + 1); index = end; continue; }
                }
                if (c == '/' && next == '*') { block = true; index++; continue; }
                if (c == '/' && next == '/')
                {
                    while (index < source.Length && source[index] != '\n') index++;
                    code.Append('\n');
                    continue;
                }
                code.Append(c);
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
