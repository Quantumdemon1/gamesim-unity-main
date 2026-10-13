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
    /// batch tests and the exact owned humanoid-wiring entry point. Assistant 2.19.0-pre.2
    /// explicitly provides SuppressAutoStart for this purpose.
    /// No logs/assertions are ignored, no package/preferences are changed, and interactive MCP and
    /// lazy initialization remain available. Reflection avoids a dependency on its internal type.
    /// </summary>
    public static class BatchTestRelayIsolation
    {
        public const string AssistantAssembly = "Unity.AI.Assistant.Editor";
        public const string RelayTypeName = "Unity.Relay.Editor.RelayService";
        public const string HumanoidWiringMethod = "Gamesim.Editor.HumanoidClipWiring.Apply";
        public static bool Applied { get; private set; }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            var arguments = Environment.GetCommandLineArgs();
            if (!IsBatchTestRun(Application.isBatchMode, arguments)
                && !IsHumanoidWiringRun(Application.isBatchMode, arguments)) return;
            var assembly = CurrentAssemblies.GetLoadedAssemblies().SingleOrDefault(a => a.GetName().Name == AssistantAssembly);
            if (assembly == null) return; // The Assistant package is optional, not a new build dependency.
            var relay = assembly.GetType(RelayTypeName, throwOnError: true);
            SuppressEagerStart(relay);
            Applied = true;
            Debug.Log("Gamesim owned batch operation: optional Assistant relay eager startup isolated; strict Console checks remain enabled.");
        }

        public static bool IsBatchTestRun(bool batchMode, string[] arguments) => batchMode && arguments != null
            && arguments.Any(a => string.Equals(a, "-runTests", StringComparison.OrdinalIgnoreCase));

        /// <summary>The outer workflow owns the process; this only recognizes its exact generation command.</summary>
        public static bool IsHumanoidWiringRun(bool batchMode, string[] arguments)
        {
            if (!batchMode || arguments == null) return false;
            int selectors = 0;
            for (int i = 0; i < arguments.Length; i++)
            {
                string argument = arguments[i];
                if (argument == null || !argument.StartsWith("-executeMethod", StringComparison.OrdinalIgnoreCase)) continue;
                if (++selectors != 1 || !string.Equals(argument, "-executeMethod", StringComparison.Ordinal)
                    || i + 1 >= arguments.Length
                    || !string.Equals(arguments[i + 1], HumanoidWiringMethod, StringComparison.Ordinal)) return false;
            }
            return selectors == 1;
        }

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
