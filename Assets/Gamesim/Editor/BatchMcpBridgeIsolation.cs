using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assemblies;

namespace Gamesim.Editor
{
    /// <summary>
    /// Keeps the AI Assistant package's MCP bridge (<c>UnityMCPBridge</c> in Unity.AI.MCP.Editor) from
    /// starting in a batch-mode editor. The bridge checks the executable of every MCP client that
    /// connects; under Unity's Mono that check reads the whole file and leaves about five times its size
    /// in short-lived garbage (1.4-1.6 GiB for claude.exe or codex.exe), and Boehm keeps the heap at its
    /// high-water mark, so batch runs sat at 15-26 GiB of private commit. With the bridge off the whole
    /// UMA-build PlayMode suite peaked at 8.4 GiB (2026-10-25). The package's own batch switch is the
    /// <c>UNITY_MCP_DISABLE_BATCH</c> environment variable (UnityMCPBridge.IsAllowedInBatchMode). The
    /// Tools launchers set it for the Unity they start; this is the safety net for one that does not.
    /// <para>Why a class constructor: UnityMCPBridge.Init is an [InitializeOnLoadMethod], and in every
    /// domain Unity runs all [InitializeOnLoad] class constructors (EditorAssemblies.
    /// ProcessInitializeOnLoadAttributes) before any [InitializeOnLoadMethod]
    /// (ProcessInitializeOnLoadMethodAttributes), whatever the assembly order. An InitializeOnLoadMethod
    /// of ours would race Init. Stopping the bridge is no substitute: UnityMCPBridge.Stop() leaves it
    /// scheduled to start again at the next play-mode change, and UnityMCPBridge.Enabled saves the
    /// machine-wide MCP settings.</para>
    /// <para>Nothing persistent changes: the variable is set for this editor process (and the processes
    /// it starts) only - never the user's or the machine's environment, the MCP settings or any
    /// EditorPrefs. An interactive editor is left alone, and so are asset import workers: they run in
    /// batch mode but belong to whichever editor started them, so an interactive editor's workers keep
    /// what that editor gives them, and an owned run's workers inherit the launcher's variable (or this
    /// hook's, when they start after it). Not covered: the first domain of a run whose compiled Library
    /// predates this file. The launchers' variable covers that.</para>
    /// <para>The order is checked where it matters: in the domain where the hook had to set the variable,
    /// it takes one read-only look at the bridge after that domain's InitializeOnLoadMethods
    /// (<see cref="Witness"/>) and logs the verdict. A later domain inherits the variable, so it proves
    /// nothing about the order.</para>
    /// </summary>
    public static class BatchMcpBridgeIsolation
    {
        public const string Variable = "UNITY_MCP_DISABLE_BATCH";
        public const string PackageName = "com.unity.ai.assistant";
        public const string BridgeAssembly = "Unity.AI.MCP.Editor";
        public const string BridgeTypeName = "Unity.AI.MCP.Editor.UnityMCPBridge";
        private const string SettingsManagerTypeName = "Unity.AI.MCP.Editor.Settings.MCPSettingsManager";
        private const string Prefix = "Gamesim batch run: ";

        /// <summary>Why the package's bridge is or is not there, read after the domain's UnityMCPBridge.Init.</summary>
        public enum BridgeWitness
        {
            /// <summary>The package no longer has the members this reads: its switch must be re-checked.</summary>
            Unreadable,
            /// <summary>Init built a bridge: it ran before the variable was set (or ignored it).</summary>
            BuiltByInit,
            /// <summary>No bridge, but the MCP settings would have kept it off anyway: the order was not exercised.</summary>
            KeptOffBySettings,
            /// <summary>No bridge although both settings let Init build one: Init saw the variable.</summary>
            KeptOffByTheVariable,
        }

        /// <summary>True when this domain's hook set the variable because the launcher had not.</summary>
        public static bool SetByHook { get; private set; }

        /// <summary>The package treats any non-blank value as off (IsNullOrWhiteSpace), so a launcher's
        /// value is kept as it is and only a blank one is filled in. Only the batch editor itself is given
        /// it, never an asset import worker (see the class remarks).</summary>
        public static bool Needed(bool batchMode, bool importWorker, string current) =>
            batchMode && !importWorker && string.IsNullOrWhiteSpace(current);

        /// <summary>Sets the variable for this process only when <see cref="Needed"/> says so; true when it did.</summary>
        public static bool Apply(bool batchMode, bool importWorker)
        {
            if (!Needed(batchMode, importWorker, Environment.GetEnvironmentVariable(Variable))) return false;
            Environment.SetEnvironmentVariable(Variable, "1", EnvironmentVariableTarget.Process);
            SetByHook = true;
            return true;
        }

