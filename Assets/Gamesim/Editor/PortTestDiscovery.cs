using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.Editor
{
    /// <summary>Opt-in diagnostics for a zero-test result; discovery never certifies execution.</summary>
    [InitializeOnLoad]
    public static class PortTestDiscovery
    {
        private const string ActiveTestRunKey = "Gamesim.TestDiscovery.AnyTestRunActive";
        private const double DiscoveryTimeoutSeconds = 60;
        private static readonly RunTracker runTracker;
        private static DiscoveryOperation pending;
        private static bool reloadRequested;
        private static bool discoveryRequiresReload;

        static PortTestDiscovery()
        {
            // The public callback registry is not serialized. Register in every new
            // domain, retaining the observed active-run flag in Editor SessionState.
            runTracker = new RunTracker();
            TestRunnerApi.RegisterTestCallback(runTracker);
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
        }

        [MenuItem("Gamesim/U01/Inspect Play Mode Test Discovery")]
        public static void InspectPlayModeTests()
        {
            RequireIdle();
            var operation = new DiscoveryOperation
            {
                deadline = EditorApplication.timeSinceStartup + DiscoveryTimeoutSeconds,
                report = new DiscoveryReport
                {
                    startedUtc = DateTime.UtcNow.ToString("O"), status = "Discovering"
                }
            };
            pending = operation;
            try
            {
                operation.report.loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetName().Name)
                    .Where(name => name != null && name.StartsWith("Gamesim", StringComparison.Ordinal))
                    .OrderBy(name => name, StringComparer.Ordinal).ToArray();
                operation.report.compiledAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                    .Where(a => a.name.StartsWith("Gamesim", StringComparison.Ordinal))
                    .Select(a => a.name + " | " + a.flags).ToArray();
                Write(operation.report);
                operation.api = ScriptableObject.CreateInstance<TestRunnerApi>();
                EditorApplication.update += WatchDiscovery;
                // Public API available in the installed Test Framework 1.8.0.
                operation.api.RetrieveTestList(TestMode.PlayMode, root => DiscoveryFinished(operation, root));
            }
            catch (Exception exception)
            {
                FailWithoutThrowing(operation, "Unable to schedule discovery: " + exception);
                ReleaseWithoutThrowing(operation);
                throw;
            }
        }

        [MenuItem("Gamesim/U01/Refresh Test Discovery")]
        public static void RefreshDiscovery()
        {
            RequireIdle(true);
            reloadRequested = true;
            try
            {
                // TestListCache clears its cache on [DidReloadScripts]. No scene,
                // gameplay state, package or generated Library file is overwritten.
                EditorUtility.RequestScriptReload();
            }
            catch
            {
                reloadRequested = false;
                throw;
            }
        }

        private static void RequireIdle(bool forRefresh = false)
        {
            if (pending != null || reloadRequested || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode
                || SessionState.GetBool(ActiveTestRunKey, false)
                || U01TestRunner.GetStatus().StartsWith("Status=Running", StringComparison.Ordinal))
                throw new InvalidOperationException("Wait until Unity and test execution are idle before inspecting or refreshing discovery.");
            if (discoveryRequiresReload && !forRefresh)
                throw new InvalidOperationException("Discovery timed out. Use Refresh Test Discovery while Unity and all tests are idle before retrying.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Save modified scenes before inspecting or refreshing test discovery.");
        }

        private static void DiscoveryFinished(DiscoveryOperation operation, ITestAdaptor root)
        {
            try
            {
                // Public discovery has no cancellation API. A callback arriving after
                // timeout or reload must not overwrite a newer report or clear its guard.
                if (operation.completed || !ReferenceEquals(pending, operation)) return;
                if (root == null) throw new InvalidOperationException("Test discovery returned no root node.");
                var children = (root.Children ?? Enumerable.Empty<ITestAdaptor>()).ToArray();
                var target = children.FirstOrDefault(child => child.Name == "Gamesim.PlayModeTests"
                    || child.Name == "Gamesim.PlayModeTests.dll");
                operation.report.finishedUtc = DateTime.UtcNow.ToString("O");
                operation.report.rootTestCount = root.TestCaseCount;
                operation.report.gamesimTestCount = target?.TestCaseCount ?? 0;
                operation.report.discoveredAssemblies = children
                    .Select(child => child.Name + " | " + child.TestCaseCount).ToArray();
                operation.report.status = operation.report.gamesimTestCount > 0
                    ? "Discovered (not executed)" : "Missing Gamesim Play Mode tests";
                Write(operation.report);
            }
            catch (Exception exception)
            {
                // TestListJob removes its update delegate AFTER invoking this callback.
                // Never let an exception escape and prevent that framework cleanup.
                FailWithoutThrowing(operation, "Discovery callback failed: " + exception);
            }
            finally { ReleaseWithoutThrowing(operation); }
        }

        private static void WatchDiscovery()
        {
            var operation = pending;
            if (operation == null) return;
            try
            {
                if (SessionState.GetBool(ActiveTestRunKey, false)
                    || EditorApplication.isPlayingOrWillChangePlaymode)
                    AbortDiscovery("Test execution or Play Mode started during discovery.");
                else if (EditorApplication.timeSinceStartup >= operation.deadline)
                {
                    discoveryRequiresReload = true;
                    AbortDiscovery("Discovery timed out after 60 seconds. Its public API cannot cancel the framework job. Once Unity and tests are idle, use Refresh Test Discovery before retrying.");
                }
            }
            catch (Exception exception)
            {
                FailWithoutThrowing(operation, "Discovery watchdog failed: " + exception);
                ReleaseWithoutThrowing(operation);
            }
        }

        private static void BeforeAssemblyReload()
        {
            AbortDiscovery("Script-domain reload interrupted discovery; inspect again after Unity is idle.");
        }

        private static void AbortDiscovery(string details)
        {
            var operation = pending;
            if (operation == null) return;
            FailWithoutThrowing(operation, details);
            ReleaseWithoutThrowing(operation);
        }

        private static void FailWithoutThrowing(DiscoveryOperation operation, string details)
        {
            try
            {
                operation.report.status = "Failed";
                operation.report.finishedUtc = DateTime.UtcNow.ToString("O");
                operation.report.details = details;
                Write(operation.report);
            }
            catch (Exception writeException)
            {
                WarnOnceWithoutThrowing(operation, details + "\nUnable to write diagnostic report: " + writeException.Message);
            }
        }

        private static void ReleaseWithoutThrowing(DiscoveryOperation operation)
        {
            operation.completed = true;
            if (ReferenceEquals(pending, operation))
            {
                pending = null;
                try { EditorApplication.update -= WatchDiscovery; }
                catch (Exception exception) { WarnOnceWithoutThrowing(operation, "Could not remove discovery watchdog: " + exception.Message); }
            }
            var api = operation.api;
            operation.api = null;
            try { if (api != null) UnityEngine.Object.DestroyImmediate(api); }
            catch (Exception exception) { WarnOnceWithoutThrowing(operation, "Could not dispose discovery API: " + exception.Message); }
        }

        private static void WarnOnceWithoutThrowing(DiscoveryOperation operation, string message)
        {
            if (operation.warningLogged) return;
            operation.warningLogged = true;
            try { Debug.LogWarning("[Gamesim test discovery] " + message); }
            catch { /* Diagnostics must never retain a framework update callback. */ }
        }

        private static void Write(DiscoveryReport report)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/Port-test-discovery.json", JsonUtility.ToJson(report, true));
        }

        private sealed class RunTracker : IErrorCallbacks
        {
            // This observes runs from every TestRunnerApi caller, not just Gamesim.
            // In 1.8.0 RunStarted is AFTER prebuild and RunFinished/OnError are
            // BEFORE cleanup; these flags are not universal scheduled-job/idle proof.
            // Invoke the menus only at an externally verified idle boundary as well.
            public void RunStarted(ITestAdaptor testsToRun)
            {
                SessionState.SetBool(ActiveTestRunKey, true);
                AbortDiscovery("A Test Runner API run started during discovery.");
            }

            public void RunFinished(ITestResultAdaptor result) => SessionState.SetBool(ActiveTestRunKey, false);
            public void OnError(string message) => SessionState.SetBool(ActiveTestRunKey, false);
            public void TestStarted(ITestAdaptor test) => SessionState.SetBool(ActiveTestRunKey, true);
            public void TestFinished(ITestResultAdaptor result) { }
        }

        private sealed class DiscoveryOperation
        {
            public TestRunnerApi api;
            public DiscoveryReport report;
            public double deadline;
            public bool completed, warningLogged;
        }

        [Serializable] private sealed class DiscoveryReport
        {
            public string status, startedUtc, finishedUtc, details;
            public int rootTestCount, gamesimTestCount;
            public string[] loadedAssemblies, compiledAssemblies, discoveredAssemblies;
        }
    }
}
