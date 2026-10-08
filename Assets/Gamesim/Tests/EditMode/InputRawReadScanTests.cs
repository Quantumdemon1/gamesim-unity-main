using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The raw-read guard (PLAN A, A0): every press in the game reaches it through the actions map,
    /// so that a pad, a rebinding and a test all go the same way. A read straight off
    /// <c>Keyboard.current</c>, <c>Gamepad.current</c> or <c>Mouse.current</c> in Runtime bypasses
    /// every one of those, so each file that still has one is named here with how many and why.
    ///
    /// <para>Two reasons are for good: the pointer on the house itself (a click on the floor is a
    /// position, not a press any action can carry) and typing (a letter is whatever the keyboard's
    /// own layout says it is). Anything else is pending a reroute, and the list only shrinks: a file
    /// that gains a read fails, and so does an entry whose reads have gone, so the list stays exact.</para>
    ///
    /// <para>Unity-free, so the subset runs it on every push; the editor finds the scripts through
    /// <c>Application.dataPath</c>, the subset by walking up from its own binary.</para>
    /// </summary>
    public sealed class InputRawReadScanTests
    {
        public enum Allowance { Pointer, Typing, Pending }

        private sealed class Allowed
        {
            public readonly string File;
            public readonly int Count;
            public readonly Allowance Why;
            public readonly string Reason;
            public Allowed(string file, int count, Allowance why, string reason) { File = file; Count = count; Why = why; Reason = reason; }
        }

        /// <summary>Each Runtime file that may read a device directly, relative to Assets/Gamesim/Runtime.</summary>
        private static readonly Allowed[] Allowlist =
        {
            // A1 rerouted the rest through the actions asset (HouseInput): the director's Tab and G,
            // the HUD's ring and the speech field, the ceremony cards, a competition's board, hold,
            // directions and results, the opening, the season report and the prototype house.
            new Allowed("House/HousePlayerController.cs", 1, Allowance.Pointer, "a left click on the floor walks there"),
            new Allowed("Presentation/CompetitionGameScreen.Words.cs", 1, Allowance.Typing, "the word board types each letter on the keyboard's own layout"),
        };

        [Test]
        public void TheAllowlist_HoldsOnlyThePointerAndTyping()
        {
            // PLAN A, A1's acceptance: nothing left pending a reroute.
            Assert.That(Allowlist.Where(entry => entry.Why == Allowance.Pending).Select(entry => entry.File), Is.Empty);
        }

        private static readonly Regex RawRead = new Regex(@"\b(Keyboard|Gamepad|Mouse)\.current\b", RegexOptions.CultureInvariant);

        [Test]
        public void Runtime_ReadsDevicesDirectlyOnlyWhereTheAllowlistSays()
        {
            string root = RuntimeRoot();
            var found = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                int count = CountReads(File.ReadAllText(path));
                if (count > 0) found[Relative(root, path)] = count;
            }

            TestContext.WriteLine("Direct device reads allowed in Runtime (" + Allowlist.Length + " files):");
            foreach (var entry in Allowlist.OrderBy(entry => entry.Why).ThenBy(entry => entry.File, StringComparer.Ordinal))
                TestContext.WriteLine("  " + entry.Why + "  " + entry.File + " x" + entry.Count + " - " + entry.Reason);

            var problems = new List<string>();
            foreach (var pair in found)
            {
                var allowed = Allowlist.FirstOrDefault(entry => entry.File == pair.Key);
                if (allowed == null) problems.Add(pair.Key + " reads a device directly " + pair.Value + " time(s) and is not allowlisted: route it through the actions map.");
                else if (allowed.Count != pair.Value)
                    problems.Add(pair.Key + " has " + pair.Value + " direct read(s); the allowlist says " + allowed.Count
                        + (pair.Value > allowed.Count ? ": a new read must go through the actions map." : ": shrink the allowlist to match."));
            }
            foreach (var entry in Allowlist)
                if (!found.ContainsKey(entry.File))
                    problems.Add(entry.File + " no longer reads a device directly: take it off the allowlist.");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void TheScan_CountsCodeAndIgnoresComments()
        {
            const string source = "var k = Keyboard.current; // Gamepad.current in a comment\n"
                + "/* Mouse.current in a block\n Keyboard.current still in it */ var p = Gamepad.current;\n"
                + "/// <see cref=\"Mouse.current\"/>\n"
                + "var position = Pointer.current; var notIt = MyKeyboard.currentThing;\n";
            Assert.That(CountReads(source), Is.EqualTo(2), "Keyboard.current and Gamepad.current in code; nothing in comments, and Pointer is not a press.");
        }

        [Test]
        public void TheAllowlist_NamesEachFileOnceWithAReason()
        {
            Assert.That(Allowlist.Select(entry => entry.File).Distinct().Count(), Is.EqualTo(Allowlist.Length));
            foreach (var entry in Allowlist)
            {
                Assert.That(entry.Count, Is.GreaterThan(0), entry.File);
                Assert.That(entry.Reason, Is.Not.Empty, entry.File);
                Assert.That(entry.File, Does.EndWith(".cs").And.Not.Contain("\\"), entry.File);
            }
        }

        /// <summary>Direct reads in a source, comments stripped: line comments and block comments both.</summary>
        internal static int CountReads(string source)
        {
            var code = new System.Text.StringBuilder(source.Length);
            bool block = false;
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
                if (c == '/' && next == '*') { block = true; index++; continue; }
                if (c == '/' && next == '/')
                {
                    while (index < source.Length && source[index] != '\n') index++;
                    code.Append('\n');
                    continue;
                }
                code.Append(c);
            }
            return RawRead.Matches(code.ToString()).Count;
        }

        private static string Relative(string root, string path) =>
            path.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');

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
