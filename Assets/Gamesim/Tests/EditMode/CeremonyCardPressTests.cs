using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The ceremony cards name the press that works, stay up long enough to read, and remember Speed
    /// up (UI-UX plan U5 and D2): the words in each device's terms, the card's hold for its line, the
    /// veto draw's numbers on CeremonyPacing with a Quick column beside the ones it always had, the
    /// session's speed, and - held in the source - the director handing that speed to every reveal
    /// from a field of its own. The built cards are checked in PlayMode (CeremonyCardPressPlayModeTests).
    ///
    /// <para>Unity-free, so the subset runs it too; the editor finds the scripts through
    /// <c>Application.dataPath</c>, the subset by walking up from its own binary.</para>
    /// </summary>
    public sealed class CeremonyCardPressTests
    {
        [Test]
        public void TheCardsNameThePressInTheDevicesWords()
        {
            Assert.That(CeremonyCardWords.CloseFor(false), Is.EqualTo("Click or Enter  Close"), "A keyboard closes a card with a click or Enter,");
            Assert.That(CeremonyCardWords.CloseFor(true), Is.EqualTo("A  Close"), "a pad with A.");
            Assert.That(CeremonyCardWords.SkipToResultFor(false), Is.EqualTo("Click or Enter  Skip to result"), "The veto draw says what the press does,");
            Assert.That(CeremonyCardWords.SkipToResultFor(true), Is.EqualTo("A  Skip to result"), "on a pad too.");
            Assert.That(CeremonyCardWords.ShowsClickLine(false), Is.True, "The key ceremony keeps its click line on a keyboard,");
            Assert.That(CeremonyCardWords.ShowsClickLine(true), Is.False, "and drops it on a pad, which has no click.");
        }

        [Test]
        public void AShortCardHoldsAsItAlwaysDid()
        {
            Assert.That(CeremonyPacing.CardAuthoredHold, Is.EqualTo(2.6f), "The card's own hold is the one it always had.");
            Assert.That(CeremonyPacing.CardHold("Veto", string.Empty, CaptionTime.Standard), Is.EqualTo(2.6f).Within(1e-5f),
                "A line read in under 2.6 seconds holds for 2.6.");
        }

        [Test]
        public void ALongLineStaysUpLongEnoughToRead()
        {
            const string title = "Nomination Ceremony", line = "The keys go up on the memory wall. Two will not get one.";
            float hold = CeremonyPacing.CardHold(title, line, CaptionTime.Standard);
            Assert.That(hold, Is.EqualTo(1.2f + 0.3f * 15).Within(1e-4f), "Fifteen words: 1.2 seconds and 0.3 a word.");
            Assert.That(hold, Is.GreaterThan(CeremonyPacing.CardAuthoredHold), "Longer than the 2.6 seconds the card used to give it,");
            Assert.That(CeremonyPacing.CardHold(title, line, CaptionTime.Longer), Is.EqualTo(hold * 1.5f).Within(1e-4f),
                "and half as long again under the Longer setting.");
            Assert.That(CeremonyPacing.CardHold(null, null, CaptionTime.Standard), Is.EqualTo(2.6f).Within(1e-5f), "Nothing to read holds the authored time.");
        }

        [Test]
        public void TheVetoDrawKeepsItsNumbersAndGainsAQuickColumn()
        {
            var suspense = CeremonyPace.Suspenseful;
            Assert.That(CeremonyPacing.DrawIntro(suspense), Is.EqualTo(0.70f), "The beat with every chip in the bag,");
            Assert.That(CeremonyPacing.PerChip(suspense), Is.EqualTo(0.90f), "each chip,");
            Assert.That(CeremonyPacing.ChipTurn(suspense), Is.EqualTo(0.30f), "a chip's turn,");
            Assert.That(CeremonyPacing.FieldHold(suspense), Is.EqualTo(2.6f), "and the field: the numbers the draw always had.");
            Assert.That(CeremonyPacing.VetoDraw(suspense, 3), Is.EqualTo(0.30f + 0.70f + 3 * 0.90f + 2.6f + 0.45f).Within(1e-4f),
                "Three chips run fade to fade as before.");

            var quick = CeremonyPace.Quick;
            Assert.That(CeremonyPacing.DrawIntro(quick), Is.LessThan(CeremonyPacing.DrawIntro(suspense)));
            Assert.That(CeremonyPacing.PerChip(quick), Is.LessThan(CeremonyPacing.PerChip(suspense)));
            Assert.That(CeremonyPacing.ChipTurn(quick), Is.LessThan(CeremonyPacing.ChipTurn(suspense)));
            Assert.That(CeremonyPacing.FieldHold(quick), Is.LessThan(CeremonyPacing.FieldHold(suspense)));
            Assert.That(CeremonyPacing.ChipTurn(quick) * 2f, Is.LessThanOrEqualTo(CeremonyPacing.PerChip(quick)),
                "A quick chip still leaves the bag and turns over before the next comes out.");
            Assert.That(CeremonyPacing.VetoDraw(quick, 0), Is.GreaterThan(0f));
        }

        [Test]
        public void ASpedUpRevealKeepsTheNextOnesFast()
        {
            Assert.That(CeremonyPacing.SessionSpeed(1f), Is.EqualTo(1f), "A session nobody sped up plays at the reveals' own pace.");
            Assert.That(CeremonyPacing.SessionSpeed(CeremonyPacing.SpeedUp), Is.EqualTo(CeremonyPacing.SpeedUp), "Sped up stays sped up,");
            Assert.That(CeremonyPacing.SessionSpeed(0f), Is.EqualTo(1f), "and nothing slower than the own pace is kept.");
            Assert.That(CeremonyPacing.SessionSpeed(2f), Is.EqualTo(CeremonyPacing.SpeedUp), "Any speed-up is the speed-up.");
        }

        /// <summary>Every reveal the director plays, by the call that plays it, relative to Assets/Gamesim/Runtime.</summary>
        private static readonly string[][] Reveals =
        {
            new[] { "Episode/EpisodeDirector.cs", "keyCeremony.Play(" },
            new[] { "Episode/EpisodeDirector.cs", "voteReveal.Play(" },
            new[] { "Episode/EpisodeDirector.cs", "juryReveal.Play(" },
            new[] { "Episode/EpisodeDirector.Finale.cs", "juryReveal.Play(" },
            new[] { "Episode/EpisodeDirector.VetoDraw.cs", "drawReveal.Play(" },
            new[] { "Episode/EpisodeDirector.CeremonyStageVeto.cs", "takeover.PlayVetoMeeting(" },
        };

        [Test]
        public void TheDirectorHandsItsSessionSpeedToEveryReveal()
        {
            var missing = new List<string>();
            foreach (var reveal in Reveals)
            {
                var calls = Calls(Read(reveal[0]), reveal[1]).ToList();
                if (calls.Count == 0) missing.Add(reveal[0] + ": no " + reveal[1]);
                foreach (var call in calls)
                    if (!call.Contains("ceremonySpeed")) missing.Add(reveal[0] + ": " + reveal[1] + " without ceremonySpeed");
            }
            Assert.That(missing, Is.Empty, "Every reveal starts at the speed the player left the last one at.");

            string field = Read("Episode/EpisodeDirector.CeremonySpeed.cs");
            Assert.That(field, Does.Contain("private float ceremonySpeed"), "The speed is the director's own field,");
            Assert.That(field.Split('\n').Where(line => line.Contains("ceremonySpeed") && line.Contains("static")), Is.Empty,
                "never a static, which would outlive the play session.");
            foreach (var card in new[] { "KeyCeremony.cs", "VoteReveal.cs", "JuryReveal.cs", "VetoDrawReveal.cs", "CeremonyTakeover.cs" })
                Assert.That(Read("Presentation/" + card), Does.Contain("SpeedChanged?.Invoke("), card + " tells the director when the player speeds it up.");
        }

        /// <summary>Each call of <paramref name="call"/> in <paramref name="source"/>: its argument list, to the parenthesis that closes it.</summary>
        private static IEnumerable<string> Calls(string source, string call)
        {
            for (int at = source.IndexOf(call, StringComparison.Ordinal); at >= 0; at = source.IndexOf(call, at + 1, StringComparison.Ordinal))
            {
                int depth = 0, index = at + call.Length - 1;
                var args = new StringBuilder();
                for (; index < source.Length; index++)
                {
                    char c = source[index];
                    if (c == '(') depth++;
                    else if (c == ')' && --depth == 0) break;
                    args.Append(c);
                }
                yield return args.ToString();
            }
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
