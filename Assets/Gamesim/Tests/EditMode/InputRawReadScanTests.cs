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
            new Allowed("Episode/EpisodeDirector.Challenge.cs", 1, Allowance.Pending, "Space taps a classic reaction target"),
            new Allowed("Episode/EpisodeDirector.cs", 5, Allowance.Pending, "Tab follows with nothing focused; G opens your moves"),
            new Allowed("Episode/EpisodeHud.cs", 2, Allowance.Pending, "Tab walks the HUD's ring; the speech field swallows Escape and Tab"),
            new Allowed("House/HouseInteraction.cs", 1, Allowance.Pending, "the prototype house's E, Escape and reply digits"),
            new Allowed("House/HousePlayerController.cs", 1, Allowance.Pointer, "a left click on the floor walks there"),
            new Allowed("Presentation/CeremonyTakeover.cs", 8, Allowance.Pending, "a ceremony card's skip, speed and which device was last used"),
            new Allowed("Presentation/CompetitionDirectionControl.cs", 2, Allowance.Pending, "a direction game's four directions"),
            new Allowed("Presentation/CompetitionGameScreen.Chrome.cs", 2, Allowance.Pending, "the legend follows the device last used"),
            new Allowed("Presentation/CompetitionGameScreen.Endurance.cs", 2, Allowance.Pending, "Space or the right trigger holds on"),
            new Allowed("Presentation/CompetitionGameScreen.cs", 2, Allowance.Pending, "Escape, P, Tab and the pad's buttons on the board"),
            new Allowed("Presentation/CompetitionResult.cs", 2, Allowance.Pending, "back and continue on the results card"),
            new Allowed("Presentation/OpeningSequence.cs", 1, Allowance.Pending, "Space moves the opening on"),
            new Allowed("Presentation/SeasonReport.cs", 3, Allowance.Pending, "Page Up, Page Down, Home, End and the right stick"),
        };

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
