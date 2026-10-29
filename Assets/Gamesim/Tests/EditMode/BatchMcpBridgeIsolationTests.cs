using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Gamesim.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Assemblies;
using Witness = Gamesim.Editor.BatchMcpBridgeIsolation.BridgeWitness;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The AI Assistant package's MCP bridge must not run in a batch editor: its client checks held batch
    /// runs at 15-26 GiB (see <see cref="BatchMcpBridgeIsolation"/>). This fails loudly in the next batch
    /// EditMode run if neither the launcher nor the load-time hook kept it off, and in any editor if a
    /// package upgrade moves the switch or the Init the hook has to precede.
    /// </summary>
    public sealed class BatchMcpBridgeIsolationTests
    {
        [TestCase(true, false, null, true)]
        [TestCase(true, false, "", true)]
        [TestCase(true, false, "  ", true)]
        [TestCase(true, false, "1", false)]
        [TestCase(true, false, "0", false)]
        [TestCase(true, true, null, false)]
        [TestCase(true, true, "", false)]
        [TestCase(false, false, null, false)]
        [TestCase(false, false, "", false)]
        [TestCase(false, false, "1", false)]
        public void OnlyABatchEditorWithoutTheVariableIsGivenIt(bool batch, bool importWorker, string current, bool expected)
        {
            // Blank counts as unset, as the package reads it; any other value (even "0") already turns the
            // bridge off there, so a launcher's value is never overwritten. An asset import worker runs in batch
            // mode but belongs to the editor that started it, so an interactive editor's workers are left alone.
            Assert.That(BatchMcpBridgeIsolation.Needed(batch, importWorker, current), Is.EqualTo(expected));
        }

        [Test]
        public void ThePackagesInitStillReadsTheSwitch()
        {
            // Installed directly or as another package's dependency. Not installed: no bridge and no switch to
            // check. No Assert.Ignore: the review gate counts a skipped test as a failure.
            var package = UnityEditor.PackageManager.PackageInfo.FindForPackageName(BatchMcpBridgeIsolation.PackageName);
            if (package == null) return;
            string source = Path.Combine(package.resolvedPath, "Modules", BatchMcpBridgeIsolation.BridgeAssembly, "UnityMCPBridge.cs");
            string release = BatchMcpBridgeIsolation.PackageName + " " + package.version;
            Assert.That(File.Exists(source), Is.True, release + " has no " + source + ": re-check how it switches its MCP bridge off in batch mode.");
            string text = File.ReadAllText(source);
            Assert.That(text.Contains("GetEnvironmentVariable(\"" + BatchMcpBridgeIsolation.Variable + "\")"), Is.True,
                release + " no longer reads " + BatchMcpBridgeIsolation.Variable + " in UnityMCPBridge: the runners' switch and Gamesim.Editor's hook do nothing.");
            Assert.That(Regex.IsMatch(text, @"\[InitializeOnLoadMethod\]\s*public static void Init\(\)"), Is.True,
                release + "'s UnityMCPBridge.Init is no longer an [InitializeOnLoadMethod]: Gamesim.Editor's [InitializeOnLoad] hook may no longer run before it.");
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
            // Absence comes from the package manager, not from a failed lookup, so a renamed assembly or type fails
            // below instead of passing unchecked.
            if (UnityEditor.PackageManager.PackageInfo.FindForPackageName(BatchMcpBridgeIsolation.PackageName) == null)
            {
                TestContext.WriteLine(BatchMcpBridgeIsolation.PackageName + " is not installed: no MCP bridge to keep off.");
                return;
            }
            var assembly = CurrentAssemblies.GetLoadedAssemblies().SingleOrDefault(a => a.GetName().Name == BatchMcpBridgeIsolation.BridgeAssembly);
            Assert.That(assembly, Is.Not.Null, BatchMcpBridgeIsolation.PackageName + " is installed but " + BatchMcpBridgeIsolation.BridgeAssembly
                + " is not loaded: re-check how the package switches its bridge off.");
            var witness = AssertTheBridgeIsOff(assembly, out string detail);
            // Only a domain whose own hook set the variable exercises the load order; a later domain (after any
            // reload) and a launcher's variable do not. The lead reads this line in the results XML.
            TestContext.WriteLine(Describe(witness, BatchMcpBridgeIsolation.SetByHook, detail));
        }

        /// <summary>The read-only check on the package's bridge, apart from the engine calls above so it can also be
        /// exercised against the package's compiled assembly outside the editor.</summary>
        public static Witness AssertTheBridgeIsOff(Assembly assembly, out string detail)
        {
            var bridge = assembly.GetType(BatchMcpBridgeIsolation.BridgeTypeName, throwOnError: true);
            var running = bridge.GetProperty("IsRunning", BindingFlags.Public | BindingFlags.Static);
            Assert.That(running, Is.Not.Null, "The pinned package's public IsRunning.");
            Assert.That(running.GetValue(null), Is.EqualTo(false),
                "The MCP bridge is running in a batch editor: expect 'Failed to collect Windows signature' lines and a 15-26 GiB heap.");
            // Read-only witness that Init itself returned early, i.e. saw the variable, rather than a bridge that
            // was built and merely has not started yet.
            var witness = BatchMcpBridgeIsolation.Witness(assembly, out detail);
            Assert.That(witness, Is.Not.EqualTo(Witness.BuiltByInit),
                "UnityMCPBridge.Init built a bridge in this domain: the variable was set after Init ran.");
            Assert.That(witness, Is.Not.EqualTo(Witness.Unreadable), "Read-only witness for the pinned package's bridge: " + detail + ".");
            return witness;
        }

        /// <summary>Which case this run's bridge check covered, for the results XML.</summary>
        public static string Describe(Witness witness, bool setByHook, string detail)
        {
            if (witness == Witness.KeptOffBySettings)
                return "MCP settings keep the bridge off in batch mode (" + detail + "): ordering not exercised";
            return setByHook
                ? "MCP switch set by the hook in this domain (" + detail + "): ordering exercised"
                : "MCP switch inherited (launcher or an earlier domain; " + detail + "): ordering not exercised";
        }
    }
}
