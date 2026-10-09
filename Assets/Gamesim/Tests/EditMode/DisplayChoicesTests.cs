using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The display settings' choices (PLAN A, A12), Unity-free: the resolution list, the display
    /// modes, VSync beside the frame-rate limit, how an older build's records read, and the first
    /// launch's decision, flag by flag.
    /// </summary>
    public sealed class DisplayChoicesTests
    {
        private static IEnumerable<KeyValuePair<int, int>> Sizes(params (int Width, int Height)[] sizes) =>
            sizes.Select(size => new KeyValuePair<int, int>(size.Width, size.Height));

        [Test]
        public void TheResolutionList_IsDesktopThenEverySizeAtLeast1280x720_OnceEach_SmallestFirst()
        {
            // Displays report each size once per refresh rate, and sizes the HUD cannot hold.
            var list = DisplayChoices.Resolutions(Sizes((1920, 1080), (800, 600), (1280, 720), (1920, 1080), (2560, 1440), (1280, 720),
                (1024, 768), (1600, 900), (1280, 1024), (1366, 768), (1280, 600)));
            Assert.That(list, Is.EqualTo(new[] { "Desktop", "1280x720", "1366x768", "1280x1024", "1600x900", "1920x1080", "2560x1440" }));
            Assert.That(DisplayChoices.Resolutions(null), Is.EqualTo(new[] { "Desktop" }), "Nothing offered: the desktop is still a choice.");
            Assert.That(DisplayChoices.Resolutions(Sizes((1920, 1080)), "1700x950"), Is.EqualTo(new[] { "Desktop", "1920x1080", "1700x950" }),
                "The window's own size joins the cycle, so the cycle starts where the player is.");
            Assert.That(DisplayChoices.Resolutions(Sizes((1920, 1080)), "1920x1080"), Is.EqualTo(new[] { "Desktop", "1920x1080" }));
            Assert.That(DisplayChoices.Resolutions(Sizes((1920, 1080)), "Desktop"), Is.EqualTo(new[] { "Desktop", "1920x1080" }));
        }

        [Test]
        public void AWindowAskedForTheDesktop_IsTheLargestSizeThatLeavesTheTitleBarAndTaskbarOnScreen()
        {
            // "Play in a window" from the first launch's borderless desktop: never a window the desktop's own size.
            var offered = Sizes((1280, 720), (1366, 768), (1600, 900), (1680, 1050), (1920, 1080), (1920, 1200));
            AssertWindowed(offered, 1920, 1080, 1600, 900, "1680x1050 and the desktop's own 1920x1080 run under the taskbar.");
            AssertWindowed(Sizes((1280, 720), (1920, 1080), (2048, 1152), (2560, 1440)), 2560, 1440, 2048, 1152, "The largest offered size inside 90% each way.");
            AssertWindowed(null, 1920, 1080, 1600, 900, "Nothing offered: the standard 16:9 sizes still are.");
            AssertWindowed(null, 3840, 2160, 2560, 1440, "A 4K desktop: the largest standard size.");
            AssertWindowed(Sizes((1366, 768), (1280, 720)), 1366, 768, 1280, 720, "Nothing fits a small laptop's desktop: the smallest the HUD holds.");
            AssertWindowed(Sizes((1024, 768), (800, 600)), 1280, 800, 1280, 720, "Sizes under 1280x720 are never chosen, even when nothing else fits.");
            AssertWindowed(Sizes((1680, 945), (1600, 900)), 1920, 1080, 1680, 945, "The larger area wins.");
        }

        private static void AssertWindowed(IEnumerable<KeyValuePair<int, int>> offered, int desktopWidth, int desktopHeight, int width, int height, string why)
        {
            DisplayChoices.WindowedDesktopSize(offered, desktopWidth, desktopHeight, out int readWidth, out int readHeight);
            Assert.That(DisplayChoices.SizeKey(readWidth, readHeight), Is.EqualTo(DisplayChoices.SizeKey(width, height)),
                "On a " + DisplayChoices.SizeKey(desktopWidth, desktopHeight) + " desktop: " + why);
        }

        [Test]
        public void ASize_ReadsAndWritesAsWidthByHeight()
        {
            Assert.That(DisplayChoices.SizeKey(2560, 1080), Is.EqualTo("2560x1080"));
            Assert.That(DisplayChoices.TryParseSize("1280x800", out int width, out int height), Is.True);
            Assert.That((width, height), Is.EqualTo((1280, 800)));
            foreach (var bad in new[] { null, "", "Desktop", "x900", "1600x", "16OOx900", "-1600x900", "0x900", "1600x900x2" })
                Assert.That(DisplayChoices.TryParseSize(bad, out _, out _), Is.False, "'" + bad + "' is not a size.");
        }

        [Test]
        public void TheCycles_GoRound()
        {
            Assert.That(DisplayChoices.NextMode(DisplayMode.Windowed), Is.EqualTo(DisplayMode.Borderless));
            Assert.That(DisplayChoices.NextMode(DisplayMode.Borderless), Is.EqualTo(DisplayMode.Exclusive));
            Assert.That(DisplayChoices.NextMode(DisplayMode.Exclusive), Is.EqualTo(DisplayMode.Windowed));
            var cycle = new[] { "Desktop", "1280x720", "1920x1080" };
            Assert.That(DisplayChoices.Next(cycle, "Desktop"), Is.EqualTo("1280x720"));
            Assert.That(DisplayChoices.Next(cycle, "1920x1080"), Is.EqualTo("Desktop"));
            Assert.That(DisplayChoices.Next(cycle, "640x480"), Is.EqualTo("Desktop"), "A size not in the cycle starts it.");
            foreach (var mode in new[] { DisplayMode.Windowed, DisplayMode.Borderless, DisplayMode.Exclusive })
            {
                Assert.That(DisplayChoices.TryParseMode(DisplayChoices.ModeName(mode), out var read), Is.True);
                Assert.That(read, Is.EqualTo(mode));
            }
            Assert.That(DisplayChoices.TryParseMode("FullScreen", out _), Is.False);
            Assert.That(DisplayChoices.FrameLimits, Is.EqualTo(new[] { 30, 60, 120, -1 }), "Three caps, then none (decision 12).");
            Assert.That(DisplayChoices.FrameLimitName(60), Is.EqualTo("60 fps"));
            Assert.That(DisplayChoices.FrameLimitName(-1), Is.EqualTo("Uncapped"));
        }

        [Test]
        public void AnOlderBuildsRecords_ReadAsTodaysSettings()
        {
            // Full screen 1 / 0 is borderless / windowed; the game's own record wins; neither, the window as it is.
            Assert.That(DisplayChoices.ModeFromRecords(null, 1, DisplayMode.Windowed), Is.EqualTo(DisplayMode.Borderless));
            Assert.That(DisplayChoices.ModeFromRecords(null, 0, DisplayMode.Exclusive), Is.EqualTo(DisplayMode.Windowed));
            Assert.That(DisplayChoices.ModeFromRecords("Exclusive", 0, DisplayMode.Windowed), Is.EqualTo(DisplayMode.Exclusive));
            Assert.That(DisplayChoices.ModeFromRecords(null, null, DisplayMode.Borderless), Is.EqualTo(DisplayMode.Borderless));
            Assert.That(DisplayChoices.ModeFromRecords("nonsense", null, DisplayMode.Windowed), Is.EqualTo(DisplayMode.Windowed));

            // An older frame cap of 0 was VSync: VSync on, no limit. A cap was a cap with VSync off.
            AssertFrame(null, 0, true, -1);
            AssertFrame(null, 60, false, 60);
            AssertFrame(null, -1, false, -1);
            AssertFrame(null, 45, true, -1);
            // Today's records: VSync and the limit apart.
            AssertFrame(1, 120, true, 120);
            AssertFrame(0, 30, false, 30);
            AssertFrame(0, 0, false, -1);
        }

        private static void AssertFrame(int? vSyncRecord, int capRecord, bool vSync, int limit)
        {
            DisplayChoices.FrameFromRecords(vSyncRecord, capRecord, out bool readVSync, out int readLimit);
            Assert.That((readVSync, readLimit), Is.EqualTo((vSync, limit)), "VSync record " + vSyncRecord + ", cap " + capRecord);
        }

        [Test]
        public void SetFrameCap_KeepsItsMeaning()
        {
            // The profile's -1 is uncapped with VSync off; 0 is VSync with no cap; a cap is a cap with VSync off.
            foreach (var (cap, vSync, limit) in new[] { (-1, false, -1), (0, true, -1), (30, false, 30), (60, false, 60), (120, false, 120), (75, true, -1) })
            {
                DisplayChoices.FrameFromCap(cap, out bool readVSync, out int readLimit);
                Assert.That((readVSync, readLimit), Is.EqualTo((vSync, limit)), "SetFrameCap(" + cap + ")");
            }
        }

        [Test]
        public void TheFirstLaunch_IsBorderlessAtTheDesktopOnlyWhenNothingElseSays()
        {
            var plain = new[] { "Gamesim.exe" };
            Assert.That(DisplayFirstRun.Decide(plain, false, false, false, false), Is.EqualTo(DisplayFirstRun.Decision.Apply));
            Assert.That(DisplayFirstRun.Decide(new[] { "Gamesim.exe", "--gamesim-save-root", @"C:\playtest" }, false, false, false, false),
                Is.EqualTo(DisplayFirstRun.Decision.Apply), "A playtest root with no record starts like a first install.");
            Assert.That(DisplayFirstRun.Decide(plain, true, false, false, false), Is.EqualTo(DisplayFirstRun.Decision.Editor));
            Assert.That(DisplayFirstRun.Decide(plain, false, true, false, false), Is.EqualTo(DisplayFirstRun.Decision.Batch));
            foreach (var flag in new[] { "-screen-width", "-screen-height", "-screen-fullscreen", "-window-mode", "-popupwindow", "-monitor", "-SCREEN-WIDTH", "-Window-Mode" })
                Assert.That(DisplayFirstRun.Decide(new[] { "Gamesim.exe", flag, "1" }, false, false, false, false),
                    Is.EqualTo(DisplayFirstRun.Decision.CommandLineWindow), flag + " already says how the window stands.");
            Assert.That(DisplayFirstRun.Decide(new[] { "Gamesim.exe", "--gamesim-verify", "--gamesim-save-root", @"C:\v" }, false, false, false, false),
                Is.EqualTo(DisplayFirstRun.Decision.Verifier), "The profile is windowed 1920x1080, never borderless.");
            Assert.That(DisplayFirstRun.Decide(plain, false, false, false, false, isolatedWithoutPreferences: true),
                Is.EqualTo(DisplayFirstRun.Decision.IsolatedWithoutPreferences));
            Assert.That(DisplayFirstRun.Decide(plain, false, false, true, false), Is.EqualTo(DisplayFirstRun.Decision.Recorded), "A second launch.");
            Assert.That(DisplayFirstRun.Decide(plain, false, false, false, true), Is.EqualTo(DisplayFirstRun.Decision.LegacyRecorded),
                "An older build's full-screen flag: nobody already playing is moved (decision 15).");
            Assert.That(DisplayFirstRun.Decide(null, false, false, false, false), Is.EqualTo(DisplayFirstRun.Decision.Apply));
            Assert.That(DisplayFirstRun.Decide(new[] { "Gamesim.exe", "-screen-widths" }, false, false, false, false), Is.EqualTo(DisplayFirstRun.Decision.Apply),
                "Only the flags themselves.");
        }
    }
}
