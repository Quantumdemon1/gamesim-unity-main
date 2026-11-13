using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The creator's studio takes the preview's two newer behaviours by one line: it fits the camera to the
    /// posed (deformed) body and publishes a view only once the latest pose has rendered. Both are off by
    /// default, so the cast screen and the portrait queue keep their own studios as they were. Nothing else
    /// fails if a merge puts the creator back on the plain Create(): the framing and recovery fixtures build
    /// their own previews with the options set. This reads the creator's source, so it runs without Unity
    /// (Tools/SimulationTests compiles it too).
    /// </summary>
    public sealed class CharacterCreatorStudioOptInTests
    {
        [Test]
        public void TheCreatorsStudioFitsThePosedBodyAndShowsOnlyRenderedViews()
        {
            string source = File.ReadAllText(Path.Combine(SourceRoot(), "Runtime", "Presentation", "CharacterCreator.Studio.cs"));
            int start = source.IndexOf("private void EnsureStudio()", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "The creator's EnsureStudio was found.");
            int end = source.IndexOf("private ", start + 10, StringComparison.Ordinal);
            string body = source.Substring(start, (end < 0 ? source.Length : end) - start);

            var calls = Regex.Matches(body, @"CharacterStudioPreview\.Create\(([^)]*)\)").Cast<Match>().ToList();
            Assert.That(calls, Has.Count.EqualTo(1), "The creator makes its studio in one place.");
            string arguments = Regex.Replace(calls[0].Groups[1].Value, @"\s+", "");
            Assert.That(arguments, Does.Contain("fitDeformedBody:true"), "The creator frames the posed body, not the bind pose.");
            Assert.That(arguments, Does.Contain("settleRenders:true"), "The creator shows a view only once its latest pose has rendered.");
        }

        /// <summary>Assets/Gamesim, found upward from the working directory (the Unity project) or the test binary (Tools/SimulationTests).</summary>
        private static string SourceRoot()
        {
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // An assembly loaded from bytes has no location, and Path.GetDirectoryName("") throws on Mono.
            string binary = typeof(CharacterCreatorStudioOptInTests).Assembly.Location;
            var starts = new List<string> { Directory.GetCurrentDirectory() };
            if (!string.IsNullOrEmpty(binary)) starts.Add(Path.GetDirectoryName(binary));
            foreach (string from in starts)
            {
                string at = from;
                for (int depth = 0; depth < 12 && !string.IsNullOrEmpty(at); depth++)
                {
                    string candidate = Path.Combine(at, "Assets", "Gamesim");
                    if (File.Exists(Path.Combine(candidate, "Simulation", "SeasonBuilder.cs"))) { found.Add(WithoutLongPathPrefix(Path.GetFullPath(candidate))); break; }
                    string next = Path.GetDirectoryName(at);
                    if (next == at) break;
                    at = next;
                }
            }
            Assert.That(found, Has.Count.EqualTo(1), "Exactly one Assets/Gamesim source tree above the test: " + string.Join(", ", found));
            return found.Single();
        }

        /// <summary>
        /// A path without Windows' extended-length prefix: past about 248 characters the test host gives the binary's
        /// location as \\?\C:\... while the working directory reads C:\..., one tree under two spellings.
        /// </summary>
        private static string WithoutLongPathPrefix(string path) =>
            path.StartsWith(@"\\?\UNC\", StringComparison.Ordinal) ? @"\\" + path.Substring(8)
            : path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path.Substring(4) : path;
    }
}
