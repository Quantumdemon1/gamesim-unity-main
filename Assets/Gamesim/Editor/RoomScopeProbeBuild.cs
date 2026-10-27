using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.Editor
{
    /// <summary>
    /// The room scope probe's development player (ROOM-SCOPE-PLAN §5.1 and §5.3, RS0): the shipping
    /// scenes built with <see cref="BuildOptions.Development"/>, so the probe can read the GPU's time
    /// (FrameTimingManager is on in a development player), the drawer's own counters (they exist only
    /// with the profiler compiled in) and, if URP's player compile defines it, URP's atlas line under
    /// UNITY_ENABLE_CHECKS. The release player is built the usual way (Tools/build-review-candidate.ps1).
    ///
    /// <para>It never touches ProjectSettings: no player setting is written, nothing is saved, and the
    /// build fails if any file under ProjectSettings changed while it ran. On every build it reads the
    /// player compile's response file for URP's runtime and says whether UNITY_ENABLE_CHECKS was
    /// defined, in <see cref="ReportName"/> beside the player: that answer says whether the probe's P7
    /// can be taken in the development player or needs an editor diagnostic run.</para>
    ///
    /// <para>Run on the frozen probe copy, never a lane copy and never the owner's play copy:
    /// <c>Unity.exe -batchmode -quit -projectPath &lt;probe copy&gt; -executeMethod
    /// Gamesim.Editor.RoomScopeProbeBuild.BuildDevelopment -gamesimProbeOutput &lt;folder&gt; -logFile &lt;log&gt;</c>.</para>
    /// </summary>
    public static class RoomScopeProbeBuild
    {
        public const string OutputArgument = "-gamesimProbeOutput";
        public const string DefaultOutput = "Builds/RoomScopeProbe-Development/Gamesim.exe";
        public const string ReportName = "room-scope-probe-build.json";
        public const string UrpRuntimeResponseFile = "Unity.RenderPipelines.Universal.Runtime.rsp";
        public const string ChecksDefine = "-define:UNITY_ENABLE_CHECKS", DevelopmentDefine = "-define:DEVELOPMENT_BUILD";
        public const string Defined = "defined", NotDefined = "not defined", Unknown = "unknown";

        [MenuItem("Gamesim/Room scope/Build the probe's development player")]
        public static void BuildDevelopment()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new BuildPlayerWindow.BuildMethodException("Wait for an idle Editor before building the probe.");
            if (!EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == EpisodeProjectSetup.ScenePath))
                throw new BuildPlayerWindow.BuildMethodException("The episode scene must be enabled before building the probe.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new BuildPlayerWindow.BuildMethodException("Save modified scenes before building the probe.");
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            foreach (string scene in scenes)
                if (!File.Exists(scene)) throw new BuildPlayerWindow.BuildMethodException("Enabled scene is missing: " + scene);

            string output = OutputPath(Environment.GetCommandLineArgs());
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            string settingsBefore = SettingsFingerprint();
            var started = DateTime.UtcNow;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                // The release build's options (U01ProjectSetup.BuildDesktop) and Development: the two
                // players differ in that alone.
                options = BuildOptions.Development | BuildOptions.CleanBuildCache | BuildOptions.StrictMode,
            });
            string settingsAfter = SettingsFingerprint();
            string checks = UrpChecks(started, out string responseFile, out bool developmentCompile);
            var summary = report.summary;
            var record = new ProbeBuildRecord
            {
                result = summary.result.ToString(), outputPath = summary.outputPath, developmentBuild = true,
                totalBytes = summary.totalSize, errors = summary.totalErrors, warnings = summary.totalWarnings,
                durationSeconds = summary.totalTime.TotalSeconds, scenes = scenes, startedUtc = started.ToString("O"),
                urpEnableChecks = checks, urpResponseFile = responseFile, urpResponseFileIsDevelopment = developmentCompile,
                projectSettingsUnchanged = settingsBefore == settingsAfter,
                inputActionsPreloaded = PlayerSettings.GetPreloadedAssets().Any(asset => asset != null
                    && AssetDatabase.GetAssetPath(asset) == "Assets/InputSystem_Actions.inputactions"),
                messages = report.steps.SelectMany(step => step.messages)
                    .Where(message => message.type == LogType.Error || message.type == LogType.Exception)
                    .Select(message => message.content).Distinct().ToArray(),
            };
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), ReportName), JsonUtility.ToJson(record, true));
            if (!record.projectSettingsUnchanged)
                throw new BuildPlayerWindow.BuildMethodException("ProjectSettings changed during the probe's build; restore them before anything is committed.");
            if (summary.result != BuildResult.Succeeded)
                throw new BuildPlayerWindow.BuildMethodException("The probe's development build failed: " + summary.result + " (" + summary.totalErrors + " errors).");
            Debug.Log("Room-scope probe development build succeeded: " + output + "; URP's player compile: UNITY_ENABLE_CHECKS " + checks + ".");
        }

        /// <summary>Where the player goes: <c>-gamesimProbeOutput</c>, a folder or an .exe, else <see cref="DefaultOutput"/>.</summary>
        public static string OutputPath(string[] args)
        {
            int at = args != null ? Array.IndexOf(args, OutputArgument) : -1;
            if (at < 0 || at + 1 >= args.Length || string.IsNullOrWhiteSpace(args[at + 1])) return DefaultOutput;
            string asked = args[at + 1];
            return asked.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? asked : Path.Combine(asked, "Gamesim.exe");
        }

        /// <summary>Whether a response file defines UNITY_ENABLE_CHECKS: <see cref="Defined"/>, <see cref="NotDefined"/>, or <see cref="Unknown"/> for none.</summary>
        public static string ChecksIn(string responseFile)
        {
            if (responseFile == null) return Unknown;
            return responseFile.Split('\n').Any(line => line.Trim() == ChecksDefine) ? Defined : NotDefined;
        }

        /// <summary>
        /// URP's runtime as the player compile just built it: the newest player graph's response file
        /// (Library/Bee/artifacts/*P.dag) written since <paramref name="since"/>, and whether it defines
        /// UNITY_ENABLE_CHECKS. Unknown when no fresh one is there.
        /// </summary>
        private static string UrpChecks(DateTime since, out string path, out bool development)
        {
            path = null;
            development = false;
            var artifacts = new DirectoryInfo(Path.Combine("Library", "Bee", "artifacts"));
            if (!artifacts.Exists) return Unknown;
            var fresh = artifacts.GetDirectories("*P.dag")
                .Select(dag => new FileInfo(Path.Combine(dag.FullName, UrpRuntimeResponseFile)))
                .Where(file => file.Exists && file.LastWriteTimeUtc >= since.AddMinutes(-1))
                .OrderByDescending(file => file.LastWriteTimeUtc).FirstOrDefault();
            if (fresh == null) return Unknown;
            path = fresh.FullName;
            string text = File.ReadAllText(fresh.FullName);
            development = text.Split('\n').Any(line => line.Trim() == DevelopmentDefine);
            return ChecksIn(text);
        }

        /// <summary>Every file under ProjectSettings, by name and content: the build must leave it as it found it.</summary>
        private static string SettingsFingerprint()
        {
            using (var sha = SHA256.Create())
            {
                var text = new StringBuilder();
                foreach (string file in Directory.GetFiles("ProjectSettings", "*", SearchOption.AllDirectories).OrderBy(name => name, StringComparer.Ordinal))
                    text.Append(file.Replace('\\', '/')).Append(':')
                        .Append(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file)))).Append('\n');
                return text.ToString();
            }
        }

        [Serializable]
        private sealed class ProbeBuildRecord
        {
            public string result, outputPath, startedUtc, urpEnableChecks, urpResponseFile;
            public bool developmentBuild, urpResponseFileIsDevelopment, projectSettingsUnchanged, inputActionsPreloaded;
            public ulong totalBytes;
            public int errors, warnings;
            public double durationSeconds;
            public string[] scenes, messages;
        }
    }
}
