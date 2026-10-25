using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Assemblies;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The AI Assistant package's MCP bridge must not run in a batch editor: its client checks held batch
    /// runs at 15-26 GiB (see <see cref="BatchMcpBridgeIsolation"/>). This fails loudly in the next batch
    /// EditMode run if neither the launcher nor the load-time hook kept it off, or if the hook ran too late.
    /// </summary>
    public sealed class BatchMcpBridgeIsolationTests
    {
        private const string PackageName = "com.unity.ai.assistant";
        private const string BridgeAssembly = "Unity.AI.MCP.Editor";
        private const string BridgeTypeName = "Unity.AI.MCP.Editor.UnityMCPBridge";

        [TestCase(true, null, true)]
        [TestCase(true, "", true)]
        [TestCase(true, "  ", true)]
        [TestCase(true, "1", false)]
        [TestCase(true, "0", false)]
        [TestCase(false, null, false)]
        [TestCase(false, "", false)]
        [TestCase(false, "1", false)]
        public void OnlyABatchEditorWithoutTheVariableIsGivenIt(bool batch, string current, bool expected)
        {
            // Blank counts as unset, as the package reads it; any other value (even "0") already turns the
            // bridge off there, so a launcher's value is never overwritten.
            Assert.That(BatchMcpBridgeIsolation.Needed(batch, current), Is.EqualTo(expected));
        }

        [Test]
        public void TheVariableIsThePackagesOwnSwitch()
        {
            Assert.That(BatchMcpBridgeIsolation.Variable, Is.EqualTo("UNITY_MCP_DISABLE_BATCH"));
        }

        [Test]
        public void ABatchEditorNeverRunsTheMcpBridge()
        {
            if (!Application.isBatchMode)
            {
                Assert.That(BatchMcpBridgeIsolation.SetByHook, Is.False, "An interactive editor's environment is never changed.");
                return;
            }
            Assert.That(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(BatchMcpBridgeIsolation.Variable)), Is.False,
                "A batch editor without UNITY_MCP_DISABLE_BATCH: neither the launcher nor Gamesim.Editor's load-time hook set it.");
            // A project without the package has no bridge to keep off. Its absence is read from the manifest, not
            // from a failed lookup, so a renamed assembly or type fails below instead of passing unchecked. No
            // Assert.Ignore: the review gate counts a skipped test as a failure.
            string manifest = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Packages", "manifest.json"));
            if (!manifest.Contains("\"" + PackageName + "\"")) return;
            var assembly = CurrentAssemblies.GetLoadedAssemblies().SingleOrDefault(a => a.GetName().Name == BridgeAssembly);
            Assert.That(assembly, Is.Not.Null, PackageName + " is in the manifest but " + BridgeAssembly + " is not loaded: re-check how the package switches its bridge off.");
            AssertTheBridgeIsOff(assembly);
        }

        /// <summary>The read-only check on the package's bridge, apart from the engine calls above so it can also be
        /// exercised against the package's compiled assembly outside the editor.</summary>
        public static void AssertTheBridgeIsOff(Assembly assembly)
        {
            var bridge = assembly.GetType(BridgeTypeName, throwOnError: true);
            var running = bridge.GetProperty("IsRunning", BindingFlags.Public | BindingFlags.Static);
            Assert.That(running, Is.Not.Null, "The pinned package's public IsRunning.");
            Assert.That(running.GetValue(null), Is.EqualTo(false),
                "The MCP bridge is running in a batch editor: expect 'Failed to collect Windows signature' lines and a 15-26 GiB heap.");
            // Read-only witness that Init itself returned early, i.e. saw the variable, rather than a bridge that
            // was built and merely has not started yet.
            var instance = bridge.GetField("s_Instance", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(instance, Is.Not.Null, "Read-only witness for the pinned package's bridge instance.");
            Assert.That(instance.GetValue(null), Is.Null,
                "UnityMCPBridge.Init built a bridge in this domain: the variable was set after Init ran.");
        }
    }
}
