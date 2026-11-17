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
    /// Menu slice 4's shell (the review's change 2): the Settings frame's sections and the row kit's
    /// words and steps, and - held in the source - every row Settings had in its one long scroll
    /// drawn by exactly one section, the door that opens the game menu card, and the frame's ways
    /// back. Unity-free, so the subset runs it on every push.
    /// </summary>
    public sealed class MenuSettingsShellTests
    {
        // ---------------------------------------------------------------- the sections

        [Test]
        public void Sections_AreTheSixInTheReviewsOrder_EachNamedOnce()
        {
            Assert.That(SettingsWords.Sections.Select(SettingsWords.Name), Is.EqualTo(new[]
                { "Saves", "Sound", "Display", "Accessibility", "Controls", "Advanced" }));
            Assert.That(SettingsWords.Sections.Select(SettingsWords.TabName).Distinct().Count(), Is.EqualTo(6), "Each entry's object is named apart.");
            Assert.That(SettingsWords.TabName(SettingsSection.Sound), Is.EqualTo("Settings section: Sound"));
            Assert.That(SettingsWords.Heading(SettingsSection.Advanced), Is.EqualTo("ADVANCED"), "The heading is the name in the one style's capitals.");
        }

        [Test]
        public void Settings_OpenOnSaves_AndABlockedSaveOnAdvanced()
        {
            Assert.That(SettingsWords.OpeningSection(false), Is.EqualTo(SettingsSection.Saves));
            Assert.That(SettingsWords.OpeningSection(true), Is.EqualTo(SettingsSection.Advanced),
                "A save that cannot be read opens where the tools that mend it are.");
        }

        [Test]
        public void LeftAndRight_WalkTheListAndWrap()
        {
            Assert.That(SettingsWords.Neighbour(SettingsSection.Saves, 1), Is.EqualTo(SettingsSection.Sound));
            Assert.That(SettingsWords.Neighbour(SettingsSection.Saves, -1), Is.EqualTo(SettingsSection.Advanced));
            Assert.That(SettingsWords.Neighbour(SettingsSection.Advanced, 1), Is.EqualTo(SettingsSection.Saves));
        }

        // ---------------------------------------------------------------- the row kit

        [Test]
        public void ASwitch_ReadsOnOrOff()
        {
            Assert.That(SettingsWords.SwitchValue(true), Is.EqualTo("On"));
            Assert.That(SettingsWords.SwitchValue(false), Is.EqualTo("Off"));
        }

        [Test]
        public void ASlider_ReadsAPercent_AndStepsByTenWithinItsEnds()
        {
            Assert.That(SettingsWords.Percent(70), Is.EqualTo("70%"));
            Assert.That(SettingsWords.Percent(140), Is.EqualTo("100%"));
            Assert.That(SettingsWords.SliderStepped(70, 1), Is.EqualTo(80));
            Assert.That(SettingsWords.SliderStepped(70, -1), Is.EqualTo(60));
            Assert.That(SettingsWords.SliderStepped(95, 1), Is.EqualTo(100), "Held at the top.");
            Assert.That(SettingsWords.SliderStepped(4, -1), Is.EqualTo(0), "Held at the foot.");
        }

        [Test]
        public void AChoice_StepsBackAndOnAndWraps()
        {
            Assert.That(SettingsWords.ChoiceStepped(0, 3, 1), Is.EqualTo(1));
            Assert.That(SettingsWords.ChoiceStepped(2, 3, 1), Is.EqualTo(0));
            Assert.That(SettingsWords.ChoiceStepped(0, 3, -1), Is.EqualTo(2));
            Assert.That(SettingsWords.ChoiceStepped(0, 0, 1), Is.EqualTo(0), "A choice with no values stays put.");
        }

        [Test]
        public void ARow_TellsAScreenReaderItsNameAndValue_AndWhyItCannotBeUsed()
        {
            Assert.That(SettingsWords.Description("Music", "On"), Is.EqualTo("Music: On"));
            Assert.That(SettingsWords.Description("Music volume", "70%", "Turn the sound on first."),
                Is.EqualTo("Music volume: 70%. Turn the sound on first."));
        }

        // ---------------------------------------------------------------- the game menu card

        [Test]
        public void TheGameMenu_HasItsFourRows_AndSaysWhenItSaved()
        {
            Assert.That(new[] { SettingsWords.ResumeCaption, SettingsWords.SaveCaption, SettingsWords.SettingsCaption, SettingsWords.MainMenuCaption },
                Is.EqualTo(new[] { "Resume", "Save", "Settings", "Main menu" }));
            Assert.That(SettingsWords.GameMenuTitle, Is.EqualTo("Game menu"));
            Assert.That(SettingsWords.PausedLabel, Is.EqualTo("PAUSED"));
            Assert.That(WayOut.BackTo(SettingsWords.GameMenuPlace), Is.EqualTo("Back to the game menu"));
            Assert.That(SettingsWords.SavedAtLine(new DateTime(2026, 11, 16, 19, 42, 0)), Is.EqualTo("Saved at 7:42 PM."));
        }

        [Test]
        public void TheClock_ComesInThroughOneSeam()
        {
            var held = SettingsWords.Clock;
            try
            {
                var fixedNow = new DateTime(2030, 1, 2, 3, 4, 5);
                SettingsWords.Clock = () => fixedNow;
                Assert.That(SettingsWords.Now, Is.EqualTo(fixedNow));
                SettingsWords.Clock = null;
                Assert.That((SettingsWords.Now - DateTime.Now).Duration(), Is.LessThan(TimeSpan.FromMinutes(1)), "With no seam set, the real clock.");
            }
            finally { SettingsWords.Clock = held; }
        }

        // ---------------------------------------------------------------- held in the source

        /// <summary>
        /// Every row of the one long scroll Settings had before slice 4, by the caption (or the
        /// constant naming it) its drawing call carries: each drawn by exactly one section. Two lists,
        /// one a part each: a part that turns a row into a name-and-value row puts its new caption or
        /// constant in place of the old here, in its own list.
        /// </summary>
        private static readonly string[] SavesAndAdvancedRows =
        {
            // Part a: the running season's card replaced the slot's name; Reload became Advanced's go-back pick.
            "SavesWords.SaveNowCaption", "SavesWords.LoadCaption", "SavesWords.DeleteCaption", "SavesWords.NoSaves",
            "SavesWords.GoBackCaption", "SavesWords.GoBackResult", "RecoverBackupCaption", "SavesWords.RestoreResult", "NewSeasonInNewSlotCaption",
            "ResetCareerCaption", "ConfirmResetCareerCaption", "KeepCareerCaption", "UnreadCareerLine",
            "ImportHeading", "ImportLine", "\"Full path to exported JSON\"",
        };

        /// <summary>
        /// The Sound, Display, Accessibility and Controls rows, as <see cref="SavesAndAdvancedRows"/>: since
        /// part b, each setting by its row's name (SettingRowWords), the window's two controls one row.
        /// </summary>
        private static readonly string[] PreferenceRows =
        {
            "SettingRowWords.AllSoundRow", "SettingRowWords.MasterVolumeRow", "SettingRowWords.MusicRow", "SoundPictures.MeterCaption(",
            "AccessibilityLine", "SettingRowWords.ReducedMotionRow", "SettingRowWords.ReducedSoundRow", "SettingRowWords.TextSizeRow",
            "SettingRowWords.CaptionTimeRow", "ReplayTourCaption", "InputGlossary.CeremonyPaceLine(", "SettingRowWords.CeremonyPaceRow",
            "SettingRowWords.CompactHudRow", "SettingRowWords.QualityRow", "SettingRowWords.FrameRateRow", "SettingRowWords.VSyncRow",
            "SettingRowWords.DisplayModeRow", "SettingRowWords.ResolutionRow", "SettingRowWords.MonitorRow", "SettingRowWords.EdgePanningRow",
            "SettingRowWords.HouseViewRow", "SettingRowWords.LanguageRow", "SettingRowWords.CameraSpeedRow", "SettingRowWords.CameraTiltRow",
            "ShowControlsCaption", "RebindHeading", "KeyBindings.ResetCaption", "ControlsPage.Page(",
        };

        private static IEnumerable<string> TodaysRows => SavesAndAdvancedRows.Concat(PreferenceRows);

        /// <summary>Each section's drawing, by the methods it calls (the file each lives in).</summary>
        private static readonly (SettingsSection section, (string file, string method)[] parts)[] SectionParts =
        {
            (SettingsSection.Saves, new[] { ("Episode/EpisodeDirector.SettingsSaves.cs", "SavesSection"),
                ("Episode/EpisodeDirector.SettingsSaves.cs", "RunningSeasonCard"), ("Episode/EpisodeDirector.SettingsSaves.cs", "SeasonCardFor") }),
            (SettingsSection.Advanced, new[] { ("Episode/EpisodeDirector.SettingsSaves.cs", "AdvancedSection"), ("Episode/EpisodeDirector.Career.cs", "CareerSettings") }),
            (SettingsSection.Sound, new[] { ("Episode/EpisodeDirector.SettingsPrefs.cs", "SoundSection") }),
            (SettingsSection.Display, new[] { ("Episode/EpisodeDirector.Display.cs", "DisplaySettings") }),
            (SettingsSection.Accessibility, new[] { ("Episode/EpisodeDirector.SettingsPrefs.cs", "AccessibilitySection") }),
            (SettingsSection.Controls, new[] { ("Episode/EpisodeDirector.SettingsPrefs.cs", "ControlsSection"), ("Episode/EpisodeDirector.Controls.cs", "ControlsSettings"),
                ("Episode/EpisodeDirector.CameraSettings.cs", "CameraSettings"), ("Episode/EpisodeDirector.Display.cs", "EdgePanRows") }),
        };

        [Test]
        public void EveryRowSettingsHadIsDrawnByExactlyOneSection()
        {
            var bodies = SectionParts.ToDictionary(entry => entry.section,
                entry => string.Join("\n", entry.parts.Select(part => Body(Code(Read(part.file)), part.method))));
            var problems = new List<string>();
            foreach (var row in TodaysRows)
            {
                var holders = bodies.Where(pair => pair.Value.Contains(row)).Select(pair => pair.Key.ToString()).ToList();
                if (holders.Count != 1) problems.Add(row + " is drawn by " + (holders.Count == 0 ? "no section" : string.Join(", ", holders)) + ".");
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That(SectionParts.Select(entry => entry.section), Is.EquivalentTo(SettingsWords.Sections), "Every section is drawn by something.");
        }

        [Test]
        public void TheFrame_DrawsEachSectionFromItsOwnMethod_AndOneHeadingForIt()
        {
            string frame = Code(Read("Episode/EpisodeDirector.SettingsFrame.cs"));
            string settings = Body(frame, "Settings");
            foreach (var (section, method) in new[] { (SettingsSection.Saves, "SavesSection"), (SettingsSection.Sound, "SoundSection"),
                (SettingsSection.Display, "DisplaySection"), (SettingsSection.Accessibility, "AccessibilitySection"), (SettingsSection.Controls, "ControlsSection") })
                Assert.That(settings, Does.Contain("case SettingsSection." + section + ": " + method + "();"), section + " is drawn by " + method + ".");
            Assert.That(settings, Does.Contain("default: AdvancedSection();"));
            Assert.That(settings, Does.Contain("hud.SectionHeading(SettingsWords.Heading(settingsSection));"), "One heading for the section shown, in the one style.");
            Assert.That(settings, Does.Contain("hud.SettingsSections(SettingsWords.Sections, settingsSection, ShowSettingsSection);"), "The list, with the shown section lit.");
            // The section headings the moved code drew are the frame's now: none of them is drawn twice.
            Assert.That(Code(Read("Episode/EpisodeDirector.Display.cs")), Does.Not.Contain("SectionHeading(\"DISPLAY\")"));
            Assert.That(Code(Read("Episode/EpisodeDirector.Controls.cs")), Does.Not.Contain("SectionHeading(\"CONTROLS\")"));
        }

        [Test]
        public void TheHousesDoor_OpensTheGameMenu_AndTheFrontDoorsTheFrame()
        {
            string frame = Code(Read("Episode/EpisodeDirector.SettingsFrame.cs"));
            Assert.That(Body(frame, "OpenSettings"), Does.Contain("OpenSettingsOn(null)"), "The door names no section,");
            Assert.That(Body(frame, "OpenSettingsOn"), Does.Contain("gameMenuShown = section == null && !settingsFromFrontDoor && !blockedRecovery;"),
                "so from the house it shows the card; from the main menu, the creator's gear or a blocked save, the frame.");
            Assert.That(Code(Read("Episode/EpisodeDirector.Season.cs")), Does.Contain("MarkSettingsFromMainMenu();"),
                "The main menu's Settings say so, so their way back goes there.");
            string wayBack = PropertyBody(frame, "SettingsWayBackCaption");
            Assert.That(wayBack, Does.Contain("settingsFromMainMenu ? WayOut.BackTo(WayOut.MainMenu)"));
            Assert.That(wayBack, Does.Contain("WayOut.BackTo(SettingsWords.GameMenuPlace)"));
            string game = Body(frame, "GameMenu");
            Assert.That(game, Does.Contain("hud.GameMenuRow(SettingsWords.ResumeCaption, ClosePanels, primary: true);"),
                "Resume is the card's one main button, and does what Close, Escape and B do.");
            Assert.That(game, Does.Contain("hud.GameMenuCard(IsHouseClockHeld);"), "PAUSED only while the house's clock is held.");
        }

        // ---------------------------------------------------------------- the source

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

        /// <summary>The body of the property named <paramref name="name"/>, up to its semicolon.</summary>
        private static string PropertyBody(string code, string name)
        {
            int at = code.IndexOf(" " + name + " =>", StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), "No property " + name + ".");
            return code.Substring(at, code.IndexOf(';', at) - at + 1);
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
