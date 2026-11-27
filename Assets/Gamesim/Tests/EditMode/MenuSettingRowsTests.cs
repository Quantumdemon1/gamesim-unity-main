using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Menu slice 4, part b: the Sound, Display, Accessibility and Controls rows as names and values
    /// (the review's change 2, ruling S5). Each setting's name says what it is and its value how it
    /// stands; no row is worded as an action and no setting keeps plus and minus buttons; each keeps
    /// the preference key it always had; a value changed from outside its row is drawn again; and left
    /// and right wait while a rebinding listens. Unity-free, so the subset runs it on every push; the
    /// built rows are checked in PlayMode (EpisodePlayModeTests.MenuSettingRows.cs).
    /// </summary>
    public sealed class MenuSettingRowsTests
    {
        /// <summary>The captions the rows replaced: each said what a press would do, not what the setting is.</summary>
        private static readonly string[] OldCaptions =
        {
            "Volume down", "Volume up", "Mute sound", "Turn sound on", "Turn music off", "Turn music on",
            "Use compact HUD", "Show full HUD", "Turn VSync off", "Turn VSync on", "Play in a window", "Play full screen",
            "Reduce character motion", "Enable character motion", "Reduce sound", "Full sound",
            "Use larger text", "Use standard text", "Use largest text", "Stop the screen edges panning", "Let the screen edges pan",
            "Invert camera tilt", "Restore camera tilt", "Make ceremonies quick", "Make ceremonies suspenseful",
        };

        /// <summary>The old cycling captions' openings: "Quality: Full  (change)" and the like.</summary>
        private static readonly string[] OldCyclePrefixes =
        {
            "Quality: ", "Frame rate: ", "Display mode: ", "Resolution: ", "Monitor: ", "House view: ", "Language: ", "Camera speed: ", "Caption time: ",
        };

        // ---------------------------------------------------------------- the words

        [Test]
        public void Names_SayWhatTheSettingIs_NeverWhatAPressDoes_AndAreEachOne()
        {
            var problems = new List<string>();
            foreach (var name in SettingRowWords.Rows)
            {
                foreach (var word in SettingRowWords.ActionWords)
                    if (name.StartsWith(word, StringComparison.Ordinal)) problems.Add("\"" + name + "\" begins with \"" + word + "\": a name says what the setting is.");
                if (OldCaptions.Contains(name)) problems.Add("\"" + name + "\" is an old caption.");
                // A test, a screen reader and the verify find a control by its caption: no section or card row may share one.
                if (SettingsWords.Sections.Select(SettingsWords.Name).Contains(name)) problems.Add("\"" + name + "\" is also a section's name.");
                if (new[] { SettingsWords.ResumeCaption, SettingsWords.SaveCaption, SettingsWords.SettingsCaption, SettingsWords.MainMenuCaption }.Contains(name))
                    problems.Add("\"" + name + "\" is also a game menu row.");
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That(SettingRowWords.Rows.Distinct().Count(), Is.EqualTo(SettingRowWords.Rows.Count), "Each name names one setting.");
            Assert.That(SettingRowWords.Rows, Has.No.Member(SoundPictures.MeterCaption(SoundChannel.Music)), "The music's switch and its volume are two rows.");
            // The checks above find what they are for.
            Assert.That(SettingRowWords.ActionWords.Any(word => "Turn music off".StartsWith(word, StringComparison.Ordinal)), Is.True);
            Assert.That(SettingRowWords.ActionWords.Any(word => "Reduced motion".StartsWith(word, StringComparison.Ordinal)), Is.False,
                "\"Reduced\" says how the setting stands; \"Reduce \" would be a press.");
        }

        [Test]
        public void Values_ReadHowEachSettingStands()
        {
            Assert.That(SettingRowWords.CeremonyPaceValues, Is.EqualTo(new[] { "Suspenseful", "Quick" }));
            Assert.That(SettingRowWords.CeremonyPaceAt(0), Is.EqualTo(CeremonyPace.Suspenseful), "The default pace first.");
            Assert.That(SettingRowWords.CeremonyPaceAt(1), Is.EqualTo(CeremonyPace.Quick));
            Assert.That(SettingRowWords.HouseViewValues, Is.EqualTo(new[] { "One room", "Whole house" }), "The house view's words, as before.");
            Assert.That(SettingRowWords.CameraTiltValues, Is.EqualTo(new[] { "Normal", "Inverted" }));
            Assert.That(SettingRowWords.MonitorValue(2, 3), Is.EqualTo("2 of 3"));
            Assert.That(SettingsWords.SwitchValue(true), Is.EqualTo("On"), "A switch reads On or Off.");
            Assert.That(SettingsWords.Description(SettingRowWords.MusicRow, SettingsWords.SwitchValue(false)), Is.EqualTo("Music: Off"),
                "A screen reader hears the name and the value together.");
            Assert.That(DisplayChoices.FrameLimits.Select(DisplayChoices.FrameLimitName), Is.EqualTo(new[] { "30 fps", "60 fps", "120 fps", "Uncapped" }),
                "The frame rate row's values, the words its caption used to carry.");
        }

        [Test]
        public void TheTour_SaysWhyItCannotBeReplayed_WhereItCannot()
        {
            Assert.That(SettingRowWords.TourWhyNot(false, false, false), Is.Null, "In the house it can.");
            Assert.That(SettingRowWords.TourWhyNot(true, false, false), Is.EqualTo("Only from Settings in the house."));
            Assert.That(SettingRowWords.TourWhyNot(false, true, false), Is.EqualTo("Not while a save needs recovering."));
            Assert.That(SettingRowWords.TourWhyNot(false, false, true), Is.EqualTo("Not during the opening."));
            Assert.That(SettingRowWords.OneQualityLevelLine, Is.EqualTo("Only one quality level is available here."));
        }

        [Test]
        public void EverySetting_KeepsTheKeyItWasStoredUnder()
        {
            // S1: nothing new is stored; each row writes the key and the values the old control wrote.
            Assert.That(TextSizes.SizeKey, Is.EqualTo("Gamesim.TextSize"));
            Assert.That(TextSizes.LegacyKey, Is.EqualTo("Gamesim.LargeText"));
            Assert.That(CaptionTiming.Key, Is.EqualTo("Gamesim.CaptionTime"));
            Assert.That(new[] { SoundPictures.MusicVolumeKey, SoundPictures.EffectsVolumeKey, SoundPictures.InterfaceVolumeKey },
                Is.EqualTo(new[] { "Gamesim.MusicVolume", "Gamesim.EffectsVolume", "Gamesim.InterfaceVolume" }));
            Assert.That(new[] { DisplayChoices.DisplayModeKey, DisplayChoices.ResolutionKey, DisplayChoices.VSyncKey, DisplayChoices.FrameCapKey },
                Is.EqualTo(new[] { "Gamesim.DisplayMode", "Gamesim.Resolution", "Gamesim.VSync", "Gamesim.FrameCap" }));
            string kept = Code(Read("Episode/EpisodeDirector.cs")) + Code(Read("Episode/EpisodeDirector.Season.cs"))
                + Code(Read("Episode/EpisodeDirector.Display.cs")) + Code(Read("Episode/EpisodeDirector.CameraSettings.cs"));
            foreach (var key in new[] { "Gamesim.Muted", "Gamesim.Music", "Gamesim.Volume", "Gamesim.ReducedMotion", "Gamesim.ReducedAudio",
                "Gamesim.CeremonyPace", "Gamesim.Quality", "Gamesim.EdgePan", "Gamesim.CompactHud", "Gamesim.Language", "Gamesim.CameraSpeed", "Gamesim.InvertY" })
                Assert.That(kept, Does.Contain("\"" + key + "\""), key + " is still read and written.");
        }

        [Test]
        public void TheConversationLinesRow_WaitsByDefault_AndIsKeptUnderItsOwnKey()
        {
            // UI plan D11, lead ruling L15: a row of Accessibility, after the caption time, that waits for a press unless the player chose otherwise.
            Assert.That(SettingRowWords.ConversationLinesRow, Is.EqualTo("Conversation lines"));
            Assert.That(SettingRowWords.ConversationLinesValues, Is.EqualTo(new[] { "Wait for me", "Move on" }), "Waiting for a press first: the default.");
            var rows = SettingRowWords.Rows.ToList();
            Assert.That(rows.IndexOf(SettingRowWords.ConversationLinesRow), Is.EqualTo(rows.IndexOf(SettingRowWords.CaptionTimeRow) + 1),
                "Drawn under the caption time, the other row about reading time.");
            Assert.That(ConversationLinePace.Key, Is.EqualTo("Gamesim.AutoContinueLines"));
            Assert.That(ConversationLinePace.FromRecord(0) || ConversationLinePace.FromRecord(-1) || ConversationLinePace.FromRecord(2), Is.False,
                "No record, or any record but 1, waits for a press.");
            Assert.That(ConversationLinePace.FromRecord(ConversationLinePace.Record(true)), Is.True);
            Assert.That(ConversationLinePace.Record(false), Is.EqualTo(0));
            // Read as the house starts, written with every other setting, and drawn from the field both use.
            string start = Code(Read("Episode/EpisodeDirector.cs"));
            Assert.That(start, Does.Contain("autoContinueLines = ConversationLinePace.FromRecord(preferences.GetInt(ConversationLinePace.Key, 0));"));
            Assert.That(Body(Code(Read("Episode/EpisodeDirector.Season.cs")), "ApplyPreferences"),
                Does.Contain("store.SetInt(ConversationLinePace.Key, ConversationLinePace.Record(autoContinueLines));"));
            Assert.That(Body(Code(Read("Episode/EpisodeDirector.SettingsPrefs.cs")), "AccessibilitySection"),
                Does.Contain("hud.SettingChoice(SettingRowWords.ConversationLinesRow, SettingRowWords.ConversationLinesValues, autoContinueLines ? 1 : 0,"));
        }

        // ---------------------------------------------------------------- held in the source

        /// <summary>Each preference section's drawing, by the methods that draw it.</summary>
        private static readonly (string file, string method)[] RowDrawers =
        {
            ("Episode/EpisodeDirector.SettingsPrefs.cs", "SoundSection"), ("Episode/EpisodeDirector.SettingsPrefs.cs", "AccessibilitySection"),
            ("Episode/EpisodeDirector.Display.cs", "DisplaySettings"), ("Episode/EpisodeDirector.Display.cs", "EdgePanRows"),
            ("Episode/EpisodeDirector.CameraSettings.cs", "CameraSettings"),
        };

        [Test]
        public void TheSections_DrawEverySettingAsAKitRow_WithNoStepButtonsLeft()
        {
            var problems = new List<string>();
            var drawn = new StringBuilder();
            foreach (var (file, method) in RowDrawers)
            {
                string body = Body(Code(Read(file)), method);
                drawn.Append(body);
                foreach (var call in new[] { "hud.Action(", "hud.VolumeSlider(", "hud.Disclosure(" })
                    if (body.Contains(call)) problems.Add(file + " " + method + " draws " + call + ": a setting is a name-and-value row.");
            }
            // Controls: the tour is an action row that says why when it cannot play; the rebinding rows and
            // the reset are actions, as they were (a press starts listening, or puts every control back).
            string controls = Body(Code(Read("Episode/EpisodeDirector.SettingsPrefs.cs")), "ControlsSection");
            Assert.That(controls, Does.Contain("hud.SettingAction(ReplayTourCaption, ReplayTour,"), "The tour's row says why when it cannot play.");
            Assert.That(controls, Does.Not.Contain("hud.Action("));
            string rebinding = Body(Code(Read("Episode/EpisodeDirector.Controls.cs")), "ControlsSettings");
            Assert.That(CountOf(rebinding, "hud.Action("), Is.EqualTo(2), "Only the rebinding rows and the reset are actions there.");
            Assert.That(rebinding, Does.Contain("hud.Action(each.Caption, () => StartRebind(each));"));
            Assert.That(rebinding, Does.Contain("hud.Action(KeyBindings.ResetCaption, ResetBindings);"));
            foreach (var name in SettingRowWords.Rows)
            {
                string constant = "SettingRowWords." + typeof(SettingRowWords).GetFields().Single(field => field.IsLiteral && (string)field.GetRawConstantValue() == name).Name;
                int uses = CountOf(drawn.ToString(), constant + ",");
                if (uses != 1) problems.Add(name + " is drawn " + uses + " times.");
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void TheFourSections_HeadEveryBlockInTheOneSectionHeadingStyle()
        {
            // Ruling S4 and B4: the controls list's groups and "Change a control" were drawn in the large
            // accent heading beside CAMERA's section heading; every block now has the one style.
            var drawers = RowDrawers.Concat(new[]
            {
                ("Episode/EpisodeDirector.SettingsPrefs.cs", "ControlsSection"), ("Episode/EpisodeDirector.Controls.cs", "ControlsSettings"),
            });
            foreach (var (file, method) in drawers)
                Assert.That(Body(Code(Read(file)), method), Does.Not.Contain("hud.Heading("), file + " " + method + " heads a block in another style.");
            string rebinding = Body(Code(Read("Episode/EpisodeDirector.Controls.cs")), "ControlsSettings");
            Assert.That(rebinding, Does.Contain("hud.SectionHeading(context);"), "Each group of the controls list under the section heading.");
            Assert.That(rebinding, Does.Contain("hud.SectionHeading(RebindHeading);"), "Change a control under the section heading.");
        }

        [Test]
        public void TheOldActionCaptions_AreGoneFromEveryScreen()
        {
            string root = RuntimeRoot();
            var problems = new List<string>();
            foreach (var path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string code = Code(File.ReadAllText(path));
                string file = path.Substring(root.Length + 1).Replace('\\', '/');
                foreach (var caption in OldCaptions)
                    if (code.Contains("\"" + caption + "\"")) problems.Add(file + " still says \"" + caption + "\".");
                foreach (var prefix in OldCyclePrefixes)
                    if (code.Contains("\"" + prefix)) problems.Add(file + " still draws \"" + prefix + "...\".");
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void AValueChangedOutsideItsRow_IsDrawnAgain_AndARebindingKeepsLeftAndRight()
        {
            string display = Code(Read("Episode/EpisodeDirector.Display.cs")), camera = Code(Read("Episode/EpisodeDirector.CameraSettings.cs"));
            foreach (var (code, method) in new[] { (display, "SetVSync"), (display, "SetFrameCap"), (display, "SetWholeHouse"),
                (camera, "SetCameraSpeed"), (camera, "SetInvertTilt") })
                Assert.That(Body(code, method), Does.Contain("RedrawSettingsRows();"), method + " draws the rows again while they are on screen.");
            Assert.That(Braced(display, display.IndexOf('{', display.IndexOf("void SetDisplay(DisplayMode mode, string resolutionKey)", StringComparison.Ordinal))),
                Does.Contain("RedrawSettingsRows();"), "SetDisplay too.");
            Assert.That(Body(Code(Read("Episode/EpisodeDirector.SettingsPrefs.cs")), "RedrawSettingsRows"), Does.Contain("AreSettingsSectionsOpen"),
                "Only while the sections are on screen: a closed panel is not drawn over the house.");
            foreach (var file in new[] { "Episode/SettingRowButton.cs", "Episode/SettingRowSlider.cs" })
            {
                string move = Body(Code(Read(file)), "OnMove");
                int busy = move.IndexOf("HouseBindings.Busy", StringComparison.Ordinal);
                Assert.That(busy, Is.GreaterThanOrEqualTo(0), file + ": left and right wait while a rebinding listens.");
                Assert.That(busy, Is.LessThan(move.IndexOf("IsInteractable()", StringComparison.Ordinal)), file + ": before any step.");
            }
        }

        // ---------------------------------------------------------------- the source

        private static int CountOf(string text, string part)
        {
            int count = 0;
            for (int at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal)) count++;
            return count;
        }

        /// <summary>The body of the method named <paramref name="method"/> (declared "void name("), braces matched.</summary>
        private static string Body(string code, string method)
        {
            int at = code.IndexOf("void " + method + "(", StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), "No method " + method + ".");
            int arrow = code.IndexOf("=>", at, StringComparison.Ordinal), brace = code.IndexOf('{', at);
            int semicolon = code.IndexOf(';', at);
            if (arrow >= 0 && arrow < brace && semicolon < brace) return code.Substring(at, semicolon - at + 1);
            return Braced(code, brace);
        }

        private static string Braced(string code, int open)
        {
            int depth = 0;
            for (int index = open; index < code.Length; index++)
            {
                if (code[index] == '{') depth++;
                else if (code[index] == '}' && --depth == 0) return code.Substring(open, index - open + 1);
            }
            return code.Substring(open);
        }

        private static string Read(string relative) => File.ReadAllText(Path.Combine(RuntimeRoot(), relative));

        /// <summary>A source with its comments blanked and its strings kept.</summary>
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
