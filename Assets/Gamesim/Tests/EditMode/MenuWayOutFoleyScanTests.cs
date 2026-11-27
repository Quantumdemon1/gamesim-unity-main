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
    /// <para>Every press answers (AU-10): a HUD press that asked for no sound of its own clicks, a
    /// greyed-out control is silent under the pointer, and the main menu, the cast screen, the creator
    /// and the season report tick and answer as the HUD does, with the sounds the director hands them.
    /// Built, in PlayMode: EpisodePlayModeTests.Audio_EveryPressAnswersOnceAndAGreyedControlIsSilent.</para>
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
            Assert.That(Regex.IsMatch(report, @"Action\s+IWayOutHost\.Dress\(\s*Button\s+control\s*,\s*Action\s+action\s*\)\s*=>\s*ControlSounds\.Dress\(\s*control\s*,\s*action\s*\)\s*;"), Is.True,
                "and it sounds as the report's other controls do (AU-10): their tick and their answered press, and no dip.");
        }

        [Test]
        public void TheHud_AnswersAPressThatAskedNoSound_AndIsSilentOverAGreyedControl()
        {
            string all = string.Join("\n", Directory.GetFiles(Path.Combine(RuntimeRoot(), "Episode"), "EpisodeHud*.cs")
                .Select(file => Code(File.ReadAllText(file))));
            var problems = new List<string>();
            const string answer = @"PressAnswer\.Run\(\s*action\s*,\s*CuesAskedSoFar\s*,\s*\(\)\s*=>\s*Foley\(\s*HouseAudio\.Cue\.Button\s*\)\s*\)\s*;";
            const string guard = @"!\s*button\.IsActive\(\)\s*\|\|\s*!\s*button\.IsInteractable\(\)\s*\)\s*return\s*;";
            foreach (string signature in new[] { "private Action PressLike(", "private T PressableAs<T>(" })
            {
                string body = Method(all, signature);
                var guarded = body == null ? null : Regex.Match(body, guard);
                var answered = body == null ? null : Regex.Match(body, answer);
                if (answered == null || !answered.Success)
                    problems.Add(signature + " does not answer a press that asked no sound with the click (PressAnswer.Run over CuesAskedSoFar).");
                else if (guarded == null || !guarded.Success || guarded.Index > answered.Index)
                    problems.Add(signature + " answers before its guard: a greyed-out or retained control would click.");
            }
            string asked = Method(all, "private int CuesAskedSoFar(");
            if (asked == null || !Regex.IsMatch(asked, @"\bfoley\.CuesAsked\b"))
                problems.Add("CuesAskedSoFar does not read the house's count of cues asked for (HouseAudio.CuesAsked).");
            string motion = Code(Read("Presentation/HudMotion.cs"));
            string hudPress = Method(motion, "public sealed class HudPress");
            string enter = hudPress == null ? null : Method(hudPress, "public void OnPointerEnter(");
            if (enter == null || !Regex.IsMatch(enter, @"if\s*\(\s*ControlSounds\.CanPress\(\s*gameObject\s*\)\s*\)\s*Hovered\?\.Invoke\(\)"))
                problems.Add("HudPress ticks over a control that cannot be pressed (ControlSounds.CanPress before Hovered).");
            string sounds = Code(Read("Presentation/ControlSounds.cs"));
            string canPress = Method(sounds, "public static bool CanPress(");
            if (canPress == null || !Regex.IsMatch(canPress, @"selectable\.IsActive\(\)\s*&&\s*selectable\.IsInteractable\(\)"))
                problems.Add("CanPress does not ask the control whether it is shown and not greyed out.");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void TheFrontDoorAndTheReport_TickAndAnswerEveryControl_WithTheHousesSounds()
        {
            var screens = new[]
            {
                ("MainMenu", new[] { "MainMenu.cs" }, false),
                ("CastSelect", new[] { "CastSelect.cs" }, true),
                ("CharacterCreator", Directory.GetFiles(Path.Combine(RuntimeRoot(), "Presentation"), "CharacterCreator*.cs").Select(file => Path.GetFileName(file)).ToArray(), true),
                ("SeasonReport", Directory.GetFiles(Path.Combine(RuntimeRoot(), "Presentation"), "SeasonReport*.cs")
                    .Select(file => Path.GetFileName(file)).Where(name => name != "SeasonReportControl.cs").ToArray(), true),
            };
            var problems = new List<string>();
            var counts = new List<string>();
            foreach (var (screen, files, wayOut) in screens)
            {
                string code = string.Join("\n", files.Select(file => Code(Read("Presentation/" + file))));
                if (!Regex.IsMatch(code, @"class\s+" + screen + @"\s*:[^{]*\bISoundedScreen\b"))
                    problems.Add(screen + " is no ISoundedScreen: the director has nowhere to hand it the house's sounds.");
                if (!Regex.IsMatch(code, @"public\s+ControlSounds\s+Sounds\s*\{\s*get\s*;\s*set\s*;\s*\}"))
                    problems.Add(screen + " keeps no Sounds the director can set.");
                if (wayOut && !Regex.IsMatch(code, @"Action\s+IWayOutHost\.Dress\(\s*Button\s+control\s*,\s*Action\s+action\s*\)\s*=>\s*ControlSounds\.Dress\(\s*control\s*,\s*action\s*\)\s*;"))
                    problems.Add(screen + "'s way out is not dressed with its sounds (IWayOutHost.Dress => ControlSounds.Dress).");
                // Every control the screen makes ticks: by ControlSounds.Tick, or by the creator's Seen, which ticks.
                var made = Regex.Matches(code, @"var\s+button\s*=\s*\w+\.gameObject\.AddComponent<(Button|SeasonReportControl)>\(\)\s*;").Cast<Match>().ToList();
                foreach (var site in made)
                {
                    string rest = BlockFrom(code, site.Index);
                    if (!Regex.IsMatch(rest, @"ControlSounds\.Tick\(\s*button\s*\)\s*;|\bSeen\(\s*button\s*,"))
                        problems.Add(screen + ": a control made at line " + LineOf(code, site.Index) + " has no hover tick.");
                }
                // Every press it runs is answered.
                var presses = Regex.Matches(code, @"onClick\.AddListener\(").Cast<Match>().ToList();
                foreach (var press in presses)
                    if (!Regex.IsMatch(code.Substring(press.Index + press.Length), @"^\s*\(\)\s*=>\s*ControlSounds\.Press\(\s*button\s*,"))
                        problems.Add(screen + ": a press at line " + LineOf(code, press.Index) + " is not answered (ControlSounds.Press(button, ...)).");
                if (made.Count == 0 || presses.Count == 0) problems.Add(screen + ": no control found to scan; the scan has lost its sites.");
                counts.Add(screen + " " + made.Count + "/" + presses.Count);
            }
            string seen = Method(string.Join("\n", Directory.GetFiles(Path.Combine(RuntimeRoot(), "Presentation"), "CharacterCreator*.cs")
                .Select(file => Code(File.ReadAllText(file)))), "private static void Seen(");
            if (seen == null || !Regex.IsMatch(seen, @"ControlSounds\.Tick\(\s*button\s*\)\s*;"))
                problems.Add("The creator's Seen does not tick: its controls are silent under the pointer.");
            string director = Code(Read("Episode/EpisodeDirector.cs"));
            if (!Regex.IsMatch(director, @"var\s+controlSounds\s*=\s*new\s+ControlSounds\(\s*cue\s*=>\s*\{\s*if\s*\(\s*audioBed\s*!=\s*null\s*\)\s*audioBed\.PlayCue\(\s*cue\s*\)\s*;\s*\}\s*,\s*\(\)\s*=>\s*audioBed\s*!=\s*null\s*\?\s*audioBed\.CuesAsked\s*:\s*0\s*\)\s*;"))
                problems.Add("The director makes no control sounds over the house's audio (PlayCue, CuesAsked).");
            foreach (string field in new[] { "mainMenu", "castSelect", "characterCreator", "seasonReport" })
                if (!Regex.IsMatch(director, @"\b" + field + @"\.Sounds\s*=\s*controlSounds\s*;"))
                    problems.Add("The director does not hand " + field + " the house's sounds.");
            Assert.That(problems, Is.Empty, string.Join("\n", problems) + "\n(controls/presses: " + string.Join(", ", counts) + ")");
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

        /// <summary>The rest of the block <paramref name="at"/> stands in, braces matched: up to the brace that closes it.</summary>
        private static string BlockFrom(string code, int at)
        {
            int depth = 0;
            for (int index = at; index < code.Length; index++)
            {
                if (code[index] == '{') depth++;
                else if (code[index] == '}' && --depth < 0) return code.Substring(at, index - at);
            }
            return code.Substring(at);
        }

        private static int LineOf(string code, int at) => code.Take(at).Count(c => c == '\n') + 1;

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
