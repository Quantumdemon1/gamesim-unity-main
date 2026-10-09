using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Only the window's own controls apply the window (PLAN A, A12; Risk R3), held by the source.
    /// The PlayMode guard counts <c>ApplyWindow</c> and <c>MoveToMonitor</c>, and the editor writes no
    /// window at all, so a window written anywhere else - a <c>Screen.fullScreen</c> put back into
    /// <c>ApplyDisplayPreferences</c>, the fan-out every volume click goes through - would pass it and
    /// undo an Alt+Enter, or the profile's windowed sampling, in a build. Here every write to the
    /// window in Runtime is found: <c>Screen.SetResolution</c>, an assignment to
    /// <c>Screen.fullScreen</c> or <c>Screen.fullScreenMode</c>, and <c>Screen.MoveMainWindowTo</c>.
    /// Each must sit in a method that may apply it - the director's <c>ApplyWindow</c> (the display
    /// controls and the profile's <c>SetDisplay</c>), its <c>MoveToMonitor</c> and the first launch's
    /// <c>ApplyFirstRunDisplay</c> - or in the verifier's own files, whose sweeps set the windows
    /// they photograph and never run in a player's session.
    ///
    /// <para>Unity-free, so the subset runs it on every push; the editor finds the scripts through
    /// <c>Application.dataPath</c>, the subset by walking up from its own binary.</para>
    /// </summary>
    public sealed class WindowWriteScanTests
    {
        /// <summary>The director's display file, relative to Assets/Gamesim/Runtime.</summary>
        private const string Director = "Episode/EpisodeDirector.Display.cs";

        /// <summary>The director's methods that may write the window: each must still write it, so the list stays exact.</summary>
        private static readonly string[] DirectorMethods = { "ApplyWindow", "MoveToMonitor", "ApplyFirstRunDisplay" };

        /// <summary>The verifier's files: its sweeps set their own windows, outside any player's session.</summary>
        private const string VerifierPrefix = "Episode/PortVerification";

        /// <summary>A write to the window: a resolution or a move, or an assignment (not a comparison) to its mode.</summary>
        private static readonly Regex WindowWrite = new Regex(
            @"\bScreen\s*\.\s*(SetResolution|MoveMainWindowTo)\s*\(|\bScreen\s*\.\s*(fullScreenMode|fullScreen)\s*=(?!=)",
            RegexOptions.CultureInvariant);

        /// <summary>A method's head up to its opening bracket: an access modifier, any others, a return type and the name.</summary>
        private static readonly Regex MethodHeader = new Regex(
            @"\b(?:public|private|protected|internal)\s+(?:(?:static|override|virtual|sealed|async|new|extern|unsafe|partial|readonly)\s+)*"
            + @"[\w\.\?\[\]]+(?:<[^()]*?>)?[\?\[\]]*\s+(?<name>\w+)\s*(?:<[^()]*?>)?\s*\(",
            RegexOptions.CultureInvariant);

        private sealed class Write
        {
            public string File, Method, What;
            public int Line;
            public override string ToString() => File + ":" + Line + " " + What + " in " + (Method ?? "no method");
        }

        [Test]
        public void Runtime_WritesTheWindowOnlyInTheWindowsOwnMethodsAndTheVerifier()
        {
            string root = RuntimeRoot();
            var found = new List<Write>();
            foreach (var path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                found.AddRange(Writes(Relative(root, path), Code(File.ReadAllText(path))));
            found = found.OrderBy(write => write.File, StringComparer.Ordinal).ThenBy(write => write.Line).ToList();
            TestContext.WriteLine("Window writes in Runtime (" + found.Count + "):\n  " + string.Join("\n  ", found));

            var problems = found.Select(Problem).Where(problem => problem != null).ToList();
            foreach (var method in DirectorMethods)
                if (!found.Any(write => write.File == Director && write.Method == method))
                    problems.Add(Director + "'s " + method + " no longer writes the window: take it off the list, or find why the scan stopped seeing it.");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void TheScan_FindsAWindowWriteInTheFanOutAndNamesItsMethod()
        {
            // The mutation the PlayMode counter cannot see: the window put back from the preferences' fan-out.
            const string source = "public sealed partial class EpisodeDirector\n{\n"
                + "    private void ApplyWindow() { if (!Application.isEditor) Screen.SetResolution(size.x, size.y, mode); }\n"
                + "    private void ApplyDisplayPreferences()\n    {\n"
                + "        QualitySettings.vSyncCount = vSync ? 1 : 0;\n"
                + "        Screen.fullScreen = displayMode != DisplayMode.Windowed;\n"
                + "    }\n}\n";
            var writes = Writes(Director, Code(source));
            Assert.That(writes.Select(write => write.Method + ":" + write.Line), Is.EqualTo(new[] { "ApplyWindow:3", "ApplyDisplayPreferences:7" }));
            Assert.That(Problem(writes[0]), Is.Null, "ApplyWindow may write the window.");
            Assert.That(Problem(writes[1]), Does.Contain("ApplyDisplayPreferences"), "The fan-out may not.");
            Assert.That(Problem(new Write { File = "Episode/PortVerification.LookSheet.cs", Method = "Capture", What = "Screen.SetResolution(", Line = 1 }),
                Is.Null, "The verifier's sweeps may.");
            Assert.That(Problem(new Write { File = "Presentation/Somewhere.cs", Method = "ApplyWindow", What = "Screen.SetResolution(", Line = 1 }),
                Is.Not.Null, "A method of the same name elsewhere is not the director's.");
        }

        [Test]
        public void TheScan_CountsWritesNotReads_AndCommentsStringsAndCharactersNeitherCountNorMoveTheMethod()
        {
            const string source = "private void Look()\n{\n"
                + "    if (Screen.fullScreenMode == FullScreenMode.Windowed && Screen.fullScreen != wanted) { }\n"
                + "    var mode = Screen.fullScreenMode; // Screen.SetResolution(1, 2, mode) in a comment\n"
                + "    /* Screen.fullScreen = true; } */ Debug.Log(\"Screen.MoveMainWindowTo(display, at) }\");\n"
                + "    Debug.Log($\"{(full ? \"}\" : \"{\")} Screen.SetResolution( {{\" + @\"}\"\"\" + '{');\n"
                + "}\n"
                + "private void Write() { Screen.fullScreenMode = FullScreenMode.Windowed; UnityEngine.Screen.SetResolution(1, 2, true); Screen.MoveMainWindowTo(d, p); }\n";
            var writes = Writes("Episode/Probe.cs", Code(source));
            Assert.That(writes.Select(write => write.Method + ":" + write.Line), Is.EqualTo(new[] { "Write:8", "Write:8", "Write:8" }),
                "Three writes, all in Write: reads, comments, strings and the braces inside literals are none, and do not end Look early.");
        }

        /// <summary>What a write breaks, in words, or null when it is where a window may be written.</summary>
        private static string Problem(Write write)
        {
            if (write.File.StartsWith(VerifierPrefix, StringComparison.Ordinal)) return null;
            if (write.File == Director && DirectorMethods.Contains(write.Method)) return null;
            return write.File + " line " + write.Line + " writes the window (" + write.What + ") in " + (write.Method ?? "no method")
                + ": only the display controls (ApplyWindow, MoveToMonitor) and the first launch (ApplyFirstRunDisplay) may, never a setting"
                + " that goes through ApplyPreferences - it would undo an Alt+Enter, and the profile's windowed sampling (Risk R3).";
        }

        /// <summary>Every window write in a file's code (comments and literals blanked), with the method it sits in.</summary>
        private static List<Write> Writes(string file, string code)
        {
            var spans = new List<(string Name, int Start, int End)>();
            foreach (Match header in MethodHeader.Matches(code))
            {
                int end = BodyEnd(code, header.Index + header.Length - 1);
                if (end > 0) spans.Add((header.Groups["name"].Value, header.Index, end));
            }
            var writes = new List<Write>();
            foreach (Match match in WindowWrite.Matches(code))
            {
                // The last method that opened before the write and has not closed: methods do not nest.
                string method = spans.Where(span => span.Start < match.Index && span.End > match.Index).Select(span => span.Name).LastOrDefault();
                writes.Add(new Write { File = file, Method = method, What = Regex.Replace(match.Value, @"\s+", ""), Line = Line(code, match.Index) });
            }
            return writes;
        }

        /// <summary>Where a member that opens its parameters at <paramref name="open"/> ends: its body's brace, or an expression body's semicolon; -1 for neither.</summary>
        private static int BodyEnd(string code, int open)
        {
            int close = Matching(code, open, '(', ')');
            if (close < 0) return -1;
            int at = close + 1;
            while (at < code.Length && code[at] != '{' && code[at] != ';' && !(code[at] == '=' && at + 1 < code.Length && code[at + 1] == '>')) at++;
            if (at >= code.Length || code[at] == ';') return -1;
            if (code[at] == '{') return Matching(code, at, '{', '}');
            int depth = 0;
            for (int index = at + 2; index < code.Length; index++)
            {
                char c = code[index];
                if (c == '(' || c == '{' || c == '[') depth++;
                else if (c == ')' || c == '}' || c == ']') depth--;
                else if (c == ';' && depth == 0) return index;
            }
            return -1;
        }

        private static int Matching(string code, int open, char opening, char closing)
        {
            int depth = 0;
            for (int index = open; index < code.Length; index++)
            {
                if (code[index] == opening) depth++;
                else if (code[index] == closing && --depth == 0) return index;
            }
            return -1;
        }

        private static int Line(string code, int index)
        {
            int line = 1;
            for (int at = 0; at < index; at++) if (code[at] == '\n') line++;
            return line;
        }

        // ------------------------------------------------------------------ comments and literals blanked

        /// <summary>
        /// A source with its comments and the insides of its string and character literals blanked,
        /// its length and line breaks kept: so a brace or a call in a literal neither counts nor
        /// throws the methods' brackets out. An interpolated string's holes stay code.
        /// </summary>
        private static string Code(string source)
        {
            var text = source.ToCharArray();
            int at = 0;
            ScanCode(text, ref at, false);
            return new string(text);
        }

        private static void Blank(char[] text, int index)
        {
            if (text[index] != '\n' && text[index] != '\r') text[index] = ' ';
        }

        /// <summary>Code to the end, or in an interpolation hole to the brace that closes it (left at <paramref name="i"/>).</summary>
        private static void ScanCode(char[] s, ref int i, bool hole)
        {
            int depth = 0;
            while (i < s.Length)
            {
                char c = s[i], next = i + 1 < s.Length ? s[i + 1] : '\0';
                if (c == '/' && next == '/') { while (i < s.Length && s[i] != '\n') Blank(s, i++); continue; }
                if (c == '/' && next == '*')
                {
                    Blank(s, i++); Blank(s, i++);
                    while (i < s.Length && !(s[i] == '*' && i + 1 < s.Length && s[i + 1] == '/')) Blank(s, i++);
                    if (i < s.Length) { Blank(s, i++); Blank(s, i++); }
                    continue;
                }
                if (c == '"') { ScanString(s, ref i); continue; }
                if (c == '\'') { ScanCharacter(s, ref i); continue; }
                if (hole && c == '{') depth++;
                else if (hole && c == '}') { if (depth == 0) return; depth--; }
                i++;
            }
        }

        /// <summary>A string literal from its opening quote: regular, verbatim, interpolated or both.</summary>
        private static void ScanString(char[] s, ref int i)
        {
            bool verbatim = false, interpolated = false;
            for (int back = i - 1; back >= 0 && back >= i - 2 && (s[back] == '@' || s[back] == '$'); back--)
            {
                if (s[back] == '@') verbatim = true;
                else interpolated = true;
            }
            i++;
            while (i < s.Length)
            {
                char c = s[i], next = i + 1 < s.Length ? s[i + 1] : '\0';
                if (verbatim && c == '"' && next == '"') { Blank(s, i++); Blank(s, i++); continue; }
                if (!verbatim && c == '\\') { Blank(s, i++); if (i < s.Length) Blank(s, i++); continue; }
                if (c == '"') { i++; return; }
                if (interpolated && (c == '{' || c == '}') && next == c) { Blank(s, i++); Blank(s, i++); continue; }
                if (interpolated && c == '{')
                {
                    Blank(s, i++);
                    ScanCode(s, ref i, true);
                    if (i < s.Length) Blank(s, i++);
                    continue;
                }
                if (!verbatim && c == '\n') return;
                Blank(s, i++);
            }
        }

        private static void ScanCharacter(char[] s, ref int i)
        {
            i++;
            while (i < s.Length && s[i] != '\'' && s[i] != '\n')
            {
                if (s[i] == '\\') { Blank(s, i++); if (i < s.Length) Blank(s, i++); continue; }
                Blank(s, i++);
            }
            if (i < s.Length && s[i] == '\'') i++;
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
