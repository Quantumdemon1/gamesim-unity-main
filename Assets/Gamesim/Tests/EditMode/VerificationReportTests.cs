using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    public sealed class VerificationReportTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject owner;
        private PortVerification runner;
        private string temporary;

        [SetUp]
        public void SetUp()
        {
            temporary = Path.Combine(Path.GetTempPath(), "gamesim-qa-report-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            owner = new GameObject("Isolated verification report contract");
            runner = owner.AddComponent<PortVerification>();
        }

        [TearDown]
        public void TearDown()
        {
            if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(8)]
        public void EveryCycleVisitsAllRoomsThenJournalSettingsAndStation(int rooms)
        {
            for (int cycle = 0; cycle < 2; cycle++)
            {
                var steps = Enumerable.Range(cycle * (rooms + 3), rooms + 3)
                    .Select(action => VerificationProfilePlan.Step(action, rooms)).ToArray();
                Assert.That(steps.Take(rooms).Select(step => step.Action),
                    Is.All.EqualTo(VerificationProfileAction.Room));
                Assert.That(steps.Take(rooms).Select(step => step.RoomIndex), Is.EqualTo(Enumerable.Range(0, rooms)));
                Assert.That(steps.Skip(rooms).Select(step => step.Action), Is.EqualTo(new[]
                { VerificationProfileAction.Journal, VerificationProfileAction.Settings, VerificationProfileAction.Station }));
                Assert.That(steps.Skip(rooms).Select(step => step.RoomIndex), Is.All.EqualTo(-1));
            }
        }

        [TestCase(-1, 8)]
        [TestCase(0, -1)]
        [TestCase(0, int.MaxValue)]
        public void InvalidScheduleArgumentsAreRejected(int action, int rooms)
            => Assert.Throws<ArgumentOutOfRangeException>(() => VerificationProfilePlan.Step(action, rooms));

        [Test]
        public void ProfileCoverageRequiresEveryRoomAndEveryUiStationAction()
        {
            Field("profileRoomCount").SetValue(runner, 8);
            var visited = (HashSet<string>)Field("profileVisitedRooms").GetValue(runner);
            for (int i = 0; i < 8; i++) visited.Add("room-" + i);
            var coverage = typeof(PortVerification).GetProperty("ProfileWorkloadCycleCompleted", PrivateInstance);
            Assert.That(coverage, Is.Not.Null);
            Assert.That((bool)coverage.GetValue(runner), Is.False, "Room routing alone is not the promised UI workload.");
            var actions = new[] { "profileJournalRequests", "profileSettingsRequests", "profileStationRequests" };
            foreach (string action in actions) Field(action).SetValue(runner, 1);
            Assert.That((bool)coverage.GetValue(runner), Is.True);
            foreach (string action in actions)
            {
                Field(action).SetValue(runner, 0);
                Assert.That((bool)coverage.GetValue(runner), Is.False, action);
                Field(action).SetValue(runner, 1);
            }
            visited.Remove("room-7");
            Assert.That((bool)coverage.GetValue(runner), Is.False, "An unvisited room cannot be hidden by UI requests.");
        }

        [Test]
        public void ProfileTargetIsExplicit1080pUncappedWithoutVSync()
        {
            Assert.That(PortVerification.ProfileWidth, Is.EqualTo(1920));
            Assert.That(PortVerification.ProfileHeight, Is.EqualTo(1080));
            Assert.That(PortVerification.ProfileFrameCap, Is.EqualTo(-1));
            Assert.That(PortVerification.ProfileVSyncCount, Is.Zero);
        }

        [Test]
        public void Complete1080pSamplingReportsSettingsButDoesNotAwardSixtyFpsAcceptance()
        {
            InstallProfileEvidence();
            var report = ProfileReport();
            Assert.That((string)report["status"], Is.EqualTo("Passed"));
            Assert.That((string)report["overallStatus"], Is.EqualTo("Passed"));
            Assert.That((string)report["requestedResolution"], Is.EqualTo("1920x1080"));
            Assert.That((string)report["sampledResolution"], Is.EqualTo("1920x1080"));
            Assert.That((int)report["sampledDisplayFrames"], Is.EqualTo((int)report["frameCount"]).And.EqualTo(101));
            Assert.That((int)report["sampledResolutionMismatchCount"], Is.Zero);
            Assert.That((int)report["requestedFrameCap"], Is.EqualTo(-1));
            Assert.That((int)report["sampledFrameCap"], Is.EqualTo(-1));
            Assert.That((int)report["requestedVSyncCount"], Is.Zero);
            Assert.That((int)report["sampledVSyncCount"], Is.Zero);
            Assert.That((int)report["sampledFrameCapMismatchCount"], Is.Zero);
            Assert.That((string)report["requestedDisplayMode"], Is.EqualTo("Windowed"));
            Assert.That((string)report["requestedDisplayRoute"], Is.EqualTo(EpisodeDirector.DisplayRoute),
                "The profile's window is set through the settings' own call (A12).");
            Assert.That((string)report["sampledDisplayMode"], Is.EqualTo("Windowed"));
            Assert.That((int)report["sampledDisplayModeMismatchCount"], Is.Zero);
            Assert.That((bool)report["uncapped"], Is.True);
            Assert.That((double)report["frameMedianMs"], Is.EqualTo(100), "Deliberately slow synthetic evidence is not a 60 FPS result.");
            Assert.That((string)report["performanceAcceptance"], Does.StartWith("Not assessed: ").And.Contain("ran 11.0 s of the 300 s required"),
                "An eleven-second sample is no verdict either way, however slow.");
            Assert.That((string)report["workload"], Does.Contain("not the 60 FPS performance target"));
        }

        // ------------------------------------------------------------------ the performance verdict and its evidence

        /// <summary>A five-minute 1920x1080 windowed uncapped run, every frame <paramref name="frameMs"/>, with its six captures.</summary>
        private void InstallTargetRun(float frameMs, int frameCount = 400)
        {
            Field("seconds").SetValue(runner, 300d);
            var samples = (List<float>)Field("frames").GetValue(runner);
            for (int i = 0; i < frameCount; i++) { samples.Add(frameMs); RecordDisplay(1920, 1080, -1, 0); }
            var captures = (List<VerificationFrameEvidence>)Field("capturedFrames").GetValue(runner);
            for (int i = 0; i < 6; i++) captures.Add(new VerificationFrameEvidence { rendered = true });
        }

        [TestCase(10f, "Met")]
        [TestCase(16.7f, "Met")]
        [TestCase(20f, "Not met")]
        public void AFullTargetRunIsJudgedOnItsPercentilesAndStatusStaysItsOwn(float frameMs, string verdict)
        {
            InstallTargetRun(frameMs);
            var report = ProfileReport(measured: 300.5);
            Assert.That((string)report["performanceAcceptance"], Is.EqualTo(verdict));
            Assert.That((string)report["status"], Is.EqualTo("Passed"), "The execution checks pass either way: Passed is never the verdict.");
            Assert.That((string)report["performanceAcceptanceBasis"], Does.Contain("p95 ").And.Contain("p99 ").And.Contain("300.5 s"));
            Assert.That((double)report["performanceP95LimitMs"], Is.EqualTo(16.7).Within(1e-5));
            Assert.That((double)report["performanceP99LimitMs"], Is.EqualTo(33.3).Within(1e-5));
            Assert.That((double)report["performanceMinimumSeconds"], Is.EqualTo(300));
        }

        [TestCase("batch")]
        [TestCase("nographics")]
        [TestCase("short")]
        [TestCase("resolution")]
        [TestCase("window")]
        [TestCase("cap")]
        [TestCase("development")]
        public void AFastRunThatMissedTheTargetIsNotAssessed(string condition)
        {
            InstallTargetRun(5f);
            bool graphical = condition != "nographics", batch = condition == "batch", development = condition == "development";
            double measured = condition == "short" ? 299.0 : 300.5;
            if (condition == "resolution") ProfileFrame(1600, 900, -1, 0);
            if (condition == "window") ProfileFrame(1920, 1080, -1, 0, FullScreenMode.FullScreenWindow);
            if (condition == "cap") ProfileFrame(1920, 1080, 60, 1);
            var report = ProfileReport(graphical, batch, measured, development);
            Assert.That((string)report["performanceAcceptance"], Does.StartWith("Not assessed: "), condition);
            Assert.That((bool)report["developmentBuild"], Is.EqualTo(development), "The report names the build it judged.");
        }

        [Test]
        public void TheMeasuredSeasonIsNamedBySourceSeedAndSession()
        {
            InstallProfileEvidence();
            Field("profileSeason").SetValue(runner, VerificationProfileSeason.Director);
            Field("seasonSeed").SetValue(runner, 4000000000L);
            Field("seasonSessionId").SetValue(runner, "0123abcd");
            Field("seasonStartSeconds").SetValue(runner, 12.5);
            var report = ProfileReport();
            Assert.That((string)report["seasonSource"], Is.EqualTo("director"));
            Assert.That((long)report["seasonSeed"], Is.EqualTo(4000000000L), "A uint seed survives whole.");
            Assert.That((string)report["sessionId"], Is.EqualTo("0123abcd"));
            Assert.That((double)report["seasonStartedSeconds"], Is.EqualTo(12.5));
        }

        [Test]
        public void AMemoryCounterThatHasNotSampledReadsUnavailableNotZero()
        {
            // Read in the frame the counters start, as the steady sample once did: no frame has ended
            // under them, so a reading is either a real one or -1 - never the 0 a fresh recorder holds.
            typeof(PortVerification).GetMethod("StartMemoryRecorders", PrivateInstance).Invoke(runner, null);
            try
            {
                var sample = (VerificationMemorySample)typeof(PortVerification).GetMethod("SampleMemory", PrivateInstance).Invoke(runner, null);
                Assert.That(sample.systemUsedBytes, Is.EqualTo(-1L).Or.GreaterThan(0L));
                Assert.That(sample.gfxUsedBytes, Is.EqualTo(-1L).Or.GreaterThan(0L));
            }
            finally { typeof(PortVerification).GetMethod("DisposeMemoryRecorders", PrivateInstance).Invoke(runner, null); }
        }

        [Test]
        public void TheSteadySampleReportsItsDistributionSlowFramesAndWorstFrame()
        {
            InstallProfileEvidence(0);
            var samples = (List<float>)Field("frames").GetValue(runner);
            samples.Clear();
            for (int i = 0; i < 95; i++) samples.Add(10f);
            samples.AddRange(new[] { 20f, 20f, 20f, 40f, 60f, 10f });
            var report = ProfileReport();
            Assert.That((int)report["frameCount"], Is.EqualTo(101));
            Assert.That((int)report["slowFramesOver16_7Ms"], Is.EqualTo(5));
            Assert.That((int)report["slowFramesOver33_3Ms"], Is.EqualTo(2));
            Assert.That((int)report["slowFramesOver50Ms"], Is.EqualTo(1));
            Assert.That((double)report["frameMaxMs"], Is.EqualTo(60));
            Assert.That((double)report["frameMinMs"], Is.EqualTo(10));
            Assert.That((double)report["frameMeanMs"], Is.EqualTo((96 * 10 + 3 * 20 + 40 + 60) / 101d).Within(1e-6));
            Assert.That(report["frameHistogramCounts"].Values<int>().Sum(), Is.EqualTo(101), "The histogram holds every steady frame.");
            Assert.That(report["frameHistogramUpperMs"].Values<float>().Count(), Is.EqualTo(report["frameHistogramCounts"].Values<int>().Count() - 1));
            Assert.That((int)report["steady"]["frames"], Is.EqualTo(101));
            Assert.That((double)report["steady"]["p99Ms"], Is.EqualTo((double)report["frameP99Ms"]));
        }

        [Test]
        public void StartupFramesAreReportedStageByStageApartFromTheSteadySample()
        {
            InstallProfileEvidence();
            var begin = typeof(PortVerification).GetMethod("BeginStartupStage", PrivateInstance);
            var stages = (List<List<float>>)Field("startupStageFrames").GetValue(runner);
            begin.Invoke(runner, new object[] { "bootstrap" });
            stages[0].AddRange(new[] { 400f, 30f });
            begin.Invoke(runner, new object[] { "bodies" });
            stages[1].AddRange(new[] { 2500f, 12f, 12f });
            var report = ProfileReport();
            var startup = (JArray)report["startupStages"];
            Assert.That(startup.Select(stage => (string)stage["name"]), Is.EqualTo(new[] { "bootstrap", "bodies" }));
            Assert.That((double)startup[1]["maxMs"], Is.EqualTo(2500), "The body-construction hitch is in its own stage.");
            Assert.That((int)report["startup"]["frames"], Is.EqualTo(5));
            Assert.That((double)report["startup"]["maxMs"], Is.EqualTo(2500));
            Assert.That((int)report["startup"]["framesOver50Ms"], Is.EqualTo(2));
            Assert.That((double)report["frameMaxMs"], Is.EqualTo(100), "...and never in the steady sample.");
            Assert.That((int)report["frameCount"], Is.EqualTo(101));
        }

        [Test]
        public void MemoryIsReportedAtTheSampleStartEndAndPeak()
        {
            InstallProfileEvidence();
            var start = new VerificationMemorySample { atSeconds = 30, totalAllocatedBytes = 100, totalReservedBytes = 200, monoUsedBytes = 10, monoHeapBytes = 20, gfxDriverBytes = 0, systemUsedBytes = 1000, gfxUsedBytes = 50 };
            var middle = new VerificationMemorySample { atSeconds = 31, totalAllocatedBytes = 180, totalReservedBytes = 260, monoUsedBytes = 15, monoHeapBytes = 20, gfxDriverBytes = 0, systemUsedBytes = 1400, gfxUsedBytes = 55 };
            var end = new VerificationMemorySample { atSeconds = 330, totalAllocatedBytes = 150, totalReservedBytes = 260, monoUsedBytes = 12, monoHeapBytes = 24, gfxDriverBytes = 0, systemUsedBytes = 1200, gfxUsedBytes = 52 };
            ((List<VerificationMemorySample>)Field("memorySamples").GetValue(runner)).AddRange(new[] { start, middle, end });
            Field("memoryAtSampleStart").SetValue(runner, start);
            Field("memoryAtSampleEnd").SetValue(runner, end);
            var report = ProfileReport();
            Assert.That((long)report["memoryAtSampleStart"]["totalAllocatedBytes"], Is.EqualTo(100));
            Assert.That((long)report["memoryAtSampleEnd"]["monoHeapBytes"], Is.EqualTo(24));
            Assert.That((long)report["memoryPeak"]["totalAllocatedBytes"], Is.EqualTo(180));
            Assert.That((long)report["memoryPeak"]["systemUsedBytes"], Is.EqualTo(1400));
            Assert.That((long)report["memoryPeak"]["monoHeapBytes"], Is.EqualTo(24));
            Assert.That((int)report["memorySamples"], Is.EqualTo(3));
            Assert.That((bool)report["gfxDriverMemoryAvailable"], Is.False, "A release player reports no driver estimate, and the report says so.");
        }

        [Test]
        public void TheBuildItsQualityAndItsRenderScaleAreReported()
        {
            InstallProfileEvidence();
            var report = ProfileReport();
            Assert.That((string)report["buildVersion"], Is.EqualTo(Application.version));
            Assert.That((string)report["buildGuid"] ?? "", Is.EqualTo(Application.buildGUID ?? ""));
            Assert.That((string)report["unityVersion"], Is.EqualTo(Application.unityVersion));
            Assert.That((int)report["qualityLevel"], Is.EqualTo(QualitySettings.GetQualityLevel()));
            Assert.That((string)report["qualityLevelName"], Is.EqualTo(QualitySettings.names[QualitySettings.GetQualityLevel()]));
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            var scale = pipeline == null ? null : pipeline.GetType().GetProperty("renderScale");
            Assert.That((float)report["renderScale"], Is.EqualTo(scale != null ? (float)scale.GetValue(pipeline) : -1f).Within(1e-5f));
            Assert.That((string)report["renderPipeline"], Is.EqualTo(pipeline != null ? pipeline.name : "Built-in"));
            Assert.That((string)report["graphicsDeviceVersion"], Is.EqualTo(SystemInfo.graphicsDeviceVersion));
            Assert.That((string)report["displayResolution"], Does.Match(@"^\d+x\d+$"));
        }

        [Test]
        public void TheRawFramesAreWrittenInTheOrderMeasuredAndNamedInTheReport()
        {
            InstallProfileEvidence();
            Field("outputDirectory").SetValue(runner, temporary);
            var samples = (List<float>)Field("frames").GetValue(runner);
            samples[0] = 30f; samples[1] = 10f; samples[2] = 20f;
            typeof(PortVerification).GetMethod("BeginStartupStage", PrivateInstance).Invoke(runner, new object[] { "bootstrap" });
            ((List<List<float>>)Field("startupStageFrames").GetValue(runner))[0].Add(400f);
            typeof(PortVerification).GetMethod("WriteRawFrameTimes", PrivateInstance).Invoke(runner, null);
            var report = ProfileReport();
            string steady = Path.Combine(temporary, PortVerification.RawFrameTimesName);
            Assert.That((string)report["rawFrameTimesFile"], Is.EqualTo(steady));
            var lines = File.ReadAllLines(steady);
            Assert.That(lines.Take(4), Is.EqualTo(new[] { "ms", "30", "10", "20" }));
            Assert.That(lines.Length, Is.EqualTo(102));
            Assert.That(samples.Take(3), Is.EqualTo(new[] { 30f, 10f, 20f }), "The report sorts a copy; the measured order survives it.");
            string startup = Path.Combine(temporary, PortVerification.RawStartupFrameTimesName);
            Assert.That((string)report["rawStartupFrameTimesFile"], Is.EqualTo(startup));
            Assert.That(File.ReadAllLines(startup), Is.EqualTo(new[] { "stage,ms", "bootstrap,400" }));
        }

        [Test]
        public void AStressRunSaysSo()
        {
            InstallProfileEvidence();
            Field("stressRoster").SetValue(runner, true);
            Field("houseSize").SetValue(runner, 16);
            Field("measuredHouseSize").SetValue(runner, 16);
            var report = ProfileReport();
            Assert.That((bool)report["stressRoster"], Is.True);
            Assert.That((int)report["houseSizeRequested"], Is.EqualTo(16));
            Assert.That((int)report["houseSize"], Is.EqualTo(16));
        }

        [TestCase(1600, 900)]
        [TestCase(1280, 720)]
        [TestCase(1280, 800)]
        [TestCase(1919, 1080)]
        [TestCase(1920, 1079)]
        public void OneWrongResolutionFrameCannotBeHiddenByRestoringTheTarget(int width, int height)
        {
            InstallProfileEvidence();
            ProfileFrame(width, height, -1, 0);
            ProfileFrame(1920, 1080, -1, 0);
            var report = ProfileReport();
            Assert.That((string)report["status"], Is.EqualTo("Failed"));
            Assert.That((string)report["overallStatus"], Is.EqualTo("Failed"));
            Assert.That((string)report["sampledResolution"], Is.EqualTo("1920x1080"), "First sample alone does not establish all-frame resolution.");
            Assert.That((int)report["sampledDisplayFrames"], Is.EqualTo(103));
            Assert.That((int)report["sampledResolutionMismatchCount"], Is.EqualTo(1));
            Assert.That((int)report["sampledFrameCapMismatchCount"], Is.Zero);
            Assert.That(report["errors"].Values<string>(), Has.Some.Contains("1920x1080"));
        }

        [TestCase(-1, 1)]
        [TestCase(60, 0)]
        [TestCase(0, 0)]
        public void OneCappedFrameCannotBeHiddenByRestoringUncappedSettings(int cap, int vSync)
        {
            InstallProfileEvidence();
            ProfileFrame(1920, 1080, cap, vSync);
            ProfileFrame(1920, 1080, -1, 0);
            var report = ProfileReport();
            Assert.That((string)report["status"], Is.EqualTo("Failed"));
            Assert.That((int)report["sampledFrameCap"], Is.EqualTo(-1));
            Assert.That((int)report["sampledVSyncCount"], Is.Zero);
            Assert.That((int)report["sampledFrameCapMismatchCount"], Is.EqualTo(1));
            Assert.That((bool)report["uncapped"], Is.False);
            Assert.That(report["errors"].Values<string>(), Has.Some.Contains("VSync"));
        }

        [TestCase(FullScreenMode.ExclusiveFullScreen)]
        [TestCase(FullScreenMode.FullScreenWindow)]
        [TestCase(FullScreenMode.MaximizedWindow)]
        public void Fullscreen1080pCannotMasqueradeAsWindowedEvidence(FullScreenMode mode)
        {
            InstallProfileEvidence();
            ProfileFrame(1920, 1080, -1, 0, mode);
            ProfileFrame(1920, 1080, -1, 0);
            var report = ProfileReport();
            Assert.That((string)report["status"], Is.EqualTo("Failed"));
            Assert.That((string)report["sampledDisplayMode"], Is.EqualTo("Windowed"));
            Assert.That((int)report["sampledDisplayModeMismatchCount"], Is.EqualTo(1));
            Assert.That((int)report["sampledResolutionMismatchCount"], Is.Zero);
            Assert.That((int)report["sampledFrameCapMismatchCount"], Is.Zero);
            Assert.That(report["errors"].Values<string>(), Has.Some.Contains("Windowed"));
        }

        [TestCase(0)]
        [TestCase(100)]
        public void TimingFramesWithoutMatchingDisplayEvidenceCannotPass(int displaySamples)
        {
            InstallProfileEvidence(displaySamples);
            var report = ProfileReport();
            Assert.That((int)report["frameCount"], Is.EqualTo(101));
            Assert.That((int)report["sampledDisplayFrames"], Is.EqualTo(displaySamples));
            Assert.That((string)report["status"], Is.EqualTo("Failed"));
            Assert.That(report["errors"].Values<string>(), Has.Some.Contains("Every measured frame"));
            if (displaySamples == 0) Assert.That((bool)report["uncapped"], Is.False);
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void CorrectResolutionDoesNotMakeBatchOrNongraphicalEvidencePass(bool graphical, bool batchMode)
        {
            InstallProfileEvidence();
            var report = ProfileReport(graphical, batchMode);
            Assert.That((string)report["status"], Is.EqualTo("Failed"));
            Assert.That((bool)report["graphical"], Is.EqualTo(graphical));
            Assert.That((bool)report["batchMode"], Is.EqualTo(batchMode));
        }

        [Test]
        public void CorrectResolutionDoesNotDiscardSharedRuntimeErrors()
        {
            InstallProfileEvidence();
            Collect(LogType.Exception);
            var report = ProfileReport();
            Assert.That((string)report["status"], Is.EqualTo("Failed"));
            Assert.That(report["errors"].Values<string>(), Has.Some.Contains("shared runtime failure"));
        }

        [TestCase(LogType.Log, false)]
        [TestCase(LogType.Warning, false)]
        [TestCase(LogType.Error, true)]
        [TestCase(LogType.Exception, true)]
        [TestCase(LogType.Assert, true)]
        public void LookSheetCompletionIncludesSharedRuntimeErrors(LogType type, bool isFailure)
        {
            var report = InstallCompleteRouteReport();
            Assert.That(Complete(), Is.EqualTo(12));
            Assert.That(Status(report), Is.EqualTo("Passed"), "The route-only fixture is complete before logging.");
            Collect(type);
            Assert.That(Complete(), Is.EqualTo(12));
            Assert.That(Status(report), Is.EqualTo(isFailure ? "Failed" : "Passed"));
            var failures = (IList)report.GetType().GetField("errors").GetValue(report);
            Assert.That(failures.Count, Is.EqualTo(isFailure ? 1 : 0));
            if (isFailure) Assert.That((string)failures[0], Does.Contain("shared runtime failure").And.Contain("retained stack"));
        }

        [Test]
        public void LookSheetRetainsErrorsLoggedBeforeItsReportExists()
        {
            Collect(LogType.Exception);
            var report = InstallCompleteRouteReport();
            Assert.That(Complete(), Is.EqualTo(12));
            Assert.That(Status(report), Is.EqualTo("Failed"));
            Assert.That(((IList)report.GetType().GetField("errors").GetValue(report)).Count, Is.EqualTo(1));
        }

        private static FieldInfo Field(string name)
        {
            var field = typeof(PortVerification).GetField(name, PrivateInstance);
            Assert.That(field, Is.Not.Null, name);
            return field;
        }

        private void InstallProfileEvidence(int displaySamples = 101)
        {
            Field("seconds").SetValue(runner, 10d);
            // Synthetic contract data only: no native frame, window, capture or performance claim.
            var samples = (List<float>)Field("frames").GetValue(runner);
            for (int i = 0; i < 101; i++) samples.Add(100);
            for (int i = 0; i < displaySamples; i++) RecordDisplay(1920, 1080, -1, 0);
            var captures = (List<VerificationFrameEvidence>)Field("capturedFrames").GetValue(runner);
            for (int i = 0; i < 6; i++) captures.Add(new VerificationFrameEvidence { rendered = true });
        }

        private void ProfileFrame(int width, int height, int frameCap, int vSync, FullScreenMode mode = FullScreenMode.Windowed)
        {
            ((List<float>)Field("frames").GetValue(runner)).Add(100);
            RecordDisplay(width, height, frameCap, vSync, mode);
        }

        private void RecordDisplay(int width, int height, int frameCap, int vSync, FullScreenMode mode = FullScreenMode.Windowed)
            => typeof(PortVerification).GetMethod("RecordProfileDisplaySample", PrivateInstance)
                .Invoke(runner, new object[] { width, height, frameCap, vSync, mode });

        private JObject ProfileReport(bool graphical = true, bool batchMode = false, double measured = 11d, bool developmentBuild = false)
            => JObject.Parse(JsonUtility.ToJson(typeof(PortVerification).GetMethod("CompleteProfileReport", PrivateInstance)
                .Invoke(runner, new object[] { measured, graphical, batchMode, developmentBuild })));

        private object InstallCompleteRouteReport()
        {
            var reportType = typeof(PortVerification).GetNestedType("LookSheetReport", BindingFlags.NonPublic);
            var report = Activator.CreateInstance(reportType, true);
            var shotsField = reportType.GetField("shots");
            var shots = (IList)shotsField.GetValue(report);
            for (int i = 0; i < 12; i++)
            {
                var shot = Activator.CreateInstance(shotsField.FieldType.GetGenericArguments()[0], true);
                string path = Path.Combine(temporary, "route-" + i + ".png");
                // Only the completion/error contract is under test. These nonempty sentinels
                // are not captures or visual acceptance; image validation has its own tests.
                File.WriteAllBytes(path, new byte[] { 1 });
                shot.GetType().GetField("path").SetValue(shot, path);
                shot.GetType().GetField("reached").SetValue(shot, true);
                shots.Add(shot);
            }
            var layoutsField = reportType.GetField("layouts");
            var layouts = (IList)layoutsField.GetValue(report);
            for (int i = 0; i < 31; i++)
                layouts.Add(Activator.CreateInstance(layoutsField.FieldType.GetGenericArguments()[0], true));
            Field("lookReport").SetValue(runner, report);
            return report;
        }

        private void Collect(LogType type)
            => typeof(PortVerification).GetMethod("CollectError", PrivateInstance)
                .Invoke(runner, new object[] { "shared runtime failure", "retained stack", type });

        private int Complete()
            => (int)typeof(PortVerification).GetMethod("CompleteLookSheetReport", PrivateInstance).Invoke(runner, null);

        private static string Status(object report) => (string)report.GetType().GetField("status").GetValue(report);
    }
}
