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
        private string outputDirectory;
        private string profileFinishedUtc;
        private double seconds = 300;
        // --gamesim-house-size: profile a season of this many houseguests rather than the six the
        // scene starts with. The C-row thresholds were derived at six; the plan asks for sixteen.
        private int houseSize, measuredHouseSize;
        private readonly List<string> errors = new List<string>();
        private readonly List<float> frames = new List<float>(180000);
        private readonly List<long> allocations = new List<long>(180000);
        private ProfilerRecorder gc;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallWhenRequested()
        {
            if (Application.isEditor) return;
            var args = Environment.GetCommandLineArgs();
            if ((args.Contains("--gamesim-verify-study") || args.Contains("--gamesim-verify-blocs") || args.Contains("--gamesim-verify-autonomy"))
                && (!args.Contains("--gamesim-verify") || !args.Contains("--gamesim-verify-season")))
            { Debug.LogError("Study/bloc/autonomy verification requires both --gamesim-verify and --gamesim-verify-season, plus an isolated absolute save root."); Application.Quit(2); return; }
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
            int duration = Array.IndexOf(args, "--gamesim-profile-seconds");
            if (duration >= 0 && duration + 1 < args.Length && double.TryParse(args[duration + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                runner.seconds = Math.Clamp(parsed, 10, 1800);
            int size = Array.IndexOf(args, "--gamesim-house-size");
            if (size >= 0 && size + 1 < args.Length && int.TryParse(args[size + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSize))
                runner.houseSize = Math.Clamp(parsedSize, 3, 16);
        }

        private IEnumerator Start()
        {
            Application.logMessageReceived += CollectError;
            Application.runInBackground = true;
            Directory.CreateDirectory(outputDirectory);
            EpisodeDirector director = null;
            double deadline = Time.realtimeSinceStartupAsDouble + 35;
            while ((director == null || !director.IsReady) && Time.realtimeSinceStartupAsDouble < deadline)
            { director = FindAnyObjectByType<EpisodeDirector>(); yield return null; }
            if (director == null || !director.IsReady)
            { errors.Add("The bootstrap did not reach a ready episode."); Finish(0, false); yield break; }
            var player = FindAnyObjectByType<HousePlayerController>();
            var rooms = FindObjectsByType<HouseRoomMarker>().OrderBy(room => room.RoomName, StringComparer.Ordinal).ToArray();
            if (player == null || rooms.Length < 5) errors.Add("Expected a navigable player and five room markers.");
            if (houseSize > 0 && director.Snapshot.contestants.Count != houseSize)
            {
                director.StartSeason(new SeasonBuilder.Choice { HouseSize = houseSize });
                for (int i = 0; i < 30; i++) yield return null;
                if (director.Snapshot.contestants.Count != houseSize)
                    errors.Add("The requested house size did not start: " + director.Snapshot.contestants.Count + " of " + houseSize + ".");
            }
            measuredHouseSize = director.Snapshot.contestants.Count;
            director.SaveNow();
            bool graphical = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            if (graphical)
            {
                // Force the hidden-launched desktop window to allocate its presentable backbuffer.
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
                for (int i = 0; i < 15; i++) yield return null;
                Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            }
            // A ready simulation can precede the first presentable frame / Unity splash completion.
            double captureDeadline = Time.realtimeSinceStartupAsDouble + 15;
            while (graphical && !SplashScreen.isFinished && Time.realtimeSinceStartupAsDouble < captureDeadline) yield return null;
            for (int i = 0; i < 60; i++) yield return null;
            if (graphical) ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, "house.png"));
            for (int i = 0; i < 20; i++) yield return null;
            director.OpenSettings();
            for (int i = 0; i < 5; i++) yield return null;
            if (graphical) ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, "settings.png"));
            for (int i = 0; i < 5; i++) yield return null;
            if (graphical)
            {
                var larger = director.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == "Use larger text");
                if (larger != null) larger.onClick.Invoke();
                for (int i = 0; i < 5; i++) yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, "settings-large.png"));
                for (int i = 0; i < 10; i++) yield return null;
                foreach (int height in new[] { 720, 800 })
                {
                    Screen.SetResolution(1280, height, FullScreenMode.Windowed);
                    for (int i = 0; i < 15; i++) yield return null;
                    ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, "settings-large-1280x" + height + ".png"));
                    for (int i = 0; i < 10; i++) yield return null;
                }
                var standard = director.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == "Use standard text");
                if (standard != null) standard.onClick.Invoke();
                Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
                for (int i = 0; i < 15; i++) yield return null;
            }
            director.ClosePanels();
            gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            double started = Time.realtimeSinceStartupAsDouble;
            double nextAction = started, nextSave = started + 60;
            int action = 0;
            while (Time.realtimeSinceStartupAsDouble - started < seconds)
            {
                frames.Add(Time.unscaledDeltaTime * 1000);
                if (gc.Valid) allocations.Add(gc.LastValue);
                double now = Time.realtimeSinceStartupAsDouble;
                if (now >= nextAction)
                {
                    director.ClosePanels();
                    int step = action++ % 8;
                    if (step < rooms.Length && player != null)
                    {
                        if (!player.TryMoveTo(rooms[step].transform.position)) errors.Add("Room route was not reachable: " + rooms[step].RoomName);
                    }
                    else if (step == 5) director.OpenJournal();
                    else if (step == 6) director.OpenSettings();
                    else director.GoToStation();
                    nextAction = now + 10;
                }
                if (now >= nextSave) { director.SaveNow(); nextSave = now + 60; }
                yield return null;
            }
            gc.Dispose();
            director.ClosePanels();
            for (int i = 0; i < 5; i++) yield return null;
            if (graphical) ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, "house-after-workload.png"));
            for (int i = 0; i < 10; i++) yield return null;
            double profileElapsed = Time.realtimeSinceStartupAsDouble - started;
            profileFinishedUtc = DateTime.UtcNow.ToString("O");
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
            frames.Sort(); allocations.Sort();
            bool passed = errors.Count == 0 && measured >= seconds && frames.Count > 100;
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
                workload = "Standalone five-room navigation, notebook/settings rebuilds, and isolated local saves. Not a human playtest or full-season timing sample.",
                requestedSeconds = seconds, measuredSeconds = measured, frameCount = frames.Count,
                frameMedianMs = Percentile(frames, .5), frameP95Ms = Percentile(frames, .95), frameP99Ms = Percentile(frames, .99),
                gcCounterAvailable = allocations.Count > 0, gcMedianBytes = Percentile(allocations, .5), gcP95Bytes = Percentile(allocations, .95),
                graphical = graphical, resolution = Screen.width + "x" + Screen.height, houseSize = measuredHouseSize,
                unityVersion = Application.unityVersion, processor = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                systemMemoryMB = SystemInfo.systemMemorySize, graphicsMemoryMB = SystemInfo.graphicsMemorySize,
                developmentBuild = Debug.isDebugBuild, errors = errors.ToArray(), saveDirectory = outputDirectory
            };
            File.WriteAllText(Path.Combine(outputDirectory, "verification.json"), JsonUtility.ToJson(report, true));
            Debug.Log("Gamesim standalone profile " + report.status + "; functional season " + report.seasonStatus
                + "; overall " + report.overallStatus + ": " + Path.Combine(outputDirectory, "verification.json"));
            Application.Quit(passed && (!verifySeason || seasonPassed) ? 0 : 1);
        }

        private static double Percentile(List<float> values, double p) => values.Count == 0 ? 0 : values[Math.Min(values.Count - 1, (int)Math.Ceiling(values.Count * p) - 1)];
        private static double Percentile(List<long> values, double p) => values.Count == 0 ? 0 : values[Math.Min(values.Count - 1, (int)Math.Ceiling(values.Count * p) - 1)];
        private void OnDestroy() { Application.logMessageReceived -= CollectError; gc.Dispose(); }

        [Serializable] private sealed class VerificationReport
        {
            public string status, finishedUtc, workload, resolution, unityVersion, processor, gpu, saveDirectory;
            public string overallStatus, overallFinishedUtc, seasonStatus, seasonReport, studyStatus, blocsStatus, autonomyStatus;
            public bool studyRequested, blocsRequested, autonomyRequested;
            public double requestedSeconds, measuredSeconds, frameMedianMs, frameP95Ms, frameP99Ms, gcMedianBytes, gcP95Bytes;
            public int frameCount, systemMemoryMB, graphicsMemoryMB, houseSize;
            public bool graphical, developmentBuild, gcCounterAvailable;
            public string[] errors;
        }
    }
}
