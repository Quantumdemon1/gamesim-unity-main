using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Keeps the GAMESIM_UMA scripting define in step with whether UMA is actually installed.
    ///
    /// UMA is a large third-party pack that this repository deliberately does not track, so a fresh
    /// clone has no Assets/UMA at all. Gamesim.Uma is constrained on this define: with UMA absent
    /// the define is absent, Unity excludes the assembly, and the project compiles with houseguests
    /// on the primitive rig. Re-import UMA from Package Manager > My Assets and it comes back.
    ///
    /// This lives in Gamesim.Editor precisely because that assembly never references UMA — it has to
    /// be able to compile in the state it is meant to detect.
    /// </summary>
    [InitializeOnLoad]
    internal static class UmaPresenceDefine
    {
        private const string Define = "GAMESIM_UMA";
        private const string UmaFolder = "Assets/UMA";

        static UmaPresenceDefine()
        {
            // Deferred: changing defines triggers a recompile, which is not something to start from
            // inside a static constructor running during assembly load.
            EditorApplication.delayCall += Sync;
        }

        private static void Sync()
        {
            bool installed = Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), UmaFolder));
            var target = NamedBuildTarget.FromBuildTargetGroup(
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));

            var defines = new List<string>(PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

            bool present = defines.Contains(Define);
            if (present == installed) return;

            if (installed) defines.Add(Define);
            else defines.Remove(Define);

            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
            Debug.Log(installed
                ? "[Gamesim] UMA detected — enabling " + Define + " so Gamesim.Uma compiles."
                : "[Gamesim] UMA is not installed — clearing " + Define + "; houseguests use the primitive rig.");
        }
    }
}
