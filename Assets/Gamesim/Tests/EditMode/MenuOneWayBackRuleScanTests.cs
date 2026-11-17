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
    /// Menu slice 2's rule, held in the screens' sources so a screen added or changed later keeps it
    /// (the review's changes 1, 3 and 4): every way out is made by the shared helper
    /// (Presentation/WayOut.cs) - "Back to &lt;place&gt;" at the top left, "Close" at the top right -
    /// and is spelled only by it; none of the old exit wordings ("Back", "Done", "Close  [Esc]",
    /// "Return to briefing", ...) is a caption; and the WORDS table's old names for one thing
    /// ("Customize", "social actions", "No commitments", ...) are gone from what the screens say.
    ///
    /// <para>The scans read string literals with comments blanked, in every file under
    /// Assets/Gamesim/Runtime/Episode and Presentation. Each exemption is one line below with its
    /// reason, and an exemption that no longer matches anything fails too, so the list never
    /// outlives its lines. Unity-free, so the subset runs it on every push; the editor finds the
    /// scripts through <c>Application.dataPath</c>, the subset by walking up from its own binary.</para>
    /// </summary>
    public sealed class MenuOneWayBackRuleScanTests
    {
        private const string Spelled = "spelled", Drawn = "drawn", ExitWord = "exit word", OldName = "old name";

        private sealed class Exemption
        {
            public readonly string File, Rule, Text, Reason;
            public Exemption(string file, string rule, string text, string reason) { File = file; Rule = rule; Text = text; Reason = reason; }
            public override string ToString() => File + " [" + Rule + "] " + Text + " (" + Reason + ")";
        }

        /// <summary>
        /// The lines the rule lets stand, one each. For the literal rules the text is the literal as
        /// written; for the drawing rule it is "&lt;the call that draws it&gt;:&lt;the caption's name&gt;".
        /// </summary>
        private static readonly Exemption[] Exemptions =
        {
            new Exemption("Presentation/InputGlossary.cs", Spelled, "\"Back to your houseguest\"", "the camera's Recenter row says what the key does; it is no way out"),
            new Exemption("Episode/EpisodeDirector.DiaryRoom.cs", Drawn, "PinnedPair:DiaryCancelCaption", "the diary review's discard back stays in the pinned Confirm pair, the owner's layout contract (part c; the lead's call)"),
            new Exemption("Episode/EpisodeDirector.DiaryRoom.cs", Drawn, "Action:DiaryCancelCaption", "the same discard back, in the diary's unpinned review (part c; the lead's call)"),
            new Exemption("Episode/EpisodeDirector.DiaryRoom.cs", Drawn, "Action:DiaryCancelReflectionCaption", "the same discard back, for a reflection (part c; the lead's call)"),
            new Exemption("Episode/EpisodeDirector.DiaryRoom.cs", Drawn, "Action:StudyCancelCaption", "the same discard back, for a study (part c; the lead's call)"),
            new Exemption("Episode/EpisodeDirector.DiaryRoom.cs", Drawn, "Action:BackToNomineesCaption", "the pitches in the diary's own column keep their row: the diary's head holds the corner (a later slice)"),
            new Exemption("Presentation/CompetitionGameScreen.Chrome.cs", Drawn, "Button:BackToBriefingCaption", "the game screen's way back stays in its controls card (part d, ruling 7)"),
            new Exemption("Presentation/CompetitionGameScreen.Assembly.cs", Drawn, "Button:BackToBriefingCaption", "the assembly card's way back stays in its card (part d, ruling 7)"),
            new Exemption("Presentation/CompetitionGameScreen.cs", Drawn, "ShowOverlay:BackToBriefingCaption", "the ranked attempt's ask names the control it means by its caption"),
            new Exemption("Episode/EpisodeDirector.Challenge.cs", Drawn, "ShowFinished:BackToBriefingCaption", "the finished practice's one button, on the competition screen's own card"),
            new Exemption("Presentation/CompetitionResult.cs", Drawn, ".text:BackToStandingsCaption", "the results' details toggle reads its way back while the details show"),
            new Exemption("Episode/EpisodeHud.Chrome.cs", ExitWord, "\"Done\"", "an objective row's state, not a control"),
            new Exemption("Presentation/CompetitionApparatus.Show.cs", ExitWord, "\"Done\"", "a competitor's status word on the show board"),
            new Exemption("Presentation/CreatorSteps.cs", ExitWord, "\"Done\"", "a creator step's mark word (done, needs attention, not visited)"),
            new Exemption("Presentation/CompetitionGameScreen.Chrome.cs", ExitWord, "\"X\"", "the pad's X button, named in the undo hint"),
            new Exemption("Presentation/InputGlossary.cs", ExitWord, "\"X\"", "the pad's X button in the controls list"),
            new Exemption("Presentation/InputGlossary.cs", ExitWord, "\"Back\"", "the shortcut action named Back, which rebinding keys by name"),
            new Exemption("Presentation/InputGlossary.cs", ExitWord, "\"[Esc]\"", "the old key text the glyph swap still recognises; never drawn"),
            new Exemption("Presentation/InputGlossary.cs", ExitWord, "\"Leave the board; in a ranked attempt the first press asks\"", "the controls list's line for Back on a competition board"),
            new Exemption("Presentation/KeyBindings.cs", ExitWord, "\"Cancel\"", "the input system's UI Cancel action, by name"),
            new Exemption("Presentation/CharacterCreator.Studio.cs", ExitWord, "\"Return to edited look\"", "the compare toggle's other state, not a way out"),
            new Exemption("Presentation/CharacterCreator.cs", OldName, "\"Edit appearance\"", "the cast screen's start-from-this-look chip; one entry into the creator is the review's change 8, a later slice"),
            new Exemption("Presentation/CharacterCreator.Studio.cs", OldName, "\"Customize your look. Your appearance is independent of pronouns, personality and competition stats.\"", "the Appearance step's sentence, not a name for editing a houseguest"),
            new Exemption("Presentation/WeeklyRecapScreen.cs", OldName, "\"Deals and promises\"", "an object's name for tests, never drawn"),
        };

        /// <summary>The helper itself, which alone spells and draws a way out.</summary>
        private static readonly string[] HelperFiles = { "Presentation/WayOut.cs", "Presentation/WayOut.Build.cs" };

        /// <summary>Exit words that are never a caption of their own: the one way out says where it goes.</summary>
        private static readonly string[] OldExitWords = { "Back", "Done", "Return", "Leave", "X", "×", "Exit", "Cancel", "Close" };

        /// <summary>Old exit wordings, anywhere in a literal.</summary>
        private static readonly Regex OldExitWording = new Regex(
            @"\[Esc\]|^""Return to |^""Leave the |^""Close your |Ask something else|Return to briefing|Cancel . keep this season",
            RegexOptions.CultureInvariant);

        /// <summary>The old names the slice's WORDS table (W1 to W11) retired, as they were written on screen.</summary>
        private static readonly Regex OldNames = new Regex(
            @"Create your own houseguest|Create new houseguest|Confirm new houseguest|\bCustomi[sz]e\b|Edit appearance|\b(Search|Previous|Next) profiles\b|social actions?\b|No commitments|Back to the menu|Deals and promises",
            RegexOptions.CultureInvariant);

        /// <summary>The calls a way-out caption may be passed to: the helper's, its measures, and reading it back.</summary>
        private static readonly HashSet<string> HelperCalls = new HashSet<string>(StringComparer.Ordinal)
        {
            "AddBack", "AddClose", "WayBack", "DecisionPageBack", "Width", "BackTo", "BackToKeeping", "IsWayBack",
            "Text", "CaptionOf", "Equals", "Contains", "StartsWith", "Find",
        };

        private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "if", "while", "for", "foreach", "switch", "return", "nameof", "typeof", "lock", "using", "catch", "when",
        };

        private static readonly Regex Literal = new Regex("@?\"(?:[^\"\\\\\\n]|\\\\.)*\"", RegexOptions.CultureInvariant);
        private static readonly Regex CaptionDeclaration = new Regex(
            @"\b(\w+)\s*(?:=>|=)\s*(?:WayOut\.(?:BackPrefix\b|BackTo\(|BackToKeeping\(|CloseCaption\b)|FrontDoorWords\.CastBack\()",
            RegexOptions.CultureInvariant);
        private static readonly Regex TextAssignment = new Regex(@"\.text\s*=(?!=)([^;]*)", RegexOptions.CultureInvariant);

        [Test]
        public void TheWayBack_IsSpelledOnlyByTheHelper()
        {
            var problems = Screens(includeVerify: true).SelectMany(file => SpellingProblems(file.Key, file.Value, null)).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void EveryWayOut_IsDrawnByTheHelper()
        {
            var screens = Screens(includeVerify: false);
            var ids = CaptionIds(screens.Values);
            Assert.That(ids, Is.SupersetOf(new[] { "BackToFreeTimeCaption", "ProfileBackCaption", "BackToBriefingCaption", "BackCaption" }),
                "The scan must keep finding the screens' way-back captions.");
            var problems = screens.SelectMany(file => DrawingProblems(file.Key, file.Value, ids, null)).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void TheOldExitWordings_AreNoCaptions()
        {
            var problems = Screens(includeVerify: false).SelectMany(file => ExitWordProblems(file.Key, file.Value, null)).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void TheWordsTablesOldNames_AreGone()
        {
            var problems = Screens(includeVerify: false).SelectMany(file => OldNameProblems(file.Key, file.Value, null)).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void EveryExemption_StillHasItsLine()
        {
            var used = new HashSet<Exemption>();
            var withVerify = Screens(includeVerify: true);
            var screens = Screens(includeVerify: false);
            var ids = CaptionIds(screens.Values);
            foreach (var file in withVerify) SpellingProblems(file.Key, file.Value, used).ToList();
            foreach (var file in screens)
            {
                DrawingProblems(file.Key, file.Value, ids, used).ToList();
                ExitWordProblems(file.Key, file.Value, used).ToList();
                OldNameProblems(file.Key, file.Value, used).ToList();
            }
            var stale = Exemptions.Where(exemption => !used.Contains(exemption)).Select(exemption => exemption.ToString()).ToList();
            Assert.That(stale, Is.Empty, "An exemption with no line left to exempt: remove it.\n" + string.Join("\n", stale));
            Assert.That(Exemptions.All(exemption => exemption.Reason.Length > 10), Is.True, "Every exemption gives its reason.");
        }

        [Test]
        public void TheScans_FindWhatTheyForbid_AndTheExemptionsPass()
        {
            Assert.That(SpellingProblems("Episode/A.cs", Code("var a = \"Back to the pool\"; // \"Back to the deck\""), null).ToList(),
                Has.Count.EqualTo(1), "A way back spelled by hand, and none in a comment.");
            Assert.That(SpellingProblems("Episode/A.cs", Code("const string A = WayOut.BackPrefix + \"the pool\";"), null), Is.Empty,
                "The helper's prefix spells it.");
            Assert.That(SpellingProblems("Presentation/InputGlossary.cs", Code("Row(\"Back to your houseguest\");"), null), Is.Empty,
                "An exemption passes in its own file,");
            Assert.That(SpellingProblems("Presentation/Other.cs", Code("Row(\"Back to your houseguest\");"), null).ToList(), Has.Count.EqualTo(1),
                "and nowhere else.");

            string drawing = Code("public const string BackToPoolCaption = WayOut.BackPrefix + \"the pool\";\n"
                + "void F() { hud.Action(BackToPoolCaption, Go); hud.WayBack(BackToPoolCaption, Go);\n"
                + "  label.text = flag ? BackToPoolCaption : \"Pool\"; var w = WayOut.Width(BackToPoolCaption, 1f, false);\n"
                + "  Pill(row, WayOut.CloseCaption, Go); WayOut.AddClose(frame, Go); bool same = caption == BackToPoolCaption; }");
            var ids = CaptionIds(new[] { drawing });
            Assert.That(ids, Does.Contain("BackToPoolCaption"));
            var drawn = DrawingProblems("Episode/A.cs", drawing, ids, null).ToList();
            Assert.That(drawn, Has.Count.EqualTo(3), "A row, a hand-set label and a pill draw a way out the helper did not:\n" + string.Join("\n", drawn));
            Assert.That(DrawingProblems("Episode/EpisodeDirector.DiaryRoom.cs", Code("hud.Action(EpisodeHud.StudyCancelCaption, go);"),
                new HashSet<string> { "StudyCancelCaption" }, null), Is.Empty, "An exempt drawing passes in its own file.");

            Assert.That(ExitWordProblems("Episode/A.cs", Code("hud.Action(\"Done\", go); hud.Action(\"Close  [Esc]\", go); // \"Back\""), null).ToList(),
                Has.Count.EqualTo(2), "Two old exit wordings on screen; none in a comment.");
            Assert.That(ExitWordProblems("Presentation/CreatorSteps.cs", Code("public const string DoneWord = \"Done\";"), null), Is.Empty);
            Assert.That(ExitWordProblems("Presentation/WayOut.cs", Code("public const string CloseCaption = \"Close\";"), null), Is.Empty,
                "The helper's own Close.");

            Assert.That(OldNameProblems("Presentation/CastSelect.cs", Code("Chip(row, \"Customize \" + first, go); Note(\"No commitments yet\");"), null).ToList(),
                Has.Count.EqualTo(2), "Two old names.");
            Assert.That(OldNameProblems("Presentation/CastSelect.cs", Code("Chip(row, \"Edit \" + first, go);"), null), Is.Empty);
        }

        // ---------------------------------------------------------------- the rules

        private static IEnumerable<string> SpellingProblems(string file, string code, HashSet<Exemption> used)
        {
            if (HelperFiles.Contains(file)) yield break;
            foreach (var (text, line) in LiteralsWithLines(code))
                if (text.TrimStart('@').StartsWith("\"Back to", StringComparison.Ordinal) && !Exempt(file, Spelled, text.TrimStart('@'), used))
                    yield return file + ":" + line + " spells " + text + " by hand: a way back is WayOut.BackTo or WayOut.BackPrefix + its place.";
        }

        /// <summary>The names the screens give a way-out caption: declared from the helper's words.</summary>
        private static HashSet<string> CaptionIds(IEnumerable<string> codes)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal) { "CloseCaption" };
            foreach (var code in codes)
                foreach (Match match in CaptionDeclaration.Matches(code)) ids.Add(match.Groups[1].Value);
            return ids;
        }

        private static IEnumerable<string> DrawingProblems(string file, string code, HashSet<string> ids, HashSet<Exemption> used)
        {
            if (HelperFiles.Contains(file)) yield break;
            string blank = BlankLiterals(code);
            var found = new List<(string call, string id, int at)>();
            if (ids.Count > 0)
            {
                var name = new Regex(@"(?<![\w])(?:" + string.Join("|", ids.Select(Regex.Escape)) + @")\b", RegexOptions.CultureInvariant);
                foreach (Match match in name.Matches(blank))
                {
                    if (IsDeclaration(blank, match.Index + match.Length)) continue;
                    string call = EnclosingCall(blank, match.Index);
                    if (call != null && !HelperCalls.Contains(call)) found.Add((call, match.Value, match.Index));
                }
                foreach (Match assignment in TextAssignment.Matches(blank))
                    foreach (Match id in name.Matches(assignment.Groups[1].Value))
                        found.Add((".text", id.Value, assignment.Index));
            }
            foreach (var (call, id, at) in found)
                if (!Exempt(file, Drawn, call + ":" + id, used))
                    yield return file + ":" + LineOf(code, at) + " draws " + id + " with " + call + "(: a way out is drawn by WayOut.AddBack or AddClose (or the HUD's WayBack and DecisionPageBack).";
        }

        private static IEnumerable<string> ExitWordProblems(string file, string code, HashSet<Exemption> used)
        {
            if (HelperFiles.Contains(file)) yield break;
            foreach (var (literal, line) in LiteralsWithLines(code))
            {
                string text = literal.TrimStart('@');
                bool word = OldExitWords.Any(exit => text == "\"" + exit + "\"");
                if ((word || OldExitWording.IsMatch(text)) && !Exempt(file, ExitWord, text, used))
                    yield return file + ":" + line + " says " + text + ": an old exit wording; the one way out is \"Back to <place>\" or \"Close\", from the helper.";
            }
        }

        private static IEnumerable<string> OldNameProblems(string file, string code, HashSet<Exemption> used)
        {
            foreach (var (literal, line) in LiteralsWithLines(code))
            {
                string text = literal.TrimStart('@');
                if (OldNames.IsMatch(text) && !Exempt(file, OldName, text, used))
                    yield return file + ":" + line + " says " + text + ": an old name from the WORDS table (one name per thing).";
            }
        }

        private static bool Exempt(string file, string rule, string text, HashSet<Exemption> used)
        {
            var exemption = Exemptions.FirstOrDefault(entry => entry.File == file && entry.Rule == rule && entry.Text == text);
            if (exemption == null) return false;
            used?.Add(exemption);
            return true;
        }

        // ---------------------------------------------------------------- reading code

        /// <summary>Whether the name just read is the one being declared ("Name =" or "Name =&gt;", never "==").</summary>
        private static bool IsDeclaration(string code, int after)
        {
            int index = after;
            while (index < code.Length && (code[index] == ' ' || code[index] == '\t')) index++;
            if (index >= code.Length || code[index] != '=') return false;
            return index + 1 >= code.Length || code[index + 1] != '=';
        }

        /// <summary>
        /// The name of the call whose argument list holds <paramref name="at"/>, or null when it stands
        /// in no call of its statement. Literals are blanked first, so their brackets never count.
        /// </summary>
        private static string EnclosingCall(string blank, int at)
        {
            int depth = 0;
            for (int index = at - 1; index >= 0; index--)
            {
                char c = blank[index];
                if (c == ')' || c == ']') depth++;
                else if (c == '[') { if (depth == 0) return null; depth--; }
                else if (c == '(')
                {
                    if (depth > 0) { depth--; continue; }
                    int end = index - 1;
                    while (end >= 0 && char.IsWhiteSpace(blank[end])) end--;
                    int start = end;
                    while (start >= 0 && (char.IsLetterOrDigit(blank[start]) || blank[start] == '_')) start--;
                    string call = end > start ? blank.Substring(start + 1, end - start) : null;
                    // A condition, a loop's head or nameof is no call that draws.
                    return call == null || Keywords.Contains(call) ? null : call;
                }
                else if (depth == 0 && (c == ';' || c == '{' || c == '}')) return null;
            }
            return null;
        }

        private static string BlankLiterals(string code) =>
            Literal.Replace(code, match => new string(' ', match.Length - 1).Insert(0, "\"").Substring(0, match.Length - 1) + "\"");

        private static IEnumerable<(string text, int line)> LiteralsWithLines(string code)
        {
            foreach (Match match in Literal.Matches(code)) yield return (match.Value, LineOf(code, match.Index));
        }

        private static int LineOf(string code, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < code.Length; i++) if (code[i] == '\n') line++;
            return line;
        }

        /// <summary>Every screen source under Episode and Presentation, by its path under Runtime, with its comments blanked.</summary>
        private static Dictionary<string, string> Screens(bool includeVerify)
        {
            string root = RuntimeRoot();
            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var folder in new[] { "Episode", "Presentation" })
                foreach (var path in Directory.GetFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
                {
                    string file = path.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
                    // The verify walk clicks the screens' captions and logs what it did; it draws none.
                    if (!includeVerify && file.StartsWith("Episode/PortVerification", StringComparison.Ordinal)) continue;
                    files[file] = Code(File.ReadAllText(path));
                }
            Assert.That(files.Keys, Is.SupersetOf(new[] { "Presentation/CastSelect.cs", "Episode/EpisodeHud.cs", "Presentation/CharacterCreator.Chrome.cs" }),
                "The scan must keep finding the screens.");
            return files;
        }

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
