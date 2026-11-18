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
    /// Menu slice 2's way out, dressed as the screen's other buttons, held in the source. The slice made
    /// the shared helper (WayOut.AddBack, AddClose) the only way a way out is made, and the season's HUD
    /// dressed none of them: the panel's Close, a page's way back and the overview's Close lost the hover
    /// tick, the press dip and the guarded press every HUD button has (Pressable), and the season report's
    /// Close stopped being a report control. The helper now asks the screen it is drawn on (IWayOutHost):
    /// the HUD dresses a way out on its canvas with the same code Pressable uses, and the report makes its
    /// Close a SeasonReportControl. The built Close's tick is checked in PlayMode
    /// (EpisodePlayModeTests.Audio_PanelsAndHoversHaveTheirFoley).
    ///
    /// <para>Unity-free: the editor finds the scripts through <c>Application.dataPath</c>, an offline run
    /// by walking up from its own binary.</para>
    /// </summary>
    public sealed class MenuWayOutFoleyScanTests
    {
        [Test]
        public void TheHelper_HasTheScreenMakeAndDressEveryWayOut()
        {
            string make = Method(Code(Read("Presentation/WayOut.Build.cs")), "private static Button Make(");
            var problems = new List<string>();
            if (!Regex.IsMatch(make, @"\bvar\s+host\s*=\s*HostOf\(\s*frame\s*\)"))
                problems.Add("Make does not ask the frame's screen (HostOf(frame)).");
            if (!Regex.IsMatch(make, @"host\s*!=\s*null\s*\?\s*host\.AddControl\(\s*box\.gameObject\s*\)"))
                problems.Add("Make does not let the screen make the control (host.AddControl).");
            if (!Regex.IsMatch(make, @"\bvar\s+press\s*=\s*host\s*!=\s*null\s*\?\s*host\.Dress\(\s*button\s*,\s*action\s*\)\s*:\s*action\s*;"))
                problems.Add("Make does not let the screen dress the control (host.Dress).");
            var listeners = Regex.Matches(make, @"onClick\.AddListener\(([^;]*)\);").Cast<Match>().Select(match => match.Groups[1].Value).ToList();
            if (listeners.Count != 1 || !Regex.IsMatch(listeners[0], @"^\(\)\s*=>\s*press\(\)$"))
                problems.Add("A way out's press runs what the screen's dressing returned, once: " + string.Join(" | ", listeners));
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void TheHud_DressesItsWaysOutAsItDressesItsButtons()
        {
            var hud = Directory.GetFiles(Path.Combine(RuntimeRoot(), "Episode"), "EpisodeHud*.cs")
                .Select(file => Code(File.ReadAllText(file))).ToList();
            var problems = new List<string>();
            if (!hud.Any(code => Regex.IsMatch(code, @"partial\s+class\s+EpisodeHud\s*:[^{]*\bIWayOutHost\b")))
                problems.Add("EpisodeHud is no IWayOutHost: the helper's ways out on its canvas go undressed.");
            string all = string.Join("\n", hud);
            string dress = Method(all, "Action IWayOutHost.Dress(");
            if (dress == null || !Regex.IsMatch(dress, @"\bPressLike\(\s*control\s*,\s*action\s*\)"))
                problems.Add("The HUD's Dress does not give a way out what every HUD button carries (PressLike).");
            if (dress == null || !Regex.IsMatch(dress, @"IsChildOf\(\s*canvas\.transform\s*\)"))
                problems.Add("The HUD's Dress does not keep to its own canvas: a screen attached beside the HUD would be dressed too.");
            string like = Method(all, "private Action PressLike(");
            if (like == null || !Regex.IsMatch(like, @"AddComponent<HudPress>\(\)"))
                problems.Add("PressLike adds no HudPress: no press dip and no hover.");
            if (like == null || !Regex.IsMatch(like, @"\.Hovered\s*=\s*\(\)\s*=>\s*Foley\(\s*HouseAudio\.Cue\.Hover\s*\)"))
                problems.Add("PressLike does not tick on hover (Foley(HouseAudio.Cue.Hover)).");
            if (like == null || !Regex.IsMatch(like, @"\.ReducedMotion\s*=\s*ReducedMotion\b"))
                problems.Add("PressLike's dip does not follow reduced motion.");
            if (like == null || !Regex.IsMatch(like, @"!\s*button\.IsActive\(\)\s*\|\|\s*!\s*button\.IsInteractable\(\)"))
                problems.Add("PressLike's press is not guarded: a retained or disabled control would act.");
            string pressable = Method(all, "private Button Pressable(");
            if (pressable == null || !Regex.IsMatch(pressable, @"\bPressLike\(\s*button\s*,\s*action\s*\)"))
                problems.Add("Pressable does not use PressLike: the HUD's buttons and its ways out would drift apart.");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void TheSeasonsWaysOut_AreDrawnOnlyByTheHud()
        {
            var problems = new List<string>();
            foreach (var file in Directory.GetFiles(Path.Combine(RuntimeRoot(), "Episode"), "*.cs"))
            {
                string name = Path.GetFileName(file);
                if (name.StartsWith("EpisodeHud", StringComparison.Ordinal)) continue;
                if (Regex.IsMatch(Code(File.ReadAllText(file)), @"\bWayOut\.Add(Back|Close)\("))
                    problems.Add("Episode/" + name + " draws a way out itself: the season's ways out are the HUD's (hud.WayBack, hud.DecisionPageBack), on its canvas, where it dresses them.");
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void TheReportsClose_IsAReportControl()
        {
            string report = Code(Read("Presentation/SeasonReport.cs"));
            Assert.That(Regex.IsMatch(report, @"partial\s+class\s+SeasonReport\s*:[^{]*\bIWayOutHost\b"), Is.True,
                "The season report is no IWayOutHost: its Close would be a plain Button.");
            Assert.That(Regex.IsMatch(report, @"Button\s+IWayOutHost\.AddControl\(\s*GameObject\s+box\s*\)\s*=>\s*box\.AddComponent<SeasonReportControl>\(\)\s*;"), Is.True,
                "The report's Close is a SeasonReportControl, as its footer way was: a right-stick scroll takes no step off it.");
            Assert.That(Regex.IsMatch(report, @"Action\s+IWayOutHost\.Dress\(\s*Button\s+control\s*,\s*Action\s+action\s*\)\s*=>\s*action\s*;"), Is.True,
                "and nothing else is added to it: the report's buttons have no hover tick, dip or guard.");
        }

        // ---------------------------------------------------------------- reading

        /// <summary>The body of the member that starts with <paramref name="signature"/>, braces matched, or null.</summary>
        private static string Method(string code, string signature)
        {
            int at = code.IndexOf(signature, StringComparison.Ordinal);
            if (at < 0) return null;
            int open = code.IndexOf('{', at);
            if (open < 0) return null;
            int depth = 0;
            for (int index = open; index < code.Length; index++)
            {
                if (code[index] == '{') depth++;
                else if (code[index] == '}' && --depth == 0) return code.Substring(open, index - open + 1);
            }
            return null;
        }

        private static string Read(string relative) => File.ReadAllText(Path.Combine(RuntimeRoot(), relative));

        /// <summary>A source with its comments blanked and its line breaks kept; strings are kept as they are.</summary>
        private static string Code(string source)
        {
            var code = new StringBuilder(source.Length);
            bool block = false, line = false, text = false;
            for (var index = 0; index < source.Length; index++)
            {
                char c = source[index];
                char next = index + 1 < source.Length ? source[index + 1] : '\0';
                if (line)
                {
                    if (c == '\n') { line = false; code.Append(c); }
                    continue;
                }
                if (block)
                {
                    if (c == '*' && next == '/') { block = false; index++; }
                    else if (c == '\n') code.Append(c);
                    continue;
                }
                if (text)
                {
                    code.Append(c);
                    if (c == '\\') { if (index + 1 < source.Length) code.Append(source[++index]); }
                    else if (c == '"' || c == '\n') text = false;
                    continue;
                }
                if (c == '/' && next == '/') { line = true; index++; continue; }
                if (c == '/' && next == '*') { block = true; index++; continue; }
                if (c == '"') text = true;
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
            throw new DirectoryNotFoundException("Assets/Gamesim/Runtime is not above " + AppContext.BaseDirectory);
#endif
        }
    }
}
