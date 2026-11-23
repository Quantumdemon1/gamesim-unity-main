using System;
using System.Collections.Generic;
using System.IO;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// A ceremony card opens in the words of the device the house last heard (UI-UX plan U5): a
    /// controller player's first takeover card says "A  Close", the first veto draw names A and the
    /// first key ceremony drops its click line, before any press on the card itself. A card's first
    /// frames ignore input, so a card that only listened to presses made on it would name the
    /// keyboard until the very press that closes it. The built cards are checked in PlayMode
    /// (CeremonyCardPressPlayModeTests).
    ///
    /// <para>Unity-free, so the subset runs it too; the editor finds the scripts through
    /// <c>Application.dataPath</c>, the subset by walking up from its own binary.</para>
    /// </summary>
    public sealed class CeremonyCardDeviceTests
    {
        private static readonly string[] Cards = { "CeremonyTakeover.cs", "VetoDrawReveal.cs", "KeyCeremony.cs", "VoteReveal.cs", "JuryReveal.cs" };

        [Test]
        public void ACardOpensInTheDeviceTheHouseLastHeard()
        {
            Assert.That(CeremonyCardWords.StartsOnPad(() => true, false), Is.True, "A card handed the house's device opens in the pad's words,");
            Assert.That(CeremonyCardWords.StartsOnPad(() => false, true), Is.False, "or the keyboard's, whatever it last saw itself.");
            Assert.That(CeremonyCardWords.StartsOnPad(null, true), Is.True, "A card nobody handed the device keeps the one it last saw,");
            Assert.That(CeremonyCardWords.StartsOnPad(null, false), Is.False, "and a new one starts on the keyboard.");

            var missing = new List<string>();
            foreach (var card in Cards)
            {
                string source = Read("Presentation/" + card);
                if (!source.Contains("public System.Func<bool> PadHints")) missing.Add(card + ": no PadHints");
                // Every Play opens on the house's device, before its first line is worded.
                int plays = Count(source, "public bool Play(") + Count(source, "public void Play(string kind, int week, IList<Subject> subjects, bool reducedMotion, string titleText")
                    + Count(source, "public bool PlayVetoMeeting(");
                int asks = Count(source, "usingPad = CeremonyCardWords.StartsOnPad(PadHints, usingPad);");
                if (plays == 0 || asks != plays) missing.Add(card + ": " + plays + " Play, " + asks + " asking the device");
            }

            string director = Read("Episode/EpisodeDirector.CeremonySpeed.cs") + Read("Episode/EpisodeDirector.VetoDraw.cs");
            foreach (var card in new[] { "keyCeremony", "voteReveal", "juryReveal", "takeover", "drawReveal" })
                if (!director.Contains(card + ".PadHints = CardsOnPad;")) missing.Add("the director hands " + card + " no device");
            if (!director.Contains("private bool CardsOnPad() => padHints;")) missing.Add("the director's device is not its hints' device");
            Assert.That(missing, Is.Empty, "Every card opens in the words of the device the house last heard.");
        }

        private static int Count(string source, string text)
        {
            int count = 0;
            for (int at = source.IndexOf(text, StringComparison.Ordinal); at >= 0; at = source.IndexOf(text, at + 1, StringComparison.Ordinal)) count++;
            return count;
        }

        private static string Read(string relative) => File.ReadAllText(Path.Combine(RuntimeRoot(), relative));

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