        /// <summary>
        /// A read-only look at the package's bridge once this domain's UnityMCPBridge.Init has run: its private
        /// <c>s_Instance</c> (Init builds it synchronously, through EnsureInstance) and the two MCP settings Init
        /// reads, <c>bridgeEnabled</c> and <c>batchModeEnabled</c> (through the package's cached copy; nothing is
        /// saved). With both settings on and no instance, Init can only have returned at IsAllowedInBatchMode's
        /// variable check, so it saw the variable.
        /// </summary>
        public static BridgeWitness Witness(Assembly assembly, out string detail)
        {
            try
            {
                var instance = assembly.GetType(BridgeTypeName, false)?.GetField("s_Instance", BindingFlags.NonPublic | BindingFlags.Static);
                if (instance == null)
                {
                    detail = "no static field " + BridgeTypeName + ".s_Instance";
                    return BridgeWitness.Unreadable;
                }
                if (instance.GetValue(null) != null)
                {
                    detail = "UnityMCPBridge.Init built a bridge in this domain";
                    return BridgeWitness.BuiltByInit;
                }
                var settings = assembly.GetType(SettingsManagerTypeName, false)
                    ?.GetProperty("Settings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
                var bridgeEnabled = settings?.GetType().GetField("bridgeEnabled")?.GetValue(settings) as bool?;
                var batchModeEnabled = settings?.GetType().GetField("batchModeEnabled")?.GetValue(settings) as bool?;
                if (bridgeEnabled == null || batchModeEnabled == null)
                {
                    detail = "no bridgeEnabled and batchModeEnabled in " + SettingsManagerTypeName + ".Settings";
                    return BridgeWitness.Unreadable;
                }
                detail = "bridgeEnabled " + bridgeEnabled.Value + ", batchModeEnabled " + batchModeEnabled.Value;
                return bridgeEnabled.Value && batchModeEnabled.Value ? BridgeWitness.KeptOffByTheVariable : BridgeWitness.KeptOffBySettings;
            }
            catch (TargetInvocationException e)
            {
                detail = "reading the MCP settings threw " + (e.InnerException ?? e).GetType().Name;
                return BridgeWitness.Unreadable;
            }
        }

        /// <summary>The line the hook logs for its one look at the bridge, and its log type: an error only
        /// when the order this class relies on did not hold.</summary>
        public static string WitnessLine(BridgeWitness witness, string detail, out LogType type)
        {
            switch (witness)
            {
                case BridgeWitness.KeptOffByTheVariable:
                    type = LogType.Log;
                    return Prefix + "the MCP bridge's Init ran after Gamesim.Editor set " + Variable + " and built no bridge ("
                        + detail + "): load order confirmed in this domain.";
                case BridgeWitness.KeptOffBySettings:
                    type = LogType.Log;
                    return Prefix + "the MCP settings keep the bridge off in batch mode anyway (" + detail
                        + "): the load order was not exercised.";
                case BridgeWitness.BuiltByInit:
                    type = LogType.Error;
                    return Prefix + "the MCP bridge's Init built a bridge before Gamesim.Editor set " + Variable
                        + ": the load order BatchMcpBridgeIsolation relies on does not hold. Set the variable in the launcher.";
                default:
                    type = LogType.Warning;
                    return Prefix + "could not read the MCP bridge (" + detail + "): re-check how " + PackageName
                        + " switches its bridge off in batch mode.";
            }
        }
    }

    /// <summary>The load-time call, kept apart so the policy above has no class constructor of its own.</summary>
    [InitializeOnLoad]
    internal static class BatchMcpBridgeIsolationHook
    {
        static BatchMcpBridgeIsolationHook()
        {
            if (!BatchMcpBridgeIsolation.Apply(Application.isBatchMode, AssetDatabase.IsAssetImportWorkerProcess())) return;
            Debug.Log("Gamesim batch run: the launcher did not set " + BatchMcpBridgeIsolation.Variable
                + ", so Gamesim.Editor set it for this process before the MCP bridge's Init. Set it in the launcher (Tools/README.md).");
            // Only this domain can show whether the order held: every later one inherits the variable.
            EditorApplication.delayCall += ReportTheBridge;
        }

        /// <summary>Runs on the first editor update, after every InitializeOnLoadMethod of the domain, Init's included.</summary>
        private static void ReportTheBridge()
        {
            var assembly = CurrentAssemblies.GetLoadedAssemblies().FirstOrDefault(a => a.GetName().Name == BatchMcpBridgeIsolation.BridgeAssembly);
            if (assembly == null)
            {
                if (UnityEditor.PackageManager.PackageInfo.FindForPackageName(BatchMcpBridgeIsolation.PackageName) != null)
                    Debug.LogWarning("Gamesim batch run: " + BatchMcpBridgeIsolation.PackageName + " is installed but "
                        + BatchMcpBridgeIsolation.BridgeAssembly + " is not loaded: re-check how the package switches its bridge off.");
                return;
            }
            var witness = BatchMcpBridgeIsolation.Witness(assembly, out string detail);
            string line = BatchMcpBridgeIsolation.WitnessLine(witness, detail, out LogType type);
            Debug.unityLogger.Log(type, (object)line);
        }
    }
}
