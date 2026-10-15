using System;
using System.Linq;
using System.Reflection;
using Gamesim.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Assemblies;

namespace Gamesim.Tests.EditMode
{
    public sealed class BatchTestRelayIsolationTests
    {
        [TestCase(false, null, false)]
        [TestCase(true, null, false)]
        [TestCase(true, "-runTests", true)]
        [TestCase(false, "-runTests", false)]
        [TestCase(true, "-RUNTESTS", true)]
        [TestCase(true, "-runTests-extra", false)]
        [TestCase(true, "-executeMethod", false)]
        [TestCase(true, "C:/somewhere/runTests", false)]
        public void OnlyAnExplicitBatchTestRunIsIsolated(bool batch, string argument, bool expected)
        {
            Assert.That(BatchTestRelayIsolation.IsBatchTestRun(batch, argument == null ? null : new[] { "Unity.exe", argument }), Is.EqualTo(expected));
            Assert.That(BatchTestRelayIsolation.IsBatchTestRun(batch, Array.Empty<string>()), Is.False);
        }

        private const string WiringMethod = "Gamesim.Editor.HumanoidClipWiring.Apply";

        [TestCase(false, "-executeMethod", WiringMethod, false)]
        [TestCase(true, null, null, false)]
        [TestCase(true, "-executeMethod", WiringMethod, true)]
        [TestCase(true, "-executeMethod", "Gamesim.Editor.HumanoidClipWiring.ReimportLibrary", false)]
        [TestCase(true, "-executeMethod", "gamesim.Editor.HumanoidClipWiring.Apply", false)]
        [TestCase(true, "-EXECUTEMETHOD", WiringMethod, false)]
        [TestCase(true, "-executeMethod=Gamesim.Editor.HumanoidClipWiring.Apply", null, false)]
        [TestCase(true, "-executeMethod", null, false)]
        public void OnlyTheExactBatchWiringSelectorOptsIn(bool batch, string selector, string method, bool expected)
        {
            string[] arguments = selector == null ? null
                : method == null ? new[] { "Unity.exe", selector } : new[] { "Unity.exe", selector, method };
            Assert.That(BatchTestRelayIsolation.HumanoidWiringMethod, Is.EqualTo(WiringMethod));
            Assert.That(BatchTestRelayIsolation.IsHumanoidWiringRun(batch, arguments), Is.EqualTo(expected));
            Assert.That(BatchTestRelayIsolation.IsHumanoidWiringRun(batch, Array.Empty<string>()), Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DuplicateAndMalformedWiringSelectorsNeverOptIn(bool batch)
        {
            string[][] refused = {
                new[] { "Unity.exe", "-executeMethod", WiringMethod, "-executeMethod", WiringMethod },
                new[] { "Unity.exe", "-executeMethod", WiringMethod, "-EXECUTEMETHOD", WiringMethod },
                new[] { "Unity.exe", "-executeMethod", WiringMethod, "-executeMethod=" + WiringMethod },
                new[] { "Unity.exe", "-executeMethod-extra", WiringMethod },
                new[] { "Unity.exe", "-executeMethod", WiringMethod + " " },
                new[] { "Unity.exe", "-executeMethod", (string)null },
                new[] { "Unity.exe", "-executeMethod", "-quit" },
                new[] { "Unity.exe", "-logFile", WiringMethod }
            };
            foreach (var arguments in refused)
                Assert.That(BatchTestRelayIsolation.IsHumanoidWiringRun(batch, arguments), Is.False,
                    string.Join("|", arguments));
        }

        [Test]
        public void WiringRecognitionDoesNotBroadenLegacyBatchTestDetection()
        {
            var wiring = new[] { "Unity.exe", "-batchmode", "-projectPath", "D:/owned", "-executeMethod", WiringMethod, "-quit" };
            Assert.That(BatchTestRelayIsolation.IsHumanoidWiringRun(true, wiring), Is.True);
            Assert.That(BatchTestRelayIsolation.IsBatchTestRun(true, wiring), Is.False);
            var tests = new[] { "Unity.exe", "-RUNTESTS", "-executeMethod", "An.Unrelated.Method" };
            Assert.That(BatchTestRelayIsolation.IsBatchTestRun(true, tests), Is.True, "Existing explicit batch-test behavior stays intact.");
            Assert.That(BatchTestRelayIsolation.IsHumanoidWiringRun(true, tests), Is.False);
        }

        private static class FakeRelay
        {
            internal static int calls;
            public static object Instance => throw new InvalidOperationException("Never initialize a connection.");
            public static void SuppressAutoStart() { calls++; }
        }
        private static class WrongReturn { public static int SuppressAutoStart() => 0; }
        private static class WrongParameters { public static void SuppressAutoStart(bool ignored) { } }

        [Test]
        public void TheAdapterInvokesOnlyTheExplicitHookWithoutResolvingAnInstance()
        {
            FakeRelay.calls = 0;
            BatchTestRelayIsolation.SuppressEagerStart(typeof(FakeRelay));
            Assert.That(FakeRelay.calls, Is.EqualTo(1));
        }

        [Test]
        public void AnInstalledButIncompatibleHookFailsVisibly()
        {
            Assert.Throws<ArgumentNullException>(() => BatchTestRelayIsolation.SuppressEagerStart(null));
            Assert.Throws<InvalidOperationException>(() => BatchTestRelayIsolation.SuppressEagerStart(typeof(object)));
            Assert.Throws<InvalidOperationException>(() => BatchTestRelayIsolation.SuppressEagerStart(typeof(WrongReturn)));
            Assert.Throws<InvalidOperationException>(() => BatchTestRelayIsolation.SuppressEagerStart(typeof(WrongParameters)));
        }

        [Test]
        public void TheInstalledPackageContractIsHonoredWithoutChangingInteractiveEditors()
        {
            var assembly = CurrentAssemblies.GetLoadedAssemblies().SingleOrDefault(a => a.GetName().Name == BatchTestRelayIsolation.AssistantAssembly);
            bool batchTests = BatchTestRelayIsolation.IsBatchTestRun(Application.isBatchMode, Environment.GetCommandLineArgs());
            if (assembly == null)
            {
                Assert.That(BatchTestRelayIsolation.Applied, Is.False, "No optional package means nothing to isolate.");
                return;
            }
            var relay = assembly.GetType(BatchTestRelayIsolation.RelayTypeName, throwOnError: true);
            var hook = relay.GetMethod("SuppressAutoStart", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            Assert.That(hook, Is.Not.Null, "The pinned package provides its batch-test hook.");
            Assert.That(hook.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(BatchTestRelayIsolation.Applied, Is.EqualTo(batchTests));
            if (batchTests)
            {
                var flag = relay.GetField("s_AutoStartSuppressed", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(flag, Is.Not.Null, "Read-only witness for the pinned package's actual hook, not an ignored error.");
                Assert.That(flag.GetValue(null), Is.EqualTo(true));
            }
        }
    }
}
