using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Gamesim.Tools.Tests
{
    /// <summary>
    /// Unity writes a UMA part's slot (its mesh inside) and overlay as binary even in a Force Text
    /// project. Stored as text, git would rewrite their line endings and hand every clone an asset
    /// Unity cannot load, so every .asset under the UMA content folder that is not YAML text must be
    /// marked binary in .gitattributes: by the folder's pattern lines for slots, overlays and meshes,
    /// or by a line of its own.
    ///
    /// <para>Here, not in the editor's suite: this needs the repository's .gitattributes, which a
    /// Unity test copy may not carry. It reads files only and runs no git command; a line is matched
    /// as a whole string, never as a glob.</para>
    /// </summary>
    [TestFixture]
    public sealed class UmaContentBinaryPinTests
    {
        private const string Content = "Assets/Gamesim/Uma/Content";
        private static readonly string[] PinnedEndings = { "_Slot.asset", "_Overlay.asset", "_Mesh.asset" };
        private static readonly string[] Pins = PinnedEndings.Select(ending => Content + "/**/*" + ending + " binary").ToArray();

        /// <summary>The repository: the first folder above the test assembly that holds .gitattributes and Assets.</summary>
        private static string Root()
        {
            for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
                if (File.Exists(Path.Combine(folder.FullName, ".gitattributes")) && Directory.Exists(Path.Combine(folder.FullName, "Assets")))
                    return folder.FullName;
            Assert.Fail("No folder above " + AppContext.BaseDirectory + " holds both .gitattributes and Assets.");
            return null;
        }

        private static HashSet<string> Lines(string root) => new HashSet<string>(
            File.ReadAllLines(Path.Combine(root, ".gitattributes")).Select(line => line.TrimEnd('\r', ' ', '\t')), StringComparer.Ordinal);

        /// <summary>YAML text starts with "%YAML", after a byte-order mark if there is one.</summary>
        private static bool IsYamlText(string path)
        {
            var head = new byte[8];
            int read;
            using (var stream = File.OpenRead(path)) read = stream.Read(head, 0, head.Length);
            int start = read >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF ? 3 : 0;
            return read - start >= 5 && Encoding.ASCII.GetString(head, start, 5) == "%YAML";
        }

        [Test]
        public void TheUmaContentFolderIsPinnedBinaryForSlotsOverlaysAndMeshes()
        {
            var lines = Lines(Root());
            foreach (string pin in Pins)
                Assert.That(lines, Does.Contain(pin), ".gitattributes keeps the line `" + pin + "`.");
        }

        [Test]
        public void EveryAssetUnderUmaContentThatIsNotYamlTextIsMarkedBinary()
        {
            string root = Root();
            var lines = Lines(root);
            var binary = Directory.EnumerateFiles(Path.Combine(root, Content), "*.asset", SearchOption.AllDirectories)
                .Where(path => !IsYamlText(path))
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal).ToList();
            Assert.That(binary, Is.Not.Empty, "The house garments' slots and overlays are binary: the walk found none under " + Content + ".");
            foreach (string path in binary)
                Assert.That(PinnedEndings.Any(ending => path.EndsWith(ending, StringComparison.Ordinal)) || lines.Contains(path + " binary"),
                    Is.True, path + " is not YAML text: add the line `" + path + " binary` to .gitattributes before this file is committed.");
        }
    }
}
