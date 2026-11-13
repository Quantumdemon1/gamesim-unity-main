using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Uma.Tests
{
    /// <summary>
    /// The lead's way to run a face-fit export job in a Unity batch: with GAMESIM_FACE_EXPORT_JOB set to a job file
    /// (outside the project), this runs that job through the editor's export tool and fails loudly on anything but a
    /// complete run; a run its budget stopped fails with "resume: ..." (run the same batch again to continue). Unset, it
    /// passes at once with one line in the log: batch runs execute every test, so the variable is the only gate.
    ///
    /// <para>A fixture of its own so a narrowed names file can select it alone (the export batch), and nothing else
    /// selects it by accident. The 40-minute timeout covers the job's default 30-minute budget plus the builds around it.</para>
    /// </summary>
    public sealed class UmaFaceFitExportRunPlayModeTests
    {
        [UnityTest, Timeout(2400000)]
        public IEnumerator TheExportJobNamedInTheEnvironment_RunsToCompletion()
        {
            string job = Environment.GetEnvironmentVariable(UmaFaceFitTool.JobVariable);
            if (string.IsNullOrEmpty(job))
            {
                Debug.Log("[Gamesim.Uma] Face-fit export run: " + UmaFaceFitTool.JobVariable + " is not set, so there is no job to run.");
                yield break;
            }
            // UMA warns about bones some hair lacks; the export's own failures come back as its outcome.
            LogAssert.ignoreFailingMessages = true;
            try
            {
                string outcome = null;
                yield return UmaFaceFitTool.Run(job, line => Debug.Log("[Gamesim.Uma] face-fit: " + line), result => outcome = result);
                Assert.That(outcome, Is.Not.Null, "The export of " + job + " ended without an outcome.");
                Debug.Log("[Gamesim.Uma] face-fit " + outcome);
                Assert.That(outcome, Does.StartWith("complete"), "The export of " + job + " did not complete: " + outcome);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }
    }

    /// <summary>
    /// The face-fit export tool, reached by reflection: it lives in the editor assembly Gamesim.Uma.Editor, which this
    /// test assembly does not reference (and gets no reference to). Every missing piece fails with its own message.
    /// </summary>
    internal static class UmaFaceFitTool
    {
        public const string JobVariable = "GAMESIM_FACE_EXPORT_JOB";
        private const string AssemblyName = "Gamesim.Uma.Editor", TypeName = "Gamesim.Uma.Editor.UmaFaceFitExport";

        public static Type Type()
        {
            // By its assembly-qualified name: null when the editor assembly is not there (a player, a build without UMA) or
            // holds no such type.
            var type = System.Type.GetType(TypeName + ", " + AssemblyName, throwOnError: false);
            Assert.That(type, Is.Not.Null, "No type " + TypeName + " in the assembly " + AssemblyName
                + ": the face-fit export is editor code, so it runs only in the editor's play mode, with UMA.");
            return type;
        }

        private static IEnumerator Call(string method, params object[] arguments)
        {
            var info = Type().GetMethod(method, BindingFlags.Public | BindingFlags.Static);
            Assert.That(info, Is.Not.Null, TypeName + " has no public static method " + method + ".");
            Assert.That(info.GetParameters().Length, Is.EqualTo(arguments.Length), TypeName + "." + method + " takes " + info.GetParameters().Length + " arguments, not " + arguments.Length + ".");
            return (IEnumerator)info.Invoke(null, arguments);
        }

        /// <summary>UmaFaceFitExport.Run(jobPath, log, outcome): outcome gets "complete: ...", "resume: ..." or "error: ...".</summary>
        public static IEnumerator Run(string jobPath, Action<string> log, Action<string> outcome) => Call("Run", jobPath, log, outcome);

        /// <summary>UmaFaceFitExport.BuildBare(race, parent, done): a bare body of the race, built as the game builds one.</summary>
        public static IEnumerator BuildBare(string race, Transform parent, Action<UMA.CharacterSystem.DynamicCharacterAvatar, string> done) =>
            Call("BuildBare", race, parent, done);

        /// <summary>UmaFaceFitExport.Capture(avatar, dna, timeout, done): the body's vertices in the export frame, x y z per vertex.</summary>
        public static IEnumerator Capture(UMA.CharacterSystem.DynamicCharacterAvatar avatar, System.Collections.Generic.IDictionary<string, float> dna,
            double timeoutSeconds, Action<float[], string> done) => Call("Capture", avatar, dna, timeoutSeconds, done);
    }
}
