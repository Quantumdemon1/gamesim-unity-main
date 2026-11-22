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
    /// The slogans stay gone (the owner, 2026-11-14 for the main menu and the creator, 2026-11-21 for
    /// the season's screens and the house's signs: "Remove it from screens and signs"). No string the
    /// game's code holds, no text in a scene and no sign the scenes or the set-piece builder place
    /// carries a slogan's words.
    ///
    /// <para>Unity-free, so the subset runs it too; the editor finds the folder through
    /// <c>Application.dataPath</c>, the subset by walking up from its own binary. Comments are blanked
    /// before the code is read, so a note saying why a line went is not a line on a screen.</para>
    /// </summary>
    public sealed class SloganScanTests
    {
        /// <summary>
        /// The slogans whole, as words only: lower case, punctuation and line breaks read as single
        /// spaces, so "GOOD PEOPLE · BIGGER STORIES" and "Good People\nBigger Stories" both match.
        /// Whole slogans, never a two-word piece of one, so an everyday line such as "they're good
        /// people" stays free to be written (the review, finding 3).
        /// </summary>
        private static readonly string[] SloganWords =
        {
            "same house different stories", "good people bigger stories", "good people better stories",
            "real players bigger possibilities", "last competition last decision",
        };

        /// <summary>The neon lettering models that spell a slogan, which no scene and no builder may place.</summary>
        private static readonly string[] SloganSigns = { "bb_set_sign_samehouse", "bb_set_sign_goodpeople" };

        /// <summary>The code whose strings reach a screen or a sign, relative to Assets/Gamesim.</summary>
        private static readonly string[] CodeFolders = { "Runtime", "Simulation", "Editor" };

        [Test]
        public void NoScreenAndNoSignCarriesASlogan()
        {
            string root = GamesimRoot();
            var problems = new List<string>();

            foreach (var folder in CodeFolders)
                foreach (var file in Directory.GetFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
                {
                    string code = Code(File.ReadAllText(file));
                    foreach (var slogan in SlogansIn(code))
                        problems.Add(Relative(root, file) + " holds the slogan words \"" + slogan + "\".");
                    foreach (var sign in SloganSigns.Where(code.Contains))
                        problems.Add(Relative(root, file) + " names the slogan sign " + sign + ".");
                }

            var signGuids = SloganSigns
                .Select(sign => Directory.GetFiles(root, sign + ".fbx.meta", SearchOption.AllDirectories).FirstOrDefault())
                .Where(meta => meta != null)
                .Select(meta => File.ReadAllLines(meta).Select(line => line.Trim())
                    .FirstOrDefault(line => line.StartsWith("guid:", StringComparison.Ordinal)))
                .Where(line => line != null)
                .Select(line => line.Substring("guid:".Length).Trim())
                .ToArray();
            Assert.That(signGuids, Is.Not.Empty, "The slogan signs' models are found by their metas, so a placed copy can be told by its guid.");

            var scenes = Directory.GetFiles(root, "*.unity", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(root, "*.prefab", SearchOption.AllDirectories)).ToArray();
            Assert.That(scenes.Any(scene => Path.GetFileName(scene) == "EpisodeHouse.unity"), Is.True, "The house scene is read.");
            foreach (var scene in scenes)
            {
                int number = 0;
                foreach (var line in File.ReadLines(scene))
                {
                    number++;
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("m_text:", StringComparison.Ordinal) || trimmed.StartsWith("m_Text:", StringComparison.Ordinal)
                        || trimmed.StartsWith("value:", StringComparison.Ordinal))
                    {
                        foreach (var slogan in SlogansIn(trimmed))
                            problems.Add(Relative(root, scene) + ":" + number + " shows the slogan words \"" + slogan + "\".");
                    }
                    foreach (var sign in SloganSigns.Where(line.Contains))
                        problems.Add(Relative(root, scene) + ":" + number + " places the slogan sign " + sign + ".");
                    foreach (var guid in signGuids.Where(line.Contains))
                        problems.Add(Relative(root, scene) + ":" + number + " places a slogan sign's model (guid " + guid + ").");
                }
            }

            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        /// <summary>
        /// The matcher alone, on lines a screen could show: every way the slogans were written is
        /// caught, and the same words in an everyday line are not.
        /// </summary>
        [Test]
        public void TheScanCatchesTheSlogansAndNotEverydayWords()
        {
            Assert.That(SlogansIn("strap.text = \"SAME HOUSE. DIFFERENT STORIES.\";"), Is.EqualTo(new[] { "same house different stories" }));
            Assert.That(SlogansIn("neon.text = \"Good People\\nBigger Stories\";"), Is.EqualTo(new[] { "good people bigger stories" }));
            Assert.That(SlogansIn("Text(column, \"GOOD PEOPLE  \\u00b7  BIGGER STORIES\", 15f);"), Is.EqualTo(new[] { "good people bigger stories" }));
            Assert.That(SlogansIn("  m_text: Good people, better stories"), Is.EqualTo(new[] { "good people better stories" }));
            Assert.That(SlogansIn("FixedText(card, \"Last competition. Last decision.\", 11);"), Is.EqualTo(new[] { "last competition last decision" }));
            Assert.That(SlogansIn("Say(\"They're good people, and the house has different stories to tell.\");"), Is.Empty);
            Assert.That(SlogansIn("Say(\"Same house, same rules, bigger stakes.\");"), Is.Empty);
        }

        /// <summary>The slogans a text holds, read as words: lower case, escapes and punctuation as spaces.</summary>
        private static string[] SlogansIn(string text)
        {
            string words = Normal(text);
            return SloganWords.Where(slogan => words.Contains(" " + slogan + " ")).ToArray();
        }

        private static readonly Regex Escapes = new Regex(@"\\(u[0-9A-Fa-f]{4}|x[0-9A-Fa-f]{1,4}|[ntr])", RegexOptions.CultureInvariant);
        private static readonly Regex NotWords = new Regex(@"[^\p{L}\p{Nd}]+", RegexOptions.CultureInvariant);

        /// <summary>Lower case words with single spaces between them and one at each end; a string's escapes and every mark read as a space.</summary>
        private static string Normal(string text) =>
            " " + NotWords.Replace(Escapes.Replace(text, " "), " ").Trim().ToLowerInvariant() + " ";

        private static string Relative(string root, string file) =>
            file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');

        /// <summary>A source with its comments blanked and its line breaks kept: line comments and block comments both, never inside a string.</summary>
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
                    // A char literal, '"' among them, is copied whole so it never opens a string.
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

        private static string GamesimRoot()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "Gamesim"));
#else
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "Gamesim");
                if (Directory.Exists(Path.Combine(candidate, "Runtime"))) return Path.GetFullPath(candidate);
            }
            Assert.Fail("Assets/Gamesim was not found above " + AppContext.BaseDirectory);
            return null;
#endif
        }
    }
}
