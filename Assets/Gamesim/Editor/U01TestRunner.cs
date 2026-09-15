using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.Editor
{
    [InitializeOnLoad]
    public static class U01TestRunner
    {
        public const string ResultsPath = "Logs/U01-editmode-results.txt";
        public const string PlayModeResultsPath = "Logs/U02-playmode-results.txt";

        private const string ActiveRunKey = "Gamesim.TestRunner.ActiveRun";
        private const string LatestRunKey = "Gamesim.TestRunner.LatestRun";

        private static ResultWriter callback;

        static U01TestRunner()
        {
            // The callback registry is not serialized by the Test Framework. Register
            // once in every domain; SessionState reconnects it to a Play Mode run.
            EnsureCallbackRegistered();
        }

        [MenuItem("Gamesim/U01/Run Edit Mode Tests")]
        public static void RunEditModeTests()
        {
            StartRun(TestMode.EditMode, "Gamesim.EditModeTests", ResultsPath);
        }

        [MenuItem("Gamesim/U02/Run Play Mode Tests")]
        public static void RunPlayModeTests()
        {
            StartRun(TestMode.PlayMode, "Gamesim.PlayModeTests", PlayModeResultsPath);
        }

        /// <summary>
        /// Pollable after an asynchronous MCP invocation, including across domain reloads.
        /// A completed test report does not imply that the editor has finished restoring scenes.
        /// </summary>
        public static string GetStatus()
        {
            var state = ReadState(ActiveRunKey) ?? ReadState(LatestRunKey);
            return state == null ? "Status=Idle\n" : FormatSummary(state);
        }

        private static void EnsureCallbackRegistered()
        {
            if (callback != null)
            {
                return;
            }

            callback = new ResultWriter();
            TestRunnerApi.RegisterTestCallback(callback);
        }

        private static void StartRun(TestMode mode, string assemblyName, string resultsPath)
        {
            if (ReadState(ActiveRunKey) != null)
            {
                throw new InvalidOperationException("A Gamesim test run is already active. Poll GetStatus() before starting another run.");
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Wait until the editor is idle and out of Play Mode before starting Gamesim tests.");
            }

            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty)
                {
                    throw new InvalidOperationException($"Save the modified scene '{scene.name}' before running Gamesim tests.");
                }
            }

            EnsureCallbackRegistered();
            Directory.CreateDirectory("Logs");

            var state = new RunState
            {
                runId = Guid.NewGuid().ToString("N"),
                mode = mode.ToString(),
                assemblyName = assemblyName,
                resultsPath = resultsPath,
                xmlPath = Path.ChangeExtension(resultsPath, ".xml"),
                status = "Running",
                startedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };

            SaveActiveState(state);
            try
            {
                // Replace any previous completion marker before Execute can schedule work.
                // Only a completed marker with XmlWritten=True belongs to the current run.
                File.WriteAllText(state.resultsPath, FormatSummary(state));
                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                try
                {
                    var jobId = api.Execute(new ExecutionSettings(new Filter
                    {
                        testMode = mode,
                        assemblyNames = new[] { assemblyName }
                    }));

                    // Execute may invoke callbacks before returning. Do not replace a
                    // completed report or a newer callback state with this initial state.
                    var active = ReadState(ActiveRunKey);
                    if (active != null && active.runId == state.runId)
                    {
                        active.jobId = jobId;
                        SaveActiveState(active);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(api);
                }
            }
            catch (Exception exception)
            {
                CompleteWithError(state, exception.ToString());
                throw;
            }
        }

        private static RunState ReadState(string key)
        {
            var json = SessionState.GetString(key, string.Empty);
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<RunState>(json);
        }

        private static void SaveActiveState(RunState state)
        {
            SessionState.SetString(ActiveRunKey, JsonUtility.ToJson(state));
        }

        private static void Complete(RunState state)
        {
            state.finishedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            SessionState.SetString(LatestRunKey, JsonUtility.ToJson(state));
            SessionState.EraseString(ActiveRunKey);
            File.WriteAllText(state.resultsPath, FormatSummary(state));
        }

        private static void CompleteWithError(RunState state, string message)
        {
            state.status = "Failed";
            state.details = message;
            Complete(state);
        }

        private static string FormatSummary(RunState state)
        {
            var output = new StringBuilder();
            output.AppendLine($"Status={state.status}");
            output.AppendLine($"RunId={state.runId}");
            output.AppendLine($"JobId={state.jobId}");
            output.AppendLine($"Mode={state.mode}");
            output.AppendLine($"Assembly={state.assemblyName}");
            output.AppendLine($"StartedUtc={state.startedUtc}");
            output.AppendLine($"FinishedUtc={state.finishedUtc}");
            output.AppendLine($"PassCount={state.passCount}");
            output.AppendLine($"FailCount={state.failCount}");
            output.AppendLine($"SkipCount={state.skipCount}");
            output.AppendLine($"InconclusiveCount={state.inconclusiveCount}");
            output.AppendLine($"DurationSeconds={state.duration.ToString("F3", CultureInfo.InvariantCulture)}");
            output.AppendLine($"ResultState={state.resultState}");
            output.AppendLine($"XmlPath={state.xmlPath}");
            output.AppendLine($"XmlWritten={state.xmlWritten}");
            output.AppendLine($"LastTest={state.lastTest}");
            if (!string.IsNullOrEmpty(state.details))
            {
                output.AppendLine("Details:");
                output.AppendLine(state.details);
            }

            return output.ToString();
        }

        [Serializable]
        private sealed class RunState
        {
            public string runId;
            public string jobId;
            public string mode;
            public string assemblyName;
            public string resultsPath;
            public string xmlPath;
            public string status;
            public string startedUtc;
            public string finishedUtc;
            public int passCount;
            public int failCount;
            public int skipCount;
            public int inconclusiveCount;
            public double duration;
            public string resultState;
            public bool xmlWritten;
            public string lastTest;
            public string details;
        }

        private sealed class ResultWriter : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var state = ReadState(ActiveRunKey);
                if (state == null)
                {
                    return;
                }

                state.passCount = result.PassCount;
                state.failCount = result.FailCount;
                state.skipCount = result.SkipCount;
                state.inconclusiveCount = result.InconclusiveCount;
                state.duration = result.Duration;
                state.resultState = result.ResultState;
                state.details = result.Message;
                state.status = result.PassCount > 0 && result.FailCount == 0 &&
                    result.SkipCount == 0 && result.InconclusiveCount == 0 &&
                    string.Equals(result.ResultState, "Passed", StringComparison.Ordinal)
                    ? "Passed"
                    : "Failed";

                try
                {
                    TestRunnerApi.SaveResultToFile(result, state.xmlPath);
                    state.xmlWritten = true;
                    Complete(state);
                }
                catch (Exception exception)
                {
                    CompleteWithError(state, "Unable to write the complete test report.\n" + exception);
                }
            }

            public void TestStarted(ITestAdaptor test)
            {
                var state = ReadState(ActiveRunKey);
                if (state != null)
                {
                    state.lastTest = test.FullName;
                    SaveActiveState(state);
                }
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }

            public void OnError(string message)
            {
                var state = ReadState(ActiveRunKey);
                if (state != null)
                {
                    CompleteWithError(state, message);
                }
            }
        }
    }
}
