using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Menu slice 2 on the character creator (part a), in plain C#: the step row without My
    /// Houseguests, numbered from 1; the done, needs-attention and not-visited marks; the first thing
    /// missing and the link to where it is put right; the replace card's words; one name per thing;
    /// and the creator's sources held to them. The built screen is pinned in
    /// MenuOneWayBackCreatorPlayModeTests. Unity-free, so the subset runs it on every push.
    /// </summary>
    public sealed class MenuOneWayBackCreatorTests
    {
        // ---------------------------------------------------------------- A1, A2: the steps

        [Test]
        public void TheStepRowsAreTheRealStepsWithoutTheLibrary()
        {
            Assert.That(CreatorSteps.Detailed, Is.EqualTo(new[] { "Appearance", "Identity", "Personality", "Review" }));
            Assert.That(CreatorSteps.Quick, Is.EqualTo(new[] { "Quick setup", "Review" }));
            Assert.That(CreatorSteps.Detailed, Has.No.Member(CreatorSteps.Library), "My Houseguests is a header button, not a step.");
            Assert.That(CreatorSteps.Quick, Has.No.Member(CreatorSteps.Library));
        }

        [Test]
        public void EachModesStepsAreNumberedFromOne()
        {
            Assert.That(CreatorSteps.Number(CreatorSteps.Appearance, false), Is.EqualTo(1));
            Assert.That(CreatorSteps.Number(CreatorSteps.Review, false), Is.EqualTo(4));
            Assert.That(CreatorSteps.Number(CreatorSteps.QuickSetup, true), Is.EqualTo(1));
            Assert.That(CreatorSteps.Number(CreatorSteps.Review, true), Is.EqualTo(2));
            Assert.That(CreatorSteps.Number(CreatorSteps.Library, false), Is.EqualTo(0), "The library has no number.");
        }

        [Test]
        public void AStepsMarkFollowsWhatWasSeenAndWhatFails()
        {
            Assert.That(CreatorSteps.MarkOf(visited: false, passed: false, fails: false), Is.EqualTo(CreatorSteps.Mark.NotVisited));
            Assert.That(CreatorSteps.MarkOf(visited: true, passed: false, fails: false), Is.EqualTo(CreatorSteps.Mark.Done));
            Assert.That(CreatorSteps.MarkOf(visited: true, passed: true, fails: false), Is.EqualTo(CreatorSteps.Mark.Done));
            Assert.That(CreatorSteps.MarkOf(visited: true, passed: false, fails: true), Is.EqualTo(CreatorSteps.Mark.NeedsAttention));
            Assert.That(CreatorSteps.MarkOf(visited: false, passed: true, fails: true), Is.EqualTo(CreatorSteps.Mark.NeedsAttention),
                "Passed by going on, with something missing.");
            Assert.That(CreatorSteps.MarkOf(visited: false, passed: true, fails: false), Is.EqualTo(CreatorSteps.Mark.NotVisited),
                "Passed but never seen is not done.");
            Assert.That(CreatorSteps.MarkOf(visited: false, passed: false, fails: true), Is.EqualTo(CreatorSteps.Mark.NotVisited),
                "Nothing is flagged on a step the player has not reached.");
        }

        [Test]
        public void TheThreeMarksHaveTheirOwnWords()
        {
            var words = new[] { CreatorSteps.Mark.Done, CreatorSteps.Mark.NeedsAttention, CreatorSteps.Mark.NotVisited }.Select(CreatorSteps.Word).ToArray();
            Assert.That(words, Is.EqualTo(new[] { "Done", "Needs attention", "Not visited yet" }));
            Assert.That(words, Is.Unique);
        }

        // ---------------------------------------------------------------- A3: the warning is a link

        [Test]
        public void TheFirstThingMissingIsTheNameAndTheLinkSaysWhereItGoes()
        {
            var draft = Named(null);
            var first = CreatorSteps.First(draft, quick: false);
            Assert.That(first, Is.Not.Null);
            Assert.That(CreatorSteps.LinkCaption(first), Is.EqualTo("Name needed - go to Identity"));
            Assert.That(first.Field, Is.EqualTo("Name field"), "The cursor goes in the name.");
            Assert.That(CreatorSteps.LinkCaption(CreatorSteps.First(draft, quick: true)), Is.EqualTo("Name needed - go to Quick setup"),
                "Quick setup asks for the name on its own page.");
        }

        [Test]
        public void OnlyTheFirstThingMissingIsShown()
        {
            var draft = Named(null);
            draft.Bio = new string('b', CharacterDraft.BioLimit + 1);
            var problems = CreatorSteps.Problems(draft, quick: false);
            Assert.That(problems.Select(problem => problem.Words), Is.EqualTo(new[] { "Name needed", "Shorter bio needed" }));
            Assert.That(CreatorSteps.First(draft, false).Words, Is.EqualTo("Name needed"), "In the order the draft's own check finds them.");
        }

        [Test]
        public void WhatIsMissingAgreesWithTheDraftsOwnCheck()
        {
            var cases = new Dictionary<string, CharacterDraft>
            {
                ["ready"] = Named("Robin"),
                ["no name"] = Named("  "),
                ["long name"] = Named(new string('n', CharacterDraft.NameLimit + 1)),
                ["young"] = With(Named("Robin"), draft => draft.Age = CharacterDraft.MinimumAge - 1),
                ["long occupation"] = With(Named("Robin"), draft => draft.Occupation = new string('o', CharacterDraft.ShortLimit + 1)),
                ["long hometown"] = With(Named("Robin"), draft => draft.Hometown = new string('h', CharacterDraft.ShortLimit + 1)),
                ["long bio"] = With(Named("Robin"), draft => draft.Bio = new string('b', CharacterDraft.BioLimit + 1)),
                ["three traits"] = With(Named("Robin"), draft => { draft.Traits.Add("Loyal"); draft.Traits.Add("Loyal"); draft.Traits.Add("Loyal"); }),
                ["unknown trait"] = With(Named("Robin"), draft => draft.Traits.Add("Not a trait")),
                ["bad stat"] = With(Named("Robin"), draft => draft.Stats = null),
            };
            foreach (var pair in cases)
            {
                bool valid = pair.Value.TryValidate(out var error);
                Assert.That(CreatorSteps.Problems(pair.Value, false).Count == 0, Is.EqualTo(valid), pair.Key + " (" + error + ")");
                Assert.That(CreatorSteps.Problems(pair.Value, true).Count == 0, Is.EqualTo(valid), pair.Key + " in Quick setup");
            }
        }

        [Test]
        public void EachProblemBelongsToTheStepThatPutsItRight()
        {
            var bio = With(Named("Robin"), draft => draft.Bio = new string('b', CharacterDraft.BioLimit + 1));
            Assert.That(CreatorSteps.Fails(bio, CreatorSteps.Identity, false), Is.True);
            Assert.That(CreatorSteps.Fails(bio, CreatorSteps.Appearance, false), Is.False);
            Assert.That(CreatorSteps.Fails(bio, CreatorSteps.Personality, false), Is.False);
            Assert.That(CreatorSteps.Fails(bio, CreatorSteps.Review, false), Is.True, "Review answers for the whole draft.");
            var traits = With(Named("Robin"), draft => draft.Traits.Add("Not a trait"));
            Assert.That(CreatorSteps.First(traits, false).Step, Is.EqualTo(CreatorSteps.Personality));
            Assert.That(CreatorSteps.First(traits, true).Step, Is.EqualTo(CreatorSteps.Personality),
                "Quick setup has no traits: the link leads to the detailed step that has them.");
            Assert.That(CreatorSteps.Fails(Named("Robin"), CreatorSteps.Review, false), Is.False);
        }

        // ---------------------------------------------------------------- A2: the replace card

        [Test]
        public void TheReplaceCardAsksInPlainWords()
        {
            Assert.That(CreatorSteps.ReplaceQuestion("Robin"), Is.EqualTo("Replace your draft with Robin?"));
            Assert.That(CreatorSteps.ReplaceQuestion(null), Is.EqualTo("Replace your draft with a new houseguest?"));
            Assert.That(CreatorSteps.ReplaceCaption, Is.EqualTo("Replace my draft"));
            Assert.That(CreatorSteps.KeepCaption, Is.EqualTo("Keep my draft"));
            Assert.That(WayOut.IsWayBack(CreatorSteps.KeepCaption), Is.False, "An answer, not a way back.");
        }

        // ---------------------------------------------------------------- A6: one name per thing

        [Test]
        public void MakingAndChangingAHouseguestHaveOneNameEach()
        {
            Assert.That(CreatorSteps.CreateCaption, Is.EqualTo("Create a houseguest"));
            Assert.That(CreatorSteps.EditCaption("Robin "), Is.EqualTo("Edit Robin"));
            Assert.That(CreatorSteps.EditCaption(" "), Is.EqualTo("Edit houseguest"), "With no name at hand.");
            Assert.That(CreatorSteps.Title(null), Is.EqualTo("CREATE A HOUSEGUEST"));
            Assert.That(CreatorSteps.Title("Robin"), Is.EqualTo("EDIT ROBIN"));
            Assert.That(CreatorSteps.Title(string.Empty), Is.EqualTo("EDIT HOUSEGUEST"));
        }

        [Test]
        public void TheLibrarysMessagesSayHouseguestNotProfile()
        {
            var store = new[]
            {
                "Invalid houseguest profile identifier or version.", "The houseguest profile is incomplete.",
                "The existing profile is unreadable. Save a new copy to preserve it.", "The houseguest exceeds the profile size limit.",
                "Recovered the previous valid profile in memory. The original file was preserved.", "Houseguest profile not found.",
                "Unsupported houseguest profile format.", "The profile file has a different identity.",
            };
            foreach (string message in store)
            {
                string shown = CreatorSteps.LibraryWords(message);
                Assert.That(shown.IndexOf("profile", StringComparison.OrdinalIgnoreCase), Is.EqualTo(-1), shown);
            }
            Assert.That(CreatorSteps.LibraryWords("Invalid houseguest profile identifier."), Is.EqualTo("Invalid saved houseguest identifier."));
            Assert.That(CreatorSteps.LibraryWords("The houseguest exceeds the profile size limit."), Is.EqualTo("The houseguest exceeds the size limit."));
            Assert.That(CreatorSteps.LibraryWords("Saved Robin."), Is.EqualTo("Saved Robin."), "A message without the word is left alone.");
        }

        // ---------------------------------------------------------------- the creator's sources

        private static readonly string[] CreatorFiles =
        {
            "Presentation/CharacterCreator.cs", "Presentation/CharacterCreator.Chrome.cs", "Presentation/CharacterCreator.Entry.cs",
            "Presentation/CharacterCreator.Identity.cs", "Presentation/CharacterCreator.Library.cs", "Presentation/CharacterCreator.Personality.cs",
            "Presentation/CharacterCreator.Steps.cs", "Presentation/CharacterCreator.Studio.cs", "Presentation/CharacterProfileBrowser.cs",
        };

        /// <summary>The old names a player met in the creator, each with the one name it became.</summary>
        private static readonly string[] OldWords =
        {
            "Create new houseguest", "Confirm new houseguest", "Create your own houseguest", "EDIT CAST HOUSEGUEST", "CREATE YOUR",
            "Search profiles", "Previous profiles", "Next profiles", "Back to the cast with this edit",
        };

        [Test]
        public void TheCreatorSaysEachThingByItsOneName()
        {
            var problems = new List<string>();
            foreach (string file in CreatorFiles)
                foreach (string literal in Literals(Code(Read(file))))
                    problems.AddRange(OldWords.Where(word => literal.Contains(word))
                        .Select(word => file + " still says " + literal));
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void TheCreatorsWayBackIsDrawnByTheSharedHelperAndTheFooterHasNone()
        {
            string chrome = Code(Read("Presentation/CharacterCreator.Chrome.cs"));
            Assert.That(chrome, Does.Contain("WayOut.AddBack("), "The way back is the helper's.");
            Assert.That(Regex.IsMatch(chrome, @"Pill\(footer,[^;]*BackCaption"), Is.False, "No way back in the footer.");
            string library = Code(Read("Presentation/CharacterCreator.Library.cs"));
            Assert.That(library, Does.Contain("WayOut.AddClose("), "The library panel's Close is the helper's.");
        }

        [Test]
        public void OnlyTheFootersWayOnIsStyledAsTheMainAction()
        {
            var bright = new List<string>();
            foreach (string file in CreatorFiles)
            {
                string code = Code(Read(file));
                foreach (Match match in Regex.Matches(code, @"Tone\.Primary\b"))
                {
                    string line = code.Substring(code.LastIndexOf('\n', match.Index) + 1);
                    line = line.Substring(0, line.IndexOf('\n') < 0 ? line.Length : line.IndexOf('\n')).Trim();
                    if (line.StartsWith("case Tone.Primary", StringComparison.Ordinal) || line.Contains("tone == Tone.Primary")) continue;
                    bright.Add(file + ": " + line);
                }
            }
            Assert.That(bright, Has.Count.EqualTo(2), string.Join("\n", bright));
            Assert.That(bright.All(line => line.StartsWith("Presentation/CharacterCreator.Chrome.cs", StringComparison.Ordinal)), Is.True,
                "Only the footer's next step and its last step's start: " + string.Join("\n", bright));
        }

        [Test]
        public void TheScansFindWhatTheyForbid()
        {
            Assert.That(Literals(Code("Pill(box, \"Create new houseguest\", r); // \"Search profiles\" in a comment")),
                Is.EqualTo(new[] { "\"Create new houseguest\"" }));
            Assert.That(Regex.IsMatch(Code("Pill(footer, editingCastSlot ? CancelSlotCaption : BackCaption, r);"), @"Pill\(footer,[^;]*BackCaption"), Is.True);
        }

        // ---------------------------------------------------------------- helpers

        private static CharacterDraft Named(string name)
        {
            var draft = CharacterDraft.Blank();
            draft.Name = name ?? string.Empty;
            return draft;
        }

        private static CharacterDraft With(CharacterDraft draft, Action<CharacterDraft> change) { change(draft); return draft; }

        private static readonly Regex Literal = new Regex("@?\"(?:[^\"\\\\\\n]|\\\\.)*\"", RegexOptions.CultureInvariant);

        private static List<string> Literals(string code) => Literal.Matches(code).Cast<Match>().Select(match => match.Value).ToList();

        private static string Read(string relative) => File.ReadAllText(Path.Combine(RuntimeRoot(), relative));

        /// <summary>A source with its line comments and block comments blanked, never inside a string.</summary>
        private static string Code(string source)
        {
            var code = new System.Text.StringBuilder(source.Length);
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
