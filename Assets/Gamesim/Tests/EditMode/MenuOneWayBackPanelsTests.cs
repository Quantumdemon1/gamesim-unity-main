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
    /// Menu slice 2, part c, held in the source: the in-season panels and pages have one way out
    /// each, drawn by the shared helper. A panel's Close names no key in its caption - the key is a
    /// hint beside it, and the pad's chip reads the hint; a page reached from another page draws its
    /// "Back to &lt;that page&gt;" with the HUD's WayBack, which Escape and B press before anything
    /// closes; and the panels call one thing by one name: actions left, deals and promises.
    ///
    /// <para>The scans read the source, so they run in the Unity-free suite as well as in EditMode
    /// (the source root from <c>Application.dataPath</c> in the editor, by walking up from the
    /// binary in the subset). The cases on the HUD's own captions need the Unity assemblies and run
    /// in EditMode only.</para>
    /// </summary>
    public sealed class MenuOneWayBackPanelsTests
    {
        private static readonly Regex Literal = new Regex("@?\"(?:[^\"\\\\\\n]|\\\\.)*\"", RegexOptions.CultureInvariant);

        [Test]
        public void ThePanelsKeyHint_IsTheShortcutsBack_AndACloseCaptionNamesNoKey()
        {
            Assert.That(InputGlossary.BackKeyHint, Is.EqualTo("Esc"));
            Assert.That(InputGlossary.HintAction(InputGlossary.BackKeyHint), Is.EqualTo("Shortcuts/Back"), "Escape's hint wears the pad's B.");
            Assert.That(InputGlossary.HintAction(null), Is.Null);
            Assert.That(InputGlossary.HintAction("F5"), Is.Null, "Only the way out's key is a hint.");
            Assert.That(InputGlossary.CaptionAction(WayOut.CloseCaption), Is.Null, "Close's caption is its word alone.");
        }

        [Test]
        public void NoPanel_SaysItsKeyInItsCloseCaption()
        {
            var problems = new List<string>();
            foreach (var file in SourceFiles("Episode").Concat(SourceFiles("Presentation")))
                foreach (var text in Literals(Code(File.ReadAllText(file))))
                    if (text.Contains("Close  [Esc]"))
                        problems.Add(Relative(file) + " says " + text + ": Close is the word; the key is a hint beside it (WayOut.AddClose).");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            string panel = Code(Read("Episode/EpisodeHud.cs"));
            Assert.That(panel, Does.Contain("WayOut.AddClose(modal,director.ClosePanels,FontScale,InputGlossary.BackKeyHint)"),
                "The docked panel's Close is the helper's, with Escape as its hint.");
        }

        [Test]
        public void ThePagesReachedFromAPage_DrawTheirWayBackWithTheHelper()
        {
            var wanted = new[]
            {
                ("Episode/EpisodeHud.Roster.cs", "WayBack(ProfileBackCaption, view.Back)"),
                ("Episode/EpisodeDirector.YourWord.cs", "hud.WayBack(BackToYourNotesCaption"),
                ("Episode/EpisodeDirector.FreeTimeBoard.cs", "hud.WayBack(BackToFreeTimeCaption, CloseFreeTimeBeat)"),
                ("Episode/EpisodeDirector.FreeTimeBoard.cs", "hud.WayBack(BackToFreeTimeCaption, CloseHouseguestScreen)"),
                ("Episode/EpisodeDirector.FreeTimeScreen.cs", "hud.WayBack(BackToFreeTimeCaption, CloseHouseguestScreen)"),
                ("Episode/EpisodeDirector.Story.cs", "hud.WayBack(EpisodeHud.StoryBackCaption"),
            };
            foreach (var (file, call) in wanted)
                Assert.That(Code(Read(file)), Does.Contain(call), file + " draws its way back with the HUD's WayBack: " + call);
            var gone = new[]
            {
                ("Episode/EpisodeHud.Roster.cs", "KitButton(root, ProfileBackCaption"),
                ("Episode/EpisodeDirector.YourWord.cs", "PageDoor(BackToYourNotesCaption"),
                ("Episode/EpisodeDirector.FreeTimeBoard.cs", "PinnedSecondary(BackToFreeTimeCaption"),
                ("Episode/EpisodeDirector.FreeTimeScreen.cs", "hud.Action(BackToFreeTimeCaption"),
                ("Episode/EpisodeDirector.Story.cs", "hud.Action(EpisodeHud.StoryBackCaption"),
                ("Episode/EpisodeDirector.Story.cs", "hud.Action(SceneCardDoneCaption"),
                ("Presentation/WeeklyRecapScreen.cs", "Button(foot, BackCaption"),
            };
            foreach (var (file, call) in gone)
                Assert.That(Code(Read(file)), Does.Not.Contain(call), file + " still draws a way out of its own: " + call);
            Assert.That(Literals(Code(Read("Episode/EpisodeHud.Story.cs"))), Has.None.EqualTo("\"Back\""), "A story step's way back names its place.");
        }

        [Test]
        public void EscapeAndB_GoBackAPage_BeforeTheyCloseThePanel()
        {
            string chain = Code(Read("Episode/EpisodeDirector.Controls.cs"));
            int start = chain.IndexOf("private bool BackOut(", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "The chain Escape, Start and B share.");
            string body = chain.Substring(start);
            int nested = body.IndexOf("BackNestedPage()", StringComparison.Ordinal);
            int recap = body.IndexOf("weeklyRecap.GoBack()", StringComparison.Ordinal);
            int close = body.IndexOf("ClosePanels();", StringComparison.Ordinal);
            Assert.That(nested, Is.GreaterThan(0), "The chain asks the page's way back.");
            Assert.That(recap, Is.GreaterThan(0), "and an earlier week's.");
            Assert.That(close, Is.GreaterThan(nested), "before it closes the panel.");
            Assert.That(chain, Does.Contain("hud.WayBackAction"), "The way back pressed is the one the page drew.");
        }

        [Test]
        public void ThePanels_CallTheWeeklyBudgetActionsLeft_AndDealsDealsAndPromises()
        {
            var problems = new List<string>();
            foreach (var file in new[] { "Episode/EpisodeDirector.DiaryRoom.cs", "Episode/EpisodeHud.cs" })
                foreach (var text in Literals(Code(Read(file))))
                    if (text.IndexOf("social action", StringComparison.OrdinalIgnoreCase) >= 0)
                        problems.Add(file + " says " + text + ": the weekly budget is \"actions left\".");
            foreach (var text in Literals(Code(Read("Episode/EpisodeDirector.YourWord.cs"))))
                if (text == "\"No commitments\"")
                    problems.Add("Episode/EpisodeDirector.YourWord.cs says " + text + ": they are deals and promises.");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void TheScans_FindWhatTheyLookFor()
        {
            Assert.That(Literals(Code("var a = \"Close  [Esc]\"; // \"Close  [Esc]\"")).Count(text => text.Contains("Close  [Esc]")), Is.EqualTo(1),
                "A literal is found; a comment is not.");
            Assert.That(Literals(Code("/* \"No commitments\" */ hud.EmptyState(\"No commitments\", x);")), Is.EqualTo(new[] { "\"No commitments\"" }));
            Assert.That(Code("a // b\nc"), Is.EqualTo("a \nc"));
        }

#if UNITY_5_3_OR_NEWER
        [Test]
        public void TheHudsWaysBack_AreWordedAsWaysBack_AndTheStoryCardHasNoBackToTheHouse()
        {
            foreach (var caption in new[]
            {
                Gamesim.Episode.EpisodeHud.ProfileBackCaption, Gamesim.Episode.EpisodeHud.StoryBackCaption,
                Gamesim.Episode.EpisodeHud.DiaryCancelCaption, Gamesim.Episode.EpisodeHud.StudyCancelCaption,
                Gamesim.Episode.EpisodeHud.DiaryCancelReflectionCaption, Gamesim.Episode.EpisodeDirector.BackToFreeTimeCaption,
                Gamesim.Episode.EpisodeDirector.BackToYourNotesCaption, WeeklyRecapScreen.BackCaption,
                WeeklyRecapScreen.BackToHouseCaption, WeeklyRecapScreen.BackToNotebookCaption,
            })
                Assert.That(WayOut.IsWayBack(caption), Is.True, "'" + caption + "' says where it goes back to.");
            Assert.That(Gamesim.Episode.EpisodeHud.StoryBackCaption, Is.EqualTo("Back to the choices"));
            Assert.That(Gamesim.Episode.EpisodeHud.DiaryCancelCaption, Is.EqualTo("Back to the diary (choice discarded)"),
                "Leaving the review discards the choice, and the way back says so.");
            Assert.That(WeeklyRecapScreen.BackToHouseCaption, Is.EqualTo("Back to the house"));
            Assert.That(WeeklyRecapScreen.BackToNotebookCaption, Is.EqualTo("Back to the notebook"));
            Assert.That(Gamesim.Episode.EpisodeHud.StudyConfirmCaption, Does.Not.Contain("social"), "One name for the weekly budget.");
        }
#endif

        // ---------------------------------------------------------------- reading the source

        private static string Read(string relative) => File.ReadAllText(Path.Combine(RuntimeRoot(), relative));

        private static IEnumerable<string> SourceFiles(string folder) =>
            Directory.GetFiles(Path.Combine(RuntimeRoot(), folder), "*.cs", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal);

        private static string Relative(string path) => path.Substring(RuntimeRoot().Length).TrimStart('\\', '/').Replace('\\', '/');

        private static List<string> Literals(string code) => Literal.Matches(code).Cast<Match>().Select(match => match.Value).ToList();

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
