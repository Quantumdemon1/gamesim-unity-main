using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The tour names the left rail by its captions (UI-UX plan U8): every rail caption a tour step
    /// names - "The Overview of the whole house", the rows listed after "Notebook:", and the rows
    /// "at its foot" - is one of the rail's caption constants (the rail's first entry in
    /// EpisodeHud.Chrome.cs, <see cref="NotebookStrip.RailHeading"/>, <see cref="NotebookStrip.RailRows"/>
    /// and <see cref="NotebookStrip.FootRows"/>, their key hints aside). A row renamed on the rail
    /// and not in the tour fails here. And the cast strip's step says what a click on a face opens:
    /// its menu, with talk, profile, deals and follow.
    ///
    /// <para>Plain C# reading the sources, so the cases run in the Unity-free suite as well as in
    /// EditMode (the source root from <c>Application.dataPath</c> in the editor, by walking up from
    /// the binary in the subset).</para>
    /// </summary>
    public sealed class TourNamesRailTests
    {
        [Test]
        public void EveryRailCaptionATourStepNames_IsARailCaption()
        {
            var captions = RailCaptions();
            var named = new List<string>();
            foreach (var step in TourSteps())
                named.AddRange(RailNames(step.Body));
            Assert.That(named.Count, Is.GreaterThanOrEqualTo(NotebookStrip.RailRows.Count - 1 + NotebookStrip.FootRows.Count + 1),
                "The tour's rail step still lists the rail as this test reads it (the Overview, the Notebook's rows, the foot); found: "
                + string.Join(" | ", named));
            var strangers = named.Where(name => !captions.Contains(name)).ToList();
            Assert.That(strangers, Is.Empty, "The tour names rail entries the rail does not have: " + string.Join(", ", strangers)
                + ". The rail's captions: " + string.Join(", ", captions));
        }

        [Test]
        public void TheRuleFindsARailCaptionTheRailDoesNotHave()
        {
            var captions = RailCaptions();
            var named = RailNames("The Overview of the whole house, then your Notebook: your notes, Houseguests, The votes and Who is where. Save and Options are at its foot.").ToList();
            Assert.That(named, Is.EqualTo(new[] { "Overview", "Houseguests", "The votes", "Who is where", "Save", "Options" }),
                "Lowercase words are descriptions; capitalised items are captions.");
            Assert.That(named.Where(name => !captions.Contains(name)), Is.EqualTo(new[] { "The votes", "Options" }));
        }

        [Test]
        public void TheCastStripStep_SaysAFaceOpensItsMenu()
        {
            var steps = TourSteps().ToList();
            var strip = steps.SingleOrDefault(step => step.Anchors.Contains("CastRail.StripName"));
            Assert.That(strip.Body, Is.Not.Null, "The tour's step on the cast strip, lit by CastRail.StripName.");
            Assert.That(strip.Body, Does.Not.Contain("follow them with the camera"), "A click on a face opens its menu; following is one of its rows.");
            Assert.That(strip.Body, Does.Contain("open their menu"));
            foreach (var row in new[] { "talk", "profile", "deals", "follow" })
                Assert.That(strip.Body, Does.Contain(row), "The step names the menu's row: " + row + ".");
            string menu = Read("Episode/EpisodeHud.CastMenu.cs");
            foreach (var caption in new[] { "\"Talk to \"", "\"'s profile\"", "\"Deals with \"", "\"Follow \"" })
                Assert.That(menu, Does.Contain(caption), "The cast menu still has the row the tour names: " + caption + ".");
        }

        // ---------------------------------------------------------------- the rail and the tour

        /// <summary>The rail's captions, key hints aside: its first entry, the Notebook heading and rows, and the foot.</summary>
        private static HashSet<string> RailCaptions()
        {
            var match = Regex.Match(Read("Episode/EpisodeHud.Chrome.cs"),
                @"IconRail\.Entry\(\s*IconRail\.Mark\.Overview\s*,\s*EpisodeDirector\.OverviewSection\s*,\s*""([^""]+)""\s*\)");
            Assert.That(match.Success, Is.True, "The rail's first entry, the Overview, in EpisodeHud.Chrome.cs.");
            var captions = new HashSet<string>(StringComparer.Ordinal) { match.Groups[1].Value, NotebookStrip.RailHeading };
            foreach (var row in NotebookStrip.RailRows) captions.Add(WithoutKey(row.Caption));
            foreach (var row in NotebookStrip.FootRows) captions.Add(WithoutKey(row));
            return captions;
        }

        private static string WithoutKey(string caption) => Regex.Replace(caption, @"\s*\[[^\]]*\]\s*$", "");

        /// <summary>
        /// The rail captions a step's words name: "The X of the whole house", the list after
        /// "Notebook:" up to the sentence's end, and the list before "are at its foot". A listed item
        /// that starts in lower case ("your notes") describes a row; one that starts with a capital is
        /// a caption.
        /// </summary>
        private static IEnumerable<string> RailNames(string body)
        {
            foreach (Match overview in Regex.Matches(body, @"\bThe (\w+) of the whole house"))
                yield return overview.Groups[1].Value;
            foreach (Match group in Regex.Matches(body, @"\b" + Regex.Escape(NotebookStrip.RailHeading) + @":\s*([^.]+)\."))
                foreach (var item in Items(group.Groups[1].Value)) yield return item;
            foreach (Match foot in Regex.Matches(body, @"(?:^|\.\s+)([^.]+?) are at its foot"))
                foreach (var item in Items(foot.Groups[1].Value)) yield return item;
        }

        private static IEnumerable<string> Items(string list) =>
            Regex.Split(list, @",\s*|\s+and\s+").Select(item => item.Trim())
                .Where(item => item.Length > 0 && char.IsUpper(item[0]));

        private struct TourStep
        {
            public string Body;
            public List<string> Anchors;
        }

        /// <summary>
        /// The tour's steps as the source writes them: each <c>new Step(id, title, body, anchors...)</c>
        /// in HouseTutorial's Steps, the body as its string literals joined (the constants it adds,
        /// such as the controls line, left out), and each anchor as written.
        /// </summary>
        private static IEnumerable<TourStep> TourSteps()
        {
            string source = Code(Read("Presentation/HouseTutorial.cs"));
            int start = source.IndexOf("Step[] Steps", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThan(0), "HouseTutorial's Steps.");
            int end = source.IndexOf("};", start, StringComparison.Ordinal);
            string table = source.Substring(start, end - start);
            int at = 0, found = 0;
            while ((at = table.IndexOf("new Step(", at, StringComparison.Ordinal)) >= 0)
            {
                at += "new Step(".Length;
                var args = Arguments(table, at);
                found++;
                if (args.Count < 3) continue;
                yield return new TourStep
                {
                    Body = string.Concat(Literals(args[2])),
                    Anchors = args.Skip(3).Select(arg => arg.Trim()).Select(arg => arg.StartsWith("\"", StringComparison.Ordinal) ? string.Concat(Literals(arg)) : arg).ToList(),
                };
            }
            Assert.That(found, Is.GreaterThan(3), "The tour's steps were read.");
        }

        /// <summary>The top-level arguments of the call whose list opens at <paramref name="at"/>.</summary>
        private static List<string> Arguments(string code, int at)
        {
            var args = new List<string>();
            var current = new StringBuilder();
            int depth = 0;
            bool text = false;
            for (int index = at; index < code.Length; index++)
            {
                char c = code[index];
                if (text)
                {
                    current.Append(c);
                    if (c == '\\' && index + 1 < code.Length) { current.Append(code[++index]); continue; }
                    if (c == '"') text = false;
                    continue;
                }
                if (c == '"') { text = true; current.Append(c); continue; }
                if (c == '(' || c == '[' || c == '{') depth++;
                if (c == ')' || c == ']' || c == '}')
                {
                    if (depth == 0) { args.Add(current.ToString()); return args; }
                    depth--;
                }
                if (c == ',' && depth == 0) { args.Add(current.ToString()); current.Clear(); continue; }
                current.Append(c);
            }
            args.Add(current.ToString());
            return args;
        }

        private static IEnumerable<string> Literals(string expression) =>
            Regex.Matches(expression, @"""((?:[^""\\]|\\.)*)""").Cast<Match>().Select(match => Regex.Unescape(match.Groups[1].Value));

        // ---------------------------------------------------------------- reading code

        private static string Read(string relative) => File.ReadAllText(Path.Combine(RuntimeRoot(), relative));

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
