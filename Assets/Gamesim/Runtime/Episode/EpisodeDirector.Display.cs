using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Presentation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// Display preferences (MASTER-PLAN §3.H; PLAN A, A12): a quality tier, how the window stands
    /// (windowed, borderless or exclusive full screen), its resolution and monitor, VSync and a
    /// frame-rate limit beside it, and whether the screen's edges pan the camera. Kept in the
    /// session's preferences store (A13) beside the sound and accessibility preferences.
    ///
    /// <para>The window is applied only by the controls that name it - the display mode, the
    /// resolution, the monitor and the full-screen toggle - and by the verifier's profile through
    /// <see cref="SetDisplay(DisplayMode,int,int)"/>; never by the fan-out every other preference goes
    /// through (<see cref="ApplyPreferences"/>), which used to put the window back to the field on any
    /// volume click after Alt+Enter had changed it. A first launch comes up borderless at the
    /// desktop's resolution (<see cref="DisplayFirstRun"/>), and later launches as Unity's own screen
    /// records left them.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The frame caps <see cref="SetFrameCap"/> takes: 0 is VSync, -1 neither, the rest a cap with VSync off.</summary>
        public static readonly int[] FrameCaps = { 0, 30, 60, 120, -1 };

        public const string PlayWindowedCaption = "Play in a window";
        public const string PlayFullScreenCaption = "Play full screen";
        public const string VSyncOffCaption = "Turn VSync off";
        public const string VSyncOnCaption = "Turn VSync on";
        /// <summary>What the profile's report says set its window: the same director call the settings make.</summary>
        public const string DisplayRoute = "EpisodeDirector.SetDisplay";

        private int qualityTier = 1;
        private DisplayMode displayMode;
        private string resolution = DisplayChoices.Desktop;
        private bool vSync = true;
        private int frameLimit = -1;
        private int monitorChosen = -1;
        private bool edgePan = true;
        private bool compactHud;
        private bool reducedAudio;
        /// <summary>Reduced audio: the room tone off and the cues and music at half, the sound's reduced motion.</summary>
        public bool ReducedAudio => reducedAudio;
        private string language = Localisation.DefaultLanguage;
        public string Language => language;

        public int QualityTier => qualityTier;
        /// <summary>The frame cap as code has always read it: 0 with VSync on, else the limit (-1 for none).</summary>
        public int FrameCap => vSync ? 0 : frameLimit;
        /// <summary>Whether VSync paces the frames.</summary>
        public bool VSync => vSync;
        /// <summary>The frame-rate limit with VSync off: 30, 60, 120, or -1 for none.</summary>
        public int FrameLimit => frameLimit;
        /// <summary>How the window stands, as the settings record it.</summary>
        public DisplayMode DisplayModeSetting => displayMode;
        /// <summary>The window's resolution, "Desktop" or "1920x1080".</summary>
        public string Resolution => resolution;
        /// <summary>Whether the window is full screen, borderless or exclusive.</summary>
        public bool Fullscreen => displayMode != DisplayMode.Windowed;
        public bool EdgePanOn => edgePan;
        public bool CompactHud => compactHud;

        /// <summary>
        /// How many times this director has applied the window - a display control, the monitor, the
        /// profile's <see cref="SetDisplay(DisplayMode,int,int)"/>. Every other setting leaves it alone.
        /// </summary>
        public int WindowApplications { get; private set; }

        public void SetCompactHud(bool compact)
        {
            compactHud=compact;
            ApplyPreferences();
            Render();
        }

        public static string QualityName(int tier)
        {
            var names = QualitySettings.names;
            if (tier < 0 || tier >= names.Length) return "Default";
            // The project's two levels are the pipeline assets' names; say what they mean.
            return names[tier] == "Mobile" ? "Lean" : names[tier] == "PC" ? "Full" : names[tier];
        }

        public static string FrameCapName(int cap) => cap == 0 ? "VSync" : DisplayChoices.FrameLimitName(cap);

        public static string DisplayModeCaption(DisplayMode mode) => "Display mode: " + DisplayChoices.ModeName(mode) + "  (change)";
        public static string ResolutionCaption(string resolution) => "Resolution: " + resolution + "  (change)";
        public static string MonitorCaption(int number, int count) => "Monitor: " + number + " of " + count + "  (change)";
        public static string FrameRateCaption(int limit) => "Frame rate: " + DisplayChoices.FrameLimitName(limit) + "  (change)";

        /// <summary>
        /// Sets the frame-rate preference from code - the verifier's profile, which is a benchmark and
        /// must not inherit VSync, or a test - with the meaning it has always had: -1 is uncapped with
        /// VSync off, 0 is VSync with no cap, and 30, 60 or 120 a cap with VSync off. Applied with the
        /// other preferences, so a later settings toggle keeps it rather than putting the default back.
        /// </summary>
        public void SetFrameCap(int cap)
        {
            DisplayChoices.FrameFromCap(cap, out vSync, out frameLimit);
            ApplyPreferences();
        }

        /// <summary>Turns VSync on or off, the limit kept for when it is off.</summary>
        public void SetVSync(bool on)
        {
            vSync = on;
            ApplyPreferences();
        }

        /// <summary>
        /// Sets how the window stands and its size, applies the window and keeps the choice: what the
        /// display controls do, and the route the verifier's profile takes to its windowed 1920x1080
        /// (the lead's decision 13), so the profile's window is the settings' window.
        /// </summary>
        public void SetDisplay(DisplayMode mode, int width, int height) => SetDisplay(mode, DisplayChoices.SizeKey(width, height));

        /// <summary>Sets how the window stands and its resolution, "Desktop" or "1920x1080", applies it and keeps it.</summary>
        public void SetDisplay(DisplayMode mode, string resolutionKey)
        {
            displayMode = mode;
            if (resolutionKey == DisplayChoices.Desktop || DisplayChoices.TryParseSize(resolutionKey, out _, out _)) resolution = resolutionKey;
            ApplyWindow();
            ApplyPreferences();
        }

        private static FullScreenMode ToFullScreenMode(DisplayMode mode) =>
            mode == DisplayMode.Exclusive ? FullScreenMode.ExclusiveFullScreen : mode == DisplayMode.Borderless ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        /// <summary>How the window stands right now, in the settings' words.</summary>
        private static DisplayMode WindowModeNow()
        {
            switch (Screen.fullScreenMode)
            {
                case FullScreenMode.ExclusiveFullScreen: return DisplayMode.Exclusive;
                case FullScreenMode.FullScreenWindow: return DisplayMode.Borderless;
                default: return DisplayMode.Windowed;
            }
        }

        /// <summary>The desktop's size: the display the window is on, or the current resolution where that is not known.</summary>
        private static Vector2Int DesktopSize()
        {
            var display = Screen.mainWindowDisplayInfo;
            if (display.width > 0 && display.height > 0) return new Vector2Int(display.width, display.height);
            var current = Screen.currentResolution;
            return new Vector2Int(Mathf.Max(1, current.width), Mathf.Max(1, current.height));
        }

        /// <summary>The window's resolution as a record says it: the desktop's when it is the desktop's size.</summary>
        private static string ResolutionNow()
        {
            var desktop = DesktopSize();
            return Screen.width == desktop.x && Screen.height == desktop.y ? DisplayChoices.Desktop : DisplayChoices.SizeKey(Screen.width, Screen.height);
        }

        /// <summary>The size the recorded resolution means now.</summary>
        private Vector2Int WindowSize() =>
            DisplayChoices.TryParseSize(resolution, out int width, out int height) ? new Vector2Int(width, height) : DesktopSize();

        /// <summary>
        /// Applies the window - mode and size - and counts it. The editor's game view is not a window
        /// the game owns, so there the fields are the record of the choice and nothing is applied.
        /// </summary>
        private void ApplyWindow()
        {
            WindowApplications++;
            var size = WindowSize();
            if (!Application.isEditor) Screen.SetResolution(size.x, size.y, ToFullScreenMode(displayMode));
        }

        /// <summary>
        /// The record read again from the window as the settings open, so a window changed outside the
        /// settings (Alt+Enter, a monitor unplugged) is what they show. Not in the editor or a batch
        /// run, where the window is not the game's and the fields are the record.
        /// </summary>
        private void RefreshDisplayRecordFromWindow()
        {
            monitorChosen = -1;
            if (Application.isEditor || Application.isBatchMode) return;
            displayMode = WindowModeNow();
            resolution = ResolutionNow();
        }

        private void LoadDisplayPreferences()
        {
            // Through the session's store (A13); the defaults store answers each fallback, which is
            // what an isolated root has always read: the window as it is, VSync, edges panning.
            var store = Preferences;
            int levels = Mathf.Max(1, QualitySettings.names.Length);
            qualityTier = Mathf.Clamp(store.GetInt("Gamesim.Quality", QualitySettings.GetQualityLevel()), 0, levels - 1);
            // An older build's cap of 0 was VSync: it reads as VSync on with no limit (decision 12).
            DisplayChoices.FrameFromRecords(store.HasKey(DisplayChoices.VSyncKey) ? store.GetInt(DisplayChoices.VSyncKey, 1) : (int?)null,
                store.GetInt(DisplayChoices.FrameCapKey, 0), out vSync, out frameLimit);
            // An older build's full-screen flag reads as borderless or windowed; with no record at
            // all, the window as it stands. Read, never applied: Unity restores the window itself.
            displayMode = DisplayChoices.ModeFromRecords(store.GetString(DisplayChoices.DisplayModeKey, ""),
                store.HasKey(DisplayChoices.LegacyFullscreenKey) ? store.GetInt(DisplayChoices.LegacyFullscreenKey, 0) : (int?)null, WindowModeNow());
            string recorded = store.GetString(DisplayChoices.ResolutionKey, "");
            resolution = recorded == DisplayChoices.Desktop || DisplayChoices.TryParseSize(recorded, out _, out _) ? recorded : ResolutionNow();
            edgePan = store.GetInt("Gamesim.EdgePan", 1) == 1;
            compactHud = store.GetInt("Gamesim.CompactHud", 0) == 1;
            language = store.GetString("Gamesim.Language", Localisation.DefaultLanguage);
            Localisation.Load(language);
            language = Localisation.Language;
            LoadCameraPreferences();
        }

        private void ApplyDisplayPreferences()
        {
            if (qualityTier != QualitySettings.GetQualityLevel() && qualityTier < QualitySettings.names.Length)
                QualitySettings.SetQualityLevel(qualityTier, true);
            // VSync paces the frames when it is on; off, the limit does, and no limit is what a
            // benchmark wants and nothing else does.
            QualitySettings.vSyncCount = vSync ? 1 : 0;
            Application.targetFrameRate = vSync ? -1 : frameLimit;
            // Not the window: only the display controls apply it (ApplyWindow).
            if (cameraRig != null) cameraRig.EdgePan = edgePan;
            if (hud != null) hud.Compact = compactHud;
            if (Localisation.Language != language) { Localisation.Load(language); language = Localisation.Language; }
            // Kept in the session's store; ApplyPreferences saves it.
            var store = Preferences;
            store.SetString("Gamesim.Language", language);
            store.SetInt("Gamesim.Quality", qualityTier);
            store.SetInt(DisplayChoices.VSyncKey, vSync ? 1 : 0);
            store.SetInt(DisplayChoices.FrameCapKey, frameLimit);
            store.SetString(DisplayChoices.DisplayModeKey, DisplayChoices.ModeName(displayMode));
            store.SetString(DisplayChoices.ResolutionKey, resolution);
            store.SetInt("Gamesim.EdgePan", edgePan ? 1 : 0);
            store.SetInt("Gamesim.CompactHud", compactHud ? 1 : 0);
            ApplyCameraPreferences();
        }

        /// <summary>The resolutions the settings cycle: Desktop, and every size the display offers at 1280x720 or more.</summary>
        private List<string> ResolutionChoices() =>
            DisplayChoices.Resolutions(Screen.resolutions.Select(size => new KeyValuePair<int, int>(size.width, size.height)), resolution);

        private readonly List<DisplayInfo> displays = new List<DisplayInfo>();

        /// <summary>The displays the window may move to, read afresh; one or none on most machines and in every batch run.</summary>
        private List<DisplayInfo> Displays()
        {
            displays.Clear();
            try { Screen.GetDisplayLayout(displays); }
            catch (Exception) { displays.Clear(); }
            return displays;
        }

        /// <summary>Moves the window to the display at <paramref name="index"/>, centred when it is a window.</summary>
        private void MoveToMonitor(int index)
        {
            var layout = Displays();
            if (index < 0 || index >= layout.Count) return;
            WindowApplications++;
            monitorChosen = index;
            var target = layout[index];
            var position = displayMode == DisplayMode.Windowed
                ? new Vector2Int(Mathf.Max(0, (target.width - Screen.width) / 2), Mathf.Max(0, (target.height - Screen.height) / 2))
                : Vector2Int.zero;
            if (!Application.isEditor) Screen.MoveMainWindowTo(target, position);
        }

        /// <summary>The display block of the settings panel; each control cycles or flips one preference.</summary>
        private void DisplaySettings()
        {
            hud.Heading("Display");
            hud.Action(compactHud ? "Show full HUD" : "Use compact HUD",()=>SetCompactHud(!compactHud));
            hud.Paragraph("Compact HUD keeps the cast, next objective and controls visible. House details stay in the notebook and Overview.");
            hud.Action("Quality: " + QualityName(qualityTier) + "  (change)", () =>
            {
                qualityTier = (qualityTier + 1) % Mathf.Max(1, QualitySettings.names.Length); ApplyPreferences(); Render();
            });
            // The frame rate's limit, and VSync beside it (A12, the lead's decision 12).
            hud.Action(FrameRateCaption(frameLimit), () =>
            {
                int at = Array.IndexOf(DisplayChoices.FrameLimits, frameLimit);
                frameLimit = DisplayChoices.FrameLimits[(at + 1) % DisplayChoices.FrameLimits.Length]; ApplyPreferences(); Render();
                hud.FocusWhenWired(FrameRateCaption(frameLimit));
            });
            hud.Action(vSync ? VSyncOffCaption : VSyncOnCaption, () =>
            {
                SetVSync(!vSync); Render();
                hud.FocusWhenWired(vSync ? VSyncOffCaption : VSyncOnCaption);
            });
            hud.Paragraph("With VSync on the display paces the frames; the frame rate's limit holds with VSync off.");
            // The window: how it stands, its size and, with more than one, its monitor (A12).
            hud.Action(fullscreenToggleCaption, () =>
            {
                SetDisplay(Fullscreen ? DisplayMode.Windowed : DisplayMode.Borderless, resolution); Render();
                hud.FocusWhenWired(fullscreenToggleCaption);
            });
            hud.Action(DisplayModeCaption(displayMode), () =>
            {
                SetDisplay(DisplayChoices.NextMode(displayMode), resolution); Render();
                hud.FocusWhenWired(DisplayModeCaption(displayMode));
            });
            hud.Action(ResolutionCaption(resolution), () =>
            {
                SetDisplay(displayMode, DisplayChoices.Next(ResolutionChoices(), resolution)); Render();
                hud.FocusWhenWired(ResolutionCaption(resolution));
            });
            // Only with more than one display, as Language below: a control that cycles one is a dead control.
            var layout = Displays();
            if (layout.Count > 1)
            {
                int at = monitorChosen >= 0 ? monitorChosen : Mathf.Max(0, layout.IndexOf(Screen.mainWindowDisplayInfo));
                int count = layout.Count;
                hud.Action(MonitorCaption(at + 1, count), () =>
                {
                    MoveToMonitor((at + 1) % count); Render();
                    hud.FocusWhenWired(MonitorCaption((at + 1) % count + 1, count));
                });
            }
            hud.Action(edgePan ? "Stop the screen edges panning" : "Let the screen edges pan", () => { edgePan = !edgePan; ApplyPreferences(); Render(); });
            hud.Paragraph("The edges pan the camera only in full screen, where the cursor cannot leave the game.");
            // Only when a table ships: a control that cycles one language is a dead control.
            var languages = Localisation.Available();
            if (languages.Count > 1)
                hud.Action("Language: " + language + "  (change)", () =>
                {
                    int at = languages.IndexOf(language);
                    language = languages[(at + 1) % languages.Count]; ApplyPreferences(); Render();
                });
            // The camera's speed and tilt, after the display (A7).
            CameraSettings();
        }

        /// <summary>The full-screen toggle's words: the one press between a window and borderless full screen.</summary>
        private string fullscreenToggleCaption => Fullscreen ? PlayWindowedCaption : PlayFullScreenCaption;

        // ---------------------------------------------------------------- the first launch (A12, decision A-4)

        /// <summary>What the first launch's window rule decided in this session, for the player log; null before it ran.</summary>
        public static DisplayFirstRun.Decision? FirstRunDecision { get; private set; }

        /// <summary>
        /// The first launch's window, before the splash: borderless full screen at the desktop's
        /// resolution, recorded so the next launch is not a first one - in the machine's PlayerPrefs,
        /// or a playtest root's own preferences file, so each participant starts identically. Parses
        /// the command line itself: the verifier's install comes later (BeforeSceneLoad).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void ApplyFirstRunDisplay()
        {
            var args = Environment.GetCommandLineArgs();
            if (Application.isEditor || Application.isBatchMode)
            {
                FirstRunDecision = DisplayFirstRun.Decide(args, Application.isEditor, Application.isBatchMode, false, false);
                return;
            }
            var route = PreferenceRouting.ForLaunch(SaveRootOverride, PreferencesBesideSaves, args, out var root);
            var store = PreferenceStores.Open(route, root);
            var decision = DisplayFirstRun.Decide(args, false, false, store.HasKey(DisplayChoices.DisplayModeKey),
                store.HasKey(DisplayChoices.LegacyFullscreenKey), route == PreferenceRoute.Defaults);
            FirstRunDecision = decision;
            if (decision != DisplayFirstRun.Decision.Apply) return;
            var desktop = DesktopSize();
            Screen.SetResolution(desktop.x, desktop.y, FullScreenMode.FullScreenWindow);
            store.SetString(DisplayChoices.DisplayModeKey, DisplayChoices.ModeName(DisplayMode.Borderless));
            store.SetString(DisplayChoices.ResolutionKey, DisplayChoices.Desktop);
            store.Save();
            Debug.Log("Gamesim display first run: borderless full screen at " + DisplayChoices.SizeKey(desktop.x, desktop.y) + ".");
        }

        /// <summary>A play session starts without the last one's decision: statics outlive play sessions in the editor.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetFirstRunDecision() => FirstRunDecision = null;
    }
}
