using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Episode
{
    /// <summary>How the game's window stands: in a window, borderless full screen, or exclusive full screen.</summary>
    public enum DisplayMode { Windowed, Borderless, Exclusive }

    /// <summary>
    /// The display settings' choices and their records (PLAN A, A12), Unity-free so the subset holds
    /// them: the resolution list the settings cycle, the display modes, the frame-rate limits beside
    /// VSync, and how an older build's records read today. The director turns them into
    /// <c>Screen</c> and <c>QualitySettings</c> calls.
    ///
    /// <para>Records, kept in the session's preferences store (A13): <c>Gamesim.DisplayMode</c>
    /// ("Windowed", "Borderless", "Exclusive"), <c>Gamesim.Resolution</c> ("Desktop" or "1920x1080"),
    /// <c>Gamesim.VSync</c> (1 or 0) and <c>Gamesim.FrameCap</c> (30, 60, 120, or -1 for none). An older
    /// build kept <c>Gamesim.Fullscreen</c> (1 or 0) and a <c>Gamesim.FrameCap</c> whose 0 meant VSync.</para>
    /// </summary>
    public static class DisplayChoices
    {
        public const string DisplayModeKey = "Gamesim.DisplayMode";
        public const string ResolutionKey = "Gamesim.Resolution";
        public const string VSyncKey = "Gamesim.VSync";
        public const string FrameCapKey = "Gamesim.FrameCap";
        /// <summary>An older build's full-screen flag, which still counts as a display record (the lead's decision 15).</summary>
        public const string LegacyFullscreenKey = "Gamesim.Fullscreen";

        /// <summary>The resolution that is whatever the desktop is.</summary>
        public const string Desktop = "Desktop";

        /// <summary>The smallest window the settings offer: the HUD is laid out for 1600x900 and holds down to 1280x720.</summary>
        public const int MinimumWidth = 1280, MinimumHeight = 720;

        /// <summary>The frame-rate limits the settings cycle with VSync off: three caps, then none.</summary>
        public static readonly int[] FrameLimits = { 30, 60, 120, -1 };

        /// <summary>A size as the records and the settings write it: "1920x1080".</summary>
        public static string SizeKey(int width, int height) =>
            width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture);

        /// <summary>A size from its record, "1920x1080"; false for "Desktop" or anything that is not one.</summary>
        public static bool TryParseSize(string key, out int width, out int height)
        {
            width = height = 0;
            if (string.IsNullOrEmpty(key)) return false;
            int x = key.IndexOf('x');
            if (x <= 0 || x == key.Length - 1) return false;
            return int.TryParse(key.Substring(0, x), NumberStyles.None, CultureInfo.InvariantCulture, out width)
                && int.TryParse(key.Substring(x + 1), NumberStyles.None, CultureInfo.InvariantCulture, out height)
                && width > 0 && height > 0;
        }

        /// <summary>
        /// The resolutions the settings cycle: Desktop first, then every size the display offers at
        /// least 1280x720, each once however many refresh rates it comes in, smallest first; and the
        /// window's own size when it is none of those, so the cycle always starts where the player is.
        /// </summary>
        public static List<string> Resolutions(IEnumerable<KeyValuePair<int, int>> offered, string current = null)
        {
            var sizes = (offered ?? Enumerable.Empty<KeyValuePair<int, int>>())
                .Where(size => size.Key >= MinimumWidth && size.Value >= MinimumHeight)
                .Distinct()
                .OrderBy(size => (long)size.Key * size.Value).ThenBy(size => size.Key)
                .Select(size => SizeKey(size.Key, size.Value)).ToList();
            var list = new List<string> { Desktop };
            list.AddRange(sizes);
            if (!string.IsNullOrEmpty(current) && !list.Contains(current) && TryParseSize(current, out _, out _)) list.Add(current);
            return list;
        }

        /// <summary>The sizes a window may always take, whatever the display reports: 16:9, as the HUD is laid out.</summary>
        public static readonly KeyValuePair<int, int>[] StandardWindowSizes =
        {
            new KeyValuePair<int, int>(1280, 720), new KeyValuePair<int, int>(1600, 900),
            new KeyValuePair<int, int>(1920, 1080), new KeyValuePair<int, int>(2560, 1440),
        };

        /// <summary>The share of the desktop a window asked for the desktop's size may cover each way, leaving room for its title bar and the taskbar.</summary>
        public const double WindowShareOfDesktop = .9;

        /// <summary>
        /// The size a window takes when its resolution is "Desktop": a window the desktop's own size
        /// puts its title bar and bottom edge under the taskbar or off the screen - what "Play in a
        /// window" did from the first launch's borderless desktop. So the largest of the sizes the
        /// display offers and the standard 16:9 ones, at least 1280x720, that fits within 90% of the
        /// desktop each way (the larger area, then the wider); 1280x720, the smallest the HUD holds,
        /// when none does.
        /// </summary>
        public static void WindowedDesktopSize(IEnumerable<KeyValuePair<int, int>> offered, int desktopWidth, int desktopHeight,
            out int width, out int height)
        {
            double roomWidth = desktopWidth * WindowShareOfDesktop, roomHeight = desktopHeight * WindowShareOfDesktop;
            var fits = (offered ?? Enumerable.Empty<KeyValuePair<int, int>>()).Concat(StandardWindowSizes)
                .Where(size => size.Key >= MinimumWidth && size.Value >= MinimumHeight && size.Key <= roomWidth && size.Value <= roomHeight)
                .OrderByDescending(size => (long)size.Key * size.Value).ThenByDescending(size => size.Key)
                .ToList();
            width = fits.Count > 0 ? fits[0].Key : MinimumWidth;
            height = fits.Count > 0 ? fits[0].Value : MinimumHeight;
        }

        /// <summary>The next of a cycle after <paramref name="current"/>, the first when it is not in it.</summary>
        public static string Next(IList<string> cycle, string current)
        {
            if (cycle == null || cycle.Count == 0) return current;
            int at = cycle.IndexOf(current);
            return cycle[(at + 1) % cycle.Count];
        }

        public static string ModeName(DisplayMode mode) =>
            mode == DisplayMode.Borderless ? "Borderless" : mode == DisplayMode.Exclusive ? "Exclusive" : "Windowed";

        public static bool TryParseMode(string name, out DisplayMode mode)
        {
            switch (name)
            {
                case "Windowed": mode = DisplayMode.Windowed; return true;
                case "Borderless": mode = DisplayMode.Borderless; return true;
                case "Exclusive": mode = DisplayMode.Exclusive; return true;
                default: mode = DisplayMode.Windowed; return false;
            }
        }

        /// <summary>The settings' cycle: Windowed, Borderless, Exclusive, and round.</summary>
        public static DisplayMode NextMode(DisplayMode mode) => (DisplayMode)(((int)mode + 1) % 3);

        /// <summary>
        /// The display mode a session reads: its own record; an older build's full-screen flag
        /// (1 borderless, 0 windowed); or, with neither, the window as it stands.
        /// </summary>
        public static DisplayMode ModeFromRecords(string recorded, int? legacyFullscreen, DisplayMode window)
        {
            if (TryParseMode(recorded, out var mode)) return mode;
            if (legacyFullscreen.HasValue) return legacyFullscreen.Value == 1 ? DisplayMode.Borderless : DisplayMode.Windowed;
            return window;
        }

        /// <summary>
        /// VSync and the frame-rate limit a session reads. With a VSync record, the record and the
        /// limit (an unknown limit is none). Without one - an older build's - its frame cap: 0 was
        /// VSync, which is VSync on with no limit; a cap was a cap with VSync off; -1 was neither.
        /// </summary>
        public static void FrameFromRecords(int? recordedVSync, int recordedCap, out bool vSync, out int limit)
        {
            bool known = Array.IndexOf(FrameLimits, recordedCap) >= 0;
            if (recordedVSync.HasValue)
            {
                vSync = recordedVSync.Value == 1;
                limit = known ? recordedCap : -1;
                return;
            }
            vSync = !known;
            limit = known ? recordedCap : -1;
        }

        /// <summary>
        /// A frame cap as code has always set it (<c>EpisodeDirector.SetFrameCap</c>): 0 is VSync with
        /// no limit, -1 is neither - what the profile's benchmark wants - and 30, 60 or 120 is a cap with
        /// VSync off. Anything else is VSync.
        /// </summary>
        public static void FrameFromCap(int cap, out bool vSync, out int limit)
        {
            if (cap != 0 && Array.IndexOf(FrameLimits, cap) >= 0) { vSync = false; limit = cap; return; }
            vSync = true;
            limit = -1;
        }

        /// <summary>A limit's words: "30 fps", "Uncapped".</summary>
        public static string FrameLimitName(int limit) => limit < 0 ? "Uncapped" : limit.ToString(CultureInfo.InvariantCulture) + " fps";
    }

    /// <summary>
    /// The first launch's window (the owner's decision A-4): borderless full screen at the desktop's
    /// resolution, applied in code before the splash - never by editing the project's settings. Only
    /// on a real first launch: not in the editor or a batch run, not when the command line already
    /// says how the window stands, not under the verifier (whose profile is windowed 1920x1080), not
    /// for an isolated root that keeps no preferences, and not when a display record exists - the
    /// game's own, or an older build's full-screen flag, so nobody already playing is moved (decision 15).
    /// Later launches come back as they were left through Unity's own screen records.
    /// </summary>
    public static class DisplayFirstRun
    {
        /// <summary>The player arguments that already set the window, matched without regard to case.</summary>
        public static readonly string[] WindowArguments = { "-screen-width", "-screen-height", "-screen-fullscreen", "-window-mode", "-popupwindow", "-monitor" };

        public enum Decision { Apply, Editor, Batch, CommandLineWindow, Verifier, IsolatedWithoutPreferences, Recorded, LegacyRecorded }

        public static Decision Decide(IList<string> args, bool isEditor, bool isBatch, bool hasRecord, bool hasLegacyFullscreenKey,
            bool isolatedWithoutPreferences = false)
        {
            if (isEditor) return Decision.Editor;
            if (isBatch) return Decision.Batch;
            if (args != null)
                foreach (var arg in args)
                {
                    if (string.Equals(arg, Gamesim.Persistence.PreferenceRouting.VerifyArgument, StringComparison.OrdinalIgnoreCase)) return Decision.Verifier;
                    foreach (var window in WindowArguments)
                        if (string.Equals(arg, window, StringComparison.OrdinalIgnoreCase)) return Decision.CommandLineWindow;
                }
            if (isolatedWithoutPreferences) return Decision.IsolatedWithoutPreferences;
            if (hasRecord) return Decision.Recorded;
            if (hasLegacyFullscreenKey) return Decision.LegacyRecorded;
            return Decision.Apply;
        }
    }
}
