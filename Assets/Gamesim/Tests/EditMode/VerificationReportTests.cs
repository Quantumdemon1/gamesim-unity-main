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
            Assert.That((string)report["sampledDisplayMode"], Is.EqualTo("Windowed"));
            Assert.That((int)report["sampledDisplayModeMismatchCount"], Is.Zero);
            Assert.That((bool)report["uncapped"], Is.True);
            Assert.That((double)report["frameMedianMs"], Is.EqualTo(100), "Deliberately slow synthetic evidence is not a 60 FPS result.");
            Assert.That((string)report["performanceAcceptance"], Is.EqualTo("Not assessed; uncapped diagnostic sample only."));
            Assert.That((string)report["workload"], Does.Contain("not the 60 FPS performance target"));
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

        private JObject ProfileReport(bool graphical = true, bool batchMode = false)
            => JObject.Parse(JsonUtility.ToJson(typeof(PortVerification).GetMethod("CompleteProfileReport", PrivateInstance)
                .Invoke(runner, new object[] { 11d, graphical, batchMode })));

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
