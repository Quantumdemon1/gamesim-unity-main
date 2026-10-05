using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
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
