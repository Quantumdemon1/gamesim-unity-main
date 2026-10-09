using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Gamesim.House;
using Gamesim.Simulation;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>Opt-in standalone QA workload. Inert unless both explicit verification and isolated-save arguments exist.</summary>
    public sealed partial class PortVerification : MonoBehaviour
    {
        public const int ProfileWidth = 1920, ProfileHeight = 1080;
        public const int ProfileFrameCap = -1, ProfileVSyncCount = 0;
        private string outputDirectory;
        private string profileFinishedUtc;
        private double seconds = 300;
        // --gamesim-house-size: profile a season of this many houseguests rather than the six the
        // scene starts with. The C-row thresholds were derived at six; the plan asks for sixteen.
        private int houseSize, measuredHouseSize;
        private string houseSizeNote = "";
        private readonly List<string> errors = new List<string>();
        private readonly List<float> frames = new List<float>(180000);
        private readonly List<long> allocations = new List<long>(180000);
        private readonly List<VerificationFrameEvidence> capturedFrames = new List<VerificationFrameEvidence>();
        private readonly HashSet<string> profileVisitedRooms = new HashSet<string>(StringComparer.Ordinal);
        private int profileRoomCount, profileRoomRequests, profileJournalRequests, profileSettingsRequests, profileStationRequests, profileSaveRequests;
        private int profileDisplaySampleCount, profileResolutionMismatchCount, profileFrameCapMismatchCount;
        private int profileDisplayModeMismatchCount;
        private int profileSampledFrameCap, profileSampledVSyncCount;
        private string profileSampledResolution, profileSampledDisplayMode;
        private ProfilerRecorder gc;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallWhenRequested()
        {
            if (Application.isEditor) return;
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("--gamesim-verify-creator") && !args.Contains("--gamesim-verify"))
            { Debug.LogError("Creator verification requires --gamesim-verify and an isolated absolute save root."); Application.Quit(2); return; }
            if ((args.Contains("--gamesim-verify-study") || args.Contains("--gamesim-verify-blocs") || args.Contains("--gamesim-verify-autonomy"))
                && (!args.Contains("--gamesim-verify") || !args.Contains("--gamesim-verify-season")))
            { Debug.LogError("Study/bloc/autonomy verification requires both --gamesim-verify and --gamesim-verify-season, plus an isolated absolute save root."); Application.Quit(2); return; }
            // The stress house is verification-only and asked for twice: a size no roster seats AND the flag.
            // The refusal judges the size as written; only an ordinary profile's size is clamped, below.
            bool sized = VerificationPerformance.TryReadHouseSize(args, out int askedHouseSize);
            string stressRefusal = VerificationPerformance.StressRosterRefusal(args, askedHouseSize);
            if (stressRefusal != null) { Debug.LogError(stressRefusal); Application.Quit(2); return; }
            if (!args.Contains("--gamesim-verify")) return;
            int root = Array.IndexOf(args, "--gamesim-save-root");
            if (root < 0 || root + 1 >= args.Length || !Path.IsPathRooted(args[root + 1]))
            { Debug.LogError("Verification requires an explicit absolute --gamesim-save-root."); Application.Quit(2); return; }
            EpisodeDirector.SaveRootOverride = Path.GetFullPath(args[root + 1]);
            var owner = new GameObject("Gamesim opt-in standalone verification");
            DontDestroyOnLoad(owner);
            var runner = owner.AddComponent<PortVerification>();
            runner.outputDirectory = EpisodeDirector.SaveRootOverride;
            runner.verifySeason = args.Contains("--gamesim-verify-season");
            runner.verifyStudy = args.Contains("--gamesim-verify-study");
            runner.verifyBlocs = args.Contains("--gamesim-verify-blocs");
            runner.verifyAutonomy = args.Contains("--gamesim-verify-autonomy");
            runner.verifyCreator = args.Contains("--gamesim-verify-creator");
            runner.lookSheet = args.Contains("--gamesim-look-sheet");
            int duration = Array.IndexOf(args, "--gamesim-profile-seconds");
            if (duration >= 0 && duration + 1 < args.Length && double.TryParse(args[duration + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                runner.seconds = Math.Clamp(parsed, 10, 1800);
            runner.houseSize = sized ? Math.Clamp(askedHouseSize, 3, 16) : 0;
            runner.stressRoster = args.Contains(VerificationPerformance.StressRosterArgument);
        }

        private IEnumerator Start()
        {
            Application.logMessageReceived += CollectError;
            Application.runInBackground = true;
            Directory.CreateDirectory(outputDirectory);
            // Every frame before the steady sample is the startup, reported stage by stage beside it.
            BeginStartupStage("bootstrap");
            // The scene's own season began building with the scene; a season the profile starts moves this.
            seasonStartSeconds = Time.realtimeSinceStartupAsDouble;
            EpisodeDirector director = null;
            double deadline = Time.realtimeSinceStartupAsDouble + 35;
            while ((director == null || !director.IsReady) && Time.realtimeSinceStartupAsDouble < deadline)
            { director = FindAnyObjectByType<EpisodeDirector>(); yield return null; }
            if (director == null || !director.IsReady)
            { errors.Add("The bootstrap did not reach a ready episode."); Finish(0, false); yield break; }
            readySeconds = Time.realtimeSinceStartupAsDouble;
            if (verifyCreator || lookSheet) startupStage = -1;
            if (verifyCreator) { yield return RunCreatorVerification(director); yield break; }
            // The look sheet is its own mode: twelve captures and a report, no profile, no recorded walk.
            if (lookSheet) { yield return RunLookSheet(); FinishLookSheet(); yield break; }
            // Uncapped from here, so the startup's frames are its cost and not the display's interval.
            director.SetFrameCap(ProfileFrameCap);
            BeginStartupStage("season");
            var player = FindAnyObjectByType<HousePlayerController>();
            var rooms = FindObjectsByType<HouseRoomMarker>().OrderBy(room => room.RoomName, StringComparer.Ordinal).ToArray();
            profileRoomCount = rooms.Length;
            if (player == null || rooms.Length < 5) errors.Add("Expected a navigable player and five room markers.");
            profileSeason = VerificationPerformance.ProfileSeason(houseSize, stressRoster);
            string sceneSession = director.Snapshot.sessionId;
            if (profileSeason == VerificationProfileSeason.Stress)
            {
                // The sixteen-person stress run: a house no roster seats, from both rosters, and only
                // here (VerificationPerformance.StressRosterRefusal admitted the size at launch).
                houseSizeNote = "Verification-only stress house: " + houseSize + " houseguests from both rosters "
                    + "(SeasonBuilder.CreateVerificationStressHouse); no roster seats more than " + SeasonBuilder.LargestRosterHouse + ".";
                seasonStartSeconds = Time.realtimeSinceStartupAsDouble;
                director.StartVerificationStressSeason(houseSize);
                for (int i = 0; i < 30; i++) yield return null;
                if (director.Snapshot.contestants.Count != houseSize)
                    errors.Add("The stress house did not start: " + director.Snapshot.contestants.Count + " of " + houseSize + ".");
            }
            else if (profileSeason == VerificationProfileSeason.Director)
            {
                // Every size asked for is a season the director starts, even six: the scene's own six
                // (ContentCatalog's bootstrap) plays with every rule off - no story, read, levers, week,
                // economy, agency, finale or commitments - so keeping it would measure a different game
                // from the twelve and the sixteen.
                // A roster seats twelve, and the builder will not pad a season with the other
                // roster to reach a number (SeasonBuilder.LargestHouse). Sixteen is what a save may
                // hold, not what a season can start with, so a larger request profiles the largest
                // house there is and the report says so, rather than failing a run that measured
                // exactly what it could.
                var choice = new SeasonBuilder.Choice { HouseSize = houseSize };
                int seated = SeasonBuilder.ClampHouseSize(choice.Roster, houseSize);
                if (seated != houseSize)
                    houseSizeNote = "The " + CastTemplates.RosterName(choice.Roster) + " roster seats " + seated
                        + "; profiled at " + seated + " (" + houseSize + " requested).";
                choice.HouseSize = seated;
                seasonStartSeconds = Time.realtimeSinceStartupAsDouble;
                director.StartSeason(choice);
                for (int i = 0; i < 30; i++) yield return null;
                if (director.Snapshot.contestants.Count != seated)
                    errors.Add("The requested house size did not start: " + director.Snapshot.contestants.Count + " of " + seated + ".");
            }
            if (profileSeason != VerificationProfileSeason.Scene && director.Snapshot.sessionId == sceneSession)
                errors.Add("The profile's season did not start: the scene's own season is still installed.");
            // StartSeason seeds from the clock: the seed and session say which season this run measured.
            seasonSeed = director.Snapshot.seed;
            seasonSessionId = director.Snapshot.sessionId;
            measuredHouseSize = director.Snapshot.contestants.Count;
            // The opening put away and every body built before anything is measured or captured.
            yield return SettleTheHouse();
            BeginStartupStage("verification");
            // Started a stage early, so the counters hold a frame's sample when the steady sample reads them.
            StartMemoryRecorders();
            // A benchmark, so uncapped: the display preference defaults to VSync, which would make
            // every windowed profile read as 16.7 ms and say nothing about the frame's cost. Set
            // as the director's own preference, because the workload toggles other preferences
            // and every toggle re-applies the lot.
            director.SetFrameCap(ProfileFrameCap);
            // Verification plays the reveals at the quick pace: the look sheet waits a fixed time
            // for the key ceremony's block and a framing's end, and a suspenseful card outlasts both.
            director.SetCeremonyPace(Presentation.CeremonyPace.Quick);
            director.SaveNow();
            bool graphical = !Application.isBatchMode && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            if (!graphical) errors.Add("Graphical performance evidence requires a windowed player without -batchmode/-nographics; a graphics device alone does not prove presentation.");
            if (graphical)
            {
                // Force the hidden-launched desktop window to allocate its presentable backbuffer.
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
                for (int i = 0; i < 15; i++) yield return null;
                // The profile's window through the settings' own call (A12, the lead's decision 13):
                // what the in-game option sets is what is measured.
                director.SetDisplay(DisplayMode.Windowed, ProfileWidth, ProfileHeight);
            }
            // A ready simulation can precede the first presentable frame / Unity splash completion.
            double captureDeadline = Time.realtimeSinceStartupAsDouble + 15;
            while (graphical && !SplashScreen.isFinished && Time.realtimeSinceStartupAsDouble < captureDeadline) yield return null;
            for (int i = 0; i < 60; i++) yield return null;
            if (graphical) yield return CaptureVerifiedFrame(Path.Combine(outputDirectory, "house.png"), capturedFrames.Add, errors.Add);
            for (int i = 0; i < 20; i++) yield return null;
            director.OpenSettings();
            for (int i = 0; i < 5; i++) yield return null;
            if (graphical) yield return CaptureVerifiedFrame(Path.Combine(outputDirectory, "settings.png"), capturedFrames.Add, errors.Add);
            for (int i = 0; i < 5; i++) yield return null;
            if (graphical)
            {
                var larger = director.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == "Use larger text");
                if (larger != null) larger.onClick.Invoke();
                for (int i = 0; i < 5; i++) yield return null;
                yield return CaptureVerifiedFrame(Path.Combine(outputDirectory, "settings-large.png"), capturedFrames.Add, errors.Add);
                for (int i = 0; i < 10; i++) yield return null;
                foreach (int height in new[] { 720, 800 })
                {
                    Screen.SetResolution(1280, height, FullScreenMode.Windowed);
                    for (int i = 0; i < 15; i++) yield return null;
                    yield return CaptureVerifiedFrame(Path.Combine(outputDirectory, "settings-large-1280x" + height + ".png"), capturedFrames.Add, errors.Add);
                    for (int i = 0; i < 10; i++) yield return null;
                }
                var standard = director.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == "Use standard text");
                if (standard != null) standard.onClick.Invoke();
                director.SetDisplay(DisplayMode.Windowed, ProfileWidth, ProfileHeight);
                for (int i = 0; i < 15; i++) yield return null;
            }
            director.ClosePanels();
            gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            double started = Time.realtimeSinceStartupAsDouble;
            BeginSteadySample(started);
            double nextAction = started, nextSave = started + 60;
            int action = 0;
            while (Time.realtimeSinceStartupAsDouble - started < seconds)
            {
                RecordProfileDisplaySample(Screen.width, Screen.height, Application.targetFrameRate, QualitySettings.vSyncCount, Screen.fullScreenMode);
                frames.Add(Time.unscaledDeltaTime * 1000);
                if (gc.Valid) allocations.Add(gc.LastValue);
                double now = Time.realtimeSinceStartupAsDouble;
                SampleMemoryDuringSteadySample(now);
                if (now >= nextAction)
                {
                    director.ClosePanels();
                    var step = VerificationProfilePlan.Step(action++, rooms.Length);
                    switch (step.Action)
                    {
                        case VerificationProfileAction.Room:
                            profileRoomRequests++;
                            var room = rooms[step.RoomIndex];
                            profileVisitedRooms.Add(room.RoomName);
                            if (player == null || !player.TryMoveTo(room.transform.position))
                                errors.Add("Room route was not reachable: " + room.RoomName);
                            break;
                        case VerificationProfileAction.Journal:
                            profileJournalRequests++; director.OpenJournal(); break;
                        case VerificationProfileAction.Settings:
                            profileSettingsRequests++; director.OpenSettings(); break;
                        case VerificationProfileAction.Station:
                            profileStationRequests++; director.GoToStation(); break;
                    }
                    nextAction = now + 10;
                }
                if (now >= nextSave) { profileSaveRequests++; director.SaveNow(); nextSave = now + 60; }
                yield return null;
            }
            // End the measured interval before post-workload captures or the optional season.
            double profileElapsed = Time.realtimeSinceStartupAsDouble - started;
            profileFinishedUtc = DateTime.UtcNow.ToString("O");
            if (seconds >= (profileRoomCount + 3) * 10d && !ProfileWorkloadCycleCompleted)
                errors.Add("The timed profile did not request every room, notebook, settings and station action in its scheduled cycle.");
            gc.Dispose();
            EndSteadySample();
            director.ClosePanels();
            for (int i = 0; i < 5; i++) yield return null;
            if (graphical) yield return CaptureVerifiedFrame(Path.Combine(outputDirectory, "house-after-workload.png"), capturedFrames.Add, errors.Add);
            for (int i = 0; i < 10; i++) yield return null;
            // The optional season is a separate functional workload, never part of the frame sample.
            if (verifySeason) yield return RunSeasonVerification(graphical);
            Finish(profileElapsed, graphical);
        }

        private void CollectError(string text, string stack, LogType type)
        {
            if (seasonRunning)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    RecordSeasonError(text + "\n" + stack);
                return;
            }
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && errors.Count < 30)
                errors.Add(text + "\n" + stack);
        }

        private void Finish(double measured, bool graphical)
        {
            WriteRawFrameTimes();
            var report = CompleteProfileReport(measured, graphical, Application.isBatchMode, Debug.isDebugBuild);
            File.WriteAllText(Path.Combine(outputDirectory, "verification.json"), JsonUtility.ToJson(report, true));
            Debug.Log("Gamesim standalone profile " + report.status + "; functional season " + report.seasonStatus
                + "; overall " + report.overallStatus + ": " + Path.Combine(outputDirectory, "verification.json"));
            Application.Quit(report.overallStatus == "Passed" ? 0 : 1);
        }

        // Record every measured frame, not only the requested size or the window after a season run.
        private void RecordProfileDisplaySample(int width, int height, int frameCap, int vSyncCount, FullScreenMode displayMode)
        {
            if (profileDisplaySampleCount == 0)
            {
                profileSampledResolution = width + "x" + height;
                profileSampledFrameCap = frameCap;
                profileSampledVSyncCount = vSyncCount;
                profileSampledDisplayMode = displayMode.ToString();
            }
            profileDisplaySampleCount++;
            if (width != ProfileWidth || height != ProfileHeight) profileResolutionMismatchCount++;
            if (frameCap != ProfileFrameCap || vSyncCount != ProfileVSyncCount) profileFrameCapMismatchCount++;
            if (displayMode != FullScreenMode.Windowed) profileDisplayModeMismatchCount++;
        }

        // Kept separate from file output and Quit so native Edit tests can check the report contract.
        private VerificationReport CompleteProfileReport(double measured, bool graphical, bool batchMode, bool developmentBuild)
        {
            // Sorted copies: the frames stay in the order measured, which is the raw distribution kept beside the report.
            var steady = VerificationFrameSummary.Of("steady", frames);
            var sortedAllocations = new List<long>(allocations);
            sortedAllocations.Sort();
            var reportErrors = new List<string>(errors);
            if (profileDisplaySampleCount == 0 || profileDisplaySampleCount != frames.Count)
                reportErrors.Add("Every measured frame requires actual resolution and frame-cap evidence.");
            if (profileResolutionMismatchCount > 0)
                reportErrors.Add("The actual sampled window did not remain at the requested 1920x1080 resolution.");
            if (profileFrameCapMismatchCount > 0)
                reportErrors.Add("The sampled profile did not remain uncapped with VSync disabled.");
            if (profileDisplayModeMismatchCount > 0)
                reportErrors.Add("The actual sampled display mode did not remain Windowed.");
            bool passed = reportErrors.Count == 0 && measured >= seconds && frames.Count > 100
                && !batchMode && graphical && capturedFrames.Count == 6 && capturedFrames.All(frame => frame.rendered);
            var report = new VerificationReport
            {
                status = passed ? "Passed" : "Failed", finishedUtc = profileFinishedUtc ?? DateTime.UtcNow.ToString("O"),
                overallFinishedUtc = DateTime.UtcNow.ToString("O"),
                overallStatus = passed && (!verifySeason || seasonPassed) ? "Passed" : "Failed",
                seasonStatus = verifySeason ? this.seasonReport == null ? "Not run" : seasonPassed ? "Passed" : "Failed" : "Not requested",
                seasonReport = verifySeason && this.seasonReport != null ? Path.Combine(outputDirectory, "season-verification.json") : null,
                studyRequested = verifyStudy,
                studyStatus = verifyStudy ? this.studyReport == null ? "Not run" : this.studyReport.status : "Not requested",
                blocsRequested = verifyBlocs,
                blocsStatus = verifyBlocs ? this.blocReport == null ? "Not run" : this.blocReport.status : "Not requested",
                autonomyRequested = verifyAutonomy,
                autonomyStatus = verifyAutonomy ? this.autonomyReport == null ? "Not run" : this.autonomyReport.status : "Not requested",
                workload = "Timed standalone room-navigation, notebook/settings and station requests, plus isolated local saves. Actual requests are counted; short smoke runs may not complete a cycle. Not a human playtest or full-season timing sample. A Passed status validates workload/evidence, not the 60 FPS performance target; that verdict is performanceAcceptance alone.",
                profileRoomCount = profileRoomCount, profileRoomsRequested = profileVisitedRooms.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                profileRoomRequests = profileRoomRequests, profileJournalRequests = profileJournalRequests,
                profileSettingsRequests = profileSettingsRequests, profileStationRequests = profileStationRequests, profileSaveRequests = profileSaveRequests,
                profileWorkloadCycleRequired = seconds >= (profileRoomCount + 3) * 10d,
                profileWorkloadCycleCompleted = ProfileWorkloadCycleCompleted,
                requestedSeconds = seconds, measuredSeconds = measured, frameCount = frames.Count,
                frameMedianMs = steady.medianMs, frameP95Ms = steady.p95Ms, frameP99Ms = steady.p99Ms,
                gcCounterAvailable = sortedAllocations.Count > 0, gcMedianBytes = Percentile(sortedAllocations, .5), gcP95Bytes = Percentile(sortedAllocations, .95),
                graphical = graphical, batchMode = batchMode, capturedFrames = capturedFrames,
                resolution = Screen.width + "x" + Screen.height, houseSize = measuredHouseSize,
                requestedResolution = ProfileWidth + "x" + ProfileHeight, sampledResolution = profileSampledResolution,
                sampledDisplayFrames = profileDisplaySampleCount, sampledResolutionMismatchCount = profileResolutionMismatchCount,
                requestedFrameCap = ProfileFrameCap, requestedVSyncCount = ProfileVSyncCount,
                sampledFrameCap = profileSampledFrameCap, sampledVSyncCount = profileSampledVSyncCount,
                sampledFrameCapMismatchCount = profileFrameCapMismatchCount,
                requestedDisplayMode = FullScreenMode.Windowed.ToString(), requestedDisplayRoute = EpisodeDirector.DisplayRoute,
                sampledDisplayMode = profileSampledDisplayMode,
                sampledDisplayModeMismatchCount = profileDisplayModeMismatchCount,
                uncapped = profileDisplaySampleCount > 0 && profileFrameCapMismatchCount == 0,
                houseSizeRequested = houseSize, houseSizeNote = houseSizeNote,
                unityVersion = Application.unityVersion, processor = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                systemMemoryMB = SystemInfo.systemMemorySize, graphicsMemoryMB = SystemInfo.graphicsMemorySize,
                developmentBuild = developmentBuild, errors = reportErrors.ToArray(), saveDirectory = outputDirectory
            };
            CompletePerformanceReport(report, steady, measured, graphical, batchMode, developmentBuild);
            return report;
        }

        private bool ProfileWorkloadCycleCompleted => profileVisitedRooms.Count == profileRoomCount
            && profileJournalRequests > 0 && profileSettingsRequests > 0 && profileStationRequests > 0;
        private static double Percentile(List<long> values, double p) => values.Count == 0 ? 0 : values[Math.Min(values.Count - 1, (int)Math.Ceiling(values.Count * p) - 1)];
        private void OnDestroy() { Application.logMessageReceived -= CollectError; gc.Dispose(); DisposeMemoryRecorders(); }

        [Serializable] private sealed class VerificationReport
        {
            public string status, finishedUtc, workload, resolution, unityVersion, processor, gpu, saveDirectory;
            public string requestedResolution, sampledResolution;
            // The verdict against the owner's target (VerificationPerformance.Verdict): "Met", "Not met",
            // or "Not assessed: <reasons>". Never implied by status.
            public string performanceAcceptance, performanceAcceptanceBasis;
            public double performanceP95LimitMs, performanceP99LimitMs, performanceMinimumSeconds;
            // The steady sample's distribution, beyond its median/p95/p99, and where the raw frames are.
            public VerificationFrameSummary steady;
            public double frameMeanMs, frameMinMs, frameMaxMs, frameP90Ms, frameP999Ms, averageFps;
            public int slowFramesOver16_7Ms, slowFramesOver33_3Ms, slowFramesOver50Ms;
            public float[] frameHistogramUpperMs;
            public int[] frameHistogramCounts;
            public string rawFrameTimesFile, rawStartupFrameTimesFile;
            // Everything before the steady sample, stage by stage: bootstrap, season, bodies, verification.
            public VerificationFrameSummary startup;
            public List<VerificationFrameSummary> startupStages;
            // bodyAssemblySeconds: from the measured season's start (seasonStartedSeconds) until every body was built.
            public double startupReadySeconds, startupSampleBeganSeconds, bodyAssemblySeconds;
            public bool bodiesAssembled, openingSkipped;
            public int bodiesAssemblingAtSampleStart;
            // Memory through the steady sample, in bytes (-1 unavailable; the driver estimate is 0 in release players).
            public VerificationMemorySample memoryAtSampleStart, memoryAtSampleEnd, memoryPeak;
            public int memorySamples;
            public bool gfxDriverMemoryAvailable;
            // What was measured: the build, its quality and render scale, the display and the machine.
            public string buildVersion, buildGuid, productName, qualityLevelName, renderPipeline;
            public int qualityLevel;
            public float renderScale;
            public string displayResolution, graphicsDeviceType, graphicsDeviceVersion, graphicsDeviceVendor, operatingSystem;
            public double displayRefreshHz;
            public int processorCount, processorFrequencyMHz;
            public bool stressRoster;
            // Which season was measured: "scene" (the bootstrap, no size asked for), "director" or "stress";
            // its seed and session, since a started season seeds from the clock; and when it was started.
            public string seasonSource, sessionId;
            public long seasonSeed;
            public double seasonStartedSeconds;
            public string requestedDisplayMode, sampledDisplayMode;
            // How the profile's window was set (A12): through the settings' own call, EpisodeDirector.SetDisplay.
            public string requestedDisplayRoute;
            public int sampledDisplayModeMismatchCount;
            public int sampledDisplayFrames, sampledResolutionMismatchCount, requestedFrameCap, requestedVSyncCount;
            public int sampledFrameCap, sampledVSyncCount, sampledFrameCapMismatchCount;
            public bool uncapped;
            public string overallStatus, overallFinishedUtc, seasonStatus, seasonReport, studyStatus, blocsStatus, autonomyStatus;
            public bool studyRequested, blocsRequested, autonomyRequested;
            public double requestedSeconds, measuredSeconds, frameMedianMs, frameP95Ms, frameP99Ms, gcMedianBytes, gcP95Bytes;
            public int frameCount, systemMemoryMB, graphicsMemoryMB, houseSize, houseSizeRequested;
            public string houseSizeNote;
            public int profileRoomCount, profileRoomRequests, profileJournalRequests, profileSettingsRequests, profileStationRequests, profileSaveRequests;
            public string[] profileRoomsRequested;
            public bool profileWorkloadCycleRequired, profileWorkloadCycleCompleted;
            public bool graphical, batchMode, developmentBuild, gcCounterAvailable;
            public List<VerificationFrameEvidence> capturedFrames;
            public string[] errors;
        }
    }
}
