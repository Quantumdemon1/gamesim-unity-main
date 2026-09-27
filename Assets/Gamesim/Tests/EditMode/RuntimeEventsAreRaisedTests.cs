using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Every event the runtime declares is raised by something.
    ///
    /// <para>This exists because one was not. <c>HousePlayerController.DestinationChosen</c> shipped
    /// declared, subscribed by the director, and never invoked anywhere - so cancelling a walk by
    /// clicking the floor could not work, and nothing said so. An event nobody raises is
    /// indistinguishable from one nobody listens to until something presses the button, and the
    /// PlayMode test that was correctly reporting it was deleted as unreliable.</para>
    ///
    /// <para>A text scan rather than reflection, because the defect is a missing CALL SITE and
    /// reflection can only see the declaration. It is deliberately loose about what counts as a
    /// raise - the local-copy idiom <c>var handler = Thing; handler?.Invoke()</c> is as valid as
    /// <c>Thing?.Invoke()</c> - so it catches the one thing worth catching: an identifier that is
    /// declared and then only ever subscribed to.</para>
    /// </summary>
    public sealed class RuntimeEventsAreRaisedTests
    {
        /// <summary>The declaration, capturing the event's name whatever its type looks like.</summary>
        private static readonly Regex Declaration =
            new Regex(@"\bevent\s+[^;=(){}]+?\s+(\w+)\s*[;=]", RegexOptions.Compiled);

        private static string[] RuntimeFiles()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "Gamesim"));
            return new[] { "Runtime", "Simulation", "Uma" }
                .Select(folder => Path.Combine(root, folder))
                .Where(Directory.Exists)
                .SelectMany(folder => Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
                // The editor tooling and the tests are not the shipped runtime.
                .Where(file => !file.Contains(Path.DirectorySeparatorChar + "Tests" + Path.DirectorySeparatorChar))
                .Where(file => !file.Contains(Path.DirectorySeparatorChar + "Editor" + Path.DirectorySeparatorChar))
                .ToArray();
        }

        [Test]
        public void EveryRuntimeEventHasSomethingThatRaisesIt()
        {
            var files = RuntimeFiles();
            Assert.That(files, Is.Not.Empty, "The scan found no runtime source at all, so it proves nothing.");

            var sources = files.ToDictionary(file => file, File.ReadAllText);
            var declared = new List<(string Name, string File)>();
            foreach (var pair in sources)
                foreach (Match match in Declaration.Matches(pair.Value))
                    declared.Add((match.Groups[1].Value, Path.GetFileName(pair.Key)));

            Assert.That(declared, Is.Not.Empty,
                "No event declarations were found, so either the runtime has none or the pattern is wrong. "
                + "Either way this test is not doing its job.");

            var unraised = new List<string>();
            foreach (var (name, file) in declared.Distinct())
            {
                // Every mention of the identifier that is neither its declaration nor a subscription.
                // Subscribing is += or -=; anything else that names it - Invoke, a bare call, or a
                // copy into a local before invoking - is something raising it.
                var mention = new Regex(@"\b" + Regex.Escape(name) + @"\b\s*(?<op>\+=|-=)?");
                bool raised = sources.Any(pair => mention.Matches(pair.Value).Cast<Match>().Any(hit =>
                    hit.Groups["op"].Success == false
                    && !Declaration.Matches(pair.Value).Cast<Match>().Any(
                        decl => decl.Groups[1].Index == hit.Index)));
                if (!raised) unraised.Add(name + " (declared in " + file + ")");
            }

            Assert.That(unraised, Is.Empty,
                "These events are declared and never raised, so nothing they promise can happen: "
                + string.Join(", ", unraised)
                + ". DestinationChosen shipped exactly like this - declared, subscribed, dead.");
        }
    }
}
