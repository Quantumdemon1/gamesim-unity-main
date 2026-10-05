using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assemblies;

namespace Gamesim.Editor
{
    /// <summary>
    /// The optional Assistant relay must not start unrelated background connections during native
    /// batch tests. Assistant 2.19.0-pre.2 explicitly provides SuppressAutoStart for this purpose.
    /// No logs/assertions are ignored, no package/preferences are changed, and interactive MCP and
    /// lazy initialization remain available. Reflection avoids a dependency on its internal type.
    /// </summary>
    public static class BatchTestRelayIsolation
    {
        public const string AssistantAssembly = "Unity.AI.Assistant.Editor";
        public const string RelayTypeName = "Unity.Relay.Editor.RelayService";
        public static bool Applied { get; private set; }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            if (!IsBatchTestRun(Application.isBatchMode, Environment.GetCommandLineArgs())) return;
            var assembly = CurrentAssemblies.GetLoadedAssemblies().SingleOrDefault(a => a.GetName().Name == AssistantAssembly);
            if (assembly == null) return; // The Assistant package is optional, not a new build dependency.
            var relay = assembly.GetType(RelayTypeName, throwOnError: true);
            SuppressEagerStart(relay);
            Applied = true;
            Debug.Log("Gamesim batch tests: optional Assistant relay eager startup isolated; strict Console checks remain enabled.");
        }

        public static bool IsBatchTestRun(bool batchMode, string[] arguments) => batchMode && arguments != null
            && arguments.Any(a => string.Equals(a, "-runTests", StringComparison.OrdinalIgnoreCase));

        /// <summary>Invoke only the documented zero-argument hook; never touch Instance or initialize a connection.</summary>
        public static void SuppressEagerStart(Type relay)
        {
            if (relay == null) throw new ArgumentNullException(nameof(relay));
            var hook = relay.GetMethod("SuppressAutoStart", BindingFlags.Public | BindingFlags.Static,
                binder: null, types: Type.EmptyTypes, modifiers: null);
            if (hook == null || hook.ReturnType != typeof(void))
                throw new InvalidOperationException("The installed Assistant package no longer provides its supported batch-test isolation hook.");
            hook.Invoke(null, null);
        }
    }
}
