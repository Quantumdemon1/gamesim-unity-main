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
    /// build fails if any file under ProjectSettings changed while it ran. The release build's own
    /// writes - the window defaults and the input-action preload - must therefore already hold, as they
    /// do in the committed settings, and the copy must build with UMA (GAMESIM_UMA in its own,
    /// never-committed ProjectSettings, set before this build): it refuses to build otherwise. On every
    /// build it reads the player compile's response files and records, in <see cref="ReportName"/>
    /// beside the player, whether URP's runtime defined UNITY_ENABLE_CHECKS (whether the probe's P7 can
    /// be taken in the development player or needs an editor diagnostic run) and whether Gamesim's
    /// runtime defined GAMESIM_UMA; the build fails without the latter.</para>
    ///
    /// <para>Run on the frozen probe copy, alone (never beside the owner's editor), never a lane copy and
    /// never the owner's play copy, with UNITY_MCP_DISABLE_BATCH=1:
    /// <c>Unity.exe -batchmode -quit -projectPath &lt;probe copy&gt; -executeMethod
    /// Gamesim.Editor.RoomScopeProbeBuild.BuildDevelopment -gamesimProbeOutput &lt;folder&gt; -logFile &lt;log&gt;</c>;
    /// then <c>git status -- ProjectSettings</c> on the copy shows nothing Unity wrote after the method returned.</para>
    /// </summary>
    public static class RoomScopeProbeBuild
    {
        public const string OutputArgument = "-gamesimProbeOutput";
        public const string DefaultOutput = "Builds/RoomScopeProbe-Development/Gamesim.exe";
        public const string ReportName = "room-scope-probe-build.json";
        public const string UrpRuntimeResponseFile = "Unity.RenderPipelines.Universal.Runtime.rsp";
        public const string GamesimRuntimeResponseFile = "Gamesim.Runtime.rsp";
        public const string ChecksDefine = "-define:UNITY_ENABLE_CHECKS", DevelopmentDefine = "-define:DEVELOPMENT_BUILD", UmaDefine = "-define:GAMESIM_UMA";
        public const string UmaSymbol = "GAMESIM_UMA";
        public const string Defined = "defined", NotDefined = "not defined", Unknown = "unknown";
        /// <summary>What U01ProjectSetup.BuildConfiguredPort writes before the release build: the window and the preload.</summary>
        public const int ReleaseWidth = 1600, ReleaseHeight = 900;
        public const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

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
            // The release player is the review candidate: U01ProjectSetup.BuildReviewCandidate, then
            // BuildConfiguredPort in the acceptance copy, which writes the window defaults and re-adds
            // the input-action preload, saves, and builds with BuildDesktop's options. This build writes
            // nothing, so it needs those settings to hold already, and the UMA cast every probe measures.
            var differs = ReleaseSettingsDiffer();
            if (differs.Length > 0)
                throw new BuildPlayerWindow.BuildMethodException("The probe copy's player settings are not the release build's (" + string.Join("; ", differs)
                    + "): restore the committed ProjectSettings; the probe's build writes none.");
            if (!StandaloneDefines().Contains(UmaSymbol))
                throw new BuildPlayerWindow.BuildMethodException("The probe copy does not define " + UmaSymbol + ": install UMA and set the define in the copy's own "
                    + "ProjectSettings (never committed) before building, or every body the probe measures is the primitive rig's.");

            string output = OutputPath(Environment.GetCommandLineArgs());
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            string settingsBefore = SettingsFingerprint();
            var started = DateTime.UtcNow;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                // BuildDesktop's options and Development: with the settings above, the two players differ in that alone.
                options = BuildOptions.Development | BuildOptions.CleanBuildCache | BuildOptions.StrictMode,
            });
            string settingsAfter = SettingsFingerprint();
            string checks = PlayerCompile(started, out string responseFile, out bool developmentCompile, out string umaCompile);
            var summary = report.summary;
            var record = new ProbeBuildRecord
            {
                result = summary.result.ToString(), outputPath = summary.outputPath, developmentBuild = true,
                totalBytes = summary.totalSize, errors = summary.totalErrors, warnings = summary.totalWarnings,
                durationSeconds = summary.totalTime.TotalSeconds, scenes = scenes, startedUtc = started.ToString("O"),
                urpEnableChecks = checks, urpResponseFile = responseFile, urpResponseFileIsDevelopment = developmentCompile,
                gamesimUma = umaCompile,
                projectSettingsUnchanged = settingsBefore == settingsAfter,
                inputActionsPreloaded = InputActionsPreloaded(),
                messages = report.steps.SelectMany(step => step.messages)
                    .Where(message => message.type == LogType.Error || message.type == LogType.Exception)
                    .Select(message => message.content).Distinct().ToArray(),
            };
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), ReportName), JsonUtility.ToJson(record, true));
            if (!record.projectSettingsUnchanged)
                throw new BuildPlayerWindow.BuildMethodException("ProjectSettings changed during the probe's build; restore them before anything is committed.");
            if (summary.result != BuildResult.Succeeded)
                throw new BuildPlayerWindow.BuildMethodException("The probe's development build failed: " + summary.result + " (" + summary.totalErrors + " errors).");
            if (umaCompile != Defined)
                throw new BuildPlayerWindow.BuildMethodException("The player compiled Gamesim.Runtime without " + UmaSymbol + " (" + umaCompile + "): its cast is the primitive rig's.");
            Debug.Log("Room-scope probe development build succeeded: " + output + "; URP's player compile: UNITY_ENABLE_CHECKS " + checks + ".");
        }

        /// <summary>Where this copy's player settings differ from what the release build writes before it builds; empty when they agree.</summary>
        public static string[] ReleaseSettingsDiffer()
        {
            var differs = new System.Collections.Generic.List<string>();
            if (PlayerSettings.defaultScreenWidth != ReleaseWidth || PlayerSettings.defaultScreenHeight != ReleaseHeight)
                differs.Add("the window is " + PlayerSettings.defaultScreenWidth + "x" + PlayerSettings.defaultScreenHeight + ", not " + ReleaseWidth + "x" + ReleaseHeight);
            if (PlayerSettings.fullScreenMode != FullScreenMode.Windowed) differs.Add("the window mode is " + PlayerSettings.fullScreenMode + ", not Windowed");
            if (!PlayerSettings.resizableWindow) differs.Add("the window is not resizable");
            if (!InputActionsPreloaded()) differs.Add(InputActionsPath + " is not preloaded");
            return differs.ToArray();
        }

        private static bool InputActionsPreloaded()
            => PlayerSettings.GetPreloadedAssets().Any(asset => asset != null && AssetDatabase.GetAssetPath(asset) == InputActionsPath);

        private static string[] StandaloneDefines()
            => PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Standalone).Split(';').Select(symbol => symbol.Trim()).ToArray();

        /// <summary>Where the player goes: <c>-gamesimProbeOutput</c>, a folder or an .exe, else <see cref="DefaultOutput"/>.</summary>
        public static string OutputPath(string[] args)
        {
            int at = args != null ? Array.IndexOf(args, OutputArgument) : -1;
            if (at < 0 || at + 1 >= args.Length || string.IsNullOrWhiteSpace(args[at + 1])) return DefaultOutput;
            string asked = args[at + 1];
            return asked.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? asked : Path.Combine(asked, "Gamesim.exe");
        }

        /// <summary>Whether a response file defines UNITY_ENABLE_CHECKS: <see cref="Defined"/>, <see cref="NotDefined"/>, or <see cref="Unknown"/> for none.</summary>
        public static string ChecksIn(string responseFile) => DefineIn(responseFile, ChecksDefine);

        /// <summary>Whether a response file carries a <c>-define:</c> line: <see cref="Defined"/>, <see cref="NotDefined"/>, or <see cref="Unknown"/> for none.</summary>
        public static string DefineIn(string responseFile, string define)
        {
            if (responseFile == null) return Unknown;
            return responseFile.Split('\n').Any(line => line.Trim() == define) ? Defined : NotDefined;
        }

        /// <summary>
        /// The player compile just built: the newest player graph (Library/Bee/artifacts/*P.dag) whose
        /// URP runtime response file was written since <paramref name="since"/>; whether URP's runtime
        /// defines UNITY_ENABLE_CHECKS (returned) and whether Gamesim's runtime, in the same graph,
        /// defines GAMESIM_UMA (<paramref name="gamesimUma"/>). Unknown when no fresh graph is there.
        /// </summary>
        private static string PlayerCompile(DateTime since, out string path, out bool development, out string gamesimUma)
        {
            path = null;
            development = false;
            gamesimUma = Unknown;
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
            string gamesim = Path.Combine(fresh.DirectoryName, GamesimRuntimeResponseFile);
            gamesimUma = DefineIn(File.Exists(gamesim) ? File.ReadAllText(gamesim) : null, UmaDefine);
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
            public string result, outputPath, startedUtc, urpEnableChecks, urpResponseFile, gamesimUma;
            public bool developmentBuild, urpResponseFileIsDevelopment, projectSettingsUnchanged, inputActionsPreloaded;
            public ulong totalBytes;
            public int errors, warnings;
            public double durationSeconds;
            public string[] scenes, messages;
        }
    }
}
