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
    /// The menu review's quick wins at the front door and in Settings (part b, items 13 to 17), held
    /// in the source: Settings in plain words under one section heading style, no "slot" on the cast
    /// screen (a slot is a save), "tour" and never "tutorial" on the tour, and one brand mark drawn
    /// from one helper. The built screens are checked in PlayMode (EpisodePlayModeTests.MenuFront.cs);
    /// these read what every screen's words are made of, so a line added later is held too.
    ///
    /// <para>Unity-free, so the subset runs it on every push; the editor finds the scripts through
    /// <c>Application.dataPath</c>, the subset by walking up from its own binary.</para>
    /// </summary>
    public sealed class MenuFrontWordsScanTests
    {
        /// <summary>The Settings and saves screen, relative to Assets/Gamesim/Runtime.</summary>
        private static readonly string[] SettingsFiles =
        {
            "Episode/EpisodeDirector.Season.cs", "Episode/EpisodeDirector.Display.cs", "Episode/EpisodeDirector.Career.cs",
            "Episode/EpisodeDirector.Controls.cs", "Episode/EpisodeDirector.CameraSettings.cs",
        };

        /// <summary>The words Settings used that a player cannot be expected to know.</summary>
        private static readonly string[] DeveloperWords = { "validated", "receipt", "cloud", "AI dialogue", "credentials", "snapshot" };

        /// <summary>The tour's names that are never on screen: its preference key and two objects' names.</summary>
        private static readonly string[] TourInternalNames = { "Gamesim.TutorialSeen", "Tutorial card", "Gamesim Tutorial" };

        private static readonly Regex Literal = new Regex("@?\"(?:[^\"\\\\\\n]|\\\\.)*\"", RegexOptions.CultureInvariant);
        private static readonly Regex Slot = new Regex(@"\bslots?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Tutorial = new Regex("tutorial", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex OtherHeading = new Regex(@"\bhud\.(Section|Heading)\(", RegexOptions.CultureInvariant);

        [Test]
        public void Settings_SpeakPlainly_AndUseTheOneSectionHeading()
        {
            var problems = new List<string>();
            foreach (var file in SettingsFiles)
            {
                string code = Code(Read(file));
                problems.AddRange(WordProblems(file, code, DeveloperWords));
                problems.AddRange(HeadingProblems(file, code));
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void CastScreen_SaysSlotNowhere()
        {
            const string file = "Presentation/CastSelect.cs";
            var problems = Literals(Code(Read(file))).Where(text => Slot.IsMatch(text))
                .Select(text => file + " says " + text + ": a slot is a save; the cast screen says houseguest.").ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void Tour_SaysTourNeverTutorial()
        {
            const string file = "Presentation/HouseTutorial.cs";
            var problems = Literals(Code(Read(file))).Where(text => Tutorial.IsMatch(text)
                    && !TourInternalNames.Any(name => text == "\"" + name + "\""))
                .Select(text => file + " says " + text + ": the word is \"tour\".").ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That(Read(file), Does.Contain("SkipCaption = \"Skip the tour\""));
        }

        [Test]
        public void BrandMark_IsDrawnFromTheOneHelper()
        {
            string root = RuntimeRoot();
            var problems = new List<string>();
            var drawers = new List<string>();
            foreach (var path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string file = Relative(root, path);
                if (file == "Presentation/BrandMark.cs") continue;
                string code = Code(File.ReadAllText(path));
                problems.AddRange(MarkProblems(file, code));
                if (code.Contains("\"Brand mark\"") || code.Contains("BrandEmblemName")) drawers.Add(file);
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That(drawers, Is.SupersetOf(new[]
            {
                "Episode/EpisodeHud.Chrome.cs", "Presentation/CastSelect.cs", "Presentation/CharacterCreator.Chrome.cs",
                "Presentation/OpeningSequence.Cards.cs",
            }), "The scan must keep finding the screens that draw the brand.");
            Assert.That(Read("Presentation/MainMenu.cs"), Does.Contain("BrandMark.Sprite()"), "The front door's mark is the one mark.");
            Assert.That(Read("Presentation/BrandMark.cs"), Does.Contain("public const Kind Current = Kind.House;"),
                "The house, the mark most screens drew, until the owner chooses.");
        }

        [Test]
        public void TheScans_FindWhatTheyForbid()
        {
            Assert.That(WordProblems("A.cs", Code("hud.Action(\"Recover validated backup\", go); // validated in a comment"), DeveloperWords).ToList(),
                Has.Count.EqualTo(1), "A developer word on screen, and none in a comment.");
            Assert.That(HeadingProblems("A.cs", Code("hud.Heading(\"Display\"); hud.Section(\"SOUND\"); hud.SectionHeading(\"CAMERA\");")).ToList(),
                Has.Count.EqualTo(2), "Both other styles; the one style passes.");
            Assert.That(HeadingProblems("Episode/EpisodeDirector.Controls.cs", Code("hud.Heading(context); hud.Section(\"CONTROLS\");")).ToList(),
                Has.Count.EqualTo(1), "Controls keeps its lines' second-level headings; its section takes the one style.");
            Assert.That(Literals(Code("Chip(row, \"Cast slots\", 0f); var slot = 1; // \"Edit slot\"")).Count(text => Slot.IsMatch(text)),
                Is.EqualTo(1), "A slot on screen, not in a name or a comment.");
            Assert.That(MarkProblems("Presentation/Screen.cs", Code("mark.sprite = UiTheme.Pack(PackArt.IconHome);")).ToList(), Has.Count.EqualTo(1));
            Assert.That(MarkProblems("Presentation/Screen.cs", Code("var eye = UiTheme.Icon(\"eye\"); Box(\"Eye emblem\", card);")).ToList(),
                Has.Count.EqualTo(1), "An eye emblem of a card's own.");
            Assert.That(MarkProblems("Presentation/Screen.cs", Code("var mark = BrandMark.Draw(\"Brand mark\", brand, UiTheme.Heading);")), Is.Empty);
            Assert.That(MarkProblems("Presentation/CharacterCreator.Studio.cs", Code(FaceCategoryIcon)), Is.Empty, "The Face category's eye is no brand.");
            Assert.That(MarkProblems("Presentation/CharacterCreator.Studio.cs", Code("mark.sprite = UiTheme.Pack(PackArt.KitIconEye);")).ToList(),
                Has.Count.EqualTo(1), "Any other eye in the creator is still a mark of its own.");
        }

        private static IEnumerable<string> WordProblems(string file, string code, IEnumerable<string> words) =>
            Literals(code).SelectMany(text => words.Where(word => text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(word => file + " says \"" + word + "\" in " + text + ": Settings speak plainly."));

        /// <summary>A section heading in any style but the one; Controls keeps its lines' second-level headings.</summary>
        private static IEnumerable<string> HeadingProblems(string file, string code)
        {
            foreach (Match match in OtherHeading.Matches(code))
            {
                if (file.EndsWith("EpisodeDirector.Controls.cs", StringComparison.Ordinal) && match.Groups[1].Value == "Heading") continue;
                yield return file + " heads a section with hud." + match.Groups[1].Value + "(: Settings use hud.SectionHeading.";
            }
        }

        /// <summary>A mark drawn from the art directly, or a card's own eye emblem.</summary>
        /// <summary>The one line the brand scan lets use the eye: the creator's Face category icon.</summary>
        private const string FaceCategoryIcon = "case \"Face\": return PackArt.KitIconEye;";

        private static IEnumerable<string> MarkProblems(string file, string code)
        {
            // The creator's Face category wears the eye as a category icon (menu quick win 5), not as the brand.
            if (file == "Presentation/CharacterCreator.Studio.cs") code = code.Replace(FaceCategoryIcon, "");
            if (code.Contains("PackArt.IconHome") || code.Contains("PackArt.KitIconEye"))
                yield return file + " draws a mark from the art: the brand is BrandMark's.";
            if (code.Contains("\"Eye emblem\"") && !file.StartsWith("Presentation/OpeningDoor", StringComparison.Ordinal))
                yield return file + " draws an eye emblem: the title card shows the one brand mark.";
        }

        private static List<string> Literals(string code) => Literal.Matches(code).Cast<Match>().Select(match => match.Value).ToList();

        private static string Read(string relative) => File.ReadAllText(Path.Combine(RuntimeRoot(), relative));

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
