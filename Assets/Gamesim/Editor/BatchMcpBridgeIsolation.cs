using System;
using UnityEditor;
using UnityEngine;

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
    /// EditorPrefs - and an interactive editor is left alone (the package reads the variable only in
    /// batch mode anyway). Not covered: the first domain of a run whose compiled Library predates this
    /// file. The launchers' variable covers that.</para>
    /// </summary>
    public static class BatchMcpBridgeIsolation
    {
        public const string Variable = "UNITY_MCP_DISABLE_BATCH";

        /// <summary>True when this domain's hook set the variable because the launcher had not.</summary>
        public static bool SetByHook { get; private set; }

        /// <summary>The package treats any non-blank value as off (IsNullOrWhiteSpace), so a launcher's
        /// value is kept as it is and only a blank one is filled in.</summary>
        public static bool Needed(bool batchMode, string current) => batchMode && string.IsNullOrWhiteSpace(current);

        /// <summary>Sets the variable for this process only when <see cref="Needed"/> says so; true when it did.</summary>
        public static bool Apply(bool batchMode)
        {
            if (!Needed(batchMode, Environment.GetEnvironmentVariable(Variable))) return false;
            Environment.SetEnvironmentVariable(Variable, "1", EnvironmentVariableTarget.Process);
            SetByHook = true;
            return true;
        }
    }

    /// <summary>The load-time call, kept apart so the policy above has no class constructor of its own.</summary>
    [InitializeOnLoad]
    internal static class BatchMcpBridgeIsolationHook
    {
        static BatchMcpBridgeIsolationHook()
        {
            if (BatchMcpBridgeIsolation.Apply(Application.isBatchMode))
                Debug.Log("Gamesim batch run: the launcher did not set " + BatchMcpBridgeIsolation.Variable
                    + ", so Gamesim.Editor set it for this process before the MCP bridge's Init. Set it in the launcher (Tools/README.md).");
        }
    }
}
