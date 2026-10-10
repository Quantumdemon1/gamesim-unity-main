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
    /// The claim the Back matrix's ceremony rows rest on (PLAN A, A4f): every ceremony card reads
    /// its presses the same way, so the key ceremony and the vote reveal stand for them all. A card
    /// is any Runtime file that stamps <c>CeremonyOverlays.Showing()</c> - the click guard every card
    /// keeps while it plays - and each one reads a press only through
    /// <c>CeremonyTakeover.SkipPressed()</c> and <c>SpeedPressed()</c>, the Ceremony map's Skip and
    /// Speed, which carry Escape and B alike. None names the house's Menu or Back actions, so no card
    /// can answer Escape and B differently.
    ///
    /// <para>Unity-free, so the subset runs it on every push; the editor finds the scripts through
    /// <c>Application.dataPath</c>, the subset by walking up from its own binary.</para>
    /// </summary>
    public sealed class CeremonyCardInputScanTests
    {
        /// <summary>The cards there are today, relative to Assets/Gamesim/Runtime: the scan must keep finding them.</summary>
        private static readonly string[] KnownCards =
        {
            "Episode/EpisodeDirector.CeremonyStage.cs",
            "Presentation/CeremonyTakeover.cs",
            "Presentation/JuryReveal.cs",
            "Presentation/KeyCeremony.cs",
            "Presentation/VetoDrawReveal.cs",
            "Presentation/VoteReveal.cs",
        };

        /// <summary>The one file whose code may read the Ceremony map itself: where SkipPressed and SpeedPressed are defined.</summary>
        private const string Definitions = "Presentation/CeremonyTakeover.cs";

        private static readonly Regex Stamp = new Regex(@"\bCeremonyOverlays\.Showing\(\)", RegexOptions.CultureInvariant);

        /// <summary>Any read of a press, a held control or a value off an action or a control.</summary>
        private static readonly Regex PressRead = new Regex(
            @"\.(WasPressedThisFrame|WasReleasedThisFrame|WasPerformedThisFrame|WasCompletedThisFrame|IsPressed|IsInProgress|ReadValue\w*|triggered|wasPressedThisFrame|wasReleasedThisFrame|isPressed)\b",
            RegexOptions.CultureInvariant);

        /// <summary>The definitions' own reads: the Ceremony map's Skip and Speed, pressed this frame.</summary>
        private static readonly Regex DefinitionRead = new Regex(
            @"\bHouseInput\.Actions\.(Skip|Speed)\.WasPressedThisFrame\(\)", RegexOptions.CultureInvariant);

        /// <summary>The house's Escape/Start and B, by action or by the director's chain.</summary>
        private static readonly Regex MenuOrBack = new Regex(
            @"\.(Menu|Back)\b|\b(MenuPressed|BackPressed|BackOut)\s*\(", RegexOptions.CultureInvariant);

        /// <summary>Any action read off the actions asset: on a card, only the Ceremony map's Skip and Speed.</summary>
        private static readonly Regex ActionUse = new Regex(@"\bHouseInput\.Actions\.(\w+)", RegexOptions.CultureInvariant);

        [Test]
        public void EveryCeremonyCard_ReadsPressesOnlyThroughSkipAndSpeed()
        {
            string root = RuntimeRoot();
            var cards = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string code = Code(File.ReadAllText(path));
                if (Stamp.IsMatch(code)) cards[Relative(root, path)] = code;
            }

            TestContext.WriteLine("Ceremony cards (files stamping CeremonyOverlays.Showing()): " + string.Join(", ", cards.Keys));
            Assert.That(KnownCards.Where(known => !cards.ContainsKey(known)), Is.Empty,
                "The scan must keep finding every card there is; a card that stopped stamping the guard is a card the house no longer holds the click for.");

            var problems = new List<string>();
            foreach (var card in cards) problems.AddRange(Problems(card.Key, card.Value));
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void TheScan_FindsAReadOtherThanSkipAndSpeed()
        {
            const string obeys = "void Update() { CeremonyOverlays.Showing(); if (CeremonyTakeover.SkipPressed()) Cancel();\n"
                + "if (CeremonyTakeover.SpeedPressed()) Faster(); } // Gamesim.House.HouseInput.Actions.Back.WasPressedThisFrame() in a comment\n"
                + "/* Menu.WasPressedThisFrame() in a block */";
            Assert.That(Problems("Presentation/Card.cs", Code(obeys)), Is.Empty, "Skip and Speed, and nothing in comments.");

            Assert.That(Problems("Presentation/Card.cs", Code("CeremonyOverlays.Showing(); if (HouseInput.Actions.Back.WasPressedThisFrame()) Cancel();")),
                Has.Count.EqualTo(3), "B read directly: a press read, the Back action and an action other than Skip and Speed.");
            Assert.That(Problems("Presentation/Card.cs", Code("CeremonyOverlays.Showing(); if (shortcuts.Menu.WasPressedThisFrame()) Cancel();")),
                Has.Count.EqualTo(2), "Escape through the house's shortcuts.");
            Assert.That(Problems("Episode/EpisodeDirector.Card.cs", Code("CeremonyOverlays.Showing(); if (pressed) BackPressed();")),
                Has.Count.EqualTo(1), "The director's own chain.");
            Assert.That(Problems("Presentation/Card.cs", Code("CeremonyOverlays.Showing(); if (HouseInput.Actions.Skip.WasPressedThisFrame()) Cancel();")),
                Has.Count.EqualTo(1), "Skip itself, read outside the definitions, goes around them.");
            Assert.That(Problems(Definitions, Code("internal static bool SkipPressed() => Gamesim.House.HouseInput.Actions.Skip.WasPressedThisFrame();")),
                Is.Empty, "The definitions read the Ceremony map's Skip.");
            Assert.That(Problems(Definitions, Code("bool held = Gamesim.House.HouseInput.Actions.Advance.IsPressed();")),
                Has.Count.EqualTo(2), "but nothing else, even there.");
        }

        /// <summary>What a card's code does that the claim does not allow, one line each.</summary>
        private static List<string> Problems(string file, string code)
        {
            var problems = new List<string>();
            string reads = file == Definitions ? DefinitionRead.Replace(code, "") : code;
            foreach (Match match in PressRead.Matches(reads))
                problems.Add(file + " reads a press with " + match.Value + " (line " + Line(reads, match.Index)
                    + "): a ceremony card reads only CeremonyTakeover.SkipPressed() and SpeedPressed().");
            foreach (Match match in MenuOrBack.Matches(code))
                problems.Add(file + " names the house's Menu or Back (" + match.Value.Trim() + ", line " + Line(code, match.Index)
                    + "): a card answers Escape and B through the Ceremony map's Skip alone.");
            foreach (Match match in ActionUse.Matches(code))
                if (match.Groups[1].Value != "Skip" && match.Groups[1].Value != "Speed")
                    problems.Add(file + " reads the action " + match.Groups[1].Value + " (line " + Line(code, match.Index) + "): a card reads only Skip and Speed.");
            return problems;
        }

        private static int Line(string code, int index) => code.Take(index).Count(c => c == '\n') + 1;

        /// <summary>A source with its comments blanked and its line breaks kept: line comments and block comments both.</summary>
        private static string Code(string source)
        {
            var code = new StringBuilder(source.Length);
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
